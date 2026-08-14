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
    /// 从查询串里取当前页码，取不到或不合法时当作第一页
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static int PageNumber(this HttpRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        return int.TryParse(request.Query[Key], out var page) && page > 1 ? page : 1;
    }
}
