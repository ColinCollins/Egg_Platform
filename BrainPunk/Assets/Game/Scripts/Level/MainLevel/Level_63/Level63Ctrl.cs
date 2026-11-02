using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 提供一个 clickProjector 函数，用来切换一个 projector 的开关显示。这一关主要重在表现。
/// 打开动画有先后顺序。 Sp_l63_img_bg_02 投影光先出现。Sp_l63_img_bg_01 是 slice 投影幕布，
/// width 保持 0.1，height 从 0 到 5，然后 width 从 0.1 到 10。关闭时反向播放一次。
/// Sp_l63_img_bg_03 字体最后出现。
/// 写一个 sprite 的 像素故障表现 shader 给我。
///
/// version 2.0.0
/// 监听一下 OnTiggerItemEvent id = 1 时，触发解锁逻辑后，Use_Btn 隐藏不再需要交互
/// </summary>
public class Level63Ctrl : MonoBehaviour
{
    private enum ProjectorState
    {
        Off,
        Revealing,
        On,
        Closing
    }

    [Header("Projector")]
    [SerializeField] private Transform projectorParent;
    [SerializeField] private SpriteRenderer projectionLight;
    [SerializeField] private SpriteRenderer projectionCurtain;
    [SerializeField] private SpriteRenderer projectedText;
    [SerializeField] private float fullCurtainWidth = 10f;
    [SerializeField] private float curtainStartWidth = 0.1f;
    [SerializeField] private float lightHoldDuration = 0.08f;
    [SerializeField] private float curtainHeightRevealDuration = 0.2f;
    [SerializeField] private float curtainRevealDuration = 0.45f;
    [SerializeField] private float textRevealDuration = 0.12f;

    [Header("Pixel Glitch")]
    [SerializeField] private SpriteRenderer[] glitchTargets;
    [SerializeField] private Material glitchMaterial;

    private EventSubscriber _subscriber;
    private ProjectorState _projectorState = ProjectorState.Off;
    private Sequence _projectorSequence;
    private float _curtainHeight;
    private int _projectorVersion;
    private bool _useButtonSubscribed;
    private Material[] _glitchOriginalMaterials;
    private CustomButton _fallbackUseButton;
    private bool _hasStarted;
    private bool _useButtonLocked;

    private Tween _projectorLightFadeTween;

    public EventSubscriber Subscriber => _subscriber;

    public CustomButton Use_btn;

    void Awake()
    {
        _fallbackUseButton = Use_btn;

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchObjActiveEvent>(OnSwitchObj);
        _subscriber.Subscribe<OnTiggerItemEvent>(OnTiggerItem);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);

        CacheProjectorDefaults();
        CacheGlitchMaterials();
        ResetProjectorVisuals();
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

    private void OnTiggerItem(OnTiggerItemEvent evt)
    {
        if (evt == null || evt.EventId != 1)
            return;

        _useButtonLocked = true;
        UnsubscribeUseButton();
        HideUseButton();
    }

    private void OnSwitchObj(SwitchObjActiveEvent evt)
    {
        if (_useButtonLocked)
        {
            HideUseButton();
            return;
        }

        if (Use_btn == null)
            RegisterUseButton();

        if (Use_btn == null)
            return;

        var trans = Use_btn.transform;
        if (evt.isShow)
        {
            Use_btn.gameObject.SetActive(evt.isShow);

            trans.DOKill();
            trans.localScale = Vector3.one * 0.5f;
            trans.DOScale(Vector3.one, 0.1f);
        }
        else
        {
            trans.DOKill();
            trans.localScale = Vector3.one;
            trans.DOScale(Vector3.zero, 0.1f);
        }
    }

    private void OnShowPasswordPopup(CustomButton btn)
    {
        try
        {
            PasswordPopup.Create("PasswordPopup_63_2");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Level63Ctrl] Failed to open PasswordPopup_63_2: {exception.Message}");
        }
    }

    private void OnGameReset(GameResetEvent evt)
    {
        _useButtonLocked = false;
        _projectorVersion++;
        ResetProjectorVisuals();
        RegisterUseButton();
    }

    public void ClickProjector()
    {
        ToggleProjector();
    }

    public void ClickProjector(Transform clicker, Vector2 worldPoint)
    {
        ToggleProjector();
        AudioManager.PlaySound("level63projector");
    }

    public void ToggleProjector()
    {
        if (!isActiveAndEnabled)
            return;

        if (_projectorState == ProjectorState.Off)
        {
            StartProjectorReveal();
            return;
        }

        if (_projectorState == ProjectorState.Closing)
            return;

        TurnProjectorOff();
    }

    private void StartProjectorReveal()
    {
        if (!HasMinimumProjectorReferences())
            return;

        StopProjectorSequence();
        _projectorVersion++;
        int revealVersion = _projectorVersion;
        _projectorState = ProjectorState.Revealing;

        SetProjectorRootActive(true);
        // SetSpriteVisible(projectionLight, true);
        SetProjectorFade(true);
        SetSpriteVisible(projectionCurtain, false);
        SetSpriteVisible(projectedText, false);
        SetCurtainSize(GetCurtainStartWidth(), 0f);
        SetGlitchActive(true);

        _projectorSequence = DOTween.Sequence().SetLink(gameObject);
        _projectorSequence.AppendInterval(Mathf.Max(0f, lightHoldDuration));
        _projectorSequence.AppendCallback(() =>
        {
            if (!IsCurrentReveal(revealVersion))
                return;

            SetSpriteVisible(projectionCurtain, true);
            SetCurtainSize(GetCurtainStartWidth(), 0f);
        });
        _projectorSequence.Append(DOTween.To(
            () => projectionCurtain != null ? projectionCurtain.size.y : 0f,
            height => SetCurtainSize(GetCurtainStartWidth(), height),
            GetFullCurtainHeight(),
            Mathf.Max(0f, curtainHeightRevealDuration)).SetEase(Ease.OutQuad));
        _projectorSequence.Append(DOTween.To(
            () => projectionCurtain != null ? projectionCurtain.size.x : GetCurtainStartWidth(),
            width => SetCurtainSize(width, GetFullCurtainHeight()),
            Mathf.Max(0f, fullCurtainWidth),
            Mathf.Max(0f, curtainRevealDuration)).SetEase(Ease.OutQuad));
        _projectorSequence.AppendCallback(() =>
        {
            if (!IsCurrentReveal(revealVersion))
                return;

            SetSpriteVisible(projectedText, true);
        });
        _projectorSequence.AppendInterval(Mathf.Max(0f, textRevealDuration));
        _projectorSequence.OnComplete(() =>
        {
            if (!IsCurrentReveal(revealVersion))
                return;

            SetCurtainWidth(Mathf.Max(0f, fullCurtainWidth));
            // SetSpriteVisible(projectionLight, true);
            SetProjectorFade(true);
            SetSpriteVisible(projectionCurtain, true);
            SetSpriteVisible(projectedText, true);
            _projectorState = ProjectorState.On;
            _projectorSequence = null;
        });
    }

    private bool IsCurrentReveal(int revealVersion)
    {
        return _projectorState == ProjectorState.Revealing && _projectorVersion == revealVersion;
    }

    private bool HasMinimumProjectorReferences()
    {
        return projectionLight != null || projectionCurtain != null || projectedText != null || projectorParent != null;
    }

    private void TurnProjectorOff()
    {
        _projectorVersion++;
        ResetProjectorVisuals(true);
    }

    private void ResetProjectorVisuals(bool playClose = false)
    {
        StopProjectorSequence();

        if (playClose && isActiveAndEnabled && HasVisibleProjectorVisuals())
        {
            StartProjectorCloseReverse();
            return;
        }

        ApplyProjectorOffVisuals();
    }

    private void StartProjectorCloseReverse()
    {
        int closeVersion = _projectorVersion;
        _projectorState = ProjectorState.Closing;

        _projectorSequence = DOTween.Sequence().SetLink(gameObject);
        SetSpriteVisible(projectedText, false);

        bool hasCloseTween = AppendCurtainWidthClose()
            | AppendCurtainHeightClose();

        if (!hasCloseTween)
        {
            ApplyProjectorOffVisuals();
            _projectorSequence = null;
            return;
        }

        _projectorSequence.OnComplete(() =>
        {
            if (_projectorState != ProjectorState.Closing || _projectorVersion != closeVersion)
                return;

            ApplyProjectorOffVisuals();
            _projectorSequence = null;
        });
    }

    private bool AppendCurtainWidthClose()
    {
        if (projectionCurtain == null || !projectionCurtain.gameObject.activeSelf)
            return false;

        float startWidth = projectionCurtain.size.x;
        float height = projectionCurtain.size.y;
        float targetWidth = GetCurtainStartWidth();
        if (startWidth <= targetWidth || height <= 0f)
            return false;

        _projectorSequence.Append(DOTween.To(
            () => projectionCurtain != null ? projectionCurtain.size.x : targetWidth,
            width => SetCurtainSize(width, height),
            targetWidth,
            Mathf.Max(0f, curtainRevealDuration)).SetEase(Ease.OutQuad));
        return true;
    }

    private bool AppendCurtainHeightClose()
    {
        if (projectionCurtain == null || !projectionCurtain.gameObject.activeSelf)
            return false;

        float targetWidth = GetCurtainStartWidth();
        float startHeight = projectionCurtain.size.y;
        if (startHeight <= 0f)
            return false;

        _projectorSequence.Append(DOTween.To(
            () => projectionCurtain != null ? projectionCurtain.size.y : 0f,
            height => SetCurtainSize(targetWidth, height),
            0f,
            Mathf.Max(0f, curtainHeightRevealDuration)).SetEase(Ease.InQuad));
        return true;
    }

    private void ApplyProjectorOffVisuals()
    {
        _projectorState = ProjectorState.Off;

        // SetSpriteVisible(projectionLight, false);
        SetProjectorFade(false);
        SetSpriteVisible(projectionCurtain, false);
        SetSpriteVisible(projectedText, false);
        SetCurtainSize(GetCurtainStartWidth(), 0f);
        SetGlitchActive(false);
    }

    private void StopProjectorSequence()
    {
        if (_projectorSequence == null)
            return;

        _projectorSequence.Kill(false);
        _projectorSequence = null;
    }

    private void CacheProjectorDefaults()
    {
        if (projectionCurtain == null)
            return;

        _curtainHeight = projectionCurtain.size.y;
        if (fullCurtainWidth <= 0f)
            fullCurtainWidth = projectionCurtain.size.x;
    }

    private void CacheGlitchMaterials()
    {
        if (glitchTargets == null || glitchTargets.Length == 0)
            return;

        _glitchOriginalMaterials = new Material[glitchTargets.Length];
        for (int i = 0; i < glitchTargets.Length; i++)
        {
            if (glitchTargets[i] == null)
                continue;

            _glitchOriginalMaterials[i] = glitchTargets[i].sharedMaterial;
        }
    }

    private void SetProjectorRootActive(bool isActive)
    {
        if (projectorParent == null)
            return;

        projectorParent.gameObject.SetActive(isActive);
    }

    private void SetSpriteVisible(SpriteRenderer renderer, bool isVisible)
    {
        if (renderer == null)
            return;

        renderer.gameObject.SetActive(isVisible);
    }

    private void SetProjectorFade(bool isVisible)
    {
        if (!projectionLight)
            return;
        projectionLight.DOKill();
        projectionLight.DOFade(isVisible ? 1 : 0, lightHoldDuration);
    }

    private bool HasVisibleProjectorVisuals()
    {
        return IsSpriteVisible(projectionLight) || IsSpriteVisible(projectionCurtain) || IsSpriteVisible(projectedText);
    }

    private void SetCurtainWidth(float width)
    {
        SetCurtainSize(width, GetFullCurtainHeight());
    }

    private void SetCurtainSize(float width, float height)
    {
        if (projectionCurtain == null)
            return;

        projectionCurtain.size = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    private bool IsSpriteVisible(SpriteRenderer renderer)
    {
        return renderer != null && renderer.gameObject.activeSelf;
    }

    private float GetCurtainStartWidth()
    {
        return Mathf.Max(0.01f, curtainStartWidth);
    }

    private float GetFullCurtainHeight()
    {
        if (_curtainHeight > 0f)
            return _curtainHeight;

        return projectionCurtain != null ? Mathf.Max(0f, projectionCurtain.size.y) : 0f;
    }

    private void SetGlitchActive(bool isActive)
    {
        if (glitchTargets == null || glitchTargets.Length == 0)
            return;

        for (int i = 0; i < glitchTargets.Length; i++)
        {
            SpriteRenderer target = glitchTargets[i];
            if (target == null)
                continue;

            if (isActive && glitchMaterial != null)
            {
                target.sharedMaterial = glitchMaterial;
                continue;
            }

            if (_glitchOriginalMaterials != null && i < _glitchOriginalMaterials.Length)
                target.sharedMaterial = _glitchOriginalMaterials[i];
        }
    }

    private void SubscribeUseButton()
    {
        if (Use_btn == null || _useButtonSubscribed || _useButtonLocked)
            return;

        Use_btn.OnClick += OnShowPasswordPopup;
        Use_btn.Interactable = true;
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

    private void RegisterUseButton()
    {
        UnsubscribeUseButton();
        Use_btn = ResolveUseButton();

        if (_useButtonLocked)
        {
            HideUseButton();
            return;
        }

        if (Use_btn != null)
        {
            Use_btn.Interactable = true;
            Use_btn.gameObject.SetActive(false);
        }

        SubscribeUseButton();
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
        if (!Use_btn.gameObject.activeSelf)
        {
            Use_btn.transform.localScale = Vector3.zero;
            return;
        }

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
        _useButtonLocked = false;
        _projectorVersion++;
        ResetProjectorVisuals();
        UnsubscribeUseButton();

        if (Use_btn)
        {
            Use_btn.Interactable = true;
            Use_btn.transform.DOKill();
        }
    }

    void OnDestroy()
    {
        _useButtonLocked = false;
        _projectorVersion++;
        ResetProjectorVisuals();
        UnsubscribeUseButton();
        EventsUtils.ResetEvents(ref _subscriber);

        if (projectionLight)
            projectionLight.DOKill();

        if (Use_btn)
        {
            Use_btn.Interactable = true;
            Use_btn.transform.DOKill();
        }
    }
}
