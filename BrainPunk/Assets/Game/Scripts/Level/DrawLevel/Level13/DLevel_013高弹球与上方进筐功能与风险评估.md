# DLevel_013 高弹球与上方进筐功能与风险评估

## 1. 需求与原行为

- 初始需求：球撞普通地面几乎原地等高反弹，撞玩家画线后有可见能量衰减。
- 2026-08-03 试玩反馈：球整体偏轻飘，画线也先按 `0.98` 高弹试玩；因此本关球
  Gravity Scale 从 `1` 提到 `1.5`，画线 Bounce 从 `0.65` 提到 `0.98`。
- 需求二：球必须从篮筐上方弹入才过关；用户绑定的两个 BoxCollider2D 作为左右实体篮沿。
- 原 Prefab 由 DLevel015 结构复制，仍挂 `DLevel15Ctrl`，可以入场和放球，但两个篮沿只发生
  普通物理碰撞，没有任何成功提交组件。
- 共享球和画线的 Rigidbody2D/Collider2D 原本都没有本关弹性材质；球 Linear Damping 为 `0.2`。

## 2. 新行为

```text
首次猫夹球入场
  -> 第一笔有效线
  -> 开夹，球 Dynamic
  -> Gravity Scale 1.5：缩短下落/回弹周期，降低轻飘感
  -> 普通地面：约 0.98 弹性，接近等高反弹
  -> 玩家线：本轮同样约 0.98 弹性
  -> 球心向下穿过篮沿上平面，记录从上方进入
  -> 仍在开口内并向下穿过篮沿下平面
  -> 立即锁定成功，球保持 Dynamic 继续运动
  -> 在下平面精确穿越点播放常规成功门彩纸烟花
  -> 0.35 秒后 Actor/球渐隐 + 胜利结算
```

- 材质只覆盖 DLevel013 内的嵌套球 Rigidbody2D 和画线模板 Rigidbody2D，共享 Prefab 不变。
- 弹性差异由 PhysicsMaterial2D Combine 求解，不使用 `OnCollisionEnter2D` 二次修正速度。
- 两个篮沿保持 `isTrigger=false`；代码从 Bounds 计算虚拟计分平面，不增加第三个篮筐碰撞盒。
- 烟花抽为共享 `DrawGame_BasketSuccessFirework.prefab`；它继承常规成功门的 Confetti Burst、
  尺寸、向上受力和 SceneLayer 排序覆盖，根 SortingGroup Order 为 `220`，并关闭 Loop/Play On Awake。

## 3. 影响范围

- 新增：`Level13/DLevel13Ctrl.cs`、两个 DLevel013 局部 PhysicsMaterial2D、本关结构/风险文档，
  以及 D013/D014 共用的篮筐成功烟花 Prefab。
- 修改：`DLevel_013.prefab`、`DrawLevelCtrl.cs`的无成功门渐隐入口。
- 复用：共享猫夹球 Rig、DLevel2Ctrl、DrawLineFeature、DrawLevelCountdownCtrl、FallArea、Draw 结算/UI。
- 不修改：共享 Rig/DrawLine Prefab、既有 `Bouncy` 材质、Physics2D 全局 Bounce Threshold/Layer Matrix、
  DB/Luban、存档、资源地址、analytics、广告/IAP、SDK 或平台配置。

## 4. 关键参数与可调范围

| 项目 | 当前值 | 建议 PlayMode 可调范围 |
| --- | --- | --- |
| 球 Bounciness | `0.98` | `0.95–1.0` |
| 球 Gravity Scale | `1.5` | `1.2–2.0`；调节下落/回弹节奏，不用质量伪造重力 |
| 球 Linear Damping | `0.03` | `0.02–0.05` |
| 画线 Bounciness | `0.98` | 本轮试玩；若需要恢复衰减可回到 `0.65` |
| 进筐 X 容差 | `0.04` | `0.03–0.05`；优先修篮沿几何，不建议超过 `0.05` |
| 最小向下速度 | `0.1` | `0.05–0.2` |
| 成功渐隐延迟 | `0.35s` | `0.2–0.6s`；期间球保持 Dynamic，不定格在篮内 |
| 烟花位置 | 下平面插值穿越点 | 不使用篮筐中心或球成功后的延迟位置 |

## 5. 风险

| 风险 | 等级 | 当前控制 | 未验证项 |
| --- | --- | --- | --- |
| 球仍显轻飘或回弹节奏过快 | 高 | Bounce 0.98、Damping 0.03、Gravity Scale 1.5 | 实际连续 3–5 次高度和周期待量测 |
| 画线高弹导致难以修正弹道 | 中 | 当前按反馈设为 Minimum + 0.98；Unity 6 API 明确 Rigidbody2D 材质应用于全部 attached Collider2D | 高弹画线手感待 PlayMode |
| 碰框或从侧面进入误判成功 | 高 | 上/下双平面、向下速度、X 开口三重门禁 | 擦框高速弹跳待验证 |
| 篮口或篮沿几何影响空心球 | 中 | 当前实体开口约 2.141，球直径约 1.526 | Contact Offset/不同斜线待验证 |
| 成功后球在渐隐前运动过远 | 中 | 立即占用结果锁，保持物理 `0.35s` 后才渐隐 | 实际出筐路径与渐隐时机待 PlayMode |
| 烟花层级、尺寸或重复播放异常 | 中 | 共享 Prefab 非循环/非自播；仅成功锁占用后实例化；Reset 清理 | D013 实机视觉待验证 |
| 暂停中物理、判定或演出继续 | 中 | 已识别球模拟、FixedUpdate、既有无门成功等待与新烟花均未统一冻结 | 飞行中及得分瞬间暂停待治理 |
| 无门成功 API 影响旧成功门 | 中 | 旧入口仍显式 `playDoorAnimation=true` | 普通成功门回归待 PlayMode |
| 高弹持续模拟影响移动端 | 中 | Continuous，不改全局物理参数 | Android/iOS 60 fps 待验证 |

## 6. 验证证据

- C# 全量 Roslyn 编译：`0 error`，输出仅项目既有 warning。
- Prefab：D013 `109` 个 anchor 唯一、本地 fileID 无缺失；篮沿两盒仍为非 Trigger；共享烟花
  `5` 个 anchor 唯一且 Unity PrefabImporter 导入成功。
- 引用：DLevel13Ctrl、球 Rigidbody/CircleCollider、两篮沿、DrawLevelCtrl、球/线材质和共享烟花全部非空。
- 影响隔离：共享球、共享画线和既有 Bouncy 材质没有修改。
- `uncertain`：未执行完整 Unity PlayMode 及移动端验收，弹跳手感、可解性与判定时机待玩家实测。

## 7. 手工验收清单

- [ ] 第一笔有效线后只放球一次，猫/夹子动画正常。
- [ ] 球垂直撞普通地面后，第一次回弹高度接近原下落高度。
- [ ] 球撞画线后也保持接近 `0.98` 的高弹，且不出现脚本瞬移/速度跳变。
- [ ] 只碰左/右框、从下方弹过、在篮筐侧面穿行均不过关。
- [ ] 空心球从上往下穿过两个虚拟平面后只成功一次。
- [ ] 烟花只在确认成功的下平面穿越点播放一轮，失败、碰框和 Reset 不残留粒子。
- [ ] 成功后球不定格，继续按真实物理运动；约 `0.35` 秒后人物和球同步渐隐，且没有缺失成功门的报错。
- [ ] 15 秒归零、FallArea、失败重开、手动重来与 Replay 全部正常。

## 8. 回滚路径

1. 若只回滚本轮试玩参数，将球 Gravity Scale 恢复 `1`、画线 Bounce 恢复 `0.65`；
   完整撤销弹性功能时再移除球/线局部材质覆盖并恢复球 Linear Damping `0.2`。
2. 移除 DLevel13Ctrl 的篮筐字段/检测，或恢复 DLevel15Ctrl，会撤销进筐成功但保留猫夹球放球。
3. 移除 `TryTriggerSuccessWithoutDoor` 时必须同步替换 DLevel13 调用；旧 `OnTriggerSuccess` 不需回滚。
4. 不要回滚用户绑定的两个篮沿 BoxCollider2D 位置/尺寸，除非 PlayMode 确认开口物理过紧。
5. 若只撤销成功后的自由运动，恢复 DLevel13 成功时清速/Kinematic，并将 `successFadeDelay` 设为 `0`。
