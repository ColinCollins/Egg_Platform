# Level75 功能说明

## 2026-07-27 第九次 PlayMode 反馈修正（当前实现）

- 最新要求：门受压开头的观感基本正确，但后半段压扁过快，齿尖还没有继续压到对应位置时门已经自行缩短；需要让门的后半段变形继续跟随匀速下降的刺墙。
- 本地几何核对表明触发器不是主要原因：刺墙从 `Y=2.2` 在 `2.5s` 内线性下降到 `Y=-3.4`，速度为 `2.24u/s`；第二段门可视高度按约 `3.03u`、Y 倍率从 `0.82` 到 `0.55` 估算，门顶需下降约 `0.819u`。旧 `0.18s + Ease.InQuad` 的平均门顶速度约 `4.55u/s`，末段还会继续加速，明显快于齿尖。
- 第一段 `0.26s / Ease.InQuad / (1.12, 0.82, 1)` 保持不变。第二段持续时间由 `0.18s` 延长为 `0.36s`，缓动由 `Ease.InQuad` 改为 `Ease.Linear`；门顶估算速度约 `2.28u/s`，与刺墙的 `2.24u/s` 基本同步。
- 门从接触到爆开的总变形时间由约 `0.44s` 调整为约 `0.62s`。最终倍率 `(1.25, 0.55, 1)`、触发器位置和尺寸、刺墙下降轨迹、碎门落点、四向碎片、震屏与音效均未修改。
- 本轮只修改 `Level75Ctrl.cs`、`Level_075.prefab` 和 Level075 本地/共享记录；没有修改 StartDoor、Caltrop 公共 Prefab、Level007、Level029、Level034、UI、输入、配置、存档、事件、analytics 或平台流程。
- 验证：Unity 6000.0.59f2 的 Roslyn 响应文件全量编译为 `0 error / 105` 个项目既有 warning，Level075 诊断为 `0`；`Level_075.prefab` 为 `266` 个定义、`543` 个本地引用、`0` 重复、`0` 缺失，且只存在一处 `doorSqueezeBreakDuration=0.36` 绑定。静态复核确认第一段、两段倍率、Trigger/Collider、粒子 GUID、Sequence 清理和公共 StartDoor/Caltrop 资源均未改动；目标差异空白检查通过。
- 待验证：仍需 Unity PlayMode 目测齿尖与门顶在第二段是否持续贴合、`0.62s` 总节奏、爆炸时机，以及玩家在受压期间抬升刺墙或退出/重开时的中断表现。
- 仅回滚本次时，将 `doorSqueezeBreakDuration` 恢复为 `0.18`，并把第二段缓动恢复为 `Ease.InQuad`；第八次的四向粒子和此前的门受压基础流程无需回滚。

以下第八次及更早章节均为历史记录；第八次粒子参数继续保留，但旧文档中的 `0.44s` 门受压总时长已由本节覆盖。

## 2026-07-27 第八次 PlayMode 反馈修正（历史，粒子参数仍保留）

- 最新要求：门达到临界形变后，碎片要从受压点向四周爆开并飞得更远，单片尺寸略微增大；门受压动画、碎门残骸、震屏和其它关卡逻辑保持不变。
- 修改前的主碎片仍是根节点 `X=-90°` 下的 `Cone(type=4)` 窄锥发射，`20°` 锥角再叠加本地 X 轴 `-3~3` 随机速度，只能形成“单向喷射加横向扰动”，不是二维四周放射。
- 当前只修改 075 私有 `DoorBroken_075` 主碎片系统：Shape 改为 `Circle(type=10)`，`arc=360°`、`radius=0.12`、`radiusThickness=0.15`；Shape 自身增加 `X=90°` 旋转，抵消根节点的 `X=-90°`，使圆形发射平面落在游戏世界 XY 平面。
- Circle 自带径向速度，因此关闭原 `VelocityModule`，避免额外 X 轴速度把爆炸重新拉成横向偏置。主初速度由 `5~10` 提高为 `9~15`，重力由 `4` 降为 `2.5`，让上下左右的碎片在落下前先形成清晰的径向展开。
- 主碎片起始尺寸由 `0.5~0.8` 小幅提高到 `0.6~0.95`。Burst 仍为 `25`，生命周期仍为 `0.8~1.2s`，根缩放、贴图动画、Size over Lifetime、材质和 Clouds 子系统均保持不变。
- 本轮只修改 `Assets/Game/ArtSrc/Particles/Prefabs/DoorBroken_075.prefab` 与 Level075 本地/共享记录；没有修改 `Level75Ctrl.cs`、`Level_075.prefab`、门挤压参数、Level007、Level029、Level034、共享 `BoxBroken`、UI、输入、存档、配置、事件、analytics 或平台流程。
- 验证：目标主系统字段已逐项回读；主系统为 `Circle/360°/X=90°/9~15/0.6~0.95/gravity=2.5`，主 `VelocityModule.enabled=0`，Clouds 的 Shape 与 Velocity 状态未被改动。粒子 Prefab 为 `8` 个定义、`14` 个本地引用、`0` 重复、`0` 缺失；Unity Editor 日志已记录该 Prefab 由 `PrefabImporter` 成功重新导入。此次没有修改 C#，因此未重复执行脚本编译。
- 待验证：仍需 Unity PlayMode 目测四向覆盖、最大飞行边界、首帧密度、碎片尺寸、门边缘构图和震屏衔接，并在 Android/iOS 横屏低帧率下确认单次 `25` 片 Burst 的表现。
- 仅回滚本次时，将主 Shape 恢复为 `Cone(type=4)`、`arc=180°`、`radius=0.1`、`radiusThickness=1`、Shape 旋转归零，重新启用 `VelocityModule.x=-3~3`，并把初速度、尺寸和重力分别恢复为 `5~10`、`0.5~0.8`、`4`。第七次的门受压动画无需回滚。

以下第七次及更早章节均为历史记录；其中“窄锥加 X 轴随机速度”的碎片方案已由本节覆盖。

## 2026-07-27 第七次 PlayMode 反馈修正（历史，已由第八次覆盖）

- 最新要求：门被刺墙挤压后不能立即爆炸，需要先出现横向鼓起、纵向压扁的受压过程，达到临界变形后再生成碎门、碎片、震屏和音效；门碎片需要比上一版散开一些。
- 修改前，`CrushDoor()` 在触发当帧直接生成碎门和粒子、隐藏原门。`DoorBroken_075` 虽基于 007 的 `BoxBroken` 制作，但没有带上 Level007 场景实例对主碎片 `VelocityModule.x=-3~3` 的覆写，25 片碎片又从半径 `0.01` 的窄区域同时发射，因此容易聚成一团。
- 当前 `doorVisual` 绑定 `StartDoor (1)/StartDoor` 可视子节点，不缩放门根、物理触发器或 `CrushedDoorSpawnPoint`。接触刺墙后立即锁存 `doorCrushed`、关闭拖拽和门层级内全部 Collider，防止延迟期间重复触发；随后先用 `0.26s` 变形到原缩放的 `(1.12, 0.82, 1)`，再用 `0.18s` 变形到 `(1.25, 0.55, 1)`，两段均为 `Ease.InQuad`。
- 总计约 `0.44s` 的受压动画正常完成后，才沿用原流程创建贴地碎门、在原门碰撞体中心播放碎片、隐藏原门、播放全屏震动和 `blockByDoor` 音效。刺墙下降 Tween 不会被门压碎流程终止。
- `DoorBroken_075` 只调整主碎片系统：补回 Level007 已使用的本地 X 轴随机速度 `-3~3`，并将 Shape 半径从 `0.01` 增至 `0.1`。主初速度 `5~10`、窄锥角 `20`、Burst `25`、根缩放 `1` 和 Clouds 子系统保持不变，避免同时放大碎片、烟尘或移动端峰值。
- 门受压 Sequence 使用非缩放时间，与本关持续使用非缩放时间的刺墙运动保持一致。`Cleanup()` 只 Kill Sequence，不在 `OnKill` 执行爆炸完成逻辑，因此 Reset、退出或销毁发生在受压中途时不会误生成碎门和粒子。
- 修改范围仅为 `Level75Ctrl.cs`、`Level_075.prefab`、075 私有 `DoorBroken_075.prefab` 和本地文档；没有修改 Level007、Level029、Level034、共享粒子、共享输入、配置、存档或平台流程。
- 验证：Unity 6000.0.59f2 Roslyn 全量编译为 `0 error / 105` 个项目既有 warning，Level075 诊断为 `0`；`Level_075.prefab` 为 `266` 个定义、`543` 个本地引用，粒子 Prefab 为 `8/14`，两者重复和缺失均为 `0`；目标文件 `git diff --check` 通过。
- 待验证：仍需在 Unity PlayMode 目测 `0.44s` 受压节奏、门底是否稳定、刺墙与门变形的接触感、碎片横向扩散范围、震屏衔接、Reset/退出中断，以及 Android/iOS 横屏低帧率下的粒子表现。
- 仅回滚本次时，移除 `doorVisual` 与四个受压参数、受压 Sequence 和 Collider 批量关闭，恢复 `CrushDoor()` 当帧完成表现；粒子 Prefab 将主碎片 X 速度恢复为 `0`、Shape 半径恢复为 `0.01`。此前 UI、跳跃、拖拽、吸附和抬墙逻辑无需回滚。

以下第六次及更早章节均为历史记录；其中“门触发当帧直接爆炸”的流程已由本节覆盖。

## 2026-07-27 第六次 PlayMode 反馈修正（历史，已由第七次覆盖）

- 最新要求：页面出现时，唯一跳跃按钮必须和其它 HUD 一样按顺序播放入场，不能在其它按钮之前直接满尺寸出现；按钮出现和消失时序参考 `Level_034`，此前松手跳跃、严格拖拽、吸附与机关规则保持不变。
- 修改前，`Level75Ctrl.InitializeWorldUpButton()` 在取得 `Root/jump_btn` 槽位并等待 `PLAYING` 后直接激活 `WorldUpButton`。该对象不属于 `GamePlayPanel.SequentialScaleAnim`，Prefab 目标缩放又是 `1.09`，因此会绕过 HUD 顺序动画直接显示，并提前开放点击。
- 当前在 `Awake()` 缓存 `WorldUpButton` 的 Prefab 目标缩放 `1.09` 后立即归零；取得槽位时先隐藏原 UI `jump_btn` 和兼容槽 `up_btn`，再以零缩放激活场景按钮，不存在激活当帧的满尺寸闪现。
- 入场动画继续使用 `Level_034` 的 `initButtonScaleDuration=0.3`、`initButtonDelayStep=0.06`、jump 顺位 `2` 和 `Ease.OutBack`。区别是 034 会同时替换左/右/跳三个按钮，而 075 的左右键仍由 GamePanel 在约 `1.30s/1.36s` 播放；因此 075 的 `initButtonDelayOffset=0.8` 额外包含 `0.5s` 面板起点对齐，实际 Tween 延迟为 `0.92s`，约在页面启动后 `1.42s` 接在左右键之后出现。Tween 使用 unscaled update，与页面 UI 的非缩放时间入场保持同步。
- 入场期间私有输入组件本身保持 disabled，点击和拖拽均关闭；完成后先确认 `PLAYING`，再启用组件、等待一帧完成激活，随后再次确认 `PLAYING` 才同时开放输入。`Level75WorldButtonClick` 在每帧入口和松手结算时实时检查 FSM，不再依赖可能缺失的状态切换事件，因此重置/重玩从 `PAUSE` 直接切回 `PLAYING` 时不会遗留陈旧暂停缓存。清理时会禁用输入组件、终止入场 Tween、将缩放归零并停用按钮。
- 本轮修改限于 `Level75Ctrl.cs`、075 私有 `Level75WorldButtonClick.cs`、`Level_075.prefab` 和文档；`GamePlayPanel_027_75.prefab`、Level034、共享 `GamePlayPanel` 和共享输入脚本均未修改。松手单次跳跃、`60px` 拖拽阈值、吸附后不跳且永久锁拖、按钮随墙移动及 `3s` 抬墙逻辑保持不变。
- 本轮不新增资源、配置、存档、全局事件、analytics、广告、IAP 或平台分支；事件流仍仅在未吸附的合法短按松手时派发既有 `PlayerJumpEvent`。
- 验证：Unity 6000.0.59f2 Roslyn 按当前 `Assembly-CSharp.rsp` 完整编译为 `0 error`，仅有项目既有 warning，Level075 诊断为 `0`；`Level_075.prefab` 为 `266/542/0/0`，专用面板为 `35/68/0/0`（定义/本地引用/重复/缺失）；目标差异检查通过。
- 待验证：仍需在 Unity PlayMode 目测确认左右键与跳跃键的实际先后节奏、入场期间完全不可操作、暂停/退出重开时无残留，以及 Android/iOS 横屏触摸和低帧率下的表现。
- 仅回滚本次时，移除三个 `initButton*` 字段、目标缩放缓存和入场 Tween，恢复定位后直接激活按钮；若只回滚最终顺序校准，则把 `initButtonDelayOffset` 从 `0.8` 恢复为 `0.3`。此前第五次及更早的跳跃、拖拽、吸附、门和刺墙逻辑无需回滚。

以下第五次及更早章节均为历史记录；其中“按钮定位后直接满尺寸激活”的时序已由本节覆盖。

## 2026-07-27 第五次 PlayMode 反馈修正（历史，已由第六次覆盖）

- 最新要求：跳跃必须在正常松手时触发；按钮吸附到红框后不再让角色跳跃；拖拽启动需要更严格，避免轻点跳跃时因手指小幅偏移误拖按钮。
- 当前松手规则：`MouseDown/Touch Began` 只认领指针，不发送事件。只有 `MouseUp/Touch Ended` 正常结束、仍处于 `PLAYING`、整段手势未进入拖拽且按钮尚未吸附时，才发送一次 `PlayerJumpEvent`。
- 当前拖拽规则：`dragThresholdPixels` 从 `20` 提高到 `60`。按下后的最大屏幕位移必须超过 `60px` 才开始移动按钮；阈值内的轻微偏移仍在松手时按跳跃处理。一旦整段手势曾超过阈值，即使又移回起点，也只按拖拽处理，不会误跳。
- 当前吸附规则：吸附成功后 `Level75Ctrl.IsWorldUpButtonInstalled=true`，私有输入组件不再发送 `PlayerJumpEvent`。此后的正常松手只调用 `ClickWorldUpButton()`；第一次触发尖刺墙上升，后续由 `mechanismActivated` 防止重复启动。
- 单按钮、红框外扩 `0.5` 世界单位、吸附后锁拖并随墙移动，以及尖刺墙 `3s` 上升到 `Y=7.8` 的规则保持不变。
- 本次只修改 `Level75WorldButtonClick.cs`、`Level75Ctrl.cs`、`Level_075.prefab` 和 Level075/共享设计记录；未修改共享 `GamePlayPanel`、`DragableItem`、`PlayerJumpEvent` 接收端或其它关卡。
- 静态验证：Unity 6000.0.59f2 Roslyn 按当前 `Assembly-CSharp.rsp` 完整编译为 `0 error / 105` 个项目既有 warning，Level075 诊断为 `0`；`Level_075.prefab` 仍为 `266` 个定义、`542` 个本地引用、`0` 重复、`0` 缺失。
- 待验证：需要在 PlayMode 检查按下不跳、松手只跳一次、`60px` 内手抖仍跳、明确拖动不跳、吸附后点击只抬墙不跳，以及不同分辨率下 `60px` 的实际手感。Android/iOS 横屏触摸尚未真机验证。
- 回滚本次规则时，移除吸附状态的跳跃阻断并把 `dragThresholdPixels` 恢复为 `20`；单按钮、吸附、碎门、震屏和慢速抬墙无需回滚。

以下第四次及更早的全部章节均为历史记录；其中“吸附后仍发送跳跃”“机关后仍可跳跃”和“20px 拖拽阈值”已被本节覆盖，不代表当前实现。

## 2026-07-27 第四次 PlayMode 反馈修正（历史，已由第五次覆盖）

- 需求：画面只保留一个跳跃按钮；同一按钮短按时让角色跳跃，拖动时可送入红框；尖刺墙向上移动的节奏需要比此前更慢。
- 既有问题：上一版同时保留共享 `jump_btn` 和场景 `WorldUpButton`，因此画面出现两个向上按钮；共享 `DragableItem` 与私有点击脚本分别读取输入，移动端多指时还可能出现点击识别到第二根手指、拖拽却只读取第 0 根手指的分歧。
- 当前唯一入口：`Level75Ctrl` 读取专用面板 `Root/jump_btn` 的响应式位置，将 `WorldUpButton` 放到对应世界坐标后停用原 `jump_btn`；旧 `Root/up_btn` 在 `GamePlayPanel_027_75.prefab` 中默认失活，运行时也会再次停用。因此最终只有 `WorldUpButton` 可见并接收输入。
- `WorldUpButton` 不再挂共享 `DragableItem`。075 私有 `Level75WorldButtonClick` 统一负责鼠标与触摸的按下、短按、拖拽、释放和取消，不修改共享输入脚本或其它关卡。
- 按下按钮后，整段手势的最大屏幕位移不超过 `dragThresholdPixels=20` 时，正常松手只发送一次现有 `PlayerJumpEvent`；位移超过阈值后转为拖拽，按钮才开始移动，释放时只执行红框安装判定，不发送跳跃事件。
- Android/iOS 会在按下时记录实际命中按钮的 `fingerId`，后续只跟踪该触点；因此玩家用另一根手指按住左右移动时，按钮仍可由独立触点短按或拖动。活动触点消失、暂停、触摸取消、应用失焦/切后台、对象禁用和关卡清理都只取消手势，不跳跃也不吸附。
- 指针归属只在按下时通过按钮自身 Collider 确认；短按松手不重复做 Collider 命中。这样按钮吸附后即使随尖刺墙移动，合法短按也不会因按钮离开手指位置而被丢弃。
- 红框仍使用 `targetFrame.bounds` 并向四周外扩 `targetTolerance=0.5` 世界单位。命中后按钮吸附到红框中心、改挂到红框 Transform 下并永久关闭拖拽，所以不能再拖下且会继续随尖刺墙移动。
- 吸附前的短按只负责跳跃；吸附后的短按仍发送跳跃，同时调用一次抬墙机关。`mechanismActivated` 只防止墙体重复启动，不再关闭唯一按钮，因此机关触发后按钮仍可继续跳跃。
- 尖刺墙目标位置仍为世界坐标 `Y=7.8`，缓动仍为 `Ease.OutCubic`；`ceilingRaiseDuration` 已由 `1s` 放慢为 `3s`。
- 本次影响文件为 `Level75Ctrl.cs`、`Level75WorldButtonClick.cs`、`Level_075.prefab`、`GamePlayPanel_027_75.prefab` 和 Level075/共享设计记录。未修改 Level029、Level034、共享 `GamePlayPanel`、共享 `DragableItem`、角色跳跃接收端、配置、存档、资源加载、analytics 或平台 SDK。
- 静态验证：Unity 6000.0.59f2 自带 Roslyn 按当前 `Assembly-CSharp.rsp` 完整编译为 `0 error`，仅输出项目既有 warning；`Level_075.prefab` 为 `266` 个本地定义、`542` 个本地引用，专用面板为 `35` 个本地定义、`68` 个本地引用，两者重复和缺失均为 `0`。
- 待验证：仍需重新进入 Unity PlayMode 检查“轻点只跳一次、轻微抖动不漂移、拖动全程不跳、未命中可继续拖、吸附后锁定且点击抬墙、机关后仍能跳、按钮随墙移动、3 秒上升节奏”；Android/iOS 横屏多指触摸尚未真机验证。
- 回滚本次输入调整时，恢复 `Root/up_btn + DragableItem` 作为独立机关入口、让共享 `jump_btn` 保持可见，并把 `ceilingRaiseDuration` 恢复为 `1s`；门压碎、碎门锚点、粒子、震屏和关卡配置无需回滚。

以下全部章节（包括旧反馈、需求与基线、当前行为、UI/资源/事件流、验证及回滚）仅记录方案演进；其中“双按钮”“共享 jump 单独跳跃”“WorldUpButton 只拖不跳”和“1 秒抬墙”均已被本节覆盖，不代表当前实现。

## 2026-07-27 第三次 PlayMode 反馈修正

- 反馈背景：截图中实际可见的是共享 `jump_btn`，075 专用 `up_btn` 没有呈现；共享跳跃键不含 `UIDrag`，因此玩家看到的“向上”按钮完全无法拖动。继续调整 UI 射线或吸附容差不能解决该问题。
- 既有基线：Level034 会读取 HUD 槽位，将同外观的场景 Sprite 按钮放到对应世界坐标并隐藏原 HUD，输入由场景 `SpriteRenderer + BoxCollider2D` 组件承担，不依赖 Canvas Raycast。
- 当前实现：`Level75Ctrl` 启动后等待专用 GamePanel 就绪，读取 `Root/up_btn` 的响应式槽位，通过 `CommonUtility.ConvertUIToWorldPosition` 设置 `WorldUpButton` 的世界起点，然后隐藏槽位对象。共享 `jump_btn` 保持原样，继续负责正常跳跃。
- 世界按钮不会在 HUD 入场暂停期间提前响应：槽位换算完成后会等待 `PlayCtrl.CheckState(PLAYING)`，激活输入组件并留出一帧完成状态事件订阅，再次确认仍为 `PLAYING` 后才开放拖拽；本关点击组件启动时也会主动读取当前 FSM 状态。
- 吸附入口和抬墙入口都会再次校验当前状态为 `PLAYING`。共享 `DragableItem` 在暂停时强制结束拖拽，也不会因此把刚好位于红框内的按钮误判为已安装。
- `WorldUpButton` 直接复用 Level034 的按钮底图和向上箭头资源，使用本关 `GameCamera`、`BoxCollider2D`、`DragableItem` 与本关私有 `Level75WorldButtonClick` 接收鼠标/触摸。拖拽阶段关闭点击；只有松手命中红框后才开放一次独立点击。
- 红框位置被墙体的大 Collider 覆盖，不能直接复用只接受 Physics2D 首个命中的共享 `ClickItemEventHandle`。`Level75WorldButtonClick` 直接调用按钮自身 `Collider2D.OverlapPoint`，不会被墙体 Collider 抢占；它仍监听暂停状态，且只在 075 内使用。
- 红框命中使用 `targetFrame.bounds`，四周外扩序列化参数 `targetTolerance=0.5` 世界单位，并以按钮中心判定，允许一定投放误差。未命中时按钮保留在释放位置，可再次拖动。
- 命中后按钮吸附到红框中心，改挂到红框 Transform 下，并调用 `DragableItem.SetEnableDrag(false)` 永久锁定；因此按钮不能再被拖下，同时会自然跟随尖刺墙下降和抬升。再次点击才终止下降 Tween 并抬升墙体。
- 可拖门 `StartDoor (1)` 下新增空节点 `CrushedDoorSpawnPoint`，初始局部坐标为 `(0, -0.615, 0)`。碎门直接使用该节点的世界位置和旋转生成，策划可在 `Level_075.prefab` 中手动移动节点；节点作为门的子物体，会保留玩家横向拖门后的 X。
- `crushedDoorGroundY=-2.365` 仅保留为锚点未绑定或被删除时的兼容兜底。粒子仍在原门碰撞体中心生成，全屏震动逻辑不变。
- 本轮修改仅涉及 `Level75Ctrl.cs`、新增 `Level75WorldButtonClick.cs`、`Level_075.prefab` 和 Level075 文档；没有修改 Level029、Level034、共享 `GamePlayPanel`、共享跳跃键、`UIDrag`、`DragableItem`、`ClickItemEventHandle`、`CommonUtility`、配置、存档、全局事件、analytics 或平台分支。
- 静态验证：`Level_075.prefab` 共 `268` 个本地对象定义、`548` 个本地 fileID 引用，重复与缺失均为 `0`；目标差异 `git diff --check` 通过。Unity 已重新生成包含新增本关脚本的响应文件，Unity 6000.0.59f2 自带 Roslyn 按当前 `Assembly-CSharp.rsp` 完整编译为 `0 errors / 105` 个项目既有 warning，未发现 Level075 诊断。
- 待验证：需要策划重新进入 PlayMode 确认场景向上键显示、鼠标拖动、容差吸附、未命中可继续拖、吸附后不能拖离且可点击、普通跳跃键仍正常、按钮随墙移动，以及手调 `CrushedDoorSpawnPoint` 后的最终落地观感。Android/iOS 横屏触摸尚未验证。
- 尖刺墙下降/抬升继续保留现有 `SetUpdate(true)` 非缩放时间行为，因此暂停时墙体 Tween 仍会运行；这是本轮未改变的关卡基线，是否改为随暂停停止需策划另行确认。
- 仅回滚本轮时：删除 `WorldUpButton` 与 `CrushedDoorSpawnPoint`，恢复 `Level75Ctrl` 对 `Level75GamePlayPanelCtrl/UIDrag` 的绑定和硬编码碎门落点；其它 075 机关、粒子、震屏、资源和配置无需回滚。

## 2026-07-27 第二次 PlayMode 反馈修正

- 反馈基线：上一版只在玩家再次点击时调用 `CommonUtility.ConvertUIToWorldPosition` 判定红框；075 面板自身为 World Space Canvas、运行时又位于 UI 根 Canvas 下，坐标系不一致会导致场景红框无法命中，而且 DragEnd 没有吸附行为。
- 当前行为：`UIDrag.OnDragEnd` 立即触发本关吸附判定。代码分别把按钮中心和场景红框投影到屏幕坐标，以红框 bounds 外扩 `0.5` 世界单位作为容错区域，不再调用公共 UI 到世界坐标转换。
- 命中后先确认按钮能成功移动到红框中心，再提交“已安装”状态；随后只禁用 `UIDrag`，保留 `CustomButton` 点击。玩家不能把已吸附按钮拖下来，仍需再独立点击一次才抬升尖刺墙。
- 未命中或 Canvas 转换失败时，按钮恢复到专用面板起点，允许玩家重新投放，不会遗留在屏幕外或错误进入已安装状态。
- 已吸附按钮在 `LateUpdate` 中持续按 `targetFrame.bounds.center` 回写屏幕位置，因此会随 `CaltropGround` 的下降和抬升保持镶嵌；点击成功后也会继续随红框移动。
- 碎门实际静态图为 `door_sl_01.png`，图像最低行存在有效像素。上一版 `Y=-2.5764` 是 `Ground_long` 的物理碰撞顶面，但门所在位置的可见地表线是 `Y=-2.365`，会使碎门埋入画面约 `0.2114` 世界单位；当前改为按 `Y=-2.365` 对齐 Sprite 最低点。
- 修改仍仅涉及 `Level75Ctrl.cs`、`Level75GamePlayPanelCtrl.cs`、`Level_075.prefab` 和本地文档；未修改共享 `UIDrag`、`CommonUtility`、029、公共面板、公共按钮、公共相机或配置。
- Unity 6000.0.59f2 自带 Roslyn 使用当前 `Assembly-CSharp.rsp` 完整编译通过，结果为 `0 error / 105` 个项目既有 warning。
- 待 PlayMode 复验：不同释放误差下的吸附/回弹、吸附后不可拖离且仍可点击、按钮全程跟随移动红框、碎门可见落点和主体轮廓观感。Android/iOS 横屏触摸仍未验证。
- 仅回滚本轮时，恢复点击时的坐标命中逻辑、移除 DragEnd 吸附/跟随 API，并把 `targetTolerance` 与 `crushedDoorGroundY` 恢复为上一版值；其它 075 资源和主机关无需回滚。

本节覆盖本文后续旧段落中关于 `CommonUtility.ConvertUIToWorldPosition`、`0.05` 容差和物理地面 `Y=-2.5764` 的描述。

## 需求与基线

Level075 基于 Level029 制作，保留刺墙延时下压和门可拖动的基础结构，但过关方式改为：

1. 将独立的“向上”UI 按钮拖入场景红框。
2. 松开后再点击一次该按钮。
3. 按钮中心位于红框范围内时，刺墙停止下压并向上移出危险区域。

Level029 中“门挡住刺墙并停止移动”的解法不再成立。门被拖到刺墙下方后会被压碎，刺墙继续下降。

## 修改范围

| 文件/资源 | 作用 |
|---|---|
| `Assets/Game/Scripts/Level/MainLevel/Level_75/Level75Ctrl.cs` | 075 独立关卡逻辑、红框命中、刺墙抬升、门压碎表现 |
| `Assets/Game/Scripts/Level/MainLevel/Level_75/Level75GamePlayPanelCtrl.cs` | 独立向上按钮绑定、拖拽后释放点击隔离、交互锁定 |
| `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_027_75.prefab` | 基于标准面板结构的 075 专用面板 |
| `Assets/Game/Prefabs/Level/Level_075.prefab` | 改绑 075 控制器、专用面板、红框、相机、压扁门和碎片特效 |
| `Assets/Game/ArtSrc/Sprites/l75/door_sl_01.png` | 门压碎后的静态残骸图；导入模式为 Single Sprite |
| `Assets/Game/ArtSrc/Sprites/l75/door_zl_02.png` | 8×2 门碎片粒子图集，15 格有效、最后一格透明 |
| `Assets/Game/ArtSrc/Particles/Prefabs/DoorBroken_075.prefab` | 075 私有门碎裂粒子，保留 007 参考特效的碎片与烟尘结构 |
| `Assets/Game/ArtSrc/Particles/Material/DoorBroken_075.mat` | 同时将 `_BaseMap`、`_MainTex` 绑定到门碎片图集 |
| `Luban/Configs/Datas/LevelData.xlsx` | 策划提供的 `10075 -> Level_075` 配置源，本次不改其内容 |
| `Assets/Game/Configs/tbleveldata.json` | 由当前 Excel 隔离生成并同步的 075 运行时配置行 |
| `Assets/Game/Configs/tblevelsort.json` | 当前已有 `86 -> 10075`，因此 HUD 显示 Level 86 时加载 `Level_075`；本轮不改该映射 |

`Level29Ctrl`、`GamePlayPanel_000`、`GamePlayPanel_004/005`、`BoxBroken` 和 `brokenBox.mat` 均未修改。工作区中策划已调整的 `Level_075.prefab` 布局与 `LevelData.xlsx` 内容保持原样。

## 当前行为

### 刺墙

- 关卡开始后仍按 Level029 基线：延迟 `2s`，在 `2.5s` 内线性下降到世界坐标 `Y=-3.4`。
- 红框 `Sp_l75_wxqk_01` 是刺墙整体 `CaltropGround` 的子物体；初始世界位置仍为 `(0.17, 4.91, 0)`，之后会跟随刺墙下降、抬升和局部抖动。
- 向上按钮命中红框并被再次点击后，当前下降 Tween 被终止。
- 刺墙在 `1s` 内以 `Ease.OutCubic` 上升到世界坐标 `Y=7.8`。
- 成功触发后向上按钮立即锁定，不能再次拖动或点击。

### 向上按钮

- `GamePlayPanel_027_75` 恢复共享 `jump_btn`；它保持标准 `PlayerJumpEvent` 行为，不附加 `UIDrag`，不修改公共 `GamePlayPanel`。
- 面板新增的独立 `up_btn` 使用右侧第二槽位 `(-593, -403.5)`，与右侧原跳跃键 `(-277, -403.5)` 相隔 `316` UI 单位，不发生渲染或射线重叠。
- `up_btn` 的箭头旋转为 `-90°`，与标准向上箭头方向一致；它保留现有按钮外观、按压缩放和震动组件，拖拽范围不做边界限制。
- 只有独立 `up_btn` 绑定本关 `UIDrag` 和机关点击；拖动机关按钮不会触发共享跳跃事件。
- `UIDrag.OnDragBegin/OnDragEnd` 会阻断拖拽结束同一帧的 Click；玩家必须在松开后再执行一次独立点击。
- `Level75Ctrl` 使用 `CommonUtility.ConvertUIToWorldPosition` 将按钮中心转换到关卡相机的世界坐标。
- 红框直接使用现有 `Sp_l75_wxqk_01.SpriteRenderer.bounds` 判定，不新增碰撞体；边界外扩 `0.05` 世界单位作为触控容差。

### 门压碎

- 门仍在关卡开始 `1.5s` 后开放拖动。
- 门与 `enemy` 刺墙触发器接触时调用 `Level75Ctrl.CrushDoor(Collider2D)`。
- 回调不会终止刺墙 Tween，只会关闭门碰撞体、拖动组件和原门对象。
- 压扁门底边固定贴合本关 `Ground_long` 物理顶面 `Y=-2.5764`，不再使用比地面高约 `0.28` 的外层门触发器底边；放置时使用 `Sprite.bounds.min.y` 消除 Sprite 高度和中心点偏差。
- 压扁门使用红框相同的 `SceneLayer`，排序值为 `201`。
- `DoorBroken_075` 在门碰撞体中心实例化、激活并播放；根粒子结束后自动销毁。
- 同一扇门只允许压碎一次，并播放 `blockByDoor` 音效。
- 压碎时对本关 `GameCamera` 播放 `0.25s`、强度 `0.18`、振动数 `20` 的全屏位置震动；Tween 使用非缩放时间，并在完成、关卡禁用或销毁时恢复相机初始局部坐标。
- 029 复制来的 `MMF_Player` 含门和刺墙 PositionShake，但 075 不再调用它：原门会立即隐藏，刺墙反馈会和下降 Tween 同时写 Transform。可见震动统一由 075 的 GameCamera Tween 负责，旧 prefab 组件本身不改。

## UI、资源与事件流

```text
PlayCtrl 创建 GamePlayPanel_027_75
    -> Level75Ctrl.Start()
        -> 绑定 Level75GamePlayPanelCtrl
        -> jump_btn 继续提供标准跳跃
        -> up_btn 独立提供机关拖拽/点击
        -> 刺墙按 029 时序开始下降
        -> 1.5s 后开放门拖动

拖动 up_btn
    -> UIDrag 更新 RectTransform
    -> DragEnd 阻断同帧 Click
    -> 玩家再次点击
        -> UI 中心转换为世界坐标
        -> SpriteRenderer.bounds 命中
        -> 锁定按钮
        -> Kill 下降 Tween
        -> 刺墙上升到 Y=7.8
        -> 红框作为子物体同步上升

门进入刺墙触发器
    -> CrushDoor
        -> 在地面 Y=-2.5764 生成 CrushedDoor_075
        -> 实例化 DoorBroken_075
        -> 播放并归位 GameCamera 震动
        -> 关闭原门
        -> 刺墙继续下降
```

运行时配置通过 `TbLevelSort[86].Path = 10075` 定位 `TbLevelData[10075].Path = Level_075`，因此 HUD 显示 Level 86 时加载本关；未改 `LevelData`/`LevelSort` 字段结构，本轮也未修改该排序映射。

本次没有新增全局事件、存档字段、配置字段、资源加载入口、analytics、广告、内购或平台分支。

## 验证记录

- 静态确认 `Level_075.prefab` 已无 `Level29Ctrl` / `KillMove` 绑定，门事件改为 `Level75Ctrl.CrushDoor`。
- 静态确认专用面板不再覆盖隐藏共享 `jump_btn`；`up_btn` 位于独立槽位，箭头为 `-90°`，其 `CustomButton`、`UIDrag` 和面板控制器引用闭合。
- 静态确认红框的父级为 `CaltropGround`，局部坐标 `(0.17, 2.71, 0)` 与墙体初始 `Y=2.2` 合成原世界坐标 `Y=4.91`。
- 静态确认压扁门使用地面顶面 `Y=-2.5764`，相机震动具有完成、禁用和销毁三条归位路径。
- 静态确认门碎片材质的 `_BaseMap`、`_MainTex` 均指向 `door_zl_02.png`，主粒子和 Clouds 均为 `SceneLayer/201`。
- 静态确认 075 专用资源 GUID 唯一，未修改共享 029、007、公共 UI 与公共粒子资源。
- 使用 Unity 6000.0.59f2 生成的 Roslyn 响应文件完整编译 `Assembly-CSharp`，结果为 `0 error`；输出仅包含项目既有警告，两个 Level075 脚本均无警告。
- YAML 引用闭包检查通过：`Level_075.prefab` 为 `257` 个定义 / `256` 个本地引用，专用面板为 `35/35`，门碎片粒子为 `8/8`，均无重复或缺失 fileID。
- Unity `Editor.log` 已记录 Level075 关卡 Prefab、专用面板、门碎片 Prefab/Material 和本地文档导入成功；未发现 Level075 相关 `error CS`。
- 075 目标文件的行尾、最终换行和 `git diff --check` 检查通过。
- Luban 使用当前 Excel 隔离生成成功；生成的 `tbleveldata.json` 与仓库版本相比仅新增 `10075 -> Level_075`，同步后配置内容一致。
- 本轮 `git diff --check` 对关卡脚本、关卡 Prefab 和专用面板通过，仅有仓库既有的 LF/CRLF 提示。
- 本轮使用 Unity 6000.0.59f2 自带 `DotNetSdkRoslyn/csc.dll` 和 Unity 生成的 `Assembly-CSharp.rsp` 完整编译目标程序集，结果为 `0 error`；输出仅包含项目既有警告，未发现 Level075 诊断。

## 未验证与回滚

- 仍需重新进入 Unity PlayMode 验证普通跳跃键、独立向上键、红框跟随、拖拽后必须再点一次、刺墙上升距离、门压碎落点、全屏震动、碎片尺度和通关链。
- 仍需 Android/iOS 横屏验证不同安全区、分辨率、触摸拖拽和低帧率下的点击隔离。
- 当前静态排序已是 `86 -> 10075 -> Level_075`；仍需在正式选关流程复验入口、前后关解锁和远端配置环境下的实际顺序。
- `S_congraktTips_Des_level_75`、`S_answer_Des_level_75` 尚未出现在 `localization.csv` / `I2Languages.asset`；通关与提示文案需由本地化配置补齐。
- 场景 Tips 当前仍使用 `S_LevelTips_Des_level_29`，因此运行时可能把 Prefab 中的“危险！”覆盖为 029 文案；本次未在需求未明确时改动策划文案归属。
- 回滚时将 `Level_075.prefab` 恢复为 `Level29Ctrl` 与原面板绑定，并删除本目录新增脚本、`GamePlayPanel_027_75`、`DoorBroken_075` 及其材质；两张策划图片可独立保留。
