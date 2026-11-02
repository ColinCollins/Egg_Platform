# DLevel_015 镜像猫夹球入场与释放功能及风险评估

## 1. 需求与背景

- DLevel_015 使用当前统一的猫、Spine 架子和蓝球组合，整套装置从屏幕右侧飞入。
- 关卡编辑时，Alien 根必须直接摆在蓝球最终开始物理运动的位置；屏幕外起点由独立
  `entranceStartPoint` 表示，并在 Scene 视图显示轨迹和起点占位。
- 装置到位前猫播放 `idle01`，架子保持夹球状态；整体有 OutBack 回弹，架子按前冲惯性
  从反方向预倾，再向前并多次衰减摆动，最终回正。
- 玩家完成第一笔有效画线后，猫播放按按钮的 `control`，架子播放 `clip_open`，球真正
  脱离动画父级并获得 Dynamic 物理；同时启动 15 秒倒计时。
- 本关为右侧镜像实例，释放时不能把负缩放直接留在 Dynamic Rigidbody2D 根上，也不能
  让角色视觉或 CircleCollider2D 在脱父级瞬间错位。
- 所有“角色在可释放蓝球中”的关卡都不显示角色影子；夹子和球在入场结束后的静止状态
  必须整体回正。
- 成功时蓝球和球内角色同步渐隐，其它玩法、成功/失败、存档、UI 和配置读取边界保持不变。

## 2. 原行为基线与问题

- DLevel_002 已建立单套装置的完整行为：遮罩后 Intro、`0.78s / OutBack(1.9)`、
  `+38 -> -32 -> +15 -> -7 -> +3 -> 0°` 的衰减摆、首笔开夹放球、15 秒倒计时和
  球/人物同步渐隐。
- DLevel_015 的玩法与上述流程同构，原使用 `scaleX=-1` 的右侧镜像装置；当前整体缩小后为
  `scale=(-0.9,0.9,0.9)`。若直接在保持世界
  位姿脱父级后切 Dynamic，父级反射会折算到球刚体根，可能造成 Rigidbody2D 负缩放、
  内容翻转或碰撞 offset 与画面不一致。
- 猫 `idle01` 中挂点 `bone2` 的静止角为 `+4.22°`。在 Pivot 为零的情况下，
  BoneFollower 会让夹子、球和角色整体保留约 4.22° 的倾斜；镜像后倾斜方向虽然反转，
  但仍不是最终正位。
- 共享 Actor prefab 的 Shadow 默认可见。球内角色属于嵌套 Actor，若不在球装置范围关闭，
  DLevel02、DLevel06 和 DLevel15 会同时出现不符合当前表现要求的影子。
- DLevel10 使用本地旧装置而不是当前共享 Rig，但其球内 Actor 已有 Shadow inactive
  override，无需通过修改通用 Actor prefab 扩大影响。
- DLevel_015 当前 `DrawLineFeature.isConfig=false`、Prefab 墨水为 50；因此 Luban 中的
  15 不参与运行时。该差异属于当前关卡显式配置，本次不静默改成表值。

## 3. 影响范围

- `Assets/Game/Prefabs/DrawLevel/DLevel_015.prefab`
- `Assets/Game/Scripts/Level/DrawLevel/Level15/DLevel15Ctrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level2/DLevel2Ctrl.cs` 的可继承与镜像释放边界
- `Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab`
- DLevel02、DLevel06、DLevel15：继承共享 Shadow inactive 和静止正位补偿
- DLevel10：只确认并保留已有本地 Shadow inactive，不迁移到共享 Rig
- Level15 两份本地说明、DrawGame 中央结构演进记录、已有功能记录

本次未修改通用 `Actor.prefab`、成功门/FallArea 资源、DB、存档、Luban 表、UI、全局事件、
analytics、广告、SDK 或 Android/iOS 平台分支。

## 4. 修改后行为

1. DLevel_015 根使用独立 `DLevel15Ctrl`；该类继承 `DLevel2Ctrl` 且当前不覆写行为，
   既保留关卡身份，又复用 DLevel_002 已验证的单套装置时序。
2. `level15/Alien` 直接放在 `FINAL=(5.41,2.41,0)`，根 `scale=(-0.9,0.9,0.9)`；
   `entranceStartPoint` 对应 `START=(16.39,2.41,0)`。运行时先缓存 START/FINAL，再把根移到
   START，避免移动父级后起点世界坐标漂移。
3. 转场遮罩完全揭开后，猫循环播放 `idle01`，架子播放 `clip_close`，蓝球保持 Kinematic
   并随夹子运动。Alien 用 `0.78s / Ease.OutBack(1.9)` 从右向左飞到 FINAL。
4. `clipSwingPivot` 以缓存静止姿态为基准先叠加 `+38°`，保持 `0.18s`，随后按
   `-32° / +15° / -7° / +3° / 0°` 衰减；对应分段时长
   `0.25 / 0.14 / 0.11 / 0.09 / 0.08s`。Intro 总门禁约 `0.85s`，完整摆动结束后才开放画线。
5. 共享 Rig 的 `clipSwingPivot.localZ=-4.22°` 抵消 `idle01/bone2` 的 `+4.22°` 静止角；
   `yellow_ball.localZ=180°` 与之成对校正球内容方向。Intro 的所有角度都相对该缓存基线
   叠加，结束和 Reset 回到补偿后的正位，不回到错误的零角基线。
6. 共享 Rig 内嵌 Actor 的完整 Shadow GameObject 设为 inactive。DLevel02、DLevel06 两套和
   DLevel15 自动无影子；DLevel10 保留其已有本地 inactive。普通 Actor 关卡不受影响。
7. 第一笔有效画线完成后，DrawLineFeature 已先让有效线条进入物理；DrawLevelCtrl 随后
   同步调用 DLevel15Ctrl 和 DrawLevelCountdownCtrl。架子立即播放 `clip_open`，猫播放一次
   `control` 后回 `idle01`，倒计时只启动一次并从 15 秒开始。
8. 球释放前先保持世界位姿脱离 `BoneFollower/clipSwingPivot`，仍保持 Kinematic 并清零速度。
   对镜像实例，控制器把球刚体根缩放改为正值，将镜像等价转移到
   `payloadContentRoot`，并反向处理 CircleCollider2D 的 X offset；最后才切 Dynamic、WakeUp。
9. 失败、手动重来或胜利 Replay 不再播放 Intro；新实例在黑幕下直接把镜像装置放到 FINAL，
   恢复球的原父级、局部位置/旋转/缩放、Collider offset、payload 位姿、夹子静止补偿、
   `clip_close`、Kinematic 球和猫 `idle01`。
10. Actor 子碰撞体进入成功门后，`DrawLevelCtrl.successExtraRendererGroup` 与人物渐隐组使用
    同一个 alpha；蓝球壳与角色同步渐隐，之后沿既有链提交成功。FallArea 或倒计时归零
    继续触发失败重建。
11. 画线仍使用 Prefab 的 `maxInkLength=50`。由于 `isConfig=false`，Luban 的 15 被明确忽略；
    本次未修改该口径、允许多笔、线宽、质量或其它玩家画线参数。

## 5. 关键流程

```text
DLevel_015 实例化
  ├─ 缓存 FINAL (5.41, 2.41) / START (16.39, 2.41)
  ├─ Alien scale = (-0.9, 0.9, 0.9)
  ├─ 猫 idle01 / 架子 clip_close
  ├─ 球 Kinematic + 清速度 + 挂在夹子下
  └─ 球内 Actor Shadow inactive

转场遮罩完全揭开
  -> Alien 从右侧 START 飞到 FINAL
  -> 0.78s / OutBack(1.9)
  -> Pivot 相对静止补偿做 +38 / -32 / +15 / -7 / +3 / 0° 衰减摆
  -> 回到 -4.22° 缓存基线
  -> 开放画线

第一笔有效画线完成
  -> 有效线条先进入 Dynamic 物理
  -> clip_open + 猫 control
  -> 15s 倒计时启动
  -> 球保持世界位姿脱父级
  -> 球根负缩放迁移到 payloadContentRoot
  -> CircleCollider2D offset 同步镜像
  -> 球根正缩放 + Dynamic + WakeUp

Actor 子碰撞体进入成功门
  -> Actor 与 yellow_ball 同步渐隐
  -> 既有 Draw 成功结算
```

## 6. 风险项

| 风险 | 等级 | 控制与现状 |
| --- | --- | --- |
| DLevel15 继续直接挂 DLevel2Ctrl，关卡身份混淆 | 中 | 新增薄子类 DLevel15Ctrl；继承稳定实现但保留独立类型与文档边界 |
| 为减少复制而把 DLevel15 单关坐标/倒计时写进共享 Rig | 高 | FINAL、START、控制器和倒计时仍保留在 DLevel15 prefab；共享 Rig 只保存结构与共享表现基线 |
| 根移动后才读取子起点，导致 START 跟着 FINAL 漂移 | 高 | Awake/Reset 前一次性缓存世界 START/FINAL；运行时 Tween 只使用缓存值 |
| 右侧镜像实例仍按左侧方向硬编码夹子角度 | 中 | 摆动施加在镜像根下的局部 Pivot，画面方向随根镜像；参数仍使用与 DLevel2 相同的幅度序列 |
| Dynamic Rigidbody2D 根保留负缩放 | 高 | 切 Dynamic 前把反射迁移到 payloadContentRoot，球根缩放转正 |
| 只转正球根导致角色或球壳突然反向 | 高 | 同步向 payloadContentRoot 应用等价镜像旋转，并保持脱父级时世界位姿 |
| CircleCollider2D offset 未随镜像迁移 | 高 | 同步反向 X offset；Reset 恢复缓存 offset |
| `bone2` 静止角让夹子/球到位后仍歪 | 高 | 当前资源按 `+4.22°` 测得，共享 Pivot 用 `-4.22°` 抵消，球角度成对设为 `180°` |
| 未来猫 Spine 更新后固定补偿失效 | 中 | 把补偿记录为当前资源基线；资源更新时重新测量 bone2，不继续叠加经验角度 |
| 只改 Pivot 不校正球，导致夹子正而球内容歪 | 高 | Pivot `-4.22°` 与 ball `180°` 作为一组提交和回滚 |
| 修改通用 Actor prefab 导致所有角色无影子 | 高 | 只关闭共享球 Rig 内的嵌套 Actor Shadow；DLevel10 使用本地 override |
| 共享 Shadow 修改遗漏某个带球关卡 | 中 | 范围扫描确认球内角色关卡为 DLevel02、DLevel06、DLevel10、DLevel15；前三者由共享/本地配置覆盖，DLevel06 两实例都继承共享值 |
| 架子动画完成后才切物理造成释放延迟 | 中 | `clip_open` 与实际脱父级/Dynamic 在同一首笔回调触发；视觉动画不作为物理解锁门禁 |
| 第一笔无效点击也启动倒计时或放球 | 高 | 继续依赖 DrawStrokeResult 的有效笔画和 `IsFirstStroke`；取消/过短笔画不派发有效完成 |
| 成功时人物消失但球仍留在画面 | 高 | yellow_ball 的 SpriteRendererGroupAlpha 显式绑定 successExtraRendererGroup，与 Actor 使用同一 alpha |
| 成功门动画期间 15 秒倒计时抢先失败 | 中 | 属于既有 Draw 成功提交时序，当前未扩大修改；必须在 15 秒临界点 PlayMode 回归 |
| 策划以为 Luban 15 生效，实际可画 50 | 中 | 文档和 Prefab 明确 `isConfig=false / maxInkLength=50`；若要统一必须另立配置需求 |
| OutBack 与大角度摆动在不同帧率下手感不同 | 中 | 使用 DOTween 时长与绝对角峰值，逻辑不依赖 Update 累加；移动端仍需目视和物理验收 |
| 共享基线修改误伤 DLevel02/DLevel06 | 高 | Shadow 与静止角明确是三关共享表现；DLevel02、DLevel06、DLevel15 必须成组验证和成组回滚 |
| 根缩小后 START 跟着缩近 | 高 | 共享起点 `-10.82→-12.022222`，DLevel15 `-10.98→-12.2`，世界起点和飞行距离不变 |
| 只把镜像 X 保留为 `-1` 导致横向拉宽 | 高 | DLevel06 右侧与 DLevel15 统一为 `(-0.9,0.9,0.9)` |
| 整体缩放后物理接触范围变小 | 中 | CircleCollider2D 与 Actor 复合碰撞体随整体缩小 10%；质量不变，待 PlayMode 验收落球与成功门 |

## 7. 验证计划与当前证据

- 静态已核对：DLevel15Ctrl 继承 DLevel2Ctrl；DLevel_015 根唯一绑定该本关控制器；
  Alien 根为 `FINAL=(5.41,2.41,0)`、`scale=(-0.9,0.9,0.9)`，起点世界坐标为
  `START=(16.39,2.41,0)`。
- 静态已核对：入场参数 `0 / 0.78 / 1.9`，架子摆动参数
  `+38 / -32 / +15 / -7 / +3 / 0°` 与对应时长完整；首笔倒计时 `15s`。
- 静态已核对：共享 Pivot `-4.22°`、ball `180°`，Actor Shadow inactive；
  DLevel02/DLevel06/DLevel15 引用同一共享 Rig，DLevel10 保持本地 Shadow inactive。
- 静态已核对：镜像释放在球切 Dynamic 前执行，球根转为正缩放、payload 承接镜像、
  CircleCollider2D offset 同步，Reset 恢复缓存状态；球额外渐隐组绑定成功链。
- 静态已核对：`DrawLineFeature.isConfig=false / maxInkLength=50`，因此当前不读取 Luban 15。
- PlayMode 待测：右侧装置从 START 飞入、OutBack 速度、夹子完整衰减摆、最终正位、
  第一笔开夹与实际放球同帧、球/Actor/Collider 无镜像跳变、无影子、15 秒失败、成功同步
  渐隐、15 秒临界成功竞争、Reset/Replay 恢复。
- 设备待测：Android/iOS 代表性横屏比例、60/30 fps、触控首笔、出屏起点、Spine 排序、
  Dynamic 球碰撞和切后台恢复。以上 PlayMode 与移动端结果均为 `uncertain`。

## 8. 回滚

1. 若回滚 DLevel15 的独立身份，从 DLevel_015 移除 DLevel15Ctrl，并恢复任务前控制器绑定；
   不删除仍被 DLevel02/DLevel06 使用的 DLevel2Ctrl 或共享 Rig。
2. 若回滚本关入场，成组恢复 Alien 根位置/镜像、`entranceStartPoint`、控制器 START/FINAL
   缓存方式与 Intro 参数；只移根或只删起点会造成首帧、Reset 或 Tween 目标错误。
3. 若回滚镜像物理修复，必须一起恢复球根缩放、payload 旋转、CircleCollider2D offset 和
   Reset 缓存逻辑；不允许只移除其中一项。
4. 若回滚静止正位，必须把共享 `clipSwingPivot` 与 `yellow_ball` 角度成对恢复任务前值；
   该操作会同时影响 DLevel02、DLevel06、DLevel15，应三关一起回归。
5. 若回滚无影子，把共享 Rig 内 Actor/Shadow 恢复 active，并单独确认 DLevel10 本地 override；
   不修改通用 Actor prefab。
6. 若只回滚成功同步渐隐，清空本关 `successExtraRendererGroup` 的球绑定；这会恢复人物淡出、
   球壳保留的旧表现，不影响物理或结算。
7. 若要把墨水恢复为 Luban 15，应另行把 `isConfig` 切为 true 并验证表 ID/读取链；这不是
   本次表现修正的安全回滚项。
8. DB、存档、Luban 表、UI、全局事件、analytics、广告、SDK 和平台代码未修改，无需数据迁移。
9. 若回滚 0.9 整体缩放，必须成组恢复共享根 `(1,1,1)`、DLevel06/DLevel15 镜像 X `-1`、
   共享起点 `-10.82` 和 DLevel15 起点 `-10.98`，否则会改变轨迹或产生非等比缩放。

## 9. 2026-08-02 球内 Actor 多边形碰撞移除

- 镜像球内 Actor、ActorCtrl、Spine、成功门身份与球/人物同步渐隐均保留；共享实例仅移除
  Actor 自带 PolygonCollider2D，不改通用 Actor prefab。
- Actor 自带 Rigidbody2D 仍保持既有移除状态；镜像释放继续把负缩放迁移到 payload，物理由
  yellow_ball 根 Rigidbody2D 统一模拟。球根 CircleCollider2D 与 Actor 既有子 BoxCollider2D
  保留，只移除角色轮廓 PolygonCollider2D。
- 需 PlayMode 回归镜像落球、成功门、15 秒竞争和 Reset/Replay，当前为 `uncertain`。回滚只应
  恢复 PolygonCollider2D override，不恢复 Actor Rigidbody2D。
