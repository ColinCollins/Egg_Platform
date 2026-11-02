# DLevel_086 脚本结构记录

## 2026-09-15 地图恢复与 D087 分离

用户确认今天误存的新地图另存为 D087，D086 恢复昨天布局。以 `E:/WorkSpace/outputs/dlevel-new-stroke-tunnel-20260914/DLevel_086.prefab.before-guard` 为历史布局依据，仅恢复绳、两块 StoneB 启用状态、Tips、成功门、Rig姿态、中地面及长地面8个变化块。D086 原 GUID 保持，当前根上的出生保护组件保留；球有界弹射字段、线材质和共享代码均未回退。

恢复后两块 StoneB 和 Ground_mid 启用，成功门回到左上原位置，长地面恢复昨天左侧位置/角度。新地图保存在独立 DLevel_087.prefab，编号20087、排序62、标题弹性的线4。D086自身20086/排序61/系列3不变。

逐块恢复/保留断言通过，D086和D087各156个对象、本地引用完整、42个外部GUID可定位，33个旧关/共享文件哈希保持。没有新增运行时代码或修改重置/UI/事件边界；完整解法、提示、重开和移动端仍为 uncertain。证据及本轮实际修改前备份位于 `E:/WorkSpace/outputs/dlevel86-87-split-20260915/`；回滚按8个变化块恢复今天状态，不整文件覆盖后续编辑。D087职责与配置见相邻Level87记录。

当前实现已按文末“新线出生保护与有界弹射结构修正”更新，早期材质和纯限速描述为历史基线。

## 基线与职责

2026-09-14，用户确认 `20086 / DLevel_086 / 弹性的线3`，按 D085 最终物理方案接入。依据用户当前 Prefab、中央 DrawGame 结构规范、D085 最新限速/平台/滚动/尖刺章节。本关无需新增运行时 C#，只读复用原组件，不迁移或改写旧关。

```text
DLevel_086
├─ DrawLevelCtrl                  尝试状态、输入、结果锁与标准成功/失败重建入口
├─ DrawLineFeature                多笔物理画线、墨水与首笔分发
├─ DrawLevelCountdownCtrl         首笔后 20 秒失败倒计时
├─ DLevel2Ctrl                    可见 Intro、夹持、首笔放球与 Retry 到位态
├─ DLevel85SpikeTrapHandle        本关尖刺的球身份判断、失败表现与局部清理
├─ Alien / DrawGame_AlienClipBallRig
│  └─ yellow_ball                Rigidbody2D、CircleCollider2D、Actor、DLevel84BallSpeedLimiter
├─ SpikeStrip_00                  Trigger → OnSpikeTriggered
├─ DLevel086_Knock_Explosion      本关持有的裁剪粒子实例
├─ DrawGame_SuccessDoor           OnTriggerSuccess 与成功 Spine
├─ DrawGame_SuccessDoor_DisabledLegacy（停用）
├─ Ground_mid / Ground_long / StoneB (1) / StoneB (2)
├─ 四周 FallArea                 既有出界失败入口
└─ Tips、提示线、相机、背景与装饰
```

## 接口、调用和生命周期

1. 既有 Draw 加载链按配置路径创建本关/HUD，DrawLevelCtrl 管理尝试状态。DLevel2Ctrl 通过 `IDrawLevelVisibleIntroHandler` 执行猫夹球 Intro，完成后放开输入。
2. DrawLineFeature 将有效笔画交给 `IDrawStrokeCompletedHandler`；D002 每次尝试首笔释放球，Countdown 启动 20 秒计时。球从 Kinematic 改为 Dynamic，并脱离动画父级。
3. 正常弹射、回弹和滚动由 Physics2D 与成对材质承担。限速器在 FixedUpdate、OnCollisionEnter2D/Stay2D 约束球线速度；只在运行态、组件启用、球 simulated 且 Dynamic 时工作，保留角速度，不增加重置接口。
4. 尖刺 OnTrigger2DHandle 动态持久事件调用本关 Handler。只有 attachedRigidbody 等于绑定球且结果未锁定时，才能占用 `TryBeginFailurePerformance`；随后播放 Knock、Die 和 trap 音效，保留 0.5 秒演出，再 `CompleteFailurePerformance → TryFailAndReset → DrawGameResetEvent`。失败演出期间输入/其它结果锁定、倒计时暂停。
5. 有效门调用当前根 `OnTriggerSuccess`，成功 Spine 属于同一个启用门。倒计时或 FallArea 走既有失败接口；没有另建局部成功事件。
6. Reset/Replay 沿用现行黑幕销毁/重建关卡与 HUD，重建后 `PlayResetIntroAsync → ApplyDrawRetryIntroState` 直接到位。D002 Disable 清理 Intro，尖刺 `IDrawStrokeCompletedHandler.OnDrawAttemptReset` 和 Disable 只取消自身未完成演出并停止粒子。限速无尝试状态，无需额外 Reset。

## 序列化绑定

| 字段或宿主 | 本关最终绑定 |
| --- | --- |
| DrawLevelCtrl / drawFeature | `900000000000000001` / `900000000000000002` |
| successDoorSpine / 成功组 | `7594331605050263445` / `3091325050892645083` |
| 有效门实例 | `2322687108484577280`，OnEnter target 为根控制器，方法 OnTriggerSuccess |
| DLevel2Ctrl / alien | `900000000000000039` / `1255546920056987011` |
| 球刚体 / 放球父级 | `6262018380601849612` / 根 Transform `8549164348132234925` |
| alienSpineCtrl / alienSkeletonAnimation | `6833647044240291983` / `1326194386933571420` |
| BoneFollower / Swing / Clip Spine | `6907488015446782417` / `1943803583873973196` / `2849637529966163699` |
| 球限速器 / 球对象 | `840850000000000001` / `4500677910547608250`，maxLinearSpeed=30 |
| Rig 的 added component | `3168915018210331464` 在源 yellow_ball `1553638369533164018` 上挂限速器 |
| 尖刺 Handler / Collider | `850000000000000001` / `2235164531252919237` |
| 尖刺事件 | `2012992477989094739` → Handler.OnSpikeTriggered，空 Tag 过滤，运行态动态调用 |
| knockEffect / 实例 | `982062655520787473` / `850000000000000002`，源 PS `462519149007686675` |
| 两平台 Collider | `3583285206964772767`、`6676497914815354429`，都显式 StableGround |

## 数据、资源与局部边界

- LevelData 第 66 行新增 20086，保持编号升序，20999 移至 67 行；LevelSort 第 66 行追加末项 61。两份 Draw JSON 由 Luban 生成并同步，旧记录不变。
- Tips 使用独立 `S_DrawLevel_Tips_Level_86`，编辑态文字“弹性的线3”；I2 新增系列 3 的 12 种语言，保留其它 64 个空语言槽与所有旧词条。
- 画线保持墨水 50、星级 0/10/20、质量 32、最小质量比例 0.25；`isConfig=0`，Prefab 参数与源表保持一致，多笔开启。
- 线/地面复用 Level84 材质；球复用 Level85 RollingBall，球弹性 0.4/Maximum，线 2/Multiply，地面/平台 0/Multiply。球摩擦 0.25、角阻尼 0.05、无旋转约束；后续仅本关调参先复制资源再改引用。
- 每关实例独立持有状态和粒子。HUD、提示、暂停、广告、Analytics、存档、远端排序和资源加载仍归既有 Draw 流程；本次未增加全局事件、保存字段或平台协议。

## 相对 D085 的差异与结构复盘

本关采用用户新布局。已有逻辑引用与材质复制完整，只需配置入口和独立标题，另将复制的粒子对象名称改为 086。类名仍保留原关编号，当前代码没有按该编号访问配置或查找本关对象，名称不构成跨关状态依赖。

复盘决定：纯物理系列优先复用已验证的实例组件与显式材质，逐个核对新增静态平台和实际球宿主。无需为形式一致添加 DLevel86Ctrl，或改名迁移既有关卡脚本。通用化尖刺/限速命名仍保留为后续候选，未扩大共享接口或旧关回归范围。

## 验证、缺口与回滚

155 个唯一对象与本地引用完整；关键组件、方法、材质和源资源校验通过；Prefab 仅两个实例块变化。42 个外部 GUID 可定位；旧背景/角色外观的 7 个无源对象 override 及 2 个已移除门组件记录单独保留，均来自 D085，未把这类历史记录当成运行时空绑定。详情、剩余视觉核验与 ID 明细见同目录功能风险记录和 `resource-validation.json`。

两表已回读、重算、局部渲染和错误扫描；原值/类型/样式及其它工作表保持，Luban 生成通过，两份 Draw JSON 仅新增本关记录。旧关 Prefab、局部代码/材质和共享球 Rig 的哈希保持。无 C# 改动，本轮未重复编译/物理压力测试；前序测试只支撑相同组件参数的既有边界。

`uncertain`：当前布局的完整进门解法、提示画法、连续死亡重开、暂停与移动端手感尚未试玩验收。回滚仅撤销本关标题、粒子名称、I2 86 词条及 20086 配置，重生成对应 JSON，保留用户布局与旧关。备份/验证目录为 `E:/WorkSpace/outputs/dlevel86-20260914/`。


## 2026-09-14 新线出生保护与有界弹射结构修正

本节取代此前“线材质弹性 2、仅球速钳制足以覆盖新增笔画”的方案。用户实际报告补画一根线时穿模，测试复现同一物理步位置已跳出、末速度却已受限。

```text
当前 DLevel 根
├─ DrawLineFeature → StrokeCompleted → 既有 IDrawStrokeCompletedHandler 分发
├─ DLevel84StrokeSpawnGuard（新增，组件 840860000000000001）
│  ├─ drawFeature = 900000000000000002
│  ├─ 只取得当前已完成运行时线条的附属碰撞体
│  ├─ 出生重叠动态碰撞对暂缓 → 分离超过 0.02 → 恢复
│  └─ AttemptReset / Disable 仅释放自身临时忽略关系
└─ yellow_ball / DLevel84BallSpeedLimiter（原组件 840850000000000001）
   ├─ elasticLineMaterial = 8db86cf100124ebfbbb13b4e50990f8f
   ├─ lineLaunchRestitution = 2；每物理步最多一次有界法向弹射
   └─ maxLinearSpeed = 30，窄框按间距继续降速；角速度保持
```

线材质本体现在为 0/Multiply，球和地面材质不变。零增能的求解器负责线线/线地稳定接触，球组件在有效线接触后补足弹射速度。出生保护先于下一物理步工作，不更改笔画几何、静态地面或 Trigger；新状态随本关实例存在，由局部接口清理，不增加全局 Reset 或事件。

复盘结论：对高弹多线场景，回调后的速度上限不能保证同一步的位移安全；须避免大于 1 的材质在求解器内反复增能，并处理新实体初始重叠。改动限制在已请求的 084/085/086 系列局部资源和显式组件，未把功能上提到 DrawLineFeature，未改变已验收其它关卡。

最终 36 组单次追加 + 16 组连续追加、各 30 秒均通过，正常单面发射/球地回弹和滚动保持，临时忽略关系清理与编译通过。组件复制基线必须同时包含零弹线材质、球有界弹射绑定和根出生保护，不能只复制其中一部分。完整关卡、任意画法及移动端仍为 uncertain，细节、边界与回滚见本关同日功能风险记录和 E:/WorkSpace/outputs/dlevel-new-stroke-tunnel-20260914/。
