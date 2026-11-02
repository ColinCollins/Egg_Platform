# DLevel_037 中心齿轮物理与五秒通关功能记录

## 1. 需求与最终行为

- 中央齿轮固定，长平台以齿轮中心为唯一转轴，在真实重力和碰撞作用下自由旋转，不限制角度。
- 第一笔有效画线完成前，平台和 Actor 保持 Kinematic；第一笔完成后两者同时释放为 Dynamic。
- Actor 只在 D037 复用 friction `0.8` 的 `DrawStrokeStable.physicsMaterial2D`，共享 Actor Prefab 不变。
- 本关成功方式与其它限时成功关一致：第一笔有效画线完成后启动通用 `5s` 倒计时，时间归零直接成功。
- 不检查平台角度、角速度或是否平衡；没有失衡清零、宽限、回正或其它附加成功逻辑。
- 关卡四周复用共享 `FallArea.prefab`，Actor 越出任一边界时沿用现有失败重建链。

## 2. 影响范围

- Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_037.prefab`
- 本关脚本：`Assets/Game/Scripts/Level/DrawLevel/Level37/DLevel37Ctrl.cs`
- 复用脚本：`DrawLevelCountdownCtrl`、`FailAreaTriggerHandle`
- 复用资源：`FallArea.prefab`、`DrawStrokeStable.physicsMaterial2D`
- 无 DB/Luban、存档、UI Prefab、全局事件、Physics2D Layer Matrix、analytics、SDK 或平台分支变更。

## 3. 物理与边界参数

| 对象 | 当前配置 | 作用 |
| --- | --- | --- |
| 中央齿轮 | Static Rigidbody2D | 铰链固定 connected body |
| 长平台 | 首笔前 Kinematic，首笔后 Dynamic；mass 10；linear damping 0.05；angular damping 1.5；gravity 1；Interpolate；Continuous | 承载 Actor 与画线，并围绕齿轮自由旋转 |
| HingeJoint2D | anchor `(2.7022433,0)`；connected anchor `(0,0)`；Limits/Motor/connected collision 均关闭 | 固定转轴但不限制旋转角度 |
| Actor | mass 3；继承 gravity 4.5；本关 material friction 0.8、Maximum combine | 作为左侧真实载荷并减少在平台上的侧滑 |
| Actor Ground Mask | `24904`，局部包含 Layer 13 | 识别 `WithoutShadowGround` 平台为地面 |
| DrawLineFeature | 多笔；stroke mass 30 按长度缩放；最低比例 0.25 | 继续作为右侧配重，不改共享质量公式 |

四个 FallArea 沿用 DLevel_017 已验证过的固定视野外布局：

| 名称 | 中心位置 | Transform Scale | 共享 Collider 原始尺寸 |
| --- | --- | --- | --- |
| `FallArea_Left` | `(-18.23, 0)` | `(1, 1.5)` | `10 x 10` |
| `FallArea_Right` | `(18.1, 0)` | `(1, 1.5)` | `10 x 10` |
| `FallArea_Bottom` | `(-0.66, -10.39)` | `(2.99625, 0.78749996)` | `10 x 10` |
| `FallArea_Top` | `(-0.66, 10.39)` | `(2.99625, 0.78749996)` | `10 x 10` |

## 4. 生命周期与结算

1. `DLevel37Ctrl.Awake()` 缓存平台和 Actor 初始位姿，并应用齿轮、平台与铰链物理参数。
2. 第一笔有效画线完成时，`DLevel37Ctrl` 只负责释放平台和 Actor；不读取或提交成功条件。
3. 同一 Stroke 事件由 `DrawLevelCountdownCtrl(cdTime=5, isSuccess=true)` 启动通用倒计时。
4. 未暂停时倒计时按 `Time.deltaTime` 递减；归零后由通用控制器提交 `Success`。
5. `OnDrawAttemptReset()` 分别让物理控制器恢复位姿、让通用倒计时停止并清理 HUD。
6. Actor 先进入任一 FallArea 时，现有失败事件和 `DrawLevelCtrl` 结果锁负责阻止成功/失败重复结算。

## 5. 验证、风险与回滚

- 独立 Roslyn 全量 `Assembly-CSharp` 编译 `exit code 0`；输出只有工程既有 warning 和旧 Roslyn
  无法加载 Unity 6 SourceGenerator 的 warning，没有 C# error。
- Unity Editor 日志确认 `DLevel_037.prefab` 由 PrefabImporter 成功导入；YAML 静态检查为 146 个 anchor
  全部唯一、146 个本地引用全部可解析。
- 静态确认 DLevel37Ctrl 已无角度、角速度、计时、HUD 或 `TryResolve` 代码；Prefab 恰有一个
  `DrawLevelCountdownCtrl(cdTime=5,isSuccess=true)`、四个共享 FallArea，且无 Actor gravity override。
- `uncertain`：完整 PlayMode 下第一笔释放与倒计时是否同帧符合预期、Actor 四向越界失败、暂停、
  Reset/Replay、倒计时与 FallArea 同帧竞争、以及 Android/iOS 横屏表现。
- 回滚本轮结算调整：移除根节点 `DrawLevelCountdownCtrl`，再另行实现新的成功条件；回滚边界时移除四个
  FallArea nested instance 及根 Transform 子引用。物理机关回滚需同时移除 DLevel37Ctrl、HingeJoint2D
  和相关 Rigidbody2D override，不能只删除脚本。
