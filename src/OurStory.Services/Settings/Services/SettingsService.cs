// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Data;
using System.Globalization;
using System.Text.Json;

namespace OurStory.Services.Settings;

internal class SettingsService(OurStoryDbContext db, IMemoryCache cache) : ISettingsService {
    private const string CacheKey = "ourstory.settings";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<SiteSettings> GetAsync(CancellationToken cancellationToken = default) {
        if (cache.TryGetValue(CacheKey, out SiteSettings? cached) && cached is not null) {
            return cached;
        }

        var raw = await ReadAllAsync(cancellationToken);
        var settings = Materialize(raw);
        _ = cache.Set(CacheKey, settings, CacheDuration);
        return settings;
    }

    public async Task SaveAsync(SiteSettings settings, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(settings);

        var values = new Dictionary<string, string>(StringComparer.Ordinal) {
            [SettingKeys.SiteTitle] = settings.SiteTitle,
            [SettingKeys.SiteDescription] = settings.SiteDescription,
            [SettingKeys.BoyName] = settings.BoyName,
            [SettingKeys.GirlName] = settings.GirlName,
            [SettingKeys.BoyAvatar] = settings.BoyAvatar,
            [SettingKeys.GirlAvatar] = settings.GirlAvatar,
            [SettingKeys.BoySentence] = settings.BoySentence,
            [SettingKeys.GirlSentence] = settings.GirlSentence,
            [SettingKeys.LoveStartedAt] = settings.LoveStartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            [SettingKeys.HomeSentence] = settings.HomeSentence,
            [SettingKeys.DailyNote] = settings.DailyNote,
            [SettingKeys.LoveLetters] = JsonSerializer.Serialize(settings.LoveLetters),
            [SettingKeys.ColorMode] = settings.ColorMode,
            [SettingKeys.MomentsPageSize] = settings.MomentsPageSize.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.CommentsRequireMail] = settings.CommentsRequireMail ? "1" : "0",
            [SettingKeys.AllowGuestComments] = settings.AllowGuestComments ? "1" : "0",
            [SettingKeys.HeartbeatDailyLimit] = settings.HeartbeatDailyLimit.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.RewardHeartbeat] = settings.RewardHeartbeat.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.RewardMoment] = settings.RewardMoment.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.RewardAnniversary] = settings.RewardAnniversary.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.ShopPriceMin] = settings.ShopPriceMin.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.ShopPriceMax] = settings.ShopPriceMax.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.ShopListingDays] = settings.ShopListingDays.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.ShopValidDays] = settings.ShopValidDays.ToString(CultureInfo.InvariantCulture)
        };

        foreach (var (key, value) in values) {
            await UpsertAsync(key, value ?? string.Empty, cancellationToken);
        }

        _ = await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    public async Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) {
        var entry = await db.Settings.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == key, cancellationToken);
        return entry?.Value;
    }

    public async Task SetRawAsync(string key, string value, CancellationToken cancellationToken = default) {
        await UpsertAsync(key, value, cancellationToken);
        _ = await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    #region 私有方法

    private async Task UpsertAsync(string key, string value, CancellationToken cancellationToken) {
        var entry = await db.Settings.FirstOrDefaultAsync(item => item.Key == key, cancellationToken);
        if (entry is null) {
            _ = db.Settings.Add(new SettingEntry { Key = key, Value = value, UpdatedAt = DateTimeOffset.UtcNow });
            return;
        }

        entry.Value = value;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task<Dictionary<string, string>> ReadAllAsync(CancellationToken cancellationToken) {
        var entries = await db.Settings.AsNoTracking().ToListAsync(cancellationToken);
        return entries.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private static SiteSettings Materialize(Dictionary<string, string> raw) {
        var settings = new SiteSettings();

        settings.SiteTitle = Text(raw, SettingKeys.SiteTitle, settings.SiteTitle);
        settings.SiteDescription = Text(raw, SettingKeys.SiteDescription, settings.SiteDescription);
        settings.BoyName = Text(raw, SettingKeys.BoyName, settings.BoyName);
        settings.GirlName = Text(raw, SettingKeys.GirlName, settings.GirlName);
        settings.BoyAvatar = Text(raw, SettingKeys.BoyAvatar, settings.BoyAvatar);
        settings.GirlAvatar = Text(raw, SettingKeys.GirlAvatar, settings.GirlAvatar);
        settings.BoySentence = Text(raw, SettingKeys.BoySentence, settings.BoySentence);
        settings.GirlSentence = Text(raw, SettingKeys.GirlSentence, settings.GirlSentence);
        settings.HomeSentence = Text(raw, SettingKeys.HomeSentence, settings.HomeSentence);
        settings.DailyNote = Text(raw, SettingKeys.DailyNote, settings.DailyNote);
        settings.ColorMode = Text(raw, SettingKeys.ColorMode, settings.ColorMode) switch {
            "light" => "light",
            "dark" => "dark",
            _ => "auto"
        };

        if (raw.TryGetValue(SettingKeys.LoveStartedAt, out var startedAt)
            && DateTime.TryParse(startedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart)) {
            settings.LoveStartedAt = DateTime.SpecifyKind(parsedStart, DateTimeKind.Unspecified);
        }

        settings.LoveLetters = ParseLoveLetters(raw);
        settings.MomentsPageSize = Math.Clamp(Number(raw, SettingKeys.MomentsPageSize, settings.MomentsPageSize), 1, 100);
        settings.HeartbeatDailyLimit = Math.Clamp(Number(raw, SettingKeys.HeartbeatDailyLimit, settings.HeartbeatDailyLimit), 1, 9999);
        settings.CommentsRequireMail = Flag(raw, SettingKeys.CommentsRequireMail, settings.CommentsRequireMail);
        settings.AllowGuestComments = Flag(raw, SettingKeys.AllowGuestComments, settings.AllowGuestComments);

        settings.RewardHeartbeat = Math.Clamp(Number(raw, SettingKeys.RewardHeartbeat, settings.RewardHeartbeat), 0, 100);
        settings.RewardMoment = Math.Clamp(Number(raw, SettingKeys.RewardMoment, settings.RewardMoment), 0, 100);
        settings.RewardAnniversary = Math.Clamp(Number(raw, SettingKeys.RewardAnniversary, settings.RewardAnniversary), 0, 100);
        settings.ShopListingDays = Math.Clamp(Number(raw, SettingKeys.ShopListingDays, settings.ShopListingDays), 1, 3650);
        settings.ShopValidDays = Math.Clamp(Number(raw, SettingKeys.ShopValidDays, settings.ShopValidDays), 1, 3650);

        settings.ShopPriceMin = Math.Clamp(Number(raw, SettingKeys.ShopPriceMin, settings.ShopPriceMin), 1, 99999);
        settings.ShopPriceMax = Math.Clamp(
            Number(raw, SettingKeys.ShopPriceMax, settings.ShopPriceMax),
            settings.ShopPriceMin,
            99999);

        return settings;
    }

    private static IReadOnlyList<string> ParseLoveLetters(Dictionary<string, string> raw) {
        if (!raw.TryGetValue(SettingKeys.LoveLetters, out var json) || string.IsNullOrWhiteSpace(json)) {
            return SiteSettings.DefaultLoveLetters;
        }

        try {
            var letters = JsonSerializer.Deserialize<List<string>>(json);
            var cleaned = letters?
                .Where(letter => !string.IsNullOrWhiteSpace(letter))
                .Select(letter => letter.Trim())
                .ToList();

            return cleaned is { Count: > 0 } ? cleaned : SiteSettings.DefaultLoveLetters;
        } catch (JsonException) {
            return SiteSettings.DefaultLoveLetters;
        }
    }

    private static string Text(Dictionary<string, string> raw, string key, string fallback) =>
        raw.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static int Number(Dictionary<string, string> raw, string key, int fallback) =>
        raw.TryGetValue(key, out var value) && int.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool Flag(Dictionary<string, string> raw, string key, bool fallback) =>
        raw.TryGetValue(key, out var value) ? value is "1" or "true" or "True" : fallback;

    #endregion
}
