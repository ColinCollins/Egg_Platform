# IWannerGame_ScaleTriggerListener 功能与风险记录

日期：2026-08-13

## 需求 / 背景

- 用户要求参考 `IWannerGame_MoveTriggerListener` 新增 `IWannerGame_ScaleTriggerListener`，用于由触发执行链改变物体缩放，并在 Inspector 暴露 X、Y 和 `duration` 参数；随后明确补充同样需要 `delay` 参数。
- 本次采用项目既有 `ItemScaleListener` 的“目标 localScale”语义：X/Y 是绝对目标值，不是每次触发累加的倍率。

## 影响文件、资源与系统

- 新增脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_ScaleTriggerListener.cs`。
- 新增 Unity 元数据：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_ScaleTriggerListener.cs.meta`。
- 新增本功能/风险记录及 Unity 自动生成的对应 `.md.meta`。
- 依赖既有 `Game.ItemEvent.BaseItemEventHandle`、`TriggerExecutor/BaseItemExecutor` 执行契约和 DOTween `DOScale`。
- 本次未修改或绑定任何 Prefab，也未修改配置、存档、资源地址、UI、全局事件、Analytics 或平台 SDK。

## 既有行为基线

- IWanner 目录已有 `IWannerGame_MoveTriggerListener`，可按 X/Y 位移和固定时长执行 Transform Tween，但没有对应的通用缩放触发监听器。
- 主线已有 `ItemScaleListener`，可把目标缩放到 Vector3 目标值；本次不修改或复用其已有脚本，以免扩大既有关卡回归范围。

## 新行为

- `IWannerGame_ScaleTriggerListener` 继承 `BaseItemEventHandle`，可加入 `TriggerExecutor/BaseItemExecutor` 的监听器列表。
- Inspector 暴露 `targetScale`（Vector2，X/Y）、`duration` 和 `delay`；`target` 未绑定时在运行时回退到组件自身 Transform。
- `Execute()` 先等待非负 `delay`，再将目标当前 `localScale` 线性过渡到配置的 X/Y 绝对值，Z 保持触发瞬间值；`duration <= 0` 且 `delay <= 0` 时立即应用。
- 缩放期间设置 `IsRunning = true`、`IsDone = false`，完成后反转状态，兼容 Sequence 执行推进。
- 重复触发会停止旧 Tween，再从当前缩放开始到同一目标值，不累计倍率。
- `ResetState()` 会停止未完成 Tween 并重置执行标志，但不会恢复触发前缩放；该边界与参考 Move Listener 的“不恢复 Transform 值”行为一致。

## 数据 / 资源 / 事件流

- 运行时链路：`TriggerExecutor/BaseItemExecutor -> Execute() -> Transform.DOScale().SetDelay() -> CompleteScale() -> IsRunning/IsDone`。
- 无 DB、Save、RemoteConfig、资源加载、实例化、UI、全局事件或打点链。
- 缩放仅改变目标 `localScale`；若目标带 Collider，Unity 物理形状会随 Transform 缩放，这是既有引擎行为，本脚本不额外重建 Collider。

## 验证

- CodeGraph 核对了 `IWannerGame_MoveTriggerListener`、`BaseItemEventHandle`、`BaseItemExecutor` 和既有 `ItemScaleListener` 的源码与调用边界。
- 新脚本 GUID `48578a815b824359a6c5b00a7fba442b` 与文档 GUID `77fd7add01def074a83569638af41885` 经仓库 `.meta` 静态搜索均未发现重复。
- 初次验证时将新脚本临时纳入本地生成的 `Assembly-CSharp.csproj`，构建结果为 `0 error / 101 warning`，随后撤销该临时改动；Unity Editor 后续已自动把脚本写入生成项目文件。
- 补充 `delay` 后直接重新执行同一定向构建，结果仍为 `0 error / 101 warning`。
- 静态确认脚本未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/Save、实例化或全局事件路径。

## 剩余风险 / 未验证缺口

- `uncertain`：尚未在具体 IWanner Prefab 中挂接该组件，因此 Inspector 序列化、Trigger 实际触发、Sequence 推进、重复触发与 Reset 行为未做 PlayMode 验证。
- `uncertain`：目标带 Collider 或 Rigidbody2D 时，缩放过程对碰撞接触、站立稳定性和移动平台乘客表现尚未按具体关卡验证。
- `uncertain`：Android/iOS 横屏真机帧率与暂停/原生覆盖层期间 DOTween 表现未验证；本次没有接入原生覆盖层流程。
- X/Y 允许负值以支持翻转；若具体关卡不允许镜像，需要在 Prefab 配置与关卡验证中限制为非负值。

## 回滚路径

- 未绑定 Prefab 时，删除新增的 `.cs`、`.cs.meta` 和本记录即可完整回滚。
- 若后续已有 Prefab 绑定，先移除对应组件或监听器列表引用，再删除脚本与元数据，避免产生 Missing Script。
