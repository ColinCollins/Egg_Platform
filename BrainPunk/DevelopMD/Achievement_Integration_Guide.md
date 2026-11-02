# Guru SDK 成就系统接入指南

> 基于 Guru SDK Game 成就接入文档整理。

---

## 一、概述

- **SDK 版本要求**：Unity 中台 v3.2.0 以上
- **支持平台**：
  - **Android**：Google Play Games 成就
  - **iOS**：Apple Game Center 成就
- **iOS 最低版本要求**：`16.0`（注意：登录文档写的是 15.6，成就系统要求 16.0）

### 前置条件

成就系统**强依赖登录系统**，接入前必须完成：
1. [游戏登录接入](./LoginTODO.md)（`AuthType.Game`）
2. [Game 成就配置文档](https://guru-doc-page.pages.dev/unity/next/GuruSdk/Services/2.2_Account/2.2.6_Game%E6%88%90%E5%B0%B1%E9%85%8D%E7%BD%AE) — 在后台创建成就并获取平台 ID

---

## 二、SDK 配置

### 2.1 更新 `guru_sdk.yaml`

在 `Assets/Guru/guru_sdk.yaml` 的 `integrated` 列表中确保包含：

```yaml
guru_sdk: v26.4.0  # 当前项目版本

integrated:
  # ... 其他模块
  - google_play_games   # Google Play Games 服务
  - apple_game_center   # Apple Game Center 服务
  - auth_game           # 游戏账号认证（登录）
```

> ⚠️ **注意**：当前项目的 `guru_sdk.yaml` 只有 `auth_game`，缺少 `google_play_games` 和 `apple_game_center`。

### 2.2 更新 `guru_spec.yaml`

在 `Assets/Guru/guru_spec.yaml` 末尾添加 `achievements` 节点：

```yaml
# Assets/Guru/guru_spec.yaml

achievements:
  Newbie_Of_Passing:
    title: "Newbie of passing"
    description: "Unlock by passing 5 levels."
    type: single
    target: 5
    bindings:
      google_play_games:
        remote_id: "CgkIn8HDjr8bEAIQAw"
        points: 5
      apple_game_center:
        remote_id: "parking_jam_3d_newbie_of_passing"
        points: 5
        
  Master_Of_Passing:
    title: "Master of Passing"
    description: "Unlock by passing 20 levels."
    type: single
    target: 20
    bindings:
      google_play_games:
        remote_id: "CgkIn8HDjr8bEAIQBA"
        points: 20
      apple_game_center:
        remote_id: "parking_jam_3d_master_of_passing"
        points: 20
```

#### 成就配置字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `title` | string | 成就标题 |
| `description` | string | 成就描述 |
| `type` | string | 成就类型：`single`（单次解锁） |
| `target` | int | 解锁目标值 |
| `bindings` | object | 平台绑定配置 |
| `bindings.google_play_games.remote_id` | string | Google Play Games 成就 ID |
| `bindings.google_play_games.points` | int | Google Play Games 成就积分 |
| `bindings.apple_game_center.remote_id` | string | Game Center 成就 ID |
| `bindings.apple_game_center.points` | int | Game Center 成就积分 |

---

## 三、代码实现

### 3.1 在 GuruRoot 中注册 GameServicePackage

文档示例要求在项目自定义的 `GuruRoot` 子类（如 `ParkingGameRoot`）中注册游戏服务包。

**当前项目需要确认**：你们是否有自定义的 `GuruRoot` 实现？如果没有，需要确认 Guru SDK 是否通过其他方式（如配置文件/自动初始化）注册 `GameServicePackage`。

```csharp
#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru;
using Guru.SDK.Framework.Core;
using Guru.SDK.Framework.GameService.Core.Package;
using Guru.SDK.Framework.Utils.Log;
using Guru.SDK.Framework.Utils.Package;

#if UNITY_ANDROID
using Guru.SDK.Framework.GameService.GooglePlayGames;
#elif UNITY_IOS
using Guru.SDK.Framework.GameService.AppleGameCenter;
#endif

namespace Game
{
    public class GameRoot : GuruRoot
    {
        public GameRoot(List<GuruPackage> children, int priority = 0) : base(children, priority)
        {
            var builder = new GameServicePackage.Builder();
            
#if UNITY_ANDROID && !UNITY_EDITOR
            builder.AddService(GooglePlayGamesServiceBuilder.Build());
#elif UNITY_IOS && !UNITY_EDITOR
            builder.AddService(AppleGameCenterServiceBuilder.Build());
#endif
            
            // 注册本地成就配置（由 Guru Spec 工具根据 guru_spec.yaml 自动生成）
            builder.UseLocalAchievements(Achievements.AllAchievements);
            children.Add(builder.Build());
        }
    }
}
```

### 3.2 获取 GameServicePackage

```csharp
using Guru.SDK.Framework.GameService.Core.Package;
using Guru.SDK.Framework.Core;

var gameServicePackage = GuruSdk.Instance.GuruRoot.ObtainPackage<GameServicePackage>();
```

### 3.3 检查游戏服务是否可用

```csharp
if (gameServicePackage.IsGameServiceReady())
{
    // 当前游戏服务可用
}
else
{
    // 当前游戏服务不可用（可能未登录或平台服务未初始化）
}
```

### 3.4 解锁成就

```csharp
// 获取成就定义（Achievements 类由 Guru Spec 工具自动生成）
var achievement = Achievements.GetAchievement("First_Steps");

if (achievement != null)
{
    bool success = await gameServicePackage.Unlock(achievement);
    if (success)
    {
        Debug.Log("Achievement unlocked!");
    }
}
```

### 3.5 递增增量型成就进度

```csharp
var achievement = Achievements.GetAchievement("Path_Finder");

// 增加 1 步进度
gameServicePackage.Increment(achievement, 1).Forget();
```

### 3.6 显示隐藏成就

```csharp
var achievement = Achievements.GetAchievement("First_Steps");
gameServicePackage.Reveal(achievement);
```

### 3.7 显示平台原生成就 UI

```csharp
// 弹出 Google Play Games / Game Center 内置成就列表
gameServicePackage.ShowUI().Forget();
```

### 3.8 加载成就列表

```csharp
// 加载所有成就记录（本地 + 平台）
var achievementItems = await gameServicePackage.LoadAchievements();
foreach (var item in achievementItems)
{
    Debug.Log($"{item.Title}: {item.Progress}/{item.Target} unlocked={item.Unlocked}");
}
```

### 3.9 重置成就（仅 iOS 支持）

```csharp
public void ResetAchievements()
{
#if UNITY_IOS
    Apple.GameKit.GKAchievement.ResetAchievements();
#endif
}
```

---

## 四、UI 集成

### 4.1 打开成就 UI

文档示例（注意：`AccountManager.Instance.CurrentUser` 在源码中不存在，应使用 `AccountDataStore.Instance.User`）：

```csharp
using Guru.SDK.Framework.Core.Account.Data;

public async UniTask OpenAchievementShowUIAsync()
{
    var user = AccountDataStore.Instance.User;
    if (user != null && user.IsValid)
    {
        // 已登录，直接显示成就 UI
        GuruSdk.Instance.GuruRoot.ObtainPackage<GameServicePackage>().ShowUI().Forget();
    }
    else
    {
        // 未登录，先尝试登录
        await AccountManager.Instance.LoginWith(AuthType.Game);
    }
}
```

### 4.2 调试界面集成（可选）

```csharp
// 打开成就 UI 按钮
private void OpenDebugAchievementShowUI()
{
    OpenAchievementShowUIAsync().Forget();
}

// 重置成就按钮（仅 iOS 有效）
private void ResetDebugAchievements()
{
#if UNITY_IOS
    Apple.GameKit.GKAchievement.ResetAchievements();
#endif
}
```

---

## 五、平台配置

### 5.1 Android - Google Play Console

1. 登录 [Google Play Console](https://play.google.com/console)
2. 选择应用
3. 导航至 **成长 → 成就**
4. 添加成就，确保 ID 与 `guru_spec.yaml` 中的 `google_play_games.remote_id` 一致

### 5.2 iOS - App Store Connect

1. 登录 [App Store Connect](https://appstoreconnect.apple.com/)
2. 选择应用
3. 导航至 **功能 → Game Center**
4. 添加成就，确保 ID 与 `guru_spec.yaml` 中的 `apple_game_center.remote_id` 一致

---

## 六、添加新成就流程

1. **在平台后台创建成就**
   - Google Play Console 和 App Store Connect 分别创建
   - 记录平台分配的 remote_id
2. **更新 `guru_spec.yaml`**
   - 在 `achievements` 节点下添加新成就定义
3. **重新生成代码**
   - 通过 Guru 的代码生成工具重新生成 `GuruAllSpecs.G.cs`，使 `Achievements` 类包含新成就

---

## 七、API 参考

### 核心类

| 类名 | 命名空间 | 说明 |
|------|----------|------|
| `GameServicePackage` | `Guru.SDK.Framework.GameService.Core.Package` | 游戏服务包，提供成就功能 |
| `Achievements` | 自动生成 | 成就定义类，包含所有成就配置 |
| `Achievement` | `Guru.SDK.Framework.GameService.Core.Achievement` | 单个成就对象 |

### 常用方法

```csharp
// 获取 GameServicePackage
var gameService = GuruSdk.Instance.GuruRoot.ObtainPackage<GameServicePackage>();

// 显示平台成就 UI
gameService.ShowUI().Forget();

// 解锁成就
var achievement = Achievements.GetAchievement("achievement_id");
gameService.Unlock(achievement).Forget();

// 递增进度
gameService.Increment(achievement, 1).Forget();

// 获取所有成就定义
var allAchievements = Achievements.AllAchievements;
```

---

## 八、注意事项

1. **登录依赖**：成就功能依赖游戏登录，确保用户已登录后再调用成就相关 API
2. **网络要求**：解锁成就需要网络连接，离线时成就会缓存并在恢复网络后同步
3. **平台差异**：
   - Android 成就 UI 由 Google Play Games 提供
   - iOS 成就 UI 由 Game Center 提供
4. **重复解锁**：重复调用 `Unlock` 不会产生错误，SDK 会自动处理
5. **测试环境**：
   - Android：使用 Google Play Console 的测试账号
   - iOS：使用 App Store Connect 的 Sandbox 账号
6. **成就发布**：
   - Android 成就需要发布后才能正常解锁
   - iOS 成就需要与提审版本一起提审

---

## 九、接入检查清单（针对当前项目）

| 检查项 | 状态 | 备注 |
|--------|------|------|
| 游戏登录已接入 | ✅ | `auth_game` 已配置，委托类已注册 |
| `guru_sdk.yaml` 添加 `google_play_games` | ✅ | 已存在 |
| `guru_sdk.yaml` 添加 `apple_game_center` | ✅ | 已存在 |
| `guru_spec.yaml` 添加 `achievements` 节点 | ✅ | 已配置 21 个 Apple Game Center binding |
| 平台后台已创建成就并获取 remote_id | ❓ | 需确认中台/运营是否已配置 |
| GuruRoot 中注册 `GameServicePackage` | ✅ | `GameRoot` 已注册平台 provider 和本地成就 |
| iOS 构建设置 min version >= 16.0 | ✅ | ProjectSettings 与 BuildToolV2 均为 16.0 |

---

## 2026-07-29：iOS 成就按钮按 Game Center 登录状态显示

### 需求与原行为

- 需求：iOS 玩家未登录 Game Center 时隐藏关卡选择界面的 `GpsBtn`。
- 原行为：`GpsBtn` 仅受 RemoteConfig `open_gaps` 控制；未登录玩家仍能看到按钮，点击后会尝试登录。

### 修改范围与新行为

- 修改文件：`Assets/Game/UI/LevelChoicePanel/Scripts/ChoiceLevelPanel.cs`。
- 影响系统：关卡选择 UI、Game Center 认证状态、成就原生 UI 入口。
- iOS 真机仅在 `open_gaps=true` 且 `AppleGameCenterAuthAgent.IsAuthenticated=true` 时显示 `GpsBtn`。
- 每次 `ChoiceLevelPanel.OnOpen()` 读取一次 Game Center 认证状态并刷新按钮，不持续监听登录状态。
- 点击时再次检查认证状态，处理显示后退出登录的竞态；未认证时隐藏按钮并停止，不主动拉起登录。
- Android 与 Editor 保持原行为：按钮仅受 `open_gaps` 控制，未登录时点击仍走既有登录流程。
- 本次不修改 prefab、RemoteConfig、存档、成就配置、事件埋点或平台后台配置。

### 验证、风险与回滚

- `git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 个错误，99 个项目既有警告。
- 尚未执行 iOS Sandbox 真机登录、退出登录和重新打开面板的显隐验证。
- 面板保持打开期间若 Game Center 登录状态变化，按钮显隐会在下次 `OnOpen()` 更新；点击前校验仍会阻止未认证状态打开成就 UI。
- 回滚时移除 `ChoiceLevelPanel.OnOpen()` 的 Game Center 显隐刷新、点击前认证保护和 `RefreshGpsButtonVisibility()`，恢复在 `InitButtons()` 中仅按 `GetOpenGaps()` 设置按钮显隐。

## 2026-08-27：主界面与选关界面实时响应 Game Center 认证状态

### 需求、基线与修改范围

- 需求：`StartPanel.GasBtn` 与 `ChoiceLevelPanel.GpsBtn` 在面板保持打开期间，应在 Game Center 登录状态变化后正确刷新显隐。
- 修改前两个面板只在 `OnOpen()` 读取认证状态；登录成功、退出或认证失败发生在当前打开周期内时，按钮会保持旧状态。
- 修改文件：`Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`、
  `Assets/Game/UI/LevelChoicePanel/Scripts/ChoiceLevelPanel.cs` 及对应本地说明文档。
- 复用 `AppleGameCenterAuthAgent.AuthStateUpdated`，没有修改 SDK 包、prefab、RemoteConfig、Luban、存档、资源、全局事件、analytics 或平台后台配置。

### 当前 UI 与平台流程

1. 两个面板在 `OnOpen()` 重建各自的 R3 订阅，并继续主动调用原显隐方法处理初始状态。
2. `AuthStateUpdated` 发布有效状态后，面板回调各自现有刷新方法；iOS 真机仍要求
   `GetOpenGaps()==true && IsAuthenticated==true` 才显示入口。
3. `OnClose()` 释放订阅；对象销毁也有清理兜底，避免已关闭或已销毁面板继续响应认证变化。再次打开会重新订阅，不使用逐帧轮询。
4. Android 与 Editor 不订阅 Apple 状态，继续只由 `open_gaps` 控制显隐；未登录点击后通过
   `GameAccountUtils.LoginWithGame(true)` 登录并打开原生成就 UI 的既有流程不变。

### 验证、风险与回滚

- 已静态确认两个订阅仅在 `UNITY_IOS && !UNITY_EDITOR` 编译，过滤 `BehaviorSubject` 的初始空状态，并在关闭/销毁路径释放。
- `dotnet build Assembly-CSharp.csproj` 的 Android Editor 常量编译通过：`0 error / 1082 warning`；使用 iOS Player 常量并隔离无关 Editor 项目后，
  `Guru.UniKit.Apple.GameKit.csproj` 为 `0 warning / 0 error`，`Assembly-CSharp.csproj` 为 `124 warning / 0 error`。
- `git diff --check` 对本次两份 C# 与两份 Markdown 通过。
- `uncertain`：尚未在 iOS Sandbox 真机验证当前面板打开时的登录成功、退出、认证错误、切后台恢复及两个面板反复切换；Android/Editor PlayMode 回归和横屏触控也未执行。
- SDK 认证由现有账号初始化流程触发；本次监听只消费状态，不主动弹出登录 UI，也不改变认证超时、网络恢复或账号绑定策略。
- 回滚时移除两个面板的认证订阅字段、订阅/释放方法及生命周期调用，保留原有 `OnOpen()` 和点击前显隐校验；无需数据或资源迁移。

*最后更新：2026-08-27*
