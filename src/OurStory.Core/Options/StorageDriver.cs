// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Options;

/// <summary>
/// 附件存储方式
/// </summary>
public enum StorageDriver {
    /// <summary>
    /// 本地
    /// </summary>
    Local = 0,

    /// <summary>
    /// 阿里云 oss
    /// </summary>
    AliyunOss = 1
}
