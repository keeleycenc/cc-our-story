// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Web.Infrastructure;
using Xunit;

namespace OurStory.Tests;

/// <summary>纪念日分类展示信息测试。</summary>
public class AnniversaryKindsTests {
    /// <summary>后台选择器覆盖全部分类，且不产生重复值。</summary>
    [Fact]
    public void 分类选项覆盖全部枚举值() {
        var enumValues = Enum.GetValues<AnniversaryKind>();

        Assert.Equal(enumValues.Length, AnniversaryKinds.All.Count);
        Assert.Equal(enumValues.Order(), AnniversaryKinds.All.Select(option => option.Value).Order());
        Assert.Equal(AnniversaryKinds.All.Count, AnniversaryKinds.All.Select(option => option.Label).Distinct().Count());
    }

    /// <summary>旧分类的数值保持稳定，避免已有数据库记录改变含义。</summary>
    [Fact]
    public void 已有分类数值保持兼容() {
        Assert.Equal(0, (int)AnniversaryKind.Love);
        Assert.Equal(1, (int)AnniversaryKind.Birthday);
        Assert.Equal(2, (int)AnniversaryKind.Milestone);
        Assert.Equal(3, (int)AnniversaryKind.Custom);
        Assert.Equal(0, (int)AnniversaryCalendarType.Solar);
        Assert.Equal(1, (int)AnniversaryCalendarType.Lunar);
    }
}
