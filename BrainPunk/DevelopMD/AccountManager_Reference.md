# Guru SDK AccountManager 生命周期与使用指南

> 基于 `com.guru.sdk.framework.core` 包源码分析整理，供 Account（游戏登录）模块接入参考。

---

## 一、生命周期：谁初始化、何时初始化

`AccountManager` 采用**单例模式**，其完整生命周期由 **Guru SDK 内部托管**，游戏业务层**不需要手动初始化**。

### 1.1 `Prepare()` — 同步准备阶段

- **调用方**：`GuruSdk.DispatchInitializeSync()`
- **源码位置**：`Runtime/GuruSdk.cs:254`
- **执行时机**：SDK 启动流程中，在 `RemoteConfigManager`、`GuruAnalytics`、`AdsManager` 之后
- **核心职责**：
  1. 读取 `IGuruSdkProtocol.AccountAuthDelegate`（即你在 `GameProtocol` 中注册的 `GameSDKAccountManager`）
  2. 构建认证委托列表：默认包含 `AnonymousAuthCredentialDelegate`，再加上你返回的委托列表（如 `GamesCredentialDelegate`）
  3. 初始化 `AuthCredentialManager`

```csharp
// GuruSdk.cs 内部调用
await AccountManager.Prepare();
```

### 1.2 `Initialize()` — 异步恢复阶段

- **调用方**：`GuruSdk.Initializer.InitializeAccount()`
- **源码位置**：`Runtime/GuruSdk.Initializer.cs:89`
- **执行时机**：`Prepare()` 完成后，在 `DispatchInitializeAsync()` 中被调用
- **核心职责**：
  1. 等待所有 pending preparations
  2. 从本地存储加载历史账号，执行 **`RestoreAccount()`**
  3. 若支持游戏登录，自动执行 **`CheckAndBindGaming()`** 尝试静默绑定
  4. 启动各类监听（FCM Token、网络状态、用户身份变化、GuruUser 变化）

```csharp
// GuruSdk.Initializer.cs 内部调用
private void InitializeAccount()
{
    AccountManager.Instance.Initialize().Forget();
}
```

### 1.3 对业务层的结论

- ✅ **无需在游戏代码中手动调用** `AccountManager.Prepare()` 或 `Initialize()`
- ✅ 你唯一需要做的，是在 `GameProtocol` 里正确绑定 `IAccountAuthDelegate`（已完成）
- ✅ 之后在任何地方直接使用 `AccountManager.Instance.xxx` 即可

---

## 二、核心公共 API

| API | 签名 | 说明 |
|-----|------|------|
| **Instance** | `public static AccountManager Instance { get; }` | 单例访问点 |
| **LoginWith** | `public async UniTask<bool> LoginWith(AuthType authType)` | 主动触发登录（如 `AuthType.Game`） |
| **LogoutAsync** | `public async UniTask<GuruUser?> LogoutAsync(bool switching = false, HashSet<AuthType>? authTypes = null)` | 登出或解绑指定凭证；若全部登出后会自动创建匿名账号 |
| **IsPreparationCompleted** | `public static bool IsPreparationCompleted { get; }` | 账号模块是否已完成 `Prepare` |
| **ObservablePreparationCompleted** | `public static Observable<bool> ObservablePreparationCompleted { get; }` | 可订阅的 Prepare 完成事件（R3） |

---

## 三、获取当前登录用户

### ⚠️ 注意：接入文档与源码存在差异

接入文档示例写的是：

```csharp
// 文档示例（源码中不存在该属性）
var user = AccountManager.Instance.CurrentUser;
```

实际上，`AccountManager` **并未暴露** `CurrentUser` 或 `User` 属性。

### 正确写法

```csharp
using Guru.SDK.Framework.Core.Account.Data;

var user = AccountDataStore.Instance.User;
if (user != null && user.IsValid)
{
    Debug.Log($"User is logged in: {user.Uid}");
}
else
{
    Debug.Log("User is not logged in");
}
```

`AccountDataStore.Instance` 也是单例，与 `AccountManager` 共享同一份内存状态。

---

## 四、自动恢复登录逻辑（Initialize 内部行为）

当 `AccountManager.Initialize()` 执行时，会自动尝试恢复上一次的有效登录态，无需业务层干预：

### 4.1 `RestoreAccount()` 流程

1. **尝试恢复 Gaming 登录**
   - 调用 `IsSupportGameLogin()` 判断：
     - iOS 平台 **或** `GuruSdk.Instance.Details.GameServiceClientId` 不为空
   - 若条件满足且本地有 Game 凭证，自动静默登录 Game Center / Google Play Games
2. **Gaming 恢复失败 → 恢复匿名登录**
   - 自动生成匿名凭证并请求 Guru 服务器创建/恢复匿名账号
3. **恢复成功后**
   - 绑定凭证到本地存储
   - 回调你的 `GameSDKAccountManager.OnLogin()` 或 `OnAnonymousLogin()`
   - 启动 Firebase 认证与设备上报

### 4.2 `CheckAndBindGaming()` 流程

在 `InitializeInternal` 成功后，会额外调用：

```csharp
await CheckAndBindGaming();
```

逻辑如下：
- 判断是否支持游戏登录
- 检查 `BindGameCredentialStrategy` 是否发生变化（如从 `ignore` 改为其他值）
- 检查 Game 凭证是否失效或不存在
- **若满足条件，自动调用 `AccountManager.Instance.LoginWith(AuthType.Game)`**

> 这也是文档要求 `bind_game_credential_strategy: ignore` 的原因——避免在不需要绑定游戏账号的场景下自动触发登录弹窗。

---

## 五、业务层调用建议

### 5.1 主动触发游戏登录

```csharp
using Guru.SDK.Framework.Core.Account;
using Guru.SDK.Framework.Utils.Auth;

// 在需要登录的时机调用（如点击"登录"按钮、进入主界面时）
public async UniTask<bool> LoginWithGame()
{
    if (AuthCredentialManager.Instance.IsLogin(authType: AuthType.Game))
    {
        Debug.Log("Already logged in with Game");
        return true;
    }

    var success = await AccountManager.Instance.LoginWith(AuthType.Game);
    Debug.Log($"Game login result: {success}");
    return success;
}
```

### 5.2 登出游戏账号

```csharp
var logoutUser = await AccountManager.Instance.LogoutAsync(
    switching: false,
    authTypes: new HashSet<AuthType> { AuthType.Game }
);

if (logoutUser != null)
{
    Debug.Log("Game logout success");
}
```

> 注意：登出后若当前无其他凭证，`AccountManager` **会自动创建匿名账号并登录**。

### 5.3 检查登录状态

```csharp
var user = AccountDataStore.Instance.User;
if (user != null && user.IsValid)
{
    Debug.Log($"Current user: {user.Uid}");
}
```

---

## 六、关键接口回调说明

你实现的 `GameSDKAccountManager : IAccountAuthDelegate` 会在以下时机被 AccountManager 内部回调：

| 回调 | 触发时机 | 典型用途 |
|------|---------|---------|
| `OnLogin` | 非匿名账号登录成功 | 加载玩家远程数据、同步存档 |
| `OnAnonymousLogin` | 匿名账号登录/恢复成功 | 初始化本地游客数据 |
| `OnLogout` | 账号整体登出成功 | 清理玩家数据、返回登录页 |
| `OnAnonymousLogout` | 匿名账号被登出 | 清理本地游客数据 |
| `OnConflict` | 账号冲突（409）且需要用户手动确认切换 | 弹出切换确认弹窗 |
| `OnAutoTransition` | 账号冲突但可自动静默过渡 | 执行无感切换前的数据准备 |
| `OnSwitch` | 账号切换完成后 | 刷新 UI、重新加载玩家数据 |
| `OnAccountDeleted` | 账号被删除后 | 清理所有本地数据 |

---

## 七、参考源码路径

```
Packages/com.guru.sdk.framework.core/Runtime/
├── Account/
│   ├── AccountManager.cs              # 主类：Prepare / Initialize / LoginWith / LogoutAsync
│   ├── AccountManager.Service.cs      # 恢复账号、冲突处理、设备上报、切换账号
│   ├── AccountManager.Dispatcher.cs   # 委托回调转发（InvokeLogin / InvokeLogout 等）
│   ├── AccountManager.Gaming.cs       # IsSupportGameLogin / CheckAndBindGaming
│   ├── AccountManager.Firebase.cs     # Firebase Token 刷新与认证
│   └── Data/AccountDataStore.cs       # 用户状态存储中心
├── GuruSdk.cs                         # SDK 主入口，调用 AccountManager.Prepare()
└── GuruSdk.Initializer.cs             # SDK 初始化器，调用 AccountManager.Instance.Initialize()
```

---

*文档生成时间：2026-04-16*
