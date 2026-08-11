// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Configuration;

/// <summary>
/// 一次配置加载的结果
/// </summary>
/// <param name="Configuration">拿到的配置，任何情况下都不为空</param>
/// <param name="Path">配置文件路径</param>
/// <param name="Source">这份配置的来路</param>
/// <param name="Error">出岔子时的原因，<see cref="ConfigurationSource.Fallback"/> 才有值</param>
public sealed record ConfigurationLoadResult(
    OurStoryConfiguration Configuration,
    string Path,
    ConfigurationSource Source,
    string? Error = null);
