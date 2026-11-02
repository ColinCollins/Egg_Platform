using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using Spine;
using UnityEngine;

public partial class ComicsPopup : BaseUIView
{
    public UISpineCtrl[] spinesCtrl;

    [Header("动画间隔")]
    [Tooltip("每个动画播放完到下一个动画开始之间的延迟（秒）")]
    [SerializeField] private float delayBetweenAnimations = 0f;

    public MoveAnimation moveAnimation;
    private Action _onCompleteCallback;
    private Queue<string> _animationQueue;
    private List<Spine.AnimationState> _animationStates;
    private List<UISpineCtrl> _validSpines;
    private int _pendingCompletionCount;
    private CancellationTokenSource _delayCts;

    public override void OnCreate()
    {
        base.OnCreate();
        NextBtn.OnClick += ClickClose;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        StartPlayAnimationsAsync();
    }

    public override void OnClose()
    {
        base.OnClose();
        CancelDelayTask();
        UnbindAnimationEvents();
        _onCompleteCallback = null;
        _animationQueue = null;
        _animationStates = null;
        _validSpines = null;
    }

    /// <summary>
    /// 开始按顺序播放全部动画（所有有效的 spine 同时播放同一动画）
    /// </summary>
    private async Task StartPlayAnimationsAsync()
    {
        if (spinesCtrl == null || spinesCtrl.Length == 0)
        {
            Debug.LogWarning("[ComicsPopup] spinesCtrl is null or empty.");
            OnAllAnimationsComplete();
            return;
        }

        AudioManager.PauseMusic();

        _validSpines = new List<UISpineCtrl>();
        _animationStates = new List<Spine.AnimationState>();
        _animationQueue = new Queue<string>();

        AudioManager.PlaySound("Comic");
        await UniTask.WaitForSeconds(1.8f);

        foreach (var spine in spinesCtrl)
        {
            if (spine == null)
            {
                continue;
            }

            var animState = spine.GetAnimationState();
            if (animState == null)
            {
                Debug.LogWarning($"[ComicsPopup] Spine '{spine.name}' has no AnimationState, skipping.");
                continue;
            }

            _validSpines.Add(spine);
            _animationStates.Add(animState);

            // 只从第一个有效的 spine 获取动画队列（假设所有 spine 动画一样）
            if (_animationQueue.Count == 0 &&
                animState.Data != null &&
                animState.Data.SkeletonData != null &&
                animState.Data.SkeletonData.Animations != null)
            {
                foreach (var anim in animState.Data.SkeletonData.Animations)
                {
                    if (anim != null)
                    {
                        _animationQueue.Enqueue(anim.Name);
                    }
                }
            }
        }

        if (_validSpines.Count == 0)
        {
            Debug.LogWarning("[ComicsPopup] No valid spine found.");
            OnAllAnimationsComplete();
            return;
        }

        if (_animationQueue.Count == 0)
        {
            Debug.LogWarning("[ComicsPopup] No animations to play.");
            OnAllAnimationsComplete();
            return;
        }

        // 绑定动画完成事件
        foreach (var state in _animationStates)
        {
            state.Complete += OnAnimationComplete;
        }

        // 播放第一个动画
        PlayNextAnimation();
    }

    /// <summary>
    /// 所有有效的 spine 同时播放下一个动画
    /// </summary>
    private void PlayNextAnimation()
    {
        if (_animationQueue == null || _animationQueue.Count == 0)
        {
            OnAllAnimationsComplete();
            return;
        }

        string animName = _animationQueue.Dequeue();
        _pendingCompletionCount = _validSpines.Count;

        foreach (var spine in _validSpines)
        {
            spine.PlayAnimation(animName, loop: false, checkCurrent: false);
        }
    }

    /// <summary>
    /// 单个 spine 的单个动画播放完成回调
    /// </summary>
    private void OnAnimationComplete(TrackEntry trackEntry)
    {
        _pendingCompletionCount--;
        if (_pendingCompletionCount <= 0)
        {
            PlayNextWithDelayAsync().Forget();
        }
    }

    /// <summary>
    /// 延迟后播放下一个动画
    /// </summary>
    private async UniTaskVoid PlayNextWithDelayAsync()
    {
        if (delayBetweenAnimations > 0f)
        {
            CancelDelayTask();
            _delayCts = new CancellationTokenSource();
            try
            {
                await UniTask.Delay((int)(delayBetweenAnimations * 1000), cancellationToken: _delayCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                _delayCts?.Dispose();
                _delayCts = null;
            }
        }

        PlayNextAnimation();
    }

    /// <summary>
    /// 取消延迟任务
    /// </summary>
    private void CancelDelayTask()
    {
        if (_delayCts != null)
        {
            _delayCts.Cancel();
            _delayCts.Dispose();
            _delayCts = null;
        }
    }

    /// <summary>
    /// 所有动画播放完毕
    /// </summary>
    private void OnAllAnimationsComplete()
    {
        // AudioManager.ResumeMusic();
        moveAnimation.gameObject.SetActive(true);
    }

    private void ClickClose(CustomButton _)
    {
        UnbindAnimationEvents();
        DB.GameData.HasComics = true;
        DB.GameData.Save();

        _onCompleteCallback?.Invoke();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.DestroyUI(this);
        }
        else
        {
            Debug.LogError("[ComicsPopup] UIManager.Instance is null.");
        }
    }

    /// <summary>
    /// 解绑所有动画完成事件
    /// </summary>
    private void UnbindAnimationEvents()
    {
        CancelDelayTask();

        if (_animationStates != null)
        {
            foreach (var state in _animationStates)
            {
                if (state != null)
                {
                    state.Complete -= OnAnimationComplete;
                }
            }
        }
    }

    /// <summary>
    /// 创建 ComicsPopup 弹窗，按顺序播放 Spine 全部动画
    /// </summary>
    /// <param name="onComplete">全部动画播放完成后的回调</param>
    /// <returns>弹窗实例</returns>
    public static ComicsPopup Create(Action onComplete = null)
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("[ComicsPopup] UIManager.Instance is null. Cannot create popup.");
            return null;
        }

        var panel = UIManager.Instance.OpenUI<ComicsPopup>(
            $"{typeof(ComicsPopup).Name}",
            UILayer.Top);

        if (panel == null)
        {
            Debug.LogError("[ComicsPopup] Failed to open ComicsPopup from UIManager.");
            return null;
        }

        panel._onCompleteCallback = onComplete;
        return panel;
    }
}
