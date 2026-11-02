# DLevel_064 风险评估

## 1. 需求与原行为基线

- 目标：检查复制得到的 D064，确认没有新机制或残留配置。
- 基线：D053 单角色/单蜂巢蜂群关；共享 Ctrl 现在支持可选第二角色，但 D064 未填写 `additionalActors`。
- 结论：静态证据显示 D064 保持单角色、单 Hive、单倒计时结构，无需增加本关脚本。

## 2. 风险与控制

| 风险 | 等级 | 控制措施 | 当前证据 | 待补验证 |
| --- | --- | --- | --- | --- |
| 复制关卡残留多角色/双蜂巢组件 | 中 | 检查根 Ctrl 字段、Hive 数量和脚本 GUID | 单 Actor、单 Hive、无 `additionalActors` | Unity Inspector/PlayMode |
| 共享多目标逻辑改变单角色行为 | 中 | 空数组回退到首个 `actor`，命中解析仍只接受登记对象 | Roslyn 与 prefab 字段检查 | 单角色命中实测 |
| `swell` 骨骼或材质导入异常 | 中 | prefab 有一条 SkeletonData Override，资源 GUID 可解析 | YAML 静态检查 | Spine 实际播放 |
| 倒计时/失败链未绑定 | 高 | 保留 `DrawLevelCountdownCtrl` 和 D053 Ctrl 引用 | YAML 引用有效 | 进入、失败、重开、通关 |

## 3. 数据、资源与回滚

- 不新增数据、存档、全局事件或平台链路；Luban 仅保留已有 20064 映射和第 56 项排序。
- 回滚本关只涉及 `DLevel_064.prefab` 与 20064 配置映射；共享蜂群脚本属于 D062/D065 共用，不在本关回滚范围内。

## 4. 验证证据与未决项

- 已完成：D064 130 个 YAML 定义、0 重复 anchor、0 悬空引用；单 Ctrl、Hive、倒计时、Actor 和资源 GUID 静态有效；Luban 生成成功；Roslyn 编译退出码 0。
- `uncertain`：Unity PlayMode 的进入/画线/蜂群碰撞/`swell`/重开/通关，以及 Android/iOS 横屏。
- 验收重点：确认复制关卡没有隐藏 Handler；第一笔只启动一组蜂群；成功和失败后无残蜂、残粒子或倒计时竞争。
