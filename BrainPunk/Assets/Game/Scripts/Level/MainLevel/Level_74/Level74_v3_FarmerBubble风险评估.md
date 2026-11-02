# Level74 v3.0 Farmer Bubble 风险评估

## 原始行为基线

- v3.0 前，Level74 的完成目标仍是 Hoe 永久移除全部 root 后由 `Level74Ctrl.CheckDoorOpen()` 调用 `OpenDoor()`。
- v3.0 前，Farmer 首次点击释放 Hoe，并通过关闭 `farmerClick` 阻止后续 Farmer 点击。
- v2.0 Fire burn 是 leaf 临时视觉路径，不移除 root、不触发 door completion；本次 v3.0 不改变该边界。

## 本次影响文件与资源

- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_074.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74_v3_FarmerBubble说明.md`
- `specs/027-level74-v3-bubble/spec.md`
- `specs/027-level74-v3-bubble/checklists/requirements.md`
- `DevelopMD/已有功能设计与使用记录.md`

## 现有系统使用

- `ClickItemEventHandle`：继续作为 Farmer 点击入口；本次保留点击能力并重置 click count，避免 `maxClickCount` 或历史计数阻断后续 Bubble 点击。
- DOTween：用于 Bubble scale 0 -> authored scale -> 0 的本地表现；restart/fail/success/disable/destroy 会 kill sequence。
- `SwitchGameStateEvent` / `GameResetEvent`：复用 Level74 已有事件订阅路径清理 Bubble 和其他本地运行态。
- `Level_074.prefab`：复用已存在的 `Bubble` Transform，不新增运行时资源加载和实例化。

## 已规避风险

- P1：Farmer 后续点击被永久禁用，导致 v3.0 Bubble 无法触发。处理：首次释放 Hoe 后保持 `farmerClick` enabled。
- P1：Bubble 延迟回调在 restart/fail/success/exit 后残留。处理：`ResetRuntimeState()`、`CleanupFireRuntime()`、`Cleanup()` 均调用 `ResetFarmerBubbleState()`。
- P1：重复点击 Farmer 生成多个 Bubble 或多个延迟回调。处理：`ShowFarmerBubble()` 每次先 kill 旧 sequence，再从 scale 0 重放。
- P1：Bubble 缺失阻断关卡完成。处理：`farmerBubble == null` 时只跳过 Bubble 显示，不影响首次释放 Hoe 或 root completion。
- OBS：引入共享 Bubble 抽象扩大回归面。处理：本次保持 Level74 本地实现，不修改其他关卡或共享脚本。

## 剩余风险

- P1：Bubble 的 authored 位置、scale、sorting、父节点与 Farmer 视觉关系尚未通过 Unity Inspector/PlayMode 确认，可能出现显示位置或遮挡不符合策划预期。
- P1：后续 Farmer 点击在工具动画、Fire burn、UI cut/dig 显示期间的实际触摸竞争尚未 PlayMode/移动端验证。
- P1：`ClickItemEventHandle` 的 Raycast 命中要求 collider 对象本身命中；当前静态证据显示 Farmer click 事件已绑定，但仍需 PlayMode 验证后续点击能命中 Farmer 并显示 Bubble。
- OBS：当前工作区同时包含 Level74 prefab、Grass prefab 化、字体、平台配置等其他未提交改动；本次结论仅覆盖 v3.0 Bubble 相关新增字段和代码路径。

## 验证证据

- 静态检索：`Level74Ctrl.cs` 存在 `farmerBubble`、`ShowFarmerBubble()`、`ResetFarmerBubbleState()`；未再保留 Level74 内 `farmerClick?.SetEnableClick(false)`。
- Prefab 静态证据：`Level_074.prefab` 的 `Level74Ctrl` 已序列化 `farmerBubble: {fileID: 2284642791940186871}`，该 fileID 对应名为 `Bubble` 的 Transform。
- 编译：已执行 `dotnet build D:\GitStore\BrinPunk\BrainPunk\Assembly-CSharp.csproj --no-restore`，结果 0 error / 1115 warning。
- 2026-06-22 Speckit implement 复核：`ClickItemEventHandle` 本地实现公开 `ResetClickCount()`，Level74 首次释放 Hoe 和 `ResetRuntimeState()` 均调用该方法；因此 Farmer 首次点击后的历史 click count 不应阻断后续 Bubble 点击。
- 2026-06-22 Speckit implement 复核：`Level74Ctrl.ShowFarmerBubble()` 在 `farmerBubble == null` 时直接返回，缺失 Bubble 不会阻断首次 Hoe release 或 root completion；重复点击会先 kill 旧 sequence，再从 scale 0 重新播放。
- 2026-06-22 Speckit implement 复核：`Level74Ctrl` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、analytics 或平台 SDK 调用。
- 2026-06-22 Speckit implement 复核：再次执行 `dotnet build .\Assembly-CSharp.csproj --no-restore`，结果 0 error / 1118 warning；warning 未逐项归因到本功能，仍需按当前工程 warning debt 跟踪。

## 未验证缺口

- Unity Editor PlayMode：首次 Farmer 点击释放 Hoe、后续 Farmer 点击显示 Bubble、3 秒自动隐藏、重复点击重置计时。
- Unity Editor PlayMode：restart/fail/success/exit/disable/destroy 中 Bubble 是否无残留。
- Unity Inspector：Bubble 的初始位置、scale、sorting 和最终视觉是否符合关卡美术。
- Android/iOS 横屏触摸：Farmer 连点、工具点击、Grass/Fire 交互同时存在时是否无误触或软锁。

## 回滚路径

- 回滚 `Level74Ctrl.cs` 中 Farmer Bubble 字段、`ClickFarmer()` 保持点击/后续显示 Bubble 分支、Bubble helper 和清理调用。
- 回滚 `Level_074.prefab` 中 `farmerBubble`、`farmerBubbleShowDuration`、`farmerBubbleScaleDuration` 三个序列化字段。
- 规格与文档不影响运行时；若运行时回滚，可同步标注本文件和 `specs/027-level74-v3-bubble` 为已回滚。
