// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Models;

/// <summary>
/// 心有灵犀题目的固定分类
/// </summary>
public static class AffinityQuestionCategories {
    /// <summary>
    /// 获取后台创建题目时可选的全部分类
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [
        "日常", "爱情", "回忆", "未来",
        "旅行出游", "兴趣偏好", "相处方式", "默契挑战"
    ];

    /// <summary>
    /// 判断分类是否属于固定选项
    /// </summary>
    public static bool Contains(string category) => All.Contains(category, StringComparer.Ordinal);
}
