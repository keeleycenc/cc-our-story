// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core;
using OurStory.Core.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OurStory.Services.Cycles;

/// <summary>
/// 将一次周期的事实整理为规则小结与模型上下文
/// </summary>
/// <remarks>
/// 规则与模型共用同一个 <see cref="CycleNarrativeContext"/>。
/// 模型未配置、调用失败或返回空内容时，页面使用 <see cref="Compose"/> 生成的规则小结。
/// </remarks>
public static class CycleNarrative {
    /// <summary>
    /// 小结正文允许的最大长度，超出部分将被截断
    /// </summary>
    public const int MaximumLength = 260;

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("zh-CN");

    /// <summary>
    /// 使用站内规则生成小结
    /// </summary>
    /// <param name="context">本次周期的全部事实</param>
    /// <returns>可直接显示的小结正文</returns>
    public static string Compose(CycleNarrativeContext context) {
        ArgumentNullException.ThrowIfNull(context);

        var sentences = new List<string> { Span(context) };

        if (Rhythm(context) is { Length: > 0 } rhythm) {
            sentences.Add(rhythm);
        }

        if (Body(context) is { Length: > 0 } body) {
            sentences.Add(body);
        }

        if (context.Note.Length > 0) {
            sentences.Add($"共同备注：{Clip(context.Note, 40)}");
        }

        return string.Join('；', sentences) + "。";
    }

    /// <summary>
    /// 计算当前事实的指纹
    /// </summary>
    /// <param name="context">本次周期的全部事实</param>
    /// <returns>16 个十六进制字符的指纹</returns>
    /// <remarks>
    /// 日期、备注或每日记录发生变化时，指纹随之变化，已保存的模型小结将失效。
    /// </remarks>
    public static string Stamp(CycleNarrativeContext context) {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new StringBuilder()
            .Append(context.StartDate.DayNumber).Append('|')
            .Append(context.EndDate?.DayNumber ?? -1).Append('|')
            .Append(context.CycleDays ?? -1).Append('|')
            .Append(context.Note);

        foreach (var day in context.Days.OrderBy(item => item.Date)) {
            _ = builder
                .Append('|').Append(day.Date.DayNumber)
                .Append(':').Append((int)day.Flow)
                .Append(':').Append((int)day.Mood)
                .Append(':').Append(day.Pain)
                .Append(':').Append((int)day.Symptoms)
                .Append(':').Append(day.Note);
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(digest)[..16];
    }

    /// <summary>
    /// 生成模型写作要求
    /// </summary>
    /// <param name="tone">后台配置的语气偏好，可为空</param>
    /// <returns>Responses 协议的 instructions</returns>
    public static string Instructions(string? tone) {
        var builder = new StringBuilder()
            .AppendLine("你正在协助一对情侣共同整理女方的经期记录，请为其中一次周期撰写一段中文小结。")
            .AppendLine("要求：")
            .AppendLine("1. 仅使用所提供的事实，不得编造任何数据、症状或日期。")
            .AppendLine($"2. 使用两到三句话，不超过 {MaximumLength} 个字，直接输出正文。")
            .AppendLine("3. 不使用标题、Markdown、列表、Emoji，也不要用引号包裹全文。")
            .AppendLine("4. 表达清晰、克制、可靠，同时保留伴侣共同记录与彼此关心的温度；避免网络口语、拟人化和过度抒情。")
            .AppendLine("5. 不提供医疗诊断、用药或治疗建议；可以客观比较本次记录与既往规律。")
            .AppendLine("6. 数据明显偏离既往规律时，以温和、明确的方式提醒双方共同留意，避免制造焦虑。");

        if (!string.IsNullOrWhiteSpace(tone)) {
            _ = builder.AppendLine().Append("补充语气偏好：").Append(tone.Trim());
        }

        return builder.ToString();
    }

    /// <summary>
    /// 生成提供给模型的事实清单
    /// </summary>
    /// <param name="context">本次周期的全部事实</param>
    /// <returns>Responses 协议的输入文本</returns>
    public static string Input(CycleNarrativeContext context) {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new StringBuilder()
            .Append("本次经期：").Append(Full(context.StartDate)).Append(" 至 ")
            .AppendLine(context.EndDate is { } end ? Full(end) : "尚未结束")
            .Append("持续天数：").Append(context.DurationDays).AppendLine(context.IsActive ? " 天（进行中）" : " 天");

        if (context.CycleDays is { } gap) {
            _ = builder.Append("距上次开始：").Append(gap).AppendLine(" 天");
        } else {
            _ = builder.AppendLine("距上次开始：这是第一条记录");
        }

        if (context.AverageCycleDays is { } averageCycle) {
            _ = builder.Append("既往平均周期：").Append(averageCycle).AppendLine(" 天");
        }

        if (context.AveragePeriodDays is { } averagePeriod) {
            _ = builder.Append("既往平均经期：").Append(averagePeriod).AppendLine(" 天");
        }

        _ = builder.Append("与既往规律：").AppendLine(RhythmWord(context));

        if (context.Note.Length > 0) {
            _ = builder.Append("共同备注：").AppendLine(Clip(context.Note, 200));
        }

        if (context.Days.Count == 0) {
            _ = builder.AppendLine("每日补充记录：未填写。");
            return builder.ToString();
        }

        _ = builder.AppendLine("每日补充记录：");
        foreach (var day in context.Days.OrderBy(item => item.Date)) {
            _ = builder.Append("- ").Append(Full(day.Date)).Append('：').AppendLine(DayLine(day));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 清理模型返回的文本
    /// </summary>
    /// <param name="text">模型返回的原始文本</param>
    /// <returns>可直接显示的小结正文；内容不可用时返回空字符串</returns>
    public static string Clean(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return string.Empty;
        }

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('#', '-', '*', '>', ' ').Trim())
            .Where(line => line.Length > 0);

        var joined = string.Join(string.Empty, lines).Trim().Trim('"', '「', '」', '“', '”');
        return joined.Length <= MaximumLength ? joined : joined[..MaximumLength].TrimEnd() + "…";
    }

    #region 私有方法

    private static string Span(CycleNarrativeContext context) {
        var head = context.EndDate is { } end
            ? $"{Short(context.StartDate)} 到 {Short(end)}，持续 {context.DurationDays} 天"
            : $"{Short(context.StartDate)} 开始，目前为第 {context.DurationDays} 天";
        return head;
    }

    private static string Rhythm(CycleNarrativeContext context) {
        if (context.CycleDays is not { } gap) {
            return "这是第一条记录，继续共同记录后将形成更清晰的周期参考";
        }

        var delta = context.CycleDelta;
        return context.Rhythm switch {
            CycleRhythm.Early when delta is { } early => $"距上次相隔 {gap} 天，较既往平均提前 {-early} 天",
            CycleRhythm.Late when delta is { } late => $"距上次相隔 {gap} 天，较既往平均推迟 {late} 天",
            CycleRhythm.Normal => $"距上次相隔 {gap} 天，与既往节奏基本一致",
            _ => $"距上次相隔 {gap} 天"
        };
    }

    private static string Body(CycleNarrativeContext context) {
        if (context.Days.Count == 0) {
            return string.Empty;
        }

        var parts = new List<string>();
        var flows = context.Days.Where(day => day.Flow != CycleFlow.Unset).ToArray();
        if (flows.Length > 0) {
            var peak = flows.Max(day => day.Flow);
            parts.Add($"记录中的经量峰值为{peak.Name()}");
        }

        var symptoms = context.Days.Aggregate(CycleSymptom.None, (all, day) => all | day.Symptoms);
        if (symptoms != CycleSymptom.None) {
            parts.Add($"身体状况记录了{symptoms.Join()}");
        }

        var aching = context.Days.Count(day => day.Pain >= 2);
        if (aching > 0) {
            parts.Add($"其中 {aching} 天不适较明显");
        }

        var moods = context.Days.Where(day => day.Mood != CycleMood.Unset).ToArray();
        if (moods.Length > 0) {
            var common = moods
                .GroupBy(day => day.Mood)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .First().Key;
            parts.Add($"这几天的心情多为{common.Name()}");
        }

        return string.Join('，', parts);
    }

    private static string DayLine(CycleDayFact day) {
        var parts = new List<string>();

        if (day.Flow != CycleFlow.Unset) {
            parts.Add($"经量{day.Flow.Name()}");
        }

        if (day.Mood != CycleMood.Unset) {
            parts.Add($"心情{day.Mood.Name()}");
        }

        if (day.Pain > 0) {
            parts.Add($"不适{CycleLabels.PainName(day.Pain)}");
        }

        if (day.Symptoms != CycleSymptom.None) {
            parts.Add(day.Symptoms.Join());
        }

        if (day.Note.Length > 0) {
            parts.Add($"备注：{Clip(day.Note, 60)}");
        }

        return parts.Count == 0 ? "未填写具体状态" : string.Join('；', parts);
    }

    private static string RhythmWord(CycleNarrativeContext context) => context.Rhythm switch {
        CycleRhythm.Early => $"偏早 {-(context.CycleDelta ?? 0)} 天",
        CycleRhythm.Late => $"偏晚 {context.CycleDelta ?? 0} 天",
        CycleRhythm.Normal => "与既往一致",
        _ => "暂无可比较的历史记录"
    };

    private static string Short(DateOnly date) => date.ToString("M 月 d 日", Culture);

    private static string Full(DateOnly date) => date.ToString("yyyy 年 M 月 d 日", Culture);

    private static string Clip(string text, int limit) {
        var trimmed = text.Replace('\n', ' ').Trim();
        return trimmed.Length <= limit ? trimmed : trimmed[..limit] + "…";
    }

    #endregion
}
