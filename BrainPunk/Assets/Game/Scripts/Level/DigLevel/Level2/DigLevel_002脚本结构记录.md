# DigLevel_002 入笼与成功表现

## 2026-10-13 多角色适配：当前表现绑定

共享DigLevelCtrl按全部参与角色到达判胜，单个角色到达先回调并冻结本人。因此成功动效不再依赖整关已经锁胜：002南瓜新增Visual子节点，将既有SpriteRenderer与Light_effect移入，DigPumpkinSuccessEffect.pumpkin显式绑定Visual；原Renderer文件ID、Sprite、位置、排序和事件引用保留。静态Rigidbody2D、Base形状源、左右墙、底与GoalTrigger留原层级，动画不改变碰撞。002保持旧单actor引用，由兼容入口初始化，没有迁移布局／地图。

真实Unity隔离验证：旧002单角色仍到达即锁胜；003首次到达回调时Visual缩放、全部笼子Box世界Bounds不变，另一角色继续。23项及Editor／Android条件编译见DevelopMD/验证记录/Dig多角色_2026-10-13/验证记录.md。完整生产UI／003路线／双端真机仍uncertain。此节替代下方历史“动画缩放静态Collider但已锁整关”的适用前提；回滚只恢复Visual相关5个既有YAML块并移除2个新增块，不回退其它用户编辑。

## 2026-10-13 南瓜成功动效：修改前核对

- 用户已确认触发成功，现要求OnReached补充可爱的抖动缩放。当前Prefab已由用户恢复笼子Base（IsShow=true、外扩0），选择开局预留空腔；此前Agent只在TEMP复现／预览，没有回写地图修复。保留当前地图、材质、音频、排序和砖块，以及已有Light_effect.SetActive(true)监听和VictoryDelay1秒。
- 来源／边界：DigTargetTrigger.ReportArrival→TryReachTarget／BeginVictory先锁／冻玩法，再OnReached，最后TickVictoryDelay结算。SuccessAnimCtrl含全局遮罩／音效／DB，不匹配；ShakeRotateHandle默认循环旋转，无本次单次缩放，不改共享组件。复用DOTween和局部UnityEvent，不动共享Actor或结果流程。已读模块／本关风险和结构、Editor约束与上述表现说明；CodeGraph不可调用，以源码／YAML核对。
- 拟改：Level2新增DigPumpkinSuccessEffect，显式绑定当前南瓜Transform；用户补充要求干脆利落，将动画收紧为0.36游戏秒，短压扁、快速上弹、轻微左右摆动后立即恢复原姿态；OnReached追加PlaySuccess。Sprite与Static刚体／碰撞体当前在同节点，动画只在胜利已锁后执行，主角／病菌被冻结、输入关闭，不移动位置。禁用／销毁和重复播放杀自有Tween、归位；Editor不启动，无单例或数据链。
- 验证：真实Trigger→亮灯和一次动效；压扁／拉伸／角度采样、0.36秒归位、1秒一次结算；半途禁用和重复播放恢复／不累乘、新实例原姿态；持久绑定保存重导入、非任务Prefab块逐字保留、Editor／Android条件编译。更短胜利延迟可能截断动效，双端真机／完整生产UI／原生广告仍uncertain。仅回滚新增脚本／组件／事件监听，不回退用户地图和其它编辑。
- 首次共享日期记录写入失败，已先在本模块留存事前核对后才进入代码修改；收尾再同步日期记录。

### 最终实现与验证

- 南瓜节点新增 `DigPumpkinSuccessEffect`，显式绑定同节点Transform；动画时长0.36、缩放幅度0.1、摇摆角度6。约50毫秒压扁、90毫秒上弹、79毫秒轻摆、140毫秒快速归位，结尾使用OutCubic消除拖尾，不循环、不移动位置。参数在Inspector中文显示。
- GoalTrigger.OnReached保留首个Light_effect.SetActive(true)，第二个持久监听调用效果组件PlaySuccess。自然成功仍先锁／冻玩法再启动两项表现；无效／重复接触不播放，原1秒结算不等待动画回调。半途禁用／失活杀本组件Sequence并还原原缩放和旋转，新关不自动播放。
- 真实Unity6000.0.59f2／DOTween14项场景检查通过；真实目标触发、压扁／上弹／摆动、0.36秒归位、重复不重播、1秒一次结果、半途禁用、重播不累乘、失活和新关恢复均覆盖。实测Y缩放约0.88～1.14、最大摆角约6度。Editor／Android条件C#编译0错误。只改Prefab两个既有块、新增一个组件块，177个未涉块逐字保留，地图／Base配置／材质／音频／用户排序保留。
- 首次TEMP将关卡导入和大量挖掘放在同一帧后直接触发，长帧跳过50～90毫秒初段采样；验证改为完成准备后进入稳定帧再触发，没有为此改变生产代码。最终0.36秒参数按用户节奏要求调整。证据见 `DevelopMD/验证记录/Dig南瓜成功动效_2026-10-13/验证记录.md`。
- Android／iOS真机60fps、完整生产UI／原生广告、主观美术验收仍uncertain。缩放／旋转包含当前南瓜节点下静态Collider，但播放边界是玩法已锁／主角被冻结；不改地图、结果或暂停流程。若以后胜利延迟短于动效时长，清理会截断并归位，不补全等待。

## 需求和原有行为

用户要求主角落进南瓜笼子，目标成功到达后回调并显示已有 `Light_effect`。修改前已有静态刚体和1.38×0.9的大Trigger，但没有实体左右墙／底，目标脚本引用为空；灯光默认inactive，笼子排序组50把整个笼子放在主角前方。

## 最终绑定和职责

- 根 `DigLevelCtrl`：ReachTarget、Index匹配本关Actor0、VictoryDelay1；保留已有相机、地形、墨水、提示绑定。主角继续使用源 `DigActorCtrl`，不新增关卡动作或共享Actor接口。
- `Scene/SuccessTarget/Sp_wd_lg_01_1`：保留位置、静态Rigidbody2D和原大Trigger。当前用户恢复的 `BaseDigComp`，HoleShape显式绑定该大Box，OutwardDistance0、IsShow=true，已加入根地形SpawnHoles；原FixedBrick源保留。只由Editor InitMap生成笼内空腔，运行时不初始化挖洞。新增局部成功动效组件，见上节。
- 三个子节点实体Box：LeftWall offset(-0.75,-0.42)／size(0.12,0.9)，RightWall(0.75,-0.42)／(0.12,0.9)，Bottom(0,-0.9)／(1.62,0.12)，均非Trigger，开口向上。
- `GoalTrigger`：Box offset(0,-0.84)／size(1.2,0.08)，IsTrigger=true；独立 `DigTargetTrigger` 显式绑定根关卡、自己Box和Index0。原大Box不挂目标脚本，避免笼口接触提前冻结。
- `Light_effect`：保留用户已有对象、Sprite和位置，初态inactive；GoalTrigger.OnReached先绑定此对象GameObject.SetActive(true)，再调用南瓜成功动效。当前用户美术排序及材质保持，本轮不恢复早期后片9／灯光12设置。

## 调用、状态和维护边界

实际Trigger Enter／Stay → attachedRigidbody识别主角 → TryReachTarget核对本关／编号／模式／暂停／结果锁 → BeginVictory锁结果并冻结 → 返回true → OnReached点亮；Update继续原1游戏秒等待 → Submit → 原DigPlayCtrl统计／广告／胜利UI。拒绝到达和重复接触不执行回调。灯光由Prefab初始状态控制，Reset／Replay新建关卡自然恢复熄灯；不追加全局事件、DB、存档或控制器重置逻辑。

修改资源为002 Prefab、其地图Mask与目标脚本；MapPreview内容保持原样，继续引用同一Mask GUID。用户装饰、砖块、精度参数、其它关卡不在本轮行为变更范围。共享目标组件新增事件默认空，旧关只保留原上报行为。没有运行时层级搜索、动态挂组件或地图烘焙。

## 验证与回滚

Unity6000.0.59f2隔离验证14项Editor检查和26项PlayMode检查通过；定向合并后的实际Prefab再次导入并通过物理检查。笼子下落检查从笼口上方开始，实测主角经过笼口不提前触发，落至笼底才点亮并冻结，1秒后仅结算一次；关闭目标后底板停稳本地y=-0.49，左右墙能独立挡住主角。编号不匹配／其它Actor／暂停／禁用／非Trigger、重复接触、延迟0、空回调、新实例熄灯均覆盖。Editor／Android条件C#编译0错误。

另从原出生位置沿竖井下落，实测停在原FixedBrick顶，body.IsTouching明确命中该砖，世界中心y≈0.75；未达到目标时不亮灯。当前砖块位于(-0.98,0.1)、Scale(1.65,0.6)，碰撞顶y=0.4加Actor半径0.35与实测一致。生产布局保留，玩家通道需要绕砖；完整从出生点绕砖到笼子的手工路线未验收，不能把笼口下落检查写成整关已通关。

156个未涉Prefab YAML块逐字保留，MapPreview SHA256前后相同；目标脚本与隔离副本SHA256一致。证据见 `DevelopMD/验证记录/Dig入笼_2026-10-13/验证记录.md`。隔离环境只替代共享事件订阅依赖，使用真实Unity物理、Dig脚本和烘焙地图；Android／iOS真机、完整生产HUD／广告／存档／退出链仍uncertain。

回滚仅恢复本轮目标回调、002笼子相关Prefab块及MapMask；备份在 `C:/Users/admin/AppData/Local/Temp/brainpunk-cage-20261013/before.prefab` 和 `before-DigLevel_002_MapMask.asset`。不回退并行艺术资源迁移、随机装饰或001改动。
