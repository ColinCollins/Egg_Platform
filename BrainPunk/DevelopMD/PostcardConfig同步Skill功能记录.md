# PostcardConfig 同步 Skill 功能记录

## 需求与背景

`Assets/Game/UI/Postcard/Resources` 新增 `Postcard_*.prefab` 后，需要在 `Luban/Configs/Datas/#PostcardConfig.xlsx` 建立最小配置骨架。本次要求不修改既有配置行，只为缺失明信片追加 `id` 和 `themeName`，并将流程沉淀为可重复调用的项目 Skill。

## 影响范围

- Skill：`D:/GitStore/BrinPunk/.agents/skills/brainpunk-sync-postcard-config/`
- 扫描资源：`Assets/Game/UI/Postcard/Resources/Postcard_*.prefab`
- 目标配置：`Luban/Configs/Datas/#PostcardConfig.xlsx`
- 本次识别 prefab：`Postcard_toys`、`Postcard_pirate`、`Postcard_magic`

不修改 prefab、图片、meta、生成配置、运行时代码、存档、事件、分析或平台 SDK；不执行 Luban 生成。

## 原有行为基线

配置表原 used range 为 `A1:M14`，数据行包含 30001～30008 及特殊行 30999。Resources 中已有 toys、pirate、magic 三个运行时 prefab，但表内没有对应 `id/themeName` 行。

## 当前行为

1. Skill 先按 C 列 `prefabPath`（不区分大小写）排除已配置 prefab。
2. 对缺失 prefab，从 YAML 中唯一的 `UI_mingxinpian_img_jueseN_01` 节点提取主题编号，计算 `id=30000+N`，生成 `themeName=U_Diary_title_<id>`。
3. 只在表尾追加行，仅 B/G 两列写值并黄色标记；其他列保持空白，不推断 prefabPath、排序、日记、进度或奖励。
4. 同一行已存在匹配 `id/themeName` 时视为已补齐；重复 ID、重复 themeName、编号歧义或工作簿结构异常会停止。
5. 脚本只生成候选表，不直接覆盖源表；覆盖前后使用独占访问、源文件 SHA-256、候选哈希和回读检查保护。

本次新增：

| 行 | id | themeName | 识别来源 |
| --- | ---: | --- | --- |
| 15 | 30009 | `U_Diary_title_30009` | `Postcard_toys` / `juese9` |
| 16 | 30010 | `U_Diary_title_30010` | `Postcard_pirate` / `juese10` |
| 17 | 30011 | `U_Diary_title_30011` | `Postcard_magic` / `juese11` |

## 数据与资源流

Resources prefab YAML（只读）→ 角色节点编号 → 计算 ID/本地化键 → 与 `#PostcardConfig.xlsx` 既有 B/C/G 列比较 → 临时候选表 → 回读与视觉校验 → 覆盖固定配置表。流程不进入 Unity 运行时，也不触发 DB、Save、资源加载或事件链。

## 验证证据

- 修改前使用 `@oai/artifact-tool` 导入并渲染 `A1:M14`，确认 B/G 列及既有格式。
- analyze 模式识别 3 条：toys→30009、pirate→30010、magic→30011，无 ID/themeName 冲突。
- write 模式回读确认原 `A1:M14` 的非公式值和公式文本保持一致；K 列公式缓存仅按同一公式重新计算浮点精度。
- 新增范围 `A15:M17` 只有 `B15:B17`、`G15:G17` 非空，没有新增公式，公式错误扫描为 0。
- 候选表目视确认新增行位置、黄色标记和其余列空白。
- 写回前源表 SHA-256 仍为 `2B70D202BBC90ECA48601611D7F18C81DE2CD2374B07F8A768B0D28BA42C9EF1`；写回后与候选表 SHA-256 同为 `24E15C46A65487CA900EC959B92068C7FA5BB285C234000943C810426CD880F0`。
- 写回后再次 analyze，新增数量为 0，验证幂等。

## 剩余风险与未验证项

- 未执行 Luban 生成，因此新骨架行在生成器中的必填字段检查和生成结果尚未验证。
- 新行按用户要求只有 id/themeName，其余业务字段为空，不能视为可直接进入运行时的完整配置。
- 未运行 Unity 编译、PlayMode 或 Android/iOS；本次仅变更编辑期配置数据与 Skill。
- 后续 prefab 若没有唯一 `jueseN` 节点，Skill 会停止，需要人工确认编号规则。

## 回滚

通过 Git 回退 `Luban/Configs/Datas/#PostcardConfig.xlsx` 可恢复原 14 行；删除或回退 `brainpunk-sync-postcard-config` Skill 可移除自动同步入口。脚本不保留永久候选、备份或输出目录。

## 2026-08-17：dream / fishing 配置骨架同步

### 需求与影响范围

- Resources 中新增 `Postcard_dream.prefab` 与 `Postcard_fishing.prefab`，需要补充最小 `PostcardConfig` 配置骨架。
- 本次只修改 `Luban/Configs/Datas/#PostcardConfig.xlsx`，不修改两个 prefab、图片、meta、运行时代码、存档、事件、分析或平台 SDK，也不执行 Luban 生成。

### 原有行为基线与新增行为

- 修改前工作簿 used range 为 `A1:M19`；既有非公式值和公式文本作为不可变基线。
- `Postcard_dream.prefab` 内唯一角色节点为 `UI_mingxinpian_img_juese14_01`，新增 `B20=30014`、`G20=U_Diary_title_30014`。
- `Postcard_fishing.prefab` 内唯一角色节点为 `UI_mingxinpian_img_juese15_01`，新增 `B21=30015`、`G21=U_Diary_title_30015`。
- 新增范围 `A20:M21` 除 B/G 外保持空白；B/G 使用黄色标记。现有 `prefabPath`、sort、备注、diary、unlockProgress、rewards 和 rewardMultiple 均未推断。

### 数据与资源流

两个 Resources prefab YAML（只读）→ 唯一 `jueseN` 节点 → `id=30000+N` 与 `themeName=U_Diary_title_<id>` → 与源表 B/C/G 列冲突检查 → 临时候选表 → 回读、公式错误和视觉校验 → 固定配置表。

### 验证证据

- 修改前独占访问成功；初始及写回前 SHA-256 均为 `D4341E099B7AD1082BD774912BA8C8FE3B1F9A2B1E78E10A0A1A1550A52D1C65`，修改时间未变化。
- analyze 模式识别 2 条映射，无重复 ID、重复 themeName、编号歧义或既有行冲突。
- write 模式回读确认原 `A1:M19` 的非公式值和公式文本完全一致；新增行只有 B/G 有值、没有公式，公式错误扫描为 0。
- 修改前和候选表均完成目视检查；写回后的源表与候选 SHA-256 均为 `0CB3777FB98FEC5535CCB6AF17B69AD33FF0D3833EDC23A7D4A1064A64B4EEC8`。
- 写回后再次运行 analyze，新增数量为 0，幂等性通过。

### 剩余风险与回滚

- 未执行 Luban 生成、Unity 编译、PlayMode 或 Android/iOS 验证；新增行只是缺少其他业务字段的配置骨架，不能视为可直接进入运行时的完整配置。
- 如需回滚，通过 Git 回退 `Luban/Configs/Datas/#PostcardConfig.xlsx` 与本节文档即可；本次不保留临时候选或备份文件。

## 2026-08-19：Prefab 扫描目录跟随 YooAsset 托管迁移

- Postcard 页面 prefab 已从 `Assets/Game/UI/Postcard/Resources/` 移至 `Assets/Game/UI/Postcard/Prefabs/Pages/`，因此 Skill 说明和 `sync_postcard_config.mjs` 默认扫描目录同步调整为新路径。
- ID 推导、冲突检查、候选工作簿、哈希与回读保护规则均未修改；`Luban/Configs/Datas/#PostcardConfig.xlsx` 本次没有改动。
- 16 条当前配置与新目录 prefab/meta 静态匹配通过。由于本次未运行依赖 `@oai/artifact-tool` 的 Skill analyze/write 流程，其运行环境与工作簿写入行为仍沿用此前证据，本次路径调整的端到端执行结果为 `uncertain`。
- 回滚时应与页面资源回迁一起恢复默认扫描目录；只恢复脚本路径会导致同步工具扫描不到现有 YooAsset 页面。

## 2026-09-08：poolParty 配置骨架

- 需求：为新增 `Assets/Game/UI/Postcard/Prefabs/Pages/Postcard_poolParty.prefab` 补齐配置。节点 `UI_mingxinpian_img_juese16_01`（1223 行）推导 id 30016。
- 变更：`Luban/Configs/Datas/#PostcardConfig.xlsx` 从 A1:M21 扩为 A1:M22，仅新增 B22=30016、G22=U_Diary_title_30016 并标黄；其它新行字段留空，原 prefab 与运行时代码不修改。
- 流程：只读 prefab 编号 → ID/主题键冲突检查 → 追加候选 → artifact-tool 回读与独立 OOXML 校验 → 哈希确认后写回。未进入资源加载、存档或事件流程。
- 验证：旧区域非公式值与公式文本一致；独立 ZIP/XML 解析确认原单元格未误写空值，新行仅 B/G 非空；公式错误扫描 0，前后预览通过，源文件与候选哈希一致，写回后 analyze 新增 0 条。
- 缺口：尚未运行 Luban、Unity 或 Android/iOS 验证；prefabPath、排序、日记、解锁进度和奖励等仍待人工填写，仅为配置骨架。
- 回滚：仅撤回本次第 22 行和本节记录，保留用户其它配置、prefab 和工作区改动。

## 2026-09-18：四个新主题补齐 prefabPath

- 背景：四个新主题首次同步只建立 id/themeName 骨架；用户随后明确要求补上 prefab 名称，本轮按该要求填写 C 列。开始时工作簿为 A1:M26，C23:C26 均为空，已有 B/G 值保持不变。
- 变更：C23=`Postcard_candy`（30017）、C24=`Postcard_detective`（30018）、C25=`Postcard_esports`（30019）、C26=`Postcard_circustroup`（30020）。沿用既有配置的资源名格式，不加目录或 .prefab 扩展名，四个名称与 Pages 中实际 prefab 一致。
- 流程：按唯一 id 定位行 → 确认目标 prefab 存在及原 C 列为空 → artifact-tool 写入候选 C 列 → 回读逐单元格比较 → 预览 → 源表独占哈希/修改时间校验后写回。没有修改 prefab、图片、运行时代码、存档、事件、奖励或平台流程。
- 验证：候选回读确认只有 C23:C26 的非公式值变化，其余值与全部公式文本保持一致，公式错误扫描 0；前后 A19:H26 预览已目视检查。写回后的源表与候选 SHA-256 均为 `26C9648D8DD9321E93FA0C683CFBE2AD4AD96BAF05AA9A2FD52894A7DDB1B3E3`；本轮修改前哈希为 `7BAB853F9DE1D7C2AB447DC0EB604971A03D64FBA26CEF2660E35E57A2B0CC7C`。
- 边界：用户本轮要求覆盖了同步 Skill 仅追加 B/G 的默认范围，但没有扩大到排序、日记、解锁进度或奖励字段。未执行 Luban、YooAsset 构建、Unity PlayMode 或 Android/iOS 验证，完整运行时配置仍为 `uncertain`。
- 回滚：只清空 C23:C26，即可撤回本轮名称补齐，保留新增 B/G 和其它工作区内容。临时候选、预览、脚本和 junction 收尾删除；回读摘要留存于 `C:/Users/admin/AppData/Local/Temp/brainpunk-postcard-four-20260918/prefabPath补齐验证.json`。
