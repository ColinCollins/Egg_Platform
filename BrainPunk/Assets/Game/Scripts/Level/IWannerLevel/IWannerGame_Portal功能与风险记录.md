# IWannerGame_Portal 功能与风险记录

日期：2026-08-14

## 需求与原始基线

- 用户要求新增 `IWannerGame_Portal`，把 `target` 传送到 `endPoint.position`；随后明确更正触发结构：
  Portal 使用自身 `BoxCollider2D` 做 Tag 检测，只有进入者匹配指定 Tag 才触发，触发者不要求属于 `target`。
- 用户进一步明确 `target` 必须改为 `ActorCtrl`；传送期间禁止 Actor 移动，并且只有成功移动到 `endPoint` 后才允许恢复。
- 当前表现要求替换纯占位等待：Actor 先从可见尺寸 `DOScale` 到 0，中间延迟可配置且默认 1 秒，移动到
  `endPoint` 后再从 0 以 `Ease.OutBack` 缩放回可见尺寸；缩放时长需要暴露给 Inspector。
- Portal 需要提供运行时 API 切换 `endPoint`，供关卡逻辑在触发前或传送搬运发生前改写目标点。
- 第 037 关要求当前传送始终使用旧终点；Actor 完整传送并恢复移动后再等待 1 秒，才切换到预约的新终点。
- 当前阶段在两段缩放之间用默认 1 秒的 `UniTask.Delay` 保留表现空挡；后续再补更完整的目标动画效果和粒子移动表现。
- 实现前 IWanner 目录没有传送门运行时脚本。当前
  `Assets/Game/Prefabs/IWannerGameItems/IWannerGame_Portal.prefab` 已挂载该脚本，其根节点带 `BoxCollider2D` 且
  `isTrigger=true`；本次停止问题修复不修改该共享 Portal prefab。

## 影响文件、资源与系统

- 新增脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_Portal.cs`。
- 新增 Unity 元数据：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_Portal.cs.meta`。
- 新增本功能与风险记录及对应 `.md.meta`。
- 复用 Unity `OnTriggerEnter2D`、DOTween、UniTask 取消机制和 Bear Logger；未新增全局事件、逐帧 `Update`、DB/Save、
  配置、资源加载、UI、Analytics、广告或平台 SDK 链路。
- 本次没有修改 Portal prefab、`ILevel_036` 或其它已完成关卡资源；第 037 关实例现有 `target/endPoint` 绑定保持不变。

## 修改后行为与调用流

1. 组件通过 `RequireComponent` 要求同对象具备 Trigger `BoxCollider2D`，Inspector 配置 `triggerTag`、
   `ActorCtrl target` 和 `Transform endPoint`；`triggerTag` 默认 `Player`。
2. `OnTriggerEnter2D` 使用 `other.CompareTag(triggerTag)` 检查进入者；只有 Tag 匹配才启动流程。触发 Collider 与
   `target` 相互独立，允许由一个对象触发、传送另一个指定对象。
3. 对外 API `SetEndPoint(Transform newEndPoint)` 可切换终点。有效引用会立即写入；传入 `null` 时保留原终点并
   通过 Bear Logger 警告。因为搬运发生时才读取字段，Actor 尚未搬运前调用会影响本次传送。
4. 对外 API `SetEndPointAfterTeleport(Transform newEndPoint, float delaySeconds)` 只预约新终点，不立刻覆盖当前
   `endPoint`；下一次成功传送完成、Actor 恢复移动并等待指定游戏时间后才应用。重复预约以后一次为准。
5. 首次有效进入会缓存本次 `ActorCtrl`，先调用 `SetMoveInput(0)` 与 `StopMoving()`，再建立运行中门禁，
   避免多个 Collider 在等待期间启动多条传送流程。`StopMoving()` 会关闭 Actor 的 `canMove` 并清零刚体速度，
   共享跳跃入口同样受该移动门禁限制。
6. 开始传送时把 Actor `Rigidbody2D.simulated` 设为 false，确保缩放、延迟和搬运期间不受重力下落；缺少刚体时记录警告并继续视觉流程。
7. 以 Actor 触发时的 `localScale` 作为“1”，先用 `scaleDuration` 线性 `DOScale` 到 0；`scaleDuration` 默认
   `0.25s`，消失与出现两段共用。
8. 消失完成后通过 `UniTask.Delay` 等待 `teleportDelay`，默认 `1s`；随后设置
   `target.transform.position = endPoint.position`。
9. 到达终点后从 0 `DOScale` 回触发时尺寸，使用 `Ease.OutBack`；只有出现 Tween 完整完成后，才重新启用
   `Rigidbody2D.simulated` 并调用 `ResumeMoving()`，恢复移动与下落。
10. 若存在延迟终点预约，恢复 Actor 后才开始额外等待；等待结束后切换 `endPoint`，此前的搬运全程使用旧终点。
    额外等待期间仍保持 Portal 的重复触发门禁。
11. 组件禁用、销毁、Tween 被外部终止或引用失效会取消/终止未完成传送，不会恢复移动或下落；这符合
   “只有完整抵达并出现后才恢复”的当前约束。

## 验证记录

- 先用 CodeGraph 核对 IWanner 相邻 Trigger 组件、`BaseItemEventHandle` 和现有调用边界，再直接检查未索引的新脚本与 Portal prefab YAML。
- Unity 生成的 `Assembly-CSharp.csproj` 已纳入新脚本；执行
  `dotnet build Assembly-CSharp.csproj --no-restore --nologo --verbosity:minimal`，包含延迟终点预约 API 的最终实现结果为
  `0 error / 101` 个工程既有 warning。
- 静态确认入口由 `Application.isPlaying` 保护；脚本没有 `Update`、`OnValidate`、`Reset`、
  `[ExecuteAlways]`、`[ExecuteInEditMode]`、实例化、DB/Save 或全局事件访问。
- 静态确认 `target` 已改为 `ActorCtrl`，`SetMoveInput(0) -> StopMoving() -> 缩小 -> 延迟 -> 使用旧终点搬运 -> OutBack 放大 -> 恢复刚体 -> ResumeMoving() -> 延迟预约时间 -> 切换新终点` 顺序明确；
  指定 Tag 门禁、重复触发门禁、禁用/销毁取消、空引用保护和 Bear Logger 缺失绑定提示均已存在。

## 风险与未验证缺口

- `uncertain`：虽然脚本及第 037 关实例的 Inspector 引用已有静态 YAML 证据，但 Tag、Collider/Layer 矩阵、
  默认 1 秒节奏、暂停恢复和实际落点尚未做 Unity PlayMode 验证。
- `uncertain`：粒子移动仍是明确的后续需求；当前目标消失/出现仅使用 Transform 缩放，没有粒子、透明度或 Spine 动画。
- 若 `endPoint` 位于另一传送门 Collider 内，目标落地后可能触发反向传送。制作关卡时应把终点放在出口碰撞区外，
  或在后续双向 Portal 设计中新增跨 Portal 冷却协议。
- `CompareTag` 检查进入 Collider 所在 GameObject 的 Tag；若 Tag 只配置在父节点、Collider 在未同步 Tag 的子节点，
  当前不会触发。Prefab 制作时需把指定 Tag 配置到实际进入 Trigger 的 Collider 对象上。
- `StopMoving()` 会在入口清零刚体速度，抵达出口后不会恢复入口前速度；重新启用刚体后从静止状态开始受重力下落。
- 取消、Portal 禁用/销毁、Tween 被杀或运行中引用失效时，Actor 按当前明确要求保持不可移动且刚体不模拟；若这些情况可能发生在关卡继续运行期间，
  外部 Reset/重载流程必须负责恢复或重建 Actor，避免永久锁定。
- 当前缩放目标是 Actor 根 Transform，若其它系统同时控制根缩放，两个 Tween 会竞争；具体关卡应避免传送期间并发根缩放。
- `SetEndPoint(...)` 在 Actor 搬运前会立即影响当前传送目标；需要保持本次旧终点时，应使用
  `SetEndPointAfterTeleport(...)`。延迟预约在传送取消时不会应用，而是保留到下一次成功传送。
- `uncertain`：若全局暂停在 `teleportDelay` 或缩放期间发生，而项目暂停没有停止游戏时间，抵达终点后的 `ResumeMoving()` 可能与暂停锁竞争，
  需在实际 IWanner PlayMode 暂停流程中验证。
- 所有等待均使用游戏时间；暂停时动画占位和传送后终点切换延迟同步暂停。当前未接入原生覆盖层，也没有 Android/iOS
  平台分支，横屏真机表现与性能尚未验证。

## 回滚路径

- 在尚未绑定 prefab 的当前状态下，删除新增脚本、脚本 `.meta`、本记录和记录 `.meta`，并移除共享设计记录中的对应条目即可回滚。
- 若后续已有 prefab 或关卡实例绑定，应先移除 `IWannerGame_Portal` 组件，再删除脚本，避免产生 Missing Script。

## 2026-08-18：Actor_IWanner 双段 distortion 联动增量

### 需求与原始基线

- 用户要求 Portal 的 `target` 为 IWanner 角色时，在入口开始缩放时触发一次 `distortion`，Actor 设置到
  `endPoint.position` 后再次触发；出口 `distortion` 完整播放前禁止恢复移动和 Rigidbody2D 物理。
- 修改前 Portal 已在入口执行 `StopMoving()`、清零速度并关闭 `Rigidbody2D.simulated`，但只等待出口
  `DOScale` 完成便恢复物理和移动，没有调用或等待 `distortion`。
- 修改前 `Actor_IWanner.PlayDistortion()` 只提供无返回值的普通播放入口，外部无法取得本次 `TrackEntry` 来区分
  `Complete` 与 `Interrupt`。

### 影响文件与系统

- 修改 `Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_Portal.cs` 与
  `Assets/Game/Scripts/Level/Actor/Actor_IWanner.cs`。
- 复用现有 Spine `TrackEntry.Complete/Interrupt`、UniTask、DOTween、Actor 移动门禁、`Rigidbody2D.simulated`
  和 Bear Logger；未修改 prefab、共享 `ActorCtrl`、动画资源、配置、存档、全局事件、UI、打点或平台链路。
- 非 `Actor_IWanner` 的 `ActorCtrl target` 不播放 distortion，继续沿用原来的缩放完成后恢复流程。

### 修改后时序与边界

1. Portal 仍先清空输入、停止移动、清零速度并关闭 `Rigidbody2D.simulated`。
2. `target is Actor_IWanner` 时，在入口缩小 Tween 创建前同步调用第一次 `PlayDistortion()`；该动作与缩小并行，
   不额外延长入口等待。
3. 消失和 `teleportDelay` 完成后设置 `target.transform.position = endPoint.position`，随后立即启动第二次
   distortion，并与出口 `Ease.OutBack` 放大并行播放。
4. 出口使用 `UniTask.WhenAll` 同时等待放大 Tween 与第二次 distortion。只有 Tween 完整结束且 TrackEntry 收到
   `Complete`，才重新启用 `Rigidbody2D.simulated` 并调用 `ResumeMoving()`。
5. 第二次 distortion 收到 `Interrupt` 时返回未完成，Portal 记录 Bear Logger 警告并保持移动、Rigidbody2D 锁定；
   Portal 取消、Actor 销毁或死亡同样不会提前恢复。
6. 若角色未死亡但 Spine 绑定缺失或动画播放未返回 TrackEntry，Portal 记录降级警告并只等待出口缩放，避免因表现资源
   异常永久卡死；死亡状态不进入该降级恢复分支。
7. `Actor_IWanner` 保留原 `PlayDistortion()` 对外签名，新增程序集内 `PlayDistortionTracked()` 返回当前 TrackEntry；
   普通调用与 UnityEvent 可见边界不变。

### 验证记录

- 执行 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`：
  `0 error / 109 warning`；warning 为工程既有项，目标脚本没有新增编译诊断。
- 定向 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- 静态确认入口顺序为“锁移动与物理 → 入口 distortion + 缩小 → 延迟 → 搬运到 endPoint →
  出口 distortion + 放大 → 两者完整完成 → 恢复物理与移动”；没有新增 `Update`、全局事件、DB/Save、资源加载或实例化。

### 风险、未验证项与回滚

- `uncertain`：尚未在 Unity PlayMode 验证两次动画的实际可见时机、出口动画与 OutBack 缩放的组合节奏、暂停、死亡、
  Reset、重复触发和 Android/iOS 横屏表现。
- 出口 distortion 若被外部动画中断，Actor 会按明确约束保持锁定；关卡 Reset/重载必须继续负责重建或恢复 Actor。
- Spine 缺失的安全降级会牺牲 distortion 表现以避免流程永久卡死，运行日志会保留诊断证据。
- 只回滚本增量时，移除 Portal 内两次 distortion 调用、`PlayIwannerDistortionAsync()` 与双任务等待，恢复为只等待
  出口缩放；同时删除 `Actor_IWanner.PlayDistortionTracked()`，并把 `PlayDistortion()` 恢复为直接建立 TrackEntry。

## 2026-08-18：入口可见性与出口反向播放修正

### 需求、基线与影响范围

- 用户反馈入口 distortion 不可见，要求移除入口 `DOScale`，改为 distortion 播完后直接把 Scale 设为 0；到达
  `endPoint` 后的 distortion 改为反向播放。
- 修正前入口 distortion 与线性缩小 Tween 同时开始，Actor 会在动画仍播放时迅速缩小；出口 distortion 使用正向播放。
- 修改 `IWannerGame_Portal.cs` 与 `Actor_IWanner.cs`，不修改 Portal/关卡 prefab、Qboxy 动画资源、配置、存档、事件、
  UI、打点、资源加载或平台 SDK。非 `Actor_IWanner` 目标继续使用原入口缩小和出口放大流程。

### 修改后行为与流程

1. Portal 仍先锁输入、移动和 `Rigidbody2D.simulated`，随后等待 IWanner 的入口正向 distortion 完整 `Complete`。
2. 入口 distortion 完成后不再创建缩小 Tween，而是立即写入 `targetTransform.localScale = Vector3.zero`；然后才进入
   原 `teleportDelay` 并搬运到 `endPoint.position`。
3. 到达终点后仍从 Scale 0 执行原 `Ease.OutBack` 出口放大，同时把第二次 distortion 的
   `Spine.TrackEntry.Reverse` 设为 `true`。反向播放仍以正向 TrackTime 推进，因此继续使用原 `Complete/Interrupt` 门禁。
4. 入口或出口动画被中断、Portal 取消、Actor 死亡或销毁时，仍不提前恢复移动和物理；未死亡但 Spine 无法启动时继续记录
   Bear Logger 警告并按原降级边界推进。

### 验证、风险与回滚

- 原始 `Assembly-CSharp.csproj` 构建先被陈旧的缺失源文件引用 `ILevel041Ctrl.cs` 阻断；使用临时 MSBuild 目标仅过滤缺失
  `Compile` 条目后，完整 `Assembly-CSharp` 构建通过：`0 error / 109 warning`。临时目标已删除，warning 为工程既有项。
- 静态确认 IWanner 入口路径不再调用 `DOScale`，出口调用显式传入反向标记；非 IWanner 入口与出口缩放代码保持存在。
- 六个本次修改文件的定向 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- `uncertain`：尚未在 Unity PlayMode 验证入口 distortion 全程可见、Scale 归零帧、出口反向动画与 OutBack 放大的组合节奏，
  也未验证暂停、死亡、Reset、Replay、连续触发及 Android/iOS 横屏表现。
- 回滚本修正时，把 IWanner 入口恢复为 distortion 与 `PlayScaleAsync(...Vector3.zero...)` 并行/先后旧逻辑，出口去掉
  `reverse=true`，同时移除 `PlayDistortionTracked(bool reverse)` 的参数和 `TrackEntry.Reverse` 赋值；无需回滚任何 prefab 或资源。

## 2026-08-18：出口 distortion 提前量修正

### 需求、证据与修改后行为

- 用户在实际观察中确认出口先看到放大、随后才看到 distortion，要求 distortion 提前播放。
- 静态解析当前 `Qboxy.json` 得到 distortion 最长关键帧时间约 `1.1667s`；反向播放起始段的相关 Slot 仍为透明，约推进
  `0.6s` 后才进入可见关键帧。原出口放大仅 `0.25s`，因此同帧启动会先看到 Scale。
- Portal 新增本地序列化字段 `exitDistortionLeadTime`，默认 `0.6s`。IWanner 到达 `endPoint` 后立即启动反向 distortion，
  等待该提前量后再创建出口 `DOScale`；非 IWanner 目标不使用该延迟，原缩放时序不变。
- distortion 与延迟后的 Scale 仍由同一个 `UniTask.WhenAll` 等待，两者都完整完成后才恢复物理与移动。取消、死亡、销毁、
  TrackEntry 中断及延迟期间的取消边界保持不变。

### 验证、风险与回滚

- 临时过滤生成工程中陈旧的缺失源文件条目后，完整 `Assembly-CSharp` 构建通过：`0 error / 109 warning`；临时文件已删除。
- `uncertain`：默认 `0.6s` 是否正好匹配玩家视觉仍需 Unity PlayMode 复核；当前字段可在 Portal Inspector 中按关卡调整。
- 未修改 prefab、Qboxy 资源、配置、存档、事件、UI、打点、资源加载或平台 SDK；Android/iOS 横屏表现未验证。
- 回滚本修正时删除 `exitDistortionLeadTime` 与 `PlayScaleAfterDelayAsync()`，把出口 scaleTask 恢复为直接调用
  `PlayScaleAsync()`；入口和出口反向播放能力不需回滚。

## 2026-08-18：出口 distortion 恢复正向播放

### 需求与修改后行为

- 用户明确撤销出口反向播放要求；出口 distortion 必须与入口一致，使用原动画正向播放。
- 已移除 `Actor_IWanner.PlayDistortionTracked(bool reverse)` 的方向参数和 `TrackEntry.Reverse` 赋值，恢复为无参数的
  `PlayDistortionTracked()`；Portal 的入口与出口统一调用该正向入口。
- 保留上一轮已确认的出口提前时序：到达 `endPoint` 后立即播放正向 distortion，默认延迟
  `exitDistortionLeadTime=0.6s` 后再启动出口 DOScale；两者都完成后才恢复物理与移动。
- 非 IWanner 目标、入口 distortion 完成后 Scale 归零、落点、传送延迟、终点切换、取消和中断边界不变。

### 验证、风险与回滚

- 临时过滤生成工程中陈旧的缺失源文件条目后，完整 `Assembly-CSharp` 构建通过：`0 error / 109 warning`；临时文件已删除。
- 静态扫描确认 `Actor_IWanner.cs` 与 `IWannerGame_Portal.cs` 不再存在 `Reverse`、`reverse` 或带方向参数的播放调用。
- `uncertain`：正向 distortion 提前 `0.6s` 与出口 Scale 的最终视觉节奏仍需 Unity PlayMode 复核；Android/iOS 横屏未验证。
- 未修改 prefab、Qboxy 资源、配置、存档、事件、UI、打点、资源加载或平台 SDK。
- 回滚本次纠正时重新增加方向参数并在出口设置 `TrackEntry.Reverse=true`；不需要修改提前量、prefab 或动画资源。

## 2026-08-18：ILevel_036 WorldCoinAttraction 接入

### 需求、影响范围与现行时序

- 用户要求 Portal 配合 `WorldCoinAttraction`：吸附 target 改为当前 Portal 的 `endPoint`，并在入口 distortion 完成前
  `0.3s` 把特效 position 切到 Actor 位置后触发动画。
- Portal 新增可选序列化引用 `worldCoinAttraction` 与提前量 `worldCoinAttractionLeadTime`。入口 distortion 启动后按
  TrackEntry 剩余时长调度；尚未触发时若动作中断、取消或 Actor 销毁，提前回调不会执行。
- 助手先确保已绑定的特效 GameObject 为 active，再检查组件在层级中有效；随后执行
  `worldCoinAttraction.transform.position = actorTransform.position` 和 `worldCoinAttraction.Play(endPoint)`。
- `Play(endPoint)` 在 WorldSpriteParticleAttractor 内部先替换吸附目标，再清空上一轮粒子并显式播放；Portal 仍等待入口
  distortion 完整完成后才把 Actor Scale 设为 0，继续原 `teleportDelay` 与出口流程，不等待粒子结束。
- `ILevel_036.prefab` 中三个 Portal 共用关卡根节点下的同一个 `WorldCoinAttraction` 实例；其它 Portal/关卡没有绑定，
  继续保持原行为。共享 Portal prefab 只保存空默认引用，未新增全局事件或查找逻辑。

### 验证、风险与回滚

- 临时过滤生成工程中陈旧的缺失源文件条目后，完整 `Assembly-CSharp` 构建通过：`0 error / 109 warning`；临时文件已删除。
- 静态确认 ILevel_036 三个 Portal override 均指向同一个 WorldSpriteParticleAttractor stripped 组件，组件源 fileID、prefab
  GUID、脚本 GUID 与 WorldCoinAttraction prefab 一致；ILevel_036 对该实例局部覆写 `playOnEnable=0`，共享 prefab 不变。
- `uncertain`：尚未在 Unity PlayMode 目测入口 distortion 完成前 `0.3s` 的粒子发射帧、Actor Scale 归零帧、三个 endPoint 的
  实际吸附结果、连续 Portal 触发、Reset/Replay、暂停/死亡和 Android/iOS 横屏表现。
- **P2：三个 Portal 共用一个特效实例。** 若不同 Portal 在一轮粒子结束前又触发，后一次 `Play(endPoint)` 会清空上一轮并
  重新定位置/目标；这与组件既有重复播放语义一致，但多人或并发 Actor 场景需重新评估是否改为独立实例。
- **P2：特效父层级或组件被禁用时不会播放。** Portal 会尝试激活特效自身 GameObject；若父层级仍 inactive 或组件禁用，
  记录 Bear Logger 警告并继续传送，不让纯表现缺失阻断玩法。
- 回滚时移除 Portal 的可选字段与 `PlayWorldCoinAttraction()` 调用/助手，删除 ILevel_036 三个引用 override、
  `playOnEnable=0` 局部 override 和对应 stripped 组件引用；入口/出口 distortion 与传送门禁无需回滚。

## 2026-08-18：出口 distortion 再次改为反向播放

### 需求、基线与现行行为

- 用户撤销上一轮“出口保持正向”的选择，明确要求出口 distortion 恢复反向播放。
- 修改前入口、出口均调用无方向参数的正向 `PlayDistortionTracked()`；修改后入口调用方向参数 `false`，出口调用
  `true`，Actor 侧通过 `Spine.TrackEntry.Reverse` 应用反向，不使用负 `TimeScale`。
- 出口仍先启动 distortion，再按 `exitDistortionLeadTime=0.6s` 延迟创建 OutBack Scale；两者完整完成后才恢复
  `Rigidbody2D.simulated` 与 Actor 移动。入口仍正向完整播放，WorldCoinAttraction 默认在完成前 `0.3s` 触发，完成后
  再把 Actor Scale 设为 0。
- 非 IWanner 目标、Portal trigger、endPoint、teleportDelay、WorldCoinAttraction 的位置/目标、取消与中断门禁均不变；
  未修改 prefab、Spine 资源、配置、存档、事件、资源加载、UI、打点或平台 SDK。

### 验证、风险与回滚

- 临时过滤生成工程中缺失的 `ILevel041Ctrl.cs` Compile 条目后，完整 `Assembly-CSharp` 构建通过：
  `0 error / 109 warning`；临时目标已删除，warning 为工程既有项。
- 静态确认 Portal 入口传 `false`、出口传 `true`，`Actor_IWanner.PlayDistortion()` 的无参数公开调用仍默认正向；
  定向 `git diff --check` 通过，仅有工作区行尾转换提示。
- **P2：反向动画与 0.6 秒提前量的视觉组合仍依赖 Qboxy 当前关键帧。** 当前提前量沿用此前针对反向透明首段的静态分析，
  但最终首帧、末帧和 Scale 露出节奏仍需 Unity PlayMode 目测。
- `uncertain`：尚未验证 Reset/Replay、暂停/死亡、连续 Portal、WorldCoinAttraction 同时播放及 Android/iOS 横屏表现。
- 回滚本次改动时移除 Actor 内部方向参数与 `TrackEntry.Reverse` 赋值，并让 Portal 两次调用恢复无参数正向；
  WorldCoinAttraction、提前量、物理锁和移动恢复门禁无需回滚。

## 2026-08-18：WorldCoinAttraction 提前 0.3 秒触发

### 修改后时序与证据

- 修改前 WorldCoinAttraction 只在入口 distortion 完整 `Complete` 后播放；现在 Portal prefab 新增默认
  `worldCoinAttractionLeadTime=0.3s`，入口启动 TrackEntry 后计算剩余播放时间并提前调度。
- 调度值为 `max(0, (TrackComplete - TrackTime) / abs(TimeScale) - leadTime)`；提前量大于剩余时长或轨道 TimeScale 为 0
  时立即触发。触发时仍先写特效世界坐标为 Actor 位置，再以当前 Portal `endPoint` 调用 `Play()`。
- distortion 完整结束前只触发粒子，不隐藏 Actor；收到 `Complete` 后才写入 Scale 0。一次性门禁避免预回调与完成兜底重复播放。
- 动作在提前点之前中断、Portal 取消或 Actor 销毁时，链接取消令牌会终止尚未发生的回调；若轨道未创建但走既有降级继续分支，
  完成侧兜底仍播放一次，保持旧降级行为。

### 验证、风险与回滚

- 临时过滤生成工程中缺失的 `ILevel041Ctrl.cs` Compile 条目后，完整 `Assembly-CSharp` 构建通过：
  `0 error / 109 warning`；临时目标已删除，warning 为工程既有项。
- 静态确认 shared Portal prefab 默认值为 `0.3`，入口回调位于 Scale 归零前，出口反向调用和 0.6 秒 Scale 提前量未改变。
- **P2：若粒子已在最后 0.3 秒发出，随后 distortion 才被中断，粒子会继续本轮生命周期，但 Portal 会按原门禁停止推进。**
  当前不主动 Stop 共用特效，避免误清理另一 Portal 的后一次播放；需在 PlayMode 验证中断观感。
- `uncertain`：Spine AnimationState 或 Skeleton 级额外 TimeScale 不在当前换算内；暂停、变速、Reset/Replay、连续 Portal 与
  Android/iOS 横屏实际提前量尚未验证。
- 回滚时删除提前量字段、`PlayPreCompleteActionAsync()` 与入口一次性回调，把 WorldCoin 调用恢复到入口完成检查后；
  ILevel_036 绑定、WorldCoinAttraction 通用 API、出口反向 distortion 和物理门禁无需回滚。

## 2026-08-18：ILevel_036～040 Portal Inspector 参数统一

### 当前有效配置与边界

- 用户指定 Group 8 五关的全部 Portal 使用同一组局部参数：`worldCoinAttractionLeadTime=0.2`、
  `scaleDuration=0.15`、`exitDistortionLeadTime=0.3`、`teleportDelay=0.5`，并绑定本关
  `WorldCoinAttraction`。
- 五关共有 23 个 Portal，均通过关卡 prefab override 显式保存上述值；共享 Portal prefab 的默认值和现有脚本行为
  没有修改，因此其它关卡不受本次参数统一影响。
- 每关的 Portal 共用该关根节点下既有的一个 WorldCoinAttraction prefab instance；`ILevel_037`～`ILevel_040`
  只补充指向该既有 instance 的 stripped 组件引用，没有复制特效资源或新增运行时查找。

### 验证、风险与回滚

- 静态逐实例校验确认 Portal 数 `3/3/4/7/6`、四项时间值、WorldCoinAttraction 引用、source component fileID、
  PrefabInstance 映射和 fileID 唯一性全部通过；Unity 6000.0.59f2 已成功导入五个 prefab。
- **P2：同关多个 Portal 的重叠播放仍以后一次为准。** 这是 WorldCoinAttraction 既有清理/重播语义，本次没有改变。
- `uncertain`：专用地图 `--validate-only` 被缺失的 `8-1.png.meta` 阻断；PlayMode 的 0.2 秒粒子提前量、
  0.3 秒出口提前量、0.5 秒传送等待、连续触发、Reset/Replay 和 Android/iOS 横屏表现尚未验证。
- 回滚本次配置时只撤销五关 Portal override 与 `ILevel_037`～`ILevel_040` 的 stripped 引用；共享脚本、Portal prefab、
  WorldCoinAttraction prefab 和此前 `ILevel_036` 的接入能力保持不变。

## 2026-08-18：出口改用 distortion_reverse 独立动画

### 需求、基线与影响范围

- 用户要求 Portal 传送 Actor 后的出现阶段正向播放独立的 `distortion_reverse`，不再把 `distortion` 倒放。
- 修改前入口、出口都加载 `distortion`；出口通过 `Spine.TrackEntry.Reverse=true` 反向应用同一动画。
- 本次修改 `Actor_IWanner.cs` 与 `IWannerGame_Portal.cs`。现有 `Qboxy.json` 已包含
  `distortion_reverse`，本次只复用该动画名，没有修改骨骼资源、Portal/关卡 prefab、配置、存档、事件、资源加载、
  UI、打点或平台 SDK。

### 当前行为与调用流

1. Portal 入口仍传入 `useReverseAnimation=false`，正向播放 `distortion`；入口完成、Actor Scale 归零、
   WorldCoinAttraction 提前触发与 `teleportDelay` 均保持原流程。
2. Actor 移动到 `endPoint.position` 后，Portal 传入 `useReverseAnimation=true`；Actor 选择
   `distortion_reverse` 并按正常 TrackTime 正向播放，不再设置 `TrackEntry.Reverse`。
3. 出口仍先启动动画，再按实例现有 `exitDistortionLeadTime` 延迟启动 OutBack Scale；Portal 继续等待
   `distortion_reverse` 与 Scale 都完整完成后，才恢复 `Rigidbody2D.simulated` 和 Actor 移动。
4. 动画启动失败、中断、Portal 取消、Actor 死亡/销毁的既有降级与锁定边界不变；非 `Actor_IWanner` 目标继续只走缩放流程。

### 验证、风险与回滚

- 静态确认 `Assets/Game/ArtSrc/Spines/sp_qboxy_001/Qboxy.json` 同时存在 `distortion` 与
  `distortion_reverse`；目标脚本不再设置 `TrackEntry.Reverse`。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`：
  `0 error / 109 warning`；warning 为工程既有诊断，目标脚本没有新增编译错误。
- **P2：现有出口提前量需要按新动画观感复核。** `exitDistortionLeadTime` 沿用当前各实例配置，
  `distortion_reverse` 的可见关键帧与此前倒放 `distortion` 不一定完全一致；本次不在缺少 Game View 证据时改动参数。
- `uncertain`：尚未在 Unity PlayMode 目测出现首帧、OutBack Scale 叠加、连续 Portal、Reset/Replay、暂停/死亡及
  Android/iOS 横屏表现。
- 回滚本次改动时移除 `DistortionReverseAnimationName` 与动画名选择，恢复出口对 `distortion` 设置
  `TrackEntry.Reverse=true`；Portal 的落点、提前量、WorldCoinAttraction、物理与移动门禁无需回滚。

## 2026-08-19：抵达终点直接恢复 Scale 1

### 需求、基线与影响范围

- 用户要求 Portal Actor 到达 `endPoint` 时不再播放出现缩放，直接把 Scale 设为 1。
- 修改前 Actor 抵达后仍保持 Scale 0：IWanner 先启动 `distortion_reverse`，再按
  `exitDistortionLeadTime` 延迟创建 OutBack DOScale；普通 `ActorCtrl` 也通过 OutBack DOScale 恢复触发前尺寸。
- 本次修改 `IWannerGame_Portal.cs`、共享 `IWannerGame_Portal.prefab` 及 `ILevel_036`～`ILevel_040`
  五个关卡 prefab；不修改 Actor 脚本、动画资源、WorldCoinAttraction、地图几何、配置、存档、事件、UI、打点或平台 SDK。

### 当前行为与调用流

1. 入口隐藏流程不变：IWanner 完整播放 `distortion` 后 Scale 归零；普通 Actor 仍按 `scaleDuration` 缩小到 0。
2. 传送等待结束后写入 `endPoint.position`，先建立 IWanner 的 `distortion_reverse` 轨道，再在同一同步调用段直接执行
   `targetTransform.localScale = Vector3.one`，不创建出口 Scale Tween。
3. IWanner 仍等待 `distortion_reverse` 完整 `Complete` 后恢复 `Rigidbody2D.simulated` 与移动；动画中断、取消、死亡或销毁
   仍保持原锁定边界。普通 Actor 没有出口动画，Scale 设为 1 后直接进入恢复流程。
4. 已删除不再有消费者的 `PlayScaleAfterDelayAsync()`、`visibleScale` 缓存与 `exitDistortionLeadTime` 序列化字段；
   `scaleDuration` 现在只控制普通 Actor 的入口消失缩放。
5. 共享 Portal prefab 删除一条旧字段序列化值，Group 8 五关删除 22 条对应实例 override，避免保留失效属性路径。

### 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal` 通过：
  `0 error / 106 warning`；warning 为工程既有诊断。
- 静态扫描确认目标脚本、共享 prefab 与五个 Group 8 prefab 均不再包含 `exitDistortionLeadTime`、
  `PlayScaleAfterDelayAsync`、`visibleScale` 或出口 `Ease.OutBack`；定向 `git diff --check` 通过，仅有行尾策略提示。
- **P2：Scale 1 会覆盖触发前的非 1 或负轴根缩放。** 这是用户指定的直接赋值语义；若未来某个 Portal 需要保留特殊尺寸，
  应增加该关局部出口尺寸配置，不能静默恢复为旧的触发前尺寸。
- **P2：IWanner 会在抵达帧立即恢复可见。** `distortion_reverse` 轨道已先建立，但最终首帧姿态与瞬时出现观感仍需
  Unity Game View 目测。
- `uncertain`：连续 Portal、Reset/Replay、暂停/死亡、非 IWanner 目标及 Android/iOS 横屏尚未 PlayMode/真机验证。
- 回滚时恢复 `visibleScale`、出口 Scale 任务、`PlayScaleAfterDelayAsync()` 与 `exitDistortionLeadTime`；共享 prefab 恢复
  `0.1`，ILevel_036 恢复两条 `0.1` override，ILevel_037～040 恢复 `3/4/7/6` 条 `0.15` override。

## 2026-09-04：可选的一次性传送门收缩

### 需求、基线与影响范围

- 用户要求 `IWannerGame_Portal` 增加 `bool isOnce`；启用后，Portal 成功传送一次便以 `Ease.InBack` 在 `0.2s` 内
  `DOScale` 到 `Vector3.zero`。
- 修改前 Portal 在成功传送后会恢复 `_isTeleporting=false`，可继续响应后续合法 Trigger；Portal 自身 Scale 不会改变。
- 本次只修改 `IWannerGame_Portal.cs` 与本记录。没有修改共享 Portal prefab 或任何关卡 prefab，故现有实例缺省
  `isOnce=false`，既有关卡行为保持不变；需要一次性效果的实例必须在 Inspector 显式勾选。

### 当前行为与调用流

1. 首次传送仍完整执行入口隐藏、延迟、落点、出口表现、物理恢复和 `ResumeMoving()`；只有到达这一成功边界后才把
   `_hasTeleported` 标记为 `true`，取消、中断、Actor 死亡或引用失效不会消耗一次性机会。
2. `isOnce=true` 时，成功边界立即创建 Portal 根 Transform 的 `DOScale(Vector3.zero, 0.2f)`，Ease 为
   `Ease.InBack`；同时后续 `OnTriggerEnter2D` 会被一次性状态门禁拒绝，不依赖 Collider 随缩放缩小的时机。
3. 一次性收缩使用独立 Tweener 引用，不与普通 Actor 入口缩放共用 `_scaleTweener`；组件禁用或销毁时会清理 Tween。
   `SetEndPointAfterTeleport()` 的既有延迟切换仍独立执行，收缩不会额外延迟它。
4. `isOnce=false` 时不会写入一次性状态或改变 Portal Scale，所有既有传送与终点切换流程保持原样。未新增全局事件、
   查找、存档、配置、资源、UI、打点或平台 SDK 流程。

### 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal` 通过：
  `0 error / 122 warning`；warning 为工程既有诊断，目标脚本没有新增编译错误。
- 定向 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示；静态确认一次性门禁只在成功恢复移动后设置，失败分支不设置。
- **P2：Portal 根节点缩放会同时缩放其视觉、子节点和 Trigger。** 这是本需求指定的消失方式；代码同时使用状态门禁，
  避免 `0.2s` 收缩期间再次触发。若关卡把不应消失的对象挂在 Portal 根节点下，需要调整 prefab 层级或改绑视觉根节点。
- `uncertain`：尚未在 Unity PlayMode 目测 `InBack` 收缩节奏，也未验证一次性 Portal 的 Reset/Replay、组件禁用再启用、
  连续 Trigger 以及 Android/iOS 横屏表现。
- 回滚时删除 `isOnce`、`_hasTeleported`、一次性 Trigger 门禁、`PlayOnceDisappear()`、独立 Tweener 及其清理调用即可；
  无需回滚 prefab、资源、配置或存档。
