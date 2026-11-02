# DLevel_053 脚本结构记录

## 1. 需求与基线

- 记录日期：2026-08-13。
- 关卡目标：玩家完成第一笔有效画线后，蜂巢在约 3 秒内按随机批次生成 8 只蜜蜂；蜜蜂追踪玩家，撞击画线或场景实体时施力并反冲。玩家被命中后在真实接触点播放特效，冻结成功倒计时，等待 1.5 秒后按标准失败流程重开；坚持 10 秒则成功。
- 原有基线：DrawGame 已有 `DrawLevelCtrl`、`DrawLineFeature`、`DrawLevelCountdownCtrl`、局部 Handler 扫描、暂停、成功和失败重建链路。DLevel_053 不另写倒计时、输入锁或整关重载。
- 新增范围：仅增加 DLevel_053 的蜂群控制器、单蜂行为、蜂巢出生点及对应 Prefab；没有新增 DB、存档、全局事件、分析埋点或平台 SDK。

## 2. 受影响文件与资源

```text
Assets/Game/Scripts/Level/DrawLevel/Level53/
|-- DLevel53Ctrl.cs       # 玩家命中、受击表现与失败演出
|-- DLevel53Bee.cs        # 两帧动画、追踪、碰撞施力和反冲
`-- DLevel53Hive.cs       # 蜂群模板、批次、随机路线与运行实例

Assets/Game/Scripts/Level/DrawLevel/
|-- DrawLevelCtrl.cs          # 延迟失败演出的占用、完成、取消与结算门禁
`-- DrawLevelCountdownCtrl.cs # 失败演出占用期间冻结共享成功倒计时

Assets/Game/Prefabs/DrawLevel/
|-- DLevel_053.prefab
`-- Level53/
    |-- DLevel53Bee.prefab
    `-- DLevel53Hive.prefab
```

- 关卡数据源：`Luban/Configs/Datas/LevelData.xlsx` 的 `draw_level` 已包含
  `20053 / DLevel_053 / 蜂群围攻`；`LevelSort.xlsx.drawLevelSort` 已追加
  `45 / 20053 / 蜂群围攻`。
- 已执行项目 Luban 客户端 JSON 生成；`tbdrawleveldata.json` 和
  `tbdrawlevelsort.json` 分别唯一包含一次 `20053`，关卡资源路径与排序入口已闭环。

## 3. 组件树与职责

```text
DLevel_053
|-- DrawLevelCtrl
|-- DrawLineFeature
|-- DrawLevelCountdownCtrl            # 10 秒，到时 Success
|-- DLevel53Ctrl                      # 玩家命中和失败演出
|-- Actor                             # D053 当前唯一追踪和受击目标
|-- DLevel53Hive instance
|   |-- SpawnPoint
|   `-- BeeTemplate_AdjustHere        # inactive，可直接调本关 Bee 参数
`-- Knock_Explosion                   # 复用 D003 接触点特效

DLevel53Bee prefab
|-- SpriteRenderer                    # 两帧图片
|-- Rigidbody2D                       # Dynamic，零重力
|-- CircleCollider2D                  # 非 Trigger 实体碰撞
`-- DLevel53Bee
```

| 组件 | 职责 | 不负责 |
| --- | --- | --- |
| `DLevel53Ctrl` | 玩家命中去重、接触点表现、失败演出占用与标准失败提交 | 蜂群生成、实例生命周期、单蜂飞行、共享倒计时 |
| `DLevel53Bee` | 两帧动画、追踪、朝向、接触点冲量、反冲和碰撞恢复 | 生成节奏、胜负倒计时、关卡重建 |
| `DLevel53Hive` | 模板、批次、出生/攻击分布、蜂间忽略碰撞、暂停和运行实例清理 | 单蜂碰撞解算、共享倒计时和最终结算 |
| `DrawLevelCountdownCtrl` | 第一笔后计时；失败演出占用期间停止扣时；到时提交 Success | 蜂群逻辑和受击表现 |
| `DrawLevelCtrl` | 输入门禁、原子结算、失败演出占用及标准失败重建 | DLevel_053 专用参数和单蜂行为 |

## 4. Prefab 与序列化绑定

### DLevel_053.prefab

| 字段 | 绑定或数值 |
| --- | --- |
| `DLevel53Ctrl.drawLevelCtrl` | 同根 `DrawLevelCtrl` |
| `actor` | 当前关卡 Player Actor |
| `hive` | `DLevel53Hive` 实例 |
| `knockEffect` | `Knock_Explosion` ParticleSystem |
| `failureDelay` | `1.5` 秒 |
| `DrawLevelCountdownCtrl.cdTime / isSuccess` | `10 / true` |
| `DrawLineFeature.strokeMass` | `50`；按长度约为 `12.5~50` |

### DLevel53Bee.prefab

| 字段或组件 | 数值 |
| --- | --- |
| `animationFrames / frameInterval` | 两帧 Sprite / `0.1` 秒 |
| `fadeInDuration / root Scale` | `0.2` 秒 / `0.825` |
| `trackingSpeed / trackingAcceleration` | `5.5 / 22` |
| `wobbleAmplitude / wobbleFrequency` | `0.16 / 2.2` |
| `impactImpulse` | 源 Prefab `350`；D053 场景 Override 后 `900` |
| `horizontalImpactImpulse / random angle` | `400 / ±8°` |
| `recoilDistance / recoilSpeed` | `0.35 / 9` |
| `impactCooldown` | `0.18` 秒 |
| Rigidbody2D | Dynamic、gravity `0`、Interpolate、Continuous、FreezeRotation |
| CircleCollider2D | 非 Trigger；物理材质为光滑材质 |

### DLevel53Hive.prefab

- `beeTemplate` 显式绑定 inactive 的 `BeeTemplate_AdjustHere`，`spawnPoint` 和 `runtimeBeeRoot` 均有绑定。
- 生成参数为总数 `8`、窗口 `3s`、批次 `1~3`、出生椭圆 `(0.6,0.3)`、最小间距 `0.4`、攻击角覆盖 `-42~42°`。
- 蜂巢自身不带 Collider；生成和实例管理由 `DLevel53Hive` 负责。

## 5. 生命周期

1. `DrawLevelCtrl` 初始化时扫描 `DLevel53Ctrl`、`DLevel53Hive` 与 `DrawLevelCountdownCtrl` 三个 `IDrawStrokeCompletedHandler`；Ctrl 只使用 Reset 生命周期，生成入口由 Hive 消费。
2. 第一笔有效画线完成后，共享倒计时从 10 秒开始；`DLevel53Hive` 随机生成每批 1 至 3 只的计划，总数固定为 8，计划分布在 3 秒窗口内。
3. 每只蜜蜂由 inactive 模板克隆并初始化，启用后与当前存活蜜蜂逐对调用 `Physics2D.IgnoreCollision`，绑定唯一 Player Actor 和本轮攻击角。
4. 蜜蜂以零重力动态刚体追踪玩家并播放两帧动画。撞到非 Trigger 场景碰撞体时：
   - 只对 Dynamic Rigidbody2D 在接触点施加冲量；
   - 静态或 Kinematic 障碍不受力，但蜜蜂仍反向快速退回；
   - 反冲期间临时忽略当前障碍，结束、禁用或销毁时恢复碰撞。
5. 蜜蜂碰到当前 Player 的真实 Collider 后，首只蜜蜂由 `DLevel53Ctrl` 调用一次 `TryBeginFailurePerformance()`，在接触点播放 Knock、播放音效并执行一次 `actor.Die()`；失败等待期间蜂群继续生成和运动。后续不同蜜蜂仍各播放一次命中表现，但不重复提交失败或调用 `Die()`。
6. `DrawLevelCountdownCtrl` 在 `IsFailurePerformanceActive` 为 true 时停止扣时。DLevel_053 使用不受 `timeScale` 影响的 1.5 秒等待，随后调用 `CompleteFailurePerformance()`，复用标准失败重建。
7. 若 10 秒先归零，共享倒计时提交 Success；`Resolved` 回调冻结蜂群。
8. `OnDrawAttemptReset`、`OnDisable` 和销毁路径会使异步版本失效、清理蜂群与粒子；`OnDisable` 还会取消尚未完成的失败演出占用。

## 6. 共享复用边界

- 复用：`DrawLevelCtrl` 的结果门禁、输入锁、失败演出占用和重建；`DrawLevelCountdownCtrl` 的首笔启动、HUD、暂停与成功提交；D003 的 Knock 粒子实例。
- 局部保留：随机批次、蜂巢散布、蜜蜂两帧动画、追踪、施力、反冲、蜂间碰撞隔离和玩家接触点定位。
- 不新增：全局 Physics2D Layer 矩阵修改、共享 Actor 行为、全局事件、DB 字段、资源异步加载、广告或分析流程。

## 7. 静态验证

- 三个目标 Prefab 的 YAML 定义数为主关卡/Hive/Bee `120 / 9 / 6`，重复定义均为 0，非零本地引用均无悬空。
- 主、Bee、Hive Prefab GUID 与三个脚本 GUID 均可解析；所有 `MonoBehaviour.m_Script` 非零，未发现 Missing Script。
- DrawLevel 目录只存在当前 `DLevel_053`、`DLevel53Bee`、`DLevel53Hive` 这一套命名；旧下划线资源与旧 GUID 无残留引用。
- Bee 的 Rigidbody2D、Collider、两帧 Sprite 和行为参数，以及 Hive 的出生点均已绑定。
- 相关三个 C# 和三个 Prefab 的 `git diff --check` 通过。
- 本轮最终 D053 三脚本隔离编译为 `0 error / 21 warning`；warning 均为引用既有 `Assembly-CSharp.dll` 后的同名类型 `CS0436`。当前全工程编译被无关的 `Level117Ctrl.cs` 缺失 `sweat*` 字段阻断。
- Luban 生成成功；运行时 Draw 数据共 46 项、排序共 45 项，D053 在两份 JSON 中均唯一。
- `uncertain`：尚未完成 Unity PlayMode 和 Android/iOS 横屏设备验证，不能据静态检查宣称实际手感、碰撞或性能已验收。

## 2026-08-13 蜂群分散、蜂窝职责与可调模板调整

### 需求与原行为

- 用户现场反馈蜜蜂几乎沿同一路线集中撞向一点，推不动当时质量为 `200` 的画线，并希望蜂群生成配置集中到蜂窝、场景内能直接选择蜜蜂模板调参；这是历史基线，当前线质量已改为按长度约 `12.5~50`。
- 原实现由 `DLevel53Ctrl` 持有 Bee Prefab、运行父节点、总数、生成窗口和批次；Hive 只提供半径 `0.25` 的圆形出生点。
- 原 Bee 每帧都直接追踪 Actor 中心，只有幅度 `0.16` 的横向摆动；历史 `impactImpulse=40 / strokeMass=200` 组合仅产生很小的速度变化。

### 最终脚本与组件树

```text
DLevel_053
|-- DLevel53Ctrl                  # 玩家判定、Knock、1.5 秒失败演出与标准结算
`-- DLevel53Hive instance         # 首笔回调、生成计划、分散路线、暂停与运行蜂清理
    |-- SpawnPoint
    `-- BeeTemplate_AdjustHere    # inactive 的 DLevel53Bee nested prefab，可直接调本关参数

DLevel53Bee prefab
|-- SpriteRenderer
|-- Rigidbody2D
|-- CircleCollider2D
`-- DLevel53Bee                  # 飞行、路线收束、碰撞冲量与反冲
```

| 类/组件 | 当前职责 | 明确不负责 |
| --- | --- | --- |
| `DLevel53Ctrl` | 识别本关登记玩家（D053 当前为单目标）、受击接触点特效、失败占用、角色死亡、延迟标准失败、Resolved 状态 | 蜂群批次、模板、出生点、路线和实例列表 |
| `DLevel53Hive` | 接收首笔/Reset，持有模板与全部蜂群生成参数，建立批次和攻击角，忽略蜂间碰撞，暂停及清理运行实例 | 单蜂碰撞解算、共享倒计时、最终结算实现 |
| `DLevel53Bee` | 两帧动画、固定攻击通道、近距离收束、追踪、冲量和反冲 | 蜂群数量、生成节奏、标准失败重建 |

### Inspector 与 Prefab 绑定

- `DLevel53Ctrl` 在 D053 prefab 中只绑定 `drawLevelCtrl`、首个 `actor`、`hive`、`knockEffect` 和 `failureDelay=1.5`；共享脚本另有可选 `additionalActors`，D053 留空以保持单目标行为。旧 `beePrefab`、`beeRoot`、总数/窗口/批次字段已移除。
- `DLevel53Hive` 持有：
  - `beeTemplate=BeeTemplate_AdjustHere`、`spawnPoint`、`runtimeBeeRoot`；
  - `totalBeeCount=8`、`spawnWindowSeconds=3`、批次 `1~3`；
  - 出生椭圆 `spawnAreaRadius=(0.6,0.3)`、同批最小间距 `0.4`、最多重采样 `12` 次；
  - 最大攻击偏角 `42` 度、槽内抖动 `0.25`。
- `BeeTemplate_AdjustHere` 是 Hive 内嵌的 `DLevel53Bee.prefab`，根对象保持 inactive；运行时从该场景实例克隆，先初始化再显式激活，因此在 DLevel_053 中对模板做的 Inspector Override 会由新生成蜜蜂继承。
- Bee 源 Prefab 的 `impactImpulse=350`，D053 场景模板 Override 后实际为 `900`。调整路径：展开关卡中的 `DLevel53Hive > BeeTemplate_AdjustHere`，在 `DLevel53Bee > 碰撞推力（推力调这里） > Impact Impulse` 修改。
- 蜜蜂模板新增 `attackRouteConvergeStartDistance=1.2` 和 `attackRouteDirectDistance=0.45`；这两个值控制靠近玩家时从分散路线平滑恢复直扑。

### 随机与物理流程

1. Hive 按总数生成一组覆盖 `-42~42` 度的均匀分层槽位；每个槽只在自身槽宽内随机抖动，再整体洗牌，因此每次尝试的顺序不同但覆盖不会重新集中。
2. 每批出生位置在 `0.6 x 0.3` 椭圆内采样，并优先满足 `0.4` 的同批间距；空间不足时使用有限尝试中最远的候选点。
3. 单蜂远离玩家时保持自身固定攻击角；进入 `1.2` 距离后按 `SmoothStep` 收束，距离小于等于 `0.45` 时完全直扑玩家，避免分散路线导致永远无法命中。
4. 蜂间继续逐对 `IgnoreCollision`。撞动态刚体时按真实接触点施加 `ForceMode2D.Impulse`；撞静态或 Kinematic 物体仍反冲但不尝试移动障碍。
5. 玩家首击时 Ctrl 在同一物理回调内占用一次失败；共享成功倒计时保持暂停，但 Hive 和运行蜂继续生成、飞行与碰撞，后续不同蜂仍可各播一次命中表现。
6. 克隆保持 inactive 完成引用和路线初始化，启用后在同一主线程调用内立即与现有运行蜂建立 `IgnoreCollision`，再登记到运行列表；这样满足 Physics2D 只对 active Collider 保留忽略状态的前提，同时尚未进入下一物理步。
7. Reset/Disable 先停计划，禁用运行蜂以立即退出物理，再销毁并清列表；inactive 模板从不登记到运行列表，因此不会被清理。

### 验证与缺口

- Unity 6000.0.59f2 已在打开的当前工程中完成最终 `Assembly-CSharp` 编译：Bee 构建日志在历史 `CS7036` 之后出现两次 `ExitCode: 0`，最终 `Library/ScriptAssemblies/Assembly-CSharp.dll` 的生成时间晚于本轮三个 D053 脚本。日志中的 `DLevel53Ctrl.cs(403)` 是职责迁移过程中的旧失败记录，当前该文件只有 278 行。
- Roslyn 隔离编译三个最终 D053 脚本亦为 `0 error`。Visual Studio 工程级 MSBuild 仍被当前生成 `.csproj` 中缺失的 `Temp/bin/Debug` 领域程序集阻断，但不再影响本轮 Unity 实际脚本编译结论。
- Hive/Bee/主关卡 Prefab 分别为 `9/6/120` 个唯一 YAML 定义，均无重复 fileID、悬空本地引用或 Missing Script；嵌套模板 source GUID、stripped Transform/MonoBehaviour 和 Hive 引用闭合。
- 目标代码与 Prefab 的 `git diff --check` 通过；静态确认 Ctrl 旧生成字段无残留、Bee 源 Prefab 冲量为 `350`、D053 Override 后实际为 `900`。
- 最终复审修正了在 inactive 新蜂上调用 `Physics2D.IgnoreCollision` 的问题；当前顺序为初始化、启用、同帧建立忽略关系、登记列表。
- `uncertain`：尚未运行 D053 PlayMode 或 Android/iOS 横屏测试。扇形路线的视觉宽度、实际撞线位置覆盖、实际主冲量 `900` 与水平冲量 `400` 对质量约 `12.5~50` 画线的手感及高速碰撞仍需在关卡内验收。

### 回滚

- 职责回滚：恢复 Ctrl 的 Bee Prefab/生成字段和列表，恢复 Hive 为单一出生点提供者，并移除 Hive 内嵌模板。
- 路线回滚：移除攻击槽位参数与 Bee 的固定偏角/距离收束，恢复直追 Actor 中心。
- 物理手感回滚：只将模板 `impactImpulse` 从 `350` 调回旧值 `40`，不需要改共享画线质量或 Physics2D Layer 矩阵。

## 2026-08-13 蜜蜂渐显、水平推动与失败后持续运动

### 需求与最终行为

- 用户要求蜜蜂出生时不要瞬间出现、整体再放大 `1.1` 倍；碰撞推动的大方向跟随蜜蜂入射方向，但需要小范围随机，并补足水平方向的推动。
- 玩家首次被蜜蜂扎到后仍走既有 1.5 秒延迟失败链，但失败演出期间不再冻结蜂群：已生成蜜蜂继续追踪和碰撞，Hive 继续执行尚未完成的批次计划。
- 首只有效命中只调用一次 `TryBeginFailurePerformance()` 和 `Actor.Die()`；后续每只不同蜜蜂仍可各播放一次真实接触点 Knock 与 `trap` 音效，但不会重复提交失败。同一蜜蜂的 `CollisionStay` 不会重复播放表现。

### Inspector 与运行时实际值

- `DLevel53Bee.prefab` 根缩放由 `0.75` 调为 `0.825`，即在上一版基础上放大 `1.1` 倍；`fadeInDuration=0.2s`，使用 SpriteRenderer Alpha 的 `SmoothStep` 渐显，暂停时不推进。
- Bee 源 Prefab 默认值为 `impactImpulse=350`、`horizontalImpactImpulse=400`、`impactDirectionRandomAngleDegrees=8`。主关卡 `DLevel_053.prefab` 对 Hive 内嵌模板存在 `impactImpulse=900` 的场景 Override，因此本关运行时实际使用“主冲量 `900` + 水平冲量 `400` + 主方向随机偏转 `±8°`”。
- 调参入口仍为 `DLevel53Hive > BeeTemplate_AdjustHere > DLevel53Bee > 碰撞推力（推力调这里）`：一般推动调 `Impact Impulse`，水平方向不足调 `Horizontal Impact Impulse`，方向离散程度调 `Impact Direction Random Angle Degrees`。
- 本关 `DrawLineFeature.strokeMass=50`，并启用按长度缩放与最低质量比例 `0.25`；旧记录中的质量 `200` 不再代表当前关卡配置。
- 蜜蜂自身反冲仍严格取原入射方向的反方向，不使用随机后的施力方向，避免反冲轨迹乱跳。

### 状态边界

1. `_isFailing=true` 只表示失败结果已被首击占用，不再作为蜂群暂停条件；`IsBeeSimulationSuspended` 在该状态下保持 false。
2. `DrawLevelCountdownCtrl` 继续在失败演出占用期间停止成功倒计时，HUD 与其它结果提交继续被共享失败占用门禁拦截。
3. 真正的设置暂停、成功结算和延迟失败结束后的标准失败重建仍会暂停并清理蜂群；Reset/Disable 仍使旧异步版本失效。

### 验证、缺口与回滚

- D053 三个最终脚本通过 Unity Roslyn 隔离编译：`0 error / 21 warning`；warning 均为隔离编译引用既有 `Assembly-CSharp.dll` 后的同名类型 `CS0436`。
- 静态核对 Bee/Hive/主关卡 Prefab 的字段、Override、GUID/FileID 和 Missing Script；`git diff --check` 通过。
- `uncertain`：尚未完成 Unity PlayMode、Android/iOS 横屏验证。需重点测试短线、半长线和满长线；当前最短线质量约 `12.5`，水平冲量 `400` 可能偏猛。若实机过猛，优先把 `Horizontal Impact Impulse` 调到 `250~350`，不要先改全局 Physics2D 或共享画线参数。
- 表现限制：多只蜜蜂同帧命中时复用同一个 Knock ParticleSystem，画面只保留最后一次命中的位置。
- 仅回滚本轮时，将 Scale 恢复 `0.75`、`fadeInDuration` 设 `0`、水平冲量设 `0`、随机角设 `0`，并恢复首次命中时暂停 Hive 的旧逻辑；共享失败占用和标准重建链无需回滚。

## 2026-08-13 蜜蜂头部持续朝向玩家

### 需求、旧行为与修复结果

- 用户现场反馈蜜蜂斜向飞行时仍保持水平，且头尾方向与角色不一致。旧 `RefreshFacing()` 只比较玩家与蜜蜂的 X 坐标并设置 `SpriteRenderer.flipX`，完全没有使用 Y 方向设置俯仰角；同时两帧源图的头和触角实际位于左侧，但 Bee Prefab 的 `spriteFacesRight` 旧值错误标成了 `true`。
- `DLevel53Bee` 初始化时缓存玩家根节点可用的非 Trigger 主 `Collider2D`，朝向点读取其 `bounds.center`；主碰撞体不可用时回退到 Actor Transform，运行时不逐帧调用 `GetComponent`。
- 每个追踪物理帧都先按角色左右方向翻面，再使用 `Vector2.SignedAngle` 将翻面后的头部水平轴旋转到角色身体中心。角色接近正上方或正下方时保留上一左右状态，使用 `0.02` 的水平死区避免临界位置连续翻面。
- 反冲期间仍刷新“头朝玩家”的视觉朝向，不会因为蜜蜂沿反方向后退而把头转向退路。物理刚体继续冻结碰撞产生的旋转，并由脚本通过 `SetRotation` 设置表现角；圆形碰撞体的轮廓不受该旋转影响。

### 影响边界、验证与回滚

- 只修改 `DLevel53Bee.cs` 和 `DLevel53Bee.prefab` 的局部朝向行为；生成批次、攻击通道、追踪速度、渐显、尺寸、碰撞推力、反冲距离和失败后继续运动保持不变。没有新增配置表、存档、全局事件、分析打点、资源地址或平台链路。
- D053 三脚本 Roslyn 隔离编译通过：`0 error / 21 warning`，warning 均为隔离编译同时引用已有 `Assembly-CSharp.dll` 的 `CS0436` 同名类型提示。Bee/Hive/主关卡三个 Prefab 静态检查均无重复 fileID、悬空本地引用或 Missing Script。
- `uncertain`：尚未执行 Unity PlayMode 与 Android/iOS 横屏视觉验收。需观察从玩家左右两侧、斜上方、斜下方以及接近垂直方向飞入时，头部是否始终对准身体中心，快速越过垂直线时是否仍有可见跳变。
- 只回滚本轮时，恢复旧的仅 X 轴 `RefreshFacing()`，删除目标 Collider 缓存与反冲朝向刷新，并将 Bee Prefab 的 `spriteFacesRight` 恢复为 `1`；其它 D053 玩法参数无需回滚。

## 8. 回滚

1. 从 `DLevel_053.prefab` 移除 `DLevel53Ctrl`、Hive 实例与 Knock 绑定，并移除本关成功倒计时配置。
2. 删除 `Level53/DLevel53Bee.prefab`、`DLevel53Hive.prefab` 及三个 Level53 脚本和对应 meta。
3. 从 `LevelData.xlsx`、排序源表及生成 JSON 移除 `20053`，再执行既有 Luban 生成流程。
4. 完整回滚还需还原 `DrawLevelCtrl` 的失败演出占用/完成/取消 API、输入与结算门禁，以及 `DrawLevelCountdownCtrl` 的失败演出冻结增量；若其它关卡届时已复用这些接口，应先确认调用方。
5. 不需要迁移或回滚玩家存档、全局事件、SDK 或共享 Physics2D 配置。

## 2026-08-18 D053-D061 蜜蜂命中肿胀动画

### 需求、基线与新行为

- 适用关卡为 `20053~20061`，运行资源依次为 `DLevel_053~DLevel_061.prefab`；九关继续共用 `DLevel53Ctrl` 的玩家命中和延迟失败链。
- 旧行为在首只蜜蜂命中后直接执行 `ActorCtrl.Die()`，因此播放默认 `die`；共享 `Actor.prefab` 使用的 `sp_Box_001` 骨骼没有 `swell`，仅调用动画名会在 Spine 层失败。
- 新行为仍先执行 `Die()` 设置 `IsDied`、停止移动并阻止自动 idle/walk/jump 覆盖，然后仅在首次命中时用非循环 `swell` 覆盖默认 `die`，结束后排队循环 `swell_idle`，直至 1.5 秒失败演出完成并标准重开。
- 后续不同蜜蜂仍各播放一次接触点 Knock 和音效，但不会重启肿胀动画或重复提交失败；同一蜜蜂仍由 `_playerHitBees` 去重。
- 若运行时骨骼缺少 `swell`，本地检查会记录错误并保留默认 `die`。失败异步在播放特殊动画前已启动，因此表现资源异常不会阻断标准失败重建。

### 影响文件与资源边界

- 代码：`Assets/Game/Scripts/Level/DrawLevel/Level53/DLevel53Ctrl.cs`。
- Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_053.prefab` 至 `DLevel_061.prefab`。
- 九个关卡都只对各自嵌套 Actor 增加局部 Override：SkeletonData 指向 `sp_box_025/Box_SkeletonData.asset`，两个 SkeletonPartsRenderer 材质指向同目录 `Box_Material.mat`。该组合复用 `Level_083.prefab` 的既有绑定方式。
- `sp_box_025` 保留旧骨骼的全部 64 个动画、7 个 Skin、旧 bones/slots，并新增 `swell`、`swell_idle`；没有修改共享 `Actor.prefab`，因此其它关卡不受本轮资源替换影响。
- 没有修改关卡配置、存档、DB、共享事件、分析打点、广告、SDK 或平台分支。

### 验证、未验证项与回滚

- Unity 6000.0.59f2 自带 Roslyn 按当前 `Assembly-CSharp.rsp` 全量编译为 `0 error / 107 warning`；warning 均为工程既有诊断，`DLevel53Ctrl.cs` 无诊断。
- 九个 Prefab 各精确包含 `1` 条 SkeletonData 和 `2` 条配套材质 Override；静态扫描分别无重复 fileID、无悬空本地引用，两个资源 GUID 均可解析；`git diff --check` 通过。
- `uncertain`：尚未执行 Unity PlayMode、Retry、各 Skin、D058/D059/D061 入场与释放衔接，以及 Android/iOS 横屏视觉验收。需重点确认命中后先完整播放 `swell`、材质无紫块/错图、第二只蜂不重启动画、1.5 秒后只重开一次。
- 仅回滚本轮时，删除 `DLevel53Ctrl` 的 swell 播放辅助逻辑，并从九个 Actor 实例移除上述三条资源 Override；共享 Actor、蜂群参数、倒计时和失败占用链无需回滚。
