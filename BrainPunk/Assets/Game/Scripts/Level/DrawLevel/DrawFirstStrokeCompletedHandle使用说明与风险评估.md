# DrawFirstStrokeCompletedHandle 使用说明与风险评估

## 需求、原行为与影响范围

2026-09-18，用户要求通用脚本在第一笔画完后触发回调，并能在 Inspector 配置回调对象和方法。

原有 `DrawLineFeature.StrokeCompleted → DrawLevelCtrl → IDrawStrokeCompletedHandler` 已提供局部完成通知，但未找到只负责首笔通知并开放 Inspector 回调绑定的组件。新增 `DrawFirstStrokeCompletedHandle.cs` 及其 meta，复用该通知链，不修改原画线、主控、接口或现有关卡 Prefab。本次没有自动挂载到 D048，也不调整其开场 Dynamic 配置。

引用的 `../specs/037-remote-resource-loading/plan.md` 当前缺失，CodeGraph CLI 不可用。本次以现有模块说明和当前源码为依据；不存在匹配本组件的旧使用文档。

## 使用方法与绑定

1. 在 DLevel Prefab 的根节点或其子节点添加 `DrawFirstStrokeCompletedHandle`，保证组件在 `DrawLevelCtrl.InitializeRuntime()` 收集局部处理器之前已存在。
2. 在 **On First Stroke Completed** 中点击 `+`，显式拖入当前关卡的目标对象，选择需要执行的方法。
3. 运行关卡，完成第一笔有效画线后触发一次回调。后续笔画不再次触发。

无需绑定 `DrawLevelCtrl` 引用，也无需手动订阅事件。新增组件自身没有节点扫描、按名称查找或轮询。

这里的“画完”沿用现有有效笔画契约：`DrawLineFeature.cs:684` 完成笔画时，点数不足 2 或长度小于 `MinimumStrokeLength` 会取消，不发布完成通知；有效笔画完成几何、碰撞和刚体处理后，才在第 709 行发布通知。因此不是每次松手都会触发。

## 调用与生命周期

- `DrawLevelRuntimeTypes.cs:129` 的 `IsFirstStroke` 判断本次尝试的有效笔画序号是否为 1。
- `DrawLevelCtrl.cs:879` 在初始化时收集关卡层级内的接口组件，第 499 行起同步分发完成通知；现有收集范围包含禁用和失活组件。
- 新组件在入口检查 `Application.isPlaying`、`isActiveAndEnabled`、`IsFirstStroke` 和本次尝试的触发标记。回调执行前先记录已触发，防止同一次通知同步重入或重复派发。
- `OnDrawAttemptReset()` 清除本次触发标记。由于现有主控也会向禁用组件分发重置，重置入口只检查运行态，允许禁用期间恢复门禁。
- Reset/Replay 销毁并重建关卡时，新实例的标记自然为未触发；同实例尝试重置也能重新等待首笔。单纯关闭再打开组件不会重新开放已触发的门禁。
- 若完成第一笔时组件或宿主失活，该次通知被跳过；重新启用不补发。运行中才添加组件不会自动进入已缓存的接口列表，应在关卡初始化前添加。
- UnityEvent 同步调用，不等待异步动画、协程或任务结束。该组件不负责目标对象的初始状态、取消、结算或重置；这些仍由目标控制器负责。

## 数据、资源、事件与性能边界

唯一新增序列化数据为局部 `UnityEvent` 绑定，运行态只保存一个触发标记。不新增全局事件、订阅、协程、每帧执行、实例化或资源加载，不接 DB、存档、Luban、云控、打点或平台 SDK。Android/iOS 横屏继续由已有画线输入负责，本组件不直接处理屏幕坐标或触摸。

## 验证证据与待验证项

- 使用现有 `Assembly-CSharp.csproj` 的实际引用路径、`Library/ScriptAssemblies/Assembly-CSharp.dll` 中的游戏接口及当前 Unity 引擎程序集，对新增生产源码执行独立 Roslyn 编译，退出码 0、0 错误；首次命令有一个 `/noconfig` 放在响应文件内的命令选项警告，不是源码警告。
- 可复查编译响应文件和日志：`C:/Users/admin/AppData/Local/Temp/brainpunk-first-stroke-callback-20260918/compile.rsp`、`compile.log`。编译输出位于同一临时目录，不写 Packages 或 GF。
- 静态核对：新增类实现两个现有接口方法；启用门禁与现有收集范围对应；只处理首笔；先锁定再调用；尝试重置清标记；无编辑器生命周期、运行时查找或外部系统依赖。
- 当前生成的 `Assembly-CSharp.csproj` 尚未列入新脚本，所以这次独立编译不能冒充完整 Unity 工程编译或资源导入验证。
- `uncertain`：未运行主工程 PlayMode、未绑定实际 Prefab 回调、未验证 Inspector 持久化与 Android/iOS/IL2CPP。临时目录可能被清理，本文保留验证方式与边界。

手工验收：开场和无效短笔不触发；有效首笔触发一次；后续笔画及重复同一通知不触发；回调同步重入不重复；尝试重置后再次触发；Reset/Replay 重建后再次触发；首笔时禁用不触发且启用不补发；已触发后禁用再启用不重复；退出不残留订阅。实际目标回调的动画、物理与结算效果须由挂载关卡另行验证。

## 风险与回滚

- 本组件通知不等于目标动作已完成；绑定异步方法时，由目标脚本管理后续生命周期和取消。
- 目标方法若抛异常，本次门禁仍为已触发；现有主控会记录该处理器异常。不要将首笔通知作为自动重试机制。
- 本轮未挂载到现有关卡，影响入口仅为后续使用者主动添加组件。使用前应核对目标自身的开场与重置职责，例如 D048 的 OnEnable 仍会立即配置 Dynamic。
- 回滚时先从使用它的 Prefab 移除组件及本地事件绑定，再删除本脚本、meta 和使用文档；如已派发目标动作，应按目标控制器原有重置流程恢复。本轮回滚无需改共享画线代码、Packages、GuruSDK、GF 或玩家存档。
