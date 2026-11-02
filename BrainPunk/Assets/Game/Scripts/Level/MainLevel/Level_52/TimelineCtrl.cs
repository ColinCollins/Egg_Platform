using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

/// <summary>
/// 控制 PlayableDirector（Timeline）。销毁或禁用时解除回调、停止播放，避免泄漏与野回调。
/// </summary>
public class TimelineCtrl : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;

    [Tooltip("启用时若未指定 Director，则在自身上 GetComponent。")]
    [SerializeField] private bool autoFindDirector = true;

    [Tooltip("每次 OnEnable 是否自动 Play（适合关卡随显随播）。")]
    [SerializeField]
    private bool playOnEnable;

    [Tooltip("OnDisable 时是否 Stop（释放当前 Graph 的播放状态，利于切换关卡内存）。")]
    [SerializeField]
    private bool stopOnDisable = true;

    [Header("播放事件")]
    [Tooltip("Timeline 开始播放时触发")]
    [SerializeField]
    private UnityEvent onDirectorPlayed;

    [Tooltip("Timeline 暂停时触发")]
    [SerializeField]
    private UnityEvent onDirectorPaused;

    [Tooltip("Timeline 恢复播放时触发")]
    [SerializeField]
    private UnityEvent onDirectorResumed;

    [Tooltip("Timeline 停止时触发（包括自然结束和手动 Stop）")]
    [SerializeField]
    private UnityEvent onDirectorStopped;

    [Tooltip("Timeline 自然播放完成时触发（排除手动 Stop）")]
    [SerializeField]
    private UnityEvent onDirectorCompleted;

    private bool _stoppedSubscribed;
    private bool _pausedSubscribed;
    private System.Action<PlayableDirector> _stoppedHandler;
    private System.Action<PlayableDirector> _pausedHandler;

    private void Awake()
    {
        if (director == null && autoFindDirector)
            director = GetComponent<PlayableDirector>();

        _stoppedHandler = OnDirectorStoppedInternal;
        _pausedHandler = OnDirectorPausedInternal;
    }

    private void OnEnable()
    {
        SubscribeEventsIfNeeded();
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        if (stopOnDisable)
            StopDirectorSoft();
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        // 与 OnDisable 双保险：整关销毁时若未先 Disable，仍要摘掉委托
        UnsubscribeEvents();
        StopDirectorSoft();
    }

    private void SubscribeEventsIfNeeded()
    {
        if (director == null)
            return;

        if (!_stoppedSubscribed && _stoppedHandler != null)
        {
            director.stopped += _stoppedHandler;
            _stoppedSubscribed = true;
        }

        if (!_pausedSubscribed && _pausedHandler != null)
        {
            director.paused += _pausedHandler;
            _pausedSubscribed = true;
        }
    }

    private void UnsubscribeEvents()
    {
        if (director == null)
            return;

        if (_stoppedSubscribed && _stoppedHandler != null)
        {
            director.stopped -= _stoppedHandler;
            _stoppedSubscribed = false;
        }

        if (_pausedSubscribed && _pausedHandler != null)
        {
            director.paused -= _pausedHandler;
            _pausedSubscribed = false;
        }
    }

    private void OnDirectorStoppedInternal(PlayableDirector d)
    {
        onDirectorStopped?.Invoke();
        OnStopped?.Invoke();

        // 判断是否自然播放完成（时间接近总时长）
        if (d.playableAsset != null && d.time >= d.playableAsset.duration - 0.01f)
        {
            onDirectorCompleted?.Invoke();
            OnCompleted?.Invoke();
        }
    }

    private void OnDirectorPausedInternal(PlayableDirector _)
    {
        onDirectorPaused?.Invoke();
        OnPaused?.Invoke();
    }

    /// <summary>
    /// Stop 并避免在 Director 已销毁时反复访问。
    /// </summary>
    private void StopDirectorSoft()
    {
        if (director == null)
            return;

        if (director.state == PlayState.Playing)
            director.Stop();
    }

    // ========== 代码动态添加回调 ==========

    /// <summary>播放开始时的回调（代码动态添加）</summary>
    public event System.Action OnPlayed;

    /// <summary>暂停时的回调（代码动态添加）</summary>
    public event System.Action OnPaused;

    /// <summary>恢复播放时的回调（代码动态添加）</summary>
    public event System.Action OnResumed;

    /// <summary>停止时的回调（代码动态添加）</summary>
    public event System.Action OnStopped;

    /// <summary>自然播放完成时的回调（代码动态添加）</summary>
    public event System.Action OnCompleted;

    public PlayableDirector Director => director;

    public bool TryPlay(out string error)
    {
        error = string.Empty;
        if (director == null)
        {
            error = "PlayableDirector is null.";
            return false;
        }

        if (director.playableAsset == null)
        {
            error = "PlayableAsset is null.";
            return false;
        }

        try
        {
            SubscribeEventsIfNeeded();
            director.Play();
            onDirectorPlayed?.Invoke();
            OnPlayed?.Invoke();
        }
        catch (System.Exception ex)
        {
            error = $"Exception on Play: {ex.Message}";
            return false;
        }

        if (director.state != PlayState.Playing)
        {
            error = $"Director state is {director.state} after Play().";
            return false;
        }

        return true;
    }

    public void Play()
    {
        TryPlay(out _);
    }

    public void Play(PlayableAsset asset, DirectorWrapMode mode = DirectorWrapMode.Hold)
    {
        if (director == null)
            return;

        director.playableAsset = asset;
        director.extrapolationMode = mode;
        Play();
    }

    public void Stop()
    {
        StopDirectorSoft();
    }

    public void Pause()
    {
        if (director == null)
            return;

        director.Pause();
    }

    public void Resume()
    {
        if (director == null)
            return;

        director.Resume();
        onDirectorResumed?.Invoke();
        OnResumed?.Invoke();
    }

    public void SetTime(double timeSeconds)
    {
        if (director == null)
            return;

        director.time = timeSeconds;
        director.Evaluate();
    }

    /// <summary>
    /// 播放并在本次播放结束（stopped）时完成；物体销毁或传入 token 取消时会解除监听并 TrySetCanceled。
    /// </summary>
    public async UniTask PlayAndWaitStoppedAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        var dir = director;
        if (dir == null)
            return;

        if (cancellationToken == default)
            cancellationToken = this.GetCancellationTokenOnDestroy();

        var tcs = new UniTaskCompletionSource();

        void Once(PlayableDirector _)
        {
            dir.stopped -= Once;
            tcs.TrySetResult();
        }

        var registration = cancellationToken.Register(() =>
        {
            dir.stopped -= Once;
            if (dir != null && dir.state == PlayState.Playing)
                dir.Stop();
            tcs.TrySetCanceled(cancellationToken);
        });

        try
        {
            dir.stopped += Once;
            dir.Play();
            await tcs.Task;
        }
        finally
        {
            registration.Dispose();
            dir.stopped -= Once;
        }
    }
}
