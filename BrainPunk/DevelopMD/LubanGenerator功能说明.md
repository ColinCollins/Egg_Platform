# LubanGenerator 功能说明

## 当前有效范围：2026-10-12 Dig 远端配置闭环

用户要求新增 Global 字段时同时完成远端配置链，本轮事后复核确认下面链路已经在 Dig 第二轮实现；本节更新早期章节仅描述主线/Draw 的范围，原章节作为历史演进保留。

- Unity 统一入口是 `Tools/Luban/Update All Configs To Guru YAML`，依次合并主线排序、主线常量、Draw、IWanner 和 Dig，五步全部校验成功才写入一次 YAML，并触发一次 Guru 官方生成菜单。不要将五步同步理解为五个 remote key：运行时正式 key 为 Main/Draw/IWanner/Dig 四个。
- `GlobalConstDigGameConfigKeys` 包含14个 `digGame` 字段，`UpdateDigGameConfigToGuruSpec` / `ApplyDigGameConfigToYamlAsset` 支持独立同步；统一菜单调用同一 `TryMergeDigGameConfig`。Dig 排序取 `Assets/Game/Configs/tbdiglevelsort.json` 的30项 Path（真实数据ID），生成 `diggame_config.level_sort`，不导出其它玩法排序。
- 输入为实际 Luban Global JSON和Dig排序 JSON，输出为 `Assets/Guru/guru_spec.yaml` 的独立 `diggame_config`，官方模板输出位于 `Assets/Guru/Gen/GuruAllSpecs.G.cs`。保留已有 enabled 和未知扩展字段，更新已知字段及排序；缺字段/旧JSON损坏/缺锚点时拒绝，不部分改写。
- Runtime 已具备14个字段常量及11个 `GetDigGame…` getter（3个区间字段由间隔/失败 getter 消费）；优先读取 `diggame_config`，缺字段或类型解析失败回退 Dig Luban。首页/模式入口/OneLink、提示与跳关价格、等待/失败提示、Dig独立插屏策略和每日免广告模块均已使用这些入口，不是未消费的默认字段。
- `RemoteConfigService.RemoteConfigKeys` 已包含 Dig；缓存、初始化与运行期候选验证、广告/暂停/转场延期、保存成功后同步更新排序/已有UI均有游戏侧接入。该范围已获用户确认，ForceFetch次数、5秒/10秒预算与Loading顺序保留。
- 本次修正统一菜单无变化提示中的“四组”为“五组”，原因是实际流程已包含Dig。额外执行当前生成器纯合并/序列化方法的27项断言，覆盖14字段、30项整数排序、生产默认语义相等、非Dig行保留、enabled/未知字段保留、失败不改写与地区数字格式；生成器程序集编译0错误。既有运行时51项隔离断言另行复核。
- 真实 Unity 菜单导出/官方生成菜单和 Firebase 服务端发布、设备联网/广告恢复仍为 `uncertain`；本次测试使用实际纯方法，不写生产 YAML，也不发布远端值。未改写只读 Packages/GuruSDK/GF。

闭环逐字段清单和验证源见 `DevelopMD/验证记录/DigGame选关云控_2026-10-12/RemoteConfig与LubanGenerator闭环复核.md`。回滚本轮复核只需恢复提示文字和这次文档增量；不能据此移除已接入的14字段、远端消费者或改写启动拉取。

## 1. 脚本定位

`Assets/Game/Editor/LubanGenerator.cs` 是一个只在 `UNITY_EDITOR` 下生效的静态编辑器工具，不参与运行时逻辑。

它主要负责 5 件事：

1. 从 Unity 菜单执行 `Luban/Configs/gen.bat`。
2. 把本地 `tblevelsort.json` 的关卡顺序写回 `guru_spec.yaml`。
3. 把本地 `lubanconfig_tbglobalconst.json` 的字段合并写回 `guru_spec.yaml` 的 `level_config`。
4. 把 DrawGame GlobalConst 字段与 `tbdrawlevelsort.json` 顺序写入 `drawgame_config`。
5. 根据 Luban 生成的 `Tables.cs` 自动刷新 `ConfigFileNames.cs`。

相关代码：`LubanGenerator.cs:22-33`、`LubanGenerator.cs:64-114`、`LubanGenerator.cs:669-877`

## 2. Unity 菜单入口

脚本注册了以下菜单：

- `Tools/Luban/Generate Config`
- `Tools/Luban/Generate Config (Validate)`
- `Tools/Luban/Generate Config (Update File Names)`
- `Tools/Luban/Update LevelSort To Guru YAML/guru_spec.yaml`
- `Tools/Luban/Export GlobalConst To Guru/guru_spec.yaml`
- `Tools/Luban/Update DrawGameConfig To Guru YAML/guru_spec.yaml`

对应代码：

- 生成配置：`LubanGenerator.cs:64-93`
- 校验 `gen.bat`：`LubanGenerator.cs:726-750`
- 手动更新文件名：`LubanGenerator.cs:873-877`
- 同步 LevelSort：`LubanGenerator.cs:96-108`
- 同步 GlobalConst：`LubanGenerator.cs:111-114`

## 3. 功能一：执行 Luban 配置生成

`GenerateConfig()` 的流程如下：

1. 通过 `Application.dataPath` 反推出项目根目录。
2. 拼出 `Luban/Configs/gen.bat` 的绝对路径。
3. 校验路径是否存在。
4. 弹确认框提示用户。
5. 通过 `Process.Start` 启动 `.bat`。
6. 通过两次 `EditorApplication.delayCall` 延迟刷新资源，并调用 `UpdateConfigFileNames()`。

相关代码：

- 定位 `gen.bat`：`LubanGenerator.cs:269-297`
- 执行生成：`LubanGenerator.cs:669-720`
- 入口菜单：`LubanGenerator.cs:64-93`

这个功能的主要输入输出：

- 输入：`Luban/Configs/gen.bat`
- 输出：`gen.bat` 本身生成的 Luban 产物
- 附带输出：`Assets/Game/Scripts/Config/ConfigFileNames.cs`

实现特点：

- 使用 `UseShellExecute = true`，直接启动系统批处理。
- 没有等待进程退出，而是依赖编辑器延迟回调刷新。
- 启动失败或文件缺失时会弹框并写日志。

## 4. 功能二：同步 LevelSort 到 Guru YAML

`UpdateLevelSortToGuruSpec()` 会调用 `ApplyLevelSortTableToYamlAsset()`。

目标是把 `Assets/Game/Configs/tblevelsort.json` 中的 `path` 顺序写回 `Assets/Guru/guru_spec.yaml`。

相关代码：`LubanGenerator.cs:96-108`、`LubanGenerator.cs:186-264`

具体做法：

1. 校验 `tblevelsort.json` 和目标 YAML 是否存在。
2. 用正则从 JSON 中提取全部 `"path":"xxx"`。
3. 弹确认框，预览数量和前 15 个值。
4. 把结果拼成 JSON 数组文本。
5. 在 YAML 中定位 `level_config: '{...}'` 这一行。
6. 只替换其中 `levelsort` 或 `level_sort` 数组。
7. 保留其他 YAML 内容和原有键名。
8. 写回文件并调用 `AssetDatabase.ImportAsset(...)`。

相关代码：

- 提取 `path`：`LubanGenerator.cs:300-325`
- 替换 `levelsort` / `level_sort`：`LubanGenerator.cs:447-523`

这个功能的关键约束：

- 它假设 YAML 中的 `level_config` 是单行、单引号包裹的 JSON。
- 如果 `level_config` 改成多行结构化 YAML，这个工具会失效。
- 它只提取 `path` 字段，不解析完整表结构。

相关代码：`LubanGenerator.cs:469-478`、`LubanGenerator.cs:502-507`

## 5. 功能三：同步 GlobalConst 到 Guru YAML

`UpdateGlobalConstToGuruSpec()` 会调用 `ApplyGlobalConstTableToYamlAsset()`，把 `Assets/Game/Configs/lubanconfig_tbglobalconst.json` 的首行数据合并到 `Assets/Guru/guru_spec.yaml` 的 `level_config`。

相关代码：`LubanGenerator.cs:111-184`、`LubanGenerator.cs:328-442`

脚本会同步 `GlobalConstLevelConfigKeys` 中定义的字段，包括：

- `showTipsFailCount`
- `showTipsTimeLimit`
- `ad_interstitialCD`
- `ad_rewardResetInterstitialCD`
- `ad_interstitialShowMinLevel`
- `ad_interstitialShowIntervalCount`
- `ad_interstitialShowFailedCount`
- `showRatingLevel`
- `open_gaps`
- `victoryHorizantalRewards`
- `gameTipsPopupCoinCost`
- `gameTipsPopupPassLevelCoinCost`
- `ad_dailyNOAdPopupAutoShowCount`
- `shop_adfreeCoinAutoShowCount`
- `currentRemoteConfigVersion`
- `showVersionBoard`
- `dailyChallengeRewardMultiple`

其中有一个当前的特殊处理：

- `currentRemoteConfigVersion` 在导出到 `guru_spec.yaml` 时，不直接使用 `lubanconfig_tbglobalconst.json` 里的值。
- 该字段会统一使用当前 Unity 工程的 `Application.version` 填充。

同时会删除旧字段：

- `victoryHorziontalRewards`
- `shopFreeCoinShowCount`

相关代码：`LubanGenerator.cs:38-62`、`LubanGenerator.cs:418-431`

具体做法：

1. 用 `JArray.Parse` 解析 `lubanconfig_tbglobalconst.json`。
2. 取数组第一行 `JObject` 作为同步源。
3. 生成字段预览并弹确认框。
4. 对 `currentRemoteConfigVersion` 单独改用 `Application.version` 作为导出值。
5. 定位 YAML 中 `level_config: '{...}'` 这一行。
6. 用 JSON 方式覆盖目标字段。
7. 删除旧字段。
8. 按原有键顺序优先的方式重新序列化并写回。

相关代码：

- 读取首行：`LubanGenerator.cs:328-349`
- 生成预览：`LubanGenerator.cs:353-389`
- 合并字段：`LubanGenerator.cs:391-442`
- 重新序列化：`LubanGenerator.cs:526-664`

实现特点：

- 缺字段时直接报错，不做部分写入。
- `currentRemoteConfigVersion` 是例外：该字段始终由 `Application.version` 生成，不依赖 Global 表里的原始值。
- 数值序列化使用 `InvariantCulture`，避免本地化格式污染 JSON。
- 对数组、布尔、整数、浮点、字符串都做了显式序列化。

## 6. 功能四：自动维护 ConfigFileNames.cs

脚本会根据 Luban 生成的 `Assets/Configs/Code/Tables.cs`，更新 `Assets/Game/Scripts/Config/ConfigFileNames.cs` 中的配置文件名列表。

相关代码：`LubanGenerator.cs:756-877`

具体做法：

1. 读取 `Tables.cs`。
2. 用正则匹配 `loader("filename")`。
3. 去重收集文件名。
4. 找到 `ConfigFileNames.GetFileNames()` 中的 `return new string[] { ... };`。
5. 用新数组替换旧数组。
6. 写回并导入资源。

相关代码：

- 提取 loader：`LubanGenerator.cs:756-795`
- 写回 `ConfigFileNames.cs`：`LubanGenerator.cs:800-867`

当前 `Tables.cs` 可见的配置名来源如下：

- `lubanconfig_tbglobalconst`
- `tbgaps`
- `tblanguage`
- `tbleveldata`
- `tblevelsort`
- `tbproducts`
- `tbshop`

相关代码：

- `Assets/Configs/Code/Tables.cs:21-31`
- `Assets/Game/Scripts/Config/ConfigFileNames.cs:11-21`

## 7. 主要输入输出文件

主要输入：

- `Luban/Configs/gen.bat`
- `Assets/Game/Configs/tblevelsort.json`
- `Assets/Game/Configs/lubanconfig_tbglobalconst.json`
- `Assets/Configs/Code/Tables.cs`

主要输出：

- `Assets/Guru/guru_spec.yaml`
- `Assets/Game/Scripts/Config/ConfigFileNames.cs`
- 以及 `gen.bat` 生成的其他 Luban 产物

## 8. 维护注意事项

需要重点注意 3 个实现前提：

1. `level_config` 的定位依赖正则 `^(?<indent>\\s*)level_config:\\s*'(?<json>\\{[^']*\\})'\\s*$`，因此 YAML 格式不能随意改。
2. `ConfigFileNames.cs` 的更新依赖 `Tables.cs` 中仍然保留 `loader("...")` 这种代码形态。
3. `GlobalConstLevelConfigKeys` 里的键名必须与 Guru spec 和运行时读取链保持一致。

相关代码：

- YAML 匹配：`LubanGenerator.cs:502-507`
- loader 提取：`LubanGenerator.cs:771-783`
- GlobalConst 键集合：`LubanGenerator.cs:38-62`

## 9. 建议使用顺序

如果本次改动涉及 Luban、关卡排序或 Guru 默认配置，建议按这个顺序使用：

1. 先修改表、JSON 或 `guru_spec` 相关源文件。
2. 执行 `Tools/Luban/Generate Config`。
3. 如改了关卡顺序，再执行 `Update LevelSort To Guru YAML/...`。
4. 如改了远端默认值，再执行 `Export GlobalConst To Guru/guru_spec.yaml`。
5. 最后检查 `guru_spec.yaml`、`ConfigFileNames.cs` 和生成产物是否符合预期。

## 10. 结论

`LubanGenerator` 的本质是一个 Unity Editor 侧的桥接工具，用来把 Luban 生成流程、本地配置表和 Guru 默认配置串起来，减少手工同步 `tblevelsort`、`GlobalConst`、`guru_spec` 与 `ConfigFileNames` 时的漏改风险。

## 11. 2026-07-23 DailyChallenge 奖励倍率同步

### 11.1 需求与原有缺口

- `GlobalConst` 已新增 `dailyChallengeRewardMultiple`，运行时也需要通过 `level_config` 接受 RemoteConfig 控制。
- 原 `GlobalConstLevelConfigKeys` 白名单未包含该字段，因此即使 Luban JSON 已生成倍率，执行“Export GlobalConst To Guru”仍不会把它合并到 `guru_spec.yaml`。

### 11.2 当前行为

- `GlobalConstLevelConfigKeys` 新增 `dailyChallengeRewardMultiple`。
- `Assets/Guru/guru_spec.yaml` 的 `level_config` 当前默认值同步为 `2`。
- 后续执行 `UpdateGlobalConstToGuruSpec()` 时，会从 `lubanconfig_tbglobalconst.json` 读取该字段并覆盖同名默认值；RemoteConfig 顶层 key 仍是既有 `level_config`。

### 11.3 验证、风险与回滚

- 静态检查确认源 Luban JSON、生成 `GlobalConst`、生成器白名单、Guru `level_config` 和运行时读取 key 五处名称完全一致。
- `guru_spec.yaml` 的单行 `level_config` JSON 已完成反序列化校验，`dailyChallengeRewardMultiple` 为整数 `2`。
- 尚未在 Unity Editor 实际点击菜单并确认弹窗预览、文件写回与 `AssetDatabase.ImportAsset(...)`；远端后台发布仍不在本工具职责内。
- 回滚时从白名单和 Guru `level_config` 删除该键；如同时回滚完整功能，还需回退 Global 表、Luban 产物和运行时读取入口。

## 12. 2026-07-24 LevelData / LevelSort 多 Sheet 手动拆表

### 12.1 需求与原有行为

- `#LevelData.xlsx` 和 `#LevelSort.xlsx` 原先使用 Luban 的 `#*.xlsx` 自动导入规则；同一个工作簿内新增 Sheet 后，Luban 会尝试按同一张自动表合并读取，无法把不同字段结构的 Sheet 自动生成成独立配置。
- 直接保留 `#` 前缀生成时，`draw_level` 会因缺少主表字段 `isGiveHints` 失败，`drawLevelSort` 会与 `defaultSort` 产生重复主键，说明两个新增 Sheet 不能沿用原自动合表方式。
- 本次采用手动表定义方案：用户已将源文件改名为 `LevelData.xlsx` 和 `LevelSort.xlsx`，避免自动导入；再由 `__tables__.xlsx` 明确声明每个 Sheet 对应的表。

### 12.2 当前表定义与生成结构

`Luban/Configs/Datas/__tables__.xlsx` 新增以下四条手动映射：

| 表名 | 数据类型 | 输入 Sheet |
| --- | --- | --- |
| `TbLevelData` | `LevelData` | `level@LevelData.xlsx` |
| `TbDrawLevelData` | `DrawLevelData` | `draw_level@LevelData.xlsx` |
| `TbLevelSort` | `LevelSort` | `defaultSort@LevelSort.xlsx` |
| `TbDrawLevelSort` | `DrawLevelSort` | `drawLevelSort@LevelSort.xlsx` |

- 原有运行时入口仍是 `TbLevelData` 和 `TbLevelSort`，对应文件名仍为 `tbleveldata`、`tblevelsort`。
- 新增生成入口为 `TbDrawLevelData` 和 `TbDrawLevelSort`，对应 JSON 为 `tbdrawleveldata.json`、`tbdrawlevelsort.json`。
- `Assets/Configs/Code/Tables.cs` 已生成四个表的加载与引用解析代码。
- `Assets/Game/Scripts/Config/ConfigFileNames.cs` 已补入两张新表的文件名，使现有配置预加载流程能够取得全部 `Tables` loader 依赖。
- 本次没有把 Draw 表接入 `LevelCtrl` 或远端关卡排序替换逻辑；它们目前只完成独立生成和加载，具体玩法消费入口待后续需求明确后单独实现。

### 12.3 数据与资源流

1. `LevelData.xlsx` 的 `level`、`draw_level` 分别生成普通关卡和 Draw 关卡数据表。
2. `LevelSort.xlsx` 的 `defaultSort`、`drawLevelSort` 分别生成普通关卡和 Draw 关卡排序表。
3. Luban 生成 `Assets/Configs/Code/*.cs` 与 `Assets/Game/Configs/*.json`。
4. `ConfigFileNames` 预加载四个 JSON，`Tables` 再按对应 loader 构造四张表。
5. 当前 gameplay 仍只读取普通关卡两张表，因此本次不改变已发布关卡选择、远端排序、存档或资源加载行为。

### 12.4 验证证据

- Luban 命令连续执行两次均成功；第二次没有新增或覆盖提示，生成结果稳定。
- 改动前后 `tbleveldata.json`、`tblevelsort.json`、`LevelData.cs`、`LevelSort.cs`、`TbLevelData.cs`、`TbLevelSort.cs` 的 SHA-256 完全一致，证明原有两张表的数据与生成类型契约未改变。
- `Tables.cs` 的 13 个 loader 与 `ConfigFileNames.cs` 的 13 个预加载名一一对应，没有缺失或额外项。
- 新数据统计：`TbDrawLevelData` 6 条，ID 为 `20001`～`20005`、`20999`；`TbDrawLevelSort` 5 条，路径引用 `20001`～`20005`。两张新表内部均无重复主键，排序表没有缺失的数据引用。
- Unity Editor 日志记录了脚本重新编译、程序集成功重载，以及两个新 JSON 的导入；相关日志中未发现 C# 编译错误。
- 独立 `dotnet build BrainPunk.slnx --no-restore` 在 60 秒后超时，因此不能作为本次完整解决方案构建证据；超时启动的诊断 MSBuild 子进程已停止。
- 当前环境缺少 `@oai/artifact-tool`、LibreOffice 和可用的 Excel 命令行，未完成工作簿视觉渲染；已通过程序化检查确认新增行的单元格值正确，且 A:K 样式 ID 与相邻既有数据行一致。

### 12.5 风险、未验证项与回滚

- `draw_level` 中的 `20999` 当前没有被 `drawLevelSort` 引用；其用途为 `uncertain`，本次保留源数据，不擅自补入排序或删除。
- 尚未执行 Unity PlayMode、真机、YooAssets 打包或远端配置联调；后续真正接入 Draw 玩法前，需要确认表选择规则、排序切换边界和缺表降级行为。
- 回滚时应恢复 `#LevelData.xlsx`、`#LevelSort.xlsx` 命名，删除 `__tables__.xlsx` 中四条手动映射，重新执行 Luban，并同步移除新增 Draw 生成代码、JSON、`.meta` 与 `ConfigFileNames` 两项。不要只手工修改生成代码，否则下次生成会再次覆盖。

## 13. 2026-07-29 Skin / SkinShop 多 Sheet 手动拆表

### 13.1 需求与原有行为

- 用户把原 `#Skin.xlsx`、`#SkinShop.xlsx` 改为无 `#` 前缀的 `Skin.xlsx`、
  `SkinShop.xlsx`，并分别增加 `pencilSkin`、`pencilSkinShop` Sheet。
- 去掉 `#` 后，Luban 不再按自动表规则导入原 `actorSkin`、`actorSkinShop`；
  修改前 `__tables__.xlsx` 也没有对应手动定义，因此旧 Skin 表和新增 PencilSkin
  表都缺少稳定导出入口。
- 本次只补手动表定义、生成产物和预加载名单，不修改用户填写的 Skin/SkinShop
  单元格内容，不接入 UI、存档、资源切换、广告、内购或运行时消费逻辑。

### 13.2 当前表定义与生成结构

`Luban/Configs/Datas/__tables__.xlsx` 末尾新增四条映射：

| 表名 | 数据类型 | 输入 Sheet | JSON |
| --- | --- | --- | --- |
| `TbSkin` | `Skin` | `actorSkin@Skin.xlsx` | `tbskin.json` |
| `TbPencilSkin` | `PencilSkin` | `pencilSkin@Skin.xlsx` | `tbpencilskin.json` |
| `TbSkinShop` | `SkinShop` | `actorSkinShop@SkinShop.xlsx` | `tbskinshop.json` |
| `TbPencilSkinShop` | `PencilSkinShop` | `pencilSkinShop@SkinShop.xlsx` | `tbpencilskinshop.json` |

- 原 `TbSkin`、`TbSkinShop` 的表名、数据类型和输出文件名保持不变。
- Luban 新增生成 `PencilSkin`、`TbPencilSkin`、`PencilSkinShop`、
  `TbPencilSkinShop` 四个 C# 类型，并把两张新表加入 `Tables` 构造与引用解析。
- `ConfigFileNames.GetFileNames()` 新增 `tbpencilskin`、`tbpencilskinshop`，
  当前 15 个 `Tables` loader 与 15 个预加载名一一对应。

### 13.3 数据与加载流

1. `actorSkin`、`pencilSkin` 分别生成 Actor 皮肤与铅笔皮肤数据。
2. `actorSkinShop`、`pencilSkinShop` 分别生成对应商店映射数据。
3. Luban 输出 C# 类型到 `Assets/Configs/Code`，输出 JSON 到
   `Assets/Game/Configs`。
4. `ConfigFileNames` 预加载全部 JSON，`Tables` 再通过同名 loader 构造四张表。
5. 当前 `Assets/Game` 脚本中未检索到 `TbPencilSkin` 或
   `TbPencilSkinShop` 消费入口；本次交付范围是可生成、可预加载，具体业务使用仍待后续
   PencilSkin 功能接入。

### 13.4 验证证据

- 使用 `@oai/artifact-tool` 检查并渲染两个源工作簿和 `__tables__.xlsx`；候选表
  导出回读后，原 `A1:K9` 值与公式完全一致，新增四行样式沿用既有手动定义行。
- 在隔离目录与实际工程各执行一次 Luban，均以退出码 0 完成；新增 JSON 分别为
  `TbPencilSkin` 7 条、`TbPencilSkinShop` 15 条，两个表内部均无重复主键。
- 修改前后 `Skin.cs`、`TbSkin.cs`、`SkinShop.cs`、`TbSkinShop.cs`、
  `tbskin.json`、`tbskinshop.json` 的 SHA-256 全部一致，证明原表生成类型和数据未变。
- 生成器带出的无关 `tblevelsort.json` 差异已按 Git HEAD 精确恢复，未留在本次改动中。
- `Tables.cs` loader 与 `ConfigFileNames.cs` 预加载名集合一致，无缺失或额外项。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal
  /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false
  /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 101 warnings。
- `git diff --check` 通过；尚未执行 Unity PlayMode、移动端构建或资源包加载验证。

### 13.5 风险、未验证项与回滚

- `pencilSkin` 当前 ID 为 `301001`～`301007`，但 `pencilSkinShop.skinId`
  当前引用 `201xxx/202xxx`，全部 15 个引用都不在 `pencilSkin` ID 集合中。该数据来自
  用户工作簿，是否有意跨表引用为 `uncertain`；本次不擅自改表，业务接入前必须确认。
- 当前没有 PencilSkin 配置消费代码证据，因此无法验证 UI 展示、购买、拥有状态、存档、
  材质资源加载或画线表现；Android/iOS 横屏行为也未验证。
- 回滚时删除 `__tables__.xlsx` 中四条 Skin 手动映射并恢复原自动表命名策略，重新执行
  Luban；同步移除 PencilSkin 四个生成类型、两个 JSON 及其 `.meta`，从
  `ConfigFileNames` 删除两个新文件名。若仍保留无 `#` 工作簿，则必须另行保留旧
  `TbSkin`、`TbSkinShop` 手动映射，否则原 Skin 配置也会停止导出。

## 14. 2026-08-04 DrawGameConfig 同步工具

- 新增菜单 `Tools/Luban/Update DrawGameConfig To Guru YAML/guru_spec.yaml`。
- 工具从 `lubanconfig_tbglobalconst.json` 读取 9 个 `drawGame*` 字段，从 `tbdrawlevelsort.json` 读取当前 path 顺序，生成或更新 `guru_spec.yaml` 的单行 `drawgame_config` JSON。
- 新建配置时默认保留 `enabled=false`；重复执行时保留已有 `enabled` 与未知扩展字段，只覆盖 DrawGame 专用字段和 `level_sort`。
- 当前 Guru 默认 Draw 顺序包含 33 条表内 path，末项为 `20039`；`20999` 与主线 `10999` 一样由运行时控制器追加，不写入表内远端排序数组。
- 工具属于 Editor 文件读写，不访问 DB、运行时单例或存档链；修改前执行后仍需手动运行 Guru Spec 官方生成，才能更新 `GuruAllSpecs.G.cs`。

## 15. 2026-08-05 GuruAllSpecs 自动生成串联

### 15.1 需求与原有行为

- `Generate Config (Update File Names)`、`Update LevelSort To Guru YAML`、
  `Export GlobalConst To Guru`、`Update DrawGameConfig To Guru YAML` 四个菜单完成后，
  原流程不会继续更新 `Assets/Guru/Gen/GuruAllSpecs.G.cs`，需要开发者再手动执行
  `Guru/Guru Spec/Auto-Gen <GuruAllSpecs.G.cs>`。
- 该手工步骤容易遗漏，使 `guru_spec.yaml` 与生成代码短时间内不一致。

### 15.2 当前行为与调用边界

- `LubanGenerator` 新增统一的 `AutoGenerateGuruAllSpecs()` 私有入口，通过
  `EditorApplication.ExecuteMenuItem(...)` 调用 Guru 包已经注册的官方生成菜单，
  不复制或改写 Guru 包内部生成逻辑。
- 上述四个菜单在自身操作正常完成后会自动触发官方生成；YAML 内容已经一致、无需写回时，
  也会触发生成，以保证现有 `guru_spec.yaml` 可以重新产出代码。
- 用户取消确认、输入文件缺失、解析失败或写入异常时，不触发后续生成。
- 文件名更新内部现在返回成功状态；没有提取到配置名、目标文件缺失或发生异常时，手动菜单会显示失败提示并停止串联。
- `Tools/Luban/Generate Config` 与 `Generate Config (Validate)` 不在本次自动串联范围内；
  前者当前是独立启动外部 `gen.bat` 的异步流程，后者只负责路径校验。
- 如果 Guru 菜单没有加载，工具会写错误日志并显示失败对话框；原 Luban/YAML 操作的结果不回滚。

### 15.3 数据、资源与生命周期影响

1. Luban 文件名维护或 YAML 同步先按原逻辑完成。
2. 工具随后调用 Guru 官方菜单。
3. Guru 包继续以 `Assets/Guru/guru_spec.yaml` 为输入，更新
   `Assets/Guru/Gen/GuruAllSpecs.G.cs`、对应 asmdef/link 文件及其负责的插件配置资源。
4. 本次变更仅运行在 Unity Editor 菜单入口，不访问 DB、存档、运行时单例或场景对象，
   不改变 Android/iOS 玩家运行时流程。

### 15.4 验证、剩余风险与回滚

- 静态证据确认 Guru 包在
  `Packages/com.guru.sdk.framework.guruspec/Editor/GuruSpecGenerator/Code/GuruSpecGeneratorEditorMenu.cs`
  注册的菜单路径与本工具调用常量完全一致。
- 已检查四个入口：三个 YAML 工具的“写入成功”和“内容一致”分支，以及文件名手动更新入口，
  均进入统一自动生成方法；取消、校验失败和异常分支均不会进入。
- `Assembly-CSharp-Editor.csproj` 定向构建通过（0 error / 4 warning）；四个 warning 均来自既有
  `Assets/Editor/BuildPipline/BuildToolV2.cs`。本次三个文件的 `git diff --check` 通过。
- 待在 Unity Editor 中逐项点击四个菜单，确认 Guru 成功日志、脚本重载以及生成文件内容；
  `EditorApplication.ExecuteMenuItem` 返回成功只能证明菜单已找到并被调用，Guru 生成器内部结果仍以其日志和产物为准。
- 回滚时删除 `GURU_SPEC_AUTO_GEN_MENU`、`AutoGenerateGuruAllSpecs()` 及四处调用，
  即恢复为手动执行 Guru Spec 生成的旧流程；不需要回滚运行时数据或存档。

## 16. 2026-08-10 IWanner LevelData / LevelSort Sheet 注册

### 16.1 需求、基线与变更

- 用户在 `Luban/Configs/Datas/LevelData.xlsx` 新增 `iwanner_level`，并在
  `Luban/Configs/Datas/LevelSort.xlsx` 新增 `iwannerLevelSort`。
- 原 `Luban/Configs/Datas/__tables__.xlsx` 只注册主线、DrawGame、Skin 等既有 Sheet，
  没有这两个 IWanner Sheet 的独立导出入口，因此 Luban 不会生成对应表类型和 JSON。
- 本次仅在 `__tables__.xlsx` 追加两条手动映射，不修改用户填写的两个源 Sheet，也不生成或提交
  运行时代码、JSON、资源、存档、事件、Analytics、RemoteConfig 或平台配置：

| 表名 | 数据类型 | 输入 Sheet | 预期 JSON |
| --- | --- | --- | --- |
| `TbIWannerLevelData` | `IWannerLevelData` | `iwanner_level@LevelData.xlsx` | `tbiwannerleveldata.json` |
| `TbIWannerLevelSort` | `IWannerLevelSort` | `iwannerLevelSort@LevelSort.xlsx` | `tbiwannerlevelsort.json` |

### 16.2 数据流与验证

1. Luban 从两个新增 Sheet 的标题行读取结构，分别生成记录类型和 map 表。
2. 隔离目录执行 Luban client/json/cs-simple-json 生成命令成功，校验阶段无报错，实际生成
   `IWannerLevelData`、`TbIWannerLevelData`、`IWannerLevelSort`、`TbIWannerLevelSort`
   以及两个预期 JSON。
3. `tbiwannerleveldata.json` 共 21 条，ID 为 `30001`～`30020` 和 `20999`；
   `tbiwannerlevelsort.json` 共 20 条，`path` 为 `20001`～`20020`。
4. 使用 `@oai/artifact-tool` 回读并渲染修改后的 `__tables__.xlsx`；原 `A1:K13` 的值与公式保持一致，
   新增第 14～15 行沿用相邻数据行样式，公式错误扫描无命中。

### 16.3 风险、未验证项与回滚

- 两张新表当前尚未写入工程生成目录，也未加入 `ConfigFileNames`，因此本次只完成“可被 Luban 识别和生成”的
  表入口；运行时预加载与玩法消费仍为 `uncertain`，需在后续明确接入范围后再生成并验证。
- `iwannerLevelSort.path` 当前为 `20001`～`20020`，与 `iwanner_level.id` 的 `30001`～`30020`
  不一致。Luban 因该字段是字符串而不会报引用错误；这是否有意复用 DrawGame ID 为 `uncertain`，
  本次尊重用户源表，不擅自改值，玩法接入前需要确认对应关系。
- 未执行 Unity PlayMode、Android/iOS 横屏、资源预加载或存档流程验证；本次也没有运行时改动可供验证。
- 回滚时删除 `__tables__.xlsx` 中上述两条 IWanner 映射即可；若后续已经生成和接入，还需同步移除对应
  C#、JSON、`.meta`、`ConfigFileNames` 条目及运行时消费代码。

## 17. 2026-08-10 IWannerGameConfig 同步工具

### 17.1 需求与原有缺口

- `lubanconfig_tbglobalconst.json` 已生成 9 个 `iWannerGame_*` 字段，但原工具只维护主线
  `level_config` 和 Draw 的 `drawgame_config`，无法生成独立 `iwannergame_config`。
- 手工维护 Guru YAML 容易遗漏字段大小写或默认值，也不能自动串联既有 Guru 官方代码生成菜单。

### 17.2 当前行为与调用边界

- 新增菜单 `Tools/Luban/Update IWannerGameConfig To Guru YAML/guru_spec.yaml`。
- `GlobalConstIWannerGameConfigKeys` 精确维护入口开关、Skip 失败次数、提示等待时间、插屏与免广告共 9 个字段；
  同步前任一字段缺失即整体失败，不做部分写入。
- 若 YAML 已存在 `iwannergame_config`，工具保留未知扩展字段，只覆盖白名单；若不存在，则优先插在
  `drawgame_config` 后，没有 Draw 行时回退插在 `level_config` 后。
- 写入成功或内容一致时都会调用 Guru 官方 `Auto-Gen <GuruAllSpecs.G.cs>` 菜单；取消、输入缺失、解析失败
  或写入异常时不触发后续生成。
- 本工具只运行于 Unity Editor 文件操作路径，不访问 DB、运行时单例、场景对象或存档。

### 17.3 输入、输出与验证

- 输入：`Assets/Game/Configs/lubanconfig_tbglobalconst.json`、`Assets/Guru/guru_spec.yaml`。
- 输出：`Assets/Guru/guru_spec.yaml` 的单行 `iwannergame_config`，并由 Guru 官方流程维护
  `Assets/Guru/Gen/GuruAllSpecs.G.cs`。
- 字段级静态校验确认 GlobalConst JSON、YAML 与生成 C# 的名称和值一致；运行时和 Editor C# 定向构建均
  为 0 error，`git diff --check` 通过。
- `uncertain`：尚未在 Unity Editor 实际点击菜单，预览、无变化分支、AssetDatabase 导入和官方生成日志
  仍需人工验证。

### 17.4 风险与回滚

- 工具依赖 Guru YAML 的单行单引号 JSON 形式；若 YAML 改为多行对象，当前正则不会匹配并会停止写入。
- 字段名大小写以用户提供并由 Luban 生成的 `iWannerGame_*` 为权威，不能自动规范化。
- 回滚时删除菜单常量、`GlobalConstIWannerGameConfigKeys`、公开菜单入口、应用/合并方法，并移除 Guru YAML
  与生成 C# 的 `iwannergame_config` 行；其它 Luban 生成流程不受影响。

## 18. 2026-08-13 IWanner 小关通过次数插屏字段同步

- IWanner 白名单新增 `iWannerGame_interstitialShowPassedLevelCount`，与 GlobalConst、运行时 RemoteConfig 字段名保持完全一致。
- 字段属于 `iwannergame_config` 的必需同步项；执行 IWanner 同步菜单时会参与预览和覆盖，源 JSON 缺失则停止写入。
- 当前 `guru_spec.yaml` 与 `GuruAllSpecs.G.cs` 已同步该字段默认值 `-1`，并同时清理旧的提示时间和最小关卡字段。
- `Assembly-CSharp-Editor-firstpass.csproj` 与 `Assembly-CSharp-Editor.csproj` 定向构建通过；未在 Unity Editor 实际执行菜单，交互确认和官方生成日志仍待人工验证。
- 回滚只需从 IWanner 白名单与 Guru 默认载荷删除该键；不要手改 GlobalConst 生成代码，配置列的回滚应从源表执行后重新生成。

## 19. 2026-08-18 三玩法插屏关卡区间字段同步

- 主线、DrawGame、IWannerGame 白名单分别新增各自的 `LevelRange`、`RangeLevelCount`、`RangeFailedCount` 三个数组字段；名称严格沿用 `Global.xlsx` 的现有拼写。
- 三个 Guru 默认载荷已同步九个新区间字段，同时继续保留原有固定间隔和失败字段，兼容未升级客户端及旧 RemoteConfig。
- Luban 重新生成 `GlobalConst.cs` 与 `lubanconfig_tbglobalconst.json` 成功；九个新区间字段从源 JSON 到白名单、Guru YAML、Guru 生成 C# 和运行时读取常量的名称与值一致。
- Editor 工具仍只读本地 JSON/YAML 并写文件，不接触 DB、存档或运行时单例；运行时区间解析、兼容顺序、验证缺口和回滚见 `DevelopMD/InterstitialAdPolicy_LevelRange_Record.md`。

## 20. 2026-08-19 Guru YAML 四入口合并

### 20.1 需求与原有行为

- 原 `Tools/Luban` 下分别注册主线关卡排序、主线 GlobalConst、DrawGameConfig、
  IWannerGameConfig 四个 Guru YAML 更新菜单，需要逐项点击和确认；每项完成后还会各自触发一次
  `Guru/Guru Spec/Auto-Gen <GuruAllSpecs.G.cs>`。
- 本次只调整 `Assets/Game/Editor/LubanGenerator.cs` 的 Unity Editor 工具入口与编排，不修改
  Luban 源表、生成 JSON、Guru YAML 当前内容、运行时配置读取、DB、存档、事件、资源或平台流程。

### 20.2 当前行为与数据流

- `Tools/Luban/Update All Configs To Guru YAML` 是上述四项唯一可见菜单；一次确认后按“主线排序 →
  主线 GlobalConst → DrawGame 配置与排序 → IWannerGame 配置”的固定顺序复用既有解析与合并方法。
- 工具会先检查主线排序 JSON、Draw 排序 JSON、GlobalConst JSON 和 Guru YAML 是否齐全，并确认两组
  排序均非空；四步都能在内存中成功合并后才一次写入 `Assets/Guru/guru_spec.yaml`，避免部分更新。
- YAML 有变化时只导入一次；无变化时不写文件。两种正常完成路径都只在最后触发一次 Guru 官方生成菜单，
  由其继续维护 `Assets/Guru/Gen/GuruAllSpecs.G.cs`。
- 原四个公开方法保留为代码调用边界，但不再注册独立 `MenuItem`，避免菜单重复；它们的既有单项确认和
  单项更新逻辑没有改写。

### 20.3 验证、风险与回滚

- 静态扫描确认四个旧 `MenuItem` 已移除，当前只剩统一菜单常量和一个对应 `MenuItem`。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false
  /p:BuildInParallel=false` 通过：0 error / 4 warning；warning 来自工程既有代码。
- `git diff --check` 通过。当前工作区的 `guru_spec.yaml` 与生成文件已有其它任务改动，为避免覆盖用户数据，
  未实际点击统一菜单；确认框、AssetDatabase 导入、Guru 官方生成日志和脚本重载仍为 `uncertain`。
- 回滚时删除统一菜单及 `ApplyAllGuruConfigsToYamlAsset()`，恢复四个菜单常量和四个公开方法上的
  `[MenuItem]`；无需回滚运行时数据或存档。

## 21. 2026-09-04 每日免费金币配置同步

### 21.1 需求与原有缺口

- `Global.xlsx`、生成的 `GlobalConst.cs` 与 `lubanconfig_tbglobalconst.json` 已包含
  `dailyFreeCoinCount`，ShopPanel 每日免费金币控制器从本地 GlobalConst 读取该数量。
- 原 `GlobalConstLevelConfigKeys` 白名单未包含该字段；执行统一 Guru 配置更新时不会把它合并进
  `level_config`，容易导致配置源与 Guru 默认载荷不同步。

### 21.2 当前行为与边界

- `GlobalConstLevelConfigKeys` 新增精确字段名 `dailyFreeCoinCount`。
- 后续执行 `Tools/Luban/Update All Configs To Guru YAML` 或调用
  `UpdateGlobalConstToGuruSpec()` 时，会从 GlobalConst JSON 首行读取该字段；缺失时沿用既有整批校验失败策略，
  不进行部分写入。
- 本次只更新 Editor 生成器白名单，不修改 `ConfigManager.RemoteConfig`、运行时奖励读取、存档、Prefab、
  Guru YAML 当前内容或远端后台配置。

### 21.3 验证、风险与回滚

- 静态一致性检查确认源 JSON 字段为 `dailyFreeCoinCount: 10`，生成 C# 属性为
  `DailyFreeCoinCount`，生成器白名单使用 JSON 原始字段名。
- Editor 程序集定向编译通过；尚未在 Unity Editor 点击统一更新菜单，因此 Guru YAML 写回、确认弹窗和
  Guru 官方代码生成链为 `uncertain`。
- 回滚时从 `GlobalConstLevelConfigKeys` 删除 `dailyFreeCoinCount`，并删除本节记录；不涉及存档迁移。

## 22. 2026-09-11 Guru 字段同步必须闭合云控消费链

### 22.1 问题与规则

- 第 21 节只把 `dailyFreeCoinCount` 加入生成器白名单与 Guru 默认载荷，没有补
  `ConfigManager.RemoteConfig` Getter，也没有让运行时消费者读取远端值，导致“能写入 spec.yaml”不等于“云控生效”。
- 用户明确要求：后续凡是要求 `LubanGenerator` 写入 `Assets/Guru/guru_spec.yaml` 的业务字段，同一任务必须检查并补齐
  RemoteConfig 字段常量、读取 API、运行时消费者、远端优先/本地兜底和字段类型/业务边界验证；不得仅凭 YAML 与生成 C#
  一致就报告云控完成。该约束已同步写入 `AGENTS.md`。

### 22.2 本次闭环与验证边界

- `dailyFreeCoinCount` 已增加 `ConfigManager.RemoteConfig.GetDailyFreeCoinCount()`，卡片与红点共享规则改用该入口；
  Luban 值继续作为远端缺失或解析失败时的兜底。
- 本次不修改生成器文件、Guru 默认载荷、Firebase 拉取、缓存和 Loading 顺序；字段现有名称和值保持不变。
- 静态完整清单已确认：源 JSON 字段、生成器白名单、Guru YAML、Guru 生成 C#、RemoteConfig 常量/Getter、全部运行时消费者
  名称与链路一致；完整项目依赖编译为 `0 error / 1141 warning`。远端不同值、缺失、错误类型、0/负数的最终表现仍需 PlayMode/真机验证。
- 回滚时删除本规则的 `AGENTS.md` 条款、`dailyFreeCoinCount` Getter/常量并恢复本地直读；保留生成器白名单则只代表默认值同步，
  不得继续声明该字段支持云控。

## 23. 2026-09-14 每日宝箱开关同步与云控消费

- 背景：`isOpenDailyChest` 已存在于 `Global.xlsx`、Luban 生成的 `GlobalConst` 和本地 JSON，但原 `GlobalConstLevelConfigKeys` 未列出，Guru 默认 `level_config` 也没有该字段；`GiftManager` 原来只读本地值。先前补充 Luban 字段时漏掉了云控同步链，不能视为已完成云控接入。
- 本次在 `Assets/Game/Editor/LubanGenerator.cs` 加入精确键名 `isOpenDailyChest`；`Assets/Guru/guru_spec.yaml` 与 `Assets/Guru/Gen/GuruAllSpecs.G.cs` 的 `level_config` 默认值同步为 JSON 布尔 `true`。生成器仍按白名单更新，不导出其它 GlobalConst 字段。
- 运行时由 `ConfigManager.RemoteConfig.GetIsOpenDailyChest()` 读取 `level_config.isOpenDailyChest`。仅接受布尔类型，远端有效值优先；字段缺失、JSON 解析失败或类型不符时回退 Luban，Luban 未就绪时保持原来的开启默认值。编辑态直接返回开启，不访问运行时配置链。`GiftManager` 的入口和打开门禁改用该 API；不修改 Firebase 拉取、缓存、Loading 时序或 Gift 存档。
- 静态校验：Luban JSON、YAML 与生成 C# 的字段名及布尔类型一致；YAML 与生成 C# 的完整 `level_config` 默认 JSON 文本一致。使用项目缓存的 Newtonsoft.Json DLL 检查 `JToken` 转换：JSON `true/false` 为 Boolean，`0` 为 Integer，`"false"` 为 String，缺字段为空，只有前两者通过类型门禁。`git diff --check` 通过。`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 因 `Temp/bin/Debug` 缺少 Unity 元数据 DLL 报 `CS0006`，未取得可靠编译结果；Unity 菜单生成及 Android/iOS 设备上的远端覆盖和回退均为 `uncertain`。
- 回滚：从生成器白名单和 Guru 默认载荷移除该键，删除 RemoteConfig 字段常量与 Getter，并让 `GiftManager.IsOpenDailyChest` 恢复为读取 Luban。本地字段与既有 Daily Chest 存档无需迁移。

## 24. 2026-09-20 ChestGift 主线胜利自动弹出关卡同步

- `autoShowChestGiftLevel` 已加入 `GlobalConstLevelConfigKeys`，由现有 GlobalConst 导出流程写入 `level_config`；默认值为整数 `5`。任务开始时 Global 表及 Luban 生成产物已包含该字段，本次没有覆盖其余未提交表格内容。
- 云控链闭合为：GlobalConst JSON → LubanGenerator 白名单 → `guru_spec.yaml` / `GuruAllSpecs.G.cs` 默认载荷 → `ConfigManager.RemoteConfig.GetAutoShowChestGiftLevel()` → `GiftManager.TryOpenFirstDayVictoryChestGift()`。远端只接受 JSON Integer；缺失、解析失败或类型不符时回退 Luban，小于等于 `0` 时由消费者关闭自动弹出。
- 静态验证确认本地 JSON、Guru YAML 与 Guru 生成 C# 均为整数 `5`，两份 Guru `level_config` 文本完全一致，`git diff --check` 通过。`Global.xlsx` 当时被其它进程独占，未直接重新打开；Unity 菜单导出、PlayMode 与真实远端覆盖仍为 `uncertain`。
- 回滚时从白名单与 Guru 默认载荷移除该字段，并同步删除 RemoteConfig Getter 和运行时消费者；不能只删除白名单而保留失去默认值来源的业务读取。

## 25. 2026-09-20 主线插屏区间与每日免费金币本地默认值同步

### 25.1 需求与变更

- 用户指定把当前远端参数中的主线区间配置和每日免费金币数量同步到 GlobalConst；`level_sort` 与其余参数不在本次范围。
- `Global.xlsx` 与运行时生成 JSON 同步修改如下：

| 字段 | 原值 | 新值 |
| --- | --- | --- |
| `mainGame_interstitalAdLevelRange` | `6|26|56|76|999` | `4|999` |
| `mainGame_interstitalAdRangeLevelCount` | `6|5|4|3|2` | `4|2` |
| `mianGame_interstititalAdRangeFailedCount` | `-1|30|30|30|30` | `-1|12` |
| `dailyFreeCoinCount` | `10` | `5` |

- 本次未新增字段，因此不需要改变 `GlobalConst.cs` 的结构；现有远端优先、本地 GlobalConst 兜底顺序保持不变。
- 本次未修改 `guru_spec.yaml`、RemoteConfig 拉取/缓存/Loading 时序、存档、事件、Prefab 或平台代码。

### 25.2 数据流、验证与回滚

- 主线插屏仍按“远端区间 → 远端旧固定值 → 本地区间 → 本地旧固定值”读取；每日免费金币仍按“远端 `level_config` → 本地 GlobalConst → `0`”读取。本次只改变上述本地兜底值。
- 使用 `@oai/artifact-tool` 回读 `Global.xlsx` 的 `A55:D71`、扫描公式错误并渲染预览；目标值正确且错误扫描无命中。独立逐单元格比较确认只有 `D58`、`D59`、`D60`、`D69` 的值变化，样式差异为 0；工作簿压缩包中只有共享字符串和目标工作表 XML 发生变化。
- `lubanconfig_tbglobalconst.json` 已同步四项值并通过 JSON 反序列化检查；完整 Luban 生成、Unity 编译、PlayMode、Android/iOS 与真实远端覆盖未执行，运行时实测仍为 `uncertain`。
- 回滚时把四项恢复为表中“原值”，并同步更新生成 JSON；无需回滚字段结构或远端读取 API。
