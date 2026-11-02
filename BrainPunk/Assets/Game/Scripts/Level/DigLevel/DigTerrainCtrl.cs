using System;
using System.IO;
using System.IO.Compression;
using Game.Scripts.Effects;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Dig
{
    /// <summary>同一圆笔刷同时擦除沙土视觉和隐藏 Tilemap；固定砖块不属于此 Tilemap。</summary>
    public sealed partial class DigTerrainCtrl : MonoBehaviour
    {
        #region Prefab 绑定
        /// <summary>关闭自带鼠标/触摸输入的擦除组件，笔刷使用无软边圆形。</summary>
        [SerializeField] private SceneSpriteBrushClear visual;
        /// <summary>沙土 Sprite；使用实际本地矩形及变换换算 UV，支持非等比尺寸调整。</summary>
        [SerializeField] private SpriteRenderer sandSprite;
        /// <summary>只负责碰撞的隐藏 Tilemap，绑定静态刚体与 CompositeCollider2D。</summary>
        [SerializeField] private Tilemap collisionMap;
        /// <summary>碰撞网格；显式绑定 SandGrid，细分只改变格子密度，不改变地形世界范围。</summary>
        [SerializeField] private Grid collisionGrid;
        /// <summary>用于立即更新已移除地块的 TilemapCollider2D。</summary>
        [SerializeField] private TilemapCollider2D tileCollider;
        /// <summary>实心 Grid Collider 类型 Tile 资产。</summary>
        [SerializeField] private TileBase sandTile;
        /// <summary>视觉与碰撞共用的世界半径；UV 尺寸按 Sand 当前宽高换算，非等比缩放后仍为正圆。</summary>
        [SerializeField] private float brushRadius = 0.75f;
        /// <summary>每个基础格子的单轴细分数；InitMap 烘焙生效。4 表示把 0.2 格细化为 0.05，格数按平方增加。</summary>
        [Header("挖掘精度（InitMap 后保存）")]
        [Tooltip("越大洞口越细腻，碰撞生成开销越高。首关建议 4；1 恢复原精度。")]
        [SerializeField, Range(1, 8)] private int collisionSubdivisions = 4;
        /// <summary>连续笔画的采样间距占半径比例；视觉与碰撞共用，越小快划轮廓越平滑。</summary>
        [Tooltip("首关 0.1 对应 0.075 世界单位；不要通过共享擦除组件的自带输入调节。")]
        [SerializeField, Range(0.02f, 0.3f)] private float strokeStepRatio = 0.1f;
        /// <summary>仅 InitMap 按图片 Alpha 裁剪外轮廓；旧烘焙地图不自动改变，关闭时恢复矩形填格。</summary>
        [Header("碰撞贴边（InitMap 后保存）")]
        [Tooltip("InitMap 按沙土图片透明轮廓裁剪碰撞格，支持 Simple／Sliced；关闭后按原矩形填格。已有地图需重新烘焙。")]
        [SerializeField] private bool fitSpriteOutline = true;
        /// <summary>仅轮廓烘焙读取的图片 Alpha 门槛；不含 Shader 外描边，碰撞仍有半格量级误差。</summary>
        [Tooltip("0.1 排除透明区域；增大可进一步收进半透明边缘。修改后 InitMap 并保存。")]
        [SerializeField, Range(0.01f, 1f)] private float collisionAlphaThreshold = 0.1f;
        /// <summary>碰撞相对视觉的世界单位偏移；出生/预留洞在 InitMap 读取，玩家笔刷在 BindRuntime 读取。</summary>
        [Tooltip("0 按格中心贴近视觉圆；正值扩大碰撞洞口，负值缩小。建议每次调整 0.005～0.01，过大会穿入可见沙土。")]
        [SerializeField, Range(-0.1f, 0.1f)] private float collisionRadiusOffset;
        /// <summary>Tilemap 碰撞格的世界单位挤出量；InitMap 烘焙时设置。用于连接相邻格，过大会侵入洞口。</summary>
        [Tooltip("世界单位，建议 0.001；0 关闭挤出。修改后执行 InitMap 并保存，由本字段换算 TilemapCollider2D 参数。")]
        [SerializeField, Range(0f, 0.01f)] private float colliderExtrusion = 0.001f;
        /// <summary>显式绑定挖洞对象；有 HoleShape 时读取轮廓并在 InitMap 后按 IsShow 设置其 enabled，否则只读取 Radius。</summary>
        [Tooltip("Actor/Germ 勾选 Is Show；障碍物绑定 Hole Shape，保留砖块时也勾选 Is Show。")]
        [SerializeField] private BaseDigComp[] spawnHoles;
        /// <summary>Sand 子节点上的预留洞标记；InitMap 后仅切换 HoleShape.enabled，下次仍读取 inactive 节点及禁用的碰撞体。</summary>
        [SerializeField] private BaseDigComp[] reservedHoles = Array.Empty<BaseDigComp>();
        /// <summary>烘焙时保存的碰撞格范围；恢复压缩初态与玩家挖掘共用，不按洞源重新烘焙。</summary>
        [SerializeField, HideInInspector] private BoundsInt bakedCells;
        /// <summary>上次烘焙的细分数；再次 InitMap 时用于恢复基础比例，避免重复缩小 Grid。</summary>
        [SerializeField, HideInInspector] private int bakedSubdivisions;
        /// <summary>已完成编辑器地图烘焙；没有地图的关卡加载失败，不在运行时自动生成兜底。</summary>
        [SerializeField, HideInInspector] private bool mapBaked;
        /// <summary>InitMap生成的Deflate占位位图，按GetTilesBlock顺序每格一位；为空时兼容旧Prefab内Tilemap。</summary>
        [SerializeField, HideInInspector] private byte[] compressedCollisionCells = Array.Empty<byte>();
        #endregion

        #region 压缩碰撞初态恢复
        /// <summary>仅BindRuntime接入或显式播放烘焙时恢复已存格图；不读洞源，解码完成后才覆盖Tilemap。</summary>
        private void RestoreBakedCollisionCells()
        {
            if (compressedCollisionCells == null || compressedCollisionCells.Length == 0) return;
            if (bakedCells.size.x <= 0 || bakedCells.size.y <= 0 || bakedCells.size.z != 1)
                throw new InvalidDataException("Dig压缩地图范围无效，请重新InitMap并保存。");
            int count = checked(bakedCells.size.x * bakedCells.size.y);
            var bitmap = new byte[checked((count + 7) / 8)];
            using (var input = new MemoryStream(compressedCollisionCells, false))
            using (var inflater = new DeflateStream(input, CompressionMode.Decompress))
            {
                int offset = 0;
                while (offset < bitmap.Length)
                {
                    int read = inflater.Read(bitmap, offset, bitmap.Length - offset);
                    if (read == 0) throw new InvalidDataException("Dig压缩地图数据不完整，请重新InitMap并保存。");
                    offset += read;
                }
                if (inflater.ReadByte() != -1)
                    throw new InvalidDataException("Dig压缩地图与烘焙范围不匹配，请重新InitMap并保存。");
            }
            var tiles = new TileBase[count];
            for (int i = 0; i < count; i++)
                if ((bitmap[i >> 3] & (1 << (i & 7))) != 0) tiles[i] = sandTile;
            collisionMap.ClearAllTiles();
            collisionMap.SetTilesBlock(bakedCells, tiles);
            tileCollider.ProcessTilemapChanges();
        }
        #endregion

        #region 运行时笔刷
        private BoundsInt _runtimeCells;
        private float _collisionRadius;
        // 与视觉遮罩同尺寸的余沙位图；每像素占一位，不从 GPU 读回，也不使用低精度进度或碰撞判断耗墨。
        private uint[] _sandPixels;
        private int _maskWidth, _maskHeight;
        /// <summary>连续路径采样距离，避免快划产生视觉/碰撞断层。</summary>
        public float StrokeStep => brushRadius * Mathf.Clamp(strokeStepRatio, 0.02f, 0.3f);

        /// <summary>检查路径中心是否处于沙土范围；仅供 Dig 输入耗墨判断，不读取运行时单例。</summary>
        public bool ContainsPoint(Vector2 world)
        {
            Vector3 local = sandSprite.transform.InverseTransformPoint(world);
            Bounds bounds = sandSprite.localBounds;
            return local.x >= bounds.min.x && local.x <= bounds.max.x && local.y >= bounds.min.y && local.y <= bounds.max.y;
        }

        /// <summary>新关接入已烘焙地图，紧凑格式先恢复碰撞初态；不读取出生对象或重新烘焙洞口。</summary>
        public void BindRuntime()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("DigTerrain 只能在播放时接入地图。");
            ValidateBindings();
            if (!mapBaked || !visual.InitialMask || bakedSubdivisions <= 0)
                throw new InvalidOperationException("DigTerrain 尚未烘焙，请在 Prefab Mode 点击 InitMap 并保存。");
            RestoreBakedCollisionCells();
            _runtimeCells = bakedCells;
            _collisionRadius = Mathf.Max(0f, brushRadius + Mathf.Clamp(collisionRadiusOffset, -0.1f, 0.1f));
            // 接入时只把已烘焙遮罩装入运行时 RT，不重新填土或根据对象计算空洞。
            visual.ResetAll();
            BuildSandPixels();
        }

        /// <summary>每次接入地图从烘焙图恢复余沙；不同纹理尺寸按 GPU 的双线性采样读取初态。</summary>
        private void BuildSandPixels()
        {
            Texture2D mask = visual.InitialMask;
            Vector2Int resolution = visual.MaskResolution;
            _maskWidth = resolution.x;
            _maskHeight = resolution.y;
            int count = _maskWidth * _maskHeight;
            _sandPixels = new uint[(count + 31) / 32];
            bool direct = mask.format == TextureFormat.RGBA32 && mask.width == _maskWidth && mask.height == _maskHeight;
            var pixels = direct ? mask.GetRawTextureData<Color32>() : default;
            // GetPixelBilinear 的坐标以像素索引为基准；减半个源纹素，对齐 GPU 在目标像素中心的采样。
            for (int i = 0; i < count; i++)
            {
                float erased = direct ? pixels[i].r / 255f : mask.GetPixelBilinear(
                    (i % _maskWidth + 0.5f) / _maskWidth - 0.5f / mask.width,
                    (i / _maskWidth + 0.5f) / _maskHeight - 0.5f / mask.height).r;
                if (erased < 1f) _sandPixels[i >> 5] |= 1u << (i & 31);
            }
        }

        /// <summary>按视觉圆章覆盖清除余沙位；返回是否擦到新沙，不把章边缘的旧空洞再次计费。</summary>
        private bool ClearSandPixels(Vector2 uv, Vector2 diameterUV)
        {
            Vector2 radius = diameterUV * 0.5f;
            int xMin = Mathf.Max(0, Mathf.FloorToInt((uv.x - radius.x) * _maskWidth));
            int xMax = Mathf.Min(_maskWidth, Mathf.CeilToInt((uv.x + radius.x) * _maskWidth));
            int yMin = Mathf.Max(0, Mathf.FloorToInt((uv.y - radius.y) * _maskHeight));
            int yMax = Mathf.Min(_maskHeight, Mathf.CeilToInt((uv.y + radius.y) * _maskHeight));
            bool changed = false;
            for (int y = yMin; y < yMax; y++)
            {
                float dy = ((y + 0.5f) / _maskHeight - uv.y) / radius.y;
                for (int x = xMin; x < xMax; x++)
                {
                    int index = y * _maskWidth + x;
                    uint bit = 1u << (index & 31);
                    if ((_sandPixels[index >> 5] & bit) == 0) continue;
                    float dx = ((x + 0.5f) / _maskWidth - uv.x) / radius.x;
                    if (dx * dx + dy * dy > 1f) continue;
                    _sandPixels[index >> 5] &= ~bit;
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>只检查当前序列化引用；编辑器烘焙也可调用，不初始化运行时服务。</summary>
        private void ValidateBindings()
        {
            if (!visual || !sandSprite || !sandSprite.sprite || !collisionMap || !collisionGrid ||
                collisionMap.layoutGrid != collisionGrid || !tileCollider || !sandTile || brushRadius <= 0)
                throw new InvalidOperationException("DigTerrain Prefab 绑定无效。");
            Vector3 size = sandSprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f || !float.IsFinite(size.x) || !float.IsFinite(size.y))
                throw new InvalidOperationException("Sand 必须具有有限的非零尺寸。");
        }

        /// <summary>将世界矩形的四角转换为格范围；兼容网格旋转，不只取两个对角。</summary>
        private BoundsInt WorldBoundsToCells(Bounds bounds)
        {
            Vector3Int min = collisionMap.WorldToCell(bounds.min);
            Vector3Int max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, bounds.center.z);
                Vector3Int cell = collisionMap.WorldToCell(corner);
                min = Vector3Int.Min(min, cell);
                max = Vector3Int.Max(max, cell);
            }
            return new BoundsInt(min.x, min.y, 0, max.x - min.x + 1, max.y - min.y + 1, 1);
        }

        /// <summary>处理单个有效路径样本；只有视觉圆章擦到新沙才返回 true，供 Dig 按采样长度耗墨。完全空洞不盖章。</summary>
        /// <remarks>仅 BindRuntime 后由运行时输入调用；碰撞仍按格中心删格，边缘存在半格量级近似误差。</remarks>
        public bool Stamp(Vector2 world)
        {
            Vector3 local = sandSprite.transform.InverseTransformPoint(world);
            Bounds bounds = sandSprite.localBounds;
            Vector2 uv = new Vector2((local.x - bounds.min.x) / bounds.size.x, (local.y - bounds.min.y) / bounds.size.y);
            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return false;
            float width = sandSprite.transform.TransformVector(Vector3.right * bounds.size.x).magnitude;
            float height = sandSprite.transform.TransformVector(Vector3.up * bounds.size.y).magnitude;
            Vector2 diameterUV = new Vector2(brushRadius * 2f / width, brushRadius * 2f / height);
            if (!ClearSandPixels(uv, diameterUV)) return false;
            visual.StampEraseAt(uv, diameterUV);
            BoundsInt range = WorldBoundsToCells(new Bounds(world, Vector3.one * _collisionRadius * 2f));
            for (int y = Mathf.Max(range.yMin, _runtimeCells.yMin); y < Mathf.Min(range.yMax, _runtimeCells.yMax); y++)
            for (int x = Mathf.Max(range.xMin, _runtimeCells.xMin); x < Mathf.Min(range.xMax, _runtimeCells.xMax); x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (_runtimeCells.Contains(cell) && ((Vector2)collisionMap.GetCellCenterWorld(cell) - world).sqrMagnitude <= _collisionRadius * _collisionRadius)
                    collisionMap.SetTile(cell, null);
            }
            return true;
        }

        /// <summary>一笔路径样本完成后立即更新复合碰撞，不等待下一帧隐式刷新。</summary>
        public void FlushCollider() => tileCollider.ProcessTilemapChanges();
        #endregion
    }
}
