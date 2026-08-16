// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;

namespace OurStory.Services.Anniversaries;

/// <summary>
/// 后台纪念日编辑数据
/// </summary>
public class AnniversaryEditModel {
    /// <summary>
    /// 获取或设置名称
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置日期
    /// </summary>
    public DateOnly AnniversaryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// 获取或设置纪念日遵循的历法；日期字段本身始终是换算后的公历日期
    /// </summary>
    public AnniversaryCalendarType CalendarType { get; set; } = AnniversaryCalendarType.Solar;

    /// <summary>
    /// 获取或设置简短故事
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// 获取或设置封面图地址；留空时从 Markdown 正文提取第一张图
    /// </summary>
    public string? CoverUrl { get; set; }

    /// <summary>
    /// 获取或设置分类
    /// </summary>
    public AnniversaryKind Kind { get; set; } = AnniversaryKind.Love;

    /// <summary>
    /// 获取或设置是否每年重复
    /// </summary>
    public bool RepeatYearly { get; set; } = true;

    /// <summary>
    /// 获取或设置是否仅情侣双方可见
    /// </summary>
    public bool IsPrivate { get; set; }
}
