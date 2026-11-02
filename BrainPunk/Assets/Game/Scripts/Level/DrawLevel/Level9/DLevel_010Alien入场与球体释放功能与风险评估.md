# DLevel_010 Alien 入场与球体释放功能与风险评估

## 1. 需求与背景

- `DLevel10Ctrl` 最初需求要求 Alien 移动到 endPoint，并把移动时长和 delay 暴露给
  Inspector，再释放 ball。
- 用户后续明确覆盖动画要求：去掉 Alien Spine `control`，参考 DLevel6，抵达后必须播放
  钩子打开动画。资源静态证据确认真实 Animator 参数为严格大小写的 `isOpen`，不是
  `isOPen`。
- 本次因旧注释已与最新要求冲突，已将其最小修正为“抵达后参考 DLevel6 播放钩子打开
  动画”；移动仍按 DLevel_010 的实际纵向布局对齐完整世界坐标。
- 参考 DLevel2 的可见 Intro、ball 初态缓存和 Tween 取消方式，参考 DLevel6 的
  catch `isOpen` 与 ball 释放表现；不修改共享 DrawLevelCtrl、DrawPlayCtrl、ActorCtrl
  或 GamePlayPanel。

## 2. 修改前行为基线

- `DLevel10Ctrl.cs` 仅有 Unity 生成的空 `Start()` / `Update()`，不产生关卡行为。
- DLevel_010 根节点有 `DelayAutoBeginDrawHandle(delay=2)`，只等待后开放画线，不移动
  Alien、不播放控制动画，也不释放 ball。
- prefab 中 Alien 初始 localPosition 为 `(-3.21, 11.32, 0)`；其同级唯一带定位图标的
  空节点名为 `GameObject`，位置为 `(-3.52, 6.58, 0)`。
- `Alien/yellow_ball` Rigidbody2D 初始为 Kinematic；Alien ActorSpineCtrl 与
  SkeletonAnimation 默认循环 `idle01`。
- 本次调整前的首版 DLevel10Ctrl 会在抵达后主动播放 Spine `control`，且未驱动
  catch Animator；该表现已被用户最新要求否定。
- DLevel_010 根已有 DLevel001Ctrl；第一笔有效画线后会解析本关 Actor 并持续向右移动。
- DLevel_010 已有 DrawLevelCountdownCtrl，配置为 20 秒且失败结算；该倒计时在第一笔
  有效画线后启动，与原 2 秒开画延迟相互独立。

## 3. 修改内容与新行为

### 3.1 脚本

- DLevel10Ctrl 实现 `IDrawLevelVisibleIntroHandler` 和 `IDebuger`。
- `entranceDelay` 与 `entranceDuration` 均序列化；当前 prefab 值为 2 秒和 1.2 秒。
- 可见转场结束后先等待 delay，再通过 DOTween `DOMove(cachedFinalPosition, duration)`
  以 `Ease.OutBack` 移动 Alien，任务完成前不开放画线。
- ball 在移动过程中保持初始 localPosition/localRotation 和 Kinematic；抵达后先设置
  `catchAnimator.SetBool("isOpen", true)`，随后切 Dynamic、清零线速度/角速度并
  `WakeUp()`。
- DLevel10Ctrl 不再持有 ActorSpineCtrl，不播放 `control`，Alien Spine 保持 prefab
  默认循环 `idle01`。
- 首次 Intro 开始前恢复缓存的 Alien 场外起点、夹子闭合和 ball 初态。失败、手动重来及
  胜利 Replay 重建后不再播放 Intro，而是在全黑遮罩下直接应用释放、落地与装置退场完成态；
  禁用时仍 Kill 未完成的装置和球 Tween。
- 引用为空时优先按 `level10/Alien`、`Alien/catch`、`Alien/yellow_ball` 局部路径解析；
  起点从 Alien 上的 `DrawEntrancePathView/entranceStartPoint` 读取。仍缺失时用 Bear Logger
  报错，不访问跨关卡对象。

### 3.2 Prefab

- DLevel_010 根节点用 DLevel10Ctrl 替换旧 DelayAutoBeginDrawHandle，避免两个 Intro
  Handler 被 DrawLevelCtrl 顺序等待。
- Alien 根直接摆在最终位置；原同级 `endPoint` 已删除，旧场外位置迁入 Alien 直接子节点
  `entranceStartPoint`，并由 `DrawEntrancePathView` 可视化。
- 显式绑定 Alien Transform、catch Animator 和 yellow_ball Rigidbody2D；移除
  DLevel10Ctrl 的 Alien ActorSpineCtrl 与旧 endPoint 绑定。
- 旧 delay 的 2 秒数值迁移到 DLevel10Ctrl.entranceDelay；球/Actor 的质量、重力、玩法碰撞、
  Layer、Tag、Spine 数据和倒计时配置不变。

## 4. 影响文件与系统

- `Assets/Game/Scripts/Level/DrawLevel/Level10/DLevel10Ctrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_010.prefab`
- 本关结构记录、本文、中央 DrawGame 演进记录和 `DevelopMD/已有功能设计与使用记录.md`
- 影响系统：Draw 可见 Intro 门禁、DOTween Transform 移动、Spine 夹子、Rigidbody2D，
  以及失败/手动重来/胜利 Replay 的直接完成态。
- 未修改：共享 Draw/Actor/GamePlayPanel 代码、全局事件、DB/存档、Luban、资源地址、
  UI、analytics、广告、IAP、RemoteConfig、SDK、ProjectSettings 或平台配置。

## 5. 数据、资源、UI 与事件流

```text
DrawPlayCtrl 转场完成
  -> DrawLevelCtrl.PlayIntroAsync()
    -> DLevel10Ctrl.PlayDrawIntroAsync()
      -> 恢复 Alien / ball 初态
      -> 等待 entranceDelay
      -> Alien DOMove 到缓存最终位置（ball 保持相对位姿）
      -> catch Animator 设置 isOpen=true
      -> ball Kinematic -> Dynamic + WakeUp
  -> BeginDrawing()
  -> 第一笔有效画线
  -> DLevel001Ctrl 驱动 Actor 向右移动
  -> DrawLevelCountdownCtrl 启动 20 秒倒计时
```

- 数据/存档：DLevel10Ctrl 无读写。
- 资源：复用 prefab 已绑定的 `catch.controller`；Alien Spine 保持 prefab 默认状态，无
  新增运行时加载。
- UI：只通过现有 Intro 返回时机间接控制画线门禁。
- 事件：DLevel10Ctrl 无新增事件，未调用 PlayEvents 或 DispatchEvent；既有
  DLevel001Ctrl 继续使用 Actor 的局部公开 API。
- 移动端：无平台分支；目标仍是 Android/iOS 横屏，但本次未执行设备验证。

## 6. 风险评估

| 风险 | 等级 | 控制措施 | 剩余缺口 |
| --- | --- | --- | --- |
| Alien/入场轨迹引用缺失导致 Intro 跳过 | 中 | 缺失时 Bear 报错并立即结束任务，允许共享流程继续 | 关卡会缺少目标演出 |
| ball/catch 引用缺失导致部分表现丢失 | 中 | 显式 prefab 绑定并保留局部路径回退；单项失败不阻断另一项 | PlayMode 仍需确认 |
| 旧 2 秒延迟与新 Intro 重复 | 高 | 用 DLevel10Ctrl 替换旧 DelayAutoBeginDrawHandle | 静态确认旧 GUID 为零 |
| OutBack 轨迹越过最终位置或穿过美术/碰撞 | 中 | 参数保持关卡局部，未修改共享 Tween 行为 | 必须 PlayMode 看实际轨迹 |
| ball 在开放画线时已开始下落，解法窗口过短 | 高 | 按需求顺序在抵达后释放，倒计时仍由第一笔开始 | 玩家体验和关卡可解性未验证 |
| Reset/Replay 残留 Dynamic 速度或钩子打开状态 | 中 | Intro 开头恢复位置、刚体、速度和 `isOpen=false`；禁用时 Kill Tween | 失败与手动重开未运行验证 |
| 暂停期间 Tween 继续推进 | 中 | 沿用 DLevel2 现有 Tween 模式，未扩大共享修改 | 暂停/恢复表现未验证 |
| Android/iOS 2D 物理差异 | 中 | 不改质量、重力、碰撞检测或约束 | 设备横屏未验证 |

## 7. 验证证据

- C# 单进程构建通过：`0 error / 0 warning`。
- Assembly-CSharp.csproj 已包含 DLevel10Ctrl。
- prefab 静态检查：
  - DLevel10Ctrl 脚本 GUID 一次，旧 DelayAutoBeginDrawHandle GUID 零次；
  - Alien/catch/ball 引用各一项；`DrawEntrancePathView` 与起点引用各一项；
  - 旧 endPoint 已清除，ball 初始 BodyType 为 Kinematic；
  - 157 个 YAML 对象锚点，无重复 fileID。
- catch 绑定 `Assets/Game/ArtSrc/Animations/Level70/catch.controller`；静态确认参数名和
  转场条件均为 `isOpen`，无 `isOPen`。DLevel10Ctrl 已无 `control`/ActorSpineCtrl 调用。
- 代码静态扫描未发现 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、
  RemoteConfig、PlayerPrefs、Instantiate、Unity `Debug.Log`、PlayEvents 或事件派发。
- `git diff --check` 对本次已跟踪目标未报告新增空白错误；DLevel10 新增文件还需随最终
  Git 纳入后由提交前检查再次覆盖。

## 8. 手工验证清单与未验证项

- [ ] 首次进入时先完成转场，再等待约 2 秒，Alien 用约 1.2 秒从上方到达 endPoint。
- [ ] delay 和 duration 在 Inspector 可调，负值/极小值按代码下限安全处理。
- [ ] 移动期间 ball 不发生相对漂移，不提前 Dynamic。
- [ ] 抵达首帧钩子播放打开动画，Animator 参数 `isOpen=true`，ball 同时开始 Dynamic。
- [ ] Alien Spine 始终保持 prefab 默认 idle，不播放 `control`。
- [ ] 画线只在抵达和释放后开放；第一笔后 20 秒倒计时正常出现和失败。
- [ ] 暂停/恢复不会造成 Tween、ball 或钩子动画异常。
- [ ] 失败、手动重开、退出再进时 Alien、catch 和 ball 回到 prefab 初态。
- [ ] 成功门、星级、进度保存和下一关流程正常。
- [ ] Android/iOS 横屏触控、60 fps 目标、Animator 和 2D 物理表现正常。

当前 Unity PlayMode、完整可解性、成功条件、暂停/恢复、失败/Replay、保存和移动端均为
未验证，不应据此声明整关已验收。

## 9. 回滚路径

1. 从 DLevel_010 根节点移除 DLevel10Ctrl 组件。
2. 恢复 `DelayAutoBeginDrawHandle` 并设置 `drawLevelCtrl` 为本关根控制器、`delay=2`。
3. 将定位节点 `endPoint` 恢复原名 `GameObject`。
4. 将 DLevel10Ctrl 恢复为空的 Start/Update 骨架或移除脚本及 meta。
5. 删除本关新增文档，并撤回中央演进记录与已有功能记录的 DLevel_010 追加段落。

回滚不涉及共享 Draw 代码、DB、存档、Luban、资源地址、UI、analytics 或平台迁移。

## 10. 2026-08-02 Alien/夹子冗余 Collider 清理

- 当前持球基线是 `yellow_ball` Kinematic 且控制器保持局部位姿；抵达后 Animator 打开夹子，
  球直接切 Dynamic。`DLevel10Ctrl` 没有 Collider 字段、查询或回调。
- 已删除 `level10/Alien` 根 Trigger，以及 `catch/hook_left`、`hook_right`、`block`、
  `catchArea` 上共 5 个 Collider2D。hook 节点和 SpriteRenderer/Animator 路径保留，视觉动画
  不受影响。删除后这些旧代理不再阻挡落笔、线段或球。
- 保留 `yellow_ball` CircleCollider2D、Rigidbody2D 和 nested Actor 主碰撞体；后者参与
  compound body，并让成功门能通过 `GetComponentInParent<ActorCtrl>()` 识别玩家。
- Unity 迁移日志确认删除 5 个目标组件；YAML 静态计数当前只剩 DrawArea Box 和球 Circle。
  完整 Intro、落球、成功门、Replay 与 Android/iOS 仍为 `uncertain`。
- 回滚只需按原几何恢复这 5 个 Collider2D；不需要修改 DLevel10Ctrl、Animator、数据、UI
  或平台配置。

## 11. 2026-08-02 最终位置配置与纵向轨迹可视化

- `level10/Alien` 从旧场外位置 `(-3.21,11.32,0)` 迁移到最终释放位置
  `(-3.52,6.58,0)`；旧同级 `endPoint` 删除。Alien 直接子节点
  `entranceStartPoint.localPosition=(0.31,4.74,0)` 精确保留旧世界起点。
- Alien 新增共享 `DrawEntrancePathView`，Scene 中可见起终点圆标、虚线路径、方向箭头和
  起点虚拟包围盒；该组件只读取编辑数据，不参与 Tween、Animator 或 Rigidbody2D。
- DLevel10Ctrl 在 Awake 首次移动前缓存世界起点/最终位置，再把根放到起点；Intro 仍等待
  `2s`，以 `1.2s / Ease.OutBack` 完整 `DOMove` 到缓存最终位置，抵达后才设置
  `isOpen=true` 并把球切 Dynamic。第一笔、20 秒倒计时和既有结算没有变化。
- Unity 迁移器在 `0.001` 容差内核对两点，旧目标节点已清除；C# 编译和 PrefabImporter
  通过。完整纵向 Intro、球随动、Replay、可解性与 Android/iOS 仍为 `uncertain`。
- 回滚必须成套恢复场外 Alien、同级 `endPoint` 与旧控制器字段；只移除轨迹组件会让运行时
  缺少独立起点并跳过本关入场。

## 12. 2026-08-02 共享猫夹球、上方入场与落地双弹（当前行为）

### 12.1 需求与基线

- 用户要求 DLevel_010 的猫、夹子和球改用项目现有的
  `DrawGame_AlienClipBallRig.prefab`，不再保留本关内嵌的旧 Alien、Animator 夹子和黄球。
- 入场需要在既有左右方向语义上增加 `FromTop` 状态：装置从上方移向最终位置，使用
  `Ease.OutBack` 越过目标后回正。
- 本关默认像其它夹子球关一样夹紧球；抵达后放球时播放 `clip_open`。球落地后必须完整播放
  两次衰减弹性跳动，动画停止后才允许玩家画线。
- 修改前当前基线是旧内嵌装置从上方入场，抵达后才设置 Animator `isOpen=true` 并立即把
  黄球切为 Dynamic；画线门禁没有等待落地或弹性动画。

### 12.2 当前实现与受影响资源

- `DLevel_010.prefab` 的 `level10/Alien` 已替换为共享嵌套 Prefab 实例，根最终位置仍为
  `(-3.52,6.58,0)`，共享根继承 `0.9` 整体缩放；旧内嵌 Alien、Animator catch、yellow_ball
  和旧 Alien Spine 资源引用已从本关 YAML 移除。
- 共享实例的 `entranceStartPoint` 调整到根正上方，世界入场距离保持约 `4.75`；
  `DLevel10Ctrl.RigEntranceState` 提供 `FromLeft / FromRight / FromTop`，本关序列化为
  `FromTop`。运行时按配置距离重建纯纵向起点，避免缩放改变世界入场距离。
- `DLevel10Ctrl` 通过 `DrawAlienClipBallRigView` 获取猫 Spine、BoneFollower、夹子 Spine、
  蓝球刚体和 `payloadContentRoot`；不再依赖旧 catch Animator。
- Intro 初态播放共享默认 `clip_close`，球以 Kinematic 保持在夹子内；装置按
  `delay=0s / duration=1.2s / OutBack overshoot=1.9` 从上方入场，抵达后先播放
  `clip_open`，再让球脱离 BoneFollower 层级并切 Dynamic。
- 共享球进入 Dynamic 前沿用 DLevel002/006 的镜像迁移边界：把球根负缩放转移到
  `payloadContentRoot`，同步 CircleCollider2D offset，使刚体根保持正缩放且画面朝向不变。
- 控制器在 FixedUpdate 时机等待球 Collider 连续两帧接触碰撞面且竖直速度不超过 `0.35`；
  落地后临时设为 Kinematic，依次播放高度 `0.55` 和 `0.3025` 的两次衰减跳动，并同步做
  payload 拉伸/压缩，最终恢复基准缩放、停止 Tween、切回 Dynamic。
- `PlayDrawIntroAsync()` 只在双弹结束后返回，因此 DrawLevelCtrl 之后才开放画线；落地等待
  超过 `8s` 时记录错误并结束门禁，避免碰撞异常造成永久不可操作。

### 12.3 数据、资源、UI 与事件流

```text
可见转场完成
  -> DLevel10Ctrl 恢复共享 Rig 上方起点、闭夹与夹持球初态
  -> 上方 OutBack 入场并回正
  -> 播放 clip_open，球脱离挂点、负缩放迁移、Dynamic 下落
  -> 检测稳定落地
  -> Kinematic 双弹两次并停止表现
  -> 恢复 Dynamic
  -> Intro 返回，DrawLevelCtrl 开放画线
```

- 数据/存档/Luban：无新增读写或迁移。
- 资源：复用共享 Rig、猫/夹子 Spine、蓝球与嵌套 Actor；无运行时 Resources/Addressable 加载。
- UI/事件/analytics/SDK：无新增；输入门禁继续使用既有 Draw Intro 返回时机，不新增全局事件。
- 平台：没有 Android/iOS 分支；DOTween、Spine 和 Rigidbody2D 使用现有移动端运行时。

### 12.4 验证、风险与回滚

- Unity 强制脚本刷新成功，DLevel10 迁移器记录 `MIGRATION_OK`，随后一次性迁移脚本及 meta
  已删除；Prefab 由 Unity `LoadPrefabContents / SaveAsPrefabAsset` 正常保存。
- 独立 `dotnet build` 无可用 SDK；Visual Studio 2019 MSBuild 可启动，但 Unity 生成的 csproj
  当前缺少 `Temp/bin/Debug` 中的包程序集而以 `CS0006` 结束。因此编译结论只采用本次 Unity
  编辑器域重载无 C# 错误的证据，不把命令行失败误报为代码错误。
- YAML 静态确认本关恰有一套共享 Rig PrefabInstance、控制器 `rig` 与 `ballReleaseParent`
  有效、`entranceState=2`，旧 `catchAnimator / ballRigidbody` 字段和旧 Alien Spine GUID
  均已清除；114 个 YAML 对象锚点无重复，定向 `git diff --check` 通过。清理空值尾随空格后，
  Unity PrefabImporter 再次导入 DLevel_010 且未记录解析或编译错误。
- 仍需 PlayMode 验证：开场首帧夹子状态、纵向 OutBack 手感、球真实落点、碰撞接触门槛、
  两次弹高和缩放观感、双弹后线条与球交互、失败/Replay、暂停/恢复和完整通关。
- Android/iOS 横屏触控、60 fps、Spine/BoneFollower 与 2D 物理表现仍为 `uncertain`。
- 回滚时应成组恢复旧内嵌 Alien/catch/yellow_ball 与旧 DLevel10Ctrl 字段；若只回滚运行时代码，
  当前共享 Prefab 绑定会失效。无需回滚 DB、配置、UI、事件或平台数据。

## 13. 2026-08-02 开场提速、放球开夹与原路退场（当前行为）

### 13.1 反馈与行为调整

- 遮罩揭开后的装置额外等待由 `2s` 改为 `0s`，Intro 一开始就从上方飞入；入场移动仍为
  `1.2s / OutBack(1.9)`，不改变配置终点、纵向轨迹或越过终点后回正的惯性感。
- DLevel010 不再覆盖共享夹子的首帧动画，初态继承与其它夹子球关相同的 `clip_close`；装置
  夹紧球完成上方入场，抵达释放点时才播放 `clip_open` 并放球。猫夹随后保持开夹姿态退场。
- 球脱离 `clipSwingPivot` 并切为 Dynamic 后，猫与夹子立即使用 `0.8s / InBack(1.9)` 沿原
  入场路径退回上方起点；球已换到 `level10` 稳定父节点，不会被退场根再次带走。
- 猫夹退场与“等待球稳定落地 -> 两次衰减弹跳”并行执行。只有两条任务都结束，Intro 才返回，
  玩家才可画线；落地检测超时仍会安全结束球分支，避免永久锁输入。

### 13.2 Actor 碰撞边界纠正

- 球内 Actor 保留，继续提供 `ActorCtrl`、`shrink` 表现、成功门身份与同步渐隐；没有删除
  Actor GameObject，也没有修改通用 `Actor.prefab`。
- 共享 `DrawGame_AlienClipBallRig.prefab` 的 Actor 嵌套实例现移除 Actor 自带
  `PolygonCollider2D`；原本已移除的 Actor 自带 `Rigidbody2D` 继续保持移除。实际球物理由
  `yellow_ball` 根的 Rigidbody2D 统一模拟；球根 CircleCollider2D 与 Actor 既有子
  BoxCollider2D 保留，只让角色轮廓 PolygonCollider2D 退出复合碰撞。
- 共享修改同步影响 DLevel002、DLevel006、DLevel010、DLevel015，不影响普通 Actor 关卡。
  DLevel010 的 `successExtraRendererGroup` 绑定到球渲染组，过关时 Actor 与球壳继续同步渐隐。

### 13.3 风险、验证与回滚

- 静态确认共享 Actor PrefabInstance 保留，RemovedComponents 同时包含 Actor 源
  `Rigidbody2D` 与 `PolygonCollider2D`；共享 YAML 锚点唯一。Unity PrefabImporter、完整
  PlayMode、落点手感、成功/失败、Replay 与 Android/iOS 仍需最终验证，未验证项为
  `uncertain`。
- 回滚开场表现时只需恢复 `entranceDelay=2` 并移除 `exitDuration`/退场；夹子当前没有本关
  首帧动画 override，继续继承共享 `clip_close`。回滚碰撞时只恢复 Actor 的
  PolygonCollider2D removed override。
  不得恢复 Actor 自带 Rigidbody2D，否则会与球根刚体形成嵌套刚体边界。
