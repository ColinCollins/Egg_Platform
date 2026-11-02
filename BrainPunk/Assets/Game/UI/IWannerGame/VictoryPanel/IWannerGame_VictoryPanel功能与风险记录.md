# IWannerGame VictoryPanel 功能与风险记录

## 2026-10-09：1.16 OneLink 结算 Replay 门禁

- 本轮完整同步 OneLink：基类 OnOpen 读取已成功加载的 IWanner OneLink 来源快照；同一 OneLink Group 组内推进继续属于该入口。刷新时隐藏 replayButton 且不可交互，OnReplay 在派发原事件前拒绝。普通进关的 Replay 和 Reward 派生页其它广告／领奖／next／分享／NoAds 逻辑不改。
- 必要依赖为 OneLinkManager、GameData 成功历史及 IWannerPlayCtrl 接受／加载成功／失败／退出通知；失败不记成功历史，普通进关清来源，退出清本轮标记。资源、事件类型、Prefab 和原统计时序不改。
- Editor／Android 真实引用 C# 编译通过；当前 Manager/Router 的状态与配置测试通过，但未在 Unity／横屏真机验证普通／Reward 页面、广告原生覆盖后刷新和关闭重开，结论 uncertain。
- 当前实现、各层验证、剩余风险及回滚详见 ../../../Scripts/SDK/Services/Core/OneLink功能与测试记录.md 的 1.16 当前有效实现章节。回滚只撤销同步引入的来源快照／显隐／点击门禁和对应 OneLink 接点，不改原普通 Replay 或玩家存档。

日期：2026-08-11

## 需求与背景

- IWanner 当前已经能从 Group 第 0 小关推进到末关，但末关完成后原逻辑只记录 warning，没有正式结算界面。
- 本次依据 `Assets/Game/Scripts/Level/IWannerLevel/IWannerGame玩法制作进度Checklist.md` 的 VictoryPanel 条目接入：Group 首次通关打开 Reward 版表现，重复通关打开普通版；Replay 从当前 Group 第 0 小关开始；下一步复用 SettingPopup Menu 退出语义，返回 StartPanel 后弹出 IWanner 选组界面。
- 2026-08-11 用户确认替换初版“只展示、不发奖励”约定：Reward 版广告领奖逻辑必须与
  `DrawGame_GameVictoryPanel_Reward` 保持一致。

## 影响文件、资源与系统

- 新增脚本：
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Scripts/IWannerGame_GameVictoryPanel.cs`
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Scripts/IWannerGame_GameVictoryPanel_Reward.cs`
- 用户已创建、此次补齐根组件绑定的资源：
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Resources/IWannerGame_GameVictoryPanel.prefab`
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Resources/IWannerGame_GameVictoryPanel_Reward.prefab`
- 结算编排：`Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`
- IWanner 独立存档入口与集合兼容：
  - `Assets/Game/DB/DB.IWannerGame.cs`
  - `Assets/Game/DB/IWannerGame.cs`
- 复用系统：Bear UI、Bear EventSystem、Bear Logger、DOTween、现有 `UISpineCtrl`、`HorizontalRandomAdRewardCtrl`、
  `RewardAdHelper`、`SimpleBag`、`CommonUIRewardAnimCtrl`、`GameReplayEvent`、`EnterNextLevelEvent` 和 IWanner 临时选组弹窗。
- 未修改 DrawGame 胜利脚本、主线胜利脚本、共享 `GamePlayPanel`、共享 `ActorCtrl`、关卡 prefab、Luban 配置、
  RemoteConfig、广告策略或平台 SDK 实现；IWanner 只调用已有奖励广告和 Bag 接口。

## 修改前行为基线

- `IWannerPlayCtrl.AdvanceToNextLevelAsync()` 在最后一小关完成后进入 `HandlePendingGroupCompletion()`。
- 占位实现会清空失败次数、重新开放 HUD 输入并记录“VictoryPanel flow is not implemented yet”，不会写入 Group 通关状态。
- 两个 IWanner Victory prefab 是从 DrawGame 资源复制得到，根组件仍指向 `DrawGame_GameVictoryPanel` / `DrawGame_GameVictoryPanel_Reward`；直接以 IWanner 类型打开会因根节点缺少目标组件而失败。
- IWanner 存档类型和 `DBSetting` 条目已经存在，但没有 `DB.IWannerGame` / `DB.SaveIWannerGame()` 静态运行时入口。

## 修改后行为

### Group 结算编排

1. 末关门动画结束后，`IWannerPlayCtrl` 清空当前小关失败次数、关闭失败重置区、锁定 HUD 与 Actor 输入，并暂停当前 LevelCtrl。
2. 控制器以 `IWannerLevelSort.Id` 作为 Group 身份读取 `DB.IWannerGame.PassedLevels`：列表中不存在该 Id 时视为首通，否则视为复通。
3. 首通创建 `IWannerGame_GameVictoryPanel_Reward`，复通创建 `IWannerGame_GameVictoryPanel`。
4. 面板创建请求后把 Group Id 写入 `PassedLevels`，并通过 `DB.SaveIWannerGame()` 保存。旧档集合为空时只补空集合，不改已有值。
5. 面板创建失败时复用 SettingPopup Menu 退出语义：切回主线、销毁当前关卡/HUD并恢复 StartPanel，随后尝试打开 IWanner 选组弹窗；弹窗创建失败时保留 StartPanel 作为安全落点。

### 面板交互

- 普通版沿用 DrawGame 的 `2_appear01 -> 2_idle` Spine 节奏；首次通关版沿用 `1_appear01 -> 1_idle`。
- 入场完成后开放 next/replay，单次导航门禁阻止重复点击。
- Replay 销毁面板并派发 `GameReplayEvent`；活动 `IWannerPlayCtrl` 只在 Group 已结算状态接受，保持 Group 不变并从索引 0 重建。
- next 销毁面板并派发 `EnterNextLevelEvent`；活动控制器调用与 SettingPopup Menu 相同的 `ExitToLevelSelection()`，通过 `GameManager.ReturnToStartPanel()` 切回主线并恢复 StartPanel，再创建 `IWannerGame_ChoiceLevelPopup`。
- Group 结算时系统返回键和暂停恢复入口被本地 `_isGroupResolved` 状态拦截，避免设置弹窗覆盖 Victory。

### Reward 广告与金币流

1. Reward 入场完成后启动 `HorizontalRandomAdRewardCtrl`；广告按钮点击时先设置请求门禁、锁定 next/replay，
   并立即停止转盘，不等待任何依赖 Unity PlayerLoop 的 Tween 后再打开原生广告。
2. 广告使用 `RewardPlacement.GameVictory`。失败或取消时不写 Bag，重新启动转盘并恢复按钮；
   `RewardAdHelper` 继续负责成功/失败提示和 IWanner 激励后插屏冷却更新。
3. 成功回调先设置单面板完成门禁并置灰广告按钮，再读取转盘最终值，通过
   `GameManager.Instance.Bag.AddTool(GameProps.Coin, coins, RewardType.Ad)` 发放金币；该既有接口负责
   `DB.GameData` 保存、道具更新事件和虚拟货币奖励打点。
4. 金币写入后从转盘指针位置播放 `CommonUIRewardAnimCtrl` 收集动画，播放结束刷新 `PropBlock`；动画或
   `PropBlock` 缺失只降级表现，不回滚已经到账的金币。
5. Victory 创建前当前 IWanner Level 已暂停，Actor/HUD 输入和 GameplayPanel Block 保持锁定；广告请求和
   领奖动画期间 Victory 导航也保持不可交互，回调结束后由当前面板恢复按钮状态。

### IWanner 与 Draw 边界

> 本节记录 2026-08-11 初版边界；其中“隐藏分享”的约定已被下方 2026-08-19 分享接入记录替代，
> “隐藏 NoAds”的约定已被下方 2026-08-21 免广告购买逻辑补充记录替代。

- IWanner 当前没有胜利星级、分享数据和 Victory 内免广告购买需求；复制 prefab 中的 `DrawGame_VictoryStarGroup`、`GameVictoryPanel_Share` 和 `NoAdBtn` 在打开时隐藏。
- Reward 版的广告、转盘停止/恢复、单面板幂等、金币发放、收集动画和 PropBlock 刷新与 DrawGame Reward
  实现一致；IWanner 仍不启用 Draw 星级、分享或 Victory 内免广告购买。
- VictoryPanel 仍以 Group Id 记录 `PassedLevels`；currentGroup/currentLevel 续关已由后续断点功能补齐，正式解锁、
  分页状态和完整关卡 analytics 仍不属于本次奖励修改。

## 验证证据

- 定向构建：临时把三个新增运行时代码文件加入 Unity 生成的 `Assembly-CSharp.csproj` 后执行 `dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet -consoleloggerparameters:ErrorsOnly`，结果为 `0 error / 25 warning`；warning 为工程既有依赖范围。
- 真实奖励修订后再次执行同一 Runtime 构建命令，结果为 `0 error / 1119 warning`。
- Victory next/Menu 导航统一后再次执行 `dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet -consoleloggerparameters:ErrorsOnly`，结果为 `0 error / 1119 warning`。
- 静态确认两个 IWanner Victory prefab 的根组件 GUID 已分别指向 IWanner 普通版与 Reward 版脚本，原有必需按钮、Spine、CanvasGroup 和 Reward 表现引用保持非空。
- 静态确认结算流程没有新增全局事件类型，只复用现有 `GameReplayEvent` 与 `EnterNextLevelEvent`；Victory next 与 SettingPopup Menu 共用 StartPanel/选组退出入口；Reward 版调用
  与 Draw 相同的 `RewardAdHelper`、`GameVictory` 广告位、Bag Coin/`RewardType.Ad` 和通用收集动画，不读取 Draw DB。
- Unity PlayMode、YooAsset/Resources 实际加载与 Android/iOS 横屏真机尚未执行，结论为 `uncertain`。

## 风险与未验证缺口

1. **P1：首通/复通与按钮链只有编译和静态证据。** 需要在 PlayMode 清空 IWanner 存档后完成同一 Group 两次，确认第一次为 Reward 版、第二次为普通版，并覆盖 next/replay 快速连点。
2. **P1：存档失败会保留当前内存中的 PassedLevels。** 当前与 Draw 进度的失败处理基线一致：记录 warning，不回滚已完成的内存状态；应用重启后仍可能再次显示首通版。
3. **P1：首通奖励机会依赖当前面板生命周期。** Group 通关记录在 Reward 面板创建时已保存；如果玩家在广告成功
   回调前强制退出或杀进程，本次不会发金币，重启后同一 Group 会按复通打开普通版。该行为与当前 Draw 首通基线一致，
   但仍需产品在真机确认是否接受。
4. **P1：正式 IWanner 选组状态仍未闭环。** `CurrentGroupID`、`CurrentLevel` 断点已由后续功能接入，但
   `UnlockGroups`、`MaxGroupId` 和选组锁定/通关展示仍未完成。
5. **P1：正式 analytics 和时长清理仍未接入。** 当前控制器没有 IWanner attempt 时长字段；插屏已按 Group 胜利口径接入，但不得把 Checklist 中的正式时长和打点条目视为完成。
6. **P1：真实广告回调与弱网只复用了 Draw 静态基线。** 需要覆盖广告成功、失败、取消、加载超时、退后台、
   面板异常关闭和 Bag 不可用；当前未获得 Android/iOS SDK 实测证据。
7. **P2：移动端与资源加载未验证。** 需要验证两个 Resources 名称、Prefab 根组件导入、Spine 动画名、
   不同分辨率横屏按钮区域和触控射线。

## 2026-08-11：Group 胜利插屏补充

- Group 末关完成后先记录通关状态和一次 Group 胜利计数，再等待 `InGameVictory` 插屏结果；只有广告回调完成并通过活动版本校验后才创建首通或复通 VictoryPanel。
- 原生覆盖期间当前 LevelCtrl 保持暂停，Actor/HUD 输入和 ClickBlock 保持锁定，因此 VictoryPanel Spine、转盘与按钮动画不会在广告背后提前推进。
- Skip 激励成功会更新 IWanner 独立的激励后插屏 CD，可能按配置阻止紧随其后的 Group 胜利插屏。
- 完整失败、Reset、免广告弹窗、验证与回滚记录见 `Assets/Game/Scripts/Play/IWanner/IWannerGame插屏广告功能与风险记录.md`。

## 最短验收清单

1. 清空 `SaveData_IWannerGame` 后进入任一有效 Group，连续通过全部小关，确认出现 Reward 版；广告成功后只增加
   转盘最终值对应的金币一次，并播放收集动画、刷新 PropBlock。
2. 分别模拟广告失败、取消和加载超时，确认不增加金币、转盘恢复、next/replay 可再次操作；广告成功后重复点击
   不得二次发奖。
3. 点击 Replay，确认从同一 Group 第 0 小关开始，旧关卡/HUD/Victory 不残留。
4. 再次完成同一 Group，确认出现普通版。
5. 点击 next，确认先恢复 StartPanel，再在其上打开 IWanner 选组弹窗；选择任一 Group 后按已有断点规则重新进入 IWanner。
6. 在入场动画期间快速点击 next/replay，并在 Victory 显示时按系统返回键，确认没有重复导航或设置弹窗插队。
7. 重启应用后再次通关同一 Group，确认仍判定为复通；同时检查控制台没有缺脚本、缺引用或 Resources 加载错误。

## 回滚路径

- 恢复两个 IWanner Victory prefab 根组件为原复制来源仅用于资源回退，不能保留当前 IWanner 工厂调用；完整回滚应同时移除两个 IWanner Victory 脚本。
- 将 `IWannerPlayCtrl.HandlePendingGroupCompletion()` 恢复为原占位逻辑，并移除 `EnterNextLevelEvent` 订阅、Group 结算状态与通关记录方法，即可恢复到末关只记录 warning 的基线。
- 移除 `DB.IWannerGame.cs` 及 `IWannerGame.EnsureValidCollections()` 后，本次新增的静态存档入口消失；已有 PlayerPrefs 中的 IWanner 数据不会自动删除。
- 若只回滚真实广告奖励，恢复 `IWannerGame_GameVictoryPanel_Reward` 为转盘预览实现并把广告按钮设为不可交互；
  已经通过 Bag 发放的 Coin 不自动追回，普通 Victory、Group 通关和选组流程无需回滚。
- 若只回滚本次 next/Menu 导航统一，恢复 `OnEnterNextLevelEvent()` 直接打开选组弹窗，并恢复
  `ExitToLevelSelection()` 保持 IWanner 激活的旧实现；不涉及存档迁移或奖励回退。

## 2026-08-17：New SkeletonGraphic 入场与待机控制

### 需求与修改前基线

- 普通版和 Reward 版 prefab 已把 `New SkeletonGraphic` 使用的 SkeletonData 更换为
  `Assets/Game/ArtSrc/Spines/ui_victory_003/box_SkeletonData.asset`；对应 `box.json` 的动画清单只有
  `in`、`idle`。
- 修改前根面板的 `spineCtrl` 仍绑定该 `New SkeletonGraphic`，基类却按旧胜利资源请求
  `2_appear01 -> 2_idle`，Reward 版请求 `1_appear01 -> 1_idle`。新资源不包含这些动画，因此静态证据表明
  新角色 Spine 无法按目标顺序播放。
- 两个 prefab 的 `Bg` 节点保留独立 `UISpineCtrl`，其新 `sp.json` 动画清单为
  `1_appear`、`1_idle`、`2_appear`、`2_idle`；`light` 节点继续使用 `glow` 循环，不修改其资源或播放方式。

### 影响文件、资源与修改后行为

- 修改脚本：
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Scripts/IWannerGame_GameVictoryPanel.cs`
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Scripts/IWannerGame_GameVictoryPanel_Reward.cs`
- 补充绑定：
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Resources/IWannerGame_GameVictoryPanel.prefab`
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Resources/IWannerGame_GameVictoryPanel_Reward.prefab`
- 原 `spineCtrl` 改为绑定各自 `Bg` 的 `UISpineCtrl`，普通版使用 `2_appear -> 2_idle`，Reward 版使用
  `1_appear -> 1_idle`；该链仍负责原入场完成回调和导航开放时机。
- 新增序列化字段 `newSkeletonGraphicSpineCtrl`，显式绑定各自角色 `UISpineCtrl`。面板每次打开时先非循环播放
  `in`，再通过同一 0 号轨道的 Spine 动画队列自动切换并循环播放 `idle`。
- 两条 Spine 链并行且相互独立；新角色引用缺失或无法播放时只降级该角色表现，不改变 next/replay 门禁。
- 未新增实例化、逐帧轮询、全局事件、存档、配置、资源加载、analytics、广告、内购或平台 SDK 流程；Reward
  金币和原生广告覆盖层边界保持不变。

### 验证证据与剩余风险

- UTF-8 解析 `glow.json`、`sp.json`、`box.json`，静态确认动画清单分别为 `glow`、
  `1_appear/1_idle/2_appear/2_idle`、`idle/in`。
- 静态确认普通版根组件绑定 `Bg UISpineCtrl -> New SkeletonGraphic UISpineCtrl` 的两个独立引用；Reward 版也完成
  对应双引用绑定，且引用 fileID 均指向各 prefab 内现有组件。
- `git diff --check` 对本次脚本和 prefab 通过；执行
  `dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet -consoleloggerparameters:ErrorsOnly`，结果为
  `0 error / 1124 warning`，warning 为工程现有依赖范围。
- Unity PlayMode 尚未验证两条动画的视觉同步、关闭重开、按钮开放时机和 Reward 首通流程；Android/iOS 横屏真机也未执行，
  这些结果均为 `uncertain`。需要分别打开普通版和 Reward 版，确认背景入场结束后循环待机、角色严格从 `in`
  切到 `idle`，重复打开不会停留在上一轮动画队列。

### 回滚路径

- 移除 `newSkeletonGraphicSpineCtrl` 字段和 `PlayNewSkeletonGraphicAnimation()` 调用，并删除两个 prefab 的新增字段绑定，
  即可只回滚角色 `in -> idle` 控制。
- 若连同新 `sp.json` 背景节奏一起回滚，应同步恢复 `EntranceAnimationName` 的 `01` 后缀以及两个 prefab 的原
  `spineCtrl` 绑定；不涉及存档、配置、奖励或资源迁移。

## 2026-08-19：新布局绑定与 CongraktTips 提示

### 需求、基线与影响范围

- 用户重新调整了普通版和 Reward 版 Victory prefab 布局，并要求按新层级恢复代码引用；IWanner Group 配置新增
  `congraktTips`，胜利页需要参考 `GameVictoryPanel` 使用新布局中的 `tips_txt`。
- 修改前的当前工作区中，两个根面板脚本仍在，但 Spine、next/replay、CanvasGroup 及多数 Reward 字段已经因布局替换变为
  空引用；`tips_txt` 节点存在但脚本没有字段、配置传递或本地化刷新逻辑。
- 新布局不再包含旧 `Bg + New SkeletonGraphic` 双 Spine 结构。普通版只保留 `New SkeletonGraphic`，Reward 版只保留
  `SkeletonGraphic`；其 SkeletonData 分别来自 `ui_victory_002`、`ui_victory_001`，对应 JSON 的动画清单均为
  `in / idle`。因此 2026-08-17 的双 Spine 记录已被本节的新布局事实替换。
- 影响代码与资源：
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Scripts/IWannerGame_GameVictoryPanel.cs`
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Scripts/IWannerGame_GameVictoryPanel_Reward.cs`
  - `Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`
  - 两个 `Assets/Game/UI/IWannerGame/VictoryPanel/Resources/IWannerGame_GameVictoryPanel*.prefab`

### 修改后行为与数据/UI 流

1. `IWannerPlayCtrl.HandleGroupCompletion()` 从当前 `IWannerLevelSort.CongraktTips` 取得本地化键，并传给普通版或 Reward 版
   Victory 工厂；不新增配置读取入口，也不修改自动生成的 Luban 代码或 JSON。
2. 两种面板都绑定新布局唯一的 `UISpineCtrl`，统一播放 `in -> idle`。Spine 入场完成后启动提示文案刷新，不再并行控制已经
   不存在的旧背景 Spine。
3. `tips_txt` 通过 `LocalizationManager.GetTranslation()` 读取 Group 配置键，调用 `UpdateFontAsset()` 和
   `ForceMeshUpdate()` 后播放 1 秒 DOTween 打字机动画；next/replay 在文本完成后开放，并保留 2 秒不受 timeScale 影响的
   兜底。空键、本地化缺失或文本引用缺失会记录 Bear Logger 并直接开放导航，避免结算软锁。
4. 普通版已绑定当前 `nextBtn` CanvasGroup、next/replay、`tips_txt`、唯一 Spine、分享区域和 NoAds 节点；NoAds 继续隐藏，
   分享区域按下方同日增量恢复。Reward 版还恢复绑定广告按钮、广告后 next、奖励文本、按钮组、灰度组件及原有 arrow、转盘、
   PropBlock、奖励动画、ClickBlock 引用；广告按钮在 Spine 入场完成前保持不可交互。
5. 新布局与提示本身不改变 Group 首通判定、`PassedLevels` 保存、Replay/next 事件、RewardAd、插屏或资源加载；同日后续的分享
   增量独立增加 IWanner 分享/评价存档和 `RewardType.Media` 金币流。所有提示键仍只用于当前 Victory 实例显示，不写入存档。

### 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet -consoleloggerparameters:ErrorsOnly`：成功，
  最终增量构建为 `0 error / 106 warning`；warning 为当前工程既有依赖范围。
- YAML 静态检查：普通版 `118` 个对象定义、Reward 版 `245` 个对象定义；两者均为 `0` 重复定义、`0` 缺失本地 fileID。
  根组件预期字段逐项检查均已绑定到当前 prefab 内现有对象/组件。
- 静态解析两个 SkeletonData 指向的 JSON，动画清单均为 `idle,in`。
- `tbiwannerlevelsort.json` 的 9 个 Group 均存在非空 `congraktTips`；对应 Term 在 `I2Languages.asset` 中逐项唯一命中。
- 当前两个 prefab 在任务开始前已有大范围布局改动，新增 YAML 中包含 Unity 序列化空值尾随空格，因此针对整个 prefab 的
  `git diff --check` 仍会报告这些布局差异；本次绑定字段与 C# 文件没有新增该类空白。

### 剩余风险与回滚

- Unity PlayMode 尚未实测普通版/首通 Reward 版的 `in -> idle`、文本打字机、不同语言换行、按钮开放时机、广告成功/失败、
  关闭重开和 Resources 导入，结论为 `uncertain`。
- Android/iOS 横屏的安全区、不同宽高比、触控射线、字体回退和原生广告覆盖层仍未验证，结论为 `uncertain`。
- 只回滚提示功能时，恢复两个 Victory 工厂无参调用，删除 `tipsTxt`、本地化/打字机逻辑及 prefab 的 `tipsTxt` 绑定；
  `IWannerLevelSort.CongraktTips` 配置可保留。
- 只回滚新布局代码适配时，必须同时恢复旧双 Spine prefab 层级与旧字段绑定；不能只恢复 `2_appear/1_appear` 动画名，否则
  当前唯一 Spine 资源没有对应动画。奖励与存档数据不需要迁移。

## 2026-08-19：普通版与 Reward 版恢复分享入口

### 需求纠正与影响范围

- 用户明确纠正同日适配中的“继续隐藏分享”假设：普通版分享部分参考 `GameVictoryPanel`，Reward 版参考
  `GameVictoryPanel_Reawrd + GameVictoryPanel_Share`，两种 IWanner Victory 都必须提供可用分享入口。
- 主线 `GameData.HasSharedLevel` 与 Draw `DrawGame.SharedLevelIds` 的标识空间都不适用于 IWanner GroupId，因此没有直接复用它们；
  本次在 `IWannerGame` 存档中新增 `SharedGroupIds` 和 `ReactionGroupResults`，旧档通过 `EnsureValidCollections()` 补空集合。
- 影响文件除两个 IWanner Victory 脚本/prefab 与 `IWannerPlayCtrl` 外，还包括：
  - `Assets/Game/DB/IWannerGame.cs`
  - `Assets/Game/DB/IWannerGame_Partial.cs`
  - `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_Share.cs`

### 修改后行为与数据/UI 流

1. `IWannerPlayCtrl` 创建 Victory 时一并传入当前 `IWannerLevelSort.Id`、末小关 `Path` 和 `CongraktTips`，分享奖励以 GroupId
   去重，Reward 评价打点使用末小关 Path；面板仍不自行推进 Group 进度。
2. 普通版按 `GameVictoryPanel` 行为显示分享按钮：点击后调用平台分享商店链接；当前 Group 首次点击时读取
   `GetLevelVictoryShareCoinReward()`，以 `RewardType.Media` 发金币并把 GroupId 写入 `SharedGroupIds`。再次点击仍可分享，但不再
   发奖，且 `+金币` 文本保持隐藏。当前普通版布局没有公共奖励动画实例，因此只跳过飞金币表现，不影响到账和存档。
3. Reward 版复用 prefab 现有 `GameVictoryPanel_Share`，打开时注入 IWanner 上下文，2 秒后按参考 Reward 面板调用 `Show()`；
   喜欢/不喜欢结果按 GroupId 首次写入 `ReactionGroupResults`，分享奖励按 `SharedGroupIds` 去重，不读取或写入主线/Draw 当前关。
4. Reward 分享组件已绑定 prefab 现有 `CommonUIRewardAnimCtrl`，首次分享发奖后从分享按钮播放 1 个金币飞入表现；该动画和广告
   转盘奖励共用现有控制器，但两个流程都有各自点击门禁，不改变实际 Bag 发奖来源。
5. NoAds、Group 首通/复通、Replay/next、RewardAd、胜利插屏和 `CongraktTips` 流程不变；没有新增全局事件或平台 SDK API。

### 验证证据

- 执行 Runtime 定向构建：`dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为
  `0 error / 106 warning`；warning 为工程当前既有输出。
- Prefab YAML 静态检查：普通版 `118` 个对象定义、Reward 版 `245` 个对象定义，均为 `0` 重复定义、`0` 缺失本地 fileID；
  普通版已绑定 share 按钮/奖励文本，Reward 版已绑定 `GameVictoryPanel_Share` 和公共奖励动画控制器。
- 静态确认普通版已领取后仍保留分享按钮，只隐藏奖励文本；Reward 版在评价与分享都完成后由共享组件隐藏整组，行为分别与
  对应参考脚本一致。

### 风险、未验证缺口与回滚

- Unity PlayMode 尚未实测两种面板的分享按钮射线、Reward 三按钮入场/退场、重复打开、Bag 数值刷新和旧档集合补齐；结论为
  `uncertain`。Android/iOS 原生分享面板、横屏恢复和应用切前后台也未验证。
- 与参考脚本一致，金币先写 Bag、领取状态随后保存；若 IWanner 存档写入失败，当前会记录错误但已到账金币不回滚，重启后存在
  再次领取风险。需要在设备存储异常场景单独验证。
- 只回滚本次分享接入时：恢复两个 prefab 的分享字段绑定与隐藏策略，删除 IWanner 上下文分支、`SharedGroupIds`、
  `ReactionGroupResults` 及其初始化/属性；`CongraktTips`、Spine、新布局和广告奖励逻辑可独立保留。旧存档中多出的字段可由
  序列化器忽略，无需迁移玩家进度。

## 2026-08-21：VictoryPanel 补齐主线 NOAd 购买逻辑

### 需求纠正与影响范围

- 用户明确要求 IWanner VictoryPanel 的 NOAd 采用主线胜利界面逻辑，替代此前“当前 IWanner 不包含 Victory 内免广告购买、
  打开时固定隐藏”的范围假设。
- 代码影响限于：
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Scripts/IWannerGame_GameVictoryPanel.cs`
  - `Assets/Game/UI/IWannerGame/VictoryPanel/Scripts/IWannerGame_GameVictoryPanel_Reward.cs`
- 两个 `IWannerGame_GameVictoryPanel*.prefab` 已有 `noAdPart` 绑定，本次不修改 prefab、Luban 配置、购买配置、存档结构、
  全局事件、关卡进度、分享、插屏或 Reward 金币发放逻辑。

### 修改前基线

- 普通版与 Reward 版虽然都保留从 `UICommon_btn.prefab` 继承的 NOAd 节点和 `CustomButton`，但基类在每次 `OnOpen()` 时执行
  `noAdPart.SetActive(false)`；按钮没有点击绑定、购买状态、权益判断或回调刷新，因此始终不可见且不可购买。

### 修改后行为与购买/UI 流

1. `OnCreate()` 从现有 `noAdPart` 根节点解析 `CustomButton` 并绑定购买入口；引用缺失时使用 Bear Logger 报错，并保持节点隐藏，
   不阻断 Victory 的 next/replay。
2. 每次按钮状态刷新都按主线口径检查 `PurchaseManager.IsPurchased(ProductName.NoAds)`，并兼容 Bag 中已有的
   `GameProps.NoAds` 权益：未拥有时显示并允许购买，已拥有时隐藏。
3. 点击后先校验购买管理器、重复购买状态和 `Products.GetProductId()`；通过后立即置购买门禁、刷新 UI、记录 `IAP_CLK`，再调用
   `PurchaseManager.Purchase()`。成功回调走 `OnPurchaseSuccess()`，失败或取消走 `OnPurchaseFailed()`，随后刷新权益与按钮显隐。
4. 发起原生购买前不等待 Tween 或依赖 Unity PlayerLoop 的延时。购买期间 NOAd、next、replay 均不可交互；Reward 版的激励广告
   按钮也读取同一购买门禁，避免 IAP 与 RewardAd 两个原生覆盖层并发。反向地，RewardAd 请求或领奖表现忙碌时 NOAd 也不可交互。
5. Victory 创建前 IWanner Level、Actor/HUD 输入和 GameplayPanel Block 的既有锁定边界不变；本次不提前恢复关卡输入，也不让
   购买回调推进 Group、销毁面板或触发导航。

### 验证证据

- 首次定向构建暴露 `ProductName` 缺少 `Products` 静态成员入口，修正为与主线一致的 `using static Products` 后重新执行：
  `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为
  `0 error / 106 warning`；warning 为当前工程既有输出。
- Prefab 静态检查确认：普通版 `noAdPart=2983396660610746954`、Reward 版
  `noAdPart=8910221405383816373`，两者都继承 `UICommon_btn.prefab` 根对象；该源 prefab 含 GUID
  `e6f705424d3ac3644b7f44ff3a83b0e6` 的 `CustomButton`，支持运行时 `GetComponent<CustomButton>()`。
- 两个目标脚本执行 `git diff --check` 通过；仅有工作区 LF 将按 Git 配置转为 CRLF 的提示，无空白错误。

### 风险、未验证缺口与回滚

- Unity PlayMode 尚未实测普通版/Reward 版的未购买显示、已购买隐藏、Bag 权益隐藏、重复点击、回调后刷新及与 RewardAd 的互斥，
  结论为 `uncertain`。
- Android/iOS 真实购买成功、失败、取消、弱网超时、切前后台、恢复购买、SKU/价格配置和横屏输入恢复尚未验证，结论为
  `uncertain`；当前实现复用主线 `PurchaseManager` 与 `GameSDKService.IAP_CLK` 边界，没有新增平台 SDK API。
- 回滚时删除 IWanner 基类的 NOAd 常量、运行时按钮/购买状态、购买与权益判断方法，移除 Reward 按钮的
  `IsNoAdsPurchasing` 门禁，并在 `ConfigureSupplementaryParts()` 恢复 `noAdPart.SetActive(false)`；两个 prefab 与玩家存档无需迁移。

## 2026-09-14 NoAdBtn 无网前置校验

- 旧版先设置 `_isNoAdsPurchasing` 并锁住导航，再等待购买结果；无网且无 SDK 结果时，关闭 WaitingPopup 不会恢复导航。
- 当前在锁住按钮、发送 IAP 点击打点和调用 `Purchase()` 前检查设备无网；直接复用 `OnPurchaseFailed()` 显示网络提示，并保持导航可用。普通版与 Reward 版共用此基类入口。
- 奖励、关卡推进、存档、Prefab 和 SDK 回调未变。静态顺序、差异检查及整个运行时程序集编译通过；真机切网和商店不可达仍待验证。回滚时删除此入口的断网分支即可。

## 2026-09-14 NoAdBtn 新商品与旧权益判断

- IWanner 普通版与 Reward 版复用基类 NoAdBtn。购买目标由旧 `no_ads` 改为 `newno_ads`，成功回调仍经 PurchaseManager 发放 Shop `20001` 的 NoAds 道具。
- `HasNoAds()` 同时识别新旧独立商品、新旧免广告礼包的平台资产与 Bag 权益；按钮显隐和点击拦截共用此判断，避免已购买旧商品的玩家看到可重复购买入口。原生覆盖、胜利导航和存档格式未改。
- 静态配置核对及程序集编译通过；旧账号恢复、新 SKU 可售性与 iOS/Android 横屏真机交互未验证。回滚时恢复旧购买商品名和判断，保留旧商品的恢复映射。

## 2026-09-24：OneLink 整组进入与重玩限制

- OneLink 的 IWanner sub2 可用资源 Path 或配置 level ID，命中后从所属 Group 索引 0 开始；未命中仍按传送门规则选择最低未完成目标。只解锁目标组，不提前增加通关记录。
- `IWannerGame_GameVictoryPanel.cs` 在 OnOpen 读取本轮 IWanner 来源，普通和 Reward 共用按钮刷新，隐藏 Replay 并拦截点击。标记跨组内小关推进及 Reset 保留到整组胜利；普通选组或退出清除，后续普通进入仍可重玩。资源 Prefab、奖励、广告覆盖层与关卡打点规则未修改。
- Editor 提取实际面板按钮方法的模拟覆盖隐藏/刷新/恢复/重复点击，六种面板的绑定及基类继承静态检查通过。`uncertain`：未做完整组首进关到末关胜利、真实加载失败、横屏和原生广告回调的真机检查。
- 完整基线、影响文件、验证与回滚见 [OneLink 功能与测试记录](../../../Scripts/SDK/Services/Core/OneLink功能与测试记录.md) 的每玩法一次章节。回滚只撤销本轮路由/控制器来源管理及胜利面板 Replay 限制，不删除已存的解锁和成功记录。
