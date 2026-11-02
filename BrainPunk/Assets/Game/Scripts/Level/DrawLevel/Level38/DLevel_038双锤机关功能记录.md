# DLevel_038 双锤机关功能记录

## 需求与原始状态

- 需求：中央齿轮固定左右两个锤子；玩家完成第一笔有效画线后，双锤切换为 Dynamic 并在重力作用下向下旋转；运行时画线或任意锤子撞到 Actor 都判定失败。
- 修改前，`DLevel_038.prefab` 已有双锤、中央齿轮的视觉摆位，以及运行时画线上的碰撞陷阱组件；双锤与齿轮没有刚体、铰链和碰撞盒，`DLevel038Ctrl` 也只有空生命周期方法。
- 本关保留现有 `DrawLevelCountdownCtrl` 的 7 秒成功倒计时，不新增或修改通关规则。

## 影响范围

- `Assets/Game/Prefabs/DrawLevel/DLevel_038.prefab`
- `Assets/Game/Scripts/Level/DrawLevel/Level38/DLevel038Ctrl.cs`
- 复用 `IDrawStrokeCompletedHandler`、`DrawLevelCtrl`、`DLevel4RockTrapHandle` 和既有 `OnTriggerTrapEvent` 失败链。
- 不修改 Luban/配置表、存档、资源加载、UI、广告、分析打点或平台 SDK。

## 当前行为与物理设置

1. 中央 `Sp_l93_cl_01` 使用 Static `Rigidbody2D`，作为两个铰链共同的固定连接体。
2. 左右 `Sp_hx_cz_01` 各有一个 `Rigidbody2D` 和一个不启用角度限制、马达的 `HingeJoint2D`；控制器保留两个 Rigidbody 各自序列化的质量，当前左锤为 `50`、右锤为 `5`，不再在 Awake 时统一覆盖。线性阻尼为 `0.05`、角阻尼为 `0.2`、重力倍率为 `1`，并使用 Interpolate 和 Continuous Collision Detection。
3. 开场与尝试重置时双锤为 Kinematic，并恢复初始位置、角度和零速度。`DrawLevelCtrl` 分发第一笔有效画线完成回调后，两锤在同一帧切换为 Dynamic。
4. 铰链锚点由控制器根据中央齿轮世界坐标重新计算，`useLimits=false`，因此旋转角度不受限制。
5. 每个锤子使用“细杆 CapsuleCollider2D + 锤头 BoxCollider2D”的复合实心碰撞盒，避免整张透明图片形成过大的死亡范围。
6. 锤子复用 `DLevel4RockTrapHandle`；画线继续使用 prefab 原有的碰撞陷阱组件。两者命中 Actor 后都进入既有死亡表现和 `OnTriggerTrapEvent -> DrawLevelCtrl` 失败重置流程。

## 验证证据

- 使用 Unity 当前 `Assembly-CSharp.rsp` 和 Unity Roslyn 编译全部运行时代码：`CSC_EXIT=0`，没有 D038 新增编译错误；输出仅包含工程既有 warning。
- Prefab YAML 静态检查：136 个 anchor 全部唯一；136 个本地非零引用均能解析，没有缺失 fileID。
- `git diff --check` 通过，未发现新增空白错误。
- 静态确认双锤控制器、2 个锤子刚体、2 个无限制铰链、4 个复合碰撞盒、2 个锤子陷阱组件和 1 个中央 Static 刚体均已序列化。
- 静态确认本关 `strokeMass=10`，运行时画线质量按长度约为 `2.5～10`；左锤 `mass=50`、右锤 `mass=5`，控制器代码中不再存在 `hammer.mass` 赋值，因此该不对称质量会真实进入物理模拟。

## 未验证风险

- `uncertain`：Unity 当前仍处于资源加载状态，尚未完成 PlayMode 的第一笔释放、双锤同步下摆、锤子/画线命中 Actor、7 秒通关、失败重开和清线复位实测。
- `uncertain`：复合碰撞盒按当前视觉尺寸静态拟合，锤头边缘的公平性、两锤在最低点互相碰撞后的手感及移动设备低帧率下的高速接触仍需人工验收。
- `uncertain`：当前左右质量比为 `10:1`；最终右推距离还会受到画线形状、落地点摩擦、两锤接触时序和框体是否卡住 Actor 的影响，需要在 Inspector 中继续调节，但不需要修改状态机代码。

## 回滚

- 可从 `DLevel_038` 根节点移除 `DLevel038Ctrl`，并移除中央齿轮的 Static Rigidbody2D、左右锤子的 Rigidbody2D/HingeJoint2D/复合 Collider2D/陷阱组件，即可恢复为纯视觉摆位。
- 画线碰撞失败组件是修改前已有内容，不属于本次双锤物理回滚范围。
- 无数据迁移、配置回滚或运行时资源清理要求。
