// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Web.Infrastructure;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 翻页条的取值规则
/// </summary>
public class PaginationModelTests {
    /// <summary>
    /// 第一页不带 ?page=，其余页都带上
    /// </summary>
    [Theory]
    [InlineData(1, "/moments")]
    [InlineData(2, "/moments?page=2")]
    [InlineData(99, "/moments?page=99")]
    public void UrlFor_FirstPageHasNoQuery(int page, string expected) =>
        Assert.Equal(expected, new PaginationModel(1, 99, "/moments").UrlFor(page));

    /// <summary>
    /// 同页多列表可以使用独立页码参数并在翻页后定位到对应区域
    /// </summary>
    [Theory]
    [InlineData(1, "/admin/affinity?page=3#answered-records")]
    [InlineData(2, "/admin/affinity?page=3&answeredPage=2#answered-records")]
    public void UrlFor_SupportsCustomKeyAndFragment(int page, string expected) =>
        Assert.Equal(expected, new PaginationModel(
            1,
            3,
            "/admin/affinity?page=3",
            "answeredPage",
            "answered-records").UrlFor(page));

    /// <summary>
    /// 中间那段页码不含首尾两页，宽屏时当前页两侧各留两个
    /// </summary>
    [Fact]
    public void WindowPages_SkipsFirstAndLast() {
        var pages = new PaginationModel(11, 99, "/moments").WindowPages().ToArray();

        Assert.Equal([9, 10, 11, 12, 13], pages.Select(item => item.Number));
    }

    /// <summary>
    /// 外圈那两个标成 Far，窄屏上由样式表收起来
    /// </summary>
    [Fact]
    public void WindowPages_MarksOuterRingAsFar() {
        var pages = new PaginationModel(11, 99, "/moments").WindowPages().ToArray();

        Assert.Equal([true, false, false, false, true], pages.Select(item => item.Far));
    }

    /// <summary>
    /// 贴着首尾两页时，中间那段不会越界
    /// </summary>
    [Theory]
    [InlineData(1, 99, new[] { 2, 3 })]
    [InlineData(99, 99, new[] { 97, 98 })]
    [InlineData(1, 2, new int[0])]
    [InlineData(2, 3, new[] { 2 })]
    public void WindowPages_StaysInsideRange(int page, int total, int[] expected) {
        var pages = new PaginationModel(page, total, "/moments").WindowPages().ToArray();

        Assert.Equal(expected, pages.Select(item => item.Number));
    }

    /// <summary>
    /// 页码断开的地方才放省略号
    /// </summary>
    [Theory]
    [InlineData(11, 99, PageGap.Always, PageGap.Always)]
    [InlineData(3, 99, PageGap.None, PageGap.Always)]
    [InlineData(97, 99, PageGap.Always, PageGap.None)]
    [InlineData(3, 5, PageGap.None, PageGap.None)]
    public void Gaps_OnlyWherePagesAreBroken(int page, int total, PageGap leading, PageGap trailing) {
        var model = new PaginationModel(page, total, "/moments");

        Assert.Equal(leading, model.LeadingGap);
        Assert.Equal(trailing, model.TrailingGap);
    }

    /// <summary>
    /// 宽屏连着、收起外圈后才断开的地方，省略号只在窄屏上出现
    /// </summary>
    /// <remarks>
    /// 共 7 页停在第 4 页：宽屏是 1 2 3 4 5 6 7 一路连着，
    /// 窄屏收掉 2 和 6 之后两头都缺了一格，得补上省略号。
    /// </remarks>
    [Fact]
    public void Gaps_AppearOnNarrowScreensOnly() {
        var model = new PaginationModel(4, 7, "/moments");

        Assert.Equal(PageGap.NarrowOnly, model.LeadingGap);
        Assert.Equal(PageGap.NarrowOnly, model.TrailingGap);
    }

    /// <summary>
    /// 首尾两页上不再给「上一页 / 下一页」
    /// </summary>
    [Fact]
    public void HasPreviousAndNext_StopAtBothEnds() {
        Assert.False(new PaginationModel(1, 9, "/moments").HasPrevious);
        Assert.True(new PaginationModel(1, 9, "/moments").HasNext);
        Assert.True(new PaginationModel(9, 9, "/moments").HasPrevious);
        Assert.False(new PaginationModel(9, 9, "/moments").HasNext);
    }
}
