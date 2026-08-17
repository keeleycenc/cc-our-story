// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Services.Notifications;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 启动时保证 VAPID 密钥就位
/// </summary>
public static class PushKeyBootstrap {
    /// <summary>
    /// 没有密钥就现生成一对写进配置文件
    /// </summary>
    /// <remarks>
    /// 这对密钥就是浏览器眼里的「这个站点」。生成一次就一直用下去：
    /// 换掉它等于让所有设备上的授权集体作废，人人都得重新点一次开启通知。
    /// 所以只在真的空着时才生成，写失败也只是当次跑不了推送，站点照常起
    /// </remarks>
    public static void EnsurePushKeys(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        var configuration = app.Services.GetRequiredService<ActiveConfiguration>();
        if (configuration.Current.Push.IsConfigured) {
            return;
        }

        var (publicKey, privateKey) = VapidKeys.Create();

        if (configuration.Update(
            next => {
                next.Push.PublicKey = publicKey;
                next.Push.PrivateKey = privateKey;
            },
            out var error)) {
            app.Logger.LogInformation("已生成通知用的 VAPID 密钥，写进了 {Path}。", configuration.FilePath);
            return;
        }

        app.Logger.LogWarning(
            "通知服务需要一对 VAPID 密钥，但 {Path} 无法写入（{Error}），本次会话不会收到推送",
            configuration.FilePath,
            error);
    }
}
