# DLevel_004 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 4 关
- 记录日期：2026-07-31
- 交付/验收状态：石头首笔释放与命中特效接触点修复已实现；Unity PlayMode 与移动端尚未验收
- 参考基线关卡：DLevel_005 的 Collision2D 陷阱调用链，以及 DLevel_004 原有首笔释放结构
- 选择基线理由：两者均依赖既有失败事件和整关重建；本关只需要局部修正石头命中时的粒子坐标
- 基线证据限制：没有可确认“已验收”的同机制 DrawGame 结构记录，验收状态为 `uncertain`
- 主要脚本：`DLevel4Ctrl.cs`、`DLevel4RockTrapHandle.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_004.prefab`

## 2. 脚本与组件树

```text
DLevel_004
├─ DrawLevelCtrl                         # 唯一主运行时控制器、结算与重建
├─ DrawLineFeature                       # 画线输入与有效笔画结果
├─ DLevel4Ctrl                           # 第一笔有效画线后释放已绑定的 10 个石头刚体
├─ Actor                                 # Player ActorCtrl
├─ Knock_Explosion                       # 既有、单个 ParticleSystem 实例
└─ 石头 x10
   ├─ PolygonCollider2D                  # 非 Trigger 碰撞形状
   ├─ Rigidbody2D                        # 初始 Kinematic，首笔后改为 Dynamic
   └─ DLevel4RockTrapHandle              # 本关 Collision2D 陷阱与特效接触点定位
```

共享 DrawLevel 控制器、共享角色和共享 `OnTriggerTrapHandle` 保持原职责；石头命中特效的
位置规则仍封装在 `Level4/` 组件中。2026-08-02 起，DLevel_016 因首笔释放与普通落石碰撞
行为完全一致，直接引用同一 DLevel4Ctrl 与 DLevel4RockTrapHandle。DLevel4Ctrl 运行时代码
未改；DLevel4RockTrapHandle 后续为 DLevel_016 的复合角色 Collider 修正了目标识别方式。

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| DrawLevelCtrl | 共享主控制器 | 初始化、局部 Handler 分发、失败、结算与整关重建 | `IDrawLevelRuntime` | DrawPlayCtrl | 不持有石头或特效坐标规则 |
| DrawLineFeature | 共享功能 | 生成有效线条与 `DrawStrokeResult` | MonoBehaviour | DLevel prefab | 不释放石头、不处理命中 |
| DLevel4Ctrl | DLevel_004 建立、当前由 DLevel_004/016 复用的局部控制器 | 第一笔有效画线后将已绑定刚体切为 Dynamic 并唤醒 | `IDrawStrokeCompletedHandler` | 各宿主 DLevel prefab | 不处理碰撞、特效、失败事件或存档 |
| DLevel4RockTrapHandle | DLevel_004 建立、当前由 DLevel_004/016 复用的局部组件 | 验证碰撞体属于宿主绑定的 Actor、用接触点播放已有特效，并复用失败事件链 | `IEventSender` | 随宿主危险碰撞物创建/销毁 | 不控制画线、石头释放、结算或共享陷阱行为 |
| OnTriggerTrapHandle | 共享陷阱组件 | 其它关卡的通用 Trigger/Collision2D 失败行为 | `IEventSender` | 其它 prefab | 不再承担 DLevel_004 石头特效坐标 |

## 4. 生命周期与关键调用

1. DLevel_004 创建时，10 个石头刚体保持 prefab 序列化的 Kinematic 初态。
2. `DrawLevelCtrl.InitializeRuntime()` 发现 DLevel4Ctrl；首笔有效画线结束后，
   DLevel4Ctrl 把 `boxRigidbodies`（历史字段名，当前绑定 10 个石头）逐项改为 Dynamic 并唤醒。
3. 石头与角色发生 `Collision2D` 时，DLevel4RockTrapHandle 从命中 Collider 的父级解析
   `ActorCtrl`，并要求它等于当前组件序列化绑定的 Player Actor；角色主体或同一 Actor 的
   子 Collider 都能识别，其他角色/物体不会进入该失败链。
4. 有接触数据时，处理器读取 `collision.GetContact(0).point`，保留 Knock_Explosion 原 Z 值后播放。
5. 随后保持原有顺序：`Actor.Die()`、向上击退、`trap` 音效、0.5 秒后派发既有
   `OnTriggerTrapEvent`，由 DrawLevelCtrl 走失败与整关重建。
6. `collision.contactCount` 为零时，特效以 Player Collider 对石头中心的 ClosestPoint 回退；
   不会伪造接触点。
7. Reset、Replay、失败和退出均销毁旧 DLevel 后重建，因此局部 `isDead` 与特效位置由新 prefab
   实例恢复，无额外持久状态。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| DLevel_004 根 | DLevel4Ctrl | 本关局部首笔释放控制器 | 必填 | 石头保持 Kinematic，不会随首笔释放 |
| DLevel4Ctrl | `boxRigidbodies` | 10 个石头的 Rigidbody2D | 必填 | 空引用被跳过，对应石头不释放 |
| 每个石头 | DLevel4RockTrapHandle | Player Actor、同一个 Knock_Explosion、`isCollision=true` | 必填 | 缺处理器则该石头不触发失败；缺粒子/Actor 属配置错误，需 Inspector 修复 |
| DLevel_004 根 | Knock_Explosion | 既有 ParticleSystem 实例 | 必填 | 命中无法显示粒子；本次不创建替代资源 |

当前 prefab 的 10 个本地处理器均保留原 `actor`、`knock` 和 `isCollision: 1` 序列化绑定，
仅替换脚本 GUID。

## 6. 数据、UI、资源与事件流

- DB/存档：无新增读写。
- Luban/配置：无新增字段或读取。
- UI：无直接交互；失败页继续由既有 DrawLevel 流程处理。
- 局部引用：每个石头只持有同关 Player Actor 与既有 Knock_Explosion。
- 事件：不新增事件；继续派发已有 `OnTriggerTrapEvent`。
- 资源加载：随 DLevel_004 prefab 加载，无新地址、异步加载或运行时实例化。
- Analytics/SDK：无新增或修改。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| DLevel_004 石头从共享 trap 改为本地 trap | 共享组件将粒子固定到角色根节点，且影响范围跨多关 | 特效贴合石头与角色接触处；死亡时序不变 | 不同方向碰撞、失败、重开 | 否，单关局部修复 |
| Collision2D 优先读 contact point | 真实物理接触点可直接由本次回调提供 | 保持粒子原 Z，仅更新 XY | 粒子层级与落点 | 否 |
| contact 缺失才使用 ClosestPoint | 物理回调异常时仍需安全显示，不以角色中心伪造落点 | 极少数无 contact 场景为近似点 | contactCount=0 | 候选，需多关证据 |

## 8. 可复用结论与优化候选

- 本关继续沿用的稳定结构：一个主 DrawLevelCtrl，单关碰撞表现由 prefab 局部组件承担。
- 本关验证的新结构：单关特效坐标需求可消费 `Collision2D` 接触点，不必修改共享陷阱。
- 2026-08-02 复用结论：DLevel_016 对首笔刚体释放与普通落石 contact point 失败链的需求
  完全一致，已直接绑定这两个组件；Player 与粒子引用仍由每个宿主 prefab 独立提供。
- 同日复合 Collider 结论：玩家身份以宿主明确绑定的 ActorCtrl 层级为准，不要求每个角色
  子 Collider 重复 Player Tag；这避免漏判，也不扩大共享 Tag 触发器的识别范围。
- 当前仍不迁移到共享陷阱或无编号新抽象：保留现有脚本 GUID/类名可避免改动已验证的
  DLevel_004；若更多关卡继续采用，再评估安全迁移。

## 9. 验证证据与缺口

- 静态检查：DLevel_004 的 10 个石头已绑定本地脚本；旧共享 trap 在本关的引用为 0；本地
  C# 以 `GetComponentInParent<ActorCtrl>() == actor` 限定宿主目标，并保留
  `contactCount > 0`、`GetContact(0).point` 与粒子 Z。
- 编译：复合 Collider 识别修改后，Unity Editor 日志记录
  `*** Tundra build success (4.00 seconds)` 与 Assembly-CSharp CopyFiles，未见本次脚本错误。
  命令行 .NET SDK 不可用，无法运行 `dotnet build`。
- Diff：新增 C#/meta 的 `git diff --check` 通过。DLevel_004 prefab 的既有大范围工作区修改包含
  Unity 空序列化字段行尾空白，本次没有清理这些无关内容。
- Unity PlayMode：未执行。
- Android/iOS：未执行。
- 未验证项：多石头同时下落下的不同接触方向、特效排序、失败后的重开与设备物理性能。

## 10. 风险与回滚

- 主要风险：若石头或 Player Collider 的接触形状与美术边界差异较大，物理接触点仍可能与视觉
  边缘略有差别；需 PlayMode 以实际碰撞形状确认。Actor 层级下新增的激活 Collider 会按复合
  玩家 Collider 处理，这是预期语义，但其几何范围仍需关卡验证。
- 回滚路径：若只回滚 DLevel_004，将 10 个石头恢复到共享 OnTriggerTrapHandle，但保留
  DLevel4RockTrapHandle 供 DLevel_016 使用；只有同时移除 DLevel_016 引用后才能删除脚本。
  共享 DrawLevel、角色、事件、资源和存档不需要回滚。
