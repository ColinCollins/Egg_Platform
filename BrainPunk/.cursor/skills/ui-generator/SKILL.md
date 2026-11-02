---
name: ui-generator
description: Generates Unity UI prefabs from design JSON via the project's UIGenerator workflow. Use when modifying or extending UI generation, adding layer rules, sprite fallbacks, button bindings, hierarchy organization, moving prefabs to UI folders, or BaseUIView script generation. Trigger terms: UIGenerator, Generate from JSON, default sprite folder, UI prefab from JSON, hierarchy 整理, anchorPosition 覆盖, BaseUIView, Generate UI Components, 优化 prefab 节点名称, 优化 Hierarchy 节点名称, 功能性组件优先, 创建对应名称脚本, StartPanel 参考.
---

# UIGenerator Workflow

## Prefer Unity MCP Over Code Changes

**优先使用 Unity MCP 进行操作，而不是修改项目代码。**

- **能用 MCP 完成的，不改脚本**：通过 Unity MCP 调用菜单、操作场景/预制体/资源，在不动 C# 的前提下完成任务。
- **适合用 MCP 的操作**：
  - **触发生成**：先选中 JSON，再执行 `execute_menu_item("Assets/UI Generator/Generate UI")`，或使用 `Tools/UI Generator/Generate from JSON` 选文件生成。
  - **场景与物体**：`manage_scene` 加载/保存/获取层级；`manage_gameobject` 在场景中创建、修改、移动、删除 GameObject（如创建 Cube、改位置、改父节点）。
  - **预制体与资源**：`manage_prefabs` 查看/修改 prefab 内容；`manage_asset` 查询、移动、复制资源；读取 prefab 的 YAML 文件做分析。
  - **编辑器状态**：`manage_editor` 播放/暂停/停止；`read_console` 查错；`refresh_unity` 刷新资源与编译。
- **需要改代码的情况**：调整生成逻辑（新 layer 类型、sprite 加载规则、命名规则）、新增菜单项、改 UIGeneratorConfig/Settings 行为、在生成管线中增加新步骤时，再修改 UIGenerator / UIGeneratorMenu 等脚本。
- **工作流建议**：用户要求“生成某面板”“在某某场景加个物体”“整理已有 prefab”时，先尝试用菜单 + MCP 完成；只有无法用现有菜单/MCP 实现时再提议改代码。

## When to Use This Skill

- User asks to change how UI is generated from JSON, add new layer types, or adjust button/sprite behavior.
- User mentions UIGenerator, `Tools/UI Generator`, default sprite folder, or generating prefabs from design JSON.
- Adding or changing bindings (e.g. CustomButton, ClickAudio, ClickScaleAnim) for generated buttons.
- Post-prefab workflow: hierarchy organization by rect overlap, moving prefab to `Assets/Game/UI`, or generating BaseUIView partial script.
- Optimizing node names in an existing prefab (e.g. “优化 GameVictoryPanel.prefab 的节点名称”): prefer direct YAML edit of the `.prefab` file; see **1.5 优化已有 prefab 的节点名称**. By default, only optimize **functional components** (_btn/_txt/_img/_toggle/_state); skip non-functional (decorative) nodes unless user asks for full optimization.

## Entry Points and Flow

1. **Menu（可用 MCP 直接执行，无需改代码）**
   - `Tools/UI Generator/Generate from JSON` — open file panel, pick JSON under Assets.
   - `Assets/UI Generator/Generate UI` — context menu on selected `.json` asset（先选中 JSON 再执行）.
   - **MCP**：先选中 JSON，再 `execute_menu_item("Assets/UI Generator/Generate UI")`；或 `execute_menu_item("Tools/UI Generator/Generate from JSON")` 用文件对话框选 JSON。

2. **Pipeline（自动按顺序执行）**
   - `UIGeneratorMenu.GenerateUIPrefab(jsonPath)` 加载 **UIGeneratorSettings.Instance**，构建 **UIGeneratorConfig**，创建 **UIGenerator(config)**，调用 **GenerateUIPrefab(jsonPath)**。
   - 在 **UIGenerator.GenerateUIPrefab** 内自动顺序为：
     1. 创建所有 layer 节点；
     2. **Hierarchy organization**（`RebuildHierarchyByRectContainment`）按 rect 覆盖关系整理树状层级；
     3. **节点名称精简**（`SimplifyNodeNames`）按 1.5 的命名规则对 _btn/_txt/_img/_toggle/_state 节点改名；
     4. 返回根 GameObject。
   - Menu 将根节点 **SaveAsPrefabAsset** 到 `settings.outputPath`，销毁临时根。
   - **保存后**可选的后续步骤：move to UI folder（若存在）→ **检查是否存在对应界面脚本**（见下方）→ optional BaseUIView script generation。

3. **Settings (preset)**
   - **UIGeneratorSettings** (ScriptableObject): `Tools/UI Generator/Settings` or load from `Assets/Game/Editor/UIGenerator/UIGeneratorSettings.asset`.
   - Fields: `outputPath`, `defaultTMPFont`, `defaultSpriteFolder`.
   - Config is built in editor from Settings (e.g. `config.DefaultSpriteFolder = settings.defaultSpriteFolder`). Paths use forward slashes.

## Generator Structure

- **UIGenerator** (namespace `Game.Editor.UIGenerator`): takes **UIGeneratorConfig**, produces a root GameObject (Canvas + root with artboard name).
- **Layer types** (from JSON `type` and naming):
  - **Text**: `LayerData.IsTextLayer` (`type == "textLayer"`) → **CreateTextLayer** (TextMeshProUGUI, name lower + `_txt`).
  - **Shape**: `IsShapeLayer` → **CreateShapeLayer** (Image, default color from config).
  - **Image**: `IsImageLayer` (`type == "layer"`) → **CreateImageLayer** (Image, sprite load; optional button + triggers).

## Sprite Loading (Image layers)

- **LoadSprite(layerName, jsonPath)**:
  1. Try JSON directory + `layerName` with extensions `.png`, `.jpg`, `.jpeg` (paths normalized to Assets, forward slashes).
  2. If not found and **config.DefaultSpriteFolder** is set: `AssetDatabase.FindAssets("t:Sprite", new[] { folder })`, then match by filename without extension (case-insensitive) to `RemoveExtension(layerName)`.
- Use **config.DefaultSpriteFolder** from Settings’ **defaultSpriteFolder** so “file management preset” controls fallback folder.

## Button and Trigger Bindings

- **Image layers** whose name contains **`_btn_`** get:
  1. **CustomButton** (reflection: `GetTypeFromAssemblies("CustomButton")`).
  2. **BindButtonTriggerIfMissing(obj, "ClickAudio")** and **BindButtonTriggerIfMissing(obj, "ClickScaleAnim")** — add component only if missing.
- CustomButton discovers **ButtonClickTrigger** via `GetComponentsInChildren<ButtonClickTrigger>()`, so ClickAudio and ClickScaleAnim on the same GameObject are used automatically.
- Use **GetTypeFromAssemblies(typeName)** for any runtime type (Assembly-CSharp or current domain); **BindButtonTriggerIfMissing** only adds when component is absent.

## Conventions

- Paths: `Path.Combine(...).Replace('\\', '/')` or `string.Replace('\\', '/')` for Unity/Assets paths.
- Naming: `_icon_` in layer name → Image `raycastTarget = false`. `_btn_` → button + triggers.
- Text layers: object name = `nameWithoutExtension.ToLower() + "_txt"`.
- Design resolution: set from artboard bounds; position/size conversion uses **config.DesignResolution**.

## Post-Prefab Workflow (after prefab is generated)

**执行 UIGenerator 时，以下步骤按顺序自动执行**（保存前在内存中完成）：**1. Hierarchy organization** → **1.5 节点名称精简**（代码中 `SimplifyNodeNames`，与 1.5 命名规则一致）→ 保存 prefab。保存后：**2. Move prefab to UI folder (if applicable)** → **3. 检查是否存在对应界面脚本** → **4. Optional: BaseUIView script generation**。

- **3. 检查是否存在对应界面脚本**（生成好界面后必须执行）：
  - 根据生成的 prefab 名称（如 `NoAdsPopup.prefab`）得到面板名 `{PanelName}`（即文件名无扩展名）。
  - 检查路径 `Assets/Game/UI/{PanelName}/Scripts/{PanelName}.cs` 是否存在（若 prefab 在 `Assets/Game/UI/{X}/Resources/` 下，则 PanelName 取 `X` 或 prefab 文件名；若在 Generated 等通用目录，则 PanelName = prefab 文件名无扩展名，并检查是否存在 `Assets/Game/UI/{PanelName}/` 及其中 Scripts）。
  - **若不存在**：按 **4. 创建对应名称脚本流程** 在 `Assets/Game/UI/{PanelName}/Scripts/` 下创建 `{PanelName}.cs`（参考 StartPanel.cs 的继承与 Create/OnOpen 结构）。
  - **若已存在**：无需创建，后续可在 prefab 根节点挂该脚本并做 Generate UI Components / Bind Components。

- **MCP 可做**：`manage_asset(action="get_info"|"search")` 查 prefab 路径与资源；直接读取 `Assets/.../xxx.prefab` 的 YAML 分析层级与名称；`manage_scene` 在场景中创建/调整物体。对**已有 prefab 文件**做 hierarchy/改名 若项目未提供对应菜单，则需加菜单或改代码；有菜单时用 `execute_menu_item` 触发即可。

### 1.5 节点名称精简（生成时自动执行；已有 prefab 可改 YAML）

- **生成时自动执行**：在 UIGenerator 流程中，保存 prefab 前会先执行 **SimplifyNodeNames**；仅对**原本就带** _btn/_txt/_img/_toggle/_state 后缀且组件匹配的节点做全面优化，其他节点做简单优化（不增加后缀）。
- **已有 prefab 单独优化时**：当用户要求「优化某 prefab 的 Hierarchy 节点名称」且不重新跑生成时，**优先通过直接编辑 prefab 的 YAML 文件**完成，不修改 C# 脚本。

- **优先跳过非功能性组件**：
  - **功能性组件**（需优先优化）：带 `_btn`、`_txt`、`_img`、`_toggle`、`_state` 且组件匹配的节点，BaseUIViewEditor 会扫描绑定，必须优化以便生成字段。
  - **非功能性组件**（可优先跳过）：纯装饰（如 pic_xxx、bak_xxx、icon_xxx 无后缀）、容器（Content、Root 等）、背景图等。默认**不修改**，除非用户明确要求「全部优化」或「包括装饰/背景」。
  - **有要求再改**：用户只说「优化节点名称」时，仅处理功能性组件；用户说「全部优化」「包括非功能组件」「连装饰一起改」时，再对非功能性节点做简单优化。

- **适用场景**：对 `Assets/Game/Prefabs/UI/Generated/xxx.prefab` 或 `Assets/Game/UI/xxx/Resources/xxx.prefab` 等已有 prefab 做节点命名优化。
- **做法**：
  1. 用 `read_file` 读取目标 `.prefab` 文件（Unity 预制体为 YAML 格式）。
  2. 在文件中找到所有 `m_Name: 原名称`（GameObject 的 serialized 名称）。
  3. 若用户未要求「全部优化」，则**只对功能性组件**（_btn/_txt/_img/_toggle/_state）应用命名规则；非功能性节点跳过。
  4. 按下面「命名规则」对节点做**字符串替换**（如 `search_replace`），将 `m_Name: 原名称` 改为 `m_Name: 新名称`。
  5. 保存后 Unity 刷新即可生效；无需改任何脚本。

- **命名规则（分两类）**：
  - **原本有 _btn / _txt / _img / _toggle / _state 后缀的**（且组件匹配）：**全面优化**。保留该后缀，中间部分：去掉 `UI_`、`UI_L_`、面板/artboard 名、尾部 `_01`/`_02` 等，小写+下划线保留语义；过长文案可收束（如 `thebestway..._txt` → `quote_txt`）。便于 BaseUIViewEditor 识别。
  - **其他对象**（无上述后缀）：**简单优化**。仅去掉 `UI_`、`UI_L_`、尾部 `_01`/`_02`，中间小写+下划线保留语义；**不增加** `_img`、`_btn` 等额外后缀。例如 `UI_shengli_bak_xia_01` → `bak_xia`（不改为 `bak_xia_img`）。
  - **同组件同名区分**：若多个节点精简后得到**相同组件类型 + 相同名称**（如两个都是 `title_txt`），则在名称中插入数字区分：第一个保持 `title_txt`，第二个为 `title_2_txt`，第三个为 `title_3_txt`，依此类推（格式：`base_num_suffix`）。避免 BaseUIView 生成字段重名。
  - 根节点/画板名（如 `GameVictoryPanel`、`胜利常态2`）一般不改。

- **示例映射**（便于实现时对照）：
  - 有后缀（全面）：`thebestwaytopredictthefutureistocreateit_txt` → `quote_txt`；`nextlevel_txt` → `next_level_txt`；`UI_shengli_btn_next_01` → `next_btn`（原名含 _btn，保留后缀）。
  - 无后缀（简单）：`UI_shengli_bak_xia_01` → `bak_xia`；`UI_shengli_bak_shang_01` → `bak_shang`；`UI_shengli_pic_gongxi_01` → `pic_gongxi`；`UI_L_shengli_pic_zhuese_01` → `pic_zhuese`（均不追加 _img 等后缀）。
  - 同组件同名加 num：两个 `title_txt` → `title_txt`、`title_2_txt`；三个 `close_btn` → `close_btn`、`close_2_btn`、`close_3_btn`。

### 1. Hierarchy organization (树状结构管理)

- **Goal**: Turn the flat list of layer GameObjects under the root into a tree by **anchorPosition/rect 覆盖关系** — i.e. when one RectTransform’s rect fully contains another’s, make the contained one a **child** of the container (smallest containing rect wins as parent).
- **Algorithm (outline)**:
  - Collect all direct children of the artboard root that have **RectTransform**.
  - For each node, compute its **rect in root space** (anchorMin/anchorMax + sizeDelta + anchoredPosition, or use `RectTransformUtility` / world corners then transform to root local).
  - Build containment: A contains B iff A’s rect fully contains B’s rect (with optional small tolerance). For each node, choose as parent the **smallest** node that contains it; if none, keep under root.
  - Rebuild hierarchy: set parent so that contained nodes are children of the chosen container, preserving order among siblings where needed.
- **Button / UI element name simplification (与 hierarchy 同流程)**  
  - **只对原本有 _ 后缀的名称全面优化**：名称中含下列**后缀**且组件匹配的节点做**全面优化**（去前缀、去_01、保留后缀、中间精简）。**其他对象**只做**简单优化**（去 UI_、去尾部 _01，中间小写+下划线），**不增加** _img、_btn 等额外后缀。
  - **Recognition (与 BaseUIViewEditor 一致)**：全面优化仅针对含下列后缀且组件匹配的节点；**后缀必须保留**。
    - `_btn` + **CustomButton**
    - `_txt` + **TextMeshProUGUI**
    - `_img` + **Image**
    - `_toggle` + **Toggle**
    - `_state`（无组件要求，视为 GameObject）
  - **命名规则 (参考 BaseUIViewEditor.GetFieldName)**：对上述节点，后缀在 GameObject 名上保持为 `_btn` / `_txt` / `_img` / `_toggle` / `_state`；中间部分：去掉 `UI_`、面板名/artboard 名、尾部 `_01`/`_02` 等，小写+下划线保留语义（如 `setting_huang_btn`、`close_btn`）。不要改成 PascalCase（那是 FieldName）。
  - **流程**：在 hierarchy 整理前后均可；对“有后缀”节点按上规则生成新名称；“无后缀”节点仅做简单精简，不追加后缀。若新名称与当前名不同则 `gameObject.name = newName`（prefab 内修改后需 SetDirty/ApplyPrefab）。
- **Where to implement**: Either inside **UIGenerator** after creating all layers (before returning the root), or in a separate post-process step that runs on the prefab root after save (e.g. in **UIGeneratorMenu** after `SaveAsPrefabAsset`, open prefab, run hierarchy pass + name simplification, save). Use **EditorUtility.SetDirty** / prefab apply when modifying in editor.

### 2. Move prefab to UI folder (参考 Assets/Game/UI)

- **Directory structure** under `Assets/Game/UI/`:
  - One folder per panel/view: e.g. `StartPanel`, `GamePanel`, `SettingPopup`, `LevelChoicePanel`, `Popup` (shared).
  - Under each: **Scripts/** (e.g. `StartPanel.cs`, `StartPanel.Generated.cs`), **Resources/** or **Prefabs/** (prefabs), **Sprites/** (art, JSON).
- **Rule**: After generating prefab `{fileName}.prefab` (e.g. `StartPanel.prefab`):
  1. **Check** if folder `Assets/Game/UI/{fileName}/` exists (e.g. `Assets/Game/UI/StartPanel/`).
  2. **If yes**: Move (or copy) the prefab into that folder under **Resources/** or **Prefabs/** (e.g. `Assets/Game/UI/StartPanel/Resources/StartPanel.prefab`). Prefer **Resources** if the panel is loaded at runtime via `Resources.Load`; use **Prefabs** if only referenced in editor/scenes.
  3. **If no**: Leave prefab in **settings.outputPath** (e.g. `Assets/Game/Prefabs/UI/Generated`), or optionally move to a global `Assets/Game/UI/Resources` (or similar) if you want a single place for “unassigned” generated prefabs.
- Paths: use forward slashes; create **Resources** or **Prefabs** subfolder if missing. Use **AssetDatabase.MoveAsset** (or delete + save to new path) and **AssetDatabase.Refresh**.

### 3. BaseUIView and script generation

- **BaseUIView** (in **Bear.UI** or project): base class for UI views. Each panel is a **partial class** (e.g. `public partial class GamePlayPanel : BaseUIView`).
- **BaseUIViewEditor** (`Assets/Game/Editor/BaseUIViewEditor.cs`): CustomEditor for BaseUIView. It:
  - **Generate UI Components** / **Rescan & Regenerate Partial Script**: scans children of the view GameObject, generates a **partial** file `{ClassName}.Generated.cs` in the **same directory** as the main script (via **GetScriptPath** → **GetPartialScriptPath**). The generated file declares `[SerializeField] private` fields for CustomButton, TextMeshProUGUI, Image, Toggle, GameObject by naming.
  - **Bind Components to Inspector**: after compilation, finds child objects by name and assigns them to the serialized fields (requires the partial to be compiled).
- **When to generate view script**:
  - If the generated prefab should become a **bindable view** (like GamePlayPanel, StartPanel): the **root** of the prefab (artboard root) must have a **BaseUIView** (or subclass) component. The **main** script (e.g. `StartPanel.cs`) must already exist under `Assets/Game/UI/{PanelName}/Scripts/` and be a **partial class** inheriting BaseUIView.
  - **Option A (manual)**: After moving prefab to `Assets/Game/UI/{PanelName}/Resources/`, user opens prefab, adds **BaseUIView** (or the existing panel script) to root, then in Inspector clicks “Generate UI Components”, waits for compile, then “Bind Components to Inspector”.
  - **Option B (command)**: Add a menu/item (e.g. `Tools/UI Generator/Generate View Script for Selected Prefab` or run after Generate): ensure root has a BaseUIView component; call **BaseUIViewEditor** logic programmatically (e.g. get Editor via **Editor.CreateEditor**, invoke the method that does **ScanChildObjects** + **GeneratePartialClassCode** + **SavePartialScript**), then prompt user to run “Bind Components to Inspector” after compile, or run binding via reflection if safe.
- **Creating a new panel from scratch**: If there is **no** existing `{PanelName}.cs` under `Assets/Game/UI/{PanelName}/Scripts/`, either (1) add a command that creates a minimal partial class stub (e.g. `public partial class {PanelName} : BaseUIView { }`) and the folder structure, then run generation, or (2) document that user creates the script and folder first, then generates prefab and runs “Generate UI Components” + “Bind Components to Inspector”.

### 4. 创建对应名称脚本流程（参考 StartPanel.cs）

新建面板时，在 `Assets/Game/UI/{PanelName}/Scripts/` 下创建与面板同名的主脚本 `{PanelName}.cs`，继承与结构参考 **StartPanel**。

- **脚本路径**：`Assets/Game/UI/{PanelName}/Scripts/{PanelName}.cs`（PanelName 与 UI 文件夹名一致，如 `StartPanel`、`ShopPanel`、`NoAdsPopup`）。
- **类声明**：`public partial class {PanelName} : BaseUIView`；如需与项目一致可加接口，例如 `, IDebuger, IEventSender`（参考 StartPanel）。
- **命名空间**：与 StartPanel 一致，顶层类即可，无需包在 namespace 内（除非项目统一要求）。
- **常用 using**：`Bear.UI`（BaseUIView）、`Bear.EventSystem` / `Bear.Logger`（若用接口）、`Game.Events` 等按需；TMPro 若用到再加。
- **必须/推荐内容**：
  1. **OnOpen()**：`public override void OnOpen()` 中调用 `base.OnOpen()`，再挂接按钮等逻辑（如 `XxxBtn.OnClick += Handler`）。
  2. **Create()**：静态工厂方法，与 StartPanel 一致：  
     `var panel = UIManager.Instance.OpenUI<{PanelName}>($"{typeof({PanelName}).Name}", UILayer.Normal); return panel;`
- **后续**：prefab 放到该面板的 Resources 后，在 prefab 根节点挂此脚本（或 BaseUIView 子类），在 Inspector 用 BaseUIViewEditor 的「Generate UI Components」生成 `{PanelName}.Generated.cs`，编译后再「Bind Components to Inspector」。

**参考**：`Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`（继承 `BaseUIView, IDebuger, IEventSender`、OnOpen、Create 写法）。

## Key Files

| File | Role |
|------|------|
| `Assets/Game/Editor/UIGenerator/UIGenerator.cs` | Core generator; layer creation, LoadSprite, button/trigger binding. |
| `Assets/Game/Editor/UIGenerator/UIGeneratorMenu.cs` | Menu items; builds config from Settings, runs generator, saves prefab. Can extend with post-prefab steps (hierarchy, move, optional view script). |
| `Assets/Game/Editor/UIGenerator/UIGeneratorSettings.cs` | ScriptableObject preset (output path, font, default sprite folder). |
| `Assets/Game/Editor/UIGenerator/Models/UIGeneratorConfig.cs` | Runtime config (resolution, DefaultFont, DefaultSpriteFolder, PlaceholderColor, etc.). |
| `Assets/Game/Editor/UIGenerator/Models/LayerData.cs` | JSON layer (Id, Index, Name, Type, X, Y, Width, Height, TextInfo); IsTextLayer, IsShapeLayer, IsImageLayer. |
| `Assets/Game/Editor/BaseUIViewEditor.cs` | Generates .Generated.cs and binds components for BaseUIView; GetScriptPath, GetPartialScriptPath, ScanChildObjects, GeneratePartialClassCode, BindComponentsToInspector. |
| `Assets/Game/UI/{PanelName}/Scripts/*.cs` | Panel partial class + .Generated.cs; prefabs in Resources/ or Prefabs/. |

## Extending the Workflow

- **先考虑 MCP**：能用 `execute_menu_item`、`manage_scene`、`manage_gameobject`、`manage_prefabs`、`manage_asset` 等完成的（如触发生成、在场景中加物体、移动资源），不新增或修改脚本。
- New layer type: in **CreateLayerGameObject** add condition on `layer.Type` or name pattern, then a dedicated `CreateXxxLayer(layer, obj, jsonPath)`.
- New preset: add field to **UIGeneratorSettings** and map it to **UIGeneratorConfig** in **UIGeneratorMenu.GenerateUIPrefab**.
- New button trigger: ensure type is discoverable by **GetTypeFromAssemblies** and call **BindButtonTriggerIfMissing(obj, "TypeName")** in the same `_btn_` block.
- Post-prefab: implement hierarchy pass (rect containment → parent choice → reparent), then move prefab under `Assets/Game/UI/{Name}/Resources/` or `Prefabs/` when folder exists, then optionally hook into BaseUIViewEditor for script generation/binding.
