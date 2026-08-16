// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core;

/// <summary>
/// 站点里的三种身份。用于描述「当前请求是谁发来的」
/// </summary>
public enum UserRole {
    /// <summary>
    /// 获取或设置 Guest
    /// </summary>
    Guest = 0,
    /// <summary>
    /// 获取或设置 Boy
    /// </summary>
    Boy = 1,
    /// <summary>
    /// 获取或设置 Girl
    /// </summary>
    Girl = 2
}

/// <summary>
/// 点点滴滴的发布状态
/// </summary>
public enum MomentStatus {
    /// <summary>
    /// 草稿状态
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 已发布状态
    /// </summary>
    Published = 1
}

/// <summary>
/// 纪念日的视觉与语义分类
/// </summary>
public enum AnniversaryKind {
    /// <summary>
    /// 两个人关系中的重要日子
    /// </summary>
    Love = 0,

    /// <summary>
    /// 生日
    /// </summary>
    Birthday = 1,

    /// <summary>
    /// 共同完成的里程碑
    /// </summary>
    Milestone = 2,

    /// <summary>
    /// 其它值得记住的日子
    /// </summary>
    Custom = 3,

    /// <summary>
    /// 第一次见面或初次相遇
    /// </summary>
    FirstMeeting = 4,

    /// <summary>
    /// 一起完成的旅行
    /// </summary>
    Travel = 5,

    /// <summary>同庆祝的节日
    /// </summary>
    Festival = 6,

    /// <summary>
    /// 两个人之间的约定
    /// </summary>
    Promise = 7,

    /// <summary>
    /// 家庭相关的重要日期
    /// </summary>
    Family = 8,

    /// <summary>
    /// 结婚纪念日
    /// </summary>
    Wedding = 9
}

/// <summary>
/// 纪念日遵循的历法
/// </summary>
public enum AnniversaryCalendarType {
    /// <summary>
    /// 公历（阳历）
    /// </summary>
    Solar = 0,

    /// <summary>
    /// 中国农历
    /// </summary>
    Lunar = 1
}

/// <summary>
/// 表示一条心意流水来源
/// </summary>
public enum HeartPointReason {
    /// <summary>
    /// 当天第一次想你
    /// </summary>
    DailyHeartbeat = 0,

    /// <summary>
    /// 当天第一次发布点点滴滴
    /// </summary>
    MomentPublished = 1,

    /// <summary>
    /// 当天第一次发布纪念日
    /// </summary>
    AnniversaryPublished = 2,

    /// <summary>
    /// 兑换心愿，心意直接销毁
    /// </summary>
    Purchase = 3,

    /// <summary>
    /// 当天把想你点满
    /// </summary>
    DailyHeartbeatFull = 4,

    /// <summary>
    /// 当天第一次来看看
    /// </summary>
    DailyVisit = 5
}

/// <summary>
/// 表示心愿的使用确认方式
/// </summary>
public enum ShopRedeemMode {
    /// <summary>
    /// 持有人发起使用，由发布者确认完成后核销。
    /// 适用于洗碗券、做饭券等需要对方履行的心愿。
    /// </summary>
    MutualConfirm = 0,

    /// <summary>
    /// 持有人确认使用后立即核销。
    /// 适用于「今晚我选电影」等无需对方确认的心愿。
    /// </summary>
    Instant = 1
}

/// <summary>
/// 心愿商品状态
/// </summary>
public enum ShopItemStatus {
    /// <summary>
    /// 已上架，等待兑换
    /// </summary>
    Listed = 0,

    /// <summary>
    /// 已兑换，等待使用
    /// </summary>
    Redeemed = 1,

    /// <summary>
    /// 已发起使用，等待发布者确认
    /// </summary>
    PendingConfirm = 2,

    /// <summary>
    /// 已使用，终态
    /// </summary>
    Used = 3,

    /// <summary>
    /// 上架有效期已结束且未被兑换，终态
    /// </summary>
    ListingExpired = 4,

    /// <summary>
    /// 兑换后未在有效期内使用，终态
    /// </summary>
    Expired = 5
}
