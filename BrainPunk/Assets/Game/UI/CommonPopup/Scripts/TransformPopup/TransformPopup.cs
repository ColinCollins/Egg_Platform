using System;
using Bear.UI;
using Config.Game;
using Game.ConfigModule;
using Game.Play;
using GameCommon;
using UnityEngine;

/// <summary>
/// 用于转换 Tips 为 Coin 的界面。
/// </summary>
public partial class TransformPopup : BaseUIView
{
    private int _tipsCount;
    private int _convertedCoinCount;
    private bool _hasUnlimitTips;
    private bool _hasTransformed;
    private Action _onClosed;

    public override void OnOpen()
    {
        base.OnOpen();
    }

    public override void OnCreate()
    {
        base.OnCreate();

        if (UIGonggaoLiaojie01Btn != null)
        {
            UIGonggaoLiaojie01Btn.OnClick += ClosePopup;
        }

        CacheTransformPreview();
        RefreshDisplay();
    }

    public override void OnClose()
    {
        Action onClosed = _onClosed;
        _onClosed = null;

        base.OnClose();
        GameManager.Instance.Bag.SyncOldVersion();
        if (UIGonggaoLiaojie01Btn != null)
        {
            UIGonggaoLiaojie01Btn.OnClick -= ClosePopup;
        }

        onClosed?.Invoke();
    }

    private void TransformTipsToCoin()
    {
        if (_hasTransformed)
        {
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            return;
        }

        int tipsCount = Mathf.Max(0, bag.GetToolCount(GameProps.Tips));
        bool hasUnlimitTips = bag.GetToolCount(GameProps.UnlimitTips) > 0;
        int convertedCoinCount = ResolveConvertedCoinCount(tipsCount, hasUnlimitTips);
        bool canTransform = hasUnlimitTips || (tipsCount > 0 && convertedCoinCount > 0);
        if (!canTransform)
        {
            return;
        }

        int currentCoinCount = Mathf.Max(0, bag.GetToolCount(GameProps.Coin));
        int targetCoinCount = convertedCoinCount + currentCoinCount;

        bag.SetToolCount(GameProps.Coin, targetCoinCount, false);

        if (tipsCount > 0)
        {
            bag.DeleteTool(GameProps.Tips, false);
        }

        DB.SaveGameData();
        _hasTransformed = true;
    }

    private void CacheTransformPreview()
    {
        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            _tipsCount = 0;
            _convertedCoinCount = 0;
            _hasUnlimitTips = false;
            return;
        }

        _tipsCount = Mathf.Max(0, bag.GetToolCount(GameProps.Tips));
        _hasUnlimitTips = bag.GetToolCount(GameProps.UnlimitTips) > 0;
        _convertedCoinCount = ResolveConvertedCoinCount(_tipsCount, _hasUnlimitTips);
    }

    private void RefreshDisplay()
    {
        if (UiTips02Txt != null)
        {
            UiTips02Txt.text = _hasUnlimitTips ? "∞" : _tipsCount.ToString();
        }

        if (UiTips01Txt != null)
        {
            UiTips01Txt.text = _hasUnlimitTips ? "∞" : _convertedCoinCount.ToString();
        }
    }

    private void ClosePopup(CustomButton btn)
    {
        UIManager.Instance?.DestroyUI(this);
    }

    private static int ResolveConvertedCoinCount(int tipsCount, bool hasUnlimitTips)
    {
        if (tipsCount <= 0)
        {
            return 0;
        }

        int tipCostCount = Mathf.Max(0, ConfigManager.RemoteConfig.GetGameTipsPopupCoinCost());
        if (tipCostCount <= 0)
        {
            return 0;
        }

        int convertedCoinCount = tipsCount * tipCostCount;
        return convertedCoinCount;
    }

    public static bool HasTransformableTips()
    {
        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            return false;
        }

        int tipsCount = Mathf.Max(0, bag.GetToolCount(GameProps.Tips));
        return tipsCount > 0 || bag.GetToolCount(GameProps.UnlimitTips) > 0;
    }

    /// <summary>
    /// 创建旧 Tips 转金币弹窗。
    /// 调用边界：启动弹窗序列可传入关闭回调；商店等既有入口可以省略。
    /// 注意事项：回调在 `SyncOldVersion()` 完成后才触发，确保后续弹窗读取到同步后数据。
    /// </summary>
    /// <param name="onClosed">弹窗关闭完成后的可选回调。</param>
    public static TransformPopup Create(Action onClosed = null)
    {
        if (UIManager.Instance == null)
        {
            return null;
        }

        TransformPopup popup = UIManager.Instance.OpenUI<TransformPopup>(
            $"{typeof(TransformPopup).Name}",
            UILayer.Popup);
        if (popup != null)
            popup._onClosed = onClosed;

        return popup;
    }
}
