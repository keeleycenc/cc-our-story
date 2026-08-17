// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using OurStory.Core;
using OurStory.Services.Moments;
using MomentEditPage = OurStory.Web.Areas.Admin.Pages.Moments.EditModel;
using Xunit;

namespace OurStory.Tests;

/// <summary>点点滴滴后台保存流程测试。</summary>
public class MomentEditPageTests {
    /// <summary>创建成功后返回列表，而不是停留在编辑页面。</summary>
    [Fact]
    public async Task 新建保存后返回点点滴滴列表() {
        await using var db = TestDoubles.Database(nameof(新建保存后返回点点滴滴列表));
        var service = Service(db);
        var page = Page(service);
        page.Input = Input("新的记录");

        var result = await page.OnPostAsync(null, default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/admin/moments", redirect.Url);
        Assert.Equal("记录已经保存好了，快去看看吧。", page.TempData["Flash"]);
    }

    /// <summary>更新成功后也返回列表，避免误以为下一次保存是新建。</summary>
    [Fact]
    public async Task 更新保存后返回点点滴滴列表() {
        await using var db = TestDoubles.Database(nameof(更新保存后返回点点滴滴列表));
        var service = Service(db);
        var created = await service.CreateAsync(new MomentEditModel {
            Title = "旧标题",
            Content = "旧正文",
            MomentDate = new DateTime(2026, 8, 14),
            Status = MomentStatus.Published
        }, 0);
        var page = Page(service);
        page.Input = Input("新标题");

        var result = await page.OnPostAsync(created.Id, default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/admin/moments", redirect.Url);
        Assert.Equal("改动已经保存。", page.TempData["Flash"]);
    }

    private static MomentService Service(Data.OurStoryDbContext db) =>
        new(db, new SettingsStub(), new MarkdownRenderer(), TestDoubles.NoPoints(), TestDoubles.Notifications(), TestDoubles.Clock());

    private static MomentEditPage Page(IMomentService service) {
        var httpContext = new DefaultHttpContext {
            RequestServices = new ServiceCollection()
                .AddSingleton<ITempDataProvider, MemoryTempDataProvider>()
                .AddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>()
                .BuildServiceProvider()
        };
        return new MomentEditPage(service, TestDoubles.Clock()) {
            PageContext = new PageContext { HttpContext = httpContext }
        };
    }

    private static MomentEditPage.InputModel Input(string title) => new() {
        Title = title,
        Content = "**值得记住**",
        MomentDate = new DateTime(2026, 8, 14),
        Status = MomentStatus.Published,
        AllowComment = true
    };

    private sealed class MemoryTempDataProvider : ITempDataProvider {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
