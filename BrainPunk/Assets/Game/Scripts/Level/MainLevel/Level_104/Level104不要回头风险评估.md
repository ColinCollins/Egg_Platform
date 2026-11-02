# Level 104 不要回头风险评估

## 2026-08-18 行为等价精简风险复核

- **事实**：`dogExitX`、`arrowSfxTag` 已在注释中标记为历史保留且代码零读取；`pitPlatformCollider` 仅被赋值、从未读取。生命周期清理体原本重复。
- **直接结论**：现行小狗下落终点、箭矢破空音、左岸按钮可用碰撞器、机关时序、事件和结算逻辑均不依赖被删成员；共享清理入口保持原顺序。
- **假设**：不把未来回滚对旧 Inspector 值的便利性视为当前运行功能；若未来恢复旧方案，应从版本历史同时恢复字段和接线。
- **缺失证据**：Prefab 仍含两个旧序列化键，但当前源码已无对应承接字段；尚未通过当前 Unity Editor 的导入、重保存和 PlayMode 日志确认其表现，也未完成真机机关全链，标记为 `uncertain`。
- **回滚**：恢复 `dogExitX`、`arrowSfxTag`、`pitPlatformCollider` 及两处赋值，并把清理代码展开；其它 Level104 资产无需回滚。

## 原有行为基线

在 `a63eb6d20` 快照导入前，当前 1.11 基线没有 Level 104 Prefab、Domain、运行时脚本或本地资源。本关以独立 Prefab 和本地组合控制器加入，不修改共享 `ActorCtrl`、`GamePlayPanel`、DB 或配置表。

## 影响文件与系统

- `Assets/Game/Prefabs/Level/Level_104.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_104/`
- `Assets/Game/ArtSrc/Animations/Level_104/`
- 只读复用：`BaseLevelCtrl`、`ActorCtrl`、Bear EventSystem、DOTween、SoundsGood 音效入口、标准门/陷阱/FallArea 链，以及小狗叫喊的 `Assets/Game/ArtSrc/Animations/Noise/Bark.prefab`；2026-08-07 起新增只读复用共享 `Plantform_short`、`Ground_short`、`FallArea` Prefab（均实例级覆写，不改源资产）；本次左岸按钮只读复用 `SceneCommonButton.prefab`、`SceneCommonButton` / `PressButtonBase` 与 `Sp_button_1_01.png`；出口方向引导组合只读复用 `Arrow_02.prefab`、`Sp_arrow_1_03.png`、`Tips.prefab` 与现有 I2 key `S_LevelTips_Des_level_15_02`；新增关卡标题占位另复用同一个 `Tips.prefab` 与现有 `S_LevelTips_Des_level_83`。上述共享文件、`I2Languages.asset`、Level 15 与 Level 083 均不修改。

## 风险项

1. **P0：正常入口缺失。** 当前 LevelData/LevelSort 没有 Level 104，`PlayCtrl.CreateLevelAsync` 又依赖配置提供的 path。若该关应正式上线，会在选关/下一关链路中不可达；若它只是暂存关，必须明确记录，避免误补配置。
2. **P1：小狗击飞可能停在冻结态。** 小狗命中后 Domain 保持 `Result=None`、阶段为 `DogKnockback`，失败依赖 Actor 最终进入共享 FallArea。任何碰撞层、击飞方向、力度或场景边界错误都可能造成既不失败也不恢复控制。
3. **P1：箭雨独立启动与碰撞仲裁。** 玩家中心首次达到 `X=-5.18` 时即可启动箭阵，小狗冲锋、命中和离场不再是前置条件；箭命中仍立即判负、刺墙接触仍延到 `LateUpdate`。新启动点与移动中的 Actor Collider 是否相交、不同 FixedUpdate/帧率下是否保持箭优先，均需运行证据。
4. **P1：Actor降速与第三屏整组平移后，身后假坑、刺墙与飞门节拍仍可能在运行时重叠。** 当前Actor实例水平速度为4.2，门槛为狗bark X=26.5、狗run X=28.5、刺墙X=41、飞门X=44.9（门根49.4左侧4.5）。刺墙入口还要求Actor主碰撞体左缘超过坑右缘0.15；揭坑改在逼近间距≤2.0（接触兜底）。bark到run门槛约0.48秒，run门槛至刺墙约2.98秒，刺墙到飞门约0.93秒；刺墙名义约在Actor X=46.68接触、晚于飞门约0.42秒，2.0间距揭坑约启动后1.05秒。飞门门槛距绕屏点 8.2，持续右移约 1.95 秒后才绕屏。静态值不能证明实际主观节奏。
5. **P1：固定世界坐标、相机跟随与绕屏对画幅敏感。** 2026-08-12 `CameraFollowRange` 已扩为三屏：中心 (19.75,-2)、尺寸 65.5×20、边界 X∈[-13,52.5]；`GameCamera` 位置 `(0,0,-10)`、正交尺寸 `5.9`、死区、平滑和偏移不变，1920×1080 / 2340×1080 初始中心均为 X=0。`wrapRightX` / `spikeWallExitX` 为 53.1，静态几何证明二者比最右锁定视野边界 52.5 多 0.6，且距右岸拼接地面碰撞右沿 55.4958205（≥53.7）仍有承托余量。第一屏死区 X∈[-9,10]、左绕回点 -11.3、Y 轴构图和玩法触发点不变；因此左绕回点在 1920×1080 仍位于初始画面外，但在 2340×1080 的初始可见区间 [-12.78,12.78] 内。跟随平滑、绕屏瞬移时该可见回入的实际观感、第三屏拼接与低帧率表现均无运行证据。
6. **P1：三屏场景与升降陷坑均为 Prefab YAML 序列化接线。** 历史 `Ground_right_ext` 已改名并收短为 `Ground_lift_pit_left`，`Ground_lift_pit_top`/`safe`/`right` 当前又接入 103 同款运行机制；2026-08-13 整组右移 20 后，坑左岸空档由四段 `Ground_short (2)/(3)/(4)/(5)` 按中段贴边续接，右岸仍为原生短段 `Ground_lift_pit_right` + 原位复制 `Ground_long (1)`，外边界约 14.26/75.4958205（≥53.7）。六个坑沿节点依赖运行时精确层级解析，顶/底条带和两岸主体依赖实例浮点覆写闭合。`CommonBg (1)`、三段远景和第三屏新增中景/植物对象与右岸拼接段共同构成三屏扩展。静态解析可确认 fileID、引用、父层级和确定性几何。坑口范围内唯一的 `Sp_em_1_02 (2)` 挂在 `Ground_lift_pit_top`，组内 SortingOrder=1；右岸继承0、`Ground_lift_pit_left/21 < Ground_lift_pit_top/22 < CommonBg 纸张 Mask/23`。剩余风险是 Unity 实际导入、SortingGroup 与嵌套纸张 Renderer 合成、竖线逐像素收口、第三屏拼接观感、接缝发线、色差与遮挡关系，均需人工预览。
7. **P1：既有测试资产与当前行为不一致。** 仓库保留 Level 104 EditMode Domain 测试，但其中飞门阶段命名与离屏换位断言属于旧轨迹；当前项目约束禁止为本任务调整或执行这些测试。生产代码保留旧 Domain API/阶段名兼容别名以避免符号断裂，但可见左飞缺少自动回归证据。
8. **P2：销毁、失败清理与升降陷坑定格。** 本关持有小狗、箭、刺墙和飞门多个 Tween；当前代码有统一 Kill/禁用路径。升降陷坑另以本局冻结门禁监听标准陷阱与 FallArea 失败入口，并在既有规则终态和 `Actor.IsDied` 路径兜底；冻结只 Kill 升降 Tween，不改地块位置、Collider 或坑沿显隐，所有启动与完成回调都会拒绝迟到推进。静态调用链不能证明实际死亡帧定格、音效自然收尾、失败重开、退出关卡和对象销毁后没有迟到回调或残留音画。
9. **P1：坑洞实例与第二屏地面接线均为手写 YAML 接线。** `Ground_pit_right`（Ground_short 实例）、`Plantform_pit`（Plantform_short 实例）、`FallArea_Pit`、`PitBackdrop` 与原 `Ground_long` 收短覆写均由定向 YAML 编辑维护。2026-08-08 左移后，右岸根 X=8.32，平台/FallArea/PitBackdrop 中心 X=18.02；左岸右帽 local X=7.3033、末段条带 posX=1.6932/1.6982 且 scaleX=0.3882289、碰撞 sizeX/offsetX=15.0301/6.9549、主体 scaleX/posX=150.301/6.9549。静态几何可证明坑口 `[0.82,7.82]`、宽 7、中心 4.32，但 Unity 实际导入、坑沿/坑壁观感、平台跳跃与 FallArea 触发时序未验证。
10. **P2：小狗双门槛、bark前摇与升降块重叠改变反应窗。** 狗根位于X=24；玩家X=16（相对距离8）触发bark，成功动画后保持最终姿态0.4s，且X=18（相对距离6）run门槛已达到才武装碰撞并以6单位/秒冲刺。持续右移时0.444秒即越过run门槛，但完整表现需约1.033秒，因此实际起跑约在玩家X=16.65；按狗Collider左缘19.4与Actor名义右缘约+0.5估算，起跑前水平余量约2.25，按12.5 u/s相对速度约0.18秒接触。若玩家停步，表现后恢复idle等待；bark失败也不绕过run门槛。实际动画、声波、构图、碰撞开放帧和跳躲时机仍需运行证据。
11. **P2：箭阵反向后完成登记点修正。** 从右往左落后最后落地的是最左箭，`isLastArrow` 已改由首个有效索引承担；若后续改回左先落，此处必须同步还原，否则箭阵提前收口会使仍在空中的箭失去致死判定。
12. **P1：箭阵先触发不得阻断后续小狗。** 箭阵在 `X=-5.18` 启动时，玩家尚未到小狗bark门槛 `X=16`；Domain继续以`DogHasCharged`作为整条叫喊/冲锋序列的一次性事实，控制器另以表现完成与X=18门槛决定实际run。既有EditMode资产按项目约束未调整或执行。
13. **P1：升降陷坑仍保留掉入兜底，必须避免被运行时变成真实躲法。** 设计合同要求陷坑只在玩家完整走过后、刺墙逼近过程中于身后揭露。代码继续以 `actorX≥41` 加 `Actor Collider bounds.min.x ≥ pitRightEdge + 0.15` 约束刺墙启动，揭坑另按刺墙 Trigger 左缘与 Actor 主体右缘间距 `≤2.0`（接触兜底），Collider 无法解析时不得猜测提前触发；按当前 PolygonCollider2D 最左点 -0.5519，中心 X=41 时左缘约40.4481，较新坑右缘38.7453多约1.7028，扣除0.15后仍余约1.5528。既有自动上跳只为异常外力造成的掉入防软锁；若实际物理、碰撞体选择或帧序仍让正常向右玩家落坑并避开刺墙，即为本需求失败，不能把自动上跳解释成正确解法。
14. **P1：按钮按绝对 `X=4.5` 冒出后，时机与观感仍缺运行证据。** 当前入口只看玩家中心 `actorX >= 4.5`，不再读取 `Plantform_pit` 落地，也不再跟随最右轨。低帧率跨过门槛、绕屏后是否仍被一次性门闩挡住、以及玩家尚未看见按钮就已走过第一屏，都可能改变诱导效果。按钮初始化只关闭 Renderer/Collider 并保留终态 Transform，冒出完成后才开碰撞；0.3 秒 `OutBack` 的地下穿帮、缩放观感、共享按压与音效仍缺运行证据。本次未指定按下后的新机关效果，代码没有注册业务回调。
15. **P1：箭阵破空音此前经 AudioManager 从头播放，会落在落地之后。** `arrowWhoosh.ogg` 自带约 0.54s 前导静音，104 单箭下落仅 0.45s；现已改为本关 AudioSource 小池从 0.54s 起播、音量 1.0。静态只能证明接线、跳过静音和 Sfx 开关；十支 0.05s 错峰叠音、与落地渐隐的听感、失败清理是否截断，均为 `uncertain`。
16. **P1：箭阵收为 10 轨且单支改为 `0.45s / InQuad`、错峰 `0.05s`、落地 `0.15s` 渐隐后，命中窗口与右端落地时序会变化。** 覆盖区现为 `-0.18…8.82`，门槛是最左轨左侧 3.5（`actorX>=-3.68`），不等于覆盖区左界。Actor 以 4.5 u/s 从 `-3.68` 走到覆盖区左界 `-0.18` 约 0.78s；整轮 `0.45+9×0.05=0.90s`，因此右端几支可能在玩家进入覆盖区前已落地并开始 0.15 秒渐隐。箭与玩家的相交窗口、10 支空中重叠/尾随 overdraw、与出口方向引导箭头的水平叠画，以及与独立狗/刺墙/飞门的并行关系都会变化；静态参数只能证明轨数、时长和缓动曲线，不能证明新节奏下安全通过或必然命中。该口径覆盖同日 12 轨 `-0.18…10.82` 与整轮 `1.00s`、`0.8s / 0.08s / 0.3s` 与整轮 `1.68s`，以及更早五轨 `1.5s / 0.18s / 2.22s` 与落地 `0.5s` 渐隐。
17. **P1：078 同款尾随的竖直朝向与生命周期缺少运行证据。** 078 原箭水平向右，104 通过箭根 -90° 旋转为向下；代码沿用 Sprite 本地负 X 半长锚定箭尾，静态变换关系表明尾随应位于箭上方，但实际长度、透明叠加、SortingGroup 内层级和 10 支重叠 overdraw 仍需画面确认。若接地、重置或 Tween Kill 未正确停止并清空，可能留下残线或在箭重定位时产生跨屏长线；当前入口分别停止发射和统一 `Clear()`，但只属于静态调用链证据。
18. **P1：箭命中特效位置与双音效叠加缺少运行证据。** 触发器用双方 Collider 最近点中点近似首次接触位置，Trigger 穿透深度、Collider 形状和 FixedUpdate 时序可能让粒子中心与玩家感知的箭头接触点存在偏差。`Knock_Explosion`（本关三处实例已禁用子节点 `Knock 1`/`KNOCK!` 文字层，根星爆保留）、`trap` 与 `failed` 在同帧播放，静态调用链不能证明粒子覆盖、音量层次和失败切换前可见时长；独立粒子实例避免箭命中移动小狗或执法刺特效，但关卡快速销毁仍可能截断尾帧。文字层禁用仅为 Prefab 实例覆盖，运行时是否确实不显示 `KNOCK!` 仍为 `uncertain`。
19. **P2：第二颗星不随坑/狗迁移。** 第二颗星仍位于 `(14,-2)`（用户明确保持）；狗run门槛现为X=18，星落在新固定坑口左缘附近，拾取与过坑/bark节拍关系为 `uncertain`。静态坐标不能证明实际音画层次。
20. **P2：常驻出口方向引导组合可能被场景层遮挡、发生本地化排版问题或与追逐箭阵混淆。** 箭头 `ExitDirectionGuideArrow` 固定在 `(8.9,2.7)`，文字 `ExitDirectionGuideText` 固定在 `(6.58,3.16)`，复用 `Tips.prefab` 与 `S_LevelTips_Des_level_15_02`；文字名义 RectTransform 包络 X `[2.80635,10.35365]`、Y `[2.03965,4.28035]`，在 1920×1080 初始画幅右侧仅余约0.14世界单位。共享 Tips 默认 `Background/100` 会被本关纸张 `SceneLayer/23` 压住，因此实例覆写为 `SceneLayer/25`，与箭头 `SceneLayer/24` 一起低于 `Midscene/51`。剩余风险包括 Unity 对嵌套 SortingGroup、纸张和中景的实际合成，各语言字形、换行、截断、RTL 与缺失翻译回退，不同横屏比例/HUD 的留白和遮挡，以及玩家是否把组合误读为致死箭或门的实时位置；这些都需要人工预览，静态层级与 RectTransform 不能证明。
21. **P2：新增标题占位可能与原出口文案争夺层级或被误认成正式标题。** `LevelTitlePlaceholder` 中心 `(-1.2,3.06)`、容器 X 范围 `[-4.97365,2.57365]`，静态上与 `ExitDirectionGuideText` 保留约 `0.2327` 间距；但标题继承 083 的 `SceneLayer/100`，高于原出口文案 `SceneLayer/25`，且 `S_LevelTips_Des_level_83` 当前简体中文为“动人心弦”。实际文字网格可能因语言、换行或字体超出名义容器，层级也可能与 Midscene/前景产生不同合成；玩家还可能把占位内容误认成 Level 104 正式标题。以上均缺少运行或人工画面证据，保持 `uncertain`。

## 验证证据

### facts

- 关卡标题占位静态核对：`LevelTitlePlaceholder` 的 PrefabInstance、stripped RectTransform 和根 child 各唯一一处，新增 fileID 唯一；源 GUID `49d63fd22a39a044bab1c28dbf961bcb` 解析到共享 `Tips.prefab`，实例覆写 `m_text=Well... \nShow some respect.`、`m_margin.z=-0.40812898`、`mTerm=S_LevelTips_Des_level_83`、中心 `(-1.2,3.06)` 与 SceneLayer。共享源提供 `7.5473×2.2407`、SortingOrder 100、TMP/Localize/SortingGroup；原 `ExitDirectionGuideText` 仍保持 `S_LevelTips_Des_level_15_02`、`(6.58,3.16)`、`SceneLayer/25`，两个名义容器间距约 `0.2327`。新增 anchor 无重复，引用闭合，共享 Prefab 与 I2 库未修改。

- 出口方向引导组合静态核对：箭头与文字各自的 PrefabInstance、stripped Transform/RectTransform 和根 child 引用均恰好一处，新增 fileID 唯一。箭头源 GUID 解析到 `Arrow_02.prefab` 及 `Sp_arrow_1_03.png`；文字源 GUID `49d63fd22a39a044bab1c28dbf961bcb` 解析到 `Tips.prefab`，实例覆写 `m_text=Way to the Finish.`、`mTerm=S_LevelTips_Des_level_15_02`、`m_AnchoredPosition=(6.58,3.16)` 与 `SceneLayer/25`，没有字体、颜色、尺寸、缩放、Active 或组件增删覆写。I2 key 存在且简体中文为「通往终点的路。」；共享 Tips 尺寸为 `7.5473×2.2407`，含 TMP、Localize、SortingGroup 且不含 Collider/Animator。文字相对箭头偏移 `(-2.32,+0.46)`，名义 RectTransform 位于 1920×1080 / 2340×1080 初始相机范围；`git diff --check`、fileID 重复检查和共享资源只读检查通过。

- 当前代码明确分离玩家左移与脚本绕屏入口。
- 玩家中心达到 `X=-5.18` 时，`Level104Ctrl.Update` 经 `ArrowVolleyHasFired` 一次性门禁直接调用 `TryStartArrowVolley()`；小狗离场回调只登记 `DogHasExited`，不再启动箭阵。
- `TryStartArrowVolley()` 不再要求 `DogHasExited`；小狗启动改由 `DogHasCharged` 门禁控制，避免先落箭阵阻断后续小狗。
- 小狗双门槛静态核对：共享`sp_smalldog.json`的bark最大关键帧时间0.6333s；源码默认值与Prefab均为`dogBarkTriggerDistance=8`、`dogChargeTriggerDistance=6`、`dogPostBarkHold=0.4`、`dogRunSpeed=6`。X=12启动bark，表现完成与X=14门槛同时满足才进入`StartDogRun()`；该方法仍是唯一隐藏特效、武装狗碰撞、切run和创建移动Tween的入口。停步时恢复idle，播放失败只标记表现完成。移动段4秒，从bark到隐藏名义5.0333秒。
- 当前Prefab中门/刺墙根为49.4/50.8、狗根34.5、固定坑中心约18.02、升降陷坑中心37.5；Actor实例`moveSpeed=4.2`。狗bark/run距离8/6、速度6，等效玩家X=26.5/28.5；刺墙X=41，飞门X=44.9；逼近2.5、揭坑间距2.0、半落0.25、落箭10轨 `0.45/0.05/0.15` /InQuad、左飞0.6。
- 刺墙/升降陷坑节奏静态核对：`Level104Ctrl` 默认值与 `Level_104.prefab` 序列化值均为 `spikeWallStartActorX=41`、`spikeWallAdvanceSpeed=2.5`、`liftPitRevealWallGap=2`、`liftPitDropDuration=0.25`；`Update()` 在刺墙启动判断后调用 `UpdateLiftPitRevealFromSpikeWall()`；`PlaySpikeWallAdvance()` 不再直接 `PlayLiftPitCollapse()`；`NotifySpikeWallTouched()` 接受信号时若坑未触发则先揭坑。主体/刺墙 Collider 解析失败时间距揭坑不提前触发。
- 箭命中即时提交 Failure；刺墙接触只排队并在 `LateUpdate` 处理。
- 节奏可见性静态参考：相机代码在Actor越过X=10后以0.2s SmoothDamp追向角色中心；按16:9、60FPS与4.5u/s名义模拟，Actor X=16时相机已跟入第二屏；固定坑约[14.52,21.52]。Spine bark静态包络按SkeletonData 0.01与子节点0.8缩放约为狗根[-1.16,+0.74]，狗根X=20时bark名义右缘约20.74，因此X=12触发时完整可见；左侧轮廓约在ActorX=11.3入画，预看约0.15s。实际帧序、Spine裁切与玩家辨识仍为`uncertain`。
- `OnDisable`、`OnDestroy`、`GameResetEvent` 都进入本关清理路径。
- 升降陷坑失败冻结静态核对：控制器订阅 `OnTriggerTrapEvent` / `OnTriggerFailAreaEvent`，既有左移、箭命中和狗撞飞终态收口也统一调用 `FreezeLiftPitMovement()`；`Actor.IsDied` 提供兜底。冻结标志在 `PlayLiftPitCollapse()`、半落完成、延迟回升和回升完成入口均有门禁，`KillLiftPitTween()` 默认不补终点，因此源码层面保留当时 Transform、Collider 与坑沿显隐；没有调用音效停止接口。
- 仓库保留 Level 104 EditMode Domain 测试资产，但其部分飞门命名和旧轨迹断言已经过时；按当前项目约束未修改或执行。
- 2026-08-07 历史 1.5 屏调整：`wrapRightX` 11.3→21.5（C# 默认与 Prefab 覆写同步）、`wrapLeftX=-11.3` 不变；Prefab YAML 定向解析 28 项 PASS（新组件 fileID 引用可解析且无重复、地面碰撞 [-14.21,14.52]+[13.79,42.52] 连续、地面主体视觉在 X=14.12 对接、16:9 镜头行程 10.51、初始镜头在 16:9/2340×1080 下均不被夹移、白纱覆盖 [-15.65,22.35]（2026-08-07 二次修正：前版未计入父节点 X=-4.46 偏移，实际仅覆盖到 18.03）、狗/刺墙/箭/执法刺既有参数未动）。
- 2026-08-07 历史地面接缝二次修订（103 同款）：用户 preview 截图确认原 `Ground_long (1)`（X=14.35）原位复制拼接在 X≈14 处双侧端帽叠画出可见竖缝；改为 103 同款方案——延伸段换成 `Ground_short` 实例 `Ground_right_ext`（根 (17.14,-3)，顶面/底边中段 scaleX 1.861 posX 1.035、主体 scaleX 102.5 posX 1.265、碰撞 sizeX 9.85 offsetX 2.045，左端帽组三叶停用），原 `Ground_long` 右端帽组三叶（`Sp_ground_1_03`/`1_09`/`1_05`）停用。定向静态解析 42 项 PASS：锚点无重复、全部覆写目标存在于对应源 prefab、顶面 13.28 平直接续（差 0.003）、主体交叠 0.84、碰撞 [14.26,24.11] 与原碰撞右缘 14.52 交叠 0.26 且覆盖绕屏行走需求（≥22.16）、ext 右帽组 23.76 在锁定视野右缘 21 之外、无 `Ground_long (1)` 残留、`git diff --check` 干净。静态证据不能证明接缝观感已消除（发线/色差与 SortingGroup 绘制顺序需运行截图），待用户人工 preview。

- 2026-08-07 坑洞/箭阵/三件套调整：Prefab YAML 定向解析 39 项 PASS（fileID 无重复；门 18/狗 16/刺墙 19.4/星 2 (14,-2)；触发点 15.4/15.9；当时箭时长 0.4444；左岸碰撞右缘=2.32、右岸碰撞左缘=9.32；新实例、stripped Transform 与根 children 闭环；旧值零残留）；脚本 diff 逐行复核（狗触发位置化、箭序反转与最后落地箭登记完成修正、Domain 常量 15.9）；既有 EditMode 域测试全部符号化引用 `DoorFlyStartActorX`，与新常量一致。
- 历史三屏节奏重排当时的箭雨时长、错峰与缓动静态核对：`Level104Ctrl.arrowFallDuration/arrowStaggerDelay` 默认值和 `Level_104.prefab` 序列化值均为 `1.5 / 0.18`，落箭 `DOMoveY` 明确设置 `Ease.InQuad`；当时五轨整轮 `1.5+4×0.18=2.22s`。该五轨/`1.5s` 口径现已被 2026-08-13 追逐箭阵扩 12 轨覆盖。
- 本次追逐箭阵收为 10 轨静态核对：源码 `ArrowWorldXs` 恰为 10 条字面量 `-0.18 / 0.82 / 1.82 / 2.82 / 3.82 / 4.82 / 5.82 / 6.82 / 7.82 / 8.82`；门槛常量 `ArrowVolleyTriggerActorX=-3.68f`（最左轨左侧 3.5），不再当第一轨、也不等于覆盖区左界 `-0.18`；源码默认值与 Prefab 序列化值均为 `arrowFallDuration=0.45`、`arrowStaggerDelay=0.05`、`arrowFadeDuration=0.15`；Prefab `arrows` 10 个 stripped Transform 与 `ArrowVolley` children 同序闭合，Arrow_10/11 已删除；落箭 `DOMoveY` 仍明确设置 `Ease.InQuad`；整轮 `0.45+9×0.05=0.90s`。该口径覆盖同日 12 轨 `-0.18…10.82` 与整轮 `1.00s`。狗/坑/刺墙/飞门点位未随本次扩轨回滚。
- 箭雨尾随静态核对：Prefab 的 `arrowTrailMaterial` 精确引用 Level 078 `Level078ArrowTrail.mat` GUID `4be79cb1e8e64aa8a8d5e2b4f93c7801`，不修改该材质；当前 `arrowTrailTime/Width` 源码默认值与 Prefab 序列化值均为 `0.1 / 0.2`，Alpha 头为 `0.65`。其余仍与 078 创建逻辑一致：Sprite 负 X 半长锚点、暖白 `(1,0.94,0.82)`、线性宽度、`minVertexDistance=0.04`、端帽/拐角 2、View/Stretch、关闭阴影和 Sprite SortingOrder 减 1。下落 `OnStart` 启用、接地停止生成、初始化和统一 Kill 路径停止并清空的调用链存在。该口径覆盖短暂放大 `width=0.3 / Alpha 0.95 / time=0.3s` 与旧同款 `0.1 / 0.1 / 0.65`。
- 箭命中反馈静态核对：`Level104TrapTrigger` 对狗和箭只计算一次 `CalculateContactPoint`，箭分支调用 `NotifyArrowHit(contactPoint)`；控制器只在 Domain 接受首次命中后移动并播放 `arrowHitEffect`，再依次播放 `arrowHitSfxTag=trap`、`failed` 和派发既有死亡事件，Teardown 会停止并清空粒子。Prefab 新增独立 `Level104ArrowHitEffect`，源 GUID 与既有 `Knock_Explosion.prefab` 一致，ParticleSystem 引用、根 children 与控制器字段闭合，不复用小狗或执法刺实例。2026-08-13：`SpikeStoneImpact_104` / `Level104DogKnockEffect` / `Level104ArrowHitEffect` 三处实例均覆盖子节点 `Knock 1`（fileID `5393762992232218095`）为 `m_IsActive=0`，共享源 Prefab 未改。

- 2026-08-12 三屏扩展静态核对：`CameraFollowRange` center (19.75,-2)、size 65.5×20，窗口 `[-13,52.5]`；`wrapRightX` / `spikeWallExitX` 均为 53.1；白纱覆盖约 `[-15.66,69.5]`；新增远景 X=59.414 与 4 个 +21 装饰复制件父子引用闭合；YAML 锚点唯一。该证据不证明 Unity 导入和实际视觉观感。
- 右岸地面拼接（废止拉伸；接缝/坑沿返工）：`Ground_lift_pit_right` 碰撞 `[20.2452655,27.8670105]`，关 `Group_right`，顶/底 mid localX=2.2，`Sp_ground_1_02` 无 inactive 覆写；`Ground_long (1)` fileID `9001040000000000800`/stripped `9001040000000000801` 根 X=27.3271555，碰撞 `[26.7670105,55.4958205]`，接缝交叠 1.1，右缘 ≥53.7；名与 `Group_left` 三叶仍在；`wrapRightX`/`spikeWallExitX` 53.1 未改。该证据不证明接缝观感与 Unity 导入。
- 2026-08-07 两屏连续场景静态核对：`CameraFollowRange` center (10.5,-2)、size 42×20，对应窗口 `[-10.5,31.5]`；`Ground_right_ext` 碰撞覆盖 `[14.26,32.7]`，`wrapRightX` / `spikeWallExitX` 均为 32.1；白纱覆盖约 `[-15.66,48.5]`，第二份 `CommonBg` 位于 X=50，两段远景位于 X=12.598/36.006，5 个第二屏装饰的父层级、坐标、素材与引用闭合。新增 YAML 锚点无重复，`git diff --check` 通过。该证据不证明 Unity 导入和实际视觉观感。
- 2026-08-07 坑洞左沿封边修正：`Ground_long` 实例 `Group_mid_3` 顶/底边条（`Sp_ground_1_07`/`1_06`）posX→2.4432/2.4482、scaleX→0.6734，条带右端收至与端帽左沿齐平（静态差 0.00006），不再越过当时坑沿 2.32 伸入坑口；碰撞链未动。
- 2026-08-08 坑洞与平台整体左移 1.5：定向解析确认左岸 Collider 外侧左缘仍为 -14.21015、右缘为 0.81995，右岸 Collider 左缘为 7.8200001，坑宽约 7；`Ground_pit_right` 根 X=8.32，`Plantform_pit` / `FallArea_Pit` / `PitBackdrop` 中心 X 均为 4.32。上述整体平移节点的 Y、缩放与自身 Collider 尺寸保持不变；左岸右帽、末段条带、主体和 Collider 则按新坑沿同步收短，其他玩法节点未随本次偏移修改。
- 升降陷坑静态核对：`Level104Ctrl` 显式绑定 `Ground_lift_pit_top`，参数仍为完整距离4.8、等待5秒、半落/回升0.25秒、自动上跳28；`liftPitActorClearance=0.15`、`liftPitRevealWallGap=2`、`HasActorClearedLiftPit()` 与玩家 X=41 刺墙候选门槛均保持。整组再右移 2.5 后实时坑口为 `[36.2547345,38.7452655]`、中心约X=37.5、宽2.490531；`liftPitCenterX` 源码默认值与 Prefab 序列化值均同步为37.5。Actor PolygonCollider2D 最左点为 -0.5519，中心 X=41 时左缘约40.4481，较坑右缘约1.7028、扣除0.15后静态余量约1.5528。自动上跳代码仅保留异常掉入防软锁，不作为躲墙解法。
- 升降陷坑坑沿与坑内凸起静态核对：坑口为 `[37.7547345,40.2452655]`，`Sp_em_1_02 (2)` 触发前世界中心 `(38.80,-3.42)` 位于其中；其新父级、局部 `(0.485122,-0.42,0)`、组内 SortingOrder=1、嵌套 Prefab 新增子节点登记与旧 `zhiwu` 子引用移除闭合。坑口外同素材节点中心约 X=17.404878，父级和坐标未改。右岸继续继承共享 `Ground_short` SortingGroup 0，左岸/升降块/两屏纸张 Mask 排序为 `21/22/23`，覆写目标与 Level 103 同款源引用一致；243 个 YAML 锚点唯一，`git diff --check` 通过。该证据只证明序列化结构、名义世界位置和数值排序。
- 左岸按钮静态核对：`Level104Ctrl` 序列化绑定 `PitPlatformRevealButton`，门槛钉死为绝对世界 `X=4.5`（源码默认值与 Prefab `pitPlatformButtonRevealActorX=4.5`）；源码存在一次性门闩、初态 Renderer/Collider 关闭、0.6 世界单位/0.3 秒 `OutBack` 冒出、完成后开放碰撞及销毁/终态 Kill Tween 调用链。Prefab 实例源为 `SceneCommonButton.prefab` GUID `ed8eb6512c19d204f9390c7e39169a60`，顶部 Sprite 覆写为 `Sp_button_1_01.png` GUID `f20819c899c40eb4aa0c896c546fb107`，终态 `(-2.5,-2.41)`。该证据只证明源码、引用和序列化几何。

### missing evidence

- 关卡标题占位的 I2 Awake 刷新、`S_LevelTips_Des_level_83` 各语言实际文字网格、TMP 换行/截断/RTL、`SceneLayer/100` 与中前景及原出口组合的实际合成、不同横屏比例/HUD 下的构图，以及玩家是否把“动人心弦”误认成 Level 104 正式标题；本次没有 Unity 或人工预览证据。

- 出口方向引导组合在 Unity 中的实际可见性、纸张与 Midscene 遮挡关系、箭头 Alpha 边缘、I2 各语言实际取值、TMP 字形/换行/截断/RTL/缺失翻译回退、不同横屏比例与 HUD 下的位置和留白、文字与箭头的相对构图，以及与竖直追逐箭阵的辨识度；本次没有 Unity 或人工预览证据。

- Unity 编译和 Prefab 导入结果。
- 玩家中心在 `X=-3.68` 触发箭阵时的首次可见帧、新 10 轨 `0.45s / 0.05s / 0.15s / Ease.InQuad` 下落与错峰的追逐观感、Actor 4.5 u/s 从 `-3.68` 走到覆盖区左界 `-0.18` 约 0.78s 时右端几支是否已落地渐隐、Level 078 同款尾随的实际朝向/长度/透明度/层级/10 支 overdraw、与出口方向引导箭头水平重叠的叠画、箭命中粒子是否落在感知接触点、`trap + failed` 同帧音量层次和粒子在失败切换前的可见时长、接地与重置是否无残线、Collider 接触、伤害窗口及与坑洞/小狗/刺墙的实际时序。
- 现有 EditMode 资产在新独立启动合同下的语义一致性；按当前项目约束，本次未修改或执行测试。
- 既有 `Assets/Editor/Level104HappyPathProbe.cs` 仍硬编码历史 `X≥5.5 / 6.5 u/s` 刺墙合同，不能作为当前 `X≥38.5 + 完整越坑 / 2.5 u/s` 与间距揭坑的验证证据；按当前项目约束，本次保留该历史探针且不修改、不执行。
- 正解、左移、箭命中、小狗击飞、同帧冲突、门落定和失败重开实机日志；飞门在当前相机跟随与不同横屏比例下的实际入画位置、`Y=3.545` 高位构图、1 秒左飞（54.51 世界单位水平路程）的方向感知、三段转向衔接、扇翅与门体同步，以及落定后 Trigger 恢复时机。
- Android/iOS 横屏、异形屏、触控与低帧率证据。
- 相机跟随平滑、绕屏同帧瞬移、三屏地面/白纱/背景/远景/装饰拼接观感与宽屏表现；待用户人工 preview。
- 本次升降陷坑死亡冻结的实际帧：下降、5 秒等待、回升和未触发四种状态下是否都保持当时高度、实心承托和坑沿显隐，已开始音效是否自然收尾，以及失败重开后是否恢复初始状态；本次只有静态调用链证据。
- 本次升降陷坑坑沿与坑内凸起：左右竖边在实际半落/回升画面中是否始终只显示到升降块顶面、两屏纸张纹理是否维持原质感、唯一坑内小凸起是否贴地并与地块同步运动，均缺少修正后运行证据；用户截图只证明修正前左线穿透和凸起未随动。
- 狗bark/run、刺墙/间距揭坑、飞门在X=26.5/28.5/41/44.9下的实际首个可见帧、双门槛与0.4秒停留观感、声波特效、星星仍X=14与新run门槛分离、狗跳躲落点、飞门与刺墙名义接触几乎同拍及后续完整流程均缺少运行证据。
- 2026-08-07 新增：+3 高台上跳手感与起跳窗口（当前4.5 u/s静态推算坑沿前 ≈0.85）、坑沿/坑壁/坑底视觉观感、小狗相对距离门槛下的实际反应窗体验、从右往左落箭演出、坑底 FallArea 判死时序、星 2 与狗触发同帧的拾取表现；2026-08-08 左移后的坑沿构图、平台跳跃时机、碰撞与 FallArea 实际触发仍缺少运行证据。
- 本次左岸按钮：玩家中心到达 `X=4.5` 时是否同帧冒出、绕屏后是否仍被一次性门闩挡住、按钮地下阶段是否被地板正确遮挡、0.3 秒 `OutBack` 冒出观感、完成帧 Rigidbody2D/Collider 同步、玩家若接触按钮时共享下压/回弹与 `boardTrigger` 音效是否正确，均未取得运行证据。

## 回滚路径

仅回退本次关卡标题占位时：从 `Level_104.prefab` 移除 `LevelTitlePlaceholder` PrefabInstance、stripped RectTransform 和根 child，并同步恢复 Context、PRD、功能说明与风险评估；完整保留 `ExitDirectionGuideArrow`、`ExitDirectionGuideText` 及其 `S_LevelTips_Des_level_15_02`、位置和排序，不得修改共享 `Tips.prefab`、`I2Languages.asset` 或 Level 083。

仅回退本次小狗双门槛与停留调整时：删除两个控制器的 `dogBarkTriggerDistance` 与新增表现状态，让 bark 恢复在原 run 距离门槛启动，并把 `dogPostBarkHold` 恢复为0.25；从两个Prefab删除bark距离字段并恢复停留值，同步Context、PRD与功能/风险说明。狗位置、原run距离、速度、终点、碰撞和其它机关均不回退。

仅回退本次新增的出口方向引导文案时，从 `Level_104.prefab` 移除 `ExitDirectionGuideText` PrefabInstance、对应 stripped RectTransform 与根 `m_Children` 引用，并同步恢复 PRD、Context、功能说明和风险评估。保留 `ExitDirectionGuideArrow`；共享 `Tips.prefab`、`I2Languages.asset`、Level 15 与其它关卡内容均不回退。

回退完整出口方向引导组合时，同时移除 `ExitDirectionGuideArrow` / `ExitDirectionGuideText` 两个 PrefabInstance、对应 stripped Transform/RectTransform 与根引用，并同步恢复相关文档。共享 `Arrow_02.prefab`、`Sp_arrow_1_03.png`、`Tips.prefab`、I2 词条、追逐箭阵、固定坑、相机和其它机关均不回退。

仅回退本次小狗叫喊特效与音量提升时：从 `Level104Ctrl.cs` 删除 `dogBarkEffect`、`dogSfxVolume` 与显隐调用，把狗叫恢复为 `AudioManager.PlaySound(dogSfxTag)`；从 `Level_104.prefab` 移除 `DogBait/body` 下的 `DogBarkEffect` 嵌套实例及控制器接线。不得修改共享 `Noise/Bark.prefab`、`bark.ogg`、SoundsGood 配置、bark Spine 动画、0.4 秒停留、碰撞门禁、狗速、撞飞或其它机关；同步恢复 PRD、Context 与本目录两份说明。

仅回退本次刺墙与飞门时长调整时，把 `Level104Ctrl.spikeWallAdvanceSpeed` 默认值及 `Level_104.prefab` 序列化值从 `4` 恢复为 `6.5`，把 `Level104FlyingDoorCtrl.flyLeftDuration` 默认值及 Prefab 序列化值从 `1` 恢复为 `2`；`spikeWallBounceSpeed=8`、触发门槛、坑联动、飞门其它轨迹和其它机关均保持不变。

仅回退本次小狗过坑下落时，删除 `dogPitDropX` / `dogPitFallY` / `dogPitFallDuration` 与 `StartDogPitFall()`，恢复 `StartDogRun()` 对 `dogExitX=-12` 的锁 Y `DOMoveX`；不回退狗根、门槛、速度、撞飞或其它机关。
仅回退本次箭阵破空音从发声点起播时，删除 `arrowWhooshClip`、`PlayArrowWhooshSound()` 与音源池，恢复下落 `OnStart` 的 `AudioManager.PlaySound(arrowSfxTag)`；不修改 `arrowWhoosh.ogg`、078 或其它机关。
仅回退本次升降坑再左移 2、刺墙门槛再左移 1、飞门改门左 3 / 左飞 1 秒时，把 `liftPitCenterX` / 升降坑组根坐标 / `spikeWallStartActorX` / `DoorFlyStartActorX` / `flyLeftDuration` 从 `35 / 现坐标 / 38.5 / 43.9 / 1` 恢复为 `37 / 任务前坐标 / 39.5 / 52.5 / 1.5`；门实体、狗、固定坑和箭阵不回退。
仅回退本次升降坑与刺墙门槛左移 2 时，把 `liftPitCenterX` / 升降坑组根坐标 / `spikeWallStartActorX` 从 `37 / 现坐标 / 39.5` 恢复为 `39 / 任务前坐标 / 41.5`；门、狗、固定坑和箭阵不回退。
仅回退本次左岸按钮门槛钉死为 `X=6` 时，把 `pitPlatformButtonRevealActorX` 恢复为按最右轨左侧 1 计算（当时 `7.82`）；按钮世界坐标、冒出距离/时长、狗/坑/箭阵不回退。
仅回退本次左岸按钮改为箭阵右沿左侧 1 触发时，恢复 `IsActorStandingOnPitPlatform()` 落地组合条件与 `pitPlatformLandingTolerance=0.2`，删除相对最右轨偏移字段；按钮世界坐标、冒出距离/时长、狗/坑/箭阵不回退。
仅回退本次小狗再右移 2 个世界单位时，把 `DogBait` 根 X 从 31 恢复为 29；相对距离 8/6 会使绝对门槛从 23/25 回到 21/23。坑、星、刺墙、飞门和箭阵不回退。
仅回退本次小狗右移 5 个世界单位时，把 `DogBait` 根 X 从 29 恢复为 24；相对距离 8/6 会使绝对门槛从 21/23 回到 16/18。坑、星、刺墙、飞门和箭阵不回退。
仅回退本次箭雨尾随改为 `width=0.2`、Alpha `0.65` 时，把 `arrowTrailWidth` 恢复为 `0.3`、Alpha 头恢复为 `0.95`；残留 `0.1s`、10 轨、门槛、时长不回退。
仅回退本次箭雨尾随残留改回 `0.1s` 时，把 `arrowTrailTime` 默认值与 Prefab 序列化值恢复为 `0.3`；当时宽度 `0.3`、Alpha `0.95`、10 轨、门槛、时长不回退。
仅回退本次箭雨尾随放大时，把 `arrowTrailTime/arrowTrailWidth` 默认值与 Prefab 序列化值恢复为 `0.1/0.1`，并把 Alpha 头恢复为 `0.65`；不修改 Level 078 材质，10 轨、门槛、时长、狗/坑/刺墙/飞门不回退。
仅回退本次门槛改为最左轨左侧 3.5 时，把 `ArrowVolleyTriggerActorX` 恢复为 `-5.18f`，并同步当前文档；10 轨、时长、狗/坑/刺墙/飞门不回退。
仅回退本次删最右两支收为 10 轨时，把 `ArrowWorldXs` / Prefab `arrows` 恢复为 12 轨 `-0.18…10.82` 并补回 Arrow_10/11，整轮口径恢复为 `1.00s`，并同步当前文档；时长 `0.45 / 0.05 / 0.15`、当时门槛、狗/坑/刺墙/飞门不回退。
仅回退本次箭阵时长再标定 `0.45 / 0.05 / 0.15` 时，把 `arrowFallDuration/arrowStaggerDelay/arrowFadeDuration` 默认值与 Prefab 序列化值恢复为 `0.8 / 0.08 / 0.3`，整轮口径恢复为 `1.68s`，并同步当前文档；当时 12 轨、门槛、狗/坑/刺墙/飞门不回退。
仅回退本次相邻错峰 `0.18→0.08` 时，把 `Level104Ctrl.arrowStaggerDelay` 默认值与 `Level_104.prefab` 序列化值恢复为 `0.18`，整轮口径恢复为 `2.78s`，并同步当前文档；12 轨、单支 `0.8s`、落地 `0.3s` 渐隐、狗/坑/刺墙/飞门不回退。
仅回退本次追逐箭阵扩 12 轨时，把 `Level104Ctrl.ArrowWorldXs` / Prefab `arrows` / 时长恢复为五轨 `-5.18…-1.18`、`arrowFallDuration/arrowStaggerDelay/arrowFadeDuration=1.5 / 0.18 / 0.5`，删除 Arrow_5..11；门槛常量仍可保持 `-5.18`，狗/坑/刺墙/飞门点位不回退，并同步当前文档。
仅回退本次三屏节奏重排时，恢复 `spikeWallAdvanceSpeed=4`、`liftPitDropDuration=0.3`、同帧 `PlayLiftPitCollapse()`、`arrowFallDuration/arrowStaggerDelay=1.6/0.1` 与 `Ease.Linear`、`DoorFlyStartActorX=26.5`、`flyLeftDuration=1`，移除间距揭坑字段与路径，并同步当前文档；狗/三屏/wrap 不回退。
仅回退本次第三屏整组平移时，把门根/`rightFlightX`/`SpikeWall`/`liftPitCenterX`/`spikeWallStartActorX`/`DoorFlyStartActorX` 从 `46.9/46.9/48.3/39/41.5/52.5` 恢复为 `26.9/26.9/28.3/19/21.5/32.5`，移除 `Ground_short (2)/(3)/(4)/(5)`，并把升降坑左右岸根坐标恢复为任务前值；狗、箭阵、固定坑、三屏相机与 `wrapRightX` 不回退。
仅回退历史尖刺墙共享联动门槛再左移1.5个世界单位时，把 `Level104Ctrl.spikeWallStartActorX` 默认值与 `Level_104.prefab` 序列化值从21.5恢复为23，并同步当前文档与Domain注释；Actor完整越坑0.15门禁、墙体速度/反弹、飞门及其它机关均不回退。

仅回退本次尖刺墙共享联动门槛左移1个世界单位时，把 `Level104Ctrl.spikeWallStartActorX` 默认值与 `Level_104.prefab` 序列化值从23恢复为24，并同步当前文档；Actor完整越坑0.15门禁、升降陷坑同帧联动、墙体与陷坑参数、飞门及其它机关均不回退。

仅回退本次升降陷坑坑沿与坑内凸起修正时，将 `Sp_em_1_02 (2)` 从 `Ground_lift_pit_top` 移回 `Frontscene/zhiwu`，恢复局部 `(18.33,-2.92,0)`、SpriteRenderer SortingOrder=0，并移除升降块 PrefabInstance 的新增子节点登记；把 `Ground_lift_pit_top` SortingOrder 从22恢复为1，删除 `Ground_lift_pit_left=21` 与两屏 CommonBg 纸张 Mask=23覆写。Collider、坑体几何、六节点显隐、Tween、C#与其它机关均不回退。

仅回退本次升降陷坑左移3个世界单位时，把 `Ground_lift_pit_top` / `Ground_lift_pit_safe` 锚点 X 从18.314878恢复为21.314878，把 `liftPitCenterX` 源码默认值与 Prefab 序列化值从19恢复为22，并恢复 `Ground_lift_pit_left/right` 坑向端帽、顶/底条带、主体与 Collider 的本次横向覆写，使坑口回到 `[20.7547345,23.2452655]`、地面外边界继续保持14.26/32.7。`spikeWallStartActorX=21.5`、`liftPitActorClearance=0.15`、下降/回升参数、C#控制流、Domain、狗与其它机关均不回退。

仅回退本次小狗右移时，把 `Level_104.prefab` 的 `DogBait` 根X从20恢复为19；当前bark/run相对距离8/6会使绝对门槛从12/14恢复为11/13。bark、0.4秒停留、速度、终点、碰撞、撞飞、坑、星星和其它机关均不回退。

仅回退本次小狗 bark 后停留时，删除 `Level104Ctrl.dogPostBarkHold` 与 `StartDogRunAfterBarkHold()`，让完成回调恢复直接调用 `StartDogRun()`，并从 `Level_104.prefab` 删除 `dogPostBarkHold: 0.4`；狗的位置、触发距离、bark 动画、速度、碰撞、撞飞和其它机关均不回退。

选择性回滚 Level 104 的 Prefab、脚本目录、飞门动画资源和本地文档即可。仅回退本次Actor降速时，删除`Level_104.prefab`中Actor PrefabInstance的`moveSpeed=4.5`覆写，使其恢复继承共享默认值5；共享Actor、其他关卡、跳跃参数和104机关参数均不回退，并同步当前文档。仅回退本次身后假坑节奏时，删除 `liftPitActorClearance` 与 `HasActorClearedLiftPit()`，把 `spikeWallStartActorX` 默认值/Prefab 值从24恢复为20，并把 Domain `DoorFlyStartActorX` 从26.5恢复为24.5；狗、坑几何、速度、碰撞、Tween和飞门轨迹均不回退。仅回退本次节奏重排时，把 `DogBait` 根 X 从 19 恢复为 24.9，把 `dogChargeTriggerDistance` 默认值与 Prefab 序列化值从 6 恢复为 7.9，并把 Domain `DoorFlyStartActorX` 从 24.5 恢复为 19.4；刺墙/陷坑 X=20、门与刺墙实体、速度、碰撞、Tween 和各机关逻辑均不回退，同时同步 PRD、Context、开发记录与两份说明。仅回退本次最终门槛调整时，把 `dogChargeTriggerDistance` 默认值与 Prefab 序列化值从 `7.9` 恢复为 `6`，把 `spikeWallStartActorX` 从 `20` 恢复为 `15.9`，并恢复 Domain 对 `DoorLanded` 的刺墙窗口限制；同步恢复 PRD、Context 与两份说明，狗根、飞门门槛与轨迹、刺墙实体与速度、箭阵和升降陷坑均不回退。仅回退本次触发点交换时，把 `Level104Ctrl.spikeWallStartActorX` 默认值与 Prefab 序列化值从 `15.9` 恢复为 `19.4`，并把 Domain `DoorFlyStartActorX` 从 `19.4` 恢复为 `15.9`，同步恢复 PRD、Context 与两份说明；刺墙实体坐标、速度、飞门轨迹、小狗、箭阵和升降陷坑均不回退。仅回退本次 Level 104 小狗降速时，把 `Level104Ctrl.dogRunSpeed` 默认值和 `Level_104.prefab` 序列化值从 `8` 恢复为此前 `15`，同步恢复 PRD、Context 与两份说明；bark 前摇、位置门槛、碰撞、终点、撞飞、103 与其它机关不回退。仅回退本次 103/104 小狗叫喊前摇中的 104 部分时，删除 `Level104Ctrl` 的 bark TrackEntry 等待与 `StartDogRun()` 拆分，恢复触发同帧武装碰撞、切 run、播放 bark 音效和创建冲锋 Tween；Prefab、Spine/音效资源、坐标、速度、Domain、103 与其它机关不回退。仅回退本次飞门可见左飞时，把 `Level104FlyingDoorCtrl` 恢复为垂直完全离屏后单帧写入 `leftDoorX` 的旧路径，把 Prefab 的 `flightHeight/flyLeftDuration` 恢复为 `offscreenPadding=0.2` 并移除水平 Tween，恢复 Domain 旧阶段语义；门的初始/落地坐标、启动点、翅膀与进门门禁不变。仅回退本次门位对齐与刺墙触发点调整时，把门根/`rightFlightX`/狗根/刺墙根/`spikeWallStartActorX` 从 `26.9/26.9/24.9/28.3/19.4` 恢复为 `29/29/27/30.4/15.4`，其余 Prefab、脚本逻辑和关卡参数不变。仅回退本次单箭 `1.6s` 时长调整时，把 `Level104Ctrl.arrowFallDuration` 默认值与 `Level_104.prefab` 序列化值恢复为此前 `1.3`，保留 `0.1s` 错峰、`Ease.Linear`、尾随、碰撞、渐隐和命中反馈，并同步 PRD、Context 与两份说明。仅回退本次箭雨尾随时，删除 `Level104Ctrl` 的 `arrowTrail*` 字段、运行时字典及创建/启停/清理方法，移除下落开始、接地、初始化和 Tween 清理处的尾随调用，并从 Prefab 删除材质与 `0.1/0.1` 接线；保留当前 `1.6s / 0.1s / Ease.Linear` 标定和箭命中反馈，不修改 Level 078 材质、共享箭 Prefab、箭轨、碰撞或 Domain。仅回退本次箭雨时长与错峰调整时，把 `Level104Ctrl.arrowFallDuration/arrowStaggerDelay` 默认值与 Prefab 序列化值恢复为此前 `0.75/0.15`，保留 `Ease.Linear`、尾随和命中反馈，并同步 PRD、Context 和两份说明。仅回退本次箭命中反馈时，把 `Level104TrapTrigger` 箭分支恢复为无接触点参数调用，从 `Level104Ctrl` 删除 `arrowHitEffect/arrowHitSfxTag`、粒子/`trap` 播放和清理，移除 Prefab 的 `Level104ArrowHitEffect` 实例、根 children、stripped 引用及控制器接线；保留 `failed` 和标准死亡链，不修改共享粒子、小狗或执法刺实例。仅回退本次左岸按钮时，删除 `Level104Ctrl` 的 `pitPlatform*` 字段、运行状态与 Pit Platform Button 区域，移除 Prefab 的 `PitPlatformRevealButton` 实例、stripped Transform、根 children 与控制器引用，并同步恢复 PRD、Context 和两份说明；不要修改共享 `SceneCommonButton.prefab`、`PressButtonBase` 或 `Sp_button_1_01.png`，也不要回退固定坑、箭阵、升降陷坑或 Domain。仅回退本次升降陷坑对齐时，删除 `Level104Ctrl` 的 `liftPit*` 字段、运行状态和 Lift Pit 区域，移除 Prefab 对 `liftPitGroundSegment` 与参数的接线，把右侧 `Sp_ground_1_02` 恢复为原初态，并恢复两岸顶/底条带及右岸主体的任务前覆写；`Ground_lift_pit_top/safe/left/right` 静态结构和固定致死坑保留，同时恢复 Context 的“待接线”合同。仅回退本次箭阵独立触发：恢复 `Level104Ctrl` 在小狗到 `X=-12` 的完成回调中启动箭阵、恢复 `Level104DontLookBackRule.TryStartArrowVolley()` 的 `DogHasExited` 前置与小狗 `Entrance` 阶段门禁，并同步恢复 PRD、Context 与两份 Level 104 说明；不改箭轨、Prefab、刺墙、飞门、坑洞或绕屏。仅回退 2026-08-08 坑洞左移：把 `Ground_pit_right` 根 X 恢复为 9.82，`Plantform_pit` / `FallArea_Pit` / `PitBackdrop` 中心 X 恢复为 5.82，并把左岸右帽、末段条带、主体与 Collider 恢复为 8.8033、2.4432/2.4482、0.6734、165.301/7.7049、16.5301/7.7049；不得回退箭阵、狗、刺墙、飞门或两屏场景。仅回退本次两屏场景扩展：把 `CameraFollowRange`、`Ground_right_ext` 的拉伸覆写、白纱、`wrapRightX` / `spikeWallExitX` 恢复到任务前数值，并移除新增 `CommonBg (1)`、两段远景和 5 个第二屏装饰；不回退既有相机组件、绕屏机制、玩法节点或其它关卡内容。仅回退 2026-08-07 坑洞/箭阵/三件套调整：还原本轮 Prefab/脚本 diff（三件套与星 2 坐标、触发点、`ArrowWorldXs`、箭时长与顺序、狗触发入口、`DoorFlyStartActorX`），移除 `Ground_pit_right`/`Plantform_pit`/`FallArea_Pit`/`PitBackdrop` 四个新增块及其 stripped Transform/根 children 条目，并恢复 `Ground_long` 右帽组三叶停用覆写、删除收短覆写。当前没有 Level 104 配置、存档字段或玩家数据需要迁移；不要整体回滚 `a63eb6d20`，否则会同时移除其他关卡和资源。
