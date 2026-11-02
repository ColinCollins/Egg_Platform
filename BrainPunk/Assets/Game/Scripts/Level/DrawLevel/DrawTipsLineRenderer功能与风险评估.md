# DrawTipsLineRenderer 功能与风险评估

## 需求与背景

- 画线玩法需要针对不同折线路径显示大小、间距一致的圆点提示线。
- 原方案直接把 `ui_common_pic_dian_01.png` 以 Tile 方式贴到连续 `LineRenderer` 带状网格上。直线区域比例正确，但锐角两侧的网格会形成斜切、梯形和内侧挤压，圆点随 UV 一起拉长或折弯。
- 本地纹理证据为 23×10，实际非透明圆点区域为 X=6..15、Y=0..9，即 10×10；因此运行时可以裁出该正方形 UV，为每个圆点生成独立正方形 Quad。

## 影响文件、资源与系统

- 运行时组件：`Assets/Game/Scripts/Level/DrawLevel/DrawTipsLineRendererNormalizer.cs`。
- 首个使用资源：`Assets/Game/Prefabs/DrawLevel/DLevel_001.prefab` 的 `Line` 节点。
- 固定材质：`Assets/Game/ArtSrc/Materials/DrawGame_TipsLine.mat`。
- 路径编辑来源：同节点现有 `LineRenderer.Positions`。
- 运行时显示：同节点预置的 `MeshFilter` 与 `MeshRenderer`。
- 不涉及 DB、存档、配置、事件、分析打点、动态资源加载或平台 SDK。

## 原行为基线

- `LineRenderer` 负责路径和最终显示，使用固定宽度、Tile 模式与连续 UV。
- 所有点的 Z 已统一为 0，退化重复点已移除，直线上的圆点大小和间距基本一致。
- 锐角处多个纹理周期仍映射在同一条连续带状 Mesh 上，因此圆点会随折角网格发生形变。

## 修改后行为

- `LineRenderer` 仅作为编辑器路径数据和近似预览，不再承担 PlayMode 最终显示。
- 运行时读取任意数量的 Positions，将路径转换到当前节点本地空间并统一 Z。
- 采样距离跨线段连续累计，不在折点处重置；当前 `DLevel_001` 路径总长约 10.798、默认间距 0.23，静态预期生成 47 个圆点。
- 每个圆点使用独立的正方形 Quad，裁取纹理中的 10×10 圆点区域；折点只改变圆点中心位置，不改变 Quad 和 UV 形状。
- 所有圆点合并为一个运行时 Mesh，每个圆点 4 顶点、2 三角形；当前静态路径预期为 188 顶点、94 三角形。
- `MeshRenderer` 复用固定材质，并复制原 LineRenderer 的 Sorting Layer、Sorting Order 与 Rendering Layer Mask。
- 不增加 `Update` 或每帧重建。静态路径只在 Awake 生成一次；运行时路径通过 `SetPath(...)` 更新，修改原 Positions 后通过 `RebuildDots()` 显式重建。
- `maxDotCount` 默认 4096，避免异常路径或过小间距生成无界网格。

## 路径与调用边界

- 编辑器制作不同路径：直接修改各自 `LineRenderer.Positions`；每个提示路径使用一个组件和一个运行时 Mesh。
- `DrawTipsLineRendererNormalizer` Inspector 通过 Odin Button 直接显示“按提示线规范重新应用”和“按当前路径重新生成圆点”；两个入口使用同一套重建逻辑，并会在编辑器内立即生成临时 Mesh 预览。
- 需要继续调整 `LineRenderer.Positions` 时，点击 Odin 按钮“恢复 LineRenderer 路径编辑”，临时 Mesh 会被释放并重新显示 LineRenderer；该按钮在 PlayMode 中置灰。
- 运行时动态路径：调用 `SetPath(IReadOnlyList<Vector3>, bool positionsAreWorldSpace)`；组件会复制输入，不持有调用方集合引用。
- 临时动态路径结束后调用 `UseSerializedLinePath()`，恢复使用 prefab 中的静态 Positions。
- 多段不连续路径应使用多个组件，避免跨越不相连区域继续采样和连接。

## 编辑器与运行时边界

- `Reset` / `OnValidate` 只规范序列化参数、路径 Z、本地材质引用和 Renderer 设置，不创建 Mesh、GameObject、单例或运行时服务。
- 编辑器 Mesh 仅由上述 Odin Button 显式创建，使用 `HideFlags.HideAndDontSave`，只用于当前编辑器会话的效果预览，不会生成或污染 `.asset`；重载后需要重新点击按钮。
- 编辑器预览生成后，`MeshFilter.sharedMesh` 会显示临时的 `Line_DrawTipsDots`，LineRenderer 被禁用、MeshRenderer 被启用。
- 运行时 Mesh 在 `Application.isPlaying` 后自动创建，使用 `HideFlags.DontSave`，组件销毁时释放。
- 没有 `ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Instance、Instantiate 或全局事件。

## 验证记录

- 使用 Unity 6000.0.59f2 自带 Roslyn 分别按 Editor 与 Player 引用对组件执行定向编译，两次均为 0 error；仅有 `positionZ` 和固定材质依赖 Unity 序列化赋值的 `CS0649` 警告。
- Odin Button 调整后再次按 Editor 与 Player 引用联合编译两个提示线组件，均为 0 error；静态搜索确认组件内不再保留 `ContextMenu`。
- 静态检查确认 `DLevel_001/Line` 已预置 `MeshFilter`、默认禁用的 `MeshRenderer`、固定材质、原排序层和新增 Mesh 生成参数。
- 静态计算确认当前路径总长约 10.798，默认间距下预期为 47 个圆点、188 顶点和 94 三角形，远低于默认 4096 圆点上限。
- 静态检查确认编辑器临时 Mesh 只由 Odin Button 显式触发，`Reset` 与 `OnValidate` 不进入 Mesh 创建路径，并提供恢复 LineRenderer 编辑的释放入口。
- 完整工程编译仍被当前工作区既有 `DrawGame_ChoiceLevelPanel.Generated.cs` 语法错误阻断，尚未进入 PlayMode 验证实际 Shader、朝向、像素清晰度和最终截图。

## 风险与未验证项

- 本地 meta 证据确认 `DrawGame_TipsLine.mat` 使用 URP `Unlit.shader`，可用于普通 MeshRenderer；但最终颜色、Alpha Clip 边缘、三角形朝向和像素清晰度仍缺少 PlayMode 画面证据。
- 编辑器临时 Mesh 不持久化；域重载、重新导入或重新打开 prefab 后 `MeshFilter` 可能恢复为空，这是预期行为，需要再次点击生成按钮。
- 当前圆点 Quad 固定在本地 XY 平面，适用于现有 2D 横屏相机；若未来路径位于任意 3D 平面，需要增加朝向基准，不能直接复用当前假设。
- 运行时频繁调用 `SetPath` 会重新提交 Mesh 数据并产生与路径长度相关的 CPU 开销；当前设计面向离散更新，不应每帧调用。
- 尚未进行 Unity PlayMode、Android/iOS 横屏真机、不同分辨率、动态路径、极短路径和达到圆点上限的验证。

## 回滚路径

- 删除 `DLevel_001/Line` 上的 `MeshFilter` 与 `MeshRenderer`，把组件恢复为仅规范 LineRenderer 的上一版本，即可回到连续 Tile 渲染。
- 回滚不涉及数据迁移、存档、配置、事件或资源地址；`DrawGame_TipsLine.mat` 与路径 Positions 可以原样保留。
