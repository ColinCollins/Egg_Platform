using System;
using Bear.Logger;
using Config.Game;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ChestGift 奖励展示项。
/// 只负责把一条 Shop 奖励转换为图标和数量显示，不负责发奖、购买或写入存档。
/// </summary>
public sealed class ChestGiftItem : MonoBehaviour, IDebuger
{
    private const float SkinIconScale = 1.1f;

    #region Inspector

    [Tooltip("奖励图标；Coin 使用固定图标，Skin 会异步替换为配置对应图标。")]
    [SerializeField] private Image icon;

    [Tooltip("奖励数量文本；Coin 使用 x{num} 格式，Skin 与无效数量隐藏。")]
    [SerializeField] private TextMeshProUGUI countText;

    [Tooltip("金币奖励固定图标。")]
    [SerializeField] private Sprite coinSprite;

    #endregion

    #region State

    private int _skinIconLoadVersion;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：使用一条 Luban Shop 奖励刷新当前展示项。
    /// 调用边界：仅由 ChestGiftPopup 在运行时实例化后调用；返回 false 时调用方应销毁该无效项。
    /// 注意事项：Coin 只接受 IntParam 数量，Skin 只接受 StringParam Skin ID，避免数字 Skin ID 被误当成数量。
    /// </summary>
    public bool Refresh(GameProps prop, Config.ParamBase rewardValue)
    {
        _skinIconLoadVersion++;

        switch (prop)
        {
            case GameProps.Coin:
                return RefreshCoin(rewardValue);
            case GameProps.Skin:
                return RefreshSkin(rewardValue);
            default:
                this.LogWarning($"[ChestGiftItem] Unsupported reward type: {prop}.");
                ClearVisual();
                return false;
        }
    }

    #endregion

    #region Reward Binding

    private bool RefreshCoin(Config.ParamBase rewardValue)
    {
        if (!BaseParamUtils.TryGetIntParam(rewardValue, out int count) || count <= 0 || coinSprite == null)
        {
            this.LogError("[ChestGiftItem] Coin reward is invalid or coin sprite is missing.");
            ClearVisual();
            return false;
        }

        ApplyVisual(coinSprite, count);
        return true;
    }

    private bool RefreshSkin(Config.ParamBase rewardValue)
    {
        if (rewardValue is not Config.StringParam stringParam ||
            string.IsNullOrWhiteSpace(stringParam.Value) ||
            !SkinUtils.TryGetSkin(stringParam.Value, out Config.Skin skin))
        {
            this.LogError("[ChestGiftItem] Skin reward must contain a valid StringParam Skin ID.");
            ClearVisual();
            return false;
        }

        string iconLocation = SkinUtils.ResolveIconLocation(skin);
        if (string.IsNullOrEmpty(iconLocation))
        {
            this.LogError($"[ChestGiftItem] Skin icon is unavailable. skinId={skin.Id}, texture={skin.TextureResources}");
            ClearVisual();
            return false;
        }

        ApplyVisual(null, 0);
        LoadSkinIconAsync(skin, iconLocation, _skinIconLoadVersion).Forget();
        return true;
    }

    private async UniTaskVoid LoadSkinIconAsync(Config.Skin skin, string location, int loadVersion)
    {
        try
        {
            Sprite sprite = await SkinUtils.LoadIconAsync(location);
            if (this == null || loadVersion != _skinIconLoadVersion)
                return;

            ApplySkinVisual(sprite, 0);
        }
        catch (Exception exception)
        {
            if (this != null && loadVersion == _skinIconLoadVersion)
                this.LogWarning($"[ChestGiftItem] Skin icon load failed. skinId={skin.Id}, location={location}, error={exception.Message}");
        }
    }

    #endregion

    #region Visual

    private void ApplyVisual(Sprite sprite, int count)
    {
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.rectTransform.localScale = Vector3.one;
        }

        ApplyCount(count);
    }

    private void ApplySkinVisual(Sprite sprite, int count)
    {
        ApplyCount(count);

        if (icon == null)
            return;

        icon.sprite = sprite;
        icon.enabled = sprite != null;

        if (sprite == null)
            return;

        icon.SetNativeSize();
        icon.rectTransform.localScale = Vector3.one * SkinIconScale;
    }

    private void ApplyCount(int count)
    {
        if (countText == null)
            return;

        bool showCount = count > 0;
        countText.gameObject.SetActive(showCount);
        countText.text = showCount ? $"x{count}" : string.Empty;
    }

    private void ClearVisual()
    {
        ApplyVisual(null, 0);
    }

    private void OnDisable()
    {
        _skinIconLoadVersion++;
    }

    #endregion
}
