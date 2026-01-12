using Bear.EventSystem;
using Config;
using UnityEngine;

/// <summary>
/// 关卡状态，未解锁关闭，广告解锁，已解锁，已通关
/// </summary>
public partial class LevelChoiceItem : MonoBehaviour, IAutoUIBind
{
    private enum State
    {
        // 可以进入
        Normal,
        // 关卡等级不够
        LevelLock,
        // 广告锁定
        AdLock,
        // 已经通关
        Passed
    }

    private State _state;
    private LevelData _levelData;
    private ChoiceLevelPanel _owner;

    public void Init(ChoiceLevelPanel panel)
    {
        _owner = panel;
    }

    public void SetData(LevelData data)
    {
        _levelData = data;

        if (data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _state = State.Normal;
        gameObject.SetActive(true);

        RefreshState();

        switch (_state)
        {
            case State.Passed:
            case State.Normal:
                ClickAreaBtn.OnClick += EnterLevel;
                break;
            case State.AdLock:
                ClickAreaBtn.OnClick += ShowRewardAd;
                break;
            case State.LevelLock:
                break;
        }
    }

    private void RefreshState()
    {

        bool isPassed = false;
        bool isUnlock = PlayCtrl.Instance.Level.CanEnterLevel(_levelData.Id);

        if (!isUnlock)
            _state = State.LevelLock;

        if (_levelData.LockType != Config.Level.LevelLockType.Unlock)
        {
            if (!PlayCtrl.Instance.Level.IsUnlock(_levelData.Id))
            {
                isUnlock = false;
                if (_levelData.LockType == Config.Level.LevelLockType.Ad)
                    _state = State.AdLock;

            }
        }

        if (isUnlock)
            isPassed = PlayCtrl.Instance.Level.IsPassed(_levelData.Id);
        if (isPassed)
            _state = State.Passed;

        // refresh
        AdImg.gameObject.SetActive(_state == State.AdLock);
        PassedImg.gameObject.SetActive(_state == State.Passed);
        LockImg.gameObject.SetActive(_state == State.AdLock || _state == State.LevelLock);
    }

    private void ShowRewardAd(CustomButton btn)
    {
        if (_levelData == null)
            return;

        PlayCtrl.Instance.Level.UnlockLevel(_levelData.Id);
    }

    private void EnterLevel(CustomButton btn)
    {
        _owner.EnterLevel(_levelData);
    }
}
