// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;

namespace OurStory.Web.Infrastructure;

/// <summary>纪念日分类在前后台共用的展示信息。</summary>
public static class AnniversaryKinds {
    /// <summary>获取所有可选分类。</summary>
    public static IReadOnlyList<AnniversaryKindOption> All { get; } = [
        new(AnniversaryKind.Love, "恋爱纪念", "heart"),
        new(AnniversaryKind.Birthday, "生日", "gift"),
        new(AnniversaryKind.FirstMeeting, "初次相遇", "users-round"),
        new(AnniversaryKind.Wedding, "结婚纪念", "calendar-heart"),
        new(AnniversaryKind.Travel, "共同旅行", "map-pin"),
        new(AnniversaryKind.Festival, "节日", "sparkles"),
        new(AnniversaryKind.Promise, "约定", "heart"),
        new(AnniversaryKind.Family, "家庭", "house"),
        new(AnniversaryKind.Milestone, "里程碑", "sparkles"),
        new(AnniversaryKind.Custom, "特别日子", "calendar-heart")
    ];

    /// <summary>获取分类名称。</summary>
    public static string Name(AnniversaryKind kind) => Find(kind).Label;

    /// <summary>获取分类图标。</summary>
    public static string Icon(AnniversaryKind kind) => Find(kind).Icon;

    private static AnniversaryKindOption Find(AnniversaryKind kind) =>
        All.FirstOrDefault(option => option.Value == kind)
        ?? new AnniversaryKindOption(kind, "特别日子", "calendar-heart");
}

/// <summary>纪念日分类选择项。</summary>
public sealed record AnniversaryKindOption(AnniversaryKind Value, string Label, string Icon);
