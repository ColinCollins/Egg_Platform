# DLevel_011 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 11 关
- 记录日期：2026-08-03
- 交付/验收状态：小偷延迟移动与 enemy 专用钥匙跟随已实现并通过编译、Prefab 导入及静态校验；
  完整 PlayMode、可解性和设备验收未完成
- 参考基线关卡：DLevel_001
- 选择基线理由：两关都通过 `IDrawStrokeCompletedHandler` 在第一笔完成后持续调用
  `ActorCtrl.SetMoveInput/OnUpdate` 驱动 Actor 向右；Level11 在此基础上增加局部延迟和钥匙分流
- 基线证据限制：DLevel_001 结构记录标明整关尚未验收，因此其验收状态为 `uncertain`；
  本关只采用已存在的局部 Handler、暂停/结算门禁与 Bear Logger 结构
- 主要脚本：`DLevel11Ctrl.cs`，并保留既有 `DLevel001Ctrl.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_011.prefab`

## 2. 脚本与组件树

```text
DLevel_011
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DLevel001Ctrl                         # 玩家首笔后向右移动
├─ DLevel11Ctrl                          # enemy 延迟移动、enemy/Key 局部状态
└─ Level11
   ├─ Actor（Player）
   │  └─ ActorCtrl
   ├─ Enmey（enemy）
   │  └─ ActorCtrl
   ├─ Key（嵌套 Key.prefab）
   │  ├─ OnTrigger2DHandle（原 Player Trigger，未修改）
   │  ├─ TriggerExecutor（原玩家取钥匙链）
   │  ├─ FollowTarget（Level11 实例新增，初始禁用）
   │  └─ OnTrigger2DHandle（Level11 实例新增，只过滤 enemy）
   ├─ DoorLock
   └─ DrawGame_SuccessDoor
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| `DrawLevelCtrl` | 共享主控制器 | 初始化、输入、Handler 分发、暂停与胜负锁定 | `IDrawLevelRuntime` | DrawPlayCtrl | 不持有 Level11 小偷或 Key 规则 |
| `DLevel001Ctrl` | 既有局部 Handler | 第一笔后持续驱动玩家向右 | `IDrawStrokeCompletedHandler`、`IDebuger` | DLevel Prefab | 不驱动 enemy，不处理 Key |
| `DLevel11Ctrl` | Level11 局部控制器 | 首笔后延迟驱动 enemy；处理 enemy 专用 Key Trigger；停止悬浮并启用跟随；Reset 恢复局部状态 | `IDrawStrokeCompletedHandler`、`IDebuger` | DLevel Prefab | 不处理玩家原取钥匙链、胜负、存档、UI 或资源加载 |
| Key 原 `OnTrigger2DHandle` | 共享 Prefab 组件 | 只接受 Player 并调用原 TriggerExecutor | MonoBehaviour | Key Prefab | 不处理 enemy |
| Key 新 `OnTrigger2DHandle` | Level11 Prefab override 组件 | 只接受 enemy 并调用 DLevel11Ctrl | MonoBehaviour | DLevel_011 Key 实例 | 不调用玩家 TriggerExecutor |
| `FollowTarget` | Level11 Key 实例组件 | enemy 命中后以 `(0,0.3)` 偏移、速度 10 跟随 | MonoBehaviour | DLevel_011 Key 实例 | 不判断触发者或结算 |

## 4. 生命周期与关键调用

1. DLevel_011 实例启用，`DLevel11Ctrl.OnEnable()` 解析同关 DrawLevelCtrl、enemy 与 Key，缓存
   Key 初始位置、目标和禁用状态，并订阅 `Resolved`。
2. DrawLevelCtrl 初始化时缓存 `DLevel001Ctrl` 与 `DLevel11Ctrl` 两个 Stroke Handler。
3. 第一笔有效线完成后，DrawLevelCtrl 同步通知两个 Handler：玩家立即开始向右，Level11
   enemy 启动 `enemyMoveDelay=0.5` 秒倒计时。
4. 非暂停且未结算时，DLevel11Ctrl 倒计时归零，随后每帧对 enemy 设置右移输入并调用
   `ActorCtrl.OnUpdate()`；enemy 的速度、碰撞、朝向与动画继续由 ActorCtrl 负责。
5. 玩家进入 Key 时，Key 原 Player Trigger 独立调用原 TriggerExecutor；DLevel11Ctrl 不参与。
6. enemy 进入同一 Key Collider 时，新增 enemy Trigger 调用
   `OnEnemyEnteredKeyTrigger()`；该方法校验碰撞者就是已绑定 enemy，停止 Key 悬浮，设置目标并
   启用 FollowTarget。
7. Pause/结算/死亡时清除 enemy 移动输入；恢复后倒计时或持续移动按本地状态继续。
8. `OnDrawAttemptReset()` 停止 enemy，恢复 Key 初始位置、旋转、目标、禁用状态和悬浮；整关
   Reset/Replay 的重建路径也从同一 Prefab 初态开始。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| DLevel_011 根 | `DLevel11Ctrl.drawLevelCtrl` | 同根 DrawLevelCtrl | 必填 | 运行时从父层级回退；仍缺失则不推进移动 |
| DLevel11Ctrl | `enemy` | Enmey 实例的 ActorCtrl | 必填 | 按同关 `enemy` Tag 回退；仍缺失用 Bear Logger 报错 |
| DLevel11Ctrl | `enemyMoveDelay` | `0.5` 秒 | 必填 | 旧序列化缺失时使用脚本默认 `0.5`；负值运行时按 0 |
| DLevel11Ctrl | `keyFollower` | Key 实例新增 FollowTarget | 必填 | 从同关子层级回退；仍缺失时报错且不取走 Key |
| Key 原 Trigger | `targetTag` / UnityEvent | `Player` / `TriggerExecutor.OnTriggerExecute` | 必填 | 玩家原取钥匙链失效；本次未改 |
| Key 新 Trigger | `targetTag` / UnityEvent | `enemy` / `DLevel11Ctrl.OnEnemyEnteredKeyTrigger` | 必填 | enemy 经过 Key 时不会启用跟随 |
| Key FollowTarget | target / offset / speed | enemy Transform / `(0,0.3)` / `10`，初始禁用 | 必填 | Key 不跟随或位置不符合预期 |
| enemy ActorCtrl | Actor Prefab 继承引用 | 继承 `spineCtrl`、`pushCollider`、`whatIsGround=328` | 必填 | 清空会导致 OnUpdate 空引用或无法正常步行动画 |

## 6. 数据、UI、资源与事件流

- DB/存档：本关脚本不访问；Draw 胜利后的既有进度链不变。
- Luban/配置：无新增或读取。
- UI：不直接访问 GamePlayPanel；暂停和结果只读取 DrawLevelCtrl 局部状态。
- 局部引用：根控制器显式绑定 enemy 和 Key FollowTarget；Key 新 Trigger 显式绑定 DLevel11Ctrl。
- 事件：DrawLevelCtrl 的接口分发、`Resolved` 本地 C# 事件、两个 Key 局部 UnityEvent；无全局事件。
- 资源加载：全部随 DLevel_011 与嵌套 Prefab 加载，无运行时实例化或地址加载。
- Analytics/SDK：无。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| enemy 移动增加可配置延迟 | 需求明确第一笔后等待 0.5 秒 | 玩家先移动，enemy 稍后追出 | 首笔、多笔、暂停、Reset | 本关必要差异 |
| 同一关有两个独立 Actor 行走 Handler | 玩家沿用 DLevel001，enemy 有独立时序 | 两个 Actor 可按各自节奏移动 | Handler 缓存、每帧输入、结算停止 | 局部组合，不提取共享抽象 |
| Key 额外挂 enemy Trigger，不改原 Player Trigger | 用户明确要求保护原 Trigger 内容 | 玩家取钥匙与小偷偷钥匙分流 | 两标签、两 UnityEvent、同帧竞争 | 局部稳定模式候选 |
| enemy 命中只停悬浮并启用 FollowTarget | 小偷应带 Key 离场，不能进入玩家开门/销毁链 | Key 保持可见并跟随小偷 | 命中首帧、偏移、离场 | 本关必要差异 |
| 恢复 Actor Prefab 继承引用 | 现有空 override 与 ActorCtrl.OnUpdate 契约冲突 | enemy 能使用既有移动与动画 | 引用、Ground、walk、碰撞 | 缺陷修复，不上提共享代码 |

## 8. 可复用结论与优化候选

- 继续沿用的稳定结构：DrawLineFeature/DrawLevelCtrl 产出本地笔画生命周期，单关控制器只消费
  自己的 Actor 和机关引用。
- 本关局部验证的新结构：同一 Collider 需要按不同 Tag 执行完全不同业务时，可以在关卡实例上
  额外挂过滤明确的 `OnTrigger2DHandle`，保护共享 Prefab 原 Trigger 内容。
- 暂不共享：`enemyMoveDelay`、enemy Tag、Key FollowTarget 偏移、悬浮停止和偷钥匙归属只保留
  Level11；目前只有一个关卡且没有完整 PlayMode 证据。
- 后续观察：若多个关卡都需要“附加 Trigger 保护共享原事件”，再评估形成 Prefab 使用清单；
  当前不修改 `OnTrigger2DHandle` 或 Key Prefab。

## 9. 验证证据与缺口

- 静态检查：DLevel11Ctrl、enemy、Key FollowTarget 与 enemy Trigger 引用完整；Key 原 Trigger
  在共享 Prefab 仍为 `Player -> OnTriggerExecute`，DLevel_011 对该组件没有 override。
- Prefab：Unity 最终规范化后的 140 个 YAML 锚点唯一，140 个非零本地引用全部解析；Unity Editor 最终导入成功。
- 编译：Unity 6000.0.59f2 `Assembly-CSharp.rsp` Roslyn 全量编译 `0 error`；warning 为项目既有项。
- 生命周期扫描：本关无 `OnValidate`、`Reset`、`ExecuteAlways`、DB、存档、全局事件或运行时实例化。
- Unity PlayMode：未执行。
- Android/iOS：未执行。
- 未验证：0.5 秒实际节奏、双 Actor 同时移动、enemy walk/碰撞、Key 跟随首帧、玩家原取钥匙、
  同帧竞争、完整成功/失败/Reset/Replay、关卡可解性和移动端 60 fps。

## 10. 风险与回滚

- 主要风险：两个 Trigger 同一物理步的回调顺序未验证；FollowTarget 与物理/悬浮切换的首帧视觉
  可能需要手感微调；enemy `moveSpeed=8` 与 0.5 秒节奏尚未经过整关验收。
- 回滚路径：只移除根 DLevel11Ctrl 和 Key 实例新增的 enemy Trigger，并删除 DLevel11Ctrl.cs；
  玩家 DLevel001Ctrl、Key 原 Trigger、DoorLock、成功门和共享 Prefab 均保持。
- 若回滚三个 enemy 引用修复，会重新引入已确认的 OnUpdate 空引用风险；应只在同时取消 enemy
  ActorCtrl 驱动时进行。

## 11. 2026-08-03 多 TipsObject 展示补充

- DLevel_011 当前包含两份既有 `DrawGame_TipsObjectSubObject`。根 `DrawLineFeature.tipsObject` 继续绑定
  原对象 `{fileID: 5552925418446422282}`；第二个对象只追加到 `additionalTipsObjects`，没有替换、
  清空或重复保存原绑定。
- 两份提示都由共享 `DrawLineFeature.ShowHint(bool)` 统一显隐；运行时初始化分别缓存各自
  `activeSelf`，Attempt Reset 分别恢复各自初态。DLevel11Ctrl、小偷移动、Key 双 Trigger、胜负和
  Reset 局部逻辑不参与提示对象控制。
- 序列化边界：主字段必填，用于保持既有关卡兼容；附加列表可为空。空引用或误配重复引用不会造成
  异常，但关卡制作时仍应只把新增对象放入附加列表。
- 静态验证确认 20 个现有 DLevel Prefab 的原主提示引用全部非空；仅 DLevel_011 有 1 个附加提示。
  DLevel_011 的 164 个 YAML 锚点唯一且非零本地引用完整，Assembly-CSharp 全量编译 `0 error`。
- 未执行 PlayMode；需手工确认两份提示同时显示/隐藏、清线 Reset 恢复各自初态、Replay 重建以及
  Android/iOS 横屏表现。回滚只需删除 DLevel_011 的附加列表项；原 tipsObject 不动。
