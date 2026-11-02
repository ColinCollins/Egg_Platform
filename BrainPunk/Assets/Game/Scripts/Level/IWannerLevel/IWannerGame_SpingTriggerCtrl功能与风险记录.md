# IWannerGame_SpingTriggerCtrl 功能与风险记录

日期：2026-08-13

## 需求 / 背景

- 用户要求新增 `IWannerGame_SpingTriggerCtrl`：可配置是否允许多次触发，默认允许；触发后弹簧对象按配置 `offset` 快速移动，再按独立时长返回原局部位置。
- 进入 Trigger 的 Actor 获得可配置的向上 force；触发 Tag 同样可配置，默认为 `Player`。
- 2026-08-13 最新补充要求：有效触发弹簧时保留 Actor 当前水平线速度，只把垂直线速度清零，再施加向上冲量。
- 类名与文件名按用户指定保留 `Sping` 拼写，本次不静默改名为 `Spring`。

## 影响文件、资源与系统

- 新增脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_SpingTriggerCtrl.cs`。
- 新增 Unity 元数据：`IWannerGame_SpingTriggerCtrl.cs.meta`。
- 依赖项目已有 `Collider2D` / `Rigidbody2D` 触发和 DOTween Transform 动画；警告日志使用 `Bear.Logger`。
- 当前 `Assets/Game/Prefabs/IWannerGameItems/IWannerGame_Spring.prefab` 已绑定该组件，序列化配置为
  `canTriggerMultipleTimes = true`、`triggerTag = Player`、`force = 50`；本次不修改 Prefab 配置。
- 未修改已验收关卡脚本、配置、存档、资源地址、UI、全局事件、打点或平台 SDK。

## 原有行为基线

- IWanner 目录已有受 `TriggerExecutor` 驱动的位移与缩放 Listener，但没有一个由 2D Trigger 直接驱动、同时处理弹簧往返位移与 Actor 向上冲量的通用组件。
- `Actor_IWanner.prefab` 根对象 Tag 为 `Player`，同对象挂有 Dynamic、Simulated 的 `Rigidbody2D` 与 `BoxCollider2D`；因此可通过进入碰撞体的 `attachedRigidbody` 作为受力边界。

## 新行为

- Inspector 暴露 `canTriggerMultipleTimes`（默认 `true`）和 `triggerTag`（默认 `Player`）。禁止多次触发时，每次组件启用期间只接受第一个有效进入。
- Inspector 暴露 `spring`、`offset`、`moveDuration` 和 `returnDuration`。组件启用时缓存弹簧对象的 `localPosition`，触发后先移到原位置加 `offset`，随后立即返回原位置；重复触发会停止旧 Tween 并重启，位移不累积。
- `spring` 未配置时使用组件自身 Transform。组件禁用时停止 Tween 并还原弹簧局部位置，避免重用对象保留半途状态。
- Inspector 暴露非负 `force`。有效且可模拟的非 Static `Rigidbody2D` 触发时，保留进入前的
  `linearVelocity.x`，只将 `linearVelocity.y` 清零，再通过
  `Rigidbody2D.AddForce(Vector2.up * force, ForceMode2D.Impulse)` 施加一次向上冲量；不修改角速度。
- 脚本要求同对象存在 `Collider2D`；该 Collider 未勾选 `Is Trigger`、命中对象没有可用刚体时，使用 `Bear.Logger` 输出警告。

## 数据 / 资源 / 事件流

- 运行时链路：`OnTriggerEnter2D -> Tag/次数门禁 -> DOTween localPosition 往返 -> 校验 attachedRigidbody ->
  保留 linearVelocity.x / 清零 linearVelocity.y -> 向上 Impulse`。
- 无 DB、Save、RemoteConfig、资源加载、实例化、UI、Bear 全局事件、Analytics 或原生覆盖层链路。

## 风险项

- `force` 采用 Impulse，同一数值在不同 Rigidbody2D 质量下会产生不同速度变化；具体关卡需以 Actor 质量和重力实测调参。
- Tag 只比较进入的 Collider 所在 GameObject；若后续改为子 Collider 且子对象不再使用 `Player` Tag，需同步调整 Prefab 或触发解析边界。
- 允许多次触发时，每个有效 `OnTriggerEnter2D` 都会再次施加冲量；多 Collider 或快速反复进出的具体 Prefab 需 PlayMode 核对手感。
- 当前会保留 Actor 触发前的水平速度，但移除下落/上升速度及平台携带速度中的 Y 分量；因此横向移动可连续，
  竖向运动会由本次 Spring 冲量重新起算。这是本次明确的手感边界。
- DOTween 使用 Unity 时间推进；暂停或原生覆盖层期间的动画节奏未为本功能单独改写。

## 验证

- 新脚本与文档 GUID 经 `rg` 全 Assets 搜索，均只出现在各自 `.meta` 中，未发现重复。
- 新增文件末尾空白静态扫描无命中；`DevelopMD/已有功能设计与使用记录.md` 执行 `git diff --check` 通过，仅有工作区 LF/CRLF 提示。
- 脚本静态扫描未发现 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/Save/PlayerPrefs 或全局事件链。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过：0 个错误、101 个现有警告。
- CodeGraph 复核确认 `OnTriggerEnter2D()` 是 `ApplyUpwardForce()` 的唯一调用方，方法内顺序为刚体有效性检查、
  保留 `linearVelocity.x` 并将 Y 设为 `0`、向上 `Impulse`。
- `uncertain`：尚未在 PlayMode 验证水平/竖直运动中触发、连续触发、平台携带、`force <= 0`、Reset/切关及
  Android/iOS 横屏表现。

## 回滚路径

- 回滚本次行为时，只删除 `ApplyUpwardForce()` 中保留 X、清零 Y 的 `linearVelocity` 赋值，并恢复本记录；
  无需修改 `IWannerGame_Spring.prefab`、配置、存档或事件。

## 2026-09-01：受力方向可配置与 Actor 横向速度衰减

### 需求 / 背景

- 用户要求完善 `IWannerGame_Spring` 能力，把固定向上的受力方向暴露到 Inspector，并以当前向上方向作为默认值。
- 实际运行组件仍为项目既有 `IWannerGame_SpingTriggerCtrl`；本次继续保留 `Sping` 类名和文件名，不做无关迁移。

### 影响文件、资源与系统

- 修改脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_SpingTriggerCtrl.cs`。
- 更新本记录与 `DevelopMD/已有功能设计与使用记录.md`。
- 静态资源搜索确认基础 `IWannerGame_Spring.prefab` 与 `ILevel_049.prefab` 直接引用该组件；两者均未修改。
- 未修改 Trigger、Collider、弹簧动画、关卡 prefab、存档、配置、资源加载、UI、事件、打点或平台 SDK。

### 原有行为基线

- 受力方向固定为世界空间 `Vector2.up`。
- 施加冲量前固定保留 `linearVelocity.x`、清零 `linearVelocity.y`，再执行
  `AddForce(Vector2.up * force, ForceMode2D.Impulse)`。

### 新行为与物理流

- Inspector 新增 `forceDirection`，默认 `Vector2.up`，表示世界空间方向。
- 运行时先归一化非零方向，使 `force` 继续只表示冲量大小；零向量安全回退为 `Vector2.up`，避免弹簧失效。
- 施力前不再固定清除 Y，而是通过向量投影清除刚体沿 `forceDirection` 的线速度分量，并保留垂直于受力方向的分量，
  随后沿归一化方向施加 Impulse。默认向上时该公式仍等价于“保留 X、清零 Y”。
- 源码核对确认 `ActorCtrl.HandleMovement()` 会在常规移动更新中重写 `linearVelocity.x`；只调用横向 `AddForce` 无法保证
  IWanner Actor 在下一帧保留弹射速度。已撤销 Spring 内固定 `0.2s` 保持 X 的过渡实现，Spring 不再持有计时器，
  也不再通过 `FixedUpdate` / `LateUpdate` 锁定角色速度。
- 当方向含 X 分量且目标为挂有 `Actor_IWanner` 的 Dynamic 刚体时，Spring 按 Impulse 公式
  `Δvx = direction.x × force / mass` 换算速度增量，调用 Actor 的 `AddDecayingHorizontalVelocity()`；多次有效触发会累加。
- `Actor_IWanner` 在共享输入移动与移动 Ground 速度计算完成后叠加外部水平速度，再按 Inspector 中
  `externalHorizontalVelocityDeceleration` 逐帧向 0 衰减。默认衰减量为 `80` 世界单位/秒²，不使用固定持续时间，
  因此角色输入与移动平台仍能参与最终 X 速度合成。
- 斜向弹簧的 Y 分量继续以 `ForceMode2D.Impulse` 交给 Physics2D；纯竖向与普通非 IWanner 刚体继续使用完整方向冲量。
  Actor 触墙、死亡或禁用时清除外部水平速度，避免残留速度跨状态继续生效。
- 选中组件时，原黄色 Gizmo 继续表示弹簧位移；新增青色单位长度线和终点球表示归一化受力方向，不表示 force 大小。
- 运行时链路更新为：`OnTriggerEnter2D -> Tag/次数门禁 -> 弹簧动画 -> 刚体有效性检查 -> 解析归一化方向
  -> 横向 IWanner Actor 按质量换算并累加 X 速度 / 竖向分量继续施加 Impulse -> Actor 更新中逐帧衰减 X 附加速度`。

### 验证

- Roslyn 语法树检查通过，无语法错误。
- 定向静态检查通过：默认向上、方向归一化、零向量回退、同轴速度投影清除、按方向 Impulse 与青色 Gizmo 均存在。
- 使用三组正负 X/Y 样例检查默认向上公式，结果与原 `(velocity.x, 0)` 完全一致。
- 已核对 `ActorCtrl.HandleMovement()`：默认每帧按输入和 `moveSpeed` 重写 X；外部速度因此由 `Actor_IWanner.OnUpdate()`
  在基础输入和移动 Ground 速度之后统一叠加，未修改共享 `ActorCtrl`。
- 横向路径定向检查通过：右/左方向均进入 Dynamic `Actor_IWanner` 衰减分支；以 `mass = 3`、`force = 50`
  计算，初始 X 速度增量分别约为 `+16.666667 / -16.666667`。默认衰减量 `80` 时，理论归零时间约 `0.208s`；
  实际离散帧结果取决于帧率，60 FPS 下约 `0.217s`。
- 静态确认 Spring 中已无 `horizontalMomentumRetentionDuration`、横向保持状态、`FixedUpdate` 或 `LateUpdate`；
  Actor 通过 `Mathf.MoveTowards` 按 `Time.deltaTime` 衰减，并在触墙、死亡、禁用时清理。
- `git diff --check` 通过；敏感项扫描未新增 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、`Reset()`、
  DB、Save、RemoteConfig、实例化、直接 `Debug.Log` 或全局事件。
- 完整 `Assembly-CSharp.csproj` 构建未进入本次源码的有效语义编译阶段：当前 `Temp/bin/Debug` 缺少 200 个
  项目依赖 DLL，`dotnet build` 以 `CS0006` 失败；该环境缺口不构成本次脚本编译通过证据。

### 剩余风险 / 未验证缺口

- `uncertain`：尚未在 Unity PlayMode 实测向上、水平、斜向、零向量、连续触发及不同质量刚体的手感。
- 玩家输入会与衰减速度叠加，而不是在固定时间内被锁死；仍需 PlayMode 调整默认 `80` 的手感，并检查连续弹簧累加的速度上限需求。
- 当前实现触墙时清除附加速度并把 X 速度置零；`uncertain`：斜墙、移动墙和墙体检测边界下是否需要更细的法线判断。
- 暂停期间 `Actor_IWanner.OnUpdate()` 不推进，因此外部速度及其衰减也会冻结，恢复后继续；该暂停语义尚未 PlayMode 验证。
- `uncertain`：尚未确认 Unity Editor 重载脚本后现有 prefab 实例的 Inspector 展示和重新序列化结果。
- 方向采用世界空间，不随 Spring Transform 旋转；需要随机关旋转的关卡应在 Inspector 同步配置方向。
- 尚未验证 Reset、切关、暂停、原生覆盖层及 Android / iOS 横屏真机行为。

### 回滚路径

- 删除 `forceDirection` 字段、方向解析与青色 Gizmo，把速度赋值恢复为 `(velocity.x, 0)`，并把 AddForce 恢复为
  `Vector2.up * force`，即可回到原行为。
- 仅回滚 Actor 横向衰减时，删除 `Actor_IWanner` 的外部速度字段、配置、累加/衰减/清理方法及更新调用，
  并让 Spring 恢复对完整方向直接执行 `AddForce`；无需修改共享 `ActorCtrl`。
- 本次没有 prefab、存档、配置、资源或事件数据迁移，回滚无需处理序列化资源。
