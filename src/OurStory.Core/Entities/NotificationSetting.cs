// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Entities;

/// <summary>
/// 一个人的通知偏好，一行对应一个账号
/// </summary>
/// <remarks>
/// 通知是很私人的事：想收什么、几点提醒，两个人各管各的
/// </remarks>
public class NotificationSetting {
    /// <summary>
    /// 默认的每日提醒时间：晚上九点
    /// </summary>
    public const int DefaultRemindMinutes = 21 * 60;

    /// <summary>
    /// 获取或设置偏好归属的用户编号，同时也是主键
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 获取或设置偏好归属的用户
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// 获取或设置通知服务的总开关，关掉之后除了「通知测试」什么都不发
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
    /// 获取或设置每日提醒的时刻，从当天零点算起的分钟数，按站点时区理解
    /// </summary>
    /// <remarks>
    /// 「每日想你」和「今天 / 明天有纪念日」这两条到点的提醒共用它，
    /// 后台只用填一个时间
    /// </remarks>
    public int RemindMinutes { get; set; } = DefaultRemindMinutes;

    /// <summary>
    /// 获取或设置最近一次发出每日想你提醒的日期，形如 <c>2026-08-17</c>
    /// </summary>
    /// <remarks>
    /// 定时那一头每分钟醒一次，靠这个日期确保一天只发一条；
    /// 站点重启也不会重复打扰
    /// </remarks>
    public string LastDailyMissOn { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置最近一次发出纪念日提醒的日期，形如 <c>2026-08-17</c>
    /// </summary>
    public string LastAnniversaryOn { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置最后修改时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 这一类通知要不要发给这个人
    /// </summary>
    /// <remarks>
    /// 「对方发来的一句话」和「通知测试」不看那四个勾：
    /// 前者是人主动按下发送键，后者本来就是用来确认通知通不通的
    /// </remarks>
    public bool Allows(NotificationTopic topic) => topic switch {
        NotificationTopic.Test => true,
        NotificationTopic.Direct => Enabled,
        NotificationTopic.Moment => Enabled && Moments,
        NotificationTopic.Anniversary => Enabled && Anniversaries,
        NotificationTopic.Shop => Enabled && Shop,
        NotificationTopic.DailyMiss => Enabled && DailyMiss,
        _ => false
    };
}
