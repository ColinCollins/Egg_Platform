# GameSDKService LoginDay 登录日打点功能说明

## 1. 需求与背景

项目已有 `GameSDKService.FBCustom.LoginDay()` 空入口和 `LoginCheckDays` 检测天数列表，但没有读取 `PlayDays`、没有发送事件，也没有运行时调用点。本次要求在 GuruSDK 初始化完成后执行检测，不从 `LoadManager.EnterGame()` 直接调用。

## 2. 影响文件与系统

- `Assets/Game/Scripts/SDK/Services/Core/GameSDKService.cs`
- `Assets/Game/Scripts/SDK/Services/Events/GameSDKService.FBCustom.cs`
- `Assets/Game/Scripts/Schedule/Modules/PlayDaysRefreshModule.cs`
- Bear SaveModule：`DBManager`、`DB.GameData.PlayDays`、`DB.GameSchedule`
- Schedule System：当地 `DailyScheduleModule`
- GuruAnalytics：Facebook capability 和 Facebook SDK 待发送队列

未新增 DB 字段、PlayerPrefs Key、配置表、资源、UI、全局事件、RemoteConfig、广告或 IAP 链路。

## 3. 修改前后行为

### 修改前

- `LoginDay()` 方法体为空。
- `LoginCheckDays` 当前只包含第 `2` 天。
- `GameSDKService.InitService()` 在 `await GuruSdk.Initialize(...)` 后只执行 `onInitCallback`。

### 修改后

- `GameSDKService.InitService()` 在 `await GuruSdk.Initialize(...)` 返回后调用 `LoginDay()`，随后仍执行原有 `onInitCallback`。
- `LoginDay()` 只允许在 `Application.isPlaying` 时启动检测，并以 fire-and-forget 方式运行，不阻塞 SDK Ready 回调。
- 检测最多等待 10 秒，直到 DB 已初始化、当地 Daily 模块已注册且 `Daily` 刷新时间为当地今天日界线。
- 只有 `DB.GameData.PlayDays` 精确命中 `LoginCheckDays`，并且 `PlayDaysRefreshModule.WasIncrementedThisSession` 为 `true` 时才上报，避免同一当地自然日多次冷启动重复发送。
- 当前事件名格式为 `login_day_{天数}`；第 2 天发送 `login_day_2`，参数包含 `login_day = 2`。
- 事件 capability 固定为 `AnalyticsCapabilities.FacebookCapabilities`，不会发送到 Firebase、Guru、Adjust 或 AppsFlyer。
- GuruAnalytics 的 Facebook 实现会在 Facebook SDK 尚未完成初始化时缓存事件，并在 `FB.Init` 回调完成后发送。

## 4. 数据与调用流

```text
LoadManager.EnterGame
  -> booster.Init() 异步启动 GameSDKService.InitService
  -> DBManager.Initialize()
  -> 注册并检查当地 Daily
  -> PlayDaysRefreshModule 当天首次刷新：PlayDays++、SaveGameData、标记 WasIncrementedThisSession

GameSDKService.InitService
  -> await GuruSdk.Initialize(...)
  -> LoginDay()
  -> 等待 DB 与当地 Daily 就绪（最多 10 秒）
  -> PlayDays 命中配置天数且本进程完成过递增
  -> LogEvent("login_day_{day}", ..., FacebookCapabilities)
```

## 5. 验证记录

- 静态检查确认 `LoginDay()` 调用位于 `await GuruSdk.Initialize(...)` 之后，`LoadManager.EnterGame()` 没有直接调用 `LoginDay()`。
- 静态检查确认运行态外层有 `Application.isPlaying` 保护，没有新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、PlayerPrefs、全局事件或资源实例化链路。
- 因 Unity 编辑器未自动把未跟踪脚本刷新进生成的 `Assembly-CSharp.csproj`，使用工作区外临时 MSBuild targets 显式引入 `GameSDKService.FBCustom.cs` 和两个 Schedule 新脚本后编译；最终结果为 0 error / 101 warnings，临时文件已删除，warnings 未逐项审计。
- 目标 tracked 文件 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示；新增脚本未发现行尾空白。

## 6. 剩余风险与未验证项

- `login_day_{天数}` 是本次新增命名，项目内没有既有事件表或数据侧文档可验证；需要数据/投放侧确认最终名称和参数口径。
- Facebook Events Manager 真机收数、DebugView、Android/iOS 构建与 ATT/隐私同意场景尚未验证。
- 如果 DB 或当地 Daily 在 10 秒内未就绪，本次启动会跳过登录日事件，不会在后台无限等待。
- 当前按精确天数匹配，不补发已经错过的里程碑；例如当前 `PlayDays = 3` 时不会补发第 2 天事件。
- `WasIncrementedThisSession` 只在 `PlayDays` 成功保存后置为 `true`；保存失败不会发送事件。现有 Schedule 基类仍可能写入周期刷新时间，因此该日不会自动重试。
- 当前只配置第 2 天；增加其他天数需要修改 `LoginCheckDays`。

## 7. 回滚路径

- 从 `GameSDKService.InitService()` 移除 `LoginDay()` 调用。
- 恢复 `GameSDKService.FBCustom.LoginDay()` 为空实现。
- 从 `PlayDaysRefreshModule` 移除 `WasIncrementedThisSession`；不需要迁移或清理存档数据。
