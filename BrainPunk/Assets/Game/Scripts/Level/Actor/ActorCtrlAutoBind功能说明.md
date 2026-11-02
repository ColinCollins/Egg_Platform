# ActorCtrl AutoBind 编辑器工具说明

## 需求背景

`ActorCtrl` 新增了 Odin Inspector 的 `AutoBind` 按钮，用于在 Unity Editor 中手动补齐 Actor 常用序列化引用，降低 prefab 或关卡内 Actor 实例漏绑 `pushCollider`、`spineCtrl`、基础碰撞层的风险。

该能力只面向编辑器配置阶段，代码被 `#if UNITY_EDITOR` 包裹，移动端运行时构建不会编译该按钮和 `UnityEditor` API。

## 影响范围

- 修改脚本：`Assets/Game/Scripts/Level/Actor/ActorCtrl.cs`
- 相关组件：`ActorCtrl`、`ActorSpineCtrl`
- 相关字段：`pushCollider`、`spineCtrl`、`whatIsGround`、`whatIsWall`、`whatIsStuff`
- 未修改 prefab、关卡资源、DB、Save、RemoteConfig、Luban 配置、资源加载、热更、广告、IAP、analytics 或平台 SDK

## 原有行为基线

- `ActorCtrl.AutoBind()` 已存在 Odin `[Button("AutoBind")]` 入口和注释，但方法体没有实际绑定逻辑。
- `Actor.prefab` 文本证据显示标准 Actor 已有名为 `pushCollider` 的子物体，`spine` 子节点挂有 `ActorSpineCtrl`，且默认碰撞层配置包含 `Ground`、`Stuff` 等层。
- 运行时 `Start()` 仍通过 `GetComponent<Rigidbody2D>()`、`transform.Find("spine")` 初始化运行态引用；本次没有改变运行时初始化、移动、跳跃、碰撞或动画流程。

## 当前行为

- 点击 `AutoBind` 时，在当前 Actor 层级下递归查找名为 `pushCollider` 的子物体，并写入 `pushCollider` 字段。
- 优先从直接子节点 `spine` 获取 `ActorSpineCtrl`，找不到时回退为 `GetComponentInChildren<ActorSpineCtrl>(true)`。
- 仅当 LayerMask 当前为 Nothing 时写入默认层：
  - `whatIsGround` -> `Ground`
  - `whatIsWall` -> `Ground`
  - `whatIsStuff` -> `Stuff`
- 只有实际字段变化时才调用 `Undo.RecordObject`、`EditorUtility.SetDirty`；如果对象属于 prefab instance，则记录 prefab override。
- 如果引用已正确绑定或目标层不存在，按钮不会写入字段，也不会创建对象。

## 数据与资源流

- 工具只读取当前对象已有 Transform 层级和 Unity ProjectSettings 中已有 Layer。
- 工具只写 `ActorCtrl` 本地序列化字段。
- 不创建 GameObject，不 Instantiate prefab，不访问 DB/save/config/manager，不触发运行时资源加载。
- `UnityEditor` 和 Odin `[Button]` 引用均位于 `#if UNITY_EDITOR` 条件编译内。

## 验证记录

- 静态检查确认 `ActorCtrl.cs` 的 `UnityEditor`、`Sirenix.OdinInspector`、`AutoBind` 实现均在 `#if UNITY_EDITOR` 内。
- 静态风险扫描确认本次新增实现未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`Instance` 或 `Instantiate` 链路。
- `ProjectSettings/TagManager.asset` 静态证据显示存在 `Ground` 和 `Stuff` layer。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 266 warnings。
- 待补充：Unity Editor Inspector 中实际点击 `AutoBind`，验证 Undo、prefab override 和 prefab asset dirty 行为。

## 剩余风险

- 尚未在 Unity Editor 中实际点击按钮验证 Inspector 写入效果。
- 若非标准 Actor 层级中存在多个 `ActorSpineCtrl` 或多个名为 `pushCollider` 的子物体，当前工具会按层级递归的第一个匹配项写入；特殊 Actor 需要人工复核。
- 该工具不会覆盖已有非空 LayerMask，避免误改已配置的关卡特例；如果已有 LayerMask 配错，需要人工清空后再点击或手动修正。

## 回滚方式

- 回退 `ActorCtrl.cs` 中 `#if UNITY_EDITOR` 下的 `AutoBind` 实现和 editor-only using。
- 删除本说明文件及对应 `.meta` 文件即可恢复到仅保留空按钮注释的状态。
