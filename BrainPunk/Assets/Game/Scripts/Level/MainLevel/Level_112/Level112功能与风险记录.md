# Level112「另辟蹊径」功能与风险记录

## 需求与实现背景

- 本关的正解是穿过中央柱子中间的 `Square`：遮挡块默认显示，Actor 不被它阻挡；Actor 进入下半块内的指定区域后遮挡块渐隐，离开后渐显恢复；箱子必须被挡在外面，不能推进暗道。
- 左侧拉环是误导路线。玩家可把拉环向下拉，松手后拉环以发条式脉冲速度缓慢回卷；Actor 靠近正在回卷的拉环后挂上，并随拉环持续升高、在画面中逐渐变远。
- 升空表现参考 Level125 的 `hang` 动作与挂环偏移，天空层次参考配置 `10013 -> Level_013` 的云、星和月亮；夜色渐变沿用项目既有夜色值。
- 2026-09-04 试玩反馈要求：Actor 不得对暗道墙播放推墙、必须能实际进入；拉环升空不得横移或抖动；挂住后必须有动作；背景横条纹必须固定在世界背景上，不能粘着相机移动。
- 2026-09-04 后续试玩反馈要求：Square 不能在角色仅碰到外沿时就显形；吊环整体还需向下调整；进入夜空后必须继续清楚保留纸面横条纹。
- 2026-09-04 最新试玩反馈要求：误导路线不能在高空停住；吊环与 Actor 需持续上升，云、星星、月亮等天空元素需在上方无限循环，同时不能用持续实例化制造性能压力。
- 2026-09-04 最新修正：月亮只能出现一次，不参与无限循环；持续回收仅适用于云和星星。
- 2026-09-04 最新截图再次确认横纹在镜头升到约 Y=25 后从画面上半部截断；该边界与 CommonBg 内嵌 Mask 默认高度 50 的顶部完全吻合，说明仅靠 Level112 Prefab 对两层嵌套来源写 `m_Size.y=160` 未能可靠作用于当前运行实例。
- 2026-09-04 最新手感反馈：拉环松手后起步过快，玩家经常来不及跳上去；最低限位还需再向下开放一点，但仍须保留起跳要求。
- 2026-09-07 用户把 `Square` 更换为 `Sp_l112_img_02` 后，原有约 1×1 的两份 Collider 已明显小于新图；截图黄色标注要求碰撞范围横向贴近下半块左右边缘、底部贴底、顶部下移到下半块顶边，不覆盖上方两根立柱。
- 2026-09-07 最新显隐反馈要求反转原逻辑：遮挡块默认显示，Actor 真正进入黄色框后渐隐，离开黄色框后渐显；显隐 Trigger 另按最新截图的黄色框收窄高度并向地面区域对齐。
- 2026-09-07 最新截图确认显隐前后的两张柱体素材下沿都低于左右地面线，且两种状态的偏差不同；必须分别校正底层 `Sp_l112_img_01` 与上层 `Square`，避免只修一层后在渐隐切换时再次露出错位底边。
- 2026-09-07 最新试玩反馈确认角色靠近右门后无法通关；本关不需要钥匙或其它前置条件，必须沿用右门既有 Trigger 直接进入标准成功流程。

## 原始基线

- `Assets/Game/Prefabs/Level/Level_112.prefab` 是用户已摆放、尚未被 Git 跟踪的关卡 Prefab，已有 Actor、箱子、中央 `Square`、左右门、CommonBg、GameCamera 和左侧拉环素材。
- `Square` 原来只有 SpriteRenderer，没有碰撞体或显隐脚本；拉环只有 SpriteRenderer、CircleCollider2D 和一份失效的 Level125 接触组件引用，没有可用的下拉/回卷流程。
- 首轮实现把 `Square` 放在 Ground Layer；虽然实体 Collider 排除了 Actor，ActorCtrl 独立的 Ground 墙检测 Raycast 仍会命中它，导致移动被截断并播放 `push`。首轮还让 CommonBg 根节点逐帧跟随相机，并在 FixedUpdate 用 Rigidbody2D.MovePosition 追随拉环；相机变焦会让左侧拉环视觉上向画面中心漂移，跨更新阶段也可能产生抖动。
- 2026-09-04 第二轮试玩确认首轮修正仍未打通入口。进一步核对 Prefab 继承关系后发现，`Square` 的父 Prefab `Ground_short 1` 根节点还额外添加了一块 Ground Layer 的 BoxCollider2D（旧 fileID `2918326949231021970`，offset `(0.676,-2.000)`，size `(2.471,1.873)`）；它完整覆盖柱子下半段，Actor 在接触 `Square` 前已经被该外层碰撞挡住。此前只修改 `Square` Layer/Collider，因此无法解决实际堵路。
- 首轮 Actor 使用公共 `sp_box_001` 骨骼，该资源没有 `hang`；Editor.log 已记录两次 `[Level112ActorCtrl] Animation not found: hang`。Level125 实际覆盖为 `sp_box_025/Box_SkeletonData.asset`，该骨骼包含 `hang`。
- 标题 Tips 原先仍是复制来源的本地化文本，且 Luban 的 `LevelSort.xlsx/defaultSort` 与 `LevelData.xlsx/level` 中没有 Level112。本次补入两份 Excel 源配置；未修改 Luban 生成 JSON、共享 Actor Prefab、共享 `ActorCtrl`、公共相机或 GF 子模块。

## 新行为与系统边界

- 删除 `Ground_short 1` 实例上覆盖柱子下半段的额外 Ground Collider，只保留柱子上半段原 Collider；`Square` 固定到 TriggerItem Layer 9。Level112 私有 Actor 的 `whatIsWall` 同时设为 0，不再对柱体播放墙推状态。`Square` 运行时保留素材原有可见 alpha；Actor 根节点进入显隐 Trigger 的实际水平边界后以 0.25 秒渐隐，离开后从当前 alpha 以 0.25 秒渐显恢复。Stuff Layer 8 只与独立实体 Collider 碰撞，因此箱子被挡住且不会触发显隐，遮挡 Renderer 关闭时也不影响箱子阻挡。
- 新图适配后，Stuff 实体 Collider 本地范围保持 `size=(2.18,1.36) / offset=(0,-0.21)`，对应当前非等比 Transform 下约 `2.58×2.25` 世界单位；Actor 显隐 Trigger 按最新黄色框改为 `size=(1.82,1.02) / offset=(0,-0.21)`，约 `2.15×1.69` 世界单位，并把 `revealHorizontalInset` 设为 0，使 Actor 中心进入黄色框实际边界即触发。两者 Layer include/exclude 与 Trigger 属性仍严格分流。为避免 Unity Auto Save 把外部 YAML 调整还原为旧的 1×1 范围，Collider 尺寸作为本关组件字段保存，并在 `OnValidate` 与 `Awake` 同步到实际 Collider；Scene 绿色框、Prefab 保存值和 PlayMode 使用值由同一配置驱动。
- 柱体下沿对齐修正把底层 `Sp_l112_img_01` 的本地 Y 从 -0.3 调到 0，把上层 `Square` 的本地 Y 从 -1.641 调到 -1.2。结合素材高度 343px/179px、100 PPU 与现有 Y 缩放 1.5625125/1.6535157，两层 Sprite Bounds 下沿均落在父节点本地 Y≈-2.68；两张源图底部还恰好各有约 9px 透明边距，因此实际手绘线下沿也能对齐。`Square` 上的 Stuff blocker 与 Actor Trigger 随 Transform 整体上移，尺寸、offset、Layer mask 和渐变逻辑不变。
- `Level112RingCtrl` 直接接入现有 `DragEventsListener`，同时沿用其编辑器鼠标和移动端单指输入。拉环初始环心为 Y=1.05，最低环心最终下调到 Y=-0.6；拉动不足 0.35 世界单位时快速复位，有效下拉后关闭再次拖拽，并逐帧沿固定世界 X 无限回卷，不再以 Y=62 作为停止点。松手后的实际基础速度从 0.45 开始，用 3 秒 SmoothStep 平滑提到 1.8；发条速度起伏保持 `0.12 / 1.2Hz`，因此起步阶段更慢但后续无限升空速度不变。`visualTransitionDistance=48` 只控制夜色与镜头拉远进度，不限制上升高度。
- 当前 Actor 根节点 Y=-2.11、站立 PolygonCollider2D 顶约 Y=-1.52，拉环 CircleCollider2D 半径为 0.7；最低位时环底约 Y=-1.3，与站立碰撞顶仍保留约 0.22 世界单位间距。Actor 站立时不会自动挂环，仍需起跳接触正在回卷的拉环，但允许的接触窗口比上一版更宽。
- Actor 只在拉环进入回卷阶段后挂载。挂载时停止公共移动、暂时关闭当前 Actor Rigidbody2D 的模拟，并逐帧把 Transform 精确固定在环心下方 `(0,-1.25)`，避免 FixedUpdate/LateUpdate 追赶造成相对抖动；退出时恢复原 BodyType、Simulated 和移动状态。
- Level112 Actor Prefab 实例覆盖为 Level125 同款 `sp_box_025/Box_SkeletonData.asset`，优先托管资源内的 `hang`；若资源意外缺失则依次退化为 `controlhandle`、`lift01_idle`，不会再直接回到普通走路/待机动作。共享 Actor Prefab 与 ActorCtrl 未修改。
- Actor 挂环后，GameCamera 纵向平滑跟随，同时把正交尺寸从当前值逐步拉到 8.5，形成角色越来越远的观感；相机 X 会按初次挂环的 Viewport X 补偿正交缩放，因此拉环在画面中的横向位置保持稳定。
- CommonBg 底色仍复用一份 DrawMode=Tiled 的 SpriteRenderer，Prefab 实例把纵向平铺尺寸从 50 扩到 160。实际横纹来自 CommonBg 内嵌 `Mask.prefab` 的 SpriteRenderer及其 `Hidden_Game_TxtlineRender` 材质，而不是底色 Sprite。为避免深层 Prefab 覆盖未作用于当前实例，`Level112RingCtrl` 会在初始化及挂环升空时通过共享材质的 shader 名 `Game/TxtlineRender` 一次性定位横纹 Renderer，并把底色、横纹、夜色三层的高度不足值直接校正到 `backgroundPanelHeight=160`。为支持无限升空，镜头超过当前背景中心半个回收步长后，CommonBg 与夜色叠层按 72 世界单位整体上移；72 是横纹贴图 14.4 世界单位周期的 5 倍，重定位后纹理相位不跳，也不会逐帧粘着镜头。夜色叠层 alpha 随前 48 世界单位升空进度从 0 到 0.78。
- `Level112Sky.prefab` 放置 7 个 SpriteRenderer（3 云、3 星星、1 月亮），资源来自 Level013；根节点使用与 Level013 一致的 Background SortingGroup/order 5。`Level112SkyLoopCtrl` 每帧仅回收 6 个云/星 Transform：它们落到镜头下方 3 世界单位后，按 48 世界单位的整数倍回收到镜头上方；月亮通过 `oneShotMoon` 引用与 `SkyMoon` 名称兜底显式排除，只在初始高度出现一次。没有复制 Level013 的 InfiniteScrollMap、粒子、Tween 或成片纵向 Tile，运行时也不实例化新天空对象。
- Tips 文本直接设为“另辟蹊径”，并停用复制来源的 I2 Localize 覆盖；其它语言版本尚未配置。
- 右侧 `door_0` 继续复用源 Prefab 的 `OnTrigger2DHandle(targetTag=Player)`，其 `onEnter` 已绑定本关 `BaseLevelCtrl.OnActorTrigger2D`。修正 Level112 实例对源 BoxCollider2D fileID `6885174075320494341` 的 `m_Enabled=0` 错误覆盖为 1；源 Collider 保持 `isTrigger=true`，Actor 靠近即设置 `IsActorEnterDoor` 并走标准 `CheckFinished -> PlaySuccessAnim -> SUCCESS`，不新增并行结算入口。

## 影响文件与资源

- `Assets/Game/Prefabs/Level/Level_112.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_112/Level112ActorCtrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_112/Level112HiddenPassage.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_112/Level112RingCtrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_112/Level112SkyLoopCtrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_112/Prefabs/Level112Sky.prefab`
- `Luban/Configs/Datas/LevelSort.xlsx`
- `Luban/Configs/Datas/LevelData.xlsx`
- `DevelopMD/已有功能设计与使用记录.md`

## 运行流与状态恢复

- 正解流：`Square` 遮挡 Renderer 默认显示 -> Actor Collider 与显隐 Trigger 重叠且 Actor 根节点进入黄色框实际水平边界 -> 遮挡块从当前透明度渐隐并在 alpha 到 0 后停用 Renderer -> Actor 穿过暗道并靠近右门 -> 门 Trigger 调用 `BaseLevelCtrl.OnActorTrigger2D` -> 标准成功动画与 SUCCESS；Actor 离开黄色框时 Renderer 从当前透明度渐显到素材原有 alpha，箱子接触独立的 Stuff 专用实体碰撞后停止。
- 误导流：按下拉环 -> 纵向下拉至 Y=-0.6 限位 -> 松手 -> 拉环以 0.45 起步并在 3 秒内平滑提速到 1.8 -> Actor 起跳接触回卷中的拉环 -> 切换 `hang` 并锁定相对姿势 -> 镜头上移/拉远且保持拉环横向屏幕位置 -> 吊环持续上升，背景与固定天空对象池在镜头上下边界按各自整周期循环。
- 拉环或关卡对象停用时，脚本解除 Actor 动画托管、恢复刚体类型和移动，复原镜头、CommonBg/夜色叠层位置及颜色，并把天空元素恢复到初始局部位置。误导路线没有高度终点，也不新增成功或失败条件。

## 验证证据

- Unity 6.0.59f2 已重新编译 `Assembly-CSharp.dll`，二进制中可检索到 `Level112ActorCtrl`、`Level112HiddenPassage` 和 `Level112RingCtrl`；随后 Level112 主 Prefab 与天空 Prefab 均生成了新的 PrefabImporter artifact。
- 静态核对层定义：Stuff 为 Layer 8（bit 256），Actor 为 Layer 11（bit 2048），Actor Prefab 根节点 Tag 为 `Player`；`Square` 两份 Collider2D 的 include/exclude mask 与该分层一致。
- YAML 引用检查覆盖主 Prefab 和天空 Prefab：本地 fileID 定义唯一且引用闭合；新脚本 GUID 在 Assets 中均唯一。`git diff --check` 未发现空白错误。
- 最新导入后未再出现 Level112 C# 编译错误或 Prefab component load 错误。初次热加载发生在新脚本尚未编译完成的窗口，曾出现瞬态 component load 日志，完成编译并重导后资源引用仍完整。
- 使用项目指定的 spreadsheet artifact 工具重新读取并渲染两份仓库原表：`LevelSort/defaultSort` 第 126 行唯一新增 `[121, 10112, 另辟蹊径]`；`LevelData/level` 第 126 行唯一新增 `10112 / Level_112 / 另辟蹊径 / 1.14.0`，提示键为 `S_congraktTips_Des_level_112` 与 `S_answer_Des_level_112`，第 127 行仍为 `10999 / Level_999 / TBC`。两表公式错误扫描均为 0，原有三张工作表页签均保留。
- 2026-09-04 五项修正后使用 Unity 6.0.59f2 自带 Roslyn 与当前 `Assembly-CSharp.rsp/rsp2` 完整编译，命令 `exit 0`；输出只有仓库既有 warning，没有 Level112 新错误。Prefab 静态检查确认 `Square Layer=9`、两 Collider 引用闭合、拉环低频参数、CommonBg `m_Size.y=160` 和 Level125 SkeletonData GUID 均已序列化；`git diff --check` 通过。
- 2026-09-04 第二轮堵路修正确认旧 fileID `2918326949231021970` 及其 Ground Collider 定义、Prefab `m_AddedComponents` 引用均已从 Level112 主 Prefab 删除；Level112 Actor 序列化的 `whatIsWall.m_Bits=0`，`Square` 的 Stuff blocker/Actor trigger 仍保持引用闭合。Prefab `git diff --check` 通过；修正后的 PlayMode 实走仍待用户重新载入关卡验收。
- 2026-09-04 Square 渐变修正使用 Unity 6.0.59f2 自带 Roslyn 与当前 `Assembly-CSharp.rsp/rsp2` 编译，`exit 0` 且没有 Level112 新错误；Prefab 已显式序列化 `fadeDuration=0.25`，脚本与 Prefab 的 `git diff --check` 通过。
- 2026-09-04 后续三项修正使用同一 Unity Roslyn 响应文件完整编译，`exit 0` 且没有 Level112 新错误；Level112 Prefab 静态核对确认 `revealHorizontalInset=0.2`、拉环初始 Y=1.05、最低 Y=-0.35、夜色叠层引用闭合且新增三个本地 fileID 唯一，相关脚本/Prefab 的 `git diff --check` 通过。
- 2026-09-04 横纹断层修正确认 CommonBg 底色 Renderer 已是 `m_Size.y=160`，而内嵌 Mask 横纹 Renderer 仍继承 `m_Size.y=50`；Level112 Prefab 现对嵌套来源 `Mask.prefab / fileID 3227289699383236056` 显式覆盖 `m_Size.y=160`。Prefab 本地引用闭合且 `git diff --check` 通过；完整升空目视仍待 PlayMode 验收。
- 2026-09-04 无限升空修正移除拉环的 Y=62 完成阶段；Unity 6.0.59f2 Roslyn 使用当前 Assembly-CSharp 响应文件并显式加入新 `Level112SkyLoopCtrl.cs` 完整编译，`exit 0` 且没有 Level112 新错误。天空 Prefab 序列化 7 个天空 Transform，并把月亮单独绑定到 `oneShotMoon`；循环时显式跳过该引用与名为 `SkyMoon` 的对象，因此实际只回收 6 个云/星。主 Prefab 序列化 `visualTransitionDistance=48`、`backgroundLoopStep=72`。主/天空 Prefab 的本地 fileID 唯一性、引用闭合与脚本 GUID 唯一性已静态核对，相关文件 `git diff --check` 通过。
- 2026-09-04 横纹运行时兜底修正新增 `backgroundPanelHeight=160` 序列化参数；源码静态核对确认只在缓存为空时执行一次 `GetComponentsInChildren<SpriteRenderer>(true)`，使用 `sharedMaterial` 匹配 `Game/TxtlineRender`，之后每帧只做三次高度比较且仅在不足时写 `SpriteRenderer.size`。Unity Roslyn 完整编译 `exit 0`，Prefab 参数、脚本字段与本地引用检查通过；重新载入 Level112 后的 Y=25 边界和无限升空接缝仍待 PlayMode 目视确认。
- 2026-09-04 拉环慢启动与低限位修正序列化为 `lowestCenterY=-0.6`、`initialRiseSpeed=0.45`、`riseAccelerationDuration=3`、`riseSpeed=1.8`；起步速度使用 SmoothStep 插值后再乘既有低频发条系数。Unity Roslyn 完整编译 `exit 0`，Prefab 字段唯一、主/天空 Prefab 本地引用闭合；实际起跳成功率与“仍需跳跃”的手感待 PlayMode/移动端验收。
- 2026-09-07 最新显隐与 Trigger 调整后静态确认：Stuff blocker fileID `112000000000000301` 保持非 Trigger、只包含 Stuff/排除 Actor；Actor visibility Trigger fileID `112000000000000302` 保持 Trigger、只包含 Actor/排除 Stuff。两份 offset 均为 `(0,-0.21)`，size 分别为 `(2.18,1.36)` 与 `(1.82,1.02)`；组件序列化相同几何参数及 `revealHorizontalInset=0`，`OnValidate` 与 `Awake` 均会同步实际 Collider。Unity 6.0.59f2 Roslyn 使用当前 Assembly-CSharp 响应文件完整编译为 `exit 0`；Prefab 的 173 个本地 fileID 定义唯一、引用闭合，遮挡 Renderer 序列化 alpha 为 1。进入渐隐、离开渐显的实际过渡和黄色轮廓像素级贴合仍待 PlayMode 验收。
- 2026-09-07 下沿对齐修正静态确认两层 Transform 本地 Y 分别为 0 与 -1.2；按源图片实际像素高度、100 PPU 和 Prefab 缩放换算，底层 Sprite Bounds 下沿为 `0-(3.43×1.5625125÷2)≈-2.680`，上层为 `-1.2-(1.79×1.6535157÷2)≈-2.680`，两者差小于 0.001 本地单位。源图 alpha 扫描确认两张图底部均保留约 9px 透明边距，阈值 64 下的实际笔画下沿差小于 0.01 本地单位。Prefab 的 173 个本地 fileID 仍唯一且引用闭合；与左右手绘地面线的最终像素观感待 PlayMode 验收。
- 2026-09-07 右门通关修正静态确认：`door_0.prefab` 的实际 BoxCollider2D fileID `6885174075320494341` 为启用且 `m_IsTrigger=1`；Level112 实例已把同一源组件的 `m_Enabled` 覆盖改为 1。源 `OnTrigger2DHandle` 使用 `targetTag=Player`，Level112 的 `onEnter` override 目标为根节点 BaseLevelCtrl fileID `5816475025737335787`、方法为 `OnActorTrigger2D`；Actor 根节点 Layer 为 Actor、Tag 为 Player。Prefab 本地 fileID 唯一且引用闭合；实际靠近门后的成功动画和结算仍待 PlayMode 验收。

## 风险与待验收项

- 用户截图与 Editor.log 已复现首轮的堵路、推墙、无 `hang` 和背景跟随问题；修正版尚未完成新一轮 PlayMode 目视验收。Y=-0.6 的 0.22 世界单位站立间距、0.45 到 1.8 的三秒提速、移动端从拉环手势切到跳跃按钮、低频发条节奏、`hang` 对齐、视口 X 锚定与逐渐变远观感均为 `uncertain`。
- `Square` 已移出 Ground 墙检测，覆盖下半通道的外层 Ground Collider 已删除，并在 Awake 重申 per-collider layer override；静态 mask 已核对，但箱子高速推动、Actor 从边缘跳入和不同角色皮肤碰撞范围仍需 PlayMode 验证，状态为 `uncertain`。
- Square 的进入/离开渐变按 Trigger 内 Actor Collider 集合控制，可避免多 Collider 提前恢复；0.25 秒时长、快速折返时从当前 alpha 反向过渡、角色死亡/传送离开以及对象停用时恢复默认可见的表现仍需 PlayMode 目视验收，状态为 `uncertain`。
- Actor 中心判定只对本关水平穿越使用 X 边界，不额外要求根节点 Y 进入，以免 Actor 根节点位于脚底导致永不触发；当前水平内沿为 0，纵向是否进入由黄色 Trigger 的物理重叠负责。不同皮肤下的实际触发时机仍需 PlayMode 目视验收，状态为 `uncertain`。
- Square 使用非等比缩放 `(1.1821876,1.6535157)`，本轮依据截图与 100 PPU 素材尺寸换算黄色范围；Unity Collider 编辑器中的最终轮廓、不同箱子接触法线和边角手感仍为 `uncertain`，如需像素级贴合可继续在 Inspector 微调本地 size/offset。
- 两张柱体素材已按各自尺寸与缩放把 Sprite Bounds 下沿统一到父节点本地 Y≈-2.68，实际手绘笔画下沿对齐误差小于 0.01；但线条自身有 1～2px 起伏，且左右地面素材也并非完全水平，Game View 中线条接缝的最终主观观感仍为 `uncertain`。
- 右门的标准 Trigger、Player Tag 过滤和 BaseLevelCtrl UnityEvent 绑定已静态核对；实际物理步触发、成功动画资源状态以及重复进入保护仍需 PlayMode 验证，状态为 `uncertain`。
- 横纹层、底色层与夜色层由运行时代码确保纵向至少覆盖 160 世界单位，并按 72 世界单位重定位；该步长是当前横纹贴图周期的整数倍，但材质在不同图形 API 下的实际采样相位、重定位瞬间与不同宽高比的边缘仍需 PlayMode/真机确认，状态为 `uncertain`。
- 天空采用 6 个循环云/星 Renderer 加 1 个一次性月亮 Renderer，循环步长为 48 世界单位；背景采用既有底色/横纹与单个夜色叠层，避免运行期实例化。低端 Android 的 overdraw、重复图案辨识度、一次性月亮离屏时机与 Background SortingGroup 的最终遮挡效果仍需真机或 Game View 验证，状态为 `uncertain`。连续运行极长时间后 Transform 世界坐标持续增大可能出现浮点精度下降，也为 `uncertain`。
- 当前标题按用户给定中文直出并关闭 I2 覆盖；若本关需要多语言，必须补正式本地化 Term，现状为 `uncertain`。
- 本轮只更新 Luban 的 Excel 源配置，没有执行 Luban 生成流程；运行时生成 JSON/代码是否已经包含 Level112 为 `uncertain`，正式提测前需按项目既有流程重新生成并核对产物。
- 无限回卷不会自动死亡、成功或返回地面；若产品期望自动失败/重开，需要用户补充规则，当前不擅自新增。
- 仓库规则引用的 `E:/WorkSpace/specs/037-remote-resource-loading/plan.md` 当前缺失，与 Level112 没有已知冲突，但无法完成该计划依据核对，状态为 `uncertain`。

## 性能与回滚

- 运行期每帧只更新拉环/挂载 Actor、一台相机、夜色叠层 alpha、相机正交尺寸、6 个云/星 Transform 的边界检查、三层背景高度比较，以及渐变期间的单个 Square Renderer alpha；一次性月亮没有循环检查，CommonBg/夜色 Transform 只在跨越 72 世界单位边界时移动一次。横纹 Renderer 的子节点扫描与数组分配仅首次解析时发生一次；所有元素都在 Prefab 中预置，无运行期 Instantiate/Destroy、无协程、无 Tween、无逐帧集合分配。
- 仅回滚最新右门通关修正时，把 Level112 对源 Collider fileID `6885174075320494341` 的 `m_Enabled` override 恢复为 0；仅回滚最新下沿对齐时，把 `Sp_l112_img_01` 本地 Y 恢复为 -0.3、`Square` 本地 Y 恢复为 -1.641。仅回滚最新反向显隐与 Trigger 调整时，把初始状态恢复为 alpha 0、进入目标恢复为可见 alpha、离开目标恢复为 0，并把 Actor Trigger 恢复为 `size=(2.06,1.28)`、`revealHorizontalInset=0.2`。若同时回滚新素材 Collider 适配，则把 Stuff blocker 恢复为 `size=(1,1) / offset=(0,0)`、Actor Trigger 恢复为 `size=(0.94,0.94) / offset=(0,0)`，并删除本关几何配置字段与 `OnValidate` 同步。仅回滚拉环手感调整时，把 `lowestCenterY` 恢复为 -0.35，并删除 `initialRiseSpeed`、`riseAccelerationDuration` 与提速插值，恢复直接使用 `riseSpeed=1.8`；仅回滚横纹运行时兜底时，删除 `backgroundPanelHeight`、`EnsureBackgroundCoverage`、`EnsureRendererHeight` 及横纹 Renderer 缓存字段，恢复只依赖 Prefab 的尺寸覆盖；仅回滚无限升空时，可恢复 Y=62 目标与完成阶段，移除 `Level112SkyLoopCtrl` 组件/脚本，并删除 `LoopBackgrounds` 与 `backgroundLoopStep`。完整回滚 Level112 时再删除本关私有脚本/天空 Prefab，恢复 Actor Prefab 实例的共享 `ActorCtrl`、默认 SkeletonData 与原 `whatIsWall`，移除 `Square` 两个 Collider2D 与显隐组件、拉环的拖拽/控制组件，并恢复 `Ground_short 1` 实例旧的下半段额外 Collider；撤回 CommonBg 纵向尺寸、主 Prefab 标题及天空嵌套修改；同时删除 `LevelSort/defaultSort` 的 10112 排序行、删除 `LevelData/level` 的 10112 数据行并保持 10999/TBC 为末行。共享资源无需回滚。
