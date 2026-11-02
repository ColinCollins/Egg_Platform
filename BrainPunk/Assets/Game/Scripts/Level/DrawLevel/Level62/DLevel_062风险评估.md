# DLevel_062 风险评估

## 1. 需求与原行为基线

- 目标：保护两个角色，蜜蜂随机攻击其中一个；猫夹正常入场和首笔释放；两个角色被蛰时播放 `swell`。
- 基线：D053 为单角色、单蜂巢蜂群；D058 的 `DLevel58ActorReleaseCtrl` 负责猫夹入场和首笔释放。既有 Draw 倒计时、失败等待和 Reset 链不改变。
- 本次实际变化：共享蜂群控制器支持 `additionalActors`，并在 D062 prefab 绑定第二角色；根节点增加第二个 D058 释放 Handler。

## 2. 受影响对象与控制

| 风险 | 等级 | 控制措施 | 当前证据 | 待补验证 |
| --- | --- | --- | --- | --- |
| 两套猫夹 Intro 串行，入场节奏与预期不同 | 中 | 保持现有 `DrawLevelCtrl` 顺序扫描，明确记录，不改共享时序 | prefab 有两个 `DLevel58ActorReleaseCtrl`；`DrawLevelCtrl` 逐个 await | Unity PlayMode 观察是否需要并行 |
| Bee 命中第二角色未被识别 | 高 | 目标列表只接收有效 Actor；Collider 从根、父级和附着刚体解析 | `additionalActors` 与第二 Actor stripped 引用有效 | 两角色各自被命中实测 |
| 两角色 `swell` 资源不一致 | 高 | 两套 Actor 均覆盖含 `swell` 的 SkeletonData；播放前 `FindAnimation` | prefab 有 2 条 SkeletonData Override | 不同 Skin/导入后 Spine 轨道 |
| 首击后重复失败或重复动画 | 高 | Bee/Actor 两组 HashSet 去重；失败演出期间不重复提交 Draw 失败 | 代码静态审查通过 | 多 Bee 同帧 CollisionStay |
| 失败演出与成功倒计时竞争 | 中 | 复用 Draw failure performance 占用和倒计时冻结 | 共享 API 调用链存在 | 实际 1.5 秒边界 |
| 两套释放角色物理初态错误 | 中 | 复用 D002 的 Kinematic 持有、首笔 Dynamic 释放 | 两个刚体引用有效 | PlayMode 画线、落地、重开 |
| 移动端实例化/物理峰值 | 低 | 沿用 D053 8 蜂批次和现有参数，不新增持续分配循环 | 静态结构与编译通过 | Android/iOS 8 蜂压力测试 |

## 3. 数据与回滚边界

- 不新增 DB、存档、全局事件、Analytics、广告、SDK 或平台配置。
- Luban 只同步已有 `20062` 行及 Draw 排序；若需撤销，移除对应 Excel 行后重新生成两个 Draw JSON。
- 代码回滚顺序：先移除 D062 第二角色/第二释放组件和 `additionalActors`，再视 D065 是否仍在使用决定是否回滚共享 `DLevel53Ctrl` 多目标逻辑。

## 4. 验证证据与未决项

- 已完成：prefab 131 定义、0 重复 anchor、0 悬空引用；脚本 GUID 与资源引用静态解析；Luban 生成成功；Roslyn 定向编译退出码 0；`swell` SkeletonData 资源存在。
- `uncertain`：Unity Editor 导入后的实际目标随机性、两套猫夹的先后/同步观感、角色命中点特效、重开和 Android/iOS 横屏。
- 验收重点：分别让 Bee 命中角色 A/B；确认命中角色位置播放 `swell`，另一角色仍可运动；失败等待结束后只重开一次；成功倒计时不会在失败演出期间完成。
