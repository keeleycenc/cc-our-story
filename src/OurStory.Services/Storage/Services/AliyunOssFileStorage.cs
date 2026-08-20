// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using OurStory.Core.Abstractions;
using OurStory.Core.Configuration;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace OurStory.Services.Storage;

// 只用到 PUT / DELETE 两个接口，直接按 OSS 的 V1 签名拼请求头，不引入官方 SDK：省一个依赖，也省掉它一长串传递依赖
public class AliyunOssFileStorage(
    IHttpClientFactory httpClientFactory,
    ActiveConfiguration configuration,
    ILogger<AliyunOssFileStorage> logger) : IFileStorage {

    public const string HttpClientName = "aliyun-oss";

    public string DriverName => "阿里云 OSS";

    public async Task<string> SaveAsync(Stream content, string extension, string contentType, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(content);

        var objectKey = ObjectKeyFactory.Create(configuration.Storage.Prefix, extension, DateTime.Now);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var request = CreateRequest(HttpMethod.Put, objectKey, contentType, new Dictionary<string, string>(StringComparer.Ordinal) {
            ["x-oss-object-acl"] = "public-read"
        });

        request.Content = new StreamContent(buffer);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var response = await Send(request, cancellationToken);
        if (!response) {
            throw new InvalidOperationException("上传到 OSS 失败，详情见日志。");
        }

        return objectKey;
    }

    public async Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default) {
        using var request = CreateRequest(HttpMethod.Delete, objectKey, string.Empty, []);
        return await Send(request, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) {
        var files = new List<StoredFile>();
        var prefix = ObjectKeyFactory.NormalizePrefix(configuration.Storage.Prefix).TrimEnd('/') + "/";
        string? marker = null;

        do {
            var query = $"?prefix={Uri.EscapeDataString(prefix)}&max-keys=1000";
            if (!string.IsNullOrEmpty(marker)) {
                query += $"&marker={Uri.EscapeDataString(marker)}";
            }

            using var request = CreateRequest(HttpMethod.Get, string.Empty, string.Empty, []);
            request.RequestUri = new Uri(ApiUrl(string.Empty) + query);

            var client = httpClientFactory.CreateClient(HttpClientName);
            try {
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    logger.LogError("OSS GET 列表失败（{Status}）：{Body}", (int)response.StatusCode, body);
                    return [];
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
                var contents = document.Descendants().Where(element => element.Name.LocalName == "Contents").ToList();

                foreach (var content in contents) {
                    var key = Value(content, "Key");
                    if (string.IsNullOrEmpty(key) || key.EndsWith('/')) {
                        continue;
                    }

                    _ = long.TryParse(Value(content, "Size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size);
                    _ = DateTimeOffset.TryParse(Value(content, "LastModified"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var modified);
                    files.Add(new StoredFile(key, size, modified));
                }

                var truncated = string.Equals(Value(document.Root, "IsTruncated"), "true", StringComparison.OrdinalIgnoreCase);
                marker = truncated ? Value(document.Root, "NextMarker") : null;
                if (truncated && string.IsNullOrEmpty(marker)) {
                    marker = contents.Select(content => Value(content, "Key")).LastOrDefault(key => !string.IsNullOrEmpty(key));
                }
            } catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Xml.XmlException) {
                logger.LogError(exception, "OSS 图片列表读取失败");
                return [];
            }
        } while (!string.IsNullOrEmpty(marker));

        return files;
    }

    public string PublicUrl(string objectKey) => $"{configuration.Storage.Oss.PublicBaseUrl.TrimEnd('/')}/{LocalFileStorage.EncodeKey(objectKey)}";

    #region 私有方法

    private async Task<bool> Send(HttpRequestMessage request, CancellationToken cancellationToken) {
        var client = httpClientFactory.CreateClient(HttpClientName);

        try {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) {
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("OSS {Method} 失败（{Status}）：{Body}", request.Method, (int)response.StatusCode, body);
            return false;
        } catch (HttpRequestException exception) {
            logger.LogError(exception, "OSS {Method} 请求发不出去", request.Method);
            return false;
        } catch (TaskCanceledException exception) {
            logger.LogError(exception, "OSS {Method} 请求超时", request.Method);
            return false;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string objectKey, string contentType, Dictionary<string, string> ossHeaders) {
        var oss = configuration.Storage.Oss;
        var date = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);

        // 签名串的格式和顺序是 OSS 定死的，多一个换行少一个换行都会 403
        var canonicalHeaders = new StringBuilder();
        foreach (var (name, value) in ossHeaders.OrderBy(item => item.Key, StringComparer.Ordinal)) {
            _ = canonicalHeaders.Append(name.ToLowerInvariant()).Append(':').Append(value.Trim()).Append('\n');
        }

        var canonicalResource = $"/{oss.Bucket}/{objectKey.TrimStart('/')}";
        var stringToSign = $"{method.Method}\n\n{contentType}\n{date}\n{canonicalHeaders}{canonicalResource}";

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(oss.AccessKeySecret));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        var request = new HttpRequestMessage(method, ApiUrl(objectKey));
        _ = request.Headers.TryAddWithoutValidation("Date", date);
        _ = request.Headers.TryAddWithoutValidation("Authorization", $"OSS {oss.AccessKeyId}:{signature}");

        foreach (var (name, value) in ossHeaders) {
            _ = request.Headers.TryAddWithoutValidation(name, value);
        }

        return request;
    }

    private Uri ApiUrl(string objectKey) {
        var oss = configuration.Storage.Oss;
        var endpoint = string.IsNullOrWhiteSpace(oss.ApiEndpoint)
            ? $"https://oss-{oss.Region}.aliyuncs.com"
            : oss.ApiEndpoint;

        var parsed = new Uri(endpoint, UriKind.Absolute);
        var host = parsed.Host.StartsWith(oss.Bucket + ".", StringComparison.OrdinalIgnoreCase)
            ? parsed.Host
            : $"{oss.Bucket}.{parsed.Host}";

        return new Uri($"{parsed.Scheme}://{host}/{LocalFileStorage.EncodeKey(objectKey)}");
    }

    private static string? Value(XElement? parent, string name) =>
        parent?.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value;

    #endregion
}
