# CommonUIReward 流程稳定性修复记录

## 1. 需求与背景

2026-08-07 排查 Android 商店包后确认：设置页点击 Youtube、Tiktok 等外链时，金币已先写入背包并保存领取状态，随后 `Application.OpenURL` 让应用进入后台。原实现同一帧启动飞金币动画，Unity PlayerLoop 在原生应用覆盖期间停止推进，因而可能出现“金币到账但动画没有完整播放”。

本次只处理以下范围：

- 社交外链返回前台后补播飞金币动画。
- 修正 `CommonUIReward` 对自然完成、替换、禁用、销毁和中断的结果语义及日志。
- Shop 与 Victory 奖励调用方等待或显式处理动画结果。

不调整对象池注册所有权，不新增全局事件，不修改奖励数值、领取资格、存档结构、广告或内购 SDK 接口。

## 2. 影响文件与系统

- `Assets/Game/UI/Global/Scripts/CommonUIRewardAnimCtrl.cs`
- `Assets/Game/UI/Global/Scripts/CommonUIRewardItem.cs`
- `Assets/Game/UI/Global/Resources/CommonUIReward.prefab`
- `Assets/Game/UI/Global/Resources/CommonUIRewardItem.prefab`
- `Assets/Game/UI/SettingPopup/Scripts/MediaShareItem.cs`
- `Assets/Game/UI/SettingPopup/Scripts/CustomButtonOpenUrl.cs`
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs`
- `Assets/Game/UI/ShopPanel/Scripts/ShopRewardTipPopup.cs`
- `Assets/Game/UI/ShopPanel/Resources/ShopPanel.prefab`
- `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_Reawrd.cs`
- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel_Reward.cs`

涉及设置页社交奖励、公共飞金币动画、商店广告/内购金币奖励、主线及 DrawGame 胜利页广告金币奖励。

## 3. 修改前行为

### 3.1 社交外链

`MediaShareItem.OnClickShare()` 发奖并保存领取状态后立即调用 `rewardAnimCtrl.PlayFrom(...).Forget()`；同一按钮上的 `CustomButtonOpenUrl` 随即调用 `Application.OpenURL`。两个监听器之间没有“动画排队/返回补播”协议，也依赖按钮监听顺序。

### 3.2 CommonUIReward 结果

- `CommonUIRewardItem` 的 DOTween `OnComplete` 与 `OnKill` 共用同一个完成回调。
- 动画被替换、控制器禁用、对象回收或销毁时，单个 Item 的等待任务仍按完成结束。
- `CommonUIRewardAnimCtrl` 只要创建过至少一个 Item 并等到任务结束就返回 `true`，因此被 Kill 的批次也可能被调用方理解为成功完成。
- Shop 调用使用 `Forget()` 且没有读取结果；Victory 虽然 `await`，但忽略了原 `bool` 返回值。

## 4. 修改后行为

### 4.1 外链返回补播

同一物体同时存在 `MediaShareItem` 与 `CustomButtonOpenUrl` 时，由 `MediaShareItem` 接管点击顺序：

1. 未领取时先发放金币、保存领取状态并登记一条待播放请求。
2. 再显式调用 `CustomButtonOpenUrl.OpenUrl()` 打开外链。
3. `OnApplicationPause` / `OnApplicationFocus` 记录是否真正离开应用。
4. 返回前台且恢复焦点后等待一帧，再播放排队的飞金币动画。
5. Pause 与 Focus 可能各触发一次恢复回调，但版本号和待播放标记保证同一奖励最多启动一次。
6. 若目标平台打开 URL 后没有让应用失焦或暂停，则使用 0.75 秒非缩放时间兜底播放，避免奖励动画永久排队。

已领取条目仍能继续打开外链，但不会重复发奖或重复排队动画。

### 4.2 播放结果语义

`CommonUIRewardAnimCtrl` 返回 `CommonUIRewardPlayResult`：

- `Completed`：请求数量的所有 Item 都自然完成 DOTween Sequence。
- `Replaced`：同一控制器上的新播放请求替换旧批次。
- `ControllerDisabled`：控制器禁用导致批次终止。
- `ControllerDestroyed`：控制器销毁导致批次终止。
- `InvalidRequest`：模板、根节点、起点、终点或坐标转换无效。
- `NoItemsSpawned`：没有成功创建动画 Item。
- `Interrupted`：Item 被非自然 Kill、部分创建失败或其它中断。

`CommonUIRewardItem.PlayAsync()` 仅在 DOTween `OnComplete` 时返回 `true`；`OnKill`、回收和销毁返回 `false`。控制器为每个批次保留独立会话及停止原因，新批次、禁用和销毁不再共用模糊的“取消”日志。

日志现在包含批次开始、版本、请求/创建数量和最终结果。社交、Shop、ShopRewardTipPopup、主线 Victory Reward 与 Draw Victory Reward 都会记录非 `Completed` 结果。

### 4.3 奖励数据流保持不变

- 社交奖励仍先调用 Bag `AddTool(..., RewardType.Media)`，再保存 `GameSetting` 对应领取标记。
- Shop 广告仍按 `TbShop` 发奖；IAP 金币仍在购买成功回调中发放。
- Victory 广告奖励仍在选择器停止后发放，再等待收集动画和刷新道具显示。
- 动画结果不回滚已到账奖励，也不重复补发奖励；它只用于表现补播、调用方等待和诊断日志。

### 4.4 ShopPanel 社媒按钮绑定修复

`ShopPanel.prefab` 的 Youtube、Instagram、Discord、Tiktok 节点虽然都已挂载 `MediaShareItem`，但四个组件的 `rewardAnimCtrl` 原先均为空，因此奖励到账后只能记录“Controller is null”，无法播放或补播动画。当前四个节点统一绑定到 ShopPanel 已有的 `CommonUIRewardAnimCtrl`，不新增控制器实例，也不修改社媒领取状态或奖励数值。

### 4.5 社媒飞金币数量

Youtube、Instagram、Discord、Tiktok 的单次领取动画固定只创建 1 个飞金币 Item。实际发放金币数量仍读取 `GetMediaShareCoinReward()`，本次只减少视觉对象数量，不改变背包增量、领取状态或其它 Shop/Victory 奖励动画数量。

### 4.6 AAB 中模板引用缺失的兜底修复

2026-08-07 连接真机采集到以下直接证据：

- 14:44:46，Shop 广告奖励进入 `ShopPanel.OnWatchAdResult()` 后，`CommonUIRewardAnimCtrl.Play()` 报错 `Reward item template is missing`。
- 14:45:07，主线 Victory Reward 创建控制器时，`CommonUIRewardAnimCtrl.Awake()` 报错无法从 `Resources/CommonUIRewardItem` 加载 prefab。
- 14:47:29，主线 Victory 广告回调进入 `GameVictoryPanel_Reawrd.OnRewardAdCallback()` 后再次因模板缺失跳过播放。
- 14:50:34，同一旧包的 `ShopRewardTipPopup` 成功注册 Item 对象池并启动 8 个 Item，14:50:36 记录 `result:Completed, requested:8, spawned:8`；说明公共 Tween 流程在模板有效时可以完成，故不同入口的序列化模板状态差异是造成“不稳定”的关键条件。

因此该包中的失败发生在动画创建入口，不是 DOTween 中途 Kill、TimeScale、渲染层级或返回前台恢复失败。代码的备用加载路径固定为 `Resources.Load("CommonUIRewardItem")`，但原资源位于 `Assets/Game/UI/Global/Prefabs/CommonUIRewardItem.prefab`，备用加载在序列化引用为空时必然失败。

当前将 `CommonUIRewardItem.prefab` 及其 `.meta` 移入 `Assets/Game/UI/Global/Resources/`，保留原 GUID `87095b3c4c3a2eb42844dab7399a3163`，并在 `CommonUIReward.prefab` 上配置同一模板为默认引用。既有 Shop、Victory、DrawGame Victory 和其它 prefab 的 GUID 引用保持不变；即使构建产物中的实例 override 再次为空，控制器仍可使用公共默认引用或 `Resources.Load` 兜底。

## 5. 验证

- 确认 `GameSettingPopup.prefab`、`GameSettingPopup2.prefab` 与 `ShopPanel.prefab` 的 Youtube、Instagram、Discord、Tiktok 条目均在同一 GameObject 上绑定 `MediaShareItem` 和 `CustomButtonOpenUrl`。
- 确认 `ShopPanel.prefab` 四个 `MediaShareItem.rewardAnimCtrl` 均绑定到同一现有奖励控制器，且引用不是空值。
- `git diff --check`：本次目标文件没有新增空白错误；命令仍报告用户已有字体资源尾随空格，与本次修改无关。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`：通过，`0 error / 1119 warning`；警告为项目既有告警。
- Unity Asset Pipeline 已成功导入移动后的 `CommonUIRewardItem.prefab`、公共控制器及所有引用该 GUID 的 Shop/Victory prefab，导入日志未出现 Missing Reference 或 prefab 导入失败。
- 静态核对确认移动前后 `.meta` GUID 未变化，`CommonUIReward.prefab.rewardItem` 与所有消费方 override 均指向同一组件 fileID/GUID；代码备用路径与新 `Resources/CommonUIRewardItem.prefab` 路径一致。

## 6. 剩余风险与未验证项

- Android Google Play AAB、iOS 真机从各社交应用/浏览器返回后的实际 Pause/Focus 顺序尚未真机回归，标记为 `uncertain`。
- 若用户在外部应用期间通过系统回收、主动关闭或流程切换销毁设置页，原控制器不存在，待播放表现会随对象销毁取消；金币与领取存档仍保留，不会重复发奖。
- 同一 `CommonUIRewardAnimCtrl` 的新请求仍会主动替换旧请求，这是现有设计；现在会明确返回并记录 `Replaced`，本次不改变其并发策略。
- 本次未调整全局对象池的注册/注销所有权，相关跨控制器风险不在本次范围。
- 当前连接手机安装的仍是修改前构建，只有重新构建并安装 APK/AAB 后才能验证飞金币恢复；Google Play 重新打包、资源拆分和商店下载链路仍标记为 `uncertain`。
- 真机日志还显示不同 `CommonUIRewardAnimCtrl` 生命周期内存在共享对象池重复注册/注销现象；本次失败发生在模板校验之前，与该现象不是同一直接原因，本次不扩改对象池所有权。

## 7. 回滚路径

按以下边界可独立回滚：

1. 回滚 `MediaShareItem.cs` 与 `CustomButtonOpenUrl.cs` 可恢复“点击即播放并由外链组件独立监听”的旧流程。
2. 回滚 `ShopPanel.prefab` 的四个 `rewardAnimCtrl` 引用会恢复 Shop 社媒奖励动画必然跳过的旧状态。
3. 回滚两个 CommonUIReward 脚本可恢复 `UniTask<bool>` 与 Kill 视为完成的旧语义；同时必须回滚所有读取 `CommonUIRewardPlayResult` 的调用方。
4. 回滚 Shop/Victory 调用文件只会移除结果处理日志，不影响奖励发放数据。
5. 将 `MediaShareItem.PlayFrom(rewardStart, 1)` 恢复为不传数量，可恢复使用控制器默认批次数量的旧表现。
6. 将 `CommonUIRewardItem.prefab` 与原 `.meta` 移回 `Assets/Game/UI/Global/Prefabs/`，并把 `CommonUIReward.prefab.rewardItem` 恢复为空，可回滚本次资源兜底；必须同时保留原 GUID，避免破坏所有消费方引用。
