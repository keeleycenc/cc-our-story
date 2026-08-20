// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging.Abstractions;
using OurStory.Core.Configuration;
using OurStory.Core.Options;
using OurStory.Services.Storage;
using System.Net;
using System.Text;
using Xunit;

namespace OurStory.Tests;

/// <summary>OSS 图片库的列举与删除都使用当前 Bucket。</summary>
public sealed class OssFileStorageTests {
    [Fact]
    public async Task 图片列表解析OSS对象并限制在配置前缀下() {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ListBucketResult xmlns="http://doc.oss-cn-hangzhou.aliyuncs.com">
              <IsTruncated>false</IsTruncated>
              <Contents>
                <Key>ourstory/public/2026/08/photo.png</Key>
                <LastModified>2026-08-20T08:30:00.000Z</LastModified>
                <Size>12345</Size>
              </Contents>
            </ListBucketResult>
            """;
        var handler = new OssHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml")
        });
        var storage = Storage(handler);

        var files = await storage.ListAsync();

        var file = Assert.Single(files);
        Assert.Equal("ourstory/public/2026/08/photo.png", file.ObjectKey);
        Assert.Equal(12345, file.Size);
        Assert.Contains("prefix=ourstory%2Fpublic%2F", handler.Requests.Single().RequestUri!.Query);
        Assert.Equal(HttpMethod.Get, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task 删除向OSS发送带对象键的Delete请求() {
        var handler = new OssHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var storage = Storage(handler);

        var deleted = await storage.DeleteAsync("ourstory/public/2026/08/photo.png");

        Assert.True(deleted);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://bucket.oss-cn-shanghai.aliyuncs.com/ourstory/public/2026/08/photo.png", request.RequestUri!.AbsoluteUri);
        Assert.StartsWith("OSS id:", request.Headers.Authorization?.ToString());
    }

    private static AliyunOssFileStorage Storage(HttpMessageHandler handler) {
        var configuration = new ActiveConfiguration(new ConfigurationStore("."), new OurStoryConfiguration {
            Storage = new StorageOptions {
                Driver = StorageDriver.AliyunOss,
                Prefix = "ourstory/public",
                Oss = new OssOptions {
                    Region = "cn-shanghai",
                    Bucket = "bucket",
                    AccessKeyId = "id",
                    AccessKeySecret = "secret",
                    PublicBaseUrl = "https://images.example.test"
                }
            }
        });
        return new AliyunOssFileStorage(new ClientFactory(handler), configuration, NullLogger<AliyunOssFileStorage>.Instance);
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class OssHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
