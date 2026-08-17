// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Services.Notifications;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// Web Push 那一层加密和签名的测试
/// </summary>
/// <remarks>
/// 这些格式一旦写错，表现是「通知悄悄收不到」——推送服务照收不误，
/// 只有设备那头解不开。所以这里干脆把浏览器该做的解密照着实现一遍，
/// 真的解出原文才算过
/// </remarks>
public class WebPushTests {
    [Fact]
    public void 生成的密钥能原样还原回来() {
        var (publicKey, privateKey) = VapidKeys.Create();

        var parameters = VapidKeys.ImportPrivate(publicKey, privateKey);

        Assert.Equal(32, parameters.D!.Length);
        Assert.Equal(32, parameters.Q.X!.Length);
        Assert.Equal(32, parameters.Q.Y!.Length);
        Assert.Equal(publicKey, Base64Url.EncodeToString(VapidKeys.ToUncompressedPoint(parameters.Q)));
    }

    [Fact]
    public void 每次生成的密钥都不一样() {
        var (PublicKey, PrivateKey) = VapidKeys.Create();
        var (SecondPublicKey, SecondPrivateKey) = VapidKeys.Create();

        Assert.NotEqual(PublicKey, SecondPublicKey);
        Assert.NotEqual(PrivateKey, SecondPrivateKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("不是 base64")]
    [InlineData("c2hvcnQ")]
    public void 密钥不合法时当场报错(string value) =>
        Assert.Throws<FormatException>(() => VapidKeys.Decode(value, VapidKeys.PublicKeyLength, "p256dh"));

    [Fact]
    public void 加密后的内容设备能解开() {
        var device = PushSubscriber.Create();
        const string plaintext = """{"title":"想你了","body":"今天也是"}""";

        var encrypted = WebPushPayload.Encrypt(
            Encoding.UTF8.GetBytes(plaintext),
            device.PublicKey,
            device.AuthSecret);

        Assert.Equal(plaintext, device.Decrypt(encrypted));
    }

    [Fact]
    public void 同一条内容每次加密的结果都不同() {
        var device = PushSubscriber.Create();
        var payload = "同一句话"u8.ToArray();

        var first = WebPushPayload.Encrypt(payload, device.PublicKey, device.AuthSecret);
        var second = WebPushPayload.Encrypt(payload, device.PublicKey, device.AuthSecret);

        // 每条都换临时密钥和随机盐，密文一样反而说明哪里写死了
        Assert.NotEqual(first, second);
        Assert.Equal(device.Decrypt(first), device.Decrypt(second));
    }

    [Fact]
    public void 内容超过一个记录就拒绝() {
        var device = PushSubscriber.Create();
        var tooLong = new byte[WebPushPayload.MaxPlaintextLength + 1];

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => WebPushPayload.Encrypt(tooLong, device.PublicKey, device.AuthSecret));
    }

    [Fact]
    public async Task 请求带着推送服务要看的那几样东西() {
        var device = PushSubscriber.Create();
        var (sender, capture) = Sender();

        var outcome = await sender.SendAsync(Device(device), """{"title":"喂"}""");

        Assert.Equal(PushSendOutcome.Delivered, outcome);

        var request = capture.Request!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("aes128gcm", Assert.Single(request.Content!.Headers.ContentEncoding));
        Assert.Equal("application/octet-stream", request.Content.Headers.ContentType!.MediaType);
        Assert.True(request.Headers.Contains("TTL"));

        // 发出去的那一坨确实是这条通知，而不是别的什么
        Assert.Equal("""{"title":"喂"}""", device.Decrypt(capture.Body!));
    }

    [Fact]
    public async Task VAPID令牌签得对而且认得出受众() {
        var (sender, capture) = Sender(subject: "mailto:us@example.com");
        var (publicKey, _) = KeyPair;

        _ = await sender.SendAsync(Device(PushSubscriber.Create()), "{}");

        var header = capture.Request!.Headers.GetValues("Authorization").Single();
        Assert.StartsWith("vapid t=", header, StringComparison.Ordinal);

        var token = header["vapid t=".Length..header.IndexOf(", k=", StringComparison.Ordinal)];
        var advertised = header[(header.IndexOf(", k=", StringComparison.Ordinal) + 4)..];
        Assert.Equal(publicKey, advertised);

        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        // 签名要能被公布出去的那把公钥验过，否则推送服务会直接拒收
        using var key = ECDsa.Create(new ECParameters {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = VapidKeys.ImportPrivate(publicKey, KeyPair.PrivateKey).Q
        });

        Assert.True(key.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            Base64Url.DecodeFromChars(parts[2]),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1])).RootElement;
        Assert.Equal("https://push.example.com", claims.GetProperty("aud").GetString());
        Assert.Equal("mailto:us@example.com", claims.GetProperty("sub").GetString());
        Assert.True(claims.GetProperty("exp").GetInt64() > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task 按推送服务的回应决定这条订阅还留不留() {
        // 404 和 410 是「这个订阅没了」，其余失败都还有下次
        Assert.Equal(PushSendOutcome.Delivered, await OutcomeOf(HttpStatusCode.Created));
        Assert.Equal(PushSendOutcome.Delivered, await OutcomeOf(HttpStatusCode.OK));
        Assert.Equal(PushSendOutcome.Gone, await OutcomeOf(HttpStatusCode.NotFound));
        Assert.Equal(PushSendOutcome.Gone, await OutcomeOf(HttpStatusCode.Gone));
        Assert.Equal(PushSendOutcome.Failed, await OutcomeOf(HttpStatusCode.TooManyRequests));
        Assert.Equal(PushSendOutcome.Failed, await OutcomeOf(HttpStatusCode.InternalServerError));
    }

    [Fact]
    public async Task 没配密钥时不会往外发() {
        var configuration = new ActiveConfiguration(new ConfigurationStore("."), new OurStoryConfiguration());
        var capture = new CapturingHandler(HttpStatusCode.Created);
        var sender = new WebPushSender(new StubClients(capture), configuration, NullLogger<WebPushSender>.Instance);

        Assert.False(sender.IsConfigured);
        Assert.Equal(PushSendOutcome.NotConfigured, await sender.SendAsync(Device(PushSubscriber.Create()), "{}"));
        Assert.Null(capture.Request);
    }

    [Fact]
    public async Task 订阅里的密钥是坏的就把这台设备判掉() {
        var (sender, capture) = Sender();
        var device = Device(PushSubscriber.Create());
        device.P256dh = "这不是密钥";

        Assert.Equal(PushSendOutcome.Gone, await sender.SendAsync(device, "{}"));
        Assert.Null(capture.Request);
    }

    #region 私有方法

    private static readonly (string PublicKey, string PrivateKey) KeyPair = VapidKeys.Create();

    private static async Task<PushSendOutcome> OutcomeOf(HttpStatusCode status) {
        var (sender, _) = Sender(status);
        return await sender.SendAsync(Device(PushSubscriber.Create()), "{}");
    }

    private static (WebPushSender Sender, CapturingHandler Capture) Sender(
        HttpStatusCode status = HttpStatusCode.Created,
        string subject = "https://ourstory.example.com") {
        var configuration = new ActiveConfiguration(
            new ConfigurationStore("."),
            new OurStoryConfiguration {
                Push = {
                    PublicKey = KeyPair.PublicKey,
                    PrivateKey = KeyPair.PrivateKey,
                    Subject = subject
                }
            });

        var capture = new CapturingHandler(status);
        return (new WebPushSender(new StubClients(capture), configuration, NullLogger<WebPushSender>.Instance), capture);
    }

    private static (WebPushSender Sender, CapturingHandler Capture) Sender(string subject) =>
        Sender(HttpStatusCode.Created, subject);

    private static PushDevice Device(PushSubscriber subscriber) => new() {
        Id = 1,
        UserId = 1,
        Endpoint = "https://push.example.com/send/abc123",
        P256dh = Base64Url.EncodeToString(subscriber.PublicKey),
        Auth = Base64Url.EncodeToString(subscriber.AuthSecret)
    };

    #endregion
}

/// <summary>
/// 扮演浏览器那一头：拿着自己的私钥去解服务端发来的密文
/// </summary>
/// <remarks>
/// 步骤和 RFC 8291 里描述的接收端一模一样，只是方向反过来
/// </remarks>
internal sealed class PushSubscriber {
    private ECDiffieHellman _key = null!;

    /// <summary>订阅里的 p256dh，65 字节未压缩点。</summary>
    public byte[] PublicKey { get; private set; } = [];

    /// <summary>订阅里的 auth，16 字节。</summary>
    public byte[] AuthSecret { get; private set; } = [];

    /// <summary>造一份新的订阅。</summary>
    public static PushSubscriber Create() {
        var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        return new PushSubscriber {
            _key = key,
            PublicKey = VapidKeys.ToUncompressedPoint(key.ExportParameters(false).Q),
            AuthSecret = RandomNumberGenerator.GetBytes(16)
        };
    }

    /// <summary>把一份 aes128gcm 报文解回原文。</summary>
    public string Decrypt(byte[] body) {
        var salt = body[..16];
        var recordSize = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4));
        var keyLength = body[20];
        var serverPublicKey = body[21..(21 + keyLength)];
        var record = body[(21 + keyLength)..];

        Assert.Equal(65, keyLength);
        Assert.True(record.Length <= recordSize);

        using var server = ECDiffieHellman.Create(VapidKeys.ImportPublic(serverPublicKey));
        var shared = _key.DeriveRawSecretAgreement(server.PublicKey);

        var keyInfo = new byte[14 + PublicKey.Length + serverPublicKey.Length];
        "WebPush: info\0"u8.CopyTo(keyInfo);
        PublicKey.CopyTo(keyInfo, 14);
        serverPublicKey.CopyTo(keyInfo, 14 + PublicKey.Length);

        var material = HKDF.Expand(
            HashAlgorithmName.SHA256,
            HKDF.Extract(HashAlgorithmName.SHA256, shared, AuthSecret),
            32,
            keyInfo);

        var prk = HKDF.Extract(HashAlgorithmName.SHA256, material, salt);
        var contentKey = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, "Content-Encoding: nonce\0"u8.ToArray());

        var ciphertext = record[..^16];
        var tag = record[^16..];
        var plaintext = new byte[ciphertext.Length];

        using (var aes = new AesGcm(contentKey, tag.Length)) {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        // 末尾那个 0x02 是记录分隔符，不属于内容
        Assert.Equal(0x02, plaintext[^1]);
        return Encoding.UTF8.GetString(plaintext[..^1]);
    }
}

/// <summary>把请求原样留下来，不真的出网。</summary>
internal sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler {
    /// <summary>最后一次收到的请求。</summary>
    public HttpRequestMessage? Request { get; private set; }

    /// <summary>最后一次收到的请求体。</summary>
    public byte[]? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Request = request;
        Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(string.Empty) };
    }
}

/// <summary>不管要哪个名字，都给同一个假的客户端。</summary>
internal sealed class StubClients(HttpMessageHandler handler) : IHttpClientFactory {
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
