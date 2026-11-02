# Level113 彩色钥匙与按钮道具功能说明

## 1. 需求与背景

Level113 已由场景侧删除黄色箱子和黄色钥匙。本次只实现用户明确指定的关卡行为，不在代码里写场景标题、多语言文本或其它本来属于 Prefab 的显示配置。

原 Prefab 继续挂着 `Level18Ctrl`，但其 Keys/Chests 数组在黄色对象删除后已有空引用，且旧顺序与新颜色链不一致，因此本关禁用该实例组件，改由 `Level113Ctrl` 管理局部玩法；共享 `ActorCtrl`、`GamePlayPanel_000`、门 Prefab 均不修改。`ItemJumpOutExecutor` 只新增一个固定起终点入口，原 `Play()` 及其它关卡行为不变。

## 2. 影响范围

- `Assets/Game/Prefabs/Level/Level_113.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_113/Level113Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_113/Level113ActorCtrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_113/Level113KeyDrag.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_113/Level113TriggerRelay.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_7/ItemJumpOutExecutor.cs`（仅新增复用原参数、指定起终点的入口）
- `Assets/Game/ArtSrc/Spines/sp_box_025/Box.json`
- `Assets/Game/ArtSrc/Spines/sp_box_025/Box.atlas.txt`

## 3. 新行为

### 3.1 箱钥匙颜色链

钥匙统一在当前红钥匙 `Sp_key_1_05` 的场景位置出现。正常接触链为：

```text
红钥匙(05)跟随角色
  -> 红箱 Chest_red 开出蓝钥匙(02)
  -> 蓝箱 Chest_blue 开出橙钥匙(04)
  -> 橙箱 Chest_orange 开出绿钥匙(03)
  -> 绿箱 Chest_green 开出粉钥匙(06)
```

- 箱子只接受当前跟随角色、且颜色链匹配的钥匙；碰到错误箱子不执行。
- 开箱完全沿用 Level018 Prefab 的原事件顺序：旧钥匙依次经过原排序、0.3 秒缩放和 0.3 秒飞入箱子节点，随后播放 `openChest` 与 `box-open`，再等待原 0.5 秒；开箱全程不锁定角色移动。
- 等待完成后仍按原顺序延迟隐藏旧钥匙、实例化原 `Shiny`，再显示下一把钥匙；不再添加渐显、额外缩放或 `box-open_idle`。
- 下一把钥匙直接复用自身原 `ItemJumpOutExecutor` 的随机 `0.3–0.5` 秒时长、`2–3` 高度和原时间曲线。唯一改动是从当前箱子跳到统一台面位置，不再使用原随机水平落点。
- 台面钥匙靠近当前 Actor 时直接执行钥匙 Prefab 原有 `TriggerExecutor`：`getKey`、排序 151、关闭漂浮、等待 0.3 秒、启用原 `FollowTarget(smoothSpeed=15, offsetY=0.7)`，没有自定义拾取 Tween。
- 钥匙原有 `StateTriggerSwitchStateHandle`、`OnTrigger2DHandle` 自动触发入口由本关关闭，避免旧状态链重复执行；原 `TriggerExecutor` 与 `ItemJumpOutExecutor` 表现节点由 Level113 在正确时机显式复用。

### 3.2 台面钥匙拖入跳跃按钮

- 任意处于 `ReadyOnPlatform`、尚未跟随角色的钥匙都可拖动。
- 拖动期间显示 `ScreenSpaceOverlay` 顶层影像，排序为 `32760`；支持鼠标和独立 fingerId 触摸所有权。
- 只有松手中心命中 `Root/jump_btn` 才进入道具装备链；拖到 Actor 或其它位置均视为无效投放，恢复场景钥匙和漂浮。
- 安装后临时禁用共享 `CustomButton`，由 Level113 监听同一区域，避免继续派发普通跳跃事件；退出或重建关卡时恢复按钮原 Sprite、颜色、尺寸、层级和交互状态。

### 3.3 obtain、hit 与门锁

- 命中按钮后先替换按钮图标，场景钥匙不再播放按钮到角色的飞行动画，而是立即使用 `BoneFollower` 出现在 Box Spine 的 `point_hit` 骨骼。用户描述中的 “point int” 依据已交付资源和截图说明按 `point_hit` 实现。
- 钥匙挂点出现后立即播放 `obtain`；动画完成才恢复角色操作并开放按钮点击，随后角色恢复通用 `idle / walk / jump` 等动画。钥匙只作为 `point_hit` 的挂载物继续随骨骼运动，并保留该钥匙原 SpriteRenderer Order（红钥匙为 201），不再强制提升到 1000。
- 点击已替换的按钮都会复用公共按钮原 `ClickScaleAnim` 播放按下与回弹，再播放一次 `hit`。远离 DoorLock 时只播放打击并恢复操作；距离 DoorLock 不超过 `2.6` 世界单位时，`hit` 与 DoorLock 的 `unlock` 同时播放。实际挥击时只播放场景 `Knock_Explosion` 的星形爆点根粒子，英文 `Knock 1` 子粒子在本关 Prefab 中关闭；爆点放大到 1.35 倍，并相对门锁偏移 `(-0.35, +0.25)`。
- 两段动画完成后隐藏 DoorLock、保留 `point_hit` 上的钥匙并启用 `door_0` 原出口 Collider。角色之后进入门，仍由既有 `BaseLevelCtrl.OnActorTrigger2D` 完成成功、星星、存档和关卡打点，不新增成功事件或直接写存档。

## 4. 角色控制器

Prefab 内 Actor 的共享 `ActorCtrl` 实例组件替换为 `Level113ActorCtrl : ActorCtrl`，移动、跳跃、碰撞参数保持原值。该子类只在一次性的 `obtain / hit` 播放期间阻止通用动画覆盖；动作结束立即恢复通用动画，钥匙的持续持有完全由 `point_hit` 挂点负责。

## 5. 验证记录

- Unity 6000.0.59f2 自带 Roslyn 对完整 `Assembly-CSharp` 加入四份 Level113 新脚本定向编译：`exit 0`、`0 error`；输出只有工程既有 warning。
- Prefab 自定义脚本 GUID、根控制器引用、Actor 替换引用和新增 fileID 已做静态闭合检查。
- Box Spine JSON 静态确认存在 `point_hit` 骨骼以及 `obtain`、`hang`、`hit` 动画。
- `Level18Ctrl` 只在 Level113 实例禁用；共享 `ItemJumpOutExecutor.Play()` 不变，新增 `PlayBetween` 仅供 Level113 固定新落点。

## 5.1 2026-09-07 PlayMode 反馈修复

- 修复开箱流程只恢复 `BaseLevelCtrl.ActorCtrlable`、未调用 `ActorCtrl.ResumeMoving()` 导致角色在第一只箱子旁永久停住的问题；所有本关临时锁定流程完成后现在统一恢复角色移动与输入。
- 补回旧开箱链中的 `Shiny` 闪光、完整 `box-open` 时长和 `box-open_idle` 收尾。
- 第一轮曾把下一把钥匙延后到开箱完成并把飞行时长改为 1.2 秒；后续试玩认为过慢，现行为以下方 5.2 节为准。
- 修复后使用 Unity 6000.0.59f2 Roslyn 完整响应文件编译为 `exit 0 / 0 error`，Unity Editor 随后成功重载程序集并重新导入 `Level_113.prefab`。

## 5.2 2026-09-07 第二轮 PlayMode 手感修正（已由 5.3 替代）

- 开箱流程不再调用 `SetCtrlable(false)` 或 `StopMoving()`；重复开箱和钥匙交互仍由 Level113 私有操作锁保护，但角色可在箱子播放动画时继续移动。
- 下一把钥匙改为与 `box-open` 同帧出现，先用 0.18 秒缩放回弹和渐显，再用 0.85 秒飞到统一台面，替代上一轮“等待开箱完成 + 1.2 秒飞行”。
- 当时曾增加 Actor Collider/Renderer 命中并让拖给主角的钥匙进入背后跟随；该历史方案已由 5.3 和 5.4 依次替代，当前不再使用。
- 修改后使用 Unity 6000.0.59f2 Roslyn 完整响应文件编译为 `exit 0 / 0 error`；仍需在 Unity PlayMode 目视确认速度、渐显以及移动中开箱的手感。

## 5.3 2026-09-07 第三轮原表现对齐

- 逐项读取 `Level_018.prefab` 的实际事件链，确认原开箱时序为“排序 → 缩放/飞入箱子 0.3 秒 → openChest/box-open → 等待 0.5 秒 → 延迟隐藏旧钥匙 → Shiny → 激活新钥匙”。Level113 现按该顺序执行，不再使用第二轮的同帧渐显、0.85 秒飞行或 `box-open_idle` 收尾。
- 箱子旧钥匙直接执行各箱实例已有的 `SwitchSortLayerListener`、`ItemScaleListener`、`ItemFlyPathListener`；新钥匙通过新增的 `ItemJumpOutExecutor.PlayBetween` 保留自身原随机时长、高度和曲线，只覆盖起点为当前箱子、终点为统一台面。
- 普通靠近拾取直接执行钥匙上原 `TriggerExecutor`，恢复原 0.3 秒等待和 `FollowTarget` 平滑跟随；本轮曾让拖拽命中 Actor 改走按钮道具链，已由 5.4 的最终澄清撤销。
- Unity 6000.0.59f2 Roslyn 完整响应文件编译为 `exit 0 / 0 error`；Prefab 中 5 个钥匙拾取 TriggerExecutor、下一钥匙 ItemJumpOut 及箱子原表现组件均已静态确认存在。

## 5.4 2026-09-07 拖放目标最终澄清

- 道具装备只接受拖到右下角 `Root/jump_btn`：替换按钮图标后，钥匙直接在角色 `point_hit` 出现，角色播放 `obtain` 并在完成后恢复操作、开放按钮点击；动作完成后的当前移动表现已由 5.7 更新为通用 `idle / walk / jump`。
- 拖到 Actor 本体或其它位置不触发装备，也不触发背后跟随，只按无效投放恢复台面钥匙；普通靠近碰撞仍独立复用原关的背后 `FollowTarget` 流程。

## 5.5 2026-09-07 橙色钥匙接触修复

- 橙色钥匙原 `TriggerExecutor` 的第二个节点仍调用已停用 `Level18Ctrl.NextKey`；由于删除黄色对象后旧 `Keys` 引用不完整，该调用抛出 `UnassignedReferenceException`，使后续排序、启用 `FollowTarget` 和关闭漂浮节点无法执行。
- Level113 Prefab 已从蓝、橙、绿、粉四把钥匙各自的拾取 `TriggerExecutor.items` 中移除仅负责旧 Level18 状态推进的 `EventHandleListener` 引用。`getKey` 音效、排序、漂浮开关、原等待节点（资源存在时）和 `FollowTarget` 节点保持原组件与原顺序；颜色链状态继续只由 `Level113Ctrl` 管理。

## 5.6 2026-09-07 point_hit 与按钮点击修复

- 根因是 Level113 Actor 仍继承共享 Actor 的旧 `sp_Box_001/Box_SkeletonData`，其中没有本次资源新增的 `point_hit / obtain / hang / hit`；运行时因此把钥匙降级放到 Actor 根节点上方，动作调用也只能无动画完成。
- `Level_113.prefab` 现仅覆盖本关 Actor 的 `SkeletonAnimation.skeletonDataAsset` 为 `sp_box_025/Box_SkeletonData`，不修改共享 Actor 和其它关卡。该资源静态确认含 `box_original` 皮肤、`point_hit` 骨骼和三段目标动画。
- 按钮安装移除 `toolFlyDuration` 与 DOTween 飞行链：场景钥匙在替换 UI 图标后直接归零挂到 `point_hit`，随即播放 `obtain`；完成回调再设置按钮可用，后续点击进入原 `hit`/门锁判断。

## 5.7 2026-09-07 持钥匙移动、按钮反馈与命中特效修复

- `obtain` 和 `hit` 完成后不再强制播放 `hang` 循环；`Level113ActorCtrl` 立即回到通用 `UpdateAnimation()`，因此角色继续使用原 `idle / walk / jump` 等状态，钥匙仍由 `BoneFollower(point_hit)` 持续跟随。
- Level113 继续禁用共享跳跃按钮的 Click 派发以避免普通跳跃，但单独复用该按钮已有的 `ClickScaleAnim`。鼠标或对应 fingerId 按下时缩小、抬起或取消时回弹，输入释放检测独立于打击期间的操作锁。
- 命中门锁时复用关卡内已有、默认不自动播放的 `Knock_Explosion`，在 `hit` 开始约 `0.18` 秒后对齐 DoorLock 世界坐标并播放；解锁完成只隐藏 DoorLock、开启出口，不再隐藏手持钥匙或清除 Actor 的持有状态。
- Unity 6000.0.59f2 Roslyn 完整 `Assembly-CSharp` 编译为 `exit 0 / 0 error`；公共 HUD 内 `jump_btn` 静态确认带有 `ClickScaleAnim(Scale=0.9, Delay=0.05, Duration=0.1)`，Level113 Prefab 静态确认存在 `Knock_Explosion` 粒子实例。

## 5.8 2026-09-07 持钥匙层级与纯击打爆点调整

- 持钥匙阶段不再把世界 SpriteRenderer 强制写为 Order 1000，而是恢复该钥匙缓存的场景原值；红钥匙原值为 Stuff/201，角色 SortingGroup 为 Stuff/200，因此钥匙仍在角色前方，但不会跨过更高场景层级造成角色、钥匙、箱子相交时的异常夹层。
- `Knock_Explosion` 两张纹理已核对：根粒子材质使用 `Sp_knock_1_01.png` 星形爆点，子节点 `Knock 1` 使用 `Sp_knock_1_02.png` 英文图。Level113 Prefab 仅关闭子节点，保留根粒子。
- 纯爆点实例缩放由 1 调到 1.35；播放坐标在 DoorLock 基础上增加世界偏移 `x=-0.35 / y=+0.25`。播放改为不包含子层级的 `ParticleSystem.Play(false)`，作为 Prefab 关闭英文节点之外的运行时保护。

## 6. 未验证项

- `uncertain`：Unity PlayMode 中四次开箱的实际触发手感、钥匙落点和背后跟随偏移。
- `uncertain`：不同角色位置与朝向下，钥匙直接出现在 `point_hit`、播放 `obtain` 后切回通用移动动画的视觉连续性。
- `uncertain`：不同分辨率、Safe Area、Android/iOS 多指输入下的按钮命中和顶层显示。
- `uncertain`：当前 Box Spine 美术下 `point_hit` 的最终旋转、缩放、`obtain/hang/hit` 视觉衔接。
- `uncertain`：纯击打爆点的 1.35 倍尺寸、左上 `(-0.35,+0.25)` 偏移、0.18 秒命中时机，以及持钥匙与箱子交叠时 Order 201 的最终观感。
- `uncertain`：修复后的橙色钥匙接触需在 Unity PlayMode 复测，并继续连续验证绿、粉钥匙不会再调用旧 `Level18Ctrl.NextKey`。

## 7. 回滚

1. 从 `Level_113.prefab` 移除 `Level113Ctrl`，恢复 Actor 源 `ActorCtrl` 组件和 `BaseLevelCtrl`/出生门 Actor 引用。
2. 重新启用 Level113 实例的 `Level18Ctrl`；若仍保留删除黄色对象的场景版本，必须同时修复其数组空引用和顺序，不能只恢复启用状态。
3. 删除 `Level_113` 下四份私有脚本及对应 meta。
4. 若回滚 Box Spine，需成组恢复 `Box.json`、`Box.atlas.txt` 和 `Box.png`，不能只回滚单个文件。
## 2026-10-10：钥匙按钮图标定位修复

### 修改前核对

- 需求与行为基线：用户确认 Level113 同样误替换背景，要求改 `bg` 下的正确图标。原装备链只应将箭头换成钥匙；共享按钮背景、根命中矩形、按压反馈、`obtain/hit`、开箱、解锁与出口流程须保留。
- 来源与维护边界：`Level113Ctrl.TryResolveJumpButton()` 当前遍历子 Image 并取首个非根节点；共享 HUD 调整后该节点是 `bg`。`Level_113.prefab` 使用 `GamePlayPanel_000`，基础面板 `jumpButtonIcon` 已显式绑定 `UI_youxizhong_icon_jiantou_01`（Image fileID `5393492493011141298`），现有 `GamePlayPanel.JumpButtonIcon` 可直接复用。
- 拟修改：仅替换本关控制器的图标取得代码，不增加名称查找或层级扫描兜底。安装、缓存、退出恢复继续作用于同一真实箭头；不改共享面板、Prefab、配置、资源、Actor、事件、GF 或 Packages。
- 验证场景：核对基础绑定及派生面板无覆盖，比较非目标代码一致并编译。运行验收包括逐色钥匙安装后背景不变、获得/击打与开锁正常、暂停和重开后恢复箭头；本轮 Unity PlayMode、Android/iOS 与 IL2CPP 尚未执行，标记 `uncertain`。
- 依据缺口：规则引用的当前 feature plan `../specs/037-remote-resource-loading/plan.md` 缺失；CodeGraph CLI 不可用。本轮依据本关现有设计说明、实际源码/Prefab 与用户明确指定的图标目标，不涉及远端资源加载。

### 回滚边界

- 仅撤销 `TryResolveJumpButton()` 本次图标取得改动；恢复旧遍历会重新引入误改背景，不可整体回滚共享 HUD 或本关其它功能。

### 修改结果与验证

- 已实施：`Level113Ctrl.cs:893` 改用现有 `GamePlayPanel.JumpButtonIcon`。缓存/安装/恢复继续操作真实箭头，不再操作背景；根命中范围、按压反馈、共享跳跃禁用、钥匙颜色链、角色 `obtain/hit`、开锁与清理代码均未改。
- 静态：`GamePlayPanel.prefab:2497` 的箭头绑定及 Jump→bg→箭头结构已核对；`GamePlayPanel_000` 未覆盖相关对象/绑定，`Level_113.prefab:86` 使用该面板。三关合计 12 项引用、层级与源码断言通过。
- 编译：Unity 6000.0.59f2 Roslyn 使用当前主程序集响应文件编译退出码 0、0 error、113 warning；三份修改脚本无诊断。日志为 `Temp/level-hud-icon-review-20261010/compile.log`。常规 dotnet 构建的缺少资产文件/依赖 DLL 失败不等于 Unity 源码错误，未据此改 SDK 或工程设置。
- 待运行验收（`uncertain`）：本地 LevelSort 124（内容 10113）逐色安装钥匙时背景保留、箭头被替换、按压与 `obtain/hit`、开锁和出口正常；暂停及 Reset/Replay/退出后箭头与交互恢复。Unity PlayMode、Android/iOS 和 IL2CPP 未执行。
