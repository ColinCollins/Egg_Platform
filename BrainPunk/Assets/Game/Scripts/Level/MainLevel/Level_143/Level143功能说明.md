# Level143「钢绳图钉」功能说明

更新时间：2026-10-10

## 2026-10-10：重置后图钉拖拽失效修复

### 修改前核对

- 需求与行为基线：用户实测 Level143 重置后 nail 无法拖动。本次恢复手动 Reset、失败重试、VictoryPanel Replay 后的图钉输入，保留首次进入、暂停禁用输入、成功/失败/死亡门禁、钢绳碰撞与火花、Shadow 清理和整关销毁重建。
- 来源：`Level143Ctrl.OnGameResume/CanReceiveInput/Update`；`PlayCtrl.ResetGame/ReplayGame`；`PlayCtrl_Pause.OnExit`；`Bear.Fsm.StateMachine.Enter`；`Level_143.prefab` 根节点已显式绑定本关控制器和五个 Anchor。现有 Editor.log 的 ResetGame 调用栈确认执行了 Pause 退出再 Playing 进入，但没有记录本关私有 `isPaused` 值。
- 原流程与原因：重置/Replay 销毁旧实例、创建新实例，再直接调用状态机进入 PAUSE 和 PLAYING；GameResumeEvent 在旧暂停节点 OnExit 内发送，此时状态机仍为 PAUSE。本关恢复回调读取 `!IsGameplayPlaying()` 得到 true，随后没有 SwitchGameStateEvent 修正缓存，Update 和输入门禁持续拒绝输入。
- 拟修改位置与边界：仅将 `Level143Ctrl.OnGameResume` 的本地暂停标记解除，不在恢复事件内读取尚未完成切换的状态；保留 teardownStarted 拦截和 CanReceiveInput 对实际 PLAYING、Actor 可控/存活及成功/失败的检查。不新增事件、API、资源或对象，不改 Prefab、共享 PlayCtrl/FSM、Level005、Actor、SDK、存档与打点；与《关卡打点生命周期约定》无冲突。
- 验证计划：用当前源码回调与共享状态机/暂停节点做隔离时序测试，覆盖首次进入、连续 Reset/失败重试/Replay、正常暂停恢复、暂停切到非 PLAYING、旧实例晚到 Resume、失败/成功/死亡/不可控门禁；使用 Unity Bee 响应文件做隔离输出编译并检查 diff。Unity PlayMode 的五图钉实际拖拽、钢绳物理、火花及 Android/iOS 横屏触控仍需运行验收。
- 未决项：AGENTS.md 引用的 `../specs/037-remote-resource-loading/plan.md` 缺失；CodeGraph CLI/MCP 当前不可用，沿已核对源码、Prefab 和本关文档继续。本次不改变资源加载或远端配置，不依赖上述缺口作行为决策。

### 实施、验证与剩余边界

- 已实施：`OnGameResume` 保留 teardownStarted 拦截，将 `isPaused` 设为 false，并新增两行中文时序说明。未改写原有代码注释；脚本逐字对比确认只有该赋值和新增注释变化，输入拾取/拖动、钢绳物理、火花、Shadow、事件订阅和重建清理均未修改。
- 隔离时序验证：读取 Git HEAD 和工作区版本的 `OnGamePause/OnGameResume/OnSwitchGameState/CanReceiveInput/IsGameplayPlaying` 方法，复用当前共享 `StateMachine`、接口、属性和真实 `PlayCtrl_Pause` 源码；Unity 日志、事件派发与场景对象使用最小替身，不执行真实资源加载或物理。182 项断言通过：修改前普通暂停恢复及 Reset/失败重试/Replay 各连续 5 次共复现 16 次输入恢复失败，修改后这 16 个场景全部允许输入。
- 保护验证：Resume 发送期间状态仍是 PAUSE，输入继续被实际状态门禁拒绝；暂停退出到 FAILED、旧实例晚到 Resume、成功、失败、死亡、角色不可控、玩法未激活、控制器/关卡未初始化均不接受输入。首次进入和显式 PLAYING 通知保持可输入。
- 编译：使用 Unity 当前 Bee `Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp.rsp` 和 Unity Roslyn 编译器，覆盖输出到系统 TEMP，返回 0，0 error / 113 warning，本关脚本无诊断；未覆盖 Unity 自身的编译产物或只读依赖。
- 证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-level143-resume-20261010/verify-resume.ps1`、`verify-resume.log`、`compile.log`。静态/隔离验证不能替代 Unity 导入、PlayMode、Android/iOS 或 IL2CPP 验收；本轮没有操作 Unity 窗口。
- 最短运行验收：退出旧 PlayMode，等待脚本编译完成，重进 Level143；分别拖五个图钉，手动 Reset 和失败重试各 5 次，再通关后 Replay 5 次；检查恢复后可重新起手拖拽、门固定、绳面可站、火花与阴影恢复，暂停期间不能拖。Android/iOS 横屏重复触控与应用恢复检查仍为 `uncertain`。
- 局部回滚：只将 `OnGameResume` 的赋值恢复为 `isPaused = !IsGameplayPlaying()`，删除本次新增时序注释并同步本节；不需要删除关卡、改 Prefab、配置、共享状态机或存档。该回滚会重新引入本次已复现的恢复失效。

## 需求背景

Level143 是主线 Level005「切绳子」的变种。场景保留 Level005 的五根绳、五个图钉、悬空出口门、Actor、相机、反馈和失败区结构，但解法不再是切断绳子：绳子表现为不可切断的钢绳，玩家需要拖动图钉改变线段位置，将其中一根钢绳调整为可站立坡道，再让角色沿钢绳进入门内。

玩家在非图钉区域划动并尝试切绳时，绳子保持完整；滑动轨迹与钢绳相交或贴近时，在实际接触点反复播放 Level052（配置 ID `10052`）使用的 `SparkExplosion2D` 火花和 `frictional` 摩擦音效。

本关实现独立放在 Level143，不修改已经验收的 Level005、共享 Actor、共享 Door 或共享输入组件。

## 影响文件和配置

- `Assets/Game/Prefabs/Level/Level_143.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_143/Level143Ctrl.cs`
- `Luban/Configs/Datas/LevelData.xlsx`：沿用工作区已有 `10143 / Level_143 / 拉动图钉` 行，本轮未改写工作簿。
- `Luban/Configs/Datas/LevelSort.xlsx`：沿用工作区已有显示位 `117 -> 10143` 行，本轮未改写工作簿。
- `Assets/Game/Configs/tbleveldata.json`、`tblevelsort.json`：通过项目 Luban 正式生成流程同步上述现有源表配置。
- 只读复用：`Assets/Epic Toon FX/Prefabs 2D/Explosions/SparkExplosion2D.prefab`、Level052 的 Glow 材质和 `frictional` 音效。
- 未修改：Level005 脚本/Prefab、Level052 脚本/Prefab、共享 Actor、Door、BaseLevelCtrl、事件定义、存档、RemoteConfig、analytics、广告、IAP 和平台 SDK。

## 关卡结构与运行流程

1. `Level_143.prefab` 保留已有 Level005 副本的五个 `Level5Anchor`、五个 `Level5TrackLine`、悬空门及现有蓝色图钉/钢绳表现。
2. 进入运行态后，`Level143Ctrl` 将门体固定为 Kinematic，并禁用五个 Level005 遗留的 `SpringJoint2D`，避免拖动图钉时把门体拉走。
3. 控制器在每个 `Line` 节点上补一个运行时 `EdgeCollider2D`，设置到项目预留的 `WithoutShadowGround` 层；本关 Actor 的 `whatIsGround` 由 `320` 局部扩为 `8512`，因此钢绳仍参与落地，Actor Prefab 和其它关卡不变。碰撞端点和 `LineRenderer` 使用相同的 StartPoint/EndPoint，所以钢绳外观、碰撞和拖动位置保持一致。
4. `ShadowController.groundLayer=320` 不包含 `WithoutShadowGround`，角色跳起时的向下射线不会再把钢绳当成投影接收面。角色脚底真实踩住钢绳时，本关仍临时隐藏 Actor 的整个 `Shadow` 根节点，避免射线穿过钢绳后把影子投到更低的普通地面；离开、暂停或关卡清理时恢复进入钢绳前缓存的激活状态。
5. 手势按下时优先以 `anchorPickRadius` 查找最近图钉。命中图钉后进入拖拽模式，图钉限制在相机视口内，并与门端点保持最短距离。
6. 未命中图钉时进入试切模式，显示 Level005 留下的刀光拖尾。每段输入采样与五根钢绳求线段交点；严格相交优先，靠近绳面时使用 `sparkContactRadius` 容差。
7. 命中钢绳只播放火花和摩擦音，不改变 Line、Collider 或关卡状态。火花按 `sparkInterval` 重复触发。
8. Actor 沿调整后的 `Stuff` 层钢绳行走；最终进入原 Door Trigger，由保留的共享过关链完成结算。

## Level052 火花复用参数

- 资源：`SparkExplosion2D.prefab`，直接引用共享 ParticleSystem Prefab，不复制或修改源资源。
- 根粒子：尺寸 `0.15`、速度 `25`、寿命 `0.5s`、重力 `0`、Burst 数量 `8`。
- 颜色：黄色到橙色并渐隐；Glow 子粒子尺寸 `2`。
- 排序：`SceneLayer / 250`，与 Level052 的场景火花层级一致。
- 播放策略：预建 6 个实例循环复用，连续摩擦不逐帧 Instantiate；默认火花间隔 `0.08s`、音效间隔 `0.18s`。
- 粒子使用世界空间，图钉继续移动后，已经产生的火花不会跟随绳或图钉漂移。

## 暂停、重试与清理

- 输入只在 PlayCtrl 为 `PLAYING`、关卡未成功/失败、Actor 可控且未死亡时接收。
- Pause、状态切换会立即终止当前图钉拖拽或试切轨迹；Resume 解除本地暂停缓存，输入仍必须等实际玩法回到 `PLAYING` 才能接收，不恢复上一段已终止的手势。
- Reset/Replay 的 Teardown 第一步终止交互，停止火花并关闭本实例动态绳碰撞，随后交给现有整关销毁/重建流程恢复 authored 初始位置。
- 角色离开钢绳、暂停、状态切换、Reset、Replay、Disable 或 Destroy 时都会释放本关的 Shadow 隐藏状态，避免把无影状态带到地面或下一次尝试。
- 事件订阅通过本地 `EventSubscriber` 统一清理；不缓存已销毁对象后继续访问，不改变共享 GameReset/GameReplay 派发顺序。

## 验证记录

- Luban 临时输出完成 schema、数据加载和 validation，随后正式生成；运行时 JSON 已包含 `10143 / Level_143` 和 `117 -> 10143`。
- 新脚本已被 Unity Bee 响应文件收录；完整 Assembly-CSharp Roslyn 编译通过，0 个错误，warning 属于当前工程既有范围。
- Prefab 静态检查确认 Level143 控制器唯一、五个图钉引用完整、Level005 的 `CutManager` GUID 已移除、Level052 火花与 Glow 材质引用存在。
- Prefab 本地 fileID 定义唯一性、引用闭合和 Unity PrefabImporter 日志已检查。
- 2026-09-03 第一版只在 Actor Collider 与钢绳真实接触时隐藏 Shadow；用户截图证明角色仍在空中时，`ShadowController` 已先向 `Stuff` 层钢绳生成多个动态投影，因此该条件覆盖不完整。
- 第二版把钢绳改为 `WithoutShadowGround`，并只给 Level143 Actor 的落地掩码追加该层，同时保留站立接触时隐藏 Shadow 的兜底。完整 Assembly-CSharp Roslyn 编译结果为 `exit 0`、0 error、107 条项目既有 warning；Prefab 仍为 126 个唯一定义、0 个重复、0 个缺失本地引用，`whatIsGround=8512` 唯一存在。
- 当前 Unity 会话在修改发生后仍持有 14:27 的旧 Prefab 缓存，Editor.log 持续报告磁盘 14:31 与 SourceAssetDB 14:27 的版本差异，Assembly-CSharp.dll 时间也尚未推进；必须退出当前 PlayMode/旧实例并等待自动导入后再重进关卡。新版本的 Unity 导入与截图视觉因此仍为 `uncertain`。
- 当前未操作 Unity 前台进入 PlayMode；图钉拖拽手感、Actor 站立稳定性、门口最后一步、火花尺寸/层级/密度、连续 Reset/Replay，以及 Android/iOS 横屏触控均为 `uncertain`，需要视觉验收。

## 回滚路径

删除 Level143 私有脚本目录与 `Level_143.prefab`，并从 `LevelData.xlsx` / `LevelSort.xlsx` 撤回 `10143` / 显示位 `117` 后重新运行 Luban，即可完整移除本关。共享 Level005、Level052、钢绳粒子资源、Actor、Door 和事件系统无需回滚。
