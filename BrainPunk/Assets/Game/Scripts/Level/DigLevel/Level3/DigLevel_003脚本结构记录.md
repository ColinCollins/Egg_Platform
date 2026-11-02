# DigLevel_003 多角色绑定记录

## 需求、基线与职责

用户要求场上全部角色初始化，所有角色到达各自匹配Index的目标才胜利。现有003已有两个DigActorCtrl实例（Index0／1）和目标0／1／2，但根控制器只绑定一个历史actor；没有新增角色、目标或改变美术／地图。修改前核对记录在DigGame功能说明的2026-10-13章节。

- 根DigLevelCtrl显式Actors列表绑定两个现有角色；原actor字段保留兼容，运行时不扫描层级。SerializedObject在真实PrefabStage保存列表，仅合并控制器块及第二角色stripped组件引用块。
- DigActorCtrl独立初始化／成长，上报自身与病菌；DigLevelCtrl管理各角色接触批次、到达、冻结、结果和统一墨水输入。任何角色大病菌失败优先；病菌只认领一次。
- DigTargetTrigger保留已显式绑定的所属关卡和Collider，按attachedRigidbody核对角色／Index。角色首次到达执行OnReached、冻结本人，全部到达才BeginVictory；目标2无需命中，不按目标数量判胜。
- 成功目标源Prefab的Visual持有图与灯，DigPumpkinSuccessEffect只动画Visual，物理与出生洞源保持原层级；02／03变体继承，原Sprite覆盖不改。
- 初始化／暂停／失活沿IDigLevelRuntime，结果继续通过Resolved到DigPlayCtrl；不新增全局事件、存档、资源加载、运行时构图或专用共享Actor动作。

## 验证和边界

真实Unity隔离23项通过，含003两个角色初始化、部分到达后其它角色继续、重复／错Index／外来拒绝、全部到达后原延迟一次胜利，以及物理与动效隔离。Editor／Android条件编译0错误。日志和源码范围见DevelopMD/验证记录/Dig多角色_2026-10-13/验证记录.md。

正式003从出生点挖掘路线、地图是否需单独重新烘焙、UI／广告／存档、Android／iOS真机与性能仍uncertain；本次仅完成多角色控制和绑定，不修改关卡图数据或承诺整关可通。

## 回滚

仅恢复根控制器原单actor块并删除新增第二Actor引用块，同时按模块记录回退多角色代码及Visual隔离；保留原角色实例、目标、布局和地图。快照在TEMP brainpunk-multi-actors-20261013/DigLevel_003.before.prefab。
