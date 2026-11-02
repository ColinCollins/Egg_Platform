# DLevel_039 脚本结构记录

## 1. 基本信息

- 关卡：`DLevel_039`，配置 ID `20039`，名称“悬崖巨石”。
- 记录日期：2026-08-04。
- 当前状态：代码、Prefab、配置一致性和静态编译已验证；PlayMode 与移动端尚未验证。
- 参考基线：DLevel_026 的单套猫夹球流程，以及 DLevel_038 的首笔物理释放与尝试复位结构。
- 主要脚本：`Level39/DLevel39Ctrl.cs`、`Level39/DLevel39StoneBallFailureHandle.cs`。
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_039.prefab`。

## 2. 脚本与组件树

```text
DLevel_039
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DrawLevelCountdownCtrl (20 秒失败倒计时)
├─ DLevel39Ctrl : DLevel2Ctrl
├─ DrawGame_AlienClipBallRig
│  ├─ Alien / ActorSpineCtrl / SkeletonAnimation
│  ├─ BoneFollower / clipSwingPivot / clip SkeletonAnimation
│  └─ yellow_ball / Rigidbody2D
└─ Stone
   ├─ Rigidbody2D (等待态 Kinematic，mass 150)
   ├─ CircleCollider2D
   └─ DLevel39StoneBallFailureHandle
      └─ 指定 yellow_ball / D003 Knock_Explosion / DrawLevelCtrl
```

Prefab 根节点原有的 `DLevel26PlatformCtrl` 三组引用全部为空，运行时只会报错并自禁用，现已删除。
跨关卡 `DLevel26Ctrl` 已替换为本关 `DLevel39Ctrl`，原有夹子、球和动画引用保持不变。

## 3. 类职责与边界

| 类/组件 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- |
| `DLevel39Ctrl` | 猫夹球 Intro、首笔开夹放球、Stone 首笔释放、尝试重置 | `DLevel2Ctrl`、`IDrawLevelVisibleIntroHandler`、`IDrawStrokeCompletedHandler` | 成功门、倒计时、画线生成、石头伤害判定 |
| `DLevel39StoneBallFailureHandle` | 首笔后精确识别 Stone 与指定球的 Collision2D，在接触点播放特效并延迟提交失败 | `IDrawStrokeCompletedHandler` | Actor 死亡/击退、Stone 释放、全局陷阱事件 |
| `DLevel2Ctrl` | 共享 Alien 入场、夹子动画、球的父级和物理状态恢复 | Draw Intro 与 Stroke Handler | D039 Stone |
| `DrawLevelCtrl` | 收集本地 Handler，分发 Intro、落笔和 Attempt Reset | `IDrawLevelRuntime` | D039 机关细节 |
| `DrawLineFeature` | 生成运行时画线并提交有效 Stroke | 共享画线组件 | 直接控制夹子或 Stone |

## 4. 生命周期与关键调用

1. `Awake` 先调用 `DLevel2Ctrl.Awake()` 缓存并恢复夹持球，再缓存 Stone 世界位置和角度，将其保持为 Kinematic、零速度等待态。
2. 可见 Intro 沿用单套 Alien 入场；两秒未完成时应用 Intro 完成态，避免画线门禁永久关闭。
3. 第一笔有效画线完成后，`DrawLevelCtrl` 向本关 Handler 分发回调。`DLevel39Ctrl` 先调用基类播放 `clip_open`、释放球并播放 Alien control，再在同一同步回调把 Stone 切为 Dynamic、清零速度并 `WakeUp()`；命中处理器同时进入 armed 状态。
4. 后续笔画不再次释放球或 Stone。
5. armed 后 Stone 的 `OnCollisionEnter2D` 只接受 `collision.collider.attachedRigidbody == targetBallRigidbody`；首次命中用第一个 contact point 定位并重播粒子、播放 `trap` 音效，等待 0.5 秒后调用 `DrawLevelCtrl.TryFailAndReset(...)`。
6. Attempt Reset 先恢复猫、夹子、球的挂载和 Kinematic 初态，再恢复 Stone 初始位姿、Kinematic 与零速度；命中处理器递增尝试版本、解除 armed/failing 并清空粒子，旧延迟不能影响新尝试。
7. `OnDisable` 沿用基类清理 Intro Tween 和动画版本，并让命中处理器取消旧等待；本关没有新增 Update、Coroutine 或全局事件订阅。

## 5. Prefab 与序列化绑定

| 字段/组件 | 绑定 | 缺失时行为 |
| --- | --- | --- |
| `DLevel39Ctrl.stoneRigidbody` | Stone `4111949391997656232` | 记录错误；开夹放球继续，Stone 释放停用 |
| `yellowBallRigidbody` | 夹持球 `6262018380601849612` | 基类记录错误并无法完成正常放球 |
| `alien` | `1255546920056987011` | 基类 Intro 无法正常执行 |
| `clipSkeletonAnimation` | `2849637529966163699` | 基类记录夹子动画缺失 |
| `drawLevelCtrl` | 根控制器 `900000000000000001` | 基类尝试从父层级解析 |
| `DLevel39StoneBallFailureHandle.drawLevelCtrl` | 根控制器 `900000000000000001` | 尝试从父层级解析；仍缺失时不提交失败 |
| `targetBallRigidbody` | 指定夹持球 `6262018380601849612` | 不匹配任何碰撞，避免误判其它 Actor 层物体 |
| `knockEffect` | D003 同源根 ParticleSystem `3757748896162189796` | 仍播放音效并提交失败，但没有粒子表现 |
| `failDelay` | `0.5` 秒 | Inspector 暴露，运行时按非负值等待 |

## 6. 数据、资源、UI 与事件流

- Luban/运行时配置：`20039 -> DLevel_039`，墨水上限 15，评级阈值 `0/2/5`；排序为第 33 项。
- Guru 默认配置：`drawgame_config.level_sort` 已同步为 33 项，末项为 `20039`；`enabled=false`，线上远端覆盖状态为 `uncertain`。
- 资源：继续复用 `DrawGame_AlienClipBallRig` 及其 Spine 资源；命中表现复用 D003 同源的 `Knock_Explosion`（GUID `972fa0cfdd6f5e04a8c9fde50e1bb924`），沿用 D003 的裁剪方式移除敲门子对象，不新增运行时加载或实例化。
- DB/存档、UI、Analytics、广告/IAP、平台 SDK：本关脚本不直接访问或修改。
- 事件：只复用 `DrawLevelCtrl` 的关卡内 Handler 分发和 `TryFailAndReset` 结果锁，不派发 `OnTriggerTrapEvent`，不新增 `PlayEvents` 或其它全局事件。

## 7. 相对参考基线的差异

| 差异 | 原因 | 影响 | 决策 |
| --- | --- | --- | --- |
| 相比 D026 增加 Stone 同步释放和复位 | D039 玩法要求 | 首笔后巨石参与真实 Physics2D | 保持在本关控制器 |
| 相比 D038 不使用数组、齿轮或铰链 | 本关只有一个自由落体 Stone | 更小的绑定面 | 不提取共享机关抽象 |
| 使用本关类替换 D026 类 | 避免 D039 继续依赖跨关卡身份组件 | 不改变共享 D002/D026 行为 | 局部隔离 |
| 不复用 Stone 原挂载的 D004 命中组件 | `DLevel4RockTrapHandle` 只识别 ActorCtrl，且会执行死亡、击退和共享陷阱事件；D039 目标是无 ActorCtrl 的指定球 | 避免错误目标模型和跨关行为副作用 | 替换为本关局部 Collision2D 处理器 |

## 8. 验证证据与缺口

- Unity 6000.0.59f2 Bee 响应文件已包含 `DLevel39Ctrl.cs` 与 `DLevel39StoneBallFailureHandle.cs`；Editor Tundra build success，最终源码再使用同一 Bee 响应文件独立全量 Roslyn 编译，退出码 0，未出现新的 `error CS`。
- Unity Editor 日志确认脚本编译和 `DLevel_039` PrefabImporter 完成，未发现 `error CS`。
- Prefab 静态检查：139 个 YAML anchor 唯一，139 个唯一非零本地 fileID 全部可解析；D026 两个旧控制器残留为 0，旧 `DLevel4RockTrapHandle` GUID 残留为 0，新处理器、指定球与根 ParticleSystem 引用完整。
- Guru YAML、生成 C# 内嵌 JSON 与运行时排序 JSON 均可解析，33 项顺序完全一致。
- `uncertain`：实际 Intro、首笔同步时序、球与石头落点、命中点粒子、0.5 秒失败、20 秒失败倒计时竞争、清线、Reset/Replay 尚未做 PlayMode 验证；当前 legacy 成功门实例为 inactive。
- `uncertain`：Android/iOS 横屏的物理手感、低帧率碰撞和性能尚未验证。

## 9. 风险与回滚

- Stone 使用现有质量 150、Discrete Collision Detection；与不同画线形状的碰撞效果需要 PlayMode 调参确认。
- 指定球匹配依赖当前 Prefab 的 Rigidbody2D 引用而非 Tag/Layer；这是精确隔离，但换球时必须同步重绑该字段。
- 命中后只播放粒子和音效，不冻结球或 Stone；0.5 秒表现窗口内物理仍继续，最终由整关重载清理。
- 当前没有 active 成功来源；若以后启用成功门或其它成功条件，需要在命中时先占用结果锁，避免 0.5 秒表现窗口内由成功抢先结算。
- 仅回滚本次命中失败时，可从 Stone 移除 `DLevel39StoneBallFailureHandle` 并删除其脚本/meta，同时移除 ParticleSystem stripped 引用；无需回退放球、巨石释放、配置或共享资源。
