# Level108 功能与风险记录

## 需求与实现背景

- Level108 的场景 Prefab 由 Level20 复制，但只允许复用 `DoorEnemy/body` 的门 Spine；Level20 的门点击、跟随玩家移动、碰撞替换真门和无限背景滚动逻辑均不参与本关。
- 角色依次经过两个可调场景点：到 `DoorStandPoint` 时门从躺卧状态站起，到 `DoorJumpPoint` 时门才跳向绿色水管。L108 全程使用固定镜头。
- 唯一通关条件为：把场景下箭头拖到公共跳跃按钮，角色站在绿色水管顶面，再按已经变成下箭头的按钮，角色下沉进水管后通关。其它位置按下不触发通关。
- 用户已明确要求关卡通关逻辑不确定时先沟通；本记录不扩展其它成功或失败条件。

## 原始基线

- `Assets/Game/Prefabs/Level/Level_108.prefab` 已存在，为未跟踪的用户关卡资产；复制基线中根节点挂有 Level20Ctrl 和两个 InfiniteScrollMap，另有 4 个 FallArea，其中 3 个激活的 FallArea 通过 FollowTarget 跟随 Actor 的 X 坐标。
- `DoorEnemy` 原本挂有 DoorCtrl、PressItemEventHandle、DoorEnemyCtrl、物理刚体、碰撞体和碰撞执行器，属于 Level20 的可操控门逻辑。
- `Sp_l108_sz_01` 是绿色水管，已有 SpriteRenderer 和整管 BoxCollider2D，但原 Layer 0 不属于 Actor 的 `whatIsGround=320`。
- `Sp_l108_sz_03` 是场景下箭头，原来只有 SpriteRenderer，不能拖动。关卡配置已存在 `10108 -> Level_108`，本次未修改 JSON/Excel。

## 新行为与系统边界

- 新增 `Level108Ctrl`。Level20Ctrl 和两个 InfiniteScrollMap 已从 Level108 Prefab 根节点移除，不再依赖 Awake 运行时补关；DoorEnemy 的旧交互/移动组件保持禁用，门刚体停止模拟、旧碰撞体关闭，仅由本关脚本驱动 ActorSpineCtrl 与 SkeletonAnimation 表现。
- 门序列使用既有动画 `idle -> idle01 -> jump_up -> jump_down`，复用 `doorStandup` 和 `jump` 音效。站起与起跳由两个独立触发点控制；起跳改为一次连续 `DOJump` 抛物线。门越过最高点时只切换 `jump_down`，跳跃进度达到 75% 后才关闭影子并开始缩放到初始尺寸 60%、将 Spine alpha 渐变为 0；缩小渐隐持续覆盖剩余下落和入管下沉，最后才隐藏对象，避免刚到空中便开始消失或从管底漏出。
- 门的 `Shadow` 初始关闭，到 `DoorStandPoint` 才开启，开始入管时再次关闭。L108 的 GameCamera 已直接移除 CameraFollowArea 组件，关卡脚本也不再包含运行时查找/关闭镜头跟随的补丁逻辑。
- 4 个 Level20 遗留 FallArea Prefab 实例以及其中 3 个会把失败区 X 坐标贴到 Actor 的 FollowTarget 组件已全部从 Level108 删除，因此进入关卡时不会再把失败区拉到角色脚下。Level108 不新增失败区或其它死亡/重开条件。
- 水管切到 Ground Layer 6；原覆盖整根管体的 BoxCollider2D 收窄为只覆盖顶部的 `3.08 × 0.22` 薄碰撞，保留原管口高度，避免角色跳向管口时先撞到整面侧墙。没有修改共享 Actor Prefab 或 ActorCtrl。
- 新增 `Level108DownArrowDrag`，同时支持编辑器鼠标与 Android/iOS 单指拖拽。命中检测直接使用 SpriteRenderer 的可视边界，场景箭头不再挂 BoxCollider2D，因此不会阻挡或碰撞角色。拖动期间把同一箭头投到排序值 32760 的 ScreenSpaceOverlay Canvas，确保高于操作按钮；无效投放以 0.24 秒回原位。箭头中心命中 `Root/jump_btn` 后，先读取场景箭头的实际屏幕宽高并换算成按钮父级局部尺寸，再隐藏场景实例和替换按钮子 Image，避免 Canvas 缩放使安装后的箭头突然缩小。
- 安装后只禁用当前 GamePlayPanel 实例的 CustomButton，不改共享面板 Prefab；Level108 自己读取按钮矩形的按下输入。角色不在管口时按下无效果，站稳管口后才停止角色控制、隐藏 Shadow、切到管体下层并下沉。水平对齐与垂直下沉同时执行；下落距离取配置最小值与“当前启用 Renderer 顶边完全越过管口”所需距离的较大值，下沉时长为 1 秒；动画完成后才隐藏 Actor 并调用成功，避免成功界面出现后角色脚部仍露在管底。
- 下沉完成后先将 BaseLevelCtrl 的成功门表现设为空，再调用 `SuccessStrightly()`，因此不会回放 Level20 的门出口动画，仍走公共成功状态、存档和结算链。
- 角色只有脚底贴近管口顶面且中心 X 位于管宽内时才视为踩管；踩管及下沉期间隐藏 Shadow，离开管口则恢复原激活状态。

## 影响文件与资源

- `Assets/Game/Prefabs/Level/Level_108.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_108/Level108Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_108/Level108DownArrowDrag.cs`
- `Assets/Game/ArtSrc/Sprites/l108/Sp_l108_sz_01.png`、`Sp_l108_sz_03.png` 仅复用，未修改。
- Level20 脚本、共享 Actor、GamePlayPanel、BaseLevelCtrl、配置和 GF 子模块均未修改。

## 验证证据

- 核对现有配置：`tbleveldata.json` 唯一包含 `10108 / Level_108`，`tblevelsort.json` 包含显示路径 `10108`。
- 使用 Unity 当前 Bee `Assembly-CSharp` 响应文件，并显式加入两份 Level108 新脚本进行完整 Roslyn 编译：`exit 0`、0 个编译错误；输出仅包含工程既有 warning。
- Prefab YAML 最新静态检查：546 个对象定义全部唯一，546 个本地 fileID 引用全部可解析，0 个缺失；下箭头 BoxCollider2D 的组件引用与对象定义均为 0，门入管缩放比例为 0.6。
- 静态确认 Level108 根节点已无 Level20Ctrl/InfiniteScrollMap，Prefab 已无 CameraFollowArea、FollowTarget 或 FallArea；门点击/移动/碰撞执行器仍禁用，门刚体 `m_Simulated=0`，旧 PolygonCollider2D 关闭。
- 2026-09-03 修正后再次使用当前 Bee 响应文件全量 Roslyn 编译：`exit 0`、0 个编译错误；静态确认两个触发点均接入 Level108Ctrl、镜头跟随关闭、门初始 Shadow 关闭、水管薄碰撞与拖拽顶层 Canvas 参数已写入 Prefab。
- 2026-09-03 对持续重开原因重新核对 Prefab 后确认：3 个激活 FallArea 各自带 FollowTarget，并直接引用 Actor Transform；它们会在 LateUpdate 把失败触发区横向跟到角色处。先前用于清理“旧 Level108 实例”的 Awake 补丁已撤回，修复改为清除实际触发源，避免用实例销毁掩盖关卡资产错误。
- 两份 Level108 脚本使用当前依赖引用做定向 Roslyn 编译：`exit 0`、0 error。工程全量外部编译仍受工作区既有 Level078/083/103/104/105 生成域类型缺失影响，不能据此声明全工程通过；该问题与本次 Level108 改动无关。
- 2026-09-03 箭头去碰撞与门渐隐修正后再次定向编译两份 Level108 脚本：`exit 0`、0 error；Prefab 本地引用校验仍为 0 缺失。
- 2026-09-03 角色完整入管修正后，成功仍只由角色下落 Tween 的 OnComplete 回调触发；当前在完整位移动画结束后隐藏 Actor，再进入成功结算，避免成功瞬间残留脚部。两份 Level108 脚本定向 Roslyn 编译为 `exit 0`、0 error。
- 2026-09-03 箭头安装尺寸修正后，安装前的 SpriteRenderer 屏幕 Bounds 会换算到按钮 Image 的 RectTransform，且 Reset/Teardown 会恢复原图标尺寸；安装判定、按钮输入和过关条件均未修改。两份 Level108 脚本定向 Roslyn 编译为 `exit 0`、0 error。

## 风险与待验收项

- `DoorStandPoint` 当前 X=-1.93、`DoorJumpPoint` 当前 X=1.86；它们是默认初值，用户可在场景中直接移动。门的跳跃高度、落点偏移、切层时刻及完全进入管体的遮挡效果仍需 Unity PlayMode 目视验收，状态为 `uncertain`。
- 角色脚底容差当前为 0.28 世界单位；角色从管口边缘落下、移动中按下以及不同皮肤碰撞范围需要手感验收，状态为 `uncertain`。
- 不同皮肤和帽饰的 Renderer Bounds 会改变自动下沉距离；完全入管、管底不漏出和成功弹出时机仍需逐皮肤 PlayMode 目视验收，状态为 `uncertain`。
- 下箭头安装前后的视觉尺寸已按当前屏幕坐标换算；透明边缘、不同分辨率安全区和 Android/iOS 多点触控仍未进行设备验证，状态为 `uncertain`。
- 门在跳跃进度 75% 后开始缩放到 60% 与渐隐的节奏、管口遮挡衔接仍需 PlayMode 目视验收，状态为 `uncertain`。
- 需补验进入、下箭头无效投放、管外按下、踩管无影、成功、Reset/Replay、暂停恢复、退出、存档/解锁和关键打点。现有实现未修改公共打点代码。
- 必须完全退出当前 PlayMode 后重新进入 Level108，才能清掉本轮已经生成的 FallArea/重开异步状态；连续进入、重开和退出后的实机结果仍为 `uncertain`。
- 仓库规则引用的 `../specs/037-remote-resource-loading/plan.md` 当前缺失，与本关没有已知行为冲突，但该计划依据无法核对，状态为 `uncertain`。

## 性能与回滚

- 运行期只创建门和角色各一条 DOTween Sequence；下箭头仅在正在拖动时临时创建一份顶层 Canvas/Image，松手立即销毁。按钮查找和层级/碰撞引用只在初始化缓存，Update 不持续分配集合，移动端目标仍为平滑 60 fps。
- 回滚本次清理时可从 Level20 Prefab 恢复 Level108 根节点的 Level20Ctrl/InfiniteScrollMap、GameCamera 的 CameraFollowArea，以及 4 个 FallArea（其中 3 个带 FollowTarget）；这会重新引入失败区跟随 Actor 并可能持续重开的旧行为。Level20、共享 UI、Actor、BaseLevelCtrl 和配置均未修改。
- 只回滚本次表现修正时，恢复 `Sp_l108_sz_03` 的 BoxCollider2D，并删除 `doorInsidePipeScaleRatio`、`doorVanishStartNormalized`、门的 DOScale 和 Spine alpha Tween；无需回滚 FallArea/跟随清理或任何过关逻辑。

## 2026-10-10：下箭头替换目标修复

### 修改前核对

- 需求与基线：用户明确要求 Level108 安装下箭头时替换 `UI_youxizhong_icon_jiantou_01`，不能替换 `bg`。原 `TryResolveJumpButton()` 取第一个非根 Image；共享面板拆分背景后该选择会误取 `jump_btn/bg`。
- 来源与维护边界：`Level_108.prefab` 使用 `GamePlayPanel_000`；基础 `GamePlayPanel.prefab` 的 `jumpButtonIcon` 显式绑定箭头 Image `5393492493011141298`，由 `GamePlayPanel.JumpButtonIcon` 提供只读引用。图标替换、缓存与恢复均由 Level108 私有脚本维护。
- 拟修改：仅把 `Level108Ctrl.TryResolveJumpButton()` 的 Image 遍历改为读取现有显式绑定。保留 `CacheJumpButtonState()`、安装尺寸换算、根矩形命中、共享跳跃禁用、角色入管和退出恢复流程；不修改共享面板、Prefab、配置、其它关卡、GF 或 Packages。
- 验证范围：静态核对引用确实指向箭头且派生面板未覆盖；对比修改前后非目标代码一致；执行 C# 编译。需人工复测无效投放、有效安装后背景保持、管外按下无效果、管口按下通关、暂停及重开恢复；Unity PlayMode、Android/iOS 与 IL2CPP 尚未执行，标记 `uncertain`。
- 依据缺口：规则指定的 `../specs/037-remote-resource-loading/plan.md` 仍缺失；本轮依据用户明确的替换对象、现有本关功能记录和实际绑定实施，不涉及远端资源加载方案。CodeGraph CLI 不可调用，使用源码与 YAML 查证。

### 回滚边界

- 仅撤销本节对应的 `TryResolveJumpButton()` 图标取得改动，恢复先前 Image 遍历；此回滚会重新引入误替换背景的问题。不要整体恢复共享 Prefab 或其它关卡文件。

### 修改结果与验证

- 已实施：`Level108Ctrl.cs:375` 改用 `GamePlayPanel.JumpButtonIcon`，不再选择首个子 Image。缓存、安装与退出恢复的实现均未改变，改为作用于真实箭头；基础背景 Image `2140115765941960643` 不再是本关替换目标。
- 静态证据：基础面板 `GamePlayPanel.prefab:2497` 显式绑定箭头 Image `5393492493011141298`；箭头 RectTransform 的父节点是 Jump 的 `bg`。`GamePlayPanel_000.prefab:629` 继承基础面板，未覆盖 Jump/bg/箭头或绑定；`Level_108.prefab:166` 使用该面板。三关合计 12 项引用、层级与源码断言通过，未执行 Unity 实例化验证。
- 源码编译：使用 Unity 6000.0.59f2 的 Roslyn 与现有 `Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp.rsp`，当前全部主程序集源码编译退出码 0、0 error、113 warning，三份修改脚本无诊断。产物和日志仅写入 `Temp/level-hud-icon-review-20261010/compile.log` 等临时文件，不覆盖运行程序集。
- 构建失败边界：第一次 `dotnet build --no-restore` 缺少 `project.assets.json`；还原后禁用依赖项目构建的尝试报 200 个 `CS0006`（Temp 下依赖 DLL 未生成）。未据此修改工程或 SDK；后续直接使用 Unity 的实际引用参数完成上述编译。
- 待运行验收（`uncertain`）：本地 LevelSort 120（内容 10108）安装后原背景保留、仅箭头换下箭头、管外点击不通关、管口按下入管、暂停及 Reset/Replay/退出后恢复。Unity PlayMode、正式资源加载、Android/iOS 触控和 IL2CPP 未执行，不能宣称整关运行通过。
