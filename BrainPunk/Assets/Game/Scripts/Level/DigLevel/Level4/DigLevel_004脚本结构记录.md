# DigLevel_004 进入故障与绑定记录

## 木头滑动：修改前核对（2026-10-13）

用户截图希望木头沿挖开的斜面滑动。当前木头FixedBrick刚体为Dynamic、Simulated=1、GravityScale=1、LinearDamping=0、Constraints=0，Box为4.81×0.59、EdgeRadius=0，已绑定BaseDigComp轮廓与外扩0.1。木头与CollisionTiles刚体均用Ice（GUID192c74ec22c3e1943ba33537fa8b24bf），其friction=5、FrictionCombine=Average，两个实际Collider材质为空、继承所属刚体；不是从材质名称推断低摩擦。

依据Unity6000.0官方PhysicsMaterialCombine2D／Collider2D.sharedMaterial文档，Collider材质优先于刚体，Minimum优先于Average／Multiply／Mean（Maximum除外）。拟在Level4新增局部Dig004_WoodSlide.physicsMaterial2D：Friction0.05、Bounciness0、FrictionCombine=Minimum，仅绑定木头Box Collider；保持沙土／共享Ice、刚体参数、Collider轮廓、Base、地图、目标、其它关卡与脚本。手写Prefab／原生材质，无生成器或运行时初始化变更。

修改前先在隔离Unity创建实际材质并验证实际木头在同20度坡面的旧／新材质物理位移、绑定保存重导入、沙土原材质保持；再定向合并一个Box块和材质／meta。仅摩擦参数改善不能保证任意阶梯状洞口或残土夹住时均滑动；截图的实际挖掘轨迹／接触点未知，正式手感、完整路线和双端真机仍uncertain。回滚只撤木头Box材质引用／新增局部材质，不整文件恢复地图或并行失败条件代码。

### 用户已改材质后补查（前述材质方案尚未实施）

用户随后反馈改材质仍不滑。最新Prefab木头Box已绑定DrawStrokeStable（Friction0.8），木头和沙土刚体绑定DrawStrokeFree（Friction0）。初次补查错误地按文档列举顺序解释了序列化枚举，导致“有效摩擦应为0”的判断；已由实际Unity枚举／坡面实测撤回。Unity6000.0.59f2实际枚举为Average0、Mean1、Multiply2、Minimum3、Maximum4：Stable实际是Maximum，Free实际是Minimum，Maximum优先取0.8。Collider优先于刚体，刚体的零摩擦不能覆盖木头Box的Stable。保存态仍Dynamic／无锁轴／GravityScale1／Damping0；当前不滑不能继续归因于先前Ice5。

撤销尚未实施的新增低摩擦材质方案，先只在TEMP导入用户最新Prefab／原生材质／地图，比对平滑坡、基于截图近似笔画的真实Tilemap洞口、更长／更宽通道及圆角木头的运动和接触法线。实际笔画／运行时接触未取得，近似复现不能声称现场唯一根因；生产几何／脚本／地图暂不改，取得物理证据后再选最小处理。

### 原生证据后的最小修正方案

同实际木头和同20度平滑坡模拟3秒：当前Stable0.8／Maximum对Free0／Minimum停住，木头Box改Free0／Minimum后沿坡位移约15.2世界单位。因此只将004木头Box5835200198103860221的材质引用96f0e9c7dbad4856bb70b847a68c2c55改成已有DrawStrokeFree（329228da23674f499fd70252e3c1c51d），不新增材质或改变共享材质参数／地图／圆角／刚体。随后用修正Prefab复测平滑坡及预挖斜通道。

原近似笔画案例在初始横向洞与斜洞之间留下承托沙土，木头仍可能水平停住；圆角／粗化Composite也未证明安全可解，不采用。零摩擦不能穿过实体土或提供平地水平推力；真实玩家路径／现场实例接触点仍uncertain，需主工程用同路径验证。回滚只恢复该Box的Stable引用，不回退用户刚体／沙土材质修改。

### 实际沙土接触补查后的范围修正

真实Tilemap斜通道试验发现，当前CollisionTiles的CompositeCollider2D4310272260181104433也独立绑定Stable0.8／Maximum。前述仅看沙土刚体Free0、并推断接触零摩擦的范围不完整；木头Box改Free后平滑坡可滑，但实际沙土仍受Composite的Maximum0.8影响而停住。完整有效材质链必须检查实际接触Collider，不能只看所属Rigidbody。

实施范围补充为004木头Box与沙土Composite两个Collider材质都显式绑定现有DrawStrokeFree0／Minimum，保留两个刚体当前材质、物理形状／尺寸／圆角／VertexDistance、地图、其它关卡和共享材质文件。对本关Actor与沙土接触也降低摩擦是此配置的有意影响，角色滚动／整关手感需主工程验收；不影响其它关卡材质引用。先记录这一依赖再修改Composite，随后重新验证已摆入斜通道的真实木头和两种Collider有效材质。

### 最终配置与验证

木头Box已改Free；尝试改Composite前检测到用户并行将它改成Free，保留用户修改而未重复写入。最终Box／Composite及所属刚体均使用DrawStrokeFree（Friction0、Minimum）。真实Unity隔离验证：仅木头改Free时平滑坡可滑但真实沙土仍停，两个Collider均Free后，在挖出的真实Tilemap斜通道中3秒位移(-3.2145,-1.1112)，约3.4世界单位，再停在通道端部；平滑20度坡位移约15.2。圆角／VertexDistance修改只在TEMP试验，没有回写。

测试斜道约19.5度，木头摆入挖好的通道再模拟，不是原出生点完整玩家笔画回放。当前水平残土承托时仍可停住，需让洞口连通并留足木头两端空间；正式手感／通关路线／双端真机仍uncertain。证据DevelopMD/验证记录/Dig004滑动_2026-10-13/验证记录.md；本次未提交Git。

用户报告第四关无法进入。Editor.log第1203508行明确记录“角色引用缺失或重复”，配置40004映射DigLevel_004且已加载进入初始化。根DigLevelCtrl的Actors为一个有效引用和一个空引用，实际Prefab仅一个DigActorCtrl。因非空列表逐项校验抛异常，DigPlayCtrl.BuildLevelAsync捕获／HandleFailure返回首页。

本轮仅删除根6695274849859263318列表的空第二项，保留4468337775836762992角色。DigLevelCtrl初始化角色／地图、处理匹配目标和统一墨水／结果；Actor上报身份，目标使用既有DigTargetTrigger；胜利模式ReachTarget，现有烘焙地图mapBaked=1、004专属遮罩。不修改布局、Index、目标回调、地图、资源配置或共享脚本，不弱化空引用门禁。

真实Unity6000.0.59f2隔离5项通过，原空项可复现错误，修正后InitializeRuntime完整成功、角色物理可开启。整个Prefab仅删该空项，164个其它块逐字保持。证据DevelopMD/验证记录/Dig004进入_2026-10-13/验证记录.md。

主工程完整进入／玩家挖掘通关／UI／广告／存档、双端真机仍uncertain。重新进入004验证；增删角色后使用“按通关条件收集”并保存。回滚仅恢复第二个空项（会恢复原初始化故障），任务快照TEMP brainpunk-dig004-entry-20261013/before.prefab；不能整文件还原覆盖其它用户编辑。
