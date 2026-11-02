# DLevel_042 无刺双球功能风险评估

## 1. 影响范围

- Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_042.prefab`。
- 共享行为：复用 `DrawTwoBallReleaseCtrl` 与 `DrawTwoBallReunionSuccessHandle`，本次不修改两者源码。
- 配置：核对 `LevelData.xlsx`、`LevelSort.xlsx`、两份 Draw JSON 与 Guru 默认 Draw 排序。
- 文档：Level42 本地记录、Draw 中央演进记录和 `DevelopMD/已有功能设计与使用记录.md`。

## 2. 风险与控制

| 风险 | 等级 | 当前控制 |
| --- | --- | --- |
| 无尖刺关卡保留 D032 失败组件 | 高 | 删除尖刺控制器、Knock nested Prefab 及其全部本地引用 |
| 手工删除 nested Prefab 后出现 Missing Reference | 高 | 回读全部 YAML anchor 与非零本地 fileID，结果为 0 缺失 |
| D042 画线重量偏离同玩法基线 | 中 | `strokeMass` 从 20 对齐为用户指定 D032 的 100 |
| 首笔前球提前下落 | 高 | 两球初态保持 Kinematic、mass 10，首笔回调才切 Dynamic |
| 右球镜像缩放干扰动态碰撞 | 高 | 共享释放控制器继续归一刚体根缩放并转移 payload 镜像 |
| 双球接触不触发成功 | 高 | 右球成功组件与 `otherBall`、镜头和 Bubble 引用均静态闭合 |
| 对齐 D032 时覆盖 D042 自有布局 | 高 | 只对齐行为参数，保留全部 D042 Transform 与美术布局 |
| Excel、JSON 与默认远端顺序不一致 | 高 | 三处均为相同 31 项顺序；D042 为第 30 项，D043 为末项 |
| 线上远端配置覆盖本地默认值 | 中 | 当前 `enabled=false`；真实后台状态未验证，标记 `uncertain` |
| Reset/Replay 或移动端物理手感异常 | 中 | 静态状态链完整，仍要求 PlayMode 与真机验证 |

## 3. 验证证据

- Prefab 为 259 个唯一 anchor、259 个唯一非零本地引用且无缺失；残留脚本、Knock GUID、名称和旧
  fileID 均不存在。
- 两球的 Kinematic/mass 10、共享释放绑定、右球成功组件和 `otherBall` 引用均已回读。
- 排除 Transform 和 D032 专属危险物后，D042 与 D032 的行为参数差异为 0。
- D042 配置加入时隔离 Luban 生成成功且哈希一致；随后新增的 D043 已进入当前 31 项 JSON，Guru 默认
  排序同步为同一完整顺序。
- Unity 已成功导入 D042 Prefab；并行加入 D043 后的最终 31 项 Guru YAML 尚未由当前 Editor 自动刷新。
- Unity 6000.0.59f2 当前 Roslyn 响应文件独立编译最终 `GuruAllSpecs.G.cs`，退出码 0且无诊断。
- 完整 PlayMode、低帧率碰撞、连续 Reset/Replay、暂停恢复和 Android/iOS 横屏尚未验证，结论为
  `uncertain`。

## 4. 回滚路径

- 尖刺与 Knock 清理无需数据迁移；无刺关卡不应恢复这些复制残留。
- 若仅回滚画线重量，只需恢复 D042 Prefab 的 `strokeMass`，不修改共享 DrawLineFeature。
- 若撤销关卡配置，应同时处理 Excel、Luban JSON 与 Guru 默认排序，避免本地和默认远端顺序分叉。

## 5. 2026-08-05 爱心尺寸与发射率风险补充

- D042 与 D006/D032/D033/D043 统一配置 `heartSizeMultiplier=1.5`、`heartEmissionRate=4`；旧基线为
  Bubble `startSize=0.2~0.4`、无额外倍率和 `5/s`。共享脚本默认倍率保留 `1`，只对五关 Prefab
  显式启用放大。
- 运行时仅缩放克隆 ParticleSystem 的 `startSize`，不修改 Bubble 源 Prefab或正式贴图。放大后的遮挡、
  降密后的节奏以及移动端过绘需在 D042 自有布局中单独目测，不能由 D032 的结果代替。
- Unity 6000.0.59f2 自带 Roslyn 完整编译为 `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断；
  共享脚本消费者恰为五关且每关成功组件、rate `4`、size `1.5` 各 1 个。D042 Prefab 为
  `Definitions/LocalReferences/duplicate/missing=252/543/0/0`。Unity Editor.log 已记录五个目标 PrefabImporter 成功导入并完成脚本重编译；
  PlayMode、低帧率、Pause/Reset/Replay 与 Android/iOS 横屏仍为 `uncertain`。
- 只回滚本次视觉调整时恢复倍率 `1`、发射率 `5`，必要时成组移除新字段及五个 Prefab YAML；
  尖刺清理、画线重量、关卡配置、正式贴图和两阶段成功逻辑均保持不变。
