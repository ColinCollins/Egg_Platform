# DigLevel_001 脚本结构记录

## 2026-10-12 两种胜利条件与统一延迟（当前扩展）

- 用户要求增加 index 匹配到达目标，并补充“当前吞噬也保持相同胜利流程”。001继续使用原全部病菌吞噬条件，根Prefab显式victoryCondition=0、victoryDelay=1；Actor源Prefab显式index=0，不改变三只病菌大小／增长、地形、Actor／Germ布局或已有GUID。
- 脚本树新增手写 DigVictoryCondition 枚举及 DigTargetTrigger 组件；控制器仍实现 IDigLevelRuntime，Actor仍继承BaseDigComp。目标脚本显式绑定所属关卡／同节点Collider2D／index，使用attachedRigidbody识别Actor并调用TryReachTarget；没有关卡专用空子类、共享Main Actor／Draw升级、DB／全局事件／UI直接调用或编辑器运行态链。
- 生命周期：001的QueueContact／FixedUpdate／OnConsumed／大小规则保持；只有在剩余病菌与动画都为0时TrySucceed→BeginVictory。到达模式独立走同编号目标→同一BeginVictory；金币跳关也复用。条件满足先锁结果、清接触、关输入／冻结物理和Tween；配置游戏秒到点再一次Resolved，玩法控制器继续原成功统计→插屏→胜利面板。插屏可使实际UI晚于1秒，不绕过广告。
- 暂停／后台停计时；恢复保持结束态冻结。失活清等待并沿原generation／Tween清理，新实例重新开始。到达已锁后旧吞噬回调拒绝，死亡已锁后目标拒绝；到达同物理步先达标清未处理接触，吞噬模式大者优先失败仍保持。此节替换下方旧“吞噬完即Submit”的即时胜利描述。
- Prefab修改仅两处模式／延迟和源Actor编号字段；其余数据规范化换行后与本轮起始HEAD相同。新脚本meta各唯一；原玩法／UI／存档／广告链及批次方法按静态证据保持。完整场景和验证边界见本关风险记录及Dig胜利条件验证记录，不能把隔离关卡当主工程001整关端到端通过。
- 结构复盘：胜利条件差异局部保留，最终收尾在原控制器内复用一个入口，减少三个成功入口重复锁定代码；没有稳定证据支持跨玩法框架，不创建共享成功条件抽象。回滚仅撤本轮模式／目标／index／统一延迟与三个Prefab字段，不回退地图／装饰并行任务或清玩家数据。
- 本轮证据：61项真实Unity隔离检查、旧Prefab缺字段导入通过，30项原源码／方法保持，Editor／Android条件编译0错误。只覆盖关卡胜负／物理／Tween及字段导入，001完整生产HUD／插屏／存档／双端仍uncertain；日志与最短复现方式见Dig胜利条件验证记录。

## 2026-10-12 空洞免费计费（当前）

- 用户要求既有空洞不耗墨；DigTerrainCtrl新增本实例CPU余沙位图，从烘焙InitialMask按GPU遮罩尺寸恢复。Stamp只有擦到新沙才返回true并同步视觉/碰撞；DigLevelCtrl据此增加世界长度与通知既有HUD。完全空章不Blit、不刷新碰撞/HUD。
- 位图按32位打包，当前1024×512约64KiB，无每章分配/GPU读回，不使用碰撞偏移或192进度决定耗墨。中心空洞但圆刷边缘擦新沙仍付费；计费单位保留采样世界长度，不按面积折算。
- 当前用户Prefab配置已改为brushRadius0.5/stepRatio0.1/细分8，有效首章最低0.05；参数、地图和FixedBrick没有由本轮修改。旧章节参数为历史快照。本轮无新脚本或本关空壳组件，只给共享擦除组件添加只读MaskResolution，原擦除方法不变。
- 70项真实隔离Unity/URP播放断言，含GPU遮罩一致性、64方向正圆、重复挖掘、跨洞、预算、门禁、奖励/评星/重建；Editor/Android宏源码编译均0错误。生产HUD/广告、002完整关卡、真机性能/iOS仍uncertain，源码编译不等于真机构建。
- 结构复盘：余沙及有效章归Terrain，计费/墨水状态归LevelCtrl，UI仍由DigPlayCtrl通知独立HUD。没有新全局事件、存档、扫描、配置/烘焙改动。验证产物及仅撤3个脚本增量的回滚见上级“已挖空区域不耗墨”章节。

## 2026-10-12 矩形预留洞绑定（当前）

- FixedBrick BaseDigComp.HoleShape显式绑定同节点BoxCollider2D，OutwardDistance0.1、IsShow=true；InitMap沿现有BuildShapeDistance/RasterHole生成轮廓外扩和凸角圆弧，Radius0.5在有Shape时不参与。用户的Collider和对象变换保留。

- 只改本关绑定与Tilemap/MapMask，无新增脚本/API、自动扫描或运行时生成。15项真实隔离编辑器/URP验证通过，保存重开和隐藏源重复生成覆盖；正式Editor与手机轮廓/性能仍待验证。

- 当前遮罩512×512不变；调清晰度建议先Mask Width/Height=1024×512并InitMap。其余6/192/0.1保留，参数作用/工作量估算/风险与回滚详见上级新增章节。


## 2026-10-12 尺寸、正圆与显示开关（当前）

- BaseDigComp新增isShow默认false；当前Actor/Germ源Prefab明确true。绑定HoleShape时读取Collider轮廓/OutwardDistance，否则读取Radius；Root障碍物可挂在Spawn Holes中。InitMap按IsShow设置active，inactive源仍可再次读取。
- Terrain移除固定cells，依Sand.localBounds与世界Transform推导碰撞范围；格中心在矩形内才填土。当前17.7×6.7、y=-0.45，保存532×202格范围。Current FixedBrick已保存的Radius0.5/HoleShape空/外扩0.1/IsShow=true被保留；实际是圆洞，外扩不作用于圆形Radius。
- Runtime仍InitializeRuntime→BindRuntime加载图/RT，再玩家Stamp；新增UV直径重载只由Dig调用，世界半径经横纵尺寸各自换算而保持正圆。旧关单参接口/软边/进度保留。
- InitMap编辑器校验、定向Shader/材质刷新改善Prefab预览；正式Editor颜色恢复由用户实测反馈确认。27项Editor/34项真实URP PlayMode与Editor/Android编译通过；原粉色唯一原因、直接首帧RT失效恢复、移动端/完整Undo等边界见风险记录。
- 同步仅Tilemap与Terrain块，69其它块逐字保留；Maps/GUID稳定。详细资源流、当前规则、验证证据与局部回滚见上级DigGame功能说明新增章节。

## 2026-10-12 编辑器地图与 BaseDigComp

- 主角/病菌继承可挂载 BaseDigComp，Radius 默认 0.75；Spawn Holes 绑定四个组件。Reserved Holes 绑定 Sand 子节点 BaseDigComp 与同节点 Hole Shape/Outward Distance，仅该组 InitMap 后隐藏，inactive 仍可读取。
- Terrain 手写 Editor partial 提供 Odin InitMap，Tilemap/洞口、Grid 比例/挤出/范围、遮罩/预览材质在 Prefab Stage 生成保存；Maps 两份资产由 Prefab 引用，首关只预烘焙原出生洞。
- InitializeRuntime → BindRuntime 接入已有图/RT，共享擦除 initialMask 恢复 GPU/CPU 初态；玩家挖掘与整关重建保留。本节替换历史运行时构图描述，不改变并行配置/选关/打点约定。
- 结构复盘：局部 Editor 地图能力，不升级共享 UI/Actor/事件；17+24 项隔离检查及 Editor/Android 编译通过。Odin/Undo、其它形状、生产 URP/双端/包体 uncertain；操作、证据与成对回滚见上级地图当前约定。

## 2026-10-12 第二轮配置接入

本关根DigLevelCtrl新增ConfigureInk(capacity,stages)，调用方DigPlayCtrl在InitializeRuntime前传入固定DigLevelData的墨水/阈值；已初始化拒绝改写，编辑器态不写入。Prefab输入引用与吞噬/大小/地形算法未由本轮改动。容量/星数的来源由第一轮Prefab默认改为运行时独立Luban表；首关仍20及0/4/10。风险为配置可能改变解题预算、远端重排进度口径和生产广告恢复，验证/回滚见上级第二轮说明与Dig选关与云控功能及风险记录。原生仅验证配置调用边界，当前同步地图烘焙和完整生产首关未由本轮验收。


## 2026-10-12 墨水、提示与星级扩展

- 首关继续使用共享 DigLevelCtrl 的挖土/批次吞噬/成长/失败唯一结果，未改 Level1 的验收玩法脚本。新增本关 Prefab 参数：maxInkLength=20、inkStarStage=(0,4,10)；当时 `hintObjects` 引用 `DigHintPath`，该绑定已由 2026-10-13 的提示对象需求替换。
- 原提示对象使用 LineRenderer/SortingGroup 且移除了 Draw 脚本和碰撞；当前改为 `DigGame_TipsObjectSubObject` 完整根对象，复用 Draw 的纯表现路径渲染和呼吸组件，仍不接入 Draw 关卡状态或碰撞。初始化隐藏，控制器恢复已购资格时显示；不写 DB、不自行挖土。出生空腔不耗墨；首章消耗 0.075 世界长度，移动按路径耗墨，耗尽物理继续。
- IDigLevelRuntime 新增墨水快照/局部 InkChanged、提示与金币跳关边界。HUD、金币/广告、资格、最高星和转场仍由控制器管理；TrySucceed 仍要求真实吞噬完成，TryCompleteByHint 为独立明确的提示跳关入口。
- 对照 Draw：保留模式控制器/关卡运行时/独立 HUD/局部事件/全黑重建职责；墨水参数采用 Dig Prefab，长度单位为世界空间。首个圆章付费是 Dig 圆刷相对 Draw 折线的必要差异，防止免费点按绕过预算。
- 75 项隔离 Unity 断言通过，包含新墨水/阈值/提示对象/跳关以及原物理与死亡/后台/重建回归；生产 UI/DB/平台为未覆盖边界。既有细分 6 和贴边参数保留；首关移动端性能、真实广告/提示交互待验收。详细证据见本轮验证记录。
- 回滚仅撤新增参数/提示节点及相应接口与控制器能力，保留现有地形/相机/用户同步资源和 Editor 数值显示，不恢复已被新要求替代的共享面板改法。

日期：2026-09-29。依据为本轮用户批准的 Dig 首关计划、最终源码与 `Assets/Game/Prefabs/DigLevel/DigLevel_001.prefab`；主工程整体流程仍待实测。

## 基线与职责

原项目无 Dig。本关参考 Draw 中央脚本规范、`Level86/DLevel_086脚本结构记录.md` 和 DrawPlayCtrl 的遮挡重建/结果归属，不修改已验收关卡。

```text
DigPlayCtrl（模式、加载、HUD、重建、存档/导航）
└─ IDigLevelRuntime ← DigLevelCtrl（关卡输入、暂停、接触批次、唯一结果）
   ├─ DigActorCtrl（动态刚体、CurrentSize/GrowthSize、成长/死亡）
   ├─ DigGermCtrl × 3（嵌套同一个病菌 Prefab、一次性认领/吸入）
   ├─ DigSizeRules（纯数值批量比较、sqrt 体型映射）
   └─ DigTerrainCtrl（同一路径擦除视觉、清 Tilemap、刷新复合碰撞）
      └─ SceneSpriteBrushClear（复用，只读）
```

脚本在上级 `DigLevel/`，玩法在 `Scripts/Play/Dig/`；没有只有类名不同的空 Level1 子类。当前唯一关卡使用通用 DigLevelCtrl，后续机制差异再在对应关卡目录局部扩展。

## 最终 Prefab 绑定与布局

### 2026-10-12 标签清理后的展示结构

- DigActor/DigGerm 的 SizeLabel、SizePlate 与首关根下的 DIG TO EAT 文字已删除；两个对象不再持有 TMP_Text。首关四处嵌套文字样式 override 同步移除，body、刚体/触发器、眼睛、currentSize/growthSize 和位置 override 均与实施前快照相同。
- 数值展示集中在 DigLevelCtrl 的 UNITY_EDITOR OnGUI 区段，读取原序列化 actor/germs，无额外相机/层级搜索或新组件。仅 Editor 播放、runtimeReady、未锁定结果的 Repaint 显示右侧调试面板；认领后的病菌不显示。玩家构建中不保留这些标签、底板或 OnGUI。
- 结构复盘：这次只将展示从实体组件移到本关已有控制器，两个对象继续只负责数值/物理/局部表现；不扩展共享 UI/Actor 或跨模式抽象。非展示业务源码快照一致，13 项静态核对及 Editor/Android 宏编译通过；主工程播放、窗口布局与双端仍为 uncertain。证据、人工验收和成对回滚见上级功能说明本次章节。

- 根节点绑定 DigLevelCtrl 与 DigTerrainCtrl。关卡控制器显式绑定正交相机、主角、三个病菌数组及地形；无运行时名字查找或层级扫描。
- 独立 `DigActor.prefab`：CurrentSize=10、GrowthSize=0、单位圆半径=0.35；根缩放为 1，动态 Rigidbody2D、圆实体碰撞；原数字节点已按 2026-10-12 要求删除。初始世界位置 `(-1.4,3.5,0)`。
- 独立 `DigGerm.prefab` 三个嵌套实例：`(-1.4,1.9)` 为 `5/+5`，`(-1.4,-0.1)` 为 `12/+5`，`(-1.4,-2.3)` 为 `18/+10`。数值、位置是实例 override，不是三份复制资源。
- 身体缩放和圆半径同用 `sqrt(CurrentSize/10)`；主角大小、病菌大小及增长收益改为 Editor OnGUI 显示，数字节点与深色底板已删除。
- 沙土可见区域为中心 8×8 正方形；隐藏 Grid 基础每格 0.2、基础范围 `(-20,-20)` 起 40×40。2026-09-30 最终实现按单轴细分数扩大索引并缩小 SandGrid 变换，不运行时修改基础 cellSize；当前 Prefab 细分 6，对应约 0.0333 世界格宽和 57600 格。静态刚体、TilemapCollider2D 合并到 CompositeCollider2D，没有 TilemapRenderer。
- 地形显式绑定沙土 Sprite、擦除组件、Tilemap、Grid、Collider、Tile 和四个出生空腔 Transform；笔刷世界半径 0.75，连续采样比例 0.1，步长 0.075。组件自带输入关闭、无软边。初版完整格保守删格已在用户授权后改为格中心判定；半径偏移默认 0，世界挤出 0.001，初始化读取、重开生效，仍非像素级轮廓。
- 中央固定砖块中心 `(0.45,0)`、尺寸 `1.65×0.6`，独立 BoxCollider2D，不属于可挖 Tilemap；左右与底边另有固定边界。首版左侧路线可不碰砖块完成教学。
- 相机正交 size=4.8；占位美术为原生圆/方形 Sprite，不引入 AI 美术。调试数字使用屏幕坐标，仅 Editor 可见；Game 视图布局尚未实测。

## 生命周期、数据与事件

1. 实例化后 InitializeRuntime 验证正数大小、非负成长、引用唯一及总增长不溢出；填土并擦出生洞。控制器先保持暂停、关闭输入。
2. 揭幕后开启物理/输入；鼠标和单指只由关卡处理，起笔过滤 UI，同一连续插值同时驱动纹理与碰撞。
3. 主角 TriggerEnter/Stay 仅排队；下一 FixedUpdate 在新模拟前裁决上一批，所有比较共享开始大小。任何较大病菌优先失败；否则先全部认领、关病菌碰撞、累加收益，再动画。
4. 吸入约 0.3 秒、成长约 0.22 秒。剩余病菌与待完成动画都为零才成功；2026-09-30 将该判定集中到 TrySucceed，不能强制跳过条件。失败先关输入/物理并撤销未完成吸入，再用 0.5 秒死亡动画提交失败。
5. 暂停保存速度、暂停局部 Tween；系统后台暂停独立叠加。成功/死亡对象不会被 Resume 重新启动物理。
6. OnDisable 增加 generation、清接触和回调、停止吸入；主角/病菌组件各自 Kill Tween。仅支持控制器销毁并重建新实例，不复用已失活的旧运行时对象。
7. 关卡只报告 Resolved；不写 DB、不打开 UI。玩法管理 DigGameResetEvent、Replay/Next、版本取消和独立 DB。失败不能到达 RecordVictory。

## 与参考关卡的差异

2026-09-30 补记：地形增加显式 SandGrid 引用与单轴细分/笔画采样比例，后续补入半径偏移与世界挤出。直接修改 cellSize 的初版细分在真实射线测量中仍有较大误差，最终改用 Grid 变换同步缩放几何并记录基础缩放，重复初始化不累乘；扩大格索引保持世界范围。默认细分 4，当前 Prefab 为用户设置的 6。本次字段不进入存档，仍是新实例重建；Stamp/FlushCollider 的局部接口及 DigLevelCtrl 输入采样调用不变。用户同步给关卡 Camera 补入 Draw 基线具备的 URP AdditionalCameraData，Agent 未修改共享 GameCamera、Main 场景或 HUD。最终重跑 48 项核心及新增 14 项贴边隔离断言通过；完整 UI 渲染尚未实测，见本轮验证记录。

- 挖土不是画物理绳子：不继承 DrawLevelCtrl，不借用画线/墨水/星级字段。
- 大小比较需延迟一个 FixedUpdate 批量裁决；这是消除碰撞回调顺序影响的必要差异。没有新增每病菌全局事件。
- 按用户后续要求，HUD 使用独立 `DigGame_GamePlayPanel : GamePlayPanel` 和同名 Resources Prefab；共享父类无 Dig 分支。副本直接隐藏左右移动、跳跃、Tips、Shop 和星星块，仅保留关卡号/Reset/Pause。子类隔离父类主线生命周期，黑幕等待绑定本次打开会话。双胜利 Prefab 保持独立。
- Reset 重建全地形与对象，未实现对象池、地形存档、多关编辑工具或通用战斗框架。

## 验证、复盘与回滚

### 2026-10-13 提示对象

- 关卡根 `DigLevelCtrl.hintObjects[0]` 显式绑定嵌套 `DigGame_TipsObjectSubObject` 的完整根对象。该根下包含提示 Sprite、`DrawTipsLineObjectRenderer` 虚线路径和 `BreathLineAlphaHandle`，购买后整组显示，初始化未购买时整组隐藏。
- 表现组件只复用 Draw 的局部渲染能力；本关仍由 `DigPlayCtrl` 管理提示资格、重建恢复和清理，不接入 Draw 关卡状态、存档或输入。提示对象不参与碰撞与挖洞。
- 隔离 Unity 已验证提示资源是 Regular Prefab、001 单一绑定非空并可实例化，实例 `HasHint=true`。正式首关画面位置、UI 购买点击及移动设备显示仍待人工验收。

- 2026-09-30 最终贴边代码实测：六倍细分、圆心 (2,2)、半径 0.75，72 方向平均绝对误差 0.01047；覆盖偏移正负、挤出方向、细分 1/2/4/6/8 和重建恢复。测试采用真实物理与 GPU mask，不能代表所有复杂笔画或真机性能。只回滚本轮贴边时撤销 DigTerrainCtrl 的策略/两个参数与 Prefab 挤出增量，不回退用户相机与其它前序修改。
- 隔离 Unity 6000.0.59f2 真实 PlayMode 38 项断言通过；包含实际挖通路线并依靠重力完成三个病菌。核心源码逐字匹配，资产引用与 Luban 输出静态检查通过。
- 最后吞噬期间的“外部死亡”用反射调用私有失败入口注入，验证结果隔离；正常首关没有额外死亡机关，不能称该场景已由真实自然碰撞复现。
- 编译只验证 Editor/Android 条件源码，未完成原主工程 UI、存档往返、资源包及 Android/iOS 真机验收。具体矩阵和日志在 `DevelopMD/验证记录/DigGame_2026-09-29/验证记录.md`。
- 结构复盘结论：维持局部职责，不向已验收 Draw 类添加尺寸/挖土能力。潜在共享候选保留在 Dig 功能说明，尚无多关证据支持抽象。
- 回滚本关需一起移除独立资源、脚本、配置和首页/控制器接入；详细顺序见上级功能说明。既有字体、Android/ProjectSettings、其它模式及只读依赖不属于本次回滚范围。
