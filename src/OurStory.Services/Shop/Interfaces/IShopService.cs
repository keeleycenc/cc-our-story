// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Entities;
using OurStory.Core.Models;

namespace OurStory.Services.Shop;

/// <summary>
/// 心意商城服务接口
/// </summary>
public interface IShopService {
    /// <summary>
    /// 异步获取商城列表
    /// </summary>
    /// <param name="query">筛选条件</param>
    /// <param name="viewer">谁在看，访客看不到「仅双方可见」的心愿</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，一页心愿卡片</returns>
    Task<PagedList<ShopItemCard>> GetPageAsync(ShopQuery query, ShopViewer viewer, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步找出某件心愿落在商城列表的第几页
    /// </summary>
    /// <param name="itemId">心愿 ID</param>
    /// <param name="query">筛选条件，页码不参与计算</param>
    /// <param name="viewer">谁在看</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，页码从 1 开始；这件心愿不在筛选结果里时返回 0</returns>
    Task<int> FindPageAsync(int itemId, ShopQuery query, ShopViewer viewer, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取某个人心愿仓库里的东西，也就是他兑换到手的那些
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，能用的排在前面</returns>
    Task<IReadOnlyList<ShopItemCard>> GetWarehouseAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取要由某个人去履约的心愿，也就是他发布、又被对方兑换走的那些
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，等着确认的排在最前面</returns>
    Task<IReadOnlyList<ShopItemCard>> GetPromisesAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步统计上架中的心愿数量
    /// </summary>
    /// <param name="viewer">谁在看</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，上架中的件数</returns>
    Task<int> CountOnSaleAsync(ShopViewer viewer, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步发布一件心愿
    /// </summary>
    /// <param name="model">发布数据</param>
    /// <param name="sellerId">发布者的用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，成功与否和一句提示</returns>
    Task<ShopActionResult> PublishAsync(ShopPublishModel model, int sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步兑换一件心愿，心意直接销毁，不转给发布者
    /// </summary>
    /// <param name="itemId">心愿 ID</param>
    /// <param name="buyerId">兑换者的用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，成功与否和一句提示</returns>
    Task<ShopActionResult> PurchaseAsync(int itemId, int buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步由持有人发起使用
    /// </summary>
    /// <remarks>
    /// 立即使用的当场就记成已使用；双方确认的转成待履约，等发布者点头
    /// </remarks>
    /// <param name="itemId">心愿 ID</param>
    /// <param name="userId">持有人的用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，成功与否和一句提示</returns>
    Task<ShopActionResult> RequestRedeemAsync(int itemId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步由发布者确认履约，确认完就是终态
    /// </summary>
    /// <param name="itemId">心愿 ID</param>
    /// <param name="userId">发布者的用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，成功与否和一句提示</returns>
    Task<ShopActionResult> ConfirmRedeemAsync(int itemId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步撤回一次核销申请，退回到「可以使用」
    /// </summary>
    /// <remarks>持有人自己撤回，或者发布者觉得还没做完，两边都能按</remarks>
    /// <param name="itemId">心愿 ID</param>
    /// <param name="userId">操作人的用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，成功与否和一句提示</returns>
    Task<ShopActionResult> CancelRedeemAsync(int itemId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步把到期的心愿改成对应的终态
    /// </summary>
    /// <remarks>
    /// 没有后台任务，列表页读之前顺手扫一遍就够了
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，这一轮改了几条</returns>
    Task<int> SweepExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取心愿预设
    /// </summary>
    /// <param name="activeOnly">只要还在用的那些</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，按排序值排好的预设</returns>
    Task<IReadOnlyList<ShopPreset>> GetPresetsAsync(bool activeOnly, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步新建一个心愿预设
    /// </summary>
    /// <param name="model">预设数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，新建好的预设</returns>
    Task<ShopPreset> CreatePresetAsync(ShopPresetEditModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步启用或停用一个心愿预设
    /// </summary>
    /// <param name="id">预设 ID</param>
    /// <param name="isActive">启用还是停用</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，改成功返回 true</returns>
    Task<bool> SetPresetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除一个心愿预设
    /// </summary>
    /// <remarks>预设只是发布时的模板，删掉它不影响已经发出去的心愿</remarks>
    /// <param name="id">预设 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，删成功返回 true</returns>
    Task<bool> DeletePresetAsync(int id, CancellationToken cancellationToken = default);
}
