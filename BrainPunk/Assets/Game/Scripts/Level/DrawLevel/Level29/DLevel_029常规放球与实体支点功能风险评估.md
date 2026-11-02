# DLevel_029 常规放球与实体支点功能风险评估

## 功能基线

- 本关是进场即放球的猫夹球关卡：首次进入时整套装置横向飞入，到达最终位置后立即开夹放球，猫夹退回场外，球落稳并完成双弹后才开放画线；第一笔不负责放球。
- 高尔夫解法由玩家自行画线完成。`Circle (4)` 只是实体物理支点，画线与球完全依赖 Rigidbody2D、Collider2D、重力和碰撞响应。
- 成功继续使用右侧通用门；失败继续使用 20 秒倒计时和现有 Right、Bottom、Top FallArea。

## 原始问题

1. D029 根节点仍绑定复制来源的 `DLevel27Ctrl`，关卡脚本身份错误。
2. DLevel2 需要的猫、球和 Spine 字段全部为空，只能依赖运行时层级搜索兜底。
3. 成功附属渲染组为空，球内 Actor 渐隐时蓝色球壳不会同步渐隐。
4. 虚线提示实例已经摆好，但 `DrawLineFeature.tipsObjects` 为空，提示按钮和重置流程不能控制该对象。
5. Circle 残留主线 Level5 的 `Level5Anchor`，但本对象没有 SpringJoint2D 且 `trackLine=null`；该组件不会产生任何支点效果，只会造成错误归属和维护误导。

## 最终实现

- 根节点改为直接复用现有自动投放 `DLevel10Ctrl`，并绑定共享 RigView、FromLeft 入场和既有落地/Retry 参数；移除基于 DLevel2 的 DLevel29 薄包装脚本。
- 显式绑定共享 Rig 的 `DrawAlienClipBallRigView`；Alien、球刚体、猫/夹子 Spine 与 BoneFollower 继续由该 View 封装。
- 绑定成功球壳渐隐组和现有虚线提示对象。
- 删除无效 `Level5Anchor`，保留 Circle 原有非 Trigger CircleCollider2D，使它继续作为玩家画线可接触的静态场景支点。
- 保留用户当前球质量 `1` 与画线 `strokeMass=120`、按长度缩放、多笔、50 墨水和原地图布局。
- 新增本关独立 `DLevel029_GolfBall` 材质：friction `0.08`、bounce `0.25`、Friction Combine `Minimum`、Bounce Combine `Maximum`；球阻尼由共享值 `0.2 / 0.5` 局部覆盖为 `0.08 / 0.2`。
- 本关画线模板 `gravityScale` 局部覆盖为 `1.15`，Circle 支点复用 `DrawStrokeFree` 零摩擦材质；这些参数随运行时克隆和 Collider 接触进入 Physics2D 求解，不使用脚本补速度。
- 首轮增强参数为 ball `friction=0.03 / bounce=0.55 / damping=0.03,0.1`、line gravity `1.5`，试玩反馈导致普通线条也能轻易送球进门。本轮保留支点低摩擦和用户的 `strokeMass=120`，只回调全局碰撞爆发与球体续航。
- 明确未实现：用于高尔夫解法的运行时 HingeJoint2D、JointMotor2D、自动吸附、自动旋转、AddForce/AddTorque 或自定义成功条件。Intro、退场和双弹仅复用 `DLevel10Ctrl` 既有 Tween。

## 预期行为

1. 首次进关播放猫夹球入场；装置到位后先开夹并让轻球进入 Dynamic 物理，猫夹退场且球完成落地双弹后才允许画线。
2. 玩家画出第一笔有效线后只启动 20 秒失败倒计时，球不会在这一笔重复释放。
3. 玩家线条自身也是 Dynamic 刚体。线条是否围绕 Circle 转动、何时击中球以及击球方向，完全由线形、重心、线质量、Circle 接触点和重力决定。
4. 进入成功门后球壳和 Actor 一起渐隐；越界或超时沿用共享失败重建。
5. 手动重开、失败重开和 Replay 不重复播放首次入场；球直接安置到夹点下方最近的本关静态落点并休眠，猫夹保持场外。

## 风险与调参边界

| 风险 | 影响 | 建议 |
| --- | --- | --- |
| 线与 Circle 接触后滑落而非绕点 | 解法不稳定 | 优先微调 Circle 位置/半径、提示线形、线质量或物理材质；不要先加自动铰链 |
| 普通短线仍能把球直接送进门 | 绕 Circle 的关卡意图失效 | 对比普通短线与完整摆锤线；继续过易时先小幅提高球阻尼/摩擦，不改 Circle 支点稳定性 |
| 高尔夫球材质影响首次自然落地 | Intro 稳定落地判定可能变慢 | 核对 8 秒 landing timeout 和双弹衔接；必要时下调 bounce，不改共享 `DLevel10Ctrl` |
| 球下方最近 Static 面不是预期小平台 | Retry 后球落点错误 | PlayMode 核对向下 Cast 命中；必要时只调本关静态 Collider 或搜索起点，不改共享控制器 |
| Circle 为 Layer 0 实体 Collider | 玩家不能穿过支点内部连续画线 | 这是当前实体支点规则；应围绕外轮廓作图，是否改 Layer/Collider 需玩法验收后决定 |
| 没有 Left FallArea | 球向左飞出后可能等到超时才失败 | 试玩确认入场路径安全后再决定是否补 Left |
| 多笔且笔间不碰撞 | 后续线不会与旧线互撞 | 当前保持用户配置；若设计只允许一根球杆，应由策划明确后再改单笔 |
| 物理帧率差异 | 移动端击球距离和接触稳定性可能不同 | 验证 30/60 FPS 与 Android/iOS；优先调质量、阻尼、材质和 Collider |

## 影响范围、验证与回滚

- 本次类型更正影响 `DLevel_029.prefab`、移除的 Level29 薄控制器及本地文档；共享 `DLevel10Ctrl`、DrawGame 中央行为和其它关卡不变。
- 本次击球距离与难度平衡新增一个 D029 局部 PhysicsMaterial2D，并只修改本关球/线/支点的 Prefab 参数；回滚到首轮增强值时恢复材质 `0.03 / 0.55`、球阻尼 `0.03 / 0.1` 和线 gravity `1.5`；完整撤销则移除球材质 override、恢复共享阻尼并清空 Circle 材质。
- 未修改共享 Draw/Actor/猫夹球、配置、存档、UI、资源地址、Physics2D Matrix、analytics、SDK 或平台代码。
- 隔离 Unity Roslyn 全量编译为 `0 error / 104 warning`；最新球材质 NativeFormatImporter 与 D029 PrefabImporter 均成功生成 artifact，未见反序列化、Missing Script 或材质引用错误。
- Prefab 为 147 个唯一 YAML anchor、本地引用缺失 0；球材质、支点材质、画线模板和共享球 Rig GUID 均可解析，旧 DLevel29 GUID 为 0。
- 完整 PlayMode、首次投放/退场/双弹、Retry/Replay 静态落点、真实高尔夫线形、成功渐隐、左侧出界、暂停与移动端均为 `uncertain`。
- 只回滚本次类型更正时，恢复原 DLevel29 脚本/meta 和 Prefab 上的 DLevel2 序列化字段；提示、球壳渐隐和 Circle 清理应继续保留。
