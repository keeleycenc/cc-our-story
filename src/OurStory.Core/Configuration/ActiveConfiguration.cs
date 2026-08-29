// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Helpers;
using OurStory.Core.Options;
using System.Text.Json;

namespace OurStory.Core.Configuration;

/// <summary>
/// 提供当前生效的站点配置
/// </summary>
public sealed class ActiveConfiguration(ConfigurationStore store, OurStoryConfiguration current) {
    private readonly ConfigurationStore _store = store;
    private readonly Lock _gate = new();
    private OurStoryConfiguration _current = current;

    /// <summary>
    /// 获取整份配置
    /// </summary>
    public OurStoryConfiguration Current => Volatile.Read(ref _current);

    /// <summary>
    /// 获取站点运行参数
    /// </summary>
    public SiteOptions Site => Current.Site;

    /// <summary>
    /// 获取附件存储参数
    /// </summary>
    public StorageOptions Storage => Current.Storage;

    /// <summary>
    /// 获取 SMTP 邮件通知参数
    /// </summary>
    public EmailOptions Email => Current.Email;

    /// <summary>
    /// 获取 LLM 氛围组参数
    /// </summary>
    public LlmAtmosphereOptions LlmAtmosphere => Current.LlmAtmosphere;

    /// <summary>
    /// 获取花信小结的模型通道参数
    /// </summary>
    public CycleInsightOptions CycleInsight => Current.CycleInsight;

    /// <summary>
    /// 获取配置文件的完整路径，后台页面上会显示出来
    /// </summary>
    public string FilePath => _store.FilePath;

    /// <summary>
    /// 改一处配置并写回文件
    /// </summary>
    /// <param name="edit">在配置的副本上改，改完才整体换过去</param>
    /// <param name="error">写不进去时的原因</param>
    /// <returns>落盘成功返回 <c>true</c></returns>
    public bool Update(Action<OurStoryConfiguration> edit, out string? error) {
        lock (_gate) {
            var next = Clone(Current);
            edit(next);

            if (!_store.TrySave(next, out error)) {
                return false;
            }

            Volatile.Write(ref _current, next);
            return true;
        }
    }

    /// <summary>
    /// 通过序列化完成深拷贝，新增配置节点时无需同步修改此方法
    /// </summary>
    private static OurStoryConfiguration Clone(OurStoryConfiguration configuration) =>
        JsonSerializer.Deserialize<OurStoryConfiguration>(
            JsonSerializer.Serialize(configuration, JsonFile.Options),
            JsonFile.Options) ?? new OurStoryConfiguration();
}
