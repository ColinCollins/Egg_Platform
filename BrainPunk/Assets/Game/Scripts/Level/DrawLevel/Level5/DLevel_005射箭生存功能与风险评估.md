# DLevel_005 射箭生存功能与风险评估

## 0. 2026-07-31 优化调整（当前生效口径）

- 已移除 Player 目标点、随机椭圆和指定最高点反算。每支箭现在使用真正的 Dynamic
  `Rigidbody2D`；离弦后只受自身重力与场景碰撞控制，不读取 Player 位置。
- 当前使用“8 段连续分层随机袋”：速度从 `22.2` 连续增加到 `23.8`，仰角使用同一个
  分层位置从 `65°` 连续降低到 `57°`。每轮将 8 段随机洗牌，每段内部继续随机一次，
  完整一轮保证区域各段各有一箭，不再只落在原来的两个窄簇；重力倍率为 `4`。
- DLevel_005 的画线模板 Rigidbody2D 现在局部覆盖为既有
  `DrawStrokeStable.physicsMaterial2D`：摩擦系数 `0.8`、弹性 `0`、摩擦合并模式为 Maximum。
  运行时线段从模板克隆刚体，子碰撞链未单独指定材质时使用该刚体材质；其他 DLevel
  和共享 `DrawLineFeature` 不受影响。
- Inspector 当前暴露速度中点 `23`、速度范围半宽 `0.8`、额外速度浮动 `0`、仰角中点
  `61°`、仰角范围半宽 `4°`、额外角度浮动 `0` 和分层数 `8`。速度与仰角共用同一
  分层位置，保持蓝线高弧的同时让无遮挡平地落程近似均匀。
- `attackSpeedMultiplier=2.5` 同时作用于
  `draw_pull / draw_hold / draw_shoot` 的 Spine `TrackEntry.TimeScale`，并把
  `shootInterval=0.05` 按倒数缩短；实际每箭间隔仍包含三段 Spine 动画时长。
- 每次 `draw_shoot` 完成后，Enemy 循环播放 `draw_hold`，不再回到 `idle`；
  Reset、结算或关卡销毁时仍可取消动作并恢复既有初态。
- 箭模板 BoxCollider2D 从覆盖全身改为只覆盖 local `+X` 的箭头尖端，当前
  `offset=(0.7,0)`、`size=(0.3,0.28)`；源图箭头朝 local `+X`，运行时旋转后碰撞盒
  会跟随真正箭头，不再由箭身或箭尾提前命中。
- 小碰撞盒不再使用 AutoMass。运行时显式写入 `arrowMass=8`、Continuous 和 Interpolate，
  避免碰撞面积缩小后质量下降，也改善高速飞行的命中可靠性与视觉平滑度。
- `launchOffset` 从 `(-1,1)` 收回到 `(-0.3,0.3)`，让箭更靠近弓弦出现；同时使用
  `arrowFadeInDuration=0.06` 对运行时 SpriteRenderer 做短暂渐显，避免突然跳出。
- 新增运行时局部组件 `DLevel5ArrowImpactHandle`：飞行中按速度更新箭头朝向；新箭与
  当前尝试内所有旧箭逐对调用 `Physics2D.IgnoreCollision`，不修改全局 Layer 碰撞矩阵。
- 箭模板改用关卡局部 `DLevel5ArrowTrapHandle` 处理玩家命中：同时检查碰撞双方的
  Collider，粒子移动到 `Collision2D.GetContact(0).point`；无接触点时才回退到玩家
  Collider 的 `ClosestPoint`。命中仍播放死亡、trap 音效并延迟提交原失败事件，但不再
  调用向上 `Knockback`，同时清零玩家刚体线速度和角速度，避免角色被箭顶起。
- 箭命中线、Player、地板或其他非箭碰撞体后，先让当前 Dynamic 接触完成动量传递，
  到下一物理步再归零、冻结、挂到命中对象并关闭模拟，使画线先产生反应、箭再以
  “扎住”的造型保留。`DLevel5ArrowTrapHandle` 可在同一次 Collision2D 回调中处理
  玩家受伤与失败事件；画线继续使用共享 `OnTriggerTrapHandle`。
- 此前截图暴露的 BadBox 误停、空中误黏和飞行乱转修复继续保留：每次生成前关闭 Enemy
  物理；碰撞回调同时检查
  `Collision2D.collider` 与 `otherCollider`，排除箭自身层级后只把真正的对方 Collider
  作为命中对象；箭与箭接触时恢复碰撞前飞行速度，不会误进场景定格分支。
- 运行时克隆不再继承模板用于静态展示的 Y 轴 180° 翻转，而是以源贴图的本地 `+X`
  作为箭头方向；飞行中直接用速度 `atan2` 设置绝对 Z 角，并冻结物理旋转。
  命中场景时继续 `FreezeAll`，保持真正碰撞瞬间的扎入造型。
- 失败 `Resolved` 到达时只停止后续射箭，不再立即销毁现场箭；箭会覆盖死亡演出后的
  0.5 秒旧关卡淡出窗口，直到真正重载/退出触发 `OnDisable` 才随旧关卡清理。成功结算
  仍立即清箭，显式原地 Reset 仍由 `OnDrawAttemptReset` 清箭，因此不会跨局残留。

## 1. 需求、参考与原行为基线

- 首次实现需求：第一笔有效画线结束后，Enemy 参考主线 `Level78Ctrl` 持续播放拉弓、
  持弓、射箭动作；后续已修正为向左发射、射后回到 hold、箭间无碰撞并在命中场景后
  定格。当前需求进一步撤回目标点反算，改为可调速度/仰角/重力/质量的真实物理发射，
  箭头小碰撞盒、更快射击循环和短暂渐显。后续反馈要求轨迹达到蓝线高度、角色前后都能
  留箭且整个区域内平均分布；当前以局部 8 段连续分层随机袋实现，不恢复目的地插值或
  Player 目标点反算。最新反馈要求线条增加摩擦，并在 Player 死亡后的失败淡出阶段保留
  已射出的箭。
- 用户明确指定主线 Level 78 为射箭动作参考。本关继续复用 DrawGame 已有的
  `IDrawStrokeCompletedHandler`、`DrawLevelCountdownCtrl` 和 `DrawLevelCtrl` 结算链。
- 修改前 `DLevel_005.prefab` 已有：
  - 根节点 `DrawLevelCtrl`、`DrawLineFeature`、`DelayAutoBeginDrawHandle`；
  - `DrawLevelCountdownCtrl(cdTime=20, isSuccess=true)`；
  - Player、Enemy、箭矢模板、画线模板和成功门；
  - 箭矢模板上的 `OnTriggerTrapHandle(isCollision=true)`；
  - 画线模板上的 `OnTriggerTrapHandle(isCollision=false)`。
- 修改前没有 Level5 本地脚本。首次实现时 `DrawLineFeature` 为有效线段生成非 Trigger
  Collider，因此画线模板的 Trigger 监听模式与实际碰撞类型不匹配，玩家碰线不会进入
  trap 回调；当前共享实现可使用子 BoxCollider2D 链，但仍保持 Collision2D 边界。

## 2. 修改后行为

- `DLevel5Ctrl` 实现 `IDrawStrokeCompletedHandler`。第一笔有效画线完成后立即启动一次
  Enemy 射箭动作；动作完整结束后等待 `shootInterval`，再开始下一次拉弓。
- `shootInterval` 是 DLevel5 prefab 上的序列化字段，当前值为 `0.05` 秒；实际等待时间
  还会除以 `attackSpeedMultiplier=2.5`。间隔定义为上一套动作结束到下一套开始，
  不会让动作重叠。
- `DLevel5ActorCtrl : ActorCtrl` 持有
  `draw_pull -> draw_hold -> draw_shoot -> draw_hold(loop)` 动画序列、动作忙碌状态、
  2.5 倍播放速度、松弦时点和完成回调。未修改共享 `ActorCtrl`。
- `draw_shoot` 播放到归一化时间 `0.55` 时，`DLevel5Ctrl` 克隆隐藏箭模板，从当前 8 段
  洗牌袋消费一个未使用分层，并在该段内部随机归一化位置；速度与仰角按同一位置连续
  插值，再以 `mass * sampledSpeed` 的 Impulse 启动 Dynamic Rigidbody2D。每 8 箭重洗
  一轮，后续轨迹只由自身重力倍率和物理接触演算。
- `DLevel5ArrowImpactHandle` 负责飞行朝向、箭与箭逐对忽略、非箭命中后的冻结与附着；
  同时负责 0.06 秒 SpriteRenderer 渐显。场景命中会延迟到下一物理步再定格，让本次
  接触先向动态画线传递动量；定格箭保留至本次尝试结束统一清理。
- `DLevel5ArrowTrapHandle` 只负责箭命中 Player 的受击表现与失败事件。粒子使用真实
  接触点，`Actor.Die()` 后不施加击退，并再次清零玩家刚体速度；共享陷阱脚本及其他
  关卡的击退行为不变。
- 箭在本次关卡进行期间不再定时销毁；倒计时成功、其他先行结算、重置、禁用或销毁时
  都会停止射箭并使旧回调失效。Success 会立即清理本次生成的箭；Failed 会保留箭到
  旧关卡实例真正禁用/销毁；显式 Reset 仍立即清理，避免影响下一次关卡实例。
- Enemy 只承担演出。运行时关闭其 Rigidbody2D 模拟和全部 Collider2D，避免箭在出生点
  碰到射手自身，同时不影响 Player 的移动和碰撞。
- 画线模板的 `OnTriggerTrapHandle.isCollision` 改为 `true`，与运行时线段的非 Trigger
  Collider2D 对齐。画线模板 Rigidbody2D 还局部绑定 `DrawStrokeStable`，给本关运行时
  线碰撞提供 `0.8` 摩擦和 `0` 弹性。箭矢继续使用原有 Collision2D trap 入口。
- 2026-07-28 Reset 日志确认旧陷阱的 0.5 秒异步派发可越过关卡销毁并命中新实例；
  共享 `OnTriggerTrapHandle` 已为该等待绑定销毁取消，取消后不再派发旧事件。
- 倒计时成功继续由既有
  `DrawLevelCountdownCtrl -> DrawLevelCtrl.TryResolve(Success) -> DrawPlayCtrl`
  链处理；本次未新增胜利事件或修改共享倒计时代码。

## 3. 数据、资源、UI 与事件流

```text
第一笔有效画线完成
  ├─ DrawLevelCountdownCtrl 开始 10 秒倒计时
  └─ DLevel5Ctrl 启动循环
       -> DLevel5ActorCtrl 以 2.5 倍播放 draw_pull / draw_hold / draw_shoot
       -> 箭从弓弦附近以 0.06 秒渐显
       -> 8 段随机洗牌；每段内采样一次连续速度 22.2~23.8 / 仰角 65°~57°
       -> Rigidbody2D 以 gravityScale 4、质量 8 自然下坠，朝向跟随速度
       -> RuntimeArrow 忽略其他 RuntimeArrow
       -> 命中非箭对象先完成接触响应，下一物理步冻结并附着
       -> Enemy 回到 draw_hold(loop)
       -> 等待 shootInterval / attackSpeedMultiplier
       -> 下一轮

Player 与箭矢发生 Collision2D
  -> DLevel5ArrowTrapHandle
  -> 接触点播放 Knock 粒子
  -> Actor.Die / 清零刚体速度 / trap 音效（无 Knockback）
  -> OnTriggerTrapEvent
  -> DrawLevelCtrl 既有失败链
  -> DLevel5Ctrl 停止射箭但保留现场箭
  -> 0.5 秒淡出后整关重建，旧实例 OnDisable 清箭

Player 与线段发生 Collision2D
  -> OnTriggerTrapHandle（共享原行为）
  -> Actor.Die / Knockback / trap 音效
  -> OnTriggerTrapEvent
  -> DrawLevelCtrl 既有失败链
  -> DLevel5Ctrl 停止射箭但保留现场箭
  -> 0.5 秒淡出后整关重建，旧实例 OnDisable 清箭

倒计时归零
  -> TryResolve(Success)
  -> DLevel5Ctrl 停止动作并清箭
  -> DrawPlayCtrl 既有胜利、星级、进度和面板流程
```

- DB/存档：不新增字段；成功后的关卡进度继续由 DrawPlayCtrl 既有流程处理。
- Luban/配置：无新增字段或表修改。
- UI：复用 Draw HUD 的倒计时显示，无新增 UI。
- 资源：复用 Actor Spine 的 Level 78 射箭动画、prefab 内现有箭矢模板，以及
  `Assets/Game/ArtSrc/Physics/DrawStrokeStable.physicsMaterial2D`；不新增资源。
- 事件：不新增全局事件；trap 和结算均复用既有事件/局部回调。
- Analytics/SDK：无新增或修改。
- 平台：没有 Android/iOS 分支；箭矢不定时销毁，单局数量只由 20 秒倒计时和动作周期限制。

## 4. 影响文件与资源

- `Assets/Game/Scripts/Level/DrawLevel/Level5/DLevel5ActorCtrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level5/DLevel5Ctrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level5/DLevel5ArrowImpactHandle.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level5/DLevel5ArrowTrapHandle.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_005.prefab`
- `Assets/Game/ArtSrc/Physics/DrawStrokeStable.physicsMaterial2D`（仅复用，资源本体未修改）
- `Assets/Game/Scripts/Level/MainLevel/Level_27/OnTriggerTrapHandle.cs`
- 本功能风险记录、脚本结构记录、中央结构演进记录和 `DevelopMD` 事实记录。

未修改共享 `ActorCtrl`、`DrawLevelCtrl`、`DrawLineFeature`、
`DrawLevelCountdownCtrl`、`GamePlayPanel`、箭矢共享资源、DB、Luban 或平台配置。

## 5. 验证证据

- CodeGraph 已识别 `DLevel5ActorCtrl -> ActorCtrl` 继承关系，以及
  `DLevel5Ctrl -> IDrawStrokeCompletedHandler`、射箭动作、生成箭和结算停止调用关系。
- Unity Editor 6000.0.59f2 已刷新新增脚本并输出 `Finished compiling in 25s`；该次编译
  日志未出现本次脚本错误。日志中存在工程既有材质 Drawer warning，与本关脚本无调用关系。
- 命令行 `dotnet build Assembly-CSharp.csproj --no-restore ...` 在工程依赖阶段超过
  120 秒超时，未返回可作为成功或失败依据的最终结果。
- 随后使用相同工程并设置 `BuildProjectReferences=false` 进行聚焦复核，5.58 秒完成，
  结果为 `0 warning / 0 error`；Unity Editor 的完整程序集刷新证据继续覆盖项目引用解析。
- 2026-07-28 将箭改为左向且默认速度提高到 15 后，再次执行相同聚焦构建，11.14 秒完成，
  `0 error / 103 warning`；本次方向与速度修改未新增编译错误，warning 未在本次任务中处理。
- 方向与速度修正后 Unity Editor 再次完成脚本编译并输出 `Finished compiling in 18s`；
  最近 500 行 Editor.log 未出现 `DLevel5`、`error CS` 或 YAML 错误。
- 改为 Impulse 且移除定时销毁后，聚焦构建 8.10 秒完成，结果为
  `0 error / 103 warning`；静态扫描确认脚本与 prefab 已无 `arrowSpeed`、
  `arrowLifetime`、直接写箭矢 `linearVelocity` 或带时间参数的 `Destroy(arrow, ...)`。
- Trap 延迟派发补充销毁取消后再次聚焦构建，结果为
  `0 error / 103 warning`（10.17 秒）；目标 C#/Markdown diff 检查通过。
- 2026-07-31 使用 Unity 当前 Bee 的 `Assembly-CSharp.rsp` 和 Unity 自带 Roslyn
  对完整 `Assembly-CSharp` 做独立编译，结果 `0 error`；输出仅有工程既有 warning，
  本次三个 Level5 脚本没有新增编译诊断。运行中的 Unity 随后也重新生成
  `Library/ScriptAssemblies/Assembly-CSharp.dll`；最新编译与程序集重载区段未出现
  `error CS` 或 Level5 编译错误。
- 2026-07-31 17:40 复核 `CS1501`：`DLevel5Ctrl` 的三参调用先于新增
  `DLevel5ArrowImpactHandle.Initialize(Rigidbody2D, Vector2, float)` 文件完成导入，Unity
  在两文件分步保存的中间轮次产生了旧错误。强制刷新后最新 Tundra build
  成功，`Assembly-CSharp.dll` 于 17:40:10 重新生成；当前调用与唯一重载
  签名完全匹配，无需反向改为两参或删除渐显参数。
- BadBox 出生重叠修复后，Unity 于 17:14:19 再次生成 Assembly-CSharp.dll，时间晚于
  Level5 脚本修改；最新编译/重载区段没有新增 `error CS`。静态断言确认发射点计算发生
  在 Instantiate 前，同一 `(5.387,-0.95)` 起点用于重新计算弹道，且没有按 Layer/Tag
  屏蔽 Player、画线或地板。
- 高度、悬空和朝向修复后，Unity 于 17:23:49 再次生成 Assembly-CSharp.dll，时间晚于
  两个改动脚本；最新编译/重载区段未出现新增 `error CS`。静态断言确认 Prefab 高度为
  `2.5`、运行时箭使用单位旋转与 `FreezeRotation`、碰撞解析同时检查双方 Collider 并
  排除箭自身层级、箭间接触恢复飞行，以及朝向只使用速度的绝对角。
- 固定物理发射、箭头 Collider、渐显和延迟定格修改完成后，使用当前 Unity Bee
  `Assembly-CSharp.rsp` 与 Unity 自带 Roslyn 对完整 Assembly-CSharp 独立编译，结果
  `0 error`；输出只有工程既有 warning。静态检查确认目标点弹道字段和计算已移除，
  发射使用等效 Impulse，渐显与命中协程可分别取消，命中冻结发生在
  `WaitForFixedUpdate` 之后。运行中的 Unity 随后于 17:50:47 重新生成
  `Library/ScriptAssemblies/Assembly-CSharp.dll`，最近日志未出现 `error CS`。
- 随机弧线与更高仰角调整后，同一 Bee/Roslyn 完整独立编译再次通过，结果 `0 error`；
  Unity 于 18:00:32 重新生成 Assembly-CSharp.dll，最近日志无 `error CS`。静态断言
  确认 Prefab 序列化两项随机幅度，代码对速度和仰角分别调用 `Random.Range`，并保持
  目标点弹道代码无残留；四个边界弹道估算覆盖 `0.84~1.87` 的顶点抬升。
- 前后落点双档机制初版完成后，运行中的 Unity Bee 再次完成
  `Assembly-CSharp.dll` 的 Csc、CopyFiles 与程序集重载，程序集于 `18:21:57` 生成且
  晚于 Level5 脚本；最近 1600 行 Editor.log 中 `error CS=0`、编译失败标记为 0。
  静态断言确认两发随机袋每组取一短一长、角度上限为 `80°`、Prefab 与箭模板重力均为
  `3`，145 个 YAML 锚点保持唯一。PlayMode 视觉和碰撞结果仍待验证。
- 最终按蓝线继续抬高两档后，使用当前 Unity Bee 的完整 `Assembly-CSharp.rsp` 与 Unity
  `DotNetSdkRoslyn/csc.dll` 独立编译完整 Assembly-CSharp，结果为
  `exit 0 / 0 error / 104 warning`；warning 均为工程现有诊断。最终静态边界为短程
  `18.7~19.3 / 63°~65°`、长程 `19.7~20.3 / 57°~59°`，目标点代码仍为 0 处。
- 玩家命中定位与取消击飞修改后，Unity 当前 `Assembly-CSharp.rsp` 已包含新增
  `DLevel5ArrowTrapHandle.cs`；使用同一 Unity Roslyn 完整独立编译再次得到
  `exit 0 / 0 error / 104 warning`。静态断言确认新脚本包含 contact point 与
  `ClosestPoint` 回退、死亡后速度归零且没有 `Knockback` 调用；箭模板只绑定一处局部
  handler，画线仍绑定一处共享 handler。
- 连续区域分层采样修改后，使用同一 Unity Bee 响应文件与 Unity Roslyn 完整编译再次
  得到 `exit 0 / 0 error / 104 warning`。解析当前 Prefab 参数并按全局重力 `-9.81`、
  `gravityScale=4` 做静态弹道检查：8 个分层边界的理论落程从 `10.074` 连续增长到
  `13.817`，相邻边界间距仅在 `0.454~0.476` 之间；顶点抬升保持在 `5.077~5.167`。
- 已静态确认 prefab 上：
  - 只有一个主 `DrawLevelCtrl`；
  - `DLevel5Ctrl` 的主控制器、Enemy 子类和箭模板引用均非空；
  - `shootInterval=0.05`、`attackSpeedMultiplier=2.5`、`launchOffset=(-0.3,0.3)`；
  - `launchSpeed=23`、`launchRangeSpeedBandOffset=0.8`、
    `launchSpeedRandomOffset=0`、`launchAngleDegrees=61`、
    `launchRangeAngleBandOffsetDegrees=4`、`launchAngleRandomOffsetDegrees=0`、
    `launchDistributionSegmentCount=8`、`arrowGravityScale=4`、`arrowMass=8`、
    `arrowFadeInDuration=0.06`；
  - arrow 的 Rigidbody2D 为 Dynamic、显式质量 8、Continuous、Interpolate、
    FreezeRotation；BoxCollider2D 只覆盖箭头 `offset=(0.7,0)`、`size=(0.3,0.28)`；
  - Enemy 原 `ActorCtrl` 已由 `DLevel5ActorCtrl` 替换，并同步更新 Skin 绑定；
  - 箭模板使用 `DLevel5ArrowTrapHandle(isCollision=true)`，原 actor/knock 引用保持非空；
  - 运行时线模板仍使用共享 `OnTriggerTrapHandle(isCollision=true)`；
  - 倒计时当前是 `cdTime=10`、`isSuccess=true`。
- Prefab YAML 当前共 149 个对象锚点且无重复、无缺失内部 fileID 引用；新增 trap 脚本
  meta GUID 只由自身 meta 与箭模板引用。
- 线摩擦与失败留箭修改后，使用 Unity 当前 Bee 的 `Assembly-CSharp.rsp` 和 Unity Roslyn
  独立编译完整 Assembly-CSharp，结果为 `exit 0 / 0 error / 104 warning`；warning 均为
  工程既有诊断。静态检查确认 DLevel_005 的 `m_Material` override 指向现有
  `DrawStrokeStable` GUID、Prefab 仍为 149 个唯一锚点且无缺失内部 fileID；同时确认
  `OnLevelResolved` 只在 Success 清箭，`OnDrawAttemptReset` 与 `OnDisable` 仍保留清理。
- 本次 C#/Prefab/Markdown 目标文件 `git diff --check` 通过；仅输出仓库既有的
  LF/CRLF 转换提示，没有新增尾随空格错误。

## 6. 风险分层与未验证项

### 6.1 Facts

- Player prefab 实例初始 X 为 `-5.39`，Enemy 初始 X 为 `6.82`。
- 箭模板初始世界位置约为 `(6.387,-1.95)`；当前 `launchOffset=(-0.3,0.3)` 把运行时
  箭根移到约 `(6.087,-1.65)`，箭头小碰撞盒本身继续位于飞行方向最前端。
- Player 初始 X 约为 `-5.39`；静态地面顶约为 `y=-2.6664`。按当前连续走廊边界估算，
  箭根无遮挡落程为 `10.074~13.817`，对应世界 X 约 `-3.987~-7.730`，完整覆盖 Player
  前后区域；8 个分层边界的落程间隔为 `0.454~0.476`。理论顶点相对发射点抬升为
  `5.077~5.167`，保持用户蓝线标注的高弧线。箭头碰撞体、Player 或画线提前接触仍会
  改变最终可见扎箭位置。
- DLevel_005 的运行时画线质量由当前 `strokeMass=80` 与长度规则决定，约为 `20~80`；
  本次没有覆盖该配置。箭显式质量 8、连续速度 `22.2~23.8`，初始动量标量范围约为
  `177.6~190.4`。
- `DrawStrokeStable` 的序列化值为 friction `0.8`、bounciness `0`，且本关只在画线模板
  Rigidbody2D 上增加该 Prefab override；共享画线模板资源没有改动。

### 6.2 Direct conclusions

- 画线模板改用 Collision2D 后，与 `DrawLineFeature` 生成的非 Trigger Collider2D
  类型一致。
- Enemy 运行时 Collider2D 被关闭，箭不会因出生点与射手自身重叠而触发玩家 trap。
- 任意结算事件到达后都不会再启动下一套射箭动作；Success 立即清箭，Failed 保留现场箭
  至旧关卡禁用/销毁。后续重载创建的是全新 DLevel 实例，旧箭不会进入下一局。
- 箭不会因固定存活时长在关卡进行中消失；缺失 Rigidbody2D 的无效克隆仍会立即销毁。
- 每支新箭只忽略本关当前尝试内其他运行时箭，不会削弱箭与 Player、玩家线或场景地板
  的碰撞。命中后关闭该箭的模拟，可避免后续碰撞解算继续改变角度。
- 命中分类只使用排除箭自身层级后的真正对方 Collider；箭与箭偶发接触会恢复上一帧
  飞行速度，不会因为 Collision2D 两端字段语义而误判成“命中空气”。
- 箭头使用速度向量的绝对角度，不再累计模板 Y 翻转与角度差；飞行刚体冻结物理旋转，
  因此碰撞冲量不会使其持续乱转。
- 场景命中不再在 Collision2D 回调内立即清速，而是等到下一物理步再定格，避免在接触
  回调中抢先结束箭的 Dynamic 状态；画线仍保持自己的质量、重力和约束。
- 旧陷阱对象销毁时会取消其未完成的失败事件，不再由新 DrawLevelCtrl 接收旧尝试事件。
- 箭命中玩家时，命中特效坐标来自本次物理接触点；Level5 专用箭陷阱不调用
  `ActorCtrl.Knockback`，并在死亡后清零玩家刚体线速度和角速度。共享陷阱、画线与
  其他关卡的既有击退行为不受影响。
- 分层袋每轮包含 `0~7` 全部分段且每段只消费一次，段内位置和整轮顺序继续随机；
  Reset、结算或 Disable 会清空未消费分层，下一次尝试重新洗牌。
- 本关运行时画线的子碰撞体未设置自己的 PhysicsMaterial2D，因此会使用模板克隆根
  Rigidbody2D 上的 `DrawStrokeStable`；该继承关系仍需在 Unity PlayMode 中做物理手感确认。

### 6.3 Hypotheses

- 当前连续速度 `22.2~23.8`、仰角 `65°~57°`、重力倍率 4 与 8 段分层的镜头构图、
  躲避手感和受阻后的可见分布属于 `uncertain`；速度/角度中点、范围半宽和分层数可在
  Inspector 调整，不应恢复目标点反算。
- 发射点收回弓弦附近并加入 0.06 秒渐显后的视觉衔接是否自然仍需 PlayMode 目测。
- 箭头碰撞盒能否在所有入射角可靠命中 0.1 厚度的动态线段，以及质量 8 对
  `50~200` 质量画线的推动幅度是否合适，仍需物理实测。
- 定格箭会关闭物理模拟，开销低于持续参与解算，但 10 秒内可见对象累计数量和移动端
  渲染开销仍属于 `uncertain`。

### 6.4 Missing evidence

- 修复后尚未重新执行 Unity PlayMode：首笔启动、2.5 倍动作连续性、实际连续射速、
  0.06 秒渐显、8 段连续分层的区域均匀性与蓝线高度、箭头命中细线、画线受力、箭间穿透且不悬空、
  空中朝向稳定、碰线/Player/远端地板后的延迟定格与 trap、Player 身体接触点特效、
  命中后角色保持原地且不跳起、
  暂停恢复、线摩擦手感、死亡淡出期间箭保持可见、10 秒胜利、失败重开、胜利 Replay
  和快速退出均待验证。
- Reset 取消修复尚需在 PlayMode 复验“陷阱触发后立即重建，新 Actor 恢复 prefab 初态”，以及
  正常不重建时 0.5 秒失败事件仍按原时序派发。
- 未执行 Android/iOS 横屏设备验证；帧率、高速 Continuous 碰撞、触控和单局定格箭
  累计开销待验证。
- 未验证本关最终美术摆位与当前速度、仰角、重力倍率是否构成合理攻击路径。

## 7. 回滚

1. 从 DLevel_005 根节点移除 `DLevel5Ctrl`。
2. 将 Enemy 的 `DLevel5ActorCtrl` 恢复为 Actor prefab 原 `ActorCtrl`，并恢复 Skin 绑定。
3. 将画线模板 `OnTriggerTrapHandle.isCollision` 恢复为 `false`。
4. 删除 `DLevel5ArrowImpactHandle`，把 `DLevel5Ctrl` 恢复为原固定左向 Impulse，
   并从 `DLevel5ActorCtrl` 移除播放倍率与射后 hold。
5. 从 DLevel5Ctrl prefab 序列化数据移除 `attackSpeedMultiplier`、`launchOffset`、
   `launchSpeed`、`launchAngleDegrees`、`arrowGravityScale`、`arrowMass`、
   `arrowFadeInDuration`，并恢复箭模板 Collider 与 Rigidbody 原值。
6. 保留原 `DrawLevelCountdownCtrl(cdTime=20, isSuccess=true)` 和既有 prefab 美术内容，
   不涉及存档迁移。
7. 若仅回滚 Reset 取消修复，将 `OnTriggerTrapHandle.TriggerTrapFailed()` 恢复为无
   cancellationToken 的等待；这会重新引入旧事件跨关卡风险。
8. 若只回滚本次玩家命中修正，将箭模板脚本重新绑定为共享 `OnTriggerTrapHandle`，
   删除 `DLevel5ArrowTrapHandle`；这会恢复脚下特效和向上击退。
9. 若只回滚连续区域分层，将 `CalculateLaunchVelocity()` 恢复为两档消费逻辑，删除
   `launchDistributionSegmentCount`，并将速度/角度半宽和额外浮动恢复为
   `0.5 / 0.3 / 3 / 1`；这会重新产生两个落箭簇。
10. 若只回滚线摩擦，删除 DLevel_005 画线模板 Rigidbody2D 的 `m_Material` Prefab
    override；不需要删除或修改共享 `DrawStrokeStable` 资源。
11. 若只回滚死亡现场保留，将 `OnLevelResolved` 恢复为无条件调用
    `CleanupSpawnedArrows()`；这会让 Failed 结果到达时现场箭立即消失。
