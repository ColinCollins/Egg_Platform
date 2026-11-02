using Bear.Logger;
using DG.Tweening;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Level71 商店免费钥匙入口：仅在 Level71 的暂停商店中显示，负责领取防重、飞走渐隐动画和关闭商店。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level71ShopBtnCtrl : MonoBehaviour, IPointerClickHandler, IDebuger
{
    #region Configuration

    private const string TargetLevelPath = "Level_071";

    /// <summary>
    /// 用途：指定领取时播放飞走动画的 UI 根节点；未绑定时使用当前对象的 RectTransform。
    /// 调用边界：仅在运行态初始化和播放领取动画时读取。
    /// 注意事项：目标应与本组件位于同一 ShopPanel 生命周期内。
    /// </summary>
    [Header("领取动画")]
    [SerializeField] private RectTransform animationTarget;

    /// <summary>
    /// 用途：指定免费钥匙整体的透明度与射线控制；未绑定时运行态在当前对象上补充 CanvasGroup。
    /// 调用边界：仅由本组件控制，不应与其它常驻透明度动画同时驱动。
    /// 注意事项：CanvasGroup 应覆盖免费钥匙的全部子节点。
    /// </summary>
    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>
    /// 用途：定义免费钥匙领取后相对初始位置的飞出偏移。
    /// 调用边界：使用当前 RectTransform 的 anchoredPosition 坐标系。
    /// 注意事项：默认向右上方飞出，后续可在 Prefab Inspector 中微调。
    /// </summary>
    [SerializeField] private Vector2 flyOffset = new(240f, 180f);

    /// <summary>
    /// 用途：定义飞走与渐隐动画时长。
    /// 调用边界：使用未缩放时间，确保 ShopPanel 暂停游戏时仍能完成。
    /// 注意事项：小于或等于 0 时会直接完成领取并关闭商店。
    /// </summary>
    [SerializeField, Min(0f)] private float flyDuration = 0.45f;

    /// <summary>
    /// 用途：定义免费钥匙飞出时的缓动曲线。
    /// 调用边界：仅作用于位置动画，透明度保持线性渐隐。
    /// 注意事项：默认使用 Ease.InQuad 表现加速飞离。
    /// </summary>
    [SerializeField] private Ease flyEase = Ease.InQuad;

    #endregion

    #region Runtime State

    private static BaseLevelCtrl claimedAttemptOwner;

    private ShopPanel shopPanel;
    private Sequence claimSequence;
    private Vector2 initialAnchoredPosition;
    private bool isClaiming;

    /// <summary>
    /// 用途：供 Level71 关卡逻辑判断当前关卡实例是否已经领取免费钥匙。
    /// 调用边界：仅表示当前存活的 Level71 实例；Reset、Replay 或重新进入关卡重建实例后自动恢复为 false。
    /// 注意事项：该状态不写入存档，也不代表实体钥匙已经被角色拾取。
    /// </summary>
    public static bool HasClick
    {
        get
        {
            if (!Application.isPlaying)
                return false;

            PlayCtrl playCtrl = PlayCtrl.Instance;
            return IsTargetLevel(playCtrl)
                && playCtrl.LevelCtrl != null
                && ReferenceEquals(claimedAttemptOwner, playCtrl.LevelCtrl);
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        initialAnchoredPosition = animationTarget.anchoredPosition;
        RefreshVisibility();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        ResetVisualState();
        RefreshVisibility();
    }

    private void Start()
    {
        if (Application.isPlaying)
            RefreshVisibility();
    }

    private void OnDisable()
    {
        claimSequence?.Kill();
        claimSequence = null;

        if (isClaiming && shopPanel != null)
            shopPanel.SetClickBlocked(false);

        isClaiming = false;
    }

    #endregion

    #region Interaction

    /// <summary>
    /// 用途：接收免费钥匙区域点击并启动领取动画。
    /// 调用边界：由 Unity EventSystem 在当前对象或子节点命中射线时调用。
    /// 注意事项：非 Level71、非暂停商店、重复领取或动画进行中都会忽略。
    /// </summary>
    /// <param name="eventData">本次 UI 指针点击数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isClaiming || HasClick || !IsLevel71ShopContext(out BaseLevelCtrl attemptOwner))
            return;

        claimedAttemptOwner = attemptOwner;
        isClaiming = true;
        canvasGroup.blocksRaycasts = false;
        // shopPanel.SuppressRewardTipAutoShowOnClose();
        shopPanel.SetClickBlocked(true);

        PlayClaimAnimation();

        AudioManager.PlaySound("shopGetReward");
    }

    private void PlayClaimAnimation()
    {
        claimSequence?.Kill();

        if (flyDuration <= 0f)
        {
            animationTarget.anchoredPosition = initialAnchoredPosition + flyOffset;
            canvasGroup.alpha = 0f;
            CompleteClaim();
            return;
        }

        Vector2 targetPosition = initialAnchoredPosition + flyOffset;
        claimSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .Join(animationTarget
                .DOAnchorPos(targetPosition, flyDuration)
                .SetEase(flyEase))
            .Join(canvasGroup
                .DOFade(0f, flyDuration)
                .SetEase(Ease.Linear))
            .OnComplete(CompleteClaim);
    }

    private void CompleteClaim()
    {
        claimSequence = null;
        isClaiming = false;

        if (shopPanel != null && shopPanel.TryClosePanel())
            return;

        if (shopPanel != null)
            shopPanel.SetClickBlocked(false);

        this.LogWarning("[Level71ShopBtnCtrl] Free key was claimed, but ShopPanel could not be closed.");
    }

    #endregion

    #region Context And Visual State

    private void ResolveReferences()
    {
        animationTarget ??= transform as RectTransform;
        canvasGroup ??= GetComponent<CanvasGroup>();
        canvasGroup ??= gameObject.AddComponent<CanvasGroup>();
        shopPanel ??= GetComponentInParent<ShopPanel>();
    }

    private void RefreshVisibility()
    {
        bool shouldShow = !HasClick && IsLevel71ShopContext(out _);
        if (gameObject.activeSelf != shouldShow)
            gameObject.SetActive(shouldShow);
    }

    private static bool IsTargetLevel(PlayCtrl playCtrl)
    {
        return playCtrl?.Level?.CurrentLevelData?.Path == TargetLevelPath;
    }

    private bool IsLevel71ShopContext(out BaseLevelCtrl attemptOwner)
    {
        attemptOwner = null;
        if (shopPanel == null)
            return false;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        if (!IsTargetLevel(playCtrl)
            || playCtrl.LevelCtrl == null
            || playCtrl.CurrentGamePlayPanel == null
            || !playCtrl.CheckState(GamePlayStateName.PAUSE))
        {
            return false;
        }

        attemptOwner = playCtrl.LevelCtrl;
        return true;
    }

    private void ResetVisualState()
    {
        animationTarget.anchoredPosition = initialAnchoredPosition;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
    }

    #endregion
}
