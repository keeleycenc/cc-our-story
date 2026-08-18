// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Options;
using OurStory.Data;
using OurStory.Services.LlmAtmosphere;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages.Atmosphere;

/// <summary>
/// 氛围组总览：总开关、几条公用规矩，还有角色清单
/// </summary>
public class IndexModel(
    ActiveConfiguration configuration,
    ILlmAtmosphereService atmosphere,
    OurStoryDbContext db) : PageModel {
    /// <summary>
    /// 调试时能选的话题条数，取最近发布的这几条
    /// </summary>
    private const int TopicChoices = 10;

    /// <summary>
    /// 获取或设置表单输入
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// 获取现有的角色，按后台里排的顺序
    /// </summary>
    public IReadOnlyList<LlmAtmosphereMember> Members { get; private set; } = [];

    /// <summary>
    /// 获取每个角色到目前为止留了多少条言，键是角色标识
    /// </summary>
    public IReadOnlyDictionary<string, int> Counts { get; private set; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取已经删掉的角色留下的历史留言条数
    /// </summary>
    public int OrphanCount { get; private set; }

    /// <summary>
    /// 获取配置文件的位置，写不进去时页面上要说清楚是哪个文件
    /// </summary>
    public string ConfigFilePath => configuration.FilePath;

    /// <summary>
    /// 获取或设置保存没成功时的原因
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 获取可以拿来当话题的记录，最近发布的排前面
    /// </summary>
    public IReadOnlyList<(int Id, string Title)> Topics { get; private set; } = [];

    /// <summary>
    /// 获取刚才那次「立即触发」的回执；没点过时为 null
    /// </summary>
    public AtmosphereProbe? Probe { get; private set; }

    /// <summary>
    /// 获取刚才试的是哪个角色，用来把回执贴回它那张卡片上
    /// </summary>
    public string? ProbedMemberId { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        var options = configuration.LlmAtmosphere;

        Input = new InputModel {
            Enabled = options.Enabled,
            IncludeProtected = options.IncludeProtected,
            MaxCommentsPerMoment = options.MaxCommentsPerMoment,
            QuietMinutes = options.QuietMinutes,
            RecentDays = options.RecentDays,
            SweepMinutes = options.SweepMinutes,
            TimeoutSeconds = options.TimeoutSeconds,
            MaxImages = options.MaxImages
        };

        await LoadAsync(cancellationToken);
    }

    /// <summary>
    /// 保存这几条公用规矩
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存结果</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) {
        if (!ModelState.IsValid) {
            Error = string.Join("；", ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage));
            await LoadAsync(cancellationToken);
            return Page();
        }

        if (!configuration.Update(Save, out var error)) {
            Error = $"{ConfigFilePath} 无法写入：{error}";
            await LoadAsync(cancellationToken);
            return Page();
        }

        TempData["Flash"] = "氛围组设置已经保存。";
        return RedirectToPage();
    }

    /// <summary>
    /// 删掉一个角色。它留过的言都还在，只是往后不再开口
    /// </summary>
    /// <param name="id">角色标识</param>
    /// <returns>删除结果</returns>
    public IActionResult OnPostDelete(string id) {
        var removed = configuration.Update(
            next => {
                var member = next.LlmAtmosphere.Find(id);
                if (member is not null) {
                    _ = next.LlmAtmosphere.Members.Remove(member);
                }
            },
            out var error);

        TempData["Flash"] = removed
            ? "角色已经删掉，它之前留下的话还在。"
            : $"{ConfigFilePath} 无法写入：{error}";

        return RedirectToPage();
    }

    /// <summary>
    /// 复制指定角色，并创建一个新的角色副本。
    /// </summary>
    /// <param name="id">需要复制的角色唯一标识符。</param>
    /// <returns>复制成功后进入新角色编辑页；失败时返回角色列表。</returns>
    public IActionResult OnPostDuplicate(string id) {
        string? copyId = null;

        var saved = configuration.Update(
            next => {
                var options = next.LlmAtmosphere;
                if (options.Find(id) is not { } source) {
                    return;
                }

                var copy = source.CopyAs(
                    options.NewId(),
                    options.UniqueName($"{source.Name} 副本"));

                options.Members.Add(copy);
                copyId = copy.Id;
            },
            out var error);

        if (!saved) {
            TempData["Flash"] = $"{ConfigFilePath} 无法写入：{error}";
            return RedirectToPage();
        }

        if (copyId is null) {
            TempData["Flash"] = "未找到需要复制的角色，可能已经被删除。";
            return RedirectToPage();
        }

        TempData["Flash"] = "角色已复制完成。调整名称和人设后即可启用，新角色默认处于停用状态。";
        return RedirectToPage("Edit", new { id = copyId });
    }

    /// <summary>
    /// 立即触发指定角色生成一次互动内容，不受触发概率与延迟时间限制
    /// </summary>
    /// <param name="id">角色唯一标识符</param>
    /// <param name="topicId">作为互动上下文的点点滴滴记录标识；为 0 时使用最近发布的记录</param>
    /// <param name="persist">指示是否将生成内容写入评论区；为 false 时仅返回预览结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含本次执行结果的页面响应</returns>
    public async Task<IActionResult> OnPostTestAsync(
        string id,
        int topicId,
        bool persist,
        CancellationToken cancellationToken) {
        ProbedMemberId = id;

        try {
            Probe = await atmosphere.ProbeAsync(id, topicId, persist, cancellationToken);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            Probe = AtmosphereProbe.Blocked($"调用时抛了异常：{exception.Message}");
        }

        await OnGetAsync(cancellationToken);
        return Page();
    }

    #region 私有方法

    private async Task LoadAsync(CancellationToken cancellationToken) {
        Members = [.. configuration.LlmAtmosphere.Members];

        var tallies = await db.Comments
            .Where(comment => comment.LlmMemberId != null)
            .GroupBy(comment => comment.LlmMemberId!)
            .Select(group => new { MemberId = group.Key, Count = group.Count() })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Counts = tallies.ToDictionary(item => item.MemberId, item => item.Count, StringComparer.OrdinalIgnoreCase);

        Topics = await db.Moments
            .AsNoTracking()
            .Where(moment => moment.Status == MomentStatus.Published && moment.AllowComment)
            .OrderByDescending(moment => moment.CreatedAt)
            .Take(TopicChoices)
            .Select(moment => new ValueTuple<int, string>(moment.Id, moment.Title))
            .ToListAsync(cancellationToken);

        OrphanCount = tallies
            .Where(item => configuration.LlmAtmosphere.Find(item.MemberId) is null)
            .Sum(item => item.Count);
    }

    private void Save(OurStoryConfiguration next) {
        var options = next.LlmAtmosphere;

        options.Enabled = Input.Enabled;
        options.IncludeProtected = Input.IncludeProtected;
        options.MaxCommentsPerMoment = Input.MaxCommentsPerMoment;
        options.QuietMinutes = Input.QuietMinutes;
        options.RecentDays = Input.RecentDays;
        options.SweepMinutes = Input.SweepMinutes;
        options.TimeoutSeconds = Input.TimeoutSeconds;
        options.MaxImages = Input.MaxImages;
    }

    #endregion

    /// <summary>
    /// 几条对所有角色都生效的规矩
    /// </summary>
    public class InputModel {
        /// <summary>
        /// 获取或设置总开关
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 获取或设置上锁的记录要不要也发给模型
        /// </summary>
        public bool IncludeProtected { get; set; }

        /// <summary>
        /// 获取或设置一条记录上最多留几条
        /// </summary>
        [Range(1, 50, ErrorMessage = "一条记录上最多留 1 到 50 条")]
        public int MaxCommentsPerMoment { get; set; } = 6;

        /// <summary>
        /// 获取或设置两条氛围组留言之间至少隔多少分钟
        /// </summary>
        [Range(0, 1440, ErrorMessage = "间隔要在 0 到 1440 分钟之间")]
        public int QuietMinutes { get; set; } = 30;

        /// <summary>
        /// 获取或设置巡检回看多少天
        /// </summary>
        [Range(1, 90, ErrorMessage = "回看天数要在 1 到 90 之间")]
        public int RecentDays { get; set; } = 3;

        /// <summary>
        /// 获取或设置巡检间隔分钟数
        /// </summary>
        [Range(1, 720, ErrorMessage = "巡检间隔要在 1 到 720 分钟之间")]
        public int SweepMinutes { get; set; } = 20;

        /// <summary>
        /// 获取或设置单次调用超时秒数
        /// </summary>
        [Range(5, 300, ErrorMessage = "超时要在 5 到 300 秒之间")]
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// 获取或设置一次最多随正文发几张图
        /// </summary>
        [Range(0, 10, ErrorMessage = "一次最多发 0 到 10 张图")]
        public int MaxImages { get; set; } = 3;
    }
}
