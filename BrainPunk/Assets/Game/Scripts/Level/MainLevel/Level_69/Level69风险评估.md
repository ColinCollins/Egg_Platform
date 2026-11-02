# Level69 风险评估

## 2026-10-10：发射结束音乐恢复条件风险复核

### 事实与直接结论

- 原行为：开战 `PlayBattleSoundOnce()` 暂停背景音乐，生存成功无条件恢复；`AudioManager.ResumeMusic()` 不检查用户开关，设置关闭的已暂停音轨也能进入恢复调用。修改前隔离测试复现四种开关组合中的三种错误恢复，但未执行真实听感复现。
- 本次仅改 `Level69Ctrl.cs`：在运行态 `Start()` 记录 `DB.GameSetting.MusicOn`，成功时同时检查入关快照与当前开关。沿用原 AudioManager，不修改共享音频、Prefab、配置、存档模型、SFX、打点、事件或平台 SDK。
- 入关关闭：本关结束不主动恢复。入关开启但关内关闭：当前开关优先阻止恢复，不用旧快照覆盖新选择。入关关闭但关内开启：主动开启动作仍由原设置事件处理，本关结束不追加恢复。
- 快照只在新实例 `Start()` 赋值；同实例 Disable/Enable、Idle 刷新不重采样。`PlayCtrl.cs` 的 Reset/Replay 和 `CreateLevelAsync()` 证明正式重开使用新实例；失败/销毁路径未新增恢复入口，原单次胜利与死亡门禁保留。
- 性能/维护：每个实例新增一个非序列化 bool，入关读取一次、成功时至多读取一次当前开关；无新轮询、音轨创建、Tween、全局监听或生成代码变更。新增 DB 读取由 `Start()` 的 `Application.isPlaying` 门禁保护，不接入编辑器预览。

### 验证、未验证风险与回滚

- 37 项真实方法提取加副作用桩断言通过；剥离本轮字段/采样/恢复门禁后，原源码全文一致（归一行尾）。证据：TEMP `brainpunk-level069-music-20261010/verify.ps1`、`verify.log`。
- 独立 Unity Roslyn Editor 主程序集编译通过，0 error / 113 warning，Level69 无诊断；定向差异检查通过。编译不证明声音、生命周期或设备表现。
- `uncertain`：Unity PlayMode、实际设置事件、音轨句柄、Reset/Replay、前后台/蓝牙和 Android/iOS 真机听感未验证；关闭再开启时原设置事件仍可能在战斗期间恢复音乐，属于原共享行为，不在本次“发射结束”修改范围内。
- 风险边界：快照记录用户开关，不记录音轨因其它系统临时暂停的状态；本次不新增共享暂停所有权系统。标准启动先初始化 DB；直接运行未完成 bootstrap 的测试场景需初始化既有数据，不能以该场景推定正式进关已验证。
- 回滚仅移除本轮私有字段、Start 新增门禁/采样和成功条件，恢复无条件 Resume；不回滚其它任务、Prefab、资源或存档。回滚后重新出现原错误恢复风险，无数据迁移。

## 2026-06-26：FakeSystemTips Collider 生命周期控制风险评估

### 原始行为基线

- `FakeSystemTips` 原逻辑只控制 In/Out 位移和 `SpriteRenderer` 透明度。
- `Level_069.prefab` 静态文本显示 `FakeSystemTips` 根对象挂有 `BoxCollider2D`，并且脚本 `target` 指向自身 Transform。
- 修改前 `FakeSystemTips` 不会在 Out 或 In 动画阶段主动关闭/打开 Collider，因此 Out 动画期间 Collider 可能仍处于启用状态。

### 受影响文件/资源

| 文件/资源 | 影响说明 |
|------|------|
| `Assets/Game/Scripts/Level/LevelItem/Level_69/FakeSystemTips.cs` | 新增 Collider 缓存与生命周期开关 |
| `Assets/Game/Prefabs/Level/Level_069.prefab` | 本次未修改；作为 `FakeSystemTips` 根对象 Collider 与 `target` 绑定证据 |
| `BtnArea` / `pressInput` | 不在 `FakeSystemTips.target` 子层级内，不会被本次逻辑开关 |

### 现有系统使用

- `DG.Tweening`：继续负责 In/Out 动画；本次只在动画开始和 In 完成回调中切换 Collider。
- `Collider2D`：通过 `GetComponentsInChildren<Collider2D>(true)` 缓存，运行时统一开关 `enabled`。
- 本次不接入对象池、存档、配置、资源加载、热更、打点、广告、IAP 或平台 SDK。

### 风险项

| 风险 | 等级 | 说明 |
|------|------|------|
| In 动画过程中不可碰撞 | 低 | 当前按需求只在 In Complete 后打开 Collider；如果设计需要进入动画期间就可碰撞，需要调整打开时机。 |
| target 子层级新增 Collider 被统一控制 | 中 | 缓存范围是 `target` 子层级全部 `Collider2D`，未来如果子物体 Collider 有独立生命周期，可能被误关/误开。 |
| 外部直接改 Collider.enabled 导致状态竞争 | 低 | 当前本地代码未发现其他 `FakeSystemTips` Collider 控制入口；PlayMode 仍需验证是否有动画/事件外部改写。 |
| DOTween 完成回调边界 | 低 | `PlayIn()` 完成回调增加 `_isIn` 判断，避免已切到 Out 后旧完成回调误开 Collider；仍需运行时验证快速按放。 |
| 重复 PlayOut 调用 | 低 | 重复 Out 时也会关闭 Collider 后返回，降低状态不同步风险。 |

### 验证证据

- 静态确认 `FakeSystemTips.cs` 在 `EnsureInitialized()` 中缓存 `target.GetComponentsInChildren<Collider2D>(true)`。
- 静态确认 `Start()` 非 In 初始状态关闭 Collider，`PlayOut()` 进入和重复调用路径都会关闭 Collider。
- 静态确认 `PlayIn()` 开始关闭 Collider，完成回调仅在 `_isIn` 仍为 true 时打开 Collider。
- 静态确认 `Level_069.prefab` 中 `FakeSystemTips` 根对象挂有 `BoxCollider2D`，脚本 `target` 指向自身 Transform；`BtnArea` 不是该 target 子层级。
- 手动尾随空白检查覆盖 `FakeSystemTips.cs`、`Level69功能说明.md`、`Level69风险评估.md` 和 `DevelopMD/已有功能设计与使用记录.md`，未发现尾随空白；`git diff --check` 对当前已跟踪修改返回 0，仅提示 `DevelopMD/已有功能设计与使用记录.md` 工作区 LF/CRLF 转换。当前 Level69 脚本和本地文档在工作区仍为未跟踪文件，纳入 Git 后需复跑完整 diff check。
- 静态风险扫描本次新增代码只命中 `Collider2D`；未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、PlayerPrefs、StreamingAssets、YooAsset、analytics、广告或 IAP 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，原因是 `Temp/bin/Debug` 缺少多个项目引用 DLL 元数据文件，未得到完整 Unity 工程编译结论。

### 未验证缺口

- 未在 Unity PlayMode 验证长按触发 In、松手触发 Out、成功自动 Out 三条路径上的 Collider enabled 状态。
- 未验证快速按下后立刻松手时，In tween 被 Kill 后不会有残留完成回调打开 Collider。
- 未验证移动端横屏触摸和多指/重复点击条件下，FakeSystemTips Collider 与 BtnArea 输入 Collider 的交互是否符合预期。
- 未完成完整 C# 工程编译验证；需要 Unity Editor 重新生成/编译或补齐 `Temp/bin/Debug` 依赖后复跑。

### 回滚路径

1. 回退 `FakeSystemTips.cs` 中 `_colliders`、`SetCollidersEnabled(bool)`、`EnsureInitialized()` Collider 缓存和 `Start()` / `PlayIn()` / `PlayOut()` 的 Collider 开关调用。
2. 删除 `Level69功能说明.md`、本文件和 `DevelopMD/已有功能设计与使用记录.md` 中的 FakeSystemTips Collider 记录。

## 2026-06-26：countdown_txt 剩余时间显示风险评估

### 原始行为基线

- `Level69Ctrl` 已有 `_playingTimer` 和 `playingDuration` 控制 `Playing` 持续时间。
- 修改前没有将剩余时间写入任何文本 UI。
- 用户已在脚本中补充 `TMP_Text countdown_txt` 字段，但静态 prefab 文本未显示该字段绑定；是否已通过 Unity 序列化保存绑定仍需 PlayMode 或 Inspector 确认。

### 受影响文件/资源

| 文件/资源 | 影响说明 |
|------|------|
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` | 新增倒计时文本缓存和刷新逻辑 |
| `Assets/Game/Prefabs/Level/Level_069.prefab` | 本次未修改；依赖 Inspector 绑定或同名 `countdown_txt` 子节点兜底查找 |

### 风险项

| 风险 | 等级 | 说明 |
|------|------|------|
| `countdown_txt` 未绑定且同名查找失败 | 低 | 脚本会跳过刷新，不影响关卡主流程，但倒计时不可见。 |
| 格式理解偏差 | 低 | 已按用户澄清改为 `ss:ms`，当前毫秒位为两位百分之一秒；若最终需要三位毫秒，需要调整格式。 |
| 文本刷新造成额外 GC | 中 | 当前按百分之一秒变化写入 `text`，刷新频率高于秒级显示；需移动设备验证。 |
| 成功边界显示跳变 | 低 | `_playingTimer >= playingDuration` 后强制显示 `00:00`，未在 PlayMode 验证最后一秒表现。 |

### 验证证据

- 静态确认 `Level69Ctrl.cs` 新增 `CacheCountdownTextReference()`，优先使用 Inspector 绑定，否则查找同名 `TMP_Text` 子节点。
- 静态确认 `UpdateCountdownText(...)` 使用 `Mathf.CeilToInt(remainingTime * 100f)` 计算剩余百分之一秒，并输出 `$"{displaySeconds:00}:{displayMilliseconds:00}"`。
- 静态扫描本次新增倒计时代码未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、PlayerPrefs、StreamingAssets、YooAsset、analytics、广告或 IAP 链路；命中项来自旧对象池和输入逻辑。
- `git diff --check` 通过，仅提示当前工作区 LF/CRLF 转换。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，原因是 `Temp/bin/Debug` 缺少多个项目引用 DLL 元数据文件，未得到完整 Unity 工程编译结论。

### 未验证缺口

- 未在 Unity PlayMode 验证 `countdown_txt` 是否绑定成功、是否显示在预期层级，以及从 `20:00` 到 `00:00` 的实际变化。
- 未在 Android/iOS 横屏验证字体清晰度、遮挡、缩放和不同分辨率下的位置。
- 未验证快速退出、重开、成功后重新进入时文本是否符合策划期望。
- 未完成完整 C# 工程编译验证；需要 Unity Editor 重新生成/编译或补齐 `Temp/bin/Debug` 依赖后复跑。

### 回滚路径

1. 回退 `Level69Ctrl.cs` 中 `countdown_txt`、`_lastDisplayedCountdownCentiseconds`、`CacheCountdownTextReference()`、`GetRemainingPlayingTime()`、`UpdateCountdownText(...)` 以及相关调用。
2. 删除 `Level69功能说明.md`、本文件和 `DevelopMD/已有功能设计与使用记录.md` 中的 countdown 记录。

## 2026-06-26：version2.0 spawnMissile 三阶段优化风险评估

### 原始行为基线

本次修改前的当前基线：

- `Level69Ctrl.Update()` 在 `Playing` 状态下累加 `_spawnTimer`，达到 `missileSpawnInterval` 后调用 `SpawnMissile()`。
- `SpawnMissile()` 每次只从 `GetStartPointPosition()` 获取一个生成位置，未按 `version2.0` 注释拆分阶段。
- `GetStartPointPosition()` 每次重新读取 `startPoint.position`，缺失 `startPoint` 时回退为导弹初始位置。
- `Level69MissileCtrl` 已负责导弹向下移动、碰撞特效、出屏回收和活跃实例统一回收；本次未修改该脚本。

### 受影响文件/资源

| 文件/资源 | 影响说明 |
|------|------|
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` | 新增三阶段生成参数和阶段切换状态 |
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69MissileCtrl.cs` | 未修改，作为导弹移动和回收依赖 |
| `Assets/Game/Prefabs/Level/Level_069.prefab` | 未修改；新增字段使用脚本默认值，后续可由 Inspector 调整 |

### 现有系统使用

- `ObjectPoolManager`：继续注册、获取和回收 `Level69MissileCtrl`。
- `PressItemEventHandle` / `FakeSystemTips` / 成功链：不属于本次改动点，保持旧路径。
- `startPoint`：继续作为所有阶段的 Y/Z 来源，降低对现有场景高度和层级的影响。

### 风险项

| 风险 | 等级 | 说明 |
|------|------|------|
| 新增 X 坐标默认值不符合最终关卡设计 | 中 | 默认 `-3/0/3` 与 `-3~3` 只是可运行占位，需要在 Unity Inspector 中按关卡画面调节。 |
| 阶段二 4 秒边界与实际帧率存在小误差 | 低 | 阶段计时依赖 `Time.deltaTime`，切换时可能有单帧误差；玩家可见行为应是 A/B 往返一次后进入随机阶段。 |
| 阶段三 0.2 秒生成间隔增加对象池压力 | 中 | `maxSize=30` 保持不变，快速生成时若导弹回收不及时可能出现 `Get` 返回 null；需 PlayMode 和移动设备验证。 |
| 新增序列化字段导致 Inspector 兼容显示变化 | 低 | 未改字段名和原有字段顺序之前的含义，旧 `missileSpawnInterval` 保留；但打开 prefab 后会显示新增调参项。 |
| 生成 X 坐标覆盖 startPoint 的动态 X | 中 | 这是 `version2.0` 的意图；Y/Z 仍每次读取 `startPoint`，但旧“完全跟随 startPoint”的 X 行为已变更。 |

### 验证证据

- 静态确认 `Level69Ctrl.cs` 顶部 `version1.0` / `version2.0` 需求注释未改写。
- 静态确认新增逻辑只在 `Level69Ctrl.cs`，未修改 `Level69MissileCtrl.cs`、`Level_069.prefab` 或共享对象池。
- 静态扫描本次新增代码未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、PlayerPrefs、StreamingAssets、YooAsset、analytics、广告或 IAP 链路；命中项来自旧对象池 `Instantiate` 和输入事件 `AddListener`。
- `git diff --check` 通过，仅提示既有 `DevelopMD` 文件 LF/CRLF 工作区转换。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 在 120 秒内超时，未获得编译结果；已停止本次超时派生的 `dotnet build` 进程。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，原因是 `Temp/bin/Debug` 缺少多个项目引用 DLL 元数据文件，未得到完整 Unity 工程编译结论。

### 未验证缺口

- 未在 Unity PlayMode 验证三阶段节奏、A/B 往返、随机范围、对象池上限和导弹碰撞回收。
- 未完成完整 C# 工程编译验证；需要 Unity Editor 重新生成/编译或补齐 `Temp/bin/Debug` 依赖后复跑。
- 未在 Android/iOS 横屏设备验证 0.2 秒随机生成阶段的帧率、触摸长按和退出/重开清理。
- 未由策划/美术确认阶段一三点、阶段二 A/B 和阶段三范围的最终 X 坐标。
- 未验证旧 `missileSpawnInterval` 字段在 Inspector 中保留但不控制 v2.0 主生成节奏是否符合调参预期。

### 回滚路径

1. 回退 `Level69Ctrl.cs` 中 `MissileSpawnPhase`、三阶段序列化字段、`ResetMissileSpawnPhases()`、`UpdateMissileSpawn()`、阶段坐标计算和阶段切换逻辑。
2. 恢复 `Update()` 中旧的 `_spawnTimer >= missileSpawnInterval` 后直接 `SpawnMissile()`。
3. 删除 `Level69功能说明.md` 和本文件中的 2026-06-26 记录。

## 原始行为基线

最早基线：`Level69Ctrl.cs`、`Level69MissileCtrl.cs` 均为空实现，`FakeSystemTips` 脚本不存在。关卡加载后：

- 点击/长按按钮无任何响应。
- `FakeSystemTips` 不会显示或隐藏。
- 不会生成导弹，无计时，无法进入成功状态。
- 场景中的 `Missile` GameObject 若保持 Active，其 `Level69MissileCtrl` 也不会移动（Update 为空）。

本次修复前的当前基线：

- `Level_069.prefab` 已有 `Level69Ctrl`、`PressItemEventHandle`、`FakeSystemTips`、导弹和按钮事件绑定。
- `OnButtonPressed()` 首次从 `Idle` 进入 `Playing` 时提前返回，首按不会触发 `FakeSystemTips.PlayIn()` 和 `pressBtn` 显示。
- 成功延迟没有取消令牌，禁用/销毁后仍可能等待结束并派发成功。
- 成功完成直接派发 `SwitchGameStateEvent.SUCCESS`，未优先走 `BaseLevelCtrl.SuccessStrightly()` 的 finished/成功动画链。
- `pressBtn.SetActive(...)` 缺少空引用保护，缺失绑定会报错。
- `OnButtonClick(Transform, Vector2)` 为私有方法，不能作为可靠的 `ClickItemEventHandle` 兼容 API。

## 受影响的文件/资源

| 文件/资源 | 影响说明 |
|-----------|----------|
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` | 新增并修正状态机、输入响应、计时、对象池调用、成功链和生命周期取消 |
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69MissileCtrl.cs` | 新增移动、碰撞、对象池回收逻辑 |
| `Assets/Game/Scripts/Level/LevelItem/Level_69/FakeSystemTips.cs` | 新增脚本，需要挂载到 `FakeSystemTips` GameObject |
| `Assets/Game/Prefabs/Level/Level_069.prefab` | 已静态确认 `Level69Ctrl`、`StartPoint`、`PressItemEventHandle`、按钮事件和成功参数绑定 |
| `Assets/Game/UI/Global/Scripts/SystemTips.cs` | 成功时调用，显示最终提示 |
| `Assets/Game/Scripts/Common/ObjectPool/ObjectPoolManager.cs` | 注册/注销 `Level69MissileCtrl` 对象池 |
| `Assets/Game/Scripts/Level/BaseLevelCtrl.cs` | 成功完成优先调用 `SuccessStrightly()`，进入既有关卡完成链 |

## 使用的现有系统

- `Bear.EventSystem`：`DispatchEvent`、`Witness<T>`、`SwitchGameStateEvent`。
- `Game.Common.ObjectPoolManager`：对象池注册、获取、回收。
- `Game.UI.Global.SystemTips`：全局提示 UI。
- `PressItemEventHandle` / `ClickItemEventHandle`：输入检测组件。
- `DG.Tweening`：`FakeSystemTips` 动画。

## 风险项

| 风险 | 等级 | 说明 |
|------|------|------|
| Prefab 引用缺失导致运行时行为不完整 | 中 | 当前静态检查显示 `Level_069.prefab` 已绑定 `startPoint`、`fakeSystemTips`、`pressInput`、`pressBtn`。`pressBtn` 已有空引用兜底；`fakeSystemTips` 或导弹缺失时会退化为不显示/不生成，仍需 PlayMode 验证。 |
| 模板导弹被实例化为可见对象 | 中 | `Level69Ctrl.Awake()` 会隐藏场景中的 `Missile` 模板并记录其位置；若逻辑被跳过，可能导致场景中存在静止或提前下落的导弹。 |
| 对象池未正确注销导致泄漏 | 低 | 关卡禁用时先调用 `Level69MissileCtrl.RecycleAllActive()` 回收所有活跃导弹；`OnDestroy` 中调用 `UnregisterPool`。若 `ObjectPoolManager` 先于 `Level69Ctrl` 销毁，会退化为 `Destroy(gameObject)`。 |
| 导弹碰撞标签/层级不匹配 | 中 | `Level69MissileCtrl` 同时检测 `tag == "Player"` 或 `layer == "Actor"`、以及 `tag == "Ground"` 或 `layer == "Ground"`。若项目实际标签/层级不同，碰撞无响应。 |
| 长按/松手输入重复触发 | 低 | 当前 Prefab 已有 `PressItemEventHandle` 持久事件绑定。本次脚本只在没有持久绑定当前目标方法时动态补监听，避免 Awake 再 AddListener 造成一次输入触发两次。 |
| 活跃导弹在关卡切换时残留 | 低 | 已使用静态 `activeInstances` 集合跟踪，并在 `Level69Ctrl.OnDisable()` 中统一回收；集合也会清理已被销毁的引用。 |
| 成功提示内容占位 | 低 | `successTipsContent` 默认 `"Success!"`，需要策划/本地化替换。 |
| DOTween 动画被异常中断 | 低 | `FakeSystemTips` 在 `OnDestroy` 中 Kill Sequence，正常退出可清理。 |
| 过期成功回调跨生命周期触发 | 中 | 本次已为 `successDelay` 增加 `CancellationTokenSource`，在进入 Playing、禁用、销毁时取消；仍需 Unity 运行时验证快速退出/重开。 |
| 成功链绕过 BaseLevelCtrl | 中 | 本次成功延迟后优先调用 `BaseLevelCtrl.SuccessStrightly()`，缺失时才兜底派发 `SwitchGameStateEvent.SUCCESS`；仍需验证 Level69 实际成功动画、音效和下一关流程。 |

## 验证证据

- 静态检查 `Level_069.prefab`：`Level69Ctrl` 已绑定 `missile`、`startPoint`、`fakeSystemTips`、`pressInput`、`pressBtn`、`playingDuration=20`、`missileSpawnInterval=0.3`、`successDelay=2`、`successTipsContent=Success!`。
- 静态检查 `BtnArea`：`PressItemEventHandle.OnPressEvent` 绑定 `Level69Ctrl.OnButtonPressed()`，`OnCancelPressEvent` 绑定 `Level69Ctrl.OnButtonReleased()`。
- 静态扫描 `Assets/Game/Scripts/Level/LevelItem/Level_69` 未发现本次新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets` 或热更链路。
- `Level69Ctrl.cs` 顶部 `version1.0` 注释块未改动。
- `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 未完成：缺少 `Temp\obj\Assembly-CSharp\project.assets.json`。
- 允许 restore 的 `dotnet build .\Assembly-CSharp.csproj ...` 在 120 秒内未完成，未获得编译结果；该次 build 派生的 dotnet/MSBuild 进程已清理。
- restore 产物生成后复跑 `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 暴露 `FakeSystemTips.cs` 的 `_originalPosition` 编译错误；已通过点位/offset 兼容修复解决。
- 最终 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 96 warning；warning 属于当前工程既有范围。

## 未验证缺口

- 未在 Unity 编辑器中实际运行关卡，未验证按钮点击/长按、FakeSystemTips 动画、导弹生成与移动、20 秒计时、成功切换的完整流程。
- 未验证 `Missile` GameObject 上的碰撞体是否为 Trigger，以及 `Actor`/`Ground` 的标签/层级是否匹配。
- 未验证 `FakeSystemTips` 子节点的 `SpriteRenderer` 是否能被自动收集，In/Out 的视觉表现需美术确认。
- 未验证 `SystemTips.Show()` 在成功时是否能正确显示在最上层。
- 未验证 `BaseLevelCtrl.SuccessStrightly()` 在 Level69 中是否完整触发成功动画、音效、关卡完成、下一关和存档/解锁相关流程。
- 未验证 Android/iOS 横屏触摸路径下 `PressItemEventHandle` 的长按、松手和多指边界。

## 回滚路径

1. 回退 `Level69Ctrl.cs` 与 `Level69MissileCtrl.cs` 到空实现版本。
2. 删除 `FakeSystemTips.cs` 及其 `.meta` 文件。
3. 在 `Level_069.prefab` 中：
   - 移除 `FakeSystemTips` GameObject 上的 `FakeSystemTips` 脚本组件（如已挂载）。
   - 清空 `Level69Ctrl` 上新增字段的引用。
   - 移除额外添加的 `PressItemEventHandle` 组件和 `StartPoint` 节点（如已添加）。
4. 回退后关卡恢复为“无玩法”的原始状态。

## 待澄清问题

1. `successTipsContent` 最终显示文案需要策划/本地化确认。
2. `FakeSystemTips` 的 In/Out 动画具体位移、透明度、持续时间需要美术/策划确认。
3. 是否需要为 `ClickItemEventHandle` 在 prefab 中额外绑定 `OnButtonClick(Transform, Vector2)`，作为不长按时的备用入口；当前主路径已使用 `PressItemEventHandle`。

## 2026-07-10：version3.0 实现前风险基线

### 工作树与归属边界

- 实现前 `git status --short` 已记录：规则指针、`.specify/feature.json`、`Level_069.prefab`、`Level69Ctrl.cs`、`Packages/packages-lock.json` 和 `specs/036-level69-v3-logic/` 已存在改动。
- 本次只接管 Level69 本地脚本、`Level_069.prefab`、Level69 中文文档、共享事实记录及当前 feature 的 `tasks.md`；`Packages/packages-lock.json` 和 SDK/配置/共享脚本不属于本次实现。
- 回滚边界为当前分支开始实现前的 Level69 脚本/prefab 草稿；不得用破坏性 Git 操作覆盖用户已有改动。

### 实现前 Prefab 证据

| 对象/绑定 | 实现前状态 |
|---|---|
| BtnArea | `PressItemEventHandle` 同时绑定 `OnButtonPressed`/`OnButtonReleased`，不符合 click-only 开始/召回边界。 |
| FakeSystemTips | 只有一个位于 `TriggerItem` 层的 `BoxCollider2D`，输入区域与物理盾牌未分离。 |
| Missile | 同时存在 `Level69MissileCtrl`、`TrapEventHandlle` 和 `OnTrigger2DHandle`，Player 命中可能重复结算。 |
| Key | `Level69Ctrl.keyPrefab` 指向共享 Key 资源，运行时实例化；场景内没有 Level69 本地 Key 实例。 |
| DoorLock | 场景内没有 Level69 本地 DoorLock 实例；既有 door event 1 executor 位于 `door_0`。 |
| door event 1 | 现有 executor `Id: 1` 会启用门碰撞、播放 `door_open` 和开门音效；门触发继续调用 `BaseLevelCtrl.OnActorTrigger2D`。 |
| Actor/countdown/pool | Actor、`countdown_txt`、missile/startPoint 已绑定；倒计时 20 秒；池初始 3、上限 30。 |

### 主要风险与回滚条件

- 缺失新输入、Key、DoorLock、LockPoint 或动画完成引用时必须一次性报错并阻止开始，允许安全退出/重开，不能自动判胜或持续刷错。
- 同一物理步同时触碰盾牌与 Actor 时必须以盾牌优先；多枚导弹同帧不得重复死亡、炸飞或失败。
- event 1 只能在钥匙触碰、飞行和锁动画完成后触发；event 1 不等于最终通关。
- 若 Unity 编译、Level69 完整开始/失败/成功闭环或 prefab 绑定验证失败，回滚 Level69 本地脚本、新增本地组件/meta 和 `Level_069.prefab` 本次覆盖，不修改共享 Key/DoorLock/门/BaseLevel/存档/SDK。

## 2026-07-10：version3.0 实现后风险评估

### Facts

- 实际修改范围为 Level69 本地脚本、`Level_069.prefab`、本地文档、共享事实记录和 feature tasks；未编辑共享输入、Key/DoorLock、BaseLevel、PlayCtrl、GamePlayPanel、存档、配置或 SDK 脚本。
- 原 version 注释哈希实现前后相同；`git diff --check` 的三处脚本尾随空格只来自必须保留的原注释行。
- Prefab 的新 Key/DoorLock 为 inactive nested prefab instance；Key 默认 `onEnter` 共享执行链在 Level69 override 为空，由 Level69Ctrl 运行态只注册一次 `OnKeyContact`。
- door event 1 的既有 executor 仍负责启用门碰撞、播放 door_open 和音效；最终 Player 门触发仍绑定 `BaseLevelCtrl.OnActorTrigger2D`。
- Unity batchmode 失败来源是现有 UniTask/YooAsset 与 Guru 生成代码依赖缺失；日志未出现 Level69 脚本错误或 `Level_069.prefab` 导入错误。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| 缺失引用导致软锁或自动胜利 | 已在代码层降低 | 首次点击前集中校验 click/hold/tips/missile/countdown/Key/Lock/LockPoint/Actor/pool；失败只记录一次并禁用点击。 |
| 不可见盾牌或输入区挡弹 | 已在静态层降低 | HoldInputArea 位于非 TriggerItem 层；原 TriggerItem collider 初始 disabled，仅 Defending 由 FakeSystemTips 开启。 |
| Actor/盾牌同物理步竞态 | 已在代码层降低，待运行验证 | Missile 先即时处理盾牌，Actor 命中延后 LateUpdate；Missile 执行序 -50、Level69Ctrl +50。 |
| 多导弹重复失败 | 已在代码层降低，待运行验证 | 每个池实例有 collisionResolved；Level69Ctrl resultClosed 在首次提交时关闭结果并统一回收。 |
| 钥匙自动开锁/提前通关 | 已在静态层降低 | 活动 3.0 代码没有直接成功调用；钥匙落地后才开 collider，首次接触后等待 jump 与 unlock 完成再发 event 1。 |
| 重开残留 | 已在代码层降低，待运行验证 | OnDisable/OnDestroy/失败统一取消 session/prompt/key token、owner、tween、盾牌、导弹、池、Key/Lock。 |
| 对象数量无界增长 | 已在代码层降低 | 池 maxSize 30，调用 Get 前检查 ActiveCount，容量满时跳过生成。 |

### Hypotheses / pending validation

- DoorLock 根节点设置在 `door_0/LockPoint` 推导世界坐标处，静态引用正确，但实际 Spine pivot 视觉是否对齐仍需 PlayMode 确认。
- `Actor.Die()` 后调用 `Knockback()` 可形成炸飞反馈，代码顺序明确；共享失败切换前实际可见时长需运行确认。
- `ClickItemEventHandle` 在 Out 阶段立即可召回；快速点击中断 Out 后是否符合最终手感需要运行确认。

### Missing evidence

- Unity 完整 0-error 编译：被项目级 UniTask/Guru 缺失依赖阻断。
- Unity PlayMode：未执行 US1、20 组碰撞、20 组 key/lock/door、每阶段 10 次重开/退出。
- Profiler：未获得随机阶段 frame time、steady-state GC、Physics2D、TMP、池峰值和十次重开数据。
- Android/iOS：无本次候选 build 与代表性真机，横屏触控、多指、Cancel、后台恢复、fps/GC 均未验证。
- `quickstart.md` 第 2 节静态检查与第 8 节文档/回滚已执行；第 3 节 Unity 编译结果为项目依赖失败；第 4～7 节因没有可运行的 0-error 候选版本而阻断，未伪造 PlayMode、Profiler 或真机结果。

### 回滚路径

1. 回退 `Level69Ctrl.cs`、`FakeSystemTips.cs`、`Level69MissileCtrl.cs` 到实现前分支基线。
2. 删除 `Level69HoldInput.cs` 与 `.meta`。
3. 回退 `Level_069.prefab` 的 BtnArea click override、HoldInputArea、Missile 组件去重以及 Level69Key/Level69DoorLock nested instance。
4. 删除本次 2026-07-10 实现后文档段落；不要回退或修改共享 Key/DoorLock/door/BaseLevel/save/SDK。

## 2026-07-10：倒计时与战斗音效风险评估

### 原始行为基线与影响范围

- `l69countdown` 原先跟随厘秒文本刷新，存在一秒内高频重复播放风险。
- `l69battle` 原先位于非 Idle 的按钮入口，召回提示时可能重复播放；返回的 `Sound` 未保存，Level 销毁时无法定向停止。
- 本次只修改 `Level69Ctrl.cs` 及 Level69/共享中文记录；`AudioManager`、Sounds Good 资源、`Level_069.prefab`、存档、配置、事件、analytics 和平台 SDK 均未修改。
- 复用现有 `AudioManager.PlaySound(...)` 与 Sounds Good `Sound.Stop()`，三个 Level69 tag 均有本地生成类和集合资产证据。

### 风险项与控制

| 风险 | 等级 | 控制与当前结论 |
|---|---|---|
| countdown 随帧率重复播放 | 中 | 使用 `CeilToInt(remainingTime)` 的整数秒桶去重；文本厘秒刷新不再直接触发音效。 |
| 低帧率时跨过多个秒数 | 低 | 当前只播放最新秒一次，不追赶补播，避免同帧音效堆叠；实际听感待 PlayMode 验证。 |
| battle 在提示召回时重复 | 中 | 播放入口移到 `BeginSession()`，并用 `_battleSoundStarted` 做局内一次性保护。 |
| Level 退出后 battle 残留 | 中 | `OnDisable` 与 `OnDestroy` 均进入 `CleanupSession()`，定向 `Stop()` 并清空句柄。 |
| SFX 关闭时句柄为空 | 低 | `StopBattleSound()` 使用空安全调用；一次性标记仍阻止本局反复尝试。 |
| battle 是否需要 loop | 待确认 | 本次保持用户现有默认 `loop:false`，只增加句柄持有和销毁清理；若音频设计要求循环，需要另行确认。 |

### 验证证据与未验证缺口

- 目标脚本 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示；新增 diff 未命中编辑器生命周期、DB/save、RemoteConfig、PlayerPrefs、实例化或全局事件链。
- 静态扫描确认 `l69battle` 和 `l69countdown` 各只有一个播放入口，`StopBattleSound()` 由统一清理路径调用。
- `Assembly-CSharp.csproj` 编译通过，结果为 0 error / 98 warnings；warnings 未逐项审计。
- 未在 Unity PlayMode 统计一局 20 秒的实际 countdown 播放次数，未验证快速召回、失败、成功等待钥匙、退出、销毁、SFX 开关切换和低帧率场景。
- Android/iOS 横屏真机的音量、重叠、蓝牙/后台切换和性能影响未验证。

### 回滚路径

1. 回退 `Level69Ctrl.cs` 中整数秒去重状态和 `UpdateCountdownSound(...)` 调用。
2. 回退 battle 句柄、一次性标记及 `PlayBattleSoundOnce()` / `StopBattleSound()` 调用。
3. 删除本节与功能说明、共享事实记录中的对应音效段落；无需回滚 prefab、音频资产或 `AudioManager`。

## 2026-07-13：pressBtn 按压态显示修复风险评估

### Facts

- `Level_069.prefab` 将 `Level69Ctrl.pressBtn` 绑定到 `Sp_l69_btn_02`，该节点默认 inactive。
- 修改前 `SetButtonState(...)` 有多个状态机调用入口，并同时控制 `pressBtn` 显隐与 `clickInput` 可点击性。
- 本次代码修改只位于 `Level69Ctrl.cs`：按下显示 `pressBtn`、松开隐藏；其余状态机入口只保留原可点击性参数。
- 未修改 prefab、共享输入组件、全局事件、DB/save、配置、资源加载、analytics 或平台 SDK。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| 状态切换误显示按下态 | 已在代码层消除 | 状态机不再控制 `pressBtn`；显隐入口收敛到按下/松开回调。 |
| 修复意外改变内置点击仲裁 | 已通过拆分降低 | 各状态机调用保留原 `interactable` 参数，只移除其对视觉节点的副作用。 |
| 初始进入即显示按下态 | 静态证据不支持 | Prefab 中 `Sp_l69_btn_02` 保持 `m_IsActive: 0`，本次未修改 prefab。 |
| 空引用导致异常 | 现有保护保留 | `SetPressButtonVisible(...)` 与 `SetButtonInteractable(...)` 都保留 null 检查。 |

### Hypotheses / pending validation

- 正常 Press/Release 输入路径下，按下态会严格覆盖手指按住区间；需要 Unity PlayMode 实际观察帧序和快速连按表现。
- 若输入组件在手指移出、系统 Cancel、应用暂停或关卡禁用时不派发 `OnButtonReleased()`，`pressBtn.activeSelf` 可能保留为 true；当前缺少对应运行日志，不能作为已发生问题处理。

### Missing evidence

- `Assembly-CSharp.csproj` 未获得有效编译结论：当前 `Temp/bin/Debug` 缺少 191 个 Unity/包引用 DLL，错误均为 `CS0006` 元数据文件缺失，编译未进入本次脚本诊断。
- 未执行 Unity PlayMode 的按下、松开、快速连按、移出、Cancel、暂停、失败、重开与退出矩阵。
- 未在 Android/iOS 横屏真机验证触摸取消及多指边界。
- 未获得本次视觉帧序的录屏或截图证据。

### 回滚路径

1. 恢复 `SetButtonState(visible, interactable)`，并恢复各调用点的原两个参数。
2. 移除 `SetPressButtonVisible(...)` / `SetButtonInteractable(...)` 拆分及按下、松开的显隐调用。
3. 恢复原 Tooltip，并删除本节、功能说明与共享事实记录中的本次条目；无需修改 prefab 或共享输入组件。

## 2026-07-13：BtnArea Drag 输入迁移风险评估

### Facts

- BtnArea 当前挂载 `DragEventsListener`，BeginDrag/EndDrag 分别绑定 `OnButtonPressed()` / `OnButtonReleased()`；原 `ClickItemEventHandle` 已从 BtnArea 移除。
- `Level69Ctrl` 已改为序列化并校验 `dragInput`，用 `_buttonInteractable` 仲裁新的 BeginDrag；禁用时结束当前 Drag，清理链会隐藏 `pressBtn`，启用时恢复监听。
- BtnArea EndDrag 不再调用 `StartPromptExit()`；防御退出仍由 `Level69HoldInput.HoldEnded` 驱动。
- Level69Key 的 `OnTrigger2DHandle` 保留并绑定，默认 `TriggerExecutor` 持久回调已清空；运行时只注册 `OnKeyContact()`。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| 空 clickInput 阻止开始 | 已消除 | 校验字段和 prefab 引用统一为非空 `dragInput`。 |
| 非交互阶段重复开始/召回 | 已在控制器仲裁 | BeginDrag 首先检查 `_buttonInteractable`，无效阶段不显示、不执行业务入口。 |
| 状态切换提前结束按压视觉 | 已规避 | 不通过 `SetEnableDrag(false)` 中断当前 Drag；真实 End/Cancel/移出负责隐藏，清理链兜底隐藏。 |
| BtnArea 松手结束另一手指防御 | 已消除代码入口 | BtnArea End 只隐藏视觉，防御退出只接受 `Level69HoldInput` owner 的 HoldEnded。 |
| Key 默认与本地流程双跑 | 已消除静态配置入口 | Key 持久回调数组为 0，保留碰撞转发并运行时注册本地回调。 |

### Hypotheses / pending validation

- `DragEventsListener` 在 BtnArea 的 OverlapPoint 命中顺序和 fallback 半径能覆盖目标按钮区域；需要 PlayMode 在重叠 Collider 场景验证。
- 无效阶段仍会由共享 Drag 组件轮询输入，但控制器立即忽略 Begin；预计不改变玩法，实际移动端开销和多指表现待验证。

### Missing evidence

- Unity 项目编译被 191 个缺失引用 DLL 阻断，未得到本次脚本编译结论。
- 未执行 PlayMode 的 Begin/End/移出/Cancel/多指/阶段切换/禁用重开矩阵。
- 未验证 Key 碰撞只进入一次本地飞行开锁流程，也未验证 Android/iOS 横屏触摸。

### 回滚路径

1. 恢复 `ClickItemEventHandle clickInput`、原必填校验及 `SetEnableClick(...)`。
2. 恢复 BtnArea 的 Click 组件和 `OnButtonClick` prefab 绑定，移除当前 Drag Begin/End 绑定。
3. 如回退到共享 Key 默认流程，恢复 Key 的 `TriggerExecutor` 持久回调并同步移除 Level69 运行时 `OnKeyContact`，不得同时保留两条链。
4. 删除本节、功能说明和共享事实记录的对应条目。

## 2026-07-13：单次战斗入口风险评估

### Facts

- 当前需求明确：同一个 Level69 关卡实例只进入一次战斗，Playing 阶段按钮仅承担提示召回。
- 修改前 `BeginSession()` 首句执行完整 `CleanupSession(false)`；`OnButtonPressed()` 已先显示 `pressBtn`，因此清理会在同一调用栈再次隐藏它。
- 修改前 `OnEnable()` 在 `_hasStarted` 后无条件初始化 Idle，可能让已开战实例重新满足开始条件。
- 当前代码已改为 `BeginBattle()`，使用 `_battleStarted` 作为实例生命周期内不复位的一次性门闩；完整清理只保留在禁用与销毁入口。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| 首次按下态同帧消失 | 已消除代码原因 | `BeginBattle()` 不再调用清理；`pressBtn` 正常隐藏仍由 EndDrag 负责。 |
| 快速连按重复开始战斗 | 已在代码层双重拦截 | `OnButtonClick()` 与 `BeginBattle()` 都检查 `_battleStarted`，且门闩在创建 token 和启动提示前设置。 |
| Disable/Enable 后重复开战 | 已在代码层拦截 | 已开战实例不会在 `OnEnable()` 恢复 Drag 或再次执行 Idle 初始化；Idle 可交互计算也检查门闩，清理后的 Update 由 `_resultClosed` 停止。 |
| 门闩误伤 Playing 召回 | 静态代码未发现 | 门闩只用于 Idle 开战分支；Playing 仍按 Hidden/Exiting 与 `_resultClosed` 条件调用 `BeginPromptAttempt(false)`。 |
| 退出资源残留 | 原清理职责保留 | `CleanupBattle()` 仍由 `OnDisable()` / `OnDestroy()` 取消任务、回收导弹、注销池和停止战斗音效。 |

### Hypotheses / pending validation

- Unity 同帧连续输入或外部 UnityEvent 重复投递时，门闩应保证倒计时、战斗音效、对象池和开场提示只初始化一次；尚缺 PlayMode 计数日志或录屏证据。
- 已开战对象 Disable 后再 Enable 会保持不可重新开战，这是本次明确约束；关卡框架是否会复用同一实例作为“重开一局”仍需结合实际重开流程验证。若框架需要重开，应创建新关卡实例，而不是复位该门闩。

### Missing evidence

- `Assembly-CSharp.csproj` 被 `Temp/bin/Debug` 缺少 191 个程序集阻断，没有获得本次 C# 编译结论。
- 未执行 Unity PlayMode 的快速连按、重复 UnityEvent、Playing 召回、失败重开、Disable/Enable 和退出矩阵。
- 未验证 Android/iOS 横屏触摸的重复 Begin、Cancel 与后台恢复行为。

### 回滚路径

1. 将 `BeginBattle()` 恢复为 `BeginSession()` 并恢复其开头的完整清理调用。
2. 移除 `_battleStarted`、OnButtonClick/方法内部双重门闩和 OnEnable 限制。
3. 将 `CleanupBattle()` 恢复为 `CleanupSession(bool)` 并恢复初始化路径调用。
4. 删除本节及功能说明、共享记录对应条目；回滚后需重新接受按下态同帧隐藏和重复开战风险。

## 2026-07-13：Editor Mouse 优先风险补充

### Facts

- Level69 BtnArea 的输入来源是共享 `DragEventsListener`。
- Android/iOS Build Target 在 Unity Editor 中也会定义对应平台宏；原条件会让普通 Game View 编译进 Touch 分支。
- 当前条件已增加 `!UNITY_EDITOR`，Editor 走 Mouse，非 Editor Android/iOS 走 Touch。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| 普通 Game View 鼠标不触发 | 已修正静态分支原因 | Editor 不再进入仅依赖 `Input.touchCount` 的移动分支。 |
| 真机误走 Mouse | 静态条件未发现 | Android/iOS Player 不定义 `UNITY_EDITOR`，仍满足移动 Touch 分支。 |
| Level69 业务入口变化 | 未修改 | Begin/Drag/End、按钮门闩和召回逻辑均保持原调用。 |
| 共享关卡回归 | 仍需验证 | 共享监听器还被 Level24、Level34、Level74、Level87 等使用，缺少逐关 PlayMode 证据。 |

### Missing evidence 与回滚

- 尚未在普通 Game View 实际点击 Level69，也未验证 Device Simulator、Android/iOS 真机、多指、移出和 Cancel。
- C# 编译若仍被工作区 Unity 临时程序集缺失阻断，应保留为未验证缺口。
- 回滚只需恢复共享监听器的原预处理条件，并删除本节、功能说明和共享记录对应条目；Level69 prefab 无需修改。

## 2026-07-13：Key/DoorLock 职责收缩风险评估

### Facts

- 当前明确需求是：DoorLock 不由 `Level69Ctrl` 隐藏；生存胜利后只显示 `keyObject`，其余 Key/门锁逻辑由用户在场景侧处理。
- 修改前控制器拥有 Key 下落、Collider 开关、Player 触发、飞向 LockPoint、unlock 动画等待、Door event 派发以及 Key/DoorLock 清理复位整条链。
- 当前控制器只校验 `keyObject` 并在 `EnterSurvivalCleared()` 调用一次 `keyObject.SetActive(true)`；未修改 prefab 资产。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| DoorLock 被控制器隐藏 | 已消除代码入口 | 控制器不再持有或调用 `doorLockObject.SetActive(...)`。 |
| Key 被控制器移动或复位 | 已消除代码入口 | Key Transform 缓存、下落插值和清理复位代码已移除。 |
| 胜利后 Key 未立即显示 | 已按需求收口 | 成功提示后同步执行 `keyObject.SetActive(true)`，无延时 token。 |
| 缺少旧 Key/Door 绑定阻止开战 | 已消除 | 必填校验只保留 `keyObject`，不再要求 Trigger/Collider/Jump/LockPoint/动画。 |
| 后续不能自动开门通关 | 有意移出本次范围 | 控制器不再监听 Key 或派发门事件，后续由用户场景逻辑负责。 |

### Missing evidence 与回滚

- 尚未在 Unity PlayMode 验证 Key 初始 active 状态、胜利瞬时显示、DoorLock 默认显示和用户后续交互链。
- `Level_069.prefab` 仍可能保留已移除字段的旧序列化 YAML；Unity 会忽略脚本中不存在的字段，但尚未通过 Inspector 保存/重载验证，当前不主动改 prefab，避免覆盖用户后续处理。
- C# 编译若继续受 `Temp/bin/Debug` 缺失程序集阻断，应保留为未验证缺口；Android/iOS 横屏也未验证。
- 回滚需恢复完整 Key/DoorLock 字段、状态、异步任务、碰撞回调、事件派发、清理逻辑和必填校验；本次无 prefab 变更可回滚。

## 2026-07-13：单次按钮与独立爆炸风险评估

### Facts

- 用户要求按钮仅首次触发、玩家死亡不清除其他炸弹、爆炸特效每次使用 `Instantiate`，并补充炸弹只在 Level 销毁时统一清理。
- `ObjectPoolManager.UnregisterPool()` 只销毁池内可用对象，不处理已借出的活跃对象；若失败时仅注销池，场上炸弹后续回收会找不到池。因此失败/胜利路径必须同时保留池注册。
- 当前 `OnDestroy()` 执行 `RecycleAllActive()` 后再 `UnregisterObjectPool()`，可把活跃实例送回池后统一清理；清理兜底仍受 `_ownsPoolRegistration` 约束。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| 开战后按钮再次触发 | 已在代码层关闭 | Playing 不再恢复 `_buttonInteractable`，首次 EndDrag 后禁用 `dragInput`。 |
| 玩家死亡清掉所有炸弹 | 已移除失败入口 | `EnterFailed()` 不再调用全量回收或注销池；命中的炸弹仍自行回收。 |
| 胜利时炸弹提前消失 | 已移除成功入口 | `EnterSurvivalCleared()` 不再操作导弹集合或池。 |
| 场上炸弹回收找不到池 | 已规避 | 失败/胜利后池保持注册，直到 Level `OnDestroy()`。 |
| 同时爆炸互相抢占特效 | 已在代码层隔离 | 每次碰撞实例化独立 ParticleSystem，不移动或播放模板对象。 |
| 爆炸实例永久残留 | 已增加定时销毁 | 按根与子粒子最长声明生命周期计算 Destroy 延迟；复杂 SubEmitter 实际尾效仍待验证。 |
| 移动端实例化峰值/GC | 未验证、中风险 | 每次爆炸产生 GameObject/ParticleSystem 实例和延迟 Destroy，需 Android/iOS Profiler 验证。 |

### Missing evidence 与回滚

- 未执行 Unity PlayMode 的快速连按、EndDrag 后再次点击、死亡后多炸弹、胜利后多炸弹、Level Destroy、同时爆炸和粒子尾效验证。
- 未验证 Level 只 Disable 但暂不 Destroy 时，独立于 Level 层级的活跃导弹是否会短暂继续运行；这是“只在 Destroy 清理”的直接结果。
- 未获得 Android/iOS 横屏真机的爆炸峰值帧耗、GC Alloc、内存与粒子实例峰值。
- 回滚时恢复 Playing 召回、失败/胜利回收和模板直接播放，并删除本节及功能说明/共享记录对应条目。

## 2026-07-13：倒计时结束后的炸弹失败表现风险评估

### Facts

- 倒计时归零仍在控制器 `LateUpdate()` 直接调用 `EnterSurvivalCleared()`；该方法关闭常规结果入口，但不会清除场上炸弹。
- 残留炸弹命中会调用 `TryCommitPlayerHit()`；当前入口不再读取 `_resultClosed`、GameState 或活跃炸弹数量，只检查目标 Actor 是否存在且尚未死亡。
- 成功路径会显示 Key；后续失败入口不会隐藏 Key，这是用户已明确留给场景侧处理的范围。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| 零秒后炸弹命中却不能失败 | 已修正 | 炸弹命中入口不再受结果关闭或成功状态阻止。 |
| 炸弹逻辑参与胜利判定 | 已避免 | 未增加 `ActiveCount` 等待或胜利状态分支，倒计时判胜保持原样。 |
| 多枚炸弹重复提交失败 | 已以 Actor 状态限制 | 首次命中执行 `Die()` 后，后续命中因 `IsDied` 返回 false。 |
| 胜利后又进入 Failed 状态 | 有意行为、需表现确认 | 用户要求残留炸弹仍可触发失败；内部状态会由 SurvivalCleared 切为 Failed。 |
| 胜利 Key 与失败表现同时存在 | 未处理、范围明确 | 失败入口不隐藏 Key，Key/DoorLock 后续由场景侧处理。 |

### Missing evidence 与回滚

- 尚未执行 Unity PlayMode 的零秒同帧 Player/Ground/Shield 碰撞、胜利后命中、玩家已死亡、多枚连续命中和 Level 销毁场景。
- C# 项目构建已通过（0 错误、98 个既有警告），目标文件 `git diff --check` 已通过。
- 成功提示/Key 已显示后再触发死亡与 Failed 的最终组合表现尚未由用户确认，当前按“炸弹只负责失败”实现。
- 尚未验证暂停、低帧率或 Android/iOS 真机下倒计时跨零帧与炸弹碰撞的顺序一致性。
- 回滚时恢复 `TryCommitPlayerHit()` 对 `_resultClosed` 以及 Intro/Playing 状态的校验。

## 2026-07-13：FakeSystemTips Idle 碰撞体风险评估

### Facts

- FakeSystemTips 根 `shieldCollider` 与 `DragableItem` 位于同一 GameObject；prefab 中该 Collider 初始关闭。
- `PlayIn()` 完成后现在开启该 Collider，`PlayOut()` 开始时关闭。
- 导弹是否被盾牌阻挡不只读取 Collider enabled，还必须通过 `Level69Ctrl` 的 Defending 状态检查。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| Idle 阶段 DragableItem 无根 Collider 可命中 | 已修正代码生命周期 | PlayIn 完成时先启用 Collider，再通知控制器进入 HoldWindow。 |
| Idle 自动获得防御能力 | 已由状态仲裁阻止 | `IsMissileBlockedByShield()` 仍要求 PromptPhase.Defending。 |
| Out 动画期间仍可开始拖拽 | 已关闭入口 | PlayOut 开始时同步关闭 Collider。 |
| Collider 提前开启产生额外物理接触 | 待 PlayMode 验证 | Collider 为非 Trigger，尽管导弹业务仲裁有状态门槛，仍需确认 Unity 物理接触不会影响移动轨迹。 |
| HoldInputArea 与根 Collider 输入竞争 | 未验证 | 两套输入组件同时读取鼠标/触摸，需确认拖拽和按住的目标交互符合预期。 |

### Missing evidence 与回滚

- 未执行 Idle 拖拽、Idle 炸弹接触、Defending 挡弹、Out 禁止拖拽以及 Android/iOS 多触点 PlayMode 验证。
- C# 项目构建已通过（0 错误、98 个既有警告），目标文件 `git diff --check` 已通过。
- 未修改用户已有脏改的 `Level_069.prefab`；当前结论基于现有绑定和源码静态证据。
- 回滚时删除 `FakeSystemTips.PlayIn()` 完成回调中的 Collider 开启逻辑，并恢复本节对应注释与记录。

## 2026-07-14：star2 战斗开场移动风险评估

### Facts

- `Level69Ctrl.BeginBattle()` 受 `_battleStarted` 与 `GameState.Idle` 双重门闩保护；本次在状态切换为 `Intro` 后启动 `star2` Tween。
- `star2` 复用现有 `star` 的世界落点 Y `-0.76`，移动时间为序列化字段 `star2MoveDuration`，prefab 当前值为 `10` 秒。
- `star2` prefab 实例的 `MoveFloatHandle` 当前为禁用状态，Tween 自然完成后才启用。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| 重复输入创建多个 star2 Tween | 已由现有开战门闩限制 | 只有首次有效 `BeginBattle()` 会调用 `StartStar2Move()`。 |
| star2 缺失导致不能开战 | 已避免 | `StartStar2Move()` 对空引用直接返回，未加入必填绑定校验。 |
| 缺少 MoveFloatHandle 抛异常 | 已避免 | 完成回调先获取并判空，再启用组件。 |
| 配置负时长造成异常 | 已做运行时收敛 | 传给 `DOMoveY` 的时长使用 `Mathf.Max(0f, star2MoveDuration)`。 |
| 退出或失败后 Tween 继续推进 | 未主动改变 | 本次未统一 Kill star/star2 Tween，以免扩大既有表现职责；需 PlayMode 确认实际期望。 |

### Missing evidence 与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 已通过：0 个错误、99 个既有警告；目标文件 `git diff --check` 未发现空白错误，仅有工作区 LF/CRLF 转换提示。
- 尚未运行 Unity PlayMode，未验证 10 秒移动是否与关卡战斗时长、失败、胜利、Disable/Destroy 和浮动组件衔接一致。
- 尚未验证 Android/iOS 横屏设备的可见范围、帧率和暂停/后台恢复行为；单个 DOTween 的性能风险预计较低，但仍未有设备证据。
- 回滚仅需移除 `StartStar2Move()` 及其调用、`star2MoveDuration` 字段和 prefab 配置；用户已有 `star2` 实例与引用可保留。

## 2026-07-14：生存胜利保持按住提示风险评估

### Facts

- 正常松手 Out 由 `HoldEnded` → `StartPromptExit()` → `FakeSystemTips.PlayOut()` 驱动。
- 原生存胜利路径调用 `CancelPromptAndHide()` → `AbortAndHide()`，会让仍在按住的提示立即消失。
- 当前生存胜利不再主动清理提示；`IsCurrentPrompt()` 只允许 `SurvivalCleared` 下的 `Defending/Exiting` 完成松手退出。
- 失败、Disable 和 Destroy 的清理路径未改，仍立即中断并隐藏提示。

### Direct conclusions

| 风险 | 结论 | 证据/控制 |
|---|---|---|
| 持续按住跨过倒计时后提示消失 | 已修正代码路径 | 胜利入口不再调用 `CancelPromptAndHide()`。 |
| 生存胜利后松手无法 Out | 已局部放行 | 仅 `SurvivalCleared + Defending/Exiting` 可绕过 `_resultClosed` 完成既有退出。 |
| 结果状态放宽导致旧异步流程重启战斗 | 已由状态条件阻止 | 战斗启动仍要求 `GameState.Intro` 且 `_resultClosed == false`。 |
| 失败/退出后提示继续残留 | 未引入 | Failed、Disable、Destroy 仍调用 `CancelPromptAndHide()`。 |
| 胜利后盾牌 Collider 继续生效 | 有意保持、需验证 | 按住状态与 Collider 均保留到松手；残留炸弹仍可被 Defending 仲裁。 |
| 修改扩散到其他关卡或共享 UI | 已隔离 | 只修改 `Level69Ctrl` 私有流程，没有新增事件、公共 API 或 prefab 改动。 |

### Hypotheses

- 成功提示、Key、star 与仍被按住的 FakeSystemTips 会同时显示，实际视觉层级和节奏需要 PlayMode 确认。

### Missing evidence 与回滚

- C# 工程构建通过（0 个错误、99 个既有警告），静态调用顺序检查通过。
- 尚未运行 Unity PlayMode，未验证持续按住跨零点、零点后松手、提前松手、残留炸弹失败和 Disable/Destroy 的实际表现。
- 尚未验证 Android/iOS 横屏真机的触摸 owner、胜利后 Collider、防御仲裁、动画流畅度和成功提示叠放。
- 回滚时恢复胜利入口的 `CancelPromptAndHide()`，并删除 `IsCurrentPrompt()` 对 `SurvivalCleared` 的局部放行；不涉及资源或序列化数据迁移。
