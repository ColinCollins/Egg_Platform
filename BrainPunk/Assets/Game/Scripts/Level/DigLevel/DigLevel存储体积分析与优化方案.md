# DigLevel 存储体积分析与优化方案
## 当前实现与使用（2026-10-13）

已接入InitMap自动压缩：组件内Deflate占位位图替代Prefab逐格文本；编辑态清空Tilemap，运行时BindRuntime在Ready及揭幕前批量恢复并刷新Collider。空压缩字段保留旧格式入口；笔刷不改压缩初态。PNG输出独立新GUID，后续重烘焙保留该PNG GUID，保持RGBA32／分辨率／线性／可读／无mipmap／Clamp／Bilinear。每关使用自己的PNG。保存Dig Prefab后Editor回调延迟检查Assets落盘依赖，只清理已有PNG且无任何资产引用的旧Maps/_MapMask.asset；共享遮罩仍有引用时保留，播放态不清理。

工作流保持：编辑Prefab → 点击InitMap → 保存Prefab。生产旧关卡本轮没有批量转换，需各自重按InitMap并保存后才实际缩小；播放态只保存地图资产并同步当前运行图，不自动应用组件修改回Prefab。生成资源和清理不属于Undo磁盘事务，撤销后重按InitMap再保存。

84项Editor＋66项PlayMode隔离检查通过，Editor／Android条件编译0错误。首关隔离保存51968953→159822字节，PNG11895字节、压缩位图763字节；测试夹具缺失非Dig组件被移除，生产精确大小／正式Odin手动点击需本工程验证。桌面完整InitializeRuntime包含新恢复为约102～294ms；移动端／IL2CPP、整关UI／原生平台、APK包体、完整Undo及大工程保存清理耗时仍uncertain。iOS全量宏编译仍被已有Guru.UniKit.Apple两处缺失阻断，不改只读SDK。

代码影响仅Terrain两个手写partial及其中Editor清理类，不改关卡脚本、配置／存档／统计／HUD。证据：DevelopMD/验证记录/Dig自动压缩_2026-10-13/验证记录.md与清单、CSV、测试源、编译日志。已转换资源回滚须成对恢复Prefab／原生遮罩meta／预览材质及恢复代码，不能只删压缩代码留下空Tilemap。

下方保留第一轮分析与实现前记录；同GUID换容器是已撤回候选，当前实现以本节为准。

## 2026-10-13 InitMap 自动压缩：修改前核对

- 需求与验收：用户要求压缩自动纳入 InitMap，仍以“编辑关卡 → 点击 InitMap → 保存 Prefab”为工作流；保持格坐标、细分、洞轮廓、RGBA像素、笔刷耗墨及胜负／重开行为。原生产资源不批量重烘焙；旧格式继续可加载，再次 InitMap 才转换。
- 来源与维护边界：手写 DigTerrainCtrl.cs／DigTerrainCtrl.Editor.cs，无生成器；Editor.InitMap 负责格图、遮罩和预览材质，BindRuntime 接入已烘焙图。SceneSpriteBrushClear.ResetAll 读取显式 InitialMask；DigLevelCtrl.InitializeRuntime 在角色／病菌被冻结后调用 BindRuntime，之后才置 IsRuntimeReady；DigPlayCtrl 在 Loading 遮幕内加载。已读宪法、Editor态约束、模块说明、001风险及历史／当日记录。037 plan仍缺失，CodeGraph工具仍不可用，使用当前源码／YAML。
- 拟修改：在 Terrain 本身保存 Deflate 占位位图（1格1bit，范围仍为 bakedCells），避免新资源类型与额外引用；InitMap 烘焙后自动压缩并在非播放态清空 Tilemap 的冗长序列化数据。BindRuntime 仅对有压缩数据的地图批量恢复并立即刷新 Collider，不读取洞源、不重复烘焙；空数据保留旧地图路径。运行时挖掘不改压缩初态。
- 遮罩：InitMap 自动输出无损 PNG，线性／RGBA32／可读／无mipmap／不缩放／Clamp／Bilinear，保留分辨率；生成独立PNG并保持其重复烘焙GUID，更新显式预览与 InitialMask 引用，兼容播放克隆的 .asset／.png 路径。仅显式按钮生成本地输出，不在 OnValidate／Prefab预览初始化或触发DB。
- 原编辑器保存边界：烘焙资产立即写盘，Prefab仍需用户保存；播放实例的组件修改仍不会自动应用回Prefab。Undo原先登记组件／地图／纹理但写盘资产不是磁盘事务，需实测范围；明确不承诺PNG文件Undo，撤销后以当前配置重跑InitMap再保存，地图及遮罩成对提交／回滚。
- 风险与验证：先在TEMP核验GUID／2800000引用和原生导入逐像素一致，再核验完整旧烘焙与新输出的坐标／Collider／CPU-GPU遮罩、空图保存重开、重复烘焙／细分、坏配置拒绝、旧格式接入、播放态重烘焙、挖掘／新实例初态、最大图恢复耗时。移动端耗时及最终包体尚无证据，保留uncertain，不能用桌面结果替代。回滚必须将已转换资源和脚本成对恢复，不能只撤还原代码留下空Tilemap。
- 注释调整：原 BindRuntime“不填土”及 bakedCells“不在进关时重新填土”的描述会与压缩初态恢复冲突，定向改成“不按洞源重新烘焙”；其它原注释不改。
- 实现中补查：已有预览材质引用纹理时，MoveAsset跨原生／PNG容器的首次导入仍报旧资源加载错误，虽后续像素及引用核对通过，也不作为可交付路径。TEMP验证改为AssetDatabase释放旧容器并以同GUID重建PNG元数据；保留原meta的资源包标记与用户字段，验证共享遮罩及重开引用。写盘失败恢复事前原始.asset／meta；运行态RT应按当前visual实例读取，不持有导入前的旧RT对象作验证。

## 2026-10-13 需求与证据范围

用户最初称内存太大，随后明确为实际存储空间。本次检查工程Assets中的Prefab／地图源文件，不将源文件大小等同于运行内存、APK或AssetBundle压缩后大小。没有修改生产脚本、Prefab、地图或ProjectSettings；仅TEMP的数据格式试压和本地证据记录。

CodeGraph索引存在但CLI／MCP不可调用；读取当前DigTerrainCtrl、Editor.InitMap、SceneSpriteBrushClear及相关模块／首关性能记录，以YAML原始数据核实。项目EditorSettings.m_SerializationMode=2；InitMap以SetTilesBlock填满细分网格、逐格挖出生洞，再把Tilemap留在Prefab；遮罩用RGBA32 Texture2D原生.asset保存，文本含RGBA十六进制。

## 可观察事实

| 关卡 | Prefab MiB | 有效格数 | Tilemap文本 MiB | 文本占比 |
| --- | ---: | ---: | ---: | ---: |
| 001 | 49.556 | 182360 | 49.387 | 99.66% |
| 002 | 28.412 | 104509 | 28.271 | 99.50% |
| 003 | 27.446 | 100881 | 27.293 | 99.44% |
| 004 | 16.404 | 60195 | 16.272 | 99.19% |
| 005 | 16.844 | 61684 | 16.674 | 98.99% |
| 006 | 16.844 | 61684 | 16.674 | 98.99% |

001～006及999 Prefab共155.704 MiB；当前五张MapMask.asset共12.005 MiB（001为1024×512／4.001MiB，其它512×512／各2.001MiB）。统计快照不覆盖Library、Git历史或安装包。

目前六关每格使用同一Tile资源，m_TileIndex／matrix／color均0，matrix为单位矩阵，color为白色，没有逐格实例对象。m_Tiles的每格重复坐标及相同元数据约10行。来源：DigLevel_001.prefab:133开始的m_Tiles及对应其它关卡字段，DigTerrainCtrl.Editor.InitMap的SetTilesBlock和RGBA32保存，MapMask.asset头及_typelessdata。

## TEMP试压结果（没有生产迁移）

使用实际六关的完整已存格坐标，依据bakedCells映射为1格1bit，加范围头后压缩；解压并重建坐标集合，六关均与原坐标集合完全一致。所有重复元数据已单独核对为统一值；预览文件只编码存在性和范围，不冒充完整Unity可加载地图资产。

| 关卡 | 位图含范围 KiB | 压缩位图 KiB | 仅剥除m_Tiles后Prefab KiB |
| --- | ---: | ---: | ---: |
| 001 | 23.306 | 0.615 | 172.374 |
| 002 | 13.142 | 0.120 | 145.292 |
| 003 | 13.142 | 0.327 | 156.977 |
| 004 | 13.142 | 0.161 | 135.729 |
| 005 | 13.142 | 0.461 | 174.212 |
| 006 | 13.142 | 0.461 | 174.212 |

五份RGBA32初始遮罩分别从4.001／2.001MiB压成PNG 3.183／1.824／2.075／1.821／2.210KiB，PIL编码／解码后RGBA字节逐个一致。未降低像素尺寸。此预览没有经过Unity的PNG导入；纹理Y方向、线性色彩／gamma、可读性、Alpha、Bilinear和边缘采样仍需正式验证。

数据及源SHA256在DevelopMD/验证记录/Dig存储分析_2026-10-13/数据格式预览结果.json，TEMP原型位于C:/Users/admin/AppData/Local/Temp/brainpunk-dig-storage-preview-20261013。记录前再次核对源哈希未变。剥除m_Tiles后的数字只是结构估算，不是可直接使用／已迁移的Unity Prefab。

## 建议方案与需要保留的行为

1. 初始碰撞图移为独立紧凑地图资产，保存bakedCells、Tile引用和占位位图／压缩数据；Prefab保持Grid、Tilemap和Collider组件，移除几十万条初始Tile文本。保留现有细分、坐标、洞口、格中心删除和碰撞贴边参数，避免为省源文件体积改变玩法精度。
2. 初始视觉遮罩使用无损PNG存储；InitMap烘焙输出PNG及紧凑碰撞图，明确导入／采样设定，并同步预览材质与visual.InitialMask引用和Clone路径识别。需处理GUID／fileID及旧.asset兼容／转换，不可只改扩展名。
3. 运行时在揭幕和恢复Actor／Germ物理之前，一次批量恢复已保存碰撞图并ProcessTilemapChanges；Reset／Replay仍用新实例。不是在运行时按洞源重新烘焙，也不应每次Stamp解压整张图。

首关按当前数据结构可望由49.6MiB源Prefab降至约0.17MiB主体＋少量地图数据，遮罩另约3.2KiB；正式Unity保存结果、迁移元数据及运行性能尚未验证，不能承诺安装包同步减少同样百分比。

## 风险、验证计划与未决项

- 原DigTerrainCtrl.BindRuntime只接入已烘焙Tilemap，不填土；紧凑存储方案将增加加载期批量恢复步骤。需测桌面／移动端恢复182360格及Collider重建的帧耗和峰值，确认Loading遮罩、异步版本取消和首次开启物理边界。当前没有此实测，标uncertain；不能默认为零耗时。
- PNG需通过Unity导入及GPU／CPU二值对照校对方向与采样；只做RGBA往返不证明游戏中纹理完全一致。保持IsReadable、无mipmap、线性、无损格式；扩展名变化影响Editor.InitMap和预览引用。
- 编辑器预览不能自动进入运行时／DB链；必须为紧凑数据恢复设计局部预览及Undo／重烘焙边界。旧已验收关卡需迁移前后格集合、初始洞和碰撞形状对比，再覆盖进入／挖掘／吞噬／目标／失败／Reset／Replay／退出。
- 若真实目标是APK／下载AssetBundle存储，还须比较构建后的资源包，不能拿ForceText工程源文件大小替代；当前尚无包体实测。

## 第一轮分析交付与回滚（历史）

本次为存储定位及可重复的数据表示验证，未实施生产迁移；没有通过修改碰撞细分／纹理分辨率减少内容。报告可直接删除，TEMP原型可留作后续实现基线；生产资源无需回滚。后续实施须先补足上述加载／导入证据和边界，再逐关转换，保持旧格式兼容或可定向恢复。

## 实现中引用迁移方案纠正（替代上方同GUID换容器候选）

真实六关引用验证发现：.asset使用type=2，导入PNG使用type=3；仅保留GUID／2800000后共享005遮罩的006仍丢失引用，出现原生资源加载错误。撤回同GUID换容器方案。改为每关独立PNG（新GUID，后续重烘焙保持），原生遮罩在当前Prefab保存前保留；保存Dig Prefab后的局部Editor导入回调延迟核对Assets落盘依赖，只删除Maps内无任何资产引用的旧_MapMask.asset。005仍被006引用时不得删除，006自身转换／保存后再自动清理。该清理类与Editor partial同文件，仅维护本次格式迁移，不做运行时扫描或全局事件；播放态不清理。验证须同时覆盖共享引用保留、无引用删除、重开与运行场景。Undo文件边界仍按前述重烘焙处理。

## 2026-10-13 Bear批量刷新地图工具：修改前核对

- 用户明确要求Tools/Bear下新增DigGame入口，批量刷新所有DigLevel。原入口为逐Prefab点击InitMap后手动保存；新增工具顺序加载DigLevel_*.prefab内容，复用同一InitMap，成功后保存该Prefab，finally卸载。只新增Assets/Editor下手写工具，无生成器，不改既有Terrain算法或关卡参数，不自动收集／改变洞源。
- 依据：Assets/Editor/DrawGameTipsReferenceRepairTool.cs与PostcardStarLevelPrefabRefresher.cs的菜单／LoadPrefabContents／SaveAsPrefabAsset／卸载结构；DigTerrainCtrl.Editor.InitMap的Prefab路径、校验与压缩输出、DigMapAssetCleanup保存后资源清理；已有模块文档与Editor约束。CodeGraph CLI／MCP不可用，037 plan缺失，知识库批量刷新无专项条目，使用当前源码补足。
- 验收：Tools/Bear/DigGame/刷新所有 DigLevel 地图；播放或编译期间禁用；仅处理目录内DigLevel_命名Prefab，TBC／无Terrain跳过，打开的PrefabStage跳过以保护未保存编辑；单关异常继续并记录路径，取消保留已成功结果，进度框finally清理。编辑器API加载的是隔离本地Prefab内容，不调用运行时InitializeRuntime／DB／单例。
- 最小验证：先用两关紧凑Prefab及TBC在TEMP验证LoadPrefabContents路径可由现有InitMap识别、批量保存／重开、坏引用失败隔离、菜单门禁；不重复此前六关150项地图算法测试。不执行生产批量刷新；首次正式人工菜单及全工程耗时标uncertain。回滚删除工具／meta；若已运行，按本模块已有成对资源回滚约定。

## 2026-10-13 Bear批量刷新地图工具：实现与使用

- 菜单入口：`Tools/Bear/DigGame/刷新所有 DigLevel 地图`。工具只枚举 `Assets/Game/Prefabs/DigLevel` 根目录下名称以 `DigLevel_` 开头的 Prefab，按路径顺序加载、执行原 `InitMap` 烘焙并立即保存。
- `LoadPrefabContents` 的隔离根节点无法通过原无参 `InitMap()` 反查 Prefab 路径。为保持烘焙算法唯一，`DigTerrainCtrl.Editor` 新增 `InitMapFromPrefabAsset(string prefabPath)`，无参 Inspector 按钮和批处理分别解析／传入路径后共用同一主体；显式入口不负责保存 Prefab。
- 保护边界：播放、切换播放状态或编译期间菜单禁用；当前在 Prefab Stage 打开的关卡跳过；没有根节点 `DigTerrainCtrl` 的 TBC 等 Prefab 跳过；单关失败记录后继续；用户取消时保留取消前已经保存的关卡。工具不调用玩法初始化、DB、Save、RemoteConfig 或运行时单例。
- 隔离 Unity 6000.0.59f2 验证：首次实测确认原无参入口在 `LoadPrefabContents` 下路径为空并失败；改用显式路径入口后，临时工程内 001～006 共 6 个普通关卡全部刷新并保存，999 因无 `DigTerrainCtrl` 跳过，失败 0，Unity 批处理正常退出。生产关卡未由本次验证批量改写；主工程人工菜单、Android／iOS 真机运行和全工程执行耗时仍为 `uncertain`。
- 回滚代码时删除 `Assets/Editor/DigGameMapRefreshTool.cs` 及 meta，并移除 `InitMapFromPrefabAsset` 后让无参 `InitMap` 恢复直接进入原主体。若菜单已经改写关卡资源，必须按关卡成对恢复 Prefab、PNG／旧遮罩及预览材质，不能只删除工具。
