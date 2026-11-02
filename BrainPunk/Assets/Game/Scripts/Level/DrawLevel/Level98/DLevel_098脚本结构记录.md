# DLevel_098 脚本结构记录

> 本文保留首次接入记录。2026-09-16 后续按用户要求将本关蓝色结构改为黄色，并接入与画线相同的弹射；当前变化与验证见文末“黄色墙体与画线同规则弹射”。

## 需求与当前基线

2026-09-16，用户要求制作 D098，过关方式与 D087 保持一致，并明确“098地图已经弄好了”。因此以用户现有 DLevel_098.prefab 为地图基线，只读参考当前 D087 的玩法组件，不重新复制或覆盖地图。

本轮没有新增生产 C#。D098 原有七个关键组件与 D087 序列化内容完全相同，成功门已正确绑定。接入只独立 98 号提示词条，并将本地 20098 占位墨水参数从 15/0/2/5 对齐现有 Prefab 的 50/0/10/20。源 Excel 因占用尚未写回，详见下文。完整地图试玩状态为 `uncertain`。

当前 feature plan `specs/037-remote-resource-loading/plan.md` 缺失，`.codegraph` 没有有效索引，本次使用实际源码、Prefab YAML、Luban 导出和逐字段验证。

## 组件树与职责

```text
DLevel_098
├─ DrawLevelCtrl                  输入、尝试状态、成功失败与重建契约
├─ DrawLineFeature                多笔画线、墨水与星级、笔画完成分发
├─ DrawLevelCountdownCtrl         首笔完成后20秒超时失败
├─ DLevel2Ctrl                    夹球装置入场、首笔放球、Retry恢复
├─ DLevel85SpikeTrapHandle        绑定球触碰尖刺后的失败演出
├─ DLevel84StrokeSpawnGuard       新笔画出生时动态重叠碰撞保护
├─ Alien / DrawGame_AlienClipBallRig
│  └─ yellow_ball / DLevel84BallSpeedLimiter / Actor
├─ DrawGame_SuccessDoor           入门触发本关成功链
└─ 用户完成的石块、装饰、地面、相机、Tips和出界区域
```

D2 复用 IDrawLevelVisibleIntroHandler 和 IDrawStrokeCompletedHandler；倒计时与出生保护沿现有笔画/重置接口协作。没有 DLevel98Ctrl 空壳、全局事件、共享 Actor API 或运行时对象查找的新增。

## 生命周期与过关方式

1. 现有加载链创建 DLevel_098 与 Draw HUD，D2 完成可见夹球入场后开放画线。
2. 第一笔完成时 D2 每次尝试只开夹、释放一次球，球脱离动画父级并切 Dynamic；倒计时启动20秒，后续画线不重复放球。
3. 画线使用零弹性材质，由球限速器执行有界法向弹射：倍率2、线速度上限30；出生保护临时忽略新线与出生重叠动态物体的碰撞，分离后恢复。
4. 球进入有效成功门，门 onEnter 调用同一根 DrawLevelCtrl.OnTriggerSuccess。共享代码检查当前 Actor 身份、存活与结果状态，完成开门和 Actor/球壳渐隐后提交成功。并非坚持20秒胜利。
5. 尖刺、出界与20秒超时沿原失败流程；暂停停止倒计时，结果确定后停止计时。D2 的尝试恢复保持原夹持初态，Reset/Replay/下一关和退出清理由原共享链负责。

源码依据：`Level2/DLevel2Ctrl.cs` 的 `OnDrawStrokeCompleted`、`OnDrawAttemptReset` 与 `ReleaseBall`；`DrawLevelCtrl.cs` 的 `OnTriggerSuccess`、`TryTriggerActorSuccess`、`ResolveSuccessTriggerActor` 和 `PlaySuccessAsync`；`DrawLevelCountdownCtrl.cs` 的首笔、暂停、结果与超时分支。

## 显式绑定与保留参数

| 组件或用途 | 当前绑定/参数 |
| --- | --- |
| 主控 / 画线 | 900000000000000001 / 900000000000000002 |
| D2 / 球刚体 | 900000000000000039 / 6262018380601849612 |
| 倒计时 | 700000000000000037，cdTime=20，isSuccess=0 |
| 尖刺失败 / 新线出生保护 | 850000000000000001 / 840860000000000001 |
| 球限速器 | 840850000000000001，倍率2、速度上限30 |
| 有效成功门实例 | 2322687108484577280，onEnter→本关根 OnTriggerSuccess |
| 成功门Spine / 球壳表现组 | 7594331605050263445 / 3091325050892645083 |
| 画线 | 墨水50，星级0/10/20，多笔，质量32，最小质量比例0.25 |
| 线材质 | GUID 8db86cf100124ebfbbb13b4e50990f8f，沿用零弹性材质 |
| Prefab身份 | GUID 1686d958571aaa747a24d05413916436，保留用户原GUID |

七个关键组件逐块等同 D087；用户地图、对象启停、成功门位置、碰撞体和物理参数均没有修改。

## 数据、语言与资源流

- 本地 `20098 → DLevel_098` 与排序 `98 → 20098` 已存在；不重复新增或重排，保留版本1.15.0、类型0与锁类型1。
- I2 新增 `S_DrawLevel_Tips_Level_98`，完整复制87语言内容、空槽与Flags，保持“弹性的线4”原文案；Prefab Tips 仅由87键改为98键。
- `tbdrawleveldata.json` 只修改20098的 maxInkLength/inkStarStage2/inkStarStage3，目标50/0/10/20；所有其他记录及 `tbdrawlevelsort.json` 保持。
- `LevelData.xlsx / draw_level` 候选仅 D103、H103:K103；`LevelSort.xlsx / drawLevelSort` 候选仅 D103。候选由 Artifact Tool 编写，随后仅合入原OOXML对应单元格，以保留其它部件、样式、类型和内容。
- 两份源表仍被Excel占用，打开写入返回PermissionError，本次未强行关闭Excel或覆盖工作簿。候选在 `E:/WorkSpace/outputs/dlevel98-20260916/`；只含D098改动，不合入其他任务92～97尚未写回的候选。
- 保存关闭源表后执行同目录 `install_workbooks.py`。安装前检查两份源表快照均未变，再同时打开两份文件；若用户保存导致源文件变化，先重新按当前表定位98并合并本关单元格，不能直接覆盖。
- 共享加载、HUD、存档、解锁、打点和SDK代码未改；未发布 Firebase。

## 验证、边界与结构复盘

`outputs/dlevel98-20260916/verification.json`：481个唯一YAML对象、958处本地引用闭合、42个外部GUID可定位；夹球装置9个源组件绑定有效，Prefab GUID唯一。只有Tips所在的1718395716459421693块变化，其余480块逐字保留。七个关键组件与D087一致，门成功绑定有效，2352份旧关/共享脚本、Prefab/meta哈希未变。

Luban隔离导出成功，候选20098与运行JSON一致；Excel其它单元格、类型、样式、无关ZIP部件保持，局部前后渲染和错误扫描通过。验证脚本为 `verify.mjs`，表格证据为 `workbook-validation.json` 和 `luban.log`。

本轮未改变C#或玩法参数，不重复编译/运行历史物理探针。没有完成正式Unity整图解法、Spine入场/门演出、失败重开、暂停退出、存档打点和Android/iOS验收；静态绑定一致不能替代这些运行证据。

结构复盘：用户已完成的新地图只做身份与配置接入；完整复制的现有Handler直接复用，不新增编号空壳或重做门成功流程。源表占用时单独保留当前关候选和安装前快照检查，不能为合并本关而覆盖其它任务的待写配置。该结论不升级为共享运行时抽象。

回滚与手工验证见相邻《DLevel_098功能与风险评估》。

## 2026-09-16：黄色墙体与画线同规则弹射

用户确认仅调整 D098：碰到原蓝色结构时按画线规则弹射，并使用 l118 对应黄色素材。修改基线重新取自用户最新保存的500对象地图，未使用首次接入的481对象备份覆盖地图。

- `DLevel_098.prefab` 中40处 `Sp_l118_zz_03` 换为黄色 `Sp_l118_zz_01`，60处 `Sp_l118_zz_04` 换为黄色 `Sp_l118_zz_02`。两组图片尺寸分别同为68×67、68×134，PPU和中心枢轴一致，Renderer为Simple；位置、缩放、翻转和排序不改。本关没有 `Sp_l118_pt_02` 引用。
- 20个 StoneB 的 BoxCollider2D 从 `DLevel084_StableGround` 改绑已有 `DLevel084_ElasticLine`（GUID `8db86cf100124ebfbbb13b4e50990f8f`）。包括4个原本禁用的同类结构，所有启停状态仍保留。两个禁用的普通 Ground_mid/long 不属于蓝色结构，保留其原材质。
- 球上既有 `DLevel84BallSpeedLimiter` 根据接触材质身份识别弹射面，直接复用倍率2和速度上限30。该线材质仍为零物理弹性，增能由真实接触回调执行；没有提高共享物理材质的bounciness，没有新增或修改生产C#。
- 画线、球材质/刚体、首笔放球、20秒失败、尖刺、有效门成功绑定、墨水、Tips、配置和存档/打点链不变。D086/D087及共享脚本、素材未修改。

证据位于 `E:/WorkSpace/outputs/dlevel98-yellow-20260916/`：`verify.mjs` / `verification.json` 核对500对象、996处本地引用和42个外部GUID；仅100个SpriteRenderer引用及20个BoxCollider2D材质变化，其余380块逐字保持，所有Transform、碰撞体形状和行为组件保持；804份受保护文件哈希一致。

独立 Unity 6000.0.59f2 项目使用未改动的生产限速器、实际材质及项目Physics2D设置执行 `D098YellowBounceCheck.Run`。五种接触方向（地面、左右墙、顶部、35度斜面）共20组对照：入射速度8时旧地面出射3.1828，黄色面及从刚体继承材质的画线均为15.9139；入射25时黄色面出射限为30。两个黄色封闭框在0.02和1/30秒步长下各连续模拟30秒，未穿出框，限速点峰值30，7391次断言通过。

测试边界：限速在FixedUpdate和碰撞回调执行，非接触帧随后仍积分重力，积分后峰值分别30.0897/30.1719，下一步重新限速；初版探针把积分后速度也要求严格≤30，已按实际时序修正断言，生产代码不变。正式D098整图解法、入场/门演出、重开及设备试玩仍为 `uncertain`，隔离物理检查不能替代整关验收。

结构复盘：现有限速器以显式材质引用识别弹射面，关卡墙体可通过局部Prefab绑定复用，不需要新增关卡脚本或改共享材质。首次接入的源Excel待写状态未在本轮处理。
