# Level107 功能说明

更新时间：2026-09-03

## 需求与背景

Level107 以已验收的 Level093 为场景和多数 trick 基线。用户已在 `Level_107.prefab` 中把角色、入口门、横向杠杆和 EndPoint 调整到本关位置；本次保留这些摆位与 093 的移动平台、尖刺、相机、反馈和基础成功/失败链，只替换齿轮与通关机关。

本关明确解法是：角色站在横向杠杆左侧边缘，玩家把初始门拖到杠杆右侧边缘上方并松手，让门真实下落砸中杠杆，随后角色像 093 一样沿抛物线飞到 EndPoint 并成功。齿轮点击只是误导，不是过关入口。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_107.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_107/Level107JammedGear.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_107/Level107DoorDragItem.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_107/Level107DoorLever.cs`
- 只读复用：`Level93VerticalPlatformDrag`、`Level93PlatformPingPong`、`BaseLevelCtrl`、`ActorCtrl`、`DragableItem`、`StartDoor.prefab`、DOTween、既有 FallArea/尖刺失败链、L93 两个机关音效

本次未修改 Level093、公共 StartDoor、公共 Actor、公共 GamePlayPanel、共享成功/失败逻辑、存档、打点、RemoteConfig、广告、IAP、平台 SDK 或 GF 子模块。

## 原有行为基线

- 当前 `Level_107.prefab` 是 `Level_093.prefab` 的副本，保留 093 的 3 个竖轨拖拽平台、7 个往复平台、19 个尖刺和齿轮抛射组件。
- 复制后齿轮仍挂 `Level93GearLever`，点击会转动整个 `platform_Stone (3)` 并可能直接把角色抛到 EndPoint，不符合本关“齿轮卡住且不是过关点”的要求。
- 公共 StartDoor 只有开场 Trigger 和演出控制，没有自由拖放、实体坠落或杠杆落点判定。

## 本次行为

### 卡死齿轮

- `chilun` 改挂 `Level107JammedGear`，齿轮仍只提供点击区域；点击后让整个 `platform_Stone (3)` 顺时针尝试转动 `5°`，用 `0.1s` 到达卡点，再用 `0.16s` 复原。
- 两段旋转均使用 `RotateMode.Fast` 的最短角路径，避免平台初始 Z 角接近 `0°/360°` 时绕大圈；`5°` 是当前整台平台的微动值。
- 复原后可以再次点击并重复同一表现；脚本不引用角色或 EndPoint，也不执行门杠杆抛射分支，因此这次整个平台的轻微转动不会触发成功。
- 非 `PLAYING`、暂停或上一轮卡死表现尚未结束时不接收新点击；暂停时 Tween 暂停，恢复后继续。

### 入口门拖放

- 只在 Level107 的 StartDoor 实例上追加 Kinematic/Dynamic `Rigidbody2D`、非 Trigger `BoxCollider2D`、本关 `Level107DoorDragItem` 和 `Level107DoorLever`，不修改公共门 Prefab。
- 开场完成、角色可控、关卡处于 `PLAYING` 且不在尝试切换时开放拖拽，同时恢复开场阶段关闭的门实体 Collider。拖拽时门为 Kinematic 并关闭实体碰撞；松手后门为 Dynamic，按 `gravityScale=1.8` 下落。
- `Level107DoorDragItem` 直接检查门自身/子物体的 Collider 与 Renderer 边界，不受平台先被公共 `Physics2D.OverlapPoint` 命中的影响，因此门与平台重叠时仍可从可见门体稳定起拖。
- “从空中砸下”不增加未由需求指定的固定最低高度：门松手时与杠杆水平重叠、门中心位于杠杆顶面以上即可登记为目标落点。允许玩家把门贴近顶面放下，避免门已经落在杠杆上却被过严的底边间距过滤。
- 门掉落后不自动回到初始位置或最近安全落点，包括低于世界 Y `-9` 的情况。落到普通地面后仍固定并可继续拖动；砸中杠杆后保持 Dynamic，沿倾斜平台继续滑落。
- 2026-09-08 按用户要求移除整个自动回收路径；普通小抛恢复控制后也不再回收。手动 Reset/Replay 仍走既有关卡重建流程。

### 杠杆结果判定

- 左右边缘均使用 Level093 已验收的外侧 `28%` 比例。
- 门从杠杆上方释放并真实接触指定杠杆后，杠杆按门所在侧以 `0.2s` 转动 `90°`，转过 `48%` 时释放角色，沿用 Level093 的四分之一圈/释放节奏；平台转完后停留，不自动恢复水平。门不再被脚本锁住或附着到平台，而是保留撞击速度、Collider 和 Dynamic 刚体，继续受重力及倾斜平台碰撞影响。碰撞回调之外还用 `Collider2D.IsTouching` 做物理帧兜底，避免接触法线方向或回调顺序漏判。
- 角色必须以碰撞体几何关系站在杠杆顶面、刚体仍在模拟且没有上升，才会参与抛射；不再依赖 `ActorCtrl.IsGround` 的帧状态，避免门撞击当帧的地面状态更新顺序漏判。角色不在杠杆上时只播放杠杆受力反馈。
- 角色在左侧 `28%`、门落点在右侧 `28%`：角色随杠杆到受力峰值后释放，沿 `2.45s`、弧高 `20` 的显式抛物线到 EndPoint，再调用 `BaseLevelCtrl.SuccessStrightly()`。
- 角色在右侧 `28%`、门落点在左侧 `28%`：角色以力度 `28` 向左上方物理抛出，由既有 FallArea/陷阱链触发死亡；脚本不直接调用死亡。
- 角色已站在杠杆上，但上述任一成对边缘条件不足：以 093 的力度 `12` 和方向模板 `(0.35, 1)` 小幅抛射，方向朝角色所在侧的对面；落地或最多 `1.2s` 后恢复原有操作权。
- 任一实际抛射分支开始时，Level107 暂时停用共享 `CameraFollowArea` 的 `LateUpdate`，由 `Level107DoorLever` 直接把 GameCamera 目标设为角色实时世界坐标 `(actor.x, actor.y, camera.z)`，以 authored `0.2s` 做二维 SmoothDamp。普通小抛在落地/超时恢复操作时交还共享跟随器，正确成功和反向飞出则持续直接跟随到尝试结束。
- 直接跟镜不再经过 `CameraFollowRange`，因此不会只更新水平位置，也不会被原纵向边界约 `[-3.6, 9.6]` 截断。Level107 实例仍关闭 `CameraFollowRange.limitY` 作为共享跟随器兜底；该修改不影响 Level093 或公共 Prefab。

### 抛射跟镜背景覆盖

- Level107 原有 3 个 `CommonBg` 实例均为 `50×50`，只覆盖约 `X=-25～125、Y=-25～25`；二维跟镜到达高抛顶部或反向飞出区域时，相机视口会越过纸张底图并露出黑色清屏色。
- 本关把中央 `CommonBg` 的纸张 `Bg` 与同实例遮罩都改为 Tiled `250×120`，世界覆盖约 `X=-125～125、Y=-60～60`。纹理保持平铺而非拉伸；原来位于 X=50、100 的两块重复实例停用，避免大底图上继续叠绘或产生接缝。
- 只扩充最底层纸张和遮罩，不缩放 `FarBg` 的山体、遗迹等装饰，也不修改共享 `CommonBg.prefab`，因此其它关卡不受影响。

## 数据、资源、事件与状态流

```text
点击齿轮 -> 整条杠杆平台轻转 -> 复原 -> 可再次点击（无过关分支）

StartDoor 拖拽 -> 松手切 Dynamic -> 从杠杆上方下落 -> 支撑碰撞
  -> 杠杆朝受力侧转动并停留；门保留 Dynamic，沿平台滑落/掉落
  -> 角色未站杠杆：只做上述投石机受力反馈
  -> 左角色边缘 + 右门边缘：显式抛物线到 EndPoint -> SuccessStrightly
  -> 右角色边缘 + 左门边缘：向左强物理抛射 -> 既有 FallArea/陷阱死亡
  -> 其它站位：小幅物理抛射 -> 落地/超时恢复操作

任一角色抛射 -> 暂停共享 CameraFollowArea -> 本关脚本直接二维跟随角色
```

两个本关脚本只订阅既有 `SwitchGameStateEvent`、`GamePauseEvent` 和 `GameResumeEvent`，用于输入与 Tween/物理暂停恢复；没有新增全局事件、运行时资源加载、实例化、配置读写或存档字段。

## 已执行验证

- Unity 6000.0.59f2 的当前 Bee/Roslyn `Assembly-CSharp.rsp` 完整编译通过，退出码 `0`，本关脚本没有 C# error 或新增 warning。
- 正在运行的 Unity 已把新增 `Level107DoorDragItem.cs` 写入响应文件；最终修改后 `Library/ScriptAssemblies/Assembly-CSharp.dll` 于 2026-09-03 13:59:01 重建。
- Unity PrefabImporter 已重新导入 `Level_107.prefab`；最新静态回读确认 521 个 YAML 文档、521 个唯一 fileID、0 个重复 fileID、0 个缺失本地引用候选。
- 静态绑定确认齿轮只引用 `Level107JammedGear`，不再引用 `Level93GearLever`；StartDoor 的 4 个新增组件、两个 UnityEvent 回调、Actor/BaseLevelCtrl、杠杆、EndPoint 和 CameraFollowArea 引用均已写入。
- 根据 2026-09-03 实机反馈，门落杠杆判定已增加顶面贴近释放、指定杠杆碰撞优先和 `IsTouching` 物理帧兜底；相机非跟随区由 X `[-10.62, 1.38]` 扩到约 `[-10.60, 4.50]`，角色站在中央杠杆右端时相机回到初始位置并继续显示入口门。
- 第二轮实机日志明确记录 `Lever=True, LeverCollider=False`。现已把源 `platform_Stone` 的 `BoxCollider2D` 作为 stripped 组件直接序列化给 `Level107DoorLever.platformCollider`，并保留 `GetComponentInChildren<Collider2D>(true)` 兜底；不再依赖运行时只从嵌套 Prefab 根节点执行 `GetComponent`。
- 本轮静态回读确认齿轮角度为 `5°` 且不再使用 `FastBeyond360`；门命中改为本关适配器，投石机采用 Level093 的 `90° / 0.2s / 48%` 节奏且不再追加回正 Tween，撞击后门保持 Dynamic；抛射相机直接绑定现有 `CameraFollowArea`。
- 针对最新 PlayMode 反馈，源码回读确认门回收新增“抛射中、角色死亡、尝试切换、非 PLAYING”门禁；抛射阶段现由本关脚本直接写入相机 X/Y，并停用共享跟随器避免同帧覆盖，Prefab 已显式绑定 GameCamera。
- 当前打开的 Unity 主实例已重新导入背景修改后的 `Level_107.prefab`，最新 PrefabImporter 记录导入成功且没有 YAML/Prefab 反序列化错误；静态回读确认中央纸张与遮罩均为 `250×120`，另外两个 `CommonBg` 根实例均为 inactive。
- `git diff --check` 通过。

## 未验证与待确认

- 用户实机已复现旧版“门难拖、平台卡顿旋转过大、撞击后平台强制回正”、死亡瞬间门回收，以及抛射镜头只有水平跟随；本轮修复后的门拖放、四类站位结果、投石机停留角、死亡时门状态、门滑落、直接二维抛射跟镜、失败、重开、通关与退出仍待再次 PlayMode 验收。
- 四向纸张底图的 Prefab 尺寸和 Unity 导入已确认；高抛最高点、反向最远点、下落最低点及不同横屏比例下是否始终不露黑仍需 PlayMode 逐帧目视验收，状态为 `uncertain`。
- `5°` 卡顿角、Level093 式 `90° / 0.2s / 48%` 投石机节奏、门的拖拽范围、力度 `28` 的反向飞出距离、门滑落/回收手感和成功飞行弧线仍需在 Unity 中验收；这些属于手感参数，不作为已验证结论。
- Android/iOS 横屏多指触控、暂停/恢复和代表性设备 60 fps 尚未验证。
- 当前工作区的共享配置已包含 `10107 / Level_107`，现有 `tblevelsort.json` 将其放在显示位 `116`；显示位 `107` 仍映射 `10101 / Level_101`。这些配置及其 Excel 源表在本轮开始前已存在其它工作区修改，本次没有改写或重排；是否调整正式显示位不属于已确认需求，状态为 `uncertain`。
- 仓库规则引用的 `specs/037-remote-resource-loading/plan.md` 在当前工作区不存在，相关计划依据为 `uncertain`。

## 回滚路径

从 `Level_107.prefab` 恢复齿轮原组件并移除 StartDoor 实例上的 4 个本关新增组件，删除 `Level_107` 目录下三个脚本及本地文档，即可撤回本关新逻辑。Level093、公共 StartDoor/Actor/UI、配置、存档和其它关卡无需回滚。

## 2026-09-08：StartDoor 掉落后不自动复位

- 需求：门掉落之后不要重置到初始位置。此前 Update 会在动态门低于 Y=-9 且满足状态门禁时调用 RecoverDoorFromFall，恢复到安全落点（初始默认是出生位置）。本次取消整个自动回收路径。
- 影响文件：Level107DoorLever.cs 移除回收检查、坐标缓存、专用状态与方法；Level_107.prefab 仅移除废弃 doorRecoveryY 字段，保留任务开始前已有 Prefab 改动。删除的字段注释仅属于同步删除的回收配置，无关旧注释未改。
- 保留流程：松手后物理下落、普通地面固定、撞击杠杆、角色抛射与相机逻辑；无新增数据、资源、事件、存档或打点链。共享 StartDoor、Level093 与 GF 未修改。
- 验证：源码核对不再包含回收方法、坐标缓存或门的 SetPositionAndRotation；执行 git diff --check。dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -v:quiet 失败，报 CS0006 / 200 个缺失 Temp/bin/Debug 依赖程序集错误，无法据此确认编译通过。
- 未验证（uncertain）：Unity PlayMode、Android/iOS 横屏实际表现；需验证直接掉出场景、无角色在杠杆时掉落、小抛结束、反向失败后门均不瞬移，普通地面仍可拖拽，手动重开正常重建。门掉出可操作范围后本次尝试无法再拖回，需要重开；这是取消回收的直接影响。
- 回滚：仅恢复本次删除的回收逻辑及 Prefab doorRecoveryY 字段，并同步本节；不得覆盖工作区原有 Prefab 改动。此前验证记录属于历史版本，不代表本次运行验证。
