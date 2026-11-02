# Level088 风险评估

## 2026-08-18 行为等价精简风险复核

- **事实**：本轮只删除 `SetFirstBurstCount()` 内的重复空判断；唯一调用源为 `GetComponentsInChildren<ParticleSystem>()` 返回的当前遍历元素。
- **直接结论**：有效组件输入下，Burst 检查、数量修改和粒子播放路径与原实现一致；无资源、事件、存档、打点或生命周期变化。
- **假设**：未把 Unity 组件枚举产生空元素当作正常运行合同；当前本地代码和 Prefab 未提供这种证据。
- **缺失证据**：尚未执行 Unity PlayMode、Android/iOS 真机粒子观感回归，标记为 `uncertain`。
- **回滚**：恢复该私有方法的空引用判断；其余关卡实现无需回滚。

更新时间：2026-08-10

## 影响边界

关卡逻辑位于 Level088 私有控制器和 Prefab 接线。Level004、Level075、共享 `KnockKnock`、`BaseLevelCtrl`、Actor、`door_0` 与两个粒子 Prefab 均只复用、不修改。共享 `SuccessAnimCtrl` 增加一个默认关闭的音效抑制开关，只有 Level088 的 Prefab 实例覆盖为开启，因此其他关卡保持原行为。

## 风险项与控制

| 风险 | 当前控制 | 验证状态 |
| --- | --- | --- |
| 30 次敲击因停顿被清零 | 私有控制器不使用 Level004 的 `TimeLimit`，计数只随关卡实例重建而重置 | 待 PlayMode |
| UI 点击或非游玩阶段误计数 | 每次输入检查 UI 命中和 `PlayCtrl` 实际 `PLAYING` 状态 | 代码静态检查 |
| 多指同帧漏计或一次增加多次 | 遍历同帧 Began 触点直至命中敲门区，命中后立即结束，因此每帧最多累计一次 | 代码静态检查，真机待验 |
| 敲门粒子看不到或停留在旧坐标 | 每次有效敲击保存屏幕坐标，转换到世界坐标后移动 `Knock_Explosion`，先 Clear 再递归 Play | Prefab 接线及代码静态通过，画面待验 |
| 阶段顺序错误或跳过 | 阈值固定递增为 `15 < 20 < 30`，四张 Sprite 按 01 到 04 接线 | 静态通过，PlayMode 待验 |
| 前两次碎片仍过密或过少 | 激活共享 Prefab 前把主 Burst 覆盖为 4、5，并禁用 `Clouds` | 待画面验收 |
| 最终破碎力度不足 | 第三次主 Burst 为 18，同时保持 Level075 的四向速度和尺寸并禁用 `Clouds` | 待画面验收 |
| 烟尘仍从复用特效出现 | 遍历运行时粒子实例并禁用名为 `Clouds` 的子 GameObject，脚本与 Prefab 已移除所有烟尘数量字段 | 代码静态检查，PlayMode 待验 |
| 共享粒子以后重命名导致烟尘重新出现 | 当前去烟尘依赖子节点精确名称 `Clouds`；若 Level075 资源结构变更，需同步本关筛选条件 | 资源未改时静态通过，后续资源变更需回归 |
| 修改共享粒子影响 Level075 | 只修改每个运行时实例的 Burst，不写共享 Prefab | 代码静态检查 |
| 完全破碎前可以进门 | Awake 强制关闭成功 Collider，仅第 30 次启用 | 待 PlayMode |
| 破洞后仍显示旧门 | Level088 Prefab 已单独禁用 door_0 的旧 `door` 显示节点，L88 Sprite 独立显示 | Prefab 静态通过，画面待验 |
| 最终无法吸入过关 | 保留原成功 Collider、OnTrigger 回调、SuccessAnim 和 ActorDestroy 引用 | 静态闭合，PlayMode 待验 |
| 其他关卡意外没有关门音效 | `suppressCloseDoorSfx` 默认值为 false，Prefab override 只写在 Level088 的 `door_0/SuccessAnimCtrl` 实例 | 代码与 YAML 静态通过，其他关卡回归待验 |
| Level088 仍播放关门音效 | Level088 override 将 `suppressCloseDoorSfx` 设为 true，成功动画仍保留其他音效与回调 | YAML 静态通过，PlayMode 待验 |
| 误删开门或进门音效 | 条件判断只包裹成功动画末尾的 `AudioManager.PlaySound("closeDoor")`，`openDoor` 与 `enterDoor` 调用未改 | 代码静态检查，听感待验 |
| 粒子实例残留 | 复用 Prefab 根 ParticleSystem 的 StopAction Destroy，关卡销毁时子实例也随父节点清理 | 静态通过，运行待验 |
| 正常选关入口不可达 | 当前生成配置无 `10088 / Level_088`，本次不擅自选择排序号或生成配置 | 待策划配置 |
| 提示文案仍是 Level004 | Prefab 当前保留 `S_LevelTips_Des_level_04`，没有推断新的 Level088 提示/答案/通关 key | 待策划文案 |

## 未验证缺口

- 当前生成配置没有 Level088 正常入口，Tips 仍沿用 Level004；源 Excel 的有效 Level088 行与独立文案尚未确认。
- Unity PlayMode 中连续、间断和快速敲击 30 次的阈值与手感。
- 四张门图切换时的位置、比例、透明区域和底层旧门遮挡。
- 每次敲门粒子的点击位置、排序层和连续快速点击时的重播表现。
- 三次纯碎片粒子密度、排序层、音效组合和最终破碎力度。
- 第 30 次后 Actor 穿过破洞、吸入动画、成功回调、失败重开和手动重开。
- Level088 通关时是否不再听到关门音，以及任一既有关卡的关门音是否仍保留。
- Android/iOS 横屏触摸、多指、UI 排除、切后台与性能。
- 阈值调整为 `15 / 20 / 30` 后已用当前 Unity Roslyn 响应文件复编为 `0 error`；完整 PlayMode 行为仍待验证。

## 回滚路径

回滚 Level088 根组件、Prefab 接线和新增私有脚本/文档即可恢复复制关卡状态。若要完整撤回本轮关门音处理，再删除 `SuccessAnimCtrl.suppressCloseDoorSfx` 及其条件判断；共享 Level004、Level075、两个粒子 Prefab 与其余门成功链无需回滚。
