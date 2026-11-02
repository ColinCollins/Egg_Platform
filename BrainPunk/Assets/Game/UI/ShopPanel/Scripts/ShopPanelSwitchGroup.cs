using UnityEngine;

/// <summary>
/// Controls the mutually exclusive ShopPart and SkinPart tabs.
/// </summary>
public class ShopPanelSwitchGroup : MonoBehaviour
{
    [SerializeField] private GameObjectToggleGroup ToggleGroup;
    [SerializeField] private GameObjectSwitchToggle ShopToggle;
    [SerializeField] private GameObjectSwitchToggle SkinToggle;

    [SerializeField] private GameObject ShopPart;
    [SerializeField] private GameObject SkinPart;

    private enum ActivePart
    {
        Shop,
        Skin
    }

    private ActivePart _activePart;
    private bool _isBound;
    private bool _isApplyingSwitchState;

    public bool IsSkinSelected => _activePart == ActivePart.Skin;

    /// <summary>
    /// 用途：优先提供 Skin Toggle 子节点 CommonRedDot 的引导定位目标。
    /// 调用边界：仅由 ShopPanel 的局部引导查询入口在运行态读取。
    /// 注意事项：Prefab 变体缺少 CommonRedDot 时回退到 Skin Toggle 自身；两者都缺失时返回 null。
    /// </summary>
    internal RectTransform SkinGuideTarget
    {
        get
        {
            ResolveReferences();
            if (SkinToggle == null)
                return null;

            Transform redDot = FindChildRecursive(SkinToggle.transform, "CommonRedDot");
            return redDot as RectTransform ?? SkinToggle.transform as RectTransform;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        _activePart = ResolveInitialActivePart();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindToggles();
        ApplyState(_activePart, false);
    }

    private void OnDisable()
    {
        UnbindToggles();
    }

    private void OnDestroy()
    {
        UnbindToggles();
    }

    public void SelectShop()
    {
        ApplyState(ActivePart.Shop, true);
    }

    public void SelectSkin()
    {
        ApplyState(ActivePart.Skin, true);
    }

    private void BindToggles()
    {
        if (_isBound)
        {
            return;
        }

        if (ToggleGroup != null)
        {
            ToggleGroup.OnSelectionChanged += OnGroupSelectionChanged;
        }
        else
        {
            if (ShopToggle != null) ShopToggle.OnIsOnChanged += OnShopToggleChanged;
            if (SkinToggle != null) SkinToggle.OnIsOnChanged += OnSkinToggleChanged;
        }

        _isBound = true;
    }

    private void UnbindToggles()
    {
        if (!_isBound)
        {
            return;
        }

        if (ToggleGroup != null)
        {
            ToggleGroup.OnSelectionChanged -= OnGroupSelectionChanged;
        }
        else
        {
            if (ShopToggle != null) ShopToggle.OnIsOnChanged -= OnShopToggleChanged;
            if (SkinToggle != null) SkinToggle.OnIsOnChanged -= OnSkinToggleChanged;
        }

        _isBound = false;
    }

    private void OnGroupSelectionChanged(GameObjectSwitchToggle selectedToggle)
    {
        if (_isApplyingSwitchState)
        {
            return;
        }

        if (selectedToggle == ShopToggle)
        {
            ApplyState(ActivePart.Shop, false);
            return;
        }

        if (selectedToggle == SkinToggle)
        {
            ApplyState(ActivePart.Skin, false);
            return;
        }

        ApplyState(_activePart, false);
    }

    private void OnShopToggleChanged(bool isOn)
    {
        OnToggleChanged(ActivePart.Shop, isOn);
    }

    private void OnSkinToggleChanged(bool isOn)
    {
        OnToggleChanged(ActivePart.Skin, isOn);
    }

    private void OnToggleChanged(ActivePart togglePart, bool isOn)
    {
        if (_isApplyingSwitchState)
        {
            return;
        }

        if (isOn)
        {
            ApplyState(togglePart, false);
            return;
        }

        if (_activePart == togglePart)
        {
            ApplyState(togglePart, false);
        }
    }

    private void ApplyState(ActivePart activePart, bool notifySelectedToggle)
    {
        _activePart = activePart;

        bool isShopActive = activePart == ActivePart.Shop;
        SetActiveIfNeeded(ShopPart, isShopActive);
        SetActiveIfNeeded(SkinPart, !isShopActive);

        ApplySwitchState(activePart, notifySelectedToggle);
    }

    private ActivePart ResolveInitialActivePart()
    {
        if (ShopPart != null && ShopPart.activeSelf)
        {
            return ActivePart.Shop;
        }

        if (SkinPart != null && SkinPart.activeSelf)
        {
            return ActivePart.Skin;
        }

        return ActivePart.Shop;
    }

    private void ResolveReferences()
    {
        Transform lookupRoot = transform.parent != null ? transform.parent : transform;

        ToggleGroup ??= GetComponent<GameObjectToggleGroup>();
        ShopPart ??= FindChildGameObjectRecursive(lookupRoot, "ShopPart");
        SkinPart ??= FindChildGameObjectRecursive(lookupRoot, "SkinPart");

        ShopToggle ??= FindChildToggleRecursive(transform, "Shop_toggle");
        SkinToggle ??= FindChildToggleRecursive(transform, "Skin_toggle");
    }

    private void ApplySwitchState(ActivePart activePart, bool notifySelectedToggle)
    {
        _isApplyingSwitchState = true;

        try
        {
            var selectedToggle = activePart == ActivePart.Shop ? ShopToggle : SkinToggle;

            if (ToggleGroup != null && selectedToggle != null)
            {
                if (notifySelectedToggle)
                {
                    ToggleGroup.SelectToggle(selectedToggle);
                }
                else
                {
                    ToggleGroup.SelectToggleWithoutNotify(selectedToggle);
                }

                return;
            }

            bool isShopActive = activePart == ActivePart.Shop;
            ApplyToggleState(ShopToggle, isShopActive, notifySelectedToggle && isShopActive);
            ApplyToggleState(SkinToggle, !isShopActive, notifySelectedToggle && !isShopActive);
        }
        finally
        {
            _isApplyingSwitchState = false;
        }
    }

    private static void ApplyToggleState(GameObjectSwitchToggle targetToggle, bool isOn, bool notify)
    {
        if (targetToggle == null)
        {
            return;
        }

        if (notify)
        {
            targetToggle.SetIsOn(isOn);
        }
        else
        {
            targetToggle.SetIsOnWithoutNotify(isOn);
        }
    }

    private static GameObject FindChildGameObjectRecursive(Transform root, string childName)
    {
        var child = FindChildRecursive(root, childName);
        return child != null ? child.gameObject : null;
    }

    private static GameObjectSwitchToggle FindChildToggleRecursive(Transform root, string childName)
    {
        var child = FindChildRecursive(root, childName);
        return child != null ? child.GetComponent<GameObjectSwitchToggle>() : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            var result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
