// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace OurStory.Services.Notifications;

/// <summary>
/// 把一条通知加密成推送服务能转发、但它自己读不懂的那一坨字节
/// </summary>
/// <remarks>
/// 走的是 RFC 8291 的 aes128gcm：服务端临时生成一对 P-256 密钥，
/// 和设备订阅时给出的公钥做 ECDH，再掺进订阅里那 16 字节 auth，
/// 推出内容密钥。中间的推送服务只见得到密文，看不到我们写了什么。
///
/// .NET 自带 ECDH、HKDF 和 AES-GCM，这里不需要任何第三方库
/// </remarks>
internal static class WebPushPayload {
    /// <summary>
    /// 记录大小。内容比它小得多，一条通知永远只占一个记录
    /// </summary>
    private const int RecordSize = 4096;

    /// <summary>
    /// 明文的上限：一个记录里还要留出 16 字节校验码和 1 字节分隔符
    /// </summary>
    public const int MaxPlaintextLength = RecordSize - 16 - 1;

    private static readonly byte[] KeyLabel = "WebPush: info\0"u8.ToArray();
    private static readonly byte[] ContentLabel = "Content-Encoding: aes128gcm\0"u8.ToArray();
    private static readonly byte[] NonceLabel = "Content-Encoding: nonce\0"u8.ToArray();

    /// <summary>
    /// 加密一条通知
    /// </summary>
    /// <param name="plaintext">要发过去的内容，长度不能超过 <see cref="MaxPlaintextLength"/></param>
    /// <param name="devicePublicKey">设备订阅里的 p256dh，65 字节未压缩点</param>
    /// <param name="deviceAuthSecret">设备订阅里的 auth，16 字节</param>
    /// <returns>可以直接当作 HTTP 请求体发出去的 aes128gcm 数据</returns>
    public static byte[] Encrypt(
        ReadOnlySpan<byte> plaintext,
        byte[] devicePublicKey,
        byte[] deviceAuthSecret) {
        ArgumentNullException.ThrowIfNull(devicePublicKey);
        ArgumentNullException.ThrowIfNull(deviceAuthSecret);

        if (plaintext.Length > MaxPlaintextLength) {
            throw new ArgumentOutOfRangeException(
                nameof(plaintext),
                $"一条通知最多 {MaxPlaintextLength} 字节。");
        }

        // 每发一条都换一对临时密钥：同一台设备的两条通知之间没有任何可关联的密钥材料
        using var server = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var serverPublicKey = VapidKeys.ToUncompressedPoint(server.ExportParameters(false).Q);

        using var device = ECDiffieHellman.Create(VapidKeys.ImportPublic(devicePublicKey));
        var sharedSecret = server.DeriveRawSecretAgreement(device.PublicKey);

        // 第一段 HKDF 把 ECDH 的结果和订阅里的 auth 拧在一起，
        // info 里带上双方公钥，防止密钥被挪到别的会话上用
        var keyInfo = new byte[KeyLabel.Length + devicePublicKey.Length + serverPublicKey.Length];
        KeyLabel.CopyTo(keyInfo);
        devicePublicKey.CopyTo(keyInfo.AsSpan(KeyLabel.Length));
        serverPublicKey.CopyTo(keyInfo.AsSpan(KeyLabel.Length + devicePublicKey.Length));

        var secretPrk = HKDF.Extract(HashAlgorithmName.SHA256, sharedSecret, deviceAuthSecret);
        var keyMaterial = HKDF.Expand(HashAlgorithmName.SHA256, secretPrk, 32, keyInfo);

        // 第二段 HKDF 用这次专属的随机盐，导出真正的内容密钥和 nonce
        var salt = RandomNumberGenerator.GetBytes(16);
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, keyMaterial, salt);
        var contentKey = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, ContentLabel);
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, NonceLabel);

        // RFC 8188 规定每个记录末尾要有分隔符，最后一个记录用 0x02
        var padded = new byte[plaintext.Length + 1];
        plaintext.CopyTo(padded);
        padded[^1] = 0x02;

        var ciphertext = new byte[padded.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(contentKey, tag.Length)) {
            aes.Encrypt(nonce, padded, ciphertext, tag);
        }

        // 报文头：盐 16 字节、记录大小 4 字节大端、公钥长度 1 字节，然后是服务端公钥本身
        var body = new byte[16 + 4 + 1 + serverPublicKey.Length + ciphertext.Length + tag.Length];
        var cursor = body.AsSpan();

        salt.CopyTo(cursor);
        cursor = cursor[salt.Length..];

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(cursor, RecordSize);
        cursor = cursor[4..];

        cursor[0] = (byte)serverPublicKey.Length;
        cursor = cursor[1..];

        serverPublicKey.CopyTo(cursor);
        cursor = cursor[serverPublicKey.Length..];

        ciphertext.CopyTo(cursor);
        tag.CopyTo(cursor[ciphertext.Length..]);

        CryptographicOperations.ZeroMemory(sharedSecret);
        CryptographicOperations.ZeroMemory(secretPrk);
        CryptographicOperations.ZeroMemory(keyMaterial);
        CryptographicOperations.ZeroMemory(prk);
        CryptographicOperations.ZeroMemory(contentKey);

        return body;
    }
}
