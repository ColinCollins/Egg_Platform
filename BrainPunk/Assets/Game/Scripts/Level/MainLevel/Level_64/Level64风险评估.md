# Level64 风险评估

## 原始行为基线

- `Level64Ctrl.Start()` 订阅 `SpriteDragRotate.OnAngleChanged`，初始化 `Level64BlockCtrl`，并刷新剩余砖块数量文本。
- 玩家进入 output/input 区域时，仍通过 `OnPlayerEnterOutputArea(Collider2D)` 和 `OnPlayerEnterInputArea(Collider2D)` 转发到 actor 级取砖/放砖 API。
- `TryDropInputForActor(Level64ActorCtrl)` 是玩家和 AI 共用的成功放砖计数路径；每次成功放砖只执行一次 `currentBlockCount++`。
- rotate-to-max 路径仍通过 `OnDragToMaxRotateCallback()` 推动 output 根节点移动到 input 根节点后调用完成逻辑。
- `Level64BlockCtrl` 负责 output/input 双层显示缓存、对象池注册、input 层回收、output 层补排，且不持有 `Level64Ctrl`。
- `Level_064.prefab` 当前序列化证据显示 `maxBlockCount: 10000`、`maxAICount: 2`、`blockCountTxt` 已绑定；`TaskTips` / `SuccessTips` 对象存在于 prefab 子节点中。
- 2026-06-10 v2.0.0 要求第 1 个 helper 切为 `box_rich`、第 2 个 helper 切为 `box_bad`；2026-06-23 v2.1.0 继续保留该生成顺序 full-body 皮肤，只在生成后移除从玩家 actor 克隆来的 SkinPart 部件外观和克隆体 `ActorSkinBinding`。

## 受影响文件与资源

- `Assets/Game/Scripts/Level/LevelItem/Level_64/Level64Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_64/Level64AICtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_64/Level64BlockCtrl.cs`
- `Assets/Game/Prefabs/Level/Level_064.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_64/Level64功能说明.md`
- `DevelopMD/已有功能设计与使用记录.md`

## 已使用的现有系统

- `ActorSpineCtrl.SetSkin(string)` 继续用于 Level64 helper 的生成顺序 full-body 皮肤：第 1 个 helper 使用 `box_rich`，第 2 个 helper 使用 `box_bad`。
- `ActorSkinBinding` 是玩家 Actor 的 Skin 绑定组件；生成 helper 后会在 helper 克隆体上禁用该组件，避免后续事件刷新玩家 Skin。
- `BoneFollowerAutoBinder` / `BoneFollower` 用于识别 `back`、`face`、`body` Skin 部件挂点，清理时只删除挂点子物体，不删除挂点自身。
- `Level64ActorCtrl.TransportState` 继续作为 AI 运输方向状态。
- `Level64Ctrl.TryPickupOutputForActor()` / `TryDropInputForActor()` 继续作为真实取砖、放砖、计数入口。
- `Level64BlockCtrl` 的 output/input 双层缓存继续承担有限可见砖块复用。
- DOTween 继续用于取放砖 jump、rotate-to-max output 移动、完成剩余数反馈。
- Bear.Logger 用于本次新增 Level64 gameplay 日志。

## 已实现的风险控制

- Helper 上限默认值改为 2，且 `Level_064.prefab` 已序列化为 2。
- `Level64AICtrl` 保留按生成顺序调用 `SetSkin("box_rich")` / `SetSkin("box_bad")` 的路径，避免 v2.0.0 helper 视觉表现回归。
- `TrySpawnAI()` 在 `CreateToolActor()` 后立即调用 `RemoveGeneratedAIInheritedSkins(createdTool)`，再调用 `AssignSpawnOrderSkin(createdTool, spawnOrder)`；清理和赋值范围只作用于新生成的 helper。
- `RemoveGeneratedAIInheritedSkins()` 会禁用 helper 子层级中的 `ActorSkinBinding`，同时清空 `back` / `face` / `body` Skin 部件挂点下的子物体；不会清空 helper 的 full-body Spine runtime skin。
- Skin 部件挂点通过 `BoneFollowerAutoBinder` 或 `BoneFollower` 识别，避免误删 Actor 外层同名 `body` 视觉根或其它普通子节点。
- AI 调度现在在到达 output/input x 范围后主动调用 Level64 主控取砖/放砖 API，避免只移动不计数。
- AI 放砖与玩家放砖共用 `TryDropInputForActor()`，保证每次成功 drop 只加 1。
- `maxBlockCount` 默认值为 10000，`GetLeftBlockCount()` clamp 到 0，正常文本显示剩余数量。
- 完成时先执行 `OpenDoor()`，再启动非阻塞完成反馈；反馈 1 秒内把显示剩余数归 0，然后隐藏 `blockCountTxt` 和 `TaskTips`，显示 `SuccessTips`。
- 完成反馈有 `_isCompletionFeedbackRunning` 与 `IsFinished` 双重保护，避免重复完成回调重启反馈。
- `TaskTips` / `SuccessTips` 字段为空时，运行时按子节点名称查找兜底；缺失只记录 warning，不阻塞完成。
- 对象池最大容量不再按 10000 配置，而是使用 `Level64BlockCtrl.GetRecommendedPoolCapacity()` 按可见 output/input 缓存和少量额外余量计算。
- AI 退出和 `OnDestroy()` 会调用 `Level64Ctrl.CleanupActorCarry()`，回收 carried brick 并停止对应 tween。

## 验证清单

- [ ] 10 次 fresh entry：生成 helper 后不显示玩家当前 back/face/body Skin 部件；第 1 个 helper 显示 `box_rich`，第 2 个 helper 显示 `box_bad`。
- [ ] 玩家在 ShopPanel 切换 Skin 后再进入 Level64，点击 start-door 生成的 helper 仍不继承玩家 Skin。
- [ ] 10 次第三次点击：两个 helper 存活时不生成第三个 helper，不重复播放生成副作用。
- [ ] 两个 helper 各完成至少 10 次 pickup/drop 循环。
- [ ] 每次 AI drop 后剩余数减少 1，玩家手动 drop 后剩余数也减少 1。
- [ ] 无 helper 情况下，反复手动 output/input 能持续推进计数，无可见砖块池耗尽软锁。
- [ ] 10000 正常完成路径：门立即打开，完成反馈在 1 秒内显示 0，然后隐藏 count/task tips，显示 success tips。
- [ ] rotate-to-max 直接完成路径：未到 10000 时仍能开门，并进入相同最终 UI 状态。
- [ ] 重启、失败、胜利、退出、销毁时，无 helper 残留、carried brick 残留、活动 tween 残留、tips 卡住。
- [ ] Android 横屏 smoke：start-door 点击、AI 搬砖、完成反馈无明显卡顿。
- [ ] iOS 横屏 smoke：start-door 点击、AI 搬砖、完成反馈无明显卡顿。

## 静态验证证据

- `Level_064.prefab` 静态检查：`maxBlockCount: 10000`、`maxAICount: 2`、`blockCountTxt` 绑定存在。
- `TaskTips` / `SuccessTips` prefab instance 名称存在；代码提供运行时名称兜底查找。
- 代码静态检查确认 Level64 v2.0.0 修改只涉及 Level64-local 脚本和文档，没有修改 save/config/analytics/RemoteConfig/YooAsset/ProjectSettings/platform SDK 路径。
- `rg "AssignSpawnOrderSkin|FirstHelperSkinName|SecondHelperSkinName|box_rich|box_bad" Assets/Game/Scripts/Level/LevelItem/Level_64/Level64AICtrl.cs` 确认 helper 生成顺序皮肤赋值路径存在。
- `Actor.prefab` 静态证据显示存在 `ActorSkinBinding`，且 `back`、`face`、`body` 挂点带 `BoneFollowerAutoBinder` / `BoneFollower`；本次代码复用这些组件作为清理识别边界。
- `dotnet build .\Assembly-CSharp.csproj --no-restore` 已执行通过：0 errors / 1114 warnings。warnings 为当前项目既有编译输出，未阻断本次 Level64 改动。

## 未验证风险

- 未运行 Unity Editor PlayMode，AI Skin 部件实际清理效果、`box_rich` / `box_bad` 生成顺序显示、AI 到达 output/input 的 x 距离阈值、trigger 表现、tips 层级与可见状态仍需运行时确认。
- 未运行 Android/iOS 设备 smoke，移动端帧率、触控、tips 布局仍需设备确认。
- `TaskTips` / `SuccessTips` 通过名称兜底查找可以降低漏接线风险，但最终仍建议在 Unity Inspector 中把字段显式绑定。

## 回滚路径

- 回滚 `Level64AICtrl.cs` 中 `RemoveGeneratedAIInheritedSkins()` 调用与相关 helper SkinPart 清理方法；`box_rich` / `box_bad` 生成顺序赋值路径属于 v2.0.0 保留行为，不应随本次 SkinPart 清理回滚一并移除。
- 回滚 `Level64Ctrl.cs` 中 10000 默认目标、完成反馈、tips 查找/切换、AI 区域判定、对象池容量配置和完成日志改动。
- 回滚 `Level64BlockCtrl.cs` 中 `GetRecommendedPoolCapacity()`，并恢复 `Level64Ctrl.InitializeBlockCtrl()` 原有池容量配置。
- 回滚本文件、`Level64功能说明.md` 和 `DevelopMD/已有功能设计与使用记录.md` 中 v2.1.0 的补充记录。
