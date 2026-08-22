// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
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
    /// 获取一个值，指示 SMTP 邮件通知是否已经启用并配置完整
    /// </summary>
    bool IsEmailConfigured { get; }

    /// <summary>
    /// 获取一个值，指示至少有一个通知渠道可用
    /// </summary>
    bool HasConfiguredChannel { get; }

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
    /// 异步查询指定推送订阅在服务端的归属
    /// </summary>
    /// <remarks>
    /// 浏览器仅知晓本地是否存在订阅，无法判断归属。需区分三种结果：
    /// - Mine：归当前用户
    /// - Other：归其他账号
    /// - Unknown：服务端无记录（数据被清理或设备已移除）
    /// </remarks>
    /// <param name="userId">用户标识</param>
    /// <param name="endpoint">推送端点地址</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>订阅归属状态</returns>
    Task<PushDeviceOwnership> GetOwnershipAsync(int userId, string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步预检目标用户的接收就绪状态
    /// </summary>
    /// <remarks>
    /// 在构建并发送消息载荷之前，预先验证对方是否具备接收条件（通知开关状态及可用设备数）。
    /// 此检查旨在将发送失败的可能性前置，避免用户在完成消息编辑并触发发送操作后，才因对方不可达而导致操作失败
    /// </remarks>
    /// <param name="userId">当前发起请求的用户标识</param>
    /// <param name="cancellationToken">用于取消异步操作的取消令牌</param>
    /// <returns>异步操作任务结果，包含目标用户通知启用状态及当前活跃设备数量的任务结果</returns>
    Task<PartnerReadiness> GetPartnerReadinessAsync(int userId, CancellationToken cancellationToken = default);

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
    Task<NotificationDeliveryResult> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用当前 SMTP 配置向当前账号填写的地址发送测试邮件；不受个人渠道开关影响
    /// </summary>
    Task<EmailDeliveryResult> SendTestEmailAsync(
        int userId,
        string emailAddress,
        string? siteOrigin = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步找出另一个人的用户编号；只有两行用户，所以「对方」是确定的
    /// </summary>
    /// <param name="userId">自己的用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，对方的用户编号；只有一个账号时返回 null</returns>
    Task<int?> GetPartnerIdAsync(int userId, CancellationToken cancellationToken = default);
}
