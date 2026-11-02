# DigGame_GamePlayPanel 独立 HUD

## 2026-10-15 墨水来源修正

DigLevelCtrl现与Draw来源规则对齐：isConfig默认false使用Prefab容量／阈值，显式开启才接受DigLevelData。此前文档中的“普通HUD墨水与阈值改由DigLevelData注入”只适用于开关开启时。HUD代码未改，BindRuntime／SetInkState继续使用运行实例的InkState；奖励也按该实例基础容量增加。同关Reset／Replay由宿主恢复奖励资格，不能在HUD另读表覆盖显示。验证和风险见DigLevel目录《DigGame功能说明.md》当日主题；13项隔离验证包含InkChanged快照，正式HUD点击与真机未验证。

## 2026-10-13 当前版本：cd HUD已接入

前轮只补超时失败，cd_txt没有引用／通知链；本轮已接通。最后5秒显示5→4→3→2→1，整数变化才刷新；使用Draw原1.35倍／0.12秒OutQuad／0.18秒OutBack缩放脉冲。Prefab只新增手写countdownText字段绑定原TMP组件2084045529361983505，节点初始隐藏，字体、布局、层级和遮罩不改；Generated和生成器均未改。

数据流为DigLevelCountdownCtrl→本关CountdownSeconds／CountdownChanged→DigPlayCtrl→HUD.SetCountdownSeconds。0表示隐藏，控制器订阅后读当前快照、销毁旧关前退订；TBC永远0。胜负开始即清显示，不等待死亡／胜利延迟结束；手动Reset／Replay重建前、关卡／计时组件失活及HUD关闭／销毁也隐藏和Kill Tween，重开恢复原尺度。暂停／后台不扣时且保留数字；unscaled动画只在Unity PlayerLoop推进时可完成，不代替原生覆盖层恢复逻辑。

使用时仍在关卡Prefab挂DigLevelCountdownCtrl并绑定本关Level、配置Cd Time；HUD资源已绑定，无需给关卡脚本持有HUD或手工寻找cd_txt，不新增Draw／主线全局事件。超时启动和TryFail、死亡、统计／存档／广告／RC均保持前轮规则。

验证：独立Unity6000.0.59f2共90项通过，含前轮62项结果回归及28项实际TMP文本／真实DOTween／快照／去重／暂停／结果隐藏／关闭重开；13份测试源码与生产哈希一致，HUD Prefab剥去唯一新增绑定行即与快照字节一致。主程序集Editor／Android条件编译0错误；iOS仍有2处原有Apple引用错误。BaseUIView／广告／资源控制器为隔离桩，正式UI字体／安全区、平台广告、资源包、存档与双端真机仍uncertain。详见DevelopMD/验证记录/Dig倒计时HUD_2026-10-13/验证记录.md。

回滚：按本轮diff撤局部倒计时快照／事件、runtime接口及TBC实现、控制器转发、面板字段／Tween与Prefab一行绑定；保留前轮失败脚本及并行关卡、地图。首次修改前记录见下节，不把事前计划作为运行通过证据。

## 2026-10-13 倒计时HUD：修改前核对

- 需求／基线：用户追问Dig HUD的cd逻辑；前轮只提供DigLevelCountdownCtrl超时脚本，没有最后5秒显示。Prefab已经有初始inactive的cd_txt／TMP组件2084045529361983505，但手写／Generated字段均未持有它，DigPlayCtrl只转发InkChanged。补齐可观察的5→4→3→2→1和Draw同款数字脉冲，归零、成功／失败锁定、Reset／Replay、关闭／销毁须隐藏。
- 参考来源：DrawLevelCountdownCtrl.RefreshCountdownDisplay／StopCountdown、DrawGame_GamePlayPanel.SetCountdownVisible和同目录倒计时数字动画功能风险记录；Dig HUD说明、DigLevelCtrl／IDigLevelRuntime／TBC、DigPlayCtrl.BuildLevelAsync／DestroyRuntime／BeginRebuild、失败条件模块说明。已读Editor约束；CodeGraph不可调用，以当前源码／YAML补足。
- 维护边界：BaseUIViewEditor.GeneratePartialClassCode只输出扫描字段到.Generated.cs，BindComponents按生成字段名写序列化引用；新countdownText跟Draw一样放在手写partial，不改生成输出／模板或已有Generated字段。Prefab在已存在TMP fileID上显式绑定，不改节点名、层级、遮罩、字体、布局，不调用Find或组件扫描。生成器源码只读。
- 拟改：倒计时通过关卡局部CountdownSeconds／CountdownChanged(int)发布整数快照（0隐藏），每整数变化才通知；IDigLevelRuntime增加同一边界，TBC返回0／空订阅。DigPlayCtrl跟Ink相同绑定／退订、进关读快照、重建先隐藏，HUD新增手写TMP引用和显示／Tween方法。原计时启动、暂停、死亡0.5秒、统计／存档／广告、数据／RC／资源路径不变，无Draw／主线／全局事件或只读目录变更。
- 状态与影响：胜负锁立即清零，组件禁用／关卡失活清显示；暂停保留数字且不扣时，脉冲使用Draw原1.35倍、0.12+0.18秒的unscaled Tween，在正常PlayerLoop内可回原尺度，不假设原生广告停帧仍推进。关闭与重开清Tween／显示；HUD晚绑定读当前快照，销毁旧关前退订防旧通知写新HUD。
- 验证计划：Unity隔离真实TMP／DOTween＋Dig runtime测最后5秒阈值、整数去重、零秒／暂停／后台、胜负／Reset重建、关闭重开／Tween归位和旧源退订；再复跑前轮62项结果测试。Editor／Android条件编译、Prefab绑定／其它YAML块字节检查，实际字体视觉、安全区、原生广告、包加载／存档及双端真机仍uncertain。回滚按本轮diff撤快照链／字段绑定和面板Tween，保留前轮三个失败条件及并行关卡编辑。

## 2026-10-12 第二轮更新

普通HUD墨水与阈值改由DigLevelData注入（20、0/4/10），提示金币/跳关价格使用独立GetDigGame API；缺字段或解析失败回退Dig Global。新尝试开放时读取失败/等待阈值，已有提示不再提示warning；暂停/广告/转场云控延后提交。失败插屏在新关卡/HUD全黑准备后请求，回调后揭幕；胜利统计在UI开放前收尾。三项原生配置边界和51项托管配置/统计检查通过，生产HUD与平台广告输入恢复仍uncertain。文件、资源/UI/事件流及回滚见Dig选关与云控功能及风险记录，本节更新此前沿用Draw价格API的说明。


## 2026-10-12 当前版本：从 Draw HUD 复制

- 用户明确替换下方首版“主线副本/只开放 Reset 与 Pause”的限制。本次脚本移到 `GamePlayPanel/Scripts`，资源移到 `GamePlayPanel/Resources`，保留原 GUID；面板改为 `BaseUIView`，Generated 字段和显式序列化绑定取自相同命名层级的 Draw 副本。原主线与 Draw 资源/脚本不改。
- 开放墨水、Reset、Pause、Shop、金币提示与广告双倍；副本初始隐藏 PencilShop 和倒计时。墨水条复用纯显示 `DrawGame_InkProcessHandle`，没有 Draw 全局事件订阅；TBC 隐藏墨水、广告和提示。引用均按副本 fileID 显式绑定，不新增节点名扫描。
- 控制器持有本关广告/提示资格，同关 Reset/失败重试/Replay 恢复，切关退出清除。广告前暂停物理并结束笔画，HUD 保持点击遮罩；有效回调才增加一份基础容量并 Resume。HUD 打开版本、当前控制器和关卡版本均需匹配；关闭取消黑幕等待、释放表现和当前广告暂停。
- 提示及第二次金币跳关使用独立 `TipsPopup/Resources`，沿用 Draw 两项价格 getter、Bag/无限金币/CoinGiftPopup。弹窗匹配关卡版本后才执行；提示路径为首关显式绑定的 DigHintPath。没有提示广告（现行 Draw 提示使用金币）。
- 49 项资源静态检查及 Editor/Android 条件编译通过；75 项 Unity 检查覆盖核心关卡输入/提示对象，33 项托管检查覆盖独立进度/协议。完整新 HUD、胜利星组、原生广告失败/取消/晚回调、移动端安全区与横屏仍 `uncertain`，不是 UI 完整实测。
- 维护边界、逐场景清单与回滚见 `Scripts/Level/DigLevel/DigGame功能说明.md` 的 2026-10-12 章节及 `DevelopMD/验证记录/DigGame_2026-10-12/验证记录.md`。

## 需求与基线（2026-09-29）

- 用户要求：原 GamePlayPanel 脚本不增加 Dig 逻辑；复制面板 Prefab 并创建继承子类。新面板隐藏移动及跳跃按钮。首版只保留关卡号、Reset、Pause，Tips/Shop 继续隐藏。
- 调整前：上轮使用共享 `GamePlayPanel.cs` 的 Dig 分支、`GamePlayPanel.PostcardStar.cs` 的事件过滤及 `GamePlayPanel.Dig.cs` partial。
- 修改前核对已写入 `Assets/Game/Scripts/Level/DigLevel/DigGame功能说明.md` 文首。本轮替换上述设计，不再沿用原计划中直接改共享面板的实现方式。

## 最终文件与绑定

- 新脚本：`Scripts/DigGame_GamePlayPanel.cs`，继承 GamePlayPanel；没有新增父类成员，也没有生成重复字段。
- 新资源：`Resources/DigGame_GamePlayPanel.prefab`，复制自 `UI/GamePanel/Prefabs/GamePlayPanel.prefab`，独立 GUID，不是原件或动态修改原件。
- `DigPlayCtrl._hud` 类型及创建入口均改用子类；仍由现有 UIManager 的 Normal 层管理，按独立 Resources 名称加载。
- 恢复原 `GamePlayPanel.cs`、`GamePlayPanel.PostcardStar.cs`；删除本任务上轮新增的 `GamePlayPanel.Dig.cs` 和 meta，其功能由新子类承接。原 `GamePlayPanel.Generated.cs` 和原 Prefab 不改。

| 子类引用 | 明确绑定的原副本 fileID | 用途 |
|---|---|---|
| digResetButton | 7675669623049427876 | 派发 DigGameResetEvent(Manually) |
| digPauseButton | 6584363646932030180 | 当前 IPlayPauseHost.Pause 与设置弹窗 |
| digBlackMask | 7473071453443217776 | 本关重建黑幕 |
| digClickBlock | 5323227841874266840 | 输入遮罩 |
| 继承 LevelTxt | 4717331680754514819 | 复用父类 SetData 显示关卡号 |

黑幕两个时长均保留原 Prefab 的 0.2 秒。绑定由既有序列化 fileID 映射完成，无运行时或编辑器节点名扫描。

- 副本的 LeftMoveBtn、RightMoveBtn、JumpBtn、TipsBtn、ShopBtn 对应 GameObject 均为 `m_IsActive=0`，星星 PropBlock 根也设为不活动。隐藏发生在 Prefab 初始状态，不靠进场一帧后隐藏。
- 副本相对原 Prefab 仅变更 8 个 YAML 对象：根名称、面板脚本/新绑定、5 个按钮活动值、星星嵌套实例根活动 override。原层级、按钮位置、遮罩尺寸和资源引用保留。

## 生命周期与事件边界

1. 父类 OnCreate 会绑定主线按钮、OnOpen 会订阅主线 Reset/Replay/Resume/星星事件；字段为生成器生成的 private，故子类覆写 OnCreate/OnOpen/OnClose，不调用其主线实现。
2. 已核对 BaseUIView：OnCreate 为空，OnOpen 仅设 `_isOpen`，OnClose 仅清 `_isOpen/_isVisible`。子类保留相同状态赋值；Awake、OnShow、OnHide、SetLayer、OnDestroyView 继续继承原 Bear.UI 契约。父类将来变更基础契约时需复核，不声称可以无条件兼容未来修改。
3. 子类只绑定自己的 Reset/Pause，私有 Update/OnDestroy 屏蔽父类对应 Unity 消息。没有主线事件订阅、移动/跳跃派发或星星/引导清理。
4. ConfigureForDig 注入关卡号与控制器实时门禁。关闭门禁同步设置按钮 Interactable 和遮罩；点击再查实时结果锁，防止物理死亡后、控制器下一 Tick 前误操作。
5. Reset 点击先锁本面板，再派发一次 Dig 事件；Pause 先锁，再暂停当前宿主并打开既有 GameSettingPopup。不增加全局事件类型，也不写存档或正式关卡统计。
6. 每次打开拥有独立取消令牌；关闭和销毁取消旧黑幕等待并 Kill Tween。新实例/重新打开后的遮罩不被旧异步收尾隐藏。

## 验证与剩余风险

- `git diff --exit-code -- Assets/Game/UI/GamePanel`：0；原 GamePanel 脚本、生成字段及资源相对原版本无本次差异。
- Unity Roslyn 全游戏源码 Editor/Android 条件编译均 0 错误，113/116 警告；无新增 Dig 文件警告。不是主工程 AssetDatabase 刷新或移动包构建。
- Prefab 静态核验：上述 8 个变更对象符合范围；新增字段均指向正确类型对象；移动、跳跃等 5 个按钮初始隐藏。Dig 资产检查扩大到 9 个资源、131 个外部 GUID，引用闭合。
- **18 项隔离 Unity PlayMode HUD 断言通过**：真实 Dig 子类、BaseUIView、UniTask、DOTween；检查状态、父类隔离、输入门禁、Reset 去重、Pause、遮黑/揭幕、关闭重开/直接销毁取消。父类主线逻辑、按钮点击、事件派发器、设置弹窗和暂停宿主为替身；不是生产 Prefab 的完整点击实测。脚本与证据见 `DevelopMD/验证记录/DigGame_2026-09-29/验证记录.md` 的 HUD 修订章节。
- 隔离测试第一次因临时工程未引用 AudioModule 而编译失败；只给临时工程补齐 Unity 音频模块后重跑通过，未改主工程 Packages。
- `uncertain`：原主工程中资源加载、实际遮罩层级/安全区/字体/按钮反馈、从暂停菜单退出重进、主线/Draw/IWanner 回归，以及 Android/iOS 横屏。首次 PlayMode 应验证 Dig 仅出现关卡号/Reset/Pause，再进入其它模式确认原 HUD 不变。

## 回滚

若需要撤销独立 HUD，先让 Dig 入口不可进入，再移除新 Prefab/子类与控制器引用；不要恢复已被用户否定的共享 Dig partial 或重新修改父类。其余 Dig 数值、地形、存档、胜利面板不属于本次 HUD 修订的回滚范围。
