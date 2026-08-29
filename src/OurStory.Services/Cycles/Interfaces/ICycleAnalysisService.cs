// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core.Models;

namespace OurStory.Services.Cycles;

/// <summary>
/// 花信周期统计与预测边界服务接口
/// </summary>
public interface ICycleAnalysisService {
    /// <summary>
    /// 根据历史周期事实与指定日期动态生成统计及预测结果
    /// </summary>
    /// <param name="facts">用于分析的历史周期事实集合</param>
    /// <param name="today">作为当前时间基准的日期</param>
    /// <returns>根据有效历史样本计算得到的周期统计与预测结果</returns>
    CycleStatistics Analyze(IReadOnlyList<CycleFact> facts, DateOnly today);

    /// <summary>
    /// 判断某一天落在周期的哪个阶段
    /// </summary>
    /// <param name="date">待判断的日期</param>
    /// <param name="facts">用于判断的历史周期事实集合</param>
    /// <param name="statistics">根据同一组事实计算的统计结果</param>
    /// <param name="today">作为当前时间基准的日期</param>
    /// <returns>当天所处的阶段及其在周期中的位置</returns>
    CycleDayPhase Describe(
        DateOnly date,
        IReadOnlyList<CycleFact> facts,
        CycleStatistics statistics,
        DateOnly today);

    /// <summary>
    /// 对照既往规律评价一条周期记录，给出标签
    /// </summary>
    /// <param name="fact">待评价的周期记录</param>
    /// <param name="cycleDays">这条记录距上一次开始的间隔天数；首条记录传 <see langword="null"/></param>
    /// <param name="statistics">根据同一组事实计算的统计结果</param>
    /// <param name="today">作为当前时间基准的日期</param>
    /// <returns>这条记录的规律判断与页面标签</returns>
    CycleAppraisal Appraise(
        CycleFact fact,
        int? cycleDays,
        CycleStatistics statistics,
        DateOnly today);

    /// <summary>
    /// 判断候选经期开始日期是否偏离已有周期规律，需要用户进行二次确认
    /// </summary>
    /// <param name="facts">用于判断的历史周期事实集合</param>
    /// <param name="candidate">待登记的经期开始日期</param>
    /// <param name="reason">需要二次确认时返回对应原因，否则为空字符串</param>
    /// <returns>候选日期需要二次确认时返回 <see langword="true"/>；否则返回 <see langword="false"/></returns>
    bool IsSuspiciousStart(IReadOnlyList<CycleFact> facts, DateOnly candidate, out string reason);
}
