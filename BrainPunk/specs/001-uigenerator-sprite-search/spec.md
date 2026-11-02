# Feature Specification: UIGenerator Sprite Search Documentation

**Feature Branch**: N/A  
**Created**: 2026-06-12  
**Status**: Draft  
**Input**: "检测 UIGenerator 功能，整理文档。并优化 image 填充检索 sprite 的规则，Sprites 文件夹下的所有子文件夹也要包含到检索范围呢"

## User Scenarios & Testing

### User Story 1 - Generate UI with nested Sprites assets (Priority: P1)

As a UI prefab maintainer, I want UIGenerator image filling to find matching sprite files even when they are placed under subfolders inside a `Sprites` folder, so exported UI JSON can generate complete image references without requiring all cut images to be flattened.

**Why this priority**: Missing image references directly break generated prefab visual completeness and force manual repair.

**Independent Test**: Place a JSON export beside no matching image, configure the fallback sprite folder to a directory containing `Sprites/Nested/<sprite>.png`, generate the prefab, and verify the corresponding generated Image has the expected sprite assigned.

**Acceptance Scenarios**:

1. **Given** a generated image layer references `src` with a file name, **When** the file exists only under a child folder of a discovered `Sprites` directory, **Then** UIGenerator assigns that sprite to the generated Image.
2. **Given** a legacy image layer has no `src` and relies on the layer name, **When** the matching file exists only under a child folder of a discovered `Sprites` directory, **Then** UIGenerator assigns that sprite to the generated Image.
3. **Given** the same sprite file exists at a direct candidate path and under a nested folder, **When** UIGenerator searches for the image, **Then** the direct candidate keeps priority over nested fallback matches.

---

### User Story 2 - Understand UIGenerator behavior from docs (Priority: P2)

As a developer maintaining UI generation, I want local documentation to describe UIGenerator entry points, input formats, sprite search behavior, and validation gaps, so future updates can be made without rediscovering the tool from code.

**Why this priority**: The tool is editor-facing and depends on naming/path conventions; documented behavior reduces accidental regressions.

**Independent Test**: Read the updated UIGenerator documentation and verify it answers how to run generation, where settings are stored, how images are matched, and which behavior remains unverified.

**Acceptance Scenarios**:

1. **Given** a developer needs to use UIGenerator, **When** they read the documentation, **Then** they can identify the menu/context-menu entry points and settings asset location.
2. **Given** a developer needs to organize cut images, **When** they read the sprite search section, **Then** they know that child folders under discovered `Sprites` directories are included in fallback matching.

### Edge Cases

- `defaultSpriteFolder` is empty, outside `Assets`, or missing on disk.
- A JSON `src` path includes subfolder segments while the fallback match exists in a different nested subfolder.
- Multiple nested folders contain the same file name.
- The texture is imported as a multi-sprite atlas and the sprite sub-asset name must be matched.
- A layer name has no usable base name.

## Requirements

### Functional Requirements

- **FR-001**: UIGenerator MUST preserve existing direct image lookup priority for JSON-adjacent paths and direct fallback paths.
- **FR-002**: UIGenerator MUST include all child folders under each fallback `Sprites` directory when matching image files by file name.
- **FR-003**: UIGenerator MUST support nested fallback lookup for both `src`-based image layers and layer-name-based legacy image layers.
- **FR-004**: UIGenerator MUST keep the current behavior for multi-sprite assets by matching the sprite sub-asset name before falling back to the first sprite.
- **FR-005**: UIGenerator documentation MUST describe current entry points, settings, JSON format support, sprite lookup order, generated image sizing behavior, post-processing, and verification caveats.
- **FR-006**: The change MUST avoid runtime game systems, save/config data chains, platform SDK flows, and shipped level behavior.

### Key Entities

- **UIGenerator**: Editor tool that converts supported UI JSON exports into Unity UI prefab hierarchies.
- **UIGeneratorSettings**: Editor settings asset that provides output path, default fonts, and fallback sprite folder.
- **Image Layer**: A JSON layer that produces a Unity Image and needs a matching Sprite.
- **Fallback Sprites Directory**: The configured default folder itself and any discovered directory named `Sprites` under it.

## Success Criteria

- **SC-001**: 100% of tested image layers with matching files under nested `Sprites` child folders are filled without manual sprite assignment.
- **SC-002**: Existing tested direct-path sprite matches continue to resolve to the same sprite before nested fallback is considered.
- **SC-003**: A developer can identify the nested `Sprites` child-folder rule from local documentation within 2 minutes.
- **SC-004**: Static verification reports no new compile errors in the UIGenerator editor code.

## Assumptions

- The requested "image 填充" refers to generated Unity UI Image sprite assignment in the editor UIGenerator.
- Matching by file name is acceptable for nested fallback search; if duplicates exist, the first file returned by filesystem enumeration is used.
- This feature is editor tooling only and does not intentionally change runtime UI loading or mobile player behavior.
- `.specify/templates` is empty in this workspace, so this specification uses the standard SpecKit section structure directly.

## Evidence & Verification Notes

- Local evidence: UIGenerator currently lives under `Assets/BearGame/com.bear.ui@919f05cf0418/Editor/UIGenerator/`.
- Local evidence: package documentation already contains a UIGenerator section in `Assets/BearGame/com.bear.ui@919f05cf0418/Readme.md`.
- Local evidence: `DevelopMD/Editor态开发约束.md` applies because this is editor tooling.
- Missing project context: `specs/014-level74-fire-burn/plan.md` referenced by `AGENTS.md` does not exist in this workspace.
