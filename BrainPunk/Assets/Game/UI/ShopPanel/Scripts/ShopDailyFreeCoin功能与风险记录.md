# ShopPanel 每日免费金币功能与风险记录

## 需求与原行为基线

- ShopPanel 需要提供每日一次的免费金币领取入口；当前数量优先来自
  `level_config.dailyFreeCoinCount`，字段缺失或解析失败时回退 `GlobalConst.dailyFreeCoinCount`。
- 用户已在 `ShopPanel.prefab` 放入 `DailyCoin` 卡片、数量文本、`Free` 文案、`cd_txt` 和 `CommonRedDot` 视觉节点；原节点没有领取、存档、倒计时和红点业务逻辑。
- 原 `GameSchedule` 没有记录该奖励上次领取时间和已观察时间高水位，无法跨会话阻止同一自然日重复领取，也无法识别设备时间回拨。
- `ShopPanel` 已有 `CommonUIRewardAnimCtrl` 与 `shopGetReward` 音效链路，可复用为领取表现。

## 影响文件与系统

- `Assets/Game/UI/ShopPanel/Scripts/ShopDailyFreeCoinController.cs`
  - 独立管理卡片点击、配置展示、资格校验、倒计时、发奖和领取表现；每日金币逻辑不进入 `ShopPanel.cs`。
- `Assets/Game/UI/ShopPanel/Resources/ShopPanel.prefab`
  - 在 `DailyCoin` 绑定独立控制器；在 `DailyCoin/Root` 增加 `CustomButton` 与 `UIEmptyRaycastTarget`，并显式绑定全部运行时引用。
- `Assets/Game/DB/GameSchedule.cs`、`GameSchedule_Partial.cs`
  - 新增 `DailyFreeCoinLastClaimTime`，保存最近一次成功领取的 Unix 秒时间戳。
  - 新增只增不减的 `DailyFreeCoinLastObservedTime`，保存该功能已观察到的最大有效时间，用于跨会话识别设备时间回拨。
- `Assets/Game/Scripts/Reddot/RedDotKey.cs`
  - 追加 `DailyGiftCoin = 120`，未复用或修改既有枚举值。
- `Assets/Game/Scripts/Reddot/Shop/DailyGiftCoinRedDotChecker.cs`、`DailyGiftCoinRedDotRegister.cs`
  - 注册只读资格 checker；Loading 初始化红点时一并注册。
- `Assets/Game/UI/Loading/Scripts/LoadManager.cs`
  - 注册 `DailyGiftCoinRedDotRegister`。
- `GlobalConst.dailyFreeCoinCount`
  - 当前工作区配置值为 `10`，作为云控字段不可用时的本地兜底；控制器不写死奖励数量。
- `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
  - 提供 `dailyFreeCoinCount` 字段常量与统一 Getter，负责远端优先、本地兜底。

## 新行为与数据流

1. DailyCoin 激活后通过 `ConfigManager.RemoteConfig.GetDailyFreeCoinCount()` 读取云控/本地兜底数量，并读取
   `GameSchedule.DailyFreeCoinLastClaimTime` 与 `DailyFreeCoinLastObservedTime`。
2. 从未领取，或当前 Unix 时间已到达“上次领取所在设备当地自然日的下一天 00:00”时，卡片显示 `Free` 并允许点击。
3. 不可领取时隐藏 `Free`，显示 `HH:mm:ss`；控制器使用非缩放时间每秒刷新一次，跨过 00:00 后立即恢复可领取。
4. 点击时重新校验资格，先写入并保存领取时间；保存失败不发金币，避免同一奖励因存档失败重复领取。
5. 时间保存成功后，通过 `Bag.AddTool(GameProps.Coin, count, RewardType.Other)` 发金币；发放结果异常时尝试回滚领取时间。
6. 成功后播放 `shopGetReward`，并从 DailyCoin 卡片位置调用现有 `CommonUIRewardAnimCtrl` 播放 8 个飞行金币。
7. `DailyGiftCoinRedDotChecker` 与卡片控制器共享同一资格规则；首次进入、领取成功和跨日恢复时刷新 `RedDotKey.DailyGiftCoin`。
8. 当前进程首次查询时建立“设备 UTC 墙钟 + `Time.realtimeSinceStartupAsDouble`”基线；运行中墙钟偏离单调时间超过 60 秒后，资格和倒计时继续使用单调时间，不接受改系统时间带来的跨日。
9. 控制器启用、禁用、进入后台和领取时将有效时间高水位保存到 `GameSchedule`；后续会话若设备时间早于高水位，则保持冷却直到时间追平。

## 生命周期与边界

- 控制器只在 PlayMode 的 `OnEnable` 绑定按钮，在 `OnDisable` 解绑；`OnValidate` 仅限制动画数量，不访问 DB 或配置。
- 红点 checker 只查找场景中已经存在且已初始化的 `DBManager`、`ConfigManager`，不创建单例、不写存档、不发奖。
- 设备时间被回拨到 `DailyFreeCoinLastObservedTime` 之前时保持冷却；运行中设备时间前跳或回拨时使用单调时间继续推进。
- 未新增 `PlayEvents` 或 Bear 全局事件；金币更新继续沿用 `SimpleBag` 的既有道具更新事件与保存流程。

## 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:q -consoleLoggerParameters:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：`0 error / 122 warning`。
- 由于 Unity 已打开且本地生成的 `Assembly-CSharp.csproj` 尚未自动纳入三个新增脚本，编译验证期间临时把新增脚本加入该忽略文件，验证后已移除临时条目；仓库源文件和 prefab 未依赖该临时修改。
- 加入时间高水位与单调时钟保护后再次执行同一主程序集编译：`0 error / 122 warning`；Unity Editor 最新日志显示 `Finished compiling`，未在最近 500 行发现新的 `error CS` 或 `Compilation failed`。
- ShopPanel prefab YAML 检查：新增 fileID 无重复；控制器、按钮、数量、Free、倒计时、Owner、奖励动画均为显式引用；`RedDotView.redDotKey = 120`。
- 静态检查确认没有在 `OnValidate`、`Reset`、编辑器预览入口访问 DB，也没有新增运行时节点名扫描或资源加载。
- 抗改时间静态路径检查：红点 checker 与卡片资格共同使用 `DailyGiftCoinClockGuard`；存档高水位只递增，时间回拨分支不会降低或覆盖该值。

## 剩余风险与未验证项

- Unity 当前打开实例在新增脚本首次热重载时曾因本地 C# 工程未刷新报告找不到新注册器；后续已完成编译且最新日志未发现新的编译错误，但仍需在 Console 和 PlayMode 中确认历史错误已清空。
- 尚未手工验证首次领取、连续点击、关闭重开、跨当地 00:00、前跳/回拨设备时间、金币动画终点及红点显隐。
- 本地存档和进程内单调时钟不能可靠识别“应用完全关闭期间把设备时间持续调到未来”和“玩家真实离线多日”的差异；当前实现可防运行中改时间及跨会话回拨重复领取。若需要覆盖关闭应用后持续向未来快进，必须引入服务器可信时间或平台侧可信时钟。
- `SimpleBag.AddTool` 返回数量但不返回 `GameData` 实际落盘结果；极端存储失败时可能出现本次会话金币已增加但重启后丢失，领取时间仍已保存。当前取舍优先满足“不重复领取”。
- Android/iOS 横屏触控与设备当地时区/DST 边界尚未验证，状态为 `uncertain`。

## 回滚路径

- 从 `ShopPanel.prefab` 移除 `ShopDailyFreeCoinController`、DailyCoin/Root 的点击组件并把 RedDot Key 恢复为 `None`。
- 删除独立控制器与 DailyGiftCoin 红点 checker/register，移除 Loading 注册和 `RedDotKey.DailyGiftCoin`。
- 删除 `GameSchedule.DailyFreeCoinLastClaimTime`、`DailyFreeCoinLastObservedTime` 字段及属性。旧存档多出的字段可被忽略，不需要迁移脚本。

## 2026-09-07 非正数奖励配置隐藏卡片

### 需求与原行为

- 用户要求 `GlobalConst.dailyFreeCoinCount` 小于 0 时不显示 ShopPanel 的 DailyReward；结合原规则已把 `<= 0` 统一视为
  无效奖励，本次对 0 和负数采用相同隐藏行为。
- 修改前规则层会拒绝领取，但 `DailyCoin` 根节点仍显示，界面呈现 `x0`、隐藏 Free、禁用按钮并显示 `00:00:00`。

### 当前行为与边界

- `ShopDailyFreeCoinController` 已确认直接挂在 `ShopPanel.prefab` 的 `DailyCoin` 根节点。运行态启用时若配置已经就绪且
  `DailyFreeCoinCount <= 0`，控制器直接关闭自身 `gameObject`，整张卡片从布局中隐藏。
- 配置尚未初始化或 GlobalConst 缺失时不会按 0 误判为关闭，仍进入原有无状态防御分支；正数配置的领取、倒计时、
  时间高水位、红点、存档和飞金币流程不变。
- 非正数配置导致组件自行隐藏时不会执行 `OnDisable()` 的时间高水位保存，避免关闭功能仍产生 DailyReward 存档写入。
- 规则层新增只读的配置数量解析，并由显隐判断与领取资格共同复用；没有新增 Prefab 引用、名称查找、全局事件或配置写入。
- DailyCoin 隐藏后不会在同一实例内主动监听运行时配置变化；当前 ShopPanel 关闭时会销毁，重新创建或下次启动时会按最新配置重判。

### 验证、剩余风险与回滚

- 静态确认控制器与 `RedDotView` 均挂在 fileID `6095950535955464870` 的 `DailyCoin` 根节点，隐藏不依赖子节点层级。
- `uncertain`：Unity PlayMode 的负数、0、正数三组布局与 Android/iOS 横屏显示尚未手工验证。
- 回滚时删除 `OnEnable()` 的 `IsRewardDisabled()` 分支与规则辅助方法，恢复 `TryGetState()` 内原配置读取即可；Prefab、存档和
  配置无需迁移。

## 2026-09-07 GM 跨天测试即时刷新

### 问题与当前行为

- GM Schedule 原“跨天测试”只回退通用 Daily 时间和 ChestGift 购买日，没有回退 `DailyFreeCoinLastClaimTime`；免费金币因此继续按当天已领取处理。
- GM 现把免费金币领取日期回退到昨天当地 00:00。若 ShopPanel 已打开，控制器会在原有每秒 Tick 中检测领取时间变化并立即调用现有 `RefreshState()`，同步 Free、按钮、倒计时和红点状态；重新打开 ShopPanel 仍走原 `OnEnable()` 刷新。
- `DailyFreeCoinLastObservedTime` 保持只增不减，GM 不直接发金币，也不修改奖励数量、领取落盘顺序、飞金币动画或当地自然日算法。

### 验证、风险与回滚

- 静态确认 GM 复用一次 `SaveGameSchedule()` 保存 Daily 与免费金币日期，Shop 控制器只在领取时间值变化时额外重算状态；`git diff --check` 通过，运行时程序集构建为 `0 error / 121 warning`。
- `uncertain`：Unity PlayMode 即时刷新、再次领取、红点和移动端横屏尚未实测。
- 回滚时删除 GM 的领取时间回退与红点刷新，并移除 Shop 控制器的 `_lastKnownClaimTimestamp` 检测；无需迁移正常玩家数据。

## 2026-09-07 冷却期点击提示

### 需求与原行为

- 每日免费金币处于倒计时冷却时，点击卡片需要通过 `SystemTips` 显示多语言键 `U_Shop_WaitingCD_01`。
- 原状态刷新把 `claimButton.Interactable` 直接设置为领取资格；冷却期按钮无法派发点击，`TryClaim()` 的不可领取分支也只刷新状态，因此不会显示提示。

### 当前行为与边界

- 只要正数配置和 `GameSchedule` 能形成有效状态，DailyCoin 点击按钮在可领取与冷却期都保持可交互；冷却视觉仍隐藏 `Free` 并显示原倒计时。
- `TryClaim()` 每次点击继续重新计算当前资格。确实处于冷却时刷新界面并调用 `SystemTips.Show(LocalizationManager.GetTranslation("U_Shop_WaitingCD_01"))`，不写领取时间、不发金币、不播放奖励动画。
- 如果点击瞬间已经跨过下一当地自然日，即使界面的一秒刷新尚未执行，也会以本次实时资格进入正常领取流程，不要求再次点击。
- 配置未就绪、奖励非正数或 `GameSchedule` 缺失时仍不会把按钮开放为可领取入口；本次不修改 Prefab、存档字段、红点、时间保护、金币动画和全局事件。
- 当前本地化 CSV 与 I2 资源中均已存在 `U_Shop_WaitingCD_01`；本次控制器修改只消费该键。

### 验证、风险与回滚

- 静态确认冷却分支发生在任何领取时间写入和 `Bag.AddTool()` 之前，且按钮交互只在规则状态有效时保持开启。
- 运行时程序集定向构建通过：`0 error / 121 warning`；`git diff --check` 通过，warning 为工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 验证冷却点击的 SystemTips 文案、快速连点、倒计时跨零点击及 Android/iOS 横屏触控。
- 回滚时恢复 `claimButton.Interactable = _isAvailable`，并删除冷却分支中的 `SystemTips` 调用与多语言键常量；无需迁移 Prefab 或玩家数据。

## 2026-09-11 每日金币数量补齐云控消费链

### 需求、基线与修复行为

- 用户要求补齐 `dailyFreeCoinCount` 云控逻辑，并要求后续所有由 `LubanGenerator` 写入 `spec.yaml` 的业务字段
  同步具备对应云控消费逻辑。
- 修改前字段已存在于 Luban JSON、生成器白名单和 Guru `level_config`，但卡片规则仍直接读取
  `TbGlobalConst.DataList[0].DailyFreeCoinCount`；即使 Firebase 返回不同值，展示、显隐、领取数量和红点资格也不会消费它。
- 当前新增 `ConfigManager.RemoteConfig.DailyFreeCoinCountFieldKey` 与 `GetDailyFreeCoinCount()`：优先解析
  `level_config.dailyFreeCoinCount`，字段缺失或类型解析失败时回退 `GlobalConst.DailyFreeCoinCount`，两者均不可用时返回 0。
- `DailyGiftCoinRules` 保留“ConfigManager 已初始化”门禁，数量读取改为统一 Getter。卡片展示、非正数隐藏、实际发奖和
  `DailyGiftCoinRedDotChecker` 因共享该规则而使用同一个最终值；领取日期、时间防篡改、存档顺序、动画和事件流程不变。

### 验证、风险与回滚

- 静态链路检查通过：Luban JSON/Guru 字段 → `RemoteConfigCache.level_config` → Getter → `DailyGiftCoinRules` →
  卡片与红点；运行时业务代码已无绕过 Getter 直接读取 `DailyFreeCoinCount` 的路径。
- 完整项目依赖编译通过：`dotnet build Assembly-CSharp.csproj --no-restore`，`0 error / 1141 warning`；warning 为项目既有范围。
  首次关闭项目依赖构建的定向命令因 `Temp/bin/Debug` 缺少约 200 个 Unity/Guru/Apple 元数据 DLL 失败，恢复项目依赖构建后通过。
- 当前 Editor 日志的最近新鲜拉取结果仍为 `dailyFreeCoinCount: 10`，与本地兜底相同；真实远端不同值、字段缺失、错误类型、
  0/负数，以及 Android/iOS 真机领取仍需验证，状态为 `uncertain`。
- 本次不修改 `RemoteConfigService`、`ForceFetch`、缓存回退、Loading 时序、Prefab、存档字段或平台分支。
- 回滚时删除字段常量与 Getter，并让 `TryGetConfiguredRewardCount()` 恢复直接读取 GlobalConst；无需迁移玩家存档或清理缓存。
