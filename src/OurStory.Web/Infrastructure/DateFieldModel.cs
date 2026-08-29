// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Globalization;

namespace OurStory.Web.Infrastructure;

/// <summary>全站共用的日期／时间选择器配置</summary>
/// <param name="FieldName">表单字段名，直接对应绑定的属性路径</param>
/// <param name="Label">字段标题</param>
/// <param name="Value">已格式化的控件值</param>
/// <param name="WithTime">是否连同时间一起选</param>
/// <param name="Required">是否必填</param>
/// <param name="Hint">标题下方的补充说明</param>
/// <param name="Min">可选的最早日期，已完成格式化</param>
/// <param name="Max">可选的最晚日期，已完成格式化</param>
/// <param name="Id">指定的 DOM 标识；同一字段名在页面中多次出现时用于区分控件</param>
public sealed record DateFieldModel(
    string FieldName,
    string Label,
    string Value,
    bool WithTime = false,
    bool Required = false,
    string? Hint = null,
    string? Min = null,
    string? Max = null,
    string? Id = null) {
    /// <summary>
    /// 日期控件使用的格式
    /// </summary>
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// 获取可用于标签关联的稳定 DOM 编号
    /// </summary>
    public string ElementId => Id ?? FieldName.Replace('.', '_').Replace('[', '_').Replace(']', '_');

    /// <summary>
    /// 获取原生控件类型
    /// </summary>
    public string InputType => WithTime ? "datetime-local" : "date";

    /// <summary>
    /// 获取快捷按钮上的文字
    /// </summary>
    public string NowLabel => WithTime ? "此刻" : "今天";

    /// <summary>
    /// 使用日期创建配置
    /// </summary>
    public static DateFieldModel Date(string fieldName, string label, DateOnly value, bool required = false, string? hint = null) =>
        new(fieldName, label, value.ToString(DateFormat, CultureInfo.InvariantCulture), false, required, hint);

    /// <summary>
    /// 使用日期时间创建精确到天的配置
    /// </summary>
    public static DateFieldModel Date(string fieldName, string label, DateTime value, bool required = false, string? hint = null) =>
        new(fieldName, label, value.ToString(DateFormat, CultureInfo.InvariantCulture), false, required, hint);

    /// <summary>
    /// 使用日期时间创建精确到分钟的配置
    /// </summary>
    public static DateFieldModel Moment(string fieldName, string label, DateTime value, bool required = false, string? hint = null) =>
        new(fieldName, label, value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture), true, required, hint);

    /// <summary>
    /// 创建不可选择未来日期且允许留空的配置
    /// </summary>
    /// <param name="fieldName">表单字段名</param>
    /// <param name="label">字段标题</param>
    /// <param name="value">当前值；留空时控件为空</param>
    /// <param name="today">允许选到的最后一天</param>
    /// <param name="required">是否必填</param>
    /// <param name="hint">标题下方的补充说明</param>
    /// <param name="id">强制指定的 DOM 编号</param>
    /// <returns>已设置日期上限的选择器配置</returns>
    public static DateFieldModel Past(
        string fieldName,
        string label,
        DateOnly? value,
        DateOnly today,
        bool required = false,
        string? hint = null,
        string? id = null) =>
        new(
            fieldName,
            label,
            value?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty,
            false,
            required,
            hint,
            null,
            today.ToString(DateFormat, CultureInfo.InvariantCulture),
            id);
}
