// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Web.Infrastructure;

/// <summary>翻页条的参数</summary>
/// <param name="Page">当前页码，从 1 开始</param>
/// <param name="TotalPages">总页数</param>
/// <param name="BasePath">页码拼在这个地址后面，第一页不带 ?page=</param>
public record PaginationModel(int Page, int TotalPages, string BasePath) {
    /// <summary>
    /// 页数很多时只显示当前页附近的一小段
    /// </summary>
    private const int Window = 2;

    /// <summary>
    /// 构建地址For
    /// </summary>
    public string UrlFor(int page) => page <= 1 ? BasePath : $"{BasePath}?page={page}";

    /// <summary>
    /// 获取可见页码
    /// </summary>
    public IEnumerable<int> VisiblePages() {
        var first = Math.Max(1, Page - Window);
        var last = Math.Min(TotalPages, Page + Window);

        for (var page = first; page <= last; page++) {
            yield return page;
        }
    }
}
