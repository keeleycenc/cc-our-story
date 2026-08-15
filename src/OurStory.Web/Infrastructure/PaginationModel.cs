// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 首尾两页之间断开的位置要不要放省略号
/// </summary>
public enum PageGap {
    /// <summary>
    /// 页码是连着的，不用放
    /// </summary>
    None,

    /// <summary>
    /// 宽屏窄屏都断开了
    /// </summary>
    Always,

    /// <summary>
    /// 宽屏连着，窄屏收起外圈页码后才断开
    /// </summary>
    NarrowOnly
}

/// <summary>翻页条的参数</summary>
/// <param name="Page">当前页码，从 1 开始</param>
/// <param name="TotalPages">总页数</param>
/// <param name="BasePath">页码拼在这个地址后面，第一页不带 ?page=</param>
public record PaginationModel(int Page, int TotalPages, string BasePath) {
    /// <summary>
    /// 宽屏时当前页两侧各留几个页码
    /// </summary>
    private const int Window = 2;

    /// <summary>
    /// 窄屏放不下，收窄到两侧各留几个
    /// </summary>
    private const int NarrowWindow = 1;

    /// <summary>
    /// 获取是否还有上一页
    /// </summary>
    public bool HasPrevious => Page > 1;

    /// <summary>
    /// 获取是否还有下一页
    /// </summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>
    /// 获取首页和中间那段页码之间的断开情况
    /// </summary>
    public PageGap LeadingGap => GapOf(Low(Window) > 2, Low(NarrowWindow) > 2);

    /// <summary>
    /// 获取中间那段页码和尾页之间的断开情况
    /// </summary>
    public PageGap TrailingGap => GapOf(High(Window) < TotalPages - 1, High(NarrowWindow) < TotalPages - 1);

    /// <summary>
    /// 构建指定页码的地址
    /// </summary>
    /// <param name="page">页码</param>
    /// <returns>该页的地址</returns>
    public string UrlFor(int page) => page <= 1 ? BasePath : $"{BasePath}{(BasePath.Contains('?', StringComparison.Ordinal) ? '&' : '?')}page={page}";

    /// <summary>
    /// 获取当前页周围那一段页码，不含首尾两页
    /// </summary>
    /// <remarks>
    /// Far 标记的是外圈那两个，窄屏上由样式表收起来；
    /// 收起后新出现的缺口由 <see cref="PageGap.NarrowOnly"/> 的省略号补上。
    /// </remarks>
    /// <returns>页码，以及它是不是窄屏要收起的外圈</returns>
    public IEnumerable<(int Number, bool Far)> WindowPages() {
        for (var page = Low(Window); page <= High(Window); page++) {
            yield return (page, Math.Abs(page - Page) > NarrowWindow);
        }
    }

    private static PageGap GapOf(bool wide, bool narrow) =>
        wide ? PageGap.Always : narrow ? PageGap.NarrowOnly : PageGap.None;

    private int Low(int radius) => Math.Max(2, Page - radius);

    private int High(int radius) => Math.Min(TotalPages - 1, Page + radius);
}
