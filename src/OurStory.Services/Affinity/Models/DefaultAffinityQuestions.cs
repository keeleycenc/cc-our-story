// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Services.Affinity;

/// <summary>
/// 获取心有灵犀题目初始化数据
/// </summary>
/// <param name="Category">获取题目分类</param>
/// <param name="Text">获取题目内容</param>
/// <param name="Options">获取题目选项集合</param>
internal sealed record AffinityQuestionSeed(string Category, string Text, params string[] Options);

/// <summary>
/// 获取默认心有灵犀题目集合
/// </summary>
internal static class DefaultAffinityQuestions {
    /// <summary>
    /// 获取默认题目列表
    /// </summary>
    public static IReadOnlyList<AffinityQuestionSeed> All { get; } = [
        new("日常", "如果今晚突然空出三个小时，你最想和 TA 做什么？", "出去吃点东西", "看一部电影", "随便散散步", "什么都不做，待在一起"),
        new("吃喝玩乐", "周末临时决定出门，你更想去哪里？", "安静的咖啡店", "热闹的商场", "公园或郊外", "没去过的新地方"),
        new("爱情", "哪一种小事最能让你感到被爱？", "一句认真回应", "一个拥抱", "记住我的喜好", "默默帮我做好事情"),
        new("回忆", "如果重播我们的一段回忆，你最想选哪类？", "第一次见面", "一次难忘旅行", "某个普通却开心的晚上", "一起跨过的低谷"),
        new("未来", "理想中的两人假期更接近哪一种？", "海边放空", "城市漫游", "山野露营", "在家慢生活"),
        new("二选一", "一起吃饭时，你更愿意怎么选？", "一直去熟悉的爱店", "每次都试一家新店"),
        new("脑洞", "如果我们能共同拥有一种超能力，你会选？", "随时瞬移", "暂停时间", "读懂彼此心情", "永远精力充沛"),
        new("日常", "忙碌的一天结束后，你最需要哪种陪伴？", "聊聊今天", "安静地靠在一起", "一起吃点好吃的", "各自放松但待在同一空间"),
        new("吃喝玩乐", "两个人点外卖时，你最容易被哪一类打动？", "烧烤炸物", "火锅冒菜", "米饭面食", "甜品饮料"),
        new("未来", "以后共同的家里，你最想先布置哪个角落？", "舒适的卧室", "一起做饭的厨房", "能窝着的客厅", "放满回忆的展示墙"),
        new("爱情", "发生小分歧时，你更希望我们先做什么？", "马上说清楚", "各自冷静一会儿", "先抱一下再聊", "用文字整理想法"),
        new("回忆", "我们相处中最值得收藏的瞬间通常是？", "精心准备的惊喜", "旅行中的风景", "日常里的小默契", "彼此需要时的陪伴")
    ];
}
