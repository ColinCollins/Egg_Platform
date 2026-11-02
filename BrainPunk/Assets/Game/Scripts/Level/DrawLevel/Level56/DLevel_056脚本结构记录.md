# DLevel_056 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 056（`20056 / DLevel_056`）。
- 记录日期：2026-08-17。
- 参考基线：已验收的 DLevel_026 单平台释放逻辑，以及 DLevel_037 的中心齿轮铰链结构。
- 主要资源：`Assets/Game/Prefabs/DrawLevel/DLevel_056.prefab`。
- 当前状态：根节点现已有效挂载平台控制器，补齐首笔画线后的物理释放；Unity PlayMode 和移动端尚未完成验证，标记为 `uncertain`。

## 2. Prefab 组件树

```text
DLevel_056
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl (10s, success)
├─ DLevel53Ctrl / DLevel53Hive       # 当前沿用的蜂群组件，是否属于本关仍待确认
└─ DLevel26PlatformCtrl              # 本次补挂到根节点的单平台绑定
   ├─ gearRigidbodies[0]   -> pingtai/Sp_l93_cl_01/Rigidbody2D
   ├─ platformRigidbodies[0] -> pingtai/platform_Stone/Rigidbody2D
   └─ platformHinges[0]     -> pingtai/platform_Stone/HingeJoint2D
```

## 3. 职责边界

| 组件 | 职责 | 不负责 |
| --- | --- | --- |
| `DrawLevelCtrl` | Draw 生命周期、首笔分发、成功/失败与重试 | 平台刚体参数和铰链几何 |
| `DrawLineFeature` | 采样、生成带碰撞体的画线刚体 | 平台释放时机 |
| `DLevel26PlatformCtrl` | 保存平台初态；首笔有效线后将平台切为 Dynamic 并唤醒；Reset 恢复 Kinematic 初态 | Actor 行为、倒计时、蜂群生成 |
| `DLevel53Ctrl/Hive` | 当前 Prefab 中沿用的 D053 蜂群生命周期 | 平台释放；若本关不需要蜂群，应由关卡设计确认后再移除 |

## 4. 生命周期与关键调用

1. `DLevel26PlatformCtrl.Awake` 校验三组单元素引用，缓存平台位置/角度，并配置静态齿轮、连续碰撞、插值和 HingeJoint2D 连接。
2. 开场和每次 Reset 时平台保持 `Kinematic`，速度与角速度清零，铰链仍连接中心支点。
3. `DrawLevelCtrl` 分发第一笔有效画线回调后，`OnDrawStrokeCompleted` 将平台改为 `Dynamic`、清零残留速度并 `WakeUp`；之后画线碰撞可通过真实刚体力矩让平台左右摆动。
4. `OnDrawAttemptReset` 恢复平台初始位置、角度和等待状态。平台组件不改变共享 Draw、Actor、UI 或倒计时实现。

## 5. 序列化绑定

| 字段 | DLevel_056 fileID | 说明 |
| --- | ---: | --- |
| `DLevel26PlatformCtrl` | `900000000000000007` | 原有组件块补入根节点 `m_Component` 列表 |
| `gearRigidbodies[0]` | `1321536595882453968` | `Sp_l93_cl_01` 的 Static Rigidbody2D |
| `platformRigidbodies[0]` | `7465383645696124887` | `platform_Stone` 的 Kinematic Rigidbody2D |
| `platformHinges[0]` | `6239077892484375286` | 连接中心支点的 HingeJoint2D |

平台原有质量 `10`、线性阻尼 `0.05`、角阻尼 `1.5`、重力倍率 `1` 和碰撞体保持不变；本次只补释放生命周期。

## 6. 与相近关卡的差异

- D026 的控制器支持两组平台；本关用单元素数组复用同一稳定实现，不改 D026 的 Prefab 或脚本。
- D037 还会在首笔后释放 Actor；D056 当前 Actor 仍是复制场景中的 Static 覆盖，因此本次只释放平台，避免擅自改变角色行为。
- D056 仍有 `DLevel53Ctrl` 与 `DLevel53Hive` 引用。它们是否是本关有意的组合玩法缺少用户确认，本次不删除。

## 7. 验证记录

- Prefab 本地 anchor 去重：`0`。
- D054/D055/D056 的脚本 GUID 均能解析到现有 `.cs.meta`；D056 新增脚本 GUID 为 `410f1cee44a64adfa68440f94433fed6`。
- D056 三项本地引用均能在同一 Prefab 中解析；新增挂载行已单独检查，无尾随空格。该 Prefab 当前为未跟踪文件，普通 `git diff --check` 不会覆盖完整资源；Unity YAML 原有的 `m_Name: ` 等空值行保持不动。
- `uncertain`：尚未在 Unity PlayMode 实际画线观察左右摆动、Reset/Replay、暂停/成功期间的刚体状态，也未做 Android/iOS 横屏验证。

## 8. 回滚

从 DLevel_056 根节点的 `m_Component` 列表移除 `DLevel26PlatformCtrl` 引用（fileID `900000000000000007`）即可恢复原行为；不回滚共享 `DLevel26PlatformCtrl`、D053 蜂群脚本或其它关卡。
