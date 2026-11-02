# Level43LoadingPopup 功能说明

## 2026-07-09 角度加速倍率插值

### 需求与背景

用户要求 `Level43LoadingPopup` 的自然倒计时加速不要再用当前角度是否超过目标角度做直接 `if` 判断，而是根据当前 angle 与目标 angle 对 `speedUpMultiple` 做 `Lerp` 插值过渡。

### 影响范围

- 脚本：`Assets/Game/Scripts/Level/LevelItem/Level_43/Scripts/Level43LoadingPopup.cs`
- 关联运行时组件：`UIDragRotate.GetCurrentAngle()`、`UICustomSlider.SetProcess(...)`、`SpeedUpBtn`、`OkBtn`
- 关联资源：`Assets/Game/Scripts/Level/LevelItem/Level_43/Prefabs/Level43LoadingPopup.prefab`

本次未修改 prefab、配置表、存档、资源加载路径、全局事件、广告、IAP、analytics、RemoteConfig 或平台 SDK。

### 原有行为基线

- `Update()` 中自然倒计时默认按 `Time.deltaTime` 扣减。
- 当 `angleSlider != null` 且 `IsAngleExceeded()` 为 true，并且没有处于 `fakeSpeedUp` 状态时，直接将 `deltaTime` 乘以 `speedUpMultiple`。
- `IsAngleExceeded()` 在绝对角模式下使用 `Mathf.Abs(currentAngle) > angleThreshold`，非绝对角模式下使用 `currentAngle < angleThreshold`。
- `fakeSpeedUp` 期间仍使用 `fakeSpeedUpMultiple`，不叠加角度倍率。

### 新行为

- `angleThreshold` 字段名保持不变，以兼容现有 prefab 序列化；Inspector 描述改为“目标角度”。
- 自然倒计时倍率由 `GetAngleSpeedUpMultiplier()` 计算：
  - 没有 `angleSlider` 时倍率为 `1`。
  - 绝对角模式下使用 `Mathf.Abs(currentAngle)` 和 `Mathf.Abs(angleThreshold)` 计算插值进度。
  - 非绝对角模式下使用 `Mathf.InverseLerp(0f, angleThreshold, currentAngle)`，支持正向或负向目标角度。
  - 进度从 `0` 到 `1` 时，倍率从 `1` 线性插值到 `speedUpMultiple`；超过目标角度后由 `InverseLerp` 夹到最大倍率。
- `fakeSpeedUp` 期间保留原有行为，只使用 `fakeSpeedUpMultiple`。

### 数据、资源与事件流

- 不新增或修改 DB/save 字段。
- 不新增资源加载、对象实例化或全局事件。
- UI 文本和进度条刷新仍由原 `UpdateUI()` 执行。
- 点击加速按钮仍只扣 `clickCostTime` 并播放飞字，不受本次角度插值改动影响。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_43/Scripts/Level43LoadingPopup.cs`：通过，仅有 Git 的 LF/CRLF 提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。

### 未验证与风险

- 未在 Unity PlayMode 中实际拖动 Level43 loading 角度滑块验证倍率曲线手感。
- 未在 Android/iOS 横屏设备上做触控 smoke。
- `angleThreshold` 字段名因兼容序列化未改名，后续 Inspector 使用时应按“目标角度”理解。

### 回滚路径

如需恢复旧行为，回滚 `Level43LoadingPopup.cs` 中 `GetAngleSpeedUpMultiplier()` 与 `Update()` 的倍率计算，恢复 `IsAngleExceeded()` 的 bool 判断与超过阈值后直接乘 `speedUpMultiple` 的逻辑即可。
