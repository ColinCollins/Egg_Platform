# GMPanel 模块参考

## 1. 范围

本文记录 `Assets/Game/UI/GM/` 下 GM 调试 UI 的当前实现，重点覆盖 `Schedule Daily Test` 这一组调试入口。

相关脚本：

- `Assets/Game/UI/GM/Scripts/GMPanel.cs`
- `Assets/Game/UI/GM/Scripts/GMPanel.Generated.cs`
- `Assets/Game/UI/GM/Scripts/GMPasswordPopup.cs`
- `Assets/Game/UI/GM/Scripts/DebugModeSwitchAllBtns.cs`
- `Assets/Game/UI/GM/Scripts/GMDailyScheduleTestController.cs`
- `Assets/Game/UI/GM/Scripts/GMRemoteConfigDisplay.cs`

---

## 2. 入口链路

### 2.1 启动入口

`StartPanel.OnCreate()` 会直接调用：

```csharp
GMPanel.Create();
```

### 2.2 设置面板入口

`GameSettingPopup` 里也提供密码入口：

```csharp
GMPasswordPopup.Create();
```

`GMPasswordPopup` 校验通过后再调用：

```csharp
GMPanel.Create();
```

### 2.3 调试开关入口

`DebugModeSwitchAllBtns` 负责统一显示/隐藏调试按钮区域。

---

## 3. 模块组成

### 3.1 GMPanel

`GMPanel : BaseUIView, IEventSender`

职责：

- 作为 GM 调试功能的主面板
- 绑定 GM 按钮行为
- 展示 RemoteConfig 原始缓存
- 提供广告、购买、关卡、成就、日志、调试器等测试入口

### 3.2 GMDailyScheduleTestController

`GMDailyScheduleTestController : MonoBehaviour, IDebuger`

职责：

- 服务 `Schedule Daily Test` 分组
- 展示 `DailyScheduleModule` 当前调试文本
- 自动生成测试按钮
- 提供跨天准备和立即检查入口

当前实现特征：

- 当前只有 2 个按钮：
  - `Prepare CrossDay + Check`
  - `Refresh Daily Data`
- `Prepare CrossDay + Check` 会先把 `DB.GameSchedule["Daily"]` 写成昨天 UTC 0 点，再立即执行一次 `ScheduleSystem.CheckAllModules()`
- `Refresh Daily Data` 会直接执行一次 `ScheduleSystem.CheckAllModules()`
- 调试文本实时刷新 `Now UTC`
- 调试文本展示 2 个 daily count：
  - `DailyCount.InterstitialNoAdPopup`
  - `DailyCount.ShopFreeCoin`
- 调试文本展示 DailyChallenge 状态：
  - 当日累计进度
  - 已领取奖励阶段 id
  - `DailyChallengeManager` 是否已注册到 `DailyScheduleModule`
- 关键状态使用 rich text 做红绿区分
- 不再提供 `启动链路验证`
- 不再提供 `恢复`

### 3.3 GMPasswordPopup

`GMPasswordPopup : BaseUIView`

职责：

- 提供简单密码校验
- 输入正确后打开 `GMPanel`

### 3.4 DebugModeSwitchAllBtns

`DebugModeSwitchAllBtns : MonoBehaviour`

职责：

- 作为调试环境下的显隐总开关
- 控制 GM 按钮入口自身显隐
- 控制 `PlayCtrl.Instance.CurrentGamePlayPanel` 根节点透明度

---

## 4. GMPanel 功能分组

### 4.1 广告与 SDK 调试

- `RewardAdBtn` -> `TestForRewardAd()`
- `InterstitialBtn` -> `TestForInterstitialAd()`
- `SdkDebugBtn` -> `ShowSDKDebug()`
- `SdkLogBtn` -> `SendLogToSDK()`

### 4.2 游戏流程调试

- `NextLevelBtn` -> 活动控制器的 `IGMFeature.TryEnterNextLevel()`；Dig 按当前有效排序进入下一关，末尾拒绝。
- `LastLevelBtn` -> 活动控制器的 `IGMFeature.TryEnterPreviousLevel()`；Dig 按当前有效排序进入上一关，首关拒绝。
- `VictoryBtn` -> 活动控制器的 `IGMFeature.TryForceVictory()`；Dig 普通关经原胜利延迟和结果链结算，TBC 拒绝。
- `EnterLevelLoadingBtn` -> 打开 `EnterLevelLoading`
- `UnlockBtn` -> 批量解锁 Main／Draw／IWanner／Dig；Dig 保存成功后刷新现存选关界面，不写通关或星级。具体边界及验证见 `Assets/Game/Scripts/Play/GM调试关卡控制API功能与风险评估.md` 的 2026-10-14 章节。

### 4.3 数据与购买调试

- `DeleteBtn` -> 清空本地缓存并退出应用
- `RestoreBtn` -> 恢复购买
- `LoginBtn` -> 当前实际走 `GameAchievementUtils.TryToGetPackage()`
- `AchievementClearBtn` -> 清空成就数据
- `GetRandomSkinBtn` -> 从 Skin 配表中随机打开一个未拥有皮肤的 `GetSkinPopup`

### 4.4 UI 调试

- `WaitingPopupBtn` -> 打开等待弹窗
- `RatingPopupBtn` -> 打开评分弹窗
- `ComicsPopupBtn` -> 打开漫画弹窗
- `DebugBtn` -> 打开 Max 调试器
- `SwitchBtn` -> 切换 GM 主内容显示
- `InterstitialSwitchBtn` -> 切换插屏调试信息显示

### 4.5 Schedule Daily Test

由 `GMDailyScheduleTestController` 提供：

- `Prepare CrossDay + Check`
  - 将 `DB.GameSchedule["Daily"]` 设置为昨天 UTC 0 点
  - 调用 `DB.SaveGameSchedule()`
  - 立即调用 `ScheduleSystem.Instance.CheckAllModules()`
- `Refresh Daily Data`
  - 直接调用 `ScheduleSystem.Instance.CheckAllModules()`

当前调试文本会展示：

- `Now UTC`
- `Today UTC Start`
- `Daily Last Refresh`
- `Daily Last Refresh (Local)`
- `DailyCount.InterstitialNoAdPopup`
- `DailyCount.ShopFreeCoin`
- `DailyChallenge.Progress`
- `DailyChallenge.ClaimedRewardStageIds`
- `DailyChallenge Registered`
- `Prediction`
- `Prepared CrossDay`
- `Module Registered`
- `GameSchedule Ready`
- `Last Action`

---

## 5. 当前注意点

### 5.1 这套工具优先验证 Daily 判定和落库

它能确认：

- `DB.GameSchedule["Daily"]` 是否被正确写入
- `ScheduleSystem.CheckAllModules()` 之后 `Daily` 时间戳是否推进到今天 UTC 0 点
- 当前 `GameSchedule` 内的两个 daily count 当前值是多少
- `DailyChallenge` 累计进度、已领奖阶段和刷新模块注册状态

它不能单独保证：

- 所有业务模块都已经注册到 `DailyScheduleModule`
- 某个临时模块在面板关闭状态下仍能收到跨天刷新

### 5.2 Shop daily count 是否重置取决于注册时机

`DailyCount.ShopFreeCoin` 只是展示当前值。  
如果对应模块没有注册进 `DailyScheduleModule`，`CheckAllModules()` 只会推进 `Daily` 时间戳，不一定会把业务计数清零。

### 5.3 DeleteAllData 仍是高风险操作

当前逻辑：

```csharp
DBManager.Instance.ClearPlayerPrefsCache();
DBManager.Instance.ClearJsonCache();
Application.Quit();
```

如果后续继续保留在可达 UI 中，建议增加确认弹窗或编译宏保护。

---

## 6. 2026-06-30 GetRandomSkin 调试入口

### 6.1 需求背景

GMPanel 需要补充 `GetRandomSkin` API，方便调试获得随机皮肤弹窗和所有权授予链路。

### 6.2 影响范围

- 运行时代码：`Assets/Game/UI/GM/Scripts/GMPanel.cs`
- 已存在序列化引用：`Assets/Game/UI/GM/Scripts/GMPanel.Generated.cs` 中的 `GetRandomSkinBtn`
- 已存在 prefab 绑定：`Assets/Game/UI/GM/Resources/GMPanel.prefab` 中的 `GetRandomSkinBtn`
- 复用系统：`ConfigManager.Instance.Tables.TbSkin.DataList`、`DB.GameData.OwnedSkin`、`GetSkinPopup.Create(skin)`、`SimpleBag.GetNewSkin(...)`

本次未修改 Skin 配表、存档字段、资源加载规则、ShopPanel、Actor Skin 绑定、广告、内购、analytics 或平台 SDK。

### 6.3 原行为基线

- `GMPanel.OnCreate()` 已绑定 `GetRandomSkinBtn` 到 `GetRandomSkin(...)`。
- `GetRandomSkin(...)` 原本只有注释，没有实际筛选或打开弹窗。
- `GMPanel.OnClose()` 原本没有解绑 `ClearRemoteConfigBtn` 和 `GetRandomSkinBtn`。

### 6.4 新行为

- 点击 `GetRandomSkinBtn` 时，先检查 `PlayCtrl.Instance.Bag` 是否存在；缺失时只记录错误并返回。
- 从 `TbSkin.DataList` 中收集候选皮肤，过滤空 id、已拥有 id、`SkinUnlockType.Default` 和 `SkinUnlockType.Unavailable`。
- 使用 `UnityEngine.Random.Range(...)` 从候选中随机选择一个皮肤。
- 找到候选后调用 `GetSkinPopup.Create(skin)`，由现有弹窗链路展示皮肤并通过 `SimpleBag.GetNewSkin(...)` 授予所有权。
- 没有候选时记录 warning，并显示 `SystemTips` 提示。
- `GMPanel.OnClose()` 现在会解绑 `ClearRemoteConfigBtn` 和 `GetRandomSkinBtn`，避免面板重复打开后回调叠加。

### 6.5 数据、UI、保存和事件流

- GMPanel 只读 `TbSkin.DataList` 和 `DB.GameData.OwnedSkin`，不直接写入 `OwnedSkin`。
- 所有权写入仍发生在 `GetSkinPopup.Create(skin)` 内部的 `GrantSkin(...) -> SimpleBag.GetNewSkin(skin.Id)`。
- `GetSkinPopup` 授予路径只保存所有权，不穿戴、不刷新 ShopPanel Skin 列表、不派发 `ActorSkinSelectionChangedEvent`。
- 本入口只属于 GM 调试 UI，不改变玩家正常 Skin 商店、免费皮肤弹窗或关卡 Actor 外观绑定流程。

### 6.6 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 97 warnings；warnings 属于当前工程既有范围。
- `git diff --check -- Assets/Game/UI/GM/Scripts/GMPanel.cs` 通过，仅提示工作区 LF/CRLF 规范化。
- 静态检查确认 `GetRandomSkinBtn` 已在 `GMPanel.Generated.cs` 中存在，并在 `GMPanel.prefab` 中已有序列化绑定。

### 6.7 未验证风险

- 尚未执行 Unity PlayMode，仍需验证点击 `GetRandomSkinBtn` 后能打开 `GetSkinPopup`、展示随机皮肤名称和图标，并把该皮肤 id 写入 `DB.GameData.OwnedSkin`。
- 尚未验证候选全部拥有时的 `SystemTips` 提示层级和文案显示。
- 尚未执行 Android/iOS 横屏 smoke；该入口属于 GM 调试 UI，仍需在需要的调试包中确认触摸链路。

### 6.8 回滚路径

回滚 `GMPanel.cs` 中 `GetRandomSkin(...)`、`FindRandomUnownedSkin()`、`CanRandomGrantSkin(...)` 的实现，以及 `OnClose()` 中 `GetRandomSkinBtn` 的解绑，即可恢复为按钮无实际随机皮肤行为的旧状态。

---

## 7. 2026-07-20 跨天测试显示 DailyChallenge

### 7.1 需求背景与原行为

- GMPanel 的跨天测试页原本只显示 Daily 时间戳、插屏免广告弹窗次数和商店免费金币次数。
- `DailyChallengeManager.OnScheduleRefresh()` 会清空 `DB.GameSchedule.DailyChallengeCurrentProcess` 与 `DailyChallengeHasRewardId`，但测试页没有直接展示这两项，也无法看到 Manager 是否已注册到 `DailyScheduleModule`。

### 7.2 影响范围与新行为

- 仅修改 `Assets/Game/UI/GM/Scripts/GMDailyScheduleTestController.cs` 和本地说明文档。
- 调试文本新增 `每日挑战.累计进度`，直接读取 `GameSchedule.DailyChallengeCurrentProcess`。
- 调试文本新增 `每日挑战.已领取奖励阶段`，列表为空显示“无”，存档不可用显示 `N/A`。
- 调试文本新增 `DailyChallenge 已注册`，通过当前 `DailyScheduleModule.GetRefreshable<DailyChallengeManager>()` 显示红绿注册状态。
- 未修改 GMPanel prefab、按钮、每日挑战配置、存档结构、跨天执行逻辑、事件、资源、广告、analytics 或平台 SDK。

### 7.3 数据与 UI 流

```text
DB.GameSchedule.DailyChallengeCurrentProcess
DB.GameSchedule.DailyChallengeHasRewardId
DailyScheduleModule.GetRefreshable<DailyChallengeManager>()
        -> GMDailyScheduleTestController.BuildDebugText()
        -> GMPanel 跨天测试页 debugText
```

本次只读并显示当前状态；跨天按钮仍通过原有 `PrepareCrossDay() -> ScheduleSystem.CheckAllModules()` 链路触发刷新和保存。

### 7.4 验证记录

- `git diff --check -- Assets/Game/UI/GM/Scripts/GMDailyScheduleTestController.cs` 通过，仅有工作区 LF/CRLF 规范化提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 个错误、1117 个项目既有警告。
- 静态检查确认本次没有新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、全局事件或额外存档写入。

### 7.5 剩余风险与回滚

- 尚未在 Unity PlayMode 打开 GMPanel 验证新增三行的实际排版、跨天前后数值变化和红绿状态。
- 尚未执行 Android/iOS 横屏调试包 smoke；本次只增加调试文本，不改变玩家正常 DailyChallenge 流程。
- 回滚 `BuildDebugText()` 中三行输出、DailyChallenge 注册查询和两个只读格式化方法即可恢复原显示，不涉及数据迁移或 prefab 回滚。

---

## 8. 2026-07-20 OnGUI 显示 Bag Star 数量

### 8.1 需求背景与影响范围

- GMPanel 需要在既有 `OnGUI()` 调试信息中补充当前 Bag Star 数量，方便对照获得 100 Star、清空 Star 和明信片扣星结果。
- 仅修改 `Assets/Game/UI/GM/Scripts/GMPanel.cs` 与本地说明文档。
- 本次未修改 `GMPanel.prefab`、Bag 存档结构、配置、资源、事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。

### 8.2 原行为与新行为

- 原行为：GM 主内容展开后，左上角 `OnGUI()` 依次显示当前语言、用户 ID 和 SDK 版本，没有显示 Bag Star 数量。
- 新行为：SDK 版本下方新增 `Bag Star数量`，通过 `PlayCtrl.Instance?.Bag?.GetToolCount(GameProps.Star)` 读取当前值；PlayCtrl 或 Bag 尚未就绪时显示 `0`，不阻断 GMPanel 绘制。

### 8.3 数据与 UI 流

```text
GMPanel.OnGUI()
    -> PlayCtrl.Instance?.Bag
    -> SimpleBag.GetToolCount(GameProps.Star)
    -> DB.GameData.GameTools 只读查询
    -> GUI.Label 显示数量
```

该显示不写入 Bag、不保存 DB、不派发道具事件；Star 的增减仍由现有 GM 按钮和业务链负责。

### 8.4 验证记录

- `git diff --check -- Assets/Game/UI/GM/Scripts/GMPanel.cs`：通过。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly`：构建成功，0 个错误、371 个警告。
- 静态核对：显示读取口径与 `Get100Star(...)`、`RemoveAllStar(...)` 使用的 `GameProps.Star` 一致，并包含 PlayCtrl/Bag 空值保护。

### 8.5 剩余风险与回滚

- 尚未在 Unity PlayMode 打开 GMPanel 验证新增行的实际排版，以及获得、清空和消费 Star 后的实时数值变化。
- 尚未执行 Android/iOS 横屏调试包 smoke；本次仅增加 GM 调试文字，不改变正常玩家 UI。
- 回滚 `GMPanel.OnGUI()` 中 Star 数量读取和对应 `GUI.Label` 即可恢复原显示，不涉及数据迁移或 prefab 回滚。

---

## 9. 2026-07-21 OnGUI 显示 DailyChallengeCurrentProcess

### 9.1 需求背景与影响范围

- GMPanel 需要参考既有 Bag Star 显示，在 `OnGUI()` 调试信息中补充每日挑战当日累计进度。
- 仅修改 `Assets/Game/UI/GM/Scripts/GMPanel.cs` 与本地说明文档。
- 本次未修改 `GMPanel.prefab`、每日挑战配置、GameSchedule 存档结构、跨天逻辑、事件、资源、analytics、广告、IAP、RemoteConfig 或平台 SDK。

### 9.2 原行为与新行为

- 原行为：GM 主内容展开后，`OnGUI()` 在 SDK 版本下方显示 `Bag Star数量`，没有直接显示 `DailyChallengeCurrentProcess`。
- 新行为：在 Bag Star 下方新增 `每日挑战进度：` 数值显示，直接读取 `DB.GameSchedule?.DailyChallengeCurrentProcess`；GameSchedule 尚未就绪时显示 `0`，不阻断 GMPanel 绘制。采用中文短标签，避免较长的字段名在 GMPanel 中显示不全。

### 9.3 数据与 UI 流

```text
GMPanel.OnGUI()
    -> DB.GameSchedule?.DailyChallengeCurrentProcess
    -> GUI.Label 显示数值
```

该显示只读当前存档状态，不写入 GameSchedule、不触发保存、不派发事件；进度增长和跨天清理仍由现有每日挑战及 GM 调试链路负责。

### 9.4 验证记录

- `git diff --check -- Assets/Game/UI/GM/Scripts/GMPanel.cs`：通过，仅提示工作区 LF/CRLF 规范化。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：构建成功，0 个错误、99 个警告。
- 静态核对：新增显示沿用 Star 的 `OnGUI()` 行布局，使用下一行坐标，并包含 GameSchedule 空值保护；未新增运行时写入或编辑器生命周期入口。

### 9.5 剩余风险与回滚

- 尚未在 Unity PlayMode 打开 GMPanel 验证新增行的实际排版，以及每日挑战进度变化后数值是否实时反映。
- 尚未执行 Android/iOS 横屏调试包 smoke；本次仅增加 GM 调试文字，不改变正常玩家每日挑战流程。
- 回滚 `GMPanel.OnGUI()` 中 `dailyChallengeCurrentProcess` 读取和对应 `GUI.Label` 即可恢复原显示，不涉及数据迁移或 prefab 回滚。

---

## 10. 2026-07-23 gmConfig 补充当前读取数据说明

### 10.1 需求背景与影响范围

- GMPanel 的 `gmConfig` 原本只格式化展示 `DB.GameData.RemoteConfigCache` 中的键值，界面上没有直接标出当前读取对象。
- 仅修改 `Assets/Game/UI/GM/Scripts/GMPanel.cs` 与本文档。
- 本次未修改 `GMPanel.prefab`、RemoteConfig 拉取、版本校验、缓存写入、存档结构、配置、资源、事件、analytics 或平台 SDK。

### 10.2 原行为与新行为

- 原行为：`GMPanel.OnShow()` 调用 `RefreshGmConfigRemoteDisplay()`，按 key 展示缓存 JSON；缓存为空时只显示无缓存提示。
- 新行为：JSON 前只增加一行 `当前读取：DB.GameData.RemoteConfigCache（业务生效缓存）`。
- 缓存为空时继续显示该行和无缓存状态，不触发 RemoteConfig 拉取或 DB 写入。

### 10.3 数据与 UI 流

```text
GMPanel.OnShow()
    -> DB.GameData.RemoteConfigCache 只读
    -> 显示一行当前读取数据说明
    -> 按 key 格式化缓存 JSON
    -> gmConfig.text
```

该显示只读取现有业务缓存；不会读取 Firebase SDK 当前激活值，不会写存档，也不会改变正常 RemoteConfig 回退与业务取值。

### 10.4 验证记录

- `git diff --check -- Assets/Game/UI/GM/Scripts/GMPanel.cs DevelopMD/GMPanel_Reference.md`：通过，仅提示工作区 LF/CRLF 规范化。
- `dotnet build .\Assembly-CSharp.csproj --no-restore`：运行超过 4 分钟仍未结束，已停止本次构建进程；没有获得编译成功或失败结论。
- 静态检查确认新增界面文案只有一行，缓存 JSON 格式化逻辑和只读数据流保持不变。
- 尚未在 Unity PlayMode 打开 GMPanel 验证长文本布局与滚动位置。

### 10.5 剩余风险与回滚

- 该行只说明当前展示对象是业务生效缓存，不能证明缓存值来自远端、历史缓存或包内默认值。
- 尚未执行 Android/iOS 横屏调试包 smoke；本次仅增加 GM 调试文字，不改变玩家正常 UI。
- 回滚 `RefreshGmConfigRemoteDisplay()` 中诊断头构建，并恢复原空缓存文案，即可恢复旧显示，不涉及数据迁移或 prefab 回滚。

---

## 11. 2026-07-27 插屏数据按活动 GameController 延迟展示

### 11.1 需求背景与原行为

- GMPanel 创建后即把 `_isInterstitialConfigVisible` 初始化为 `true`，即使仍在启动页、
  `PlayControllerManager.ActiveController` 为空，也会进入插屏数据绘制链。
- 原 `ShowInterstitialData()` 固定读取 `DB.GameData` 与主线 `ad_*` 间隔配置；
  Draw 控制器激活后仍会显示主线失败次数和成功间隔，不符合两种玩法的独立插屏状态。
- 本次需求是默认不展示插屏数据，只有确定进入一个有效 GameController 后才展示该玩法数据。

### 11.2 影响范围与新行为

- 运行时代码只修改 `Assets/Game/UI/GM/Scripts/GMPanel.cs`，未修改 prefab、Controller
  生命周期、广告 Policy、RemoteConfig、存档结构、广告 SDK、analytics 或平台分支。
- 插屏数据显示初始为关闭；未进入玩法时开关文字显示
  `插屏信息: false (未进入玩法)`，且不读取主线或 Draw 插屏存档。
- `GMPanel.OnShow()` 和 `OnGUI()` 通过
  `PlayControllerManager.ActiveController` 确认 Controller 已初始化、已激活并实现
  `IInterstitialAdPolicyHost` 后，才建立显示上下文。
- 首次进入 Main/Draw 或发生 Controller 切换时自动展示对应玩法数据；同一 Controller
  内手动关闭后不会被逐帧同步重新打开。GMPanel 再次显示时按当前 Controller 重置一次。
- Main 只显示 `DB.GameData` 与 `ad_*` 间隔；Draw 只显示 `DB.DrawGame` 与
  `drawGame_*` 间隔。当前 CD 统一来自活动 Controller 提供的 Policy。
- 原关卡耗时调试信息只在 Main Controller 下继续展示，Draw 下不读取停用主线的关卡状态。

### 11.3 Controller 与 UI 数据流

```text
GMPanel.OnShow / OnGUI
    -> GameManager.ControllerManager.ActiveController
    -> IsInitialized && IsActive
    -> IInterstitialAdPolicyHost.InterstitialAdPolicy
        -> 无有效 Controller：关闭插屏显示，不读插屏 DB
        -> Main：DB.GameData + ad_* + InterstitialAdPolicy.CurrentInterstitialCD
        -> Draw：DB.DrawGame + drawGame_* + DrawInterstitialAdPolicy.CurrentInterstitialCD
    -> GM OnGUI 标签
```

该链路仅执行调试数据显示，不修改插屏计数、CD、RemoteConfig 或存档。

### 11.4 验证记录

- `git diff --check -- Assets/Game/UI/GM/Scripts/GMPanel.cs`：通过，仅有工作区
  LF/CRLF 规范化提示。
- 定向执行
  `dotnet build Assembly-CSharp.csproj --no-dependencies --no-restore ...`：
  构建成功，0 个错误、0 个警告。
- 完整依赖重建第一次超时，第二次因残留 MSBuild 节点与 VS Code 构建宿主占用
  `Temp/bin`、`Temp/obj` DLL 而失败；错误为 `MSB3248`、`CS2012` 文件锁，
  没有取得完整工程编译结论。
- 静态确认无有效 Controller 时不会调用 `ShowInterstitialData()`；Main 与 Draw
  分支分别读取各自 DB 和 RemoteConfig getter；未新增全局事件、存档写入或编辑器生命周期入口。

### 11.5 剩余风险与回滚

- `uncertain`：尚未在 Unity PlayMode 验证启动页默认隐藏、进入 Main 自动展示、
  Main 内手动关闭、切换 Draw 后自动显示 Draw 数据、返回启动页隐藏这五条 UI 路径。
- 尚未执行 Android/iOS 横屏调试包 smoke；本次只调整 GM 调试文字和绘制条件，
  不改变玩家广告展示链。
- 回滚 `_interstitialDisplayController`、Controller 上下文解析及 Main/Draw 数据分支，
  恢复 `_isInterstitialConfigVisible = true` 和原 `ShowInterstitialData()`，即可恢复旧显示；
  不涉及数据迁移或 prefab 回滚。

---

## 12. 2026-08-04 RemoteConfig 按 ConfigKey 分页显示

### 12.1 需求背景与影响范围

- RemoteConfig 业务缓存已同时包含 `level_config` 与 `drawgame_config`，原 GMPanel 会把缓存内所有非空 key 一次性拼接到同一个文本中。
- 用户在 `Assets/Game/UI/GM/Resources/GMPanel.prefab` 的 RemoteConfig 区域新增了原生 `UnityEngine.UI.Button` 类型的 `next_btn` 与 `last_btn`；本次补齐按钮切换行为。
- 新增 `Assets/Game/UI/GM/Scripts/GMRemoteConfigDisplay.cs` 并挂载到 RemoteConfig 子节点；该组件独立持有文本、按钮、当前 key 和 JSON 格式化职责，`GMPanel.cs` 只保留区域显隐控制。
- 未修改 RemoteConfig 拉取、回退、解析、缓存写入、存档结构、业务 getter、事件、analytics 或平台 SDK。

### 12.2 原行为与新行为

- 原行为：每次刷新会按 key 排序并连续展示 `DB.GameData.RemoteConfigCache` 的全部非空 JSON。
- 新行为：默认展示 `ConfigManager.RemoteConfig.LevelConfigKey`；点击 next/last 后循环切换到 `DrawGameConfigKey` 或返回 `LevelConfigKey`。
- 标题区会显示当前 `ConfigKey`；当前 key 不存在或 JSON 为空时显示“当前 ConfigKey 无 RemoteConfig 缓存”，不会回退展示另一个 key。
- `GMRemoteConfigDisplay` 在运行态 `OnEnable()` 绑定按钮并刷新，在 `OnDisable()` 解绑；切换只刷新当前 `configText.text`。编辑器非运行态不会读取 DB。

### 12.3 数据与 UI 流

```text
nextBtn / lastBtn
    -> GMRemoteConfigDisplay 更新当前索引（两个 key 循环）
    -> DB.GameData.RemoteConfigCache 只读查询当前 key
    -> JSON 格式化
    -> configText.text
```

该流程不调用 `RemoteConfigService` 拉取接口，不调用 `SetRaw`，不保存 DB，也不改变 Main/Draw 业务读取边界。Android 与 iOS 共享相同的 GM UI 只读逻辑，没有新增平台分支。

### 12.4 验证记录

- 将 Unity 尚未自动收录的新脚本临时加入 `Assembly-CSharp.csproj` 后执行 `dotnet build .\Assembly-CSharp.csproj --no-dependencies --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：构建成功，0 个错误、101 个项目既有警告；临时工程项已移除，未留下 csproj 差异。
- 静态确认 prefab 的 `GMRemoteConfigDisplay` 已绑定文本与两个新增 `CustomButton`；组件包含按钮空值保护和运行态外层保护。
- 静态确认切换链只读 `RemoteConfigCache` 并刷新文本，未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、全局事件或存档写入。

### 12.5 剩余风险与回滚

- `uncertain`：尚未在 Unity PlayMode 验证默认页、双向循环切换、长 JSON 滚动位置和当前 key 缺失提示。
- 尚未执行 Android/iOS 横屏调试包触摸 smoke；本次只改变 GM 调试界面，不改变玩家正常 RemoteConfig 业务链。
- 回滚时移除 `GMRemoteConfigDisplay` 组件和脚本，把旧的全缓存遍历与文本绑定恢复到 `GMPanel`，并移除两个按钮对象，即可恢复旧显示；不涉及数据迁移。

---

## 13. 2026-08-05 Main/Draw 全关解锁与直接胜利

### 13.1 原行为与新行为

- 原“解锁全部关卡”只逐关调用主线 `LevelCtrl.UnlockLevel()`，不处理 Draw 且会重复保存；现在分别调用 Main/Draw 的批量 API，各玩法最多保存一次，并覆盖两边当前最终有效排序。
- 原“直接胜利”只派发主线 `SwitchGameStateEvent(SUCCESS)`；现在先检查 `PlayControllerManager.ActiveController`，Draw 活动时改调 `DrawPlayCtrl.TryForceVictory()`，Main 保留原事件链。
- Draw 强制胜利只在当前单关处于可结算的 Drawing 阶段时接受，并通过 `IDrawLevelRuntime.TryResolve()` 进入原成功插屏、统计、每日挑战、存档和胜利面板链；选关、加载、暂停和重复结算会被拒绝。

### 13.2 验证、风险与回滚

- 定向差异检查通过；`Assembly-CSharp` 构建成功，0 个错误、101 个项目既有警告。
- 未新增 prefab、配置、资源、存档字段、全局事件、analytics 接口或平台分支。
- `uncertain`：Unity PlayMode、保存失败、重复点击、每日挑战/成功插屏以及 Android/iOS 横屏调试包尚未验证。
- 完整数据流、风险与回滚见 `Assets/Game/Scripts/Play/GM调试关卡控制API功能与风险评估.md`。

---

## 14. 2026-08-05 RemoteConfig 切换按钮引用修复

### 14.1 问题与修复

- 运行态点击 `next_btn` / `last_btn` 后显示内容不切换；代码检查确认索引循环和文本刷新逻辑完整。
- `GMRemoteConfigDisplay.nextBtn`、`lastBtn` 原错误声明为项目 `CustomButton`，但 prefab 实际挂载的是原生 `UnityEngine.UI.Button`；类型不兼容使 Unity 无法持久化引用，当前 prefab 中两个字段均被清成 `{fileID: 0}`，`OnEnable()` 因此跳过回调绑定。
- 修复把两个序列化字段改为 `Button`，通过 `Button.onClick` 在 `OnEnable()` / `OnDisable()` 成对绑定和解绑，并恢复现有按钮组件引用：`next_btn` 对应 `5925334681961581233`，`last_btn` 对应 `7611169058311073725`；未修改 RemoteConfig 缓存、拉取、解析、存档或业务 getter。

### 14.2 验证、风险与回滚

- 静态确认两个目标 fileID 均属于 prefab 内现存且启用的原生 `Button`，`GMRemoteConfigDisplay` 字段类型匹配且不再为空；`git diff --check` 通过。
- `uncertain`：尚未在 Unity PlayMode 实际点击验证 `level_config` 与 `drawgame_config` 双向循环，也未执行 Android/iOS 横屏触摸 smoke。
- 回滚只需再次清空两个序列化引用，但会恢复按钮无响应问题；不涉及数据迁移。

---

## 15. 2026-08-05 解锁所有皮肤调试入口

### 15.1 需求背景与影响范围

- GMPanel 已由用户在 `Assets/Game/UI/GM/Resources/GMPanel.prefab` 新增“解锁所有皮肤”按钮，并在 `Assets/Game/UI/GM/Scripts/GMPanel.Generated.cs` 生成 `UnlockSkinBtn` 序列化字段；原运行时代码尚未消费该字段。
- 本次运行时代码只修改 `Assets/Game/UI/GM/Scripts/GMPanel.cs`，补齐按钮绑定、解绑和批量授予逻辑；未改写现有 prefab、Generated 文件、Skin 配置、存档结构、资源、广告、IAP、RemoteConfig 或平台分支。
- 复用 `ConfigManager.Instance.Tables.TbSkin.DataList`、`GameManager.Instance.Bag`、`SimpleBag.GetNewSkin(...)`、`DB.GameData` 和 `SystemTips`。

### 15.2 原行为与新行为

- 原行为：`UnlockSkinBtn` 虽已存在且在 prefab 中绑定，但 `GMPanel.OnCreate()` 没有注册点击回调，因此点击不会解锁皮肤。
- 新行为：`OnCreate()` 将按钮绑定到 `UnlockAllSkins(...)`，`OnClose()` 成对解绑，避免面板重复生命周期产生重复回调。
- 点击后遍历当前 `TbSkin.DataList`，跳过空对象、空 ID 和 `SkinUnlockType.Unavailable` 占位项；其余配置行（包括 Default、Coin、Ad、Special、Purchase）均通过 `SimpleBag.GetNewSkin(skin.Id, GetSkinWayType.Free, false)` 尝试授予。
- `GetNewSkin(...)` 负责去重；本轮有实际新增时只调用一次 `DB.GameData.Save()`。重复点击不会重复增加所有权，并显示“所有可用皮肤已解锁”。
- Bag、GameData 或 Skin 配表未初始化时只记录错误并显示失败提示，不写入部分空数据。

### 15.3 数据、UI、保存与打点流

```text
UnlockSkinBtn
    -> GMPanel.UnlockAllSkins()
    -> TbSkin.DataList（过滤空 ID / Unavailable）
    -> SimpleBag.GetNewSkin(id, Free, false)
        -> DB.GameData.OwnedSkin 去重新增
        -> DB.GameData.WaitingPreviewSkinId 去重新增
        -> NewSkinRedDotQuery.Refresh()
        -> GameSDKService.GetNewSkin(id, Free)
    -> 本轮有新增时 DB.GameData.Save() 一次
    -> SystemTips 显示新增数量或已全部解锁
```

- 该 GM 入口只授予所有权，不自动穿戴、不修改 `CurrentSkinId` / 当前部位选择，也不主动刷新已打开的 ShopPanel 列表。
- 因复用 `Free` 来源，实际新增皮肤会沿用既有待预览红点和 `getNewSkin` 获得打点；未新增的已有皮肤不会重复打点。
- 未新增全局事件、资源加载、每帧轮询或移动平台差异逻辑；Android/iOS 共用同一调试入口。

### 15.4 验证记录

- CodeGraph 已确认 `OnCreate()` 与 `OnClose()` 都引用 `UnlockAllSkins(...)`，方法调用链包含 `GetNewSkin(...)`、`Save()` 和 `SystemTips.Show(...)`。
- `dotnet build .\Assembly-CSharp.csproj --no-dependencies --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：构建成功，0 个错误、101 个项目既有警告。
- `git diff --check -- Assets/Game/UI/GM/Scripts/GMPanel.cs` 通过；连同用户已有 `GMPanel.prefab` 增量检查时发现其新增 Unity YAML 块存在 8 处尾随空格，本次未修改该资产以避免覆盖用户编辑。
- 静态检查确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`PlayerPrefs`、实例化或新的事件派发。

### 15.5 剩余风险与回滚

- `uncertain`：尚未在 Unity PlayMode 点击验证新增数量提示、重复点击、ShopPanel 所有权刷新和新皮肤红点表现。
- `uncertain`：尚未执行 Android/iOS 横屏调试包触摸 smoke，也未模拟批量保存失败或 SDK 打点异常；`GetNewSkin(...)` 会按新增皮肤逐条调用既有获得打点。
- 如需要让 GM 批量解锁不产生红点或业务打点，应先扩展并确认专用 GM 来源语义，不能静默伪装为 Purchase/Ad。
- 回滚 `GMPanel.cs` 中 `UnlockSkinBtn` 的绑定/解绑和 `UnlockAllSkins(...)` 方法即可恢复按钮无运行时功能的旧状态；不涉及存档迁移，已解锁的测试存档需通过既有清档入口恢复。

---

## 16. 2026-08-05 完成所有明信片调试入口

### 16.1 需求背景与影响范围

- 用户已在 `Assets/Game/UI/GM/Resources/GMPanel.prefab` 新增“解锁所有明信片”按钮，并在 `Assets/Game/UI/GM/Scripts/GMPanel.Generated.cs` 生成 `UnlockPostcardBtn` 序列化字段；原 `GMPanel` 没有注册该按钮。
- 本次运行时代码只修改 `Assets/Game/UI/GM/Scripts/GMPanel.cs`，补齐按钮绑定、解绑和完成全部普通 Postcard 配置的存档逻辑；用户已有 prefab 与 Generated 增量未被改写。
- 数据来源为 `Assets/Game/Configs/tbpostcardconfig.json` / `TbPostcardConfig.DataList`，写入既有 `DB.Postcard` 字段并通过 `DB.SavePostcard()` 保存。

### 16.2 原行为与新行为

- 原行为：`UnlockPostcardBtn` 已绑定到 GMPanel prefab，但 `OnCreate()` 未注册点击回调，点击不会修改 Postcard 状态。
- 新行为：`OnCreate()` 绑定 `CompleteAllPostcards(...)`，`OnClose()` 成对解绑。
- 存档入口先检查 `Application.isPlaying`，编辑器非运行态不会访问 DB 或写入存档。
- 点击后按 `PostcardPopup.IsUnlockConfig(...)` 的同一判定排除无进度、无奖励且倍率不大于 0 的尾页配置；当前 14 行配置中完成 13 个普通明信片，跳过 `Postcard_unlock`。
- 所有普通配置 ID 去重写入 `DB.Postcard.CompletedId`；清空 `CurrentCollectedPuzzledId`，无有效当前页时回到 Sort 最小的普通配置，并设置 `HasGuide=true`、`IsGuiding=false`，使存档处于已开放且不再等待首次引导的状态。
- 重复点击不会重复加入 ID；保存失败时恢复 `CompletedId`、当前拼图进度、`CurrentId` 和两个引导字段的原内存值。

### 16.3 数据、UI、奖励与事件流

```text
UnlockPostcardBtn
    -> GMPanel.CompleteAllPostcards()
    -> TbPostcardConfig.DataList
    -> 过滤 Postcard_unlock 尾页配置
    -> DB.Postcard.CompletedId 去重新增全部普通配置 ID
    -> CurrentCollectedPuzzledId 清空
    -> HasGuide=true / IsGuiding=false
    -> DB.SavePostcard() 一次
        -> 成功：刷新 RedDotKey.Postcard，显示完成数量
        -> 失败：恢复本次修改前内存状态并提示失败
```

- 该 GM 入口不会扣除 Bag Star，不伪造 `LevelCollectedStar`，不修改 `LevelWaitingAnimStar`，也不执行页面动画。
- 已完成状态会让 Postcard 页面按 `CompletedId` 直接显示完整内容和满 Chest 进度，但本入口不发放每页 Coin/Skin 等完成奖励；用途是内容和完成态调试，不是奖励补发。
- 未新增全局事件、资源加载、analytics、广告、IAP、RemoteConfig 或平台分支；只在保存成功后调用现有 `RedDotManager.Refresh(RedDotKey.Postcard)`。

### 16.4 验证记录

- 静态解析 `tbpostcardconfig.json`：总配置 14 行，普通可完成配置 13 行，尾页解锁配置 1 行。
- 静态确认 `GMPanel.Generated.cs` 存在 `UnlockPostcardBtn`，`GMPanel.prefab` 已绑定 `fileID: 5503772189998758326`。
- `git diff --check -- Assets/Game/UI/GM/Scripts/GMPanel.cs` 通过。
- `dotnet build .\Assembly-CSharp.csproj --no-dependencies --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：构建成功，0 个错误、101 个项目既有警告。

### 16.5 剩余风险与回滚

- `uncertain`：尚未在 Unity PlayMode 验证首次点击新增 13 页、部分完成存档的新增数量、重复点击、保存失败回滚、红点消失以及关闭 GM 后重新打开 PostcardPopup 的完整视觉状态。
- 已经打开的 PostcardPopup 不接收本次 GM 数据变更事件，需关闭并重新打开以重新加载全部页面；本次没有为调试入口新增全局刷新事件。
- `uncertain`：尚未执行 Android/iOS 横屏调试包触摸 smoke；本入口没有平台差异代码。
- 回滚 `GMPanel.cs` 中 `UnlockPostcardBtn` 的绑定/解绑、`CompleteAllPostcards(...)` 和 `IsCompletablePostcardConfig(...)` 即可恢复按钮无运行时功能的旧状态；已写入的完成测试存档需要通过既有清档入口恢复。

---

## 17. 2026-08-10 IWanner 上一小关与直接胜利

### 17.1 行为与代码边界

- 新增 `IGMFeature` 统一声明 GM 上一关、下一关和直接胜利能力；Main、Draw、IWanner Controller 分别实现自身
  支持范围，GMPanel 不再依赖任何具体 Controller 类型。
- `GMPanel` 的关卡切换/直接胜利按钮实现已迁入 `GMPanel.PlayController.cs` partial，只解析活动
  `IGMFeature` 并转发；无活动 Controller 时拒绝命令。原按钮字段、OnCreate/OnClose 绑定和 prefab 均未改变。
- IWanner 活动时，“上一关”调用 `IWannerPlayCtrl.TryEnterPreviousLevel()` 返回当前 Group 的上一小关，并复用
  已有黑幕销毁重建链；第一小关、加载中或运行时无效时只记录 warning。
- IWanner 活动时，“胜利”调用 `TryForceVictory(nameof(GMPanel))`，最终由当前 `IWannerLevelCtrl.TrySuccess()`
  统一接受或拒绝，成功后继续原进门表现与组内推进。
- Main 与 Draw 的直接胜利链保持原行为；Draw/IWanner 的 `TryEnterNextLevel()` 与 Draw 的
  `TryEnterPreviousLevel()` 明确返回 false，GMPanel 不会再因不支持命令而误改主线关卡。

### 17.2 验证、风险与回滚

- `Assembly-CSharp` 定向构建通过：`0 error / 101 warning`；未新增全局事件、存档、配置、资源加载、广告、
  analytics 或平台分支。
- `uncertain`：Unity PlayMode 的第一小关拒绝、连续回退、黑幕切换、暂停/死亡/重复胜利与 Android/iOS
  横屏触控尚未验证。
- 完整调用链、风险与回滚见
  `Assets/Game/Scripts/Play/GM调试关卡控制API功能与风险评估.md` 第 6 节。

---

## 18. 2026-08-19 GMPanel 查看 iwannargame_config 复核

### 18.1 需求背景与影响范围

- 需求是让 GMPanel 的 RemoteConfig 区域可以查看 `iwannargame_config`。
- 复核发现该能力已由提交 `99503b862` 落入
  `Assets/Game/UI/GM/Scripts/GMRemoteConfigDisplay.cs`：`ConfigKeys` 已依次注册
  `level_config`、`drawgame_config`、`iwannargame_config`。
- `Assets/Game/UI/GM/Resources/GMPanel.prefab` 中 `configText`、`nextBtn`、`lastBtn`
  三个序列化引用均非空，现有按钮可复用同一索引切换逻辑，不需要增加 prefab、按钮或公开 API。
- 本轮没有重复修改运行时代码、RemoteConfig 拉取列表、缓存结构、业务 getter、存档、资源、事件、
  analytics 或平台分支；只补充当前模块说明与验证证据。

### 18.2 当前行为与数据流

- GMPanel 打开 RemoteConfig 区域时默认显示 `level_config`。
- 点击 next 按钮时按
  `level_config -> drawgame_config -> iwannargame_config -> level_config` 循环；last 按钮按反方向循环。
- 每次切换只读取 `DB.GameData.RemoteConfigCache` 中当前 key 的 raw JSON，解析成功时格式化显示；
  当前 key 缓存缺失或为空时显示独立缺失提示，不触发远端拉取，也不回退展示另一个 key。

```text
nextBtn / lastBtn
    -> GMRemoteConfigDisplay.ChangeConfig()
    -> 三个 ConfigKey 循环更新索引
    -> DB.GameData.RemoteConfigCache 只读查询当前 key
    -> JSON 格式化
    -> configText.text
```

`RemoteConfigService.RemoteConfigKeys` 已包含 `iwannergame_config`，因此启动拉取、缓存、Guru 默认值
回退仍走现有三 key 链路；GMPanel 只负责查看业务生效缓存，不改变配置来源或业务取值。

### 18.3 验证、剩余风险与回滚

- CodeGraph 与磁盘源码静态确认 `ConfigKeys` 包含三个常量，next/last 统一按数组长度循环。
- 静态确认 prefab 的 `configText`、`nextBtn`、`lastBtn` 引用非空，两个按钮均为现有原生
  `UnityEngine.UI.Button`。
- 执行 `dotnet build .\Assembly-CSharp.csproj --no-dependencies --no-restore --nologo -v:minimal
  /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：构建成功，
  0 个错误、106 个项目既有警告。
- `uncertain`：尚未在 Unity PlayMode 实际完成三页正向/反向循环、缺失缓存提示、长 JSON 滚动验证，
  也未执行 Android/iOS 横屏调试包触摸 smoke。
- 本轮若需回滚只需删除本节文档；若产品明确要撤销既有查看能力，可从 `ConfigKeys` 移除
  `IWannerGameConfigKey`，不会删除或迁移已经存在的 RemoteConfig 缓存。

---

## 19. 2026-09-03 Daily Gift 弹窗调试入口

### 19.1 需求背景与原行为

- `GMPanel.prefab` 已新增 `dailyChestGift_btn` 与 `dailyCoinGift_btn` 两个 `CustomButton`；原补接方案曾在
  `GMPanel.OnCreate()` 中按节点名扫描按钮，偏离该脚本其它按钮统一使用 Generated 序列化字段的方式。
- `ChestGift` 已由 `GiftManager/ChestGiftController` 管理五档 Daily 轮换；CoinGift 接入前仍由
  `CoinGiftPopup.Create()` 自行打开，未参加 Gift 类型路由和跨类型窗口互斥。

### 19.2 新行为与调用边界

- `GMPanel.Generated.cs` 现包含 `DailyChestGiftBtn` 与 `DailyCoinGiftBtn`，Prefab 上的 `GMPanel` 组件已绑定
  对应 `CustomButton`。`OnCreate()` 直接复用现有 `BindButton()`，`OnClose()` 成对调用 `UnbindButton()`；
  不再存在运行时节点名常量、层级遍历或兜底解析。
- 点击 Chest 按钮时读取 `GiftManager.Instance.ChestGift.CurrentIndex`，再通过
  `GiftManager.Open(ChestGift, index)` 打开当前 Daily 档位。指定档位调用只用于 GM 预览，不推进 Daily
  次数，也不修改首次主页、购买日隐藏或入口资格；五档全部购买完成时仍由控制器拒绝打开。
- 点击 Coin 按钮统一调用 `GiftManager.Open(GiftPopupType.CoinGift)`；CoinGift 已注册独立
  `CoinGiftController`，只复用 GiftManager 的类型路由和窗口互斥，不实现 `IScheduleRefreshable`，因此不会被
  Daily Schedule 推进或伪造为每日轮换礼包。

```text
dailyChestGift_btn
    -> GMPanel.ShowDailyChestGift()
    -> ChestGiftController.CurrentIndex
    -> GiftManager.Open(ChestGift, index)

dailyCoinGift_btn
    -> GMPanel.ShowDailyCoinGift()
    -> GiftManager.Open(CoinGift)
    -> CoinGiftController.Open()
    -> Resources/CoinGiftPopup
```

### 19.3 验证、风险与回滚

- 静态回读确认两个 prefab 节点、Generated 字段和 `GMPanel` 序列化引用一一对应；运行时代码未使用
  `Transform.Find` 或 `GetComponentsInChildren` 解析这两个按钮。
- 复用 Unity Bee 响应文件执行运行时 Roslyn 编译为 `0 error`；输出仅包含工程既有 warning。
- `uncertain`：Unity PlayMode 的两个 GM 点击、Gift 跨类型互斥、原生广告/IAP 返回后的弹层顺序以及
  Android/iOS 横屏触控尚未验证。
- 回滚时删除 `GMPanel.cs` 中两个按钮的绑定、解绑和点击方法，并移除 Prefab/Generated 中对应序列化字段；
  CoinGiftController 与 ChestGift/Schedule 存档可独立保留。

---

## 20. 2026-09-07 Schedule 跨天测试一键打开 ChestGift

### 20.1 需求、原行为与问题

- 用户需要点击 Schedule Daily Test 的跨天按钮后即可直接测试 ChestGiftPopup。
- 修改前按钮只把 `DB.GameSchedule["Daily"]` 回退到昨天 UTC 00:00 并执行
  `ScheduleSystem.CheckAllModules()`；它不会修改 `GiftData.LastChestGiftPurchaseLocalDay`，也不会重新执行已经结束的
  启动弹窗队列。玩家当天购买过 ChestGift 时，Daily 计数虽然推进，弹窗仍被“今日已购买”状态拦截。
- `GMPanel` 位于 `UILayer.System`，ChestGiftPopup 位于 `UILayer.Popup`；测试成功后若继续保留 GMPanel，弹窗会被更高层
  GM 界面遮挡。

### 20.2 当前一键测试行为

- 按钮文案修正为“跨天测试”。点击后以 `DailyScheduleModule` 的设备当地自然日口径取得昨天 00:00，同时写入
  `GameSchedule` 的 Daily 最后刷新时间与 `GiftData.LastChestGiftPurchaseLocalDay`，并分别保存。
- 修改 GiftData 前先通过 `ChestGiftController.DailyRefreshCount` 触发既有旧存档迁移，避免 DataVersion 仍为 0 时后续迁移
  把测试日期覆盖回旧字段。
- 随后执行原 `ScheduleSystem.CheckAllModules()`；ChestGift 日刷次数由既有 `GiftManager -> ChestGiftController` 链推进，
  未在 GM 脚本内直接改计数。
- Daily 刷新完成后通过 `TryResolveCurrentIndex()` 解析刷新后的第一个可售档位，再沿 `GiftManager.Open(ChestGift, index)`
  打开弹窗。打开成功后关闭 System 层 GMPanel，使 Popup 层 ChestGift 可见。
- 测试仍保留线上功能开关、商品/奖励/SKU/Resources 校验、永久购买次数和 GiftPopup 互斥；五档全部购买、配置无效、
  `IsOpenDailyChest=false` 或已有活动 GiftPopup 时会记录失败并保留 GMPanel。
- 调试文本补充 ChestGift 日刷次数、当前轮换档位和最近购买当地日期，并把日界线显示修正为“今日当地起点”。

```text
点击“跨天测试”
    -> Daily 与 ChestGift 最近购买日回退到昨天当地 00:00并保存
    -> ScheduleSystem.CheckAllModules()
    -> GiftManager.OnScheduleRefresh()
    -> ChestGiftController 日刷次数 +1
    -> 解析刷新后的可售档位并打开 ChestGiftPopup
    -> 打开成功后关闭 GMPanel
```

本节替代本文第 3.2、4.5、5.1 中关于跨天按钮仅验证 Daily 单点的旧描述；“刷新 daily 数据”仍只执行 Schedule 检查，
不会修改 ChestGift 购买日或打开弹窗。

### 20.3 验证、剩余风险与回滚

- `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -v:q
  /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：`0 error / 121 warning`；warning 为工程既有范围。
- `git diff --check` 通过；静态回读确认生产 `DailyScheduleModule`、`GiftManager`、`ChestGiftController`、Prefab、配置、购买与
  奖励代码均未修改。
- `uncertain`：尚未在 Unity PlayMode 实际点击验证 GMPanel 关闭、ChestGiftPopup 可见、连续跨天轮换、全档已购/已有 GiftPopup
  的失败提示，也未执行 Android/iOS 横屏调试包触摸验证。
- 回滚时恢复 `GMDailyScheduleTestController.PrepareCrossDay()` 只写 GameSchedule 并执行 Schedule Check 的旧实现，删除
  ChestGift 三项调试文本与打开方法即可；测试期间已经写入的本地存档需手动清理或由后续正常 Daily 覆盖。

---

## 21. 2026-09-07 Daily ChestGift 按钮显示当前档位

### 21.1 需求与行为

- 修改前 `dailyChestGift_btn/chestIndex_txt` 只显示“每日宝箱”，测试人员需打开弹窗或进入 Schedule 调试页才能确认当前轮换档位。
- `chestIndex_txt` 现由 `GMPanel.Generated.cs` 声明为 `ChestIndexTxt`，并在 `GMPanel.prefab` Inspector 数据中显式绑定；运行时不按节点名或层级搜索。
- `GMPanel.OnOpen()` 每次打开时读取 `GiftManager.Instance.ChestGift.CurrentIndex`，显示“每日宝箱”与 `Chest Index: 1～5`。该值与 `ShowDailyChestGift()` 打开的 Daily 轮换档位使用同一来源。
- 本次只增加调试文本展示，不修改 Daily 刷新次数、存档、商品可售性、购买次数、奖励、功能开关或弹窗互斥。

### 21.2 验证、风险与回滚

- 静态回读确认 `chestIndex_txt` 节点、`ChestIndexTxt` 绑定和目标 `TextMeshProUGUI` 组件各唯一一处，三者使用同一 fileID；`git diff --check` 通过。
- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 121 warning`，warning 为工程既有范围。
- `uncertain`：Unity PlayMode 中 1～5 档文字排版、跨天后重新打开 GMPanel 的刷新结果，以及 Android/iOS 横屏显示尚未验证。
- 回滚时删除 `ChestIndexTxt` 字段及 Prefab 绑定，并移除 `OnOpen()` 的刷新调用和 `RefreshDailyChestGiftIndexText()`；ChestGift 业务数据无需迁移。

---

## 22. 2026-09-07 Schedule 跨天测试恢复商店免费金币

### 22.1 问题与修正

- 用户实测点击 GM Schedule“跨天测试”后，ShopPanel 每日免费金币仍处于已领取状态。
- 直接证据是免费金币资格只读取 `GameSchedule.DailyFreeCoinLastClaimTime` 与 `DailyFreeCoinLastObservedTime`，不读取 Schedule 的 `Daily` 最后刷新时间；原跨天按钮只回退 Daily 与 ChestGift 日期，因此不会改变免费金币资格。
- 跨天按钮现在同时把 `DailyFreeCoinLastClaimTime` 回退到昨天当地 00:00，并复用原 `DB.SaveGameSchedule()` 一次落盘；刷新完成后主动刷新 `RedDotKey.DailyGiftCoin`。
- 已打开的 `ShopDailyFreeCoinController` 每秒检查一次领取时间是否被外部调试入口修改，变化时重算卡片资格、Free/倒计时和红点，因此无需关闭重开 ShopPanel。
- `DailyFreeCoinLastObservedTime` 不回退，继续保留生产时间防回拨边界；正常系统时间下，昨天领取日期会立即恢复可领取。

### 22.2 影响、验证与回滚

- 修改仅涉及 GM 测试数据准备和 Shop 卡片对领取时间变化的刷新，不直接发金币，不绕过奖励配置，不改变正常领取的先保存后发奖、飞金币或当地日界线规则。
- 静态回读确认 Daily 时间与免费金币领取时间复用同一次 `DB.SaveGameSchedule()`；`git diff --check` 通过，运行时程序集构建为 `0 error / 121 warning`。
- `uncertain`：Unity PlayMode 中已领取卡片即时切回 Free、红点同步、再次领取和 Android/iOS 横屏触控尚未验证。
- 回滚时移除 GM 对 `DailyFreeCoinLastClaimTime` 的回退和红点刷新，并删除 Shop 控制器的领取时间变化检测；测试期间写入的领取时间需由正常领取或清档覆盖。

---

## 23. 2026-09-09 按原按钮结构一次解锁当前一张明信片

### 23.1 需求、原行为与复用边界

- 用户要求 GMPanel 增加“解锁当前 postcard”，一次解锁一张，并明确沿用 GMPanel 原有按钮结构。
- 原入口 `unlockPostcard_btn -> CompleteAllPostcards()` 一次完成所有普通明信片，无法逐张测试。本次复用其普通配置过滤、完成存档语义、红点和提示入口，增加独立单张按钮。
- 修改文件为 `Assets/Game/UI/GM/Scripts/GMPanel.cs`、`GMPanel.Generated.cs`、`Assets/Game/UI/GM/Resources/GMPanel.prefab` 和 `Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`。PostcardPopup 只增加 GM 专用刷新入口；不修改 PostcardManager、共享 UI、配置、奖励或全局事件。
- Prefab 从原 `unlockPostcard_btn` 复制完整按钮与 TMP 子节点，新增 `unlockCurrentPostcard_btn`，文案“解锁当前明信片”，紧邻原按钮放入同一 Content。保留原 Image、CustomButton、缩放动画、字体、字号和网格布局；原 Content 使用单行 GridLayoutGroup 和横向 ContentSizeFitter，新增内容沿原滚动区域排列。
- 按 BaseUIViewEditor 的命名规则同步 `GMPanel.Generated.cs` 中的 `[SerializeField] UnlockCurrentPostcardBtn` 和 `UnlockCurrentPostcardStatusTxt`，Prefab 显式绑定 CustomButton 与原 TMP 子节点。单张按钮在 `OnOpen()` 先解绑再绑定，在 `OnClose()` 解绑，支持 UIManager 缓存实例重开；没有增加运行时节点查找或层级扫描。原 TMP 文案追加本次结果，字号以 24 为上限、16 为下限自动适配原按钮尺寸。

### 23.2 当前页选择与数据流程

1. 点击后仅在 `Application.isPlaying` 时执行。通过已有 `UIManager.GetUI<PostcardPopup>()` 取得弹窗；已打开弹窗正在动画、领奖或引导时提示等待，不写进度。随后读取 `DB.Postcard` 和 `TbPostcardConfig.DataList`；数据或配置不可用则提示失败并返回。
2. 使用既有 `IsCompletablePostcardConfig()` 排除空 ID 和解锁尾页。优先完成 `CurrentId` 对应的未完成普通明信片；当前页已完成或无效时，按 `Sort`、ID 序数比较选择最前一张未完成页。没有候选时只提示，不保存。
3. 复制完成列表并只增加目标 ID；清空 `CurrentCollectedPuzzledId`，把 `CurrentId` 设为该张，设置 `HasGuide=true`、`IsGuiding=false`。只调用一次 `DB.SavePostcard()`，不扣星、不发页面奖励、不改 `LastRecordProgress` 或星星收集记录。
4. 保存返回 false 时恢复上述所有字段和原列表引用；成功后刷新 `RedDotKey.Postcard`，通过 `PostcardPopup.RefreshAfterGmUnlock()` 刷新已打开页面，并在按钮原 TMP 和 SystemTips 中显示本次结果。连续点击会继续选择未完成页，每次最多新增一张。

`PostcardPopup.TryResolveInitialIndex()` 会在重新打开时从已完成当前页转到排序最前的未完成页；若所有页已完成，则保留当前页。已打开的 Popup 在单张 GM 保存成功后通过 `ShowConfigPage(index, false)` 重建本次完成页，复用完成态、宝箱和翻页按钮刷新，不再次保存 CurrentId、不进入正常领奖流程。资源加载失败时保留成功存档，并提示重新打开明信片。

### 23.3 验证、风险与回滚

- 使用 Unity 6000.0.59f2 自带 Roslyn 和 `Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp.rsp` 编译完整运行时源码，输出到 `Temp/GMPostcardCheck.dll`：退出码 0、0 error；编译日志为 `Temp/GMPostcardCheck.log`。
- 从最终源码提取实际点击方法和既有过滤方法，使用隔离的配置/存档替身执行 10 项检查：当前未完成页优先、连续点击逐张推进并在全部完成时停止、无效当前页回退、同排序 ID 比较、空列表、保存失败完整回滚、空列表回滚、空配置、数据/配置缺失、非运行态不写入。全部通过，见 `Temp/GMPostcardBehaviorCheck.log`；未操作真实玩家存档。
- Prefab 静态校验通过：新增 10 个对象/组件块，fileID 唯一，本地引用均可解析（包含嵌套 Prefab 的 stripped 引用），新按钮与原按钮相邻、CustomButton 绑定和中文文案正确；仅修改原有 Content 子节点列表及 GMPanel 序列化绑定两个块。定向 `git diff --check` 通过。
- 后续排查的本地 Editor.log 已出现 `CustomButton.OnPointerClick -> GMPanel.CompleteCurrentPostcard` 成功日志，包含 30002、30007 等 ID，证实当时点击和保存分支已执行；不能据此认定用户的“无反应”一定来自事件丢失。用户随后确认“有效的，没事”，因此停止追加逻辑调整。
- 补充页面刷新、按钮结果文字和重开绑定后的完整运行时 Roslyn 编译为退出码 0、0 error，日志 `Temp/GMPostcardRefreshCheck.log`。原 10 项替身检查属于初版数据逻辑证据，未覆盖后续 Popup 刷新和重开绑定。
- `uncertain`：用户确认未逐项区分刷新、重开、错误提示等场景；真实存档重启恢复、动画/引导中拒绝写入、资源加载失败、横向滚动与 Android/iOS 横屏触控仍未逐项实测。CodeGraph 工具不可用，本次依据本地源码、Prefab、Editor.log 和现有文档核对。
- 回滚时删除新按钮及其 Generated/Prefab 引用、绑定/解绑、`CompleteCurrentPostcard()` 和结果提示方法，并移除 PostcardPopup 的两个 GM 专用成员，原“解锁所有明信片”入口可独立保留。已经通过 GM 写入的完成状态需恢复测试前存档或清理测试存档，代码回滚不会自动撤销。

---

## 24. 2026-09-10 Schedule 跨天测试保留 GMPanel

### 24.1 需求、原行为与修改范围

- 用户要求点击 GMPanel 的“跨天测试”并成功弹出 ChestGiftPopup 时，不要关闭 GMPanel。
- 原 `GMDailyScheduleTestController.OpenChestGiftForCrossDayTest()` 在 `GiftManager.Open(ChestGift, giftIndex)` 成功后显式调用 `UIManager.CloseUI<GMPanel>()`，这是 GMPanel 被关闭的直接原因；ChestGift 正常打开链没有关闭 System 层 GMPanel。
- 本次只移除该测试方法中的显式关闭调用及随之失效的 `Bear.UI` using；不修改 GMPanel Prefab、ChestGiftPopup、GiftManager、Daily 数据准备、存档、商品、购买、奖励或生产入口。

### 24.2 修改后 UI 与测试流程

- 跨天测试继续回退 Daily、ChestGift 最近购买日和免费金币领取日，保存并执行 `ScheduleSystem.CheckAllModules()`，随后解析刷新后的可售档位并打开 ChestGiftPopup。
- ChestGiftPopup 打开成功后只记录测试完成日志，GMPanel 保持 `IsOpen=true`；关闭 ChestGiftPopup 后可直接继续使用原 GMPanel 和 Schedule 测试页，不需要重新创建或重新定位调试页。
- 打开失败分支仍保留 GMPanel 并刷新失败信息，与原行为一致。不同 UILayer 的显示先后与输入遮罩继续由现有 Bear.UI 管理，本次不改变共享层级规则。

### 24.3 验证、剩余风险与回滚

- 静态检查确认 GM Daily 脚本已不存在 `CloseUI<GMPanel>`，ChestGift 打开、成功日志与失败刷新调用保持不变。
- 使用现有 Unity Bee 响应文件执行 Unity Roslyn 运行时程序集编译，结果为退出码 0、0 error；目标脚本和文档差异检查通过。
- `uncertain`：尚未在 Unity PlayMode 和 Android/iOS 横屏实测 GMPanel System 层与 ChestGift Popup 层同时存在时的遮挡、输入、关闭礼包后 Schedule 页状态，以及连续跨天测试。
- 回滚时在 ChestGiftPopup 打开成功日志后恢复 `UIManager.Instance?.CloseUI<GMPanel>()` 和 `using Bear.UI;`；无需迁移或清理测试存档。

本节替代第 20.2、20.3 节中“ChestGiftPopup 打开成功后关闭 GMPanel”及其对应验证项；其余跨天数据与礼包打开流程保持有效。

---

## 25. 2026-09-16 手动拉取 Firebase Remote Config

### 25.1 需求、基线与影响范围

- GMPanel 原有“清空 remote”按钮只调用 `RemoteConfigService.ClearRemoteCache()`，没有在运行中手动触发 Firebase 拉取的入口。
- 本次在同一横向滚动 Content 中新增 `fetchRemoteConfig_btn`，文案为“拉取 remote”，同步 `GMPanel.Generated.cs` 的 `FetchRemoteConfigBtn` 显式绑定。
- 影响文件为 `GMPanel.cs`、`GMPanel.Generated.cs`、`GMPanel.prefab` 与游戏侧 `RemoteConfigService.cs`；不修改 GuruSDK、Remote Config key、存档结构、配置表、正常启动时序、analytics 或平台分支。

### 25.2 点击行为与边界

```text
点击“拉取 remote”
    -> GMPanel.FetchRemoteConfigAsync()
    -> RemoteConfigService.ForceUpdateRemoteConfigsAsync()
    -> 等待当前 SDK Pending 结束 / ForceFetch 一次 / 60 秒预算
    -> Firebase 值或 cache/spec fallback
    -> ConfigManager.RemoteConfig.TrySetRawBatch()
```

- 同一 GMPanel 实例已有拉取未结束时，重复点击只提示“正在拉取中”，不会再发起请求。
- 流程结束提示明确要求查看 `RemoteConfigService` 日志；“流程结束”不等同于 Firebase 成功，可能已经使用 cache/spec fallback。
- 按钮在 `OnCreate()` 绑定、`OnClose()` 解绑；关闭 GMPanel 不取消 SDK 请求，也不改变已有长期配置订阅。

### 25.3 验证、风险与回滚

- `Assembly-CSharp.csproj` 最终定向构建通过：0 error / 122 warning；warning 为工程现有范围。
- Unity 6000.0.59f2 的 `PrefabImporter` 已成功导入 `GMPanel.prefab`；静态检查确认新增 CustomButton、TMP 子节点、Content 子节点与 `FetchRemoteConfigBtn` 序列化引用一致。
- `uncertain`：尚未在 PlayMode 或 Android/iOS 真机点击，未实测弱网、断网、Firebase throttled、连续点击提示与横向滚动位置。
- 回滚时移除新增按钮对象、Generated/Prefab 引用、绑定/解绑和 `FetchRemoteConfigAsync()`；不需要迁移存档或清理 Remote Config 缓存。

### 25.4 2026-09-17 拉取次数收敛

- 启动流程若已收到本次运行的新鲜 SDK Success，会直接采用并跳过额外 ForceFetch；否则只执行一次 ForceFetch，不再重试第二次。
- GMPanel 明确使用手动强制入口：若 SDK 当前仍为 Pending，先等待其终态；随后无论启动时是否已有新鲜 Success，都会再 ForceFetch 一次。按钮防重复点击逻辑保持不变。
- 运行时程序集构建通过：0 error / 122 warning。真机弱网、永久 Pending、Firebase throttled 和手动拉取仍为 `uncertain`。

---

## 26. 2026-09-19 GuruSDK DebugMode 双向联动

### 26.1 需求、基线与修改范围

- 中台要求项目自身调试界面开关与 `GuruSettings.Instance.DebugMode` 同步，并监听该值供 QA 自动化控制项目调试界面。
- 原 `GMPanel.SwitchBtn` 只调用 `SetView1Visible(...)` 切换 GM 内容；项目代码未在该入口写入中台 DebugMode，也未监听中台值。
- 本次只修改 `Assets/Game/UI/GM/Scripts/GMPanel.cs`；不修改设置页、密码弹窗、StartPanel、GMPanel Prefab、Packages/GuruSDK、配置、存档、资源、事件、analytics 或平台启动参数。

### 26.2 修改后流程

- `GMPanel.OnOpen()` 订阅 `GuruSettings.Instance.DebugMode.Observe()`，订阅时立即按当前值刷新 GM 内容，后续中台值变化继续在主线程调用 `SetView1Visible(...)`。
- 点击 `SwitchBtn` 时先保持原有本地显隐切换，再把同一个 bool 写入 `GuruSettings.Instance.DebugMode.Set(...)`；因此 GM 内容打开写入 `true`，关闭写入 `false`。
- `GMPanel.OnClose()` 释放本次订阅，避免已关闭实例继续接收状态；再次打开时重新创建订阅。
- 当前限定范围不改项目调试入口的创建条件。只有 `GMPanel` 已创建并打开时，中台 DebugMode 才能反向驱动其内容显隐。

### 26.3 验证、风险与回滚

- 按用户要求未新增或执行编译、PlayMode、自动化或真机测试；仅回读目标代码并检查定向差异格式。
- `uncertain`：尚未实测 `Set(true/false)` 的持久化、Android `guru_debug` 启动参数、GMPanel 重开订阅、GuruSDK 26.4.4 的 QA 标志，以及 Android/iOS 横屏表现。当前 SDK 26.4.4 按中台文档不提供 GuruAssistant 浮动入口。
- 回滚时删除 `GMPanel` 的 GuruSettings/R3 引用、订阅字段与 OnOpen/OnClose 订阅生命周期，并把 `OnSwitchBtnClick()` 恢复为只调用 `SetView1Visible(...)`；无需迁移存档或修改 Prefab。

## 2026-10-09：1.16 OneLink 完整同步

- 本轮替代下节“仅编辑器或 DEBUG_MODE”的旧约定：OneLink 按钮在现有 GM 隐藏入口／密码之后可用，不新增公开入口；OnGUI 最外层加入 Application.isPlaying 门禁，避免编辑态访问运行数据。
- Popup 同步 sub3 用户限制、getCoin/getSkin 和只读真实系统 URL／主动请求按钮；所有 13 个引用由 Prefab 显式绑定，不用名称／层级兜底。接受后关闭 Popup 和键盘，回到正式首页门禁；会真实修改当前玩家奖励／进关历史。
- 原 Popup meta GUID、GMPanel 其它功能和资源不改。静态引用、真实引用 C# Editor／Android 编译通过；横屏布局、密码访问、键盘／剪贴板和 SDK 请求仍为 uncertain。
- 完整链路、验证证据、剩余风险与只撤销本轮提交的回滚路径见 Assets/Game/Scripts/SDK/Services/Core/OneLink功能与测试记录.md 的 1.16 当前有效实现章节。

## 2026-09-22：OneLink 参数与 URL 测试

- GMPanel 现有滚动按钮区新增 **OneLink 测试**；`GMPanel.cs` 持有 Prefab 显式绑定的 `oneLinkTestButton`，只在编辑器或 DEBUG_MODE 开放。
- 点击创建独立 `GMOneLinkTestPopup`（Popup 层）。三个 TMP 输入框分别填写 sub1、sub2、URL；两个测试按钮调用 `OneLinkManager.TestDeepLink` / `TestDeepLinkUrl`，另有粘贴 URL 和关闭按钮。字段全部显式绑定，不修改 GMPanel.Generated.cs，不在运行时搜索子节点。
- 有效请求排队后关闭当前 Popup 和输入键盘，再由正式首页门禁进入关卡；拒绝则留窗提示。空 sub2／不存在的 Path 按正式规则回退。OneLink 路径查找兼容大小写，实际加载配置原始 Path。
- Android 当前定义包含 DEBUG_MODE，重新构建该宏的包后可做游戏内测试；外部 App Links／SDK 唤起不由此工具证明。测试会使用并修改当前设备正常存档。
- 源码定向编译通过，Editor/DEBUG_MODE 模拟测试各 68 项通过，Release 宏测试 50 项通过；Prefab 引用静态检查通过。Android 真机布局、键盘和粘贴仍为 uncertain。
- 详细影响、验证边界、Android Manifest 缺口和回滚见 `Assets/Game/Scripts/SDK/Services/Core/OneLink功能与测试记录.md` 的 GM 接入章节。

### 同日补齐短链接在线解析

- 初版 GM 只解析 URL query，用户短链 `/IjED/gacjwdmm` 在 16:31 两次被 `missing_deep_link_parameters` 拒绝。本次 Android 调试包支持粘贴该短链，由已有 AppsFlyer SDK 解析后台参数。
- URL API 增加可选结果回调；bool true 可以表示解析已开始，Popup 不据此立即关闭。等待时显示状态并禁用提交/输入，最长 10 秒；成功排队才关闭，失败/超时恢复控件并展示原因。关闭取消当前测试，带请求标识的晚到回调丢弃；重开用版本号隔离旧回调。
- 复用全部现有 Prefab 引用、按钮和状态文本，没有新增资源或共享 UI 修改。编辑器/iOS 暂不支持在线短码，保留参数与长链接测试并明确提示平台限制。
- Editor/Android 宏 C# 编译通过；游戏侧模拟 Editor 74 项、Android 106 项、无调试宏 53 项通过。用户链接 HTTP 响应确认后台参数和测试标识能返回；Android APK/IL2CPP、实际 SDK 网络与横屏 UI 仍为 uncertain。
- 回滚仅恢复 Manager/Popup 同步 URL 测试和 Router 原比较方式；已解锁存档不自动恢复。详见 OneLink 模块文档“补齐 Android GM 短链解析”。

## 2026-09-29：在 OneLink 测试弹窗显示系统 URL 并主动请求

- 需求与基线：原弹窗仅有参数和输入 URL 测试；首次将系统 URL 控件放在 GMPanel 外侧，用户截图反馈难以找到，随后明确要求收拢到同一弹窗。当前外侧重复控件已移除，GMPanel 仅保留打开弹窗职责。
- 使用：打开 **OneLink 测试** 弹窗，在原“解析 URL 并测试／粘贴 URL”下方查看 **当前 _systemUrl（只读）**，下面就是 **主动请求当前 _systemUrl** 按钮。不要求外侧 GM 为 True；空值显示尚未收到并禁用按钮。Editor 不执行原生拉取，真机需包含改动和 DEBUG_MODE 的调试包。
- 链路：`GMOneLinkTestPopup → OneLinkManager.SystemUrl / RequestSystemUrl → StartUrlResolution → AppsFlyer`。Popup 的只读 TMP_InputField 和 CustomButton 在现有 Prefab 显式绑定；背景扩大至 960×940 并重排控件，不改生成绑定，不使用运行时层级查找。真实地址与上方用户输入 URL 分离，运行态刷新。
- 按钮不模拟 ERROR、不重启 SDK、不覆盖地址。未就绪、无网、关内或正在处理时留窗提示；受理后关闭本弹窗以放行首页/重试门禁，不取消正式请求，也不把关闭当作跳转成功。原输入 URL 测试期间系统按钮禁用；关闭重开按原生命周期解绑。
- 真实 ERROR 的自动恢复属于另一条正式逻辑：有可请求的短链且网络/首页条件允许时补发一次，不因重复 ERROR 循环；长链接本地参数回退、无 URL 安装恢复保留。手动成功仍受正式进关历史及奖励防重约束，会使用当前玩家存档。
- 验证：迁入弹窗后五宏模拟同轮 641 次断言执行通过（455 奖励、136 系统 URL、50 弹窗，含平台重复）；Editor/Android 定向编译 0 错误，113/116 警告。Prefab 152 对象引用、10 字段绑定、主要矩形边界通过静态检查；实际 Unity 渲染、横屏触摸/键盘及真实 SDK 仍为 `uncertain`。证据在 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-popup-20260929`。
- 日志验收：`manual_url_started` 只证明调用开始；ERROR 后检查 `retry_started, attempt=1`，不得有 attempt=2。进关完成看 `entry_completed`，奖励看 `reward_completed`。空 `_systemUrl` 不具备测试原 URL 重交的前提。
- 回滚：仅回退 Popup 和 Prefab 的本次新增引用、节点与布局；如需恢复外侧旧位置，连同旧 GMPanel 控件一起还原，不要撤销 Manager 单次恢复、存档或无关工作区文件。详细基线、验证和回滚分层见 `Assets/Game/Scripts/SDK/Services/Core/OneLink功能与测试记录.md` 同日章节。

## 2026-09-30：OneLink 测试入口恢复为 TestFlight 可用

- 用户明确纠正：此前由 Agent 自行加入的 `UNITY_EDITOR || DEBUG_MODE` 门禁不是需求，且在 2026-09-29 恢复旧实现时覆盖了用户已经去掉的限制。Git 证据显示初始门禁来自 `1d4163fb94705e668cb99fda2b33f5199bda2707`；本次按用户实际 TestFlight 验收方式撤销，不再沿用旧文档中的“仅调试包”结论。
- 无 `DEBUG_MODE` 时，设置页 `gmBtn` 连点五次仍可打开密码弹窗，密码通过后创建 GMPanel；OneLink 按钮现在始终绑定，Popup 与 Manager GM API 也不再被宏裁掉。`StartPanel` 自动常驻 GM 仍仅在 `DEBUG_MODE`，因此普通正式启动不会自动出现 GM。
- 操作路径：设置页隐藏五击 → 密码弹窗 → GMPanel → **OneLink 测试** → 查看“当前 `_systemUrl`”或点击“主动请求当前 `_systemUrl`”。原参数/URL 测试仍可能改动当前存档、进关或发奖，真实请求仍受 SDK、网络、首页、玩法、并发与正式去重门禁。
- 验证：Editor、Android/iOS 各有/无 `DEBUG_MODE` 五组当前源码断言通过；无宏 Android/iOS 分别覆盖 Popup 创建和系统 URL 平台桥接。Editor、Android 无宏定向编译 `0 error / 2 warning`。未构建 IPA、未上传新 TestFlight、未做真机密码入口、横屏 UI 或 AppsFlyer 回调，以上仍为 `uncertain`。
- 回滚仅恢复 `GMPanel.cs`、`GMOneLinkTestPopup.cs`、`OneLinkManager.Debug.cs` 的旧宏边界；不改 Prefab、密码入口、自动 ERROR 单次恢复、奖励存档或其它 GM 功能。权威细节见 OneLink 模块文档同日章节。

## 2026-09-30：OneLink 金币与皮肤领奖参数测试

- 同一 OneLink 测试弹窗新增“金币测试”“皮肤测试”，原按钮改为“进关测试”。领奖使用 sub1=奖励唯一标识、sub2=金币正整数数量/精确皮肤 Id、sub3=可选目标 Guru Uid；进关仍使用玩法/资源 Path，忽略 sub3。原 URL、粘贴、系统 URL 功能保留。
- 两按钮调用 `OneLinkManager.TestDeepLinkReward`，只排队，关窗后由首页正式发奖；不会绕过账号限制、已领取记录、皮肤存在性或保存流程。不自动生成新标识，方便检查重复领取。同一 sub1 在金币和皮肤之间共用防重记录。
- 三个新增引用在 `GMOneLinkTestPopup.prefab` 显式绑定；Popup 为手写绑定，不改 GMPanel.Generated 或 GameData 生成代码。URL 等待期间禁用新按钮与 sub3；打开/关闭成对绑定并关闭输入键盘。
- 新增五宏模拟 164 次断言及既有回归通过，Editor/Android Unity 引用定向编译无错误；191 个 Prefab 对象与 13 个绑定、16 个控件边界通过静态检查。未验证 Unity 渲染、真机触摸/键盘或新 TestFlight，仍为 uncertain。
- 证据、详细前置核对、行为链与回滚见 `Assets/Game/Scripts/SDK/Services/Core/OneLink功能与测试记录.md` 同日“GM 增加金币与皮肤领奖参数测试”。代码和 Prefab 需一起更新；测试实际改存档，代码回滚不撤销已发奖励。

## 2026-10-09：GM OneLink 进关不受每玩法一次历史限制

- 用户明确要求绕过进关历史：GM 参数/长 URL、Android 输入短 URL、Android/iOS 主动请求系统 URL 的已关联结果（含本轮恢复）允许重复进入已记录玩法，且不追加/删除 `OneinlinkEnterType`。本节取代旧章节关于 GM 成功消耗永久进关机会的当前规则；正式 UDL/FB/GCD/系统唤起保留原限制。
- 使用原 OneLink 测试按钮及 Popup，无新增开关或 Prefab 改动。首页 sub1=`iwanner`、sub2=`IGroup_3` 可测试该组，回首页再测 `ILevel_012`/其它组；加载成功看 `entry_completed, source=gm, historyRecorded=False`。仍不能关内插队或绕过首页弹窗、加载并发、网络/SDK/配置条件；iOS 普通输入短链仍不支持。
- GM 请求携带仅代码可赋值的历史例外，Router 预留后固定来源；成功仍保留 DeepLink 胜利/Replay 来源。真实新系统 URL 重置 GM 轮次，未关联真实 SDK 回调和链接参数不能获取该权限；正式永久历史保持原样，不需要清存档。GM 解锁/保存进度和奖励发放仍使用当前玩家存档，不是沙盒。
- 五宏 Manager/Request 1934 次断言与实际 Router/本地配置 3445 次断言通过；Editor/Android 定向编译均 0 error。iOS 被工程已有 `Guru.UniKit.Apple` 引用缺失阻断；PlayMode、实际 SDK 标识、真机横屏和真实存档仍为 `uncertain`。
- 完整基线、数据流、验证命令、证据边界和回滚见 `Assets/Game/Scripts/SDK/Services/Core/OneLink功能与测试记录.md` 同日 GM 历史例外章节。回滚只移除这次请求/预留/确认中的 GM 例外，不清理永久历史，不撤销其它 OneLink 或玩家数据。
