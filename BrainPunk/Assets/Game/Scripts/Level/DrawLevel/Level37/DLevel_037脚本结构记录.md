# DLevel_037 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 37 关
- 记录日期：2026-08-03
- 当前方案：中心齿轮铰接平台、首笔物理释放、通用五秒成功倒计时、四向 FallArea
- 本关脚本：`DLevel37Ctrl.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_037.prefab`

## 2. 组件树

```text
DLevel_037
├─ DrawLevelCtrl                    # 共享 Draw 生命周期与结果防重
├─ DrawLineFeature                  # 多笔有质量画线
├─ DrawLevelCountdownCtrl           # 第一笔后倒计时 5 秒，时间到直接成功
├─ DelayAutoBeginDrawHandle         # 输入开放
├─ TrapEventHandlle                 # Actor 受击失败链
├─ DLevel37Ctrl                     # 只负责铰链物理、首笔释放和 Attempt Reset
├─ Actor (nested prefab)
│  └─ Rigidbody2D                   # Kinematic -> Dynamic；本关 friction 0.8
├─ pingtai
│  ├─ Sp_l93_cl_01 / Rigidbody2D    # Static 固定锚点
│  └─ platform_Stone
│     ├─ Rigidbody2D                # Kinematic -> Dynamic，mass 10
│     ├─ HingeJoint2D               # 不限制旋转角度
│     └─ BoxCollider2D
├─ FallArea_Left
├─ FallArea_Right
├─ FallArea_Bottom
└─ FallArea_Top
```

## 3. 职责边界

| 类/组件 | 负责 | 不负责 |
| --- | --- | --- |
| `DLevel37Ctrl` | 解析四个刚体/关节引用；缓存初态；配置物理；首笔释放；Attempt Reset | 不计时、不检查平衡、不提交成功、不派发 HUD |
| `DrawLevelCountdownCtrl` | 第一笔后启动 5 秒倒计时；暂停门禁；HUD；时间到提交成功；Reset 停止 | 不读取平台角度、角速度或接触状态 |
| `DrawLevelCtrl` | Stroke Handler 派发、结果锁、成功/失败公共流程 | 不知道本关铰链几何和配重状态 |
| `FailAreaTriggerHandle` | 四周 FallArea 识别 Player Actor 并派发既有失败事件 | 不处理画线出界，不自行实现 D037 结算 |
| Rigidbody2D + HingeJoint2D | 真实重力、碰撞和固定转轴 | 不脚本回正、不限制旋转角度 |

## 4. 关键序列化参数

| 宿主 | 字段/组件 | 当前值 |
| --- | --- | --- |
| DLevel_037 | `DrawLevelCountdownCtrl` | `cdTime=5`、`isSuccess=true`、绑定根 `DrawLevelCtrl` |
| DLevel37Ctrl | bindings | gear、platform、hinge、actor 四个引用 |
| Platform Rigidbody2D | mass/damping/gravity | `10 / 0.05 / 1.5 / 1` |
| HingeJoint2D | anchor / connected anchor | `(2.7022433,0)` / `(0,0)` |
| HingeJoint2D | limits / motor / collision | Off / Off / Off |
| Actor Rigidbody2D | mass / gravity / material | `3 / 4.5 / DrawStrokeStable`；friction 0.8 |
| ActorCtrl | `whatIsGround` | `24904`，局部包含 Layer 13 |
| DrawLineFeature | multiple / mass | 多笔；mass 30 按长度；minimum ratio 0.25 |
| 四向 FallArea | source | 共享 `Assets/Game/Prefabs/LevelItems/FallArea.prefab` |

## 5. 事件、数据与资源

- `DLevel37Ctrl` 与 `DrawLevelCountdownCtrl` 都实现 `IDrawStrokeCompletedHandler`，由根 `DrawLevelCtrl`
  对同一第一笔事件派发；两者互不调用。
- 倒计时和 HUD 完全复用 `DrawLevelCountdownCtrl` 的 `DrawGameCountdownChangedEvent` 链。
- FallArea 复用 `FailAreaTriggerHandle -> OnTriggerFailAreaEvent` 失败链。
- 不新增或修改 DB/Luban、存档、UI Prefab、全局事件类型、Physics2D Layer Matrix、Addressable、
  analytics、SDK 或平台配置。
- 不新增材质；Actor 复用现有 `DrawStrokeStable.physicsMaterial2D`。

## 6. 验证与剩余风险

- 独立 Roslyn 全量 `Assembly-CSharp` 编译 `exit code 0`；只有工程既有 warning 和编译器版本导致的
  Unity 6 SourceGenerator 加载 warning，没有 C# error。
- Unity PrefabImporter 已成功导入 DLevel_037；YAML 为 146 个唯一 anchor、0 重复、0 缺失本地引用。
- 静态计数：一个 `DrawLevelCountdownCtrl(cdTime=5,isSuccess=true)`、四个 FallArea shared instance、
  无 Actor gravity override；DLevel37Ctrl 无自定义成功提交代码。
- `uncertain`：正式 PlayMode 的第一笔同帧释放/计时、5 秒到点成功、四向 Actor 越界失败、暂停、
  Reset/Replay、同帧成功失败结果竞争和移动端表现。
- 回滚成功规则只需移除通用倒计时并明确替代规则；回滚四向边界需成组移除四个 nested instance；
  回滚物理机关则需成组恢复控制器、铰链和刚体 override。
