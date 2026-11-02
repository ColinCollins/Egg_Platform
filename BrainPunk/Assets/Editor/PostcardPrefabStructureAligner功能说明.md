# PostcardPrefabStructureAligner 功能说明

## 需求与背景

美术产出的 `Postcard_*.prefab` 可能只有主题图片、拼图块和临时 CostBlock 层级，缺少明信片页面运行所需的 `Postcard_PageItem`、Animator、ProgressItem、TurnEffect、正式 CostBlock 与 CompleteMode。该工具提供 Project 右键入口，也提供可由项目 Skill 指定路径调用的公开入口，用 Unity Editor API 补齐结构和组件。

## 影响范围

- 工具脚本：`Assets/Editor/PostcardPrefabStructureAligner.cs`
- 手动入口：Project 资源右键 `Bear/Postcard/补齐结构与组件`
- Skill：`D:/GitStore/BrinPunk/.agents/skills/brainpunk-align-postcard-prefab/`
- 本次转换输入来自 Pictures 下的 magic、pirate、toys 主题美术 prefab；完成后的运行时 prefab 位于 `Assets/Game/UI/Postcard/Resources/Postcard_magic.prefab`、`Postcard_pirate.prefab`、`Postcard_toys.prefab`。
- 运行时结构契约参考：`Assets/Game/UI/Postcard/Resources/Postcard_universe.prefab`

工具不访问 DB、Save、RemoteConfig、运行时单例、配置、事件、分析或平台 SDK，也不修改参考 prefab、主题图片和目标 `.meta`。

## 原有行为基线

转换前的海盗美术 prefab 是扁平结构：根节点包含 Canvas、CanvasRenderer 与 GraphicRaycaster；主题内有背景、角色和 4 组拼图图片，但没有 `Postcard_PageItem`、Animator、`Postcard_ProgressItem`、`Postcard_TurnEffect`、嵌套正式 CostBlock 或 CompleteMode。转换前备份位于 `C:/Users/admin/AppData/Local/Temp/brainpunk-postcard-pirate-before-align-20260722/`。

## 当前行为

1. 仅识别 `Assets/Game/UI/Postcard/Sprites/Pictures/` 下名称以 `Postcard_` 开头的 prefab；不符合规则时右键命令不可用。
2. 读取目标主题背景、角色图、拼图组、感叹号和 RectTransform；语义映射不唯一时停止，不保存目标。
3. 以 `Postcard_universe.prefab` 定义运行时结构契约，并从 Resources 中选择拼图数量相同且通过契约检查的模板。示例海盗图有 4 组，因此实际选择 `Postcard_ocean.prefab`，不会强行增加第 5 组。
4. 使用 `PrefabUtility.LoadPrefabContents` 与 `PrefabUtility.SaveAsPrefabAsset` 保存目标，保留目标主题图片、4 组布局、名称和 `.meta` GUID；正式 CostBlock 与 CompleteMode 保持嵌套 prefab 实例。
5. 保存后检查根组件、Missing Script、ProgressItem 数量、关键序列化引用、主题 Sprite、CompleteMode 和 `.meta` GUID。已完成的运行时 prefab 不会被重复转换。

## 触发方式

- 手动：在 Project 中选择目标 prefab，右键 `Bear/Postcard/补齐结构与组件`，核对路径后执行。
- Skill/命令行：调用 `Bear.Editor.PostcardPrefabStructureAligner.AlignFromCommandLine`，并传入 `-postcardTarget <Assets相对路径>`。
- 项目已被 Unity 打开时，不能再启动第二个 batchmode 实例；此时使用当前 Editor 的右键入口或公开的 `TryAlignPrefabAtPath`。

## 验证证据

- Unity 6000.0.59f2 已成功编译并重载工具脚本，右键菜单实际执行返回“成功 1/1”。
- Console 报告海盗主题识别 4 组拼图、选择 `Postcard_ocean.prefab`，目标 GUID 保持 `e91131f245c740541a1cfd6a5c163d02`。
- 对 Resources 最终输出的只读审计确认：magic 为 5 个 ProgressItem/CostBlock，pirate 为 4 个，toys 为 5 个；三者根节点均含 RectTransform、Animator、`Postcard_PageItem`，均含 1 个 `Postcard_CompleteMode.prefab`，没有缺少 universe 的脚本或嵌套 prefab 依赖。
- Prefab Mode 已目视检查：背景和角色为海盗主题，4 个拼图缺口、感叹号与 CostBlock 位置保留；层级包含 CompleteMode、Root、mainPage、角色和 4 个拼图组。
- Skill 经 `quick_validate.py` 验证通过。

## 剩余风险与未验证项

- 尚未运行 PlayMode，因此实际扣星解锁、翻片动画、完成态、关闭重开和存档链路仍待验证。
- Android/iOS 横屏与真机触摸未验证。
- 当前自动识别依赖目标图片命名 `UI_mingxinpian_img_<编号>[12]_01`、感叹号命名和 CostBlock 名称；不符合约定时会停止并报告，需人工调整映射后再执行。
- 工具会覆盖目标 prefab 内容；执行前应确认目标没有需要另行保留的非明信片业务组件。

## 回滚

- 单个海盗 prefab：用转换前备份恢复 `Postcard_pirate.prefab`，并保留现有 `.meta`，即可恢复扁平美术结构；Resources 下的三个新运行时 prefab 可按 Git 提交整体回退。
- 工具：删除 `Assets/Editor/PostcardPrefabStructureAligner.cs` 及其 `.meta`。
- Skill：回退 `D:/GitStore/BrinPunk/.agents/skills/brainpunk-align-postcard-prefab/` 的本次入口说明。

## 2026-08-19：运行时页面目录迁移

- 配合 Postcard 页面改由 YooAsset 内置托管，工具的运行时根目录和 `Postcard_universe.prefab` 参考路径已从 `Assets/Game/UI/Postcard/Resources/` 调整为 `Assets/Game/UI/Postcard/Prefabs/Pages/`。
- 模板选择、结构契约、GUID 保留、保存后校验和菜单/API 行为均未改变；前文中的 Resources 路径是迁移前历史记录。
- `Assembly-CSharp-Editor.csproj` 构建通过：0 error / 4 warnings。尚未在当前 Unity Editor 中实际执行一次对齐操作，因此目录选择、写回和 Prefab Mode 结果仍需后续样本验证。
- 回滚本项时必须与运行时资源回迁同步恢复工具路径；仅回滚工具会导致新生成资产写入旧目录，仅回滚资源目录会导致工具找不到参考模板。
