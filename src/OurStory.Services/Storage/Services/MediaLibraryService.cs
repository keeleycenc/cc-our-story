// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using OurStory.Core.Abstractions;
using OurStory.Core.Configuration;
using OurStory.Core.Models;
using OurStory.Data;

namespace OurStory.Services.Storage;

internal sealed class MediaLibraryService(
    OurStoryDbContext db,
    IFileStorage storage,
    IThumbnailService thumbnails,
    ActiveConfiguration configuration) : IMediaLibraryService {

    public async Task<MediaDeleteResult> DeleteAsync(string objectKey, CancellationToken cancellationToken = default) {
        objectKey = Normalize(objectKey);
        if (!IsOwnedKey(objectKey)) {
            return MediaDeleteResult.Failed("图片标识不合法，未执行删除。");
        }

        var references = await FindReferencesAsync(objectKey, cancellationToken);
        if (references.Count > 0) {
            return MediaDeleteResult.InUse(references);
        }

        if (!await storage.DeleteAsync(objectKey, cancellationToken)) {
            return MediaDeleteResult.Failed("图片删除失败，请查看服务日志后重试。");
        }

        await thumbnails.ClearAsync(objectKey, cancellationToken);
        return MediaDeleteResult.Deleted();
    }

    public async Task<IReadOnlyList<MediaReference>> FindReferencesAsync(string objectKey, CancellationToken cancellationToken = default) {
        objectKey = Normalize(objectKey);
        if (!IsOwnedKey(objectKey)) {
            return [];
        }

        var references = new List<MediaReference>();

        var moments = await db.Moments.AsNoTracking()
            .Where(item => item.Content.Contains(objectKey)
                || item.ContentHtml.Contains(objectKey)
                || (item.CoverUrl != null && item.CoverUrl.Contains(objectKey)))
            .Select(item => new { item.Id, item.Title, item.Content, item.ContentHtml, item.CoverUrl })
            .ToListAsync(cancellationToken);
        references.AddRange(moments.Select(item => new MediaReference(
            "点点滴滴",
            $"点点滴滴「{item.Title}」{Fields((item.Content.Contains(objectKey) || item.ContentHtml.Contains(objectKey), "正文"), (item.CoverUrl?.Contains(objectKey) == true, "封面"))}",
            $"/admin/moments/edit/{item.Id}")));

        var anniversaries = await db.Anniversaries.AsNoTracking()
            .Where(item => (item.Note != null && item.Note.Contains(objectKey))
                || item.NoteHtml.Contains(objectKey)
                || (item.CoverUrl != null && item.CoverUrl.Contains(objectKey)))
            .Select(item => new { item.Id, item.Title, item.Note, item.NoteHtml, item.CoverUrl })
            .ToListAsync(cancellationToken);
        references.AddRange(anniversaries.Select(item => new MediaReference(
            "纪念日",
            $"纪念日「{item.Title}」{Fields((item.Note?.Contains(objectKey) == true || item.NoteHtml.Contains(objectKey), "故事正文"), (item.CoverUrl?.Contains(objectKey) == true, "封面"))}",
            $"/admin/anniversaries/edit/{item.Id}")));

        var shopItems = await db.ShopItems.AsNoTracking()
            .Where(item => item.CoverUrl != null && item.CoverUrl.Contains(objectKey))
            .Select(item => new { item.Id, item.Title }).ToListAsync(cancellationToken);
        references.AddRange(shopItems.Select(item => new MediaReference("心意商城", $"心愿「{item.Title}」的封面", $"/admin/shop?edit={item.Id}")));

        var presets = await db.ShopPresets.AsNoTracking()
            .Where(item => item.CoverUrl != null && item.CoverUrl.Contains(objectKey))
            .Select(item => new { item.Id, item.Title }).ToListAsync(cancellationToken);
        references.AddRange(presets.Select(item => new MediaReference("心意商城", $"心愿预设「{item.Title}」的封面", $"/admin/shop?preset={item.Id}")));

        var comments = await db.Comments.AsNoTracking()
            .Where(item => (item.AuthorUrl != null && item.AuthorUrl.Contains(objectKey))
                || (item.LlmAvatarUrl != null && item.LlmAvatarUrl.Contains(objectKey)))
            .Select(item => new { item.Id, item.AuthorName }).ToListAsync(cancellationToken);
        references.AddRange(comments.Select(item => new MediaReference("留言", $"{item.AuthorName} 的留言资料/头像（留言 #{item.Id}）", "/admin/comments")));

        var settings = await db.Settings.AsNoTracking()
            .Where(item => item.Value.Contains(objectKey))
            .Select(item => item.Key).ToListAsync(cancellationToken);
        references.AddRange(settings.Select(key => new MediaReference("站点设置", SettingDescription(key), "/admin/settings")));

        foreach (var member in configuration.LlmAtmosphere.Members.Where(member => member.AvatarUrl.Contains(objectKey, StringComparison.Ordinal))) {
            references.Add(new MediaReference("氛围组", $"氛围组角色「{member.Name}」的头像", "/admin/atmosphere"));
        }

        return references;
    }

    private bool IsOwnedKey(string objectKey) {
        var prefix = ObjectKeyFactory.NormalizePrefix(configuration.Storage.Prefix).TrimEnd('/') + "/";
        return ObjectKeyFactory.IsSafe(objectKey) && objectKey.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static string Normalize(string objectKey) => (objectKey ?? string.Empty).Replace('\\', '/').Trim('/');

    private static string Fields(params (bool Used, string Name)[] fields) =>
        "的" + string.Join("和", fields.Where(field => field.Used).Select(field => field.Name));

    private static string SettingDescription(string key) => key switch {
        SettingKeys.BoyAvatar => "男生头像",
        SettingKeys.GirlAvatar => "女生头像",
        _ => $"设置项「{key}」"
    };
}
