# Level118 双组切换与分段相机功能说明

## 需求背景

Level118 场景已有 `Group1` 与 `Group2` 两组蓝、黄机关。用户确认本关从开局起按每次真正成功的起跳互换两组状态；虚体组不可碰撞且统一使用 `0.3` Alpha。相机在前段只保持原有水平跟随，角色首次到达图示高柱顶部区域后永久开启垂直跟随。

## 影响范围

- `Assets/Game/Scripts/Level/MainLevel/Level_118/Level118Ctrl.cs`
  - 本关有效起跳确认、两组状态切换、分段相机跟随。
- `Assets/Game/Prefabs/Level/Level_118.prefab`
  - 根节点绑定 `Level118Ctrl`、Actor、两组机关、GameCamera、原相机组件和边界组件。
  - `Group2` 的 21 个 SpriteRenderer 初始 Alpha 改为 `0.3`，9 个 BoxCollider2D 初始关闭。
- 复用系统：`PlayerJumpEvent`、`ActorCtrl` 公开状态、`CameraFollowRange`。
- 未修改共享 `ActorCtrl`、`BaseLevelCtrl`、`GamePlayPanel`、`CameraFollowArea` 源码，也未修改其它关卡。

## 原有行为基线

- `Group1` 的 21 个 SpriteRenderer 为 Alpha `1`，9 个 BoxCollider2D 开启。
- `Group2` 的 21 个 SpriteRenderer 为 Alpha `0.49019608`，但 9 个 BoxCollider2D 也全部开启；视觉半透明与碰撞状态不一致。
- Prefab 根节点的公共 `CameraFollowArea` 配置为 `strickX=0 / strickY=0`，角色离开跟随区域后可同时改变相机 X/Y。
- Level118 没有本关专用玩法控制脚本。

## 当前行为

1. 进入关卡时强制恢复初始状态：
   - `Group1`：Alpha `1`，所有 Collider2D 开启。
   - `Group2`：Alpha `0.3`，所有 Collider2D 关闭。
2. 本关在 `OnEnable` 监听既有 `PlayerJumpEvent`，记录输入当刻的角色 Y 速度。
3. 只有在随后 `0.3` 秒窗口内检测到明确的向上速度增量，才认定为一次成功起跳并互换两组状态；空中误点且没有真正起跳时不切换。该窗口覆盖共享 Actor 的 `0.25` 秒跳跃缓冲。
4. 组状态切换为即时切换：实体组 Alpha 设为 `1` 且 Collider2D 全开，虚体组 Alpha 设为 `0.3` 且 Collider2D 全关。
5. 本关运行时停用当前 Level118 实例上的公共 `CameraFollowArea`，避免两个组件同时写相机位置；不改公共脚本资产。
6. 尚未进入用户配置的相机 Trigger 时，相机沿用水平安全区跟随，Y 固定为开局值。
7. Trigger 调用 OnVerticalFollowTriggerEnter(Collider2D)，且碰撞体 attachedRigidbody 为本关 Actor.RB 时，永久开启垂直跟随；继续使用原平滑参数与 CameraFollowRange 边界。
8. 垂直跟随开启后，镜头中心最低为开局画面的世界 Y=`0`，不会因角色回落而继续向下。修正直接调整 Prefab 中 `CameraFollowRange` 的 Y 边界，保持原最高镜头中心 Y=`9.6` 不变。

## 事件与运行时流程

`跳跃按钮 → PlayerJumpEvent → Level118Ctrl 记录跳前速度 → BaseLevelCtrl/ActorCtrl 执行原跳跃 → Level118Ctrl.LateUpdate 确认向上速度 → Group1/Group2 状态互换`

相机流程为：

`Awake 缓存相机初始位置并停用本实例旧跟随器 → LateUpdate 保持 X 跟随/Y 固定 → 本关 Actor 进入 Trigger 并调用公开 API → 永久同时跟随 X/Y → CameraFollowRange 钳制最终位置`

本需求不新增资源加载、存档、配置、UI、分析打点或平台接口。

## 性能与移动端说明

- 两组 Renderer/Collider 只在初始化时收集并缓存；每次有效起跳才遍历各 21 个 Renderer 和 9 个 Collider。
- 相机每帧只进行常量级坐标、SmoothDamp 与边界计算，不创建运行时集合。
- 逻辑不依赖屏幕像素或触摸坐标，Android/iOS 横屏共用现有输入事件和正交相机边界。

## 验证记录

- 使用 Unity 6000.0.59f2 当前 `Assembly-CSharp.rsp` 加入 `Level118Ctrl.cs` 执行完整 Roslyn 定向编译：`exit 0`、`0 error`，仅有工程既存警告。
- Prefab 静态解析：371 个本地定义、371 个本地引用、缺失引用 `0`；新脚本 GUID 在 Prefab 与 `.meta` 各解析 1 次。
- 组状态静态解析：`Group1 = 21 Renderer(alpha 1) / 9 Collider(enabled)`；`Group2 = 21 Renderer(alpha 0.3) / 9 Collider(disabled)`。
- 尚未完成 PlayMode 实走、Reset/Replay、不同皮肤跳跃、移动端真机和相机阈值观感验证，状态为 `uncertain`。

## 2026-09-07 试玩修正：落地状态、连续跳跃与机关无影

- 首轮试玩中，角色能被实体平台的 Collider 托住，但落地后仍保持空中动作，后续跳跃输入也不生效。
- 原因是 `Group1/Group2` 中 12 个横平台和 6 个竖柱根节点位于 `Default` Layer，而本关 Actor 的 `whatIsGround` 不包含 `Default`。物理碰撞虽然存在，`ActorCtrl` 的地面 BoxCast 无法识别它们，因此 `isGrounded` 始终不能正确恢复。
- 修正只将上述 18 个场景对象的 Layer 设为现有 `WithoutShadowGround` Layer（13），不在运行时脚本中覆写 Layer，不修改共享 Actor 判定、平台位置、透明度或切换时序。
- Level118 Actor 已有的 `whatIsGround=8520` 包含 Layer 13，因此横平台和竖柱顶部仍能正常恢复落地与再次跳跃；其 `ShadowController.ignoreLayer=8192` 恰好忽略 Layer 13，因此角色站在这些机关上时不产生脚下影子。
- 修正后静态复核：18/18 个 Collider 承载对象均为 Layer 13；`Group1` 仍为 21 个 Alpha 1 Renderer / 9 个开启 Collider，`Group2` 仍为 21 个 Alpha 0.3 Renderer / 9 个关闭 Collider；Prefab 371 个定义无重复、缺失本地引用为 0。实际无影效果、落地动作和连续跳跃仍需 PlayMode 复测，状态为 `uncertain`。

## 2026-09-07 试玩修正：垂直跟随下限

- 用户指定 Level118 开局画面为镜头垂直跟随的最低画面，垂直跟随解锁后不得比该画面更低。
- Prefab 中 GameCamera 初始中心为 Y=`0`、正交尺寸为 `5.9`。原 `CameraFollowRange` 为 `boundaryCenter.y=3 / boundarySize.y=25`，允许镜头中心下降到 `-3.6`。
- 修正只将 Y 边界改为 `boundaryCenter.y=4.8 / boundarySize.y=21.4`；按正交相机边界公式，镜头中心范围由 `[-3.6, 9.6]` 改为 `[0, 9.6]`。X 边界、最高 Y、解锁阈值、跟随速度和代码均不变。
- Prefab 数值、引用和公式已静态复核；不同屏幕比例下的实际画面下沿及高处回落手感仍需 PlayMode/真机验收，状态为 `uncertain`。

## 回滚方式

1. 从 `Level_118.prefab` 根节点移除 `Level118Ctrl` 组件。
2. 删除 `Level118Ctrl.cs` 及其本关文档。
3. 如需完全恢复原 Prefab，把 `Group2` 的 21 个 SpriteRenderer Alpha 恢复为 `0.49019608`，9 个 BoxCollider2D 恢复为开启。
4. 如需恢复本次落地/无影修正，将两组中 12 个横平台和 6 个竖柱根节点的 Layer 改回 `Default`；这会再次使 Actor 无法把它们识别为地面。
5. 如需恢复镜头原下限，把 `CameraFollowRange` 的 `boundaryCenter.y / boundarySize.y` 改回 `3 / 25`。
6. 公共 `CameraFollowArea` 源码未改；移除本关控制器后会自动恢复 Prefab 原有相机行为。

## 2026-09-08：Trigger API 替换高度解锁

- 用户要求取消高度判定，Trigger 由用户自行摆放和绑定。本轮只修改 Level118Ctrl.cs 与本关文档，不修改 Prefab；工作区已有 Prefab 改动保留。
- 删除 verticalFollowUnlockY 及 Update 高度检查。新增公开 OnVerticalFollowTriggerEnter(Collider2D other)，供 OnTrigger2DHandle.OnEnter 的动态 Collider2D 事件绑定。
- 绑定方式：Trigger 对象添加启用的 Collider2D（Is Trigger）及 OnTrigger2DHandle；OnEnter 拖入本关 Level118Ctrl，选择动态参数 OnVerticalFollowTriggerEnter。事件参数必须传入实际进入的 Collider2D。
- API 只在运行态、组件启用、本关 Actor 刚体匹配时生效；重复进入幂等，离开不锁回，关卡实例重建后布尔状态重新为 false。未绑定时不会自动解锁，没有高度兜底。
- 保留水平安全区、镜头边界、SmoothDamp、机关切换和原公共相机停用方式；无新增全局事件、存档、资源、UI 或平台接口。
- 修改旧类注释是因为“指定高度”已与当前 Trigger 实现冲突；其它无关注释保留。
- 验证：使用 Unity 6000.0.59f2 当前 Assembly-CSharp.rsp 定向 Roslyn 编译，输出到临时目录；最终结果见本轮风险记录。Trigger 绑定、物理层接触、进入/退出/重复进入、Reset/Replay、Android/iOS 实测未完成，状态 uncertain。
- 回滚：仅恢复高度字段和 Update 检查、移除该 API 及用户事件绑定；不要整体回退含用户其它修改的 Prefab。

### 2026-09-08：清理冗余引用查找

- 用户要求去掉无效代码。当前 Level_118.prefab 已显式绑定 actor、group1、group2、targetCamera、followRange、legacyCameraFollowArea；删除 Level118Ctrl.ResolveReferences 及 Awake 中的调用，去掉这些对象的运行时查找兜底。正常绑定下初始化和玩法流程保持原样；今后引用缺失需在 Inspector 修复。
- 静态核对：旧高度字段已不在代码和当前 Prefab 中；FollowTrigger 的 OnEnter 已动态绑定 OnVerticalFollowTriggerEnter，目标为本关控制器。上述 Prefab 状态为用户已有修改，本轮未改 Prefab。
- 保留实际使用中的机关组件缓存、相机初始化保护、边界钳制和 Trigger 参数检查；不新增资源、数据、UI 或事件流。
- 验证：最终脚本使用 Unity 6000.0.59f2 完整响应文件 Roslyn 编译 exit 0、0 error。PlayMode 触发和移动端表现仍未验证（uncertain）。
- 回滚：仅恢复 ResolveReferences 方法及 Awake 调用，不回退用户的 Prefab 绑定。
