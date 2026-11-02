# Level44 version 2.0 Star 逻辑记录

## 需求背景

2026-07-09，`Level44Ctrl` 需要补充 version 2.0 的 Star 逻辑：每次成功 throw 后执行一次随机测试，随机值大于 `0.7` 时开启 Star，并参考 `ThrowItemHandle.JumpTo` 的抛物线轨迹移动。

## 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_44/Level44Ctrl.cs`
  - 在成功生成 throw item 后触发一次 Star 随机测试。
  - 命中后把 Star 从 throw item 起点移动到它在关卡 prefab 中的配置位置。
  - 未绑定 `star` 字段时，运行时从本关卡子节点兜底查找 `Postcard_Star (1)`。
- `Assets/Game/Prefabs/Level/Level_044.prefab`
  - 静态证据显示当前工作区已有一个 inactive 的 `Postcard_Star (1)`，`index` 为 `2`。
  - 本次脚本未继续手改 prefab 绑定，避免混入任务开始前已有 prefab 改动。
- 相关既有系统：
  - `ThrowItemHandle.SpawnItem()` 和 `JumpTo(...)` 提供抛物线移动参考。
  - `Postcard_Star` 继续负责玩家触发后的永久星记录、背包 Star 奖励、`PostcardGetStarEvent` 派发和自身隐藏。

## 原有行为基线

- `Level44Ctrl.SpawnThrowItem(...)` 原本只生成投掷物、更新拖拽生成进度、调整重量和完成门逻辑。
- `Level44Ctrl` 中已有 version 2.0 注释和 `star` 序列化字段，但没有运行时代码触发 Star。
- `Postcard_Star` 原本只在自身 active 且玩家进入 2D trigger 时收集；inactive 的 `Postcard_Star (1)` 不会由 Level44 throw 流程激活。

## 当前行为

- `SpawnThrowItem(...)` 只在实际生成至少一个 throw item 后调用 `TryTriggerStarFromThrow(...)`。
- 每次成功 throw 只随机一次；当 `Random.value > 0.7f` 且本局尚未触发过 Star 时，记录 `_hasTriggeredStar = true`。
- Star 终点使用它在 prefab 中的原始 world position；起点使用本次第一个 throw item 的生成位置，缺失时回退到 `throwItems.transform.position`。
- Star 移动复用 `ThrowItemHandle.JumpTo` 的同类公式：线性进度 + `4 * jumpPower * t * (1 - t)` 抛物线弧高。
- 如果玩家已经收集过该 `Postcard_Star`，它在启用时会由自身 `Awake()` / 收集状态逻辑重新隐藏；`Level44Ctrl` 不绕过 `Postcard_Star` 的去重、保存或奖励流程。
- `OnDestroy()` 会停止 Star tween，避免关卡销毁后仍有 DOTween 回调访问目标。

## 数据与流程

1. 玩家点击或拖拽触发 `Level44Ctrl.SpawnThrowItem(...)`。
2. `ThrowItemHandle.SpawnItem()` 生成投掷物并启动自身抛物线。
3. `Level44Ctrl` 在生成成功后对 Star 做一次随机测试。
4. 命中时启用 Star，并把 Star 从投掷起点抛到 prefab 配置位置。
5. 玩家碰到 Star 后进入 `Postcard_Star.OnTriggerEnter2D()`，由 `PostcardManager.TryCollectLevelStar(...)`、`Bag.AddTool(GameProps.Star, 1, RewardType.Other)` 和 `PostcardGetStarEvent` 继续处理。

本次未新增 DB 字段、配置项、资源加载路径、全局事件、广告、IAP、analytics、RemoteConfig、平台 SDK 或每帧轮询。

## 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 99 warnings。
- 静态扫描 `Level44Ctrl.cs`：未发现新增 `DB.`、`SavePostcard`、`PlayEvents`、`DispatchEvent`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、`RemoteConfig`、`PlayerPrefs`、`Resources`、`YooAsset`、`Analytics`、`Advertisement` 或 `IAP`；仅存在原有 `OnValidate()`。
- 静态证据：`Level_044.prefab` 中存在 `Postcard_Star (1)`，且 `m_IsActive` 覆盖为 `0`。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_44/Level44Ctrl.cs` 当前失败，阻断点是任务开始前已有的 version 2.0 注释空行和 `star` 字段前空白行的 trailing whitespace；本次按用户“不改注释”要求未清理。

## 剩余风险与未验证项

- 尚未在 Unity PlayMode 实际触发 Level44 throw 流程，Star 命中概率、抛物线终点、玩家碰撞收集和 HUD 飞星表现仍待补验。
- 尚未验证 Android/iOS 横屏设备上的触控、碰撞和帧率表现。
- 兜底查找依赖当前对象名 `Postcard_Star (1)`；如果后续 prefab 重命名且 Inspector 字段仍未绑定，需要重新绑定或调整兜底。

## 回滚路径

回退 `Level44Ctrl.cs` 中 Star 常量、运行时字段、`CacheStarReference()`、`TryTriggerStarFromThrow(...)`、`JumpStarTo(...)`、`StopStarAnimation()`、`SpawnThrowItem(...)` 调用点和 `OnDestroy()` 清理，即可恢复到只生成 throw item、不激活 Star 的旧行为。
