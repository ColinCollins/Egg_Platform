using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using TMPro;
using UnityEngine;

/// <summary>
/// version1.0
/// StartSign 进入关卡时，从 y = 25 位置可调，DOMoveY 移动到 6.5f 进入画面，等待 1s，然后回退会原位置。Ease.OutBase.
///
/// trophy4 参考 Level24Ctrl 隐藏在 rightBtn 之后，这里代码逻辑可以赋值过来
/// </summary>
public class Level86Ctrl : MonoBehaviour
{
    private const string ScrollPickupTrigger = "pickup";
    private const string ChestOpenAnimName = "box-open";
    private const string TipLocalizationKey = "S_LevelTips_Des_level_86";
    private const string FallbackTip = "Not yet.";

    private enum TrophySlot
    {
        Trophy1 = 0,
        Trophy2 = 1,
        Trophy3 = 2,
        Trophy4 = 3,
        Trophy5 = 4
    }

    [Header("Cell")]
    [SerializeField] private Rigidbody2D trophy6;

    [Header("Hat")]
    [SerializeField] private float clickFeedbackScale = 1.08f;
    [SerializeField] private float clickFeedbackDuration = 0.08f;
    [SerializeField] private GameObject trophy1;
    [SerializeField] private SpriteRenderer hatRender;

    [SerializeField] private float hatOpenLocalYOffset = 0.28f;
    [SerializeField] private float hatOpenScale = 0.92f;
    [SerializeField] private float hatOpenDuration = 0.12f;
    [SerializeField] private float hatFadeDuration = 0.18f;

    [Header("Scroll")]
    [SerializeField] private Animator scrolls;
    [SerializeField] private GameObject trophy2;

    [Header("Chest")]
    [SerializeField] private ActorSpineCtrl chest;
    [SerializeField] private GameObject trophy3;
    [SerializeField] private GameObject shiny;

    [Header("Slider Btn")]
    [SerializeField] private GameObject trophy4;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float trophy4ColliderZPosition = 0f;
    [SerializeField] private float trophy4RightBtnOffsetX = 0f;
    [SerializeField] private float trophy4ResolveMoveX = 200f;

    [Header("Break Box")]
    [SerializeField] private GameObject trophy5;

    [Header("Real One")]
    [SerializeField] private Transform StartSign;
    [SerializeField] private SpriteRenderer signBoard;
    [Tooltip("告示牌提示文字；仅在 Level86 成功结算表现中与 signBoard 同步渐隐，缺失时跳过。")]
    [SerializeField] private TMP_Text[] signTips;
    [SerializeField] private Transform trophy0;
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private float entrySignFromY = 25f;
    [SerializeField] private float entrySignShowY = 6.5f;
    [SerializeField] private float entrySignMoveDuration = 0.35f;
    [SerializeField] private float entrySignStayDuration = 1f;
    [SerializeField] private float finalSuccessDelay = 0.2f;
    [SerializeField] private float finalTrophyScale = 1.35f;
    [SerializeField] private float finalTrophyMoveDuration = 0.45f;
    [SerializeField] private float finalSignFadeDuration = 0.3f;

    [SerializeField] private GameObject star;

    private readonly bool[] resolvedTrophies = new bool[5];

    private Sequence entrySignSequence;
    private Sequence finalSuccessSequence;
    private Tween finalSuccessDelayTween;
    private Vector3 startSignInitialPosition;
    private Vector3 trophy0InitialScale = Vector3.one;
    private float signBoardInitialAlpha = 1f;
    private float[] signTipsInitialAlphas = new float[0];
    private bool hasCachedInitialState;
    private bool successAccepted;
    private int attemptVersion;
    private RectTransform rightBtn;
    private float rightBtnStartPosX;

    private bool hasChestOpen = false;

    private void Awake()
    {
        CacheInitialState();
        ResolveLevelCtrl();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        CacheInitialState();
        ResetAttemptState();
        PlayEntrySignPresentation();
        ResetTrophy4PositionAsync(attemptVersion).Forget();
    }

    private void OnDisable()
    {
        CleanupRuntimeState(false);
    }

    private void OnDestroy()
    {
        CleanupRuntimeState(false);
    }

    public void PlayPickUpScroll(Transform trans, Vector2 pos)
    {
        if (!CanHandleApi())
        {
            return;
        }

        scrolls?.SetTrigger(ScrollPickupTrigger);
        ResolveTrophy(TrophySlot.Trophy2, trophy2);
    }

    public void PlayHatClickFeedback(Transform trans, Vector2 pos)
    {
        if (!CanHandleApi())
        {
            return;
        }

        PlayHatAnim(trans);
        ResolveTrophy(TrophySlot.Trophy1, trophy1);
    }

    /// <summary>
    /// 播放开启宝箱
    /// </summary>
    public void PlayChest()
    {
        if (!CanHandleApi() || hasChestOpen)
        {
            return;
        }

        hasChestOpen = true;

        // chest play box-open
        // trophy3 显示
        OnPlayChestAsync().Forget();
    }

    private async UniTaskVoid OnPlayChestAsync()
    {
        var version = attemptVersion;
        if (chest != null)
        {
            PlayClickFeedback(chest.transform);
            chest.PlayAnimation(ChestOpenAnimName, false, false);
        }

        AudioManager.PlaySound("openChest");

        PlayShiny();

        await UniTask.WaitForSeconds(1f, ignoreTimeScale: true);

        if (star)
            star.SetActive(true);

        if (!Application.isPlaying || version != attemptVersion || successAccepted)
        {
            return;
        }

        ResolveTrophy(TrophySlot.Trophy3, trophy3);
    }


    public void PlayShiny()
    {
        if (!Application.isPlaying || shiny == null || chest == null)
        {
            return;
        }

        var obj = Instantiate(shiny, transform);
        obj.SetActive(true);
        obj.transform.position = chest.transform.position + new Vector3(0, 2f, 0);
    }

    public void PlayTrophy4(Transform trans, Vector2 pos)
    {
        if (!CanHandleApi())
        {
            return;
        }

        PlayClickFeedback(trans);
        ResolveTrophy(TrophySlot.Trophy4, trophy4);
    }

    private void Update()
    {
        if (!CanHandleApi())
        {
            return;
        }

        CheckRightBtnPosition();
    }

    public void PlayTrophy5(Transform trans, Vector2 pos)
    {
        if (!CanHandleApi())
        {
            return;
        }

        PlayClickFeedback(trans);
        ResolveTrophy(TrophySlot.Trophy5, trophy5);
    }

    public void PlayTrophy6(Transform trans, Vector2 pos)
    {
        if (!Application.isPlaying || trophy6 == null)
        {
            return;
        }

        if (trophy6.bodyType != RigidbodyType2D.Dynamic)
        {
            trophy6.bodyType = RigidbodyType2D.Dynamic;
        }
    }

    /// <summary>
    /// 播放胜利动画，然后通关
    /// </summary>
    public void PlaySuccessLevel(Transform trans, Vector2 pos)
    {
        if (!CanTriggerSuccess())
        {
            return;
        }
        entrySignSequence?.Kill();
        // trophy0
        OnSuccess(trans);
    }

    public void PlaySystemTips(Transform trans, Vector2 pos)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        var msg = LocalizationManager.GetTranslation(TipLocalizationKey);
        if (string.IsNullOrWhiteSpace(msg))
        {
            msg = FallbackTip;
        }

        SystemTips.Show(msg);
        if (trans == null)
        {
            return;
        }

        AudioManager.PlaySound("passwordError");
        trans.DOKill();
        trans.DOShakeRotation(.5f, Vector3.forward * 40, 20, 40f);
    }

    private bool CanHandleApi()
    {
        return Application.isPlaying && !successAccepted;
    }

    /// <summary>
    /// 仅允许在游戏进行中且没有 Popup 界面时启动成功表现，避免暂停或弹窗期间误触发结算。
    /// </summary>
    private bool CanTriggerSuccess()
    {
        if (!CanHandleApi())
        {
            return false;
        }

        var playCtrl = PlayCtrl.Instance;
        if (playCtrl == null || !playCtrl.CheckState(GamePlayStateName.PLAYING))
        {
            return false;
        }

        return !HasOpenPopup();
    }

    /// <summary>
    /// 检查 UIManager 当前仍处于打开状态的 Popup 界面；关闭但缓存的实例不会阻断成功。
    /// </summary>
    private bool HasOpenPopup()
    {
        var popupViews = UIManager.Instance.GetComponentsInChildren<BaseUIView>(true);
        for (var i = 0; i < popupViews.Length; i++)
        {
            var popupView = popupViews[i];
            if (popupView != null && popupView.Layer == UILayer.Popup && popupView.IsOpen)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveTrophy(TrophySlot slot, GameObject trophy)
    {
        var index = (int)slot;
        if (index < 0 || index >= resolvedTrophies.Length || resolvedTrophies[index])
        {
            return;
        }

        resolvedTrophies[index] = true;
        SetActiveIfNotNull(trophy, true);
    }

    private void OnSuccess(Transform trans)
    {
        if (successAccepted)
        {
            return;
        }

        successAccepted = true;
        if (trans != null && trans != trophy0)
        {
            PlayClickFeedback(trans);
        }

        SetActiveIfNotNull(trophy0 != null ? trophy0.gameObject : null, true);

        finalSuccessSequence?.Kill();
        finalSuccessDelayTween?.Kill();

        finalSuccessSequence = DOTween.Sequence().SetLink(gameObject);
        if (signBoard != null)
        {
            finalSuccessSequence.Join(signBoard.DOFade(0f, Mathf.Max(0.01f, finalSignFadeDuration)).SetEase(Ease.OutQuad));
        }

        JoinSignTipsFade(finalSuccessSequence);

        if (trophy0 != null)
        {
            var targetPosition = GetFinalTrophyTargetPosition(trans);
            finalSuccessSequence.Join(trophy0.DOMove(targetPosition, Mathf.Max(0.01f, finalTrophyMoveDuration)).SetEase(Ease.OutBack));
            finalSuccessSequence.Join(trophy0.DOScale(trophy0InitialScale * Mathf.Max(0.01f, finalTrophyScale), Mathf.Max(0.01f, finalTrophyMoveDuration)).SetEase(Ease.OutBack));
        }

        finalSuccessSequence.AppendInterval(Mathf.Max(0f, finalSuccessDelay));
        finalSuccessSequence.AppendInterval(1f);
        finalSuccessSequence.OnComplete(() =>
        {
            ResolveLevelCtrl();
            levelCtrl?.SuccessStrightly();
        });

        AudioManager.PlaySound("footballAudienceCelebrate");
    }

    private void PlayClickFeedback(Transform target)
    {
        if (target == null)
        {
            return;
        }
        target.DOKill();

        target
             .DOScale(Vector3.one * Mathf.Max(0.01f, clickFeedbackScale), Mathf.Max(0.01f, clickFeedbackDuration))
             .SetEase(Ease.OutQuad)
             .SetLoops(2, LoopType.Yoyo)
             .SetLink(gameObject);
    }

    private void PlayHatAnim(Transform target)
    {
        if (target == null)
        {
            return;
        }

        target.DOKill();

        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        seq.Append(target
            .DOScale(Vector3.one * Mathf.Max(0.01f, clickFeedbackScale), Mathf.Max(0.01f, clickFeedbackDuration))
            .SetEase(Ease.OutQuad)
            .SetLoops(2, LoopType.Yoyo)
            .SetLink(gameObject));


        float openDuration = Mathf.Max(0.01f, hatOpenDuration);
        Vector3 openPosition = target.localPosition + Vector3.up * hatOpenLocalYOffset;
        seq.Append(target.DOLocalMove(openPosition, openDuration).SetEase(Ease.OutQuad));
        seq.Append(target.DOScale(Vector3.one * Mathf.Max(0.01f, hatOpenScale), openDuration).SetEase(Ease.OutQuad));
        if (hatRender != null)
        {
            seq.Append(hatRender.DOFade(0f, Mathf.Max(0.01f, hatFadeDuration)).SetEase(Ease.OutQuad));
        }
    }

    private void PlayEntrySignPresentation()
    {
        if (StartSign == null)
        {
            return;
        }

        entrySignSequence?.Kill();

        var startPosition = startSignInitialPosition;
        startPosition.y = entrySignFromY;

        StartSign.position = startPosition;
        entrySignSequence = DOTween.Sequence()
            .SetLink(gameObject)
            .Append(StartSign.DOMoveY(entrySignShowY, Mathf.Max(0.01f, entrySignMoveDuration)).SetEase(Ease.OutBack))
            .AppendInterval(Mathf.Max(0f, entrySignStayDuration))
            .Append(StartSign.DOMoveY(startSignInitialPosition.y, Mathf.Max(0.01f, entrySignMoveDuration)).SetEase(Ease.InBack))
            .SetDelay(1f);
    }

    private void ResetAttemptState()
    {
        CleanupRuntimeState(true);
        attemptVersion++;
        rightBtn = null;
        rightBtnStartPosX = 0f;

        for (var i = 0; i < resolvedTrophies.Length; i++)
        {
            resolvedTrophies[i] = false;
        }

        hasChestOpen = false;
        successAccepted = false;
        SetActiveIfNotNull(trophy1, false);
        SetActiveIfNotNull(trophy2, false);
        SetActiveIfNotNull(trophy3, false);
        SetActiveIfNotNull(trophy4, false);
        SetActiveIfNotNull(trophy5, false);

        if (trophy0 != null)
        {
            trophy0.localScale = trophy0InitialScale;
        }

        if (signBoard != null)
        {
            var color = signBoard.color;
            color.a = signBoardInitialAlpha;
            signBoard.color = color;
        }

        RestoreSignTipsAlpha();
    }

    private async UniTaskVoid ResetTrophy4PositionAsync(int version)
    {
        await UniTask.WaitForSeconds(0.5f, ignoreTimeScale: true);

        if (!Application.isPlaying || version != attemptVersion || successAccepted)
        {
            return;
        }

        var panel = PlayCtrl.Instance?.CurrentGamePlayPanel;
        if (panel == null)
        {
            return;
        }

        var panelRoot = panel.transform.childCount > 0 ? panel.transform.GetChild(0) : panel.transform;
        rightBtn = panelRoot.Find("rightMove_btn") as RectTransform;
        if (rightBtn == null)
        {
            rightBtn = panel.transform.Find("Root/rightMove_btn") as RectTransform;
        }

        if (rightBtn == null || trophy4 == null)
        {
            return;
        }

        rightBtnStartPosX = rightBtn.anchoredPosition.x;
        ResolveMainCamera();

        var worldPos = CommonUtility.ConvertUIToWorldPosition(rightBtn, mainCamera, trophy4ColliderZPosition);
        worldPos.x += trophy4RightBtnOffsetX;
        trophy4.transform.position = worldPos;

        await UniTask.WaitForSeconds(1.5f, ignoreTimeScale: true);
        if (!Application.isPlaying || version != attemptVersion || successAccepted)
        {
            return;
        }

        SetActiveIfNotNull(trophy4, true);
    }

    private void CheckRightBtnPosition()
    {
        if (rightBtn == null || resolvedTrophies[(int)TrophySlot.Trophy4])
        {
            return;
        }

        var dx = rightBtn.anchoredPosition.x - rightBtnStartPosX;
        if (dx > trophy4ResolveMoveX)
        {
            ResolveTrophy(TrophySlot.Trophy4, trophy4);
        }
    }

    private void CleanupRuntimeState(bool keepInitialState)
    {
        attemptVersion++;
        entrySignSequence?.Kill();
        entrySignSequence = null;
        finalSuccessSequence?.Kill();
        finalSuccessSequence = null;
        finalSuccessDelayTween?.Kill();
        finalSuccessDelayTween = null;

        StartSign?.DOKill();
        trophy0?.DOKill();
        signBoard?.DOKill();
        KillSignTipsTween();

        if (keepInitialState && StartSign != null)
        {
            StartSign.position = startSignInitialPosition;
        }
    }

    private void CacheInitialState()
    {
        ResolveSignTips();

        if (hasCachedInitialState)
        {
            return;
        }

        if (StartSign != null)
        {
            startSignInitialPosition = StartSign.position;
        }

        if (trophy0 != null)
        {
            trophy0InitialScale = trophy0.localScale;
        }

        if (signBoard != null)
        {
            signBoardInitialAlpha = signBoard.color.a;
        }

        CacheSignTipsInitialAlphas();

        hasCachedInitialState = true;
    }

    private void ResolveSignTips()
    {
        if (HasValidSignTips() || signBoard == null)
        {
            return;
        }

        signTips = signBoard.GetComponentsInChildren<TMP_Text>(true);
    }

    private void JoinSignTipsFade(Sequence sequence)
    {
        if (sequence == null || signTips == null)
        {
            return;
        }

        var fadeDuration = Mathf.Max(0.01f, finalSignFadeDuration);
        for (var i = 0; i < signTips.Length; i++)
        {
            var signTip = signTips[i];
            if (signTip == null)
            {
                continue;
            }

            sequence.Join(signTip.DOFade(0f, fadeDuration).SetEase(Ease.OutQuad));
        }
    }

    private void RestoreSignTipsAlpha()
    {
        if (signTips == null)
        {
            return;
        }

        for (var i = 0; i < signTips.Length; i++)
        {
            var signTip = signTips[i];
            if (signTip == null)
            {
                continue;
            }

            var color = signTip.color;
            color.a = GetSignTipInitialAlpha(i);
            signTip.color = color;
        }
    }

    private void KillSignTipsTween()
    {
        if (signTips == null)
        {
            return;
        }

        for (var i = 0; i < signTips.Length; i++)
        {
            signTips[i]?.DOKill();
        }
    }

    private void CacheSignTipsInitialAlphas()
    {
        if (signTips == null || signTips.Length == 0)
        {
            signTipsInitialAlphas = new float[0];
            return;
        }

        signTipsInitialAlphas = new float[signTips.Length];
        for (var i = 0; i < signTips.Length; i++)
        {
            signTipsInitialAlphas[i] = signTips[i] != null ? signTips[i].color.a : 1f;
        }
    }

    private float GetSignTipInitialAlpha(int index)
    {
        if (signTipsInitialAlphas != null && index >= 0 && index < signTipsInitialAlphas.Length)
        {
            return signTipsInitialAlphas[index];
        }

        return 1f;
    }

    private bool HasValidSignTips()
    {
        if (signTips == null)
        {
            return false;
        }

        for (var i = 0; i < signTips.Length; i++)
        {
            if (signTips[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetFinalTrophyTargetPosition(Transform trans)
    {
        if (signBoard != null)
        {
            return signBoard.transform.position;
        }

        if (StartSign != null)
        {
            return StartSign.position;
        }

        if (trans != null)
        {
            return trans.position;
        }

        return trophy0 != null ? trophy0.position : transform.position;
    }

    private void ResolveLevelCtrl()
    {
        if (levelCtrl != null)
        {
            return;
        }

        levelCtrl = GetComponent<BaseLevelCtrl>();
        if (levelCtrl == null)
        {
            levelCtrl = GetComponentInParent<BaseLevelCtrl>();
        }
    }

    private void ResolveMainCamera()
    {
        if (mainCamera != null)
        {
            return;
        }

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
    }

    private void SetActiveIfNotNull(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
