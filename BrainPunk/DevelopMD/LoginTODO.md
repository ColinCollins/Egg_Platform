# Account (游戏登录) 模块接入 TODO

> 基于 Guru SDK 游戏登录接入文档分析，以下列出当前项目仍待解决的接入事项。

---

## 🔴 高优先级（阻塞接入）

### 1. `game_service_client_id` 缺失
- **现状**：`Assets/Guru/guru_spec.yaml` 的 `details` 部分未配置 `game_service_client_id`
- **文档要求**：需在 `guru_spec.yaml` 中添加 Google Play Games 的 Web Application Client ID
- **行动**：联系前端中台获取该项目的 `game_service_client_id`
- **参考配置**：
  ```yaml
  details:
    # ... 其他配置
    bind_game_credential_strategy: ignore
    game_service_client_id: "487053574982-xxxxxxxxxxxxxxxxxxxxxxx.apps.googleusercontent.com"
  ```

### 2. `game-info.json` 仍为占位符
- **现状**：`Assets/GooglePlayGames/Resources/game-info.json` 中的值全是占位符
  ```json
  {
      "ApplicationId": "__APP_ID__",
      "IosClientId": "__IOS_CLIENTID__",
      "WebClientId": "__WEB_CLIENTID__",
      "NearbyConnectionServiceId": "__NEARBY_SERVICE_ID__"
  }
  ```
- **行动**：确认真实值来源（中台提供 / Google Play Console / Firebase 下载），替换占位符

### 3. `GPGSIds.cs` 文件缺失
- **现状**：文档 5.1.1 明确要求 `Assets/GooglePlayGames/GPGSIds.cs`，但项目中只有 `Plugins` 和 `Resources` 目录
- **疑问**：新版 Google Play Games Plugin 是否已弃用该文件？还是接入时漏掉了？
- **行动**：
  1. 确认当前使用的 Google Play Games Plugin 版本
  2. 若仍需该文件，确认生成方式（手动创建 / 通过插件菜单生成）

---

## 🟡 中优先级（可能导致构建/运行异常）

### 4. Android Manifest 具体修改内容不明确
- **现状**：文档 5.1.3 只写了"确保 AndroidManifest.xml 包含必要的权限和配置"，未给出具体 XML
- **现状**：项目中已有 `Assets/GooglePlayGames/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml`
- **疑问**：该文件是否已满足需求？是否需要额外添加 `INTERNET`、`GET_ACCOUNTS` 等权限？
- **行动**：对比文档的完整示例（如有），确认 Manifest 是否完整

---

## 🟢 低优先级（细节待补全）

### 5. iOS Info.plist 具体键值不明确
- **现状**：文档 5.2.2 只写"确保 Info.plist 包含 Game Center 相关配置"
- **疑问**：是否需要手动添加 `UIRequiredDeviceCapabilities` = `gamekit`？Guru 构建工具是否会自动注入？
- **行动**：构建一次 iOS，检查生成的 Xcode 工程中 Info.plist 的 Game Center 配置是否完整

### 6. iOS Game Center 未登录时的用户引导流程
- **现状**：文档示例代码的 iOS 分支仅打印 Log：
  ```csharp
  Log.D("You are not logged in yet. Please exit the game, log in to Game Center and try again", "TCAccount");
  ```
- **疑问**：是否需要弹出系统提示或引导 UI？Guru SDK 是否提供了标准的未登录处理 API？
- **行动**：查看 `Guru.SDK.Framework.Auth.Games` 或 `Guru.SDK.Framework.Core.Account` 中是否有相关引导接口

---

## ✅ 已确认完成的事项

| 事项 | 状态 |
|------|------|
| `guru_sdk.yaml` 已添加 `auth_game` | ✅ |
| `guru_spec.yaml` 已配置 `bind_game_credential_strategy: ignore` | ✅ |
| `Assets/Guru/link.xml` 已包含 Apple 程序集保留（经确认仍然需要） | ✅ |
| 创建 `GameSDKAccountManager` 并实现 `IAccountAuthDelegate` | ✅ |
| `GameProtocol.AccountAuthDelegate` 已绑定到 `GameSDKAccountManager.Instance` | ✅ |
| `AccountManager` 自动初始化（无需游戏层手动挂载） | ✅ |
| `LoadManager.OnPostProcess` 已加入游戏登录自动检测 | ✅ |

---

## 📋 建议推进顺序

1. **先找中台确认**：`game_service_client_id`、`game-info.json` 真实值、`GPGSIds.cs` 是否还需要
2. **填充 `game-info.json`** 并检查 Android Manifest
3. **打测试包验证**：Android 测试 Google Play Games 登录，iOS 测试 Game Center 登录

---

*最后更新：2026-04-16*
