# Postcard_Star HUD 目标坐标转换风险评估

## 评估范围

- 评估日期：2026-07-02
- 变更入口：当前工作区未提交 diff
- 影响文件：`Assets/Game/Scripts/Postcard/Postcard_Star.cs`
- 配套记录：`Assets/Game/Scripts/Postcard/Postcard_Star_HudTargetWorldPosition.md`
- 评估方法：按知识库“Git 变更驱动风险评估工作流”，从脚本 diff 扩散到触发链、UI 链、相机/Canvas 链、奖励存档链、事件链和移动端验证缺口。

## 原始行为基线

- `Postcard_Star` 玩家触发收集后，保存永久收集记录，发放 `GameProps.Star`，再播放星星飞向 HUD 的视觉反馈。
- 飞行动画使用 `Transform.DOMove(...)`，因此目标位置需要是场景世界坐标。
- 原 `TryGetHudTargetPosition(...)` 直接把 `GamePlayPanel.TryGetStarBlockWorldPosition(...)` 返回的位置传给 `DOMove(...)`。
- `GamePlayPanel.TryGetStarBlockWorldPosition(...)` 目前返回 HUD `starBlock.transform.position`；当 Canvas 为 Screen Space 模式时，该值不能直接当作场景世界坐标使用。

## 本次变更

- `PlayCollectFeedbackAsync()` 调用 `TryGetHudTargetPosition(visualRoot, out targetPosition)`，把飞行动画视觉根节点传入目标解析逻辑。
- `TryGetHudTargetPosition(...)` 获取 HUD 星星块位置后，先经 `ResolveHudScreenPosition(...)` 按 Canvas 模式转为屏幕坐标。
- `ResolveWorldCamera()` 优先使用当前关卡根节点下 `GameCamera`，再退回 `Camera.main`。
- 屏幕坐标经场景相机投影到 `visualRoot.position.z` 所在世界平面，返回给 `DOMove(...)`。
- 找不到面板、HUD 目标、Canvas、视觉根节点或场景相机时，返回 false 并走原本本地缩放降级动画。

## 扩散链路

1. 玩家触碰 `Postcard_Star` 的 2D trigger。
2. `CollectAsync()` 完成重复检查、存档写入和 Star 奖励发放。
3. `PlayCollectFeedbackAsync()` 查询 HUD 目标并执行 DOTween 飞行动画。
4. 新逻辑只改变 HUD 终点的坐标转换，不改变 `DB.SavePostcard()`、`Bag.AddTool(...)` 或 `PostcardGetStarEvent`。
5. 动画完成后仍派发 `PostcardGetStarEvent`，由 `GamePlayPanel` 触发 HUD 提示和数字刷新。

## 已缓解风险

- HUD 坐标误用：Screen Space Canvas 下不再直接把 HUD `transform.position` 当作场景世界坐标，而是先转屏幕点再投影到场景世界。
- 相机缺失：关卡 `GameCamera` 和 `Camera.main` 都缺失时不强行动画飞向错误位置，保留无 HUD 目标降级表现。
- 特殊面板缺失：`CurrentGamePlayPanel`、父级 Canvas 或 `TryGetStarBlockWorldPosition(...)` 失败时仍返回 false，不阻断奖励和存档。
- 平面漂移：转换后强制 `position.z = visualRoot.position.z`，避免 2D 场景星星飞行过程中 Z 轴偏移。

## 风险项

### P1-1 真实 Canvas/相机组合仍需 PlayMode 验证

- 问题：静态代码确认了转换路径，但实际 HUD Canvas、UI 相机、关卡相机和横屏适配组合需要在 Unity 中触发收集才能确认终点位置。
- 影响文件：`Postcard_Star.cs`、`GamePlayPanel.cs`、实际关卡 prefab、实际 `GamePlayPanel` prefab/变体。
- 扩散链路：HUD 位置 -> Canvas 模式 -> UI 屏幕点 -> 场景相机投影 -> 星星 `DOMove` 终点。
- 建议：在至少一个默认面板关卡和一个特殊面板代表关卡中触发明信片星星收集，确认飞行动画终点对齐 HUD 星星块。

### P2-1 无可用场景相机会走降级动画

- 问题：如果某个关卡没有 `GameCamera` 且 `Camera.main` 也不可用，`TryGetHudTargetPosition(...)` 会返回 false。
- 影响：奖励、存档和事件不受影响，但玩家看到的是本地缩放消失，而不是飞向 HUD。
- 当前处理：不使用不可靠坐标，避免星星飞向错误位置。
- 建议：抽样检查明信片星星所在关卡是否存在可用场景相机。

### P2-2 特殊 GamePlayPanel 变体仍可能缺少 HUD 目标或 Canvas

- 问题：特殊面板如果没有 `starBlock` 或父级 Canvas，转换无法得到完整 HUD 屏幕坐标。
- 影响：收集流程仍成功，但飞行动画可能降级或终点不符合预期。
- 当前处理：保留 false 降级路径。
- 建议：对已使用 `Postcard_Star` 的特殊面板关卡做抽样 PlayMode 验证。

## 验证证据

- CodeGraph 证据：`TryGetHudTargetPosition` 仅由 `PlayCollectFeedbackAsync()` 调用；`TryGetStarBlockWorldPosition(...)` 仅被 `Postcard_Star` 使用。
- 静态 diff 证据：本次只新增 `Postcard_Star` 内部坐标转换和相机解析逻辑，未修改奖励、存档、事件或 UI 公共 API。
- 关键字扫描：本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、RemoteConfig、资源加载、广告、IAP 或 analytics 调用。
- `git diff --check -- Assets/Game/Scripts/Postcard/Postcard_Star.cs Assets/Game/Scripts/Postcard/Postcard_Star_HudTargetWorldPosition.md Assets/Game/Scripts/Postcard/Postcard_Star_HudTargetWorldPosition_RiskAssessment.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：失败，未进入本次脚本编译；原因是当前工程缺少大量 `Temp\bin\Debug\*.dll` Unity 生成程序集元数据，例如 `Apple.Core.dll`、`Assembly-CSharp-firstpass.dll`、`com.bear.logger.dll`、`DOTween.Modules.dll`。

## 未覆盖验证

- Unity Editor PlayMode：首次收集、HUD 飞行动画终点、缺 HUD 目标降级、重复触发、重开、失败、胜利、退出再进。
- Unity 编译/Domain Reload：本次 `dotnet build` 因缺少 Unity `Temp\bin\Debug` 元数据程序集失败，仍需在 Unity Editor 中补验。
- Android/iOS 横屏：不同分辨率和 Canvas 缩放下终点位置、触摸收集、动画流畅度、暂停/退出/切后台状态一致。
- 特殊 `GamePlayPanel` 变体：`starBlock`、Canvas、UI 相机和关卡相机绑定仍待抽样检查。

## 回滚路径

- 回退 `Postcard_Star.cs` 中 `TryGetHudTargetPosition(Transform, out Vector3)`、`ResolveHudScreenPosition(...)`、`ResolveWorldCamera()` 和调用点变更。
- 若只需临时规避动画问题，可让 `TryGetHudTargetPosition(...)` 返回 false，使收集继续走本地缩放降级表现。
- 删除本风险评估和对应功能记录即可回退文档。
