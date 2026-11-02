---
name: brainpunk-sync-postcard-config
description: Scan BrainPunk postcard prefabs in Assets/Game/UI/Postcard/Prefabs/Pages, compare inferred postcard IDs with Luban/Configs/Datas/#PostcardConfig.xlsx, and safely append only missing id and themeName cells without changing existing rows. Use when new Postcard_*.prefab assets must receive minimal PostcardConfig skeleton rows, when auditing whether postcard prefabs are absent from the Luban table, or when the user asks to synchronize postcard resources into #PostcardConfig.xlsx.
---

# BrainPunk 明信片配置同步

## 固定边界

默认使用以下路径，不自行搜索替代文件：

- 项目根：`D:\GitStore\BrinPunk\BrainPunk`
- prefab：`Assets/Game/UI/Postcard/Prefabs/Pages/Postcard_*.prefab`
- 配置表：`Luban/Configs/Datas/#PostcardConfig.xlsx`

实际表名是 `#PostcardConfig.xlsx`。不要创建 `PostcardConfig.xlsx`、`#PostcardConfig.updated.xlsx` 等永久副本。

## 字段映射

只追加两个字段，其他列保持空白：

| 来源 | 目标字段 | 规则 |
| --- | --- | --- |
| prefab 内唯一的 `UI_mingxinpian_img_jueseN_01` 节点 | `id`（B 列） | `30000 + N` |
| 推导出的 `id` | `themeName`（G 列） | `U_Diary_title_<id>` |

先按 `prefabPath`（C 列，不区分大小写）排除已配置 prefab。只对缺失 prefab 推导 ID。若同一行已经存在匹配的 `id/themeName`，视为已补齐，即使 `prefabPath` 为空也不得重复追加。

## 执行流程

1. 读取项目 `AGENTS.md`、当前 feature plan 和 `Spreadsheets` 技能；使用 `@oai/artifact-tool` 编辑工作簿。
2. 记录 `git status --short`，区分用户已有改动。除目标工作簿、当前 Skill 和项目要求的本地中文文档外，不修改其他文件。
3. 用独占读写方式检查 `#PostcardConfig.xlsx`。若被 Excel 锁定，停止并请用户关闭；禁止强制覆盖、COM 自动保存或绕过锁。
4. 调用 `load_workspace_dependencies`，在仓库内明确的临时目录创建指向其 Node.js packages 的 `node_modules` junction，把 `scripts/sync_postcard_config.mjs` 复制到临时目录运行。
5. 先运行 `--mode analyze`。检查缺失 prefab、节点解析、ID/themeName 冲突；无新增时直接报告，不写文件。
6. 再运行 `--mode write --output <临时候选.xlsx> --preview <临时预览.png>`。脚本只在末尾追加，不插入、不排序、不覆盖既有行，并黄色标记新填的 B、G 单元格。
7. 脚本重新导入候选表，逐单元格确认原 used range 的非公式值和公式文本完全一致（公式缓存允许工具按同一公式等价重算），确认新增范围只有 B、G 有值，并扫描公式错误。使用 `view_image` 查看修改前和候选预览。
8. 再次独占检查源表并确认源文件 SHA-256/修改时间未变化，再将候选文件复制回固定 `#PostcardConfig.xlsx`；写回后比较源文件与候选文件 SHA-256。
9. 删除候选、预览、源表副本、脚本副本和 junction。最终不得在仓库留下输出目录、备份、报告或其他辅助文件。
10. 不运行 Luban 生成。最终报告新增数量、B/G 范围、识别映射、未执行的 Luban/Unity 验证。

## 强制保护

- 保持幂等：已有匹配 `id/themeName` 不重复追加。
- `Postcard_Unlock.prefab` 与表内 `Postcard_unlock` 按不区分大小写处理。
- 遇到重复 ID、重复 themeName、同一 prefab 没有或存在多个角色编号、多个 prefab 推导出相同 ID、工作簿结构不是 `A1:M...`、原数据变化或导出校验不一致时立即停止。
- 新增行除 B/G 外必须为空；不推断 prefabPath、sort、备注、diary、unlockProgress、rewards 或 rewardMultiple。
- 不删除、重置或覆盖用户已有工作区改动。

## 脚本接口

```text
node sync_postcard_config.mjs --mode analyze [--repo-root <path>] [--workbook <path>]
node sync_postcard_config.mjs --mode write --output <xlsx> --preview <png> [--repo-root <path>] [--workbook <path>]
```

脚本永远不直接覆盖源工作簿；源文件替换由上述校验流程负责。
