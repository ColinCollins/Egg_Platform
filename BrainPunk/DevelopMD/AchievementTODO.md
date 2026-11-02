# Achievement (游戏成就) 模块接入 TODO

> 基于 Guru SDK Game 成就接入文档分析，以下列出当前项目仍待解决的接入事项。

---

## 🔴 高优先级（阻塞接入）

### 1. `guru_spec.yaml` 未配置 `achievements` 节点
- **现状**：`Assets/Guru/guru_spec.yaml` 中没有 `achievements` 配置
- **文档要求**：需在 `guru_spec.yaml` 中定义成就列表，并绑定各平台的 `remote_id`
- **行动**：联系前端中台/运营获取该项目的成就配置（包括 Google Play Games ID 和 Game Center ID）
- **参考配置**：
  ```yaml
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
  ```

### 2. 平台后台成就 remote_id 待确认
- **现状**：文档中列出了示例成就 ID，但不确定是否已针对 BrainPunk 项目在后台创建
- **行动**：
  1. 确认 Google Play Console 中是否已创建成就并获取 remote_id
  2. 确认 App Store Connect 中是否已创建成就并获取 remote_id

---

## 🟡 中优先级（可能导致构建/运行异常）

### 3. iOS 构建 Minimum Version 要求 16.0
- **现状**：成就文档要求 iOS `minimum iOS Version = 16.0`，当前项目构建参数未显式设置
- **行动**：
  1. 确认 `AppBuildParam.Build()` 是否支持 `iOSTargetVersion` 参数
  2. 在 `BuildToolV2.cs` 的 `BuildIOS()` 中设置目标版本为 `"16.0"`

---

## 🟢 低优先级（细节待补全）

### 4. 成就业务层工具类未创建
- **现状**：`Assets/Game/Scripts/SDK/Services/Account/` 下只有登录相关工具，无成就在业务层的封装
- **行动**：创建 `GameAchievementUtils.cs`，封装常用操作：
  - `UnlockAchievement(string achievementId)`
  - `IncrementAchievement(string achievementId, int steps)`
  - `ShowAchievementUI()`
  - `LoadAchievements()`
  - `IsGameServiceReady()`

### 5. 成就调用时机待确定
- **现状**：文档给出了解锁成就的 API 示例，但项目中尚未在关卡通关等节点接入
- **行动**：
  1. 确认项目中有哪些成就需要接入（如通关 5 关、通关 20 关等）
  2. 在对应的游戏逻辑节点（如 `LevelComplete`、`GameWin`）插入成就解锁调用

---

## ✅ 已确认完成的事项

| 事项 | 状态 |
|------|------|
| `guru_sdk.yaml` 已添加 `google_play_games` | ✅ |
| `guru_sdk.yaml` 已添加 `apple_game_center` | ✅ |
| `GameRoot` 中已注册 `GameServicePackage` | ✅ |
| 登录系统 `AccountManager` 已自动初始化 | ✅ |

---

## 📋 建议推进顺序

1. **找中台/运营确认**：成就列表、平台 remote_id、`guru_spec.yaml` 配置
2. **填充 `guru_spec.yaml`** 并重新生成 `GuruAllSpecs.G.cs`
3. **确认 iOS 构建版本**是否已支持并设置 16.0
4. **创建 `GameAchievementUtils.cs`** 封装成就调用
5. **在关卡通关等节点接入**成就解锁逻辑
6. **打测试包验证**：Android 和 iOS 分别测试成就解锁与 UI 展示

---

*最后更新：2026-04-16*
