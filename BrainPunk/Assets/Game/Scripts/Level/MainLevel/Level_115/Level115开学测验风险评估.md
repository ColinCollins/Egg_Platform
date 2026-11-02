# Level 115 开学测验风险评估

## 2026-08-18 行为等价精简风险复核

- **事实**：`_doorUnlockRoutine` 仅在 `_doorUnlockStarted=true` 后赋值；后续调用会在方法开头返回，原停止分支在现行赋值路径上不可达。
- **直接结论**：单次解锁门禁、协程启动时机和答案/结算链保持不变。
- **假设**：不假定反射或仓库外代码直接改写两个私有字段；当前无此证据。
- **缺失证据**：尚未执行 Unity PlayMode 的快速重复答题、暂停/重开、门动画和移动端回归，标记为 `uncertain`。
- **回滚**：恢复旧的协程非空停止判断。

## 原有行为基线

本次测验实现前，Level 115 已有右门上锁、DoorLock 与完成入口骨架，但正式玩家流程无法调用完成入口。当前完成链已改为五题全对后直接开锁，不再爆钥匙。早期 `Level115QuizPopup.cs` 只有生命周期空壳，Popup 根节点没有该组件；课桌没有 Trigger，关卡使用无手掌按钮的 `GamePlayPanel_000`。

当前已补齐静态实现，但没有 Unity、编译或运行证据。

## 影响文件与系统

### Level 115 专属

- `BrainPunk/Assets/Game/Prefabs/Level/Level_115.prefab`
- `BrainPunk/Assets/Game/Scripts/Level/MainLevel/Level_115/Level115Ctrl.cs`
- `Level115GamePanelFeature.cs`
- `Level115QuizTrigger.cs`
- `Resources/Level115QuizPopup.prefab`
- `Sprites/Scripts/Level115QuizPopup.cs`
- `Sprites/Scripts/Level115QuizDragHandle.cs`
- 本目录功能说明、风险评估与 `docs/contexts/levels/115/CONTEXT.md`

### 新增专属 HUD 资源

- `BrainPunk/Assets/Game/UI/GamePanel/Resources/GamePlayPanel_115.prefab`

### 内容数据配置

- `BrainPunk/Assets/Game/Configs/tbleveldata.json`：新增 `10115 -> Level_115`；不修改玩家排序表。

### 本关局部输入切断

- `Level115Ctrl.CutGameplayInput()` 只复用上游已有的 `GamePlayPanel.ResetState()`，并通过本关 Actor 的公开输入入口清零水平输入；Popup Mask 继续阻断新 HUD 点击。`GamePlayPanel.cs` 与其它共享运行时代码保持上游版本。

### 既有共享/用户资产

- `BrainPunk/Assets/Resources/I2Languages.asset`：Level 115 使用 `S_LevelTips_Des_level_115`（玩法说明「拖动交换数字或符号使等式成立」）、`S_LevelTips_Des_level_115_1`（条数统计）与 `S_LevelTips_Des_level_115_2`（错题固定提示「你答错了！！！」），当前均仅 zh-CN。
- DoorLock、右门、BaseLevelCtrl、ActorCtrl、UIManager、SystemTips、I2、DOTween：只复用既有能力；本关不再依赖 Key 拾取链。
- 标题占位只读复用共享 Tips 与现有 I2 key `S_LevelTips_Des_level_78`；只在 `Level_115.prefab` 加实例，不改共享 Prefab、I2、078、字体或 C#。

## 风险项

### P0：正常关卡入口仍不存在

**触发条件：** 玩家仅通过正常选关或下一关流程进入 Level 115。
**影响：** `tbleveldata.json` 已有 `10115 -> Level_115`，但 `tblevelsort.json` 没有指向 `10115` 的排序项，完整玩法仍不可达。
**当前控制：** 用户明确选择只登记内容数据，暂不修改玩家排序表或 `LevelData.xlsx`。
**剩余缺口：** 正式投放仍需单独确认 sort 位、Excel 真源与生成配置。

### P0：Popup 加载、组件反序列化或 C# 编译失败会锁死通关

**触发条件：** ResourceSystem 无法按 `Level115QuizPopup` 加载资源、根组件/字段反序列化失败，或新增代码存在编译错误。
**影响：** 手掌点击无法开始测验，`NotifyQuizCompleted` 无自然入口，右门保持锁死。
**当前控制：** Popup 根脚本 GUID、生成字段、X1/X2、五槽和计时条 fileID 已做静态闭合检查；`Create()` 使用 `UILayer.Popup`。
**剩余缺口：** 根据项目约束，本任务不运行 Unity 或编译，因此加载和编译均为 `uncertain`。

### P1：专属 HUD 变体可能无法绑定或显示手掌

**触发条件：** `GamePlayPanel_115` 未被 ResourceSystem 找到、Feature 脚本丢失、`Use_btn` 引用为空，或 Level Ctrl 的 `Start` 时机早于 HUD 创建。
**影响：** 玩家进入课桌范围但没有可用入口。
**当前控制：** Level Prefab 已改用 `GamePlayPanel_115`；HUD 变体保留既有手掌 Sprite 和 CustomButton，并由 `Level115Ctrl` 定向查找专属 Feature。
**剩余缺口：** UI 实例化时序和缩放动画未运行验证。

### P1：课桌世界 Trigger 与多 Collider 生命周期

**触发条件：** 父级缩放、玩家多个 Collider 进出顺序、Tag/Actor 引用或 Physics Layer 不符合预期。
**影响：** 手掌误显示、提前隐藏或完全不出现。
**当前控制：** Trigger 运行时按 lossyScale 把 `5.28×1.78` 世界尺寸换算为本地 size，只接受 Tag=Player 且属于当前 `BaseLevelCtrl.Actor` 的 Collider，并用集合处理多 Collider。
**剩余缺口：** Unity 真实 bounds、碰撞矩阵和进入/离开回调顺序未验证。

### P1：六位置拖拽与多指所有权

**触发条件：** 多指同时 BeginDrag、拖拽中暂停/超时、释放到边缘或 UI 嵌套 Canvas 的坐标换算异常。
**影响：** 内容复制、丢失、错误交换、拖拽后误关闭或卡住输入。
**当前控制：** Popup 只接受第一个 pointerId；源内容仅视觉隐藏，模型在有效释放时才互换；暂停、隐藏、超时和关闭统一取消拖拽。五槽使用 160×160 Rect，右上角使用 88×88 X2 Rect。
**剩余缺口：** EventSystem 默认阈值、移动端多指、World/Screen Canvas 相机换算均无运行证据。

### P1：右上角任意内容跨题继承会破坏后续参考解法

**触发条件：** 玩家把 `×`、数字或等号留在旧题槽位，或把旧题元素保存在右上角带入下一显示位；在受约束随机题序下，特殊题 `4 + 2 = 8` 可能出现在显示第 4 或第 5 位，其前序显示位仍可先换走右上角 `×`。
**影响：** 后续题元素集合改变，可能出现额外成立式，也可能无法使用题表参考解法；特殊题将 `+` 换成右上角 `×` 仅是参考解法，不是跨题操作后仍必然可用的硬保证。系统不在特殊题到来时强制恢复右上角 `×`。
**当前控制：** 这是用户明确确认的玩法后果；同次测验只重载下一显示位五槽并保留右上角，不做隐藏恢复，通用判题接受任何数学成立排列。新一次测验才恢复右上角 `×` 并重新抽取题序。
**剩余缺口：** 玩家理解、实际难度、以及随机题序与跨题继承叠加后的可解性均无运行或可用性证据。

### P1：计时、切题、反馈与暂停边界

**触发条件：** 低帧率、应用切后台、Pause/Resume 与 6 秒边界同帧、外部 Popup 覆盖，或 0.2 秒切题/全对 FinalHold、1 秒错题 FinalHold 与进度点视觉反馈进行中暂停。
**影响：** 多算/少算时间、后台回来直接判题、隐藏期间继续推进、切题/错题停留阶段卡死，或进度点 Tween 在暂停期间继续播放/恢复后错位。
**当前控制：** 使用 realtime 时钟；只在 `PLAYING`、可见且未暂停时累计；暂停/隐藏/后台均重置时钟基点，不补算经过时间；切题过渡、FinalHold（正确 0.2s / 错题 1s）与进度点视觉反馈受同一冻结规则（Pause/Resume/Hide/Show 同步 Tween 播放态，不依赖 `timeScale`）。到时已触发的正误音效不随暂停链截断。
**剩余缺口：** Unity PlayerLoop、事件订阅顺序、设备后台回调，以及暂停中途冻结/恢复勾叉观感均未运行验证。

### P1：外部 Popup 暂停所有权依赖 UIManager 生命周期

**触发条件：** Back/Escape 打开 CommonPopup、其它 Popup 以 `DestroyUI` 而非 `CloseUI` 关闭，或 CloseAll 与 OnHide 同帧发生。
**影响：** 测验被隐藏但计时继续、取消 Popup 后不恢复，或静默关闭误 Resume。
**当前控制：** Popup 在 OnHide 后延迟确认自身仍打开，再决定是否拥有全局暂停；仅由自身发起暂停时在 OnShow 恢复。关闭意图和 session 状态阻止 CloseAll 被误认成外部覆盖。
**剩余缺口：** 实际 UI 栈回调顺序未运行验证。

### P1：主动、系统、单题失败和静默关闭混淆

**触发条件：** 直接调用 UIManager CloseAll、失败重置、退出、销毁，单题到时判错与右上角轻点接近同时发生，或第五题全对与轻点接近同时发生。
**影响：** 重置时误弹统计、单题失败误弹统计、未完成题被补判、重复开锁、主动关闭不显示统计，或错题后仍切到下一题。
**当前控制：** Popup 显式区分 Active/System/Silent；`EndSession` 在 Active 或 System 且 5/5（completed）时 `ShowStatistics`；在 System 且未完成且 `wrong > 0` 时 `ShowWrongAnswerTip`（`S_LevelTips_Des_level_115_2`）；二者互斥。单题到时不成立与第五题全对均走 FinalHold 后 `EndSession(System)`，但前者因 `wrong != 0` 不 completed、不开锁；只有系统结束且 5/5 才准备完成并直接开锁。`CompleteCurrentQuestion` 在 `!correct` 时不再进入 Transition，错题 FinalHold 为 1s。
**剩余缺口：** 同帧输入与 Update 顺序、1s 错题 FinalHold 与固定提示上浮观感仍需要运行观察。

### P1：Gameplay HUD 旧输入恢复

**触发条件：** 玩家按住左右移动时用另一触点打开测验，或关闭 Popup 时旧触点仍在屏幕上。
**影响：** 角色在 Popup 下继续移动，或关闭后无需新按下便自动移动。
**当前控制：** `Level115Ctrl.CutGameplayInput()` 在打开与关闭边界调用上游已有 `GamePlayPanel.ResetState()` 并清零本关 Actor 水平输入；Popup 全屏 Mask 阻断打开期间的新 HUD 点击。没有修改共享 `GamePlayPanel.cs`。
**剩余缺口：** 真机触摸、PointerEnter 与多指组合未验证；静态证据不能证明关闭边界的旧触点一定不会重新产生 PointerEnter，保持 `uncertain`。

### P1：Level 115 专属词条只有简体中文

**触发条件：** 非 zh-CN 语言下结束测验。
**影响：** I2 可能返回空串或走未确认回退。
**当前控制：** 玩法说明使用 `S_LevelTips_Des_level_115`，条数统计使用 `S_LevelTips_Des_level_115_1`，错题提示使用 `S_LevelTips_Des_level_115_2`，不绕开 I2；当前阶段明确只交付中文。
**剩余缺口：** I2 回退和 TMP 显示未验证。

### P1：直接开锁链仅有静态等价证据

**触发条件：** 五题全对后执行 DoorLock 解锁句柄或延迟恢复门触发。
**影响：** 锁不播动画、门碰撞未恢复、无法进门或入口错误重复开放。
**当前控制：** `PrepareQuizCompletion` 在 Popup 关闭前禁用课桌，`NotifyQuizCompleted` 在关闭和统计后一次性调用 DoorLock `EventHandleListener`，约 1 秒后启用右门 Trigger；场景已移除 Key 与爆钥锚点。
**剩余缺口：** 解锁动画时序、门碰撞恢复与进门均无运行验证。

### P2：动态 TMP 与进度条视觉

**触发条件：** 右上角换入数字/等号，槽位换入 `×`，或彩色条 `fillAmount` 接近零。
**影响：** 字形尺寸、颜色、裁切、锯齿或进度条端点观感异常。
**当前控制：** 数字/`+`/`-`/`=` 用 TMP，并统一 `#665a4f` 外侧约 4px 描边（outlineWidth + FaceDilate）；凡内容为 `×` 时五槽、右上角与拖拽预览均显示同一张 46×46 X1，与 TMP 互斥；计时条内层为 Horizontal Filled，仅用 `fillAmount` 表示剩余比例，左端固定、右缘平切缩短。
**剩余缺口：** 动态 SDF 构建后字形与最终像素观感未验证。

### P2：五题进度点布局、到时反馈与结果停留（含单题失败）

**触发条件：** Prefab 中进度点锚点/层级与设计稿偏差，勾叉互斥失败，到时反馈 Tween/音效与暂停/关窗/重置同帧交错，或连续多题快速判定导致音效重叠。
**影响：** 进度点遮挡关闭钮或说明文案、未判定点 residual 勾叉/缩放/位移、第五题结果一闪而过或停留期间仍可关闭；错误路径误播正误音效；多题音效叠听影响观感。
**当前控制：** 进度点挂在 `组 5` 下独立 `组 6`，位于关闭钮之下、面板之上；每位 yuan 常驻、gou/cha 默认隐藏且 `raycastTarget=0`。到时判题仅在 `CompleteCurrentQuestion` 写点后对**当前新写入点**播放 0.2s 差异化 Tween（正确勾 OutBack 弹出+底点回弹，错误叉弹出+锚点轻抖）并 `AudioManager.PlaySound("passwordCorrect"/"passwordError")`（复用 006 既有 Tag，不新增素材、不截断）；已有点保持静止。Pause/Resume/Hide/Show 与前后台同步冻结/恢复视觉 Tween；Reset/关窗/静默取消 Kill Tween 并归位 scale/anchoredPosition。主动关闭、静默取消、重置与未判定题不触发反馈。第五题全对进入 `FinalHold` 0.2s；单题失败进入 `FinalHold` 1s（`WrongFinalHoldDuration`，略长于 `passwordError`≈0.984s）；输入锁定并走与 Transition 相同的暂停冻结，结束后才 `EndSession`；单题失败路径关窗后弹固定错题提示、不弹条数统计。
**剩余缺口：** 真实像素叠放、多分辨率锚点、勾叉 Tween 观感（含错误抖动强度与回弹幅度）、1s 错题停留主观可读性与音效对齐听感、错题 SystemTips 上浮观感、音效重叠听感与暂停中途音画关系均未运行验证；当前仅为静态代码与文档一致性证据。

### P2：UIManager 复用实例后的清理

**触发条件：** 主动关闭后再次从课桌重开，或失败重置后复用已创建的 Popup 实例。
**影响：** 旧题目、统计、pointerId、暂停所有权、owner 引用、上次显示顺序或进度点 Tween/缩放/位移残留。
**当前控制：** 每次 `BeginSession` 重置统计、右上角、时钟、关闭状态与五题进度点为空圆，并调用 `BuildConstrainedQuestionOrder` 重建 `_questionOrder`；Reset/关窗/静默取消路径 Kill 进度点反馈 Tween 并恢复 scale/anchoredPosition；OnClose 取消拖拽、解绑事件并释放 owner。
**剩余缺口：** 实例复用与快速重开无残留、以及新测验重抽在运行时的实际观感，均无 PlayMode 证据。

### P2：受约束随机题序实现偏离或分布不均

**触发条件：** 未来改动使代码退回表号 1→5 固定出题、特殊题落入显示第 1–3 位、其余四题未在剩余位均匀打乱、同一次测验中途重抽，或新测验未重抽而沿用旧序；或 `UnityEngine.Random` 在目标平台上的实际采样偏离等概率预期。
**影响：** 与已确认的题序规则（2026-08-12 用户确认）不一致；特殊题过早出现或永不落在第 4/5 位会改变难度曲线与参考解法可达窗口；进度点若误绑题目身份会与显示位语义冲突。
**当前控制：** `Level115QuizPopup.BuildConstrainedQuestionOrder` 已静态实现（方法名与算法细节仅作源码静态证据，不扩大为运行验证）：特殊题源索引 `SpecialQuestionSourceIndex = 3`，显示位由 `Random.Range(0, 2)` 等概率取 3 或 4；其余四题源索引经 Fisher–Yates 填入剩余位；仅 `BeginSession` 构造一次，`LoadQuestion` 按 `_questionOrder` 消费且不重置右上角；进度点与 5/5 完成判断按显示位 / 五题数量。此为源码静态控制，可作为未来回归对照。
**剩余缺口：** 未运行 Unity/编译/测试/探针；实际随机采样分布、玩家重试重抽观感与同局稳定运行表现均为 `uncertain`，不得把静态算法写成已验证运行行为。

### P2：关卡标题占位的实际可见性与语义误读

**触发条件：** `LevelTitlePlaceholder` 静态复用 Tips + `S_LevelTips_Des_level_78`，中心 `(0.5, 4.42)`；该 key 简体中文为「射箭比赛」，只是临时占位不是 115 正式标题。
**影响：** I2 Awake、TMP、SceneLayer、画幅/遮挡异常，或玩家把 078 文案误认成本关正式标题。
**当前控制：** 只在 `Level_115.prefab` 增加独立 Tips 实例，不改共享 Prefab、I2、078、字体或 C#；占位明确记录为临时粘贴。
**剩余缺口：** 仅有 Prefab 静态证据，画面与语义理解为 uncertain。

## 已规避风险 / 直接结论

- 未复用 Level 006 的密码 Feature，避免手掌点击打开错误 Popup。
- Popup 根组件和 Generated 字段不再依赖空壳资源。
- `UILayer.Popup` 保持 Gameplay HUD 可见并由共享 Mask 阻断点击。
- 题组仍为固定五题成员（含 `5 + 2 = 3` 等），不再依赖 Prefab 占位文本；显示顺序改为受约束随机，不以表号 1→5 作为固定显示序。
- 计时条内层为 Filled，代码只驱动 `fillAmount`，不再改 RectTransform 宽度。
- 非业务关闭不通过通用 `OnClose` 自动显示统计。
- 仅新增 `tbleveldata.json` 内容数据注册；`tblevelsort.json` 保持不变，不会静默改变玩家排序或投放范围。
- 到时正误音效复用既有 `passwordCorrect` / `passwordError` Tag，不新增音频素材；反馈触发点收敛在到时判题写点路径，主动关闭/静默取消/重置/未判定题不触发。
- 进度点视觉反馈仍约 0.2s 完成；正确切题/第五题全对 FinalHold 为 0.2s，单题失败 FinalHold 为 1s；暂停链冻结视觉、音频自然继续，避免把暂停扩大为截断 SFX。
- 单题失败即结束（2026-08-13）：到时不成立不再切下一题；1s FinalHold 后系统关窗；`EndSession` 弹 `S_LevelTips_Des_level_115_2` 固定提示，不 `ShowStatistics`，避免未通过路径误弹“正确X条，错误X条”。
- 右上角跨题继承与特殊题参考解法的“非硬保证”关系已在文档中显式记录，避免把 `4 + 2 = 8` 的 `×` 换 `+` 误写成跨题后仍必然可用。

## 验证证据

- 定向读取并核对 Level 115 Context、功能说明、风险说明、代码、Popup Prefab、Level Prefab、HUD 变体、I2 与共享 UI/输入调用链。
- Prefab YAML 锚点唯一，本地 fileID 引用闭合；新脚本 GUID 与 `.meta` 一致。
- Popup 根组件绑定五个 TMP、五个槽位框、X1/X2、bar1/bar2，以及五题进度点 yuan/gou/cha 数组。
- Level Prefab 的 `gamePlayPanelName=GamePlayPanel_115`；课桌 BoxCollider2D 为 Trigger，序列化 size 为 `5.28×1.78`。
- `tbleveldata.json` 可解析，且 `10115` / `Level_115` 注册项各唯一；`tblevelsort.json` 不含 `10115`。
- 静态算法核对固定五题题组成员、通用判题语法、进度点到时写入与当前点 Tween/正误音效触发边界、FinalHold（第五题全对 0.2s；单题失败 1s）、单题失败即结束不切题/关窗后固定错题提示、右上角跨题继承和 5/5 完成条件。
- 受约束随机题序（2026-08-12 用户确认）静态算法已核对：`BeginSession` → `BuildConstrainedQuestionOrder`（特殊题显示位 3/4 等概率 + 其余四题 Fisher–Yates）→ `LoadQuestion` 按会话 `_questionOrder` 消费；进度点与完成判断仍按显示位 / 五题数量。运行时分布与重抽观感未验证。
- `git diff --check` 作为最终文本/YAML 空白检查。
- 关卡标题占位静态核对：`Level_115.prefab` 中 `LevelTitlePlaceholder` 的 PrefabInstance `1151150000000000020`、stripped RectTransform `1151150000000000021` 与根 child 各唯一一处；源 GUID `49d63fd22a39a044bab1c28dbf961bcb` 只出现在该 Tips 实例；覆写 `m_text=射箭比赛`、`m_margin.z=-0.40812898`、`mTerm=S_LevelTips_Des_level_78`、中心 `(0.5, 4.42)`、尺寸 `7.5473×2.2407`、SortingLayer 2 / SortingLayerID `1752186269`；引用闭合，YAML 无重复 anchor。该证据不证明 Unity 导入、I2 刷新或画面合成。

## 未验证 / 剩余风险

- 未运行 Unity、编译、EditMode、PlayMode、测试或探针。
- 不声明资源可加载、Prefab 可反序列化、C# 可编译或实际输入可用。
- 不声明 Trigger、触控、多指、Popup 栈、计时、进度点勾叉 Tween 观感、正误音效重叠听感、暂停冻结/恢复反馈、TMP、I2、DoorLock 解锁时序或完整通关运行正确。
- 受约束随机题序的源码静态实现已核对，但不声明运行时特殊题第 4/5 位经验频率、其余四题经验均匀性、同局稳定表现或新测验重抽观感；上述运行项保持 `uncertain`。
- Level 115 仅完成内容数据登记；因未加入 `tblevelsort.json`，正常配置可达性仍不存在，不属于本次已验证能力。

## 回滚路径

- 只回滚 Level 115 测验时，选择性恢复 `Level_115.prefab` 中 HUD 名称与课桌 Trigger；若需恢复爆钥匙流程，优先使用 git tag `level-115-key-drop-unlock`。
- 删除 `GamePlayPanel_115.prefab` / `.meta`、`Level115GamePanelFeature`、`Level115QuizTrigger` 与 `Level115QuizDragHandle`。
- 恢复 `Level115QuizPopup.cs` 和 Popup Prefab 根组件/字段接线。
- `GamePlayPanel.cs` 固定保持上游版本，无 Level 115 共享输入阻断增量需要回滚。
- 同步恢复 Context 和成对文档。
- 如撤销 Level 115 专属提示词条，只删除 `I2Languages.asset` 的 `S_LevelTips_Des_level_115` / `S_LevelTips_Des_level_115_1` / `S_LevelTips_Des_level_115_2` 块；不整文件回滚。
- 如撤销本次内容数据登记，只删除 `tbleveldata.json` 中 `10115 -> Level_115` 条目；不修改 `tblevelsort.json` 或 `LevelData.xlsx`。
- DoorLock 共享预制、右门、Level 083 和其它关卡不在本次选择性回滚范围。
- 仅回退标题占位时：从 `Level_115.prefab` 移除 `LevelTitlePlaceholder` PrefabInstance、stripped RectTransform 和根 child，并同步恢复 Context 与成对文档；不得修改共享 Tips、I2、078 或本关测验玩法。
