---
name: brainpunk-local-doc-update
description: Create or update the required local BrainPunk feature/module documentation after any new requirement, bug fix, or behavior-changing implementation. Use when completing BrainPunk work that changed runtime behavior, UI flow, configs, save/resource/event flow, level behavior, tools, tests, or verification evidence, especially before final reporting.
---

# BrainPunk Local Documentation Update

## Workflow

1. Identify the primary changed module from the touched files and the user-facing requirement.
2. Choose the documentation target:
   - Prefer the corresponding feature/module folder next to the changed code or asset.
   - For level-specific changes under `Assets/Game/Scripts/Level/LevelItem/Level_*`, use that level folder.
   - 没有明确归属目录时，写入 `BrainPunk/DevelopMD/已有功能设计与使用记录/YYYY-MM-DD.md` 当天记录，并说明归属不确定性。
3. 新建文档前搜索目标目录和 `BrainPunk/DevelopMD/` 的既有说明，已有对应模块文档时更新该文档。共享记录始终按当天日期路由，不因主题命中旧历史文件而回写该文件。
4. 在最终回复前补齐 Markdown 文档。共享记录采用本地日期（Asia/Shanghai）：当天日期文件存在时更新对应主题章节，保留其它任务内容；不存在时才创建，不按任务、时间戳或序号另建同日副本。
5. In the final response, report the documentation path and any uncertainty about placement.

## Required Content

Keep the document concise and evidence-based. Include:

- Requirement/background
- Affected files, assets, prefabs, configs, and systems
- Existing behavior baseline
- New behavior or fixed behavior
- Data, resource, UI, save, event, analytics, or platform flow when applicable
- Verification performed, including commands or manual checks
- Remaining risks or unverified gaps
- Rollback path

## Evidence Rules

- Tie conclusions to local code, docs, logs, or verification output.
- Mark uncertain statements as pending validation.
- Do not claim mobile, platform, save, analytics, or resource behavior is verified unless it was checked.
- 避免重复大篇幅设计文档；模块文档已有对应主题时更新该文档。共享日期记录只保留事实、证据和模块文档链接，不迁移旧记录。
- 原 `BrainPunk/DevelopMD/已有功能设计与使用记录.md` 为只读历史记录：可阅读和检索，不追加、改写、迁移或重新格式化。检索既有功能时同时搜索该历史文件及日期目录。

## Naming

Use a short, topic-specific Markdown filename in the target folder, such as:

- `GetSkinPopup_FeatureRecord.md`
- `SkinShop_UsageRecord.md`
- `Level_12_RiskAssessment.md`

跨功能事实统一写入 `BrainPunk/DevelopMD/已有功能设计与使用记录/YYYY-MM-DD.md`，同一天只维护一个日期文件；规则见该目录的 `README.md`。模块与关卡专属功能／风险文档仍在原归属目录维护，日期记录不替代这些文档。
