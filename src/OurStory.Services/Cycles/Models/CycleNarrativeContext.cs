// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core;

namespace OurStory.Services.Cycles;

/// <summary>
/// 生成周期小结所需的完整事实上下文
/// </summary>
/// <remarks>
/// 规则文案与模型调用共用同一对象，以保证两种生成方式采用一致的事实。
/// </remarks>
/// <param name="StartDate">经期开始日期</param>
/// <param name="EndDate">经期结束日期；尚未结束时为 <see langword="null"/></param>
/// <param name="DurationDays">经期持续天数；进行中时为已经过的天数</param>
/// <param name="CycleDays">距上一次开始的间隔天数；首条记录为 <see langword="null"/></param>
/// <param name="CycleDelta">本次间隔相对既往平均值的偏差天数</param>
/// <param name="Rhythm">本次间隔与既往规律的关系</param>
/// <param name="Note">这条记录的备注</param>
/// <param name="AverageCycleDays">既往平均周期天数</param>
/// <param name="AveragePeriodDays">既往平均经期天数</param>
/// <param name="Days">本次经期范围内填写过的每日补充记录</param>
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
    IReadOnlyList<CycleDayFact> Days) {
    /// <summary>
    /// 获取一个值，指示这条记录是否还在进行中
    /// </summary>
    public bool IsActive => EndDate is null;
}

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
    /// <returns>成功的回执</returns>
    public static CycleInsightProbe Success(string text) => new(true, "模型通道连接正常，以下为示例小结。", text);

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="message">失败原因</param>
    /// <returns>失败的回执</returns>
    public static CycleInsightProbe Failed(string message) => new(false, message, string.Empty);
}
