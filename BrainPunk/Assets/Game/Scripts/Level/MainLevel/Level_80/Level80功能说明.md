# Level080 功能说明

更新时间：2026-07-17

## 需求背景

Level080 以 Level006 为基础，密码调整为 `5639`。四个问号砖块均可无限次顶击；第一个和第四个砖块在各自第 5 次顶击时顶出一颗明信片星星；全部砖块支持任意方向拖拽。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_080.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_80/Level80Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_80/Level80QuestionBlockReward.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_80/Resources/PasswordPopup_80_5.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_6/Level6GamePanelFeature.cs`

使用既有系统：`PasswordPopup`、`Level6GamePanelFeature`、`DragableItem`、`TriggerExecutor`、`Postcard_Star`、`MoveFloatHandle`。未新增存档字段、全局事件、埋点、RemoteConfig、资源加载规则或平台 SDK 调用。

## 原有行为基线

Level006 使用 `GamePlayPanel_001` 的 `Level6GamePanelFeature` 打开默认 `PasswordPopup`。每个问号砖块使用两个倒计数监听器，在达到原配置次数后替换砖块并禁用触发器，因此不可无限顶击。Level041 的 `DragableItem` 配置限制 X 轴，只能沿指定方向拖动。Level068 的星星使用 `Postcard_Star` 的既有收集和存档链路，并在出现时播放顶出表现。

## 本次行为

- `Level80Ctrl` 在玩法面板可用后设置 `PasswordPopup_80_5`；该变体的 `CorrectPassword` 为 `5639`。
- 2026-07-17 修复：Unity 日志确认该变体的 `.meta` 无法提取 GUID，资源因此被忽略，导致 Use 按钮无法打开密码面板。现已替换为不重复的标准 GUID，并将变体 YAML 收敛为与 Level006 已有变体相同的最小 `PrefabInstance` 结构。
- `Level6GamePanelFeature` 新增密码弹窗名配置入口，默认仍为 `PasswordPopup`，仅 Level080 在运行时覆盖为专用变体。
- Level080 四个砖块的两个原有倒计数都设为 `2147483647`，正常游戏次数内不会切换砖块或关闭触发器。
- Box_1 与 Box_4 各增加一个 `Level80QuestionBlockReward`，分别在第 5 次触发时从已配置、未激活的 `Postcard_Star` 模板实例化星星。模板星星序号分别为 1 和 2，收集后继续走原有 `Postcard_Star` 的存档、奖励和 UI 飞星事件。
- 星星出现阶段使用与 Level068 同类的抛物线顶出节奏：先从砖块位置出现，再上抛并落到砖块下方，之后启用既有 `MoveFloatHandle` 浮动。
- 四个砖块均增加 `DragableItem`，不限制拖拽范围、X/Y 方向或移动阈值。

## 数据与流程

密码流程：玩家进入 PasswordBoard 区域 -> 既有 `SwitchObjActiveEvent` 显示 Use 按钮 -> `Level6GamePanelFeature` 打开 `PasswordPopup_80_5` -> `PasswordPopup` 使用 `5639` 校验 -> 原有成功事件和关卡推进链路不变。

砖块流程：角色从下方触发砖块 -> 原有音效、反馈与等待监听器执行 -> 倒计数不会达到切换条件 -> Box_1/Box_4 的本地监听器递增次数 -> 第 5 次实例化对应 `Postcard_Star` -> 既有星星碰撞收集、存档和 UI 飞星流程执行。

## 已执行验证

- 静态检查：确认 `Level_080.prefab` 新增 Unity fileID 无重复。
- 静态检查：确认 4 个砖块的 8 个 `Countdown` 值均为 `2147483647`。
- 静态检查：确认 Box_1、Box_4 各绑定一个第 5 次奖励监听，四个 Box 均绑定无方向限制的 `DragableItem`。
- 静态检查：确认密码变体名称为 `PasswordPopup_80_5`，密码覆盖为 `5639`。
- 静态检查：`PasswordPopup_80_5.prefab.meta` 的 GUID 为 32 位小写十六进制，且 Unity 资源 `.meta` 中无重复；变体继续引用默认 `PasswordPopup` 的 GUID `5d4f2e76d49de9746923c65db07f7bb4`。
- Unity 导入：Editor.log 在 14:30 记录 `PasswordPopup_80_5.prefab` 已通过 `PrefabImporter` 以 GUID `b872ef41951e47459631865f2630c9d4` 成功导入；该记录位于旧 GUID/YAML 报错之后。
- 已跟踪改动的 `git diff --check`：无空白错误，仅有 Git 的 LF/CRLF 提示。复制而来的未跟踪 Level080 预制体保留了原有空值字段尾随空格，未对整份 YAML 做无关格式化。
- 编译：尝试在 `BrainPunk` 目录运行 `dotnet build Assembly-CSharp.csproj --no-restore`，因环境未安装 .NET SDK 而无法执行。

## 未验证项

- 需由 Unity 重新导入后在 PlayMode 验证 Level080 的密码面板可打开、密码成功、错误密码、重新开始和通关。
- 未在 Unity PlayMode 验证四个砖块的无限顶击、Box_1/Box_4 第 5 次奖励和星星收集存档。
- 未在 Android/iOS 横屏设备验证问号砖块任意方向触控拖拽、砖块碰撞和星星顶出画面。

## 回滚

删除 Level080 的 `Level80Ctrl`、`Level80QuestionBlockReward`、两个星星模板、四个 `DragableItem` 和倒计数覆盖；移除 `PasswordPopup_80_5`。若不再需要可配置的密码入口，恢复 `Level6GamePanelFeature` 中 `PasswordPopup.Create()` 的原调用即可。
