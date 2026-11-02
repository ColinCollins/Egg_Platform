# ILevel048Ctrl 功能与风险记录

## 需求与基线（2026-09-07）

- 用户要求 API 控制 Target 跳跃两次，两个终点由 Transform 指定，每跳开始和完成分别触发 Feedback。
- 修改前没有 ILevel048Ctrl 脚本或专项记录；第 048 关 Prefab 及副本已有用户改动。本次新增局部脚本与 meta、本文与 meta，并追加 DevelopMD 使用记录。
- 参考 ILevel047Ctrl 的局部 MonoBehaviour/API/Bear Logger 模式、IWannerLevelCtrl 的 MMF_Player，以及 Level61Ctrl 的 DOJump 用法。不修改共享控制器、其它关卡或 Prefab。

## 使用与行为

1. 在本关对象上挂载 ILevel048Ctrl，Inspector 显式绑定 target、firstJumpPoint、secondJumpPoint；不进行名称或层级查找。
2. 本地 Trigger/UnityEvent 绑定无参 TriggerJump()。一次有效调用连续执行两跳，每跳时长 jumpDuration 默认 0.5 秒，高度 jumpPower 默认 1 世界单位。
3. 两个终点在调用时快照世界坐标，移动标记不会改变本轮轨迹。建议标记独立于 Target 层级。
4. 按用户补充要求，仅保留一个 jumpFeedback（MMF_Player）槽，第一跳开始、第一跳完成、第二跳开始、第二跳完成共用该播放器；空槽跳过。播放器内部的冷却和并发设置仍由 Feel 决定，脚本保证每个阶段调用一次 PlayFeedbacks，不保证任意 Feel 配置均实际播放。
5. 仅运行态且组件启用时接受调用。运行中及完成反馈内的重入忽略；完整结束后允许从当前位置再次触发。
6. 控制器禁用/销毁或 Target 禁用/销毁会取消本轮 Tween，不补发完成反馈，不复位已经发生的位移。已启动 Feedback 自身的播放生命周期由其播放器管理。

## 流程与边界

- 本地 API → 同一个 DOTween Sequence 内两段显式抛物线 Tween → 四次共用 Feedback 调用。两段首尾相接，无间隔、无回调创建第二段。
- 使用受 timeScale 影响的游戏时间；无新增 Update、全局事件、资源加载、存档、UI、打点、平台 SDK 或实例化流程。
- Sequence 以本控制器为 Tween target；Target.DOKill 不再中断本关两跳，目标禁用/销毁和控制器生命周期仍清理本组件序列。

## 验证证据

- dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q：使用临时 CustomBeforeMicrosoftCSharpTargets 显式纳入新增脚本，0 错误、121 警告。没有修改生成工程；本机日志位于 %TEMP%/ILevel048-build.log。
- 静态核对：两段 numJumps=1、四次 Feedback 调用、先设置运行锁再执行回调、运行态门禁、空引用与非法时长检查、禁用/销毁清理。
- CodeGraph CLI 不可用，改用本地源码读取；规则指定的 ../specs/037-remote-resource-loading/plan.md 缺失。知识库 IWanner 检索未提供本关专项要求。

## 风险、缺口与待验收清单

- 当前 Prefab 已由用户挂载脚本、绑定 Target/两个落点/jumpFeedback，并接入 TriggerJump；本次只读核对，未修改 Prefab。Unity PlayMode 及 Android/iOS 横屏真机验证仍为 uncertain。
- Target 若由动态 Rigidbody2D、Animator 或其它 Tween 同时驱动位置，会存在写入竞争。当前实现是 Transform 表现移动，不接管物理状态或玩家控制。
- 手工验证：调用一次依次到达两点，反馈顺序为开始1→完成1→开始2→完成2；途中重复调用不叠加；结束后再次调用可执行。
- 手工验证：第一跳/第二跳中禁用或销毁控制器、Target 后不继续移动或补发落地反馈；暂停恢复符合游戏时间；缺绑定时不启动。
- 手工验证：实际碰撞、落点手感、失败/重开/退出及移动端低帧率。若同实例复用，调用方需负责所需的初始位置恢复。

## 回滚

- 移除本次新增脚本、本文及各自 meta，并删除 DevelopMD 对应追加节。
- 后续若已完成 Inspector 接线，先移除该组件和 Trigger 方法绑定；不改动用户既有 Prefab 内容或共享系统。

## 同日调整：共用 Feedback

- 删除四个独立槽并改为 jumpFeedback；旧字段注释随字段替换，因其已不符合共用引用的代码事实。若已手动绑定旧字段，需重新绑定唯一的 Jump Feedback。
- 第一跳完成和第二跳开始会连续调用同一个播放器，需在 Feel 配置中确认重复播放、冷却设置满足表现要求；PlayMode 实际播放次数仍为 uncertain。

## 同日修正：用户反馈只看到一次跳跃

### facts / 事实

- 用户实测反馈只跳了一次。旧实现会在 API 调用时创建两个 DOJump，再嵌入同一个父序列。
- 当前 ILevel_048.prefab 的 161–166 行已绑定目标、两个落点和反馈；两点同父节点下的局部 X 分别为 2.47、41.47，Y 均为 3.43，跳高 1、单跳时长 0.5。

### direct conclusions / 直接结论与修改

- 不是 Prefab 漏绑第二个落点；两个落点相隔 39 个局部 X 单位。
- 现将两段改为独立 DOJump：第一段完成后播放落地反馈，再创建第二段，使第二跳以第一段实际落点初始化。开始反馈由各段 OnStart 调用。
- 旧段自动回收时只有引用仍匹配才清空运行锁，避免清掉第二段；落地反馈若禁用/销毁控制器或 Target，不再续跳。四次共用反馈、两个终点快照、重复调用门禁保持。

### hypotheses / 待验证假设

- 提前创建并嵌套 DOJump 可能影响后段初始化或轨迹，但未用运行轨迹证明其为原故障根因，不作确定归因。
- 第二落点距离较远可能影响镜头内可见性；是否离开镜头同样为 uncertain，不擅自修改用户落点。

### missing evidence / 验证与缺口

- 修正后显式纳入脚本的 dotnet build 通过：0 错误、121 警告；尚无 PlayMode 轨迹和四阶段回调计数证据，不宣称实测修复。
- 补验重点：首次到达 firstJumpPoint 后再起跳并到达 secondJumpPoint；共四次反馈；第一段自动回收不终止第二段；中途禁用仍正确取消。
- 回滚：仅恢复本脚本为先前父 Sequence 嵌套编排并还原本文对应节，不修改 Prefab 或 meta GUID。
## 同日最终调整：两跳必须连续衔接（当前有效）

- 用户明确要求第二跳连续触发。上一版在第一段完成回调中创建第二段，会依赖后续 DOTween 更新启动；本节替代上一节的当前实现说明。
- 一次 TriggerJump 自动连续两跳，不需要第二次调用。总时间为 2 × jumpDuration；第一段完成、第二段开始两个 Feedback 在同一个时间轴边界连续调用。
- 改为一个 Sequence，内含两段普通 Tween。显式起终点为「调用时当前位置→firstJumpPoint→secondJumpPoint」，每段位置使用 Lerp(start,end,t) + up × 4 × jumpPower × t × (1-t)。因此 t=0/1 精确落在两端，中点相对起终点连线抬升 jumpPower，第二段不依赖提前创建 DOJump 的内部起点缓存。
- 跳跃曲线现在明确为抛物线；不保留之前 DOJump 内部 Y 插值细节。序列时间使用 Linear，保留两段时长、高度、世界坐标快照及取消/防重入边界。
- 原“每段落地后才创建下一段”注释随实现替换，因为已与连续时间轴设计冲突。Prefab 引用和 meta GUID 不变。
- 编译验证：0 错误、121 警告。静态检查两段之间仅有两个零时长反馈回调，无 Interval、无完成回调创建 Tween；PlayMode 视觉衔接、镜头可见性和移动端仍为 uncertain。
- 验收重点：第一段终点是第二段起点，边界无配置等待；一次调用到达两个落点；四个反馈调用顺序不变。低帧率时可能无法显示恰好落地的单帧，但时间轴不添加停顿。
- 回滚：仅还原本次脚本编排及对应说明，不改用户 Prefab；不得将之前只做编译验证表述为运行时修复证据。
## 2026-09-07：实测逻辑取证，仍待实际关卡复现

### facts / 事实

- 用户明确补充：Target 停在第一落点。不能以“第一跳不明显”或“第二点在镜头外”解释此现象。
- 使用 Unity 6000.0.59f2 独立临时项目，复制当前控制器源码与本仓库 DOTween.dll（GF 只读），以简单 MMF_Player 计数适配器代替完整 Feel；在 PlayMode 通过 DOTween.ManualUpdate 推进真实序列。
- 测试通过：总时长 1 秒；0.5 秒位置 (-0.015,0.515,-1)、Feedback 调用数 3；0.75 秒位置 (9.735,1.515,-1)；1 秒位置 (19.485,0.515,-1)、调用数 4。这证明当前控制器在隔离环境执行两段，不能证明实际关卡不受其它流程影响。
- 首轮测试使用 Goto，位置通过但回调未执行，已改为 ManualUpdate 验证，未把首轮结果当反馈链证据。本机最终报告：%TEMP%/ILevel048UnityCheck-result.txt；临时项目和日志：%TEMP%/ILevel048UnityCheck/。
- 两落点实际挂在 Knock_Explosion (1) 下，其位置 (-1.25,-1.2,-1)、缩放 0.5，上层背景及关卡根变换为单位变换。因此世界 X 间距为 19.5，之前仅报告局部间距 39 不足以判断镜头可见性。
- 第 048 关 StopAllDOTween 指向 fileID 7949568883140327883，与受控 Target 1673983454989357950 不同；仅能排除此组件直接按同一 Target 停止的路径。
- 临时只读编辑器探针报告 Editor is not playing，没有采集到运行实例；探针及 meta 已移除。

### direct conclusions / 直接结论

- 隔离环境没有复现“只执行第一段”，不能继续通过重写跳跃算法宣称修复。
- 当前只在 ILevel048Ctrl 增加编辑器条件诊断：API、四个阶段、OnKill 的时间/完成状态/调用栈和引用位置；不改变控制流。C# 编译 0 错误、121 警告。

### hypotheses / 假设

- 实际实例序列中断、对象禁用、热重载运行代码差异仍为 uncertain，待真实日志。
- 第一跳终点比起点低 2.915，现有弧线最大只高于起点约 0.07358，属于另一个表现线索，不能解释用户确认的停在第一点。

### missing evidence / 下一步与回滚

- 已请求用户重新进入第 048 关复现；待直接读取本机 Editor.log 的 [ILevel048诊断]，定位首段后是否进入第二段及中断调用方。
- 此阶段未修复已确认的实际故障；不得标记已解决。诊断完成后删除 TraceJump 及其调用，保留真正修复与证据；无需改 Prefab 或 GUID。

## 最终定位与修复：门下 Trigger 杀死两跳序列（当前有效）

### facts / 运行证据

- 用户复现后，Editor.log 中实例 -1445252 在 frame 281 记录 API 和第一跳开始；frame 337 记录序列被杀，尚未记录第一跳完成。
- 实际调用栈：BaseItemExecutor.Update → OnUpdate → IWannerGame_StopAllDOTween.Execute（40 行）→ Transform.DOKill → DOTween.Kill → 本控制器 OnKill。目标当时仍启用，位置约 (-1.09,2.43,-0.95)。OnKill 里的 0/0 时长不作为中断时刻证据，以阶段缺失和调用栈为准。
- 再次全量核对 Prefab 发现第二个 StopAllDOTween：ILevel_048.prefab 的 2305–2308 行，组件 fileID 8243594022654261487，正指向跳跃 Target 1673983454989357950。它挂在门下 Trigger (1)，由 OnTrigger2DHandle → TriggerExecutor 触发。先前只查到第一个停止器，排除范围不完整；本节纠正此前结论。

### direct conclusions / 修复

- 原序列 SetTarget(target) 会被门下停止器整体取消；问题并非缺少第二段，也不是跳跃衔接等待。
- 仅将本关跳跃 Sequence 的归属改为 SetTarget(this)，保留 SetLink(Target, KillOnDisable) 以及 OnDisable/OnDestroy 清理。连续两跳作为控制器持有的完整演出，不再由门的 Transform.DOKill 停止。
- 不修改共享停止器、Executor、Prefab、落点或其它关卡；停止器仍可停止该 Transform 的其它 Tween。未来若需要专门取消本演出，应调用本控制器生命周期或明确设计局部 API，而不是依赖 Transform.DOKill。
- 临时 TraceJump、四阶段诊断调用和编辑器探针均已移除。

### 验证证据

- 修复前：独立 Unity PlayMode 在 0.25 秒调用 Target.DOKill(false)，目标停止，0.5 秒第一终点断言失败，Feedback 只有 1 次，复现中断路径。
- 修复后：同样调用 Target.DOKill(false)，0.5 秒准确到达第一点，0.75 秒处于第二跳，1 秒准确到达第二点；Feedback 共 4 次，测试通过。
- 同轮测试验证：重复 TriggerJump 不替换当前序列；控制器禁用后目标不再移动且不补发完成反馈。dotnet build 为 0 错误、121 警告。
- 本机修改前报告：%TEMP%/ILevel048UnityCheck-before.txt；修改后报告：%TEMP%/ILevel048UnityCheck-result.txt。测试执行实际控制器与项目 DOTween.dll，Feel 使用计数适配器，不代表相机震动实际渲染验证。
- 用户完整关卡的修复后画面与移动端真机仍未复验（uncertain）；实际根因由用户复现日志确认，修复路径由独立 Unity 回归验证。

### 回滚

- 将 SetTarget(this) 还原为 SetTarget(target) 即回退此修复，但将恢复门下停止器中断两跳的已证实问题；无需修改 Prefab 或 GUID。