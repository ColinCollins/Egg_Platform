# Level091 敲门失败与暂停页门铃风险评估

更新日期：2026-07-29

## 原有行为基线

Level004 的 `KnockKnock` 在限定时间内累计敲门次数，达到次数后直接启用出口 Collider 并开门。共享暂停页的第一个 `Sfx_toggle` 用于切换音效。

Level091 有意改变这两条行为：连续敲门改为失败演出，暂停页音效按钮改为只在本关生效的门铃成功入口。实现保持在 091 私有控制器、HUD 变体和 Prefab 内，没有修改原关卡或共享系统。

成功结算配置原已引用 `S_congraktTips_Des_level_91`，但 `I2Languages.asset` 中原先缺少该 Term。新增 Term 只补齐既有引用，不修改公共胜利页。

`2026-07-29` 跟进调整 Level091 私有门铃定位和黑箱命中后的退场。用户先确认旧根偏移 `{110,-55}`，随后又要求整组右移，因此最终使用 `{130,-55}`；图片视觉倍率最终为 `1.5`，文字局部偏移仍为 `{-55,0}`，idle 流程不变。Level091 玩法逻辑没有修改共享 Actor 或其它关卡；同轮另有一项独立的共享设置音效图标替换，只改 `VolumeSettingContent.prefab` 的一行 Sprite 引用。

## 受影响文件、资源与系统

- `Assets/Game/Prefabs/Level/Level_091.prefab`
- `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_091.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_91/Level91Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_91/Level91ActorCtrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_91/Level91GamePlayPanel.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_91/Level91BoxBadActor.prefab`
- `Assets/Game/ArtSrc/Sprites/L91/Sp_l91_dd_01.png`
- `Assets/Game/ArtSrc/Sprites/L91/Sp_l91_dd_02.png`
- `Assets/Game/UI/SettingPopup/Sprites/UI_setting_icon_voise2_01.png` 及其 `.meta`
- `Assets/Game/UI/Global/Prefabs/VolumeSettingContent.prefab` 的一行音效图标 Sprite 引用
- `Assets/Resources/I2Languages.asset` 中唯一新增的 `S_congraktTips_Des_level_91`
- Level091 本地文档与共享功能记录
- 当前工作区已有但本次文档未修改：`tbleveldata.json`、`tblevelsort.json` 中的 `10091`
- 仅复用且未修改：Level004、`KnockKnock`、Level046、`Level46EnemyCtrl`、共享 Actor、两个设置 Popup 的结构与逻辑、共享 HUD

## 风险清单

| 风险 | 处理方式与静态证据 | 验证状态 |
| --- | --- | --- |
| 091 仍执行 Level004 的“三次敲门直接开门” | `Level_091.prefab` 只挂本关 `Level91Ctrl`，未找到旧 `KnockKnock` GUID；三次敲门进入 `BeginAngrySequence()`，出口 Trigger 保持关闭 | 静态与失败流程 PlayMode 通过 |
| Level004 或共享敲门逻辑被连带修改 | 091 使用私有控制器；本实现不要求修改 `Level_004.prefab` 或 `KnockKnock.cs` | 范围静态确认；004 回归待验证 |
| 暂停页音效按钮被置灰 | 当前 Toggle 始终设置 `interactable=true`；失败期通过 `Toggle.enabled=false` 拦截，不进入 Selectable Disabled 状态 | 当前正常流程 PlayMode 通过；其它皮肤待验证 |
| 点击门铃会自动关闭暂停页 | `PlayDingDongAsync` 和 `CompleteDoorbell` 不调用 `CloseUI`；设置弹窗只在整个 `GamePlayPanel_091.OnClose()` 清理时关闭 | 静态与正常流程 PlayMode 通过 |
| 暂停时 `timeScale=0` 导致 Ding Dong 不播放 | DOTween Sequence 使用 `SetUpdate(true)`，缺图兜底等待也使用非缩放时间 | 正常流程 PlayMode 通过 |
| 门铃动画仍显得僵硬或传播方向不明显 | 按钮采用压下、过冲、回弹；放射线在 `0.04s`、文字在 `0.16s` 错峰从左侧移动、缩放和渐显，结束继续向右漂移并渐隐 | 参数静态确认；策划视觉验收待完成 |
| Ding Dong 根偏离用户最新标注位置 | 动态根仍以 Sfx Toggle 视觉中心为基础锚点；用户确认旧值 `{110,-55}` 后又要求整体右移，最终 `dingDongOffsetFromSfx={130,-55}`，策划可在 Inspector 调 X/Y | 最终参数静态确认；`{130,-55}` 待视觉验收 |
| Ding Dong 图片大小仍不合适 | 唯一 `dingDongVisualScale=1.5` 只乘 Rays/Text `sizeDelta`，不缩放动态根，因此不会改变根偏移 | 静态顺序通过；`1.5` 最终视觉待用户验收 |
| Ding Dong 文字仍偏右 | 新增唯一 `dingDongTextLocalOffset={-55,0}`，仅作用于文字；基础 TextTarget X=`160`，当前有效 X=`105`，Inspector 可继续调整 | 静态数值和顺序通过；X=`105` 可能继续微调 |
| 门铃定位调整误改共享暂停页 | 三个定位字段只存在于 `Level91GamePlayPanel` 与 `GamePlayPanel_091.prefab`，动态对象仍挂当前弹窗实例，未修改共享 Popup 行为；`VolumeSettingContent.prefab` 仅有下一项另行记录的一行音效图标 Sprite 替换 | 限定范围与字段唯一性静态通过 |
| 共享音效图标替换范围不完整或误改其它 UI | `VolumeSettingContent.prefab` 只改一行 Sprite GUID；`GameSettingPopup` 与 `GameSettingPopup2` 继承该内容且无对应 override，因此有意同时覆盖游戏内外设置，不改 RectTransform、Toggle、事件或 Popup 逻辑 | 限定 diff 与继承关系静态通过；两个 Popup 待视觉验收 |
| 新音效图丢失、尺寸或导入语义改变布局 | 新旧图片均为 `106 x 104`；新 Meta GUID 为 `f240a8b03ae84ada8c57dc035e3084a7`，TextureImporter 语义与旧图一致；SHA256 为 `44D71B25D3779C54A1A9AA44A9C04A44B2ADCF7CEFAA7E5A780D993FACA0D512` | 资源和 Meta 静态通过；Toggle 开关态待 PlayMode |
| 共享图标无法局部回滚 | 旧图和旧 Meta 保留，旧 GUID 为 `7476e53ae4e2015459ee7ec10a992060`；回滚只需恢复 Prefab 一行 Sprite 引用 | 静态回滚路径存在 |
| 门铃实际上关闭了音效或写入全局设置 | 当前 Toggle 的运行时监听被本关替换，每次点击 `SetIsOnWithoutNotify(false)`；Level91 脚本不访问 DB、Save 或音量 API | 源码静态通过；下一关音效状态回归待验证 |
| 弹窗池复用后残留 Level091 门铃监听 | HUD 关闭时移除本关监听、恢复 Toggle 组件与缩放并关闭当前弹窗；普通弹窗预期在自身启用时恢复共享绑定 | 清理路径静态存在；跨关卡复用待验证 |
| 快速重复点击并发创建特效或重复完成开门 | 单次动画期间由 `doorbellInProgress` 拦截；`Ready` 首次点击令 `shouldOpenDoor=true`，`DoorOpen` 后续点击只播放反馈且令其为 false；`CompleteDoorbell()` 也只接受 `DoorbellRinging` | DoorOpen 重复反馈且不重复生效已通过 PlayMode；动画中极限连点仍待验证 |
| 玩家手动关闭暂停页后关卡卡在 `DoorbellRinging` | 动态对象销毁会 Kill Tween；取消路径在关卡仍存活、版本仍匹配时尝试 `CompleteDoorbell()` | 源码有兜底；关闭时序待 PlayMode 验证 |
| 图片或弹窗引用缺失造成无法过关 | 两张 Sprite 都缺失时非缩放等待 `0.1s` 后继续；动画异常也尝试完成。弹窗或 `Sfx_toggle` 完全找不到时无法触发成功，仍需资源绑定验收 | Sprite 兜底静态通过；弹窗缺失风险未消除 |
| 失败演出中仍可打开暂停页或按门铃 | 失败入口调用 `SetHudInputBlocked(true)`，同时阻塞 HUD 开关和专用面板输入；只有 `Ready` 能取得首次开门所有权，`DoorOpen` 的重复反馈不适用于失败状态 | 当前失败流程 PlayMode 通过；同帧输入竞争待验证 |
| 暂停状态检查在退出边界产生 `NullReferenceException` | 已移除 `Update -> PlayCtrl.CheckState()`；改为 `GamePauseEvent / GameResumeEvent` 更新本地 `gameplayPaused` | 编译与重置 PlayMode 无异常；退出边界仍待验证 |
| Resume 瞬间恢复 Actor 操作，破坏失败或门铃锁定 | 锁定状态收到 Resume 后立即并在 `LastPostLateUpdate` 再执行一次 `LockActorControl()` | 源码静态通过；恢复帧待验证 |
| 门未先开或黑箱出现过早 | `OpenExitDoor(false)` 先执行，再等待 `doorOpenLeadDelay=0.5s` 后激活场景预放黑箱 | 参数静态确认；失败流程 PlayMode 通过 |
| angry 紧接走出，停顿期间仍保持 walk | 走出 Tween 完成后立即循环播放 `idle`，再等待 `preAngryDelay=0.5s`，随后播放一次 `angry` 并等待完整结束 | 静态顺序断言通过；idle 混合与节奏待 PlayMode |
| 新增 idle 或既有黑箱资源名无效 | 结构化解析 `Box.json` 已确认 `idle / walk / angry / walk01` 与 `box_bad` 均存在；`Box_SkeletonData.asset` 的 `defaultMix=0.2` | 资源静态通过；实际混合观感待 PlayMode |
| Unity 保存策划位置后 `boxBadActor/impactVfx` 变为 None | 当前 Prefab YAML 两字段均为 `{fileID: 0}`；`ResolveReferences()` 使用 `GetComponentInChildren<Level91ActorCtrl>(true)` 和包含 inactive 的粒子遍历，不依赖易失的序列化组件引用 | YAML、源码及正常/失败 PlayMode 通过 |
| 黑箱运行时生成导致位置漂移或没有 Inspector 调整入口 | 黑箱以 inactive 实例预放在 `Level_091.prefab`；最终初始姿态以策划当前 Inspector 保存结果为准，控制器在 `Awake` 缓存该姿态并自动解析场景子物体，源码无 `Instantiate` | Prefab 静态通过；Inspector/画面待验证 |
| 自动解析误选玩家 Actor 或敲门粒子 | 玩家回退查询明确排除 `Level91ActorCtrl`；黑箱按专用类型查找；撞击粒子按精确名字 `Knock_Explosion_Impact` 查找 | 源码静态通过；存在重复类型/重名对象时待验证 |
| 关键引用或黑箱初始化异常时无演出直接击飞 | `boxBadActor/actor/exitDoorSpine` 缺失、黑箱初始化失败或 Sequence 异常都会调用 `CancelBrokenAngrySequence()`，隐藏黑箱、恢复 `Ready`、Actor 和 HUD，不再直接调用命中处理 | 源码静态通过；异常注入待验证 |
| `impactVfx` 无法按名解析时失败链软锁 | 粒子为空时只跳过 `Play()`；合法黑箱命中后的 `trap`、击飞、旋转和失败兜底继续 | 源码静态通过；缺粒子注入待验证 |
| 重开后黑箱残留在冲撞或退场位置 | `PrepareHidden()` 缓存预放姿态；清理调用 `ResetPresentation()`，取消走出、冲撞和持续退场 Tween，恢复位置/旋转/Alpha 并停用对象 | 旧冲撞流程重置 PlayMode 通过；新增退场中的 Replay 待验证 |
| 黑箱无影子 | 本关黑箱 Prefab 对共享 Actor 的 `Shadow` 节点显式保持 active，脚本不再调用隐藏影子接口 | Prefab 静态通过；视觉待验证 |
| 影子先于角色渐显而突兀出现 | Spine Alpha 单独从 `0` 渐显，Shadow 随整个 inactive 对象激活，未做独立 Alpha Tween | 已识别；需视觉决定是否调整 |
| 黑箱 Collider/刚体影响场景物理或被玩家推走 | 初始化关闭 Rigidbody2D 模拟并禁用所有子 Collider，走出和冲刺均由 DOTween 控制 | 源码静态通过；运行时碰撞旁路待验证 |
| 黑箱只比较玩家 X，Y 不对齐导致看起来没撞上 | 冲刺终点采样玩家 X，黑箱 Y 使用策划预放位置；到达 X 即触发撞击，不依赖 Collider | 设计静态确认；位置必须视觉验收 |
| 黑箱命中后停在原地 | 命中回调后保持 `walk01`，按原冲刺方向和 `enemyChargeSpeed` 继续移动到 `Camera.main` 视口边缘外 `15%`；无相机或换算无效时沿原方向追加 `16` 个世界单位 | 源码、字段和 Prefab 静态通过；本轮未做 PlayMode |
| 黑箱继续移动后撞击节奏错位 | 命中位置在回调时缓存，粒子和击飞仍延迟 `0.1s` 使用该接触点；黑箱在这段时间继续移动 | 行为静态确认；需 PlayMode 视觉验收 |
| 撞击特效与 Level10046 不一致 | 场景 `Knock_Explosion_Impact` 使用与 `Level_046.prefab` 相同 GUID 的 `Knock_Explosion`；091 按名字解析，等待 `0.1s` 并放在敌人位置 | 失败流程 PlayMode 通过；其它宽高比待验证 |
| 击飞方向或力度不符合 Level10046 | 当前序列化 `(-0.5,0.5)`、力度 `50`、`0.5s` 一圈无限旋转，与 `Level46EnemyCtrl` 对应参数一致 | 失败流程 PlayMode 通过 |
| 主角击飞后中途停住，没有进入失败链 | 091 不提前调用 `Die()`，击飞后不主动 Stop，旋转保持循环；`failureDelay=1s` 后保留失败兜底 | 当前失败流程 PlayMode 通过；其它场景构图/宽高比待验证 |
| 黑箱尚未出画就被失败事件清理 | 当前固定主相机中心 `X=0`、正交尺寸 `5.9`，玩家命中 X 约 `-7.49`；按 `1920x1080 / 2340x1080`、速度 `12` 和额外 `15%` 视口宽度静态换算，退场约需 `0.51s / 0.76s`，小于命中后 `1.1s` 的失败事件时限 | 参数静态通过；实际动画连续性和完整出画仍待 PlayMode |
| 成功和失败流程同时发生 | 状态从 `Ready` 原子切换到 `DoorbellRinging` 或 `AngrySequence`；后续入口均检查状态 | 源码静态通过；同帧输入待验证 |
| 退出/重开后异步回调写入旧关卡 | 控制器和面板各有生命周期 CancellationToken；清理 Kill Tween、递增流程版本、复位黑箱和 HUD | 重置 PlayMode 通过；退出/快速重开仍待验证 |
| Prefab 手工引用出现重复 fileID 或悬空内部引用 | `2026-07-29` 再次扫描 Level/Panel/BoxBad 三个 YAML，分别为 `137/34/4` 个定义，重复均为 `0`、缺失本地引用均为 `0`；三个 UI 字段在对应脚本和 Prefab 中各唯一，`runOffViewportPadding` 仍唯一；共享 `VolumeSettingContent` 为 `42 / 0 / 0` | 跟进修改后静态扫描通过；本轮未操作 Unity GUI |
| 配置存在但正式入口仍无法加载 | 工作区静态找到 `tbleveldata: 10091 -> Level_091` 与 `tblevelsort: id=89 -> 10091` | 配置静态存在；正式选关待验证 |
| 成功结算引用存在但本地化 Term 缺失或重复 | `I2Languages.asset` 新增且仅新增 `S_congraktTips_Des_level_91`；静态检查为 `termCount=1`，没有修改公共胜利页或 `tbleveldata.json` | Unity 导入成功；修复后胜利页动态显示待复验 |
| 新 Term 的语言槽位数量或顺序与来源不一致 | Level091 的 76 个逻辑槽位逐项复用 `S_congraktTips_Des_level_04`，脚本输出 `level04Slots=76 level91Slots=76 zhCNExpected=True`，逐槽比较 `76/76` 无差异 | 静态解析通过；原始文本块仅因 YAML 空白格式不同而不相等 |
| 本关影响存档、商业或平台系统 | Level91 脚本不访问 DB、Save、PlayerPrefs、RemoteConfig、analytics、广告、IAP、网络或平台 SDK | 源码扫描通过 |
| 把独立 Roslyn 编译误当成玩法验收 | 最新 Bee 响应文件独立编译仍为 `0 error`，但不能证明自动引用解析、Prefab 导入、Spine 动画、UI 布局、物理轨迹或触摸正确 | 编译通过；Unity/设备验证待完成 |

## 验证结论

已完成：

- Level91 三个脚本静态流程与本地暂停标记检查。
- `Level_091.prefab`、`GamePlayPanel_091.prefab`、`Level91BoxBadActor.prefab` 的脚本、资源、场景实例和关键参数回读；确认 `boxBadActor/impactVfx` 当前均为 None，但对应场景对象仍存在。
- inactive 黑箱与精确名称撞击粒子的自动解析、关键引用异常取消失败演出、按钮视觉中心定位，以及 DoorOpen 重复门铃只播放反馈的源码静态检查。
- 与 Level004 `KnockKnock` 及 Level046 撞飞参数的只读对照。
- 三个 Prefab 的 YAML 唯一 fileID 与内部引用扫描。
- `10091` 数据与排序条目的静态存在性检查。
- `S_congraktTips_Des_level_91` 唯一性、76 个逻辑语言槽位及简体中文文案静态检查通过；与 Level004 对应 Term 逐槽比较为 `76/76` 一致。
- `I2Languages.asset` 的限定差异仅新增该 Term 段，共 `83` 行；Unity 已成功导入该资源。Unity 空标量行的既有尾随空格未在本次文档工作中改写。
- 最新 Unity 6000.0.59f2 dotnet+csc 独立 Roslyn 编译：`exit code 0`、`0 error`。
- `2026-07-29` 跟进后的独立 Roslyn 编译再次得到 `exit code 0`。
- 跟进后的 YAML 静态扫描仍为 Level/Panel/BoxBad `137/34/4` 个定义、`0` 重复、`0` 缺失本地引用；`dingDongOffsetFromSfx`、`dingDongVisualScale` 与 `dingDongTextLocalOffset` 三个 UI 字段均唯一，当前值为 `{130,-55}`、`1.5`、`{-55,0}`，`runOffViewportPadding` 仍唯一。
- 静态顺序断言确认视觉倍率不缩放动态根、文字偏移只叠加到 TextTarget，以及黑箱 `walk` 走出完成后按 `idle -> 等待0.5s -> angry` 执行。
- 结构化解析 `Box.json` 确认 `idle / walk / angry / walk01` 与 `box_bad` 均存在；SkeletonData `defaultMix=0.2`。
- 固定主相机与两种 Prefab 标注横屏比例的静态时序换算为约 `0.51s / 0.76s` 到达视口外，均早于命中后 `1.1s` 失败事件；这不能证明 Spine 外轮廓已完全不可见。
- 最后一次刷新后检查 `Editor.log`，`error CS / NullReferenceException / MissingReferenceException / Level91 Exception` 计数均为 `0`；本轮未操作 Unity GUI，该日志检查不等同于 PlayMode。
- `VolumeSettingContent.prefab / GamePlayPanel_091.prefab` 分别为 `42 / 34` 个 YAML 定义，重复和缺失本地引用均为 `0`。新 GUID `f240a8b03ae84ada8c57dc035e3084a7` 在全部 `Assets` 中出现 `2` 处，旧 GUID `7476e53ae4e2015459ee7ec10a992060` 只在旧图 Meta 中保留 `1` 处；独立 Roslyn 编译仍为 exit code `0`。
- 两份新增文档的尾随空白扫描通过；共享索引的限定路径 `git diff --check` 通过。全工作区检查仍被本任务外 `Assets/Game/ArtSrc/Font/en SDF.asset` 的既有 trailing whitespace 拦截。
- `2026-07-29` 跟进前的 Unity PlayMode 核心回归通过：正常门铃成功链、连续敲门失败链、重置复位、DoorOpen 后重复 Ding Dong 但不重复完成开门。
- 上述旧测试路径中未观察到 Level091 异常或空引用；字段为 None 时 inactive 黑箱与 `Knock_Explosion_Impact` 的运行时解析正常。该结果不覆盖本次新增偏移和持续退场。

尚未完成：

- 正式程序集文件时间与构建日志归档；当前脚本已能进入 PlayMode。
- 其它横屏宽高比下的门铃视觉中心、传播动画、黑箱/Shadow、10046 粒子和击飞构图。
- 用户确认的是旧根偏移 `{110,-55}`，随后要求整体右移；最终 `dingDongOffsetFromSfx={130,-55}`、`dingDongVisualScale=1.5`、有效 TextTarget X=`105` 的组合及其可能的继续微调仍未验收。
- 黑箱走出后切换 idle 时 `defaultMix=0.2` 的过渡，以及 idle 停留 `0.5s` 后播放 angry 的节奏。
- 黑箱命中后是否不停顿并完全跑出场外，`0.1s` 延迟粒子/击飞是否仍自然，以及退场途中 Replay 是否立即恢复初始位置。
- 关键引用/初始化异常注入时的安全恢复，以及 Sprite/撞击粒子缺失注入。
- 暂停/恢复、主动关闭弹窗、动画中极限连点、退出、快速重开、Replay 和切后台边界。
- 修复后胜利页是否实际显示 `S_congraktTips_Des_level_91`；再次 PlayMode 结算被项目既有 `Loading/SDK waiting` 阻挡，当前只能确认资源导入和静态数据正确。
- Android/iOS 横屏触摸、多指、宽高比和设备性能。
- `GameSettingPopup` 与 `GameSettingPopup2` 中新音效图标的显示、Toggle 开关态、不同分辨率和 Popup 池复用表现。

## 回滚路径

- 整体回滚：恢复本轮前的 `Level_091.prefab` 与 `GamePlayPanel_091.prefab`，移除 `Level_91` 私有实现和文档；配置项仅在配置所有者确认后撤回。
- 只回滚门铃：恢复普通 HUD，移除 091 私有 Sfx Toggle 改绑和 Ding Dong Sprite 引用；不要修改共享 `GameSettingPopup`、`VolumeSettingContent` 或音效系统。
- 只回滚失败演出：恢复 091 对原 `KnockKnock` 的关卡内使用，移除预放黑箱和独立撞击粒子；不要回滚或编辑 Level004、`KnockKnock.cs`、Level046、共享 Actor。
- 只回滚 `2026-07-29` 门铃定位：从 `Level91GamePlayPanel` 与 `GamePlayPanel_091.prefab` 移除 `dingDongOffsetFromSfx`，不修改共享暂停页；这会恢复用户已指出的偏高位置。
- 只回滚 `2026-07-29` 黑箱退场：从 `Level91ActorCtrl` 与 `Level91BoxBadActor.prefab` 移除 `runOffViewportPadding` 和命中后的持续移动步骤；这会恢复黑箱停在接触点的旧问题。
- 只回滚最新门铃参数：把根偏移从最终 `{130,-55}` 恢复为此前确认过的 `{110,-55}`，把视觉倍率从 `1.5` 恢复为 `1.2`；保留文字局部偏移 `{-55,0}` 和 idle 流程。
- 只回滚整个图片大小和文字位置功能：保留当前最终根偏移 `{130,-55}`，仅移除 `dingDongVisualScale`、`dingDongTextLocalOffset` 及其 Prefab 值，恢复原始图片 `sizeDelta` 和 TextTarget X=`160`。
- 只回滚本次 idle 停顿：移除走出完成后的 `PlayAnimation(idle, true, false)`，恢复直接等待 `preAngryDelay=0.5s`；不要回滚黑箱命中后的持续退场。
- 只回滚共享音效图标：把 `VolumeSettingContent.prefab` 唯一一行 Sprite GUID 从 `f240a8b03ae84ada8c57dc035e3084a7` 恢复为旧 GUID `7476e53ae4e2015459ee7ec10a992060`；旧图和 Meta 保留，无需修改两个 Popup。
- 只回滚暂停空引用修复：不建议恢复逐帧 `PlayCtrl.CheckState()`；若必须改变，应在 091 私有范围内保留等价的空状态机保护并重新验证退出/重开。
- 只回滚成功结算本地化补项：仅删除 `I2Languages.asset` 中的 `S_congraktTips_Des_level_91` Term；保留公共胜利页和 `tbleveldata.json` 的既有实现。

## 2026-08-05：门铃音频与失败重开风险增量

| 风险 | 当前控制 | 验证状态 |
| --- | --- | --- |
| 首次门铃响但重复反馈不响 | 播放点位于合法反馈门禁后、`TryBeginDoorbell()` 前；Ready 与 DoorOpen 都会进入，状态完成仍由 `shouldOpenDoor` 隔离 | 静态顺序通过，PlayMode 待验证 |
| 动画中连点或失败态误播 | `doorbellInProgress`、`CanPlayDoorbellFeedback` 与 `specialInputEnabled` 继续先行拦截 | 静态分支通过 |
| 音频资源转码或 GUID 断链 | 源/目标 SHA256 一致；Ogg/Vorbis 44.1kHz 立体声原样保留；Meta GUID `d2f9955a06194b899bca8cb1e608c456` 在 Assets 唯一，Collection 只引用一次 | 静态通过 |
| 门铃与通用按钮声叠加 | 当前 Sfx Toggle 没有 `ClickAudio`，091 只新增 `l91Doorbell` one-shot | Prefab 静态通过 |
| 最快重复点击尾音轻微重叠 | 音频 `1.085488s`，视觉约 `0.94s`，理论尾音约 `0.145s`；保留允许重复按的需求 | 听感待 PlayMode |
| 进入 091 前已关闭 SFX 导致门铃无声 | 沿用 `AudioManager` 和玩家保存设置，不绕过全局静音；091 内按钮只是不再切换该状态 | 产品边界待确认/验收 |
| 091 被击飞后停在失败态 | 日志定位到 Level083 陈旧 `GameResetEvent` 监听；083 现在先解绑再清理已销毁 slot，避免 EventDispatcher 跳过后续 PlayCtrl 监听 | 源码与编译通过，跨关 PlayMode 待验证 |

独立 Roslyn 编译为 `0 error`，资源/tag/调用顺序断言通过。本轮未控制 Unity GUI、未执行 PlayMode 或移动端验证。门铃音频与 083 重开修复均有独立回滚路径，不需要回滚 091 视觉、共享 UI 图标或失败演出参数。
