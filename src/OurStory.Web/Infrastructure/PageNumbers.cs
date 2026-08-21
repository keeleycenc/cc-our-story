// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 翻页参数的读取
/// </summary>
public static class PageNumbers {
    /// <summary>
    /// 翻页参数在地址里的名字
    /// </summary>
    public const string Key = "page";

    /// <summary>
    /// 后台各个列表每页的条数
    /// </summary>
    public const int AdminPageSize = 20;

    /// <summary>
    /// 心有灵犀前台每页展示的共同作答记录数
    /// </summary>
    public const int AffinityHistoryPageSize = 8;

    /// <summary>
    /// 从查询串里取当前页码，取不到或不合法时当作第一页
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static int PageNumber(this HttpRequest request) => request.PageNumber(Key);

    /// <summary>
    /// 从查询串的指定参数中读取页码
    /// </summary>
    /// <param name="request">当前请求</param>
    /// <param name="key">查询参数名</param>
    /// <returns>有效页码，不合法时返回第一页</returns>
    public static int PageNumber(this HttpRequest request, string key) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return int.TryParse(request.Query[key], out var page) && page > 1 ? page : 1;
    }
}
