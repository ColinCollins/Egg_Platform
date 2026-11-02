using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using Unity.Collections;

/// <summary>
/// 刮刮乐效果：将 mask 写入 RenderTexture，供 UI 使用。手指/鼠标刮擦可擦除遮罩露出底层内容。
/// </summary>
public class BrushClearEffect : MonoBehaviour
{
    [Header("Mask")]
    [SerializeField] private Graphic _maskGraphic;
    [SerializeField] private int _maskWidth = 512;
    [SerializeField] private int _maskHeight = 512;
    [SerializeField] private float _soft = 0.005f;

    [Header("Default Circle")]
    [SerializeField] private Vector2 _defaultCircleCenter = new Vector2(0.5f, 0.5f);
    [SerializeField] private float _defaultCircleRadius = 0.3f;

    [Header("刮刮乐笔刷")]
    [Tooltip("笔刷 Sprite：需可读纹理（Read/Write 或非图集），未设置则用圆形。纹理尺寸无硬性要求，建议 64～256 像素以兼顾效果与性能（每戳一次会按 mask 分辨率采样笔刷）。")]
    [SerializeField] private Sprite _brushSprite;
    [Tooltip("笔刷在屏幕上的大小，屏幕 UV 比例 (0~1)，默认 0.05 约 5% 屏幕。")]
    [SerializeField] private float _brushSize = 0.05f;
    [SerializeField] private bool _enableMouseErase = true;
    [SerializeField, ShowIf("_enableMouseErase")] private float _stampMoveThreshold = 0.005f;

    [SerializeField] private Shader _maskRTShader;
    private Vector2 _lastStampUV = new Vector2(-1f, -1f);

    private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");

    private RenderTexture _maskRT;
    private Material _displayMaterial;
    private Texture2D _workTexture;
    private bool _maskTexApplied;

    private const int BrushCacheSize = 256;
    private byte[] _brushCache;
    private bool _brushCacheDirty = true;

    private void OnEnable()
    {
        EnsureMaterials();
        EnsureMaskRT();
    }

    private void OnDisable()
    {
        ReleaseMaskRT();
        ReleaseWorkTexture();
        _brushCache = null;
        _brushCacheDirty = true;
        DestroyDisplayMaterial();
    }

    private void Update()
    {
        if (!_enableMouseErase || _maskRT == null) return;

        Vector2? inputUV = GetInputUV();
        if (!inputUV.HasValue)
        {
            _lastStampUV.x = -1f;
            return;
        }

        var uv = inputUV.Value;
        if (_lastStampUV.x >= 0 && Vector2.Distance(uv, _lastStampUV) < _stampMoveThreshold)
            return;

        _lastStampUV = uv;
        StampEraseAt(uv);
    }

    private static Vector2? GetInputUV()
    {
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Began)
                return new Vector2(t.position.x / Screen.width, t.position.y / Screen.height);
        }
        if (Input.GetMouseButton(0))
            return new Vector2(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height);
        return null;
    }

    private void EnsureMaskRT()
    {
        if (_maskRT != null && _maskRT.width == _maskWidth && _maskRT.height == _maskHeight)
            return;

        ReleaseMaskRT();
        // Use R8 for optimization if supported, else default (usually ARGB32)
        // R8 uses 1/4 memory of ARGB32. MaskRT shader uses 'r' channel.
        var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) 
            ? RenderTextureFormat.R8 
            : RenderTextureFormat.ARGB32;
            
        _maskRT = new RenderTexture(_maskWidth, _maskHeight, 0, format)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _maskRT.Create();
        _maskTexApplied = false;
        ResetMaskRT();
    }

    private void ReleaseMaskRT()
    {
        if (_maskRT == null) return;
        _maskRT.Release();
        _maskRT = null;
        _maskTexApplied = false;
    }

    private void EnsureMaterials()
    {
        if (_displayMaterial == null && _maskGraphic != null)
        {
            if (_maskRTShader == null) _maskRTShader = Shader.Find("UI/MaskRT");
            if (_maskRTShader != null)
            {
                _displayMaterial = new Material(_maskRTShader);
                _maskGraphic.material = _displayMaterial;
            }
        }
    }

    private void EnsureWorkTexture()
    {
        if (_workTexture != null && _workTexture.width == _maskWidth && _workTexture.height == _maskHeight)
            return;

        ReleaseWorkTexture();
        // Use R8 texture format to match RT and save CPU memory/bandwidth
        // Fallback to Alpha8 or R8 (Alpha8 is also 1 byte but legacy)
        // R8 is best. Check support? TextureFormat.R8 is widely supported.
        _workTexture = new Texture2D(_maskWidth, _maskHeight, TextureFormat.R8, false);
        _workTexture.filterMode = FilterMode.Bilinear;
        _workTexture.wrapMode = TextureWrapMode.Clamp;
    }

    private void ReleaseWorkTexture()
    {
        if (_workTexture != null)
        {
            Destroy(_workTexture);
            _workTexture = null;
        }
    }

    private void EnsureBrushCache()
    {
        if (_brushSprite == null || _brushSprite.texture == null)
        {
            _brushCache = null;
            _brushCacheDirty = false;
            return;
        }
        if (_brushCache != null && _brushCache.Length == BrushCacheSize * BrushCacheSize && !_brushCacheDirty)
            return;

        _brushCache = new byte[BrushCacheSize * BrushCacheSize];
        Texture2D tex = _brushSprite.texture;
        Rect r = _brushSprite.textureRect;
        float invW = 1f / tex.width;
        float invH = 1f / tex.height;
        for (int j = 0; j < BrushCacheSize; j++)
        {
            float sy = r.y * invH + (j + 0.5f) / BrushCacheSize * (r.height * invH);
            for (int i = 0; i < BrushCacheSize; i++)
            {
                float sx = r.x * invW + (i + 0.5f) / BrushCacheSize * (r.width * invW);
                _brushCache[j * BrushCacheSize + i] = (byte)(tex.GetPixelBilinear(sx, sy).a * 255f);
            }
        }
        _brushCacheDirty = false;
    }

    private void DestroyDisplayMaterial()
    {
        if (_maskGraphic != null && _maskGraphic.material == _displayMaterial)
            _maskGraphic.material = null;

        if (_displayMaterial != null)
        {
            Destroy(_displayMaterial);
            _displayMaterial = null;
        }
    }

    /// <summary>
    /// 重置 RT：清空后写入默认 circle。
    /// </summary>
    [Button("Reset RT")]
    public void ResetMaskRT()
    {
        ClearMask();
        WriteCircleToMask(_defaultCircleCenter, _defaultCircleRadius);
    }

    /// <summary>
    /// 在指定位置戳一个擦除（刮刮乐：露出底层内容）。仅处理笔刷包围盒内的像素以降低开销。
    /// </summary>
    public void StampEraseAt(Vector2 screenUV01)
    {
        if (_maskRT == null) return;

        EnsureWorkTexture();

        float aspect = (float)Screen.width / Screen.height;
        if (Screen.height <= 0) aspect = 1f;

        bool useBrush = _brushSprite != null && _brushSprite.texture != null;
        if (useBrush) EnsureBrushCache();

        float sizeEps = _brushSize + _soft + 0.01f;
        float halfW = sizeEps * aspect * 0.5f;
        float halfH = sizeEps * 0.5f;
        int pxMin = Mathf.Clamp(Mathf.FloorToInt((screenUV01.x - halfW) * _maskWidth), 0, _maskWidth - 1);
        int pyMin = Mathf.Clamp(Mathf.FloorToInt((screenUV01.y - halfH) * _maskHeight), 0, _maskHeight - 1);
        int pxMax = Mathf.Clamp(Mathf.CeilToInt((screenUV01.x + halfW) * _maskWidth), 0, _maskWidth);
        int pyMax = Mathf.Clamp(Mathf.CeilToInt((screenUV01.y + halfH) * _maskHeight), 0, _maskHeight);

        var prev = RenderTexture.active;
        RenderTexture.active = _maskRT;
        _workTexture.ReadPixels(new Rect(0, 0, _maskWidth, _maskHeight), 0, 0);
        RenderTexture.active = prev;

        NativeArray<byte> rawData = _workTexture.GetRawTextureData<byte>();

        for (int y = pyMin; y < pyMax; y++)
        {
            float v = (y + 0.5f) / _maskHeight;
            for (int x = pxMin; x < pxMax; x++)
            {
                float u = (x + 0.5f) / _maskWidth;
                float brushAlpha = ComputeBrushAlpha(u, v, screenUV01, _brushSize, _soft, aspect, useBrush, _brushCache);
                int idx = y * _maskWidth + x;
                byte current = rawData[idx];
                byte newVal = (byte)Mathf.Max(current, (byte)(brushAlpha * 255f));
                rawData[idx] = newVal;
            }
        }

        _workTexture.Apply();
        Graphics.Blit(_workTexture, _maskRT);
        ApplyMaskToGraphic();
    }

    private static float ComputeBrushAlpha(float u, float v, Vector2 center, float size, float soft, float aspect,
        bool useBrush, byte[] brushCache)
    {
        if (useBrush && brushCache != null && brushCache.Length == BrushCacheSize * BrushCacheSize)
        {
            float dx = (u - center.x) * aspect;
            float dy = v - center.y;
            float sizeEps = size + 1e-6f;
            float bx = dx / sizeEps + 0.5f;
            float by = dy / sizeEps + 0.5f;
            if (bx < 0f || bx > 1f || by < 0f || by > 1f) return 0f;

            float fx = bx * (BrushCacheSize - 1);
            float fy = by * (BrushCacheSize - 1);
            int ix = (int)fx; int iy = (int)fy;
            int ix1 = Mathf.Min(ix + 1, BrushCacheSize - 1);
            int iy1 = Mathf.Min(iy + 1, BrushCacheSize - 1);
            float tx = fx - ix; float ty = fy - iy;
            float a00 = brushCache[iy * BrushCacheSize + ix] / 255f;
            float a10 = brushCache[iy * BrushCacheSize + ix1] / 255f;
            float a01 = brushCache[iy1 * BrushCacheSize + ix] / 255f;
            float a11 = brushCache[iy1 * BrushCacheSize + ix1] / 255f;
            float a0 = a00 * (1f - tx) + a10 * tx;
            float a1 = a01 * (1f - tx) + a11 * tx;
            return a0 * (1f - ty) + a1 * ty;
        }

        float uAspect = u * aspect;
        float vAspect = v;
        float cx = center.x * aspect;
        float cy = center.y;
        float dist = Mathf.Sqrt((uAspect - cx) * (uAspect - cx) + (vAspect - cy) * (vAspect - cy));
        float t = Mathf.Clamp01((dist - (size - soft)) / (soft * 2f + 1e-6f));
        return 1f - t * t * (3f - 2f * t);
    }

    /// <summary>
    /// 清空 mask（全遮罩，无镂空）。
    /// </summary>
    public void ClearMask()
    {
        EnsureMaskRT();
        
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = _maskRT;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
        
        // Also clear work texture data if it exists to keep them in sync
        if (_workTexture != null)
        {
            NativeArray<byte> rawData = _workTexture.GetRawTextureData<byte>();
            if (rawData.IsCreated)
            {
                // Zero out
                // NativeArray doesn't have Clear? use Loop or MemClear
                // UnsafeUtility.MemClear(rawData.GetUnsafePtr(), rawData.Length);
                // Safe way:
                for (int i = 0; i < rawData.Length; i++) rawData[i] = 0;
                _workTexture.Apply();
            }
        }
        
        ApplyMaskToGraphic();
    }

    /// <summary>
    /// 写入 circle 到 mask。与 ClickTransform 的 circle 一致（aspect 校正、smoothstep）。
    /// </summary>
    /// <param name="screenUV01">圆心，屏幕 UV 0-1，左下 (0,0) 右上 (1,1)</param>
    /// <param name="radius">半径，屏幕 UV 比例</param>
    public void WriteCircleToMask(Vector2 screenUV01, float radius)
    {
        WriteCircleToMask(screenUV01, radius, _soft);
    }

    /// <summary>
    /// 写入 circle 到 mask，可指定边缘柔化。
    /// </summary>
    public void WriteCircleToMask(Vector2 screenUV01, float radius, float soft)
    {
        if (_maskRT == null) return;

        EnsureWorkTexture();

        var prev = RenderTexture.active;
        RenderTexture.active = _maskRT;
        _workTexture.ReadPixels(new Rect(0, 0, _maskWidth, _maskHeight), 0, 0);
        RenderTexture.active = prev;

        NativeArray<byte> rawData = _workTexture.GetRawTextureData<byte>();
        float aspect = (float)Screen.width / Screen.height;
        if (Screen.height <= 0) aspect = 1f;

        float halfR = radius + soft + 0.01f;
        float halfRU = halfR / Mathf.Max(aspect, 0.001f);
        int pxMin = Mathf.Clamp(Mathf.FloorToInt((screenUV01.x - halfRU) * _maskWidth), 0, _maskWidth - 1);
        int pyMin = Mathf.Clamp(Mathf.FloorToInt((screenUV01.y - halfR) * _maskHeight), 0, _maskHeight - 1);
        int pxMax = Mathf.Clamp(Mathf.CeilToInt((screenUV01.x + halfRU) * _maskWidth), 0, _maskWidth);
        int pyMax = Mathf.Clamp(Mathf.CeilToInt((screenUV01.y + halfR) * _maskHeight), 0, _maskHeight);

        for (int y = pyMin; y < pyMax; y++)
        {
            float v = (y + 0.5f) / _maskHeight;
            for (int x = pxMin; x < pxMax; x++)
            {
                float u = (x + 0.5f) / _maskWidth;
                float brushAlpha = ComputeBrushAlpha(u, v, screenUV01, radius, soft, aspect, false, null);
                int idx = y * _maskWidth + x;
                byte current = rawData[idx];
                byte newVal = (byte)Mathf.Max(current, (byte)(brushAlpha * 255f));
                rawData[idx] = newVal;
            }
        }

        _workTexture.Apply();
        Graphics.Blit(_workTexture, _maskRT);
        ApplyMaskToGraphic();
    }

    /// <summary>
    /// 从屏幕像素坐标写入 circle。
    /// </summary>
    public void WriteCircleFromScreenPixel(float pixelX, float pixelY, float radius)
    {
        float w = Screen.width;
        float h = Screen.height;
        if (w <= 0f || h <= 0f) return;
        WriteCircleToMask(new Vector2(pixelX / w, pixelY / h), radius);
    }

    private void ApplyMaskToGraphic()
    {
        if (_maskGraphic == null || _displayMaterial == null || _maskRT == null) return;
        if (_maskTexApplied && _displayMaterial.GetTexture(MaskTexId) == _maskRT) return;

        _displayMaterial.SetTexture(MaskTexId, _maskRT);
        _maskGraphic.SetMaterialDirty();
        _maskTexApplied = true;
    }

    /// <summary>
    /// 设置使用 mask 的 Graphic。
    /// </summary>
    public void SetMaskGraphic(Graphic graphic)
    {
        if (_maskGraphic == graphic) return;

        if (_maskGraphic != null && _maskGraphic.material == _displayMaterial)
            _maskGraphic.material = null;

        _maskGraphic = graphic;
        _displayMaterial = null;
        EnsureMaterials();
        ApplyMaskToGraphic();
    }

    public RenderTexture MaskRT => _maskRT;
}
