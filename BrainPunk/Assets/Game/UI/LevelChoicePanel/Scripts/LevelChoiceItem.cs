using System;
using Bear.EventSystem;
using Config;
using DG.Tweening;
using Game.Events;
using I2.Loc;
using UnityEngine;

/// <summary>
/// 关卡状态，未解锁关闭，广告解锁，已解锁，已通关
/// </summary>
public partial class LevelChoiceItem : BaseAutoUIBind, IEventSender
{
    private enum State
    {
        // 可以进入
        Normal,
        // 最大关卡等级
        Max,
        // 关卡等级不够
        LevelLock,
        // 广告锁定
        AdLock,
        // 已经通关
        Passed
    }

    [SerializeField] private Postcard_ChoiceLevelStarGroupCtrl starGroupCtrl;

    public Color[] LevelStateColor;

    private State _state;
    private LevelSort _levelSort;
    private LevelData _levelData;
    private ChoiceLevelPanel _owner;

    public CanvasGroup canvasGroup;

    private Tween _canvasGroupAlphaTween;

    public void Init(ChoiceLevelPanel panel)
    {
        _owner = panel;
    }

    public void SetCanvasGroupAlpha(float value, float duration = 0f, float delay = 0f)
    {
        value = Mathf.Clamp01(value);
        delay = Mathf.Max(0f, delay);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            return;

        _canvasGroupAlphaTween?.Kill();
        _canvasGroupAlphaTween = null;

        if (duration <= 0f)
        {
            if (delay <= 0f)
            {
                canvasGroup.alpha = value;
                return;
            }

            _canvasGroupAlphaTween = DOVirtual.DelayedCall(delay, () =>
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = value;
            }).SetUpdate(true);
            return;
        }

        _canvasGroupAlphaTween = canvasGroup.DOFade(value, duration).SetDelay(delay).SetUpdate(true);
    }

    public void SetData(LevelSort levelSort)
    {
        if (levelSort == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _levelSort = levelSort;
        var data = PlayCtrl.Instance.Level.GetLevelDataById(_levelSort.Path);
        _levelData = data;

        if (data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _state = State.Normal;
        gameObject.SetActive(true);

        RefreshState();
        BtnClearEvents();

        switch (_state)
        {
            case State.Passed:
            case State.Max:
            case State.Normal:
                ClickAreaBtn.OnClick += EnterLevel;
                break;
            case State.AdLock:
                ClickAreaBtn.OnClick += ShowRewardAd;
                break;
            case State.LevelLock:
                ClickAreaBtn.OnClick += ShowTips;
                break;
        }
    }

    private void ShowTips(CustomButton btn)
    {
        SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_TBC"));
    }

    private void BtnClearEvents()
    {
        ClickAreaBtn.OnClick -= EnterLevel;
        ClickAreaBtn.OnClick -= ShowRewardAd;
        ClickAreaBtn.OnClick -= ShowTips;
    }

    private void RefreshState()
    {
        bool isPassed = false;
        bool isMax = false;

        // 未完待续关卡
        bool isUnlock = PlayCtrl.Instance.Level.IsUnlock(_levelSort.Id);

        if (!isUnlock)
        {
            _state = State.LevelLock;

            if (_levelData.LockType != Config.Level.LevelLockType.Unlock)
            {
                if (!PlayCtrl.Instance.Level.IsUnlock(_levelSort.Id))
                {
                    isUnlock = false;
                    if (_levelData.LockType == Config.Level.LevelLockType.Ad)
                        _state = State.AdLock;

                }
                else
                {
                    isUnlock = true;
                    _state = State.Normal;
                }
            }
        }

        if (isUnlock)
        {
            isMax = PlayCtrl.Instance.Level.MaxLevel == _levelSort.Id;
            if (!isMax)
                isPassed = PlayCtrl.Instance.Level.IsPassed(_levelSort.Id);
        }


        if (isMax)
            _state = State.Max;

        if (isPassed)
            _state = State.Passed;

        // refresh
        PassedState.SetActive(_state == State.Passed);
        NormalState.SetActive(_state == State.Normal);
        CurrentState.SetActive(_state == State.Max);
        AdLockState.SetActive(_state != State.Passed && _state != State.Normal && _state != State.Max);
        AdImg.gameObject.SetActive(_state == State.AdLock);

        NormalTxt.text = _levelSort.Id.ToString();
        PassedTxt.text = CurrentTxt.text = AdLockTxt.text = NormalTxt.text;

        if (starGroupCtrl)
            starGroupCtrl.Refresh(_levelData, _state != State.AdLock && _state != State.LevelLock, _state != State.Max);
    }

    private void ShowRewardAd(CustomButton btn)
    {
        if (_levelData == null)
            return;

        RewardAdHelper.TryToShowRewardAd(Config.Game.RewardPlacement.OutGameUnlockLevel.ToString(),
        success: "U_HurryTips_RewardVideo_succeed",
        failed: "U_HurryTips_RewardVideo_failed",
        onResult: OnRewardAdCallback);
    }

    private void OnRewardAdCallback(string placement, bool isSuc)
    {
        if (isSuc)
        {
            PlayCtrl.Instance.Level.UnlockLevel(_levelSort.Id);
            _owner.EnterLevel(_levelSort);
        }
    }

    private void EnterLevel(CustomButton btn)
    {
        _owner.EnterLevel(_levelSort);
    }

    private void OnDestroy()
    {
        _canvasGroupAlphaTween?.Kill();
        _canvasGroupAlphaTween = null;

        BtnClearEvents();
    }

    public override void Init()
    {
        // throw new System.NotImplementedException();
    }

#if UNITY_EDITOR
    private static GUIStyle s_levelPathGuiStyle;

    private void OnGUI()
    {
        if (!UnityEditor.EditorApplication.isPlaying || !isActiveAndEnabled || _levelSort == null)
            return;

        var levelPath = _levelSort.Path;
        if (string.IsNullOrEmpty(levelPath))
            return;

        var rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        var canvas = GetComponentInParent<Canvas>();
        var screenPoint = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, rectTransform.position);
        var labelSize = GetLevelPathGuiStyle().CalcSize(new GUIContent(levelPath));
        var labelRect = new Rect(
            screenPoint.x - labelSize.x * 0.5f - 8f,
            Screen.height - screenPoint.y - rectTransform.rect.height * 0.5f - labelSize.y - 6f,
            labelSize.x + 16f,
            labelSize.y + 6f);

        GUI.Box(labelRect, GUIContent.none);
        GUI.Label(labelRect, levelPath, GetLevelPathGuiStyle());
    }

    private static GUIStyle GetLevelPathGuiStyle()
    {
        if (s_levelPathGuiStyle != null)
            return s_levelPathGuiStyle;

        s_levelPathGuiStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 30,
            normal =
            {
                textColor = Color.white
            }
        };
        s_levelPathGuiStyle.padding = new RectOffset(8, 8, 3, 3);
        return s_levelPathGuiStyle;
    }
#endif
}
