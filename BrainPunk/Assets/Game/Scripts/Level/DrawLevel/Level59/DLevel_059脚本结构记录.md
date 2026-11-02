# DLevel_059 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 059（`20059 / DLevel_059`）。
- 记录日期：2026-08-18。
- 玩法基线：复制 D058 的猫夹角色与蜂群生存结构；猫先夹住角色可见飞入，第一笔有效画线后夹子张开并把角色切为 Dynamic，玩家坚持 7 秒成功，蜜蜂命中角色后走 1.5 秒失败演出。
- 本轮结论：静态检查未发现缺失玩法组件或引用；只补充与 D058 相同的关卡局部角色防滑材质。

## 2. Prefab 组件与调用链

```text
DLevel_059
├─ DrawLevelCtrl
├─ DrawLineFeature (strokeMass=100)
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl (7s, success)
├─ DLevel53Ctrl / DLevel53Hive
└─ DLevel58ActorReleaseCtrl : DLevel2Ctrl
   └─ DrawGame_AlienClipBallRig
      └─ clipSwingPivot -> Actor.prefab / Rigidbody2D
```

1. `DrawLevelCtrl` 在转场揭开后调用继承的 `PlayDrawIntroAsync()`；Alien 从 `entranceStartPoint` 飞到当前 FINAL，闭夹持有 Actor。
2. 第一笔有效画线触发 `DLevel58ActorReleaseCtrl`、Hive 和成功倒计时。继承流程播放 `clip_open`，Actor 保留世界位姿脱离夹子并切为 Dynamic/WakeUp。
3. 蜂群继续复用 D053 的生成、追踪、碰撞和失败占用；首次命中角色在接触点播放 Knock，并在 1.5 秒后提交失败。若未失败，7 秒倒计时提交成功。
4. Retry 复用 DLevel2 完成态，恢复 Alien FINAL、闭夹和 Actor Kinematic，不重播画面外飞入。

## 3. 关键绑定与本关差异

| 项目 | DLevel_059 绑定/参数 |
| --- | --- |
| `DLevel58ActorReleaseCtrl` | fileID `900000000000000007`，脚本 GUID `98e78cc6aaf249b183132cd4d46d4d2c` |
| Actor Rigidbody2D | fileID `1138963634036635986`；共享源 fileID `2233608387543695698` |
| 释放父级 / 摆动点 | D059 根 `8549164348132234925` / `3729883368532928849` |
| 蜂群 / 成功倒计时 | `DLevel53Ctrl` fileID `900000000000000006` / `7s` fileID `900000000000000005` |
| 防滑材质 | `DrawStrokeStable`，摩擦 `0.8`、弹性 `0`、Maximum 合并 |

- 相比 D058，本关保留 `strokeMass=100`、更大的蜜蜂冲量（主冲量 `600`、水平冲量 `300`）、更低蜂巢位置、两枚新增静态圆形障碍和删除 `Ground_long` 的布局差异。这些均视为关卡调参，不做复制清理。
- 猫夹 Rig 中针对已删除原 `yellow_ball` 的失效 Override 不影响运行，本轮不扩大范围清理。

## 4. 数据与影响范围

- 配置静态存在且唯一：`tbdrawleveldata.json` 为 `20059 -> DLevel_059`，`tbdrawlevelsort.json` 含一个 `20059`。
- 本轮运行时修改仅为 `Assets/Game/Prefabs/DrawLevel/DLevel_059.prefab` 的 Actor Rigidbody2D 材质 Override。
- 未修改共享 `Actor.prefab`、`DLevel2Ctrl`、D053 蜂群脚本/Prefab、DrawLine 实现、Excel/JSON 生成流、UI、存档、Analytics、SDK 或 Physics2D 矩阵。

## 5. 验证、风险与回滚

- 静态检查：Prefab 有 `143` 个唯一 anchor、`0` 个重复 anchor、`0` 个悬空本地 fileID；9 个脚本 GUID 均可解析；Actor 防滑材质 Override 恰好 `1` 条。
- 静态检查：猫可见 Intro、首笔 `clip_open`、Actor Rigidbody、蜂巢、Knock 特效、1.5 秒失败和 7 秒成功字段均已绑定。
- `uncertain`：Unity PlayMode 中的实际猫入场、开夹、角色停止距离、障碍/画线/蜂群碰撞、成功与失败竞争、Reset/Replay 及 Android/iOS 横屏尚未验证。
- 回滚防滑时只移除 Actor 根 Rigidbody2D 的 `m_Material` Override；若需整体回滚 059，应恢复该 Prefab 的用户基线。无需修改共享脚本、配置或其它关卡。
