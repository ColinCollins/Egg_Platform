# 几何与验证约定

## 坐标映射

先确定参考图中背景有效区域和目标关卡可见开口：

```text
worldX = worldLeft + (pixelX - pixelLeft) / pixelWidth * worldWidth
worldY = worldTop - (pixelY - pixelTop) / pixelHeight * worldHeight
```

- `worldLeft/worldWidth` 通常取左右墙内边界。
- `worldTop/worldHeight` 通常取相机上沿到主要地面顶面；封闭关卡取顶板下沿到地面顶面。
- 圆的世界半径使用 `pixelRadius / pixelHeight * worldHeight`，X/Y 使用同一个值。
- 参考图与关卡结构不一致时保留玩法结构，只映射背景构图；在记录中说明压缩、裁切或重排。

## 网格规则

- 简单矩形使用 4 顶点／2 三角形。
- 圆形通常使用 32～64 段；最终移动端尺寸很小时减少段数。
- 台阶山体可以把每段轮廓向下铺四边形，避免对凹多边形直接扇形三角化。
- 折线管道以线段四边形和拐点圆连接；长直线不要细分。
- 同一排序和同一移动职责的墙缝、滚轮、管线可以合并为一个 Mesh。
- 顶点先用世界构图坐标生成，随后减去 bounds center 写入 Mesh；center 写入子对象 Transform，使 Inspector 调整直观。
- MeshRenderer 使用稳定的负 sortingOrder；玩法 SpriteRenderer 保持原排序。

## Unity 验证

1. `AssetDatabase.LoadAssetAtPath` 检查 Mesh、Material、Shader 和配色非空。
2. 检查目标 prefab 下背景 Renderer 数和所有 MeshFilter.sharedMesh。
3. `ShaderUtil.GetShaderMessages` 必须没有错误。
4. 用 `PrefabUtility.LoadPrefabContents` 加载隔离副本；不要保存这个预览副本。
5. 为临时场景设置独立非零 culling mask，并同时设置 `camera.overrideSceneCullingMask` 与 `camera.scene`。
6. 渲染后在 `finally` 恢复 `RenderTexture.active`，销毁 Texture2D／RenderTexture，并 `UnloadPrefabContents`。
7. YAML 验证至少包括：fileID 无重复／缺失、原 Collider 和 PrefabInstance 块未变、配色只被预期关卡引用。

## 已知失败模式

- 只看到 PrefabImporter 成功，但新增依赖没有导入：表现为 Sprite／Mesh 为空或 GUID 查询为空。
- 按相机名称截图选中其它已加载关卡的旧相机：画面与最新 prefab 不一致。
- 预览临时场景的 SceneCullingMask 为 0：Camera.Render 只得到清屏色。
- 把参考图全高映射到相机全高：大物体被地面／顶板遮住，看起来比例过大。
- 修改共享配色 asset：所有引用关卡一起变色。

