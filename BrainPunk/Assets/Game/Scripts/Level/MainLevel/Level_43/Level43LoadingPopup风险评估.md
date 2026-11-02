# Level43LoadingPopup 风险评估

## 2026-07-09 角度加速倍率插值

### 原始行为基线

- `Level43LoadingPopup.Update()` 原先用 `IsAngleExceeded()` 做角度阈值判断。
- 达到判断条件后，自然倒计时立即从 `1` 倍跳到 `speedUpMultiple` 倍。
- `fakeSpeedUp` 期间不叠加角度倍率。
- 完成、OK 按钮显示、SpeedUp 按钮隐藏、完成回调和飞字逻辑不依赖角度判断函数。

### 受影响文件与资源

- 已修改：`Assets/Game/Scripts/Level/LevelItem/Level_43/Scripts/Level43LoadingPopup.cs`
- 只读证据：`Assets/Game/Scripts/Level/LevelItem/Level_12/UIDragRotate.cs`
- 相关 prefab：`Assets/Game/Scripts/Level/LevelItem/Level_43/Prefabs/Level43LoadingPopup.prefab`

### 使用的既有系统

- `UIDragRotate.GetCurrentAngle()` 提供当前角度。
- Unity `Mathf.InverseLerp(...)` 与 `Mathf.Lerp(...)` 计算角度进度和倍率。
- 原有 `BaseUIView`、`CustomButton`、`UICustomSlider`、DOTween fake speedup 流程保持不变。

### 风险项

- 手感变化风险：原先超过阈值是阶跃加速，现在是连续插值，目标角度附近倒计时节奏会更平滑但不再突然跳变。
- 配置理解风险：字段名仍为 `angleThreshold`，但实际语义已调整为目标角度；Inspector 维护时需要按目标角度配置。
- 非绝对角模式风险：当前实现按 `0 -> angleThreshold` 的方向插值，若已有 prefab 依赖旧的 `currentAngle < angleThreshold` 判断语义，需要 PlayMode 校验。
- 验证覆盖风险：本次只完成静态检查和 C# 编译，尚未覆盖 Unity PlayMode、真实触控和移动端横屏。

### 验证证据

- 目标脚本 `git diff --check` 通过，仅有 LF/CRLF 提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，99 warnings / 0 errors。
- 静态检查确认目标脚本中不再存在 `IsAngleExceeded`。

### 未验证缺口

- Unity PlayMode 中从 0 度拖到目标角度，确认倒计时倍率从 1 平滑过渡到 `speedUpMultiple`。
- Level43 loading prefab 中 `angleThreshold`、`speedUpMultiple`、`useAbsoluteAngle` 的实际 Inspector 值。
- Android/iOS 横屏触控下角度拖拽、倒计时、完成按钮和关闭流程。

### 回滚路径

回滚 `Level43LoadingPopup.cs` 本次改动，恢复 `IsAngleExceeded()` 和 `Update()` 中超过角度后直接乘 `speedUpMultiple` 的判断分支；无需修改存档、配置或资源。
