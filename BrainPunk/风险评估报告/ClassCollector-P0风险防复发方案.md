# ClassCollector P0 风险防复发方案

## 背景

昨天的 `清理高风险代码 classCollector` 应按 P0 处理，不是因为 `ClassCollector` 这个名字本身危险，而是它处在运行时类型保留、`link.xml`、IL2CPP/Managed Stripping、关卡 prefab 脚本绑定、构建前处理的交界处。这个位置一旦缺少门禁，问题通常不会在编辑器里暴露，而是在真机包或线上流程中表现为组件缺失、回调缺失、关卡异常。

## 我建议的三层防线

### 1. 日报提醒层：P0 哨兵

- 每日风险评估前先执行 `tools/Check-P0RiskGuard.ps1`。
- 扫描最近三天 commit 与当前未提交变更。
- 命中以下路径直接生成 P0/P1 提醒报告：
  - `Assets/Editor/ClassCollectorEditorWindow.cs`
  - `Assets/Game/Scripts/Generated/ClassCollector.cs`
  - `Assets/link.xml`
  - `Assets/Editor/LevelPrefabStripValidator.cs`
  - `Assets/Editor/BuildPipline/`
  - `ProjectSettings/ProjectSettings.asset`
  - `Assets/Game/Scripts/Play/PlayCtrl.cs`
  - `Assets/Game/Scripts/SDK/Services/`
- 报告格式兼容 `tools/Send-AdsRiskReport.ps1`，可以直接发飞书。

推荐执行：

```powershell
powershell -ExecutionPolicy Bypass -File D:\GitStore\BrinPunk\BrainPunk\tools\Check-P0RiskGuard.ps1 -Days 3
```

需要直接提醒飞书时：

```powershell
powershell -ExecutionPolicy Bypass -File D:\GitStore\BrinPunk\BrainPunk\tools\Check-P0RiskGuard.ps1 -Days 3 -SendFeishu
```

### 2. 构建阻断层：Build 前必须验证

当前 `BPGameCustomBuildDelegate.OnBeforeBuildStart()` 已经调用 `ClassCollectorEditorWindow.TryRunAutoCollection()`，这是好方向，但还不够。建议把 `LevelPrefabStripValidator` 改成可返回失败结果的无弹窗 API，然后在构建前强制执行：

- ClassCollector 自动收集失败：直接 fail build。
- `link.xml` 缺失或解析失败：直接 fail build。
- 关卡 prefab 上存在 Assembly-CSharp 脚本未被 `link.xml` 或 `[Preserve]` 保留：直接 fail build。
- `AssetDatabase.Refresh()` 后编译超时：直接 fail build。

这层的目标很简单：可以日报提醒，但绝不能让 P0 级工程链问题打进包。

### 3. Code Review 规则层：高危路径自动升级

只要 PR/commit 触及下面任一类路径，风险等级默认从 P1 起步，命中删除/裁剪/构建前处理则升级 P0：

- `Assets/link.xml`
- `Assets/Editor/ClassCollectorEditorWindow.cs`
- `Assets/Game/Scripts/Generated/`
- `Assets/Editor/BuildPipline/`
- `Assets/Editor/LevelPrefabStripValidator.cs`
- `ProjectSettings/ProjectSettings.asset`
- `Assets/Resources/GameSetting.asset`
- `Assets/Game/Scripts/Play/PlayCtrl.cs`
- `Assets/Game/UI/Global/Scripts/EnterLevelLoading.cs`
- `Assets/Game/Scripts/Runtime/Ads/`
- `Assets/Game/Scripts/SDK/Services/`

Review 时必须回答三件事：

- 这个改动是否影响 IL2CPP 裁剪或运行时反射/动态加载？
- 是否跑过 `ClassCollector`、`LevelPrefabStripValidator` 和至少一次目标平台构建？
- 是否会影响关卡 prefab 上的脚本绑定、构建前后处理顺序或资源加载状态？

## 最小落地版本

我建议先落地这三个动作：

1. 每日自动化先跑 `tools/Check-P0RiskGuard.ps1 -Days 3 -SendFeishu`。
2. 把 `LevelPrefabStripValidator` 改出一个 `ValidateOrThrow()`，接入 `BPGameCustomBuildDelegate.OnBeforeBuildStart()`。
3. 在日报扫描规则中加入“高危工程链命中则必须单独列 P0/P1 哨兵段落”。

这样不会一次性改太多系统，但能立刻防住昨天这类风险再次安静滑过去。

