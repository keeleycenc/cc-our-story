// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core;

namespace OurStory.Services.Cycles;

/// <summary>
/// 生成周期小结所需的完整事实上下文
/// </summary>
/// <remarks>
/// 规则文案与模型调用共用同一对象，以保证两种生成方式采用一致的事实。
/// 上下文只描述目标周期及其之前的事实：目标周期之后的记录一律不携带，
/// 因此同一条小结在补写与重新生成时看到的数据范围始终一致。
/// </remarks>
/// <param name="StartDate">经期开始日期</param>
/// <param name="EndDate">经期结束日期；尚未结束时为 <see langword="null"/></param>
/// <param name="DurationDays">经期持续天数；进行中时为已经过的天数</param>
/// <param name="CycleDays">距上一次开始的间隔天数；首条记录为 <see langword="null"/></param>
/// <param name="CycleDelta">本次间隔相对既往平均值的偏差天数</param>
/// <param name="Rhythm">本次间隔与既往规律的关系</param>
/// <param name="Note">这条记录的备注</param>
/// <param name="AverageCycleDays">既往平均周期天数，仅统计目标周期之前的记录</param>
/// <param name="AveragePeriodDays">既往平均经期天数，仅统计目标周期之前的记录</param>
/// <param name="Days">本次经期范围内填写过的每日补充记录</param>
/// <param name="Ordinal">目标周期在全部记录中的序号，从 1 开始</param>
/// <param name="History">
/// 排在目标周期之前的有界完整历史，按开始日期升序（同一天起始时按记录先后），不含目标周期
/// </param>
public sealed record CycleNarrativeContext(
    DateOnly StartDate,
    DateOnly? EndDate,
    int DurationDays,
    int? CycleDays,
    int? CycleDelta,
    CycleRhythm Rhythm,
    string Note,
    int? AverageCycleDays,
    int? AveragePeriodDays,
    IReadOnlyList<CycleDayFact> Days,
    int Ordinal,
    IReadOnlyList<CyclePastFact> History) {
    /// <summary>
    /// 获取一个值，指示这条记录是否还在进行中
    /// </summary>
    public bool IsActive => EndDate is null;

    /// <summary>
    /// 获取本次携带范围内最早一个周期的序号
    /// </summary>
    public int WindowStartOrdinal => History.Count == 0 ? Ordinal : History[0].Ordinal;
}

/// <summary>
/// 表示排在目标周期之前的一次周期，作为纵向比较的原始事实
/// </summary>
/// <remarks>
/// 重复登记会让两条记录落在同一天，此时按记录先后决定顺序，先登记的一条仍算作此前历史。
/// </remarks>
/// <param name="Ordinal">该周期在全部记录中的序号，从 1 开始</param>
/// <param name="StartDate">经期开始日期</param>
/// <param name="EndDate">经期结束日期；尚未结束时为 <see langword="null"/></param>
/// <param name="DurationDays">经期持续天数</param>
/// <param name="CycleDays">距上一次开始的间隔天数；首条记录为 <see langword="null"/></param>
/// <param name="Note">这条记录的备注</param>
/// <param name="Days">该次经期范围内填写过的每日补充记录</param>
public sealed record CyclePastFact(
    int Ordinal,
    DateOnly StartDate,
    DateOnly? EndDate,
    int DurationDays,
    int? CycleDays,
    string Note,
    IReadOnlyList<CycleDayFact> Days);

/// <summary>
/// 表示一次经期内某一天的补充事实
/// </summary>
/// <param name="Date">这条记录对应的日期</param>
/// <param name="Flow">当天经量</param>
/// <param name="Mood">当天心情</param>
/// <param name="Pain">当天不适程度，0 到 3</param>
/// <param name="Symptoms">当天记下的不适</param>
/// <param name="Note">当天的补充说明</param>
public sealed record CycleDayFact(
    DateOnly Date,
    CycleFlow Flow,
    CycleMood Mood,
    int Pain,
    CycleSymptom Symptoms,
    string Note);

/// <summary>
/// 后台模型通道测试结果
/// </summary>
/// <param name="Ok">本次调用是否成功</param>
/// <param name="Message">面向后台页面的结果说明</param>
/// <param name="Text">调用成功时模型生成的小结</param>
public sealed record CycleInsightProbe(bool Ok, string Message, string Text) {
    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="text">模型生成的小结</param>
    /// <param name="fromRecord">本次试写是否基于真实记录</param>
    /// <returns>成功的回执</returns>
    public static CycleInsightProbe Success(string text, bool fromRecord = false) => new(
        true,
        fromRecord
            ? "模型通道连接正常，以下小结基于最新一次花信记录试写。"
            : "模型通道连接正常；当前还没有可用的花信记录，以下为示例小结",
        text);

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="message">失败原因</param>
    /// <returns>失败的回执</returns>
    public static CycleInsightProbe Failed(string message) => new(false, message, string.Empty);
}
