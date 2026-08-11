// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Models;

/// <summary>
/// 站点设置的强类型视图，对应设置表里的一组键值对
/// </summary>
/// <remarks>
/// 默认值就是原主题的默认值，全新部署不填任何东西也能正常打开首页
/// </remarks>
public class SiteSettings {
    /// <summary>
    /// 获取或设置 SiteTitle
    /// </summary>
    public string SiteTitle { get; set; } = "CC Our Story";

    /// <summary>
    /// 获取或设置 SiteDescription
    /// </summary>
    public string SiteDescription { get; set; } = "两个人的小宇宙";

    /// <summary>
    /// 获取或设置 BoyName
    /// </summary>
    public string BoyName { get; set; } = "男主名字";

    /// <summary>
    /// 获取或设置 GirlName
    /// </summary>
    public string GirlName { get; set; } = "女主名字";

    /// <summary>
    /// 获取或设置 BoyAvatar
    /// </summary>
    public string BoyAvatar { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 GirlAvatar
    /// </summary>
    public string GirlAvatar { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 BoySentence
    /// </summary>
    public string BoySentence { get; set; } = "温柔又坚定地爱着你";

    /// <summary>
    /// 获取或设置 GirlSentence
    /// </summary>
    public string GirlSentence { get; set; } = "我的例外，也是我的偏爱";

    /// <summary>在一起的时刻，按站点时区理解。</summary>
    public DateTime LoveStartedAt { get; set; } = new(2024, 5, 20, 20, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// 获取或设置 HomeSentence
    /// </summary>
    public string HomeSentence { get; set; } = "日子慢慢过，故事慢慢写。这里珍藏着只属于我们的每一份心动。";

    /// <summary>
    /// 获取或设置 DailyNote
    /// </summary>
    public string DailyNote { get; set; } = "想和你一起，把柴米油盐过成诗，\n把每一个普通的日子都写上我们的名字。";

    /// <summary>首页每次加载随机显示一句。</summary>
    public IReadOnlyList<string> LoveLetters { get; set; } = DefaultLoveLetters;

    /// <summary>auto / light / dark，页面右上角还能各自再切。</summary>
    public string ColorMode { get; set; } = "auto";

    /// <summary>
    /// 获取或设置 MomentsPageSize
    /// </summary>
    public int MomentsPageSize { get; set; } = 10;

    /// <summary>访客留言是否必须填邮箱。</summary>
    public bool CommentsRequireMail { get; set; }

    /// <summary>是否接受访客（未登录）留言。</summary>
    public bool AllowGuestComments { get; set; } = true;

    /// <summary>同一个人一天最多记多少次心动。</summary>
    public int HeartbeatDailyLimit { get; set; } = 99;

    /// <summary>
    /// 表示 DefaultLoveLetters
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultLoveLetters =
    [
        "遇见你以后，连平凡的日子也开始闪闪发光。",
        "我把所有温柔都存起来，只为见到你时全部给你。",
        "人间有很多美好，而你是我最偏爱的那一种。",
        "想和你把日子过成诗，也把岁月写成我们的名字。",
        "喜欢你这件事，是我做过最笃定也最浪漫的决定。",
        "你一出现，我心里的天气就自动变成晴天。",
        "所谓心动，不过是你走近时，我的世界忽然有了回声。",
        "我贪恋的人间烟火，恰好每一幕都有你。",
        "比起永远，我更想把和你的每一个今天都认真收藏。",
        "愿往后的朝朝暮暮，都能与你慢慢分享。",
        "想念有声音的话，此刻一定全世界都听见了。",
        "你是我绕过山河错落，才找到的人间值得。"
    ];

    /// <summary>某个身份在页面上的称呼。</summary>
    public string RoleName(UserRole role) => role switch {
        UserRole.Boy => BoyName,
        UserRole.Girl => GirlName,
        _ => "路过的朋友"
    };

    /// <summary>某个身份在页面上的头像，没设置就是空串。</summary>
    public string RoleAvatar(UserRole role) => role switch {
        UserRole.Boy => BoyAvatar,
        UserRole.Girl => GirlAvatar,
        _ => string.Empty
    };
}

/// <summary>设置表里用到的键名，改名会丢历史设置，所以集中放在这里。</summary>
public static class SettingKeys {
    /// <summary>
    /// 表示 SiteTitle
    /// </summary>
    public const string SiteTitle = "site.title";
    /// <summary>
    /// 表示 SiteDescription
    /// </summary>
    public const string SiteDescription = "site.description";
    /// <summary>
    /// 表示 BoyName
    /// </summary>
    public const string BoyName = "couple.boy.name";
    /// <summary>
    /// 表示 GirlName
    /// </summary>
    public const string GirlName = "couple.girl.name";
    /// <summary>
    /// 表示 BoyAvatar
    /// </summary>
    public const string BoyAvatar = "couple.boy.avatar";
    /// <summary>
    /// 表示 GirlAvatar
    /// </summary>
    public const string GirlAvatar = "couple.girl.avatar";
    /// <summary>
    /// 表示 BoySentence
    /// </summary>
    public const string BoySentence = "couple.boy.sentence";
    /// <summary>
    /// 表示 GirlSentence
    /// </summary>
    public const string GirlSentence = "couple.girl.sentence";
    /// <summary>
    /// 表示 LoveStartedAt
    /// </summary>
    public const string LoveStartedAt = "couple.startedAt";
    /// <summary>
    /// 表示 HomeSentence
    /// </summary>
    public const string HomeSentence = "home.sentence";
    /// <summary>
    /// 表示 DailyNote
    /// </summary>
    public const string DailyNote = "home.dailyNote";
    /// <summary>
    /// 表示 LoveLetters
    /// </summary>
    public const string LoveLetters = "home.loveLetters";
    /// <summary>
    /// 表示 ColorMode
    /// </summary>
    public const string ColorMode = "theme.colorMode";
    /// <summary>
    /// 表示 MomentsPageSize
    /// </summary>
    public const string MomentsPageSize = "moments.pageSize";
    /// <summary>
    /// 表示 CommentsRequireMail
    /// </summary>
    public const string CommentsRequireMail = "comments.requireMail";
    /// <summary>
    /// 表示 AllowGuestComments
    /// </summary>
    public const string AllowGuestComments = "comments.allowGuest";
    /// <summary>
    /// 表示 HeartbeatDailyLimit
    /// </summary>
    public const string HeartbeatDailyLimit = "heartbeat.dailyLimit";
}
