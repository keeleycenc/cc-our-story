// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Models;

/// <summary>
/// 提供花信枚举在界面与模型上下文中的统一显示名称
/// </summary>
/// <remarks>
/// 页面、小结文案与模型上下文共用同一套名称，确保各处表达一致。
/// </remarks>
public static class CycleLabels {
    /// <summary>
    /// 页面上按顺序展示的全部不适选项
    /// </summary>
    public static readonly IReadOnlyList<CycleSymptom> AllSymptoms = [
        CycleSymptom.Cramps,
        CycleSymptom.Backache,
        CycleSymptom.Headache,
        CycleSymptom.BreastTenderness,
        CycleSymptom.Fatigue,
        CycleSymptom.Nausea,
        CycleSymptom.Acne,
        CycleSymptom.Appetite,
        CycleSymptom.Insomnia,
        CycleSymptom.MoodSwings
    ];

    /// <summary>
    /// 页面上按顺序展示的全部经量选项
    /// </summary>
    public static readonly IReadOnlyList<CycleFlow> AllFlows = [
        CycleFlow.Spotting,
        CycleFlow.Light,
        CycleFlow.Medium,
        CycleFlow.Heavy
    ];

    /// <summary>
    /// 页面上按顺序展示的全部心情选项
    /// </summary>
    public static readonly IReadOnlyList<CycleMood> AllMoods = [
        CycleMood.Good,
        CycleMood.Calm,
        CycleMood.Low,
        CycleMood.Irritable,
        CycleMood.Tired
    ];

    /// <summary>
    /// 页面上按顺序展示的亲密互动安全措施
    /// </summary>
    public static readonly IReadOnlyList<CycleIntimacyProtection> AllIntimacyProtections = [
        CycleIntimacyProtection.Condom,
        CycleIntimacyProtection.Other,
        CycleIntimacyProtection.None
    ];

    /// <summary>
    /// 页面上按顺序展示的亲密互动结束方式
    /// </summary>
    public static readonly IReadOnlyList<CycleIntimacyOutcome> AllIntimacyOutcomes = [
        CycleIntimacyOutcome.External,
        CycleIntimacyOutcome.Internal,
        CycleIntimacyOutcome.NotApplicable
    ];

    /// <summary>
    /// 取得经量的中文说法
    /// </summary>
    /// <param name="flow">经量</param>
    /// <returns>页面上显示的文字</returns>
    public static string Name(this CycleFlow flow) => flow switch {
        CycleFlow.Spotting => "点滴",
        CycleFlow.Light => "偏少",
        CycleFlow.Medium => "适中",
        CycleFlow.Heavy => "偏多",
        _ => "未填"
    };

    /// <summary>
    /// 取得心情的中文说法
    /// </summary>
    /// <param name="mood">心情</param>
    /// <returns>页面上显示的文字</returns>
    public static string Name(this CycleMood mood) => mood switch {
        CycleMood.Good => "状态不错",
        CycleMood.Calm => "平静",
        CycleMood.Low => "低落",
        CycleMood.Irritable => "容易烦躁",
        CycleMood.Tired => "疲惫",
        _ => "未填"
    };

    /// <summary>
    /// 取得安全措施的中文说法
    /// </summary>
    public static string Name(this CycleIntimacyProtection protection) => protection switch {
        CycleIntimacyProtection.Condom => "安全套",
        CycleIntimacyProtection.Other => "其他措施",
        CycleIntimacyProtection.None => "未采取",
        _ => "未记录"
    };

    /// <summary>
    /// 取得亲密互动结束方式的中文说法
    /// </summary>
    public static string Name(this CycleIntimacyOutcome outcome) => outcome switch {
        CycleIntimacyOutcome.External => "体外",
        CycleIntimacyOutcome.Internal => "体内",
        CycleIntimacyOutcome.NotApplicable => "其它",
        _ => "未记录"
    };

    /// <summary>
    /// 取得单项不适的中文说法
    /// </summary>
    /// <param name="symptom">不适项</param>
    /// <returns>页面上显示的文字</returns>
    public static string Name(this CycleSymptom symptom) => symptom switch {
        CycleSymptom.Cramps => "腹痛",
        CycleSymptom.Backache => "腰酸",
        CycleSymptom.Headache => "头痛",
        CycleSymptom.BreastTenderness => "胸胀",
        CycleSymptom.Fatigue => "乏力",
        CycleSymptom.Nausea => "恶心",
        CycleSymptom.Acne => "长痘",
        CycleSymptom.Appetite => "胃口变化",
        CycleSymptom.Insomnia => "失眠",
        CycleSymptom.MoodSwings => "情绪起伏",
        _ => string.Empty
    };

    /// <summary>
    /// 取得不适程度的中文说法
    /// </summary>
    /// <param name="pain">不适程度，0 到 3</param>
    /// <returns>页面上显示的文字</returns>
    public static string PainName(int pain) => pain switch {
        1 => "轻微",
        2 => "明显",
        >= 3 => "较重",
        _ => "无"
    };

    /// <summary>
    /// 取得阶段的中文说法
    /// </summary>
    /// <param name="phase">阶段</param>
    /// <returns>页面上显示的文字</returns>
    public static string Name(this CyclePhase phase) => phase switch {
        CyclePhase.Period => "经期",
        CyclePhase.Predicted => "预测窗口",
        CyclePhase.Fertile => "易孕期",
        CyclePhase.Ovulation => "排卵日",
        CyclePhase.Safe => "安全期",
        _ => "待记录"
    };

    /// <summary>
    /// 取得阶段在页面上的一句说明
    /// </summary>
    /// <param name="phase">阶段</param>
    /// <returns>面向页面的解释文字</returns>
    public static string Describe(this CyclePhase phase) => phase switch {
        CyclePhase.Period => "已记录的经期日期。",
        CyclePhase.Predicted => "根据既往记录计算的下次经期参考窗口。",
        CyclePhase.Fertile => "根据平均周期推算的易孕期，仅供两个人共同参考。",
        CyclePhase.Ovulation => "根据平均周期推算的排卵日，仅供两个人共同参考。",
        CyclePhase.Safe => "易孕期以外的参考日期，不能作为避孕依据。",
        _ => "继续共同记录后，系统会逐步完善这一天的周期参考。"
    };

    /// <summary>
    /// 把一组不适拆成可以逐项显示的列表
    /// </summary>
    /// <param name="symptoms">按位存放的不适集合</param>
    /// <returns>按页面顺序排列的不适项</returns>
    public static IReadOnlyList<CycleSymptom> Split(this CycleSymptom symptoms) =>
        [.. AllSymptoms.Where(item => symptoms.HasFlag(item))];

    /// <summary>
    /// 把一组不适连成一句话
    /// </summary>
    /// <param name="symptoms">按位存放的不适集合</param>
    /// <returns>用顿号连接的说明；没有任何不适时返回空字符串</returns>
    public static string Join(this CycleSymptom symptoms) =>
        string.Join('、', symptoms.Split().Select(Name));
}
