// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Entities;

namespace OurStory.Core.Models;

/// <summary>
/// 一条通知的内容，最终原样交给 Service Worker 的 <c>showNotification()</c>
/// </summary>
/// <param name="Title">通知标题</param>
/// <param name="Body">正文，锁屏上只看得到前面一两行</param>
/// <param name="Url">点开之后去哪一页</param>
/// <param name="Tag">同一个 tag 的通知会互相覆盖，用来避免同一件事堆一屏</param>
public sealed record PushMessage(string Title, string Body, string Url = "/", string Tag = "ourstory");

/// <summary>
/// 一次投递请求：把哪条通知、按哪一类、发给谁
/// </summary>
/// <param name="Topic">通知分类，决定要不要看收件人勾的那几项</param>
/// <param name="Message">通知内容</param>
/// <param name="TargetUserId">只发给这个人；为 null 表示两个人都发</param>
/// <param name="ExceptUserId">不发给这个人，通常是刚刚做出这个动作的本人</param>
/// <param name="TargetDeviceId">要发送的目标台设备</param>
public sealed record NotificationRequest(
    NotificationTopic Topic,
    PushMessage Message,
    int? TargetUserId = null,
    int? ExceptUserId = null,
    long? TargetDeviceId = null,
    NotificationChannelKind? Channel = null,
    string? SiteOrigin = null) {

    /// <summary>
    /// 发给对方：除了自己，剩下那个人
    /// </summary>
    public static NotificationRequest ToPartner(NotificationTopic topic, int selfUserId, PushMessage message) =>
        new(topic, message, ExceptUserId: selfUserId);

    /// <summary>
    /// 发给指定的某个人
    /// </summary>
    public static NotificationRequest ToUser(NotificationTopic topic, int userId, PushMessage message) =>
        new(topic, message, TargetUserId: userId);
}

/// <summary>
/// 通知可用的投递渠道
/// </summary>
public enum NotificationChannelKind {
    /// <summary>
    /// 浏览器 Web Push
    /// </summary>
    WebPush = 0,

    /// <summary>
    /// SMTP 邮件
    /// </summary>
    Email = 1
}

/// <summary>
/// 浏览器订阅成功后交上来的那份凭据
/// </summary>
/// <param name="Endpoint">推送服务给的投递地址</param>
/// <param name="P256dh">设备公钥，base64url</param>
/// <param name="Auth">设备认证密钥，base64url</param>
/// <param name="DeviceKey">浏览器自己记着的设备编号，认「是不是同一台」全靠它</param>
/// <param name="PreviousEndpoint">
/// 换发订阅前的老地址。Service Worker 读不到浏览器的本地存储、拿不出设备编号，
/// 只能靠这个把新旧两份订阅接上，不然会平白多出一台设备
/// </param>
/// <param name="UserAgent">浏览器的 User-Agent，用来给设备起个看得懂的名字</param>
public sealed record PushDeviceRegistration(
    string Endpoint,
    string P256dh,
    string Auth,
    string? DeviceKey = null,
    string? PreviousEndpoint = null,
    string? UserAgent = null);

/// <summary>
/// 后台设备列表里的一行
/// </summary>
/// <param name="Id">设备编号</param>
/// <param name="Key">设备的固定编号，页面靠它认出哪张卡片是「本机」</param>
/// <param name="Name">设备名</param>
/// <param name="CreatedAt">首次授权时间，站点时区</param>
/// <param name="LastPushedAt">最近一次收到通知的时间，站点时区；从没收到过为 null</param>
public sealed record PushDeviceCard(
    long Id,
    string Key,
    string Name,
    DateTime CreatedAt,
    DateTime? LastPushedAt);

/// <summary>
/// 对方那头准备好了没有
/// </summary>
/// <param name="Enabled">对方开着通知总开关</param>
/// <param name="Devices">对方有几台设备能收到</param>
public sealed record PartnerReadiness(
    bool Enabled,
    int Devices,
    bool WebPushEnabled = true,
    bool EmailEnabled = false,
    bool EmailAddressConfigured = false) {
    /// <summary>
    /// 获取一个值，指示现在发过去对方能不能收到
    /// </summary>
    public bool CanReceive => Enabled
        && ((WebPushEnabled && Devices > 0) || (EmailEnabled && EmailAddressConfigured));

    /// <summary>
    /// 表示还没有另一个账号
    /// </summary>
    public static readonly PartnerReadiness None = new(false, 0);
}

/// <summary>
/// 一条通知没发出去的原因，够具体才能让人知道下一步该做什么
/// </summary>
public enum PushFailureReason {
    /// <summary>
    /// 没有失败
    /// </summary>
    None = 0,

    /// <summary>
    /// 设备上的订阅已经失效，记录也清掉了
    /// </summary>
    Expired = 1,

    /// <summary>
    /// 推送网关没收下这一条
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// 服务器连不上推送网关
    /// </summary>
    Unreachable = 3,

    /// <summary>
    /// 推送网关不认本站的身份，站点配置要修
    /// </summary>
    Unauthorized = 4
}

/// <summary>
/// 一次投递的结果，后台的「通知测试」拿它给人看个交代
/// </summary>
/// <param name="Sent">成功送达了几台设备</param>
/// <param name="Failed">失败了几台</param>
/// <param name="Dropped">因为订阅已失效而被清掉了几台</param>
/// <param name="Reason">这次失败里最要紧的那个原因</param>
public sealed record PushDeliveryResult(
    int Sent,
    int Failed,
    int Dropped,
    PushFailureReason Reason = PushFailureReason.None) {
    /// <summary>
    /// 获取这次一共动了几台设备
    /// </summary>
    public int Total => Sent + Failed + Dropped;

    /// <summary>
    /// 表示一台设备都没动过
    /// </summary>
    public static readonly PushDeliveryResult Empty = new(0, 0, 0);
}

/// <summary>
/// 邮件投递失败的安全分类，不包含服务器地址、账号或凭据
/// </summary>
public enum EmailFailureReason {
    /// <summary>
    /// 没有失败
    /// </summary>
    None = 0,

    /// <summary>
    /// SMTP 或收件人配置不完整
    /// </summary>
    NotConfigured = 1,

    /// <summary>
    /// 无法连接 SMTP 服务
    /// </summary>
    ConnectionFailed = 2,

    /// <summary>
    /// SMTP 认证失败
    /// </summary>
    AuthenticationFailed = 3,

    /// <summary>
    /// 连接与认证成功，但邮件未被服务器接收
    /// </summary>
    SendFailed = 4
}

/// <summary>
/// 一次邮件渠道投递的结果
/// </summary>
public sealed record EmailDeliveryResult(
    int Sent,
    int Failed,
    EmailFailureReason Reason = EmailFailureReason.None) {
    /// <summary>
    /// 获取本次处理的收件人数
    /// </summary>
    public int Total => Sent + Failed;

    /// <summary>
    /// 表示没有处理任何收件人
    /// </summary>
    public static readonly EmailDeliveryResult Empty = new(0, 0);
}

/// <summary>
/// 一次通知在所有渠道上的汇总结果
/// </summary>
public sealed record NotificationDeliveryResult(
    PushDeliveryResult WebPush,
    EmailDeliveryResult Email) {
    /// <summary>
    /// 兼容原 Web Push 调用方：成功送达的设备数
    /// </summary>
    public int Sent => WebPush.Sent;

    /// <summary>
    /// 兼容原 Web Push 调用方：失败的设备数
    /// </summary>
    public int Failed => WebPush.Failed;

    /// <summary>
    /// 兼容原 Web Push 调用方：清理的过期设备数
    /// </summary>
    public int Dropped => WebPush.Dropped;

    /// <summary>
    /// 兼容原 Web Push 调用方：最主要的 Push 失败原因
    /// </summary>
    public PushFailureReason Reason => WebPush.Reason;

    /// <summary>
    /// 获取所有渠道本次处理的目标数
    /// </summary>
    public int Total => WebPush.Total + Email.Total;

    /// <summary>
    /// 表示没有任何渠道处理目标
    /// </summary>
    public static readonly NotificationDeliveryResult Empty = new(PushDeliveryResult.Empty, EmailDeliveryResult.Empty);
}

/// <summary>
/// 通知偏好的可编辑视图，后台表单和服务之间传的就是它
/// </summary>
public sealed class NotificationPreferences {
    /// <summary>
    /// 获取或设置通知服务的总开关
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 获取或设置是否通过 Web Push 接收通知
    /// </summary>
    public bool WebPushEnabled { get; set; } = true;

    /// <summary>
    /// 获取或设置是否通过 Email 接收通知
    /// </summary>
    public bool EmailEnabled { get; set; }

    /// <summary>
    /// 获取或设置当前账号接收通知的邮箱地址
    /// </summary>
    public string EmailAddress { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否接收点点滴滴的通知
    /// </summary>
    public bool Moments { get; set; } = true;

    /// <summary>
    /// 获取或设置是否接收纪念日的通知
    /// </summary>
    public bool Anniversaries { get; set; } = true;

    /// <summary>
    /// 获取或设置是否接收心意商城的通知
    /// </summary>
    public bool Shop { get; set; } = true;

    /// <summary>
    /// 获取或设置对方点了想你时要不要提醒我
    /// </summary>
    public bool MissYou { get; set; } = true;

    /// <summary>
    /// 获取或设置点点滴滴下面来了新留言时要不要提醒我
    /// </summary>
    public bool Comments { get; set; } = true;

    /// <summary>
    /// 获取或设置是否接收心有灵犀答题提醒
    /// </summary>
    public bool Affinity { get; set; } = true;

    /// <summary>
    /// 获取或设置纪念日提醒时刻，从零点算起的分钟数
    /// </summary>
    public int RemindMinutes { get; set; } = NotificationSetting.DefaultRemindMinutes;

    /// <summary>
    /// 从实体读出一份可编辑的副本
    /// </summary>
    public static NotificationPreferences From(NotificationSetting setting) {
        ArgumentNullException.ThrowIfNull(setting);

        return new NotificationPreferences {
            Enabled = setting.Enabled,
            WebPushEnabled = setting.WebPushEnabled,
            EmailEnabled = setting.EmailEnabled,
            EmailAddress = setting.EmailAddress,
            Moments = setting.Moments,
            Anniversaries = setting.Anniversaries,
            Shop = setting.Shop,
            MissYou = setting.MissYou,
            Comments = setting.Comments,
            Affinity = setting.Affinity,
            RemindMinutes = setting.RemindMinutes
        };
    }
}
