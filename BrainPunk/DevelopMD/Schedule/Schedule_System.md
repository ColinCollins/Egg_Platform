# Schedule System — 周期调度系统

## 1. 概述

Schedule System 是通用的周期调度框架，用于在特定时间点（如跨天、跨周、活动开启）统一触发业务模块的刷新逻辑。

`DailyScheduleModule` 提供按设备当地自然日 00:00 刷新的能力；`DailyUTCScheduleModule` 提供独立的 UTC 00:00 刷新能力。后续可平滑扩展周刷新、限时活动等策略。

---

## 2. 核心组件

### 2.1 IScheduleRefreshable（业务接口）

```csharp
public interface IScheduleRefreshable
{
    /// <summary>模块标识，用于日志追踪</summary>
    string RefreshKey { get; }

    /// <summary>周期刷新触发时执行</summary>
    void OnScheduleRefresh();
}
```

### 2.2 ScheduleSystem（中央控制器）

`Singleton<ScheduleSystem>`，职责：
1. 管理所有 `ScheduleModuleBase` 子类实例
2. 启动时统一触发各模块的 `CheckAndRefresh()`，运行时预约最近的当地或 UTC 日期边界
3. 提供 `GetModule<T>()` 供业务代码获取指定调度模块

### 2.3 DailyScheduleModule（当地自然日内置模块）

`ScheduleModuleBase` 的子类，职责：
1. 按设备当地自然日判断是否需要刷新
2. **构造函数中主动创建并注册所有当地 Daily 子模块**（`PlayDaysRefreshModule`、`InterstitialNOAdPopupAutoShowModule`、`ShopAdRewardTipAutoShowModule`、`GiftManager`）
3. 跨天时依次执行 `OnScheduleRefresh()`
4. 读取/写入 `DB.GameSchedule`
5. 提供 `GetRefreshable<T>()` 供外部按类型获取子模块实例

**日期比较规则**：
- 使用 `TimeZoneInfo.Local` 将当前 UTC 时刻和已保存时间戳转换为设备当地日期
- 已保存当地日期早于今天，或尚无保存值时触发刷新
- 刷新完成后保存设备当地今天 00:00 对应的 Unix 秒时间戳，并调用 `DB.SaveGameSchedule()`
- 旧版 `Daily` 保存的 UTC 00:00 时间戳会先转换为当地日期再比较，避免直接切换时间戳口径造成常规升级场景重复刷新

### 2.4 DailyUTCScheduleModule（UTC 自然日内置模块）

`DailyUTCScheduleModule` 使用独立 `ModuleKey = "DailyUTC"`，按 UTC 00:00 判断刷新。当前只提供调度注册点和公开日界线计算接口，不默认注册 `PlayDays`、广告或商店业务；需要 UTC 口径的业务必须显式注册到该模块。

**异常防护**：
- 每个业务模块的 `OnScheduleRefresh()` 被 `try-catch` 包裹，单个模块失败不影响其他模块

---

## 3. 数据存储

Schedule 的周期标记独立存储在 `GameSchedule` 中。业务刷新模块可按职责保存自己的数据；`PlayDaysRefreshModule` 会更新并保存 `GameData.PlayDays`。

| 字段 | 类型 | 位置 | 说明 |
|------|------|------|------|
| `scheduleLastRefreshTimes` | `Dictionary<string, long>` | `GameSchedule` | 各模块最后刷新时间（Unix 秒） |
| `playDays` | `int` | `GameData` | 按设备当地自然日累计的游玩天数 |
| `chestGiftDailyRefreshCount` | `int` | `GiftData` | ChestGift 当地自然日累计刷新次数，按 1～5 映射当前礼盒；`GameSchedule` 同名字段仅保留作旧存档迁移源 |

**存储隔离**：
```
DB.GameData       ← 游戏进度数据（关卡、背包、广告计数等）
DB.GameSchedule   ← Schedule 专用数据（调度模块刷新时间）
DB.GameSetting    ← 玩家设置（音效、语言等）
DB.GiftData       ← Gift 业务数据（首次主页、ChestGift 日刷计数、最近购买当地日期）
```

---

## 4. 调用时机

```
LoadManager.EnterGame()
  ├── booster.Init()
  ├── GameManager.Instance.Init()
  ├── DBManager.Instance.Initialize()
  ├── DB.GameData.PlayCount++ / Save()
  ├── ScheduleSystem.Instance.RegisterModule(new DailyScheduleModule())     ← 注册当地 Daily
  ├── ScheduleSystem.Instance.RegisterModule(new DailyUTCScheduleModule())  ← 注册 UTC Daily
  └── ScheduleSystem.Instance.CheckAllModules()                          ← 触发检查
```

> 放置在 `DBManager.Initialize()` 之后，确保 `GameSchedule` 已加载完毕。

运行时由 `GameManager.Update()` 调用 `ScheduleSystem.Tick()`。普通帧只比较 `Time.unscaledTime` 与预约截止值；到达最近的当地/UTC 午夜或 60 秒校准点时才读取系统日期。应用恢复前台时由 `OnApplicationFocus` / `OnApplicationPause` 立即校准并重算日界线，跨日才执行 `CheckAllModules()`。

---

## 5. 模块接入指南

### 5.1 最小实现

当地 Daily 业务模块实现 `IScheduleRefreshable` 后，由 `DailyScheduleModule` 在构造函数中统一创建并注册，**不需要在业务代码中手动 `Register`**。UTC Daily 当前没有默认业务，业务方需要在 `CheckAllModules()` 前获取 `DailyUTCScheduleModule` 并显式 `Register`。

```csharp
public class ShopModule : IScheduleRefreshable
{
    public string RefreshKey => "shop";

    public void OnScheduleRefresh()
    {
        // 重置每日购买次数
        // 刷新限时商品池
        // 重置每日奖励领取状态
    }
}
```

### 5.2 注意事项

- **注册方式**：Daily 子模块由 `DailyScheduleModule` 构造函数统一创建并 `Register`，无需业务代码自行注册。非 Daily 模块仍需在 `CheckAllModules()` 之前手动 `Register`。
- `RefreshKey` 需全局唯一，建议用模块名或功能域名
- `OnScheduleRefresh()` 内不要执行耗时操作，保持轻量；如需异步可用 `UniTask.Void()` 启动后台任务
- **外部获取子模块**：通过 `ScheduleSystem.Instance.GetModule<DailyScheduleModule>()?.GetRefreshable<T>()` 按类型查询

---

## 6. 日志输出

ScheduleSystem 和 DailyScheduleModule 会在关键节点打印日志：

```
[ScheduleSystem] Module registered: Daily              ← 模块注册成功
[ScheduleSystem] CheckAllModules. Count=1              ← 开始全局检查
[Schedule] [Daily] Trigger. Count=3                    ← Daily 满足刷新条件
[Schedule] [Daily] shop OK                             ← 某业务模块成功
[Schedule] [Daily] quest OK
[Schedule] [Daily] Finished.                           ← Daily 全部完成
```

---

## 7. 扩展方向

当前仅内置了 `DailyScheduleModule`。若后续需要：

| 需求 | 实现方式 |
|------|----------|
| 周刷新 | 新增 `WeeklyScheduleModule : ScheduleModuleBase`，判断自然周起始 |
| 限时活动 | 新增 `ActivityScheduleModule : ScheduleModuleBase`，传入起止时间戳 |
| 独立周期 | 在 `ScheduleModuleBase.ShouldRefresh()` 中按模块自身规则判断 |

扩展步骤：
1. 新建 `XXXScheduleModule.cs`，继承 `ScheduleModuleBase`
2. 在 `LoadManager.EnterGame()` 中 `RegisterModule(new XXXScheduleModule())`
3. 业务模块通过 `ScheduleSystem.Instance.GetModule<XXXScheduleModule>().Register(this)` 接入

---

## 8. 文件位置

| 文件 | 说明 |
|------|------|
| `Assets/Game/Scripts/Schedule/IScheduleRefreshable.cs` | 业务接口 |
| `Assets/Game/Scripts/Schedule/ScheduleModuleBase.cs` | 调度模块基类 |
| `Assets/Game/Scripts/Schedule/ScheduleSystem.cs` | 中央控制器 |
| `Assets/Game/Scripts/Schedule/Modules/DailyScheduleModule.cs` | Daily 内置模块，持有子模块实例 |
| `Assets/Game/Scripts/Schedule/Modules/DailyUTCScheduleModule.cs` | UTC Daily 内置模块，使用独立 `DailyUTC` Key |
| `Assets/Game/Scripts/Schedule/Modules/PlayDaysRefreshModule.cs` | 当地 Daily 的累计游玩天数业务模块 |
| `Assets/Game/Scripts/Runtime/Ads/InterstitialNOAdPopupAutoShowModule.cs` | 插屏免广告弹窗每日计数模块 |
| `Assets/Game/UI/ShopPanel/Scripts/ShopAdRewardTipAutoShowModule.cs` | 商店免费金币提示每日计数模块 |
| `Assets/Game/DB/GameSchedule.cs` | Schedule 数据字段定义 |
| `Assets/Game/DB/GameSchedule_Partial.cs` | Schedule 数据属性 & 访问方法 |
| `Assets/Game/UI/Loading/Scripts/LoadManager.cs` | 启动注册与触发点 |

---

## 9. 2026-06-24 DataViewer 登记记录

### 需求背景

`Tools/DataViewer` 需要能显示 `DB.GameSchedule` 对应的 PlayerPrefs 存档数据，便于观察 `Daily` 刷新时间戳和每日计数字段。

### 影响文件与系统

- `Assets/Game/Settings/DBSetting.asset`
- `Assets/Game/DB/GameSchedule.cs`
- `Assets/Game/Settings/DBManager_Generated.cs`
- `Assets/BearGame/com.bear.savemodule@fa7a4686436c/Runtime/Editor/DataViewer.cs`
- Bear SaveModule 的 `PlayerPrefsSaveProvider`

### 变更前行为

`DB.GameSchedule` 已由生成代码暴露，并且 `GameSchedule.StorageType` 为 `StorageType.PlayerPrefs`。但 `DBSetting.asset` 的 `dataClasses` 只登记了 `GameData` 和 `GameSetting`。`DataViewer.RefreshPlayerPrefs()` 在 `DBSetting.DataClasses` 非空时只按该列表生成 `SaveData_<className>` 候选 key，不再 fallback 扫描全部 `BaseSaveDataSO` 类型，因此不会把 `SaveData_GameSchedule` 纳入显示候选。

### 变更后行为

`DBSetting.asset` 新增 `GameSchedule` 登记：

- `className: GameSchedule`
- `filePath: Assets/Game/DB/GameSchedule.cs`
- `storageType: 0`（`StorageType.PlayerPrefs`）
- `isValid: 1`

之后 `DataViewer` 刷新 PlayerPrefs 页时会把 `SaveData_GameSchedule` 加入候选；实际显示仍取决于本机 PlayerPrefs 中是否已经存在该 key。正常运行中需要 `DB.SaveGameSchedule()` 写入后才会落盘生成。

### 数据与存档流

`GameSchedule` 仍保持独立于 `GameData` 的 PlayerPrefs 存储。保存路径未变：`DB.SaveGameSchedule()` -> `DBManager.Instance.Save<GameSchedule>()` -> `BaseSaveDataSO.Save()` -> `PlayerPrefsSaveProvider` -> `SaveData_GameSchedule`。

### 验证记录

- 静态检查确认 `DBManager_Generated.cs` 已包含 `DB.GameSchedule` 和 `SaveGameSchedule()`。
- 静态检查确认 `GameSchedule.StorageType = StorageType.PlayerPrefs`。
- 静态检查确认 `DataViewer.RefreshPlayerPrefs()` 使用 `DBSetting.DataClasses` 生成候选 key，并在 `PlayerPrefs.HasKey(key)` 为 true 时显示。
- 本次未运行 Unity Editor，未实际打开 `Tools/DataViewer` 验证界面显示。

### 剩余风险与回滚

- 若当前设备尚未生成 `SaveData_GameSchedule`，DataViewer 仍不会显示该条数据；需要先通过启动 Daily 检查或 GM 调试入口触发 `DB.SaveGameSchedule()`。
- 若后续通过 SaveModule 扫描重写 `DBSetting.asset`，需要确认 `GameSchedule` 仍保留在登记列表中。
- 回滚方式：从 `DBSetting.asset` 的 `dataClasses` 中移除 `GameSchedule` 条目。

---

## 10. 2026-07-10 当地 Daily、UTC Daily 与 PlayDays 接入记录

### 需求与原有行为

- 原 `DailyScheduleModule` 固定按 UTC 00:00 判断刷新，`GameData.PlayDays` 只有字段和访问属性，没有递增、保存或调度注册逻辑。
- 需求改为按设备当地自然日累计 `PlayDays`，同时保留一套独立 UTC Daily 调度入口，并公开两种日界线计算接口。

### 影响文件与系统

- `Assets/Game/Scripts/Schedule/Modules/DailyScheduleModule.cs`
- `Assets/Game/Scripts/Schedule/Modules/DailyUTCScheduleModule.cs`
- `Assets/Game/Scripts/Schedule/Modules/PlayDaysRefreshModule.cs`
- `Assets/Game/UI/Loading/Scripts/LoadManager.cs`
- `DB.GameData.PlayDays`、`DB.SaveGameData()`、`DB.GameSchedule.scheduleLastRefreshTimes`

### 新行为与数据流

- `DailyScheduleModule` 默认使用 `TimeZoneInfo.Local`；公开 `GetTodayStartTimestamp()` 和可传入 `DateTimeOffset`、`TimeZoneInfo` 的 `GetDayStartTimestamp(...)`。
- 当地 Daily 触发后依次执行已注册业务；`PlayDaysRefreshModule` 将 `DB.GameData.PlayDays` 加一并立即调用 `DB.SaveGameData()`。
- `DailyUTCScheduleModule` 使用 `ModuleKey = "DailyUTC"`，公开 UTC 日界线接口，并将自己的最后刷新时间保存到 `GameSchedule` 的独立 Key。
- `LoadManager.EnterGame()` 在 DB 初始化后注册当地 Daily 和 UTC Daily，再统一调用 `CheckAllModules()`。
- 未新增全局事件、配置、资源加载、UI、analytics 或平台 SDK 链路；Android 与 iOS 都读取设备系统当地时区。

### 兼容、验证与剩余风险

- 当地 Daily 会把旧 `Daily` UTC 时间戳转换为当前当地日期后比较，常规升级场景不直接按两种午夜 Unix 值比较。
- 静态检查已确认两套 `ModuleKey` 分别为 `Daily` / `DailyUTC`，`PlayDaysRefreshModule` 只注册到当地 Daily，且递增后调用 `DB.SaveGameData()`；目标 tracked 文件 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- Unity 编辑器当时未自动刷新两个新脚本到生成的 `Assembly-CSharp.csproj`；使用工作区外临时 MSBuild targets 显式引入新脚本后执行 `dotnet build Assembly-CSharp.csproj --no-restore`，最终复跑结果为 0 error / 100 warnings，临时文件已删除。warnings 未逐项审计。
- Unity PlayMode、真机时区切换、夏令时边界、跨当地午夜但进程不重启、GameData 保存失败、Android/iOS 设备验证仍需手工补验。
- `PlayDaysRefreshModule` 保存失败时会抛出异常，由 `ScheduleModuleBase` 记录该子模块失败；但现有基类仍会继续其他子模块并写入本周期刷新时间，因此保存失败不会在当天自动重试，这是当前 Schedule 框架的剩余风险。
- `PlayDaysRefreshModule.WasIncrementedThisSession` 只在本进程成功递增并保存 `PlayDays` 后置为 `true`，供 GuruSDK 登录日打点识别“当天首次当地 Daily 刷新”；该状态不新增存档字段，重新启动进程后恢复为 `false`。
- `GameManager.Update()` 现持续调用 `ScheduleSystem.Tick()`；App 前台持续运行会在预约日界线刷新，后台跨日则在应用恢复回调中补刷新。普通帧不读取系统日期，60 秒校准用于处理前台修改系统时间或时区。
- 玩家主动修改系统时区可能导致当地日期前进或后退，从而出现提前刷新或等待日期追平；本实现不引入服务端防篡改。
- `PlayDays` 不回填历史游玩天数；如果升级当日的 `Daily` 已由旧版本刷新，新增计数从下一次当地 Daily 刷新开始。

### 回滚路径

- 从 `LoadManager` 移除 `DailyUTCScheduleModule` 注册。
- 从 `DailyScheduleModule` 移除 `PlayDaysRefreshModule` 注册，并恢复 UTC 日界线计算。
- 删除新增的两个模块脚本及其 `.meta`；`GameData.PlayDays` 可保留为原有未使用字段，不需要迁移存档结构。
