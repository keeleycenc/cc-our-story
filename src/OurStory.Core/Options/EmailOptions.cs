// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Net.Mail;

namespace OurStory.Core.Options;

/// <summary>
/// SMTP 连接采用的加密方式
/// </summary>
public enum EmailSecurity {
    /// <summary>
    /// 不加密，通常只用于可信内网中的 SMTP 服务
    /// </summary>
    None = 0,

    /// <summary>
    /// 先建立普通连接，再用 STARTTLS 升级为加密连接
    /// </summary>
    StartTls = 1,

    /// <summary>
    /// 连接建立时立即使用 SSL/TLS
    /// </summary>
    SslTls = 2
}

/// <summary>
/// 配置文件里的 <c>Email</c> 节点：SMTP 服务与发件人配置
/// </summary>
public class EmailOptions {
    /// <summary>
    /// 获取或设置站点是否提供邮件通知
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 获取或设置 SMTP 主机名
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 SMTP 端口
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// 获取或设置 SMTP 加密方式
    /// </summary>
    public EmailSecurity Security { get; set; } = EmailSecurity.StartTls;

    /// <summary>
    /// 获取或设置 SMTP 用户名；留空表示不认证
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 SMTP 密码或邮箱授权码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置邮件的 From 地址
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置发件人显示名称
    /// </summary>
    public string SenderName { get; set; } = "Our Story";

    /// <summary>
    /// 获取或设置站点公开地址，用来把邮件中的站内相对链接转换成绝对地址
    /// </summary>
    public string SiteBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取一个值，指示 SMTP 发送参数是否完整且合法
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && Port is > 0 and <= 65535
        && IsValidAddress(SenderEmail)
        && IsValidSiteBaseUrl(SiteBaseUrl)
        && (string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password));

    /// <summary>
    /// 只在表单确实提供新密码时替换旧值；后台密码框留空时调用此方法不会清除凭据。
    /// </summary>
    public void SetPasswordIfProvided(string? password) {
        if (!string.IsNullOrWhiteSpace(password)) {
            Password = password;
        }
    }

    /// <summary>
    /// 判断字符串是否是可用的单个邮件地址
    /// </summary>
    public static bool IsValidAddress(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        try {
            var address = new MailAddress(value.Trim());
            return string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
        } catch (FormatException) {
            return false;
        }
    }

    /// <summary>
    /// 判断字符串是否是可用于邮件链接的 HTTP(S) 站点根地址
    /// </summary>
    public static bool IsValidSiteBaseUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
