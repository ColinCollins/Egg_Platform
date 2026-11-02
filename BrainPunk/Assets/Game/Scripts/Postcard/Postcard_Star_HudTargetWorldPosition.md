# Postcard_Star HUD 目标坐标转换记录

## 需求背景

`Postcard_Star` 收集动画使用场景内 `Transform.DOMove(...)` 播放飞行动画，终点必须是场景世界坐标。`GamePlayPanel.TryGetStarBlockWorldPosition(...)` 当前返回 HUD `starBlock.transform.position`，在 Screen Space Canvas 下该值需要先解析为屏幕坐标，再投影到场景相机下的星星飞行平面。

## 影响范围

- 修改脚本：`Assets/Game/Scripts/Postcard/Postcard_Star.cs`
- 读取系统：`PlayCtrl.Instance.CurrentGamePlayPanel`、`PlayCtrl.Instance.LevelCtrl`、`GamePlayPanel.TryGetStarBlockWorldPosition(...)`
- 使用组件：当前面板父级 `Canvas`、关卡根节点下 `GameCamera` 或 `Camera.main`
- 本次未修改 `GamePlayPanel` 公共 API、`PostcardGetStarEvent`、奖励发放、`DB.SavePostcard()`、prefab、Luban 配置、RemoteConfig、资源加载、广告、IAP、analytics 或平台 SDK。

## 原有行为基线

- `PlayCollectFeedbackAsync()` 会把星星视觉根节点直接 `DOMove` 到 `TryGetHudTargetPosition(...)` 返回的位置。
- `TryGetHudTargetPosition(...)` 直接把 `GamePlayPanel.TryGetStarBlockWorldPosition(...)` 的返回值作为 `DOMove` 世界坐标使用。
- 当 HUD 坐标来自 Screen Space Canvas 时，场景星星可能飞向错误的世界位置。

## 当前行为

- `TryGetHudTargetPosition(...)` 现在接收当前飞行动画的 `visualRoot`，先获取 HUD 星星块位置，再根据面板 Canvas 渲染模式解析 HUD 屏幕坐标。
- 场景相机优先使用当前关卡根节点下的 `GameCamera`，找不到时退回 `Camera.main`；Canvas 或场景相机缺失时返回 false，沿用原本无 HUD 目标的本地缩放降级动画。
- 屏幕坐标通过场景相机 `ScreenToWorldPoint(...)` 投影到 `visualRoot.position.z` 所在平面，保证 `DOMove` 的终点是场景世界坐标。
- 奖励、存档、事件派发和 HUD 刷新顺序保持不变。

## 数据、UI 与事件流

1. `Postcard_Star` 收集成功后进入 `PlayCollectFeedbackAsync()`。
2. `TryGetHudTargetPosition(visualRoot, out targetPosition)` 读取当前 `GamePlayPanel` 的 HUD 星星块位置。
3. `ResolveHudScreenPosition(...)` 根据 Canvas 模式把 HUD 位置解析为屏幕坐标。
4. `ResolveWorldCamera()` 获取关卡 `GameCamera` 或 `Camera.main`。
5. `ScreenToWorldPoint(...)` 把屏幕坐标投影到星星视觉根节点的世界 Z 平面。
6. `DOMove(targetPosition, duration)` 使用转换后的世界坐标播放飞行动画。
7. 动画结束后继续派发 `PostcardGetStarEvent`，由 `GamePlayPanel` 刷新 HUD 提示。

## 验证记录

- 静态 diff 确认本次只修改 `Postcard_Star.cs` 的 HUD 目标坐标解析逻辑，并保留现有奖励、存档、事件派发代码。
- 静态生命周期关键字扫描：本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、RemoteConfig、资源加载、广告、IAP 或 analytics 调用。
- `git diff --check -- Assets/Game/Scripts/Postcard/Postcard_Star.cs Assets/Game/Scripts/Postcard/Postcard_Star_HudTargetWorldPosition.md Assets/Game/Scripts/Postcard/Postcard_Star_HudTargetWorldPosition_RiskAssessment.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：失败，未进入本次脚本编译；原因是当前工程缺少大量 `Temp\bin\Debug\*.dll` Unity 生成程序集元数据，例如 `Apple.Core.dll`、`Assembly-CSharp-firstpass.dll`、`com.bear.logger.dll`、`DOTween.Modules.dll`。

## 剩余风险

- 尚未在 Unity PlayMode 中实际触发明信片星星收集，HUD 飞行动画终点、缩放消失和事件刷新顺序仍需补验。
- 尚未在 Android/iOS 横屏设备上验证不同分辨率、Canvas 缩放和相机适配下的飞行动画终点。
- 如果某个特殊关卡没有 `GameCamera` 且没有可用 `Camera.main`，星星会走无 HUD 目标的降级动画；奖励和存档不受影响。
- 如果特殊 `GamePlayPanel` 变体缺少 `Canvas` 或 `starBlock` 绑定，同样会走降级动画，仍需逐关确认表现是否符合预期。

## 回滚方式

- 回退 `Postcard_Star.cs` 中 `TryGetHudTargetPosition(Transform, out Vector3)`、`ResolveHudScreenPosition(...)` 和 `ResolveWorldCamera()` 的改动，恢复为直接返回 `GamePlayPanel.TryGetStarBlockWorldPosition(...)` 的位置。
- 删除本记录文档即可移除本次说明。
