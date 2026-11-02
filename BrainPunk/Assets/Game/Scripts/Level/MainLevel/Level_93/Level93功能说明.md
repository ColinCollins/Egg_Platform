# Level093 功能说明

## 需求与背景

Level093 的整关 Prefab 由 Level073 复制后重新制作。本关不再使用 073 的长按蓄力跳，主要玩法改为：拖动竖轨平台、点击齿轮让杠杆平台顺时针旋转、根据角色站位执行普通抛射或飞向 EndPoint，并通过尖刺造成失败。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_093.prefab`
- `Assets/Game/ArtSrc/Sprites/Sp_l93_jc_01.png`
- `Assets/Game/Scripts/Level/LevelItem/Level_93/Level93VerticalPlatformDrag.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_93/Level93PlatformPingPong.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_93/Level93GearLever.cs`
- 仅复用且未修改：`BaseLevelCtrl`、`ActorCtrl`、`CameraFollowArea`、`TrapEventHandlle`、`OnTrigger2DHandle`、DOTween、`platform_Stone.prefab`、`Knock_Explosion.prefab`

本次没有修改共享角色、共享 UI、关卡加载、配置表、存档、埋点、广告、IAP、RemoteConfig、YooAsset、平台 SDK 或其他关卡。

## 原有行为基线

- 093 Prefab 复制自 073，根节点原有 `Level73Ctrl` 会接管跳跃按钮、蓄力动画和蓄力条。
- 093 场景中的轨道平台、旋转平台和尖刺只有美术与基础碰撞，尚无本关专用交互链。
- 失活的 `L73` 蓄力条视觉层级来自复制源，但不会自行显示。

## 实现后行为

### 竖轨拖拽平台

- 三个轨道平台的可站立 `Group` 已挂 `Level93VerticalPlatformDrag`。
- 只响应命中指定 Collider2D 的鼠标或触摸；拖动时锁定本地 X/Z，只改变本地 Y。
- `minimumLocalY` 与 `maximumLocalY` 定义平台中心的上下限；当前三个 Prefab 绑定均为 `-1.5 ~ 1.2`，初始位置不要求位于区间中点。
- 选中组件时，Scene 视图会绘制范围框、中轴线和两个端点 Gizmo。
- `clampPositionOnEnable` 默认关闭，避免初始位置在范围外时一启用就跳位；首次拖动后才限制到区间。
- 每个目标都在 093 Prefab 内配置 Kinematic `Rigidbody2D`。输入只在 `Update` 记录目标，平台在 `FixedUpdate` 通过 `MovePosition` 移动。
- `carryStandingActor` 默认开启。平台是唯一的位置驱动方：Kinematic 平台继续使用 `MovePosition`，Dynamic 角色不再调用 `MovePosition`，而是在每个物理步使用“平台速度 + 玩家相对水平速度”跟随。首次锁存只接受角色与当前平台 Collider 已实际接触，或 `ColliderDistance2D` 不大于 `max(0.02, Physics2D.defaultContactOffset * 2)` 的紧贴状态，不做预测或扫掠吸附。主动跳跃会直接解除且保留跳跃速度；自然离开时仅移除当前仍存在的平台速度分量。

### 两点往复平台

- 当前有七个 `platform_Stone` 实例挂载 `Level93PlatformPingPong`，同时仍可把该本关组件挂到其他 093 平台。
- `firstPointOffset` 与 `secondPointOffset` 都相对组件首次启用时的平台本地位置，可同时编辑 X/Y，从而制作水平、竖直或斜向往复。
- `moveSpeed` 控制速度，`waitAtEndSeconds` 控制端点停留，`playOnEnable` 控制是否自动播放。
- 选中组件时，Scene 视图会显示端点和连线。七个平台均使用 093 本地 Kinematic `Rigidbody2D`，在 `FixedUpdate` 移动并按与拖拽平台相同的锁存规则携带角色；进入端点等待后的下一个物理步会同时清零平台和角色继承的载具速度。平台与拖拽速度均受 `Physics2D.maxTranslationSpeed` 限制，避免请求速度高于物理引擎实际可执行速度。
- 不应让拖拽组件与往复组件同时驱动同一个 Transform。

### 齿轮杠杆与通关

- 点击 `chilun` 的 CircleCollider2D 后，`platform_Stone (3)` 每次顺时针旋转 90 度。
- 当前平台初始为竖向；第一次点击可把它转成横向。
- 点击前若角色没有站在平台上，平台只旋转。
- 点击前若角色站在平台上但不满足“横向平台左侧边缘”条件，角色会随杠杆转到约 43 度时提前释放。普通抛射初速度由 `12` 的力度生成，顺时针切线会向上抛模板混合 `40%`，避免低角度贴地飞出；落地即恢复操作，`0.8` 秒仅作为超时兜底。
- 点击前若平台为横向、角色已落地且站在左侧 28% 边缘区，角色同样在旋转过程中提前释放，并沿显式抛物线飞向 `EndPoint`。当前旋转时长 `0.2` 秒、飞行时长 `2.45` 秒、弧高为 `20`；飞行仍使用 `Linear` 时间参数，弧线附加的初始竖直速度由约 `23.7` 提高到 `32.7`，横向速度和落点位置不变。
- 成功飞行期间角色 Rigidbody2D 暂停模拟，因此不会触发沿途尖刺；到达后调用 `BaseLevelCtrl.SuccessStrightly()`，沿用既有关卡成功归口。
- 非 `PLAYING` 或 `GamePauseEvent` 状态下拖拽和齿轮不再接受新输入，齿轮 Tween 会暂停。普通物理抛射会缓存速度并暂停 Rigidbody2D 模拟，恢复后一帧重新锁住角色输入并继续原速度，控制恢复协程在暂停期间不计时。
- `platform_Stone (3)` 的承载 Collider 在 093 Prefab 内由 Ground 改为 `WithoutShadowGround`（Layer 13）。本关 Actor 实例将 `whatIsGround` 局部扩展为 `8520`，保证 Layer 13 仍可落地；Shadow 的 `ignoreLayer` 局部设为 `8192`，射线命中该平台后直接隐藏影子，也不会穿透到下方地面继续绘制。共享 Actor、ShadowController 与平台 Prefab 均未修改。

### 尖刺失败链

- 19 个尖刺均使用 `Sp_l93_jc_01`，Transform 缩放为 `1`，SpriteRenderer 与 Trigger `BoxCollider2D` 尺寸均为 `0.49 x 0.48`。
- 19 个尖刺均配置 `OnTrigger2DHandle`。
- `targetTag` 为 `Player`，`onEnter` 统一调用 093 根节点的 `TrapEventHandlle.PlayKnock`。
- 根陷阱组件引用本关 Actor 与 `Knock_Explosion`，复用 067 等关卡已使用的失败链。

## 数据、资源与事件流

```text
触摸/鼠标命中轨道平台 -> 记录受限本地 Y -> FixedUpdate/MovePosition -> 同物理帧携带锁存角色

触摸/鼠标命中齿轮 -> 检查平台方向与角色站位 -> 顺时针旋转 90 度
  -> 转到约 43 度时释放
  -> 非边缘：恢复 Rigidbody2D -> 顺时针切线小幅物理抛射 -> 落地恢复操作
  -> 左边缘：保持 Rigidbody2D 不模拟 -> 显式高抛物线到 EndPoint -> SuccessStrightly

Player 进入尖刺 Trigger -> OnTrigger2DHandle -> TrapEventHandlle.PlayKnock -> 既有失败/重开流程

Shadow 向下射线命中 platform_Stone (3) 的 Layer 13 -> ignoreLayer 提前返回 -> 不创建玩家影子
```

本关脚本只订阅既有 `SwitchGameStateEvent`、`GamePauseEvent`、`GameResumeEvent` 与 `PlayerJumpEvent`，用于停止非游戏态交互及解除平台随动；没有新增全局事件，也没有直接读写 DB 或配置。

## 验证证据

- 独立 C# 编译：使用 Unity 6000.0.59f2 的 Roslyn、当前 `Assembly-CSharp.dll` 与 Bee 响应文件中的项目引用，针对两个平台脚本和 `Level93GearLever.cs` 生成 `Temp/CodexLevel93HighSpeedCompile/Level93HighSpeedCompile.dll`，编译通过，无 C# 编译错误。
- Unity 正式编译：刷新资源后，`Library/ScriptAssemblies/Assembly-CSharp.dll` 已于 2026-07-21 11:24:35 重建；`Editor.log` 未出现 `error CS`，也未出现 Level093 脚本的编译错误。
- Prefab 静态检查：424 个 YAML 文档对应 424 个唯一 fileID，无重复 fileID、无缺失本地引用。
- 绑定静态检查：`Level73Ctrl` GUID 为 0 次；3 个拖拽组件、7 个往复组件、1 个齿轮组件与 10 个同物体 Kinematic Rigidbody2D 均存在。
- 尖刺静态检查：19 个新 Sprite GUID、19 个 `0.49 x 0.48` Trigger 和 19 个 `PlayKnock` 持久绑定均存在；旧尖刺 Sprite GUID 为 0 次。
- 资源静态检查：旋转平台根节点包含非 Trigger `BoxCollider2D`；齿轮、旋转平台与 EndPoint 引用可解析；共享 `platform_Stone.prefab` 未修改。
- 阴影静态检查：`platform_Stone (3)` 根 Layer 为 `13`，其他石台仍为 `6`；093 Actor 实例的 `whatIsGround.m_Bits=8520`、`ignoreLayer.m_Bits=8192`，Physics2D 全层碰撞矩阵保持启用。
- 源码静态检查：无 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、PlayerPrefs 或运行时实例化路径。

## 未验证项

- 前台编辑器当前打开 `Loading` 场景；本轮已通过独立 Roslyn 编译和 Unity 正式程序集重建，但尚未进入 093 执行实际关卡流程。
- 上一版已在 PlayMode 暴露速度 `30` 的竖直往复平台周期性弹飞问题；本轮已按实际现象改为单一平台运动权威，但尚待重新验证 `platform_Stone (10)`、`(11)`、`(12)` 的上升、下降、端点等待、反向、主动跳跃和走出边缘。
- 齿轮成功抛射的弧高已提高到 `20`，普通抛射维持力度 `12` 与上抬方向；尚待 PlayMode 确认相机上边界、普通抛射落点、尖刺碰撞节奏和两条路线的最终手感。
- 旋转平台已使用 `WithoutShadowGround`，但尚待 PlayMode 确认站立、跳跃、横竖状态切换时均不显示玩家影子，且 `IsGround` 持续为真。
- 尚未在 Android/iOS 横屏真机验证触控命中、暂停恢复、帧率和不同屏幕比例。
- 七个往复平台的端点、速度和停留参数已绑定，最终节奏仍需关卡设计在 Scene Gizmo 与 PlayMode 中验收。

## 回滚路径

从 `Level_093.prefab` 移除三个拖拽组件、七个往复组件、十个本地 Kinematic Rigidbody2D、齿轮上的本关组件与尖刺事件绑定，删除 `Level_93` 下三个本地脚本即可撤回玩法实现。共享 Actor、UI、尖刺逻辑、平台 Prefab、加载、配置和其他关卡均无需回滚。

## 2026-07-22 齿轮旋转与抛射音效

### 需求与影响范围

原有齿轮旋转没有专用声音，成功飞行和普通抛射都播放通用 `jump`。本次新增 `l93GearRotate` 与 `l93PlayerLaunch`，只修改 `Level93GearLever.cs`、L93 音频目录和 SoundsGood 登记；平台旋转、抛物线参数、无敌/操作锁定、尖刺、阴影、Prefab 与共享 `AudioManager` 均不变。

### 新行为与资源流

- `齿轮转动.ogg` 原样复制为 `Assets/Game/ArtSrc/Audio/Sfx/l93/l93GearRotate.ogg`；`抛物线.ogg` 原样复制为同目录 `l93PlayerLaunch.ogg`。两者保留 44.1 kHz Ogg/Vorbis 立体声，不再次转码。
- `TryHandleClick()` 在合法齿轮点击被接受并进入旋转状态时播放一次 `l93GearRotate`，没有命中齿轮、暂停或旋转期间的输入不播放。
- 成功路线在 `BeginEndpointFlight()` 真正建立飞行 Tween 后播放一次 `l93PlayerLaunch`；非边缘路线在刚体初速度写入后播放一次同一声音。原有两处 `jump` 被替换，不与新声音叠播。
- 两种声音都是 SoundsGood 一次性音效，不新增循环句柄、全局停止或关卡外逻辑。

### 验证、缺口与回滚

- 两个目标文件与用户源文件 SHA-256 一致；Collection tag/GUID、生成项和两个抛射分支调用已静态核对。
- `Assembly-CSharp` 与 `SoundsGood.Application` 使用 Unity 6000.0.59f2 Roslyn 独立编译通过，均为 0 个错误。
- 当前 Unity 编辑器已成功导入两个 L93 音频和更新后的 Sound Collection，并于 15:22 重建两个目标程序集；最近刷新日志相关错误为 0。
- PlayMode 仍需确认 1.05 秒齿轮声与 0.2 秒旋转的观感、约 0.1 秒素材起音、成功/普通抛射各一次、暂停/退出尾音和 Android/iOS 真机音量。
- 回滚时恢复两处通用 `jump`，移除齿轮播放调用及 L93 两个 tag/资源；不回滚齿轮玩法、平台参数或共享音频系统。
