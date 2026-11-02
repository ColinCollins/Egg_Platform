using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Config.Game;
using Game.ConfigModule;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using GameCommon;
using TMPro;
using UnityEngine;

/// <summary>
/// Draw 玩法专用提示弹窗。
/// 基础资源负责获取当前 Draw 关卡的场景提示，_2 变体负责已提示后的金币跳关。
/// </summary>
public sealed class DrawGame_GameTipsPopup : BaseUIView, IEventSender, IDebuger
{
    #region Constants

    private const string PassLevelResourceName = "DrawGame_GameTipsPopup_2";
    private const string PassLevelSource = "DrawGameTipsPopupPassLevel";

    #endregion

    #region Inspector Bindings

    /// <summary>
    /// 关闭弹窗按钮，仅绑定当前 Draw 提示弹窗中的关闭按钮。
    /// </summary>
    [SerializeField] private CustomButton _closeButton;

    /// <summary>
    /// 金币操作按钮；基础资源用于获取提示，_2 变体用于直接跳关。
    /// </summary>
    [SerializeField] private CustomButton _coinButton;

    /// <summary>
    /// 金币消耗文本，由弹窗打开流程按提示或跳关模式刷新为 Draw 专用远端配置价格。
    /// </summary>
    [SerializeField] private TextMeshProUGUI _coinText;

    /// <summary>
    /// 弹窗角色 Spine 控制器，仅用于播放进入和待机动画。
    /// </summary>
    [SerializeField] private UISpineCtrl _spineCtrl;

    /// <summary>
    /// 是否为已获得提示后使用的跳关弹窗变体。
    /// 调用边界：仅由 DrawGame_GameTipsPopup_2 prefab override 为 true，基础提示弹窗保持 false。
    /// </summary>
    [SerializeField] private bool _isPassLevelPopup;

    #endregion

    #region Runtime State

    private IPlayPauseHost _pauseHost;
    private bool _isHintGranted;
    private bool _isPassLevelCompleted;
    private bool _hasClickedConfirmButton;

    #endregion

    #region UI Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();

        if (_closeButton != null)
            _closeButton.OnClick += Close;

        if (_coinButton != null)
            _coinButton.OnClick += _isPassLevelPopup ? PassLevelByCoin : TryToUseHintCoins;
    }

    public override void OnOpen()
    {
        base.OnOpen();

        _isHintGranted = false;
        _isPassLevelCompleted = false;
        _hasClickedConfirmButton = false;
        SetButtonsInteractable(true);
        RefreshCost();
        PlayAnim();
    }

    public override void OnShow()
    {
        base.OnShow();

        _pauseHost = GameManager.Instance?.ControllerManager?.ActivePauseHost;
        _pauseHost?.Pause();
    }

    public override void OnClose()
    {
        IPlayPauseHost pauseHost = _pauseHost;
        _pauseHost = null;

        base.OnClose();
        pauseHost?.Resume();
    }

    #endregion

    #region Button Handlers

    private void Close(CustomButton button)
    {
        // 按当前模式的金币价格检测余额；只决定关闭后是否打开礼包，不扣币或拦截关闭。
        bool needsCoinGift = false;
        if (!_hasClickedConfirmButton &&
            GameManager.Instance.Bag.GetToolCount(GameProps.UnlimitCoin) <= 0)
        {
            int cost = _isPassLevelPopup
                ? ConfigManager.RemoteConfig.GetDrawGameGameTipsPopupPassLevelCoinCost()
                : ConfigManager.RemoteConfig.GetDrawGameGameTipsPopupCoinCost();
            needsCoinGift = GameManager.Instance.Bag.GetToolCount(GameProps.Coin) < cost;
        }

        ClosePopup();

        // 保留 OnClose 恢复 Draw 的原流程，关闭完成后再复用金币不足礼包入口。
        if (needsCoinGift)
            OpenCoinGiftPopup();
    }

    private void TryToUseHintCoins(CustomButton button)
    {
        _hasClickedConfirmButton = true;

        if (_isHintGranted)
            return;

        if (GameManager.Instance.Bag.GetToolCount(GameProps.UnlimitCoin) > 0)
        {
            GrantHint();
            return;
        }

        int cost = ConfigManager.RemoteConfig.GetDrawGameGameTipsPopupCoinCost();
        int count = GameManager.Instance.Bag.GetToolCount(GameProps.Coin);
        if (count < cost)
        {
            OpenCoinGiftPopup();
            return;
        }

        GameManager.Instance.Bag.RemoveTool(GameProps.Coin, cost, CostType.Hint);
        GrantHint();
    }

    private void GrantHint()
    {
        if (_isHintGranted)
            return;

        _isHintGranted = true;
        SetButtonsInteractable(false);
        this.DispatchEvent(Witness<DrawGameHintEvent>._);
        ClosePopup();
    }

    private void PassLevelByCoin(CustomButton button)
    {
        _hasClickedConfirmButton = true;

        if (_isPassLevelCompleted)
            return;

        if (GameManager.Instance.Bag.GetToolCount(GameProps.UnlimitCoin) > 0)
        {
            TryCompletePassLevel();
            return;
        }

        int cost = ConfigManager.RemoteConfig.GetDrawGameGameTipsPopupPassLevelCoinCost();
        int count = GameManager.Instance.Bag.GetToolCount(GameProps.Coin);
        if (count < cost)
        {
            OpenCoinGiftPopup();
            return;
        }

        if (!TryCompletePassLevel())
            return;

        GameManager.Instance.Bag.RemoveTool(GameProps.Coin, cost, CostType.Hint);
    }

    private bool TryCompletePassLevel()
    {
        if (_isPassLevelCompleted)
            return false;

        DrawPlayCtrl drawPlayCtrl =
            GameManager.Instance?.ControllerManager?.ActiveController as DrawPlayCtrl;
        if (drawPlayCtrl == null ||
            !drawPlayCtrl.IsHintInteractionReady ||
            drawPlayCtrl.Phase != DrawPlayPhase.Drawing)
        {
            this.LogWarning("[DrawGame_GameTipsPopup] Pass level rejected because Draw runtime is not ready.");
            SetButtonsInteractable(true);
            return false;
        }

        _isPassLevelCompleted = true;
        SetButtonsInteractable(false);

        // 弹窗持有 Draw 暂停；先同步恢复再提交胜利，确保 TryForceVictory 不会被暂停门禁拒绝。
        IPlayPauseHost pauseHost = _pauseHost;
        _pauseHost = null;
        pauseHost?.Resume();
        ClosePopup();

        if (!drawPlayCtrl.TryForceVictory(PassLevelSource))
        {
            this.LogWarning("[DrawGame_GameTipsPopup] Pass level was not accepted after popup closed.");
            return false;
        }

        AudioManager.PlaySound("success");
        return true;
    }

    #endregion

    #region View Helpers

    private void OpenCoinGiftPopup()
    {
        if (CoinGiftPopup.Create() != null)
            return;

        this.LogError("[DrawGame_GameTipsPopup] Failed to open CoinGiftPopup for insufficient Coin.");
        string message = I2.Loc.LocalizationManager.GetTranslation("U_HurryTips_NeedCoin");
        SystemTips.Show(message);
    }

    private void RefreshCost()
    {
        if (_coinText == null)
            return;

        int cost = _isPassLevelPopup
            ? ConfigManager.RemoteConfig.GetDrawGameGameTipsPopupPassLevelCoinCost()
            : ConfigManager.RemoteConfig.GetDrawGameGameTipsPopupCoinCost();
        _coinText.text = $"-{cost}";
    }

    private void PlayAnim()
    {
        if (_spineCtrl == null)
            return;

        string animationName = _isPassLevelPopup ? "understand" : "dizziness";
        Spine.TrackEntry track = _spineCtrl.PlayAnimation($"ui_{animationName}_in", false);
        if (track != null)
            track.Complete += _ => _spineCtrl?.PlayAnimation($"ui_{animationName}_idle", true);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (_closeButton != null)
            _closeButton.Interactable = interactable;
        if (_coinButton != null)
            _coinButton.Interactable = interactable;
    }

    private void ClosePopup()
    {
        if (IsOpen)
            UIManager.Instance.CloseUI(this);
    }

    #endregion

    #region Factory

    /// <summary>
    /// 打开 Draw 玩法专用提示弹窗。
    /// 调用边界：仅由 DrawGame_GamePlayPanel 的提示按钮调用；非活动或未就绪的 Draw 关卡不创建弹窗。
    /// 注意事项：未获取提示时打开基础提示弹窗；已获取提示时打开 DrawGame_GameTipsPopup_2 跳关变体。
    /// </summary>
    public static DrawGame_GameTipsPopup Create()
    {
        DrawPlayCtrl drawPlayCtrl =
            GameManager.Instance?.ControllerManager?.ActiveController as DrawPlayCtrl;
        if (drawPlayCtrl == null || !drawPlayCtrl.IsHintInteractionReady)
            return null;

        if (drawPlayCtrl.HasGrantedHint && drawPlayCtrl.Phase != DrawPlayPhase.Drawing)
            return null;

        return UIManager.Instance.OpenUI<DrawGame_GameTipsPopup>(
            drawPlayCtrl.HasGrantedHint ? PassLevelResourceName : nameof(DrawGame_GameTipsPopup),
            UILayer.Popup);
    }

    #endregion
}
