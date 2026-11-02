using System;
using System.Collections.Generic;
using Bear.UI;
using Config;
using Config.Game;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ConfigModule;
using Game.ItemEvent;
using Game.Play;
using Game.Scripts.Common;
using GameCommon;
using I2.Loc;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level 105 专属假胜利纯展示页。真实 GameVictoryPanel / GameVictoryPanel_Reward Variant
/// 保留 Spine 入场、成功音效、粒子和按钮反馈；本组件只读当前星星并驱动视觉，
/// 不调用共享胜利刷新、明信片消费/引导、奖励、分享、重玩或下一关业务。
/// </summary>
public sealed class Level105FakeVictoryPopupView : BaseUIView
{
    #region Constants

    private const string InAnimationName = "in";
    private const string IdleAnimationName = "idle";
    private const string StarGroupName = "Postcard_VictoryStarGroup";
    private const string StarBreathObjectName = "UI_common_xingfaguang_01";
    private const string OpenConfettiObjectName = "ConfettiDirectionalGreenYellow";
    private const string NoAdProductName = "no_ads";

    #endregion

    #region Inspector

    [SerializeField] private float nextButtonFadeDuration = 0.2f;
    [SerializeField] private float nextButtonFadeDelay = 0.15f;
    [SerializeField] private float tipsTypewriterDuration = 1f;
    [SerializeField] private float rewardButtonGroupDelay = 2f;
    [SerializeField] private float rewardButtonGroupDuration = 0.35f;
    [SerializeField] private float rewardButtonGroupEnterOffsetY = 160f;

    #endregion

    #region Runtime State

    private UISpineCtrl spineCtrl;
    private TextMeshProUGUI tipsText;
    private CustomButton nextButton;
    private CanvasGroup nextButtonGroup;
    private CanvasGroup rewardButtonGroup;
    private RectTransform rewardButtonGroupRect;
    private Vector2 rewardButtonGroupTargetPosition;
    private HorizontalRandomAdRewardCtrl rewardSelector;
    private GameVictoryPanel_Share rewardSharePart;
    private BoneFollowerGraphic rewardArrowBone;
    private TextMeshProUGUI rewardPreviewText;
    private CustomButton noAdButton;
    private BreathingScaleHandle noAdBreathingScale;
    private TextMeshProUGUI shareRewardText;
    private Spine.AnimationState boundSpineState;

    private Tween noAdScaleTween;
    private Tween tipsTween;
    private Tween tipsFallbackTween;
    private Tween nextButtonTween;
    private Tween rewardButtonDelayTween;
    private Tween rewardButtonGroupTween;

    #endregion

    #region UIView Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();
        ResolveReferences();
        BlockBusinessButtons();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ResolveReferences();
        BlockBusinessButtons();
        DisableInheritedClickBlocks();
        ResetPresentationState();
        RefreshReadOnlyStars();
        PlayOpenEffects();
        PlaySpineEntry();
        AudioManager.PlaySound("success");
    }

    public override void OnClose()
    {
        StopTransientPresentation();
        if (rewardSelector != null)
            rewardSelector.Stop(snapToRewardBand: false).Forget();

        base.OnClose();
    }

    private void Update()
    {
        RefreshRewardPreviewText();
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        spineCtrl ??= GetComponentInChildren<UISpineCtrl>(true);
        rewardSelector ??= GetComponentInChildren<HorizontalRandomAdRewardCtrl>(true);
        rewardSharePart ??= GetComponentInChildren<GameVictoryPanel_Share>(true);

        if (noAdButton == null)
        {
            Transform noAdTransform = FindDescendant("noAd_btn");
            noAdButton = noAdTransform != null ? noAdTransform.GetComponent<CustomButton>() : null;
            noAdBreathingScale = noAdTransform != null
                ? noAdTransform.GetComponent<BreathingScaleHandle>()
                : null;
        }

        if (shareRewardText == null)
        {
            Transform shareTextTransform = FindDescendant("ui_setting_7_txt");
            shareRewardText = shareTextTransform != null
                ? shareTextTransform.GetComponent<TextMeshProUGUI>()
                : null;
        }

        if (rewardArrowBone == null)
        {
            Transform arrowBoneTransform = FindDescendant("arrow_Bone");
            rewardArrowBone = arrowBoneTransform != null
                ? arrowBoneTransform.GetComponent<BoneFollowerGraphic>()
                : null;
        }

        if (rewardPreviewText == null)
        {
            Transform rewardTextTransform = FindDescendant("ui_shengli1_08_txt");
            rewardPreviewText = rewardTextTransform != null
                ? rewardTextTransform.GetComponent<TextMeshProUGUI>()
                : null;
        }

        if (tipsText == null)
        {
            Transform tipsTransform = FindDescendant("tips_txt");
            tipsText = tipsTransform != null ? tipsTransform.GetComponent<TextMeshProUGUI>() : null;
        }

        if (nextButtonGroup == null)
        {
            Transform nextButtonTransform = FindDescendant("nextBtn");
            nextButtonGroup = nextButtonTransform != null
                ? nextButtonTransform.GetComponent<CanvasGroup>()
                : null;
        }

        if (nextButton == null)
        {
            Transform nextButtonTransform = FindDescendant("next_btn");
            nextButton = nextButtonTransform != null
                ? nextButtonTransform.GetComponent<CustomButton>()
                : null;
        }

        if (rewardButtonGroup == null)
        {
            Transform rewardGroupTransform = FindDescendant("BtnGroups");
            rewardButtonGroup = rewardGroupTransform != null
                ? rewardGroupTransform.GetComponent<CanvasGroup>()
                : null;
            rewardButtonGroupRect = rewardGroupTransform as RectTransform;
            if (rewardButtonGroupRect != null)
                rewardButtonGroupTargetPosition = rewardButtonGroupRect.anchoredPosition;
        }
    }

    #endregion

    #region Presentation Setup and Read-only Data

    private void ResetPresentationState()
    {
        StopTransientPresentation();
        RefreshBasePresentation();
        ResetRewardVariantPresentation();

        if (tipsText != null)
            tipsText.text = string.Empty;

        if (nextButton != null)
            nextButton.Interactable = false;

        if (nextButtonGroup != null)
        {
            nextButtonGroup.alpha = 0f;
            nextButtonGroup.interactable = false;
            nextButtonGroup.blocksRaycasts = false;
        }

        if (rewardButtonGroup != null)
        {
            rewardButtonGroup.alpha = 0f;
            rewardButtonGroup.interactable = false;
            rewardButtonGroup.blocksRaycasts = false;

            if (rewardButtonGroupRect != null)
            {
                rewardButtonGroupRect.anchoredPosition = new Vector2(
                    rewardButtonGroupTargetPosition.x,
                    rewardButtonGroupTargetPosition.y - Mathf.Max(0f, rewardButtonGroupEnterOffsetY));
            }

            rewardButtonDelayTween = DOVirtual
                .DelayedCall(Mathf.Max(0f, rewardButtonGroupDelay), ShowRewardButtonGroup)
                .SetUpdate(true);
        }
    }

    private void RefreshBasePresentation()
    {
        if (shareRewardText != null)
        {
            int currentLevel = PlayCtrl.Instance?.Level?.CurrentLevel ?? DB.GameData?.CurrentLevel ?? 0;
            List<int> sharedLevels = DB.GameData?.HasSharedLevel;
            shareRewardText.gameObject.SetActive(
                currentLevel <= 0 || sharedLevels == null || !sharedLevels.Contains(currentLevel));
            shareRewardText.text = "+" + ConfigManager.RemoteConfig.GetLevelVictoryShareCoinReward();
        }

        if (noAdButton == null)
            return;

        bool hasNoAds = HasNoAdsReadOnly();
        noAdButton.gameObject.SetActive(!hasNoAds);
        noAdButton.Interactable = !hasNoAds;
        if (hasNoAds)
            return;

        noAdButton.transform.localScale = Vector3.zero;
        noAdScaleTween = noAdButton.transform
            .DOScale(Vector3.one, 0.3f)
            .SetEase(Ease.OutBack)
            .SetDelay(1.5f);

        if (noAdBreathingScale != null)
        {
            noAdBreathingScale.SetStartScale(Vector3.one);
            noAdBreathingScale.StartScale();
        }
    }

    private static bool HasNoAdsReadOnly()
    {
        var purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager != null && purchaseManager.IsPurchased(NoAdProductName))
            return true;

        return GameManager.Instance?.Bag != null &&
               GameManager.Instance.Bag.GetToolCount(GameProps.NoAds) > 0;
    }

    private void ResetRewardVariantPresentation()
    {
        rewardSharePart?.RefreshData();

        if (rewardArrowBone != null)
            rewardArrowBone.enabled = true;

        RefreshRewardPreviewText();
    }

    private void RefreshRewardPreviewText()
    {
        if (rewardPreviewText != null && rewardSelector != null)
            rewardPreviewText.text = $"+ {rewardSelector.GetCurrentReward()}";
    }

    #endregion

    #region Spine Presentation

    private void PlaySpineEntry()
    {
        UnbindSpineCallbacks();
        if (spineCtrl == null)
        {
            OnSpineEntryCompleted();
            return;
        }

        Spine.TrackEntry entry = spineCtrl.PlayAnimation(InAnimationName, false);
        Spine.AnimationState state = spineCtrl.GetAnimationState();
        if (entry == null || state == null)
        {
            OnSpineEntryCompleted();
            return;
        }

        boundSpineState = state;
        boundSpineState.Event += OnSpineEvent;
        boundSpineState.Complete += OnSpineComplete;
    }

    private void OnSpineEvent(Spine.TrackEntry trackEntry, Spine.Event spineEvent)
    {
        if (!IsInAnimation(trackEntry) || spineEvent == null)
            return;

        if (spineEvent.Data.Name == "NextLevel")
            RevealNextButton();
    }

    private void OnSpineComplete(Spine.TrackEntry trackEntry)
    {
        if (!IsInAnimation(trackEntry))
            return;

        OnSpineEntryCompleted();
    }

    private void OnSpineEntryCompleted()
    {
        RevealNextButton();
        RefreshTipsPresentation();

        if (spineCtrl != null)
            spineCtrl.PlayAnimation(IdleAnimationName, true);

        UnbindSpineCallbacks();

        if (rewardSelector != null)
            rewardSelector.Play().Forget();
    }

    private static bool IsInAnimation(Spine.TrackEntry trackEntry)
    {
        return trackEntry?.Animation != null &&
               string.Equals(
                   trackEntry.Animation.Name,
                   InAnimationName,
                   StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Button and Tips Presentation

    private void RevealNextButton()
    {
        if (nextButtonGroup == null)
            return;

        nextButtonTween?.Kill();
        nextButtonTween = nextButtonGroup
            .DOFade(1f, Mathf.Max(0f, nextButtonFadeDuration))
            .SetDelay(Mathf.Max(0f, nextButtonFadeDelay))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (nextButtonGroup == null)
                    return;

                nextButtonGroup.alpha = 1f;
                nextButtonGroup.interactable = true;
                nextButtonGroup.blocksRaycasts = true;
            });
    }

    private void RefreshTipsPresentation()
    {
        if (tipsText == null)
        {
            UnlockNextButton();
            return;
        }

        LevelData levelData = PlayCtrl.Instance?.Level?.CurrentLevelData;
        string key = levelData?.CongraktTips;
        if (string.IsNullOrEmpty(key))
        {
            UnlockNextButton();
            return;
        }

        string translated = LocalizationManager.GetTranslation(key, false);
        if (string.IsNullOrEmpty(translated))
        {
            UnlockNextButton();
            return;
        }

        tipsText.text = translated;
        tipsText.UpdateFontAsset();
        tipsText.ForceMeshUpdate(true, true);
        tipsText.text = string.Empty;

        tipsTween?.Kill();
        tipsFallbackTween?.Kill();
        tipsTween = tipsText
            .DOText(translated, Mathf.Max(0f, tipsTypewriterDuration))
            .SetUpdate(true)
            .OnComplete(UnlockNextButton);
        tipsFallbackTween = DOVirtual
            .DelayedCall(2f, UnlockNextButton)
            .SetUpdate(true);
    }

    private void UnlockNextButton()
    {
        if (nextButton != null)
            nextButton.Interactable = true;
    }

    private void ShowRewardButtonGroup()
    {
        if (rewardArrowBone != null)
            rewardArrowBone.enabled = false;

        rewardSharePart?.Show();

        if (rewardButtonGroup == null)
            return;

        rewardButtonGroupTween?.Kill();
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        rewardButtonGroupTween = sequence;

        float duration = Mathf.Max(0f, rewardButtonGroupDuration);
        if (rewardButtonGroupRect != null)
        {
            sequence.Join(rewardButtonGroupRect
                .DOAnchorPos(rewardButtonGroupTargetPosition, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true));
        }

        sequence
            .Join(rewardButtonGroup.DOFade(1f, duration).SetUpdate(true))
            .OnComplete(() =>
            {
                if (rewardButtonGroup == null)
                    return;

                rewardButtonGroup.alpha = 1f;
                rewardButtonGroup.interactable = true;
                rewardButtonGroup.blocksRaycasts = true;
                rewardButtonGroupTween = null;
            });
    }

    #endregion

    #region Read-only Data and Effects

    private void RefreshReadOnlyStars()
    {
        Transform starGroup = FindDescendant(StarGroupName);
        if (starGroup == null)
            return;

        Toggle[] toggles = starGroup.GetComponentsInChildren<Toggle>(true);
        if (toggles.Length == 0)
            return;

        LevelData levelData = PlayCtrl.Instance?.Level?.CurrentLevelData;
        int visibleCount = levelData != null
            ? Mathf.Clamp(levelData.LevelSumStarCount, 0, toggles.Length)
            : toggles.Length;

        int collectedCount = 0;
        if (levelData != null && !string.IsNullOrEmpty(levelData.Id))
        {
            List<int> collectedStarIds = GameManager.Instance?.Postcard?.GetLevelCollectedStarIds(levelData.Id);
            if (collectedStarIds != null)
            {
                HashSet<int> uniqueValidIds = new HashSet<int>();
                for (int i = 0; i < collectedStarIds.Count; i++)
                {
                    int starId = collectedStarIds[i];
                    if (starId >= 1 && starId <= 3)
                        uniqueValidIds.Add(starId);
                }

                collectedCount = Mathf.Min(uniqueValidIds.Count, visibleCount);
            }
        }

        for (int i = 0; i < toggles.Length; i++)
        {
            Toggle toggle = toggles[i];
            if (toggle == null)
                continue;

            bool isVisible = i < visibleCount;
            bool isCollected = isVisible && i < collectedCount;
            toggle.gameObject.SetActive(isVisible);
            toggle.SetIsOnWithoutNotify(isCollected);

            Animator animator = toggle.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = !isCollected;

            Transform breath = toggle.transform.Find(StarBreathObjectName);
            if (breath != null)
                breath.gameObject.SetActive(isCollected);
        }
    }

    private void PlayOpenEffects()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != OpenConfettiObjectName)
                continue;

            ParticleSystem effect = candidate.GetComponent<ParticleSystem>();
            if (effect == null)
                continue;

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }
    }

    #endregion

    #region Interaction Guard and Lookup

    private void BlockBusinessButtons()
    {
        CustomButton[] buttons = GetComponentsInChildren<CustomButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].IsBlockClick = true;
        }
    }

    private void DisableInheritedClickBlocks()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == "ClickBlock")
                candidate.gameObject.SetActive(false);
        }
    }

    private Transform FindDescendant(string objectName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
                return candidate;
        }

        return null;
    }

    #endregion

    #region Cleanup

    private void StopTransientPresentation()
    {
        noAdScaleTween?.Kill();
        noAdScaleTween = null;

        tipsTween?.Kill();
        tipsTween = null;

        tipsFallbackTween?.Kill();
        tipsFallbackTween = null;

        nextButtonTween?.Kill();
        nextButtonTween = null;

        rewardButtonDelayTween?.Kill();
        rewardButtonDelayTween = null;

        rewardButtonGroupTween?.Kill();
        rewardButtonGroupTween = null;

        UnbindSpineCallbacks();
    }

    private void UnbindSpineCallbacks()
    {
        if (boundSpineState != null)
        {
            boundSpineState.Event -= OnSpineEvent;
            boundSpineState.Complete -= OnSpineComplete;
        }

        boundSpineState = null;
    }

    #endregion
}
