# Level094 风险评估

## 2026-08-18 行为等价精简风险复核

- **事实**：两个生命周期回调原有的事件重置和尝试清理代码完全相同；停用后的延迟重置标记未移动进公共方法。
- **直接结论**：停用与销毁的清理调用顺序、次数和覆盖范围保持一致，不影响弹簧、音效、失败/成功和重开合同。
- **假设**：无新增运行时假设；本轮只抽取私有重复代码。
- **缺失证据**：尚未在 Unity PlayMode 和移动端执行停用、销毁、暂停、重开全链回归，标记为 `uncertain`。
- **回滚**：将 `CleanupRuntimeState()` 内容内联回 `OnDisable()` / `OnDestroy()`。

更新时间：2026-08-11

## 影响边界

Level094 的玩法、输入、弹簧运动、地面拆分和成功发射均由 `Level94Ctrl` 与 `Level_094.prefab` 的本地接线承担。共享 `Ground_mid.prefab`、Actor Prefab、`ActorCtrl`、`BaseLevelCtrl`、Level032 脚本/Prefab、GamePanel、配置结构和其它关卡不修改。

正式配置已经存在 `tblevelsort[94] -> 10094` 与 `tbleveldata[10094] -> Level_094`。本次不改关卡排序、存档、资源加载、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。

## 原始风险基线

- 094 复制源仍挂 `Level32Ctrl`，弹簧 Trigger 和 Actor 本地碰撞检测仍调用 Level032 方法；若只叠加新控制器，会出现两个状态机同时驱动按钮、角色和输入。
- 原假盖板默认失活且无 Collider，洞壁默认显示，不满足揭盖前的画面与承载要求。
- 两个共享地面实例的 Collider 重叠并盖住整个洞口；只换 Sprite 不会产生真实弹簧空洞。
- 按钮为 Dynamic Rigidbody2D，SpringJoint2D 默认关闭；一旦地面开洞而控制器尚未接管，按钮会受重力坠落。
- 隐藏的 `door_0` 仍被 `BaseLevelCtrl.successAnim` 引用，沿用门式成功可能产生不可见演出、延迟或门音效。
- 复制源绑定 `GamePlayPanel_006`，其 Level032 专用 `DragBlock/UIDrag` 会显示无关 UI，并通过射线层拦截揭盖和按钮世界手势。

## 风险项与控制

| 风险 | 当前控制 | 验证状态 |
| --- | --- | --- |
| Level032 与 Level094 同时响应 | Prefab 根组件改绑为 Level94Ctrl；旧 `TriggerSpring`、`StopSpringJump` 本地回调移除 | YAML GUID/方法名静态通过 |
| 旧控制器继续修改全局 fixedDeltaTime | Level32Ctrl 不在 094 运行，不复用其 UI 代理碰撞或时间步逻辑 | YAML 静态通过，PlayMode 待验 |
| 初始就能看到洞或掉进洞 | Ground 已预拆，假盖板初始 active 且本地 BoxCollider2D 单独承载洞口；开始揭盖才关闭 | Prefab 静态通过，画面待验 |
| 盖板外或左右滑以外的手势误揭盖 | 起手必须命中盖板 Collider，缺失时回退 Sprite bounds；还需达到 `55px` 且水平/纵向绝对位移比至少 `1.15` | 鼠标与真机待验 |
| 多指或触点丢失导致误提交 | 每段手势绑定唯一 pointer/fingerId；Ended 正常释放与 Canceled/丢失分开处理 | Android/iOS 待验 |
| 揭盖动画重复、方向错误或 Alpha/位置残留 | 揭盖状态一次性提交；`delta.x < 0` 映射左移、其余合法横滑映射右移，位移与 Alpha 由同一 Sequence 并行驱动。完成后关闭盖板对象和承载 Collider，PrepareAttempt 恢复缓存局部原位与不透明颜色 | 方向与重开路径静态通过，画面待验 |
| 地板仍是整体，Actor 悬空站立 | Prefab 两个实例 override 分别设为 `17.170145/8.024928` 与 `3.676153/10.568078`，形成约 `[-2.00, 0.24]` 的真实洞口 | YAML 数值静态通过，PlayMode 待验 |
| 地板拆分影响其它关卡 | 只写 `Level_094.prefab` 的实例 override，不修改 `Ground_mid.prefab` 源资源 | Git 范围静态通过 |
| `floorCollider` 留空时兼容查找误拆其它地形 | 候选必须启用、非 Trigger、属于 Ground、横跨按钮中心且顶面与按钮顶面相差不超过 `0.35`；当前预拆两段都不横跨按钮中心，因此静默跳过 | 代码与 Prefab 数值静态通过 |
| 地板边缘出现缝隙、重叠或卡脚 | 左右段按世界 bounds 与洞口宽度计算，保留正常接触余量但不跨入洞口 | 不同帧率与角色行走待验 |
| 按钮拆地后直接坠落 | 控制器接管按钮刚体/位置，休止和压缩阶段均由受限 Y 行程驱动，不依赖关闭的 Joint | 后台运行探针通过 |
| 按钮 Collider 与 Sprite/弹簧错位 | 同步移动按钮根 Transform；弹簧视觉以固定底端重算高度；输入额外接受完整 Sprite bounds，但不扩大物理 Collider | 画面、输入热区和碰撞待验 |
| Actor 未站上按钮也可隔空下拉 | 起手同时要求盖板已开、命中按钮物理 Collider 或完整可见 Sprite bounds、Actor 当前站在按钮区域且无其它演出 | 边缘站位待验 |
| 轻点按钮被误判为下拉发射 | 按下即临时接管 Actor，但必须继续向屏幕下方累计 `14px` 才开始改变压缩比例；不足阈值松手会同步物理并释放接管，不压缩、不发射 | 代码静态通过，手感待验 |
| 正常踩踏自动弹跳抢走主动拖动 | 红色按钮按下成功建立候选时立即取消 pending 或尚未发射的被动 Tween，并立即关闭 Actor 物理；按住期间不再排队，达到 `14px` 后优先进入主动压缩 | 手感待验 |
| 首次踩到中央区域却不触发弹跳 | 每次尝试初始化时先武装 `passiveBounceArmed`；Spring 根固定 Trigger 的 Enter/Exit 与按钮站立扫描共同排队。盖板尚未揭开时允许普通踩踏，只有盖板正在渐隐时暂时拒绝新的排队 | 后台运行探针通过 |
| 接地求解的微小上修速度反复取消弹跳等待 | Actor 仍为 `IsGround` 时不以约 `+0.88` 的接触修正速度否定站立；真正离地时仍要求 `linearVelocity.y <= 0.5` | PlayMode 已复现并验证修复 |
| 自动弹跳落回同一按钮后无限循环 | 一次弹跳开始即消费 `passiveBounceArmed`；Trigger 与站立扫描两个排队入口都受门禁约束。只有 Actor 落地、竖直速度接近静止且水平完全走离按钮至少 `0.05` 后才重新武装 | 后台运行探针通过：落回不连弹，走离后可再次触发 |
| 被动压缩后正常站立却没有弹起 | 按钮先下移 `0.336`，不能再以变化后的顶面做完整垂直站立复核；`LaunchPassiveBounce()` 赋速前只检查 Actor 与按钮仍有水平重叠，避免 Tween `Update` / 物理 `FixedUpdate` 错位吞掉发射 | 后台运行探针通过，实际目标赋速 `(0, 10)` |
| 被动压缩期间走离后仍被隔空弹起 | 发射前的水平重叠复核仍会拒绝已经完全走离按钮的 Actor；拒绝时只让按钮 Tween 回位，不修改 Actor 速度 | 代码静态通过，PlayMode 待验 |
| 普通上弹抖动、伸展不明显或出现多余往返 | Actor 重力倍率为 `4.5`，速度 `10` 理论抬升约 `1.13` 世界单位；按钮从被动下压位向上伸展时，随动 Kinematic Collider 会追上动态 Actor 并被物理解算连续顶开。当前上探比例为 `0.405`，相对休止位约上移 `1.701`；发射后临时忽略 Actor 主 Collider 与红按钮 Collider，回到休止位并 Sync 后恢复，按钮实例不再使用 Bouncy 材质，再由独立门禁保证只收回一次 | 根因与隔离边界静态通过；无抖动、视觉穿插、当前高度及落地待策划验收 |
| 普通伸展结束后弹簧卡在高位 | 只有成功建立 Actor 发射才允许保持；发射失败立即复位。死亡、外部成功、重开、Disable/Destroy 和主动输入接管均杀掉 Tween、清 `passiveReturnStarted` 并恢复休止位 | 状态路径已静态复核；低帧率与中途状态切换待验 |
| 临时碰撞隔离未恢复导致 Boxy 穿过按钮 | 开启时缓存该 Collider 对原先的 `GetIgnoreCollision` 值；正常收回先把按钮归零并 Sync，再原样恢复。Kill、死亡、外部成功、Prepare、Cleanup、Disable/Destroy 均有恢复路径；不修改 Layer Matrix 或其它碰撞对 | 生命周期静态通过；极低帧率落地与中断待验 |
| 按下后、达到 `14px` 前角色从按钮边缘穿入洞口 | 合法按下当帧即 `TakeActorOwnership()`，关闭刚体模拟并携带角色；每帧同步 Transform 与 Rigidbody2D position，`14px` 只作为压缩激活门槛 | 代码与独立复审通过，真实触摸待验 |
| 下拉时角色受重力或水平输入脱离按钮 | 从候选按下开始就暂停 Actor 物理和控制，清速度并保持起手相对偏移；不改 Actor 父子层级 | 后台运行探针为旧基线；新候选时序静态通过 |
| 未提交拖动被当成普通/成功发射 | 暂停、Canceled、失焦和 Disable 走取消路径；只有正常释放才按比例分流 | 代码静态通过，状态切换待验 |
| 主动拖拽暂停时 Actor 在菜单后继续下落 | 暂停取消拖拽但不立即恢复刚体模拟或移动；复用本关恢复门禁，在游戏恢复后一帧再还原模拟，避免与 `BaseLevelCtrl` 回调顺序竞态 | 代码静态通过，PlayMode 待验 |
| 禁用或中断时角色留在压缩深度 | 若仍由按钮携带，先将按钮归零、同步 Actor pose 与 Physics2D，再清携带状态并恢复刚体；欠压、被动和月球路线因不处于携带状态不会被误搬回 | 独立代码复审通过，生命周期动态待验 |
| `0.5` 附近路线不稳定 | 使用一次松手快照，`< 0.5` 普通、`>= 0.5` 成功；提交后状态不可逆 | 阈值边界待验 |
| 普通抛射也能碰到月球或软锁 | 按 Actor 朝向使用 `(8.5, 19)` 基准侧上抛，力度随压缩为 `65% ~ 100%`；离地后落地恢复，`2.8s` 有效游戏时间超时兜底 | 轨迹与相机待验 |
| 普通抛射过早恢复输入 | 必须观察到离地再接受落地恢复，避免松手当帧仍接触按钮而立即解锁 | PlayMode 待验 |
| 成功飞行沿途触发地形或再次弹簧 | 成功路线保持 Rigidbody2D 不模拟，以 `2.45s / 弧高18` 的显式抛物线写 Transform | 后台运行探针通过；构图待策划验收 |
| 最后 `1/3` 渐隐不完整 | 同时更新 Actor Spine Alpha 和 Sprite 子渲染组，缩放以起飞时尺寸为基线 | Actor 全部显示层待验 |
| 渐隐/缩放过早 | 飞行归一化进度达到 `2/3` 后才开始，前 `2/3` 不修改 Alpha/Scale | 时间线待验 |
| 成功重复结算 | 成功压缩提交后锁定状态，到达月球只调用一次成功归口 | 后台运行探针通过 |
| 仍播放不可见门或关门音效 | 成功前清空本关 `BaseLevelCtrl.successAnim`，使用 `SuccessStrightly()` 直接结算 | 听感与结算待验 |
| 甩飞音效漏播、重复或污染普通小弹 | `l93PlayerLaunch` 只放在欠压初速度写入后和月球 Tween 建立后两个一次性入口；普通 `LaunchPassiveBounce`、Update、落地和恢复路径均不调用 | 调用点静态通过，实际混音待验 |
| Level032 专用 UI 显示或拦截世界手势 | `Level_094.prefab` 改绑普通 `GamePlayPanel_000`；不修改共享 `GamePlayPanel_000/006` 资源 | Prefab 字符串静态通过，PlayMode 待验 |
| 已提交演出在暂停菜单后继续推进 | 复用 `SwitchGameStateEvent`、`GamePauseEvent`、`GameResumeEvent`；暂停显式 Pause 盖板、按钮、月球 Tween，并冻结普通/欠压物理弹射；恢复事件后一帧按 FSM 实际状态复核再继续 | 代码静态通过，暂停/重开待验 |
| 恢复首帧先超时、随后又恢复旧弹射速度 | `springLaunchPaused` 或恢复协程未完成时，欠压落地/超时检测直接早退；刚体和缓存速度恢复后才继续累计有效游戏时间 | 代码静态通过，PlayMode 待验 |
| 重开后 Actor 永久不可控或不可见 | Disable/Destroy 只清理 Level094 自己的 Tween/状态，并恢复本关缓存的 Rigidbody2D、控制权、Alpha 与缩放 | 代码静态通过，重开待验 |
| 成功已提交后清理错误恢复 Actor | 成功提交标记阻止通用中断路径恢复旧控制，交由结算/关卡销毁处理 | 退出时序待验 |
| 月球目标过远导致精度、相机构图异常 | 使用本地目标 Transform 和显式插值，不依赖刚体长距离高速运动 | 不同宽高比待验 |
| 094 提示仍显示 Level032 文案 | Prefab 当前 term 为 `S_LevelTips_Des_level_32`，未在需求不明确时擅自改文案 | 待策划确认 |

## Prefab 与共享资源检查点

- 根 `Level32Ctrl` 的脚本 GUID 应为 0 次，`Level94Ctrl` 应为 1 次。
- 弹簧 Trigger `5329234296958643046` 的 Enter/Exit 应各绑定一次当前 `Level94Ctrl.OnActorEnterSpring/OnActorExitSpring`；Actor 本地 `5214472719693864906` 不应再调用 `StopSpringJump`。
- 红色活动按钮和固定蓝白圆环虽然都沿用 GameObject 名 `Sp_button_1_02`，序列化 `springButton` 必须继续指向使用 `Sp_button_b_1` Sprite 的 `8996417160726456443`；不得改成固定圆环 `907380547745308691`。
- `BaseLevelCtrl`、Actor、按钮 Transform/Collider/Rigidbody2D、弹簧 Sprite、盖板 Renderer、洞壁 Renderer和月球 Transform 的序列化引用必须全部闭合。
- 假盖板初始显隐、预拆左右 Ground、盖板承载 Collider、洞壁/弹簧初始显隐必须与“先完整地面、后揭盖”一致。
- 两个 `Ground_mid` 源实例 GUID 继续存在，当前碰撞拆分为 Level094 实例 override；`floorCollider` 留空，运行时兼容拆分不应在正常流程创建右地面；共享源 Prefab diff 必须为空。
- `Sp_l94_dm_02` 只保留 Transform 与 SpriteRenderer，不在窄井深处增加常驻实体平台；该平台会把异常下落角色困在按钮下方，不能作为防穿模方案。
- 按钮上的旧 SpringJoint2D 若继续保留，应保持不参与本关驱动，避免与手动位置同时施力。
- 隐藏成功门可保留为复制场景残留，但不应参与 Level094 最终成功演出。

## 性能与移动端边界

- 本关每帧只处理当前活动指针、少量已缓存 Transform/Collider 和正在运行的局部演出，不应扫描全场景、加载资源或产生持续容器分配。
- 正常弹跳和按钮随动应在物理节奏中更新；盖板位移渐隐、弹簧视觉和成功轨迹为短时演出。代表性 Android/iOS 设备的 60 fps 仍需验证。
- UI 排除、触点 owner、Touch Canceled、应用失焦和横屏不同安全区必须在真机验证，Editor Mouse 不能替代移动端结论。

## 验证证据

- 已静态反查 Level094 复制来源、关键节点、组件 fileID、L94 Sprite GUID、两个 Ground_mid 实例范围和旧 Level032 回调。
- 复制基线地面碰撞约覆盖 `[-19.170, -6.204]` 与 `[-9.050, 3.916]`，原右侧 Collider 确实跨过洞口；最终实例 override 将左右段改为约 `[-19.170, -2.00]` 与 `[0.24, 3.916]`。
- 已确认按钮 Joint 初始关闭且 Rigidbody2D 为 Dynamic，属于拆地前必须处理的确定风险。
- 已确认成功门实例失活但 `BaseLevelCtrl.successAnim` 仍有引用，属于成功前必须绕开的确定风险。
- Unity 6000.0.59f2 自带 Roslyn 使用当前 `Assembly-CSharp.rsp` 完整参数编译通过，结果为 `0 error / 104` 条工程既有 warning。
- 最终控制器复编已包含 `floorSurfaceTopTolerance=0.35` 的兼容拆地保护，以及事件暂停、刚体延迟恢复和欠压恢复首帧门禁，结果仍为 `0 error`。
- 本轮修复前的关卡基线曾由当前 Unity Editor 完成 Asset Pipeline Refresh、程序集重载与 `Level_094.prefab` 导入，日志未发现 C# 或 Prefab 导入失败标记。
- Unity 6000.0.59f2 PlayMode 已从主界面实际进入 Level094，并完成横滑揭盖、移动 Actor 到按钮和普通弹跳复现。修复前 Actor 接地速度在 `0/+0.88` 间波动，导致等待状态被逐帧取消；当前等待为 `0.03s`，修复后进入完整发射链并实际赋速 `(0, 10)`。
- Prefab YAML 静态扫描为 `186` 个对象定义、`0` 重复、`0` 缺失本地非零引用；Level94/Level32 脚本 GUID 次数为 `1/0`，两条旧方法持久回调均为 `0`。
- `BaseLevelCtrl.gamePlayPanelName` 静态确认为 `GamePlayPanel_000`，本关不再加载带 `DragBlock/UIDrag` 的 Level032 专用面板。
- 已静态排除中央按钮的 UI 拦截：本关 GameCamera 没有 Physics2DRaycaster，`GamePlayPanel_000` 中央没有常驻全屏 Raycast Target，且盖板与按钮共用相同 UI 过滤。拖拽失败根因是循环弹跳造成按下时几乎不站稳，并叠加 Sprite 上约 `39%` 可见区域超出物理 Collider。
- 已静态确认 `PrepareAttempt()` 会武装首次普通弹跳，而 `TrySchedulePassiveBounce()` 在盖板未开/渐隐时保持拒绝；`LaunchPassiveBounce()` 不再按压缩后的按钮顶面重复做垂直站立判断，只保留水平离开保护。
- 独立 Android 目标 Unity `-batchmode -nographics` 进程从 Loading/Main 加载 Level094 后执行运行时验证，输出 `[Level94RuntimeValidation] PASS checks=29`。覆盖首次自动武装、真实 Trigger 进出、小弹与落回不连弹、走离重武装、盖板阈值、主动下拉同步、取消、欠压侧抛、月球路线、最后 `1/3` 缩小和一次成功结算。
- `PASS checks=29` 发生在 `passiveExtensionRatio=0.18/0.27/0.405`、最高点保持和碰撞隔离之前；它仍可证明普通触发门禁与主动路线基线，但不能证明当前伸展高度、保持时长、无抖动或落地碰撞表现。本轮仅完成新时序和所有中断清理路径的静态复核。
- 本轮新时序已用 Unity 6000.0.59f2 自带 Roslyn 和当前 `Assembly-CSharp.rsp` 后台完整编译，结果为 `0 error / 104` 条工程既有 warning，Level094 无诊断；未启动、聚焦或控制前台 Unity。
- 旧探针的盖板检查早于方向性位移演出，只能继续证明手势阈值和一次性提交。当前静态确认横滑方向被传入 Reveal、Alpha 与 `1.4` 水平位移同属一个可暂停 Sequence，PrepareAttempt 会恢复盖板局部原位；新动画尚未做动态观感验证。
- 方向性揭盖代码已通过 Unity 6000.0.59f2 Roslyn 后台完整编译：`0 error / 104` 条工程既有 warning，Level094 无诊断。Prefab 为 `186/186` 个唯一对象定义、`0` 重复、`0` 悬空本地引用，新字段一次；`git diff --check` 通过且没有操作前台 Unity。
- 普通回弹碰撞隔离和 `passiveExtensionRatio=0.27` 已通过同一后台 Roslyn 完整编译：`0 error / 104` 条工程既有 warning，Level094 无诊断。静态调用链确认隔离只在 `LaunchPassiveBounce()` 成功校验后开启，正常/异常清理均恢复缓存状态；主动下拉与共享 Actor Prefab 未修改。
- 探针没有发送 OS 输入或操作前台 Unity；PASS 后主动退出时出现的全局 DB/PlayCtrl 清理空引用发生在 `EditorApplication.Exit(0)` 之后，不计为 Level094 失败。临时探针及 `.meta` 随后删除，正式逻辑没有保留测试依赖。
- 暂停/重开、压缩期间快速走离、真实鼠标/触摸热区和移动端验证仍待执行；两条主动释放路线的运行逻辑已通过后台探针，画面与力度仍需策划验收。

## 本轮风险复核：普通踩踏再加高与重复抖动（2026-08-11）

- 用户反馈的重复抖动与两个可复现的静态风险相符：移动中的 Kinematic 红按钮会被 Actor 的接地 BoxCast 误判为地面，按钮 Rigidbody2D 的共享 `Bouncy` 材质又会放大恢复碰撞后的二次弹跳。
- 当前 `passiveExtensionRatio` 为 `0.405`（上一版 `0.27` 的 `1.5` 倍）。为防止上限静默截断，脚本 `Range` 与 `OnValidate` 上限同步提高到 `0.5`；主动 `launchExtensionRatio=0.18` 未变。
- 解决方案局限在 Level094：被动 Collider ignore 有显式活动标记，落地结束、重武装和新交互均等待标记清除；所有清理路径先把按钮归零、同步物理，再恢复原始 ignore 状态。Prefab 仅清除按钮 Rigidbody2D 的材质引用，不改共享资源或 Layer Matrix。
- 后台 Roslyn 完整编译为 `0 error / 104` 条工程既有 warning，Level094 无诊断；Prefab 静态扫描为 `186` 个定义、`0` 重复 fileID、`0` 悬空本地引用。无抖动、当前高度、低帧率接地和移动端触摸仍是 `uncertain`，必须前台 PlayMode/真机验收。
- 回滚本轮可将 `passiveExtensionRatio` 恢复为 `0.27`，还原 `Range`/`OnValidate` 上限，并撤销按钮实例的材质清除及被动隔离门禁；不应修改主动路线、共享 `Bouncy` 资产或 Actor。

## 未验证缺口

- 后台运行探针已验证调整前的首次 `y=10` 小弹、原地落回不连弹、走离重武装、主动压缩同步、取消不误发射、两条主动发射路线、最后 `1/3` 缩小和成功结算；当前 `0.405` 伸展高度、最高点保持/收回、碰撞隔离后的无抖动和落地恢复，以及压缩期间快速走离不隔空赋速尚未动态验证。
- 尚未动态验证盖板向左/向右滑出的方向、`1.4 / 0.22s` 揭盖观感、暂停恢复轨迹连续性及 Reset/Replay 原位恢复。
- 探针为避免抢占用户输入而直接调用本关输入边界，因此完整红色按钮屏幕热区、真实触摸起手、`14px` 和当前 `0.5` 临界手感、普通抛射方向/力度、成功弧高/时长、渐隐曲线与音效仍需策划验收。
- 尚未验证左/右地面与盖板临时 Collider 在低帧率、高速落下和边缘走动时不会穿透或卡住。
- 尚未验证暂停恢复、失败重开、手动重开、退出、Replay 和成功途中销毁的状态恢复。
- 普通弹跳的等待/冷却使用 `Time.time`；非 `PLAYING` 期间不执行状态更新，但恢复时可能立即满足等待条件，实际观感仍需 PlayMode 确认。
- 尚未验证 Android/iOS 横屏单指、多指、取消、切后台、不同屏幕比例和性能。
- Tips 仍沿用 Level032；Level094 独立提示、答案和通关文案的最终显示需从正式入口验证并由策划确认。

## 回滚路径

- 回滚 `Level_094.prefab` 的 Level94Ctrl、地面实例碰撞、盖板/洞壁初始状态、按钮刚体设置和旧回调清理，即可恢复复制基线。
- 删除 `Level94Ctrl.cs/.meta` 与本目录两份文档。
- 共享 `Ground_mid.prefab`、Actor、`BaseLevelCtrl`、Level032、配置及 L94 美术没有修改，无需回滚。
- 只回滚本轮普通踩踏展示时，恢复 `passiveExtensionRatio=0.08` 与旧的立即伸展/回位序列即可；不要改 Actor `y=10`、主动下拉阈值或两条释放路线。
- 只回滚盖板演出时，删除 `coverSlideDistance`、方向参数和原位缓存，将 Reveal 恢复为原地 Alpha Tween；不要回滚既有横滑门槛、盖板 Collider 关闭或预拆地面。
- 只回滚本轮抖动和高度调整时，将 `passiveExtensionRatio` 恢复为 `0.18` 并删除普通发射阶段的 Collider ignore 缓存/恢复；不要修改主动 `launchExtensionRatio=0.18`、Actor 速度、Layer Matrix 或共享 Actor。

## 本轮风险复核：恢复上一版弹簧表现（2026-08-11）

- 已撤销欠压 `(3, 7) / 0.5`、失败后 `-0.18` 保持、普通踩踏永久保持 `-0.405` 及其 `2.8s` 被动飞行兜底；当前重新以 `(8.5, 19) / 0.65` 完整回位，并在普通踩踏越过最高点后收回弹簧。
- 回退恢复了 `passiveReturnStarted`、最高点单次回收和 Actor/红按钮 Collider 临时隔离。所有既有中断路径仍先归零按钮、同步物理 Transform，再恢复原碰撞状态；`AnimateInsufficientReleaseAndHold` 与 `passiveActorLaunchElapsed` 均不存在。
- 继续保留 `passiveExtensionRatio=0.405` 和红按钮无 Bouncy 材质，因此本轮没有重新引入此前确认的弹性材质二次抖动来源，也没有扩大到共享物理配置或其它关卡。
- 当前脚本和 Prefab 与目标旧快照逐字一致；后台 Roslyn 编译为 `0 error / 104` 条工程既有 warning，Prefab 为 `186` 个唯一定义、`0` 重复、`0` 悬空本地引用，Level94/Level32 GUID 为 `1/0`，旧回调为 `0`。未操作前台 Unity；实际欠压轨迹、最高点回收观感、低帧率碰撞恢复和移动端输入仍为 `uncertain`。

## 本轮风险复核：半程成功与主动拖拽防穿模（2026-08-11）

- 成功阈值已从 `0.67` 放宽为 `0.5`，脚本默认与 Prefab 序列化值一致；月球飞行渐隐起点仍为 `0.67`，没有随成功阈值提前。旧 `PASS checks=29` 使用旧成功边界，只能作为两条路线可达的历史证据。
- 原穿模窗口位于“按钮按下成功、但尚未向下移动满 `14px`”之间。当前候选按下即接管 Actor，轻点、Canceled、指针丢失和暂停均有释放路径；恢复 `Rigidbody2D.simulated` 前统一同步 Transform，携带时同时写 Rigidbody2D position。
- 曾评估在井中部放置 `2.14 x 0.6` 实体平台，但独立复审确认其横向几乎封满洞口，角色若落入会被困在按钮与平台之间。该 Collider 已完全撤销，Prefab 中对应临时 fileID 为 `0` 次；最终防护不依赖共享碰撞配置。
- `CleanupInterruptedAttempt()` 已调整为：仍在主动携带时先将压缩归零、把 Actor 随按钮带回休止位并 `Physics2D.SyncTransforms()`，之后才恢复刚体与控制。独立复审确认此前平台软锁和中断残留两项 P1 均关闭，未发现新的 P0-P2。
- 后台 Roslyn 使用 Unity 6000.0.59f2 当前 Bee 响应文件编译通过，`0 error`；Prefab 为 `186/186` 个唯一定义、`0` 重复、`0` 悬空本地引用，Level94/Level32 GUID 为 `1/0`，旧回调为 `0`，`successCompressionRatio: 0.5` 恰好一次。未启动、聚焦或操作前台 Unity。
- 真实 `0.49/0.50` 分流、轻点后立即恢复、Touch Canceled/丢指、暂停/禁用恢复、低帧率拖动和 Android/iOS 触摸仍为 `uncertain`，必须由前台 PlayMode/真机验收。
- 仅回滚本轮时，将脚本与 Prefab 的 `successCompressionRatio` 恢复为 `0.67`，撤销候选按下的提前接管、轻点释放和中断前归零携带逻辑；不要增加深井常驻 Collider，也不要修改共享 Actor、Layer Matrix、Level032 或 GamePanel。

## 本轮风险复核：主动弹射叠加抛物线音效（2026-08-11）

- 工程证据确认目标资源是 Level093 的 `l93PlayerLaunch`（原文件名 `抛物线.ogg`）；Level088 没有角色抛物线专用音效。复用现有 SoundsGood 登记，不复制音频、不新增 tag，也不修改参考关卡。
- 欠压侧抛在刚体初速度成功写入后播放，月球路线在 Tween 成功建立后播放；月球目标缺失并回退欠压时不会先播一次再重复。两个入口各保留原 `spring`，被动小弹不加甩飞声。
- 后台 Roslyn 编译为 `0 error / 104` 条工程既有 warning，Level094 无诊断；静态扫描为 `1` 个常量、`2` 个主动调用点、`0` 个被动调用点，`SoundCollection.asset` 中 tag 恰好登记一次。独立复审未发现 P0-P2。
- 尚未动态确认 `spring` 与 `l93PlayerLaunch` 同帧叠加的响度、音色遮蔽、暂停/恢复和移动端扬声器表现，标记为 `uncertain`。若混音过满，仅移除两个新增调用即可恢复原声音，不应修改共享音频资源或 Level093。
