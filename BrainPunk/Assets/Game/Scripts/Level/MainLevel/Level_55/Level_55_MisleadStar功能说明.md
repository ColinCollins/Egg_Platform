# Level_55 误导刷除 Star 显示功能说明

## 需求背景

2026-07-09 用户要求为 `Level_55Ctrl` 补充 `misleadCtrl` 的 `CompleteCallback` 逻辑：每次误导刷除完成时 `completeCount++`，当 `completeCount >= maxCount` 时显示 `star`。

## 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_55/Level55MisleadCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_55/Level_55Ctrl.cs`
- `Assets/Game/Scripts/Postcard/Postcard_Star.cs`
- `Assets/Game/Prefabs/Level/Level_055.prefab`
- 相关系统：`SceneSpriteBrushClear.Completed`、`Level55MisleadCtrl.completeCallback`、`Level_55Ctrl` 本关局部计数、`Postcard_Star (1)` 节点显示。

本次不新增 DB/save 字段、配置表、资源加载路径、全局事件、广告、IAP、analytics、RemoteConfig 或平台 SDK 链路。

## 原行为基线

- `Level55MisleadCtrl` 订阅 `SceneSpriteBrushClear.Completed` 后，仅在刷除完成时推进 `index` 并调用 `ApplySprites()` 切换 `frontSprite` / `backSprite` / brush sprite。
- `Level_55Ctrl` 已存在拖拽渐变、背景变色、装饰星缩放、门显示和音效流程。
- 任务开始前工作区已有 `misleadCtrl`、`star`、`maxCount`、`completeCount` 的脚本字段改动，但尚未完成回调触发、计数订阅和 prefab 字段绑定。

## 新行为

- `Level55MisleadCtrl.OnBrushCompleted()` 在有效 sprite 数量大于 0 时，完成原有 sprite 切换和 brush 刷新后调用 `completeCallback`。
- `Level_55Ctrl` 在运行态 `OnEnable` 绑定 `misleadCtrl.completeCallback`，`OnDisable` / `OnDestroy` 解除绑定，避免重复订阅。
- `Level_55Ctrl.Awake()` 在运行态将配置的 `star` 初始隐藏，并把 `completeCount` 重置为 0。
- 每次 `OnMisleadComplete()` 执行 `completeCount++`；当计数大于等于 `maxCount` 时，调用 `star.gameObject.SetActive(true)` 显示星星。
- `Level_55Ctrl` 显示星星后会调用 `Postcard_Star.RefreshSavedCollectionState()`，复用既有存档去重判断；如果该星已收集，`Postcard_Star` 会沿用原 `ApplyCollectedState()` 再次隐藏。
- `Postcard_Star.RefreshSavedCollectionState()` 是只读刷新入口，只读取当前关卡 Star 收集状态，不发奖、不写存档、不派发收集事件。
- `Level_055.prefab` 将 `misleadCtrl` 绑定到同关卡根节点的 `Level55MisleadCtrl`，将 `star` 绑定到现有 `Postcard_Star (1)` Transform，`maxCount` 保持 5。

## 数据、资源、UI 与事件流

`SceneSpriteBrushClear.Completed` 触发后，`Level55MisleadCtrl` 先刷新误导图 sprite 和 brush 状态，再通过局部 `Action completeCallback` 通知 `Level_55Ctrl`。`Level_55Ctrl` 只更新本关内存计数和星星节点 Active 状态，并在显示后让 `Postcard_Star` 复用原存档状态判断；玩家触碰后的 Star 收集、发奖、保存和事件仍由既有 `Postcard_Star` 流程处理。

## 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 99 warnings。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_55/Level_55Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_55/Level55MisleadCtrl.cs Assets/Game/Scripts/Postcard/Postcard_Star.cs Assets/Game/Scripts/Level/LevelItem/Level_55/Level_55_MisleadStar功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_55/Level_55_MisleadStar风险评估.md DevelopMD/已有功能设计与使用记录.md` 已通过，仅有 LF/CRLF 提示。
- 目标脚本静态扫描未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、Instance、Instantiate、PlayEvents、DispatchEvent 或 `Update()` 链路。
- `git diff --check -- Assets/Game/Prefabs/Level/Level_055.prefab` 被任务开始前已有 Unity 空字段行尾空白阻断，命中 `m_Name:` 和 `m_EditorClassIdentifier:` 空字段；本次未清理该无关 YAML 空白。

## 剩余风险与未验证缺口

- 未运行 Unity PlayMode，尚未实机验证连续刷除 5 次后 `Postcard_Star (1)` 显示、触碰收集、重开关卡初始隐藏。
- 未验证 Android/iOS 横屏设备表现。
- `Level_055.prefab` 还有任务开始前已有改动，本次只追加 `Level_55Ctrl` 三个字段绑定；其他 prefab 改动归属未验证。

## 回滚方式

移除 `Level55MisleadCtrl` 中的 `completeCallback?.Invoke()`，移除 `Level_55Ctrl` 的误导完成绑定、计数和 `SetMisleadStarVisible` 逻辑，移除 `Postcard_Star.RefreshSavedCollectionState()`，并从 `Level_055.prefab` 移除 `misleadCtrl`、`star`、`maxCount` 三个新增序列化字段，可回到刷除只切换误导图、不控制该星星显示的状态。
