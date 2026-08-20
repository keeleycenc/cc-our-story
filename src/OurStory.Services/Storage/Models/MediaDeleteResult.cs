// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Services.Storage;

/// <summary>
/// 图片删除前发现的一处业务引用
/// </summary>
public sealed record MediaReference(string Area, string Description, string EditUrl);

/// <summary>
/// 删除媒体文件的结果
/// 存在引用时，<see cref="References"/> 会返回具体引用位置
/// </summary>
public sealed record MediaDeleteResult(bool Success, string Error, IReadOnlyList<MediaReference> References) {
    /// <summary>
    /// 创建删除成功结果
    /// </summary>
    /// <returns>删除成功结果</returns>
    public static MediaDeleteResult Deleted() => new(true, string.Empty, []);

    /// <summary>
    /// 创建删除失败结果
    /// </summary>
    /// <param name="error">失败原因</param>
    /// <returns>删除失败结果</returns>
    public static MediaDeleteResult Failed(string error) => new(false, error, []);

    /// <summary>
    /// 创建文件正在使用中的结果
    /// </summary>
    /// <param name="references">引用该媒体文件的位置</param>
    /// <returns>包含引用信息的删除结果</returns>
    public static MediaDeleteResult InUse(IReadOnlyList<MediaReference> references) =>
        new(false, $"这张图片正在被 {references.Count} 处内容引用，请先删除这些地方的引用", references);
}
