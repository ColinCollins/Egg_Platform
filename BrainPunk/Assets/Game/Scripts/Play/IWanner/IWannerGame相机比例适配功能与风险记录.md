# IWannerGame 相机比例适配功能与风险记录

## 需求与背景

- 适配范围限定为 IWanner Game，不改变主线、DrawGame、Loading 或其它玩法相机。
- IWanner 小关以 `1920×1080`（`16:9`）为设计基准；常见横屏手机保持各关原构图，iPad `4:3` 及接近 `1.43:1` 的横屏比例需要完整显示设计宽度，避免左右关卡内容被裁掉。
- 适配通过缩放正交 Camera Size 完成，不改变 Camera 位置、viewport rect、关卡 prefab 构图参数或 UI Canvas 适配。

## 影响文件与系统

- `Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`
- `Assets/Game/Scripts/Common/ResolutionFit/CameraOrthographicSizeFit.cs`
- `Assets/Game/Prefabs/IWannerGameItems/IWannerGame_Camera.prefab`
- `Assets/Game/Prefabs/WannerLevel/ILevel_001～ILevel_040.prefab` 与 `ILevel_999.prefab`
- 本记录与 `DevelopMD/已有功能设计与使用记录.md`
- 未修改 Luban 配置、存档、资源地址、事件、打点、广告、IAP 或平台 SDK。

## 原有行为基线

- 静态扫描确认当前 `ILevel_001～ILevel_040` 与 `ILevel_999` 共 41 个 prefab，每个恰好包含 1 个正交 Camera。
- 各关 Camera Size 是本关构图数据，范围为 `5～7.5`；原运行时不会根据 iPad 宽高比修改这些值。
- 修改前 41 个 prefab 均未挂 `CameraOrthographicSizeFit` 或修改 rect 的 `CameraResolutionAutoFit`，Camera 结构在各关内重复序列化。
- `IWannerPlayCtrl.OpenCurrentLevelAsync()` 负责资源加载、实例化 `_levelInstance`、解析 `IWannerLevelCtrl` 并初始化小关，是所有 IWanner 小关共享且可回滚的运行时接入点。

## 新行为与调用链

```text
ResourceManager.LoadAsync(ILevel prefab)
    -> Instantiate(_levelInstance)
    -> InstallLevelCameraSizeFit()
       -> 查找当前小关唯一 Camera
       -> 校验 Orthographic
       -> 复用 IWannerGame_Camera 自带 CameraOrthographicSizeFit
       -> 缺失时保留运行时补挂降级路径
       -> ApplyFit()
    -> IWannerLevelCtrl.InitializeRuntime()
```

- `ILevel_001～ILevel_040` 与 `ILevel_999` 的 Camera 已统一替换为 `IWannerGame_Camera` PrefabInstance，共享相机组件与适配器；各关原 Size 仍通过 prefab override 或共享默认 `6.5` 保留。
- `CameraOrthographicSizeFit` 在挂载时缓存当前关卡自己配置的 Size，不统一覆盖成固定值。
- 默认参考比例为 `1920×1080`，计算公式为：

```text
scale = max(1, (1920 / 1080) / viewportAspect)
targetSize = levelAuthoredSize * scale
```

- 常见 `16:9`、`19.5:9`、`20:9` 手机：`scale = 1`，各关 Size 与既有构图保持不变。
- iPad `4:3`：`scale = 1.333333`；例如 Size `5 -> 6.666667`、Size `7.5 -> 10`。
- iPad Pro 11 英寸代表比例 `2388×1668`：`scale ≈ 1.2417`。
- viewport 尺寸运行时变化时，组件始终用缓存的本关原 Size 重算，不在上一次适配结果上累乘。
- 当前 41 个 prefab 已显式挂载该组件，中央接入会复用现有组件，不重复 `AddComponent`。

## 行为边界

- 仅处理世界正交相机，不处理 IWanner HUD；HUD 的横向安全区继续由既有 `IWannerGame_GamePlayPanelSafeArea` 负责。
- 不修改 Camera rect，因此不会主动增加黑边；宽屏自然增加左右视野，iPad 增加上下视野以保留完整设计宽度。
- Camera 缺失或不是正交相机时，通过 Bear Logger 记录警告，但不改变既有关卡加载成功/失败判断。
- 当前 IWanner 代码中未发现其它 `orthographicSize` / `DOOrthoSize` 写入；未来新增动态镜头时，必须协调它与适配器的 Size 所有权。

## 验证记录

- CodeGraph 确认调用链为 `OpenCurrentLevelAsync -> InstallLevelCameraSizeFit -> CameraOrthographicSizeFit.ApplyFit`。
- 静态扫描 41 个 IWanner prefab：`41/41` 恰好 1 个目标 Camera PrefabInstance、`0/41` 保留旧内嵌 Camera；有效 Size 与迁移前提交 `ed1a76c63` 逐关一致，范围仍为 `5～7.5`。
- Camera 父节点、Transform 位置与 YAML fileID 唯一性检查通过；`ILevel_014/015` 的 `y = -0.05` 特例保持不变。
- `dotnet build Assembly-CSharp.csproj --no-restore` 通过：`0 error / 109 warning`；warning 均为工程既有告警，未指向本次两个脚本。
- 公式静态样例覆盖 `1920×1080`、`2340×1080`、`2732×2048` 与 `2388×1668`。
- `uncertain`：尚未在 Unity PlayMode 目视检查全部 41 关的上下边界，也未在 Android/iPhone/iPad 真机验证画面、触控坐标、Safe Area 和旋转/恢复场景。

## 风险与后续验证

- iPad 会额外显示上下区域，可能暴露原先在 `16:9` 视野外的背景边缘、机关、FallArea 或装饰物；应优先抽查 Size 最大、地图边界最紧以及上下藏有对象的关卡。
- 世界坐标输入仍使用本关 Camera，理论上会随新投影同步换算，但触控落点、拖拽和边缘按钮必须以 PlayMode/真机结果为准。
- 正常路径复用共享 prefab 已挂载的轻量组件；只有 viewport 整数宽高变化时才重算，日常每帧仅执行整数比较且无堆分配。
- 共享 Camera 后续参数或组件改动会同时影响 41 个 ILevel，修改前必须按全部关卡影响面评估并至少覆盖 Size 边界关卡回归。

## 回滚路径

- 从 `IWannerPlayCtrl.OpenCurrentLevelAsync()` 删除 `InstallLevelCameraSizeFit()` 调用及其私有方法，即可恢复所有 IWanner 小关原始 Camera Size 行为。
- 通用 `CameraOrthographicSizeFit` 可保留供其它明确相机使用；若无其它消费者，可连同其 meta 和通用记录一起删除。
- 若同时撤回共享 Camera 结构，需定向恢复 41 个 ILevel 的 Camera 区块和逐关 Size；不得整文件覆盖，以免丢失关卡并行改动。详细基线与回滚见 `Assets/Game/Prefabs/WannerLevel/ILevel全量Camera替换功能记录.md` 与风险评估。
