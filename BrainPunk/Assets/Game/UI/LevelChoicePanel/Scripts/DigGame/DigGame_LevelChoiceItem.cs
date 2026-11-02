using Bear.Logger;
using Config;
using DG.Tweening;
using I2.Loc;
using UnityEngine;

/// <summary>
/// Dig 选关项，只读取 DigLevelSort、DigLevelData 和 DB.DigGame 切换显示与交互状态。
/// </summary>
[DisallowMultipleComponent]
public sealed partial class DigGame_LevelChoiceItem : BaseAutoUIBind, IDebuger
{
    #region Constants

    private const string WaitingForNextLevelDataId = "40999";

    #endregion

    #region Types

    private enum State
    {
        Normal,
        Current,
        LevelLock,
        AdLock,
        Passed
    }

    #endregion

    #region Serialized References

    /// <summary>
    /// Dig 专用选关星星组；只读取 DB.DigGame，不得绑定主线 Postcard 星星组。
    /// </summary>
    [SerializeField] private DigGame_ChoiceLevelStarGroupCtrl starGroupCtrl;

    /// <summary>
    /// 分页切换时控制整个选关项透明度的 CanvasGroup。
    /// </summary>
    [SerializeField] private CanvasGroup canvasGroup;

    #endregion

    #region Runtime State

    private DigGame_ChoiceLevelPanel owner;
    private DigLevelSort sortData;
    private DigLevelData levelData;
    private State state;
    private Tween canvasGroupAlphaTween;
    private bool referencesReady;
    private int configGeneration;

    #endregion

    #region Public API

    /// <summary>
    /// 初始化 Dig 选关项的面板归属并校验 prefab 显式引用。
    /// 调用边界：仅由 DigGame_ChoiceLevelPanel 创建缓存项时调用一次。
    /// </summary>
    public void Init(DigGame_ChoiceLevelPanel panel)
    {
        owner = panel;
        Init();
    }

    /// <summary>
    /// 校验 AutoUIBind 生成字段及 Dig 手工扩展引用。
    /// 调用边界：由 Dig 面板初始化调用；编辑器绑定后也可用于运行时完整性检查。
    /// </summary>
    public override void Init()
    {
        referencesReady = HasRequiredReferences();

        if (!referencesReady)
            this.LogError("[DigGame_LevelChoiceItem] Required prefab references are incomplete.");

        if (starGroupCtrl == null)
            this.LogWarning("[DigGame_LevelChoiceItem] Dig star group is missing.");
    }

    /// <summary>
    /// 绑定一条 Dig 排序数据，并按 DB.DigGame 刷新状态、星星与点击行为。
    /// 调用边界：仅由 DigGame_ChoiceLevelPanel 刷新当前分页时调用；传空会隐藏本项。
    /// </summary>
    public void SetData(DigLevelSort digSortData, DigLevelData digLevelData)
    {
        ClearButtonEvents();
        sortData = digSortData;
        levelData = digLevelData;
        configGeneration = owner.ConfigGeneration;

        if (!referencesReady || sortData == null || levelData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        state = ResolveState();
        gameObject.SetActive(true);
        RefreshVisualState();
        BindButtonEvent();
    }

    /// <summary>
    /// 设置分页切换时的选关项透明度。
    /// 调用边界：仅用于 Dig 选关面板的离散分页表现，不应逐帧调用。
    /// </summary>
    public void SetCanvasGroupAlpha(float value, float duration = 0f)
    {
        if (canvasGroup == null)
            return;

        canvasGroupAlphaTween?.Kill();
        canvasGroupAlphaTween = null;
        value = Mathf.Clamp01(value);

        if (duration <= 0f)
        {
            canvasGroup.alpha = value;
            return;
        }

        canvasGroupAlphaTween = canvasGroup
            .DOFade(value, duration)
            .SetUpdate(true);
    }

    #endregion

    #region State And Presentation

    private State ResolveState()
    {
        if (owner == null || !owner.IsUnlocked(sortData.Id))
        {
            return levelData.LockType == Config.Level.LevelLockType.Ad
                ? State.AdLock
                : State.LevelLock;
        }

        if (owner.IsCurrent(sortData.Id))
            return State.Current;

        return owner.IsPassed(sortData.Id) ? State.Passed : State.Normal;
    }

    private void RefreshVisualState()
    {
        PassedState.SetActive(state == State.Passed);
        CurrentState.SetActive(state == State.Current);
        NormalState.SetActive(state == State.Normal);
        AdLockState.SetActive(state == State.AdLock || state == State.LevelLock);
        AdImg.gameObject.SetActive(state == State.AdLock);

        string displayId = sortData.Id.ToString();
        PassedTxt.text = displayId;
        CurrentTxt.text = displayId;
        AdLockTxt.text = displayId;
        NormalTxt.text = displayId;

        if (starGroupCtrl != null)
        {
            bool isWaitingForNextLevel = levelData.Id == WaitingForNextLevelDataId;
            bool isStarGroupVisible =
                !isWaitingForNextLevel && state != State.AdLock && state != State.LevelLock;
            starGroupCtrl.Refresh(levelData, isStarGroupVisible, state != State.Current);
        }
    }

    private bool HasRequiredReferences()
    {
        return ClickAreaBtn != null
               && PassedTxt != null
               && CurrentTxt != null
               && AdLockTxt != null
               && NormalTxt != null
               && AdImg != null
               && PassedState != null
               && CurrentState != null
               && AdLockState != null
               && NormalState != null
               && canvasGroup != null;
    }

    #endregion

    #region Interaction

    private void BindButtonEvent()
    {
        switch (state)
        {
            case State.Normal:
            case State.Current:
            case State.Passed:
                ClickAreaBtn.OnClick += EnterLevel;
                break;
            case State.AdLock:
                ClickAreaBtn.OnClick += ShowRewardAd;
                break;
            default:
                ClickAreaBtn.OnClick += ShowLockedTips;
                break;
        }
    }

    private void EnterLevel(CustomButton button)
    {
        if (owner == null || owner.IsRewardAdPending) return;
        owner.EnterLevel(sortData, levelData, configGeneration);
    }

    private void ShowRewardAd(CustomButton button)
    {
        DigGame_ChoiceLevelPanel requestedOwner = owner;
        DigLevelSort requestedSort = sortData;
        DigLevelData requestedData = levelData;
        int requestedGeneration = configGeneration;
        if (requestedOwner == null || !requestedOwner.TryBeginRewardAd(requestedGeneration, requestedSort, requestedData)) return;

        try
        {
            RewardAdHelper.TryToShowRewardAd(
                Config.Game.RewardPlacement.OutGameUnlockLevel.ToString(),
                success: "U_HurryTips_RewardVideo_succeed",
                failed: "U_HurryTips_RewardVideo_failed",
                onResult: (_, isSuccess) =>
                {
                    if (requestedOwner == null) return;
                    try
                    {
                        if (isSuccess) requestedOwner.UnlockAndEnter(requestedSort, requestedData, requestedGeneration);
                    }
                    finally
                    {
                        requestedOwner.EndRewardAd();
                    }
                });
        }
        catch
        {
            requestedOwner.EndRewardAd();
            throw;
        }
    }

    private static void ShowLockedTips(CustomButton button)
    {
        SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_TBC"));
    }

    private void ClearButtonEvents()
    {
        if (ClickAreaBtn == null)
            return;

        ClickAreaBtn.OnClick -= EnterLevel;
        ClickAreaBtn.OnClick -= ShowRewardAd;
        ClickAreaBtn.OnClick -= ShowLockedTips;
    }

    #endregion

    #region Editor Preview

#if UNITY_EDITOR
    private static GUIStyle s_levelIdGuiStyle;

    /// <summary>
    /// 在编辑器 PlayMode 中把当前 Dig 选关项对应的关卡配置 ID 显示在 item 上方。
    /// 仅读取当前已绑定的 DigLevelSort.Path，不访问 DB、配置管理器或运行时单例。
    /// </summary>
    private void OnGUI()
    {
        if (!UnityEditor.EditorApplication.isPlaying || !isActiveAndEnabled || sortData == null)
            return;

        string levelId = sortData.Path;
        if (string.IsNullOrEmpty(levelId))
            return;

        var rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            canvas != null ? canvas.worldCamera : null,
            rectTransform.position);
        GUIStyle style = GetLevelIdGuiStyle();
        Vector2 labelSize = style.CalcSize(new GUIContent(levelId));
        Rect labelRect = new Rect(
            screenPoint.x - labelSize.x * 0.5f - 8f,
            Screen.height - screenPoint.y - rectTransform.rect.height * 0.5f - labelSize.y - 6f,
            labelSize.x + 16f,
            labelSize.y + 6f);

        GUI.Box(labelRect, GUIContent.none);
        GUI.Label(labelRect, levelId, style);
    }

    private static GUIStyle GetLevelIdGuiStyle()
    {
        if (s_levelIdGuiStyle != null)
            return s_levelIdGuiStyle;

        s_levelIdGuiStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 30,
            normal =
            {
                textColor = Color.white
            }
        };
        s_levelIdGuiStyle.padding = new RectOffset(8, 8, 3, 3);
        return s_levelIdGuiStyle;
    }
#endif

    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        canvasGroupAlphaTween?.Kill();
        canvasGroupAlphaTween = null;
        ClearButtonEvents();
    }

    #endregion
}
