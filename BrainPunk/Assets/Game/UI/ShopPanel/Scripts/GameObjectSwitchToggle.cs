using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 完善功能，切换 On/Off状态，一次显示一个 Part。并且提供一个 IsOnCallback
/// </summary>
public class GameObjectSwitchToggle : MonoBehaviour
{
    [Serializable]
    private sealed class BoolEvent : UnityEvent<bool>
    {
    }

    [SerializeField] private CustomButton TriggerBtn;

    [SerializeField] private GameObject OnPart;
    [SerializeField] private GameObject OffPart;

    [SerializeField] private BoolEvent IsOnCallback;

    public event Action<bool> OnIsOnChanged;

    public bool IsOn => _isOn;

    private bool _isOn;
    private bool _isBound;

    private void Awake()
    {
        _isOn = ResolveInitialState();
    }

    private void OnEnable()
    {
        BindTrigger();
        ApplyState(_isOn, false);
    }

    private void OnDisable()
    {
        UnbindTrigger();
    }

    private void OnDestroy()
    {
        UnbindTrigger();
    }

    public void Toggle()
    {
        SetIsOn(!_isOn);
    }

    public void SetIsOn(bool isOn)
    {
        ApplyState(isOn, true);
    }

    public void SetIsOnWithoutNotify(bool isOn)
    {
        ApplyState(isOn, false);
    }

    public void SetOn()
    {
        SetIsOn(true);
    }

    public void SetOff()
    {
        SetIsOn(false);
    }

    private void BindTrigger()
    {
        if (_isBound)
        {
            return;
        }

        if (TriggerBtn != null)
        {
            TriggerBtn.OnClick += OnTriggerClicked;
        }

        _isBound = true;
    }

    private void UnbindTrigger()
    {
        if (!_isBound)
        {
            return;
        }

        if (TriggerBtn != null)
        {
            TriggerBtn.OnClick -= OnTriggerClicked;
        }

        _isBound = false;
    }

    private void OnTriggerClicked(CustomButton _)
    {
        Toggle();
    }

    private bool ResolveInitialState()
    {
        bool hasOnState = OnPart != null && OnPart.activeSelf;
        bool hasOffState = OffPart != null && OffPart.activeSelf;

        if (hasOnState && !hasOffState)
        {
            return true;
        }

        if (hasOffState && !hasOnState)
        {
            return false;
        }

        return false;
    }

    private void ApplyState(bool isOn, bool notifyOnChanged)
    {
        bool changed = _isOn != isOn;
        _isOn = isOn;

        SetActiveIfNeeded(OnPart, isOn);
        SetActiveIfNeeded(OffPart, !isOn);

        if (notifyOnChanged && changed)
        {
            IsOnCallback?.Invoke(_isOn);
            OnIsOnChanged?.Invoke(_isOn);
        }
    }

    private static void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
