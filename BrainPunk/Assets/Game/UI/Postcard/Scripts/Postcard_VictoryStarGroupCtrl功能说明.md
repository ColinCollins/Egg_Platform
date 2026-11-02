# Postcard_VictoryStarGroupCtrl 胜利星数量显示功能说明

## 需求背景

2026-07-03 需求调整：胜利面板上的明信片胜利星只关心当前关卡已经收集的有效星星数量，不再按照保存的 star id 映射到固定视觉位置。示例：保存数据为 `[3, 1]` 时，UI 显示为前两个星星点亮，而不是点亮第 3 个和第 1 个位置。

2026-07-09 需求补充：胜利面板上的星星 Toggle 显示数量需要受 `LevelData.LevelSumStarCount` 控制，与关卡选择星组的配置数量口径保持一致。

## 影响文件与系统

- `Assets/Game/UI/Postcard/Scripts/Postcard_VictoryStarGroupCtrl.cs`
  - `Refresh(LevelData levelData)` 仍由胜利面板创建或打开流程调用。
  - `ApplyStarState(...)` 改为按有效收集数量刷新静态显示和等待动画。
  - `GetVisibleToggleCount(...)` 和 `ApplyToggleVisibility(...)` 按 `LevelSumStarCount` 控制本次可见 Toggle 数量。
  - `GetCollectedStarIds(...)` 仍通过 `PostcardManager.GetLevelCollectedStarIds(...)` 读取保存的有效 id。
- `Assets/Game/UI/Postcard/Scripts/PostcardManager.cs`
  - 本次未修改，仍负责保存数据读取、等待动画计数消费和保存失败警告。
- `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs`
  - 本次未修改，仍负责找到或缺省消费胜利星组。

## 原有行为基线

- 胜利星组读取保存的 star id 后，按 `starId - 1` 映射到 Toggle 位置。
- 等待动画目标从保存 id 中取最后 N 个有效 id，再映射到对应 Toggle。
- 保存 id 为 `[3, 1]` 时，视觉上会点亮第 3 和第 1 个位置。
- 2026-07-09 之前，胜利星组不读取 `LevelSumStarCount`，只按最多前三个绑定 Toggle 与有效收集数量刷新显示。

## 当前行为

- 保存 id 仍只作为收集状态来源，用于计算唯一有效数量。
- 有效 id 范围仍是 1 到 3，重复 id 和非法 id 不计入显示数量。
- UI 从第一个 Toggle 开始连续显示 `collectedCount` 个点亮状态。
- UI 可见 Toggle 数量先按 `Mathf.Clamp(levelData.LevelSumStarCount, 0, GetToggleCount())` 计算；超过绑定数量时最多显示已绑定的前三个 Toggle，小于等于 0 时隐藏全部 Toggle。
- 隐藏的 Toggle 会同时切回未点亮，避免组件复用时保留上一关视觉状态。
- 有效收集数量和等待动画数量都会被限制在可见 Toggle 数量内；如果配置总星数少于已保存星星数量，只展示配置允许的前 N 个位置。
- 如果存在等待动画数量，则前 `collectedCount - waitingAnimCount` 个 Toggle 静态点亮，最后 `waitingAnimCount` 个可见位置依次播放点亮动画。
- 每次刷新、禁用或销毁都会让旧动画版本失效，避免 3 秒等待期间关闭面板后继续创建后续 Tween。
- 保存 id 为 `[3, 1]` 时，视觉上点亮前两个位置。

## 数据与 UI 流程

1. `GameVictoryPanel` 调用 `Postcard_VictoryStarGroupCtrl.Refresh(levelData)`。
2. 组件根据 `levelData.LevelSumStarCount` 计算本次可见 Toggle 数量，并同步设置 Toggle active 状态。
3. 组件通过 `PostcardManager.ConsumeCollectedStarCount(levelId)` 消费等待动画数量，UI 侧将返回数量限制在可见 Toggle 数量内。
4. 组件通过 `PostcardManager.GetLevelCollectedStarIds(levelId)` 获取当前关卡保存的明信片星 id。
5. 组件统计唯一有效 id 数量，最多显示配置允许且已绑定的 Toggle 数量。
6. 组件根据等待动画数量决定静态点亮数量和动画点亮数量。

## 验证记录

- 已执行静态读取检查，确认本次改动未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Update`、`DB` 直接访问、`PlayEvents` 新事件、资源加载或平台 SDK 调用。
- `git diff --check` 已通过，仅有 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 353 warnings。
- 已执行静态验证：`LevelSumStarCount` 经 `Mathf.Clamp(levelData.LevelSumStarCount, 0, GetToggleCount())` 限制，0/1/2/3/大于 3 的配置会分别受配置和绑定数量限制。
- 待执行 PlayMode：保存 `[3, 1]` 显示前两个星；保存 `[2, 2, 4, -1, 1]` 显示前两个星；等待动画数量 1/2/3 时只动画最后 N 个可见位置。
- 待执行普通胜利面板和奖励胜利面板 prefab 绑定验证。
- 待执行 Android/iOS 横屏 smoke。

## 剩余风险

- 尚未通过 Unity PlayMode 验证 prefab 中 `starToggles` 的绑定顺序和数量。
- 尚未通过 Unity PlayMode 验证 `LevelSumStarCount` 小于已收集数量时胜利星组只展示配置数量，且隐藏 Toggle 不残留点亮状态。
- 当前脚本保留工作区已有的 3 秒等待动画延迟，是否符合最终表现节奏仍需 UI 实机确认。
- 禁用面板会让旧动画版本失效；销毁面板还会取消等待任务。该清理逻辑已通过静态检查和编译，仍待 PlayMode 验证关闭面板时没有残留视觉表现。
- 等待动画保存清理失败的语义依赖 `PostcardManager.ConsumeCollectedStarCount(...)`，本文件只能证明 UI 使用返回的等待数量。

## 回滚路径

如果按数量显示不符合最终表现要求，可将 `ApplyStarState(...)` 恢复为按保存 id 映射 Toggle，并恢复 `NormalizeCollectedStarIds(...)` / `ResolveAnimatedStarIds(...)` 一类按 id 解析动画目标的逻辑；如果仅需回滚 `LevelSumStarCount` 显示限制，则移除 `Refresh(...)` 中可见数量计算、`ApplyToggleVisibility(...)` 调用和 `visibleToggleCount` 对显示/动画数量的限制。同时回滚 Speckit 中“按数量显示”的需求描述。

## 2026-07-17 VictoryPanel 星星动画期间 ClickBlock 拦截

### 需求、基线与影响范围

- 需求：普通及奖励 `GameVictoryPanel` 存在待播放明信片星星动画时，从等待起播到最后一颗星动画结束前均不允许点击。
- 原行为：`clickBlock` 只在明信片引导触发关卡开启；非引导关卡播放星星动画时仍可点击 Next、重玩、分享、NoAds 或奖励版控件。
- 影响文件：`Postcard_VictoryStarGroupCtrl.cs`、`GameVictoryPanel.cs` 及本地说明/风险文档。未修改两个 Victory prefab、Animator Controller、动画 clip、DB、配置、奖励、事件、analytics 或平台 SDK。

### 修改后行为

- `Refresh(...)` 在确有 `LevelWaitingAnimStar` 可播放时返回 true，并接收整组动画完成回调；没有等待动画、数据无效或星组缺失时立即返回 false，不额外阻塞。
- 整组完成点包含 `waitingPlayDelay`、多星 `waitingAnimInterval`、单星触发等待以及最后一颗星 Animator Controller 中最长 clip 的实际时长。当前 `Postcard_UIStarCollection` 的 `xing2` clip 静态资源时长为 1 秒。
- `GameVictoryPanel` 分别记录明信片引导阻塞与星动画阻塞，`clickBlock` 仅在两者都结束后关闭。引导 Stage One 结束不会误释放尚未播完的星动画遮罩，星动画结束也不会误释放仍在进行的引导遮罩。
- 星组禁用或销毁会停止本轮 Tween 并通知调用方释放星动画阻塞，避免面板复用或异常隐藏后留下输入软锁。数据消费与星星授予流程未改变。

### 验证、风险与回滚

- 已静态核对两个 Victory prefab 均绑定 `clickBlock` 和 `postcard_starGroup`；星组 Animator Controller 绑定 `xing2`，资源时长为 1 秒。
- `dotnet build Assembly-CSharp.csproj` 通过：0 errors / 1062 warnings。首次 `--no-restore` 因 Unity `Temp/obj/Assembly-CSharp/project.assets.json` 缺失未进入代码编译，正常 restore/build 后通过。
- 尚未在 Unity PlayMode 验证 1/2/3 颗星的实际遮罩释放帧、普通/奖励胜利面板全部按钮射线、引导与星动画重叠、快速关闭和复用；Android/iOS 横屏触摸未验证。
- 回滚本次行为时，恢复 `Refresh(LevelData)` 无回调入口和原星动画调度，并删除 `GameVictoryPanel` 的星动画阻塞状态；引导专用 `clickBlock` 可独立保留，不涉及存档或资源迁移。

## 2026-07-20 星动画完成后按 isOn 显示呼吸发光节点

### 需求与原有行为

- 需求：`Postcard_VictoryStarGroupCtrl` 播放完本轮全部 star 动画后，只显示 `isOn == true` 星星对应的 `breathGameObject`；当前 prefab 中该对象名为 `UI_common_xingfaguang_01`。
- 第一版误把所有受控星星的发光节点统一设为 active，没有按 Toggle 点亮状态筛选；同时只执行 `SetActive(true)`，仍会被默认 `xing` Animator 状态写回 inactive。

### 修改后行为与影响范围

- `CompleteAnimation(...)` 在动画版本仍有效时遍历现有 `starToggles`：`toggle.isOn` 为 true 时显示对应 `UI_common_xingfaguang_01`，为 false 时保持隐藏，再调用原完成回调。
- 对需要显示呼吸节点的星星，完成步骤会禁用该星星根节点 Animator，避免已返回默认 `xing` 状态后继续把呼吸节点写成 inactive；下一次 `Refresh(...)` 会重新启用 Animator 并隐藏旧呼吸节点，保证后续动画仍能正常触发且面板复用不残留。
- `Postcard_UIStarCollection.prefab` 已为呼吸节点挂载现有 `BreathingScaleHandle`；节点本地缩放为 `Vector3.one`，呼吸基准 `startScale` 为 `0.5`，`scaleRange` 为 `2`，节点首次激活时由组件既有 `autoStart` 流程在 `0.5~1` 之间呼吸。
- `animCount > 0` 时仍在整组动画自然到达完成出口后执行；`animCount <= 0` 时在静态 Toggle 状态赋值完成后立即执行一次，确保没有新 starCollect 动画时已有 `isOn` 星星也显示呼吸节点。
- 无动画分支仍返回 `false`，不调用 `animationComplete`，不会让 `GameVictoryPanel` 误判为存在星动画或增加 ClickBlock 等待；刷新替换、组件禁用或销毁触发的中断清理也不会额外调用完成回调。
- 缺少对应子节点或 Toggle 为空时跳过，不中断其他星星处理，也不影响完成回调和 ClickBlock 释放。
- 相关改动包含 `Postcard_VictoryStarGroupCtrl.cs`、`BreathingScaleHandle.cs`、`Postcard_UIStarCollection.prefab`、对应 Animator Controller 与 `xing2.anim`；普通 `GameVictoryPanel.prefab` 仅补齐共享呼吸组件的 `startScale = Vector3.one` 兼容值。未修改存档、配置、资源加载、事件、analytics 或平台 SDK。

### 验证、剩余风险与回滚

- 静态确认 `Postcard_UIStarCollection.prefab` 的 `UI_common_xingfaguang_01` 是 Toggle 根节点的直接子物体，默认 `m_IsActive: 0`、本地缩放为 1，并挂有 `startScale = 0.5`、`scaleRange = 2` 的现有 `BreathingScaleHandle`。
- 静态确认默认 `xing.anim` 会把该节点写为 inactive，说明完成后若不释放 Animator 控制，脚本设置会被覆盖。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过：0 error / 1117 个项目既有 warning；`git diff --check` 已通过。
- Unity PlayMode 的 1/2/3 星动画完成表现、动画中途关闭及 Android/iOS 横屏表现未执行，继续作为未验证风险保留。
- Unity PlayMode 还需覆盖 `animCount == 0` 且存在 1/2/3 个静态 `isOn` 星星时呼吸节点立即显示，同时确认 `Refresh(...)` 返回 false、胜利面板不增加输入阻塞。
- 回滚时删除 `StarBreathObjectName`、`PrepareStarVisuals()`、`ShowEnabledStarBreathObjects()` 及 `CompleteAnimation(...)` 中对应调用即可，不需要迁移 prefab 或数据。

## 2026-10-09 Draw DeepLink 纯数值动画入口

- `GameVictoryPanel_DeepLink` 的 Draw 分支此前只调用静态星 API，丢弃了被消费的待播数量；新增 `RefreshStars(int collectedStarCount, int waitingAnimStarCount, Action animationComplete)` 接收面板已缓存的最高星与待播星，复用当前 `ApplyStarState`、Animator、音效、呼吸收尾与生命周期清理。
- 新 API 仅在 PlayMode 执行，不读取／消费 DB、Postcard 或 Bag，不修改永久星；最多三个有效 Toggle，待播数限制在已获得数量内。无待播星返回 false 且不通知回调，有待播星返回 true 并在原完成边界通知面板。
- 原主线 `Refresh(LevelData)` 和静态星 API 没有修改；主线 DeepLink 仍调用原 `Refresh(LevelData)`，已具备新增星动画。普通 Draw 继续使用独立 `DrawGame_VictoryStarGroup`，无需迁移 Prefab 或修改资源。
- 修改前核对、来源、验证矩阵与回滚详见 [DeepLink 胜利记录](../../GameVictory/GameVictoryPanel_DeepLink功能与风险记录.md) 的 2026-10-09 Draw 新增星章节。当前源方法隔离 578 项断言通过，Bee Editor 定向编译 0 错误／113 警告；真实 Unity 动画／关闭／移动端为 `uncertain`。
- 回滚仅移除该 API 和 Draw DeepLink 对应调用，不改旧 API 或玩家星级。
