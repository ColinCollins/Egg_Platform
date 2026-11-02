# Level 078 射箭对决风险评估

## 原有行为基线

- `Level_078.prefab` 原根节点使用 `BaseLevelCtrl + KnockKnock`，面板为 `GamePlayPanel_000`。
- 射箭挡板 `Sp_l78_img_03` 原本只有 `Transform + SpriteRenderer`，计分板只有底图，单支场景箭只是展示素材。
- 共享 Actor、GamePlayPanel 与 Level 70/87 参考实现不包含 Level 078 的完整三箭规则。

## 影响文件与隔离边界

- 修改：`Assets/Game/Prefabs/Level/Level_078.prefab`。
- 新增：`Assets/Game/Scripts/Level/LevelItem/Level_78/` 下领域、运行时适配、测试与文档。
- 新增：`Assets/Game/UI/GamePanel/Resources/GamePlayPanel_026_78.prefab`。
- 未修改：共享 `Actor.prefab`、`ActorCtrl.cs`、`GamePlayPanel.cs`、`GamePlayPanel_000`、`GamePlayPanel_025_70`、Level 70/87 Prefab 与脚本、Spine JSON/atlas/texture。
- 未新增：DB/save、配置、全局事件定义、资源加载、analytics、广告、IAP、RemoteConfig、平台 SDK。

## 2026-07-20 挡板渲染层级修复评估

- 原有行为基线：`Sp_l78_img_03` 使用 `BaseLayer/0`，Box 根 `SortingGroup` 使用 `SceneLayer/200`；`box_bad` 由 Box 运行时克隆并继承相同排序。由于 `BaseLayer` 位于 `SceneLayer` 之后，挡板会覆盖两名角色。
- 修复范围：只修改 `Assets/Game/Prefabs/Level/Level_078.prefab` 中挡板 `SpriteRenderer`，设为 `SceneLayer/199`；不修改共享 Actor、`Level78Ctrl`、皮肤资源、材质、拖拽 Collider 或玩法状态机。
- P1（已修复并通过专项可视验证）：若挡板仍位于更高 Sorting Layer，Box 与 `box_bad` 在重叠区域会被遮挡。运行时断言已确认挡板 `199` 小于两名角色 `200`，修复后截图确认两名角色像素都覆盖挡板。
- 回归风险：同层排序后，挡板与 Level 078 其他 `SceneLayer` 对象的相对顺序由 order 决定；`199` 紧邻角色 `200`，避免把挡板降到普通背景，同时保留计分靶和计分板原有 `BaseLayer` 行为。
- 验证证据：`git diff --check` 对目标 Prefab 与临时探针通过；Unity batchmode、GUI Initial Refresh、真实 `Level_078` 加载和三方运行时排序断言通过；修复前后截图归档在 `docs/screenshot/20260720/`。
- 未验证缺口：Android/iOS 真机、所有 Box 皮肤、完整失败/重开/通关/退出流程与 60fps 尚未覆盖。排序变更不触碰逻辑和资源加载，但仍需发布前做横屏真机抽查。
- 回滚路径：恢复挡板为 `BaseLayer/0`；无需迁移配置或玩家数据，但会恢复已确认的角色遮挡缺陷。

## 2026-07-20 挡板近靶终点标定调整评估

- 原有行为基线：`Level78Ctrl.nearBoardLocalX` 与 `Level_078.prefab` 均使用 `4.65`，结合挡板原始 Y/Z `-2.89/0`，近靶最终本地坐标为 `(4.65, -2.89, 0)`。
- 变更范围：只把脚本默认值和 Prefab 序列化值同步改为 `2.65`，使近靶最终本地坐标变为 `(2.65, -2.89, 0)`；不修改远距原点、领域状态、计分、Tween 时长、输入或共享资源。
- P1：近靶点向远距方向回移 `2` 个本地单位后，Box 射箭站位与计分靶的视觉距离发生变化；必须在真实 `Level_078` PlayMode 确认挡板不与 Box、蓄力表或场景对象异常重叠，且近靶 30 分路径仍可完成。
- 验证证据：静态检查确认脚本与 Prefab 值一致、挡板原始本地位置仍为 `(-1.99, -2.89, 0)`、运行时 `DOMoveX` 只改变 X；`git diff --check` 通过。
- 未验证缺口：Unity PlayMode、失败/重开/通关/退出、Android/iOS 横屏触控、不同分辨率画面与 60fps 未执行；发布前需要人工抽查。
- 回滚路径：同步恢复脚本和 Prefab 的 `nearBoardLocalX = 4.65`；无需迁移存档、配置或资源。

## 2026-07-20 挡板射箭点左移解锁修复评估

- 原有行为基线：小恶魔阶段只限制 Box 向右越过射箭挡板，左移应始终可用；提前占位的 Box 在小恶魔归位后必须先离开射箭点，再重新进入取得射箭资格。
- 缺陷与根因：`BoardAdjustment` 阶段的站位判定会在 Box 位于 `shootingPointTolerance` 内时每帧对齐 X 并清零输入。该吸附发生在领域层拒绝提前占位资格之前，导致用于满足“先离开”的左移输入自身被取消。
- 修复范围：只在 `Level78Ctrl.UpdatePlayerBoundaryAndQualification()` 中让明确的左移输入绕过站位吸附；位置跨出容差后仍登记离开。新增 Editor-only 真实 Prefab 行为探针，不修改共享控制器、HUD、Prefab、碰撞层或领域状态机。
- P1（已修复并自动验证）：Box 在挡板处等待小恶魔完整流程后，必须在首个短时间窗内产生可见左移。旧代码专项探针为 `0.300s/0.000` 并失败；修复与重排后均为 `0.040s/0.102` 并通过。
- 回归风险：若左移绕过条件过宽，可能跳过重新进入资格；当前实现先按真实位置登记跨出容差，再仅在尚未跨出时检查负方向输入。既有 `ShootingQualification_RequiresFreshEntry_WhenBoxWasAlreadyAtPoint` 等 Level 078 EditMode 测试共 43/43 通过。
- 未验证缺口：自动探针通过现有公开移动事件驱动真实 Prefab，但未覆盖 Android/iOS 触摸 EventSystem、低帧率设备、完整通关/失败/重开/退出。发布前仍需在横屏真机执行提前占位、松开右移、左移离开和重新进入的人工路径。
- 回滚路径：删除负方向输入的提前返回并移除专项探针；无需迁移配置或玩家数据，但会恢复已复现的左移锁死。

## 风险项

- P1：Box 现在非循环播放 `draw_pull` 后以原动画 `1.0×` 速度循环 `draw_hold` 作为射箭待发状态，首个持弓周期完成前拒绝射击输入，有效点击直接切换 `draw_shoot`；必须在 Play Mode 确认两段衔接、蓄力感、循环接缝，且前两箭结束后重新准备时不会短暂闪回普通 idle。
- P0：完整 Unity 编译与 Test Runner 当前被仓库既有依赖缺口阻断。两次 Unity 6.0.59f2 batchmode 均出现 98 个去重后的既有错误，集中为缺失 `Guru`、`YooAsset`、`Cysharp` 类型；Level 078 路径错误为 0。依赖恢复后必须重新执行 EditMode、Prefab 加载和 Play Mode。
- P0：近靶 X、射箭点偏移/容差、蓄力表前向/纵向偏移及缩放、计分文字锚点、计分环落点和松弦比例属于视觉标定。蓄力表当前为前向 `1.6`、向上 `0.76`、根缩放 `0.09`；箭靶当前按真实中心、6 个固定环半径和 3 个箭序方位标定，箭矢沿抛物线并跟随切线转向。5 场景探针已确认箭尖落点和三箭可辨，仍需完整双方三箭人工验收。
- P1（已确认并修复，待复验）：运行时小恶魔通过玩家 Actor 创建。修复前只禁用 Collider，动态 Rigidbody2D 仍受重力影响，手测中小恶魔直接掉出场景。现在创建时清零速度/重力并关闭物理模拟，开屏停留在等待位，首次左右移动才单次启动进场；仍需确认修复版排序层、朝向、开场位置和第一箭视觉。
- P1（已修复并通过专项可视验证）：运行时小恶魔克隆后若不重绑定 `ShadowController` 的角色目标，可出现本体可见但脚下无影子。Level 078 现在在 `CreateDemon()` 中启用克隆影子根节点并显式跟踪 `demon.transform`；真实 Prefab 探针已确认待机、进场、归位和回家朝向四个检查点通过，且待机截图中影子可见。
- P1（已修复并通过完整箭环探针）：旧箭靶圆心标定 `(-0.18, 0.16)` 比素材真实视觉圆心低约 `0.27`，导致 9 分朝下箭序进入 8 分红圈。当前圆心已改为 `(-0.23, 0.43)`；六档环半径、箭序方位和计分规则未变。仍需在最终 Game View 手测中确认箭簇遮挡时每个箭头尖端都可辨认。
- P1（已修复并通过慢放轨迹探针）：旧箭矢先设置固定插入角再直线平移，飞行姿态僵硬。当前箭尖沿距离自适应抛物线移动，箭身跟随切线由上扬连续过渡到下压；轨迹终点仍复用既有落点。正常飞行时长经视觉评审由 `0.35s` 调整为 `0.45s`；风险集中于不同屏幕比例下的主观弧度观感，发布前仍需人工体验双方完整六箭。
- P1：挡板移动端输入使用 Unity Touch/Mouse 和运行时 BoxCollider2D。Android/iOS 模块当前未安装，无法完成触控、刘海安全区和横屏验证。
- P1：成功路径调用 `SuccessStrightly()` 后由既有关卡切换清理；失败路径派发 `GameResetEvent(Failed)`。需验证结果界面与重载时序不会短暂显示旧箭或旧计分。
- P1：标题与计分字段均使用世界空间 `TextMeshPro` 并绑定 Level 006 同款 `en SDF.asset`，不修改共享字体材质。常驻标题 `ScoreBoardTitle` 的 I2 `Localize` 绑定 `S_LevelTips_Des_level_78_1`，简体中文“计分板”已登记到飞书多语言表；本地 CSV/I2 尚未导入、其他语言仍为空，不能据此声称语言切换已完整可用。标题使用字号 `50`、`#E2E8EF`，底图不再包含标题像素；8 个运行时分数字段使用文本框 `7×6.8`、缩放 `0.1`、字号 `76`。六个单箭分纯白无描边；Box 总分 `#2F7EDF`、小恶魔总分 `#E11B00`，两者使用 `#FFFFFF` 外描边，`outlineWidth` 与 `Face Dilate` 均为 `0.2631579`。Android/iOS 真机像素密度与抗锯齿仍未验证。
- P1：`releaseNormalizedTime = 0.55` 已按 `TrackEntry.TrackTime` 而非固定秒数判定，能跟随动画进度；但 `draw_shoot` 没有 Spine Event，仍需逐帧确认 0.55 是否正好是视觉松弦点。
- P1：领域默认颜色边界为绿色半宽 0.1、黄色外边界 0.3；算法保证黄色二等分、红色三等分和左右对称，实际颜色边界仍需与素材角度对齐。
- P2：运行时创建 6 支落箭和 8 个世界空间 `TextMeshPro`，工作量有界且只在尝试初始化发生；仍需 Profiler 验证 60fps、GC 和低端机表现。
- P2：Editor/Touch 拖拽入口没有依赖共享 `DragableItem`，避免其桌面平台分支差异；仍需验证多指触控时只消费第一根触点符合预期。

## 自动与静态验证证据

- 临时 `.NET` 测试项目直接加载正式 `Level78DuelTests.cs`；加入开场门禁、持弓准备期和近靶不可逆回归后 NUnit 结果为 33/33 通过。覆盖合法组合/打乱/只读、小恶魔首次移动单次触发、触发前拒绝射箭、逐箭、双清理门禁、重新进入、挡板锁定与吸附、近靶禁止回退、忙碌门禁、默认/标定计分、近靶胜利、远距失败、非法输入。
- `Box.json` 静态核验确认 `draw_hold`、`draw_pull`、`draw_shoot` 均存在，时间线最大时间分别为 `0.1667s`、`0.9s`、`0.5s`；本次没有修改 JSON、atlas 或 texture。
- 调用路径静态核验确认 Box 进入射箭点调用 `ShowArcheryReadyPose()`，有效 Aim 点击调用 `PlayArcheryShotFromReadyPose()`；小恶魔仍调用 `PlayArcherySequence()`。
- Unity 6000.0.59f2 batchmode 本次复跑退出 1；去重后 14 类 `CS0246` 均来自既有外部依赖缺口，`Level78ActorCtrl.cs`、`Level78Ctrl.cs` 与 `Level_78/` 没有 `error CS` 命中。完整编译与 Play Mode 仍按 P0 未验证处理。
- 共享/参考差异门禁与 `git diff --check` 通过；两张输入/目标截图均已验证为可解码 PNG 并归档到 `docs/screenshot/20260716/`。
- Unity 6000.0.59f2 的 `-level78ManualProbe` 使用真实 `Level_078.prefab` 退出码 `0`，日志记录 `PASS waiting=True, openingWalk=True, returnWalk=True, homeFacingRight=True`；影子修复前后 PNG 归档到 `docs/screenshot/20260717/`。
- 箭靶素材像素扫描测得中央红区质心约 `(118.43, 196.42)`，相对 Sprite 中心 Pivot 换算为世界偏移 `(-0.2307, 0.4308)`；圆心常量采用 `(-0.23, 0.43)`。
- 临时 `.NET` 项目直接加载正式领域与测试源码，加入圆心与自然弹道回归后 NUnit 结果为 43/43 通过。
- Unity 6000.0.59f2 的慢放飞行探针记录箭身角度由 `+11.2° → +1.5° → -9.4° → -13.9°` 连续变化并退出码 `0`；15%、50%、85% 三张 PNG 与精简结果归档到 `docs/screenshot/20260717/`。
- Unity 6000.0.59f2 的 `-level78ArrowProbe` 在自然弹道实现后再次完成 5/5 场景并退出码 `0`：10/9/8、7/6/5、重复 10、重复 9、0 分均命中 PASS；PNG 与探针日志归档到 `docs/screenshot/20260717/`。
- 2026-07-20 总分样式调整后，Unity `6000.0.59f2` batchmode 与 GUI Initial Refresh 均通过，三类编译错误为 0；真实 `PlayCtrl` 进入链路加载 `Level_078`。首次 `outlineWidth=0.5263158` 经 A/B 证明确实过粗；只减为 `0.2631579` 后白边仍居中覆盖彩色字芯，与效果图不符。最终以同值 `Face Dilate` 把白边推到字形外侧。旧 Play 对象曾因只重载 Editor 探针而保留旧材质，完整重启后只读日志确认正式运行值为 `76/0.2631579/0.2631579`；彩色主体占彩色与纯白像素约 `66%`，效果图约 `63%`。Android/iOS 真机像素密度与抗锯齿仍未验证。
- Unity 已独立编译生成 `Game.Level078.Domain.dll` 与 `Game.Level078.Domain.Tests.dll`；全项目脚本错误发生在测试发现前，因此没有可接受的 Test Runner XML。
- Unity compile 日志去重后 98 个外部依赖错误，Level 078 自身 `error CS` 命中为 0。
- `git diff --check`、Prefab YAML fileID 去重、脚本/GUID/面板名扫描通过。
- 专属面板扫描确认存在 `aim_btn` 和准星 GUID，不包含 Level70 脚本 GUID、`PlayerJumpEvent` 或 `ClickVibration` fileID。
- 共享与参考文件差异门禁要求为零。

## 必做人工验证

1. 依赖完整环境加载 `Level_078.prefab`，确认无 Missing Script/引用和 YAML 导入错误。
2. 开屏确认小恶魔已显示且静止、不下坠；仅跳跃不启动；首次左/右移动只启动一次进场，到达射箭点后才开始第一箭。随后验证三种总分与箭序、逐箭落点/计分、1 秒停留、0.5 秒渐隐和归位并行收口。
3. 玩家在小恶魔阶段移动/跳跃、右侧边界、提前占位后离开再进入。
4. 挡板 Mouse/Touch 水平拖拽、范围、中点回远距点、0.2 秒吸附、吸附忙碌、近靶精确位置，以及近靶提交后无法再拖回。
5. Jump/Aim 同槽切换、有效点击锁定指针、忙碌快速连点无音效/震动/排队。
6. `draw_pull → draw_hold → draw_shoot` 与箭释放帧；确认 6 支箭离弦后先上扬再下压、飞行中无角度跳变，并复核重复分错位、目标插入和 0 分地板落点。
7. 标题使用常驻世界空间 `TextMeshPro` 和工程 `en SDF.asset`，验证文本“计分板”、字号 50、`#E2E8EF`、顶部居中、底图无烘焙标题与渲染层；8 个分数字段使用同一字体资产，验证字号 76、六个单箭分纯白无描边、Box 总分蓝色、小恶魔总分红色、两个总分白色外描边、居中、上 Box 下 Demon 布局，并确认单/双位数不越格、重开清空与小恶魔成绩保持。
8. 远距三箭显示“你输了”1.5 秒后失败、近靶 30 分显示“你赢了”1.5 秒后进入无右门成功链；两条终局播报期间均保持操作锁定。
9. 动画、飞行、吸附、归位、结算期间分别重开/退出，确认旧回调不回写。
10. Android/iOS 横屏真机拖拽、移动、跳跃、Aim 快速点击与 60fps。
11. Box 到达远距和近靶射箭点时均立即握弓；点击前不播放射击，前两箭后恢复握弓，第三箭结算时不残留待发动作。
12. Box 到达远距和近靶射箭点时，弧形蓄力表均显示在 Box 朝计分靶方向前方；上端不得进入计分板，下端贴近地面，且不得留在开场门口或遮住 Box 主体。

## 回滚路径

本次自然弹道可独立回退 `Level78ArrowTrajectory` 与 `Level78Ctrl.LaunchArrow()` 的逐帧轨迹应用，恢复固定旋转加 `DOMove`；不会影响箭靶圆心、分值落点、影子、角色动画、Prefab 或素材。持弓修正仍可局部回退 `Level78Ctrl.EnterBoxShooting()` 的待发调用及 Box 专用射击入口。若需回滚整个 Level 078 功能，再把 `Level_078.prefab` 恢复为 `BaseLevelCtrl + KnockKnock + GamePlayPanel_000` 并删除本关新增目录与专用面板。

## 2026-07-20 持弓循环速度风险补充

- 变更范围仅为 `Level78ActorCtrl.BoxReadyHoldTimeScale` 从 `0.35f` 最终调整到 `1f`；小恶魔自动射箭保持默认 `1.0×`，共享 Spine 资源未修改。
- `draw_hold` 时间线最大时间约 `0.1667s`，恢复原速后首个持弓周期门禁会比旧 `0.35×` 行为更早完成；仍需人工确认点击开放时机、循环接缝和持续蓄力观感符合预期。
- 编译与 Editor PlayMode 只能证明调用链和桌面视觉可用；Android/iOS 真机的帧率、触控节奏及不同皮肤观感仍未验证。
- 回滚只需恢复 `BoxReadyHoldTimeScale=0.35f`，不涉及 Prefab、配置、存档或资源迁移。

## 2026-07-20 靶面受击回弹风险补充

- P1（已自动验证）：命中箭若继续保持在关卡根节点，只移动 `Sp_l78_img_01` 会让留靶箭与靶面分离。当前有效命中箭以 `SetParent(target, true)` 保持世界落点后加入靶面层级，后续命中会带动该节点下全部留靶箭；专项探针确认命中箭父节点正确。
- P1（已自动验证）：Tween 若只完成后退段或中断后不复位，会使多箭后的靶面位置累计漂移。当前序列明确执行 `0 → +0.06 → -0.015 → 0`，完成回调再次写回基准 `localPosition`；生命周期清理也会 Kill 序列并复位。专项探针最终偏移为 `0.0000`。
- P1（已处理，仍需完整流程 smoke）：第三支 Box 箭原先到达后立即成功或派发失败重置；若不加门禁，重置会在回弹开始后立刻清理表现。当前保持计分即时提交，但把最终结算延后到活动受击序列完成。需要在正常入口人工确认成功/失败界面延后 `0.22s` 的节奏可接受，且不会被其他全局事件提前打断。
- P1（已自动验证）：0 分落地箭不应让靶面受力。触发条件继续使用 `placement.IsMiss`，专项探针确认脱靶期间靶面偏移 `0.0000`、箭未挂到靶面。
- P2：默认位移和时长虽在 Inspector 可调，但异常配置为 0 时会退化为零时长序列；运行时已用非负限制与 `Mathf.Max/Abs` 防止反向或负时长。发布前仍应避免把总时长调出用户确认的 `0.18–0.25s` 区间。
- P2：动效严格限定本地 X 轴并排除缩放、旋转、随机抖动、粒子、相机震动和音效，风险集中在移动端较小屏幕上 `0.06` 位移是否足够可读；可仅调整 Prefab 五个参数，无需改变代码或共享素材。
- 数据与系统边界：不修改计分规则、落点、箭飞行时间、角色动画、配置、存档、全局音频、analytics、广告、IAP、RemoteConfig 或平台 SDK。
- 回滚：删除受击序列、留靶箭重挂与结算等待逻辑，恢复 Prefab 参数即可；无数据迁移。
## 2026-07-20 蓄力指针缓动风险补充

- 原有行为基线：Level 078 Prefab 的有效指针范围为 `60° → -50°`、单程 `1s`，`StartPointerLoop()` 使用 `Ease.Linear`，因此单程恒定角速度为 `110°/s`。
- 变更范围：仅把该 Tween 改为 `Ease.InOutSine`；Prefab、时长、范围、Yoyo 循环、点击锁定、计分区间和领域状态机均不改。
- P1：缓动后端点附近停留感增强、中段瞬时速度提高，玩家锁定端部和中部计分区的体感概率可能变化；静态检查不能替代完整三箭人工手感验收。
- 验证证据：Unity `6000.0.59f2` 编译与 GUI Initial Refresh 的三类错误计数均为 0；真实 `Level_078.prefab` PlayMode 对单程五个归一化时点的实测角度与 `Ease.InOutSine` 理论值逐点误差 `0.0000°`，五张阶段图已归档。仍需人工验证远距完整三箭锁定、前两箭重启、第三箭结算，并补做 Android/iOS 横屏触控与 60fps 检查。
- 回滚：将 `Level78Ctrl.StartPointerLoop()` 的缓动恢复为 `Ease.Linear`；不涉及 Prefab、资源、配置、存档或数据迁移。

## 2026-07-20 近靶蓄力表全黄风险补充

- 原有行为基线：近靶领域判分已固定为 10 分，但弧形蓄力表继续显示远距红/黄/绿分段，容易让玩家误以为近靶仍受指针颜色影响。
- 变更范围：`Level78Ctrl` 只切换 `ArrowRange_normal` 的运行时材质；`Level_078.prefab` 显式引用该 Image 与仓库既有 `SkeletonGraphicFill-Straight.mat` 模板。原 PNG、蓝色外框、指针、Tween、领域计分和箭矢落点均不修改。
- P1（已通过桌面专项验证）：纯色材质必须保留 Sprite Alpha，否则会把整个矩形填黄或吃掉手绘弧段。真实 Prefab 截图确认只有有效弧段变为 `#FFD84D`，透明区、蓝色外框和指针均保持。
- P1：共享材质若被直接改色会污染其他使用者；当前只在运行时 `new Material(template)` 后设置 `_FillColor`，清理时恢复默认材质并在销毁时释放克隆。
- P1：远距与重开必须恢复红/黄/绿分段。代码在初始化、远距吸附和统一清理路径显式恢复默认材质，并在进入射箭时按 `duel.BoardPosition` 再同步；仍需完整重开/退出人工 smoke。
- 验证证据：Unity `6000.0.59f2` batchmode 与 GUI Initial Refresh 的 `error CS`、`Compilation failed`、`Scripts have compiler errors` 均为 0；Play Mode 日志为 `material=Level078_NearAimGaugeYellow_Runtime, fillPhase=1.000, fillColor=#FFD84DFF`，截图归档到 `docs/screenshot/20260720/20260720-level078-near-aim-gauge-yellow-after.png`。
- 未验证缺口：Android/iOS 真机 Shader Variant、不同色域/亮度、完整近靶三箭、失败/成功、重开/退出及 60fps 尚未覆盖。现有 10 分与 30 分领域回归必须在同步基线后复跑。
- 回滚路径：删除近靶材质切换及两个 Prefab 引用；无需迁移存档、配置或资源，近靶仍会固定 10 分，但会恢复视觉与规则不一致的旧表现。

## 2026-07-29 近靶全绿改用现成 Sprite 风险补充

- 用户明确要求不得通过 Sprite Alpha、Shader 或运行时材质自行绘制近靶纯色，必须直接使用现有 `Sp_l73_jdt_02.png`；本节取代上方历史材质方案的当前实现结论。
- 变更范围：`Level78Ctrl` 改为切换 `ArrowRange_normal` 与 `ArrowRange_green` 两个现有 Image；`Level_078.prefab` 把后者显式接入控制器。共享材质、原始 PNG、蓝色外框、指针、Tween、领域计分和箭矢落点均不修改。
- P1：近靶时如果只显示绿色 Image 而没有隐藏远距分段图，透明区会透出红黄绿分段。当前切换逻辑保证近靶关闭 `ArrowRange_normal`、打开 `ArrowRange_green` 并设置 `fillAmount=1`；远距执行完全相反的恢复。
- P1：Prefab 引用错误可能显示错误的绿色素材。静态 GUID 证据确认 `ArrowRange_green` 引用 `Sp_l73_jdt_02.png`，且用户桌面文件与仓库文件 SHA-256 同为 `7da86fc9b194901a448eab6cef9620a55fb25e9abb22c3231cec367ceae357c2`。
- P1：当前未执行 PlayMode，静态证据不能证明最终 Canvas 渲染、透明边缘、层级和指针叠放观感；需在用户明确指定 `verify_compile.command --probe-playmode` 后补充截图证据。
- 回滚：恢复材质模板引用与运行时材质切换代码；不涉及领域计分、资源迁移、存档或配置。

## 2026-07-21 箭矢力量感与拖尾风险补充

- 原行为基线：所有箭固定飞行 `0.45s`，远位与近位水平距离不同但停留时间相同；没有飞行轨迹拖尾。现有抛物线、箭尖落点、逐箭计分和靶面受击回弹已验收。
- 影响文件与资源：`Level78Ctrl.cs`、`Domain/Level78Duel.cs`、`Level78DuelTests.cs`、`Level_078.prefab`、`Level078ArrowTrail.mat`、`Level78ManualProbe.cs` 及本关文档。
- 风险：近位约 `0.15s`，低帧率设备上可见采样帧减少；`TrailRenderer` 透明叠加可能增加少量 overdraw；材质或 sorting 设置错误可能遮挡箭身；生命周期中断必须随箭对象销毁，不能残留独立对象。
- 规避：保持双方共用入口、每箭只创建一个短时拖尾子对象、使用共享本地材质且不实例化材质、关闭阴影、命中即停止发射；不修改弹道弧度、落点、计分、角色动画和全局系统。
- 验证证据：领域测试 `49/49` 通过并覆盖时长比例与非法输入；专项探针读取 DOTween 配置时长为远位 `0.3210s`、近位 `0.1362s`，检查飞行中拖尾存在、抵达后停止并生成 1103×545 截图；Unity 编译错误模式为 0。用户确认人工 PlayMode 手感通过；目标路由日志保留 `READY`，但未出现 `ACTIVE/SAVE_SNAPSHOT/SAVE_RESTORED`，因此存档隔离不宣称已由该次人工会话验证，运行证据由专项 PlayMode 探针承担。
- 未验证风险：Android/iOS 真机 `60fps`、透明 overdraw、不同屏幕像素密度和触觉观感在完成真机 smoke 前保持未验证。
- 回滚：恢复固定 `0.45s` 字段与 Tween 时长，移除本地拖尾材质和参数；靶面受击、自然弹道、计分、存档与配置无需回退。

## 2026-07-21 差异化计分反馈风险补充

- 原有行为基线：Level 078 的单次得分从 `0.65×` 经 `Ease.OutBack` 在 `0.20s` 回到 `1×`；总分经 `DOCounter(...) + Ease.OutQuad` 在 `0.20s` 从旧累计值逐整数滚到新值。
- 变更范围：只替换两个总分格的本地表现并为每行增加一个运行时旧值副本；单次得分反馈、计分领域、箭序、累计规则、Prefab、字体资源、既有颜色/描边规则、共享 UI 与其他关卡均不修改。
- P1：旧值副本若没有继承正式总分的参与者颜色、白色外描边与排序，交叉阶段会闪变或遮挡异常；需要真实 Prefab 截图与运行态属性断言共同验证。
- P1：第三箭结算、重开、退出或下一次计分打断翻牌时，若只 Kill Tween 而不恢复状态，可能残留双文本、半透明或垂直偏移。当前设计继续保存已提交字符串，并在完成和统一清理时隐藏旧值副本、恢复正式文本的最终值、原位、完整透明度与正常缩放。
- P1：Box 与小恶魔必须遵循同一整串反馈规则；专项探针需要分别覆盖两行，并确认 `9 → 19` 等位数变化不会退化为逐位滚动。
- P1：脱靶 `0` 必须仍强调本箭结果，但未变化总分不应产生空翻牌。
- P2：总分旧值使用 `Ease.InCubic` 向上淡出，新值使用 `Ease.OutCubic` 自下方移入淡入，位移约字高 `35%`、总时长 `0.20s`；移动端较小屏幕、低帧率和双位数观感仍需 Android/iOS 真机 smoke。
- 验证结果：Unity `6000.0.59f2` worktree 门禁通过；真实 `Level_078.prefab` 的无图形专项探针 exit `0`，断言 `0 → 10` 和 `10 → 19` 的整串交叉中间态、`0.238` 本地单位位移、透明度、无缩放/旋转、Box/小恶魔两行、脱靶跳过和主动打断收口；图形探针 exit `0`，进行中帧归档于 `docs/screenshot/20260721/level078-total-score-flip-in-progress.png`。三类编译错误计数均为 0。
- 回滚：恢复 `PlayTotalScoreFeedback(...)` 的 `DOCounter(...) + Ease.OutQuad`，删除旧值副本与翻牌复位逻辑即可；不需要迁移数据或回退领域计分。

## 2026-07-21 箭簇插入点排序风险补充

- 原有风险：命中箭全部继承模板的相同 `sortingOrder`，箭簇遮挡依赖 Renderer 并列顺序，无法稳定表达靶面上的空间前后关系。
- 变更边界：排序只在 5–10 分命中箭抵达时执行一次；0 分脱靶、飞行动画、靶面回弹、计分和其他关卡均不变。偏移基于 Level 078 全部 18 个合法插入点，因此保持在有限的 `1..18` 局部区间。
- P1：命中箭需要继续位于计分靶前方且不能覆盖计分板或 HUD。当前箭模板与计分靶同层同 order，正偏移保证命中箭高于靶面；仍需在真实 Level 078 中检查三箭交叠和其他场景 Sprite 的相对关系。
- P1：若未来扩展 4 支以上箭、5–10 以外分值或运行时自由落点，候选集合必须同步扩展，否则不同新落点可能取得相同偏移。现有领域状态固定每名参与者三箭且有效分为 5–10，回归测试要求当前 18 个点偏移唯一。
- P2：直接浮点比较是用户确认的无容差规则；Y 完全相同才比较 X。运行时与候选集合都复用 `Level78ArrowLayout.Resolve()`，避免重复公式产生精度漂移。
- 未验证缺口：Android/iOS 真机不同像素密度、完整双方六箭、所有合法分数组合及失败/成功/重开/退出流程仍需人工 smoke；自动验证不替代主观遮挡观感确认。
- 回滚路径：撤销领域排序服务、命中时排序调用和对应测试；无需修改 Prefab、存档、配置或资源。

## 2026-07-21 箭矢入靶遮挡与新箭余振风险补充

- P1（已自动验证）：遮挡不得通过平移箭 Transform 伪造，否则会让 9 分箭视觉跨区并破坏插入点排序语义。当前根节点直接承载 `Level78ArrowTrajectory` 的逻辑箭尖位置，遮罩只改变 `Visual` 子节点的像素可见性；专项探针最终箭尖误差为 `0.00000`。
- P1（已自动验证）：`SpriteMask` 默认会影响排序范围内的其他 Sprite。当前每支遮罩把 front/back sorting layer 与 order 精确收窄到本箭命中后的 `SpriteRenderer`，三支箭仍沿用 `Level78ArrowDepthOrder` 的唯一排序；有效命中启用 `VisibleOutsideMask`，0 分脱靶保持 `None` 且遮罩禁用。
- P1（已自动验证）：余振若绕箭身中心旋转会让箭尖离开计分环。当前只旋转逻辑箭尖根节点，固定支点就是既有插入点；结束回调和统一清理都会恢复原 `localRotation`，探针采样约为 `+3° → -1.5° → 0°` 且最终角误差为 `0°`。
- P1：`SpriteMask` 是 Level 078 新接入的生产渲染路径。桌面真实 Prefab 截图已确认靶面原色从遮挡区露出且没有黑块，但 Android/iOS 的 URP SpriteMask Variant、低端机批次和不同分辨率仍需发布前 smoke。
- P2：每支飞行箭新增一个根节点、一个遮罩子节点和一个 `SpriteMask`；每名参与者最多三支箭，生命周期继续由 `spawnedArrows` 统一清理。未新增材质实例、纹理、粒子或逐帧查询，性能增量限定为命中箭的局部遮罩与 `0.22s` 旋转 Tween。
- P2：遮挡深度 `0.15`、首摆角 `3°`、总时长 `0.22s` 都暴露在 `Level_078.prefab`。异常调大可能导致箭头截断或橡胶感；发布配置应维持用户确认的约 `14–16px`、`3° → -1.5°`、`0.20–0.24s` 范围。
- 数据与系统边界：不修改计分、落点、插入角、箭飞行时间、角色动画、存档、配置、事件、analytics、广告、IAP、RemoteConfig 或平台 SDK；不增加粒子、常驻裂纹和压痕。
- 回滚路径：删除箭根/视觉子节点拆分、局部遮罩和余振序列，移除 Prefab 四个参数；既有计分领域、箭环位置、自然弹道、箭簇排序和靶面回弹可保持不变，无数据迁移。

## 2026-07-21 无右门胜负播报风险补充

- 原有行为基线：第三支 Box 箭完成靶面受击后立即调用成功或失败；`BaseLevelCtrl.successAnim` 指向右侧 `door_0`。左侧 `StartDoor` 独立承担开场入场动画。
- P1（已处理）：删除右门但残留 `successAnim` 会产生错误成功引用；误清 `enterDoorAnim` 会破坏开场。当前 Prefab 已完整移除右门、把 `successAnim` 置空，并保留左侧 `StartDoor` / `enterDoorAnim`。静态 GUID、对象名和序列化引用扫描均通过。
- P1（已自动验证）：行动交接提示若阻塞输入会改变既有解谜节奏。专项探针在“到你了”显示期间确认 `BaseLevelCtrl.ActorCtrlable=true`，挡板调整与玩家移动按原阶段立即开放。
- P1（已自动验证）：比分播报期间必须锁定终局，不能提前成功、失败或接受第四箭。`CompleteSettlement()` 只接受一次，先停指针、关闭 Aim、锁定角色，再等待 1.5 秒；同步后真实 Prefab 探针分别确认胜利约 `1.413s`、失败约 `1.501s` 后才触发结果链。
- P1（已自动验证）：共享气泡默认在 `SceneLayer`，低于本关计分板的 `BaseLayer`，会遮掉末字；`Twinkly SDF` 还会把中文放入 fallback 子网格。当前只调整 Level 078 运行时气泡实例：显式绑定支持中文的 `korean SDF` 与匹配材质、字号上限 `4`，并把根排序层对齐计分板且保留 order `300`。三张 1103×545 截图确认三句完整可读；共享 `HintBubble.prefab` 未修改。
- P1：胜利播报满 1.5 秒后仍进入 `SuccessStrightly()` 的标准无门链，该入口自带约 0.8 秒成功过渡；这是已确认节奏，不应改为直接派发成功状态。若后续主观节奏调整，应只改 `demonSpeechDuration`，不得绕过成功音效、星星、状态与打点链。
- P1：重开/退出必须取消延迟与气泡。当前 `CleanupAttempt()` 同时 Kill `settlementDelayTween`、`speechCloseTween`，关闭气泡并递增 `sessionVersion`；最终回调还校验当前会话及 `Settled` 阶段。
- P2：三句气泡改用 `S_LevelTips_Des_level_78_2`、`S_LevelTips_Des_level_78_3`、`S_LevelTips_Des_level_78_4`。飞书多语言表当前只填写简体中文，其他语言留空，且本任务不导入本地 `localization.csv` / `I2Languages.asset`；导入前运行时会 fallback 显示 key 原文，不能据此声称多语言已完整可用。
- P2：当前分数集合证明无同分：小恶魔 28/29，Box 远距至多 27、近距 30。若未来扩大计分集合，必须重新定义平局语义并补测试；当前不新增平局分支。
- 验证边界：V2 覆盖真实 Prefab、气泡文字网格/截图、输入开放、胜负时序和右门删除；V1 `49/49` 覆盖既有分数不变量。未执行 V3 构建、Android/iOS 真机、不同分辨率人工操作或多语言测试。
- 回滚路径：恢复右门和 `successAnim`，删除本地播报字段、气泡实例与延迟，恢复即时胜负；左侧入场门、领域计分、配置和存档无需迁移。

## 2026-07-22 失败结算待机风险补充

- 原有行为基线：失败结算调用 `CancelLevel78Action(false)` 清理射箭流程并清空 Spine 轨道，随后锁定角色等待 1.5 秒“你输了”播报；没有显式稳定动画，因此结算早于 `draw_shoot` 收尾时可能暴露持弓、射箭或 setup pose 残姿。
- 变更范围：仅为 `Level78ActorCtrl` 增加失败结算专用的普通待机入口，并由 `Level78Ctrl.CompleteSettlement()` 的 `Failure` 分支调用；胜利分支保留原清理路径。
- P1：失败播报期间若恢复通用动画控制或移动，会破坏终局锁定。当前入口只取消射箭回调并播放循环 `idle`，不解锁 `level78ControlLocked`；控制器随后继续设置 `ActorCtrlable=false` 与 Level 078 控制锁。
- P1：若把普通待机应用到所有取消路径，会改变重开、退出、小恶魔销毁和胜利过渡。当前 API 只在失败结算分支调用，统一生命周期清理仍保留原语义。
- 验证结果与边界：`V2` 真实 Spine 探针最终确认“你输了”期间轨道 0 为循环 `idle`、关卡输入锁定，失败约 `1.501s` 后重置；Level 078 领域回归 `49/49` 通过。首次 `-nographics` 因既有截图渲染崩溃，一次图形运行因既有胜利时序采样 `1.241s` 未过容差，原命令重试后胜利 `1.403s`、失败 `1.501s` 均通过。验证上限为 `V2`，未执行构建或真机；Android/iOS 观感与设备性能仍保持未验证。
- 平台与性能：每局失败只增加一次既有 Spine 动画切换，不增加 Update、实例化、资源加载或持续分配；Android/iOS 横屏最终观感和设备性能仍未验证。
- 回滚路径：删除 `ShowFailureSettlementIdlePose()` 及失败分支调用，恢复统一清轨；不涉及 Prefab、配置、存档、事件或资源迁移。

## 2026-07-22 双收集星风险补充

- 原有行为基线：`Level_078.prefab` 没有 `Postcard_Star`，因此本关没有明信片收集进度、星星背包奖励或对应 HUD 反馈。变更只增加两个工程既有星星 Prefab 实例，不修改 Level 078 射箭状态机或共享收集实现。
- P1（已自动验证）：两颗星若误用同一 `index`，既有重复收集保护会导致玩家只能得到一个有效收集。静态检查和真实 Prefab 探针均确认左星为 `One`、右星为 `Two`。
- P1（已自动验证）：右星若放在挡板移动终点右侧，即使完成解谜也无法取得；若碰撞体跨过初始挡板右边界，则可能在移动挡板前提前取得。专项探针使用真实挡板、玩家与星星 Collider，确认右星初始完整位于挡板右侧，移动后完整位于挡板左侧且在新射箭点 `x=1.80` 之前。
- P1（已自动验证）：左星需要开局直接可达，且两颗星都不能要求跳跃。探针把真实玩家 Collider 仅水平移动到两颗星中心，保持原行走高度，二者重叠检查均为 `true`；左星 Collider 还完整位于初始挡板左侧。
- P1（保留风险）：本次没有从正式入口实际触发两颗星并观察存档、背包和 HUD。变更复用 73 个既有关卡共同使用的 `Postcard_Star` 链路且未改共享代码，但保存失败、HUD 缺失和重复进关状态仍需后续完整流程 smoke。
- P2：运行时只新增两个既有动画/Trigger 对象，不新增 `Update`、材质实例、资源协议或持续分配；性能风险与其他配置两颗星的关卡一致。Android/iOS 真机触发手感、不同屏幕可视性和设备性能未验证。
- 环境边界：探针成功并记录 PASS 后，Unity Package Manager 出现账号授权请求失败日志；该日志发生在验收断言与截图完成之后，未命中 `error CS`、`Compilation failed` 或 `Scripts have compiler errors`，与 Level 078 资源变更无调用关系，不作为本任务失败。
- 回滚路径：删除两个星星实例、专项探针和对应文档补充即可；不修改或清理既有明信片存档，不需要数据迁移。

## 2026-07-22 人工场景布局合并风险补充

- 原有行为基线：同步后的集成基线已把计分板标题迁移为常驻 `ScoreBoardTitle` TMP，并保留两颗收集星的既定位置和编号；用户人工版本来自同步前会话，不能整文件覆盖基线。
- P1（已处理）：`ScoreBoardTitle` 与 `Sp_l78_img_02` 的新 `SortingGroup` 在 YAML 相同插入位置产生冲突。三方合并同时保留 TMP 标题对象和计分板 `SceneLayer/0` SortingGroup，冲突标记、重复 YAML 锚点和悬空本地 `fileID` 均为 0；最终 Prefab 已通过 Unity batchmode 导入、GUI Initial Refresh 与指定关卡 `READY`。
- P1：计分靶新增 `SceneLayer/50`、计分板新增 `SceneLayer/0` 后，二者与留靶箭、标题、讲话气泡及角色的相对遮挡可能变化。静态顺序只能证明配置值，最终画面仍需真实关卡人工检查。
- P1：人工版本还包含 Actor Prefab 嵌套 Transform/组件覆写、提示文本和位置变化。当前只保留用户明确要求合并的磁盘结果，不把这些变化解释为新的玩法规则；角色碰撞、射箭站位、提示可读性和多语言效果在完成针对性运行验证前保持未验证。
- P2：`CommonBg` 位置和场景对象层级变化可能在不同宽高比下露边或改变构图；未执行 Android/iOS 横屏、刘海安全区、真机像素密度和性能验证。
- 数据边界：本次不修改存档、配置、事件、资源加载地址、analytics、广告、IAP、RemoteConfig 或平台 SDK；风险集中在 Level 078 Prefab 的局部层级、坐标、排序与可见文本。
- 回滚路径：回退本任务提交中的 `Level_078.prefab` 人工差异和本节记录；保留集成基线已有的 `ScoreBoardTitle`、计分板底图及其他已合并修复，无需迁移玩家数据。

## 2026-07-24 Jump/Aim 控制迁移风险补充

- 原有行为基线：`GamePlayPanel_026_78.prefab` 通过 `Level78GamePlayPanel` 管理 Jump/Aim 同槽切换、Aim 点击转发和暂停门禁；`Level78Ctrl.Update()` 每帧尝试解析该专用组件并订阅 `AimPressed`。
- 变更范围：删除专用面板脚本与 `.meta`，Prefab 根组件切回共享 `GamePlayPanel`；`Level78Ctrl` 本地缓存 Jump/Aim、绑定 `OnClickDown`、控制显隐并复用现有游戏状态事件维护暂停门禁。共享 `GamePlayPanel`、按钮 Prefab、领域状态机、关卡场景、配置和存档均不修改。
- P1：Prefab 脚本 GUID 若未切回共享 `GamePlayPanel`，删除专用脚本后会产生 Missing Script。静态检查已确认 `GamePlayPanel_026_78.prefab` 使用共享 GUID `4195c0e2d24708f489dc7405fd70ef49`，旧 GUID 在活动 `Assets/Game` 中清零；仍需 Unity 导入确认序列化字段无丢失。
- P1：`Root/jump_btn` 或 `Root/aim_btn` 路径变化会导致对应按钮无法缓存，进而出现 Jump 不隐藏或 Aim 无输入。当前 `aim_btn` 是本地对象，Jump 继续由 Prefab 的 `JumpBtn` 引用和既有层级提供；真实运行层级仍需 PlayMode 复验。
- P1：移除面板子类不能丢失暂停门禁。当前 `Level78Ctrl` 已订阅 `GameResumeEvent` 和 `SwitchGameStateEvent`，`OnAimPressed(...)` 在暂停时直接返回；设置弹窗、提示弹窗和恢复后的点击仍需手工验证。
- P1：重开、退出或面板销毁时必须解绑 Aim，避免旧按钮访问已清理的领域状态。`CleanupAttempt()` 继续先关闭 Aim，再由 `UnbindPanel()` 移除 `OnClickDown` 并清空按钮/面板引用；Unity 生命周期中断仍需专项 smoke。
- P2：控制器只在当前面板引用为空时解析按钮，移除了成功绑定后每帧 `GetComponent<Level78GamePlayPanel>()` 的查询；没有新增 Update 分配、实例化、资源加载或 Tween。
- 现有系统复用：继续使用 `PlayCtrl.CurrentGamePlayPanel`、`CustomButton.OnClickDown`、`GameResumeEvent`、`SwitchGameStateEvent` 和 `Level78Duel.TryStartBoxShot(...)`；不接入 `GamePanel_UseBtnCommonFeature`，避免为本关新增 `SwitchObjActiveEvent` 依赖。
- 验证证据与缺口：旧类/旧 GUID 活动引用扫描、共享面板 GUID、Aim 节点和 C# 静态调用检查已完成。此前生成工程尚未适配 `LevelItem → MainLevel` 目录重命名，构建被旧路径/重复类型错误阻断，不能据此确认或否定本次迁移的独立编译结果。Unity Prefab 导入、PlayMode、暂停/恢复、三箭、失败/成功、Android/iOS 横屏和 60fps 均未验证。Prefab 的行尾空格来自任务开始前已有工作区改动，导致包含该文件的 `git diff --check` 仍失败，不能把该失败归因于本次 GUID 修改。
- 回滚路径：恢复 `Level78GamePlayPanel.cs/.meta`、专用脚本 GUID 与 `AimPressed` 订阅路径，移除 `Level78Ctrl` 的按钮缓存和暂停迁移；领域层、箭矢、计分、场景、配置、存档和玩家数据无需回滚。

## 2026-08-05 小恶魔继承玩家 Skin 风险补充

- 原有行为基线：运行时小恶魔整体克隆玩家 Actor，随后只把 Spine 全身 Skin 设置为 `box_bad`。玩家在克隆前已经实例化的 back、face、body SkinPart 子物体仍会被复制；`ActorSkinBinding` 的玩家身份门禁只能阻止后续刷新，不能删除这些克隆子物体。
- 受影响文件与系统：仅 `Level78Ctrl.cs` 的 `CreateDemon()` 初始化路径，复用 `ActorSkinBinding`、`BoneFollowerAutoBinder`、`BoneFollower` 与 `ActorSpineCtrl.SetSkin(...)` 的现有边界；Prefab、资源、DB/Save、配置、事件与平台流程未改。
- P1（已处理）：只禁用 `ActorSkinBinding` 仍会残留克隆时已存在的 SkinPart。当前同时禁用绑定并删除三个已识别挂点下的全部子物体，再应用 `box_bad`，与 Level26/64 已有生成体清理模式一致。
- P1（已处理）：异步 SkinPart 加载若在克隆后继续完成，可能重新挂载部件。禁用绑定会触发其版本递增与事件解绑，异步回写还受 `isActiveAndEnabled` 和 apply version 门禁保护。
- P2：挂点查找只接受名称为 `back`、`face`、`body` 且带 follower 组件的节点，避免误删同名普通节点；若未来 Actor Skin 挂点命名或组件协议改变，本地清理需同步更新。
- 验证证据：定向 C# 构建为 `0` error、`1119` 个项目既有 warning；代码差异检查通过。未执行 Unity PlayMode、带三类部件的实际生成、Reset/Replay、Android/iOS 真机或 60fps 验证，因此这些结果保持 `uncertain`。
- 性能与生命周期：每次尝试只在创建一个小恶魔时做一次有限子层级扫描和最多三个挂点清理，不增加 `Update`、Tween、资源加载或常驻分配。
- 回滚路径：移除 `RemoveDemonInheritedSkins()`、两个挂点清理方法及新增 using，并恢复创建时直接设置 `box_bad`；不需要修改玩家数据或资源。
