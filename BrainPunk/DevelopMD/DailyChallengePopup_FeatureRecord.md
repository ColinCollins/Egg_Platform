# DailyChallengePopup 功能记录

## 需求与背景

- `DailyChallengePopup` 仅在成功结算链中自动出现，没有详情入口或其它打开途径。
- 弹出顺序固定为 `RatingPopup -> DailyChallengePopup -> CommonGetRewardPopup（达成奖励时）-> GameVictoryPanel`。
- 当日首次通过一个尚未记录在 `PassedLevels` 的关卡时增加 1 点累计进度；重复通关不增长。Popup 中央数字展示累计进度，两个奖励节点分别展示各自阶段目标，长进度条中两个阶段各占一半。
- 达到阶段节点并成功打开 `DailyChallengePopup` 后，由 Manager 立即选择并发放普通奖励、记录领取状态；进度与勾选动画完成后再由 `CommonGetRewardPopup` 只展示同一份奖励数据。第二阶段优先从随机 Skin 奖励池抽取未拥有 Skin；数量奖励的广告倍率继续由通用奖励弹窗处理。

## 影响文件与系统

- `Assets/Game/Scripts/Play/PlayCtrl_Nodes/PlayCtrl_Success.cs`
  - 在 SUCCESS 状态入口记录本次每日挑战进度。
  - Rating 结束后按结算结果打开 DailyChallengePopup，关闭后继续 VictoryPanel。
  - 增加一次性继续保护，避免异常路径重复推进胜利面板。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeManager.cs`
  - 增加胜利前后只读快照、待领取阶段计算、奖励数据构建、普通奖励发放和领奖状态保存。
  - 第二阶段只向 `SkinUtils` 请求随机未拥有 Skin，不直接解析随机池。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs`
  - 打开时请求 Manager 发放待领奖励，缓存展示数据，随后展示快照、播放进度/数字/勾选动画并串联通用奖励弹窗。
  - 进度数字变化时先缩放到 `1.2` 再回到 `1`；阶段勾选由 `2` 缩放到 `1`。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeStepItem.cs`
  - 在原纯展示刷新能力上增加可复用的领奖勾选缩放动画。
- `Assets/Game/UI/DailyChallenge/Resources/DailyChallengePopup.prefab`
  - 根节点增加透明全屏射线和 `CustomButton`，用于非奖励结算后的关闭，并阻止点击穿透。
- `Assets/Game/Scripts/Skins/SkinUtils.cs`
  - 增加 Skin 配置存在、玩家持有、随机奖励可发放性和按权重抽取接口。
- `Luban/Configs/Datas/__tables__.xlsx`
  - 注册已有 `RandomSkinRewardPool.xlsx` 为 `TbRandomSkinRewardConfig`。
- `Assets/Configs/Code/LubanConfig/RandomSkinRewardConfig.cs`、`TbRandomSkinRewardConfig.cs`、`Assets/Configs/Code/Tables.cs`
  - 由 Luban 生成随机 Skin 奖励池模型和表入口。
- `Assets/Game/Configs/lubanconfig_tbrandomskinrewardconfig.json`、`Assets/Game/Scripts/Config/ConfigFileNames.cs`
  - 增加运行时随机奖励池数据和加载文件名。
- `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
  - 增加两阶段金币奖励读取入口，运行时优先 RemoteConfig，缺失时回退已有 GlobalConst 值。

`Luban/Configs/Datas/Const/Global.xlsx` 本次未修改。

## 原有行为基线

- `DailyChallengePopup` 只有空生命周期骨架，没有状态刷新、动画、奖励或关闭编排。
- 成功流程从 Rating 直接进入 VictoryPanel，不记录每日挑战进度。
- `DailyChallengeManager` 只提供入口展示快照和跨日清理，不生成胜利结算或奖励。
- `RandomSkinRewardPool.xlsx` 已存在，但没有注册到 `__tables__.xlsx`，因此运行时 Tables 无法加载。
- `SkinUtils` 原本只集中 Skin 图标和 prefab 资源解析/加载，不处理配置存在性、拥有状态或随机奖励候选。

## 实现后行为

1. SUCCESS 进入时，Manager 在功能开启、节点配置有效、GameSchedule 就绪、达到解锁关卡且当前 `levelId` 尚未存在于 `PassedLevels` 时记录 1 点进度并立即保存；重复通关不增长。
2. Manager 返回保存前后快照；配置无效、未解锁、保存失败，或总进度完成且无待领取奖励时不打开 Popup。
3. Rating 结束后打开 DailyChallengePopup；无 Rating 时直接进入该判断。
4. Popup 先展示旧累计进度 1 秒，再更新累计数字并播放 `1.2 -> 1` 的轻微缩放；数字动画结束后，长进度条和标记才从旧累计位置补间到新累计位置。跨阶段时中央数字持续累计，不归零。
5. 达到奖励节点时，对应 `DailyChallengeStepItem` 显示勾选并播放 `2 -> 1` 缩放。
6. `DailyChallengePopup.SetData(...)` 收到结算快照后，立即让 Manager 为所有待领取阶段按顺序选择奖励、发放 normal Coin/Star/Skin，并记录阶段已领取。
7. 第一阶段使用金币奖励；第二阶段从当前随机 Skin 奖励池按正权重抽取未拥有且可发放的 Skin。数量奖励统一通过 `SimpleBag.GrantRewards(...)` 发放，Skin 通过 `SimpleBag.GetNewSkin(...)` 发放。
8. Popup 缓存 Manager 已发放的 `DailyChallengeRewardData`；对应阶段动画完成后调用 `CommonGetRewardPopup.Create(..., grantNormalReward: false)`，只展示同一份奖励数据并保留广告额外数量奖励流程。
9. 通用奖励弹窗关闭后继续下一个待展示阶段或关闭 DailyChallengePopup；展示 Popup 创建失败不回退已经完成的普通发奖和领奖标记。
10. DailyChallengePopup 关闭后进入 GameVictoryPanel。奖励创建或普通发放失败时记录警告并停止后续阶段，避免在数据不完整时继续发奖。

## 数据、配置、资源与事件流

```text
PlayCtrl_Success
    -> DailyChallengeManager.TryRecordVictory()
    -> GameSchedule.DailyChallengeCurrentProcess + 1
    -> DB.SaveGameSchedule()
    -> RatingPopup
    -> DailyChallengePopup（前后快照动画）
        -> 打开时 DailyChallengeManager.TryClaimRewardOnPopupOpen(stageId)
            -> TryCreateRewardData(stageId)
            -> 阶段 Coin 配置
            -> SkinUtils.TryGetRandomUnownedRewardSkin("2")
                -> TbRandomSkinRewardConfig
                -> TbSkin + DB.GameData.OwnedSkin 过滤
            -> SimpleBag.GrantRewards(Coin/Star, RewardType.Other)
            -> SimpleBag.GetNewSkin(Skin)
            -> DailyChallengeHasRewardId + DB.SaveGameSchedule()
        -> 阶段动画完成
        -> CommonGetRewardPopup(grantNormalReward: false)
            -> 只展示已发放普通奖励
            -> 无 Skin 时可选广告追加 1 倍 Coin
    -> GameVictoryPanel
```

- 没有新增 `PlayEvents`、全局事件、每帧轮询、PlayerPrefs、analytics、IAP 或新的平台分支。
- `SkinUtils` 新增的配置和存档查询入口均以 `Application.isPlaying` 为外层门禁，不在编辑态初始化 ConfigManager 或访问 DB。
- 随机选择只返回候选；实际普通 Skin 授予和保存由 `DailyChallengeManager -> SimpleBag.GetNewSkin(...)` 在 DailyChallengePopup 打开时完成。
- 当前奖励池中的 `202016`、`203009` 均能在 `tbskin.json` 中找到，且 `unlockType` 为 Special。

## 验证证据

- Luban 使用当前 `luban.conf` 生成成功；随机奖励池生成类、Tables 入口和运行时 JSON 已成套产生。
- `dotnet build Assembly-CSharp.csproj --no-restore` 通过，0 个编译错误；输出警告均来自项目既有代码或依赖。
- `DailyChallengePopup.prefab` 静态 YAML 检查：82 个本地 anchor，无重复 fileID，无缺失本地引用。
- 随机奖励池 JSON 静态核对：三个配置 id 均包含 `203009;10` 和 `202016;10`；两个 Skin id 均存在于 Skin 表。
- 静态职责检查确认随机池解析只位于 `SkinUtils`，Manager 只调用其公开抽取接口。

## 剩余风险与未验证项

- 尚未在 Unity PlayMode 实测完整链路、实际动画节奏、点击关闭、连续两个待领取阶段和 Popup 创建失败降级。
- 尚未实测广告成功、失败、取消以及无广告库存时的 2 倍按钮行为；该部分复用 `CommonGetRewardPopup` 既有逻辑。
- 普通奖励已经发放但 `GameSchedule` 保存失败时，本会话会保留领奖 id，重启后仍存在重复发奖风险；当前只记录警告，没有新增跨 GameData/GameSchedule 的事务系统。
- 尚未在 Android/iOS 横屏验证安全区、触摸射线、弹窗层级和不同宽高比下的进度标记位置。
- 当前阶段与奖励池的映射约定第二阶段使用配置 id `"2"`；若产品改为独立字符串 key，应新增明确配置字段，不能继续依赖阶段 id 转字符串。

## 回滚路径

- 从 `PlayCtrl_Success` 移除结算快照和 DailyChallengePopup 编排，即可恢复 Rating 后直接进入 VictoryPanel。
- 回滚 `DailyChallengePopup.cs`、`DailyChallengeStepItem.cs` 的动画扩展及 Popup prefab 根点击组件，可恢复空 Popup/纯展示 Item。
- 从 `__tables__.xlsx` 移除随机池注册项后重新运行 Luban，并同步移除生成模型、Tables 属性、运行时 JSON和 `ConfigFileNames` 条目。
- 回滚 `SkinUtils` 新增配置/拥有/抽取 API 与 Manager 奖励构建逻辑，不影响原有 Skin 资源加载 API。
- GameSchedule 沿用已有字段名称和类型，不需要额外存档迁移。

## 2026-07-21：进度游标与长进度条同步

### 需求与原有行为基线

- `DailyChallengePopup.prefab` 已将原导航标记整理为 `cursor`，并与 `processBar` 放在同一父节点下；两者的横向锚点均位于父节点中心。
- 原脚本通过自动生成的图片引用移动导航标记，没有把新增的 `cursor` 序列化引用作为明确的进度游标入口。

### 影响文件与新行为

- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs`
  - 静态刷新和进度补间统一驱动 `cursor.anchoredPosition.x`，保留游标原有 `y`。
  - 横向目标位置按 `processBar.rect.xMin + process * processBar.rect.width` 计算，使中心锚点布局下的 `0%`、`100%` 分别对应进度条左右端。
  - `progressFill`、`processBar`、`cursor` 必须由 prefab Inspector 直接绑定，不再按子节点名称动态查找，节点改名不会使同步引用失效。
- `Assets/Game/UI/DailyChallenge/Resources/DailyChallengePopup.prefab`
  - 直接绑定进度填充图片、`processBar` RectTransform 和 `cursor` RectTransform。

### 数据、资源与事件流

```text
DailyChallengeViewState.ExpandedNormalizedProgress
    -> Clamp01
    -> progressFill.fillAmount / DOFillAmount
    -> processBar 左端 + process * processBar.width
    -> cursor.anchoredPosition.x / DOAnchorPosX
```

- 不新增存档、配置、资源加载、全局事件、analytics 或平台分支；只改变弹窗内游标的显示同步。
- 计算仅在状态应用和结算补间时执行，没有新增 `Update` 或持续 GC 路径。

### 验证、剩余风险与回滚

- 已进行 C# 工程编译、差异格式检查和 prefab YAML 静态绑定检查。
- 尚未在 Unity PlayMode 与 Android/iOS 横屏设备实测 0%、阶段跨越、100% 以及不同 Canvas 缩放下的最终视觉位置。
- 回滚时恢复脚本使用原导航标记的定位逻辑，并移除本节记录即可；无需迁移存档、配置或资源。

## 2026-07-21：结算流程完成后自动关闭

### 需求与原有行为基线

- 原行为在没有待领取阶段奖励时，进度动画完成后仍需点击弹窗全屏关闭区域才能退出。
- 有待领取阶段奖励时，弹窗会在阶段动画和通用奖励弹窗流程结束后自动退出。

### 影响文件与新行为

- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs`
  - 移除 `closeArea` 序列化字段、点击监听和 `_canClose` 等待状态。
  - 新增 Inspector 配置 `autoCloseDelay`，默认 1 秒，允许设为 0 立即关闭。
  - 没有阶段奖励时，进度动画完成并等待 `autoCloseDelay` 后调用现有 `CompleteAndClose()`；有阶段奖励时在全部奖励流程结束并等待相同时间后关闭。
- `Assets/Game/UI/DailyChallenge/Resources/DailyChallengePopup.prefab`
  - 不再保存 `closeArea` 引用；沿用当前已移除根节点全屏点击组件的 prefab 结构。

### 数据、资源与事件流

- 不新增存档、配置、资源加载、全局事件、analytics 或平台分支。
- `PlayCtrl_Success` 仍通过 `ShowAndWaitAsync` 等待 `DailyChallengePopup.OnClose` 完成，变化仅是正常流程完成后等待可配置时间并自动进入后续流程，无需玩家点击。

### 验证、剩余风险与回滚

- 已进行静态调用检查、prefab YAML 配置检查与 `git diff --check`；`dotnet build Assembly-CSharp.csproj --no-restore --nologo` 通过，0 个错误、99 个项目既有警告。
- 尚未在 Unity PlayMode 和 Android/iOS 横屏设备实测无奖励与有奖励两条关闭路径。
- 回滚时恢复 `closeArea`、点击监听和 `_canClose` 等待分支，并在 prefab 根节点恢复对应点击组件。

## 2026-07-21：进度数字缩放参数开放配置

### 需求与原有行为基线

- 原进度数字变化动画固定从 1.2 倍缩放回弹至原始大小，持续 0.18 秒，prefab Inspector 无法调整。

### 影响文件与新行为

- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs`
  - 将原常量改为序列化字段 `numberStartScale` 和 `numberScaleDuration`，分别控制起始缩放倍数和回弹时长。
  - 两个参数均限制为非负值；默认值保持原效果不变。
- `Assets/Game/UI/DailyChallenge/Resources/DailyChallengePopup.prefab`
  - 当前 Inspector 配置保存为 `numberStartScale: 1.5`、`numberScaleDuration: 0.3`；脚本字段默认值仍为原效果的 1.2 和 0.18 秒。

### 数据、资源与事件流

- 参数只在总进度数字发生变化时传入现有 DOTween 缩放动画，不新增存档、配置表、资源加载、事件或每帧逻辑。

### 验证、剩余风险与回滚

- 已通过 `git diff --check`、prefab YAML 字段检查和 `dotnet build Assembly-CSharp.csproj --no-restore --nologo`；编译结果为 0 个错误、99 个项目既有警告。
- 尚未在 Unity PlayMode 和 Android/iOS 横屏设备验证不同参数下的视觉节奏。
- 回滚时恢复两个动画常量，并移除 prefab 对应序列化值。

## 2026-07-21：阶段领奖前置动画与特效

### 需求与原有行为基线

- 原流程在阶段达成后仅播放 0.25 秒的已领取勾选缩放，随后立即创建 `CommonGetRewardPopup`。
- 新需求要求领奖时让对应阶段的 `mainIcon` 抖动并弹性放大缩小 1.5 秒，同时在该图标位置播放 `rewardEffects`，动画完成后才弹出奖励窗口。

### 影响文件与新行为

- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeStepItem.cs`
  - 新增 prefab 直接绑定的 `mainIcon` RectTransform；`rightIcon` 继续只承担原有已领取状态显示。
  - `PlayClaimedAnimation` 改为 1.5 秒内同时执行旋转抖动和“放大—回弹—恢复”缩放；动画完成或中断时恢复初始姿态。
  - 提供只读的 `RewardEffectWorldPosition`，供所属弹窗把特效定位到当前阶段图标。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs`
  - 进入对应阶段领奖动画时，把 `rewardEffects` 中所有有效粒子移动到 `mainIcon` 世界坐标，清理旧粒子后重播。
  - 等待 1.5 秒 Tween 完成后才进入既有奖励数据创建和 `CommonGetRewardPopup` 流程。
  - 弹窗打开和关闭时停止并清理粒子，避免复用时残留。
- `Assets/Game/UI/DailyChallenge/Prefabs/DailyChallengeStepItem.prefab`
  - `mainIcon` 直接绑定到当前主图标 RectTransform，不依赖节点名称。
- `Assets/Game/UI/DailyChallenge/Resources/DailyChallengePopup.prefab`
  - 已保存 `rewardEffects` 序列化列表入口；当前 prefab 没有可静态确认的 ParticleSystem 对象，因此列表仍为空，待实际特效对象加入后在 Inspector 绑定。

### 数据、资源与事件流

```text
PendingRewardStageIds
    -> 对应 DailyChallengeStepItem
    -> rewardEffects 定位并 Play
    -> mainIcon 1.5 秒抖动与弹性缩放
    -> TryCreateRewardData
    -> CommonGetRewardPopup
```

- 不改变奖励数据生成、领取标记、存档、配置、资源加载、全局事件、analytics 或平台分支。
- 动画只在阶段领奖时创建一次 DOTween Sequence，不新增 `Update` 或持续 GC 路径。

### 验证、剩余风险与回滚

- 已通过 `git diff --check`、静态时序/禁止名字查找扫描、prefab YAML 绑定检查和 `dotnet build Assembly-CSharp.csproj --no-restore --nologo`；编译结果为 0 个错误、99 个项目既有警告。
- 当前缺少已绑定的实际 `rewardEffects` 粒子对象，粒子视觉效果无法静态或 PlayMode 验证；`mainIcon` 动画节奏及 Canvas 粒子层级也尚未在 Unity PlayMode、Android/iOS 横屏设备验证。
- 回滚时恢复原 `ClaimedScaleDuration` 与勾选缩放逻辑，移除 `mainIcon`、`rewardEffects` 播放和 prefab 新增绑定；奖励数据与存档无需迁移。

## 2026-07-21：阶段目标区域始终显示

### 需求、原因与修复行为

- `DailyChallengeStepItem.prefab` 当前把 `count_txt` 放在原 `claimedBackground` 节点下面。
- 原 `Refresh()` 在奖励未领取时关闭 `claimedBackground`，导致其子节点 `count_txt` 即使自身保持 Active，也会随父节点一起隐藏。
- `Refresh()` 现始终启用该目标区域父节点，只根据 `isRewardClaimed` 切换 `claimedCheck/rightIcon`；目标底板与目标次数在未领取、领奖动画中和已领取状态下都保持显示。
- 同步修正序列化字段 Tooltip，说明该节点当前承担目标区域父节点职责；旧注释已与 prefab 层级事实冲突，因此不再描述为“仅已领取时显示”。

### 影响范围、验证与回滚

- 仅修改 `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeStepItem.cs` 的局部显隐逻辑；不改变目标数值、奖励判断、存档、配置、事件、资源或平台流程。
- 已通过 `git diff --check` 和 `dotnet build Assembly-CSharp.csproj --no-restore --nologo`；编译结果为 0 个错误、99 个项目既有警告。Unity PlayMode 和 Android/iOS 横屏视觉仍待验证。
- 回滚时把 `claimedBackground` 显隐恢复为 `isRewardClaimed`，但必须同时把 `count_txt` 移出该父节点，否则未领取状态会再次隐藏目标次数。

## 2026-07-21：Step2 目标数字显示累计配置值

### 需求、原有行为与修复

- `GetDailyChallengeSteps()` 返回累计目标，例如 `goals = [4, 8]`；Manager 内部为第二阶段进度计算得到增量目标 `8 - 4 = 4`。
- 原 `DailyChallengeViewState.SecondStageTarget` 错误保存了该增量值，导致 Step2 的 `countTxt` 显示 4，而不是配置原始累计目标 `goals[1] = 8`。
- `CreateViewState()` 现把内部 `finalTarget = firstStageTarget + secondStageTarget` 传给 UI 的 `SecondStageTarget`，因此 Btn 与 Popup 的 Step2 均显示 `goals[1]`。

### 数据流与影响范围

```text
GlobalConst / RemoteConfig goals[1]
    -> TryGetStageTargets 内部拆分阶段增量
    -> finalTarget 还原累计总目标
    -> DailyChallengeViewState.SecondStageTarget
    -> DailyChallengeStepItem countTxt
```

- 第二阶段内部进度仍按 `(TotalProgress - goals[0]) / (goals[1] - goals[0])` 计算；最终完成条件、长进度条、奖励达成、存档和配置读取均未改变。
- 影响 `DailyChallengeBtn` 和 `DailyChallengePopup` 共用的 Step2 展示，不影响 Step1。

### 验证、风险与回滚

- 已通过 `git diff --check`、`SecondStageTarget` 使用点扫描和 `dotnet build Assembly-CSharp.csproj --no-restore --nologo`；编译结果为 0 个错误、99 个项目既有警告。Unity PlayMode 与 Android/iOS 横屏显示仍待验证。
- 回滚时把 `CreateViewState()` 传入的 Step2 展示值恢复为阶段增量值；无需迁移存档或配置。

## 2026-07-20：SUCCESS 结算改为 UniTask 串行编排

### 需求与原有基线

- 原 `PlayCtrl_Success.VictoryProcess()` 通过 RatingPopup 的关闭 callback 进入 `ShowDailyChallengeOrVictory(...)`，再通过 DailyChallengePopup 的关闭 callback 进入 `ShowVictory(...)`。
- 玩家可见顺序已经正确，但控制流拆散在两层 callback 中；SUCCESS 状态退出时也没有统一取消仍在等待的弹窗流程。
- 本次只调整编排结构，必须保持 `RatingPopup -> DailyChallengePopup -> CommonGetRewardPopup -> GameVictoryPanel` 的既有条件和顺序不变。

### 影响文件与新行为

- `Assets/Game/Scripts/Play/PlayCtrl_Nodes/PlayCtrl_Success.cs`
  - `OnEnter()` 创建本轮专用 `CancellationTokenSource`，以唯一 `.Forget()` 边界启动 `RunVictoryProcessAsync(...)`。
  - 主流程依次 `await RatingPopup.ShowAndWaitAsync(...)`、`await DailyChallengePopup.ShowAndWaitAsync(...)`，最后调用 `ShowVictory(...)`。
  - `OnExit()` 取消本轮流程；取消只终止后续结算，不修改已经记录的每日挑战进度或已经发放的奖励。
  - 移除 `ShowDailyChallengeOrVictory(...)` 和无业务内容的插屏结果 callback；插屏触发条件与调用时机不变。
- `Assets/Game/UI/RatingPopup/Scripts/RatingPopup.cs`
  - 新增 `ShowAndWaitAsync(...)`，使用 `UniTaskCompletionSource` 等待既有 `OnClose()` 生命周期。
  - 外部取消时销毁仍打开的 RatingPopup；原 `Create(...)` 和评分完成 callback API 保留，GM 等已有入口不受影响。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs`
  - 唯一公开入口调整为 `ShowAndWaitAsync(settlement, token)`。
  - 用关闭完成源替代 `_closeCallback`；内部进度、勾选、CommonGetRewardPopup 和领奖保存流程不变。
  - 外部取消时销毁仍打开的 DailyChallengePopup，使内部基于 GameObject 销毁令牌的动画和奖励等待停止。

### 流程与边界

```text
PlayCtrl_Success.OnEnter
    -> RunVictoryProcessAsync(SUCCESS token)
        -> 原成功计数 / 插屏触发 / DailyChallenge 进度记录 / LevelEndEvent
        -> 可选 await RatingPopup.OnClose
        -> 可选 await DailyChallengePopup.OnClose
            -> 原 CommonGetRewardPopup 奖励流程
        -> ShowVictory

PlayCtrl_Success.OnExit
    -> cancel SUCCESS token
    -> 当前可等待 Popup 若仍打开则销毁
    -> 不再执行后续 Popup 或 ShowVictory
```

- 不修改 DailyChallengeManager、GameSchedule、Luban/RemoteConfig、prefab、资源、奖励倍率、广告实现、analytics 参数或平台分支。
- CommonGetRewardPopup 继续使用其现有 callback API；本次只清理 PlayCtrl_Success 的主结算编排，避免扩大到 Postcard 和 GM 调用链。
- `_hasContinuedToVictory` 继续保留为最终防重复保护，没有因线性 await 流程删除。

### 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore`：通过，0 个编译错误。
- 静态调用检查确认 `PlayCtrl_Success` 只调用两个 `ShowAndWaitAsync(...)`；已不存在 `ShowDailyChallengeOrVictory`、DailyChallengePopup callback 创建入口或空 `OnInterstitialCallback`。
- 尚未在 Unity PlayMode 验证 Rating 显示/跳过、DailyChallenge 显示/跳过、外部切换状态时 Popup 清理，以及 Android/iOS 系统评分返回时序。
- 回滚时恢复 PlayCtrl_Success 的两层关闭 callback，移除两个 Popup 的 `ShowAndWaitAsync`/关闭完成源，并恢复 DailyChallengePopup 的 `_closeCallback` 即可；数据、配置和 prefab 不需要回滚。

## 2026-07-20：旧进度展示时间调整为 1 秒

### 需求与行为变化

- 原行为在应用结算前快照后同一帧立即启动进度补间，旧进度没有独立的静态展示窗口。
- 现在仅当本次 `TotalProgress` 确实增长时，先展示旧进度 1 秒，再播放原有进度条、数字和阶段勾选动画。
- 只有待补发奖励而没有进度增长时继续直接进入领奖流程，不额外等待 1 秒。

### 影响范围、验证与回滚

- 仅修改 `DailyChallengePopup.cs` 的表现时序；不修改 Manager、存档、配置、Prefab、奖励、广告、事件或成功结算顺序。
- 等待使用 unscaled time，并绑定 Popup 的销毁取消令牌；弹窗销毁或 SUCCESS 状态退出时不会继续播放后续动画。
- 已执行 C# 工程构建和差异格式检查；Unity PlayMode、Android/iOS 横屏下的实际 1 秒节奏仍待验证。
- 回滚时移除 `PreviousProgressHoldDuration` 及进度动画前的延迟等待即可恢复原时序。

## 2026-07-20：累计数字展示与动画顺序调整

### 需求与新行为

- `ui_daily_03_txt` 原先读取 `CurrentStageProgress`，进入第二阶段后会从阶段内零值重新计数；现在统一读取 `TotalProgress`，展示当日累计值。
- 有进度增长时保持 `旧累计状态 1 秒 -> 累计数字更新并缩放 -> 长进度条与标记增长 -> 阶段勾选/奖励` 的串行顺序。
- 无进度增长但存在待领取奖励时继续直接应用最新累计状态，不播放数字或进度增长动画。
- 原阶段边界专用的 `GetAnimatedStageProgress(...)` 已无用途并删除；Btn 的阶段内数字逻辑不变。

### 影响范围、验证与回滚

- 仅修改 `DailyChallengePopup.cs` 的显示值和动画编排；不修改 Manager、存档、配置、Prefab、奖励、广告、事件或 SUCCESS 弹窗顺序。
- 已执行 C# 工程构建、旧阶段值调用扫描和差异格式检查；Unity PlayMode 与 Android/iOS 横屏实际节奏仍待验证。
- 回滚时让 `ApplyState()` 恢复读取 `CurrentStageProgress`，并把数字缩放等待移回进度补间之后即可；无需迁移存档。

## 2026-07-20：重复通关不再累计进度

### 需求与新行为

- 原 `TryRecordVictory(...)` 只检查每日挑战是否完成，没有判断本次关卡是否已经通过，因此重复完成同一 `levelId` 仍会增加累计进度。
- SUCCESS 流程调用 Manager 时，`Level.Victory()` 尚未把本关写入 `PassedLevels`；Manager 现在以 `DB.GameData.PassedLevels.Contains(currentLevel)` 判断是否首次通过。
- 当前 `levelId` 已存在时不增长、不保存新的进度；如果已有阶段达到但奖励尚未领取，仍返回结算快照继续补发奖励。
- `PassedLevels` 不可用时按不可确认首次通关处理，不增长进度，避免异常存档状态下重复累计。

### 影响范围、验证与回滚

- 仅修改 `DailyChallengeManager.TryRecordVictory(...)` 的增长条件及冲突注释；Popup 的累计数字、动画顺序、奖励和 SUCCESS 编排不变。
- 已执行 C# 工程构建、调用顺序静态核对和差异格式检查；Unity PlayMode 中的首次通关、重复通关及重复通关补领奖仍待验证。
- 回滚时移除 `PassedLevels` 判断并恢复只按最终目标判断即可；不需要迁移既有进度存档。

## 2026-07-21：最终阶段随机衣服奖励池改为配置驱动

### 需求与原有行为基线

- `Global.xlsx` 已新增整数字段 `dailyChallengeRandomSkinPoolId`，当前本地值为 `1`，用于指定每日挑战最终阶段的 `RandomSkinRewardConfig` 奖励池。
- 原 `DailyChallengeManager.TryCreateRewardData(...)` 在第二阶段直接把 `stageId.ToString()` 传给 `SkinUtils.TryGetRandomUnownedRewardSkin(...)`，因此奖励池固定随第二阶段 id 解析为 `"2"`，无法由 GlobalConst 或 RemoteConfig 独立调整。
- 原有金币奖励、无可用 Skin 时的 2 倍金币入口、通用奖励弹窗实际发放和领奖状态保存流程保持不变。

### 影响文件与新行为

- `Assets/Configs/Code/LubanConfig/GlobalConst.cs`、`Assets/Game/Configs/lubanconfig_tbglobalconst.json`
  - 通过项目 Luban 生成流程同步新增字段，运行时本地配置可读取 `DailyChallengeRandomSkinPoolId = 1`。
- `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
  - 新增 `dailyChallengeRandomSkinPoolId` 字段 key 和 `GetDailyChallengeRandomSkinPoolId()` 读取入口。
  - 运行时优先读取 `level_config.dailyChallengeRandomSkinPoolId`；远端缺失或解析失败时回退 `GlobalConst.DailyChallengeRandomSkinPoolId`；两者均不可用时返回 `0`。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeManager.cs`
  - 最终阶段创建随机 Skin 奖励时读取上述配置，并把正数池 id 转为字符串传给既有 `SkinUtils` 抽取入口。
  - 配置值非正数、奖励池不存在或没有可发放候选时，不生成 Skin，继续沿用原第二阶段金币 2 倍入口。
- `Assets/Game/Editor/LubanGenerator.cs`
  - 工作区已有改动已把该字段加入 GlobalConst 导出白名单；后续通过 Unity 菜单导出 Guru RemoteConfig 默认值时会包含该字段。本次未直接运行交互式 Unity 导出菜单。

### 配置与奖励数据流

```text
level_config.dailyChallengeRandomSkinPoolId
        -> ConfigManager.RemoteConfig.GetDailyChallengeRandomSkinPoolId()
        -> 缺失时回退 GlobalConst.DailyChallengeRandomSkinPoolId
        -> DailyChallengeManager 最终阶段领奖
        -> SkinUtils.TryGetRandomUnownedRewardSkin(poolId.ToString())
        -> RandomSkinRewardConfig 按权重筛选未拥有且可发放 Skin
        -> CommonGetRewardPopup 继续负责实际授予与原领奖流程
```

- 本次不新增存档字段、全局事件、资源路径、Prefab、UI 动画、广告实现、analytics 或平台分支。
- Android/iOS 使用同一配置和奖励代码路径，没有新增平台条件分支或持续运行开销。

### 验证证据

- 项目 Luban 生成命令执行成功，仅同步覆盖 `GlobalConst.cs` 和 `lubanconfig_tbglobalconst.json`；生成结果包含字段和值 `1`。
- 静态扫描确认 DailyChallenge 不再使用 `stageId.ToString()` 选择随机 Skin 池，改为调用 `GetDailyChallengeRandomSkinPoolId()`。
- `git diff --check` 针对本次生成配置和 C# 修改通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo`：通过，0 个错误、1117 个项目既有警告。

### 剩余风险与未验证项

- 尚未在 Unity PlayMode 实测远端值覆盖、本地 GlobalConst 回退、无效池 id、池内候选全部拥有以及正常随机 Skin 发放。
- 尚未验证 Guru/Firebase 后台实际 `level_config` 已配置新字段；当前即使远端缺失，也会按本地 GlobalConst 值 `1` 回退。
- 尚未在 Android/iOS 横屏设备验证最终阶段弹窗、Skin 图标加载、实际授予和广告 2 倍金币降级分支。

### 回滚路径

- 将 `DailyChallengeManager` 的奖励池参数恢复为 `stageId.ToString()`，即可恢复第二阶段固定读取池 `"2"` 的旧行为。
- 移除 `ConfigManager.RemoteConfig` 的新 key/getter，并从 Global 表删除字段后重新运行 Luban，可完整回滚配置入口；不需要迁移存档或修改 Prefab。

## 2026-07-22：DailyChallenge 初见标记与胜利插屏保护

### 需求与新行为

- `GameData` 新增 `HasShowDailyChallenge` 持久化布尔值；新档和按当前 Newtonsoft 反序列化链缺失该字段的旧档以 `false` 进入初见状态。
- 胜利插屏策略仅在 DailyChallenge 开启、当前关大于等于解锁阈值、当前关首次通关且初见标记为 `false` 时跳过 `InGameVictory` 插屏。
- `DailyChallengePopup.Create(...)` 只有在有效结算和 `OpenUI` 成功后才设置数据、写入 `HasShowDailyChallenge=true` 并保存；创建失败不消费初见状态。
- 标记成功保存后，后续关卡恢复原插屏 CD、间隔和计数策略；失败广告位不受影响。

### 数据流、验证与回滚

```text
InterstitialAdPolicy 读取 HasShowDailyChallenge=false
    -> 初见胜利跳过插屏
    -> DailyChallengeManager 生成有效结算
    -> DailyChallengePopup 创建成功
    -> HasShowDailyChallenge=true
    -> DB.GameData.Save()
```

- 不新增配置、事件、资源、prefab、奖励或分析打点；Android/iOS 共用同一存档和策略代码。
- C# 构建与差异格式检查结果见 `DevelopMD/InterstitialAdPolicy_DailyChallengeUnlock_Record.md`；Unity PlayMode、旧存档反序列化样本和 Android/iOS 真机仍待验证。
- 回滚时移除 Popup 标记写入、Policy 初见分支和 GameData 字段/属性/初始化；已写入新键的旧 JSON 向下兼容仍需用实际存档验证。

## 2026-07-22：正常通关均累计每日挑战进度

### 需求、基线与新行为

- 原 `TryRecordVictory(...)` 通过 `DB.GameData.PassedLevels` 判断本次是否首次通关；已通过关卡再次正常通关时不会增加每日挑战进度。
- 现在移除首次通关判定。只要 SUCCESS 流程调用 `TryRecordVictory(...)`，且每日挑战功能开启、配置和存档有效、关卡满足解锁条件、累计进度未达到最终目标，本次正常通关就增加 1 点进度。
- `currentLevel` 参数继续用于解锁条件判断；不再用于查询 `PassedLevels`。达到最终目标后仍不增长；已有阶段奖励未领取时仍可返回结算快照补发奖励。

### 数据流与影响范围

```text
PlayCtrl_Success
    -> DailyChallengeManager.TryRecordVictory(currentLevel)
    -> 功能、配置、存档及解锁条件校验
    -> 未达到最终目标时 DailyChallengeCurrentProcess + 1
    -> DB.SaveGameSchedule()
    -> DailyChallengePopup 读取前后快照并处理阶段奖励
```

- 仅修改 `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeManager.cs` 的累计条件和对应注释。
- 不修改 Popup 动画、阶段目标、奖励选择、领奖状态、每日重置、配置、存档结构、事件、资源、分析打点或平台分支；Android/iOS 共用同一运行时代码路径。

### 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo`：通过，0 个错误、99 个项目既有警告。
- `git diff --check` 通过；静态扫描确认 `TryRecordVictory(...)` 的累计分支已不再读取 `PassedLevels`，并保留最终目标上限与保存失败回滚。
- Unity PlayMode 中的首次通关、重复通关、最终目标封顶、保存失败回滚和重复通关补领奖仍待手工验证；Android/iOS 横屏设备尚未验证。
- 回滚时恢复 `PassedLevels` 非空且不包含 `currentLevel` 的首次通关判断，并恢复方法注释即可；不需要迁移存档。

## 2026-07-22：按当日通关关卡去重累计

### 需求、基线与新行为

- 上一节移除永久 `GameData.PassedLevels` 判断后，同一天重复完成同一关会重复增加每日挑战进度；本节以当日 Schedule 数据替代永久通关数据完成去重。
- `GameSchedule` 新增 `DailyChallengePassedLevel` 列表，记录每日挑战有效期间当天成功通关的关卡 id。当天首次完成某关时保存关卡 id；未达到最终目标时同时增加进度，当天再次完成同一关不再增长。
- 最终进度封顶后，新的首次日通关关卡仍会写入列表以保持当日记录完整，但不会继续增加进度或触发增长动画。
- 每日 `DailyChallengeManager.OnScheduleRefresh()` 会同时清空累计进度、已领取奖励 id 和 `DailyChallengePassedLevel`。刷新后同一关在新的一天可以再次计入。
- 旧存档缺少该列表时由属性惰性初始化为空列表，不需要主动迁移；若版本更新发生在当天中途，更新前已计入进度的关卡无法还原，更新当天可能对同一关额外计入一次，次日刷新后恢复完整日去重规则。

### 存档与刷新数据流

```text
成功通关
    -> TryRecordVictory(currentLevel)
    -> DailyChallengePassedLevel 不包含 currentLevel
    -> 列表加入 currentLevel
    -> 进度未封顶时 DailyChallengeCurrentProcess 加 1
    -> DB.SaveGameSchedule()
    -> 保存失败时同时回滚列表与进度

DailyScheduleModule 每日刷新
    -> DailyChallengeManager.OnScheduleRefresh()
    -> 进度清零 + 奖励 id 清空 + DailyChallengePassedLevel 清空
    -> DB.SaveGameSchedule()
```

### 影响范围、验证与回滚

- 修改 `GameSchedule.cs`、`GameSchedule_Partial.cs` 和 `DailyChallengeManager.cs`；不修改永久 `GameData.PassedLevels`、配置、Popup 动画、奖励、事件、资源、分析打点或平台分支。
- Android/iOS 共用同一存档和每日刷新链；列表查找只发生在单次 SUCCESS 入口，没有新增逐帧逻辑。进度封顶后每个新的日内首次通关仍会保存一次 GameSchedule，以保证当日关卡记录完整。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo` 最终增量构建通过，0 个错误、0 个警告；`git diff --check` 通过。
- 静态检查确认新增列表在默认初始化、旧存档空值兜底、成功结算保存、保存失败回滚和每日刷新清空五个边界均已接入，且未新增编辑器生命周期、全局事件或逐帧入口。
- Unity PlayMode 的同关日内去重、跨日重新计入、进度封顶后继续记录、存档失败回滚和旧存档加载仍待验证；Android/iOS 横屏设备尚未验证。
- 回滚时移除 `DailyChallengePassedLevel` 字段/属性和刷新清空逻辑，并让 Manager 恢复不按当日关卡去重的增长条件；已有 JSON 中的额外字段是否被旧版本忽略仍需实际旧版本存档加载验证。

## 2026-07-22：长进度条与 StepItem 按 goals 比例同步

### 需求、基线与新行为

- 本地 `dailyChallengeSteps` 当前累计目标为 `[4,10]`。原实现无论两个阶段的实际目标差值如何，都把长进度条固定拆成两个 50% 区间，Popup 与 Btn 中的第一阶段 `DailyChallengeStepItem` 也保持 prefab 中点位置；因此第一目标 4 实际应位于最终目标 10 的 40%，却显示在 50%。
- `DailyChallengeManager` 现在按 `TotalProgress / SecondStageTarget` 生成 `ExpandedNormalizedProgress`；紧凑进度条仍按当前阶段内的 `CurrentStageProgress / CurrentStageTarget` 显示，不改变其口径。
- `DailyChallengePopup` 与 `DailyChallengeBtn` 每次应用有效只读快照时，都会先解析两个 StepItem，再把第一阶段节点同步到 `FirstStageTarget / SecondStageTarget`，把第二阶段节点同步到 100%。位置通过目标进度条的世界坐标转换到 Item 父节点，只修改根节点 X，保留各 prefab 实例的 Y、缩放、旋转、Sprite 和领奖表现。
- `DailyChallengeStepItem.SyncHorizontalPosition(...)` 只承担宿主传入进度条后的横向布局，不读取配置、DB 或 Manager；进度条或父节点无效时保留 prefab 位置。

### 影响范围与数据流

```text
RemoteConfig / GlobalConst dailyChallengeSteps
    -> DailyChallengeManager 构建 DailyChallengeViewState
    -> ExpandedNormalizedProgress = TotalProgress / 最终累计目标
    -> Popup / Btn ApplyState
    -> 第一 StepItem = 第一累计目标 / 最终累计目标
    -> 第二 StepItem = 100%
```

- 修改 `DailyChallengeManager.cs`、`DailyChallengePopup.cs`、`DailyChallengeBtn.cs` 和 `DailyChallengeStepItem.cs`；不修改 prefab、配置表、存档结构、奖励、资源、事件、分析打点或平台分支。
- 同步只发生在 Popup/Btn 的现有状态刷新入口，没有新增 `Update`、全局事件、运行时实例化或额外资源加载；Android/iOS 共用同一 UI 计算代码。

### 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo`：通过，0 个错误、1117 个项目既有警告。
- 目标脚本 `git diff --check` 通过；静态扫描确认累计长条改为 `totalProgress/finalTarget`，Popup 与 Btn 均调用同一个 StepItem 横向同步 API，且未新增逐帧入口或事件。
- Unity PlayMode 尚未验证 `[4,10]` 下第一节点的最终视觉中心、Popup 领奖粒子跟随、Btn Bubble 展开动画后的坐标，以及其他远端 goals 比例；Android/iOS 横屏不同宽高比尚未验证。
- 回滚时恢复 Manager 的固定半段 `ExpandedNormalizedProgress` 算法，并移除两个宿主的 `SyncStepItemPositions(...)` 调用和 `DailyChallengeStepItem.SyncHorizontalPosition(...)`；不需要回滚 prefab、配置或迁移存档。

### 2026-07-22 坐标系偏移修正

- 用户运行截图显示 Btn 第一阶段节点被推到进度条末端、第二阶段节点超出 Bubble；该现象直接证明初版横向同步存在坐标系偏移，不能视为美术微调。
- 根因是 `progressTrack.TransformPoint(...)` 再经 Item 父节点 `InverseTransformPoint(...)` 得到的是父节点 pivot 坐标系中的 `localPosition`，初版却把该 X 写入以 Item anchor 为基准的 `anchoredPosition`。Btn 的 `ProcessBubble` 使用左侧 pivot，而两个 Item 使用中心 anchor，因此额外叠加了约半个 Bubble 宽度；Popup 当前父节点 pivot/anchor 组合未暴露同量偏移，但也共用错误语义。
- `DailyChallengeStepItem.SyncHorizontalPosition(...)` 现改为只写 `localPosition.x`，保留原 local Y/Z、缩放和旋转；不修改 goals 比例、进度填充、prefab、配置、存档、奖励、事件或资源。
- 修正后 `dotnet build Assembly-CSharp.csproj --no-restore --nologo` 通过，0 个错误、99 个项目既有警告；目标差异格式检查通过。修正后的 Btn/Popup 视觉位置和 Android/iOS 横屏仍待 Unity PlayMode/设备复验。
- 如需只回滚本次修正，可把位置写回 `anchoredPosition.x`，但会恢复截图中的整体右移问题；不涉及数据迁移。

## 2026-07-23：所有阶段默认提供 2 倍奖励入口

### 需求、基线与新行为

- 原 `DailyChallengeManager.TryCreateRewardData(...)` 仅在第二阶段没有可发放 Skin 时传入倍率 `2`；第一阶段以及包含 Skin 的第二阶段传入倍率 `1`，因此不会显示通用奖励弹窗的广告追加按钮。
- 现在两个阶段统一使用 `DefaultRewardMultiple = 2`。`CommonGetRewardPopup` 打开时仍先立即发放 1 倍 Coin/Star 普通奖励；玩家选择广告且广告成功后，再按 `multiple - 1` 补发 1 倍 Coin/Star，使数量奖励合计达到 2 倍。
- Skin 奖励继续只在普通领取时发放一次，不参与广告倍率，也不会因本次改动重复授予。

### 影响范围与奖励流

```text
DailyChallengeManager.TryCreateRewardData(stageId)
    -> 构建阶段 Coin / 可选 Skin
    -> DailyChallengeRewardData.Multiple = 2
    -> DailyChallengePopup
    -> CommonGetRewardPopup
        -> 打开时发放 1 倍 Coin/Star + 1 次 Skin
        -> 广告成功后补发 1 倍 Coin/Star
```

- 仅修改 `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeManager.cs` 的 DailyChallenge 奖励倍率选择与对应事实注释；不修改基础金币配置、随机 Skin 池、Prefab、GameSchedule 领奖状态、广告 placement、通用弹窗实现、事件、资源或平台分支。
- 原注释“第二阶段无 Skin 时返回金币 2 倍入口”已因本次需求不再符合代码事实，因此同步改为“所有阶段默认提供数量奖励的广告 2 倍入口”。

### 验证、风险与回滚

- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 个错误、99 个项目既有警告。
- 静态调用链确认 DailyChallenge 的 `rewardData.Multiple` 原样传入 `CommonGetRewardPopup.Create(...)`；通用弹窗只对 Coin/Star 执行广告追加，Skin 不参与。
- 尚未在 Unity PlayMode 验证两个阶段的广告按钮、广告成功/失败/取消、Coin 实际增量、含 Skin 阶段的一次性授予和关闭回调；Android/iOS 真机广告 SDK 流程仍未验证。
- 回滚时移除 `DefaultRewardMultiple`，恢复第二阶段无 Skin 为 `2`、其余情况为 `1` 的条件表达式即可；不涉及配置或存档迁移。

## 2026-07-23：奖励倍率接入 dailyChallengeRewardMultiple

### 需求、基线与新行为

- `Luban/Configs/Datas/Const/Global.xlsx` 新增整数配置 `dailyChallengeRewardMultiple`，当前值为 `2`；项目 Luban 临时生成校验成功，配置类型和输出值与《每日挑战功能需求分析.xlsx》的默认 2 倍要求一致。
- 原实现由 `DailyChallengeManager.DefaultRewardMultiple = 2` 固定倍率；当前 Manager 改为在创建每个阶段的 `DailyChallengeRewardData` 时调用 `ConfigManager.RemoteConfig.GetDailyChallengeRewardMultiple()`。
- 读取优先级为 `level_config.dailyChallengeRewardMultiple` → `GlobalConst.DailyChallengeRewardMultiple` → 代码兜底 `2`。远端或本地配置小于 `1` 时按 `1` 处理；值为 `1` 可关闭广告追加倍率，但普通 1 倍奖励仍由通用奖励弹窗发放。
- Skin 继续只发放一次，不参与倍率；阶段达成、领奖存档、Coin 基础数量和广告成功后的追加算法均未修改。

### 影响范围与配置流

```text
Global.xlsx dailyChallengeRewardMultiple
    -> Luban 生成 GlobalConst.cs + lubanconfig_tbglobalconst.json
    -> ConfigManager.RemoteConfig.GetDailyChallengeRewardMultiple()
       -> level_config 同名远端字段
       -> GlobalConst 本地值
       -> 缺失时回退 2
    -> DailyChallengeManager.TryCreateRewardData()
    -> DailyChallengeRewardData（最小值 1）
    -> CommonGetRewardPopup
```

- 修改 `ConfigManager.RemoteConfig.cs`、`DailyChallengeManager.cs` 和 `LubanGenerator.cs`，由正式 Luban 生成更新 `Assets/Configs/Code/LubanConfig/GlobalConst.cs` 与 `Assets/Game/Configs/lubanconfig_tbglobalconst.json`，并把默认值 `2` 同步到 `Assets/Guru/guru_spec.yaml` 的既有 `level_config`。
- 本次不修改 `Global.xlsx` 中用户新增的源配置，不新增 RemoteConfig 顶层 key、存档字段、prefab、资源加载、事件、分析或平台分支；`LubanGenerator.GlobalConstLevelConfigKeys` 负责后续重复导出时保留该字段。

### 验证、风险与回滚

- 项目 Luban 先在临时目录完成 schema、数据加载与 validation，再正式生成；正式生成只覆盖 `GlobalConst.cs` 和 `lubanconfig_tbglobalconst.json` 两个对应产物。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 个错误、99 个项目既有警告。
- 尚未在 Unity PlayMode 验证远端缺失、本地值 1/2/大于 2/小于 1、配置热更新后的领奖弹窗按钮和实际 Coin 增量；Android/iOS 真机广告流程仍待验证。
- 回滚时恢复 Manager 的固定倍率常量，删除 RemoteConfig 读取入口和 LubanGenerator 白名单项，并将源表配置、两个 Luban 产物及 Guru `level_config` 同名字段一并回退；不涉及存档迁移。

## 2026-07-23：被其他 Popup 打断后的自动关闭修复

### 问题基线与修复行为

- Bear `UIManager.OpenUI(...)` 在同一 `UILayer.Popup` 打开新界面时，会对原栈顶 DailyChallengePopup 调用 `OnHide()`；基类完成关闭动画后会禁用其 GameObject。
- 原结算异步流程在 Delay/Tween/奖励等待后使用 `isActiveAndEnabled` 校验。弹窗被意外覆盖时校验返回 false，流程直接结束；之后其他 Popup 关闭并重新 `OnShow()` DailyChallengePopup 时，没有恢复或关闭入口，导致结算弹窗长期停留。
- 当前 `OnHide()` 会区分两类隐藏：
  - DailyChallengePopup 主动打开 `CommonGetRewardPopup`：标记为正常奖励等待，不触发中断关闭，原领奖与后续阶段流程继续。
  - 其他 Popup 意外覆盖：记录 `_closeAfterInterruption`；DailyChallengePopup 再次回到 Popup 栈顶时不重播结算，而是直接 `CompleteAndClose()`，完成等待并让 SUCCESS 流程继续。
- 不在隐藏期间直接销毁 DailyChallengePopup，避免它仍非栈顶时提前完成等待并抢先打开 VictoryPanel，打断当前外部 Popup。

### 影响范围与生命周期流

```text
其他 Popup 打开
    -> UIManager 调用 DailyChallengePopup.OnHide()
    -> 非 CommonGetRewardPopup 正常等待：记录中断关闭
    -> 其他 Popup 关闭
    -> UIManager 调用 DailyChallengePopup.OnShow()
    -> CompleteAndClose()
    -> ShowAndWaitAsync 完成
    -> SUCCESS 流程继续

CommonGetRewardPopup 打开
    -> 标记正常奖励等待
    -> DailyChallengePopup.OnHide() 不记录中断
    -> 奖励 Popup 关闭后恢复 DailyChallengePopup
    -> 继续下一阶段或正常自动关闭
```

- 仅修改 `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs` 的本地生命周期状态；不修改 Bear UIManager、CommonGetRewardPopup、奖励发放、领奖存档、配置、prefab、事件、资源、分析或平台分支。

### 验证、风险与回滚

- 静态调用链确认同层 Popup 的隐藏/恢复顺序为 `OnHide()` → 新 Popup → 原 Popup `OnShow()`；正常奖励 Popup 在创建前已经设置等待标记。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 个错误、99 个项目既有警告；目标差异格式检查通过。
- 尚未在 Unity PlayMode 验证进度等待、数字 Tween、阶段动画、自动关闭 Delay 各时点被外部 Popup 打断，以及两阶段 CommonGetRewardPopup 正常串联；Android/iOS 横屏仍待验证。
- 中断后未展示的待领奖阶段不会在本次继续播放；普通奖励和领奖 id 已在 DailyChallengePopup 打开时完成，不依赖 CommonGetRewardPopup 是否成功展示。
- 回滚时删除 `OnShow()` / `OnHide()` 中断处理、两个运行时标记及奖励 Popup 等待标记边界，即恢复原行为；不涉及存档或配置迁移。

## 2026-07-23：打开结算弹窗时前置发放普通奖励

### 需求、基线与新行为

- 原流程在每个阶段 1.5 秒领奖动画完成后调用 `TryCreateRewardData(...)`，随后由 `CommonGetRewardPopup.Create(...)` 同步发放 normal 奖励，再由 Popup 调用 Manager 写入阶段领取状态。
- 新流程在 `DailyChallengePopup.SetData(...)` 收到结算快照时立即遍历待领取阶段，调用 `DailyChallengeManager.TryClaimRewardOnPopupOpen(...)` 同步完成奖励选择、normal 入包和 `DailyChallengeHasRewardId` 保存。
- Popup 按阶段 id 缓存同一次选择产生的 `DailyChallengeRewardData`。阶段动画完成后使用缓存数据调用 `CommonGetRewardPopup.Create(..., grantNormalReward: false)`，因此只展示奖励并继续处理广告额外数量奖励，不会再次发放 normal Coin、Star 或 Skin。
- 多个待领取阶段按结算快照顺序在 Popup 打开时领取；任一阶段创建或普通发放失败时停止后续阶段，避免越过失败阶段继续发奖。已经成功领取但尚未播放展示动画的阶段不会因 Popup 中断而重复发放。

### SimpleBag 复用边界

- `SimpleBag` 新增 `GrantRewards(IEnumerable<KeyValuePair<GameProps, int>>, RewardType, int)`，统一处理已经解析好的数量奖励、倍率和既有 `AddTool` 保存/事件/打点。
- `CommonGetRewardPopup` 的 normal 与广告数量奖励、`DailyChallengeManager` 的前置 normal 数量奖励均复用该 API。
- 配置参数解析仍由上层业务负责，`SimpleBag` 不解析 `object` 或 Luban `ParamBase`；Skin 所有权仍由 `GetNewSkin(...)` 负责。

### 数据、存档与 UI 流

```text
DailyChallengePopup.SetData
    -> ClaimPendingRewardsOnOpen
    -> DailyChallengeManager.TryClaimRewardOnPopupOpen
        -> TryCreateRewardData
        -> SimpleBag.GetNewSkin（可选）
        -> SimpleBag.GrantRewards（Coin/Star normal）
        -> TryMarkRewardClaimed + DB.SaveGameSchedule
    -> 进度与阶段领奖动画
    -> CommonGetRewardPopup(grantNormalReward: false)
        -> 只展示 normal 奖励
        -> 广告成功时 SimpleBag.GrantRewards（额外数量）
```

- 本次不修改奖励配置、随机 Skin 池、广告 placement、prefab、存档结构、资源地址、全局事件、analytics、IAP 或平台 SDK。
- `GameData` 奖励保存与 `GameSchedule` 领取标记仍是两个既有保存边界；没有新增跨存档事务。

### 验证、剩余风险与回滚

- CodeGraph 复核确认 normal 时序为 `SetData -> ClaimPendingRewardsOnOpen -> TryClaimRewardOnPopupOpen -> TryGrantNormalReward`，阶段动画后只读取缓存数据。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 个错误、99 个项目既有警告。
- 尚未在 Unity PlayMode 验证单阶段、双阶段、随机 Skin、Popup 创建失败、中断恢复、广告成功/失败和存档失败路径；Android/iOS 横屏真机尚未验证。
- 如果普通奖励已经写入 `GameData` 但 `GameSchedule` 保存失败，当前会话内领取 id 会保留，重启后仍存在重复发奖风险；需要跨存档事务才能彻底消除，当前标记为未验证风险。
- 回滚时移除 `ClaimPendingRewardsOnOpen(...)` 和缓存字典，恢复动画后 `TryCreateRewardData(...)`、CommonGetRewardPopup 默认发奖及展示成功后 `TryMarkRewardClaimed(...)`；`SimpleBag.GrantRewards(...)` 可独立保留或把两个调用点恢复为原内联循环。

## 2026-07-23：Coin PropBlock 分阶段手动刷新

### 需求、基线与影响范围

- 需求：`DailyChallengePopup` 顶部 Coin `PropBlock` 需要在奖励展示结束时更新，但不能因为全部待领取阶段已在 `SetData(...)` 前置入包而在进度、领奖和奖励弹窗动画之前直接显示最终余额。
- 当前 prefab 已新增 Coin `PropBlock` 并把 `IsAutoUpdate` 设为 false；修改前 Popup 没有缓存或手动刷新它，前置发奖后会一直显示打开时的旧余额。
- 影响文件：`DailyChallengePopup.cs` 和本功能记录。未修改共享 `PropBlock`、DailyChallengeManager、奖励配置、发奖/存档、广告、prefab 层级、资源、事件、analytics 或平台分支。

### 修改后 UI 流

1. `OnOpen()` 缓存当前 Popup 子级的 Coin `PropBlock` 并用真实 Bag 数量建立打开时显示基线；奖励入包后依靠 `IsAutoUpdate=false` 保持该数字。
2. 每个阶段打开 `CommonGetRewardPopup` 前，计算其后尚未展示、但已经前置入包的阶段基础 Coin 总数。
3. 当前阶段奖励弹窗关闭、DailyChallengePopup 恢复显示时，读取真实 Coin 并减去尚未展示阶段的基础 Coin，再用 `PropBlock.BindData(...)` 更新父 Popup。
4. 因此第一阶段返回时只显示“打开前余额 + 第一阶段基础 Coin + 第一阶段广告额外 Coin”；第二阶段返回时剩余基础 Coin 为 0，显示真实最终余额。
5. `OnShow()` 和异步等待结束共用一次性刷新标记，兼容 Bear UI 恢复顺序；奖励弹窗创建失败时原流程会关闭 DailyChallengePopup，不刷新一个即将销毁的父界面。

### 验证、剩余风险与回滚

- CodeGraph 已复核 `RunSettlementFlowAsync()` 按 pending stage 顺序计算剩余基础 Coin，`OpenRewardPopupAsync(...)` 只在奖励弹窗返回边界触发刷新；C# 构建通过：0 errors / 99 warnings。
- 静态 YAML 确认 DailyChallengePopup prefab 的新增 PropBlock 覆写 `IsAutoUpdate: 0`；目标 `git diff --check` 通过。
- **P1 配置风险：** 若 prefab 重新打开 `IsAutoUpdate`，全部前置奖励会立即显示，分阶段控制失效。
- **P2 兜底风险：** 当前 prefab 静态证据只有目标 Coin PropBlock；若以后增加其他 PropBlock，应显式绑定序列化字段，避免 `GetComponentInChildren(...)` 兜底选错。
- 尚未在 Unity PlayMode 验证单阶段、双阶段、两阶段分别看广告、无 Coin/Skin、Popup 创建失败、中断恢复和缓存重开；Android/iOS 横屏真机未验证。
- 回滚时删除 Coin PropBlock 字段、剩余 Coin 计算和奖励弹窗返回刷新逻辑，并恢复 prefab 自动刷新或其他统一刷新方式；不涉及存档迁移。

## 2026-07-23：胜利进度改用持久解锁标记门禁

- `PlayCtrl_Success.OnEnter()` 现在先调用 `DailyChallengeManager.TryUnlock(currentLevel)`；本次成功通关关卡达到 `dailyChallengeUnlockLevel` 时写入并保存 `GameData.HasUnlockDailyChallenge`，随后同一 SUCCESS 流程的 `TryRecordVictory(...)` 才读取运行时上下文，因此解锁关本身可以计入当日进度。
- 原运行时上下文对本次关卡、`CurrentLevel` 和 `PassedLevels` 的动态扫描已移除；Btn 展示与 Popup 进度统一要求持久解锁标记为 `true`，其余功能开关、阶段配置和 `GameSchedule` 条件不变。
- 保存解锁标记失败时会回滚为 `false`，不记录本次每日挑战进度；旧存档按未解锁处理，需后续成功通关达到阈值的关卡。奖励、领取、动画、Popup 顺序、prefab、资源、事件和平台分支未修改。
- C# 构建通过（99 个项目既有警告、0 个错误），静态调用与差异格式检查通过；Unity PlayMode 的阈值边界、保存失败、重启持久化、旧存档和 Android/iOS 横屏仍待验证。
- 完整字段、数据流、风险和回滚路径见 `DevelopMD/DailyChallengeBtn_FeatureRecord.md` 的“2026-07-23：使用持久解锁标记统一入口与进度门禁”章节。

## 2026-07-23：结算进度改为双阶段分段计算

- Popup 不再按 `TotalProgress / goal2` 直接填充整条进度。Goal1 占前 50% 并按 `goal1` 等分；Goal2 占后 50% 并按 `goal2 - goal1` 等分。
- `PlayProgressAnimationAsync(...)` 和静态状态刷新仍使用同一个 `DailyChallengeViewState.ExpandedNormalizedProgress`，因此动画目标与最终显示一致；第一阶段奖励节点同步使用快照提供的 50% 阶段边界。
- Btn 收起条、Btn Bubble 和 Popup 同步消费 Manager 的同一分段结果。累计 Process、目标数字、阶段达成、奖励领取、Popup 时序、prefab 和存档均未修改。
- C# 构建通过（99 个项目既有警告、0 个错误），目标差异格式和调用点静态检查通过；Goal1/Goal2 边界动画、不同 goals 组合及 Android/iOS 横屏仍待 Unity PlayMode/设备验证。
- 完整公式、影响文件与回滚方式见 `DevelopMD/DailyChallengeBtn_FeatureRecord.md` 的“2026-07-23：Popup 与 Btn 统一使用双阶段分段进度”章节。

## 2026-07-23：Common 奖励弹窗隐藏尚未展示 Coin

- 原流程虽然在 DailyChallenge 父 Popup 中用“真实 Coin - 后续阶段基础 Coin”控制分阶段显示，但 `CommonGetRewardPopup` 自己激活时会读取全部前置发放后的真实余额，第一阶段展开时仍可能提前显示第二阶段金额。
- `OpenRewardPopupAsync(...)` 现在把既有 `remainingBaseCoin` 同时传给 Common 的 `remainingHiddenCoinAfterReward`：
  - 展开第一阶段时，Common 显示“真实余额 - 当前阶段 Coin - 后续阶段 Coin”。
  - 当前阶段飞币结束后，Common 只滚动到“真实余额 - 后续阶段 Coin”。
  - 最后阶段的剩余值为 `0`，飞币后显示真实最终余额。
- 第一阶段广告成功后新增的 Coin 已进入真实余额，且不会被“后续基础 Coin”扣除，因此会随当前阶段动画显示；后续阶段仍保持隐藏。
- 本次不改变前置发奖、领奖标记、奖励数量、广告倍率、Skin、父 Popup 的刷新公式、prefab、存档或事件。
- C# 工程构建通过：0 个错误、99 个项目既有警告；目标差异格式检查通过。Unity PlayMode 的双阶段、两阶段分别看广告、无限 Coin、快速关闭和 Android/iOS 横屏仍待验证。
- 回滚时移除 Common `Create(...)` 的 `remainingHiddenCoinAfterReward` 参数传递，即恢复 Common 直接读取真实余额的旧表现；不涉及数据迁移。

## 2026-08-04：Popup 进度改为两张阶段独立填充图

### 需求、原有基线与修改后行为

- 当前 `DailyChallengePopup.prefab` 已把原单张 `progressFill` 替换为固定布局的 `process1`、`process2`；StepItem 的位置由 prefab 美术布局直接确定。
- 原 Popup 仍向一张图片补间累计双阶段进度，并运行时把 StepItem 定位到 50%/100%，与新 prefab 不匹配。
- Popup 现在分别计算两张图的目标 `fillAmount`：第一阶段图按第一阶段内进度增长并在达成后保持 1；第二阶段图在第一阶段期间保持 0，进入第二阶段后按第二阶段内进度增长。
- 结算动画同时补间有效的阶段进度图；游标改为使用当前阶段进度图的实际 RectTransform 宽度计算位置，缺少阶段图时才回退到原 `processBar + ExpandedNormalizedProgress` 计算。
- StepItem 不再被 Popup 运行时改写位置，目标数字、领奖状态、粒子定位和主图标动画保持原流程。

### 影响文件、资源与流程

- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs`
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeManager.cs`
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeStepItem.cs`
- `Assets/Game/UI/DailyChallenge/Resources/DailyChallengePopup.prefab`

```text
BeforeState / AfterState
    -> 第一阶段 fillAmount
    -> 第二阶段 fillAmount
    -> 当前阶段图片实际宽度上的 cursor X
    -> 既有阶段领奖动画与 CommonGetRewardPopup 流程
```

- 不改变累计进度、奖励领取、Coin/Skin 发放、存档、配置、弹窗顺序、资源加载、全局事件、analytics 或平台分支。

### 验证、剩余风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore` 通过：0 个错误、0 个警告。
- 目标 `git diff --check` 通过；静态扫描确认旧 `progressFill`、StepItem 定位调用和 `FirstStageEndNormalizedProgress` 均无残留。
- Prefab YAML 静态检查确认两张阶段进度图引用均已声明并正确绑定，94 个本地对象无缺失本地引用候选。
- Unity PlayMode 尚未验证跨阶段时游标越过两段图片间隔的视觉节奏、Tween 中断恢复、弹窗复用与奖励动画叠加；Android/iOS 横屏仍为 `uncertain`。
- 回滚时恢复单张 `progressFill`、累计进度补间、原游标计算与 StepItem 定位 API；存档和配置无需迁移。

## 2026-08-07：Cursor 阶段终点对齐 StepItem

### 需求、原有基线与修改后行为

- 需求：DailyChallengePopup 的 cursor 在阶段性到达目标后，应与对应 `DailyChallengeStepItem` 的 X 位置对齐。
- 原实现始终根据当前阶段进度图的 `RectTransform` 宽度计算 cursor X；StepItem 由 prefab 固定美术布局，
  因此阶段进度为满时，两者的终点位置不一定重合。
- 修改后第一阶段刚好达成时，cursor 终点取第一阶段 StepItem 的世界坐标并换算到 cursor 父节点坐标空间；
  第二阶段达成时同理对齐第二阶段 StepItem。尚未达成目标的阶段内移动继续使用原进度图计算。
- StepItem 缺失时保持原进度图算法兜底，不阻断进度动画或结算流程。

### 影响范围与系统边界

- 修改 `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs` 的 cursor X 解析；本次相关提交同时收录
  `DailyChallengePopup.prefab` 中已有的 cursor 初始 X 从 `14` 调整为 `-23`，算法终点仍以 StepItem 实际位置为准，
  不依赖该固定初始值。
- 不修改 StepItem prefab、进度图片、目标配置、存档、奖励、事件、analytics 或平台分支。

### 验证、剩余风险与回滚

- 已静态核对第一/第二 StepItem 的运行时识别、阶段状态边界和跨坐标空间换算；`git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode、不同 Canvas 缩放、弹窗复用和 Android/iOS 横屏下目测两个阶段终点对齐效果。
- 回滚时删除 `TryGetReachedStageItemAnchoredPositionX(...)` 及其优先调用，并按需将 prefab 的 cursor 初始 X 恢复为 `14`；无需迁移数据。
