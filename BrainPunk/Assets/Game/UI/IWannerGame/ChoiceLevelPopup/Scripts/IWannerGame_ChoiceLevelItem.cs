using System;
using Config;
using TMPro;
using UnityEngine;

/// <summary>
/// IWanner Group 选项，负责显示 LevelSort Id、四态表现并把点击结果回传给选择弹窗。
/// Boss Group 的专属美术由选择弹窗改用 Boss Item prefab 提供，本组件仍按普通进度规则切换状态。
/// </summary>
public sealed class IWannerGame_ChoiceLevelItem : MonoBehaviour
{
    private enum State
    {
        Normal,
        Passed,
        Current,
        Lock
    }

    #region References

    /// <summary>
    /// 用途：显示普通的已解锁 Group 状态。
    /// 调用边界：仅由 SetData 根据 IWanner 存档与配置切换显隐。
    /// 注意事项：与 passedState、currentState、lockState、bossState 互斥。
    /// </summary>
    [SerializeField] private GameObject normalState;

    /// <summary>
    /// 用途：显示已经完整通关的 Group 状态。
    /// 调用边界：仅由 SetData 根据 IWanner 存档与配置切换显隐。
    /// 注意事项：Group Id 必须存在于 DB.IWannerGame.PassedLevels 才会显示。
    /// </summary>
    [SerializeField] private GameObject passedState;

    /// <summary>
    /// 用途：显示玩家当前未通关且仍可进入的 Group；没有有效当前记录时显示顺序进度 Group。
    /// 调用边界：仅由 SetData 根据 IWanner 存档与配置切换显隐。
    /// 注意事项：当前状态使用 Popup 按实际 CurrentGroupID 与顺序进度共同解析后的 currentGroupId 判定。
    /// </summary>
    [SerializeField] private GameObject currentState;

    /// <summary>
    /// 用途：显示尚未解锁的 Group 状态。
    /// 调用边界：仅由 SetData 根据 IWanner 存档与配置切换显隐。
    /// 注意事项：本字段只负责表现，不在 Item 内发起广告或写入解锁存档。
    /// </summary>
    [SerializeField] private GameObject lockState;

    /// <summary>
    /// 用途：显示 normalState 对应的 Group Id。
    /// 调用边界：仅由 SetData 刷新文本；必须在 Item prefab Inspector 中绑定 normal_txt。
    /// 注意事项：不得改为运行时层级查找，避免美术层级调整后静默绑定错误文本。
    /// </summary>
    [SerializeField] private TMP_Text normalTxt;

    /// <summary>
    /// 用途：显示 passedState 对应的 Group Id。
    /// 调用边界：仅由 SetData 刷新文本；必须在 Item prefab Inspector 中绑定 passed_txt。
    /// 注意事项：不得与其它状态文本交叉绑定。
    /// </summary>
    [SerializeField] private TMP_Text passedTxt;

    /// <summary>
    /// 用途：显示 lockState 对应的 Group Id。
    /// 调用边界：仅由 SetData 刷新文本；必须在 Item prefab Inspector 中绑定 lock_txt。
    /// 注意事项：本字段只负责显示，不参与广告解锁或存档写入。
    /// </summary>
    [SerializeField] private TMP_Text lockTxt;

    #endregion

    #region Runtime State

    private IWannerLevelSort _levelSort;
    private Action<IWannerLevelSort> _onClick;
    private Action<IWannerLevelSort> _onLockedClick;
    private Action _onTbnClick;
    private Action _onTbnLockedClick;
    private int _currentGroupId;
    private State _state;
    private bool _isTbn;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 用途：绑定一个 IWanner Group 配置、当前表现 Group Id，并刷新四态显示和点击回调。
    /// 调用边界：仅由 IWannerGame_ChoiceLevelPopup 创建克隆项后调用一次。
    /// 注意事项：普通状态回调进入请求，Lock 状态只回调锁定处理，不得直接绕过解锁存档。
    /// </summary>
    public void SetData(
        IWannerLevelSort levelSort,
        int currentGroupId,
        Action<IWannerLevelSort> onClick,
        Action<IWannerLevelSort> onLockedClick)
    {
        _levelSort = levelSort;
        _currentGroupId = currentGroupId;
        _onClick = onClick;
        _onLockedClick = onLockedClick;
        _onTbnClick = null;
        _onTbnLockedClick = null;
        _isTbn = false;

        RefreshLevelTexts();
        RefreshState();
    }

    /// <summary>
    /// 用途：把当前 prefab 实例设置为 LevelSort 之外的 TBN 占位项，并刷新 unlock/lock 两态。
    /// 调用边界：仅由 IWannerGame_ChoiceLevelPopup 创建末尾 TBN Item 后调用一次。
    /// 注意事项：TBN 不绑定 IWannerLevelSort；锁定回调只能显示提示，不得转入广告解锁流程。
    /// </summary>
    public void SetTbnData(
        bool isUnlocked,
        Action onClick,
        Action onLockedClick)
    {
        _levelSort = null;
        _currentGroupId = 0;
        _onClick = null;
        _onLockedClick = null;
        _onTbnClick = onClick;
        _onTbnLockedClick = onLockedClick;
        _isTbn = true;

        RefreshLevelTexts();
        _state = isUnlocked ? State.Current : State.Lock;
        RefreshStateObjects();
    }

    #endregion

    #region Interaction

    private void BindButtons()
    {
        SetButtonBinding(normalState, true);
        SetButtonBinding(passedState, true);
        SetButtonBinding(currentState, true);
        SetButtonBinding(lockState, true);
    }

    private void UnbindButtons()
    {
        SetButtonBinding(normalState, false);
        SetButtonBinding(passedState, false);
        SetButtonBinding(currentState, false);
        SetButtonBinding(lockState, false);
    }

    private void SetButtonBinding(GameObject stateRoot, bool shouldBind)
    {
        CustomButton stateButton = stateRoot == null
            ? null
            : stateRoot.GetComponent<CustomButton>();
        if (stateButton == null)
            return;

        stateButton.OnClick -= OnButtonClicked;
        if (shouldBind)
            stateButton.OnClick += OnButtonClicked;
    }

    private void OnButtonClicked(CustomButton _)
    {
        if (_isTbn)
        {
            if (_state == State.Lock)
                _onTbnLockedClick?.Invoke();
            else
                _onTbnClick?.Invoke();
            return;
        }

        if (_levelSort == null)
            return;

        if (_state == State.Lock)
        {
            _onLockedClick?.Invoke(_levelSort);
            return;
        }

        _onClick?.Invoke(_levelSort);
    }

    #endregion

    #region State Presentation

    private void RefreshLevelTexts()
    {
        string levelId = _levelSort?.Id.ToString() ?? string.Empty;
        SetLevelText(normalTxt, levelId);
        SetLevelText(passedTxt, levelId);
        SetLevelText(lockTxt, levelId);
    }

    private static void SetLevelText(TMP_Text stateText, string levelId)
    {
        if (stateText != null)
            stateText.text = levelId;
    }

    private void RefreshState()
    {
        _state = ResolveState(_levelSort, _currentGroupId);
        RefreshStateObjects();
    }

    private void RefreshStateObjects()
    {
        SetStateActive(normalState, _state == State.Normal);
        SetStateActive(passedState, _state == State.Passed);
        SetStateActive(currentState, _state == State.Current);
        SetStateActive(lockState, _state == State.Lock);
    }

    private static State ResolveState(IWannerLevelSort levelSort, int currentGroupId)
    {
        if (levelSort == null)
            return State.Lock;

        var saveData = DB.IWannerGame;
        if (saveData?.PassedLevels?.Contains(levelSort.Id) == true)
            return State.Passed;

        if (currentGroupId == levelSort.Id)
            return State.Current;

        bool isUnlocked = levelSort.LockType == Config.Level.LevelLockType.Unlock ||
                          saveData?.UnlockGroups?.Contains(levelSort.Id) == true;
        return isUnlocked ? State.Normal : State.Lock;
    }

    private static void SetStateActive(GameObject stateRoot, bool isActive)
    {
        if (stateRoot != null)
            stateRoot.SetActive(isActive);
    }

    #endregion
}
