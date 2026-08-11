// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace OurStory.Core.Options;

/// <summary>
/// 配置文件里的 "Storage" 节点
/// </summary>
/// <remarks>
/// 本地和 OSS 用同一套 object key（<c>前缀/年/月/随机名.后缀</c>），
/// 所以把本地目录整个传到 Bucket 再切换 Driver，已有图片的地址会自动跟着变
/// </remarks>
public class StorageOptions {
    /// <summary>
    /// 获取或设置存储方式；留空表示自动：OSS 那几项填全了就走 OSS，否则用本地目录
    /// </summary>
    /// <remarks>
    /// 想把参数留在配置文件里但暂时先存本地，就显式写成 <see cref="StorageDriver.Local"/>
    /// </remarks>
    public StorageDriver? Driver { get; set; }

    /// <summary>
    /// 获取或设置 object key 的前缀，只保留字母、数字、下划线、连字符和斜线
    /// </summary>
    public string Prefix { get; set; } = "ourstory/public";

    /// <summary>
    /// 获取或设置单个附件的大小上限（字节）
    /// </summary>
    public long MaxFileSize { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// 获取或设置允许上传的扩展名，小写、不带点
    /// </summary>
    public string[] AllowedExtensions { get; set; } = ["jpg", "jpeg", "png", "gif", "webp", "avif"];

    /// <summary>
    /// 获取或设置执行 Oss 操作
    /// </summary>
    public OssOptions Oss { get; set; } = new();

    /// <summary>
    /// 获取真正生效的存储方式
    /// </summary>
    /// <remarks>
    /// 选了 OSS 但参数没配全的一律退回本地，免得后台一上传就报错
    /// </remarks>
    [JsonIgnore]
    public StorageDriver EffectiveDriver =>
        (Driver ?? StorageDriver.AliyunOss) == StorageDriver.AliyunOss && Oss.IsConfigured
            ? StorageDriver.AliyunOss
            : StorageDriver.Local;
}
