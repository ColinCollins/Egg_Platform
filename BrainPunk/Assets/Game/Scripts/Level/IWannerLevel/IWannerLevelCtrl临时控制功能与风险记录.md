# IWannerLevelCtrl 临时控制功能与风险记录

## 需求背景

当前 Main 场景已存在 `Actor_IWanner` 和 Qboxy Spine 动画映射，但没有关卡控制器调用
`ActorCtrl.OnUpdate()`、订阅玩家移动/跳跃事件，因此 Play 后无法直接操作。需求是增加一个不包含胜利、
失败和结算的临时控制器，先支持场景内移动与跳跃验证。

## 影响文件、资源与系统

- 新增 `Assets/Game/Scripts/Level/IWannerLevel/IWannerLevelCtrl.cs`。
- 修改 `Assets/Game/Scenes/Main.unity`：给现有 `Actor_IWanner` 挂载控制器，并补充本地
  `pushCollider` 子节点及 `ActorCtrl` 引用。
- 修改 `Assets/Game/Prefabs/WannerLevel/ILevel_001.prefab`：把现有 Square 地板设置为项目既有
  `Ground` Layer（6），供 Actor 落地和跳跃检测使用。
- 复用 `ActorCtrl`、`Actor_IWanner`、Bear `EventSubscriber` 和现有
  `PlayerRightMoveEvent`、`PlayerLeftMoveEvent`、`PlayerMoveCancelEvent`、`PlayerJumpEvent`。
- 不接入 BaseLevelCtrl、成功/失败、Reset/Replay、DB、存档、配置、UI、资源加载、打点或平台 SDK。

## 原有行为基线

- `Actor_IWanner` 已挂载并能把角色状态映射到 `idle`、`walk`、`jump`、`die`。
- `ActorCtrl` 没有 Unity `Update()`，必须由外部控制器调用 `OnUpdate()`。
- Main 场景没有 BaseLevelCtrl；`Actor_IWanner.pushCollider` 为空，直接启动更新会在共享碰撞处理中产生空引用。

## 当前行为

- `IWannerLevelCtrl.Update()` 每帧调用当前 Actor 的 `OnUpdate()`。
- 控制器启用时订阅项目既有方向、停止和跳跃事件，禁用时解除订阅并清零水平输入。
- Unity Editor 中场景 Actor 保持 `IsEditor=true`，因此方向键与空格会沿用 ActorCtrl 既有事件链。
- Main 场景增加一个默认失活、结构与标准 Actor 一致的局部 `pushCollider`，避免启动更新后的空引用。
- `ILevel_001` 的 Square 地板使用 `Ground` Layer，匹配 Actor 当前 `whatIsGround` 掩码。
- 控制器不判断完成条件，也不会触发胜利、失败、存档或场景切换。

## 数据、事件与平台流

Unity Editor 键盘输入由 `ActorCtrl.HandleInput()` 转为既有玩家事件，`IWannerLevelCtrl` 同步接收事件并写入
Actor 输入，随后同一帧由 `ActorCtrl` 完成物理、碰撞和动画更新。移动端若已有 UI 派发同一事件也可复用，
但本次没有新增 IWanner 专属触控 UI。

## 验证记录

- 已静态确认控制器只订阅现有事件，没有新增 `PlayEvents` 或全局事件类型。
- 已静态确认 Main 场景控制器绑定目标为现有 `Actor_IWanner`，`pushCollider` 指向局部子节点。
- 已静态确认 `ILevel_001.Square` 为 Ground Layer（6），且 Actor 当前地面掩码包含该层。
- Unity 刷新 csproj 并收录 `Actor_IWanner` 与 `IWannerLevelCtrl` 后定向构建通过：
  `0 error / 101 warning`；warning 为工程现有项。
- 新增脚本与文档行尾检查通过；仓库级 `git diff --check` 仍会报告 Main 场景中任务开始前已有的
  Unity YAML 空值行尾空格，本次未批量改写这些用户场景内容。

## 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 实际验证方向键、空格、落地检测、朝向和四个 Spine 动作表现。
- `uncertain`：当前 `ILevel_001` 仅是临时场景内容，正式 IWanner 关卡加载与生命周期尚未接入。
- `uncertain`：没有 IWanner 专属移动端触控 UI，Android/iOS 横屏操作尚未验证。
- 正式关卡控制器接入前必须移除或禁用本临时控制器，避免两个控制器重复调用 `Actor.OnUpdate()` 或重复订阅输入。

## 回滚方式

从 Main 场景移除 `IWannerLevelCtrl` 组件和局部 `pushCollider`，把 `Actor_IWanner.pushCollider` 恢复为空，
并把 `ILevel_001.Square` 恢复为原 Layer 后，再删除本目录下控制器脚本、对应 `.meta` 和本记录即可；
不会影响 Qboxy Spine 资源或 `Actor_IWanner` 动画映射。
