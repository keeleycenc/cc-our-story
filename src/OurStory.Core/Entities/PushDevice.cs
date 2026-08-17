// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Entities;

/// <summary>
/// 一台已经授权接收通知的设备，对应浏览器里的一份 PushSubscription
/// </summary>
/// <remarks>
/// 一个人可以有很多台：手机、平板、家里的电脑各算一条
/// </remarks>
public class PushDevice {
    /// <summary>
    /// 获取或设置唯一标识
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 获取或设置这台设备是谁的，只会是男主或女主
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 获取或设置设备归属的用户
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// 获取或设置推送服务给的投递地址
    /// </summary>
    /// <remarks>
    /// 每台设备一个，全表唯一：同一台设备重新授权会拿到同一个地址
    /// </remarks>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置设备的公钥，base64url 的 65 字节未压缩 P-256 点
    /// </summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置设备的认证密钥，base64url 的 16 字节随机串
    /// </summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置设备在列表里的名字，从 User-Agent 猜出来的
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置首次授权时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置最近一次带着这份订阅来打招呼的时间
    /// </summary>
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置最近一次成功投递的时间；从没发出去过为 null
    /// </summary>
    public DateTimeOffset? LastPushedAt { get; set; }

    /// <summary>
    /// 获取或设置连续投递失败的次数
    /// </summary>
    /// <remarks>
    /// 推送服务明确说「这个订阅没了」（404 / 410）时直接删掉这一行；
    /// 这个计数管的是网络超时之类说不清的失败，攒够了也当作没了
    /// </remarks>
    public int FailureCount { get; set; }
}
