# DLevel_001 延迟开放输入功能与风险评估

## 1. 需求与原行为

- 需求：`DelayAutoBeginDrawHandle.delay` 只延迟开放画线输入，不得影响
  `EnterLevelLoading` 的 Erase 开始时间。
- 原行为：该组件作为普通 `IDrawLevelIntroHandler`，在关卡加载回调内被等待；因此关卡
  初始化完成后仍需等待 prefab 配置时长，EnterLevelLoading 才开始 Erase。

## 2. 影响范围与修改后行为

- `DelayAutoBeginDrawHandle.cs` 改为实现现有 `IDrawLevelVisibleIntroHandler`。
- `DelayAutoBeginDrawHandle.cs` 同时标记为 `IDrawLevelEnterOnlyIntroHandler`，只参与首次
  进关门禁。
- `DrawPlayCtrl.cs` 在 Reset 时改用 `PlayResetIntroAsync()`，跳过仅进关 Handle，但继续
  等待其它承担关卡状态恢复职责的 Intro 和 0.5 秒黑幕淡出输入门禁。
- 本次代码提交不包含 `DLevel_001.prefab` 的序列化调整。
- 正常进入：资源加载与运行时初始化完成后立即开始 Erase；Erase 完成后等待 prefab 当前
  配置时长，再开放画线输入。
- Reset/Replay：不执行 Erase，也不重放 prefab 配置的进关延迟；重载完成后只等待既有
  0.5 秒黑幕淡出输入门禁，随后开放输入。

## 3. 数据、资源、UI 与事件流

- 数据/存档/Luban：无读取或写入变化。
- 资源：继续加载原 DLevel_001 prefab，没有新增资源地址或实例。
- UI：只改变 EnterLevelLoading 与输入门禁的相对顺序；Erase 自身参数不变。
- 事件：没有新增全局事件；输入仍由 DrawPlayCtrl 统一开放。
- Analytics/广告/SDK：无变化。
- 平台：Android/iOS 共用相同逻辑，没有新增平台分支。

## 4. 验证证据与未验证缺口

- 静态确认 DLevel_001 仍通过 `DelayAutoBeginDrawHandle` 提供首次进关门禁；本次不把
  工作区中的 prefab 序列化值纳入代码提交。
- 静态确认 `IDrawLevelVisibleIntroHandler` 继承 `IDrawLevelIntroHandler`，不会丢失既有
  Handler 缓存和调用。
- 静态确认 `IDrawLevelEnterOnlyIntroHandler` 只由 `DelayAutoBeginDrawHandle` 使用；
  DLevel_002、DLevel_010 等实际入场控制器不会被 Reset 过滤。
- 目标代码 `git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1
  /p:UseSharedCompilation=false` 通过：`0 error / 101 warning`，warning 为工程既有项。
- 未验证：Unity PlayMode 中 Erase 开始时机、Erase 后按当前 prefab 配置执行的输入门禁、
  Reset 后黑幕淡出完成即允许画线、连续 Reset、暂停和切后台。
- 未验证：Android/iOS 横屏触控和低帧率下的实际视觉节奏。

## 5. 风险与回滚

- `delay` 使用缩放时间；若 Erase 完成后 `timeScale=0`，门禁会继续暂停，需在 PlayMode
  验证暂停恢复。
- 共享组件还挂在 DLevel_003～DLevel_009；这些关卡 Reset 同样跳过仅进关延迟，继续通过
  0.5 秒门禁保持黑幕淡出期间输入关闭，仍需逐关回归。
- 风险：后续只有明确“不承担 Reset 状态恢复”的 Intro 才能实现
  `IDrawLevelEnterOnlyIntroHandler`；真实入场、机关复位或物体释放流程错误标记后会被
  Reset 跳过。
- 回滚时将 `DelayAutoBeginDrawHandle` 恢复为普通 `IDrawLevelIntroHandler`，并恢复
  DrawPlayCtrl Reset 调用 `PlayIntroAsync()`；无需迁移 prefab、配置或存档，但会恢复
  DLevel_001 Reset 后额外等待 prefab 配置时长的旧行为。
