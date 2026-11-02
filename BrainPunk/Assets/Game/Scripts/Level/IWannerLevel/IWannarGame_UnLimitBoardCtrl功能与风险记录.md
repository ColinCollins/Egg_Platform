# IWannarGame_UnLimitBoardCtrl 功能与风险记录

## 需求与背景

- 2026-08-25：新增 IWanner 局部四向循环边界。给定一个世界坐标 `Rect`，Actor 从左、右、上、下任一边越界后，从相对边继续移动。
- 需求中的脚本名按用户指定保留为 `IWannarGame_UnLimitBoardCtrl`；现有模块多数脚本使用 `IWannerGame_` 前缀，本次不做批量命名调整。

## 修改前行为基线

- IWanner Actor 由 `Rigidbody2D` 驱动，现有共享关卡控制器负责输入、暂停、死亡和成功流程。
- 模块已有传送门和回收区域，但没有只按位置矩形进行四向首尾相接的通用局部组件。
- 本次不修改任何已验收关卡脚本或 prefab；未挂载新组件的关卡行为保持不变。

## 影响文件与系统

- 新增运行时脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannarGame_UnLimitBoardCtrl.cs`。
- 不修改 `Actor_IWanner`、`ActorCtrl`、`IWannerLevelCtrl`、输入事件、存档、配置、资源加载、打点或 UI。
- 当前未修改关卡 prefab；组件需要由目标关卡在 Inspector 中挂载并配置。

## 修改后行为

1. 组件优先使用 Inspector 绑定的 `Actor_IWanner`；未绑定时只在自身子层级查找。
2. `boardRect` 使用世界坐标，Actor 根节点位置越过左右边界时环绕 X，越过上下边界时环绕 Y；角落越界时两个轴同时处理。
3. 环绕通过 `Mathf.Repeat` 保留越界余量，并支持单个物理帧跨过超过一个矩形宽度或高度。
4. Actor 存在 `Rigidbody2D` 时写入 `Rigidbody2D.position`；没有刚体时才回退到 `Transform.position`。环绕不修改线速度、角速度、重力、输入状态或朝向。
5. Rect 宽或高不大于 0、或找不到 Actor 时，组件记录 Bear Logger 警告并停用。
6. 选中组件时以青色世界坐标线框显示 Rect；Gizmo 只读取本地序列化数据，不触发运行时初始化。

## 数据、资源与事件流

- 数据流：Inspector `boardRect` + Actor 当前世界坐标 → `FixedUpdate` 越界判断 → 相对边世界坐标。
- 资源流：无新增运行时资源加载、实例化或销毁。
- 事件流：无新增 `PlayEvents`、Bear Event 或 UnityEvent。
- 存档与配置：无 DB、Save、Luban、RemoteConfig 读写。

## 风险与验证

- 边界按 Actor 根节点位置判定，不包含 Collider2D 外形；若美术要求整个角色完全离开屏幕后再出现，需要在 Rect 中预留半个角色尺寸。
- 瞬移后保持原速度，Actor 可能立即与相对边附近 Collider2D 发生接触；关卡摆放需保证出生侧有足够空间。
- 世界坐标 Rect 不会跟随组件 Transform 移动；若关卡整体运行时平移，需要同步调整设计或后续明确改为局部坐标语义。
- 每个物理帧只有常数次比较与取模，不产生托管分配，符合 Android/iOS 横屏 60 fps 的局部运行时预算。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo --verbosity:minimal` 已通过：`0 error / 118 warning`；warning 属于工程既有范围，目标脚本没有新增编译错误。
- 已静态确认目标脚本未引入 `OnValidate`、`Reset`、`ExecuteAlways`、DB/Save、RemoteConfig、全局事件、实例化/销毁或 `Debug.Log`。
- Unity PlayMode 中四边、四角、高速跨越、暂停/Reset、碰撞恢复以及 Android/iOS 真机表现仍为 `uncertain`。

## 手工验证清单

1. 将组件挂到目标 IWanner 小关根节点，绑定 Actor，并设置有效世界坐标 Rect。
2. 分别让 Actor 越过左、右、上、下边界，确认从相对边出现且速度方向和大小保持。
3. 从四个角越界，确认 X/Y 同时环绕。
4. 使用高速度或较低帧率跨过边界，确认越界余量保留且不会卡在边缘。
5. 验证相对边附近平台/机关不会因瞬移产生非预期碰撞，并验证 Reset 后组件重新随关卡实例初始化。

## 回滚路径

- 从目标关卡 prefab 移除该组件，并删除 `IWannarGame_UnLimitBoardCtrl.cs` 及其 `.meta`。
- 该组件不写存档、不改共享 Actor 或控制器，回滚不需要迁移玩家数据、配置或资源。
