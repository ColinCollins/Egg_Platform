# LocalizationPopup 功能说明

## 2026-07-14：自定义语言项首位插入

### 需求与原有行为

- `LocalizationPopup.AddItem(...)` 用于向当前语言弹窗会话追加一个自定义 `LanguageItem`，现有调用方是 Level65 的外星语言临时项。
- 修改前，自定义项通过 `Instantiate(itemPrefab, content)` 追加到语言列表末尾；接口没有可用的顺序控制，追加后也不会主动刷新父级滚动区域的位置。

### 当前行为

- `AddItem(LanguageItem itemPrefab, Action<LanguageItem> onClick, bool isFirst = true)` 增加末尾可选参数 `isFirst`，保留现有两参数调用兼容。
- `isFirst = true` 时，自定义项创建后通过 `SetAsFirstSibling()` 放到列表首位，并强制重建 `content` 布局。
- 布局刷新后会停止父级 `ScrollRect` 的残余惯性，并把 `verticalNormalizedPosition` 设为 `1`，使列表回到顶部。
- `isFirst = false` 时维持原来的末尾追加顺序，不主动改变当前滚动位置。
- 每次仍只保留一个 `customItemView`；新建前沿用 `ClearSessionCustomItem()` 清理当前会话旧项。

### 影响范围与数据流

- 修改脚本：`Assets/Game/UI/SettingPopup/Scripts/LocalizationPopup.cs`。
- 关联 prefab：`Assets/Game/UI/SettingPopup/Resources/LocalizationPopup.prefab`；静态 YAML 显示其 `ScrollRect.m_Content` 与脚本 `content` 指向同一个 `RectTransform`。
- 关联调用：`Assets/Game/Scripts/Level/LevelItem/Level_65/Level65Ctrl.cs` 继续用两参数形式追加外星语言项，默认获得首位插入和回顶行为。
- 本次不修改普通语言选择、`LocalizationManager`、`DB.GameSetting` 保存、配置表、资源地址、事件、分析打点或平台 SDK 流程。

### 验证记录

- 已执行目标脚本 `git diff --check`，未发现空白错误；仅有工作区 LF/CRLF 转换提示。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为 0 个错误、1116 个警告。
- 尚未运行 Unity PlayMode；需要验证首位层级、布局刷新、滚动回顶、`isFirst = false` 的末尾追加，以及重复打开弹窗时只保留一个临时项。
- Android/iOS 横屏触控与滚动手感尚未验证。

### 风险与回滚

- 如果 prefab 层级中找不到父级 `ScrollRect`，自定义项仍会创建并排序，但会输出 warning 且无法回顶。
- 强制布局重建只在 `isFirst = true` 的单次追加入口执行，不新增 `Update` 或持续分配路径；移动端实际开销仍待运行验证。
- 回滚时移除 `isFirst` 参数、`SetAsFirstSibling()` 与 `RefreshScrollViewToTop()`，即可恢复末尾追加且不改变滚动位置的原行为。
