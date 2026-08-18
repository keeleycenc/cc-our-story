// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Abstractions;
using OurStory.Services.Storage;

namespace OurStory.Web.Api;

/// <summary>
/// 派生图的取图入口
/// </summary>
public static class MediaEndpoints {
    /// <summary>
    /// 列表封面和正文配图都从这里取图，地址与 <c>/uploads</c> 一一对应：
    /// <c>/uploads/ourstory/public/2026/08/x.png</c> 的封面是
    /// <c>/media/cover/ourstory/public/2026/08/x.png</c>，正文那一档换成
    /// <c>/media/preview/…</c>。
    ///
    /// 第一次访问时现压一张缓存下来，之后直接发文件。原图本来就由静态文件中间件
    /// 公开挂在 /uploads 上，所以这里不额外要求登录。
    /// </summary>
    public static void MapMediaEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        _ = app.MapGet("/media/{variant}/{**objectKey}", async (
            string variant,
            string objectKey,
            IThumbnailService thumbnails,
            IFileStorage storage,
            HttpContext context,
            CancellationToken cancellationToken) => {
                if (ImageVariant.Parse(variant) is not { } wanted || !ObjectKeyFactory.IsSafe(objectKey)) {
                    return Results.NotFound();
                }

                var path = await thumbnails.EnsureAsync(objectKey, wanted, cancellationToken);
                if (path is null) {
                    return Results.Redirect(storage.PublicUrl(objectKey));
                }

                context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return Results.File(path, "image/webp");
            });
    }
}
