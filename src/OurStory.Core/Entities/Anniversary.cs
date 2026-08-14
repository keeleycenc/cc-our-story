// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Entities;

/// <summary>
/// 一个值得反复回望或等待的纪念日
/// </summary>
public class Anniversary {
    /// <summary>
    /// 获取或设置唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置纪念日名称
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置这个日子第一次发生的日期
    /// </summary>
    public DateOnly AnniversaryDate { get; set; }

    /// <summary>
    /// 获取或设置简短故事
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// 获取或设置由 Markdown 故事渲染得到的 HTML
    /// </summary>
    public string NoteHtml { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置封面图片；留空时使用故事中的第一张图片
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

    /// <summary>
    /// 获取或设置记录这篇纪念日的用户编号；历史数据为空时表示由两个人共同记录
    /// </summary>
    public int? AuthorId { get; set; }

    /// <summary>
    /// 获取或设置记录这篇纪念日的用户
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// 获取或设置创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置最后更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
