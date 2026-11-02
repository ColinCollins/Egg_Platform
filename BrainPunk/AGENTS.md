# BrainPunk Workspace Rules

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current feature plan:
`../specs/037-remote-resource-loading/plan.md`
<!-- SPECKIT END -->

## Scope

These rules apply to the whole repository unless a deeper `AGENTS.md` overrides them for a subdirectory.

## Startup Procedure

Before making code changes:

1. Read this `AGENTS.md`.
2. Read `../.specify/memory/constitution.md` for project-level stability,
   Unity/C#, mobile performance, regression, and reuse rules.
3. Read project rules referenced here that match the current task.
4. If the task touches a specific module, scan `DevelopMD/` and nearby module docs for matching files by keyword before editing.

Use this minimum lookup flow:

- Search `DevelopMD/` for files whose names match the task domain.
- Search code for the target class/module name and check for adjacent README / md docs.
- If the task touches editor lifecycle, prefab preview, config preview, or inspector logic, always read `DevelopMD/Editor态开发约束.md` first.

### 修改前核对门槛

- **凡涉及已有功能的任何改动，必须先完成前置核对，确认拟议改法有依据且符合原有约束后，才能开始修改。** 新增功能只要接入、复用或影响旧流程，也适用本门槛；覆盖代码、配置、Prefab、资源、编辑器及生成/构建工具，不因改动小、只加字段、只接回调或当前能编译而豁免。
- **先明确验收对象。** 写明原有行为、本次要改变的行为、必须保留的行为和受影响入口；不得把用户要求的整体功能缩成某个函数能运行。区分用户已明确的需求、代码事实、推断和未知项。
- **先核对原设计与实际实现。** 阅读相关功能文档，并沿当前源码、配置和资源追查入口、调用方/被调用方、数据读写、状态转换、生命周期及结束/清理流程。确认目标文件由谁维护、真实输入或权威来源在哪里、哪些内容会被工具覆盖、哪些内容只读，以及已有可复用入口；不能仅凭名称或一个相似文件判断。
- **先评估改法与影响。** 对照既有职责和约束确定允许改动的位置，检查共享调用方及适用的失败、取消、重复、重入、异步晚到、退出重开、旧数据与平台分支。按同一验收范围确定验证办法；专项约束按任务实际涉及的模块读取，不用无关检查充数。
- **关键前提未确认，不得开始依赖它的修改。** 继续只读查证或执行隔离且不改生产文件的验证；代码中能查到的事实由 Agent 自己查，只有产品取舍或无法取得的关键事实才向用户询问。缺少文档时须明确缺口，并用当前实现补足依据。实现中发现新的依赖、冲突或影响范围时，先暂停受影响部分并补查，再继续，禁止先写后补。
- **首次修改前留下可复查记录。** 在既有功能文档或当日记录中简短写明：`需求与行为基线 → 来源路径/符号 → 原流程及维护边界 → 拟修改位置、理由与影响范围 → 验证场景及未决项`。只有依赖的关键未决项已解决，才可进入对应实现。用户指出漏查后补做必须注明“事后补查”，不得伪装成事前核对。
- 修改后按事前确定的行为和场景复核。编译、静态检查、模拟和真实环境验证分别报告，只能证明各自覆盖的范围；不得用事后测试代替前置核对，也不得用局部通过宣称整个功能正确。该门槛要求 Agent 自行查证，不新增常规实现选择的逐次用户审批。

生成代码专项：涉及 partial、DB、序列化字段、配置或自动绑定时，必须在修改前追到实际生成器/模板与输入，核对输出、覆盖成员、初始化、类型引用和属性特性；可在 TEMP 预览时先检查生成结果。DB 的 `_Partial.cs` 只同步生成器应输出的属性和初始化，不放手写业务逻辑；不得在手写文件重复声明生成属性，不得靠手补生成文件的 using 或特性维持编译。修复/兼容逻辑放在既有手写入口；与本任务无关的既有生成差异不得整文件覆盖。

## Speckit Worktree Isolation

Before running or carrying out any Speckit Specify workflow that will create or
modify a feature specification, prefer creating a dedicated Git worktree from the
repository root (`..`) so specification and later implementation changes are
isolated from other active work.

Use this default shape unless the user gives a different branch or path:

- worktree path: a sibling directory of `D:\GitStore\BrinPunk`, named after the
  feature short name when practical.
- branch name: `codex/<feature-short-name>`.
- run Speckit Specify and subsequent edits from inside that worktree.

If an appropriate worktree already exists for the feature, reuse it instead of
creating another one. If the repository state, branch conflict, filesystem, or
user instruction prevents worktree creation, state that explicitly before
continuing in the current working tree.

## Task Doc Map

When task keywords match the following domains, read these docs before editing:

- `Editor`, `OnValidate`, `ExecuteAlways`, `prefab preview`, `edit mode`
  - `DevelopMD/Editor态开发约束.md`
- `Account`, `Login`
  - `DevelopMD/AccountManager_Reference.md`
  - `DevelopMD/LoginTODO.md`
- `Achievement`
  - `DevelopMD/Achievement_Integration_Guide.md`
  - `DevelopMD/AchievementTODO.md`
- `Rating`
  - `DevelopMD/GuruRating_Documentation.md`

If there is no matching doc, say so explicitly and continue from code context.

## Hard Rules

- `Packages/` 中的所有内容必须视为只读；禁止新增、修改、删除、格式化或生成其中的文件。
  接回调、修复功能或构建等普通要求不包含修改 Package 的授权，必须优先在游戏侧复用已有公开接口。
  不得通过 `checkout`、`reset`、`restore`、`clean` 或补丁/辅助脚本间接改写 Packages。
  除非用户明确要求修改或撤销本规则，否则不得修改 Packages 内容。
- `Assets/GF` 是外部 GF 子模块，其仓库内的代码、资源与配置必须视为只读；禁止新增、修改、
  删除、格式化或生成其中的文件，也不得通过 `checkout`、`reset`、`clean`、`cherry-pick`、
  `commit` 等操作改写其工作树或提交历史。排查或修复 GF 子模块下载、提交不可达、构建失败
  等问题时，只允许执行只读检查，并优先从父仓库 gitlink 指针、CI 拉取流程或缺失远端提交
  的交付状态处理。任何会改变 GF 远端分支、标签或提交可达性的操作也必须另行获得用户明确
  授权；普通的“修复构建”要求不得推定包含该授权。除非用户明确要求更新或撤销本规则，
  不得修改 GF 仓库内容。
- 用户的所有关机指令均按一次性操作处理：同一条指令最多发起一次关机动作；不得创建或保留重复、循环、周期或自动重试的关机任务。关机已经发起或安排后，额度恢复、继续消息或自动续跑不得再次发起；只有用户明确提出新的关机指令时才可执行下一次。
- 修改 `RemoteConfigService`、`RemoteConfigManager.ForceFetch()`、Firebase `FetchAndActivate`、最小拉取间隔、
  Loading 等待/超时、远端缓存回退或 `DrawGameLevelCtrl` 远端排序初始化前，必须先阅读
  `DevelopMD/RemoteConfigService_Design.md`，并在动手前向用户明确警告可能造成远端值不更新、
  Loading 等待延长或卡住、Firebase 节流以及 Draw `level_sort` 使用旧缓存等风险；只有用户确认后才能继续。
  未经用户明确批准，不得移除或弱化启动期 `ForceFetch`，也不得把 DrawGame 关卡序列初始化提前到
  本轮 RemoteConfig 解析与落盘完成之前。
- 用户要求把 Luban 配置字段通过 `LubanGenerator` 写入 `Assets/Guru/guru_spec.yaml` 时，必须在同一任务中
  补齐并核对对应云控逻辑：`ConfigManager.RemoteConfig` 的字段常量与读取 API、运行时消费者改用该 API、
  远端值优先且本地 Luban 值兜底、字段名/类型一致性，以及缺字段、解析失败和非正数等业务边界验证。
  不得只完成生成器白名单、Guru 默认值或生成代码同步后就报告云控接入完成；若字段明确仅用于本地默认值，
  必须由用户当次明确说明并在功能文档中记录该例外。
- 修改主线或 DrawGame 的 `level_start`、`level_end`、`level_end_success_N`、`b_play`、
  `start_type`、`play_count`、revive、Reset、Replay、成功/退出清理或关卡运行统计前，必须先阅读
  `DevelopMD/关卡打点生命周期约定.md`。若用户的新要求与该文档任一当前有效条目明确冲突，必须先指出
  冲突章节、现行逻辑、拟议逻辑及影响范围，立即停止冲突部分的代码和文档修改，并询问用户是否确认
  替换现行约定；在用户明确确认继续前，不得把新要求视为静默覆盖，也不得先改后问。用户确认替换后，
  必须先更新该权威文档，再修改实现和其它说明。无冲突部分可独立继续。
- Existing accepted levels take priority over new abstractions. When reuse could
  change shipped level behavior, prefer local implementation, local config, or
  level-specific isolation unless regression evidence covers every affected level.
- 已完成或已验收关卡的脚本默认只允许读取与复用；除非用户明确点名该关卡或脚本并批准修改，
  不得为新关卡需求、复用便利、结构统一或问题规避而改写其内容。新关卡需要差异时，优先在
  新关卡目录新增局部脚本、继承薄封装或使用 prefab override，并把边界写入本关记录。
- Optimization and stability work must preserve original player-visible behavior,
  save/config/analytics/resource loading, UI, and platform flows unless an
  intentional behavior change is documented before implementation.
- Before optimizing existing functionality, capture the original behavior baseline,
  risk being avoided, rollback condition, and after-change consistency evidence.
- Gameplay, level, save/config, resource loading, hot reload, platform SDK, and
  performance-sensitive changes must include verification evidence or an explicit
  unverified-risk note.
- 未经用户明确要求，不得为局部 UI、关卡状态或单关表现随意新增
  `PlayEvents` / 全局事件；优先使用已有公开引用、当前控制器可达对象、
  局部 API 或关卡内隔离实现。
- 实现或修改 UI 相关逻辑功能前，必须先按功能、面板和模块名称搜索并阅读
  `DevelopMD/`、对应 UI 目录及邻近目录中的既有功能设计、使用记录和相关说明，
  并结合现有脚本、Prefab、生成绑定、生命周期入口及调用关系，梳理原有职责、
  数据/资源/事件/UI 流和可复用边界。应优先复用已经验证且职责匹配的现有组件、
  接口和流程；若复用或共享抽象可能改变既有 UI 的层级/遮罩、输入、生命周期、
  状态、资源、事件、存档或平台流程，必须先完成影响评估和回归范围确认，并优先
  采用局部实现。若未找到匹配文档，必须明确记录文档缺口，再基于本地代码和 Prefab
  证据完成设计分析；不得跳过原设计与复用性评估直接实现。
- 用户已经放入 Prefab 且脚本需要持有的对象，必须声明为 `[SerializeField]` 并在 Prefab Inspector
  显式绑定；禁止为这些引用新增 `GameObject.Find`、`Transform.Find`、`GetComponentsInChildren`
  或其它按节点名/层级扫描的运行时兜底。编辑器绑定工具也不得用名称查找代替 Inspector 绑定，
  除非用户明确要求自动生成或批量绑定。
- 未经用户明确要求，不得为单关 UI 或 `LevelCtrl` 相关逻辑直接修改共享
  `GamePlayPanel` 脚本；确需扩展时优先创建继承 `GamePlayPanel` 的子类或
  关卡专用面板组件，并绑定到对应关卡面板 prefab。
- 关卡内新增 Actor 专用动作或动画时，不得把单关动画名、状态或控制 API
  直接加入共享 `ActorCtrl`。必须参考
  `Assets/Game/Scripts/Level/Actor/Level66ActorCtrl.cs`，创建或复用对应关卡的
  `LevelXXActorCtrl : ActorCtrl`，将动画序列、关卡状态和完成回调保留在关卡
  子类中，并绑定到对应关卡 prefab；按需在子类中覆写 `HandleAnimators()`、
  `OnUpdate()` 或提供关卡专用 API，由关卡控制器调用。只有经本地证据确认是
  多关共享的基础 Actor 能力、完成受影响关卡回归评估且用户明确批准时，才可
  修改共享 `ActorCtrl`。
- Android and iOS mobile landscape behavior must be considered for runtime changes.
- 插屏、激励视频、支付、登录、系统权限或其它原生覆盖层接入死亡、Reset、Replay、胜负结算、
  切场景或资源重建流程时，必须在实现前核对：调用前的玩家/关卡状态、原生覆盖期间 Unity
  PlayerLoop 不推进时所有等待和 Tween 的行为、成功/失败回调后的状态、遮罩所有权以及输入恢复边界。
  不得假设 `ignoreTimeScale` 能在原生覆盖层暂停 Unity 帧更新期间继续推进；覆盖层显示前必须先把
  运行时推进到可安全恢复的状态，或保持不透明遮罩和输入锁直到回调后完成恢复。
- `OnValidate` must not access `DB` data paths directly or indirectly.
- `Reset`, `[ExecuteAlways]`, `[ExecuteInEditMode]`, editor `OnGUI`, prefab preview callbacks must not trigger runtime data or save chains.
- In edit mode and non-runtime state, avoid instance creation by default. Treat runtime-style instance creation as forbidden unless there is explicit protection and a clear editor-safe reason.
- Guard runtime-only logic at the outer entry with `Application.isPlaying`; do not rely on deep callee self-protection.
- Editor preview logic must use local serialized data, constants, or local read-only assets as fallback. Do not depend on runtime singleton initialization.
- During feature work, record reusable facts learned about existing systems,
  design responsibilities, data flow, prefab usage, event usage, and verification
  caveats in `DevelopMD/已有功能设计与使用记录/YYYY-MM-DD.md`. Keep facts tied to local
  evidence; mark uncertain items as pending validation instead of treating them
  as final knowledge.
- 共享设计与使用记录按本地日期（Asia/Shanghai）归档到
  `DevelopMD/已有功能设计与使用记录/YYYY-MM-DD.md`：当天文件已存在时，只更新该文件的对应主题章节，
  保留其它任务内容；不存在时才创建。不得按任务、时间戳或序号另建同日副本。
  原 `DevelopMD/已有功能设计与使用记录.md` 为只读历史记录，不追加、改写、迁移或重新格式化；
  查阅既有功能时同时检索历史文件和日期目录。模块与关卡专属文档的维护要求不变。
- After completing any new requirement, bug fix, or behavior-changing implementation,
  create or update a local feature/module document in the corresponding folder
  before reporting completion. The document must cover requirement/background,
  affected files and systems, behavior before/after, data/resource/event flow
  when applicable, verification performed, remaining risks or unverified gaps,
  and rollback path. If the corresponding folder is unclear, update
  `DevelopMD/已有功能设计与使用记录/YYYY-MM-DD.md` and state the uncertainty in the final response.
- For level-specific changes under `Assets/Game/Scripts/Level/LevelItem/Level_*`,
  create or update a local feature/usage document in that level folder when new
  behavior is implemented or discovered.
- 开发新的 DrawGame 关卡前，必须先阅读
  `Assets/Game/Scripts/Level/DrawLevel/DrawGame关卡脚本结构规范与演进记录.md`
  以及玩法最接近的已验收 DrawGame 关卡脚本结构记录，明确本关采用的参考基线；
  默认保持命名、职责拆分、生命周期入口、接口边界、UI/事件归属、Reset 与清理结构一致。
  因本关机制需要偏离时，必须在本关记录中写明差异、原因、影响和验证范围。
- DrawGame 正常胜利进入下一关时，转场时序默认必须对齐主线：请求通过后可先销毁旧 HUD，
  但已 `Resolved` 且关闭输入的旧 DLevel 必须保留到 `EnterLevelLoading` 翻页完全遮住；只有进入
  `onPageTurnComplete` 回调后才能销毁旧 DLevel、加载并初始化新 DLevel/HUD，随后由 Erase 揭开。
  除非用户明确批准并记录例外，不得恢复为创建 Loading 前提前销毁旧 DLevel；本规则不自动改变
  Reset/Replay 的既有黑幕重建边界。
- 每完成一关 DrawGame，必须在对应
  `Assets/Game/Scripts/Level/DrawLevel/LevelN/` 目录创建或更新
  `DLevel_XXX脚本结构记录.md`。记录必须以最终代码和 prefab 为证据，覆盖脚本树、
  类职责、继承/接口、关键调用与生命周期、序列化绑定、局部与共享边界、
  数据/资源/UI/事件流、相对参考关卡的差异、验证证据、剩余风险和回滚路径；
  未完成该记录不得报告该关卡交付完成。
- 每关 DrawGame 收尾时必须执行一次结构复盘，并更新上述中央演进记录中的候选或决策。
  对当前关卡安全、局部且不改变既有关卡行为的改进，可以在验证后直接采用并记录；
  共享抽象、共享接口升级或既有关卡迁移，只有在多个已完成关卡出现稳定重复模式、
  收益与职责边界明确、回归范围覆盖全部受影响关卡时才可实施。不得仅为形式统一
  自动批量改造已验收关卡；证据不足时保留为候选，在后续关卡继续验证。
- After completing or materially changing a level feature, create or update a
  risk assessment document in that same level folder. It must include original
  behavior baseline, affected files/resources, existing systems used, risk
  items, verification evidence, unverified gaps, rollback path, and any pending
  clarification.
- 额外留存的本地功能文档、风险评估、验证记录和 `DevelopMD/` 追加记录必须使用中文；除非用户明确要求英文，不能用英文编写新增文档内容。
- 实现或修改脚本时，不允许随意改写原本已有的注释内容。确需调整旧注释时，必须先确认该注释已经与代码事实冲突，并在改动说明中明确原因；普通实现不应改动无关原注释。
- 新增脚本或为脚本新增对外 API、公共方法、公共属性、序列化字段时，必须补充中文注释说明用途、调用边界和注意事项；生成或大幅新增脚本结构时，必须使用 `#region` / `#endregion` 做清晰分区。

## Analysis Discipline

- **问题解决优先原则：**每次回答和交付必须以解决当前最高优先级问题为目标，先给出明确结论、关键证据和立即可执行的处理动作，再补充背景。不得用复述问题、泛泛建议、道歉、罗列可能性或“建议进一步排查”代替实际查证。能够通过现有代码、日志、配置或工具核实的内容必须先自行核实；证据不足时必须明确标记未知项，说明缺失证据、影响和最短验证步骤。多个问题按影响程度和紧急性排序，优先闭环 P0/P1，并为每项给出操作、验收标准和失败后的下一步。
- 本项目的所有技术分析、方案、实现与交付回复都必须有可复查的逻辑和准确来源。先区分用户实测事实、源码/Prefab/配置/日志证据、由证据直接推出的结论、假设及缺失证据；关键判断给出具体文件与行号或可复查的验证记录。沿实际调用链、状态变化和生命周期检查结论的成立条件，主动寻找会推翻结论的分支或反例，不得由局部代码推断整体行为。
- 声称“已解决”“完全一致”“不影响原流程”或“已验证”前，必须逐项列出目标行为和受影响场景，用对应证据核对每一项。编译通过只证明编译，静态检查只证明所检查的路径；缺少运行时或平台证据时必须明确标记 `uncertain`，说明尚未覆盖的场景和影响。用户指出结论与实际表现冲突时，立即撤回该结论并重新查证，不得重复无证据的保证。
- 当用户要求两个或多个入口的点击表现、状态或结果“一致”时，必须以玩家可观察行为为验收对象，先列出同一场景矩阵，再逐入口核对完整链路：Prefab/生成绑定与遮罩层级、按钮激活和可交互门禁、生命周期刷新、点击转发、业务前置校验、加锁时机、平台请求、成功/失败/取消回调、提示、解锁和重入。至少覆盖正常、已报告无网、设备显示有网但服务不可用、配置或商品详情缺失、请求失败、等待弹窗关闭但无结果回调、关闭重开及晚到回调；不适用的场景要说明原因。
- 上述一致性结论必须逐场景标明代码/Prefab/日志或实测来源与验证范围；只核对点击处理函数、只通过编译、或只发现并修复一处差异，都不得宣称全部入口已对齐或真机问题已解决。未复现或缺少平台结果证据时明确标记 `uncertain`，列出仍不一致的路径及其影响，不得用推测补全结论。
- CodeGraph default lookup policy:
  - For engineering code questions, class/method discovery, call chains, data
    flow, and change impact analysis, use CodeGraph as the first lookup path
    when the project has a valid `.codegraph/` index.
  - Treat CodeGraph output as code evidence for the returned source and call
    graph, but keep the evidence-boundary rules below: do not extend conclusions
    beyond the returned code, logs, docs, or reproduced behavior.
  - Use `rg`, direct file reads, or Unity asset/YAML inspection instead of, or in
    addition to, CodeGraph when the task depends on non-code files such as
    `.prefab`, `.unity`, `.asset`, `.meta`, images, binary resources, markdown
    process docs, Speckit artifacts, exact string matches, or freshly edited
    files that CodeGraph reports as stale.
  - CodeGraph static analysis does not replace Unity compilation, tests, runtime
    logs, prefab/resource validation, or platform verification.
- Treat evidence boundaries as a hard rule: do not present unverified assumptions as facts.
- Any direct conclusion about code, prefab setup, load path, or runtime behavior must be backed by local evidence from this workspace, with file path references and line numbers when practical.
- If evidence is missing or incomplete, say `uncertain` explicitly and keep the statement in a hypothesis / validation bucket instead of the conclusion bucket.
- Separate `facts`, `direct conclusions`, `hypotheses`, and `missing evidence` when investigating bugs, runtime issues, or project behavior. Do not merge hypotheses into the conclusion section.
- Do not extend a cause chain beyond what the observed code, logs, docs, or reproduced behavior directly supports.
- When a user challenges an assumption or states that a premise is wrong, stop that line of reasoning, re-check the local evidence, and only continue after the contradiction is resolved.
- If no matching doc or local evidence exists for a claimed workflow or system behavior, say so explicitly before continuing from code context.
- Do not rely on verbal commitments as a control mechanism. Any behavior-changing commitment made to the user must be converted into an external rule, checklist item, output template, or repo-visible process before relying on it.
- If a requested constraint has not been externalized yet, describe it as pending and avoid claiming it is already enforced.
- Favor repo-visible, auditable controls over informal promises. Preferred order: `AGENTS.md` rule, task checklist, required output structure, then plain-language explanation.

## Review Checklist

When reviewing or editing Unity scripts, check for:

- `OnValidate`, `Reset`, `ExecuteAlways`, `ExecuteInEditMode`
- `DB`, `Save`, `RemoteConfig`, `Instance`, `Instantiate`
- edit mode code paths that may initialize managers, services, or bootstraps
- preview code paths that should be replaced by local fallback data

## Working Style

- Prefer minimal, targeted edits over broad refactors.
- 实现优先复用现有脚本和接口，只添加当前明确需求必要的最小代码。
  新增代码必须有具体调用或可验证问题作为依据；不得擅自增加补丁工具、打包守卫、辅助脚本、通用封装或无依据的防御性逻辑。
- 制作新主线关卡时，`ResetEvent` 及相关重置流程只添加当前流程实际需要的逻辑；不得无依据地增加重复清理、防御性兜底或通用封装。新增逻辑的必要性必须结合当前重置调用链与职责判断。
- 编程过程中不得过度编程或过度设计；优先完成当前明确需求所需的最小可运行实现，只有当现有证据显示扩展、抽象、配置化或通用化能直接降低当前风险或重复成本时才增加。
- 进行游戏编程、C# 实现和技术方案讨论时，必须基于代码证据、玩家体验、生命周期、性能、可维护性与项目约束作出独立专业判断，不得为了迎合用户而直接附和其技术意见。用户提出方案后应先评估；若发现风险、事实冲突或更合适的设计，必须明确说明不同意见、依据、可能后果和可执行替代方案。用户保留产品目标与最终取舍权，但技术结论不得因迎合而失真。
- Do not assume scattered markdown files were read automatically; use the lookup flow above each time.
- When a rule in docs conflicts with existing code, follow the documented rule and call out the conflict in the final response.
- For high-risk analysis tasks, default to the structure: `facts` / `direct conclusions` / `hypotheses` / `missing evidence`.
- When making process or behavior commitments to the user, externalize them in the workspace when appropriate and reference the resulting rule or checklist in the response.

## AI Rule Promotion

日常自动化、风险评估、CodeGraph 维护、功能实现和问题复盘中发现的新规则，不得默认直接写入本 `AGENTS.md`。

新增经验应先进入 `DevelopMD/AI规则提升队列.md`，按以下状态维护：

- `待观察`：单次出现、证据不足、尚未造成返工。
- `待固化`：多次重复出现、用户明确要求、已造成返工、影响高风险模块，或能明显减少误操作。
- `已固化`：已经写入 `AGENTS.md`、检查清单、报告模板或自动化说明。
- `暂不处理`：收益低、证据不足、已过期或项目归属不明确。

每条候选必须记录来源日期与任务类型、触发事实或失败证据、适用范围、建议固化位置、提升条件和回滚方式。

只有满足以下条件之一，才允许把候选写入本 `AGENTS.md`：

- 连续多次出现同类问题。
- 用户明确要求固化。
- 已导致返工、误提交、漏验证、漏发送或错误结论。
- 涉及 P0/P1 风险、存档、资源加载、主流程、SDK、构建、CodeGraph 索引等高风险链路。
- 能形成稳定、可执行、可验证的检查项。

固化时必须只做最小可逆改动，不得把单日事实、临时状态、个人判断或未验证猜测写入长期规则。

## Daily Automation Tasks

每日自动化、风险评估和维护任务必须按以下顺序执行：

1. 读取本 `AGENTS.md`、当前 feature plan，以及任务相关的 `DevelopMD/` 文档或专项规则。
2. 记录任务开始前的 `git status --short`，区分已有改动和本次任务改动。
3. 对中文 Markdown、风险报告、飞书摘要和自动化记忆执行 PowerShell 读写时，显式使用 UTF-8；若发现乱码，必须重新读取并记录证据质量风险。
4. 从 Git 变更、工作区 diff、日志、报告产物、CodeGraph 状态或用户反馈等可验证证据出发，不用未验证推测补全结论。
5. 严格区分 `facts`、`direct conclusions`、`hypotheses` 和 `missing evidence`，尤其是风险评估、CodeGraph 维护和运行时问题排查。
6. 对每日发现的新约束，先写入 `DevelopMD/AI规则提升队列.md`，不要直接膨胀本 `AGENTS.md`。
7. 任务结束前复核本次实际修改文件、验证证据、未验证缺口、Git 上传或飞书发送结果；失败时记录失败命令、错误摘要和下一步处理建议。
8. 只提交或上传本次任务明确创建或修改的文件，不得混入任务开始前已有且无法确认归属的改动。
