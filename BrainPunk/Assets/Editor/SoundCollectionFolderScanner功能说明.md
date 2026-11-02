# SoundCollection 文件夹扫描工具功能说明

## 需求与背景

- 美术或开发人员在 Project 窗口指定音频文件夹后，需要批量把其中的音频补充到 Sounds Good 的 `SoundCollection.asset`。
- 新条目的 tag/name 与不含扩展名的音频文件名保持一致；遇到重名时必须提示且不得覆盖既有条目。

## 影响范围

- 工具脚本：`Assets/Editor/SoundCollectionFolderScanner.cs`。
- 目标资产：`Assets/SoundsGood/Data/Collections/SoundCollection.asset`，仅在手工执行右键菜单后写入。
- 生成代码：`Assets/SoundsGood/Data/Generated/SFX_Generated.cs`，仅在实际新增条目后通过 Sounds Good 既有保存入口刷新。
- 扫描来源：用户在 Project 窗口右键选中的文件夹，包含其子目录中的 `AudioClip` 资产。

## 原有行为基线

- Sounds Good 自带 Audio Creator 需要逐条指定 tag 与 AudioClip，再调用 `SoundDataCollection.CreateSound(...)` 并刷新 `SFX_Generated.cs`。
- 项目此前没有从 Project 文件夹右键批量扫描音频并补充 SoundCollection 的本地工具。

## 新行为

- Project 文件夹右键菜单新增 `Assets/Bear/扫描音频并补充到 SoundCollection`；入口保持可点击，执行后才检查 PlayMode 和文件夹选择，并用弹窗说明不能执行的原因。
- 工具递归查找当前文件夹中的 `AudioClip`，按资源路径稳定排序，以文件名（不含扩展名）作为 SoundCollection tag。
- 新条目使用 Sounds Good 的 `FrequentSound` 分类并保持 `forceToMono=false`，但工具不改写 AudioImporter 设置。
- 与既有 SoundCollection tag 重名时按不区分大小写比较并跳过；扫描目录内部出现同名文件时整组跳过。完成弹窗和 Console 都会给出重名警告与具体路径。
- 为保证 `SFX_Generated.cs` 可编译，不能作为 C# 字段名的文件名会跳过并提示；下划线属于合法字符，名称不会被改写。
- 实际新增时记录 Unity Undo、标记 SoundCollection dirty，并复用 Sounds Good 既有保存和 SFX 生成入口。

## 数据、资源与事件流

1. 用户右键 Project 文件夹并执行菜单。
2. `AssetDatabase.FindAssets("t:AudioClip")` 只读取所选文件夹及子目录。
3. 工具加载固定路径的现有 `SoundCollection.asset`，完成重名和名称合法性检查。
4. 合格音频通过 `SoundDataCollection.CreateSound(...)` 追加；不会覆盖、删除或重排既有条目。
5. 有新增时调用 Sounds Good Editor 保存入口，保存资产并刷新 `SFX_Generated.cs`。

该流程不访问 DB、Save、RemoteConfig、运行时单例、事件、分析、广告或平台 SDK，也不会实例化运行时对象。

## 验证记录

- 已静态核对 `SoundDataCollection.CreateSound(...)`、`EditorHelper.SaveCollectionChanges(...)` 和现有 `SFX_Generated.cs` 的生成链。
- 已检查工具入口受 `EditorApplication.isPlayingOrWillChangePlaymode` 限制，脚本未包含 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Instance 或 Instantiate 路径。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo -v:minimal` 通过：0 error、1121 个项目既有 warning；验证时临时加入的 csproj 编译项已移除。
- `git diff --check` 未发现空白错误。
- 待完成：Unity Editor 中对 `Assets/Game/ArtSrc/Audio/Sfx/DailyChallenge` 的首次扫描和重复扫描实测。

## 2026-07-22 菜单不可用修复

- 修复 `CSharpKeywords` 声明中变量名缺失导致的 C# 编译错误。
- 移除依赖 `Selection.activeObject` 的 `MenuItem` 启用校验回调，避免 Unity 在 Project 右键上下文尚未稳定时提前把菜单置灰。
- PlayMode 与文件夹合法性检查保留在实际点击入口的最外层；不满足条件时只显示原因，不扫描或写入资产。
- 修复后重新执行 Editor C# 工程构建，结果为 0 error、1121 个项目既有 warning。

## 2026-07-22 Project 左侧目录树右键兼容

- 实测从 Project 左侧目录树右键 `DailyChallenge` 时，菜单能够点击，但 `Selection.activeObject` 没有提供该文件夹，工具因此误报“请选择文件夹”。
- 菜单入口现接收 Unity 传入的 `MenuCommand.context`，优先使用实际右键对象；普通 Project 选择继续回退到 `Selection.activeObject` 和 `Selection.assetGUIDs`。
- 该调整只改变 Editor 文件夹路径的取得方式，不改变音频扫描、重名判断、SoundCollection 写入或 SFX 生成逻辑。
- 修复后 Editor C# 工程增量构建通过：0 error、4 个项目既有 warning。
- Unity 左侧目录树右键 `DailyChallenge` 已实际执行成功：`SoundCollection.asset` 追加 `DC_number`、`DC_OpenChest`、`DC_PopIn`、`DC_process`，四个 AudioClip GUID 均与对应 `.ogg.meta` 一致；`SFX_Generated.cs` 同步生成四个同名字段。
- 新增条目和生成代码落盘后再次构建 Editor C# 工程，结果为 0 error、104 个项目既有 warning。
- 重复执行后的重名警告弹窗仍待手工复验。

## 剩余风险与未验证项

- Sounds Good 的内部保存入口通过反射复用；插件若更改程序集、类型或方法名，SoundCollection 仍会保存，但自动刷新 `SFX_Generated.cs` 会失败并在 Console 明确报警。
- 尚未在 Unity Editor 右键实测，因此菜单显示、Undo、首次新增、重复扫描和弹窗内容仍需验证。
- 工具不更改音频压缩、单声道、预加载等导入配置，相关参数仍以现有 `.meta` 或人工配置为准。

## 回滚路径

- 删除 `SoundCollectionFolderScanner.cs` 及其 `.meta` 可移除右键工具。
- 若已执行扫描，可通过 Unity Undo，或从 `SoundCollection.asset` 删除本次新增条目后使用 Sounds Good 编辑器重新生成 `SFX_Generated.cs`。
