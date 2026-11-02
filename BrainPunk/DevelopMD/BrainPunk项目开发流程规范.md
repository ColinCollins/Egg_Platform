# BrainPunk 项目开发流程规范（简版）

创建时间：2026-07-01
用途：作为日常开发、Bug 修复、关卡修改和 AI 协作的快速流程入口。详细背景可参考 `DevelopMD/BrainPunk开发操作模式.md`。

## 1. 依据与适用范围

本规范依据以下本地规则整理：

- `AGENTS.md`
- `../.specify/memory/constitution.md`
- `DevelopMD/BrainPunk开发操作模式.md`
- 知识库 `D:\GitStore\Obsidian\01-Core\AI 工作流规则.md`
- 知识库 `D:\GitStore\Obsidian\04-Extensions\Git变更驱动风险评估工作流.md`

适用于本项目所有代码、关卡、Prefab、配置、资源、编辑器工具和验证文档相关工作。若与 `AGENTS.md` 或 constitution 冲突，以 `AGENTS.md` 和 constitution 为准。

## 2. 核心原则

1. 先确认边界，再实现：明确目标、范围、不做范围、验收标准和回滚方式。
2. 保护已验收内容：不为复用、整理或优化改变已发布关卡、存档、配置、打点、资源加载或平台流程。
3. 小范围改动：优先靠近变更点实现，避免无关重构和跨系统副作用。
4. 证据优先：代码结论、Prefab 结论和运行表现必须有本地文件、日志、测试或验收记录支撑。
5. 文档同步：新需求、Bug 修复或行为变化完成后，必须补齐本地中文文档、验证记录和风险说明。

## 3. 开始任务前

每次动手前至少完成：

1. 读取 `AGENTS.md`、constitution、当前 feature plan。
2. 查看 `git status --short`，识别已有未提交改动；默认不回滚、不覆盖用户改动。
3. 搜索 `DevelopMD/` 是否有相关模块文档。
4. 搜索目标类、Prefab、配置、事件和邻近代码。
5. 实现或修改 UI 逻辑时，先按功能、面板和模块名称阅读已有功能设计、使用记录及邻近说明，并结合脚本、Prefab、生成绑定和生命周期梳理原职责与复用边界；没有匹配文档时，明确记录缺口并以本地代码和 Prefab 证据补足分析。
6. 涉及 Editor 生命周期、Prefab 预览、Inspector、`OnValidate`、`ExecuteAlways` 时，先读 `DevelopMD/Editor态开发约束.md`。

## 4. 流程分级

### 4.1 轻量流程

适用于小 Bug、小 UI 调整、局部脚本修改、已有功能的小范围补充。

必须保留：

- 目标
- 范围和不做范围
- 预计改动文件
- 验收方式
- 风险与回滚
- 完成后的验证结果和未验证项

### 4.2 Speckit 完整流程

适用于新功能、新关卡、跨模块改动、存档/配置/资源/热更/SDK/打点相关改动，或任何高风险行为变化。

默认产物：

- `spec.md`：玩家目标、范围、成功标准、不可接受回归。
- `plan.md`：技术方案、宪法检查、原始行为基线、风险与回滚。
- `research.md`：已有系统证据和关键决策。
- `data-model.md`：状态、对象、生命周期和数据关系。
- `contracts/`：接口、Prefab 绑定或调用约定。
- `quickstart.md`：可执行验证清单。
- `tasks.md`：实现、验证和文档任务。

创建或修改规格前，优先按 `AGENTS.md` 要求使用独立 Git worktree。

## 5. 实现规则

- 关卡改动优先放在对应 `Assets/Game/Scripts/Level/LevelItem/Level_*` 局部实现。
- UI 逻辑优先复用已验证且职责匹配的现有组件、接口和流程；复用或共享抽象可能改变既有 UI 层级/遮罩、输入、生命周期、状态、资源、事件、存档或平台流程时，先完成影响评估和回归范围确认，并优先局部实现。
- 未经明确要求，不新增全局 `PlayEvents`；优先使用当前控制器可达对象、局部 API 或关卡内隔离实现。
- 未经明确要求，不直接改共享 `GamePlayPanel`；确需扩展时优先做子类或关卡专用面板组件。
- 不在编辑器态入口触发 DB、Save、RemoteConfig、manager 初始化、运行时实例化或运行时数据链。
- 运行时逻辑入口用 `Application.isPlaying` 做外层保护。
- 新增脚本、新增 public API、公共方法、公共属性或序列化字段时，补充中文注释说明用途、调用边界和注意事项。
- 不随意改写已有注释；只有注释与代码事实冲突时才调整，并在说明中写明原因。

## 6. 验证要求

按改动风险选择可执行验证，并记录结果：

- 静态检查：生命周期入口、全局事件、DB/Save/RemoteConfig、Instantiate、事件订阅和回调清理。
- 编译检查：Unity 编译、可用的 `dotnet build` 或说明无法执行原因。
- PlayMode 检查：进入、核心交互、失败、重开、退出、通关。
- 移动端检查：Android/iOS 横屏触控、布局、性能和平台差异。
- 回归检查：至少覆盖受影响旧关卡或相关旧流程。

无法验证的项目必须写成“未验证风险”，不能当作已通过结论。

## 7. 文档留存

完成新需求、Bug 修复或行为变化后，按影响范围更新文档：

- 通用事实：`DevelopMD/已有功能设计与使用记录/YYYY-MM-DD.md`（本地日期 Asia/Shanghai；同日只更新同一个文件）
- 模块功能：对应 `DevelopMD/` 模块文档
- 关卡功能：对应 `Level_*` 目录下的功能说明
- 关卡风险：对应 `Level_*` 目录下的风险评估
- Speckit 任务：更新对应 `tasks.md`、`quickstart.md` 或验证记录

原 `DevelopMD/已有功能设计与使用记录.md` 保留为只读历史记录，不追加、修改或迁移内容。
当天日期文件存在时更新对应主题章节，保留其它任务记录；不存在时才创建，不按任务另建同日副本。
查阅已有功能时同时检索历史文件与日期目录，具体写法见 `DevelopMD/已有功能设计与使用记录/README.md`。

新增文档和补充记录必须使用中文。文档至少包含：需求背景、影响文件和系统、变更前后行为、数据/资源/事件流、验证结果、未验证风险、回滚路径。

## 8. 收尾与 Git

- 日常开发分支必须基于 `1.9.0_common` 创建。可以按单个需求创建独立分支，也可以维护一个个人开发分支；无论哪种方式，都必须保证每次 commit 是完整、可说明、可回滚的闭环。
- 提交修改前必须先同步主干内容：从 `1.9.0_common` merge 最新内容到当前开发分支，处理冲突并完成必要验证后再提交。
- 用户验收前不默认 commit/push。
- 汇报时列出改动文件、验证结果、需要开发者在 Unity Editor 中操作的事项、未验证风险和回滚方式。
- 阶段性收尾或风险评估时，从最近 Git 变更和当前未提交 diff 出发，沿调用链、回调链、资源链、状态链、数据链和打点链扩散分析。
- 风险结论必须说明来源、扩散路径和优先级原因，禁止复制旧风险或泛泛按模块罗列。

## 9. 常用 Tools

本节只整理 Unity Editor 顶部菜单栏 `Tools/...` 下的常用工具，不包含仓库 `tools/` 目录里的命令行脚本。

- `Tools/Bear/Class Collector`
  用途：扫描脚本和指定 Prefab，生成类型保留信息和 `Assets/link.xml`。
  注意：会改 `link.xml`，用于处理 IL2CPP/Managed Stripping 裁剪风险；执行后要验证关卡 prefab 脚本保留情况。
- `Tools/Bear/Validate Level Prefab Script Preserve`
  用途：扫描 `Assets/Game/Prefabs/Level` 里的关卡 prefab，检查脚本类型是否被 `link.xml` 或 `[Preserve]` 保留。
  注意：打包、裁剪、ClassCollector 或关卡 prefab 变更后优先运行。
- `Tools/Luban/Generate Config`
  用途：执行 `Luban/Configs/gen.bat` 生成配置代码和数据，并同步配置文件名。
  注意：会覆盖生成代码和配置产物；执行前确认 Excel/JSON/YAML 改动来源。
- `Tools/Luban/Generate Config (Validate)`
  用途：检查 `Luban/Configs/gen.bat` 是否存在。
  注意：只做校验，不生成配置。
- `Tools/Luban/Generate Config (Update File Names)`
  用途：从 `Tables.cs` 提取配置文件名并更新 `ConfigFileNames.cs`。
  注意：配置生成后文件名列表不一致时使用。
- `Tools/Luban/Update LevelSort To Guru YAML/guru_spec.yaml`
  用途：把 LevelSort 配置同步到 `Assets/Guru/guru_spec.yaml`。
  注意：涉及远端配置/Guru 配置联动，执行后要检查 YAML diff。
- `Tools/Luban/Export GlobalConst To Guru/guru_spec.yaml`
  用途：把 GlobalConst 配置同步到 `Assets/Guru/guru_spec.yaml`。
  注意：会影响 RemoteConfig 默认值语义，执行后要检查关键字段。
- `Tools/Guru/RemoteConfig Default Values Viewer`
  用途：查看 `Assets/Guru/Gen/GuruAllSpecs.G.cs` 中的 RemoteConfig 默认值。
  注意：用于核对生成结果，不直接修改配置。
- `Tools/Config/Test Config Tables`
  用途：Play Mode 下测试 `ConfigManager` 初始化、重载和配置表读取。
  注意：必须在 Play Mode 使用，会访问运行时配置管理器。
- `Tools/飞书文档/拉取文档并保存为 CSV`
  用途：从飞书文档或表格拉取内容，保存到 `Luban/Configs/Datas/localization.csv`。
  注意：会覆盖本地 CSV；执行前确认飞书文档、权限和目标文件。
- `Tools/Localization/导出 I2Languages 文本到 TXT`
  用途：从 `Assets/Resources/I2Languages.asset` 导出已使用字符到 `I2Languages_chars.txt`。
  注意：常用于字体字符集补全。
- `Tools/Game/UI/TMP Font Batch Switcher`
  用途：批量替换指定目录 Prefab 内的 TMP 字体。
  注意：支持 Dry Run，正式替换前应先 Dry Run 并确认目标目录。
- `Tools/SpriteAtlas/Build From Selected Config`
  用途：基于选中的 `SpriteAtlasPackerConfig` 构建 SpriteAtlas。
  注意：需要先选中正确配置资产。
- `Tools/SpriteAtlas/Clean Unused Sprites In SceneDecoration`
  用途：清理 `Assets/Game/ArtSrc/Sprites/SceneDecoration` 下未被引用的 Sprite。
  注意：会删除资源，执行前必须确认 Git 状态和目标目录。
- `Tools/Bear/Auto Generate Skins Prefabs`
  用途：从 Skin Sprite 批量生成 Skin 部件 prefab。
  注意：会写入 `Assets/Game/Prefabs/Skins`，执行后检查 prefab 新增/更新结果。
- `Tools/UI Generator/...`
  用途：Bear UI 生成器，常用于从 JSON 或预置入口生成 UI 脚本/结构。
  注意：生成结果需要人工检查命名、绑定和 prefab 结构。
- `Tools/Save Module/...`
  用途：Bear Save Module 相关代码生成和 DB 设置管理。
  注意：涉及存档结构和 DBManager 生成，属于高风险工具；执行后必须做存档/读写回归。
- `Tools/Debug/...`
  用途：打开或关闭 BearLogger 调试能力，包括当前平台调试开关。
  注意：提交前确认目标平台日志开关符合发布要求。
- `Tools/DataViewer`
  用途：查看运行或数据相关内容。
  注意：用于排查，不应替代正式验证记录。
- `Tools/自动化打包/...`
  用途：编辑器内切换 Android/iOS 平台并执行 Debug、Release、DeepClean 打包。
  注意：打包前确认配置、证书、资源包、`link.xml`、ClassCollector 和目标平台。

通用边界：会生成代码、修改 prefab、删除资源、改配置、改 `link.xml` 或执行打包的工具，执行前必须确认 Git 状态和影响范围；执行后必须记录改动文件和验证结果。工具窗口中的 Editor 逻辑仍受 `DevelopMD/Editor态开发约束.md` 约束。

## 10. 禁止项

- 禁止未读规则、文档和邻近代码就改实现。
- 禁止把假设写成事实。
- 禁止为了抽象复用影响已验收关卡。
- 禁止无说明地改存档、配置、打点、资源加载、热更、SDK 或 ProjectSettings。
- 禁止只交代码，不留验证、文档和风险说明。
- 禁止忽略 Prefab、Inspector、资源引用和移动端横屏差异。

