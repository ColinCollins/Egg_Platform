# Level128 功能说明

更新时间：2026-08-24

## 需求与背景

Level128 复用 Level096 的场景、四个可拖拽问号方块、暂停提示位置和密码门事件链。玩家在密码盘输入手机当前本地时间后通关；暂停设置页中的原固定密码改为 `????`，只提示答案是四位数字，不直接展示答案。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_128.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_128/Level128Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_128/Level128PasswordPopup.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_128/Resources/PasswordPopup_128_Time.prefab`
- `Luban/Configs/Datas/LevelData.xlsx`
- `Luban/Configs/Datas/LevelSort.xlsx`
- `Assets/Game/Configs/tbleveldata.json`
- `Assets/Game/Configs/tblevelsort.json`
- `Assets/Guru/guru_spec.yaml`
- `Assets/Guru/Gen/GuruAllSpecs.G.cs`

本次不修改 Level096、共享 `PasswordPopup`、`GamePlayPanel_001`、门 Prefab、拖拽组件、存档、分析事件、广告或平台 SDK。

## 原有行为基线

用户提供的 `Level_128.prefab` 最初是 Level096 的完整副本，仅根节点改名。它仍挂 `Level96Ctrl`，会打开固定密码 `3316` 的弹窗；暂停提示也直接显示 `3316`。共享密码盘只有数字 1 至 9，不能输入包含 `0` 的四位时间。

四个 Box 已沿用 Level096 的 `DragableItem` 结构，原 Level006 的顶击、等待和倒计数监听没有恢复。门继续监听 `OnTiggerItemEvent` 的事件 ID `1`。

## 当前行为

- `Level128Ctrl` 沿用 Level096 的 HUD 延迟配置、暂停来源检查和清理时序，只把本关密码资源配置为 `PasswordPopup_128_Time`。
- 暂停提示仍位于原位置、保持原旋转和初始隐藏；只有游戏内 `GameSettingPopup` 实际可见时显示 `????`，恢复、重置、离开或其它非暂停状态立即隐藏。
- `Level128PasswordPopup` 固定接收四位输入。运行时分别复制 7、8、9 生成第四行 `* / 0 / #`，并把 Grid 强制设为固定 3 列；背景的逻辑尺寸只向纵向拉长，上方显示区和四行按键组都以原版比例等比缩小到 `92%`。整个可见 `Bg` 面板再统一等比缩到 `95%`，键盘局部 Y 由 `-144` 上移到 `-132`，不会横向拉伸。三个新增键都参与输入并显示；包含 `*` 或 `#` 的四位内容会按错误密码反馈，只有四位时间数字能通过。
- 第四位输入后只读取一次 `DateTimeOffset.Now`，按设备本地时间比较。24 小时制使用 `HHmm`，12 小时制使用 `hhmm`；例如 21:05 可输入 `2105` 或 `0905`。
- 为避免玩家输入过程中刚好跨分钟，提交时同时接受当前分钟和上一分钟。更早或未来时间不通过。
- 密码正确后锁定输入，播放既有成功音效和提示，等待 1 秒后只派发一次 `OnTiggerItemEvent(1)`，继续使用原门打开与通关链。
- `Level128Ctrl` 只在事件 ID 1 真正派发后把本次尝试标记为已解锁，同时对当前 HUD 的 `Use_btn` 设置点击阻断、不可交互并隐藏。即使之后重新进入密码区域、共享显示事件再次激活按钮，按钮仍不能重新打开密码页；HUD 引用重新绑定时也会重放该状态。
- 关闭、Reset 或 UI 被统一关闭时递增会话版本，使尚未执行的成功任务失效；关闭淡出期间继续锁定输入并忽略重复关闭，直到缓存实例下次 `OnOpen` 才恢复。因此在 1 秒等待期间关闭不会提前锁死入口。Reset 与退出会销毁当前关卡和 HUD，新建的 `Level128Ctrl` 与 `Use_btn` 恢复初始未解锁状态。未成功输入在应用失焦或进入后台时清空。
- 四个 Box、四个下方占位 Tips、场景物体、暂停提示 Transform 和门事件链均未修改。

## 数据、资源、事件与存档

`LevelData` 在 `Level_124` 与 `Level_999` 之间新增 `10128 / Level_128`，版本备注为 `1.12.0`，结算和答案键分别登记为 `S_congraktTips_Des_level_128`、`S_answer_Des_level_128`。`LevelSort` 在默认排序尾部新增连续排序 ID `107 -> 10128`，避免用资源号 128 破坏逐关解锁。

Luban 已生成两份运行时 JSON。Guru 的 `level_config.level_sort` 默认值与生成 C# 同步在末尾追加 `10128`。本关时间和输入状态只存在于当前 UI 会话，成功入口锁只存在于当前 `Level128Ctrl` 尝试，不新增存档字段或全局状态；设备时间来自本地系统，不申请权限、不联网。

## 验证记录

- Level128 场景相对 Level096 只存在根名、控制器 GUID、暂停文本三类差异；归一化后文件 SHA256 一致，四个 Trick 点与门事件链保持不变。
- Level128 Prefab 有 245 个唯一定义、0 个悬空本地引用；密码弹窗有 144 个唯一定义、0 个悬空本地引用。新增 GUID 均全项目唯一，静态 YAML 未发现 Missing Script。
- 密码弹窗的 9 个原数字键、关闭键、4 个显示文本和错误 Feedback 均能唯一解析；`* / 0 / #` 的模板子树无本地化或持久点击回调，运行时分别绑定一次输入，不会覆盖标签或重复绑定。
- Prefab 仍保留原 `177x169` Cell、`13x16` 间距和 `645x1028` 背景逻辑尺寸；运行时上方 View 与按键组统一缩放为 `0.92`，`Bg` 整体再缩放为 `0.95`。最终可见背景约为 `612.75x976.60`，四行按键约为 `486.82x632.78`；键盘上移后与 View 的净间距约 `23.21`，距背景底部约 `46.51`，左右边距各约 `62.97`。背景图片为无 border 的 Simple Image，纵向拉长约 17.5% 会同时拉伸边框，最终观感仍需 PlayMode 和多分辨率验收。
- LevelData、LevelSort 已用 Artifact Tool 回读；除目标新增行外，既有值与公式及其它 Sheet 均逐项一致。Sheet 顺序、既有定义名称、7 条批注的位置/作者/正文均保留；全部 Sheet 已渲染检查，两份工作簿公式错误扫描均为 0。xlsx 体积变化来自导出器重新打包。
- Luban 生成退出码为 0，运行时 JSON 只新增 `10128 / Level_128` 和 `107 -> 10128`；两份 JSON 可解析且目标 ID 唯一。
- Guru YAML 与生成 C# 中的 `level_sort` 均可解析，长度为 107，末项均为 `10128`。
- 使用 Unity 6000.0.59f2 当前 Bee `Assembly-CSharp.rsp` 和 Unity 自带 Roslyn完整编译：退出码 0，结果为 0 error / 104 条工程既有 warning，Level128 诊断为 0。最新整体缩放与键盘上移后，再对 `Level128Ctrl` 与 `Level128PasswordPopup` 定向编译，结果为 0 error / 0 warning；键盘 Y、内容缩放、三列、符号输入、`Bg` 唯一 `0.95`、全屏 root 和开场动画目标共 11 项静态断言全部通过。
- 对编译产物反射调用 `IsPasswordValid`，09:05、21:05、00:00、12/24 小时制、上一分钟、跨日、两分钟前、非数字、短输入、空串与 null 共 14/14 用例通过。

## 未验证与剩余风险

- 当前 Unity Editor 正式程序集尚未刷新本轮布局与入口锁；PlayMode 也未验证纵向拉长后的边框观感、`* / 0 / #` 最终位置、不同横屏比例下的遮挡、错误/成功反馈、成功后重进区域不可重开、Reset 恢复入口和门动画完整链路，标记为 `uncertain`。
- 门沿用共享 `EventListenerExecutor`；静态代码显示它订阅 `OnTiggerItemEvent` 后没有在销毁时显式释放订阅。该问题同样存在于 Level096 等既有关卡，不在本关局部修复范围内；连续 Reset 后的旧门回调影响仍需 PlayMode 验证。
- Android/iOS 的 12/24 小时设置、时区切换、退后台、触控和安全区尚未真机验证。
- `S_congraktTips_Des_level_128` 与 `S_answer_Des_level_128` 当前尚未写入 `localization.csv` 和 `I2Languages.asset`；正式文案未提供，结算和答案界面显示为 `uncertain`。
- 若线上 Firebase/Guru 启用了旧 `level_config` 且其 `level_sort` 未包含 `10128`，远端值会覆盖仓库默认排序；正式发布需同步远端配置。

## 回滚路径

只回滚成功入口锁时，移除 `Level128Ctrl` 对 `OnTiggerItemEvent` 的订阅、尝试状态和 `Use_btn` 状态应用即可。完整回滚时删除 Level128 Prefab、私有脚本、密码资源与文档；从 LevelData、LevelSort、两份运行时 JSON、Guru YAML 和生成 C# 中删除唯一的 `10128` 与排序 ID `107` 记录。Level096 和所有共享密码、暂停、拖拽、门与存档实现无需回滚。
