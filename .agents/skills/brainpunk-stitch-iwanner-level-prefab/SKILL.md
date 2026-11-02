---
name: brainpunk-stitch-iwanner-level-prefab
description: Build BrainPunk IWanner ILevel_XXX Unity prefabs from 100 PPU reference screenshots while preserving the accepted Actor, Door, Camera, Feedback, FallArea, color, and event structure. Use when continuing IWanner map stitching from numbered PNG references, cloning an accepted WannerLevel prefab into new levels, converting measured platform rectangles into Ground SpriteRenderer/BoxCollider2D objects, or auditing generated IWanner prefab references and geometry.
---

# IWanner 关卡 Prefab 拼接

## 基本流程

1. 读取仓库 `AGENTS.md`、项目宪法、IWanner 本地功能/风险记录，并记录任务前 `git status --short`。
2. 核对 `Assets/Game/Configs/tbiwannerlevelsort.json` 与 `tbiwannerleveldata.json` 的 Group、ID、Path 映射。映射缺失或冲突时停止生成。
3. 选择用户已验收或最近完成的 `ILevel_XXX.prefab` 作为模板；不得为复用便利修改该模板。
4. 用 `scripts/inspect_reference.py` 测量源图。默认换算为 100 PPU：
   - `x = ((left + right) / 2 - imageWidth / 2) / 100`
   - `width = (right - left + 1) / 100`
   - 地面顶边 `topY = (imageHeight / 2 - topPixel) / 100`
   - 向下延伸的标准平台 `y = topY - height / 2`
5. 人工核对脚下平台、浮台、墙、顶边、Actor 与 Door。浏览器框架色与真实关卡色相同，不能把脚本候选直接视为最终规格。
6. 把明确坐标写入符合 `references/spec-schema.md` 的 JSON。固定相机边界与源图可见平台必须分列，但生成后统一命名为 `Ground`、`Ground (N)`。
7. 先运行 `scripts/stitch_prefabs.py --dry-run`，确认输出路径、Ground 数量、图片 GUID、Actor/Door 坐标；再去掉 `--dry-run` 生成。
8. 运行 `scripts/stitch_prefabs.py --validate-only`，再检查 `git diff --check`、Unity PrefabImporter/Console。没有 PlayMode 或移动端证据时明确标记 `uncertain`。
9. 按 `brainpunk-local-doc-update` 更新 IWanner 中文功能记录、风险评估和 `DevelopMD/已有功能设计与使用记录/YYYY-MM-DD.md` 当天记录；同日使用同一个文件，原历史文件保持只读。

## 生成边界

- 只创建规格明确列出的新 prefab 与 `.meta`，默认拒绝覆盖已存在目标；只有用户明确允许返工时才使用 `--force`。
- 关卡地形拼接与装饰背景分开处理。任务包含夕照山景、工厂设备或其它分层背景时，先用 `brainpunk-build-iwanner-mesh-background` 判断 Mesh／Sprite，并按目标关卡实际开口构图；本 Skill 的 100 PPU 地形坐标不能直接当作背景映射范围。
- 保留模板的 `IWannerLevelCtrl`、Actor、Door、成功回调、Camera、Feedback、FallArea、颜色资源和嵌套 prefab GUID。
- 参考图节点必须保留但保持 `m_IsActive: 0`，仅供编辑对照，不参与运行时显示或碰撞。
- 生成时清除模板 `Scene` 下的旧 Ground 与关卡专属机关，只按规格回填 Ground；Actor、Door、Camera、Feedback、FallArea 等核心对象继续从模板根结构保留，避免模板单关机关泄漏到新关卡。
- Ground 同时写入 `SpriteRenderer.m_Size`、`BoxCollider2D.m_Size` 和 tiling `newSize`；所有 Ground 保持 Layer 6。
- 不新增运行时代码、全局事件、配置、存档或共享 Actor 能力。
- 图片测量只能形成静态几何证据，不能替代出生落地、跳跃可达、掉落、进门、连续切关及 Android/iOS 横屏验证。
- `--validate-only` 必须同时确认 `Scene` 的直接子节点只有规格 Ground；不能仅以 Ground 数量和本地 fileID 完整为通过依据。

## 工具

```powershell
$python = '<bundled-or-system-python>'
& $python scripts/inspect_reference.py <reference.png>
& $python scripts/stitch_prefabs.py --project-root <BrainPunk> --spec <spec.json> --dry-run
& $python scripts/stitch_prefabs.py --project-root <BrainPunk> --spec <spec.json>
& $python scripts/stitch_prefabs.py --project-root <BrainPunk> --spec <spec.json> --validate-only
```

读取 `references/spec-schema.md` 获取规格字段、坐标示例和验证口径。
