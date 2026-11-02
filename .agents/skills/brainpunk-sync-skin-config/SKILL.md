---
name: brainpunk-sync-skin-config
description: Analyze BrainPunk Skin images and prefabs and synchronize both Skin.xlsx and SkinShop.xlsx by default, appending only missing resource mappings and shop references without changing existing rows. Use when adding Back, Face, or Clothes/Body skins, filling skin configuration tables, or auditing missing Luban skin mappings.
---

# BrainPunk Skin 配置同步

## 固定边界

默认使用以下路径，不自行搜索替代文件：

- 项目根：`D:\GitStore\BrinPunk\BrainPunk`
- 图片根：`Assets/Game/ArtSrc/Skins`
- UI 图标：`Assets/Game/ArtSrc/Skins/UI`
- prefab：`Assets/Game/Prefabs/Skins`
- 配置表：`Luban/Configs/Datas/Skin.xlsx` 和 `Luban/Configs/Datas/SkinShop.xlsx`

用户要求新增皮肤、添加皮肤配置或同步皮肤时，默认两张表都必须有配置：先完成 Skin，再以保存后的 Skin 同步 SkinShop。只有用户明确限定“只改 Skin”或“只改 SkinShop”时才缩小范围；仅检查、审计请求仍不写表。

只有固定路径不存在或用户明确指定其他路径时才允许改路径。角色皮肤工作表为 `Skin.xlsx` 的 `actorSkin`；商店工作表为 `SkinShop.xlsx` 的 `actorSkinShop`。同簿中的 `pencilSkin` / `pencilSkinShop` 必须保留，不应用角色 ID 规则。实际表名是 `Skin.xlsx`，不要创建 `Skins.xlsx`、`#Skin.updated.xlsx` 等永久副本。

## 字段映射

仅追加以下非人工字段：

| 资源目录 | 文件格式 | GoodsID | skinName | skinPart |
| --- | --- | --- | --- | --- |
| `Back` | `Sp_bs_img_NNN.png` | `201000 + NNN` | `U_Skin_Name_Back_NN` | `Back` |
| `Face` | `Sp_zs_img_NNN.png` | `202000 + NNN` | `U_Skin_Name_Eyes_NN` | `Face` |
| `Clothes` | `Sp_zs_fz_img_NNN.png` | `203000 + NNN` | `U_Skin_Name_Outfit_NN` | `Body` |

同一行的 `textureResources` 与 `prefabResource` 都使用不带扩展名的资源键。只有对应 UI PNG 和同名 prefab 都存在时才允许追加。

人工字段必须保持空白，不推断、不复制相邻值：备注、Spine、解锁方式、品质、解锁提示、金币价格、商品 ID（D、H:M）。

## 执行流程

1. 先读取项目 `AGENTS.md`、当前 feature plan 和 `Spreadsheets` 技能；使用 `@oai/artifact-tool` 编辑工作簿。
2. 记录 `git status --short`，区分用户已有改动。除目标工作簿外，不修改图片、prefab、meta、生成配置、C# 或其他文件；用户明确要求更新本 Skill 时允许修改其说明和脚本。
3. 用独占读写方式预检本次目标工作簿（默认两张表）。若被 Excel 锁定，停止并请用户关闭；禁止强制覆盖、COM 自动保存或绕过锁。
4. 调用 `load_workspace_dependencies`，在仓库外的临时目录创建指向其 Node.js packages 的 `node_modules` junction，把 `scripts/sync_skin_config.mjs` 和 `scripts/read_xlsx_cells.py` 一并复制到临时目录运行；可用 `--python <Python路径>` 指定只读校验运行时。
5. 对 Skin 先运行 `--target skin --mode analyze`。检查新增资源、ID/Key 冲突、UI PNG 与 prefab 完整性；Skin 无新增时不写 Skin，但仍必须继续检查 SkinShop，不能提前结束任务。
6. 再运行 `--mode write --output <临时候选.xlsx> --preview <临时预览.png>`。脚本只在末尾追加，不插入、不排序、不覆盖既有行，并黄色标记新填的 B:C、E:G。
7. 脚本必须重新导入候选表，逐工作表、逐单元格确认原 used range 的值与公式完全一致，检查新增范围并扫描公式错误。使用 `view_image` 查看临时预览。
8. 视觉与数据校验通过后，再次检查源表未被锁定；用明确绝对路径将候选文件复制回固定 `Skin.xlsx`，比较候选与源文件 SHA-256。
9. 使用 `--target shop` 对 SkinShop 执行相同的 analyze、候选写入、数据/预览校验和锁/哈希检查流程。两表保存后分别再运行 analyze，均须新增 0 行。任一表失败时明确报告已完成和未完成范围，不宣称双表同步完成。
10. 删除临时候选、预览、脚本副本和 junction。最终不得在仓库留下输出目录、备份、报告或其他辅助文件。
11. 最终报告两张目标工作簿各自的追加数量/范围、SkinShop.id → skinId 映射、保留空白的人工字段和未执行的 Luban/Unity 验证；用户限定单表时仅报告该表。

## 强制保护

- 保持幂等：已存在的 texture/prefab 资源键不重复追加。
- 遇到重复 GoodsID、重复 skinName、缺 UI 图标、缺 prefab、未知命名、多个候选工作簿或导出前后原数据不一致时立即停止。
- 不运行 Luban 生成；人工必填字段为空时只形成配置骨架。
- 不保留辅助文档或临时文件。如果仓库治理要求修改工作簿之外的文件，先向用户说明冲突并取得确认。
- 不删除、重置或覆盖用户已有工作区改动。

## 脚本接口

```text
node sync_skin_config.mjs --target skin|shop --mode analyze [--repo-root <path>] [--workbook <path>]
node sync_skin_config.mjs --target skin|shop --mode write --output <xlsx> --preview <png> [--repo-root <path>] [--workbook <path>]
```

脚本永远不直接覆盖源工作簿；源文件替换由上述校验流程负责。

## SkinShop 同步

- 默认必须使用 `--target shop` 补充 SkinShop；先完成 Skin，再以保存后的 Skin 为依据处理 SkinShop。仅用户明确限定单表时跳过另一张表。
- 输入为 `Skin.xlsx/actorSkin`，输出为 `SkinShop.xlsx/actorSkinShop`。只补 Back、Face、Body 的缺失 `skinId`；不自动处理 FullBody 或铅笔皮肤。
- 现有角色商店映射是 `SkinShop.id = Skin.id + 10000`：Back 为 `211000 + NNN`，Face 为 `212000 + NNN`，Body 为 `213000 + NNN`；`skinId` 等于 Skin 的 id。执行前核对既有映射，出现例外时停止，不静默改写。
- 仅追加 B:C 并标黄；A、D:F 留空，商品备注不推断。维持既有行位置和所有其它工作表。
- 检查本工作表内 id / skinId 唯一性、Skin 引用有效性、新行图标与 prefab 完整性。不同工作表可使用同一 id，禁止跨角色/铅笔表错误去重。
- 仍需执行 analyze、候选导出、全表原值/公式对比、新行验证、公式错误扫描、预览和源文件锁/哈希检查。再分析应新增 0 行。
- Skin 的解锁方式、品质、价格等人工字段未填齐时，SkinShop 补齐不代表可发布；不运行 Luban，也不宣称通过 Unity 或真机验证。

## 回归证据与回滚边界

2026-09-08 本地表结构核对：两份工作簿均含角色与铅笔工作表，角色表原有 78 条数据；本次 SkinShop 补齐 14 条（背饰 4、面饰 5、服装 5），追加范围 A84:F97。ID 规则由既有角色商店行逐条核对；数据流为资源 → Skin.actorSkin → SkinShop.actorSkinShop。验证结果以实际执行输出为准，Unity 商店展示、Luban 和 Android/iOS 尚未验证。

回滚仅撤回本次追加行和本 Skill 的本次差异，不回退用户已有 Skin 配置或资源改动。后续执行不得把上述行号或数量当作固定输入。

## XLSX 空共享字符串导入缺陷与独立校验

- 已复现 artifact-tool 将 OOXML 的空共享字符串误读为共享字符串索引文本：SkinShop 两张表 B3:C4 的空值变成 `5`，触发 Luban 在 B6 报“简单数据类型字段 不支持子列名或者多行”。同一导入器回读前后相等不能证明原数据未变。
- `read_xlsx_cells.py` 仅用标准库只读解析 ZIP/XML，正确解引用 sharedStrings；同步脚本先依原始数据纠正内存中的空值，导出后独立对比所有工作表原单元格的值、类型和公式。任一变化均停止替换。
- 排查导出错误时允许将 Luban 的 outputCodeDir / outputDataDir 都指向仓库外临时目录执行验证；禁止直接调用写入 Assets 的 gen.bat。日常骨架同步仍不默认生成项目配置。
- 2026-09-08 修复证据：独立比对确认只恢复 SkinShop 两张表 B3:C4 共 8 格，原有及新增 14 条数据保留；Skin.xlsx 原有单元格与 Git 基线没有数据差异。修复后完整 Luban client / cs-simple-json / json 解析、验证和临时目录生成成功（退出码 0）。Unity 展示未验证。
- 回滚注意：不得恢复含错误 `5` 的表头版本；需撤销新增皮肤时仅移除相应新增数据行。
