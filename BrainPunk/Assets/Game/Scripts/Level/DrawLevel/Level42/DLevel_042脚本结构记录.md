# DLevel_042 脚本结构记录

## 1. 需求与原始状态

- D042 与 D032 使用相同的双球玩法：玩家完成第一笔有效画线后，两颗球进入动态物理，接触后过关。
- 本关明确没有尖刺，也不需要尖刺命中、击退爆炸或局部失败控制器。
- 初始 Prefab 由 D032 复制，残留了 `DLevel32SpikeTrapHandle` 和
  `DLevel032_Knock_Explosion`；用户已确认这些无消费者残留可以删除。
- D042 自己的场景 Transform 与美术布局属于本关内容，不按 D032 覆盖。

## 2. 最终结构

```text
DLevel_042
|-- DrawLevelCtrl
|-- DrawLineFeature
|-- DLevel001Ctrl
|-- DelayAutoBeginDrawHandle
|-- DrawTwoBallReleaseCtrl
|-- DrawLineArea
`-- level6
    |-- yellow_ball               # 左球，Kinematic / mass 10
    |   `-- payloadContentRoot/Actor
    `-- yellow_ball               # 右球，Kinematic / mass 10
        |-- DrawTwoBallReunionSuccessHandle
        `-- payloadContentRoot/Actor
```

Prefab 不包含尖刺控制脚本、Knock nested Prefab、Knock 粒子引用或尖刺命中失败分支。

## 3. 行为与复用

- `DrawTwoBallReleaseCtrl` 显式绑定左右球，实现 `IDrawStrokeCompletedHandler`，不实现 Intro 接口。
- 第一笔有效画线完成后，两球清零速度并从 Kinematic 切到 Dynamic；后续笔画不重复释放。
- Attempt Reset 恢复两球父级、局部位姿、缩放、碰撞盒偏移、payload 镜像和 Kinematic 初态。
- 右球的 `DrawTwoBallReunionSuccessHandle.otherBall` 显式绑定左球；碰撞冻结、爱心、镜头聚焦和
  两阶段成功提交沿用共享逻辑，不新增距离吸附、脚本施力或保送分支。
- `DrawLineFeature.strokeMass=100`，与用户指定的 D032 双球玩法基线一致；其它行为参数也保持一致。
- 本关没有新增 C# 脚本、全局事件、关卡编号判断或共享 Physics2D Layer Matrix 修改。

## 4. 配置与数据流

- `LevelData.xlsx/draw_level` 为 `20042 / DLevel_042`，墨水量 15，评级阈值 `0/2/5`。
- `LevelSort.xlsx/drawLevelSort` 为排序 `30 / 20042`。
- 运行时 `tbdrawleveldata.json`、`tbdrawlevelsort.json` 与 Excel 一致；当前共 31 项，D042 为
  `30 / 20042`，随后是并行新增的 `31 / 20043`。
- Guru 的 `guru_spec.yaml` 与 `GuruAllSpecs.G.cs` 默认 `drawgame_config.level_sort` 已同步同一 31 项顺序。
- `drawgame_config.enabled=false`；真实远端后台若启用后是否覆盖本地顺序仍为 `uncertain`。

## 5. 验证

- D042 Prefab 静态检查：259 个唯一 YAML anchor、259 个唯一非零本地引用、0 duplicate、
  0 missing local reference。
- 尖刺脚本 GUID、Knock Prefab GUID、`DLevel032_Knock_Explosion` 名称及对应旧 fileID 均为 0。
- 排除 Transform 布局与 D032 专属尖刺后，D042 与 D032 的双球、释放、成功和画线行为参数无差异。
- D042 配置加入时的隔离 Luban 生成退出码 0且输出哈希一致；随后用户新增 D043 并正式生成，当前两份
  Draw JSON 为 31 项，D042 行与位置保持不变。
- Unity 日志已记录 D042 PrefabImporter 成功；并行加入 D043 后的最终 31 项 `guru_spec.yaml` 尚未由
  当前 Editor 自动重新导入。
- 使用 Unity 6000.0.59f2 当前 `Guru.SDK.Gen.rsp` 的 Roslyn 独立编译最终
  `GuruAllSpecs.G.cs`，退出码 0且无诊断。
- PlayMode 首笔释放、双球接触、连续 Reset/Replay、暂停恢复与 Android/iOS 横屏尚未执行，结论为
  `uncertain`。

## 6. 回滚

- 若只回滚画线重量，将 `strokeMass` 恢复为旧值 20；这会重新偏离 D032 双球基线。
- 若移除 D042，应成套移除 Prefab、Excel 中 `20042`/排序 30，并重新运行 Luban 与 Guru 同步。
- 不应把已删除的尖刺控制器或 Knock 粒子恢复到无尖刺关卡；未来新增危险物时应按新需求显式实现。

## 7. 2026-08-05 爱心粒子尺寸与密度调整

- D042 的 `DrawTwoBallReunionSuccessHandle` 与 D006/D032/D033/D043 共用同一爱心表现。原随机尺寸来自
  `Bubble.prefab` 的 `0.2~0.4`，持续发射率为 `5/s`，碰撞时另有一颗即时爱心。
- 共享脚本新增默认 `1` 的 `heartSizeMultiplier`；D042 Prefab 显式设为 `1.5`，只把克隆粒子的
  `main.startSize` 等比放大到约 `0.3~0.6`，持续发射率同步降为 `4/s`。正式贴图继续使用
  `Assets/Game/Resources/Sp_hx_ax_01.png`，程序纹理仅为缺资源回退。
- `Bubble.prefab`、正式贴图、D042 自有布局、画线重量、球物理、相机、成功时长、配置、存档、UI、
  analytics 与平台流程不变。
- Unity 6000.0.59f2 自带 Roslyn 完整编译为 `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断。
  D042 当前静态检查为 `Definitions/LocalReferences/duplicate/missing=252/543/0/0`，成功组件、rate `4`、
  size `1.5` 各 1 个。Unity Editor.log 已记录五个目标 PrefabImporter 成功导入并完成脚本重编译；本关布局中的遮挡、
  爱心密度、连续 Reset/Replay、暂停恢复和 Android/iOS 横屏性能仍为 `uncertain`。
- 本次表现可独立回滚为倍率 `1`、发射率 `5`；不恢复已删除的尖刺/Knock，不回滚画线重量或关卡配置。
