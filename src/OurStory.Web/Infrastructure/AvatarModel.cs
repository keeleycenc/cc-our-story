// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Web.Infrastructure;

/// <summary>头像局部视图的参数。</summary>
/// <param name="Url">头像地址，留空时显示文字头像。</param>
/// <param name="Fallback">文字头像里的那个字。</param>
/// <param name="CssClass">avatar-boy / avatar-girl。</param>
public record AvatarModel(string Url, string Fallback, string CssClass);
