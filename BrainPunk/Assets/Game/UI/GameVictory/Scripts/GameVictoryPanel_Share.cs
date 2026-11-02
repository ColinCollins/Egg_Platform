using System.Collections.Generic;
using Bear.Logger;
using Config;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ConfigModule;
using GameCommon;
using TMPro;
using UnityEngine;

/// <summary>
/// Controls the like/dislike/share button row in the victory panel.
/// Buttons stay hidden until <see cref="Show"/> is called.
/// </summary>
public class GameVictoryPanel_Share : MonoBehaviour, IDebuger
{
    // Buttons enter from an offset position, then exit further left after selection.
    private const float ShowDuration = 0.35f;
    private const float HideDuration = 0.2f;
    private const float ShowStagger = 0.06f;
    private const float EnterOffsetX = 179;
    private const float ExitOffsetX = 179;

    [SerializeField] private RectTransform likeBtnTransform;
    [SerializeField] private RectTransform dislikeBtnTransform;
    [SerializeField] private RectTransform shareBtnTransform;
    [SerializeField] private CommonUIRewardAnimCtrl rewardAnimCtrl;

    [SerializeField] private TextMeshProUGUI shareTxt;

    private CustomButton _likeBtn;
    private CustomButton _dislikeBtn;
    private CustomButton _shareBtn;

    private Vector2 _likeTargetPosition;
    private Vector2 _dislikeTargetPosition;
    private Vector2 _shareTargetPosition;

    private bool _buttonsBound;
    private bool _hasCachedTargetPositions;
    // Like/dislike is a single-choice pair, while share remains independently clickable.
    private bool _isReactionLocked;
    private bool _isShareLocked;
    private Tween _likeTween;
    private Tween _dislikeTween;
    private Tween _shareTween;
    private int _cachedCurrentLevel;
    private string _cachedDrawLevelId;
    private int _cachedIWannerGroupId = -1;
    private bool _usesDrawLevelContext;
    private bool _usesIWannerContext;
    private bool _usesDigLevelContext;
    private string _cachedDigLevelId;

    private string levelPath;

    private void Awake()
    {
        CacheButtons();
        CacheTargetPositions();
    }

    private string GetCurrentLevelPathOrDefault()
    {
        var playCtrl = PlayCtrl.Instance;
        var level = playCtrl?.Level;
        var levelData = level?.CurrentLevelData;
        if (levelData == null)
        {
            UnityEngine.Debug.LogWarning("CurrentLevelData is null when reporting game analytics. Use 'no_start' as level path.");
            return "no_start";
        }

        return levelData.Path;
    }

    public void RefreshData()
    {
        _usesDigLevelContext = false;
        _cachedDigLevelId = null;
        _usesDrawLevelContext = false;
        _usesIWannerContext = false;
        _cachedDrawLevelId = null;
        _cachedIWannerGroupId = -1;
        _cachedCurrentLevel = GetCurrentLevel();
        levelPath = GetCurrentLevelPathOrDefault();
        this.Log($"Current VictoryLevel: {_cachedCurrentLevel}， levelPath: {levelPath}");
    }

    /// <summary>
    /// 注入 Draw 胜利面板的分享上下文。
    /// 调用边界：仅由 DrawGame_GameVictoryPanel.Configure 在运行时调用。
    /// 注意事项：分享奖励领取状态写入 DB.DrawGame，不得与主线 HasSharedLevel 混用。
    /// </summary>
    public void RefreshDrawData(DrawLevelData levelData)
    {
        _usesDigLevelContext = false;
        _cachedDigLevelId = null;
        _usesDrawLevelContext = true;
        _usesIWannerContext = false;
        _cachedCurrentLevel = 0;
        _cachedIWannerGroupId = -1;
        _cachedDrawLevelId = levelData?.Id;
        levelPath = string.IsNullOrEmpty(levelData?.Path) ? "no_start" : levelData.Path;
        this.Log($"Current Draw VictoryLevel: {_cachedDrawLevelId}， levelPath: {levelPath}");
    }

    /// <summary>
    /// 注入 IWanner Reward 胜利面板的评价与分享上下文。
    /// 调用边界：仅由 IWannerGame_GameVictoryPanel_Reward 在运行时调用。
    /// 注意事项：评价和分享领取状态写入 DB.IWannerGame，不得与主线或 Draw 存档混用。
    /// </summary>
    /// <param name="groupId">当前已完成的 IWannerLevelSort.Id。</param>
    /// <param name="currentLevelPath">当前 Group 最后一小关的配置 Path，用于胜利评价打点。</param>
    public void RefreshIWannerData(int groupId, string currentLevelPath)
    {
        _usesDigLevelContext = false;
        _cachedDigLevelId = null;
        _usesDrawLevelContext = false;
        _usesIWannerContext = true;
        _cachedCurrentLevel = 0;
        _cachedDrawLevelId = null;
        _cachedIWannerGroupId = groupId;
        levelPath = string.IsNullOrEmpty(currentLevelPath) ? "no_start" : currentLevelPath;
        this.Log($"Current IWanner VictoryGroup: {_cachedIWannerGroupId}， levelPath: {levelPath}");
    }

    /// <summary>Dig 胜利面板注入专属评价/分享上下文；禁止落到 Main、Draw 或 IWanner 存档。</summary>
    public void RefreshDigData(DigLevelData levelData)
    {
        _usesDigLevelContext = true;
        _usesDrawLevelContext = false;
        _usesIWannerContext = false;
        _cachedCurrentLevel = 0;
        _cachedDrawLevelId = null;
        _cachedIWannerGroupId = -1;
        _cachedDigLevelId = levelData?.Id;
        levelPath = string.IsNullOrEmpty(levelData?.Path) ? "no_start" : levelData.Path;
    }

    private void OnEnable()
    {
        CacheButtons();
        BindButtons();
        ResetView();
    }

    private void OnDisable()
    {
        UnbindButtons();
        KillTweens();
    }

    public void Show()
    {
        bool showReactionButtons = !HasCompletedReaction();
        bool showShareButton = !HasSharedCurrentLevel();
        if (!showReactionButtons && !showShareButton)
        {
            KillTweens();
            SetButtonVisible(likeBtnTransform, false);
            SetButtonVisible(dislikeBtnTransform, false);
            SetButtonVisible(shareBtnTransform, false);
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        KillTweens();
        _isReactionLocked = false;
        _isShareLocked = false;

        // Refresh share reward text every time the panel is shown.
        if (shareTxt != null)
        {
            shareTxt.gameObject.SetActive(showShareButton);
            int coins = ConfigManager.RemoteConfig.GetLevelVictoryShareCoinReward();
            shareTxt.text = "+" + coins;
        }

        // Reset to off-screen start positions before replaying the reveal animation.
        PrepareButtonForShow(likeBtnTransform, _likeBtn, _likeTargetPosition, showReactionButtons);
        PrepareButtonForShow(dislikeBtnTransform, _dislikeBtn, _dislikeTargetPosition, showReactionButtons);

        PrepareButtonForShow(shareBtnTransform, _shareBtn, _shareTargetPosition, showShareButton);

        _likeTween = showReactionButtons ? PlayShowTween(likeBtnTransform, _likeTargetPosition, 0f) : null;
        _dislikeTween = showReactionButtons ? PlayShowTween(dislikeBtnTransform, _dislikeTargetPosition, ShowStagger) : null;
        _shareTween = showShareButton ? PlayShowTween(shareBtnTransform, _shareTargetPosition, ShowStagger * 2f) : null;
    }

    private void ResetView()
    {
        _isReactionLocked = false;
        _isShareLocked = false;
        KillTweens();
        SetButtonVisible(likeBtnTransform, false);
        SetButtonVisible(dislikeBtnTransform, false);
        SetButtonVisible(shareBtnTransform, false);
        SetButtonInteractable(_likeBtn, true);
        SetButtonInteractable(_dislikeBtn, true);
        SetButtonInteractable(_shareBtn, true);
    }

    private void CacheButtons()
    {
        if (likeBtnTransform != null && _likeBtn == null)
            _likeBtn = likeBtnTransform.GetComponent<CustomButton>();

        if (dislikeBtnTransform != null && _dislikeBtn == null)
            _dislikeBtn = dislikeBtnTransform.GetComponent<CustomButton>();

        if (shareBtnTransform != null && _shareBtn == null)
            _shareBtn = shareBtnTransform.GetComponent<CustomButton>();
    }

    private void CacheTargetPositions()
    {
        if (_hasCachedTargetPositions)
            return;

        // Cache the authored layout once so tween endpoints stay stable across multiple shows.
        if (likeBtnTransform != null)
            _likeTargetPosition = likeBtnTransform.anchoredPosition;

        if (dislikeBtnTransform != null)
            _dislikeTargetPosition = dislikeBtnTransform.anchoredPosition;

        if (shareBtnTransform != null)
            _shareTargetPosition = shareBtnTransform.anchoredPosition;

        _hasCachedTargetPositions = true;
    }

    private void BindButtons()
    {
        if (_buttonsBound)
            return;

        if (_likeBtn != null)
        {
            _likeBtn.OnClick += OnClickLike;
        }

        if (_dislikeBtn != null)
        {
            _dislikeBtn.OnClick += OnClickDislike;
        }

        if (_shareBtn != null)
            _shareBtn.OnClick += OnClickShare;

        _buttonsBound = true;

        // Share reward text follows the current remote-config reward amount.
        int coins = ConfigManager.RemoteConfig.GetLevelVictoryShareCoinReward();
        shareTxt.text = "+" + coins;
    }

    private void UnbindButtons()
    {
        if (!_buttonsBound)
            return;

        if (_likeBtn != null)
            _likeBtn.OnClick -= OnClickLike;

        if (_dislikeBtn != null)
            _dislikeBtn.OnClick -= OnClickDislike;

        if (_shareBtn != null)
            _shareBtn.OnClick -= OnClickShare;

        _buttonsBound = false;
    }

    private void OnClickLike(CustomButton btn)
    {
        if (!TryLockReactionButtons())
            return;

        MarkReactionCompleted(true);
        GameSDKService.Instance.LevelVictoryReaction(levelPath, true);


        PlayHideTween(likeBtnTransform, _likeTargetPosition, ref _likeTween);
        PlayHideTween(dislikeBtnTransform, _dislikeTargetPosition, ref _dislikeTween);
    }

    private void OnClickDislike(CustomButton btn)
    {
        if (!TryLockReactionButtons())
            return;

        MarkReactionCompleted(false);
        GameSDKService.Instance.LevelVictoryReaction(levelPath, false);

        PlayHideTween(likeBtnTransform, _likeTargetPosition, ref _likeTween);
        PlayHideTween(dislikeBtnTransform, _dislikeTargetPosition, ref _dislikeTween);
    }

    private void OnClickShare(CustomButton btn)
    {
        if (!TryLockShareButton())
            return;

        ShareStoreUrl();

        if (shareTxt != null)
            shareTxt.gameObject.SetActive(false);

        if (GrantShareReward() && rewardAnimCtrl != null)
            rewardAnimCtrl.PlayFrom(shareBtnTransform != null ? shareBtnTransform : transform, 1).Forget();

        PlayHideTween(shareBtnTransform, _shareTargetPosition, ref _shareTween);
    }

    private bool TryLockReactionButtons()
    {
        if (_isReactionLocked || HasCompletedReaction())
            return false;

        _isReactionLocked = true;
        SetButtonInteractable(_likeBtn, false);
        SetButtonInteractable(_dislikeBtn, false);
        return true;
    }

    private bool HasCompletedReaction()
    {
        if (_usesDigLevelContext)
            return !string.IsNullOrEmpty(_cachedDigLevelId) && DB.DigGame.HasReactionLevel.ContainsKey(_cachedDigLevelId);
        if (_usesIWannerContext)
        {
            if (_cachedIWannerGroupId <= 0)
                return false;

            Dictionary<int, int> reactionGroups = DB.IWannerGame?.ReactionGroupResults;
            return reactionGroups != null && reactionGroups.ContainsKey(_cachedIWannerGroupId);
        }

        if (_usesDrawLevelContext)
        {
            if (string.IsNullOrEmpty(_cachedDrawLevelId))
                return false;

            Dictionary<string, int> drawReactionLevels = DB.DrawGame?.HasReactionLevel;
            return drawReactionLevels != null && drawReactionLevels.ContainsKey(_cachedDrawLevelId);
        }

        if (string.IsNullOrEmpty(levelPath) || levelPath == "no_start")
            return false;

        Dictionary<string, int> reactionLevels = DB.GameData?.HasReactionLevel;
        return reactionLevels != null && reactionLevels.ContainsKey(levelPath);
    }

    private void MarkReactionCompleted(bool isLike)
    {
        int reactionValue = isLike ? 1 : 0;
        if (_usesDigLevelContext)
        {
            if (string.IsNullOrEmpty(_cachedDigLevelId)) return;
            var digGame = DB.DigGame;
            digGame.EnsureValidCollections();
            if (digGame.HasReactionLevel.ContainsKey(_cachedDigLevelId)) return;
            digGame.HasReactionLevel.Add(_cachedDigLevelId, reactionValue);
            if (!DB.SaveDigGame()) this.LogError("[GameVictoryPanel_Share] Dig 评价保存失败。");
            return;
        }
        if (_usesIWannerContext)
        {
            var iwannerGame = DB.IWannerGame;
            if (iwannerGame == null || _cachedIWannerGroupId <= 0)
            {
                this.LogError("[GameVictoryPanel_Share] IWanner reaction state save failed. context is invalid.");
                return;
            }

            iwannerGame.EnsureValidCollections();
            if (iwannerGame.ReactionGroupResults.ContainsKey(_cachedIWannerGroupId))
                return;

            iwannerGame.ReactionGroupResults.Add(_cachedIWannerGroupId, reactionValue);
            if (!DB.SaveIWannerGame())
                this.LogError("[GameVictoryPanel_Share] IWanner reaction state save failed.");
            return;
        }

        if (_usesDrawLevelContext)
        {
            var drawGame = DB.DrawGame;
            if (drawGame == null || string.IsNullOrEmpty(_cachedDrawLevelId))
            {
                this.LogError("[GameVictoryPanel_Share] Draw reaction state save failed. context is invalid.");
                return;
            }

            if (drawGame.HasReactionLevel == null)
                drawGame.HasReactionLevel = new Dictionary<string, int>();

            if (drawGame.HasReactionLevel.ContainsKey(_cachedDrawLevelId))
                return;

            drawGame.HasReactionLevel.Add(_cachedDrawLevelId, reactionValue);
            if (!DB.SaveDrawGame())
                this.LogError("[GameVictoryPanel_Share] Draw reaction state save failed.");
            return;
        }

        var gameData = DB.GameData;
        if (gameData == null || string.IsNullOrEmpty(levelPath) || levelPath == "no_start")
        {
            this.LogError("[GameVictoryPanel_Share] Main reaction state save failed. context is invalid.");
            return;
        }

        if (gameData.HasReactionLevel == null)
            gameData.HasReactionLevel = new Dictionary<string, int>();

        if (gameData.HasReactionLevel.ContainsKey(levelPath))
            return;

        gameData.HasReactionLevel.Add(levelPath, reactionValue);
        if (!gameData.Save())
            this.LogError("[GameVictoryPanel_Share] Main reaction state save failed.");
    }

    private bool TryLockShareButton()
    {
        if (_isShareLocked)
            return false;

        _isShareLocked = true;
        SetButtonInteractable(_shareBtn, false);
        return true;
    }

    private void UnlockShareButton()
    {
        _isShareLocked = false;
        SetButtonInteractable(_shareBtn, shareBtnTransform != null && shareBtnTransform.gameObject.activeSelf);
    }

    private bool GrantShareReward()
    {
        if (HasSharedCurrentLevel())
            return false;

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError("[GameVictoryPanel_Share] Share reward grant failed. Bag is null.");
            return false;
        }

        if (!HasValidShareRewardContext())
        {
            this.LogError("[GameVictoryPanel_Share] Share reward grant failed. current level is invalid.");
            return false;
        }

        int coins = ConfigManager.RemoteConfig.GetLevelVictoryShareCoinReward();
        bag.AddTool(Config.Game.GameProps.Coin, coins, RewardType.Media);

        // Share reward is limited to one claim per level and persisted in the active gameplay save context.
        MarkCurrentLevelShared();
        return true;
    }

    protected void ShareStoreUrl()
    {
        string url = "https://link.brainpunk.studio/store/install";
#if UNITY_ANDROID
        // url = "https://play.google.com/store/apps/details?id=com.brain.tricky.puzzle.games.test.quest";
        AndroidShare.ShareText(url.Trim());
#elif UNITY_IOS
        // url = "https://apps.apple.com/us/app/brain-riddle-tricky-box/id6757749388";
        IosShare.ShareText(url.Trim());
#endif
    }

    private bool HasSharedCurrentLevel()
    {
        if (_usesDigLevelContext)
            return !string.IsNullOrEmpty(_cachedDigLevelId) && DB.DigGame.SharedLevelIds.Contains(_cachedDigLevelId);
        if (_usesIWannerContext)
        {
            if (_cachedIWannerGroupId <= 0)
                return false;

            List<int> sharedGroupIds = DB.IWannerGame?.SharedGroupIds;
            return sharedGroupIds != null && sharedGroupIds.Contains(_cachedIWannerGroupId);
        }

        if (_usesDrawLevelContext)
        {
            if (string.IsNullOrEmpty(_cachedDrawLevelId))
                return false;

            List<string> sharedLevelIds = DB.DrawGame?.SharedLevelIds;
            return sharedLevelIds != null && sharedLevelIds.Contains(_cachedDrawLevelId);
        }

        if (_cachedCurrentLevel <= 0)
            return false;

        List<int> sharedLevels = DB.GameData?.HasSharedLevel;
        return sharedLevels != null && sharedLevels.Contains(_cachedCurrentLevel);
    }

    private int GetCurrentLevel()
    {
        return PlayCtrl.Instance?.Level != null
            ? PlayCtrl.Instance.Level.CurrentLevel
            : DB.GameData?.CurrentLevel ?? 0;
    }

    private bool HasValidShareRewardContext()
    {
        if (_usesDigLevelContext) return !string.IsNullOrEmpty(_cachedDigLevelId);
        if (_usesIWannerContext)
            return _cachedIWannerGroupId > 0;

        return _usesDrawLevelContext
            ? !string.IsNullOrEmpty(_cachedDrawLevelId)
            : _cachedCurrentLevel > 0;
    }

    private void MarkCurrentLevelShared()
    {
        if (_usesDigLevelContext)
        {
            if (string.IsNullOrEmpty(_cachedDigLevelId)) return;
            var digGame = DB.DigGame;
            digGame.EnsureValidCollections();
            if (digGame.SharedLevelIds.Contains(_cachedDigLevelId)) return;
            digGame.SharedLevelIds.Add(_cachedDigLevelId);
            if (!DB.SaveDigGame()) this.LogError("[GameVictoryPanel_Share] Dig 分享保存失败。");
            return;
        }
        if (_usesIWannerContext)
        {
            var iwannerGame = DB.IWannerGame;
            if (iwannerGame == null)
            {
                this.LogError("[GameVictoryPanel_Share] IWanner share reward state save failed. IWannerGame is null.");
                return;
            }

            iwannerGame.EnsureValidCollections();
            if (iwannerGame.SharedGroupIds.Contains(_cachedIWannerGroupId))
                return;

            iwannerGame.SharedGroupIds.Add(_cachedIWannerGroupId);
            if (!DB.SaveIWannerGame())
                this.LogError("[GameVictoryPanel_Share] IWanner share reward state save failed.");
            return;
        }

        if (_usesDrawLevelContext)
        {
            var drawGame = DB.DrawGame;
            if (drawGame == null)
            {
                this.LogError("[GameVictoryPanel_Share] Draw share reward state save failed. DrawGame is null.");
                return;
            }

            if (drawGame.SharedLevelIds == null)
                drawGame.SharedLevelIds = new List<string>();

            if (drawGame.SharedLevelIds.Contains(_cachedDrawLevelId))
                return;

            drawGame.SharedLevelIds.Add(_cachedDrawLevelId);
            DB.SaveDrawGame();
            return;
        }

        var gameData = DB.GameData;
        if (gameData == null)
        {
            this.LogError("[GameVictoryPanel_Share] Share reward state save failed. GameData is null.");
            return;
        }

        if (gameData.HasSharedLevel == null)
            gameData.HasSharedLevel = new List<int>();

        if (gameData.HasSharedLevel.Contains(_cachedCurrentLevel))
            return;

        gameData.HasSharedLevel.Add(_cachedCurrentLevel);
        gameData.Save();
    }

    private void PrepareButtonForShow(RectTransform target, CustomButton button, Vector2 anchoredTarget, bool visible = true)
    {
        if (target == null)
            return;

        target.anchoredPosition = new Vector2(anchoredTarget.x - EnterOffsetX, anchoredTarget.y);
        SetButtonVisible(target, visible);
        if (button != null)
            button.Interactable = visible;
    }

    private Tween PlayShowTween(RectTransform target, Vector2 anchoredTarget, float delay)
    {
        if (target == null || !target.gameObject.activeSelf)
            return null;

        return target
            .DOAnchorPos(anchoredTarget, ShowDuration)
            .SetDelay(delay)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    private void PlayHideTween(RectTransform target, Vector2 anchoredTarget, ref Tween tween)
    {
        if (target == null || !target.gameObject.activeSelf)
            return;

        tween?.Kill();
        // Selected buttons leave the row and remain hidden until the next Show call.
        tween = target
            .DOAnchorPos(new Vector2(anchoredTarget.x - ExitOffsetX, anchoredTarget.y), HideDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .SetDelay(.5f)
            .OnComplete(() =>
            {
                SetButtonVisible(target, false);
                HideGroupIfCompleted();
            });
    }

    private void HideGroupIfCompleted()
    {
        bool hasVisibleButton =
            (likeBtnTransform != null && likeBtnTransform.gameObject.activeSelf) ||
            (dislikeBtnTransform != null && dislikeBtnTransform.gameObject.activeSelf) ||
            (shareBtnTransform != null && shareBtnTransform.gameObject.activeSelf);
        if (!hasVisibleButton && gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void SetButtonVisible(RectTransform target, bool visible)
    {
        if (target != null)
            target.gameObject.SetActive(visible);
    }

    private void SetButtonInteractable(CustomButton button, bool interactable)
    {
        if (button != null)
            button.Interactable = interactable;
    }

    private void KillTweens()
    {
        _likeTween?.Kill();
        _likeTween = null;

        _dislikeTween?.Kill();
        _dislikeTween = null;

        _shareTween?.Kill();
        _shareTween = null;
    }
}
