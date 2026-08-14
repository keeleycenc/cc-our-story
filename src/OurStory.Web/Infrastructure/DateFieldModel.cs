// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Globalization;

namespace OurStory.Web.Infrastructure;

/// <summary>后台共用的日期／时间选择器配置</summary>
/// <param name="FieldName">表单字段名，直接对应绑定的属性路径</param>
/// <param name="Label">字段标题</param>
/// <param name="Value">已经格式化好的控件值</param>
/// <param name="WithTime">是否连同时间一起选</param>
/// <param name="Required">是否必填</param>
/// <param name="Hint">标题下方的补充说明</param>
public sealed record DateFieldModel(
    string FieldName,
    string Label,
    string Value,
    bool WithTime = false,
    bool Required = false,
    string? Hint = null) {
    /// <summary>
    /// 获取可用于标签关联的稳定 DOM 编号
    /// </summary>
    public string Id => FieldName.Replace('.', '_').Replace('[', '_').Replace(']', '_');

    /// <summary>
    /// 获取原生控件类型
    /// </summary>
    public string InputType => WithTime ? "datetime-local" : "date";

    /// <summary>
    /// 获取快捷按钮上的文字
    /// </summary>
    public string NowLabel => WithTime ? "此刻" : "今天";

    /// <summary>
    /// 用一个日期建立配置
    /// </summary>
    public static DateFieldModel Date(string fieldName, string label, DateOnly value, bool required = false, string? hint = null) =>
        new(fieldName, label, value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), false, required, hint);

    /// <summary>
    /// 用一个日期时间建立只到天的配置
    /// </summary>
    public static DateFieldModel Date(string fieldName, string label, DateTime value, bool required = false, string? hint = null) =>
        new(fieldName, label, value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), false, required, hint);

    /// <summary>
    /// 用一个日期时间建立精确到分钟的配置
    /// </summary>
    public static DateFieldModel Moment(string fieldName, string label, DateTime value, bool required = false, string? hint = null) =>
        new(fieldName, label, value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture), true, required, hint);
}
