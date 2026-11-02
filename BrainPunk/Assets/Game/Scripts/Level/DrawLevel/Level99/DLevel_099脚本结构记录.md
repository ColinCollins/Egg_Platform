# DLevel_099 脚本结构记录

## 需求与参考基线

2026-09-16，用户要求“D099和096规则一样，走进门里”。以用户现有D099地图和当前D096行为组件为依据，保留D099场景布局与20墨水、0/5/10星级参数。参考当前D096的首笔向右行走、10秒超时失败、角色进门成功及踩线阴影处理。

D099原来已具备正确的D001行走绑定与成功门事件，但没有倒计时和踩线隐藏组件；画线模板为Layer14，Actor接地掩码16712，Tips仍共用94。本轮补齐D096的计时与阴影绑定，独立99提示和配置。D099没有D096的15块可释放石头，不复制D4释放列表或石头机关。

未找到既有Level99记录，当前feature plan `specs/037-remote-resource-loading/plan.md`缺失；按当前源码、Prefab、配置和隔离检查建立本关记录。无新增生产C#，完整整关与设备验收为 `uncertain`。

## 脚本树与职责

```text
DLevel_099
├─ DrawLevelCtrl                  输入/尝试/结果门禁、Actor进门身份校验与成功演出
├─ DrawLineFeature                20墨水、多笔物理画线、星级0/5/10
├─ DLevel001Ctrl                  首笔完成后驱动本关Actor向右行走
├─ DelayAutoBeginDrawHandle       可见Intro后按原流程开放画线，delay=0
├─ DrawLevelCountdownCtrl         首笔后10秒超时失败，最后5秒显示HUD
├─ DLevel096LineShadowHandle      实际踩线时隐藏Shadow，离开恢复
├─ TrapEventHandlle               现有尖刺触发的死亡/击退/粒子表现
├─ Actor                         共享Actor.prefab，本关Inspector显式引用
├─ DLevel088_ReverseGravityZone×2 现有反重力区域，位置与参数保留
└─ 成功门 / 平台 / 尖刺 / 出界区域 / 相机 / 背景 / Tips
```

D001和倒计时实现 `IDrawStrokeCompletedHandler`，通过既有主控分派首笔与尝试复位。阴影组件通过本关显式引用读取角色接触，不新建事件或跨关查找。没有DLevel99Ctrl空壳，不修改已完成关卡脚本、共享Actor、主控、阴影算法、重力区或GF。

## 生命周期、成功与失败

1. 关卡加载后沿原可见Intro开放输入。首笔前角色不由D001主动行走，也未开始倒计时。
2. 首笔有效画线完成后，D001向右设置输入并调用Actor更新；倒计时从10秒开始。后续笔画不重新开始行走尝试或重置计时。
3. 两块现有反重力区继续原D88机制，未改区域形状、筛选层或动力学。D099的画线层由14改为13，与D096相同；Actor接地掩码增加13层至24904，原层保留。
4. 球或画线不是本关成功主体。有效门的 `OnTrigger2DHandle.onEnter` 调用本关 `DrawLevelCtrl.OnTriggerSuccess`，按主控运行/结果/失败状态、当前Actor身份与存活状态过滤。成功占用后停走，执行开门和角色渐隐，完成后原链提交成功。
5. 10秒归零走 `TryFailAndReset`，不按坚持到时间结束判胜。现有尖刺和出界失败继续原流程；暂停停止行走输入与倒计时，恢复后继续。成功或失败Resolved通知行走与倒计时停止。
6. 阴影仅在脚底真实支撑接触Layer13时隐藏，侧碰/顶线不算；离线恢复原activeSelf，结果锁定后不重新打开阴影。非结算态停用只恢复本组件曾隐藏的状态。
7. Reset/Replay仍由既有整关重建流程负责，复位接口清理行走和计时。没有修改存档、打点、解锁、转场、SDK和资源加载链。

源码依据：`Level1/DLevel001Ctrl.cs`、`DrawLevelCountdownCtrl.cs`、`Level96/DLevel096LineShadowHandle.cs`；成功过滤和演出见 `DrawLevelCtrl.cs` 的 `OnTriggerSuccess`、`TryTriggerActorSuccess`、`ResolveSuccessTriggerActor`、`PlaySuccessAsync`。

## 显式序列化引用

| 对象或组件 | 绑定及参数 |
| --- | --- |
| 主控 / 画线 | 900000000000000001 / 900000000000000002 |
| D001 / Actor | 900000000000000003 / 1716601560298323527 |
| Intro | 900000000000000004，delay=0 |
| 倒计时 | 3865479001201854532，cdTime=10、isSuccess=0，绑定同根主控 |
| 踩线阴影 | 910000000000000001，复用D096只读脚本，lineLayers=8192 |
| 角色主PolygonCollider / Shadow根 | 7771184473350448207 / 1484931394204090090，均指向本关Actor实例1238832509353424896 |
| 角色源组件 | Collider源8856887770667473999；Shadow源408348339682887402 |
| 成功门实例 / 门Spine | 6932855938480942226 / 2984154252043883271，onEnter→本关根OnTriggerSuccess |
| 画线模板实例 | 3027212103460825394，覆盖Layer13 |
| 画线保留参数 | 墨水20、星级0/5/10、质量200、多笔、最小质量比例0.25 |
| Prefab身份 | GUID 4a70595e2bc21c849bb4c0dc4f9706a7，保留原身份且唯一 |

D096的倒计时、阴影组件与两个Actor stripped引用块原样复用；主控、D001、Intro也与当前D096逐块相同。用户D099的所有Transform、装饰、门位置和地图碰撞几何保持。D096自身在本次开始前已有其它参数调整，本关不照搬它的80墨水或石头设置。

## 配置、资源与UI流

- 现有选关 `99→20099→DLevel_099` 保持，类型0、锁1和版本1.15.0不变。运行配置仅将20099占位15/0/2/5改为Prefab实际20/0/5/10。
- I2新增 `S_DrawLevel_Tips_Level_99`，完整复制当前96语言、空项和Flags，中文为“反重力区域”；本关Tips从94键改为99，原94/96词条不变。
- `LevelData.xlsx / draw_level` 仅第104行D104描述及H104:K104参数；`LevelSort.xlsx / drawLevelSort` 仅D104描述。候选以Artifact Tool编辑后，将目标单元格合回原OOXML以保持无关部件和样式。
- 源Excel仍占用，写入返回PermissionError，未强行关闭Excel或覆盖源文件。候选位于 `E:/WorkSpace/outputs/dlevel99-20260916/LevelData.xlsx` 与 `LevelSort.xlsx`，仅包含本次99差异。此前92～98待写候选没有被合入或宣称已同步。
- 同目录 `install_workbooks.py` 在两表快照不变且同时可写时安装。如果用户保存后源表变化，必须重新合并99单元格，不能用旧候选覆盖新编辑。源表落盘前，从旧表重新生成会覆盖99运行参数。
- 无Firebase发布、SDK调用、全局事件新增或存档迁移。

## 验证、结构复盘与边界

证据目录 `E:/WorkSpace/outputs/dlevel99-20260916/`。`verification.json`：164个对象、310处本地引用闭合、46个外部GUID可定位；只改4个原对象块并增加4块，156个其它原对象块逐字保持；2357份受保护脚本、旧关Prefab与meta哈希一致。成功门绑定本关主控，7个复用组件/绑定块与D096相同。

独立Unity 6000.0.59f2项目逐字复制当前D001、倒计时、D096阴影组件和ShadowController，并使用项目Physics2D/层设置，48项通过：22项首笔/10秒/后续笔画/暂停恢复/失败演出/成功停止/超时一次/三次重建与停用；26项真实Physics2D角色多边形与水平线、斜线、线端、侧碰、顶线、下层地面、离线恢复及结果锁阴影检查。`mechanics-checks.txt`、`shadow-checks.txt`和`unity.log`留存结果。首次隔离编译仅因探针输出路径使用未转义Windows反斜杠失败，修正测试路径后通过，未修改生产代码。

边界：主控、Actor行走API与HUD为测试替身，阴影检查使用真实Actor形状和Physics2D；门事件/身份链完成静态核对，没有完整加载正式D099、Actor动画、门演出、SDK或移动端。完整路线能否10秒到门、提示线、失败重开、退出/存档/打点和Android/iOS为 `uncertain`。

Luban隔离导出通过，生成20099与本地运行记录一致；候选表的非目标单元格、数据类型、样式及其它ZIP部件保持，局部前后渲染与错误扫描通过。源Excel写回尚未完成。

结构复盘：按“首笔行走＋计时＋进门成功”的职责复用现有组件；阴影局部表现组件可由新关显式绑定复用。没有石头的地图不引入空释放列表，不为了新关迁移或改写D096。回滚与手工验收见相邻风险记录。
