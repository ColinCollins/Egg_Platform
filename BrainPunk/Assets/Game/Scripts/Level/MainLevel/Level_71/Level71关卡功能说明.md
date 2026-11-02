# Level71 关卡功能说明

## 需求与实现范围

- 权威需求：飞书《购物计划》及用户在当前任务中的补充确认。
- 关卡初始为夜晚，月亮位于场上；太阳和月亮两个完整对象始终激活，并位于共同圆心 `center` 的两侧。
- 点击 HUD 坐下按钮后，角色先完成坐到公园长椅的位移 Tween；坐稳且保持不动满 3 秒后进入循环睡眠，同时启动逐渐加速的昼夜循环。
- 起身后立即停止昼夜推进，并停留在当前昼夜画面；再次坐下仍需等待 3 秒，随后从冻结进度继续循环。
- 免费钥匙领取链与坐下/睡眠/昼夜链互不依赖。领取商店免费商品后，等待 `ShopPanel` 实例完全关闭，再以未缩放时间延迟 1 秒，实体钥匙从策划在 Prefab 中摆放的初始位置开始下落，并同步从 `scale 0` 缓动到 `scale 1`；拾取钥匙后复用标准出口解锁与过关流程。
- 商店始终只是装饰，不因为昼夜切换而开门。
- GamePlayPanel 按用户要求暂时复用 `GamePlayPanel_018_45`；本次只使用其 `GamePanel_UseBtnCommonFeature.Use_btn`。
- Reset、失败重试、Replay 或重新进入关卡通过既有关卡实例重建恢复初始夜晚、未坐下、未领取和未投放钥匙状态。

## 影响文件与职责

- `Level71Ctrl.cs`
  - 绑定 45 关 HUD 的 `Use_btn`，在本关内部切换坐下/起身。
  - bench 的 `OnTrigger2DHandle` 进入/离开事件绑定同节点 `SwitchGamePanelUseBtnBoard`，只在角色位于长椅交互范围时显示 HUD `Use_btn`。
  - 坐下/起身时同步调用 bench 上的 `SitableIStuffCtrl.SitIn()` / `SitOut()`，保持可坐机关表现契约与 45 关一致。
  - 等待角色坐下位移 Tween 完成后才维护完整 3 秒睡眠计时；坐下 Tween 仍在运行时每帧把计时归零。
  - 维护昼夜相位与逐渐加速速度；起身时只停止推进，不重置相位。
  - 昼夜推进时以 `center` 为圆心计算 `Sun` 与 `Moon` 根节点的轨道位置；不旋转 `center`，也不改变两个天体自身朝向或透明度。
  - 用 `Mathf.Repeat(_cyclePhase + colorTransitionOffset, 1)` 得到独立颜色相位，再把 `CommonBg/Bg` 从可配置的夜晚颜色渐变到可配置的白天颜色并返回；该 Offset 不改变天体轨道。
  - 同时把该插值颜色传给 `SpriteRendererGroupColor`，让 Inspector 明确配置的一组场景 SpriteRenderer 同步渐变，并保留每个成员自身 Alpha。
  - `OnValidate()` 在非运行态固定使用初始昼夜相位 `0` 调用同一颜色计算，因此拖动 `colorTransitionOffset` 或修改两端颜色时可在 Scene/Prefab 视图实时预览；预览不移动太阳月亮。
  - 读取 `Level71ShopBtnCtrl.HasClick`，等待 `UIManager` 中的 `ShopPanel` 实例消失后，再等待可配置的 `keySpawnDelay`（默认 1 秒）。
  - 延迟完成后不再覆盖钥匙位置，直接激活标准实体钥匙并保留 Prefab 初始 Transform；根节点以 `Ease.OutCubic` 在 `keyScaleDuration`（默认 0.3 秒）内从零缩放恢复到一倍缩放。
  - 钥匙落地后停止 `DirectMove` 并启用 `MoveFloatHandle`，等待角色拾取。
- `Level71ActorCtrl.cs`
  - 继承共享 `ActorCtrl`，只封装本关 `sofa_sleep` 循环动画。
  - 不向共享 `ActorCtrl` 增加关卡动画名或状态。
- `Level_071.prefab`
  - 继续使用任务开始前已经设置的 `GamePlayPanel_018_45` 与 `Level71Ctrl` 根组件。
  - 将长椅 Transform、`SitableIStuffCtrl`、`center`、`Sun`/`Moon` 根节点、`CommonBg/Bg` Renderer、实体钥匙和钥匙运动组件绑定到 `Level71Ctrl`。
  - `Level71Ctrl` 已绑定 `SpriteRendererGroupColor` 并同步调用，组成员由 Prefab Inspector 明确配置。
  - 修复 bench 的 `OnTrigger2DHandle.onEnter/onExit` 空目标引用，分别绑定到同节点 `SwitchGamePanelUseBtnBoard.OnEnterTrigger/OnExitTrigger`。
  - `Sun` 是一整个太阳、`Moon` 是一整个月亮；控制器不再单独识别、命名或绑定其内部图片，避免破坏组合美术。
  - 关闭月亮原有的自身旋转 Tween；昼夜循环每帧恢复太阳、月亮根节点的初始局部旋转，只改变局部位置。
  - 角色组件由共享 `ActorCtrl` 薄替换为 `Level71ActorCtrl`。
  - 既有 `ParallaxBackground` 挂到共同圆心 `center`，水平视差系数为 `0.15`；这样整个远景天体系统随角色移动，同时不会由 `LateUpdate` 覆盖控制器计算出的月亮轨道位置。
  - 嵌入标准 `Key.prefab`：初始关闭，下落起点直接使用策划在 Prefab 中摆放并保存的根局部位置，代码不缓存或写死坐标；初速度为 `(-2, 0)`、加速度为 `(0, -15)`。激活时根节点从 `scale 0` 缓动到 `scale 1`，时长由 `keyScaleDuration` 控制。拾取后启用本关 `door_0` 的出口触发碰撞体，并把钥匙飞向门的 `LockPoint`。
- `Level71ShopBtnCtrl.cs`、`ShopPanel.cs`、`ShopPanel.prefab`
  - 沿用已完成的免费商品显隐、同尝试防重复、右上飞走渐隐和统一关闭商店逻辑。

## 行为前后对比

### 修改前

- `Level71Ctrl` 为空占位脚本。
- 长椅没有 HUD 交互，角色不能在本关进入睡眠。
- 太阳、月亮、夜色遮罩与夜灯同时保持 Prefab 静态状态，没有初始夜晚或昼夜循环。
- 月亮没有本关运行态视差。
- 商店免费商品只能完成 UI 领取，实体钥匙、拾取和出口解锁尚未接入。

### 修改后

1. 关卡实例创建时应用夜晚轨道相位：`center` 保持 Prefab 初始角度，完整月亮位于场上，完整太阳位于轨道另一侧；两个对象都保持激活且不做透明度淡入淡出。`colorTransitionOffset = 0` 时 `CommonBg/Bg` 使用夜晚颜色，非零时按偏移后的颜色相位显示。
2. 角色进入 bench 的 Trigger 后，既有 `SwitchObjActiveEvent` 显示 HUD `Use_btn`；离开 Trigger 后隐藏按钮。按钮第一次点击让角色复用 `ActorCtrl.PlaySit(...)` 坐到 bench，并同步调用 `SitableIStuffCtrl.SitIn()`；坐姿偏移沿用 45 关 `(0.3, -0.15, 0)`。
3. 角色坐下期间只要 Transform 仍有 DOTween 位移，3 秒计时就保持为 0；坐稳后完整保持不动 3 秒，`Level71ActorCtrl` 才循环播放 `sofa_sleep`，昼夜相位从当前值开始推进。
4. 控制器用同一轨道四元数分别旋转太阳、月亮相对 `center` 的初始位置向量，使二者沿圆形轨迹公转；两个天体根节点始终保持 Prefab 初始朝向，不产生自转。`CommonBg/Bg` 与人工配置的 `SpriteRendererGroupColor` 同步按偏移后的颜色相位做夜晚→白天→夜晚渐变。`colorTransitionOffset` 以完整周期为单位，正值提前、负值延后；当前速度从 `0.01 cycle/s` 逐渐加速到 `0.3 cycle/s`，加速度为 `0.012 cycle/s²`。
5. HUD 再次点击调用 `StopSit()` 和 `SitableIStuffCtrl.SitOut()` 起身；睡眠与昼夜推进停止，但昼夜相位和当前速度保留。
6. 免费商品一经领取，`Level71Ctrl` 先等待 `ShopPanel` 实例真正关闭，再通过 `WaitForSecondsRealtime` 等待 `keySpawnDelay = 1s`。延迟完成时保留 Key Prefab 当前初始位置，以初速度 `(-2, 0)`、加速度 `(0, -15)` 落下，并在默认 0.3 秒内从 `scale 0` 缓动到 `scale 1`。
7. 钥匙接触 Ground 层后停止下坠并悬浮；角色触发标准 `Key.prefab` 拾取链后，出口门碰撞体启用，进入门继续由 `BaseLevelCtrl` 标准成功流程处理。

## 数据、资源、UI 与事件流

```text
坐下链（独立）
GamePlayPanel_018_45.Use_btn
  <- bench.OnTrigger2DHandle
  <- SwitchGamePanelUseBtnBoard 派发 SwitchObjActiveEvent 控制显隐
  -> Level71Ctrl.ToggleSitting
  -> ActorCtrl.PlaySit + bench.SitableIStuffCtrl.SitIn
  -> 等待坐下 Transform Tween 完成
  -> 坐稳且保持不动 3 秒
  -> Level71ActorCtrl.PlaySleepLoop(sofa_sleep)
  -> Level71Ctrl 更新 Sun/Moon 围绕 center 的轨道位置，并保持天体自身朝向
  -> colorPhase = Repeat(cyclePhase + colorTransitionOffset, 1)
  -> 按独立颜色相位在夜晚色与白天色之间渐变 CommonBg/Bg 与颜色组
  -> 昼夜相位逐渐加速
  -> 再点 Use_btn / StopSit + bench.SitableIStuffCtrl.SitOut
  -> 冻结当前昼夜相位

钥匙链（独立）
ShopPanel 免费商品点击
  -> Level71ShopBtnCtrl.HasClick = 当前关卡实例已领取
  -> Level71Ctrl 等待 UIManager.GetUI<ShopPanel>() == null
  -> WaitForSecondsRealtime(keySpawnDelay，默认 1 秒)
  -> 保留 Key.prefab 的策划初始位置并激活
  -> 根节点 scale 0 -> 1（keyScaleDuration，默认 0.3 秒）
  -> DirectMove 下坠
  -> 接触地面后 MoveFloatHandle 悬浮
  -> 角色拾取标准 Key.prefab
  -> 启用 door_0 的 BoxCollider2D
  -> BaseLevelCtrl.OnActorTrigger2D / 标准过关
```

- 数据：仅使用当前关卡实例内存状态和 `Level71ShopBtnCtrl` 当前尝试引用，不访问 DB、Save、RemoteConfig 或 Luban。
- 资源：完整复用本关 `Sun`、`Moon` 组合层级以及角色现有 `sofa_sleep`、标准 `Key.prefab`、`door_0.prefab`、`ParallaxBackground` 和 45 关 HUD；不拆分内部图片，不改变天体透明度，也不新增运行时资源加载。
- 美术配置：`nightBackgroundColor`、`dayBackgroundColor` 与 `colorTransitionOffset` 均暴露在 `Level71Ctrl` Inspector；Prefab 当前夜晚色为 `(0.22745098, 0.22745098, 0.30588236, 1)`，白天色为白色 `(1, 1, 1, 1)`，Offset 默认 `0`。Offset 范围为 `-1~1`，`+0.25` 提前四分之一周期，`-0.25` 延后四分之一周期。
- 编辑器预览：`OnValidate()` 最外层先排除 `Application.isPlaying`，随后只读取本组件序列化颜色/Offset，并写入已绑定的 `SpriteRenderer` 与 `SpriteRendererGroupColor`；不访问 DB、Save、RemoteConfig、Manager.Instance，不创建对象，也不执行运行时昼夜/钥匙/UI 链路。
- 颜色组配置：`SpriteRendererGroupColor.renderers` 提供 Inspector 手工列表；当前 Prefab 已绑定组件及 5 个 Renderer 引用，尚未列入的美术节点不会被染色。
- UI：不修改共享 `GamePlayPanel` 脚本，只由本关控制器订阅现有 `Use_btn.OnClick`，销毁时解除订阅。
- 事件：不新增全局 `PlayEvents`；坐下采用局部按钮回调，钥匙拾取沿用 Prefab 内局部 UnityEvent。
- 平台：钥匙使用关卡 Prefab 的固定初始位置，延迟使用未缩放时间，缩放动画和 DirectMove 使用正常游戏时间；Android/iOS 横屏共用同一代码路径，相机边界或关卡布局调整后仍需确认钥匙入画与落点。

## 验证

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo`：`0 error / 101 个工程既有 warning`。
- Unity Editor 已完成 `Level71Ctrl.cs` 程序集重载，并由 `PrefabImporter` 成功重新导入包含 `keyScaleDuration` 的 `Level_071.prefab`；日志未出现 Level71 编译或 Prefab 导入错误。
- 本轮 `ApplyDayNightState()` 已被 Hot Reload，`Level_071.prefab` 也成功导入并稳定保留 `colorTransitionOffset: 0`；但新增序列化字段尚未取得完整 Domain Reload 后的 Inspector 显示证据。
- `git diff --check`：本次涉及的 `Level71Ctrl.cs`、`Level_071.prefab` 与本关中文文档均通过；仅输出工作区行尾转换提示，没有尾随空格错误。
- 静态绑定核对：长椅 Transform/Sitable、bench 进入/离开 UseBtn 触发事件、`center`、`Sun`/`Moon` 根节点、`CommonBg/Bg` Renderer、两端渐变颜色、`colorTransitionOffset = 0`、Level71ActorCtrl、共同圆心视差、Key.prefab 策划初始位置、`keySpawnDelay = 1`、`keyScaleDuration = 0.3`、DirectMove `(-2, 0)/(0, -15)`、门碰撞体和 LockPoint 引用均已写入本关 Prefab。
- `uncertain`：尚未执行 Unity PlayMode 的坐下 Tween 完成后再计时完整 3 秒、睡眠、反复起身/坐下、昼夜观感、钥匙从初始位置缩放飞出、落地/拾取、过关及 Reset/Replay 动态验证。
- `uncertain`：首次在 Inspector 调整 `colorTransitionOffset` 前，需完整刷新或重启 Unity，确认新增字段正式进入序列化布局；随后分别验证 `0`、正值和负值的初始颜色进度。
- `uncertain`：需在完整刷新后的 Prefab Mode/Scene View 实际拖动 Offset，确认滑条连续变化时两个颜色目标同步刷新且不会产生不期望的 Prefab override。

## 剩余风险与回滚

- 坐姿偏移、昼夜旋转方向与速度、背景两端颜色、天体轨道范围和钥匙抛落参数已经可配置，但最终观感仍需策划/美术在 PlayMode 验收。
- 2026-08-07 昼夜表现最终修正：需求明确 `Sun` 是完整太阳、`Moon` 是完整月亮，二者必须同时保留并围绕共同 `center` 公转，但自身不能旋转。最终实现只改变两个完整根节点相对圆心的位置并维持初始朝向，不再旋转共同父节点，也不缓存或改写任何天体子 Renderer 透明度；背景仅渐变 `CommonBg/Bg` 的颜色。
- 临时复用的 45 关 HUD 可能包含 71 关不需要的视觉节点；后续专用 HUD 到位时只替换 `gamePlayPanelName` 并保留 `GamePanel_UseBtnCommonFeature` 契约即可。
- 回滚本次完整关卡逻辑时：恢复 `Level71Ctrl.cs` 为空占位；删除 `Level71ActorCtrl`；把 Actor 脚本引用恢复为共享 `ActorCtrl`；移除月亮视差和嵌入钥匙；移除本次新增序列化绑定。
- 任务开始前已有的 `Level71Ctrl` 根组件、`GamePlayPanel_018_45`、StartDoor 名称修正及其它用户工作区改动不属于本次回滚目标，不应自动覆盖。

## 2026-08-10：场景重排适配、动态钥匙落点与延迟镜头跟随

> 本节是当前有效行为，明确覆盖上文 2026-08-07 关于“钥匙固定从 Prefab 初始 X 生成、代码不按相机换算”的历史说明；钥匙的 Prefab 初始 Y/Z 和原下落轨迹仍继续保留。

### 需求与影响范围

- 用户已在 `Level_071.prefab` 内移动长椅、门锁、出口、失败区、美术节点并补充右侧长地面；本次先核对这些移动是否破坏本关绑定，不回退或重新摆放用户坐标。
- 免费钥匙不再依赖绝对世界 X；商店领取、ShopPanel 完全关闭并延迟 1 秒后，以当前本关相机的横向视口中心 `0.5` 作为生成 X。
- 镜头开局保持静止；角色到达首屏横向视口 `2/3` 后，本轮关卡锁存启动横向跟随。
- 只修改 `Level71Ctrl.cs` 与 `Level_071.prefab` 的本关逻辑和接线；没有修改 `Level71ShopBtnCtrl`、共享 `CameraFollowArea`、`ActorCtrl`、商店、存档、配置、资源地址、全局事件、Analytics 或平台分支。

### 当前行为

1. `Level71Ctrl` 在 `Awake` 缓存本关 GameCamera 的初始世界位置；Prefab 显式绑定相机，绑定异常时只在当前关卡子层级及 `Camera.main` 中兜底查找。
2. `LateUpdate` 用 `WorldToViewportPoint` 判断 Actor 的屏幕位置。达到 `cameraFollowStartViewportX = 0.6666667` 前，相机保持开局坐标。
3. 首次达到阈值时记录 Actor 与相机的世界 X 差作为锚点，因此开始跟随不会把角色从屏幕 `2/3` 突然拉到中线。之后只用 `Mathf.SmoothDamp` 更新相机 X，平滑时间为 `0.2s`；Y/Z 始终保持开局值，最左不越过开局相机 X。
4. 跟随状态在当前关卡实例中只会从未触发变为已触发；角色回走不会取消状态，但相机可以随角色回到开局 X。Reset/Replay 销毁并重建关卡后重新从静止镜头开始。
5. 生成钥匙前，控制器读取钥匙当前世界位置，转为当前相机视口坐标，只把视口 X 改为 `keySpawnViewportX = 0.5`，再转回世界坐标；随后显式恢复原世界 Y/Z。因此策划仍可通过移动 Key Prefab 实例调整掉落高度和深度，场景整体或相机移动不会让钥匙沿用旧绝对 X。
6. 钥匙原有 `DirectMove` 初速度 `(-2, 0)`、加速度 `(0, -15)`、缩放、Ground 停止、悬浮、拾取和开门链均不变；“屏幕居中”指生成起点，原轨迹会使最终落地点相对生成点偏左。

### 场景移动核对

- `Level71Ctrl` 的 Actor、bench Transform、`SitableIStuffCtrl`、昼夜节点、颜色组、Key 及运动组件仍全部解析到有效本地 fileID；移动 Transform 不会断开这些对象引用。
- DoorLock 当前 X 为 `15.09`，`door_0` 当前 X 为 `15.289999`，仍保留约 `0.2` 的原相对间距；标准钥匙飞锁与出口引用未改变。
- 新增长地面将可见/碰撞范围延伸到右侧场景。静态几何换算显示其右缘与右侧 FallArea 左缘约有 `0.18` 世界单位间隙；该位置在出口之后，但仍需 PlayMode 实走确认不会出现意外掉落。

### 验证、未验证项与回滚

- Unity 6000.0.59f2 Roslyn 按当前 Bee `Assembly-CSharp.rsp` 定向编译通过：`0 error`，只有工程既有 warning，没有 Level71 新 warning。
- `Level_071.prefab` 静态检查为 `183` 个 YAML 文档、`183` 个唯一 fileID、`0` 重复、`0` 缺失本地引用；新增相机引用及 `0.6666667 / 0.2 / 0.5` 三项参数均恰好序列化一次。
- `git diff --check` 通过；未操作 Unity 界面，也未执行 PlayMode 或 Android/iOS 真机验证。
- `uncertain`：需要在 PlayMode 验证三种横屏宽高比下的 `2/3` 触发、触发瞬间无构图跳变、回走/Reset、不同镜头位置领取钥匙、钥匙落地可达，以及新地面右缘的碰撞连续性。
- 只回滚本节时，删除 `Level71Ctrl` 的 Camera Follow 区域和钥匙视口 X 换算，移除 Prefab 中 `mainCamera`、`cameraFollowStartViewportX`、`cameraFollowSmoothTime`、`keySpawnViewportX` 及新增 stripped Camera 引用；不得回滚用户本次移动的场景 Transform 或新增长地面。

## 2026-08-10：领取后弹窗隔离与商店出口交接

> 本节是当前有效的钥匙生成行为，覆盖上一节“固定使用当前屏幕中心 X、保留 Prefab Y”的说明；中心 X 现在只作为出口坐标采集失败时的回退。

### 需求与影响范围

- 仅在 Level071 有效点击免费钥匙后，关闭 ShopPanel 时不再自动创建 `ShopRewardTipPopup`；Level071 未领取钥匙时的普通关店、其它关卡和后续 ShopPanel 实例保持原行为。
- 实体钥匙不再从屏幕中心开始下落，而是衔接 `L71_Obj` 向右上飞走动画的结束点，使 UI 钥匙消失位置与场景钥匙出现位置一致。
- 直接涉及 `Level71ShopBtnCtrl.cs`、`Level71Ctrl.cs` 和 `ShopPanel.cs`；本次不改 `Level_071.prefab`、`ShopPanel.prefab`、场景 Transform、配置、存档、广告发奖或钥匙运动参数。

### 当前流程

```text
Level071 有效点击免费钥匙
  -> 当前 ShopPanel 标记本次关闭不自动显示 ShopRewardTipPopup
  -> L71_Obj 按既有参数向右上飞走并渐隐
  -> 动画终点由所属根 Canvas 换算为实际屏幕像素坐标
  -> ShopPanel 沿原 TryClosePanel / OnClose 链关闭并恢复玩法
  -> 等待 ShopPanel 销毁和 keySpawnDelay
  -> 用生成当帧的 Level071 Camera 通过 ScreenToWorldPoint 换算到 Key 原 Z 平面
  -> Key 缩放出现并继续既有 DirectMove、落地、拾取和开门链
```

- 弹窗抑制状态属于当前 ShopPanel 实例，`OnOpen()` 会复位；跳过这一次展示不会调用 `RecordShow()`，因此不会消耗后续普通商店的每日提示机会。
- 出口数据同时受 `claimedAttemptOwner` 和当前 `Level_071` 身份校验；Reset、Replay 或重新进入后旧关卡实例的坐标不会被新尝试读取。
- 屏幕坐标保留 UI 动画的实际像素终点，不假定 GameCamera 的 `pixelRect` 占满整个屏幕。若 Canvas、非 Overlay Canvas 相机或坐标无效，则记录 warning，并回退到原 `keySpawnViewportX = 0.5` 加 Prefab Y/Z 的生成方式。
- DirectMove 初速度 `(-2, 0)`、加速度 `(0, -15)`、`keySpawnDelay`、缩放、落地悬浮、拾取、开门和镜头跟随均未修改。

### 验证、缺口与回滚

- Unity 6000.0.59f2 Bee/Roslyn `Assembly-CSharp.rsp` 定向编译通过：`0 error`，只有工程既有 warning。
- 静态调用核对确认：共享关闭按钮仍调用无参数 `TryClosePanel()`；只有 `Level71ShopBtnCtrl` 调用本次弹窗抑制 API。
- 本次未操作 Unity 界面；`uncertain`：仍需 PlayMode 验证弹窗不出现、UI/场景钥匙衔接点、不同横屏画幅、Canvas 相机、镜头已跟随时的位置、Reset/Replay 与完整过关链。
- 回滚本节只需移除 `ShopPanel.SuppressRewardTipAutoShowOnClose()`、Level71 的出口坐标采集/读取及生成时的 X/Y 分支，恢复中心 X 回退为主路径；不得回滚用户场景坐标、镜头跟随或钥匙既有运动参数。

## 2026-08-10：免费钥匙起点与落点最终校准

> 本节为当前有效行为，覆盖上文“衔接 ShopPanel 内 `L71_Obj` 动画终点”和“由 DirectMove 决定最终落点”的历史说明。领取后不弹通用奖励提示、商店关闭和同尝试防重复逻辑保持不变。

### 需求、范围与流程

- 用户截图要求场景钥匙从 Gameplay 右上购物篮的左侧偏下入口飞出，并落到画面中部商店前的箭头尖端；旧实现把 ShopPanel 商品动画终点当作入口，且 DirectMove 的水平初速只能把钥匙带到画面偏右处。
- 本次行为修改仅位于 `Level71Ctrl.cs` 与 `Level_071.prefab`。`Level71ShopBtnCtrl.cs` 继续负责领取动画和本关弹窗抑制，但不再提供场景钥匙坐标；没有修改共享 `GamePlayPanel`、`ShopPanel`、`DirectMove`、Key Prefab、存档、配置、事件、广告、打点或其它关卡。
- 起点优先读取当前 `GamePlayPanel.Shop2SkinGuideTarget`，在按钮 Rect 内使用可配置归一化点 `keyShopButtonStartNormalizedPosition = (0.2, 0.4)`；该点经根 Canvas 和 UI Camera 换算为屏幕像素，再换算到本关相机视口。引用异常时使用 `keyFallbackStartViewportPosition = (0.915, 0.875)`。
- 落点固定为生成/飞行期间当前本关相机的 `keyLandingViewportPosition = (0.59, 0.35)`。截图静态测量与地面/钥匙 Collider 高度反推均落在商店前目标区域，且钥匙底边接近地面顶面。
- 飞行由本关私有二次贝塞尔轨迹驱动，默认 `1.1s`、世界高度 `1`；相机视口起终点在 Tween 每帧重新投影，因此镜头平滑跟随期间最终画面落点仍为 `(0.59, 0.35)`。飞行中禁用 Key Collider、DirectMove 和悬浮，完成后先启用悬浮再恢复 Collider；只有相机/深度解析失败时才回退原 DirectMove。

### 验证、缺口与回滚

- Unity 6000.0.59f2 自带 Roslyn 使用当前 `Assembly-CSharp.rsp/.rsp2` 编译通过：`exit 0 / 0 error`，输出均为工程既有 warning，Level071 修改文件无诊断。
- 静态核对确认 HUD 按钮 Rect、截图红线起点、目标 viewport、地面顶面和钥匙 Collider 高度相互吻合；Prefab 为 `183` 个 YAML 文档、`183` 个唯一 fileID、`0` 个悬空本地引用，五个新参数的代码声明和 Prefab 序列化均各一处。
- 本次未控制 Unity 界面，也未执行 PlayMode、不同横屏比例或 Android/iOS 真机视觉验收；窄屏 Camera `pixelRect`、实际飞行手感、Reset/Replay、拾取和过关全链仍为 `uncertain`。
- 回滚本节时移除 `Level71Ctrl` 的按钮局部点换算、视口贝塞尔飞行和五个钥匙定位/轨迹字段，恢复正常路径启用原 DirectMove；不要回滚用户已调整的场景 Transform、镜头跟随、免费商品 UI 或本关弹窗抑制。

## 2026-08-10：钥匙飞行节奏与黄色 Area 镜头右边界

> 本节为当前有效行为，补充上一节的定点飞行，并覆盖“镜头没有右侧最大 X”的历史说明。用户已确认钥匙起点、落点等其它表现，本次不再改动这些位置。

- 修改范围仅为 `Level71Ctrl.cs`、`Level_071.prefab` 和本关文档；没有修改共享 `CameraFollowArea`、`CameraFollowRange`、HUD、ShopPanel、Key/DirectMove Prefab、用户场景 Transform、存档、配置、事件、打点或其它关卡。
- 钥匙二次贝塞尔的进度缓动由新增 `keyFlightEase` 控制，Prefab 默认 `Ease.OutQuad`（序列化值 `6`）。它让前段移动更利落、落点前自然收速；`1.1s` 时长、弧高 `1`、HUD 起点 `(0.2, 0.4)`、回退起点 `(0.915, 0.875)`、落点 `(0.59, 0.35)`、缩放与 Collider/悬浮/拾取链均不变。
- 镜头仍在开局静止，Actor 到达横向 viewport `0.6666667` 后锁存启动，并保持触发时 Actor/Camera X 锚点，以 `0.2s` SmoothDamp 只跟随 X。黄色 Area 为世界坐标 `center=(5.6,-0.25)`、`size=(24,14)`，右界为 `17.6`；最大 Camera X 运行时按 `max(initialX, 17.6 - orthographicSize * aspect)` 计算，16:9 约为 `7.11`，2340:1080 约为 `4.82`。目标值和平滑后的实际值都会再次钳制，Camera Y/Z 固定。
- 选中 `Level71Ctrl` 时会显示黄色 Area Gizmo；Area 的 Y/高度仅用于 Scene 视图标记。该 Area 定义的是“跟随后不越过的右侧可视边界”，不强制开局画面完整包含在黄框内，也不是共享 `CameraFollowArea` 的“角色在框内时返回初始镜头”死区语义。
- Unity 6000.0.59f2 自带 Roslyn 使用当前 Bee `Assembly-CSharp.rsp` 编译通过：`0 error / 104 个工程既有 warning`，`Level71Ctrl.cs` 诊断为 `0`。Prefab 静态扫描为 `183` 个 YAML 文档、`183` 个唯一 fileID、`0` 个悬空本地引用；三个新增参数各序列化一次，`git diff --check` 通过，仅有行尾转换提示。
- `uncertain`：未操作 Unity 界面，尚未在 PlayMode/Android/iOS 验收 OutQuad 手感、两种横屏画幅的右侧停点、回走/Reset，以及钥匙飞行期间镜头同时触边的观感。
- 仅回滚本节时，把钥匙 Tween 恢复为 `Ease.Linear` 并移除 `keyFlightEase`，同时移除黄色 Area 参数、Gizmo 和动态右限；保留此前已确认的起落点、首屏 `2/3` 触发和用户场景布局。
