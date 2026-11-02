---
name: brainpunk-build-iwanner-mesh-background
description: Build or revise a BrainPunk IWanner level background from a visual reference by decomposing it into small layered Unity Mesh assets, mapping the composition to the level camera and playable opening, isolating colors per level, and validating the actual prefab render. Use for flat geometric backgrounds such as mountains, factory silhouettes, pipes, tanks, screens, or conveyors. Do not use for painterly, organic, or texture-heavy art where layered sprites are more maintainable.
---

# IWanner 分层 Mesh 背景制作

## 决策边界

- 参考图主要由纯色、简单渐变、直线、圆、台阶或规则设备轮廓组成时，采用分层 Mesh。
- 颗粒、笔触、云雾、人物、复杂建筑细节或高频纹理决定画面质量时，改用分层 Sprite；不要为了避免图片而堆叠大量顶点。
- 背景只是装饰。不得改变 Ground、Collider、Actor、Door、Camera、机关、事件或关卡成功链，除非用户明确把这些内容纳入范围。
- 优先复用同一主题已经验收的 Mesh、材质和 Shader。关卡差异通过层的 Transform、启用状态、排序与少量新增通用形状表达。
- 同一主题跨多个 Group 使用时，把 Mesh、材质和 Shader 集中放在 `ILevel_Theme_Backgrounds/<Theme>`；配色资产按 `ILevel_GroupXXToYY_<Theme>_ColorSettings` 命名，避免资源归属仍伪装成单关。
- 把旧单关主题提升为共享主题时，先移动或重命名资源并重绑 Prefab，再检查旧 GUID／路径依赖；确认引用为零后才删除旧目录和旧配色，保留迁移记录与回滚边界。

## 制作流程

1. 读取仓库规则、项目宪法、IWanner 功能／风险记录及目标关卡附近文档，记录任务前 `git status --short`。
2. 在 Unity 中读取目标 prefab 的 Camera 正交尺寸和位置、可见 Ground 顶面／墙内边界。不要只按参考图画布尺寸布景。
3. 把参考图拆成语义层，例如天空、远山、近山，或墙面、管线、罐体、显示器、传送带。语义层作为父节点；只把用户明确需要独立调整的位置、颜色或启用边界拆为独立 Mesh。可识别的完整建筑、台地或设备结构默认保持整体，不得仅因“以后可能复用”继续细拆；只有编辑边界允许且当前确有重复成本时，才使用原点基准或单位尺寸 Mesh 加子节点 Transform 复用。
4. 使用 `scripts/map_reference.py` 计算参考像素到世界坐标的映射。圆形半径只使用纵向比例，保持圆形；显示器等刚性物体统一等比缩放。
5. 按 [references/geometry-and-validation.md](references/geometry-and-validation.md) 创建 Mesh。网格顶点使用对象局部坐标，对象 Transform 保存关卡构图；顶点色只保留层内明暗权重，不作为日常配色入口。
6. 每个主题按天空、远景、主体、近景等语义层创建独立 Material，同一语义层的重复图形共用一个 Material。Material 保存暗部／亮部颜色并使用主题共用 Shader；不要为每关复制 Shader。
7. 背景挂在目标 prefab 的独立根节点，放在玩法前景后方。每个子层名称表达职责，并保持可在 Inspector 单独移动、缩放或停用。
8. 每关使用独立 `IWannerLevelColorSettings`，或确认共享资产确实只服务这组关卡后才共享。不得直接修改被其它主题关卡引用的旧配色。
9. 在 Unity AssetDatabase 中按依赖顺序定向同步导入 Mesh／材质／配色／Prefab。PrefabImporter 成功日志不能替代引用非空检查。
10. 使用隔离 Prefab 预览场景和目标 prefab 内的 Camera 渲染 16:9；目标支持超宽屏时再渲染 20:9。显式设置 `camera.scene` 和独立 `SceneCullingMask`，避免按相机名字误选已加载场景中的旧实例。
11. 对比修改前 YAML：玩法块保持一致；检查本地 fileID、Mesh／材质／配色引用、Shader 诊断和目标主题的关卡隔离。没有 PlayMode、移动端或资源包证据时标记 `uncertain`。
12. 使用 `brainpunk-local-doc-update` 写中文功能／风险记录，并更新 `DevelopMD/已有功能设计与使用记录/YYYY-MM-DD.md` 当天记录；同日使用同一个文件，原历史文件保持只读。
13. 批量保存多个 Prefab 后，在交付前立即重跑一次全量引用检查。若用户正打开其中某个 Prefab，Prefab Stage 可能把旧内存内容再次保存；此时只在当前内容上重加背景与配色，不能用批处理前备份覆盖玩法改动。

## 配色调整

- 日常改色直接编辑 `ILevel_Theme_Backgrounds/<Theme>/Materials` 下对应语义层 Material 的暗部／亮部颜色；不要修改 Mesh 顶点色来换主题色。
- 同主题关卡共享这些 Material。只想改一关时，复制该关实际使用的语义层 Material 并只改绑该 Prefab；修改共享 Material 前先确认引用范围。
- Mesh 顶点色是 Shader 的明暗采样依据。只有构图本身的明暗分布错误时才允许修改，并应同时核对所有复用该 Mesh 的关卡。

## 质量要求

- 背景主体的比例以实际可见开口为准，不能把整张参考图等比缩放后任由地形遮挡。
- 太阳、圆罐、滚轮等圆形必须保持 X/Y 半径一致。
- 参考图中的大物体应拆成可调整层；重复小件可以合并到一个 Mesh，避免一个图元一个 Renderer。
- 同一 Mesh 中出现左右两套设备、不同框架或其它可独立移动的物件时，优先拆成语义父层下的子 Mesh。记录拆分后的 Renderer 数，并通过实际渲染确认父层 Transform 仍正确作用于全部子物件。
- 弧形与直条不按几何类型自动拆分，必须先确认用户指出的完整结构边界。用户以“一个立柱＋拱形结构”或框选拱门单元要求单独拆出时，应把该单元的两侧立柱和上方拱顶保留在同一个 Mesh 中，只把穿过单元的连续水平长条作为独立 Mesh；不得把“拱形”误解为只剩弧顶。
- 只有用户接受结构细分，并且拓扑、顶点明暗权重、材质职责和厚度规则一致时才共用 Mesh。重复完整单元应先从当前合并 Mesh 提取一个单元，并对其它实例做归一化几何与顶点属性校验；左右比例不同则分别保留单元 Mesh，不要强行拉伸成一个资源。
- 静态纯色背景不生成大尺寸整图。若需要质感，优先叠加小型平铺纹理或少量 Sprite 细节层。
- 不宣称 Mesh 一定比 Sprite 性能好。记录 Renderer 数、顶点数和资源体积；Draw Call、GPU、内存只有实测后才能下结论。
- 编辑器临时预览不得进入运行时初始化、DB、存档或配置链；预览结束必须卸载临时场景和释放 RenderTexture。

## 交付证据

- 目标关卡 16:9 预览图；必要时补 20:9。
- 背景层清单、Renderer 数、总顶点数、资源体积与关卡独占／共享边界。
- 修改前后 prefab 块对比：玩法、碰撞、嵌套 prefab 和事件是否保持一致。
- Unity 中 Mesh、各语义层 Material、Shader、配色引用均非空，Material 的材质配色开关已启用，Shader 无编译错误。
- PlayMode、Reset、切关、Android/iOS 与 YooAsset 的已验证项和 `uncertain` 项。
