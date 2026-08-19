// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Models;

/// <summary>
/// 点滴列表项，用于首页和点滴列表页展示
/// </summary>
/// <remarks>
/// 对于当前访问者无权查看的受保护记录，服务层会提前清空摘要和封面等敏感内容。
/// 展示层只需按模型内容渲染，无需重复判断访问权限
/// </remarks>
public class MomentCard {
    /// <summary>
    /// 点滴记录 ID
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 点滴标题
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 点滴的 URL 标识
    /// </summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// 点滴内容摘要
    /// </summary>
    /// <remarks>
    /// 当前访问者无权查看受保护记录时为空字符串。
    /// </remarks>
    public string Excerpt { get; init; } = string.Empty;

    /// <summary>
    /// 封面图片地址
    /// </summary>
    /// <remarks>
    /// 当前访问者无权查看受保护记录时为空字符串。
    /// </remarks>
    public string CoverUrl { get; init; } = string.Empty;

    /// <summary>
    /// 点滴心情
    /// </summary>
    public string Mood { get; init; } = "日常";

    /// <summary>
    /// 点滴发生地点
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// 点滴发生时间
    /// </summary>
    /// <remarks>
    /// 已转换为站点配置的时区。
    /// </remarks>
    public DateTime MomentDate { get; init; }

    /// <summary>
    /// 当前访问者是否处于锁定状态
    /// </summary>
    /// <remarks>
    /// 为 <see langword="true"/> 时，表示该记录受保护且当前访问者尚未获得查看权限。
    /// </remarks>
    public bool IsLocked { get; init; }

    /// <summary>
    /// 点滴是否设置了访问密码
    /// </summary>
    /// <remarks>
    /// 与 <see cref="IsLocked"/> 不同，本属性描述记录本身是否受密码保护，
    /// 不受当前访问者是否已经解锁影响。
    /// </remarks>
    public bool IsProtected { get; init; }

    /// <summary>
    /// 作者显示名称
    /// </summary>
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>
    /// 评论数量
    /// </summary>
    public int CommentCount { get; init; }

    /// <summary>
    /// 点滴发生在相恋后的第几天
    /// </summary>
    /// <remarks>
    /// 为 0 时表示无法计算，例如点滴时间早于相恋日期。
    /// </remarks>
    public int LoveDay { get; init; }

    /// <summary>
    /// 点滴详情页地址
    /// </summary>
    public string Url => "/moments/" + Slug;
}

/// <summary>
/// 点滴详情页模型
/// </summary>
public class MomentDetail {
    /// <summary>
    /// 点滴记录 ID
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 点滴标题
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 点滴的 URL 标识
    /// </summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// 点滴正文 HTML
    /// </summary>
    /// <remarks>
    /// 当前访问者无权查看受保护记录时为空字符串，
    /// 详情页会据此显示密码验证界面。
    /// </remarks>
    public string ContentHtml { get; init; } = string.Empty;

    /// <summary>
    /// 点滴心情
    /// </summary>
    public string Mood { get; init; } = "日常";

    /// <summary>
    /// 点滴发生地点
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// 点滴发生时间
    /// </summary>
    /// <remarks>
    /// 已转换为站点配置的时区。
    /// </remarks>
    public DateTime MomentDate { get; init; }

    /// <summary>
    /// 当前访问者是否处于锁定状态
    /// </summary>
    /// <remarks>
    /// 为 <see langword="true"/> 时，表示该记录受保护且当前访问者尚未获得查看权限。
    /// </remarks>
    public bool IsLocked { get; init; }

    /// <summary>
    /// 点滴是否设置了访问密码
    /// </summary>
    /// <remarks>
    /// 与 <see cref="IsLocked"/> 不同，本属性描述记录本身是否受密码保护，
    /// 不受当前访问者是否已经解锁影响。
    /// </remarks>
    public bool IsProtected { get; init; }

    /// <summary>
    /// 是否允许访客发表评论
    /// </summary>
    public bool AllowComment { get; init; }

    /// <summary>
    /// 作者显示名称
    /// </summary>
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>
    /// 点滴发生在相恋后的第几天
    /// </summary>
    /// <remarks>
    /// 为 0 时表示无法计算，例如点滴时间早于相恋日期。
    /// </remarks>
    public int LoveDay { get; init; }

    /// <summary>
    /// 评论数量
    /// </summary>
    public int CommentCount { get; init; }

    /// <summary>
    /// 上一篇点滴
    /// </summary>
    public MomentLink? Previous { get; init; }

    /// <summary>
    /// 下一篇点滴
    /// </summary>
    public MomentLink? Next { get; init; }
}

/// <summary>
/// 点滴上一篇或下一篇的导航信息
/// </summary>
/// <param name="Title">点滴标题</param>
/// <param name="Slug">点滴的 URL 标识</param>
public record MomentLink(string Title, string Slug) {
    /// <summary>
    /// 点滴详情页地址
    /// </summary>
    public string Url => "/moments/" + Slug;
}
