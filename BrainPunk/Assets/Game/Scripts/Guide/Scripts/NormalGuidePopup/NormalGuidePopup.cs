using System;
using System.Collections.Generic;
using System.Threading;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ConfigModule;
using I2.Loc;
using UnityEngine;

/// <summary>
/// 明信片引导弹窗阶段；各枚举值分别对应 `NormalGuidePopup_1` 至 `NormalGuidePopup_4` 资源。
/// </summary>
public enum PostcardStageStep
{
    One = 10001,
    Two = 10002,
    Three,
    Four,
}


/// <summary>
/// version 1.0 
/// 补充逻辑 CheckStarCount
/// 
/// version 1.1 
/// 补充 spineCtrl 动画控制。
/// PlayTips 的时候，spineCtrl 随机从 turoial01 - 03 选择一个 API 播放，然后回到 tutorial_idle。如果不是 tutorial_idle 那么就不切换播放。
/// 
/// version 2.0
/// 使用 PostcardStageStep 选择阶段资源；弹窗只展示引导内容，不修改明信片或背包数据。
/// </summary>
public partial class NormalGuidePopup : BaseUIView, IDebuger
{
    private const string TutorialIdleAnimName = "tutorial_idle";
    private const string Tutorial01AnimName = "tutorial_01";
    private const string Tutorial02AnimName = "tutorial_02";
    private const string Tutorial02IdleAnimName = "tutorial_02_idle";
    private const string Tutorial02ReturnAnimName = "tutorial_02_return";
    private const string Tutorial03AnimName = "tutorial_03";
    private const string DefaultSpineSkinName = "box_original";

    [SerializeField] private List<string> tipsKey;

    [SerializeField] private CustomButton btn;
    [SerializeField] private float textAnimDuration = 1f;
    [SerializeField] private UISpineCtrl spineCtrl;

    [SerializeField] private GameObject arrow;
    [SerializeField] private Animator groundAnim;
    [SerializeField] private Animator bubbleAnim;

    [SerializeField] private CanvasGroup group;

    /// <summary>
    /// 当前步骤
    /// </summary>
    private int _step = 0;
    private Tweener _contentTween;
    private bool _isCompleting;
    private bool _isTipPlaying;
    private bool _shouldCompleteAfterOpen;
    private bool _isCreateConfigured;
    private int _lifecycleVersion;
    private int _tipRequestVersion;

    private Action _closeCallback;

    private Spine.TrackEntry _tutorialEntry;
    private Spine.AnimationState.TrackEntryDelegate _tutorialCompleteHandler;

    public override void OnOpen()
    {
        base.OnOpen();

        _lifecycleVersion++;
        _tipRequestVersion++;
        _step = 0;
        _isCompleting = false;
        _isTipPlaying = false;
        _shouldCompleteAfterOpen = false;
        _isCreateConfigured = false;
        if (groundAnim)
            groundAnim.SetBool("isOut", false);

        if (!BindBtn())
        {
            _shouldCompleteAfterOpen = true;
            return;
        }

        if (IsOver())
        {
            this.LogWarning("[NormalGuidePopup] Open skipped because tipsKey is empty.");
            _shouldCompleteAfterOpen = true;
            return;
        }

        PlayTips(false).Forget();
        PlayTutorial01();
    }

    private bool BindBtn()
    {
        if (btn == null)
        {
            this.LogWarning("[NormalGuidePopup] Button is null.");
            return false;
        }

        btn.OnClick -= PlayNextStep;
        btn.OnClick += PlayNextStep;
        return true;
    }

    private void PlayNextStep(CustomButton btn)
    {
        if (_isCompleting || _isTipPlaying || !IsOpen)
            return;

        _step++;
        if (IsOver())
        {
            CompleteGuideAsync().Forget();
            return;
        }

        PlayTips().Forget();
    }

    private async UniTask HideTips()
    {
        if (arrow)
            arrow.gameObject.SetActive(false);

        if (bubbleAnim)
            bubbleAnim.SetBool("show", false);

        await UniTask.WaitForSeconds(1);
    }

    private async UniTask PlayTips(bool hasTutorial = true)
    {
        if (ContentTxt == null)
        {
            this.LogWarning("[NormalGuidePopup] ContentTxt is null.");
            return;
        }

        if (IsOver())
        {
            this.LogWarning($"[NormalGuidePopup] Tip step is out of range. step:{_step}");
            return;
        }

        int lifecycleVersion = _lifecycleVersion;
        int requestVersion = ++_tipRequestVersion;
        int targetStep = _step;
        _isTipPlaying = true;

        try
        {
            KillContentTween();
            await HideTips();

            if (!IsTipRequestCurrent(lifecycleVersion, requestVersion))
                return;

            if (tipsKey == null || targetStep < 0 || targetStep >= tipsKey.Count)
            {
                this.LogWarning($"[NormalGuidePopup] Tip step became invalid while waiting. step:{targetStep}");
                return;
            }

            var key = tipsKey[targetStep];
            if (string.IsNullOrWhiteSpace(key))
            {
                ContentTxt.text = string.Empty;
                return;
            }

            var content = LocalizationManager.GetTranslation(key);
            if (string.IsNullOrEmpty(content))
                content = key;

            ContentTxt.text = content;
            ContentTxt.UpdateFontAsset();
            ContentTxt.ForceMeshUpdate(true, true);
            ContentTxt.text = string.Empty;

            if (bubbleAnim)
                bubbleAnim.SetBool("show", true);

            Tweener contentTween = ContentTxt.DOText(content, textAnimDuration).OnComplete(() =>
            {
                if (IsTipRequestCurrent(lifecycleVersion, requestVersion) && arrow)
                    arrow.gameObject.SetActive(true);
            });
            _contentTween = contentTween;

            if (hasTutorial)
                PlayRandomTutorial();

            if (contentTween != null && contentTween.IsActive())
                await contentTween.AsyncWaitForCompletion();
        }
        finally
        {
            if (IsLifecycleCurrent(lifecycleVersion) && requestVersion == _tipRequestVersion)
                _isTipPlaying = false;
        }
    }

    private bool IsOver()
    {
        return tipsKey == null || _step >= tipsKey.Count;
    }

    private async UniTask CompleteGuideAsync()
    {
        if (_isCompleting)
            return;

        int lifecycleVersion = _lifecycleVersion;
        _shouldCompleteAfterOpen = false;
        _isCompleting = true;
        _tipRequestVersion++;
        UnbindBtn();
        KillTweens();

        if (groundAnim)
        {
            groundAnim.SetBool("isOut", true);
        }

        if (group) 
            group.DOFade(0, 0.5f);

        HideTips().Forget();
        
        await UniTask.WaitForSeconds(1f);

        if (!IsLifecycleCurrent(lifecycleVersion))
            return;

        UIManager.Instance?.DestroyUI(this);
    }

    public override void OnCreate()
    {
        base.OnCreate();
        // TODO: 注册按钮等（示例见 PictureVerifticationPopup）
    }

    public override void OnClose()
    {
        _lifecycleVersion++;
        _tipRequestVersion++;
        UnbindBtn();
        KillTweens();
        _isCompleting = false;
        _isTipPlaying = false;
        _shouldCompleteAfterOpen = false;
        _isCreateConfigured = false;
        base.OnClose();
        InvokeCloseCallback();
    }

    /// <summary>
    /// 用途：按 `PostcardStageStep` 打开对应的 `NormalGuidePopup_1` 至 `NormalGuidePopup_4` 资源。
    /// 调用边界：仅运行态调用；流程调用方负责决定阶段以及任何数据状态变更。
    /// 注意事项：本方法只创建 UI 和保存关闭回调，不读取或修改 DB、背包和配置数据。
    /// </summary>
    /// <param name="step">需要打开的引导阶段，默认第一阶段。</param>
    /// <param name="closeCallback">弹窗进入 Bear UI 关闭生命周期后执行一次；回调异常只记录日志，不中断 UI 清理。</param>
    /// <returns>打开成功时返回弹窗实例，否则返回 null。</returns>
    public static NormalGuidePopup Create(
        PostcardStageStep step = PostcardStageStep.One,
        Action closeCallback = null)
    {
        return CreateByPrefabName(GetStagePrefabName(step), closeCallback);
    }

    private static NormalGuidePopup CreateByPrefabName(
        string prefabName,
        Action closeCallback,
        string skinName = null)
    {
        if (!Application.isPlaying)
            return null;

        if (string.IsNullOrEmpty(prefabName))
            return null;

        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
            return null;

        NormalGuidePopup existingPanel = uiManager.GetUI<NormalGuidePopup>(prefabName);
        if (existingPanel != null && existingPanel.IsOpen)
        {
            existingPanel.LogWarning($"[NormalGuidePopup] Duplicate Create ignored because {prefabName} is already open.");
            return existingPanel;
        }

        var panel = uiManager.OpenUI<NormalGuidePopup>(prefabName, UILayer.Top);
        if (panel == null)
            return null;

        panel.SetCloseCallback(closeCallback);
        if (skinName != null)
            panel.SetSpineSkin(skinName);

        panel.CompleteAfterOpenIfNeeded();
        return panel;
    }

    /// <summary>
    /// 用途：打开指定阶段的明信片引导弹窗并等待其进入 Bear UI 关闭生命周期。
    /// 调用边界：仅由 Guide Flow 在 Unity 主线程调用；现有 Create(...) 回调入口保持兼容。
    /// 注意事项：同阶段弹窗已打开时返回 AlreadyRunning；创建失败返回 Failed；外部取消会销毁本次弹窗并返回 Cancelled。
    /// </summary>
    /// <param name="step">需要展示并等待关闭的引导阶段。</param>
    /// <param name="cancellationToken">Guide Flow 传入的统一取消令牌。</param>
    /// <returns>弹窗关闭、取消、重复运行或创建失败对应的 GuideFlowResult。</returns>
    public static async UniTask<GuideFlowResult> ShowAsync(
        PostcardStageStep step,
        CancellationToken cancellationToken = default)
    {
        return await ShowAsync(GetStagePrefabName(step), cancellationToken);
    }

    /// <summary>
    /// 用途：打开首次提示剧情引导指定阶段的 NormalGuidePopup，并等待其关闭。
    /// 调用边界：仅由 GameTipsGuideFlow 在 Unity 主线程运行时调用。
    /// 注意事项：阶段值必须对应同编号的 Resources prefab；取消、重复打开与创建失败沿用现有等待式弹窗结果。
    /// </summary>
    /// <param name="step">首次提示剧情引导阶段。</param>
    /// <param name="cancellationToken">Guide Flow 传入的统一取消令牌。</param>
    /// <param name="skinName">目标 Spine Skin 名称，默认 `box_original`；设置失败时保留 prefab 当前外观。</param>
    /// <returns>弹窗关闭、取消、重复运行或创建失败对应的 GuideFlowResult。</returns>
    public static async UniTask<GuideFlowResult> ShowAsync(
        GameTipsGuideStep step,
        CancellationToken cancellationToken = default,
        string skinName = DefaultSpineSkinName)
    {
        return await ShowAsync(GetStagePrefabName(step), cancellationToken, skinName);
    }

    private static async UniTask<GuideFlowResult> ShowAsync(
        string prefabName,
        CancellationToken cancellationToken,
        string skinName = null)
    {
        if (cancellationToken.IsCancellationRequested)
            return GuideFlowResult.Cancelled;

        if (!Application.isPlaying)
            return GuideFlowResult.Failed;

        if (string.IsNullOrEmpty(prefabName))
            return GuideFlowResult.Failed;

        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
            return GuideFlowResult.Failed;

        NormalGuidePopup existingPanel = uiManager.GetUI<NormalGuidePopup>(prefabName);
        if (existingPanel != null && existingPanel.IsOpen)
        {
            existingPanel.LogWarning($"[NormalGuidePopup] ShowAsync ignored because {prefabName} is already open.");
            return GuideFlowResult.AlreadyRunning;
        }

        var completionSource = new UniTaskCompletionSource<GuideFlowResult>();
        NormalGuidePopup panel = CreateByPrefabName(
            prefabName,
            () => completionSource.TrySetResult(GuideFlowResult.Completed),
            skinName);
        if (panel == null)
            return GuideFlowResult.Failed;

        try
        {
            return await completionSource.Task.AttachExternalCancellation(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (panel != null && panel.IsOpen)
                uiManager.DestroyUI(panel);

            return GuideFlowResult.Cancelled;
        }
    }


    #region 随机 Spine

    /// <summary>
    /// 用途：设置当前引导弹窗 Spine 的 Skin，未传名称时使用 `box_original`。
    /// 调用边界：仅在弹窗实例创建后由运行态 UI 或 Guide Flow 调用。
    /// 注意事项：Skin 名称为空、Spine 未绑定或资源中不存在目标 Skin 时返回 false，不创建替代资源。
    /// </summary>
    /// <param name="skinName">目标 Spine Skin 名称，默认 `box_original`。</param>
    /// <returns>Skin 设置成功返回 true，否则返回 false。</returns>
    public bool SetSpineSkin(string skinName = DefaultSpineSkinName)
    {
        if (string.IsNullOrWhiteSpace(skinName))
        {
            this.LogWarning("[NormalGuidePopup] Spine skin name is empty.");
            return false;
        }

        UISpineCtrl targetSpineCtrl = ResolveSpineCtrl();
        if (targetSpineCtrl == null)
        {
            this.LogWarning("[NormalGuidePopup] UISpineCtrl is null.");
            return false;
        }

        return targetSpineCtrl.SetSkin(skinName);
    }

    /// <summary>
    /// 用途：在当前 Spine 处于 `tutorial_idle` 时播放第一段引导动画，并在结束后回到 idle。
    /// 调用边界：由引导文案刷新随机触发，或由调试/动画按钮直接调用。
    /// 注意事项：非 idle 状态会忽略请求，避免打断正在播放的引导动画。
    /// </summary>
    public void PlayTutorial01()
    {
        // tutorial_01 ->  tutorial_idle
        if (!CanSwitchTutorialFromIdle())
            return;

        Spine.TrackEntry entry = PlayTutorialAnimation(Tutorial01AnimName, false);
        if (entry == null)
        {
            PlayTutorialIdle();
            return;
        }

        TrackTutorialEntry(entry, OnTutorialReturnToIdleComplete);
    }

    /// <summary>
    /// 用途：在当前 Spine 处于 `tutorial_idle` 时播放第二段串联引导动画，并在结束后回到 idle。
    /// 调用边界：由引导文案刷新随机触发，或由调试/动画按钮直接调用。
    /// 注意事项：串联过程会绑定当前 TrackEntry 完成回调，关闭弹窗时会统一清理。
    /// </summary>
    public void PlayTutorial02()
    {
        // tutorial02 -> tutorial02_idle -> tutorial02_return -> tutorial_idle
        if (!CanSwitchTutorialFromIdle())
            return;

        Spine.TrackEntry entry = PlayTutorialAnimation(Tutorial02AnimName, false);
        if (entry == null)
        {
            PlayTutorialIdle();
            return;
        }

        TrackTutorialEntry(entry, OnTutorial02EnterComplete);
    }

    /// <summary>
    /// 用途：在当前 Spine 处于 `tutorial_idle` 时播放第三段引导动画，并在结束后回到 idle。
    /// 调用边界：由引导文案刷新随机触发，或由调试/动画按钮直接调用。
    /// 注意事项：非 idle 状态会忽略请求，避免打断正在播放的引导动画。
    /// </summary>
    public void PlayTutorial03()
    {
        // tutorial_03 ->  tutorial_idle
        if (!CanSwitchTutorialFromIdle())
            return;

        Spine.TrackEntry entry = PlayTutorialAnimation(Tutorial03AnimName, false);
        if (entry == null)
        {
            PlayTutorialIdle();
            return;
        }

        TrackTutorialEntry(entry, OnTutorialReturnToIdleComplete);
    }

    #endregion

    private static string GetStagePrefabName(PostcardStageStep step)
    {
         return $"{nameof(NormalGuidePopup)}_{(int)step}";
    }

    private static string GetStagePrefabName(GameTipsGuideStep step)
    {
        return $"{nameof(NormalGuidePopup)}_{(int)step}";
    }

    private void SetCloseCallback(Action callback)
    {
        if (_isCreateConfigured)
        {
            this.LogWarning("[NormalGuidePopup] Duplicate Create ignored because this popup instance is already configured.");
            return;
        }

        _closeCallback = callback;
        _isCreateConfigured = true;
    }

    /// <summary>
    /// 在 Create 注入阶段参数与关闭回调后处理 OnOpen 检出的无效配置，避免同步关闭早于回调初始化。
    /// </summary>
    private void CompleteAfterOpenIfNeeded()
    {
        if (!_shouldCompleteAfterOpen)
            return;

        _shouldCompleteAfterOpen = false;
        CompleteGuideAsync().Forget();
    }

    /// <summary>
    /// 消费本次关闭回调；先清空并隔离调用方异常，避免重复关闭或异常中断 UIManager 后续清理。
    /// </summary>
    private void InvokeCloseCallback()
    {
        Action callback = _closeCallback;
        _closeCallback = null;
        if (callback == null)
            return;

        try
        {
            callback.Invoke();
        }
        catch (Exception exception)
        {
            this.LogError($"[NormalGuidePopup] Close callback failed. error:{exception}");
        }
    }

    private void UnbindBtn()
    {
        if (btn != null)
            btn.OnClick -= PlayNextStep;
    }

    private void KillTweens()
    {
        KillContentTween();
        ClearTrackedTutorialEntry();
    }

    private void KillContentTween()
    {
        if (_contentTween != null && _contentTween.IsActive())
            _contentTween.Kill();

        _contentTween = null;
    }

    private void PlayRandomTutorial()
    {
        if (!CanSwitchTutorialFromIdle())
            return;

        int randomIndex = UnityEngine.Random.Range(0, 3);
        if (randomIndex == 0)
        {
            PlayTutorial01();
        }
        else if (randomIndex == 1)
        {
            PlayTutorial02();
        }
        else
        {
            PlayTutorial03();
        }
    }

    private bool CanSwitchTutorialFromIdle()
    {
        UISpineCtrl targetSpineCtrl = ResolveSpineCtrl();
        if (targetSpineCtrl == null)
        {
            this.LogWarning("[NormalGuidePopup] SpineCtrl is null.");
            return false;
        }

        string currentAnimName = targetSpineCtrl.GetCurrentAnimationName();
        return string.Equals(currentAnimName, TutorialIdleAnimName, StringComparison.Ordinal);
    }

    private Spine.TrackEntry PlayTutorialAnimation(string animName, bool loop)
    {
        UISpineCtrl targetSpineCtrl = ResolveSpineCtrl();
        if (targetSpineCtrl == null)
            return null;

        try
        {
            return targetSpineCtrl.PlayAnimation(animName, loop, false);
        }
        catch (Exception e)
        {
            this.LogWarning($"[NormalGuidePopup] Play spine animation failed. anim:{animName}, error:{e.Message}");
            return null;
        }
    }

    private void TrackTutorialEntry(
        Spine.TrackEntry entry,
        Spine.AnimationState.TrackEntryDelegate completeHandler)
    {
        ClearTrackedTutorialEntry();
        if (entry == null || completeHandler == null)
            return;

        _tutorialEntry = entry;
        _tutorialCompleteHandler = completeHandler;
        _tutorialEntry.Complete += _tutorialCompleteHandler;
    }

    private bool IsTrackedTutorialEntry(Spine.TrackEntry entry)
    {
        return entry != null && ReferenceEquals(entry, _tutorialEntry);
    }

    private void OnTutorialReturnToIdleComplete(Spine.TrackEntry entry)
    {
        if (!IsTrackedTutorialEntry(entry))
            return;

        PlayTutorialIdle();
    }

    private void OnTutorial02EnterComplete(Spine.TrackEntry entry)
    {
        if (!IsTrackedTutorialEntry(entry))
            return;

        Spine.TrackEntry idleEntry = PlayTutorialAnimation(Tutorial02IdleAnimName, false);
        if (idleEntry == null)
        {
            PlayTutorial02Return();
            return;
        }

        TrackTutorialEntry(idleEntry, OnTutorial02IdleComplete);
    }

    private void OnTutorial02IdleComplete(Spine.TrackEntry entry)
    {
        if (!IsTrackedTutorialEntry(entry))
            return;

        PlayTutorial02Return();
    }

    private void PlayTutorial02Return()
    {
        Spine.TrackEntry returnEntry = PlayTutorialAnimation(Tutorial02ReturnAnimName, false);
        if (returnEntry == null)
        {
            PlayTutorialIdle();
            return;
        }

        TrackTutorialEntry(returnEntry, OnTutorialReturnToIdleComplete);
    }

    private void PlayTutorialIdle()
    {
        ClearTrackedTutorialEntry();
        PlayTutorialAnimation(TutorialIdleAnimName, true);
    }

    private UISpineCtrl ResolveSpineCtrl()
    {
        if (spineCtrl == null)
            spineCtrl = GetComponentInChildren<UISpineCtrl>(true);

        return spineCtrl;
    }

    private void ClearTrackedTutorialEntry()
    {
        if (_tutorialEntry != null && _tutorialCompleteHandler != null)
            _tutorialEntry.Complete -= _tutorialCompleteHandler;

        _tutorialEntry = null;
        _tutorialCompleteHandler = null;
    }

    private bool IsLifecycleCurrent(int lifecycleVersion)
    {
        return this != null && IsOpen && lifecycleVersion == _lifecycleVersion;
    }

    private bool IsTipRequestCurrent(int lifecycleVersion, int requestVersion)
    {
        return IsLifecycleCurrent(lifecycleVersion) &&
               !_isCompleting &&
               requestVersion == _tipRequestVersion;
    }
}
