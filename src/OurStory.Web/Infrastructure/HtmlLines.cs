// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Html;
using System.Text;
using System.Text.Encodings.Web;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 表示 HtmlLines
/// </summary>
public static class HtmlLines {
    /// <summary>
    /// 把多行文本转成保留换行的 HTML。
    ///
    /// 不能先整体转义再替换换行：编码器会把 \n 本身escape 成 &amp;#xA;，
    /// 替换就再也匹配不上了。所以先按行切开，逐行转义，最后用 &lt;br&gt; 连起来。
    /// </summary>
    public static IHtmlContent WithBreaks(string? text, HtmlEncoder encoder) {
        ArgumentNullException.ThrowIfNull(encoder);

        if (string.IsNullOrEmpty(text)) {
            return HtmlString.Empty;
        }

        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var builder = new StringBuilder(text.Length + lines.Length * 4);

        for (var index = 0; index < lines.Length; index++) {
            if (index > 0) {
                _ = builder.Append("<br>");
            }

            _ = builder.Append(encoder.Encode(lines[index]));
        }

        return new HtmlString(builder.ToString());
    }
}
