# Level136 翻页拼字密码功能说明

更新时间：2026-09-08（纸背配色设为 C9D2D6）

## 2026-09-08 纸背颜色

- 按用户指定，将翻回纸背的 `foldColor` 改为 `#C9D2D6`（RGB 201/210/214，Alpha=1）。同步修改 Level_136.prefab 的序列化配置与 Level136PaperAppearance 的代码默认值；Inspector 仍可配置。
- 本轮不改底页 underColor、横线、数字、Transform、材质、折页几何或密码流程。校验 RGB 换算与 Prefab 除 foldColor 外的内容摘要；三份几何/输入/绘制脚本保持原哈希。未操作 Unity，实际显示未重新验收。
- 回滚只需把上述两处 foldColor 恢复为 (0.795,0.753,0.753,1)，不覆盖其它场景修改。无资源、存档、事件或平台链路变更。

## 当前需求

- 使用共享美术目录的 Sp_l136_sz_01.png / Sp_l136_sz_02.png 替换占位图，并使用正式资源名和项目路径。
- 只清理场景中的四个问号 Box、下方四个占位 Tips，以及旧机关在暂停时显示的四个 `????`。用户本轮明确确认该暂停提示，不是翻页数字或暂停按钮。
- 角色、门、星星、地面、前中后景、关卡标题/署名及完整常规 HUD 恢复清理前状态；密码恢复走近触发、使用按钮输入、开门后进门通关。
- 正面的残缺 29 放在场景中，设计者自己摆放位置、旋转和大小，运行时不强制定位。
- 纸背颜色可配置，翻开后露出的底页也有浅灰笔记本横线。
- 初始纸页完全铺平，不显示右上小折角；保留右上拖动入口。
- 继续支持二维自由拖动、松手保持、通过正面 29 与纸背 986 拼出 2986。无固定轨道或答案吸附。

## 2026-09-08 纸背与揭开区域面积不一致修正（当前有效）

- 用户截图指出：露出的底页只有一小条，纸背却铺满大半页。原因是上一版 Flex=(2.3,0.865) 将纸背仿射变换的面积倍率改变，近水平拖动达到约 3.6 倍；此前只通过拼字与裁切检查，未验证面积，不能作为翻页观感验收依据。
- 本轮只改 Level136FoldGeometry：移除 Flex 放大，折线经过原角与自由角的中点，折叠方向归一化满足 `n·direction=1`，变换行列式为 -1，反折自逆。同一块原纸与翻回纸背保持 1:1 面积，屏幕裁切只可能让可见纸背更少，不会放大。纸尖仍跟随二维指针，未改输入速度、路径、抓取偏移、初始铺平或松手保持。
- 为适配不可移动的正面接缝，保留本关二维方向校准（FoldDirectionRatio=5.2）并重算纸背数字参考姿态。这是保面积的美术化仿射反折，不是等边长刚性反射或完整三维纸张模拟；斜拖时仍存在剪切形变，其手感需 Unity 目测验收。不能再用放大纸背来弥补配准。
- FrontDigits_29 的位置 (-3.11,1.12,0)、旋转 Z=12.996°、缩放 (0.7873016,0.7873016,1) 不变。整份 Prefab 前后 SHA256 均为 `CEA6C97D7A32653537DDCDC63F3CF787873AA07B02152A4AFBBF1B88094CF54C`，两张正式 PNG 哈希未变；PaperFold、FoldGraphic、Ctrl、场景/HUD/密码/门链无本轮代码改动。旧放大注释已因实现被替换而更新，未改无关注释。

验证证据（均在 `E:/WorkSpace/outputs/level136-fold/`）：

- `AreaChecks.cs` 同时运行修改前备份与生产新几何。`area-before.log` 复现近水平面积比 3.5998～3.6000 且不自逆；`AreaChecks-area-fix.log` 覆盖五种比例、32,805 个角位置，面积比均为 1:1，最大相对误差 0.00000557，纸尖跟手与反折自逆通过。
- `CurrentLayoutChecks-area-fix.log` 只读当前 Prefab，五种比例下完整纸背印刷包络零裁切、正面零遮挡，配准误差小于 0.001 像素。检查中的叉积改为除以纸边长度后比较实际像素距离，避免不同分辨率下把浮点噪声误判为遮挡。当前 2340×1080 对齐纸尖约为左起 28.98%、上起 26.59%；上一节 26.72%/24.28% 不再适用，均不是吸附目标。
- `GeometryChecks-area-fix.log` 的 6,724 个二维拖动回归通过；`compile-area-fix.log` 为完整 Roslyn 编译 exit 0 / 0 error / 109 warning，本关无诊断。
- 通过 computer-use 尝试 Unity 试玩：首次点击超时后重新观察再操作，已进入 PlayMode 主菜单；关闭启动弹窗期间用户按物理 Esc 停止电脑操作，之后未再输入。未进入 Level136 完成实际翻页验收，故 Unity 画面、触摸手感和移动端仍为 `uncertain`。停止时未继续操作 Play 按钮来退出试玩。

回滚：仅恢复 `area-fix-backup-20260908/Level136FoldGeometry.cs`；该备份用于追溯，恢复会重新引入纸背放大。不要覆盖其中的 Prefab 快照或回退用户正面数字摆放。未改配置、资源加载、存档、事件、打点和平台链路。

## 2026-09-08 新摆放配准：正面对象不可改动（放大方案已被后续修正）

用户把新数字放在场景偏左上方，要求检查是否能合上；合不上只修改翻页侧，不动正面对象。

- 本轮保护基线：FrontDigits_29 的位置 `(-3.11, 1.12, 0)`，旋转提示 Z=`12.996°`，缩放 `(0.7873016, 0.7873016, 1)`。已核对保存的 Prefab 与 Unity Inspector 一致。整份 Prefab 本轮未编辑，前后 SHA256 均为 `CEA6C97D7A32653537DDCDC63F3CF787873AA07B02152A4AFBBF1B88094CF54C`；图片、材质、相机、颜色、场景对象和密码流程不改。
- 原问题已复现：在 2340×1080 下，旧纸尖为 `(1105.94,488.23)`，接缝起点在线段参数 `-0.2530`，位于纸尖左侧的延长线上，完整纸背印刷区有 3 个边角被裁掉。此前只验证共线，不能证明整幅密码可拼；旧验证中的旧摆放常量也不能用于此次新位置。
- 仅将 Level136FoldGeometry 的纸背横向展开参数 `1.31 → 2.3`，纵向 `0.865` 不变。继续从场景正面接缝反算纸背配准；正面 Transform 只读，986 与纸背共用变换，不独立漂移。TryReferenceCorner 增加两端位于真实自由纸边线段内的检查，排除延长线假成功；PaperFold 的失败提示改为校准翻页侧，不再建议移动设计者的 29。
- 不改变初始铺平、二维自由拖动、抓取偏移、松手保持或密码 2986；不自动吸附答案。当前 2340×1080 的对齐纸尖约为页面左起 `26.72%`、上起 `24.28%`，该数据仅供验收，不约束玩家路径。1920×1080 对应约 `18.94% / 22.04%`。

验证：`E:/WorkSpace/outputs/level136-fold/CurrentLayoutChecks.cs` 只读解析当前 Prefab 的 Transform，直接编译生产几何；相机基线 5.9、593×317 / 100 PPU 已核对。覆盖 1920×1080、2340×1080、2520×1080、1920×1440、527×329：完整纸背印刷包络无裁切、正面包络无遮挡、接缝误差小于 0.0001 像素、参考印刷误差小于 0.001 像素。修改前后日志为 `current-layout-before.log` / `current-layout-after.log`。`GeometryChecks.cs` 移除旧摆放硬编码，单独验证 6,724 个二维纸角及延长线拒绝；PNG 8 行接缝/224 点颜色测试继续通过。完整 Roslyn 编译 `exit 0 / 0 error / 109 warning`，本关无诊断，日志 `compile-new-position.log`。

验证边界：通过 computer-use 只读观察 Unity 时，Game 正在试玩 Level122，未切换或打断该关，因此此次 Level136 的实际拖动、渲染接缝和真机手感仍为 `uncertain`，不把数学覆盖检查当作 Unity 实机截图。没有调整共享类、HUD、角色、门、存档、资源加载、事件、打点或平台流程。

回滚：仅从 `E:/WorkSpace/outputs/level136-fold/new-position-backup-20260908/` 恢复本轮两份折页脚本；其中 Prefab 仅为保护基线快照，不应用它覆盖设计者之后的摆放。回滚脚本会恢复此次新位置拼不完整的问题，不能同时回退正面数字位置来掩盖。

## 2026-09-08 纠正误清理（当前有效范围）

此前将“问号、暂停时的显示”扩大解释成删除角色、门、景物及常规按钮，是范围误判；该精简方案及直接成功流程已撤销，不能作为用户需求或今后实现基线。

- 按之前实际补丁逐项逆向恢复，未整份覆盖 Prefab：恢复 Midscene / Frontscene / Backscene、分隔节点、两处 FallArea、两颗 Postcard_Star、Actor、GameBg、Tips 标题、Tips (2) 署名、door_0、StartDoor、Ground_long 的原状态。原先子节点上的独立设置保持不变。
- 只保留 Box_1～4（连同其子物件）及 Placeholder_Tips_1～4 的停用状态。旧暂停四问号对应 `Tips (1)`，来自 Level128Ctrl.passwordClue；本关不挂 Level128Ctrl / Level96Ctrl，也不订阅其暂停显示逻辑。该节点继续停用，并将本地 m_text 覆盖清空，不会显示 `????`。
- 根 BaseLevelCtrl 的 gamePlayPanelName 恢复 `GamePlayPanel_001`，successAnim / enterDoorAnim 恢复原场景引用；PasswordBoard 的 Collider、OnTrigger2DHandle 和 SwitchGamePanelUseBtnBoard 重新启用。
- `Level136Ctrl.cs` 撤销密码器透明点击区、强制常驻入口、跳过入口门/角色气泡以及 SuccessStrightly 接线，恢复原本的密码区域显隐和门事件链；不修改共享 BaseLevelCtrl、HUD、Actor、密码弹窗或关卡打点生命周期契约。
- 误建的 `Resources/GamePlayPanel_136.prefab` 及 meta 已删除，删除前已确认无其它运行资源引用；备份位于 `E:/WorkSpace/outputs/level136-fold/restore-scope-backup-20260908/`，同时留存纠正前本关 Prefab 和控制器，仅供审计/恢复，不作为有效玩法。
- 正式 Sp_l136_sz_01 / sz_02、GUID、三份翻页/几何脚本、29 的场景 Transform/Renderer、纸色/横线、密码 2986 均保留，不恢复旧占位美术。

验证：完整 Roslyn 编译 `exit 0 / 0 error / 109 warning`，本关无诊断（`compile-restore-scope.log`）；Prefab 250 个唯一定义、531 个纯本地引用、无缺失；场景节点状态、HUD 资源名、三项密码触发组件、门引用和旧暂停控制器缺席已静态核对。正式素材只读检查与生产几何 6,724 点回归仍通过。Unity Game 视图、暂停恢复、走近密码器及密码开门仍需试玩，标记 `uncertain`，不把编译当作运行验收。

回滚边界：保留纠正前备份用于追溯，但未经用户新指示，不得再次恢复已经否定的全场景清空/仅密码器模式。若只需找回问号机关，可单独恢复 Box / Placeholder_Tips 及旧提示资源；不修改新素材或其它关卡。

## 2026-09-08 正式素材替换

来源：`//192.168.1.5/DW-ArtSource/BrainPunk_001/原画/9月/l136/`，只读取共享目录，没有改写原稿。游戏使用导入项目的本地副本，不在运行时访问该网络路径。

| 用途 | 项目资源路径 | GUID（沿用） |
| --- | --- | --- |
| 场景正面 29 | Assets/Game/ArtSrc/Sprites/l136/Sp_l136_sz_01.png | e4ed560720964f52a246f8a67789fdac |
| 翻回纸背 986 | Assets/Game/ArtSrc/Sprites/l136/Sp_l136_sz_02.png | a55d943e809542a394374aafc37cb0a1 |

- 原 `Sp_l136_page_moving_29.png` / `Sp_l136_page_fixed_986.png` 已从 Assets 移出；旧图、原 meta、三份配准脚本与场景快照保存在 `E:/WorkSpace/outputs/level136-fold/asset-backup-20260908/`，可恢复。项目内仅保留正式命名的两张图及对应 meta。
- 沿用原 GUID、Single Sprite、中心 pivot、100 PPU、透明通道和原平台导入设置。Level_136 的正面 SpriteRenderer 与 foldDigits986 自动引用新路径下的图片，整个 Prefab 与替换前 SHA256 相同；29 的位置、旋转、缩放、颜色，纸色/横线和密码器均未改动。
- 旧图为 493×263、563×279；正式图为 593×317、678×375，且剪切线方向不同，不能只换文件继续用旧像素常量。`Level136FoldGeometry` 集中记录新斜切线 `(202.5,0) → (593,307)` 与第二张图相对第一张图的左上偏移 `(231,-59)`，由 PaperFold / FoldGraphic 共用。参考几何测试用的 InkPoint 同步使用正式图宽度 909；修正旧配准注释是因为其已与新图片事实不符。
- 保留 100 PPU 和场景 Transform，因此新画布像素数对应的 Sprite 外框比旧图大；未暗中缩放、裁图、抠色或移动设计位置，后续仍可在场景自行调整 29。

验证：两张本地 PNG 与用户指定源文件 SHA256 分别一致（`31EB03609AAAB1F5160981E4AA49A1F500775345E5FA3B2A82193805265AAAED`、`44CB24F78C9BC9152C11FF527F38091FB197B04E9458F16526452154F89DD314`）；两个 GUID 在 Assets 中均唯一。只读图像检查确认 8 个有笔触的斜切行相邻像素连续，224 个抗锯齿重叠点 RGB 一致；脚本为 `E:/WorkSpace/outputs/level136-fold/CheckFinalArt.py`。完整 Roslyn 编译 `0 error / 109 warning`，本关无诊断，日志 `compile-final-art.log`；生产几何 6,724 点回归及四种比例下当前场景摆放配准通过。未运行 Unity Game 视图或移动端，实际渲染/抗锯齿接缝和资源导入缓存的最终表现仍为 `uncertain`。

回滚：从上述备份恢复旧 PNG 与原名 meta，并恢复三份配准脚本；必须整组回滚，不能仅换回图片却保留新接缝参数。无需改场景摆放、HUD、密码、共享类、配置、存档或打点。

## 历史误清理记录（范围扩大，已撤销）

以下为当时错误实现的事实记录，不代表当前需求；以本页“当前有效范围”为准。

本轮基线仍包含从 Level128 复制的四个 Box、金币、星星、角色、入口门/出口门、草木、地面、说明文字和通用 HUD。密码入口依赖角色走近 PasswordBoard，答对仅开门；直接隐藏角色和按钮会造成入口或通关不可达。

- `Level_136.prefab` 停用 24 个无关根节点：四个 Box（含下挂金币和反馈）、三个前中后景组、分隔节点、两处 FallArea、四个占位文字、两颗场景星、Actor、GameBg、三处 Tips、两扇门和 Ground_long。保留 CommonBg 纸张背景、StaticCamera、FrontDigits_29 和 PasswordBoard。停用而非删除源资源，便于恢复；Actor 仅保留一个非激活的框架引用，不显示、不模拟物理或接受输入。
- PasswordBoard 的碰撞触发器、进入/离开回调和旧入口开关组件停用，保留原图和 Transform。点击区域通过相机投影跟随其 Renderer Bounds，额外留 32 UI 单位的触摸余量；不改变 29 或密码器的位置。
- 新增本关私有 `Resources/GamePlayPanel_136.prefab`：通用 HUD 的本地变体，停用关卡数字块及商店、重置、暂停、提示、左右移动、跳跃共七个按钮。唯一启用的 Use_btn 是覆盖密码器的透明点击区，不再显示箭头和按钮底图；其已有点击功能仍打开 `PasswordPopup_136_2986`。转场遮罩与启动点击保护保留，正常显示状态不增加图案。
- `Level136Ctrl.cs` 初始化时关闭本关入口门演出和角色提示；配置密码器点击区并保持入口可用。正确密码事件 1 由 `_passwordSolved` 做一次性防重，隐藏入口和折页后调用已有 `BaseLevelCtrl.SuccessStrightly()`。Prefab 的 successAnim/enterDoorAnim 清空，不再等待角色走进门。
- 已前置核对 `DevelopMD/关卡打点生命周期约定.md` 第 2.3 节：成功继续经过 BaseLevelCtrl → SUCCESS 状态，不新增或直发打点，不调整成功次数、存档、revive、Reset/Replay 顺序或全局事件。共享 BaseLevelCtrl、GamePlayPanel、Actor、密码弹窗、门和已验收关卡均未修改。

本轮验证：完整运行时 Roslyn 编译 `exit 0 / 0 error / 109 warning`，本关无诊断；本关 Prefab 250 个唯一定义、530 个纯本地引用，私有 HUD 16 个唯一定义、31 个纯本地引用，均无缺失或重复。检查所有根节点显隐、HUD 八个停用覆盖和新 GUID 唯一性；重新运行既有生产几何 6,724 点测试通过。日志为 `E:/WorkSpace/outputs/level136-fold/compile-cleanup.log`。

本轮未操作 Unity Game 视图：密码器点击区实际对齐、空场景视觉、首次进入/Replay、正确密码成功结算及移动端仍为 `uncertain`。共享编辑器 GM/蓝字调试叠层不属于本关资源，本轮未修改全局调试开关。

回滚本轮：恢复上述本地节点 active/触发组件状态、BaseLevelCtrl 的原门动画引用和 GamePlayPanel_001 名称，撤回 Level136Ctrl 的密码器投影与直接成功接线，并移除本关新 HUD 及 meta；保留上一轮 29 摆放、纸色、横线、自由翻页和原始素材。

## 怎么配置

打开 `Assets/Game/Prefabs/Level/Level_136.prefab`。

### 摆放正面 29

选中根节点下的 `FrontDigits_29`，直接编辑 Transform 的 Position、Rotation Z、Scale。它是正常的场景 SpriteRenderer，非运行时生成 UI，可在编辑模式直接看到。初始位置沿用上一版配准附近，仅作为摆放起点。

- 根节点 Level136Ctrl 的 `frontDigits29` 已绑定该 SpriteRenderer。
- 正面图片为 `Assets/Game/ArtSrc/Sprites/l136/Sp_l136_sz_01.png`，背面为同目录的 `Sp_l136_sz_02.png`。
- Renderer 使用 URP 自带的 Sprite-Unlit-Default，让正面墨迹不因场景灯光而与 UI 纸背墨迹出现额外色差。未修改包内材质。
- 数字颜色可以改此 SpriteRenderer 的 Color，背面墨迹会继承同一 tint。
- 运行时只通过 TransformPoint 读取位置，不写入、重设或隐藏这个 Renderer，也不会另外绘制一份正面 29。

背面图会根据场景 29 的位置、旋转和缩放重新计算印刷配准与可拼合纸角。可拼性仍受纸页边界限制：如果摆到极端位置/角度导致无可用折角，运行时会提示 `FrontDigits_29 当前位置/角度无法在页面内拼合`，不会擅自移回固定点。通常将它放在画面右半部并保留斜向上读字角度即可；具体外观由设计者试玩确认。

### 纸张颜色与横线

根节点 Level136Ctrl → Paper Appearance：

| 字段 | 用途 |
| --- | --- |
| Fold Color / foldColor | 翻回来的灰色纸背 |
| Under Color / underColor | 翻开后露出的底纸 |
| Line Color / lineColor | 底页横线颜色，Alpha 控制深浅 |
| Line Spacing / lineSpacing | 横线间距，默认 74 |
| Line Width / lineWidth | 横线粗细，默认 2 |
| Line Offset / lineOffset | 首条线离页顶的偏移，默认 34，用来对齐场景横线 |

线条数值以 1080 高画面为基准，随页面高度等比换算。横线裁切在揭开的底纸范围内，再被折回纸背遮挡，不会覆盖关卡正面。Inspector 在 PlayMode 修改参数后由运行时主循环重绘；修改运行实例不会自动保存到原 Prefab。

### 初始与操作

初始自由角为右上顶点 `(1,0)`，折面、底纸、阴影和背面墨迹均不绘制，仅保留不可见的右上局部命中层。玩家抓住右上区域后可横、竖、斜拖；松手停留，回到右上极小范围后完全铺平。单指所有权、暂停/失焦清理与局部输入透传延续上一版。

## 变更基线与文件职责

本轮之前：29 虽在正面，但由 UI 网格按数学常量定位；纸背与底纸颜色为常量；底纸无横线；初始有小折角。以上四项按用户本轮截图要求变更，不恢复更早已否定的截图卷页或固定 progress 轨道。

- `Level136Ctrl.cs`：场景 SpriteRenderer 引用、可序列化外观、运行时折页及 2986 密码资源配置；入口显隐由原场景触发器负责，正确密码沿用门链。OnValidate 仅在运行时设置刷新标志，不访问 DB/单例或创建对象。
- `Level136PaperFold.cs`：二维输入；只读场景 29 投影和颜色；重新计算背面配准。画布尺寸/相机投影或摆放变化时刷新，无持续新建对象。
- `Level136FoldGeometry.cs`：铺平状态、自由角折线、场景斜切边反算、共享纸背仿射变换。
- `Level136FoldGraphic.cs`：底纸与横线、纸背、背面墨迹、独立命中层，共四层；正面数字不再由它绘制。
- `Level_136.prefab`：新增 FrontDigits_29 三个 YAML 对象定义（GameObject、Transform、SpriteRenderer），绑定本关控制器并保存外观默认值。
- 本轮不改两张原始 PNG、密码资源、纸张几何或正面 29 摆放；场景/HUD 清理见上节。共享脚本及共享资源未改。

## 素材与数据边界

正式素材的配准偏移为 `(231,-59)` 像素，斜切边与导入说明见本轮替换记录。场景 Renderer 的完整局部坐标经相机投影到页面，再用于纸背印刷；纸背网格与墨迹共用一个变换并按实际纸片多边形裁切。

密码资源为私有 `Resources/PasswordPopup_136_2986.prefab` 的 2986，正确后沿用事件 1 开门，再由角色进门触发标准成功流程。不新增存档、配置表、全局事件、分析打点、平台流程；不修改 GF、共享材质、已验收关卡或远端加载。

## 上一轮外观验证（本轮清理验证见上节）

- Unity 6000.0.59f2 当前完整运行时响应文件 Roslyn 编译：exit 0，0 error，本关无诊断。
- 生产 Geometry 源码＋Unity CoreModule 独立数学测试：四种画面比例共 6,724 组二维纸角通过。追加完全铺平、由场景斜切边反算参考姿态、四方向移动后仍沿同一边配准检查，均通过。
- Prefab：250 个定义、0 重复、531 个纯本地引用、0 缺失；场景 Renderer 与原始 29 Sprite 引用已核对。
- 静态检查：只有 SceneInkPoint 读取 `_front29.transform.TransformPoint`，没有写正面对象 Transform；不再创建 FrontInk_29 UI 层；横线裁切使用本关原有折线裁切逻辑。
- 测试入口：`outputs/level136-fold/GeometryChecks.cs`；编译日志：`outputs/level136-fold/compile-scene-appearance.log`。这些产物位于仓库根 outputs 下，不进入游戏资源。

## 未验证与风险

- 本轮未操作 Unity Game 视图，场景对象编辑器显示、相机到 UI 的实际投影、Inspector 改色即时反馈、横线与背景的像素对齐仍为 `uncertain`，需要 Unity 试玩。
- 用户提供的新截图是改动前的实机证据；先前六姿态离线图不作为本轮新外观的实机验收。
- 极端旋转、非等比缩放或移动到页面外可能无法完成拼字，当前只警告，不自动改设计者的摆放。
- 暂停/多指、Reset/Replay、密码开门、退出重进、Android/iOS 安全区及帧率/GC 未重新实测。
- 默认颜色及 74/2/34 横线参数是参照截图的初始配置，最终对齐可由设计者调节。
- 仓库引用的 feature plan 文件仍缺失；本轮不涉及其远端加载职责。

## 上一轮外观回滚

仅回退本轮四份 Level136 脚本及 Level_136 的 FrontDigits_29/引用/外观字段改动。保留 2986 密码入口，不修改共享关卡、HUD、Actor、GF、存档或配置。已否定的截图卷页和固定 progress 轨道不作为默认发布回退版本。
