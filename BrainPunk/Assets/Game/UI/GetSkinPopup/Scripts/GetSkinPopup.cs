using System;
using Bear.Logger;
using Bear.UI;
using Config;
using Config.Game;
using Cysharp.Threading.Tasks;
using GameCommon;
using I2.Loc;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// NoBtn 直接 DestroyUI
/// CommonYellowBtn DestroyUI 并且跳转 ShopPanel
/// 
/// </summary>
public partial class GetSkinPopup : BaseUIView, IDebuger
{
    private Skin skinData;

    [Tooltip("用于显示本次获得的皮肤图标；为空时会从名为 icon 的子节点自动获取。")]
    [SerializeField] private Image icon;
    private int _iconLoadVersion;

    public override void OnOpen()
    {
        base.OnOpen();
        ResolveReferences();
        RefreshSkinName();
        RefreshSkinIcon();
    }

    public override void OnCreate()
    {
        base.OnCreate();
        ResolveReferences();
        if (NoBtn != null)
        {
            NoBtn.OnClick += ClosePopup;
        }

        if (UICommonYellow01Btn != null)
        {
            UICommonYellow01Btn.OnClick += OpenSkinPanel;
        }
    }

    public override void OnClose()
    {
        base.OnClose();
        _iconLoadVersion++;

        if (NoBtn != null)
        {
            NoBtn.OnClick -= ClosePopup;
        }

        if (UICommonYellow01Btn != null)
        {
            UICommonYellow01Btn.OnClick -= OpenSkinPanel;
        }
    }

    private void ClosePopup(CustomButton _)
    {
        UIManager.Instance?.DestroyUI(this);
    }

    private void OpenSkinPanel(CustomButton _)
    {
        Config.Skin acquiredSkin = skinData;
        UIManager.Instance?.DestroyUI(this);
        if (Shop2SkinGuideFlow.TryStart(acquiredSkin))
        {
            return;
        }

        if (acquiredSkin != null)
        {
            ShopPanel.CreateSkin(acquiredSkin.SkinPart);
            return;
        }

        ShopPanel.CreateSkin();
    }

    public static GetSkinPopup Create(Skin data)
    {
        var panel = UIManager.Instance?.OpenUI<GetSkinPopup>($"{typeof(GetSkinPopup).Name}", UILayer.Popup);
        if (panel != null)
        {
            panel.SetSkinData(data);
            panel.GrantSkin(data, RewardType.Other);
        }

        return panel;
    }

    public static GetSkinPopup Create()
    {
        return Create(null);
    }

    private void SetSkinData(Skin data)
    {
        skinData = data;
        RefreshSkinName();
        RefreshSkinIcon();
    }

    private void RefreshSkinName()
    {
        if (UiHuodepifu01Txt == null)
        {
            return;
        }

        if (skinData == null)
        {
            UiHuodepifu01Txt.text = string.Empty;
            return;
        }

        string skinName = skinData.SkinName;
        string localizedName = string.IsNullOrEmpty(skinName)
            ? string.Empty
            : LocalizationManager.GetTranslation(skinName);

        UiHuodepifu01Txt.text = string.IsNullOrEmpty(localizedName) ? skinName : localizedName;
    }

    private void RefreshSkinIcon()
    {
        ResolveReferences();
        _iconLoadVersion++;

        if (icon == null)
        {
            return;
        }

        icon.sprite = null;
        icon.enabled = false;

        if (skinData == null || string.IsNullOrEmpty(skinData.TextureResources))
        {
            return;
        }

        LoadIconAsync(skinData, _iconLoadVersion).Forget();
    }

    private async UniTaskVoid LoadIconAsync(Skin skin, int loadVersion)
    {
        if (icon == null || skin == null || string.IsNullOrEmpty(skin.TextureResources))
        {
            return;
        }

        string location = SkinUtils.ResolveIconLocation(skin);
        if (string.IsNullOrEmpty(location))
        {
            this.LogWarning($"[GetSkinPopup] Skin icon location is invalid. skin:{skin.Id}, texture:{skin.TextureResources}");
            return;
        }

        try
        {
            var sprite = await SkinUtils.LoadIconAsync(location);
            if (this == null ||
                loadVersion != _iconLoadVersion ||
                skinData != skin ||
                icon == null)
            {
                return;
            }

            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.SetNativeSize();
            icon.transform.localScale = Vector3.one * 1.5f;
        }
        catch (Exception ex)
        {
            if (this != null && loadVersion == _iconLoadVersion && skinData == skin)
            {
                this.LogWarning($"[GetSkinPopup] Load skin icon failed. skin:{skin.Id}, location:{location}, error:{ex.Message}");
            }
        }
    }

    private void ResolveReferences()
    {
        icon ??= FindChildComponent<Image>("icon");
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        var child = FindChildRecursive(transform, childName);
        return child != null ? child.GetComponent<T>() : null;
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

    private void GrantSkin(Config.Skin skin, RewardType rewardType)
    {
        if (skin == null || string.IsNullOrEmpty(skin.Id))
        {
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError("[GetSkinPopup] GrantSkin failed. Bag is null.");
            return;
        }

        bag.GetNewSkin(skin.Id, GetSkinWayType.Free);
    }
}
