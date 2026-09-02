// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Models;

/// <summary>
/// 周期分析器使用的最小事实输入
/// </summary>
/// <param name="StartDate">经期开始日期</param>
/// <param name="EndDate">经期结束日期；尚未结束时为 <see langword="null"/></param>
public sealed record CycleFact(DateOnly StartDate, DateOnly? EndDate);

/// <summary>
/// 页面上一枚标签
/// </summary>
/// <param name="Text">标签文字</param>
/// <param name="Tone">标签语气，决定配色</param>
public sealed record CycleTag(string Text, CycleTagTone Tone);

/// <summary>
/// 一次周期预测结果
/// </summary>
/// <param name="ExpectedStart">预测的下次经期开始日期</param>
/// <param name="WindowStart">预测窗口起始日期</param>
/// <param name="WindowEnd">预测窗口结束日期</param>
/// <param name="Ovulation">推算的排卵日；无法推算时为 <see langword="null"/></param>
/// <param name="FertileStart">推算的易孕期起始日期；无法推算时为 <see langword="null"/></param>
/// <param name="FertileEnd">推算的易孕期结束日期；无法推算时为 <see langword="null"/></param>
/// <param name="SampleCount">参与本次预测的有效历史样本数量</param>
/// <param name="Confidence">预测可信度，取值 0 到 100，由样本数量与波动共同决定</param>
public sealed record CyclePrediction(
    DateOnly ExpectedStart,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    DateOnly? Ovulation,
    DateOnly? FertileStart,
    DateOnly? FertileEnd,
    int SampleCount,
    int Confidence) {
    /// <summary>
    /// 获取预测窗口一共覆盖多少天
    /// </summary>
    public int WindowDays => WindowEnd.DayNumber - WindowStart.DayNumber + 1;
}

/// <summary>
/// 根据历史事实记录动态计算得到的周期统计结果
/// </summary>
/// <param name="TotalRecords">历史周期记录总数</param>
/// <param name="CompletedRecords">已记录完整开始与结束日期的周期数量</param>
/// <param name="AverageCycleDays">平均周期天数；样本不足时为 <see langword="null"/></param>
/// <param name="AveragePeriodDays">平均经期持续天数；样本不足时为 <see langword="null"/></param>
/// <param name="ShortestCycleDays">历史最短有效周期天数；无有效样本时为 <see langword="null"/></param>
/// <param name="LongestCycleDays">历史最长有效周期天数；无有效样本时为 <see langword="null"/></param>
/// <param name="CycleSwingDays">近期周期相对平均值的平均偏离天数；无有效样本时为 <see langword="null"/></param>
/// <param name="NextPrediction">下一次周期预测；暂不具备预测条件时为 <see langword="null"/></param>
/// <param name="Analyzer">生成当前统计结果的分析器标识</param>
public sealed record CycleStatistics(
    int TotalRecords,
    int CompletedRecords,
    int? AverageCycleDays,
    int? AveragePeriodDays,
    int? ShortestCycleDays,
    int? LongestCycleDays,
    int? CycleSwingDays,
    CyclePrediction? NextPrediction,
    string Analyzer);

/// <summary>
/// 月历上某一天所处的阶段及其在周期中的位置
/// </summary>
/// <param name="Phase">当天所处的阶段</param>
/// <param name="DayOfCycle">当天位于本轮周期的第几天；无法判断时为 <see langword="null"/></param>
public sealed record CycleDayPhase(CyclePhase Phase, int? DayOfCycle);

/// <summary>
/// 对一条周期记录相对既往规律的判断
/// </summary>
/// <param name="Rhythm">本次间隔与既往规律的关系</param>
/// <param name="CycleDelta">本次间隔相对既往平均值的偏差天数；无法比较时为 <see langword="null"/></param>
/// <param name="Tags">可以直接渲染到页面上的标签，第一枚是「正常」或「留意」的总判断</param>
public sealed record CycleAppraisal(CycleRhythm Rhythm, int? CycleDelta, IReadOnlyList<CycleTag> Tags);

/// <summary>
/// 由系统规则或模型生成的周期小结
/// </summary>
/// <param name="Text">小结正文</param>
/// <param name="Source">小结来源</param>
/// <param name="UpdatedAt">生成时间；规则文案为 <see langword="null"/></param>
public sealed record CycleSummaryText(string Text, CycleSummarySource Source, DateTimeOffset? UpdatedAt) {
    /// <summary>
    /// 获取一个值，指示该小结是否由模型生成
    /// </summary>
    public bool FromModel => Source == CycleSummarySource.Model;
}

/// <summary>
/// 某一天补充记录的展示内容
/// </summary>
/// <param name="Flow">当天经量</param>
/// <param name="Mood">当天心情</param>
/// <param name="Pain">当天不适程度，0 到 3</param>
/// <param name="Symptoms">当天记下的不适</param>
/// <param name="Note">当天的补充说明</param>
/// <param name="IsIntimate">是否记录了亲密互动</param>
/// <param name="IntimacyCount">这条记录包含的亲密互动次数</param>
/// <param name="IntimacyProtection">采用的安全措施</param>
/// <param name="IntimacyOutcome">亲密互动的结束方式</param>
/// <param name="CreatedByUserId">记录者标识，仅用于判断双方是否都参与记录</param>
/// <param name="CreatedByName">记录者的显示名称</param>
/// <param name="CreatedAt">站点时区下的记录时间</param>
public sealed record CycleDayLog(
    CycleFlow Flow,
    CycleMood Mood,
    int Pain,
    CycleSymptom Symptoms,
    string Note,
    bool IsIntimate,
    int IntimacyCount,
    CycleIntimacyProtection IntimacyProtection,
    CycleIntimacyOutcome IntimacyOutcome,
    int CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt);

/// <summary>
/// 一条周期记录在页面上的完整展示内容，历史时间轴与月历共用
/// </summary>
/// <param name="Id">周期记录标识</param>
/// <param name="StartDate">经期开始日期</param>
/// <param name="EndDate">经期结束日期；尚未结束时为 <see langword="null"/></param>
/// <param name="DurationDays">经期持续天数；进行中时为已经过的天数</param>
/// <param name="CycleDays">距上一次开始的间隔天数；首条记录为 <see langword="null"/></param>
/// <param name="CycleDelta">本次间隔相对既往平均值的偏差天数；无法比较时为 <see langword="null"/></param>
/// <param name="Rhythm">本次间隔与既往规律的关系</param>
/// <param name="Tags">页面上直接渲染的标签</param>
/// <param name="Note">周期补充备注</param>
/// <param name="Summary">本次周期的小结</param>
/// <param name="LogCount">本次经期内追加的补充记录条数</param>
/// <param name="PeakFlow">本次经期内记录到的最大经量</param>
/// <param name="Symptoms">本次经期内记录过的全部不适</param>
/// <param name="IsActive">是否为正在进行中的记录</param>
/// <param name="CreatedByName">创建记录的用户显示名称</param>
/// <param name="UpdatedByName">最后修改记录的用户显示名称</param>
/// <param name="CreatedAt">记录创建时间</param>
/// <param name="UpdatedAt">记录最后更新时间</param>
public sealed record CycleRecordItem(
    int Id,
    DateOnly StartDate,
    DateOnly? EndDate,
    int DurationDays,
    int? CycleDays,
    int? CycleDelta,
    CycleRhythm Rhythm,
    IReadOnlyList<CycleTag> Tags,
    string Note,
    CycleSummaryText Summary,
    int LogCount,
    CycleFlow PeakFlow,
    CycleSymptom Symptoms,
    bool IsActive,
    string CreatedByName,
    string UpdatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 周期月历中的一天
/// </summary>
/// <param name="Date">当前日历日期</param>
/// <param name="IsInMonth">是否属于当前展示月份</param>
/// <param name="IsToday">是否为今天</param>
/// <param name="IsFuture">是否晚于今天</param>
/// <param name="Phase">当天所处的阶段</param>
/// <param name="DayOfCycle">当天位于本轮周期的第几天</param>
/// <param name="PeriodDay">当天是这次经期的第几天；不在经期内时为 <see langword="null"/></param>
/// <param name="IsPeriodStart">是否为某条记录的开始日</param>
/// <param name="IsPeriodEnd">是否为某条记录的结束日</param>
/// <param name="IsExpectedStart">是否为预测的下次经期开始日期</param>
/// <param name="Record">覆盖当天的周期记录；没有时为 <see langword="null"/></param>
/// <param name="Logs">当天按记录时间排列的补充记录</param>
public sealed record CycleCalendarDay(
    DateOnly Date,
    bool IsInMonth,
    bool IsToday,
    bool IsFuture,
    CyclePhase Phase,
    int? DayOfCycle,
    int? PeriodDay,
    bool IsPeriodStart,
    bool IsPeriodEnd,
    bool IsExpectedStart,
    CycleRecordItem? Record,
    IReadOnlyList<CycleDayLog> Logs);

/// <summary>
/// 单月周期日历数据
/// </summary>
/// <param name="Year">当前展示年份</param>
/// <param name="Month">当前展示月份</param>
/// <param name="Rows">当前月历行数</param>
/// <param name="MinimumYear">年份选择器最小值</param>
/// <param name="MaximumYear">年份选择器最大值</param>
/// <param name="PreviousMonth">上一个月的基准日期</param>
/// <param name="NextMonth">下一个月的基准日期</param>
/// <param name="Today">服务端认定的今天，前端据此限制未来日期</param>
/// <param name="Days">按日历布局生成的日期集合</param>
public sealed record CycleCalendarMonth(
    int Year,
    int Month,
    int Rows,
    int MinimumYear,
    int MaximumYear,
    DateOnly PreviousMonth,
    DateOnly NextMonth,
    DateOnly Today,
    IReadOnlyList<CycleCalendarDay> Days);

/// <summary>
/// 当前周期状态
/// </summary>
/// <param name="IsActive">当前是否处于已登记但尚未结束的经期中</param>
/// <param name="ActiveRecordId">当前进行中的周期记录标识；无进行中记录时为 <see langword="null"/></param>
/// <param name="StartedOn">当前经期开始日期；无进行中记录时为 <see langword="null"/></param>
/// <param name="ActiveDay">当前经期进行到的天数；未处于经期时为 0</param>
/// <param name="ExpectedEnd">根据历史平均经期推算的预计结束日期；无法推算时为 <see langword="null"/></param>
/// <param name="DaysUntilExpectedStart">距离预计下次经期开始的天数；无法预测时为 <see langword="null"/></param>
/// <param name="IsLate">当前日期是否已经超过预计经期开始日期</param>
/// <param name="Phase">今天所处的阶段</param>
/// <param name="DayOfCycle">今天位于本轮周期的第几天；无法判断时为 <see langword="null"/></param>
/// <param name="CycleLength">本轮周期的参考总长度，用于环形进度；无法判断时为 <see langword="null"/></param>
/// <param name="Headline">卡片上的主标题</param>
/// <param name="Detail">卡片上的补充说明</param>
/// <param name="Summary">本次周期的小结；暂无记录时为 <see langword="null"/></param>
public sealed record CycleCurrentStatus(
    bool IsActive,
    int? ActiveRecordId,
    DateOnly? StartedOn,
    int ActiveDay,
    DateOnly? ExpectedEnd,
    int? DaysUntilExpectedStart,
    bool IsLate,
    CyclePhase Phase,
    int? DayOfCycle,
    int? CycleLength,
    string Headline,
    string Detail,
    CycleSummaryText? Summary) {
    /// <summary>
    /// 获取环形进度的完成比例，取值 0 到 1
    /// </summary>
    public double Progress => DayOfCycle is { } day && CycleLength is { } length && length > 0
        ? Math.Clamp(day / (double)length, 0, 1)
        : 0;
}

/// <summary>
/// 花信如期页面使用的完整聚合数据
/// </summary>
/// <param name="Current">当前周期状态</param>
/// <param name="Statistics">历史周期统计与预测结果</param>
/// <param name="Calendar">当前展示月份的周期日历</param>
/// <param name="History">分页后的历史周期记录</param>
public sealed record CycleDashboard(
    CycleCurrentStatus Current,
    CycleStatistics Statistics,
    CycleCalendarMonth Calendar,
    PagedList<CycleRecordItem> History);

/// <summary>
/// 周期记录写入动作结果状态
/// </summary>
public enum CycleWriteStatus {
    /// <summary>
    /// 已成功保存记录
    /// </summary>
    Saved,

    /// <summary>
    /// 当前记录存在可疑情况，需要用户确认后才能继续写入
    /// </summary>
    RequiresConfirmation,

    /// <summary>
    /// 当前请求已处理，无需重复执行写入
    /// </summary>
    AlreadyProcessed,

    /// <summary>
    /// 当前记录与已有周期数据发生冲突
    /// </summary>
    Conflict,

    /// <summary>
    /// 提交的数据不符合周期记录规则
    /// </summary>
    Invalid,

    /// <summary>
    /// 当前用户无权执行该写入操作
    /// </summary>
    Forbidden
}

/// <summary>
/// 花信周期写入动作的结构化结果
/// </summary>
/// <param name="Status">写入动作状态</param>
/// <param name="Message">面向调用方的结果说明</param>
/// <param name="RecordId">关联的周期记录标识；无对应记录时为 <see langword="null"/></param>
public sealed record CycleWriteResult(
    CycleWriteStatus Status,
    string Message,
    int? RecordId = null) {
    /// <summary>
    /// 获取本次写入请求是否已成功完成或已被幂等处理
    /// </summary>
    public bool IsSuccess => Status is CycleWriteStatus.Saved or CycleWriteStatus.AlreadyProcessed;
}

/// <summary>
/// 周期记录完整表单的提交内容
/// </summary>
/// <param name="StartDate">本次经期开始日期</param>
/// <param name="EndDate">本次经期结束日期；留空表示仍在进行</param>
/// <param name="Note">本次周期的补充备注</param>
/// <param name="RequestKey">用于防止重复提交的请求唯一键</param>
/// <param name="ConfirmSuspicious">是否确认继续提交被规则判定为可疑的记录</param>
public sealed record CycleRecordSubmission(
    DateOnly StartDate,
    DateOnly? EndDate,
    string Note,
    string RequestKey,
    bool ConfirmSuspicious = false);

/// <summary>
/// 在月历中补充某一天的提交内容
/// </summary>
/// <param name="Date">这条记录对应的日期</param>
/// <param name="Flow">当天经量</param>
/// <param name="Mood">当天心情</param>
/// <param name="Pain">当天不适程度，0 到 3</param>
/// <param name="Symptoms">当天记下的不适</param>
/// <param name="Note">当天的补充说明</param>
/// <param name="IsIntimate">是否记录了亲密互动</param>
/// <param name="IntimacyProtection">采用的安全措施</param>
/// <param name="IntimacyOutcome">亲密互动的结束方式</param>
/// <param name="IntimacyCount">这条记录包含的亲密互动次数，默认 1 次</param>
public sealed record CycleDaySubmission(
    DateOnly Date,
    CycleFlow Flow,
    CycleMood Mood,
    int Pain,
    CycleSymptom Symptoms,
    string Note,
    bool IsIntimate = false,
    CycleIntimacyProtection IntimacyProtection = CycleIntimacyProtection.Unset,
    CycleIntimacyOutcome IntimacyOutcome = CycleIntimacyOutcome.Unset,
    int IntimacyCount = 1);

/// <summary>
/// 周期规则分析参数，独立建模以支持后续配置或其它分析实现
/// </summary>
public sealed class CycleAnalysisOptions {
    /// <summary>
    /// 获取参与分析的最小有效周期天数
    /// </summary>
    public int MinimumCycleDays { get; init; } = 15;

    /// <summary>
    /// 获取参与分析的最大有效周期天数
    /// </summary>
    public int MaximumCycleDays { get; init; } = 60;

    /// <summary>
    /// 获取无有效样本时采用的默认周期长度
    /// </summary>
    public int DefaultCycleDays { get; init; } = 28;

    /// <summary>
    /// 获取单次经期允许的最大持续天数
    /// </summary>
    public int MaximumPeriodDays { get; init; } = 15;

    /// <summary>
    /// 获取判定经期偏短的天数上限
    /// </summary>
    public int ShortPeriodDays { get; init; } = 3;

    /// <summary>
    /// 获取判定经期偏长的天数下限
    /// </summary>
    public int LongPeriodDays { get; init; } = 8;

    /// <summary>
    /// 获取黄体期长度，用于由下次开始日期倒推排卵日
    /// </summary>
    public int LutealPhaseDays { get; init; } = 14;

    /// <summary>
    /// 获取排卵日之前计入易孕期的天数
    /// </summary>
    public int FertileDaysBefore { get; init; } = 5;

    /// <summary>
    /// 获取排卵日之后计入易孕期的天数
    /// </summary>
    public int FertileDaysAfter { get; init; } = 1;

    /// <summary>
    /// 获取预测窗口在预计日期两侧最少浮动的天数
    /// </summary>
    public int MinimumWindowDays { get; init; } = 1;

    /// <summary>
    /// 获取预测窗口在预计日期两侧最多浮动的天数
    /// </summary>
    /// <remarks>
    /// 窗口宽度根据历史波动动态调整；记录越规律，窗口越窄。
    /// </remarks>
    public int MaximumWindowDays { get; init; } = 4;

    /// <summary>
    /// 获取判定周期偏早或偏晚所允许的偏差天数下限
    /// </summary>
    public int RhythmToleranceDays { get; init; } = 3;

    /// <summary>
    /// 获取判定相邻记录可能重复时允许的最小间隔天数
    /// </summary>
    public int MinimumDuplicateGapDays { get; init; } = 10;

    /// <summary>
    /// 获取判定相邻记录可能重复时使用的周期间隔比例阈值
    /// </summary>
    public double DuplicateGapRatio { get; init; } = .55;

    /// <summary>
    /// 获取周期分析最多使用的历史样本数量
    /// </summary>
    public int MaximumAnalysisSamples { get; init; } = 12;

    /// <summary>
    /// 获取周期备注允许的最大长度
    /// </summary>
    public int MaximumNoteLength { get; init; } = 500;

    /// <summary>
    /// 获取每日补充说明允许的最大长度
    /// </summary>
    public int MaximumDayNoteLength { get; init; } = 300;
}
