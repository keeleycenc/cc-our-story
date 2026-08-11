// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Abstractions;
using OurStory.Core.Configuration;

namespace OurStory.Services.Storage;

internal class AttachmentService(IFileStorage storage, ActiveConfiguration configuration) : IAttachmentService {
    private static readonly Dictionary<string, string> MimeByExtension = new(StringComparer.OrdinalIgnoreCase) {
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["webp"] = "image/webp",
        ["avif"] = "image/avif"
    };

    public string DriverName => storage.DriverName;

    public async Task<UploadResult> UploadAsync(Stream content, string fileName, long length, CancellationToken cancellationToken = default) {
        if (length <= 0) {
            return UploadResult.Fail("文件是空的。");
        }

        // 配置随时可能在后台被改，用的时候现取
        var limits = configuration.Storage;

        if (length > limits.MaxFileSize) {
            return UploadResult.Fail($"文件超过 {limits.MaxFileSize / 1024 / 1024} MB 的上限。");
        }

        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (extension.Length == 0 || !limits.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) {
            return UploadResult.Fail($"只支持这些格式：{string.Join("、", limits.AllowedExtensions)}。");
        }

        var contentType = MimeByExtension.GetValueOrDefault(extension, "application/octet-stream");
        var objectKey = await storage.SaveAsync(content, extension, contentType, cancellationToken);

        return UploadResult.Ok(storage.PublicUrl(objectKey), objectKey);
    }
}
