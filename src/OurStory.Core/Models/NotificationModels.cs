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
public sealed record NotificationRequest(
    NotificationTopic Topic,
    PushMessage Message,
    int? TargetUserId = null,
    int? ExceptUserId = null) {

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
/// 浏览器订阅成功后交上来的那份凭据
/// </summary>
/// <param name="Endpoint">推送服务给的投递地址</param>
/// <param name="P256dh">设备公钥，base64url</param>
/// <param name="Auth">设备认证密钥，base64url</param>
/// <param name="UserAgent">浏览器的 User-Agent，用来给设备起个看得懂的名字</param>
public sealed record PushDeviceRegistration(string Endpoint, string P256dh, string Auth, string? UserAgent);

/// <summary>
/// 后台设备列表里的一行
/// </summary>
/// <param name="Id">设备编号</param>
/// <param name="Name">设备名</param>
/// <param name="CreatedAt">首次授权时间，站点时区</param>
/// <param name="LastPushedAt">最近一次收到通知的时间，站点时区；从没收到过为 null</param>
public sealed record PushDeviceCard(
    long Id,
    string Name,
    DateTime CreatedAt,
    DateTime? LastPushedAt);

/// <summary>
/// 一次投递的结果，后台的「通知测试」拿它给人看个交代
/// </summary>
/// <param name="Sent">成功送达了几台设备</param>
/// <param name="Failed">失败了几台</param>
/// <param name="Dropped">因为订阅已失效而被清掉了几台</param>
public sealed record PushDeliveryResult(int Sent, int Failed, int Dropped) {
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
/// 通知偏好的可编辑视图，后台表单和服务之间传的就是它
/// </summary>
public sealed class NotificationPreferences {
    /// <summary>
    /// 获取或设置通知服务的总开关
    /// </summary>
    public bool Enabled { get; set; }

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
    /// 获取或设置是否接收每日想你的提醒
    /// </summary>
    public bool DailyMiss { get; set; } = true;

    /// <summary>
    /// 获取或设置每日提醒时刻，从零点算起的分钟数
    /// </summary>
    public int RemindMinutes { get; set; } = NotificationSetting.DefaultRemindMinutes;

    /// <summary>
    /// 从实体读出一份可编辑的副本
    /// </summary>
    public static NotificationPreferences From(NotificationSetting setting) {
        ArgumentNullException.ThrowIfNull(setting);

        return new NotificationPreferences {
            Enabled = setting.Enabled,
            Moments = setting.Moments,
            Anniversaries = setting.Anniversaries,
            Shop = setting.Shop,
            DailyMiss = setting.DailyMiss,
            RemindMinutes = setting.RemindMinutes
        };
    }
}
