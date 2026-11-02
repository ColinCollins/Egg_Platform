# DLevel_033 脚本结构记录

## 1. 需求与原始状态

- D033 是纯物理双球关卡，过关方式与 D032 相同：玩家画线后，两颗球进入动态物理，接触后过关。
- 本关没有尖刺或其它脚本失败条件；五个圆环是可受力的物理障碍。
- 初始 Prefab 由 D032 复制，因此误带了 `DLevel32SpikeTrapHandle` 和
  `DLevel032_Knock_Explosion`，但场景中没有尖刺消费者。

## 2. 最终结构

```text
DLevel_033
|-- DrawLevelCtrl
|-- DrawLineFeature
|-- DrawTwoBallReleaseCtrl
|-- Sp_hx_yxwp_01 x 5             # Dynamic / mass 1
`-- level6
    |-- yellow_ball               # 左球，Kinematic / mass 10
    |   `-- payloadContentRoot/Actor
    `-- yellow_ball               # 右球，Kinematic / mass 10
        |-- DrawTwoBallReunionSuccessHandle
        `-- payloadContentRoot/Actor
```

Prefab 不包含尖刺 Trigger、`DLevel32SpikeTrapHandle`、Knock 粒子、猫或夹子。

## 3. 行为与复用

- `DrawTwoBallReleaseCtrl` 显式绑定左右球，实现 `IDrawStrokeCompletedHandler`，不实现 Intro 接口。
- 第一笔有效画线后，两球清零速度并从 Kinematic 切到 Dynamic；后续笔画不重复释放。
- Attempt Reset 恢复两球父级、局部位姿、缩放、碰撞盒偏移、payload 镜像和 Kinematic 初态。
- 右球的 `DrawTwoBallReunionSuccessHandle.otherBall` 绑定左球；碰撞冻结、正式爱心贴图、镜头聚焦和
  两阶段成功提交继续复用 D032/D006 已有共享逻辑。
- 五个圆环从开场起就是 Dynamic、mass 1，没有脚本施力、吸附、限制角度或伪物理过关分支。

## 4. 配置与数据流

- `LevelData.xlsx/draw_level` 已有 `20033 / DLevel_033 / 恋爱球球3`，墨水量 15，评级阈值 `0/2/5`。
- `LevelSort.xlsx/drawLevelSort` 为 `29 / 20033 / 恋爱球球3`；已清理复制残留的“尖刺”备注。
- Luban 已成功生成 `tbdrawleveldata.json` 和 `tbdrawlevelsort.json`，运行时可通过正式配置加载 D033。
- 不修改 DB/存档、RemoteConfig、analytics、UI、广告/IAP、资源地址或平台 SDK。

## 5. 验证

- D033 Prefab 静态检查：268 个唯一 YAML anchor、0 duplicate、0 missing local reference。
- 共享释放组件 1 个、双球绑定 2 个、Kinematic/mass 10 球 2 个、Dynamic/mass 1 圆环 5 个、
  双球成功组件 1 个；尖刺控制 GUID、Knock GUID 和 `OnTrigger2DHandle` 均为 0。
- Luban 正式生成退出码 0，并明确覆盖两份 Draw JSON；生成结果含 `20033` 与排序 `29 -> 20033`。
- Unity 自带 Roslyn 使用当前 `Assembly-CSharp.rsp/rsp2` 完整编译通过（退出码 0）；Unity Editor 日志已
  记录 D032/D033 的 PrefabImporter 成功导入。
- PlayMode 首笔释放/圆环碰撞/双球接触、连续 Replay、暂停恢复与 Android/iOS 横屏尚未执行，结论为
  `uncertain`。

## 6. 回滚

- 回滚本关功能时移除 D033 Prefab 与 Excel 的 `20033`/排序 `29` 行，再运行 Luban；不影响 D032。
- 若只回滚物理布局，恢复五个圆环 Transform/Rigidbody2D 参数即可，不修改共享释放和成功组件。
- 不应把已删除的尖刺控制器或 Knock 粒子恢复到无尖刺关卡。

## 7. 2026-08-05 爱心粒子尺寸与密度调整

- D033 的双球成功组件与 D006/D032/D042/D043 共用 `DrawTwoBallReunionSuccessHandle`。调整前沿用
  `Bubble.prefab` 的 `startSize=0.2~0.4`、持续发射率 `5/s`，并保留一颗即时 `Emit(1)`。
- 共享组件新增默认 `1` 的 `heartSizeMultiplier`；D033 Prefab 显式配置 `1.5`，使克隆粒子的随机
  尺寸等效约为 `0.3~0.6`，同时把持续发射率降为 `4/s`。正式贴图仍为
  `Assets/Game/Resources/Sp_hx_ax_01.png`，资源缺失时才使用程序心形。
- 本次不修改 `Bubble.prefab`、正式贴图、五个圆环、双球释放/冻结、镜头、成功时长、配置、存档、
  UI、analytics 或平台分支；尺寸倍率只作用于运行时克隆的 ParticleSystem。
- Unity 6000.0.59f2 自带 Roslyn 完整编译为 `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断。
  D033 当前静态检查为 `Definitions/LocalReferences/duplicate/missing=228/498/0/0`，成功组件、rate `4`、
  size `1.5` 各 1 个。Unity Editor.log 已记录五个目标 PrefabImporter 成功导入并完成脚本重编译；圆环遮挡场景下的
  可读性、粒子密度、Reset/Replay、暂停和 Android/iOS 横屏过绘仍为 `uncertain`。
- 只回滚本次爱心调整时将倍率恢复 `1`、发射率恢复 `5`；不得恢复已删除的尖刺/Knock，也无需改动
  D033 配置或正式贴图。
