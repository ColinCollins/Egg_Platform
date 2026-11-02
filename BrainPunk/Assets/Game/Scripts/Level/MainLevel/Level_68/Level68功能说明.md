# Level68 功能说明

**日期**: 2026-06-10

## 功能目标

Level68 复用 Level63 的 `Use_btn -> PasswordPopup` 入口节奏，但密码弹窗通过 `PasswordPopup.Create(popupName, popupCanvas.transform)` 手动创建到 Level68 关卡内 Canvas 下。

v2.0.0 新增 `PasswordTips` 自动填充逻辑：玩家必须先通过 Use button 打开 `PasswordPopup_68_4`，再把 `PasswordTips` 拖到 Level68 的 `TriggerArea` 内释放，才会隐藏 tips、播放 clue reveal，并通过既有 `PasswordPopup.AddInput(int)` 输入 `4148`。

本次逻辑保持 Level68 局部实现，不改共享 `DragableItem`、`TriggerAreaHandle`、`SwitchGamePanelUseBtnBoard`、`GamePanel_UseBtnCommonFeature`、存档、解锁、打点、RemoteConfig、Luban、YooAsset、ProjectSettings 或平台 SDK。

## 当前入口

- `Level_068.prefab` 根对象挂载 `Level68Ctrl`，`BaseLevelCtrl.gamePlayPanelName` 保持 `GamePlayPanel_017`。
- `PasswordBoard` 触发区继续通过 `SwitchObjActiveEvent` 通知 Use button 显示/隐藏。
- `GamePlayPanel_017.prefab` 的 `GamePanel_UseBtnCommonFeature.Use_btn` 是运行时优先入口，`Level68Ctrl.Use_btn` 只作为 Inspector 兜底。
- `PasswordTips` 上的 `DragableItem.onDragEndEvent` 绑定到 `Level68Ctrl.OnTipsEnterPasswordArea(Transform)`。
- `Level68Ctrl.triggerArea` 绑定到 `Level_068.prefab` 内 `TriggerArea` 的 `BoxCollider2D`。

## Use Button 逻辑

- `Awake()` 订阅 `SwitchObjActiveEvent`、`OnTiggerItemEvent`、`GameResetEvent` 和 `SwitchGameStateEvent`。
- `Start()` / `OnEnable()` 会重新获取当前 `Use_btn`，默认隐藏，并订阅 `Use_btn.OnClick += OnShowPasswordPopup`。
- `SwitchObjActiveEvent.isShow = true` 时，Use button 激活并按 `0.5 -> 1`、`0.1s` 缩放显示。
- `SwitchObjActiveEvent.isShow = false` 时，Use button 按 `1 -> 0`、`0.1s` 缩放收起。
- 自动填充成功后，Level68 会锁定并隐藏 Use button，避免同一 active run 内重复打开密码弹窗。
- `GameResetEvent` 会解除 Use button 锁并重新注册按钮。

## Password Popup 创建逻辑

- Level68 只创建 `PasswordPopup_68_4`；当前工作区该资源已经存在，且 `CorrectPassword` 覆盖为 `4148`。
- 创建路径为 `PasswordPopup.Create("PasswordPopup_68_4", popupCanvas.transform)`：
  - 不走 `UIManager.OpenUI(...)`
  - `Resources.Load<GameObject>("PasswordPopup_68_4")`
  - 实例化到 Level68 `_worldCanvas` 下
  - 立即执行 `OnCreate()`、`OnOpen()`、`OnShow()`
- 当弹窗已经存在时，再次点击 Use button 不重复实例化，只执行 `OnOpen()`、`OnShow()` 并将弹窗提到最后一个 sibling。
- Level68 创建出的弹窗默认保持 `CancelPendingSuccessOnClose = false`，手动输入路径不改变；只有 `PasswordTips` 自动填充流程启动后才临时启用该开关，用于阻止“自动输入正确密码后、成功延迟派发前关闭弹窗”继续晚到派发成功。

## PasswordTips 自动填充规则

- `OnTipsEnterPasswordArea(Transform target)` 只接受当前 `passwordTips` 或其子节点。
- 没有 active password popup 时释放 tips：忽略，不自动打开弹窗，不隐藏 tips，不派发成功。
- active popup 存在但释放点不在 `TriggerArea` 内：忽略，tips 保持可见可用。
- 自动填充进行中、已经提交或已经成功消费后：重复 drop 忽略。
- 合法 drop 后：
  - 隐藏 `PasswordTips`
  - 调用 `Level68PasswordCtrl.WaitingPass(...)`
  - `WaitingPass()` 重置当前输入、隐藏数字显示、让 `password_txt` 在 0.5 秒内逐字显示
  - reveal 完成后依次调用 `PasswordPopup.AddInput(4)`、`AddInput(1)`、`AddInput(4)`、`AddInput(8)`
  - 仍由 `PasswordPopup` 原成功链路派发 `OnTiggerItemEvent` id 1 并关闭弹窗

## 关闭 / 成功 / 生命周期

- `PasswordTips` 只有在 Level68 收到自动填充流程对应的 `OnTiggerItemEvent` id 1 后才视为永久消费。
- 如果 popup 在成功事件前关闭，Level68 会取消 `Level68PasswordCtrl` 当前等待流程并恢复 `PasswordTips` 初始位置/旋转/缩放/显示状态。
- `GameResetEvent` 会重置 tips、自动填充状态、active controller、Use button 锁和当前 popup。
- `OnDisable()` / `OnDestroy()` 会取消等待流程、销毁手动创建 popup、退订 Use button 和事件订阅，避免切关或退出后残留。

## PasswordPopup_68_4 资源状态

- `PasswordPopup_68_4.prefab` 是 `PasswordPopup.prefab` 的 variant。
- 当前静态证据：`m_Name = PasswordPopup_68_4`，`CorrectPassword = 4148`，新增 `Level68PasswordCtrl` 组件。
- `password_txt` 是本 variant 新增子对象，已绑定到 `Level68PasswordCtrl.passwordTxt`。
- `numTxts` 与 `popup` 在 variant YAML 中保留运行时兜底：`Level68PasswordCtrl` 会从子 TMP 名称和同物体 `PasswordPopup` 组件自动发现，避免手工写 stripped source prefab 组件引用造成 prefab variant 损坏。

## 相关文件

- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_68/Level68Ctrl.cs`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_68/Level68PasswordCtrl.cs`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_68/Level68功能说明.md`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_68/Level68风险评估.md`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_6/PasswordPopup.cs`
- `BrainPunk/Assets/Game/Prefabs/Level/Level_068.prefab`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_6/Resources/PasswordPopup_68_4.prefab`

## 验证重点

- 静态：`Level_068.prefab` 保持 `GamePlayPanel_017`、`_worldCanvas`、`passwordTips`、`TriggerArea` 和 `PasswordTips.onDragEndEvent -> OnTipsEnterPasswordArea`。
- 静态：`PasswordPopup_68_4.prefab` 存在、密码为 `4148`、有 `Level68PasswordCtrl` 和 `password_txt`。
- 编译：`dotnet build Assembly-CSharp.csproj --no-restore` 无 Level68/PasswordPopup 编译错误。
- PlayMode：验证 Use button、手动 `4148`、合法 drop、重复 drop、关闭前恢复、成功后消费、重开/退出清理。
- 设备：Android/iOS 横屏验证 `PasswordTips` touch drag、TriggerArea 边界、popup 层级和关闭按钮命中。
