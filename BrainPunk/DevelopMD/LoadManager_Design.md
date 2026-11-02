# LoadManager 设计说明

## 1. 文档目的

本文档描述 `LoadManager` 在 BrainPunk 项目中的当前职责、启动链路、阶段顺序、依赖前置条件、进度表现方式与已知风险。

本文档只记录当前仓库代码已经体现出来的行为，不把理想设计写成现状事实。

当前相关文件：

- `Assets/Game/UI/Loading/Scripts/LoadManager.cs`
- `Assets/Game/Scripts/GameManager.cs`
- `Assets/Game/Scripts/Booster.cs`
- `Assets/Game/Scripts/HotReload/HotReloadCtrl.cs`
- `Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs`
- `Assets/Game/Scripts/Config/ConfigManager.cs`
- `Assets/Game/Scripts/Play/PlayCtrl.cs`
- `Assets/Game/Scripts/Play/LevelCtrl.cs`

## 2. 模块定位

`LoadManager` 是当前 Loading 场景里的启动编排器。

当前职责主要有四类：

### 2.1 串行组织启动阶段

当前在 `Awake()` 里按固定顺序入队：

1. `EnterGame()`
2. `HotReload()`
3. `LoadConfigAsync()`
4. `OnPostProcess()`

这些阶段由 `ProcessQueue()` 串行执行，队列清空后再切场景到 `Main`。

### 2.2 驱动 Loading 界面进度

`LoadManager` 不直接维护统一总进度模型。

当前做法是：

- 每个阶段各自 `yield return float`
- `ProcessQueue()` 读取该值并更新 `ProcessFill.fillAmount`
- 阶段切换时把进度条重新置为 `0`

因此当前进度条语义更接近“阶段内进度”，不是完整启动流程的全局累计进度。

### 2.3 作为多个子系统的启动汇合点

当前启动链会在 `LoadManager` 内依次触发：

- `Booster` / `GameSDKService`
- `DBManager`
- `HotReloadCtrl`
- `RemoteConfigService`
- `ConfigManager`
- `UIManager`
- `AudioManager`
- `PurchaseManager`
- `PlayCtrl`
- `GameAchievementUtils`

### 2.4 处理部分启动后补充逻辑

当前 `OnPostProcess()` 还负责：

- 尝试展示版本公告
- 兼容旧版本数据转换提示
- 首次进入奖励初始化
- 结束“新版本流程”标记

## 3. 启动链路

### 3.1 入口与队列启动

`Awake()` 当前只做入队，不直接同步执行阶段逻辑。

当 `EnqueueFunction(...)` 发现 `currentCoroutine == null` 时，会启动 `ProcessQueue()`。

这意味着当前启动流程是：

1. `Awake()` 连续把 4 个阶段压入 `functionQueue`
2. 第一次入队时启动 `ProcessQueue()`
3. `ProcessQueue()` 依次 `Dequeue()` 并执行每个阶段
4. 队列清空后调用 `LoadSceneAsync("Main")`

### 3.2 阶段顺序

当前固定顺序如下：

1. `EnterGame()` 负责基础运行时初始化
2. `HotReload()` 负责热更新阶段等待
3. `LoadConfigAsync()` 负责 RemoteConfig 与配置表加载
4. `OnPostProcess()` 负责进入正式游戏前的收尾初始化
5. `LoadSceneAsync("Main")` 负责切到主场景

当前代码中没有动态跳过某个阶段的通用调度机制。

## 4. 各阶段现状

### 4.1 EnterGame

`EnterGame()` 当前顺序是：

1. `booster.Init()`
2. `GameManager.Instance.Init()`
3. `DBManager.Instance.Initialize()`
4. `InitLanguage()`
5. `DB.GameData.PlayCount++`
6. `DB.GameData.Save()`
7. `GameManager.Instance.RefreshIsNewVersion()`
8. 注册并检查 `DailyScheduleModule`

当前含义是：

- 先把基础运行时、存档和语言准备好
- 再记录本次启动次数
- 再计算本次是否按“新版本启动”处理

### 4.2 HotReload

`HotReload()` 当前顺序是：

1. 从 `GameSettingData.Setting` 读取默认包名与包配置
2. 初始化 HotReload 事件订阅
3. 调用 `HotReloadCtrl.Instance.Init(...)`
4. 等待 `HotReloadCtrl.Instance.IsFinish && booster.IsReady`
5. 如果 `HotReloadCtrl.Instance.IsFailed`，记录错误并继续
6. 无论完成或失败，重置事件订阅并结束阶段

当前等待条件不是只看热更新完成，还会同时等待：

- `HotReloadCtrl.Instance.IsFinish`
- `booster.IsReady`

其中 `booster.IsReady` 当前来自 `GameSDKService.Instance.InitService(...)` 完成回调。

### 4.3 LoadConfigAsync

`LoadConfigAsync()` 当前顺序是：

1. `RemoteConfigService.TryToClearRemoteCache()`
2. `RemoteConfigService.UpdateRemoteConfigsForLoadManager()`
3. 检查 `ConfigManager.Instance`
4. 如未初始化，调用 `ConfigManager.Instance.InitializeAsync(...)`
5. 轮询 `ConfigManager.Instance.LoadProgress`
6. 根据 `loadSuccess` 记录成功或失败日志

当前 `RemoteConfig` 阶段结束后，配置表加载才开始。

### 4.4 OnPostProcess

`OnPostProcess()` 当前顺序是：

1. `InitUIManager()`
2. `AudioManager.Init()`
3. `GameManager.Instance.Purchase.InitConfig()`
4. `GameSDKService.Instance.UpdateBPlay()`
5. `GameSDKService.Instance.UpdateBLevel()`
6. `GameManager.Instance.ReadyToPlay()`
7. `GameManager.Instance.Purchase.SyncRestore()`
8. `GameAchievementUtils.Init()`（仅 `UNITY_ANDROID || UNITY_EDITOR`）
9. 在新版本标记落盘前缓存更新公告展示资格
10. 补录历史通关明信片星星
11. `InitGameReward()`
12. `GameManager.Instance.NewVersionProcessFinished()`
13. `InitReddot()`
14. 启动 `StartupPopupSequence`，依次处理更新公告、`TransformPopup` 和 ChestGift

当前含义是：

- 先把 UI、音频、内购和打点侧运行时入口准备好
- 再进入正式游戏控制器初始化
- 最后处理首次奖励与其它收尾初始化，再按关闭边界串行展示版本公告、兼容转换和 ChestGift

## 5. 关键依赖与前置条件

### 5.1 序列依赖

当前启动链有几个明确的先后依赖：

- `DBManager.Instance.Initialize()` 必须先于 `DB.GameData` / `DB.GameSetting` 的直接访问
- `GameManager.Instance.Init()` 必须先于 `GameManager.Instance.Purchase.InitConfig()` / `SyncRestore()`
- `ConfigManager.Instance.InitializeAsync(...)` 成功后，`PlayCtrl.Init()` 才有机会安全读取 `ConfigManager.Instance.Tables`
- `RemoteConfigService.TryToClearRemoteCache()` 与 `UpdateRemoteConfigsForLoadManager()` 当前依赖 `DB` 已初始化

### 5.2 Inspector 依赖

当前代码对以下字段没有完整空保护：

- `booster`
- `ProcessFill`
- `processTxt`

这意味着当前 `LoadManager` 运行依赖场景正确绑定这些引用。

### 5.3 RemoteConfig 前置条件

根据 `RemoteConfigService` 当前实现，`LoadConfigAsync()` 里的 RemoteConfig 阶段当前默认依赖：

- `DB.GameData` 已初始化
- `GuruSdk.Instance.RemoteConfigSpec` 已可用，才有机会使用 spec 默认值 fallback

在当前主链设计里，这个前提主要依赖 `HotReload()` 等待 `booster.IsReady`。

## 6. 进度与场景切换机制

### 6.1 阶段进度

`ProcessQueue()` 当前对每个阶段的处理方式是：

1. 取出一个 `IEnumerator<float>`
2. 把 `ProcessFill.fillAmount` 直接置为 `0`
3. 每帧读取阶段返回的 `float`
4. 用 `DOTween` 把 `ProcessFill.fillAmount` 过渡到该值

当前表现特征：

- 进度条在阶段切换时会重置
- 如果新值小于当前值，则直接赋值，不做 tween
- 如果新值大于当前值，则按 `Mathf.Max(0.02f, delta)` 作为 tween 时长

### 6.2 Main 场景切换

`ProcessQueue()` 在队列清空后固定调用：

- `LoadSceneAsync("Main")`

`LoadSceneCoroutine(...)` 当前做法是：

1. 调用 `SceneManager.LoadSceneAsync(sceneName)`
2. 先令 `allowSceneActivation = false`
3. 用 `asyncLoad.progress / 0.9f` 更新 `ProcessFill.fillAmount`
4. 当 `asyncLoad.progress >= 0.9f` 时允许激活场景

## 7. 与版本流程的关系

### 7.1 新版本标记生成

当前 `EnterGame()` 会调用：

- `GameManager.Instance.RefreshIsNewVersion()`

`LoadManager` 后续并不直接比较 `GameData.CurrentAppVersion`，而是主要依赖：

- `DB.GameSetting.IsNewVersion`

### 7.2 RemoteConfig 清缓存判定

当前 `LoadConfigAsync()` 一开始调用：

- `RemoteConfigService.TryToClearRemoteCache()`

而 `RemoteConfigService.ShouldClearCache()` 当前直接返回：

- `DB.GameSetting.IsNewVersion`

因此当前版本清缓存逻辑与 `LoadManager` 里的新版本标记阶段直接耦合。

### 7.3 新版本流程结束

当前 `OnPostProcess()` 末尾调用：

- `GameManager.Instance.NewVersionProcessFinished()`

这会把：

- `DB.GameSetting.IsNewVersion`

重置为 `false`。

## 8. 当前已知风险与限制

本节只记录当前代码中已经能直接看到的结构性风险或限制，不把理想优化方向写成现状问题。

### 8.1 配置加载失败后仍继续进入正式流程

现状：

- `LoadConfigAsync()` 即使 `loadSuccess == false`，当前也只是记录错误日志，然后 `yield return 1f` 结束。
- `ProcessQueue()` 不会因为该阶段失败而中断，仍会继续执行 `OnPostProcess()`。
- `OnPostProcess()` 会调用 `GameManager.Instance.ReadyToPlay()`。
- `PlayCtrl.Init()` 会继续调用 `LevelCtrl.Init()`。
- `LevelCtrl.Init()` 当前直接读取 `ConfigManager.Instance.Tables.TbLevelData` 与 `TbLevelSort`。

风险：

- 如果配置表初始化失败导致 `ConfigManager.Instance.Tables == null`，当前启动链仍可能继续走到 `LevelCtrl.Init()`，存在后续空引用风险。

### 8.2 HotReload 超时后会跳过 `booster.IsReady` 等待

现状：

- `HotReload()` 的等待循环目标是同时等 `HotReloadCtrl.Instance.IsFinish` 和 `booster.IsReady`。
- 但当前存在总超时 `HOT_RELOAD_TIMEOUT = 30f`。
- 一旦超时，会直接 `break` 并继续后续阶段。

风险：

- 超时后 `LoadConfigAsync()` 仍会继续执行。
- 此时 `RemoteConfigService` 需要的 `GuruSdk.Instance.RemoteConfigSpec` 可能尚未准备完成。
- 结果会使 RemoteConfig 阶段更容易落到“无远端值且 spec fallback 也不可用”的路径。

### 8.3 HotReload 超时日志与实际超时时间不一致

现状：

- 代码常量是 `HOT_RELOAD_TIMEOUT = 30f`
- 但超时日志当前输出的是 `timeout (60s)`

影响：

- 排查启动问题时，日志会误导为 60 秒超时，和真实代码行为不一致。

### 8.4 Loading UI 引用缺少完整防护

现状：

- `ProcessQueue()` 里在每个阶段开始时直接执行 `ProcessFill.fillAmount = 0`
- HotReload 事件回调里直接写 `processTxt.text` 与 `processTxt.color`

风险：

- 如果 `ProcessFill` 或 `processTxt` 未在场景中正确绑定，当前会直接空引用。

### 8.5 启动阶段总体策略偏“失败后继续”

现状：

- `HotReload()` 失败后继续
- `HotReload()` 超时后继续
- `LoadConfigAsync()` 失败后继续

当前含义：

- `LoadManager` 目前更接近“尽量完成启动”的 best-effort 设计
- 不是“任一关键阶段失败就中断进入主场景”的强一致启动器

## 9. 维护约束

- 如果新增启动阶段，当前应明确它是放在 `EnterGame`、`HotReload`、`LoadConfigAsync`、`OnPostProcess` 哪一段之后，而不是只看功能归属。
- 如果新增阶段依赖配置表，必须确认它发生在 `ConfigManager.Instance.InitializeAsync(...)` 成功之后。
- 如果新增阶段依赖 Guru SDK / RemoteConfigSpec，必须确认它不会被当前 `HotReload()` 超时路径绕过前置条件。
- 如果修改 Loading UI，需同时检查 `ProcessFill` / `processTxt` 的空保护与 DOTween 生命周期。

## 10. 一句话总结

当前 `LoadManager` 的定位更准确地说是：

- 一个按固定顺序串行执行启动阶段、并在大多数失败场景下继续尝试进入主场景的 Loading 编排器

而不是：

- 一个对每个关键前置条件都做强一致校验、失败即中断的启动状态机

## 11. 2026-07-24 首次启动语言开放校验

### 11.1 需求与原行为基线

- 首次启动时，`EnterGame()` 会在 `DBManager` 初始化后调用 `InitLanguage()`，按系统 UI 语言设置 `LocalizationManager.CurrentLanguageCode` 和 `DB.GameSetting.CurrentLanguageKeyCode`。
- 此时 `TbLanguage` 尚未加载，原流程不会检查系统语言是否存在于 `TbLanguage.DataList` 且 `IsOpen == true`。
- 因此，I2 支持但设置语言表未开放的系统语言仍可能成为当前语言和存档语言，语言设置弹窗中也不会有对应的普通语言项。

### 11.2 新行为

- `LoadConfigAsync()` 在配置已经初始化、异步加载结束或配置入口不可用时，都会调用 `ValidateFirstLaunchLanguage()`。
- 校验只在 `DB.GameData.PlayCount == 1` 时执行。该值表示 `EnterGame()` 已把本次首次启动计数从 `0` 增加到 `1`，不会重新校验老玩家或玩家之后主动选择的语言。
- 当前存档语言必须在 `ConfigManager.Instance.Tables.TbLanguage.DataList` 中找到大小写无关的 `CorrespondingKey`，并且该行 `IsOpen == true`。
- 未找到开放语言、语言列表不可用或配置入口不可用时，统一把 I2 当前语言与 `DB.GameSetting.CurrentLanguageKeyCode` 设置为 `en`，并调用 `DB.GameSetting.Save()`。
- 已开放语言保持原值；iOS 首次简体中文的现有临时繁体显示分支不变，因为 DB 中的 `zh-CN` 仍可通过开放列表校验。

### 11.3 数据、配置与平台流

```text
系统 UI 语言
  -> InitLanguage() 早期设置并保存候选语言
  -> HotReload
  -> ConfigManager 加载 TbLanguage
  -> ValidateFirstLaunchLanguage()
       -> CorrespondingKey 匹配且 IsOpen=true：保持候选语言
       -> 未开放/配置不可用：I2 当前语言=en
                              DB.GameSetting.CurrentLanguageKeyCode=en
                              DB.GameSetting.Save()
```

- 本次不修改 `tblanguage.json`、I2 语言资源、存档字段结构、事件、analytics、资源路径或平台 SDK。
- 校验只发生在首次启动的 Loading 配置阶段，没有新增每帧轮询、实例化或额外资源加载。
- Android 与 iOS 共用开放语言校验；iOS 既有 `zh-CN` 特殊显示逻辑保持原样。

### 11.4 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：构建成功，`0 error / 99 warning`。
- 首次构建因 `Temp/bin/Debug` 缺少 Unity 生成的项目引用 DLL 而失败；允许构建项目引用补齐缓存后，目标程序集复编通过。
- PowerShell 静态读取 `Assets/Game/Configs/tblanguage.json`：`en` 与 `zh-CN` 命中开放列表；`ar`、`de` 和空代码均得到 `en` 回退结果。
- `git diff --check` 通过；未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 或每帧更新入口。

### 11.5 未验证风险

- 尚未在 Unity PlayMode 清空存档后验证开放系统语言、未开放系统语言、配置加载失败和重启恢复场景。
- 尚未在 Android/iOS 真机验证系统语言读取、首次 Loading 期间的短暂语言表现、最终主界面语言及横屏 UI。
- 由于候选系统语言仍在 `EnterGame()` 提前应用，未开放语言可能在 `TbLanguage` 加载完成前短暂出现在 Loading UI；进入主场景前会完成英文回退。

### 11.6 回滚

- 移除 `LoadConfigAsync()` 三处 `ValidateFirstLaunchLanguage()` 调用及该私有方法，即恢复原先只按系统语言/I2 支持情况决定首次语言的行为。
- 无需回滚配置、prefab、资源、事件或存档结构；已被保存为 `en` 的测试存档需清档或手动切换语言才能恢复原值。

## 12. 2026-07-24 多语言启动职责收敛到 LocalizatioinUtils

### 12.1 需求与原行为基线

- `LoadManager` 原本同时负责系统语言检测、I2 语言设置、首次启动存档和 `TbLanguage` 开放校验，启动编排与多语言业务规则耦合。
- 非 Editor 的非首次启动不会主动把 `DB.GameSetting.CurrentLanguageKeyCode` 同步给 I2，最终语言可能受 I2 自身 `PlayerPrefs["I2 Language"]` 恢复结果影响。
- `LocalizationPopup` 直接设置 I2 后再单独写入并保存 `DB.GameSetting`，与启动入口存在重复同步代码。
- iOS 首次系统语言为 `zh-CN` 时，原流程临时应用 `zh-TW` 且 `RememberLanguage=false`，但 DB 仍保存 `zh-CN`，两个持久化来源可能不一致。

### 12.2 修改范围与新行为

- `Assets/Game/Scripts/Common/LocalizatioinUtils.cs`
  - 新增 `InitializeStartupLanguage()`，在 DB 初始化后执行；首次启动读取一次系统语言，后续启动以 DB 存档为准同步 I2。
  - 新增 `ValidateAndApplyStartupLanguage()`，在配置加载出口执行；每次启动都检查存档语言是否在 `TbLanguage` 中开放，并检查 I2 是否支持该代码。
  - 新增 `TrySetLanguage(...)`，统一设置 I2、更新 `DB.GameSetting.CurrentLanguageKeyCode`，并只在存档值变化且允许保存时写盘。
  - iOS 继续保留“首次系统 `zh-CN` 使用 `zh-TW`”规则，但现在 DB 与 I2 都保存实际应用的 `zh-TW`，不再保留两个不同值。
- `Assets/Game/UI/Loading/Scripts/LoadManager.cs`
  - 删除本地 `InitLanguage()` 与 `ValidateFirstLaunchLanguage()` 实现，只在原启动时机调用 `LocalizatioinUtils`。
  - 不再直接引用 I2，启动队列、配置加载顺序和主场景进入顺序不变。
- `Assets/Game/UI/SettingPopup/Scripts/LocalizationPopup.cs`
  - 普通语言项改为调用 `TrySetLanguage(...)`；I2 不支持时不保存 DB，也不派发成功回调。
  - 列表生成、开放语言过滤、自定义语言项和选中表现逻辑不变。

### 12.3 数据、配置、UI 与平台流

```text
DBManager.Initialize
  -> LocalizatioinUtils.InitializeStartupLanguage
       -> 首次启动：系统语言候选
       -> 后续启动：DB 已保存语言
       -> 同步 I2
  -> PlayCount 增加
  -> ConfigManager 加载 TbLanguage
  -> LocalizatioinUtils.ValidateAndApplyStartupLanguage
       -> TbLanguage 已开放且 I2 支持：应用并保持
       -> 未开放或 I2 不支持：回退 en，并同步 I2 + DB
       -> 配置不可用：
            首次启动回退 en
            老玩家保留 DB 语言，避免临时加载失败覆盖用户选择
```

- `LocalizationPopup` 主动切换同样进入 `TrySetLanguage(...)`，因此启动与设置 UI 共用一条 I2/DB 同步链。
- I2 自身仍会在本地化组件启用时初始化；本次不修改插件源码，也不清空 I2 PlayerPrefs。正常启动时 DB 会覆盖不一致的旧 I2 语言缓存。
- 未修改 `tblanguage.json`、`I2Languages.asset`、存档字段结构、prefab、资源地址、事件类型、analytics、SDK 或 RemoteConfig。
- Android 使用系统语言候选与 DB 同步通用逻辑；iOS 额外保留首次 `zh-CN -> zh-TW` 映射，但持久化值改为一致。

### 12.4 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：构建成功，`0 error / 102 warning`；warning 为当前工程既有输出。
- `git diff --check` 对三个运行时代码文件通过，仅出现仓库既有的 LF/CRLF 转换提示。
- 静态引用检查确认 `LoadManager` 和 `LocalizationPopup` 已不再直接设置 `LocalizationManager.CurrentLanguageCode`，启动校验和普通语言选择都调用 `LocalizatioinUtils`。
- 静态对照 `tblanguage.json` 与 `I2Languages.asset`，当前 12 个开放语言代码均能在 I2 语言源中找到。
- 静态检查确认没有新增 `Update`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、全局事件、资源加载或实例化入口。

### 12.5 未验证风险

- 尚未在 Unity PlayMode 验证“DB 与 I2 历史缓存不一致”时 Loading UI 是否存在一帧旧语言显示；若 I2 的 `Localize.OnEnable` 早于 `LoadManager` 语言初始化，仍可能在最终校正前出现短暂刷新。
- 尚未验证首次开放系统语言、首次未开放系统语言、老玩家语言被配置关闭、配置入口不可用和 I2 缺少对应语言代码的运行时日志与最终 UI。
- 尚未在 iOS 真机验证首次简体系统语言最终保存并显示 `zh-TW`；该平台规则的产品意图沿用旧代码，但持久化一致性属于本次有意修正。
- 尚未进行 Android/iOS 横屏真机验证；本次没有新增每帧逻辑，性能影响预计仅为启动阶段常量级语言表查询，此结论仍待设备验证。

### 12.6 回滚

- 将 `LoadManager` 两个工具调用恢复为原 `InitLanguage()` / `ValidateFirstLaunchLanguage()`，并把相应方法移回 `LoadManager`。
- 将 `LocalizationPopup.OnClickLanguageItem(...)` 恢复为直接设置 I2、写入并保存 `DB.GameSetting`。
- 删除 `LocalizatioinUtils` 新增的三个同步方法即可回滚代码职责迁移；本次没有存档 schema、配置或资源迁移。
- 已被新逻辑从无效语言回退为 `en`，或在 iOS 首次保存为 `zh-TW` 的测试存档，需要清档或在语言弹窗中重新选择才能恢复旧值。

## 13. 2026-08-07 首次启动西班牙语代码归一化

### 13.1 需求与原行为基线

- Android 系统首选语言为“Español (España)”时，`CultureInfo.CurrentUICulture.TwoLetterISOLanguageName` 提供裸代码 `es`。
- `Assets/Game/Configs/tblanguage.json` 与 `Assets/Resources/I2Languages.asset` 使用的开放代码为 `es-ES`、`es-US`，没有裸 `es`。
- I2 的 `LocalizationManager.GetLanguageFromCode(...)` 默认执行精确代码匹配，因此首次启动候选 `es` 会在 I2 应用阶段失败，并在配置校验阶段因未命中开放语言而回退 `en`。

### 13.2 修改范围与新行为

- `Assets/Game/Scripts/Common/LocalizatioinUtils.cs`
  - `GetCodeFromSystemCode(...)` 将系统返回的裸 `es` 归一化为 `es-ES`。
  - 首次启动的“Español (España)”现在会进入既有 `TrySetLanguage(...)` 链，统一更新 I2 与 `DB.GameSetting.CurrentLanguageKeyCode`。
  - 已保存或手动选择的完整代码（包括 `es-US`）不经过裸代码映射，后续启动行为保持不变。
- 未修改语言配置、I2 语言源、存档字段结构、Prefab、资源、事件、Analytics、RemoteConfig 或平台 SDK。

### 13.3 数据与平台流

```text
Android/iOS 首次启动系统语言 Español (España)
  -> CurrentUICulture.TwoLetterISOLanguageName = es
  -> GetCodeFromSystemCode: es -> es-ES
  -> TrySetLanguage(es-ES)
  -> I2 切换 Spanish (Spain)
  -> DB.GameSetting.CurrentLanguageKeyCode = es-ES
  -> 配置加载后通过 TbLanguage.IsOpen 校验
```

该映射只在 `DB.GameData.PlayCount == 0` 的首次启动系统语言检测中生效；后续启动仍以玩家存档语言为准，不会因系统语言变化覆盖玩家选择。

### 13.4 验证、剩余风险与回滚

- 静态对照确认 `tblanguage.json` 中 `es-ES` 为开放状态，`I2Languages.asset` 中存在 `Spanish (Spain) / es-ES`。
- PowerShell 静态断言确认代码包含 `es -> es-ES` 映射、`tblanguage.json` 中 `es-ES` 已开放，且 `I2Languages.asset` 存在 `Spanish (Spain) / es-ES`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 构建通过，结果为 `0 error / 101 warning`；警告为当前工程既有输出。
- `git diff --check` 对本次三个修改文件通过，仅输出仓库现有的 LF/CRLF 转换提示。
- Unity PlayMode、Android/iOS 清档首次启动、Loading UI 是否短暂显示旧语言仍为 `uncertain`，需要真机或 PlayMode 补验。
- 回滚只需删除 `GetCodeFromSystemCode(...)` 中 `es -> es-ES` 的局部映射；若测试存档已首次保存为 `es-ES`，需清档或在设置页重新选择语言才能复现旧流程。

## 14. 2026-08-21 首次语言改用 I2 设备语言解析结果

> 历史说明：本节记录的 GF 内部实现已在 2026-08-24 随父仓库 gitlink 修复撤回，当前有效实现见第 15 节。`Assets/GF` 为只读第三方子模块，不得依据本节再次修改。

### 14.1 需求与原行为基线

- `App.Awake()` 原先先由 `I2Kit.InitGameLanguage()` 按 `Application.systemLanguage` 和本地枚举表设置 I2，
  `LoadManager.EnterGame()` 又在 DB 初始化后通过 `CultureInfo.CurrentUICulture` 计算一次首次语言，存在两套地区码适配。
- 原 `CultureInfo` 流程只能稳定得到双字母代码，需要额外维护 `es -> es-ES`；葡萄牙语会产生裸 `pt`，
  无法命中当前开放的 `pt-BR`，配置校验后回退英文。
- I2 自身启动选择在无法匹配设备语言时会采用语言源第一个启用项；当前 `I2Languages.asset` 首项为
  `zh-CN`，因此业务不能直接把未经保护的 I2 默认结果当作未知语言回退值。
- `I2Kit.CurrentLanguage` 原为独立可变字段。`LocalizatioinUtils.TrySetLanguage(...)` 直接切换
  `LocalizationManager` 时不会更新该字段，邮件等读取入口可能观察到旧枚举。

### 14.2 修改范围与当前行为

- `Assets/GF/Scripts/Runtime/I2/I2Kit.cs`
  - `InitGameLanguage()` 改用 I2 的 `GetCurrentDeviceLanguage(true)`、`GetSupportedLanguage(...)` 和
    `GetLanguageCode(...)` 解析设备语言与地区；无法匹配时明确应用 `en`，不采用语言源首项作为业务默认。
  - `CurrentLanguage` 改为按 `LocalizationManager.CurrentLanguageCode` 实时反查的只读属性，移除与 I2
    实际状态可能发散的项目侧缓存。对应旧注释已因字段语义变化更新为当前事实。
- `Assets/Game/Scripts/Common/LocalizatioinUtils.cs`
  - 首次启动不再读取 `CultureInfo` 或维护项目侧系统语言映射，直接取得 I2Kit 初始化后实际生效的 code。
  - 首次候选只先更新 GameSetting 内存值并应用到 I2；等待 `TbLanguage` 校验完成后再保存最终 code，
    避免把未开放候选提前写盘。相关启动注释已同步为 I2 解析语义。
  - 后续启动仍以 `DB.GameSetting.CurrentLanguageKeyCode` 为准覆盖 I2 历史缓存；设置页继续复用
    `TrySetLanguage(...)` 同步 I2 与存档。
  - iOS 首次 `zh-CN -> zh-TW` 规则继续保留。
- `LocalizationPopup`、`tblanguage.json`、`I2Languages.asset`、存档 schema、Prefab、资源、事件、
  Analytics、RemoteConfig 和平台 SDK 均未修改。

### 14.3 数据、UI 与平台流

```text
App.Awake
  -> I2Kit.InitGameLanguage
       -> I2 解析设备语言与地区
       -> I2 不支持：立即回退 en

LoadManager.EnterGame
  -> DBManager.Initialize
  -> LocalizatioinUtils.InitializeStartupLanguage
       -> 首次：读取 I2 当前 code，写入 GameSetting 内存值，不立即存盘
       -> 后续：GameSetting 已保存 code 覆盖 I2
  -> PlayCount 增加

ConfigManager 加载 TbLanguage
  -> LocalizatioinUtils.ValidateAndApplyStartupLanguage
       -> code 已开放且 I2 支持：应用并保存首次最终值
       -> code 未开放或 I2 不支持：回退 en，并同步 I2 + GameSetting
       -> 配置不可用：首次回退 en；老玩家保留存档语言
```

- `LocalizationPopup` 仍只生成 `IsOpen == true` 的普通语言项，以
  `DB.GameSetting.CurrentLanguageKeyCode` 决定选中态；点击后继续通过 `TrySetLanguage(...)` 立即刷新 I2、
  保存 GameSetting 并更新勾选，不改变弹窗关闭、滚动或 Level65 自定义语言项逻辑。
- Android 的 I2 设备检测会优先读取 Java `Locale`，可保留 `pt-BR`、`es-US` 等地区信息；iOS 仍受
  Unity/I2 可取得的系统语言粒度限制，且继续应用项目现有中文映射。
- I2 支持但 `TbLanguage` 未开放的语言只会作为配置加载前候选；最终回退 `en`，不会出现在
  `LocalizationPopup`。若之后重新开放，老玩家不会被自动切换，仍保持其当前存档选择。

### 14.4 验证证据

- CodeGraph 已复核调用链：`App.Awake -> I2Kit.InitGameLanguage`、
  `LoadManager.EnterGame -> InitializeStartupLanguage`、配置加载出口到
  `ValidateAndApplyStartupLanguage`，以及 `LocalizationPopup.OnClickLanguageItem -> TrySetLanguage`。
- 静态语言断言通过：I2 设备解析入口、英文回退、旧 `CultureInfo/GetCodeFromSystemCode` 移除、
  `LocalizationPopup` 开放语言过滤和统一切换入口均存在；12 个开放 `TbLanguage` code 全部可在
  `I2Languages.asset` 中找到，`en` 为开放状态。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false
  /p:BuildInParallel=false` 构建通过：`0 error / 106 warning`。
- 主工程目标脚本与 GF 子模块 `I2Kit.cs` 的 `git diff --check` 均通过，仅有仓库现有 LF/CRLF 转换提示。

### 14.5 剩余风险与回滚

- Unity PlayMode、清档首次启动、I2 PlayerPrefs 与 GameSetting 冲突、配置加载失败、未开放语言、
  `pt-BR`、`es-US`、iOS `zh-CN -> zh-TW` 和 Android/iOS 真机均为 `uncertain`，仍需运行验证。
- I2 会早于 DB 与 Luban 配置初始化；配置未开放语言在 Loading 阶段是否出现短暂文本刷新仍为
  `uncertain`。最终进入游戏前会由 `ValidateAndApplyStartupLanguage()` 收敛到开放语言。
- 回滚时恢复 `I2Kit.InitGameLanguage()` 的枚举映射与 `CurrentLanguage` 字段，恢复
  `LocalizatioinUtils.GetCodeFromSystemCode(...)` 和首次即时保存逻辑；若测试存档已保存新的地区 code，
  需要清档或从语言设置页重新选择才能复现旧流程。

## 15. 2026-08-24 地区语言解析迁回项目层并保持 GF 只读

> 本节记录迁回项目层的基础方案；I2 匹配与项目地区分区的最终执行顺序以第 16 节为准。

### 15.1 需求、问题与原行为基线

- 2026-08-21 的实现错误地修改了 GF 子模块，并让父仓库一度引用 GF 远端不存在的提交
  `9593665bab9022402ead57b10beee061d1827f35`。CI 因 `not our ref` 无法取得该提交而失败。
- 父仓库 `origin/develop` 的修复提交 `6f10f82c80012225e9ebe03db95dc66175eb5e07` 已把 GF gitlink 恢复为远端可获取的
  `635506c9d1cc10cf143ef1e42ab7c447a98d5117`；本地功能分支已快进包含该修复，但未代替用户推送远端分支。
- GF `635506c9` 的 `I2Kit.InitGameLanguage()` 仍通过 `Application.systemLanguage` 做粗粒度枚举映射。
  该路径无法保留 Android Java `Locale` 可提供的地区信息：例如葡萄牙语会得到 `pt-PT`，不能命中当前开放的
  `pt-BR`；美式西班牙语会归一为 `es-ES`，不能保留 `es-US`。
- 用户明确要求禁止修改 GF，因此设备地区解析必须在 BrainPunk 项目层完成，不能再次依赖私有 GF 提交。

### 15.2 修改范围与当前行为

- `Assets/Game/Scripts/Common/LocalizatioinUtils.cs`
  - 首次启动在 DB 初始化后直接调用 I2 的 `GetCurrentDeviceLanguage(true)`、
    `GetSupportedLanguage(..., true)` 与 `GetLanguageCode(...)`，取得 I2 支持的设备地区 code。
  - I2 无法识别或不支持设备语言时明确回退 `en`，不采用语言源首个启用项作为业务默认值。
  - 首次候选仍只写入 GameSetting 内存并应用到 I2，等待 `TbLanguage.IsOpen` 校验后才保存最终 code；
    iOS 的首次 `zh-CN -> zh-TW` 产品规则保持不变。
  - 老玩家仍以 `DB.GameSetting.CurrentLanguageKeyCode` 覆盖 I2 历史缓存；设置页继续通过
    `TrySetLanguage(...)` 同步 I2 和 GameSetting，未改变已有用户选择。
- 父仓库仅采用已有的 GF gitlink 修复提交，GF 工作树中的源码、资源和配置均未被本次功能迁移修改。
- `tblanguage.json`、`I2Languages.asset`、存档 schema、Prefab、UI、事件、Analytics、RemoteConfig 和平台 SDK
  均未修改。
- 2026-08-21 将 `I2Kit.CurrentLanguage` 改成实时属性的方案已随 GF 回退撤销。当前项目的反馈邮件入口
  `EmailUtils` 读取 `DB.GameSetting.CurrentLanguageKeyCode`，不依赖 GF 的独立 `CurrentLanguage` 字段，
  因此当前多语言主流程不需要重新引入该 GF 修改。

### 15.3 启动、配置与存档流

```text
App.Awake
  -> GF I2Kit.InitGameLanguage（保持 635506c9 原状，可能先设置粗粒度临时语言）

LoadManager.EnterGame
  -> DBManager.Initialize
  -> LocalizatioinUtils.InitializeStartupLanguage
       -> 首次：I2 读取设备 Locale/地区并匹配支持语言
       -> 无法匹配：候选明确回退 en
       -> iOS zh-CN：按现有产品规则转为 zh-TW
       -> 候选写入 GameSetting 内存并应用 I2，不立即保存
       -> 后续：GameSetting 已保存 code 覆盖 I2 历史缓存
  -> PlayCount 增加并保存

ConfigManager 加载 TbLanguage
  -> LocalizatioinUtils.ValidateAndApplyStartupLanguage
       -> 候选已开放且 I2 支持：应用并保存首次最终值
       -> 候选未开放或 I2 不支持：回退 en，同步 I2 与 GameSetting
       -> 配置不可用：首次回退 en；老玩家保留存档语言
```

- GF 在 `App.Awake` 的临时语言不会成为最终业务依据；DB 初始化后项目层会重新解析设备地区并覆盖，
  配置加载完成后再根据开放语言表进行最终收敛。
- 没有新增全局事件、资源加载入口或额外存档字段；首次保存时序与第 14 节已确认的“配置校验后落盘”保持一致。

### 15.4 验证证据

- 父仓库当前 `HEAD` 为 `6f10f82c80012225e9ebe03db95dc66175eb5e07`，其 GF gitlink 指向
  `635506c9d1cc10cf143ef1e42ab7c447a98d5117`；GF 当前 `HEAD` 与该 gitlink 一致。
- `git -C Assets/GF diff -- Scripts/Runtime/I2/I2Kit.cs` 无输出，确认本次未修改 GF 多语言代码。
  GF 状态中的 DOTween 文件改动为任务开始前已有内容，本次未清理、覆盖或纳入功能修改。
- 静态断言通过：项目层存在 I2 设备地区解析三段调用和英文回退；旧
  `GetInitializedI2LanguageCode()` 已移除；`tblanguage.json` 的 12 个开放 code 全部存在于
  `I2Languages.asset`，且 `en` 处于开放状态。
- `git diff --check -- Assets/Game/Scripts/Common/LocalizatioinUtils.cs` 通过，仅有工作区既有的 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false
  /p:BuildInParallel=false` 构建通过：`0 error / 106 warning`；警告为当前工程既有输出。

### 15.5 剩余风险与回滚

- Unity PlayMode、Android/iOS 清档首次启动、`pt-BR`、`es-US`、iOS 中文映射、配置加载失败以及
  Loading 早期文本是否短暂显示 GF 粗粒度临时语言仍为 `uncertain`，需要对应运行环境补验。
- Android 的 I2 路径会读取 Java `Locale`；不同系统对菲律宾语返回 `tl` 或 `fil` 的差异是否均能命中
  当前语言源仍为 `uncertain`，需要目标系统版本验证。
- `PlayCount` 在最终语言配置校验前已保存；若进程恰好在两者之间中断，下次启动会按老玩家路径处理内存候选。
  这是既有首次保存时序风险，本次未扩大修改范围，需要后续 PlayMode/异常中断专项验证。
- 若必须回滚本次项目层迁移，只回退 `LocalizatioinUtils.GetSupportedDeviceLanguageCode()` 的调用与实现；
  GF gitlink 继续保持 `635506c9`，不得通过恢复 `9593665b` 或修改 GF 来回滚。回滚后 `pt-BR`、`es-US`
  会重新受粗粒度系统语言映射限制。

## 16. 2026-08-24 I2 默认匹配后的统一地区分区处理

### 16.1 需求与基线

- 用户明确要求 I2 保持语言匹配权威：项目地区规则不得提前参与、替代或改变 I2 的默认匹配过程，
  只能在 I2 已完成匹配并产出语言 code 后统一后处理语言分区。
- I2 的 `GetSupportedLanguage(...)` 会先按完整 code 匹配，失败后再忽略地区匹配同语种首项。
  当前语言源中 `es-ES` 排在 `es-US` 前，因此 Android `es-MX` 默认会先匹配为 `es-ES`；
  这是地区分区后处理的输入，不应绕开 I2 直接决定语言资源。
- 先前讨论的“项目归类后再检查 I2”顺序不符合该要求，已放弃且未写入 GF 或 I2 包。

### 16.2 当前行为与影响范围

- `Assets/Game/Scripts/Common/LocalizatioinUtils.cs`
  - `GetSupportedDeviceLanguageCode()` 先完整调用 I2 的设备检测、支持语言匹配和 code 反查；I2 无匹配结果时
    仍直接回退 `en`。
  - I2 已匹配成功后，才调用 `PostprocessLanguageRegion(deviceLanguage, matchedLanguageCode)` 统一处理项目分区。
  - 西语设备 code 包含地区时，西班牙 `es-ES` 归入 `es-ES`，其余西语地区归入当前拉美资源 `es-US`；
    设备只提供裸 `es` 时不推断地区，保留 I2 默认匹配结果。
  - 分区目标必须精确存在于 I2 语言源；若目标资源缺失，保留 I2 原匹配结果，不因后处理直接回退英文。
  - 既有 iOS `zh-CN -> zh-TW` 产品规则迁入同一个后处理入口，执行阶段改变但最终行为不变。
- `tblanguage.json`、`I2Languages.asset`、存档 schema、UI、Prefab、资源、事件、Analytics、RemoteConfig、
  平台 SDK 与 GF 均未修改。配置加载后仍由 `ValidateAndApplyStartupLanguage()` 检查最终 code 是否开放。

### 16.3 执行顺序与西语矩阵

```text
I2 GetCurrentDeviceLanguage(true)
  -> I2 GetSupportedLanguage(deviceLanguage, true)
  -> I2 GetLanguageCode(supportedLanguage)
  -> 无匹配：en
  -> 有匹配：PostprocessLanguageRegion
       -> iOS zh-CN：zh-TW
       -> es-ES 地区：es-ES
       -> 其他 es-* 地区：es-US
       -> 裸 es / 非西语：保留 I2 匹配结果
       -> 分区目标不在 I2：保留 I2 匹配结果
  -> TbLanguage.IsOpen 最终校验
  -> 应用 I2 并按既有首次启动时序保存 GameSetting
```

| 设备语言证据 | I2 默认匹配 | 地区后处理结果 |
| --- | --- | --- |
| `es-ES` | `es-ES` | `es-ES` |
| `es-US` | `es-US` | `es-US` |
| `es-MX`、`es-AR` 等 | 当前语言源顺序下为 `es-ES` | `es-US` |
| 裸 `es` / `Spanish` | 当前语言源顺序下为 `es-ES` | 保留 `es-ES` |
| 非西语 | I2 默认结果 | 保留 I2 默认结果 |

### 16.4 验证、风险与回滚

- 静态核对确认 I2 的 `GetSupportedLanguage(...)` 实现顺序为“完整 code -> 忽略地区的同语种匹配 -> 名称匹配”；
  地区后处理调用位于该流程之后。
- `tblanguage.json` 中 `es-ES`、`es-US` 均开放，`I2Languages.asset` 中两个精确 code 均存在。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false
  /p:BuildInParallel=false` 构建通过：`0 error / 0 warning`。
- GF `I2Kit.cs` 定向 diff 为空；本次没有修改或清理 GF 工作树。
- Android 各厂商 Java `Locale` 到 I2 语言名称的实际结果、iOS 仅提供裸 `Spanish`、清档首次启动、
  Loading 早期文本与 Android/iOS 真机仍为 `uncertain`。裸 `es` 按要求不推断地区，因此保持 I2 默认的
  `es-ES`；若产品以后要求无地区西语归入拉美，需要另行明确改变后处理规则。
- 回滚地区分区时删除两个西语常量和 `PostprocessLanguageRegion(...)`，恢复直接返回 I2 匹配 code；
  同时把既有 iOS 中文规则移回首次启动调用处。不得通过修改 GF 或 I2 包完成回滚。

## 17. 2026-09-03 启动弹窗一次性 UniTask 序列骨架

### 17.1 需求、基线与设计边界

- 后续需要按“更新公告 -> 旧数据转换 -> ChestGift”编排启动弹窗，用户确认先抽出 UniTask 队列结构，本阶段不改动现有弹窗打开时机。
- 原 `LoadManager.OnPostProcess()` 在同一收尾阶段直接创建公告与 `TransformPopup`，两者没有关闭等待契约；`GiftManager` 只通过 `DailyScheduleModule` 间接接入 Loading，还没有启动自动打开步骤。
- 新增 `Assets/Game/UI/Loading/Scripts/StartupPopupSequence.cs/.meta`。该类是启动收尾专用的普通 C# 一次性执行器，不是 `MonoBehaviour`、单例、全局 Popup 队列或 `GiftManager` 的新职责。

### 17.2 结构契约与运行流

- `Enqueue(key, Func<CancellationToken, UniTask>)` 用于追加普通异步步骤；`EnqueuePopup(key, Func<Action, bool>)` 把“条件不满足则跳过、打开成功则等待 OnClose”收口为同一契约。
- `RunAsync()` 严格按入队顺序执行。单步普通异常会记录后跳过，不阻断后续启动弹窗；取消会终止整个序列。
- 弹窗等待使用 `UniTaskCompletionSource` 和真实关闭回调，不使用 `Update`、`WaitUntil(popup == null)` 或常驻轮询，因此未引入持续帧开销。
- 序列一旦启动，不允许再追加步骤或重复执行；完成或取消后清空剩余引用，如需重试必须创建新实例。
- 未来接入时，取消令牌必须绑定常驻 `UIManager` 或应用生命周期；不能绑定 `LoadManager` GameObject，否则切换到 Main 场景时会把尚未关闭的弹窗等待提前取消。

### 17.3 影响、验证、风险与回滚

- 本节记录的是接入前骨架阶段：当时尚未在 `LoadManager`、`BulletinBoardPopup`、`TransformPopup` 或 `GiftManager` 中实例化序列；最新接入状态以下方第 18 节为准。
- Unity 生成的 `Assembly-CSharp.csproj` 和 Bee `Assembly-CSharp.rsp` 已收录新脚本；执行运行时程序集构建通过，结果为 `0 error / 122 warning`，warning 为当前工程既有输出。
- `uncertain`：序列尚未实例化，公告/转换/ChestGift 的实际关闭回调、外部 `CloseAllUI`、场景切换取消与 Android/iOS 横屏都未进入 PlayMode/真机验证。
- 后续接入必须保证每个成功打开的弹窗在所有销毁路径都会完成关闭回调；否则队列会停在当前步骤。
- 回滚时删除 `StartupPopupSequence.cs/.meta` 和本节记录即可；因尚未接入任何调用点，无需迁移存档、Prefab 或配置。

## 18. 2026-09-03 启动弹窗序列正式接入

### 18.1 需求、原行为与影响范围

- 用户确认将一次性 UniTask 序列接入 Loading，顺序固定为“更新公告 -> 旧 Tips 转金币 -> ChestGift”，前一个弹窗真实关闭后才能进入下一步。
- 原 `LoadManager.OnPostProcess()` 在同一帧先创建 `BulletinBoardPopup`、再创建 `TransformPopup`；`UIManager` 会隐藏前一个 Popup 栈顶，玩家实际会先看到后创建的转换弹窗。ChestGift 没有启动自动打开路径。
- 影响脚本为 `LoadManager.cs`、`StartupPopupSequence.cs`、`BulletinBoardPopup.cs`、`TransformPopup.cs` 和 `GiftManager.cs`；未修改 Prefab、Luban/RemoteConfig、Schedule 存档、购买/恢复、奖励、analytics 或平台 SDK。

### 18.2 当前调用流与关闭契约

```text
LoadManager.OnPostProcess
  -> ReadyToPlay（StartPanel.OnOpen -> ConfirmHomeReached）
  -> SyncRestore / Achievement / 初始奖励 / 版本收尾 / 红点
  -> StartupPopupSequence
       -> BulletinBoardPopup（按收尾前缓存的新版本资格）
       -> 真实 OnClose，跨一个 PlayerLoop 帧
       -> TransformPopup（存在可转换 Tips 时）
       -> SyncOldVersion + 真实 OnClose，跨一个 PlayerLoop 帧
       -> GiftManager.IsEntryAvailable(ChestGift)
       -> GiftManager.OpenStartupOnce(ChestGift)
```

- `BulletinBoardPopup.Create(Action)` 和 `TransformPopup.Create(Action)` 保留无参默认值，既有调用源码兼容；关闭回调在真实 `OnClose` 清理完成后且只触发一次。
- `StartupPopupSequence` 在完成回调后额外等待一个 `PlayerLoopTiming.Update` 帧，避免在 `UIManager.DestroyUI/CloseAllUI` 正在清理旧栈时同步重入打开下一窗。
- 启动序列在新版本标记清除前缓存公告资格，但在其它 `OnPostProcess` 收尾操作完成后才开始，既保留旧公告条件，也避免无前置弹窗时 ChestGift 抢在首次奖励和红点初始化之前打开。
- `GiftManager.OpenStartupOnce()` 只在真实打开成功后记录当前应用会话已自动展示该 Gift 类型；首次主页、购买当日、全部已购、配置无效或打开失败均不记录成功。普通入口仍调用 `Open()`，不受自动展示记录限制。
- 序列始终使用常驻 `UIManager.GetCancellationTokenOnDestroy()`，三个步骤为静态委托，不捕获或依赖切场景后销毁的 `LoadManager` 实例。

### 18.3 验证、剩余风险与回滚

- 运行时程序集构建通过：`0 error / 122 warning`，warning 为当前工程既有输出。静态检查确认 Loading 中不再直接连续创建公告和转换弹窗，ChestGift 只在两个前置步骤结束后调用。
- `uncertain`：尚未在 Unity PlayMode 实测八种前置条件组合、强制 `CloseAllUI/DestroyAllUI`、首次/旧存档、购买当日、五档全购、场景切换和 Android/iOS 横屏。
- 若弹窗被外部代码只执行 `OnHide` 而永不进入 `OnClose`，序列会合理地停在当前步骤；现有关闭按钮和 UIManager 正常销毁路径均会进入 `OnClose`。
- 回滚时恢复 `LoadManager` 直接创建公告/转换弹窗，删除两个 Popup 的可选关闭回调、`GiftManager.OpenStartupOnce()` 与 `StartupPopupSequence.cs/.meta`；不涉及存档、Prefab、配置或玩家购买数据迁移。

## 19. 2026-09-07 ChestGift 等待 StartPanel 进场完成

### 19.1 需求、基线与当前时序

- 启动 ChestGift 必须等 StartPanel 根 Animator 与两个按钮缩放动画全部结束，避免礼包覆盖首页进场表现；StartPanel 动画期间由其本地 ClickBlock 禁止按钮输入。
- 原序列在公告和旧 Tips 转金币步骤结束后直接检查 ChestGift 资格，没有等待首页动画的契约。当前 ChestGift 步骤先从 `UIManager` 取得已由 `ReadyToPlay()` 创建的 StartPanel，再等待其 `WaitForEnterAnimationAsync(cancellationToken)`。
- 等待完成后会再次确认 StartPanel 实例仍存在且 `IsOpen`，随后才执行原有 `GiftManager.IsEntryAvailable(ChestGift)` 与 `OpenStartupOnce(ChestGift)`。StartPanel 不可用时跳过本次启动礼包并记录 warning，不创建新的首页实例。
- 序列顺序仍为公告 -> 旧 Tips 转金币 -> ChestGift；前两个弹窗的关闭回调、跨帧栈保护、Gift 资格、会话一次性记录、配置、存档和购买逻辑均未改变。

### 19.2 验证、风险与回滚

- 静态确认 `ReadyToPlay()` 在 `StartStartupPopupSequence()` 前同步创建 StartPanel；运行时程序集构建通过：`0 error / 122 warning`。
- `uncertain`：Unity PlayMode 下无前置弹窗、有公告、有转换弹窗及二者都有时的实际层级与弹出帧尚未验证；StartPanel 被外部提前关闭时会跳过 ChestGift，需结合真实启动流程复核。
- 回滚时把 `TryToShowStartupChestGiftAsync()` 恢复为同步资格检查，并移除 StartPanel 对外等待 API；不影响其它启动弹窗或玩家数据。

## 2026-09-22：OneLink 首页消费门禁

- 启动队列在 ChestGift 步骤后只通知 OneLink“所有弹窗已派发”。礼包关闭、后续奖励弹窗、关闭销毁和遮罩淡出由 StartPanel 的运行态检查继续等待，不延长 Loading 网络超时、不等待 OneLink 请求。
- StartPanel 进场完成且 Main START、应用有焦点、Popup/Top 与关闭遮罩均结束后才尝试一次进关；SDK 回调不重入 UI 栈。未收到链接时无额外业务动作。
- 同日补充：OneLinkManager 在上述门禁放行后，若只有系统 URL 而尚无 SDK 参数，开始 query 本地解析或 Android 短码主动解析；此能力包含在正式包中。结果仍排队等下一次首页门禁检查，不阻塞 Loading，也不增加首页输入锁。SDK 已自动返回有效参数时不发此补拉。iOS 不透明短码仍依赖 SDK 自动 UDL。
- 编译与模拟门禁验证通过，真实启动动画／购买回焦／手机 SDK 为 `uncertain`。实现、两个编辑器 API、风险与回滚见 `Assets/Game/Scripts/SDK/Services/Core/OneLink功能与测试记录.md`。

## 2026-09-23 OneLink 跳过启动 ChestGift

- 启动弹窗顺序仍为更新公告、旧数据转换、ChestGift、OneLink 派发。ChestGift 步骤等待 StartPanel 首次进场完成后，如果 `OneLinkManager.HasPendingRequest` 为 true，则不打开启动礼包并直接继续 OneLink 派发。
- 该判断只控制 Loading 的启动自动弹窗，不消费礼包自动展示次数，不改变普通入口、首次胜利礼包、Daily、购买、奖励或存档；公告与旧数据转换仍保持原顺序。
- 完整 `Assembly-CSharp.csproj` 编译结果为 `0 error / 1030 warning`。真机冷启动、UDL 晚到和普通启动礼包仍待验证；删除该条件分支即可回滚。
