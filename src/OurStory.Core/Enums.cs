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
    DailyVisit = 5,

    /// <summary>
    /// 完成一道心有灵犀每日题目
    /// </summary>
    AffinityAnswer = 6,

    /// <summary>
    /// 今天正好是某个纪念日，两个人各得一份
    /// </summary>
    AnniversaryDay = 7
}

/// <summary>
/// 心有灵犀题型。每日题目保存该值的快照
/// </summary>
public enum AffinityQuestionType {
    /// <summary>
    /// 单选题
    /// </summary>
    SingleChoice = 0,

    /// <summary>
    /// 多选题
    /// </summary>
    MultipleChoice = 1,

    /// <summary>
    /// 开放题
    /// </summary>
    OpenEnded = 2
}

/// <summary>
/// 浏览器订阅在服务端的归属状态
/// </summary>
/// <remarks>
/// 区分两种非当前用户场景：
/// - Other：同一浏览器切换了账号，订阅仍属于其他账号
/// - Unknown：服务端无此订阅记录（数据被清理或设备已移除）
/// </remarks>
public enum PushDeviceOwnership {
    /// <summary>
    /// 服务端无记录，无法确定归属
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 归属当前登录用户
    /// </summary>
    Mine = 1,

    /// <summary>
    /// 归属其他账号
    /// </summary>
    Other = 2
}

/// <summary>
/// 一条通知属于哪一类，决定它要不要看收件人后台勾的那几项
/// </summary>
public enum NotificationTopic {
    /// <summary>
    /// 对方发布了新的点点滴滴
    /// </summary>
    Moment = 0,

    /// <summary>
    /// 纪念日：对方新记下一个日子，或者今天 / 明天就是某个日子
    /// </summary>
    Anniversary = 1,

    /// <summary>
    /// 心意商城：上架、兑换、待确认、已完成
    /// </summary>
    Shop = 2,

    /// <summary>
    /// 想你：对方在首页点了想你
    /// </summary>
    MissYou = 3,

    /// <summary>
    /// 留言：点点滴滴
    /// </summary>
    Comment = 4,

    /// <summary>
    /// 对方手动发来的一句话，不受上面几项开关影响
    /// </summary>
    Direct = 5,

    /// <summary>
    /// 后台点的「通知测试」，只发给自己，同样不受那几项开关影响
    /// </summary>
    Test = 6,

    /// <summary>
    /// 心有灵犀：对方完成了今日回答
    /// </summary>
    Affinity = 7
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

/// <summary>
/// 一条留言是谁写的
/// </summary>
public enum CommentSource {
    /// <summary>
    /// 访客
    /// </summary>
    Guest = 0,

    /// <summary>
    /// 男主或女主
    /// </summary>
    Owner = 1,

    /// <summary>
    /// LLM 氛围组
    /// </summary>
    LlmAtmosphere = 2
}
