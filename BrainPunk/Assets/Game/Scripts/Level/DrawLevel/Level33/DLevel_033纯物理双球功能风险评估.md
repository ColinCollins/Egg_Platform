# DLevel_033 纯物理双球功能风险评估

## 1. 影响范围

- Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_033.prefab`。
- 共享脚本：`DrawTwoBallReleaseCtrl.cs`，由 D032/D033 共同引用，GUID 保持不变。
- 配置源：`LevelData.xlsx`、`LevelSort.xlsx` 中用户已有 D033 行；本轮只运行 Luban 同步生成 JSON。
- 文档：Level32/Level33 本地记录、Draw 中央演进记录和 `DevelopMD/已有功能设计与使用记录.md`。

## 2. 风险与控制

| 风险 | 等级 | 当前控制 |
| --- | --- | --- |
| 复制 D032 后无尖刺却保留失败组件 | 高 | 删除尖刺控制器、Knock nested Prefab 和全部对应本地引用 |
| D033 挂载关卡名为 DLevel32 的脚本 | 中 | 组件以原 GUID 移到公共目录并更名为 `DrawTwoBallReleaseCtrl` |
| 脚本更名造成 D032/D033 Missing Script | 高 | `.meta` GUID `5baf...9e32` 原样迁移，两个 Prefab PPtr 不变 |
| 首笔前球提前下落 | 高 | 两球 Prefab 初态保持 Kinematic，首笔回调才切 Dynamic |
| 右球镜像缩放干扰动态碰撞 | 高 | 共享控制器释放时将负缩放转移到 payload，并恢复 Collider offset |
| 五个圆环不参与物理 | 高 | 五个圆环均为 Dynamic、mass 1，保留现有 Collider 与布局 |
| 圆环阻挡后出现脚本保送 | 高 | 成功仍只来自双球真实碰撞，不新增距离判定、吸附或 AddForce |
| Excel 已有行但 JSON 未同步 | 高 | Luban 正式生成成功，运行时 JSON 已含 `20033` 与排序 29 |
| 排序备注包含“尖刺”与玩法不符 | 低 | 已按“无尖刺”明确需求把 D033 备注改为“恋爱球球3” |
| 成功/Reset/暂停竞态 | 中 | 复用既有两阶段成功门禁和 Attempt Reset；完整 PlayMode 待验证 |

## 3. 验证与未验证项

- Prefab YAML 锚点唯一且本地引用闭合；尖刺控制脚本 GUID、Knock Prefab GUID、Knock 名称和
  `OnTrigger2DHandle` 均不存在。
- 两球的 Kinematic/mass 10、共享释放引用、右球成功组件与 `otherBall` 绑定均已静态回读。
- 五个圆环的 Dynamic/mass 1 已逐项计数；没有新增 Joint2D、脚本力、角度限制或成功兜底。
- Luban 校验和生成完成，退出码 0。工作簿修改前后均已检查，D033 只改备注且原格式保持不变。
- Unity 自带 Roslyn 使用当前 `Assembly-CSharp.rsp/rsp2` 完整编译通过（退出码 0）；Unity Editor 日志已
  记录 D032/D033 的 PrefabImporter 成功导入。
- PlayMode、低帧率碰撞、连续 Replay、暂停恢复和 Android/iOS 横屏仍需最终动态验证，未验证结论为
  `uncertain`。

## 4. 回滚路径

- 共享脚本更名可通过把文件/类名改回 DLevel32Ctrl 并保留 GUID 回滚，但 D033 会重新出现错误命名。
- D033 关卡回滚应成套移除 Prefab、Excel 两行并重跑 Luban；不要只删除生成 JSON。
- 尖刺/Knock 清理无需数据迁移，若未来 D033 新增危险物，应按新需求显式添加本关控制器，而不是恢复
  复制残留。

## 5. 2026-08-05 爱心尺寸与密度风险补充

- D033 与 D006/D032/D042/D043 的共享成功组件统一增加 `heartSizeMultiplier`；脚本默认 `1`，五关
  Prefab 均显式设为 `1.5`，`heartEmissionRate` 从 `5` 降为 `4`。只缩放运行时克隆粒子的
  `startSize`，不修改 Bubble/贴图资产。
- 主要风险是放大后被五个动态圆环或双球遮挡、降低发射率后节奏过疏，以及较大像素覆盖带来的移动端
  过绘。首颗即时 `Emit(1)`、Sorting Order 和生命周期不变；这些控制不能替代 PlayMode/真机目测。
- Unity 6000.0.59f2 自带 Roslyn 完整编译为 `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断；
  共享脚本消费者恰为五关且每关成功组件、rate `4`、size `1.5` 各 1 个。D033 Prefab 为
  `Definitions/LocalReferences/duplicate/missing=228/498/0/0`。Unity Editor.log 已记录五个目标 PrefabImporter
  成功导入并完成脚本重编译；D033 低帧率物理、爱心层级、Pause/Reset/Replay 和 Android/iOS 横屏仍为 `uncertain`。
- 回滚仅恢复五关倍率 `1` 和发射率 `5`；若移除新字段，应同步删除五个 Prefab 的对应序列化项。
  圆环、正式贴图、双球成功门禁、Excel、JSON 与 Guru 均不参与本次回滚。
