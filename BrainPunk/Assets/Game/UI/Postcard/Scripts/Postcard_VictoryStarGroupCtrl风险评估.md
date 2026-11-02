# Postcard_VictoryStarGroupCtrl 胜利星数量显示风险评估

## 原始行为基线

- 胜利星组按保存 star id 映射视觉位置。
- id 1/2/3 分别对应第 1/2/3 个 Toggle。
- 等待动画目标按保存 id 解析后播放。
- 2026-07-09 之前，胜利星组不读取 `LevelData.LevelSumStarCount`，不会按关卡配置总星数隐藏多余 Toggle。

## 当前改动

- 胜利星组改为只按唯一有效收集数量显示。
- 保存 id 仍用于去重和合法性判断，不再决定视觉位置。
- 等待动画改为播放最后 N 个可见位置。
- 胜利星组新增按 `LevelData.LevelSumStarCount` 控制 Toggle 可见数量；显示数量被限制在 `0..GetToggleCount()`，隐藏 Toggle 会重置为未点亮。
- 已收集数量和等待动画数量都会限制在可见 Toggle 数量内，避免配置只允许 1/2 个星时仍点亮或动画播放第 3 个位置。

## 影响文件与资源

- 已修改：`Assets/Game/UI/Postcard/Scripts/Postcard_VictoryStarGroupCtrl.cs`
- 已新增文档：
  - `Assets/Game/UI/Postcard/Scripts/Postcard_VictoryStarGroupCtrl功能说明.md`
  - `Assets/Game/UI/Postcard/Scripts/Postcard_VictoryStarGroupCtrl风险评估.md`
- 未修改：
  - `Assets/Game/UI/Postcard/Scripts/PostcardManager.cs`
  - `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs`
  - `Assets/Game/UI/GameVictory/Resources/GameVictoryPanel.prefab`
  - `Assets/Game/UI/GameVictory/Resources/GameVictoryPanel_Reward.prefab`
  - 保存字段与 Luban 配置

## 风险项

- **显示语义风险**：旧需求要求按 id 位置显示，新需求改为按数量显示；若其他 UI 或设计稿仍按位置理解，会出现验收口径冲突。
- **配置一致性风险**：如果 `LevelVictoryStarCount` 或历史存档已收集数量大于 `LevelSumStarCount`，胜利面板会按 `LevelSumStarCount` 隐藏多余位置；数据仍保留在 `PostcardManager`，但玩家本次只能看到配置允许数量。
- **Prefab 绑定风险**：若 `starToggles` 绑定数量不足或顺序错误，按数量显示仍可能表现不符合预期。
- **隐藏状态残留风险**：组件复用时，如果隐藏 Toggle 未重置可能残留上一关点亮状态；当前实现隐藏时同步 `SetToggleOn(false)`，仍需 PlayMode 验证。
- **动画节奏风险**：当前逻辑保留工作区已有的 `UniTask.WaitForSeconds(3f)` 延迟，需确认胜利面板整体节奏是否接受。
- **关闭清理风险**：脚本通过动画版本号阻止禁用后的旧刷新继续创建 Tween，并通过销毁取消令牌处理对象销毁；仍需 PlayMode 验证关闭胜利面板时没有残留视觉表现。
- **保存失败风险**：等待数据清理失败的内存清理和风险记录由 `PostcardManager` 控制，当前脚本不直接处理保存失败。
- **文档同步风险**：Speckit 已同步为按数量显示，后续如果再回到按 id 位置显示，需要同步回滚 spec/tasks/quickstart。

## 验证证据

- 静态检查目标：
  - 本脚本不直接访问 `DB`。
  - 本脚本不新增 `PlayEvents` / 全局事件。
  - 本脚本不新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 或 `Update`。
  - 本脚本不新增资源加载、广告、IAP、analytics 或平台 SDK 调用。
  - `LevelSumStarCount` 只用于本地 UI Toggle 显隐和显示数量限制，不写入保存数据。
- 已补验证：
  - `git diff --check` 已通过，仅有 LF/CRLF 转换提示。
  - `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 353 warnings。
  - 静态验证 `LevelSumStarCount` 会经 `Mathf.Clamp(levelData.LevelSumStarCount, 0, GetToggleCount())` 限制可见 Toggle 数量。
- 待补验证：
  - PlayMode 验证 `LevelSumStarCount` 为 0/1/2/3/大于 3 时 Toggle 显示数量符合配置限制。
  - PlayMode 验证 `LevelSumStarCount` 小于已收集数量时只点亮配置允许数量，隐藏 Toggle 不残留状态。
  - PlayMode 验证 `[3, 1]` 显示前两个位置。
  - PlayMode 验证重复/非法 id 只影响有效数量。
  - PlayMode 验证等待动画播放最后 N 个可见位置且不重播。
  - PlayMode 验证胜利面板禁用或销毁后不会继续创建延迟动画。
  - 普通/奖励胜利面板 prefab 均可找到星组。
  - Android/iOS 横屏 smoke 无软锁、动画卡死或明显输入停顿。

## 回滚条件

- 胜利面板显示数量与有效收集数量不一致。
- 胜利面板可见 Toggle 数量与 `LevelData.LevelSumStarCount` 不一致。
- 等待动画播放位置与“最后 N 个可见位置”不一致。
- 普通/奖励胜利面板表现不一致。
- 改动导致胜利面板软锁、按钮不可用或动画残留。

## 回滚路径

- 回退 `Postcard_VictoryStarGroupCtrl.cs` 中 `ApplyStarState(...)` 和星 id helper 的数量显示逻辑。
- 如仅回退本次 `LevelSumStarCount` 限制，移除 `GetVisibleToggleCount(...)`、`ApplyToggleVisibility(...)` 以及 `visibleToggleCount` 参数，恢复按 `GetToggleCount()` 控制显示/动画数量。
- 恢复 Speckit 文档中按 saved star id 位置显示的描述。
- 若 prefab 绑定有误，仅回滚或修正对应 prefab 引用，不改 `PostcardManager` 保存结构。

## 2026-07-17 VictoryPanel 星动画 ClickBlock 风险补充

### 事实与控制

- `GameVictoryPanel` 现在用独立布尔状态合并引导阻塞与星动画阻塞；任一状态为 true 时全屏 `clickBlock` 保持 active。
- 星组只在实际动画数量大于 0 时报告正在播放；静态星状态不会增加输入等待。
- 最后一颗星的结束时间按运行态 Animator Controller 最长 clip、Animator speed、错峰间隔和触发等待计算，不使用只覆盖起播延迟的固定总时长。
- 星组禁用或销毁会完成待处理回调；重复 Refresh 会取消旧版本并以新一轮返回值重置 VictoryPanel 阻塞状态。

### 风险与缺失证据

- **P1：Animator 结束口径。** 当前控制器只有 0 秒 `xing` 和 1 秒 `xing2`，取最长 clip 可覆盖可见动画；若未来控制器加入更长的无关 clip，遮罩会相应延后，需要同步调整为指定状态口径。
- **P1：TimeScale 口径。** 星星触发后的 DOTween 使用 unscaled update，Animator 当前使用 Normal update；胜利面板正常流程下预期 TimeScale 为 1，暂停或变速时的释放帧尚未 PlayMode 验证。
- **P2：层级射线。** 两个 Victory prefab 已静态确认绑定全屏 ClickBlock，但普通/奖励面板所有控件的实际 EventSystem 命中仍待 PlayMode 验证。
- C# 构建通过：0 errors / 1062 warnings；未验证 Android/iOS 横屏、多指、快速关闭或面板缓存复用。

### 回滚

- 删除星组完成回调、Animator 时长计算与 `GameVictoryPanel` 星动画阻塞状态，恢复 ClickBlock 仅由引导控制；无需修改 prefab、DB、配置或动画资源。

## 2026-07-20 星动画完成后按 isOn 显示呼吸发光节点风险补充

### 事实与直接结论

- `Postcard_UIStarCollection.prefab` 中 `UI_common_xingfaguang_01` 是星星 Toggle 根节点的直接子物体，默认 inactive。
- 默认 `xing.anim` 明确把该节点写为 inactive；该节点当前本地缩放为 1，并挂载已显式配置 `startScale = 0.5`、`scaleRange = 2` 的 `BreathingScaleHandle`。
- 本次在 `CompleteAnimation(...)` 原完成回调之前执行一次最多 3 个 Toggle 的本地遍历，只对 `toggle.isOn == true` 的星星显示呼吸节点，并明确保持未点亮星星隐藏。
- 对点亮星星禁用根 Animator 后显示呼吸节点，节点首次激活时由现有呼吸组件的 `autoStart` 流程启动；下一次 `Refresh(...)` 会重新启用 Animator 并清掉旧呼吸显示。
- 动画版本失效或组件已禁用时，既有 `IsAnimationVersionActive(...)` 会阻止进入显示步骤；`StopAnimations(true)` 的中断完成通知不调用该步骤。
- 未新增 Update、实例化、资源加载、存档读写、全局事件或平台分支；每个点亮星星会启动 prefab 已有的一个呼吸 Tween，最多 3 个，并由现有组件在禁用/销毁时清理。

### 风险与缺失证据

- **P1：视觉时序。** 显示时刻沿用当前“最长 Animator clip 结束”口径；如果 Controller 后续增加更长的无关 clip，发光节点和 ClickBlock 都会延后，需同步复核完成口径。
- **P2：层级依赖。** 逻辑依赖发光节点保持为 Toggle 的直接子物体且名称不变；缺失或改名时会安全跳过，但对应星星不会发光。
- **P2：Animator 恢复。** 点亮星星在完成后会停用根 Animator 以防默认状态覆盖呼吸节点；下一次 Refresh 已显式重新启用，但仍需 PlayMode 验证同一面板实例跨关卡复用时动画可重播。
- **P2：呼吸重启。** `BreathingScaleHandle` 当前只在 `Start()` 中执行 `autoStart`，禁用时会停止 Tween；若同一胜利面板实例被缓存复用，呼吸节点重新启用后是否会自动恢复仍需 PlayMode 验证。
- **P2：无待播动画路径。** `animCount <= 0` 时会在静态 Toggle 状态赋值后立即同步呼吸节点，但仍返回 false 且不调用动画完成回调；需 PlayMode 确认静态星立即呼吸，同时 VictoryPanel 不增加 ClickBlock 等待。
- C# 工程构建通过：0 error / 1117 个项目既有 warning；`git diff --check` 通过。
- Unity PlayMode、普通/奖励胜利面板的 1/2/3 星实际完成帧、动画中途关闭、面板复用及 Android/iOS 横屏表现尚待验证。

### 回滚

- 删除 `CompleteAnimation(...)` 与无动画分支中的呼吸显示调用、`Refresh(...)` 中的视觉准备调用及对应私有 helper/名称常量，并回退本次呼吸 prefab/Animator/组件序列化调整，即可恢复原行为；无需修改 DB 或配置。

## 2026-10-09 Draw DeepLink 纯数值入口风险

- 原基线：Draw DeepLink 只静态显示最高星；本轮新增 `RefreshStars`，只扩展调用入口，原主线刷新、静态显示和动画实现不改。最高星／待播星来自专用面板已消费的 Draw 数据，不读取 Postcard 存档，不对普通 Draw 组件做共享迁移。
- P1 时钟与生命周期：复用已有 scaled UniTask 等待、unscaled Tween 和 Animator 的既有时钟；暂停、原生覆盖层暂停 PlayerLoop、动画中途关闭和重开仍未在 PlayMode／真机验证。没有新增等待项恢复机制；创建面板时已消费的数据中途关闭不补播，与旧主线／Draw 相同。
- P2 数据与门禁：负数／超过三颗／待播超过最高星夹取；无动画返回 false，面板直接释放星动画门禁。有动画沿原完成回调或禁用／销毁通知释放，不改原 ClickBlock 层级、动画资源或全局事件。
- 证据：当前源码提取方法的内存隔离检查 578 项通过，Editor Bee 定向编译 0 错误／113 警告；Unity／存储／调度／完成驱动为替身，不能证明真实动画或落盘。主线 DeepLink 已使用原刷新入口，没有 Draw 的静态漏接差异。
- 回滚只删除新增 API 和专用面板 Draw 动画调用，保留旧方法、玩家永久星及本日 tips 修改。完整验收与剩余缺口见 [DeepLink 胜利记录](../../GameVictory/GameVictoryPanel_DeepLink功能与风险记录.md)。
