# Guide DOText 的 TMP 强制刷新说明

## 需求背景

2026-07-24，`NormalGuidePopup` 与 `GuideDialogue` 的逐字文案在调用 `DOText` 前需要参考
`GameVictoryPanel.RefreshTips()`，先让 TMP 按完整本地化文本刷新字体和网格，再清空文本并开始逐字动画。
该处理用于减少动态字体、回退字体和首帧网格信息未及时更新时的显示偏差。

## 影响文件与系统

- `Assets/Game/Scripts/Guide/Scripts/NormalGuidePopup/NormalGuidePopup.cs`
  - `PlayTips(...)` 在 `ContentTxt.DOText(...)` 前补充 TMP 刷新。
- `Assets/Game/Scripts/Guide/Scripts/GuideDialogue/GuideDialogue.cs`
  - `PlayTip()` 在 `UiYindao201Txt.DOText(...)` 前补充相同刷新。
- 参考实现：`Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs` 的 `RefreshTips()`。
- 未修改 prefab、本地化配置、字体资源、DOTween 参数、Bear UI 生命周期、存档或平台代码。

## 原有与修改后行为

- 原有行为：两个引导弹窗取得本地化文本后直接把 TMP 文本清空，再启动 `DOText`。
- 修改后行为：仅在确实进入 `DOText` 分支时，先写入完整目标文本，依次调用
  `UpdateFontAsset()`、`ForceMeshUpdate(true, true)`，再清空文本并启动原有 `DOText`。
- `GuideDialogue` 的零时长或空文本分支仍直接写入最终文本，不额外执行逐字动画刷新链。
- `NormalGuidePopup` 的提示点击锁、请求版本、Tween 等待、箭头显示和 Spine 表现保持原逻辑。

## 数据、资源、UI 与事件流

- UI 文本流：本地化文本 → TMP 字体/网格强制刷新 → 清空 → `DOText` 逐字显示。
- 不新增资源加载、字体资源、实例化、全局事件、每帧轮询或公开 API。
- 不访问 DB、Save、RemoteConfig、analytics、广告、IAP 或平台 SDK。
- Android 与 iOS 共用同一 TMP/DOTween 运行时路径，没有新增平台分支。

## 验证记录

- `git diff --check` 已通过，两处代码只新增与 `GameVictoryPanel` 同构的 TMP 刷新顺序。
- 静态扫描未发现目标脚本新增 `OnValidate`、`Reset`、`Update`、`ExecuteAlways`、
  `ExecuteInEditMode`、全局事件、`PlayerPrefs` 或实例化入口。
- 首次 `dotnet build Assembly-CSharp.csproj` 未进入目标代码编译，因
  `Temp/bin/Debug` 缺少 77 个 Unity/插件临时程序集元数据而失败。
- 允许项目引用重建的复跑已生成晚于两份目标源码的 `Temp/bin/Debug/Assembly-CSharp.dll`，
  但进程持续超过两分钟未完整退出，已精确终止本次构建进程。
- 临时项目引用产物补齐后，最终使用
  `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`
  复跑通过，结果为 0 个错误、99 个项目既有警告。

## 剩余风险与回滚

- `uncertain`：尚未在 Unity PlayMode 验证中日韩等回退字体、快速推进、Tween Kill/重开和实际首帧排版。
- 尚未在 Android/iOS 横屏设备验证字体回退、网格刷新开销和逐字动画表现。
- Unity Editor 内的实际脚本重载与 PlayMode 交互仍需补验。
- 回滚时删除两个 `DOText` 前新增的完整文本赋值、`UpdateFontAsset()` 和
  `ForceMeshUpdate(true, true)` 三行即可；Tween、资源、数据和调用方无需回滚。
