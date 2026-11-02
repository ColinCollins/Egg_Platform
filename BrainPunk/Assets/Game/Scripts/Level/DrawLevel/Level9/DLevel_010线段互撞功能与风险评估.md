# DLevel_010 线段互撞功能与风险评估

## 1. 需求与背景

- DLevel_010 允许多笔画线，用户要求不同笔画之间能够碰撞。
- 同一笔可能出现乱画、自交和重复覆盖，要求不能因内部碰撞或大量重叠接触而失控。
- 原 EdgeCollider2D 没有正面积，不能满足稳定的边界与边界互撞。

## 2. 影响文件与系统

- 共享代码：`Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`
- 本关 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_010.prefab`
- 本关结构记录：`DLevel_010脚本结构记录.md`
- 物理系统：Unity Rigidbody2D、CapsuleCollider2D、CompositeCollider2D
- 不涉及 DB、存档、Luban、UI、资源地址、事件、analytics、广告或 SDK。

## 3. 原行为与修改后行为

- 原行为：每笔完成后创建 EdgeCollider2D；不同笔都只有零面积边界，不能可靠互撞。
- 修改后：本关开启 `enableStrokeToStrokeCollision`。共享实现先简化采样点，再生成最多
  32 个胶囊段，通过 Polygon Composite Merge 为单笔生成一个有面积的复合形状。
- 同一笔的重叠胶囊合并且共用一个 Rigidbody2D，不产生自碰撞；不同笔拥有独立刚体和
  CompositeCollider2D，可以互相碰撞。
- 玩家可见 LineRenderer、线段中心归位、墨水长度、质量、重力和本关阻尼 override 保持
  原链路；本次未改用户当前调节的 Linear/Angular Damping。

## 4. 性能与稳定性控制

- RDP 简化容差取采样间距与碰撞半径较大者的一半。
- 单笔最多生成 32 个 CapsuleCollider2D，防止 20 单位墨水按 0.05 间隔形成约 400 段。
- Composite 使用 Manual 生成，只在线段完成时调用一次，不在运行时循环持续刷新。
- 极复杂乱画达到上限后，碰撞轮廓会按顺序降采样，视觉笔迹不变但物理轮廓是近似值。

## 5. 验证与剩余风险

- 静态检查：DLevel_010 开关为 true；旧关卡默认 false；Composite 类型为 Polygons，
  Capsule 操作为 Merge，单笔段数上限为 32。
- C# 单进程构建第一次在 124 秒超时且未返回诊断；终止本次残留构建进程后重试通过，
  结果为 `0 error / 314 warning`。Unity Editor 日志同时记录 DrawLineFeature 热重载完成，
  但热重载不替代 PlayMode。
- 代码与中文文档 `git diff --check` 通过。完整检查 DLevel_010 prefab 时命中该新增资源
  原有空 YAML 值的尾随空格；本次新增开关行无尾随空格，未批量清理用户资源。
- 静态确认只有 DLevel_010 显式开启新字段；prefab YAML 共 173 个锚点且无重复；新增文档
  meta GUID 在 Assets 内唯一。
- Unity PlayMode 未验证：两笔互撞、同笔自交、重复涂抹、极短笔画、外部物体接触、
  失败/Replay、暂停恢复和完整通关。
- Android/iOS 横屏未验证；低端设备多笔 Composite 生成峰值和持续接触成本为剩余风险。
- Dynamic 对 Dynamic 的高速碰撞仍可能穿透，需要按实际速度验证，不以增加段数替代测试。

## 6. 回滚

- 在 DLevel_010 的 DrawLineFeature 关闭 `enableStrokeToStrokeCollision`，即可恢复原
  EdgeCollider2D 行为。
- 若全量回滚，删除共享 Composite 构建、简化方法和字段；不涉及存档或配置迁移。

## 7. 2026-07-30 配置变更说明

- 用户后续要求把 `DLevel_003` 截图中的画线配置同步到全部 `DLevel_001～010`。
- `DLevel_010` 当前已改为 `allowMultipleStrokes=false`、
  `enableStrokeToStrokeCollision=false`，因此本文件第 1～6 节记录的是此前启用状态与实现历史；
  共享 Composite 能力仍保留，但当前关卡不再进入该分支。
- 同步后的其它关键值为 `sampleSpacing=0.3`、`maxInkLength=20`、`strokeMass=200`、
  `scaleMassByLength=true`、`minimumMassRatio=0.25`，质量曲线仍为原线性曲线。
- 静态配置校验通过；Unity PlayMode 中第一笔后关闭输入、旧多笔解法失效、球体交互和完整通关
  尚未验证。这是当前最大的关卡回归风险。
- 若要恢复本文件描述的多笔互撞行为，只需把本关 `allowMultipleStrokes` 与
  `enableStrokeToStrokeCollision` 同时恢复为 `true`；共享代码无需回滚。
