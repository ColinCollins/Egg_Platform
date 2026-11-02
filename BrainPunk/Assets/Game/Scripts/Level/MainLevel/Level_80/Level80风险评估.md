# Level080 风险评估

更新时间：2026-07-17

## 原有行为基线

Level006 的密码入口和问号砖块逻辑由共用 `GamePlayPanel_001`、`PasswordPopup`、`TriggerExecutor` 与倒计数监听器驱动。现有问号砖块达到配置次数后会隐藏/替换并关闭触发器。Level041 的拖拽组件可限制方向。Level068 已使用 `Postcard_Star` 与抛物线顶出表现。

## 受影响资源与系统

- Level080 预制体及其 PasswordPopup 变体。
- Level006 共用的 `Level6GamePanelFeature`，本次只增加默认值为 `PasswordPopup` 的配置入口。
- 已有 `DragableItem`、`Postcard_Star`、`MoveFloatHandle`、砖块触发器和明信片星星存档链路。

## 风险与处理

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| 修改共用面板导致 Level006 密码入口变化 | 保持配置默认值为 `PasswordPopup`；仅 Level080 运行时改为专用变体 | 静态已检查，PlayMode 未验证 |
| 无限顶击后砖块仍被原倒计数关闭 | 四个砖块的两个倒计数统一设为 `2147483647` | 静态已检查，PlayMode 未验证 |
| 星星重复发放或绕过存档 | 第 5 次只实例化一次，星星使用既有 `Postcard_Star` 组件与序号 1/2 | 静态已检查，收集存档未验证 |
| 星星顶出时既有浮动动画与抛物线冲突 | 顶出期间禁用 `MoveFloatHandle`，落点后再启用 | 代码静态已检查，PlayMode 未验证 |
| 拖拽造成原有固定砖块碰撞表现异常 | 拖拽只挂在 Level080 的四个砖块，未修改共享 `DragableItem` | 静态已检查，移动端未验证 |
| 预制体 YAML 引用损坏 | 新增 fileID 去重检查通过，星星模板与本地组件引用存在 | Unity Inspector 导入未验证 |
| 密码面板资源被 Unity 忽略 | 将 `PasswordPopup_80_5` 改为与 Level006 已有密码变体相同的最小结构，并使用无重复的标准 GUID | `.meta` 静态格式已检查，需 Unity 重新导入确认 |

## 验证证据

- `Level_080.prefab` 静态 fileID 重复检查通过。
- 8 个 `Countdown` 字段均为 `2147483647`。
- 密码变体 `PasswordPopup_80_5` 显式覆盖 `CorrectPassword: 5639`。
- 用户提供的 Unity 日志记录：旧 `.meta` 的 GUID 无法由 YAML Parser 提取，导致对应资源被忽略；该错误直接阻断 `PasswordPopup_80_5` 的 Resources 加载。
- 修复后静态检查：新 `.meta` GUID `b872ef41951e47459631865f2630c9d4` 为 32 位小写十六进制，Unity 资源 `.meta` 中未发现重复；变体仅保留名称与 `CorrectPassword: 5639` 两项覆盖。
- Unity 导入证据：Editor.log 在旧错误之后记录该 prefab 以新 GUID 完成 `PrefabImporter` 导入，未再出现后续同资源的 GUID/YAML 错误。
- 代码未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB 访问或全局事件；已跟踪改动的 `git diff --check` 无空白错误。未跟踪的复制预制体保留原有空值字段尾随空格，未做无关格式化。
- `dotnet build Assembly-CSharp.csproj --no-restore` 未能运行：本机没有 .NET SDK，无法提供程序集编译证据。

## 待补验证

- Unity 重新导入后确认日志不再出现 `PasswordPopup_80_5.prefab.meta` 的 GUID/YAML 警告，且该预制体无 Missing Script 或序列化警告。
- Use 按钮输入 `5639` 成功；输入错误密码保持原错误反馈；重新开始后再次验证。
- 分别顶击四个砖块超过 5 次，确认不消失；Box_1 与 Box_4 仅在第 5 次出现对应星星。
- 收集两颗星星后确认不会重复发放，重新进入关卡后已收集星星不再可收集。
- Android/iOS 横屏确认四个砖块可向任意方向拖拽，触控、碰撞、顶击和星星画面无异常。

## 回滚路径

回退 Level080 本地脚本、密码变体、星星模板、砖块倒计数和拖拽组件。共用 `Level6GamePanelFeature` 的新增配置入口可恢复为固定调用 `PasswordPopup.Create()`；其余 Level006 资源无需修改。
