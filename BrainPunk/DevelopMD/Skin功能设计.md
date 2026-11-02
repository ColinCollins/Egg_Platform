---
type: feature-design
version: 2.1.3
status: draft
created: 2026-06-16
tags:
  - Skin
  - ShopPanel
  - Actor
  - ResourceSystem
---

# Skin 功能设计文档

## 概述

Skin 功能负责在 ShopPanel 中展示、解锁、保存和预览玩家皮肤，并在关卡运行时把玩家当前保存的皮肤选择绑定到主 Actor 外观。

当前功能分为两条边界：

- ShopPanel Skin：负责皮肤目录、虚拟滚动列表、所有权、解锁、应用/清除、场景 Boxer 预览。
- Actor Skin Binding：负责主玩家 Actor 在关卡中读取已保存的皮肤选择，并应用全身 Spine 皮肤与 back/face/body 部件 prefab。

本文只记录已在当前工作区代码或规格中能直接找到的事实。未经过 Unity PlayMode、设备或资源包验证的内容统一放在“待验证项”中。

## 目标与背景

- 业务目标：玩家在 Skin 页选择或解锁皮肤后，ShopPanel 预览和关卡主角外观使用同一组当前选择数据。
- 用户场景：玩家在 ShopPanel 中浏览皮肤、通过默认/广告/金币/内购等方式获得皮肤，点击已拥有皮肤应用，再进入关卡或在 Actor 已激活时刷新外观。
- 现状：Skin 功能已覆盖 UI 列表、解锁、预览、资源加载边界和 Actor 运行时绑定；仍需要 PlayMode、Android/iOS 和资源包验证。

## 功能范围

### 包含

- 从 `Config.Skin` / `Config.SkinShop` 构建 Skin 列表。
- 使用 `InfiniteScrollView` 复用有限数量的 `SkinItem` 槽位。
- SkinItem 展示名称、品质背景、图标、选中状态、解锁状态。
- 默认皮肤所有权迁移到 `DB.GameData.OwnedSkin`。
- 已拥有皮肤点击应用；再次点击当前皮肤清除对应保存字段。
- 广告、金币、内购三类皮肤解锁。
- ShopPanel 场景 Boxer 预览，包括全身皮肤和 back/face/body prefab 部件。
- 主 Actor 运行时绑定当前保存的全身皮肤与部件 prefab。
- ShopPanel apply/clear 后通过事件通知已激活 Actor 刷新。

### 不包含

- 普通 Shop 商品、`TbShop.Rewards` 发奖、恢复购买、售罄状态、金币飞行动画。这些仍由 `ShopPanel` / `ShopItem` 负责。
- 新增存档字段、新增 RemoteConfig、新增平台 SDK 流程、新增资源打包规则。
- 自动给 NPC、helper、特殊 Actor 应用玩家皮肤；非主 Actor 需要显式 opt-in。

## 关键数据

### Config.Skin

`Config.Skin` 是 Luban 生成的只读配置类，字段包括：

| 字段 | 用途 |
| --- | --- |
| `Id` | 皮肤配置 id，也可作为旧保存值匹配键 |
| `SkinName` | UI 展示名称 |
| `SkinPart` | 部位：FullBody / Back / Face / Body |
| `TextureResources` | SkinItem 列表图标资源键 |
| `PrefabResource` | Boxer 预览和 Actor 部件 prefab 资源键 |
| `SpineResource` | Spine skin 名称 |
| `UnlockType` | 解锁方式 |
| `Quality` | 品质背景索引 |
| `UnlockHint` | 特殊/广告等提示文本 |
| `CoinPrice` | 金币解锁价格 |
| `ProductId` | 内购商品 id |

证据：`Assets/Configs/Code/Skin.cs:20` - `Assets/Configs/Code/Skin.cs:81`。

### GameData

当前 Skin 存档字段：

| 字段 | 用途 |
| --- | --- |
| `OwnedSkin` | 已拥有皮肤 id 列表 |
| `CurrentBackPart` | 当前背部部件保存值 |
| `CurrentFacePart` | 当前脸部部件保存值 |
| `CurrentBodyPart` | 当前身体/衣服部件保存值 |
| `CurrentSkinId` | 当前全身皮肤保存值 |

证据：`Assets/Game/DB/GameData_Partial.cs:233` - `Assets/Game/DB/GameData_Partial.cs:260`。

### 保存值匹配规则

当前 ShopPanel 新写入的选择值使用 `skin.Id`；读取现有选择时统一兼容按 `Id`、`PrefabResource` 或 `SpineResource` 匹配同部位 Skin 行。

证据：

- 写入 key：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs` 的 `ResolvePartSkinKey(...)`。
- 读取兼容：`Assets/Game/Scripts/Skins/SkinUtils.cs` 的 `ResolveSavedSkin(...)`、`SavedValueMatchesSkin(...)` 和 `ResolveFullBodySkinName(...)`。

2026-07-20 复核：未使用且优先返回 `PrefabResource` 的旧 `ActorSkinSelectionResolver.ResolvePartSkinKey(...)` 已随 resolver 删除；当前写入策略保持 `skin.Id`，三键读取只用于兼容历史存档。

## ShopPanel Skin 模块

### 职责边界

当前 ShopPanel Skin 运行时按以下边界协作：

- 构建 `Config.Skin` / `Config.SkinShop` 目录并喂给 `InfiniteScrollView`。
- 从 `DB.GameData` 刷新所有权和选中状态。
- 处理广告、金币、内购皮肤解锁。
- `ShopPanel_SkinPart` 合成保存状态与临时试穿状态，并把有效皮肤值交给 `ShopPanelBoxerPreviewController`。
- `ShopPanelBoxerPreviewController` 控制场景 `Skin` 预览 root、RenderTexture 相机、Spine 动作和 back/face/body 预览 prefab；它不读取 DB、不处理购买、不派发事件。

非职责：普通 ShopPanel 商品、共享商店奖励、恢复购买、售罄状态、金币奖励动画。

证据：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:18` - `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:30`。

### 生命周期

| 时机 | 行为 |
| --- | --- |
| `Awake` | 解析引用，关闭 Boxer 预览，隐藏操作按钮 |
| `OnEnable` | 绑定按钮/筛选组，迁移默认皮肤所有权；当前 ShopPanel 首次进入 Skin 页时初始化预览，同一面板内再次进入时恢复保留的临时试穿与预览部件，按筛选刷新列表 |
| `OnDisable` | 解绑事件并清空滚动数据，只挂起场景预览 root/相机；保留当前 ShopPanel 内的临时试穿、每部位选中项、预览部件和未完成加载 |
| `ShopPanel.OnClose` | 结束预览会话，清理临时试穿、每部位选中项和预览部件，使未完成加载失效并隐藏场景预览 |
| `OnDestroy` | 兜底结束预览会话并释放控制器 prefab 缓存 |

证据：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:104` - `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:146`。

### 列表与槽位复用

`InfiniteScrollView` 是 ShopPanel Skin 的有限槽位 ScrollRect 适配器：

- 通过 `SetData(itemCount, bindSlot, resetToBeginning)` 设置数据数量和绑定回调。
- 根据 viewport 高度、列数和 buffer 计算槽位容量。
- 只保留可见区域和 buffer 所需的 `SkinItem` 槽位。
- 原 `SkinItem` 强类型入口、Restore prefab selector 和引导查询保持不变；组件同时提供
  `SetComponentItemPrefab(...)` / `SetComponentData(...)`，允许
  `PencilSkinItem` 等独立 `MonoBehaviour` Item 复用同一套槽位和滚动生命周期。
- 通用入口要求业务控制器显式提供 Item prefab、完整绑定回调和清理回调；
  `InfiniteScrollView` 不读取业务表、不判断购买状态，也不把通用 Item 转成 `SkinItem`。
- `OnValidate` 只修正本地序列化尺寸/间距/列数/buffer，不访问 DB、配置表或资源系统。

证据：`Assets/Game/UI/ShopPanel/Scripts/InfiniteScrollView.cs:15` - `Assets/Game/UI/ShopPanel/Scripts/InfiniteScrollView.cs:125`，`Assets/Game/UI/ShopPanel/Scripts/InfiniteScrollView.cs:135` - `Assets/Game/UI/ShopPanel/Scripts/InfiniteScrollView.cs:183`。

`SkinItem` 负责单个槽位绑定：

- `Bind(...)` 重置旧状态后写入标题、品质背景、图标、选中状态、解锁状态和交互状态。
- 图标从 `TextureResources` 解析为 `Assets/Game/ArtSrc/Skins/.../*.png` 候选路径，并先用 `ResourceManager.CanLoad<Sprite>()` 校验。
- 使用 `_iconLoadVersion` 防止复用槽位时旧异步图标回写到新数据。

证据：`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs:89` - `Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs:137`，`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs:181` - `Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs:263`。

### 所有权与应用/清除

- `EnsureDefaultOwnedSkins()` 会把 `SkinUnlockType.Default` 行加入 `OwnedSkin`，变更后保存。
- 点击已拥有皮肤时，如果该皮肤已经是当前选择，则清除对应 current 字段；否则写入对应 current 字段。
- back/face/body 写入 `CurrentBackPart` / `CurrentFacePart` / `CurrentBodyPart`。
- 其他部位写入 `CurrentSkinId`。
- apply/clear 保存后分发 `ActorSkinSelectionChangedEvent`。

证据：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:344` - `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:373`，`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:409` - `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:431`，`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:586` - `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:665`。

### 解锁流程

| 解锁方式 | 当前行为 |
| --- | --- |
| Default | 运行时加入 `OwnedSkin`，作为已拥有项处理 |
| Ad | 调 `RewardAdHelper.TryToShowRewardAd(RewardPlacement.ShopReward.ToString())`，成功后 `GrantSkin` |
| Coin | 先检查 `PlayCtrl.Instance?.Bag` 是否拥有 `GameProps.UnlimitCoin`；有无限金币时不检查/扣除普通金币并直接 `GrantSkin`，否则检查 `GameProps.Coin`，不足提示 `U_HurryTips_NeedCoin`，足够则 `RemoveTool(..., CostType.Other)` 后 `GrantSkin` |
| Purchase | 通过 `Products.GetProductId(skin.ProductId)` 解析商品，发送 `IAP_CLK`，调用 `Purchase.Purchase(productId, "skin", callback)` |

Skin 内购成功只授予 Skin 自身，不走 `PurchaseManager.OnPurchaseSuccess(...)` 的 `TbShop.Rewards` 发奖路径。

证据：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:439` - `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:550`，`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:557` - `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:584`。

### ShopPanel 预览

`ShopPanel_SkinPart.ApplyCurrentSkinToViewer(...)` 只负责从 `DB.GameData` 与临时试穿字典合成 Back、Face、Body、FullBody 有效值。`ShopPanelBoxerPreviewController` 接收这些值后控制场景 root `Skin`、`SkinRt_Camera`、`Boxer_Rt` RawImage、RenderTexture、`ActorSpineCtrl` 和部件挂点。

全身皮肤通过场景预览 `ActorSpineCtrl.SetSkin(...)` 更新；部件 prefab 通过 `PrefabResource` 加载后挂到 back/face/body。Back、Body、FullBody、Face 的一次性预览动作播放后，会在 0 号轨道排入循环 `idle`，由 Spine 在动作自然结束时自动回到待机。生产 `ShopPanel.prefab` 的原 `skinViewer` 字段为空，prefab 内也没有 `UISpineCtrl` 组件，因此 2026-07-20 整理时移除了未生效的 UI Spine 兼容分支。

证据：

- 有效皮肤值合成与转发：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs` 的 `ApplyCurrentSkinToViewer(...)`。
- 场景预览应用与生命周期：`Assets/Game/UI/ShopPanel/Scripts/ShopPanelBoxerPreviewController.cs`。

## 资源加载边界

Skin 当前依赖 `Bear.ResourceSystem.ResourceManager`：

- UI 图标：`TextureResources` -> `Sprite`。
- Boxer / Actor 部件：`PrefabResource` -> `GameObject` prefab。
- bare prefab key 会解析为 `Assets/Game/Prefabs/Skins/{PrefabResource}.prefab`。
- 调用 `LoadAsync<T>()` 前先调用 `CanLoad<T>()` 过滤无效资源。
- Skin 业务层不直接调用 YooAsset API；YooAsset 校验留在 Bear resource-system adapter 内。

证据：

- SkinItem 图标：`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs:181` - `Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs:263`。
- ShopPanel prefab 预览：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:1112` - `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:1235`。
- Actor prefab 绑定：`Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:191` - `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:241`。
- 资源路径解析：`Assets/Game/Scripts/Skins/SkinUtils.cs` 的 `ResolveIconLocation(...)` 与 `ResolvePrefabLocation(...)`。

## Actor Skin Binding

### 职责

`ActorSkinBinding` 是运行时组件，负责在 Actor 激活时读取 `DB.GameData` 当前 Skin 选择，并应用到 Actor 外观：

- 全身皮肤通过 `ActorSpineCtrl.SetSkin(...)`。
- back/face/body 通过 prefab 实例挂载到对应 mount。
- 空或无效字段清理对应部件；全身皮肤回退到 `fallbackFullBodySkinName`。
- 监听 `ActorSkinSelectionChangedEvent`，在 ShopPanel apply/clear 后刷新。

证据：`Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:41` - `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:147`。

### 主 Actor 资格

默认 `BindingMode.MainPlayerOnly` 会比较本地 `ActorCtrl` 是否等于 `FindFirstObjectByType<BaseLevelCtrl>()?.Actor`。`BindingMode.ExplicitOptIn` 用于显式允许非主 Actor 绑定玩家皮肤。

证据：`Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:244` - `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:258`。

### 运行时生命周期保护

`ActorSkinBinding` 的 `Awake`、`OnEnable`、`Start`、`OnDisable`、`OnDestroy`、`RefreshFromCurrentSelection` 都在入口处检查 `Application.isPlaying`，避免编辑器态触发 DB/config/resource/instantiate 链。

证据：`Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:41` - `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:117`。

### 部件实例生命周期

- 每次应用部件前先 `ClearPart(part)`，避免旧实例重复存在。
- 异步加载后检查 `this == null`、`isActiveAndEnabled` 和 `applyVersion`，避免禁用、销毁或后续刷新后的旧结果回写。
- `OnDisable` / `OnDestroy` 增加 `_applyVersion`、清理事件订阅并销毁已拥有部件实例。

证据：`Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:78` - `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:99`，`Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:168` - `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:241`，`Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:283` - `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:304`。

### Actor prefab 现状

`Assets/Game/Prefabs/LevelItems/Actor/Actor.prefab` 已包含 `ActorSkinBinding`，并序列化了 fallback skin 与 back/face/body mount 引用。

证据：`Assets/Game/Prefabs/LevelItems/Actor/Actor.prefab:716` - `Assets/Game/Prefabs/LevelItems/Actor/Actor.prefab:719`。

`Actor_infiniteJump.prefab` 当前没有在本次整理中确认到同等 back/face/body mount 绑定证据；是否需要接入应以后续 prefab 结构和关卡用途验证为准。

## 事件

`ActorSkinSelectionChangedEvent` 是 Bear EventSystem 事件，字段为：

| 字段 | 含义 |
| --- | --- |
| `ChangedPart` | 变化的 SkinPart |
| `Reason` | Applied / Cleared / GrantedAndApplied / Refresh |
| `Version` | ShopPanel 侧递增版本 |

ShopPanel 当前在 apply/clear 保存后分发事件。Actor 绑定组件订阅该事件并刷新当前保存选择。

证据：

- 事件定义：`Assets/Game/Scripts/Level/Actor/ActorSkinSelectionChangedEvent.cs:6` - `Assets/Game/Scripts/Level/Actor/ActorSkinSelectionChangedEvent.cs:19`。
- ShopPanel 分发：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:662` - `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs:665`。
- Actor 订阅：`Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:119` - `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs:128`。

## 风险与约束

### 已确认约束

- SkinItem 和 ShopPanel_SkinPart 使用 `Bear.ResourceSystem.ResourceManager`，不直接依赖 YooAsset。
- ActorSkinBinding 运行时入口有 `Application.isPlaying` 保护。
- ActorSkinBinding 默认只绑定主 Actor，非主 Actor 需要显式 opt-in。
- 部件 prefab 刷新会先清旧实例，并用版本号拦截过期异步结果。
- ShopPanel Skin 的普通商品、恢复购买、售罄和金币动画仍不属于 SkinPart。

### 待验证项

- Unity PlayMode：ShopPanel Skin 打开、筛选、滚动复用、图标加载、选中动画、close/reopen 清理。
- Unity PlayMode：默认皮肤迁移、已拥有点击应用/清除、广告/金币/内购解锁回调。
- Unity PlayMode：场景 `Skin` root、`SkinRt_Camera`、`Boxer_Rt`、RenderTexture 与 back/face/body 挂点是否都绑定到生产 prefab/scene 的正确对象。
- Unity PlayMode：Actor.prefab 关卡入口 full-body 和 back/face/body 部件显示、重复进入/重启/失败/退出时无重复实例。
- Unity PlayMode：Level64 类 helper/custom Actor 不会在未 opt-in 时收到玩家皮肤。
- Android/iOS 横屏：ShopPanel Skin 滚动、按钮点击、广告/内购 SDK 回调、Actor 刷新时无明显卡顿。
- 资源包验证：生产 `GamePackage` 中所有 `TextureResources` 和 `PrefabResource` 均可由 Bear ResourceSystem 加载。
- 配置完成度：当前 `tbskin.json` / `#Skin.xlsx` 曾存在 `prefab1_*`、`prefab2_*`、`prefab3_*` 等 legacy 值，需确认最终配置已改为有效 prefab resource。

## 相关文件

| 文件 | 说明 |
| --- | --- |
| `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs` | Skin 页主控制器 |
| `Assets/Game/UI/ShopPanel/Scripts/ShopPanelBoxerPreviewController.cs` | ShopPanel 场景 Boxer 预览、资源与动画生命周期控制器 |
| `Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs` | 列表单项绑定 |
| `Assets/Game/UI/ShopPanel/Scripts/InfiniteScrollView.cs` | 有限槽位滚动适配器 |
| `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs` | Actor 运行时皮肤绑定 |
| `Assets/Game/Scripts/Skins/SkinUtils.cs` | Skin 配置、保存兼容与资源读取公共入口 |
| `Assets/Game/Scripts/Level/Actor/ActorSkinSelectionChangedEvent.cs` | 当前 Skin 选择变更事件 |
| `Assets/Game/Scripts/Level/ActorSpineCtrl.cs` | Actor Spine 皮肤应用 |
| `Assets/Game/DB/GameData_Partial.cs` | Skin 存档字段 |
| `Assets/Configs/Code/Skin.cs` | Luban 生成 Skin 配置类 |
| `Assets/Game/Prefabs/LevelItems/Actor/Actor.prefab` | 主 Actor prefab 绑定点 |
| `Assets/Game/Prefabs/Skins/` | 部件 prefab 资源目录 |
| `Assets/Game/ArtSrc/Skins/` | SkinItem 图标资源目录 |
| `DevelopMD/已有功能设计与使用记录.md` | 按日期记录的功能事实与待验证项 |

## 修改记录

| 版本 | 日期 | 修改人 | 修改说明 |
| --- | --- | --- | --- |
| 1.0.0 | 2026-06-16 | Codex | 初版整理 Skin 功能职责、数据、流程、资源边界、Actor 绑定与待验证项 |
| 1.1.0 | 2026-06-17 | Codex | 补充 ShopPanel Skin 列表排序与 ownedToggle 拥有筛选行为 |
| 1.2.0 | 2026-06-22 | Codex | 补充 Skin 部件 prefab 自动生成工具说明 |
| 2.0.0 | 2026-06-23 | Codex | 重新按当前代码结构复核 SkinPart v2.0 状态需求，补充 Restore 行、点击试穿不落存档、SkinItem State enum 与当前差距 |
| 2.0.1 | 2026-06-24 | Codex | 补充 SkinPart 金币皮肤解锁对 `UnlimitCoin` 无限金币状态的处理 |
| 2.0.2 | 2026-06-24 | Codex | 补充 ShopPanel SkinPart 外部入口指定 Body Type 筛选行为 |
| 2.0.3 | 2026-07-03 | Codex | 补充 `SkinUtils` 资源读取整合记录 |
| 2.1.0 | 2026-07-20 | Codex | 拆分 `ShopPanelBoxerPreviewController`，移除无引用入口与未生效 UISpine 兼容分支 |
| 2.1.1 | 2026-07-20 | Codex | 保存兼容解析和资源入口收敛到 `SkinUtils`，删除冗余 resolver 与 ShopPanel 重复实现 |
| 2.1.2 | 2026-07-22 | Codex | Skin 页 Disable 改为挂起并保留 preview session，仅在 ShopPanel 关闭或销毁时清理 |
| 2.1.3 | 2026-07-30 | Codex | InfiniteScrollView 增加独立 MonoBehaviour Item 兼容入口，保留 SkinItem 旧接口与引导边界 |

## 2026-07-03 SkinUtils 资源读取整合

### 需求背景

本次分析 `ActorSkinBinding`、`SkinItem`、`ShopPanel_SkinPart` 的 Skin 资源读取逻辑，并把可复用的路径解析、可加载性检查和异步加载入口集中到 `Assets/Game/Scripts/Skins/SkinUtils.cs`。

### 影响范围

- `SkinUtils` 现在统一提供 `ResolveIconLocation(Skin)`、`ResolvePrefabLocation(string)`、`CanLoadIconLocation(...)`、`LoadIconAsync(...)`、`CanLoadPrefabLocation(...)` 和 `LoadPrefabAsync(...)`。
- `SkinItem` 和 `GetSkinPopup` 的 UI 图标加载改为通过 `SkinUtils`，但仍各自保留异步版本号、UI 回写和生命周期保护。
- `ShopPanel_SkinPart` 和 `ActorSkinBinding` 的部件 prefab 加载改为通过 `SkinUtils`，但仍各自保留缓存、取消令牌、版本保护、实例化和挂点处理。
- `ActorSkinSelectionResolver` 仍负责保存值到 Skin 行的匹配，只把 prefab 路径规则转发到 `SkinUtils`。

### 行为边界

- 图标短名仍解析为 `Assets/Game/ArtSrc/Skins/UI/{TextureResources}.png`；完整 `Assets/` 路径仍原样使用。
- 部件 prefab 短名仍解析为 `Assets/Game/Prefabs/Skins/{PrefabResource}.prefab`；完整 `Assets/` 路径仍原样使用。
- 本次没有新增或修改资源、资源包规则、存档字段、`Save()`、事件派发、广告、IAP、analytics、RemoteConfig 或平台 SDK。
- `SkinUtils` 不持有 UI 状态、试穿状态、选中状态、Boxer 预览缓存或 Actor 实例生命周期。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Skins/SkinUtils.cs Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs Assets/Game/Scripts/Level/Actor/ActorSkinSelectionResolver.cs` 通过，仅有工作区 LF/CRLF 转换提示。
- 静态扫描确认重复的图标路径常量、prefab 路径常量和本地 `ResourceManager` Skin 资源读取入口已收敛到 `SkinUtils`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:quiet /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 97 warnings。

### 未验证风险

- Unity PlayMode 尚未验证 ShopPanel Skin 列表图标、GetSkinPopup 图标、Boxer 预览和关卡 Actor 部件应用。
- Android/iOS 横屏 smoke 尚未验证快速滚动、切换皮肤、弹窗打开和关卡进入时的资源加载表现。
- 资源包完整性仍待验证，尤其是所有 `TextureResources` 与 `PrefabResource` 是否都能由 Bear `ResourceManager` 加载。

### 回滚路径

回滚 `SkinUtils` 的资源读取公共入口，并恢复各调用方原本的本地路径解析、`CanLoad` 与 `LoadAsync` 逻辑，即可恢复整合前行为。

## 2026-07-20 Skin 保存解析与资源入口完整收敛

### 需求与原行为基线

- `ActorSkinBinding` 原通过 `ActorSkinSelectionResolver` 解析 FullBody、部件配置和 prefab 路径，其中 prefab 路径只是转发到 `SkinUtils`。
- `ShopPanelBoxerPreviewController` 另有一份相同的 Skin 表遍历、三键匹配和 FullBody 名称解析；`ShopPanel_SkinPart` 也重复实现三键匹配。
- resolver 中 `ResolvePartSkinKey(...)` 没有调用方，整个类只有 `ActorSkinBinding` 使用。

### 修改后行为与影响范围

- `SkinUtils` 统一提供 `ResolveSavedSkin(...)`、`ResolveFullBodySkinName(...)`、`SavedValueMatchesSkin(...)` 和原有资源读取 API。
- Actor 与 Boxer 预览改用统一解析；ShopPanel 主控制器删除重复匹配方法，但保留本地 `skin.Id` 写入策略。
- 删除 `ActorSkinSelectionResolver.cs/.meta`，不迁移无调用方的旧 `ResolvePartSkinKey(...)`。
- 玩家可见外观、历史存档三键兼容、资源路径、Bear `ResourceManager`、取消令牌、异步版本、实例挂点、DB/save 和事件流保持原有行为。

### 验证、风险与回滚

- 静态扫描确认 resolver 引用和三类重复私有解析无残留，`git diff --check` 通过；C# 工程编译通过，结果为 0 error / 99 warnings。
- Unity PlayMode、资源包和 Android/iOS 横屏未执行，ShopPanel 选中态、Boxer 预览和 Actor 各部位实际加载仍属于待验证项。
- 回滚时恢复 resolver 及两个 ShopPanel 本地解析实现，并改回原调用即可，不需要修改资源、配置、prefab 或存档。

## 2026-06-24 SkinPart 外部入口指定筛选

### 需求背景

`GetSkinPopup` 跳转到 ShopPanel Skin 页时，需要直接切到 Body Type。为避免外部入口直接依赖 `ShopPanel_SkinPart` 私有筛选实现，ShopPanel 侧补充一个带 `SkinPart` 参数的创建入口，SkinPart 侧补充公开筛选 API。

### 影响范围

- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs`：新增 `CreateSkin(SkinPart targetSkinPart, ...)`，旧 `CreateSkin()` 保持原语义。
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`：新增 `SelectSkinPart(SkinPart)`，并让 `ShowAll/ShowBack/ShowFace/ShowBody` 复用该入口。
- `Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs`：黄色按钮跳转指定 `SkinPart.Body`。

### 行为边界

- `SelectSkinPart(SkinPart)` 只同步 Skin 页内筛选按钮和列表数据，不授予皮肤、不试穿、不写入存档、不派发 Actor skin 事件。
- `SkinPart.Body` 与既有筛选按钮组 index `2` 对应；如果筛选按钮组不可用，会直接刷新 Body 列表作为降级路径。
- 普通 ShopPanel 商品、普通 `ShopPanel.Create()`、旧 `ShopPanel.CreateSkin()`、Skin 购买/广告/金币/内购、资源加载和 Actor 关卡绑定均不属于本次行为变更。

### 验证记录

- `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs` 通过，仅提示当前工作区 LF/CRLF 转换。
- 新增 diff 风险词扫描未发现本次新增行包含 `DB`、`Save()`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、广告、内购或 analytics 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 152 warnings；warnings 属于当前工程既有范围。

### 未验证风险

- Unity PlayMode 尚未验证 `GetSkinPopup` 黄色按钮跳转后 ShopPanel 是否实际选中 Body Type toggle。
- Android/iOS 横屏尚未验证该跳转和筛选切换在触摸路径下无可见异常。

### 回滚路径

回滚 `GetSkinPopup.OpenSkinPanel(...)` 指定 Body 的调用，并删除 `ShopPanel.CreateSkin(SkinPart, ...)`、`ShopPanel.SelectSkinPart(SkinPart)`、`ShopPanel_SkinPart.SelectSkinPart(SkinPart)` 及对应 index helper，即可恢复为只打开 Skin 大页的旧入口行为。

## 2026-06-23 SkinPart version 2.0 状态需求复核

### 需求背景

`ShopPanel_SkinPart.cs` 顶部已有 `version 2.0` 注释，要求两件事：

- `RefreshItem()` 时默认添加一个 `SkinItem_Revert`，默认排在第一位，State 为 Restore，只负责清理当前 `SkinPart` 数据。
- 点击未拥有皮肤后，Boxer 直接试穿对应皮肤，但不因该次点击改变 `GameData` 数据；已拥有皮肤点击仍保持当前立即应用并保存的基线，其他购买逻辑不变。

`SkinItem.cs` 顶部已有 `version 3.0` 注释和 `SkinItemState` enum，但当前 enum 只声明在脚本中，还没有被 `Bind(...)`、`Clear()`、按钮点击或 `ShopPanel_SkinPart.BindSkinItem(...)` 接入。

### 当前代码事实

- `ShopPanel_SkinPart.RefreshItem()` 当前执行 `RebuildCatalog()`、`EnsureDefaultOwnedSkins()`、`RefreshSelectedSkinFromSave()`，然后把 `_skins.Count` 交给 `InfiniteScrollView.SetData(...)`；当前 `_skins` 类型是 `List<Config.Skin>`，没有承载非配置行的数据结构。
- `RebuildCatalog()` 当前只从 `TbSkinShop.DataList` 或 `TbSkin.DataList` 收集 `Config.Skin` 行，并按拥有状态、品质和来源顺序排序；没有插入 `SkinItem_Revert` 或 Restore 状态行。
- `BindSkinItem(...)` 当前按索引取得 `_skins[index]`，计算 `owned` 与 `selected` 后调用 `SkinItem.Bind(skin, selected, owned, OnSkinItemClicked, ...)`；没有给 `SkinItem` 传递状态 enum。
- `OnSkinItemClicked(...)` 当前点击已拥有且非当前皮肤时会调用 `ApplyOwnedSkin(_selectedSkin, true)`，也就是立即写入 `GameData` 并保存；2026-06-23 澄清后，这属于需要保留的已拥有点击基线。
- `ClearCurrentSkinPart(SkinPart part)` 已存在公开入口，但当前会先按传入 part 取保存字段，再固定用 `SkinPart.FullBody` 调 `ResolveSavedSkin(...)`，因此用于 back/face/body Restore 时存在解析不到正确部位 Skin 的风险。
- `ApplyCurrentSkinToViewer()` 当前只从 `DB.GameData` 的 `CurrentBackPart`、`CurrentFacePart`、`CurrentBodyPart` 和 `CurrentSkinId` 刷新 Boxer 预览；没有独立的临时试穿状态。
- `SkinItem.SkinItemState` 当前包含 `None`、`Resort`、`Selected`。2026-06-23 澄清后，`Resort` 按拼写错误处理，后续实现应迁移为规范 `Restore`，不保留为独立状态；当前 enum 仍未与 `SkinItem_Revert.prefab` 或点击清理逻辑建立绑定。
- `Assets/Game/UI/ShopPanel/Prefabs/SkinItem_Revert.prefab` 当前存在，`ShopPanel.prefab` 的 `skinItemPrefab` 仍指向 `SkinItem.prefab`，没有确认到 `ShopPanel_SkinPart` 对 `SkinItem_Revert` 的序列化引用。
- `GameData` 当前 Skin 保存字段为 `OwnedSkin`、`CurrentBackPart`、`CurrentFacePart`、`CurrentBodyPart` 和 `CurrentSkinId`；旧文档中 `CurrentSkinName` 已不符合当前代码结构。

### v2.0 目标行为

- Skin 列表的数据模型需要能区分配置 Skin 行和 Restore 特殊行，不能继续只用 `List<Config.Skin>` 表达所有可见项。
- 每次按 SkinPart 过滤刷新时，Restore 特殊行必须稳定出现在第一位，不参与普通 Skin 行排序，也不依赖 `TbSkin` 或 `TbSkinShop` 中存在一条伪配置。
- `SkinItem` 的 State enum 需要成为绑定和点击路由的一部分，规范值为 `None`、`Normal`、`Selected`、`Preview`、`Restore`；当前 `Resort` 只作为拼写错误迁移到 `Restore`，不作为独立兼容状态保留。
- Restore 行点击只清理当前筛选部位对应的保存字段和同部位临时试穿状态：Back -> `CurrentBackPart`，Face -> `CurrentFacePart`，Body -> `CurrentBodyPart`，FullBody -> `CurrentSkinId`；不得修改 `OwnedSkin`、广告/金币/内购状态或普通 Shop 恢复购买逻辑。
- 未拥有的 `Ad`、`Coin`、`Purchase`、`Special` SkinItem 点击应先驱动 Boxer 临时试穿，不因点击本身保存到 `GameData`；`Unavailable` 保持不可交互且不试穿；已拥有 SkinItem 点击保持立即应用并保存。后续如果通过购买、广告或金币解锁成功，仍按当前 `GrantSkin -> ApplyOwnedSkin(..., true)` 基线授予所有权并应用保存。
- Boxer 预览需要能读取“临时试穿状态 + 已保存状态”的组合：临时状态只影响 ShopPanel 预览，不能影响关卡 Actor 绑定；切换筛选时保留其他部位临时试穿，Restore 清理当前部位临时状态，关闭 ShopPanel 时清理全部临时试穿状态。
- `ActorSkinBinding` 继续只读取 `GameData` 保存字段，不读取 ShopPanel 临时试穿状态。

### 当前差距与风险

- P1：点击未拥有的 `Ad`、`Coin`、`Purchase`、`Special` 皮肤当前只显示操作按钮或提示，尚未实现“先试穿但不改 GameData”；`Unavailable` 当前不可交互且需要保持不试穿；点击已拥有皮肤立即保存是需要保留的基线。
- P1：Restore 行尚未插入 `_skins`/滚动数据源，`SkinItem_Revert.prefab` 只是资源存在，未被当前列表绑定路径确认使用。
- P1：`SkinItemState` enum 尚未驱动 `SkinItem` 绑定、视觉重置或点击路由；复用槽位仍只按 `Config.Skin + selected + owned` 绑定，且当前 `Resort` 需要迁移为规范 `Restore`。
- P1：`ClearCurrentSkinPart(SkinPart part)` 对 back/face/body 当前保存值使用 `SkinPart.FullBody` 解析，后续直接用于 Restore 行会有清理失败风险。
- P2：`ShopPanel_SkinPart.ResolvePartSkinKey(...)` 当前返回 `skin.Id`，而 `ActorSkinSelectionResolver.ResolvePartSkinKey(...)` 优先返回 `PrefabResource`；当前 Actor 仍可通过三键匹配兜底，但两端语义不一致会增加后续整理成本。
- P2：当前 `ApplyCurrentSkinToViewer()` 只读保存字段，没有临时试穿层；快速点击、切换筛选、关闭面板时需要版本号或等效保护避免异步预览回写旧状态；切换筛选需保留其他部位临时试穿，关闭 ShopPanel 需清理全部临时试穿。

### 影响范围

- 计划改动运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`、`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs`，必要时扩展 `InfiniteScrollView` 的绑定数据但不改变其滚动职责。
- 计划使用资源：`Assets/Game/UI/ShopPanel/Prefabs/SkinItem_Revert.prefab`。
- 需要复核 prefab：`Assets/Game/UI/ShopPanel/Resources/ShopPanel.prefab` 是否增加 Restore prefab 引用或是否通过代码路径加载/查找。
- 不计划修改：Luban Skin 配置结构、`GameData` 存档 schema、普通 ShopPanel 商品、Shop 恢复购买、广告/内购 SDK、analytics、YooAsset/热更配置、关卡 prefab 和 Actor gameplay 逻辑。

### 验证建议

- 静态检查：确认 `SkinItemState` 规范值为 `None`、`Normal`、`Selected`、`Preview`、`Restore`，并被 `Bind`、`Clear`、点击路由和 Restore 行绑定路径使用；确认没有新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`PlayerPrefs` 或平台 SDK 直连。
- 编译：运行 `dotnet build .\Assembly-CSharp.csproj --no-restore`。
- PlayMode：验证四个筛选下 Restore 行均第一位，点击只清对应保存字段和同部位临时试穿状态；点击未拥有的 `Ad`、`Coin`、`Purchase`、`Special` 皮肤只改变 Boxer 预览不改 `GameData`；切换筛选时其他部位临时试穿仍可保留在 ShopPanel 预览组合中；`Unavailable` 行不可交互且不试穿；点击已拥有皮肤仍立即应用保存；广告、金币、内购解锁成功后仍授予所有权并应用保存；关闭 ShopPanel 后清理全部临时试穿且不影响 Actor 进关卡表现。
- 移动端横屏：验证快速滚动、快速点击、筛选切换、关闭重开时无明显卡顿、重叠或 stale preview。

### 回滚路径

如果 v2.0 状态需求实现失败或表现不符合预期，回滚 `ShopPanel_SkinPart` 的 Restore 行数据源、临时试穿状态和保存路由调整，回滚 `SkinItem` 的 State enum 接入与 Restore 绑定，恢复为当前点击已拥有皮肤直接保存的旧流程，并删除或停用 `SkinItem_Revert` 关联引用。

## 2026-06-22 Skin 部件 prefab 自动生成工具

### 需求背景

当前 Skin 运行时使用 `Config.Skin.PrefabResource` 作为 boxer 预览和 Actor 部件 prefab 的资源键，并把裸资源名解析到 `Assets/Game/Prefabs/Skins/{PrefabResource}.prefab`。为减少手动制作部件 prefab 的重复操作，新增 Unity Editor 菜单工具从 `Assets/Game/ArtSrc/Skins/Back`、`Clothes`、`Face` 三个目录扫描 Sprite，生成同名 prefab 到 `Assets/Game/Prefabs/Skins`。

### 影响范围

- 新增编辑器脚本：`Assets/Editor/SkinsPrefabGenerator.cs`。
- 菜单入口：`Tools/Bear/Auto Generate Skins Prefabs`。
- 输入资源目录：`Assets/Game/ArtSrc/Skins/Back`、`Assets/Game/ArtSrc/Skins/Clothes`、`Assets/Game/ArtSrc/Skins/Face`。
- 输出资源目录：`Assets/Game/Prefabs/Skins`。
- 不修改 `Config.Skin`、Luban 表、存档字段、Actor 运行时代码、资源加载器、关卡 prefab、平台 SDK 或分析打点。2026-06-24 后，ShopPanel 运行时代码只负责在 SkinPart 预览换装实例上临时切换 layer。

### 生成行为

- 每个可加载的 Sprite 生成一个同名 prefab，目标路径为 `Assets/Game/Prefabs/Skins/{SpriteName}.prefab`。
- prefab 结构参考现有 Skin 部件 prefab：根节点同名，根节点下一个同名子节点，子节点挂 `SpriteRenderer` 并引用源 Sprite。
- 自动生成的根节点和子节点不再默认设置 `Ignore Raycast` layer，保持 Unity 新建对象默认 layer；只有 `ShopPanel_SkinPart` 预览换装实例会在运行时递归切换到 `Ignore Raycast` layer（数值 2）。
- 子节点 `SpriteRenderer.size` 使用源 Sprite 的 `bounds.size`，用于接近当前手工 prefab 的显示尺寸来源。
- 已存在同名 prefab 时会覆盖保存为当前 Sprite 对应结构；不会创建重复 prefab。
- 工具只扫描 Back、Clothes、Face，不扫描 `Assets/Game/ArtSrc/Skins/UI`，避免把列表图标误生成为运行时部件 prefab。

### 编辑器边界

- 工具只通过 `AssetDatabase`、`PrefabUtility` 和本地 Sprite 资产工作。
- 工具没有 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、导入回调或每帧逻辑。
- 工具不访问 `DB`、`SaveManager`、`RemoteConfig`、`ResourceManager`、运行时 singleton、热更、广告、内购、分析或平台服务。

### 验证与风险

- 已做静态实现检查：新增脚本为 Editor 目录脚本，菜单路径符合需求，扫描目录固定为 Back/Clothes/Face，输出目录固定为 `Assets/Game/Prefabs/Skins`。
- 已做 Unity batchmode 编译/域重载尝试：Unity 6000.0.59f2 能加载项目并完成程序集域重载，日志未发现 `error CS`；但两次 `-executeMethod Bear.Editor.SkinsPrefabGenerator.AutoGenerateSkinsPrefabs` 都停在 `Application.AssetDatabase Initial Refresh Start`，未进入生成器 Console 汇总，已终止本次新建的 batchmode 进程。
- 待 Unity Editor 验证：实际执行菜单后，检查当前资源集所有目标 Sprite 是否生成/更新同名 prefab，Console 汇总是否准确，已有 `.meta` 是否按预期保留。
- 待 PlayMode 验证：生成后的 prefab 能否被 ShopPanel boxer 预览和 Actor Skin 绑定正确加载、挂载和显示。
- 待资源包验证：生产 `GamePackage` manifest 是否包含生成后的 `Assets/Game/Prefabs/Skins/*.prefab`。
- 待 Android/iOS 横屏验证：使用生成 prefab 的 Skin 切换、关卡进入和外观刷新无明显卡顿或显示错位。
- 回滚路径：删除 `Assets/Editor/SkinsPrefabGenerator.cs` 与本次生成/更新的 Skin prefab，恢复到手工维护 prefab 流程。

## 2026-06-17 SkinItem 排序与 ownedToggle 筛选

### 行为

- `ShopPanel_SkinPart` 在构建当前 Skin 列表时，先按当前 SkinPart 筛选，再根据 `ownedToggle` 状态决定是否排除未拥有皮肤。
- 可见列表按三段规则排序：已拥有皮肤优先，`Quality` 数值从高到低，同拥有状态且同品质时保留原始 skin 数据源顺序。
- 原始数据源顺序来自当前目录来源：优先 `TbSkinShop.DataList`，没有 shop 顺序时使用 `TbSkin.DataList`。
- `SkinUnlockType.Default` 仍按既有所有权语义视为已拥有；`ownedToggle` 只影响列表可见项，不修改所有权、当前选择、解锁、预览或 Actor 绑定。

### 影响范围

- 运行时代码只修改 `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- `SkinItem`、`InfiniteScrollView`、Luban 配置、存档字段、资源路径、Actor 绑定、广告/内购 SDK 和普通 ShopPanel 商品逻辑保持不变。
- `ShopPanel.prefab` 已有 `ownedToggle` 序列化引用，指向 `UI_pifu_toggle_owned_01` Toggle；本次未修改 prefab。

### 验证与风险

- 静态编译：`dotnet build BrainPunk/Assembly-CSharp.csproj --no-restore` 通过，0 errors，92 个既有 warnings。
- 静态扫描：`ShopPanel_SkinPart.cs` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`StreamingAssets` 或 `PlayerPrefs`。
- 未验证风险：Unity PlayMode 尚未实际验证 all/back/face/body 过滤、ownedToggle 开关、解锁后重排、apply/clear、快速滚动、Actor 外观回归和 Android/iOS 横屏触控表现。
- 回滚路径：还原 `ShopPanel_SkinPart.cs` 中 ownedToggle 事件绑定、owned-only 过滤和 `SkinCatalogEntry` 排序逻辑，即恢复为原始 SkinPart 过滤后的数据源顺序。

## 2026-06-23 SkinPart version 2.0 实现记录

### 需求背景

本次实现落地 `specs/029-skinpart-v2-state/` 中的 SkinPart v2.0 状态需求：Skin 列表首位增加 Restore 逻辑行，`SkinItem` 使用明确的 `State` enum，未拥有的 `Ad` / `Coin` / `Purchase` / `Special` 皮肤点击先在 ShopPanel Boxer 中临时试穿但不写入 `GameData`，已拥有皮肤点击仍保持立即应用并保存。

### 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs`、`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 静态核对边界：`Assets/Game/UI/ShopPanel/Scripts/InfiniteScrollView.cs`、`Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs`、`Assets/Game/Scripts/Level/Actor/ActorSkinSelectionResolver.cs`。
- 资源/Prefab 证据：`Assets/Game/UI/ShopPanel/Prefabs/SkinItem_Revert.prefab` 存在且含 `U_Skins_Des_05`；实际 `ShopPanel` prefab 路径为 `Assets/Game/UI/ShopPanel/Resources/ShopPanel.prefab`，其中已存在 `skinItemRestorePrefab` 引用。
- 未修改保存 schema、Luban 配置 schema、RemoteConfig、Analytics、平台 SDK、热更新/资源打包规则、普通 ShopPanel 商品、Actor 运行时绑定逻辑。

### 实现前基线

- `ShopPanel_SkinPart` 原列表源是 `List<Config.Skin>`，只能表达配置皮肤行，不能表达非配置 Restore 行。
- `SkinItemState` 原先只在 `SkinItem.cs` 中声明，且存在拼写错误 `Resort`，未参与绑定、清理或点击路由。
- 未拥有皮肤点击只显示解锁按钮/提示，不会更新 Boxer 试穿预览。
- `ClearCurrentSkinPart(SkinPart part)` 会把 back/face/body 当前值固定按 `SkinPart.FullBody` 解析，不能作为 v2.0 Restore 的可靠清理入口。
- `ApplyCurrentSkinToViewer()` 原先只读取 `DB.GameData` 当前保存字段，没有 ShopPanel-only 临时试穿层。

### 实现后行为

- `SkinItemState` 规范值为 `None`、`Normal`、`Selected`、`Preview`、`Restore`；`Resort` 已从最终脚本中移除。
- `SkinItem` 新增显式状态绑定入口和 `State` 只读属性；普通皮肤 `Clear()` / 重新绑定会重置数据、状态、点击回调、图标、标题、选中标记、缩放、解锁显示和交互状态，并递增图标异步加载版本。
- Restore 行通过 `SkinItem.BindRestore(...)` 绑定，不需要 `Config.Skin`，不加载皮肤图标；Restore 绑定和 Restore 状态下的 `Clear()` 都不替换、不清空、不启用/禁用 `icon`，不显示价格或广告解锁区域，点击只回调 SkinPart 当前部件清理逻辑。
- `ShopPanel_SkinPart` 使用本地 `SkinCatalogRow` 表达 Restore 行和普通 Skin 行，每次 `RebuildCatalog()` 都先插入一个 Restore 行，再按原有拥有优先、品质、来源顺序加入普通皮肤行。
- `InfiniteScrollView` 新增按数据索引选择 `SkinItem` prefab 的可选回调，仍保持有限可见槽位复用；Restore 行在 `skinItemRestorePrefab` 已配置时实例化该 prefab，普通行继续使用 `skinItemPrefab`，缺失 Restore prefab 时回退为普通槽位的 Restore 状态绑定。
- 未拥有且可试穿的 `Ad`、`Coin`、`Purchase`、`Special` 行点击后写入 `_transientPreviewSkins[SkinPart]`，立即刷新 ShopPanel Boxer 预览，然后继续显示原有 action button 或提示路径；点击本身不调用 `GameData.Save()`，也不派发 `ActorSkinSelectionChangedEvent`。
- `Unavailable` 仍由 `SkinItem` 设置为不可交互，不进入临时试穿。
- 已拥有皮肤点击仍走 `ApplyOwnedSkin(skin, true)` 保存应用；成功广告/金币/内购解锁仍走 `GrantSkin -> ApplyOwnedSkin(..., true)`，并在保存应用时清除同部位临时试穿状态。
- Restore 按当前筛选部位直接清空对应保存字段：Back -> `CurrentBackPart`，Face -> `CurrentFacePart`，Body -> `CurrentBodyPart`，FullBody -> `CurrentSkinId`；同时清除同部位临时试穿。保存字段本来为空时不保存、不派发保存事件，但仍刷新预览和可见槽位。
- `ApplyCurrentSkinToViewer()` 现在按“临时试穿优先，否则读取保存字段”组合四个部位；关闭、禁用或销毁 SkinPart 时清空全部临时试穿并递增 `_boxerApplyVersion`，防止旧异步预览结果回写。
- Actor 侧仍只读取 `DB.GameData.CurrentSkinId`、`CurrentBackPart`、`CurrentFacePart`、`CurrentBodyPart`，没有接入 ShopPanel 临时试穿状态。

### 数据、资源、事件流程

- 预览点击流程：`SkinItem(State.Normal/Preview)` -> `ShopPanel_SkinPart.OnSkinItemClicked()` -> `SetTransientPreview()` -> `ApplyCurrentSkinToViewer()`；不写保存、不派发 Actor 保存事件。
- Restore 流程：`SkinItem(State.Restore)` -> `ClearCurrentSkinPart(_skinPart)` -> 清同部位 transient -> 按部位清保存字段 -> 保存字段变化时 `GameData.Save()` 和 `ActorSkinSelectionChangedEvent(Cleared)` -> 刷新 Boxer 与可见槽位。
- 拥有/解锁流程：已拥有点击或 `GrantSkin(...)` 成功后仍通过 `ApplyOwnedSkin(..., true)` 写入对应保存字段、`GameData.Save()`、派发 `ActorSkinSelectionChangedEvent(Applied)` 并刷新预览。
- 资源加载仍使用 Bear `ResourceManager`；SkinItem 普通图标加载和 Boxer 部件 prefab 加载继续使用已有 async version 检查。Restore 绑定和 Restore 状态下的清理只递增图标异步加载版本以拦截旧回写，`icon` 可见状态由 `skinItemRestorePrefab` 自身维护。

### 验证记录

- Speckit checklist：`specs/029-skinpart-v2-state/checklists/requirements.md` 为 16/16 完成。
- Speckit prerequisite：模板要求的 bash prerequisite 在当前 PowerShell 环境中无法执行，原因是 `bash` 命令不可用；改用 PowerShell prerequisite 从仓库根 `D:\GitStore\BrinPunk` 执行时失败，原因是当前分支 `feature/1.8.0_common` 不符合 Speckit 编号分支命名；实现继续基于显式目录 `specs/029-skinpart-v2-state/`。
- 静态检查确认最终脚本中无 `SkinItemState.Resort`、无 `_skins` 列表源残留、无 `CurrentSkinName` 残留。
- 静态 prefab 检查确认 `SkinItem_Revert.prefab` 存在，`Resources/ShopPanel.prefab` 存在 `skinItemRestorePrefab` 引用；Restore 行已通过 `InfiniteScrollView.SetItemPrefabSelector(...)` 接入该 prefab。
- 静态代码检查确认 `BindRestore(...)` 调用 `ResetBindingVisuals(..., resetIconVisuals: false)`，`Clear()` 在当前状态为 Restore 时同样跳过 icon 可见处理，不会写入 `icon.sprite` 或 `icon.enabled`；普通行绑定和普通状态清理仍会按原路径清理普通图标。
- `dotnet build .\Assembly-CSharp.csproj --no-restore` 在接入 Restore prefab selector 后复核通过，结果为 0 error / 100 warnings；warning 为项目现有全局 warning 类别。

### 未验证风险

- 未执行 Unity PlayMode 场景 1-6，因此 Restore 首位实际 UI、四筛选清理、20 次未拥有预览不存档、30 次槽位复用、关闭清 transient、解锁回调完成后的 UI 表现仍需在 Editor 中补验。
- 未执行 Android/iOS 横屏 smoke，因此触控滚动、快速点击、文本/图标适配、帧率和 stale preview 在真机/模拟器上仍未验证。
- 当前 ShopPanel 保存键仍使用 `ResolvePartSkinKey(skin) -> skin.Id`，Actor resolver 仍支持 `Id` / `PrefabResource` / `SpineResource` 三键匹配；本次未扩大范围统一保存键策略。
- Restore 行已改为优先使用 `skinItemRestorePrefab`，且 Restore 绑定不再处理 icon 可见状态；但尚未执行 Unity PlayMode 实机 UI 复核，因此独立 Restore prefab 的实际尺寸、锚点、点击区域、icon 初始显示状态和滚动复用表现仍需补验。

### 回滚路径

- 回滚 `SkinItem.cs` 中显式 `SkinItemState` 绑定、`BindRestore(...)` 和 Preview/Restore 文本逻辑。
- 回滚 `ShopPanel_SkinPart.cs` 中 `SkinCatalogRow`、`_transientPreviewSkins`、Restore 首行、预览合成和按部位 Restore 清理逻辑，恢复为纯 `List<Config.Skin>` 数据源和未拥有点击只显示 action button 的旧行为。
- 不需要回滚保存 schema、配置 schema、Actor 绑定、资源打包或平台 SDK，因为本次未修改这些边界。

## 2026-06-23 SkinPart tryUsingTxt 显示同步

### 需求背景

本次需求是在 `ShopPanel_SkinPart` 中增加 `tryUsingTxt` 的显示控制：当前存在 ShopPanel 临时试穿时显示提示，没有临时试穿时隐藏提示。

### 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- UI 对象：`Assets/Game/UI/ShopPanel/Resources/ShopPanel.prefab` 中已存在名为 `u_skins_des_06_txt` 的试穿提示文本，本次代码在序列化字段未绑定时按该名称兜底查找。
- 未修改 `SkinItem.cs`、`InfiniteScrollView.cs`、Luban 配置、`GameData` 存档 schema、Actor 绑定、广告/金币/内购、analytics、资源打包或平台 SDK。

### 实现前基线

- `ShopPanel_SkinPart` 已有 ShopPanel-only `_transientPreviewSkins` 临时试穿状态，并且未拥有可试穿皮肤点击后会刷新 Boxer 预览。
- `ShopPanel.prefab` 中存在 `u_skins_des_06_txt` 文本对象，但当前 `ShopPanel_SkinPart` 没有确认到序列化引用，脚本也没有根据 `_transientPreviewSkins` 统一控制该提示显隐。

### 实现后行为

- `tryUsingTxt` 只根据 `_transientPreviewSkins.Count > 0` 控制 active：存在任意临时试穿时显示，没有临时试穿时隐藏。
- `Awake` 和 `OnEnable` 会同步一次提示状态，避免 prefab 初始 active 状态遗留到无试穿状态。
- `SetTransientPreview(...)` 写入临时试穿后立即显示提示。
- `ClearTransientPreview(...)` 和 `ClearAllTransientPreviews()` 清理临时试穿后立即刷新提示；Restore、已拥有应用、解锁成功应用、禁用和销毁路径都会复用这些清理入口。
- 显示控制只调用 `GameObject.SetActive(...)`，不触发按钮动画，不写保存，不派发 `ActorSkinSelectionChangedEvent`，也不改变 Boxer 资源加载或 Actor 绑定逻辑。

### 数据、UI、保存和事件流

- 预览点击流程仍为 `SkinItem` 点击 -> `SetTransientPreview(...)` -> `_transientPreviewSkins` 更新 -> `tryUsingTxt` 显示 -> `ApplyCurrentSkinToViewer()`。
- 清理流程仍为 Restore / 已拥有应用 / 解锁成功应用 / 关闭清理 -> 清 `_transientPreviewSkins` -> `tryUsingTxt` 隐藏；只有原本需要保存或派发事件的保存字段变化路径继续保存和派发事件。
- `tryUsingTxt` 不参与 `GameData`，不会影响进入关卡后的 Actor 外观。

### 验证记录

- 静态检查确认 `ShopPanel_SkinPart.cs` 中 `tryUsingTxt` 在 `Awake`、`OnEnable`、`SetTransientPreview`、`ClearTransientPreview`、`ClearAllTransientPreviews` 路径同步。
- 静态 prefab 检查确认 `Assets/Game/UI/ShopPanel/Resources/ShopPanel.prefab` 中存在 `u_skins_des_06_txt` 文本对象；当前脚本提供同名兜底查找。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig` 或 `PlayerPrefs`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore` 通过，结果为 0 error / 100 warnings；warning 为项目既有全局 warning。

### 未验证风险

- Unity PlayMode 尚未验证实际打开 ShopPanel 时无试穿隐藏、点击未拥有可试穿皮肤显示、Restore/已拥有应用/关闭面板后隐藏。
- Android/iOS 横屏真机或模拟器尚未验证提示文本位置、active 切换和快速点击时的显示表现。

### 回滚路径

- 回滚 `ShopPanel_SkinPart.cs` 中 `tryUsingTxt` 字段、`SyncTryUsingText()`、按名称兜底查找和临时试穿状态同步调用，即可恢复为原来不控制该提示文本显隐的行为。

## 2026-06-23 SkinPart FullBody Spine 预览修复

### 需求背景

本次问题是 SkinPart 切换 FullBody 的 Spine 皮肤名没有在 ShopPanel 预览中生效。用户要求检查并修复时不要修改 `GameData` 内容，因此本次只调整 ShopPanel 预览刷新代码，不改存档数据、不改 Luban 配置、不进入 PlayMode 写存档路径。

### 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 预览对象：ShopPanel 当前场景预览使用场景根 `Skin` 下的 `ActorSpineCtrl`、`SkinRt_Camera` 和 RenderTexture；`Resources/ShopPanel.prefab` 中 `skinViewer` 序列化引用当前为空。
- 配置证据：`Config.Skin` 中字段名是 `SpineResource`；当前 `tbskin.json` 里 Back/Face/Body 行的 `spineResource` 为空，FullBody 行 `204001` 配置了 `box_astronaut`。
- 未修改 `GameData` 保存字段、`OwnedSkin`、`CurrentSkinId`、`CurrentBackPart`、`CurrentFacePart`、`CurrentBodyPart`、Luban 配置、`ActorSkinBinding`、广告/金币/内购、analytics、资源打包或平台 SDK。

### 实现前基线

- `ApplyCurrentSkinToViewer()` 会先解析保存值和临时试穿值，并加载 back/face/body 部件 prefab。
- Spine 皮肤应用只在 `skinViewer != null` 时走 `UISpineCtrl.SetSkin(...)` 或 `UISpineCtrl.SetCombinedSkin(...)`。
- 当前 ShopPanel prefab 没有绑定 `skinViewer`，因此方法会在 `skinViewer == null` 时提前返回；场景 RenderTexture 里实际看到的 `ActorSpineCtrl` 没有收到 FullBody Spine 皮肤切换。

### 实现后行为

- `ApplyCurrentSkinToViewer()` 先解析 `backSpine`、`faceSpine`、`bodySpine` 和 `fullBodySkinName`，再调用 `ApplyFullBodySkinToBoxerActor(...)`。
- `ApplyFullBodySkinToBoxerActor(...)` 会在缺少 `boxerActorSpine` 引用时复用现有 `ResolveBoxerPreviewReferences()` 查找场景预览 Actor，然后调用 `ActorSpineCtrl.SetSkin(fullBodySkinName)`。
- 如果目标 FullBody skin 为空或不存在，并且不是已经尝试的 fallback 名称，则回退调用 `fallbackBaseSkinName`。
- 原 `UISpineCtrl` 分支保留不变；未来如果 ShopPanel 重新绑定 UI Spine viewer，仍可继续支持 UI Spine 的单皮肤或合并皮肤路径。

### 数据、资源、保存和事件流

- 预览点击流程仍为 `SkinItem` 点击 -> `SetTransientPreview(...)` -> `_transientPreviewSkins` 更新 -> `ApplyCurrentSkinToViewer()` -> 解析 `Config.Skin.SpineResource` -> `ActorSpineCtrl.SetSkin(...)`。
- 保存应用流程仍为已拥有点击或解锁成功后才进入 `ApplyOwnedSkin(..., true)`、保存字段写入、`GameData.Save()` 和 `ActorSkinSelectionChangedEvent`。
- 本次新增的 `ApplyFullBodySkinToBoxerActor(...)` 只读当前有效选择和配置，只刷新 ShopPanel 场景预览，不写 `GameData`，不派发保存事件。
- 当前部件行没有 `SpineResource`，因此场景 Actor 路径只修复 FullBody Spine 皮肤；若未来 back/face/body 也配置 Spine 皮肤，仍需评估 `ActorSpineCtrl` 是否需要合并 skin 能力。

### 验证记录

- 静态检查确认 `ShopPanel.prefab` 中 `skinViewer` 为空，`Main.unity` 中存在名为 `Skin` 的场景根、`SkinRt_Camera` 和 Actor 预览路径。
- 静态检查确认 `Config.Skin` 字段为 `SpineResource`，当前只有 FullBody 配置 `box_astronaut`，Back/Face/Body 的 `spineResource` 为空。
- 静态扫描确认本次未新增 `GameData.Save()` 调用；`ShopPanel_SkinPart.cs` 中保存调用仍只存在于原有授予、应用和清理保存字段路径。
- `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs` 通过，仅提示该文件下次 Git 接触时 LF/CRLF 规范化。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 100 warnings；warning 为项目既有全局 warning。

### 未验证风险

- 未执行 Unity PlayMode，避免在检查过程中触发运行时存档写入路径；因此实际打开 ShopPanel 后 FullBody `box_astronaut` 是否在 RenderTexture 预览中正确显示仍需 Editor 中手动验证。
- 未执行 Android/iOS 横屏 smoke，快速切换 FullBody 试穿、Restore、关闭面板后的预览稳定性仍需真机或模拟器补验。
- `ActorSpineCtrl` 当前只有 `SetSkin(...)`，没有 `UISpineCtrl.SetCombinedSkin(...)` 等价能力；未来部件 SpineResource 非空时，需要单独设计场景 Actor 的合并皮肤路径。

### 回滚路径

- 回滚 `ShopPanel_SkinPart.cs` 中 `ApplyFullBodySkinToBoxerActor(...)` helper，以及 `ApplyCurrentSkinToViewer()` 中对该 helper 的调用和 `fullBodySkinName` 提前解析，即可恢复为只在 `skinViewer` 非空时应用 Spine 的旧行为。

## 2026-06-23 SkinPart 穿戴中与试戴互斥修复

### 需求背景

本次修复针对 ShopPanel SkinPart 列表中同一部位可能同时显示“穿戴中”和“试戴”的表现问题。期望行为是：当某个 `SkinPart` 存在 ShopPanel-only 临时试戴时，该部位列表里只能有试戴项显示 `Preview/试戴`，保存中的已穿戴项不能同时显示 `Selected/穿戴中`。

### 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 视觉状态：`SkinItemState.Selected` 与 `SkinItemState.Preview` 的列表绑定优先级。
- 未修改 `SkinItem.cs`、`GameData` 保存字段、`OwnedSkin`、Luban 配置、资源加载、广告/金币/内购、analytics、平台 SDK、普通 Shop 商品或 Actor 关卡绑定逻辑。

### 实现前基线

- `ResolveSkinItemState(Config.Skin skin)` 原先先判断 `IsCurrentSkin(skin)`，再判断 `IsTransientPreviewSkin(skin)`。
- 当同一部位保存了已穿戴皮肤，同时又点击未拥有皮肤进行临时试戴时，保存项仍会解析为 `Selected`，试戴项解析为 `Preview`，列表中可能同时出现“穿戴中”和“试戴”。
- 临时试戴本身不写 `GameData`，已拥有点击和解锁成功仍通过 `ApplyOwnedSkin(..., true)` 保存，并会清理同部位临时试戴。

### 实现后行为

- `ResolveSkinItemState(...)` 现在先判断当前行是否为同部位临时试戴项，是则返回 `Preview`。
- 如果同一 `SkinPart` 已存在临时试戴，但当前行不是该试戴项，则当前行直接按 `Normal` 绑定，不再显示 `Selected/穿戴中`。
- 没有同部位临时试戴时，保存项仍按原逻辑显示 `Selected/穿戴中`。
- 已拥有皮肤点击保存穿戴、Restore 清理、关闭 ShopPanel 清理临时试戴等路径保持原有边界；本次只调整列表状态表现，不改变存档语义。

### 数据、UI、保存和事件流

- 试戴点击流程仍为 `SkinItem` 点击 -> `SetTransientPreview(...)` -> `_transientPreviewSkins[SkinPart]` 更新 -> Boxer 预览刷新 -> 可见槽位刷新。
- 列表状态流现在是：同部位存在 transient 时，transient 行显示 `Preview`，保存穿戴行降级为 `Normal`；transient 清除后，保存穿戴行下一次刷新恢复 `Selected`。
- 本次没有新增 `GameData.Save()` 或 `ActorSkinSelectionChangedEvent` 派发；保存和事件仍只发生在已拥有应用、解锁授予应用、Restore 清理保存字段等原有路径。

### 验证记录

- 静态检查确认 `ResolveSkinItemState(...)` 的优先级已改为 `Preview` -> 同部位 transient 屏蔽 `Selected` -> 保存选中 `Selected` -> `Normal`。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig` 或 `PlayerPrefs`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore` 通过，结果为 0 error / 1114 warnings；warning 为项目既有全局 warning。

### 未验证风险

- Unity PlayMode 尚未验证实际 UI 中点击未拥有可试戴皮肤后，试戴项显示“试戴”、原穿戴项不再显示“穿戴中”，清除试戴或关闭重开后保存穿戴项恢复显示。
- Android/iOS 横屏尚未验证快速点击、滚动复用、筛选切换时互斥表现无残留。

### 回滚路径

- 回滚 `ShopPanel_SkinPart.ResolveSkinItemState(...)` 中对 `IsTransientPreviewSkin(...)` 和 `HasTransientPreviewForPart(...)` 的优先判断，并移除 `HasTransientPreviewForPart(...)` helper，即可恢复为保存穿戴优先显示的旧状态解析。

## 2026-06-23 SkinItem 试戴缩放动画同步

### 需求背景

本次修复针对 SkinItem 进入试戴状态时的缩放表现。期望行为是：试戴项从普通状态切换到 `Preview/试戴` 时，`BlockBg` 缩放动画应与选中项从普通状态切换到 `Selected/穿戴中` 时保持一致。

### 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 复用的视觉逻辑：`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs` 中已有 `IsHighlightedState(...)`、`SwitchSelected(...)` 和 `ApplySelectedScale(...)`。
- 未修改 `SkinItem.cs`、`GameData`、Luban 配置、资源加载、Actor 绑定、广告/金币/内购、analytics 或平台 SDK。

### 实现前基线

- `SkinItem.IsHighlightedState(...)` 已经把 `SkinItemState.Selected` 和 `SkinItemState.Preview` 都视为高亮状态。
- `SkinItem.ApplySelectedScale(...)` 对所有高亮状态使用同一个 `SelectedScale` 和 `BlockBg.transform.DOScale(..., 0.15f)` 动画。
- 已拥有皮肤保存应用后会调用 `RefreshVisibleSlots(true)`，因此同一可见槽位从普通态切到 `Selected` 时会播放选中缩放动画。
- 未拥有皮肤试戴点击后只调用 `RefreshVisibleSlots()`，没有传入动画刷新标记；同一可见槽位切到 `Preview` 时会直接设置缩放而不是播放与选中一致的 tween。

### 实现后行为

- 未拥有可试戴皮肤点击后改为调用 `RefreshVisibleSlots(true)`。
- 当被点击项仍在同一可见槽位且从 `Normal` 切到 `Preview` 时，`SkinItem.Bind(...)` 会按已有条件触发 `animateSelectionChange`，并复用 `SwitchSelected(true, true)` 与 `ApplySelectedScale(true, true)`。
- 试戴项的缩放倍率、动画时长和选中项保持一致；滚动复用、整列表刷新和不同数据槽位绑定仍按原规则直接设置状态，避免滚动时反复播放动画。

### 数据、UI、保存和事件流

- 试戴点击流程仍为 `SkinItem` 点击 -> `SetTransientPreview(...)` -> `_transientPreviewSkins[SkinPart]` 更新 -> Boxer 预览刷新 -> `RefreshVisibleSlots(true)`。
- 本次只改变可见槽位状态刷新是否允许动画；不新增存档写入、不派发 `ActorSkinSelectionChangedEvent`，也不改变解锁按钮、资源加载或 Actor 关卡绑定。

### 验证记录

- 静态检查确认 `Preview` 与 `Selected` 都走 `IsHighlightedState(...)`，并共用 `ApplySelectedScale(...)` 的 `DOScale(..., 0.15f)`。
- 静态检查确认试戴点击路径已从 `RefreshVisibleSlots()` 改为 `RefreshVisibleSlots(true)`。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig` 或 `PlayerPrefs`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore` 通过，结果为 0 error / 96 warnings；warning 为项目既有全局 warning。

### 未验证风险

- Unity PlayMode 尚未验证实际点击未拥有可试戴项时，`Preview/试戴` 的 `BlockBg` 缩放节奏与 `Selected/穿戴中` 完全一致。
- Android/iOS 横屏尚未验证快速点击、快速滚动和筛选切换时不会出现重复动画或槽位缩放残留。

### 回滚路径

- 回滚 `ShopPanel_SkinPart.OnSkinItemClicked(...)` 中试戴点击后的 `RefreshVisibleSlots(true)` 为 `RefreshVisibleSlots()`，即可恢复为试戴状态直接设置缩放、不播放选中同款缩放动画的旧表现。

## 2026-06-23 SkinPart Restore icon 保持不变

### 需求背景
`ShopPanel_SkinPart` 的 Restore 行使用独立的 `SkinItem_Revert.prefab` 视觉。需求要求当 `SkinItem` 处于 `SkinItemState.Restore` 时，`icon` 必须一直保持 prefab 初始表现；即使 `SkinItem` 被刷新、清理、禁用/启用，或旧的普通皮肤 icon 异步加载返回，也不允许改变 Restore icon。

### 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs`。
- 相关调用边界：`ShopPanel_SkinPart.BindSkinItem(...)` 仍通过 `BindRestore(...)` 绑定 Restore 行，`InfiniteScrollView` 刷新和槽位清理仍调用 `SkinItem.Clear()`。
- 本次未修改 `ShopPanel_SkinPart.cs`、`InfiniteScrollView.cs`、`SkinItem_Revert.prefab`、`ShopPanel.prefab`、`GameData` 保存字段、Luban 配置、Actor 绑定、广告、金币、内购、analytics 或平台 SDK。

### 实现前基线

- `BindRestore(...)` 已经通过 `ResetBindingVisuals(..., resetIconVisuals: false)` 避免在 Restore 绑定时清理 icon。
- `Clear()` 依赖当前 `_state == SkinItemState.Restore` 判断是否跳过 icon 清理；如果刷新链路或后续维护在状态切换窗口调用清理，Restore icon 仍存在被误清理的风险。
- 普通皮肤图标异步加载已经使用 `_iconLoadVersion` 和 `data == skin` 防止大部分旧回写，但 Restore icon 保护逻辑分散在调用参数和状态判断中。

### 实现后行为

- `SkinItem` 现在会在首次解析 `icon` 引用时缓存 prefab 初始的 `sprite` 和 `enabled` 状态。
- `BindRestore(...)` 会进入 `_preserveRestoreIconVisuals` 保护态，重置其他绑定视觉时不处理 icon，并立即恢复 prefab 初始 icon 表现。
- `Clear()` 在 Restore 状态或 Restore 保护态下只清理数据、标题、选中、价格和交互状态，不清空、不禁用、不替换 icon。
- `OnEnable()` / `OnDisable()` 在 Restore 保护态下会恢复 prefab 初始 icon 表现，避免刷新、隐藏或复用过程留下被其他路径改动后的 icon 状态。
- `LoadIconAsync(...)` 在开始前和异步返回后都会检查 Restore 保护态；一旦槽位已经进入 Restore，就不会把普通皮肤图标回写到 Restore icon 上。
- 普通皮肤绑定会关闭 Restore icon 保护，并继续按原逻辑清理旧 icon、加载 `TextureResources` 对应图标。

### 数据、资源、UI、保存和事件流

- Restore icon 保护只作用于单个 `SkinItem` 的 UI Image 状态，不读取或写入 `DB.GameData`。
- 普通皮肤图标资源加载仍通过 Bear `ResourceManager`，本次没有新增资源路径、资源包规则或 YooAsset 直接调用。
- Restore 点击后的清理保存逻辑仍由 `ShopPanel_SkinPart.ClearCurrentSkinPart(...)` 负责；本次没有新增 `GameData.Save()` 或 `ActorSkinSelectionChangedEvent` 派发。
- `SkinItem_Revert.prefab` 继续作为 Restore 行 icon 的视觉来源；代码只恢复 prefab 初始 icon，不在运行时指定新的 icon 资源。

### 验证记录

- 静态检查确认 `SkinItem.cs` 中 Restore icon 保护覆盖 `BindRestore()`、`Clear()`、`OnEnable()`、`OnDisable()` 和 `LoadIconAsync()` 回写路径。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`Instantiate` 或平台 SDK 链路。
- `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs` 通过；仅提示工作区下次 Git 接触该文件时会做 LF/CRLF 规范化。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 1114 warnings；warnings 为当前工程既有全局 warning。

### 未验证风险

- 尚未执行 Unity PlayMode，仍需验证打开 ShopPanel 后 Restore 首行 icon 在筛选切换、列表刷新、快速滚动复用、点击 Restore、关闭重开过程中始终保持 prefab 初始图标。
- 尚未执行 Android/iOS 横屏 smoke，仍需验证触摸快速滚动和刷新过程没有 Restore icon 闪烁、消失或被普通皮肤图标覆盖。

### 回滚路径

- 回滚 `SkinItem.cs` 中 `_preserveRestoreIconVisuals`、初始 icon 快照、`RestoreInitialIconVisuals()`、`ShouldPreserveRestoreIconVisuals()` 以及相关调用，即可恢复为仅依赖 `resetIconVisuals: false` 和 `_state == Restore` 的旧保护方式。

## 2026-06-23 SkinPart Boxer preview_skin 切换动画

### 需求背景

本次需求是 `ShopPanel_SkinPart` 每次切换 Skin 时，让场景 Boxer Spine 播放 `preview_skin` 动画；如果 `preview_skin` 当前已经在播放，则跳过本次触发，避免快速点击时反复重启同一动画。

### 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 预览对象：ShopPanel Skin 页场景预览使用的 `ActorSpineCtrl boxerActorSpine`。
- 未修改 `SkinItem.cs`、`InfiniteScrollView.cs`、prefab、`GameData` 保存字段、Luban 配置、Actor 关卡绑定、广告、金币、内购、analytics、资源打包或平台 SDK。

### 实现前基线

- `ApplyCurrentSkinToViewer()` 会刷新 back/face/body 部件 prefab 和 FullBody Spine skin，但不会在 Skin 切换时触发额外 Spine 动画。
- `ActorSpineCtrl` 已提供 `AnimationState`、`PlayAnimation(...)` 和当前动画查询能力，可复用现有 Spine 控制封装。
- 试戴和已拥有穿戴路径都会走 `ApplyCurrentSkinToViewer()`，Restore 清理当前部件时也会走同一预览刷新入口。

### 实现后行为

- `ShopPanel_SkinPart` 新增本地常量 `preview_skin` 和 `PlayBoxerPreviewSkinAnimationIfNeeded()`。
- 未拥有可试戴皮肤切换到新的临时试戴项时，刷新 Boxer 预览并播放一次非循环 `preview_skin`。
- 已拥有皮肤应用保存后，刷新 Boxer 预览并播放一次非循环 `preview_skin`。
- Restore 清理同部位临时试戴或保存值实际发生变化时，刷新 Boxer 预览并播放一次非循环 `preview_skin`。
- 如果 Boxer 当前 0 号轨道正在播放 `preview_skin` 且该 TrackEntry 仍在循环或尚未完成，则跳过，不重启动画。
- 打开 SkinPart 初始化恢复当前保存外观时不主动播放该切换动画。
- 如果 Boxer skeleton 中找不到 `preview_skin`，只记录一次 warning，后续不会持续刷日志。

### 数据、资源、保存和事件流

- 本次新增逻辑只读取 `ActorSpineCtrl.AnimationState` 和 SkeletonData 中的动画名，不读取或写入 `DB.GameData`。
- 保存仍只发生在原有默认拥有迁移、已拥有应用、解锁授予和 Restore 清理保存字段路径。
- `ActorSkinSelectionChangedEvent` 派发条件不变；临时试戴仍不会派发保存事件。
- 未新增资源加载路径，`preview_skin` 是否存在依赖当前 Boxer Spine 资源内容。

### 验证记录

- 静态检查确认 `OnEnable` 初始化仍调用 `ApplyCurrentSkinToViewer()`，不会主动播放 `preview_skin`。
- 静态检查确认试戴切换、已拥有应用、Restore 有效清理路径会以 `playBoxerSwitchAnimation = true` 进入预览刷新。
- 静态检查确认同一个未拥有试戴项重复点击不会因 `_transientPreviewSkins` 未变化而重播。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig` 或 `PlayerPrefs`。
- `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs` 通过；仅提示工作区下次 Git 接触该文件时会做 LF/CRLF 规范化。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 1114 warnings；warnings 为当前工程既有全局 warning。

### 未验证风险

- Unity PlayMode 尚未验证实际 Boxer skeleton 中是否存在 `preview_skin` 动画，以及快速切换不同 Skin 时是否按预期只在未完成时跳过。
- Android/iOS 横屏尚未验证快速点击、滚动复用、Restore、关闭重开时的动画表现和帧稳定性。

### 回滚路径

- 回滚 `ShopPanel_SkinPart.cs` 中 `BoxerPreviewSkinAnimationName`、`_hasLoggedMissingBoxerPreviewSkinAnimation`、`PlayBoxerPreviewSkinAnimationIfNeeded()`，并恢复 `ApplyCurrentSkinToViewer(...)` 调用签名与调用点，即可恢复为只刷新外观、不播放 `preview_skin` 的旧行为。

## 2026-06-24 SkinPart 无限金币购买皮肤

### 需求背景

本次需求是 ShopPanel SkinPart 金币购买皮肤时需要考虑玩家已经拥有无限金币状态：如果存在 `GameProps.UnlimitCoin`，金币皮肤应直接购买成功，不再因为普通 `GameProps.Coin` 数量不足而阻断，也不扣除普通金币。

### 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 触发入口：Skin 页金币解锁按钮 `OnCoinButtonClicked(...)`。
- 复用系统：`PlayCtrl.Instance.Bag.GetToolCount(GameProps.UnlimitCoin)`、`GrantSkin(...)`、`ApplyOwnedSkin(...)`、`DB.GameData.OwnedSkin` 和当前 Skin 保存字段。
- 未修改 `SkinItem.cs`、`InfiniteScrollView.cs`、prefab、Luban 配置、RemoteConfig、资源加载、广告、内购、analytics、平台 SDK、普通 Shop 商品或 Actor 关卡绑定逻辑。

### 原行为基线

- 金币皮肤点击购买时先读取 `GameProps.Coin` 数量。
- 当普通金币数量小于 `CoinPrice` 时显示 `U_HurryTips_NeedCoin` 并返回。
- 当普通金币足够且价格大于 0 时调用 `bag.RemoveTool(GameProps.Coin, price, CostType.Other)`，然后 `GrantSkin(_selectedSkin, RewardType.Purchase)`。
- 该路径未考虑 `GameProps.UnlimitCoin`，因此拥有无限金币但普通金币不足时仍会被错误拦截。

### 修复后行为

- `OnCoinButtonClicked(...)` 先计算 `hasUnlimitCoin = bag.GetToolCount(GameProps.UnlimitCoin) > 0`。
- 有无限金币时跳过普通金币余额检查和 `RemoveTool(GameProps.Coin, ...)`，直接进入原有 `GrantSkin(...)` 成功路径，并显示购买成功提示。
- 没有无限金币时维持原有普通金币检查、扣费、授予和成功提示行为。
- `GrantSkin(...)` 后续仍会写入 `OwnedSkin`、保存、应用当前皮肤、刷新列表并播放原有奖励音效；本次不改变保存语义。

### 数据、UI、保存和事件流

- 无限金币状态只读取 `GameProps.UnlimitCoin` 数量，不新增道具类型或存档字段。
- 无限金币购买不会扣除 `GameProps.Coin`，因此不会触发普通金币扣减事件或 `CostType.Other` 消耗打点。
- 皮肤授予、当前穿戴保存和 `ActorSkinSelectionChangedEvent` 仍由既有 `GrantSkin(...)` / `ApplyOwnedSkin(..., true)` 处理。
- 不涉及资源加载、广告、内购或普通 Shop 商品奖励链路。

### 验证记录

- 静态检查确认 `OnCoinButtonClicked(...)` 中 `GameProps.UnlimitCoin` 优先于普通金币余额检查，并且只有非无限金币状态才会调用 `RemoveTool(GameProps.Coin, price, CostType.Other)`。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、资源加载或平台 SDK 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 首次因 `Temp/obj` 文件被其他进程锁定失败；使用 `/p:UseSharedCompilation=false /m:1` 复跑通过，结果为 0 warning / 0 error。

### 未验证风险

- Unity PlayMode 尚未验证拥有 `UnlimitCoin` 且普通金币不足时，点击金币皮肤购买能直接授予、应用并显示成功提示。
- Unity PlayMode 尚未验证无 `UnlimitCoin` 时普通金币不足/足够两条旧路径保持不变。
- Android/iOS 横屏尚未验证快速点击金币购买按钮时的触摸、提示和列表刷新表现。

### 回滚路径

- 回滚 `ShopPanel_SkinPart.OnCoinButtonClicked(...)` 中 `hasUnlimitCoin` 判断及对应条件分支，使金币皮肤购买重新只依赖 `GameProps.Coin` 余额检查和扣费。

## 2026-06-24 Skin 部件 prefab layer 归属调整

### 需求背景

本次需求是 `Tools/Bear/Auto Generate Skins Prefabs` 生成 Skin 部件 prefab 时不要默认使用 `Ignore Raycast` layer；只有在 `ShopPanel_SkinPart` 预览换装时，实例化出来的 skin prefab 才需要切换为 `Ignore Raycast` layer，避免 ShopPanel 预览对象影响 UI 射线。

### 影响范围

- 编辑器工具：`Assets/Editor/SkinsPrefabGenerator.cs`。
- ShopPanel 运行时预览：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs` 的 `ApplyBoxerPartAsync(...)` 实例化路径。
- 未修改 `Config.Skin`、Luban 表、`GameData`、`ActorSkinBinding`、`ActorSkinSelectionResolver`、广告/金币/内购、analytics、资源打包规则或平台 SDK。

### 原行为基线

- `SkinsPrefabGenerator.CreatePrefabRoot(...)` 原先会把生成 prefab 根节点和同名 SpriteRenderer 子节点都设置为 layer 2。
- `ShopPanel_SkinPart.ApplyBoxerPartAsync(...)` 原先直接 `Instantiate(prefab, target)` 后重置 transform，不在 ShopPanel 预览实例上显式设置 layer。
- 静态扫描现有 `Assets/Game/Prefabs/Skins/*.prefab` 显示当前已存在资源的 `m_Layer` 均为 0；因此本次不需要批量改动现有 prefab 资产。

### 新行为

- `SkinsPrefabGenerator` 继续生成同名根节点、同名子节点和 `SpriteRenderer`，但不再写入 layer，生成结果保持 Unity 新建对象默认层级。
- `ShopPanel_SkinPart` 在 SkinPart 预览换装实例化 prefab 后，调用本地 `SetLayerRecursively(...)` 把该运行时实例及其子节点切换到 `Ignore Raycast` layer（数值 2）。
- Actor 关卡绑定路径未修改；`ActorSkinBinding` 仍按 prefab 自身层级实例化部件，不继承 ShopPanel 预览专用的 layer 调整。

### 数据、资源、UI、保存和事件流

- 本次不改变 `PrefabResource` 解析、Bear `ResourceManager` 加载、资源包收集、存档字段或 `ActorSkinSelectionChangedEvent` 派发。
- `Ignore Raycast` 切换只发生在 ShopPanel SkinPart 预览实例创建后的本地 GameObject 层级上，不写回 prefab 资产，也不影响已保存的 skin 选择。
- 编辑器生成器仍为手动菜单入口，没有新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、导入回调、运行时 singleton、DB、Save 或 RemoteConfig 链路。

### 验证记录

- 静态扫描确认 `SkinsPrefabGenerator.cs` 已无 `IgnoreRaycastLayer` 常量和 `root.layer` / `spriteNode.layer` 赋值。
- 静态扫描确认 `ShopPanel_SkinPart.ApplyBoxerPartAsync(...)` 只在实例化后的运行时对象上调用 `SetLayerRecursively(instance.transform, IgnoreRaycastLayer)`。
- 静态扫描 `Assets/Game/Prefabs/Skins/*.prefab` 的 `m_Layer` 当前均为 0，本次未批量修改现有 prefab。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 warning / 0 error。

### 未验证风险

- 尚未执行 Unity PlayMode，仍需验证 ShopPanel SkinPart 换装后预览对象确实为 `Ignore Raycast` 且不影响 UI 点击、滚动和 Restore。
- 尚未执行 Editor 菜单实测，仍需确认新生成或覆盖的 prefab 在 Inspector 中保持默认 layer。
- 尚未执行 Android/iOS 横屏 smoke，快速换装和滚动时的触摸路径仍待真机确认。

### 回滚路径

- 回滚 `SkinsPrefabGenerator.CreatePrefabRoot(...)` 中移除 layer 赋值的改动，并移除 `ShopPanel_SkinPart` 中 `IgnoreRaycastLayer`、`SetLayerRecursively(...)` 及实例化后的调用，即可恢复为生成时固定 layer、ShopPanel 预览不额外切 layer 的旧行为。

## 2026-06-30 Skin 所有权授予入口收敛

### 需求背景

本次修正 `SimpleBag.GetNewSkin(...)` 新增后的边界问题：皮肤所有权授予需要统一入口，避免 `GetSkinPopup`、`ShopPanel_SkinPart` 和默认皮肤迁移各自直接写 `DB.GameData.OwnedSkin`；同时避免默认皮肤迁移按皮肤数量多次保存。

### 影响范围

- 运行时代码：`Assets/Game/Scripts/Common/SimpleBag.cs`、`Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs`、`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 数据：继续使用 `DB.GameData.OwnedSkin`，不新增存档字段。
- UI：`GetSkinPopup` 获得皮肤弹窗、ShopPanel Skin 默认拥有迁移、广告/金币/内购解锁授予。
- 未修改 Luban 配置、资源加载、Actor 关卡绑定、广告 SDK、IAP SDK、analytics 或平台配置。

### 原行为基线

- `GetSkinPopup.GrantSkin(...)` 直接写入 `DB.GameData.OwnedSkin` 并立即保存。
- `ShopPanel_SkinPart.EnsureDefaultOwnedSkins()` 原本按 `changed` 汇总后最多保存一次；新增 `GetNewSkin` 后曾在循环中对每个默认皮肤分别保存。
- `ShopPanel_SkinPart.GrantSkin(...)` 仍保留一套直接写 `OwnedSkin` 的旧逻辑，并在随后 `ApplyOwnedSkin(..., true)` 再保存当前穿戴字段。

### 修复后行为

- `SimpleBag.GetNewSkin(string skinId, bool autoSave = true)` 成为皮肤所有权写入入口，负责校验空 id、初始化 `OwnedSkin`、去重新增和按需保存。
- `GetNewSkin(...)` 只负责所有权入库，不负责穿戴、刷新 UI、派发 `ActorSkinSelectionChangedEvent`，也不处理广告、金币、内购或 analytics 语义。
- `GetSkinPopup.GrantSkin(...)` 通过 `PlayCtrl.Instance?.Bag` 获取背包，背包缺失时记录错误并退出；正常路径仍授予弹窗对应皮肤并立即保存。
- `ShopPanel_SkinPart.EnsureDefaultOwnedSkins()` 批量调用 `GetNewSkin(..., false)`，只在本轮确实新增默认皮肤时统一 `gameData.Save()` 一次。
- `ShopPanel_SkinPart.GrantSkin(...)` 通过 `GetNewSkin(..., false)` 授予所有权，并复用随后 `ApplyOwnedSkin(..., true)` 的保存，把所有权和当前穿戴字段合并到一次保存中。

### 数据、UI、保存和事件流

- 默认皮肤迁移：`Config.Skin.UnlockType == Default` -> `SimpleBag.GetNewSkin(..., false)` -> 发生新增时统一 `GameData.Save()`。
- GetSkinPopup 免费皮肤：`GetSkinPopup.Create(skin)` -> `GrantSkin(...)` -> `SimpleBag.GetNewSkin(..., true)` -> `OwnedSkin` 保存；不穿戴、不派发 Actor skin 事件。
- ShopPanel 解锁皮肤：广告/金币/内购成功 -> `GrantSkin(...)` -> `SimpleBag.GetNewSkin(..., false)` -> `ApplyOwnedSkin(..., true)` 保存当前穿戴字段、派发 Actor skin 事件、刷新预览和列表。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Common/SimpleBag.cs Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs DevelopMD/Skin功能设计.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。

### 未验证风险

- 尚未执行 Unity PlayMode，仍需验证免费皮肤弹窗领取、ShopPanel 默认皮肤迁移、广告/金币/内购解锁后所有权保存和穿戴应用均符合预期。
- 尚未执行 Android/iOS 横屏 smoke，快速打开 SkinPanel、快速购买/领取皮肤时的触摸与存档表现仍待真机确认。

### 回滚路径

- 回滚 `SimpleBag.GetNewSkin(...)` 的 `autoSave` 参数和空 id 防护，并恢复 `GetSkinPopup.GrantSkin(...)`、`ShopPanel_SkinPart.EnsureDefaultOwnedSkins()`、`ShopPanel_SkinPart.GrantSkin(...)` 各自直接写 `OwnedSkin` 的旧实现。

## 2026-07-09 SkinItem Special 解锁提示本地化

### 需求背景

Skin 列表中 `skin.UnlockType == SkinUnlockType.Special` 时，槽位的 `defaultPart` 文案需要使用配置提示文本并经过多语言解析。当前本地生成的 `Config.Skin` 字段名是 `UnlockHint`，对应需求中提到的 HintText/提示文本用途。

### 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs`。
- UI：ShopPanel Skin 列表单项的 `defaultPart` 文本。
- 数据来源：`Config.Skin.UnlockHint`。
- 本次未修改 prefab、Luban 配置、DB/save、资源加载、广告、IAP、analytics、RemoteConfig、平台 SDK 或全局事件。

### 原行为基线

- `ApplyUnlockState(...)` 对未拥有的 `Special` skin 会显示 `defaultPart`。
- `defaultPart` 文本统一走 `ResolveDefaultText()`，普通未拥有状态回退到 `U_Skins_Des_04`，没有使用 `UnlockHint`。
- 已拥有、Selected、Preview、Restore 文案已有独立状态文本。

### 修改后行为

- 未拥有且 `UnlockType == Special`，并且 `UnlockHint` 非空时，`defaultPart` 文本改为 `LocalizationManager.GetTranslation(UnlockHint)`。
- 如果本地化表找不到该 key 或返回空文本，则回退显示原始 `UnlockHint`，避免空白。
- 非 Special 的默认、购买、不可用、已拥有、Selected、Preview、Restore 文案保持原状态文本路径。

### 数据、UI、保存和事件流

- 新逻辑只读取只读配置字段 `Config.Skin.UnlockHint` 并刷新当前 `SkinItem` 的 TMP 文本。
- 不写入 `DB.GameData`，不保存，不触发 `ActorSkinSelectionChangedEvent`，不加载新资源，不改变 ShopPanel actionButton 的提示弹窗逻辑。

### 验证记录

- `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs` 通过，仅有当前工作区 LF/CRLF 转换提示。
- 新增 diff 静态扫描未发现 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save()`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、`Instantiate`、全局事件、广告、IAP 或 analytics 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 99 warnings；warnings 属于当前工程既有范围。

### 未验证风险

- 尚未执行 Unity PlayMode；需要验证 Special 皮肤未拥有状态下列表项 `defaultPart` 能显示对应语言文案，缺翻译时显示配置原文。
- 尚未执行 Android/iOS 横屏 smoke；快速滚动复用时 Special 文案刷新仍待设备或编辑器确认。

### 回滚路径

- 回滚 `SkinItem.ResolveDefaultText(Skin skin = null)` 的 Special 分支和 `ResolveLocalizedText(...)`，并将 `ApplyUnlockState(...)` 中的 `SetText(defaultPart, ResolveDefaultText(skin))` 恢复为 `ResolveDefaultText()`，即可恢复为 Special 未拥有状态统一显示默认状态文本。

## 2026-07-23 Skin 新服装红点按获得来源过滤

### 需求背景

SkinPart 界面内购买或广告解锁的服装不应展示新服装红点；通过奖励或免费来源获得的服装应进入待预览列表并展示红点。

### 影响范围

- 运行时代码：`Assets/Game/Scripts/Common/SimpleBag.cs`。
- 数据：沿用 `DB.GameData.OwnedSkin` 和 `DB.GameData.WaitingPreviewSkinId`，不新增存档字段。
- UI：受 `NewSkinRedDotQuery`、`NewSkinRedDotChecker` 和 `NewSkinPartRedDotChecker` 读取待预览列表的红点节点影响。
- 未修改 ShopPanel prefab、Skin 配置、资源加载、穿戴事件、广告/IAP SDK、analytics 事件结构或平台分支。

### 原行为基线

- `SimpleBag.GetNewSkin(...)` 在新增任意来源的服装所有权时，都会把服装 id 加入 `WaitingPreviewSkinId` 并刷新 `RedDotKey.NewSkin` 分支。
- `ShopPanel_SkinPart` 内金币/内购购买传 `GetSkinWayType.Purchase`，广告解锁传 `GetSkinWayType.Ad`，默认服装初始化传 `GetSkinWayType.Free`；这些来源因此也会产生新服装红点。
- 通用奖励、DailyChallenge 和 Postcard 奖励入口传 `GetSkinWayType.Reward`。

### 修改后行为

- `GetNewSkin(...)` 仍对所有合法来源新增 `OwnedSkin`、按原 `autoSave` 规则保存并保留既有获得服装打点。
- 只有 `type == GetSkinWayType.Reward || type == GetSkinWayType.Free` 时才新增 `WaitingPreviewSkinId` 并调用 `NewSkinRedDotQuery.Refresh()`。
- `Purchase` 和 `Ad` 来源不再新增待预览 id，因此 SkinPart 内金币/内购购买和广告解锁不会产生新服装红点。
- `Reward` 和 `Free` 来源仍由现有待预览列表驱动总红点、对应 SkinPart 子红点，并在玩家实际预览后沿用 `RemoveFromNewSkinList(...)` 消除。

### 数据、UI、保存和事件流

- SkinPart 内付费或广告获得：购买/广告 -> `SimpleBag.GetNewSkin(..., Purchase/Ad, ...)` -> 新增 `OwnedSkin` -> 按原调用方路径保存和应用穿戴 -> 不写 `WaitingPreviewSkinId`、不刷新红点。
- 奖励或免费获得：CommonGetRewardPopup/DailyChallenge/Postcard 使用 `Reward`，GetSkinPopup 和默认服装初始化使用 `Free` -> 新增 `OwnedSkin` 与 `WaitingPreviewSkinId` -> 保存 -> 刷新 `RedDotKey.NewSkin` 分支。
- 本次不改变 `GameSDKService.GetNewSkin(...)` 的来源参数和打点调用，也不新增全局事件或每帧查询。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Common/SimpleBag.cs` 通过，仅有当前工作区 LF/CRLF 转换提示。
- 静态检查确认 `WaitingPreviewSkinId.Add(skinId)` 位于 `GetSkinWayType.Reward || GetSkinWayType.Free` 条件内；现有外部奖励调用方使用 `Reward`，免费来源使用 `Free`，SkinPart 购买和广告解锁使用 `Purchase` 或 `Ad`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 99 个项目既有 warning。

### 未验证风险

- 尚未执行 Unity PlayMode；需要分别验证金币/内购购买和广告解锁不出现红点，以及 GetSkinPopup、默认服装初始化、CommonGetRewardPopup、DailyChallenge、Postcard 的免费或奖励来源出现总红点和对应部位红点。
- 旧版本已经写入 `WaitingPreviewSkinId` 的历史记录没有来源字段，无法可靠区分购买与外部奖励；本次只保证修改后新获得服装的来源行为，不自动清理历史待预览数据。
- 尚未执行 Android/iOS 横屏 smoke，购买、领奖后红点刷新和触摸流程仍待真机确认。

### 回滚路径

- 移除 `GetNewSkin(...)` 中的 `Reward || Free` 条件，使所有新服装重新统一加入 `WaitingPreviewSkinId` 并刷新红点，即可恢复原行为。

## 2026-08-17 三个 Skin 资源配置骨架同步

### 需求背景

本次将工作区新增的两个 Face Skin 和一个 Clothes Skin 资源同步到 Luban Skin 源配置，使资源键具备对应的 GoodsID、本地化名称键、皮肤部位、纹理资源与 prefab 资源映射。人工策划字段不做推断，继续留空等待后续补充。

### 影响范围

- 配置源表：`Luban/Configs/Datas/Skin.xlsx` 的 `actorSkin` 工作表。
- 新增 Face 资源：`Sp_zs_img_029`、`Sp_zs_img_030`，对应 Face PNG、UI PNG 与同名 prefab。
- 新增 Clothes 资源：`Sp_zs_fz_img_026`，对应 Clothes PNG、UI PNG 与同名 prefab。
- 未修改 `pencilSkin` 工作表、图片、prefab、meta、生成后的 JSON、C#、DB/save、UI、事件、analytics、RemoteConfig 或平台配置。

### 原行为基线

- `actorSkin` 原 used range 为 `A1:M80`，没有上述三个资源键对应的配置行。
- 三组运行时图片、UI 图标和同名 prefab 已存在，但配置源表尚未建立资源映射。
- `Skin.xlsx` 同时包含 `actorSkin` 和 `pencilSkin` 两个工作表，本次仅允许追加 `actorSkin`。

### 新配置行为

- 在 `actorSkin!A81:M83` 末尾追加三行配置骨架：
  - `202029 / U_Skin_Name_Eyes_29 / Face / Sp_zs_img_029 / Sp_zs_img_029`。
  - `202030 / U_Skin_Name_Eyes_30 / Face / Sp_zs_img_030 / Sp_zs_img_030`。
  - `203026 / U_Skin_Name_Outfit_26 / Body / Sp_zs_fz_img_026 / Sp_zs_fz_img_026`。
- 新填的 GoodsID、skinName、skinPart、textureResources 与 prefabResource 保持表内既有映射规则，并使用黄色标记。
- 备注、Spine、解锁方式、品质、解锁提示、金币价格、商品 ID 保持空白，没有从相邻行复制或推断。

### 数据、资源、UI、保存和事件流

- 本次只建立 Luban 源表配置骨架；未运行 Luban 生成，因此生成配置和运行时 `Config.Skin` 尚未更新。
- 资源映射依赖三组源 PNG、UI PNG 与同名 prefab；同步前已静态确认对应文件存在。
- 本次没有运行时代码、UI 流程、存档字段、全局事件、打点或 Android/iOS 平台分支变更。

### 验证记录

- 写入前独占读写检查确认 `Skin.xlsx` 未被 Excel 锁定。
- 只读分析确认恰好缺少三行；GoodsID、skinName、textureResources、prefabResource 无重复冲突，UI PNG 与同名 prefab 完整。
- 候选表重新导入后逐单元格确认原 `actorSkin!A1:M80` 值保持一致，新增范围为 `A81:M83`，公式错误扫描结果为 0。
- 确认工作表名称和顺序未变化，未修改的 `pencilSkin` used range 与全部单元格值保持一致。
- 已渲染并检查 `actorSkin` 与 `pencilSkin` 两个工作表；新增字段可见，人工字段保持空白。
- 写回前再次确认源表未锁定且 SHA-256 未变化；写回后源表 SHA-256 与已验证候选一致。

### 未验证风险

- 尚未运行 Luban 生成，未验证生成配置是否成功包含三条新记录。
- 尚未执行 Unity 导入、编译或 PlayMode，未验证三个 Skin 在 ShopPanel 列表、预览、穿戴和 Actor 应用中的实际表现。
- 尚未补充人工字段；在解锁方式、品质、价格等字段确认前，这三行仅是资源配置骨架，不应视为可投放配置。
- 尚未执行 Android/iOS 横屏设备验证。

### 回滚路径

- 从 `actorSkin` 工作表末尾删除本次新增的 `A81:M83` 三行即可恢复原配置源表；若已运行 Luban 生成，还需重新生成配置以同步回滚结果。

## 2026-09-07 ChestGift 购买奖励新皮肤红点例外

### 原因与规则边界

- 2026-07-23 的来源过滤规则继续适用于 SkinPart 内直接购买和广告解锁：`Purchase/Ad` 默认不加入待预览列表，因为玩家正在皮肤界面完成获得和预览。
- ChestGift 虽然属于购买来源，但皮肤作为礼包奖励在弹窗关闭后到账，玩家没有进入 SkinPart 查看该皮肤；原调用只新增 `OwnedSkin`，没有写入红点唯一数据源 `GameData.WaitingPreviewSkinId`。
- `SimpleBag.GetNewSkin()` 现提供默认关闭的 `markForPreview` 参数。仅 ChestGift 购买奖励显式开启，保留 `GetSkinWayType.Purchase` 打点语义，同时把真正新增的皮肤加入待预览列表并刷新 NewSkin 红点分支。

### 数据流、影响与验证

- ChestGift 新 Skin：购买成功 -> `GetNewSkin(Purchase, markForPreview: true)` -> `OwnedSkin` + `WaitingPreviewSkinId` -> `NewSkinRedDotQuery.Refresh()` -> 保存。
- 已拥有 Skin 不会重新加入待预览列表；普通商店购买、广告解锁、Reward/Free 原行为以及红点清除流程均保持不变。
- 运行时程序集构建为 `0 error / 121 warning`，`git diff --check` 通过；真实 IAP、各 SkinPart 红点和预览后清除仍为 `uncertain`。
- 回滚时移除 `markForPreview` 参数与 ChestGift 的显式开启即可；无需迁移 `OwnedSkin`，测试产生的待预览记录按既有预览或清档流程移除。
