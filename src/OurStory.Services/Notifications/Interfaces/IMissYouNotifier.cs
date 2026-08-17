// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.Notifications;

/// <summary>
/// 「想你」消息的聚合发送器
/// </summary>
public interface IMissYouNotifier {
    /// <summary>
    /// 记录本次点击，并推迟消息发送时间
    /// </summary>
    /// <param name="userId">点击者的用户 ID</param>
    /// <param name="displayName">点击者在站内的显示名称，会直接写入通知内容</param>
    /// <param name="taps">本次点击的次数</param>
    void Record(int userId, string displayName, int taps);
}
