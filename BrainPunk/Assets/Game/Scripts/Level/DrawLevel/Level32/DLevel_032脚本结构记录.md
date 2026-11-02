# DLevel_032 脚本结构记录

## 1. 需求与基线

- 本关只保留两颗原地球，不需要猫、夹子或装置入场动画。
- 两颗球初态为 `Kinematic`、`mass=10`；玩家完成第一笔有效画线后，两球同步进入
  `Dynamic`，之后由真实 Physics2D 求解。
- 两球接触成功规则继续复用 `DrawTwoBallReunionSuccessHandle`，正式爱心贴图为
  `Resources/Sp_hx_ax_01.png`。
- 任一球内 Actor 碰到下方尖刺时，命中表现必须与 DLevel003 一致，然后执行死亡、击退、
  `trap` 音效和 Draw 专用失败重置。

## 2. 最终结构

```text
DLevel_032
|-- DrawLevelCtrl
|-- DrawLineFeature
|-- DrawTwoBallReleaseCtrl
|-- DLevel32SpikeTrapHandle
|-- DLevel032_Knock_Explosion       # 与 D003 同源并裁剪同一子对象
|-- Scene/Caltrop_long x 10
`-- level6
    |-- yellow_ball                 # 左球，Kinematic，mass=10
    |   `-- payloadContentRoot/Actor
    `-- yellow_ball                 # 右球，Kinematic，mass=10
        |-- DrawTwoBallReunionSuccessHandle
        `-- payloadContentRoot/Actor
```

Prefab 中没有 `Alien`、共享猫夹 Rig 或 `DLevel6Ctrl`。`level6` 名称是现有场景容器名，
不代表仍复用第六关控制逻辑。

## 3. 组件职责

### `DrawTwoBallReleaseCtrl`

- 实现 `IDrawStrokeCompletedHandler`，不实现 Intro 接口，因此不会增加开场等待或画线门禁。
- 第一笔有效画线后清零两球速度并切换为 `Dynamic`；后续笔画不重复释放。
- Awake/Attempt Reset 缓存并恢复两球父级、局部位姿、缩放、碰撞盒偏移和
  `payloadContentRoot` 位姿，然后恢复 `Kinematic`。
- 右球的负缩放在释放时转移到内容根，避免 Dynamic Rigidbody2D 根继续携带镜像缩放。

### `DLevel32SpikeTrapHandle`

- 在 `OnEnable` 收集本关 `TriggerItem` Layer 的 10 个 `OnTrigger2DHandle`，兼容球根碰撞体
  与球内 Actor 子碰撞体；只接受当前 DLevel032 层级内的 `ActorCtrl`。
- 命中后移动并重播现成 `knockEffect`，不再运行时 `Instantiate` 完整粒子 Prefab。
- 粒子实例来自 D003 使用的同一 `Knock_Explosion.prefab`，并同样移除源 localID
  `5393762992232218095`，因此不会带出错误的敲门子表现。
- 命中后调用 `ActorCtrl.Die/Knockback`、播放 `trap`，等待 0.5 秒后调用
  `DrawLevelCtrl.TryFailAndReset`。Reset/Disable 会停止并清空粒子。

### `DrawTwoBallReunionSuccessHandle`

- 右球的 `otherBall` 显式绑定左球；冻结、爱心、镜头聚焦与两阶段成功提交沿用共享逻辑。
- 爱心优先加载 `Assets/Game/Resources/Sp_hx_ax_01.png`，缺资源时才回退程序纹理。

## 4. 序列化与资源

- `DrawTwoBallReleaseCtrl.firstBall`：`4113373739594850306`。
- `DrawTwoBallReleaseCtrl.secondBall`：`5045858249553790803`。
- `DLevel32SpikeTrapHandle.knockEffect`：D032 内的 stripped ParticleSystem，源 fileID
  `462519149007686675` / GUID `972fa0cfdd6f5e04a8c9fde50e1bb924`。
- DB、存档、Luban、RemoteConfig、analytics、UI 和平台 SDK 均无改动。

## 5. 验证与未验证项

- Unity 自带 Roslyn 对完整 `Assembly-CSharp` 聚焦编译通过：`exit 0`，无新增 error。
- Prefab 静态检查：297 个唯一 YAML anchor、0 duplicate、0 missing local reference；
  `DLevel6Ctrl` GUID 为 0、`DrawTwoBallReleaseCtrl` 为 1、球绑定为 2、尖刺 Trigger 为 10。
- D003 与 D032 均引用同一 Knock Prefab，并均移除 source localID
  `5393762992232218095`；D032 无 `knockPrefab` 直接模板字段。
- 当前打开的 Unity Editor 未响应外部 Refresh 快捷键，因此本轮未取得最新 PrefabImporter 日志。
  完整 PlayMode 的首笔释放、尖刺命中位置、失败/成功竞态、Replay 及 Android/iOS 横屏仍为
  `uncertain`。

## 6. 回滚

- 释放逻辑可通过移除 `DrawTwoBallReleaseCtrl` 并恢复两球静态配置回滚，不需要恢复猫或夹子。
- 命中特效可恢复为原模板字段，但会重新带回完整 Prefab 的错误子表现；推荐回滚时仍保留
  D003 的裁剪实例结构。
- 本关回滚不涉及共享 Actor、D006、爱心资源、DB、配置或平台数据迁移。

## 7. 2026-08-05 爱心粒子尺寸与密度调整

- 原 D032 成功演出直接使用 `Bubble.prefab` 的 `startSize=0.2~0.4`，Prefab 显式配置
  `heartEmissionRate=5`，并由共享组件额外即时发射首颗爱心。
- `DrawTwoBallReunionSuccessHandle` 新增默认值为 `1` 的 `heartSizeMultiplier`；D032 Prefab 显式设为
  `1.5`，只缩放运行时克隆粒子的 `main.startSize`，等效范围约 `0.3~0.6`。持续发射率降为 `4`，
  首颗即时发射、演出时长、正式贴图、Sorting Order、相机与两阶段成功流程不变。
- `Bubble.prefab` 和 `Assets/Game/Resources/Sp_hx_ax_01.png` 均不修改；缺少正式贴图时的程序心形
  回退同样经过尺寸倍率。该共享参数同步作用于 D006/D032/D033/D042/D043，不改变本关尖刺失败链、
  球释放、DB、Luban、UI、analytics 或平台流程。
- Unity 6000.0.59f2 自带 Roslyn 完整编译为 `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断。
  D032 当前静态检查为 `Definitions/LocalReferences/duplicate/missing=259/539/0/0`，成功组件、
  rate `4`、size `1.5` 各 1 个；本节数据取代上文针对旧 Prefab 修订的历史计数。Unity Editor.log 已记录五个目标
  PrefabImporter 成功导入并完成脚本重编译；尖刺/成功竞态、爱心遮挡与节奏、Reset/Replay、Android/iOS 横屏
  仍为 `uncertain`。
- 仅回滚本次爱心表现时恢复倍率 `1` 和发射率 `5`；无需回滚正式贴图、Bubble 模板、尖刺处理器、
  双球释放或成功门禁。
