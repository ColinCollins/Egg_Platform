#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Dig
{
    public sealed partial class DigTerrainCtrl
    {
        #region 编辑器挖洞来源收集
        /// <summary>
        /// 显式点击时收集本地形层级的BaseDigComp及派生组件，含inactive，排除嵌套Terrain的来源。
        /// 统一填Spawn Holes并清空Reserved Holes避免重复；支持Undo，随后保存Prefab并按需执行InitMap。
        /// </summary>
        [Button("收集子节点挖洞对象", ButtonSizes.Large)]
        private void CollectHoleSources()
        {
            Undo.RecordObject(this, "Dig 收集挖洞对象");
            spawnHoles = Array.FindAll(GetComponentsInChildren<BaseDigComp>(true),
                source => source.GetComponentInParent<DigTerrainCtrl>(true) == this);
            reservedHoles = Array.Empty<BaseDigComp>();
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (!Application.isPlaying && gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        #endregion

        #region Prefab 地图烘焙
        /// <summary>仅Editor可用，支持Prefab及播放实例；保存本地地图资产，播放中同步地形缓存，不初始化关卡。</summary>
        [Button("InitMap", ButtonSizes.Large)]
        public void InitMap()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            string prefabPath = stage != null && stage.IsPartOfPrefabContents(gameObject)
                ? stage.assetPath : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
            if (string.IsNullOrEmpty(prefabPath) && Application.IsPlaying(gameObject))
            {
                // 运行克隆可能没有Prefab连接，以显式绑定的初始地图资产确定所属关卡，不按对象名字猜路径。
                string maskPath = AssetDatabase.GetAssetPath(visual.InitialMask);
                if (maskPath.EndsWith("_MapMask.asset", StringComparison.Ordinal) || maskPath.EndsWith("_MapMask.png", StringComparison.Ordinal))
                    prefabPath = Path.GetDirectoryName(Path.GetDirectoryName(maskPath)).Replace('\\', '/') + "/" +
                        Path.GetFileNameWithoutExtension(maskPath).Replace("_MapMask", ".prefab");
            }
            InitMapFromPrefabAsset(prefabPath);
        }

        /// <summary>
        /// 仅供Editor批处理工具调用，以明确的DigLevel Prefab路径执行同一套地图烘焙流程。
        /// 调用方必须传入当前加载内容所属的资源路径；本方法不负责保存Prefab。
        /// </summary>
        public void InitMapFromPrefabAsset(string prefabPath)
        {
            ValidateBindings();
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.StartsWith("Assets/Game/Prefabs/DigLevel/", StringComparison.Ordinal))
                throw new InvalidOperationException("请在 DigLevel 关卡 Prefab／实例上执行 InitMap；运行克隆必须绑定初始地图资产。");
            ValidateHoleSources();
            Func<Vector3, bool> containsSand = fitSpriteOutline ? BuildSpriteCollisionFilter() : world => ContainsPoint(world);

            var visualSettings = new SerializedObject(visual);
            int width = Mathf.Max(1, visualSettings.FindProperty("maskWidth").intValue);
            int height = Mathf.Max(1, visualSettings.FindProperty("maskHeight").intValue);
            Shader shader = visualSettings.FindProperty("displayShader").objectReferenceValue as Shader;
            if (!shader) throw new InvalidOperationException("Sand 显示 Shader 未绑定。");
            // 显式编辑器生成入口重新导入显示 Shader，更新 Prefab 预览使用的编译资源。
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(shader), ImportAssetOptions.ForceUpdate);
            if (!shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Sand 显示 Shader 编译失败或当前图形设备不支持，请查看 Console 的 Shader 错误。");
            string directory = Path.GetDirectoryName(prefabPath).Replace('\\', '/') + "/Maps";
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'), "Maps");
            string prefix = directory + "/" + Path.GetFileNameWithoutExtension(prefabPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(prefix + "_MapPreview.mat");
            // 仅创建并保存本关的编辑器资产；不实例化玩法对象或初始化运行时服务。
            if (!material)
            {
                material = new Material(shader) { name = "Dig 地图编辑预览" };
                AssetDatabase.CreateAsset(material, prefix + "_MapPreview.mat");
            }
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[]
                { this, collisionGrid.transform, collisionMap, tileCollider, visual, sandSprite, material }, "Dig InitMap");
            mapBaked = false;

            int subdivisions = Mathf.Clamp(collisionSubdivisions, 1, 8);
            Vector3 scale = collisionGrid.transform.localScale;
            if (bakedSubdivisions > 0)
                scale = new Vector3(scale.x * bakedSubdivisions, scale.y * bakedSubdivisions, scale.z);
            // 保持基础格尺寸，以网格变换同步缩放格位置和真实碰撞几何；重复烘焙不累乘缩放。
            collisionGrid.transform.localScale = new Vector3(scale.x / subdivisions, scale.y / subdivisions, scale.z);
            bakedCells = WorldBoundsToCells(sandSprite.bounds);
            bakedSubdivisions = subdivisions;
            float worldScale = Mathf.Min(Mathf.Abs(collisionMap.transform.lossyScale.x), Mathf.Abs(collisionMap.transform.lossyScale.y));
            tileCollider.extrusionFactor = Mathf.Clamp(colliderExtrusion, 0f, 0.01f) / Mathf.Max(worldScale, 0.0001f);
            collisionMap.ClearAllTiles();
            var tiles = new TileBase[bakedCells.size.x * bakedCells.size.y * bakedCells.size.z];
            for (int y = bakedCells.yMin; y < bakedCells.yMax; y++)
            for (int x = bakedCells.xMin; x < bakedCells.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (containsSand(collisionMap.GetCellCenterWorld(cell)))
                    tiles[(y - bakedCells.yMin) * bakedCells.size.x + x - bakedCells.xMin] = sandTile;
            }
            collisionMap.SetTilesBlock(bakedCells, tiles);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 255);
            foreach (BaseDigComp hole in spawnHoles)
            {
                if (hole.HoleShape)
                {
                    RasterReservedHole(hole, pixels, width, height);
                    continue;
                }
                Vector2 center = hole.transform.position;
                float radius = hole.Radius;
                float collisionRadius = Mathf.Max(0f, radius + Mathf.Clamp(collisionRadiusOffset, -0.1f, 0.1f));
                float extent = Mathf.Max(radius, collisionRadius);
                RasterHole(pixels, width, height, new Bounds(center, Vector3.one * extent * 2f),
                    point => (point - center).sqrMagnitude <= radius * radius,
                    point => (point - center).sqrMagnitude <= collisionRadius * collisionRadius);
                ApplySourceColliderState(hole);
            }
            foreach (BaseDigComp hole in reservedHoles) RasterReservedHole(hole, pixels, width, height);
            Texture2D mask = SaveBakedMaskPng(prefix, pixels, width, height);
            CompressBakedCollisionCells();
            // 编辑态仅保存压缩初态；隐藏Tilemap不参与沙土预览，避免Prefab重新序列化几十万格。
            if (!Application.IsPlaying(gameObject)) collisionMap.ClearAllTiles();
            tileCollider.ProcessTilemapChanges();
            material.shader = shader;
            material.SetTexture("_MaskTex", mask);
            Rect rect = sandSprite.sprite.textureRect;
            Texture2D texture = sandSprite.sprite.texture;
            Vector4 rectUV = new Vector4(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
            material.SetVector("_SpriteRectUV", rectUV);
            material.SetFloat("_UseSpriteRectUV", rectUV == new Vector4(0, 0, 1, 1) ? 0f : 1f);
            Game.Scripts.Effects.SceneSpriteBrushClear.ApplySpriteGeometryToMaterial(material, sandSprite);
            if (!Application.IsPlaying(gameObject)) sandSprite.sharedMaterial = material;
            visualSettings.FindProperty("initialMask").objectReferenceValue = mask;
            visualSettings.FindProperty("displayMaterial").objectReferenceValue = material;
            visualSettings.ApplyModifiedProperties();
            mapBaked = true;
            foreach (UnityEngine.Object changed in new UnityEngine.Object[]
                { this, collisionGrid.transform, collisionMap, tileCollider, visual, sandSprite, material }) EditorUtility.SetDirty(changed);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.ImportAsset(prefix + "_MapPreview.mat", ImportAssetOptions.ForceUpdate);
            if (Application.IsPlaying(gameObject)) BindRuntime();
            else sandSprite.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(prefix + "_MapPreview.mat");
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
            SceneView.RepaintAll();
        }

        /// <summary>仅显式烘焙读取图片 Alpha；临时读回不可读纹理，保留导入设置，返回后不持有 GPU 资源。</summary>
        private Func<Vector3, bool> BuildSpriteCollisionFilter()
        {
            if (sandSprite.drawMode == SpriteDrawMode.Tiled)
                throw new InvalidOperationException("轮廓碰撞烘焙仅支持 Simple／Sliced，请关闭 Fit Sprite Outline 或调整 Draw Mode。");
            Sprite sprite = sandSprite.sprite;
            Rect rect = sprite.textureRect;
            Texture2D source = sprite.texture;
            Color32[] pixels;
            if (source.isReadable) pixels = source.GetPixels32();
            else
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Texture2D readable = null;
                try
                {
                    Graphics.Blit(source, temporary);
                    RenderTexture.active = temporary;
                    readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                    pixels = readable.GetPixels32();
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(temporary);
                    if (readable) UnityEngine.Object.DestroyImmediate(readable);
                }
            }

            Bounds bounds = sandSprite.localBounds;
            Vector4 border = sprite.border / sprite.pixelsPerUnit;
            Vector4 borderUV = new Vector4(sprite.border.x / sprite.rect.width, sprite.border.y / sprite.rect.height,
                sprite.border.z / sprite.rect.width, sprite.border.w / sprite.rect.height);
            bool sliced = sandSprite.drawMode == SpriteDrawMode.Sliced;
            int width = source.width, height = source.height;
            float threshold = Mathf.Clamp(collisionAlphaThreshold, 0.01f, 1f) * 255f;
            return world =>
            {
                Vector3 local = sandSprite.transform.InverseTransformPoint(world);
                float u = (local.x - bounds.min.x) / bounds.size.x;
                float v = (local.y - bounds.min.y) / bounds.size.y;
                if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
                if (sliced)
                {
                    u = SliceCollisionAxis(u, bounds.size.x, border.x, border.z, borderUV.x, borderUV.z);
                    v = SliceCollisionAxis(v, bounds.size.y, border.y, border.w, borderUV.y, borderUV.w);
                }
                else
                {
                    if (sandSprite.flipX) u = 1f - u;
                    if (sandSprite.flipY) v = 1f - v;
                }
                float x = Mathf.Clamp(rect.x + u * rect.width - 0.5f, rect.xMin, rect.xMax - 1f);
                float y = Mathf.Clamp(rect.y + v * rect.height - 0.5f, rect.yMin, rect.yMax - 1f);
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                int x1 = Mathf.Min(x0 + 1, width - 1), y1 = Mathf.Min(y0 + 1, height - 1);
                float bottom = Mathf.Lerp(pixels[y0 * width + x0].a, pixels[y0 * width + x1].a, x - x0);
                float top = Mathf.Lerp(pixels[y1 * width + x0].a, pixels[y1 * width + x1].a, x - x0);
                return Mathf.Lerp(bottom, top, y - y0) >= threshold;
            };
        }

        /// <summary>与 Dig/Sand 的 SliceAxis 保持一致；仅映射图片采样，不改变线性挖洞遮罩坐标。</summary>
        private static float SliceCollisionAxis(float uv, float size, float left, float right, float sourceLeft, float sourceRight)
        {
            float factor = Mathf.Min(1f, size / Mathf.Max(left + right, 0.000001f));
            left *= factor;
            right *= factor;
            float position = uv * size;
            if (position < left) return position / Mathf.Max(left, 0.000001f) * sourceLeft;
            if (position > size - right) return 1f - sourceRight + (position - size + right) / Mathf.Max(right, 0.000001f) * sourceRight;
            return sourceLeft + (position - left) / Mathf.Max(size - left - right, 0.000001f) * (1f - sourceLeft - sourceRight);
        }

        /// <summary>仅显式InitMap调用；把已完成洞口计算的格图存入本组件，不改变精度或读取其它资源。</summary>
        private void CompressBakedCollisionCells()
        {
            TileBase[] tiles = collisionMap.GetTilesBlock(bakedCells);
            var bitmap = new byte[(tiles.Length + 7) / 8];
            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i]) bitmap[i >> 3] |= (byte)(1 << (i & 7));
            using (var output = new MemoryStream())
            {
                using (var compressor = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                    compressor.Write(bitmap, 0, bitmap.Length);
                compressedCollisionCells = output.ToArray();
            }
        }

        /// <summary>显式烘焙输出独立PNG并保留其GUID，导入为可读线性RGBA32；旧.asset由Prefab保存后的引用检查清理，文件输出不属于Undo事务。</summary>
        private static Texture2D SaveBakedMaskPng(string prefix, Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            byte[] png;
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                png = texture.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }

            string path = prefix + "_MapMask.png";
            string absolutePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), path);
            File.WriteAllBytes(absolutePath, png);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.format = TextureImporterFormat.RGBA32;
            settings.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
            importer.SetPlatformTextureSettings(settings);
            importer.ClearPlatformTextureSettings("Android");
            importer.ClearPlatformTextureSettings("iPhone");
            importer.ClearPlatformTextureSettings("Standalone");
            importer.SaveAndReimport();
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!mask || mask.width != width || mask.height != height || mask.format != TextureFormat.RGBA32)
                throw new InvalidOperationException("Dig地图PNG导入失败或尺寸／格式被改变，请检查纹理导入设置。");
            return mask;
        }

        /// <summary>先验证显式配置，再修改地图；inactive 标记不被过滤，烘焙不改变节点显示状态。</summary>
        private void ValidateHoleSources()
        {
            if (spawnHoles == null || reservedHoles == null) throw new InvalidOperationException("出生洞/预留洞数组未配置。");
            for (int i = 0; i < spawnHoles.Length; i++)
            {
                BaseDigComp hole = spawnHoles[i];
                if (!hole) throw new InvalidOperationException($"Spawn Holes[{i}] 引用缺失，请显式绑定 BaseDigComp。");
                if (hole.HoleShape) ValidateShapeSource(hole);
                else if (hole.Radius <= 0f || float.IsNaN(hole.Radius) || float.IsInfinity(hole.Radius))
                    throw new InvalidOperationException($"Spawn Holes[{i}] {hole.name}：未绑定 Hole Shape 时 Radius 必须为有限正数。");
            }
            foreach (BaseDigComp hole in reservedHoles)
            {
                if (!hole || !hole.HoleShape || hole.HoleShape.gameObject != hole.gameObject ||
                    !hole.transform.IsChildOf(sandSprite.transform) || hole is DigGermCtrl || hole is DigActorCtrl)
                    throw new InvalidOperationException("预留洞必须是 Sand 子节点上的 BaseDigComp，显式绑定同节点 Collider2D；不能绑定玩法对象。");
                ValidateShapeSource(hole);
            }
        }

        /// <summary>同一节点上的 Collider 定义实际轮廓；Radius=0 的形状源同样有效。</summary>
        private static void ValidateShapeSource(BaseDigComp hole)
        {
            if (hole.HoleShape.gameObject != hole.gameObject)
                throw new InvalidOperationException($"{hole.name} 的 Hole Shape 必须绑定同节点 Collider2D。");
            if (hole.OutwardDistance < 0f || float.IsNaN(hole.OutwardDistance) || float.IsInfinity(hole.OutwardDistance))
                throw new InvalidOperationException($"{hole.name} 的 Outward Distance 必须为有限非负数。");
            BuildShapeDistance(hole.HoleShape, out _);
        }

        /// <summary>仅显式烘焙时切换已绑定的 HoleShape；保持节点 active，未绑定形状的圆洞不改玩法碰撞体。</summary>
        private static void ApplySourceColliderState(BaseDigComp hole)
        {
            Collider2D shape = hole.HoleShape;
            if (!shape) return;
            Undo.RegisterCompleteObjectUndo(shape, "Dig 挖洞对象碰撞");
            shape.enabled = hole.IsShow;
            if (PrefabUtility.IsPartOfPrefabInstance(shape)) PrefabUtility.RecordPrefabInstancePropertyModifications(shape);
            EditorUtility.SetDirty(shape);
        }

        /// <summary>读取 Collider 的序列化轮廓，不依赖编辑器物理状态；inactive 节点同样可重烘焙。</summary>
        private void RasterReservedHole(BaseDigComp hole, Color32[] pixels, int width, int height)
        {
            Func<Vector2, float> signedDistance = BuildShapeDistance(hole.HoleShape, out Bounds bounds);
            float distance = hole.OutwardDistance;
            float collisionDistance = distance + Mathf.Clamp(collisionRadiusOffset, -0.1f, 0.1f);
            bounds.Expand(Mathf.Max(0f, Mathf.Max(distance, collisionDistance)) * 2f);
            RasterHole(pixels, width, height, bounds,
                point => signedDistance(point) <= distance,
                point => signedDistance(point) <= collisionDistance);
            ApplySourceColliderState(hole);
        }

        #region Collider 序列化轮廓
        /// <summary>从显式 Collider 配置构建世界距离函数；不查询运行时物理代理，也不访问其它组件。</summary>
        private static Func<Vector2, float> BuildShapeDistance(Collider2D shape, out Bounds bounds)
        {
            Transform target = shape.transform;
            Vector2 offset = shape.offset;
            float scale = Mathf.Max(Mathf.Abs(target.lossyScale.x), Mathf.Abs(target.lossyScale.y));
            if (shape is CircleCollider2D circle)
            {
                Vector2 center = target.TransformPoint(offset);
                float radius = circle.radius * scale;
                bounds = new Bounds(center, Vector3.one * radius * 2f);
                return point => Vector2.Distance(point, center) - radius;
            }
            if (shape is CapsuleCollider2D capsule)
            {
                bool vertical = capsule.direction == CapsuleDirection2D.Vertical;
                Vector2 center = target.TransformPoint(offset);
                Vector2 longAxis = target.TransformVector(vertical ? Vector2.up * capsule.size.y : Vector2.right * capsule.size.x);
                float radius = target.TransformVector(vertical ? Vector2.right * capsule.size.x : Vector2.up * capsule.size.y).magnitude * 0.5f;
                Vector2 delta = longAxis.normalized * Mathf.Max(0f, longAxis.magnitude * 0.5f - radius);
                Vector2 from = center - delta, to = center + delta;
                bounds = new Bounds(from, Vector3.zero); bounds.Encapsulate(to); bounds.Expand(radius * 2f);
                return point => SegmentDistance(point, from, to) - radius;
            }
            Vector2[][] paths;
            bool closed;
            float edgeRadius;
            if (shape is BoxCollider2D box)
            {
                Vector2 half = box.size * 0.5f;
                paths = new[] { new[] { new Vector2(-half.x, -half.y), new Vector2(half.x, -half.y),
                    new Vector2(half.x, half.y), new Vector2(-half.x, half.y) } };
                closed = true; edgeRadius = box.edgeRadius * scale;
            }
            else if (shape is PolygonCollider2D polygon)
            {
                paths = new Vector2[polygon.pathCount][];
                for (int i = 0; i < paths.Length; i++) paths[i] = polygon.GetPath(i);
                closed = true; edgeRadius = 0f;
            }
            else if (shape is EdgeCollider2D edge)
            {
                paths = new[] { edge.points }; closed = false; edgeRadius = edge.edgeRadius * scale;
            }
            else throw new InvalidOperationException("预留洞支持 Circle/Box/Capsule/Polygon/Edge Collider2D；其它类型请使用这些轮廓定义。");
            bounds = new Bounds(target.TransformPoint(offset), Vector3.zero);
            foreach (Vector2[] path in paths)
            for (int i = 0; i < path.Length; i++)
            {
                path[i] = target.TransformPoint(path[i] + offset);
                bounds.Encapsulate(path[i]);
            }
            bounds.Expand(edgeRadius * 2f);
            return point =>
            {
                float nearest = float.PositiveInfinity;
                bool inside = false;
                foreach (Vector2[] path in paths)
                {
                    bool inPath = false;
                    int count = closed ? path.Length : path.Length - 1;
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 from = path[i], to = path[(i + 1) % path.Length];
                        nearest = Mathf.Min(nearest, SegmentDistance(point, from, to));
                        if (closed && (from.y > point.y) != (to.y > point.y) &&
                            point.x < (to.x - from.x) * (point.y - from.y) / (to.y - from.y) + from.x) inPath = !inPath;
                    }
                    inside |= inPath;
                }
                return (inside ? -nearest : nearest) - edgeRadius;
            };
        }

        /// <summary>世界坐标中的线段距离，用于轮廓外扩；零长度边按端点处理。</summary>
        private static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float t = delta.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - from, delta) / delta.sqrMagnitude) : 0f;
            return Vector2.Distance(point, from + delta * t);
        }
        #endregion

        /// <summary>按同一世界形状烘焙视觉像素和碰撞格；只改已有 Tilemap 与本次本地像素数组。</summary>
        private void RasterHole(Color32[] pixels, int width, int height, Bounds worldBounds,
            Func<Vector2, bool> visualContains, Func<Vector2, bool> collisionContains)
        {
            Bounds spriteBounds = sandSprite.localBounds;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector3 local = new Vector3(spriteBounds.min.x + (x + 0.5f) / width * spriteBounds.size.x,
                    spriteBounds.min.y + (y + 0.5f) / height * spriteBounds.size.y, 0f);
                Vector2 world = sandSprite.transform.TransformPoint(local);
                if (world.x >= worldBounds.min.x && world.x <= worldBounds.max.x &&
                    world.y >= worldBounds.min.y && world.y <= worldBounds.max.y && visualContains(world))
                    pixels[y * width + x] = new Color32(255, 255, 255, 255);
            }
            BoundsInt range = WorldBoundsToCells(worldBounds);
            for (int y = Mathf.Max(range.yMin, bakedCells.yMin); y < Mathf.Min(range.yMax, bakedCells.yMax); y++)
            for (int x = Mathf.Max(range.xMin, bakedCells.xMin); x < Mathf.Min(range.xMax, bakedCells.xMax); x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (collisionContains(collisionMap.GetCellCenterWorld(cell))) collisionMap.SetTile(cell, null);
            }
        }
        #endregion
    }

    /// <summary>Dig Prefab保存后的旧烘焙遮罩清理；只检查本地资产引用，不接入运行时或替换共享关卡引用。</summary>
    internal sealed class DigMapAssetCleanup : AssetPostprocessor
    {
        #region 保存后清理无引用旧遮罩
        private const string LevelDirectory = "Assets/Game/Prefabs/DigLevel/";
        private static bool _scheduled;

        /// <summary>保存导入关卡后延迟核对落盘依赖；不在导入回调内删除资源，不在播放态清理。</summary>
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (_scheduled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Array.Exists(importedAssets, path => path.StartsWith(LevelDirectory, StringComparison.Ordinal) &&
                    path.EndsWith(".prefab", StringComparison.Ordinal))) return;
            _scheduled = true;
            EditorApplication.delayCall += RemoveUnusedLegacyMasks;
        }

        /// <summary>仅清理Maps下生成的_MapMask.asset；任一持久资产仍引用就保留，避免破坏共享遮罩。</summary>
        private static void RemoveUnusedLegacyMasks()
        {
            _scheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var candidates = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { LevelDirectory.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Maps/") && path.EndsWith("_MapMask.asset", StringComparison.Ordinal) &&
                    AssetDatabase.LoadAssetAtPath<Texture2D>(Path.ChangeExtension(path, ".png"))) candidates.Add(path);
            }
            if (candidates.Count == 0) return;
            string[] sources = Array.FindAll(AssetDatabase.GetAllAssetPaths(), path =>
                path.StartsWith("Assets/", StringComparison.Ordinal) && Path.HasExtension(path) && !candidates.Contains(path));
            var referenced = new System.Collections.Generic.HashSet<string>(AssetDatabase.GetDependencies(sources, false), StringComparer.Ordinal);
            foreach (string path in candidates)
                if (!referenced.Contains(path)) AssetDatabase.DeleteAsset(path);
        }
        #endregion
    }
}
#endif
