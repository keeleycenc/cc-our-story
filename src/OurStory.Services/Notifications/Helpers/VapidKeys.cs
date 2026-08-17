// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Buffers.Text;
using System.Security.Cryptography;

namespace OurStory.Services.Notifications;

/// <summary>
/// VAPID 用的那对 P-256 密钥：生成、编码，以及从配置里还原成能签名的对象
/// </summary>
/// <remarks>
/// Web Push 圈子里约定的编码方式是：公钥取 65 字节的未压缩点（0x04 开头），
/// 私钥取 32 字节的标量，两个都用 base64url。浏览器的
/// <c>applicationServerKey</c> 认的就是前者
/// </remarks>
public static class VapidKeys {
    /// <summary>
    /// 未压缩点的长度：1 字节前缀加上 X、Y 各 32 字节
    /// </summary>
    public const int PublicKeyLength = 65;

    /// <summary>
    /// P-256 私钥标量的长度
    /// </summary>
    public const int PrivateKeyLength = 32;

    /// <summary>
    /// 现生成一对新密钥
    /// </summary>
    /// <returns>base64url 编码的公钥和私钥</returns>
    public static (string PublicKey, string PrivateKey) Create() {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: true);

        return (
            Base64Url.EncodeToString(ToUncompressedPoint(parameters.Q)),
            Base64Url.EncodeToString(Pad(parameters.D!, PrivateKeyLength)));
    }

    /// <summary>
    /// 把配置里那对 base64url 还原成带私钥的曲线参数
    /// </summary>
    /// <exception cref="FormatException">编码不合法，或者长度对不上。</exception>
    public static ECParameters ImportPrivate(string publicKey, string privateKey) {
        var point = Decode(publicKey, PublicKeyLength, nameof(publicKey));
        var scalar = Decode(privateKey, PrivateKeyLength, nameof(privateKey));

        return new ECParameters {
            Curve = ECCurve.NamedCurves.nistP256,
            D = scalar,
            Q = ToPoint(point)
        };
    }

    /// <summary>
    /// 把设备交上来的 65 字节公钥还原成曲线参数
    /// </summary>
    /// <exception cref="FormatException">编码不合法，或者长度对不上。</exception>
    public static ECParameters ImportPublic(byte[] uncompressedPoint) {
        ArgumentNullException.ThrowIfNull(uncompressedPoint);

        return uncompressedPoint.Length != PublicKeyLength || uncompressedPoint[0] != 0x04
            ? throw new FormatException("公钥必须是 65 字节的未压缩 P-256 点。")
            : new ECParameters {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = ToPoint(uncompressedPoint)
            };
    }

    /// <summary>
    /// 解一段 base64url，顺带检查长度
    /// </summary>
    /// <exception cref="FormatException">编码不合法，或者长度对不上。</exception>
    public static byte[] Decode(string? value, int expectedLength, string name) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new FormatException($"{name} 是空的。");
        }

        byte[] bytes;
        try {
            bytes = Base64Url.DecodeFromChars(value.Trim());
        } catch (FormatException exception) {
            throw new FormatException($"{name} 不是合法的 base64url。", exception);
        }

        return bytes.Length == expectedLength
            ? bytes
            : throw new FormatException($"{name} 应该是 {expectedLength} 字节，实际 {bytes.Length} 字节。");
    }

    /// <summary>
    /// 把曲线上的点摊成 0x04 开头的 65 字节
    /// </summary>
    public static byte[] ToUncompressedPoint(ECPoint point) {
        var bytes = new byte[PublicKeyLength];
        bytes[0] = 0x04;
        Pad(point.X!, 32).CopyTo(bytes, 1);
        Pad(point.Y!, 32).CopyTo(bytes, 33);
        return bytes;
    }

    private static ECPoint ToPoint(byte[] uncompressedPoint) => new() {
        X = uncompressedPoint[1..33],
        Y = uncompressedPoint[33..65]
    };

    /// <summary>
    /// 大整数在前面补零对齐到固定长度：曲线参数偶尔会少几个前导零字节
    /// </summary>
    private static byte[] Pad(byte[] value, int length) {
        if (value.Length == length) {
            return value;
        }

        if (value.Length > length) {
            return value[^length..];
        }

        var padded = new byte[length];
        value.CopyTo(padded, length - value.Length);
        return padded;
    }
}
