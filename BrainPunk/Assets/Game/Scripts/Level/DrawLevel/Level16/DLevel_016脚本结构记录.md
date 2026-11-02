# DLevel_016 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 16 关
- 记录日期：2026-08-02
- 交付/验收状态：角色害怕初态、隐藏影子、首笔重型落石，以及落石/画线命中角色失败已配置；Unity 导入及编译通过，PlayMode 与设备验收待完成
- 参考基线关卡：DLevel_008 的角色害怕动作、DLevel_004 的首笔刚体释放
- 选择基线理由：DLevel_008 与本关引用同一 `Actor.prefab`，DLevel_004 已用
  `IDrawStrokeCompletedHandler` 把预置 Kinematic 刚体在第一笔有效画线后切为 Dynamic
- 基线证据限制：两处实现均有本地静态/编译记录，但完整玩家 PlayMode 与移动端验收状态为
  `uncertain`
- 主要脚本：共享 `DrawLevelCtrl.cs`、`DrawLineFeature.cs`、`DelayAutoBeginDrawHandle.cs`，
  复用 `Level4/DLevel4Ctrl.cs` 与 `Level4/DLevel4RockTrapHandle.cs`；本关没有专属运行时 C#
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_016.prefab`

## 2. 脚本与组件树

```text
DLevel_016
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DLevel4Ctrl                         # 复用：第一笔释放本关 Stone
├─ TrapEventHandlle                    # 用户已有旧组件；Stone 不再通过 Trigger 链调用
├─ Actor.prefab 实例
│  ├─ SkeletonAnimation                # _animationName=cover_ears01
│  ├─ ActorSpineCtrl                   # testAnimName=cover_ears01
│  ├─ pushCollider                     # 本关启用的右侧辅助碰撞体，Actor Layer / Untagged
│  └─ Shadow                           # 本关 override 为 inactive
├─ Stone
│  ├─ SpriteRenderer
│  ├─ Rigidbody2D                      # 初始 Kinematic，mass=100，gravityScale=1
│  ├─ CircleCollider2D                 # 非 Trigger
│  └─ DLevel4RockTrapHandle            # 复用：普通碰撞接触点、受击与失败链
├─ DrawGame_DrawLine 模板实例
│  └─ DLevel4RockTrapHandle            # 每条运行时线继承；直接或被 Stone 推入角色均判定
├─ DrawLineArea
└─ 关卡背景、斜坡与 Water/Buoyancy
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| DrawLevelCtrl | 共享主控制器 | 初始化本关、发现局部 Handler、派发笔画与胜负生命周期 | `IDrawLevelRuntime` | DrawPlayCtrl | 不持有 Stone 的具体配置 |
| DrawLineFeature | 共享功能 | 画线输入、线段创建和 `DrawStrokeResult` | MonoBehaviour | DLevel prefab | 不释放 Stone、不控制角色动作 |
| DelayAutoBeginDrawHandle | 共享局部 Handler | 按本关 delay 开放画线 | `IDrawLevelIntroHandler` | DLevel prefab | 不参与落石或碰撞 |
| DLevel4Ctrl | DLevel_004 建立、DLevel_016 直接复用的局部控制器 | 第一笔有效画线后把序列化目标刚体切为 Dynamic 并唤醒 | `IDrawStrokeCompletedHandler` | DLevel prefab | 不处理碰撞、失败、动画或重力轨迹 |
| Actor prefab 实例 | 共享角色 | 显示角色、碰撞与受击表现 | ActorCtrl/Spine 组件 | DLevel prefab | 共享资源不保存 Level16 专属初态 |
| DLevel4RockTrapHandle | DLevel_004 建立、DLevel_016 直接复用的局部碰撞处理器 | 普通 Collision2D 命中已绑定 Actor 的主体或子碰撞体后，按接触点播放受击、死亡并派发失败事件 | `IEventSender` | Stone 随 DLevel prefab、画线处理器随每条运行时线创建/销毁 | 不释放 Stone、不处理画线输入 |
| TrapEventHandlle | 用户已有旧组件 | 保留在根节点，但当前 Stone 不再通过不匹配的 Trigger 链调用 | `IEventSender` | DLevel prefab | 不参与当前 Stone 普通碰撞 |

## 4. 生命周期与关键调用

1. DrawPlayCtrl 实例化 DLevel_016；Stone 按 prefab 初态保持 Kinematic、Simulated，
   `mass=100`、`gravityScale=1`，暂不产生下落位移。
2. Actor 的本关 nested prefab override 把 SkeletonAnimation 初始动作设为
   `cover_ears01`，并关闭完整 Shadow GameObject；共享 Actor prefab 不变。任务前误带入的
   DLevel001Ctrl 已移除，避免首笔后每帧驱动 Static Actor 并把害怕动作切换成 walk/jump。
3. `DrawLevelCtrl.InitializeRuntime()` 在本关根节点发现 DLevel4Ctrl，并把它登记为
   `IDrawStrokeCompletedHandler`。
4. 第一笔有效画线完成后，DrawLevelCtrl 同步派发 `DrawStrokeResult`；DLevel4Ctrl 仅在
   `IsFirstStroke` 为 true 时把唯一绑定的 Stone Rigidbody2D 切为 Dynamic 并 `WakeUp()`。
5. Stone 随后由 Unity 2D 重力、斜坡和已有碰撞体驱动；控制器不逐帧改写位置或速度。
6. Stone 或每条运行时画线与角色发生普通 Collision2D 时，DLevel4RockTrapHandle 从命中
   Collider 向父级解析 `ActorCtrl`，并要求它与本关序列化绑定的 Actor 完全相同。因此角色
   主体的 Player Collider 和右侧 `Untagged` 的 `pushCollider` 都会进入同一失败链；Stone
   推着线撞到 `pushCollider` 时不会再被 Tag 门禁漏掉。处理器优先用
   `GetContact(0).point` 定位本关既有受击粒子，再复用角色死亡、击退、`trap` 音效和
   `OnTriggerTrapEvent`。Collider 仍保持非 Trigger，以继续碰撞地形、Stone 和画线。
7. 后续笔画不会再次释放 Stone；本次不修改 `allowMultipleStrokes` 的现有配置。
8. Reset、Replay、失败或退出由既有 Draw 流程销毁并重建整关，新实例恢复 Stone 的
   Kinematic 初态、角色害怕动作和无影子配置。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| DLevel_016 根 | DLevel4Ctrl | 脚本 GUID `f273510b4b568a24182292bc67ed25a1` | 必填 | 第一笔不会释放 Stone |
| DLevel4Ctrl | `boxRigidbodies[0]` | Stone Rigidbody2D fileID `2062884533412947148` | 必填 | 空引用被跳过，Stone 保持 Kinematic |
| Stone | Rigidbody2D | 初始 Kinematic、Simulated、mass=100、gravityScale=1 | 必填 | 无法按预期从首笔开始以重物惯性受重力和碰撞 |
| Actor 实例 | SkeletonAnimation `_animationName` | `cover_ears01` | 必填 | 角色会回到共享 prefab 或其它 override 的初始动作 |
| Actor 实例 | ActorSpineCtrl `testAnimName` | `cover_ears01` | 编辑器一致性 | 运行时主要初态仍由 SkeletonAnimation 决定，Inspector 预览不一致 |
| Actor/Shadow | GameObject `m_IsActive` | `0` | 必填 | 本关会显示共享 Actor 的默认影子 |
| Stone | DLevel4RockTrapHandle | Actor、既有 Knock ParticleSystem、`isCollision=1` | 必填 | Stone 普通碰撞无法进入受击/失败链 |
| DrawGame_DrawLine 模板根 | DLevel4RockTrapHandle | 同一 Actor、既有 Knock ParticleSystem、`isCollision=1` | 必填 | 运行时线直接或被 Stone 推入角色时不会进入失败链 |

## 6. 数据、UI、资源与事件流

```text
玩家完成第一笔有效画线
  -> DrawLineFeature 产出 DrawStrokeResult(IsFirstStroke=true)
  -> DrawLevelCtrl 派发 IDrawStrokeCompletedHandler
  -> DLevel4Ctrl
     -> Stone Rigidbody2D.bodyType = Dynamic
     -> Stone Rigidbody2D.WakeUp()
  -> Unity 2D 重力驱动 Stone 下落
  -> Stone 直接接触 Actor，或 Stone 推动运行时线接触 Actor/pushCollider
  -> DLevel4RockTrapHandle
     -> GetComponentInParent<ActorCtrl>() == 本关绑定 Actor
     -> 接触点播放既有 Knock
     -> Actor.Die + Knockback + trap SFX
     -> OnTriggerTrapEvent
  -> 既有失败重建链
```

- DB/存档：无新增读写；DrawGame 原有进度链不变。
- Luban/配置：无新增字段或读取。
- UI：无直接操作。
- 资源：复用共享 Actor、其 Spine 动画 `cover_ears01`、本关已有 Stone 与受击粒子；
  无新增资源地址或运行时加载。
- 事件：无新增全局事件；只复用 Draw 局部 Handler 分发和 prefab 已有 UnityEvent。
- Analytics/广告/SDK/平台分支：无修改。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| DLevel_008 的 `cover_ears01` 初态只以 Level16 nested override 复用 | 两关位置、朝向和其它 Actor override 不同 | Level16 默认害怕，DLevel008 与共享 Actor 不变 | 首帧、Reset、受击 | 否，关卡表现配置 |
| Level16 关闭完整 Shadow GameObject | 用户明确要求本关无影子 | 角色脚下不显示共享影子 | 首帧、移动、Reset | 否，关卡表现配置 |
| 移除误带入的 DLevel001Ctrl | 该组件会在首笔后每帧驱动 Static Actor，Editor.log 已出现 `Cannot use 'linearVelocity' on a static body.`，同时会覆盖害怕动画 | 角色保持本关原地害怕状态，不再产生静态刚体速度错误 | 首笔、持续运行、Reset | 否，修正错误关卡组件 |
| DLevel_004 同一 DLevel4Ctrl 在 Level16 只绑定一个 Stone | 首笔 Kinematic→Dynamic 行为完全一致 | 第一笔结束后石头立即开始受重力 | 首笔、重复笔画、Reset | 已复用既有稳定模式 |
| Level16 的 Stone 从旧 OnTrigger2DHandle 改为复用 DLevel4RockTrapHandle | Stone 与 Actor 都是非 Trigger，旧 OnTriggerEnter2D 不会收到普通碰撞 | 石头撞人可进入失败链，粒子位于真实接触点，同时仍碰撞地形/画线 | Stone/Player、Stone/地形接触 | 已复用 DLevel_004 稳定模式 |
| Stone mass 从 1 调为 100 | 本关线条按长度实际为 7.5～30；原石头甚至轻于最短线，会被线明显推走 | Stone 约为满长线 3.33 倍、常见短线 8～10 倍，更接近重物压线而非被线推动 | 短/长线侧推、托举、斜坡滚落 | 本关必要物理参数 |
| 玩家判定从“Collider 自身 Actor Layer + Player Tag”改为“父级 ActorCtrl 等于绑定 Actor” | 本关启用的右侧 `pushCollider` 属于同一 Actor，但为 `Untagged`；被 Stone 推动的线会先撞到它 | 主体和辅助碰撞体都只触发绑定角色的失败链，不扩大 Player Tag 对其它共享触发器的影响 | 直接画线、Stone 推线、Stone 直撞、其它碰撞体 | 复合角色 Collider 的稳定局部模式 |

## 8. 可复用结论与优化候选

- 对“第一笔后把一组预置 Kinematic 刚体切为 Dynamic”的完全同构需求，直接复用已有
  `IDrawStrokeCompletedHandler` 组件即可，无需复制 Level16 控制器。
- 历史类名 `DLevel4Ctrl` 和字段名 `boxRigidbodies` 暂时保留，以维持既有脚本 GUID、
  DLevel_004 prefab 绑定与最小变更；注释已明确当前跨 DLevel 的职责。
- 角色动作和 Shadow 都属于关卡实例表现，应使用 nested prefab override，不修改共享 Actor。
- 复合角色的危险碰撞判定应以宿主明确绑定的 `ActorCtrl` 身份为准；不要要求每个辅助
  Collider 重复 Player Tag，否则会漏判，也可能在补 Tag 后扩大其它共享触发器范围。
- 若后续更多关卡采用同一首笔释放组件，再评估无关卡编号的共享命名与安全迁移；当前不做
  脚本 GUID/类名迁移。

## 9. 验证证据与缺口

- 静态检查：DLevel_016 共 136 个 YAML 对象锚点，全部唯一且内部 fileID 引用无缺失；
  DLevel4Ctrl GUID 唯一、只绑定一个 Stone Rigidbody2D；Actor 恰有两项 `cover_ears01`
  override 和一项 Shadow inactive override；Stone 保持 Kinematic、Simulated、mass=100、
  gravityScale=1；DLevel4RockTrapHandle 在 Stone 和 DrawGame_DrawLine 模板各有一份，二者的
  actor/knock/isCollision 均绑定完整；Actor 的 `pushCollider` 保持 Actor Layer、Untagged 并在
  本关启用；DLevel001Ctrl GUID 与组件 fileID 在本关均为 0 次。
- Unity：6000.0.59f2 已完成 DLevel_016 PrefabImporter、Level16 文档 TextScriptImporter，
  本次目标识别修改后再次记录 `*** Tundra build success (4.00 seconds)` 与
  Assembly-CSharp CopyFiles；刷新后的日志未见 `error CS`、Compilation failed、
  Prefab import failed 或缺失引用异常。
- Diff：目标 C#/文档的 `git diff --check` 通过；DLevel_016 是用户当前未跟踪 prefab，
  其 40 行 Unity 既有空值尾随空格保持原状，本次新增行未引入额外尾随空格。
- Unity PlayMode：未执行。
- Android/iOS：未执行。
- 未验证项：第一笔落石时机、石头沿斜坡/画线的真实轨迹、角色无影子首帧、Stone 直撞、
  直接画线与 Stone 推线命中主体/pushCollider、失败防重、Reset/Replay 恢复及移动端物理一致性。

## 10. 风险与回滚

- 主要风险：DLevel4Ctrl 与 DLevel4RockTrapHandle 保留历史 Level4 命名，Inspector 可读性
  依赖文档；本关完整物理轨迹、接触点视觉与失败时序仍未经 PlayMode 验收。若后续新增角色
  移动需求，应单独设计 Static Actor 的移动/动画状态，不恢复 DLevel001Ctrl 直接驱动。
  Stone 仍使用 Discrete 碰撞检测，高质量重物压住很细或高速运动的线时可能抖动/穿透，
  当前只记录风险，不在没有实测证据时扩大修改。按 Actor 层级识别后，该绑定角色下任何
  激活 Collider 都会被视为角色命中；这是复合 Collider 的预期语义，新增辅助 Collider 时仍需
  在 PlayMode 确认其几何范围。
- 回滚路径：从 DLevel_016 根移除 DLevel4Ctrl 组件及 Stone 引用，把 Actor 两项动画 override
  恢复为 `idle` 并移除 Shadow inactive override；若需恢复任务前碰撞配置，再把 Stone 组件
  脚本与字段恢复为原 OnTrigger2DHandle UnityEvent。DLevel_004、共享 Actor、Stone Collider、
  DB、资源和平台逻辑无需回滚；本次重量参数可单独把 Stone mass 从 100 恢复为 1。若只回滚
  本次推线命中修复，可把 `IsPlayerCollider` 恢复为 Collider 自身 Layer/Tag 门禁，但会重新漏掉
  `Untagged` 的 `pushCollider`。
