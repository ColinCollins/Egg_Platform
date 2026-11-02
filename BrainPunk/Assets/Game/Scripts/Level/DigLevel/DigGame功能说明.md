# DigGame 模式与首关

## 2026-10-16 Actor初始半径与死亡排序（修改前核对）

- 需求：初始化不调整CircleCollider2D半径，死亡显示在SceneLayer／200。原Initialize用unitRadius×ScaleFor覆盖手调半径；Grow／ApplyScale也依赖unitRadius，若只删初始化赋值，第一次成长可能缩小碰撞体并令Body跳变。本次记录初始Collider半径，成长与Body共用相对初始数值尺寸的倍率，不改变初始Body缩放、Collider Offset、材质或刚体速度。
- 排序基线／来源：DigActor.prefab只有Spine MeshRenderer，没有自身SortingGroup；关卡Actor位于Scene排序组内（SceneLayer／1），边界子组为50。只改Renderer不能脱离祖先组；新增默认禁用的Actor根SortingGroup并显式绑定，PlayDeath启用、设SceneLayer／200和sortAtRoot=true。正常玩法沿原排序，新实例从Prefab恢复禁用；不扫描层级、不运行时添加组件、不改父级Scene排序。
- 维护边界：DigActorCtrl及Animations为手写partial，无生成器；unitRadius仅本类使用，移除该已不再作为权威来源的字段及基础Prefab对应值，Circle.radius成为初始半径来源。所有现有Dig关卡中的Actor控制器均为嵌套stripped引用，girl／bad继承基础Actor。只改本类与基础Actor，保留用户未提交的资源设置。Grow仍采用0.22秒OutQuad；失败跳／落轨迹、FailArea速度接管、暂停、结果回调、重建／广告／统计保持此次修改前行为。
- 验证与风险：检查初始数值10／40及手调半径、非等比Body缩放、成长中断／暂停／死亡、上下跳两种死亡排序、新实例恢复；原生Prefab导入读取新组件绑定，并在父SortingGroup与遮挡物场景实际渲染排序。Editor／Android条件编译；正式关卡、手机及广告流程未覆盖则标uncertain。使用已有模块文档，CodeGraph不可调用、037计划缺失，以当前源码与Prefab为证据。
- 回滚：只撤本次初始半径倍率改法、死亡SortingGroup字段／设置、基础Prefab新增组件和引用；恢复旧unitRadius字段／值。不覆盖用户Circle半径、身体缩放和关卡布局。后续旧章节中的“初始化仍设置Circle半径”已由本节替代。

### 当前实现、使用与验证

- DigActorCtrl.Initialize只缓存Circle.radius，当前基础Prefab的0.54原样保留；初始currentSize不是10时也不重算半径。Grow从当前半径比例续接，目标为初始半径×sqrt(当前数值／初始数值)，Body使用同一倍率；Offset和默认三轴缩放不被初始化改写。unitRadius不再作为Actor配置，Germ独立逻辑未修改。
- DigActor.prefab新增根SortingGroup并绑定deathSortingGroup，默认禁用；girl／bad与关卡嵌套实例继承。PlayDeath两种入口均启用该组，使用SceneLayer／200、sortAtRoot=true，所以能够独立于父Scene组排序。不会抬高其它角色／场景；新关实例从Prefab恢复原正常排序，不新增对象池复用流程。
- Unity6000.0.59f2隔离36项通过：数值10／40初始化保留手调半径和非等比Body缩放、成长首帧／中断连续、相对增长、Offset保持、暂停恢复、两种死亡入口排序／尺寸／碰撞关闭／原时长／唯一回调、禁用取消、新实例恢复。实际渲染验证父SceneLayer／1下的角色，死亡前被外部80层遮挡，死亡后显示在其前方；采用测试Sprite验证排序机制，不将此当作正式Spine画面验收。
- 基础Prefab原生导入确认新排序组件引用闭合、默认禁用、SceneLayer200、sortAtRoot=true、Circle.radius=0.54；剔除本次排序组件／引用及旧unitRadius移除后，Prefab与编辑前快照一致，保留用户其它设置。生产Actor脚本与测试副本字节一致。Editor／Android1226源文件编译均0错误（113／118条现有警告），代码／文档diff检查通过。
- 证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-actor-radius-sort-20261016/`中的checks.txt、prefab-checks.txt、unity.log、before／after-True.png、before／after-False.png及compile-editor／android.log。正式关卡遮挡、Spine美术与手机仍uncertain；本轮未改FailArea原有Tween速度接管。

## 2026-10-15 墨水来源开关与Reset对齐Draw（修改前核对）

- 需求与事实：用户修改DigLevelCtrl的Max Ink Length后Reset未生效，并要求核对Draw一致性。DigPlayCtrl.BuildLevelAsync在每次实例化后调用ConfigureInk，Dig当前无条件接受Luban容量／星级；DrawPlayCtrl也注入表值，但DrawLineFeature.isConfig默认false，ConfigureInk关闭时直接返回。因此此前“运行时一律配置覆盖”与用户要求的Draw行为存在偏差，当前修复以Draw已有来源开关为准。
- 来源：DrawLineFeature.cs的isConfig／ConfigureInk、DrawPlayCtrl.BuildLevel与RestoreRewardInkForNewRuntime、DigPlayCtrl.BuildLevelAsync／OnReset／OnReplay、DigLevelCtrl.ConfigureInk／InitializeRuntime／AddRewardInk、DigGame_GamePlayPanel.BindRuntime／SetInkState；历史记录2026-07-28“DrawLineFeature墨水配置来源开关”和“Draw HUD墨水配置来源唯一化”。CodeGraph命令不可用，知识库没有相关命中，按当前源码和本地记录核对。
- 修改边界：仅给手写DigLevelCtrl添加相同的序列化isConfig（默认false）与ConfigureInk门禁，保留已有运行态／初始化保护。关闭时以已保存Prefab容量和阈值为准，打开时才接受DigLevelData；不删表字段或注入调用，不改生成器／远端加载／统计／广告／重建顺序。此类不是生成文件，无绑定生成器维护字段。旧Prefab没有isConfig时默认关闭；003的30、005的100、007的25将实际生效，正是此次来源规则修复的影响。
- 完整链路与验证：InitializeRuntime仍归一化容量／阈值、清空已用量；同关已领取奖励由DigPlayCtrl在新实例初始化后加一份新的基础容量，HUD和评星只读该实例InkState。验证默认关／显式开、初始化后拒绝覆盖、新实例重建读新Prefab值、奖励重建、UsedInk清零及Stage快照；Editor／Android编译。修改运行实例但未保存到Prefab不能跨重建保留，表对象热更新不在本次范围。正式HUD点击、资源模拟加载及真机仍待验证。
- 回滚：仅移除新增isConfig字段及对应门禁，恢复无条件表覆盖；不覆盖用户关卡Prefab或其它既有工作区修改。此节替代下文旧记录“运行时由配置覆盖容量／评星”的当前规则，旧段落保留作为历史。

### 实现与验证结果

- DigLevelCtrl已增加isConfig=false，ConfigureInk仅在运行态、尚未初始化且开关打开时覆盖容量／阈值；原方法注释“进关注入Luban”未描述来源门禁，已按实际行为更新。没有修改DigPlayCtrl、HUD、Prefab、Luban表或生成代码。
- 真实Unity隔离场景13项通过：默认Prefab100不被表20覆盖；实际TryDigSegment耗墨；奖励容量200且InkChanged快照一致；保存Prefab改150后重新加载／实例化读150并清空消耗，阈值同步更新，恢复奖励后300；显式开启配置时使用20及表阈值，奖励40；初始化后拒绝再次覆盖。采用生产DigLevelCtrl／Actor，地形为桩，未运行正式资源宿主和HUD按钮。
- Editor／Android主程序集1226源文件条件编译均0错误（113／118条现有警告）。日志和13项结果在 `C:/Users/admin/AppData/Local/Temp/brainpunk-dig-ink-source-20261015/`：checks.txt、unity.log、compile-editor.log、compile-android.log。正式游戏Reset点击、资源缓存、广告服务与Android／iOS画面仍uncertain，不能用上述验证宣称两玩法整体一致。
- 当前使用：编辑并保存关卡Prefab的Max Ink Length／Ink Star Stage，保持Is Config关闭；待Unity编译完成后重建关卡。需要表控制的关卡才显式开启Is Config。运行中只改Clone实例的Inspector值不会被保存到下一次新实例。

## 2026-10-15 第1～6关背景Transform对齐（修改前核对）

- 用户指定以DigLevel_005的Sp_wd_bg_01为准，统一第1～6关同名对象的本地位置、旋转与缩放。当前参考值：位置(0, 0.3, 0)、旋转(0, 0, 0)、缩放(1, 3, 1)。只调整目标Transform，不改Sprite、材质、父级或其它关卡内容。
- 本地证据：DigLevel_005.prefab的Transform 5950496799298010813；DigLevel_004同节点当前位置Y=0、缩放Y=2.1。用户新建DigGame_Bg.prefab中的目标节点已符合参考值，第001／002／003／006关引用该Prefab且没有目标Transform覆盖，因此不重复写入这些关卡或共享资源。
- 修改位置与边界：只补齐004尚存的两个差异；编辑前再次核对文件当前值，避免覆盖Unity中的并行编辑。验收逐关解析本地节点或嵌套Prefab及其覆盖，确认六关目标Transform等于005，并检查本次只改变两项数值。没有执行Unity视觉验收，画面结果仍为uncertain；回滚仅将004位置Y改回0、缩放Y改回2.1。
- 执行结果：004已补齐两个数值，六关Prefab／嵌套源与覆盖的静态核对全部通过；001／002／003／005／006和共享DigGame_Bg未由本次改写。没有新增脚本、资源、绑定或运行时流程。

## 2026-10-15 BaseDigComp只控制碰撞体（修改前核对）

- 用户要求：BaseDigComp不再按开关控制GameObject.active，只控制对应Collider2D.enabled。原行为来自手写 `DigTerrainCtrl.Editor.ApplySourceVisibility`，出生洞和预留洞在InitMap完成各自栅格化后调用；BaseDigComp本身仅存配置，运行时BindRuntime不遍历洞源。
- 修改边界：保留序列化字段isShow及其已有值，只将Inspector名称／说明改为“烘焙后启用碰撞体”。InitMap仅修改显式绑定的HoleShape.enabled，记录Collider Undo／Prefab override／dirty；未绑定HoleShape的Radius圆洞不修改任何对象或碰撞体。原activeSelf不变，先前已经被隐藏的对象不自动激活；需要显示时由制作人手动开启。
- 来源／影响：BaseDigComp.HoleShape／IsShow、DigTerrainCtrl.Editor.ValidateHoleSources／RasterReservedHole／BuildShapeDistance／InitMap。从Collider序列化几何计算洞口，不依赖enabled或物理bounds，因此关闭Collider后仍能重复烘焙。保留原仅显式InitMap生效时机，不添加OnValidate、运行时扫描或新绑定字段，不改Actor／Germ初始化、失败／吞噬碰撞状态。
- 验证：核对两类洞源调用链、不再SetActive、无绑定保持原状、开启／关闭Collider且activeSelf保持、重复读取禁用Collider几何，以及Editor／Android编译。正式Inspector点击／Undo／保存重开与整关运行仍待验证；本次不批量重新烘焙或修改用户Prefab。回滚仅恢复本轮Collider开关入口与对应说明，原序列化字段无需迁移。

### 当前结果与使用

- 已修改BaseDigComp、DigTerrainCtrl及其Editor文件：现有Is Show在Inspector显示为“烘焙后启用碰撞体”，执行InitMap时只切换绑定Hole Shape的enabled。空HoleShape不做切换，activeSelf在两种开关值下都保持制作人的现有设置。以前已被隐藏的节点需手动开启一次。
- 两类洞源调用均改为ApplySourceColliderState，记录对象改为Collider；新增Prefab实例属性记录，不新增运行时入口。旧注释／Tooltip中的“设置active”与新需求冲突，因此仅更新这些关联说明，isShow字段名和原值保留。
- 静态核对通过：入口无SetActive、两处调用覆盖、未绑定直接返回、禁用Collider继续使用序列化轮廓读取。Editor／Android1225源文件条件编译均0错误（113／118警告），定向diff检查通过；日志在 `C:/Users/admin/AppData/Local/Temp/brainpunk-dig-collider-toggle-20261015/compile-editor.log` 与 `compile-android.log`。没有执行正式Inspector点击、Undo、保存重开或完整地图烘焙，这些仍为 `uncertain`。

## 2026-10-15 挖土圆粒子（修改前核对）

- 需求与基线：挖到沙土时在手指对应的挖掘位置喷出简单圆形碎土；原流程没有挖掘粒子。保留沙土擦除、碰撞、耗墨、评星、两种胜负条件及统计／广告／Reset流程。
- 来源：手写 `DigLevelCtrl.BeginStroke／ContinueStroke → TryDigSegment → DigTerrainCtrl.Stamp` 已完成屏幕到世界坐标转换；Stamp仅在擦到新沙时返回true。暂停／后台通过ApplyPause、关闭输入通过EndStroke、销毁通过OnDisable收口。无需新增全局事件、输入监听、生成字段或Shader。
- 拟修改：新增独立 `DigGame_DiggingParticles.prefab` 与圆粒材质，复用现有DigCircle纹理和Sprites/Default；绑定到现有普通Dig关卡根下，避开Sand拉伸。DigLevelCtrl显式持有ParticleSystem及单次数量，在每段有效挖掘的最后实际采样点Emit，空洞／越界／无墨／暂停不发射。自动Emission关闭，粒子使用世界空间，旧粒子不随手指拖动；暂停冻结粒子，失活清空。
- 验证与风险：用隔离Unity生成并检查原生ParticleSystem资源，检查世界坐标、大小／寿命／颜色、暂停与停止清理及Prefab引用，执行Editor／Android编译。正式关卡触控手感、遮挡、不同分辨率与真机性能仍需观察；每关一个粒子系统、上限128粒，不新增逐帧实例化或物理碰撞。当前CodeGraph不可调用、037计划缺失，按现行源码与本地文档核对。
- 回滚：移除DigLevelCtrl新增粒子字段／发射与生命周期调用，移除关卡新增的独立粒子节点和绑定，删除本次新增特效Prefab及材质；其它资源和已有工作区改动保留。

### 当前实现、用法与验证

#### 粒子不可见：现场反馈后的修正核对

- 用户反馈实际挖掘看不到粒子。事后补查001与当前日志进入的003资源：Sand虽然自身为Default／0，但归属于Scene父节点的SortingGroup（SceneLayer／1）；根下粒子Default／10仍在整个Scene组之后。上一版“在Sand和装饰上方”的判断漏掉祖先排序组，撤回该结论；孤立预览没有场景遮挡，不能证明游戏内可见。
- 拟仅将共用粒子Prefab的Renderer改为SceneLayer／2，使其位于Scene组之前；保留粒子位置／大小／速度、八关绑定与发射代码。先在隔离Unity复用项目排序层及SceneLayer／1祖先结构，对照旧Default／10与新SceneLayer／2实际渲染，再核对绑定关卡的组值。正式触控与手机渲染仍待现场验收。
- 定向回滚仅恢复粒子Renderer的排序三字段，不改场景排序组或其它粒子参数。
- 实施结果：只将 `DigGame_DiggingParticles.prefab:4810` 的Renderer改为SceneLayer／2；001～007及012的Scene祖先组均核对为SceneLayer／1，原显式绑定继续继承共用资源。
- 验证：复用项目TagManager，在隔离Unity建立相同祖先排序结构并做相同相机／粒子状态的前后渲染，旧层级可见粒子像素0，新层级新增7008个可见像素；`sorting-before.png`／`sorting-after.png`／`sorting-checks.txt`／`unity-sorting-rerun.log` 位于前述TEMP证据目录。第一次夹具没有同步SortingGroup便Camera.Render，导致未复现；显式调用UpdateAllSortingGroups后重跑通过，失败日志保留。未修改C#，未重复编译；正式Game窗口和真机仍未实测，不把隔离渲染等同于完整关卡验收。

- 资源：`Assets/Game/Prefabs/DigLevel/DigGame_DiggingParticles.prefab`、`Assets/Game/ArtSrc/Sprites/Dig/DigGame_DirtParticle.mat`；复用既有DigCircle贴图。001～007及012各新增一个嵌套实例，绑定根DigLevelCtrl的“挖土粒子”，TBC不接入；每关仅新增26行绑定／嵌套引用，不复制整份粒子数据。原有地图、Actor、提示和其它用户修改按改前快照逐字还原对照保持。
- 默认表现：每次有效输入段4粒，棕色 `#835233` 至土黄 `#D8AE59`，大小0.06～0.15世界单位，寿命0.28～0.5秒，向左右／上方溅出，重力下落并缩小淡出。世界空间、Shape缩放模式，排序SceneLayer／2，在包含沙土与装饰的Scene祖先排序组（SceneLayer／1）前方；最大128粒，自动Emission与Shape关闭，Play On Awake关闭，无粒子碰撞。
- 调整：打开上述特效Prefab，在Main调整Start Color／Start Size／Start Lifetime／Gravity Modifier，在Velocity over Lifetime调整左右与向上速度；单次数量在关卡DigLevelCtrl的“单次挖土粒子数”调整。新建关卡时将特效Prefab放在关卡根下并显式绑定，勿放到非等比拉伸的Sand下，勿打开自动Emission。未绑定的旧资源仍可运行但不显示粒子。
- 调用与清理：TryDigSegment仅在Stamp成功后记录实际末点，在FlushCollider后发射；墨水末段裁剪使用裁剪后的坐标。旧洞、原地持续按住、墨水耗尽、输入关闭不会发射；松手后已产生的粒子自然消散。ApplyPause同步暂停／恢复已有粒子；初始化和OnDisable停止并清空。没有改输入／墨水计算、碰撞、存档／云控／统计或重建顺序。
- 验证：Unity6000.0.59f2原生资源生成与渲染预览通过，检查手动发射数量、世界空间不随发射器位移、暂停恢复、清空和寿命结束后不继续发射；真实DigLevelCtrl＋原生ParticleSystem的隔离PlayMode10项通过，覆盖有效沙、空洞、原地按住、暂停恢复、关闭输入、无墨、末段坐标裁剪与关卡失活。该夹具的地形返回值是隔离桩，不等于正式地形／HUD／触控实测。Editor／Android各1225源文件条件编译0错误，警告分别113／118。
- 证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-particles-20261015/` 内的 `asset-checks.txt`、`runtime-checks.txt`、`binding-checks.txt`、`particles-preview.png`、`unity-assets.log`、`unity-runtime.log` 和两份编译日志。全目录diff检查存在任务前Unity序列化空白警告，不改无关资源；本次新增绑定逐字回退对照和定向脚本检查通过。
- 剩余风险／验收：正式关卡相机与UI遮挡、真实触屏快划、不同分辨率、原生广告恢复及Android／iOS真机性能仍 `uncertain`；需实际挖掘、空洞划动、暂停恢复和Reset各观察一次。孤立预览只证明粒子资源能渲染，未宣称已完成正式画面验收。

## 2026-10-15 提示对象自动收集：当前用法与结果

- 两种胜利模式点击“按通关条件收集”都会填充“提示对象”。已有 `DigGame_TipsObjectSubObject` 源Prefab根已挂 `DigHintObject`，其关卡实例可被收集，名字修改不影响识别；新制作的自定义提示，在包含图标／路径的完整根节点挂同一标记即可。标记没有运行时回调，不负责显示或获取资格。
- 包含隐藏对象，排除嵌套其它关卡和当前关卡根；保留本关有效的手动提示引用，合并新标记对象并去重、移除空或已移出本关的引用。按钮不改变任何对象 active，不触发购买或地图刷新；编辑态保存Prefab，Editor播放态只改当前实例。
- 修改范围：DigLevelCtrl收集块、新增DigHintObject.cs／meta、提示源Prefab根标记。基础提示组件fileID闭合／唯一性、脚本diff和两模式入口静态核对通过；1225源文件Editor／Android编译均0错误（113／118条既有警告）。日志在 `C:/Users/admin/AppData/Local/Temp/brainpunk-dig-hint-collect-20261015/`。Inspector点击、保存重开及真实提示购买显示未实测，仍 uncertain；未批量改写关卡Prefab或提示美术。

## 2026-10-15 提示对象自动收集：修改前核对

- 用户要求现有“按通关条件收集”同时获取提示对象。原 hintObjects 是手工 GameObject 根引用；InitializeRuntime 隐藏、DigPlayCtrl 依据会话资格调用 ShowHint，HasHint 认可至少一个有效对象。当前001仍有空引用，其它关卡存在手工引用，不能因新增收集误删自定义提示。
- 来源／资源边界：DigLevelCtrl.hintObjects／HasHint／ShowHint／CollectLevelObjects；模块10-13提示适配记录、DevelopMD/GameTips_Logic.md和Editor约束；DigGame_TipsObjectSubObject.prefab根只有Transform，路径Renderer位于子节点，直接收集Renderer会遗漏同组图标。现有root没有可复用识别组件。
- 拟改：为完整提示根增加无生命周期回调的 DigHintObject 标记，接入已有提示源Prefab；两种玩法的按钮都按组件收集，含inactive并排除最近所属DigLevelCtrl为其它关卡的对象。合并保留本关有效手工引用并去重，移除空／已移出本关的引用。不用节点名称、路径Renderer子物体或整关根来替代提示根；不新增运行时扫描、金币／广告／存档／全局事件逻辑。
- 保留原Undo／Prefab override／场景dirty和Editor播放可点击行为；按钮只收集，不自动显示提示、不购买或改变激活状态。预定验证为字段／Prefab引用和Editor／Android编译，人工Inspector点击、保存重开和提示购买显示仍 uncertain。不新增测试工具。回滚仅撤提示标记组件／脚本及本次收集分支，保留用户提示美术和其它工作区改动；相关代码是手写，不经生成器覆盖。

## 2026-10-15 ReachTarget 自动绑定：当前用法与结果

- 关卡 DigLevelCtrl 选择 `ReachTarget`，点击现有“按通关条件收集”：除了收集 Actors，还会把本关全部 `DigTargetTrigger.Level` 绑定到当前控制器。包括隐藏的 SuccessTarget／GoalTrigger；按组件识别，不依赖命名。嵌套其它 DigLevelCtrl 的目标排除，已正确绑定的目标跳过。只更改目标的 Level，不重设 index、Collider或表现回调。
- 使用 SerializedObject.ApplyModifiedProperties 写引用并记录 Prefab override，复用按钮原有 Undo／场景 dirty；编辑态点击后需保存 Prefab。Editor播放态可以操作当前实例，退出播放不自动持久化。吞噬模式保持原 Actors／Germs 收集。
- 静态核对目标脚本私有字段、基础／两变体 Prefab 和分支范围，脚本 `git diff --check` 通过；生产1224个源码 Editor／Android条件编译均0错误，分别113／118条既有警告。日志目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-target-collect-20261015/`。尚未人工点击Inspector、执行Undo或保存重开，以上运行效果为 uncertain，不能用编译替代编辑器验收。没有自动执行收集或批量改写用户关卡资源。

## 2026-10-15 ReachTarget 自动绑定：修改前核对

- 需求与原行为：用户要求扩展 DigLevelCtrl 现有“按通关条件收集”，ReachTarget 时自动寻找 SuccessTarget 并绑定所属关卡。现按钮只重填 Actors，吞噬模式另收集 Germs；目标仍须逐个绑定。SuccessTarget 基础 Prefab 的 GoalTrigger 子节点已有 DigTargetTrigger，triggerCollider 已绑定、level 为空；两个目标变体继承基础结构。
- 来源／维护边界：DigLevelCtrl.CollectLevelObjects；DigTargetTrigger.level／ReportArrival → TryReachTarget；DigGame_SuccessTarget.prefab。按 DigTargetTrigger 类型查找，而非依赖 SuccessTarget／GoalTrigger 节点名字，包含 inactive，最近所属 DigLevelCtrl 必须是当前关，排除嵌套关卡。仅显式 Editor 按钮操作，无 OnValidate／运行时扫描、DB／生成代码或新实例创建。
- 拟改位置与影响：仅在现有 CollectLevelObjects 的 ReachTarget 分支通过 SerializedObject 写目标的 level，沿用 Undo／Prefab override／场景 dirty；目标编号、Collider、UnityEvent、地图和吞噬模式不改。播放态按钮继续只改当前实例，不初始化或重建运行态角色列表，退出播放不自动保存。
- 验证范围：静态核对目标字段与 Prefab、隐藏／嵌套边界、两模式分支、Editor API 隔离和生产 Editor／Android 条件编译；本轮不新增测试工具。Unity Inspector 手点、Undo和保存重开未实测，标记 uncertain，验收步骤为 ReachTarget 点击收集后检查各 GoalTrigger.Level，再撤销和保存重开。回滚仅撤此分支及对应注释。CodeGraph 工具不可调用、037 plan 缺失，沿当前源码和既有模块／Editor约束核对。

## 2026-10-15 Body 默认缩放：修正结果

- 初始化不再写 Body.localScale，保留 Prefab／关卡实例配置的三轴 scale；首次初始化缓存原始 scale 与当前数值对应的尺寸。后续吞噬成长只乘相对初始数值的尺寸倍率，非等比缩放、负值翻转和 Z 比例保留，Circle 半径继续沿原数值映射。未改任何 Prefab 的 scale。
- Unity 隔离 PlayMode 56 项通过：包含上一轮36项 Spine／生命周期检查，以及本次20项初始值保留、初始数值10／40、三组等比／非等比／负缩放、零增长和中断后连续成长检查。生产 Editor／Android 条件编译均0错误；证据在 `DevelopMD/验证记录/DigActorSpine_2026-10-15/scale-checks.txt` 和同目录补充记录。正式关卡画面／手机观感未验证，仍为 uncertain。

## 2026-10-15 Body 默认缩放：修改前核对

- 用户指出 Actor Body 默认 scale 被改写，明确要求保留配置值。本次属于上一轮 Spine 适配遗漏的事后补查：基础 Prefab 当前 Body.localScale=(0.35,0.35,0.35)，但 Initialize 调用 ApplyScale，后者用 Vector3.one × unitRadius × 2 × ScaleFor(currentSize) 覆盖三轴；currentSize=10、unitRadius=0.35 时运行态变为0.7。源码能证明覆盖，不能据此断言用户现场数值恰好为1的全部来源。
- 原流程／边界：DigLevelCtrl.InitializeRuntime → Actor.Initialize；唯一生产 Grow 调用来自确认病菌认领的物理批次，Tween 从现有 Circle 半径插值，死亡会 Kill 成长 Tween 并保留当帧 Body 大小。动画 partial 只控制 Spine 轨道，不写 Transform.localScale。相关字段均手写，不涉及生成器。已核对当前模块／失败记录、Prefab 和同日文档。
- 拟改：Actor 初始化保存当前 Body.localScale 和初始数值尺寸，初始化只沿原公式设置 Circle 半径；成长以“当前数值尺寸／初始数值尺寸”乘原始三轴缩放，保留非等比、翻转和 Z 值。保留碰撞半径映射、增长量、Tween 时长、动画状态、死亡与重建入口；不修改任何 Prefab scale 或 Germ 规则。原 Body 字段“单位身体”注释已不符合用户的可配置缩放需求，定向更新。
- 验证：初始数值10／40、0.35／非等比／负缩放、零增长、连续成长中断、碰撞增长、死亡保持、暂停和新实例重建；沿现有真实 Unity／DOTween／Spine 隔离工程验证，并做生产编译。完整关卡观感／真机仍 uncertain；回滚仅撤缓存和 ApplyScale 比例改法，保留 Spine 工作与用户关卡资源变化。

## 2026-10-15 Actor Spine：当前用法与验证

- 动画逻辑位于 `DigActorCtrl.Animations.cs`，使用 Bear FSM，默认 `idle`；刚体线速度超过 Inspector“滚动速度阈值”（默认 0.1 世界单位／秒）进入 `scroll`，降到一半以下退出。ReachTarget 按住画线且静止时 `nervous`，移动优先，松手恢复基础状态。吞噬认领后的 Grow 播 `eat`，同一次 eat 尚未完成时不重播／不排队；完成后按当前移动与画线状态恢复。
- 基础 `DigActor.prefab` 显式绑定根上的 `ActorSpineCtrl`，其已绑定 Body 上的 SkeletonAnimation；启动／预览动画改为循环 idle。girl／bad 变体继续继承此绑定，保持用户皮肤覆盖。未增加名称查找、全局事件、生成器或共享 ActorSpineCtrl 修改。旧无 Spine 角色可留空并维持原玩法；已绑定但动画资源缺失时初始化明确报错，避免胜利无限等待。
- 自然吞噬全清后所有参与角色同时播 `hiccup`，刚体、碰撞结算和输入保持锁定。原胜利延迟与动画并行，最后一个 hiccup 完成且延迟到期才发唯一 Resolved；资源当前 eat=0.6s、hiccup=1.4s，未额外串行叠加一秒。ReachTarget、提示跳关、GM 胜利继续原延迟；菜单／后台暂停动画时间，禁用解除回调，generation 拒绝退出后的旧结果。死亡仍为保持尺寸、上跳后坠出画面；不新增死亡动画。
- 编译：生产 1224 个 C# 源文件，Editor／Android 条件均 0 错误（分别 113／118 条现有警告）。Unity 6000.0.59f2 隔离 PlayMode 使用真实 Spine JSON／Atlas／DLL、ActorSpineCtrl、Bear FSM、DOTween 与当前 Dig 脚本，36 项通过，详见 `DevelopMD/验证记录/DigActorSpine_2026-10-15/验证记录.md`。覆盖状态优先级、不重播、暂停／后台、多人嗝等待、零／非零延迟、退出取消、旧角色和原死亡链。
- 风险／未验证：测试的地形是边界替身，物理接触由 QueueContact 输入，Spine 使用真实 SkeletonAnimation.Update 可重复推进；未实测完整生产关卡画面、设备手势与物理手感、原生广告、IL2CPP／iOS／Android 真机，标记 uncertain。节点已加 Preserve 防止反射构造被裁剪，但条件编译不能代替 IL2CPP 打包验证。基础 Prefab 及变体引用另作静态核对，不将程序化 Spine 实例测试宣称为 Prefab 画面验收。
- 回滚：定向移除新 partial／meta、Actor 中的动画接线、关卡 hiccup 门禁及基础 Prefab 新绑定／默认动画；保留用户已添加的 Spine 资源、ActorSpineCtrl、皮肤变体、003 调整和其它工作区改动。此次修改旧注释仅因原“0 秒立即成功／仅死亡动画暂停”描述已与新行为不符。

## 2026-10-15 Actor Spine：修改前核对

- 需求与基线：用户已给基础 DigActor 添加 SkeletonAnimation／ActorSpineCtrl 和三种皮肤；当前脚本只控制物理、成长与死亡 Tween，Prefab 预览默认 nervous，没有动画状态管理。要求默认 idle、移动 scroll、吞噬 eat 且播放中不重复触发；吞噬全部完成后 hiccup 播完才结算；ReachTarget 画线且未滚动时 nervous。
- 来源与维护边界：DigActorCtrl.Initialize／Grow／SetFrozen／PlayDeath；DigLevelCtrl.FixedUpdate／OnConsumed／BeginVictory／ApplyPause／BeginStroke／EndStroke／OnDisable；ActorSpineCtrl.PlayAnimation／AnimationState；基础 DigActor.prefab 及 girl、bad 变体；Qboxy_round.json 已含五个所需动画。现有 Rigidbody2D 锁 Z 旋转，不能仅用角速度判滚动。Bear.Fsm.StateMachine 支持显式 Inject／Apply，Dispose 不调用当前节点 OnExit，必须主动解绑 Spine Complete。
- 拟修改：用 DigActorCtrl.Animations.cs partial 隔离 Bear FSM，显式绑定已有 ActorSpineCtrl；用刚体移动速度切换 scroll，阈值可调并带回差。一次性动作优先于移动／紧张；eat 播放期间合并其它吞噬触发，完成后恢复当前基础状态。画线状态直接读取所属 DigLevelCtrl 的局部属性，不增加全局事件。无 Spine 的旧角色继续保留原流程，不运行时扫描绑定。
- 胜利与生命周期：仅自然吞噬全清对全部参与 Actor 播一次 hiccup；先锁玩法，原 victoryDelay 与 hiccup 并行，二者都完成后才沿 Submit → DigPlayCtrl.OnResolved → 统计／广告／胜利面板结算。ReachTarget、GM、提示跳关继续使用原延迟。Spine 与物理／Tween 分开暂停，避免胜利锁把 hiccup 一起停住；菜单／后台暂停 Spine，退出解绑回调并用关卡 generation 拒绝晚到结果。死亡保持已验收的上跳下坠、尺寸与相机边界。
- 验证范围：实际 Spine 动画与 Bear FSM 的 idle／scroll／nervous 转移、连续吞噬不重播、hiccup 完成门禁、多角色、暂停／恢复、退出取消、失败优先、旧无 Spine 角色、变体继承绑定；Editor／Android 编译。主工程完整 UI／广告与移动真机观感尚为 uncertain。CodeGraph CLI／MCP 不可用、037 plan 缺失，以现有源码／Prefab／模块文档补足；没有独立 DigActor Spine 旧文档。仅修改手写 Dig 模块与基础 Actor 绑定，保留用户资源和变体覆盖，不修改共享 ActorSpineCtrl、SDK／Packages／GF、配置或打点公式。

## 2026-10-14 播放态重烘焙后的几何同步修复

- 修前实测：实际SpriteRenderer高度8.8，调用ResetAll后运行材质仍保留7.2。InitMap播放分支更新资产模板，再由BindRuntime调用ResetAll，因此不能用模板参数正确证明已有运行实例正确。截图现场是否正是该分支仍为uncertain，未取得现场运行材质／Renderer快照，不能写成已唯一定位。
- 仅修改SceneSpriteBrushClear：ResetAll先同步当前Sprite、矩形、Sliced模式与Border；这些几何或图片发生变化才重建CPU有效Alpha，随后执行原遮罩／进度重置。无几何变化重复Reset继续使用原CPU位图和RT，不复制风格或创建新材质，不改碰撞、地图数据、Prefab、相机、存档／事件／SDK。播放中修改Size后仍须InitMap重新烘焙地图；单独Reset只同步显示几何并恢复原遮罩，不能代替地图重新烘焙。
- 已复测：实际高度8.8，运行材质高度8.8；真实PlayMode10项通过，覆盖三分辨率×全屏／0.76高度viewport共6场景、7780个稳定坐标点，尺寸变更、无变化Reset复用、Simple切换和更换Sprite／Border／Size。实际001图片、遮罩另有7项URP检查，三分辨率11426个稳定坐标点无偏移，保存圆洞宽高比约1。
- 分辨率规则：当前描边仍按渲染像素计宽，分辨率／Game窗口显示比例会改变它相对沙土的厚度；本次没有静默迁移用户调过的宽度数值或改成世界单位。上述验证只覆盖记录中的资源快照和隔离相机，不证明现场所有加载、相机堆叠、UI、完整胜负／重开、图集及双端环境。现场需重新InitMap或重进关卡复测；仍异常时需当前运行实例的几何与材质参数。
- 证据：`DevelopMD/验证记录/Dig沙土描边_2026-10-14/分辨率与运行材质验证记录.md`；回滚只删除ResetAll中的RefreshGeometryBeforeReset调用及该私有方法，保留前轮Slice／内外描边与用户资源修改。本次未发起Git暂存／提交。

## 2026-10-14 分辨率与播放态材质几何：前置核对

- 用户反馈Game画面拉伸偏移并询问分辨率影响，不能以原小九宫格检查推翻现场。当前001实际Sprite为Sp_wd_tk_02（900×87、非对称Border），Sand为Sliced／Size17.7×7.2／Scale1；使用实际PNG／meta、MapMask和材质，在URP分别640×360、1280×720、3088×1440核对原生图片与遮罩，7项通过，合计11426个稳定像素坐标样本无偏移。保存遮罩两圆洞框约2.005×1.997和2.005×2.011世界单位；这不证明当前运行实例与保存资源相同。
- 真实PlayMode又覆盖上述3分辨率与全屏／0.76高度viewport，共6场景、7780个稳定坐标点无偏移。确认分辨率会改变当前像素描边的世界厚度，但此证据不支持“分辨率本身把保存圆洞拉成椭圆”。共享CameraResolutionAutoFit只在Start读取尺寸，是其它入口风险；001自己的相机未绑定该组件，本轮不改共享相机、Loading或其它玩法。
- 当前确定的调用缺口：Editor播放中InitMap更新预览几何后调用BindRuntime→visual.ResetAll；ResetAll只恢复遮罩，不刷新Sprite矩形／Border／Sliced标志，运行材质在OnEnable时已经复制，之后Size或Sprite改变会保留旧几何。现场是否触发这一支仍缺当前实例快照，标uncertain；必须先在真实PlayMode复现Reset后参数陈旧，不能先宣称唯一根因。
- 拟仅修SceneSpriteBrushClear.ResetAll在原恢复流程前同步当前Sprite几何，几何／图片改变才重新建立CPU有效Alpha；保持风格、Shader、RT所有权、输入／事件／地图／存档与无变化Reset路径，不自动每帧修改Size或重烘焙，也不改用户当前资源。验证先记录修前实际Renderer高度与材质高度冲突，再以同场景证明修后一致，复查Simple与Sliced。回滚只撤新增Reset几何刷新入口，保留既有描边和Slice代码。

## 2026-10-14 Sand Sliced：使用与验证结果

已支持 `SpriteRenderer.Draw Mode = Sliced`。Sprite导入使用Full Rect，在Sprite Editor设置Border；Sand优先保持Scale为1，以Renderer的Size调整长宽。已有尺寸如由Scale形成，转换时须保持原世界尺寸。修改Sprite、Border或Size后执行InitMap并保存Prefab，烘焙会同时写入预览材质的切片参数，运行时从当前Renderer复制几何参数。

本地切片映射把图片Border与实际Sand矩形分开：图片保留四角／四边，中间拉伸；挖洞遮罩使用线性实际矩形UV，不重复九宫格的分段UV。内外描边与Noise保持；外扩连续扩展整个网格并按原矩形采样。输入UV、CPU有效Alpha和笔刷宽高比增加仅Sliced分支；Terrain的原Renderer.localBounds地图／碰撞范围和装饰遮罩映射沿用。新增 `SceneSpriteBrushClear.ApplySpriteGeometryToMaterial` 是Editor InitMap和运行材质初始化共用的本地只读几何入口，不初始化DB／服务或生成玩法对象。

修改文件：DigSand.shader、SceneSpriteBrushClear.cs、DigTerrainCtrl.Editor.cs及相关记录。没有切换／保存生产Prefab的Draw Mode，不改用户图片Border、材质风格、Size、Scale或地图；不写Packages/Guru/GF。描边宽度仍为屏幕像素，此项支持不改变之前Scene／Game窗口缩放导致的显示差异。

真实Unity6000.0.59f2／URP17隔离17项Sliced检查通过：有色九宫格3×2与原生Sliced逐像素对齐（允许最多19个分界像素差异）；小于Border总和的Size、非等比Scale、非中心pivot；跨分界单洞、内外描边／矩形外扩、URP渲染；实际屏幕输入、GPU盖章、CPU进度／透明边框采样；InitMap参数及遮罩资产。随后同一启动复跑26项Simple内外描边检查通过。

证据 `DevelopMD/验证记录/Dig沙土描边_2026-10-14/Sliced验证记录.md`。失败记录来自夹具：第一次把外框采样点放在已挖开的洞边；第二次旧TEMP关卡仍绑定原MaskRTSprite，更新为正式Dig/Sand绑定后通过。未据此修改生产参数。正式人工Prefab操作、完整PlayMode胜负／重开、双端Shader变体／GPU耗时、SpriteAtlas旋转／紧网格、Tiled、Flip／负Scale／父级剪切和多Sand批处理为uncertain。运行中直接改变Size不会每帧刷新或重建碰撞，须重新初始化／显式烘焙；静态地图尺寸制作是当前工作流。

回滚仅撤三个文件的Sliced属性、映射／输入／进度分支与共享几何入口，让InitMap恢复此前Sprite矩形传递。若制作人已经转换关卡，需恢复该关原Draw Mode／Size／Scale后重新InitMap保存，保留用户其它资源。未执行Git暂存／提交。

## 2026-10-14 Sand Sliced支持：修改前核对

- 用户询问能否支持Slice，按新增Sliced兼容实现：Sprite Border保留四角／四边，中间随Renderer Size延展；预留洞、玩家挖掘、内外Noise描边和编辑预览都按实际Sand矩形对齐。不替用户切换已有关卡的Draw Mode／尺寸／图片，也不重烘焙生产地图；描边仍沿用当前屏幕像素单位，本次不静默替换成世界单位。
- 当前Shader以纹理UV推导遮罩坐标并对顶点外扩，Sliced分段UV会使遮罩重复／错位，内部分界顶点也会被错误推移；共享擦除的SpriteLocalRect、输入UV与CPU Alpha按原始Sprite尺寸读取。Terrain的世界→UV和洞烘焙已使用Renderer.localBounds，装饰也是实际Sand矩形，但其读取的遮罩必须修正。源为DigSand.shader、SceneSpriteBrushClear、DigTerrainCtrl.Editor、Terrain.TryDigSegment／RasterHole与RandomSandSpriteGenerator，均手写，没有生成器。历史Sliced仅列uncertain，不是已支持功能。
- 改动：仅Sliced分支把遮罩坐标改为实际矩形线性归一；图片采样独立按Border和PPU映射，Renderer小于边框总和时同比缩小边框。外扩按整个切片矩形连续扩展，片元重新映射原图片，避免内部接缝；共享擦除的矩形／边框参数、输入UV、进度Alpha与宽高比仅Sliced分支处理，Simple保留当前分支。InitMap绑定同一套本地切片参数，保留材质风格。
- 验证：一轮真实Unity SpriteRenderer Sliced／URP，覆盖有色九宫格边框、宽高Size、非等比Scale、洞跨分界保持单洞、内外同时描边、Size小于边框总和、非中心pivot、实际输入UV、InitMap后预览／播放接入与CPU/GPU初态、Simple关闭恢复。Tiled、旋转打包Atlas、负Scale／剪切、真机与GPU性能不扩为本轮已验证。回滚定向撤Sliced参数与分支，不回退用户材质／地图／Prefab修改。

### 2026-10-14 内外描边同时保留：修改前核对

- 用户明确要求内描边与外扩同时具备。现Shader已能按Placement分配同一总宽度，但内外宽度相互牵制，之前交付强调二选一的说明不符合当前要求。
- 本次仅将Shader的总宽度／Placement替换为独立Inner Outline Width与Outer Outline Width，均允许0关闭；默认内侧3px／外侧5px，共用已有835233颜色、柔化和Noise参数。顶点只按外扩宽度扩展，内外片元分支分别判断；材质仅替换相关宽度字段，不改模板／Prefab／地图／碰撞或用户其它调整。
- 验证复用已有真实相机与SpriteRenderer夹具，一次启动覆盖同时显示、各侧独立关闭／调宽、两侧关闭恢复原Shader、URP、模板和InitMap参数保留。原单外侧20项保留为历史证据，新增结果另存；真机与GPU耗时仍uncertain。回滚仅恢复总宽度／Placement及对应材质字段。
- 实现结果：Shader和001～006已有材质已替换为独立内宽度3／外宽度5，同时开启。真实Unity／GPU26项检查通过，含图片与洞边两侧同时显示、调宽一侧另一侧逐像素保持、独立关闭、URP外扩、InitMap／运行材质双宽度保留。原20项为历史单外侧验证，最终证据为本目录验证记录的内外独立补充；无需重新InitMap，直接修改材质即可。

## 2026-10-14 Sand笔触描边：使用与结果

选中Sand的 `SpriteRenderer` 材质，或直接打开 `Assets/Game/Prefabs/DigLevel/Maps/DigLevel_XXX_MapPreview.mat`。默认Shader为 `Dig/Sand`、描边色 `#835233`，内描边3屏幕像素／外扩5屏幕像素同时开启，互相独立。内侧改变边缘沙土颜色，外侧仅在原透明区域追加颜色，原轮廓中间不移动。修改材质后直接看预览，不需要重烘焙地图。播放时修改运行时材质只影响该实例；要保留到下次进入须修改资源模板。

| 材质参数 | 用途／默认值 |
| --- | --- |
| Outline Color | 描边颜色与透明度，默认 `#835233`／Alpha1 |
| Inner Outline Width (Screen Pixels) | 内描边宽度，默认3；设0只关闭内侧 |
| Outer Outline Width (Screen Pixels) | 外扩宽度，默认5；设0只关闭外侧，两侧均0关闭全部 |
| Outline Softness | 边缘Alpha过渡，默认0.5 |
| Noise Texture (R) | 默认已有 `MMBrushNoise`，可换灰度Noise；使用红通道 |
| Noise Scale (World Space) | 笔触密度，默认4；纹理Tiling／Offset也可调 |
| Noise Width Variation | 描边宽窄扰动，默认0.45；设0关闭宽度扰动 |
| Noise Opacity Variation | 笔触浓淡扰动，默认0.2；设0关闭浓淡扰动 |

文件／数据流：新增 `Assets/Game/ArtSrc/Shader/Sprites/DigSand.shader` 与meta；001～006已有MapPreview材质切换Shader并设置Noise／默认参数；001～007显式绑定显示材质模板与Shader。007当前仍引用006材质，保留原共享资产关系，之后执行007的InitMap会生成并绑定自身MapPreview。`SceneSpriteBrushClear`只增加可选材质模板复制与Sprite本地尺寸传递；模板为空的原关卡仍走原Shader创建入口。`DigTerrainCtrl.Editor`烘焙继续保存本关预览／遮罩，额外绑定该显示模板并维护Sprite矩形，保留已经调整的风格参数。

外扩需要Shader扩展显示顶点并外推UV，没有移动Transform、Collider或Sprite资源；片元在原可见区外追加颜色，保留原GPU遮罩和CPU碰撞／墨水算法。原共享 `MaskRTSprite.shader`、装饰Shader、地图PNG及碰撞数据没有本轮修改；006／007的用户地图、布局及任务前修改保留。没有新增RT、CPU读回或每帧扫描；GPU新增Noise和八方向Sprite／遮罩采样，内外同时开启需要两组轮廓采样。

验证：Unity6000.0.59f2／桌面D3D、实际生产Shader与相关脚本在TEMP运行20项针对性检查通过，含关闭逐像素恢复原Shader、实心颜色保留、洞中心透明、洞边／图片外侧、Noise与内侧切换、真实SpriteRenderer网格外显示、非等比尺寸下横纵约5px、URP17 Renderer2D实际渲染、独立运行模板复制、InitMap风格／引用保留、遮罩绑定及材质释放重建。首次外扩Blit夹具没有真实屏幕投影而失败，改用真实相机／SpriteRenderer后通过，未据此修改生产算法。原14项内侧检查属于用户调整前版本，最终以20项外侧检查为准。

证据在 `DevelopMD/验证记录/Dig沙土描边_2026-10-14/`。编译证据仅覆盖隔离Unity相关脚本／Shader；正式全工程、Android／iOS Shader变体、IL2CPP、真实美术图集旋转／紧网格、主观笔触验收、完整重开与双端GPU耗时为 `uncertain`。相机若把原Renderer bounds完全剔除，仅描边接近屏边时可能被一起剔除；很小的洞可能被外描边视觉覆盖，可减小宽度，实际洞／碰撞数据仍在。

回滚：先将001～007的显示Shader和001～006材质Shader恢复原 `Sprites/MaskRTSprite`，撤新增模板字段绑定及描边参数；再定向撤共享擦除可选模板／Sprite矩形与InitMap两处接入，删除新Shader／meta。不要整文件回退006／007用户修改，不需要恢复或重新生成地图。本轮未提交Git。

## 2026-10-14 Sand笔触描边：修改前核对

- 需求：Sand图片外轮廓及挖洞后可见轮廓增加内侧描边，默认色 `#835233`，开放颜色、屏幕像素宽度、柔化、Noise纹理／密度／宽度扰动／透明度扰动。保留原Sprite与遮罩Alpha，描边不填回洞、不改变碰撞、耗墨或地图数据。
- 来源：`MaskRTSprite.shader` 当前只乘Sprite Alpha与 `(1-mask)`；`SceneSpriteBrushClear.EnsureMaterials` 从Shader新建材质，不能保留预览材质参数；`DigTerrainCtrl.Editor.InitMapFromPrefabAsset` 维护各关MapPreview材质与SpriteRectUV，最后接入运行RT。所有相关文件均手写，无生成器。已有Dig模块／装饰使用记录已核对，未找到Sand描边专项文档；CodeGraph索引存在但CLI／MCP不可调用，037计划缺失，知识库Shader检索无匹配，以当前源码／资源补足。
- 改法与边界：新增独立 `Dig/Sand` Shader，不改原共享MaskRTSprite；共享擦除组件增加可选显式显示材质模板（空时保持原新建Shader路径），Dig绑定本关已有MapPreview模板并复制为运行时材质。InitMap保存该模板引用，重复烘焙保留风格参数。既有关卡仅定向修改Shader／材质引用与样式资源，不重烘焙，不动布局或用户已有修改；复用项目MMBrushNoise资产，不改第三方内容。
- 验证：真实Unity导入／Shader编译，GPU检查空洞透明、实心中心、外轮廓／洞边、描边关闭恢复、Noise参数变化与运行模板复制；核对既有RT交换／Reset／禁用重启和InitMap样式保留。Android／iOS真机GPU耗时和主观美术尚uncertain。若回滚，先恢复Dig材质／Shader绑定，再撤可选模板与新Shader，保留地图、Prefab其它并行修改。

### 用户调整为外扩描边：实施前补查

- 用户指出内侧描边显得内凹，明确要求外扩。本轮将默认改为外侧，提供0内侧／1外侧的Placement参数。外扩仅覆盖原轮廓附近的透明像素（包括洞内），不移动真实沙土／碰撞边界，不修改GPU挖洞遮罩数据。
- Sprite原矩形网格会裁掉图片外缘描边，因此Shader顶点仅扩展显示网格，并外推UV；共享擦除仅给支持新Shader属性的材质传入已绑定Sprite本地矩形，InitMap同时维护同一矩形。扩展量由屏幕像素宽度换算，不改变Transform、Sprite资产或Collider。
- 验证范围更新：宽度0须逐像素恢复基线；外侧描边须保留原实心颜色并覆盖紧邻轮廓的透明区域，洞中心／远处仍透明；实际SpriteRenderer矩形外能显示，非等比缩放下横纵宽度按屏幕像素一致。旧“全部Alpha相同”的断言仅属于先前内侧版本，不能用作外侧验收。

## 2026-10-13 InitMap自动压缩地图

Prefab Mode点击InitMap即自动生成压缩碰撞初态与独立PNG遮罩，随后照常保存Prefab；编辑态隐藏Tilemap为空，沙土PNG预览保留，运行时在初始化／揭幕前恢复碰撞。旧关未重烘焙时仍走原格式，播放中烘焙仍仅写地图资产及当前实例，不自动保存Prefab。

保存后自动清理无资产引用的旧MapMask.asset，共享遮罩保留至使用者都转为自己的PNG。生成文件不随Undo回滚，撤销配置／烘焙后须重新InitMap并保存。150项隔离Editor／PlayMode检查及Editor／Android宏编译通过，真机性能和完整生产入口尚uncertain；依据、体积、风险及成对回滚见同目录DigLevel存储体积分析与优化方案.md，证据DevelopMD/验证记录/Dig自动压缩_2026-10-13/。

## 2026-10-13 Terrain按钮允许Editor播放态：当前结果

“收集子节点挖洞对象”和InitMap均保留在UNITY_EDITOR partial，去掉DisableInPlayMode和方法内播放拦截。Editor非播放Prefab／实例保持原烘焙；播放中可对关卡实例点击或调用InitMap，无Prefab连接时从已绑定InitialMask资产定位。绑定、洞源、Shader与DigLevel资产归属校验仍执行。非播放才MarkSceneDirty，播放中通过现有BindRuntime刷新RT、CPU余沙及碰撞范围，并保留运行时显示材质；不调用关卡／Actor／Germ初始化。

真实Unity CLI隔离3项Editor＋11项Play检查通过：按钮特性、原PrefabStage烘焙、播放实例和无Prefab连接实例、inactive／派生／嵌套收集、重复Grid比例、RT材质、继续挖掘／空洞免费及重烘焙CPU刷新。Editor／Android全源码编译0错误，证据DevelopMD/验证记录/DigTerrain播放按钮_2026-10-13/验证记录.md。正式Odin人工点击、当前生产地图、整关及真机仍uncertain；先前夹具断言错误与首次MarkSceneDirty异常均保留，不虚报为一次通过。

InitMap继续保存对应地图资产，退出Play不会撤销资产写入；运行实例变更不会自动保存到Prefab。回滚定向恢复两个按钮门禁、原PrefabStage路径及预览分支，勿覆盖并行地图。Germ掉出地图另列已知未修复，本次不把Editor按钮改变当成该问题修复。

## 2026-10-13 Terrain按钮允许Editor播放态：修改前核对

- 用户明确要求API只在Editor可用，不限制Editor的Play Mode。原CollectHoleSources及InitMap均有DisableInPlayMode，方法内分别return／throw；InitMap还只接受PrefabStage，移除播放门禁后仍会拒绝运行实例。整个Editor partial已有UNITY_EDITOR保护，Android／iOS无需加入按钮或资产写入逻辑。
- 来源与边界：DigTerrainCtrl.Editor.cs两个按钮、InitMap路径／Tilemap／mask／material写入、DigTerrainCtrl.BindRuntime／BuildSandPixels，SceneSpriteBrushClear.ResetAll／EnsureMaterials已核对。地图权威来源为当前PrefabStage或实例已有InitialMask资产，不从关卡名字、DB或层级扫描推断；本次不调用Level／Actor／Germ初始化或改地图加载时机。
- 拟改：移除两个按钮的DisableInPlayMode及播放态return／throw；InitMap保留有效绑定／洞源／Shader／资产归属校验，接受DigLevel Prefab实例；运行克隆无Prefab连接时由已绑定的_MapMask.asset确定输出路径。播放态不把预览材质替换到活跃Renderer，重烘焙后用现有BindRuntime同步RT／CPU余沙位图及_runtimeCells；非播放预览保持原材质。关卡场景标dirty，保存地图资产行为保持。
- 验证：Editor Play Mode克隆对象收集包含inactive／派生／嵌套排除，InitMap无PrefabStage时成功重烘焙；恢复被挖区域／出生洞、继续Stamp及空洞不耗墨、Renderer仍使用运行RT、重复烘焙Grid比例；原PrefabStage烘焙入口和Editor／Android编译。正式Odin人工点击、最新生产地图与真机为uncertain。用户未要求该按钮修复Germ掉出地图，该反馈另行记录为未修复。
- 风险／回滚：播放态按钮会修改本地地图资产，退出Play不会撤销这些资产写入；场景实例的序列化更改按Unity Play Mode规则处理，本次不自动保存Prefab或复制关卡对象。仅定向回退Editor partial新增分支／按钮门禁；事前快照TEMP/brainpunk-terrain-play-buttons-20261013/before.Editor.cs，保持其它任务及Package只读。
- 实现中补查：首次真实Play调用Collect时，EditorSceneManager.MarkSceneDirty明确抛出“This cannot be used during play mode”；编译不能证明该API播放可用。两按钮仅在非播放态标记场景dirty，Play Mode仍执行收集／烘焙与资产保存；失败日志留存，修正后重新验证完整播放入口。
- 验证返工边界：后续失败来自测试夹具把“全局不存在PrefabStage／Prefab连接”当成断言，未先读取实际状态；不是生产按钮异常。已撤回该夹具假设，记录实际归属路径，验证具体按钮操作／缓存同步。后续同类小改先快速编译，必要的Unity检查合并到一次启动；不因夹具状态假设无依据地修改生产功能。

## 2026-10-13 Germ实体刚体碰撞：当前结果及未修复反馈

DigGermCtrl的Contact Collider绑定任意Collider2D，Physics Body显式绑定同节点Rigidbody2D；Collider可以在Body子节点，但必须附着于该根刚体。Initialize强制isTrigger=false并保持初始暂停，关卡解除暂停后开始物理模拟。Actor通过Collision Enter／Stay上报真实接触，仍按原大小批次结算；认领关闭Collider和模拟，0.3秒吸入由Tween驱动。暂停／恢复保留速度，胜败锁与退出停止所有病菌物理。旧触发器及暂停用途注释与新需求冲突，已定向更新，其他注释保持。

当前六个Germ源Prefab绑定现有Dynamic刚体，001～004关闭Circle Trigger，005／006保留Body子Polygon轮廓。旧DigGerm→DigGerm_001使用同GUID，提交需同时保存删除旧路径／新增新路径，避免重复GUID；美术Sprite均为已跟踪资源。Base出生洞参数仍独立配置，未重烘焙任何生产地图。

6项真实Prefab导入绑定＋33项真实Unity Physics2D／Tween检查通过，包含地面支撑、Circle／Box／Body子Polygon碰撞吞噬、较大病菌失败、重复／暂停恢复／取消／退出；三个脚本与通过副本SHA256一致，Editor／Android源码编译0错误。证据见DevelopMD/验证记录/DigGerm实体碰撞_2026-10-13/验证记录.md。隔离环境有既有URP Probe资源加载报错，不将这些检查解释为完整渲染或生产关卡验收。

**已知未修复：用户在正式关卡中确认Germ掉出地图，截图中的调试列表仍有三只病菌。该用户实测优先于隔离地面支撑测试；掉落原因、发生位置／时序及生产Sand碰撞链尚未查证，不能宣称Germ整体已正常。本次按用户后续“提交git”保存当前改动，不包含该问题修复。** 整关路线、实际HUD／广告／存档及Android／iOS真机性能仍uncertain。回滚仅撤实体碰撞与刚体生命周期增量及Prefab对应字段，保留Collider泛化和并行地图／资源修改。

## 2026-10-13 Germ实体刚体碰撞：修改前核对

- 用户明确要求Germ Collider不得是Trigger、需要刚体碰撞。本次把病菌由触发区改为实体物理对象；保留原数值大小／成长／批次失败优先、0.3秒吸入、圆形半径映射和非圆轮廓参数。验收覆盖真实碰撞吞噬／大者失败、子Collider身份、暂停／后台／胜败／退出和新实例；下方上一轮Trigger及同节点用法为历史基线，本节替代该要求。
- 来源：DigActorCtrl目前只有OnTriggerEnter2D／Stay；DigGermCtrl.Initialize只缩放，TryClaim只关Collider，SetPaused只停Tween；DigLevelCtrl.ApplyPause／FixedUpdate／BeginFailure／OnDisable负责批次与生命周期。当前Germ_001～006全部已有根节点Dynamic Rigidbody2D；001～004的Circle仍isTrigger=1，005／006的Polygon在Body子节点且已绑定。脚本与Prefab手工维护，无生成器／模板。已核对Editor约束、宪法、模块及001风险文档；知识库DigGerm无结果，CodeGraph索引存在但CLI／MCP不可调用，037 plan缺失，使用当前源码及YAML补足依据。
- 拟改：Germ显式序列化physicsBody引用并校验根刚体与绑定Collider的attachedRigidbody一致；Initialize在运行态关闭Trigger并冻结至关卡解除暂停。Actor改用Collision Enter／Stay，经实际Collider的attachedRigidbody识别Germ；不增加名字或层级扫描。Germ暂停保存速度、恢复速度，认领关闭Collider及刚体模拟，吸入由Tween驱动；认领后恢复不得重新开启物理。关卡胜败锁均暂停Germ，退出终止吞噬同时停止物理。
- 资源边界：仅在现有六个Germ源Prefab绑定已经存在的刚体、关闭四个Circle的Trigger；保留Body子Polygon、几何、重力／质量／旋转约束、美术、GUID和所有关卡／地图／Base洞源。用户已添加Dynamic刚体，因此不另行改变BodyType；出生洞仍须独立HoleShape／Radius与InitMap。没有DB／存档／配置／打点／SDK写入或新全局事件。
- 风险与验证：实体接触产生推挤／落沙，属于本次有意行为变化；新刚体若不冻结会在暂停／失败期间继续移动，或干扰吸入Tween。用Unity6000真实隔离物理验证Circle／Box／子Polygon、失败／重复碰撞、初始暂停／重复暂停速度／恢复、吞噬冻结及取消；Editor／Android编译与六Prefab绑定静态核对。正式地图路线、生产HUD／广告／真机性能仍uncertain；新增动态碰撞预算需真实Android／iOS横屏关卡评估，回调不新增集合或扫描分配。
- 回滚：仅撤本节对应三个脚本增量、PrefabphysicsBody引用与Trigger值；保留上一轮Collider泛化／字段迁移和所有并行资源编辑。源文件及六Prefab事前快照在TEMP/brainpunk-germ-solid-20261013；不能整文件覆盖并行修改。

## 2026-10-13 Germ通用Collider：历史阶段用法（已由实体碰撞替代）

DigGermCtrl的碰撞字段改为Contact Collider（Collider2D），支持Circle／Box／Capsule／Polygon等；FormerlySerializedAs("circle")保留旧Prefabs的组件引用。初始化保留旧Body缩放，Circle仍设置unitRadius×ScaleFor半径；非圆Collider的尺寸／Offset／Points由Prefab配置，脚本不推导统一半径或自动改形状。TryClaim只禁用绑定的Collider，0.3秒吸入／暂停／取消和数值成长保持。

新Collider需与DigGermCtrl同节点并在Inspector显式绑定、勾选IsTrigger。当前Germ_005多边形在Body子节点且旧引用为空，仍需制作人将轮廓配置到根节点Collider并绑定；本次没有修改该Prefab或扩展Actor子Collider识别。Base.HoleShape是独立的Editor地图轮廓引用，需要自行绑定／InitMap，不因碰撞字段泛化自动改变出生洞。非圆尺寸与Body视觉缩放需制作人按实际表现核对。

真实Unity隔离19项PlayMode通过，覆盖圆半径／Body旧映射、Box／Capsule／Polygon参数、实际Actor触发／认领／成长、关闭Collider、重复、吸入完成／取消；旧circle字段导入断言通过后进入Play。Editor／Android条件编译Exit0，详见DevelopMD/验证记录/DigGerm通用碰撞_2026-10-13/验证记录.md。正式新Prefab绑定／整关地图路线／最新HUD及失败组件／双端真机仍uncertain；只改脚本，没有重写关卡／病菌资源。本轮未提交Git。

影响及回滚：仅DigGermCtrl序列化引用名称／类型与Initialize圆形分支／TryClaim引用，未新增扫描、事件或运行时分配。旧“单位圆身体”注释因非圆支持不再准确，定向更新用途；其它旧注释保持。回滚必须同时将已保存的新contactCollider字段迁回circle；不能只删除FormerlySerializedAs后丢失引用，优先保留迁移能力或按单组件字段回退，勿整文件覆盖并行Prefab编辑。

## 2026-10-13 Germ解除Circle类型限制：修改前核对

- 用户点名DigGermCtrl将CircleCollider2D限制去掉。原Initialize校验circle引用，按unitRadius／ScaleFor缩放Body并改circle.radius；TryClaim禁用circle，吸入／暂停／取消维持原0.3秒Tween和一次回调。DigLevelCtrl初始化、按数值判定／成长，Actor.ReportContact只从实际碰撞Collider同节点TryGetComponent识别病菌。
- 拟改仅手写DigGermCtrl：序列化contactCollider类型为Collider2D，以FormerlySerializedAs("circle")迁移旧引用；圆形仍按原半径映射，其它形状保留 authored尺寸／offset／paths，Body原缩放不改，锁定禁用实际绑定Collider。无生成器／模板／OnValidate／运行时层级扫描或全局事件，不修改Actor、地图／Base出生洞及新旧关卡Prefab。
- 来源／边界：DigGermCtrl.Initialize／TryClaim／PlayConsume／OnDisable、DigActorCtrl.ReportContact、DigLevelCtrl.InitializeRuntime／FixedUpdate、BaseDigComp／Editor InitMap和当前Germ_001～005资源；模块与001结构／风险、Editor约束及历史／日期记录已核对。CodeGraph不可调用，使用源码／YAML。旧“单位圆身体”和半径注释不再覆盖非圆碰撞，按实际范围定向调整，其他旧注释保留。
- 当前新Germ_005的Polygon在Body子节点，原circle引用为空；泛化类型不等于新增子Collider身份转发。新Collider需和DigGermCtrl同节点、手工绑定；本轮不擅自搬动美术层级或为引用增加扫描兜底。Polygon出生洞仍需单独配置Base.HoleShape并InitMap，不从运行时触碰引用自动绑定地图源。
- 验证：真实Unity旧circle字段导入保留／圆尺寸回归，Box／Capsule／Polygon保持参数且claim禁用、重复claim／吸入回调／取消；Editor／Android条件编译。正式新病菌Prefab绑定、子Collider支持、整关路线与真机仍uncertain。回滚定向撤字段泛化／迁移属性和Circle分支，保留其它并行资源／陷阱动效修改。

## 2026-10-13 陷阱knock动效

DigTrapFailureHandle新增可选Knock Effect实例引用；TryFail接受后播放，Collision取实际contact、Trigger取Collider最近点，取点在角色Collider关闭前完成，保留粒子Z。重复／无效／暂停后台／已到达／胜负锁不播，运行态禁用清父子粒子，原0.5秒失败／重开保持。DigGame_SharpTrap源Prefab已放置并绑定既有Knock_Explosion；其它自建陷阱Inspector显式绑定实例，无自动查找或运行时生成。

最新资源在实施期间有并行缩放及引用ID重存，已保留并对当前状态重新验证，未覆盖关卡／美术编辑。Unity隔离92项通过（62结果＋30粒子／资源），Editor／Android条件编译0错误，正式渲染／平台流程与双端真机仍uncertain。配置、事前核对、风险和回滚见同目录DigTrapFailureHandle击中动效功能与风险记录.md，证据DevelopMD/验证记录/Dig陷阱Knock_2026-10-13/验证记录.md。

## 2026-10-14 陷阱主线同款音效

`DigTrapFailureHandle` 在本关首次有效 Actor 接触被 `TryFail` 接受后，播放原 Knock 粒子并调用 `AudioManager.PlaySound("trap")`。音效标签、资源和全局 SFX 开关与主线 `OnTriggerTrapHandle` 一致；Trigger／Collision 的 Stay 重复回调、暂停／后台、已到达／外来 Actor 和胜负已锁均不播放。没有新增音频资源、Inspector 字段、全局事件或失败延迟。

Unity 隔离验证由原92项扩为96项，新增首次一次、Stay不重复、暂停拒绝不播和胜利锁后不播；Editor／Android条件编译0错误。计数桩证明调用条件与标签，正式 SoundsGood 混音和双端真机仍为 `uncertain`。完整边界见 `DigTrapFailureHandle击中动效功能与风险记录.md`，证据见 `DevelopMD/验证记录/Dig陷阱音效_2026-10-14/验证记录.md`。

## 2026-10-13 后续更新：最后5秒HUD

DigLevelCountdownCtrl已有超时失败，本轮补齐本关CountdownSeconds／CountdownChanged快照、DigPlayCtrl订阅／转发／退订、Dig HUD原cd_txt绑定与5→1数字动画；0／胜负锁定／失活／重建／面板关闭时隐藏。TBC固定0，未新增跨玩法事件，不改变计时、死亡、统计、广告或数据规则。下方前轮“未新增HUD”的描述为当时范围，本节及UI模块当前版本替代该限制。

Unity隔离90项通过（前轮62＋HUD28），Editor／Android条件编译0错误；真实HUD布局／原生广告／包加载／存档／双端真机仍uncertain。配置与风险／回滚在Assets/Game/UI/DigGame/DigGame_GamePlayPanel功能记录.md，证据DevelopMD/验证记录/Dig倒计时HUD_2026-10-13/验证记录.md。

## 2026-10-13 Terrain收集按钮：当前用法与验证

在关卡根DigTerrainCtrl点击“收集子节点挖洞对象”，再按需InitMap并保存Prefab。按钮包含当前地形层级的BaseDigComp本身及Actor／Germ等派生组件、inactive来源；排除最近所属Terrain为其它嵌套地形的组件。统一重填SpawnHoles、清空ReservedHoles以免同来源重复处理：SpawnHoles现有逻辑已支持Radius圆洞和HoleShape轮廓洞；按钮不会自动绑定Collider或改变IsShow／参数。原手工数组被覆盖，可Undo；不在运行时扫描，不自动挖地图／创建对象或调用数据链。

真实Unity6000.0.59f2 PrefabStage9项通过：类型范围、两数组归并、Undo、inactive、嵌套排除、Germ派生、重复／删除来源、原地图不变、保存重开；Editor／Android全源码条件编译Exit0。初次TEMP保存因用户并行新Countdown脚本未在隔离项目导入而失败，只在TEMP移除缺失组件后重跑，生产组件不改；验证不覆盖Countdown。证据DevelopMD/验证记录/DigTerrain收集_2026-10-13/验证记录.md，正式Odin人工点击尚未实测。回滚只删DigTerrainCtrl.Editor.cs新增引用收集区，保留地图、来源对象和其它功能。

## 2026-10-13 地形收集挖洞对象：修改前核对

- 用户明确要求DigTerrainCtrl加Odin按钮扫描子节点中的BaseDigComp及其派生类。当前Terrain在关卡根，Actor／Germ／木头和预留洞位于其子层级；现有SpawnHoles既支持Radius圆洞，也支持HoleShape的Collider轮廓，ReservedHoles仅走轮廓入口。两个数组手写序列化，无生成器／模板覆盖；InitMap只编辑器显式运行并按IsShow隐藏来源。
- 拟改仅DigTerrainCtrl.Editor.cs：显式“收集子节点挖洞对象”按钮，非播放态按类型收集本层级所有BaseDigComp（包括Base本身、派生类及inactive），排除最近所属Terrain为其它嵌套Terrain的来源；统一写SpawnHoles并清空ReservedHoles，保证每个来源只处理一次。用户本次授权编辑器层级扫描，无按名匹配／自动挂Collider／运行时兜底。
- Undo记录Terrain组件、dirty／Prefab覆盖／场景，点击后人工保存；收集不调用InitMap、不创建对象、不改变IsShow／Radius／HoleShape／地图／DB。只在编辑器点击扫描，Android／iOS无新运行开销；现有烘焙／挖掘／结果流程保留。
- 来源与验证：已读Editor态约束、BaseDigComp和InitMap两来源循环／ValidateHoleSources、Odin现有按钮；CodeGraph不可调用，以当前源码／Prefab核对。真实PrefabStage收集包含inactive／派生类型、重收集不重复、嵌套排除、Undo／保存重开与不烘焙；Editor／Android条件编译。主工程Odin人工操作仍uncertain。回滚只删新增Editor按钮区；误收集Undo，不回退其它任务。

## 2026-10-13 可选失败条件：墨水耗尽／超时／陷阱

新增DigInkExhaustionFailureCtrl、DigLevelCountdownCtrl、DigTrapFailureHandle，按关卡Prefab挂载，均显式绑定本关Level；陷阱另绑定同节点Trap Collider，支持Trigger与普通Collision，无需Index。墨水归零在运行Update上报；超时默认第一笔有效挖掘正常结束后20秒，可通过Cd Time配置，暂停／后台不扣时。未挂载旧关保持原零墨物理行为；本轮未写任何生产关卡Prefab。

三个条件复用DigLevelCtrl.TryFail／原BeginFailure、0.5秒死亡和唯一Resolved(Failed)，随后DigPlayCtrl原失败统计／黑幕重建／广告恢复链；拒绝外来或已到达角色，任何未到达角色失败整关失败，胜利锁定后不再接受失败。局部StrokeCompleted只在有效新沙笔画正常松手通知，取消／暂停／输入关闭不通知。结果职责参考Draw，墨水耗尽失败为Dig本次新增，未新增Draw全局事件、HUD倒计时、DB／配置／打点结构或只读SDK写入。

62项真实Unity隔离运行检查通过，Editor／Android条件编译0错误；iOS仍2处既有Guru.UniKit.Apple引用错误。真实HUD／广告／存档／包模式重开与双端真机为uncertain。配置、事前核对、来源、风险和回滚见同目录《DigGame失败条件功能与风险记录.md》；复查证据在DevelopMD/验证记录/Dig失败条件_2026-10-13/验证记录.md。本节为可选组件挂载后的规则，未挂载时下方耗尽继续物理的既有说明仍适用。

## 2026-10-13 第四关初始化失败：修改前核对

- 用户实测第四关进不去。当前Editor.log第1203508行明确记录DigPlayCtrl加载／重建失败，异常为“角色引用缺失或重复”；此前第1162695行同错。运行配置40004→DigLevel_004，资源已成功进入InitializeRuntime，因此本次阻断不依赖猜测解锁或缺Prefab。
- Prefab事实：DigLevel_004根6695274849859263318的actors是[4468337775836762992,0]；实际仅一个DigActorCtrl stripped引用4468337775836762992、一个Actor源Prefab实例。InitializeRuntime对非空actors逐项检查并因第二项null抛异常；DigPlayCtrl捕获后返回首页。第四关mapBaked=1、初始遮罩引用004专属资源，不能把后续初始化正确仅凭这一项推断。
- 拟改／边界：仅移除根Actors列表的空第二项；保持已有角色、Index、目标、地图、布局、相机、病菌／墨水和回调。不弱化运行时空引用校验、不修改共享控制器／远端排序／配置／SDK，不运行地图烘焙。用户本次明确指出004，当前资源未跟踪，非已验收关卡迁移。
- 验证：实际Unity隔离导入004后确认一个角色、对原空项复现异常，调用修正绑定初始化确认RuntimeReady；定向合并前后仅根控制器块改变，其它Prefab块和地图哈希保持。完整主工程重新进入／通关路线／双端真机仍uncertain。任务快照brainpunk-dig004-entry-20261013，回滚只恢复该列表空项，不能整文件覆盖其它用户编辑。

### 修正与证据

移除Actors空第二项，保留现有唯一角色。真实Unity隔离5项通过：有效列表／实际角色数、原错误复现、修正后InitializeRuntime含地图接入成功、物理开启且无结果锁。整个Prefab仅删除该空项，164个其它块逐字相同；地图及脚本不改。记录见Level4结构／风险文档及DevelopMD/验证记录/Dig004进入_2026-10-13/验证记录.md。主工程完整进入／通关、真机仍uncertain；重新进入004确认，后续增删角色可用“按通关条件收集”并保存Prefab。本次未自动提交Git。

## 2026-10-13 自动收集按钮：当前用法

在Prefab Mode选择关卡根DigLevelCtrl，设置Victory Condition，点击“按通关条件收集”，检查引用后保存Prefab。ReachTarget填Actors；ConsumeAllGerms填Actors和Germs。收集范围是本关子层级，包括inactive对象，排除最近所属DigLevelCtrl为其它嵌套关卡的组件，按层级遍历顺序填列表；历史actor同步首项／空。ReachTarget保留已有Germs，因为到达关卡也可以有病菌；不修改目标Index、Trigger、回调、相机、地形、地图或布局。若inactive角色不应参与关卡，请从收集后的Actors中手动移除，否则其到达仍是胜利条件。

### 2026-10-13 收集按钮允许 Editor Play Mode（修改前核对）

- 用户要求该 API 仍只在 `UNITY_EDITOR` 下存在，但取消编辑器播放态限制。当前按钮带 `DisableInPlayMode`，方法入口在 `Application.isPlaying` 时直接返回，因此 Play Mode 无法执行。
- 收集职责与范围不变：按当前 `victoryCondition` 重填本关 Actors，吞噬模式再重填 Germs；仍包含 inactive、排除嵌套关卡、同步旧 `actor` 字段，不初始化玩法、不改目标编号或地图。
- 拟修改仅移除 Odin 播放禁用和入口播放态返回。已知 `EditorSceneManager.MarkSceneDirty` 在 Play Mode 会抛异常，因此只在非播放态标记场景；播放态仍完成对象收集和序列化字段更新，结果属于当前运行实例，不承诺退出 Play Mode 后自动持久化到 Prefab。
- 验证采用 Editor/Android 条件编译和隔离 Unity Edit／Play 两种入口；正式主工程 Odin 点击与退出播放后的 Inspector 显示仍为 `uncertain`。

实施后按钮仍位于 `#if UNITY_EDITOR` 区域，已移除 `DisableInPlayMode` 与方法入口的播放态返回。非播放态继续执行 Undo、Prefab override 与场景 dirty；播放态执行相同组件收集和字段更新，但跳过 Unity 明确禁止的 `EditorSceneManager.MarkSceneDirty`。隔离 Unity Play Mode 在当前001实际层级收集到1个Actor、6个Germ且无异常；此前12项Edit Mode证据仍适用。Editor／Android条件编译均0 error，Android结果同时证明Editor API未进入玩家代码。正式主工程Odin点击和退出播放后的瞬态结果观察仍为 `uncertain`；Play Mode结果不会自动保存到Prefab。

按钮为UNITY_EDITOR内的显式操作，运行时禁用且入口拒绝Application.isPlaying，支持Undo与Prefab实例覆盖／场景dirty，不自动保存或初始化玩法。此按钮只帮助制作引用，运行时继续读取已保存的列表，无移动端扫描开销。

真实Unity6000.0.59f2隔离Editor12项通过：003角色收集、旧actor同步、无运行态初始化、Undo、inactive／嵌套边界、重复调用、保存重开、001吞噬角色／病菌、ReachTarget保留病菌、相机／地形不改。Editor／Android条件编译0错误。详见DevelopMD/验证记录/Dig引用收集_2026-10-13/验证记录.md；正式Odin Inspector人工点击及主工程Prefab实例覆盖手工验收仍uncertain。测试只操作TEMP资源，生产001／002／003未由本次按钮任务重写。

风险／回滚：按钮会按当前层级覆盖相应列表，参与范围和顺序需制作人检查；同病菌竞争仍沿多角色规则按Actors顺序。错误点击可Undo；取消功能仅移除DigLevelCtrl的编辑器引用收集区，保留此前多角色与已存引用。未新增生成器、数据／事件链、实例创建或其它运行时行为。

## 2026-10-13 按通关条件收集引用：修改前核对

- 需求／基线：用户明确要求DigLevelCtrl提供按钮自动抓取通关条件相关组件，ReachTarget抓Actors。现有actors与germs均是手写SerializeField，无生成器／自动绑定模板；InitializeRuntime读取显式列表，ReachTarget仍允许配置病菌，不可静默清空已有germs。原角色快照、结果／暂停／墨水／地图和旧actor兼容必须保留。
- 来源／维护边界：DigLevelCtrl.Prefab绑定／InitializeRuntime／QueueContact／TryReachTarget，DigVictoryCondition两模式，以及DigTerrainCtrl.Editor.InitMap的Odin按钮／Undo／场景保存模式；已读Editor态约束与模块当前说明。CodeGraph索引存在但CLI／MCP不可调用，037 plan缺失，知识库DigLevelCtrl无命中，以手写源码补足依据。
- 拟改：仅现有DigLevelCtrl的UNITY_EDITOR区增加“按通关条件收集”私有按钮。两种模式均收集本关子层级DigActorCtrl；ConsumeAllGerms再收集DigGermCtrl，ReachTarget保留germs。包含inactive，以最近DigLevelCtrl所属关系排除嵌套其它关卡；按层级顺序填数组并同步历史actor为首项／空。用户本次明确授权编辑器类型扫描，不引入运行时扫描或按名称匹配。
- 操作／影响：Undo记录当前组件、标记dirty及Prefab实例属性／场景，不自动保存Prefab、初始化玩法或挖地图；无目标Index／UnityEvent／相机／Terrain绑定改写，无新脚本／公共API。只在点击时编辑本组件引用，无移动端运行开销。
- 验证计划：Editor和Android条件编译；真实PrefabStage的003两角色收集／保存重开、001吞噬角色和病菌收集、ReachTarget病菌保留、inactive与嵌套关卡边界、重复收集／Undo；不直接回写已验收001／002或当前003Prefab。正式Inspector手工点击／真机本轮仍uncertain；回滚只删除新增按钮区和使用记录，不撤前轮多角色。

## 2026-10-13 多角色：当前用法与验证

- 关卡根DigLevelCtrl的“场上角色（Actors）”显式填写所有参与角色。003已绑定现有两个角色（Index0、1）；目标仍使用相同“胜利匹配 Index”，没有改布局或重烘焙地图。旧001／002未填列表时继续使用历史actor绑定；非空列表中的空项或重复引用会拒绝初始化。
- 每个角色独立初始化、记录接触快照和成长。到达模式中，匹配角色第一次到达即冻结本人并触发目标OnReached，剩余角色继续挖掘／物理；重复、外来角色和错误编号拒绝。全部参与角色到达才锁胜并启动原VictoryDelay。同Index角色可共用同Index目标，回调按角色首次到达执行。
- 暂停、后台、失活和结果锁处理全部角色；恢复不释放已到达角色。吞噬模式仍需全部病菌及动画完成；任何角色的过大病菌批次先全局失败，播放实际失败者的死亡。共享病菌在同一步按Actors顺序只认领一次，成长和吸入归实际认领者；金币跳关沿原整体完成流程。
- 部分到达时仍在玩法中，原南瓜根含静态碰撞体，不能随成功动画缩放。基础成功目标及002内联目标新增无碰撞Visual，原SpriteRenderer／Light移入Visual，动效显式绑定Visual；Renderer原文件ID保留，02／03变体Sprite覆盖有效。形状源、刚体、墙、底和Trigger仍在原物理层级。
- 真实Unity6000.0.59f2隔离PlayMode共23项通过，覆盖003双角色、部分到达回调／冻结、其它角色继续、重复／错编号／外来拒绝、暂停恢复、最终延迟一次成功、旧001吞噬／002到达、独立成长、共享病菌一次认领、坏批次优先失败和三个目标变体。Editor／Android条件编译均0错误；详见DevelopMD/验证记录/Dig多角色_2026-10-13/验证记录.md。
- 未验证：正式主工程完整UI／广告／存档链、003从出生点的实际挖掘路线、同Index多人真实物理接触、后台和中途销毁专项重跑、Android／iOS真机及性能。以上为uncertain，23项不代表整关真机已验收。运行时不新增对象扫描、全局事件、DB或地图初始化。
- 回滚仅撤多角色字段／状态和Actor接触参数、003角色列表／引用块，以及基础目标／002的Visual剥离；使用TEMP任务快照按块回退，保留并行地图／资源修改。此节更新下方旧单角色和“到达即锁整关”的历史描述。

## 2026-10-13 多角色：修改前核对

- 用户需求／确认：场上存在多个Actors，DigLevelCtrl必须逐个初始化；用户选择全部角色分别到达自己匹配Index的目标才胜利。原实现单一actor用于初始化、接触批次／成长、死亡、暂停／失活、调试显示和目标身份门禁，单改初始化循环会留下错误成长／提前胜利／部分角色继续物理的问题。
- 来源／边界：DigActorCtrl.ReportContact→DigLevelCtrl.QueueContact／FixedUpdate／OnConsumed；DigTargetTrigger.ReportArrival→TryReachTarget／BeginVictory；DigPlayCtrl只通过IDigLevelRuntime加载、暂停、接收一次Resolved。本轮保留控制器／统计／UI／存档、输入／墨水／地图／TBC、共享ActorCtrl和只读SDK／Packages／GF。已读Editor约束、001结构／风险、002表现记录、胜利证据及打点当前约定；CodeGraph不可调用、037 plan缺失、知识库无Dig命中，以源码／Prefab核对。
- 原资源：001／002／003根仍保存actor单引用，003已有两个Actor实例（Index0／1）和显式绑定本关的目标（0／1／2），第二个Actor未纳入控制器。旧单引用保留兼容，不批量迁移已验收001布局。新actors列表显式绑定全部参与角色；003填入已放置两角色，不按名字或运行时层级扫描兜底。
- 拟改：初始化时保存角色快照和每角色接触状态，拒绝空／重复元素，旧列表未配置时复用原序列化actor。暂停／结束处理全部角色；Actor上报this与Germ，按每角色首次接触大小先验证全部批次，任一角色碰到过大病菌先失败并冻结其它角色；共享病菌TryClaim只结算一次，按actors配置顺序决定同一步可吃时的归属，成长／吸入只给实际认领者。
- 到达：匹配且属于角色快照的角色首次到达即冻结本人、清本人未裁决接触、触发该目标表现，其余继续，重复／错Index／外来Actor拒绝；全部角色已到达才BeginVictory并开始原1秒延迟。吞噬模式仍要求全部病菌与动画结束，金币跳关仍整体完成；不新增全局事件或进度存档。
- 关联表现风险：南瓜原动效绑定的Transform包含Static刚体与左右墙／底／目标，多角色首次到达时关卡尚未锁，继续摇碰撞会影响剩余角色。仅将原SpriteRenderer与Light移到无碰撞的Visual子节点，动效显式改绑Visual；保留Renderer文件ID，使目标02／03变体的Sprite覆盖仍有效，形状源／Collider留原节点。范围为基础目标Prefab和002内联目标；变体、003嵌套引用继承，不改美术图／排序／位置／地图。
- 验证：真实Unity旧001单角色吞噬／批次优先失败、旧002单角色到达与动效；多角色初始化／暂停／后台／失活、部分到达保持输入和其它角色物理、不同Index／外来／重复、同目标同Index多人、最终延迟一次结果、退出取消、新实例；每角色成长／吸入、同病菌一次认领、任一坏批次全局优先失败／失败角色死亡；003真实保存绑定、变体Sprite与目标Collider世界姿态在动效时不变；Editor／Android条件编译。移动端真机、正式UI／广告／存档仍uncertain。
- 回滚：定向撤多角色控制器／Actor上报和目标注释、003列表与剥离Visual的目标Prefab块；不回滚用户地图、艺术资源、SDK或其它并行修改。事前快照在TEMP brainpunk-multi-actors-20261013，代码为手写、无生成器／模板接入。

## 2026-10-12 装饰默认材质路径修复（当前）

用户将默认材质移动至Assets/Game/ArtSrc/Sprites/Dig，旧路径加载空导致Generate报Shader校验错误；材质及Shader GUID仍一致。Editor生成入口改用材质稳定GUID定位，在开始Undo/改绑定/删旧布局之前完成校验，正确显式模板保留。14项真实隔离Unity移动/重命名/失败保持/Undo检查和Editor/Android宏编译通过；正式Editor当前Prefab仍待重新Generate实测。操作、证据与局部回滚见RandomSandSpriteGenerator使用与风险说明新增章节；不回写用户资源目录/Prefab/地图。

## 2026-10-12 随机沙土装饰（当前）

新增RandomSandSpriteGenerator及Editor partial，在Prefab Mode显式绑定Sand Sprite/Visual，配置本地区域、Sprite组、数量/世界缩放和排序，Odin Generate生成并保存布局。整张图限制在区域内，包含非中心pivot；生成时抵消父节点非等比缩放。默认材质随功能提供，未自动改变已有Prefab或替用户选择美术图组。

装饰读取同一Sand遮罩，并按世界位置映射Sand UV，预留洞和玩家圆章逐像素擦除；同张Sprite未挖部分保留。运行时每区域一份材质，只随RT交换更新纹理，没有每章扫描装饰/额外RT/GPU读回；原Terrain/墨水/碰撞不改。共享擦除只增加局部MaskTexture读取/更新事件7行。Editor预览只使用本地配置/烘焙图/MPB，实际运行对象用Application.IsPlaying(gameObject)区分，不创建编辑器玩法实例。

20项真实PrefabStage/URP Editor + 30项PlayMode装饰检查、70项原耗墨/正圆回归通过，Editor/Android宏编译0错误。真实美术布局/图集打包、双端硬件/性能、生产UI及100次重建内存仍uncertain。使用步骤、数据/资源流、Undo修复、逐项证据、风险与局部回滚见同目录RandomSandSpriteGenerator使用与风险说明.md。

## 2026-10-12 两种胜利条件与统一延迟（当前约定与修改前核对）

- 需求与验收：用户所述 DiaGame 按当前 DigGame 上下文处理。新增“到达指定位置”，Actor 和 Collider2D Trigger 各记录 index，相同且属于当前关卡的主角才满足条件；保留“全部病菌吞噬”模式。用户补充要求现有吞噬胜利也使用相同流程；两种条件均锁定唯一结果、关闭输入／冻结玩法，默认等待可配置的 1 秒后提交成功，再进入现有胜利 UI 链。
- 基线与来源：DigLevelCtrl.InitializeRuntime 当前强制病菌非空；QueueContact／FixedUpdate 按接触开始大小统一裁决，任何大病菌优先失败；OnConsumed → TrySucceed 要求剩余病菌和待完成动画同时为零。TrySucceed 与 TryCompleteByHint 各自重复即时锁结果／冻主角／Submit。DigActorCtrl 以 Rigidbody2D／CircleCollider2D 显式绑定主角；DigPlayCtrl.OnResolved 负责统计、插屏与胜利面板，不能由目标脚本绕开它直接打开 UI。001 根控制器和 Actor 源 Prefab 的现有 GUID／对象引用已核对；002 为任务前未跟踪资源，保持原样。
- 原边界与资料缺口：关卡只报告 Resolved，不写存档／云控／打点，不改 Packages/GuruSDK/GF。BaseDigComp、地图／笔刷和吞噬组件职责继续保留。已读取001结构／风险、模块说明、Editor约束、四玩法打点有效条目及历史／当日使用记录。没有已有 index 到达目标模块文档，知识库 DigGame 无命中；CodeGraph CLI／MCP 不可用，037 plan 缺失，改以当前源码／Prefab 为依据。相关脚本为手写，不涉及生成器或配置字段。
- 拟修改与理由：增加胜利条件枚举（吞噬=0保证旧关默认）及关卡级 victoryDelay=1；Actor 增加序列化 index；新增 DigTargetTrigger 显式绑定所属关卡和同节点 Collider2D，Enter／Stay 按物理 attachedRigidbody 获取 Actor 身份并交由关卡核对，不按名称／层级扫描。到达模式允许空病菌但有病菌仍按原吞噬／大小失败规则处理，吞噬完成不能提前结束到达模式。两种自然成功及金币跳关统一进入 BeginVictory，Update 推进游戏秒延迟；暂停／后台不计时，禁用／销毁取消，不引入协程／异步晚回调或全局事件。
- 结束与原生边界：条件达成立即锁结果、清接触队列并冻结物理／局部动画，避免等待期间再吞噬或死亡；失败已锁时到达拒绝。延迟到点后一次 Submit(Succeeded)，仍经成功统计 → 插屏 → 面板；触发广告时实际面板显示时间受广告回调影响，1秒是条件达成到发起成功结算的游戏时间，不修改广告策略／缓存／Loading预算。尚未提交成功前 Reset／退出仍按原生命周期处理，失活旧关取消等待。
- 验证范围：默认001／旧Prefab加载、两种类型、默认／0／自定义延迟；匹配／不匹配 index、不同关主角、子 Collider 的 attachedRigidbody、重复 Enter／Stay、禁用或非 Trigger、到达无病菌／有病菌不提前胜利；吞噬全清且全部动画完成才开始延迟、大者失败优先、暂停／后台／恢复、失活／重建、失败先锁、金币跳关、成功只提交一次及旧玩法链不改。执行真实 Unity 隔离物理／Tween 验证、Editor／Android条件编译、Prefab／源码静态核对；完整生产 HUD／插屏／DeepLink／实际存档和双端真机仍 uncertain。
- 回滚：仅撤本轮条件／index／目标脚本／统一延迟及001字段，恢复原即时胜利入口；不回退地图、笔刷、病菌大小／布局、星级存档、广告、打点或并行资源。根胜利场景和001风险记录收尾更新；无需修改其它已验收玩法／关卡。

### 使用与最终职责

1. 在关卡根 DigLevelCtrl 的 `Victory Condition` 选择 `ConsumeAllGerms`（大鱼吃小鱼）或 `ReachTarget`（到达指定位置），设置 `Victory Delay`；默认1秒，0为即时结算。
2. 到达模式在本关 Actor 的 `胜利匹配 Index` 填编号，例如1；场景目标节点添加 Collider2D 并勾选 `Is Trigger`，再添加 DigTargetTrigger。显式将本关 DigLevelCtrl 拖入 `Level`、同节点 Collider2D 拖入 `Trigger Collider`，目标的 `胜利匹配 Index` 也填1。两字段按用户指定统一使用Odin LabelText标签，序列化字段名仍为index，原绑定和判定保持。保持节点与Collider启用、2D层碰撞允许；Actor沿用既有动态Rigidbody2D，支持其子节点Collider。
   - `On Reached` 是关卡接受本次到达后的局部 UnityEvent，可显式绑定已有特效节点的 `GameObject.SetActive(true)`。暂停、编号／身份不匹配、已锁结果、禁用或非 Trigger 时不执行；重复 Enter／Stay 共用关卡结果锁，只执行一次。回调发生在条件达成时，默认早于1秒后的成功结算；延迟0时原结算同步执行，回调在 `TryReachTarget` 返回后执行。
   - 002追加本关 `DigPumpkinSuccessEffect.PlaySuccess`，保留先点亮再动效的顺序。默认0.36秒压扁、弹起、轻摆并归位，中文参数为动画时长／缩放幅度／摇摆角度，禁用或重播恢复原姿态。没有延长胜利等待或新增全局流程；结构、风险和实测见Level2记录。
3. 当前关卡只接受Inspector“场上角色（Actors）”列表中的角色，旧空列表兼容历史actor绑定；其它关卡／未参与Actor不触发。到达模式必须全部角色各自匹配目标才胜利，可以不放病菌；若仍配置病菌，吞噬与成长保留，但全清不判胜。吞噬模式保持病菌非空，全部认领且吸入动画结束后才开始等待。
4. 到达／吞噬／金币跳关共用 BeginVictory：结果锁定、输入结束、清尚未处理的接触、物理／局部Tween冻结 → Update按游戏秒计时 → 一次 Submit(Succeeded) → 原DigPlayCtrl统计／插屏／普通、奖励或DeepLink面板。暂停和后台停表，恢复只继续等待，不恢复已经结束的玩法；失活／销毁取消旧等待。
5. 失败已锁时到达拒绝；到达条件在当前物理步先达成时立即锁胜并清尚未裁决的病菌接触。吞噬模式原“同批大者优先失败”不变。等待期间Manual Reset／Replay／Next沿原结果门禁拒绝，不另加结束事件；控制器退出失活后没有旧延迟回调。

- 影响文件：DigLevelCtrl.cs、DigActorCtrl.cs、IDigLevelRuntime.cs、新DigVictoryCondition.cs／DigTargetTrigger.cs与meta，001根Prefab新增模式0／延迟1、Actor源Prefab新增index0；仅新增这些YAML字段，尺寸、绑定、布局、地图与其它组件保持。002未跟踪资源与装饰／笔刷任务内容未修改。没有生成／Luban／RemoteConfig／存档字段或广告策略修改。
- 注释调整依据：TrySucceed原“正常胜利唯一入口”与新增到达模式冲突，改为吞噬入口；IDigLevelRuntime原“胜利提交后锁定、吞噬表现完成即通知”与条件达成先锁／统一延迟冲突，按新契约更新。Actor与其它旧业务注释保留。
- 本节覆盖下方旧“吞噬或金币跳关即时Submit”的描述。结构、风险与验证详情见 Level1/DigLevel_001脚本结构记录.md、Level1/DigLevel_001风险评估.md 和 DevelopMD/验证记录/Dig胜利条件_2026-10-12/验证记录.md；完整生产UI／原生广告与双端真机仍待验收。
- 验证结果：61项真实Unity隔离物理／Trigger／Tween检查、缺新字段的实际PrefabImporter默认值检查通过；30个原文件／方法及新脚本副本SHA256核对通过。Editor／Android 1218源码条件编译0错误，iOS仍两处既有Apple引用错误。地形／输入UI为替身，生产完整胜利界面与原生平台仍uncertain；证据在上述验证目录。

## 2026-10-12 已挖空区域不耗墨（当前）

### 需求、原流程与本轮边界

用户要求“已经空的部分就不要计入挖掘消耗”。原 TryDigSegment 仅用 ContainsPoint 检查 Sand 矩形，每个内部路径采样都会扣世界长度，包括预留洞、出生洞及已挖过的区域。本节替代下方旧记录的重复空洞计费约定。编辑器烘焙地图、世界正圆、固定砖块、长度单位、容量/评星、现有 HUD/广告、吞噬/物理、暂停和完整重建保留。

修改前核对见当天记录“Dig 已挖空区域免费”。当前用户已调整首关：Mask=1024×512、Brush Radius=0.5、Stroke Step Ratio=0.1、碰撞细分8，因此本关有效首章最低费用是0.05世界长度。旧章节的512²/细分6/0.075是此前快照；本轮没有修改这些参数、Prefab或地图。新复制的002同样复用这两个运行时脚本，但没有完整试玩002。

### 实现与数据流

- SceneSpriteBrushClear 仅增加只读 MaskResolution；原输入、单参数擦除、软边、进度、Shader、GPU写入和生命周期方法保持原源码。
- DigTerrainCtrl.BindRuntime 在恢复 GPU 烘焙图后，从同一 InitialMask 恢复每像素一位的 CPU 余沙位图。尺寸取实际GPU配置，不取192进度网格；同尺寸RGBA32直接读取已有原始数据，无像素数组副本。初始图尺寸不同时双线性采样，并减源图半纹素，与 GPU 目标像素中心对齐。
- Stamp 仍按 Sand.localBounds、变换与世界半径换算横纵 UV 直径；按像素中心是否位于同一圆章覆盖区清除余沙位。发现新沙才调用现有 GPU 盖章/Tilemap 删除并返回true；完全空洞返回false，不 Blit、不删格。碰撞半径偏移不作为耗墨依据，保留格中心碰撞近似。
- DigLevelCtrl.TryDigSegment 只在 Stamp 返回true后增加 _usedInk；本段至少一个有效章时才 FlushCollider 和发送现有 InkChanged。完全免费路径不改余额/星级、不发送无效HUD刷新，广告入口沿已有UsedInk门禁保持。采样起点仍随完整输入位置推进，避免余额不足时把免费空洞段连成错误的收费长段。
- 计费仍是“擦到新沙的采样所对应的世界长度”，不是按新增面积折算。圆刷中心在空洞但边缘擦到新沙时仍按该样本扣费；只有整章覆盖区无新沙才免费。按下并擦到新沙保留至少 StrokeStep 的原最低费用；移动仍受可用余额裁剪。剩余零时停止新增洞口，已有物理继续。
- 位图仅属于当前关卡实例；整关Reset/Replay/失败重建后恢复烘焙图和零已用墨水，奖励资格仍由原控制器恢复。不新增存档、配置、全局事件、自动扫描或编辑器运行链。此改动无需重新InitMap。

### 验证、性能与剩余风险

真实隔离 Unity6000.0.59f2/URP17 PlayMode 共70项断言通过：最新正式首关图加载零耗墨；预留洞点按/移动免费；重复点按不交换GPU RT、不刷新HUD；相同采样路径重复免费；跨空洞入实心；中心空洞边缘新沙；短余额穿洞/末段裁剪/耗尽后不冻结物理；静止/矩形外/暂停/后台/输入锁/结果锁；奖励容量/星级；新实例恢复已挖沙与初始洞；512/1024横向尺寸、非等比/25度旋转、正碰撞偏移、64方向世界正圆。遮罩GPU读回仅用于TEMP测试，生产逻辑没有GPU读回。

检查了8组初态或擦除后 CPU/GPU二值像素，均0个不一致；512目标/1024初始图的双线性边缘332个中间灰度像素不属于二值比较范围。首次不同尺寸检查发现181个边缘误差，修正半纹素后复跑通过；夹具首次边缘位置被其它出生洞覆盖和测试编译依赖错误已修正，不计作生产故障或通过证据。测试仍不能代替完整生产事件/HUD/广告及真机验收。

Editor宏全游戏源码编译0错误/113警告，Android宏0错误/118警告；这些是源码编译，不是APK/IL2CPP/iOS构建。1024×512位图65536B（64KiB）；500次重复空章循环0B托管分配，桌面约17.87ms合计。旧Tilemap/GPU进度成本仍在有效章执行，双端横屏触控、R8/ARGB32设备差异、移动端CPU/GPU耗时、完整002试玩和直接首帧RT恢复仍uncertain；没有真机60fps保证。

可复查产物：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-empty-ink-20261012/` 的 `checks.txt`、`gpu.txt`、`performance.txt`、`unity.log`、`compile-editor.log`、`compile-android.log`、修改前3个脚本快照及status。隔离Prefab/生成地图和测试脚本只在TEMP，不回写正式资产。

### 局部回滚

只撤 DigTerrainCtrl 的余沙位图和 Stamp 布尔结果、DigLevelCtrl 按结果收费分支、SceneSpriteBrushClear 的只读 MaskResolution及本章节；保留本轮前的尺寸/地图/Collider/IsShow/Editor烘焙、用户1024×512/笔刷0.5/细分8及其它并行代码/文档。恢复旧公共方法注释时以对应源码快照为准；本轮仅因收费语义变化调整 TryDigSegment/Stamp 的旧注释，没有改无关注释。

## 2026-10-12 胜利星等待记录对齐 Draw（当前约定、事前核对与验证）

- 需求与基线：用户已要求 Dig 沿用 Draw 的星级规则，本轮追问为何等待动画未保持一致。此前实现仅保存最高星，普通／奖励／DeepLink 面板接收本轮内存增量；这不是获确认的产品差异。对前轮漏查标记为“事后补查”，本节同时是本次修正开始前的核对记录。验收对象是独立 Dig 的最高星、待播新增星、面板消费和保存失败链，不能仅以正常一次结算有动画判定一致。
- 来源与原流程：DrawPlayCtrl.TryRecordVictoryStars 在创建面板前保存最高星及 LevelWaitingAnimStar，正增量累加，保存失败恢复原条目；普通及 Reward 的 Configure → CacheAndConsumeVictoryStars、DeepLink 的 ConfigureDrawStars → ConsumeDrawWaitingStars 均在读取后删除当前关卡待播项并保存，随后展示。Draw 不等待动画完成才删除，也没有启动时自动补播承诺。Dig 的 ShowVictoryPanel → ConfigureStars／CreateForDig(int,int) → RecordVictory 是本轮需替换的内存传参链。
- 维护与生成边界：DigGame.cs 是手写序列化输入，Bear.SaveModule.Editor.PartialClassGenerator 根据 [SerializeField] 生成 DigGame_Partial 的初始化和公共属性；只同步新增字段对应输出，不覆盖原有成员顺序。BaseSaveDataSO.FromJson 使用 JsonConvert.PopulateObject，DBManager 先 Init 再填旧档；旧档缺字段仍需 EnsureValidCollections 补空。DB.SaveDigGame 使用独立 DigGame 存储键。Draw、Main、IWanner、选关永久星读取、Prefab、Packages/GuruSDK/GF 不改。
- 拟修改位置、理由与影响：Dig 存档增加以 DigLevelData.Id 为键的 LevelWaitingAnimStar；DigPlayCtrl 复制 Draw 的星级写入和失败回滚，在工厂前执行；RecordVictory 只保留通关／解锁进度职责，避免星级保存失败后被它再次写入。普通基类 Configure 与 DeepLink Dig 专用配置分别从 Dig 档读取和一次性消费，Reward 继承同一流程。删除不再有调用者的内存 ConfigureStars／整数工厂签名，不抽取共享存档工具、不更改 Draw 实现。现有动画、导航门禁、Replay/Next/Home、广告、打点和 TBC 规则继续沿原链。
- 验证场景：旧档缺字段／显式 null、新档初始化、JSON 往返；0→3、1→3、同星／降星、已有待播累加、非法待播钳制、跨关隔离；写入成功／失败回滚、无新增时补集合、面板不存在前待播保留；普通首胜／复胜／Reward／DeepLink 消费、重复配置／重开无重复新增、缺星组仍消费、消费保存失败沿用 Draw 日志与内存删除语义；TBC 不写、其它玩法方法不变。无网／服务不可用／晚广告回调不适用于本次纯存档增量，但原广告方法需保持。执行实际方法隔离测试、生成器预览、Editor/Android 编译及 Excel 对比记录更新；完整 Unity UI、PlayerPrefs 原生落盘、Android/iOS 真机仍 uncertain。
- 回滚：仅撤销本节对应代码及生成属性和记录增量，不整文件回退覆盖地图并行修改，不删除最高星、通关、解锁或其它玩法玩家数据。旧版本会忽略新增等待字段；已经消费的动画记录不会因回滚重新出现。
- 实现结果：DigGame 增加 levelWaitingAnimStar；Partial 只补对应初始化／属性，旧集合校验同步。TryRecordVictoryStars 在三个工厂之前保存正增量并执行 Draw 同样的失败回滚；RecordVictory 移除再次写星职责。普通／Reward 的 Configure、DeepLink 的 ConfigureDigStars 缓存永久星和待播星后删除当前关等待项并保存。原 ConfigureStars 与 CreateForDig(int,int) 已移除，替换为真实配置参数工厂。消费保存失败仍按 Draw 保留内存删除、磁盘原记录和警告，未擅自改成新事务策略。
- 注释核对：旧 Dig 星组／Configure／DeepLink 工厂的“控制器内存注入、创建后保存最高星”及 RecordVictory 的“保存最高星”与新职责冲突，定向更新；Reward 工厂的“保存进度后调用”也与实际顺序冲突，改为保存星级后、保存排序进度前。其它旧注释保留。
- 验证：89 项实际方法隔离断言通过，包含与 Draw 原写入和普通／DeepLink 消费方法的同场景比较、实际 Newtonsoft JSON 往返、实际 PartialClassGenerator 纯方法预览；33 个既有生命周期／三玩法方法及源文件保持。当前 1214 个源文件 Editor 0 error／113 warning，Android 0 error／118 warning；iOS 仍有两处既有 Guru.UniKit.Apple 缺引用。详细场景、命令、日志与范围见 DevelopMD/验证记录/Dig待播星对齐_2026-10-12/验证记录.md。
- 证据边界：隔离验证的 UI、Spine、存储后端和 PlayerLoop 为模拟；实际序列化方法／新增字段／存档写入消费方法使用当前源码。最高星与等待数的 JSON 可往返，不等于真实 PlayerPrefs 落盘／重启或完整胜利动画实测。Android/iOS 横屏、真实关闭重开／原生广告后 UI／掉电存储仍 uncertain；Draw 本身不保证动画途中退出后自动补播。此节覆盖下方旧“创建后保存最高星、只注入内存新增星”的描述，其它地图／玩法约定不变。

## 2026-10-12 Collider轮廓洞口与移动端分辨率（当前）

- 用户截图中的FixedBrick此前HoleShape为空，Radius0.5生成圆洞。本轮显式绑定同节点BoxCollider2D（5835200198103860221），保持Radius0.5、OutwardDistance0.1、IsShow=true和砖块/沙土变换。InitMap复用现有世界距离外扩：0保持原矩形轮廓，0.1保留直边并在凸角形成半径约0.1的外扩圆弧；不是将矩形替换成圆形，也不修改砖块Collider自身。当前不提供外扩与圆角半径独立参数。
- 只修改本关Base引用并重新烘焙Tilemap/MapMask，没有新增C#逻辑、自动扫描绑定、运行时构图或其它玩法变更。玩家挖掘仍按世界正圆笔刷。同步时发现用户另改4310272260181104433碰撞块，核对它不属于洞源输入后原样保留；仅替换Tilemap，其余71块保持当次快照字节一致。
- 验证：真实Unity Prefab Stage/URP SceneView相机15项通过，覆盖矩形角部、直边外扩、圆角内外、对应Tilemap、外扩0、显示保留/隐藏源重复生成、Grid比例不累乘、保存重开及实际渲染。证据C:/Users/admin/AppData/Local/Temp/brainpunk-dig-spawn-radius-20261012-9222474ff4604f0e87f514a8ccfee24c/contour/，以verified.log、checks.txt、collider-hole.png、merge-checks.txt为准；旧日志包含临时验证工具API拼写错误和像素边界阈值失败，均不作为通过证据。像素/格量化存在，正式Editor新轮廓与移动端未测。回滚仅恢复本次绑定/图数据快照，不能覆盖期间其它Collider改动。

### 分辨率与性能参数建议（未自动修改）

当前首关maskWidth/maskHeight=512×512、collisionSubdivisions=6、progressResolution=192、strokeStepRatio=0.1。提高视觉清晰度应调整Sand的SceneSpriteBrushClear Mask Width/Height，再InitMap/保存；不应以提高进度分辨率替代视觉遮罩。首选1024×512；横向世界像素0.0346→0.0173，纵向保持0.0131。当前Sand17.7×6.7适合宽纹理，进一步可在目标机试1536×512，横向0.0115，与纵向接近；1024×1024只进一步提高纵向，对整图盖章成本更高。

| 遮罩尺寸 | 相对盖章像素量 | 双R8 RT | 可读RGBA32初始纹理CPU+GPU | 合计估算 |
| --- | --- | --- | --- | --- |
| 512×512 | 1倍 | 0.5 MiB | 2 MiB | 2.5 MiB |
| 1024×512 | 2倍 | 1 MiB | 4 MiB | 5 MiB |
| 1536×512 | 3倍 | 1.5 MiB | 6 MiB | 7.5 MiB |
| 1024×1024 | 4倍 | 2 MiB | 8 MiB | 10 MiB |

这些是按源码格式与像素数计算的存储/工作量估算，不是实测总内存或帧时间；R8不支持时源码回退ARGB32，双RT成本为表内4倍，Unity/驱动/其它资源开销未包含。SceneSpriteBrushClear.StampVisual每个采样Graphics.Blit整图，GPU量随面积和采样次数增长；CPU进度单独按192网格统计，不需跟着增大。

碰撞精度单独由collisionSubdivisions控制：当前基础格0.2、细分6，世界格边0.0333；升8变0.025，范围内格数与同面积检查量约增78%。Terrain.Stamp逐格SetTile(null)，DigLevelCtrl.TryDigSegment每段采样后FlushCollider，Native碰撞更新应和GPU耗时分别看。先保持6。strokeStepRatio保持0.1：它既决定采样数量，又被TryDigSegment用作首笔最低耗墨，随意调大会改变预算/玩法，不能当成纯性能旋钮。

待手机验证：代表性中低端Android连续长划/快速划线、进关与重开，Unity Profiler分别查看GPU盖章、主线程TilemapCollider2D/Composite更新及内存，再决定更高档尺寸。当前没有手机帧耗证据，不宣称1024×512已达到移动端60fps。本轮不修改性能参数，也不把桌面隔离渲染当作真机性能。

## 2026-10-12 Sand 尺寸、正圆笔刷与 IsShow（当前地图规则）

本节覆盖下一节中固定格范围、仅预留洞隐藏、形状源必须使用独立数组等旧约定；其它选关、云控、打点、墨水与生命周期约定保持原职责。

### 操作与参数

- 调整 Sand 的 Transform 尺寸、位置后，在关卡 Prefab Mode 点击 InitMap 并保存。碰撞范围由 Sand 实际矩形推导，取消独立固定 cells 参数；旋转后的地形只填充沙土矩形内的格中心。格大小和细分仍由既有 Grid/collisionSubdivisions 控制，重复生成不累乘比例。
- BaseDigComp 新增序列化 isShow，默认 false。InitMap 读取全部显式引用，包括 inactive 节点，生成后执行 SetActive(IsShow)；切回 true 再生成可恢复激活。Actor/Germ 源 Prefab 显式设 true，需要保留的砖块也设 true。这个字段只控制烘焙后的节点 active，不创建/销毁对象，不在运行时切换。
- Spawn Holes 可绑定玩法对象或需要保留的障碍物。有 Hole Shape 时按同节点 Collider2D 轮廓挖洞，忽略 Radius（包括 Radius=0）；为空时按世界 Radius 生成圆洞。Reserved Holes 保留既有 Sand 子节点形状引用入口，同样服从 IsShow。
- Outward Distance 是 Collider 轮廓外额外挖出的世界距离，例如 0.01 在轮廓四周多留 0.01；不改变对象自身 Collider，也不扩大未绑定 Hole Shape 时的 Radius 圆洞。保留砖块且预留轮廓空隙：显式绑定砖块 BoxCollider2D，勾选 Is Show，调整外扩后重新生成。
- 当前已保存首关 FixedBrick 在同步期间改为 Radius=0.5、Hole Shape 空、外扩0.1、IsShow=true；导出采用这一最新配置，没有回写此前 Collider/0.01 配置。当前使用圆洞，外扩不参与计算。用户若需要矩形外扩，重新绑定 BoxCollider2D。

### 改动、资源与调用链

- BaseDigComp.cs、DigTerrainCtrl.cs、DigTerrainCtrl.Editor.cs、SceneSpriteBrushClear.cs；Actor/Germ 两个源 Prefab 与 DigLevel_001 的地图数据、Maps 遮罩/材质。仅补充与新行为直接冲突的注释，其余旧注释保留。没有最终 Shader 源码改动、旧关脚本/资源改动、Packages/GuruSDK/GF 写入，也没有新 DB/存档/事件/SDK 路径。
- InitMap 先校验形状/引用与有限尺寸，再定向重导入显示 Shader 并检查支持/编译状态，重填 Tilemap、烘焙已有形状、保存遮罩/材质并重载生成材质，刷新 Prefab Stage。错误指出 Spawn Holes 下标与对象名；Collider 源不再被 Radius=0 校验阻断。
- ContainsPoint、Stamp、烘焙像素使用同一 Sand.localBounds。玩家 brushRadius 是世界半径，StampEraseAt 的新重载接收横纵 UV 直径：各自等于世界直径除以对应世界边长，GPU/CPU 使用同一比例；非等比缩放后仍是世界正圆。旧单参数 API 保留原笔刷/软边/进度算法。
- BindRuntime 继续只加载保存的 Tilemap/初始遮罩；进关不填土，不遍历洞源，不生成出生洞。当前17.7×6.7、y=-0.45，烘焙范围532×202，origin=(-266,-114)，有效实心格因出生洞不同而变化。只替换 Tilemap/Terrain 两个 YAML 块，其余69块逐字保留；Actor/Germ 的 IsShow 是单独显式字段修改。当前首关文本约28.87MB，体积增加来自按实际尺寸填充高密度地图，不宣称加载/运行性能改善。

### 原因、验证与边界

- Editor.log 记录旧 InitMap 在 ValidateHoleSources 因 FixedBrick Radius=0 提前退出；旧固定40×40基础范围与17.7×6.7视觉范围不一致，旧UV笔刷也未抵消缩放。这些是源码/日志事实。现有 MaskRTSprite 在 URP17 Renderer2D 的 Game/SceneView 类型相机能渲染，supported=true、没有该 Shader 编译消息，因此撤回“旧 Shader 必然不兼容 URP”的假设。
- 用户在正式工程完整编译、重开 Prefab、执行 InitMap 并保存后回报“已经恢复正常颜色”。这是用户实测反馈；具体导致旧粉色的原始 Editor 材质/编译缓存状态未直接取得，不把其唯一根因写成已证实。
- Editor/Android 全源定向 C# 编译均0 error；不是 APK/IL2CPP 或双端真机验证。真实 URP17、同生产 Renderer2D/GlobalSettings 的隔离 Unity 检查：27项编辑态、34项 PlayMode 通过。覆盖当前宽矩形/改尺寸位置/25°旋转、重复生成、Collider Radius=0、外扩、隐藏与恢复、inactive 重读、玩法对象保留、加载不改 Tilemap、出生洞GPU、64方向正圆、真实Physics2D内外边界、恢复遮罩、旧API GPU逐像素与CPU进度（软边0/0.01）。另当前最新圆洞配置导出检查通过。
- TEMP证据：C:/Users/admin/AppData/Local/Temp/brainpunk-dig-spawn-radius-20261012-9222474ff4604f0e87f514a8ccfee24c/resize/，以 resize-final.log、editor-checks.txt、runtime-checks.txt、current-export.txt、map-merge-checks.txt、compile-*.log 为最终证据。shader-before/scene、immediate/created、失败日志保留作反证；不把中间28项当作初态完整验收。
- 过程纠正：隔离测试直接在播放首帧创建第一个实例，Bind 后GPU=1、下一帧RT.IsCreated=false，读RT触发重建为0。复制 Shader 和恢复 active 没有解决，相关试验已撤回。最终场景先渲染启动相机再创建关卡，对齐 DigPlayCtrl NextFrame/Loading完成后加载的阶段，首次与后续遮罩均正常；不是靠运行时延迟重试或重新挖洞修复。
- uncertain：原始粉色唯一根因、直接播放首帧/设备丢失 RT 后的恢复、完整Undo、Android/iOS/IL2CPP、移动性能与包体、复杂Collider、Sprite Tiled/Sliced/翻转、父级剪切变换。已验证范围为当前 Simple矩形Sprite、正比例轴尺寸及Z旋转，像素/格中心仍存在量化边缘误差。
- 回滚只撤本节尺寸/UV重载/IsShow/编辑器刷新改动，成对恢复相应地图快照；保留用户当前布局、FixedBrick参数和其它任务。快照位于resize目录，不能全文件回退覆盖并行改动，也不恢复已被上一节替代的运行时构图。

## 2026-10-12 Prefab 地图烘焙与 BaseDigComp（地图当前约定）

### 需求、对象与操作

- 用户要求 BaseDigComp 提供出生半径、DigGermCtrl 继承，随后明确将地图构建移到编辑器，用 Odin InitMap 生成，进关不再填土或挖出生洞。预留洞由 Sand 子节点上的 Collider2D 形状与外扩距离定义，生成后隐藏节点，下次仍读取它们；本节只替换地图生成时机，不改变下方配置、选关、云控、打点或奖励约定。
- BaseDigComp 是可挂载的具体基类；DigGermCtrl、DigActorCtrl 继承它。Radius 是圆形出生洞世界半径，默认 0.75，编辑器 InitMap 读取，与 CurrentSize/GrowthSize/unitRadius 分离。
- 打开关卡 Prefab Mode，在 **Sand 下创建子节点**，添加 **BaseDigComp + Collider2D**；将同节点 Collider 拖到 **Hole Shape**，设置世界单位的 **Outward Distance**，将 BaseDigComp 拖到根 DigTerrainCtrl 的 **Reserved Holes**。Germ/Actor 保留在 **Spawn Holes**，不要放进隐藏数组。
- 点击根 DigTerrainCtrl 的 Odin **InitMap**，再保存 Prefab。Reserved Holes 节点设 inactive，引用/形状/参数保留；再次 InitMap 不按 active 过滤。编辑 Collider 时可暂时激活标记，修改后重新生成。位置/Radius/形状/外扩/细分/格范围/挤出变更后需 InitMap 和保存，不在 OnValidate 或播放态自动构图。

### 最终职责、文件与资源流

- 新增 BaseDigComp.cs 与手写 DigTerrainCtrl.Editor.cs partial；修改 DigActor/Germ 继承、DigTerrainCtrl、DigLevelCtrl 地形接入、SceneSpriteBrushClear 可选 initialMask、两个源 Prefab 的 radius 与首关地图字段/资源。Editor partial 仅 UNITY_EDITOR；没有生成器、DB/单例/事件/SDK 入口，不修改 Packages/GuruSDK/GF 或旧关脚本/资源。
- InitMap 仅允许 Assets/Game/Prefabs/DigLevel/ 的 Prefab Stage。读取本地配置生成 Tilemap/洞口，保存 Grid 比例/挤出/格范围，输出 Maps/<Prefab名>_MapMask.asset、_MapPreview.mat。Texture2D/Material 是明确的 Editor 资产用途，不实例化运行时 GameObject；只 SaveAssetIfDirty 两个生成资产，不全局 SaveAssets，正常保存 Prefab 落盘其它对象。
- 预留洞直接读取 Collider 本地字段、offset、Transform 计算世界轮廓距离，不依赖 bounds/ClosestPoint/编辑器物理注册，不临时激活标记。实现支持 Circle/Box/Polygon/Capsule/Edge，其它类型报错；Polygon 多 path 按区域并集，Edge 外扩形成通道。复杂带孔/自交 Polygon、Composite/Custom 不属于支持/验证结论。
- DigLevelCtrl.InitializeRuntime → Terrain.BindRuntime 验证已保存地图，缓存范围/玩家半径，把遮罩装入 RT。没有 ClearAllTiles/SetTilesBlock、出生对象遍历或出生 Stamp；未烘焙关卡按既有加载异常路径失败，不提供运行时生成兜底。
- SceneSpriteBrushClear.initialMask 为可选可读资产，Dig 从它恢复 GPU/CPU 初态；无 initialMask 的旧关仍为全实心/空进度。玩家 Stamp/耗墨/碰撞保持原路径；Reset/Replay 仍整关重建，读取保存的 Tilemap/遮罩，不重新计算洞。中途按次笔刷重载已撤回，最终不保留无生产调用的 API。
- 首关已烘焙原出生洞，Reserved Holes 为空，没有添加未指定玩法洞。同步只改六个地图块/字段，其余 64 个 YAML 块逐字相同，保留用户 Sand 尺寸/位置、CameraGizmo 与并行扩展。Prefab YAML 约 14.63 MB，保存高密度 Tilemap；包体实际压缩/加载/内存未测，不宣称性能提升。
- 旧“每次新关构图/重开生效”文案与新时机冲突，只修正这些注释/调参提示，其它旧注释保留。

### 验证、边界与回滚

- 最终 Editor/Android 全源定向编译均 0 error、113/118 warning；纳入并行新 Choice/Ads/Analytics/游戏侧 SDK partial，未改这些文件。不是 APK/IL2CPP 或主工程完整导入验收。
- **17 项 Prefab Stage 检查**通过：非播放态、可读遮罩/预览材质、出生洞、重复比例/格数据/GUID、Box 非圆形/外扩、Circle/Polygon、inactive 再生成/参数修改、玩法对象激活、保存重开。**24 项真实隔离 PlayMode 检查**通过：接入前后 Tilemap 不变、GPU/碰撞读取、隐藏、初始进度、实时挖掘、视觉/整关恢复、未烘焙拒绝、成长/暂停/唯一胜负/失败重建；旧入口 CPU/GPU 基线一致，软边 0/0.01 的 GPU 逐像素一致。
- 证据：C:/Users/admin/AppData/Local/Temp/brainpunk-dig-spawn-radius-20261012-9222474ff4604f0e87f514a8ccfee24c/bake/，最终以 editor-checks.txt、runtime-checks.txt、bake-playmode-verified.log、compile-*-final.log 为准。中间 failed.txt 是历史残留；事件为替身，URP 相机仅序列化占位，生产渲染未由此验证。真实基线导出后，形状/玩法使用固定 8×8 隔离范围，未回写工作区尺寸。
- uncertain：正式 Odin/Scene 视觉、完整 Undo、Capsule/Edge/复杂形状、URP/生产完整入口、Android/iOS/IL2CPP、包体/性能。最短验收：Prefab 创建绑定 Box 标记，InitMap/保存/重开/再生成，再正式进关挖掘、失败、Reset/Replay、通关，检查隐藏与视觉/碰撞。
- 过程纠正：Box 失败来自误把实际 19.93×8 当作 8×8，已撤回编辑器物理故障归因；后续尺寸并行修改后测试固定隔离范围。遮罩补回 RenderTexture.active 原状态边界，BindRuntime 接入 GPU 初态；没有延迟/重试或运行时开洞。前置核对/失败证据见当天记录。
- 回滚只撤本轮 Base/Editor、初始遮罩与六处图块，成对恢复实施前快照并删除 Maps 资产/meta；保留 OnGUI 与其它任务。恢复旧构图须同步 Terrain.Initialize 和关卡调用，不能只删资产。

## 2026-10-12 第二轮：30关配置、独立选关、云控和四玩法打点（当前有效）

本轮覆盖下文第一轮“没有正式统计、只有本地排序、TBC合成排序号999”的限制。普通配置40001～40030、TBC真实数据40999；DigLevelData墨水字段与Draw同构，普通20、阈值0/4/10；本地TBC追加为排序31。首页打开独立 `DigGame_ChoiceLevelPanel`，item和starGroup各自复制；解锁/分页/广告/星级只读Dig档。独立diggame_config与14个字段、Luban/Guru默认/消费者闭合，沿现行ForceFetch、Loading和安全延期提交链。用户确认后的四玩法累计与生命周期详见打点权威文档。

新增配置注入 `DigLevelCtrl.ConfigureInk`，在初始化前接受真实表数据；不调整吞噬/地形算法和同步地图烘焙任务。原Prefab局部值仍供编辑器预览；运行时由配置覆盖容量/评星。

完整文件、前后行为、数据/UI/广告/事件流、51项模拟断言与编译证据、场景矩阵、剩余风险和局部回滚见 `Assets/Game/UI/LevelChoicePanel/Scripts/DigGame/DigGame选关与云控功能及风险记录.md`。普通第2～30关尚无Prefab，生产UI/真机SDK/iOS与后台收数未验收。本节以下第一轮记录保留为历史基线，不作为第二轮云控/打点状态依据。


## 2026-10-12：Draw 基线的基础功能扩展（当前设计）

### 需求、原行为与维护边界

本次用户明确要求拓展 Dig 控制流程和 HUD 资源结构，HUD 从 Draw 复制，增加墨水评星、广告翻倍、提示、`DigLevel_999` 和 `deep_link_sub1=dig`；用户确认沿用 Draw 规则。此前版本没有墨水/星级、HUD 继承主线且隐藏提示、末关返回首页、OneLink 不解析 dig。本节替代旧章节中上述首版限制，原挖土、批次吞噬、大小比较、失败表现与全黑重建继续使用原实现。前置核对见 `DevelopMD/已有功能设计与使用记录/2026-10-12.md` 的 DigGame 章节。

### 最终代码和资源结构

```text
Scripts/Play/Dig/
  DigPlayCtrl.cs             # 控制器、转场、墨水/提示会话、结果与独立进度
  DigGameLevelCtrl.cs        # 本地排序、最高星、末尾 TBC、链接回退
  DigPlayControllerPolicy.cs / DigGameResetEvent.cs
Scripts/Level/DigLevel/
  IDigLevelRuntime.cs / DigLevelCtrl.cs / DigTerrainCtrl.cs
  DigActorCtrl.cs / DigGermCtrl.cs / DigSizeRules.cs
  Level1/                   # 原首关结构与风险记录
  Level999/DigLevel999Ctrl.cs # TBC 只读占位运行时
UI/DigGame/
  GamePlayPanel/{Scripts,Resources}/DigGame_GamePlayPanel.*
  TipsPopup/{Scripts,Resources}/DigGame_GameTipsPopup.* 与 _2 变体
  Scripts/ 与 Resources/    # 保留双胜利面板，增加显式星组引用
Prefabs/DigLevel/
  DigLevel_001.prefab / DigActor.prefab / DigGerm.prefab / DigLevel_999.prefab
```

- HUD 脚本与资源移动时保留 GUID（脚本 `442589a9b5724f0493b97ff461e241d0`，Prefab `d5d94497926d4024b274cd0cb12b735f`）。新面板继承 `BaseUIView`，相同命名层级的 Generated 字段复制自 Draw；原主线和 Draw 文件不改。
- 复用 `DrawGame_InkProcessHandle` 与 `DrawGame_VictoryStarGroup` 的纯表现接口。Dig 向其注入 `DrawInkState` 格式，数值单位是 Dig 世界长度，不读取 Draw 运行时或 Draw 存档。PencilShop 与倒计时在副本初始隐藏；保留 Shop、Reset、Pause、墨水与提示。
- `DigGame.cs` 新增 `levelCollectedStar`，以普通 `DigLevelData.Id` 保存最高星；`_Partial.cs` 仅按已核对的 PartialClassGenerator 同步初始化与属性。旧档由手写 `EnsureValidCollections()` 补空字典，原进度/分享/评价保留。没有新增等待动画存档或正式关卡统计。

### 墨水、广告、提示与结算流程

1. 首关显式配置 `maxInkLength=20`、`inkStarStage=(0,4,10)`。单位为世界轨迹长度；阈值是一/二/三星所需的剩余绝对墨水，初始化时限制到非递减的有效容量范围。后续关卡在各自 Prefab 配置，不手补 Luban 生成字段。
2. 出生/预留空腔已在编辑器 InitMap 烘焙，进关只加载地图。当前仅擦到新沙的采样耗墨，完全空洞免费；有效首章至少消耗一个采样间距（当前首关0.05），移动按有效采样的世界轨迹长度计费，静止及沙土范围外不耗墨。余额不足裁剪末段，耗尽停止新增洞口，已有洞口、物理和吞噬继续运行。
3. `InkChanged` 是当前关卡接口上的局部事件，控制器订阅并刷新当前 HUD；不新增 PlayEvents 全局广播。正常通关仍要求全部病菌吞噬且表现完成；金币跳关使用单独 `TryCompleteByHint()`，不放宽 `TrySucceed()`。
4. 广告入口与 Draw 一样在首次耗墨后隐藏，只允许同关领取一次。请求前先暂停物理和动画并结束笔画，HUD 锁输入；成功加一份基础容量（首关总容量 40），失败/取消可重试。回调核对当前 HUD 打开版本、控制器身份和关卡版本，旧回调不向新关加墨；关闭 HUD 会使回调失效并释放本面板持有的暂停。等待弹窗关闭取消沿用 `RewardAdHelper`/`WaitingPopup`，不新增超时系统。
5. 提示复制 Draw 的金币弹窗；价格沿用既有 `GetDrawGameGameTipsPopupCoinCost/PassLevelCoinCost`，没有修改远端拉取/排序。未获取时买提示，已获取时打开 `_2` 金币跳关变体；无限金币权益沿用 Bag 入口。金币不足打开现有 CoinGiftPopup。提示/跳关须匹配当前版本；操作无效不扣币。首关原先绑定无 Draw 脚本的 `DigHintPath`；2026-10-13 已按新需求替换为 `DigGame_TipsObjectSubObject` 完整根对象，复用 Draw 的纯表现路径渲染/呼吸组件，仍不接入 Draw 关卡状态或碰撞。
6. 同关 Reset、自动失败重试、Victory Replay 完整重建地形和物理，清耗墨并恢复已领取的一份容量及已买提示；切关/退出清两项资格。胜利面板创建后，由控制器写最高星/普通通关/下一关解锁；面板展示最高星，只为本次新增星播放动画，不读 Draw 档。
7. 原生覆盖层暂停 Unity 帧推进时，不依赖 ignoreTimeScale 推动物理或重建；进入广告前已处于安全暂停状态，回调后才恢复。真实 SDK 丢失结果回调时的恢复仍为平台验收缺口。

### TBC 与 deeplink

- `999` 为本地保留排序 ID，运行时构造占位配置 `id=40999/path=DigLevel_999`，不是声称 Luban 已有该行。TBC 不计 `Levels` 普通总关数，也不写普通通关、最高星、首胜奖励。
- 最后一个普通关通过后解锁并进入 TBC；旧档全部通关时初始化迁移到 TBC。后续增加本地普通关时，原停在 TBC 的档案续入首个未通关关卡并解锁该目标。
- TBC 复制 Draw 末关美术/相机/传送门布局，移除 DrawLevelCtrl、DrawLineFeature、主线 Actor、死亡区和入门演出，绑定独立 `DigLevel999Ctrl : IDigLevelRuntime`。保留主线传送门点击，暂停/加载/结果锁和 UI 指针门禁；没有挖掘/提示/墨水/通关。创建 HUD 时复用 TBC 音效。完整画面和触控需主工程验收。
- 协议：`deep_link_value=open_level`、`deep_link_sub1=dig`，`deep_link_sub2` 可为 `DigLevel_001` 或 `DigLevel_999`。空或无效目标回退当前序列首个未通关关卡，全通回退 TBC；仅加载配置内目标，路径不作为任意资源地址。
- `OneLinkRequest` 处理平铺与 iOS click_event；`OneLinkLevelRouter` 增加 Dig 分支，`GameManager.TryEnterDigLevel` 复用首页控制器切换/失败回首页链，仅链接可解锁本次目标。加载揭幕完成调用 ConfirmLevelEntered，退出/失败经 EndEntry 清来源。已有首页空闲、重复请求、首装预算与关内晚到丢弃规则保留。SRN 尾标额外支持 `DL-OL-dig-001/999`，其它来源规则未改。

### 验证证据与剩余风险

| 范围 | 本轮证据 | 结论边界 |
|---|---|---|
| 源码编译 | Unity Roslyn/Bee 引用，最新 1205 源码，Editor 0 error/113 warning，Android 0 error/116 warning | 条件源码编译，非资源包/IL2CPP/真机 |
| 核心玩法 | 隔离 Unity 6000.0.59f2 PlayMode 75 项断言 | 真实 Dig 源码、Prefab、Tilemap、物理与 Tween；共享事件替身，URP/UI/平台服务未集成 |
| 配置/存档/协议 | 33 项托管断言 | 当前解析/SRN/排序/DB字段实现及 Dig 路由方法；Unity、DB服务、GameManager 和传输外围替身 |
| Prefab 静态 | 49 项检查，原脚本/资源 GUID 保留，anchor 唯一、local fileID 闭合、提示变体和 TBC 门禁引用存在 | 不证明实际屏幕布局、热更采集或触控 |
| 未覆盖 | iOS 宏两处既有 Guru.UniKit.Apple 缺引用；真实广告、完整 HUD/星组/弹窗、原模式回归、Android/iOS 横屏和性能 | `uncertain`，须按验证记录人工清单补验 |

隔离工程开始时缺 Physics2D/Tilemap/IMGUI/ImageConversion 等内置测试依赖，编译失败后仅补临时工程，正式 Packages/GuruSDK/GF 未写。资源静态检查发现 TBC 复制相机残留已移除 Actor 的 target 引用，已设为空；重复检查通过。当前并行出现的 Editor 数值显示、首页与字体更改保留，不作为本轮实现归属。

验证期间同步新增 BaseDigComp 出生半径、Actor/Germ 继承和地形出生圆章调整；已保留，并按最新源码/首关资源重跑 75 项 Unity 检查与 1205 源码编译。该组并行修改不作为本轮实现归属；没有回写或覆盖它们。

完整脚本、命令、日志及逐场景验收清单见 `DevelopMD/验证记录/DigGame_2026-10-12/验证记录.md`。性能仍按横屏移动端 60 fps 目标验收，尤其首关 57600 碰撞格、连续划动 GPU 盖章/碰撞重建及奖励动画；未做 Profiler，不能把隔离断言当作帧率结论。

### 回滚

先撤 Dig 链接路由/SRN 新分支并关闭 Dig 入口，再按本轮 diff 撤墨水/提示/星级/TBC 接入。HUD 移回原路径且保留原 GUID，恢复首版独立 GamePlayPanel 子类；不要修改原主线/Draw 面板。玩家最高星新字段可保留为空或历史值，禁止清玩家进度。撤首关新增提示节点和四个字段时保留用户相机/精度/美术以及同步 Editor 显示；不整文件覆盖、不改 Packages/GuruSDK/GF 或其它已有任务。

## 2026-10-12 数值标签改为 Editor OnGUI

- 需求：删除场景中的 SizeLabel 及配套底板，以 OnGUI 在界面内显示数值，且仅 Unity Editor 生效。原行为为主角显示当前大小、病菌显示大小/成长量、首关显示 DIG TO EAT；两个对象初始化强制要求 TMP 引用，成长时刷新、吞噬/死亡时隐藏。
- 修改前核对：见 `DevelopMD/已有功能设计与使用记录/2026-10-12.md` 对应主题。已读取 Editor 态约束及本模块/首关说明，检查初始化、批次判定、成长、认领、吞噬、死亡、暂停和退出清理。指定 037 feature plan 缺失、CodeGraph CLI/MCP 不可用，以当前源码和 YAML 为依据。此节只替换展示约定，不变更其它章节的玩法要求。
- 影响文件：`DigGermCtrl.cs`、`DigActorCtrl.cs`、`DigLevelCtrl.cs`；`Assets/Game/Prefabs/DigLevel/DigGerm.prefab`、`DigActor.prefab`、`DigLevel_001.prefab`。两个对象脚本删除 TMP using、sizeLabel 字段及显示/校验语句；原 Initialize 注释提及“文字”已失效，只改为“体型”。删除两个源 Prefab 的 SizeLabel/SizePlate、首关的 DIG TO EAT 节点，以及四处嵌套文字样式 override。资源 GUID、身体/眼睛、碰撞、数值与位置 override 保留。
- 新展示入口：`DigLevelCtrl.OnGUI` 与缓存样式均在 `#if UNITY_EDITOR` 内；只在播放、已初始化、结果未锁定的 Repaint 显示。右侧固定面板显示 DIG TO EAT、Actor 当前大小及各未认领且激活病菌的大小/成长量；保留原数组编号，认领后不再显示该行，失败/成功锁定后隐藏面板。这里的 Editor 生效指 Editor 播放的 Game 视图，不添加非播放态 Scene/Prefab 预览。
- 数据/资源/UI/事件流：OnGUI 仅读取关卡已显式绑定的 actor/germs 及其数值属性，不写数据、不查找节点、不创建场景对象、不引入 DB/存档/资源/全局事件/SDK 入口。体型、批次胜负、成长、Tween、暂停及清理业务保持实施前源码；正式玩家构建有意取消这些文字和底板，现有 Dig HUD 不在清理范围。
- 验证：Unity 6000.0.59f2 自带 Roslyn 与既有 Bee 引用定向编译 1204 个源码，Editor 为 0 error / 113 warning，Android 非 Editor 宏为 0 error / 116 warning。读取编译 DLL 元数据确认 OnGUI 和 `_debugLabelStyle` 仅存在于 Editor 产物。13 项静态核对通过：两个对象只删除 TMP 语句；移除新增 Editor 区段后 DigLevelCtrl 与实施前快照一致；三个 Prefab 非调试内容保持、所有本地 fileID 引用闭合、无标签/TMP/失效嵌套文字引用。收尾按本次文件执行 `git diff --check`，exit=0。
- 验证证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-editor-labels-20261012-c97c969475204a1f888010901655684a/` 的 `compile-editor.log`、`compile-android.log`、`compiled-metadata.txt`、`source-prefab-checks.txt` 与实施前六份快照。编译不是主工程 AssetDatabase 导入、PlayMode 或 APK/IL2CPP 验证；静态一致性不代表已复测完整关卡。
- `uncertain`：尚未实测 Game 视图布局、小窗口可读性、运行时 HUD 遮挡，以及 Android/iOS 实际资源加载与横屏。最短复核为首页进入 Dig，观察初始主角 10 和三只病菌，吞噬后数值增长/病菌行隐藏，暂停/恢复、失败重建、成功和退出后确认显示门禁；改变 Game 视图尺寸检查面板。非 Editor 包应无该面板和原标签/底板。
- 回滚：移除本轮 Editor 显示区段，恢复两对象标签字段/校验/刷新，并从实施前快照恢复三个 Prefab 中被删对象、父子/字段引用及四处文字 override；代码与资源必须成对恢复。只撤本轮增量，保留任务前或同步发生的 Dig 扩展与首关序列化变化，不整体覆盖 DigLevelCtrl、首关 Prefab 或日记录。没有性能测量，不宣称帧率提升。

## 2026-09-30 碰撞贴边调整与调参

- 用户在边界错位截图后授权“改改看，把参数暴露给我调整”。本轮只改 `DigTerrainCtrl` 和 `DigLevel_001.prefab` 的地形参数，不改相机、UI、胜负规则或共享擦除组件。
- 原先按“完整格落入圆”删格，现改为按格中心落入圆删格，取消半格对角线收缩。保留隐藏 Tilemap + CompositeCollider2D，不引入像素轮廓重建。
- 隔离实测发现原因不止收缩和挤出：直接缩小 `Grid.cellSize` 时，格位置虽细分，真实碰撞仍有较大偏差；刷新 Tile/重新启用 Collider 也未改善。现在保持基础格尺寸 0.2，通过 `SandGrid` 变换缩放与扩大索引范围同步细分格位置及碰撞，世界范围仍为 8×8。该节点只包含隐藏碰撞 Tilemap，不缩放沙土视觉、主角或病菌。Unity 内部成因未确认，不宣称是某个已证实的引擎缺陷。

在 `DigLevel_001` 根节点的 `DigTerrainCtrl` 调整下列字段；修改 Prefab 后 Reset 或重新进关生效，运行时实例的临时修改不会写回 Prefab。

| Inspector 参数 | 当前首关值 | 调整作用 |
|---|---|---|
| Collision Subdivisions | 6（保留用户值；脚本默认 4） | 越大台阶越细，当前格宽约 0.0333；格数随平方增长，6 为 57600 格 |
| Collision Radius Offset | 0 | 世界单位；正值扩大碰撞洞口，负值缩小。建议每次 0.005～0.01；过大会让身体进入可见沙土 |
| Collider Extrusion | 0.001 | 世界单位；越大洞口越窄，0 关闭挤出，旧值为 0.01。由本字段统一换算，不直接调运行时 TilemapCollider2D |
| Stroke Step Ratio | 0.1 | 连续采样步长为半径乘比例，即 0.075；越小快划采样越密，工作量越大 |

- 新增贴边参数仅初始化读取；正偏移同步扩大删格遍历范围，视觉笔刷半径仍为 0.75。挤出会按碰撞节点世界缩放换算：细分 6 时 TilemapCollider2D 显示 0.006，本字段 0.001 才是世界挤出量，不能把两者误认为配置不同步。
- 最终源码的 14 项真实隔离 Unity 贴边检查、48 项核心玩法检查通过，Editor/Android 条件编译 0 错误。细分 6、圆心 (2,2)、视觉半径 0.75 时，72 方向碰撞平均绝对半径误差由 0.14759 降为 0.01047；这是该固定场景的物理射线/GPU 遮罩测量，不是所有笔画或主工程画面验收。完整证据见 `DevelopMD/验证记录/DigGame_2026-09-30/验证记录.md`。
- 仍有格子离散和纹理采样误差；主工程截图对照、任意复杂笔画、Android/iOS 横屏与性能为 `uncertain`。本轮仅因策略已变更而更新 `Stamp` 的旧保守删格注释，其它无关注释不改。
- 局部回滚：仅撤本轮格中心判定、Grid 缩放和两个参数及 Prefab 挤出值；不要回退此前精度/胜利修改或用户的相机、细分、其它资源编辑。

## 2026-09-30 精度、胜利入口与 UI 缺失排查

- DigTerrainCtrl 增加 `collisionSubdivisions`（1～8，默认 4）与 `strokeStepRatio`（默认 0.1），显式绑定 `collisionGrid`。基础 Grid 为 0.2，细分 4/8 对应 0.05/0.025；同时扩大格子索引范围，保持 8×8 世界范围。重开生效，格子数量按细分平方增长。用户正在 Inspector 调参，Prefab 值不强制回写默认。
- `DigLevelCtrl.TrySucceed()` 是正常胜利入口：全部病菌已吞噬且动画完成才允许提交一次 Resolved。控制器收到有效结果先冻结关卡/HUD，再打开 `DigGame_GameVictoryPanel_Reward`（首胜）或 `DigGame_GameVictoryPanel`（重复）；面板创建成功后记录通关，异常经已有返回首页路径处理，失败不写通关。
- Draw 的关卡报告结果／玩法管理 UI 和存档／遮满后重建／揭幕后输入职责保留；不把 UI 创建塞进 DigLevelCtrl。Dig 不接墨水/星级/正式统计，HUD 按用户要求继承 GamePlayPanel 而非修改原件。
- UI 缺失发现生产相机与 Draw 的 URP 组件配置不一致；用户同步补入 Dig AdditionalCameraData，本次移除 Agent 重复组件，保留用户改动。Main 场景相机与 GameManager.StartCamera 是不同对象，本次未改 Main 相机或 HUD 排序/布局。
- 此阶段地形仍使用“完整格落入圆才移除”的保守轮廓及 Tilemap 0.01 挤出，精度参数不等于完全贴边；当时的归因不完整。后续用户已授权格中心近似，实际碰撞几何的补充发现及最终调整见文首，保留本节作为前一阶段记录。
- 本轮 48 项隔离 PlayMode 通过、Editor/Android 条件编译 0 错误。隔离项目不含 URP/生产 UI/DB/平台服务，主工程视觉回归和 Android/iOS 真机均 `uncertain`。细节、基线、并发资源修改归属及回滚见 `DevelopMD/验证记录/DigGame_2026-09-30/验证记录.md`。

## 2026-09-29 目标分支移植

- 按用户选择，仅将本次 Dig 改动应用到 `feature/1.16.0_common` 的 `1d4163fb` 基线，不合入来源分支额外 29 个历史提交。原工作区及其未提交改动保留；移植在独立 worktree 完成。
- GameManager、LoadManager、StartPanel 仅应用 Dig 增量，保留目标原有 OneLink/启动礼包流程；共享 GamePlayPanel 不变。修改前核对及验证见 `DevelopMD/已有功能设计与使用记录/2026-09-29.md`。
- 目标源码 Editor/Android 条件重新编译均 0 错误（113/116 警告）；只读复用现有 Unity/第三方依赖，不是目标工程完整导入或移动端构建。核心及 HUD 源码统一 CRLF/LF 后与既有测试版本一致，未重新宣称完整 UI/DB/SDK 或真机通过。
- 下方实施与隔离测试记录来自本次 Dig 开发过程；目标集成的验证边界和复现日志补充于 `DevelopMD/验证记录/DigGame_2026-09-29/验证记录.md`。回滚以目标分支最终 Dig 单次提交为单位，不回退其他历史。

## 2026-09-29 HUD 隔离修订：修改前核对

- 本轮用户明确替换原计划中“直接复用默认 GamePlayPanel”的实现方式：复制独立 `DigGame_GamePlayPanel.prefab`，创建 `DigGame_GamePlayPanel : GamePlayPanel`，不得在原共享脚本保留 Dig 改动；追加要求移动按钮在新 Prefab 中隐藏。
- 当前基线：上轮在共享 `GamePlayPanel.cs`、`GamePlayPanel.PostcardStar.cs` 加入 Dig 分支，并新增 `GamePlayPanel.Dig.cs` partial。此结构将全部撤回；保留数值玩法、独立 Reset 路由、暂停、黑幕重建及输入门禁。
- 源码事实：父类 `OnCreate` 绑定主线按钮，`OnOpen` 订阅主线 Reset/Replay/Resume/星星等事件，按钮/黑幕/生成字段均为 private，不能由子类直接安全复用。`BaseUIView.OnCreate` 为空，`OnOpen/OnClose` 仅设置 protected 状态，Awake 收集动画、OnShow/OnHide 管理可见性。
- 方案：子类独立覆写创建/开关生命周期，不调用带主线副作用的父类实现；保留 BaseUIView 的 Awake/显示隐藏/层级契约，使用已有 SetData 与复制后保留的 LevelTxt 绑定。子类新增显式序列化 Reset/Pause/黑幕/遮罩引用，不反射访问父类私有字段、不生成重复成员、不执行名字查找；隔离父类私有 Update/OnDestroy。
- 资源来源：`Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab` 为实际完整面板。复制到 Dig 的 Resources 目录、换新脚本 GUID，保留层级/布局/原字段；在副本中隐藏左右移动、跳跃、Tips、Shop 和星星块。原 Prefab 不改。
- 生成边界：已核对 `GamePlayPanel.Generated.cs` 及 `BaseUIViewEditor.GenerateUIComponents/GeneratePartialClassCode`；生成器输出 private 序列化字段。本次不修改或重新生成父类绑定，不为子类生成同名字段。
- 验证：原 GamePanel 脚本与 Prefab 相对本任务前无差异；子类新引用和隐藏按钮静态核验；Editor/Android 条件源码编译；隔离 UI 生命周期/点击/取消测试。原主工程完整 UI 与手机横屏仍需实测。
- CodeGraph 索引目录存在但 CLI/MCP 不可用，本轮已尝试后改用源码和 YAML；指定 remote-resource feature plan 仍缺失。历史主线提示/星星文档仅用于识别不应进入的副作用链。
- 修订结果：原 GamePanel 目录无本次差异；独立子类与副本已接入。新增 18 项隔离 HUD PlayMode 检查通过，Editor/Android 条件源码编译均 0 错误；父类主线/UI 服务为替身，主工程整体仍 `uncertain`。最终绑定、生命周期与回滚详见 `Assets/Game/UI/DigGame/DigGame_GamePlayPanel功能记录.md`。

## 2026-09-29 实施前核对

### 需求与行为基线

- 依据：用户本轮《DigGame 首关计划：挖土、大小比较、吞噬成长与失败重置》。这是独立新模式，不修改已验收 Draw/Main/IWanner 关卡。
- 当前项目没有 Dig 运行时、关卡、配置或独立存档。保留现有模式入口、Reset、统计、奖励、资源管理和 RemoteConfig 启动时序。
- 本轮新增：主角 10，病菌 5/+5、12/+5、18/+10；大于等于可吃，成长来自病菌；小于则消灭、失败、整关重建。无限挖掘、无星级、无正式关卡统计。
- 同一物理步先按批次开始大小判断所有接触，任何更大病菌优先失败；全部可吃才提交成长。每只病菌只结算一次；全部吞噬动画结束才胜利。

### 来源与原流程

- `Play/Controller/IPlayController.cs`、`PlayModeType.cs`、`GameManager.ReadyToPlay`：Policy 选择与控制器注册、激活/停用；暂停弹窗依赖 `IPlayPauseHost`。
- `DrawPlayCtrl.BeginLevelTransitionAsync/OpenLevelAfterCoverAsync/ResetGameAsync`、`EnterLevelLoading.Create`：普通转场遮挡回调替换关卡，揭幕后输入；Reset 黑幕重建。
- `DrawLevelCtrl.TryBeginFailurePerformance/TryFailAndReset`、`DrawLevel/Level86/DLevel_086脚本结构记录.md`、中央脚本规范：关卡掌管物理/局部表现、玩法掌管加载/HUD/结算导航；失败先锁定再派发重置。
- `SceneSpriteBrushClear.StampEraseAt`：使用局部 UV；会在完成阈值停止擦除，Dig 实例阈值设 1，关闭自带输入；平方形沙土地面避免 UV 宽高映射与碰撞笔刷不一致。
- 初版曾在 `GamePlayPanel`、`GamePlayPanel.PostcardStar` 添加 Dig 条件分支；本轮用户要求改为独立 Prefab 与子类，当前规则见文首 HUD 隔离修订，原方案不再有效。
- Draw 双胜利面板与 `GameVictoryPanel_Share`：复用现有奖励/购买/分享服务；Dig 删除星级、OneLink 和每日挑战分支，并独立分享/评价记录。
- `PartialClassGenerator.GeneratePartialClassCodeStatic` 从手写存档脚本解析字段生成属性与 Init；DBManager 运行时扫描独立 SO 类。只生成 Dig partial，避免全量 DBManager 生成覆盖已有手写 accessor。
- `Luban/Configs/gen.bat`、`LubanGenerator`、`ConfigFileNames`：Excel 为输入、Luban 生成为输出；先在临时目录预览，仅同步新增表/枚举/Tables 和生成文件名。不得改 Guru YAML 或 Package/GF。

### 修改边界、影响与未决证据

- 新增 `DigLevel`、`Play/Dig`、Dig 独立 DB、Prefab、UI；共享改动为注册、配置初始化、首页入口和分享上下文。初版默认 HUD 的 Dig 分支已在后续修订撤销。
- 不改 DrawLevelCtrl/DrawPlayCtrl/既有关卡；不改 RemoteConfig 等待/ForceFetch；不接正式 level_start/level_end/b_play。仅新添用户明确要求的 `DigGameResetEvent`。
- 所有关内对象通过序列化引用绑定；禁止运行时名字/层级兜底。旧关失活后销毁，版本号和局部动画清理阻止晚到回调。
- 现有未提交字体、Android、build_info、ProjectSettings 与 `.scratch` 保留。
- 指定 `specs/037-remote-resource-loading/plan.md` 及上级相对路径均不存在；相关资源加载依据当前源码和项目约束，缺失文档不作为行为变更依据。
- Unity 编辑器当前已打开，缺少现成自动控制入口。编译、PlayMode、真机横屏将分开记录；无法实测的部分明确 `uncertain`，不由静态检查推断通过。

### 预定验证

1. 小于/等于/大于、增长来自病菌、重复触碰、同一步大小混合、吞噬中终止与晚回调。
2. 失败/手动 Reset/Replay/退出重进完整恢复；暂停保持速度、不复活已结算对象。
3. Prefab 嵌套和显式引用、Luban 生成一致性、独立进度/首次与重复胜利、首页和单关 Next。
4. Unity 编译与可用 PlayMode 测试；Main/Draw/IWanner 和 Android/iOS 横屏需要明确运行证据。

## 实施结果

### 已落地的职责与入口

| 层 | 文件/资源 | 责任 |
|---|---|---|
| 模式入口 | `GameManager`、`PlayModeType`、`Play/Dig/DigPlayControllerPolicy`、`DigGameEnter` | 注册独立 Dig 控制器；首页显示有效关卡通关数并进入当前关 |
| 配置与进度 | `Play/Dig/DigGameLevelCtrl`、`DB/DigGame*`、`DB/DB.DigGame.cs`、`DBSetting.asset` | 本地 Luban 排序、无效当前关回退 1、独立解锁/通关/分享/评价；不存关内大小 |
| 玩法 | `Play/Dig/DigPlayCtrl`、`DigGameResetEvent` | 加载、HUD、暂停宿主、唯一结算、整关重建、末关返回首页 |
| 关卡 | 本目录六个 `.cs` 文件 | `IDigLevelRuntime` 契约、主角/病菌、批次比较、视觉与 Tilemap 同步挖掘 |
| HUD | `UI/DigGame/Scripts/DigGame_GamePlayPanel.cs` 与同名 Resources Prefab | 独立子类只开放关卡号、Reset、Pause；副本隐藏移动/跳跃/Tips/Shop；原 GamePlayPanel 脚本与资源不改 |
| 胜利 | `UI/DigGame/Scripts` 与 `Resources` 双面板 | 首胜奖励版、重复普通版；复用奖励/购买/分享，去掉星级、OneLink 和每日挑战入口 |
| 资源 | `Prefabs/DigLevel`、`ArtSrc/Dig`、`UI/DigGame/Prefabs/DigGameEnter` | Unity 原生生成占位资源、独立嵌套主角/病菌和首页按钮 |

以上资源路径以 `Assets/Game/` 为前缀；模式源码位于 `Assets/Game/Scripts/`。

### 数据与运行流程

- `LevelData.xlsx/dig_level`：`40001 → DigLevel_001`，`levelType=Dig`；`LevelSort.xlsx/digLevelSort`：`1 → 40001`。`__tables__` 新增两表，`__enums__` 新增 `GameType.Dig=3`。生成的两份 JSON、四个新配置类、Tables、枚举及 `ConfigFileNames` 已同步；没有改 Guru 配置或云控。
- 存档字段为 `CurrentLevel / MaxLevel / UnlockLevels / PassedLevels / SharedLevelIds / HasReactionLevel`；生成器只生成属性和 Init，业务校验保留在手写文件。关卡显示/解锁用排序整数 Id，分享/评价用数据字符串 Id `40001`。
- 首页 → `TryEnterDig` → ControllerManager 切换 → 下一帧请求翻页 → 完全遮挡回调中替换关卡/HUD → 揭幕完成开放输入/物理。只为擦除组件复用 `SwitchGameStateEvent(PLAYING)`，不发送正式关卡统计。
- 普通暂停由既有 `GameSettingPopup` 注入 `IPlayPauseHost`；关卡缓存速度、冻结模拟和局部 Tween。应用切后台使用另一暂停标志，两种暂停不能互相错误解除。
- 每次物理触碰先入集合；下一 FixedUpdate 用该批次首次触碰时的主角大小统一判断。大病菌优先死亡，不发生先吃小病菌再通过的顺序差异。可吃批次先锁病菌/关碰撞/累加成长，再播放动画。
- 失败立即锁定结果、关闭输入/物理、取消吸入；约 0.5 秒主角消灭结束后仅提交 Failed。玩法派发 `DigGameResetEvent(Failed)`；失败路径不调用 RecordVictory。
- 手动 Reset、失败和 Replay 都调用 BeginRebuild：冻结旧关 → 黑幕 → 旧对象失活再销毁 → 新 Prefab 与 HUD 初始化 → 揭幕 → 开放。关卡 generation 和玩法 version 分别隔离旧动画与异步加载。
- 成功等全部吸入完成才提交；先计算是否首次，再保存 Dig 通关，打开对应面板。只有一关时 Next 返回首页；Replay 不变更通关历史。奖励/购买回调额外核对面板打开版本，关闭重开不会修改新面板状态。

### 验证结论（2026-09-29）

- **真实隔离 Unity PlayMode：38 项断言通过。** 包含真实 Physics2D、Tilemap、DOTween、生产核心源码和首关 Prefab；真实挖通左侧路线后依靠重力按 `10→15→20→30` 通关。覆盖重复接触、相等、混合批次正反序、暂停/后台、吞噬中失败、最后吞噬中故障注入、重建旧回调和恢复。共享事件依赖是替身，不能推出主工程 UI、DB 或转场已实测。
- **编译：** 使用 Unity 6000.0.59f2 的 Roslyn、主工程参考参数和实际源码，Editor 条件 0 错误/113 警告，Android 非 Editor 条件 0 错误/116 警告；日志没有 Dig 文件警告。不是 APK/IL2CPP 构建，也不是原工程本轮 AssetDatabase 刷新/域重载验证。
- **静态资源/生成：** HUD 隔离后 9 个 Prefab/DB 资产本地引用闭合，131 个外部 GUID 可定位；两胜利资源无星级/每日挑战引用；六份 Luban C# 与生成输出逐字一致；四份 Excel 的原有非目标单元格、样式保持，渲染查看新增数据。生产六份关卡源码与隔离测试副本逐字一致。
- 详细证据、测试源码与人工验收清单：`DevelopMD/验证记录/DigGame_2026-09-29/验证记录.md`。
- **uncertain：** 主工程完整首页/HUD/暂停/胜利/奖励/分享/购买联调，独立 DB 实际落盘与重启读取，主线/Draw/IWanner 入口和 Reset 回归，AssetBundle 构建与设备加载、Android/iOS 真机横屏和性能。不能将上述静态或隔离结果写成整体已验收。

### 结构复盘与回滚

- 参考 `DLevel_086` 和 Draw 的职责/生命周期，但不继承带画线、墨水、星级的 DrawLevelCtrl；Dig 新契约仅保留初始化、门禁、暂停、Resolved。批次吞噬、地形和暂停表现局部实现，没有迁移旧关卡或升级共享接口。
- 可复用候选：结果先锁定、同一步接触批量裁决、回调版本隔离。当前只有一关，保留 Dig 局部，暂不提炼跨模式基类。独立 Dig 不回写已验收 Draw 中央结构记录。
- 回滚时逐项撤销本次共享接入（注册/Loading/首页/分享/DBSetting/资源采集），再删除本次新增 Dig 文件及 meta，并同步撤销 Excel 中新增表/枚举后运行既有生成器。独立 HUD 随 Dig 移除，不需要改回共享 GamePlayPanel。不要整文件覆盖含其它任务修改的文件；不要删除玩家其它模式存档。测试产生的 Dig 独立存档是否清除由使用者决定。
- 关卡绑定详见 `Level1/DigLevel_001脚本结构记录.md`；包括共享擦除组件释放行为在内的剩余风险详见 `Level1/DigLevel_001风险评估.md`。

### 资源采集补充前置核对

- `LoadManager.InitUIManager` 当前注册 ResourcesUILoader；Dig 胜利面板保留 `Resources/` 路径，首页入口由 StartPanel 嵌套引用。
- `Assets/BPGame/AssetBundleCollectorSetting.asset` 的 Level 组只采集 Level/DrawLevel/WannerLevel 三个目录，不会自动采集新 DigLevel。必须新增同组的 DigLevel `CollectPrefab + PackSeparately` 采集项，否则编辑器可读不代表移动包可加载。
- 本次仅新增一个目录采集项；不调整既有分组、过滤器、资源初始化、远端模式或已有关卡路径。DevelopMD 文件名检索未找到独立资源采集说明，以该设置与当前加载器为证据；实际 AssetBundle 构建/下载仍待验证。


## 2026-10-12 Dig DeepLink 专属胜利与导航（当前扩展）

- 通过OneLink已确认进入普通DigLevel后，首胜/复胜均用GameVictoryPanel_DeepLink；普通入口保留原奖励/普通面板。最高星与本次新增星由Dig控制器注入纯显示工厂，不读取/消费Draw档或新增星等待存档。普通通关进度仍在面板创建后保存，TBC仍不可通关。
- Replay保留DeepLink及同关提示/墨水资格；Home退出由Deactivate清来源；专属Next用当前有效序列首未通过项（全通到TBC），接受/降级后清来源，仍走原统计和遮挡后重建。普通Next按进入时真实DataId定位相邻项。
- 22项实际方法隔离断言通过；14个原生命周期/三玩法方法不变；Editor/Android宏0 error，iOS既有Apple引用2 error。生产UI/Spine/SDK/原生广告/双端横屏为uncertain，不把模拟作为实测；29个普通资源仍缺失。
- 代码、数据/UI/事件流、场景、证据边界和局部回滚见UI/GameVictory/GameVictoryPanel_DeepLink功能与风险记录.md的同日Dig扩展；证据在DevelopMD/验证记录/DigDeepLink胜利_2026-10-12/。本节补齐此前Dig/Draw对照中的专属面板缺口，不替换并行地图与烘焙约定。


## 2026-10-12 Android Back / Escape（当前扩展）

- 活动Dig Tick读取Escape并复用默认CommmonPopup退出应用确认，选关/普通/TBC/非转场暂停允许；确认Quit（Editor退出PlayMode），No/Close保持原状态。本次不增加暂停管理或菜单事件。
- 默认弹窗已开、Guide活动/排队、加载重建、结算或Runtime先锁结果、HUD/选关广告pending时不插队；收尾后新按键可再次打开。统计/提示Tick原主体及12个生命周期/DeepLink方法保持。
- 22项实际方法隔离断言在Editor与Player宏分别通过；Editor/Android宏0错误，iOS既有Apple2错误；真实UI/Android Back映射/退出/广告/横屏未测。组件、资源/保存/云控/SDK均复用未改。
- 完整来源/流/门禁/风险/回滚：DevelopMD/CommonPopup_Quit防插队功能记录.md的同日Dig扩展；证据：DevelopMD/验证记录/Dig返回键_2026-10-12/。回滚只撤Dig按键与局部方法，保留其它任务改动。

## 2026-10-13 Dig 提示对象适配（修改前核对）

- 需求与验收对象：用户已在 `DigLevel_001.prefab` 放入 `DigGame_TipsObjectSubObject.prefab`。购买提示后应同时显示该实例根节点下的图标、虚线路径等全部表现；未购买时隐藏；同关重建沿用既有会话资格，新关与退出仍清除资格。
- 代码与资源基线：`DigPlayCtrl.BuildLevelAsync` 已在 Runtime 初始化后调用 `ShowHint(_hintGranted)`，`TryGrantHint` 已负责即时显示；`DigLevelCtrl.ShowHint` 已按 `hintObjects` 整体切换 GameObject。当前缺口是 001 的 `hintObjects` 只有一个空引用，且 `HasHint` 只判断数组长度，空引用也会错误开放提示入口。
- 维护边界：新 Prefab 复用现有 `DrawTipsLineObjectRenderer` 与 `BreathLineAlphaHandle` 纯表现组件，不引入 Draw 关卡状态、存档或输入流程。本次不修改 Draw、金币价格、广告、Bag、结算、地图烘焙与提示美术布局；Prefab 内用户增加的 Canvas、排序和路径点保持原样。
- 拟修改位置：在 `DigLevelCtrl` 中让 `HasHint` 只认可有效绑定，并为提示数组补充中文 Inspector 说明；在 001 Prefab 显式绑定新提示实例的完整根对象。`DigPlayCtrl` 仅在有证据需要时补充幂等门禁，不改变现有提示生命周期。
- 验证范围：执行 Editor/Android 条件编译、Prefab 引用闭合与目标根对象绑定静态检查，并用隔离 Unity 验证有效/空引用判定和整组显隐。正式 UI 点击、金币/广告服务、生产 AssetBundle、Android/iOS 真机画面与触控仍为 `uncertain`。

### 实施结果与边界

- `DigLevelCtrl.hintObjects` 继续由关卡 Prefab 显式绑定，不增加运行时层级扫描。`HasHint` 改为至少存在一个有效对象才返回 true；`ShowHint` 只在运行态切换每个有效根对象，根节点下的图标、虚线和扩展表现同步显隐。
- `DigLevel_001` 绑定用户放入的 `DigGame_TipsObjectSubObject` 完整根对象。新提示 Prefab 原先带有指向自身 GUID 的残留嵌套元数据，Unity 实际报 `Missing Nested Prefab Asset`；已将路径子树规范为本 Prefab 的本地对象并移除无效实例块，未改变层级、路径点、Sprite、材质或表现参数。
- `DigPlayCtrl.TryGrantHint` 增加会话资格幂等门禁，成功后关闭既有提示警示。正常入口仍由 Popup 在返回 true 后扣币；重复调用返回 false，不会形成第二次扣币资格。同关重建、新关/退出清理规则保持原实现。
- 验证：Editor 与 Android 条件编译均 0 error；隔离 Unity 6000.0.59f2 真实导入、加载和实例化通过，确认 Regular Prefab、两个根子节点、提示渲染/呼吸组件、001 单一根绑定及实例 `HasHint=true`。正式 UI 点击、购买/广告回调、AssetBundle 和移动设备表现仍为 `uncertain`。证据见 `DevelopMD/验证记录/Dig提示对象_2026-10-13/验证记录.md`。
- 回滚：撤 `DigLevelCtrl` 的有效引用判定/Inspector 说明、`DigPlayCtrl` 幂等与警示关闭、001 的根对象绑定，并恢复提示 Prefab 修改前资源。不得整文件覆盖 001，因为同文件包含用户地图、美术和并行任务修改。

## 2026-10-14 SuccessTarget 成功彩纸（修改前核对）

- 需求与验收：`DigGame_SuccessTarget` 的目标回调先点亮 `Light_effect`，随后让用户已放入的 `ConfettiBlastRainbow` 播放一次；关卡开始不播放，重复拒绝到达不播放，目标失活／重建后不残留旧粒子。
- 当前事实：`DigTargetTrigger.onReached` 已按顺序绑定 `Light_effect.SetActive(true)`、`DigPumpkinSuccessEffect.PlaySuccess()`。新加入的 `ConfettiBlastRainbow` 是 Epic Toon FX 嵌套 Prefab，根和两个子粒子系统均已覆盖 `playOnAwake=0`，根粒子不循环；目前没有播放入口。`DigPumpkinSuccessEffect` 位于目标根并已由亮灯后的第二个回调调用，适合承载同一局部成功表现。
- 拟修改：为 `DigPumpkinSuccessEffect` 增加显式序列化 `ParticleSystem` 引用；`PlaySuccess` 先清除旧粒子再以 `withChildren=true` 播放，`OnDisable` 停止并清除。只在 `DigGame_SuccessTarget.prefab` 绑定用户放入的根 ParticleSystem，不扫描层级、不改共享目标判定、回调顺序、胜利延迟、地图或第三方 Epic Toon FX 源资源。
- 影响与验证：基础目标的02／03变体和引用该基础Prefab的关卡继承彩纸；002内联目标未绑定彩纸，继续只亮灯和抖动。验证 Prefab 原生导入／引用闭合、初始不播放、首次成功亮灯后根及子粒子播放、禁用清除、新实例不残留，以及 Editor／Android条件编译。正式关卡美术位置、移动端粒子性能与完整胜利UI仍为 `uncertain`。

### 实施结果、风险与回滚

- `DigPumpkinSuccessEffect` 新增显式绑定的 `ParticleSystem confetti`。既有 `PlaySuccess` 回调先清除上一次粒子，再调用 `Play(true)`，因此根粒子与两个子粒子由同一成功回调播放；目标禁用时用 `StopEmittingAndClear` 清理。未新增运行时层级扫描、全局事件或胜负状态。
- `DigGame_SuccessTarget.prefab` 将 `ConfettiBlastRainbow` 根粒子绑定到上述字段；`DigTargetTrigger.onReached` 的持久化调用顺序仍为 `Light_effect.SetActive(true)`、`DigPumpkinSuccessEffect.PlaySuccess()`，三个粒子的 `Play On Awake` 均保持关闭。被拒绝或重复的到达不会进入 `onReached`，本次未修改该判定链。
- 影响范围：引用共享 SuccessTarget 的关卡与其02／03变体继承该表现；未绑定 `confetti` 的旧目标保持原有灯光和南瓜动画，空引用不会报错。移动端风险主要是成功瞬间的粒子过绘；该特效为非循环单次播放，未改粒子源资源和参数。
- 验证：Unity 6000.0.59f2 Roslyn 的 Editor、Android 条件编译均为0错误；静态检查确认彩纸字段和根粒子引用唯一、第三方Prefab GUID可解析、三个 `playOnAwake=0` 覆盖存在，且亮灯调用排在 `PlaySuccess` 前。尚未在正式关卡 PlayMode 或移动真机观察彩纸位置、遮挡和瞬时性能，这些项目为 `uncertain`。
- 回滚：仅移除 `DigPumpkinSuccessEffect` 的彩纸字段及播放／清理调用，并删除 `DigGame_SuccessTarget.prefab` 中该字段绑定；保留用户已放入的 `ConfettiBlastRainbow`、其位置缩放、粒子参数和其它 Prefab 修改。

## 2026-10-14 DigLevel_007 激光眼（修改前核对）

- 需求与验收：新增 `DigGame_LaserEye`，以 `Sp_wd_jg_01` 的 `SpriteRenderer.size.x` 从左侧 Pivot 向本地 X 正方向扩张；首次碰到 Inspector 指定的阻挡 Collider 时截断并停止继续增长，在命中点播放一次粒子。激光范围内命中任一本关未到达 Actor 时立即播放其死亡表现、锁定失败，并从命中开始等待默认1秒后进入既有失败重建链；扩张速度、起始／最大宽度、阻挡对象、命中特效和失败等待均暴露。
- 当前资源事实：用户新建且未提交的 `DigLevel_007.prefab` 已放置 `DigGame_Eyes/Sp_wd_yq_01/Sp_wd_jg_01`。激光 Sprite GUID 为 `177ad1e7b922ccd45a1ac8fa5308b167`，Pivot 为 `(0,0.5)`，Renderer 使用 Tiled 模式；同节点已有 BoxCollider2D。右侧 `Boundary (1)` Collider 为 `7286803771269464819`，本关仅绑定一个 Actor，胜利条件为 ReachTarget。
- 原流程与维护边界：`DigLevelCtrl.TryFail` 当前立即锁结果并播放 Actor 固定0.5秒死亡，完成即 `Resolved(Failed)`；`DigPlayCtrl` 随后按现行统计、失败插屏、黑幕和重建顺序处理。激光必须复用该唯一失败入口，不能直接播放死亡后另发重置事件。现有墨水、超时和陷阱仍保持死亡完成即提交；只为明确传入延迟的激光请求增加“命中至提交”的最短游戏时间，不改变打点顺序、广告策略或其它关卡。
- 拟修改：新增局部脚本，使用固定容量 `RaycastHit2D` 缓冲区做无GC的 FixedUpdate 射线检测；Actor 由 `attachedRigidbody.TryGetComponent<DigActorCtrl>` 识别，阻挡物由显式 Collider 列表／可选层选择，忽略激光眼自身子树 Collider。视觉与可选 BoxCollider2D 同步宽度，粒子实例显式绑定。`DigLevelCtrl` 增加带失败等待参数的重载和暂停感知倒计时，旧 `TryFail(actor)` 继续以0秒额外等待调用。
- 影响与验证：仅在007的 `DigGame_Eyes` 显式挂载并绑定关卡、激光 Renderer／Collider、右墙和本地 `Knock_Explosion` 粒子实例；不扫描名字、不修改地图烘焙、胜利目标、Actor资源、Packages／GuruSDK／GF。验证扩张／截断、旋转缩放、阻挡／Actor先后、一次粒子、暂停／后台、重复失败、失活重建、旧失败0.5秒不变，以及 Editor／Android条件编译。正式美术遮挡、双端真机性能和完整广告重建仍为 `uncertain`。

### 最终实现与使用

- `Sp_wd_jg_01` 是激光 Sprite 资源，不是运行时查找键。制作人已将眼睛提取为 `DigGame_Eyes.prefab`，007嵌套该源并显式绑定所属关卡；源绑定激光Renderer、Transform和BoxCollider2D。Renderer保持Tiled，脚本修改 `size.x`，Trigger同步宽度与左Pivot偏移。
- 用户追加要求后改为仅按“阻挡 Layer”判断，保留 `stoppingLayers` 序列化名，默认Ground。007三个边界使用Ground、沙土继续Default；新增阻挡物设置其Collider节点Layer即可。精确Collider数组及007旧单墙override已移除，Actor无需加入掩码。
- 命中特效绑定源内 `SparkExplosion2D` 根粒子，根／Glow关闭PlayOnAwake，保留用户循环及80／5每秒发射等参数。每步按hit.point更新XY，Z保持原值，同Collider不重播；失去命中停止发射、重启／失活清空。命中偏移当前为0，精确落在接触点。
- 初始宽度0.1、最大宽度20、扩张速度16、失败等待1秒均可调。失败等待从命中时开始，与约0.5秒死亡动画并行；旧失败入口保持原时长。最新原生Prefab／Physics2D／DOTween／ParticleSystem共25项通过，Editor／Android条件编译0错误；正式007视觉、HUD／广告链和真机性能仍uncertain。本关结构与回滚见 `Level7/DigLevel_007激光眼功能与风险记录.md`，证据见 `DevelopMD/验证记录/Dig激光眼_2026-10-14/验证记录.md`。

### Ground命中后火花停止：当前修复

用户现场指出触Ground后未持续播放，沿实际眼睛坐标复现首次截断宽度16.6894341后下一步漏检：检测射线恰在接触面结束，浮点舍入使Ground落在终点之外，随后StopEmitting。41组坐标／缩放／旋转中7组复现；Spark自身Loop=true／Rate80、Glow Loop=true／Rate5，不能把此现象归为未设置循环。

仅改脚本：已截断时给检测末端加0.001世界单位容差，显示宽度不增加；同Collider仍持续更新位置，发射正常不重播，已停止时Play(true)恢复而不清空。目标真正离开或Collider销毁时停止，Restart清理。修后41样本无丢失，实际Spark约2.21秒跨多个Duration连续发射等22项通过，原25项回归通过；Editor／Android条件编译0错误。用户007 MaxWidth22、源Prefab与粒子参数保留。实际Game窗口／真机视觉仍uncertain；详细证据和局部回滚见本关记录及原验证记录持续修复章节。

### 最新：每帧持续检测，阻挡物移开后恢复伸长

用户指出首次阻断后短线不再变长。原_growthStopped永久限制增长和探测到首次距离，是独立于端点舍入的设计问题。现已删除锁定与临时容差，改LateUpdate每帧按最大范围重新查询最近有效Collider，显示宽度按速度增长并裁剪到当前命中点；前端实际接触才播火花，目标远离后重新长、靠近立即短，未接触不发射。Actor仍复用TryFail，结果锁由关卡持有。Awake新实例初始化、OnDisable清理，移除公开Restart／OnEnable重置。

实际源眼睛／Ground右移2米对照：旧宽度16.6894341不变，新恢复18.689436。Editor／Android条件编译0错误；按用户加速要求只做此直接对照，没有把旧25项／41样本当作新LateUpdate版本的完整验证。当前实际Game窗口、全部旋转路线／Actor／暂停／广告与真机为uncertain。源码／资源局部边界与回滚见本关最新章节，证据见原激光验证记录。

## 2026-10-15 保存 Prefab 后同关 Reset 刷新（修改前核对）

- 需求与基线：用户明确反馈的是编辑器修改并保存关卡 Prefab 后，Reset 仍显示旧内容。验收对象为同一关卡资源的重新读取；保留关号／排序、Reset 打点、墨水广告及提示资格、黑幕重建和插屏顺序。
- 来源与原流程：`DigPlayCtrl.OnReset → BeginRebuild → RebuildAsync → BuildLevelAsync` 已销毁旧实例；`BuildLevelAsync` 只在路径变化时释放资源。同关 `ResourceManager.LoadAsync` 优先返回缓存对象，`YooAssetLoader` 另持有资源 Handle；`Release(path)` 会依次清除两层缓存并尝试卸载无引用 Provider。YooAsset 的 `DatabaseAssetProvider` 在编辑器模拟加载时通过 `AssetDatabase.LoadAssetAtPath` 读源资源。现场日志已进入 DigLevel_002 的 Reset／level_start／失败插屏判断，不能归因为按钮未响应。
- 拟修改与维护边界：仅在 `DigPlayCtrl.BuildLevelAsync` 的同路径重建、`UNITY_EDITOR` 且已初始化的 `HotReloadCtrl.PlayMode == EditorSimulateMode` 时调用既有 `Release(path)`，再按原 API 加载。沿用已遮黑且旧实例失活的时机；不修改共享加载器、GF／Packages／SDK、Prefab 或烘焙数据，不绕过资源系统，不改打包资源模式。
- 验证与未决项：核对清理／重载顺序、编辑器模式门禁、首次进入／切关／同关 Reset／Replay 的路径差异，并做 Editor／Android 条件编译。缓存命中是源码事实，是否完全解释用户现场旧内容仍为 `uncertain`；最终需以当前资源路径对应的 Prefab 保存后重建为准。修改地图几何仍需原有 InitMap 烘焙，Reset 不替代制作工具。CodeGraph CLI 当前不可用，本次依据现行源码和日志核对。
- 回滚：仅撤销 DigPlayCtrl 新增的编辑器模拟同关释放分支，恢复原缓存复用；保留其它任务与用户资源修改。

### 实施与验证结果

- 已在 `DigPlayCtrl.cs:266` 增加局部条件分支：同路径、Editor 编译、热更新控制器已存在且为 EditorSimulateMode 时释放该路径；随后仍调用原 `LoadAsync<GameObject>`。没有新增全局缓存清理、主动 AssetDatabase 导入或运行时层级查找。
- 静态场景核对：首次进入的空路径不额外释放；切关仍只释放旧路径；同关手动 Reset、失败重建和 Replay 共用 BuildLevelAsync，因此均应用该刷新；编辑器离线／联机资源模式不执行该分支；Android 编译中整个分支被移除。旧实例先失活／销毁，HUD 黑幕已建立，广告请求仍位于新关初始化之后。资格字段、统计和配置身份未修改。
- 编译：Unity 6000.0.59f2 Roslyn 编译当前1225个生产源文件，Editor 0错误／113警告，Android 条件编译0错误／118警告；日志为 `C:/Users/admin/AppData/Local/Temp/brainpunk-dig-editor-reset-20261015/compile-editor.log` 与同目录 `compile-android.log`。定向 `git diff --check` 通过。本次没有执行完整Unity构建或手机验证。
- 现场验收仍 `uncertain`：等待脚本编译完成后重新进入一次 Play，修改并保存实际加载的 DigLevel Prefab 中可辨识的对象位置，再点 Reset；确认新实例显示新位置，并再次 Reset 确认恢复同一已保存值。当前没有操控用户正在运行的正式关卡，不把静态分支核对或编译通过当作现场复现通过。

## 2026-10-16 Level6 接触 Germ 不吞噬（修改前核对）

- 需求与基线：用户确认 Level6 中 Actor 接触 Germ 后 Germ 不消失。当前 DigLevel_006 为 ReachTarget，场内有3个有效 DigGermCtrl，但关卡的 germs 数组为空；截图的调试区也仅列 Actor。保留到达目标胜利、大小判定、批次去重、暂停、eat/hiccup 时序和用户地图、美术、物理设置。
- 来源与因果链：DigLevelCtrl.InitializeRuntime 只初始化绑定的 germs；QueueContact 对不在该数组中的对象直接返回，因此不会进入 FixedUpdate 的 TryClaim → Actor.Grow/PlayEat → Germ.PlayConsume → 失活链。CollectLevelObjects 目前仅在 ConsumeAllGerms 模式收集 germs，ReachTarget 分支仅绑定目标，导致该模式新增 Germ 无法由按钮登记。TrySucceed 明确限定 ConsumeAllGerms，ReachTarget 吞噬全部 Germ 不会自行胜利。
- 拟修改与维护边界：只将当前关卡子层级 Germ 收集提到两模式公共段，保留 inactive、嵌套关卡排除、Undo 和保存机制；同步更正已不符合新行为的收集方法注释。在006根 DigLevelCtrl 显式绑定已存在的3个组件，不修改 Germ/Actor 基础资源、不加运行时扫描、不批改其它关卡。该手写脚本与 Prefab 不是生成器产物。
- 验证与未决项：检查006引用闭合和仅 germs 数组差异；隔离Unity验证旧空数组拒绝、新收集后真实物理接触触发认领/eat/消失、重复接触、暂停、ReachTarget不提前胜利，以及收集的 inactive/嵌套关卡边界；执行Editor/Android条件编译。正式006完整场景与真机表现仍需实测，标记 uncertain。回滚仅撤公共收集调整及006的3个引用，保留用户其它修改。

## 2026-10-16 全部沙土 CollisionTiles 统一物理材质

### 修改前核对

- 需求与行为基线：用户明确要求所有关卡的 CollisionTiles 绑定 Assets/Game/ArtSrc/Physics/DigGame_Sand.physicsMaterial2D。Assets 下全部 Prefab／Scene 字面节点扫描命中 DigLevel_001～007、012 共8个关卡；各节点都有 Rigidbody2D、CompositeCollider2D、TilemapCollider2D，原引用混用空值、DrawStrokeFree、DrawStrokeStable。DigLevel_999 没有该节点，也未嵌套这些地形关卡，不新增沙土地形。
- 来源与维护边界：材质 meta 的 GUID 为 e3b08626395dfc74381be1f476a9c2a8，主对象 fileID=6200000；当前材质 friction=0.1、bounciness=0、m_FrictionCombine=3。DigTerrainCtrl.BindRuntime／RestoreBakedCollisionCells 恢复格图，InitMap 更新格图与挤出，不写入物理材质。Level4 既有滑动记录证明需要核对实际 Composite 的独立材质，不能只换刚体引用。CodeGraph CLI／MCP 不可用，知识库工作流搜索无匹配，依据当前源码和资源核对；引用的037计划文件当前不存在。
- 拟修改及影响：只对上述8个节点的3个物理组件显式设置同一材质，共24处引用；不改材质资源数值、碰撞形状、图层、刚体参数、地图烘焙数据、脚本和其它物体材质。用户的006／007 Prefab、地图、功能文档、字体、平台设置等已有改动保留。材质资源与meta为用户已放入但尚未追踪的文件，本次不覆盖。
- 验证计划：修改前在 TEMP brainpunk-sand-material-20261016-8f12a9 保存8个Prefab的逐字快照及材质／999哈希；修改后逐组件检查24处GUID/fileID，除目标m_Material行外逐字一致，核对全局命中数量、引用与空白差异。无C#修改，不运行全量编译；Unity导入、手机滑动手感和完整通关未覆盖，标记 uncertain。
- 风险与回滚：这是有意统一沙土接触材质，可能改变各关Actor／木头的滑动和停稳位置，接触另一侧材质仍会影响合成结果。无新增运行时代码、循环或实例分配；Android/iOS实际物理与性能待关卡验收。回滚仅按快照恢复这24处引用，不能整文件覆盖用户并行修改；不修改只读Packages／GuruSDK／GF。
### 实施结果、验证与剩余风险

- `DigLevelCtrl.CollectLevelObjects` 现对两种胜利模式共同收集本关 Germ；ReachTarget 仍额外绑定本关目标，目标 index、提示收集和运行时判定未改。原“到达模式保留病菌引用”注释已因行为变化更新为共同收集。
- `DigLevel_006.prefab` 的 germs 显式绑定现有三个 DigGermCtrl：6236643850090970574、6694797843449912858、3436573142678182240。与修改前快照逐字比对，仅该数组由空变为3项；保留用户当前地图、物理材质、Actor半径、对象位置及 ReachTarget 条件。旧运行实例不会自动重新初始化，脚本编译后退出当前Play并重新进入006加载绑定。
- 隔离Unity 6000.0.59f2的14项运行检查通过：旧空数组下真实物理接触复现不吞噬；新收集后实际Physics2D回调进入批次、认领、10→15成长、真实Spine单次eat、重复接触去重、DOTween约0.3秒吸入后GameObject失活、暂停拒绝、eat回idle；ReachTarget吃完不提前结束且到达匹配目标成功；ConsumeAllGerms仍等待hiccup后成功。编辑态检查通过：两模式收集active/inactive本关Germ、排除嵌套关卡；仅绑定本关目标且保留index/嵌套目标归属。
- Editor/Android各1226源文件条件编译0错误（113/118既有警告）。本次数组增量及代码diff检查通过；整体006对HEAD的diff仍含用户既有Unity YAML空值尾空格，未清理无关内容。
- 证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-germ-006-20261016/` 中checks.txt、collector-checks.txt、prefab-checks.txt、unity.log、compile-editor.log和compile-android.log；隔离场景使用真实Physics2D/Spine/DOTween，地形为测试替身，没有运行正式006整张地图。正式关卡的全部空间接触、资源包与手机表现仍为uncertain，不能将隔离通过等同整关验收。
- 回滚与风险：仅回退共同收集的3行逻辑／对应注释及006新增数组引用；原运行逻辑不变。之后在其它ReachTarget关卡显式点击收集，会将其子层级Germ纳入吞噬与暂停管理，这是本次按钮的预期改变；没有批量修改其它关卡。

### 实施与验证结果

- 已完成001、002、003、004、005、006、007、012共8个Prefab、24处引用替换；CollisionTiles上的Rigidbody2D／CompositeCollider2D／TilemapCollider2D均显式绑定目标GUID与fileID。Assets全局再次扫描没有遗漏同名节点；9个Dig关卡Prefab也没有被其它Prefab／Scene通过m_SourcePrefab引用的实例覆盖。999保持原状。
- 静态验证：逐组件核对3/3绑定；将每份Prefab的目标材质行恢复为快照旧值后，全文与修改前快照一致。材质资源／meta／999的SHA-256保持一致，未改材质参数或地图数据。收尾再次确认24处引用存在。本任务未改C#脚本，未运行编译或新增测试脚本。
- 差异检查边界：直接git diff --check发现006／007原有并行修改里的空字段尾空格；012原文件存在CRCRLF，保留原换行导致no-index字节视图将改动行的额外CR记为尾空白。没有清理这些用户内容。只在TEMP将修改前后副本统一换行为LF后，8份任务差异均无空白错误；生产文件仍保留原换行。baseline.json和changes.json留在上述TEMP目录，可逐条恢复引用。
- 未验证风险：没有启动Unity、执行InitMap或进行PlayMode／Android／iOS整关验证；低摩擦下物体滑落、停稳与通关体验仍为uncertain。该修改只绑定现有物理材质，不需要重新烘焙地图；运行实例需要重新加载Prefab才读取新绑定。未执行Git提交或推送。
## 2026-10-16 Germ 初始化保留制作配置（修改前核对）

- 用户要求：Germ 初始化也不得改 scale 和 Collider。当前 DigGermCtrl.Initialize 会按 currentSize/unitRadius 覆盖 Body.localScale、Circle.radius，并强制 isTrigger=false；根缩放初始化未改，但 PlayConsume 从 Vector3.one 缩小，会使非单位根缩放在吸入第一帧跳变。
- 来源与边界：DigLevelCtrl.InitializeRuntime → DigGermCtrl.Initialize；碰撞经 Actor.ReportContact → QueueContact → TryClaim/Grow/PlayConsume。当前手写脚本及Germ源Prefab管理这些字段，无生成器或模板输出；BaseDigComp 的 HoleShape/InitMap 是另一路编辑器烘焙配置，本次不改。仅改 DigGermCtrl，所有使用者初始化均改为保留配置，数值吞噬判定仍读 currentSize/growthSize。
- 拟修改：删除初始化对Body缩放、Circle半径和isTrigger的赋值，移除失去用途的unitRadius字段及校验；保留显式Body/Collider/Rigidbody绑定校验与初始暂停。PlayConsume从调用时根localScale乘衰减，保留原0.3秒吸入和结束失活。仅按新行为更新直接相关旧注释与Tooltip，不清理Prefab遗留序列化键或用户资源。吞噬认领仍关闭Collider，暂停仍控制刚体模拟，不把“不改初始化”扩大成取消这些既有流程。
- 验证计划与风险：原生Unity检查Circle/子Polygon、非均匀与负缩放、半径/Offset/形状/材质/enabled/isTrigger保留，确认吸入从当前缩放开始、暂停与结束失活；回归上一轮真实接触eat/消失及两种胜利条件，Editor/Android编译。Collider保留为Trigger时仍由现行Collision入口决定能否吞噬，不新增Trigger入口或强制修配置。正式全关卡与手机仍uncertain。回滚仅恢复本脚本删去的初始化赋值/字段及旧吸入缩放表达式，不改用户Prefab。

### Germ 保留配置：实施与验证结果

- Initialize已不写Transform缩放或Collider字段，删除旧unitRadius字段及其数值校验，保留绑定校验、记录初速度和暂停模拟；旧Prefab中未清理的unitRadius序列化键不再被代码消费。Body/Collider的直接相关注释与Tooltip随实际行为更新，未修改其它注释或任何Prefab。
- PlayConsume用调用时根localScale作为缩小起点，非均匀／负缩放不跳变；吞噬关闭Collider、0.3秒吸入和失活回调、暂停／恢复逻辑维持原入口。CurrentSize与GrowthSize继续只负责吞噬数值，不用于重算Germ显示尺寸。
- 原生Unity6000.0.59f2隔离46项通过：Circle与子Polygon、正／负非均匀缩放、Collider完整序列化快照（半径／路径／Offset／材质／enabled／isTrigger）初始化前后一致；初始暂停和恢复速度、单次认领、吸入相对缩放与完成回调通过；上一轮真实Physics2D接触→Spine eat→DOTween消失、暂停去重及ReachTarget/ConsumeAllGerms胜利区别14项回归通过。最初测试在动态添加Collider之前设置速度，恢复硬编码速度断言失败；测试修正为在组件组装完后设置并比较初始化前实际速度，生产速度逻辑未改，首次失败保留于fixture-first-run.txt。
- Editor/Android各1226源文件条件编译0错误（113/118既有警告），代码diff检查通过；生产Germ与验证副本一致。当前6个Germ源Prefab的isTrigger均为0；本次不代替Prefab设置触发器或尺寸。正式关卡整体观感／碰撞路线和Android/iOS真机仍uncertain：新配置会直接决定显示和物理接触范围，应以制作人保存的值验收。
- 证据目录：C:/Users/admin/AppData/Local/Temp/brainpunk-dig-germ-preserve-20261016，checks.txt为最终PASS46，unity-recheck.log为最终原生日志，compile-editor.log／compile-android.log为编译记录。回滚只恢复DigGermCtrl本次初始化和吸入缩放改动；不覆盖上一轮006绑定、收集按钮或并行资源修改。

## 2026-10-16 Actor 成长改为根节点整体缩放（修改前核对）

- 用户明确要求：Actor变大时不写Collider数据，改根节点整体scale，并确保缩放中心不移动。当前Grow用Circle.radius作为插值起点，ApplyScale分别写Body.localScale和Circle.radius；基础Actor根为脚底原点，Circle.offset=(0,0.5)，直接乘根scale会把碰撞中心往上推。验收中心采用现有显式Circle的世界中心，不把根Transform原点误当角色中心。
- 来源与边界：DigLevelCtrl.FixedUpdate → Actor.Grow → 0.22秒OutQuad → ApplyScale；Animations的eat只读成长入口。Initialize只记录初值，不修改配置。手写partial无生成器；本次不新增序列化字段、不改Prefab、Germ、地图和统计。保留sqrt(当前数值/初始数值)倍率、连续成长接续、暂停、死亡取消和eat去重。
- 拟修改：缓存初始根localScale与当前倍率，ApplyScale只按初始根scale乘倍率；每次回调先读取当前世界碰撞中心，缩放后用Rigidbody2D.position补偿因缩放产生的偏移，不缓存整段动画的固定世界坐标，保留自然运动。Collider.radius/offset和Body.localScale不写。死亡离屏终点原来只计半径，整体成长会同时放大中心Offset，因此终点同时考虑世界中心相对根的Y偏移，保持既有离屏要求与死亡时长。
- 验证：真实Unity/DOTween下检查默认与非均匀/翻转根scale、Circle半径/Offset/Body原值、每步世界中心、旋转/带父级、连续成长、移动中成长、暂停恢复、死亡排序与真正离屏、新实例恢复；回归真实Germ接触eat/消失/胜利。Editor/Android条件编译。正式关卡复杂接触挤出和真机仍uncertain，物理引擎因体积变大而发生接触纠正不属于缩放自身的中心漂移。
- 回滚：仅恢复Actor的初始记录、Grow/ApplyScale及死亡终点表达式，不覆盖用户Prefab或其它任务。Level6 Germ“慢慢移动”上一轮只完成参数读取，尚未复现定因，本次Actor成长修改不能作为该问题已解决的证据。

### Actor整体成长：补充核对与方案修正

原生测试发现使用Rigidbody2D.position补偿时，带旋转及非均匀父级的世界中心断言失败，不能宣称该实现满足需求。隔离对照改为在同一Transform空间修改根scale并按世界偏移补偿transform.position后，83项检查通过（含自然物理移动/旋转、速度保持、连续成长和死亡离屏）。因此正式实现采用Transform世界位置补偿，不直接回写刚体位置；继续核对SyncTransforms后的真实Collider.bounds中心。失败证据保存在TEMP目录brainpunk-dig-actor-root-scale-20261016/rigidbody-position-checks.txt。

### Actor整体成长：最终实现与验证

- DigActorCtrl.Initialize记录初始根localScale和数值基准；Grow从当前倍率接续0.22秒OutQuad，ApplyScale只改根scale并用Transform世界位置补偿缩放偏移。Body局部scale、Circle半径和Offset均不写；正常物理运动不会被旧世界坐标锁住。基准中心是显式绑定Circle的世界中心；基础资源Offset=(0,0.5)保持原值。
- 死亡离屏终点额外计入放大后Circle中心相对根的Y偏移，确保整个圆形碰撞体越过相机底边；保持原上跳／下落时长、SceneLayer200、关闭物理和唯一结果回调。只更新了与新行为冲突的Actor字段／方法注释，Prefab和动画partial未改。
- 最终原生Unity6000.0.59f2隔离89项通过：初始数值10/40，单位／非均匀／负根scale，旋转与非均匀父级，中途／连续成长，SyncTransforms后的真实Collider.bounds中心、Collider完整序列化参数和Body局部scale保留，自然运动／旋转与速度保留、暂停恢复、新实例、两种死亡路径排序／完整离屏／时长。真实Physics2D接触、Spine eat、Germ吸入与两种胜利条件14项回归包含在89项内。
- Editor与Android各1226源文件条件编译0错误（113/118既有警告），代码diff检查通过，生产Actor与最终测试副本字节一致。证据位于C:/Users/admin/AppData/Local/Temp/brainpunk-dig-actor-root-scale-20261016/checks.txt、final-unity.log、compile-editor.log、compile-android.log；先前失败及对照日志保留。
- 未验证风险：正式关卡地形接触／挤压、Spine画面和Android/iOS真机仍uncertain。以上证据证明缩放自身不偏移中心；变大后物理接触可能正常推开角色，不能承诺与地形重叠时角色绝对静止。没有修复或确认上一轮Germ慢移动原因。回滚仅撤本次Actor根缩放／位置补偿及死亡终点，保留先前初始化半径、死亡排序和Germ改动。

## 2026-10-16 每个Actor一次只吞一个Germ（修改前核对）

- 用户要求一次只触发吃一个Germ。当前DigLevelCtrl.FixedUpdate在同一Actor的Batch中认领全部Germ、合计成长并并行PlayConsume；Actor.PlayEat只合并动作，不能限制实际吞噬数量。现有Germ吸入0.3秒、真实eat0.6秒，验收为每Actor同时最多一只被吸入，本次吸入与eat均完成后才接受下一只。
- 保留规则：同一物理批次先检查全部接触，任何更大Germ仍优先失败，不通过逐只吃小的规避；每个Actor独立节奏，不做全局单Actor锁。只接受仍实际接触的下一只，不把已分离对象远程吸走；重复认领、暂停／后台、死亡退出、ReachTarget和成长中心规则保持。
- 来源与维护：DigLevelCtrl.ActorState/QueueContact/FixedUpdate/OnConsumed/TrySucceed/ClearContacts，DigActorCtrl.Animations的Eat状态与真实Spine完成回调，DigGermCtrl.PlayConsume完成失活。均为手写脚本，无生成器或新序列化资源；不改Prefab、DB、配置、SDK、UI或统计。
- 拟修改：ActorState保存当前单只吞噬占用及吸入完成标志；FixedUpdate保留批次失败检查，但空闲Actor只认领一只。新增Actor局部只读IsEating供关卡查询，吸入回调仅登记完成，等待eat退出后在FixedUpdate释放占用并递减待完成计数；自然全部吞噬胜利在最后一次eat完成后才进入hiccup。忙时不再开始其它吸入，后续真实接触回调重新提交；结果结束统一清理占用，generation仍拒绝晚到回调。
- 验证：真实Spine/Physics2D/DOTween下同刻三只、0.3秒吸入完成但eat未完、下一只再触发、持续接触及睡眠／慢速eat、多Actor共享Germ去重、同批大小混合失败、暂停／后台、终止晚回调、无Spine和最后eat→hiccup顺序；Editor/Android编译。正式Level6手感及手机仍uncertain。回滚仅撤本次吞噬门禁和动画只读属性，不回退既有收集、绑定、整体scale等改动。

### 单只吞噬：睡眠边界补充核对

首轮原生测试中，慢速eat持续接触场景使Actor及未吃Germ进入睡眠，前一只结束后不再接收后续接触，逐只吞噬断言失败。已有OnCollisionStay方案不足以覆盖这一边界。拟在单次吞噬占用释放且还有未认领Germ时，通过Actor局部API仅WakeUp当前存活、非冻结刚体，等待真实下一物理步接触再认领；不保留过期接触、不直接Queue远处Germ、不改变速度或Collider。失败记录保留于TEMP的brainpunk-dig-single-consume-20261016/sleep-first-run.txt，修后重新跑该场景。

### 单只吞噬：最终实现与验证

- 每个ActorState最多一个Consuming占用，FixedUpdate先沿用整批大小失败判定，再对空闲Actor仅认领第一只可认领Germ并单次Grow。忙时不合计其它Germ的成长，不并行吸入；其它Actor各自可吞一只，同一Germ仍由TryClaim去重。
- Germ完成吸入后只标记ConsumeCompleted；待Actor.IsEating=false才释放占用和待完成计数，真实eat未完／吸入未完都不能开始下一只。最后一次eat也完成后才自然进入hiccup与原胜利延迟。暂停／后台冻结推进，结果锁定／退出清理占用和计数，generation拒绝晚到吸入回调；ReachTarget仍由目标完成。
- 为覆盖已复现的睡眠断点，释放占用后且仍有未认领Germ时调用Actor.WakeForNextContact，只唤醒存活且未冻结的刚体，由真实后续接触重新提交；不缓存过期碰撞，不远程吸入已分离对象，也不写速度／Collider参数。新增IsEating和WakeForNextContact均是局部internal接口，已加中文边界注释；无全局事件或Prefab修改。
- 原生Unity6000.0.59f2隔离25项通过：三只同时接触分三次认领与成长、重复接触去重、eat/吸入各自先结束的双向等待、最后eat→hiccup→唯一成功、菜单／后台暂停恢复、ReachTarget不误胜利、无Spine、多Actor共享Germ去重、大Germ同批／忙时失败、退出／失败晚回调、已分离不吸入、慢速eat与真实物理睡眠后继续CollisionStay、重建恢复空闲。睡眠失败与修正均有日志，不将旧并行吞噬测试当作新规则验证。
- Editor/Android各1226源文件条件编译0错误（113/118既有警告），三个生产脚本与最终验证副本一致，代码diff检查通过。证据：C:/Users/admin/AppData/Local/Temp/brainpunk-dig-single-consume-20261016/checks.txt（PASS25）、final-unity.log、compile-editor.log、compile-android.log，首轮睡眠失败留于sleep-first-run.txt。
- 正式Level6连续吞噬观感、复杂地形接触与Android/iOS真机仍uncertain；本次测试覆盖真实物理／Spine／Tween，但地形是隔离替身，未操控正在运行的正式关卡。当前已运行实例需重新进入加载新逻辑。回滚仅撤单只占用、完成等待、IsEating/WakeForNextContact及相关计数清理；保留前次Germ配置、Actor整体缩放、关卡收集与用户资源修改。没有Git提交。


## 2026-10-16 DigGame_Bomb 与圆形爆炸范围

新增 `DigGame_Bomb`，现有 Bomb Prefab 与 Level008 实例已显式绑定。Actor／Bomb 接触引爆，播放 ExplosionRoundFire 和 rocketExplosion；世界半径默认 1.5、持续 1 秒，Inspector 可调，选中对象显示同源 Gizmos 圆。瞬间及持续期检测 Actor／同关 Bomb，支持晚进入与连锁，重复触发只播放一次。Actor 爆炸死亡使用局部 TryFailFromExplosion：立即烧焦，只按相对 X 选择左上／右上，参考 Level50 的 (±0.5,0.7) 上抛和旋转；完整离屏后进入原唯一结果回调。Bomb 可调力度、下坠加速度、旋转速度及烧焦颜色。暂停／后台冻结范围、粒子和飞出，关卡重建恢复；普通失败仍走原 PlayDeath。其它触发对象待用户后续补充。

原生隔离 Unity 40 项通过，Editor/Android 1227 源文件条件编译 0 错误；正式 Level008 渲染／听感及真机仍 uncertain。使用步骤、完整绑定、影响边界、证据和回滚见 [DigGame_Bomb功能与风险记录](DigGame_Bomb功能与风险记录.md)。

本轮追加的烧焦左右飞出专项 56 项及原 Bomb 40 项回归通过，Editor/Android 各 1228 源文件条件编译 0 错误。默认力度 50、加速度 20、旋转 720 度/秒；同 X 选择右上，其余仅按左右判断。当前用法与验证边界见上述 Bomb 专项记录。

## 2026-10-16 ReachTarget 隐藏吞噬调试 GUI

- 修改前核对：用户要求 ReachTarget 不显示截图中的 DIG TO EAT 面板。DigLevelCtrl.OnGUI 当前处于 UNITY_EDITOR 内，仅按播放、初始化、结算与 Repaint 门禁绘制，尚未区分胜利条件；其数值来自已绑定 Actor／Germ。
- 拟改位置与边界：只在 OnGUI 入口增加 ConsumeAllGerms 条件，保留吞噬模式原显示和全部既有门禁；不改通关判断、Prefab、角色数值、数据或事件流程。
- 验证计划：静态核对 ReachTarget 提前返回、ConsumeAllGerms 继续原路径以及 Editor 编译边界，检查最小差异；Unity 实际画面尚待验证。回滚仅移除新增模式门禁。
- 实施与验证：OnGUI 已增加模式入口判断，ReachTarget 在创建 GUIStyle／绘制前返回；ConsumeAllGerms 原逻辑及 UNITY_EDITOR 范围保留。代码 diff 仅一行，git diff --check 通过；未运行 Unity，实际显示为 uncertain，待编辑器编译后分别进入两种模式确认。

## 2026-10-16 CollisionTiles 按沙土图片轮廓烘焙（修改前核对）

- 需求与基线：用户指出顶部碰撞与沙面不贴合。InitMapFromPrefabAsset 目前仅以 ContainsPoint 的 Renderer 矩形填格，再减去洞源；未读取 Sprite Alpha。当前 Sp_wd_tk_02 有顶部透明区域，Sliced Border=(0,2,0,73)，不可读；只提高细分无法消除透明区域中的碰撞。截图具体关卡尚未指定，不推断或批量重烘焙用户资源。
- 来源与维护边界：DigTerrainCtrl 两个手写 partial、SceneSpriteBrushClear.SliceAxis／BuildProgressMasks、DigSand.shader.SpriteImageUV、DigLevel_010／012 Prefab，以及既有压缩存储和描边记录。CodeGraph 不可调用、037 plan 缺失、知识库无匹配，以现行源码为据。原地图压缩／恢复和玩家逐格挖掘继续复用。
- 拟改方案：增加仅 InitMap 读取的轮廓贴合开关（默认开）与 Alpha 阈值；按格中心采样 Sprite Alpha，Sliced 使用与 Shader 相同的分段映射。不可读贴图仅在显式编辑器烘焙时临时 GPU 读回，及时释放，不开启生产图片 Read/Write、不增加手机逐帧采样。关闭开关恢复旧矩形填格。采样准备在修改地图前完成，不支持的 Tiled 模式明确拒绝。
- 验收与边界：透明区不填格、不透明区保留；Simple／Sliced、Size／非等比缩放、重复烘焙、洞口、压缩恢复及开关关闭旧行为。Shader 屏幕像素描边不纳入物理，仍保留半格量级离散误差，不承诺像素级曲线。验证使用原生 Unity 隔离烘焙与编译；正式关卡及手机表现仍待验证。回滚关闭开关后重新 InitMap 保存，或成对恢复地图与 Prefab。

### 当前用法、验证与风险

- 在 DigTerrainCtrl 的“碰撞贴边”中保持 `Fit Sprite Outline` 开启（默认 true），`Collision Alpha Threshold` 默认 0.1，然后点击 InitMap 并保存当前关卡 Prefab。关闭开关再烘焙恢复旧矩形碰撞。已有烘焙图不会自动迁移；本轮没有改动或重烘焙生产关卡资源。
- 最终实现只改 Terrain 两个手写脚本：编辑器先准备图片 Alpha 采样，再按原网格中心填格；Simple 与 Sliced 分段映射兼容。不可读纹理临时读回在 finally 释放并恢复活动 RT；保留原图片导入配置。后续洞源裁剪、位图压缩、BindRuntime 恢复、玩家 Stamp 和 FlushCollider 沿用原路径，无新增运行时扫描或全局事件。
- 隔离 Unity 6000.0.59f2 验证 17 项通过：透明／实心与凹凸边缘、矩形外、Simple 翻转、Sliced 拉伸、小于 Border 的 Size、非中心 pivot、旋转及非等比 Scale、不可读纹理恢复 RT、读回前后 10000 点一致、逐格恢复／重复烘焙、非空 Composite 路径顶部收进透明区、关闭开关逐格恢复旧行为、出生洞，以及实际 Sp_wd_tk_02 图片。合成场景原矩形 2460 格，裁剪后 2251 格；实际图片顶部采样有 662 个透明点。TEMP 中测试源码副本与生产源码 SHA256 相同。
- Editor／Android 条件编译各 1228 源文件 0 错误（113／120 既有警告），定向 diff 检查通过。证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-sand-outline-collision-20261016/` 下 checks.txt、unity.log、SandCollisionVerification.cs 及 compile-editor／android.log。首次夹具切换 flip 后仍用切换前缓存的非中心 bounds，翻转断言失败；改为按实际 InitMap 时机重建采样器后通过，失败日志保留，没有据此修改生产逻辑。
- 未验证与性能：正式关卡脚底视觉、Android／iOS 真机、旋转／紧密打包 Atlas、Sliced flip 与复杂父级剪切仍 uncertain。Tiled 开启贴合时在地图修改前明确拒绝。仍按格中心近似，边界有半格误差；Shader 外描边按屏幕像素变化，不纳入物理。编辑器增加一次纹理读回／采样，运行时仍恢复同尺寸位图，不新增每帧 Alpha 读取；更复杂的边界可能增加 Composite 路径点，手机物理耗时未实测。
- 回滚：先关闭开关、InitMap 并保存；或定向撤回两个脚本本轮修改，并将已重烘焙关卡的地图／Prefab 成对恢复。生产资源未改，本轮无需回退素材或关卡数据。
- 验证补查：Prefab 隔离场景的 TilemapCollider.bounds 为零，第一轮“范围缩小”断言缺少非零前提，不能作为物理贴合证据。增加非零检查后失败，确认是该观察方式无效；最终改为 GenerateGeometry 后读取实际 Composite 路径，得到非空路径且顶部低于图片矩形上沿，17 项重新通过。范围检查的局限与失败日志保留于 bounds-empty-unity.log；本次未宣称已完成真实 Actor 接触实测。
