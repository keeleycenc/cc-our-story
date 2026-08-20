// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core.Abstractions;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Options;
using OurStory.Services.Storage;
using Xunit;

namespace OurStory.Tests;

/// <summary>图片删除必须先过引用检查。</summary>
public sealed class MediaLibraryServiceTests {
    private const string Key = "ourstory/public/2026/08/shared.png";
    private const string Url = "/uploads/ourstory/public/2026/08/shared.png";

    [Fact]
    public async Task 点点滴滴和纪念日同时引用时列出两处并拒绝删除() {
        await using var db = TestDoubles.Database(nameof(点点滴滴和纪念日同时引用时列出两处并拒绝删除));
        _ = db.Moments.Add(new Moment {
            Id = 11,
            Title = "一起看海",
            Slug = "sea",
            Content = $"![海]({Url})",
            ContentHtml = $"<img src=\"{Url}\">",
            CoverUrl = Url,
            AuthorId = 1
        });
        _ = db.Anniversaries.Add(new Anniversary {
            Id = 22,
            Title = "相识纪念",
            AnniversaryDate = new DateOnly(2026, 8, 20),
            Note = $"![合照]({Url})",
            NoteHtml = $"<img src=\"{Url}\">",
            CoverUrl = Url
        });
        _ = await db.SaveChangesAsync();

        var storage = new StorageSpy();
        var cache = new ThumbnailSpy();
        var service = new MediaLibraryService(db, storage, cache, Configuration());

        var result = await service.DeleteAsync(Key);

        Assert.False(result.Success);
        Assert.Contains("2 处", result.Error);
        Assert.Collection(result.References,
            reference => {
                Assert.Equal("点点滴滴", reference.Area);
                Assert.Contains("一起看海", reference.Description);
                Assert.Contains("正文和封面", reference.Description);
            },
            reference => {
                Assert.Equal("纪念日", reference.Area);
                Assert.Contains("相识纪念", reference.Description);
                Assert.Contains("故事正文和封面", reference.Description);
            });
        Assert.False(storage.DeleteCalled);
        Assert.False(cache.ClearCalled);
    }

    [Fact]
    public async Task 引用移除后才删除原图并清理缓存() {
        await using var db = TestDoubles.Database(nameof(引用移除后才删除原图并清理缓存));
        var moment = new Moment {
            Title = "测试记录", Slug = "test", Content = Url, ContentHtml = Url, CoverUrl = Url, AuthorId = 1
        };
        _ = db.Moments.Add(moment);
        _ = await db.SaveChangesAsync();

        var storage = new StorageSpy();
        var cache = new ThumbnailSpy();
        var service = new MediaLibraryService(db, storage, cache, Configuration());
        Assert.False((await service.DeleteAsync(Key)).Success);

        moment.Content = "引用已删";
        moment.ContentHtml = "<p>引用已删</p>";
        moment.CoverUrl = null;
        _ = await db.SaveChangesAsync();

        var result = await service.DeleteAsync(Key);

        Assert.True(result.Success);
        Assert.Equal(Key, storage.DeletedKey);
        Assert.Equal(Key, cache.ClearedKey);
    }

    private static ActiveConfiguration Configuration() => new(
        new ConfigurationStore("."),
        new OurStoryConfiguration { Storage = new StorageOptions { Prefix = "ourstory/public" } });

    private sealed class StorageSpy : IFileStorage {
        public string DriverName => "测试";
        public bool DeleteCalled => DeletedKey is not null;
        public string? DeletedKey { get; private set; }
        public Task<string> SaveAsync(Stream content, string extension, string contentType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default) {
            DeletedKey = objectKey;
            return Task.FromResult(true);
        }
        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StoredFile>>([]);
        public string PublicUrl(string objectKey) => "/uploads/" + objectKey;
    }

    private sealed class ThumbnailSpy : IThumbnailService {
        public bool ClearCalled => ClearedKey is not null;
        public string? ClearedKey { get; private set; }
        public Task<string?> EnsureAsync(string objectKey, ImageVariant? variant = null, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<ImageSize?> MeasureAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<ImageSize?>(null);
        public Task ClearAsync(string objectKey, CancellationToken cancellationToken = default) {
            ClearedKey = objectKey;
            return Task.CompletedTask;
        }
    }
}
