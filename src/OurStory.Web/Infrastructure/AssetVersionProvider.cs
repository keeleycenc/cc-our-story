// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 给样式和脚本加一个 ?v= 后缀，内容变了浏览器才会重新拉。
///
/// 取的是文件修改时间，和原主题的做法一致；文件读不到就退回固定值，
/// 顶多是缓存没刷新，不影响页面出来。
/// </summary>
public class AssetVersionProvider(IWebHostEnvironment environment) {
    private readonly ConcurrentDictionary<string, string> _versions = new(StringComparer.Ordinal);

    // 开发时每次都重新读文件时间：改完 css 刷一下就能看到，
    // 不必因为一个查询串被缓存住而去按 Ctrl+F5。
    // 线上文件不会变，缓存住省掉每次请求的几次磁盘调用。
    private readonly bool _cache = !environment.IsDevelopment();

    /// <summary>
    /// 获取资源版本
    /// </summary>
    public string Version(string relativePath) =>
        _cache
            ? _versions.GetOrAdd(relativePath, Read)
            : Read(relativePath);

    /// <summary>
    /// 构建地址
    /// </summary>
    public string Url(string relativePath) => $"/{relativePath.TrimStart('/')}?v={Version(relativePath)}";

    private string Read(string relativePath) {
        var file = environment.WebRootFileProvider.GetFileInfo(relativePath);
        return file.Exists ? file.LastModified.ToUnixTimeSeconds().ToString() : "1";
    }
}
