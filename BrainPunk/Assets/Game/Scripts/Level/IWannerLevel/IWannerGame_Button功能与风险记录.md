# IWannerGame_Button 功能与风险记录

日期：2026-08-17

## 需求 / 背景

- 用户要求新增 `IWannerGame_Button` 组件，由 `BoxCollider2D` Trigger 触发 API，并且每个按钮只能触发一次。
- 用户后续要求首次有效触发后立即隐藏 `middle`，并把 `top.localPosition.y` 设为 `0`。
- 修改前 IWanner 关卡可复用 `OnTrigger2DHandle -> TriggerExecutor` 组合派发进入事件，但该通用 Trigger 会在目标重新进入时再次派发，没有独立的一次性按钮组件。

## 影响文件与系统

- 新增 `IWannerGame_Button.cs` 及其 Unity meta，放在 IWanner 关卡共享组件目录；新增
  `Assets/Game/Prefabs/IWannerGameItems/IWannerGame_Button.prefab` 作为正式按钮资源。
- 复用现有 `Collider2DEvent`，向 Inspector 持久化监听器和运行时代码订阅者传递首次进入的 `Collider2D`。
- 复用 `Bear.Logger` 记录 Collider 或 Tag 配置异常。
- 新增本地 `middle` GameObject 与 `top` Transform 序列化引用，只修改当前按钮层级的激活状态和局部坐标。
- 未修改任何已提交关卡脚本或关卡 prefab，也不改配置、存档、资源加载、UI、全局事件、Analytics、打点或平台 SDK。

## 既有行为基线

- `OnTrigger2DHandle` 可按 Tag 派发 `onEnter/onExit`，但没有一次性门禁；目标离开后再次进入会再次调用监听器。
- IWanner 已有 Trigger 通常以 `Player` Tag 过滤玩家，`Actor_IWanner` 的主要碰撞对象也使用 `Player` Tag。
- 初版 `IWannerGame_Button` 首次有效触发只写入一次性门禁并派发 `onPressed`，没有处理按钮节点的按下外观。

## 新行为

1. 组件通过 `[RequireComponent(typeof(BoxCollider2D))]` 要求同一 GameObject 持有 `BoxCollider2D`，并在运行时检查该 Collider 是否启用 Trigger。
2. `triggerTag` 默认 `Player`；只有进入 Collider 所在 GameObject 的 Tag 与配置一致时才视为有效按下。Tag 为空时安全停用触发并记录警告。
3. `middle` 指向未按下时显示的中间节点；首次有效触发时立即调用 `middle.SetActive(false)`。
4. `top` 指向顶部节点；首次有效触发时只把 `top.localPosition.y` 写为 `0`，保留原 X/Z。
5. 首次有效进入时先把 `_hasTriggered` 置为 `true`，再依次应用 `middle/top` 表现并派发 `onPressed(Collider2D)`；监听器执行时已经可以观察到最终按下外观，同时不会因回调重入重复执行。
6. 一次性状态在组件实例整个生命周期内保留；禁用后重新启用不会重新武装，也不会恢复 `middle/top`。关卡销毁并创建新实例时，新按钮实例读取 prefab 初始外观并从未触发状态开始。
7. 公共 `OnPressed` 属性只用于订阅或移除监听器；外部不应直接 `Invoke` 绕过组件的一次性门禁。
8. 正式按钮 prefab 在根节点绑定本组件、Trigger `BoxCollider2D` 与 `SpriteRendererGroupColor`，并把
   `middle/top/bottom` 三个 Renderer 纳入颜色组。

## 数据 / 资源 / 事件流

`BoxCollider2D Trigger` → `OnTriggerEnter2D(Collider2D)` → PlayMode、未触发、非空、Tag 匹配检查 → 写入一次性门禁 → 隐藏 `middle` → 写入 `top.localPosition.y = 0` → `Collider2DEvent onPressed`。

- 该流只处理当前关卡实例内的物理回调与本地 UnityEvent，不读取或写入 DB/Save，不加载或实例化资源，也不派发 `PlayEvents` 或 Bear 全局事件。
- Android 与 iOS 使用同一 Unity 2D 物理入口，没有平台分支或持续轮询。

## 验证

- `git diff --check` 对新增脚本与 meta 通过。
- 定向静态扫描确认脚本未新增 `Update`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/Save/RemoteConfig、实例化或全局事件路径。
- 加入 `middle/top` 表现后的 `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：`0 error / 109 warning`；warning 属于当前工程既有输出范围。
- 静态 YAML 核对确认正式按钮 prefab 的脚本、Trigger Collider、`middle/top` 引用和三个颜色组 Renderer 均已绑定；
  `ILevel_041.prefab` 当前以 prefab instance 形式引用该资源。

## 剩余风险 / 未验证缺口

- `uncertain`：虽然正式按钮 prefab 已完成静态绑定，但尚未在 Unity PlayMode 实测节点隐藏、Y 坐标归零、首次进入、
  离开重进、组件禁用再启用和多 Collider 同帧进入。
- `middle` 或 `top` 缺失时组件会在 `Awake` 记录 Bear Logger 警告；一次性事件仍会继续派发，缺失引用对应的局部表现会被安全跳过。
- 组件不会缓存或恢复 `middle` 的初始激活状态和 `top` 的初始局部坐标；若未来需要关卡内原实例重置按钮，必须新增明确的重新武装与外观恢复需求，不能依赖禁用/启用。
- `BoxCollider2D.isTrigger` 必须由 prefab/场景配置为 `true`；组件只告警，不会在运行时静默改写物理配置。
- Unity 2D Trigger 回调仍要求碰撞双方至少一方具有可参与模拟的 `Rigidbody2D`，具体关卡需在绑定时验证。
- Tag 比较作用于进入回调携带的 Collider GameObject；若后续角色改为未继承 `Player` Tag 的子 Collider，需要同步调整碰撞层级或过滤策略。
- Android/iOS 横屏运行、真机物理帧率和具体按钮反馈表现未验证；组件本身无逐帧逻辑。

## 回滚路径

- 仅回滚本次外观增量时，移除 `middle/top` 字段、`ApplyPressedVisual()` 与触发入口中的调用，即可恢复为只派发一次事件的版本。
- 完整回滚组件时，删除 `IWannerGame_Button.cs`、对应 meta 和本文档，并移除 `DevelopMD/已有功能设计与使用记录.md` 的对应条目。
- 完整回滚正式按钮资源时，还需从使用关卡移除 `IWannerGame_Button.prefab` 实例后再删除 prefab 与 meta；
  配置、存档和运行时资源加载链无需迁移。

## IWanner 关卡主题色接入（2026-08-17）

### 需求、基线与影响范围

- 用户要求由 `IWannerLevelCtrl` 控制 `IWannerGame_Button` 的颜色，并明确复用
  `SpriteRendererGroupColor`，表现参考当前 Ground、Trap、Spring 共用的关卡配色链路。
- 修改前 `IWannerLevelCtrl.ApplySceneColorSettings()` 会在小关运行时初始化时写入 Camera、Ground、Trap 与
  Spring 颜色，但不会处理按钮；按钮颜色组会保留自身序列化颜色。
- 本次修改 `IWannerLevelCtrl.cs` 与 `IWannerLevelColorSettings.cs` 的配色说明，并复用正式按钮 prefab 的既有颜色组；
  不修改按钮触发逻辑、`SpriteRendererGroupColor` 公共实现、关卡 prefab、配色资产、存档、配置、资源加载、UI、事件、
  打点或平台 SDK。

### 新行为与运行流

1. `IWannerLevelCtrl.InitializeRuntime()` 继续只在 PlayMode 运行入口调用
   `ApplySceneColorSettings()`，不会从 `OnValidate`、`Reset` 或编辑器预览触发。
2. 控制器在当前小关层级内收集 `SpriteRendererGroupColor`，只处理以下颜色组：颜色组位于
   `IWannerGame_Button` 组件层级内，或颜色组所在 GameObject 精确命名为 `IWannerGame_Button`。
3. 命中的颜色组调用 `SetColor(IWannerLevelColorSettings.GroundColor)`；该公共 API 只覆盖每个
   `SpriteRenderer` 的 RGB，保留各自 Alpha。
4. 正式 `IWannerGame_Button.prefab` 根节点已同时挂载按钮组件与 `SpriteRendererGroupColor`，因此正常通过组件父层级
   判定；精确名称分支保留给按钮脚本暂缺但颜色组结构仍有效的兼容场景。
5. 其它 `SpriteRendererGroupColor` 不受影响；Ground、Trap 与 Spring 的既有直接改色分支保持不变。

运行流：`IWannerPlayCtrl` 创建小关 → `IWannerLevelCtrl.InitializeRuntime()` →
`ApplySceneColorSettings()` → 定位按钮颜色组 → `SpriteRendererGroupColor.SetColor(GroundColor)` →
颜色组成员更新 RGB、保留 Alpha。

### 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -v:minimal
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false /p:BuildInParallel=false` 通过：
  `0 error / 109 warning`；warning 属于当前工程既有输出。
- 两份修改代码的定向 `git diff --check` 通过，仅有工作区 LF/CRLF 提示。
- 静态扫描确认增量没有新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/Save、实例化或全局事件路径。
- 静态 YAML 核对确认正式按钮 prefab 同时绑定按钮组件、颜色组与三个 Renderer，`ILevel_041.prefab` 当前存在三个
  该资源的 prefab instance；本次未改写该用户现有 ILevel prefab。

### 剩余风险与回滚

- `uncertain`：尚未在 Unity PlayMode 目测按钮与本关 Ground/Trap/Spring 同色，也未验证按钮按下隐藏
  `middle` 后其余 Renderer 的颜色、各成员 Alpha、Reset/切关重建以及 Android/iOS 横屏表现。
- `SpriteRendererGroupColor` 若不在按钮组件父层级内且所在节点未精确命名为 `IWannerGame_Button`，会被有意跳过；
  后续 prefab 采用其它结构时，需要把颜色组放回按钮组件层级或保持约定名称。
- 单独回滚本增量时，移除 `ApplyButtonColorSettings()` 及其调用，并把
  `IWannerLevelColorSettings` 的两处说明恢复为 Ground、Trap、Spring；按钮触发、prefab 与配色资产无需迁移。
