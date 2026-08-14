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
