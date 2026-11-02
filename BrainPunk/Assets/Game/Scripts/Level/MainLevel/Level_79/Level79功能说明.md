# Level079 功能说明

更新时间：2026-07-29

## 需求背景

Level079 使用策划复制关卡中的地形、门和角色基础结构，新增两条互相独立的解谜线：

- 迷惑线：角色把石像推到右侧圆盘后震屏，左上两块机关石分别向左右晃动移开，露出无法取得的假钥匙。
- 正解线：玩家在石像上横向滑动切换 8 张朝向贴图；真钥匙仅在正背面的 `05` 帧快速显现。玩家松手点击真钥匙后，钥匙沿抛物线缩小并渐隐飞向门锁，标准门锁演出结束后开放出口。

## 影响范围

- 关卡 Prefab：`Assets/Game/Prefabs/Level/Level_079.prefab`
- 关卡私有脚本：`Assets/Game/Scripts/Level/MainLevel/Level_79/Level79Ctrl.cs`
- 本关美术：`Assets/Game/ArtSrc/Sprites/L79/`
- 本地功能与风险文档：本目录下 `Level79功能说明.md`、`Level79风险评估.md`
- 复用事实记录：`DevelopMD/已有功能设计与使用记录.md`

本次没有修改共享 `BaseLevelCtrl`、`ActorCtrl`、`GamePlayPanel_000`、输入组件、DoorLock、door_0、其它关卡脚本或 Prefab。没有新增全局事件、存档字段、配置项、资源加载入口、analytics、广告、IAP、RemoteConfig 或平台 SDK 调用。

## 原有行为基线

- 策划已经复制并摆放 `Level_079.prefab` 的地形、Actor、StartDoor、door_0、DoorLock、圆盘、石像和左上机关视觉。
- 根节点原有 `BaseLevelCtrl` 和标准 `GamePlayPanel_000` 配置继续保留。
- 石像原来只有 `Transform + PolygonCollider2D`，没有可推动刚体或横滑换帧逻辑。
- 圆盘和左上机关原来只有显示节点，没有命中、震屏或开合逻辑。
- 左上假钥匙只有 `Transform + SpriteRenderer`，无 Collider 或点击组件。
- 门锁和出口门已经存在，但出口 Trigger 初始关闭；原场景没有真钥匙飞锁入口。

## 当前行为

### 石像推动与假钥匙迷惑线

- 石像根节点位于 `Stuff` 层，使用 Dynamic `Rigidbody2D`，当前质量 `10`、重力倍率 `2`、Interpolate、Continuous，并冻结物理旋转。
- 角色通过现有物理碰撞推动石像；没有给石像挂 `DragableItem`，横滑手势只切换视觉帧，不直接拖动物理位置。
- 每个物理帧只在实际 `PLAYING` 状态下检查石像 Collider 中心与圆盘中心的水平距离，以及石像 Collider 底部与圆盘高度的距离。
- 进入圆盘使用水平容差 `0.72`、垂直容差 `0.6`；进入后退出判定额外增加 `0.2` 容差，避免石像停在边缘时机关反复开合。
- 石像命中圆盘后不会吸附、瞬移、清零速度或冻结 X，仍可由角色继续向任意方向推动。
- 石像进入圆盘时相机位置震动 `0.45s`、强度 `0.18`；左石块向左、右石块向右各移动 `1.45`，持续 `0.8s`，同时进行旋转晃动。
- 石像被推出圆盘后，两块机关石会晃动着合回各自初始位置；再次推入会重新移开并震屏。开合途中反向时从当前位置平滑切换，目标始终基于初始坐标，不累计漂移。
- 假钥匙始终没有 Collider、MonoBehaviour 或私有点击检测，因此移开石块后只能看见，不能取得。

### 八帧旋转与真钥匙

- 石像 8 张 Sprite 按 `01 -> 02 -> 03 -> 04 -> 05 -> 06 -> 07 -> 08` 配置在同一个 SpriteRenderer 上。
- 指针必须从石像自己的 Collider 内按下；移动端认领实际 `fingerId`，其它触点不能接管。
- 右滑每累计 `48px` 前进一帧，序列为 `01 -> 02 -> 03 ...`；左滑每累计 `48px` 后退一帧，初始序列为 `01 -> 08 -> 07 ...`。
- 横向距离至少达到纵向距离的 `1.25` 倍才切帧，降低斜滑或竖滑误触。
- 真钥匙是石像下独立的 `TrueKey_079` 节点，初始 Alpha 为 `0`。只有进入 `05` 时才以 `0.1s` 渐显；离开 `05` 时立即归零，确保 `04 / 06` 不残留钥匙画面。
- 真钥匙优先于石像手势命中。按下和松手都必须落在钥匙 Sprite bounds 内，整段位移不超过 `18px`，并且在松手时才确认点击。

### 飞锁与出口

- 接受真钥匙点击后立即锁定角色移动并阻止重复点击钥匙；石像滑动旋转保持可用，飞锁、开锁以及开锁完成后都能继续切换朝向帧。
- 真钥匙脱离石像父节点，以 `0.55s` 飞到 `door_0/LockPoint`；Y 轴叠加 `4 * 3 * t * (1 - t)` 的抛物线高度。
- 飞行同时将钥匙缩至起飞尺寸的 `50%`，缩放耗时为飞行时长的 `80%`；飞行到 `40%` 时开始渐隐，渐隐耗时为飞行时长的 `50%`。缩放和渐隐从点击时的实际尺寸与 Alpha 接续，不会在半渐显点击时闪回不透明。
- 接受点击时先播放现有 `getKey` 音效；到达后调用 DoorLock 根节点的 `BaseItemExecutor`，复用标准解锁音效、Spine 动画和销毁锁流程。
- 控制器等待 DoorLock 销毁后才启用 door_0 的成功 Trigger；最长等待 `2s`，超时会记录警告并开放出口，避免资源异常造成软锁。若期间进入暂停或其它非 `PLAYING` 状态，会先等待恢复游玩再开放出口。
- Actor 进入启用后的门 Trigger 后，继续走现有 `BaseLevelCtrl` 成功判定和门动画链。

### 输入与生命周期

- 本关交互入口同时受本地暂停标记和 `PlayCtrl` 实际 `PLAYING` 状态门禁，开场、重建等待、暂停、失败或其它非游玩态不能旋转、点击钥匙或触发圆盘。
- UI 上开始的指针不会被世界交互认领。
- Touch Ended 才提交钥匙点击；Canceled、活动触点丢失、应用失焦、移动端切后台、暂停、脚本禁用和销毁都会清除当前手势。
- 暂停会暂停本关钥匙显隐、飞行、机关和震屏 Tween；清理会 Kill Tween、恢复相机初始位置并撤销本关临时角色锁定。
- 飞锁开始时记录原 `ActorCtrlable` 状态；仅在原状态允许控制且关卡已回到 `PLAYING` 时恢复角色，避免暂停或外部演出锁定被本关覆盖。
- 飞锁期间 `ActorCtrlable` 虽由本关关闭，但石像滑动输入单独放行；钥匙的 `_keyFlying / _unlocking / _doorUnlocked` 状态只阻止再次取钥匙，不阻止石像继续旋转。外部系统原本施加的角色锁仍不会被当作本关放行条件。
- `GameResetEvent` 会立即封闭旧实例输入。现有 `PlayCtrl.ResetGame()` 在约 `0.5s` 后销毁旧关卡并重新实例化 Level079，因此新实例从 Prefab 初始状态开始。

## 数据、资源与事件流

- 输入流：Unity Touch 或 Editor Mouse -> 本关私有命中与手势仲裁 -> 石像换帧或真钥匙点击。
- 物理流：Actor 现有碰撞 -> 石像 Dynamic Rigidbody2D -> 本关圆盘位置容差检测。
- 表现流：DOTween -> 真钥匙 Alpha、钥匙抛物线/缩放、机关石开合/晃动、相机震动。
- 开锁流：真钥匙到达 LockPoint -> 现有 DoorLock `BaseItemExecutor` -> DoorLock 销毁 -> 启用 door_0 Trigger -> `BaseLevelCtrl` 判定成功。
- 事件流：只订阅现有 `SwitchGameStateEvent` 和 `GameResetEvent`，没有增加或修改 `PlayEvents`。
- 数据流：不读写 DB、Save、PlayerPrefs、配置表或远端配置。
- 资源流：所有 8 帧、圆盘、真假钥匙、机关石、门和门锁均为 Prefab 预放或序列化引用；运行时不加载、不实例化新资源。

## Prefab 当前标定

| 项目 | 当前值 |
| --- | --- |
| 石像滑动步长 | `48px` |
| 横向主导比 | `1.25` |
| 钥匙点击最大位移 | `18px` |
| 圆盘水平 / 垂直容差 | `0.72 / 0.6` |
| 圆盘退出额外容差 | `0.2` |
| 机关单侧移动距离 / 时间 | `1.45 / 0.8s` |
| 机关旋转晃动角度 | `7` |
| 震屏时间 / 强度 | `0.45s / 0.18` |
| 真钥匙进入 05 渐显 | `0.1s` |
| 飞锁时间 / 弧高 | `0.55s / 3` |
| 飞锁目标缩放 / 缩放时长占比 | `50% / 80%` |
| 飞锁渐隐起点 / 渐隐时长占比 | `40% / 50%` |
| DoorLock 等待上限 | `2s` |

## 验证记录

- Prefab YAML 静态扫描：150 个对象定义无重复；303 次本地非零引用均有声明。
- Prefab 字段静态检查：Level79Ctrl、Actor、石像 Transform/Renderer/Collider/Rigidbody、圆盘、左右机关石、真假钥匙、door_0、DoorLock 引用类型正确。
- 帧序检查：8 张 Sprite GUID 与 `Sp_l79_sx_01` 到 `Sp_l79_sx_08` 的 `.meta` 顺序一致。
- 交互隔离检查：真假钥匙都没有 Collider；仅真钥匙进入 Level79Ctrl 的私有 bounds 点击检测。
- 门链检查：door_0 实际根 Collider 初始 disabled，LockPoint 可解析，DoorLock 根含 `BaseItemExecutor`。
- Unity 导入：当前 `Level_079.prefab` GUID 已被 Unity `PrefabImporter` 成功导入，最近日志未发现该 Prefab 的 YAML、Missing Script 或导入失败。
- 编译：使用 Unity 6000.0.59f2 自带 `DotNetSdkRoslyn/csc.dll` 和当前 `Assembly-CSharp.rsp` 全量编译通过，结果为 `0 error`；输出只有工程既有 warning，没有 Level079 诊断。

## 手工验收清单

- [ ] 从正式入口进入 Level079，确认石像初始为 01、真钥匙不可见、出口不可进入。
- [ ] 角色从左右推动石像，确认石像稳定落地、不会物理翻转或被触摸直接拖动。
- [ ] 把石像推到圆盘，确认石像不瞬移、不锁定且仍能推动；每次进入只震屏一次，两块机关石分别晃动移开，假钥匙不可点击也不可由角色碰取。
- [ ] 把石像推出圆盘，确认两块机关石晃动合回；在开合动画中快速反向推动，确认机关从当前位置转向且不漂移；再次推入后确认机关重新打开并震屏。
- [ ] 在石像上右滑确认 `01 -> 02 -> 03`，左滑确认 `01 -> 08 -> 07`，连续快速滑动不跳错顺序。
- [ ] 确认只有 05 显示真钥匙；进入相邻的 04 或 06 时钥匙立即隐藏且没有残留。
- [ ] 在真钥匙上轻点、轻微抖动、明显滑动和多指操作，确认只有合法松手点击触发一次飞锁。
- [ ] 确认飞锁期间 Actor 不能移动，但石像仍可连续左右滑动旋转；钥匙沿抛物线自然缩小并渐隐，DoorLock 演出结束后才开放出口。
- [ ] 确认开锁完成后、进入出口前仍可旋转石像，且真钥匙不会因重新转到 05 而再次出现或触发。
- [ ] 进入门完成通关；验证失败、手动重开、退出、Replay 后场景回到初始状态。
- [ ] 在旋转、渐隐、飞锁、震屏和机关移动中分别暂停/恢复、切后台/回前台，确认无残留手势、相机偏移或软锁。
- [ ] Android 与 iOS 横屏各验证一次触摸阈值、多指、不同宽高比构图和稳定 60 fps。

## 未验证缺口

- 尚未对最新脚本执行 Level079 Unity PlayMode 全流程，物理推动手感、圆盘容差、震屏强度、机关距离、钥匙位置和抛物线构图仍需画面验收。
- 尚未验证 Android/iOS 横屏真机触摸、多指取消、切后台和性能。
- 当前生成配置 `tbleveldata.json` / `tblevelsort.json` 中没有 `10079 / Level_079`，本次未擅自选择正式排序号；正常选关入口接入仍待策划配置。
- Prefab 保留复制场景中的 7 条 door_0 孤儿 override，以及一个 inactive、断 Sprite GUID 的 `Sp_prop_1_09`；两者未接入本关逻辑，当前不影响静态引用闭包，但 Unity Inspector 清洁度待后续确认。

## 回滚路径

- 从 `Level_079.prefab` 根节点移除 `Level79Ctrl`，从石像移除本关新增 Rigidbody2D，并删除 `TrueKey_079`。
- 恢复石像原组件列表、door_0 初始设置和策划复制 Prefab 的原始结构；保留策划提供的 L79 美术资源和地形摆放。
- 删除 `Level79Ctrl.cs/.meta` 及本地文档，并回退 `DevelopMD/已有功能设计与使用记录.md` 的 Level079 追加节。

## 2026-08-05：机关移开与移回音效

### 需求、基线与当前行为

- 原机关在石像进入圆盘时移开、离开圆盘时移回，只有 DOTween 位移、旋转晃动和首次进入震屏，没有机关移动声音。
- `UpdatePlateState()` 继续只在 `_statueOnPlate` 真正变化时调用 `PlayMechanismTransition(...)`；本次在每次开合转场开始时播放一次既有 SoundsGood tag `l89StonePillarDrop`。停留在圆盘内或圆盘外的每物理帧检查不会重复播放。
- 该 tag 继续映射 `Assets/Game/ArtSrc/Audio/Sfx/l89/l89StonePillarDrop.ogg`；没有复制 089 音频、没有新增 Sound Collection 项，也没有修改 Level089。
- 本次只修改 `Level79Ctrl.cs` 和本关文档；`Level_079.prefab` 中策划现有的 Postcard Star 与摆位改动全部保留，音效实现不需要新增 Prefab 字段。

### 验证、风险与回滚

- 静态断言确认 `AudioManager.PlaySound(MechanismMoveSfxTag)` 只有一处，并位于已有状态变化门禁之后；Unity 生成响应文件的独立 Roslyn 编译为 `0 error`。
- 089 音频时长约 `1.715s`，079 机关单次移动为 `0.8s`；快速把石像推出又推回时两次 one-shot 尾音可能重叠，听感仍需 PlayMode 验收。
- 音效遵守既有全局 SFX 开关；未执行 Level079 PlayMode、Android/iOS 横屏或真机听感验证。
- 只回滚本次音效时，删除 `MechanismMoveSfxTag` 与 `PlayMechanismTransition(...)` 中的一次播放调用即可；不要回滚 Prefab、机关移动、089 资源或共享音效配置。
