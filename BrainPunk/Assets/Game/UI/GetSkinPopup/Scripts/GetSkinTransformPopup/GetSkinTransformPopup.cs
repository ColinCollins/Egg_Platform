using System;
using Bear.Logger;
using Bear.UI;
using Config;
using Config.Game;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using I2.Loc;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 参考 GetSkinPopup 界面功能，goBtn, 跳转到 shopPanel 打开对应皮肤栏。
/// close 直接 DestroyUI 界面
/// </summary>
public partial class GetSkinTransformPopup : BaseUIView, IDebuger
{
    [Tooltip("跳转到 ShopPanel Skin 对应部位 Type 的黄色按钮；为空时会从子节点自动获取。")]
    [SerializeField] private CommonYellowBtn goBtn;

    [Tooltip("用于显示本次皮肤转换目标图标；为空时会从名为 icon 的子节点自动获取。")]
    [SerializeField] private Image icon;

    private Skin skinData;
    private int _iconLoadVersion;
    private Action _closeCallback;

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
        // TODO: 注册按钮等（示例见 PictureVerifticationPopup）
        if (CloseBtn != null)
        {
            CloseBtn.OnClick += ClosePopup;
        }

        if (goBtn?.Btn != null)
        {
            goBtn.Btn.OnClick += OpenSkinPanel;
        }
    }

    public override void OnClose()
    {
        base.OnClose();
        _iconLoadVersion++;

        if (CloseBtn != null)
        {
            CloseBtn.OnClick -= ClosePopup;
        }

        if (goBtn?.Btn != null)
        {
            goBtn.Btn.OnClick -= OpenSkinPanel;
        }

        InvokeCloseCallback();
    }

    private void ClosePopup(CustomButton _)
    {
        UIManager.Instance?.DestroyUI(this);
    }

    private void OpenSkinPanel(CustomButton _)
    {
        UIManager.Instance?.DestroyUI(this);
        ShopPanel.CreateSkin(skinData?.SkinPart ?? SkinPart.Body);
    }

    private void ResolveReferences()
    {
        goBtn ??= GetComponentInChildren<CommonYellowBtn>(true);
        icon ??= FindChildComponent<Image>("icon");
    }

    private void SetSkinData(Skin data)
    {
        skinData = data;
        RefreshSkinName();
        RefreshSkinIcon();
    }

    private void SetCloseCallback(Action callback)
    {
        _closeCallback = callback;
    }

    private void InvokeCloseCallback()
    {
        var callback = _closeCallback;
        _closeCallback = null;
        callback?.Invoke();
    }

    private void RefreshSkinName()
    {
        if (SkinNameTxt == null)
        {
            return;
        }

        if (skinData == null)
        {
            SkinNameTxt.text = string.Empty;
            return;
        }

        string skinName = skinData.SkinName;
        string localizedName = string.IsNullOrEmpty(skinName)
            ? string.Empty
            : LocalizationManager.GetTranslation(skinName);

        SkinNameTxt.text = string.IsNullOrEmpty(localizedName) ? skinName : localizedName;
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
            this.LogWarning($"[GetSkinTransformPopup] Skin icon location is invalid. skin:{skin.Id}, texture:{skin.TextureResources}");
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
        }
        catch (Exception ex)
        {
            if (this != null && loadVersion == _iconLoadVersion && skinData == skin)
            {
                this.LogWarning($"[GetSkinTransformPopup] Load skin icon failed. skin:{skin.Id}, location:{location}, error:{ex.Message}");
            }
        }
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

    /// <summary>
    /// 打开皮肤转换弹窗并设置展示用皮肤数据；只刷新名称和图标，不授予、穿戴或保存皮肤，关闭回调用于串联后续 UI 流程。
    /// </summary>
    public static GetSkinTransformPopup Create(Skin data, Action callback = null)
    {
        var panel = UIManager.Instance?.OpenUI<GetSkinTransformPopup>($"{typeof(GetSkinTransformPopup).Name}", UILayer.Top);
        if (panel != null)
        {
            panel.SetCloseCallback(callback);
            panel.SetSkinData(data);
        }

        return panel;
    }

    /// <summary>
    /// 打开无皮肤数据的转换弹窗；名称和图标保持空状态。
    /// </summary>
    public static GetSkinTransformPopup Create()
    {
        return Create(null, null);
    }
}
