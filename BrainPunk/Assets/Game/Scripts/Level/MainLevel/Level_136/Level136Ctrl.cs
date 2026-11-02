using System.Collections;
using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using Game.Play;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Level136 私有入口：配置 2986 密码，并管理右上三角折页的生命周期。</summary>
[DisallowMultipleComponent]
public sealed class Level136Ctrl : MonoBehaviour
{
    #region 关卡引用与状态
    /// <summary>场景中的正面 29；位置、旋转、大小由设计者摆放，运行时只读，不改 Transform。</summary>
    [SerializeField, Tooltip("拖入场景中的 FrontDigits_29；直接调整该对象的位置、旋转和缩放。")]
    private SpriteRenderer frontDigits29;
    /// <summary>印在折回纸背上的残缺 986；随纸背网格变换，不单独移动。</summary>
    [SerializeField] private Sprite foldDigits986;
    /// <summary>本关私有纸张外观；不修改共享材质或其它关卡。</summary>
    [SerializeField] private Level136PaperAppearance paperAppearance = new Level136PaperAppearance();
    /// <summary>供本关网格读取 Inspector 外观配置，不创建资源。</summary>
    public Level136PaperAppearance PaperAppearance => paperAppearance;
    private EventSubscriber _subscriber;
    private Level6GamePanelFeature _panelFeature;
    private Coroutine _setup;
    private GameObject _foldRoot;
    private Level136PaperFold _fold;
    private bool _appearanceDirty;
    private bool _passwordSolved;
    #endregion

    #region 运行时生命周期
    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;
        _passwordSolved = false;
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<OnTiggerItemEvent>(OnTiggerItem);
        BuildFold();
        _setup = StartCoroutine(ConfigurePassword());
    }

    private void OnDisable()
    {
        if (_setup != null)
            StopCoroutine(_setup);
        _setup = null;
        EventsUtils.ResetEvents(ref _subscriber);
        _panelFeature = null;
        if (_foldRoot != null)
        {
            _foldRoot.SetActive(false);
            Destroy(_foldRoot);
        }
        _foldRoot = null;
        _fold = null;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;
        // Inspector 只记录刷新请求，真正的 UGUI 更新留在运行时主循环。
        _appearanceDirty = true;
    }

    private void Update()
    {
        if (!Application.isPlaying || !_appearanceDirty || _fold == null)
            return;
        _appearanceDirty = false;
        _fold.RefreshAppearance();
    }

    private void BuildFold()
    {
        if (frontDigits29 == null || frontDigits29.sprite == null || foldDigits986 == null)
        {
            Debug.LogWarning("[Level136] 缺少纸面 29 或纸背 986 素材。", this);
            return;
        }
        _foldRoot = new GameObject("Level136_TrianglePaperFold", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RectMask2D));
        _foldRoot.layer = LayerMask.NameToLayer("UI");
        _foldRoot.transform.SetParent(transform, false);
        Canvas canvas = _foldRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -25;
        CanvasScaler scaler = _foldRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _fold = _foldRoot.AddComponent<Level136PaperFold>();
        _fold.Initialize(frontDigits29, foldDigits986, this, CanDrag);
    }

    private bool CanDrag()
    {
        return Application.isPlaying && isActiveAndEnabled && !_passwordSolved
            && PlayCtrl.Instance != null && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }
    #endregion

    #region 密码入口
    private IEnumerator ConfigurePassword()
    {
        // PlayCtrl 在关卡实例化后替换 HUD；下一帧再取面板，避免配置到旧关卡。
        yield return null;
        while (isActiveAndEnabled
            && (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null))
            yield return null;
        _setup = null;
        if (!isActiveAndEnabled || PlayCtrl.Instance == null)
            yield break;
        _panelFeature = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<Level6GamePanelFeature>();
        if (_panelFeature == null)
        {
            Debug.LogWarning("[Level136] 当前 HUD 缺少 Level6GamePanelFeature。", this);
            yield break;
        }
        _panelFeature.ConfigurePasswordPopup("PasswordPopup_136_2986");
        ApplyPasswordEntryState();
    }

    private void OnTiggerItem(OnTiggerItemEvent evt)
    {
        if (evt == null || evt.EventId != 1 || _passwordSolved)
            return;
        _passwordSolved = true;
        ApplyPasswordEntryState();
        if (_foldRoot != null)
            _foldRoot.SetActive(false);
    }

    private void ApplyPasswordEntryState()
    {
        CustomButton button = _panelFeature != null ? _panelFeature.Use_btn : null;
        if (button == null)
            return;
        button.IsBlockClick = _passwordSolved;
        button.Interactable = !_passwordSolved;
        if (_passwordSolved)
            button.gameObject.SetActive(false);
    }
    #endregion
}

/// <summary>仅 Level136 使用的可序列化纸张参数，线条尺寸以 1080 高画面为基准。</summary>
[System.Serializable]
public sealed class Level136PaperAppearance
{
    #region 颜色与横线
    /// <summary>翻回来的纸背颜色。</summary>
    [Tooltip("翻回来的纸背颜色")]
    public Color foldColor = new Color(201f / 255f, 210f / 255f, 214f / 255f, 1f);
    /// <summary>翻开后露出的底页颜色。</summary>
    [Tooltip("翻开后露出的底页颜色")]
    public Color underColor = new Color(0.952f, 0.945f, 0.926f, 1f);
    /// <summary>底页笔记本横线颜色，透明度控制深浅。</summary>
    [Tooltip("底页横线颜色，Alpha 控制深浅")]
    public Color lineColor = new Color(0.48f, 0.48f, 0.45f, 0.16f);
    /// <summary>横线间距；以 1080 高设计画面为基准。</summary>
    [Min(12f), Tooltip("1080 高画面下的横线间距")]
    public float lineSpacing = 74f;
    /// <summary>横线粗细；以 1080 高设计画面为基准。</summary>
    [Min(0.1f), Tooltip("1080 高画面下的横线粗细")]
    public float lineWidth = 2f;
    /// <summary>第一条横线距页顶的偏移，可用来对齐背景中的横线。</summary>
    [Tooltip("第一条横线距页顶的偏移；用于对齐场景背景")]
    public float lineOffset = 34f;
    #endregion
}
