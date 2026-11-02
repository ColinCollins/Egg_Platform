# LevelChoicePanel QuickTurn 功能与风险记录

## 需求与验收范围

- `LevelChoicePanel_QuickTurn` 初始化时由所属面板传入最大关卡数量 `max`、每组数量 `interval` 和当前分页。
- 组件按 `interval` 把 `1..max` 分成若干关卡区间；拖动 handle 时，`page_txt` 实时显示当前到达的区间。
- handle 的活动范围与方向必须根据 `UI_xuanguan_img_ksfybg_01` 当前 `RectTransform` 计算，不能写死像素坐标；当前轨道为 `Z = 1.5°`，handle 必须沿其中心线平移。
- 拖动期间目标页变化时立即更新所属面板页码、按钮和关卡 Item，不播放书本 Spine、音效、渐隐渐显或其它切页动画；松手接口保留但只做最终状态对齐。
- 同一个 QuickTurn 组件还要接入 `DrawGame_ChoiceLevelPanel`；组件不得依赖主线配置、主线存档或具体父面板类型。

## 修改前核对（2026-10-09）

### 可观察事实

- `ChoiceLevelPanel` 使用私有 `pageIndex` 表示当前页，`MaxCount` 表示单页 Item 数量；当前 Prefab 的 `MaxCount` 为 10。
- `LastPage`、`NextPage` 分别修改 `pageIndex`，播放 `book_turn-the-page_reverse`／`book_turn-the-page`，然后刷新按钮与 Item。
- `DrawGame_ChoiceLevelPanel` 使用独立 `_pageIndex` 与 `maxCount`，现有 `ChangePage` 只执行 Draw Item 淡出、刷新、页码保存和淡入；广告结果待返回时 `_isRewardAdPending` 会阻止分页，远端排序提交后 `RefreshRemoteConfig` 可能改变总页数并约束当前页。
- 用户新增的 `LevelChoicePanel_QuickTurnComp.prefab` 已作为嵌套 Prefab 放入 `ChoiceLevelPanel.prefab`；轨道宽度为 1270、handle 宽度为 198，但这些尺寸属于当前资源状态，不应成为运行时代码常量。
- `LevelChoicePanel_QuickTurn.cs` 与生成绑定文件已经存在；生成文件声明了 `page_txt`、轨道 Image 和 handle Image 的序列化字段，但脚本 GUID 尚未挂到 QuickTurn Prefab，三个字段也尚未在 Prefab 中绑定。
- 当前未找到 QuickTurn 专项设计文档；`ChoiceLevelPanel返回与NoAd显示功能记录.md` 只覆盖返回与 NoAd 显隐，与本需求没有冲突。
- `.codegraph` 目录存在，但当前环境没有可调用的 CodeGraph MCP，`codegraph` CLI 也不可用；本次基线来自当前源码、Prefab YAML 与资源 meta。
- 仓库规则引用的 `../specs/037-remote-resource-loading/plan.md` 当前不存在，因此无法从该计划补充本功能约束。

### 直接结论与修改边界

- QuickTurn 只维护拖动预览状态，不直接访问主线/Draw 配置、DB 或父面板私有页码；松手后通过初始化回调请求父面板切页。
- `ChoiceLevelPanel` 应把左右按钮和 QuickTurn 收敛到同一个页切换入口，继续拥有实际 `pageIndex`、Spine 动画、音效和 Item 刷新职责。
- `DrawGame_ChoiceLevelPanel` 复用同一初始化契约，但继续由既有 `ChangePage` 执行 Draw 淡入淡出、存档与广告门禁；QuickTurn 不内置主线翻书动画，也不绕过 Draw 的 `_isRewardAdPending`。
- 轨道、handle、文本和父面板中的 QuickTurn 组件均使用 Prefab Inspector 序列化绑定，不增加名称或层级扫描兜底。
- 不修改生成绑定脚本、存档结构、关卡配置、打点、资源加载、Packages、GuruSDK 或 GF。

### 计划验证场景与未决项

- 静态验证：脚本 GUID、Prefab fileID、序列化引用、YAML anchor 唯一性、代码编译和 `git diff --check`。
- 行为验证：主线与 Draw 分别覆盖首组、中间组、末组、末组不足 `interval`、只有一组、拖动后松手、切页动画期间重复输入、关闭重开后当前位置同步；Draw 额外覆盖广告等待门禁与远端排序数量变化。
- 移动端验证：Android/iOS 横屏触控坐标与不同 Canvas 缩放下的轨道范围。
- `uncertain`：尚未进行 Unity PlayMode 和移动端真机拖动验证；最终触控手感与美术轨道边缘对齐需以运行画面确认。

## 最终修改后行为

- `LevelChoicePanel_QuickTurn.Initialize(max, interval, currentPageIndex, getCurrentPageIndex, onPageChanged, onPageSelected)` 建立一次分页映射：组数为
  `ceil(max / interval)`，第 `n` 组显示 `n * interval + 1` 至 `min((n + 1) * interval, max)`；空数据显示
  `0-0`，只有一组时保留显示但不接受拖动。
- handle 的起止点在运行时读取轨道与 handle 的 `RectTransform.rect`、pivot 和 Transform 计算，确保 handle 完整留在
  `UI_xuanguan_img_ksfybg_01` 范围内；移动方向直接跟随轨道局部轴（当前 `Z = 1.5°`），不重复硬编码角度。拖动起点保留
  手指沿轨道方向相对 handle 的偏移，不会在按下时跳位。
- 拖动期间更新 handle 与 `page_txt`；目标页变化时立即执行 `onPageChanged`。松手后的 `onPageSelected` 接口保留，
  当前两个父面板只通过 `SetCurrentPage` 把 handle 与区间文本对齐到权威页，不再由松手触发切页或切页表现。
  组件本身不接触配置、存档、资源加载、音效或动画。
- 初始化除首次页码外还接收所属面板当前页读取回调；按下与开始拖动时均先回读父面板权威页码，再同步
  `_currentPageIndex`、预览页、handle 和区间文本。因此主线当前页为第 `n` 页时，拖动基准为该页对应区间，
  不再无条件从第 0 页的 `1-10` 开始。
- 主线左右按钮继续进入 `TryTurnPage`，保留原有正向／反向书本动画、`FlipPage` 音效与 Item 淡入淡出；QuickTurn
  随首个 LevelItem 同步执行 `0.2s` 淡出，并在 Spine 完成后同步执行 `0.2s` 淡入。QuickTurn 拖动跨页仍直接更新
  `pageIndex`、分页按钮和 Item，不进入 `TryTurnPage`，所以不播放任何切页表现。
- Draw 左右按钮继续进入既有 `ChangePage` 并保留原 Item 淡入淡出；QuickTurn 拖动跨页直接更新 `_pageIndex`、按钮、
  Item 和保存页码，不进入 `ChangePage`。按钮切页时 QuickTurn 与 Draw Item 同步立即隐藏并用 `0.2s` 淡入；广告等待时
  仍关闭 QuickTurn，远端排序刷新和退出定位仍会同步 handle 与文本。
- 两个父面板分别拥有页码与表现：主线的 Spine 翻书动画没有下沉到共用组件，Draw 也没有依赖主线配置或存档。

## 2026-10-09 拖动失效事后补查（修改前）

### 用户实测事实

- 用户在 `ChoiceLevelPanel` 运行界面实测 `LevelChoicePanel_QuickTurnComp` 无法拖动；该结果推翻了此前仅凭脚本、引用和 Prefab 静态检查判断输入已可用的结论。

### 本地证据与直接结论

- `LevelChoicePanel_QuickTurn` 当前依赖 `IBeginDragHandler`、`IDragHandler`、`IEndDragHandler` 接收 EventSystem 拖动；子节点轨道和 handle 的 `Raycast Target` 已开启，父面板也已完成组件引用和 `Initialize` 调用。
- QuickTurn 源 Prefab 自带独立 `GraphicRaycaster`，其 `Blocking Objects` 当前为 `Two D`，`Blocking Mask` 为 `32767`；主线父 Canvas 的 `GraphicRaycaster.Blocking Objects` 为 `None`。QuickTurn 嵌套在 World Space Canvas 下时，这一额外 2D 物理遮挡条件会让射线在 UI 事件到达拖动脚本前被拒绝，是当前静态证据中与父面板输入策略不一致的阻断点。
- 本次采用最小修正：仅把 QuickTurn 源 Prefab 的 `GraphicRaycaster.Blocking Objects` 改为 `None`，不改变拖动算法、页码门禁、动画、存档或两个父面板的职责。

### 验证计划与证据边界

- 落盘后复核源 Prefab 的射线字段、三个 Prefab 的引用和 YAML 唯一性，并检查目标 diff。
- `uncertain`：当前自动化会话未暴露可操作的 Unity 窗口，无法在本轮自动执行 PlayMode 拖动；该 Prefab 修正必须由后续 Unity PlayMode 或设备触控实测确认，不能只凭静态修正宣称拖动已解决。

## 2026-10-09 handle 斜轨平移调整（修改前核对）

### 需求与行为基线

- 用户要求 handle 按 `Z = 1.5°` 的斜向轨迹平移，使其纵向位置始终与 `UI_xuanguan_img_ksfybg_01` 轨道处于同一条线上。
- Prefab 当前轨道、handle、`page_txt` 的局部旋转四元数均为 `{z: 0.013089596, w: 0.99991435}`，对应约 `1.5°`；轨道中心位置为 `{x: 0, y: -4.5}`。
- 当前 `SetHandlePosition(float handleX)` 只更新 handle 与文本的 `anchoredPosition.x`，保留原 `y` 不变；因此随着 X 变化，两者没有沿已经倾斜的轨道中心线移动。

### 拟修改位置与边界

- 在 `LevelChoicePanel_QuickTurn` 内把单一 X 范围改为轨道父坐标系中的二维起点、终点和方向；起止点仍由轨道实时尺寸、handle 宽度和 pivot 计算，再通过轨道 Transform 转换得到，因此移动方向自动跟随轨道当前 `1.5°` 旋转。
- 拖动输入投影到该线段，handle 与 `page_txt` 使用完整二维坐标更新；文本保持相对 handle 的原始二维偏移。
- 不修改 Prefab 美术旋转、分组公式、父面板页码、松手回调、主线翻书动画、Draw 淡入淡出、存档、资源或事件。

### 验证场景与未决项

- 静态核对首端、中间、末端位置都由同一轨道线段插值得到；检查分组映射、父 Prefab 引用、代码编译和目标 diff。
- `uncertain`：Unity PlayMode 与 Android/iOS 横屏触控下的实际贴轨观感、手指拖动手感仍需运行画面验证。

## 2026-10-09 拖动起点页码同步（修改前核对）

### 用户实测事实与原流程

- 用户实测点击 handle 开始操作时，Comp 每次从 `1-10` 开始，而不是从 `ChoiceLevelPanel` 当前 `pageIndex` 对应区间开始。
- 当前 `Initialize` 只接收初始化瞬间的 `currentPageIndex`；之后 Comp 依靠自己的 `_currentPageIndex`，只有父面板主动调用 `SetCurrentPage` 才能同步。
- 当前 `OnBeginDrag` 只刷新轨道范围并记录指针偏移，不会重新读取所属面板当前页。由源码可直接确认：Comp 与父面板一旦出现一次状态不同步，开始拖动没有权威状态校正点。

### 拟修改位置与边界

- `Initialize` 增加当前页读取回调；主线返回 `ChoiceLevelPanel.pageIndex`，Draw 返回 `DrawGame_ChoiceLevelPanel._pageIndex`。
- 每次 `OnBeginDrag` 在计算指针偏移前先读取父面板当前页，并通过既有 `SetCurrentPage` 同步 Comp 的当前页、预览页、handle 和区间文本。
- 初始化传入的页码继续用于首次显示；父面板切页后的既有主动 `SetCurrentPage` 保留。分组、斜轨移动、松手回调、动画、广告门禁和存档不变。

### 验证场景与未决项

- 静态覆盖主线与 Draw 的非第 0 页开始拖动、左右按钮切页后再拖动、关闭重开、父面板拒绝切页；编译并检查所有初始化调用方。
- `uncertain`：当前仍缺 Unity PlayMode 和 Android/iOS 触控实测，不能仅凭回调链静态检查宣称运行表现已完成验收。

## 2026-10-09 拖动实时无动画换页（修改前核对）

### 用户最终要求与当前行为

- 用户最终要求：拖动期间只要目标页发生变化，就立即更新当前页 Item，不播放 Spine、音效、渐隐渐显或其它切页动画；松手后的接口保留，但暂不承担切页表现。
- 该要求覆盖此前“松手后切页”和“快速切页播放渐隐渐显”的讨论；渐隐渐显方案尚未落入代码，不需要回滚实现。
- 当前共用组件的 `OnDrag` 只更新 `_previewPageIndex` 与 `page_txt`，`OnEndDrag` 才调用父面板。主线回调进入 `TryTurnPage`，会播放书本 Spine、`FlipPage` 音效和 Item 淡入淡出；Draw 回调进入 `ChangePage`，会执行 Item 淡出与 0.2 秒淡入。因此当前代码与最终要求不一致。

### 拟修改边界与验收场景

- 共用组件新增拖动页变化回调：仅在拖动跨过分页阈值时通知父面板；handle 和 `page_txt` 继续实时移动、更新。
- 主线收到拖动页变化后直接更新 `pageIndex`、按钮和 Item；Draw 直接更新 `_pageIndex`、按钮、Item 并保存页码。两个入口都不调用原动画切页方法。
- 松手回调继续保留，但只把 handle 与区间文本对齐到父面板最终确认页，不再修改父面板页码，也不播放任何表现。
- 左右翻页按钮继续走原 `TryTurnPage`／`ChangePage`，其既有动画和音效不在本次修改范围内；Draw 广告等待门禁继续阻止拖动。
- 静态验证覆盖拖动前进、回退、跨多页、回到起始页、父面板拒绝变化和松手状态对齐；Unity PlayMode、Android/iOS 横屏触控与连续快速拖动仍需运行验证，结论标记为 `uncertain`。

## 2026-10-09 按钮切页时 QuickTurn 同步淡入淡出（修改前核对）

### 需求与行为基线

- 用户要求主线播放书本 Spine 切页动画时，QuickTurn 与 LevelItem 同步渐隐渐显；DrawGame 的按钮切页也按其 LevelItem 现有节奏同步。该要求只针对左右按钮的离散切页，不覆盖已经确认的“拖动跨页立即刷新且无动画”。
- 主线 `TryTurnPage` 当前让所有旧 LevelItem 在 `0.2s` 内淡出；Spine 完成并刷新数据后，新 Item 以 `0.2s` 淡入，第 `i` 个 Item 延迟 `i * 0.05s`。QuickTurn 当前只禁用输入和更新页码，不改变透明度。
- Draw `ChangePage` 当前把旧 Item 立即置为透明，刷新数据后让所有新 Item 在 `0.2s` 内淡入；QuickTurn 当前只更新页码，不改变透明度。
- QuickTurn 源 Prefab 根节点当前没有 `CanvasGroup`，脚本也没有透明度接口，因此不能只在父面板添加调用完成需求。

### 拟修改位置、影响与验证范围

- 在 QuickTurn 源 Prefab 根节点新增 `CanvasGroup`，通过脚本 `[SerializeField]` 显式绑定；共用组件增加可取消旧 Tween 的透明度 API，使用与 LevelItem 相同的 unscaled DOTween 更新方式。
- 主线在现有 Item 淡出调用旁让 QuickTurn `0.2s` 淡出，在 Spine 完成后的首个 Item 节奏上让 QuickTurn `0.2s` 淡入；Item 自身的逐项延迟不变。
- Draw 在旧 Item 立即隐藏时同步隐藏 QuickTurn，在新 Item `0.2s` 淡入时同步淡入 QuickTurn；不调整 Draw LevelItem 原节奏。
- 拖动即时换页回调、松手对齐、页码、按钮、存档、广告门禁、Spine 动画和音效不变。面板失活时终止 QuickTurn 透明度 Tween 并恢复 Alpha 1，避免关闭途中留下不可见状态。
- 静态验证覆盖左右按钮前进／后退、主线 Spine 完成回调、Draw 同步刷新、动画途中关闭后重开、拖动换页不调用透明度 API、Prefab 显式引用和真实工程引用编译。PlayMode、Android/iOS 横屏触控和最终节奏观感仍标记为 `uncertain`。

### 修改结果与证据

- `LevelChoicePanel_QuickTurn` 新增 Inspector 显式绑定的 `quickTurnCanvasGroup` 和 `SetCanvasGroupAlpha(value, duration)`；每次调用先终止旧 Tween，渐变使用 `SetUpdate(true)`，组件失活时终止 Tween 并恢复 Alpha 1。
- 主线 `TryTurnPage` 在旧 Item `0.2s` 淡出旁同步淡出 QuickTurn，在 Spine 完成并刷新 Item 后让 QuickTurn 与第一个 Item 同步 `0.2s` 淡入；其余 Item 原有 `i * 0.05s` 延迟不变。
- Draw `ChangePage` 在旧 Item 立即隐藏和新 Item `0.2s` 淡入的位置分别同步调用 QuickTurn；`OnQuickTurnPageChanged` 与松手回调未调用透明度 API，拖动换页仍无动画。
- 源 Prefab 根节点新增一个 `CanvasGroup`，Alpha 初始为 1，脚本字段显式引用该组件；静态 YAML 检查为 29 个唯一 anchor、0 个重复 fileID，组件列表与字段引用均存在。
- Unity 6000.0.59f2 当前 Bee 响应文件定向 Roslyn 编译结果为 `exit code 0 / 0 error / 113 warning`；warning 为工程当前告警。`git diff --check` 对目标文件未发现空白错误，只输出仓库换行规范提示。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 横屏环境目视验证嵌套 Canvas 下的整体透明度、主线 Spine 时序、Draw 快速连点、切页途中关闭重开及触控输入恢复。
- 回滚本次增量时，移除源 Prefab 根节点 CanvasGroup 及脚本字段／透明度 API，并删除 `TryTurnPage`、`ChangePage` 中四处 QuickTurn Alpha 调用；拖动分页、页码与存档无需迁移。

## 2026-10-09 主线首次进入时 QuickTurn 淡入淡出（修改前核对）

### 需求与行为基线

- 用户要求 `ChoiceLevelPanel` 刚开始进入时，QuickTurnComp 配合面板现有进入动画做一次渐隐渐显；本次范围仅包含主线首次进入，不修改 DrawGame。
- 主线 `PlayOpenAnim` 当前先隐藏书本与盒子、把所有 LevelItem Alpha 设为 0，等待 `0.5s` 后显示书本与盒子，并让第一个 LevelItem 以 `0.2s` 淡入；后续 LevelItem 继续按 `i * 0.05s` 延迟淡入。QuickTurn 当前在这段等待和入场淡入期间一直保持 Alpha 1。
- 共用组件已经提供 `SetCanvasGroupAlpha(value, duration)`，且 Prefab 已显式绑定根节点 CanvasGroup，因此无需新增字段、运行时查找或 Prefab 改动。

### 拟修改位置、边界与验证范围

- 在 `PlayOpenAnim` 开始时立即把 QuickTurn Alpha 设为 0；现有 `0.5s` 等待结束后，让 QuickTurn 与第一个 LevelItem 同时以 `0.2s` 淡入，沿用现有 unscaled Tween 和旧 Tween 取消机制。
- 不修改 LevelItem 的 Punch／逐项淡入节奏，不修改左右按钮切页、拖动即时换页、松手接口、页码、存档、音效或 DrawGame。
- 静态核对首次打开调用链、关闭途中 Tween 清理与所有透明度调用点，并执行工程真实引用编译；Unity PlayMode、Android/iOS 横屏的最终视觉节奏仍标记为 `uncertain`。

### 修改结果与证据

- 主线 `PlayOpenAnim` 已在现有 `0.5s` 等待前立即把 QuickTurn Alpha 设为 0，并在等待结束、书本与盒子启用后用 `0.2s` 淡入；开始时间和时长与第一个 LevelItem 一致。
- 本次代码差异仅为 `ChoiceLevelPanel.cs` 中两处 `SetCanvasGroupAlpha` 调用；DrawGame、左右按钮切页、拖动即时换页、松手接口和 Prefab 均未修改。
- Unity 6000.0.59f2 当前 Bee `Assembly-CSharp.rsp` 真实引用定向 Roslyn 编译通过，结果为 `0 error / 113 warning`；warning 为工程当前告警。静态编译不能代替 PlayMode 目视验收。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 横屏环境确认首次进入的实际视觉节奏；此外 `PlayOpenAnim` 原有异步等待没有关闭取消令牌，若在前 `0.5s` 内关闭面板，等待结束后的原有书本／盒子激活逻辑仍可能继续执行，本次没有扩大范围处理该既有生命周期风险。
- 回滚本次增量时，只需移除 `PlayOpenAnim` 中等待前的 Alpha 0 调用和等待后的 `0.2s` Alpha 1 调用；无需修改 Prefab、存档或配置。

## 影响文件与系统

- `Assets/Game/UI/LevelChoicePanel/Sprites/Scripts/LevelChoicePanel_QuickTurn.cs`：共用拖动、分组、区间预览与回调。
- `Assets/Game/UI/LevelChoicePanel/Prefab/LevelChoicePanel_QuickTurnComp.prefab`：挂载共用脚本并显式绑定文本、轨道和 handle。
- `Assets/Game/UI/LevelChoicePanel/Scripts/ChoiceLevelPanel.cs` 与其 Resources Prefab：接入主线页码和原翻书流程。
- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelPanel.cs` 与 Draw Resources Prefab：接入 Draw
  页码、原淡入淡出、存档与广告门禁。
- 没有新增全局事件、Update 轮询、存档字段或资源加载；没有修改生成绑定脚本、关卡配置、打点、Packages、
  GuruSDK 或 GF。

## 验证证据、剩余风险与回滚

- Unity 6000.0.59f2 的 Bee `Assembly-CSharp.rsp` 真实引用定向 Roslyn 编译通过：`0 error / 113 warning`；warning
  为当前工程既有告警，编译证明脚本引用与语法成立，不等同于 PlayMode 行为验收。
- 分组公式静态边界验证通过：`0/10 -> 0-0`、`1/10 -> 1-1`、`10/10 -> 1-10`、
  `11/10 -> 1-10, 11-11`、`95/10 -> 10 组且末组 91-95`。
- Prefab YAML 静态检查：QuickTurn、主线、Draw 分别有 28、232、116 个唯一 anchor，无重复 fileID；共用脚本
  的三个生成字段以及两个父面板的 `quickTurnComp` 均有显式引用。目标脚本未新增 `GameObject.Find`、
  `Transform.Find`、`GetComponentsInChildren` 等运行时层级扫描。
- `git diff --check` 对目标脚本和 Prefab 通过，仅报告仓库换行规范提示。后续 Inspector 复核发现源 Prefab
  曾被运行中的 Unity 编辑器状态覆盖，脚本组件实际缺失，两个父 Prefab 因而显示 `Missing`；已在 Unity Prefab Mode
  将 `LevelChoicePanel_QuickTurn` 真实挂到组件根节点，手工绑定 `page_txt`、轨道 Image 和 handle Image，并分别重绑、
  保存主线与 Draw 父 Prefab。落盘后源组件 fileID 为 `2402698189282942233`，两个父 Prefab 均引用该源组件，Inspector
  不再显示 `Missing`。
- 用户实测拖动失效后，已把源 Prefab 独立 `GraphicRaycaster` 的 `Blocking Objects` 从 `Two D` 修正为 `None`，与主线
  父 Canvas 的射线策略一致；落盘复核为 `m_BlockingObjects: 0`，轨道、handle 和装饰 Image 的现有射线开关未改动，
  两个父 Prefab 仍指向源脚本组件 fileID `2402698189282942233`，目标文件 `git diff --check` 无错误输出。
- 斜轨平移改为由轨道局部中心线计算二维起止点，再把指针位置投影到该线段；当前 Prefab 四元数换算角度为
  `1.500000°`，按轨道宽 `1270`、handle 宽 `198` 计算，中心允许位置约从 `(-535.816, -18.531)` 到
  `(535.816, 9.531)`，两端与中间均由同一 `1.5°` 线段插值。`page_txt` 保留相对 handle 的原二维偏移并同步移动。
- Unity 6000.0.59f2 的当前 `Assembly-CSharp.rsp` 定向 Roslyn 编译为 `exit code 0 / 0 error / 113 warning`；本次目标脚本
  未产生新编译错误，warning 为工程现有告警。目标脚本与功能记录的 `git diff --check` 无错误输出。
- 页码同步调用链静态核对：主线 `InitQuickTurn` 传入读取 `pageIndex` 的 `GetQuickTurnCurrentPageIndex`，Draw
  `RefreshQuickTurn` 传入读取 `_pageIndex` 的同名局部方法；Assets 内 `Initialize` 生产调用方只有这两处，均已适配。
  Comp 在 `OnPointerDown` 与 `OnBeginDrag` 调用 `SyncCurrentPageFromOwner`，同步只调用 `SetCurrentPage`，不触发切页回调。
- 本轮重新执行 Unity 6000.0.59f2 `Assembly-CSharp.rsp` 定向 Roslyn 编译，结果为
  `exit code 0 / 0 error / 113 warning`；目标三个脚本没有编译告警或错误。
- 拖动实时换页调用链静态核对：共用组件只在 `_previewPageIndex` 变化时调用 `onPageChanged`；主线回调仅更新
  `pageIndex`、按钮和 Item，Draw 回调仅更新 `_pageIndex`、按钮、Item 和存档，两条路径均未调用 Spine、
  `AudioManager.PlaySound`、`SetCanvasGroupAlpha`、`TryTurnPage` 或 `ChangePage`。松手回调只调用 `SetCurrentPage`。
  修改后定向 Roslyn 编译为 `exit code 0 / 0 error / 113 warning`，所有生产初始化调用方均已适配新回调参数。
- `uncertain`：尚未执行 Unity PlayMode 目视拖动与动画验收，也未验证 Android/iOS 横屏触控、Canvas 缩放、
  刘海安全区、Draw 实际层级位置及快速连续拖动手感；左右按钮的原动画行为也未在本轮重新做运行时回归。
- 回滚可移除两个父面板的 `quickTurnComp` 字段、初始化和回调接入，恢复主线原左右按钮方法及 Draw 原
  `ChangePage` 签名，并从两个父 Prefab 移除嵌套实例；不需要迁移或清理任何存档、配置或玩家数据。
