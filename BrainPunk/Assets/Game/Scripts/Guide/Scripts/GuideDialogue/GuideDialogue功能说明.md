# GuideDialogue 功能说明

## 需求背景

2026-07-16 参考 `NormalGuidePopup` 实现提示剧情对话。按用户确认的边界，`GuideDialogue` 每次通过 Enum 选择独立 Prefab Variant，不假设 UI 实例复用；表现层只负责文案播放、点击推进和关闭通知。

## 影响文件与系统

- `GuideDialogue.cs`：定义三个实际 Variant，播放 Variant 自带的 `tipsKey`，提供 `Create(...)` 与流程所需的 `ShowAsync(...)`。
- `GuideDialogue.prefab`：作为 Variant 基础资源，绑定现有 `clickArea`，新增 `tipsKey` 和 `textAnimDuration`。
- `GameTipsGuideFlow.cs`：开场、免费说明、收尾分别使用对应枚举，不再在流程代码中保存文案。
- 复用 Bear UI、CustomButton、I2 Localization、DOTween、UniTask 和 `GuideFlowResult`；不修改 DB、配置或全局事件。

## 原有行为基线

- 初始 `GuideDialogue` 只有空生命周期和固定资源名 `GuideDialogue` 的无参 `Create()`。
- 后续工作区曾加入动态文案、实例复用判断和生命周期版本；这些设计与“Enum 对应独立 Variant”的真实用法冲突，最终实现已移除。

## 当前行为

- `GuideDialogueVariant` 当前只有实际调用需要的三项：`GameTipsOpening=10001`、`GameTipsFree=10002`、`GameTipsClosing=10003`。
- `Create(variant, callback)` 每次按枚举加载 `GuideDialogue_<枚举值>`，不查询或返回其它已打开实例。
- 文案读取目标 Variant 序列化的 `tipsKey`，I2 翻译缺失时显示原字符串；逐字播放期间禁止推进。
- 最后一条点击后销毁当前 UI；`OnClose()` 清理按钮和 Tween，并消费一次关闭回调。
- `ShowAsync(variant, token)` 仅用于现有 `GameTipsGuideFlow` 顺序等待；取消时销毁本次对话并返回 `Cancelled`。
- 不使用 `lifecycleVersion`；当前没有跨关闭生命周期继续运行的异步文案任务。

## 数据、资源与 UI 流

- 数据：不读写 DB、Save、背包、RemoteConfig 或 PlayerPrefs。
- 资源：运行时需要 `GuideDialogue_10001`、`GuideDialogue_10002`、`GuideDialogue_10003` 三个 Prefab Variant。
- UI：Flow 传入枚举 → Bear UI 打开对应 Variant → 读取 `tipsKey` → DOTween 显示 → 点击推进 → 关闭回调唤醒 Flow。
- 平台：Android/iOS 共用同一路径，没有每帧轮询或平台分支。

## 验证与剩余风险

- 静态检查确认动态文案参数、复用判断和 `lifecycleVersion` 已删除，三个调用点均改为枚举。
- 基础 Prefab 已绑定 `tipsKey`、`btn` 与 `textAnimDuration`；敏感项扫描未命中编辑器生命周期、全局事件或 DB/save。
- 已创建 `GuideDialogue_10001`、`GuideDialogue_10002`、`GuideDialogue_10003` 三个 Prefab Variant；均继承基础 Prefab，只覆盖根名称和各自 `tipsKey`。
- Unity 已检测到三个 Variant 文件，尚未在 Inspector/PlayMode 实际打开确认继承显示、逐条推进和取消路径。
- `dotnet build Assembly-CSharp.csproj --no-restore ...` 通过：0 个错误、98 个项目既有警告；Unity Inspector/PlayMode 资源加载仍待验证。
- 命令行工程构建仍可能受当前 `Temp/bin/Debug` 缺失元数据 DLL 阻断。

## 回滚路径

恢复原空脚本与固定 `GuideDialogue` 资源名，删除三个 Variant 和基础 Prefab 的 `tipsKey`、`btn`、`textAnimDuration` 字段，并把 `GameTipsGuideFlow` 的三个枚举调用恢复为旧实现即可；不需要迁移数据。
