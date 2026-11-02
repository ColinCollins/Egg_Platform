# ILevel65Ctrl 功能与风险记录

日期：2026-09-10

## 需求与原始基线

- 需求：为 IWanner 第 065 关提供控制脚本，监听一次跳跃事件便执行一次可在 Inspector 配置的回调。
- 修改前：`ILevel_065.prefab` 根节点只有 `IWannerLevelCtrl`；没有第 065 关专用的跳跃事件转发器，也没有可配置的本地回调入口。

## 影响文件、资源与系统

- `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel65Ctrl.cs`
- `Assets/Game/Prefabs/WannerLevel/ILevel_065.prefab`
- 复用既有 `PlayerJumpEvent`、`EventSubscriber` 与 `UnityEvent`。
- 不修改 `Actor_IWanner`、`IWannerLevelCtrl`、HUD 输入、关卡结算、存档、打点、配置、资源加载或平台逻辑。

## 修改后行为与调用流

`IWanner HUD/键盘跳跃输入` → `PlayerJumpEvent` → `ILevel65Ctrl.OnPlayerJump` → `onPlayerJump.Invoke()`。

- `ILevel65Ctrl` 已挂载在 `ILevel_065.prefab` 根节点，组件的 **On Player Jump** 字段可在 Inspector 直接添加本关对象的公开无参方法。
- 每次收到一个 `PlayerJumpEvent`，该 UnityEvent 只调用一次；重新启用组件时会先清理旧订阅，禁用时释放订阅，避免同一事件重复回调。
- `PlayerJumpEvent` 是跳跃输入事件，不保证 Actor 已实际离地；若玩法需要“真实起跳”判定，必须另行基于 Actor 刚体速度或接地状态确认，当前需求未引入该规则。
- 本组件只转发事件，不写入角色速度、不拦截跳跃、不改变暂停/结算门禁，也不新增全局事件。

## 验证证据

- 静态代码确认 `IWannerGame_GamePlayPanel.OnClickJump` 与键盘路径都会派发既有 `PlayerJumpEvent`；`ILevel65Ctrl` 订阅同一事件并仅调用配置的 UnityEvent。
- 静态 YAML 确认 `ILevel_065.prefab` 根节点已新增 `ILevel65Ctrl`，其 `onPlayerJump` 持久化回调列表为空，等待关卡配置。
- 脚本不包含 `OnValidate`、`Reset`、`ExecuteAlways`、运行时对象查找、DB、Save、资源加载或逐帧轮询。
- 已执行 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -m:1 --nologo`；工程在编译当前源码前因 `Temp/bin/Debug` 缺少 200 个既有依赖 DLL 失败，因此该命令不构成对本脚本的编译通过证据。

## 风险与未验证缺口

1. **P1：回调尚未配置。** 当前 UnityEvent 有意保持为空；在 Inspector 绑定目标方法前，跳跃输入不会产生可见效果。
2. **P1：输入不等于实际起跳。** 空中或输入被 `IWannerLevelCtrl` 拒绝时，仍可能派发 `PlayerJumpEvent`；若关卡机关必须由真实离地驱动，当前实现不适用，需要补充物理确认规则。
3. Unity PlayMode、Reset/Replay、暂停/恢复及 Android/iOS 横屏触控尚未实际验证，均为 `uncertain`。

## 回滚路径

- 从 `ILevel_065.prefab` 根节点移除 `ILevel65Ctrl`，并删除 `ILevel65Ctrl.cs` 与其 `.meta`，即可停止本关跳跃输入回调。
- 不涉及存档、配置或资源迁移；移除 Inspector 中后续配置的 UnityEvent 回调即可恢复无本地响应的基线。

## 2026-09-11：编辑器快速创建 Target

### 需求与原始基线

- 需求：在 `ILevel65Ctrl` Inspector 中指定一个场景 Target，选择世界坐标 X/Y 轴，从指定的二维起点按固定 Offset 快速创建指定数量的相同对象。
- 修改前：`ILevel65Ctrl` 只负责 PlayMode 跳跃事件转发，没有本关对象批量排列工具。

### 影响文件与系统

- `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel65Ctrl.cs`：新增仅在 `UNITY_EDITOR` 中存在的工具配置字段。
- `Assets/Game/Editor/ILevel65CtrlEditor.cs`：新增自定义 Inspector、Scene 视图起点手柄、排列预览与批量创建逻辑。
- `Assets/Game/Editor/ILevel65CtrlEditor.cs.meta`：新编辑器脚本的 Unity 资产标识。
- 本次未修改 `Assets/Game/Prefabs/WannerLevel/ILevel_065.prefab`；该 Prefab 在任务开始前已有未提交改动，保持用户现状。
- 不改变跳跃事件、关卡运行时、存档、配置、资源加载、UI 或平台流程。

### 修改后行为与数据流

`Inspector 本地配置` → `显式点击快速创建按钮` → `复制场景 Target` → `按世界坐标排列` → `选中新对象并标记场景变更`。

- Target 必须来自当前 Scene 或 Prefab Stage，不允许直接使用 Project 中的 Prefab 资产。
- 模板 Target 本身不移动、不计入新建数量；第一个新对象位于 `Start Position`，Z 保持 Target 当前世界坐标。
- 后续对象沿选定 X 或 Y 轴递增 `Offset`；负 Offset 表示反方向。创建多个对象时禁止 Offset 为 0，避免完全重叠。
- 新对象复制 Target 完整层级，保持相同父节点，插入 Target 之后，并在同级内使用唯一名称。
- 整批创建合并为一次 Undo；创建后自动选中全部新对象。
- 起点可直接输入，也可复制 Target 当前 X/Y，或在 Scene 视图拖动手柄；预览线显示首尾位置。
- 工具在 PlayMode 中禁用，不调用 DB、Save、RemoteConfig、Manager 单例、运行时事件或对象查找链。

### 验证证据

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo --verbosity:minimal` 执行成功：`0` 个错误；输出为项目既有警告与本次编辑器字段未被运行时直接引用的警告。
- 静态检查确认新脚本仅位于 Editor 编译路径，且没有 `OnValidate`、`Reset`、`ExecuteAlways`、DB、Save、RemoteConfig 或运行时单例调用。
- 聚焦差异检查确认本次没有写入 `ILevel_065.prefab`。

### 风险与未验证缺口

1. **P2：批量创建数量由使用者指定。** 数量过大会在当前场景产生大量完整层级副本；工具提供一次 Undo，但使用前仍需核对 Count 和 Offset。
2. **P2：位置采用世界坐标。** 对带旋转或缩放的父节点，生成后的局部坐标会自动换算；这与本次“场景指定初始位置”的世界坐标定义一致。
3. Unity Inspector 按钮、Scene 手柄、Prefab Stage 保存、嵌套 Prefab 连接及 Undo 实际交互尚未手工操作验证，均为 `uncertain`。

### 回滚路径

- 删除 `ILevel65CtrlEditor.cs` 及其 `.meta`，并移除 `ILevel65Ctrl.cs` 中 `Editor Quick Create` 的 `UNITY_EDITOR` 字段区域，即可恢复只有跳跃转发的基线。
- 尚未保存的误创建对象可通过一次 Undo 撤销；已保存的副本可在确认对象范围后手动删除。

## 2026-09-11：移除跳跃事件监听

> 本节覆盖本文档上方关于 `ILevel65Ctrl` 跳跃转发的当前行为结论；上方内容仅保留为历史记录。

### 需求、基线与影响范围

- 用户明确要求 `ILevel65Ctrl` 不再监听跳跃。
- 修改前，该组件在 PlayMode 订阅 `PlayerJumpEvent`，收到后调用 `onPlayerJump` UnityEvent，并在禁用时释放 `EventSubscriber`。
- 本次只修改 `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel65Ctrl.cs` 及本地记录；保留 `ILevel65CtrlEditor.cs` 和 Target 快速创建功能。
- `Assets/Game/Prefabs/WannerLevel/ILevel_065.prefab` 在任务前已有未提交改动，本次不直接改写其 YAML。

### 当前行为与流程

- `ILevel65Ctrl` 已移除 `Bear.EventSystem`、`Game.Events`、`UnityEngine.Events` 依赖，不再保有 `EventSubscriber`、`onPlayerJump`、`OnEnable`、`OnDisable` 或 `OnPlayerJump`。
- 现在该组件只为自定义 Inspector 保存 `UNITY_EDITOR` 下的快速创建配置；Player 构建中不包含这些字段，组件也没有运行时生命周期方法。
- HUD 和 `IWannerLevelCtrl` 原有跳跃流程未修改；变化仅是 Level 65 不再从同一全局输入事件执行附加 UnityEvent。
- Prefab YAML 当前仍可静态看到旧 `onPlayerJump` 键，但脚本已无对应序列化字段或调用入口，因此不会再触发；Unity 下次正常重新序列化该 Prefab 时可清理此旧键。
- 不新增事件、存档、配置、资源、UI、打点或平台调用。

### 验证证据

- 定向搜索确认 `ILevel65Ctrl.cs` 已无 `PlayerJumpEvent`、`onPlayerJump`、`EventSubscriber`、`OnEnable`、`OnDisable` 和 `OnPlayerJump`。
- 执行 `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo --verbosity:quiet '-consoleloggerparameters:ErrorsOnly;Summary'`：编译成功，`0` 个错误、`130` 个警告。
- 聚焦差异确认编辑器快速创建字段与自定义 Inspector 保留。

### 风险、未验证缺口与回滚

- 如 Prefab 上曾配置过有效 `onPlayerJump` 回调，该附加玩法现已按用户要求停用；当前 YAML 仅含 `m_Target: {fileID: 0}` 且 `m_MethodName` 为空的无效条目，未发现需要迁移的可执行绑定。
- Unity PlayMode 中 Level 65 跳跃仍由原 IWanner 链路正常执行、且不再触发附加回调的实际交互尚未手工验证，为 `uncertain`。
- 回滚时可从本文档历史章节恢复三个 using、`onPlayerJump`、`EventSubscriber` 及对应订阅/释放方法；恢复前需确认这不与当前用户要求冲突。
