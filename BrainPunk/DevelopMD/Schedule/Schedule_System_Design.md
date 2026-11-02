# Schedule System — 周期调度系统设计

## 1. 设计目标

将原来的"每日刷新"升级为通用的**周期调度系统**。`Daily` 不再是顶层的概念，而是作为 `ScheduleSystem` 内部的一个**内置调度模块**存在。系统为后续扩展周刷新、限时活动、自定义周期等预留设计空间。

核心原则：
- **分层解耦**：基础设施（ScheduleSystem）↔ 调度策略（ScheduleModule）↔ 业务逻辑（IScheduleRefreshable）
- **策略可插拔**：新增周期类型只需新增 ScheduleModule，不改动框架
- **周期标记隔离**：Schedule 的周期时间保存在独立 `GameSchedule`；业务刷新模块按职责保存自己的业务数据

---

## 2. 架构分层

```
┌─────────────────────────────────────────────────────────────┐
│                    ScheduleSystem (Singleton)                │
│  职责：模块容器、生命周期管理、统一存储触发、启动与跨日检查   │
└────────────────────┬────────────────────────────────────────┘
                     │
        ┌────────────┼────────────┬──────────────────────┐
        ▼            ▼            ▼                      ▼
┌──────────────┐ ┌──────────┐ ┌──────────────┐  ┌──────────────┐
│ DailySchedule│ │DailyUTCSch│ │ActivitySched │  │ CustomSched  │
│   Module     │ │  Module   │ │   Module     │  │   Module     │
│ (当地内置)    │ │ (UTC内置)  │ │  (预留)      │  │  (预留)      │
└──────┬───────┘ └────┬─────┘ └──────┬───────┘  └──────┬───────┘
       │              │              │                 │
       └──────────────┴──────────────┴─────────────────┘
                          │
                          ▼
              ┌───────────────────────┐
              │  IScheduleRefreshable │
              │   (业务模块接口)       │
              └───────────────────────┘
```

---

## 3. 核心定义

### 3.1 业务接口

```csharp
/// <summary>
/// 需要被周期调度的业务模块。
/// 实现类向具体的 ScheduleModule（如 DailyScheduleModule）注册自己。
/// </summary>
public interface IScheduleRefreshable
{
    /// <summary>模块标识，用于日志追踪</summary>
    string RefreshKey { get; }

    /// <summary>周期刷新触发时执行</summary>
    void OnScheduleRefresh();
}
```

### 3.2 调度模块基类

```csharp
/// <summary>
/// 调度模块抽象基类。封装公共的注册列表、遍历执行、异常防护。
/// 子类只需实现「是否需要刷新」的判断逻辑。
/// </summary>
public abstract class ScheduleModuleBase
{
    protected readonly List<IScheduleRefreshable> _refreshables = new();

    /// <summary>模块唯一 Key，用于存储区分（如 "Daily" / "Weekly"）</summary>
    public abstract string ModuleKey { get; }

    /// <summary>依赖注入入口</summary>
    public void Register(IScheduleRefreshable refreshable)
    {
        if (refreshable == null) return;
        _refreshables.Add(refreshable);
    }

    /// <summary>
    /// 由 ScheduleSystem 调用。判断是否满足刷新条件，
    /// 若满足则执行所有注册的业务模块，并持久化刷新时间。
    /// </summary>
    public void CheckAndRefresh()
    {
        if (!ShouldRefresh())
            return;

        foreach (var r in _refreshables)
        {
            try
            {
                r.OnScheduleRefresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Schedule] [{ModuleKey}] {r.RefreshKey} failed: {e}");
            }
        }

        OnRefreshed();
    }

    /// <summary>判断当前是否满足刷新条件</summary>
    protected abstract bool ShouldRefresh();

    /// <summary>刷新完成后回调，用于持久化当前周期时间</summary>
    protected abstract void OnRefreshed();
}
```

### 3.3 Daily 模块实现

`DailyScheduleModule` 按设备当地自然日运行，在构造函数中统一创建并注册所有当地 Daily 子模块，确保启动时 `_refreshables` 已完整。

```csharp
public class DailyScheduleModule : ScheduleModuleBase
{
    public override string ModuleKey => "Daily";

    public PlayDaysRefreshModule PlayDaysModule { get; }
    public InterstitialNOAdPopupAutoShowModule InterstitialNOAdPopupModule { get; }
    public ShopAdRewardTipAutoShowModule ShopAdRewardTipModule { get; }
    public GiftManager GiftModule { get; }

    public DailyScheduleModule()
    {
        PlayDaysModule = new PlayDaysRefreshModule();
        this.Register(PlayDaysModule);

        InterstitialNOAdPopupModule = new InterstitialNOAdPopupAutoShowModule();
        this.Register(InterstitialNOAdPopupModule);

        ShopAdRewardTipModule = new ShopAdRewardTipAutoShowModule();
        this.Register(ShopAdRewardTipModule);

        GiftModule = GiftManager.Instance;
        this.Register(GiftModule);
    }

    /// <summary>按类型获取已注册的 Daily 刷新业务模块实例。</summary>
    public T GetRefreshable<T>() where T : class, IScheduleRefreshable
    {
        foreach (var r in _refreshables)
        {
            if (r is T t)
                return t;
        }
        return null;
    }

    protected override bool ShouldRefresh()
    {
        long lastDate = DB.GameSchedule.GetScheduleLastRefreshTime(ModuleKey);
        if (lastDate <= 0)
            return true;

        var timeZone = TimeZoneInfo.Local;
        DateTime today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).Date;
        DateTime lastRefreshDate = TimeZoneInfo.ConvertTime(
            DateTimeOffset.FromUnixTimeSeconds(lastDate), timeZone).Date;
        return lastRefreshDate < today;
    }

    protected override void OnRefreshed()
    {
        long today = GetTodayStartTimestamp();
        DB.GameSchedule.SetScheduleLastRefreshTime(ModuleKey, today);
        DB.SaveGameSchedule();
    }

    public static long GetTodayStartTimestamp()
    {
        return GetDayStartTimestamp(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
    }

    public static long GetDayStartTimestamp(DateTimeOffset currentTime, TimeZoneInfo timeZone)
    {
        DateTimeOffset localTime = TimeZoneInfo.ConvertTime(currentTime, timeZone);
        DateTime localDayStart = new DateTime(
            localTime.Year, localTime.Month, localTime.Day,
            0, 0, 0, DateTimeKind.Unspecified);
        TimeSpan offset = timeZone.GetUtcOffset(localDayStart);
        return new DateTimeOffset(localDayStart, offset).ToUnixTimeSeconds();
    }
}
```

### 3.4 UTC Daily 模块实现

`DailyUTCScheduleModule` 使用独立 `ModuleKey = "DailyUTC"` 和 UTC 00:00 日界线。当前不默认注册当地 Daily 业务，UTC 业务必须显式注册，避免同一业务每天执行两次。

### 3.5 ScheduleSystem 中央控制器

```csharp
public class ScheduleSystem : Singleton<ScheduleSystem>, IDebuger
{
    private readonly Dictionary<string, ScheduleModuleBase> _modules = new();

    /// <summary>注册调度模块。通常在 ScheduleSystem.Init() 中完成。</summary>
    public void RegisterModule(ScheduleModuleBase module)
    {
        if (module == null) return;
        _modules[module.ModuleKey] = module;
    }

    /// <summary>
    /// 启动、GM 调试或运行时跨日时调用：遍历所有已注册模块并检查刷新。
    /// </summary>
    public void CheckAllModules()
    {
        foreach (var module in _modules.Values)
        {
            module.CheckAndRefresh();
        }
    }
}
```

---

## 4. 数据存储设计

Schedule 的周期标记使用独立的数据类 `GameSchedule`（继承 `BaseSaveDataSO`）。业务刷新模块仍可按职责修改业务存档，例如 `PlayDaysRefreshModule` 更新 `GameData.PlayDays` 并单独保存 `GameData`。

### 4.1 GameSchedule 字段

通用周期时间戳继续保存在 `GameSchedule`。ChestGift 的业务刷新次数已经迁入独立 `GiftData.chestGiftDailyRefreshCount`；
`GameSchedule` 同名字段只保留一个兼容版本，作为历史 PlayerPrefs 的迁移源，新业务不得写入。
该值只由 `GiftManager.OnScheduleRefresh()` 分发到 `ChestGiftController` 后增加，弹窗打开和关闭不得修改；当前档位按 1～5 循环映射。

```csharp
// Assets/Game/DB/GameSchedule.cs
public partial class GameSchedule : BaseSaveDataSO
{
    public static StorageType StorageType = StorageType.PlayerPrefs;

    [SerializeField]
    private Dictionary<string, long> scheduleLastRefreshTimes = new();
}
```

### 4.2 GameSchedule 访问方法（Partial 中扩展）

```csharp
// Assets/Game/DB/GameSchedule_Partial.cs
public long GetScheduleLastRefreshTime(string moduleKey)
{
    if (scheduleLastRefreshTimes != null &&
        scheduleLastRefreshTimes.TryGetValue(moduleKey, out var value))
    {
        return value;
    }
    return 0;
}

public void SetScheduleLastRefreshTime(string moduleKey, long timestamp)
{
    if (scheduleLastRefreshTimes == null)
        scheduleLastRefreshTimes = new Dictionary<string, long>();

    scheduleLastRefreshTimes[moduleKey] = timestamp;
}
```

### 4.3 Init 初始化

```csharp
public override void Init()
{
    scheduleLastRefreshTimes = new Dictionary<string, long>();
}
```

### 4.4 DB 访问

```csharp
// 读取
DB.GameSchedule.GetScheduleLastRefreshTime("Daily");

// 写入
DB.GameSchedule.SetScheduleLastRefreshTime("Daily", todayTimestamp);
DB.SaveGameSchedule();
```

> 使用 `Dictionary<string, long>` 的好处：新增 Weekly、Activity 等模块时，无需改动 `GameSchedule` 结构，直接写入新的 Key 即可。独立数据类也便于后续单独清理或迁移 Schedule 数据。

---

## 5. 初始化与调用时机

### 5.1 系统初始化

```csharp
// LoadManager.EnterGame() 中
DBManager.Instance.Initialize();

// 初始化 ScheduleSystem 并注册内置模块
ScheduleSystem.Instance.RegisterModule(new DailyScheduleModule());
ScheduleSystem.Instance.RegisterModule(new DailyUTCScheduleModule());
// 未来扩展：
// ScheduleSystem.Instance.RegisterModule(new WeeklyScheduleModule());
// ScheduleSystem.Instance.RegisterModule(new ActivityScheduleModule());

ScheduleSystem.Instance.CheckAllModules();
```

初始化完成后，`GameManager.Update()` 持续调用 `ScheduleSystem.Tick()`。系统以当前时间计算最近的当地或 UTC 午夜，并换算为 `Time.unscaledTime` 截止值；普通帧只比较截止值，不读取系统日期、不遍历模块、不访问存档。每 60 秒重新读取系统时间校准一次，以处理前台修改时间或时区；应用恢复前台时由 GameManager 的 Focus/Pause 回调立即补做日期检查并重新预约。

### 5.2 业务模块接入

Daily 子模块由 `DailyScheduleModule` 构造函数统一创建并注册，**不需要自行 Register**。

```csharp
public class InterstitialNOAdPopupAutoShowModule : IScheduleRefreshable, IDebuger
{
    public string RefreshKey => "InterstitialNOAdPopupAutoShow";

    public void OnScheduleRefresh()
    {
        DB.GameSchedule.Daily_interstitialNoAdPopupShowCount = 0;
        DB.SaveGameSchedule();
    }

    public bool ShouldShowNoAdPopup() { /* 业务判断 */ }
    public void RecordShow() { /* 记录展示 */ }
}
```

外部代码通过 `DailyScheduleModule.GetRefreshable<T>()` 获取子模块实例：

```csharp
var noAdModule = ScheduleSystem.Instance
    .GetModule<DailyScheduleModule>()
    ?.GetRefreshable<InterstitialNOAdPopupAutoShowModule>();

if (noAdModule != null && noAdModule.ShouldShowNoAdPopup())
{
    NoAdsPopup.Create();
    noAdModule.RecordShow();
}
```

---

## 6. 扩展预留

### 6.1 WeeklyScheduleModule（周刷新）

```csharp
public class WeeklyScheduleModule : ScheduleModuleBase
{
    public override string ModuleKey => "Weekly";

    protected override bool ShouldRefresh()
    {
        long lastWeek = DB.GameSchedule.GetScheduleLastRefreshTime(ModuleKey);
        long currentWeek = GetCurrentWeekStartTimestamp();
        return lastWeek < currentWeek;
    }

    protected override void OnRefreshed()
    {
        DB.GameSchedule.SetScheduleLastRefreshTime(ModuleKey, GetCurrentWeekStartTimestamp());
        DB.SaveGameSchedule();
    }
}
```

### 6.2 ActivityScheduleModule（限时活动）

```csharp
public class ActivityScheduleModule : ScheduleModuleBase
{
    public override string ModuleKey => "Activity_Xmas2026";

    // 活动有固定起止时间
    private readonly long _startTime;
    private readonly long _endTime;

    protected override bool ShouldRefresh()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now < _startTime || now > _endTime)
            return false;

        long last = DB.GameSchedule.GetScheduleLastRefreshTime(ModuleKey);
        return last < _startTime; // 活动期间首次触发
    }

    protected override void OnRefreshed()
    {
        DB.GameSchedule.SetScheduleLastRefreshTime(ModuleKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        DB.SaveGameSchedule();
    }
}
```

### 6.3 自定义周期（冷启动后 N 小时）

可在 `ScheduleModuleBase` 的子类中自由实现 `ShouldRefresh()` 逻辑，如：
- 自然日（Daily）
- 自然周（Weekly）
- 自然月（Monthly）
- 固定时间窗口（Activity）
- 上次刷新后 N 小时（Cooldown）

---

## 7. 与旧版 DailyRefreshController 的关系

| | 旧版 DailyRefreshController | 新版 ScheduleSystem |
|---|---|---|
| 定位 | 顶层控制器，只支持 Daily | 框架层，支持多策略 |
| 存储 | `GameData.dailyRefreshLastDate` (long) | `GameSchedule.scheduleLastRefreshTimes` (Dictionary) |
| 注册方式 | `DailyRefreshController.Instance.Register(this)` | `DailyScheduleModule` 构造函数统一创建并注册 |
| 扩展性 | 改代码 | 新增 ScheduleModule 子类 |

---

## 8. 文件规划

| 文件 | 说明 |
|------|------|
| `Assets/Game/Scripts/Schedule/IScheduleRefreshable.cs` | 业务接口 |
| `Assets/Game/Scripts/Schedule/ScheduleModuleBase.cs` | 调度模块基类 |
| `Assets/Game/Scripts/Schedule/ScheduleSystem.cs` | 中央控制器 |
| `Assets/Game/Scripts/Schedule/Modules/DailyScheduleModule.cs` | Daily 内置模块 |
| `Assets/Game/Scripts/Schedule/Modules/DailyUTCScheduleModule.cs` | UTC Daily 内置模块 |
| `Assets/Game/Scripts/Schedule/Modules/PlayDaysRefreshModule.cs` | 当地自然日累计游玩天数业务模块 |
| `Assets/Game/Scripts/GameManager.cs` | 运行时调用轻量 Tick，并在应用恢复前台时立即校准日期边界 |
| `Assets/Game/Scripts/Schedule/Modules/WeeklyScheduleModule.cs` | 周刷新（预留） |
| `Assets/Game/Scripts/Schedule/Modules/ActivityScheduleModule.cs` | 活动周期（预留） |
| `Assets/Game/DB/GameSchedule.cs` | 数据字段定义 |
| `Assets/Game/DB/GameSchedule_Partial.cs` | 属性 & Get/Set 访问方法 |
| `Assets/Game/UI/Loading/Scripts/LoadManager.cs` | 注册模块并触发 `CheckAllModules()` |
