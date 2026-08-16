// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Services.Anniversaries;
using Xunit;

namespace OurStory.Tests;

/// <summary>纪念日服务测试</summary>
public class AnniversaryServiceTests {
    [Fact]
    public async Task 前台只返回可见纪念日并按下一次日期排序() {
        await using var db = TestDoubles.Database(nameof(前台只返回可见纪念日并按下一次日期排序));
        db.Anniversaries.AddRange(
            Item("较晚", new DateOnly(2099, 12, 31), false),
            Item("较早", new DateOnly(2099, 1, 1), false),
            Item("私密", new DateOnly(2099, 2, 1), true));
        _ = await db.SaveChangesAsync();
        var service = Service(db);

        var items = await service.GetForViewerAsync(false);

        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, item => item.Title == "私密");
        Assert.True(items[0].DaysUntil <= items[1].DaysUntil);
        Assert.Equal(3, (await service.GetForViewerAsync(true)).Count);
    }

    [Fact]
    public async Task 创建纪念日会整理输入() {
        await using var db = TestDoubles.Database(nameof(创建纪念日会整理输入));
        var service = Service(db);

        var created = await service.CreateAsync(new AnniversaryEditModel {
            Title = "  第一次旅行  ",
            AnniversaryDate = new DateOnly(2026, 7, 1),
            Note = "  看到了海  ",
            Kind = AnniversaryKind.Milestone,
            RepeatYearly = false,
            IsPrivate = false
        }, null);

        Assert.Equal("第一次旅行", created.Title);
        Assert.Equal("看到了海", created.Note);
        Assert.False(created.RepeatYearly);
        Assert.Equal(AnniversaryCalendarType.Solar, created.CalendarType);
        Assert.Equal(1, await db.Anniversaries.CountAsync());
    }

    [Fact]
    public async Task 创建农历纪念日会保留历法类型() {
        await using var db = TestDoubles.Database(nameof(创建农历纪念日会保留历法类型));
        var service = Service(db);
        var solarDate = Core.Time.ChineseLunarCalendar.ToSolar(new Core.Time.ChineseLunarDate(2026, 7, 7));

        var created = await service.CreateAsync(new AnniversaryEditModel {
            Title = "七夕",
            AnniversaryDate = solarDate,
            CalendarType = AnniversaryCalendarType.Lunar,
            RepeatYearly = true
        }, null);

        Assert.Equal(AnniversaryCalendarType.Lunar, created.CalendarType);
        Assert.Equal(solarDate, created.AnniversaryDate);
        Assert.Equal("农历七月初七", (await service.GetOccurrenceAsync(created.Id, true))!.LunarDate.ShortText);
    }

    [Fact]
    public async Task 创建纪念日会渲染正文并提取封面() {
        await using var db = TestDoubles.Database(nameof(创建纪念日会渲染正文并提取封面));
        var service = Service(db);

        var created = await service.CreateAsync(new AnniversaryEditModel {
            Title = "海边日落",
            AnniversaryDate = new DateOnly(2026, 8, 14),
            Note = "## 那一天\n\n![海边](/uploads/sunset.jpg)",
            RepeatYearly = true,
            IsPrivate = false
        }, null);

        Assert.Contains("<h2", created.NoteHtml, StringComparison.Ordinal);
        Assert.Contains("<img", created.NoteHtml, StringComparison.Ordinal);
        Assert.Equal("/uploads/sunset.jpg", created.CoverUrl);
    }

    [Fact]
    public async Task 私密纪念日只对情侣双方开放() {
        await using var db = TestDoubles.Database(nameof(私密纪念日只对情侣双方开放));
        var privateItem = Item("只有我们", new DateOnly(2026, 8, 14), true);
        _ = db.Anniversaries.Add(privateItem);
        _ = await db.SaveChangesAsync();
        var service = Service(db);

        Assert.Null(await service.GetOccurrenceAsync(privateItem.Id, false));
        Assert.NotNull(await service.GetOccurrenceAsync(privateItem.Id, true));
    }

    [Fact]
    public async Task 纪念日显示实际记录人() {
        await using var db = TestDoubles.Database(nameof(纪念日显示实际记录人));
        var author = new User { UserName = "boy", Role = UserRole.Boy, PasswordHash = "test" };
        _ = db.Users.Add(author);
        _ = await db.SaveChangesAsync();
        var service = Service(db);

        var created = await service.CreateAsync(new AnniversaryEditModel {
            Title = "一起看海",
            AnniversaryDate = new DateOnly(2026, 8, 14),
            RepeatYearly = true
        }, author.Id);
        var occurrence = await service.GetOccurrenceAsync(created.Id, true);

        Assert.Equal(author.Id, created.AuthorId);
        Assert.Equal("男主", occurrence!.AuthorName);
    }

    private static Anniversary Item(string title, DateOnly date, bool isPrivate) => new() {
        Title = title,
        AnniversaryDate = date,
        RepeatYearly = true,
        IsPrivate = isPrivate
    };

    private static AnniversaryService Service(OurStory.Data.OurStoryDbContext db) =>
        new(db, TestDoubles.Clock(), TestDoubles.Markdown(), TestDoubles.NoPoints(), new SettingsStub());
}
