# DLevel_081 脚本结构记录

## 1. 需求与采用基线

- 需求：第一笔有效画线完成后，三个 `L116ObstacleVisualTemplates` 按 Prefab 中的显式数组顺序循环触发；每次发射前由对应 `Sp_l116_jc_01_RedDownTemplate` 闪烁 2 次，再沿发射器朝向直线连续发射陷阱。
- 默认参数：每轮 3 个、轮内间隔 `0.2s`、速度 `10`、一轮最后一枚发出到下一发射器预警的等待 `1s`。
- 碰撞要求：运行时陷阱只命中当前关 Actor，不与画线绳子或其它场景物体交互。
- 参考基线：首笔后持续生成物采用已验收 DLevel5 的本关局部控制器与生命周期门禁；模板朝向与 Actor-only LayerOverride 参考主线 Level116。DLevel81 不复用 DLevel5 的动态实体箭矢，因为本关明确要求不与绳子交互。
- 仓库规则引用的 `../specs/037-remote-resource-loading/plan.md` 在当前工作区未找到；该计划与本关需求的关系为 `uncertain`，实现仅依据用户当次需求、现有 Draw 生命周期代码和 Prefab 事实。

## 2. 最终脚本树

```text
DLevel_081.prefab
├─ DrawLevelCtrl                         # 共享 Draw 生命周期、暂停与结果所有权
├─ DLevel81Ctrl                         # 本关首笔门禁、预警/发射状态机、对象池与失败提交
├─ L116ObstacleVisualTemplates          # 上方，数组发射器 1，世界向上
│  └─ Sp_l116_jc_01_RedDownTemplate     # 预警模板与对象池来源
├─ L116ObstacleVisualTemplates (1)      # 右侧，数组发射器 2，世界向右
│  └─ Sp_l116_jc_01_RedDownTemplate
└─ L116ObstacleVisualTemplates (2)      # 左侧，数组发射器 0，世界向左
   └─ Sp_l116_jc_01_RedDownTemplate

DLevel81Ctrl.cs
├─ DLevel81Ctrl                         # 关卡局部 MonoBehaviour
└─ DLevel81RuntimeTrap                  # 运行时对象池条目，Kinematic Trigger 直线移动
```

## 3. 类职责与接口

### 3.1 `DLevel81Ctrl`

- 实现 `IDrawStrokeCompletedHandler`，只在 `DrawStrokeResult.IsFirstStroke` 时启动一次循环。
- 读取 `DrawLevelCtrl.IsPaused` 与 `IsOutcomeLocked`；暂停期间预警计时、发射计时和陷阱位移均不推进。
- 订阅 `DrawLevelCtrl.Resolved`，并在结算、尝试重置或组件禁用时停止状态机、隐藏预警、回收全部活动陷阱。
- 通过 `ObjectPoolManager` 注册本关专用 `DLevel81RuntimeTrap` 池。池预热数量为 `3 × projectilesPerBurst`，最大容量为预热数量的两倍；退出本关前先回收活动对象，再注销本关拥有的池。
- 命中 Actor 后复用原陷阱表现边界：播放 `knock`、`ActorCtrl.Die()`、向上击退、`trap` 音效，等待 `trapDelay` 后派发既有 `OnTriggerTrapEvent`，由共享 Draw 主控接管标准失败与重建。
- 不新增全局事件类型，不修改 `DrawLevelCtrl`、`ActorCtrl`、共享 `OnTriggerTrapHandle` 或旧关代码。

### 3.2 `DLevel81RuntimeTrap`

- 实现 `IRecycle`，池取出与回收时统一关闭渲染、Collider 和 Rigidbody2D 模拟。
- 发射时配置为 `RigidbodyType2D.Kinematic`、零重力、连续碰撞检测，并在 `FixedUpdate` 驱动的 `Advance` 中用 `MovePosition` 直线移动。
- Collider 强制为 Trigger，`includeLayers` 只包含 `Actor`，`excludeLayers` 排除其补集；因此画线绳子不会挡住、推动或触发陷阱。
- 达到 `recycleDistance` 后返回池，防止离场对象持续占用容量。

## 4. Prefab 序列化绑定

`DLevel_081.prefab` 根节点新增一个 `DLevel81Ctrl`，所有用户已放入对象均通过 Inspector 序列化引用，没有运行时按名字或层级扫描：

| 字段 | 绑定 |
|---|---|
| `drawLevelCtrl` | 根节点现有 `DrawLevelCtrl` |
| `actor` | 当前关 ActorCtrl |
| `knock` | 当前关现有击中粒子 |
| `launchers[0]` | 左侧 `L116ObstacleVisualTemplates (2)` / 对应 RedDown SpriteRenderer |
| `launchers[1]` | 上方 `L116ObstacleVisualTemplates` / 对应 RedDown SpriteRenderer |
| `launchers[2]` | 右侧 `L116ObstacleVisualTemplates (1)` / 对应 RedDown SpriteRenderer |

默认配置为：

- `projectilesPerBurst = 3`
- `projectileInterval = 0.2`
- `launchSpeed = 10`
- `launcherInterval = 1`
- `warningFlashInterval = 0.15`
- 固定 `WarningFlashCount = 2`
- `recycleDistance = 25`
- `knockForce = 10`
- `trapDelay = 0.5`

## 5. 关键生命周期与顺序

```text
首笔有效画线
  → 左侧发射器模板亮/灭两轮
  → 立即发射第 1 枚
  → 0.2s 后第 2 枚
  → 0.2s 后第 3 枚
  → 1s 等待
  → 上方发射器同流程
  → 右侧发射器同流程
  → 回到左侧发射器
```

- 2026-09-20 按用户追加要求，数组顺序调整为“左侧 → 上方 → 右侧”，即从左侧起始顺时针循环；仅修改 Prefab 序列化数组，不修改状态机代码与各发射器自身方向。
- `launcherInterval` 的定义是“当前发射器最后一枚陷阱发出后，到下一个发射器开始闪烁前”的等待时间。
- 发射方向采用 `-directionRoot.up`。这是现有 RedDown 资源的局部向下方向；按当前数组顺序实际依次得到世界向左、向上、向右。
- 闪烁过程直接控制各发射器原有 RedDown `SpriteRenderer.enabled`；模板本体的旧 Collider、Rigidbody2D 模拟和 `OnTriggerTrapHandle` 在运行时关闭，仅运行时克隆负责命中。
- 失败命中后停止新发射并回收全部活动陷阱；延迟事件用尝试版本号与 Destroy Token 拦截重置、退出后的晚到回调。

## 6. 局部与共享边界

- 共享复用：`DrawLevelCtrl` 的首笔通知、暂停/结算状态与陷阱失败链；`ObjectPoolManager`；`ActorCtrl` 死亡/击退；现有音效和粒子。
- 本关局部：三个发射器顺序、闪烁次数、状态机、对象池条目、直线移动和 Actor-only 触发过滤。
- 不复用共享 `OnTriggerTrapHandle` 作为运行时池条目命中处理。其内部 `isDead` 不提供池重置入口，直接克隆复用会使回收后的对象保持已触发状态；本关只在模板/克隆上停用该组件，保留源文件不变。
- 不修改 Level116 或 DLevel5；避免为了复用改变已验收关卡行为。

## 7. 验证证据

- Unity 6.0.59f2 主编辑器观察到新增脚本后执行实际 `Csc Assembly-CSharp.dll`、域重载和资源导入；该次编译段没有 DLevel81 编译错误。最终源码又补充了结算门禁即时清场，并使用同一 Unity 版本的 Roslyn `csc.dll` 与当前 `Assembly-CSharp.rsp` 全量编译到外部 Temp，退出码 0、无 DLevel81 警告或错误；主编辑器尚未对这笔最终小改动再次刷新。
- Prefab 静态校验：162 个 YAML 锚点唯一；新增 Ctrl 根组件引用 1 个、组件块 1 个、脚本 GUID 引用 1 个、发射器绑定 3 个；本关所需根节点、Actor、粒子、三个方向根和三个 SpriteRenderer 引用均存在。
- 2026-09-20 顺序调整后的当前 Prefab 有 168 个 YAML 锚点且全部唯一，所有非零本地引用均能解析；数组精确为左侧方向根 `4639078936631138437` → 上方 `7291427824156184849` → 右侧 `2628472558087041681`。锚点数量增加来自用户期间保存的当前 Prefab 状态，不将其归因为本次顺序调整。
- 代码静态核对：首笔门禁、固定两次闪烁、默认 `3 / 0.2 / 10 / 1`、顺序取模、暂停门禁、Reset/Resolved/Disable 清理、Actor-only LayerOverride 均在最终源码中闭合。
- 命令行 `dotnet build Assembly-CSharp.csproj` 未形成有效编译证据：Unity 自动生成工程引用了约 200 个当前不存在的 `Temp/bin/Debug/*.dll`，在进入本次源码诊断前即失败。
- 未执行正式 PlayMode、Game 视图目视、真实画线碰撞或移动端测试；实际闪烁观感、三方向画面位置、难度节奏、命中/重开和低帧率表现仍为 `uncertain`。

## 8. 回滚路径

1. 从 `DLevel_081.prefab` 根节点移除 `DLevel81Ctrl` 组件块及其根组件引用。
2. 删除本目录的 `DLevel81Ctrl.cs` 与对应 meta。
3. 保留三个用户原有 `L116ObstacleVisualTemplates`、Draw 主控、Actor、画线和其它地图内容不变。
4. 同步撤回本文件、风险记录、中央演进记录和当日使用记录中的 DLevel81 章节。
