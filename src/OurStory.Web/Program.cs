// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
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

// 维护命令与 Web 应用共用配置和依赖注入，执行完成后直接退出，不启动端口监听。
var maintenance = MaintenanceCommand.Parse(args);

var builder = WebApplication.CreateBuilder(MaintenanceCommand.StripFrom(args));

// 站点配置统一读取数据目录下的 ourstory.json。
var store = ConfigurationStore.Create(builder.Environment.ContentRootPath);
var loaded = store.Load();

// 日志级别使用代码中的默认配置
builder.Logging.AddFilter(
    "Microsoft.AspNetCore",
    builder.Environment.IsDevelopment() ? LogLevel.Information : LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

// --lan：开发时允许同一局域网内的设备访问站点。
var lanUrl = LanBinding.IsRequested(args)
    ? LanBinding.Resolve(args, builder.Configuration["urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
    : null;

if (lanUrl is not null) {
    _ = builder.WebHost.UseUrls(lanUrl);
}

builder.Services.AddOurStory(store, loaded.Configuration, store.DataDirectory);

// 持久化数据保护密钥，确保容器重启后登录状态与解锁记录保持有效。
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
    // 后台区域统一要求登录；登录页位于前台区域，不受该规则影响。
    _ = options.Conventions.AuthorizeAreaFolder("Admin", "/");
});

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

// 允许 Razor 直接输出 Unicode 字符，避免中文被转换为字符实体。
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddSingleton<AssetVersionProvider>();
builder.Services.AddSingleton<DailyVisitLedger>();
builder.Services.AddSingleton<MediaUrls>();
builder.Services.AddSingleton<ArticleMedia>();
builder.Services.AddScoped<HeartbeatTokenService>();
builder.Services.AddScoped<VisitorIdentityAccessor>();
builder.Services.AddScoped<MomentUnlockStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddHostedService<NotificationScheduler>();
builder.Services.AddHostedService<CycleReminderScheduler>();
builder.Services.AddHostedService<AnniversaryRewardScheduler>();
builder.Services.AddHostedService<LlmAtmosphereWorker>();
builder.Services.AddHostedService<LlmAtmosphereSweeper>();
builder.Services.AddHostedService<CycleInsightWorker>();

builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // 反向代理通常与应用位于同一主机，且网段不可预知，因此不限制代理来源。
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

// 显式注册 .webmanifest MIME 类型，避免静态文件中间件返回 404。
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

// 附件存放在数据目录中，使应用发布覆盖不会影响用户数据。
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
app.MapMediaEndpoints();
app.MapPushEndpoints();
app.MapAtmosphereEndpoints();
app.MapCycleInsightEndpoints();

// 存活探针供容器与反向代理判断站点状态，仅检查数据库连接，
// 不查询业务数据或渲染页面。
app.MapGet("/healthz", async (OurStoryDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Text("ok")
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

await app.InitializeDatabaseAsync();
app.EnsurePushKeys();

if (lanUrl is not null) {
    var port = LanBinding.PortOf(lanUrl);
    foreach (var address in LanBinding.LocalAddresses(port)) {
        app.Logger.LogInformation("移动设备请访问 {Address}", address);
    }
}

await app.RunAsync();
return 0;
