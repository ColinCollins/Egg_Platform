# Level101「眩晕」风险评估

更新时间：2026-08-25

## 原有行为基线

Level101 是新增主线关卡。当前基线可确认的是：关卡 Prefab 使用标准 `BaseLevelCtrl`，门来自 `door_0.prefab`，门的既有 Trigger 事件最终调用 `BaseLevelCtrl.OnActorTrigger2D`；本关局部玩法控制器和拖拽适配器属于新增实现。Level096、Level128、共享 `DragableItem`、`BaseLevelCtrl` 和门 Prefab 不在本关改动范围内。

## 影响文件、资源与系统

| 范围 | 文件/资源 | 作用 |
| --- | --- | --- |
| 局部运行时 | `Level101Ctrl.cs`、`Level101LockDragItem.cs`、`Level101BoxDragItem.cs` | 状态机、输入转发、锁动画、箱子物理、暂停与清理 |
| 场景 | `Assets/Game/Prefabs/Level/Level_101.prefab` | 控制器、门/锁/箱子引用和初始显隐/碰撞覆盖 |
| 既有门 | `Assets/Game/Prefabs/LevelItems/door_0.prefab` | `effect`、`door`、`ribbon`、门 Collider、Trigger 成功链 |
| 关卡资源 | `StunnedCirclingStars.prefab`、`l101/lock.png` | 眩晕特效和可拖动锁图片 |
| 既有反馈资源（只引用） | `Knock_Explosion.prefab`、`BoxBroken.prefab`、`crashBroke` | 纯撞击粒子、无残骸碎箱粒子与音效 |
| Spine | `sp_door-move_001` 及门体 Spine 数据 | `idle`/`lock`/`door_idle` 表现 |
| 配置 | `LevelData.xlsx`、`LevelSort.xlsx`、生成 JSON、Guru 默认排序 | 正常主线入口（正式源表与本地生成物已回读） |
| 共享系统（只复用） | `BaseLevelCtrl`、`DragableItem`、`ActorSpineCtrl`、DOTween、Game 状态事件 | 标准通关、拖拽、动画和生命周期 |

## 风险矩阵

| 等级 | 风险 | 处理/证据 | 当前状态 |
| --- | --- | --- | --- |
| P1 | `Level101Ctrl` 或两个局部拖拽组件的引用/生命周期不生效，玩法不可达 | Prefab 已出现三个脚本 GUID 并显式绑定门/锁/箱子/特效引用；此前版本 PrefabImporter 已通过，最终版本仍需重新导入和 PlayMode 确认 | 静态接线已完成，最终导入与运行效果待验证 |
| P1 | `door_0` 子表现的 inactive override 或层级解析错误，导致初始门表现与控制器切换不一致 | 源 Prefab 映射确认根 `door_0`（837609）为激活，子 `door`（610247）和 `effect`（645586）初态隐藏；此前版本 PrefabImporter 已通过 | 静态初态符合设计，最终导入与运行时待验证 |
| P1 | 控制器碰撞回调挂在无 Collider 的关卡根，箱子撞门时不触发 | 控制器保留箱子局部 relay，Prefab 已绑定 `Level101BoxDragItem` 与箱子 Rigidbody；必须通过 PlayMode 确认 `HandleBoxCollision` 调用链 | 静态接线已完成，运行时 `uncertain` |
| P1 | 用户称谓 `locked` 与资源真实动画 `lock` 不一致 | 动画名序列化，默认值使用 `lock`；不改共享 Spine 资源 | 静态已确认，PlayMode 待验收 |
| P1 | TrackEntry、兜底协程或 Tween 在暂停/Reset/退出后回调旧实例 | 流程版本号、Complete 解绑、Coroutine 停止、Tween Kill 和事件重置均已写入局部控制器 | 静态已见，Unity 时序未验证 |
| P1 | 暂停触发共享 `DragableItem.EndDrag`，误把暂停当作释放 | `EndLockDrag`/`EndBoxDrag` 检查 `IsGameplayInputOpen`；箱子下落速度在暂停时缓存 | 静态已见，多事件顺序待验证 |
| P1 | 侧面擦碰、地面接触或低速接触误触发眩晕 | 同时校验箱子与门 Collider、释放后向下位移、箱子位于门顶、绝对撞击速度、门顶接触点与接触法线竖直分量 | 阈值/接触法线未在 PlayMode 标定，`uncertain` |
| P1 | 眩晕表现绑定错误，body/effect/door/锁显隐或前后层级不符 | 目标为 `body=false`、`effect=true`、`door=true`、锁可见；锁 order `101` 高于门 `100` | 代码目标已更新，视觉与叠加层级未验证 |
| P1 | 外部星星 Prefab 引用失效或类型转换失败，导致初始化异常或砸晕后无星星 | Level101 现内嵌失活场景实例并绑定 `stunnedEffect`；PlayMode `Awake()` 优先复用场景实例，外部模板只作旧 Prefab 兼容兜底 | 静态接线已修正；最终导入、无异常和星星可见性待 Unity 验证 |
| P1 | `doorRoot` 的 `SortingGroup=100` 把眩晕粒子压在门后 | 运行时实例改挂 `Level_101` 根；四个 Renderer 设置 `SceneLayer`，当前最终 order 为 `110/111` 并保留内部 `0/1` 偏移 | 静态排序已加固；尺寸、位置和遮挡仍待 PlayMode |
| P1 | 同 GUID 的既有 `star_AB` 材质使用明信片星图，导致眩晕星星图案错误 | Prefab 绑定包内 `star.png`；仅在 Level101 运行时实例上以 MaterialPropertyBlock 覆盖 `_MainTex`，不改共享材质 | 静态绑定已确认；实际粒子画面待 PlayMode |
| P1 | 砸中后门过早切 Trigger，玩家不用再移锁即通关 | `StunnedLocked` 保持非 Trigger、继续与箱子实体碰撞并排除 Actor；只有 `Unlocking` 渐隐完成才进入 `DoorUnlocked` 并切 Trigger | 静态状态门禁已见，PlayMode 未验证 |
| P1 | 暂停菜单中最终锁仍继续渐隐并打开门 | 项目暂停不修改 `Time.timeScale`；本关在 `Unlocking` 显式 Pause/Play Tween | 代码已补，暂停时序 `uncertain` |
| P1 | `lock -> idle` 仍有动画融合，或门/ribbon 切换时跳位 | idle 和 door_idle TrackEntry 设 `MixDuration=0`；门与 ribbon 共用位置/缩放补偿 | 编译通过，像素级观感 `uncertain` |
| P1 | 撞击或碎箱粒子未绑定、文字闪现、排序错误或留下对象 | Prefab 绑定两个根 ParticleSystem；实例先禁用 `Knock 1`，使用 SceneLayer `200/201+`，按完整寿命销毁 | 静态接线/API 已核对，最终效果 `uncertain` |
| P1 | 箱子砸碎后仍有残骸/Collider，或 Reset 无法恢复 | 命中后停模拟并隐藏原箱子，不使用 Level007 `Box_2`；Reset 恢复捕获的 active/姿态/Dynamic/Simulated | 代码路径已见，Unity 回调顺序 `uncertain` |
| P1 | 重力/质量/连续碰撞参数在移动端帧率下导致漏撞或穿透 | 箱子使用 Dynamic、Continuous、非 Trigger Collider；碰撞门禁仍需低帧率实测 | 仅静态参数证据，移动端 `uncertain` |
| P1 | 配置表、本地生成物或线上远端排序不同步，正常入口或解锁顺序错误 | 正式 LevelData/LevelSort 已落盘，`108 -> 10101` 与相邻 `107 -> 10128` 已回读；两份 Luban JSON 和 Guru 默认排序已同步 | 本地源表/生成物已完成；线上 RemoteConfig 发布状态 `uncertain` |
| P2 | 锁渐隐/恢复位置与动画最后一帧不一致，玩家看到跳变 | 首次捕获 lock 的 local position/rotation/scale/color，恢复不使用 `LockPoint` | 静态设计已确认，视觉待验收 |
| P2 | 误放箱子后停在地面无法再次操作 | 停稳超过恢复延迟后回到 `LockedIdle` 并重新开放拖拽 | 静态设计已确认，物理实测待验收 |
| P2 | Android/iOS 横屏、触控、多指或异形屏导致拖拽偏移 | 复用现有 `DragableItem` 相机和输入链，需真机横屏手工矩阵 | 未验证 |
| P2 | 星星特效初始化或粒子开销影响 60 fps | PlayMode `Awake()` 最多解析/实例化一次并缓存，当前 Prefab 正常路径直接复用失活场景实例；进入 `StunnedLocked` 时只定位并激活 | 代码设计已见，移动端性能未测 |

## 事实、结论与缺失证据

### facts

- `Level101Ctrl` 定义了 `LockedIdle`、`Relocking`、`BoxDragging`、`BoxFalling`、`StunnedLocked`、`Unlocking`、`DoorUnlocked` 七个局部状态。
- `door_0.prefab` 的层级包含 `effect`、`door`、`ribbon`、`LockPoint`；其 Collider 和 Trigger 事件属于既有门资源。
- 当前移动 Spine 文件列出的动画名包含 `idle`、`lock`，没有 `locked`；旧门 Spine 使用 `door_idle`。
- 眩晕资源 `StunnedCirclingStars.prefab` 已导入且与 unitypackage 内 Prefab 的 SHA256 一致，锁图片资源位于 `Assets/Game/ArtSrc/Sprites/l101/lock.png`。包内 10 项依赖均存在，8 项内容哈希一致；相同 GUID 但内容不同的既有 `star_AB` 与 `glow1_ADD` 未被覆盖。
- `star_AB` 当前共享材质指向明信片星图；Level101 Prefab 另行绑定包内 `star.png`，控制器通过运行时 MaterialPropertyBlock 局部替换。既有 `glow1_ADD` 保留，其 additive 语义与包内资源等价。
- `Level_101.prefab` 已给控制器序列化门 Collider、body、锁、箱子 Rigidbody、`door`/`effect`、失活星星实例、`Knock_Explosion`、`BoxBroken`、门对齐值与粒子排序；锁 order 为 `101`。当前静态扫描为 172 个定义、0 重复 fileID、0 缺失本地引用。
- 命中反馈复用既有 `Knock_Explosion` 和 Level007 `BoxBroken`/`crashBroke`，但不复用 Level007 的永久 `Box_2` 残骸，也不修改共享资源本体。
- LevelData 正式源表包含 `10101 / Level_101 / 眩晕` 且位于 `10999` 前；LevelSort 正式源表 `A113:D113=[null,108,10101,"眩晕"]`，相邻 `A112:D112=[null,107,10128,"当前时间密码"]`。两份 Luban JSON 与 Guru 默认排序已同步，工作簿目标区域渲染和公式错误扫描通过。
- Unity 6000.0.59f2 Bee 的 Assembly-CSharp Csc、ILPostProcess、CopyFiles 均为 `exitcode 0`，编译响应文件包含三个 Level101 脚本。

### direct conclusions

- 玩法应保持在 Level101 私有脚本和 Prefab override 内，不需要修改共享门、Actor、拖拽或成功链。
- 初始门和 `StunnedLocked/Unlocking` 必须都是非 Trigger Collider，以便箱子碰门；这些状态同时排除 Actor Layer。只有最终锁渐隐完成进入 `DoorUnlocked` 后才移除 Actor 排除并切换 Trigger。
- 成功通路可以复用既有 `BaseLevelCtrl`，本关不应复制门触发、存档或 analytics 逻辑。

### hypotheses / pending validation

- 箱子 relay 是否能在当前 Prefab 层级稳定收到门碰撞，虽然已有 `Level101BoxDragItem -> Level101Ctrl` 和 Rigidbody 序列化引用，但仍需 Unity PlayMode 复现确认。
- `StunnedCirclingStars` 的实例父节点、`SceneLayer 110/111` 和局部贴图覆盖已有静态证据，但实际尺寸、门头位置、前后遮挡及与正常门/锁的观感尚未由 PlayMode 证明。
- 正常门/ribbon 的位置缩放补偿、`MixDuration=0` 和锁 order 有静态证据，但是否完全消除切换瞬移、融合和尺寸跳变仍需目测。
- `minimumImpactDownwardSpeed=1` 与 `topImpactTolerance=0.7` 是否适合当前箱子质量、门高度和横屏帧率，需在多次正解/误触中标定。

### missing evidence

- Unity PlayMode 中的 Missing Script/运行时空引用、实际碰撞与完整玩法时序；此前版本 PrefabImporter 和 Bee 编译链已通过，不能替代最终 Prefab 的重新导入与此项验证。
- 锁拖动渐隐、重锁动画完成、暂停中断和恢复、连续 Reset/Replay/退出清理。
- 箱子从门顶自由落体的正解、侧碰/低速误触、未命中重试、纯撞击/碎箱粒子和无残骸箱子隐藏。
- 砸晕后只开放锁；角色可无碰撞穿过门/锁但不能触发成功，锁渐隐完成后才切 Trigger，以及该流程的暂停/恢复。
- Android/iOS 横屏触控、异形屏安全区、后台/恢复和代表性低端设备性能。
- 线上 RemoteConfig 是否已经发布与本地 Guru 默认排序相同的 `...,10128,10101`；本地源表、Luban JSON 和 Guru 默认值已经同步。

## 发布前手工验收矩阵

1. 进入 Level101：门 body 与锁可见；角色可无碰撞穿过门和锁但不会触发成功，箱子能与角色、门实体碰撞且可拖拽；无 Missing Script 或空引用。
2. 任意方向拖走锁并释放：锁在释放位置渐隐，门播放 `lock`，完成后回到 `idle`，锁无跳变地回到原位；重复尝试不残留 Tween。
3. 锁动画期间暂停、恢复、Reset、Replay、退出：不重复播放、不残留回调，重新进入后锁可拖。
4. 箱子从侧面或低处碰门：不进入眩晕；误放停稳后可再次举起。
5. 箱子位于门顶释放并向下命中：只触发一次；有纯撞击和碎箱粒子、无 `Knock` 文字、无箱子残骸；body 隐藏，effect 与正常门+锁显示且无瞬移，星星在头顶。
6. `StunnedLocked`：锁可拖、箱子不可拖；角色仍可无碰撞穿过门/锁但不会触发成功，最终锁在释放位置渐隐，完成后门 Trigger 才可结算。
7. 最终锁渐隐期间暂停/恢复：锁不在暂停菜单内继续消失，门不提前变 Trigger。
8. 最终解锁后走进门：标准成功链只结算一次，门和 `ribbon` 无错位；退出、Replay 和 Reset 不留临时粒子或隐藏箱子状态。
9. Android/iOS 横屏与不同安全区：锁、箱子拖拽坐标、星星锚点、门表现和进门范围均可操作且无明显遮挡。

## 验证证据与回滚

当前已完成代码/资源/动画名静态检查、特效依赖核对、正式配置与生成物回读、当前 Bee 响应文件的完整 Roslyn 编译（`exit 0`）和 Prefab 引用闭包扫描。本轮修改后的 PrefabImporter、PlayMode、线上 RemoteConfig 和移动端仍未完成，所有未覆盖项必须保持 `uncertain`。

回滚时优先移除 Level101 的局部脚本、Prefab 接线、二次移锁状态、门/ribbon 对齐、两个临时粒子链和箱子隐藏/恢复逻辑；若配置已生成，再从源表、运行时 JSON 和 Guru 默认排序中删除 `10101/Level_101`。不要回滚共享 `Knock_Explosion`、`BoxBroken`、`crashBroke`、`StunnedCirclingStars`、`door_0.prefab`、`BaseLevelCtrl` 或 `DragableItem`。

## 2026-08-25 增量：二次移锁、无融合切换与无残骸反馈

- 本轮变更集中在 `Level101Ctrl.cs` 与 `Level_101.prefab`，共享门、共享拖拽、Level007 和粒子/音效资源本体均未修改。
- 主要风险从“砸中不开门”扩展为两阶段时序：砸中后必须仍被门挡住，只有锁渐隐完成才 Trigger；暂停、Reset 和旧 Tween 回调不得绕过该门禁。
- 静态证据包括最新完整 Roslyn 编译 `exit 0`、粒子 GUID/fileID 引用回读，以及最终 Prefab 172 个定义、0 重复、0 缺失引用。
- 剩余风险是 PlayMode 中的无融合观感、门/锁/ribbon 对齐、粒子强度与层级、箱子无残骸、二次移锁通关、暂停时序和移动端输入，均为 `uncertain`。

## 2026-08-25 增量：暂停与特效定位修正

- 本轮仅修改 Level101 局部控制器和 Prefab 序列化字段：显式本地暂停门禁、暂停拖拽姿态恢复、命中后停用箱子刚体模拟、门顶特效偏移 `0.35`、实例父节点/排序和局部星图覆盖；未改共享拖拽、门、通关脚本、特效 Prefab 或共享材质。
- 该阶段完整 Bee/Roslyn 编译为 `exit 0`，输出 104 条工程既有 warning，Level101 无诊断，且当时的 Level101 PrefabImporter 已完成；后续最终 Prefab 修改仍需重新导入。这些证据不能替代实际碰撞、粒子可见性、Trigger 通关或移动端验证。
- 特效实例已绕开门根 `SortingGroup`，四个 Renderer 运行时使用 `SceneLayer 110/111`；`star_AB` 通过 MaterialPropertyBlock 使用包内 `star.png`。共享 Prefab/材质不变，实际尺寸、位置、遮挡和贴图观感仍需 PlayMode 确认。
- 正式 `LevelSort.xlsx` 已落盘为 `108 -> 10101`，两份 Luban JSON 与 Guru 默认排序已同步；剩余配置风险仅保留线上 RemoteConfig 是否发布同一顺序。

## 2026-08-25 增量：编辑器生命周期边界修正

- `Level101Ctrl.Awake()` 已增加 `Application.isPlaying` 外层闸门：编辑器加载 Prefab 时仅解析引用和捕获初始姿态，不会实例化 `StunnedCirclingStars` 或执行运行时初始状态；PlayMode 仍走原有 `ResolveStunnedEffectInstance()` 与 `PrepareInitialState()`。
- 验证证据：Unity Bee/Roslyn 完整编译 `CSC_EXIT=0`；该阶段 Level101 Prefab 静态引用扫描为 166 个本地定义、0 个重复 fileID、0 个缺失引用。
- 剩余风险：尚未在 Unity PlayMode 观察 Prefab 编辑/运行切换、锁拖拽、箱子碰撞、眩晕显示和通关；Android/iOS 横屏触控仍未验证，均保持 `uncertain`。

- 生命周期补充：`OnDisable()`/`OnDestroy()` 以 `runtimeInitialized` 区分编辑器对象与运行时实例，降低 Prefab 编辑阶段副作用；退出 PlayMode 的实际回调顺序仍需 Unity Editor 复核。

## 2026-08-25 增量：眩晕特效异常不得阻断拖拽初始化

- 用户反馈的“锁与箱子同时不能拖”已有旧运行日志证据：失效的 `stunnedEffectPrefab` 在
  `ResolveStunnedEffectInstance()` 抛出 `MissingReferenceException`，`Level101Ctrl.Awake()` 因此未执行
  `PrepareInitialState()`，随后禁用清理又关闭两个拖拽组件。这是两个输入同时失效的直接共同原因。
- `Awake()` 现将星星初始化隔离为可降级步骤；失败时只清理本关拥有的运行时特效引用并记录 warning，
  无论星星是否可用都继续执行状态机与输入初始化。`SetStunnedEffectActive()` 仍保留 fake-null 清理边界。
- 本次没有修改共享 `DragableItem`、门 Prefab、Actor、成功链、配置、存档、事件或 analytics。锁、箱子的
  Collider 与 Prefab 接线已有静态证据，但不能替代修复后的 PlayMode 输入验证。
- 完整 Roslyn 编译为 `CSC_EXIT=0`；最新 PlayMode、Android/iOS 和线上 RemoteConfig 均为 `uncertain`。
  发布前必须确认无新的 `MissingReferenceException`，并逐项验证锁可拖、箱子可拖、重锁恢复、误放重试、
  正解眩晕与门 Trigger 通关。
- 独立剩余风险：Domain Reload 与 Scene Reload 同时关闭时，非序列化 `runtimeInitialized` 是否跨 PlayMode
  保留尚未实测，继续标记 `uncertain`，不能用本次输入修复结论覆盖该生命周期风险。
- 只回滚本次输入修复时，移除 `Awake()` 中的特效异常隔离与失败清理 helper；共享拖拽和 Prefab 无需回滚。

## 2026-08-25 增量：眩晕星星内嵌实例修正

- 旧 PlayMode 日志除 `MissingReferenceException` 外，还连续出现过星星初始化 `InvalidCastException`；仅让 `Awake()` 捕获异常虽然恢复了拖拽，却会丢失用户要求的眩晕视觉。
- `Level_101.prefab` 现保存一个初态失活的 `StunnedCirclingStars` 嵌套实例，并直接绑定 `Level101Ctrl.stunnedEffect`；当前 Prefab 的 `stunnedEffectPrefab` 外部引用已清空，正常路径不再依赖运行时模板转换。共享特效 Prefab、材质和贴图不改。
- 最终 Prefab 静态扫描为 172 个定义、172 个唯一 fileID、0 重复、0 缺失本地引用。最终 PrefabImporter、PlayMode 无异常实例化、星星可见性/层级/尺寸与移动端性能仍为 `uncertain`。
- 回滚仅删除 Level101 内嵌实例并恢复 `stunnedEffect` 空引用；不得回滚共享 `StunnedCirclingStars.prefab` 或其依赖。

## 2026-08-25 增量：局部碰撞过滤

- 静态事实：Actor 根 PolygonCollider 与动态 `pushCollider` 位于 Actor Layer 11；箱子为 Stuff Layer 8 的 Dynamic Rigidbody2D + 非 Trigger BoxCollider；门为 OpenDoor Layer 3 的非 Trigger BoxCollider；锁为 Default Layer 0 的 Trigger BoxCollider。当前全局 2D Layer Collision Matrix 全开。
- 因此角色与箱子、箱子与门无需修改共享资源即可正常实体碰撞。本轮只给 Level101 门和锁的 Collider 局部追加 Actor Layer `excludeLayers`，避免角色与锁定门或锁产生碰撞/Trigger 接触；箱子所在 Stuff Layer不在排除范围内。
- Actor 的 `pushCollider` 会由共享 `ActorCtrl` 动态激活。采用 Collider Layer Override 而不是一次性的 `Physics2D.IgnoreCollision` pair，避免失活 Collider 重新启用后丢失忽略状态。
- 进入 `DoorUnlocked` 时必须移除门上的 Actor 排除，否则标准门 Trigger 永远收不到角色。实现会先禁用门 Collider、移除排除、切为 Trigger，再重新启用，以便角色已与门范围重叠时重新建立接触。
- Reset/Replay 由新 Prefab 实例恢复锁门态并重新应用排除；旧实例只停止输入与异步流程。未修改 `Physics2DSettings`、共享 Actor/箱子/门 Prefab 或 `BaseLevelCtrl`。
- 完整 Roslyn 编译为 `0 error / 104 warning`，Level101 无诊断。检查时 `Library/ScriptAssemblies/Assembly-CSharp.dll` 仍早于最新源码，说明已打开的 Unity Editor 尚未重载本轮版本；剩余 P1 风险包括 Layer Override 的运行时接触刷新、角色推箱手感、箱子门顶碰撞回调和最终 Trigger 建立，均保持 `uncertain`。
- 回滚本增量只需移除 `Level101Ctrl.ApplyCollisionRulesForCurrentState()` 及其局部 helper，并恢复三个门 Collider 状态写入点；不要修改全局碰撞矩阵。

## 2026-08-26 增量：砸晕后开启 door_0/effect

- 原始行为基线：`body=false`、`effect=false`、`door=true`；本次有意调整为 `body=false`、`effect=true`、`door=true`。
- 受影响文件为 `Level101Ctrl.cs`、本功能说明、本风险评估和 `DevelopMD/已有功能设计与使用记录.md`；不修改已有 `Level_101.prefab` 或共享门资源。
- P1 视觉风险：`effect` 与正常门、锁、星星可能出现遮挡或重叠。静态证据确认 `effect` 循环播放 `effects`、排序为 `80`，并已与 `door/ribbon` 使用同一位置和缩放补偿；最终画面仍必须在 PlayMode 验证。
- 状态和清理风险保持受控：`effect` 仅在有效命中进入 `StunnedLocked` 时开启，新实例初始化显式关闭；旧实例 teardown 不再为即将销毁的门回写表现。门碰撞、Trigger 和通关状态机没有改变。

## 2026-08-26 增量：旧实例不再完整复位

- 原始基线：Reset/Replay 回调在旧实例上恢复门、锁、箱子、碰撞和输入状态，`OnDisable` 再重复恢复一次。
- 新逻辑只取消输入、Tween、协程、拖拽和临时特效；初态由新 Prefab 实例负责，避免黑幕未完全覆盖时发生门/箱子跳变或旧输入复活。
- 风险：若外部系统把 Level101 根节点临时 Disable 后期望原地续玩，重新启用时不会获得一次完整复位；当前主线证据为销毁重建，临时 Disable/Enable 场景仍标记 `uncertain`。
- 编译验证为 0 error；未执行 Unity PlayMode 的手动 Reset、失败重开、胜利 Replay 和连续快速重开。
- 回滚路径为恢复旧 `ResetRuntimeState()` 与 `RestorePresentationForCleanup()`，不涉及 Prefab、配置、存档或共享脚本。
- 验证包含显隐写入扫描、差异复核、`git diff --check` 和 `Assembly-CSharp.csproj` 编译（`0 error / 120 warning`，warning 均为工程既有项）；PlayMode、PrefabImporter 与 Android/iOS 横屏尚未覆盖，均标记为 `uncertain`。
- 回滚时把 `EnterStunnedState()` 中的 `doorEffectVisual?.SetActive(true)` 恢复为 `false`，移除 `effect` 对齐调用并同步恢复注释和文档；不涉及 Prefab、配置、存档、事件或共享资源。
