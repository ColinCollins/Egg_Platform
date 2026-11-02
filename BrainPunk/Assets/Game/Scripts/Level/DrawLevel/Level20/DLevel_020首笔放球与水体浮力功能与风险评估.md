# DLevel_020 首笔放球与水体浮力功能与风险评估

## 1. 需求与原行为基线

- 需求一：玩家完成第一笔有效画线后，猫按按钮、夹子打开并实际释放球。
- 需求二：球入水后保留一段向下惯性，再由真实物理浮到水面；玩家需要在合适时机继续画出
  有重量的线，把浮球压向水下成功门。
- 修改前本关误挂 `DLevel10Ctrl`。该类不实现 `IDrawStrokeCompletedHandler`，无法接收落笔完成；
  Prefab 的 `rig` 引用又为空且层级与其兜底路径不符，因此 Intro 也不能自动放球，球持续
  Kinematic、夹子持续闭合。
- 水体原有 `BuoyancyEffector2D` 处于 Disabled；Surface Level 11 对应世界 Y `-4.09`，比
  可见水面 `-2.59` 低 1.5。因此修改前只有水面 Mesh/波纹，没有作用于球的浮力。
- 首轮运行反馈显示，启用浮力后的 `Linear Drag 5` 会过快耗尽斜坡碰撞产生的水平动量；
  球从平台右缘入水后到成功门触发框尚需横移约 10.5 世界单位。

## 2. 修改后行为

```text
转场揭开
  -> 猫夹球从左侧 OutBack 入场，闭夹持球
  -> 开放画线
  -> 第一笔有效线完成
  -> clip_open + 猫 control + 球 Dynamic
  -> 重力自由落体并以向下速度入水
  -> 浸没面积产生浮力，水阻逐步耗散速度
  -> 继续下冲一段距离后反向上浮到液面
  -> 后续 Dynamic 重线落下并与球碰撞，把球物理压入水下
  -> 球进入水下成功门后沿用既有结算
```

- `DLevel20Ctrl : DLevel2Ctrl` 只建立本关入口，复用已验证的共享 Rig 引用解析、骨骼挂点、
  首笔幂等、镜像安全放球与 Retry 完成态，不修改 DLevel002/015。
- 浮力采用 Unity Physics2D 的 `BuoyancyEffector2D`，不是 Transform Tween 或预制轨迹。向下
  动量、浮力、阻力、重力与线球碰撞由固定物理步共同求解。
- Effector Mask 只包含 Layer 11 蓝球。Layer 8 线不被水托起，继续受 Gravity Scale 5 和自身
  质量影响，因此能够给球施加真实接触力。

## 3. 影响文件、资源与系统

- 新增：`Assets/Game/Scripts/Level/DrawLevel/Level20/DLevel20Ctrl.cs`。
- 修改：`Assets/Game/Prefabs/DrawLevel/DLevel_020.prefab`。
- 复用：DLevel2Ctrl、共享猫夹球 Rig、DrawLevelCtrl、DrawLineFeature、DrawLevelCountdownCtrl、
  InteractableWater、WaterTriggerHandler、BuoyancyEffector2D、成功门与 FallArea。
- 不修改：共享 DLevel2/DLevel10 代码、共享 Rig Prefab、ActorCtrl、GamePlayPanel、Physics2D
  全局矩阵、Luban/DB、存档、资源地址、UI、analytics、广告/IAP、SDK 或平台配置。

## 4. 参数与风险

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| 球入水后水平动量过快耗尽 | 高 | 根据首轮运行反馈将 Linear Drag 从 5 降为 1；Density 8 保持最终上浮 | 前冲距离、下冲时长和是否过滑待 PlayMode 复验 |
| 球浮不到可见水面或停在水下 | 高 | Surface Level 从 11 对齐到视觉顶部 12.5 | 浸没平衡位置待观察 |
| 线也被浮力托起，无法压球 | 高 | Collider Mask 只允许 Layer 11；线为 Layer 8 | 运行时 Layer override 待验证 |
| 长重线把球永久压底 | 中 | 墨水 20、20 秒倒计时限制尝试 | 难度与可解性待调 |
| 球/线穿透 | 中 | 球沿用 Continuous；线使用现有实体碰撞链 | 高速入水和长线接缝待验证 |
| 重开时重新播放 Intro 或球提前落水 | 中 | 继承 Retry 完成态：FINAL 闭夹、球 Kinematic | 失败/手动重开/Replay 待验证 |
| 成功时球壳留在画面 | 中 | 绑定共享球 SpriteRendererGroupAlpha | 完整门动画待验证 |
| 移动端 FixedUpdate 手感差异 | 中 | 无平台分支，沿用 Unity 2D Physics | Android/iOS 未验证 |

## 5. 验证证据

- Prefab 静态检查：DLevel20Ctrl GUID 唯一，旧 DLevel10Ctrl GUID 为零；Alien、释放父级、
  成功额外渐隐组均有效。
- 水体静态检查：BoxCollider2D 为 Trigger + Used By Effector，Effector Enabled，Surface Level
  12.5、Density 8、Linear Drag 1、Mask 2048；水面 Mesh 顶部与 Effector 表面均为世界
  Y `-2.59`。
- Physics2D 全局 Layer Matrix 当前全部允许；球刚体没有排除 Layer 8，因此线球可碰撞。
- PrefabImporter 通过；127 个 YAML anchor 无重复且本地引用无缺失。
- Assembly-CSharp Roslyn 全量编译为 `0 error`。
- `uncertain`：完整 PlayMode、关卡成功/失败、暂停、连续重开、不同线长，以及 Android/iOS
  横屏设备均未执行。

## 6. 手工验收清单

- [ ] 首次进入时猫夹球从左侧飞入，夹子全程闭合且球无相对漂移。
- [ ] 轻点或无效短线不放球；第一笔有效线完成后只开夹一次。
- [ ] 球先自由落体，入水后继续明显下冲，再平滑减速并回到可见液面附近。
- [ ] 水面在球进入/离开时产生波纹，球没有瞬移、锁位或 Tween 轨迹。
- [ ] 后续画出的线不浮起，能与球碰撞并把球压入水下。
- [ ] 不同长度/斜率的线仍可形成至少一种稳定可解方案，20 秒窗口合理。
- [ ] 球进门时人物和蓝球同步渐隐；掉入 FallArea 与倒计时归零能失败重开。
- [ ] 失败、手动重来和 Replay 不重播 Intro，直接在最终位置闭夹持球。
- [ ] 暂停/恢复不造成浮力爆冲；Android/iOS 横屏保持目标 60 fps。

## 7. 回滚路径

1. 禁用本关 BuoyancyEffector2D，可独立撤销水体作用力并恢复纯重力。
2. 将 Surface Level/Mask 恢复原值只会恢复旧的错位/全层配置，不需要数据迁移。
3. 若撤销首笔释放，移除 DLevel20Ctrl 并重新指定本关控制器；不要修改或删除共享
   DLevel2Ctrl、DLevel10Ctrl 与共享 Rig。
4. 清空 `successExtraRendererGroup` 会恢复“人物消失但球壳保留”的旧表现，仅用于问题隔离。
5. 本轮水平惯性调参可单独将 Linear Drag 从 `1` 恢复为 `5`；不需修改 Density、Flow
   或共享球 Prefab。
