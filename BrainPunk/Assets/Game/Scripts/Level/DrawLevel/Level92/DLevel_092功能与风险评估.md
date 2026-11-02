# DLevel_092 功能与风险评估

## 需求和改动

2026-09-16，D092使用D006通关方式：首笔双球释放，配对球接触后冻结、爱心聚焦、延迟成功。D092复制资产已包含完整D6和共享成功Handler，本轮补齐独立提示与本地配置，保留右侧反重力区及用户地图。结构和参数见相邻脚本结构记录。

涉及：DLevel_092.prefab、Assets/Resources/I2Languages.asset、两份Draw配置JSON、Luban两表D092行候选、Level92文档和两份中央记录。无新增运行时代码、数据库/存档字段、全局事件、UI或SDK行为。

## 已完成验证

- `node outputs/dlevel92-20260916/audit.mjs full`：104个YAML对象、200个本地引用，无重复ID、丢失本地引用或GUID。
- `node outputs/dlevel92-20260916/verify.mjs`：103个原对象块逐字保留，唯一修改是Tips语言键。D6、Draw根、画线、入场、旧D001以及双球成功6个组件块与D006逐字一致；两球排除层均0，右球显式配对左球。
- I2独立词条与06完整同源；删除新增92块后整个文件与即时备份相同。两份JSON新增项与隔离Luban输出完全相同，旧条目及顺序全部保持，ID唯一。
- 使用artifact-tool导入、编写和渲染两表目标范围，无公式错误；最终候选只把授权单元格值合入原OOXML，原类型、样式、其它单元格和非目标XML部件保持。源表仍占用，候选已保存，写回待完成。
- 隔离Luban命令：`dotnet BrainPunk/Luban/Luban/Luban.dll -t client -d json --conf outputs/dlevel92-20260916/Configs/luban.conf -x outputDataDir=E:/WorkSpace/outputs/dlevel92-20260916/generated`，成功。未覆盖其它运行配置。
- Unity6000.0.59f2隔离PlayMode使用逐字复制的生产 `DrawTwoBallReunionSuccessHandle`、`DLevel88GravityZone`、`DLevel88ZoneGeometry`，真实Physics2D和DOTween。6组：普通区x=-4、反重力区x=4、边界x=0.125，各含完成演出/中途Reset。
- 6组均通过：碰到非配对物体不成功；配对接触仅占用一次；两球清速度、Kinematic/FreezeAll且反重力不继续移动；镜头聚焦；暂停3秒不提前结算；恢复后1.8秒完成一次，或Reset取消旧成功且恢复约束；结束/取消恢复镜头。
- 证据：`outputs/dlevel92-20260916/reunion-physics.txt`、`unity-probe.log`、`verification.json`、`workbook-validation.json`和Luban日志。探针中早期编译错误为隔离工程缺少粒子模块/测试替身成员，补齐测试依赖后正式6组通过，生产源码未改。

## 风险与未验证项

- **uncertain：完整D092地图解法、真实双夹Spine入场/首笔派发、用户画线支撑与双球相遇路径、四向失败、主场景连续重开、移动端尚未完整试玩。** 隔离测试以简单球/粒子模板及结果门禁替身隔离HUD、Actor、SDK和存档，不等同于整关或视觉验收。无渲染截图验证爱心遮挡和镜头构图。
- 源Excel被打开而未写回；后续需先保存关闭，再校验与备份一致后安装候选。若用户已有额外编辑必须重新按最新表合入D092，不能覆盖。
- Excel预留15墨水，当前Prefab `isConfig=false` 实际40；本轮保留地图40/0/10/20，并准备源表对齐。用户若另选参数，应同步Prefab和对应表/JSON。
- 既有全量Excel排序与运行JSON不一致，本次只接入92。不要全量导出预留但未创建的关卡来掩盖差异；运行中D091仍是旧排序66，D092为92，后续统一排序需验收选择、连续推进和存档兼容。Remote Config若覆盖本地顺序，须由发布流程另行接入D092。
- 无移动端、资源包构建、存档升级、analytics或远端发布验证；未提交Git。

## 回滚

即时备份位于 `outputs/dlevel92-20260916/`。将D092提示键改回06，删除新增I2语言块和两份JSON中D092新项；若两表已写回，只恢复D97及H97:K97到即时备份值，保留后续用户其它编辑。D092 Prefab/Meta原为用户新增，不删除整份地图。共享D006、D88区域、D6及成功Handler无需回滚。文档相应更新。

## 复盘

同机制新关先核对复制组件、配对刚体和碰撞层，接线已完整时直接复用，不按编号创建空壳控制器。反重力与成功冻结需真实物理交叉检查。表格预留行与运行导出可能不同步，应检查当前文件而不是沿用上一关行号或排序ID；表被占用时先交付候选并保留并发编辑保护。

## 2026-09-16 D093制作时的配置状态复核

D093任务开始前，运行Draw JSON已重新导成源表的1～100顺序，本文此前“旧1～66后接92”仅为D092初次交付时状态。D093任务保留新排序，将被旧源表覆盖的D092参数恢复为地图40/0/10/20；源Excel仍占用，最新待写回候选已合并92/93两行，位于outputs/dlevel93-20260916，后续安装应使用合并候选并重新检查并发编辑，不能用旧D092整表覆盖D093。

## 2026-10-12：入场期间暂停后不能画线的共享修复

- 行为基线与直接链路：设置页调用 Draw Pause；双夹 Intro 完成时 `BeginDrawing` 按暂停状态关闭输入；设置页关闭调用 Resume 只解除暂停，DrawLineFeature.Tick 仍因输入 false 返回。Editor.log 有对应 DLevel_092 进关、暂停、Intro 完成和关闭设置顺序；没有将静态状态推演当作当前实例字段实测。
- 影响文件与现有系统：唯一生产脚本为 `Play/DrawPlayCtrl.cs`；复用 IDrawLevelRuntime 的暂停/输入 API 和 DrawLevelCtrl 已有结果演出门禁。没有改本关地图、D6、双球成功/反重力、存档、打点或广告；本轮开始已有本关 Prefab 外部 diff，继续保留。
- 修改后：只有 Drawing 且未加载/切换、runtime 已就绪时在 Resume 补开画线。入场未结束、重建/广告等待、Resolved 或成功/失败演出仍不能提前绘制。
- 验证：完整 Editor 源码编译 0 error / 113 warning；提取生产方法的 19 项内存隔离断言通过，旧实现复现遗漏，修改后恢复且各种门禁保持。证据在 `C:/Users/admin/AppData/Local/Temp/brainpunk-draw-resume-20261012-0a198c9998c9456e828a27fd90ad9f3e/`。全文件回放比较确认除 Resume 外源码与 HEAD 相同。
- 未验证：`uncertain`，正式 D092 完整拖画/双球玩法、重复重开/暂停、实际 SDK 和 Android/iOS 横屏未复测；主 Unity 需使用新编译的脚本后验收“入场期间暂停，等入场结束，再恢复”以及“入场尚未完成就恢复”。
- 回滚：只撤回共享 Resume 的新增条件与补开输入调用，不回退用户的 D092 Prefab 改动，也不清理进度或配置。结构复盘保留共享生命周期职责，不新增本关补丁控制器或事件。
