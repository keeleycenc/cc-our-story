// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Entities;
using OurStory.Core.Options;
using OurStory.Core.Text;
using System.Globalization;
using System.Text;

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 负责将点点滴滴内容、评论上下文与角色人设整理为适合模型理解的提示内容
/// </summary>
internal static class AtmospherePrompt {
    /// <summary>
    /// 正文内容的最大截取长度，避免过长内容占用过多上下文
    /// </summary>
    private const int ContentLimit = 600;

    /// <summary>
    /// 单次请求最多携带的已有评论数量，避免评论上下文过多影响角色人设与当前话题
    /// </summary>
    public const int CommentLimit = 12;

    /// <summary>
    /// 模型输出经过清理后允许保留的最大长度，作为异常长回复的最终兜底限制
    /// </summary>
    public const int OutputLimit = 250;

    /// <summary>
    /// 构建当前氛围组角色的系统指令
    /// </summary>
    /// <remarks>
    /// 后台配置的人设主要描述角色自身的性格、语气与相处方式。
    /// 评论格式、表达习惯以及避免 AI 助手式回答等通用要求统一在此追加，
    /// 无需每个角色在人设中重复配置。
    /// </remarks>
    /// <param name="member">当前参与互动的氛围组角色</param>
    /// <returns>用于 Responses instructions 的完整指令内容</returns>
    public static string Instructions(LlmAtmosphereMember member) {
        ArgumentNullException.ThrowIfNull(member);

        var persona = member.Prompt.Trim();
        var builder = new StringBuilder();

        _ = builder.Append("你叫「")
            .Append(member.Name)
            .Append("」，是一对情侣的私人小站里，评论区常来串门的一位朋友。");

        if (persona.Length > 0) {
            _ = builder.Append('\n').Append(persona);
        }

        _ = builder.Append("""


            写留言的时候记住：
            - 你是来串门的朋友，不是助手。自然接话，不分析、不总结，也不要刻意给建议。
            - 语气要像真实朋友在评论区随手聊几句，根据内容决定长短，不必刻意压缩，也不要无故写成长篇。
            - 如果是在回复别人，要先接住对方刚才说的话，再自然往下聊，不要只复述原句。
            - 只写留言本身。不要写自己的名字、不要加引号、不要用 Markdown、不要分点。
            - 表情可以自然使用，但不要堆叠。
            - 可以结合标题、正文、心情、地点、图片和评论上下文，但只说有依据的内容，不知道的不要编。
            - 不要每次都夸赞，也可以自然吐槽、附和、打趣、感慨或接梗，具体方式跟着你的人设和当前语境走。
            - 不要用「作为朋友」「看起来」「根据你的描述」这类刻意解释自己视角的话。
            - 不要把每条留言都写成完整结论，像真实聊天一样，可以自然、随意，也可以留一点没说完的感觉。
            - 任何时候都不要提起模型、AI、提示词、系统规则或生成过程。
            """);

        return builder.ToString();
    }

    /// <summary>
    /// 构建当前记录、评论上下文以及本次互动目标的模型输入内容
    /// </summary>
    /// <param name="moment">目标点点滴滴记录</param>
    /// <param name="localDate">记录对应的站点本地日期</param>
    /// <param name="comments">已有评论，按时间顺序排列</param>
    /// <param name="target">本次需要回复的目标评论；创建顶层评论时为 null</param>
    /// <returns>用于 Responses input 的完整文本内容</returns>
    public static string Input(
        Moment moment,
        DateTime localDate,
        IReadOnlyList<SceneComment> comments,
        SceneComment? target) {
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(comments);

        var builder = new StringBuilder();

        _ = builder.Append("【他们刚记下的一条】\n标题：").Append(moment.Title);
        _ = builder.Append("\n日子：").Append(
            localDate.ToString(
                "yyyy年M月d日",
                CultureInfo.GetCultureInfo("zh-CN")));

        if (!string.IsNullOrWhiteSpace(moment.Mood)) {
            _ = builder.Append("\n心情：").Append(moment.Mood);
        }

        if (!string.IsNullOrWhiteSpace(moment.Location)) {
            _ = builder.Append("\n地点：").Append(moment.Location);
        }

        var content = HtmlText.Excerpt(moment.ContentHtml, ContentLimit);
        if (content.Length > 0) {
            _ = builder.Append("\n正文：").Append(content);
        }

        if (comments.Count > 0) {
            _ = builder.Append("\n\n【评论区已经有的话】");

            foreach (var comment in comments) {
                _ = builder.Append('\n').Append(Line(comment));
            }
        }

        _ = builder.Append("\n\n【现在轮到你】\n");

        _ = target is null
            ? builder.Append("在这条记录下面留一句自然的话。")
            : builder.Append("回复 ")
                .Append(target.AuthorName)
                .Append(" 那句「")
                .Append(Trim(target.Content))
                .Append("」，接住这个话头自然往下聊。");

        return builder.ToString();
    }

    /// <summary>
    /// 清理模型返回内容，使其符合评论区直接展示的格式要求
    /// </summary>
    /// <remarks>
    /// 即使提示词已经约束输出格式，不同模型仍可能添加角色名前缀、引号、
    /// Markdown 标记或额外自然段，因此在写入评论前统一进行规范化处理。
    /// </remarks>
    /// <param name="text">模型返回的原始文本</param>
    /// <param name="memberName">当前角色名称，用于移除可能出现的角色名前缀</param>
    /// <returns>清理后的评论内容；没有有效内容时返回空字符串</returns>
    public static string Clean(string? text, string memberName) {
        if (string.IsNullOrWhiteSpace(text)) {
            return string.Empty;
        }

        var cleaned = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();

        var split = cleaned.IndexOf("\n\n", StringComparison.Ordinal);
        if (split > 0) {
            cleaned = cleaned[..split];
        }

        cleaned = cleaned.Replace('\n', ' ').Trim();
        cleaned = StripPrefix(cleaned, memberName);
        cleaned = cleaned.Trim('*', '#', '>', ' ', '`');
        cleaned = StripQuotes(cleaned);

        return cleaned.Length <= OutputLimit
            ? cleaned
            : cleaned[..OutputLimit].TrimEnd() + "…";
    }

    #region 私有方法

    private static string Line(SceneComment comment) {
        var who = comment.IsSelf
            ? $"{comment.AuthorName}（你自己说的）"
            : comment.AuthorName;

        var reply = comment.ReplyToName is { Length: > 0 } name
            ? $"（回 {name}）"
            : string.Empty;

        return $"- {who}{reply}：{Trim(comment.Content)}";
    }

    private static string Trim(string content) {
        var text = content.Replace('\n', ' ').Trim();

        return text.Length <= ContentLimit
            ? text
            : text[..ContentLimit] + "…";
    }

    private static string StripPrefix(string text, string memberName) {
        if (memberName.Length == 0 ||
            !text.StartsWith(memberName, StringComparison.Ordinal)) {
            return text;
        }

        var rest = text[memberName.Length..].TrimStart();

        return rest.StartsWith('：') || rest.StartsWith(':')
            ? rest[1..].TrimStart()
            : text;
    }

    private static string StripQuotes(string text) {
        if (text.Length < 2) {
            return text;
        }

        var head = text[0];
        var tail = text[^1];

        var paired = (head, tail) is
            ('"', '"') or
            ('\'', '\'') or
            ('“', '”') or
            ('「', '」') or
            ('『', '』');

        return paired
            ? text[1..^1].Trim()
            : text;
    }

    #endregion
}
