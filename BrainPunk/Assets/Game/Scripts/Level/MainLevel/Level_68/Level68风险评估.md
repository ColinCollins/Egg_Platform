# Level68 风险评估

更新时间：2026-06-10

## 原有行为基线

- `Level_068.prefab` 当前 `BaseLevelCtrl.gamePlayPanelName` 为 `GamePlayPanel_017`。
- `Level_068.prefab` 根对象绑定 `Level68Ctrl._worldCanvas` 与 `passwordTips`。
- `Level_068.prefab` 内存在 `TriggerArea`，其 `BoxCollider2D` fileID 为 `8582878188488106953`。
- `PasswordTips` 是 prefab instance，当前有 `DragableItem` 和 `BoxCollider2D`；实现前静态检查时 `DragableItem.onDragEndEvent` 有目标 Level68 根对象但方法名为空。
- `PasswordPopup_68_4.prefab` 当前存在，是 `PasswordPopup.prefab` 的 variant；静态证据显示 `m_Name = PasswordPopup_68_4`，`CorrectPassword = 4148`，并已添加 `Level68PasswordCtrl`。
- 旧文档中“`PasswordPopup_68_4` 缺失并回退 `PasswordPopup_66_4`”的记录已经过期；当前实现不再回退到 `PasswordPopup_66_4`。
- `PasswordPopup.Create(string)` 是非 Level68 默认入口，继续走 `UIManager.OpenUI(...)`。
- `PasswordPopup.Create(string, Transform)` 是 Level68 手动创建入口，实例化 Resources prefab 到指定 parent，并设置 `_destroyDirectlyOnClose = true`。

## 影响文件 / 资源

- `Assets/Game/Scripts/Level/LevelItem/Level_68/Level68Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_68/Level68PasswordCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_6/PasswordPopup.cs`
- `Assets/Game/Prefabs/Level/Level_068.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_6/Resources/PasswordPopup_68_4.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_68/Level68功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_68/Level68风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- `specs/012-level68-password-tips/tasks.md`

## 使用的既有系统

- `SwitchObjActiveEvent`：沿用 PasswordBoard 触发区驱动 Use button 显示/隐藏。
- `GameResetEvent`：重置 Level68 本地状态、销毁手动 popup、恢复 tips 与 Use button。
- `SwitchGameStateEvent`：SUCCESS / FAILED 时取消自动填充流程。
- `OnTiggerItemEvent` id 1：沿用 `PasswordPopup` 成功事件，Level68 只在自动填充流程中用它确认 tips 永久消费。
- `GamePanel_UseBtnCommonFeature.Use_btn`：复用 `GamePlayPanel_017` 的通用 Use button。
- `PasswordPopup.AddInput(int)`：自动填充仍走既有输入、校验、音效、成功派发链。
- `DragableItem.onDragEndEvent`：只做 Level68 prefab 绑定，不修改共享 `DragableItem`。
- `Bear.Logger.IDebuger`：Level68 缺引用和跳过路径使用 Bear.Logger warning/error。

## 已规避风险

- 新增拖拽自动填充逻辑限制在 Level68 和 `PasswordPopup_68_4`，未修改共享拖拽、TriggerAreaHandle、Use button provider、存档、解锁、打点、RemoteConfig、Luban、YooAsset、ProjectSettings 或平台 SDK。
- `OnTipsEnterPasswordArea` 要求 active popup 已存在；drop tips 不会自动打开 popup。
- `OnTipsEnterPasswordArea` 会校验 target 是 `passwordTips` 或子节点，避免其他拖拽物误触发。
- release 点需要落在 `triggerArea.OverlapPoint(...)` 或 collider bounds 内；无 TriggerArea 时只 warning 并忽略。
- `_autoPasswordRunning`、`_autoPasswordSubmitted`、`_passwordTipsConsumed` 防止重复 drop 或同一 active run 重复启动自动填充。
- `PasswordTips` 只有在自动填充流程收到既有 `OnTiggerItemEvent` id 1 后才永久隐藏并锁 Use button。
- popup 在成功前关闭时，Level68 会取消 `Level68PasswordCtrl` 等待流程并恢复 tips 初始 transform / active 状态。
- `PasswordPopup.CancelPendingSuccessOnClose` 默认 false；Level68 只在 `PasswordTips` 自动填充流程进行中临时设置为 true，用于避免自动输入正确密码后关闭 popup 仍晚到派发成功，不改变手动输入路径和非 Level68 默认 popup 行为。
- `Level68PasswordCtrl` 支持 Inspector 引用，也支持运行时从同物体 `PasswordPopup` 和子 TMP 名称兜底发现，降低 `PasswordPopup_68_4` variant 手工 YAML 引用风险。
- `ResetUseBtn()` 旧实现会 `EventsUtils.ResetEvents(ref _subscriber)`，可能清掉 `GameResetEvent` 等订阅；当前实现改为只退订/隐藏 Use button，保留 Level68 事件订阅。

## 静态验证证据

- `Level_068.prefab`：
  - `gamePlayPanelName` 保持 `GamePlayPanel_017`。
  - `Level68Ctrl._worldCanvas` 为 `{fileID: 4333603175605619820}`。
  - `Level68Ctrl.passwordTips` 为 `{fileID: 1168052800158320479}`。
  - `Level68Ctrl.triggerArea` 为 `{fileID: 8582878188488106953}`。
  - `TriggerArea` 存在，挂 `BoxCollider2D`。
  - `PasswordTips` 挂 `DragableItem` 与 `BoxCollider2D`。
  - `PasswordTips.DragableItem.onDragEndEvent` 调用 `Level68Ctrl.OnTipsEnterPasswordArea`。
- `PasswordPopup_68_4.prefab`：
  - variant 名称覆盖为 `PasswordPopup_68_4`。
  - `CorrectPassword` 覆盖为 `4148`。
  - `Level68PasswordCtrl` 组件存在。
  - `passwordTxt` 绑定到本 variant 新增的 `password_txt` TMP 组件。
  - `numTxts` 与 `popup` 依赖运行时兜底发现；需要 Unity Inspector 再确认是否要补显式引用。
- 静态代码检查：
  - Level68 相关脚本未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`RemoteConfig`、Luban、YooAsset、ProjectSettings 或平台 SDK 调用。
  - 本次未修改共享 `DragableItem`、`TriggerAreaHandle`、`GamePanel_UseBtnCommonFeature`、save/unlock/analytics 代码。

## 编译验证证据

- 2026-06-10 在 `BrainPunk/` 执行：
  - `dotnet build Assembly-CSharp.csproj --no-restore`
  - 结果：0 errors / 89 warnings。
- warnings 为项目既有范围，未见 `Level68Ctrl.cs`、`Level68PasswordCtrl.cs`、`PasswordPopup.cs` 新增 error。

## P1 手工验证清单

- [ ] 进入 Level68，打开 password popup 后，把 `PasswordTips` 拖入 `TriggerArea`，确认只启动一次自动填充。
- [ ] 合法 drop 后确认 tips 隐藏，`password_txt` 0.5 秒 reveal，数字显示隐藏。
- [ ] 自动输入 `4`、`1`、`4`、`8` 后确认走既有密码成功链路。
- [ ] 自动填充进行中重复 drop，确认不会重复输入或重复成功。
- [ ] 自动填充后、成功事件前关闭 popup，确认 tips 恢复且不会晚到成功。
- [ ] 自动填充中重开关卡，确认 tips 恢复、popup 销毁、无旧回调继续输入。
- [ ] 成功后确认 tips 在当前 active run 内永久隐藏，Use button 被锁定隐藏。

## P2 手工验证清单

- [ ] 进入 Level68 PasswordBoard 区域，确认 Use button 显示节奏仍为 `0.5 -> 1` / `0.1s`。
- [ ] 离开 PasswordBoard 区域，确认 Use button 收起节奏仍为 `1 -> 0` / `0.1s`。
- [ ] 点击 Use 打开 `PasswordPopup_68_4`，确认 actor 停止移动。
- [ ] 关闭 popup，确认 actor 恢复移动，Use/manual path 仍可再次使用。
- [ ] popup 已存在时重复点击 Use，确认不重复实例化，只重置/重显现有实例。
- [ ] 自动填充成功前，手动输入 `4148` 仍可完成既有密码成功。
- [ ] 至少验证一个非 Level68 password popup 关卡：打开、输错反馈、输对成功、关闭和关卡推进。

## P3 手工验证清单

- [ ] 未打开 popup 时，把 `PasswordTips` 拖入 `TriggerArea`，确认不自动打开 popup、不隐藏 tips、不派发成功。
- [ ] popup 打开时，把 `PasswordTips` 拖到 `TriggerArea` 外释放，确认 tips 可见可用、不输入密码、不派发成功。
- [ ] 在 TriggerArea 边界附近释放，确认命中范围与设计可接受。
- [ ] Android 横屏设备/模拟器验证 inside/outside touch drag 边界。
- [ ] iOS 横屏设备/模拟器验证 inside/outside touch drag 边界。

## 未验证 / 剩余风险

- 尚未在 Unity PlayMode 验证 `PasswordBoard -> SwitchObjActiveEvent -> Use_btn` 链路是否与 Level63 节奏完全一致。
- 尚未在 Unity PlayMode 验证 `PasswordPopup_68_4` 的实际层级、sorting、按钮触控和 close path。
- 尚未在 Unity PlayMode 验证 `Level68PasswordCtrl` 对 `numTxts` / `popup` 的运行时兜底发现是否覆盖当前 variant 的实际层级。
- 尚未在 Unity PlayMode 验证合法 drop、重复 drop、关闭前恢复、成功后消费、restart/disable/destroy 清理。
- 尚未验证手动 `4148` 与自动填充状态交互：手动成功后是否需要同样锁定 `PasswordTips`，当前实现只在自动填充流程中消费 tips。
- 尚未验证至少一个非 Level68 password popup 回归。
- 尚未验证 Android / iOS 横屏 touch drag、TriggerArea 边界、popup 层级和触控命中。
- `Level_068.prefab` 当前工作区已有大量未提交 prefab 差异，本次只针对 `triggerArea` 与 `PasswordTips.onDragEndEvent` 做了必要修改；其他 prefab 差异未由本次任务评估。

## 回滚路径

- 回滚 `Level68Ctrl.cs` 中 `PasswordTips` 自动填充、TriggerArea 判定、tips 消费/恢复和 Use button lock 逻辑。
- 回滚 `Level68PasswordCtrl.cs` 的 `WaitingPass()` reveal / auto input / cancel 逻辑。
- 回滚 `Level_068.prefab` 中 `Level68Ctrl.triggerArea` 引用与 `PasswordTips.DragableItem.onDragEndEvent` 绑定。
- 回滚 `PasswordPopup_68_4.prefab` 中 `Level68PasswordCtrl.passwordTxt` 引用。
- 如需完全回退 close-before-success 防护，移除 `PasswordPopup.CancelPendingSuccessOnClose` 与 `_closeVersion`，但必须重新验证 Level68 关闭前成功晚到风险。
- 文档回退：恢复本文件、`Level68功能说明.md`、`DevelopMD/已有功能设计与使用记录.md` 中本次 Level68 v2.0.0 记录。

## Quickstart 覆盖状态

- Static Checks：部分通过。`Level_068.prefab` 与 `PasswordPopup_68_4.prefab` 关键静态项已检查；`numTxts` 和 `popup` 采用运行时兜底，Inspector 显式绑定仍需 Unity 内确认。
- Compile Check：通过，0 errors / 89 warnings。
- Level68 Manual Validation：未运行，需要 Unity PlayMode。
- Version 2.0.0 Flow Validation：未运行，需要 Unity PlayMode。
- Popup-Not-Open Validation：未运行，需要 Unity PlayMode。
- Lifecycle Validation：未运行，需要 Unity PlayMode。
- Regression Validation：未运行，需要选择非 Level68 password popup 关卡手工验证。
- Mobile Smoke Validation：未运行，需要 Android/iOS 横屏设备或模拟器。

## Pending Clarification

- 当前需求明确“`PasswordTips` 只在自动填充成功后永久消费”。若玩家手动输入 `4148` 成功，当前实现不会额外消费 tips；这与“manual path remains usable until PasswordTips success”的规格一致，但需要验收确认是否符合最终策划预期。
- `PasswordPopup_68_4` 的 `numTxts` 是否必须在 Inspector 中显式绑定，还是接受 `Level68PasswordCtrl` 的运行时兜底发现，需要 Unity Inspector/PlayMode 验证后决定。
