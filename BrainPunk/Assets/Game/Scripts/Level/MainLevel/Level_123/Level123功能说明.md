# Level123 功能说明

## 2026-08-18 黄色范围脚本精简

- 背景：删除只转发到同一实现的冗余 Actor 公共别名，保持纸片状态、跳跃锁定、物理和门流程不变。
- 影响文件：`Level123ActorCtrl.cs`、`Level123Ctrl.cs`。删除 `SetPaperMode(bool)`，两处本关调用改为直接使用既有且语义准确的 `SetJumpLocked(bool)`。
- 前后差异：传入布尔值、写入的 `jumpLocked` 状态、调用时机和纸片/恢复流程完全一致；未修改 Prefab、序列化字段、资源、UI、事件、配置、存档或结算。
- 验证：仓库 C#、Prefab/UnityEvent 与文本资产检索未发现其它 `SetPaperMode` 调用；`Assembly-CSharp.csproj` 编译通过（0 错误）。
- 回滚：恢复别名方法并把两处调用名改回即可。

## 需求背景

Level123 以 Level075 的地形、按钮、刺墙、门和过关链路为基线，但把 075 的“抬墙后安全通过”改成机关陷阱。“向上”按钮只负责让墙先小幅上移、抖动，再快速下坠，是误导项，不是纸片化开关。普通 Boxy 只有在墙体实际向下运动时被左侧绿色无刺区域压中才会变成纸片；普通形态碰刺仍会死亡。门与角色使用独立判定，只要在墙体下坠期间接触绿色块或任意一段刺就会被压碎。

## 影响范围

| 类型 | 文件/资源 | 用途 |
| --- | --- | --- |
| 关卡 Prefab | `Assets/Game/Prefabs/Level/Level_123.prefab` | 075 地形副本、本关局部引用和序列化参数 |
| 关卡控制器 | `Level123Ctrl.cs` | 按钮、刺墙、门压碎、角色压扁/纸片状态和清理 |
| 角色子类 | `Level123ActorCtrl.cs` | 仅在纸盒状态拦截跳跃和落地缓冲跳，不修改共享 Actor |
| 按钮输入 | `Level123WorldButtonClick.cs` | 鼠标/触摸统一处理短按、拖拽、吸附和暂停取消 |
| 绿色压区 | `Level123CrushZone.cs` | 随墙移动，通过 Enter/Stay 只把角色交给本关控制器判定 |
| 门压碎触发 | `Level123DoorCrushTrigger.cs` | 挂在本关门上，通过 Enter/Stay 监听整面下降墙的实际 Collider |
| 纸片素材 | `Assets/Game/ArtSrc/Sprites/l123/Sp_boxy_yb_01.png` | 纸片 Sprite；Prefab 中另有局部 BoxCollider2D |
| 压缩烟雾 | `Assets/Epic Toon FX/Prefabs/Environment/Smoke/White/SmokeExplosionWhite.prefab` | 由用户提供的 `烟雾.unitypackage` 导入，压扁完成时实例化 |
| 门碎反馈 | 075 专用 `DoorBroken_075` 粒子和门碎 Sprite | 只读复用已验收资源，不改 075 资源本体 |

## 原行为基线

- Level075 使用世界空间“向上”按钮：按钮从专用 GamePanel 的 `Root/jump_btn` 槽位换算初始位置，短按发送普通跳跃事件，拖动超过阈值后在红框内吸附。
- Level075 吸附后点击会让刺墙抬升；门仍沿 075 的门触发链路处理，普通角色撞刺墙进入共享失败逻辑。
- Level075 的开场墙体会在延迟后下降到 `ceilingDownY=-3.4`；Level123 保留自动下降时序，但把本关两条下坠终点统一为 `-3.15`，给刺与地面保留纸片可通过的间隙。按钮机关启动时会中断尚未完成的开场 Tween。
- 029/075 沿用的共享 `DragableItem` 在 `Update()` 中直接写入 `transform.position`；门根 BoxCollider 是 Trigger、Rigidbody2D 是 Kinematic，因此原 Collider 只负责触发事件，不会阻挡门横向穿过墙体。项目同时关闭了 `Physics2D.autoSyncTransforms`，快速拖动还可能在两个物理帧之间跨过整个触发区。
- 共享 Actor、GamePlayPanel、`DragableItem`、Level075 脚本和 Level075 Prefab 不属于本关修改范围。

## Level123 行为

### 按钮与墙体

1. `InitializeWorldUpButton()` 等待 HUD 初始化，从 `GamePlayPanel_027_75/Root/jump_btn` 读取位置；沿用 075/034 的顺序缩放入场，入场期间输入组件关闭。
2. 鼠标或触摸位移超过序列化的 `dragThresholdPixels=60` 才进入拖拽。短按松手时仍派发一次 `PlayerJumpEvent`；拖拽松手按红框 Bounds 外扩 `targetTolerance=0.5` 的容差判定。
3. 命中后按钮挂到红框节点中心，关闭拖拽但保留点击；因此吸附后的按钮不可再拖走，点击只调用 `ClickWorldUpButton()`，不会再次让角色跳跃。
4. 刺墙开场延迟 `2s`、用 `2.5s` 自动下降至 `ceilingDownY=-3.15`。点击按钮会中断剩余开场 Tween，再执行 `wallLift=0.35` 的相对上移（`0.22s`）→等待→抖动（`0.22s`）→以 `Ease.InCubic` 快速下坠至 `wallFinalY=-3.15`。两条路径只有实际向下的 Tween 区段会开启压力判定；等待、抬升、抖动和落定后均不会压扁角色或压碎门。

### 门压碎

- 门已移除 075 的通用 `targetTag=enemy` UnityEvent，改挂本关专属 `Level123DoorCrushTrigger`。该组件同时监听 Enter/Stay，再由 `Level123Ctrl.TryCrushDoorFromWall()` 校验接触 Collider 必须属于 `CaltropGround` 层级且墙体正在下坠。
- 门使用墙体已有的绿色块和各段刺 Collider 作为真实压面，因此放在截图所示的任意刺区下方也会命中；角色仍只有绿色区入口，刺区不会触发纸片化。Enter/Stay 双路径还能覆盖“门先停在刺下方、按钮仅上抬 `0.35` 后再下降”而没有重新 Enter 的情况。
- 门拖拽另由本关 `LateUpdate()` 做扫掠阻挡：Prefab 的 `doorDragBarrier` 显式绑定 `CaltropGround/Ground_mid` 非 Trigger BoxCollider；每个拖拽帧先回到上一允许位置并执行 `Physics2D.SyncTransforms()`，再用门 Collider 从该位置向指针目标执行 `Collider2D.Cast`。命中时把门停在接触面前 `0.01` 世界单位，未命中才完整接受目标位置，因此墙在高处时门仍能从下方通过，墙落下后不能从左侧快速横拖到右侧。
- 拖拽扫掠使用固定 8 项命中缓存，只在门实际拖动时执行，不修改共享 `DragableItem`、全局 Layer Matrix、029 或 075；门原 Trigger 继续只负责下降墙体从上方接触时的压碎流程。
- 命中后只调用一次压碎流程，关闭门拖拽和门 Collider；门可视节点先宽扁变形（`0.26s + 0.36s`），完成后生成贴地的 `CrushedDoor_123`、播放 075 门碎粒子、震屏并播放门碎音效。
- 压碎流程不会停止刺墙 Tween；门残骸与门根分离，门被隐藏后残骸仍留在本关根节点。

### Boxy 压扁与纸片

1. `Level123Ctrl` 只在开场自动下降或按钮机关最终下降的实际运动区段设置 `ceilingDescending=true`；按钮是否安装、是否点击不参与 `CanCrushActor`。绿色压区同时监听 Enter/Stay 且只转发本关 Actor，角色提前站在区域内也能在下降开始后命中；下坠结束即关闭，不保留落定后的宽限窗口。
2. 压缩开始立即通过 `SetJumpLocked(true)` 和 `SetJumpForce(0)` 锁定跳跃，同时锁定本关操作、清零输入、停止移动并暂停 Actor 刚体；Spine 按“先变宽、再变扁”的两段 Tween 表现。
3. 形变完成后实例化烟雾特效（`smokeScale=1.6`，排序层置于角色上方）；实例及其子粒子关闭 `playOnAwake`、使用非缩放时间，并设为 `StopAction.Destroy`，随后隐藏 Spine 和原角色 Collider，显示纸片 Sprite/BoxCollider2D。
4. 纸片恢复刚体但使用较低重力和阻尼（`paperGravityScale=0.65`、`paperLinearDamping=0.15`），通过本关 `LateUpdate()` 给水平移动加速度和松手刹车惯性；暂停、死亡、进入过关门或成功/失败时会清零水平速度，避免上一帧输入干扰门吸入。`Level123ActorCtrl.SetJumpLocked(true)` 在纸盒阶段拦截跳跃入口和落地缓冲跳，`SetJumpForce(0)` 作为物理力度兜底。
5. 纸片 Collider 运行时切换为 `Actor` 层、`Player` Tag，并只对本关缓存到的六段刺 Collider 调用成对的 `Physics2D.IgnoreCollision`；这不会禁用刺处理器，普通形态仍正常碰刺死亡。两条下坠都停在 `Y=-3.15`，给纸片与地面保留可见间隙。进入纸片状态时停用 Actor 的整个 Shadow 节点，不再生成或保留纸片影子；清理时按进入关卡前缓存的 `activeSelf` 恢复。
6. `Level123ActorCtrl` 的纸片更新分支不再调用共享 `HandleFlip()`，因此左右移动只改变速度，不改变 Actor 根节点和纸片 Sprite 的朝向；普通形态仍执行 `base.OnUpdate()` 并保留原有翻转。纸片继续保留 `Player` Tag、Actor 刚体和原 `BaseLevelCtrl` 过关门链路，向右走后仍由标准旋转门吸入。

## 数据、资源与事件流

- 本关不新增存档字段、Luban 字段、RemoteConfig、埋点、广告、IAP 或全局事件；短按仅复用已有 `PlayerJumpEvent`，门/成功仍走现有关卡回调。
- `compressionSmokePrefab` 直接序列化引用导入后的烟雾 Prefab；门碎粒子直接序列化引用 075 专用资源，不修改共享粒子。
- 当前工作区的 `LevelData.xlsx`、`LevelSort.xlsx` 及其临时锁文件存在用户已有改动；静态搜索未找到 `100123/Level_123` 配置行。本次没有擅自写入配置，正式入口映射标记为待配置维护确认（`uncertain`）。

## 验证证据

- `Level_123.prefab` 最新 YAML 静态扫描：288 个本地 fileID 定义、596 次非零内部引用、288 个唯一内部目标，重复定义 `0`，缺失内部引用 `0`。
- Prefab 外部 GUID 扫描：65 个唯一 GUID；63 个在项目 `.meta` 中闭合，2 个为 `UniversalRenderPipelineGlobalSettings` 使用的 URP 内置默认材质 GUID。
- 导入的 `SmokeExplosionWhite.prefab` 静态扫描：8 个本地 fileID 定义，重复/缺失本地引用均为 `0`，2 个外部 GUID 均可解析。
- Level123 五个 `.cs` 文件的行尾空白扫描为 `0`；本轮没有修改 `Level_075.prefab`、Level075 脚本、共享 Actor 或公共输入。
- 使用 Unity 6000.0.59f2 自带 `DotNetSdkRoslyn/csc.dll` 和隔离输出目录后台编译最终脚本，结果为 `exit code 0 / 0 error`，Level123 诊断 `0`；没有启动或操控新的 Unity 前台。
- 用户当前已打开的 Unity 随后完成脚本编译、程序集重载和 `Level_123.prefab` 导入；`Editor.log` 记录编译耗时约 `4.867s`、PrefabImporter 成功，最近一次编译区间内 `error CS` 为 `0`。
- 已检查新脚本无 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、`TODO/FIXME`、旧 Level075 控制器引用或未实现占位。
- 新增 `Level123ActorCtrl` 后，Level123 嵌套 Actor PrefabInstance 移除源 `ActorCtrl` 并添加本关子类；所有本关 Actor 引用均指向该本地组件，未改 `Actor.prefab`。
- Level123 角色的 `IsEditor` 与 Level075 一致设置为 `0`，避免编辑器键盘轮询覆盖 GamePanel 的移动事件。

## 2026-08-17 跟进修复

- 角色不播放走路动画的直接原因是本关替换后的 `Level123ActorCtrl.spineCtrl` 被错误绑定到 Actor Prefab 的 `SpriteRendererGroupAlpha` 组件。现已改为 Actor 实例内源 fileID `4652975846866326761` 对应的 `ActorSpineCtrl` stripped 引用；Level123 本地 fileID `797000000000000124` 只定义、引用各一次。
- 开场刺墙不下降的直接原因是 `Level123Ctrl.Start()` 没有启动 075 基线的开场 Tween。现已增加并调用 `StartCeilingIntro()`；按钮机关启动时先终止这条 Tween，再进入本关上移、抖动和快速下坠序列。当前本关两条下降终点已统一为 `Y=-3.15`。
- 本次修复仅修改 `Level123Ctrl.cs` 和 `Level_123.prefab`；没有修改 Level075、共享 `ActorCtrl`、Actor Prefab、GamePlayPanel 或公共输入。

## 2026-08-17 绿色无刺压区修正

- 删除按钮机关开启纸片窗口、运行时替换刺处理器及禁用共享死亡处理器的逻辑。按钮现在只驱动墙体动作，`CanCrushActor` 的唯一运行条件是墙体处于实际下坠区段。
- 新增本关局部 `Level123CrushZone`，Prefab 节点 `CaltropGround/PaperCrushZone_GreenArea` 随墙移动，Collider 横向范围严格位于墙体左边界与第一段刺之间；Enter/Stay 只把本关 Actor 或 StartDoor 转发给控制器。
- 两段本地展开的刺恢复为共享 `OnTriggerTrapHandle`，其余嵌套刺继续使用原处理器；普通角色跨在绿色区与相邻刺之间时，控制器先检查所有有效角色 Collider 与六段刺的真实几何重叠，命中则不暂停刚体，让共享死亡回调优先。合法纸片形态只忽略自身与这六段刺的碰撞，清理时逐对恢复。
- 门移除旧 `targetTag=enemy -> CrushDoor` UnityEvent；上一版曾把门错误限制在绿色区，现已由本关专属 Enter/Stay 组件独立监听下降墙体的全部真实压面。角色在压缩或烟雾延迟期间若已经死亡，不会再切换成纸片。
- 最新脚本已使用 Unity 6000.0.59f2 自带 Roslyn 在隔离输出目录后台全程序集编译，结果 `exit code 0 / 0 error`；当前已打开 Unity 随后自行完成 Tundra 构建（最近两次均 success、无 `error CS`）和 `Level_123.prefab` 导入，AssetImportWorker 生成 artifact `baa2e147769d8f9994c586aa28169171`。导入日志只有项目既有材质 Drawer 与重复 Atlas 警告，未见本关 Prefab 失败；运行时碰撞与视觉仍需 PlayMode 补验。

## 未验证项与回滚

- Unity PlayMode 的实际按钮入场顺序、触摸拖拽命中、门在墙抬起/落下两种高度的扫掠边界、墙体最终间隙、门压碎时机、烟雾覆盖范围、纸片无影子、纸片固定朝向和过关门吸入尚未在当前占用项目的 Unity 进程中复验；Android/iOS 横屏、多指和暂停/重开也未验证。
- 普通形态左右移动时的实际 Spine `walk` 切换，以及开场 `2s` 后刺墙的可见下移，仍需要在 PlayMode 观察确认；当前证据覆盖序列化绑定、调用链、后台编译和 Unity 导入，不等同于运行时画面验收。
- 绿色压区和门专属 Enter/Stay 与开场、按钮两条下降状态的实际物理回调顺序仍需 PlayMode 确认；当前证据覆盖脚本调用链、Prefab 几何与后台编译。

## 2026-08-17 门在刺区下方不碎修复

- 根因是上一版让门与角色共用 `PaperCrushZone_GreenArea`；该区横向仅约 `0.8`，截图中的门位于刺区下方，永远不会进入压碎调用链。
- 角色入口现明确为 `TryCrushActorFromGreenZone()`，只保留绿色无刺区纸片化。门根节点新增 `Level123DoorCrushTrigger`，Enter/Stay 转交 `TryCrushDoorFromWall()`，后者只接受当前下降墙体及其子 Collider。
- 本修复只改 Level123 私有脚本、Prefab 和文档；Level075、共享 Actor、共享刺死亡脚本和公共输入保持不变。运行时画面仍需 PlayMode 复验。
- 当前 Unity 自行刷新后，最新 `Assembly-CSharp.rsp` 已收录三个相关脚本，Tundra 完成 C# 编译、IL 后处理和 DLL 复制并报告 success；该编译区间未出现 `error CS`。Prefab 当前 `288` 个本地定义全部唯一，`595` 次内部引用覆盖 `288` 个定义，缺失引用为 `0`。
- 纸片禁跳已改为本关子类拦截 `JumpButton` 与落地缓冲分支；共享事件仍会调用 `ActorCtrl.TriggerJump()` 的外层入口，但纸盒阶段不会产生跳跃力。实际触摸音效表现仍待 PlayMode。
- 入口配置未纳入本轮，需配置维护方补齐并重新生成运行时表后才能从正式选关链路验证。
- 回滚本关时删除 `Level_123.prefab`、`Level_123` 脚本/素材和烟雾导入目录，并移除本地记录；不会触碰 `Level_075.prefab`、共享 Actor、公共输入或共享粒子。若只回滚本次判定修正，可移除 `Level123CrushZone` 与 `PaperCrushZone_GreenArea`，并恢复本关控制器和 Prefab 的上一版绑定。

## 2026-08-17 纸片影子与朝向修正

- 需求是 Boxy 切换成纸片后完全不显示影子，并且纸片左右移动时不再镜像翻转；普通 Boxy 的影子和左右朝向逻辑保持原状。
- `Level123Ctrl` 在解析本关 Actor 时缓存 Shadow 节点初始启用状态，进入纸片时停用整个节点，清理时恢复缓存值。停用整个节点可以同时隐藏 `ShadowController` 已创建的子影子；旧的纸片影子宽度反射写入、缩放和 `paperShadowWidth` Prefab 参数已删除。
- `Level123ActorCtrl.OnUpdate()` 的纸片分支跳过 `HandleFlip()`，但继续执行碰撞和动画更新；普通形态仍调用 `base.OnUpdate()`。修改只涉及 Level123 私有脚本和 Prefab，没有修改共享 `ActorCtrl`、`ShadowController`、Actor Prefab 或 Level075。
- Unity 6000.0.59f2 自带 Roslyn 已在隔离目录后台编译最终脚本，结果 `exit code 0 / 0 error`；`Level_123.prefab` 静态闭包仍为 288 个唯一定义、595 次内部引用、0 个重复或缺失引用。未启动或操控 Unity 前台。
- `uncertain`：纸片切换瞬间的影子消失、左右移动固定朝向、普通形态影子/翻转回归及关卡重开恢复仍需 PlayMode 画面验证。若只回滚本次修正，可恢复纸片分支的 `HandleFlip()`、进入纸片时的旧影子缩放逻辑和 Prefab 的 `paperShadowWidth` 参数；不需要改动任何共享文件。

## 2026-08-17 门拖拽穿过落地齿墙修复

- 截图问题的直接原因不是 Layer Matrix，而是门沿用的共享拖拽直接写 Transform，且门 Collider 为 Trigger；墙落定后 `ceilingDescending=false` 又会让压碎入口忽略后续接触，所以原配置没有任何实体阻挡能力。
- `Level123Ctrl` 新增本关私有拖拽扫掠：缓存门上一允许位置，在 `LateUpdate()` 中回退、同步物理 Transform、向本帧目标 Cast，并按最近的 `Ground_mid` 命中距离减去 `0.01` skin 后截断位移。Prefab 新增的 `doorDragBarrier` 只绑定本关墙体已有实体 Collider，没有新增全局事件、资源、存档或配置。
- 修改范围为 `Level123Ctrl.cs`、`Level_123.prefab` 和 Level123 本地文档；共享 `DragableItem`、Level029、Level075、Physics2D Layer Matrix 和门原 Trigger/压碎表现均未修改。8 项命中数组预分配，运行时只在拖门时扫掠。
- Unity 6000.0.59f2 自带 Roslyn 使用当前 `Assembly-CSharp.rsp` 隔离后台编译通过：`exit 0 / 0 error / Level123 diagnostics 0`；104 项 warning 均为工程既有诊断。Prefab 为 288 个唯一定义、596 次本地引用和 288 个唯一目标，重复/缺失均为 0；未启动或操控 Unity 前台。
- `uncertain`：墙高位时从下方通过、墙下降过程中贴边拖动、墙落定后的左右快速甩动、暂停/松手/重开以及 Android/iOS 触摸仍需 PlayMode 实测。只回滚本次修复时，删除 `doorDragBarrier` 序列化绑定和 `InitializeDoorDragCollision()` / `ConstrainDoorDragAgainstBarrier()` 调用及字段即可；不得改共享 `DragableItem`。
