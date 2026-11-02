using System;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Config;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;

/// <summary>
/// DeepLink 关卡胜利后的专用结算面板。
/// 负责主页、重玩、最低未通过关卡导航、星级及现有 tips 的展示，不替换文案、不接入分享、免广告或奖励分支。
/// </summary>
public sealed class GameVictoryPanel_DeepLink : BaseUIView, IEventSender, IDebuger
{
    #region Constants

    private const float NextButtonFadeDuration = 0.2f;
    private const float NextButtonFadeDelay = 0.15f;
    private const float TipsTypewriterDuration = 1f;

    #endregion

    #region Inspector

    /// <summary>
    /// 用途：播放 DeepLink 胜利面板的入场与待机动画。
    /// 调用边界：必须由 GameVictoryPanel_DeepLink prefab 显式绑定。
    /// 注意事项：绑定缺失时会直接开放导航，不阻断结算流程。
    /// </summary>
    [SerializeField] private UISpineCtrl spineCtrl;

    /// <summary>
    /// 用途：配合 Spine 展开逐字显示 Prefab 已有的 tips 文案。
    /// 调用边界：必须显式绑定 tips_txt 的 TMP 组件；只控制可见字符数，不赋值或替换本地化文本。
    /// </summary>
    [SerializeField] private TextMeshProUGUI tipsText;

    /// <summary>
    /// 用途：控制下一关按钮的渐显和输入门禁。
    /// 调用边界：必须绑定 nextBtn 节点上的 CanvasGroup。
    /// </summary>
    [SerializeField] private CanvasGroup nextButtonGroup;

    /// <summary>
    /// 用途：返回应用主页。
    /// 调用边界：绑定由旧分享按钮改造的 home_btn，不执行任何分享逻辑。
    /// </summary>
    [SerializeField] private CustomButton homeButton;

    /// <summary>
    /// 用途：重玩本次胜利对应的关卡。
    /// 调用边界：主线与 Draw 重玩当前关；IWanner 沿用当前 Group 从第 0 小关重玩的既有语义。
    /// </summary>
    [SerializeField] private CustomButton replayButton;

    /// <summary>
    /// 用途：进入当前玩法按配置顺序最靠前的可玩未通过关卡。
    /// 调用边界：按钮只派发现有 EnterNextLevelEvent，目标解析与转场由活动玩法控制器负责。
    /// </summary>
    [SerializeField] private CustomButton nextButton;

    /// <summary>
    /// 用途：展示主线实际星级、Draw 实际星级或 IWanner 固定三星。
    /// 调用边界：必须绑定 prefab 已有的 Postcard_VictoryStarGroupCtrl。
    /// 注意事项：IWanner 只更新 UI，不写入任一玩法存档。
    /// </summary>
    [SerializeField] private Postcard_VictoryStarGroupCtrl victoryStarGroup;

    /// <summary>
    /// 用途：星星动画期间拦截胜利面板输入。
    /// 调用边界：必须绑定 prefab 已有 ClickBlock；静态星级展示不会开启拦截。
    /// </summary>
    [SerializeField] private GameObject clickBlock;

    /// <summary>
    /// 用途：每次打开专用面板时从头播放胜利粒子。
    /// 调用边界：由 prefab 显式维护需要重播的粒子列表，允许为空。
    /// </summary>
    [SerializeField] private ParticleSystem[] openEffects;

    #endregion

    #region Runtime State

    private Tween _nextButtonFadeTween;
    private Tween _tipsTween;
    private bool _isTipsPresentationStarted;
    private bool _isEntranceComplete;
    private bool _isStarPresentationComplete;
    private bool _isNavigationTriggered;

    #endregion

    #region UI Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();

        ValidateInspectorReferences();
        if (homeButton != null)
            homeButton.OnClick += OnHome;
        if (replayButton != null)
            replayButton.OnClick += OnReplay;
        if (nextButton != null)
            nextButton.OnClick += OnNextLevel;
    }

    public override void OnOpen()
    {
        base.OnOpen();

        ResetOpenState();
        PlayOpenEffects();
        PlayOpenAnimation();
        AudioManager.PlaySound("success");
    }

    public override void OnClose()
    {
        StopTransientAnimations();
        UnbindSpineCallback();
        base.OnClose();
    }

    #endregion

    #region Configuration

    /// <summary>
    /// 用途：按主线明信片星级刷新专用面板，并消费本轮等待播放的星星数量。
    /// 调用边界：仅由主线 DeepLink 胜利工厂在创建面板后调用。
    /// </summary>
    /// <param name="levelData">本次完成的主线关卡配置。</param>
    public void ConfigureMainStars(LevelData levelData)
    {
        if (victoryStarGroup == null)
        {
            GameManager.Instance?.Postcard?.ConsumeCollectedStarCount(levelData?.Id);
            CompleteStarPresentation();
            return;
        }

        bool isAnimationPlaying = victoryStarGroup.Refresh(levelData, CompleteStarPresentation);
        SetStarAnimationBlocked(isAnimationPlaying);
        if (!isAnimationPlaying)
            CompleteStarPresentation();
    }

    /// <summary>
    /// 用途：按 Draw 永久星级刷新专用面板，并消费本轮等待动画记录。
    /// 调用边界：仅由 Draw DeepLink 胜利工厂在星级已写入后调用。
    /// </summary>
    /// <param name="levelData">本次完成的 Draw 关卡配置。</param>
    public void ConfigureDrawStars(DrawLevelData levelData)
    {
        int collectedStarCount = ConsumeDrawWaitingStars(levelData, out int waitingAnimStarCount);
        if (victoryStarGroup == null)
        {
            CompleteStarPresentation();
            return;
        }

        bool isAnimationPlaying = victoryStarGroup.RefreshStars(
            collectedStarCount,
            waitingAnimStarCount,
            CompleteStarPresentation);
        SetStarAnimationBlocked(isAnimationPlaying);
        if (!isAnimationPlaying)
            CompleteStarPresentation();
    }

    /// <summary>
    /// 用途：读取 Dig 最高星并一次性消费待播记录，沿用 Draw 专属胜利的保存与播放顺序。
    /// 调用边界：仅由 Dig DeepLink 胜利工厂在星级已保存后调用，不读写其它玩法存档。
    /// </summary>
    /// <param name="levelData">本次完成的 Dig 关卡配置，使用真实配置 Id 查询星数。</param>
    public void ConfigureDigStars(DigLevelData levelData)
    {
        int collectedStarCount = ConsumeDigWaitingStars(levelData, out int waitingAnimStarCount);
        if (victoryStarGroup == null)
        {
            CompleteStarPresentation();
            return;
        }

        bool isAnimationPlaying = victoryStarGroup.RefreshStars(
            collectedStarCount,
            waitingAnimStarCount,
            CompleteStarPresentation);
        SetStarAnimationBlocked(isAnimationPlaying);
        if (!isAnimationPlaying)
            CompleteStarPresentation();
    }

    /// <summary>
    /// 用途：把 IWanner DeepLink 胜利表现固定为三颗星。
    /// 调用边界：仅由 IWanner 完整 Group 结算时调用。
    /// 注意事项：该方法只修改当前 UI，不读取或写入玩法存档。
    /// </summary>
    public void ConfigureIWannerStars()
    {
        victoryStarGroup?.RefreshStaticStars(3);
        CompleteStarPresentation();
    }

    #endregion

    #region Button Handlers

    private void OnHome(CustomButton _)
    {
        if (!TryBeginNavigation())
            return;

        GameManager manager = GameManager.Instance;
        string error = "GameManager is unavailable.";
        if (manager != null && manager.ReturnToStartPanel(out error))
        {
            UIManager.Instance?.DestroyUI(this);
            return;
        }

        this.LogError($"[GameVictoryPanel_DeepLink] Return home failed. error:{error}");
        if (!IsOpen)
        {
            UIManager.Instance?.DestroyUI(this);
            return;
        }

        CancelNavigation();
    }

    private void OnReplay(CustomButton _)
    {
        if (!TryBeginNavigation())
            return;

        if (UIManager.Instance == null)
        {
            this.LogError("[GameVictoryPanel_DeepLink] Replay failed. UIManager is null.");
            CancelNavigation();
            return;
        }

        UIManager.Instance.DestroyUI(this);
        this.DispatchEvent(Witness<GameReplayEvent>._);
    }

    private void OnNextLevel(CustomButton _)
    {
        if (!TryBeginNavigation())
            return;

        if (UIManager.Instance == null)
        {
            this.LogError("[GameVictoryPanel_DeepLink] Next level failed. UIManager is null.");
            CancelNavigation();
            return;
        }

        UIManager.Instance.DestroyUI(this);
        this.DispatchEvent(Witness<EnterNextLevelEvent>._);
    }

    #endregion

    #region Presentation

    private void ValidateInspectorReferences()
    {
        if (spineCtrl == null || homeButton == null || replayButton == null ||
            nextButton == null || victoryStarGroup == null || tipsText == null)
        {
            this.LogError("[GameVictoryPanel_DeepLink] Required Inspector references are missing.");
        }
    }

    private void ResetOpenState()
    {
        StopTransientAnimations();
        UnbindSpineCallback();
        _isEntranceComplete = false;
        _isStarPresentationComplete = false;
        _isNavigationTriggered = false;
        _isTipsPresentationStarted = false;
        if (tipsText != null)
            tipsText.maxVisibleCharacters = 0;
        if (nextButtonGroup != null)
        {
            nextButtonGroup.alpha = 0f;
            nextButtonGroup.interactable = false;
            nextButtonGroup.blocksRaycasts = false;
        }

        SetStarAnimationBlocked(false);
        RefreshButtonStates();
    }

    private void PlayOpenEffects()
    {
        if (openEffects == null)
            return;

        for (int i = 0; i < openEffects.Length; i++)
        {
            ParticleSystem effect = openEffects[i];
            if (effect == null)
                continue;

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }
    }

    private void PlayOpenAnimation()
    {
        if (spineCtrl == null)
        {
            CompleteEntrance();
            return;
        }

        Spine.TrackEntry track = spineCtrl.PlayAnimation("in", false);
        Spine.AnimationState state = spineCtrl.GetAnimationState();
        if (track == null || state == null)
        {
            CompleteEntrance();
            return;
        }

        state.Complete += OnSpineAnimationComplete;
        state.Event += OnSpineEvent;
    }

    private void OnSpineEvent(Spine.TrackEntry track, Spine.Event spineEvent)
    {
        if (track?.Animation == null || spineEvent?.Data == null ||
            !string.Equals(track.Animation.Name, "in", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(spineEvent.Data.Name, "Text", StringComparison.Ordinal))
            PlayTipsAnimation();
    }

    private void OnSpineAnimationComplete(Spine.TrackEntry track)
    {
        if (track?.Animation == null || !string.Equals(track.Animation.Name, "in", StringComparison.OrdinalIgnoreCase))
            return;

        CompleteEntrance();
    }

    private void CompleteEntrance()
    {
        if (_isEntranceComplete)
            return;

        _isEntranceComplete = true;
        PlayTipsAnimation();
        UnbindSpineCallback();
        if (spineCtrl != null)
            spineCtrl.PlayAnimation("idle", true);

        if (nextButtonGroup != null)
        {
            _nextButtonFadeTween = nextButtonGroup
                .DOFade(1f, NextButtonFadeDuration)
                .SetDelay(NextButtonFadeDelay)
                .SetUpdate(true);
        }

        RefreshButtonStates();
    }

    private void PlayTipsAnimation()
    {
        if (!IsOpen || _isTipsPresentationStarted)
            return;

        _isTipsPresentationStarted = true;
        if (tipsText == null)
            return;

        tipsText.ForceMeshUpdate(true, true);
        int characterCount = tipsText.textInfo.characterCount;
        if (characterCount <= 0)
        {
            tipsText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        _tipsTween = DOTween.To(
                () => tipsText.maxVisibleCharacters,
                value => tipsText.maxVisibleCharacters = value,
                characterCount,
                TipsTypewriterDuration)
            .SetEase(Ease.Linear)
            .SetTarget(tipsText)
            .SetUpdate(true)
            .OnComplete(() => tipsText.maxVisibleCharacters = int.MaxValue);
    }

    private void CompleteStarPresentation()
    {
        _isStarPresentationComplete = true;
        SetStarAnimationBlocked(false);
        RefreshButtonStates();
    }

    private void SetStarAnimationBlocked(bool isBlocked)
    {
        if (clickBlock != null)
            clickBlock.SetActive(isBlocked);
    }

    private bool TryBeginNavigation()
    {
        if (_isNavigationTriggered || !_isEntranceComplete || !_isStarPresentationComplete)
            return false;

        _isNavigationTriggered = true;
        RefreshButtonStates();
        return true;
    }

    private void CancelNavigation()
    {
        _isNavigationTriggered = false;
        RefreshButtonStates();
    }

    private void RefreshButtonStates()
    {
        bool interactable = _isEntranceComplete && _isStarPresentationComplete && !_isNavigationTriggered;
        if (homeButton != null)
            homeButton.Interactable = interactable;
        if (replayButton != null)
            replayButton.Interactable = interactable;
        if (nextButton != null)
            nextButton.Interactable = interactable;
        if (nextButtonGroup != null)
        {
            nextButtonGroup.interactable = interactable;
            nextButtonGroup.blocksRaycasts = interactable;
        }
    }

    private void StopTransientAnimations()
    {
        _nextButtonFadeTween?.Kill();
        _nextButtonFadeTween = null;
        _tipsTween?.Kill();
        _tipsTween = null;
        if (tipsText != null)
            tipsText.maxVisibleCharacters = int.MaxValue;
    }

    private void UnbindSpineCallback()
    {
        Spine.AnimationState state = spineCtrl?.GetAnimationState();
        if (state != null)
        {
            state.Complete -= OnSpineAnimationComplete;
            state.Event -= OnSpineEvent;
        }
    }

    #endregion

    #region Draw Star Data

    private int ConsumeDrawWaitingStars(DrawLevelData levelData, out int waitingAnimStarCount)
    {
        waitingAnimStarCount = 0;
        if (levelData == null || string.IsNullOrEmpty(levelData.Id))
            return 0;

        var saveData = DB.DrawGame;
        if (saveData == null)
        {
            this.LogWarning($"[GameVictoryPanel_DeepLink] Draw star save is unavailable. levelId:{levelData.Id}");
            return 0;
        }

        bool changed = saveData.EnsureValidCollections();
        int collectedStarCount = saveData.LevelCollectedStar.TryGetValue(levelData.Id, out int count)
            ? Mathf.Clamp(count, 0, 3)
            : 0;
        if (saveData.LevelWaitingAnimStar.TryGetValue(levelData.Id, out int waitingCount))
            waitingAnimStarCount = Mathf.Clamp(waitingCount, 0, collectedStarCount);
        if (saveData.LevelWaitingAnimStar.Remove(levelData.Id))
            changed = true;

        if (changed && !DB.SaveDrawGame())
        {
            this.LogWarning(
                $"[GameVictoryPanel_DeepLink] Failed to consume Draw waiting stars. levelId:{levelData.Id}");
        }

        return collectedStarCount;
    }

    #endregion

    #region Dig Star Data

    /// <summary>从独立 Dig 存档缓存当前真实关卡的星数；待播项在播放前删除并保存，与 Draw 相同。</summary>
    private int ConsumeDigWaitingStars(DigLevelData levelData, out int waitingAnimStarCount)
    {
        waitingAnimStarCount = 0;
        if (levelData == null || string.IsNullOrEmpty(levelData.Id))
            return 0;

        var saveData = DB.DigGame;
        if (saveData == null)
        {
            this.LogWarning($"[GameVictoryPanel_DeepLink] Dig 星级存档不可用。levelId:{levelData.Id}");
            return 0;
        }

        bool changed = saveData.EnsureValidCollections();
        int collectedStarCount = saveData.LevelCollectedStar.TryGetValue(levelData.Id, out int count)
            ? Mathf.Clamp(count, 0, 3)
            : 0;
        if (saveData.LevelWaitingAnimStar.TryGetValue(levelData.Id, out int waitingCount))
            waitingAnimStarCount = Mathf.Clamp(waitingCount, 0, collectedStarCount);
        if (saveData.LevelWaitingAnimStar.Remove(levelData.Id))
            changed = true;

        if (changed && !DB.SaveDigGame())
            this.LogWarning($"[GameVictoryPanel_DeepLink] 消费 Dig 待播星保存失败。levelId:{levelData.Id}");

        return collectedStarCount;
    }

    #endregion

    #region Factory

    /// <summary>
    /// 用途：打开主线 DeepLink 专用胜利面板并注入明信片星级。
    /// 调用边界：仅由主线成功状态在 DeepLink 上下文有效时调用。
    /// </summary>
    public static GameVictoryPanel_DeepLink Create(LevelData levelData)
    {
        if (levelData == null || UIManager.Instance == null)
            return null;

        GameVictoryPanel_DeepLink panel = OpenPanel();
        panel?.ConfigureMainStars(levelData);
        return panel;
    }

    /// <summary>
    /// 用途：打开 Draw DeepLink 专用胜利面板并注入 Draw 星级。
    /// 调用边界：仅由 DrawPlayCtrl 在星级写入后调用。
    /// </summary>
    public static GameVictoryPanel_DeepLink Create(DrawLevelData levelData)
    {
        if (levelData == null || UIManager.Instance == null)
            return null;

        GameVictoryPanel_DeepLink panel = OpenPanel();
        panel?.ConfigureDrawStars(levelData);
        return panel;
    }

    /// <summary>
    /// 用途：打开 IWanner DeepLink 专用胜利面板并固定显示三星。
    /// 调用边界：仅由 IWannerPlayCtrl 在完整 Group 结算后调用。
    /// </summary>
    public static GameVictoryPanel_DeepLink CreateForIWanner()
    {
        if (UIManager.Instance == null)
            return null;

        GameVictoryPanel_DeepLink panel = OpenPanel();
        panel?.ConfigureIWannerStars();
        return panel;
    }

    /// <summary>
    /// 用途：打开 Dig DeepLink 专属胜利面板，展示历史最高星与本次新增星动画。
    /// 调用边界：仅由 DigPlayCtrl 在已确认 DeepLink 来源且星级写入后调用；不读取或消费其它玩法存档。
    /// 注意事项：面板只消费独立 Dig 待播项；通关与解锁进度继续由控制器在面板创建后保存。
    /// </summary>
    /// <param name="levelData">本次完成的 Dig 普通关配置。</param>
    public static GameVictoryPanel_DeepLink CreateForDig(DigLevelData levelData)
    {
        if (levelData == null || UIManager.Instance == null)
            return null;

        GameVictoryPanel_DeepLink panel = OpenPanel();
        panel?.ConfigureDigStars(levelData);
        return panel;
    }

    private static GameVictoryPanel_DeepLink OpenPanel()
    {
        return UIManager.Instance.OpenUI<GameVictoryPanel_DeepLink>(
            nameof(GameVictoryPanel_DeepLink),
            UILayer.Popup);
    }

    #endregion
}
