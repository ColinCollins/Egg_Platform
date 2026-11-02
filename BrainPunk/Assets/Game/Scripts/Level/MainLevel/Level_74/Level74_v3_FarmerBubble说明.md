# Level74 v3.0 Farmer Bubble 功能说明

## 需求背景

- `Level74Ctrl.cs` 顶部 v3.0 注释要求补充 Bubble 控制：Farmer 触发 dig/Hoe 后不要取消 click 点击能力，后续点击显示 Bubble，Bubble scale 放大到 1/初始缩放，约 3 秒后 scale 变为 0。
- 本次按当前 `hoeReleased` 状态解释需求：首次点击 Farmer 仍释放 Hoe 并自动切换到 Hoe；Hoe 已释放后，Farmer 后续点击只显示 Bubble，不再重复释放 Hoe，也不改变当前草、门或工具完成状态。

## 影响范围

- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_074.prefab`
- `specs/027-level74-v3-bubble/spec.md`
- `specs/027-level74-v3-bubble/checklists/requirements.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74_v3_FarmerBubble风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

## 行为前后对比

- v3.0 前：首次点击 Farmer 后 `farmerClick` 会被关闭，Farmer 不再接受点击。
- v3.0 后：首次点击 Farmer 后仍释放 Hoe、播放 Farmer idle、启用 Hoe 并切换到 Hoe；同时保留 Farmer 点击能力。
- Hoe 已释放后，再次点击 Farmer 会显示 `farmerBubble`：从 scale 0 放大到初始 authored scale，保持 `farmerBubbleShowDuration = 3` 秒，再按 `farmerBubbleScaleDuration = 0.2` 秒缩回 0 并隐藏。
- Bubble 为 Level74 本地世界表现，不写入 save/config，不触发 analytics，不走资源加载，不影响 Fire、Grass、root、door 或 GamePlayPanel cut/dig 状态。

## Prefab 绑定

- `Level_074.prefab` 中已有 `Bubble` 对象，当前绑定到 `Level74Ctrl.farmerBubble`。
- `Level74Ctrl.farmerBubbleShowDuration` 当前为 3 秒。
- `Level74Ctrl.farmerBubbleScaleDuration` 当前为 0.2 秒。

## 数据、资源、事件和平台流

- 数据：不新增存档字段、不修改 Luban 配置、不写 PlayerPrefs。
- 资源：复用 `Level_074.prefab` 内已有 Bubble 对象，不新增运行时实例化或资源加载。
- 事件：复用 Level74 已有 `GameResetEvent`、`SwitchGameStateEvent` 清理路径，不新增跨模块事件。
- 平台：不修改 Android/iOS 平台配置；移动端仅依赖现有 Farmer 点击触摸路径。

## 生命周期清理

- `ResetRuntimeState()` 会隐藏 Bubble、缩放归 0、清理 Bubble tween，并重置 Farmer click count。
- `SwitchGameStateEvent.SUCCESS/FAILED`、`OnDisable()`、`OnDestroy()` 的清理路径会隐藏 Bubble 并清理延迟回调。
- 后续重复点击 Farmer 会先 kill 旧 Bubble sequence，再重新从 scale 0 播放，避免生成重复 Bubble 或叠加延迟回调。

## 验证记录

- 静态检索确认 `Level74Ctrl` 新增 `farmerBubble`、`ShowFarmerBubble()`、`ResetFarmerBubbleState()`，且 `Level_074.prefab` 已将 `farmerBubble` 绑定到现有 `Bubble` Transform。
- 已执行 `dotnet build D:\GitStore\BrinPunk\BrainPunk\Assembly-CSharp.csproj --no-restore`，结果为 0 error / 1115 warning。warning 为项目既有类型，未发现本次 v3.0 新增编译错误。
- 2026-06-22 Speckit implement 复核：再次静态检索确认首次 Farmer 点击路径仍设置 `hoeReleased = true`、保持 `farmerClick` enabled、调用 `ResetClickCount()`、启用 Hoe 并 `SelectTool(ToolType.Hoe)`；`hoeReleased == true` 后只调用 `ShowFarmerBubble()`。
- 2026-06-22 Speckit implement 复核：`ResetRuntimeState()` 会重置 `hoeReleased`、隐藏 Bubble、重置 Farmer click count；`SwitchGameStateEvent.SUCCESS/FAILED`、`OnDisable()`、`OnDestroy()` 均会进入 Bubble 清理路径。
- 2026-06-22 Speckit implement 复核：`Level_074.prefab` 静态确认 `farmerBubble` 指向已有 `Bubble`，`farmerBubbleShowDuration = 3`，`farmerBubbleScaleDuration = 0.2`，Farmer `OnClickEvent` 指向 `ClickFarmer`，Hoe `OnClickEvent` 指向 `ClickHoe`。
- 2026-06-22 Speckit implement 复核：再次执行 `dotnet build .\Assembly-CSharp.csproj --no-restore`，结果 0 error / 1118 warning；warning 未阻断本次 Level74 v3 静态验收，仍按当前工程 warning debt 处理。

## 未验证缺口

- 尚未执行 Unity Editor PlayMode，需验证首次 Farmer 点击释放 Hoe、后续 Farmer 点击 Bubble 显示/3 秒隐藏、重复点击重置计时、restart/fail/success/exit 清理。
- 尚未执行 Unity Inspector 视觉确认，需确认 Bubble 初始位置、层级、sorting、scale 和美术表现符合策划预期。
- 尚未执行 Android/iOS 横屏触摸验证，需确认 Farmer 连点、工具点击、Grass/Fire 交互附近的触摸不会互相干扰。

## 回滚路径

- 回滚 `Level74Ctrl.cs` 中 Farmer Bubble 字段、`ClickFarmer()` v3.0 分支和 Bubble helper。
- 回滚 `Level_074.prefab` 中 `farmerBubble`、`farmerBubbleShowDuration`、`farmerBubbleScaleDuration` 绑定。
- 规格和文档为记录性文件；如回滚运行时行为，可同步删除或标注本文件为已回滚。
