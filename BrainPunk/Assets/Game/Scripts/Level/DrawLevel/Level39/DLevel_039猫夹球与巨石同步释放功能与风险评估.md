# DLevel_039 猫夹球与巨石同步释放功能与风险评估

## 需求与原始行为

- 需求一：玩家完成第一笔有效画线后，爪子打开并放球，同时激活右上方石头的物理。
- 需求二：巨石碰到本关球时，在真实碰撞点播放参考 D003 的 `Knock_Explosion`，随后关卡失败。
- 修改前，Prefab 复用了绑定完整的 `DLevel26Ctrl`，因此开夹放球已经可用；Stone 虽为 Active 且 simulated，但 Rigidbody2D 是 Kinematic，不会受重力或画线碰撞推动。
- 根节点还残留一个全部齿轮、平台和铰链引用为空的 `DLevel26PlatformCtrl`，其 Awake 会报错后自禁用。

## 影响范围

- `Assets/Game/Scripts/Level/DrawLevel/Level39/DLevel39Ctrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level39/DLevel39StoneBallFailureHandle.cs` 及 `.meta`
- `Assets/Game/Prefabs/DrawLevel/DLevel_039.prefab`
- `Assets/Guru/guru_spec.yaml`
- `Assets/Guru/Gen/GuruAllSpecs.G.cs`
- D039 本地记录、DrawGame 结构演进/控制器风险记录、LubanGenerator 功能说明和已有功能设计记录。

本次不修改共享 `DLevel2Ctrl`、`DLevel26Ctrl`、`DLevel4RockTrapHandle`、`DrawLevelCtrl`、`DrawLineFeature`、
Physics2D Layer Matrix、Stone 质量/碰撞盒、球物理参数、成功门、倒计时、DB/存档、资源加载、UI、
Analytics 或平台 SDK。

## 当前行为

1. 开场时夹子保持关闭并夹持球；Stone 固定在 Prefab 初始位置 `(5.2, 3.63)`，Kinematic、simulated。
2. 第一笔有效画线完成后，同一回调先执行共享 `clip_open` 和放球，再把 Stone 切换为 Dynamic。
3. Stone 释放时不施加额外力，只清零线速度/角速度并唤醒；后续运动完全由 Physics2D、重力和真实碰撞决定。
4. 后续笔画不会重复播放释放。
5. 清线、失败重开或主动 Attempt Reset 会恢复夹持球，并把 Stone 放回初始位姿和 Kinematic 等待态。
6. 缺失 Stone 绑定时只停用巨石释放并记录错误，不阻断既有开夹放球流程。
7. 命中失败判定也只在第一笔有效画线后 armed；Stone 上的局部处理器只接受指定球刚体，不使用 `Player` Tag、Actor Layer 或 `ActorCtrl` 做范围判断。
8. 首次命中立即在第一个 Collision2D contact point 清空并重播 D003 同款根粒子，播放 `trap` 音效；不调用 Actor `Die()`、`Knockback()`，也不冻结球或 Stone。
9. 命中后等待 Inspector 暴露的 `failDelay=0.5` 秒，再调用 `DrawLevelCtrl.TryFailAndReset(...)`。尝试版本与销毁 CancellationToken 会取消 Reset/Disable/整关重载之前留下的旧等待。

## 复用与配置决策

- 继承 `DLevel2Ctrl`，继续复用已存在的 Alien/Spine/BoneFollower/夹子/球状态机。
- 采用本关 `DLevel39Ctrl` 承担 Stone，避免修改共享基类或让 D039 继续挂 D026 身份组件。
- Stone 原挂载的 `DLevel4RockTrapHandle` 只识别 ActorCtrl，且附带角色死亡、向上击退和共享陷阱事件；D039 的球没有 ActorCtrl，因此用本关 `DLevel39StoneBallFailureHandle` 原位替换，不修改 D004/D016。
- 命中特效直接引用 D039 已有且与 D003 同源的裁剪版 `Knock_Explosion` 根 ParticleSystem；继续移除源 Prefab 的敲门子对象，不新增 Instantiate 或资源加载。
- 复用 `IDrawStrokeCompletedHandler` 和 `DrawLevelCtrl` 的局部分发，不新增全局事件或每帧轮询。
- Excel 与运行时 JSON 已有 `20039 / DLevel_039` 和第 33 项排序；仅把 Guru 默认排序同步为相同 33 项。

## 风险

- `uncertain`：Stone 的质量 150、重力倍率 1、Discrete Collision Detection 保持原值；实际压线、滚落和推动效果需要 PlayMode 验收。
- `uncertain`：contact point 的视觉贴合、0.5 秒表现节奏、首次命中防重，以及 Stone 先碰线/地面再碰球的实际结果尚未 PlayMode 验收。
- 当前 legacy 成功门实例为 inactive，现行只需验证 20 秒失败倒计时与巨石碰球失败的并发；代码层由 `DrawLevelCtrl` 结果锁保证只提交一个结果，但接近倒计时归零时粒子能显示多久仍为 `uncertain`。
- 若以后启用成功门或其它成功来源，需要新增“命中即占用失败结果”的局部边界，避免 0.5 秒表现窗口内由成功抢先结算；当前共享控制器没有对应的延迟失败占用 API。
- `uncertain`：线上 RemoteConfig 是否覆盖本地 `drawgame_config`、Android/iOS 横屏低帧率碰撞与触控表现尚未验证。
- Intro 仍有两秒非缩放时间兜底；若 Spine 或 Tween 异常，会直接进入可画线完成态，不会永久卡住输入。
- 命中表现不修改物理速度；延迟失败的 0.5 秒内球与 Stone 会继续按 Physics2D 运动，这是当前有意保留的真实物理表现。

## 验证证据

- Unity Bee rsp 已收录 `DLevel39Ctrl.cs` 与 `DLevel39StoneBallFailureHandle.cs`；Editor Tundra build success，最终源码再使用同一 Bee rsp 独立全量 Roslyn 编译，退出码 0，未出现新的 `error CS`。
- Unity Editor 日志确认 `DLevel_039` PrefabImporter 成功，最新筛选未出现 C# 编译错误。
- Prefab 为 139 个唯一 anchor、139 个唯一非零本地引用、0 重复、0 缺失；`DLevel39Ctrl` 的 Stone 与夹子链、新命中处理器的 DrawLevelCtrl/指定球/粒子引用完整，旧 `DLevel4RockTrapHandle` GUID 残留为 0。
- D003/D039 特效源 GUID 均为 `972fa0cfdd6f5e04a8c9fde50e1bb924`，根 ParticleSystem source fileID 均为 `462519149007686675`；源粒子 `playOnAwake=0`。
- 运行时排序 JSON、Guru YAML 和 Guru 生成 C# 的内嵌 JSON 均成功解析，三者 33 项顺序一致。
- 目标 tracked 差异的 `git diff --check` 未发现空白错误；全部新增文件的尾随空白扫描为 0 且保留文件末尾换行，仅有工作区 LF/CRLF 转换提示。

## 手工验收清单

1. 进入 D039，确认 Intro 完成前 Stone 不下落、夹子不提前打开。
2. 完成第一笔有效画线，确认夹子只打开一次，球和 Stone 在同一时刻进入物理。
3. 连续画第二、第三笔，确认不会重复放球或重置 Stone 速度。
4. 让 Stone 只碰画线、地面或其它物体，确认不会误触发球的失败表现。
5. 让 Stone 命中指定球，确认爆炸出现在真实接触点、只播放一次，约 0.5 秒后失败，且球不会播放角色死亡或被脚本击飞。
6. 在命中后的等待窗口测试清线/Reset，并测试与 20 秒失败倒计时并发，确认旧等待不影响新尝试且只结算一次。
7. 分别验证 Replay、退出重进，以及连续多次失败后的球/Stone/粒子初态。
8. 在 Android/iOS 横屏设备验证低帧率碰撞不漏判、触控结束提交和整体 60 fps 手感。

## 回滚

- 将 D039 根节点恢复为原 `DLevel26Ctrl` 并移除 `DLevel39Ctrl`，即可撤销 Stone 首笔释放，同时保留原开夹放球。
- 若只回滚本次碰球失败，移除 Stone 上的 `DLevel39StoneBallFailureHandle`、对应脚本/meta 与 Prefab 中新增的根 ParticleSystem stripped 引用；D003 原资源和 D039 既有特效实例无需删除。
- 删除 Level39 本地脚本/文档，并从 Guru 默认排序末尾移除 `20039`；D034 的 `20034` 保持不变。
- 不涉及 DB、存档格式、资源迁移或线上数据清理。
