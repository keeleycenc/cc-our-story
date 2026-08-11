// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace OurStory.Core.Helpers;

/// <summary>
/// 读写小份 JSON 文件的通用助手
/// </summary>
public static class JsonFile {
    /// <summary>
    /// 读写配置用的序列化选项
    /// </summary>
    public static readonly JsonSerializerOptions Options = new() {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 反序列化一个文件；文件里是 <c>null</c> 时返回 <c>null</c>
    /// </summary>
    /// <exception cref="JsonException">内容不是合法 JSON</exception>
    /// <exception cref="IOException">文件读不了</exception>
    public static T? Read<T>(string path) where T : class => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);

    /// <summary>
    /// 序列化到文件，父目录不存在会自动建
    /// </summary>
    /// <remarks>
    /// 先写临时文件再原子替换：写到一半断电也不会留下半份配置，
    /// 那种情况下下次启动会静默退回默认值，比直接报错更难查
    /// </remarks>
    public static void Write<T>(string path, T value) where T : class {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) {
            _ = Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
        File.Move(temporary, path, overwrite: true);
    }
}
