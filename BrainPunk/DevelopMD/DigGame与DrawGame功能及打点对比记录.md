# DigGame 与 DrawGame 功能、数据打点对比及提交记录（2026-10-12）

## 2026-10-12 当前增量：最高星与待播星存档已对齐

- 功能提交 `b28c791a3795af7c83062bbcc2c57b5ffb544002`：独立 Dig LevelWaitingAnimStar／生成初始化与属性／旧档补空；在工厂前沿 Draw 保存最高星与正增量，失败恢复原条目；普通／Reward／DeepLink 读取后删除本关待播项并保存，RecordVictory 只保存通关解锁。原内存传参差异没有产品依据，此次修正；不改变 Draw 实现、其它玩法、广告、打点或 Prefab。
- 新版工作簿：[星级存档对齐版](D:/GitStore/BrinPunk/BrainPunk/outputs/01a12ebf-304e-7943-9156-675c0ca86421/DigGame_DrawGame_功能与打点对比_2026-10-12_星级存档对齐.xlsx)。原 DeepLink与返回键更新版被应用占用，保留原文件及用户改动；38 项中27代码／数据对齐、7差异、3未接入、1资源缺口。功能对比第31行已更新为对齐，第40行说明 Dig 存档消费；验收新增第23项、提交目录新增本轮28个路径。
- 验证：89 项实际方法／JSON／生成器断言，包括与 Draw 原写星及普通／DeepLink 消费同场景比较；33 个原方法／源码保持。Editor／Android宏编译0错误，iOS两处既有Apple引用缺失。完整 UI／Spine／原生 PlayerPrefs 及重启／双端真机仍 uncertain，不把模拟当实测。
- Excel只读复核2423个既有非空单元格，值／公式／无关批注及原字体／填充／对齐／边框／数字格式／保护／冻结／列宽／视图保持，缓存27/7/3/1且无错误单元格；28提交路径与Git一致。artifact-tool导出后仅恢复原 styles.xml、sheetViews和单元格style引用，新数据／公式／缓存／表／批注仍由artifact-tool编写；保存后重新导入并查看改动区渲染。来源：Dig待播星对齐_2026-10-12/Excel增量验证.json。
- 当前约定／风险／回滚见 DigGame功能说明.md 与 GameVictoryPanel_DeepLink功能与风险记录.md 顶部同日章节；消费在面板读取时发生，不等待动画完成，不承诺中断后自动补播。以下记录保留历史快照，旧“待播值持久化不同”不再作为当前结论。仅本地commit、不push；并行地图／关卡及打开的原Excel未混入提交。

## 2026-10-12 当前增量：Android Back / Escape 已接入

- 功能提交 `e40da2bad8cf4f433a44cd3fc606a5e8ebccfe2b`：活动Dig在原暂停门禁前响应Escape/Android Back，复用默认退出应用确认；选关/普通/TBC/非转场暂停允许，已有弹窗/Guide/转场/结算/Runtime锁结果/广告pending拒绝插队。确认Quit（Editor结束播放），No/Close保留原状态；没有新暂停、保存或返回选关流程。
- 最新工作簿：[DeepLink与返回键更新版](D:/GitStore/BrinPunk/BrainPunk/outputs/01a12ebf-304e-7943-9156-675c0ca86421/DigGame_DrawGame_功能与打点对比_2026-10-12_DeepLink与返回键更新.xlsx)；38项中26代码/数据对齐、8差异、3未接入、1资源缺口；返回键行补实际源码与Dig状态门禁，保留此前DeepLink更新，追加22场景两宏的验收证据及17个功能路径。
- 22个实际方法场景分别在Editor/Player宏通过，12个原Dig业务方法与原Tick统计/提示主体保持；Editor/Android宏0错误。输入/UI/Guide/Quit为模拟，不代表真实返回键/退出与平台链。iOS仍两处既有Apple引用缺失；提交外关卡与资源进度需按对应任务另查。
- 原有单元格、无关批注、字体/填充/对齐/边框/数字格式、冻结与筛选逐项只读核对；统计公式缓存26/8/3/1、无错误单元格；17功能路径与Git逐项一致，改动区域渲染已查看。验证为Dig返回键_2026-10-12/Excel增量验证.json。
- 模块职责、UI/状态/平台流、风险与回滚见CommonPopup_Quit防插队功能记录.md同日Dig扩展。以下两轮保留历史快照，不再用旧“返回键未接入”判断当前提交。其它模式/Popup/Prefab/打点/SDK未修改；仅本地提交，不push。

## 2026-10-12 当前增量：DeepLink专属胜利已接入

- 功能提交 `2d45cde47d96196d0e930aeac60b4feaa3b2f051`：有效Dig DeepLink首胜/复胜均使用GameVictoryPanel_DeepLink，注入Dig最高星/本次新增星；Replay保留来源，Home退出清理，Next首未通过（全通TBC）接受或降级后清来源。普通双面板与相邻Next继续原路径。
- 新版工作簿：[DeepLink更新版](D:/GitStore/BrinPunk/BrainPunk/outputs/01a12ebf-304e-7943-9156-675c0ca86421/DigGame_DrawGame_功能与打点对比_2026-10-12_DeepLink更新.xlsx)。原表正被应用占用（写入EBUSY），保留原始文件；新版覆盖原“专属胜利未接入”结论，38项中25项代码/数据对齐、8差异、4未接入、1资源缺口。新增专属22断言验收记录与17个功能提交路径；旧741e19f7c依据仍作为未改项的历史基线。
- 22实际方法隔离断言通过，14个原方法不变，Editor/Android当前源码宏0错误；不是生产UI、Spine、SDK与深链端到端验收。iOS仍两处既有Apple引用错误，29普通关资源缺口保留。
- Excel只读复核原有单元格/无关批注、字体/填充/对齐、冻结与筛选保持；统计公式缓存25/8/4/1，无错误单元格；新增17路径与Git逐项相符，代表区域PNG已查看。证据为DigDeepLink胜利_2026-10-12/Excel增量验证.json。
- 模块设计/数据与事件流/风险和回滚见Assets/Game/UI/GameVictory/GameVictoryPanel_DeepLink功能与风险记录.md同日Dig扩展。以下内容保留为首次741e19f7c交付快照，不能再据其“DeepLink未接入”描述判断当前版本。

## 需求与验收

用户要求提交本轮 Dig 修改，并以 Draw 当前已知功能及打点规则制作中文 Excel。对照对象包含 UI/资源、关卡生命周期、提示/墨水/解锁/TBC/深链、Luban与云控整条链及关卡统计，不能以单一函数或编译结果替代整体验收。

## 交付与维护边界

- 分支：feature/1.17.0_common；功能提交：`741e19f7c7e564786328933286483252d77b08c0`，基线：`c38451f789a25103d0f2eddda39940659776897e`。只作本地 commit，没有 push。
- 工作簿：[DigGame与DrawGame对照](D:/GitStore/BrinPunk/BrainPunk/outputs/01a12ebf-304e-7943-9156-675c0ca86421/DigGame_DrawGame_功能与打点对比_2026-10-12.xlsx)；5页：功能对比、打点对比、数据与云控、验收与缺口、提交目录。所有源码依据以该功能提交快照为准，完整相对路径保存在依据单元格批注。
- 38项功能中24项代码/数据对齐、8项明确差异、5项Draw专属未接入、1项资源缺口；这不是整体运行验收评分。另含23项打点、29项数据/云控记录（其中14项Global字段）、20个验收场景。
- 没有改写Draw既有关卡/控制器。金币/NoAds和纯表现组件按原边界复用，Dig存档/数据/选择状态独立。Packages/GuruSDK/GF只读。
- 必要烘焙依赖采用上一轮完成快照及配套地图资源。并行的Sand尺寸、世界正圆笔刷、Collider出生源/IsShow修复留在工作区；字体、首页布局、其它关卡、平台/工程设置及其它每日记录主题未并入提交。

## 关键对比事实

- 30普通配置已补，当前只有DigLevel_001与DigLevel_999资源；002～030共29个资源未制作。原表11列含标记/备注，与Draw对应；运行时导出8字段。
- 独立diggame_config的14字段通过ConfigManager.RemoteConfig API被实际消费者读取，远端优先、本地Luban兜底；排序同轮校验/落盘/应用、受阻延迟、独立候选与代际身份。LubanGenerator专用/统一入口、Guru游戏侧默认及生成代码纳入功能提交。ForceFetch、5/10秒预算、启动顺序按原设定。
- 四玩法累计为各自LevelSuccessCount.Values之和，包含同关重复成功。start/fail/exit level=累计+1；成功先加本次次数再end；success_N字段使用真实配置Id；实际SDK start_type=play/replay/continue。Dig无Continue入口，Reset按是否已通过决定play/replay。时长毫秒，历史Path时长与当前周期分开，Reset累加revive、Replay清当前周期revive。
- Draw首次引导、画笔商店、DailyChallenge、深链专属胜利面板、Escape/Android Back分支未接入；首页进度指标、首关提示、Intro、星级动画等待值、重排后的Next和IV结算顺序存在差异。具体源码/作用及待办逐项列于Excel。

## 验证与剩余风险

- 本轮提交快照Editor/Android宏全源编译0 error（113/118 warning）；生成器Editor源码编译0 error。51托管、27实际生成器合并、4220配置/表格/结构断言复跑通过；314外部GUID引用可解析。四个Excel统计公式缓存、五页冻结/筛选/批注及目录208路径与Git逐项相符，无错误单元格；代表区域PNG视觉复核已完成。
- 17 Editor/24 PlayMode烘焙检查是其它任务的已完成隔离记录，本轮没有复跑；旧75核心断言对应烘焙前版本，不作为当前全关验证。
- `uncertain`：生产UI/URP/横屏/输入遮罩、真实Firebase/IO/节流、广告失败/取消/无结果回调/晚回调、IAP/分享、实际后台打点、双端构建/移动性能。iOS宏仍有两处既有Guru.UniKit.Apple引用缺失，未报告iOS通过。缺失29关资源须另行制作。
- 证据：`DevelopMD/验证记录/Dig提交对比_2026-10-12/`与原两组Dig验证目录；权威打点约定为`DevelopMD/关卡打点生命周期约定.md`的2026-10-12当前有效扩展。

## 后续验收与回滚

按Excel验收矩阵逐入口检查正常、已断网、有网但服务不可用、缺商品/配置、失败、取消、关闭等待无结果、关闭重开和迟到回调；后台核对字段、顺序及重复上报；制作29关资源后再验收有效关卡全集。工作簿是提交快照记录，后续功能变化需更新版本及依据。功能与对比记录分别提交，可按提交差异选择撤回，保留其它未提交增量，不覆盖整个工作区或只读SDK/GF。
