using System;
using Bear.EventSystem;
using Game.Events;
using Game.Play;
using UnityEngine;

namespace Game.Scripts.Effects
{
    /// <summary>
    /// 刮刮乐（SpriteRenderer 版）：玩家拖动/按下时擦除 SpriteRenderer 的可见区域。
    /// - 显示：GPU 盖章写入 Mask RenderTexture（不卡顿）
    /// - 进度：低分辨率 CPU 统计（实时、只统计非透明像素）
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SceneSpriteBrushClear : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("用于屏幕坐标 -> 世界坐标 -> Sprite UV 的相机。为空则使用 Camera.main。")]
        [SerializeField] private Camera inputCamera;

        [Tooltip("是否允许鼠标输入")]
        [SerializeField] private bool enableMouse = true;

        [Tooltip("是否允许触摸输入")]
        [SerializeField] private bool enableTouch = true;

        [Header("Brush")]
        [Tooltip("笔刷 Sprite（只用 alpha）。为空则使用圆形软边。")]
        [SerializeField] private Sprite brushSprite;

        [Tooltip("笔刷大小（局部 Sprite UV 0~1）。")]
        [Range(0.001f, 0.5f)]
        [SerializeField] private float brushSize = 0.06f;

        [Tooltip("笔刷边缘柔化（局部 Sprite UV 0~1）。")]
        [Range(0f, 0.1f)]
        [SerializeField] private float brushSoft = 0.01f;

        [Tooltip("拖动插值步长（局部 UV）。越小越细腻，但 stamp 次数更多。")]
        [Range(0.001f, 0.05f)]
        [SerializeField] private float interpolateStep = 0.01f;

        [Header("Mask RenderTexture (Visual)")]
        [SerializeField] private int maskWidth = 512;
        [SerializeField] private int maskHeight = 512;
        /// <summary>可选的编辑器烘焙初始遮罩；红通道表示空洞，必须可读。为空时保留旧关的全实心初态。</summary>
        [SerializeField] private Texture2D initialMask;
        /// <summary>供地图加载检查的烘焙遮罩；仅返回已有资产，不创建实例或触发数据链。</summary>
        public Texture2D InitialMask => initialMask;
        /// <summary>运行时遮罩像素尺寸；供 Dig 本地耗墨位图与 GPU 对齐，只读取配置，不创建纹理。</summary>
        public Vector2Int MaskResolution => new Vector2Int(maskWidth, maskHeight);
        /// <summary>当前运行时遮罩；可能因盖章交换或禁用释放，只读引用不能作为固定纹理缓存。</summary>
        public RenderTexture MaskTexture => _maskRT;
        /// <summary>本组件遮罩交换、重置或释放时通知局部表现；释放时传 null，不派发全局游戏事件。</summary>
        public event Action<RenderTexture> MaskTextureChanged;

        [Tooltip("显示用 Shader：默认 Sprites/MaskRTSprite。")]
        [SerializeField] private Shader displayShader;
        /// <summary>可选显示材质模板；运行时复制风格参数并绑定自有遮罩，空时保持原Shader默认材质流程。</summary>
        [Tooltip("显式绑定可保留描边等材质参数；不会修改该资源。为空时使用 Display Shader。")]
        [SerializeField] private Material displayMaterial;
                [SerializeField] private Shader stampShader;

        [Header("Progress (CPU, realtime)")]
        [Tooltip("用于进度统计的分辨率。越小越省但进度越粗。建议 128 或 256。")]
        [SerializeField] private int progressResolution = 192;

        [Tooltip("只统计 sprite 原始 alpha > 阈值的像素（0~1）。")]
        [Range(0f, 1f)]
        [SerializeField] private float spriteAlphaThreshold = 10f / 255f;

        [Tooltip("进度达到阈值即完成")]
        [Range(0f, 1f)]
        [SerializeField] private float completeThreshold = 0.5f;

        public event Action Completed;

        private SpriteRenderer _sr;
        private Sprite _sprite;

        // Visual mask
        private RenderTexture _maskRT;
        private RenderTexture _tmpRT;
        private Material _displayMat;
        private Material _stampMat;

        // Progress buffers (CPU)
        private bool[] _valid;   // non-transparent pixels
        private bool[] _cleared; // cleared pixels (subset of valid)
        private int _validCount;
        private int _clearedCount;
        private bool _completed;
        private bool _canErase = true;
        private EventSubscriber _subscriber;

        private Vector2 _lastUV = new Vector2(-1f, -1f);

        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int SpriteRectUVId = Shader.PropertyToID("_SpriteRectUV");
        private static readonly int UseSpriteRectUVId = Shader.PropertyToID("_UseSpriteRectUV");
        // Dig外扩描边需要Sprite实际局部尺寸；其它Shader没有该属性时保持原路径。
        private static readonly int SpriteLocalRectId = Shader.PropertyToID("_SpriteLocalRect");
        private static readonly int UseSpriteSlicedId = Shader.PropertyToID("_UseSpriteSliced");
        private static readonly int SpriteBorderUVId = Shader.PropertyToID("_SpriteBorderUV");
        private static readonly int SpriteBorderLocalId = Shader.PropertyToID("_SpriteBorderLocal");

        private static readonly int StampCenterId = Shader.PropertyToID("_Center");
        private static readonly int StampSizeId = Shader.PropertyToID("_Size");
        private static readonly int StampSoftId = Shader.PropertyToID("_Soft");
        private static readonly int StampAspectId = Shader.PropertyToID("_Aspect");
        private static readonly int StampUseBrushId = Shader.PropertyToID("_UseBrush");
        private static readonly int StampBrushTexId = Shader.PropertyToID("_BrushTex");

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            EventsUtils.ResetEvents(ref _subscriber);
            _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        }

        private void OnEnable()
        {
            _sprite = _sr != null ? _sr.sprite : null;
            EnsureMaterials();
            EnsureMaskRT();
            BuildProgressMasks();
            ResetAll();
        }

        private void OnDisable()
        {
            ReleaseRTs();
            DestroyMaterials();
            _valid = null;
            _cleared = null;
            _validCount = 0;
            _clearedCount = 0;
            _completed = false;
            _lastUV = new Vector2(-1f, -1f);
        }

        private void OnDestroy()
        {
            EventsUtils.ResetEvents(ref _subscriber);
        }

        private void Update()
        {
            if (_completed) return;
            if (!_canErase)
            {
                _lastUV = new Vector2(-1f, -1f);
                return;
            }

            if (!TryGetInputPosition(out var screenPos))
            {
                _lastUV = new Vector2(-1f, -1f);
                return;
            }

            if (!TryScreenToSpriteUV(screenPos, out var uv))
                return;

            if (_lastUV.x < 0f)
            {
                StampAt(uv);
            }
            else
            {
                float dist = Vector2.Distance(_lastUV, uv);
                if (dist <= Mathf.Max(0.0001f, interpolateStep))
                {
                    StampAt(uv);
                }
                else
                {
                    int seg = Mathf.Clamp(Mathf.CeilToInt(dist / Mathf.Max(0.0005f, interpolateStep)), 1, 64);
                    for (int i = 1; i <= seg; i++)
                    {
                        StampAt(Vector2.Lerp(_lastUV, uv, (float)i / seg));
                    }
                }
            }

            _lastUV = uv;

            if (!_completed && GetProgress() >= completeThreshold)
            {
                _completed = true;
                Completed?.Invoke();
            }
        }

        #region Public API

        /// <summary>
        /// 兼容旧调用：屏幕坐标 -> Sprite 局部 UV(0~1)。
        /// </summary>
        public bool TryGetSpriteUV(Vector2 screenPos, Camera camera, out Vector2 uv01)
        {
            // 旧逻辑允许外部传 camera，这里仍然支持；若为空则走配置/Camera.main。
            if (camera != null)
            {
                var bak = inputCamera;
                inputCamera = camera;
                bool ok = TryScreenToSpriteUV(screenPos, out uv01);
                inputCamera = bak;
                return ok;
            }

            return TryScreenToSpriteUV(screenPos, out uv01);
        }

        /// <summary>
        /// 兼容旧调用：外部传入 Sprite UV 直接擦除。
        /// </summary>
        public void StampEraseAt(Vector2 spriteUV01)
        {
            if (_completed) return;
            if (!_canErase) return;
            if (spriteUV01.x < 0f || spriteUV01.x > 1f || spriteUV01.y < 0f || spriteUV01.y > 1f)
                return;

            StampAt(spriteUV01);

            if (!_completed && GetProgress() >= completeThreshold)
            {
                _completed = true;
                Completed?.Invoke();
            }
        }

        /// <summary>外部指定横纵 UV 直径擦除；Dig 将同一世界直径除以 Sand 宽高，抵消非等比缩放。旧入口参数不变。</summary>
        public void StampEraseAt(Vector2 spriteUV01, Vector2 diameterUV)
        {
            if (_completed || !_canErase || spriteUV01.x < 0f || spriteUV01.x > 1f || spriteUV01.y < 0f || spriteUV01.y > 1f)
                return;
            if (diameterUV.x <= 0f || diameterUV.y <= 0f) return;
            float aspect = diameterUV.y / diameterUV.x;
            StampVisual(spriteUV01, diameterUV.y, 0f, aspect);
            StampProgress(spriteUV01, diameterUV.y, 0f, aspect, (diameterUV.y + 0.01f) / aspect * 0.5f);
            if (!_completed && GetProgress() >= completeThreshold)
            {
                _completed = true;
                Completed?.Invoke();
            }
        }

        public float GetProgress()
        {
            return _validCount > 0 ? Mathf.Clamp01((float)_clearedCount / _validCount) : 0f;
        }

        /// <summary>
        /// 兼容旧命名：返回擦除进度 0~1。
        /// </summary>
        public float GetClearProgress() => GetProgress();

        public void RefreshSpriteState()
        {
            _sprite = _sr != null ? _sr.sprite : null;
            EnsureMaterials();
            EnsureMaskRT();
            BuildProgressMasks();
            ResetAll();
        }

        public void ResetAll()
        {
            RefreshGeometryBeforeReset();
            _completed = false;
            _lastUV = new Vector2(-1f, -1f);

            // 有烘焙地图时恢复初始空洞；旧关未绑定时仍清空视觉遮罩。
            if (_maskRT != null)
            {
                var prev = RenderTexture.active;
                if (initialMask) Graphics.Blit(initialMask, _maskRT);
                else
                {
                    RenderTexture.active = _maskRT;
                    GL.Clear(true, true, Color.black);
                }
                RenderTexture.active = prev;
            }

            // clear progress
            _clearedCount = 0;
            if (_cleared != null)
            {
                int resolution = Mathf.RoundToInt(Mathf.Sqrt(_cleared.Length));
                for (int i = 0; i < _cleared.Length; i++)
                {
                    _cleared[i] = initialMask && _valid[i] && initialMask.GetPixelBilinear(
                        (i % resolution + 0.5f) / resolution, (i / resolution + 0.5f) / resolution).r > 0.15f;
                    if (_cleared[i]) _clearedCount++;
                }
            }

            ApplyMaskToMaterial();
        }

        #endregion

        private void OnSwitchGameState(SwitchGameStateEvent evt)
        {
            _canErase = GamePlayStateName.PLAYING.Equals(evt.NewState);
            if (!_canErase)
                _lastUV = new Vector2(-1f, -1f);
        }

        #region Input + UV

        private bool TryGetInputPosition(out Vector2 screenPos)
        {
            screenPos = default;

            if (enableTouch && Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    screenPos = t.position;
                    return true;
                }
            }

            if (enableMouse && Input.GetMouseButton(0))
            {
                screenPos = Input.mousePosition;
                return true;
            }

            return false;
        }

        private bool TryScreenToSpriteUV(Vector2 screenPos, out Vector2 uv01)
        {
            uv01 = default;

            if (_sprite == null)
                return false;

            var cam = inputCamera != null ? inputCamera : Camera.main;
            if (cam == null)
                return false;

            Ray ray = cam.ScreenPointToRay(screenPos);
            Plane plane = new Plane(transform.forward, transform.position);
            if (!plane.Raycast(ray, out float enter))
                return false;

            Vector3 localPos = transform.InverseTransformPoint(ray.GetPoint(enter));
            if (_sr != null && _sr.drawMode == SpriteDrawMode.Sliced)
            {
                Bounds bounds = _sr.localBounds;
                uv01 = new Vector2((localPos.x - bounds.min.x) / bounds.size.x, (localPos.y - bounds.min.y) / bounds.size.y);
                return uv01.x >= 0f && uv01.x <= 1f && uv01.y >= 0f && uv01.y <= 1f;
            }
            float x = localPos.x * _sprite.pixelsPerUnit + _sprite.pivot.x;
            float y = localPos.y * _sprite.pixelsPerUnit + _sprite.pivot.y;

            uv01 = new Vector2(x / _sprite.rect.width, y / _sprite.rect.height);
            return uv01.x >= 0f && uv01.x <= 1f && uv01.y >= 0f && uv01.y <= 1f;
        }

        private float GetAspect()
        {
            if (_sprite == null) return 1f;
            if (_sr != null && _sr.drawMode == SpriteDrawMode.Sliced)
                return _sr.localBounds.size.x / Mathf.Max(_sr.localBounds.size.y, 0.0001f);
            float a = _sprite.rect.height > 0 ? (_sprite.rect.width / _sprite.rect.height) : 1f;
            return (!float.IsNaN(a) && !float.IsInfinity(a) && a > 0f) ? a : 1f;
        }

        #endregion

        #region Visual (GPU stamp)

        private void EnsureMaterials()
        {
            if (_sr == null) return;

            if (displayShader == null)
                displayShader = Shader.Find("Sprites/MaskRTSprite");

            if (_displayMat == null && (displayMaterial != null || displayShader != null))
            {
                _displayMat = displayMaterial != null ? new Material(displayMaterial) : new Material(displayShader);
                _sr.material = _displayMat;
            }

            if (_stampMat == null)
            {
                if (stampShader != null)
                    _stampMat = new Material(stampShader);
            }

            ApplySpriteRectUVToMaterial();
        }

        private void DestroyMaterials()
        {
            if (_sr != null && _sr.material == _displayMat)
                _sr.material = null;

            if (_displayMat != null) Destroy(_displayMat);
            if (_stampMat != null) Destroy(_stampMat);
            _displayMat = null;
            _stampMat = null;
        }

        private void EnsureMaskRT()
        {
            if (_maskRT != null && _maskRT.width == maskWidth && _maskRT.height == maskHeight)
                return;

            ReleaseRTs();

            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;

            _maskRT = new RenderTexture(maskWidth, maskHeight, 0, format)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _maskRT.Create();

            _tmpRT = new RenderTexture(maskWidth, maskHeight, 0, format)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _tmpRT.Create();

            ApplyMaskToMaterial();
        }

        private void ReleaseRTs()
        {
            bool hadMask = _maskRT != null;
            if (_maskRT != null) { _maskRT.Release(); _maskRT = null; }
            if (_tmpRT != null) { _tmpRT.Release(); _tmpRT = null; }
            if (hadMask) MaskTextureChanged?.Invoke(null);
        }

        private void ApplyMaskToMaterial()
        {
            if (_displayMat == null || _maskRT == null) return;
            _displayMat.SetTexture(MaskTexId, _maskRT);
            MaskTextureChanged?.Invoke(_maskRT);
        }

        private void ApplySpriteRectUVToMaterial()
        {
            if (_displayMat == null || _sprite == null || _sprite.texture == null)
                return;

            Texture2D tex = _sprite.texture;
            Rect r = _sprite.textureRect;
            if (tex.width <= 0 || tex.height <= 0)
                return;

            Vector4 rectUV = new Vector4(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            bool useRect = !(Mathf.Approximately(rectUV.x, 0f) &&
                             Mathf.Approximately(rectUV.y, 0f) &&
                             Mathf.Approximately(rectUV.z, 1f) &&
                             Mathf.Approximately(rectUV.w, 1f));

            _displayMat.SetVector(SpriteRectUVId, rectUV);
            _displayMat.SetFloat(UseSpriteRectUVId, useRect ? 1f : 0f);
            ApplySpriteGeometryToMaterial(_displayMat, _sr);
        }

        #region Sliced本地几何参数

        // 播放态InitMap最后会ResetAll；只在图片／实际矩形变更时重建CPU Alpha，始终刷新自有材质几何。
        private void RefreshGeometryBeforeReset()
        {
            if (_sr == null) return;
            bool rebuildProgress = _sprite != _sr.sprite;
            _sprite = _sr.sprite;
            if (_sprite != null && _displayMat != null && _displayMat.HasProperty(SpriteLocalRectId))
            {
                bool sliced = _sr.drawMode == SpriteDrawMode.Sliced;
                Bounds bounds = sliced ? _sr.localBounds : _sprite.bounds;
                Vector4 currentRect = new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
                rebuildProgress |= _displayMat.GetVector(SpriteLocalRectId) != currentRect;
                if (_displayMat.HasProperty(UseSpriteSlicedId))
                {
                    rebuildProgress |= (_displayMat.GetFloat(UseSpriteSlicedId) > 0.5f) != sliced;
                    if (sliced)
                    {
                        Vector4 border = _sprite.border;
                        Vector4 borderUV = new Vector4(border.x / _sprite.rect.width, border.y / _sprite.rect.height,
                            border.z / _sprite.rect.width, border.w / _sprite.rect.height);
                        rebuildProgress |= _displayMat.GetVector(SpriteBorderUVId) != borderUV
                                           || _displayMat.GetVector(SpriteBorderLocalId) != border / _sprite.pixelsPerUnit;
                    }
                }
            }
            ApplySpriteRectUVToMaterial();
            if (rebuildProgress) BuildProgressMasks();
        }

        /// <summary>
        /// 同步已绑定SpriteRenderer的实际矩形与九宫格边框；供运行材质初始化和显式Editor InitMap共用。
        /// 只读本地Sprite／Renderer并写传入材质，不创建实例、不访问运行服务；修改Size后须重新InitMap／初始化。
        /// </summary>
        public static void ApplySpriteGeometryToMaterial(Material material, SpriteRenderer renderer)
        {
            if (material == null || renderer == null || renderer.sprite == null || !material.HasProperty(SpriteLocalRectId))
                return;
            Sprite sprite = renderer.sprite;
            bool sliced = renderer.drawMode == SpriteDrawMode.Sliced;
            Bounds bounds = sliced ? renderer.localBounds : sprite.bounds;
            material.SetVector(SpriteLocalRectId, new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            if (!material.HasProperty(UseSpriteSlicedId)) return;
            material.SetFloat(UseSpriteSlicedId, sliced ? 1f : 0f);
            Vector4 border = sprite.border;
            material.SetVector(SpriteBorderUVId, new Vector4(border.x / sprite.rect.width, border.y / sprite.rect.height,
                border.z / sprite.rect.width, border.w / sprite.rect.height));
            material.SetVector(SpriteBorderLocalId, border / sprite.pixelsPerUnit);
        }

        // 与Dig/Sand的SliceAxis一致，仅CPU进度初态采样使用，不改变遮罩空间坐标。
        private static float SliceAxis(float uv, float size, float left, float right, float sourceLeft, float sourceRight)
        {
            float factor = Mathf.Min(1f, size / Mathf.Max(left + right, 0.000001f));
            left *= factor;
            right *= factor;
            float position = uv * size;
            if (position < left) return position / Mathf.Max(left, 0.000001f) * sourceLeft;
            if (position > size - right) return 1f - sourceRight + (position - size + right) / Mathf.Max(right, 0.000001f) * sourceRight;
            return sourceLeft + (position - left) / Mathf.Max(size - left - right, 0.000001f) * (1f - sourceLeft - sourceRight);
        }

        #endregion

        private void StampAt(Vector2 uv01)
        {
            StampVisual(uv01, Mathf.Max(0.0001f, brushSize), brushSoft, GetAspect());
            // Progress (CPU, low-res)
            StampProgress(uv01);
        }

        /// <summary>共用 GPU 写入；旧接口传原参数，显式尺寸接口传抵消世界缩放后的 UV 参数。</summary>
        private void StampVisual(Vector2 uv01, float size, float soft, float aspect)
        {
            // Visual
            if (_maskRT != null && _tmpRT != null && _stampMat != null)
            {
                bool useBrush = brushSprite != null && brushSprite.texture != null;

                _stampMat.SetVector(StampCenterId, new Vector4(uv01.x, uv01.y, 0f, 0f));
                _stampMat.SetFloat(StampSizeId, size);
                _stampMat.SetFloat(StampSoftId, soft);
                _stampMat.SetFloat(StampAspectId, aspect);
                _stampMat.SetFloat(StampUseBrushId, useBrush ? 1f : 0f);
                if (useBrush)
                    _stampMat.SetTexture(StampBrushTexId, brushSprite.texture);

                Graphics.Blit(_maskRT, _tmpRT, _stampMat);
                var t = _maskRT; _maskRT = _tmpRT; _tmpRT = t;
                ApplyMaskToMaterial();
            }

        }

        #endregion

        #region Progress (CPU, realtime, only non-transparent)

        private void BuildProgressMasks()
        {
            int res = Mathf.Clamp(progressResolution, 32, 512);
            int total = res * res;
            _valid = new bool[total];
            _cleared = new bool[total];
            _validCount = 0;
            _clearedCount = 0;

            if (_sprite == null || _sprite.texture == null)
            {
                for (int i = 0; i < total; i++) _valid[i] = true;
                _validCount = total;
                return;
            }

            Texture2D tex = _sprite.texture;
            Rect rect = _sprite.textureRect;
            float invW = 1f / tex.width;
            float invH = 1f / tex.height;
            bool sliced = _sr != null && _sr.drawMode == SpriteDrawMode.Sliced;
            Vector3 size = sliced ? _sr.localBounds.size : Vector3.one;
            Vector4 borderUV = new Vector4(_sprite.border.x / _sprite.rect.width, _sprite.border.y / _sprite.rect.height,
                _sprite.border.z / _sprite.rect.width, _sprite.border.w / _sprite.rect.height);
            Vector4 borderLocal = _sprite.border / _sprite.pixelsPerUnit;

            for (int y = 0; y < res; y++)
            {
                float v = (y + 0.5f) / res;
                float ty = rect.y * invH + v * (rect.height * invH);
                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res;
                    float tx = rect.x * invW + u * (rect.width * invW);
                    float sampleY = ty;
                    if (sliced)
                    {
                        float imageU = SliceAxis(u, size.x, borderLocal.x, borderLocal.z, borderUV.x, borderUV.z);
                        float imageV = SliceAxis(v, size.y, borderLocal.y, borderLocal.w, borderUV.y, borderUV.w);
                        tx = (rect.x + imageU * rect.width) * invW;
                        sampleY = (rect.y + imageV * rect.height) * invH;
                    }
                    bool valid;
                    try
                    {
                        valid = tex.GetPixelBilinear(tx, sampleY).a > spriteAlphaThreshold;
                    }
                    catch
                    {
                        // 不可读时退化：全部有效（仍可正常玩，只是进度统计不排除透明边缘）
                        valid = true;
                    }

                    int idx = y * res + x;
                    _valid[idx] = valid;
                    if (valid) _validCount++;
                }
            }
        }

        private void StampProgress(Vector2 uv01)
        {
            float aspect = GetAspect();
            float size = Mathf.Max(0.0001f, brushSize);
            float soft = Mathf.Max(0f, brushSoft);
            StampProgress(uv01, size, soft, aspect, (size + soft + 0.01f) * aspect * 0.5f);
        }

        /// <summary>共用 CPU 统计；旧入口保留原包围宽度，显式尺寸入口按实际横向直径遍历。</summary>
        private void StampProgress(Vector2 uv01, float size, float soft, float aspect, float halfW)
        {
            if (_valid == null || _cleared == null)
                return;

            int res = Mathf.RoundToInt(Mathf.Sqrt(_valid.Length));
            if (res <= 0) return;

            // Bounding box in progress grid
            float sizeEps = size + soft + 0.01f;
            float halfH = sizeEps * 0.5f;

            int xMin = Mathf.Clamp(Mathf.FloorToInt((uv01.x - halfW) * res), 0, res - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt((uv01.x + halfW) * res), 0, res);
            int yMin = Mathf.Clamp(Mathf.FloorToInt((uv01.y - halfH) * res), 0, res - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt((uv01.y + halfH) * res), 0, res);

            for (int y = yMin; y < yMax; y++)
            {
                float v = (y + 0.5f) / res;
                for (int x = xMin; x < xMax; x++)
                {
                    int idx = y * res + x;
                    if (!_valid[idx] || _cleared[idx])
                        continue;

                    float u = (x + 0.5f) / res;

                    float dx = (u - uv01.x) * aspect;
                    float dy = (v - uv01.y);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float edge0 = Mathf.Max(size * 0.5f - soft, 0f);
                    float edge1 = size * 0.5f + soft;
                    float t = Mathf.Clamp01((dist - edge0) / Mathf.Max(edge1 - edge0, 1e-6f));
                    // smoothstep
                    float alpha = 1f - (t * t * (3f - 2f * t));

                    // Progress: treat alpha > small threshold as cleared
                    if (alpha > 0.15f)
                    {
                        _cleared[idx] = true;
                        _clearedCount++;
                    }
                }
            }
        }

        #endregion
    }
}
