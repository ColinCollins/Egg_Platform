# DLevel_001 Guide 落笔隐藏功能与风险评估

## 1. 需求与背景

2026-07-29 需求：DLevel_001 增加对场景引导的控制，玩家开始绘制时隐藏整个 Guide。

本关 prefab 当前以 `Guide` 为根节点，内部包含 `GuideLine` 和 `Finger_Scene`。
本次落笔时停用整个 `Guide`，不改变画线输入、胜负、Actor 行走或其它关卡。

## 2. 原行为基线

- `DLevel001Ctrl` 只实现 `IDrawStrokeCompletedHandler`，在第一笔有效线完成后驱动 Actor
  向右移动。
- `DrawLineFeature.TryBeginStroke(...)` 能准确区分有效落笔、UI 点击、绘制区外触摸和
  输入关闭状态，但原先只在一笔有效线完成时派发 `StrokeCompleted`。
- `DLevel_001.prefab` 中 `Guide` 初始激活，原先没有脚本引用或运行时隐藏链。

## 3. 本次实现

### 3.1 受影响文件与资源

- `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level1/DLevel001Ctrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_001.prefab`

未修改 DB、Luban 配置、存档、资源地址、UI 面板、全局事件、Analytics、SDK 或平台代码。

### 3.2 新行为

1. `DrawLineFeature` 在有效绘制区域内成功创建活动线段后派发局部 `StrokeStarted` 事件。
2. 无效按下、UI 点击、绘制区外触摸、输入关闭或暂停状态不会派发该事件。
3. `DLevel001Ctrl` 在运行时订阅该事件并把已绑定的 `Guide` 根节点设为不激活，
   `GuideLine` 和 `Finger_Scene` 一起隐藏。
4. `OnDrawAttemptReset()` 恢复 Guide 进入本次 prefab 实例时的初始显隐；整关 Replay
   仍由新 prefab 实例恢复初态。
5. Prefab 已把 `guideRoot` 显式绑定到 `Guide` 根节点；运行时仍保留 `Guide` 固定层级
   回退，缺失时安全跳过。

## 4. 数据、资源、事件与平台流

- 数据/存档：无读取或写入。
- 资源：只使用 DLevel_001 prefab 内现有对象，不新增加载或实例化。
- UI：无 UI 事件；Guide 是场景 SpriteRenderer/Animator 组合节点。
- 事件：新增 `DrawLineFeature.StrokeStarted` C# 局部事件，只由同一 prefab 内的
  DLevel001Ctrl 订阅，不新增 `PlayEvents` 或 Bear 全局事件。
- 性能：每次有效落笔同步调用一次空参数委托；无 `Update` 轮询、查找循环或分配链。
- Android/iOS：输入仍复用 `DrawLineFeature` 既有鼠标/触摸入口，没有平台分支。

## 5. 风险项

| 风险 | 控制措施 | 当前状态 |
| --- | --- | --- |
| 无效触摸误隐藏手指 | 只在 `TryBeginStroke` 完成绘制区、UI 和输入门禁后触发 | 已静态核对 |
| Guide 子节点残留显示 | Prefab 字段绑定整个 `Guide` 根节点 | 已静态核对 |
| 订阅重复或对象退出后回调 | `OnEnable` 先解除再订阅，`OnDisable` 解除 | 已静态核对 |
| Reset 后手指不恢复 | 首次启用缓存 `activeSelf`，`OnDrawAttemptReset` 恢复 | 已静态核对 |
| Prefab 引用缺失 | 固定层级回退；仍缺失时不抛异常 | 已静态核对 |
| 共享画线行为回归 | 新事件无订阅时为空操作，不改变线段创建、完成和输入门禁 | 待 PlayMode 回归 |

## 6. 验证证据

- 已核对 `TryBeginStroke` 的有效落笔门禁与事件触发顺序。
- 已核对 DLevel_001 prefab 的 `guideRoot` 指向 `Guide` 根节点。
- 已检查本次代码没有新增全局事件、DB、资源加载、逐帧查找或编辑器生命周期入口。
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1` 通过，`0 error / 103 warning`；
  warning 未指向本次修改文件。
- Unity PlayMode 和移动端验证未执行。

## 7. 未验证缺口

- Unity PlayMode 中首次有效落笔的隐藏帧时机、点按后取消线段时仍保持隐藏的体验待验证。
- Reset/Replay 后整个 Guide 的恢复时机待验证。
- Android/iOS 横屏触摸、暂停恢复和多指输入待设备验证。

以上未验证项不作为已通过结论。

## 8. 回滚路径

1. 删除 `DrawLineFeature.StrokeStarted` 事件及 `TryBeginStroke` 中的派发。
2. 删除 `DLevel001Ctrl` 的 Guide 根节点字段、订阅、隐藏和恢复逻辑。
3. 删除 DLevel_001 prefab 中 `guideRoot` 序列化绑定。

回滚不需要迁移存档、配置或资源。
