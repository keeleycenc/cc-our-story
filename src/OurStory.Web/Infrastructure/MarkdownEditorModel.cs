// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Web.Infrastructure;

/// <summary>后台共用的 Markdown、图片上传和预览编辑器配置。</summary>
public sealed record MarkdownEditorModel(
    string FieldName,
    string Label,
    string? Value,
    string CoverFieldName,
    string? CoverUrl,
    string Placeholder,
    string Hint,
    int? MaxLength = null) {
    /// <summary>获取可用于标签关联的稳定 DOM 编号。</summary>
    public string Id => FieldName.Replace('.', '_').Replace('[', '_').Replace(']', '_');
}
