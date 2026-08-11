// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Options;
using OurStory.Data;
using OurStory.Services.Accounts;
using OurStory.Services.Settings;
using System.Security.Cryptography;

namespace OurStory.Services;

/// <summary>首次启动时自动创建出来的账号，会打进启动日志，方便第一次登录</summary>
/// <param name="UserName">登录名</param>
/// <param name="Password">明文口令，只在创建的这一次出现</param>
/// <param name="Role">男主还是女主</param>
public record SeededAccount(string UserName, string Password, UserRole Role);

/// <summary>
/// 
/// </summary>
public interface IDatabaseInitializer {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<SeededAccount>> InitializeAsync(CancellationToken cancellationToken = default);
}

public class DatabaseInitializer(
    OurStoryDbContext db,
    ISettingsService settings,
    ActiveConfiguration configuration,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer {
    /// <summary>访客指纹用的盐，存在设置表里，重启后统计不会断</summary>
    public const string VisitorSecretKey = "system.visitorSecret";

    private readonly SiteOptions _options = configuration.Site;

    public async Task<IReadOnlyList<SeededAccount>> InitializeAsync(CancellationToken cancellationToken = default) {
        await db.Database.MigrateAsync(cancellationToken);

        await EnsureVisitorSecretAsync(cancellationToken);
        return await EnsureAccountsAsync(cancellationToken);
    }

    private async Task EnsureVisitorSecretAsync(CancellationToken cancellationToken) {
        if (!string.IsNullOrWhiteSpace(await settings.GetRawAsync(VisitorSecretKey, cancellationToken))) {
            return;
        }

        var secret = string.IsNullOrWhiteSpace(_options.VisitorSecret)
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()
            : _options.VisitorSecret;

        await settings.SetRawAsync(VisitorSecretKey, secret, cancellationToken);
    }

    private async Task<IReadOnlyList<SeededAccount>> EnsureAccountsAsync(CancellationToken cancellationToken) {
        if (await db.Users.AnyAsync(cancellationToken)) {
            return [];
        }

        var seeds = new List<SeededAccount>
        {
            new(
                Sanitize(_options.Seed.BoyUserName, "boy"),
                Choose(_options.Seed.BoyPassword),
                UserRole.Boy),
            new(
                Sanitize(_options.Seed.GirlUserName, "girl"),
                Choose(_options.Seed.GirlPassword),
                UserRole.Girl)
        };

        foreach (var seed in seeds) {
            _ = db.Users.Add(new User {
                UserName = seed.UserName,
                Role = seed.Role,
                PasswordHash = PasswordHasher.Hash(seed.Password),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        _ = await db.SaveChangesAsync(cancellationToken);
        if (logger.IsEnabled(LogLevel.Information)) {
            logger.LogInformation("已创建初始账号：{Accounts}", string.Join('、', seeds.Select(seed => seed.UserName)));
        }

        return [.. seeds.Where(seed => IsGenerated(seed.Role))];
    }

    private bool IsGenerated(UserRole role) => role switch {
        UserRole.Boy => string.IsNullOrWhiteSpace(_options.Seed.BoyPassword),
        UserRole.Girl => string.IsNullOrWhiteSpace(_options.Seed.GirlPassword),
        _ => false
    };

    private static string Choose(string configured) =>
        string.IsNullOrWhiteSpace(configured) ? PasswordHasher.GenerateReadablePassword() : configured;

    private static string Sanitize(string userName, string fallback) {
        var cleaned = new string([.. (userName ?? string.Empty).Where(char.IsAsciiLetterOrDigit)]);
        return cleaned.Length > 0 ? cleaned.ToLowerInvariant() : fallback;
    }
}
