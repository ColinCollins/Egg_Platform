# DLevel_081 循环陷阱发射功能与风险评估

## 1. 原始行为基线

- `DLevel_081.prefab` 已包含三个按上、右、左摆放的 `L116ObstacleVisualTemplates`，每个节点下各有一个 inactive 的 `Sp_l116_jc_01_RedDownTemplate`。
- 三个 RedDown 模板原本各带 SpriteRenderer、PolygonCollider2D、Kinematic Rigidbody2D 和共享 `OnTriggerTrapHandle`，但关卡根节点没有本关控制器，也没有首笔后循环发射逻辑。
- 用户要求的对象池、轮内数量/间隔、速度、三个发射器顺序、轮间等待以及发射前闪烁均为本次新增行为。

## 2. 变更范围

- 新增：`Assets/Game/Scripts/Level/DrawLevel/Level81/DLevel81Ctrl.cs`
- 修改：`Assets/Game/Prefabs/DrawLevel/DLevel_081.prefab`，只新增根 Ctrl 组件与显式引用/默认参数。
- 文档：本风险评估、`DLevel_081脚本结构记录.md`、中央 Draw 演进记录、当日已有功能设计与使用记录。
- 未修改：`Packages/`、`Assets/GF/`、共享 Draw/Actor/ObjectPool/Trap 源码、Level116、DLevel5 和其它已验收关卡。

## 3. 行为前后

| 场景 | 变更前 | 变更后 |
|---|---|---|
| 首笔完成前 | 三个 RedDown inactive | 预警与发射均不推进 |
| 第一笔有效画线完成 | 无本关机关逻辑 | 从数组第 0 个发射器启动循环 |
| 每轮发射前 | 无预警 | 对应 RedDown 亮起两次，每个亮/灭阶段默认 `0.15s` |
| 每轮发射 | 不发射 | 默认 3 个，间隔 `0.2s`，速度 `10` |
| 发射器切换 | 无 | 最后一枚发出后默认等待 `1s`，按左侧→上方→右侧→左侧顺时针循环 |
| 与画线绳子接触 | 原模板具实体 Collider，未运行 | 运行时克隆为 Actor-only Trigger，排除绳子及其它层 |
| 命中 Actor | 原 inactive 模板不会命中 | 死亡/击退/音效/粒子后走共享 `OnTriggerTrapEvent` 失败链 |
| 暂停/重置/结算 | 无本关状态 | 暂停冻结本关时钟和位移；重置/结算回收并清场 |

## 4. 风险项

| 风险 | 等级 | 控制与当前证据 |
|---|---:|---|
| 陷阱误与绳子发生实体碰撞 | 高 | Collider 改 Trigger，LayerOverride 只包含 `Actor` 且排除补集；静态代码闭合，正式 Physics2D 运行仍待实测。 |
| 池条目复用后不再触发 | 高 | 不复用共享 `OnTriggerTrapHandle.isDead`；本关命中状态由 Ctrl 每次尝试重置，池条目仅持移动状态。 |
| 结算或重开后旧陷阱继续移动/晚到失败 | 高 | Reset、Resolved、Disable 均停止状态机并回收；异步失败用 attemptVersion 和 Destroy Token 校验。 |
| 对象池达到上限后漏发 | 中 | 默认预热 9、最大 18；默认速度/回收距离下静态估算足够，但极端 Inspector 参数仍可能耗尽并记录错误。 |
| 同时存在两个 DLevel81 实例导致池类型冲突 | 中 | 池按类型全局注册且 Ctrl 只注销自己拥有的池；检测到已注册会拒绝覆盖。标准 Draw 转场按旧关销毁后再建新关，非标准重叠加载仍 `uncertain`。 |
| 发射器方向理解错误 | 中 | 使用现有 RedDown 的局部下方向 `-root.up`；按数组顺序映射为左、上、右。Game 视图实际构图仍需目视验收。 |
| 闪烁观感过快或过慢 | 低 | 次数固定为 2；单阶段 `warningFlashInterval` 暴露在 Inspector，默认 `0.15s`。 |
| 模板激活后旧组件误执行 | 中 | 运行时只启用模板 GameObject 以显示预警，显式关闭共享 Trap Handle、Collider 和 Rigidbody2D.simulated；SpriteRenderer 由状态机控制。 |
| Pause 时物理系统仍运行 | 中 | 本关不依赖刚体线速度，所有位移由 Ctrl 在未暂停的 FixedUpdate 主动 `MovePosition`；暂停时不会调用 Advance。真实暂停帧边界仍待实测。 |

## 5. 数据、资源与事件流

- 数据：只使用 Prefab 序列化参数，不读写配置表、远端配置或存档。
- 资源：运行时从三个现有 RedDown 场景模板轮换克隆，未新增图片、材质或 Addressable 资源。
- 事件：只消费 DrawLevelCtrl 的本地首笔回调/Resolved 事件，并复用已有 `OnTriggerTrapEvent`；没有新增 `PlayEvents` 类型。
- 池：`ObjectPoolManager.RegisterPool<DLevel81RuntimeTrap>` → `Get` → 移动/命中/离场 → `Recycle` → 本关禁用时 `UnregisterPool`。

## 6. 验证与缺口

### 已验证

- Unity 主编辑器完成新增脚本的实际编译与域重载；最终源码再用相同 Unity Roslyn 和当前 `Assembly-CSharp.rsp` 全量外部编译，退出码 0 且无本关警告/错误。主编辑器尚未对最终的结算即时清场小改动再次刷新。
- Prefab YAML 的锚点唯一、关键引用存在，Ctrl 数量和三个发射器绑定数量正确。
- 源码静态复核覆盖需求默认值、固定两次闪烁、顺序循环、首笔门禁、对象池回收、暂停/结算/重置和 Actor-only 过滤。
- 2026-09-20 静态核对三个方向根的局部位置分别为左 `x=-0.76`、上 `y=1.53`、右 `x=0.76`，Prefab 数组已按左→上→右排列；本次未修改 C#、速度、数量或计时参数。
- 当前 Prefab 的 168 个 YAML 锚点唯一，所有非零本地引用均能解析；生产脚本 SHA-256 仍为 `92A00A4F649FF85A15ABFCF0019749DDA0BDD8265D3A77581D9C16ED47A8FFB0`，与本次修改前一致。

### `uncertain`

- 未在正式 DLevel81 PlayMode 中完成首笔后完整“左→上→右→左”目视与时间测量。
- 未用真实 DrawLine Collider 验证所有设备/帧率下完全无接触回调或视觉抖动。
- 未实测 Actor 命中、广告/原生覆盖、Reset/Replay、成功竞态、场景退出和移动端横屏性能。
- 当前用户地图本身仍为未跟踪 Prefab；本次只对新增 Ctrl 和其引用做了局部闭合检查，不能据此宣称整关已验收。

## 7. 回滚

- 最小回滚只移除 `DLevel_081.prefab` 根节点新增的 `DLevel81Ctrl` 组件和 `Level81/DLevel81Ctrl.cs(.meta)`。
- 不覆盖或删除用户已放入的三个发射器、地图、美术、Actor、DrawLine 和其它未提交内容。
- 若仅需调节难度，优先修改 Prefab 上的数量、轮内间隔、速度、轮间等待或闪烁阶段时间，不改状态机和共享系统。
