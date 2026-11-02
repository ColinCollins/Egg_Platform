# Level 138 奶茶排队风险评估

## 2026-09-20 主角点单 Key 对齐

用户指定主角咖啡发言从 `S_Level138_PlayerOrder` 改为已有 `S_LevelTips_Des_level_138_1`。本轮只替换HintBubble.Show实参，不改变原显示、锁操作、服务及生命周期。135复用 `_17` 不涉及138源词条或警告代码修改。

`E:/WorkSpace/outputs/l10n-key-mapping-20260920/verification.json` 验证本关源码仅一处字符串变化，138场景、HintBubble、I2等8个保护文件哈希不变。目标词条本地存在，但各语言正文与排版未实测；未重新编译或运行Unity/真机，记为 `uncertain`。回滚仅反向替换这处Key，不恢复整份主控或语言资产。

## 2026-09-20 关店问号与延后离场

- 基线：关店后无在途杯立即离场，有在途杯到手即离场。用户明确改为全队停1秒、队首问号0.8秒再离场；等待与已发杯交付并行，离场须同时满足问号完成和接杯完成。仅138总控、Prefab模板引用及QueueActor两处相关注释变更，原服务/递物声音和成功失败业务链保持。
- 风险控制：FixedUpdate停队列前移，等待期间不启动新服务/补位/插队惩罚；不清已到手杯，已发杯继续交付；重复关门不重新计时，重新开门不取消已决定的离场。无NPC时不生成问号，主角在途交付继续原通关流程。暂停冻结本关计时和气泡动画；重开、离场、禁用和销毁清理气泡；玩家失败不把NPC冻结。
- 只读复用135固定标点气泡，在138 Prefab显式绑定；135气泡脚本/Prefab和共享HintBubble哈希保持。后续若135模板的样式或ShowQuestion接口变化，应同时复看138引用；本轮未提取共享抽象或修改其行为，也未写I2/表格。
- 验证：完整生产编译0错误/113警告；隔离实际场景58项检查通过。首次预览发现夹具店铺贴图过旧，同步最新资源后重新通过58项并查看最终截图，卷帘、队伍及问号显示正常。源码/Prefab增量、编译/运行日志、断言、截图、夹具资源同步清单在`E:/WorkSpace/outputs/level138-close-reaction-20260920/`。
- **uncertain**：业务服务为替身，正式HUD/广告/原生平台及移动端未覆盖；夹具仍有既有材质Drawer提示和9个未解析资源GUID，不能据此证明正式资源构建无缺失。最终截图只作为本次气泡位置和可见性证据。回滚按before局部撤销关店阶段、门禁/清理和新增模板引用，不整体恢复旧场景。

## 2026-09-20 递物音效

- 按用户要求在实际出杯起飞分支单次播放level64Block，玩家和NPC共用。本轮只为Level138QueueActor.cs新增using和播放行；保持既有挂点有效性检查、flying/IsHeld去重，关帘后已飞出的杯继续飞行时不重播。
- 联合135完成生产编译与静态核对，主控及Prefab哈希不变；不把静态路径检查当作正式关卡听感验证，实际听感和真机仍uncertain。证据与快照：`E:/WorkSpace/outputs/level135-138-serving-audio-20260920/`。回滚仅撤新增两行，保留原点单、交付和结算逻辑。

## 2026-09-20 点单锁操作与反应时序追加

旧规则为主角点单时可继续走/跳，135有气瓶直接出现三个感叹号。用户本轮明确改为点单即锁角色，135先问号1.5秒后再“!!!”0.8秒。运行改动为两关总控，以及135喷发分部/专用气泡脚本；不改共享输入、场景或配置。

关键风险是仅关ActorCtrlable仍允许跳跃、共享Resume开回Actor、取消后无法继续移动或成功/失败/重开反而解锁。实现同时清方向/StopMoving、暂停恢复重锁，仅正常取消释放；问号与感叹号复用主控有效帧时钟和同一气泡。验证 `E:/WorkSpace/outputs/level135-138-reaction-lock-20260920/`：135隔离运行217项、138隔离运行43项通过；完整C#编译0错误、113条警告。 包含两关真实Actor移动/跳跃和生命周期断言，以及135顾客/主角两种问号时序、暂停/重开及原成功/失败分支。

仍未覆盖完整生产HUD事件、真机多指、原生覆盖返回及低帧率，标记 `uncertain`。回滚按本轮 `before/` 撤回对应脚本局部差异，保留上一轮点单和原业务链；不对共享组件或表格做整文件回退。


## 2026-09-20 主角点单变更

用户本轮明确点名138和135，新增“主角说一句话后才给饮料”的阶段；下文2026-09-15“只补文档”及禁止向工程加探针的描述属于历史轮次。本轮工程仅改138总控和一条I2词条，验证夹具置于 `outputs/` 的隔离项目，未向正式Assets新增测试或工具。

原主角有效落地服务1秒，现前置1.5秒气泡再开始同一交付时钟，右手到杯后仍走标准胜利。主要风险：点单/交付同时推进、关帘和重入绕过点单、死亡/重开后延迟胜利、旧气泡叠加。`E:/WorkSpace/outputs/level135-138-player-order-20260920/` 的31项隔离运行断言覆盖上述主角流程、暂停、关店取消与飞杯后关店例外；气泡截图已检查，C#编译0错误/113条既有警告。

范围核对保持138QueueActor、CurtainDragInput、138 Prefab、共享Actor/BaseLevelCtrl/HintBubble哈希不变。135同时按用户要求独立补齐，175项运行断言通过。旧NPC完整插队、补位生产链和移动端未重新验证，结论仍为 `uncertain`；新咖啡点单只填中文、英文，其余语言未补译。回滚仅按本轮 `before/` 撤回主角点单及新增词条，不能覆盖旧关其他历史改动。

## 基线与范围

下段为制作阶段基线；本轮仅补充文档，当前已提交/未提交边界以新增文档审计节为准。历史检查不代表本轮重新运行。

制作前已有 `Level_138.prefab`、Coffee 人工摆位、新奶茶素材和 `10138 -> Level_138` 登记，没有本关运行时脚本；旧门仍引用标准成功链。本次新增 `MainLevel/Level_138/` 三份脚本及其 `.meta`、目录 `.meta`，装配本关 Prefab，并更新同目录功能说明。共享运行时代码、Actor/粒子/动画源资产、排序、配置登记、字体、asmdef 与 ProjectSettings 不作本次修改。

需求依据为同目录 `Level138奶茶排队功能说明.md` 与 `docs/contexts/levels/138/CONTEXT.md`，包括最后确认的“空中不累计、区内资格有效时不清零，落地续计”。不新增 Domain、测试或 Editor/PlayMode 探针。

## 2026-09-15参照137的文档补充与状态审计

- 验收声明：以dev138@4c0708fe5当前实现为准，按用户指定的dev137/Level137车载钩取解谜功能说明补齐状态表、子流程、真实成员图、函数索引和共享差异风险。只改本功能/风险文档，不修改玩法、资源、配置或参考文档。
- 新版功能说明覆盖7个Phase，分别说明三类NPC成员、服务与关店标志、杯/衣服占用/气泡交接/指针捕获；去掉旧设计稿的虚构实现类型和函数，不把概念状态当新增C#枚举。增加当前参数表、共享差异D1～D10及风险R1～R13，细节以功能说明对应节为唯一说明位置。
- 本次复核纠正的表述：当前工作区存在未提交85→10138临时路由，HEAD仍为85→10084；LevelData登记及去重已提交。当前源玩家SortingGroup为200、实例无覆写，离场按PlayerOrder-10静态推算190，高于在队103～110；“降层级”不能泛指低于队伍。本关Phase.Failed与共享FAILED退出节点不等价；全场暂停、初始化事务回滚和Disable/Enable复用也没有代码保证。
- 本次没有因发现上述差异而修复代码；它们作为事实边界与潜在问题记录。实际层级观感、输入恢复窗口、共享重建受理、气泡动画完成和动态资源失效仍为uncertain。
- 验证结果：内联Python检查函数索引覆盖总控48个、拖帘9个、QueueActor/CupBinding共21个方法名，7项Phase均有状态表；旧虚构类型/函数定义已移除。方法名覆盖只是检索证据，不代替正文语义审阅。Prefab178个唯一锚点且引用闭合；两个区域、衣服11/脸饰8/背饰1、-2.8阈值与17条I2文本和当前路由核对通过。
- 命令：`git diff --check -- docs/features/levels/138`通过；本机Mermaid CLI执行`src/cli.js -i docs/features/levels/138/Level138奶茶排队功能说明.md -o /tmp/dev138-doc-audit-20260915/Level138.svg`，24/24图渲染成功（16张TD流程图、8张TB类图）。`before-hashes.json`中24份已读取输入（含固定参考副本、运行代码/资产和任务前6项本地差异）SHA-256比对通过；这些是本机临时资料，不作为运行截图或测试结果。
- 参考137原文在读取期间由外部更新，首次版本检查因此停止且未写文档；重新完整读取1533行副本（SHA-256前缀91d9a85d07e3）后固定该结构版本继续，不复制137业务参数。没有改写137原文。不运行Unity、编译、测试或探针，扩大验证范围N/A；任务前本地环境/字体/排序改动保留，动态风险仍为uncertain。

## 风险项

| 等级 | 入口与影响 | 当前控制 | 尚缺证据 |
|---|---|---|---|
| P1 | 新脚本类型/程序集依赖或嵌套 Prefab 导入失败，导致本关不能加载 | 读取当前 BaseLevelCtrl、Actor、Spine 与本机 UGUI 源码；新增脚本 GUID、源对象 ID 与本地引用逐项静态核对 | 未编译、未启动 Unity，无法排除导入和编译错误 |
| P1 | NPC 仍有 Player 身份或运行玩家皮肤/输入，误触失败、换肤或移动 | 隐藏模板将根改为 Untagged/Default，禁用 ActorCtrl、ActorSkinBinding、脚步组件及 pushCollider；只由 QueueActor 驱动运动学刚体 | 实际克隆初始化次序、碰撞和皮肤渲染未运行验证 |
| P1 | 拖帘指针滑到移动按钮造成角色误走，或多指抢占 | 本关 BaseRaycaster 捕获原 pointerId，持续命中自身且不拦截其他指针；起手先尊重 UI 命中，局部Y始终夹限 | 鼠标、触摸输入模块、不同画幅/HUD遮挡与暂停取消手势未运行验证 |
| P1 | 玩家绕到队头抢领、空中累计、关店后新发杯或补人 | 正常服务检查营业/队首/区域；关店只保留已在飞行的当前服务，finishingClosedShopDelivery阻止队列前移、攻击、新服务与补位；已有在途补位者立即转离场 | 关店瞬间、重复开关、原地等待与下一次服务的实际帧序未运行验证 |
| P1 | 关店吞掉飞杯、等待队列提前离场，或失败玩家获延迟胜利 | 保留当前杯和serviceElapsed，IsHeld后才统一离场，不等满3秒；玩家用同一标准胜利入口，失败/离区取消，Reset停止更新，Cleanup回收；无独立Tween/完成任务 | 暂停/失败/重开与接杯同帧、杯目标跟随、真实整队离场和标准胜利链未运行验证 |
| P2 | 关店在途补位者仍归队、撞人或被切回walk，等待者重新开店后恢复攻击 | 关店先Depart(inflight)再清引用，清IsRefilling前保存walk01，离场TickMove持续使用保存值；无碰撞路径和待交付队列守卫保持 | 实际walk01过渡、关闭Collider接触解除、首帧停止与骨骼姿态未运行验证 |
| P1 | 播打斗时仍悬停，或错误地延后至落地才播放 | 原头顶上方范围直接进入Fight并播放效果/音效，恢复StopMoving清掉的向下速度，保持地面碰撞并忽略NPC碰撞；0.8秒从原触发点计时，无落地等待 | NPC顶面零速度解算、边打斗边下落、暂停恢复和击飞衔接未运行验证 |
| P1 | 临时碰撞忽略残留或中途恢复导致玩家被弹上NPC头顶 | 本关记录每对原ignore值；下落到击飞保持忽略，暂停恢复重申；Reset通知只停旧流程，禁用/销毁时按原值恢复后回收NPC，不改层碰撞矩阵 | 实际物理状态恢复、销毁顺序与接触重新生成未运行验证 |
| P1 | 击飞越过 FallArea 高度或碰撞体未进入，无法失败重开 | 不伪造位置或失败事件；力度50，打斗按新要求改为(-0.5,0.5)左上45度，敲击(0.5,0.5)不变；保留重力、0.8秒时钟和真实FallArea | 新初始竖直速度25下的实际轨迹、接触和不同帧率尚未运行验证 |
| P1 | 玩家失败把整条队伍停住、清掉NPC手杯/飞杯或服务进度 | 独立CanAdvanceQueue允许完整NPC流程继续；失败入口只取消玩家服务，不改NPC进度/refilling，不暂停NPC；Failed与Inactive分开 | 打斗到失败等待的连续领餐、飞行到手、3秒离场及补员节奏未运行验证 |
| P1 | 失败后队列可运行导致玩家误领杯，或新补位者撞到失败玩家 | 玩家资格要求CanInteract且未死亡，危险检测仅Playing执行；失败时现有/新生NPC成对忽略玩家碰撞，移动不以玩家限步 | 玩家死亡/暂停/重开帧序及新NPC碰撞初始化未运行验证 |
| P2 | 1.2间距下角色美术仍有交叠、NPC被推开，或队列变长后补位接触位置变化 | Actor尺寸不变，槽位中心间距1.2，主碰撞体宽约1.0453；运动学NPC不相互动态推挤，移动前对玩家边界限步；补位目标沿用相同间距 | 新装饰组合/行走姿态/放大手杯边缘是否交叠、接触容差0.04与限步间隙0.01的实际效果未运行验证 |
| P2 | 玩家身体中心与领餐区错位，或者手杯跟错骨/遮挡 | 区域标定保持；CupBinding统一使用Soft_arm_R下的Box_Center9s，拒绝同名左手挂点；杯缩放恢复0.75、order300，离场按玩家order-10 | 换装、Spine骨骼重建、右手旋转与奶茶遮挡未运行验证 |
| P2 | NPC没穿衣服、衣服重复、额外饰物重复或遮挡奶茶 | 11衣服必选池、9额外可选池分别去重，queue/inflight/leaving共同占用；clothes为空初始化失败，衣服池全占用才等回收，额外饰物不足仍可只穿衣服；整体层级不改 | 衣服与脸/背饰组合的实际可见性、走路/接杯动作遮挡仍为uncertain |
| P2 | 警告硬替换点单、叠泡、等待中又点单或重开后补播 | Fight结束请求key17；同人旧泡先走共享收起并自行销毁，再创建警告，等待与显示阶段均优先；Reset/对手销毁取消等待，Cleanup清自有气泡 | 共享show/idle/close状态与异步销毁的实际交接、暂停/语言切换/重开帧序未运行验证 |
| P2 | 飞杯从错误位置出发、终点滞后或飞行观感跳变 | 起点绑定店铺中间杯并在起飞采样；每次推进先刷新右手BoneFollower，再按0.35秒/0.35弧高计算当前终点；抵达才挂手 | 骨骼帧序、移动/转向及空中冻结后恢复、低帧率飞行帧数、世界层到Actor SortingGroup的遮挡过渡未运行验证 |
| P2 | 不播出门也冻结玩家，或暂停恢复回写过期速度 | 玩家物理/Spine暂停前先检查IsPlayerFailurePhase与快照所有权；普通出生/Playing/Reset后的Inactive不捕获不冻结，恢复仅消费已有快照；NPC与服务仍暂停，失败演出恢复保留速度 | 003式普通落定、首次出门、跳过出门重置、失败阶段暂停/恢复的实际帧序未运行验证 |
| P2 | 玩家把标题占位或本地资源存在误认为正式投放完成 | 保持原tblevelsort；现有LevelTitlePlaceholder仍引用115数学测验key，未擅自确认正式标题/I2 | 正式入口、标题与多语言交付未包含在本次范围 |
| P2 | coffee招牌描边污染公共字体、字体缺字或被帘子遮住 | 单独CoffeeSignText材质，只读复用en字体/图集；固定框架下单个TMP、内部order1；新增Localize绑定本关第16个key，secondary为`-`，无输入组件 | 动态SDF填充、I2导入及切语言（含RTL）、最终字形/像素描边、相机画幅及SortingGroup合成未运行验证 |
| P2 | 点单闪现、重复点单或关闭后残留 | 首次队首服务Show(key, actor, 1.5)，共享自动计时/收起/暂停；Show后仅Update(0)采样首帧，不改默认缩放、不每帧接管Tween；HasOrdered一人一次，销毁时清理所有自有气泡 | 闪现是否消除、Animator默认值/首帧采样、共享异步取消、长句和混合文字显示未运行验证 |
| P1 | 失败后旧关提前重建8人和拉开帘子 | StopForReset不再回收/生成队伍、清杯或开帘，只停流程；共享PlayCtrl销毁旧关再创建新关，新Start才恢复初态 | 失败等待、广告/过渡/暂停事件与旧关销毁的实际时序未运行验证 |
| P2 | 扩大玩家区域影响NPC计时，或宽区另一侧领杯速度/遮挡不适 | NPC保留原HeadZone=0.5×1.2、HeadX=5.08；独立玩家区6.72×1.2覆盖店铺主框架，玩家正常与关店待交付离区均使用新区；飞杯源和0.35秒时长不变 | 宽区左右端的实际落地/资格边界、飞行距离变化后的观感与遮挡未运行验证 |

## 静态证据与边界

### 必穿不重复衣服、打斗1.2倍与警告气泡（当前）

- 用户确认：初始队伍和补位者都必穿一件不重复衣服，脸饰/背饰继续额外随机并沿用单件去重；只放大打斗特效1.2倍，不动敲击特效。0.8秒打斗结束、玩家被击飞时由真正触发打斗的NPC说“不可以插队哦！”，共享显示1.5秒后收起；同NPC警告替代点单，期间不点单、不补播，服务和其它NPC不受影响。
- 衣服静态筛选：现有32个身体部件中采用001、002、004、005、006、011、012、014、015、016、018，共11件衣服；读图排除飞碟/赛车/泳圈/鲸鱼/蛋糕等。所选Prefab仅含GameObject、Transform、SpriteRenderer，直接引用原资源，不改共享资产。服装池和脸/背池各有ID及占用集合，三类未回收成员共同登记；生成失败不进入队列，回收后才可复用。
- 打斗只改本关FightAnimation根缩放(1,1,1)→(1.2,1.2,1.2)；共享Dog_Fight和Hide没有缩放曲线，保持原0.8秒时钟、接触位置、击飞方向/力度、NPC流程和Knock_Explosion。两个跳跃入口把对手传入BeginFailure，不按结束时的队首替代；补位敲击不捕获对手，不显示警告。
- 点单/警告统一CreateSpeechBubble，记录气泡及发言者；ShowFightWarning改为设置待交接请求，TryShowPendingFightWarning等待同人旧泡完成共享收起并销毁，之后才创建警告。旧泡仍在show或转场时先让其进入可收起状态，不强切动画、不硬销毁；没有旧泡直接显示。HasOrdered在等待/警告抑制检查前置位，服务照常继续；Reset清等待，对手销毁/禁用不补造，Cleanup回收全部自有气泡，不修改共享HintBubble。
- I2只新增 `S_LevelTips_Des_level_138_17`，zh-CN为“不可以插队哦！”，其它75槽为空；本关共17条，原15句点单和coffee保持，随机点单范围仍1～15。未改玩家存档、玩家装扮、共享字体、排序、奶茶0.75、玩家宽区、-2.8阈值或补位离场速度8/walk01。
- 本轮仅两份本关C#、Prefab衣服引用与打斗缩放、I2新增条目及所属文档。未启动Unity、编译、测试或探针；组合服饰视觉、气泡优先级与生命周期、缩放/警告实际帧序仍为uncertain，扩大验证范围N/A。若撤销须另获批准，并只回退这些本轮差异，不覆盖其它已有工作区改动。
- 静态验证：内联Python/PyYAML对比 `/tmp/dev138-clothes-warning-before/`，Prefab仍为179个唯一锚点且引用闭合，仅总控衣服数组和Fight根缩放变化；11件衣服各自解析到正确Prefab根、11个Sprite GUID互异且无脚本/物理组件。I2仅追加key17，原条目和源设置逐字保留；只填zh-CN，其余75槽空。核对衣服必选/两池去重、对手捕获、仅Fight结束发警告、同人优先及Reset/Cleanup；两份C#词法检查通过，杯子实现未变。
- 验证命令与结果：8个目标文件执行 `git diff --no-index --check /tmp/dev138-clothes-warning-before/<同名文件> <当前文件>` 无空白诊断；共享HintBubble、动画与服饰源资产的 `git diff HEAD -- <对应路径>` 为空。本机mermaid-cli执行 `src/cli.js -i docs/features/levels/138/Level138奶茶排队功能说明.md -o /tmp/dev138-clothes-warning.svg`，16/16图成功，8个流程图仍为TD。以上仅证明静态文件/调用关系，不是编译或运行验收。
- 警告顺序交接更新：静态检查确认ShowFightWarning及待交接方法已无直接Hide/Destroy，旧实例存在时不创建警告；show/转场完成后才调用共享Close，warningPending期间抑制同人点单，Reset/销毁取消等待。4个本轮文件对照 `/tmp/dev138-warning-transition-before/` 做增量空白检查通过，I2及共享气泡资源/动画未改；Mermaid输出 `/tmp/dev138-warning-transition-1.svg` 至`-16.svg`全部成功，仍未执行Unity运行验证。
- 本轮期间另检测到Prefab保存变化，导致“字节未变”检查失败，未覆盖或回滚。按源对象解析后的总控/队伍模板/打斗绑定一致；区域坐标的两处十进制重写对应相同float32值，当前本地fileID引用闭合；另有重排、stripped引用改写与空LocalizeCallBack序列化字段。以上仅为关键接线静态审阅，不以此宣称整份Prefab运行已验证；本轮气泡交接代码不依赖恢复旧Prefab字节。

### 玩家领取区覆盖店铺与在途补位者walk01（当前）

- 用户指定只调整玩家范围，宽度覆盖奶茶店左右两侧。按主框架Sp_l138_d_01的672像素、100 PPU、中心pivot、单位缩放和世界中心X=6.92，新增playerServiceZone宽6.72，世界X=3.56～10.28；高度保持1.2，中心Y=-2.0424674不变。NPC原区域、停止点、间距和服务时序不动。
- 两种玩家检测（正常资格、关店已发杯后的离区取消）均使用playerServiceZone；NPC仍使用headZone。快速关店再开店无需再次触发开门，只要关店待交付完成、queue/inflight已清空且玩家在新区落地累计1秒即可。leaving中的已离场者不占资格，不要求走出屏幕；仍保留未失败/未暂停/营业门禁。
- 在途补位者的Depart在IsRefilling清零前记录IsRefillDeparture，动画持续walk01，总控离场移动继续使用refillSpeed=8；已JoinQueue者标记为false，离场仍为walk和leaveSpeed=5.5。无碰撞、无敲击、不归队idle的规则保持。此速度更新覆盖此前仅保留walk01但统一按5.5离场的实现。
- 影响仅两份本关C#、Prefab新增玩家区域及绑定和所属文档；不改共享层、I2、店铺美术、字体、NPC区域、奶茶0.75大小、-2.8阈值或关店待交付逻辑。未编译、未启动Unity、未运行测试或探针，扩大验证范围N/A；实际宽区领取、远端飞杯、walk01观感及Unity导入仍为uncertain。
- 静态验证：内联Python/PyYAML对照 `/tmp/dev138-player-zone-before/` 确认Prefab由176增至179个唯一锚点且本地引用闭合，既有对象仅Coffee子列表和Ctrl的新引用变化，NPC区域/全部原Transform/Sprite保持；图片宽672÷100 PPU=6.72，左右界3.56～10.28。源码中NPC一次检测仍用headZone，玩家正常/待交付两次检测用playerServiceZone；离场动画在清IsRefilling前保存且后续TickMove沿用，原离场速度和关闭Collider逻辑保持。
- 命令与结果：两份C#词法括号检查通过；7个目标文件执行 `git diff --no-index --check /tmp/dev138-player-zone-before/<同名文件> <当前文件>` 无空白诊断，I2字节不变。本机mermaid-cli执行 `src/cli.js -i docs/features/levels/138/Level138奶茶排队功能说明.md -o /tmp/dev138-player-zone.svg`，16/16图成功，8个流程图仍为TD；仅为静态证据，不代表实际Play已通过。
- 回滚仅限playerServiceZone字段/三处区域参数接线、Prefab新增510～512对象及Coffee子引用/总控引用、离场动画缓存和文档；不得回滚其它已确认逻辑或用户现有工作区改动。

### 关店待交付与在途补位者直接离场（当前）

- 用户最终确认：队首已起飞的奶茶不能因关店消失，在队NPC原地等接杯后统一离场，不等满3秒；已有在途补位者关店即直接向右离场，不再归队idle，不敲击、阻挡或推走玩家，也不参与等杯。重新开店不取消本次离场；玩家已发杯可在关店后接杯胜利，失败/重开/退出仍禁止延迟胜利。
- 关店入口先停refilling并将inflight转入leaving；当前杯IsFlying才设置finishingClosedShopDelivery，保留servedNpc/servingPlayer、serviceElapsed和原杯实例。在队者WaitForClosedShopDelivery切idle并关闭主Collider，FixedUpdate不再推进其槽位；服务只继续当前飞杯，IsHeld后NPC经DepartQueueAfterClose统一离场，玩家经CompletePlayerService标准胜利。未发杯和已接杯的关店仍直接离场。
- 关闭期间及重新开店后的待交付阶段，MaybeRefill、恢复补位、CheckDanger和队伍接触检查均有等待守卫；没有为补位者添加归队等待。玩家离区/死亡仍取消，区内起跳和暂停仍冻结服务进度；玩家失败不取消NPC待交付，Reset停止旧流程，Cleanup经CancelService清等待标记并销毁杯/队列，防跨局残留。
- 范围：仅两份本关C#和对应Context/功能/风险/Map；不改Prefab、I2、共享Actor/出门/气泡，也不恢复DOTween。关店阈值仍为-2.8，奶茶缩放0.75，原0.35秒/0.35弧高、普通玩家003暂停边界保持。
- 静态证据：内联Python核对关店先转移inflight、IsFlying分支不清进度、等待队列停止槽位移动、IsHeld在原3秒检查前完成离场，以及重新开店不恢复补位/攻击、玩家失败/离区取消和唯一胜利入口。两份C#词法括号检查通过；CupBinding除IsFlying只读属性外与任务前逐字一致，Prefab/I2与 `/tmp/dev138-close-delivery-before/` 快照字节一致。
- 验证命令与结果：6个目标文件执行 `git diff --no-index --check /tmp/dev138-close-delivery-before/<同名文件> <当前文件>` 无空白诊断；本机mermaid-cli执行 `src/cli.js -i docs/features/levels/138/Level138奶茶排队功能说明.md -o /tmp/dev138-close-delivery.svg`，16/16图成功，8个流程图仍为TD。以上仅证明文件、调用边界和文档图，不是运行验证。
- 未验证：关店发生于起飞首帧/抵达帧、队列idle与身体停止、在途补位者穿过玩家、重新开店、暂停恢复、玩家失败和共享重建的实际时序均为uncertain。不编译、不启动Unity、不运行测试或探针；扩大验证范围N/A。
- 回滚只限本轮等待状态/关店与服务分支/等待者表现及文档，须另获用户批准；不回滚阈值-2.8、杯尺寸0.75、单件独占、共享气泡或003普通玩家规则。

当前尺寸更新：用户要求奶茶恢复0.75，CupScale由0.9改回0.75；仅影响玩家/NPC手持与飞行实例，台上摆件不变。气泡字体静态核对为共享Twinkly SDF，基础字号4、自动字号范围2～20，138不覆盖；长句实际字号及字形仍为uncertain，未启动Unity。

### 003普通玩家边界与奶茶再放大1.2倍（既有证据，奶茶尺寸现已恢复0.75）

- 用户确认普通出生、非出门重置和普通暂停对齐003，不额外冻结玩家物理或Spine；只保留本关失败演出暂停，NPC规则不变。奶茶手持/飞行统一从0.75放大至0.9，台上摆件不动，原代码抛物线保留。
- 依据：003/138源Actor及出门组件引用、0.4/0.2/0.2/0.2/0.25出门参数相同；PlayCtrl.CreateLevelAsync以ResetCount/FailCount决定是否播放出门。ActorCtrl.StopMoving只锁移动并清速度，模拟关闭语句是注释；共享Pause的timeScale语句也为注释。138此前在非Playing加载/重置期间无条件写simulated=false与Spine速度0，是明确的额外接管差异。
- 改动：ApplyPresentationPause的NPC/特效处理保持，玩家快照之前先判断 `!IsPlayerFailurePhase || ownsPhysicsPause` 并提前返回；只有失败演出可暂停玩家。Spine恢复移至有快照门禁的RestorePhysicsPause，Cleanup不再无条件重写Spine速度。Start/普通暂停/Inactive重置不写玩家快照，也不为恢复而强制simulated=true；不改变共享输入、出门显隐、门位置、失败演出和重建链。
- 尺寸：CupBinding创建和重新挂手共用CupScale=0.9，0.9/0.75=1.2；飞行保留同一实例和世界缩放转换，不另改时长、弧高或目标骨骼。本轮只改两份本关C#和所属文档。
- 静态验证：内联Python核对暂停守卫位于玩家Spine/模拟/速度快照写入之前，NPC暂停在守卫之前，恢复以ownsPhysicsPause为前提，Reset先进入Inactive且无旧关重建；两份C#词法括号检查通过。QueueActor与 `/tmp/dev138-003-pause-before/` 快照相比仅CupScale一行变化；Prefab/I2字节一致，`git diff HEAD -- <003及共享Actor/Base/EnterDoor/Play/StartDoor路径>` 无差异。6个目标文件执行 `git diff --no-index --check /tmp/dev138-003-pause-before/<同名文件> <当前文件>` 无空白诊断；16个Mermaid块逐字不变，不新增渲染验证。以上只证明静态代码和文件事实。
- 未验证：实际出门/不出门重置、普通暂停落定、失败演出暂停恢复、放大奶茶的遮挡与手感仍为uncertain。未启动Unity、编译、测试或探针；扩大验证范围N/A。普通暂停时玩家可能受重力继续落定属于本次确认的共享行为，不按冻结失败处理。
- 选择性回滚只限本轮玩家暂停守卫/恢复所有权和CupScale；须另获批准才能恢复旧冻结或旧尺寸，不改共享Actor/门/PlayCtrl、Prefab、I2、装饰、点单或用户字体改动。

### 单件独占、共享气泡与共享重建（此前交付，普通玩家暂停与杯尺寸已更新）

- 用户最终确认：不强制脸部，可用其它未占用装饰，任何单件不能重复；后排高于前排保持；撤销DOTween飞杯；旧关收到重开事件不恢复初态；点单显示/计时/收起/暂停全部沿用共享。这些规则替代下文历史的48组合、气泡自管冻结、DOTween和旧实例九步复位证据。
- 单件池：保留原11份资源，每个NPC随机一件，ID按脸/身体/背部串联索引；queue/inflight/leaving联合排除已占用ID，回收后才释放。Actor接受任意一个非空部件，不再强制face；三池全空才暂停补员。未改Prefab或排序公式，队尾高于队首及装饰归属的层级保持。
- 气泡对照共享BaseLevelCtrl：同样Resources加载、Instantiate和Show(key, actor, 1.5)，自动关闭经过HintBubble.CloseStraightly与收起动画。138不再传0、不使用serviceElapsed硬销毁、不逐帧Pause/Play内部Animator或文字Tween；取消/关店/离场/重开调用共享CloseStraightly，禁用/销毁时清理包括正在收起者在内的全部自有气泡。
- 首帧风险：共享Prefab根为单位缩放，show动画从0到1（0.41666666秒），默认状态为show；138在LateUpdate生成，可能在首次Animator采样前显示一帧单位缩放。现在Show后立即Animator.Update(0)采样共享首帧；不预写根缩放0，以免Write Defaults把idle默认缩放缓存为0。此为静态时序推断及针对性处理，未证明是用户观察的唯一原因，也未证明闪现已完全消除。
- 共享重开证据：BaseLevelCtrl的WaitingFail等0.2秒后检查失败，TryToRestart再等1秒发GameResetEvent；PlayCtrl.ResetGame/ReplayGame收到事件后等约0.5秒，再DestroyLevel和CreateLevelAsync。79关OnGameReset停止/清理、132关StopAttempt停止旧流程，不在旧关重新生成初态。138改为StopForReset锁输入和暂停旧表现，不调用RecycleQueue/SpawnInitialQueue/SetCurtainOpen/CancelService；旧杯/队伍/帘子保持至共享真正销毁，Cleanup恢复碰撞并回收，新Start才恢复初态。重复暂停不覆盖已有物理暂停快照。
- 飞杯恢复原CupBinding.UpdateFlight的Lerp加抛物线，取消本关飞杯Tween字段、创建、推进和清理；原0.35秒/0.35弧高、动态右手、0.75缩放与服务资格规则不变。共享HintBubble内部文字Tween保持原样，不把撤销飞杯DOTween扩大为禁止共享组件内部实现。
- 本轮只修改两份本关C#与所属文档，不修改共享运行时代码、Prefab、I2、字体、配置或存档。未启动Unity、编译、测试或探针；实际装饰可见性、气泡闪现/关闭、失败等待到新关重建均为uncertain，扩大验证范围N/A。
- 静态验证：内联Python核对单件池串联索引、三类NPC共同占用、非强制脸饰、原排序公式、共享Show/Close与首帧采样、Reset不重建旧关；两份C#词法括号检查通过。CupBinding与 `/tmp/dev138-dotween-before/Level138QueueActor.cs` 的原实现逐字一致；Prefab/I2与 `/tmp/dev138-shared-lifecycle-before/` 快照逐字一致，原11件资源引用保持。`git diff HEAD -- <共享HintBubble代码/Prefab/动画目录/BaseLevelCtrl/PlayCtrl>` 无差异。
- 验证命令与结果：6个改动文件执行 `git diff --no-index --check /tmp/dev138-shared-lifecycle-before/<同名文件> <当前文件>` 均无空白诊断；本机mermaid-cli执行 `src/cli.js -i docs/features/levels/138/Level138奶茶排队功能说明.md -o /tmp/dev138-shared-lifecycle.svg`，16/16图成功，8个流程图仍为TD。结果仅覆盖文件、序列化及文档图，不等于编译或Unity运行验收。
- 回滚只限本轮本关代码和文档差异，必须先获用户批准才能恢复已否决规则；不得覆盖用户新增的Twinkly/en字体或其它工作区改动，不整体回滚Prefab/I2或共享流程。

### DOTween飞杯替换（2026-09-15历史证据，已按用户要求撤销）

- 用户批准把手写逐帧飞杯改为DOTween。只修改两份本关C#及功能/风险文档，Prefab、I2、共享组件、资源、队伍左移与点单规则保持。
- 复用仓库Level71的DOVirtual.Float路径补间模式，但不使用独立播放/OnComplete结算；本关Tween时长取既有flightDuration=0.35，以Ease.Linear保持原抛物线，由服务时钟Goto定位。暂停及玩家区内起跳不推进Tween，终点仍取当前右手；NPC失败后照常领餐的规则不变。
- CupBinding拥有Tween；到手先Kill(false)再挂手，Hide先Kill(false)再隐藏，Dispose复用Hide，现有CancelFlight/Recycle/OnDisable/OnDestroy及Ctrl的Reset/Cleanup均沿此路径清理。SetLink绑定杯对象的销毁，取消不强制补完，不发胜利；没有新增独立异步关闭或完成回调。
- 验收边界为DOTween创建/服务时钟推进/清理的静态调用链及原参数保持。实际DOTween首帧、右手动态跟随、暂停恢复和销毁时序仍为uncertain；不编译、不启动Unity、不运行测试或探针，扩大验证范围N/A。
- 验证结果：内联Python核对DOVirtual.Float、Pause/Goto、Kill(false)、无OnComplete结算与单一serviceElapsed，词法括号检查通过；Prefab与 `/tmp/dev138-dotween-before/Level_138.prefab` 字节一致。两份C#及成对文档执行 `git diff --no-index --check /tmp/dev138-dotween-before/<同名文件> <当前文件>` 无空白诊断。mermaid-cli执行 `src/cli.js -i docs/features/levels/138/Level138奶茶排队功能说明.md -o /tmp/dev138-dotween.svg`，16/16图成功；仅A1的方法签名变化，8个流程图方向仍为TD。这些结果不等于DOTween运行验证。
- 若另获批准撤回，只回退两份本关C#的补间字段/回调/时长传参及对应文档，不覆盖Prefab、I2条目、点单、装饰或奶茶尺寸。

### 2026-09-15 点单I2与队伍左移（原交付证据，气泡生命周期已更新）

- 当前验收：新增15句中文点单和1个英文coffee条目；第6句奇异果鲜奶、第15句榛果奶茶。NPC首次在队首服务时立即随机一句、固定1.5秒；初始者和补位者同规则，玩家不点单。服务取消/关店/离场清理，暂停冻结，玩家失败后NPC正常营业则继续。
- 实现边界：仅两份本关C#、本关Prefab、I2Languages的16个新增条目与所属文档；不修改HintBubble、Actor、BaseLevelCtrl、共享字体或材质，不改玩家出生点、StartDoor、配置排序与存档。复用132/083的Resources.Load与HintBubble.Show路径；传显示时长0禁止共享异步自动关闭，1.5秒由本关唯一服务时钟结束，清理沿项目即时隐藏/销毁惯例。
- 队首中心由旧(6.08,-2.0424674)变为(5.08,-2.0424674)，范围X=4.83～5.33，初始队尾中心X=-3.32；中间杯与飞杯源仍在(6.08,-1.17)。该变化已获用户明确批准，取代先前“对准中间杯”的要求。
- coffee使用同一TMP上的I2 Localize组件，key为`S_LevelTips_Des_level_138_16`、secondary=`-`；保留en字体、独立描边材质和所有原布局。其它15个key后缀1～15，点单只填zh-CN，coffee只填en，其它75槽留空；I2源既有Fallback策略不改。不宣称这些空语言已经翻译完成。
- 静态证据：内联Python/PyYAML对照 `/tmp/dev138-order-before/` 的改前快照，确认I2由873条增至889条、16句与已确认文本逐字一致、每条76个语言槽且仅指定语言有值；去掉本轮16条后与原资产逐字一致。Prefab由175增至176个唯一锚点、本地引用闭合；仅HeadZone的X、CoffeeText组件列表和新增Localize变化，源脚本GUID与安装包一致。首轮检查对组件列表漏写`component`包装层导致断言失败，更正检查后通过，未因此改动生产资产。
- 源码/文档证据：静态核对NPC服务入口、一次门禁、1～15随机范围、唯一serviceElapsed、1.5秒清理和暂停入口；两份C#词法括号检查通过。8个目标文件执行 `git diff --no-index --check /tmp/dev138-order-before/<同名文件> <当前文件>` 无增量空白诊断。本机mermaid-cli执行 `src/cli.js -i docs/features/levels/138/Level138奶茶排队功能说明.md -o /tmp/dev138-order.svg`，16/16图渲染成功；仅图2文字变化，8个流程图仍为TD。上述均非编译或Unity运行证据。
- 未验证：I2真实导入/缓存刷新、中文及QQNeNe混排和长句排版、咩/噗/榛等字形覆盖、气泡动画/暂停/语言切换、露出台上奶茶的最终画面和左移后真实碰撞/服务均为uncertain。未启动Unity、编译、测试或探针；扩大验证范围N/A。
- 回滚边界：只撤回本轮两个脚本中的点单字段/调用/清理、Prefab区域X与coffee的605组件/引用、16个新增I2条目，并同步当前要求文档；需另获用户批准才能恢复旧规则。不撤回装饰组合、0.75奶茶缩放、已有字体/材质、店铺摆位或配置修复，也不整体覆盖I2Languages资产。

### 2026-09-15 Box互异装饰与奶茶1.5倍（原交付证据，现改为单件独占）

- 验收声明：初始队伍、在途补位者、离场未回收者统一普通Box，每人非空随机装饰且完整组合不重复；生成后不变，回收后才复用；玩家/NPC手持及飞行奶茶由0.5放大至0.75，店铺摆件不变。
- 源码证据：`TryChooseAppearance` 同时收集queue/inflight/leaving的组合ID，在未占用集合等概率抽样；两类生成入口共用SpawnActor。激活前挂装饰，失败回收并停止；池耗尽返回空，补位后续帧重试。成员转离场不释放，Recycle后移出表，Reset/Cleanup沿原路径统一回收，无独立异步任务或存档接入。CupBinding创建和重新挂手均使用同一CupScale=0.75常量。
- 静态资源检查：内联Python/PyYAML对比 `/tmp/dev138-appearance-before/Level_138.prefab`，175个锚点及本地引用仍闭合，唯一变化对象为总控组件，仅增加3组11个装饰Prefab引用；所有原Transform、SpriteRenderer和玩法参数不变。11个引用逐项解析到资源根GameObject，只有Transform/GameObject/SpriteRenderer，无附带输入、动画或存档脚本，11个Sprite GUID互异；Box.json包含box_original和point/point01/point02/Box_Center9s。组合容量8×3×2=48，脸部必选。C#词法括号静态检查通过，不是编译或行为测试。
- 文档证据：本机已安装的mermaid-cli执行 `src/cli.js -i docs/features/levels/138/Level138奶茶排队功能说明.md -o /tmp/dev138-appearance.svg`，16/16图渲染成功；本次仅架构A2的外观字段变化，流程图保持TD。SVG只证明文档图，不是Unity截图。
- 增量空白检查：两份C#、Prefab及成对文档执行 `git diff --no-index --check /tmp/dev138-appearance-before/<同名文件> <当前文件>` 均无诊断；Context/Map全文空白检查通过。完整工作区 `git diff --check` 的8处ProjectSettings尾空白在本轮修改前已存在，未处理。
- 未覆盖：运行时激活/骨骼帧序、翅膀及衣服的实际遮挡、48种组合在动作中的辨识度、放大奶茶的观感、回收到补位的真实帧序均为uncertain。未启动Unity、编译、测试或探针，扩大验证范围N/A。
- 选择性撤销边界：仅本关两份C#的组合分配/挂饰/杯缩放及Prefab总控的三个装饰数组；同步所属文档。不得改回用户已否决的四皮规则，除非另获批准；不修改公共装饰资源、Actor、店铺摆位、排序或用户其它工作区改动。

### 2026-09-14 人工 Play 黑屏的启动场景排查

- 验收声明：修复 dev138 空启动场景缓存，让下次人工打开工程时恢复正式 Loading 入口；不把它扩大为 138 玩法运行已通过。
- 现场：`HEAD=b11060216e1fd201b2cbf04542721f8a255185cb`，Unity 日志版本 `6000.0.59f2`；本关脚本/Prefab 尚未跟踪，工作区保留用户字体、asmdef、ProjectSettings 和配置改动。用户已把 `tblevelsort` 的85临时借位为10138，不是正式投放。
- 证据：检查时上一份 `Editor-prev.log` 的工程路径为 dev138，记录加载 `Temp/__Backupscenes/0.backup`，未见 Loading/GameManager/PlayCtrl/Level138 业务执行记录；`Library/LastSceneManagerSetup.txt` 内容为 `sceneSetups: []`。这些证据指向本次 Play 未经过正式启动场景，不足以归因为138相机或玩法异常，也不能推断空缓存最初由谁产生。
- 修复：确认 dev138 Editor 已关闭（当时另一个 Unity 进程属于 dev137，未操作），仅把上述缓存写为 `Assets/Game/Scenes/Loading.unity`、`isLoaded=1`、`isActive=1`、`isSubScene=0`；格式与仓库既有场景恢复持久化格式一致。未调用恢复探针、未启动 Unity、未编辑场景资产、Prefab、运行时代码、排序或存档。
- 命令与结果：`rg` 定向检索日志、`ps -axo args=` 核对工程进程、读取启动缓存及 `EditorBuildSettings.asset` 确认正式入口；原空缓存与日志分别暂存 `/tmp/dev138-black-screen-last-scene-before-20260914.txt`、`/tmp/dev138-black-screen-editor-prev-20260914.log`（本机临时证据，不随仓库交付）。修复后以 YAML 解析核对场景条目并用 SHA-256 比较两份场景、本关Prefab与排序文件。
- 边界：缓存静态修复不证明 Unity 已重新打开 Loading，也不证明画面或138交互已恢复；仍需用户在 dev138 工程人工打开 Loading 后确认。扩大验证范围：N/A。

### 制作阶段静态证据

以下是制作阶段的证据范围；随后人工启动暴露的整表重复键问题见下一节，不能以138条目自身唯一推断整表可以初始化。

- 命令：`git status --short`、定向 `rg`、内联 Python/PyYAML 解析与制作前 Prefab 快照对比。结果：170 个锚点唯一；全部本地 fileID 闭合；新增 GUID 与嵌套源对象 ID 可解析；13 项总控资源/组件引用均序列化。URP 默认 Sprite 材质来自当前 PackageCache，不属于新增自制材质。
- 几何核对：地面碰撞顶面 `-2.57640745`，Actor 主 PolygonCollider2D 半高 `0.53394005`，固定区域中心 `(6.08,-2.0424674)`、尺寸 `0.5×1.2`。这是 YAML 算术，不是角色落地或领餐成功证据。
- 保护边界：制作前快照与当前 Prefab 的六个旧对象差异仅涉及根组件/子列表、Coffee子列表、帘子组件表、Base成功动画和旧门实例；全部既有 SpriteRenderer 字节及旧 Transform 位姿/缩放保持。没有用 git reset/clean 或整份历史 Prefab 覆盖人工摆位。
- 旧门入口：`successAnim` 清空、门 inactive、旧触发组件禁用且成功方法/目标清空；成功源代码路径为挂杯 → Won → `BaseLevelCtrl.SuccessStrightly()`。FallArea源脚本检查Player标签，NPC模板根已改Untagged。
- 内联 Python 对三份新 C# 做去注释/字符串后的括号配对、尾空白和 `.meta` GUID 格式检查，结果通过；这只是词法/文件检查，不是 C# 编译。两份功能/风险文档与 Context 的代码围栏/尾空白检查通过，16 个 Mermaid 代码块与制作前逐字相同，全部流程图仍为竖向。
- `git diff --no-index --check /dev/null <新文件>` 与 `git diff --no-index --check /tmp/dev138-before-gameplay.prefab <当前Prefab>` 无空白诊断；no-index 返回1表示存在文件差异，包装检查同时核对诊断为空，未把它当成编译或运行结果。完整工作区 `git diff --check` 返回2，八处 ProjectSettings 尾空白来自任务前状态，未处理。
- 验证不启动 Unity、不编译、不运行测试或探针；扩大验证范围：N/A。以上结果不能扩大为玩法、物理、手势、动画、标准胜利弹窗或性能已经验收。

### 2026-09-14 Loading 持续等待：主线配置重复键

- 验收声明：去除 `Assets/Game/Configs/tbleveldata.json` 中导致配置构造异常的完全相同重复记录，保留全部唯一关卡内容和 `85 -> 10138` 人工借位；不修改共享加载流程或吞掉异常。
- 直接日志证据：本次 `Editor.log` 的工程路径为 dev138。17份配置加载完成后，`TbLevelData.cs:32` 的 `_dataMap.Add(_v.Id, _v)` 抛出 `ArgumentException: An item with the same key has already been added. Key: 10125`；调用链为 `Tables` 构造 → `ConfigManager.LoadConfigTablesCoroutine` 第157行。后者未能执行完成回调，`LoadManager` 第233行起的 `while (isLoading)` 持续等待。日志末尾 `UIManager.DestroyAllUI` 的空引用发生于 `OnApplicationQuit`，不是本次持续加载的首个阻塞点，未借机修改共享 UI。
- 配置事实：145条中有8组重复 ID：10125、10127、10129、10130、10131、10132、10133、10134，每组两条所有字段完全一致；这些重复在任务前 HEAD 中也存在。修复只删除文件尾部8条冗余副本，剩余137条保持原顺序和全部字段，包含唯一的10138注册。
- 验证结果：内联 Python 确认137条 ID 全部唯一、去重前后唯一记录集合/内容及保留顺序一致；按 `Tables.cs` 的17个加载文件及对应生成代码的主键类型检查，17/17主键唯一；SHA-256 对照确认排序、138 Prefab、ConfigManager和LoadManager未改，目标 `git diff --check -- BrainPunk/Assets/Game/Configs/tbleveldata.json` 通过。日志及改前JSON暂存 `/tmp/dev138-loading-editor-20260914.log` 和 `/tmp/dev138-loading-before-tbleveldata-20260914.json`，仅为本机临时证据。
- 范围：这是启动配置数据修复，不是138玩法改动；未改Excel、生成C#、加载回调或存档。修复前异常有日志证据，修复后只有静态数据证据，需退出旧 Play 后重新进入，才能重新创建配置缓存；未主动启动Unity、编译或运行验证。共享加载器在其他配置错误下仍可能等待，属于未在本次改变的边界。

### 2026-09-14 关卡资源加载器缺失：Standalone 未启用 YooAsset

- 验收声明：补齐本机 Standalone 的 YooAsset 条件编译开关，恢复既有加载器的编译入口；不以移动138资源、改加载器或修改打包清单绕过错误。
- 证据：用户堆栈与 dev138 Editor.log 均为 `No loader can load asset. path:Assets/Game/Prefabs/Level/Level_138.prefab, type:GameObject`；资源初始化日志只注册了 `ResourcesLoader`。`ResourceSystemInitializer.cs` 第24行以及 `YooAssetLoader.cs` 顶部由 `YOOASSET || YOOASSET_ENABLED` 控制，当前 Standalone 宏列表缺少两者；本机现有 `com.bear.resourcesystem.rsp` 包含 `UNITY_STANDALONE_OSX`、`UNITASK` 和 YooAsset 程序集引用，却没有 YooAsset 启用宏。
- 排除清单缺失：`Bundles/StandaloneOSX/GamePackage/Simulate/ManifestPackage_GamePackage_Simulate.json` 的 `AssetList` 已有唯一的完整138路径，BundleID为138；`AssetBundleCollectorSetting.asset` 的 GamePackage 正常收集 `Assets/Game/Prefabs/Level`。配置文件此前可直接经 YooAsset 包加载，不代表 Bear 适配器已编入并注册。
- 最小修复：只在 `ProjectSettings.asset` 的 `scriptingDefineSymbols.Standalone` 末尾追加 `YOOASSET`，不改其他宏、平台、用户已有图标设置或 asmdef。`UNITASK` 已通过用户现有 `com.guru.unikit.unitask` versionDefines 生效，不额外扩大全局宏修改。
- 静态验证：对比改前快照，仅Standalone一行增加一个宏；Prefab、资源收集设置、资源系统asmdef、初始化代码及85借位排序的SHA-256保持；增量空白检查不处理用户既有ProjectSettings尾空格。本机改前设置、编译响应文件及日志分别暂存 `/tmp/dev138-yooasset-projectsettings-before-20260914.asset`、`/tmp/dev138-yooasset-resource-rsp-before-20260914.rsp`、`/tmp/dev138-yooasset-loader-editor-20260914.log`。
- 边界：需要用户退出旧Play，等待Unity导入设置并完成脚本重新编译，再从Loading进入。宏修改后的实际编译、加载器注册与138实例化尚未取得证据；没有主动启动Unity、编译、构建或运行探针。没有修改共享运行时代码。

### 2026-09-14 用户确认的1.2间距与右手挂杯

- 当前要求替代旧0.55紧密重叠方案：Actor大小不变，玩家/NPC统一右手持杯，杯子大小不变。修改仅为 `Level138Ctrl.spacing` 默认值与Prefab的spacing同步至1.2，以及共用CupBinding改绑右手；功能说明图2、参数与Context同步更新。
- 静态结果：与改前快照对比，Prefab只有spacing一行变化，所有位姿、缩放、SpriteRenderer与其他绑定保持；总控也只有spacing默认值改变。初始8人、向前补位和在途补位目的地共用此字段，当前名义队尾中心X为-2.32。
- 两套现用Spine JSON均确认 `Box_Center9s` 的父骨骼为 `Soft_arm_R`，旧 `Box_Center6s` 属于 `Soft_arm_L`。CupBinding只复用同一角色Skeleton且已绑定右手的handBone，拒绝复用同名左手节点；无合格节点则创建自有右手挂点，退出时按原所有权清理。杯子局部零偏移、单位旋转、缩放0.5和order300均保持。
- 内联Python/PyYAML与定向源码检查通过：主碰撞体宽1.04531843，1.2槽间距留下约0.15468157名义间隙；这是主碰撞体静态关系，不证明四种皮肤、手臂动作或杯子像素边缘绝不交叠。队列变长后的补位接触节奏、右手持杯朝向和遮挡尚未运行验证。
- 仅图2的间距文字变化，其他15个Mermaid代码块不变，所有流程图保持TD；以本机 `@mermaid-js/mermaid-cli 11.17.0` 的 `src/cli.js -i - -o /tmp/dev138-queue-spacing-1_2.svg` 渲染受影响图成功。方向未改变，不新增Unity视觉截图；该SVG只证明文档图可渲染。
- 三个生产文件的 `git diff --no-index --check <改前快照> <当前文件>` 均无空白诊断；未启动Unity、编译、测试或探针。验收边界为参数、骨骼归属与代码接线，实际排队/持杯观感仍待人工确认。

### 奶茶交付飞行（原交付证据，杯尺寸已由2026-09-15规则更新）

- 用户已确认：店铺中间杯处起飞，0.35秒小抛物线、弧高0.35世界单位，杯子大小不变；玩家0.65秒起飞、1秒接杯后胜利，NPC2秒起飞、2.35秒接杯、3秒离场。飞行期间关店/离区/失去资格取消未到手杯，暂停/区内起跳随服务时钟冻结，不消耗店铺摆件。
- 所有权：CupBinding拥有同一个杯实例，飞行时临时置于本关根节点并刷新当前右手终点，进度满1才挂回右手并置IsHeld；不是在手部新生成第二个杯。Hide清飞行和领取状态并收回父节点；NPC禁用、销毁、Recycle和总控Reset/Cleanup均覆盖显式清理，避免Actor子树销毁后遗留飞行杯。关店Depart只取消飞行杯，已到手杯可带走；玩家Won清服务不隐藏奖励杯。
- 参数与接线检查通过：Prefab仍为170个唯一锚点、本地引用闭合；与改前快照相比仅根总控新增 `cupLaunchPoint`、`cupFlightDuration`、`cupArcHeight` 三项。起点引用现有 `Sp_l138_d_04 (1)` 的Transform `2219462274178768222`，世界位置(6.08,-1.17,0)，原杯保持active。所有Transform、SpriteRenderer、1.2排队间距、Actor/奶茶大小和FightAnimation的(1,1,1)缩放均未改。
- 源码检查通过：serviceElapsed只有一个累加位置，空中返回先于计时与飞行推进；没有独立飞行dt或异步完成回调；只有UpdateFlight可开启杯显示，进度满1且挂手后才写IsHeld；总控胜利入口同时检查服务时长与IsHeld。单一创建/飞行流程由玩家与NPC共用；资格取消隐藏玩家在途杯但不会吞掉Won手杯，NPC离场先CancelFlight再设置Leaving。
- 静态证据命令：内联Python/PyYAML对比 `/tmp/dev138-cup-flight-before.prefab`、两份改前C#快照与当前文件，核对新增字段、源对象、调用顺序、词法括号和取消入口；`git diff --no-index --check <改前快照> <当前文件>` 无空白诊断。数值关系仅为静态算术：1-0.35=0.65，2+0.35=2.35<3，不是计时运行测试。
- 文档验证：本机 `@mermaid-js/mermaid-cli 11.17.0` 执行 `src/cli.js -i docs/features/levels/138/Level138奶茶排队功能说明.md -o /tmp/dev138-cup-flight.svg`，16/16个图成功渲染，8个流程图仍为TD；不把SVG当作Unity画面证据。
- 未验证：飞行帧数、右手摆动/转向时的末端跟随、冻结后恢复的视觉连续性、飞行时关卡根order300到挂手后Actor SortingGroup的遮挡过渡，以及中途取消和退出的真实生命周期。未启动Unity、编译、测试或探针，扩大验证范围N/A。

### 原位置触发打斗、同时允许下落（当前实现）

- 用户再次澄清：到NPC上方原位置就应触发打斗，玩家同时可以继续落地。上一版“先落地再播放”是Agent的理解错误，已撤回对应等待阶段，不作为当前需求。
- 两个上方插队入口直接调用BeginFailure(true, HeadContact(...))，原0.6范围和NPC头顶特效位置恢复；同次调用立即播FightAnimation/dogFight并清零演出时钟。Fight分支恢复StopMoving清掉的向下速度，保持刚体模拟和重力，不传送；Knock分支才保持原0.1秒冻结。0.8秒从上方触发点开始计时，无IsTouching(ground)门禁或单独落地等待。
- 碰撞所有权：仅记录并忽略本关玩家/NPC Collider对，不修改层碰撞矩阵或地面碰撞。下落、打斗与击飞共用原值快照，暂停恢复重申忽略；Reset、OnDisable、OnDestroy均经RecycleQueue先还原原ignore值再回收NPC，包含原本已被忽略的true值，不强行统一还原false。
- 静态检查通过：以 `/tmp/dev138-fight-falling-before-ctrl.cs` 和 `/tmp/dev138-fight-falling-before.prefab` 为改前快照，内联Python确认两个上方入口直接启动FX/音效/计时，Fight恢复下落速度而Knock保留冻结，已无落地等待；碰撞原值恢复与暂停路径保留，Prefab字节未变，C#词法/增量空白检查无异常。mmdc 11.17.0输出 `/tmp/dev138-fight-falling-1.svg` 至 `-16.svg`，16/16图成功，8个流程图均为TD；不沿用上一版等待落地实现的证据，也不把静态检查当成运行结果。
- 未验证：上方特效与玩家自然下落的同时表现、顶面零速度接触、暂停恢复、成对碰撞恢复及0.8秒后的击飞衔接。未启动Unity、编译、测试或探针，仍需重新进入Play人工确认。

### coffee招牌（原静态装配证据，I2已由当前点单条目更新）

- 验收声明：按用户给出的 `/Users/greaterbay/Downloads/l138-v2/示意图.jpg` 添加小写coffee TMP，字色`#764115`、描边色`#F8F5F1`，以100 PPU标定90px字号和6px外描边；不改原美术、玩法、公共字体或字体材质。
- 实现：`Coffee/Sp_l138_d_01/CoffeeText` 为单个世界TMP、RectTransform/MeshRenderer/MeshFilter，局部(1.2,0.98,0)、缩放0.01、文字框300×110；使用en SDF、字号90和orthographicMultiplier=1，禁止自动字号/换行。独立材质 `Assets/Game/ArtSrc/Sprites/L138/CoffeeSignText.mat`，GUID `5addf60152a14175b1cad056f056af72`，只由该文字使用。
- 描边依据：当前TMP ShaderUtilities和SDF shader的参数关系，加上90采样字号/90显示字号，`(FaceDilate + OutlineWidth) * ScaleRatioA * GradientScale = 6` 名义图集像素；两者都取1/3保持字芯，0.01缩放对应0.06世界单位。不是把6直接写进TMP的0～1 OutlineWidth字段，也不宣称任意分辨率下严格6屏幕像素。
- 样式依据与边界：图示只注明尺寸和颜色，未给字体文件；沿用工程en字体。只读字体轮廓/advance数据可核对coffee的名义宽高及文字框余量，不能证明Unity最终字体渲染与示意图逐像素一致。
- 静态检查通过：内联Python/PyYAML确认Prefab由170增至175个唯一锚点、引用闭合，只有旧框架子列表增加一项，其余旧对象字节不变；TMP/材质/图集/Shader引用、字色8位值与名义6px描边换算一致。字体轮廓/advance估算含描边的coffee约281.121×78.885设计像素，落在300×110框内；这不是Unity排版证据。
- `en SDF.asset`、`en.ttf`、公共白描边材质的SHA-256与修改前相同；`git diff --no-index --check /tmp/dev138-coffee-sign-before.prefab <当前Prefab>` 及新材质/.meta全文检查无空白诊断。未启动Unity、编译、测试或探针；新增对象没有Collider，raycastTarget为false，不修改关卡标题占位和I2。Mermaid代码块未改，不新增渲染验证。

### 左上45度与补位入队

- 打斗方向改为(-0.5,0.5)，保留knockForce=50，按ActorCtrl直接乘方向的实现得到初速度(-25,25)，相对左水平线向上45度；敲击右上方向、重力、打斗0.8秒及玩家敲击停顿0.1秒未改。
- 命中后跑至队尾切idle的要求保留；此前只放行HasHit者的临时实现已被用户进一步明确的“整条NPC队伍照常运行”取代，不再作为当前冻结规则。新实现和验证以以下队伍流程解耦条目为准，45度方向不变。

### 玩家失败与完整NPC流程解耦（当前）

- 用户明确整条队伍在玩家打斗或已判定失败后仍照常拿奶茶、向前补位和向右离场，营业补员也继续。不是只保留命中者或已有杯的离场者；此前HasHit局部例外已撤销，通用重开不延后。
- 玩家与NPC门禁分离：CanInteract仅限Playing玩家，CanAdvanceQueue允许Playing/Fight/Knock/Flying/Failed的NPC流程，并统一排除暂停、resetPending、Inactive、Won和禁用状态。FixedUpdate持续推进全部三类NPC；LateUpdate遇到玩家IsDied不再直接return，而是锁玩家后继续服务与MaybeRefill。游戏FAILED单独处理，不与PAUSE混淆。
- 失败入口只调用CancelPlayerService，其内部仅在服务对象为玩家时执行取消；不清NPC的serviceElapsed、在途杯、手持杯或refilling。因此NPC即使在2秒飞杯中、或2.35秒已经接杯但未满3秒时遇到玩家失败，也继续原服务而非清杯、重计或提前发杯。以上为源码语义，不是运行场景验证。
- 失败期间已有及新生成的NPC都按本关快照忽略玩家原生碰撞，移动也不以玩家身体限步，避免被失败玩家挡住；正常暂停不会因此改变正常玩家碰撞。新的补位者仍串行报到、idle、参与3秒领餐，剩2补满8门槛不变。玩家服务资格额外要求CanInteract且未死亡，防止队伍继续后给失败玩家发杯或翻转胜利。
- 静态检查通过：内联Python核对完整队伍门禁、三个移动分支、单一服务时钟、NPC进度保留、补位守卫、新NPC碰撞登记、FAILED/PAUSE区分和Reset/Cleanup边界；C#词法及 `git diff --no-index --check /tmp/dev138-npc-flow-before-ctrl.cs <当前脚本>` 无异常。Prefab与 `/tmp/dev138-npc-flow-before.prefab` 字节一致，所有美术、位置、45度方向以外既有参数、coffee字样和奶茶飞行设置均未修改；45度源码同样保持本轮之前版本。
- 本机mmdc 11.17.0渲染 `/tmp/dev138-npc-flow-1.svg` 至 `-16.svg` 全部成功，8个流程图保持TD。未启动Unity、编译、测试或探针；实际连续领餐、3秒离场、补员、暂停恢复、新生碰撞及失败等待到重开的衔接仍需人工Play确认。

## 回滚

仅撤回三份本关脚本、对应 `.meta` 与本次 Prefab 装配对象（fileID 前缀 `138100000000000`），恢复六个既有对象中的本次接线差异并同步功能说明。保留两端标定、Coffee/Actor_Pink/奶茶摆位、新素材、既有10138登记以及任务前字体、asmdef、ProjectSettings改动；不恢复废弃旧桌玩法，不改共享资源或存档。

Loading修复只涉及主线配置末尾8条完全相同副本的移除，应与138玩法装配区分；不要为撤回138玩法而把导致启动异常的重复键一并恢复。

Standalone宏修复属于本机加载能力配置；若单独撤销，只移除本次追加的 `YOOASSET`，不要覆盖整份ProjectSettings或恢复用户的asmdef旧版本。撤销后本机将再次缺少该条件编译加载器，不应随138玩法回滚机械撤回。

飞杯交付是本轮获批的新规则；若用户另行批准撤销，只回退两份本关脚本的飞行/取消/领取门禁和Prefab新增三项，并同步需求与Context。不要回滚已确认的右手挂点、1.2间距、打斗缩放、店铺摆位，或前述加载修复。

本轮纠正仅修改Level138Ctrl及所属需求/风险/Context；撤销时不得把已经纠正的“等落地才打斗”误当成当前需求，也不要回滚Prefab、美术、飞杯或配置修复。

只撤回coffee招牌时，移除本次Prefab的600～604后缀五个对象及框架的601子引用、CoffeeSignText材质和.meta，并同步招牌说明；保留原店铺图片/变换、奶茶飞行、打斗和公共字体资产。

只撤回本轮击飞/补位调整时，仅回退Level138Ctrl的方向向量和命中收尾/冻结例外并同步规则；不回滚Prefab、公共Actor/重开代码、已确认的边打斗边下落或其他功能。

本轮队伍流程调整只涉及Level138Ctrl和所属文档；若另行批准撤销，仅回退队伍/玩家门禁分离、Failed状态和失败NPC服务/补员变化，不回滚45度方向、飞杯、coffee文字、Prefab或共享代码。不得未经确认恢复已经撤销的“失败停全队/只放行命中者”规则。
