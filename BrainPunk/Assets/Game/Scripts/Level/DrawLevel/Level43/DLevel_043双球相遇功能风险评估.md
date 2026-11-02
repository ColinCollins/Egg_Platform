# DLevel_043 双球相遇功能风险评估

## 1. 修改范围与基线

- 需求只调整 `20043 / DLevel_043` 及另外四个指定双球关的爱心尺寸和持续发射密度。
- 共享脚本新增默认值为 `1` 的 `heartSizeMultiplier`；D043 与 D006/D032/D033/D042 五个 Prefab
  显式设置 `heartSizeMultiplier=1.5`，并把 `heartEmissionRate` 从 `5` 降为 `4`。
- 运行时只缩放新克隆 ParticleSystem 的 `startSize`。`Bubble.prefab`、正式心形贴图、粒子生命周期、
  首颗即时发射、双球物理和成功结算均不修改。

## 2. 风险与控制

| 风险 | 等级 | 当前控制与待验证项 |
| --- | --- | --- |
| 只改脚本默认值导致已有 Prefab 仍使用旧参数 | 高 | 五个现有 Prefab 必须全部显式保存 `1.5 / 4` |
| 放大后遮挡球、画线或场景关键物 | 中 | 保留原发射点与 Sorting Order；D043 需独立 PlayMode 目测 |
| 发射率降低后反馈过疏 | 中 | 保留碰撞帧即时 `Emit(1)`；`4/s` 的实际节奏待目测 |
| 放大粒子增加移动端过绘 | 中 | 同步降低持续发射率，不增加生命周期、系统或材质实例数量；真机仍为 `uncertain` |
| 修改 Bubble 源资产影响其它特效 | 高 | 只改运行时克隆实例，Bubble Prefab 与正式贴图保持不变 |
| Pause/Reset 后残留大粒子 | 中 | 沿用现有 Pause/Play、Stop/Clear/Destroy；连续 Replay 待动态验证 |
| D043 与其它四关参数漂移 | 高 | 静态核对共享脚本 GUID 的全部 Prefab 消费者及两项序列化值 |

## 3. 系统与资源边界

- 受影响文件为共享 `DrawTwoBallReunionSuccessHandle.cs` 和五个目标 DLevel Prefab；D043 本地只新增
  中文记录与 Unity `.meta`。
- `Assets/Game/ArtSrc/Particles/Prefabs/Bubble.prefab` 和
  `Assets/Game/Resources/Sp_hx_ax_01.png` 只读复用，不进入本次差异。
- 不改变 Draw 成功门禁、相机时序、输入、UI、DB/存档、Luban、Guru/RemoteConfig、全局事件、
  analytics、广告/IAP 或平台分支。

## 4. 验证证据与缺口

- Unity 6000.0.59f2 自带 Roslyn 完整编译为 `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断；
  共享组件消费者恰为五关且每关成功组件、rate `4`、size `1.5` 各 1 个。D043 Prefab 检查为
  `Definitions/LocalReferences/duplicate/missing=244/530/0/0`。Unity Editor.log 已记录五个目标 PrefabImporter
  成功导入并完成脚本重编译；
- D043 PlayMode、暂停/重置/重玩、低帧率碰撞、
  不同横屏宽高比，以及 Android/iOS 清晰度与性能均为 `uncertain`。

## 5. 回滚路径

- 把五关 `heartSizeMultiplier` 恢复为 `1`、`heartEmissionRate` 恢复为 `5`；若删除字段，五个 Prefab
  的序列化项必须成组清理。
- 保留正式贴图及缺资源回退、Bubble 源 Prefab、首颗即时发射、双球冻结、镜头聚焦和两阶段成功。
- 本次无配置或数据结构迁移，回滚不需要处理 DB、Luban、Guru、存档或平台数据。
