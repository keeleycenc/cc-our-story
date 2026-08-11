// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace OurStory.Core.Options;

/// <summary>
/// 阿里云 OSS 参数。字段名和原来插件用的环境变量一一对应（OSS_REGION、OSS_BUCKET……）
/// </summary>
public class OssOptions {
    /// <summary>
    /// 获取或设置 Region
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 Bucket
    /// </summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 AccessKeyId
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 AccessKeySecret
    /// </summary>
    public string AccessKeySecret { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置对外访问用的域名，例如 https://img.example.com
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置自定义 API 端点；留空时按 Region 拼出 https://oss-&lt;region&gt;.aliyuncs.com
    /// </summary>
    public string ApiEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// 获取一个值，指示五项必填参数是否都齐了，缺任意一个都会自动退回本地存储
    /// </summary>
    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Region)
        && !string.IsNullOrWhiteSpace(Bucket)
        && !string.IsNullOrWhiteSpace(AccessKeyId)
        && !string.IsNullOrWhiteSpace(AccessKeySecret)
        && !string.IsNullOrWhiteSpace(PublicBaseUrl);
}
