# Level081「水果忍者」风险评估

更新时间：2026-09-01

## 范围与证据边界

本关是 Level004 副本上的单关扩展。行为由 `Level81Ctrl`、`Level81ActorCtrl`、`Level81FruitItem`、`Level81GameplayHud`、`Level81SliceMesh`、`Level_081.prefab` 和本关专用 `GamePlayPanel_081.prefab` 本地承载；共享 Actor、BaseLevelCtrl、基础 GameplayPanel、Key/DoorLock 和全局事件实现不在修改范围。

当前结论来自需求、源码、Prefab/资源静态检查和 unitypackage 哈希/GUID 检查。未运行 Unity 前台、PlayMode 或真机，所有画面、手感、物理时序和完整结算结论均标记为 `uncertain`。

## 主要风险

| 风险 | 当前控制与证据 | 验证状态 |
| --- | --- | --- |
| 复制关卡仍运行旧控制器或本关字段漏绑 | 最终 Prefab 中 `Level81Ctrl`、`Level81ActorCtrl` 各唯一 1 次，旧 Level7 GUID 为 0；核心对象/图片/特效均有绑定，`ValidateConfiguration()` 继续提供运行时错误日志 | YAML 静态闭合通过，Unity Inspector/PlayMode 待验 |
| 复制遗留对象继续运行旧玩法或遮挡本关 | 不属于本关的 `Tips / Box / wayPoint / EndPoint / BoxBroken` 在最终 Prefab 均为 0；来源资源未修改 | Prefab 名称和旧控制器 GUID 静态通过 |
| 角色从木台侧面经过也误吃泡泡 | Prefab 显式绑定 `Sp_l83_tz_01` 当前 `BoxCollider2D`；触发同时要求 `Actor.IsGround`、角色非 Trigger Collider 与木台实际接触，并校验角色脚底与台面顶端误差不超过 `0.22`；距离判断只在木台绑定缺失时回退 | 源码与 YAML 静态通过，Collider 接触时序和容差仍需 PlayMode 走位验收 |
| 角色站上木台后被接触求解推得左右滑动 | 进入剑术玩法时仅给 L081 Actor 刚体追加 `FreezePositionX`，保留 Y 重力；退出、Reset、Disable 和炸飞前恢复进入玩法前约束，不修改共享物理材质 | 约束生命周期源码静态通过，低帧率落台和炸飞待 PlayMode 验收 |
| 玩法期间通用移动动画覆盖剑术动画 | Actor Prefab 实例使用本关 `Level81ActorCtrl`；剑术模式下跳过通用 `OnUpdate/HandleAnimators` | 源码静态通过，Spine 实播待验 |
| 首次挥剑索引越界、后续连续重复、发生混合或过早回 idle | `lastSwingIndex=-1` 时从完整数组随机；后续排除上次索引，TrackEntry `MixDuration=0`；完成后按暴露参数保持末帧，当前 `1.5s`，新手势用版本号打断旧等待，暂停期间不累计 | 边界源码静态通过，动画名/末帧姿势和 1.5 秒节奏待验 |
| 手势从暂停/商店 UI 开始仍切到场景 | 开始手势时调用 `InputUtils.IsPointerOverUI`；HUD 自身关闭 Raycast；暂停、失焦、Touch Cancel 和状态切换统一清手势 | 多 Canvas、刘海屏和真机 Touch 待验 |
| 一次低帧率滑动穿过水果却漏判 | 使用相邻采样之间的世界线段而非单点命中；候选半径额外叠加 `0.15` 世界单位刀宽，拖尾宽度同步为 `0.30` | 低帧率、超快滑动和相机缩放待压力测试 |
| 水果 Sprite 只削掉一点、无法正确切半或二切后区域长回 | 网格裁剪要求 `SpriteDrawMode.Simple` 和有效三角网格；切线投影先夹入 `18%~82%` 区间，再以两侧实际面积复核，必要时回退中心。二切读取当前运行时 Mesh 而非原 Sprite 三角形，且不调用计分/轮次入口 | 当前资源导入形态、凹形 Sprite 切口和连续二切需 Unity 实际导入检查 |
| 一次连续手势在后续采样帧递归切碎刚生成半片，或产生 Mesh/Material 泄漏 | 手势开始快照此前存活的 `SliceResult`，本手势只允许二切该快照；刚生成结果不进入候选，必须抬手后下一手势才能继续切。每半片拥有独立 Mesh/Material，渐隐、`Dispose()` 和 Reset 释放 | 单手势高速往返、同屏多果、下一手势二切和频繁重开需 PlayMode/Profiler 验证 |
| 半片与场景碰撞异常 | 水果半片不加 Collider；锁半片使用按裁剪边界生成的 PolygonCollider2D、Continuous Rigidbody2D | 锁落地、穿透和碰撞层待 PlayMode 验证 |
| 抛物线在宽屏/窄屏飞出可切区域，或再次靠近右侧门锁 | 左侧起点带收进截图红框下方的 viewport `0.34~0.40`，右侧自动镜像为 `0.60~0.66`；每轮仍按左右交替生成，共同向上、向内落入中部 `0.44~0.56`；起点 `y=-0.08` 保持在屏幕外，飞行时长与重力反算 | 16:9、超宽、平板、相机移动、首次入屏位置和实际门锁安全距离待 PlayMode 验收 |
| 炸弹数量影响评价分母 | 炸弹作为额外物体加入轮次，但 `roundFruitTotal` 只统计水果 | 源码静态通过 |
| 轮末评价边界错误 | 只在所有抛掷物 Resolve 后结算；按全部、少 1、少 2 的互斥顺序判断 | 3/4/5 水果各命中组合待用例验证 |
| 达到目标分后同帧又结算奖励分、接受输入或清列表越界 | `completionStarted` 在达标入口置位；`DiscardRoundItems()` 先按索引遍历销毁，再统一 `Clear()`，不会在枚举中移除；后续 `AddScore` 有完成门禁 | 边界源码静态通过，临界分同帧待验 |
| 图片锁锁链区域误触或锁体红框难以命中 | 判定使用独立可调锁体区域 `offset=(0.08,-0.39) / size=(1.05,1.15)`，不再使用整张含锁链 Sprite Bounds，并叠加 `0.15` 刀宽；锁片降低重力、增大侧移、轻微旋转并延长渐隐 | 区域与截图静态换算基本一致，实际红框手感和碎片碰撞待 PlayMode 验收 |
| 直接切锁误被水果遮挡 | 每段切线优先检查图片锁，命中后立即结束玩法；这是需求允许的替代通关路线 | 交互优先级需策划确认手感 |
| 同一刀同时穿过图片锁和炸弹 | 当前处理顺序为图片锁优先：锁切片成功后立即通关，不再检查同一线段上的炸弹 | `uncertain`，需求未定义冲突规则，已请求策划确认“锁优先”或“炸弹优先” |
| 达标开锁在暂停后台推进或卡死 | 本关不启动共享 Executor；Key 自带 `MoveFloatHandle` 被停止并禁用，钥匙从 authored 初始位置显示，镜头先聚焦并停留，再沿弧线飞向 `door_0/LockPoint`；弧线、DoorLock `unlock` Spine 和最长等待仅在 `PLAYING` 时推进，暂停时同步冻结 Spine，完成或超时后再开放出口 | 源码静态通过；真实 Spine 完成回调、音效、LockPoint 构图和各阶段 Pause/Resume 仍需 PlayMode 验收 |
| Key 演出位置或时序不符合策划预期 | Key Collider 始终关闭；流程显式缓存并恢复 authored 初始 Transform，依次执行显示、相机聚焦、`keyFocusHoldDuration` 停留、飞向门锁、`getKey` 和 DoorLock 解锁；镜头在飞行期间恢复 | `uncertain`，需视觉和时序验收 |
| 切中炸弹后仍能继续切、移动、打开其它 UI 或看到局部表现继续播放 | 失败入口同步关闭玩法和手势，通过既有 GameplayPanel 点击遮罩阻断暂停/商店/提示/移动按钮，并冻结抛掷物、切片、HUD、Actor 刚体/剑术 Spine、本关运行时粒子及刀光拖尾；聚焦冻结与暂停原因叠加 | 源码静态通过，真实多指同帧、遮罩层级与场景内第三方动画仍需 PlayMode 待验 |
| 炸弹聚焦时炸弹消失/移动，或相机只缩放不朝炸弹 | 炸弹命中只 Resolve Collider/刚体并停住火花当前帧，不隐藏 Renderer、不 Destroy、不播放切果喷溅；相机 Sequence 同时修改正交尺寸和 XY 中心到炸弹位置 | 源码静态通过；视觉中心、0.15s 节奏和炸弹火花停帧待 PlayMode 验收 |
| 炸弹聚焦顺序错误、1 秒定格未完整执行或相机不恢复 | 角色物理先冻结；聚焦阶段与定格阶段分开等待，达到 `bombFocusDuration` 后收束仍存活的 Tween，再独立累计 `bombFreezeDelay=1s`，随后恢复角色物理并爆破；Reset/Disable/Destroy Kill 聚焦 Tween 并恢复缓存的正交尺寸与世界位置 | 非正交相机、外部相机移动/缩放并发和 1 秒定格节奏待 PlayMode 验收 |
| 爆破击飞参考错误、关卡不结束、暂停后停住或失败事件失效 | 玩家显示关卡 67 经 `tblevelsort 67 -> 10050 -> Level_050`；本关复用其提前发射链的 `GrenadeExplosionFire + rocketExplosion + (-0.5,0.7)*50`，方向不归一化。爆破同帧先派发 `OnTriggerTrapEvent`，随后 Knockback；失败事件单次门禁，暂停时缓存/恢复刚体速度 | 配置与源码静态通过；完整失败、击飞角度/力度、暂停/恢复、广告重开和 Replay 待验 |
| CoconutExplosion 仍显示椰子图片 | 原包 Prefab 保留不改；运行时按名字禁用 `Gibs`，其余粒子按水果色改色 | 名称大小写已忽略；真实粒子层级/材质颜色待验 |
| CoconutExplosion 自带演示音效绕过项目音效开关 | Level081 实例化视觉特效后立即停止并禁用其全部 `AudioSource`；不修改插件 Prefab | 源码静态通过；项目正式切果音效仍待策划决定与接入 |
| Epic Toon FX 导入覆盖项目公共资产 | 只新增项目缺失的 7 个资产；已有同路径同 GUID 的 circle/cloud/glow 全部复用且未覆盖 | 哈希、GUID 和 Git 状态静态通过 |
| 暂停或 Reset 后旧异步开锁继续写状态 | 使用 `sessionVersion` 和 Destroy CancellationToken；Reset/清理递增版本并清资源，异步每次恢复后复核会话 | 各开锁阶段 Pause/Reset/Replay 待验 |
| Reset/Disable 后同实例残留玩法展示或状态 | `resetOrReplayReceived` 在旧实例销毁窗口内阻断 Update、状态事件和异步续体；复用时恢复 bubble、图片锁、DoorLock、Key/门碰撞门禁、钥匙初始 Transform，并清零本局 flags；存活角色退出剑术模式 | 源码静态通过；标准 Key/DoorLock 内部运行状态和死亡角色无法同实例复活，仍需运行态确认 |
| 炸弹演出在暂停面板后偷偷推进失败 | 炸弹等待使用 `Time.unscaledDeltaTime`，但仅在 `PLAYING` 累计；暂停期间等待保持，恢复后继续；失败事件设置单次派发门禁 | 源码静态通过；暂停/恢复和异步销毁组合待 PlayMode 验证 |
| 暂停恢复后角色重新能跳、移动或击飞速度被清零 | 共享 `BaseLevelCtrl.GameResume()` 会调用 `Actor.ResumeMoving()`；普通剑术阶段由本关在状态事件与下一帧恢复入口重新清空输入并 `StopMoving()`，直到开锁路线完成；炸弹失败态执行“缓存击飞速度 -> StopMoving 重压 -> 写回速度”，兼顾控制锁与持续飞出 | 事件订阅先后、击飞首帧暂停与真机同帧输入待 PlayMode 验证 |
| 暂停期间喷溅粒子继续播放或提前销毁 | 运行时特效由 Level081 记录粒子播放状态和剩余寿命；暂停只冻结当时播放中的粒子且不扣寿命，恢复后按记录继续，Reset/Disable/Destroy 统一销毁 | 多次快速暂停、粒子自然停止边界和移动端性能待 PlayMode/Profiler 验证 |
| 可编辑 HUD 遮挡共享按钮、I2 标签被数字更新覆盖或残留到下一局 | `GamePlayPanel_081.prefab` 直接内置 `Root/Level_081_HUD`；标签/数字拆为独立 TMP，代码只写两个数字。根 CanvasGroup 与所有 Graphic 不拦截 Raycast，Disable/Destroy/Reset 只复位预置实例，不销毁 authored HUD | SafeArea、层级顺序、三种语言尺寸、编辑器预览和快速重开待 PlayMode 验收 |
| 目标分 30000 导致流程过长 | `targetScore` 仍暴露，当前默认每水果 5 分并有轮奖；策划可临时降低目标做回归 | 达成时长和难度曲线待策划测算 |
| 正常主线无法进入 Level081 | 当前 `tbleveldata.json` 已有唯一 `10081 / Level_081`，`tblevelsort.json` 已有对应排序项；资源收集与实际入口仍依赖配置流程 | `uncertain`，需配置收集与 PlayMode 确认 |

## 数据、事件与平台风险

- 本关不写 DB、不改货币、不直接发奖、不新增资源远程加载或 analytics；分数只存在当前 Level081 实例内。
- 成功和失败最终依赖共享 BaseLevelCtrl；达标路线由本关直接编排“钥匙聚焦/飞向 LockPoint/标准 DoorLock Spine 与音效”并开放出口，若 DoorLock 动画资源、LockPoint 绑定或出口 UnityEvent 接线错误，可能出现视觉/音频不完整或无法结算。
- 输入同时包含 Touch 和仅编辑器启用的 Mouse 分支；Windows 编辑器结果不能替代 Android/iOS 触控验证。
- `CoconutExplosion` 是多 ParticleSystem 特效，每个水果切开都会实例化，连续高频切割可能产生移动端 CPU、Overdraw、音源和 GC 峰值，应以低端机 Profiler 为准。
- `GamePlayPanel_081` 中预置 HUD 的 `Reward / Target / Score` 实体通过 I2 Localize 绑定现有 L081 `_1 / _2 / _3` 词条，代码不再调用 `LocalizationManager` 或提供英文兜底；各语言译文长度、大小写、可编辑 RectTransform 和编辑器预览适配仍需多语言 PlayMode 验收。

## 已完成验证

- 阅读并静态核对 5 个本关脚本，确认核心状态门禁、事件解绑、异步会话失效和运行时资源释放入口存在；另核对 `GamePlayPanel_081` 的内置 HUD 和 `Level81Ctrl.EnsureHud()` 仅查找/复位路径。
- 最终 Level081 Prefab 为 `184` 个唯一 YAML 定义、`368` 次纯本地非零引用（覆盖 `184` 个唯一目标）、`0` 重复、`0` 悬空、`0` Missing Script；两个本关脚本 GUID 和核心绑定均唯一，旧控制器及五类复制残留为 0。出口门上从 Level007 复制来的空目标监听器及其事件执行器已仅在 L081 内移除，避免无效执行与销毁后的全局事件残留。
- 主关卡 Prefab 当前有 `65` 个唯一外部 GUID；内联并由 Unity 重序列化后的 `GamePlayPanel_081` 有 `16` 个唯一外部 GUID、`65` 个定义和 `135` 次本地引用，HUD 脚本与全部 UI 绑定均在该 Variant 内闭合。8 张水果、炸弹主体/两帧火花、HUD 节点、5 张 HUD 图、I2/TMP/Image 组件、CoconutExplosion、显示关卡 67 提前发射用 `GrenadeExplosionFire`、Key、`door_0/LockPoint` 和 DoorLock 均有静态引用。
- 核对 unitypackage：新增资产和 `.meta` 与原包 SHA256 一致，7 个业务 GUID 与 2 个新目录 GUID 均唯一。
- 核对 CoconutExplosion：22 个本地引用全部闭合；Prefab 及相关材质外部 GUID 可解析；新增脚本类型全项目唯一。
- 第二轮源码审查确认水果只有网格切片成功才 Confirm/计分、炸弹先进入标准陷阱链再击飞、Pause Resume 会重压 Actor，且 TrailRenderer 显式使用 `SceneLayer / order 1000`。
- 第三轮源码静态检查确认炸弹等待按“完整聚焦时间 + 额外停顿”累计，运行时粒子暂停期间不推进寿命，击飞刚体速度具备同帧缓存、逐帧刷新和共享 `StopMoving()` 清零后的恢复路径。
- 2026-08-31 使用 Unity 6000.0.59f2 当前完整 `Assembly-CSharp.rsp` 与 `rsp2` 编译包含最终木台判定、炸弹聚焦冻结、失败 UI 门禁和复用状态清理的源码到 `Temp/Assembly-CSharp.Level081FinalVerified.dll`，退出码 `0`，无 L081 诊断或编译错误；既有 `Library/ScriptAssemblies/Assembly-CSharp.dll` 未被该验证覆盖。
- 本轮体验修正完成后再次以 Unity 6000.0.59f2 的 Roslyn 和当前完整响应文件后台全量编译：`exit 0 / 0 error / 107 warning / 0 L081 diagnostic`；独立输出位于 `Temp/Assembly-CSharp.Level081FinalVerified.dll`，未覆盖 `Library/ScriptAssemblies`。
- 最新 Prefab 静态闭包为 `184` 个唯一定义、`368` 次本地引用、`0` 重复、`0` 悬空、`0` Missing Script、`65` 个唯一外部 GUID；内联后的 `GamePlayPanel_081` 为 `65/135`、`0` 重复、`0` 悬空，且不再依赖独立 HUD 资源。两轮额外只读审查未发现阻断项。
- 已移除 L81 目录的 `Thumbs.db.meta` 并把被 `.gitignore` 覆盖的二进制缓存移出 Unity `Assets`，避免把 Windows 缩略图缓存作为关卡资源导入。
- 搜索 `Assets/Game/Configs` 已发现唯一 `10081 / Level_081`：`tbleveldata.json` 的 `id/path` 与 `tblevelsort.json` 的排序项均存在；正式资源收集、运行时入口和线上发布仍为 `uncertain`。
- 仓库指示的 `specs/037-remote-resource-loading/plan.md` 缺失，因此相关计划依据为 `uncertain`，未据此推导本关行为。
- 2026-08-31 将抛物改为“底部左右交替向中部汇聚”，并把炸弹聚焦完成后的额外定格改为 `1s`；源码、Prefab 参数和本关说明已同步，视觉轨迹与节奏仍待 PlayMode 验收。
- 2026-09-01 显示关卡 113（实际资源 `10081 / Level_081`）将起点从 viewport `0.12~0.26` 调整为红框下方中心带 `0.34~0.40`（右侧镜像 `0.60~0.66`），落点收窄为 `0.44~0.56`；`spawnViewportY=-0.08` 仍保证水果从屏幕外竖直下方进入，降低误划右侧门锁的机会。代码默认值与 `Level_081.prefab` 已同步。
- 本次只修改 L081 私有控制器默认参数、关卡 Prefab 和本地说明；未运行 Unity PlayMode，轨迹构图、门锁距离、宽屏适配和移动端手感仍为 `uncertain`。
- 最新完整 Unity Roslyn 编译输出 `Temp/Assembly-CSharp.Level113SpawnVerified.dll`，退出码 `0`；Prefab 闭包扫描为 Level081 `184/184/0/0`（定义/唯一/重复/悬空），无 Missing Script。该证据不替代 Unity PlayMode 与真机验证。
- 上述轨迹与定格补丁使用 Unity 6000.0.59f2 当前完整 `Assembly-CSharp.rsp/rsp2` 后台全量编译到 `Temp/Assembly-CSharp.Level081TrajectoryVerified.dll`：`exit 0 / 0 error / 107 warning / 0 L081 diagnostic`；Prefab 复核为 `184` 个定义、`368` 次本地引用、`0` 重复、`0` 悬空、`0` Missing Script，且木台/泡泡 Transform 未改变。
- 2026-08-31 HUD/手势/参数补丁当时后台全量编译到 `Temp/Assembly-CSharp.Level081HudGestureVerified.dll`：`exit 0 / 0 error / 107 warning / 0 L081 diagnostic`；当时独立 HUD 为 `45/96`、GamePanel Variant 为 `22/40`。当前已内联合并为 `GamePlayPanel_081` 的 `65/135`，视觉和交互结论仍为 `uncertain`，待 PlayMode 验收。

## 仍需验证

- 本次生命周期补丁已完成源码静态检查：`GameResetEvent` 旧实例闸门、Disable/Enable presentation 复位、钥匙初始 Transform 恢复、炸弹 Tween 释放和暂停等待均已覆盖；预置 HUD 的同实例复位也已覆盖，尚未在 Unity PlayMode 验证同实例重启。Reset/Disable/Destroy 在成功开门前会调用 `InitializePresentation()`，立即复位 Key、DoorLock、图片锁、bubble、门碰撞门禁和相机；预置 HUD 只复位、不销毁。
- `ActorCtrl.IsDied` 的运行状态属于共享基类私有状态，本关不修改它；若产品要求不销毁对象的失败后重置，需要由共享生命周期或重新实例化流程另行提供 reset API。

1. Unity 完成脚本刷新后，仍需在 Inspector/PlayMode 确认没有导入期 Missing Script 或序列化重映射，且 Actor 运行时实际使用 `Level81ActorCtrl`；YAML 静态绑定与引用闭合已经通过。
2. PlayMode 分别覆盖泡泡进入、3/4/5 水果轮、三档评价、目标分达成、直接切锁和炸弹失败。
3. 在抛物、评价、Key 弧线、DoorLock 解锁和炸弹聚焦各阶段执行 Pause、Resume、Reset、Replay。
4. 检查 Sprite 切口、锁片碰撞、CoconutExplosion 去椰子及八种水果颜色、拖尾和剑术 Spine 动画。
5. Android/iOS 横屏验证单指/多指、UI 起手、超快滑动、异形屏、安全区、低帧率和低端机性能。
6. 按配置流程确认 `10081 / Level_081` 的源表、运行时 JSON、资源收集和线上发布状态一致。

## 编辑器预览补丁（2026-09-01）

- `GamePlayPanel_081.prefab/Root/Level_081_HUD/Target_Key` 已绑定标准 Key 的 `Sp_key_1_01` Sprite，解决 authored GamePanel 预览时钥匙图为空的问题；运行时动态同步仍作为容错保留。
- 静态风险已收窄为 Unity 导入/多分辨率画面待验；本次未触碰共享 UI、Key 或其它关卡资源。

## 回滚

- 默认玩法回滚仅撤销 `Level_081.prefab` 中本次新增的控制器、Actor 子类、Key/DoorLock、出口门门禁和资源绑定，并按版本控制恢复 `GamePlayPanel_081.prefab` 的修改前结构；保留策划已摆放的 Level081 场景、泡泡、图片锁和 L81 图片。
- 特效回滚只删除本次新增的 CoconutExplosion Prefab、fruitslices/splat 纹理材质、音效、`ETFXPitchRandomizer` 及两个新增目录 `.meta`；保留项目原有 circle/cloud/glow 公共依赖。
- 只有明确放弃整关且确认私有脚本/素材无其它引用后，才删除整个 `Level_81` 私有目录或 L81 素材；不修改 Level004 或任何共享控制器。
- 若只需关闭某条路线，优先在 Level081 私有控制器和 Prefab 内调整；不要修改共享 Key、DoorLock、Actor、BaseLevelCtrl 或基础 GameplayPanel。
- 若仅回滚历史 HUD/手势修正，按对应提交恢复专用面板与 HUD 绑定/代码；当前资源内联的单独回滚方式见下方风险记录。不得回滚用户场景 Transform、专用面板之外的共享 GameplayPanel 或其它关卡。


## HUD 资源内联调整风险记录（2026-09-01）

- 原始行为基线：`GamePlayPanel_081` Variant 通过 nested Prefab 挂载独立 `Level_081_HUD.prefab`，运行时由 `Level81Ctrl.EnsureHud()` 查找组件并驱动显示。
- 影响资源：删除独立 HUD prefab/meta；`GamePlayPanel_081.prefab` 直接承载原 HUD 子树；脚本只修正失效注释，不改公共 API、生命周期或数值逻辑。
- 既有系统与行为一致性：继续复用基础 `GamePlayPanel` Variant、`PlayCtrl.CurrentGamePlayPanel` 和 `Level81GameplayHud`；不改变资源加载名称、事件、存档、配置、打点、广告、支付或平台流程。
- 已控制风险：保留原 HUD 全部 fileID、组件类型、I2 词条、数值/评价/Sprite 绑定，只把 HUD 根父级改到 Variant Root；静态结果为 `65 definitions / 135 local refs / 0 duplicate / 0 missing`，独立 HUD GUID 全项目资源引用为 `0`。当前 Unity 会话另写回三个标签的本地化预览文本、TMP 当前材质和 Reward 标签位置，按当前编辑器状态保留，实际多语言与画面仍列入验收缺口。
- 未验证缺口：Unity 重新导入后是否产生 Missing Script/序列化重映射、Prefab Mode 的 Override 展示、PlayMode 的 HUD 显隐/计分/评价/暂停/Reset、多语言和 Android/iOS 横屏布局均为 `uncertain`。
- 回滚：恢复独立 HUD prefab/meta 和 nested 挂载，移除 Variant 内联子树；不回滚共享面板、Level081 场景、控制器或其它关卡资源。
