# IWannerGame_MoveListener 功能与风险记录

日期：2026-09-02

## 需求与背景

- 用户要求为 IWannerGame 补充 `MoveListener`，用于监听玩家左移、右移和跳跃移动事件。
- 修改前，IWanner HUD 已派发 `PlayerLeftMoveEvent`、`PlayerRightMoveEvent` 和 `PlayerJumpEvent`，但 IWanner 模块没有可直接在 Inspector 中把这三类输入统一转成关卡本地回调的组件。

## 影响文件与系统

- 新增 `Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_MoveListener.cs` 及 Unity `.meta`。
- 新增本功能记录 `Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_MoveListener功能与风险记录.md` 及 Unity `.meta`。
- 复用 Bear `EventSubscriber`、现有 `Game.Events` 玩家输入事件和 Unity `UnityEvent`。
- 不修改 IWanner HUD、`IWannerLevelCtrl`、Actor、关卡 prefab、配置、存档、资源、分析打点或平台 SDK。

## 既有行为基线与新增行为

- 既有 HUD 在左右控制区有效期间逐帧派发对应方向事件，跳跃按钮按下时派发一次跳跃事件；移动取消使用独立的 `PlayerMoveCancelEvent`。
- 新组件仅在 PlayMode 且自身启用时订阅左移、右移和跳跃事件，任一事件到达后调用同一个序列化 `onMove`。
- 组件禁用时释放全部订阅，再次启用时重建订阅，避免重复监听或失活对象继续响应。
- `onMove` 不区分输入类型，也不监听移动取消；本次不新增或改写全局事件。

## 事件与平台流

`IWannerGame_GamePlayPanel` 派发既有玩家输入事件 → Bear `EventSubscriber` → `IWannerGame_MoveListener` → 关卡 prefab 配置的本地 `UnityEvent`。

Android、iOS 与 Unity Editor 共用现有 IWanner HUD 的事件派发链；本组件没有平台分支、每帧轮询、对象创建、存档或资源加载。

## 验证

- Roslyn 语法树检查通过，未发现 C# 语法错误。
- 定向静态检查确认左移、右移、跳跃各订阅一次，组件禁用时统一释放订阅，三个事件入口各调用一次 `onMove`。
- `dotnet restore Assembly-CSharp.csproj --ignore-failed-sources --nologo` 通过；将新脚本临时加入 Unity 生成项目后尝试完整程序集构建，
  但在进入本次源码有效语义编译前被 `Temp/bin/Debug` 缺少 200 个依赖 DLL 的 `CS0006` 阻断。临时 csproj 改动已撤销，
  因此该结果不能作为程序集编译通过证据。
- 定向 `git diff --check` 通过，仅有仓库现行 LF/CRLF 转换提示。
- Unity PlayMode 和 Android/iOS 横屏真机尚未验证。

## 剩余风险与未验证缺口

- `uncertain`：左右按住时现有 HUD 每帧派发移动事件，因此绑定回调也会逐帧执行；回调方必须避免昂贵操作或无界实例化。
- `uncertain`：尚未在 Unity PlayMode 验证启用、禁用、重新启用以及左右/跳跃三类事件的实际触发次数。
- `uncertain`：尚未验证 Android/iOS 横屏触控下左右滑动、按住和跳跃并发时的回调表现。

## 回滚路径

- 从使用它的 prefab 移除该组件后，删除 `IWannerGame_MoveListener.cs`、对应 `.meta` 和本记录即可。
- 现有玩家输入事件、HUD、LevelCtrl、Actor、配置和存档均无需迁移或回滚。

## 2026-09-15：补充一次性监听

### 需求与原行为基线

- 用户要求增加 `isOnce`，启用后任一移动输入只监听一次，首次响应后不再监听。
- 原组件在每次启用时持续订阅左移、右移和跳跃；左右按住期间 HUD 会逐帧派发方向事件，因此 `onMove` 也会持续调用。
- 当前全量 Prefab、Scene、Asset 引用检查只确认 `ILevel_034.prefab` 挂载本组件；该 Prefab 未显式写入新字段，Unity 缺省反序列化为 `false`，既有连续监听行为不变。

### 当前行为与事件边界

- 新增默认关闭的序列化字段 `isOnce`。关闭时继续保持原有持续监听；开启时，左移、右移或跳跃中的第一个事件会先标记已触发并释放全部三类 Bear 事件订阅，再调用一次 `onMove`。
- 已触发门禁在释放订阅前写入，可阻止 `onMove` 内同步再次派发同类事件造成重入调用。Bear `EventDispatcher` 对事件派发中的注销使用延迟队列，当前门禁与其安全注销机制共同保证当前组件只执行一次回调。
- 一次性状态属于组件实例生命周期：同一实例禁用后再启用不会重新订阅；关卡销毁并创建新 Prefab 实例后状态自然重置。未增加公共重置 API、全局事件、逐帧逻辑、存档或配置。
- `OnDisable` 改为只释放现有订阅并清空引用，不再通过 `EventsUtils.ResetEvents` 为从未进入 PlayMode 的组件创建空订阅对象。

### 验证、风险与回滚

- 定向静态检查覆盖默认值、三类事件统一入口、一次性门禁写入顺序、订阅释放和再次启用门禁；使用 Unity 6000.0.59f2 自带 Roslyn 编译器及当前 `Assembly-CSharp`、`BearEventSystem`、`UnityEngine.CoreModule` 引用完成本脚本定向编译，结果为 0 错误、1 条仅针对 Unity 序列化字段的 `CS0649` 警告；`git diff --check` 通过。
- 全量 `Assembly-CSharp.csproj` 的 `dotnet build` 被工程当前缺失的 200 个 `Temp/bin/Debug/*.dll` 元数据引用阻断，不能作为本次全工程编译证据；该阻断未指向本脚本。
- 当前未完成 Unity PlayMode 的首次输入、左右按住、跳跃、回调重入、禁用再启用和关卡重建验证；Android/iOS 横屏触控同样为 `uncertain`。
- 关闭 `isOnce` 可回退单个组件的行为；若整体撤回本能力，删除 `isOnce`、`_hasTriggeredOnce`、`HandleMove` 中的一次性门禁，并让三个事件入口直接调用 `onMove`。保留 `StopListening` 仍可维持等价的禁用退订行为。
