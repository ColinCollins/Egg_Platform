# Level113 彩色钥匙玩法风险评估

## 边界

本次变更主要作用于 Level113 Prefab、Level113 私有脚本和已经指定替换的 Box Spine 资源。共享 `ItemJumpOutExecutor` 只新增固定起终点的 `PlayBetween` 入口，既有 `Play()` 实现和其它关卡调用不变。没有修改共享 `ActorCtrl`、共享 `GamePlayPanel_000`、DoorLock/door_0 Prefab、配置表、本地化、存档、RemoteConfig、全局事件或 SDK。

## 风险清单

| 风险 | 控制措施 | 当前状态 |
| --- | --- | --- |
| 删除黄色对象后旧数组空引用导致中断 | Level113 实例禁用 `Level18Ctrl`，新链按节点名建立并在缺失时明确报错停用 | 静态通过 |
| 颜色链与资源编号混淆 | 固定映射 `05红 -> 02蓝 -> 04橙 -> 03绿 -> 06粉`，箱链固定红/蓝/橙/绿 | 待 PlayMode |
| 新旧触发器同时执行 | 钥匙关闭旧状态/碰撞自动入口，箱子和 DoorLock 关闭旧 `StateTriggerDisplayHandle`；仅由 Level113 显式调用原表现组件 | 静态通过 |
| 拖动钥匙被 HUD 遮挡 | 拖动副本使用 Overlay 排序 32760；安装后 jump_btn 和图标置末层 | 待多分辨率 |
| 多指抢夺或松手错配 | 每把钥匙保存独立 fingerId；已进入流程后全局锁定其它钥匙 | 待真机 |
| obtain/hit 被走路动画覆盖，或完成后仍卡 hang | Prefab 绑定 `Level113ActorCtrl : ActorCtrl`，仅在一次性动作期间托管 Spine；完成后主动恢复通用动画，钥匙由 point_hit 独立跟随 | 编译通过，待目视 |
| 开箱期间误锁角色 | `OpenChest` 不再调用 `SetCtrlable(false)` 或 `StopMoving()`；只用 Level113 私有操作锁防止重复箱钥匙交互 | 静态通过，待 PlayMode |
| 箱子/闪光/钥匙表现偏离原关 | 复用箱实例原排序、缩放、飞行组件；`box-open` 后保留原 0.5 秒等待及 Shiny/激活顺序；新钥匙复用自身 ItemJumpOut 参数与曲线 | 静态通过，待四箱目视 |
| 误把拖到 Actor 当作装备入口 | 装备只接受 `Root/jump_btn` 命中；拖到 Actor 按无效投放恢复，普通碰撞才走背后跟随 | 编译通过，待 PlayMode |
| 靠近拾取速度偏离原关 | 不再手写跟随 Tween，直接执行钥匙 Prefab 原 `TriggerExecutor`，复用 0.3 秒 Wait、排序 151、FollowTarget 15/0.7 | 原组件静态确认，待目视 |
| 橙色钥匙接触后不跟随 | 从四把后续钥匙的拾取执行列表移除遗留 `Level18Ctrl.NextKey` 事件节点；Level113 自身状态机已承担颜色推进，其余原表现节点不变 | 日志根因确认，待 PlayMode |
| 挂点错误且点击无动画 | Level113 Actor 单关覆盖到含 `point_hit/obtain/hang/hit` 的 `sp_box_025` SkeletonData；安装时直接挂点出现，`obtain` 完成后才开放按钮 | 资源与编译静态通过，待 PlayMode |
| 道具按钮缺少点击缩放 | 共享跳跃 Click 继续关闭，但本关按 fingerId/鼠标状态直接驱动按钮原 `ClickScaleAnim`，释放检测不受 hit 操作锁影响 | 编译通过，待 PlayMode |
| 门锁命中无打击特效 | 复用 Level113 已有 `Knock_Explosion`，按 0.18 秒命中时机移动到 DoorLock 后清空并重播 | Prefab/代码静态通过，待目视 |
| 解锁完成钥匙提前消失 | 不再隐藏 equippedKey，也不在解锁完成时清除 Actor 持有状态；只隐藏锁并打开出口 | 静态通过，待 PlayMode |
| 持钥匙被 Order 1000 强制压住所有场景物体 | 装备时恢复钥匙缓存的原 Order（红钥匙为 Stuff/201），仅比 Actor SortingGroup 200 高一层 | 静态通过，待交叠目视 |
| 命中特效出现英文 Knock | Level113 Prefab 关闭使用英文纹理的 `Knock 1` 子节点，代码只播放使用星形纹理的根粒子且不递归子节点 | 纹理/Prefab 静态通过，待目视 |
| 新增共享 ItemJumpOut 入口影响其它关卡 | 原 `Play()` 仍走同一随机分支；`PlayBetween` 是纯新增入口且仅 Level113 调用 | 编译通过，调用扫描通过 |
| 挂点名称歧义 | 以 Box JSON 与美术说明共同存在的 `point_hit` 为准；缺失时仅降级到 Actor 根并报警 | 资源静态通过 |
| DoorLock 回调丢失导致永久卡住 | hit/unlock 同步等待，3.5 秒超时后仍放行出口 | 待 PlayMode |
| 提前通关或打点分叉 | 锁未开时出口 Collider 关闭；开锁后只恢复既有门触发，不直接调用成功/存档/统计 | 静态通过 |
| 退出后公共跳跃按钮残留钥匙图标 | OnDisable/OnDestroy 恢复按钮全部缓存状态 | 待 Reset/Replay |

## 重点验收顺序

1. 红钥匙碰角色后只跟随一次，只有红箱可开。
2. 逐一确认蓝、橙、绿、粉钥匙都在 `box-open` 后按原 0.5 秒时序、原 Shiny 和原 ItemJumpOut 曲线出现，仅落点改到原红钥匙位置。
3. 每个阶段把台面钥匙拖到按钮，确认进入“替换图标 -> 钥匙直接在 `point_hit` 出现 -> `obtain -> idle/walk`”；再拖到 Actor 和其它位置，确认均按无效投放原地恢复。
4. obtain 完成前不能移动，完成后恢复正常 idle/walk/jump 且钥匙稳定在 `point_hit`。
5. 远处点按钮确认按钮有缩小回弹且只播放 hit；门锁旁点击时确认 hit/unlock 同时播放、Knock_Explosion 出现在锁上，解锁后钥匙仍在手上并可进入标准结算。
6. 暂停、Reset、Replay、退出关卡后跳跃按钮图标和交互恢复。
7. 蓝、橙、绿、粉钥匙逐把接触角色，确认无 `Level18Ctrl.NextKey` 异常，且均进入各自原 `FollowTarget` 跟随。

## 2026-09-07 修复验证

- Roslyn 完整响应文件编译：`exit 0 / 0 error`，仅保留工程既有 warning。
- Unity Editor 已成功重载程序集并重新导入 `Level_113.prefab`。
- `uncertain`：仍需 PlayMode 连续跑完四个箱子，逐帧目视对比 Level018 的旧钥匙入箱、openChest/box-open、0.5 秒等待、Shiny、新钥匙 ItemJumpOut 顺序，并复测普通靠近的原背后跟随、UI 按钮装备链和拖到 Actor 的无效恢复。
- 2026-09-07 橙色钥匙接触修复：Editor.log 明确记录 `EventHandleListener.Execute -> Level18Ctrl.NextKey` 因旧 `Keys` 未赋值抛出 `UnassignedReferenceException`；Level113 Prefab 已从四把后续钥匙的拾取列表移除该非表现事件，避免橙钥匙在启用 `FollowTarget` 前中断。静态引用和完整编译已复核，PlayMode 连续接触仍为 `uncertain`。
- 2026-09-07 挂点与点击修复：确认共享 Actor 原 SkeletonData GUID 指向 `sp_Box_001`，Level113 现以 Prefab Override 指向 `sp_box_025/Box_SkeletonData`；新 JSON 含 `point_hit` 和 `obtain/hang/hit`，代码已删除按钮到角色的飞行与 Tween 状态，改为直接挂点后播放 `obtain`。完整 Roslyn 编译 `exit 0 / 0 error`，Prefab 引用闭合；实际挂点姿态与点击 `hit` 仍需 PlayMode 目视确认。
- 2026-09-07 持钥匙移动/按钮/命中特效修复：一次性 `obtain/hit` 完成后恢复通用动画；按钮原 `ClickScaleAnim` 由本关输入接管并保证释放回弹；成功 hit 延迟 0.18 秒在 DoorLock 位置重播已有 `Knock_Explosion`；解锁收尾不再隐藏钥匙。完整 Roslyn 编译 `exit 0 / 0 error`，实际动画衔接、粒子落点和手持进门仍为 `uncertain`。
- 2026-09-07 层级/纯击打爆点调整：装备钥匙恢复原 Stuff/201，不再写 Order 1000；核对粒子纹理后在 Level113 Prefab 关闭英文 `Knock 1` 子节点，只播星形根粒子，并将根粒子放大 1.35 倍、相对门锁左上偏移 `(-0.35,+0.25)`。实际交叠与粒子尺寸仍为 `uncertain`。

## 2026-10-10：共享 HUD 背景拆分后的图标修复

- 原行为与风险：装备钥匙原本只应替换 Jump 箭头，但控制器取首个非根 Image；现有 `bg` 在箭头前，导致背景被当作图标修改。用户实测反馈与源码/Prefab 选择路径一致。
- 最小修复：仅 `Level113Ctrl.TryResolveJumpButton()` 改用已绑定的 `GamePlayPanel.JumpButtonIcon`（基础 Image `5393492493011141298`），不改共享 HUD、Prefab、配置、输入、角色、钥匙/出口或 GF/Packages。原缓存和恢复链继续使用该引用。
- 证据：基础绑定、派生面板无覆盖及 113 使用 `GamePlayPanel_000` 已核对；三关合计 12 项静态断言通过。Unity Roslyn 当前主程序集编译 0 error / 113 warning，本关修改脚本无诊断；日志 `Temp/level-hud-icon-review-20261010/compile.log`。
- 未验证风险（`uncertain`）：真实钥匙替换后的尺寸/层级/按压、完整颜色链、暂停、重开和退出恢复；PlayMode、Android/iOS、IL2CPP 尚未执行。编译和静态定位不能关闭这些运行验收项，9 月证据不能证明当前共享结构通过。
- 验收与回滚：本地序列 124（内容 10113）逐色装备后必须保留背景、只替换箭头并正常击打/解锁，重开恢复默认箭头。失败时记录截图与输入/生命周期路径；仅按当日功能说明选择性回滚定位代码，恢复旧遍历会重新引入背景误替换。
