// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.WebEncoders;
using OurStory.Core.Configuration;
using OurStory.Data;
using OurStory.Services;
using OurStory.Services.Storage;
using OurStory.Web.Api;
using OurStory.Web.Infrastructure;
using System.Text.Encodings.Web;
using System.Text.Unicode;

// --reset-password / --list-accounts 这类维护命令走的是同一套配置和依赖注入，
// 只是执行完就退出，不会去监听端口
var maintenance = MaintenanceCommand.Parse(args);

var builder = WebApplication.CreateBuilder(MaintenanceCommand.StripFrom(args));

// 站点配置只有一处来源：数据目录下的 ourstory.json
var store = ConfigurationStore.Create(builder.Environment.ContentRootPath);
var loaded = store.Load();

// 日志级别也是默认值，跟着代码走
builder.Logging.AddFilter(
    "Microsoft.AspNetCore",
    builder.Environment.IsDevelopment() ? LogLevel.Information : LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

builder.Services.AddOurStory(store, loaded.Configuration, store.DataDirectory);

// 密钥落盘，容器重启后登录状态和已解锁的记录都还在
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(store.DataDirectory, "keys")))
    .SetApplicationName("CC.OurStory");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.Cookie.Name = "ourstory.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddRazorPages(options => {
    // 后台整个区域都要登录，登录页本身在前台，不受影响
    _ = options.Conventions.AuthorizeAreaFolder("Admin", "/");
});

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

// Razor 默认把非拉丁字符转义成 &#x4E2D; 这种实体，整站中文会让页面凭空大一圈，
// 查看源代码时也完全读不了。放行全部 Unicode，输出原样的 UTF-8。
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddSingleton<AssetVersionProvider>();
builder.Services.AddSingleton<DailyVisitLedger>();
builder.Services.AddSingleton<CoverThumbnails>();
builder.Services.AddScoped<HeartbeatTokenService>();
builder.Services.AddScoped<VisitorIdentityAccessor>();
builder.Services.AddScoped<MomentUnlockStore>();
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // 反代通常就在同一台机器上，网段未知，所以不限制来源
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.LogConfigurationSource(loaded);

if (maintenance is not null) {
    return await app.ExecuteAsync(maintenance);
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment()) {
    _ = app.UseExceptionHandler("/error/500");
    _ = app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/error/{0}");

app.UseStaticFiles();

// 附件放在数据目录里而不是 wwwroot 里：发布包可以整个覆盖，数据不会被冲掉
var uploadsRoot = app.Services.GetRequiredService<StoragePaths>().UploadsRoot;
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads",
    ServeUnknownFileTypes = false
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseDailyVisitReward();
app.MapRazorPages();
app.MapHeartbeatEndpoints();
app.MapThumbnailEndpoints();

// 存活探针：容器和反代拿它判断站点还活着。只探数据库连不连得上，
// 不查任何业务数据，也不渲染页面
app.MapGet("/healthz", async (OurStoryDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Text("ok")
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

await app.InitializeDatabaseAsync();

await app.RunAsync();
return 0;
