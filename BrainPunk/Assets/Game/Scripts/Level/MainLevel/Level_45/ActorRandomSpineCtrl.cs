using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 触发 play 状态的时候，能够根据 SpineAnimState List 随机选择一个 state 进行播放。
/// loopSeconds 时间用于在播放最后一个 idle 动画的时候的持续时间。
/// 当前逻辑为 loopSeconds 在进入最后一个动画的时候就开始计时了，因此要保留足够的动画播放时间是最好的。
/// 
/// </summary>
public class ActorRandomSpineCtrl : MonoBehaviour
{
    [Serializable]
    public class SpineAnimState
    {
        [LabelText("State Name")]
        public string stateName;

        [LabelText("Anim Names")]
        public List<string> animNames = new List<string>();

        [LabelText("Loop")]
        public bool loop = true;

        [ShowIf(nameof(loop))]
        [LabelText("Loop Seconds")]
        [MinValue(0f)]
        [Tooltip("当 loop=true 且进入最后一个循环动画后，保持 loop 的时长（秒）。<=0 表示立即切回 original。")]
        public float loopSeconds = 0f;

        [LabelText("Next State Name")]
        public string nextStateName;
    }

    [SerializeField] private ActorSpineCtrl actorSpineCtrl;

    [Header("States")]
    [ListDrawerSettings(Expanded = true)]
    [SerializeField] private List<SpineAnimState> states = new List<SpineAnimState>();
    [SerializeField] private bool playOriginOnAwake;

    [Header("Complete Timing")]
    [SerializeField, Min(0f)] private float originalHoldMinSeconds = 1f;
    [SerializeField, Min(0f)] private float originalHoldMaxSeconds = 2f;

    public event Action RandomCycleCompleted;

    private Spine.TrackEntry currentEntry;
    private string currentStateName;

    private string originalAnimName;
    private bool hasOriginalAnim;
    private Coroutine restoreToOriginalCoroutine;

    private Spine.AnimationState.TrackEntryDelegate _onLoopEntryStart;
    private Coroutine loopDurationCoroutine;
    private bool isWaitingLoopStart;
    private SpineAnimState pendingLoopState;

    private void Awake()
    {
        if (actorSpineCtrl == null)
        {
            actorSpineCtrl = GetComponent<ActorSpineCtrl>();
        }

        if (actorSpineCtrl == null)
        {
            actorSpineCtrl = GetComponentInChildren<ActorSpineCtrl>();
        }

        if (playOriginOnAwake)
        {
            PlayRandom();
        }
    }

    /// <summary>
    /// 从 states 中随机选择一个 state 播放。
    /// </summary>
    /// <returns>如果本次播放会在完成后触发 RandomCycleCompleted，则返回 true，否则返回 false</returns>
    public bool PlayRandom()
    {
        if (actorSpineCtrl == null)
        {
            Debug.LogWarning("[ActorRandomSpineCtrl] ActorSpineCtrl is null.");
            return false;
        }

        CaptureOriginalAnimationIfNeeded();
        StopRestoreToOriginalCoroutine();

        SpineAnimState state = GetRandomPlayableState();
        if (state == null)
            return false;

        return PlayState(state);
    }

    private bool PlayState(SpineAnimState state)
    {
        if (state.loop)
        {
            var sequence = GetValidAnimSequence(state.animNames);
            if (sequence.Count == 0)
            {
                Debug.LogWarning($"[ActorRandomSpineCtrl] Animation list is empty. state:{state.stateName}");
                return false;
            }

            return PlayStateSequenceLoop(state, sequence);
        }

        string animName = GetRandomAnimName(state.animNames);
        if (string.IsNullOrEmpty(animName))
        {
            Debug.LogWarning($"[ActorRandomSpineCtrl] Animation list is empty. state:{state.stateName}");
            return false;
        }

        return PlayStateAnim(state, animName, false);
    }

    private bool PlayStateAnim(SpineAnimState state, string animName, bool loop)
    {
        ClearCurrentEntryComplete();

        currentStateName = state.stateName;
        // 强制重新触发播放，避免随机到同名动画时因 checkCurrent 跳过导致无法收到 Complete/Start 信号
        currentEntry = actorSpineCtrl.PlayAnimation(animName, loop, false);

        if (currentEntry != null && !loop)
        {
            currentEntry.Complete += OnStateAnimComplete;
        }

        return currentEntry != null;
    }

    private bool PlayStateSequence(SpineAnimState state, List<string> animNames)
    {
        ClearCurrentEntryComplete();

        currentStateName = state.stateName;

        // 按序列播完一次（最后一个不循环），用最后一个的 Complete 作为“state 播完”的信号
        currentEntry = PlayAnimationSequence(animNames, false);
        if (currentEntry != null)
            currentEntry.Complete += OnStateAnimComplete;

        return currentEntry != null;
    }

    private bool PlayStateSequenceLoop(SpineAnimState state, List<string> animNames)
    {
        ClearCurrentEntryComplete();
        ClearLoopStart();
        StopLoopDurationCoroutine();

        currentStateName = state.stateName;

        // loop=true：从 index=0 播到最后一个，并只让最后一个循环
        currentEntry = PlayAnimationSequence(animNames, true);
        if (currentEntry == null)
            return false;

        // 当进入最后一个（loop）动画时开始计时，到点后回 original 并 complete
        isWaitingLoopStart = true;
        pendingLoopState = state;

        if (animNames.Count <= 1)
        {
            // 单动画 loop：立即视为进入 loop
            OnLoopEntryStarted();
            return true;
        }

        _onLoopEntryStart = _ => OnLoopEntryStarted();
        currentEntry.Start += _onLoopEntryStart;

        // 兜底：如果 Start 在订阅前触发，持续等待直到最后一个 entry 真正开始播放
        StartCoroutine(EnsureLoopEntryStartedCo());
        return true;
    }

    private Spine.TrackEntry PlayAnimationSequence(IReadOnlyList<string> animNames, bool loopLast)
    {
        if (actorSpineCtrl == null || actorSpineCtrl.AnimationState == null)
        {
            Debug.LogWarning("[ActorRandomSpineCtrl] ActorSpineCtrl AnimationState is null or not initialized.");
            return null;
        }

        if (animNames == null || animNames.Count == 0)
        {
            Debug.LogWarning("[ActorRandomSpineCtrl] Animation names list is empty.");
            return null;
        }

        Spine.TrackEntry lastEntry = null;
        bool hasSetFirst = false;

        for (int i = 0; i < animNames.Count; i++)
        {
            string animName = animNames[i];
            if (string.IsNullOrEmpty(animName))
                continue;

            bool isLast = i == animNames.Count - 1;
            bool loop = isLast && loopLast;

            if (!hasSetFirst)
            {
                lastEntry = actorSpineCtrl.AnimationState.SetAnimation(0, animName, loop);
                hasSetFirst = true;
            }
            else
            {
                lastEntry = actorSpineCtrl.AnimationState.AddAnimation(0, animName, loop, 0f);
            }
        }

        if (!hasSetFirst)
        {
            Debug.LogWarning("[ActorRandomSpineCtrl] Animation names list contains no valid entries.");
            return null;
        }

        return lastEntry;
    }

    private void OnStateAnimComplete(Spine.TrackEntry entry)
    {
        if (entry != currentEntry)
        {
            return;
        }

        SpineAnimState completedState = FindState(currentStateName);
        string nextStateName = completedState != null ? completedState.nextStateName : string.Empty;

        ClearCurrentEntryComplete();
        if (!string.IsNullOrEmpty(nextStateName))
        {
            SpineAnimState nextState = FindState(nextStateName);
            if (nextState != null)
            {
                PlayState(nextState);
                return;
            }
        }

        RestoreToOriginalThenComplete();
    }

    private void RestoreToOriginalThenComplete()
    {
        StopRestoreToOriginalCoroutine();

        if (actorSpineCtrl != null && hasOriginalAnim && !string.IsNullOrEmpty(originalAnimName))
        {
            actorSpineCtrl.PlayAnimation(originalAnimName, true, false);
        }

        float min = Mathf.Min(originalHoldMinSeconds, originalHoldMaxSeconds);
        float max = Mathf.Max(originalHoldMinSeconds, originalHoldMaxSeconds);
        float delay = UnityEngine.Random.Range(min, max);

        restoreToOriginalCoroutine = StartCoroutine(RestoreToOriginalThenCompleteCo(delay));
    }

    private System.Collections.IEnumerator RestoreToOriginalThenCompleteCo(float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        RandomCycleCompleted?.Invoke();

        hasOriginalAnim = false;
        originalAnimName = string.Empty;
        restoreToOriginalCoroutine = null;
    }

    private SpineAnimState FindState(string stateName)
    {
        if (states == null || string.IsNullOrEmpty(stateName))
        {
            return null;
        }

        for (int i = 0; i < states.Count; i++)
        {
            SpineAnimState state = states[i];
            if (state != null && string.Equals(state.stateName, stateName, StringComparison.OrdinalIgnoreCase))
            {
                return state;
            }
        }

        return null;
    }

    private string GetRandomAnimName(List<string> animNames)
    {
        if (animNames == null || animNames.Count == 0)
        {
            return string.Empty;
        }

        int startIndex = UnityEngine.Random.Range(0, animNames.Count);
        for (int i = 0; i < animNames.Count; i++)
        {
            int index = (startIndex + i) % animNames.Count;
            if (!string.IsNullOrEmpty(animNames[index]))
            {
                return animNames[index];
            }
        }

        return string.Empty;
    }

    private List<string> GetValidAnimSequence(List<string> animNames)
    {
        var sequence = new List<string>();
        if (animNames == null)
            return sequence;

        for (int i = 0; i < animNames.Count; i++)
        {
            string animName = animNames[i];
            if (!string.IsNullOrEmpty(animName))
                sequence.Add(animName);
        }

        return sequence;
    }

    private void ClearCurrentEntryComplete()
    {
        if (currentEntry != null)
        {
            currentEntry.Complete -= OnStateAnimComplete;
        }
    }

    private void ClearLoopStart()
    {
        if (currentEntry != null && _onLoopEntryStart != null)
        {
            currentEntry.Start -= _onLoopEntryStart;
        }

        _onLoopEntryStart = null;
        isWaitingLoopStart = false;
        pendingLoopState = null;
    }

    private void StopLoopDurationCoroutine()
    {
        if (loopDurationCoroutine != null)
        {
            StopCoroutine(loopDurationCoroutine);
            loopDurationCoroutine = null;
        }
    }

    private void OnLoopEntryStarted()
    {
        if (!isWaitingLoopStart)
            return;

        float loopSeconds = pendingLoopState != null ? pendingLoopState.loopSeconds : 0f;

        isWaitingLoopStart = false;
        ClearLoopStart();
        StopLoopDurationCoroutine();

        if (loopSeconds <= 0f)
        {
            RestoreToOriginalThenComplete();
            return;
        }

        loopDurationCoroutine = StartCoroutine(LoopDurationThenComplete(loopSeconds));
    }

    private System.Collections.IEnumerator LoopDurationThenComplete(float loopSeconds)
    {
        yield return new WaitForSeconds(loopSeconds);
        RestoreToOriginalThenComplete();
        loopDurationCoroutine = null;
    }

    private System.Collections.IEnumerator EnsureLoopEntryStartedCo()
    {
        const float timeoutSeconds = 10f;
        float endTime = Time.unscaledTime + timeoutSeconds;

        while (isWaitingLoopStart && currentEntry != null && Time.unscaledTime < endTime)
        {
            if (currentEntry.TrackTime > 0.0001f)
                break;

            yield return null;
        }

        if (isWaitingLoopStart && currentEntry != null && currentEntry.TrackTime > 0.0001f)
        {
            OnLoopEntryStarted();
        }
    }

    private void StopRestoreToOriginalCoroutine()
    {
        if (restoreToOriginalCoroutine != null)
        {
            StopCoroutine(restoreToOriginalCoroutine);
            restoreToOriginalCoroutine = null;
        }
    }

    private void CaptureOriginalAnimationIfNeeded()
    {
        if (hasOriginalAnim)
            return;

        if (actorSpineCtrl == null)
            return;

        originalAnimName = actorSpineCtrl.GetCurrentAnimationName();
        hasOriginalAnim = !string.IsNullOrEmpty(originalAnimName);
    }

    private SpineAnimState GetRandomPlayableState()
    {
        if (states == null || states.Count == 0)
            return null;

        var candidates = new List<SpineAnimState>();
        for (int i = 0; i < states.Count; i++)
        {
            SpineAnimState state = states[i];
            if (state == null || string.IsNullOrEmpty(state.stateName))
                continue;

            if (state.animNames == null || state.animNames.Count == 0)
                continue;

            bool hasAnyValidAnim = false;
            for (int j = 0; j < state.animNames.Count; j++)
            {
                if (!string.IsNullOrEmpty(state.animNames[j]))
                {
                    hasAnyValidAnim = true;
                    break;
                }
            }

            if (hasAnyValidAnim)
                candidates.Add(state);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void OnDestroy()
    {
        ClearCurrentEntryComplete();
        StopRestoreToOriginalCoroutine();
        ClearLoopStart();
        StopLoopDurationCoroutine();
    }
}
