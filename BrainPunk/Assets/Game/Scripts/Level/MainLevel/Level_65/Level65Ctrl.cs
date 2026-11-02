using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;

/// <summary>
/// 参考 Level63Ctrl，控制 passwordPopup 开启逻辑，passwordPopup Prefab 名称为。PasswordPopup_65_3
/// 控制一个语言气泡 bubble 进入场景是进行 Scale 缩放 0 ->1，Ease outback。
/// /// alienContent 内容逐字显示
/// 当玩家选中 alienLanguageItem 时， alienContent，内容更新为 string: 3315
/// 
/// LocalizationPopup 增加 OnPopupCreated 事件与 AddItem/CancelAllItem/RemoveCustomItem API。
/// AddItem 传入 LanguageItem，进入到当前 group 中 clickEvent 自定义。
/// CancelAllItem，取消所有 languageItem 的选中状态，但只是表现上的取消，并不修改真实数据。
/// 当系统正常打开 LocalizationPopup 时，如果 Level65 正在激活，讲 alienLanguageItem （prefab） 插入 btnGroup 中。
///
///
/// version 2.0 补充需求
/// alien bubble 默认 scale = 0，新增一个 click api 点击 alien 之后 alienBubble scale 从 0 -> 1
/// /// 
/// </summary>
public class Level65Ctrl : MonoBehaviour, IEventSender
{
    private const string PasswordPopupName = "PasswordPopup_65_3";
    private const string AlienCode = "3315";

    [Header("Alien Language")]
    [SerializeField] private LanguageItem alienLanguageItem;

    [SerializeField] private GameObject alienContentImg;
    [SerializeField] private TextMeshPro alienContent;
    [SerializeField] private Transform alienBubbleRoot;

    [Header("Presentation")]
    [SerializeField] private float bubbleScaleDuration = 0.35f;
    [SerializeField] private float textRevealDelay = 0.08f;
    [SerializeField] private float textRevealDuration = 0.8f;

    public CustomButton Use_btn;

    private EventSubscriber _subscriber;
    private Tween _bubbleTween;
    private Tween _textTween;
    private Tween _useButtonHideTween;
    private LocalizationPopup _activeLocalizationPopup;
    private LanguageItem _activeAlienLanguageItemView;
    private CustomButton _fallbackUseButton;
    private Vector3 _bubbleDefaultScale = Vector3.one;
    private string _initialAlienContent;
    private bool _hasStarted;
    private bool _useButtonSubscribed;
    private bool _useButtonLocked;
    private bool _isBubbleVisible;
    private bool _isBubbleRevealing;

    private bool hasSwitchAlienLanguage = false;

    public EventSubscriber Subscriber => _subscriber;

    void Awake()
    {
        _fallbackUseButton = Use_btn;

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
        _subscriber.Subscribe<SwitchLanguageEvent>(OnSwitchLanguage);
        _subscriber.Subscribe<SwitchObjActiveEvent>(OnSwitchObj);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        _subscriber.Subscribe<OnTiggerItemEvent>(OnTiggerItem);

        CachePresentationDefaults();
        ResetAlienPresentationState();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _hasStarted = true;
        RegisterUseButton();
        ResetAlienPresentationState();
    }

    void OnEnable()
    {
        LocalizationPopup.OnPopupCreated += OnLocalizationPopupCreated;

        if (!_hasStarted)
            return;

        RegisterUseButton();
        ResetAlienPresentationState();
    }

    public void ShowPasswordPopup()
    {
        try
        {
            PasswordPopup.Create(PasswordPopupName);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Level65Ctrl] Failed to open {PasswordPopupName}: {exception.Message}");
        }
    }

    public void OnClickAlien(Transform clicker, Vector2 worldPoint)
    {
        if (_isBubbleVisible || _isBubbleRevealing)
            return;

        RevealAlienBubble(false);
        AudioManager.PlaySound("level65Alien");
    }

    private void OnClickAlienLanguageItem(LanguageItem itemView)
    {
        if (_activeLocalizationPopup != null)
            _activeLocalizationPopup.CancelAllItem();


        hasSwitchAlienLanguage = true;
        SetAlienContent(true);
        RevealAlienBubble(true);

        _activeAlienLanguageItemView = itemView;
        RefreshAlienLanguageItemState();
    }

    private void OnShowPasswordPopup(CustomButton btn)
    {
        ShowPasswordPopup();
    }

    private void OnLocalizationPopupCreated(LocalizationPopup popup)
    {
        if (!isActiveAndEnabled || popup == null)
            return;

        if (_activeLocalizationPopup != null && _activeLocalizationPopup != popup)
            _activeLocalizationPopup.RemoveCustomItem();

        _activeLocalizationPopup = popup;

        if (alienLanguageItem == null)
        {
            Debug.LogWarning("[Level65Ctrl] Alien language item prefab is not assigned.");
            return;
        }

        _activeAlienLanguageItemView = popup.AddItem(alienLanguageItem, OnClickAlienLanguageItem);
        RefreshAlienLanguageItemState();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        _useButtonLocked = false;
        CleanupPopupSession();
        ResetAlienPresentationState();
        RegisterUseButton();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        if (evt.NewState == GamePlayStateName.SUCCESS || evt.NewState == GamePlayStateName.FAILED)
        {
            CleanupPopupSession();
            ResetAlienPresentationState();
        }
    }

    private void OnSwitchObj(SwitchObjActiveEvent evt)
    {
        if (_useButtonLocked)
        {
            QueueHideUseButton();
            return;
        }

        if (Use_btn == null)
            RegisterUseButton(false);
    }

    private void OnTiggerItem(OnTiggerItemEvent evt)
    {
        if (evt == null || evt.EventId != 1)
            return;

        _useButtonLocked = true;
        QueueHideUseButton();
    }

    private void OnSwitchLanguage(SwitchLanguageEvent evt)
    {
        KillPresentationTweens();
        CompleteInterruptedBubbleReveal();

        hasSwitchAlienLanguage = false;
        SetAlienContent(true);

        RefreshAlienLanguageItemState();
    }

    private void CompleteInterruptedBubbleReveal()
    {
        if (!_isBubbleRevealing)
            return;

        _isBubbleRevealing = false;
        _isBubbleVisible = true;

        if (alienBubbleRoot != null)
            alienBubbleRoot.localScale = _bubbleDefaultScale;
    }

    private void RevealAlienBubble(bool showCurrentContentImmediately)
    {
        if (_isBubbleVisible && !_isBubbleRevealing)
        {
            if (showCurrentContentImmediately)
                SetAlienContent(true);
            return;
        }

        KillPresentationTweens();
        _isBubbleRevealing = true;

        if (alienBubbleRoot != null)
        {
            alienBubbleRoot.localScale = Vector3.zero;
            _bubbleTween = alienBubbleRoot
                .DOScale(_bubbleDefaultScale, Mathf.Max(0f, bubbleScaleDuration))
                .SetEase(Ease.OutBack)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    _bubbleTween = null;
                    _isBubbleVisible = true;
                    _isBubbleRevealing = false;
                });
        }
        else
        {
            _isBubbleVisible = true;
            _isBubbleRevealing = false;
        }

        SetAlienContent(showCurrentContentImmediately);

        if (showCurrentContentImmediately)
            return;

        if (alienContent == null)
            return;

        if (!alienContent.gameObject.activeInHierarchy)
            return;

        alienContent.maxVisibleCharacters = 0;
        alienContent.ForceMeshUpdate();
        int characterCount = Mathf.Max(0, alienContent.textInfo.characterCount);
        if (characterCount <= 0)
        {
            alienContent.maxVisibleCharacters = int.MaxValue;
            return;
        }

        _textTween = DOTween.To(
                () => alienContent != null ? alienContent.maxVisibleCharacters : 0,
                value =>
                {
                    if (alienContent != null)
                        alienContent.maxVisibleCharacters = value;
                },
                characterCount,
                Mathf.Max(0f, textRevealDuration))
            .SetEase(Ease.Linear)
            .SetDelay(Mathf.Max(0f, textRevealDelay))
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (alienContent != null)
                    alienContent.maxVisibleCharacters = int.MaxValue;
                _textTween = null;
            });
    }

    private void CachePresentationDefaults()
    {
        if (alienBubbleRoot == null && alienContent != null)
            alienBubbleRoot = alienContent.transform.parent;

        if (alienBubbleRoot != null)
        {
            _bubbleDefaultScale = alienBubbleRoot.localScale;
            if (_bubbleDefaultScale == Vector3.zero)
                _bubbleDefaultScale = Vector3.one;
        }

        _initialAlienContent = alienContent != null ? alienContent.text : string.Empty;
    }

    private void ResetAlienPresentationState()
    {
        KillPresentationTweens();

        _isBubbleVisible = false;
        _isBubbleRevealing = false;

        if (alienBubbleRoot != null)
            alienBubbleRoot.localScale = Vector3.zero;


        hasSwitchAlienLanguage = false;
        SetAlienContent(false);
    }

    private void SetAlienContent(bool showImmediately)
    {
        if (alienContent == null)
        {
            Debug.LogWarning("[Level65Ctrl] Alien content is not assigned.");
            if (alienContentImg != null)
                alienContentImg.SetActive(!hasSwitchAlienLanguage);
            return;
        }


        if (hasSwitchAlienLanguage)
        {
            alienContent.gameObject.SetActive(true);
            if (alienContentImg != null)
                alienContentImg.SetActive(false);
            // alienContent.text = AlienCode;

            alienContent.UpdateFontAsset();
            alienContent.ForceMeshUpdate();
            alienContent.maxVisibleCharacters = showImmediately ? int.MaxValue : 0;
        }
        else
        {
            alienContent.text = _initialAlienContent ?? string.Empty;
            alienContent.ForceMeshUpdate();

            bool hasImage = alienContentImg != null;
            alienContent.maxVisibleCharacters = showImmediately || hasImage ? int.MaxValue : 0;
            alienContent.gameObject.SetActive(!hasImage);
            if (alienContentImg != null)
                alienContentImg.SetActive(true);
        }
    }

    private void KillPresentationTweens()
    {
        if (_bubbleTween != null)
        {
            _bubbleTween.Kill(false);
            _bubbleTween = null;
        }

        if (_textTween != null)
        {
            _textTween.Kill(false);
            _textTween = null;
        }
    }

    private void CleanupPopupSession()
    {
        if (_activeLocalizationPopup == null)
            return;

        _activeLocalizationPopup.RemoveCustomItem();
        _activeLocalizationPopup = null;
        _activeAlienLanguageItemView = null;
    }

    private void RefreshAlienLanguageItemState()
    {
        if (_activeAlienLanguageItemView == null)
            return;

        if (hasSwitchAlienLanguage)
        {
            if (_activeLocalizationPopup != null)
                _activeLocalizationPopup.CancelAllItem();
        }
        _activeAlienLanguageItemView.SetSelected(hasSwitchAlienLanguage);
    }

    private void SubscribeUseButton()
    {
        if (Use_btn == null || _useButtonSubscribed)
            return;

        Use_btn.OnClick += OnShowPasswordPopup;
        _useButtonSubscribed = true;
    }

    private void UnsubscribeUseButton()
    {
        if (!_useButtonSubscribed)
            return;

        if (Use_btn != null)
            Use_btn.OnClick -= OnShowPasswordPopup;

        _useButtonSubscribed = false;
    }

    private void RegisterUseButton(bool hideButton = true)
    {
        UnsubscribeUseButton();
        Use_btn = ResolveUseButton();

        if (Use_btn != null)
            Use_btn.Interactable = !_useButtonLocked;

        if (_useButtonLocked)
        {
            QueueHideUseButton();
            return;
        }

        if (hideButton)
            Use_btn?.gameObject.SetActive(false);

        SubscribeUseButton();
    }

    private void QueueHideUseButton()
    {
        HideUseButton();

        _useButtonHideTween?.Kill(false);
        _useButtonHideTween = DOVirtual.DelayedCall(0f, HideUseButton)
            .SetLink(gameObject)
            .OnComplete(() => _useButtonHideTween = null);
    }

    private void HideUseButton()
    {
        if (Use_btn == null)
            Use_btn = ResolveUseButton();

        if (Use_btn == null)
            return;

        Use_btn.Interactable = false;
        Use_btn.transform.DOKill();
        Use_btn.transform.localScale = Vector3.one;
        Use_btn.transform.DOScale(Vector3.zero, 0.1f)
            .SetLink(Use_btn.gameObject)
            .OnComplete(() =>
            {
                if (Use_btn != null)
                    Use_btn.gameObject.SetActive(false);
            });
    }

    private CustomButton ResolveUseButton()
    {
        if (PlayCtrl.Instance != null && PlayCtrl.Instance.CurrentGamePlayPanel != null)
        {
            GamePanel_UseBtnCommonFeature gamePanelUseFeature =
                PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<GamePanel_UseBtnCommonFeature>();
            if (gamePanelUseFeature != null && gamePanelUseFeature.Use_btn != null)
                return gamePanelUseFeature.Use_btn;
        }

        return _fallbackUseButton;
    }

    void OnDisable()
    {
        LocalizationPopup.OnPopupCreated -= OnLocalizationPopupCreated;
        CleanupPopupSession();
        ResetAlienPresentationState();
        UnsubscribeUseButton();
        _useButtonHideTween?.Kill(false);
        _useButtonHideTween = null;

        if (Use_btn != null)
            Use_btn.transform.DOKill();
    }

    void OnDestroy()
    {
        LocalizationPopup.OnPopupCreated -= OnLocalizationPopupCreated;
        CleanupPopupSession();
        ResetAlienPresentationState();
        UnsubscribeUseButton();
        _useButtonHideTween?.Kill(false);
        _useButtonHideTween = null;
        EventsUtils.ResetEvents(ref _subscriber);

        if (Use_btn != null)
            Use_btn.transform.DOKill();
    }
}
