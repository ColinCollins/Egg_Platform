# Level107 风险评估

更新时间：2026-09-03

## 原有行为基线

`Level_107.prefab` 复制自 Level093，并已由用户调整角色、StartDoor、横向杠杆与 EndPoint 位置。复制物仍保留 `Level93GearLever`：点击齿轮即可转动杠杆并根据角色站位直接抛射，入口门则只有公共开场门行为。本次目标是在不改 093 和共享系统的前提下，把齿轮变成可重复点击的卡死误导，把真实通关入口改为入口门从相反边缘砸下杠杆。

## 受影响文件与复用系统

- `Assets/Game/Prefabs/Level/Level_107.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_107/Level107JammedGear.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_107/Level107DoorDragItem.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_107/Level107DoorLever.cs`
- 本目录功能说明与风险记录
- 复用但未修改：Level093 三个专用脚本中的移动平台逻辑、`BaseLevelCtrl`、`ActorCtrl`、`DragableItem`、StartDoor、DOTween、FallArea/尖刺失败链和 L93 音效

## 风险清单

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| 复制后齿轮仍可直接通关或卡顿对象错误 | 齿轮 Collider 只负责点击命中，`Level107JammedGear` 改为用最短角路径轻转整个 `platform_Stone (3)` `5°` 后复原；无 Actor/EndPoint 引用，不进入抛射/成功分支 | Prefab 静态绑定与源码检查通过；整个平台微动观感待 PlayMode 验收 |
| 门在开场演出中参与物理或可被拖动 | Awake 时关闭门实体 Collider 和 Rigidbody2D 模拟；只在 Actor 可控、PLAYING、非切换态时开放 | 源码与序列化检查通过；开场时序待验证 |
| 门与平台重叠时难以起拖 | 首次进入可操作态同步恢复实体 Collider；本关 `Level107DoorDragItem` 直接检查门自身 Collider/Renderer，不依赖共享 OverlapPoint 的首个命中对象 | Unity 已导入并编译；鼠标/真机触摸手感待验收 |
| 门已落在杠杆上却没有抛射 | 第二轮实机日志确认不是高度阈值，而是嵌套 Prefab 的杠杆 Collider 未解析：`Lever=True, LeverCollider=False`。现直接序列化源 `BoxCollider2D` 的 stripped 引用，并保留子层级查找兜底 | 日志已定位根因；Prefab 引用/编译通过，修复后 PlayMode 待验收 |
| 非目标地面碰撞误触发杠杆 | 抛射碰撞仍必须命中指定 `platform_Stone (3)` 或其子碰撞体；普通地面只在存在竖向支撑接触时固定门 | 代码审查通过；物理回调待验证 |
| 成功边缘过宽或与 093 不一致 | Actor 左边缘沿用 093 的 28%，门右边缘使用同一对称比例 | Prefab 参数检查通过；Scene/PlayMode 边界待验收 |
| 反向错误解法没有飞出死亡 | 使用 `(1,1)` 模板、力度 28 固定向左物理抛射，不直接调用死亡，保留 FallArea/陷阱归口 | 编译通过；飞行距离与死亡触发待 PlayMode 验证 |
| 边缘不足仍误通关 | 只有 `Actor左28% && Door右28%` 进入 EndPoint 分支；其它站立组合只小抛或反向失败 | 源码与 Prefab 静态检查通过 |
| 角色明明站在杠杆上却因地面状态更新顺序漏判 | 使用角色/杠杆碰撞体的水平重叠和顶面距离，继续要求刚体模拟、非上升、非死亡/击飞；不依赖 `ActorCtrl.IsGround` 的当帧缓存 | 用户已复现旧问题；修复后站位边界待验收 |
| 角色站在中央杠杆右端时相机跟随导致入口门出屏 | 仅在 Level107 Prefab 将相机非跟随区右边界从 `1.38` 扩到约 `4.50`，左边界与 Y 范围基本不变；角色飞向更右区域后仍会触发跟随 | Prefab 参数静态回读通过；实际取景待验收 |
| 门砸下后平台/门被脚本强制复原，投石机观感僵硬 | 杠杆沿用 Level093 的 `90° / 0.2s / 48%` 四分之一圈与释放节奏，转完后停留且不追加回正 Tween；门保留撞击线速度、Dynamic 刚体和 Collider，继续沿倾斜平台滑落。角色不在平台时仍执行同一平台受力动作 | 源码与 Prefab 参数通过；物理手感待 PlayMode 验收 |
| 角色抛出后镜头只有水平跟随 | 正确、小抛和反向分支起飞时暂时停用共享 CameraFollowArea，由本关脚本直接将 GameCamera 的目标 X/Y 设为角色实时 X/Y；小抛恢复控制时交还共享跟随器，成功/反向持续到尝试结束 | GameCamera 显式引用与源码检查通过；二维跟镜待 PlayMode 验收 |
| 成功飞行途中碰到尖刺 | 与 093 一致，成功显式飞行期间保持 Actor Rigidbody2D 不模拟，到 EndPoint 后由成功归口处理 | 静态检查通过；PlayMode 待验证 |
| 暂停或状态切换造成 Tween/刚体继续 | 订阅既有状态/暂停/恢复事件；门下落和物理抛射缓存速度并暂停模拟，Tween 暂停后恢复 | 独立编译通过；完整状态切换待验证 |
| 门落出画面后无法再次操作 | 低于 Y=-9 时回到最近安全落点；普通落地会固定，杠杆撞击后保持物理直到落出回收 | 源码检查通过；多次重试待验证 |
| 反向飞出死亡瞬间门跳回安全位置 | 门回收新增抛射锁、角色存活、PLAYING 与尝试非切换门禁；普通小抛仅在角色存活并恢复控制后解除抛射锁，死亡流程不再触发门回收 | 源码/状态分支检查通过；死亡逐帧观感待 PlayMode 验收 |
| 共享跟随器或 CameraFollowRange 同帧覆盖本关纵向位置 | 直接跟镜期间禁用共享 CameraFollowArea，且直接驱动不调用 CameraFollowRange；Prefab 的 `limitY=0` 只作为兜底 | 静态回读通过；高抛与下落逐帧待 PlayMode 验收 |
| 二维跟镜越出原背景后露出黑色清屏色 | 中央 Tiled `CommonBg` 的纸张与遮罩由 `50×50` 扩为 `250×120`，覆盖约 `X=-125～125、Y=-60～60`；停用 X=50/100 的两块重复底图，远景装饰和共享 Prefab 不缩放 | Unity PrefabImporter 与静态尺寸检查通过；四向极值和多宽高比待 PlayMode 验收 |
| StartDoor 公共行为被污染 | 四个物理/拖拽组件只作为 Level107 Prefab 实例 override 添加，公共 Prefab 未修改 | Git 范围检查通过 |
| 93 已验收行为回归 | Level093 prefab、脚本和共享平台/Actor/UI 均未修改 | Git 范围检查通过 |

## 性能与移动端边界

- 运行时每帧仅检查门回收、输入门禁和少量本地状态；门落地只处理单个 Collision2D，不做场景扫描。
- 机关只使用一个门刚体、一个门实体 Collider、两个短 DOTween 和一次角色飞行/物理抛射，不新增持续容器分配、资源加载、Instantiate、网络、存档或 SDK 调用。
- 预计移动端开销较小，但 Android/iOS 横屏触控、多指竞争和代表性设备 60 fps 尚未验证。

## 验证证据

- Unity Bee/Roslyn 完整 `Assembly-CSharp.rsp` 编译退出码为 0，本关脚本 0 error、0 新增 warning。
- Unity 已将三个本关脚本纳入编译；新增 `Level107DoorDragItem.cs` 已进入 Bee 响应文件，并重建 `Library/ScriptAssemblies/Assembly-CSharp.dll`。
- PrefabImporter 已导入 `Level_107.prefab`；最新 YAML 静态检查为 521 个定义、521 个唯一 fileID、0 重复、0 缺失本地引用候选。
- 齿轮脚本 GUID、门刚体/实体 Collider/DragableItem/门杠杆 GUID、UnityEvent 目标和主要序列化引用均能解析。
- 2026-09-03 修复后完整 Bee/Roslyn 响应文件编译退出码为 0；门接触兜底、几何站位和 Level107 相机非跟随区参数已静态回读。
- 第二轮实机日志给出唯一缺失项 `LeverCollider=False`；修复后 Prefab 中 `platformCollider` 直接指向 `platform_Stone (3)` 源 `BoxCollider2D` stripped fileID，齿轮组件的 `jammedPlatform` 直接指向整个平台 Transform。
- 本轮源码断言确认不再存在 `leverRestoreDuration`、门随平台强制附着或 `FastBeyond360`；Prefab 静态值为卡顿 `5°`、抛射镜头死区 `0.1`，且 CameraFollowArea 引用可解析。
- 最新修复断言确认动态门的离屏回收必须通过存活/尝试状态检查且抛射锁未开启；Level107 Prefab 显式绑定 GameCamera，源码直接写入角色 X/Y，并在直接跟随期停用共享 CameraFollowArea。
- 背景修复后当前 Unity 主实例再次成功导入 `Level_107.prefab`；中央 `CommonBg` 的纸张和遮罩尺寸静态回读均为 `250×120`，原 X=50、100 两个根实例均覆写为 inactive，没有修改共享 `CommonBg.prefab`。
- `git diff --check` 通过；Level093、StartDoor.prefab、Actor.prefab、GamePlayPanel 与共享成功/失败代码没有本次差异。

## 未验证缺口与待确认

- 用户已提供旧版 PlayMode 复现截图；修复后的成功弧线、左飞死亡、死亡时门不跳回、普通小抛、门接触兜底、`90° / 0.2s / 48%` 投石机节奏、门自然滑落和本关直接二维相机跟随仍为 `uncertain`，需要重新运行 Level107 验收。
- 四向底图已按世界范围扩充，但尚未逐帧走完正确高抛、反向左飞、小抛及最低下落，也未覆盖所有目标设备宽高比；最终无黑边结论仍为 `uncertain`。
- 未验证门在尖刺、移动平台或其它地面落下后的安全位置与再次拖拽。
- 未验证暂停、死亡、Reset、Replay、退出和成功转场中的门/角色刚体恢复。
- 未验证标准关卡加载、解锁/存档和打点。当前工作区已有 `10107 / Level_107` 映射，现有排序把它放在显示位 `116`，而显示位 `107` 仍映射 `10101 / Level_101`；这些配置及 Excel 源表不是本轮改动，正式显示位是否需要调整仍为 `uncertain`，本次未猜测重排。
- `specs/037-remote-resource-loading/plan.md` 缺失，相关计划依据为 `uncertain`。

## 回滚路径

恢复 `Level_107.prefab` 的齿轮组件，移除 StartDoor 实例追加的 Rigidbody2D、实体 BoxCollider2D、DragableItem 和 `Level107DoorLever`，再删除本目录新增脚本/文档即可。共享资源和 Level093 不需要回滚。

## 2026-09-08：StartDoor 掉落后不自动复位

- 需求：门掉落之后不要重置到初始位置。此前 Update 会在动态门低于 Y=-9 且满足状态门禁时调用 RecoverDoorFromFall，恢复到安全落点（初始默认是出生位置）。本次取消整个自动回收路径。
- 影响文件：Level107DoorLever.cs 移除回收检查、坐标缓存、专用状态与方法；Level_107.prefab 仅移除废弃 doorRecoveryY 字段，保留任务开始前已有 Prefab 改动。删除的字段注释仅属于同步删除的回收配置，无关旧注释未改。
- 保留流程：松手后物理下落、普通地面固定、撞击杠杆、角色抛射与相机逻辑；无新增数据、资源、事件、存档或打点链。共享 StartDoor、Level093 与 GF 未修改。
- 验证：源码核对不再包含回收方法、坐标缓存或门的 SetPositionAndRotation；执行 git diff --check。dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -v:quiet 失败，报 CS0006 / 200 个缺失 Temp/bin/Debug 依赖程序集错误，无法据此确认编译通过。
- 未验证（uncertain）：Unity PlayMode、Android/iOS 横屏实际表现；需验证直接掉出场景、无角色在杠杆时掉落、小抛结束、反向失败后门均不瞬移，普通地面仍可拖拽，手动重开正常重建。门掉出可操作范围后本次尝试无法再拖回，需要重开；这是取消回收的直接影响。
- 回滚：仅恢复本次删除的回收逻辑及 Prefab doorRecoveryY 字段，并同步本节；不得覆盖工作区原有 Prefab 改动。此前验证记录属于历史版本，不代表本次运行验证。
