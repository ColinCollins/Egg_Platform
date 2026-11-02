# DLevel_001 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 1 关
- 记录日期：2026-07-29
- 交付/验收状态：Guide 整组落笔隐藏已实现；整关尚未验收
- 参考基线关卡：DLevel_004
- 选择基线理由：两关都通过关卡局部控制器消费 DrawLineFeature 生命周期；DLevel_004
  已记录第一笔完成后的局部表现边界
- 基线证据限制：当前没有 DLevel_001 的既有结构记录，也没有已确认的“已验收”参考记录，
  因此验收状态为 `uncertain`，只以当前代码和 prefab 为事实证据
- 主要脚本：`DLevel001Ctrl.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_001.prefab`

## 2. 脚本与组件树

```text
DLevel_001
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DLevel001Ctrl
├─ DelayAutoBeginDrawHandle
├─ Guide
│  ├─ GuideLine
│  └─ Finger_Scene
└─ level1
   └─ ActorCtrl（玩家 Actor）
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| DrawLevelCtrl | 共享主控制器 | 初始化、输入转发、结算、Reset、局部 Handler 分发 | `IDrawLevelRuntime` | DrawPlayCtrl 随关卡创建/销毁 | 不持有 DLevel1 手指或行走规则 |
| DrawLineFeature | 共享画线功能 | 输入门禁、线段创建、派发落笔/完成信号 | MonoBehaviour | DLevel prefab | 不直接控制 Guide |
| DLevel001Ctrl | 关卡局部控制器 | 落笔隐藏整个 Guide；首笔完成后驱动 Actor 向右移动 | `IDrawStrokeCompletedHandler` | DLevel prefab | 不处理胜负、存档、全局事件或资源加载 |
| DelayAutoBeginDrawHandle | 关卡局部开场 Handle | 首次进关 Erase 完成后延迟开放画线 | `IDrawLevelVisibleIntroHandler`、`IDrawLevelEnterOnlyIntroHandler` | DLevel prefab | 不参与加载、Reset/Replay 或 Erase，不控制手指或 Actor |

## 4. 生命周期与关键调用

1. DLevel prefab 启用时，DLevel001Ctrl 解析 DrawLevelCtrl、DrawLineFeature、Actor 和
   `Guide` 根节点，缓存 Guide 初始显隐并订阅局部落笔事件。
2. DrawLevelCtrl 初始化 DrawLineFeature；关卡准备完成后 EnterLevelLoading 立即进入 Erase。
3. Erase 完成后调用 DelayAutoBeginDrawHandle，按 prefab 当前配置等待，再统一开放输入。
4. 玩家在有效绘制区域落笔，DrawLineFeature 完成 UI、区域、暂停和墨水门禁并创建活动线段。
5. DrawLineFeature 派发 `StrokeStarted`，DLevel001Ctrl 当帧隐藏整个 Guide。
6. 第一笔有效线完成后，DrawLevelCtrl 继续通过 `IDrawStrokeCompletedHandler` 调用
   DLevel001Ctrl，Actor 开始向右移动。
7. 关卡结算、禁用时停止 Actor 移动；禁用时解除落笔订阅。
8. 原地 Reset 时恢复 Guide 初始显隐；当前 Replay 的整关重建也会恢复 prefab 初态。
   Reset/Replay 不执行 Erase，也不重放 prefab 配置的进关延迟；黑幕淡出门禁完成后开放
   输入。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| DLevel_001 根节点 | DrawLevelCtrl | 同根 DrawLineFeature、成功门 Spine | 必填 | 关卡运行时不可用 |
| DLevel001Ctrl | `drawLevelCtrl` | 同根 DrawLevelCtrl | 必填 | 运行时从父层级回退 |
| DLevel001Ctrl | `actor` | 本关玩家 ActorCtrl | 必填 | 运行时从子层级回退；缺失时报 Bear Logger 错误 |
| DLevel001Ctrl | `guideRoot` | `Guide` 根节点 | 可回退 | 按固定层级回退；仍缺失时跳过显隐 |
| DelayAutoBeginDrawHandle | `delay` | prefab 序列化值（本次不修改） | 必填 | 负数在运行时按 0 秒处理 |
| Finger_Scene | Animator | DLevel1 手指动画控制器 | 表现必填 | 手指不播放预设动画 |

## 6. 数据、UI、资源与事件流

- DB/存档：无。
- Luban/配置：无新增或读取。
- UI：无直接 UI 交互。
- 局部引用：DLevel001Ctrl 显式持有 Actor 和 Guide 根节点。
- 事件：DrawLineFeature 的本地 C# `StrokeStarted`；不新增全局事件。
- 资源加载：全部随 DLevel_001 prefab 加载，无新增地址。
- Analytics/SDK：无。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| 响应有效落笔而非只响应有效笔画完成 | 手指应在开始绘制时立即消失 | 落笔当帧隐藏提示 | 鼠标、触摸、无效按下、UI 点击 | 观察中 |
| 同一局部控制器同时管理提示与 Actor 行走 | 两项均为 DLevel1 专属且时机相邻 | 不增加新组件或全局事件 | 订阅、完成回调、Reset | 否，保留本关局部 |
| GuideLine 与 Finger_Scene 由根节点统一控制 | 需求明确隐藏整个 Guide | 落笔后整组提示消失 | Prefab 绑定和 PlayMode 视觉 | 否 |
| 延迟门禁只在首次进关的 Erase 完成后执行 | 输入延迟不应阻塞视觉转场，也不应在 Reset 重放 | 首次进关仍按 prefab 配置延后；Reset/Replay 只保留黑幕淡出门禁 | 正常进入、Reset/Replay、暂停 | 共享修复，已记录 |

## 8. 可复用结论与优化候选

- 本关继续沿用的稳定结构：共享 DrawLineFeature 只产出生命周期信号，局部控制器消费表现。
- 本关验证的新结构：有效落笔可以使用空参数局部事件表达，不把单关对象写入共享画线组件。
- 暂不共享的局部实现：`Guide` 路径、显隐和 Reset 规则只留在 DLevel1。
- 后续观察：若多个关卡都需要落笔回调，再评估统一局部 Handler；当前不升级共享接口。

## 9. 验证证据与缺口

- 静态检查：落笔事件位于所有输入门禁和线段创建成功之后；Prefab 引用指向
  `Guide` 根节点；订阅与解除路径成对。
- 编译：`dotnet build Assembly-CSharp.csproj --no-restore -m:1` 通过，
  最新验证为 `0 error / 101 warning`；warning 未指向本次修改文件。
- Unity PlayMode：未执行。
- Android/iOS：未执行。
- 未验证项：点按取消、暂停恢复、Reset/Replay 后黑幕淡出完成即画线、首次有效笔画完整
  体验和移动端多指输入。

## 10. 风险与回滚

- 主要风险：共享事件时机若异常会影响 DLevel1 提示显隐；点按成功创建后即使最终短线取消，
  整个 Guide 仍会隐藏，这是“开始绘制”语义下的当前设计，体验仍待 PlayMode 确认。
- 时序风险：`delay` 使用缩放时间；若 Erase 完成后游戏仍保持 `timeScale=0`，输入门禁不会
  结束。当前静态代码无法替代暂停/恢复 PlayMode 验证。
- 回滚路径：移除 DrawLineFeature 的落笔事件、DLevel001Ctrl 的 Guide 控制和
  prefab 字段绑定；不需要迁移存档或资源。
- 本次 Reset 跳过规则可单独回滚为普通 `IDrawLevelIntroHandler`，但会恢复 Reset 后额外
  等待 prefab 配置时长。

## 11. 2026-08-07 HUD Tips 按钮隐藏

- `DrawPlayCtrl` 以标准化后的资源路径
  `Assets/Game/Prefabs/DrawLevel/DLevel_001.prefab` 识别本关，不依赖当前显示排序 ID。
- 创建或 Reset/Replay 重建 `DrawGame_GamePlayPanel` 后，控制器通过内部 UI API 隐藏整个
  `TipsBtn`；其 warning 子节点随父节点不可见，本关也跳过 warning 阈值计时和事件派发。
- 进入其它 Draw 关卡会重新计算资源身份并显式恢复 Tips 按钮。本次不修改 DLevel_001 prefab、
  `DLevel001Ctrl`、Guide、画线输入、胜负、墨水、配置、存档或打点生命周期。
- 目标差异检查与 `Assembly-CSharp.csproj` 构建通过，结果为 `0 error / 101 个工程既有 warning`。
- Unity PlayMode 和 Android/iOS 横屏验证仍为 `uncertain`；完整风险与回滚见
  `DLevel_001Tips按钮隐藏功能与风险评估.md`。
