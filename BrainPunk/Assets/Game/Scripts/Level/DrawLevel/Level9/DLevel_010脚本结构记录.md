# DLevel_010 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 10 关
- 更新日期：2026-08-02
- 交付/验收状态：Alien 可见入场、钩子打开与 ball 释放已实现；Unity PlayMode 和设备验收未完成
- 参考基线关卡：DLevel_002、DLevel_006
- 选择基线理由：DLevel2 提供 `IDrawLevelVisibleIntroHandler`、Alien 入场门禁和 Kinematic
  ball 初态恢复结构；DLevel6 提供 catch Animator `isOpen` 与 ball 同步释放表现
- 基线证据限制：DLevel2 文档仍将整关 PlayMode 与移动端验收标为未完成，因此验收状态为
  `uncertain`；本关只复用已有代码与 prefab 结构，不据此声明运行表现已验收
- 主要脚本：`DLevel10Ctrl.cs`、共享 `DrawEntrancePathView.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_010.prefab`

## 2. 脚本与组件树

```text
DLevel_010
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DLevel001Ctrl                        # 既有首笔后 Actor 向右移动 Handler
├─ DLevel10Ctrl                         # 本关可见 Intro 与 ball 释放
├─ DrawLevelCountdownCtrl               # 20 秒，第一笔完成后开始
└─ level10
   └─ Alien                             # 根 Transform 是最终位置 (-3.52, 6.58)
   │  ├─ DrawEntrancePathView           # Scene 起终点与轨迹预览
   │  ├─ entranceStartPoint             # 世界起点 (-3.21, 11.32)
   │  ├─ ActorSpineCtrl                 # 保持 prefab 默认 idle01，不由本关脚本控制
   │  ├─ catch + Animator               # isOpen: false -> true
   │  └─ yellow_ball
   │     └─ Rigidbody2D                 # Kinematic -> Dynamic
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| DrawLevelCtrl | 共享主控制器 | 初始化、Intro 分发、画线 Handler 分发与结算 | `IDrawLevelRuntime` | DrawPlayCtrl | 不持有 Level10 的 Alien、动画或 ball 规则 |
| DrawLineFeature | 共享功能 | 画线输入、线段与墨水状态；本关启用笔画间厚碰撞 | MonoBehaviour | DLevel prefab | 不控制 Alien 入场或 ball 物理 |
| DrawEntrancePathView | 共享摆放视图 | 根节点表示最终位置、子节点表示起点，并绘制 Scene 轨迹/起点占位 | MonoBehaviour | Alien | 不播放 Tween、不打开钩子或释放球 |
| DLevel001Ctrl | 既有复用 Handler | 第一笔有效画线后驱动当前 prefab Actor 向右移动 | `IDrawStrokeCompletedHandler`、`IDebuger` | DLevel prefab | 不参与 Alien Intro、ball 释放或倒计时 |
| DLevel10Ctrl | 关卡局部控制器 | 可见转场后延迟入场、打开钩子、释放 ball、恢复局部初态 | `IDrawLevelVisibleIntroHandler`、`IDebuger` | DLevel prefab | 不控制 Alien Spine，不处理倒计时、胜负、UI、存档、资源加载或全局事件 |
| DrawLevelCountdownCtrl | 共享局部 Handler | 第一笔有效画线后启动 20 秒倒计时 | `IDrawStrokeCompletedHandler` | DLevel prefab | 不参与 Intro、Alien 或 ball 释放 |

## 4. 生命周期与关键调用

1. DLevel_010 实例创建后，`DLevel10Ctrl.Awake()` 只在 PlayMode 解析引用；先缓存 Alien
   根的策划最终位置与 `entranceStartPoint` 世界起点，再把 Alien 放回起点，缓存 ball 初态
   并保证 ball 为 Kinematic、零速度。
2. `DrawLevelCtrl.InitializeRuntime()` 扫描到 `IDrawLevelVisibleIntroHandler`；原有
   `DelayAutoBeginDrawHandle` 已移除，避免两个 Intro 顺序执行造成重复等待。
3. 转场遮罩揭开后，DrawPlayCtrl 调用 `PlayDrawIntroAsync()`；期间画线输入保持关闭。
4. 控制器按 prefab 的 `entranceDelay=2` 等待，再以 `entranceDuration=1.2` 和
   `Ease.OutBack` 将 Alien 移动到 Awake 缓存的完整世界最终坐标。
5. Tween 更新期间持续恢复 ball 的初始 localPosition/localRotation，ball 随 Alien 整体
   入场但不提前参与 Dynamic 物理。
6. Alien 抵达后先把 catch Animator 的 `isOpen` 设为 true，再把 ball 切换为 Dynamic、
   清零速度并 `WakeUp()`；Intro 任务随后返回，DrawPlayCtrl 开放画线。
7. Alien Spine 保持 prefab 默认循环 `idle01`；本关不主动播放 `control`。
8. 第一笔有效画线完成后，既有 DLevel001Ctrl 驱动当前 Actor 向右移动，同时
   DrawLevelCountdownCtrl 开始 20 秒倒计时；DLevel10Ctrl 不消费笔画回调。
9. 失败、手动重来或胜利 Replay 不再重新执行 Intro；新实例在全黑遮罩下直接打开夹子、
   释放球并将复合球体放到下方首个实体碰撞面，随后让猫猫夹子保持在退场后的场外位置。
10. 退出或销毁时 `OnDisable()` Kill 未完成入场 Tween。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或配置 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| DLevel_010 根 | DLevel10Ctrl | 本关局部控制器 | 必填 | 不执行 Alien Intro 或 ball 释放 |
| DLevel10Ctrl | `alien` | `level10/Alien` Transform | 必填 | Bear Logger 报错并结束 Intro 门禁 |
| Alien | `DrawEntrancePathView / entranceStartPoint` | 根最终位置 `(-3.52,6.58)`；子节点世界起点 `(-3.21,11.32)` | 必填 | Bear Logger 报错并结束 Intro 门禁 |
| DLevel10Ctrl | `entranceDelay` | 2 秒 | 必填参数 | 运行时负值按 0 处理 |
| DLevel10Ctrl | `entranceDuration` | 1.2 秒 | 必填参数 | 运行时最小按 0.01 秒处理 |
| DLevel10Ctrl | `catchAnimator` | `Alien/catch` Animator | 必填 | ball 仍释放，但钩子不打开并记录错误 |
| DLevel10Ctrl | `ballRigidbody` | `Alien/yellow_ball` Rigidbody2D | 必填 | Alien 仍入场并打开钩子，但 ball 不释放并记录错误 |
| catch | Animator Controller | `Level70/catch.controller`，参数 `isOpen` | 必填 | 钩子无法播放打开状态 |
| Alien Spine | SkeletonData | AlienSpaceship，默认 `idle01` | 既有资源 | 本关脚本不持有或切换 Spine 动画 |
| yellow_ball | Rigidbody2D | 初始 Kinematic，质量/重力/碰撞沿用 prefab | 必填 | 无预期下落物理 |
| DrawLineFeature | `allowMultipleStrokes` | false | 2026-07-30 十关统一配置 | 第一笔完成后关闭继续画线 |
| DrawLineFeature | `enableStrokeToStrokeCollision` | false | 2026-07-30 十关统一配置 | 使用普通 EdgeCollider2D，不进入笔画互撞分支 |

## 6. 数据、UI、资源与事件流

- DB/存档：DLevel10Ctrl 不访问；完整关卡进度沿用 DrawPlayCtrl 既有结算链。
- Luban/配置：无新增或读取。
- UI：不直接操作；画线门禁由既有 Draw Intro 阶段管理。
- 局部引用：DLevel10Ctrl 只持有 Alien、catch Animator 和 ball Rigidbody2D；起终点由 Alien 上的
  `DrawEntrancePathView` 提供。
- 事件：DLevel10Ctrl 无新增全局事件、局部事件或 UnityEvent；DLevel001Ctrl 继续通过
  现有 Actor API 驱动移动。
- 资源加载：全部对象随 DLevel_010 prefab 加载；无新增地址或运行时实例化。
- Analytics/SDK：无。
- 平台：无 Android/iOS 分支；移动、Animator 与 2D 物理使用现有跨平台运行时。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| `DOMove` 对齐缓存的完整最终坐标，不是仅 `DOMoveX` | Level10 起点位于最终位置上方，需求是沿完整路径入场 | Alien 从上方进入并同时对齐 X/Y/Z | 起终点、轨迹、OutBack 过冲 | 本关必要差异 |
| ball 在 Intro 抵达后自动释放，不等待第一笔 | 用户要求把入场、钩子打开和 ball Dynamic 串为连续流程 | 玩家开放画线时 ball 已开始参与物理 | 开画首帧、解法窗口、倒计时 | 本关必要差异 |
| 不播放 DLevel2 的 Alien `control`，改用 DLevel6 钩子 `isOpen` | 用户后续明确要求参考 DLevel6 表现 | Alien Spine 保持 idle，钩子必须打开 | 参数大小写、钩子状态、ball 脱离 | 本关明确差异 |
| 保留旧 2 秒等待为 `entranceDelay` | prefab 原有 DelayAutoBeginDrawHandle 为 2 秒 | 延迟发生在 Alien 移动前，不再追加独立开画等待 | 总 Intro 时长 | 局部迁移 |
| 关闭多笔与笔画间 Composite 厚碰撞 | 2026-07-30 用户要求十关使用 DLevel_003 同一组画线值 | 第一笔后关闭输入；使用普通 EdgeCollider2D | 关键解法、球体交互、重开、通关 | 用户指定的配置统一 |

## 8. 可复用结论与优化候选

- 本关继续沿用的稳定结构：一个主 DrawLevelCtrl，单关可见演出由本地
  `IDrawLevelVisibleIntroHandler` 承担。
- 本关验证的新结构：仅完成静态与编译验证，尚不能声明新结构已通过运行时验证。
- 暂不共享的局部实现：完整坐标移动、2 秒延迟、1.2 秒时长、OutBack、`isOpen` 和抵达即
  释放 ball 都保留在 DLevel10，不上提共享 Draw 或 Actor 控制器。
- 后续关卡需继续观察的候选：Intro 中 Kinematic 子刚体随父节点移动并在终点释放的恢复
  模板；DLevel2/10 行为时机不同，不提取共享抽象。

## 9. 验证证据与缺口

- 静态检查：DLevel10Ctrl GUID 在 prefab 中出现一次；旧 DelayAutoBeginDrawHandle GUID
  为零次；Alien、catch、ball 引用各一项；`DrawEntrancePathView` 与 `entranceStartPoint`
  各一项且绑定有效；ball 初始 BodyType 为 Kinematic；YAML 锚点无重复。
- 资源检查：DLevel_010 的 catch 绑定与 DLevel6 相同的 `catch.controller`；控制器参数和
  两条转场条件均为严格大小写的 `isOpen`。Alien Spine 默认 `idle01`，本关代码无
  `control` 调用。
- 禁用链扫描：无 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、
  RemoteConfig、PlayerPrefs、Instantiate、Unity `Debug.Log`、PlayEvents 或新事件派发。
- 画线配置静态检查：本关当前为单笔，`enableStrokeToStrokeCollision=false`；共享 RDP、
  Capsule 与 Polygon Composite Merge 实现仍在代码中，但本关当前不进入该分支。
- 编译：`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal
  /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false
  /p:BuildInParallel=false` 通过，`0 error / 0 warning`。
- 笔画互撞修改后的同命令第一次在 124 秒超时且无诊断；清理本次残留构建进程后重试通过，
  `0 error / 314 warning`。代码与中文文档 diff 检查通过；完整 prefab diff 检查只命中该
  新增 YAML 的既有空值尾随空格，本次开关行本身通过。
- Unity PlayMode：未执行。
- Android/iOS：未执行。
- 未验证项：2 秒等待与 1.2 秒移动手感、完整坐标和 OutBack 轨迹、ball 随动、抵达首帧
  `isOpen` 钩子动画、Dynamic、开放画线时机、20 秒倒计时、失败/Replay、暂停/恢复、
  成功条件、退出清理和横屏设备物理表现。

## 10. 风险与回滚

- 主要风险：绑定缺失会造成部分演出缺失；OutBack 可能短暂越过最终位置；ball 在开放
  画线时已经开始下落，实际解法窗口需 PlayMode 确认；当前统一为单笔后，原多笔互撞解法
  不再成立；Tween 暂停表现和移动端物理未验证。
- 回滚路径：从 DLevel_010 根移除 DLevel10Ctrl，恢复
  `DelayAutoBeginDrawHandle(delay=2)`，把 `endPoint` 名称恢复为 `GameObject`，并将
  DLevel10Ctrl 恢复为空骨架；若仅恢复此前多笔互撞配置，把 `allowMultipleStrokes` 与
  `enableStrokeToStrokeCollision` 同时设为 true；无需回滚 DB、资源地址、UI 或平台系统。

## 11. 2026-08-02 夹子碰撞边界

- `level10/Alien` 根及 `catch` 下 hook/block/catchArea 的 5 个 Collider2D 已删除；这些组件
  不参与 DLevel10Ctrl 的持球、开夹或释放。
- hook Transform/SpriteRenderer 和 `catch.controller` 保留，`isOpen` 动画路径不变。
- 球 CircleCollider2D、Rigidbody2D 与 Actor 主碰撞体必须保留，分别承担物理球、compound
  body 和成功门 ActorCtrl 识别。
- 静态序列化检查通过；完整 PlayMode/设备尚未验证。回滚时只恢复这 5 个组件及原几何。

## 12. 2026-08-02 最终位置摆放与纵向轨迹预览

- `level10/Alien` 现在直接序列化在最终位置 `(-3.52,6.58,0)`；原同级 `endPoint` 已删除。
  其直接子节点 `entranceStartPoint.localPosition=(0.31,4.74,0)` 对应旧世界起点
  `(-3.21,11.32,0)`。
- `DrawEntrancePathView` 与 DLevel02/06 共用相同摆放契约，在 Scene 绘制起终点圆标、
  虚线路径、方向箭头和起点虚拟包围盒；Gizmo 不参与运行时逻辑。
- `DLevel10Ctrl` 在 Awake 移动前缓存世界起点与最终位置，随后把根移到起点；Intro 仍等待
  `2s`，再用 `1.2s / Ease.OutBack` 完整 `DOMove` 到缓存最终位置，抵达后才开钩并把球切
  Dynamic。放球时机、Animator、倒计时和成功/失败链均未改变。
- Unity 迁移器在 `0.001` 容差内核对起终点并删除旧目标节点；C# 编译和 PrefabImporter
  已通过。完整 Intro、Replay、落球可解性与 Android/iOS 仍为 `uncertain`。
- 回滚本节需同时恢复场外 Alien、同级 endPoint 与旧控制器字段；只移动根或只删起点组件
  会让 Awake/Reset 使用错误坐标。

## 13. 2026-08-02 共享 Rig 与落地双弹结构（当前结构）

- 第 2～12 节保留历史演进证据；当前装置结构以本节为准：

```text
DLevel_010
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DLevel10Ctrl : IDrawLevelVisibleIntroHandler
├─ DrawLevelCountdownCtrl
└─ level10
   └─ Alien                              # DrawGame_AlienClipBallRig 嵌套实例
      ├─ DrawAlienClipBallRigView        # 强类型内部引用入口
      ├─ DrawEntrancePathView            # 根终点、子节点上方起点
      ├─ Alien Spine + ActorSpineCtrl
      └─ catch/BoneFollower
         └─ clipSwingPivot
            ├─ clipVisual/SkeletonAnimation
            └─ yellow_ball/Rigidbody2D
               └─ payloadContentRoot/Actor
```

- DLevel10Ctrl 当前只负责本关序列：恢复共享装置初态、`FromTop` 入场、默认 `clip_close`、放球时
  播放 `clip_open`、球脱父级、
  Dynamic 落地检测、两次衰减弹性跳动和 Intro 门禁。它不处理画线、倒计时、胜负、存档、UI、
  资源加载或全局事件。
- 首次进关生命周期顺序是 `Awake 缓存 -> Intro 恢复 -> 上方 OutBack -> 释放 -> 等待落地 ->
  双弹 -> 恢复 Dynamic -> 开画`；`OnDisable` 通过版本号和 Kill 取消未完成 Tween。重开则在
  黑幕下同步构造上述序列的最终可玩态，不等待入场、自由落体或双弹。
- Prefab 绑定：`rig` 指向嵌套实例的 `DrawAlienClipBallRigView`，`ballReleaseParent` 指向
  `level10`，`entranceState=FromTop(2)`；本关没有旧 Animator catch 或黄球刚体字段。
- 共享边界：Prefab 资产和 View 继续由 DLevel002/006/015 共用；本次只替换 DLevel010 实例并
  修改 DLevel10Ctrl，没有改变其它关卡的首笔释放、左右入场、倒计时或结算。
- 验证证据：Unity 脚本刷新、Prefab 保存和后续 PrefabImporter 重导入正常；一套共享源、
  114 个 YAML 锚点唯一、序列化绑定和旧引用清理静态通过，定向 `git diff --check` 通过。
  PlayMode、完整通关、Replay、暂停以及 Android/iOS 仍为 `uncertain`。
- 回滚必须同时恢复旧内嵌装置和旧控制器契约；只恢复单侧会造成序列化引用失配。

## 14. 2026-08-02 当前夹持、开夹退场与碰撞结构

```text
DLevel_010
├─ DrawLevelCtrl
│  └─ successExtraRendererGroup -> yellow_ball/SpriteRendererGroupAlpha
├─ DLevel10Ctrl
│  ├─ entranceDelay = 0
│  ├─ entranceDuration = 1.2 / OutBack(1.9)
│  └─ exitDuration = 0.8 / InBack(1.9)
└─ level10/Alien [shared rig]
   └─ catch/clipSwingPivot
      ├─ clipVisual [继承共享初态: clip_close；放球时脚本播放 clip_open]
      └─ yellow_ball [Rigidbody2D + CircleCollider2D]
         └─ payloadContentRoot
            └─ Actor [ActorCtrl；移除自身 Rigidbody2D 与 PolygonCollider2D]
```

- Intro 调用顺序为“闭夹夹球立即上方入场 -> 播放 clip_open 并释放球 -> 并行执行猫夹原路
  退场与球落地双弹 -> 两者完成后开画”。`_rigTween` 与 `_ballTween` 分开持有，`OnDisable`
  会同时 Kill 并递增版本号。
- Actor 继续是成功门识别对象；球壳通过 `successExtraRendererGroup` 与 Actor 同步渐隐。物理由
  球根 Rigidbody2D 统一模拟，球根 CircleCollider2D 与既有 Actor 子 BoxCollider2D 保留，只有
  Actor PolygonCollider2D 不再进入 compound collider。
- 该共享碰撞结构同步给 DLevel002/006/015；DLevel010 只在“抵达后立即开夹放球并原路退场”
  上存在本关差异，不会改变其它三关的闭夹、首笔开夹或左右入场流程。
