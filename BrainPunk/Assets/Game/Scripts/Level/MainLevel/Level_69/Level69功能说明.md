# Level69 功能说明

## 2026-10-10：发射结束按入关音乐开关恢复

### 修改前核对

- 需求与基线：用户要求炮弹发射结束后不要直接恢复音乐，应按进入关卡前的 music 状态判断。当前 `PlayBattleSoundOnce()` 暂停背景音乐，`EnterSurvivalCleared()` 无条件 `ResumeMusic()`；共享 `AudioManager.ResumeMusic()` 只检查音乐实例有效性，不检查用户音乐开关。
- 来源：`Level69Ctrl.cs` 的 `Start/PlayBattleSoundOnce/EnterSurvivalCleared/EnterSuccess/OnDisable/OnDestroy`；`Assets/Game/Scripts/Common/AudioManager.cs:109–120`；`Assets/Game/UI/SettingPopup/Scripts/GameSettingContent.cs:56–64`；`Assets/Game/DB/GameSetting.cs`、`GameSetting_Partial.cs` 和 `Assets/Game/Settings/DBManager_Generated.cs`；`Level_069.prefab:85–123` 显式绑定本关脚本。
- 状态定义：此处 music 状态按设置中的 `DB.GameSetting.MusicOn` 判断，不把转场中音轨是否暂时播放作为用户开关。设置界面先写入该值、保存，再派发既有音乐事件；正常启动由 `LoadManager.EnterGame()` 初始化 DB，`OnPostProcess()` 初始化 AudioManager 后进入游戏。
- 原流程与维护边界：本关 `Start -> InitializeIdleState`，首次按钮开战暂停音乐，生存成功恢复；失败/Disable/Destroy 不在本关恢复音乐，清理由原控制器负责。一个关卡实例只开战一次，重开由新的实例取得新快照；不得在 `OnEnable/InitializeIdleState` 重采样旧实例。
- 拟修改：只在 `Level69Ctrl.Start()` 的运行态入口保存一次入关开关，将成功恢复条件收窄为“入关时开启且当前仍开启”。若玩家在关内关闭音乐，不得被旧快照重新开启。不写回开关、不新增事件或共享音频 API，不改 Prefab、音效、炮弹、胜负/重置统计、配置或 SDK。
- 验证场景：入关开/关 × 结束时开/关；入关后、开战前关闭；同实例重新启用不覆盖快照；新实例按新开关采样；重复成功、失败、已死亡与销毁不多恢复；编辑态 Start 不读取 DB；定向编译与源码差异检查。真实听感、设置界面事件与 Android/iOS 横屏运行需另行 PlayMode/真机验证。
- 文档/工具缺口：既有本关音效和清理记录已读取，但未找到入关音乐快照的现行约定，以本地源码补足。CodeGraph CLI/MCP 当前不可用，引用的 feature plan 不存在；本次不依赖其修改局部音频条件。

### 实现与验证

- `Start()` 在 `Application.isPlaying` 门禁之后保存 `_musicOnAtLevelEntry`，成功时仅在该快照与当前 `DB.GameSetting.MusicOn` 同时为 true 时调用既有 `ResumeMusic()`。不调用 `PlayMusic()`、不改曲目、不写回或保存开关；玩家在关内主动切换设置仍由原设置事件处理。
- 入关开/结束开：恢复；入关开/结束关、入关关/结束开、入关关/结束关：本关均不主动恢复。入关后、开战前发生的开关变化不覆盖快照。`OnEnable/InitializeIdleState` 不重新采样；`PlayCtrl.ResetGame/ReplayGame` 先销毁旧关，再 `CreateLevelAsync` 实例化新关，新实例独立取得当前开关。
- 修改仅涉及控制器的私有字段、`Start()` 和成功恢复条件。剥离这三处改动后与任务前源码全文一致（仅归一 CRLF/LF 和文件末尾空行）；Prefab、音轨/SFX、炮弹、提示、Key、失败/退出、公共 API、共享 AudioManager 和生成存档文件均未改动，无新增持续分配或逐帧处理。
- 隔离测试：提取修改前后生产方法 `Start/InitializeIdleState/PlayBattleSoundOnce/EnterSuccess/EnterSurvivalCleared`，使用 Unity/音频副作用桩执行 37 项断言通过。修改前四种开关组合中三种错误恢复已复现；修改后四组合、开战前切换、一次音效/暂停、重复成功、Idle 重新初始化、新实例采样、失败/死亡拒绝和编辑态 DB 零读取通过。该证据不代表真实音轨播放或 Unity 生命周期实测；首轮全文比较受混合 CRLF/LF 影响失败，归一测试输入后重跑通过，生产逻辑未为该误报调整。
- 编译：Unity 6000.0.59f2 自带 Roslyn 读取现有 Editor Bee `Assembly-CSharp.rsp`，输出到独立 TEMP 路径，结果 `0 error / 113 warning`，Level69 无诊断；未覆写正式 Bee 程序集。定向 `git diff --check` 通过。
- 验证脚本和日志：`C:/Users/admin/AppData/Local/Temp/brainpunk-level069-music-20261010/verify.ps1`、`verify.log`、`compile.log`。本轮不启动新 Unity 实例、不操作 Unity 窗口、不提交 Git。
- `uncertain`：尚未执行 Unity PlayMode、真实音轨暂停/恢复、设置事件、Reset/Replay 及 Android/iOS 听感验收。待手工验证：音乐关闭后入关并完成倒计时不响背景音乐；音乐开启时可恢复；关内关闭后保持关闭；重开取得新开关；持续按住胜利提示、残留炮弹和退出音效保持原表现。
- 回滚：仅移除 `_musicOnAtLevelEntry` 与其注释、`Start()` 新增运行态门禁/采样，恢复成功处无条件 `ResumeMusic()`。不整份还原关卡、Prefab 或存档；该回滚会重新引入音乐关闭时被恢复的问题。

## 2026-06-26：FakeSystemTips Collider 生命周期控制

### 需求/背景

`FakeSystemTips` 播放 Out 时需要关闭 Collider，避免退出动画期间仍保留可碰撞/可交互区域；In 动画完成后再打开 Collider，保证进入动画未完成前不提前参与交互。

### 受影响文件和系统

| 文件/系统 | 影响说明 |
|------|------|
| `Assets/Game/Scripts/Level/LevelItem/Level_69/FakeSystemTips.cs` | 缓存 `target` 子层级 `Collider2D`，并按 In/Out 动画生命周期开关 |
| `Assets/Game/Prefabs/Level/Level_069.prefab` | 本次未修改；静态文本显示 `FakeSystemTips` 根对象挂有 `BoxCollider2D`，脚本 `target` 指向自身 Transform |
| `BtnArea` / `pressInput` | 不在 `FakeSystemTips.target` 子层级内，本次不控制按钮输入 Collider |

### 行为变化

**修改前：**

- `FakeSystemTips` 只控制位移和透明度，不主动切换 Collider。
- Prefab 中 `FakeSystemTips` 根对象的 `BoxCollider2D` 默认启用；Out 动画期间可能仍保留碰撞区域。

**修改后：**

- 初始化时缓存 `target.GetComponentsInChildren<Collider2D>(true)`。
- `Start()` 中如果当前不是 In 状态，会把透明度置 0 并关闭缓存到的 Collider。
- `PlayIn()` 开始时先关闭 Collider，进入动画完成且状态仍为 In 时才打开 Collider。
- `PlayOut()` 开始时立即关闭 Collider；重复调用 Out 时也会确保 Collider 关闭，然后退出。
- Out 动画完成后仍按原逻辑 `gameObject.SetActive(false)`。

### 数据/资源/事件流补充

```
FakeSystemTips.Awake/Start()
    └──► 缓存 target 子层级 Collider2D

PlayIn()
    ├──► 关闭 Collider
    ├──► 播放进入位移和透明度动画
    └──► OnComplete 且 _isIn 为 true 时打开 Collider

PlayOut()
    ├──► 关闭 Collider
    ├──► 播放退出位移和透明度动画
    └──► OnComplete 后隐藏 FakeSystemTips
```

### 验证记录

- 静态确认 `FakeSystemTips.cs` 新增 `_colliders` 缓存与 `SetCollidersEnabled(bool)`，未修改现有注释内容。
- 静态确认 `PlayIn()` 开始关闭 Collider，In 完成且 `_isIn` 仍为 true 时打开 Collider。
- 静态确认 `PlayOut()` 开始关闭 Collider，且重复 Out 调用也会关闭 Collider 后返回。
- 静态确认 `Level_069.prefab` 中 `FakeSystemTips` 根对象挂有 `BoxCollider2D`，脚本 `target` 指向自身 Transform；本次未修改 prefab。
- 手动尾随空白检查覆盖 `FakeSystemTips.cs`、`Level69功能说明.md`、`Level69风险评估.md` 和 `DevelopMD/已有功能设计与使用记录.md`，未发现尾随空白；`git diff --check` 对当前已跟踪修改返回 0，仅提示 `DevelopMD/已有功能设计与使用记录.md` 工作区 LF/CRLF 转换。当前 Level69 脚本和本地文档在工作区仍为未跟踪文件，纳入 Git 后需复跑完整 diff check。
- 静态风险扫描本次新增代码只命中 `Collider2D`；未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、PlayerPrefs、StreamingAssets、YooAsset、analytics、广告或 IAP 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，原因是 `Temp/bin/Debug` 缺少多个项目引用 DLL 元数据文件，未得到完整 Unity 工程编译结论。

### 剩余风险/未验证缺口

- 未在 Unity PlayMode 验证 Out 动画起始帧 Collider 是否立即关闭、In 动画完成帧是否按预期打开。
- `target` 子层级内若后续新增其他 Collider，也会被同一逻辑统一开关；目前需按 prefab 层级确认是否符合设计。
- 如果策划希望 In 动画过程中已经可碰撞/可交互，当前逻辑会延迟到 In 完成后才打开。
- 未在 Android/iOS 横屏触摸路径验证快速长按、松手、重复按压和成功自动 Out 时的 Collider 状态。

### 回滚路径

1. 从 `FakeSystemTips.cs` 移除 `_colliders`、Collider 缓存、`SetCollidersEnabled(bool)` 以及 `PlayIn()` / `PlayOut()` / `Start()` 中的 Collider 开关调用。
2. 删除本节文档、风险评估和 `DevelopMD` 中对应记录。

## 2026-06-26：countdown_txt 剩余时间显示

### 需求/背景

关卡新增 `countdown_txt` 文本，用于显示 `Playing` 状态剩余时间。显示格式按用户澄清为 `ss:ms`，例如 `playingDuration=20` 时显示 `20:00`，倒计时过程中会显示两位毫秒位。

### 受影响文件和系统

| 文件/系统 | 影响说明 |
|------|------|
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` | 接入 `TMP_Text countdown_txt` 的缓存和倒计时刷新 |
| `Assets/Game/Prefabs/Level/Level_069.prefab` | 本次未修改；脚本支持 Inspector 绑定，也会在子节点中按名称兜底查找 `countdown_txt` |
| TextMeshPro | 仅使用已有 `TMP_Text` 文本组件，不新增资源加载或 UI 面板流程 |

### 行为变化

**修改前：**

- `Level69Ctrl` 只维护 `_playingTimer`，没有把剩余时间同步到文本 UI。

**修改后：**

- `Awake()` 中如果 `countdown_txt` 未绑定，会在当前关卡子节点中查找同名 `TMP_Text`。
- `Start()` 和进入 `Playing` 时显示完整 `playingDuration`。
- `Playing` 期间按百分之一秒刷新剩余时间，只有显示值变化时才写入 `countdown_txt.text`。
- 进入 `Success` 时强制显示 `00:00`。
- `countdown_txt` 缺失时直接跳过刷新，不影响导弹、输入、成功链或关卡流程。

### 验证记录

- 静态确认 `Level69Ctrl.cs` 已引用 `TMPro`，`countdown_txt` 为 `TMP_Text` 字段。
- 静态确认脚本新增 `CacheCountdownTextReference()`、`GetRemainingPlayingTime()`、`UpdateCountdownText(...)`，未修改顶部需求注释。
- 静态扫描本次新增倒计时代码未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、PlayerPrefs、StreamingAssets、YooAsset、analytics、广告或 IAP 链路。
- `git diff --check` 通过，仅提示当前工作区 LF/CRLF 转换。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，原因是 `Temp/bin/Debug` 缺少多个项目引用 DLL 元数据文件，未得到完整 Unity 工程编译结论。

### 剩余风险/未验证缺口

- 未在 Unity PlayMode 验证 `countdown_txt` 实际绑定或同名兜底查找是否命中用户新增文本节点。
- 未在运行时确认两位毫秒位是否满足最终视觉精度需求；当前按百分之一秒显示。
- 未验证 TextMeshPro 字体、层级、缩放和横屏设备上显示是否清晰无遮挡。
- 未完成完整 C# 工程编译验证；需要 Unity Editor 重新生成/编译或补齐 `Temp/bin/Debug` 依赖后复跑。

### 回滚路径

1. 从 `Level69Ctrl.cs` 移除 `countdown_txt` 相关字段、缓存和刷新方法。
2. 删除本节文档、风险评估和 `DevelopMD` 中对应记录。

## 2026-06-26：version2.0 spawnMissile 三阶段优化

### 需求/背景

`Level69Ctrl.cs` 顶部 `version2.0` 注释要求优化 `spawnmissile` 逻辑：导弹生成拆成三个阶段，阶段一固定三个 X 位置、阶段二沿 A/B 固定路径往返一次、阶段三在范围内随机 X 位置生成，直到 `Playing` 结束。本次实现保留顶部需求注释原文，不修改已有注释内容。

### 受影响文件和系统

| 文件/系统 | 影响说明 |
|------|------|
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` | 新增 Level69 本地三阶段导弹生成参数、阶段状态和 X 坐标采样逻辑 |
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69MissileCtrl.cs` | 未修改；继续负责导弹下落、碰撞特效和对象池回收 |
| `Assets/Game/Prefabs/Level/Level_069.prefab` | 未修改；新增序列化字段使用脚本默认值，后续可在 Inspector 调整 X 坐标和间隔 |
| `ObjectPoolManager` | 未修改；继续由 `Level69Ctrl` 注册、获取和回收 `Level69MissileCtrl` |

### 行为变化

**修改前：**

- `Playing` 期间只按单一 `missileSpawnInterval` 从 `startPoint` 位置生成导弹。
- `version2.0` 注释中的三阶段生成节奏尚未接入运行逻辑。

**修改后：**

- 进入 `Playing` 后重置导弹生成阶段，第一发保持立即生成。
- 阶段一依次使用 `phaseOneSpawnX1`、`phaseOneSpawnX2`、`phaseOneSpawnX3` 三个世界 X 坐标，每次生成间隔由 `phaseOneSpawnInterval` 控制，默认 1.5 秒。
- 阶段二使用 `phaseTwoPointAX` 到 `phaseTwoPointBX` 再返回 A 的 X 轴路径，单程时长由 `phaseTwoOneWayDuration` 控制，默认 2 秒；生成间隔由 `phaseTwoSpawnInterval` 控制，默认 0.3 秒。
- 阶段三在 `phaseThreeMinX` 到 `phaseThreeMaxX` 范围内随机 X 坐标生成，生成间隔由 `phaseThreeSpawnInterval` 控制，默认 0.2 秒，直到 `Playing` 计时结束。
- 所有阶段只覆盖导弹生成点的世界 X 坐标，Y/Z 仍每次从 `startPoint` 或导弹初始位置读取，导弹下落、碰撞、回收、成功链和输入逻辑不变。

### 数据/资源/事件流补充

```
OnEnterPlaying()
    ├──► RegisterObjectPool()
    └──► ResetMissileSpawnPhases()
            └──► FixedPoints，第一发立即生成

UpdateMissileSpawn()
    ├──► FixedPoints: 三个固定 X 点依次生成
    ├──► MovingPath: A -> B -> A 路径采样生成
    └──► RandomRange: 随机 X 生成直到 Playing 结束

SpawnMissile()
    ├──► GetStartPointPosition() 读取当前 Y/Z
    ├──► 按当前阶段覆盖 X
    ├──► ObjectPoolManager.Get<Level69MissileCtrl>()
    └──► missile.Init()
```

### 验证记录

- 静态确认 `Level69Ctrl.cs` 顶部 `version1.0` / `version2.0` 需求注释未改写。
- 静态确认 `Level69Ctrl.cs` 新增字段集中在 `导弹生成阶段设置`，未修改 `Level69MissileCtrl.cs`、`Level_069.prefab`、对象池、存档、配置、资源加载、热更、广告、内购、analytics 或平台 SDK。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs ...` 通过，仅提示既有 `DevelopMD` 文件 LF/CRLF 工作区转换。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 在 120 秒内超时，未获得编译结果；已停止本次超时派生的 `dotnet build` 进程。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，原因是 `Temp/bin/Debug` 缺少多个项目引用 DLL 元数据文件，未得到完整 Unity 工程编译结论。

### 剩余风险/未验证缺口

- 未在 Unity PlayMode 验证三阶段实际节奏、X 坐标范围、第一发立即生成、阶段二 A/B 往返 4 秒边界和阶段三随机表现。
- 未完成完整 C# 工程编译验证；需要 Unity Editor 重新生成/编译或补齐 `Temp/bin/Debug` 依赖后复跑。
- 未验证 Inspector 中新增序列化字段的美术/策划最终取值；当前默认值只是可运行占位。
- 未验证 Android/iOS 横屏触摸长按期间，0.2 秒随机生成间隔对低端设备帧稳定性的影响。
- `missileSpawnInterval` 旧字段仍保留用于序列化兼容，但 v2.0 主路径改由三个阶段的独立间隔控制；如已有 prefab 后续打开保存，需要确认 Inspector 展示与调参预期一致。

### 回滚路径

1. 回退 `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` 中 `导弹生成阶段设置` 字段、`MissileSpawnPhase` 状态和三阶段生成方法。
2. 恢复 `Update()` 中按 `missileSpawnInterval` 调用 `SpawnMissile()` 的旧单点生成逻辑。
3. 删除本节文档和风险评估中的 2026-06-26 记录。

## 需求/背景

根据 `Level69Ctrl.cs` 顶部注释实现第 69 关核心逻辑：

1. 游戏状态分为 `Idle`、`Playing`、`Success` 三个阶段。
2. 长按按钮时显示 `FakeSystemTips` 并停留在 In 状态；松手后播放 Out 动画。
3. `Playing` 状态通过 `ObjectPoolManager` 持续生成导弹，导弹从 `StartPoint` 位置开始向下移动。
4. `Playing` 状态持续 20 秒，结束后进入 `Success` 状态，自动关闭 `FakeSystemTips` 并调用真正的 `SystemTips`。
5. `Level69MissileCtrl` 默认向下移动，速度可调。

## 受影响的文件和系统

| 文件 | 说明 |
|------|------|
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` | 关卡主控制器，实现状态机、输入响应、导弹生成、计时与成功判定 |
| `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69MissileCtrl.cs` | 导弹逻辑：下移、碰撞特效、对象池回收 |
| `Assets/Game/Scripts/Level/LevelItem/Level_69/FakeSystemTips.cs` | 新增：模拟 SystemTips，In/Out 动画分离，In 状态保持 |
| `Assets/Game/Prefabs/Level/Level_069.prefab` | 需要在 Inspector 中补充新字段引用（见下方“配置说明”） |

使用的现有系统：

- `Bear.EventSystem`：成功时派发 `SwitchGameStateEvent`。
- `Game.Common.ObjectPoolManager`：导弹对象池注册与回收。
- `Game.UI.Global.SystemTips`：成功时显示真正提示。
- `PressItemEventHandle` / `ClickItemEventHandle`：输入检测。

## 行为变化

**修改前：**

- `Level69Ctrl` 已有状态机、导弹生成和成功派发逻辑，但第一次按下按钮时会先进入 `Playing` 并提前返回，导致 `FakeSystemTips.PlayIn()` 和 `pressBtn` 显示不会在首按触发。
- `OnEnterSuccess()` 的延迟成功派发没有随关卡禁用/销毁取消，快速退出或重开时存在过期成功事件风险。
- `pressBtn.SetActive(...)` 没有空引用兜底，缺失绑定时会直接报错。
- `OnButtonClick(Transform, Vector2)` 为私有方法，不适合作为 `ClickItemEventHandle` 的 UnityEvent 兼容入口。

**修改后：**

- 按钮按下 -> `FakeSystemTips.PlayIn()` -> 状态进入 `Playing`。
- `Playing` 期间每 `missileSpawnInterval` 秒从 `startPoint` 生成导弹。
- 导弹以 `Level69MissileCtrl.moveSpeed` 向下移动；触碰 `Player`/`Actor` 播放 `explosion1`，触碰 `Ground` 播放 `explosion2`。
- 20 秒后自动进入 `Success`：回收活跃导弹、`FakeSystemTips.PlayOut()`、隐藏 `pressBtn`、`SystemTips.Show()`，延迟后优先调用 `BaseLevelCtrl.SuccessStrightly()`，缺失 `BaseLevelCtrl` 时才兜底派发 `SwitchGameStateEvent.SUCCESS`。
- 按钮松开 -> `FakeSystemTips.PlayOut()`。
- 成功延迟任务会在进入 Playing、关卡禁用或销毁时取消，避免旧异步回调跨生命周期触发。
- `Level_069.prefab` 已绑定 `PressItemEventHandle.OnPressEvent` / `OnCancelPressEvent`，脚本只在没有持久绑定当前目标方法时动态补监听，避免重复回调。
- `FakeSystemTips` 兼容两种动画配置：配置 `startPoint/idlePoint/endPoint` 时按点位移动；未配置点位时回退到原始位置加 `inEndOffset/outEndOffset`，匹配当前 prefab 已序列化的旧字段。

## Tasks 归纳

> 当前 `.specify/feature.json` 指向 `specs/030-level67-parachute-control`，不是 Level69。本次未生成或覆盖 Speckit `tasks.md`，以下为 Level69 本地执行清单。

- [x] T001 [US1] 静态确认 `Assets/Game/Prefabs/Level/Level_069.prefab` 的 `Level69Ctrl`、`pressInput`、`pressBtn`、`startPoint`、`fakeSystemTips`、导弹和按钮事件绑定。
- [x] T002 [US1] 修复 `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` 首次按下按钮不显示 `FakeSystemTips` 的状态切换顺序。
- [x] T003 [US1] 在 `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` 中为 `pressBtn` 显隐增加空引用兜底。
- [x] T004 [US1] 将 `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` 的 `OnButtonClick(Transform, Vector2)` 调整为 UnityEvent 可绑定的 public 入口，兼容 `ClickItemEventHandle`。
- [x] T005 [US2] 在 `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` 中取消重复动态监听，避免 prefab 已绑定当前目标方法时再由脚本 AddListener 双重触发。
- [x] T006 [US2] 在 `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` 中为成功延迟增加销毁/禁用取消，防止过期成功回调。
- [x] T007 [US2] 将 `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` 成功完成入口优先接入 `BaseLevelCtrl.SuccessStrightly()`，保留 `SwitchGameStateEvent.SUCCESS` 兜底。
- [x] T008 [US2] 成功进入时通过 `Level69MissileCtrl.RecycleAllActive()` 回收活跃导弹并隐藏 `pressBtn`。
- [x] T009 [US2] 修复 `Assets/Game/Scripts/Level/LevelItem/Level_69/FakeSystemTips.cs` 的点位/offset 兼容，避免旧 prefab 未绑定点位时空引用或编译失败。
- [x] T010 更新 `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69功能说明.md` 和 `Level69风险评估.md` 的中文记录。
- [ ] T011 在 Unity PlayMode 中验证 `Level_069.prefab` 长按/松手、FakeSystemTips In/Out、导弹生成/碰撞、20 秒成功、快速退出/重开。
- [ ] T012 在 Android/iOS 横屏触摸路径上验证长按释放、成功延迟和导弹生成性能。

## 数据/资源/事件流

```
玩家按下按钮
    │
    ▼
Level69Ctrl.OnButtonPressed()
    │
    ├──► FakeSystemTips.PlayIn()      // 显示并保持 In 状态
    │
    ▼
SetState(Playing)
    │
    ├──► ObjectPoolManager.RegisterPool<Level69MissileCtrl>()
    │
    ▼
Update() 每帧计时 + 生成导弹
    │
    ├──► ObjectPoolManager.Get<Level69MissileCtrl>()
    ├──► missile.transform.position = startPoint.position
    └──► missile.Init()
            │
            ▼
    Level69MissileCtrl.Update() 向下移动
            │
            ├──► 触碰 Player -> explosion1 -> RecycleSelf()
            └──► 触碰 Ground -> explosion2 -> RecycleSelf()

20 秒到达
    │
    ▼
Level69Ctrl.EnterSuccess()
    │
    ├──► Level69MissileCtrl.RecycleAllActive()
    ├──► FakeSystemTips.PlayOut()
    ├──► pressBtn.SetActive(false)
    ├──► SystemTips.Show(successTipsContent)
    └──► successDelay 后 BaseLevelCtrl.SuccessStrightly()
            └──► 缺失 BaseLevelCtrl 时兜底 DispatchEvent(SwitchGameStateEvent, SUCCESS)

关卡被禁用/销毁
    │
    ▼
Level69Ctrl.OnDisable()
    │
    └──► Level69MissileCtrl.RecycleAllActive()  // 统一回收所有活跃导弹
```

## 配置说明（需要在 Prefab 中补充）

在 `Level_069.prefab` 的 `Level69Ctrl` 组件上配置：

| 字段 | 建议引用 |
|------|----------|
| `missile` | 场景中的 `Missile` GameObject（已存在引用，保持即可） |
| `startPoint` | 新建一个空节点 `StartPoint`，放在希望导弹生成的位置；不填则使用 `Missile` 初始位置 |
| `fakeSystemTips` | `FakeSystemTips` GameObject，并为其挂载 `FakeSystemTips.cs` 脚本 |
| `pressInput` | `BtnArea` 上挂载的 `PressItemEventHandle`（推荐新增） |
| `clickInput` | `BtnArea` 上已有的 `ClickItemEventHandle`（备用，仅触发按下） |
| `successTipsContent` | 成功时 `SystemTips` 显示的内容 |

当前 `Level_069.prefab` 已在 `BtnArea` 上配置 `PressItemEventHandle`，并将 `OnPressEvent` / `OnCancelPressEvent` 分别绑定到 `Level69Ctrl.OnButtonPressed()` / `OnButtonReleased()`。脚本会在没有持久绑定当前目标方法的测试场景中动态补监听，但不会在已有 prefab 绑定上重复添加。

## 验证

- 静态检查 `Level_069.prefab` 已绑定 `Level69Ctrl.missile`、`startPoint`、`fakeSystemTips`、`pressInput`、`pressBtn`、`successDelay` 和 `successTipsContent`。
- 静态检查 `BtnArea` 的 `PressItemEventHandle.OnPressEvent` / `OnCancelPressEvent` 已绑定 `Level69Ctrl.OnButtonPressed()` / `OnButtonReleased()`。
- 静态扫描未发现 Level69 本次新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets` 或热更链路。
- 第一次 `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 未完成：缺少 `Temp\obj\Assembly-CSharp\project.assets.json`。
- 允许 restore 的 `dotnet build .\Assembly-CSharp.csproj ...` 在 120 秒内未完成，已清理该次 build 派生的 dotnet/MSBuild 进程。
- restore 产物生成后，第二次 `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 暴露 `FakeSystemTips.cs` 的 `_originalPosition` 编译错误；已修复。
- 最终 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 96 warning；warning 属于当前工程既有范围。

## 剩余风险/未验证缺口

- 未在 Unity 运行时验证 Prefab 引用绑定、动画表现和导弹碰撞。
- 未验证 `BaseLevelCtrl.SuccessStrightly()` 成功链在 Level69 中是否播放预期成功动画、音效、存档/解锁和下一关流程。
- `FakeSystemTips` 的 In/Out 偏移、持续时间需要根据美术资源在 Inspector 中微调。
- 成功时 `SystemTips` 显示内容当前为占位字符串 `"Success!"`，需策划确认。
- 未验证导弹数量、生成间隔对性能的影响。

## 回滚路径

1. 还原以下文件到修改前版本：
   - `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs`
   - `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69MissileCtrl.cs`
2. 删除新增文件：
   - `Assets/Game/Scripts/Level/LevelItem/Level_69/FakeSystemTips.cs`
   - `Assets/Game/Scripts/Level/LevelItem/Level_69/FakeSystemTips.cs.meta`
   - `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69功能说明.md`
   - `Assets/Game/Scripts/Level/LevelItem/Level_69/Level69风险评估.md`
3. 在 `Level_069.prefab` 上移除新增的脚本引用和组件绑定。

## 2026-07-10：version3.0 实现基线与验证清单

### 需求与原始行为基线

- 本次按 `specs/036-level69-v3-logic` 实现：点击初始按钮开始 20 秒生存；玩家按住 `FakeSystemTips` 本体形成盾牌，松手后可通过按钮召回；存活后必须触碰钥匙，等待钥匙飞向门并完成开锁，最后由玩家进入门触发既有通关。
- 实现前 `Level69Ctrl.cs` 已有未验证的 3.0 草稿：BtnArea 仍承担长按；按钮会被隐藏；钥匙通过 `Instantiate` 创建后禁用全部交互并自动完成 event 1；该草稿不是验收基线。
- version2.0 验收基线保持：20 秒倒计时；固定三点、A-B-A、随机范围三阶段；默认间隔 1.5/0.3/0.2 秒；Y/Z 每次读取 `startPoint`；对象池初始 3、上限 30；导弹速度继续读取现有 `moveSpeed`。
- 源码 version1.0/version2.0/verison 3.0 注释块实现前 SHA-256：`70f0960979137956394653d120b9caf845979950a986ddf59e425a2bf925c94e`，后续必须保持一致。

### 实现验证清单

- 全新进入、首次点击、快速连点、多指开始、开场提前按住、错过停顿窗口。
- 防御按住、松手、移出、取消、应用暂停、按钮召回、停顿窗口内外恢复。
- 固定点/A-B-A/随机阶段，以及盾牌、非防御输入区、地面、Actor、同物理步优先级和对象池复用碰撞。
- 倒计时/受击同帧仲裁，失败、重开、退出、禁用、销毁后的残留状态。
- 成功提示、中央钥匙下落、未触碰不飞、首次触碰、飞向 LockPoint、锁动画、event 1、玩家进门最终成功。
- Unity 编译、静态生命周期/作用域扫描、注释哈希、Android/iOS 横屏触控和 Profiler；无法执行的项目必须记录为未验证，不得写成通过。

## 2026-07-10：version3.0 实现结果

### 受影响文件与系统

| 文件/资源 | 变更 |
|---|---|
| `Level69Ctrl.cs` | 按 3.0 重写本关会话、提示、防御、失败仲裁、钥匙、锁、门和统一清理状态机；已有注释内容与字节哈希保持不变。 |
| `Level69HoldInput.cs` | 新增 Level69 本地单 owner 手指跟踪，支持松开、移出、Cancel、暂停、禁用和销毁边界。 |
| `FakeSystemTips.cs` | In/Out 提供当前序列完成回调；输入区域与盾牌碰撞体分离；仅 `Defending` 启用盾牌。 |
| `Level69MissileCtrl.cs` | 池化实例每次重置碰撞状态；盾牌/地面/Actor 单次结算；同帧 Actor 命中延后到 LateUpdate，由盾牌优先。 |
| `Level_069.prefab` | BtnArea 改为 click-only；新增 HoldInputArea；移除 Missile 重复 Trap 回调；配置本地 inactive Key、DoorLock 和 `door_0/LockPoint`。 |

共享 `ClickItemEventHandle`、`PressItemEventHandle`、Key/DoorLock prefab、`BaseLevelCtrl`、`PlayCtrl`、`GamePlayPanel`、存档、配置、资源地址、SDK 和全局事件定义均未修改。

### 玩家可见行为

- 初始按钮保持显示；首次点击只启动一次 20 秒计时和开场提示。提示进入时可以提前按住，抵达中间点后进入 0.7 秒窗口；持续按住后盾牌成立，未按住则提示退出后开始轰炸。
- 轰炸期间按钮始终可见。防御有效时按钮不可点击；owner 松开、取消、移出或应用暂停会立即关闭盾牌并允许按钮召回。召回时只有中间停顿窗口内新开始的按住可恢复防御。
- version2.0 固定三点、A-B-A、随机范围顺序和 1.5/0.3/0.2 秒默认间隔保持；每次生成仍读取 startPoint 的 Y/Z，对象池上限保持 30。
- 导弹触碰有效盾牌或地面只爆炸回收；触碰 Actor 时只提交一次死亡、炸飞和既有失败重开。倒计时归零通过 LateUpdate 与导弹命中仲裁，不能同时成功和失败。
- 存活后显示“恭喜你坚持到底了！”，本地 Key 从屏幕中心下落；落地前不可触碰。第一次 Player 接触后 Key 通过现有 `ItemJumpToListener` 飞向 `door_0/LockPoint`，等待本地 DoorLock 的 `unlock` 动画完成后只派发 event 1。最终成功仍必须由玩家进入门并经过 `BaseLevelCtrl.OnActorTrigger2D`。

### 输入、对象与事件流

```text
BtnArea Click -> Idle/Recall 仲裁
  -> FakeSystemTips In -> HoldWindow
     -> owner 持续按住 -> Defending -> ShieldCollider enabled
     -> 未按住/松开 -> Out -> BtnArea 可召回

Level69Ctrl version2.0 schedule -> ObjectPoolManager -> Level69MissileCtrl
  -> Shield/Ground -> explosion2 + recycle
  -> Actor -> explosion1 + Actor.Die/Knockback -> BaseLevelCtrl 失败重开

Timer 归零且无失败 -> SurvivalCleared -> SystemTips
  -> Key drop -> Player contact -> ItemJumpToListener -> LockPoint
  -> DoorLock PlaySpineListener 完成 -> OnTiggerItemEvent(1)
  -> door_0 enabled/open -> Player enter -> BaseLevelCtrl 最终成功
```

### 已完成验证

- Unity 6000.0.59f2 batchmode 已完成资源导入并进入 ScriptAssemblies；`Level_069.prefab` 没有 YAML/import 报错，Level69 四个脚本没有出现在编译错误列表中。
- 整体编译未通过：工作树现有 UniTask/YooAsset 扩展缺少 UniTask 类型，`Assets/Guru/Gen/GuruAllSpecs.G.cs` 缺少 Guru 类型；因此不能把本次记录为 Unity 编译通过。
- version 注释块 SHA-256 复核仍为 `70f0960979137956394653d120b9caf845979950a986ddf59e425a2bf925c94e`。
- 静态检查确认活动 3.0 代码没有 `SuccessStrightly`/`SwitchGameStateEvent.SUCCESS`，没有新增编辑器生命周期、DB/save、RemoteConfig、PlayerPrefs、资源地址、全局事件或共享面板路径。
- Prefab 静态引用检查通过：无重复 fileID/缺失本地 fileID；BtnArea 无 Press 组件；Missile 无重复 Trap/OnTrigger 回调；Key、DoorLock、LockPoint、动画、Actor、倒计时和池参数引用可定位。

### 剩余风险与回滚

- Unity 项目级依赖修复后仍需重新编译，并在 PlayMode 完成开始/召回、20 组碰撞、20 组钥匙门、每阶段 10 次重开退出验证。
- Android/iOS 横屏真机触控、多指、后台恢复、60 fps/GC/池峰值未验证；当前仅完成代码和 prefab 静态检查。
- Key 下落位置、DoorLock 在 `LockPoint` 的视觉对齐、炸飞力度 12、0.7 秒窗口手感需要策划/美术在 PlayMode 调整。
- 回滚时只回退 Level69 四个脚本、新增 HoldInput meta、`Level_069.prefab` 本次本地 override 和本节中文文档；共享 Key/DoorLock/门/BaseLevel/save/SDK 不需要回滚。

## 2026-07-10：倒计时与战斗音效节拍

### 需求与行为变化

- 修改前，`l69countdown` 由倒计时文本的厘秒刷新直接触发，一秒内可能重复播放多次；`l69battle` 位于按钮召回入口，每次召回都可能重新播放，也没有保存可停止的音效句柄。
- 修改后，倒计时文本继续按 `ss:ms` 刷新，音效改为只在剩余整数秒发生变化时播放一次。开始一局时立即播放当前秒的 `l69countdown`，之后按 1 秒边界播放，低帧率跨过边界时不会补播一串历史音效。
- `l69battle` 只在 `BeginSession()` 中启动一次，并保存 `AudioManager.PlaySound(...)` 返回的 `Sound`；同一局内召回 `FakeSystemTips` 只保留既有 `l69button`，不会再次启动 battle。
- `OnDisable()` 和 `OnDestroy()` 都经过 `CleanupSession()`，由该清理链停止并清空 battle 句柄，避免退出或销毁关卡后继续播放。新一局会重置一次性标记。

### 影响范围与系统流

- 仅修改 `Level69Ctrl.cs` 和本地中文文档；未修改 `Level_069.prefab`、Sounds Good 音频资源、`AudioManager`、配置、存档、事件、analytics 或平台 SDK。
- `l69battle`、`l69button`、`l69countdown` 已在 `SFX_Generated.cs` 与 `SoundCollection.asset` 中存在。
- 流程：`BeginSession -> l69countdown + l69battle`；`Update -> 剩余整数秒变化 -> l69countdown`；`OnDisable/OnDestroy -> CleanupSession -> Sound.Stop()`。

### 验证、剩余风险与回滚

- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_69/Level69Ctrl.cs` 通过，仅有 LF/CRLF 转换提示。
- 静态调用扫描确认 `l69battle` 只有一个播放入口，`l69countdown` 只有一个节拍入口，battle 停止入口已接入统一清理链。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 98 warnings。
- Unity PlayMode、SFX 开关切换、20 秒实际音效次数、低帧率跨秒、失败/成功后停留、关卡销毁及 Android/iOS 横屏真机听感仍待验证。
- 回滚时删除 `_lastPlayedCountdownSecond`、battle 句柄与一次性标记、`UpdateCountdownSound(...)`、`PlayBattleSoundOnce()`、`StopBattleSound()` 及其调用，恢复原音效触发位置；不需要修改音频资源或 prefab。

## 2026-07-13：pressBtn 按压态显示修复

### 需求与原始行为基线

- `pressBtn` 是 `Sp_l69_btn_02` 按下态视觉节点，prefab 中默认隐藏。
- 修改前，`SetButtonState(visible, interactable)` 同时控制 `pressBtn` 显隐和 `clickInput` 可点击状态；关卡状态刷新、失败、存活、清理等内部流程都可能改变 `pressBtn`，导致它不再只表达当前物理按钮是否被按住。
- 用户确认内置按钮逻辑表现正常，本次只修正额外按下态节点：按下显示，松开隐藏。

### 影响范围与修改后行为

- 仅修改 `Level69Ctrl.cs` 的 Level69 本地按钮表现与本地中文记录；未修改 `Level_069.prefab`、共享输入组件、关卡状态、导弹、提示、钥匙、门、存档、资源或事件链。
- `OnButtonPressed()` 完成原有 `OnButtonClick(...)` 调用后显示 `pressBtn`；`OnButtonReleased()` 保留原有防御退出逻辑并隐藏 `pressBtn`。
- 原 `SetButtonState(...)` 拆分为 `SetPressButtonVisible(...)` 与 `SetButtonInteractable(...)`。关卡状态机只继续维护 `clickInput` 可点击性，不再通过状态刷新改变 `pressBtn`。
- 序列化字段 Tooltip 原文“轰炸期间始终显示”已与本次确认行为冲突，因此同步改为“仅在物理按钮按下期间显示，松开时隐藏”；未改写其他已有注释。

### 输入与表现流

```text
OnButtonPressed
  -> 原有 OnButtonClick 状态/提示逻辑
  -> pressBtn 显示

OnButtonReleased
  -> 原有 Defending 退出逻辑
  -> pressBtn 隐藏

关卡状态刷新/失败/存活/清理
  -> 只更新 clickInput 可点击性
  -> 不改变 pressBtn 显隐
```

### 验证、未验证缺口与回滚

- 静态检查确认 `pressBtn.SetActive(...)` 只封装在 `SetPressButtonVisible(...)`，其调用入口仅为按下与松开回调；其余原 `SetButtonState(...)` 调用已改为只更新可点击状态。
- Prefab 静态检查确认 `pressBtn` 指向 `Sp_l69_btn_02`，该节点 `m_IsActive: 0`，无需修改 prefab 初始值。
- `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。`Assembly-CSharp.csproj` 编译被 `Temp/bin/Debug` 缺少 191 个 Unity/包引用 DLL 阻断，未进入本次脚本的有效编译诊断。
- Unity PlayMode、Android/iOS 横屏触摸、移出/Cancel/应用暂停时是否一定触发 release 仍未验证。
- 回滚时恢复 `SetButtonState(visible, interactable)` 对 `pressBtn` 与 `clickInput` 的联合控制，并删除本节及对应风险/共享记录；无需回滚 prefab 或共享输入脚本。

## 2026-07-13：BtnArea Drag 输入迁移收口

### 需求与原始行为基线

- BtnArea 已由 `ClickItemEventHandle` 改成 `DragEventsListener`，并在 prefab 中绑定 BeginDrag/EndDrag，但 `Level69Ctrl` 仍保留空的 `clickInput` 必填校验和点击禁用调用，导致关卡继续被安全校验阻止。
- `OnButtonReleased()` 同时隐藏按下态和退出 `Defending`，会让 BtnArea 手指与 `Level69HoldInput` 防御 owner 相互干扰。
- Level69Key 的 `OnTrigger2DHandle` 恢复后，默认 `TriggerExecutor` 持久回调与运行时 `OnKeyContact()` 同时存在，可能让共享 Key 默认执行链和本关钥匙流程并行。

### 影响范围与修改后行为

- `Level69Ctrl.clickInput` 替换为本关 `DragEventsListener dragInput`，prefab 绑定到 BtnArea 当前 Drag 组件；必填校验和错误文本同步改为按钮拖拽输入。
- `SetButtonInteractable(...)` 只维护 Level69 本地 `_buttonInteractable`。无效阶段收到 BeginDrag 时直接忽略，不显示 `pressBtn`、不开始或召回；有效按压开始后允许 Drag 继续跟踪到真实 End/Cancel/移出，避免状态切换导致按下态提前结束。
- BeginDrag 有效时先显示 `pressBtn`，再进入原有开始/召回逻辑；EndDrag 只隐藏 `pressBtn`。防御结束唯一保留在 `Level69HoldInput.HoldEnded -> OnPromptHoldEnded()`。
- `OnDisable()` 通过 `SetEnableDrag(false)` 结束当前 Drag，`CleanupSession(...)` 再主动隐藏 `pressBtn`；`OnEnable()` 恢复 Drag 监听，覆盖关卡禁用、销毁或重开时未正常投递 End 的生命周期边界。
- Level69Key 保留 `OnTrigger2DHandle` 作为碰撞转发器，但其 prefab 持久调用数量清为 0；只由 `RegisterLocalInputEvents()` 运行时注册一次 `OnKeyContact()`。
- 未修改共享 `DragEventsListener`、共享 Key prefab、`TriggerExecutor`、`Level69HoldInput`、存档、资源、全局事件或平台 SDK。

### 输入与 Key 流

```text
BtnArea Drag Begin
  -> _buttonInteractable 为 false：忽略
  -> _buttonInteractable 为 true：显示 pressBtn -> 原有开始/召回
BtnArea Drag End/Cancel/移出
  -> 隐藏 pressBtn

FakeSystemTips HoldEnded
  -> OnPromptHoldEnded -> Defending 退出

Level69Key Collider
  -> OnTrigger2DHandle.OnEnter
  -> 运行时 OnKeyContact
  -> Level69 本地飞行与开锁流程
```

### 验证、未验证缺口与回滚

- CodeGraph 复核确认 `OnButtonReleased()` 只调用 `SetPressButtonVisible(false)`，`StartPromptExit()` 仍由 `OnPromptHoldEnded()` 调用。
- 静态确认 prefab 的 `dragInput`、BeginDrag、EndDrag 和 `keyTrigger` 均有非零绑定，Key 的 `onEnter` 持久调用数组大小为 0，未残留 `OnTriggerExecute` override。
- `Level69Ctrl.cs` 定向 `git diff --check` 通过；完整目标检查仍被 prefab 本次接管前已有的 Unity YAML 尾随空格阻断，本次未清理这些无关行。
- `Assembly-CSharp.csproj` 编译被 `Temp/bin/Debug` 缺少 191 个 Unity/包引用 DLL 的 `CS0006` 阻断，未进入本次脚本的有效编译诊断。
- Unity PlayMode、快速连按、按住跨状态、移出、Cancel、多指、防御 owner、Key 单次执行，以及 Android/iOS 横屏触摸仍未验证。
- 回滚时恢复 `ClickItemEventHandle clickInput` 及 prefab Click 组件/绑定，恢复原按钮松手行为和 Key 持久回调；共享输入、Key prefab 与 TriggerExecutor 无需回滚。

## 2026-07-13：单次战斗入口与清理职责收口

### 需求与原始行为基线

- Level69 的一个关卡实例只允许从 `Idle` 进入一次战斗；Playing 阶段再次按按钮只用于召回 FakeSystemTips，不会重新开始战斗。
- 修改前 `BeginSession()` 每次进入都会先执行 `CleanupSession(false)`。第一次按下按钮显示 `pressBtn` 后，同一调用栈中的清理会立即隐藏它，并重复执行取消任务、回收导弹、注销对象池、复位 Key/Lock 和停止音效等退出职责。
- 修改前 `OnEnable()` 在 `Start()` 之后每次都会重新执行 `InitializeIdleState()`，存在已开始实例被重新置回 Idle、再次满足开战条件的风险。

### 修改后行为与流程

- `BeginSession()` 改名为 `BeginBattle()`，明确它只负责一次性的 `Idle -> Intro` 开战初始化，不再调用任何退出清理。
- 新增本地运行态门闩 `_battleStarted`。`OnButtonClick()` 在检查绑定前拒绝重复开战，`BeginBattle()` 内部再次检查门闩与 Idle 状态，并在创建异步任务前立即锁定，避免同帧或异常调用路径重复进入。
- `UpdateButtonState()` 在 Idle 分支同时检查 `_battleStarted`；Playing 阶段原有召回条件保持不变。
- `OnEnable()` 只允许尚未开战的实例恢复 Drag 与 Idle 初始表现；已经开战的实例不会因为 Disable/Enable 被重置为可再次开战，清理后的 `_resultClosed` 也会阻止 Update 继续推进倒计时。
- `InitializeIdleState()` 改为直接设置初始状态、隐藏提示/按压态、复位 Key/Lock 和倒计时，不再借用退出清理。
- `CleanupSession(...)` 收口并改名为无参数 `CleanupBattle()`，只由 `OnDisable()`、`OnDestroy()` 执行，负责取消异步链、回收导弹、注销对象池、复位表现和停止战斗音效。

```text
Start -> InitializeIdleState
首次有效 BtnArea Begin -> BeginBattle（门闩锁定）
Playing 有效 BtnArea Begin -> BeginPromptAttempt(false)
后续任何 Idle 开战请求 -> 门闩拒绝
OnDisable/OnDestroy -> CleanupBattle
```

### 影响范围、验证与回滚

- 仅修改 `Level69Ctrl.cs` 和 Level69/共享中文记录；未修改 prefab、共享 `DragEventsListener`、提示/导弹/Key 组件、配置、存档、资源、全局事件、analytics 或平台 SDK。
- CodeGraph 复核确认 `BeginBattle()` 只有 `OnButtonClick()` 一个调用者，且其方法内外均有一次性保护；`CleanupBattle()` 只有 `OnDisable()` 与 `OnDestroy()` 两个调用者。
- 静态扫描确认当前脚本不再存在 `BeginSession` / `CleanupSession`，`pressBtn` 的正常显隐仍为 BeginDrag 显示、EndDrag 隐藏，生命周期退出保留隐藏兜底。
- 目标脚本 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。`Assembly-CSharp.csproj` 仍被 `Temp/bin/Debug` 缺少 191 个 Unity/包程序集的 `CS0006` 阻断，未进入本次脚本的有效编译诊断。
- Unity PlayMode 仍需验证首次按住可见、快速连按只开战一次、Playing 召回不受影响、Disable/Enable 不重开，以及 Android/iOS 横屏触摸。
- 回滚时恢复 `BeginSession()` 开头的清理调用、移除 `_battleStarted` 门闩与 OnEnable 限制，并恢复 `CleanupSession(bool)`；该回滚会重新引入同帧隐藏 `pressBtn` 和重复开战风险。

## 2026-07-13：Editor Mouse 输入兼容

- Level69 BtnArea 使用共享 `DragEventsListener`。修改前在 Android/iOS Build Target 的普通 Unity Game View 中会进入 Touch 分支，只有 Device Simulator 产生模拟触摸时才能触发。
- 共享监听器现在在 `UNITY_EDITOR` 下固定优先使用现有 Mouse 分支；Level69 的 BeginDrag/EndDrag 绑定、`pressBtn` 表现、一次性 `BeginBattle` 门闩和 Playing 召回逻辑均未修改。
- Android/iOS 真机仍走 Touch/fingerId 跟踪；未修改 Level69 prefab、配置、存档、资源、事件或平台 SDK。
- 静态预处理条件与 diff 已检查；普通 Game View、Device Simulator 和真机仍需 PlayMode/设备验证。
- 回滚方式见 `Assets/Game/Scripts/Common/DragEventsListener功能说明.md`，Level69 脚本和 prefab 无需回滚。

## 2026-07-13：胜利后仅显示 Key

### 需求与原始行为基线

- 用户确认 DoorLock 不需要由 `Level69Ctrl` 隐藏，钥匙和门锁的后续交互由场景侧自行处理。
- 修改前控制器缓存并复位 Key Transform，隐藏 Key/DoorLock，控制 Key Collider，胜利后等待延时并播放下落，再监听 Player 碰撞、执行飞向 LockPoint、等待 unlock 动画并派发门事件。
- 上述流程使 Level69 的胜利职责超出当前需求，也让 DoorLock 和 Key 的场景表现被控制器反复改写。

### 修改后行为

- `EnterSurvivalCleared()` 完成既有停止轰炸、回收导弹、隐藏倒计时和显示成功提示后，只执行 `keyObject.SetActive(true)`。
- `Level69Ctrl` 不再隐藏、显示、复位或驱动 DoorLock，也不再修改 Key Transform/Collider，不监听 Key 碰撞，不执行 Key 下落/飞门/unlock 动画，不派发 Door event。
- Key 相关序列化依赖只保留 `keyObject`；开战必填校验不再要求 `keyTrigger`、`keyCollider`、`keyJump`、`unlockTarget`、`doorLockObject` 或 `unlockAnimation`。
- `GameState` 删除 Key/门锁专用阶段，控制器只保留 Idle、Intro、Playing、Failed、SurvivalCleared。
- 原 version 3.0 注释关于“钥匙下落并开锁”的描述已与本次明确需求冲突，因此最小修改为“胜利后显示钥匙，后续由场景组件负责”；其他历史注释未改。

### 影响范围、验证与回滚

- 只修改 `Level69Ctrl.cs` 与中文记录；未修改 `Level_069.prefab`、Key/DoorLock prefab、TriggerExecutor、ItemJumpToListener、PlaySpineListener、配置、存档或平台 SDK。
- 静态扫描确认控制器内只剩 `keyObject` 字段和胜利时一次 `SetActive(true)`，不存在 DoorLock、LockPoint、Key 碰撞、下落、飞行、unlock 或门事件代码引用。
- `git diff --check` 已执行；Unity PlayMode 仍需确认 Key prefab 初始 inactive、DoorLock 初始表现以及胜利后 Key 显示位置。后续钥匙触发与通关链不属于本次控制器实现。
- 回滚时恢复 Key/DoorLock 序列化字段、异步下落与飞门开锁区域、生命周期复位和必填校验，并恢复旧 version 3.0 注释；prefab 本次没有修改。

## 2026-07-13：单次按钮、炸弹销毁时机与独立爆炸实例

### 需求与原始行为基线

- BtnArea 开战后仍会在 Playing 的提示隐藏/退出阶段重新变为可交互，并可再次召回提示。
- 玩家死亡或生存胜利时，控制器会立即 `RecycleAllActive()` 并注销导弹池，导致场上所有炸弹同时消失。
- `Level69MissileCtrl` 原先移动并播放导弹实例自身持有的 `explosion1` / `explosion2`，特效对象与池化导弹实例绑定。

### 修改后行为

- BtnArea 只允许第一次有效按下：`OnButtonClick()` 只接受未开战的 Idle，`UpdateButtonState()` 不再为 Playing 恢复交互；第一次 EndDrag 隐藏 `pressBtn` 后关闭 `dragInput`，之后不再触发按钮业务。
- 玩家死亡与生存胜利只停止后续生成和关卡计时，不回收场上炸弹、不注销对象池。现有炸弹继续移动、碰撞并自行回收到仍有效的池。
- 只有 `Level69Ctrl.OnDestroy()` 才先统一回收当前活跃炸弹，再注销导弹池；因此池内与场上导弹最终随 Level 销毁释放。`OnDisable()` 不再清除导弹。
- 每次 Player/Ground/Shield 碰撞都会以对应 ParticleSystem 为模板执行 `Instantiate`，设置爆炸位置、激活并播放独立实例；按根及子 ParticleSystem 的最长 `startDelay + duration + startLifetime` 延迟销毁特效实例。
- 原有导弹本体仍使用对象池；本次只把爆炸表现改为逐次实例化，没有修改导弹生成阶段和 30 个活跃上限。

### 影响范围、验证与回滚

- 修改 `Level69Ctrl.cs`、`Level69MissileCtrl.cs` 和本地中文记录；未修改 `Level_069.prefab`、ParticleSystem 资源、共享对象池、存档、配置、事件或平台 SDK。
- 静态检查应确认 `RecycleAllActive()` / `UnregisterObjectPool()` 只位于 `OnDestroy()`，Playing 不再调用 `BeginPromptAttempt(false)`，爆炸原模板不再直接 `Play()`。
- 需要 PlayMode 验证首次按下/松开、后续点击无响应、死亡后其他炸弹继续下落、Level 销毁后无残留、两枚同时爆炸互不覆盖、特效延时销毁。
- Android/iOS 上每次爆炸的 `Instantiate/Destroy` 会产生分配和销毁开销，峰值帧耗与 GC 尚未用 Profiler 验证，这是用户明确选择的表现实现。
- 回滚时恢复 Playing 按钮召回条件；恢复失败/胜利/清理中的统一导弹回收；把 `PlayExplosion` 恢复为移动并播放模板 ParticleSystem。

## 2026-07-13：倒计时结束后的炸弹失败表现

### 需求与原始行为基线

- 倒计时达到零后，控制器会在同帧 `LateUpdate()` 进入 `SurvivalCleared` 并关闭常规结果入口。
- 场上炸弹不会在胜利时统一回收，仍会继续移动和爆炸；原先 `TryCommitPlayerHit()` 同时校验 `_resultClosed` 和 Intro/Playing 状态，因此倒计时结束后的炸弹无法提交失败表现。

### 修改后行为

- 倒计时和胜利流程保持原样，不等待活跃炸弹数量归零，也不把炸弹命中与胜利条件绑定。
- `TryCommitPlayerHit()` 只解析本次命中的 Actor，并以 Actor 尚未死亡作为单次失败保护；不再校验 `_resultClosed` 或当前是否处于胜利状态。
- 倒计时结束后，残留炸弹命中存活玩家仍会执行 `Die()`、击退和 `EnterFailed()`；命中的炸弹按原流程播放爆炸并自行回收。
- 已经死亡的 Actor 会拒绝后续重复失败提交，其他场上炸弹继续按各自碰撞/越界流程运行。

### 影响范围、验证与回滚

- 仅修改 `Level69Ctrl.cs` 的炸弹命中失败入口；不改倒计时判胜、炸弹碰撞、对象池、爆炸实例、Key、DoorLock、配置、存档或事件。
- 静态流程为：倒计时归零并照常胜利 → 场上炸弹继续运行 → 命中存活 Actor 时独立提交失败表现。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 已通过（0 错误、98 个既有警告），目标文件 `git diff --check` 已通过。
- 胜利提示和 Key 可能已经显示，之后残留炸弹仍可把内部状态切为 Failed；Key 不会由失败入口再次隐藏，符合当前“Key 后续自行处理”的范围，但仍需 PlayMode 确认最终关卡表现。
- 仍需验证零秒同帧命中、胜利后命中、已死亡后多炸弹连续命中、护盾拦截和 Level 销毁；回滚时恢复 `TryCommitPlayerHit()` 的 `_resultClosed` 与 Intro/Playing 状态校验。

## 2026-07-13：FakeSystemTips Idle 碰撞体生命周期

### 需求与原始行为基线

- FakeSystemTips 根节点的 `shieldCollider` 原先只在 `AcceptDefense()` 后开启，进入 Idle/HoldWindow 但尚未进入 Defending 时保持关闭。
- 同根节点的 `DragableItem` 依赖可命中的 Collider 开始拖拽，因此 Idle 阶段无法稳定使用根碰撞体命中。

### 修改后行为

- `PlayIn()` 动画完成并即将回调 Idle/HoldWindow 时开启 `shieldCollider`。
- `PlayOut()` 开始、`AbortAndHide()`、初始化和 Disable 时仍立即关闭碰撞体。
- Collider 提前开启不等于防御成立；`Level69Ctrl.IsMissileBlockedByShield()` 仍要求 `_promptPhase == PromptPhase.Defending`，Idle/HoldWindow 不会自动阻挡炸弹。
- 本次不修改 `Level_069.prefab`、DragableItem、HoldInputArea、对象池、倒计时、Key、存档、资源或事件。

### 验证与回滚

- 静态检查应确认 `PlayIn.OnComplete` 先开启碰撞体再执行完成回调，`PlayOut` 开始处仍关闭碰撞体，导弹盾牌仲裁仍保留 Defending 状态门槛。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 已通过（0 错误、98 个既有警告），目标文件 `git diff --check` 已通过。
- 仍需 Unity PlayMode 验证 Idle 拖拽、进入动画期间不可拖拽、Out 开始后不可拖拽、Idle 炸弹不被误挡和 Defending 正常挡弹。
- Android/iOS 横屏触摸与 Editor 鼠标仍需分别验证；回滚时删除 `PlayIn.OnComplete` 中的 `SetShieldEnabled(true)` 并恢复字段/API 注释。

## 2026-07-14：战斗开始时 star2 下落

### 需求与原始行为基线

- `Level_069.prefab` 已存在并绑定 `star2`，其初始本地 Y 约为 `18.33`，但 `Level69Ctrl` 原先没有在战斗开始时驱动该对象。
- 现有 `star` 会在生存胜利时通过 `DOMoveY(-0.76f, 3f)` 移动到指定世界高度，并在完成后启用 `MoveFloatHandle`。

### 修改后行为

- 首次有效按钮输入进入 `BeginBattle()`、状态切换为 `Intro` 后，`star2` 立即执行 `DOMoveY`，目标世界 Y 与 `star` 一致为 `-0.76`。
- 移动时间由 `star2MoveDuration` 控制，`Level_069.prefab` 当前配置为 `10` 秒；负值在运行时按 `0` 秒处理。
- 移动完成后启用 `star2` 自身的 `MoveFloatHandle`；若 `star2` 或该组件未绑定，不阻止关卡开始。
- 本次不修改 `star` 的胜利移动、导弹、倒计时、Key、输入、存档、资源、事件或平台流程。

### 验证与回滚

- 已静态确认调用位于一次性 `BeginBattle()` 入口，且 prefab 的 `star2` 与 `star2MoveDuration: 10` 已绑定。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 已通过：0 个错误、99 个既有警告；目标文件 `git diff --check` 未发现空白错误，仅有工作区 LF/CRLF 转换提示。
- 仍需 Unity PlayMode 验证 10 秒下落轨迹、落点、完成后浮动、战斗中失败/退出时的 Tween 表现，以及 Android/iOS 横屏显示。
- 回滚时移除 `StartStar2Move()`、`BeginBattle()` 中的调用、`star2MoveDuration` 字段和 prefab 对应配置；保留用户已有的 `star2` 对象与绑定。

## 2026-07-14：生存胜利保持按住提示直至松手

### 需求与原始行为基线

- FakeSystemTips 在玩家主动松开有效按住时，由 `Level69HoldInput.HoldEnded` 驱动 `StartPromptExit()`，随后播放 Out。
- 原生存胜利调用 `CancelPromptAndHide()`，会关闭 Collider、把透明度设为零并停用对象，导致仍在按住的提示立即消失。
- 用户确认倒计时结束进入 `SurvivalCleared` 时，仍在按住的 FakeSystemTips 不播放 Out、也不消失，必须等玩家松手。

### 修改后行为

- `EnterSurvivalCleared()` 不再调用 `CancelPromptAndHide()`，因此 Defending 状态的提示、Hold owner 和盾牌 Collider 会保持到玩家松手。
- `IsCurrentPrompt()` 只为 `SurvivalCleared` 状态下的 `Defending/Exiting` 放行当前提示；玩家松手后仍由既有 `StartPromptExit()` 播放 Out，并在完成后切回 `Hidden`。
- 已经在倒计时结束前松手的提示继续按原流程 Out；已经隐藏的提示不会因胜利重新出现。
- 失败、关卡 Disable/Destroy 和普通清理仍使用 `CancelPromptAndHide()` / `AbortAndHide()` 立即隐藏，不等待玩家松手。
- 本次未修改 prefab、Out 时长/缓动、导弹、倒计时、Key、存档、资源、全局事件、analytics 或平台 SDK。

### 验证与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 已通过：0 个错误、99 个既有警告。
- 静态检查确认胜利入口不再调用 `CancelPromptAndHide()`，且结果后的放行范围仅为本关 `SurvivalCleared + Defending/Exiting`。
- 尚未执行 Unity PlayMode；需验证持续按住跨过倒计时零点、零点后松手 Out、提前松手正常 Out、残留炸弹失败，以及 Android/iOS 横屏触摸。
- 回滚时恢复 `EnterSurvivalCleared()` 的 `CancelPromptAndHide()`，并将 `IsCurrentPrompt()` 恢复为只检查 `_resultClosed`。
