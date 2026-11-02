using System;using UnityEngine;using Bear.UI;
public class DigPlayCtrl {
 public bool IsActive=true,_paused,_transitioning,_resolved;public Runtime _runtime=new();public Hud _hud=new();public Choice _choice=new();private bool IsTbc,_hintGranted,_tipsWarningShown;private int _analyticsReviveCount,_showTipsFailCount=5;private float _analyticsCurrentLevelTimeSeconds,_showTipsTimeLimit=20;public int Ticks,Warnings;
 private bool CanInteract()=>IsActive&&!_paused&&!_transitioning&&_runtime!=null&&!_runtime.IsResultLocked;
 private void TickLevelAnalytics(float delta){Ticks++;_analyticsCurrentLevelTimeSeconds+=delta;}private void DispatchEvent(object e,bool v){Warnings++;}
    public void Tick()
    {
        // FixedUpdate 可以先于 GameManager.Update 锁定死亡，本帧立即同步 HUD 门禁。
        _hud?.SetDigInteraction(CanInteract());
        if (!IsActive) return;
        if (Input.GetKeyDown(KeyCode.Escape)) ShowExitPopup();
        if (_paused || _transitioning || _resolved || _runtime?.IsRuntimeReady != true) return;
        TickLevelAnalytics(Time.deltaTime);
        if (!IsTbc && !_hintGranted && _runtime.HasHint && !_tipsWarningShown &&
            (_analyticsReviveCount >= _showTipsFailCount ||
             _analyticsCurrentLevelTimeSeconds > _showTipsTimeLimit))
        {
            _tipsWarningShown = true;
            this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, true);
        }
    }
    private void ShowExitPopup()
    {
        if (!CanShowExitPopup()) return;

        CommmonPopup.Create(
            title: I2.Loc.LocalizationManager.GetTranslation("U_Quit_Title_01"),
            content: I2.Loc.LocalizationManager.GetTranslation("U_Quit_Des_01"),
            onYes: () =>
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            },
            onNo: null);
    }
    private bool CanShowExitPopup()
    {
        CommmonPopup exitPopup = UIManager.Instance?.GetUI<CommmonPopup>(nameof(CommmonPopup));
        if (exitPopup != null && exitPopup.IsOpen) return false;

        GuideFlowManager guideFlowManager = GuideFlowManager.Instance;
        if (guideFlowManager.HasActiveFlow || guideFlowManager.PendingCount > 0) return false;

        return !_transitioning && !_resolved && _runtime?.IsResultLocked != true &&
               _choice?.IsRewardAdPending != true && _hud?.IsRewardAdRequesting != true;
    }
}
public class CommmonPopup:BaseUIView {
 private Action _onYesCallback,_onNoCallback;public static int Opens;public static string Title,Content;
 public static CommmonPopup Create(string title=null,string content=null,Action onYes=null,Action onNo=null){Opens++;Title=title;Content=content;var p=new CommmonPopup{_onYesCallback=onYes,_onNoCallback=onNo};UIManager.Instance.Popup=p;return p;}
 public void Yes()=>OnYesBtnClick(new());public void No()=>OnNoBtnClick(new());public void Close()=>OnCloseBtnClick(new());
    public override void OnClose()
    {
        base.OnClose();

        // 清理回调
        _onYesCallback = null;
        _onNoCallback = null;
    }
    private void OnYesBtnClick(CustomButton btn)
    {
        Action callback = _onYesCallback;
        UIManager.Instance.CloseUI(this);
        callback?.Invoke();
    }
    private void OnNoBtnClick(CustomButton btn)
    {
        Action callback = _onNoCallback;
        UIManager.Instance.CloseUI(this);
        callback?.Invoke();
    }
    private void OnCloseBtnClick(CustomButton btn)
    {
        // 关闭弹窗，不触发任何回调
        UIManager.Instance.DestroyUI(this);
    }
}
