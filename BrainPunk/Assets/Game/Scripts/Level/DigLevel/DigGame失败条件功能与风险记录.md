# DigGame 失败条件功能与风险记录

## 2026-10-16 死亡角色排序

- 用户要求死亡角色显示于SceneLayer／200。Actor位于Scene排序组内，原来没有自身排序组；基础DigActor.prefab现显式绑定默认禁用的根SortingGroup，PlayDeath统一启用，设置SceneLayer／200并开启sortAtRoot，避免祖先SceneLayer／1限制整体排序。只抬高死亡Actor，不改场景或其它角色排序；原重建生成新实例恢复正常排序。
- 同轮初始化改为保留Inspector Circle半径，成长按其初始值相对缩放；死亡继续读取实际Circle.radius计算原离屏终点。未调整上跳／下落Tween、失败最短等待、暂停、结果提交、FailArea速度或广告顺序。
- 真实Unity隔离36项及生产Prefab排序绑定验证通过，含父组／外部80层的死亡前后渲染、上下跳两入口、成长／尺寸、暂停和回调取消；Editor／Android编译0错误。正式Spine关卡画面及手机未验证（uncertain）。完整修改前核对、证据目录和回滚见同目录《DigGame功能说明.md》2026-10-16章节；回滚只撤此次Actor排序／半径改法和Prefab新增组件，保留用户资源参数。

## 2026-10-15 FailArea下落速度变化（用户反馈后的事后补查）

- 用户实测：取消上跳后，下落velocity仍发生变化。源码确认BeginFailure先锁结果并ApplyPause，SetFrozen保存刚体速度并关闭物理；随后PlayDeath(false)继续关闭物理，使用DOMoveY(endY, deathFallDuration).SetEase(Ease.InQuad)驱动Transform。
- 直接结论：原速度虽保存在_velocity，但这条死亡Tween不读取它；Ease.InQuad从零速开始，平均速度由离屏距离／下坠时长决定。只删除上跳段会改变下坠距离，并不能保留触发前的速度与重力轨迹。此前45项验证覆盖单调下落和时长，未覆盖速度连续性，不能用其证明保持原物理下落。
- 本轮仅核查并解释原因，未修改死亡逻辑。若修正为延续原物理下落，需要一起核对结果锁冻结、暂停／后台恢复、离屏完成及禁用清理，避免只删Tween导致角色被冻结或结果不提交。

## 2026-10-15 FailArea跳过死亡上跳（修改前核对）

- 用户追加要求FailArea也不触发jump。当前FailArea／Trap均由TryFail→BeginFailure→DigActorCtrl.PlayDeath统一播放0.18秒上跳＋0.55秒下坠；本次只让FailArea跳过上跳，直接从当前位置下坠离屏，仍等待完成后提交失败。Trap、墨水、超时、激光和过大病菌保留原表现。
- 来源与边界：DigFailAreaHandle.ReportContact、Trap继承扩展、DigLevelCtrl的两个TryFail及过大病菌直接BeginFailure、DigActorCtrl.PlayDeath／OnDisable。均为手写代码；不改序列化字段、默认身体scale、Prefab、统计／广告／重开入口或只读模块。
- 拟修改：FailArea提供受保护的死亡上跳策略，默认false，Trap覆写true；在现有统一调用链传递该值。保留TryFail和PlayDeath的旧签名作为默认上跳入口，新增显式参数重载；私有BeginFailure默认仍上跳。Actor只按参数决定是否加入上跳Tween，其余冻结、碰撞关闭、Spine停止、下坠终点、暂停、取消和完成回调保持原流程。
- 验证：复用真实Physics2D／Actor／DOTween隔离场景，对比两类型×Trigger／Collision早期Y方向、FailArea整段单调下落、尺寸不变、完成后唯一结果，并回归旧TryFail默认上跳、暂停／失活及原26项；Editor／Android条件编译。完整生产画面／平台仍待实测。回滚仅撤本次策略与参数转发，恢复FailArea原上跳，不回退已验收的音效／粒子隔离或用户其它修改。

### 当前实现与验证

- FailArea的PlayDeathHop返回false，Trap覆写true，经TryFail的新三参数重载传入Actor。旧TryFail／PlayDeath签名保留并转发true；过大病菌直接调用的BeginFailure默认也是true。没有新增Inspector配置或修改Prefab。
- FailArea不创建上跳Tween，只保留原下坠段，默认0.55秒；保持身体scale，落至原相机下边界终点后提交一次失败。Trap默认0.18＋0.55秒及音效／Knock保持。暂停、后台和失活继续使用原Tween／generation边界。只更新“从最高点下坠”的旧字段注释，以包含FailArea直接下坠的新事实。
- 原26项复测并增加轨迹／时长／缩放／旧入口／暂停取消后，真实Physics2D与DOTween隔离PlayMode共45项通过。轨迹用Goto采样，结果提交／暂停恢复使用PlayerLoop等待；地形和音频输出仍为桩。Editor／Android1226源文件条件编译0错误（113／118警告），定向diff检查通过。
- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-failarea-nohop-20261015/` 的runtime-checks.txt、unity-checks.log、compile-editor.log和compile-android.log。正式关卡画面、移动端、完整广告／资源重开仍 `uncertain`，不将隔离轨迹验证扩大为现场验收。

## 2026-10-15 FailArea基类与Trap子类（修改前核对）

- 需求：单独提供无陷阱音效／Knock粒子的FailArea脚本，并提取公共基类供Trap继承。采用可直接挂载的 `DigFailAreaHandle : MonoBehaviour` 作为基类，`DigTrapFailureHandle : DigFailAreaHandle` 只扩展失败被接受后的表现；不另增空壳组件。两者仍进入DigLevelCtrl.TryFail，保留角色统一死亡与失败重开。
- 原行为／来源：DigTrapFailureHandle通过Trigger／Collision Enter和Stay、attachedRigidbody识别Actor，并检查当前组件／同节点Collider／本关引用，再由TryFail拒绝外来、已到达、暂停和已锁结果。接触点在TryFail关闭角色碰撞前取得，成功后调用PlayKnock及AudioManager.PlaySound("trap")；OnDisable清理粒子。
- 维护及兼容边界：这些脚本为手写，无生成器。已有SharpTrap源Prefab及嵌套关卡使用Trap脚本GUID、level、trapCollider、knockEffect。移动level／trapCollider到基类时保留字段名、类型和SerializeField，Trap原GUID／字段绑定不变；基类Inspector只把trapCollider显示为“失败碰撞体”。不修改现有关卡Prefab、资产布局、DigLevelCtrl、广告／存档／打点、只读Packages／GF。
- 拟修改：基类保留原判定和实际接触点计算，物理消息采用protected方法确保子类继承；增加失败已接受后的protected虚方法，基类为空。Trap覆写该方法，保持先Knock后trap音效和原粒子清理。无需新增全局事件、搜索引用、加载器或重建分支。
- 验证范围：原Trap字段移至基类后的原生Prefab序列化读取、FailArea与Trap真实Trigger／Collision、拒绝和重复接触、不播放／只播放一次的音效计数及粒子状态、原统一结果链，Editor／Android条件编译。地形／音频输出可用边界桩，但不得把音频计数当作实际混音验证；正式关卡、原生广告和手机仍待验收。回滚只将公共字段及判定移回Trap并移除新增FailArea脚本，现有Trap资源无需改写。

### 当前用法与结果

1. 在FailArea的Collider2D所在节点添加 `DigFailAreaHandle`，Collider勾选Is Trigger并保持enabled，Level绑定本关DigLevelCtrl，“失败碰撞体”绑定同节点Collider。若同节点BaseDigComp的HoleShape也绑定此Collider，执行InitMap时需勾选“烘焙后启用碰撞体”。
2. FailArea基类只提交TryFail，不调用音效、Knock或其它区域表现；按后续追加要求，角色跳过死亡上跳，直接下坠离屏后沿统一失败重开。支持Trigger／普通碰撞的Enter和Stay、角色子Collider、暂停恢复；外来／已到达角色和重复结果仍由原门禁拒绝。
3. 原Trap现在继承该基类，覆写OnFailureAccepted先播原Knock再播trap音效，OnDisable继续清理原粒子。Trap脚本GUID和三个序列化字段名不变，未修改生产SharpTrap或任何关卡Prefab；新增FailArea由制作人挂到实际失败区域。

- 验证：Unity6000.0.59f2原生导入未改写的SharpTrap副本，继承的trapCollider仍指向同节点Collider，level／knockEffect字段可读；未复制Knock外部资源，因此该项不证明原特效资产的实际渲染。真实Physics2D／DigLevelCtrl／DigActorCtrl／DOTween／ParticleSystem隔离PlayMode26项通过，覆盖两类型×两碰撞模式、表现差异、重复请求、一次失败结果、Trap清理、暂停恢复、禁用、外来Actor、子Collider和胜利后拒绝。
- 地形与AudioManager为边界桩，音效检查只证明调用次数／门禁，不证明SoundsGood实际混音。Editor／Android1226源文件条件编译0错误，警告113／118；定向diff检查通过。证据目录 `C:/Users/admin/AppData/Local/Temp/brainpunk-dig-failarea-20261015/`，包含serialization-checks.txt、runtime-checks.txt、unity-checks.log及compile-editor.log／compile-android.log。
- 剩余风险：正式FailArea的摆放、物理层矩阵、手机触控／广告与完整存档重开仍 `uncertain`；需制作人绑定目标关卡后实测。没有自动创建失败区域或替换已有陷阱，也没有将无音效要求扩大为跳过角色统一死亡。

## 2026-10-14 死亡上跳后下坠：修改前核对

- 需求与行为基线：用户要求 Dig 参考 IWanner 的死亡观感，保持大小、先上跳再下坠离开画面。当前 `DigActorCtrl.PlayDeath` 将 Body 在 0.5 秒内缩到零；全部失败来源经 `DigLevelCtrl.BeginFailure` 等待该完成回调后提交，007 另有并行的最短 1 秒等待。
- 来源与边界：`Actor_IWanner.Die` 复用 `ActorCtrl.Die` 的 Spine 死亡表现并关闭根碰撞；其另有放大后恢复的反馈，不照搬到 Dig。DigActor Prefab 的 Body／双眼共用根节点，Body 单位图形与 Circle 半径按同一尺寸映射。`DigLevelCtrl.inputCamera` 已显式绑定，初始化检查引用非空；本次使用该相机，不增加全局查找。相关代码均为手写；只修改 Dig Actor 与既有失败调用，不改共享 Actor、IWanner、Prefab、存档、打点、广告或 SDK。
- 拟修改：`PlayDeath` 接收本关已有相机，停止成长 Tween／物理／碰撞后移动整个 Actor；默认上跳 0.6 世界单位、0.18 秒，再用 0.55 秒加速落到相机下边界减当前角色半径与余量的位置。三项参数显式序列化，死亡当帧的缩放保持；仍由原完成回调与 generation 门禁结算，007 等待仍与动画并行。普通失败演出从 0.5 秒变为默认 0.73 秒，属于本次明确要求的表现变化。
- 验证计划与未决项：真实 Unity／DOTween 验证先升后落、当前尺寸不变、宽高比／相机位置／大角色离屏、成长途中死亡、暂停／后台恢复、失活取消、重复失败、多人仅失败者移动及 1 秒最短等待。全生产 HUD、原生广告与双端真机未覆盖，标记 `uncertain`；不以隔离验证替代现场观感验收。回滚定向恢复原 PlayDeath 与单个调用，保留并行激光等待和其它工作区改动。

### 当前行为、使用与验证

- `DigActorCtrl` Inspector 的“死亡上跳高度／死亡上跳时长／死亡下坠时长”默认分别为 **0.6／0.18／0.55**；距离为世界单位，时间为游戏秒。已有 Prefab 使用新增字段默认值，无需补绑定。整个角色根节点一起移动，身体和双眼保留死亡当帧尺寸；吞噬成长插值会被终止。终点取本关相机视口下边界，再减当前世界碰撞半径及 0.1 世界单位余量。
- 本次只改 `DigActorCtrl.PlayDeath` 及 `DigLevelCtrl.BeginFailure` 的相机传参。现有所有失败条件共用新动效，离屏后调用原结果链；其它角色保持冻结。007 最短 1 秒等待仍与演出并行，实际提交时刻取二者较晚者。后续历史章节的“0.5 秒缩小死亡”仅记录当时实现，不代表当前表现。
- Unity 6000.0.59f2 真实 PlayMode／DOTween **21 项通过**，覆盖保持尺寸、先升后落、完全离屏后唯一结果、成长途中死亡、多人冻结、三种相机位置／尺寸／宽高比及 letterbox、上跳暂停、下坠后台恢复、007 的最短等待、Inspector 参数、退出取消和新实例恢复。其中最高点／中途轨迹采样使用 DOTween `Goto`，其余等待由真实 PlayerLoop 推进；相机检查验证世界坐标几何，没有做生产渲染截图验收。
- 主程序集 1223 份源码：Editor 条件编译 **0 错误／113 警告**，Android 条件编译 **0 错误／118 警告**；不是 APK／IL2CPP 构建。两个修改脚本及 BaseDigComp 的测试副本 SHA256 与生产源一致。定向 `git diff --check` 通过。测试、边界桩与日志见 [死亡动效验证记录](../../../../../DevelopMD/验证记录/Dig死亡动效_2026-10-14/验证记录.md)。
- 风险与边界：移动相机、旋转相机、超出现有 Body／Circle 尺寸契约的新美术及完整生产失败 HUD／广告／双端真机仍为 `uncertain`。本次相机终点按死亡开始时的视口计算；当前 001 正交相机配置已查，未新增跟随相机逻辑。回滚只撤本次 Actor 参数／运动与单个相机传参；不要覆盖并行的失败等待、007 或美术资源。

## 2026-10-14 激光命中最短失败等待

- 007 激光要求从 Actor 命中开始约 1 秒后重置，同时必须保留原 Actor 死亡表现和统一失败结果链。`DigLevelCtrl` 新增 `TryFail(actor, failureDelay)` 重载；等待以游戏时间推进并与死亡动画并行，只有等待归零且动画完成才提交失败。暂停／后台期间不推进，失活后由原 generation 门禁取消旧回调。
- 原 `TryFail(actor)`、墨水耗尽、超时、陷阱及病菌过大失败仍使用 0 秒额外等待，保持约 0.5 秒死亡动画完成即提交。没有修改 `DigPlayCtrl` 的失败统计、广告、黑幕或重建顺序，也没有新增全局重置事件。
- Unity 6000.0.59f2 隔离运行验证延迟失败、暂停／恢复和旧入口时长，连同激光物理共 12 项通过；Editor／Android 条件编译 0 错误。完整生产 HUD／广告覆盖与双端真机仍为 `uncertain`。证据见 `DevelopMD/验证记录/Dig激光眼_2026-10-14/验证记录.md`。
- 回滚只撤等待重载、三个失败状态字段和 Update 推进入口；保留原统一失败 API 及 2026-10-13 三种失败组件。

## 2026-10-13 修改前核对

- 需求与验收：补充可挂载的墨水耗尽、超时、陷阱失败脚本，参考Draw的结果职责结构。三个来源必须进入同一Dig失败锁定／死亡／Resolved／失败统计／黑幕重建链，不能直接开UI或额外派发主线／Draw重置事件。未挂脚本的旧关保持原玩法，TBC不接入。
- 原行为：DigLevelCtrl.FixedUpdate只在过大病菌批次中调用BeginFailure；锁结果、关输入、清接触／吞噬，实际失败角色PlayDeath约0.5秒后Submit(Failed)。DigPlayCtrl.OnResolved→DigGameResetEvent(Failed)→OnReset先记revive／fail／新start，再黑幕重建、按策略处理失败插屏后开放输入。耗尽只停止挖掘；没有超时或陷阱入口。多角色部分到达冻结本人，全部到达才锁胜。
- 参考证据：DrawLevelCtrl.TryFailAndReset／TryBeginFailurePerformance、DrawLevelCountdownCtrl和Level2倒计时说明；Draw是本地条件上报、控制器一次结果、玩法重建。Draw本身并无墨水耗尽自动失败，本次将其作为Dig新增条件，不称两玩法规则完全一致。Draw倒计时首笔有效笔画完成后启动，默认20秒；其最后5秒HUD广播与限时成功开关不属于本次失败脚本范围。
- 维护边界：相关代码均手写，没有生成器／DB schema／Luban输入。保留既有DigPlayCtrl、Actor／Germ、IDigLevelRuntime、HUD、配置、存档／analytics与广告策略；不改已验收关卡Prefab、地图或Packages／GuruSDK／GF。AGENTS与constitution已读；037 plan缺失，CodeGraph索引存在但CLI／MCP不可调用，知识库未找到本功能说明，以当前源码及本地模块／结构文档补足依据。
- 拟改位置：DigLevelCtrl增加IsPaused只读状态、局部StrokeCompleted和TryFail公共入口，失败入口只接受当前参与且未到达角色，未指定角色时选第一个未到达者；统一复用BeginFailure。新增DigInkExhaustionFailureCtrl（余额归零上报）、DigLevelCountdownCtrl（第一笔实际挖到新沙并正常松手后启动，默认20秒可改）、DigTrapFailureHandle（显式绑定本关／同节点Collider，Trigger及Collision Enter／Stay识别attachedRigidbody中的Actor）。所有新引用显式Inspector绑定，无运行时对象扫描或全局事件。
- 状态与风险：成功／失败先接受者锁结果，不能覆盖；胜利延迟期间、死亡期间、暂停／后台、未初始化或关卡失活均拒绝外部失败。任何未到达角色踩陷阱使整关失败；已到达角色拒绝再次失败。耗尽脚本在归零时立即锁失败，随后0.5秒死亡，不预留追加广告墨水救场；既有广告必须在暂停中恢复容量后再恢复玩法。超时暂停不扣时；暂停／输入关闭／取消触摸结束笔画不启动倒计时。脚本失活退订和停止，新关实例重建重置计时。
- 验证计划：真实Unity隔离物理和DOTween，测试三来源／死亡延迟／重复、错Actor、已到达、结果竞争、暂后台／恢复／失活／重建、有效及空洞／取消笔画、零／可配置倒计时、Trigger与普通Collision／子Collider、旧单／多角色大小批次及成功。主程序集Editor／Android条件编译和定向diff检查；真实UI／原生广告／存档／包加载／Android与iOS真机为uncertain。回滚删除三个新组件及其Prefab挂载，定向撤控制器新增API／笔画完成通知，不整文件覆盖并行改动。

## 当前配置方法

在需要启用条件的DigLevel Prefab中挂载并保存，旧Prefab未挂载时不启用这些条件。

| 脚本 | 挂载位置及显式绑定 | 生效条件 |
| --- | --- | --- |
| DigInkExhaustionFailureCtrl | 关卡根或子节点；Level拖本关DigLevelCtrl | 运行Update发现剩余墨水为0，立即接受失败并播放原死亡 |
| DigLevelCountdownCtrl | 关卡根或子节点；Level拖本关DigLevelCtrl，Cd Time默认20，可改为非负秒数 | 第一笔实际擦到新沙并正常松手后计时；下一笔不重置，归零失败 |
| DigFailAreaHandle | 失败区域Collider2D所在节点；Level拖本关DigLevelCtrl，失败碰撞体拖本节点Collider | Actor接触即提交统一失败，不追加陷阱音效或Knock |
| DigTrapFailureHandle | 陷阱Collider2D所在节点；继承FailArea的Level／失败碰撞体绑定，另可绑定Knock Effect | 共用失败判定，接受后播放Knock和trap音效；Trigger／普通碰撞均支持Enter／Stay |

失败区域和陷阱不需要Index，适用于所有本关参与且未到达的Actor。角色的Collider可在子节点，但attachedRigidbody必须指向持有DigActorCtrl的刚体；碰撞层必须允许接触。无额外表现的掉落失败区使用DigFailAreaHandle配置Trigger区域；需要Knock和音效时使用DigTrapFailureHandle。未指定目标关卡或位置时，不代替制作人修改关卡Prefab布局或启用条件。

超时参考Draw的首笔完成时机，不从进关或按下瞬间开始。暂停、切后台和输入取消不启动新倒计时；已开始的倒计时暂停不扣秒。Cd Time=0在首笔完成时失败；须配置有限数，非法NaN／Infinity不启动。后续同日已补齐最后5秒HUD及Draw同款数字脉冲；限时成功开关仍未新增，见Assets/Game/UI/DigGame/DigGame_GamePlayPanel功能记录.md。

## 最终流程与影响

三个条件→DigLevelCtrl.TryFail→BeginFailure→冻结全部角色／取消吞噬／播放实际失败者死亡上跳及下坠离屏→演出与额外等待均结束后一次Resolved(Failed)→既有DigPlayCtrl失败重开链。无角色参数的墨水／超时使用第一个未到达角色播放死亡。结果锁拒绝重复、成功后失败以及外来／已到达角色；部分到达的关卡仍可因其它角色失败。

未挂耗尽脚本的关卡仍允许零墨水后已有物理／吞噬继续。挂载后归零在脚本下一次运行Update接受失败，不预留看广告救场时间；暂停中先补墨再恢复会读取更新容量。HUD／广告恢复仍由DigPlayCtrl负责，外部组件不打开UI或发送重置事件。

## 验证证据与风险

- Unity6000.0.59f2独立PlayMode62项通过：实际Physics2D Trigger／Collision／子Collider及DOTween，三种来源、唯一结果、暂停后台、取消笔画、重复／错Actor、部分到达、退出／新实例以及旧吞噬胜负。地形／UI输入门禁／Odin属性使用边界桩，实际触控未注入。
- 主程序集1222份源码独立条件编译：Editor 0错误／113警告，Android 0错误／118警告。iOS仍有2处既有Guru.UniKit.Apple引用错误，未修改只读SDK；不是APK／IL2CPP构建。
- 定向diff检查通过；23个原控制器方法逐字保持，11份运行脚本与实际测试副本SHA256一致；DigPlayCtrl、analytics、Actor、Germ和runtime接口未改。证据与测试源码见DevelopMD/验证记录/Dig失败条件_2026-10-13/验证记录.md。
- P1／uncertain：正式UI／广告原生覆盖层／存档／资源包重开、Android／iOS真机与性能尚未验证；失败打点和黑幕／广告恢复只核对原调用链，不能用62项证明端到端。参考组件均为空时无全局事件或运行时对象查找；挂载耗尽／超时时每个组件只有常量时间Update，无新每帧分配、GPU读回或对象生成。
- 同帧目标与陷阱／耗尽／超时竞争由实际先接受者锁结果，未设置跨来源固定优先级；目标成功已锁后剩余计时不能覆盖它。旧失败及Actor死亡0.5秒时长保留，未复制Draw的Spine演出时长。

## 回滚

移除三个条件组件及使用关卡的Prefab挂载，再定向撤DigLevelCtrl新增API／事件／正常松手通知。保留原BeginFailure、旧吞噬与成功结果链。任务前快照位于C:/Users/admin/AppData/Local/Temp/brainpunk-dig-failure-20261013/before；不整文件还原并行004、美术、地图或共享记录。


## 2026-10-16 Actor整体成长后的死亡离屏边界

- 本轮用户要求成长只改根scale、保持世界碰撞中心。根整体缩放会同时放大Circle.offset在世界空间的偏移；原死亡终点只减世界半径，会让顶部在放大后仍处于画面内。
- DigActorCtrl.PlayDeath终点改为相机底边减世界中心相对根的Y偏移、世界半径及原0.1余量，并确保终点不高于当前根位置；不改变跳／落时长、FailArea不跳、排序、回调和暂停入口。
- 原生Unity隔离验证成长到2倍后，跳／不跳两种路径均整个Circle顶部离屏、原0.73/0.55秒时长、SceneLayer200和一次回调通过；Editor/Android编译0错误。正式关卡Spine美术边界和手机仍uncertain。证据及局部回滚见同目录《DigGame功能说明.md》当日Actor整体成长章节。回滚只恢复本次终点表达式，保留其他死亡逻辑及用户资源。


## 2026-10-16 Bomb 烧焦与左右飞出

- 用户将炸弹命中死亡明确改为烧焦、参考 Level50 旋转飞出，随后将方向简化为左右。新增 DigActorCtrl.ExplosionDeath partial，仅供 TryFailFromExplosion 的已接受失败调用；原 TryFail/PlayDeath 与 Trap、FailArea、墨水、超时和大小失败继续使用原默认行为。
- Bomb Inspector 可调力度（50）、向下加速度（20）、旋转角速度（720）及烧焦 RGB。Actor 中心在炸弹左侧用 (-0.5,0.7)×力度，否则用 (0.5,0.7)×力度。Spine 实例变黑、Alpha不变，Body旋转；不改Scale、Collider尺寸或共享材质。显式绑定现有 Spine/Renderer 计算完整离屏范围，单 Tween 共用暂停／后台／取消入口。
- 失败锁、generation、死亡完成与最短等待双条件、唯一 Resolved 及后续重开／打点链保留；仅演出分支改变。普通死亡源文件与本轮修改前归一换行后相同。前置核对、验证证据及回滚见同目录《DigGame_Bomb功能与风险记录.md》当前章节。
