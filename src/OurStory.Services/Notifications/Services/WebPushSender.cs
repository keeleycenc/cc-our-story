// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OurStory.Services.Notifications;

/// <summary>
/// Web Push 的发信端：签 VAPID、加密内容、POST 到推送服务
/// </summary>
/// <remarks>
/// 推送服务是浏览器厂商自己的（Chrome 走 FCM、Safari 走 Apple、Firefox 走 Mozilla），
/// 地址由设备订阅时给出，我们只管照着发。它们看不到通知内容
/// </remarks>
internal sealed class WebPushSender(
    IHttpClientFactory clients,
    ActiveConfiguration configuration,
    ILogger<WebPushSender> logger) : IWebPushSender {
    /// <summary>
    /// 命名 HttpClient 的名字
    /// </summary>
    public const string HttpClientName = "ourstory.webpush";

    /// <summary>
    /// 推送服务替我们保留通知的时长：设备关机一整天，开机还能收到
    /// </summary>
    private static readonly TimeSpan MessageTimeToLive = TimeSpan.FromHours(24);

    /// <summary>
    /// VAPID 令牌的有效期。规范允许最多 24 小时，取一半，免得机器时钟稍微偏一点就被拒
    /// </summary>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    private static readonly JsonSerializerOptions TokenJson = new() {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public bool IsConfigured => configuration.Current.Push.IsConfigured;

    public string PublicKey => configuration.Current.Push.PublicKey ?? string.Empty;

    public async Task<PushSendOutcome> SendAsync(PushDevice device, string payload, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);

        var push = configuration.Current.Push;
        if (!push.IsConfigured) {
            return PushSendOutcome.NotConfigured;
        }

        if (!Uri.TryCreate(device.Endpoint, UriKind.Absolute, out var endpoint)) {
            logger.LogWarning("设备 {DeviceId} 的推送地址不是合法的 URL，跳过。", device.Id);
            return PushSendOutcome.Gone;
        }

        byte[] body;
        string authorization;
        try {
            body = WebPushPayload.Encrypt(
                Encoding.UTF8.GetBytes(payload),
                VapidKeys.Decode(device.P256dh, VapidKeys.PublicKeyLength, "p256dh"),
                VapidKeys.Decode(device.Auth, 16, "auth"));

            authorization = BuildAuthorization(push.PublicKey, push.PrivateKey, push.Subject, endpoint);
        } catch (FormatException exception) {
            logger.LogWarning(exception, "设备 {DeviceId} 的推送密钥不可用。", device.Id);
            return PushSendOutcome.Gone;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
            Content = new ByteArrayContent(body)
        };

        _ = request.Headers.TryAddWithoutValidation("Authorization", authorization);
        _ = request.Headers.TryAddWithoutValidation(
            "TTL",
            ((int)MessageTimeToLive.TotalSeconds).ToString(CultureInfo.InvariantCulture));

        // 情侣之间的消息值得立刻亮屏，但也别高到让系统把它当紧急告警
        _ = request.Headers.TryAddWithoutValidation("Urgency", "normal");

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");

        try {
            using var response = await clients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            return await ReadOutcomeAsync(device, response, cancellationToken);
        } catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested) {
            logger.LogWarning(exception, "给设备 {DeviceId} 发通知时没连上推送服务。", device.Id);
            return PushSendOutcome.Failed;
        }
    }

    #region 私有方法

    private async Task<PushSendOutcome> ReadOutcomeAsync(
        PushDevice device,
        HttpResponseMessage response,
        CancellationToken cancellationToken) {
        if (response.IsSuccessStatusCode) {
            return PushSendOutcome.Delivered;
        }

        // 404 / 410 是推送服务在说「这个订阅没了」，别再往这台设备发
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) {
            logger.LogInformation("设备 {DeviceId} 的订阅已失效，准备清掉。", device.Id);
            return PushSendOutcome.Gone;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
            "给设备 {DeviceId} 发通知失败：{Status} {Detail}",
            device.Id,
            (int)response.StatusCode,
            detail.Length > 200 ? detail[..200] : detail);

        return PushSendOutcome.Failed;
    }

    private static string BuildAuthorization(string publicKey, string privateKey, string? subject, Uri endpoint) {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal) {
            ["aud"] = endpoint.GetLeftPart(UriPartial.Authority),
            ["exp"] = DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds(),
            ["sub"] = Subject(subject)
        };

        var header = Base64Url.EncodeToString("""{"typ":"JWT","alg":"ES256"}"""u8);
        var payload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(claims, TokenJson));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{payload}");

        using var key = ECDsa.Create(VapidKeys.ImportPrivate(publicKey, privateKey));

        // JWS 要的是裸的 r||s，不是 .NET 默认的 DER 封装
        var signature = key.SignData(
            signingInput,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"vapid t={header}.{payload}.{Base64Url.EncodeToString(signature)}, k={publicKey}";
    }

    private static string Subject(string? configured) {
        var value = (configured ?? string.Empty).Trim();

        return value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? value
                : "mailto:noreply@ourstory.local";
    }

    #endregion
}
