# Level117「太阳太大」功能说明

更新时间：2026-08-13

## 需求背景

本关围绕“太阳太大”设计：角色离开阴影后立即开始按可调曲线减速、逐渐晒红并流汗，`5` 秒时速度降到零并死亡；遮阳伞区域可以恢复正常状态；玩家还可以拖动右上角商店圆形按钮遮住太阳，覆盖达到 `80%` 后进入日食免疫状态。角色进入遮阳区域时，Spine 叠加半透明阴影色。太阳保持在策划当前摆放位置对应的屏幕坐标，不随角色或相机漂移。商店按钮的短点击仍保留原有商店功能，只有达到 EventSystem 拖拽阈值并真正开始拖拽后才屏蔽本次点击。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_117.prefab`
- `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_117.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_117/Level117Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_117/Level117RuntimeBridge.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_117/Level117ShadeZone.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_117/Level117ShopButtonDrag.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_117/Level117ShopButtonDragHandle.cs`
- 本关素材目录：`Assets/Game/ArtSrc/Sprites/l117/`，包含伞面 `Sp_l117_zys_01`、地面阴影 `Sp_l117_zys_02`、三张汗滴原图 `Sp_l117_hz_01~03`，以及由三张原图无损横向拼接的 `Sp_l117_hz_atlas`。两套汗滴 ParticleSystem 使用同一个 `Level117_Sweat_Atlas` Unlit 材质，并从图集的三个 Sprite 切片中逐粒子随机取图。
- 关卡配置：`Luban/Configs/Datas/LevelData.xlsx`、`Luban/Configs/Datas/LevelSort.xlsx`、`Assets/Game/Configs/tbleveldata.json`、`Assets/Game/Configs/tblevelsort.json` 中的 `10117 / Level_117` 条目。

本实现使用现有 `BaseLevelCtrl`、`ActorCtrl`、`PlayCtrl`、`CustomButton`、Bear EventSystem、Spine `SkeletonAnimation`、Unity UI 和 Unity `ParticleSystem`。没有修改共享 Actor、共享 GamePlayPanel 脚本、全局事件类型、存档、货币、analytics、RemoteConfig 或平台 SDK。`GamePlayPanel_117` 是基于 `GamePlayPanel_000` 的本关名称变体，不再保存不可靠的二级 Variant 追加组件；`Level117Ctrl` 在真实 `PlayCtrl.CurrentGamePlayPanel` 创建后运行时自愈挂载并显式绑定 `Level117ShopButtonDrag`。

## 原有行为基线

- 复制关卡的基础移动、失败/成功结算和暂停流程由 `BaseLevelCtrl` 与共享 `ActorCtrl` 提供。
- 共享 Gameplay HUD 的 `shop_btn` 原本是普通 `CustomButton`，短点击进入商店。
- 场景中的太阳、遮阳伞和沙漠背景属于本关 Prefab/素材，不改变其它关卡的视觉层。

## 当前行为

### 阳光暴露与死亡

- `Level117Ctrl.Update` 在关卡处于 `PLAYING` 且角色不在遮阳区域、日食覆盖低于阈值时，使用 `Time.deltaTime` 累加 `exposureElapsed`；不检查 `ActorCtrl.IsMoving`，所以停下也会计时。
- `exposureDuration` 默认 `5` 秒；热度仍按 `exposureElapsed / exposureDuration` 线性累计，但移动速度读取可编辑的 `moveSpeedFactorByHeat`。默认曲线为 EaseInOut，从系数 `1` 平滑降到 `0`；空曲线才回退到旧线性算法，到时仍严格归零。
- 暴露达到 `exposureDuration` 后调用 `ActorCtrl.Die()`，并通过 `BaseLevelCtrl.SetCtrlable(false)`、`SetMoveInput(0)` 和速度归零锁住本关移动，交给现有死亡/失败链处理；死亡后的保持分支不再每帧调用 `StopMoving`。
- 进入遮阳伞或达到日食阈值会清零暴露计时并恢复初始移动速度。
- 角色被其它机关置为死亡时，本关会识别 `ActorCtrl.IsDied`，立即停止日晒推进和汗液；角色进入出口门并由 `BaseLevelCtrl.IsSuccess()` 判定成功后，同样清零日晒、恢复速度、停止汗液；原有关卡失败状态成立后也会停止日晒推进和汗液，避免等待重开期间重复触发死亡表现。

### 遮阳伞与角色着色

- `Level117ShadeZone` 优先使用显式 `Collider2D`；本关三把伞未配置碰撞体，因此横向范围使用已绑定的可见地面阴影 `Sp_l117_zys_02`，垂直范围仍使用伞面 `zheyang`，保证判定与玩家看到的阴影宽度一致。
- 角色进入任一有效区域后，`Level117Ctrl` 通过 Spine `Skeleton.GetColor/SetColor` 仅改变 RGB，保留角色当前 Alpha；离开后恢复初始颜色。阴影色由关卡根节点 `shadeTint` 控制，当前 Prefab 为策划已调整的黑色半透明混合值。
- 未处于阴影或日食免疫时，角色按同一个热度进度逐渐混合到 `actorHotTint`。该颜色的 RGB 是最热目标色，Alpha 是最大混合强度；进入阴影时阴影色优先，日食免疫、成功、重开、禁用或销毁时恢复初始色。
- 显式区域优先于自动伞根节点检测，避免同一场景同时存在两套判定时重复覆盖。

### 汗液与屏幕固定太阳

- 汗液使用策划在 `Level_117/hanshui1` 下制作的左右两套常驻 Unity `ParticleSystem`。控制器只负责让根节点按原摆放偏移跟随角色、播放/停发，以及按热度修改 Rate over Time；轨迹、旋转、大小、颜色、重力、排序和右侧 X 轴翻转仍由两套粒子的 Inspector 参数负责。
- 两套粒子的 Texture Sheet Animation 均使用 Sprite 模式，列表为同一张 `126x67` 无损图集中的三个 `42x67` 切片；`Start Frame` 在 `0~0.9999` 间随机，`Frame over Time=0`，因此每滴出生时随机取 01/02/03 之一，生命周期内不换图。使用同纹理图集避免 Unity 单个 ParticleSystem Renderer 无法可靠跨三张独立纹理逐粒子采样的问题。
- 每套粒子的发射率随 `exposureElapsed / exposureDuration` 从根节点可调的 `sweatMinEmissionRate=10` 平滑增加到 `sweatMaxEmissionRate=20`。进入遮阴或达到 80% 日食免疫时只停止生成新汗滴，已有汗滴继续按用户配置的轨迹和渐隐自然结束；暂停、死亡、成功、失败和重开仍立即清空。
- 初始化时将场景中 `Sun` 的当前世界位置换算为相机 viewport 锚点；`Level117Ctrl` 以执行顺序 `900` 在相机跟随脚本之后的 `LateUpdate` 反算世界坐标，HUD 日食脚本以顺序 `1000` 再读取，因此太阳相对屏幕固定且当帧遮挡计算一致。要调整最终位置，只需继续在 Prefab 中摆放 `Sun`。

### 商店按钮拖拽与日食

- `Level117ShopButtonDragHandle` 实现 `IPointerDownHandler`、`IBeginDragHandler`、`IDragHandler`、`IEndDragHandler`、`IPointerUpHandler`。指针位移未超过 EventSystem 阈值时不修改 `PointerEventData.eligibleForClick`，因此轻点仍由共享 `CustomButton` 打开商店。
- 商店按钮此前完全拖不动的确定根因是：`GamePlayPanel` 的内层 Canvas 为 World Space，但坐标换算错误读取外层 `rootCanvas`（Screen Space Overlay），得到空事件相机后换算失败，每帧只能写回原位置。当前按下和拖拽均优先使用 `PointerEventData.pressEventCamera`，兜底才使用内层 `targetCanvas.worldCamera ?? Camera.main`，与实际 `GraphicRaycaster` 相机保持一致。
- 真正开始拖拽后同时把当前指针的 `eligibleForClick` 设为 `false` 并设置 `CustomButton.IsBlockClick`，兼容 EventSystem 与同物体组件回调顺序；随后立即把按钮 `localScale` 恢复为初始化时缓存的 `initialButtonLocalScale`。拖拽期间每帧、结束和取消时都会保持/恢复该基准尺寸，避免共享按压缩放残留。拖拽开始门禁每次直接读取 `PlayCtrl` 的真实 `PLAYING` 状态，不使用可能在 HUD 初始化或 PAUSE 退出时失真的缓存。拖拽结束/取消后下一帧清除组件级点击阻断，但被取消指针自身仍保持不可点击，因此只屏蔽本次拖拽。暂停、Reset、非 `PLAYING`、关卡成功/失败或角色死亡时会取消当前指针周期并恢复按钮位置与基准尺寸；第二个指针不会接管已有拖拽，其自身点击也会被取消。
- 按钮通常受 Canvas/SafeArea 夹取；如果太阳落在横屏安全区边缘，夹取范围会仅向太阳屏幕中心扩展到可见画面边界，确保刘海/圆角设备仍能完成遮挡。Reset 恢复初始 `anchoredPosition`。
- 控制器把太阳和按钮投影到屏幕，以两个圆形的交叠面积近似遮挡率；`fullEclipseCoverage` 默认 `0.8`。覆盖计算和夹取始终补偿为按钮未按压时的基准尺寸，因此共享 `ClickScaleAnim` 把根节点缩到 `0.8` 时不会造成覆盖率跳变。未遮挡时全屏使用关卡根节点可调的 `sunHotTint`（默认 `(1, 0.55, 0.30, 0.30)`）热色；覆盖增加时同一蒙版平滑插值到可调的 `sunEclipseDarkTint`（默认 `(0.18, 0.12, 0.08, 0.46)`），并在太阳边缘渐显可调的亮黄色 `sunCoronaTint`（默认 `(1, 0.86, 0.22, 0.95)`）日冕；覆盖达到 `0.8` 时进入暗场并向 `Level117Ctrl` 报告免疫。三项颜色直接暴露在 `Level_117.prefab` 根节点 Inspector，运行时由 `BindController` 传给 HUD。
- `Level117Ctrl.Start/Update` 等待真实 `PlayCtrl.CurrentGamePlayPanel` 出现；若面板缺少 `Level117ShopButtonDrag` 就直接补挂，并通过 `BindController(this)` 显式绑定当前关卡实例。拖拽 Handle、热色层、日食暗层和日冕都由该组件在 `Awake`、`OnEnable` 或显式绑定时补建；动态视觉对象继承真实 Canvas 的 UI Layer，避免 World Space Canvas 相机按层剔除。Reset 时缓存旧控制器并清除太阳、相机和半径缓存；只有 `ActiveController` 切换为新实例后才解除重置门禁、重新绑定并恢复按钮位置，避免复用 HUD 时永久锁住拖拽或继续向旧局上报。
- `GamePlayPanel.PlayShowPanelAnim` 会先把所有按钮缩放归零再顺序放大。117 只立即缓存按钮位置；缩放基准必须等 `SequentialScaleAnim.IsPlaying == false` 且三轴均非零后才记录，基准尚未就绪时拒绝开始拖拽，避免把入场动画的零缩放缓存成“正常尺寸”并在拖拽时把商店按钮恢复成不可见。

## 数据、资源、UI 与事件流

```text
角色输入/物理位置
  -> Level117Ctrl.Update 暴露计时、减速、死亡或恢复
  -> Level117Ctrl.LateUpdate 按 Camera viewport 锚点固定 Sun

角色进入/离开遮阳伞
  -> Level117ShadeZone Collider/Bounds
  -> Level117Ctrl 注册状态 -> 清零计时/恢复速度/Spine 着色

拖动 HUD shop_btn
  -> Level117ShopButtonDragHandle
  -> Level117ShopButtonDrag 计算屏幕交叠率
  -> Level117RuntimeBridge.SetEclipseCoverage
  -> Level117Ctrl 达到 0.8 后免疫
```

- 仅订阅已有 `GamePauseEvent`、`GameResumeEvent`、`GameResetEvent`、`SwitchGameStateEvent`；生命周期使用 `EventsUtils.ResetEvents(ref subscriber)` 清理。
- 不新增存档字段或货币变更；日食覆盖、暴露计时和按钮位置均为本关运行时状态。Reset 后旧关卡实例设置 `attemptTerminating`，立即锁住输入，并拒绝 HUD 覆盖率、阴影触发和 FSM Resume/Switch 再回写，直到旧实例销毁；重建的新实例字段天然为 `false`。若 HUD 被复用，它会等待 `Level117RuntimeBridge.ActiveController` 切换后重新绑定，随后每帧以 FSM 权威状态校正 `gameplayPaused`，因此直接 `PAUSE -> PLAYING` 的重开路径也会重新执行阴影检测、死亡计时、汗液和日食计算。
- `GamePlayPanel_117.prefab` 仅保留名称变体，`m_AddedComponents` 为空。117 关卡运行时直接从当前面板按标准 `Root/shop_btn` 层级解析引用并补齐本关组件；商店按钮仍使用共享 Prefab 的点击与根节点按压反馈，本关不修改共享 `ClickScaleAnim`。日冕贴图和材质缺失时使用运行时生成的环形纹理兜底。

## 当前可调参数

| 参数 | 当前 Prefab 值 | 作用 |
| --- | ---: | --- |
| `exposureDuration` | `5s` | 离开阴影后从正常速度减到零并死亡的总时长 |
| `moveSpeedFactorByHeat` | 策划自定义曲线 | 热度 `0~1` 对应的移动速度倍率曲线，可直接调整减速手感 |
| `eclipseImmunityThreshold` | `0.8` | 日食免疫阈值 |
| `fullEclipseCoverage` | `0.8` | HUD 视觉达到全日食的覆盖率 |
| `shadeTint` | `(0, 0, 0, 0.3255)` | 阴影下 Spine 的颜色与混合强度 |
| `actorHotTint` | `(1, 0.25, 0.2, 0.8588)` | 最热目标 RGB；Alpha 为最热时最大混合强度 |
| `actorTintTransitionDuration` | `0.12s` | 正常、阴影、晒红之间的短渐变时长 |
| `shadeVerticalDepth` | `4` | 无 Collider 兜底时的伞下垂直深度 |
| `sweatParticleRoot` | `hanshui1` | 用户制作的左右汗滴粒子根节点；运行时保留初始摆位偏移并跟随角色 |
| `sweatParticleSystems` | 左右各 `1` 套 | 绑定 `hanshui1/Particle System` 与 `Particle System (1)`；右侧继续使用用户设置的 X 轴翻转 |
| `sunHotTint` | `(0.8784, 0.2577, 0.1765, 0.1490)` | 未遮挡时的热色蒙版，可在关卡根节点调整 |
| `sunEclipseDarkTint` | `(0.18, 0.12, 0.08, 0.46)` | 完全遮挡时的暗色蒙版，可在关卡根节点调整 |
| `sunCoronaTint` | `(1, 0.86, 0.22, 0.95)` | 日全食边缘的黄色日冕，可在关卡根节点调整 |
| `sunCoronaScale` | `1.8` | 日冕外框相对太阳直径的倍率 |
| `sunCoronaOuterFeather` | `0.2` | 日冕从贴边亮光向外渐弱的宽度 |
| `sweatMinEmissionRate` | `1` | 刚开始受热时每套粒子的发射率，关卡根节点统一调整 |
| `sweatMaxEmissionRate` | `30` | 最热、角色移动速度最低时每套粒子的最大发射率 |
| `Texture Sheet Animation` | 01/02/03 随机 | 每滴出生时随机选择一张汗滴图，生命周期内不换图 |

## 验证证据

- 已读取并遵循 `specs/035-postcard-guide-part/plan.md`、`BrainPunk/AGENTS.md` 与 `brainpunk-local-doc-update/SKILL.md` 的文档/验证要求。
- 已通过源码静态检查确认：暴露累计与汗液播放均不依赖 `IsMoving`；速度系数从首帧起严格线性下降；短点路径不修改 `eligibleForClick`，拖拽句柄只在 EventSystem 阈值后取消该指针点击并设置 `IsBlockClick`；拖拽使用事件实际相机；Reset 会等待新的控制器后重绑；80% 阈值、关卡根节点颜色字段和汗滴参数均已序列化。
- 已将三张汗滴设为 Sprite Single、关闭 mipmap、启用透明处理并使用 Clamp；Prefab 中三条 Sprite GUID 均可解析。五张项目 PNG 与用户给出的共享美术源执行 SHA256 比对，结果逐一完全一致；三把伞继续引用原伞面和地面阴影 GUID。
- 已通过 YAML 静态检查确认：`Level_117.prefab` 的 `BaseLevelCtrl.gamePlayPanelName` 为 `GamePlayPanel_117`，根节点绑定 `Level117Ctrl`，关卡相机、太阳、三处 `Level117ShadeZone` 与三张可见地面阴影引用存在；HUD 名称变体的 `m_AddedComponents` 为空，本关组件由 `Level117Ctrl` 在真实 HUD 上运行时补挂。
- 已使用 Unity 6000.0.59f2 自带 Roslyn 和项目 Bee 定向响应文件编译包含全部 `Assembly-CSharp` 源码的 Level117 检查程序集，结果为 `0 error`；输出仅包含工程既有 warning。
- 已执行 `git diff --check`，本关代码与文档未发现空白错误；共享记录文件仅有既有 LF/CRLF 转换提示。
- `Level_117.prefab` 当前静态统计为 `237` 个定义、`237` 个唯一 fileID、`482` 次纯本地非零引用、`0` 重复、`0` 悬空；`GamePlayPanel_117.prefab` 仅有 `1` 个 PrefabInstance 定义，所有对象目标均指向 `GamePlayPanel_000` 源 Prefab，不存在本地悬空引用。
- Prefab 中 `Level117Ctrl` 引用 `1` 次、`Level117ShadeZone` 引用 `3` 次；`Level117ShopButtonDrag`、`Level117ShopButtonDragHandle` 均由运行时按真实 HUD 补挂，`Level117RuntimeBridge` 是静态桥，因此三者不需要 Prefab 组件引用。`10117` 的关卡 ID、`Level_117` 路径和排序项各唯一 `1` 条；玩法免疫阈值与 HUD 全日食阈值均为 `0.8`。
- 用户现场反馈证明此前只检查脚本、GUID 和 Variant YAML 后即判断“拖拽/蒙版可用”的结论不成立。已进一步定位到二级 Variant 的追加组件目标是无效的间接源对象，真实 HUD 未稳定获得控制器；本轮改为当前 Level117 实例对真实 `CurrentGamePlayPanel` 自愈挂载并显式绑定。该链路已通过静态检查和编译，但仍需 PlayMode 视觉与拖拽验收。
- 回归审查同时发现 HUD 入场动画先把按钮缩放归零；已将位置/缩放基准拆开，并只在顺序缩放完成后捕获非零缩放，防止按钮首次拖拽时被恢复成零尺寸。
- 2026-08-13 现场复核发现，用户反馈“仍无拖拽/全屏热层”时运行的 `Library/ScriptAssemblies/Assembly-CSharp.dll` 时间为 `14:02:22`，早于 `Level117Ctrl.cs` 的 `14:17:36` 和 `Level117ShopButtonDrag.cs` 的 `14:25:32`；该次 PlayMode 实际未包含本轮运行时 HUD 自愈挂载、热层和汗滴修正。当前源码已用 Bee/Roslyn 离线完整编译为 `0 error`，但必须等待 Unity 完成脚本刷新并生成晚于源码的程序集后，现场结果才可用于验收。
- 按用户要求未操作 Unity 前台；Unity PlayMode、Android/iOS 横屏和真实鼠标/触摸操作尚未执行，不能据此宣称视觉或手感已验收。

## 2026-08-13：颜色可调与汗水轨迹修正

- 本轮将热色、完全遮挡暗色和日冕颜色从运行时 HUD 私有默认值上移为 `Level117Ctrl` 的序列化字段，并在真实 HUD 动态挂载后显式传递；不修改共享面板。日冕使用亮黄色独立层，对应用户截图中的暗盘边缘发光区域。
- 该轮的 SpriteRenderer 朝向方案已被后续 ParticleSystem 需求取代。当前以粒子实时速度切线旋转，详见下方“ParticleSystem 汗滴与镜像切线朝向”记录；进入阴影/日食免疫仍只停新发射，不清除已经生成的汗滴。
- 已完成源码、Prefab 字段和引用的静态检查；后台编译、Unity PlayMode、移动端横屏和真实输入/视觉仍需后续验证。

## 2026-08-13：ParticleSystem 汗滴与镜像切线朝向

- 根据本轮要求，删除 Level117 私有 `SpriteRenderer` 汗滴池，改为运行时 `hanshui` 根节点下的三个 Unity `ParticleSystem`，分别使用三张汗滴素材。该改动仅在 `Level117Ctrl` 内生效，不修改共享粒子、Actor 或其它关卡。
- 左右两侧使用相反水平初速度与相同竖直初速度/重力，产生镜像抛物线。`UpdateSweatParticleRotations` 依据粒子当前 `totalVelocity` 每帧更新图片尖端方向，与用户示意图的上升、弧顶和下落切线一致；固定随机扰动只提供个体差异，不引入连续自转。每个粒子的 X/Y 尺寸按对应 Sprite 宽高比分开设置，保留原始水滴轮廓。
- Prefab 未新增手写 ParticleSystem 节点或单粒子引用；原有三张 Sprite 与可调参数保持序列化兼容。最终源码已使用 Unity 6000.0.59f2 Bee/Roslyn 完整后台编译，结果为 `0 error / 104` 条工程既有 warning；PlayMode 真实画面、阴影停发收尾、重开及 Android/iOS 横屏仍需现场验收。

## 未验证项

- `uncertain`：太阳/按钮圆形覆盖近似与真实矩形 UI、太阳 Renderer bounds 的半径选择，是否在 16:9、超宽和不同 CanvasScaler 下与玩家看到的遮挡精确一致；代码已消除按压缩放波动并为太阳方向扩展安全区边界，但仍需设备手感校准。
- `uncertain`：橙红到黑色蒙版、日冕、地面阴影边界及角色 Spine 着色的最终观感。
- `uncertain`：5 秒线性减速、汗滴头部位置/尺寸/密度、死亡动画/失败重开完整链、成功瞬间取消拖拽、按钮基准尺寸恢复与短点击的触摸手感。
- `uncertain`：Android/iOS 横屏、多指输入、低帧率下的拖拽稳定性，以及三套 ParticleSystem 的旋转缓冲更新性能。

## 回滚路径

若需完全撤回本关行为，移除 `Level_117.prefab` 根节点的 `Level117Ctrl` 和三个 `Level117ShadeZone`，并将 `BaseLevelCtrl.gamePlayPanelName` 恢复为原面板。删除 Level117 私有脚本和本地文档即可；共享 `ActorCtrl`、`GamePlayPanel`、太阳/遮阳伞素材、存档和全局事件无需回滚。

## 2026-08-13：汗滴 ParticleSystem 常驻与 Inspector 调整入口

- 用户反馈运行时汗滴效果不便调整。原实现只在 PlayMode 中临时创建 `hanshui/SweatParticle_01~03`，退出运行后节点和运行时材质都会销毁；脚本还会覆盖 Lifetime、Start Size、Gravity、Color/Size over Lifetime、Renderer 材质与排序，因此 Inspector 调整无法稳定保存或生效。
- 当前 `Level_117.prefab` 已永久保存以下层级：`Level_117 > hanshui > SweatParticle_01 / SweatParticle_02 / SweatParticle_03`。三套节点分别使用持久材质 `Level117_Sweat_01~03.mat`，对应 `Sp_l117_hz_01~03`。每套均为 World Simulation，关闭 Loop、Play On Awake、Emission 和 Shape，由本关脚本按暴晒状态手动发射。
- 策划选中任一 `SweatParticle_0X` 后，可直接在 Particle System Inspector 调整 Main 的 Start Lifetime、Start Size 3D、Gravity Modifier、Max Particles 和 Start Color，以及 Color over Lifetime、Size over Lifetime、Noise、Trails 等模块；材质在 ParticleSystemRenderer 调整，前后层级在同节点 SortingGroup 调整。三套是独立实例，需要同步效果时应分别应用相同参数。
- 脚本仅保留发射频率、角色头部位置、左右初速度、随机种子和沿 `particle.totalVelocity` 的切线旋转。`EmitParams` 不再写 `startLifetime`、`startSize3D` 或 `startColor`，初始化和清理也不再创建、配置、销毁粒子节点或材质。因此上述 Inspector 参数是最终生效值。Main 的 Start Speed、Shape 和 Start Rotation 会被脚本的世界坐标、速度和旋转输入取代，不属于视觉调参入口。
- `sweatRotationOffsetDegrees` 默认 `-90`，用于匹配当前汗滴素材尖端朝上；若更换素材方向，可在 `Level117Ctrl` 调整。旋转缓冲按各粒子系统 `Max Particles` 动态扩容，避免旧固定上限导致粒子被截断；进入阴影/日食仍只停新发射，已有粒子继续按其 Inspector 生命周期自然结束。
- 本轮不修改共享 Actor、GamePlayPanel、Bubble 源 Prefab或其它关卡。Unity PlayMode 视觉、三套 Inspector 参数的现场手感和移动端横屏仍需人工验收。

## 2026-08-14：接入用户重做的双侧汗水粒子

### 需求与原有行为

- 用户在 `Level_117.prefab` 中新建 `hanshui1/Particle System` 与 `Particle System (1)`，完成一侧轨迹后以第二个子节点 `Transform.scale.x=-1` 镜像另一侧；每套当前 `Rate over Time=10`。
- 上一版控制器仍绑定旧 `hanshui/SweatParticle_01~03`，按计时器手动 `Emit` 并逐帧改写粒子旋转。若只换引用，新系统自动发射会与旧手动发射叠加，也会覆盖用户设置的 Velocity 和 Rotation 模块。

### 当前行为与数据流

- `Level117Ctrl.sweatParticleSystems` 现只绑定 `hanshui1` 下的两套用户粒子；旧三套节点不再参与运行时发射。控制器不再手动 `Emit`、随机选择系统或改写单粒子旋转。
- 两套 `Texture Sheet Animation` 均使用 `Sp_l117_hz_01/02/03`。`Start Frame` 为 `0~0.9999` 随机，`Frame over Time=0`，因此每滴汗出生时随机选一张并保持到消失，不会在单滴生命周期内轮播三张图。
- 控制器首次初始化时读取每套粒子 Inspector 的 `Rate over Time` 作为起点。日晒热度仍由 `exposureElapsed / exposureDuration` 计算，与角色线性减速使用同一来源；每帧将每套发射率从当前初值 `10` 线性提升到 `sweatMaxEmissionRate=20`。
- `hanshui1` 保留用户摆放时相对角色约 `(0.03, 0.37, 0)` 的世界偏移并逐帧跟随角色。脚本只改根节点位置、播放状态和发射率，不改两套粒子的 Main、Shape、Velocity、Rotation、Size、Color、材质、排序或右侧翻转。
- 进入阴影或达到日食免疫时使用 `StopEmitting`，已有汗滴自然播完；暂停、死亡、成功、失败、Reset、Disable 时继续清空。Disable/销毁时恢复 Inspector 初始发射率，避免复用后把上轮最高值当作新基准。

### 影响范围、验证与回滚

- 影响文件为 `Level117Ctrl.cs`、`Level_117.prefab` 和本关文档；引用三张既有 `Assets/Game/ArtSrc/Sprites/l117/Sp_l117_hz_01~03.png`，未修改共享 Actor、HUD、粒子材质、配置、存档、事件、其它关卡或平台链路。
- Unity 6000.0.59f2 后台 Roslyn 完整编译为 `0 errors / 104` 条工程既有 warning。Prefab 静态检查确认两套粒子除对象 ID 外配置一致、三张 Sprite 各绑定两次、左右仅保留预期的 X 轴镜像，控制器引用已切到新根与新两套系统。
- `uncertain`：未操作 Unity 前台，真实密度、三图随机观感、跟随、阴影收尾、重开及 Android/iOS 横屏仍需 PlayMode/真机验收。
- 仅回滚本次汗水方案时，恢复 `Level117Ctrl` 的旧汗水字段/方法和 Prefab 的旧三条粒子引用，移除两套 UV Sprite 随机配置即可；共享系统无需回滚。

## 2026-08-14：汗滴图集随机、角色晒红与减速曲线

### 需求与原有问题

- 汗水发射率原先只暴露最大值 `20`，最小值隐含读取两套 Particle System 的 Inspector `Rate over Time=10`，不便在关卡根节点统一调整。
- 两套 Texture Sheet Animation 虽然列出 `Sp_l117_hz_01/02/03` 并随机 `Start Frame`，但三张 Sprite 来自三个独立纹理；单个 ParticleSystemRenderer 只能稳定采样其材质绑定的纹理，因此现场始终看成固定图片。
- 角色移动速度此前严格按 `1 - exposureElapsed / exposureDuration` 线性下降，并直接写入 Actor；默认 `exposureDuration=5s`，首尾斜率立即变化，体感可能突兀。

### 当前行为与调参入口

- `Level117Ctrl` 现显式暴露 `sweatMinEmissionRate=10` 与 `sweatMaxEmissionRate=20`。左右每套粒子均使用同一日晒热度从最小值插值到最大值；运行时会保护非负值，并在最大值小于最小值时以最小值为准。
- 新增 `Sp_l117_hz_atlas.png`：按 01/02/03 顺序将三张 `42x67` 原图无缩放拼成 `126x67` Multiple Sprite，三个 Full Rect 切片共享同一纹理。左右粒子仍为 Sprite 模式、随机 `Start Frame=0~0.9999`、`Frame over Time=0`，并共同使用保持原 `Sprite-Unlit-Default` Shader 的 `Level117_Sweat_Atlas.mat`，因此每颗粒子出生时能真实随机一张且生命周期内不换图。
- 新增 `actorHotTint`。RGB 是角色最热时的目标颜色，Alpha 是最大混合强度；混合进度与汗水相同，均使用 `exposureElapsed / exposureDuration`。阴影颜色优先，日食免疫与成功时恢复初始色，暂停冻结当前红度，晒死后保持最红直到 Reset；始终保留 Spine 当前透明度。
- 新增 `moveSpeedFactorByHeat`，X 轴为热度 `0~1`，Y 轴为移动速度系数。默认曲线从 `(0,1)` 缓入缓出到 `(1,0)`，替代旧线性减速；`exposureDuration` 仍控制从开始暴晒到死亡的总时长，空曲线时回退为旧线性公式。
- 脚本仍不改用户两套粒子的 Main、Shape、Velocity、Rotation、Size、Color、排序或右侧 X 镜像；本轮只替换两套 Renderer 材质及 Texture Sheet Sprite 来源。

### 影响、验证与回滚

- 影响 `Level117Ctrl.cs`、`Level_117.prefab`、新增私有 atlas/材质和本关文档；不修改共享 Actor、HUD、配置、存档、事件、其它关卡或平台链路。
- 三格 atlas 与 01/02/03 逐像素比对为 `0` 处差异。Unity 6000.0.59f2 隔离批处理导入确认三个 Sprite 数量、`42x67` 矩形和本地 fileID 均正确；后台 Roslyn 完整编译为 `0 errors / 104` 条工程既有 warning。
- `uncertain`：按用户要求未操作当前 Unity 前台；实际三图随机分布、红色观感、默认缓动手感、阴影/日食切换、重开及 Android/iOS 横屏仍需 PlayMode/真机验收。
- 仅回滚本轮时，恢复旧三张独立 Sprite 引用与 Renderer 材质，移除 atlas/atlas 材质，并恢复旧汗水下限、Spine 调色和线性速度代码；共享系统无需回滚。

## 2026-08-14：角色颜色短渐变、日冕扩散与日晒死亡音效

### 需求与原有行为

- 角色晒红本身会随热度变化，但进入/离开阴影、达到日食免疫、成功或 Reset 恢复正常色时仍直接写入目标 RGB，状态边界会跳色。
- 旧运行时日冕的亮峰约贴着太阳边缘，但外缘只扩到约 `1.12` 倍太阳半径，截图中表现为很窄的一圈光。
- Level117 日晒死亡直接调用 `Actor.Die()`，不会经过 `BaseLevelCtrl.WaitingFail()` 的 `failed` 音效入口，因此死亡后自动重来前缺少标准失败反馈。

### 当前行为与数据流

- `Level117Ctrl` 只计算正常、阴影、晒红或日食状态的目标 RGB；`LateUpdate` 每帧统一使用 `Time.unscaledDeltaTime` 推进一次短渐变，当前 `actorTintTransitionDuration=0.12s`。渐变始终保留 Spine 当前 Alpha，不干扰死亡、进门或其它显隐表现。
- 红色、阴影、正常色之间快速来回切换时会从当前显示色继续追随新目标。成功和 Reset 也请求渐变回初始色；`OnDisable/OnDestroy` 因不再有后续帧，仍立即恢复初始色作为生命周期清理。
- 日冕外框倍率调整为 `sunCoronaScale=1.8`，运行时纹理使用短内羽化与 `sunCoronaOuterFeather=0.2` 的长外羽化。亮峰仍约在太阳边缘，外侧约到 `1.72` 倍太阳半径才完全衰减，不改变日食覆盖率或免疫判定。
- `TriggerSunDeath()` 在既有 `deathTriggered` 单次门禁内、`Actor.Die()` 前播放项目标准 `failed` 音效。手动 Reset 不走该调用，不新增音效资源、全局事件，也不改变 `GameResetEvent` 的打点、广告或重建顺序。

### 影响、验证与回滚

- 修改仅限 `Level117Ctrl.cs`、`Level117ShopButtonDrag.cs`、`Level_117.prefab` 和 Level117 文档；保留策划当前自定义减速曲线、汗水 `1~30`、热色和阴影色参数，不修改共享 Actor、GamePlayPanel、SoundsGood 集合或其它关卡。
- Unity 6000.0.59f2 Bee/Roslyn 后台完整编译为 `0 error / 104` 条工程既有 warning，Level117 无诊断。日冕半径与羽化范围已按生成公式静态复核。
- `uncertain`：未操作当前 Unity 前台；`0.12s` 的颜色手感、不同画幅下光晕实际视觉、日晒死亡音效响度，以及暂停、成功、失败重开和 Android/iOS 横屏仍需 PlayMode/真机验收。
- 仅回滚本轮时，恢复 Spine 直接写色、旧 `coronaScale=1.35` 与对称窄环生成器，移除日晒死亡处一次 `failed` 播放及三个新 Prefab 参数；共享系统无需回滚。
