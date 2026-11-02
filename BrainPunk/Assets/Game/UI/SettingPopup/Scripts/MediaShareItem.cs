using System;
using Bear.SaveModule;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using GameCommon;
using TMPro;
using UnityEngine;

public enum MediaShareType
{
    Youtube,
    Instagram,
    Discord,
    Tiktok
}

/// <summary>
/// First click on a media-share entry grants the coin reward once, then records the claim in GameSetting.
/// The button itself stays clickable so any existing URL-opening behavior on the same button can keep working.
/// </summary>
public class MediaShareItem : MonoBehaviour
{
    public MediaShareType Type;

    [SerializeField] private CustomButton button;
    [SerializeField] private CommonUIRewardAnimCtrl rewardAnimCtrl;
    [SerializeField] private GameObject coinItem;
    [SerializeField] private TextMeshProUGUI rewardTxt;

    private bool _isBound;
    private CustomButtonOpenUrl _openUrl;
    private Transform _pendingRewardStart;
    private bool _hasPendingRewardAnimation;
    private bool _hasLeftApplicationSinceReward;
    private bool _isApplicationPaused;
    private bool _hasApplicationFocus;
    private int _pendingRewardVersion;

    private void Awake()
    {
        if (button == null)
            button = GetComponentInChildren<CustomButton>(true);

        _openUrl = GetComponent<CustomButtonOpenUrl>();
        _hasApplicationFocus = Application.isFocused;
    }

    private void OnEnable()
    {
        BindButton();
        RefreshView();
    }

    private void OnDisable()
    {
        if (!_isBound || button == null)
            return;

        button.OnClick -= OnClickShare;
        _isBound = false;
    }

    private void OnValidate()
    {
        if (button == null)
            button = GetComponentInChildren<CustomButton>(true);
    }

    private void BindButton()
    {
        if (_isBound || button == null)
            return;

        button.OnClick += OnClickShare;
        _isBound = true;
    }

    private void OnClickShare(CustomButton _)
    {
        bool hasClaimed = HasClaimed();
        if (!hasClaimed)
        {
            Transform rewardStart = coinItem != null ? coinItem.transform : button != null ? button.transform : transform;

            var coin = ConfigManager.RemoteConfig.GetMediaShareCoinReward();
            var bag = GameManager.Instance?.Bag;
            if (bag == null)
            {
                Debug.LogError($"[MediaShareItem] Reward grant failed. Bag is null. type:{Type}", this);
                return;
            }

            bag.AddTool(Config.Game.GameProps.Coin, coin, RewardType.Media);
            Debug.Log($"[MediaShareItem] Reward granted. type:{Type}, amount:{coin}", this);
            SetClaimed(true);
            RefreshView();
            QueueRewardAnimation(rewardStart);
        }
        else
        {
            Debug.Log($"[MediaShareItem] Reward grant skipped. Already claimed. type:{Type}", this);
        }

        _openUrl?.OpenUrl();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        _isApplicationPaused = pauseStatus;
        if (pauseStatus)
        {
            _hasLeftApplicationSinceReward |= _hasPendingRewardAnimation;
            return;
        }

        TrySchedulePendingRewardAfterResume();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        _hasApplicationFocus = hasFocus;
        if (!hasFocus)
        {
            _hasLeftApplicationSinceReward |= _hasPendingRewardAnimation;
            return;
        }

        TrySchedulePendingRewardAfterResume();
    }

    private void QueueRewardAnimation(Transform rewardStart)
    {
        _pendingRewardStart = rewardStart;
        _hasPendingRewardAnimation = true;
        _hasLeftApplicationSinceReward = false;
        int version = ++_pendingRewardVersion;

        Debug.Log($"[MediaShareItem] Reward animation queued before opening external URL. type:{Type}, version:{version}", this);
        PlayPendingRewardIfUrlDidNotLeaveAppAsync(version).Forget();
    }

    private async UniTask PlayPendingRewardIfUrlDidNotLeaveAppAsync(int version)
    {
        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(0.75f),
                ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version != _pendingRewardVersion || !_hasPendingRewardAnimation)
            return;

        if (_hasLeftApplicationSinceReward || _isApplicationPaused || !_hasApplicationFocus)
            return;

        Debug.Log($"[MediaShareItem] External URL did not pause the app; playing queued reward animation. type:{Type}, version:{version}", this);
        await PlayPendingRewardAsync(version);
    }

    private void TrySchedulePendingRewardAfterResume()
    {
        if (!_hasPendingRewardAnimation
            || !_hasLeftApplicationSinceReward
            || _isApplicationPaused
            || !_hasApplicationFocus)
        {
            return;
        }

        int version = _pendingRewardVersion;
        Debug.Log($"[MediaShareItem] App resumed; scheduling queued reward animation. type:{Type}, version:{version}", this);
        PlayPendingRewardAsync(version).Forget();
    }

    private async UniTask PlayPendingRewardAsync(int version)
    {
        try
        {
            await UniTask.NextFrame(cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version != _pendingRewardVersion
            || !_hasPendingRewardAnimation
            || _isApplicationPaused
            || !_hasApplicationFocus)
        {
            return;
        }

        _hasPendingRewardAnimation = false;
        Transform rewardStart = _pendingRewardStart != null ? _pendingRewardStart : button != null ? button.transform : transform;
        _pendingRewardStart = null;

        if (rewardAnimCtrl == null)
        {
            Debug.LogWarning($"[MediaShareItem] Queued reward animation skipped. Controller is null. type:{Type}, version:{version}", this);
            return;
        }

        CommonUIRewardPlayResult result = await rewardAnimCtrl.PlayFrom(rewardStart, 1);
        if (result == CommonUIRewardPlayResult.Completed)
        {
            Debug.Log($"[MediaShareItem] Queued reward animation completed. type:{Type}, version:{version}", this);
        }
        else
        {
            Debug.LogWarning(
                $"[MediaShareItem] Queued reward animation did not complete. type:{Type}, version:{version}, result:{result}",
                this);
        }
    }

    private void RefreshView()
    {
        if (coinItem != null)
            coinItem.SetActive(!HasClaimed());

        if (rewardTxt)
        {
            var coin = ConfigManager.RemoteConfig.GetMediaShareCoinReward();
            rewardTxt.text = "+" + coin;
        }

    }

    private bool HasClaimed()
    {
        GameSetting setting = DB.GameSetting;
        if (setting == null)
            return false;

        switch (Type)
        {
            case MediaShareType.Youtube:
                return setting.HasClickYoutube;
            case MediaShareType.Instagram:
                return setting.HasClickInstagram;
            case MediaShareType.Discord:
                return setting.HasClickDiscord;
            case MediaShareType.Tiktok:
                return setting.HasClickTiktok;
            default:
                return false;
        }
    }

    private void SetClaimed(bool claimed)
    {
        GameSetting setting = DB.GameSetting;
        if (setting == null)
        {
            Debug.LogWarning($"[MediaShareItem] DB.GameSetting is null. Share reward state was not saved. type:{Type}", this);
            return;
        }

        switch (Type)
        {
            case MediaShareType.Youtube:
                setting.HasClickYoutube = claimed;
                break;
            case MediaShareType.Instagram:
                setting.HasClickInstagram = claimed;
                break;
            case MediaShareType.Discord:
                setting.HasClickDiscord = claimed;
                break;
            case MediaShareType.Tiktok:
                setting.HasClickTiktok = claimed;
                break;
        }

        setting.Save();
    }
}
