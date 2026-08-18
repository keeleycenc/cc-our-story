// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.Storage;

/// <summary>
/// 图片派生规格定义。
/// 一张原始图片可根据不同使用场景生成不同尺寸的版本：
/// 封面使用固定比例裁剪，正文展示使用等比例缩放。
/// <see cref="Name"/> 同时作为 URL 路径段和缓存目录名称。
/// </summary>
/// <param name="Name">规格名称，用于生成访问路径和缓存目录</param>
/// <param name="Width">目标最大宽度</param>
/// <param name="Height">目标高度；当 <see cref="Crop"/> 为 false 时不参与尺寸限制</param>
/// <param name="Crop">是否执行固定尺寸裁剪；false 表示保持原始比例缩放</param>
public sealed record ImageVariant(string Name, int Width, int Height, bool Crop) {
    /// <summary>
    /// 封面图片规格。
    /// 固定 4:3 比例裁剪，与前端封面区域的 object-fit: cover 保持一致。
    /// </summary>
    public static readonly ImageVariant Cover = new("cover", 720, 540, true);

    /// <summary>
    /// 正文展示图片规格。
    /// 保持原始比例，仅限制最大宽度，不进行裁剪。
    /// </summary>
    /// <remarks>
    /// 正文区域设计宽度约为 720px，因此生成 1440px 宽图片，
    /// 兼顾普通屏幕显示质量和高 DPI 屏幕清晰度。
    /// 查看原图时由图片查看器加载原始文件。
    /// </remarks>
    public static readonly ImageVariant Preview = new("preview", 1440, 0, false);

    private static readonly ImageVariant[] All = [Cover, Preview];

    /// <summary>
    /// 根据规格名称获取图片规格定义。
    /// </summary>
    /// <param name="name">规格名称</param>
    /// <returns>匹配的图片规格；不存在时返回 null</returns>
    public static ImageVariant? Parse(string? name) =>
        All.FirstOrDefault(variant => string.Equals(variant.Name, name, StringComparison.OrdinalIgnoreCase));
}
