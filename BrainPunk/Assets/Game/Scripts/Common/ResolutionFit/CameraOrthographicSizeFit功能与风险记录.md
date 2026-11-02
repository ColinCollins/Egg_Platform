# CameraOrthographicSizeFit 功能与风险记录

## 需求与背景

- 游戏世界画面的设计基准为 `1920×1080`。
- 需要在不裁掉设计画面内容的前提下适配实际横屏宽高比：宽屏保持基准纵向视野并自然增加左右可见范围；比 `16:9` 更窄的 viewport 通过增大正交相机 `orthographicSize`，保证设计宽度仍完整可见。
- 现有 `CameraGizmoDrawer` 只在编辑器中绘制不同分辨率的可见框，不修改运行时相机；现有 `CameraResolutionAutoFit` 修改 `Camera.rect`，职责与本次 Size 适配不同。

## 影响文件与系统

- 新增运行时组件：`Assets/Game/Scripts/Common/ResolutionFit/CameraOrthographicSizeFit.cs`。
- IWanner 中央接入：`Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs` 在小关实例化后为关内唯一正交 Camera 安装该组件。
- 未修改 `CameraGizmoDrawer.cs`、`CameraResolutionAutoFit.cs`、共享 `GameCamera.prefab`、41 个 IWanner 关卡 prefab、场景、UI、存档、配置、资源加载、事件、打点或平台 SDK。

## 原有行为基线

- `CameraGizmoDrawer` 以 `1080` 为参考高度，根据 Camera 当前 Size 绘制 `1920×1080`、`2340×1080`、`1920×1440`、`2340×1440` 的编辑器 Gizmo。
- `GameCamera.prefab` 的正交 Size 当前为 `5.9`，并挂有 `CameraResolutionAutoFit`；后者按屏幕比例修改 viewport rect，不修改 Size。
- `Level59Ctrl` 和 `DrawTwoBallReunionSuccessHandle` 存在各自明确的运行时 Size 控制，本次未改变这些既有关卡行为。

## 新增行为

组件挂到需要适配的正交 Camera 后，在 PlayMode 启动时读取 Camera 实际 viewport 宽高并计算：

```text
referenceAspect = 1920 / 1080
viewportAspect = viewportWidth / viewportHeight
widthContainScale = max(1, referenceAspect / viewportAspect)
targetSize = referenceSize * widthContainScale
```

- `viewportAspect >= 16:9`：Size 保持基准值，高度方向构图不变，屏幕越宽自然显示越多左右内容。
- `viewportAspect < 16:9`：Size 增大，完整保留设计宽度，同时会额外显示上下内容。
- `referenceOrthographicSize > 0` 时使用显式基准；为 `0` 时缓存组件 `Awake` 时 Camera 当前 Size。
- 可选监听 viewport 整数宽高变化；未变化时只做整数比较，不重复写 Camera Size，不产生每帧堆分配。
- `ApplyFit()` 仅在 PlayMode 生效，不修改 `Camera.rect`，不访问 DB、配置、存档或运行时单例。

## 使用边界

- 当前只由 `IWannerPlayCtrl` 在小关 prefab 实例化后运行时安装，适配范围不包含主线、DrawGame、Loading 或共享 `GameCamera.prefab`。
- IWanner 各关继续以 prefab 自己的 Camera Size 作为 `1920×1080` 基准；中央接入不批量修改已验收 prefab。
- 不应与持续逐帧写入 `orthographicSize` 的控制器无协调地同时使用；例如 Level 59 的手势缩放会覆盖一次性适配结果。
- 与 `CameraResolutionAutoFit` 同时存在时，计算依据是最终 Camera viewport；若需要无黑边的全屏 Size 适配，应先明确是否停用原 rect 适配，再做全关卡回归。

## 验证记录

- 使用 Unity `6000.0.59f2` 的 `UnityEngine.CoreModule.dll` 对新增脚本执行独立 C# 定向编译，结果为 `0 warning / 0 error`；接入 IWanner 后完整 `Assembly-CSharp` 构建为 `0 error / 109` 个工程既有 warning。
- 公式样例（基准 Size `5.9`）：`1920×1080 -> 5.9`、`2340×1080 -> 5.9`、`1920×1440 -> 7.866667`、`1280×720 -> 5.9`。
- 静态扫描未发现 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、单例、实例化、`Camera.main` 或 `Camera.rect` 写入。
- 本次新增文件的定向尾随空格检查通过。全仓 `git diff --check` 仍被任务开始前已有的 `ILevel_040.prefab` 尾随空格和 `DevelopMD/已有功能设计与使用记录.md.orig` 冲突标记挡住，与本次新增文件无关。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 横屏设备以及不同刘海/安全区机型上验证实际构图。

## 剩余风险

- IWanner 在 iPad 等比 `16:9` 更窄设备上会增加世界可见高度，可能暴露关卡边界、隐藏物体或非预期背景。
- 如果其它脚本在适配后继续修改 Size，最终结果由调用顺序决定；必须按具体相机控制链验证。
- Camera 使用非全屏 rect、动态分辨率或 RenderTexture 时，需要以实际 `pixelWidth/pixelHeight` 验证目标 viewport。

## 回滚路径

- 从 `IWannerPlayCtrl` 删除运行时安装调用与私有方法，即可恢复全部 IWanner 小关原相机行为。
- 若无其它消费者，再删除新增脚本、meta 与本记录；共享 prefab 和 41 个 IWanner prefab 无需回滚。
