// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core;
using OurStory.Core.Models;

namespace OurStory.Services.Cycles;

internal sealed class RuleBasedCycleAnalysisService(CycleAnalysisOptions options) : ICycleAnalysisService {
    private const string Analyzer = "rules-v2"; // 写入统计结果的分析器版本标识

    public CycleStatistics Analyze(IReadOnlyList<CycleFact> facts, DateOnly today) {
        ArgumentNullException.ThrowIfNull(facts);

        var ordered = Ordered(facts);
        var completed = ordered.Where(item => item.EndDate is not null).ToArray();
        var durations = completed
            .Select(item => Days(item.StartDate, item.EndDate!.Value))
            .Where(days => days > 0 && days <= options.MaximumPeriodDays)
            .TakeLast(options.MaximumAnalysisSamples)
            .ToArray();
        var intervals = Intervals(ordered);

        var averageCycle = Average(intervals);
        var swing = Swing(intervals, averageCycle);

        return new CycleStatistics(
            ordered.Length,
            completed.Length,
            averageCycle,
            Average(durations),
            intervals.Length == 0 ? null : intervals.Min(),
            intervals.Length == 0 ? null : intervals.Max(),
            swing,
            Predict(ordered, averageCycle, swing, intervals.Length),
            Analyzer);
    }

    public CycleDayPhase Describe(
        DateOnly date,
        IReadOnlyList<CycleFact> facts,
        CycleStatistics statistics,
        DateOnly today) {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(statistics);

        var ordered = Ordered(facts);
        var previousStart = ordered.LastOrDefault(item => item.StartDate <= date)?.StartDate;
        var dayOfCycle = previousStart is { } anchor && Days(anchor, date) <= options.MaximumCycleDays + options.MaximumPeriodDays
            ? Days(anchor, date)
            : (int?)null;

        if (ordered.Any(item => date >= item.StartDate && date <= (item.EndDate ?? today))) {
            return new CycleDayPhase(CyclePhase.Period, dayOfCycle);
        }

        var prediction = statistics.NextPrediction;
        if (prediction is not null
            && date >= prediction.WindowStart
            && (date <= prediction.WindowEnd || date <= today)) {
            return new CycleDayPhase(CyclePhase.Predicted, dayOfCycle);
        }

        var nextStart = ordered.FirstOrDefault(item => item.StartDate > date)?.StartDate
            ?? (prediction is { } forecast && forecast.ExpectedStart > date ? forecast.ExpectedStart : null);
        if (nextStart is null || previousStart is null) {
            return new CycleDayPhase(CyclePhase.Unknown, dayOfCycle);
        }

        var ovulation = nextStart.Value.AddDays(-options.LutealPhaseDays);
        if (ovulation <= previousStart.Value) {
            return new CycleDayPhase(CyclePhase.Unknown, dayOfCycle);
        }

        if (date == ovulation) {
            return new CycleDayPhase(CyclePhase.Ovulation, dayOfCycle);
        }

        return date >= ovulation.AddDays(-options.FertileDaysBefore) && date <= ovulation.AddDays(options.FertileDaysAfter)
            ? new CycleDayPhase(CyclePhase.Fertile, dayOfCycle)
            : new CycleDayPhase(CyclePhase.Safe, dayOfCycle);
    }

    public CycleAppraisal Appraise(
        CycleFact fact,
        int? cycleDays,
        CycleStatistics statistics,
        DateOnly today) {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(statistics);

        var isActive = fact.EndDate is null;
        var duration = Days(fact.StartDate, fact.EndDate ?? today);
        var detail = new List<CycleTag>();
        var attention = false;

        if (isActive) {
            detail.Add(new CycleTag($"第 {duration} 天", CycleTagTone.Active));
        } else {
            detail.Add(new CycleTag($"持续 {duration} 天", CycleTagTone.Neutral));

            if (duration <= options.ShortPeriodDays) {
                detail.Add(new CycleTag("经期偏短", CycleTagTone.Attention));
                attention = true;
            } else if (duration >= options.LongPeriodDays) {
                detail.Add(new CycleTag("经期偏长", CycleTagTone.Attention));
                attention = true;
            }
        }

        var rhythm = CycleRhythm.Unknown;
        int? delta = null;

        if (cycleDays is { } gap) {
            detail.Add(new CycleTag($"周期 {gap} 天", CycleTagTone.Neutral));

            if (statistics.AverageCycleDays is { } average) {
                delta = gap - average;
                var tolerance = Tolerance(statistics.CycleSwingDays);

                if (delta <= -tolerance) {
                    rhythm = CycleRhythm.Early;
                    detail.Add(new CycleTag($"提前 {-delta} 天", CycleTagTone.Attention));
                    attention = true;
                } else if (delta >= tolerance) {
                    rhythm = CycleRhythm.Late;
                    detail.Add(new CycleTag($"推迟 {delta} 天", CycleTagTone.Attention));
                    attention = true;
                } else {
                    rhythm = CycleRhythm.Normal;
                }
            }
        } else {
            detail.Add(new CycleTag("首次记录", CycleTagTone.Neutral));
        }

        var leading = isActive
            ? new CycleTag("进行中", CycleTagTone.Active)
            : attention
                ? new CycleTag("需留意", CycleTagTone.Attention)
                : new CycleTag("正常", CycleTagTone.Normal);

        return new CycleAppraisal(rhythm, delta, [leading, .. detail]);
    }

    public bool IsSuspiciousStart(IReadOnlyList<CycleFact> facts, DateOnly candidate, out string reason) {
        ArgumentNullException.ThrowIfNull(facts);

        var prior = facts
            .Where(item => item.StartDate <= candidate)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefault();
        if (prior is null) {
            reason = string.Empty;
            return false;
        }

        var gap = candidate.DayNumber - prior.StartDate.DayNumber;
        var statistics = Analyze(facts, candidate);
        var threshold = Math.Max(
            options.MinimumDuplicateGapDays,
            (int)Math.Round(
                (statistics.AverageCycleDays ?? options.DefaultCycleDays) * options.DuplicateGapRatio,
                MidpointRounding.AwayFromZero));
        if (gap >= threshold) {
            reason = string.Empty;
            return false;
        }

        reason = gap == 0
            ? "同一天已有一条记录，可能是双方重复登记。"
            : $"距离上一条开始记录只有 {gap} 天，明显短于近期周期。";
        return true;
    }

    #region 私有方法

    private static CycleFact[] Ordered(IReadOnlyList<CycleFact> facts) =>
        [.. facts.OrderBy(item => item.StartDate)];

    private static int Days(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;

    private int[] Intervals(CycleFact[] ordered) =>
        [.. ordered
            .Zip(ordered.Skip(1), (first, second) => second.StartDate.DayNumber - first.StartDate.DayNumber)
            .Where(days => days >= options.MinimumCycleDays && days <= options.MaximumCycleDays)
            .TakeLast(options.MaximumAnalysisSamples)];

    private CyclePrediction? Predict(CycleFact[] ordered, int? averageCycle, int? swing, int samples) {
        if (ordered.LastOrDefault() is not { } latest) {
            return null;
        }

        var cycleDays = averageCycle ?? options.DefaultCycleDays;
        var expected = latest.StartDate.AddDays(cycleDays);
        var half = samples switch {
            0 => options.MaximumWindowDays,
            1 => Math.Clamp(2, options.MinimumWindowDays, options.MaximumWindowDays),
            _ => Math.Clamp(swing ?? 2, options.MinimumWindowDays, options.MaximumWindowDays)
        };
        var ovulation = expected.AddDays(-options.LutealPhaseDays);
        var hasOvulation = ovulation > latest.StartDate;

        return new CyclePrediction(
            expected,
            expected.AddDays(-half),
            expected.AddDays(half),
            hasOvulation ? ovulation : null,
            hasOvulation ? ovulation.AddDays(-options.FertileDaysBefore) : null,
            hasOvulation ? ovulation.AddDays(options.FertileDaysAfter) : null,
            samples,
            Confidence(samples, swing));
    }

    private static int Confidence(int samples, int? swing) {
        if (samples == 0) {
            return 20;
        }

        var breadth = Math.Min(1d, samples / 6d);
        var steadiness = 1d - Math.Min(1d, (swing ?? 3) / 7d);
        return Math.Clamp((int)Math.Round(100 * breadth * (.35 + (.65 * steadiness))), 20, 96);
    }

    private int Tolerance(int? swing) =>
        Math.Clamp(Math.Max(options.RhythmToleranceDays, (swing ?? 0) + 1), options.RhythmToleranceDays, 7);

    private static int? Average(int[] values) => values.Length == 0
        ? null
        : (int)Math.Round(values.Average(), MidpointRounding.AwayFromZero);

    private static int? Swing(int[] values, int? average) => values.Length == 0 || average is null
        ? null
        : (int)Math.Round(values.Average(value => Math.Abs(value - average.Value)), MidpointRounding.AwayFromZero);

    #endregion
}
