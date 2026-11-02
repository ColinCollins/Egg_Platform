# DLevel_043 脚本结构记录

## 1. 需求与基线

- 用户指定 `20043 / DLevel_043` 与 D006、D032、D033、D042 一并调整双球相遇后的小心心表现：
  心形应更大，持续出现的数量略少。
- 调整前 D043 Prefab 已挂载共享 `DrawTwoBallReleaseCtrl` 与 `DrawTwoBallReunionSuccessHandle`：第一笔
  有效画线后释放两颗球，配对球首次碰撞后进入冻结、爱心、相机聚焦与两阶段成功流程。
- 原爱心尺寸完全沿用 `Bubble.prefab` 的 `startSize=0.2~0.4`；D043 显式配置
  `heartEmissionRate=5`，共享组件同时保留一颗即时 `Emit(1)`。
- 本关此前没有 DrawLevel/Level43 本地中文结构记录或风险文档，本文件补齐该文档所有权，不替代
  D043 Prefab 作为完整场景布局来源。

## 2. 本次相关结构

```text
DLevel_043
|-- DrawLevelCtrl
|-- DrawLineFeature
|-- DrawTwoBallReleaseCtrl
`-- level6
    |-- yellow_ball                 # 配对球之一
    `-- yellow_ball                 # 挂载 DrawTwoBallReunionSuccessHandle
        `-- DrawTwoBallReunionSuccessHandle
            |-- otherBall           # 显式绑定另一颗球
            |-- heartBubblePrefab   # Bubble.prefab
            |-- heartSizeMultiplier # 本关 1.5
            `-- heartEmissionRate   # 本关 4/s
```

其余场景 Transform、美术、Collider 与物理障碍继续由 `DLevel_043.prefab` 自己管理；本次不按其它双球关
覆盖布局。

## 3. 爱心表现现行规则

- `DrawTwoBallReunionSuccessHandle` 新增序列化 `heartSizeMultiplier`，脚本默认值为 `1`；D043 Prefab
  显式配置 `1.5`。运行时只等比缩放克隆 ParticleSystem 的 `main.startSize`，因此 Bubble 原随机尺寸
  `0.2~0.4` 等效为约 `0.3~0.6`。
- `heartEmissionRate` 从 `5` 降为 `4`。碰撞时的一颗即时 `Emit(1)`、`1.8s` 成功演出、粒子生命周期、
  接触点上方偏移、Sorting Order `340`、相机聚焦与演出结束 Clear 均保持不变。
- 主贴图继续优先加载 `Assets/Game/Resources/Sp_hx_ax_01.png`，加载失败才使用程序生成心形；正式贴图
  与程序回退都会经过相同尺寸倍率。

## 4. 影响文件、资源与系统边界

- 行为脚本：`Assets/Game/Scripts/Level/DrawLevel/DrawTwoBallReunionSuccessHandle.cs`。
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_043.prefab`。
- 只读复用资源：`Assets/Game/ArtSrc/Particles/Prefabs/Bubble.prefab`、
  `Assets/Game/Resources/Sp_hx_ax_01.png`；两者本次均不修改。
- 相同参数同步用于 D006、D032、D033、D042；目前这五关是共享成功组件的全部 Prefab 消费者。
- 不修改 DB/存档、Luban、Guru/RemoteConfig、UI、全局事件、analytics、广告/IAP 或平台 SDK。

## 5. 验证与未验证项

- Unity 6000.0.59f2 自带 Roslyn 使用当前 `Assembly-CSharp.rsp/.rsp2` 完整编译通过：
  `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断。共享脚本消费者恰为五关；D043 静态检查为
  `Definitions/LocalReferences/duplicate/missing=244/530/0/0`，成功组件、rate `4`、size `1.5` 各 1 个。
- Unity Editor.log 已记录五个目标 PrefabImporter 成功导入并完成脚本重编译。
- D043 PlayMode 的首颗即时反馈、实际尺寸与密度、场景遮挡、相机边缘、Pause/Reset/Replay、低帧率，
  以及 Android/iOS 横屏纹理清晰度和过绘均未验证，结论为 `uncertain`。

## 6. 回滚

- 仅回滚本次视觉参数时，把 D043 及其余四个目标关卡的倍率恢复为 `1`、持续发射率恢复为 `5`。
- 若回滚新增字段，应同时移除共享脚本字段和五个 Prefab 的对应 YAML，避免部分关卡残留配置。
- 不删除正式贴图、不修改 Bubble Prefab，也不回滚双球释放、冻结、相机与两阶段成功流程；无需数据迁移。
