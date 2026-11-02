using System.Threading;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Config.Game;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ConfigModule;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using GameCommon;
using I2.Loc;
using UnityEngine;

public partial class GameTipsPopup : BaseUIView, IEventSender, IDebuger
{
    public enum TipPopupState
    {
        // 正常状态
        Normal = 0,
        // 缺少道具
        Lack,
        // 直接通关
        PassLevel
    }

    [SerializeField] private UISpineCtrl spineCtrl;
    [SerializeField] private float tipTextAnimDuration = 0.4f;
    [SerializeField] private TipPopupState state = TipPopupState.Normal;

    /// <summary>
    /// 用途：按外部业务需要拦截当前提示弹窗下层的点击交互。
    /// 调用边界：由持有 GameTipsPopup 的流程通过 SetClickBlocked(...) 显式控制，本组件不判断具体业务场景。
    /// 注意事项：Prefab 应绑定默认 inactive 的全屏 ClickBlock；引用缺失时仅从当前 Popup 根节点兜底查找。
    /// </summary>
    [SerializeField] private GameObject clickBlock;

    #region First Guide Free State

    private const string FreeButtonLocalizationKey = "U_Common_Btn_Free";
    private const string FreeButtonFallback = "免费";

    private UniTaskCompletionSource<bool> _firstGuideCloseCompletionSource;
    private bool _isFirstGuideFree;
    private bool _hasClickedConfirmButton;

    #endregion

    public override void OnCreate()
    {
        base.OnCreate();
        CloseBtn.OnClick += Close;

        YesBtn.OnClick += state switch
        {
            TipPopupState.Normal => TryToUseCoins,
            TipPopupState.PassLevel => PassLevelByCoin,
            _ => (CustomButton btn) => { }
        };
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _hasClickedConfirmButton = false;
        CompleteFirstGuideCloseWait(false);
        ResetFirstGuideFreeRuntime();
        RefreshTips();
        PlayAnim();
    }

    public override void OnShow()
    {
        base.OnShow();
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
        this.Log("--------------- tipspopup show ");
    }

    public override void OnClose()
    {
        CompleteFirstGuideCloseWait(false);
        ResetFirstGuideFreeRuntime();
        base.OnClose();
        TipTxt.DOKill();
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
    }

    private void PlayAnim()
    {
        if (spineCtrl == null)
        {
            return;
        }

        string spineName = state switch
        {
            TipPopupState.Normal => "dizziness",
            TipPopupState.Lack => "want",
            TipPopupState.PassLevel => "understand",
            _ => "dizziness",
        };

        string animIn = $"ui_{spineName}_in";
        string animIdle = $"ui_{spineName}_idle";

        var track = spineCtrl.PlayAnimation(animIn, false);
        if (track == null)
        {
            return;
        }

        track.Complete += (track) =>
        {
            spineCtrl?.PlayAnimation(animIdle, true);
        };
    }

    private void RefreshTips()
    {
        this.Log("RefreshTipsPopup ------------ ");
        TipTxt.maxVisibleCharacters = int.MaxValue;
        int cost = 0;
        string content = TipTxt.text;
        if (state == TipPopupState.PassLevel)
        {
            cost = ConfigManager.RemoteConfig.GetGameTipsPopupPassLevelCoinCost();
            YesTxt.text = "-" + cost.ToString();

            var tipsKey = PlayCtrl.Instance.Level.CurrentLevelData.AnswerTips;
            content = LocalizationManager.GetTranslation(tipsKey);
            if (content == null)
            {
                content = tipsKey ?? string.Empty;
            }
            TipTxt.DOKill();

            if (tipTextAnimDuration <= 0f)
            {
                TipTxt.text = content;
            }
            else
            {
                TipTxt.enableAutoSizing = true;
                TipTxt.text = content;
                TipTxt.UpdateFontAsset();
                TipTxt.ForceMeshUpdate(true, true);

                int visibleCharacterCount = TipTxt.textInfo.characterCount;

                TipTxt.maxVisibleCharacters = 0;

                DOTween.To(
                        () => TipTxt.maxVisibleCharacters,
                        value => TipTxt.maxVisibleCharacters = value,
                        visibleCharacterCount,
                        tipTextAnimDuration)
                    .SetTarget(TipTxt)
                    .SetDelay(1f)
                    .OnComplete(() => TipTxt.maxVisibleCharacters = int.MaxValue);
            }
        }
        else
        {
            if (_isFirstGuideFree)
            {
                string freeText = LocalizationManager.GetTranslation(FreeButtonLocalizationKey);
                YesTxt.text = string.IsNullOrEmpty(freeText) ? FreeButtonFallback : freeText;
            }
            else
            {
                cost = ConfigManager.RemoteConfig.GetGameTipsPopupCoinCost();
                YesTxt.text = string.Format("-{0}", cost.ToString());
            }

            if (!string.IsNullOrEmpty(content))
            {
                try
                {
                    content = string.Format(content, cost);
                }
                catch (System.FormatException)
                {
                    // content 中包含非法格式字符，保持原样
                }
            }
        }

        this.Log($"RefreshTipsPopup ------------ state: {state}. content: {content}. cost: {cost}");
    }

    private void PassLevelByCoin(CustomButton button)
    {
        _hasClickedConfirmButton = true;
        PassLevelByCostCoin();
    }

    private void PassLevelByCostCoin()
    {
        // 无限金币
        if (GameManager.Instance.Bag.GetToolCount(GameProps.UnlimitCoin) > 0)
        {
            CompletePassLevel();
            return;
        }


        var count = GameManager.Instance.Bag.GetToolCount(GameProps.Coin);
        var cost = ConfigManager.RemoteConfig.GetGameTipsPopupPassLevelCoinCost();
        if (count < cost)
        {
            OpenCoinGiftPopup();
            return;
        }

        GameManager.Instance.Bag.RemoveTool(GameProps.Coin, cost, CostType.Hint);

        CompletePassLevel();
    }

    private void CompletePassLevel()
    {
        UIManager.Instance.CloseAllUI(UILayer.Popup);
        // 直接通关
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.SUCCESS);

        AudioManager.PlaySound("success");
    }

    private void TryToUseCoins(CustomButton btn)
    {
        _hasClickedConfirmButton = true;

        if (_isFirstGuideFree)
        {
            UseFirstGuideFreeHint();
            return;
        }

        // 无限金币
        if (GameManager.Instance.Bag.GetToolCount(GameProps.UnlimitCoin) > 0)
        {
            this.DispatchEvent(Witness<UseTipsEvent>._);

            // 进入 passLevel
            Create();
            return;
        }

        var count = GameManager.Instance.Bag.GetToolCount(GameProps.Coin);
        var cost = ConfigManager.RemoteConfig.GetGameTipsPopupCoinCost();
        if (count < cost)
        {
            OpenCoinGiftPopup();
            return;
        }

        GameManager.Instance.Bag.RemoveTool(GameProps.Coin, cost, CostType.Hint);
        this.DispatchEvent(Witness<UseTipsEvent>._);

        // 进入 passLevel
        Create();
    }

    private void OpenCoinGiftPopup()
    {
        if (CoinGiftPopup.Create() != null)
            return;

        this.LogError("[GameTipsPopup] Failed to open CoinGiftPopup for insufficient Coin.");
        string message = LocalizationManager.GetTranslation("U_HurryTips_NeedCoin");
        SystemTips.Show(message);
    }

    private void Close(CustomButton btn)
    {
        // 关闭前按确认按钮的价格检查余额；只决定是否额外弹出礼包，不执行消费或拦截关闭。
        bool needsCoinGift = false;
        if ((state == TipPopupState.Normal || state == TipPopupState.PassLevel) &&
            !_hasClickedConfirmButton && !_isFirstGuideFree &&
            GameManager.Instance.Bag.GetToolCount(GameProps.UnlimitCoin) <= 0)
        {
            var cost = state == TipPopupState.PassLevel
                ? ConfigManager.RemoteConfig.GetGameTipsPopupPassLevelCoinCost()
                : ConfigManager.RemoteConfig.GetGameTipsPopupCoinCost();
            needsCoinGift = GameManager.Instance.Bag.GetToolCount(GameProps.Coin) < cost;
        }

        CompleteFirstGuideCloseWait(ReferenceEquals(btn, CloseBtn));
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        if (PlayCtrl.Instance.Level.CurrentLevelState.HasTips)
            UIManager.Instance.CloseAllUI(UILayer.Popup);
        else
            UIManager.Instance.CloseUI(this);

        // 原关闭流程完成后再打开，避免新礼包被 CloseAllUI 一并关闭。
        if (needsCoinGift)
            OpenCoinGiftPopup();
    }

    #region First Guide Free API

    /// <summary>
    /// 用途：提供首次免费引导中需要指向的“获取提示”按钮位置。
    /// 调用边界：仅在 Normal 提示弹窗已打开且 PrepareFirstGuideFree(...) 成功后读取。
    /// 注意事项：按钮绑定缺失时返回 null，调用方必须按引导失败处理。
    /// </summary>
    public RectTransform FirstGuideFreeButtonTarget => YesBtn == null
        ? null
        : YesBtn.transform as RectTransform;

    /// <summary>
    /// 用途：按调用方要求切换当前提示弹窗的全屏点击拦截。
    /// 调用边界：由持有当前 Popup 的业务流程在运行态显式调用；本方法不判断引导、动画或其它业务状态。
    /// 注意事项：调用方负责成对开启和关闭；缺少 ClickBlock 时返回 false。
    /// </summary>
    /// <param name="isBlocked">true 开启点击拦截；false 恢复普通弹窗交互层级。</param>
    /// <returns>找到 ClickBlock 并完成目标状态同步时返回 true。</returns>
    public bool SetClickBlocked(bool isBlocked)
    {
        if (!Application.isPlaying)
            return false;

        if (clickBlock == null)
        {
            Transform clickBlockTransform = transform.Find("ClickBlock");
            clickBlock = clickBlockTransform != null ? clickBlockTransform.gameObject : null;
        }

        if (clickBlock == null)
            return false;

        if (clickBlock.activeSelf != isBlocked)
            clickBlock.SetActive(isBlocked);

        return true;
    }

    /// <summary>
    /// 用途：进入首次免费提示模式，显示“免费”并暂时锁住所有弹窗按钮。
    /// 调用边界：仅由 GameTipsGuideFlow 创建 Normal 弹窗后调用一次。
    /// 注意事项：不发放提示、不修改存档；后续需调用 EnableFirstGuideFreeButton() 开放引导按钮。
    /// </summary>
    /// <returns>弹窗状态和必要按钮绑定有效时返回 true。</returns>
    public bool PrepareFirstGuideFree()
    {
        if (!Application.isPlaying || !IsOpen || state != TipPopupState.Normal ||
            CloseBtn == null || YesBtn == null)
        {
            return false;
        }

        _isFirstGuideFree = true;
        SetFirstGuideButtonsInteractable(false, false);
        RefreshTips();
        return true;
    }

    /// <summary>
    /// 用途：在免费剧情对话结束后，只开放本次免费提示按钮供 GuideFinger 引导点击。
    /// 调用边界：仅在 PrepareFirstGuideFree() 成功且弹窗仍处于打开状态时调用。
    /// 注意事项：关闭按钮继续保持不可交互，直到免费提示被消费或流程取消。
    /// </summary>
    /// <returns>按钮成功开放时返回 true。</returns>
    public bool EnableFirstGuideFreeButton()
    {
        if (!Application.isPlaying || !IsOpen || !_isFirstGuideFree || YesBtn == null)
            return false;

        SetFirstGuideButtonsInteractable(false, true);
        return true;
    }

    /// <summary>
    /// 用途：免费发放首次提示，通过 SimpleBag 记录一次 Tips 消耗，并复用 UseTipsEvent 与现有 Create() 打开答案弹窗。
    /// 调用边界：只在首次免费模式中由引导手指完成回调或 YesBtn 本身调用一次。
    /// 注意事项：不会扣金币或播放广告；成功消费只调用一次 SimpleBag 消耗记录 API，重复调用返回 null。
    /// </summary>
    /// <returns>成功打开的 PassLevel 提示弹窗；失败或重复消费时返回 null。</returns>
    public GameTipsPopup UseFirstGuideFreeHint()
    {
        if (!Application.isPlaying || !IsOpen || !_isFirstGuideFree || state != TipPopupState.Normal)
            return null;

        SimpleBag bag = GameManager.Instance?.Bag;
        if (bag == null || !bag.RecordToolConsume(GameProps.Tips, CostType.Hint))
        {
            this.LogWarning("[GameTipsGuide] Failed to record the free hint through SimpleBag.");
            return null;
        }

        _isFirstGuideFree = false;
        SetFirstGuideButtonsInteractable(false, false);
        this.DispatchEvent(Witness<UseTipsEvent>._);

        GameTipsPopup passLevelPopup = Create();
        if (passLevelPopup == null || passLevelPopup.state != TipPopupState.PassLevel)
        {
            this.LogWarning("[GameTipsGuide] Failed to open the PassLevel popup after granting the free hint.");
            return null;
        }

        return passLevelPopup;
    }

    /// <summary>
    /// 用途：等待本提示弹窗关闭，并区分是否由右上角 CloseBtn（X）触发。
    /// 调用边界：仅由 GameTipsGuideFlow 在免费提示已打开 PassLevel 弹窗后调用一次。
    /// 注意事项：直接通关、外部关闭返回 false；外部取消继续抛给 GuideFlowBase 处理。
    /// </summary>
    /// <param name="cancellationToken">当前引导流程的统一取消令牌。</param>
    /// <returns>CloseBtn（X）关闭返回 true，其它关闭路径返回 false。</returns>
    public async UniTask<bool> WaitForFirstGuideCloseByXAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Application.isPlaying || !IsOpen || _firstGuideCloseCompletionSource != null)
            return false;

        var completionSource = new UniTaskCompletionSource<bool>();
        _firstGuideCloseCompletionSource = completionSource;
        try
        {
            return await completionSource.Task.AttachExternalCancellation(cancellationToken);
        }
        finally
        {
            if (ReferenceEquals(_firstGuideCloseCompletionSource, completionSource))
                _firstGuideCloseCompletionSource = null;
        }
    }

    /// <summary>
    /// 用途：在首次免费引导取消或失败时恢复普通提示弹窗交互与价格显示。
    /// 调用边界：由 GameTipsGuideFlow.OnFlowStopped() 对仍打开的 Normal 弹窗调用。
    /// 注意事项：已消费的免费提示不会被撤销，也不会修改 HasTips 或存档状态。
    /// </summary>
    public void CancelFirstGuideFreeMode()
    {
        if (!Application.isPlaying || !IsOpen || state != TipPopupState.Normal)
            return;

        _isFirstGuideFree = false;
        SetFirstGuideButtonsInteractable(true, true);
        RefreshTips();
    }

    /// <summary>
    /// 用途：创建并立即准备首次免费模式的 Normal 提示弹窗。
    /// 调用边界：仅由 GamePlayPanel 的首次提示引导局部入口调用。
    /// 注意事项：玩家已经获得提示时 Create() 会选择 PassLevel 变体，此时准备失败并返回 null。
    /// </summary>
    /// <returns>成功进入首次免费模式的提示弹窗；创建或准备失败时返回 null。</returns>
    public static GameTipsPopup CreateFirstGuideFree()
    {
        GameTipsPopup panel = Create();
        if (panel == null || !panel.PrepareFirstGuideFree())
            return null;

        return panel;
    }

    #endregion

    #region First Guide Free Helpers

    private void CompleteFirstGuideCloseWait(bool closedByX)
    {
        UniTaskCompletionSource<bool> completionSource = _firstGuideCloseCompletionSource;
        _firstGuideCloseCompletionSource = null;
        completionSource?.TrySetResult(closedByX);
    }

    private void ResetFirstGuideFreeRuntime()
    {
        _isFirstGuideFree = false;
        SetFirstGuideButtonsInteractable(true, true);
    }

    private void SetFirstGuideButtonsInteractable(
        bool closeInteractable,
        bool yesInteractable)
    {
        if (CloseBtn != null)
            CloseBtn.Interactable = closeInteractable;
        if (YesBtn != null)
            YesBtn.Interactable = yesInteractable;
    }

    #endregion

    public static GameTipsPopup Create()
    {
        string panelName = $"{typeof(GameTipsPopup).Name}";
        if (PlayCtrl.Instance.Level.CurrentLevelState.HasTips)
        {
            // GameTipsPopup_3
            panelName += "_3";
        }

        var panel = UIManager.Instance.OpenUI<GameTipsPopup>(panelName, UILayer.Popup);

        return panel;
    }
}
