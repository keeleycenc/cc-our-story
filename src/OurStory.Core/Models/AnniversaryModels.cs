// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Text;

namespace OurStory.Core.Models;

/// <summary>
/// 纪念日在某一天看来所处的时间位置
/// </summary>
public sealed record AnniversaryOccurrence(
    int Id,
    string Title,
    string Note,
    string NoteHtml,
    string CoverUrl,
    AnniversaryKind Kind,
    DateOnly OriginalDate,
    DateOnly? NextDate,
    int? DaysUntil,
    int Years,
    bool RepeatYearly,
    bool IsPrivate,
    string AuthorName) {

    /// <summary>
    /// 获取纪念日是否恰好是今天
    /// </summary>
    public bool IsToday => DaysUntil == 0;

    /// <summary>
    /// 获取一次性纪念日是否已经成为历史
    /// </summary>
    public bool IsArchived => NextDate is null;

    /// <summary>
    /// 获取纪念日详情地址
    /// </summary>
    public string Url => $"/anniversaries/{Id}";

    /// <summary>
    /// 获取供固定高度列表使用的纯文本摘要
    /// </summary>
    public string Summary {
        get {
            var summary = HtmlText.Excerpt(NoteHtml, 96);
            return summary.Length > 0 ? summary : Note;
        }
    }
}
