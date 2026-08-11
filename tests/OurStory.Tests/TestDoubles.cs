// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core.Configuration;
using OurStory.Core.Models;
using OurStory.Core.Options;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Settings;

namespace OurStory.Tests;

/// <summary>
/// 需要数据库的服务测试共用的几个替身
/// </summary>
internal static class TestDoubles {
    /// <summary>一个只属于这次测试的内存库。</summary>
    public static OurStoryDbContext Database(string name) =>
        new(new DbContextOptionsBuilder<OurStoryDbContext>()
            .UseInMemoryDatabase(name + "-" + Guid.NewGuid().ToString("n"))
            .Options);

    /// <summary>没配时区，等于 UTC，断言时间时不用跟着机器跑。</summary>
    public static SiteClock Clock() =>
        new(new ActiveConfiguration(new ConfigurationStore("."), new OurStoryConfiguration()));
}

/// <summary>不碰设置表，站点配置固定给一份。</summary>
internal sealed class SettingsStub(SiteSettings? settings = null) : ISettingsService {
    private readonly SiteSettings _settings = settings ?? new SiteSettings { BoyName = "男主", GirlName = "女主" };

    public Task<SiteSettings> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings);

    public Task SaveAsync(SiteSettings settings, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task SetRawAsync(string key, string value, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
