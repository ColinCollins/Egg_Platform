# Level101「眩晕」功能说明

更新时间：2026-08-26

## 当前有效结论与证据边界

本关是一个局部实现的“先误导、后砸门、再解锁”玩法：玩家可以拖走门锁，但锁会在门的重新上锁动画结束后回到原位；真正解法是把箱子举到门顶后释放，让箱子从上方砸中门。砸中后隐藏原 `body` 门 Spine，显示对齐后的正常 `door`、`door_0/effect` 和锁图片，门仍为实体；玩家再次拖走锁并等待渐隐完成后，才把门切为 Trigger 并复用 `BaseLevelCtrl` 的正常通关链。

本文以当前工作区的 `Level101Ctrl.cs`、两个局部拖拽适配器、Level101 Prefab、`door_0` Prefab 和已导入特效资源为依据。控制器的状态与清理代码已有静态证据；Level101 Prefab 已显式绑定三个局部脚本、门表现、锁、箱子和失活的星星特效嵌套实例，`door_0` 根节点显式保持激活，子 `door`/`effect` 初态隐藏。此前版本的 Level101 PrefabImporter 已成功；最终嵌入星星实例后的导入与运行表现仍需 Unity 复核，不能把旧导入结果描述为最终玩法验证。

## 需求与玩法目标

- 锁是可拖拽物。拖拽结束后，锁在玩家释放位置渐隐，门播放重新上锁动画；动画完成后门回到待机，锁图片恢复原始局部位置、旋转、缩放和颜色。
- 用户称谓中的 `locked` 动画，在当前 Spine 资源中实际名称为 `lock`。本关把动画名作为序列化字段，默认使用资源真实名称 `lock`，不改写共享 Spine 文件中的动画名。
- 箱子可被举起并拖到门的上方。释放后箱子恢复 Dynamic 刚体并受重力下落；只有已记录的释放高度、箱子向下位移、门顶空间关系、接触法线和撞击速度同时满足门禁时才进入眩晕。
- 箱子命中时显示 `StunnedCirclingStars` 星星、纯撞击粒子和箱子碎裂粒子；隐藏 `body`，显示 `door_0/effect`、对齐后的 `door_0/door` 与锁图片。此时只允许拖锁，箱子已碎并隐藏；门保持非 Trigger 且排除 Actor，角色可穿过但不会触发通关，最终拖锁渐隐完成后才启用门 Trigger。
- 暂停、Reset、Replay、退出和销毁时不得遗留拖拽、Tween、Spine 完成回调、协程、刚体速度或运行时特效实例。

## 影响范围

### 本关新增或局部修改

- `Assets/Game/Scripts/Level/MainLevel/Level_101/Level101Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_101/Level101LockDragItem.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_101/Level101BoxDragItem.cs`
- `Assets/Game/Prefabs/Level/Level_101.prefab`
- `Luban/Configs/Datas/LevelData.xlsx`、`Luban/Configs/Datas/LevelSort.xlsx`
- `Assets/Game/Configs/tbleveldata.json`、`Assets/Game/Configs/tblevelsort.json`

### 依赖的既有资源与系统

- `Assets/Game/Prefabs/LevelItems/door_0.prefab`：包含 `effect`、`door`、`ribbon`、`LockPoint`、门 Collider 和既有 `OnTrigger2DHandle`。`LockPoint` 是门 Prefab 的既有节点，不作为锁图片恢复点；恢复使用锁第一次运行时捕获的原始局部姿态。
- `Assets/Game/ArtSrc/Spines/sp_door-move_001/`：`body` 的 Spine 数据包含 `idle` 与 `lock` 等动画；旧门 `door` 使用 `door_idle` 等动画。
- `Assets/Game/ArtSrc/Sprites/l101/lock.png`：本关锁图片。
- `Assets/Epic Toon FX/Prefabs/Combat/Brawling/Stun/StunnedCirclingStars.prefab`：眩晕星星特效。`Level_101.prefab` 内保存一个失活的嵌套实例并绑定到 `stunnedEffect`；PlayMode `Awake()` 优先复用该场景实例，外部 `stunnedEffectPrefab` 只作旧 Prefab 兼容兜底。实例挂在 `Level_101` 根，避开 `door_0` 根 `SortingGroup=100` 对子粒子的统一排序。
- `Assets/Epic Toon FX/Textures/star.png`：Level101 局部星星贴图。项目既有同 GUID `star_AB` 材质当前指向明信片星图，本关通过运行时实例的 `MaterialPropertyBlock` 只覆盖 `_MainTex`，不修改共享材质或共享特效 Prefab；既有 `glow1_ADD` 保留，当前 additive 语义与包内资源等价。
- `Assets/Game/ArtSrc/Particles/Prefabs/Knock_Explosion.prefab`：复用根 ParticleSystem 作为纯撞击反馈；运行时关闭直属子节点 `Knock 1`，不显示文字。
- `Assets/Game/ArtSrc/Particles/Prefabs/BoxBroken.prefab` 与既有 `crashBroke` 音效：复用 Level007 的短时碎箱粒子和声音，不复用 Level007 的 `Box_2` 静态残骸。
- `BaseLevelCtrl`、`DragableItem`、`ActorSpineCtrl`、DOTween 和现有 Game 状态事件。共享脚本本轮不改。

## 原有行为基线

Level101 原先只有标准主线关卡根控制器和场景物体，没有本关“拖锁重锁、箱子砸门、眩晕切换”的局部状态机。`door_0` 来自既有门 Prefab，其 Trigger 事件最终调用 `BaseLevelCtrl.OnActorTrigger2D`；Level101 初始需要把门碰撞体设为非 Trigger 以接收箱子实体撞击，同时局部排除 Actor Layer，成功条件成立后再移除排除并切回 Trigger。Level101 作为新增关卡，不改变 Level096、Level128 或其它已验收关卡。

截至 2026-08-25 静态复核，`Level_101.prefab` 同时包含标准 `BaseLevelCtrl` 和三个 Level101 局部脚本的序列化组件，控制器已显式引用门 Collider、`door`/`effect`/`body`、锁、箱子 Rigidbody、失活的 `StunnedCirclingStars` 嵌套实例和门根锚点；`door_0` 根节点（fileID 837609）显式为激活，子 `door`（610247）与 `effect`（645586）有 `m_IsActive=0` 初态覆盖，符合 body 可见、旧门/效果隐藏的设计。`Logs/AssetImportWorker11.log` 只证明此前版本的 Level101 PrefabImporter 成功；最终 Prefab 仍需重新导入并做 PlayMode 复核。

## 局部状态机

| 状态 | 进入条件 | 允许行为 | 离开条件 |
| --- | --- | --- | --- |
| `LockedIdle` | 初始、重锁完成、未命中恢复、Reset/Replay | 锁拖拽、箱子拖拽；门为非 Trigger，与箱子实体碰撞并排除 Actor | 锁释放进入 `Relocking`；箱子按下进入 `BoxDragging` |
| `Relocking` | 锁拖拽结束且仍处于 Playing | 锁输入关闭；执行渐隐和 `body` 的 `lock` 动画 | TrackEntry `Complete` 或兜底时长后恢复锁和 `idle`，回到 `LockedIdle` |
| `BoxDragging` | 箱子拖拽开始 | 箱子 Rigidbody 切为 Kinematic；锁输入关闭 | 正常释放进入 `BoxFalling`；暂停/状态切换取消并回到可玩态 |
| `BoxFalling` | 箱子释放，恢复 Dynamic 和重力 | 等待向下撞击；暂停时暂存速度并停止模拟 | 门顶有效撞击进入 `StunnedLocked`；未命中且速度稳定后回到 `LockedIdle` |
| `StunnedLocked` | 箱子向下撞击门顶 | 显示正常门、`effect`、锁和星星；只允许拖锁；门 Collider 仍为实体 | 锁释放进入 `Unlocking` |
| `Unlocking` | 眩晕后再次拖锁并释放 | 锁在释放位置渐隐；暂停时显式暂停 Tween；门仍为实体 | 渐隐完成后隐藏锁并进入 `DoorUnlocked` |
| `DoorUnlocked` | 最终锁渐隐完成 | 门 Collider 为 Trigger；复用标准进门通关链 | Reset/Replay/销毁时清理 |

状态只存在本关控制器实例，不写入存档，也不新增全局事件或共享状态。

## 关键流程

### 1. 拖锁与重新上锁

`Level101LockDragItem` 继承共享 `DragableItem`，只把 `OnDragStart`/`OnDragEnd` 转发给 `Level101Ctrl`。开始拖拽必须处于 `LockedIdle` 或 `StunnedLocked`，且游戏处于 Playing。

`LockedIdle` 中释放锁时控制器：

1. 关闭锁输入并进入 `Relocking`，递增流程版本号。
2. 对 `SpriteRenderer` 做短暂 `DOFade(0)`，Tween 绑定到本关对象并在禁用时 Kill。
3. 通过 `ActorSpineCtrl.PlayAnimation("lock", false, false)` 播放实际资源中的重锁动画。
4. 同时监听 Spine TrackEntry 完成，并启动序列化兜底时长（默认约 1.9 秒）；任一入口完成后解除回调，播放 `idle` 循环并对返回的 TrackEntry 设置 `MixDuration=0`，恢复锁的原始局部姿态和颜色，回到 `LockedIdle`。

`StunnedLocked` 中释放锁时不再播放 `lock`：锁在当前释放位置渐隐，渐隐完成后锁对象失活，门 Collider 才切为 Trigger。

暂停或切换游戏状态造成共享拖拽组件主动结束时，不进入重锁流程，而是直接恢复锁视觉，避免暂停被误判为玩家释放。

### 2. 举箱与砸门

`Level101BoxDragItem` 同样只负责局部转发。开始拖拽时控制器将箱子切为 Kinematic，结束拖拽且仍在 Playing 时恢复 Dynamic、清零旧速度并进入 `BoxFalling`。碰撞通过箱子适配器的 `OnCollisionEnter2D/Stay2D` 转发给控制器；控制器同时保留本关根节点碰撞入口作为兼容路径。

有效撞击同时满足：

- 当前状态为 `BoxFalling`；
- 碰撞关联 Rigidbody 是本关箱子；
- 碰撞中的任一 Collider 是序列化的门 Collider（不会把地面或其它物体当成门）；
- 箱子当前位置相对释放点至少下降最小距离，且箱子碰撞体中心仍在门顶上方；
- `Collision2D.relativeVelocity.y` 的绝对值达到最小撞击速度，避免依赖回调接收方导致的符号差异；
- 任一接触点位于门 Collider 顶部容差内，且接触法线具有足够的竖直分量。

命中后箱子清空速度、切为 Kinematic、停止模拟并隐藏根对象，状态进入 `StunnedLocked`。同帧在接触点播放隐藏 `Knock 1` 文字的撞击粒子，在箱子中心播放 `BoxBroken` 并播放 `crashBroke`；两个粒子实例按自身完整寿命销毁，不保留静态残骸。未命中时，箱子停稳超过短暂恢复延迟后重新开放两种拖拽，避免一次误放造成软锁。

### 3. 眩晕表现与通关

进入 `StunnedLocked` 后：

- 隐藏当前 `body` 门 Spine；
- 打开 `door_0/effect`，显示 `door_0/door` 并播放 `door_idle`，返回的 TrackEntry 显式设置 `MixDuration=0`；
- 每次切换前对正常门应用 `localPosition=(0.1058,-1.5815,0)` 和 `localScale=(1.0283,1.0165,1)`，同一补偿也应用到 `effect` 和成功流的 `ribbon`，避免叠加、切门及进门时位置/尺寸跳变；
- 恢复锁图片的原始姿态，锁 SpriteRenderer 使用 order `101`，位于正常门 order `100` 之前；
- 把 `StunnedCirclingStars` 运行时实例挂在 `Level_101` 根，定位到指定锚点（当前 Prefab 绑定 `door_0` 根，未绑定时代码缺省使用 body Transform）并显示；实例内四个 Renderer 统一设置为 `SceneLayer`，当前最终 sorting order 为 `110/111`，保留资源内部 `0/1` 相对层次；
- 保持 `door_0` 根 Collider 为实体，只开放锁拖拽；锁最终渐隐完成后才切为 Trigger；
- 保留门 Prefab 已有的 `OnTrigger2DHandle -> BaseLevelCtrl.OnActorTrigger2D` 接线，不复制成功/存档/统计逻辑。

初始状态应为 `body=true`、`effect=false`、`door=false`、门 Collider 为实体；Reset/Replay 必须恢复这一组值。Prefab 序列化接线和 Unity 导入已经通过静态证据确认，实际显隐时序与通关表现仍以 PlayMode 验收为准。

## 暂停、Reset、Replay 与销毁

- 订阅 `SwitchGameStateEvent`、`GamePauseEvent`、`GameResumeEvent`、`GameResetEvent`、`GameReplayEvent`。非 Playing 时关闭本关输入；重锁阶段取消完成回调/协程并恢复锁；箱子下落阶段暂存速度、停止模拟；`Unlocking` 阶段显式 Pause/Play 锁渐隐 Tween，因为项目暂停不会把 `Time.timeScale` 设为 0。
- `ResetRuntimeState` 递增流程版本，Kill 锁 Tween，停止重锁/未命中协程，移除 Spine Complete 回调，清理两个临时粒子实例，恢复门表现、非 Trigger Collider 与 Actor 局部排除、锁姿态、箱子初始 active/姿态/Rigidbody 状态，并重新开放拖拽。
- `OnDisable` 先清理运行时状态和事件订阅，再关闭两个局部组件的拖拽能力，避免卸载/暂停过程中共享 `DragableItem` 反向触发新流程。
- `OnDestroy` 再次解除 Spine 和事件引用；若眩晕特效是本关运行时实例则销毁。当前实现没有新增存档字段，下一次关卡实例不会继承状态。

## 配置、资源与事件流

- LevelData 正式源表已落盘并回读 `10101 -> Level_101`，标题/备注为“眩晕”，位于 `10999 / Level_999` 哨兵行之前；`Assets/Game/Configs/tbleveldata.json` 已生成相同 ID 与路径。
- LevelSort 正式源表已在现有产品排序尾部追加连续排序 ID `108` 指向 `10101`，未重排既有 `107 -> 10128`；正式行已回读为 `A113:D113=[null,108,10101,"眩晕"]`，相邻 `A112:D112=[null,107,10128,"当前时间密码"]` 保持不变。`Assets/Game/Configs/tblevelsort.json`、`Assets/Guru/guru_spec.yaml` 和 `Assets/Guru/Gen/GuruAllSpecs.G.cs` 均已同步末尾 `10128,10101`。
- 本关仅使用当前关卡实例内的输入、物理、Spine、Tween 和已有门 Trigger，不新增 PlayEvents、网络请求、广告、付费、存档或 analytics 字段。

## 验证记录

### 已完成的静态核对

- 已读取并核对 `Level101Ctrl` 七个局部状态、两阶段解锁、暂停/Reset/Replay 清理、锁姿态捕获、Spine 完成回调解绑和特效实例生命周期。
- 已核对 `door_0.prefab` 层级、门 Collider、`OnTrigger2DHandle` 和实际动画名；当前资源使用 `lock`，不存在名为 `locked` 的动画。
- 已确认眩晕特效、`Knock_Explosion`、`BoxBroken` 及其外部资源 GUID 存在；锁图片位于 `Assets/Game/ArtSrc/Sprites/l101/lock.png`。
- 已核对 unitypackage 的 10 项特效依赖均存在，其中 8 项内容哈希一致；共享 `StunnedCirclingStars.prefab` 与包内 SHA256 一致。相同 GUID 但内容不同的既有 `star_AB`、`glow1_ADD` 未被覆盖，差异由 Level101 实例级贴图替换和等价 additive 语义局部兼容。
- 已核对新增脚本 `.meta` GUID 与脚本路径存在。

### 尚未完成或不能替代运行验证的项目

- Level101 Prefab 已静态出现 `Level101Ctrl`、`Level101LockDragItem`、`Level101BoxDragItem` 三个脚本 GUID，并显式绑定门 Collider、`door`/`effect`/`body`、锁、箱子 Rigidbody、失活星星实例、`Knock_Explosion` 和 `BoxBroken`；门对齐值、粒子 order `200/201` 与锁 order `101` 已显式序列化。当前 Prefab 静态扫描为 172 个定义、0 重复 fileID、0 缺失本地引用；最终修改后的 PrefabImporter 与 PlayMode 仍未执行。
- 正式 LevelData/LevelSort 源表、两份 Luban JSON 和 Guru 默认排序已同步回读。Unity 6000.0.59f2 当前 Bee 响应文件的完整 Roslyn 编译为 `exit 0`，Level101 无诊断；该证据不能替代 PlayMode。
- 锁拖拽/无融合重锁、箱子物理撞击、纯撞击与碎箱粒子、无残骸、正常门像素级对齐、星星尺寸/位置/遮挡、二次移锁后的 Trigger 通关、连续 Reset/Replay、暂停恢复和 Android/iOS 横屏触控均为 `uncertain`。

## 回滚路径

1. 若只回滚本关玩法，移除 Level101 Prefab 上的局部控制器/拖拽脚本和 `Level_101` 局部资源，恢复门 Prefab 原有状态；不修改 `door_0.prefab`、`BaseLevelCtrl`、`DragableItem` 或其它已验收关卡。
2. 若只回滚眩晕表现，移除 `StunnedCirclingStars` 的本关引用、实例级排序/贴图覆盖和 `effect/door/body` 切换，保留标准门 Trigger 链；共享特效 Prefab、`star_AB` 与 `glow1_ADD` 不需要回滚。
3. 若只回滚配置，删除 LevelData/LevelSort 源表、生成 JSON 和 Guru 默认值中唯一的 `10101/Level_101` 记录，并重新生成配置；既有 `10128` 记录保持不变。
4. 文档与脚本目录的回滚应和 Prefab、资源、配置一起执行，避免留下无法加载的脚本 GUID 或孤立资源。

## 2026-08-25：砸晕后二次移锁与命中反馈补正

- 行为从“砸中即通关”改为两阶段：砸中只进入 `StunnedLocked`，显示正常门和锁；门继续与箱子保持实体碰撞并排除 Actor。玩家再次拖锁并等渐隐完成后才进入 `DoorUnlocked`。
- `lock -> idle` 通过 TrackEntry `MixDuration=0` 禁用动画融合。正常门与成功 `ribbon` 共用精确的位置/缩放补偿；锁 order 为 `101`，位于门 order `100` 前。
- 命中时复用 `Knock_Explosion` 但关闭 `Knock 1` 文字，复用 Level007 `BoxBroken` 和 `crashBroke`，隐藏原箱子并定时销毁两个粒子实例，不保留 `Box_2` 或其它残骸。
- 暂停时显式暂停 `Unlocking` 渐隐 Tween，恢复时继续，避免暂停菜单内门自行解锁。
- 验证：完整 Roslyn 编译 `exit 0`；最终 Prefab 静态引用扫描 172 个定义、0 重复、0 缺失。PlayMode 视觉、实际物理、暂停时序和移动端仍为 `uncertain`。
- 只回滚本轮时，移除 `StunnedLocked/Unlocking/DoorUnlocked` 二次移锁分支、门/ribbon 对齐、两个粒子实例链和箱子隐藏/恢复逻辑；不回滚共享粒子 Prefab、音效、门或 `DragableItem`。

## 2026-08-25：暂停边界与眩晕定位补正

- `Level101Ctrl` 增加本地暂停闸门；静态门禁覆盖 `SwitchGameStateEvent`、`GamePauseEvent` 与共享 `DragableItem` 结束回调的两种先后顺序。暂停时若锁正在拖动，代码会恢复锁原始姿态并回到 `idle`；箱子下落速度只在第一次暂停时缓存。实际 Unity 事件顺序仍为 `uncertain`。
- `PrepareInitialState()`、`OnEnable()` 和暂停恢复统一走 `RefreshInputAvailability()`；`IsGameplayInputOpen()` 使用 `Singleton<PlayCtrl>.IsInitialized` 防止检查状态时懒创建未初始化的 PlayCtrl。
- 箱子命中后立即停止刚体模拟，避免箱子继续挡在门口；Reset、Replay、退出清理仍会恢复初始 Dynamic/Simulated 状态。
- `StunnedCirclingStars` 不再只放在门根中心，而是以门 Collider 顶部加 `0.35` 世界单位的序列化偏移定位到门头。该偏移、粒子实际尺寸和最终遮挡仍需 PlayMode 视觉标定，当前标记为 `uncertain`。
- 箱子撞击门禁不再依赖 `Collision2D.relativeVelocity.y` 的符号：代码记录释放高度，以向下位移、箱子仍在门顶上方、门顶接触点、接触法线竖直分量和绝对撞击速度共同判定，降低侧撞与回调方向差异造成的误判。
- 该阶段 Unity 6000.0.59f2 Bee 编译链和 Level101 PrefabImporter 已通过，正式 LevelData/LevelSort、Luban JSON 与 Guru 默认排序已落盘；后续最终 Prefab 修改仍需重新导入，PlayMode、移动端和线上 RemoteConfig 发布状态仍未完成验证。

## 2026-08-25：眩晕特效渲染隔离补正

- `StunnedCirclingStars` 运行时实例的父节点从 `doorRoot` 改为 `Level_101` 根，避免 `doorRoot` 的 `SortingGroup=100` 覆盖粒子 Renderer 自身 order。当前四个 Renderer 统一写入 `SceneLayer`，基准 order 至少为 `110`，在当前 Prefab 上最终保留为 `110/111`。
- 项目既有 `star_AB` 与包内资源 GUID 相同，但其 `_MainTex` 已指向 `Assets/Game/UI/Postcard/Sprites/UI_common_icon_xing2_01.png`。Level101 Prefab 显式绑定包内 `star.png`，控制器只对运行时实例中名称为 `star_AB` 的 Renderer 设置 MaterialPropertyBlock；共享材质和共享特效 Prefab 保持不变。既有 `glow1_ADD` 的 additive 渲染语义可直接复用。
- 最新完整 Bee/Roslyn 编译为 `exit 0`，输出 104 条工程既有 warning，Level101 无诊断。静态排序和资源闭包不能证明最终画面，星星尺寸、门头位置、前后遮挡及移动端表现仍需 PlayMode/真机目测。

## 2026-08-25：编辑器生命周期边界修正

- `Level101Ctrl.Awake()` 在编辑器阶段只解析引用并捕获初始姿态；`ResolveStunnedEffectInstance()` 与 `PrepareInitialState()` 仅在 `Application.isPlaying` 为真时执行，避免 Prefab/场景编辑阶段实例化眩晕特效或触发运行时状态初始化。
- 本次只改变非运行时生命周期边界，不改变 PlayMode 状态机、输入、碰撞、门 Trigger 或清理流程。Roslyn 完整编译结果为 `CSC_EXIT=0`；该阶段 Level101 Prefab 本地 fileID 扫描为 166 个定义、0 重复、0 缺失引用。
- Unity PlayMode、Android/iOS 横屏触控和特效最终画面仍未在本环境执行，继续标记为 `uncertain`。

### 生命周期清理补充

- `OnDisable()` 与 `OnDestroy()` 使用非序列化的 `runtimeInitialized` 标记区分编辑器对象和已运行实例：编辑器 Prefab 禁用/销毁不会调用拖拽开关、表现恢复或运行时特效销毁；退出 PlayMode 保留已初始化实例的清理分支，实际回调顺序和清理结果仍为 `uncertain`。

## 2026-08-25：锁与箱子同时无法拖拽修复

### 问题与证据

- 用户进入 Level101 后反馈锁和箱子均无法拖动。旧运行日志在 `Level101Ctrl.Awake()` 中记录了
  `MissingReferenceException`：序列化的 `stunnedEffectPrefab` 已被资源生命周期释放，异常从
  `ResolveStunnedEffectInstance()` 冒出，随后关卡执行禁用清理。
- 后续旧程序集的 PlayMode 日志还连续记录过 `StunnedCirclingStars` 初始化 `InvalidCastException`。这些堆栈行号不对应最终源码，不能证明最终版本仍失败，但能证明仅捕获异常并把星星降级为空并不满足完整视觉要求。
- 当时 `Awake()` 没有继续执行 `PrepareInitialState()`；因此两个局部拖拽组件没有统一获得初始可用状态。
  两个对象同时失效的直接证据指向控制器初始化中断，而不是单个 Collider 的点击范围。

### 修复行为

- `Awake()` 仍先解析门、锁、箱子引用并捕获初始姿态；PlayMode 中把眩晕星星解析隔离在
  `try/catch` 内。星星资源无效时，`CleanupFailedStunnedEffectInitialization()` 只清理本关拥有的
  失败实例并输出 warning，随后仍执行 `PrepareInitialState()`。
- 初始状态因此会继续恢复门、锁和箱子，并通过 `RefreshInputAvailability()` 开放锁与箱子的拖拽。
  星星缺失会造成砸晕视觉不完整，但不再阻断本关状态机和输入。
- 共享 `Assets/Game/Scripts/Level/MainLevel/Level_7/DragableItem.cs` 没有因本问题改变；锁和箱子继续使用
  原有输入命中与相机链，避免把 Level101 的资源异常扩散为所有拖拽物的共享行为变更。

### 验证与回滚

- Unity 6000.0.59f2 当前 Bee 响应文件的完整 Roslyn 编译结果为 `CSC_EXIT=0`，Level101 无编译诊断。
- 修复后的最新 PlayMode 尚未重新进入 Level101；锁/箱子可拖、锁的渐隐与 `lock -> idle`、误放重试、
  箱子砸门、眩晕表现和标准门 Trigger 通关均为 `uncertain`。Android/iOS 横屏和线上 RemoteConfig 同样未验证。
- 只回滚本修复时，移除 `Awake()` 对星星初始化的异常隔离以及
  `CleanupFailedStunnedEffectInitialization()`，即可恢复此前“特效初始化异常会中断本关初始化”的行为；
  不需要改动 Prefab、配置、共享 `DragableItem` 或通关链。

## 2026-08-25：眩晕星星内嵌实例修正

- 为避免 YooAsset 关卡实例中的外部 Prefab 引用再次出现 `MissingReferenceException` 或 `InvalidCastException`，`Level_101.prefab` 现内嵌一个初态失活的 `StunnedCirclingStars` Prefab 实例，并把根 GameObject 直接绑定到 `Level101Ctrl.stunnedEffect`。
- `ResolveStunnedEffectInstance()` 的正常路径会识别该对象属于有效 Scene，直接配置 Renderer、局部星图和排序后保持失活；砸中时只定位到门头并激活。控制器仍保留 `stunnedEffectPrefab` 兼容字段，但 Level101 当前 Prefab 已把该外部引用清空，不再经过已知失败的运行时模板转换路径。
- 最终 Prefab 静态扫描为 172 个定义、172 个唯一 fileID、0 重复、0 缺失本地引用。最终 PrefabImporter、无异常实例化、星星可见性/尺寸/遮挡及完整 PlayMode 流程仍为 `uncertain`。
- 只回滚本修正时，删除 Level101 内的失活星星嵌套实例，把 `stunnedEffect` 恢复为空；不要删除或改写共享 `StunnedCirclingStars.prefab`、材质或贴图。

## 2026-08-25：角色、箱子、门与锁的局部碰撞规则

- 角色根 Collider 与运行时可能启用的 `pushCollider` 均位于 Actor Layer；箱子位于 Stuff Layer，门位于 OpenDoor Layer。项目 `Physics2DSettings` 的 Layer Collision Matrix 当前全开，三者的 Collider 均为实体，因此角色与箱子、箱子与门继续使用 Unity 2D 的正常实体碰撞。
- 锁 Collider 保持 Trigger，不承担实体阻挡。Level101 运行时只在本关门 Collider 和锁 Collider 的 `excludeLayers` 中加入 Actor Layer，不修改全局 Layer Collision Matrix、共享 Actor Prefab、箱子 Prefab或门 Prefab。
- `LockedIdle / Relocking / BoxDragging / BoxFalling / StunnedLocked / Unlocking` 中，门保持非 Trigger，但排除 Actor：箱子仍可砸中门，角色不会被门或锁碰撞。
- 进入 `DoorUnlocked` 时，控制器先临时关闭门 Collider，移除门对 Actor Layer 的排除，再把门切为 Trigger 并重新启用。这样角色仍不受实体阻挡，同时既有 `OnTrigger2DHandle -> BaseLevelCtrl.OnActorTrigger2D` 能建立新的接触并完成通关。
- `PrepareInitialState()`、`OnEnable()`、砸晕切换和最终解锁会重申当前状态的局部规则；Reset/Replay 由新 Prefab 实例重新执行初始化，旧实例只停止输入、Tween、协程与临时特效。
- Unity 6000.0.59f2 当前 Bee 响应文件的完整 Roslyn 编译为 `0 error / 104 warning`，Level101 无诊断。当前结论另有静态层级、Collider 参数和全局矩阵证据；Unity PlayMode 中的角色推箱、箱子砸门、角色穿过锁定门/锁，以及最终 Trigger 通关仍为 `uncertain`。

## 2026-08-26：砸晕后开启 door_0/effect

- 需求：有效箱子命中使 `body` 隐藏后，必须开启 `door_0/effect`。
- 原行为：`EnterStunnedState()` 同时关闭 `body` 与 `effect`，只显示正常门、锁和独立星星特效。
- 新行为：同一砸晕分支先关闭 `body`，对齐 `effect/door/ribbon` 后开启 `effect`，并继续显示正常门、锁和独立星星特效；新实例初始化时关闭 `effect`，旧实例 teardown 不再回写门表现。

## 2026-08-26：Reset/Replay 销毁边界清理

- 主线普通 Reset、失败重开和胜利 Replay 都会销毁旧 Level 并重新实例化 Prefab；Level101 不再对旧实例调用完整运行态复位。
- `GameResetEvent`、`GameReplayEvent` 和 `OnDisable` 统一进入 `BeginTeardown()`：停止本地输入、Tween、协程和临时粒子，禁用拖拽并解绑事件，但不恢复门、锁、箱子或碰撞初态。
- 新关卡初态仍只由 `Awake -> PrepareInitialState()` 建立，砸晕后 `effect` 的玩家可见行为不变。
- 已通过 `Assembly-CSharp.csproj` 编译（0 error / 120 warning）；Reset/Replay 黑幕窗口、连续重开和移动端横屏仍需 PlayMode/真机验证。
- 回滚时恢复 Reset/Replay 调用旧 `ResetRuntimeState()` 及 `OnDisable` 的表现复原；该回滚会重新引入旧实例销毁前短暂复位风险。
- 纠察清理：共享 `door_0.prefab` 证据表明 `effect` 是循环播放 `effects`、排序为 `80` 的标准门视觉，且与 `door` 使用相同初始局部姿态；因此把失真的字段名 `stunnedDoorVisual` 更正为 `doorEffectVisual`，使用 `FormerlySerializedAs` 保持现有 Prefab 绑定兼容，并把 `effect` 纳入正常门的同一对齐补偿。
- 影响范围仅为 `Level101Ctrl.cs` 的本关表现切换及本地中文记录；不修改用户已有的 `Level_101.prefab` 改动、共享 `door_0.prefab`、碰撞、门 Trigger、事件、存档、配置或资源加载。
- 已执行静态写入路径扫描、`git diff --check` 和 `Assembly-CSharp.csproj` 编译（`0 error / 120 warning`，warning 均为工程既有项）；Unity PlayMode、`effect` 与正常门/锁/星星的叠加观感以及 Android/iOS 横屏表现仍为 `uncertain`。
- 回滚时将砸晕分支的 `doorEffectVisual?.SetActive(true)` 恢复为 `false`，并移除 `effect` 对齐调用、恢复对应说明；无需回滚 Prefab 或共享资源。
