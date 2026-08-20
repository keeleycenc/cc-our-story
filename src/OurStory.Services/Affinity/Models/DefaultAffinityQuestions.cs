// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Services.Affinity;

/// <summary>
/// 获取心有灵犀题目初始化数据
/// </summary>
/// <param name="IntroducedInVersion">获取该题目首次加入预设库的版本</param>
/// <param name="Category">获取题目分类</param>
/// <param name="Text">获取题目内容</param>
/// <param name="Options">获取题目选项集合（2 ~ 8）</param>
internal sealed record AffinityQuestionSeed(
    int IntroducedInVersion,
    string Category,
    string Text,
    params string[] Options);

/// <summary>
/// 获取默认心有灵犀题目集合
/// </summary>
internal static class DefaultAffinityQuestions {
    /// <summary>
    /// 获取当前预设题库版本；新增预设时递增此值，并把新题的 IntroducedInVersion 设为该版本
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// 获取默认题目列表
    /// </summary>
    public static IReadOnlyList<AffinityQuestionSeed> All { get; } = [
        new(1, "日常", "忙碌的一天结束后，你更期待怎样的相处？", "一起聊聊今天", "安静地待在一起", "出去走走", "各自放松，但陪在彼此身边"),
        new(1, "爱情", "对你来说，哪一种表达最容易让你感受到爱？", "认真倾听", "主动拥抱", "记住细节", "花时间陪伴", "实际行动", "直接说出爱意"),
        new(1, "回忆", "如果只能留下一类共同回忆，你会选择哪一种？", "初识时的心动", "一起旅行的日子", "平凡却开心的日常", "共同经历的重要时刻", "一起走过的困难"),
        new(1, "未来", "你更期待未来的两个人过怎样的生活？", "安稳而温暖", "自由而有趣", "一起不断成长", "经常探索新的地方", "简单自在就很好"),
        new(1, "旅行出游", "旅行时，你更喜欢哪一种节奏？", "提前规划好行程", "只定大方向，边走边决定", "完全随心，不做计划"),
        new(1, "兴趣偏好", "如果要培养一个共同爱好，你更愿意从哪一类开始？", "运动健身", "电影音乐", "摄影记录", "美食烹饪", "游戏娱乐", "户外探索"),
        new(1, "相处方式", "发生分歧时，你更倾向于怎样处理？", "当下说清楚", "先冷静，再沟通", "谁更在意就优先照顾谁的感受"),
        new(1, "默契挑战", "如果让你替 TA 选一份惊喜，你觉得自己最有把握选对哪一种？", "一顿喜欢的食物", "一件想要很久的东西", "一次临时出游", "一段只属于两个人的时间", "一个完全意料之外的安排")
    ];
}
