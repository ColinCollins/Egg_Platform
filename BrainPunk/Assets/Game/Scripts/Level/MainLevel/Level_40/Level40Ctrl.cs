using Bear.EventSystem;
using Game.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level40 RenderTexture 尺寸控制器。
/// 运行时根据屏幕分辨率创建/更新 RT，并绑定到 Camera 和 RawImage。
/// </summary>
public class Level40Ctrl : MonoBehaviour
{
    [Header("RT Target")]
    private RenderTexture templateRenderTexture;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera targetCamera;
    private RawImage targetRawImage;
    [SerializeField] private Material pageMaterial;

    [SerializeField] private BoxCollider2D door;
    [SerializeField] private GameObject turntable;

    [Header("Runtime")]
    [SerializeField] private bool updateWhenResolutionChanged = true;

    private RenderTexture _runtimeRenderTexture;
    private RenderTexture _backCaptureTexture;
    private Vector2Int _lastSize = new Vector2Int(-1, -1);
    private RenderTexture _cachedCameraTargetTexture;
    private Texture _cachedRawImageTexture;
    private int _cachedCullingMask;
    private int _cachedScreenWidth;
    private int _cachedScreenHeight;
    private EventSubscriber _subscriber;

    private static readonly int BackTexPropId = Shader.PropertyToID("_BackTex");
    private static readonly int EnableKeyPropId = Shader.PropertyToID("_EnableKey");

    private void Start()
    {
        AddListener();
        TryResolveRawImageFromGamePlayPanel();

        _cachedCameraTargetTexture = targetCamera != null ? targetCamera.targetTexture : null;
        _cachedCullingMask = targetCamera != null ? targetCamera.cullingMask : 0;
        _cachedRawImageTexture = targetRawImage != null ? targetRawImage.texture : null;

        ApplyRenderTexture(force: true);
        CaptureBackTextureAndApplyMask();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<OnTiggerItemEvent>(OnTriggerItem);
    }

    private void OnTriggerItem(OnTiggerItemEvent @event)
    {
        Debug.Log("----------" + @event.EventId);
        if (@event.EventId != 1)
            return;

        if (targetCamera != null)
            targetCamera.gameObject.SetActive(false);

        door.enabled = true;
        turntable.SetActive(false);


        int excludeMask = LayerMask.GetMask("NonCollisionRigibodyLayer");
        mainCamera.cullingMask = Physics2D.AllLayers & ~excludeMask;
    }

    private void LateUpdate()
    {
        if (!updateWhenResolutionChanged)
            return;

        if (Screen.width == _cachedScreenWidth && Screen.height == _cachedScreenHeight)
            return;

        ApplyRenderTexture(force: false);
    }

    private void ApplyRenderTexture(bool force)
    {
        var size = GetTargetSize();
        _cachedScreenWidth = Screen.width;
        _cachedScreenHeight = Screen.height;

        if (!force && size == _lastSize)
            return;

        _lastSize = size;
        RebuildRuntimeRenderTexture(size.x, size.y);
    }

    private Vector2Int GetTargetSize()
    {
        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        return new Vector2Int(width, height);
    }

    private void RebuildRuntimeRenderTexture(int width, int height)
    {
        ReleaseRuntimeRenderTexture();

        if (templateRenderTexture != null)
        {
            var descriptor = templateRenderTexture.descriptor;
            descriptor.width = width;
            descriptor.height = height;
            _runtimeRenderTexture = new RenderTexture(descriptor);
        }
        else
        {
            _runtimeRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        }

        _runtimeRenderTexture.name = $"Level40_RT_{width}x{height}";
        _runtimeRenderTexture.Create();

        if (targetCamera != null)
            targetCamera.targetTexture = _runtimeRenderTexture;

        // UI 可能在 Awake 后才完成初始化，重建时再尝试一次查找并绑定。
        TryResolveRawImageFromGamePlayPanel();
        if (targetRawImage != null)
        {
            targetRawImage.texture = _runtimeRenderTexture;
        }
    }

    private void CaptureBackTextureAndApplyMask()
    {
        if (targetCamera == null || pageMaterial == null)
            return;

        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        ReleaseBackCaptureTexture();
        _backCaptureTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        _backCaptureTexture.name = $"Level40_BackTex_{width}x{height}";
        _backCaptureTexture.Create();

        // 1) 仅渲染 CommonBg，作为背面纹理。
        var oldTarget = targetCamera.targetTexture;
        int oldMask = targetCamera.cullingMask;
        int commonBgLayer = LayerMask.NameToLayer("CommonBg");
        if (commonBgLayer >= 0)
            targetCamera.cullingMask = 1 << commonBgLayer;
        targetCamera.targetTexture = _backCaptureTexture;
        targetCamera.Render();
        pageMaterial.SetTexture(BackTexPropId, _backCaptureTexture);
        pageMaterial.SetFloat(EnableKeyPropId, 1f);

        // 2) 恢复 targetTexture，并剔除 Opendoor + Stuff 图层。
        targetCamera.targetTexture = oldTarget;
        ApplyRuntimeCullingMask();
    }

    private void ApplyRuntimeCullingMask()
    {
        if (targetCamera == null)
            return;

        int excludeMask = LayerMask.GetMask("OpenDoor", "Stuff");
        targetCamera.cullingMask = Physics2D.AllLayers & ~excludeMask;

        Debug.Log($"[Level40Ctrl] ApplyRuntimeCullingMask ->excludeMask:{excludeMask}, finalMask:{targetCamera.cullingMask}");
    }

    private void TryResolveRawImageFromGamePlayPanel()
    {
        if (targetRawImage != null)
            return;

        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
            return;

        // 参考 Level34Ctrl：优先从 GamePlayPanel 第一个子节点作为 panelRoot 开始查找
        var panelRoot = PlayCtrl.Instance.CurrentGamePlayPanel.transform;
        if (panelRoot.childCount > 0)
            panelRoot = panelRoot.GetChild(0);

        var rawImageRect = panelRoot.Find("RawImage") as RectTransform;
        if (rawImageRect != null)
        {
            targetRawImage = rawImageRect.GetComponent<RawImage>();
            if (targetRawImage != null)
                return;
        }

        // fallback: 名字匹配的递归查找
        var rawImages = panelRoot.GetComponentsInChildren<RawImage>(true);
        for (int i = 0; i < rawImages.Length; i++)
        {
            if (rawImages[i] != null && rawImages[i].name == "RawImage")
            {
                targetRawImage = rawImages[i];
                return;
            }
        }
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);

        if (targetCamera != null)
        {
            targetCamera.targetTexture = _cachedCameraTargetTexture;
            targetCamera.cullingMask = _cachedCullingMask;
        }

        if (targetRawImage != null)
            targetRawImage.texture = _cachedRawImageTexture;

        ReleaseRuntimeRenderTexture();
        ReleaseBackCaptureTexture();
    }

    private void ReleaseRuntimeRenderTexture()
    {
        if (_runtimeRenderTexture == null)
            return;

        if (_runtimeRenderTexture.IsCreated())
            _runtimeRenderTexture.Release();

        Destroy(_runtimeRenderTexture);
        _runtimeRenderTexture = null;
    }

    private void ReleaseBackCaptureTexture()
    {
        if (_backCaptureTexture == null)
            return;

        if (_backCaptureTexture.IsCreated())
            _backCaptureTexture.Release();

        Destroy(_backCaptureTexture);
        _backCaptureTexture = null;
    }
}

