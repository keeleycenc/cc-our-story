// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Options;
using OurStory.Services.LlmAtmosphere;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 按 Responses 协议拼请求、读响应，以及把模型写回来的话收拾干净
/// </summary>
public class ResponsesProtocolTests {
    [Theory]
    [InlineData("https://api.openai.com/v1", "https://api.openai.com/v1/responses")]
    [InlineData("https://api.openai.com/v1/", "https://api.openai.com/v1/responses")]
    [InlineData("  https://gateway.local/v1  ", "https://gateway.local/v1/responses")]
    [InlineData("https://gateway.local/v1/responses", "https://gateway.local/v1/responses")]
    public void 服务地址会补上responses这一段(string configured, string expected) =>
        Assert.Equal(expected, ResponsesClient.Endpoint(configured));

    [Fact]
    public void 纯文本请求只有一条文字内容() {
        var body = ResponsesClient.Body(Request());

        Assert.Equal("test-model", body["model"]!.GetValue<string>());
        Assert.False(body["store"]!.GetValue<bool>());

        var content = body["input"]!.AsArray()[0]!["content"]!.AsArray();
        var part = Assert.Single(content);
        Assert.Equal("input_text", part!["type"]!.GetValue<string>());
    }

    [Fact]
    public void 带图时图片和文字放在同一条消息里() {
        var body = ResponsesClient.Body(Request("data:image/webp;base64,AAAA"));
        var content = body["input"]!.AsArray()[0]!["content"]!.AsArray();

        Assert.Equal(2, content.Count);
        Assert.Equal("input_image", content[1]!["type"]!.GetValue<string>());
        Assert.Equal("data:image/webp;base64,AAAA", content[1]!["image_url"]!.GetValue<string>());
    }

    [Fact]
    public void 脱掉图之后请求的其余部分不变() {
        var withImages = Request("data:image/webp;base64,AAAA");
        var plain = withImages.WithoutImages();

        Assert.Empty(plain.Images);
        Assert.Equal(withImages.Text, plain.Text);
        Assert.Equal(withImages.Instructions, plain.Instructions);
    }

    [Fact]
    public void 从标准响应里读出正文() {
        const string Body = """
            {
              "output": [
                { "type": "message", "content": [ { "type": "output_text", "text": "今天的海好蓝" } ] }
              ]
            }
            """;

        Assert.Equal("今天的海好蓝", ResponsesClient.ReadText(Body));
    }

    [Fact]
    public void 推理条目不会被当成正文() {
        const string Body = """
            {
              "output": [
                { "type": "reasoning", "summary": [] },
                { "type": "message", "content": [ { "type": "output_text", "text": "陪你去看第二次" } ] }
              ]
            }
            """;

        Assert.Equal("陪你去看第二次", ResponsesClient.ReadText(Body));
    }

    [Fact]
    public void 网关顺手拼好的正文可以直接用() {
        Assert.Equal("好久没见你们出门了", ResponsesClient.ReadText("""{ "output_text": "好久没见你们出门了" }"""));
    }

    [Fact]
    public void 读不出正文时返回空串() {
        Assert.Equal(string.Empty, ResponsesClient.ReadText("""{ "error": { "message": "model not found" } }"""));
        Assert.Equal(string.Empty, ResponsesClient.ReadText("[]"));
    }

    [Fact]
    public void 额度被推理吃光时认得出来是话没说完() {
        const string Body = """
            {
              "status": "incomplete",
              "incomplete_details": { "reason": "max_output_tokens" },
              "output": [
                { "type": "reasoning", "status": "incomplete",
                  "content": [ { "type": "reasoning_text", "text": "我们只需要一句话，口语化，像朋友评论。阿柔是评论区常" } ] }
              ]
            }
            """;

        Assert.Equal(string.Empty, ResponsesClient.ReadText(Body));
        Assert.True(ResponsesClient.IsTruncated(Body));
    }

    [Fact]
    public void 正常说完的响应不算话没说完() {
        const string Body = """
            {
              "status": "completed",
              "incomplete_details": null,
              "output": [
                { "type": "reasoning", "content": [ { "type": "reasoning_text", "text": "想一下" } ] },
                { "type": "message", "content": [ { "type": "output_text", "text": "吹乱就吹乱吧" } ] }
              ]
            }
            """;

        Assert.Equal("吹乱就吹乱吧", ResponsesClient.ReadText(Body));
        Assert.False(ResponsesClient.IsTruncated(Body));
    }

    [Fact]
    public void 有的服务只给出原因不写状态() =>
        Assert.True(ResponsesClient.IsTruncated("""{ "incomplete_details": { "reason": "max_output_tokens" } }"""));

    [Fact]
    public void 模型给自己加的名字前缀会被削掉() =>
        Assert.Equal("这张照片好好看", AtmospherePrompt.Clean("阿柔：这张照片好好看", "阿柔"));

    [Fact]
    public void 整句裹着的引号会被脱掉() {
        Assert.Equal("好羡慕呀", AtmospherePrompt.Clean("「好羡慕呀」", "阿柔"));
        Assert.Equal("好羡慕呀", AtmospherePrompt.Clean("\"好羡慕呀\"", "阿柔"));
    }

    [Fact]
    public void 句子当中的引号留着不动() =>
        Assert.Equal("你说的「一起看海」实现了", AtmospherePrompt.Clean("你说的「一起看海」实现了", "阿柔"));

    [Fact]
    public void 写成好几段的只留第一段() =>
        Assert.Equal("真好呀", AtmospherePrompt.Clean("真好呀\n\n另外我建议你们下次带上防晒", "阿柔"));

    [Fact]
    public void 太长的话会被截断() {
        var text = AtmospherePrompt.Clean(new string('好', 300), "阿柔");

        Assert.Equal(AtmospherePrompt.OutputLimit + 1, text.Length);
        Assert.EndsWith("…", text, StringComparison.Ordinal);
    }

    [Fact]
    public void 什么都没写回来就当没说过() {
        Assert.Equal(string.Empty, AtmospherePrompt.Clean(null, "阿柔"));
        Assert.Equal(string.Empty, AtmospherePrompt.Clean("   ", "阿柔"));
    }

    [Fact]
    public void 人设和写作要求会一起交给模型() {
        var instructions = AtmospherePrompt.Instructions(Member());

        Assert.Contains("阿柔", instructions, StringComparison.Ordinal);
        Assert.Contains("说话温温柔柔", instructions, StringComparison.Ordinal);
        Assert.Contains("不是助手", instructions, StringComparison.Ordinal);
    }

    #region 私有方法

    private static ResponsesRequest Request(params string[] images) =>
        new(Member(), "你是阿柔", "他们去看海了", [.. images.Select(url => new ResponsesImage(url))]);

    private static LlmAtmosphereMember Member() =>
        new() {
            Id = "warm",
            Name = "阿柔",
            BaseUrl = "https://example.com/v1",
            Model = "test-model",
            ApiKey = "sk-test",
            Prompt = "说话温温柔柔"
        };

    #endregion
}
