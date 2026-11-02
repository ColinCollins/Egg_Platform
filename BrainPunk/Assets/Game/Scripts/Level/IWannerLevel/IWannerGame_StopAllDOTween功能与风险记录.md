# IWannerGame_StopAllDOTween 功能与风险记录

## 需求背景

IWanner 关卡需要一个可由 `TriggerExecutor` / `BaseItemExecutor` 或 UnityEvent 调用的独立组件，
用于立即停止指定 `Transform` 当前关联的全部 DOTween 表现，且不把 Tween 强制推进到终点。

## 相似能力调查

- 项目大量脚本通过 `Transform.DOKill()` 清理明确对象的 Tween，但调用都散落在各自控制器或组件生命周期中。
- `DOTweenAnimation.DOKill()` 可停止该 DOTweenAnimation 配置目标关联的 Tween，但它属于 DOTween Pro
  表现组件自身接口，不是可配置任意 IWanner `Transform` 的关卡执行 Listener。
- `MoveAnimation.KillTween()`、`BreathLineAlphaHandle.StopBreath()` 等接口只清理组件自己保存的 Tween 引用。
- 本地搜索没有发现 `IWannerGame_StopAllDOTween`、`StopAllDOTween` 或等价的通用 IWanner 组件，因此本次新增局部脚本。

## 影响范围

- 新增脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_StopAllDOTween.cs`
- 复用系统：`Game.ItemEvent.BaseItemEventHandle`、DOTween `Transform.DOKill(false)`
- 未修改或绑定 prefab、关卡资源、共享 Actor、全局事件、DB、存档、配置、资源加载、UI、打点或平台 SDK

## 原有行为基线

- IWanner 现有 Move/Scale 等 Listener 各自管理创建的 Tween，Reset 或销毁时只停止自身保存的 Tween。
- 关卡若要从另一个执行节点停止指定 Transform 的 Tween，只能在关卡脚本或 UnityEvent 中逐处增加 `DOKill()` 调用。
- 没有独立组件提供可复用的 `target Transform -> Kill` 执行边界。

## 新行为

- Inspector 提供 `target`；未配置时使用组件自身 `transform`。
- `Execute()` 仅在 PlayMode 生效，调用 `target.DOKill(false)` 后立即把 `IsRunning` 置为 false、`IsDone` 置为 true，
  可供顺序执行器继续推进。
- `false` 表示停止前不强制完成 Tween，因此目标保持 Kill 时刻的当前位置、缩放或旋转等当前值；
  Tween 的 `OnKill` 回调仍由 DOTween 正常执行。

## DOTween 目标匹配边界

- 本组件停止所有以目标 `Transform` 作为 DOTween target 的 Tween，例如通过该 Transform 调用
  `DOMove`、`DOLocalMove`、`DOScale` 或 `DORotate` 创建的常规 shortcut Tween。
- 不递归停止子节点 Transform 的 Tween。
- 不保证停止以 `GameObject`、`SpriteRenderer`、`CanvasGroup` 等其它对象注册为 target 的 Tween。
- 裸 `DOTween.To(...)` 若没有显式 `.SetTarget(targetTransform)`，即使其 setter 最终修改同一对象，也不会被
  `targetTransform.DOKill()` 匹配。需要被本组件统一停止的自定义 Tween，创建时必须显式绑定该 Transform target。
- 若目标 Tween 所属 Listener 只在 `OnComplete` 更新执行状态、没有处理 `OnKill`，外部 Kill 后该 Listener 可能仍保持
  `IsRunning=true`；接入顺序执行链时需核对目标 Listener 的中断契约。

## 验证记录

- 执行 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:quiet -clp:ErrorsOnly`：
  通过，`0 error / 109 warning`；warning 为工程既有项。
- 新脚本定向空白检查未发现 trailing whitespace 等错误，仅有工作区 LF/CRLF 转换提示。
- 静态扫描未发现 `Update`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、
  RemoteConfig、Instance、Instantiate 或直接 `Debug.Log` 路径。
- 静态确认序列化字段和公开 `Execute()` 均有中文用途、调用边界和注意事项说明。

## 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 验证 DOMove/DOScale/Sequence 的实际停止、Kill 时刻值保持、
  `OnKill` 回调和顺序执行器推进。
- `uncertain`：当前没有 prefab 绑定，尚未验证具体目标是否把 Tween 注册到 Transform；错误 target 类型会导致
  `DOKill()` 返回但没有停止预期表现。
- `uncertain`：尚未完成 Android/iOS 横屏实机验证。组件是离散即时操作，不增加逐帧循环或运行时分配热点。

## 回滚方式

若尚未绑定 prefab，删除 `IWannerGame_StopAllDOTween.cs` 及其 `.meta` 即可回滚。若已接入关卡，
先从对应 prefab/UnityEvent/执行器中移除组件或引用，再删除脚本；无需迁移 DB、配置、资源或存档。
