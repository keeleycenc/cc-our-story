// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using OurStory.Core;
using OurStory.Services.Anniversaries;
using Xunit;
using AnniversaryEditPage = OurStory.Web.Areas.Admin.Pages.Anniversaries.EditModel;

namespace OurStory.Tests;

/// <summary>
/// 纪念日后台保存流程测试
/// </summary>
public class AnniversaryEditPageTests {
    /// <summary>创建完成后回到列表，并在列表页提示结果。</summary>
    [Fact]
    public async Task 新建保存后返回列表() {
        await using var db = TestDoubles.Database(nameof(新建保存后返回列表));
        var page = Page(Service(db));
        page.Input = Input("第一次见面");

        var result = await page.OnPostAsync(null, default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/admin/anniversaries", redirect.Url);
        Assert.Equal("纪念日已创建。", page.TempData["Flash"]);
    }

    /// <summary>编辑完成后同样回到列表，避免被误认为仍在新建。</summary>
    [Fact]
    public async Task 更新保存后返回列表() {
        await using var db = TestDoubles.Database(nameof(更新保存后返回列表));
        var service = Service(db);
        var created = await service.CreateAsync(new AnniversaryEditModel {
            Title = "旧标题",
            AnniversaryDate = new DateOnly(2025, 8, 14),
            Kind = AnniversaryKind.Love,
            RepeatYearly = true,
            IsPrivate = false
        }, null);
        var page = Page(service);
        page.Input = Input("新标题");

        var result = await page.OnPostAsync(created.Id, default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/admin/anniversaries", redirect.Url);
        Assert.Equal("纪念日已更新。", page.TempData["Flash"]);
    }

    private static AnniversaryEditPage Page(IAnniversaryService service) {
        var httpContext = new DefaultHttpContext {
            RequestServices = new ServiceCollection()
                .AddSingleton<ITempDataProvider, MemoryTempDataProvider>()
                .AddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>()
                .BuildServiceProvider()
        };
        var pageContext = new PageContext { HttpContext = httpContext };
        return new AnniversaryEditPage(service) { PageContext = pageContext };
    }

    private static AnniversaryService Service(OurStory.Data.OurStoryDbContext db) =>
        new(db, TestDoubles.Clock(), TestDoubles.Markdown(), new SettingsStub());

    private static AnniversaryEditPage.InputModel Input(string title) => new() {
        Title = title,
        AnniversaryDate = new DateOnly(2026, 8, 14),
        Note = "一起记住",
        Kind = AnniversaryKind.FirstMeeting,
        RepeatYearly = true,
        IsPrivate = false
    };

    private sealed class MemoryTempDataProvider : ITempDataProvider {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
