using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls a set of GameObjectSwitchToggle instances as an exclusive group.
/// </summary>
public class GameObjectToggleGroup : MonoBehaviour
{
    [Serializable]
    private sealed class ToggleEvent : UnityEvent<GameObjectSwitchToggle>
    {
    }

    [SerializeField] private List<GameObjectSwitchToggle> toggles;
    [SerializeField] private bool allowSwitchOff;
    [SerializeField] private int defaultOnIndex;
    [SerializeField] private ToggleEvent SelectionChangedCallback;

    private readonly Dictionary<GameObjectSwitchToggle, Action<bool>> _toggleHandlers = new();
    private bool _isApplyingGroupState;
    private GameObjectSwitchToggle _selectedToggle;

    public event Action<GameObjectSwitchToggle> OnSelectionChanged;

    public GameObjectSwitchToggle SelectedToggle => _selectedToggle;
    public int SelectedIndex => GetToggleIndex(_selectedToggle);

    private void OnEnable()
    {
        BindToggles();
        NormalizeGroupState(false);
    }

    private void OnDisable()
    {
        UnbindToggles();
    }

    private void OnDestroy()
    {
        UnbindToggles();
    }

    public void SelectToggle(GameObjectSwitchToggle targetToggle)
    {
        if (targetToggle == null || !ContainsToggle(targetToggle))
        {
            return;
        }

        ApplySelection(targetToggle, true, true);
    }

    public void SelectToggleWithoutNotify(GameObjectSwitchToggle targetToggle)
    {
        if (targetToggle == null || !ContainsToggle(targetToggle))
        {
            return;
        }

        ApplySelection(targetToggle, false, false);
    }

    public void SelectIndex(int index)
    {
        var targetToggle = GetToggleAt(index);
        if (targetToggle != null)
        {
            ApplySelection(targetToggle, true, true);
        }
    }

    /// <summary>
    /// 用途：按组内顺序获取指定 Toggle，供需要定位既有 Toggle 的局部 UI 引导使用。
    /// 调用边界：仅在运行态主线程即时查询；索引顺序与当前序列化 toggles 列表一致。
    /// 注意事项：索引越界或对应引用缺失时返回 null，调用方不得修改组内集合。
    /// </summary>
    internal GameObjectSwitchToggle GetToggleAtIndex(int index)
    {
        return GetToggleAt(index);
    }

    public void ClearSelection()
    {
        if (!allowSwitchOff)
        {
            return;
        }

        ApplySelection(null, true, false);
    }

    public void RegisterToggle(GameObjectSwitchToggle targetToggle)
    {
        if (targetToggle == null)
        {
            return;
        }

        toggles ??= new List<GameObjectSwitchToggle>();

        if (!toggles.Contains(targetToggle))
        {
            toggles.Add(targetToggle);
        }

        if (isActiveAndEnabled)
        {
            BindToggle(targetToggle);
            NormalizeGroupState(true);
        }
    }

    public void UnregisterToggle(GameObjectSwitchToggle targetToggle)
    {
        if (targetToggle == null || toggles == null)
        {
            return;
        }

        UnbindToggle(targetToggle);

        bool wasSelected = _selectedToggle == targetToggle;
        while (toggles.Remove(targetToggle))
        {
        }

        if (!wasSelected)
        {
            return;
        }

        SetSelectedToggle(null, true);

        if (isActiveAndEnabled && !allowSwitchOff)
        {
            NormalizeGroupState(true);
        }
    }

    private void BindToggles()
    {
        if (toggles == null)
        {
            return;
        }

        foreach (var toggle in toggles)
        {
            BindToggle(toggle);
        }
    }

    private void BindToggle(GameObjectSwitchToggle targetToggle)
    {
        if (targetToggle == null || _toggleHandlers.ContainsKey(targetToggle))
        {
            return;
        }

        Action<bool> handler = isOn => OnToggleIsOnChanged(targetToggle, isOn);
        targetToggle.OnIsOnChanged += handler;
        _toggleHandlers.Add(targetToggle, handler);
    }

    private void UnbindToggles()
    {
        foreach (var pair in _toggleHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.OnIsOnChanged -= pair.Value;
            }
        }

        _toggleHandlers.Clear();
    }

    private void UnbindToggle(GameObjectSwitchToggle targetToggle)
    {
        if (targetToggle == null || !_toggleHandlers.TryGetValue(targetToggle, out var handler))
        {
            return;
        }

        targetToggle.OnIsOnChanged -= handler;
        _toggleHandlers.Remove(targetToggle);
    }

    private void OnToggleIsOnChanged(GameObjectSwitchToggle changedToggle, bool isOn)
    {
        if (_isApplyingGroupState || changedToggle == null)
        {
            return;
        }

        if (isOn)
        {
            ApplySelection(changedToggle, true, false);
            return;
        }

        if (_selectedToggle != changedToggle)
        {
            return;
        }

        if (allowSwitchOff)
        {
            ApplySelection(null, true, false);
        }
        else
        {
            ApplySelection(changedToggle, false, false);
        }
    }

    private void NormalizeGroupState(bool notifySelectionChanged)
    {
        var activeToggle = GetFirstOnToggle();
        if (activeToggle != null)
        {
            ApplySelection(activeToggle, notifySelectionChanged, false);
            return;
        }

        if (allowSwitchOff)
        {
            ApplySelection(null, notifySelectionChanged, false);
            return;
        }

        ApplySelection(GetDefaultToggle(), notifySelectionChanged, false);
    }

    private void ApplySelection(
        GameObjectSwitchToggle selectedToggle,
        bool notifySelectionChanged,
        bool notifySelectedToggle)
    {
        if (selectedToggle != null && !ContainsToggle(selectedToggle))
        {
            return;
        }

        if (selectedToggle == null && !allowSwitchOff)
        {
            return;
        }

        _isApplyingGroupState = true;

        try
        {
            if (toggles != null)
            {
                foreach (var toggle in toggles)
                {
                    if (toggle == null)
                    {
                        continue;
                    }

                    bool shouldBeOn = toggle == selectedToggle;
                    if (notifySelectedToggle && shouldBeOn)
                    {
                        toggle.SetIsOn(true);
                    }
                    else
                    {
                        toggle.SetIsOnWithoutNotify(shouldBeOn);
                    }
                }
            }
        }
        finally
        {
            _isApplyingGroupState = false;
        }

        SetSelectedToggle(selectedToggle, notifySelectionChanged);
    }

    private void SetSelectedToggle(GameObjectSwitchToggle selectedToggle, bool notifySelectionChanged)
    {
        if (_selectedToggle == selectedToggle)
        {
            return;
        }

        _selectedToggle = selectedToggle;

        if (!notifySelectionChanged)
        {
            return;
        }

        SelectionChangedCallback?.Invoke(_selectedToggle);
        OnSelectionChanged?.Invoke(_selectedToggle);
    }

    private GameObjectSwitchToggle GetFirstOnToggle()
    {
        if (toggles == null)
        {
            return null;
        }

        foreach (var toggle in toggles)
        {
            if (toggle != null && toggle.IsOn)
            {
                return toggle;
            }
        }

        return null;
    }

    private GameObjectSwitchToggle GetDefaultToggle()
    {
        var defaultToggle = GetToggleAt(defaultOnIndex);
        if (defaultToggle != null)
        {
            return defaultToggle;
        }

        if (toggles == null)
        {
            return null;
        }

        foreach (var toggle in toggles)
        {
            if (toggle != null)
            {
                return toggle;
            }
        }

        return null;
    }

    private GameObjectSwitchToggle GetToggleAt(int index)
    {
        if (toggles == null || index < 0 || index >= toggles.Count)
        {
            return null;
        }

        return toggles[index];
    }

    private int GetToggleIndex(GameObjectSwitchToggle targetToggle)
    {
        if (targetToggle == null || toggles == null)
        {
            return -1;
        }

        return toggles.IndexOf(targetToggle);
    }

    private bool ContainsToggle(GameObjectSwitchToggle targetToggle)
    {
        return toggles != null && toggles.Contains(targetToggle);
    }
}
