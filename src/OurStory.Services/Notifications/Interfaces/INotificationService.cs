// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Entities;
using OurStory.Core.Models;

namespace OurStory.Services.Notifications;

/// <summary>
/// 通知服务：管设备、管偏好、真正把通知发出去
/// </summary>
public interface INotificationService {
    /// <summary>
    /// 获取一个值，指示 VAPID 密钥是否已经就绪；没有它整套通知都用不了
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// 获取要交给浏览器的 VAPID 公钥
    /// </summary>
    string PublicKey { get; }

    /// <summary>
    /// 异步获取某个人的通知偏好，没有记录时按默认值现建一条
    /// </summary>
    /// <param name="userId">用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，这个人的通知偏好</returns>
    Task<NotificationSetting> GetSettingAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步保存某个人的通知偏好
    /// </summary>
    /// <param name="userId">用户编号</param>
    /// <param name="preferences">要保存的偏好</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveSettingAsync(int userId, NotificationPreferences preferences, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步登记一台设备；同一个推送地址来第二次时只更新，不新增
    /// </summary>
    /// <param name="userId">设备属于谁</param>
    /// <param name="registration">浏览器订阅成功后交上来的凭据</param>
    /// <param name="siteOrigin">站点自己的地址，用来补全没填过的 VAPID 联系方式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，登记好的设备</returns>
    Task<PushDevice> RegisterDeviceAsync(
        int userId,
        PushDeviceRegistration registration,
        string? siteOrigin = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步注销一台设备
    /// </summary>
    /// <param name="userId">设备属于谁，防止删到对方的设备</param>
    /// <param name="endpoint">要注销的推送地址</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，删掉了返回 true</returns>
    Task<bool> RemoveDeviceAsync(int userId, string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步按编号注销一台设备，后台的设备列表用它
    /// </summary>
    /// <param name="userId">设备属于谁</param>
    /// <param name="deviceId">设备编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，删掉了返回 true</returns>
    Task<bool> RemoveDeviceAsync(int userId, long deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步列出某个人已经授权的所有设备
    /// </summary>
    /// <param name="userId">用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，设备列表，最近授权的排在前面</returns>
    Task<IReadOnlyList<PushDeviceCard>> GetDevicesAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步把一条通知发出去，该发给谁由请求本身决定
    /// </summary>
    /// <param name="request">投递请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，这次投递的成绩</returns>
    Task<PushDeliveryResult> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步找出另一个人的用户编号；只有两行用户，所以「对方」是确定的
    /// </summary>
    /// <param name="userId">自己的用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，对方的用户编号；只有一个账号时返回 null</returns>
    Task<int?> GetPartnerIdAsync(int userId, CancellationToken cancellationToken = default);
}
