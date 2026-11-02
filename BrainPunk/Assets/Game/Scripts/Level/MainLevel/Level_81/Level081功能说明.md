# Level081「水果忍者」功能说明

更新时间：2026-09-01

## 需求背景

Level081 基于 Level004 的场景、角色、门和标准结算结构制作。角色到达台子上的泡泡后进入水果玩法：泡泡消失，角色停止移动并切换剑术待机；玩家滑动屏幕切开持续抛出的水果，获得分数和 `WELL / GOOD / PERFECT` 评价。达到目标分后，图片锁替换为标准 Spine 锁并演出钥匙解锁；玩家也可以在玩法过程中直接切开图片锁，恢复移动后进门过关。切中炸弹会触发聚焦、爆破击飞和标准失败流程。

## 影响范围

- 关卡 Prefab：`Assets/Game/Prefabs/Level/Level_081.prefab`
- 专用 GamePanel Prefab：`Assets/Game/UI/GamePanel/Resources/GamePlayPanel_081.prefab`（直接内置本关 HUD 节点，编辑器可预览）
- 可编辑 HUD 节点：`Assets/Game/UI/GamePanel/Resources/GamePlayPanel_081.prefab` 内的 `Root/Level_081_HUD`
- 本关脚本：
  - `Level81Ctrl.cs`：玩法状态、输入、计分、炸弹失败和开锁编排。
  - `Level81ActorCtrl.cs`：仅本关使用的剑术动画控制。
  - `Level81FruitItem.cs`：水果/炸弹抛物运动、火花帧和命中判定。
  - `Level81GameplayHud.cs`：绑定并复位 GamePanel 中预先摆放的 HUD，只更新分数/目标数值与评价表现，不创建或销毁 authored HUD。
  - `Level81SliceMesh.cs`：Sprite 网格切割、半片物理和渐隐清理。
- 本关图片：`Assets/Game/ArtSrc/Sprites/L81/`
  - 8 张水果图、3 张炸弹/火花图、刀具图及目标/评价 UI 图。
- 用户提供的切水果特效按原路径增量导入到 `Assets/Epic Toon FX/`：
  - `Prefabs/Combat/Explosions/FruitExplosion/CoconutExplosion.prefab`
  - `Materials/Misc/Unsorted/fruitslices.mat`
  - `Materials/Misc/Liquid/splat02_3x3_AB.mat`
  - `Textures/fruitslices.png`、`Textures/splat02_flat_3x3.png`
  - `Sound/etfx_explosion_slime.wav`
  - `Scripts/ETFXPitchRandomizer.cs`

实现继续复用标准 `BaseLevelCtrl`、Actor Prefab、`Key`、`DoorLock`、出口门、Bear EventSystem、DOTween、Spine 和 Level067 的爆破表现资源。没有修改共享 `BaseLevelCtrl`、共享 `ActorCtrl`、共享 GameplayPanel Prefab、全局事件类型、存档、货币、analytics、RemoteConfig 或平台 SDK。

## 原有行为基线

- Level081 场景由 Level004 复制，保留出生门、角色、出口门、背景、相机、失败区和标准成功/失败链。
- 场景已有策划摆放的 `bubble` 和图片 `lock`，但原副本没有水果玩法、计分 HUD、滑动切割、炸弹失败或标准 Key/DoorLock 接力。
- 副本根节点原先仍挂其它关卡控制器；本关逻辑由独立 `Level81Ctrl` 接管，不修改来源关卡脚本。
- 复制遗留但不属于本关的 `Tips / Box / wayPoint / EndPoint / BoxBroken` 已从 Level081 Prefab 本地移除，不修改它们的来源 Prefab 或脚本。

## 当前行为

### 进入玩法

- `Level81Ctrl` 在游戏为 `PLAYING` 时检查角色是否真正站在 `Sp_l83_tz_01` 木台上：角色必须处于落地状态，至少一个启用的非 Trigger 实体碰撞体与木台 Collider 接触，并且角色脚底到台面顶端的误差不超过 `bubblePlatformFootTolerance`。只有木台绑定缺失时才使用泡泡水平/垂直距离作为安全回退；触发后隐藏整个泡泡根节点。
- 进入玩法前记录角色原控制状态，通过 `BaseLevelCtrl.SetCtrlable(false)`、清空移动输入和 `StopMoving()` 锁定移动。
- `Level81ActorCtrl.EnterSwordMode()` 立即无混合播放循环 `sword_idle`，并阻止通用 Actor 动画更新覆盖剑术动画。
- `GamePlayPanel_081.prefab` 是基础 GamePlayPanel 的本关 Variant，并直接内置 `Root/Level_081_HUD`；运行时 `Level81Ctrl` 只从 `PlayCtrl.CurrentGamePlayPanel` 查找、初始化、显示和复位该 authored 实例，不执行 HUD 的 `Instantiate` 或 `Destroy`。`Score_Label_Localize / Target_Label_Localize / Reward_Label_Localize` 是可直接选中和调整 RectTransform、字号的实体，并分别通过 I2 `Localize` 组件绑定现有词条 `S_LevelTips_Des_level_81_3 / _2 / _1`；代码只更新独立的 `Score_Value / Target_Value`，不会覆盖本地化文本。所有 Graphic 均不拦截 Raycast，编辑器态保持可见以便预览，运行时进入玩法前由组件隐藏。

### 抛物、滑动与动画

- 每轮随机生成 `3~5` 个水果；另按 `bombRoundChance` 概率追加 1 枚炸弹。水果数量和炸弹概率均为 Inspector 参数。
- 每轮随机选择首个水果从左侧或右侧起抛，后续成功生成的水果按左右交替；左侧起点带由 Inspector 配置，右侧自动镜像，落点最小/最大值分别约束在屏幕左右半区并限定为画面中部带。起点、落点使用相机 viewport 比例换算为世界坐标，再根据目标飞行时长和 Physics2D 重力反算初速度，使整轮抛物从截图红框下方的屏幕外两条中心发射带向上、向内汇聚，避免靠近右侧门锁。水果/炸弹具有随机角速度，落到屏幕下方或寿命耗尽后回收。
- 炸弹主体旁创建 `FuseSpark`，按绑定的两张火花 Sprite 循环切帧。
- 移动端优先跟踪一个有效 Touch；编辑器下提供鼠标拖动调试。触点起始于 UI 上时不进入切割；暂停、失焦、触摸取消、Reset 或流程结束都会关闭当前手势。
- 相邻采样达到最小像素距离后，用世界空间线段处理命中，并更新运行时 `TrailRenderer` 拖尾；每次新手势先停止发射、把 Trail 根节点移动到新起点、清空旧轨迹并播种新起点，再恢复发射，避免与上一手势终点连线。拖尾明确使用 `SceneLayer / order 1000`。
- 每个有效滑动手势只触发一次挥剑，从 `sword_swing / sword_swing01 / sword_swing02` 随机选择且不会与上一次相同；切换均为零混合，挥刀结束保持末帧 `swingHoldDuration=1.5s` 后回 `sword_idle`。

### Sprite 切割和特效

- `Level81SliceMesh` 将世界切割线转换到源网格本地空间，按三角形和 UV 分别裁剪两侧，生成两个临时 Mesh；首切读取 Sprite 网格，二切读取当前半片的运行时 Mesh。
- 水果半片继承原抛物速度和角速度，向切线两侧分离、受重力下落并渐隐；图片锁半片额外生成 PolygonCollider2D，沿切割方向分离、掉落后渐隐。
- 首切只支持 `SpriteDrawMode.Simple`；圆形范围叠加刀宽负责候选命中，切线会向网格内部约束并复核最小半片面积。必须成功生成两个半片后才确认首切、计分并销毁原水果。手势开始时只快照此前已存在的半片作为本手势二切候选，因此本次刚生成的两半不会在同一连续滑动的后续采样帧再次碎裂；抬手后的下一次独立手势仍可继续切半片且不计分。
- 每次切水果实例化 `CoconutExplosion`。运行时关闭名为 `Gibs` 的椰子切片节点，保留云雾/喷溅粒子，并按该水果配置的 `juiceColor` 重着色；Cloud 保留半透明白色。

### 计分和评价

- 每切中 1 个水果立即加 `5` 分。
- 本轮水果全部结束后按切中数量结算：
  - 切中 `总数 - 2`：`WELL`，额外 `+10`。
  - 切中 `总数 - 1`：`GOOD`，额外 `+20`。
  - 全部切中：`PERFECT`，额外 `+40`。
- 评价图使用淡入、`OutBack` 弹性放大、短停留和淡出；分数变化也有一次短促回弹。
- 炸弹不计入本轮水果总数和评价分母。

### 两条开门路线

1. **目标分路线**：分数达到 `targetScore` 后停止生成和输入，隐藏 HUD 与图片锁，激活标准 Spine `DoorLock`。本关先停止并禁用 Key 自带的 `MoveFloatHandle`，按 authored Key 的初始位置显示钥匙；随后镜头移动并缩放到钥匙中心，停留 `keyFocusHoldDuration`，再让钥匙沿可调弧线飞向出口门的 `door_0/LockPoint`。飞行期间镜头开始恢复，抵达后播放 `getKey` 和 DoorLock 的 `unlock` Spine/音效；解锁动画完成或达到 `unlockTimeout` 后才隐藏 Spine 锁、开放出口、退出剑术状态并恢复角色操作，不启动共享 Key/DoorLock Executor。
2. **直接切锁路线**：水果玩法进行中，滑动线段优先检查图片锁下方锁体的专用判定框。切中后立即停止玩法，按实际切线生成两块侧移、轻旋转、下落渐隐的锁片，隐藏原锁和 HUD，开放出口并恢复角色操作；该路线不要求达到目标分。

两条路线最终都只开放既有出口 Trigger，玩家仍需移动进入门，由 `BaseLevelCtrl` 执行标准关卡成功、结算和后续流程。

### 炸弹、暂停和重开

- 切中炸弹后立即停止切割输入，并通过现有 `GamePlayPanelSwitchBlockEvent` 遮住玩法 UI，阻止暂停、商店、提示和移动按钮在失败链中介入；炸弹本体停在命中位置并保留完整图片，火花停在当前帧。同时冻结本轮其它抛掷物、已有切片、HUD、角色刚体与剑术 Spine，并暂停已有/新生成的本关粒子。刀光拖尾停止发射并保持当前形态。相机在不受 timeScale 影响的短 Tween 中同时缩放并移动中心到炸弹。
- 短暂停顿后生成显示关卡 67 提前发射同款 `GrenadeExplosionFire`，播放 `rocketExplosion`；同帧先派发现有 `OnTriggerTrapEvent`，由 `BaseLevelCtrl` 标准陷阱链同步调用 `ActorCtrl.Die()`，随后按 `(-0.5,0.7)*50` 施加未归一化击飞并持续旋转。若标准监听缺失，本关才直接调用 `Die()` 兜底。
- 本关订阅现有 `SwitchGameStateEvent` 和 `GameResetEvent`。暂停时停止输入、抛物模拟、HUD 和聚焦 Tween；恢复后继续，并重新压住 `BaseLevelCtrl.GameResume()` 对 Actor 的通用移动恢复，直到本关开锁完成。Reset、Disable、Destroy 会使异步会话失效，清除水果、切片、拖尾、Tween 和运行时材质，复位预置 HUD，并还原相机位置与尺寸。

## UI、资源与事件流

```text
角色真正站上泡泡所在木台
  -> 隐藏 bubble / 锁定 Actor / sword_idle / 显示 GamePlayPanel_081 中的预置 HUD
  -> 循环抛出 3~5 水果 + 概率炸弹

玩家滑动
  -> TrailRenderer + 非重复 sword_swing
  -> 线段命中水果 -> 网格切半 + 彩色粒子 + 基础分
  -> 首切半片可继续切一层 -> 只生成新碎片/特效，不重复计分
  -> 本轮结束 -> WELL / GOOD / PERFECT 奖励分

达到目标分
  -> 图片锁隐藏 / Spine DoorLock 显示 / Key 显示并聚焦
  -> 聚焦停留 / Key 弧线飞向 door_0/LockPoint / getKey / DoorLock unlock Spine 与音效（暂停期间冻结）
  -> 开放出口 / 恢复 Actor -> 进门走 BaseLevelCtrl 成功链

滑动切中图片锁
  -> 锁沿切线分成两半并下落渐隐
  -> 直接开放出口 / 恢复 Actor

滑动切中炸弹
  -> 炸弹原图/位置/火花当前帧冻结 + 相机中心与尺寸聚焦
  -> 显示关卡 67 提前发射同款爆破 + Die/Knockback
  -> BaseLevelCtrl 失败/重开链
```

- 所有分数、轮次、当前手势、图片锁状态和会话版本均为单局运行时状态，不新增存档字段。
- 本关没有新增全局事件类型；消费现有游戏状态/Reset 事件，并在炸弹演出结束后派发现有 `OnTriggerTrapEvent`。
- Epic Toon FX 已有 `circle/cloud/glow` 公共材质和纹理按原 GUID 复用，没有覆盖现有资产。

## 主要可调参数

| 参数 | 默认值 | 作用 |
| --- | ---: | --- |
| `targetScore` | `30000` | 正常计分路线的目标分 |
| `fruitScore` | `5` | 单个水果基础分 |
| `wellBonus / goodBonus / perfectBonus` | `10 / 20 / 40` | 每轮评价额外分 |
| `minimumFruitCount / maximumFruitCount` | `3 / 5` | 每轮水果数量范围 |
| `bombRoundChance` | `0.1` | 每轮追加炸弹概率 |
| `firstRoundDelay / betweenRoundDelay` | `0.35s / 0.45s` | 首轮和轮间等待 |
| `spawnViewportMinX / spawnViewportMaxX` | `0.34 / 0.40` | 红框下方左侧起抛带；右侧自动镜像为 `0.60~0.66` |
| `landingViewportMinX / landingViewportMaxX` | `0.44 / 0.56` | 左右向上抛物共同回落的画面中部范围 |
| `flightDurationRange` | `2.4~3.05s` | 完整抛物飞行时长范围 |
| `bubbleFloatAmplitude / bubbleFloatCycleDuration` | `0.12 / 1.4s` | 泡泡待机上下浮动幅度和周期 |
| `fruitScale` | `0.66` | 水果显示倍率，较上一版 `0.55` 精确放大 `1.2x` |
| `trailLifetime / trailWidth` | `0.18s / 0.30` | 滑动拖尾时间和宽度 |
| `sliceBladeRadius / minimumSliceAreaRatio` | `0.15 / 0.18` | 粗刀候选范围和最小半片面积比例 |
| `additionalFruitSliceDepth` | `1` | 首切后的每个半片可继续切一层且不计分 |
| `swingHoldDuration` | `1.5s` | 挥刀动画完成后保持末帧的时长，新手势可打断 |
| `bombFocusZoomRatio` | `0.84` | 炸弹聚焦后的正交相机尺寸倍率 |
| `bombFocusDuration / bombFreezeDelay` | `0.15s / 1s` | 镜头聚焦时长和聚焦完成后的定格时长 |
| `unlockTimeout` | `3s` | 等待 DoorLock `unlock` Spine 动画完成的最长时间 |

水果颜色、抛物 viewport、缩放、旋转、命中半径、半片物理、渐隐时间、钥匙弧线和炸飞参数也都只暴露在 Level081 控制器上，可由策划单关调节。

## 验证记录

- 已静态核对 5 个 Level081 私有脚本的入口、计分、输入、动画、暂停、Reset、资源释放和两条开门分支；另核对 `GamePlayPanel_081` 的 内置 HUD 绑定和运行时仅查找/复位路径。
- 已复核达标路径会通过 `DiscardRoundItems()` 先遍历销毁再统一清空列表，避免清理过程中修改列表造成越界；首次挥剑会直接从完整动画数组随机，后续才使用排除上一次的索引映射。
- 最终 `Level_081.prefab` 静态统计为 `184` 个定义、`184` 个唯一 fileID、`368` 次纯本地非零引用（覆盖 `184` 个唯一目标）、`0` 重复、`0` 悬空、`0` Missing Script；`Level81Ctrl` 与 `Level81ActorCtrl` 各唯一引用 1 次，旧 Level7 控制器 GUID 为 0 次。出口门上从 Level007 复制来的无目标 `SwitchCollision2DStateListener` 及其 `EventListenerExecutor` 已仅在 L081 内移除，源门 Prefab 和旧关未改。
- `Level_081.prefab` 已静态确认绑定 BaseLevelCtrl、本关 Actor、bubble、图片锁、标准 Key/DoorLock、Key Collider、出口 Trigger、世界相机、8 张水果、炸弹/两帧火花、CoconutExplosion、显示关卡 67 提前发射实际使用的 `GrenadeExplosionFire` 及 `GamePlayPanel_081` 面板名；`targetScore=30000`、`bombRoundChance=0.1` 已序列化。`GamePlayPanel_081.prefab` 已静态确认直接内置 `GamePlayPanel_081.prefab/Root/Level_081_HUD`，两份 Prefab 定义与引用均无重复。
- 当前主关卡 Prefab 有 `65` 个唯一外部 GUID；内联并由 Unity 重序列化后的 `GamePlayPanel_081` 有 `16` 个唯一外部 GUID、`65` 个定义、`135` 次本地引用、`0` 重复和 `0` 悬空。HUD 的 I2/TMP/Image 脚本 GUID 均复用项目现有 Prefab 的已知绑定模式。
- 已核对 unitypackage 增量导入：7 个新增资产及原始 `.meta` 的 SHA256 与包内文件一致；新增 GUID 均唯一。
- `CoconutExplosion` 的本地 fileID 引用无悬空；Prefab、切片材质和液体材质的外部 GUID 均可解析。现有 URP Cloud 材质引用的 Shader/版本脚本可在当前 PackageCache 解析。
- 已确认项目内 `ETFXPitchRandomizer` 类型只有一份，不存在重复类名。
- `specs/037-remote-resource-loading/plan.md` 在当前工作区不存在，无法作为本关实现依据，状态为 `uncertain`。
- 第二轮静态审查已修正候选命中先 Resolve 导致的无切片计分、炸弹先 `Die()` 导致标准陷阱事件被 `isFinished` 忽略、Pause Resume 重新放开 Actor，以及拖尾未显式设置 `SceneLayer` 四项问题。
- 2026-08-31 使用 Unity 6000.0.59f2 当前 `Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp` 与 `rsp2` 对包含最终木台判定、炸弹聚焦冻结、失败 UI 门禁和复用状态清理的完整 `Assembly-CSharp` 源集重新编译到 `Temp/Assembly-CSharp.Level081FinalVerified.dll`，退出码为 `0`；没有 L081 诊断或编译错误，仅有项目既有警告。该命令未覆盖 `Library/ScriptAssemblies`。
- 本轮 8 项体验修正后再次使用同一 Unity Roslyn 响应文件全量编译最新源码到 `Temp/Assembly-CSharp.Level081FinalVerified.dll`：`exit 0 / 0 error / 107` 条工程既有 warning / `0` 条 L081 diagnostic。Prefab 复核为 `184` 个唯一定义、`368` 次本地引用、`0` 重复、`0` 悬空、`0` Missing Script、`65` 个唯一外部 GUID；旧水果/拖尾/爆破/空木台绑定关键词为 `0`。
- 本次左右汇聚轨迹和炸弹独立 `1s` 定格补丁完成后，再次使用同一 Unity Roslyn 与完整响应文件后台全量编译到 `Temp/Assembly-CSharp.Level081TrajectoryVerified.dll`：`exit 0 / 0 error / 107 warning / 0 L081 diagnostic`；未启动 Unity 前台或 PlayMode。
- 两轮独立只读复审分别覆盖运行时代码和 Prefab/资源闭包，未发现需求 1~8 的阻断或明确逻辑回归；没有启动、切换或操控 Unity 前台。木台仍为策划值 `(0.02,-2.42,0)`，泡泡根仍为 `(0,-0.559,0)`。
- L81 素材目录误生成的 `Thumbs.db.meta` 已移除，二进制缓存已移出 Unity `Assets`；该缓存原本也受仓库 `.gitignore` 的 `Thumbs.db` 规则覆盖，不属于关卡资源。

## 未验证项

- 未操作 Unity 前台、未运行 PlayMode，不声明泡泡触发距离、预置 HUD 实际位置、多分辨率布局、拖尾手感、抛物节奏、Sprite 切口、粒子颜色、评价弹性、钥匙聚焦/飞门构图、钥匙与锁演出或炸飞画面已验收。
- Android/iOS 横屏触控、多分辨率 Canvas、低帧率物理和连续快速滑动尚未验证。
- Spine 中 4 个剑术动画名及零混合切换、显示关卡 67（配置映射到 `Level_050`）提前发射爆破画面、Key 弧线和 DoorLock `unlock` 动画/音效仍需在 Unity 运行态确认。
- I2 三个 L081 词条在各目标语言下的最终译文、大小写和新 HUD RectTransform 宽度适配仍需本地化与多语言画面确认；标签完全由 Prefab 上的 I2 组件负责，不再提供代码英文兜底。
- 当前 `Assets/Game/Configs/tbleveldata.json` 已静态登记 `id=10081 / path=Level_081`，`tblevelsort.json` 已有对应排序项；正式资源收集、运行时入口和线上发布状态仍需配置流程与 PlayMode 确认。

## 生命周期补丁（2026-08-31）

- `Level81Ctrl` 现在把 `GameResetEvent` 视为旧实例 teardown 边界：先标记 `resetOrReplayReceived`、递增会话版本并清理异步运行时；旧实例在 PlayCtrl 销毁前不会再次消费泡泡、接收切割输入或补发炸弹失败事件。预置 HUD 只复位显示状态，不销毁 authored 实例。
- 关卡根节点被临时禁用后重新启用时，会恢复泡泡、图片锁、DoorLock、Key/门碰撞门禁、相机尺寸和钥匙初始 Transform，并清零本局分数、轮次和失败/完成标志；预置 HUD 只复位显示状态，不销毁 authored 实例。若角色仍存活，会退出本关剑术模式并恢复原控制权。
- 炸弹聚焦和爆破等待使用非缩放时间，但只在游戏状态为 `PLAYING` 时累计；暂停面板打开期间等待保持不变，恢复后继续。爆破同帧只派发一次标准陷阱事件，击飞旋转 Tween 在清理时显式终止。

## 特效音频与编辑器控制修正（2026-08-31）

- `CoconutExplosion` 只作为 Level081 的切果汁视觉模板使用。每次实例化后，本关控制器会立即停止并禁用模板内全部 `AudioSource`，避免插件演示音效绕过项目统一音效通道和音效开关。
- Level081 专用 Actor 的 `IsEditor` 与标准 Actor Prefab 保持为启用状态，便于在编辑器中直接使用既有键盘控制验证泡泡触发、切锁和出口流程；正式包输入逻辑不变。

## 炸弹演出与暂停补丁（2026-08-31）

- 炸弹失败流程先冻结角色物理并完成 `bombFocusDuration` 镜头聚焦，再额外等待 `bombFreezeDelay`，之后恢复角色物理、派发标准陷阱失败事件并施加击飞；没有世界相机时只保留额外停顿。
- 切果喷溅和角色爆破粒子由 Level081 维护剩余寿命。暂停时逐个暂停当时仍在播放的粒子，恢复时只恢复这些粒子；暂停时间不计入销毁寿命，Reset、Disable 和 Destroy 会统一清理实例。
- 炸弹击飞后会持续缓存角色刚体速度。若暂停事件或共享 `BaseLevelCtrl.StopMoving()` 清零速度，本关先冻结刚体并在恢复时写回缓存速度，避免击飞在暂停后停在原地；旋转 Tween 与刚体冻结状态也同步响应暂停和清理。

## 木台触发与达标开锁收尾（2026-08-31）

- 泡泡入口已绑定 `Sp_l83_tz_01` 的 `BoxCollider2D`。角色必须落地、实体 Collider 与木台实际接触且脚底接近台面顶部才会进入玩法；碰撞体收集使用复用列表，避免泡泡触发前每帧分配临时数组。
- 达标路线不再调用共享 Key/DoorLock Executor。Key 自带漂浮被本关关闭，先按 authored 初始位置显示钥匙，再把相机聚焦到钥匙并停留 `keyFocusHoldDuration`，随后沿可调弧线飞向 `door_0/LockPoint`；飞行期间相机恢复，抵达后播放 `getKey`、DoorLock `unlock` Spine/音效并开放出口，全部由 L081 私有流程编排。暂停时不会后台推进，若暂停发生在开放出口的边界，恢复后会补回原角色控制权。
- 炸弹聚焦期间同时冻结本关剑术 Spine，聚焦结束后再恢复并进入死亡/击飞流程。
- 炸弹聚焦冻结状态现同时覆盖本关运行时粒子与刀光拖尾，并与暂停状态叠加；关闭暂停面板不会在镜头聚焦结束前提前恢复这些表现，Reset/Destroy 仍统一清理。
- 聚焦展示与拖尾冻结标记由统一的 `ResetRuntimeFlags()` 清零，覆盖“暂停时禁用关卡根节点、随后在 PLAYING 状态直接复用”的路径，避免新拖尾永久不衰减。
- 炸弹命中后通过现有 GameplayPanel 点击遮罩阻断其它 UI 操作，避免标准失败计时在暂停面板后继续推进；Reset、Disable 和 Destroy 清理时成对解除遮罩，不修改共享 GameplayPanel 或 `BaseLevelCtrl`。

## 水果切割与炸弹表现修正（2026-08-31）

- `bubble` 根节点按本关私有、暂停安全的正弦曲线在初始位置上下 `0.12` 浮动；消费、Reset 和同实例复用都以缓存初始本地坐标为基准，不修改策划摆位。
- 木台 Collider 已从空引用显式恢复到当前 `Sp_l83_tz_01` 的 `BoxCollider2D`。进入剑术玩法时，`Level81ActorCtrl` 只追加 `FreezePositionX` 约束，保留 Y 轴重力；退出玩法、Reset、Disable 和炸飞前恢复原约束，避免角色在台面左右漂移且不阻断击飞。
- 水果由 `0.55` 放大到 `0.66`；刀锋额外世界半径为 `0.15`，拖尾宽度为 `0.30`，切果粒子倍率为 `0.80`。切线会先夹入当前网格投影区间，再复核较小半片至少占 `18%` 面积，避免只削下一点。
- 首切生成物保存当前运行时 Mesh、材质颜色、汁液颜色和剩余切割层数；每个半片可再切一层。二切使用当前 Mesh，不会让已切除区域重新出现，只生成新碎片和同色特效，不增加水果数、基础分或轮次评价；源片和子片继续走独立 Mesh/Material 释放链。
- 挥刀 Spine 播完后按暴露参数保持最后一帧，当前为 `1.5s`；新手势可以打断等待并立刻播放下一条不重复挥刀，暂停期间保持计时不推进。
- 图片锁命中不再使用整张含锁链图片 Bounds，而是使用可调的锁体区域 `offset=(0.08,-0.39) / size=(1.05,1.15)` 并叠加刀宽。锁片以 `1.5` 侧向速度、`52deg/s` 轻旋转和 `0.6` 重力分离，停留 `0.75s` 后在 `0.8s` 内渐隐。
- 炸弹命中只冻结刚体、Collider 和火花当前帧，不隐藏 Renderer、不销毁物体，也不播放切果喷溅。相机在 `0.15s` 内同时缩放并把中心移到炸弹；随后使用显示关卡 67 实际映射 `Level_050.PlayKnock()` 的 `GrenadeExplosionFire + rocketExplosion + (-0.5,0.7)*50`，再走本关已有失败门禁。Cleanup 同时恢复相机初始位置和尺寸。
- 影响文件仅为 L081 私有脚本、`Level_081.prefab`、`GamePlayPanel_081.prefab` 及其内部 HUD 子树、本关两份文档；共享 Actor、物理材质、相机、基础 GameplayPanel、Level050/067 和其它关卡均未修改。
- 回滚本轮体验修正时，只需撤销上述 L081 私有脚本字段/逻辑与 Prefab 参数/资源绑定；不得回滚用户当前木台 `(0.02,-2.42,0)`、`bubble (0,-0.559,0)` 或其它场景 Transform。

## 抛物轨迹与炸弹停顿修正（2026-08-31）

- 每轮随机选择左右其中一侧作为首发，随后按左右交替生成水果；炸弹若出现则沿用序列中的下一侧。左侧起点位于 viewport `0.12~0.26`，右侧自动镜像为 `0.74~0.88`，共同落向中部 `0.42~0.58`，避免旧逻辑从中间向外抛或同轮全部挤在一侧。
- 炸弹失败链把镜头聚焦与额外定格拆成两个独立等待阶段：先等待并收束聚焦 Tween，再完整定格 `1s`，之后才爆破击飞；暂停期间两个阶段均不累计。
- 本轮只修改 `Level81Ctrl.cs`、`Level_081.prefab` 与本关两份说明文档；回滚时恢复上述四个 viewport 参数、`bombFreezeDelay` 和左右交替调用即可，不涉及其它关卡或策划 Transform。

## HUD 本地化、手势切片与参数修正（2026-08-31）

- 旧 HUD 由 `Level81GameplayHud` 动态创建 Image/TMP，并把本地化标签和数字拼在同一个富文本中，策划无法在 Inspector 独立挂 I2 组件或调整标签尺寸。现改为 `GamePlayPanel_081.prefab` 直接内置的 `GamePlayPanel_081.prefab/Root/Level_081_HUD`：三个标签实体自带 I2 `Localize`，两个数字实体由代码更新，评价 Image 保留策划设置的 RectTransform 尺寸；运行时只查找、显示和复位该 authored 实例，不创建或销毁它。
- 新手势创建 Trail 时执行“停止发射 -> 移动到新起点 -> Clear -> AddPosition 播种 -> 恢复发射”，正常抬手仍保留 `trailLifetime` 尾光，不会把上次终点连接到本次起点。
- 每次手势开始快照当时存在的 `SliceResult`。完整水果在本手势切成两半后不会被同一手势的下一采样帧继续切碎；下一次独立手势仍可按 `additionalFruitSliceDepth` 二切且不增加分数或评价计数。
- `targetScore` 默认值和 Prefab 序列化值改为 `30000`；`swingHoldDuration` 默认值和 Actor 实例值改为 `1.5s`，两项仍可在 Inspector 单关调整。
- 该历史补丁当时使用 Unity 6000.0.59f2 完整 `Assembly-CSharp.rsp/rsp2` 编译到 `Temp/Assembly-CSharp.Level081HudGestureVerified.dll`：`exit 0 / 0 error / 107 warning / 0 L081 diagnostic`；当时独立 HUD 为 `45/96`、GamePanel Variant 为 `22/40`。当前已按下方“HUD 资源内联调整”合并为单一 `GamePlayPanel_081` 的 `65/135`；未启动 Unity 或 PlayMode，布局、字体、本地化切换、拖尾断点和单手势碎片数仍需运行态验收。
- 该历史补丁若整体回滚，应按对应提交恢复当时的 HUD 绑定/实现，同时撤销手势候选快照与 Trail 新起点播种；当前内联结构的独立回滚方式以下方“HUD 资源内联调整”为准，不回滚木台、泡泡或其它场景 Transform。

## 编辑器预览补丁（2026-09-01）

- `GamePlayPanel_081.prefab/Root/Level_081_HUD` 的 `Target_Key` Image 现在预置标准 `Sp_key_1_01` Sprite（GUID `6753875a5a7169444b2b7b9b6dd2b3bf`），打开 `GamePlayPanel_081` 时即可看到完整目标钥匙图；运行时仍保留由本关 Key 实例同步 Sprite 的绑定路径。
- 本次只补 authored HUD 资源，不改共享 Key、基础 GamePlayPanel 或其它关卡。

## Level113 水果起点避锁调整（2026-09-01）

- 显示关卡 `113` 由 `tblevelsort.json` 映射到 `10081 / Level_081`，本次只调整该关卡的水果/炸弹发射参数，不新建 `Level113` 目录，也不改变切水果、切锁、炸弹失败或结算流程。
- 左侧起点改为 viewport `x=0.34~0.40`、右侧继续镜像为 `0.60~0.66`；起点 `y` 仍为 `-0.08`，因此物体在屏幕外、截图红框的竖直下方出现。初速度仍由目标时长和重力反算，方向保持向上并向画面中部汇聚，路径与右侧门锁拉开距离。
- `Level81Ctrl.cs` 默认值与 `Level_081.prefab` 序列化值已同步；既有左右交替生成、炸弹追加、回收阈值和 Inspector 可调能力保留。
- 使用 Unity 6000.0.59f2 当前完整 `Assembly-CSharp.rsp/rsp2` 对最新源码编译到 `Temp/Assembly-CSharp.Level113SpawnVerified.dll`，结果为 `exit 0`；该编译未覆盖 Unity 的 `Library/ScriptAssemblies`。
- 尚未启动 Unity PlayMode；宽高比、首次入屏位置、门锁安全距离和移动端触控观感仍需运行态确认。

## 回滚路径

默认回滚只撤销 `Level_081.prefab` 中本次新增的 `Level81Ctrl`、本关 Actor 子类、Key/DoorLock 和出口门门禁接线，恢复修改前的本关 Prefab 副本；策划已摆放的场景、泡泡、图片锁和 L81 图片继续保留。确认本关整体放弃且无其它引用后，才移除 `Level_81` 私有脚本/文档和本次新增的 CoconutExplosion 7 个资产及两个新目录 `.meta`。不得删除或回滚已存在并被其它特效复用的 `circle/cloud/glow` 公共资产。共享 `BaseLevelCtrl`、Actor、GameplayPanel、Key、DoorLock、Level004 和 Level067 均无需回滚。


## HUD 资源内联调整（2026-09-01）

- 需求背景：`GamePlayPanel_081` 已经是通用 `GamePlayPanel` 的本关 Variant，独立 HUD 资源只被该 Variant 单处引用，不承担跨面板复用；按当前要求移除额外资源层级，由 Variant 直接承载 81 关 HUD。
- 影响范围：删除 `Assets/Game/Prefabs/Level/Level_081_HUD.prefab` 及其 `.meta`；将原 HUD 根节点、TMP、I2 Localize、Image、CanvasGroup、Sprite 和 `Level81GameplayHud` 绑定内联到 `GamePlayPanel_081.prefab/Root/Level_081_HUD`。仅修正 `Level81GameplayHud.cs` 中已经与资源事实冲突的两处注释，不改运行时代码。当前 Unity 会话重序列化 Variant 时同时写回了三个 I2 标签的预览文本、对应 TMP 当前材质和 Reward 标签位置；这些值按当前编辑器状态保留，未当作运行逻辑改写。
- 行为基线与结果：调整前由 `GamePlayPanel_081` 以 nested Prefab 引用独立 HUD；调整后 HUD 是 Variant 的直接新增子树。关卡仍通过 `gamePlayPanelName=GamePlayPanel_081` 创建面板，`Level81Ctrl.EnsureHud()` 仍以 `GetComponentInChildren<Level81GameplayHud>(true)` 查找同一组件类型，分数、目标、钥匙图、三档评价、显隐、暂停和 Reset/清理 API 不变。
- UI/资源流：通用面板继承关系仍为 `GamePlayPanel_081 -> GamePlayPanel.prefab`；本关 UI 不新增加载、实例化、事件、存档、配置、打点或平台链路，删除的 HUD GUID 已从 `Assets` 引用中清零。
- 静态验证：`GamePlayPanel_081.prefab` 为 `65` 个 YAML 定义、`135` 次本地引用、`65` 个唯一引用目标、`0` 重复定义、`0` 悬空引用；基础 Variant GUID 保留，独立 HUD GUID 为 `0`，`Level81GameplayHud` 脚本和 HUD 根节点各唯一一次。Unity 已成功重新导入最终 Prefab；日志中出现过一次删除旧 nested 资源与写入新结构之间的瞬时 Missing Prefab，随后三次最终导入均成功。
- 未验证风险：Unity Editor 导入、Prefab Mode 层级显示、I2 多语言切换、SafeArea/多分辨率布局以及泡泡进入、计分、评价、Reset/Replay 的 PlayMode 结果仍为 `uncertain`；静态 YAML 闭合不能替代运行态验收。
- 回滚路径：通过版本控制恢复独立 HUD prefab/meta，将 `GamePlayPanel_081` 的直接 HUD 子树恢复为 nested Prefab 引用，并恢复相应注释与文档；无需修改共享 `GamePlayPanel`、Level081 玩法代码、配置或场景 Prefab。
