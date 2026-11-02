# HorizontalRandomAdRewardCtrl 说明

## 概述

`HorizontalRandomAdRewardCtrl` 挂在胜利界面的奖励选择区域，负责控制 `arrow` 在水平圆弧上往返摆动，并根据当前停留档位计算最终奖励倍率。

核心行为：

- `Play()` 启动持续摆动
- `Stop(true)` 平滑吸附到当前奖励档位中心后结束
- `Stop(false)` 立即中断
- 支持根据当前位置读取当前奖励倍率和最终奖励值

## 主要 API

| 接口 | 说明 |
| --- | --- |
| `Play()` | 启动摆动循环 |
| `Stop(bool snapToRewardBand = true)` | 停止摆动；默认平滑停靠到当前奖励带中心 |
| `ResetArrow()` | 立即回到中心位置 |
| `PreserveCurrentStateOnNextEnable()` | 下次重新启用时不要自动回中，保留当前位置 |
| `IsPlaying` | 当前是否处于播放中 |
| `CurrentRewardTier` | 当前奖励档位索引 |
| `CurrentRewardLevel` | 当前奖励等级，从 `1` 开始 |
| `GetRewardMultiplier()` | 获取当前奖励倍率 |
| `GetReward(int baseReward)` | 根据基础奖励计算最终奖励 |
| `GetCurrentNormalizedPosition()` | 获取当前归一化位置，范围 `[-1, 1]` |

## 运动模型

- 指针围绕由 `swingCenterOffset` 定义的圆心做圆弧摆动
- 摆动范围由 `rotateAngle` 控制
- 播放时通过 `Mathf.Sin(phase)` 产生持续往返运动
- 视觉表现会经过 `motionSmoothness` 平滑处理

## 奖励档位

- 归一化位置范围为 `[-1, 1]`
- 越接近 `0`，奖励档位越高
- 当前档位通过 `CalculateRewardTier()` 计算
- `GetBandCenterNormalized()` 会求出当前所处奖励带的中心点

## Stop 行为

### 平滑停止

`Stop(true)` 的流程：

1. 设置 `_stopRequested = true`
2. 记录目标奖励带中心 `_stopTargetNormalized`
3. `Play()` 主循环退出
4. 进入 `TryAnimateStopToRewardBand()`
5. 使用 `stopDuration + settleCurve` 将指针平滑吸附到目标带中心
6. 完成后结束等待中的 `Stop()` 调用

### 立即停止

`Stop(false)` 会：

- 直接递增 `_playVersion`
- 清空播放状态
- 立即结束等待任务

不会执行平滑吸附动画。

## 为什么 OnDisable 会停止播放

这是组件的刻意设计。

`OnDisable()` 中会同步执行以下清理：

- `StopRewardTextFade()`
- `_playVersion++`
- `_isPlaying = false`
- `_stopRequested = false`
- `_stopTcs?.TrySetResult()`
- `_stopTcs = null`

目的有两个：

1. 组件失活后，不允许继续保留播放循环
2. 如果外部此时正在 `await Stop()`，必须立即结束等待，避免挂住

另外，`Play()` 的循环条件本身也要求组件处于 `isActiveAndEnabled` 状态，因此对象失活后，循环本来就不应该继续执行。

## OnDisable 后的恢复策略

现在的策略不是“禁用时继续播放”，而是：

- `OnDisable` 时正常同步停机
- 但可以在停机前记录“下次启用保留当前位置”
- `OnEnable` 时跳过 `ResetArrow()`
- 之后由外层再次调用 `Play()`，从当前归一化位置继续摆动

这部分通过 `PreserveCurrentStateOnNextEnable()` 配合 `OnEnable()` 中的 `_skipResetOnNextEnable` 实现。

## 与 GameVictoryPanel 的配合

`GameVictoryPanel` 现在不再依赖 `MonoBehaviour.OnDisable/OnEnable` 做恢复，而是改成显式跟随 UI 框架的 `OnHide/OnShow` 生命周期。

### 原因

- `OnDisable()` 的触发顺序受父子节点失活顺序影响，不适合承载页面级恢复决策
- `GameVictoryPanel` 被 `ShopPanel` 临时盖住时，本质上是 UI 栈里的 `OnHide -> OnShow` 过程
- 因此恢复逻辑应该绑定到 UI 面板生命周期，而不是绑定到 Unity 组件禁用时机

### 当前流程

1. `PlayRewardSelect()` 启动摆动时，同时把 `GameVictoryPanel._isRewardSelectorActive` 设为 `true`
2. 开始奖励结算流程时，把 `_isRewardSelectorActive` 置为 `false`
3. 如果面板在奖励选择阶段被临时隐藏，`GameVictoryPanel.OnHide()` 会：
   - 调用 `adRewardCtrl.PreserveCurrentStateOnNextEnable()`
   - 记录 `_resumeRewardSelectOnEnable = true`
4. 面板重新显示时，`GameVictoryPanel.OnShow()` 会调用 `PlayRewardSelect()`

效果是：

- 被其他 UI 临时盖住后回来，指针会从上次位置继续摆动
- 真正关闭胜利面板时，不会保留这次恢复标记

## Reward 文本

组件会自动刷新奖励文本，并在启用时重启淡入动画：

- `RefreshRewardTexts()`
- `RestartRewardTextFade()`

编辑器非运行态下：

- 不访问运行时 `RemoteConfig`
- 使用本地预览值 `{1, 2, 3, 4}`

## Inspector 字段

| 字段 | 说明 |
| --- | --- |
| `arrow` | 指针 `RectTransform` |
| `swingCenterOffset` | 圆心偏移 |
| `loopFrequency` | 摆动频率 |
| `ignoreTimeScale` | 是否忽略时间缩放 |
| `rotateAngle` | 最大摆动角度 |
| `resetToCenterOnEnable` | 启用时是否默认回中 |
| `motionSmoothness` | 平滑系数 |
| `stopDuration` | 停止吸附时长 |
| `settleCurve` | 停止吸附曲线 |
| `rewardTxts` | 奖励文本数组 |
| `rewardBandBoundaryAngles` | 从中心向外递增的奖励分界角度数组；数量应为奖励档位数减一 |
| `rewardTextFadeDelay` | 文本淡入延迟 |
| `rewardTextFadeDuration` | 文本淡入时长 |

## 编辑器约束

- `OnValidate()` 只做本地字段限制和文本刷新
- 编辑器预览不访问运行时配置
- `OnDrawGizmosSelected()` 只负责摆动轨迹和奖励带可视化

## 文件位置

- 脚本：[Assets/Game/UI/GameVictory/Scripts/HorizontalRandomAdRewardCtrl.cs](D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/GameVictory/Scripts/HorizontalRandomAdRewardCtrl.cs)
- 面板配合逻辑：[Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs](D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs)
- 文档：[DevelopMD/HorizontalRandomAdRewardCtrl.md](D:/GitStore/BrinPunk/BrainPunk/DevelopMD/HorizontalRandomAdRewardCtrl.md)

## 2026-08-25 奖励边界精度与 Gizmos

### 需求与原有行为基线

原实现按 `[-1, 1]` 归一化范围等分奖励档位。当前胜利界面指针最大摆角为 `14°`，四档等分边界为 `3.5° / 7° / 10.5°`；Prefab 奖励文字的视觉角度并非等间距，因此指针接近部分文字时，视觉指向与实际奖励档位可能不一致。

本次需求是把奖励分界精度参数暴露到 Inspector，并让 Scene Gizmos 显示运行时实际采用的档位边界。

### 影响文件与系统

- `Assets/Game/UI/GameVictory/Scripts/HorizontalRandomAdRewardCtrl.cs`
- `Assets/Game/UI/GameVictory/Resources/GameVictoryPanel_Reward.prefab`
- `Assets/Game/UI/DrawGame/VictoryPanel/Resources/DrawGame_GameVictoryPanel_Reward.prefab`
- `Assets/Game/UI/IWannerGame/VictoryPanel/Resources/IWannerGame_GameVictoryPanel_Reward.prefab`
- 影响主线、DrawGame、IWanner 三套胜利奖励选择界面的指针档位判定与编辑器预览。
- 不修改奖励广告请求、RemoteConfig 奖励值读取、背包发奖、存档、资源加载、全局事件、分析打点或平台 SDK 流程。

### 修改后行为

- 新增 Inspector 字段 `rewardBandBoundaryAngles`，单位为度，表示从中心向外递增的档位边界。
- 当前三套 Prefab 默认值统一为 `1.5° / 4.5° / 8°`，对应四档奖励区间：
  - `0° <= abs(angle) < 1.5°`：最高档，当前默认奖励值为 `90`。
  - `1.5° <= abs(angle) < 4.5°`：次高档，当前默认奖励值为 `50`。
  - `4.5° <= abs(angle) < 8°`：第三档，当前默认奖励值为 `40`。
  - `8° <= abs(angle) <= rotateAngle`：最低档，当前默认奖励值为 `30`。
- 数组长度必须等于奖励档位数减一，角度必须严格递增并小于 `rotateAngle`；配置无效时，运行时判定、停止吸附和 Gizmos 会一起回退到原等分规则。
- `OnValidate()` 只对本地序列化角度执行限制和排序，不访问 `DB`、运行时 RemoteConfig、Manager 单例或实例化链。
- `CalculateRewardTier()` 与 `GetBandCenterNormalized()` 共用同一套角度边界，保证实时发奖判定与 `Stop(true)` 吸附位置一致。
- 选中组件时，Scene Gizmos 会显示：
  - 按档位颜色分段的完整摆动弧线，低档偏红、高档偏绿。
  - 橙色左右边界射线、边界球和右侧角度标签。
  - 各档位中心球和 `T1...Tn` 标签。
  - 配置无效时，在圆心显示正在使用等分回退的英文提示。

### 数据与发奖链

奖励数值仍由 `ConfigManager.RemoteConfig.GetVictoryHorizantalRewards()` 提供，缺失时沿用既有本地回退。指针点击停止后仍由胜利面板读取 `GetCurrentReward()`，广告成功后使用既有 `Bag.AddTool(..., RewardType.Ad)` 发放金币。本次只改变“当前指针角度属于哪个奖励档位”，没有新增或修改存档字段、资源地址、事件或打点。

### 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo`：完整首轮通过，`0` 个错误、项目原有 `1124` 个警告；最终增量复验通过，`0` 个错误、`106` 个警告。
- 静态序列化检查：三套 Prefab 均包含 `1.5 / 4.5 / 8` 三个边界值。
- 边界映射检查通过：`0° -> 90`、`1.49° -> 90`、`1.5° -> 50`、`4.49° -> 50`、`4.5° -> 40`、`7.99° -> 40`、`8° -> 30`、`14° -> 30`。
- `OnValidate` 静态扫描未发现 `DB`、`RemoteConfig`、`Instance` 或 `Instantiate` 访问。
- `git diff --check` 通过，仅输出工作区换行符转换提示。

### 剩余风险与未验证项

- 尚未在 Unity Scene 视图中人工确认 Gizmos 标签在三套 Canvas 缩放下的最终可读性。
- 尚未执行三套胜利界面的 PlayMode 广告成功/失败回调与边界点击手工验证。
- 尚未执行 Android/iOS 横屏设备验证；本次没有新增平台分支，但触控停点体验仍需设备烟测。
- 默认边界根据当前 UI 视觉布局设定；后续移动奖励文字或修改 `rotateAngle` 时，应在 Scene Gizmos 中同步调整边界数组。

### 回滚方式

删除三套 Prefab 的 `rewardBandBoundaryAngles` 序列化数据，移除脚本中的自定义边界字段与 Gizmos 边界显示，并将 `CalculateRewardTier()`、`GetBandCenterNormalized()` 恢复为按奖励档位数等分 `[-1, 1]` 的原逻辑即可。奖励配置、广告回调和背包发奖链无需回滚。
