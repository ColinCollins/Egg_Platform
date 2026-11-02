using System;
using System.Threading;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// 首次免费皮肤商店引导，负责从当前 Gameplay 商店入口进入 Skin 页、切换部位并定位穿戴指定皮肤。
/// </summary>
public sealed class Shop2SkinGuideFlow : GuideFlowBase, IDebuger, IEventSender
{
    /// <summary>
    /// Skin Toggle 完成点击并切换到 Skin 大页后，等待部位布局稳定再定位目标 SkinPart 的 GuideFinger。
    /// </summary>
    private const int SkinPartGuideWaitMilliseconds = 1500;

    #region Runtime Ownership

    private static Shop2SkinGuideFlow _currentFlow;

    private readonly string _skinId;
    private ShopPanel _shopPanel;
    private bool _hasPausedGameplay;
    private bool _resumePlayingOnEarlyStop;
    private bool _shopPanelOpened;

    #endregion

    #region Construction And Entry

    private Shop2SkinGuideFlow(string skinId)
    {
        _skinId = skinId;
    }

    /// <summary>
    /// 用途：校验免费皮肤弹窗来源和首次引导资格，并把流程提交给 GuideFlowManager。
    /// 调用边界：仅由 GetSkinPopup 黄色按钮在 Unity 主线程调用；方法内部读取当前 CurrentGamePlayPanel。
    /// 注意事项：相同流程已经活动或排队时视为已接受；返回 false 时调用方应保留原商店 Skin 入口作为降级路径。
    /// </summary>
    /// <param name="acquiredSkin">当前获得皮肤弹窗展示并已经授予的皮肤。</param>
    /// <returns>流程已创建、已排队或已有同类流程时返回 true；不满足首次引导条件时返回 false。</returns>
    public static bool TryStart(Config.Skin acquiredSkin)
    {
        if (!Application.isPlaying)
            return false;

        if (_currentFlow != null || GuideFlowManager.Instance.Contains<Shop2SkinGuideFlow>())
            return true;

        string skinId = ConfigManager.RemoteConfig.GetFreeSkinId();
        if (acquiredSkin == null ||
            string.IsNullOrEmpty(skinId) ||
            !string.Equals(acquiredSkin.Id, skinId, StringComparison.Ordinal))
        {
            return false;
        }

        var gameData = DB.GameData;
        if (gameData == null ||
            gameData.HasShopToSkinGuide ||
            gameData.OwnedSkin == null ||
            !gameData.OwnedSkin.Contains(skinId))
        {
            return false;
        }

        GamePlayPanel playPanel = PlayCtrl.Instance?.CurrentGamePlayPanel;
        if (playPanel == null ||
            playPanel.Shop2SkinGuideTarget == null ||
            playPanel.transform is not RectTransform)
        {
            return false;
        }

        Config.Skin targetSkin = ConfigManager.Instance?.Tables?.TbSkin?.GetOrDefault(skinId);
        if (targetSkin == null)
            return false;

        var flow = new Shop2SkinGuideFlow(skinId);
        _currentFlow = flow;
        RunCurrentAsync(flow).Forget();
        return true;
    }

    /// <summary>
    /// 用途：取消当前正在执行或排队中的首次皮肤商店引导。
    /// 调用边界：由 PlayCtrl 的重置、重玩、切关、销毁入口在 Unity 主线程调用。
    /// 注意事项：只发送取消信号，实际 UI 和状态收尾由 GuideFlowBase 保证执行。
    /// </summary>
    internal static void CancelCurrent()
    {
        _currentFlow?.Cancel();
    }

    /// <summary>
    /// 用途：当前 GamePlayPanel 关闭时取消仍依赖该 Gameplay 上下文的皮肤引导。
    /// 调用边界：仅由 GamePlayPanel.OnClose 在 Unity 主线程调用。
    /// 注意事项：非当前 Gameplay Panel 的关闭不会取消流程。
    /// </summary>
    internal static void CancelFor(GamePlayPanel playPanel)
    {
        if (_currentFlow == null ||
            !ReferenceEquals(PlayCtrl.Instance?.CurrentGamePlayPanel, playPanel))
        {
            return;
        }

        _currentFlow.Cancel();
    }

    /// <summary>
    /// 用途：引导持有的 ShopPanel 关闭时取消剩余步骤并清理 GuideFinger。
    /// 调用边界：仅由 ShopPanel.OnClose 在 Unity 主线程调用。
    /// 注意事项：普通 ShopPanel 或已完成流程的关闭不会影响其它引导。
    /// </summary>
    internal static void CancelFor(ShopPanel shopPanel)
    {
        if (_currentFlow == null || !ReferenceEquals(_currentFlow._shopPanel, shopPanel))
            return;

        _currentFlow.Cancel();
    }

    private static async UniTask RunCurrentAsync(Shop2SkinGuideFlow flow)
    {
        try
        {
            GuideFlowResult result = await flow.RunAsync();
            if (result == GuideFlowResult.Failed)
                flow.LogWarning("[Shop2SkinGuideFlow] Flow stopped because a required guide dependency was unavailable.");
        }
        catch (Exception exception)
        {
            flow.LogError($"[Shop2SkinGuideFlow] Unexpected flow exception. error:{exception}");
        }
        finally
        {
            if (ReferenceEquals(_currentFlow, flow))
                _currentFlow = null;
        }
    }

    #endregion

    #region GuideFlowBase

    /// <summary>
    /// 用途：依次引导 Gameplay 商店按钮、Skin Toggle、目标 SkinPart Toggle 和 RemoteConfig 指定皮肤槽位。
    /// 调用边界：只由 GuideFlowBase.RunAsync(...) 在取得集中执行权后调用。
    /// 注意事项：进入流程时切换 PlayCtrl Pause；ShopPanel 打开后由其既有关闭路径负责恢复 Playing。
    /// </summary>
    protected override async UniTask<GuideFlowResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveGameplayContext(
                out PlayCtrl playCtrl,
                out GamePlayPanel playPanel,
                out RectTransform shopTarget,
                out RectTransform gameplayRoot,
                out Config.Skin targetSkin) ||
            !TryMarkGuideTriggered())
        {
            return GuideFlowResult.Failed;
        }

        _resumePlayingOnEarlyStop = playCtrl.CheckState(GamePlayStateName.PLAYING);
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
        _hasPausedGameplay = true;

        GuideFlowResult shopFingerResult = await GuideFinger.WaitForClickAsync(
            shopTarget,
            gameplayRoot,
            cancellationToken);
        if (shopFingerResult != GuideFlowResult.Completed)
            return shopFingerResult;

        if (!ReferenceEquals(PlayCtrl.Instance?.CurrentGamePlayPanel, playPanel))
            return GuideFlowResult.Failed;

        _shopPanel = ShopPanel.Create(true, playPanel.GetType().Name);
        if (_shopPanel == null)
            return GuideFlowResult.Failed;

        _shopPanelOpened = true;
        if (!_shopPanel.SetClickBlocked(true))
            return GuideFlowResult.Failed;

        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        if (_shopPanel == null ||
            !_shopPanel.TryGetSkinGuideContext(
                out RectTransform skinToggleTarget,
                out RectTransform shopRoot))
        {
            return GuideFlowResult.Failed;
        }

        GuideFlowResult toggleFingerResult = await GuideFinger.WaitForClickAsync(
            skinToggleTarget,
            shopRoot,
            cancellationToken);
        if (toggleFingerResult != GuideFlowResult.Completed)
            return toggleFingerResult;

        if (_shopPanel == null ||
            !_shopPanel.TrySelectSkinPageForGuide(out ShopPanel_SkinPart skinPart))
        {
            return GuideFlowResult.Failed;
        }

        await UniTask.Delay(
            SkinPartGuideWaitMilliseconds,
            ignoreTimeScale: true,
            cancellationToken: cancellationToken);
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);

        if (skinPart == null ||
            !skinPart.TryGetSkinPartGuideTarget(
                targetSkin.SkinPart,
                out RectTransform skinPartTarget))
        {
            return GuideFlowResult.Failed;
        }

        GuideFlowResult skinPartFingerResult = await GuideFinger.WaitForClickAsync(
            skinPartTarget,
            shopRoot,
            cancellationToken);
        if (skinPartFingerResult != GuideFlowResult.Completed)
            return skinPartFingerResult;

        skinPart.SelectSkinPart(targetSkin.SkinPart);
        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        if (skinPart == null ||
            !skinPart.TryPrepareGuideSkin(_skinId, out RectTransform skinTarget))
        {
            return GuideFlowResult.Failed;
        }

        GuideFlowResult skinFingerResult = await GuideFinger.WaitForClickAsync(
            skinTarget,
            shopRoot,
            cancellationToken);
        if (skinFingerResult != GuideFlowResult.Completed)
            return skinFingerResult;

        return skinPart != null && skinPart.TrySelectGuideSkin(_skinId)
            ? GuideFlowResult.Completed
            : GuideFlowResult.Failed;
    }

    /// <summary>
    /// 流程结束时只恢复尚未打开商店的 Gameplay Pause；商店已打开时沿用 ShopPanel 关闭恢复逻辑。
    /// </summary>
    protected override void OnFlowStopped()
    {
        _shopPanel?.SetClickBlocked(false);

        if (_hasPausedGameplay &&
            _resumePlayingOnEarlyStop &&
            !_shopPanelOpened &&
            PlayCtrl.Instance?.CheckState(GamePlayStateName.PAUSE) == true)
        {
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        }

        _shopPanel = null;
        _hasPausedGameplay = false;
        _resumePlayingOnEarlyStop = false;
        _shopPanelOpened = false;
    }

    #endregion

    #region Internal Helpers

    private bool TryResolveGameplayContext(
        out PlayCtrl playCtrl,
        out GamePlayPanel playPanel,
        out RectTransform shopTarget,
        out RectTransform gameplayRoot,
        out Config.Skin targetSkin)
    {
        playCtrl = PlayCtrl.Instance;
        playPanel = playCtrl?.CurrentGamePlayPanel;
        shopTarget = playPanel?.Shop2SkinGuideTarget;
        gameplayRoot = playPanel != null ? playPanel.transform as RectTransform : null;
        targetSkin = ConfigManager.Instance?.Tables?.TbSkin?.GetOrDefault(_skinId);

        var gameData = DB.GameData;
        return Application.isPlaying &&
               playCtrl != null &&
               playPanel != null &&
               shopTarget != null &&
               gameplayRoot != null &&
               targetSkin != null &&
               gameData != null &&
               !gameData.HasShopToSkinGuide &&
               gameData.OwnedSkin != null &&
               gameData.OwnedSkin.Contains(_skinId);
    }

    private bool TryMarkGuideTriggered()
    {
        var gameData = DB.GameData;
        if (gameData == null || gameData.HasShopToSkinGuide)
            return false;

        gameData.HasShopToSkinGuide = true;
        if (!DB.SaveGameData())
            this.LogWarning("[Shop2SkinGuideFlow] Failed to save the triggered state; the current session remains consumed.");

        return true;
    }

    #endregion
}
