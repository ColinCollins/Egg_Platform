# Level089 风险评估

更新时间：2026-07-17

## 原有行为基线

Level089 预制体中已经存在石柱、四个按钮、Trigger 和角色。按钮此前使用通用 `Level47YellowButton`，只负责玩家进入/离开时的视觉下压与回弹，不含顺序验证、石柱位移或玩家禁动。

## 受影响资源与系统

- `Level_089.prefab` 根节点及 4 个 Trigger 的脚本绑定。
- 新增本关私有的 `Level89Ctrl` 与 `Level89SequenceButton`。
- 既有 `ActorCtrl`、`PressButtonBase`、DOTween 与场景主相机。

## 风险与处理

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| 按钮回弹后玩家站在 Trigger 内反复触发 | 每个按钮在进入后必须等 `OnTriggerExit2D` 才重新武装 | 静态已检查，PlayMode 待验收 |
| 正确按钮在玩家离开时回弹 | 本关按钮被控制器锁定后忽略离开触发器的回弹逻辑 | 静态已检查，PlayMode 待验收 |
| 机关动画期间玩家仍可移动或保持跑步动画 | 锁定前先调用 `SetMoveInput(0)` 清空横向输入，再调用 `StopMoving()`；序列完成或禁用时调用 `ResumeMoving()` | 静态已检查，PlayMode 待验收 |
| 错误复位与前一段动画重叠 | 根控制器使用一个本地 DOTween Sequence；新演出前终止前一序列 | 静态已检查，PlayMode 待验收 |
| 正确下落时的镜头抖动影响其他关卡或其他相机效果 | 只在正确下落时由 Level089 根控制器创建并保存本次 Tween，禁用时只终止本次 Tween | 静态已检查，PlayMode 待验收 |
| 石柱移动距离与关卡通路不匹配 | 总下落距离调整为本关序列化字段 `8.0`，可在 Inspector 中局部调整 | 静态已检查，关卡画面待验收 |
| 缺少石柱或角色引用导致空引用 | `stone` 以根节点名称查找并作空值告警；角色与镜头均有空值保护 | 静态已检查，Unity 导入待验收 |

## 验证证据

- 预制体静态核对：`stone` 位于根节点下，根节点已有 `BaseLevelCtrl` Actor 引用；四个按钮按 X 坐标由左到右递增，对应编号 `1-4`。
- 预制体静态核对：四个 Trigger 都保留启用的 Trigger Collider 和原按钮/碰撞器序列化引用。
- 源码静态核对：没有新增 `Update` 轮询、全局事件、DB/save、配置表、资源加载或编辑器生命周期入口。
- `git diff --check` 无空白问题。

## 未验证项

- 当前环境无 .NET SDK 和可调用 Unity Editor，未能执行 C# 编译、Inspector 检查或 PlayMode。
- Android/iOS 横屏设备上的镜头震动、连续错误输入、关卡重启、玩家禁动与移动恢复尚未实测。

## 2026-07-17 编译修复风险

- `DOShakePosition` 与原调用均针对 `Transform` 的位置抖动；参数未变，风险仅限于 DOTween 本地坐标实现与原错误方法意图的差异，需在 PlayMode 观察石块抖动是否围绕当前局部位置。
- 本次仅替换一个无法解析的 API 调用，没有改变按钮顺序、角色锁定、镜头抖动、DB/save、配置、资源或全局事件。

## 回滚路径

移除本关根节点的 `Level89Ctrl`，恢复 4 个 Trigger 的 `Level47YellowButton` 脚本 GUID，并删除 `Level_89` 目录。本次没有共享代码、配置或数据变更，因此不需要回滚其他关卡。

## 2026-07-22 音效风险补充

### 原有基线与受影响项

原有正确下降、首次错误和已有进度错误都只播放机关动画，没有对应声音。本次只在 Level089 本地控制器增加播放调用，并新增 L89 音频及 SoundsGood 登记；复用的 `passwordError` 资源和共享播放系统保持不变。

### 风险与处理

| 风险 | 处理与证据 | 状态 |
| --- | --- | --- |
| 首次错误与复位错误分别接入后双播 | 统一在错误条件入口播放一次，再进入两个互斥方法 | 静态通过 |
| 石柱下降与错误向上复位共用音效 | `l89StonePillarDrop` 只位于 `PlayCorrectStep()`，复位分支无调用 | 静态通过 |
| 动画期多 Collider 重复触发 | 既有 `isBusy` 与 `requiresExitBeforeRearm` 保持防重 | 静态通过，PlayMode 待验收 |
| 1.715 秒声音长于 1.2 秒单步动画 | 不破坏性裁剪用户源文件，连续正确步骤可能出现约 0.5 秒尾音重叠 | PlayMode 待决策 |
| 回滚误删共享错误声音 | 回滚只删除 L89 新资源和本关调用，`passwordError` 必须保留 | 已记录 |

### 验证缺口与回滚

格式、哈希、tag-GUID 对应、两个目标程序集的独立编译和 Unity 正式导入/重建均已通过，最近日志相关错误为 0。Unity PlayMode 的错误路径、快速连续正确输入、暂停/重开和移动端输出仍待验证。回滚不修改 Level068、共享 `PasswordPopup`、`passwordError` 或 `AudioManager`。
