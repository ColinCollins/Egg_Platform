---
name: brainpunk-align-postcard-prefab
description: Align a specified BrainPunk postcard prefab with a reference postcard prefab while preserving the target theme's sprites, text, piece count, meta GUID, and intentional art layout. Use when converting a flat postcard art prefab into the runtime structure represented by Postcard_universe.prefab, repairing missing postcard hierarchy/components/nested cost or completion prefabs, or auditing whether two postcard prefabs have equivalent structure and presentation contracts.
---

# BrainPunk 明信片 Prefab 结构对齐

## 默认边界

- 项目根：`D:\GitStore\BrinPunk\BrainPunk`
- 默认参考：`Assets/Game/UI/Postcard/Prefabs/Pages/Postcard_universe.prefab`
- 目标 prefab：必须使用用户明确指定的项目内路径。
- Editor 工具：`Assets/Editor/PostcardPrefabStructureAligner.cs`。
- 手动入口：在 Project 中右键符合规则的 prefab，选择 `Bear/Postcard/补齐结构与组件`。
- Skill/命令行入口：`Bear.Editor.PostcardPrefabStructureAligner.AlignFromCommandLine`，使用 `-postcardTarget <目标prefab路径>` 指定目标。
- 审计脚本：`scripts/audit_postcard_prefab.py`，只读 prefab；除非显式传入 `--json-out`，否则不写文件。

“保持相同的结构表现”指运行时结构契约等价，不要求不同主题拥有相同拼图数量或相同图片 GUID。对齐范围包括根组件、层级职责、重复拼图块结构、嵌套 prefab、序列化业务引用、Animator、Canvas/射线行为以及对应节点的 RectTransform/UI 表现。

## 强制读取

1. 读取仓库与 `BrainPunk/AGENTS.md`、当前 feature plan、`.specify/memory/constitution.md`。
2. 读取 `DevelopMD/Editor态开发约束.md`。
3. 搜索 `DevelopMD/` 与 `Assets/Game/UI/Postcard/` 中的明信片文档；没有匹配文档时明确记录。
4. 记录开始前 `git status --short`，将用户已有改动与本次修改分开。

## 执行流程

### 1. 建立只读基线

记录参考与目标 prefab 的 SHA-256，以及目标 `.meta` 的 GUID。运行：

```powershell
python <skill目录>/scripts/audit_postcard_prefab.py `
  --project-root D:\GitStore\BrinPunk\BrainPunk `
  --reference Assets/Game/UI/Postcard/Prefabs/Pages/Postcard_universe.prefab `
  --target <目标prefab路径>
```

把报告分成：`facts`、`direct conclusions`、`hypotheses`、`missing evidence`。静态 YAML 只能证明序列化事实，不能证明 Unity 已成功解析或运行时表现正确。

### 2. 建立语义映射

不要仅按节点名匹配。先为参考与目标建立语义角色表：

- prefab 根与主页面容器；
- 主题背景、角色图、标题/装饰；
- 每个拼图组、正常图、缺失/遮罩图、感叹号、CostBlock；
- CompleteMode/完成态；
- `Postcard_PageItem`、按钮、Animator 及其序列化引用。

允许主题名称、拼图数量与美术层级不同。重复拼图组按“父组内组件职责 + sibling 顺序 + 图片资源”映射；映射不唯一时停止自动改造并报告歧义。

### 3. 选择最小改造策略

- 目标已经具备运行时结构：只补缺失节点、组件、嵌套 prefab 或引用。
- 目标仍是扁平美术 prefab：以目标美术层级为输入，在 Unity Editor API 中补齐参考结构；不要把参考 YAML 整文件覆盖到目标。
- 不把参考主题的 Sprite、Texture、文字、材质或主题命名写入目标。
- 保留目标拼图数量与明确的美术布局；只有语义角色一一对应时才复制参考 RectTransform/排序/射线字段。
- 保留目标 prefab 的 `.meta` GUID。未经用户明确允许，不整文件替换已有且任务前已修改的目标 prefab。

### 4. 通过 Unity API 修改

优先复用项目内的 `Assets/Editor/PostcardPrefabStructureAligner.cs`。该工具会识别 `Assets/Game/UI/Postcard/Sprites/Pictures/` 下名称以 `Postcard_` 开头的 prefab，提取主题背景、角色图、拼图组和布局，再选择拼图数量一致且符合 universe 运行时契约的 Resources 模板进行保存。Python 审计脚本只负责只读校验，不负责写 prefab。

手动执行：

1. 在 Unity Project 窗口选择一个或多个符合规则的 `Postcard_*.prefab`。
2. 右键选择 `Bear/Postcard/补齐结构与组件`。
3. 核对确认框中的目标路径后执行；结果会弹窗汇总并写入 Console。

Skill 指定路径执行时，优先通过已打开的 Unity Editor 触发同一公共 API；若项目未被其他 Unity 实例占用，可使用批处理命令：

```powershell
& <Unity.exe> -batchmode -quit `
  -projectPath D:\GitStore\BrinPunk\BrainPunk `
  -executeMethod Bear.Editor.PostcardPrefabStructureAligner.AlignFromCommandLine `
  -postcardTarget <目标prefab路径> `
  -logFile <日志路径>
```

同一项目已在 Unity 中打开时，不再启动第二个批处理实例；改用 Project 右键菜单，或在当前 Editor 会话中调用 `TryAlignPrefabAtPath`。

Editor 命令必须满足：

- 入口显式校验参考和目标路径均位于项目 `Assets/` 内；
- 不访问 `DB`、Save、RemoteConfig、运行时单例或 bootstrap；
- 不使用 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 驱动改造；
- 新增公共 API、序列化字段或脚本时写中文注释；大段新增 C# 使用 `#region`；
- 嵌套 `Postcard_CostBlock`、CompleteMode 等对象从原 prefab 资源实例化，不手工伪造其组件；
- 所有对象引用按目标实例重新绑定，不保留指向参考 prefab contents 的临时引用；
- 不修改参考 prefab、目标 `.meta`、主题图片或无关资源。

Prefab 的结构性 YAML 包含 fileID、嵌套实例和跨对象引用，禁止用全局文本替换、正则批量替换或复制 YAML 文档块来完成结构改造。

### 5. 验证并收尾

1. 重新运行审计脚本，确认根组件、参考所需脚本 GUID、嵌套 prefab 依赖与重复结构已达到目标契约。
2. 让 Unity 重新导入并检查：无 Missing Script、无丢失引用、无 prefab 保存错误、无编译错误。
3. 在 Prefab Mode 对照参考和目标，检查层级、激活态、锚点、尺寸、遮罩、排序、Raycast、CostBlock 与完成态；可用截图保留视觉证据。
4. PlayMode 打开对应明信片页，验证解锁、星星消耗、动画、完成态和关闭/重开；无法执行时明确写入未验证风险。
5. 比较修改前后的目标 `.meta` GUID 和目标主题资源 GUID；必须保持不变。
6. 检查 `git diff --check` 与实际修改文件，不提交、不上传、不混入任务前已有改动。
7. 若实现改变了项目行为，按 `brainpunk-local-doc-update` 更新中文本地文档后再报告完成。

## 停止条件

遇到以下任一情况，不猜测、不强行保存：

- 目标路径不明确、指向参考自身或不在 `Assets/`；
- 同一目标节点存在多个合理语义映射；
- 目标主题图片缺失、重复命名导致映射不唯一；
- 需要改变拼图数量、业务解锁规则、存档或全局事件才能“对齐”；
- Unity 无法加载/保存 prefab，或保存后出现 Missing Script、丢失引用；
- 只能通过覆盖用户任务前已有修改才能继续。

最终报告修改文件、保留的主题差异、结构对齐证据、Unity/PlayMode 验证、未验证缺口与回滚方式。
