# DLevel_077 脚本结构记录

## 本次需求、基线与范围

2026-09-18：新增通用角速度组件，在本关 Sp_boxr_dj_01 上使用。当前有无后缀及(1)/(2)/(3)四个机关，先按四个开场90°/秒旋转配置；Inspector可调角速度和自动启用选项。

没有既有Draw77说明或DLevel077Ctrl脚本。当前Prefab复用DLevel2Ctrl的Alien/夹子/球入场释放结构，参考Level2结构记录；保持原行为边界，不为角速度单独新增空壳LevelCtrl。该记录完整关卡验收仍uncertain，不能称为已验收基线。

四个机关原本均有Kinematic Rigidbody2D、角阻尼0.05、约束None、CircleCollider2D及陷阱组件；本次仅增加角速度组件，保留Transform、美术、碰撞、刚体参数、球/Actor/门引用和10秒倒计时。

## 组件树、职责与生命周期

```text
DLevel_077
├─ DrawLevelCtrl / DrawLineFeature    原绘制、状态与结算
├─ DrawLevelCountdownCtrl            原10秒超时失败
├─ DLevel2Ctrl                       原Alien/夹子入场与首笔放球
├─ Actor、yellow_ball、Alien装置      原引用和资源
├─ DrawGame_SuccessDoor              原成功入口
└─ Sp_boxr_dj_01、(1)、(2)、(3)
   ├─ SpriteRenderer / CircleCollider2D / Kinematic Rigidbody2D / 原陷阱组件
   └─ Rigidbody2DAngularVelocityCtrl  新组件，仅设置自身显式绑定刚体角速度
```

新组件为Game.ItemEvent命名空间下MonoBehaviour，无Draw Handler接口。随机关加载/启用时，运行态applyOnEnable调用ApplyAngularVelocity，写入90°/秒；由Unity物理系统积分，无逐帧脚本。公开SetAngularVelocity(float)支持修改/停止，ApplyAngularVelocity()支持无参数事件。无对象查找、实例化、Actor专用动画、UI或新全局事件。

组件不接管DrawLevelCtrl的暂停/结果/清理，也不清除禁用组件后刚体已有速度。全局物理暂停及整关重建仍由既有流程负责；这些整关行为本轮未实测。

## Inspector显式绑定

| 对象 | 原Rigidbody2D fileID | 新组件fileID | 配置 |
| --- | --- | --- | --- |
| Sp_boxr_dj_01 (1) | 5983400808212308729 | 777000000000000001 | angularVelocity90、applyOnEnable1 |
| Sp_boxr_dj_01 | 8529878498365117815 | 777000000000000002 | 同上 |
| Sp_boxr_dj_01 (2) | 1809729807048949046 | 777000000000000003 | 同上 |
| Sp_boxr_dj_01 (3) | 1383948941870576865 | 777000000000000004 | 同上 |

每份targetRigidbody指向对应同节点原刚体。缺失时不写入、不自动解析；未改类型/模拟开关/约束/阻尼。

## 数据、事件与演进复盘

序列化角速度→本地组件OnEnable/API→Rigidbody2D.angularVelocity→UnityPhysics2D。没有新增Luban/远端/存档/加载/UI/打点/SDK链，也不改现有成功门和首笔放球流程。

相对Level2仅增加地图机关物理旋转，不改其已完成脚本。旋转采用真实刚体角速度，不使用ShakeRotateHandle的Transform Tween；由显式引用小组件满足当前通用API需求，不引入恒速循环、共享Level接口升级或旧关迁移。

## 验证、缺口与回滚

- 新脚本Roslyn编译0 error；主工程Unity6000.0.59f2编译证据留存。
- 138个YAML锚点唯一且本地引用闭合，四份绑定/配置正确；4原块修改、4新增、0删除、130其它原块逐字保持；Prefab meta不变。
- 独立Unity PlayMode真实物理12项通过，含Kinematic正负方向/速度、传0停止、自动应用开关/重启、参数保留与Dynamic阻尼。只测试生产组件和合成机关，不证明本关整图表现。
- `uncertain`：实际刀图/碰撞/球与Actor、门成功/死亡、10秒失败、暂停/重建/Replay/切关、存档打点、Android/iOS横屏未实测。
- 回滚仅删除四份新组件及对应组件表引用、通用新脚本，保留最新地图及原物理参数；不整份恢复Prefab。功能风险详见本目录风险评估和DrawLevel模块记录。
