// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Options;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Comments;
using OurStory.Services.Settings;

namespace OurStory.Services.LlmAtmosphere;

internal sealed class LlmAtmosphereService(
    OurStoryDbContext db,
    ActiveConfiguration configuration,
    ISettingsService settings,
    ICommentService comments,
    IResponsesClient client,
    IMomentImageSource images,
    SiteClock clock,
    ILogger<LlmAtmosphereService> logger) : ILlmAtmosphereService {
    public async Task<bool> RunAsync(LlmAtmosphereTrigger trigger, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(trigger);

        var options = configuration.LlmAtmosphere;
        if (options.Find(trigger.MemberId) is not { IsUsable: true } member) {
            return false;
        }

        var moment = await db.Moments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == trigger.MomentId, cancellationToken);

        if (!Speakable(moment, options)) {
            return false;
        }

        var history = await HistoryAsync(trigger.MomentId, cancellationToken);
        if (!Wanted(trigger, member, history, options)) {
            return false;
        }

        var site = await settings.GetAsync(cancellationToken);
        var target = trigger.ParentCommentId is { } parentId
            ? history.FirstOrDefault(comment => comment.Id == parentId)
            : null;

        var scene = history
            .TakeLast(AtmospherePrompt.CommentLimit)
            .Select(comment => ToScene(comment, member, site, history))
            .ToList();

        var (text, _) = await AskAsync(
            member,
            moment!,
            scene,
            target is null ? null : ToScene(target, member, site, history),
            options,
            cancellationToken);

        if (text.Length == 0) {
            return false;
        }

        _ = await comments.AddAsync(
            new CommentSubmission {
                MomentId = trigger.MomentId,
                ParentId = trigger.ParentCommentId,
                AuthorName = member.Name,
                Content = text,
                LlmMemberId = member.Id,
                LlmAvatarUrl = NullIfEmpty(member.AvatarUrl)
            },
            cancellationToken);

        logger.LogInformation("氛围组「{Member}」在《{Title}》下面留了一句。", member.Name, moment!.Title);
        return true;
    }

    public async Task<IReadOnlyList<LlmAtmosphereTrigger>> SweepAsync(CancellationToken cancellationToken = default) {
        var options = configuration.LlmAtmosphere;
        var members = options.ActiveMembers;
        if (members.Count == 0) {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-Math.Clamp(options.RecentDays, 1, 90));

        var recent = await db.Moments
            .Where(moment => moment.Status == MomentStatus.Published
                && moment.AllowComment
                && moment.CreatedAt >= since)
            .Select(moment => new { moment.Id, moment.Password })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var planned = new List<LlmAtmosphereTrigger>();

        foreach (var moment in recent) {
            if (!string.IsNullOrEmpty(moment.Password) && !options.IncludeProtected) {
                continue;
            }

            var history = await HistoryAsync(moment.Id, cancellationToken);
            planned.AddRange(Plan(moment.Id, history, members, options, now));
        }

        return planned;
    }

    public async Task<AtmosphereProbe> ProbeAsync(
        string memberId,
        int momentId,
        bool persist,
        CancellationToken cancellationToken = default) {
        var options = configuration.LlmAtmosphere;

        if (options.Find(memberId) is not { IsConfigured: true } member) {
            return AtmosphereProbe.Blocked("该角色的服务地址、模型或 API Key 配置不完整。");
        }

        var moment = momentId > 0
            ? await db.Moments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == momentId, cancellationToken)
            : await db.Moments
                .Where(item => item.Status == MomentStatus.Published && item.AllowComment)
                .OrderByDescending(item => item.CreatedAt)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

        if (moment is null) {
            return AtmosphereProbe.Blocked("暂无适合互动的点点滴滴，请先发布一条内容后再进行测试。");
        }

        // 手动测试同样遵守发布状态与隐私设置，不绕过正常的内容保护规则
        if (moment.Status != MomentStatus.Published) {
            return AtmosphereProbe.Blocked($"《{moment.Title}》目前还是草稿，草稿内容不会发送给模型。");
        }

        if (moment.IsProtected && !options.IncludeProtected) {
            return AtmosphereProbe.Blocked(
                $"《{moment.Title}》是一条上锁记录。若要使用它进行测试，请先开启「允许氛围组参与上锁记录」。");
        }

        var site = await settings.GetAsync(cancellationToken);
        var history = await HistoryAsync(moment.Id, cancellationToken);

        var scene = history
            .TakeLast(AtmospherePrompt.CommentLimit)
            .Select(comment => ToScene(comment, member, site, history))
            .ToList();

        var (text, failure) = await AskAsync(member, moment, scene, null, options, cancellationToken);

        if (text.Length == 0) {
            return AtmosphereProbe.Blocked($"{member.Name} 这次没有生成可用内容：{Explain(failure)}");
        }

        if (!persist) {
            return new AtmosphereProbe(
                true,
                text,
                $"{member.Name} 这次想留下：");
        }

        _ = await comments.AddAsync(
            new CommentSubmission {
                MomentId = moment.Id,
                AuthorName = member.Name,
                Content = text,
                LlmMemberId = member.Id,
                LlmAvatarUrl = NullIfEmpty(member.AvatarUrl)
            },
            cancellationToken);

        logger.LogInformation(
            "后台手动触发氛围组角色「{Member}」在《{Title}》下完成了一次留言。",
            member.Name,
            moment.Title);

        return new AtmosphereProbe(
            true,
            text,
            $"{member.Name} 已在《{moment.Title}》下留下留言：",
            Saved: true);
    }

    #region 私有方法

    private static string Explain(ResponsesFailure failure) => failure switch {
        ResponsesFailure.Unauthorized => "API Key 无效，请检查是否填写正确，或密钥是否已经失效。",
        ResponsesFailure.Forbidden => "当前账号暂时没有访问该模型的权限，也可能是服务商尚未为该账号开放 Responses 接口。",
        ResponsesFailure.RateLimited => "请求频率过高，模型服务当前限流，请稍后重试。",
        ResponsesFailure.Unreachable => "暂时无法连接到模型服务，可能是网络不稳定，或本次请求等待超时。",
        ResponsesFailure.Rejected => "本次请求未被模型服务接受，可能与服务地址、模型名称或 Responses 协议兼容配置有关，详细原因请查看站点日志。",
        ResponsesFailure.Truncated => "模型输出内容被截断。推理过程也会占用输出额度，请适当提高该角色的「单条最多写多少 Token」。",
        ResponsesFailure.Empty => "请求已完成，但模型未返回可展示的文本内容，请稍后重试。",
        _ => "本次调用未成功，详细原因请查看站点日志。"
    };

    private static bool Speakable(Moment? moment, LlmAtmosphereOptions options) =>
        moment is { Status: MomentStatus.Published, AllowComment: true }
        && (!moment.IsProtected || options.IncludeProtected);

    private static bool Wanted(
        LlmAtmosphereTrigger trigger,
        LlmAtmosphereMember member,
        IReadOnlyList<Comment> history,
        LlmAtmosphereOptions options) {
        if (history.Count(comment => comment.LlmMemberId is not null) >= Math.Max(options.MaxCommentsPerMoment, 1)) {
            return false;
        }

        if (trigger.ParentCommentId is not { } parentId) {
            return !history.Any(comment => comment.ParentId is null && IsFrom(comment, member));
        }

        var parent = history.FirstOrDefault(comment => comment.Id == parentId);

        return parent is not null
            && !IsFrom(parent, member)
            && !history.Any(comment => comment.ParentId == parentId && IsFrom(comment, member));
    }

    private static IEnumerable<LlmAtmosphereTrigger> Plan(
        int momentId,
        IReadOnlyList<Comment> history,
        IReadOnlyList<LlmAtmosphereMember> members,
        LlmAtmosphereOptions options,
        DateTimeOffset now) {
        var spoken = history.Where(comment => comment.LlmMemberId is not null).ToList();
        if (spoken.Count >= Math.Max(options.MaxCommentsPerMoment, 1)) {
            yield break;
        }

        var quiet = TimeSpan.FromMinutes(Math.Clamp(options.QuietMinutes, 0, 60 * 24));
        if (spoken.Count > 0 && now - spoken.Max(comment => comment.CreatedAt) < quiet) {
            yield break;
        }

        foreach (var member in members) {
            foreach (var unanswered in Unanswered(history, member)) {
                if (Rolled(member.ReplyChance)) {
                    yield return new LlmAtmosphereTrigger(
                        LlmAtmosphereTriggerKind.Reply,
                        momentId,
                        member.Id,
                        now + member.NextDelay(Random.Shared),
                        unanswered);
                }
            }

            if (!history.Any(comment => comment.ParentId is null && IsFrom(comment, member))
                && Rolled(member.CommentChance)) {
                yield return new LlmAtmosphereTrigger(
                    LlmAtmosphereTriggerKind.Comment,
                    momentId,
                    member.Id,
                    now + member.NextDelay(Random.Shared));
            }
        }
    }

    private static IEnumerable<int> Unanswered(IReadOnlyList<Comment> history, LlmAtmosphereMember member) =>
        history
            .Where(comment => comment.ParentId is not null && !IsFrom(comment, member))
            .Select(comment => comment.ParentId!.Value)
            .Distinct()
            .Where(parentId =>
                history.Any(parent => parent.Id == parentId && IsFrom(parent, member))
                && !history.Any(reply => reply.ParentId == parentId && IsFrom(reply, member)));

    private async Task<(string Text, ResponsesFailure Failure)> AskAsync(
        LlmAtmosphereMember member,
        Moment moment,
        IReadOnlyList<SceneComment> history,
        SceneComment? target,
        LlmAtmosphereOptions options,
        CancellationToken cancellationToken) {
        var attached = member.AllowImages
            ? await images.CollectAsync(moment, Math.Clamp(options.MaxImages, 0, 10), cancellationToken)
            : [];

        var request = new ResponsesRequest(
            member.ToEndpoint(options.TimeoutSeconds),
            AtmospherePrompt.Instructions(member),
            AtmospherePrompt.Input(moment, clock.ToLocal(moment.MomentDate), history, target),
            attached);

        var result = await client.CompleteAsync(request, cancellationToken);

        if (result.Failure == ResponsesFailure.Rejected && attached.Count > 0) {
            logger.LogInformation("氛围组「{Member}」的图片请求失败，将降级为纯文本重试。", member.Name);
            result = await client.CompleteAsync(request.WithoutImages(), cancellationToken);
        }

        return result.IsSuccess
            ? (AtmospherePrompt.Clean(result.Text, member.Name), ResponsesFailure.None)
            : (string.Empty, result.Failure);
    }

    private async Task<List<Comment>> HistoryAsync(int momentId, CancellationToken cancellationToken) =>
        await db.Comments
            .Where(comment => comment.MomentId == momentId && comment.IsApproved)
            .OrderBy(comment => comment.CreatedAt)
            .Include(comment => comment.Author)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    private static SceneComment ToScene(
        Comment comment,
        LlmAtmosphereMember member,
        SiteSettings site,
        IReadOnlyList<Comment> history) {
        var parent = comment.ParentId is { } parentId
            ? history.FirstOrDefault(item => item.Id == parentId)
            : null;

        return new SceneComment(
            DisplayName(comment, site),
            comment.Content,
            IsFrom(comment, member),
            parent is null ? null : DisplayName(parent, site));
    }

    private static string DisplayName(Comment comment, SiteSettings site) =>
        comment.Author is null ? comment.AuthorName : site.RoleName(comment.Author.Role);

    private static bool IsFrom(Comment comment, LlmAtmosphereMember member) =>
        comment.LlmMemberId is { } id && string.Equals(id, member.Id, StringComparison.OrdinalIgnoreCase);

    private static bool Rolled(int chance) =>
        chance > 0 && Random.Shared.Next(100) < Math.Min(chance, 100);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    #endregion
}
