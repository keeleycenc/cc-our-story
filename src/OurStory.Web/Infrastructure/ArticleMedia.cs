// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Html;
using OurStory.Services.Storage;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 将正文中的图片转换为支持缩略展示和原图查看的媒体结构。
/// 
/// 正文 HTML 在存储时已经完成渲染，此类仅在输出阶段动态处理 <c>img</c> 元素：
/// 使用优化后的展示图片替代原始地址，并通过 <c>data-full</c> 保存原图地址供查看器加载。
/// 同时写入图片尺寸信息，确保懒加载过程中保留布局空间，避免页面跳动。
/// </summary>
/// <remarks>
/// 图片查看器通过 <c>data-lightbox</c> 分组管理图片集合。
/// 后续相册、时间轴等图片场景只需输出相同的数据属性即可复用该查看器。
/// </remarks>
public sealed partial class ArticleMedia(MediaUrls media, IThumbnailService thumbnails, HtmlEncoder encoder) {
    /// <summary>
    /// 异步改写正文 HTML 中的图片元素。
    /// </summary>
    /// <param name="html">存储后的正文 HTML，不会修改原始内容</param>
    /// <param name="group">图片查看器分组名称，同组图片支持前后切换</param>
    /// <param name="cancellationToken">异步操作取消令牌</param>
    /// <returns>
    /// 改写后的 HTML 内容；
    /// 当正文为空时返回空内容。
    /// </returns>
    public async Task<IHtmlContent> RenderAsync(string? html, string group, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(html)) {
            return HtmlString.Empty;
        }

        var matches = Images().Matches(html);
        if (matches.Count == 0) {
            return new HtmlString(html);
        }

        var builder = new StringBuilder(html.Length + (matches.Count * 320));
        var cursor = 0;
        var index = 0;

        foreach (Match match in matches) {
            _ = builder.Append(html, cursor, match.Index - cursor);
            cursor = match.Index + match.Length;

            // 作者自己给图套了链接，那是他要跳去别处，别抢过来当查看器用
            if (match.Groups["anchor"].Success) {
                _ = builder.Append(match.Value);
                continue;
            }

            var tag = match.Groups["tag"].Value;
            var source = Attribute(tag, "src");

            if (source.Length == 0 || !media.CanResize(source)) {
                _ = builder.Append(match.Value);
                continue;
            }

            var figure = match.Groups["figure"].Success;
            _ = builder.Append(await BuildAsync(tag, source, group, index, figure, cancellationToken));
            index++;
        }

        _ = builder.Append(html, cursor, html.Length - cursor);
        return new HtmlString(builder.ToString());
    }

    private async Task<string> BuildAsync(
        string tag,
        string source,
        string group,
        int index,
        bool figure,
        CancellationToken cancellationToken) {
        var alternate = Attribute(tag, "alt");
        var caption = Attribute(tag, "title");
        var size = media.LocalKey(source) is { } key
            ? await thumbnails.MeasureAsync(key, cancellationToken)
            : null;

        var builder = new StringBuilder(320);

        if (figure) {
            _ = builder.Append("<figure class=\"article-figure\">");
        }

        _ = builder
            .Append("<button type=\"button\" class=\"article-figure-frame\" data-cover data-lightbox=\"")
            .Append(encoder.Encode(group))
            .Append("\" data-index=\"")
            .Append(index)
            .Append("\" data-full=\"")
            .Append(encoder.Encode(source))
            .Append('"');

        if (caption.Length > 0) {
            _ = builder.Append(" data-caption=\"").Append(encoder.Encode(caption)).Append('"');
        }

        // 比例先占好位，图到了也不会把下面的段落顶下去；
        // 读不出尺寸的（比如存在 OSS 上）交给样式里那个默认值
        if (size is { Width: > 0, Height: > 0 } known) {
            _ = builder
                .Append(" style=\"--figure-ratio:")
                .Append(known.Width)
                .Append('/')
                .Append(known.Height)
                .Append('"');
        }

        _ = builder
            .Append("><img src=\"")
            .Append(encoder.Encode(media.Preview(source)))
            .Append("\" alt=\"")
            .Append(encoder.Encode(alternate))
            .Append('"');

        if (size is { Width: > 0, Height: > 0 } pixels) {
            _ = builder
                .Append(" width=\"").Append(pixels.Width)
                .Append("\" height=\"").Append(pixels.Height).Append('"');
        }

        _ = builder.Append(" loading=\"lazy\" decoding=\"async\"></button>");

        if (figure) {
            if (caption.Length > 0) {
                _ = builder.Append("<figcaption>").Append(encoder.Encode(caption)).Append("</figcaption>");
            }

            _ = builder.Append("</figure>");
        }

        return builder.ToString();
    }

    private static string Attribute(string tag, string name) {
        var pattern = name switch {
            "src" => Source(),
            "alt" => Alternate(),
            _ => Title()
        };

        var match = pattern.Match(tag);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value) : string.Empty;
    }

    // 三种情形按优先级排：套了链接的整段跳过；独占一段的换成 figure；
    // 剩下混在文字里的只包一层，免得把 figure 塞进 p 里
    [GeneratedRegex(
        """(?<anchor><a\b[^>]*>\s*<img\b[^>]*>\s*</a>)|(?<figure><p>\s*(?<tag><img\b[^>]*>)\s*</p>)|(?<tag><img\b[^>]*>)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Images();

    [GeneratedRegex("""\bsrc\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Source();

    [GeneratedRegex("""\balt\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Alternate();

    [GeneratedRegex("""\btitle\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Title();
}
