// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using System.Globalization;

namespace OurStory.Services.Cycles;

/// <summary>
/// 将持久化事实转换为页面展示模型
/// </summary>
/// <remarks>
/// 每次请求创建一个实例，以保证历史时间轴、月历和当前状态使用同一份事实快照。
/// </remarks>
internal sealed class CycleProjection {
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("zh-CN");

    private readonly ICycleAnalysisService _analysis;
    private readonly CycleAnalysisOptions _options;
    private readonly SiteSettings _site;
    private readonly DateOnly _today;
    private readonly SiteClock _clock;
    private readonly CycleRecord[] _records;
    private readonly CycleDailyLog[] _logs;
    private readonly CycleFact[] _facts;

    /// <summary>
    /// 按“此前记录条数”缓存的历史基线，避免逐条记录重复分析
    /// </summary>
    private readonly Dictionary<int, CycleStatistics> _baselines = [];

    /// <summary>
    /// 创建页面投影
    /// </summary>
    /// <param name="analysis">周期分析服务</param>
    /// <param name="options">周期规则参数</param>
    /// <param name="site">用于解析双方显示名称的站点设置</param>
    /// <param name="today">当前日期</param>
    /// <param name="records">这段关系下的全部周期记录</param>
    /// <param name="logs">相关日期范围内的每日补充记录</param>
    public CycleProjection(
        ICycleAnalysisService analysis,
        CycleAnalysisOptions options,
        SiteSettings site,
        SiteClock clock,
        IEnumerable<CycleRecord> records,
        IEnumerable<CycleDailyLog> logs) {
        _analysis = analysis;
        _options = options;
        _site = site;
        _clock = clock;
        _today = clock.Today;
        _records = [.. records.OrderBy(item => item.StartDate).ThenBy(item => item.Id)];
        _logs = [.. logs.OrderBy(item => item.Date).ThenBy(item => item.CreatedAt).ThenBy(item => item.Id)];
        _facts = [.. _records.Select(item => new CycleFact(item.StartDate, item.EndDate))];
        Statistics = analysis.Analyze(_facts, _today);
    }

    /// <summary>
    /// 获取根据当前事实计算的统计与预测
    /// </summary>
    public CycleStatistics Statistics { get; }

    /// <summary>
    /// 获取正在进行的记录；不存在时为 <see langword="null"/>
    /// </summary>
    public CycleRecord? Active => Array.Find(_records, item => item.EndDate is null);

    /// <summary>
    /// 将一条记录转换为页面展示项
    /// </summary>
    /// <param name="record">周期记录</param>
    /// <returns>历史时间轴与月历共用的展示条目</returns>
    public CycleRecordItem Item(CycleRecord record) {
        ArgumentNullException.ThrowIfNull(record);

        var prior = Prior(record);
        var cycleDays = CycleDays(prior, record);
        var appraisal = _analysis.Appraise(Fact(record), cycleDays, Baseline(prior), _today);
        var days = LogsIn(record);
        var context = Context(record, prior, cycleDays, appraisal);

        return new CycleRecordItem(
            record.Id,
            record.StartDate,
            record.EndDate,
            Duration(record),
            cycleDays,
            appraisal.CycleDelta,
            appraisal.Rhythm,
            appraisal.Tags,
            record.Note,
            Summary(record, context),
            days.Length,
            days.Select(item => item.Flow).DefaultIfEmpty(CycleFlow.Unset).Max(),
            days.Aggregate(CycleSymptom.None, (all, item) => all | item.Symptoms),
            record.EndDate is null,
            Name(record.CreatedByUser),
            Name(record.UpdatedByUser),
            record.CreatedAt,
            record.UpdatedAt);
    }

    /// <summary>
    /// 整理生成小结所需的事实上下文
    /// </summary>
    /// <param name="record">周期记录</param>
    /// <returns>供规则或模型使用的上下文</returns>
    /// <remarks>
    /// 上下文只包含目标周期及其之前的记录，比较基线也只由此前记录算出，
    /// 因此补写旧周期的小结时不会看到当时尚未发生的数据。
    /// </remarks>
    public CycleNarrativeContext Context(CycleRecord record) {
        ArgumentNullException.ThrowIfNull(record);

        var prior = Prior(record);
        var cycleDays = CycleDays(prior, record);
        return Context(record, prior, cycleDays, _analysis.Appraise(Fact(record), cycleDays, Baseline(prior), _today));
    }

    /// <summary>
    /// 生成顶部卡片使用的当前状态
    /// </summary>
    /// <returns>当前周期状态</returns>
    public CycleCurrentStatus Current() {
        var prediction = Statistics.NextPrediction;
        var phase = _analysis.Describe(_today, _facts, Statistics, _today);
        var cycleLength = Statistics.AverageCycleDays ?? (_records.Length > 0 ? _options.DefaultCycleDays : null);

        var covering = Array.Find(_records, item => _today >= item.StartDate && _today <= (item.EndDate ?? _today));
        if (covering is not null) {
            var day = Math.Max(1, _today.DayNumber - covering.StartDate.DayNumber + 1);
            var isActive = covering.EndDate is null;
            var expectedEnd = isActive && Statistics.AveragePeriodDays is { } duration
                ? covering.StartDate.AddDays(duration - 1)
                : covering.EndDate;

            return new CycleCurrentStatus(
                isActive,
                isActive ? covering.Id : null,
                covering.StartDate,
                day,
                expectedEnd,
                null,
                false,
                phase.Phase,
                phase.DayOfCycle,
                cycleLength,
                $"经期第 {day} 天",
                Detail(covering, isActive, expectedEnd),
                Summary(covering, Context(covering)));
        }

        var latest = _records.LastOrDefault();
        var summary = latest is null ? null : Summary(latest, Context(latest));

        if (prediction is null) {
            return new CycleCurrentStatus(
                false, null, null, 0, null, null, false,
                phase.Phase, phase.DayOfCycle, cycleLength,
                "等待第一条花信记录",
                "共同记下第一次开始日期后，这里会逐步形成周期、排卵日与易孕期参考。",
                summary);
        }

        var until = prediction.ExpectedStart.DayNumber - _today.DayNumber;
        var headline = until switch {
            > 0 => $"预计还有 {until} 天",
            0 => "预计日期为今天",
            _ => $"已超过预计日期 {-until} 天"
        };
        var window = prediction.WindowDays <= 1
            ? $"参考日期 {Short(prediction.ExpectedStart)}"
            : $"参考窗口 {Short(prediction.WindowStart)} – {Short(prediction.WindowEnd)}";

        return new CycleCurrentStatus(
            false,
            null,
            null,
            0,
            null,
            until,
            until < 0,
            phase.Phase,
            phase.DayOfCycle,
            cycleLength,
            headline,
            prediction.SampleCount == 0
                ? $"{window}，当前按 {_options.DefaultCycleDays} 天默认周期推算；继续共同记录可提高参考准确度"
                : $"{window} · 依据 {prediction.SampleCount} 个周期，可信度 {prediction.Confidence}%",
            summary);
    }

    /// <summary>
    /// 生成指定月份的日历
    /// </summary>
    /// <param name="month">该月任意一天</param>
    /// <returns>按日历布局生成的月份数据</returns>
    public CycleCalendarMonth Calendar(DateOnly month) {
        var first = new DateOnly(month.Year, month.Month, 1);
        var leading = ((int)first.DayOfWeek + 6) % 7;
        var rows = Math.Max(5, (int)Math.Ceiling((leading + DateTime.DaysInMonth(first.Year, first.Month)) / 7d));
        var gridStart = first.AddDays(-leading);
        var expectedStart = Statistics.NextPrediction?.ExpectedStart;

        var items = new Dictionary<int, CycleRecordItem>();
        var days = new CycleCalendarDay[rows * 7];

        for (var index = 0; index < days.Length; index++) {
            var date = gridStart.AddDays(index);
            var record = Array.Find(_records, item => date >= item.StartDate && date <= (item.EndDate ?? _today));
            var phase = _analysis.Describe(date, _facts, Statistics, _today);

            if (record is not null && !items.ContainsKey(record.Id)) {
                items[record.Id] = Item(record);
            }

            days[index] = new CycleCalendarDay(
                date,
                date.Month == first.Month,
                date == _today,
                date > _today,
                phase.Phase,
                phase.DayOfCycle,
                record is null ? null : date.DayNumber - record.StartDate.DayNumber + 1,
                record?.StartDate == date,
                record?.EndDate == date,
                expectedStart == date,
                record is null ? null : items[record.Id],
                Logs(date));
        }

        var minimumYear = Math.Max(1900, _records.Select(item => item.StartDate.Year).DefaultIfEmpty(_today.Year).Min());
        var maximumYear = Math.Min(2200, Math.Max(_today.Year + 2, first.Year));

        return new CycleCalendarMonth(
            first.Year,
            first.Month,
            rows,
            minimumYear,
            maximumYear,
            first.AddMonths(-1),
            first.AddMonths(1),
            _today,
            days);
    }

    #region 私有方法

    private static string Detail(CycleRecord covering, bool isActive, DateOnly? expectedEnd) {
        if (!isActive) {
            return $"记录始于 {Short(covering.StartDate)}，结束于 {Short(covering.EndDate!.Value)}，已保留在双方的历史时间轴中";
        }

        return expectedEnd is { } end
            ? $"记录始于 {Short(covering.StartDate)}，根据历史时长预计在 {Short(end)} 前后结束"
            : $"记录始于 {Short(covering.StartDate)}，结束后记下日期，我们会一起保留这次变化";
    }

    private static CycleFact Fact(CycleRecord record) => new(record.StartDate, record.EndDate);

    private int Duration(CycleRecord record) =>
        (record.EndDate ?? _today).DayNumber - record.StartDate.DayNumber + 1;

    private static int? CycleDays(CycleRecord[] prior, CycleRecord record) {
        var previous = prior
            .Where(item => item.StartDate < record.StartDate)
            .Select(item => item.StartDate)
            .DefaultIfEmpty()
            .Max();
        return previous == default ? null : record.StartDate.DayNumber - previous.DayNumber;
    }

    private CycleRecord[] Prior(CycleRecord record) => [.. _records.Where(item => Precedes(item, record))];

    // 与 _records 的排序保持一致：开始日期相同的重复登记按记录先后排序，
    // 这样序号与基线的取值范围都是 _records 的前缀，不会因取用顺序不同而漂移。
    private static bool Precedes(CycleRecord earlier, CycleRecord later) =>
        earlier.StartDate < later.StartDate
        || (earlier.StartDate == later.StartDate && earlier.Id < later.Id);

    private CycleStatistics Baseline(CycleRecord[] prior) {
        if (_baselines.TryGetValue(prior.Length, out var cached)) {
            return cached;
        }

        var baseline = _analysis.Analyze([.. prior.Select(Fact)], _today);
        _baselines[prior.Length] = baseline;
        return baseline;
    }

    private CycleDailyLog[] LogsIn(CycleRecord record) {
        var end = record.EndDate ?? _today;
        return [.. _logs.Where(item => item.Date >= record.StartDate && item.Date <= end)];
    }

    private CycleDayLog[] Logs(DateOnly date) => [.. _logs
        .Where(item => item.Date == date)
        .Select(item => new CycleDayLog(
            item.Flow,
            item.Mood,
            item.Pain,
            item.Symptoms,
            item.Note,
            item.IsIntimate,
            item.IntimacyCount,
            item.IntimacyProtection,
            item.IntimacyOutcome,
            item.CreatedByUserId,
            Name(item.CreatedByUser),
            _clock.ToLocal(item.CreatedAt)))];

    private CycleNarrativeContext Context(
        CycleRecord record,
        CycleRecord[] prior,
        int? cycleDays,
        CycleAppraisal appraisal) {
        var baseline = Baseline(prior);
        var window = prior.TakeLast(CycleNarrative.HistoryWindow - 1).ToArray();
        var offset = prior.Length - window.Length;

        return new CycleNarrativeContext(
            record.StartDate,
            record.EndDate,
            Duration(record),
            cycleDays,
            appraisal.CycleDelta,
            appraisal.Rhythm,
            record.Note,
            baseline.AverageCycleDays,
            baseline.AveragePeriodDays,
            DayFacts(record),
            prior.Length + 1,
            [.. window.Select((item, index) => new CyclePastFact(
                offset + index + 1,
                item.StartDate,
                item.EndDate,
                Duration(item),
                CycleDays(prior[..(offset + index)], item),
                item.Note,
                DayFacts(item)))]);
    }

    private CycleDayFact[] DayFacts(CycleRecord record) => [.. LogsIn(record)
        .GroupBy(item => item.Date)
        .OrderBy(group => group.Key)
        .Select(group => {
            var entries = group.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).ToArray();
            var mood = entries.LastOrDefault(item => item.Mood != CycleMood.Unset)?.Mood ?? CycleMood.Unset;
            return new CycleDayFact(
                group.Key,
                entries.Select(item => item.Flow).DefaultIfEmpty(CycleFlow.Unset).Max(),
                mood,
                entries.Max(item => item.Pain),
                entries.Aggregate(CycleSymptom.None, (all, item) => all | item.Symptoms),
                string.Join("；", entries.Select(item => item.Note).Where(note => note.Length > 0)));
        })];

    private static CycleSummaryText Summary(CycleRecord record, CycleNarrativeContext context) =>
        record.Summary.Length > 0 && record.SummaryStamp == CycleNarrative.Stamp(context)
            ? new CycleSummaryText(record.Summary, record.SummarySource, record.SummaryUpdatedAt)
            : new CycleSummaryText(CycleNarrative.Compose(context), CycleSummarySource.Rule, null);

    private string Name(User? user) => user is null ? "未知" : _site.RoleName(user.Role);

    private static string Short(DateOnly date) => date.ToString("M 月 d 日", Culture);

    #endregion
}
