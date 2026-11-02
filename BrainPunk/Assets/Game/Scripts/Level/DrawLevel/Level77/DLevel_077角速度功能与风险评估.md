# DLevel_077 角速度功能与风险评估

## 基线、变更与复用

2026-09-18用户要求提供设置Rigidbody2D角速度的脚本并在本关Sp_boxr_dj_01使用。原四个机关已有Kinematic刚体、角阻尼0.05、约束None和圆形碰撞体/陷阱，本次为开场90°/秒旋转分别挂载通用组件并显式绑定。保留原D2入场/首笔放球、10秒失败、门成功、美术、位置、缩放、碰撞和物理参数。

修改：Assets/Game/Scripts/Level/DrawLevel/Rigidbody2DAngularVelocityCtrl.cs/.meta及使用记录、本关Prefab与本目录文档、中央结构演进和当日记录。未改旧关、共享脚本、源资源、配置表、事件类型、Packages或Assets/GF。

流向：Inspector配置/公开API→目标angularVelocity→UnityPhysics2D旋转。无新存档、加载、UI、打点或SDK逻辑。

## 风险与处理

| 风险 | 当前处理/证据 | 未验证项 |
| --- | --- | --- |
| 误绑或扩大物理参数改动 | 四个原刚体显式绑定，只改组件表；130原块保持 | 正式Prefab Inspector可见效果 |
| 用Transform旋转导致物理不同步 | 直接设置真实角速度，独立UnityPhysics2D方向/停止通过 | 真实碰撞与陷阱作用 |
| 阻尼导致速度变化 | Kinematic原0.05阻尼真实1秒90°通过；Dynamic仍正常衰减 | 强接触及其他脚本覆写 |
| 禁用组件误以为物理停止 | 本组件一次设置，禁用不接管已写速度；停止用SetAngularVelocity(0)，已实测 | 产品是否需要额外结果冻结未提出，本轮不增职责 |
| 开场与暂停/结算/重建时机 | applyOnEnable为运行态应用，非Editor写入；自动开关/重新启用通过 | 主关卡暂停、失败/成功、转场、Replay |
| 参数范围/方向影响关卡手感 | 先设90°/秒逆时针，可Inspector调整；支持负值和0 | 地图可解性、手感、移动端 |

## 验证与缺口

Roslyn编译0 error，主工程Unity6000.0.59f2编译检查；138锚点唯一闭合，四个目标引用/脚本GUID/配置检查通过。4原块修改、4新增、0删除、130其它块保持，metaGUID保持。

独立Unity6000.0.59f2 PlayMode真实Rigidbody2D/CircleCollider2D/Physics2D.Simulate，逐字生产脚本12项通过，无替身物理。检查90°/秒1秒转90°、-180°/秒半秒转-90°、0停止、配置API、参数保留、禁用/重新启用/关闭自动、Dynamic阻尼衰减。

证据在C:/Users/admin/AppData/Local/Temp/brainpunk-angularvelocity-d77-20260918/，含before/before-bind快照、绑定/静态差异、物理夹具、probe-result.txt（CHECKS=12）、日志。临时产物可能清理，仓内保留结论与边界。

`uncertain`：正式D077整图运行、刀图与圆碰撞体、黄球/玩家伤害、首笔放球、暂停恢复、门成功与倒计时失败、重建/Replay/切关、进度/打点、Android/iOS横屏未实测。至少正式关卡运行一次并覆盖成功/失败/重开，确认四机关方向、转速与暂停行为。

## 回滚

移除四个角速度组件及组件表引用，删除通用新脚本即可撤销新增旋转，不覆盖其它人工地图编辑。速度和启动时机优先修改局部Inspector参数，不通过改动D2或共享Draw脚本处理。
