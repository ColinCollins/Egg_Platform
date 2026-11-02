# Level 105 不要操作风险评估

## 原有行为基线

在 `a63eb6d20` 快照导入前，当前 1.11 基线没有 Level 105 Prefab、Domain、运行时脚本或关卡专属假 UI。本关新增自动流程和三份真实 UI Variant 外观，但设计合同要求这些 UI 只用于诱导与判负，不得执行真实奖励、广告、分享、存档或下一关业务。

## 影响文件与系统

- `Assets/Game/Prefabs/Level/Level_105.prefab`
- `Assets/Game/Prefabs/Level/Level_105/Resources/`
- `Assets/Game/Scripts/Level/MainLevel/Level_105/`
- 只读/复用：`BaseLevelCtrl`、`ActorCtrl`、GamePlayPanel、Bear UIManager、CustomButton、真实胜利/奖励面板外观组件、DB/RemoteConfig/Purchase/Bag/Postcard 查询。

## 风险项

1. **P0：正常入口缺失。** 当前 LevelData/LevelSort 没有 Level 105。若应正式上线，正常选关/下一关路径不可达；若仅暂存，必须明确禁止误补配置。
2. **P0：假 UI 可能触发真实业务副作用。** 本关脚本没有直接发奖、保存、广告、分享、下一关或 analytics 调用，但 Variant 继承真实面板组件及既有按钮回调，安全性依赖 `CustomButton.IsBlockClick` 和本关视图的阻断。必须动态证明点击所有按钮只产生反馈与失败，且 DB、金币、星星、PassedLevels、分享状态、广告计数和 analytics 均不变化。
3. **P1：输入判定覆盖面和重复入口。** 移动/跳跃事件、GameplayPanel `OnClickDown`、鼠标/Touch 屏幕检测和假弹窗按钮各有入口。系统按钮需要白名单，假按钮又需要绕过屏幕检测等待原反馈；层级、EventSystem 射线或按钮改名都可能造成误判、漏判或同一输入重复收口。
4. **P1：系统 UI 恢复依赖 `UIMask` 可见性。** 恢复会延后一帧并扫描可见遮罩。若某个合法子面板没有 UIMask，关闭点击可能被误判；若遮罩残留，关卡可能永久挂起。
5. **P1：自动物理流程对帧率和碰撞敏感。** 坑底判定依赖 IsGround、坑口范围和 Y 线；前五跳锁水平，第六跳恢复水平。低帧率或碰撞层错误可能漏计、重复计数、无法脱坑或越过 X=1 假金币触发点。
6. **P1：真门与假胜利同帧优先级。** 真门只在假胜利完整移除后启用，并在每帧输入前优先提交 Victory。Prefab 触发器初态或引用错误会导致提前真胜、假胜后卡死或已进门仍被点击判负。
7. **P1：真实 UI 资源与继承组件耦合。** 三个资源名、UI 层级、Animator/Spine 入场和内部按钮结构都被运行时约定使用；上游真实面板改层级、按钮名或组件时，本关可能卡在 Entering、错误展示或失去业务阻断。
8. **P1：当前没有可复验测试。** Domain、输入白名单、暂停恢复、5+1 跳和 UI 无副作用均没有当前分支测试保护。
9. **P2：清理与状态恢复。** 本关修改 Actor `IsEditor`、Rigidbody2D simulated/velocity 和 HUD 按钮 `IsBlockClick`；销毁路径有恢复逻辑，但需要验证退出、重开、失败和系统面板嵌套后不会污染下一关或共享 HUD。

## 验证证据

### facts

- 当前本关脚本只读取 PassedLevels、HasSharedLevel、RemoteConfig 分享奖励、NoAds 状态和 Postcard 星星用于展示。
- 当前本关脚本没有命中 `Save`、奖励发放、广告播放、分享、下一关或 analytics 业务调用。
- 非白名单 GameplayPanel/假弹窗按钮被设为 `IsBlockClick=true`，并在清理时恢复原状态。
- 当前分支没有 Level 105 配置条目和匹配测试文件。

### direct conclusions

- 本地脚本的设计意图是“真实外观、只读数据、按钮判负”，没有主动执行业务。
- 静态检查不能证明继承组件、Prefab UnityEvent 或 CustomButton 运行时行为绝对无副作用。

### missing evidence

- Unity 编译、Prefab Missing Script/序列化引用与 UI 资源加载。
- 全自动正解、每类违规输入、系统按钮及嵌套子面板的 PlayMode 记录。
- 点击前后 DB/金币/星星/PassedLevels/分享/广告/analytics 快照。
- Android/iOS 横屏、触控、多指、低帧率与异形屏验证。

## 回滚路径

选择性回滚 Level 105 的 Prefab、`Resources`、脚本目录和本地文档即可。当前没有 Level 105 配置或存档迁移；不要整体回滚 `a63eb6d20`，否则会同时影响 Level 78、83、103、104 和共享资源导入。
