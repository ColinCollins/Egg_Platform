# DLevel_030 平台稳定功能与风险评估

## 1. 需求与原行为基线

- 目标：平台中心不下坠、不偏移、不发生平移抖动；保留绕中心旋转及碰撞反馈；左侧长段更重。
- 修改前磁盘证据：DLevel_030 中 `platform_Stone` 为 Dynamic Rigidbody2D，`Use Auto Mass=true`、
  `Gravity Scale=1`、`Constraints=None`，且当前 prefab 已无负责固定它的 HingeJoint2D。
- 修改前直接结果：该刚体受到完整重力且没有位置约束，因此会整体下坠；这不是只绕中心旋转的结构。
- 历史截图中曾同时存在 HingeJoint2D 与 Freeze Position X/Y。两套位置约束共同参与求解时可能产生竞争，
  但历史抖动的逐帧位姿和 Physics2D 求解日志未留存，因此具体抖动量与唯一原因标记为 `uncertain`。

## 2. 当前实现与直接结论

- 新增关卡局部 `DLevel30Ctrl`，挂在 DLevel_030 根节点。
- 平台保持 Dynamic，原生重力关闭，Freeze Position X/Y，Freeze Rotation 关闭。
- 平台本地质心设为 `(0,0)`；左侧偏重不再依赖 Collider 自动质量中心，而由
  `longSideCenterOfMassOffset=(-1.25,0)` 生成明确、可调的等效重力力矩。
- 公式只调用 `AddTorque`，不会对平台施加平移力；因此中心固定和物理旋转可以同时成立。
- 平台仍参与 Dynamic Rigidbody2D 碰撞，Actor 和 Draw Line 的冲量仍会改变角速度。
- 结算后 Freeze All，Reset 恢复初始位姿和仅冻结平移的约束。

## 3. 影响文件与系统

- `Assets/Game/Scripts/Level/DrawLevel/Level30/DLevel30Ctrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_030.prefab`
- 本关结构、风险文档，以及 Draw 中央演进记录和已有功能记录。
- 未修改共享 Draw 输入/胜负、Actor、UI、配置、存档、资源加载、analytics、SDK 或 Physics2D 全局设置。

## 4. 风险项

| 风险 | 等级 | 当前控制 | 剩余缺口 |
| --- | --- | --- | --- |
| 平台中心继续发生平移 | 低 | Freeze Position X/Y；原生重力为 0；无 HingeJoint2D 位置求解 | PlayMode 逐帧 Transform 采样待验证 |
| 左侧偏重方向或强度不符合手感 | 中 | 偏移和重力倍率均在 Inspector 可调；负 X 明确代表左侧 | 需结合 Actor、线质量现场调参 |
| Draw Line 质量 100 导致过大角冲量 | 中 | 平台质量 10、Angular Damping 1.5、Continuous | 极端长线/多笔碰撞待验证 |
| 结算或 Reset 时穿插接触导致弹跳 | 中 | 结算 Freeze All；Reset 临时关闭模拟并清零速度、SyncTransforms | 连续快速 Reset 待验证 |
| 低帧率移动端物理手感差异 | 中 | FixedUpdate、无分配、Interpolate、Continuous | Android/iOS 真机待验证 |
| 重新添加 Hinge 造成重复位置约束 | 中 | 当前 prefab 无平台 Hinge；文档明确禁止组合 | 后续人工 prefab 改动需复核 |

## 5. 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal`：退出码 0，0 error，
  1119 个工程既有 warning。
- 静态 prefab 检查：根节点包含 `DLevel30Ctrl`；`platformRigidbody` 与 `drawLevelCtrl` 为非零本地引用；
  平台 Rigidbody2D 为 Mass 10、Gravity Scale 0、Constraints 3（Freeze Position X/Y）。
- 脚本固定步无 LINQ、协程、集合扩容或 Instantiate，不产生每帧托管分配。

## 6. 假设与缺失证据

- 假设：`platform_Stone` 根节点原点就是美术齿轮中心。当前 prefab Transform/画面支持该判断，但未做
  PlayMode Gizmo 与逐帧世界坐标采样，仍需现场确认。
- `uncertain`：左侧偏移 `-1.25` 是否达到目标“更重”的最终手感；这是首版安全调参值，不是验收值。
- `uncertain`：Actor、短线、最长线、多笔线同时碰撞时的最大角速度与可解性。
- `uncertain`：暂停、失败重置、胜利 Replay 和 Android/iOS 真机表现。

## 7. 回滚路径

1. 从 DLevel_030 根节点移除 `DLevel30Ctrl`。
2. 删除 Level30 局部脚本，恢复 prefab 中平台 Rigidbody2D 的旧质量、重力和约束。
3. 若回滚为 Hinge 方案，必须只让 Hinge 负责位置约束，不能同时启用 Freeze Position X/Y；锚点必须以
   平台根节点和静态齿轮各自局部坐标重新校验。
4. 本次未修改共享系统、配置或存档，因此不需要数据迁移。
