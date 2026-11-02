# Level079 风险评估

更新时间：2026-07-29

## 原始行为基线

- `Level_079.prefab` 是策划复制并继续编辑的未跟踪关卡资源，已有地形、Actor、StartDoor、door_0、DoorLock、石像、圆盘和左上机关视觉。
- 根节点已有 `BaseLevelCtrl`，继续使用标准 `GamePlayPanel_000`。
- 石像只有 Collider，没有 Rigidbody2D 或滑动旋转逻辑；圆盘与机关没有触发逻辑。
- 假钥匙只有显示组件，原本不可交互；真钥匙节点不存在。
- door_0 的成功 Trigger 初始关闭，场景没有能触发现有 DoorLock 的钥匙流程。

## 影响文件与资源

- `Assets/Game/Prefabs/Level/Level_079.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_79/Level79Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_79/Level79Ctrl.cs.meta`
- `Assets/Game/Scripts/Level/MainLevel/Level_79/Level79功能说明.md`
- `Assets/Game/Scripts/Level/MainLevel/Level_79/Level79风险评估.md`
- `Assets/Game/ArtSrc/Sprites/L79/` 中策划提供的 8 帧石像与圆盘资源
- `DevelopMD/已有功能设计与使用记录.md`

## 使用的既有系统

- `BaseLevelCtrl`：保留 Actor 移动、失败、进门和成功判定。
- Actor 与 2D Physics：角色通过碰撞推动 `Stuff` 层的 Dynamic 石像。
- `InputUtils.IsPointerOverUI(...)`：排除从 UI 开始的世界手势。
- `SwitchGameStateEvent` / `GameResetEvent` 与 `PlayCtrl.CheckState(...)`：限制非 Playing 输入并清理重开。
- DOTween：石像钥匙显隐、飞锁、机关移动/晃动和相机震动。
- `BaseItemExecutor` / DoorLock：复用标准开锁音效、Spine 动画和销毁锁流程。
- door_0 / `BaseLevelCtrl.OnActorTrigger2D`：复用标准出口成功链。

没有修改上述共享实现。

## 风险项与控制

| 风险 | 当前控制 | 验证状态 |
| --- | --- | --- |
| 修改共享系统影响其它关卡 | 新逻辑全部位于 Level079 私有组件和 Prefab；共享脚本、Prefab 与事件定义未改 | 静态通过 |
| 石像横滑同时被直接拖走 | 不挂 `DragableItem`；滑动只替换 Sprite，位移只来自 Actor 物理推动 | 代码/Prefab 静态通过，手感待 PlayMode |
| 8 帧方向颠倒或越界 | 固定 8 项数组；右滑 `+1`、左滑 `-1` 并循环取模；GUID 顺序逐项核对 | 静态通过 |
| 斜滑、轻触或多指误旋转 | `48px` 分段阈值、`1.25` 横向主导、认领 `fingerId`，其它触点忽略 | 代码静态通过，真机待验证 |
| 真钥匙被石像手势抢走 | Begin 时先检测真钥匙 bounds，再检测石像 Collider；松手再次校验 bounds 和 `18px` 位移 | 代码静态通过 |
| 透明或非 05 帧钥匙仍可点 | 仅零基索引 `4` 且 Alpha 至少 `0.35` 时接受点击；离开 05 立即将 Alpha 归零，04/06 不保留渐隐尾帧 | 代码静态通过，快速滑动待验证 |
| 假钥匙被玩家取得 | 假钥匙仅有 Transform 和 SpriteRenderer，无 Collider、MonoBehaviour 或点击检查 | Prefab 静态通过 |
| 圆盘在接触边缘过早触发或频繁开合 | 同时校验石像 Collider 中心 X 和底部 Y；进入使用 `0.72 / 0.6`，已进入时退出容差额外增加 `0.2` 形成滞回 | 参数待 PlayMode |
| 石像命中圆盘后被瞬移或锁死 | 圆盘只持续读取占用状态，不改 Transform、速度或 Rigidbody2D 约束；石像始终由 Actor 物理推动 | 代码静态通过，手感待 PlayMode |
| 石像推出后机关没有合回 | `FixedUpdate` 持续检测占用变化；进入时打开并震屏，退出时回到缓存初始位置，再次进入可重新打开 | 代码静态通过，边界待 PlayMode |
| 机关开合途中快速反向导致漂移或残留旋转 | 状态变化时 Kill 旧 Sequence、恢复初始旋转，再从当前位置移动到基于初始坐标计算的绝对目标 | 代码静态通过，连续反向待 PlayMode |
| 机关移动和抖动争写同一属性 | 位移只写 localPosition.x，晃动只写 localRotation | 代码静态通过 |
| 相机震动后位置残留 | 缓存初始 localPosition；完成、Kill、Disable、Destroy 都恢复 | 代码静态通过，运行时待验证 |
| 暂停或入场期间仍可交互 | Update、FixedUpdate、按下和飞锁入口均检查实际 `PLAYING`；事件暂停清手势并暂停 Tween | 编译/静态通过 |
| 移动端切后台残留触点 | ApplicationFocus 丢失或 ApplicationPause 时清除 owner；Canceled/触点丢失同样取消 | 代码静态通过，真机待验证 |
| 飞锁途中重复取钥匙、石像无法继续旋转、暂停时提前开门或错误恢复 Actor | 接受点击后锁存 `_keyFlying/_unlocking` 并关闭 BaseLevelCtrl 的角色移动；钥匙状态继续阻止重复点击，但本关 `_actorLocked` 允许石像滑动单独通过。非 `PLAYING` 时仍清手势并等待，恢复游玩后才开放门并按原状态恢复角色 | 代码静态通过 |
| 飞锁效果与其它关卡不一致 | 复用公共 Key 的相对时序：缩至起飞尺寸 `50%`，缩放占飞行 `80%`，飞行 `40%` 后开始以 `50%` 时长线性渐隐；位置、缩放和 Alpha 共用一个 Sequence | 代码静态通过，画面待 PlayMode |
| 锁动画异常造成永远不能进门 | 等 DoorLock 销毁，最长 `2s`；超时告警后仍启用出口 | 异常注入待验证 |
| 重开时旧异步流程继续开门 | 会话版本使旧 Tween/UniTask 回调失效；GameReset 立即设为暂停并清理；PlayCtrl 随后销毁重建关卡 | 代码与重建链静态通过，PlayMode 待验证 |
| 不同石像帧尺寸导致碰撞轮廓变化 | 物理 Collider 固定在石像根节点，不随 Sprite 重建，避免每帧物理抖动 | 静态通过，视觉贴合待验证 |
| 正式入口不可达 | 当前生成配置无 `10079 / Level_079`，本次没有未经确认选择排序号 | 待配置 |

## Prefab 与资源残留

- Level_079 有 150 个 YAML 对象定义，无重复 fileID；303 次本地非零引用均闭合。
- 8 张石像 Sprite 的 GUID 与本关 `.meta` 一致，顺序为 01 到 08。
- door_0 实际根 Collider、DoorLock 和 LockPoint 引用闭合。
- 复制场景保留 7 条指向 door_0 源 Prefab 已不存在 fileID 的孤儿 override。Unity 会忽略它们，当前不影响实际根 Collider，但属于未清理序列化残留。
- inactive 的 `Sp_prop_1_09` 引用一个没有 `.meta` 的 Sprite GUID，并标记为未分配。该节点未接入 Level079；为避免误删策划复制内容，本次未处理。

## 验证证据

- Prefab 本地引用扫描：150 个定义、0 重复；303 次非零本地引用、0 缺失。
- 序列化类型核对：Level79Ctrl 全部对象字段指向对应类型；脚本 GUID 与 `.meta` 唯一匹配。
- 真钥匙节点仅 Transform + SpriteRenderer，初始 active、Alpha 0、SortingOrder 301；石像显示为 SortingOrder 300。
- 假钥匙节点仅 Transform + SpriteRenderer，无交互组件。
- 石像 Rigidbody2D 为 Dynamic、mass 10、gravityScale 2、Interpolate、Continuous、FreezeRotation。
- door_0 根 Trigger 初始 disabled，DoorLock 根含 `BaseItemExecutor`，LockPoint 是 door_0 直接子节点。
- Unity 当前 Prefab GUID 已由 `PrefabImporter` 成功导入；最近相关日志无 YAML、Missing Script 或导入失败。
- Unity 6000.0.59f2 Roslyn 全量编译最新脚本为 `0 error`，未产生 Level079 warning。
- 静态扫描确认 Level79Ctrl 没有 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、Instantiate、analytics、广告、IAP 或平台 SDK 调用。

## 未验证缺口

- 未运行最新 Level079 Unity PlayMode，不能宣称石像推动、圆盘容差、机关演出、真假钥匙、飞锁和完整通关已通过画面验收。
- 未验证手动重开、失败重开、退出、Replay、暂停/恢复和演出中断的完整组合。
- 未验证 DoorLock 缺引用、动画不销毁或超时等异常注入。
- 未验证 Android/iOS 横屏触摸阈值、多指、Touch Canceled、切后台恢复、不同宽高比和 60 fps。
- 生成配置没有 Level079 正式入口；排序号、胜利文案和提示配置仍待策划确认。
- Prefab 的孤儿 door override 与 inactive 断 Sprite 残留尚未在 Unity Inspector 中清理或确认是否可删除。

## Android / iOS 横屏验收重点

- 单指左右慢滑、快速滑、反向滑和斜滑的帧序与阈值一致。
- 石像进入圆盘、推出圆盘、动画中快速反向和再次进入时，机关状态、震屏次数与石像物理位置都正确。
- 04、05、06 连续切换时，仅 05 有钥匙，04/06 没有 Alpha 残留。
- 第二根手指操作 HUD 时不会抢走石像或钥匙 owner。
- 按住钥匙切后台再回来不会直接触发飞锁。
- 不同宽高比下机关、圆盘、钥匙抛物线和门锁都在安全画面区域。
- 石像 Dynamic 物理、DOTween 和每物理帧常量距离检查不造成明显掉帧或持续 GC。

## 回滚路径

- 回滚 `Level_079.prefab` 中根节点 Level79Ctrl、石像 Rigidbody2D 和 `TrueKey_079` 三类新增接线，恢复策划复制版本。
- 删除 `Level79Ctrl.cs/.meta`；共享 BaseLevelCtrl、Actor、DoorLock、door_0 和 GamePlayPanel 无需回滚。
- 保留策划的 L79 美术和地形资源，除非策划明确要求删除。
- 删除或回退 Level079 本地文档与 `DevelopMD/已有功能设计与使用记录.md` 的追加节。

## 2026-08-05：机关开合音效增量风险

| 风险 | 当前控制 | 验证状态 |
| --- | --- | --- |
| `FixedUpdate` 每帧重复播放 | 播放点位于 `_statueOnPlate` 状态变化后的 `PlayMechanismTransition(...)`，相同状态在进入方法前返回 | 静态调用链通过 |
| 移开只有声音、移回无声音 | 开与关共用同一转场入口，每次真实状态切换都播放 `l89StonePillarDrop` | 静态分支通过，听感待 PlayMode |
| 复制造成 089 音频或配置分叉 | 079 直接复用既有 tag；未改 Sound Collection、生成表、089 脚本或资源 | Git 范围静态通过 |
| 快速反向造成尾音重叠 | 当前保留 one-shot 语义，`1.715s` 音频长于 `0.8s` 移动；若听感不可接受再在 079 私有范围增加停止策略 | 未验证 |
| 重开后声音残留 | 沿用 `PlayCtrl.DestroyLevel()` 的 `AudioManager.StopAllSound()`；本轮未改重开链 | 静态链路通过，PlayMode 待验证 |

Roslyn 编译为 `0 error`，音效调用唯一性和既有 tag 闭合已静态确认。未执行 Unity PlayMode、移动端或外放/耳机听感验证。只回滚本增量时删除 Level79 的常量和一次播放调用，不修改 Prefab 与共享资源。
