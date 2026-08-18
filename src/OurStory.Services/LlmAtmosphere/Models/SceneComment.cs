// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 交给模型看的一条已有留言
/// </summary>
/// <param name="AuthorName">谁说的</param>
/// <param name="Content">说了什么</param>
/// <param name="IsSelf">是不是这次要开口的这个角色自己早先说的</param>
/// <param name="ReplyToName">这句是回给谁的；顶层留言为 null</param>
public sealed record SceneComment(string AuthorName, string Content, bool IsSelf, string? ReplyToName = null);
