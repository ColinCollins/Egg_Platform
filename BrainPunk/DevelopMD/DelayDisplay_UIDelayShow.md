# DelayDisplay UI 延迟显示工具

## 需求背景

本次需求要求参考 `Level61ChoicePanelKeyCtrl` 中选择面板 key 的 Show 相关流程，抽离一份可挂在 UI `Graphic` 上使用的延迟显示脚本，只保留延迟显示和渐显逻辑，不包含 Level61 的领取、存档、按钮交互、位移出屏或关卡条件判断。

## 影响文件与系统

- `Assets/Game/UI/Global/Scripts/DelayDisplay.cs`
- Unity UGUI `Graphic` 的 `enabled` 和 `color.a` 显示链路

本次未修改 prefab、场景、关卡脚本、DB、Save、RemoteConfig、Luban 配置、资源加载、事件系统、打点、平台 SDK 或热更配置。

## 原始行为基线

`Level61ChoicePanelKeyCtrl` 当前在 `TryShow()` 中会清理旧 tween，检查本地引用和 `DB.GameData.HasGetLevel61Key`，准备 key 子节点，等待 `displayDelay` 后让 `Graphic` 用 `fadeInDuration` 从 0 渐显到 1。该脚本还包含 Level61 专属的按钮领取、保存、上跳和下落移出逻辑。

本次新增脚本只复用其中“延迟后渐显 Graphic”的局部表现，不改动 `Level61ChoicePanelKeyCtrl`，避免影响已存在的 Level61 选择面板钥匙流程。

## 新行为

- `DelayDisplay` 可挂在带 `Graphic` 的 UI 节点上，`targetGraphic` 为空时自动使用当前节点的 `Graphic`。
- `Show()` 会中断旧显示 tween，将目标透明度置 0 并临时禁用 `Graphic.enabled`，等待 `displayDelay` 后启用目标并用 `fadeInDuration` 渐显到 `displayAlpha`。
- `HideImmediate()` 会中断显示 tween，透明度置 0，并禁用 `Graphic.enabled`；不会修改目标 GameObject 的 active 状态。
- `hideOnEnable` 默认开启，用于 UI 打开或对象重新启用时先隐藏目标，避免首帧露出。
- `ignoreTimeScale` 默认开启，延迟和渐显使用不受 `Time.timeScale` 影响的 UI 时间。

## 数据 / 资源 / UI 流

该脚本只操作目标 `Graphic.enabled` 和 `Graphic.color.a`。它不读取或写入存档，不访问配置表，不加载资源，不派发事件，不实例化对象，不改变 CanvasGroup、父子层级 active、按钮状态或 raycast 配置。

调用边界：`Show()` 需要调用方保证目标 GameObject 所在层级处于 active 状态；脚本不会主动激活被隐藏的 GameObject。

## 验证记录

- 已静态读取 `Level61ChoicePanelKeyCtrl.cs`，确认本次只参考其 `TryShow()` / `PlayDelayedShow()` 中的延迟和 `Graphic.DOFade` 行为。
- 已静态读取 `UIFadeAnimation.cs`，确认其职责是 Bear UI 面板级 `CanvasGroup` 开闭动画，不覆盖单个 `Graphic` 延迟显示场景。
- 已静态检查新增脚本风险入口：`rg -n "OnValidate|Reset|ExecuteAlways|ExecuteInEditMode|DB|Save|RemoteConfig|Instance|Instantiate" Assets/Game/UI/Global/Scripts/DelayDisplay.cs` 未发现匹配。
- 已执行 `dotnet build Assembly-CSharp.csproj --no-restore`，结果为 0 errors / 1117 warnings；warnings 为当前项目既有警告，本次输出中未出现 `DelayDisplay.cs` 编译错误。

## 未验证与剩余风险

- 尚未运行 Unity Editor / PlayMode 验证 Image、RawImage、Text/TMP Graphic 等实际显示表现。
- 尚未验证挂载到带 Button 的节点时，`Graphic.enabled=false` 对射线命中和按钮交互的实际影响；如业务需要隐藏期间保留点击，需要另行确认。
- 尚未在 Android / iOS 横屏真机验证暂停状态、弹窗打开、快速重复调用 `Show()` / `HideImmediate()` 的表现与性能。

## 回滚路径

删除 `Assets/Game/UI/Global/Scripts/DelayDisplay.cs` 与对应 `.meta`，并移除后续 prefab 或场景中新增的 `DelayDisplay` 组件引用即可。本次没有改动现有业务脚本，回滚不需要恢复 Level61 代码。
