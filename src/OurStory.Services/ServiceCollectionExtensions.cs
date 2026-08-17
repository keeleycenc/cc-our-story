// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OurStory.Core.Abstractions;
using OurStory.Core.Configuration;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Accounts;
using OurStory.Services.Anniversaries;
using OurStory.Services.Comments;
using OurStory.Services.HeartPoints;
using OurStory.Services.Heartbeats;
using OurStory.Services.Moments;
using OurStory.Services.Notifications;
using OurStory.Services.Settings;
using OurStory.Services.Shop;
using OurStory.Services.Storage;

namespace OurStory.Services;

/// <summary>
/// 表示 ServiceCollectionExtensions
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// 把数据层和业务层一次性接进容器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="store">配置文件的读写入口，后台保存设置时也走它</param>
    /// <param name="configuration">已经读好的站点配置，默认值来自各个 Options 类自身</param>
    /// <param name="dataDirectory">数据目录的绝对路径，数据库、附件和密钥都落在这里</param>
    public static IServiceCollection AddOurStory(
        this IServiceCollection services,
        ConfigurationStore store,
        OurStoryConfiguration configuration,
        string dataDirectory) {
        // 各处用配置都从 ActiveConfiguration 现取，不在构造时缓存：
        // 后台一保存就换成新的那份，不用重启站点
        _ = services.AddSingleton(store);
        _ = services.AddSingleton(new ActiveConfiguration(store, configuration));

        var uploadsRoot = Path.Combine(dataDirectory, "uploads");
        var thumbnailsRoot = Path.Combine(dataDirectory, "thumbs");

        _ = Directory.CreateDirectory(dataDirectory);
        _ = Directory.CreateDirectory(uploadsRoot);
        _ = Directory.CreateDirectory(thumbnailsRoot);

        _ = services.AddSingleton(new StoragePaths(uploadsRoot, thumbnailsRoot, "/uploads"));
        _ = services.AddSingleton<SiteClock>();

        var databasePath = Path.Combine(dataDirectory, configuration.Site.DatabaseFileName);
        _ = services.AddDbContext<OurStoryDbContext>(builder => builder.UseSqlite($"Data Source={databasePath}"));

        _ = services.AddMemoryCache();
        _ = services.AddHttpClient(AliyunOssFileStorage.HttpClientName, client => {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        _ = services.AddHttpClient(WebPushSender.HttpClientName, client => {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // 两个驱动都建好，用哪个由 FileStorageRouter 按当前配置临时决定
        _ = services.AddSingleton<LocalFileStorage>();
        _ = services.AddSingleton<AliyunOssFileStorage>();
        _ = services.AddSingleton<IFileStorage, FileStorageRouter>();

        _ = services.AddScoped<ISettingsService, SettingsService>();
        _ = services.AddScoped<IUserService, UserService>();
        _ = services.AddScoped<IAnniversaryService, AnniversaryService>();
        _ = services.AddScoped<IMomentService, MomentService>();
        _ = services.AddScoped<ICommentService, CommentService>();
        _ = services.AddScoped<IHeartbeatService, HeartbeatService>();
        _ = services.AddScoped<IHeartPointService, HeartPointService>();
        _ = services.AddScoped<IShopService, ShopService>();

        _ = services.AddSingleton<INotificationQueue, NotificationQueue>();
        _ = services.AddScoped<IWebPushSender, WebPushSender>();
        _ = services.AddScoped<INotificationService, NotificationService>();

        _ = services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        _ = services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        _ = services.AddScoped<IAttachmentService, AttachmentService>();
        _ = services.AddSingleton<IThumbnailService, ThumbnailService>();

        return services;
    }
}
