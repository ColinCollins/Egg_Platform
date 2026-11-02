# Level094 功能说明

## 2026-08-18 黄色范围脚本精简

- 背景：合并生命周期中的重复清理，保持停用、销毁、重置和声音/机关表现不变。
- 影响文件：仅 `Level94Ctrl.cs`。`OnDisable()` 与 `OnDestroy()` 共同调用私有 `CleanupRuntimeState()`；原有事件解绑和中断尝试清理顺序不变，`needsResetAfterEnable=true` 仍只在停用路径执行。
- 前后差异：从两份相同清理代码改为一个私有入口，没有修改玩法参数、协程/Tween 行为、资源、UI、事件来源或成功失败链。
- 验证：`Assembly-CSharp.csproj` 编译通过（0 错误），目标 diff 检查通过。
- 回滚：把私有清理体分别展开回两个生命周期回调即可。

更新时间：2026-08-11

## 需求背景

Level094 由 Level032 的关卡 Prefab 复制并替换为 L94 场景美术。本关的正解分为三个连续步骤：

1. 玩家在假地面盖板上向左或向右滑动，盖板沿玩家横滑方向移开并同步渐隐，形成揭开盖子的感觉并露出弹簧井。
2. 玩家站到红色弹簧按钮上并向下拖动。普通踩踏只产生一次压缩和上弹；弹簧伸展后保持到角色越过最高点，再平滑收回一次，落回原按钮不会自行重复。主动拖动会让按钮、弹簧和角色一起向下。
3. 松手时按压缩比例分流：压缩不足 `1/2` 时只把角色向侧上方弹飞；压缩达到 `1/2` 时，角色沿抛物线飞向月球，并在飞行最后 `1/3` 同时渐隐、缩小，随后直接结算成功。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_094.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_94/Level94Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_94/Level94Ctrl.cs.meta`
- `Assets/Game/Scripts/Level/MainLevel/Level_94/Level94功能说明.md`
- `Assets/Game/Scripts/Level/MainLevel/Level_94/Level94风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`（追加本关复用边界与验证结论）
- 复用但不修改：`BaseLevelCtrl`、`ActorCtrl`、`InputUtils`、DOTween、`Ground_mid.prefab`、共享 Actor Prefab、Level032 脚本与资源

本关不新增存档字段、配置字段、全局事件、运行时资源地址、analytics、广告、IAP、RemoteConfig 或平台 SDK 调用。现有配置已经包含 `tblevelsort[94] -> 10094` 和 `tbleveldata[10094] -> Level_094`，本轮不修改该映射。

## 原有行为基线

- `Level_094.prefab` 是 Level032 的复制版本，根节点保留 `BaseLevelCtrl`、`InfiniteScrollMap` 和 `Level32Ctrl`。
- 弹簧 Trigger 的 `OnTrigger2DHandle` 仍调用 `Level32Ctrl.TriggerSpring`，Actor 上的本地 `LayerCollisionDetector` 仍调用 `Level32Ctrl.StopSpringJump`。旧控制器还会创建 Level032 的 UI 代理碰撞并修改全局 `Time.fixedDeltaTime`，均不属于 Level094 玩法。
- 假盖板 `Sp_l94_dm_01` 默认关闭且没有 Collider；洞壁 `Sp_l94_dm_02` 默认可见，和“先揭盖再看到弹簧井”的要求相反。
- 弹簧按钮是 Dynamic `Rigidbody2D`，`SpringJoint2D` 默认关闭；弹簧图只有静态 Sprite，没有压缩表现。
- 两个 `Ground_mid` 实例的 Collider 相互重叠，右侧 Collider 完整覆盖弹簧洞口。若只显示洞口而不拆碰撞，角色仍会站在整块不可见地面上。
- 成功门实例虽然失活，`BaseLevelCtrl.successAnim` 仍引用该门；直接沿用门式成功链会播放不可见的进门流程和门音效。
- Prefab 的 Tips term 仍为 `S_LevelTips_Des_level_32`，GamePanel 仍为复制源的 `GamePlayPanel_006`；后者带有 Level032 专用的可拖 UI 与射线拦截层，会干扰本关世界手势。

## 实现后行为

### 盖板揭开

- 关卡以假盖板可见、弹簧井视觉受遮挡的状态开始。
- 只在 `PlayCtrl` 处于 `PLAYING`、时间正常流动、Actor 可控制且关卡未成功时接收本关世界输入；从 HUD/UI 开始的指针不会被盖板手势接管。
- 指针必须从假盖板自身 Collider 内起手；若该可选 Collider 未绑定，则回退到盖板 SpriteRenderer 的可见 bounds。水平位移需达到 `55px`，且绝对水平位移至少为绝对纵向位移的 `1.15` 倍；向左或向右都可触发，不要求固定方向。
- 合法滑动只提交一次。开始演出时立即关闭盖板自身 Collider 并执行兼容地面检查；当前 Prefab 已预拆地面，因此该检查不会再次改动 Ground。左滑时盖板向左、右滑时向右，在 `0.22s / Ease.OutQuad` 内水平移动 `1.4` 世界单位并同步将 Alpha 降到 `0`；完成后关闭盖板显示，露出洞壁、按钮和弹簧。
- 位移和渐隐由同一个 Sequence 驱动，暂停时一起冻结，恢复后从相同进度继续。触点取消或应用失焦只取消当前未提交手势，不会误揭盖；对象禁用或销毁会清理本关 Tween。每次尝试会从缓存的盖板局部原位和不透明颜色开始，Reset/Replay 不会保留上次滑出的偏移。

### 地面、按钮与弹簧

- `Level_094.prefab` 通过两个 `Ground_mid` 实例 override 预先拆成“左地面 - 弹簧洞口 - 右地面”，不修改共享 `Ground_mid.prefab`。左段 Collider 的 `size.x / offset.x` 为 `17.170145 / 8.024928`，右段为 `3.676153 / 10.568078`，世界洞口约为 `[-2.00, 0.24]`。
- `floorCollider` 在当前 Prefab 留空。控制器保留按按钮 Collider 加单侧 `0.04` 留白的运行时拆分兼容路径，但预拆地面没有覆盖弹簧中心的候选 Collider，因此当前关卡不会创建 `Level094_RightGround`。
- 揭盖前由盖板本地 Collider 承载洞口；开始揭盖时立即关闭该 Collider，角色才能落到按钮上。手势命中在 Collider 缺失时可回退 Sprite bounds，但该回退只负责输入，不提供物理承载。
- 弹簧井内不增加常驻实体挡板。井宽只比角色略宽，深处平台会把异常下落转成无法离开的软锁；当前防穿模由主动输入接管解决：合法按下红按钮的当帧即冻结 Actor 刚体，并持续同步角色 Transform 与 Rigidbody2D 位置。
- 本关控制器确定性接管按钮位置，不依赖复制源中默认关闭的 `SpringJoint2D`，避免拆开地面后 Dynamic 按钮直接坠落。
- 弹簧图以底端为锚点随按钮高度压缩和回弹。`springButton` 明确绑定使用 `Sp_button_b_1` 素材的红色按钮，按钮 Collider 始终随红色按钮移动；外侧蓝白圆环是另一个同级视觉节点，压缩和回弹时保持固定。普通踩踏发射后的伸展/收回阶段会临时忽略 Boxy 主 Collider 与红按钮 Collider 这一对碰撞，避免移动中的 Kinematic 按钮连续追顶动态角色；其它 Collider、Trigger 和主动下拉碰撞不受影响。
- `Spring` 根节点上的固定 `1.7 x 0.8` Trigger 只负责可靠记录 Actor 走入/走出并调用 `OnActorEnterSpring/OnActorExitSpring`；真正排队和拖动仍要通过红按钮站立、水平覆盖和玩法状态门禁，Trigger 不替代实际按压面。
- 盖板尚未揭开时，Actor 走到中央承载区域即可触发一次小幅压缩和上弹；揭盖后从盖板落到按钮，或从左右地面走入按钮时也能触发。本次上弹落回同一位置不会再次触发。只有 Actor 已落地、竖直速度接近静止，并且碰撞体在水平方向完全离开按钮范围至少 `0.05` 世界单位后，下一次踩入才会重新触发。
- 站立检测在 Actor 仍为接地状态时接受物理接触求解产生的微小向上修正速度；只有已经离地时才继续使用 `linearVelocity.y <= 0.5` 的方向门禁。这样不会因按钮接触帧在 `0` 与约 `+0.88` 间波动而反复取消 `0.03s` 弹跳等待，也不会把真正向上离开的 Actor 继续当作站立。
- 被动小幅压缩完成、准备赋予上弹速度时只复核 Actor 在水平方向仍覆盖按钮；不会再用已经下移的按钮顶面重复判断垂直站立，避免 Tween `Update` 与角色物理 `FixedUpdate` 的短暂错位吞掉正常弹跳。若 Actor 在约 `0.10s` 的压缩期间已经完全走离，只回弹按钮和弹簧，不再隔空改变 Actor 速度。
- 普通踩踏下压后先确认 Actor 仍水平覆盖按钮，再临时隔离 Actor/红按钮碰撞并给 Actor 上弹速度。红色按钮和弹簧用 `0.16s` 伸展到 `-0.405` 比例（相对休止位上探约 `1.701` 世界单位，是上一版 `0.27` 的 `1.5` 倍）。伸展完成后不立即做往返；保持到 Actor 已离地且竖直速度降到 `0` 或以下，再用 `0.16s / Ease.OutQuad` 单次收回休止位。回到休止位并执行 `Physics2D.SyncTransforms()` 后恢复这一对 Collider 原有的 ignore 状态。
- 发射未建立时不会开启碰撞隔离；Actor 死亡、外部成功、Tween 被接管、重开、禁用或销毁时都会先复位按钮，再恢复缓存的碰撞状态，避免 Boxy 后续穿过按钮或弹簧永久停在展开状态。
- 普通上弹速度为 `10`。按共享 Actor 的 `gravityScale=4.5` 和项目重力 `-9.81` 静态估算，理论抬升约 `1.13` 世界单位，用于“踩上后只轻弹一点”的反馈；最终画面高度仍以 PlayMode 和策划观感为准。
- 普通踩踏小弹只播放现有 `spring`；主动松手后的欠压侧抛和月球发射在 `spring` 后各叠加一次 Level093 的 `l93PlayerLaunch`，复用已登记的 `抛物线.ogg`，不新增或修改共享音频资源。
- 本关面板绑定改为普通关卡复用的 `GamePlayPanel_000`，保留标准移动/跳跃操作，不加载 Level032 的 `DragBlock/UIDrag`，也不修改任何共享面板 Prefab。

### 主动下拉与释放分流

- 只有盖板已经揭开、Actor 正站在按钮上且没有处于其它演出时，才能从红色按钮建立下拉候选。输入热区接受按钮物理 Collider 或完整 SpriteRenderer 可见 bounds，物理 Collider 尺寸保持不变，避免为了扩大点击区而抬高 Boxy。合法按下当帧就取消尚未发射的普通弹跳、关闭 Actor 刚体模拟并开始携带角色，消除按下到起拖之间从按钮边缘穿入洞口的物理空档；继续向屏幕下方移动至少 `14px` 后才开始改变压缩比例。未达阈值直接松手会同步物理位置并恢复 Actor，不会压缩、侧抛或成功发射。
- 每段鼠标/触摸手势只认领最初命中的指针；后续移动、松手和取消都只跟踪该指针。
- 下拉时按钮只沿 Y 轴移动并限制在休止位到最大压缩位之间。Actor 暂停自身物理和移动输入，以开始拖动时相对按钮的偏移同步下移，不通过改父节点制造缩放或层级副作用。
- 弹簧压缩比例按按钮从休止位到最大压缩位的实际行程计算，并限制在 `0 ~ 1`。
- 松手时压缩比例 `< 0.5`：按钮和弹簧在 `0.24s` 内回弹，Actor 恢复物理并沿当前朝向获得侧上方速度。满力度基准为 `(8.5, 19)`，实际力度按压缩量在 `65% ~ 100%` 间插值。该路线不能到达月球；Actor 离地后再次落地才恢复常规操作，`2.8s` 按有效游戏时间累计，超时仅用于避免软锁。
- 松手时压缩比例 `>= 0.5`：提交成功发射。Actor 继续保持非物理、不可操作，在 `2.45s` 内沿弧高 `18` 的显式抛物线飞向月球目标。
- 成功飞行进度 `< 0.67` 时保持原可见尺寸；从进度 `0.67` 开始，Spine 与 Sprite 子显示同步渐隐，Actor 根缩放同步减小到起飞尺寸的 `12%`。到达目标后清空本关不可见的门成功动画引用并调用 `BaseLevelCtrl.SuccessStrightly()`，保留统一星星与结算流程，但不播放进门和关门音效。
- 成功一旦提交，不再接受盖板、按钮、普通弹跳或第二次发射输入。

## 状态与触发边界

| 阶段 | 可接受输入 | 离开条件 |
| --- | --- | --- |
| 假盖板关闭 | Actor 移动、中央区域普通踩踏、盖板上的单指水平滑动 | 普通踩踏伸展并保持到 Actor 最高点后单次收回；合法左右滑动进入盖板渐隐 |
| 盖板渐隐 | 不排队新的普通弹跳，不允许按钮下拉 | 渐隐完成，进入弹簧可用状态 |
| 弹簧可用 | Actor 移动、正常踩踏、按钮下拉起手 | 正常踩踏进入一次伸展保持与收回；合法下拉进入主动压缩 |
| 下拉候选 | Actor 已被按钮接管；当前指针继续向下移动 | `< 14px` 松手恢复 Actor 且不发射；达到 `14px` 进入主动压缩 |
| 主动压缩 | 当前指针的纵向拖动与松手/取消 | `< 0.5` 普通抛射；`>= 0.5` 成功发射 |
| 普通抛射 | 不接受新的弹簧拖动 | Actor 完成离地后落地，恢复弹簧可用状态 |
| 成功发射 | 不接受关卡输入 | 到达月球并完成最后 `1/3` 渐隐缩小后结算 |

暂停或退出不会把进行中的取消操作当成一次松手发射。只有正常释放才根据当前压缩比例选择路线。暂停中的主动拖拽会取消且不发射，并将 Actor 刚体保持冻结到恢复；已经提交的盖板渐隐、按钮回弹、普通/欠压弹射和月球飞行保留状态，恢复后继续。

## 当前参数

| 项目 | Prefab/脚本默认值 |
| --- | --- |
| 盖板横滑距离 / 横向优势 | `55px / 1.15` |
| 盖板位移渐隐 | `1.4 世界单位 / 0.22s / Ease.OutQuad` |
| 按钮下拉激活距离 | `14px`（仅向屏幕下方） |
| 最大压缩行程 / 成功阈值 | `4.2 / 0.5` |
| 满压弹簧纵向比例 / 按钮回位 | `0.12 / 0.24s` |
| 站立高度容差 | `0.42` |
| 普通踩踏等待 / 下压比例 / 伸展上探比例 | `0.03s / 0.08 / 0.405` |
| 普通踩踏下压 / 伸展或收回 / 冷却 | `0.10s / 0.16s / 0.12s` |
| 普通踩踏上弹速度 | `10` |
| 主动松手回弹上探比例 | `0.18` |
| 欠压侧抛基准速度 / 最小力度 | `(8.5, 19) / 0.65` |
| 欠压控制恢复超时 | `2.8s` |
| 月球飞行时长 / 弧高 | `2.45s / 18` |
| 渐隐缩小起点 / 最终尺寸 | `0.67 / 0.12` |
| 洞口单侧额外留白 | `0.04` |
| 运行时拆地候选顶面容差 | `0.35`（仅兼容回退使用） |

## Prefab 绑定

| 序列化字段 | Level094 本地对象 | 复制基线 fileID |
| --- | --- | --- |
| `levelCtrl` | 根 `BaseLevelCtrl` | `5816475025737335787` |
| `actor` | ActorCtrl / Actor Transform | `1716601560298323527` / `6894532788108621195` |
| `inputCamera` | 本关 GameCamera | Prefab 实际 Camera 组件 |
| `fakeGroundCover` | `Sp_l94_dm_01` SpriteRenderer | `1340557145843795359` |
| `fakeGroundCoverCollider` | 盖板本地承载/命中 BoxCollider2D | Level094 新增 `909400000000000001` |
| `revealedGround` | `Sp_l94_dm_02` GameObject | `3667153829696374805` |
| `springRoot` | `Spring` Transform | `8789455207087270077` |
| `springButton` / `springButtonCollider` / `springButtonBody` | Button Transform / Collider / Rigidbody2D | `8996417160726456443` / `6958462072942360617` / `2427275817629367576` |
| `legacySpringJoint` | 按钮原 SpringJoint2D，仅用于强制保持关闭 | `927133546072469197` |
| `springVisual` | Spring Sprite Transform | `2854823972229795869` |
| 固定蓝白圆环 | Spring 下另一个同名 `Sp_button_1_02`，不参与移动 | `907380547745308691` |
| 固定进入范围 | Spring 根 `BoxCollider2D` / `OnTrigger2DHandle` | `4891562134498156914` / `5329234296958643046` |
| `moonTarget` | `Sp_dmoon_1_01` Transform | `4229712891943047532` |
| `floorCollider` | 运行时拆地兼容字段 | 当前 Prefab 留空；两段地面由 target `9182361971724876853` 的实例 override 预拆 |

Prefab 已将根控制器从 `Level32Ctrl` 改为 `Level94Ctrl`，移除 Actor 上的 Level032 本地回调，并把 Spring Trigger 的 Enter/Exit 改绑当前 `Level94Ctrl`。`TriggerSpring` 只保留为复制资源兼容入口；当前普通弹跳由固定 Trigger 与按钮站立 bounds 共同保护。共享 Actor、`Ground_mid.prefab`、Level032 控制器和其它关卡 Prefab 不做修改。

`BaseLevelCtrl.gamePlayPanelName` 已从复制源的 `GamePlayPanel_006` 改为 `GamePlayPanel_000`。该修改只存在于 `Level_094.prefab`，不会改写两套共享 GamePanel 资源。

## 数据、资源与事件流

```text
盖板上的水平滑动
  -> Collider/Sprite bounds 命中 + 排除 UI
  -> 记录 delta.x 的左右方向
  -> 假盖板同向移动 1.4 并同步渐隐 + 关闭盖板承载 Collider
  -> 弹簧进入可用状态

Actor 在未揭盖中央承载区，或揭盖后落到按钮
  -> 小幅自动压缩
  -> 临时忽略 Actor 主 Collider 与红按钮 Collider 的碰撞
  -> Actor 获得向上速度 10
  -> 按钮/弹簧伸展到 -0.405 并保持
  -> Actor 越过最高点后，按钮/弹簧单次平滑收回
  -> 回到休止位后恢复原碰撞状态
  -> 落回同一按钮不重复
  -> 落地并水平走离按钮后重新武装下一次踩踏

Actor 站在按钮上并向下拖动
  -> 合法按下当帧暂停 Actor 物理与输入并开始携带
  -> 向下达到 14px 后才开始改变压缩比例
  -> 按钮受限下移 + Actor 同步下移 + 弹簧底端固定压缩
  -> 正常松手
      -> 压缩 < 0.5：侧上方物理抛射 -> 落地恢复
      -> 压缩 >= 0.5：显式抛物线飞向月球
          -> 最后 1/3 渐隐 + 缩小
          -> SuccessStrightly
```

所有美术、角色、地面 Collider 和目标对象均由 Prefab 预放并序列化绑定。当前预拆地面不走运行时右地面创建路径；本关不增加运行时资源加载、存档或配置写入。

控制器复用既有 `SwitchGameStateEvent`、`GamePauseEvent`、`GameResumeEvent`，在启用时订阅并用 `PlayCtrl` 当前状态初始化，在禁用或销毁时退订。`GameResumeEvent` 派发结束后的下一帧会再次读取 FSM 的实际 `PLAYING` 状态，消除订阅顺序差异。暂停显式 `Pause` 盖板、按钮和月球三个 Tween；正在物理飞行的普通弹跳或欠压侧抛会缓存速度并暂时关闭 `Rigidbody2D.simulated`，恢复后一帧再还原模拟与速度，避免与 `BaseLevelCtrl` 的暂停/恢复回调争抢 Actor 状态。未新增全局事件类型。

## 验证记录

- 已静态确认 Level094 的复制来源、关键节点 fileID、L94 盖板/洞壁 Sprite GUID、按钮 Rigidbody2D/Collider/Joint 状态、月球目标和旧 Level032 回调。
- 已静态确认复制基线中的两个完整地面 Collider 会覆盖洞口，因此最终拆分必须限制在 Level094 实例范围，不能修改共享 `Ground_mid.prefab`。
- 已确认正式配置存在 `94 -> 10094 -> Level_094`，本轮无需改 LevelData/LevelSort。
- 当前 Prefab 已用实例 override 将两块 Ground Collider 预拆为约 `[-19.170, -2.00]` 与 `[0.24, 3.916]`，共享 `Ground_mid.prefab` 未修改。
- 使用 Unity 6000.0.59f2 自带 Roslyn 和当前 `Assembly-CSharp.rsp` 对最终脚本执行完整编译，结果为 `0 error / 104` 条工程既有 warning。
- 本轮修复前的关卡基线曾由当前 Unity Editor 完成 Asset Pipeline Refresh、`MonoManager ReloadAssembly` 与 `Level_094.prefab` 导入，日志未出现 C# 或 Prefab 导入失败标记。
- 已在 Unity 6000.0.59f2 PlayMode 从主界面进入 Level094，实际横滑揭开盖板并将 Actor 从 `x=-7.54` 移到按钮 `x=-0.90` 的覆盖范围。修复前探针确认 Actor 接地时速度在 `0` 与约 `+0.88` 间波动，`passiveBouncePending` 每帧被取消，无法进入压缩播放。
- 控制器新增 `0.35` 顶面高度容差；即使 `floorCollider` 留空，运行时兼容查找也不会误拆与按钮表面高度不相邻的其它 Ground。
- 最终 Prefab YAML 共 `186` 个对象定义，重复 fileID 为 `0`，缺失本地非零引用为 `0`；Level94 脚本 GUID 为 `1` 次、Level32 脚本 GUID 为 `0` 次，`TriggerSpring / StopSpringJump` 旧持久回调均为 `0` 次。
- 已静态复审暂停竞态：主动拖拽取消会延迟恢复刚体，欠压弹射在恢复协程完成前不会累计超时或判断落地。
- 已确认 `gamePlayPanelName` 为 `GamePlayPanel_000`，Level032 专用 `GamePlayPanel_006` 不再由本关加载。
- 已静态确认中央按钮输入不受本关 GameCamera 的 Physics2DRaycaster 或 `GamePlayPanel_000` 常驻全屏射线阻挡；旧拖拽失败来自循环自动弹跳造成的极短站立窗口，以及按钮 Sprite 高 `0.9`、物理 Collider 高约 `0.547` 的输入缺口。
- 已静态确认尝试初始化会武装首次普通弹跳；盖板尚未揭开时允许中央区域普通踩踏，仅盖板正在渐隐时暂时拒绝新的排队，避免碰撞切换期间误触发。真正上弹前只复核 Actor 与按钮的水平重叠，不再读取被 Tween 下移后的按钮顶面；完全离开按钮时不会获得弹射速度。
- 修改站立方向门禁后，同一 PlayMode 实例记录到 `scheduled -> play-passive -> launch-entry -> launch-applied` 完整链路；当前实际赋予 Actor 的刚体速度为 `(0, 10)`，角色离地后落回按钮，`passiveBounceArmed` 保持 `false`，观察期间没有原地再次发射。
- 使用独立 Unity 6000.0.59f2 Android 目标 `-batchmode -nographics` 临时工程加载正式 Loading/Main/Level094 流程，运行时探针结果为 `PASS checks=29`。验证覆盖首次自动武装、真实 Physics2D Trigger Enter/Exit、首次小弹、落回不连弹、水平走离后重新武装与再次触发、盖板滑动阈值/方向、主动压缩随动、取消不误发射、`< 0.67` 侧抛、`>= 0.67` 月球路线、最后 `1/3` 缩小和一次成功结算。
- 上述 `PASS checks=29` 早于普通踩踏上探比例 `0.08 -> 0.18 -> 0.27 -> 0.405`、最高点保持和碰撞隔离，只能继续证明触发、门禁和主动路线基线；不能作为当前伸展高度、无抖动表现或最终观感的动态证据。当前已静态复核最高点收回只启动一次，且死亡、外部成功、中断、重开及主动输入接管路径均会复位按钮并恢复碰撞。
- 本轮调整后再次使用 Unity 6000.0.59f2 自带 Roslyn 和当前 `Assembly-CSharp.rsp` 后台完整编译，结果为 `0 error / 104` 条工程既有 warning，`Level94Ctrl.cs` 自身无诊断；该命令未启动或操作 Unity Editor。
- 旧运行探针中的盖板断言对应“达到阈值后原地渐隐”的版本，不覆盖本轮“按 `delta.x` 同向移动 `1.4` 并同步渐隐”的演出。当前仅静态确认左右方向映射、同一 Sequence 的位移/Alpha 并行、暂停继续和 PrepareAttempt 原位恢复；最终揭盖手感仍需前台验收。
- 方向性揭盖调整后，使用 Unity 6000.0.59f2 自带 Roslyn 与当前 `Assembly-CSharp.rsp` 后台完整编译为 `0 error / 104` 条工程既有 warning，Level094 无诊断。Prefab 复扫为 `186` 个唯一对象定义、`0` 重复 fileID、`0` 悬空本地引用，`coverSlideDistance: 1.4` 仅序列化一次；全程未启动、聚焦或操作前台 Unity。
- 普通回弹抖动修复与 `passiveExtensionRatio=0.27` 调整后再次后台完整编译，结果仍为 `0 error / 104` 条工程既有 warning，Level094 无诊断。静态确认碰撞隔离只在成功建立普通发射后开启；正常收回、死亡、外部成功、Kill、Prepare 和 Cleanup 都有恢复路径，主动下拉参数与 `launchExtensionRatio=0.18` 未改。
- 后台验证全程没有向操作系统发送鼠标/键盘输入，也没有操作或聚焦用户正在运行的 Unity。探针通过后调用 `EditorApplication.Exit(0)`；其后退出清理阶段的全局 DB/PlayCtrl 空引用发生在 PASS 之后，不属于 Level094 运行失败。
- 临时探针及其 `.meta` 已在验证完成后移除。正式 Level094 脚本与 Prefab 未为探针增加运行时代码或序列化依赖。

## 2026-08-11：Level094 普通踩踏再加高与重复抖动修复

- 针对“踩到弹簧后抖动几下”和“当前伸展仍不够”的反馈，普通踩踏伸展比例在 `0.27` 基础上再乘 `1.5`，当前 `passiveExtensionRatio=0.405`，相对休止位上探约 `1.701` 世界单位；普通上弹速度仍为 `10`，主动下拉 `launchExtensionRatio=0.18` 与成功阈值不变。
- 被动发射新增明确的碰撞隔离状态门禁：Actor 主 Collider 与红按钮 Collider 被忽略期间，不结束被动发射、不重武装、不接受新的交互。按钮必须先归零并执行 `Physics2D.SyncTransforms()`，再恢复这对 Collider 原有的 ignore 状态；按钮 Tween 被取消、死亡、外部成功、Prepare、Cleanup、Disable 和 Destroy 均遵循相同顺序。
- `Level_094.prefab` 仅清除红按钮 Rigidbody2D 对共享 `Bouncy.physicsMaterial2D` 的实例引用，未修改共享材质、Actor、Layer Matrix、其它关卡或主动路线，避免恢复碰撞后的物理弹性造成递减小跳。
- 本轮后台 Roslyn 完整编译结果为 `0 error / 104` 条工程既有 warning，Level094 无诊断；Prefab 静态检查仍为 `186` 个唯一定义、`0` 重复 fileID、`0` 悬空本地引用。当前无抖动、`0.405` 高度和落地观感仍需前台 PlayMode 验收。

## 手工验收清单

- [ ] 进入关卡时只看到完整假地面；按钮、弹簧井不会穿透盖板，Actor 也不会穿过洞口。
- [ ] 从盖板内向左、向右滑动都只揭开一次；确认盖板分别向左、向右移动并同步消失，方向与手势一致且有揭盖感。盖板外起手、轻点、竖滑、从 UI 起手和多指干扰不会误触发。
- [ ] 盖板渐隐后碰撞同步移除，地面只保留左右两段，洞口没有 invisible floor、缝隙卡脚或边缘漏穿。
- [ ] 盖板未揭开时走到中央、揭盖后直接落到按钮、以及从左右地面走入按钮时，都只压缩并弹起一次；确认 `0.405` 伸展高度符合截图，Boxy 上升期间不再被按钮连续追顶抖动，保持到最高点后只收回一次，且落地能正常站回按钮。原地落回不重复，走离按钮并重新踩入后才允许下一次触发。
- [ ] 普通弹跳开始压缩后立即横向走离按钮，确认按钮自行回位且 Actor 不会被隔空弹起；再次完整踩入后仍可正常触发。
- [ ] 红色按钮包含物理 Collider 外的上方可见区域都能建立下拉候选；扩大输入热区不会改变 Boxy 的实际站立高度。
- [ ] Actor 不在按钮上时不能隔空拖；合法下拉时 Actor、按钮和弹簧同步，弹簧底端不漂移。
- [ ] 按钮按下当帧角色即停止自主物理并稳定跟随；向下移动不足 `14px` 再松手不会发射，达到 `14px` 后才开始压缩。
- [ ] 在 `0.5` 阈值前后分别松手，确认普通侧抛与月球成功路线稳定分流，边界不会一帧反复切换。
- [ ] 普通侧抛不能到月球，落地后及时恢复移动、跳跃与再次使用弹簧。
- [ ] 欠压侧抛和成功飞月球起飞时都只播放一次 `spring + l93PlayerLaunch`；普通踩踏小弹不播放夸张的甩飞声，暂停/重开不会补播。
- [ ] 成功飞行轨迹不碰场景 Collider，最后 `1/3` 才渐隐缩小，到达月球后只结算一次且没有门式演出/音效。
- [ ] 在揭盖、自动弹跳、下拉、两类发射和成功飞行期间分别测试暂停、恢复、切后台、重开和退出。
- [ ] Android/iOS 横屏分别验证触摸 owner、多指、不同宽高比、60 fps 和相机对月球轨迹的构图。

## 未验证缺口

- 后台运行探针已验证 `y=10` 小弹、落回不连弹、水平走离后重新武装、主动压缩同步、取消不误发射、两条释放分流、月球飞行最后 `1/3` 缩小及成功结算；压缩期间快速走离的低概率时序边界未单独施压验证。
- 探针直接调用本关输入处理边界以避免抢占操作系统输入，因此完整红色按钮屏幕热区、真实鼠标/触摸起手、`14px` 拖动手感、当前 `0.5` 临界手感、侧抛力度、月球构图和音效仍需策划在前台手工验收。探针确认了调整前的普通下压和 Actor 上弹，但没有验证当前 `0.405` 伸展高度、最高点保持/收回、碰撞隔离后的无抖动表现或落地重新碰撞，其视觉和物理观感仍为手工项。
- 尚未动态验证盖板 `1.4 / 0.22s` 位移渐隐观感、暂停中冻结后的轨迹连续性、失败重开/Replay 原位恢复、成功途中退出，以及 Android/iOS 横屏单指、多指取消、切后台、不同安全区和代表性设备性能。
- Prefab 仍沿用 `S_LevelTips_Des_level_32`；是否需要 Level094 独立提示文案由策划另行确认。

## 回滚路径

- 从 `Level_094.prefab` 移除 `Level94Ctrl` 及其本地接线，恢复复制时的 `Level32Ctrl`、旧 Trigger 回调、地面 Collider、盖板/洞壁初始显隐和按钮 Rigidbody2D 状态。
- 删除 `Level94Ctrl.cs/.meta` 与本目录两份文档即可撤回本关私有实现。
- `Ground_mid.prefab`、共享 Actor、`BaseLevelCtrl`、Level032、配置和 L94 美术均未修改，无需回滚。
- 若只回滚本轮普通踩踏展示调整，将 `passiveExtensionRatio` 恢复为 `0.08`，并恢复旧的“压缩后立即伸展、随后直接回休止位”Tween 序列；Actor 的 `y=10` 上弹速度和主动下拉两条路线无需改动。
- 若只回滚盖板揭开演出，移除 `coverSlideDistance`、盖板原位缓存及方向参数，把 Reveal Sequence 恢复为原地 Alpha Tween；手势阈值、碰撞关闭和地面拆分无需修改。
- 若只回滚本轮抖动和高度调整，将 `passiveExtensionRatio` 恢复为 `0.18`，移除普通发射阶段的 Collider ignore 缓存/恢复调用；主动松手的 `launchExtensionRatio=0.18`、Actor `y=10`、盖板和其它玩法无需回滚。

## 2026-08-11：恢复上一版弹簧表现

- 根据“恢复前一版本”的反馈，本轮同时撤销最近的“欠压小力且停在 `-0.18`”与“普通踩踏后永久保持 `-0.405`”两项尝试；相关后续方案不再代表当前运行行为。
- 欠压 `< 0.67` 恢复为 `(8.5, 19) / 0.65` 侧抛，并恢复按钮从当前位置经过 `-0.18` 到休止位的完整回位 Tween；主动拖动下界恢复为 `0`。
- 普通踩踏继续使用 `y=10` 和 `passiveExtensionRatio=0.405`，伸展后保持到 Actor 越过最高点，再单次平滑收回；恢复 `passiveReturnStarted` 及 Actor/红按钮 Collider 的本关局部隔离与归零后恢复流程。
- 继续保留盖板同向滑出渐隐、地面拆分、红按钮移除 Bouncy 材质、主动下拉 `4.2 / 0.67`、月球轨迹和最后 `1/3` 渐隐缩小；未修改共享 Actor、Layer Matrix、Level032、GamePanel、配置、存档、事件或其它关卡。
- `Level94Ctrl.cs` 与 `Level_094.prefab` 已分别核对为目标快照 `5d3b63436e869f55521e5c10f33ceecfe432bf16`、`1d9132482b591a4e54721f32e04a9257f6197478`。Unity 6000.0.59f2 自带 Roslyn 后台完整编译为 `0 error / 104` 条工程既有 warning；Prefab 静态检查为 `186` 个唯一定义、`0` 重复 fileID、`0` 悬空本地引用，Level94/Level32 GUID 次数为 `1/0`，旧回调为 `0`。
- 本轮未启动、聚焦或操作前台 Unity。恢复后的欠压力度、最高点收回时机、无抖动和移动端触摸手感仍为 `uncertain`，需后续前台 PlayMode/真机验收。

## 2026-08-11：过关半程判定与主动拖拽防穿模

- 当前成功压缩阈值由 `0.67` 放宽为 `0.5`：松手快照 `< 0.5` 走欠压侧抛，`>= 0.5` 走月球成功；成功飞行的渐隐缩小起点仍为进度 `0.67`，两者是独立参数。
- 合法按下红按钮时立即 `TakeActorOwnership()`，不再等到累计向下 `14px` 后才关闭 Actor 物理；`14px` 只决定是否开始改变压缩比例。轻点松手、Touch Canceled、丢失指针、暂停和失去玩法状态都会释放候选接管，恢复前先执行 `Physics2D.SyncTransforms()`。
- 携带期间同时写 Actor Transform 与 Rigidbody2D position。中断或对象禁用时，按钮会在仍携带 Actor 的状态下先回到休止位并同步物理，再恢复角色刚体，避免角色留在压缩深度。
- 没有在井中增加常驻 Collider：独立复核确认深井平台会封住几乎整个洞口并使异常下落角色软锁，因此最终采用输入候选阶段立即接管的局部修复，不修改共享 Actor、Layer Matrix、地面 Prefab 或其它关卡。
- 使用 Unity 6000.0.59f2 自带 Roslyn 与当前 Bee `Assembly-CSharp.rsp` 后台完整编译通过，`0 error`；Prefab 静态复扫为 `186` 个定义、`186` 个唯一 fileID、`0` 重复、`0` 悬空本地引用，Level94/Level32 GUID 为 `1/0`，旧回调为 `0`，阈值 `0.5` 恰好序列化一次。未启动或操作前台 Unity。
- 旧 `PASS checks=29` 发生在成功阈值仍为 `0.67` 时，只保留为原主动路线与触发门禁的历史基线，不能证明当前 `0.49/0.50` 边界、候选轻点/取消、暂停恢复和移动端触摸；这些动态手感仍为 `uncertain`。

## 2026-08-11：主动弹射叠加抛物线音效

- 需求所描述的“角色被甩飞的抛物线音效”在工程中实际来自 Level093，而非 Level088：SoundsGood tag 为 `l93PlayerLaunch`，资源为 `Assets/Game/ArtSrc/Audio/Sfx/l93/l93PlayerLaunch.ogg`，Level093 文档记录其原文件名为 `抛物线.ogg`。Level088 仅使用敲门、门碎和标准进门音效。
- `Level94Ctrl` 新增本地常量 `PlayerLaunchSfxTag`，只在欠压侧抛写入刚体初速度后、以及成功月球 Tween 确实建立后播放一次；两个分支都保留原 `spring`，符合“加个音效”而非替换弹簧声。
- 普通踩踏的 `LaunchPassiveBounce()` 不播放 `l93PlayerLaunch`，避免轻微上弹也出现夸张甩飞声；空引用回退、落地检测、暂停恢复和每帧更新路径均没有该调用。
- 本轮只修改 Level094 私有控制器与文档，不改 Level088、Level093、`SoundCollection.asset`、音频文件或共享 `AudioManager`。后台 Roslyn 完整编译为 `0 error / 104` 条工程既有 warning，Level094 诊断为 `0`；静态扫描确认 tag 常量 `1` 个、调用点 `2` 个、注册项 `1` 个，被动分支调用 `0` 个。未启动或操作前台 Unity。
- 音量混合、两声叠加的听感、移动端扬声器表现和暂停/重开边界仍为 `uncertain`，需前台 PlayMode/真机验收。只回滚本轮时删除该常量和两个 `PlaySound(PlayerLaunchSfxTag)` 调用即可，保留原三处 `spring`。
