// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Models;
using System.Globalization;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 花信月历的前端传输模型
/// </summary>
/// <remarks>
/// 在服务端统一转换枚举名称、样式类名与日期格式，避免前后端重复维护映射关系。
/// </remarks>
/// <param name="Year">当前展示年份</param>
/// <param name="Month">当前展示月份</param>
/// <param name="Rows">当前月历行数</param>
/// <param name="MinimumYear">年份选择器最小值</param>
/// <param name="MaximumYear">年份选择器最大值</param>
/// <param name="PreviousMonth">上一个月的基准日期</param>
/// <param name="NextMonth">下一个月的基准日期</param>
/// <param name="Today">服务端认定的今天</param>
/// <param name="Days">按日历布局生成的日期集合</param>
public sealed record CycleCalendarPayload(
    int Year,
    int Month,
    int Rows,
    int MinimumYear,
    int MaximumYear,
    string PreviousMonth,
    string NextMonth,
    string Today,
    IReadOnlyList<CycleDayPayload> Days) {
    /// <summary>
    /// 将月份数据转换为前端传输模型
    /// </summary>
    /// <param name="month">服务层给出的月历</param>
    /// <returns>可直接序列化的月历数据</returns>
    public static CycleCalendarPayload From(CycleCalendarMonth month) {
        ArgumentNullException.ThrowIfNull(month);

        return new CycleCalendarPayload(
            month.Year,
            month.Month,
            month.Rows,
            month.MinimumYear,
            month.MaximumYear,
            Stamp(month.PreviousMonth),
            Stamp(month.NextMonth),
            Stamp(month.Today),
            [.. month.Days.Select(CycleDayPayload.From)]);
    }

    internal static string Stamp(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// 月历上的一天
/// </summary>
/// <param name="Date">日期，形如 <c>2026-08-29</c></param>
/// <param name="Day">当前月份中的日期序号</param>
/// <param name="InMonth">是否属于当前展示月份</param>
/// <param name="IsToday">是否为今天</param>
/// <param name="IsFuture">是否晚于今天</param>
/// <param name="Phase">阶段对应的类名</param>
/// <param name="PhaseName">阶段的中文说法</param>
/// <param name="PhaseHint">阶段的一句说明</param>
/// <param name="DayOfCycle">位于本轮周期的第几天</param>
/// <param name="PeriodDay">这次经期的第几天</param>
/// <param name="PeriodStart">是否为某条记录的开始日</param>
/// <param name="PeriodEnd">是否为某条记录的结束日</param>
/// <param name="ExpectedStart">是否为预测的下次经期开始日期</param>
/// <param name="JointRecord">双方是否都在当天留下过任意类型的补充记录</param>
/// <param name="Record">覆盖当天的周期记录</param>
/// <param name="Logs">当天按记录时间排列的补充记录</param>
public sealed record CycleDayPayload(
    string Date,
    int Day,
    bool InMonth,
    bool IsToday,
    bool IsFuture,
    string Phase,
    string PhaseName,
    string PhaseHint,
    int? DayOfCycle,
    int? PeriodDay,
    bool PeriodStart,
    bool PeriodEnd,
    bool ExpectedStart,
    bool JointRecord,
    CycleRecordPayload? Record,
    IReadOnlyList<CycleLogPayload> Logs) {
    /// <summary>
    /// 将单日数据转换为前端传输模型
    /// </summary>
    /// <param name="day">服务层给出的一天</param>
    /// <returns>可直接序列化的单日数据</returns>
    public static CycleDayPayload From(CycleCalendarDay day) {
        ArgumentNullException.ThrowIfNull(day);

        return new CycleDayPayload(
            CycleCalendarPayload.Stamp(day.Date),
            day.Date.Day,
            day.IsInMonth,
            day.IsToday,
            day.IsFuture,
            Slug(day.Phase),
            day.Phase.Name(),
            day.Phase.Describe(),
            day.DayOfCycle,
            day.PeriodDay,
            day.IsPeriodStart,
            day.IsPeriodEnd,
            day.IsExpectedStart,
            day.Logs.Select(log => log.CreatedByUserId).Distinct().Skip(1).Any(),
            day.Record is null ? null : CycleRecordPayload.From(day.Record),
            [.. day.Logs.Select(CycleLogPayload.From)]);
    }

    private static string Slug(CyclePhase phase) => phase switch {
        CyclePhase.Period => "period",
        CyclePhase.Predicted => "predicted",
        CyclePhase.Fertile => "fertile",
        CyclePhase.Ovulation => "ovulation",
        CyclePhase.Follicular => "follicular",
        CyclePhase.Luteal => "luteal",
        CyclePhase.Observation => "observation",
        _ => "unknown"
    };
}

/// <summary>
/// 月历上引用到的一条周期记录
/// </summary>
/// <param name="Id">记录标识</param>
/// <param name="Start">开始日期</param>
/// <param name="End">结束日期；进行中时为空字符串</param>
/// <param name="Range">供直接显示的日期区间</param>
/// <param name="Duration">持续天数</param>
/// <param name="CycleDays">距上一次开始的间隔天数</param>
/// <param name="IsActive">是否进行中</param>
/// <param name="Note">备注</param>
/// <param name="Summary">这次周期的小结</param>
/// <param name="FromModel">小结是否由模型生成</param>
/// <param name="Tags">页面标签</param>
public sealed record CycleRecordPayload(
    int Id,
    string Start,
    string End,
    string Range,
    int Duration,
    int? CycleDays,
    bool IsActive,
    string Note,
    string Summary,
    bool FromModel,
    IReadOnlyList<CycleTagPayload> Tags) {
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("zh-CN");

    /// <summary>
    /// 将周期记录转换为前端传输模型
    /// </summary>
    /// <param name="record">服务层给出的记录</param>
    /// <returns>可直接序列化的周期记录</returns>
    public static CycleRecordPayload From(CycleRecordItem record) {
        ArgumentNullException.ThrowIfNull(record);

        return new CycleRecordPayload(
            record.Id,
            CycleCalendarPayload.Stamp(record.StartDate),
            record.EndDate is { } end ? CycleCalendarPayload.Stamp(end) : string.Empty,
            $"{Short(record.StartDate)} – {(record.EndDate is { } stop ? Short(stop) : "进行中")}",
            record.DurationDays,
            record.CycleDays,
            record.IsActive,
            record.Note,
            record.Summary.Text,
            record.Summary.FromModel,
            [.. record.Tags.Select(CycleTagPayload.From)]);
    }

    private static string Short(DateOnly date) => date.ToString("M 月 d 日", Culture);
}

/// <summary>
/// 一枚标签
/// </summary>
/// <param name="Text">标签文字</param>
/// <param name="Tone">语气对应的类名</param>
public sealed record CycleTagPayload(string Text, string Tone) {
    /// <summary>
    /// 将标签转换为前端传输模型
    /// </summary>
    /// <param name="tag">服务层给出的标签</param>
    /// <returns>可直接序列化的标签</returns>
    public static CycleTagPayload From(CycleTag tag) {
        ArgumentNullException.ThrowIfNull(tag);
        return new CycleTagPayload(tag.Text, tag.Tone.ToString().ToLowerInvariant());
    }
}

/// <summary>
/// 某一天的补充记录
/// </summary>
/// <param name="Flow">经量的枚举值</param>
/// <param name="FlowName">经量的中文说法</param>
/// <param name="Mood">心情的枚举值</param>
/// <param name="MoodName">心情的中文说法</param>
/// <param name="Pain">不适程度</param>
/// <param name="PainName">不适程度的中文说法</param>
/// <param name="Symptoms">按位存放的不适集合</param>
/// <param name="SymptomNames">逐项列出的不适</param>
/// <param name="Note">当天的补充说明</param>
/// <param name="IsIntimate">是否记录了亲密互动</param>
/// <param name="IntimacyCount">这条记录包含的亲密互动次数</param>
/// <param name="ProtectionName">安全措施的显示文字</param>
/// <param name="OutcomeName">结束方式的显示文字</param>
/// <param name="RecordedBy">记录者</param>
/// <param name="RecordedAt">记录时间，供 time 元素使用</param>
/// <param name="RecordedAtText">记录时间的简短显示文字</param>
public sealed record CycleLogPayload(
    int Flow,
    string FlowName,
    int Mood,
    string MoodName,
    int Pain,
    string PainName,
    int Symptoms,
    IReadOnlyList<string> SymptomNames,
    string Note,
    bool IsIntimate,
    int IntimacyCount,
    string ProtectionName,
    string OutcomeName,
    string RecordedBy,
    string RecordedAt,
    string RecordedAtText) {
    /// <summary>
    /// 将每日补充记录转换为前端传输模型
    /// </summary>
    /// <param name="log">服务层给出的补充记录</param>
    /// <returns>可直接序列化的补充记录</returns>
    public static CycleLogPayload From(CycleDayLog log) {
        ArgumentNullException.ThrowIfNull(log);

        return new CycleLogPayload(
            (int)log.Flow,
            log.Flow.Name(),
            (int)log.Mood,
            log.Mood.Name(),
            log.Pain,
            CycleLabels.PainName(log.Pain),
            (int)log.Symptoms,
            [.. log.Symptoms.Split().Select(CycleLabels.Name)],
            log.Note,
            log.IsIntimate,
            log.IntimacyCount,
            log.IntimacyProtection.Name(),
            log.IntimacyOutcome.Name(),
            log.CreatedByName,
            log.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            log.CreatedAt.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture));
    }
}
