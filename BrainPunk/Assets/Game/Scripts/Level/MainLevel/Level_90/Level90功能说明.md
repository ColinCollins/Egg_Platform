# Level090 双水槽液面联动功能说明

更新日期：2026-07-27

## 需求与背景

Level090 基于 Level024 的场景和操作入口制作进阶关卡，但不修改 Level024。关卡需要在左右两个水槽中都提供可游泳水体；玩家只能小幅横向拖动中央隔板，隔板左移时左侧液面升高、右侧液面降低，右移时相反。任一侧液面到达槽顶后，隔板不得继续向该方向移动；角色处于上涨侧水体中时，需要由水面同步向上托举。

Level024 的潜水按钮和左下角可移动 UI 按钮继续保留，但 Level090 没有瓶塞、排水口或排水流程。

镜头开局保持固定；角色横向走到当前屏幕中线后，本轮才开始持续横向跟随角色。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_090.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_90/Level90Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_90/Level90Ctrl.cs.meta`
- `Assets/Game/Scripts/Level/MainLevel/Level_90.meta`
- 本目录的功能说明和风险评估
- 共享设计记录 `DevelopMD/已有功能设计与使用记录.md` 中的 Level090 日期小节

仅复用且未修改：

- `Assets/Game/Prefabs/Level/Level_024.prefab`
- `Level24Ctrl`
- `ActorCtrl`
- `InteractableWater`
- `WaterTriggerHandler`
- `GamePlayPanel_005` 及共享 HUD
- Luban 配置、生成代码和 JSON 配置

## Level024 基线

- `Level24Ctrl` 订阅 `PlayerDiveEvent`，调用 `ActorCtrl.AddDiveVelocity()`，并在开局隐藏 `Root/down_btn` 两秒后显示。
- Level024 会读取面板中的可移动按钮位置，把 `Outlet` 对齐到按钮附近并延迟激活；按钮移动达到阈值后会解锁瓶塞拖动。
- 瓶塞离开触发区后，Level024 会播放排水表现并把单个 Water 向下移动。
- `GamePlayPanel_005` 自身已有横向 `UIDrag`，因此可移动按钮的拖动能力属于面板，不需要 Level090 修改共享 UI。

上述排水链只作为复制来源的行为基线，不属于 Level090。

## 镜头基线与方案取舍

- 现有 `CameraFollowArea` 已被多个关卡使用，`Level_023 / Level_073` 的水平跟随参数为锁定 Y、`followSmoothTime=0.2`、零 Offset。
- 该共享脚本以固定世界矩形判断是否跟随；角色回到矩形内后会取消跟随并把镜头移回初始位置，不符合 Level090 “到中线后本轮持续跟随”的一次性触发语义。
- Level090 因此只参考其 `LateUpdate + SmoothDamp` 手感，在本关私有 `Level90Ctrl` 内实现锁存逻辑；没有修改共享 `CameraFollowArea`、`CameraFollowRange` 或其他关卡 Prefab。

## Level090 行为

### 双水槽

- `Level_090.prefab` 根节点使用本关私有 `Level90Ctrl`，不再由 `Level24Ctrl` 接管。
- 左侧 `Water_Left` 和右侧 `Water_Right` 都是完整水体树：Water 根节点包含 `InteractableWater`、水面边缘碰撞和水触发处理，子节点 `Buoyancy` 包含浮力区域及角色进出水事件。
- 两侧 `Buoyancy` 的 Enter/Exit 事件都绑定本关 Actor，使角色能在任一池内进入和退出游泳状态。
- 联动时移动和缩放完整 Water 根节点，因此水面视觉、边缘碰撞、浮力区域与游泳触发区域保持同一套变换。

### 隔板与液面联动

设隔板相对初始位置的横向位移为 `delta`：

```text
leftWidth  = leftInitialWidth  + delta
rightWidth = rightInitialWidth - delta
surfaceY   = basinBottomY + initialVolume / currentWidth
```

- 隔板左移时 `delta < 0`：左池变窄并升高，右池变宽并降低；右移时相反。
- 每侧初始水量分别由“初始宽度 × 初始水深”保存，拖动期间按各自体积守恒反算液面。
- Water 的 X 缩放表示当前槽宽，Water 中心 X 随隔板移动一半，Water 的 Y 位置随反算液面移动。
- 隔板只允许改变世界 X；Y、Z 固定为开局值。
- `maxDividerTravel` 是可选的额外人工位移上限；设为 `0` 时不再提前截断，只由左右各自液面到顶条件限制隔板。本关当前使用自动模式，后续手调 Water 高度或初始位置时会重新按实际水量计算边界。
- 当前 Prefab 数值为：槽底 `Y=-5.52`、槽顶 `Y=0.2092199`、左右 Water 根节点 `Y=-5.15`、`Height=6`，因此两侧初始液面均为 `Y=-2.15`；左初始宽度约 `9.0506115`、右初始宽度约 `9.527112`。
- 按当前序列化数值复算，左移极限约为 `-3.72693`，右移极限约为 `+3.92314`；到达对应极限时，上升侧液面均精确命中 `Y=0.2092199`。
- 鼠标或触摸只有命中隔板自身或其子节点 Collider 才开始拖动。暂停或切出 `PLAYING` 状态会立即取消当前拖动。

### 上涨水面托举角色

- 每次水体变形前，`Level90Ctrl` 用 Actor 根碰撞体中心与左右 `Buoyancy` 碰撞区域的 Bounds 判断角色所在水槽；若边界处同时命中两侧，则按角色中心相对当前隔板的左右位置选定一侧。
- 角色所在侧液面上涨多少，就在下一次 `FixedUpdate` 将角色刚体世界 Y 同步上移多少；只改刚体位置 Y，不覆盖已有水平速度、潜水速度或其他速度分量。
- 下降侧不会把角色向下拖。若一次物理帧前发生多次拖动，待托举量按所选水槽的有符号液面位移累计，因此“先升后降”会抵消，不会通过反复拖动逐次抬高角色。
- 待托举量绑定当前水槽；角色跨槽、离水、暂停，或托举量被物理帧消费后都会清空。死亡、击飞或刚体停止模拟时也丢弃旧位移，避免恢复后延迟跳高。
- 托举上限以当前水面与角色碰撞体中心的距离计算，并计入尚未消费的位移，防止连续上涨把角色中心顶过水面。
- 这里的“托举”是随上涨水面等量上移，保持角色原有浸水深度；不会把正在深处潜水的角色瞬间吸附到绝对水面，原有 `ActorCtrl` 潜水和浮力逻辑继续生效。
- Actor 刚体、Actor 根碰撞体或任一 `Buoyancy` 碰撞体缺失时只输出警告并关闭角色托举；双水槽液面联动仍可继续运行。

### 中线触发镜头跟随

- Actor 初始位置为 `(-7.94, 0.85, 0)`，主相机初始位置为 `(0, 0, -10)`、正交尺寸为 `5.4`。
- 开局用 `WorldToViewportPoint` 检查角色横向屏幕坐标；在 `viewport.x < 0.5` 时镜头保持初始位置，因此阈值会随实际屏幕宽高比正确换算，不依赖写死的世界坐标。
- 角色到达或越过屏幕中线后，`_hasStartedCameraFollow` 在本轮关卡实例中锁存为 true；角色之后回走不会取消跟随状态。
- 跟随只改变相机世界 X，使用 `Mathf.SmoothDamp` 和 Prefab 中的 `0.2` 秒平滑时间；Y、Z 始终恢复为开局值。
- 相机目标 X 不小于开局 X，因此角色回走时镜头最多回到开局中心，不会继续露出出生区左侧。
- `LateUpdate` 中水位同步和镜头更新相互独立；隔板静止或水位联动未启用时不会阻断镜头。
- 切出 `PLAYING` 时停止镜头更新并清零平滑速度；恢复后保留已经触发的跟随状态。
- 右侧成功门中心约为 `X=12.72`，场景地面和背景继续延伸到门后；当前不设置镜头最大 X，最终门口构图仍需 PlayMode 和不同宽高比验收。

### UI、排水与关卡流程

- `PlayerDiveEvent -> Level90Ctrl -> ActorCtrl.AddDiveVelocity()`，保持潜水输入语义。
- `Root/down_btn` 开局隐藏两秒再显示，保持 Level024 的节奏。
- 左下角可移动按钮继续使用 `GamePlayPanel_005` 中已有的横向 `UIDrag`；Level090 不读取该按钮位置，也不把它映射到场景排水物体。
- `Outlet` 和 `Cork` 在 Level090 Prefab 中保持失活；瓶塞离开事件为空，不存在排水回调。
- `Level90Ctrl` 不新增成功、失败或重开规则；现有场景触发和 `BaseLevelCtrl` 流程保持不变，完整过关链仍需 PlayMode 验收。

## 数据、资源与系统流

| 类型 | Level090 行为 |
| --- | --- |
| 事件 | 只订阅既有 `PlayerDiveEvent` 和 `SwitchGameStateEvent`；销毁时解除订阅；状态事件同时控制拖动和镜头暂停 |
| UI | 运行时查找当前 `GamePlayPanel` 的 `Root/down_btn`；不修改共享面板资源 |
| 物理 | 仅在隔板导致水面变化时累计上涨侧净位移，并在 `FixedUpdate` 修改该侧水中 Actor 的刚体 Y；不改共享 Actor 或 Water 脚本 |
| 资源 | 两套 Water 直接序列化在 `Level_090.prefab`，没有新增运行时资源加载或实例化 |
| 存档 | 不读写 DB 或 Save，不新增存档字段或迁移 |
| 配置 | 不修改 Excel、Luban 生成物、RemoteConfig 或 JSON；`10090 / Level_090` 尚未登记到正式关卡配置 |
| 埋点/商业化 | 不新增 analytics、广告、IAP、网络或平台 SDK 调用 |

## 已完成的静态验证

- Prefab YAML 共 `192` 个文档，fileID 无重复，内部 fileID 引用无缺失。
- `Level90Ctrl` 的 Actor、隔板、相机、左右 Water 引用均已序列化，脚本 GUID `3a5f4b959eba58e43b9a2e10ec115e4f` 可由对应 meta 解析。
- 镜头阈值 `cameraFollowStartViewportX=0.5`、平滑时间 `cameraFollowSmoothTime=0.2` 已显式序列化；Level090 中没有挂载共享 `CameraFollowArea` 或 `CameraFollowRange`。
- Prefab 中有 `2` 个 `InteractableWater`，两池 Enter/Exit 共 `4` 个角色事件绑定。
- 活动绑定中没有 `Level24Ctrl` 的脚本 GUID 或类型字符串；`Outlet`、`Cork` 都为 inactive，旧瓶塞 Exit 回调已清空。
- `GamePlayPanel_005` 保留横向 `UIDrag`；Level090 未修改该 Prefab。
- 左右 Water 的 `InteractableWater.Height`、水面 `EdgeCollider2D` 顶边和 Buoyancy `BoxCollider2D` 高度均为 `6`，用户本次手调后的视觉、边缘碰撞和浮力区域保持一致。
- 自动边界数值断言通过：当前左右拖动极限分别约为 `-3.72693` 和 `+3.92314`，两侧极限水面均为 `Y=0.2092199`；旧的人工上限 `3` 只能到约 `-0.47909 / -0.60108`，是本次未到顶的根因。
- 角色托举源码静态检查确认：只选择角色当前所在水槽、按有符号液面差累计、下降可抵消未消费的上涨量、跨槽/离水/暂停时清空，并以当前水面限制角色中心。
- 净位移算例检查通过：同一物理帧前先上涨 `0.2` 再下降 `0.2`，待托举量从 `0.2` 回到 `0`；连续上涨后小幅回落也会等量减少待托举量。
- 源码扫描未发现 `OnValidate`、`Reset`、DB、Save、RemoteConfig、DOTween、资源加载或实例化入口。
- `tbleveldata.json` 与 `tblevelsort.json` 中均未找到 `10090` 或 `Level_090`。
- 使用 Unity 6000.0.59f2 自带 dotnet 与 csc，基于隔离响应文件重新编译自动边界实现通过：exit code `0`、`0 error`，只有项目既有 warning、没有 Level90 warning；临时 DLL 时间 `17:39:13` 晚于脚本 `17:38:54`。
- 当前正式 `Library/ScriptAssemblies/Assembly-CSharp.dll` 时间为 `17:30:22`，早于最新自动边界脚本；Unity 正式程序集刷新仍待确认。
- `git diff --check` 已通过。

## 未验证项

- Unity Inspector 中 `Level_090.prefab` 的 Missing Script、序列化引用、Collider 和 Effector 可视状态；正式程序集已重建，但未在 Inspector 中逐项确认。
- PlayMode 中左右拖动方向、阻尼手感、液面连续性、到顶停止和中央间隙。
- 角色分别进入两池、离开水体、从一池移动到另一池时的重力和游泳状态。
- 角色分别位于左右池水面时，上涨侧同步托举、下降侧不下拉；需要覆盖水面附近、主动潜水中、边走边拖、快速反向拖动、跨槽、暂停恢复、击飞和顶面碰撞。
- 镜头在角色到达屏幕中线前完全固定，到线后平滑启动；触发后回走只跟到开局 X，不会取消跟随状态或越过左边界。
- 镜头暂停/恢复时无漂移或突跳，成功门附近构图正确；需要覆盖 16:9、2340:1080 及项目支持的其他横屏宽高比。
- 潜水按钮两秒节奏、左下按钮拖动、暂停/恢复、重开、退出、成功和失败全流程。
- `Outlet` 与 `Cork` 在所有流程中均不出现且无法触发排水。
- Android/iOS 横屏触摸、多指竞争、不同分辨率投影和代表性设备性能。
- 正式选关入口；在补齐 `10090 / Level_090` 配置前，不能通过现有正式配置链进入本关。

## 回滚

如需整体撤回本次 Level090 制作，应从本轮修改前的本地备份、工作区历史或确认过的基线恢复 `Level_090.prefab`，删除 `Level_90` 中新增的控制器与本地文档，并移除共享记录中的 `2026-07-27：Level090 双水槽液面联动` 小节。不要修改或回滚 `Level_024.prefab`、`Level24Ctrl`、共享 Actor、水体、HUD 或配置文件。
