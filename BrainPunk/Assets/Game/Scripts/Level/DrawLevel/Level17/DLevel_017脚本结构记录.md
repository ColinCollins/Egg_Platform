# DLevel_017 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 17 关
- 记录日期：2026-08-02
- 交付/验收状态：触发式弹簧、固定镜头与四向 FallArea 已配置；既有完整 C# 编译记录和本次冲量链静态校验通过，PlayMode 与设备验收待完成
- 参考基线：Level032 的弹簧美术与发射手感、DLevel001 的固定镜头 FallArea 布局
- 选择理由：Level032 提供按钮刚体、SpringJoint 和动态弹簧线参考；本关角色不能跳且按钮不能
  阻挡，因此删除按钮 Collider，只保留视觉刚体/Joint 与独立 Trigger。DLevel001 使用同正交尺寸 5.9，
  可作为固定横屏边界坐标证据
- 主要脚本：`DLevel17Ctrl.cs`，以及共享 `DrawLevelCtrl.cs`、`DrawLineFeature.cs`、
  `DLevel001Ctrl.cs`、`OnTrigger2DHandle.cs`、`FailAreaTriggerHandle.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_017.prefab`

## 2. 脚本与组件树

```text
DLevel_017
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DLevel001Ctrl                    # 第一笔后自动向右移动，保留用户已有配置
├─ DelayAutoBeginDrawHandle
├─ TrapEventHandlle                 # 用户复制时已有；当前不是弹簧调用目标
├─ DLevel17Ctrl                     # 本关弹簧发射与 Reset
├─ Actor                            # Player / ActorCtrl / Rigidbody2D
├─ Spring                           # 弹簧机关根
│  ├─ BoxCollider2D (Trigger/Draw)  # 唯一机关 Collider，不阻挡角色且不阻挡画线
│  ├─ OnTrigger2DHandle             # 根/子 Collider -> DLevel17Ctrl.TriggerSpring
│  ├─ Sp_button_1_02                # Transform + Sprite + Rigidbody + Joint，无 Collider
│  └─ Spring / Level5TrackLine      # LineRenderer 实时跟随 Bottom 与按钮
├─ GameCamera                       # 固定 (0,0,-10)，orthographicSize 5.9
├─ DrawLineArea
├─ FallArea_Left
├─ FallArea_Right
├─ FallArea_Bottom
├─ FallArea_Top
└─ DrawGame_SuccessDoor
```

## 3. 职责与边界

| 类/组件 | 职责 | 不负责 |
| --- | --- | --- |
| `DrawLevelCtrl` | Draw 生命周期、结果、FallArea 失败和 Reset 分发 | 不计算弹簧轨迹 |
| `DLevel001Ctrl` | 第一笔后每帧持续驱动 Actor 向右；空中也保持水平移动 | 不触发弹簧，不改 FallArea |
| `DLevel17Ctrl` | 过滤本关 Actor、预压按钮、释放觉 Joint、施加真实右上冲量、短时保留外部 X 动量、冷却与 Reset | 不锁定/恢复 Actor 移动，不跟随镜头、不判断胜利、不改全局 fixedDeltaTime |
| `ActorCtrl` | 默认关闭的可选模式下，保留与输入同向且快于基础速度的外部 X 动量 | 不自行决定机关开启时间；未开启时既有移动语义不变 |
| `OnTrigger2DHandle` | 不按 Tag 预筛，转发进入根 Trigger 的 Collider2D 参数 | 不直接写 Rigidbody2D；最终身份由 DLevel17Ctrl 判断 |
| `Sp_button_1_02` | 用无 Collider 的 Rigidbody2D + SpringJoint2D 复刻 Level032 回弹 | 不与 Actor、地板或画线发生实体碰撞 |
| `Level5TrackLine` | 每帧把 Bottom/按钮坐标写入 LineRenderer | 不计算 Actor 发射冲量 |
| `DrawGame_DrawLine` | 作为运行时笔画克隆模板；本关根 Rigidbody2D 局部绑定 DrawStrokeFree | 不修改共享模板或材质资源 |
| `FailAreaTriggerHandle` | 共享四向越界事件 | 不持有 DLevel17Ctrl 引用 |
| GameCamera | 固定视野和分辨率适配 | 不跟随 Actor |

## 4. 生命周期与关键调用

1. DLevel 实例启用时，`DLevel17Ctrl` 解析 Actor、按钮 Transform/刚体/Joint，并缓存按钮位置、
   Joint 开关、按钮视觉重力与 Actor 初始重力。
2. 第一笔有效画线后，既有 DLevel001Ctrl 驱动 Actor 向右；DLevel17Ctrl 的
   `OnDrawStrokeCompleted` 不启动机关。
3. Spring 根 Trigger 位于 Layer 14 `Draw`。DrawLineFeature 的阻挡 mask 排除 Draw，因此该区域允许
   落笔与连续画线；项目 Physics2D 矩阵当前全开，Actor 进入时 OnTrigger2DHandle 仍调用
   `TriggerSpring(Collider2D)`。
4. DLevel17Ctrl 校验进入者就是绑定 Actor、关卡未暂停、未进入成功/失败/结算链、角色未死亡且
   不在冷却，然后启动协程；暂停期间本机关的压缩、等待和发射后计时冻结。
5. 协程按 `0.2/0.1s` 的 Level032 节奏压缩按钮，然后启用 `4Hz / 0.1 damping` Joint；
   Level5TrackLine 跟随按钮形成伸缩。实际发射前再次校验 `IsOutcomeLocked`，开启 Actor 的
   可选水平动量保留，再对 Rigidbody2D 调用
   `AddForce(new Vector2(rightwardImpulse, upwardImpulse) * launchStrength, ForceMode2D.Impulse)` 与 `WakeUp()`。
6. Actor 质量为 3，Prefab 当前 `rightwardImpulse=12`、`upwardImpulse=72`、
   `launchStrength=3`，实际冲量为 `(36,216)`，等效 `Δv=(12,72)`。DLevel001Ctrl 始终
   每帧输入向右，本关基础 X 速度为 3；Actor.HandleMovement 在 0.35s 保留模式内仅保留
   同向且更快的外部 X，无碰撞时短时 X 理论约 15，并始终保留 Y。重力独立在 `1.1s` 后恢复。
7. `OnDrawAttemptReset` 或 `OnDisable` 停止协程，显式关闭外部水平动量保留，禁用 Joint、清零按钮刚体并恢复按钮、重力和冷却；
   不调用 `StopMoving`/`ResumeMoving`，也不持有 Actor 移动锁。
8. Actor 进入任一 FallArea 时，既有 DrawLevelCtrl 失败重建链接管；DLevel17Ctrl 不重复派发结果。

## 5. Inspector 绑定矩阵

| 宿主 | 字段/事件 | 当前绑定 | 必填影响 |
| --- | --- | --- | --- |
| DLevel_017 根 / DLevel17Ctrl | `drawLevelCtrl` | 同根 DrawLevelCtrl | 缺失时暂停及结算状态门禁不可用，但可局部回退解析 |
| 同上 | `actor` | 本关 ActorCtrl | 缺失时不发射 |
| 同上 | `upwardImpulse` | `72` | 右上 Impulse 的基础 Y 分量；Actor 质量变化后必须重调 |
| 同上 | `rightwardImpulse` | `12` | 右上 Impulse 的基础 X 分量 |
| 同上 | `rightwardMomentumRetentionDuration` | `0.35` | 保留同向且快于基础速度的外部 X 动量；不锁移动 |
| 同上 | `launchStrength` | `3` | 当前用户力度倍率；同时缩放 X/Y，实际冲量 `(36,216)` |
| 同上 | `springButton` | `Spring/Sp_button_1_02` Transform | 缺失时仍发射，仅跳过按钮动画；脚本可按路径回退解析 |
| 同上 | `springButtonBody` | 同按钮 Rigidbody2D | 缺失时角色仍发射，但视觉回弹退化为直接复位 |
| 同上 | `springJoint` | 同按钮 SpringJoint2D | 缺失时角色仍发射，但没有 Level032 弹性形变 |
| DrawGame_DrawLine 根 Rigidbody2D | `m_Material` | `DrawStrokeFree.physicsMaterial2D` | 摩擦 0、弹性 0、Minimum；运行时画线子 Collider 共用该刚体 |
| Spring / OnTrigger2DHandle | `targetTag` | 空 | 同时接收 Player 根和 Untagged 子 Collider；控制器仍严格校验 Actor 身份 |
| 同上 | `onEnter[0]` | `DLevel17Ctrl.TriggerSpring` | 缺失时机关不启动 |
| 四个 FallArea | source prefab | GUID `9c59e0ad78dc52c47a66a96c8207542f` | 缺失时对应方向可逃逸 |

## 6. 数据、资源、UI 与事件

- DB/存档/Luban：无新增读写或字段；关卡参数保存在 DLevel_017 Prefab。
- 资源：复用 Level032 已复制进本关的弹簧美术、Level5TrackLine、LineRenderer 材质、
  `DrawStrokeFree.physicsMaterial2D` 与既有 `boardTrigger`、`spring` 声音；复用共享 FallArea 和 GameCamera，
  没有新地址或异步加载，共享材质本体未修改。
- UI：无直接操作；固定镜头不新增 UI 或 Cinemachine 控制。
- 事件：弹簧使用本地 UnityEvent；越界继续使用共享 `OnTriggerFailAreaEvent`。
- Analytics、广告、SDK、Android/iOS 分支：无修改。

## 7. 相对参考基线的差异

| 差异 | 原因 | 运行时影响 |
| --- | --- | --- |
| 不挂 Level32Ctrl | 常规关依赖 BaseLevelCtrl、UI block、纵向镜头和全局 fixedDeltaTime | Draw 关保持局部、无主线副作用 |
| 按钮只移除 Collider，保留视觉 Rigidbody/Joint | 用户明确不要挡路，同时要求参照 Level032 形变 | 角色可穿过，按钮仍按参考 Joint 拉伸震荡 |
| Trigger 调整为 `2.2×1.4`、offsetY `-0.5` | 旧感应区高于按钮与实际行走路径 | 覆盖根/子碰撞体进入高度且不产生阻挡 |
| Spring 根从 Layer 9 改为 Layer 14 Draw | 非 Draw Layer Trigger 在 queriesHitTriggers=true 时会进入画线阻挡查询 | 感应区允许落笔和连续画线；全开物理矩阵仍允许 Actor Trigger 回调 |
| Trigger `targetTag` 留空 | Actor 前置子 Collider 为 Untagged，会先进入 | 两类 Collider 都能转发，再由控制器精确过滤 |
| Actor 质量 3，发射为 `(12,72)×launchStrength(3)` 右上真实冲量，重力 4.5 | 用户要求弹射有右上力 | `Δv=(12,72)`；高度、水平距离和门落点需 PlayMode 验收 |
| ActorCtrl 新增默认关闭的同向外部水平动量保留，DLevel17 开启 0.35s | 避免 DLevel001Ctrl 下一帧用基础 X 速度覆盖冲量 | 只保留同向且更快的 X，Reset/Disable/到期/协程结束都关闭；角色始终向右 |
| 删除移动锁、落地解锁与 `airborneControlLockDuration` | 用户要求弹射中也始终向右 | 机关不调用 Stop/Resume/SetMoveInput；动量保留不是移动锁 |
| 本关画线模板根 Rigidbody2D 局部绑定 DrawStrokeFree | 用户要求画线物理很光滑 | 运行时画线摩擦 0、弹性 0；其它 DLevel 与共享模板不变 |
| 增加冷却和统一 Reset | Draw 失败会重建，且 Actor 可能有多个 Collider | 避免重复弹射和旧协程污染新 attempt |
| 增加顶部 FallArea | 用户要求天空也闭合 | 角色向上逃逸进入既有失败链 |

## 8. 验证与缺口

- 本次最终 Assembly-CSharp 完整 C# 编译：`0 error / 104 warning`。
- Prefab：166 个 YAML 锚点唯一、本地引用完整；脚本 GUID、五个核心引用、Trigger 和 UnityEvent
  绑定有效；四个 FallArea source GUID、位置、缩放和 Collider override 完整。
- 冲量链静态校验：Actor Rigidbody2D 质量为 3，DLevel_017 序列化 `rightwardImpulse=12`、
  `upwardImpulse=72`、`rightwardMomentumRetentionDuration=0.35`、`launchStrength=3`；代码按
  `AddForce(new Vector2(rightwardImpulse,upwardImpulse)*launchStrength, Impulse) -> WakeUp()` 执行，
  已移除 `Knockback`/直接速度写入。DLevel17Ctrl 不再包含 `SetMoveInput(0)`、`StopMoving()`、
  `ResumeMoving()`、移动锁、落地解锁或 `airborneControlLockDuration`；重力仍由独立的 `1.1s`
  延时恢复。ActorCtrl 的新保留开关默认关闭，DLevel17 开启 0.35s 并在到期、
  Reset/Disable/协程结束显式关闭。暂停期间计时冻结，发射前重新校验 Actor 与结算状态。
- 画线/触发静态校验：Spring 根为 Layer 14 Draw，DrawLineFeature 阻挡 mask 排除 Draw；
  Physics2D Layer 矩阵当前全开，OnTrigger2DHandle 仍由 DLevel17Ctrl 严格过滤本关 Actor。
- 画线材质静态校验：DLevel017 的嵌套画线模板恰有一个 `m_Material` override 指向
  `DrawStrokeFree`（摩擦 0、弹性 0、Minimum）；DrawLineFeature 克隆根刚体且不覆盖材质，
  子 Box/Capsule Collider 共用该刚体。共享 `DrawGame_DrawLine.prefab` 仍未绑定材质。
- 固定镜头：无 Follow/Cinemachine/TileMapInfinite；本次没有新增镜头组件。
- Diff：目标 C# 与文档未发现新增空白错误；未跟踪 Prefab 以 YAML 结构断言代替 Git 差异检查。
- 未执行：PlayMode 中 Actor/画线滑动、画线/地面接触、成功门完整通关、
  FallArea 高速穿越、Reset/Replay、Android/iOS 横屏物理。
- `uncertain`：当前 `launchStrength=3` 的右上冲量最终高度、0.35s 水平动量保留手感、
  成功门落点、Trigger 区域落笔/连续画线与 `1.1s` 重力恢复仍需 PlayMode 实测。若 Actor 质量
  改变，必须按 `Impulse=mass×目标Δv` 同时重算 X/Y。

## 9. 风险与回滚

- 主要风险是固定镜头下的弹道、Trigger 实际接触范围和顶部失败时机尚未 PlayMode 验收。
  四向边界按最大支持宽高放在视野外，不应在正常画面内误触，但高速角色仍需实测。
- 仅回滚右向冲量时，删除 DLevel17 的右向参数/动量开关管理和 ActorCtrl 默认关闭的
  保留 API/分支，恢复为纯向上 AddForce；Spring 根 Layer 14、视觉 Joint 和四向 FallArea 不随之回滚。
- 仅回滚光滑画线时，删除 DLevel017 内嵌套画线模板根 Rigidbody2D 的
  `m_Material -> DrawStrokeFree` override；共享模板与材质资源无需回滚。
- 回滚本次视觉对齐时只移除按钮 Rigidbody/Joint 和控制器对应引用，恢复 Transform 回弹；
  不要恢复会挡路的按钮 Collider。完整功能回滚才成组移除 DLevel17Ctrl、根 Trigger/事件与
  四个 FallArea，LineRenderer、Actor、成功门和场景美术均保留。
  Level032、共享 FallArea/GameCamera、DrawLevelCtrl 和平台数据无需回滚。
