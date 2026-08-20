// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Options;
using OurStory.Data;
using OurStory.Services.Accounts;
using OurStory.Services.Affinity;
using OurStory.Services.HeartPoints;
using OurStory.Services.Settings;
using System.Globalization;
using System.Security.Cryptography;

namespace OurStory.Services;

/// <summary>首次启动时自动创建出来的账号，会打进启动日志，方便第一次登录</summary>
/// <param name="UserName">登录名</param>
/// <param name="Password">明文口令，只在创建的这一次出现</param>
/// <param name="Role">男主还是女主</param>
public record SeededAccount(string UserName, string Password, UserRole Role);

/// <summary>
/// 获取数据库初始化服务接口
/// </summary>
public interface IDatabaseInitializer {
    /// <summary>
    /// 初始化数据库数据
    /// </summary>
    /// <param name="cancellationToken">获取取消令牌</param>
    /// <returns>获取已初始化的账户列表</returns>
    Task<IReadOnlyList<SeededAccount>> InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 获取数据库初始化服务
/// </summary>
/// <param name="db">获取数据库上下文</param>
/// <param name="settings">获取设置服务</param>
/// <param name="heartPoints">获取心点服务</param>
/// <param name="configuration">获取活动配置</param>
/// <param name="logger">获取日志记录器</param>
public class DatabaseInitializer(
    OurStoryDbContext db,
    ISettingsService settings,
    IHeartPointService heartPoints,
    ActiveConfiguration configuration,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer {
    private readonly SiteOptions _options = configuration.Site;

    /// <summary>
    /// 获取访客指纹使用的盐配置键，存储在设置表中，重启后统计不会中断
    /// </summary>
    public const string VisitorSecretKey = "system.visitorSecret";

    /// <summary>
    /// 获取商店预设初始化时间配置键，有值后不会重复初始化
    /// </summary>
    public const string ShopPresetsSeededKey = "shop.presetsSeededAt";

    /// <summary>
    /// 获取心有灵犀预设题库已导入版本配置键
    /// </summary>
    public const string AffinityQuestionsVersionKey = "affinity.questionsPresetVersion";

    /// <summary>
    /// 初始化数据库数据
    /// </summary>
    /// <param name="cancellationToken">获取取消令牌</param>
    /// <returns>获取已初始化的账户列表</returns>
    public async Task<IReadOnlyList<SeededAccount>> InitializeAsync(CancellationToken cancellationToken = default) {
        await db.Database.MigrateAsync(cancellationToken);

        await EnsureVisitorSecretAsync(cancellationToken);
        var seeded = await EnsureAccountsAsync(cancellationToken);

        await EnsureShopPresetsAsync(cancellationToken);
        await EnsureAffinityQuestionsAsync(cancellationToken);
        await EnsureHeartPointsAsync(cancellationToken);

        return seeded;
    }

    #region 私有方法

    private async Task EnsureAffinityQuestionsAsync(CancellationToken cancellationToken) {
        var rawVersion = await settings.GetRawAsync(AffinityQuestionsVersionKey, cancellationToken);
        var appliedVersion = int.TryParse(rawVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedVersion)
            ? Math.Max(0, parsedVersion)
            : 0;

        if (appliedVersion >= DefaultAffinityQuestions.CurrentVersion) {
            return;
        }

        var candidates = DefaultAffinityQuestions.All
            .Where(seed => seed.IntroducedInVersion > appliedVersion &&
                seed.IntroducedInVersion <= DefaultAffinityQuestions.CurrentVersion)
            .ToList();
        var candidateTexts = candidates.Select(seed => seed.Text).ToList();
        var existingTexts = candidateTexts.Count == 0
            ? []
            : await db.AffinityQuestions
                .Where(question => candidateTexts.Contains(question.Text))
                .Select(question => question.Text)
                .ToHashSetAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var addedCount = 0;
        foreach (var seed in candidates.Where(seed => !existingTexts.Contains(seed.Text))) {
            _ = db.AffinityQuestions.Add(new AffinityQuestion {
                Text = seed.Text,
                Category = seed.Category,
                Type = AffinityQuestionType.SingleChoice,
                RewardPoints = 5,
                IsActive = true,
                IsSealed = true,
                CreatedAt = now,
                UpdatedAt = now,
                Options = [.. seed.Options.Select((text, index) => new AffinityQuestionOption {
                    Text = text,
                    SortOrder = index
                })]
            });
            addedCount++;
        }

        if (addedCount > 0) {
            _ = await db.SaveChangesAsync(cancellationToken);
        }

        await settings.SetRawAsync(
            AffinityQuestionsVersionKey,
            DefaultAffinityQuestions.CurrentVersion.ToString(CultureInfo.InvariantCulture),
            cancellationToken);

        if (addedCount > 0 && logger.IsEnabled(LogLevel.Information)) {
            logger.LogInformation(
                "心有灵犀预设题库已升级至版本 {Version}，新增 {Count} 道题目。",
                DefaultAffinityQuestions.CurrentVersion,
                addedCount);
        }
    }

    private async Task EnsureShopPresetsAsync(CancellationToken cancellationToken) {
        if (!string.IsNullOrWhiteSpace(await settings.GetRawAsync(ShopPresetsSeededKey, cancellationToken))) {
            return;
        }

        await settings.SetRawAsync(
            ShopPresetsSeededKey,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            cancellationToken);

        if (await db.ShopPresets.AnyAsync(cancellationToken)) {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var order = 0;

        foreach (var seed in DefaultShopPresets.All) {
            _ = db.ShopPresets.Add(new ShopPreset {
                Title = seed.Title,
                Description = seed.Description,
                RedeemMode = seed.RedeemMode,
                SortOrder = order += 10,
                IsActive = true,
                CreatedAt = now
            });
        }

        _ = await db.SaveChangesAsync(cancellationToken);
        if (logger.IsEnabled(LogLevel.Information)) {
            logger.LogInformation("已初始化 {Count} 个默认心愿预设，可在后台编辑或删除。", DefaultShopPresets.All.Count);
        }
    }

    private async Task EnsureHeartPointsAsync(CancellationToken cancellationToken) {
        var result = await heartPoints.BackfillAsync(cancellationToken);
        if (result.AlreadyDone || result.Entries == 0) {
            return;
        }

        if (logger.IsEnabled(LogLevel.Information)) {
            logger.LogInformation(
                "已按历史记录补充记初始心意：{Entries} 条，合计 {Total}。",
                result.Entries,
                result.Total);
        }
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

    #endregion
}
