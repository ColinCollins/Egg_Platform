# Level71 关卡风险评估

## 原有行为基线

- `Level_071.prefab` 已有场景美术、长椅、太阳/月亮、夜色遮罩、夜灯、角色、标准起点门和标准出口门。
- 任务开始前用户已把根节点组件改为 `Level71Ctrl`，并把 HUD 临时改为 `GamePlayPanel_018_45`；`Level71Ctrl` 本身仍为空实现。
- 标准出口 `door_0` 的触发碰撞体初始关闭，符合“获得钥匙后才可过关”的基线。
- 商店免费钥匙 UI 领取已实现，但实体钥匙投放、拾取和出口解锁尚未实现。
- 坐下、3 秒睡眠、昼夜循环、起身冻结和月亮视差尚未实现。

## 影响文件、资源与隔离边界

- 直接修改：`Level71Ctrl.cs`、`Level_071.prefab`、本目录 Level71 中文功能/风险文档、`DevelopMD/已有功能设计与使用记录.md`。
- 新增：`Level71ActorCtrl.cs` 及其 `.meta`。
- 复用但不改写：`ActorCtrl`、`GamePanel_UseBtnCommonFeature`、`GamePlayPanel_018_45.prefab`、`ParallaxBackground`、`Key.prefab`、`door_0.prefab`、`DirectMove`、`MoveFloatHandle`、`LayerTriggerDetector`。
- 不修改：45 关脚本和 Prefab、共享 `ActorCtrl`、共享 GamePlayPanel 脚本、DB/Save/Luban、商店商品配置、全局事件、Analytics、广告/IAP、关卡打点生命周期。
- Reset/Replay 继续依赖 `PlayCtrl` 既有销毁并重建关卡实例的流程；本次未修改 `level_start`、`level_end`、revive、play_count 或成功/退出清理，与 `DevelopMD/关卡打点生命周期约定.md` 无冲突。

## 风险项

1. **P1：完整 PlayMode 链路尚未动态验证**
   - C# 编译和 Prefab 导入通过，但尚未实际操作坐下、等待 3 秒、起身、重复坐下、领取钥匙、拾取和进门。
   - 首轮 PlayMode 应优先检查空引用、按钮绑定时机、UnityEvent 参数匹配和出口碰撞体启用。
2. **P1：坐姿与睡眠动画对位**
   - `sofa_sleep` 已由 45 关 Actor Prefab 配置证明存在；本关沿用 45 关坐姿偏移。
   - 旧实现从 `ActorCtrl.PlaySit()` 刚设置 `IsSit` 时就开始 3 秒计时，包含了约 0.5 秒坐下 DOJump；现在只要 Actor Transform 仍有 DOTween 运行就清零计时，坐稳后才开始完整 3 秒。
   - 如果未来坐下流程在 Actor Transform 上引入持续循环 Tween，睡眠计时会一直被阻断；当前本地证据只有 0.5 秒坐下 DOJump，仍需 PlayMode 验证精确边界。
   - bench 的 `SitableIStuffCtrl` 已绑定到 `Level71Ctrl`，坐下/起身分别调用 `SitIn/SitOut`；当前 `worldShiftOnSit` 为零且没有 `frontParts`，因此只保留机关契约，不额外改变 bench 画面。
   - 长椅 Sprite 的尺寸和 Pivot 不同于 45 关沙发，实际角色臀部/脚部对位仍需美术验收。
3. **P1：UseBtn 触发范围**
   - bench 的 `OnTrigger2DHandle` 以 `Player` Tag 过滤，进入/离开事件已绑定同节点 `SwitchGamePanelUseBtnBoard`，继续由既有 `SwitchObjActiveEvent` 控制 HUD 按钮显隐。
   - Collider 当前覆盖完整 bench Sprite 尺寸，是否会过早显示或坐下跳跃期间意外触发离开仍需 PlayMode 验证。
4. **P1：昼夜轨道与背景渐变观感**
   - `Sun` 与 `Moon` 始终是激活的完整对象，控制器不识别内部图片，也不修改任何天体 Renderer 透明度；昼夜循环只改变二者相对 `center` 的局部位置，并持续保持 Prefab 初始朝向。
   - `CommonBg/Bg` 在夜晚色和白天色之间按独立颜色相位往返渐变；两端颜色和 `colorTransitionOffset` 已暴露到 Inspector。颜色相位为 `Repeat(_cyclePhase + offset, 1)`，Offset 范围 `-1~1`，正值提前、负值延后，Prefab 默认 `0`。
   - 非零 Offset 会有意让背景/场景颜色与太阳月亮轨道错相，但不改变轨道、循环速度或起身冻结逻辑；数值过大可能造成月亮在场时画面偏亮，需由美术结合构图验收。
   - 初始轨道角度来自 Prefab：月亮位于场上，太阳位于轨道另一侧。月亮原有自身旋转 Tween 已关闭，仍需 PlayMode 确认圆心、公转方向、天体进出画面时机、朝向稳定性、颜色过渡和逐渐加速观感。
   - `SpriteRendererGroupColor` 已接入同一插值颜色调用，当前 Prefab 已绑定组件及 5 个 Renderer 引用；成员范围和最终染色观感仍需美术验收。
   - 编辑器预览由 `Level71Ctrl.OnValidate()` 固定以初始昼夜相位 `0` 刷新颜色，只写现有 Renderer；不会调用 `ApplyDayNightState()`，因此不会把未初始化的运行时轨道缓存写回太阳/月亮位置。
5. **P1：远景视差与轨道旋转的组件归属**
   - `ParallaxBackground.LateUpdate()` 会写入所挂 Transform 的世界坐标；若继续挂在月亮上，会覆盖控制器计算出的月亮轨道位置。
   - 本关把该既有组件迁到 `center`，让整个太阳/月亮轨道系统共同保留水平远景视差。移动角色时的视差方向、幅度以及轨道运动叠加后的画面仍需 PlayMode 验收。
6. **P1：实体钥匙物理与过关链**
   - 复用标准 `Key.prefab`，落地由 Kinematic Rigidbody2D + Ground 层 Trigger 停止，拾取后启用 `door_0` 的标准 BoxCollider2D。
   - 下落起点不再运行时换算或覆盖，直接使用 Key Prefab 实例保存的初始局部位置；策划移动并保存该实例即可调整飞出位置，代码不缓存具体坐标。
   - DirectMove 当前初速度为 `(-2, 0)`，并由 `(0, -15)` 直接向下加速；最终落点仍受初始位置、地面高度和碰撞层影响。
   - 激活时根节点从 `scale 0` 以 `Ease.OutCubic` 缓动到 `scale 1`，默认时长 0.3 秒；缩放与位移同时发生，实际节奏仍需 PlayMode 验收。
   - 仍需确认钥匙不会在当前场景碰撞层设置下穿地、重复触发，并确认拾取后能进入标准门。
7. **P1：Reset/Replay 状态恢复依赖实例重建**
   - 当前关卡重建会重新执行初始夜晚、关闭钥匙和清空本地坐下/循环状态；免费领取状态也按新 `BaseLevelCtrl` 实例恢复。
   - 若未来改为同实例软重置，需要增加本关局部 Reset API；当前没有为未知未来方案新增全局事件。
8. **P2：临时复用 45 关 HUD**
   - 本关只依赖 `GamePanel_UseBtnCommonFeature.Use_btn`，没有调用 45 关控制器或其随机动画、进度条逻辑。
   - HUD 内其它 45 关专用视觉是否默认隐藏需 PlayMode 核对；专用 71 HUD 到位后应保持相同 Use_btn 契约。
9. **P2：横屏与固定初始位置**
   - 钥匙遵循关卡 Prefab 的固定初始位置，不再自动适配当前相机画幅；相机边界、布局或横屏宽高比变化后，需在 Android/iOS 分别确认钥匙仍能正常入画且不会落到不可达区域。
10. **P2：ShopPanel 关闭与延迟时序**
   - `HasClick` 在 UI 动画开始时即变为 true，但 `Level71Ctrl` 只启动等待协程；只有 `UIManager.GetUI<ShopPanel>()` 返回空后，才会用 `WaitForSecondsRealtime` 再等待默认 1 秒并激活钥匙。
   - 协程挂在当前 `Level71Ctrl` 实例上；Reset、Replay、退出或失败重建销毁关卡时会自动停止，`OnDestroy()` 也显式停止并清空引用，避免旧尝试的延迟回调投放到新关卡。
   - 仍需 PlayMode 确认销毁面板的实际帧边界、1 秒观感，以及关闭期间无钥匙闪现或输入穿透。

## 已完成验证

- 定向 C# 构建：`0 error / 101 个工程既有 warning`。
- Unity Editor 已完成 `Level71Ctrl.cs` 程序集重载及 `Level_071.prefab` 的 `PrefabImporter` 导入，新字段 `keyScaleDuration` 保留且旧 `keyDropViewportPosition` 已移除；日志未出现 Level71 编译或导入错误。
- 本轮 `ApplyDayNightState()` 已完成 Hot Reload，PrefabImporter 已导入并保留 `colorTransitionOffset: 0`；新增字段尚无完整 Domain Reload 后的 Inspector 显示证据。
- 本次涉及的 `Level71Ctrl.cs`、`Level_071.prefab` 与本关中文文档均通过 `git diff --check`；仅输出工作区行尾转换提示，没有尾随空格错误。
- 静态引用核对通过：HUD 名称、角色子类、长椅 Transform/Sitable、bench 进入/离开 UseBtn 触发事件、`center`、`Sun`/`Moon` 根节点、`CommonBg/Bg` Renderer、两端渐变颜色、共同圆心视差、实体钥匙策划初始位置、`keySpawnDelay = 1`、`keyScaleDuration = 0.3`、DirectMove `(-2, 0)/(0, -15)`、Ground 检测、出口碰撞体和 LockPoint 均存在明确本地引用。
- 复用边界核对通过：未改 45 关、共享 `ActorCtrl` 或共享 GamePlayPanel 脚本，未新增全局事件或存档字段。

## 未验证缺口

- `uncertain`：Unity PlayMode 的坐下对位、DOJump 完成后才开始完整 3 秒计时的精确边界、`sofa_sleep` 循环、起身后冻结和再次坐下续播。
- `uncertain`：进入/离开 bench Trigger 时 UseBtn 显隐、坐下跳跃期间按钮是否稳定、起身后的再次交互。
- `uncertain`：初始月亮位置、太阳/月亮绕 `center` 的公转方向与轨道、自身朝向是否稳定、正负 `colorTransitionOffset` 下背景夜晚色/白天色渐变和逐渐加速观感。
- `uncertain`：当前 5 个颜色组成员同步 RGB 渐变与各成员 Alpha 保持是否符合最终美术预期。
- `uncertain`：当前 Prefab 已存在 `SpriteRendererGroupColor` 组件和 5 个 Renderer 引用，但实际 PlayMode 渐变与 Prefab 再保存仍需 Unity 动态验证。
- `uncertain`：完整刷新或重启 Unity 后，`colorTransitionOffset` 是否正常显示、编辑和保存，以及正负偏移是否符合美术预期。
- `uncertain`：Prefab Mode/Scene View 连续拖动时的实时刷新、Undo/Redo 与 Prefab override 表现尚未人工验证。
- `uncertain`：移动角色时整个 `center` 天体系统的视差方向和系数是否满足美术预期。
- `uncertain`：免费钥匙从当前 Prefab 初始位置同时缩放和飞出、以 `(-2, 0)/(0, -15)` 轨迹落地悬浮、拾取飞向门锁、启用出口并完成过关。
- `uncertain`：Reset、失败重试、VictoryPanel Replay、快速开关商店与同尝试重复领取。
- `uncertain`：Android/iOS 横屏、安全区、多点触控和低帧率表现。

## 回滚路径

1. 恢复 `Level71Ctrl.cs` 为任务开始前的空占位实现，但保留用户已绑定的根组件。
2. 删除 `Level71ActorCtrl.cs/.meta`，把 Actor Prefab override 的脚本 GUID 恢复为共享 `ActorCtrl`。
3. 从 `Level_071.prefab` 移除 `center` 上的 `ParallaxBackground`、嵌入的 `Key.prefab`、钥匙新增运动/地面检测组件及本次 `Level71Ctrl` 序列化字段；若只回滚本次昼夜纠错，则恢复旧字段和组件归属即可，但会重新产生天体淡化或月亮轨道被覆盖的问题。
4. 不自动恢复 `gamePlayPanelName`、StartDoor 名称或其它任务开始前已有改动。
5. 删除或回退本次新增的关卡功能/风险记录和中央设计记录；商店免费钥匙 UI 的既有独立实现可单独保留。

## 2026-08-10：场景重排、动态钥匙 X 与镜头跟随风险补充

> 当前实现已替换上文“钥匙固定初始位置、不适配相机画幅”的历史基线。钥匙只保留 Prefab 初始 Y/Z，不再保留绝对世界 X。

### 已处理风险

1. **场景移动导致脚本断链**
   - Actor、bench、`SitableIStuffCtrl`、昼夜对象、Key、DoorLock、出口和 LockPoint 均通过对象/fileID 引用连接，不按旧世界坐标查找；本次静态检查未发现 Missing 本地引用。
   - DoorLock 与 `door_0` 移动后仍保持约 `0.2` 的相对 X 间距，本次没有改写用户保存的 Transform。
2. **P1：钥匙使用旧绝对 X 后出现在屏外或上一场景**
   - 生成前按当前本关相机执行 `WorldToViewportPoint -> x=0.5 -> ViewportToWorldPoint`，只重算世界 X；Prefab 当前 Y `4.69` 和 Z `0` 继续作为掉落高度/深度。
   - 相机、场景根或横屏画幅改变时仍以生成当帧的实际视口中心为准；无法取得相机或钥匙位于相机背面时记录 warning，并软回退到 Prefab 位置，避免空引用阻断领取链。
3. **P1：镜头开局提前移动或触发时突然居中**
   - 阈值使用 Actor 的当前视口 X，不写死世界坐标；`viewport.x < 0.6666667` 时不修改相机。
   - 首次触发保存 Actor 与相机 X 差，后续以该差作为锚点，所以角色保持在触发时约 `2/3` 的构图位置；相机只改 X，Y/Z 固定，最左钳制为开局 X。
   - 跟随锁存属于当前关卡实例，不复用会进出固定世界矩形的共享 `CameraFollowArea`，也不修改其它关卡。

### 剩余风险

- **P1 / `uncertain`：新地面与失败区边界。** 新增长地面碰撞右缘约为 X `24.99`，右侧 FallArea 左缘约为 X `25.17`，静态值存在约 `0.18` 间隙。出口位于更左侧，但仍需 PlayMode 走到右端确认 Actor 不会卡入或穿过边界。
- **P1 / `uncertain`：镜头触发和关卡结束构图。** 尚未动态验证 `2/3` 临界帧、低帧率快速越界、触发后回走、进入出口、死亡及 Reset/Replay；当前没有写死右侧最大相机 X，避免以未确认坐标裁切用户新场景。
- **P1 / `uncertain`：钥匙“生成点”与“最终落点”语义。** 生成起点位于当前屏幕中心，但既有水平初速 `-2` 会让落地点偏左；本次按“从屏幕中间掉落”保留原轨迹，没有擅自改成垂直落下。
- **P2 / `uncertain`：画幅与移动端。** 视口算法适配相机当前 rect，但 Android/iOS 超宽屏、4:3 横屏、安全区及低帧率表现尚未实机验证。

### 验证与回滚

- Bee/Roslyn `Assembly-CSharp` 编译：`0 error`，没有 Level71 新 warning。
- Prefab YAML：`183` 个文档且 fileID 全部唯一，本地引用缺失数为 `0`；四项新增序列化引用/参数存在且唯一。
- `git diff --check` 通过；未控制 Unity 界面，PlayMode/设备验证仍标记为 `uncertain`。
- 回滚时只删除本关相机跟随状态/方法、钥匙视口 X 换算及对应 Prefab 字段和 stripped Camera 引用；保留用户的 bench、门、失败区、美术和新地面坐标。

## 2026-08-10：商店出口生成与弹窗隔离风险补充

> 当前实现已由“屏幕中心 X + Prefab Y”更新为“优先使用商店领取动画终点 X/Y”；旧中心路径仅用于异常回退。

### 已处理风险

1. **Level071 领取钥匙后误弹通用奖励提示**
   - `ShopPanel` 只暴露当前实例的单向抑制入口，且只有通过 Level071 身份、暂停状态和领取防重校验后的免费钥匙点击会调用。
   - `OnOpen()` 继续把抑制字段复位；共享关闭按钮、其它商品、其它关卡和后续 ShopPanel 不受影响。跳过时不记录一次通用奖励提示展示。
2. **UI 飞出点与场景钥匙出现点断裂**
   - `Level71ShopBtnCtrl` 在 DOTween 完成、ShopPanel 销毁前读取 `animationTarget.position`，按根 Canvas 的 RenderMode/WorldCamera 换算成实际屏幕像素位置。
   - `Level71Ctrl` 在实际生成帧使用当前本关相机的 `ScreenToWorldPoint` 把该 X/Y 投影到 Key 原 Z 平面；不会把整屏归一化坐标误当成非全屏 GameCamera 的 viewport，相机或场景移动后仍保持屏幕出口语义。
3. **旧尝试静态坐标泄漏到 Reset/Replay**
   - 出口坐标只有 `HasClick` 对当前 `BaseLevelCtrl` 实例成立时才可读取；新尝试引用不匹配会直接回退，不新增 Reset 全局事件。
4. **采集依赖异常阻断领取**
   - Canvas、非 Overlay Canvas 相机和浮点结果均有检查；失败只输出 warning 并回退到中心 X/Prefab Y/Z，仍继续关闭商店和投放钥匙。

### 剩余风险与验证

- **P1 / `uncertain`：实际出口构图。** 动画终点按真实屏幕像素保留；若美术把 `(240, 180)` 调到屏幕外，实体钥匙也会从屏幕外进入。该行为保证连续性，但需 PlayMode 确认最终入口观感。
- **P1 / `uncertain`：Canvas 与世界相机组合。** 静态实现覆盖 Overlay、Screen Space Camera 和 World Space Canvas；仍需在当前运行层级验证 `rootCanvas.worldCamera` 接线和不同横屏画幅。
- **P1 / `uncertain`：完整关闭恢复链。** 抑制通用弹窗后 `ShopPanel.OnClose()` 会直接恢复暂停玩法；需动态确认无一帧输入穿透、无重复 Key，并完成落地、拾取、开门和结算。
- Bee/Roslyn 定向编译为 `0 error`；共享 `TryClosePanel()` 签名及普通调用未改。未执行 Unity PlayMode 或移动端验证。
- 回滚仅移除本次 ShopPanel 抑制入口、Level71 出口缓存与 X/Y 投影分支；不修改 `Level_071.prefab`，也不回退用户场景布局、镜头跟随和 DirectMove 参数。

## 2026-08-10：钥匙起落点修正风险补充

> 当前行为已由“ShopPanel 商品动画终点生成并交给 DirectMove”更新为“Gameplay 购物篮入口到商店前目标的关卡私有定点轨迹”；上文相关坐标交接和 DirectMove 风险仅作历史保留。

### 已处理风险

1. **起点来自错误 UI。** `Level71Ctrl` 直接读取当前 Gameplay HUD 的 `Shop2SkinGuideTarget`，使用按钮内部 `(0.2, 0.4)` 而非中心或 ShopPanel 商品节点；CanvasScaler、Overlay/Camera Canvas 的转换均经过 RectTransform 世界点和根 Canvas 相机。
2. **旧速度无法到达目标。** 正常路径关闭 DirectMove，以 `keyLandingViewportPosition = (0.59, 0.35)` 为硬终点；`(-2, 0)/(0, -15)` 仅在无法取得相机/Key 深度时作为软回退。
3. **镜头跟随造成落点漂移。** `1.1s` 飞行期间每帧按当前相机重新把固定视口起终点投影到 Key 的 Z 平面，完成帧再次落定目标；不会沿用生成时已过期的世界坐标。
4. **飞行途中被拾取或 Ground 打断。** 飞行前关闭 Collider、DirectMove 和 `MoveFloatHandle`；完成后先启动悬浮再恢复 Collider。Reset/Replay 销毁关卡时已有 `OnDestroy` 会杀掉飞行/缩放 Tween。

### 剩余风险、验证与回滚

- **P1 / `uncertain`：动态视觉验收。** 当前参数来自用户截图像素测量及 Collider 高度反推，仍需 PlayMode 确认不同横屏比例、Camera `pixelRect`、飞行弧度、钥匙大小与商店美术的实际观感。
- **P1 / `uncertain`：回退路径。** 主相机或 Key 深度异常时继续原 DirectMove，无法保证截图目标；该分支会输出 warning，正常 Prefab 已显式绑定 GameCamera。
- **P2：共享接口依赖。** 只读复用 `GamePlayPanel.Shop2SkinGuideTarget`，未改共享 HUD 资源或接口；若以后 HUD 更换按钮 Rect，本关仍会跟随新 Rect，但 `(0.2, 0.4)` 需重新视觉校准。
- Unity 6000.0.59f2 Roslyn 编译为 `exit 0 / 0 error`，Level071 修改文件无诊断；Prefab 为 `183` 个对象、`0` 个重复 fileID、`0` 个悬空本地引用，五个新字段各序列化一次，运行代码/Prefab 不再包含旧坐标字段，`git diff --check` 通过。未执行 PlayMode 或移动端验证。
- 回滚只移除本关定点轨迹方法/参数及 Prefab 对应字段，并恢复正常路径的 DirectMove；保留用户场景布局、镜头跟随、商店领取 UI和领取后弹窗抑制。

## 2026-08-10：飞行缓动与黄色 Area 右限风险补充

> 本节覆盖上文“当前没有写死右侧最大相机 X”的历史结论；当前上限来自截图黄色 Area 的右界和运行时相机宽度。

### 已处理风险

1. **固定 Linear 造成钥匙抛物线速度机械。** 正常路径改用可配置 `keyFlightEase`，Prefab 默认 `Ease.OutQuad`；仅改变贝塞尔 progress 的时间分布，几何轨迹、硬终点、飞行门禁和异常 DirectMove 回退不变。
2. **Actor 继续右行使镜头露出黄色 Area 右侧。** 右界固定为 `17.6`，最大相机中心按 `17.6 - orthographicSize * aspect` 动态计算；SmoothDamp 前后的值均钳制，避免宽高比变化或平滑残余越界。最小 Camera X 仍为开局 `0`，Y/Z 不参与跟随。
3. **误复用共享 Follow 扩大其它关卡风险。** 共享 `CameraFollowArea` 在角色离开 Area 时才跟随、回到 Area 时返回初始镜头，与本关锁存跟随语义相反；本次没有修改或挂载该共享脚本，也没有让两个 `LateUpdate` 同时争抢相机。

### 剩余风险、验证与回滚

- **P1 / `uncertain`：超宽画幅无法把整幅画面完全装入黄框。** 黄色 Area 宽 `24`，小于 2340:1080 下约 `25.57` 的相机世界宽度，因此实现只保证跟随时右边缘不超过黄色右界，不宣称整幅画面始终位于黄框内。
- **P1 / `uncertain`：动态观感。** OutQuad 与同时缩放的组合、镜头触边停顿、钥匙飞行期间恰好触边、低帧率及 Android/iOS 横屏仍需 PlayMode/真机验收。
- Roslyn 编译结果为 `0 error / 104 个工程既有 warning`，Level71 无诊断；Prefab 为 `183/183/0`（文档数/唯一 fileID/悬空本地引用），三个新增字段各序列化一次，`git diff --check` 通过。未操作 Unity 界面。
- 回滚只恢复 `Ease.Linear` 并移除 Ease/Area/Gizmo/动态上限；不得回滚用户 Transform、已确认的钥匙坐标、首屏 `2/3` 触发或领取流程。
