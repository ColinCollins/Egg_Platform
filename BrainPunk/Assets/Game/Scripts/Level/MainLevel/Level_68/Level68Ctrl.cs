using Bear.EventSystem;
using Bear.Logger;
using DG.Tweening;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// 复用 Level63 的 Use button / password popup 入口。
/// Level68 v2.0.0: PasswordTips 只能在密码弹窗已打开时，拖到 TriggerArea 后触发自动输入。
/// </summary>
public class Level68Ctrl : MonoBehaviour, IDebuger
{
    private const string PreferredPasswordPopupName = "PasswordPopup_68_4";
    private const int PasswordSuccessEventId = 1;

    [SerializeField] private Canvas _worldCanvas;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private GameObject passwordTips;
    [SerializeField] private Collider2D triggerArea;
    [SerializeField] private Collider2D passwordBoard;

    public CustomButton Use_btn;

    private EventSubscriber _subscriber;
    private CustomButton _fallbackUseButton;
    private Canvas _fallbackWorldCanvas;
    private PasswordPopup _activePasswordPopup;
    private Level68PasswordCtrl passwordCtrl;
    private bool _hasStarted;
    private bool _useButtonSubscribed;
    private bool _useButtonLocked;
    private bool _autoPasswordRunning;
    private bool _autoPasswordSubmitted;
    private bool _passwordTipsConsumed;
    private Vector3 _tipsInitialLocalPosition;
    private Quaternion _tipsInitialLocalRotation;
    private Vector3 _tipsInitialLocalScale;
    private bool _tipsInitialActive;
    private bool _hasTipsInitialState;
    public EventSubscriber Subscriber => _subscriber;

    void Awake()
    {
        _fallbackUseButton = Use_btn;
        _fallbackWorldCanvas = _worldCanvas != null ? _worldCanvas : GetComponentInChildren<Canvas>(true);

        CacheTipsInitialState();
        ResolveTriggerArea();

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchObjActiveEvent>(OnSwitchObj);
        _subscriber.Subscribe<OnTiggerItemEvent>(OnTiggerItem);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
    }

    void Start()
    {
        _hasStarted = true;
        RegisterUseButton();
    }

    void OnEnable()
    {
        if (_hasStarted)
            RegisterUseButton();
    }

    private void OnSwitchObj(SwitchObjActiveEvent evt)
    {
        if (_useButtonLocked)
        {
            HideUseButton();
            return;
        }

        if (Use_btn == null)
            RegisterUseButton(false);

        if (Use_btn == null)
            return;

        var trans = Use_btn.transform;
        if (evt.isShow)
        {
            Use_btn.Interactable = true;
            Use_btn.gameObject.SetActive(true);

            trans.DOKill();
            trans.localScale = Vector3.one * 0.5f;
            trans.DOScale(Vector3.one, 0.1f).SetLink(Use_btn.gameObject);
        }
        else
        {
            trans.DOKill();
            trans.localScale = Vector3.one;
            trans.DOScale(Vector3.zero, 0.1f).SetLink(Use_btn.gameObject);
        }
    }

    private void OnTiggerItem(OnTiggerItemEvent evt)
    {
        if (evt == null || evt.EventId != PasswordSuccessEventId)
            return;

        if (_autoPasswordRunning || _autoPasswordSubmitted)
            CompleteTipsAutoPasswordSuccess();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        if (evt.NewState == GamePlayStateName.SUCCESS || evt.NewState == GamePlayStateName.FAILED)
            CancelAutoPassword(false);
    }

    private void OnGameReset(GameResetEvent evt)
    {
        ResetLevel68RuntimeState();
        DestroyActivePasswordPopup();
        RegisterUseButton();
    }

    private void OnShowPasswordPopup(CustomButton btn)
    {
        ShowPasswordPopup();
    }

    public void ShowPasswordPopup()
    {
        if (_passwordTipsConsumed)
            return;

        try
        {
            if (actor != null)
                actor.StopMoving();
            else
                this.LogWarning("[Level68Ctrl] Actor reference is missing when opening password popup.");

            if (_activePasswordPopup != null)
            {
                _activePasswordPopup.transform.SetAsLastSibling();
                _activePasswordPopup.OnOpen();
                _activePasswordPopup.OnShow();
                passwordCtrl = _activePasswordPopup.GetComponent<Level68PasswordCtrl>();
                return;
            }

            Canvas popupCanvas = ResolvePopupCanvas();
            if (popupCanvas == null)
            {
                this.LogWarning("[Level68Ctrl] Failed to open password popup: world canvas was not found.");
                ResumeActor();
                return;
            }

            string popupName = ResolvePasswordPopupName();
            if (string.IsNullOrEmpty(popupName))
            {
                this.LogWarning($"[Level68Ctrl] Failed to open password popup: {PreferredPasswordPopupName} was not found in Resources.");
                ResumeActor();
                return;
            }

            _activePasswordPopup = PasswordPopup.Create(popupName, popupCanvas.transform);
            if (_activePasswordPopup == null)
            {
                this.LogWarning($"[Level68Ctrl] Failed to create password popup {popupName}.");
                ResumeActor();
                return;
            }

            _activePasswordPopup.CancelPendingSuccessOnClose = false;
            passwordCtrl = _activePasswordPopup.GetComponent<Level68PasswordCtrl>();
            if (passwordCtrl == null)
                this.LogWarning("[Level68Ctrl] Level68PasswordCtrl is missing on PasswordPopup_68_4.");

            _activePasswordPopup.OnPopupCloseCallback += OnPasswordPopupClose;
        }
        catch (System.Exception exception)
        {
            this.LogError($"[Level68Ctrl] Failed to open password popup: {exception.Message}");
            ResumeActor();
        }
    }

    /// <summary>
    /// DragableItem.onDragEndEvent entry for PasswordTips.
    /// </summary>
    public void OnTipsEnterPasswordArea(Transform target)
    {
        if (!IsPasswordTipsTarget(target))
            return;

        if (_passwordTipsConsumed || _autoPasswordRunning || _autoPasswordSubmitted)
            return;

        if (_activePasswordPopup == null)
            return;

        if (!IsTargetInsideTriggerArea(target))
            return;

        if (passwordCtrl == null && _activePasswordPopup != null)
            passwordCtrl = _activePasswordPopup.GetComponent<Level68PasswordCtrl>();

        if (passwordCtrl == null)
        {
            this.LogWarning("[Level68Ctrl] PasswordTips drop ignored: Level68PasswordCtrl was not found.");
            return;
        }

        StartTipsAutoPassword();
    }

    private bool IsPasswordTipsTarget(Transform target)
    {
        if (target == null)
            return false;

        if (passwordTips == null)
        {
            this.LogWarning("[Level68Ctrl] PasswordTips reference is missing.");
            return false;
        }

        return target == passwordTips.transform || target.IsChildOf(passwordTips.transform);
    }

    private bool IsTargetInsideTriggerArea(Transform target)
    {
        Collider2D area = ResolveTriggerArea();
        if (area == null)
        {
            this.LogWarning("[Level68Ctrl] PasswordTips drop ignored: TriggerArea collider was not found.");
            return false;
        }

        Vector2 releasePoint = target.position;
        if (area.enabled && area.OverlapPoint(releasePoint))
            return true;

        return area.bounds.Contains(target.position);
    }

    private void StartTipsAutoPassword()
    {
        _autoPasswordRunning = true;
        _autoPasswordSubmitted = false;
        _activePasswordPopup.CancelPendingSuccessOnClose = true;
        SetTipsVisible(false, false);
        passwordCtrl.WaitingPass(OnAutoPasswordSubmitted);
    }

    private void OnAutoPasswordSubmitted()
    {
        _autoPasswordSubmitted = true;
    }

    private void CompleteTipsAutoPasswordSuccess()
    {
        _passwordTipsConsumed = true;
        _autoPasswordRunning = false;
        _autoPasswordSubmitted = false;
        if (_activePasswordPopup != null)
            _activePasswordPopup.CancelPendingSuccessOnClose = false;

        SetTipsVisible(false, false);
        LockUseButtonAfterSuccess();
    }

    private void CancelAutoPassword(bool restoreTips)
    {
        if (passwordCtrl != null)
            passwordCtrl.CancelWaitingPass(restoreTips);

        bool shouldRestoreTips = restoreTips && !_passwordTipsConsumed && (_autoPasswordRunning || _autoPasswordSubmitted);

        _autoPasswordRunning = false;
        _autoPasswordSubmitted = false;
        if (_activePasswordPopup != null)
            _activePasswordPopup.CancelPendingSuccessOnClose = false;

        if (shouldRestoreTips)
            SetTipsVisible(true, true);
    }

    private void ResetLevel68RuntimeState()
    {
        CancelAutoPassword(false);
        _passwordTipsConsumed = false;
        _useButtonLocked = false;
        RestoreTipsInitialState();
    }

    private void OnPasswordPopupClose()
    {
        bool shouldRestoreTips = !_passwordTipsConsumed && (_autoPasswordRunning || _autoPasswordSubmitted);
        CancelAutoPassword(shouldRestoreTips);

        if (_activePasswordPopup != null)
            _activePasswordPopup.OnPopupCloseCallback -= OnPasswordPopupClose;

        _activePasswordPopup = null;
        passwordCtrl = null;
        ResumeActor();
    }

    private Canvas ResolvePopupCanvas()
    {
        if (_worldCanvas != null)
            return _worldCanvas;

        if (_fallbackWorldCanvas != null)
            return _fallbackWorldCanvas;

        _fallbackWorldCanvas = GetComponentInChildren<Canvas>(true);
        return _fallbackWorldCanvas;
    }

    private string ResolvePasswordPopupName()
    {
        if (Resources.Load<GameObject>(PreferredPasswordPopupName) != null)
            return PreferredPasswordPopupName;

        return null;
    }

    private Collider2D ResolveTriggerArea()
    {
        if (triggerArea != null)
            return triggerArea;

        Transform trigger = transform.Find("TriggerArea");
        if (trigger == null)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].name == "TriggerArea")
                {
                    triggerArea = colliders[i];
                    return triggerArea;
                }
            }

            return null;
        }

        triggerArea = trigger.GetComponent<Collider2D>();
        return triggerArea;
    }

    private void CacheTipsInitialState()
    {
        if (passwordTips == null || _hasTipsInitialState)
            return;

        Transform tipsTransform = passwordTips.transform;
        _tipsInitialLocalPosition = tipsTransform.localPosition;
        _tipsInitialLocalRotation = tipsTransform.localRotation;
        _tipsInitialLocalScale = tipsTransform.localScale;
        _tipsInitialActive = passwordTips.activeSelf;
        _hasTipsInitialState = true;
    }

    private void RestoreTipsInitialState()
    {
        if (passwordTips == null)
            return;

        CacheTipsInitialState();

        if (_hasTipsInitialState)
        {
            Transform tipsTransform = passwordTips.transform;
            tipsTransform.localPosition = _tipsInitialLocalPosition;
            tipsTransform.localRotation = _tipsInitialLocalRotation;
            tipsTransform.localScale = _tipsInitialLocalScale;
            passwordTips.SetActive(_tipsInitialActive);
        }
        else
        {
            passwordTips.SetActive(true);
        }
    }

    private void SetTipsVisible(bool visible, bool restoreInitialTransform)
    {
        if (passwordTips == null)
        {
            this.LogWarning("[Level68Ctrl] PasswordTips reference is missing.");
            return;
        }

        if (restoreInitialTransform)
            RestoreTipsInitialState();

        passwordTips.SetActive(visible);
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
            HideUseButton();
            return;
        }

        if (hideButton)
            Use_btn?.gameObject.SetActive(false);

        SubscribeUseButton();
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

    private void LockUseButtonAfterSuccess()
    {
        _useButtonLocked = true;
        if (passwordBoard != null)
            passwordBoard.enabled = false;
        UnsubscribeUseButton();
        HideUseButton();
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

    private void ResumeActor()
    {
        if (actor != null)
            actor.ResumeMoving();
    }

    private void DestroyActivePasswordPopup()
    {
        if (_activePasswordPopup == null)
            return;

        _activePasswordPopup.OnPopupCloseCallback -= OnPasswordPopupClose;
        _activePasswordPopup.OnDestroyView();
        Destroy(_activePasswordPopup.gameObject);
        _activePasswordPopup = null;
        passwordCtrl = null;
    }

    void OnDisable()
    {
        CancelAutoPassword(false);
        DestroyActivePasswordPopup();
        UnsubscribeUseButton();

        if (Use_btn != null)
            Use_btn.transform.DOKill();
    }

    void OnDestroy()
    {
        CancelAutoPassword(false);
        DestroyActivePasswordPopup();
        UnsubscribeUseButton();
        EventsUtils.ResetEvents(ref _subscriber);

        if (Use_btn != null)
            Use_btn.transform.DOKill();
    }
}
