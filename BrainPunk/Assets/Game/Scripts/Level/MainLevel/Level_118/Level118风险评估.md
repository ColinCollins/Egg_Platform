# Level118 双组切换与分段相机风险评估

## 原始基线与目标

- 原始 Prefab 中蓝色 `Group1` 为全不透明且碰撞开启；黄色 `Group2` 虽为约 49% 透明，但碰撞仍开启。
- 原相机离开跟随区后允许 X/Y 同时跟随。
- 目标是从开局起只按成功起跳切换实体组，并在角色到达高柱顶部区域前锁定相机 Y，首次达到后永久解锁 Y。

## 影响文件与复用边界

- 新增：`Level118Ctrl.cs`、本说明和风险文档。
- 修改：`Level_118.prefab`。
- 复用：既有 `PlayerJumpEvent`、`ActorCtrl.RB/IsDied/IsKnocked`、`CameraFollowRange`。
- 隔离：没有修改共享 Actor、公共 HUD、公共相机脚本或其它已验收关卡。

## 风险项

### 1. 有效起跳判定

- 风险：`PlayerJumpEvent` 表示输入，不天然保证 Actor 真正起跳；若直接切换会让空中误点改变平台。
- 控制：事件只登记待确认状态，随后必须观察到 Y 速度大于 `0.25` 且相对上一帧增加超过 `0.1` 才切换；确认窗口 `0.3` 秒覆盖 Actor 的 `0.25` 秒缓冲跳。
- 剩余风险：若本关在同一窗口内新增非跳跃的外部向上冲量，可能被视为成功起跳；当前 Prefab 静态检查未发现该类本关机关，运行态仍需验证。

### 2. 切换瞬间的物理接触

- 风险：角色起跳后当帧关闭脚下实体组、开启另一组，可能在平台重叠或边缘位置产生接触突变。
- 控制：状态只在确认向上速度后切换，避免角色尚未获得起跳速度时先失去支撑；同一帧最多切换一次。
- 剩余风险：需在 PlayMode 逐个平台验证跳起、落地、擦边和连续按键。

### 3. 相机控制权

- 风险：公共 `CameraFollowArea` 与本关控制器若同时运行，会在 `LateUpdate` 竞争写 GameCamera 位置并抖动。
- 控制：Level118 初始化时只停用当前关卡实例引用的公共组件，本关独立驱动相机；共享脚本和其它关卡不变。
- 跟随下限：开局镜头中心 Y=`0`、正交尺寸 `5.9`。`CameraFollowRange` 的 Y 边界设为 `center=4.8 / size=21.4`，将有效中心 Y 范围限制为 `[0, 9.6]`，不改原上限。
- 剩余风险：进入关卡后的初始帧、暂停恢复、Reset/Replay 与退出重进需要 PlayMode 验证。

### 4. Trigger 解锁绑定

- 原基线：Actor 根节点首次达到 Y=6 后解锁；现改为用户摆放的 Trigger 调用公开 API。
- 风险：事件漏绑、Collider2D 未设为 Trigger、Layer 矩阵不接触或目标碰撞体未归属 Actor.RB 时不会解锁；无高度兜底。
- 控制：回调仅接受本关 Actor 刚体，重复调用无副作用，停用和编辑器态不生效。
- 剩余风险：用户绑定与 PlayMode 物理触发尚未验证，状态 uncertain。

### 5. 移动端性能与兼容

- 风险：高频获取子组件或产生集合会增加移动端 GC。
- 控制：Renderer/Collider 数组只缓存一次，切换仅在有效起跳发生；相机循环无显式集合分配。
- 剩余风险：Android/iOS 真机帧率、宽屏与 4:3 裁切尚未验证。

### 6. 平台物理 Layer 与 Actor 落地判定

- 风险：Collider2D 能托住角色不等于 Actor 会把该表面判为地面。若对象 Layer 不在 `ActorCtrl.whatIsGround` 遮罩内，角色会在平台上仍处于空中状态，并被原跳跃逻辑拒绝再次起跳。
- 已发现：两组 12 个横平台和 6 个竖柱根节点原为 `Default` Layer，而 Level118 Actor 序列化的地面遮罩 `8520` 不包含该层。
- 控制：在 `Level_118.prefab` 场景数据中将这 18 个对象固定为已有 `WithoutShadowGround` Layer（13），不在关卡代码或共享 Actor 中动态补丁。Level118 Actor 的 `whatIsGround=8520` 包含该层，而 `ShadowController.ignoreLayer=8192` 排除该层，同时满足正常落地与机关无影。
- 剩余风险：静态数据已闭环，但切换前后的无影表现、落地动作、连续跳跃、柱体侧面接触和边缘容错仍需 PlayMode 验收，状态为 `uncertain`。

## 验证证据

- 完整 Roslyn 定向编译：`exit 0`、`0 error`。
- Prefab 新脚本 GUID 与序列化引用已静态解析；YAML 缺失本地引用为 `0`。
- `Group1` 初始为 21 个 Alpha `1` Renderer 和 9 个启用 Collider；`Group2` 初始为 21 个 Alpha `0.3` Renderer 和 9 个禁用 Collider。
- 落地/无影修正后，两组 18/18 个 Collider 承载对象均为 `WithoutShadowGround` Layer（13）；透明度和 Collider 初始开关计数未变。Prefab 仍为 371 个唯一定义，重复定义和缺失本地引用均为 `0`。
- 镜头边界静态计算：正交尺寸 `5.9`、Y 边界 `center=4.8 / size=21.4`，得到镜头中心最低 `0`、最高 `9.6`。实际 SmoothDamp 跟随与屏幕观感尚未运行验收。
- 未进行 PlayMode 或真机验证，不将实际手感、成功/失败、重开和平台兼容标记为已验证。

## 建议手工验证清单

1. 开局确认蓝色实体、黄色虚体，角色只能站在蓝色机关上。
2. 正常起跳一次后确认两组只互换一次；空中重复点跳跃不得继续切换。
3. 在所有横平台、三组竖柱和边缘位置重复跳跃，确认碰撞与视觉同步。
4. 每次落在当前实体组后，确认角色恢复 `idle/walk` 而非保持空中动作，下一次跳跃可立即正常起跳，且横平台和竖柱顶部都不显示脚下影子。
5. 红框区域前确认相机 Y 完全不动、X 仍正常跟随。
6. 本关角色首次进入已绑定 Trigger 后确认垂直跟随；离开与重复进入不锁回，其他碰撞体不得解锁。回落时镜头仍不低于开局画面。
7. 验证死亡、Reset、Replay、退出重进后均恢复蓝实黄虚和未解锁垂直跟随。
8. 在 Android/iOS 代表性横屏分辨率检查镜头边界与 60 fps 手感。

## 回滚路径

- 移除 Level118 根节点的 `Level118Ctrl` 后即可恢复公共相机组件接管。
- 删除本关脚本、恢复 `Group2` 原 Alpha 与 Collider 状态，并把两组 18 个平台/竖柱根节点的 Layer 从 `WithoutShadowGround` 改回 `Default`，可完整回到修改前 Prefab 基线。
- 单独回滚镜头下限时，把 `CameraFollowRange.boundaryCenter.y / boundarySize.y` 从 `4.8 / 21.4` 改回 `3 / 25`。
- 不需要回滚共享 Actor、HUD、事件类、相机脚本、配置、存档或资源。

## 2026-09-08：Trigger API 替换高度解锁

- 用户要求取消高度判定，Trigger 由用户自行摆放和绑定。本轮只修改 Level118Ctrl.cs 与本关文档，不修改 Prefab；工作区已有 Prefab 改动保留。
- 删除 verticalFollowUnlockY 及 Update 高度检查。新增公开 OnVerticalFollowTriggerEnter(Collider2D other)，供 OnTrigger2DHandle.OnEnter 的动态 Collider2D 事件绑定。
- 绑定方式：Trigger 对象添加启用的 Collider2D（Is Trigger）及 OnTrigger2DHandle；OnEnter 拖入本关 Level118Ctrl，选择动态参数 OnVerticalFollowTriggerEnter。事件参数必须传入实际进入的 Collider2D。
- API 只在运行态、组件启用、本关 Actor 刚体匹配时生效；重复进入幂等，离开不锁回，关卡实例重建后布尔状态重新为 false。未绑定时不会自动解锁，没有高度兜底。
- 保留水平安全区、镜头边界、SmoothDamp、机关切换和原公共相机停用方式；无新增全局事件、存档、资源、UI 或平台接口。
- 修改旧类注释是因为“指定高度”已与当前 Trigger 实现冲突；其它无关注释保留。
- 验证：使用 Unity 6000.0.59f2 当前 Assembly-CSharp.rsp 定向 Roslyn 编译，输出到临时目录；最终结果见本轮风险记录。Trigger 绑定、物理层接触、进入/退出/重复进入、Reset/Replay、Android/iOS 实测未完成，状态 uncertain。
- 回滚：仅恢复高度字段和 Update 检查、移除该 API 及用户事件绑定；不要整体回退含用户其它修改的 Prefab。

- 最终 API 版本验证：Roslyn 完整响应文件编译 exit 0，0 error；未进行 PlayMode 或真机验证。

### 2026-09-08：清理冗余引用查找

- 用户要求去掉无效代码。当前 Level_118.prefab 已显式绑定 actor、group1、group2、targetCamera、followRange、legacyCameraFollowArea；删除 Level118Ctrl.ResolveReferences 及 Awake 中的调用，去掉这些对象的运行时查找兜底。正常绑定下初始化和玩法流程保持原样；今后引用缺失需在 Inspector 修复。
- 静态核对：旧高度字段已不在代码和当前 Prefab 中；FollowTrigger 的 OnEnter 已动态绑定 OnVerticalFollowTriggerEnter，目标为本关控制器。上述 Prefab 状态为用户已有修改，本轮未改 Prefab。
- 保留实际使用中的机关组件缓存、相机初始化保护、边界钳制和 Trigger 参数检查；不新增资源、数据、UI 或事件流。
- 验证：最终脚本使用 Unity 6000.0.59f2 完整响应文件 Roslyn 编译 exit 0、0 error。PlayMode 触发和移动端表现仍未验证（uncertain）。
- 回滚：仅恢复 ResolveReferences 方法及 Awake 调用，不回退用户的 Prefab 绑定。
