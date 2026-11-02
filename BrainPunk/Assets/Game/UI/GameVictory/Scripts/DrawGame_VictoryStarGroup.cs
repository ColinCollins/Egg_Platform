using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draw 胜利面板的星级展示组件。
/// 星级只来自当前 Draw 关卡结算传入的数值，不读取 Bag、Postcard 或任何存档数据。
/// </summary>
public sealed class DrawGame_VictoryStarGroup : MonoBehaviour
{
    #region Constants

    private const int MaxStarCount = 3;
    private const string StarBreathObjectName = "UI_common_xingfaguang_01";
    private const string PlayTriggerName = "play";
    private const string CollectedStarSoundName = "collectedStar";

    #endregion

    #region Inspector

    /// <summary>
    /// 用途：按 Draw 星级顺序展示的 Toggle 列表。
    /// 调用边界：由 prefab 显式绑定；缺失时仅在运行态从当前节点子层级补找。
    /// 注意事项：列表顺序代表第 1 至第 3 颗星，最多使用三个有效 Toggle。
    /// </summary>
    [SerializeField] private List<Toggle> starToggles = new List<Toggle>();

    /// <summary>
    /// 用途：胜利面板入场结束后，到第一颗 Draw 星开始播放前的等待时间。
    /// 调用边界：仅用于当前胜利面板生命周期内的星级表现。
    /// 注意事项：负数会按 0 处理。
    /// </summary>
    [SerializeField] private float waitingPlayDelay = 0.0f;

    /// <summary>
    /// 用途：多颗 Draw 星依次播放时的固定间隔。
    /// 调用边界：仅在本次结算星数大于 1 时产生可见效果。
    /// 注意事项：负数会按 0 处理。
    /// </summary>
    [SerializeField] private float waitingAnimInterval = 0.18f;

    #endregion

    #region Runtime State

    private readonly List<Tween> _runningTweens = new List<Tween>();
    private Action _onAnimationComplete;
    private int _animationVersion;

    #endregion

    #region Public API

    /// <summary>
    /// 按 Draw 永久星数刷新胜利星组，并只为本轮新增星播放动画。
    /// 调用边界：仅由 DrawGame_GameVictoryPanel 在运行时入场完成后调用。
    /// 注意事项：本方法只消费面板传入的缓存数值，不读取或写入 DB、Bag、Postcard；无可播放星星时不会调用完成回调。
    /// </summary>
    /// <param name="collectedStarCount">当前 Draw 关卡已经永久获得的最高星数。</param>
    /// <param name="waitingAnimStarCount">本轮新增且等待播放动画的星数。</param>
    /// <param name="animationComplete">全部星星表现结束后的回调，可为空。</param>
    /// <returns>至少有一颗星开始等待或播放时返回 true，否则返回 false。</returns>
    public bool Refresh(
        int collectedStarCount,
        int waitingAnimStarCount,
        Action animationComplete = null)
    {
        if (!Application.isPlaying)
            return false;

        StopAnimations(false);
        EnsureToggles();
        PrepareStarVisuals();

        int visibleStarCount = Mathf.Clamp(
            collectedStarCount,
            0,
            GetToggleCount());
        int animStarCount = Mathf.Clamp(
            waitingAnimStarCount,
            0,
            visibleStarCount);
        int staticStarCount = visibleStarCount - animStarCount;
        ResetAllToggles();
        for (int i = 0; i < staticStarCount; i++)
        {
            Toggle toggle = GetToggle(i);
            if (toggle != null)
                toggle.SetIsOnWithoutNotify(true);
        }

        if (animStarCount <= 0)
        {
            ShowEnabledStarBreathObjects();
            return false;
        }

        _onAnimationComplete = animationComplete;
        int currentAnimationVersion = ++_animationVersion;
        ScheduleStarAnimations(
            animStarCount,
            staticStarCount,
            currentAnimationVersion);
        return true;
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        StopAnimations(true);
    }

    private void OnDestroy()
    {
        StopAnimations(true);
    }

    #endregion

    #region Display Flow

    private void ScheduleStarAnimations(
        int starCount,
        int startIndex,
        int currentAnimationVersion)
    {
        float firstStarDelay = Mathf.Max(0f, waitingPlayDelay);
        float interval = Mathf.Max(0f, waitingAnimInterval);
        float completeDelay = firstStarDelay;

        for (int i = 0; i < starCount; i++)
        {
            Toggle toggle = GetToggle(startIndex + i);
            if (toggle == null)
                continue;

            float playDelay = firstStarDelay + interval * i;
            completeDelay = Mathf.Max(
                completeDelay,
                playDelay + GetAnimatorDuration(toggle.GetComponent<Animator>()));
            ScheduleToggleOn(toggle, playDelay, currentAnimationVersion);
        }

        Tween completeTween = DOVirtual.DelayedCall(
                completeDelay,
                () => CompleteAnimation(currentAnimationVersion))
            .SetUpdate(true);
        _runningTweens.Add(completeTween);
    }

    private void ScheduleToggleOn(
        Toggle toggle,
        float playDelay,
        int currentAnimationVersion)
    {
        Tween tween = DOVirtual.DelayedCall(
                Mathf.Max(0f, playDelay),
                () =>
                {
                    if (!IsAnimationVersionActive(currentAnimationVersion) || toggle == null)
                        return;

                    Animator animator = toggle.GetComponent<Animator>();
                    if (animator != null)
                        animator.SetTrigger(PlayTriggerName);

                    AudioManager.PlaySound(CollectedStarSoundName);
                    toggle.SetIsOnWithoutNotify(true);
                })
            .SetUpdate(true);
        _runningTweens.Add(tween);
    }

    private void CompleteAnimation(int currentAnimationVersion)
    {
        if (!IsAnimationVersionActive(currentAnimationVersion))
            return;

        Action complete = _onAnimationComplete;
        _onAnimationComplete = null;
        ShowEnabledStarBreathObjects();
        complete?.Invoke();
    }

    private void PrepareStarVisuals()
    {
        int toggleCount = GetToggleCount();
        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle == null)
                continue;

            Animator animator = toggle.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = true;

            Transform breathTransform = toggle.transform.Find(StarBreathObjectName);
            if (breathTransform != null)
                breathTransform.gameObject.SetActive(false);
        }
    }

    private void ShowEnabledStarBreathObjects()
    {
        int toggleCount = GetToggleCount();
        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle == null)
                continue;

            Transform breathTransform = toggle.transform.Find(StarBreathObjectName);
            if (breathTransform == null)
                continue;

            breathTransform.gameObject.SetActive(toggle.isOn);
            if (!toggle.isOn)
                continue;

            Animator animator = toggle.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = false;
        }
    }

    private void ResetAllToggles()
    {
        int toggleCount = GetToggleCount();
        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle != null)
                toggle.SetIsOnWithoutNotify(false);
        }
    }

    #endregion

    #region Toggle Helpers

    private void EnsureToggles()
    {
        if (starToggles == null)
            starToggles = new List<Toggle>();

        starToggles.RemoveAll(toggle => toggle == null);
        if (starToggles.Count > 0)
            return;

        Toggle[] childToggles = GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < childToggles.Length && i < MaxStarCount; i++)
            starToggles.Add(childToggles[i]);
    }

    private int GetToggleCount()
    {
        return starToggles == null
            ? 0
            : Mathf.Min(MaxStarCount, starToggles.Count);
    }

    private Toggle GetToggle(int index)
    {
        if (starToggles == null || index < 0 || index >= GetToggleCount())
            return null;

        return starToggles[index];
    }

    private bool IsAnimationVersionActive(int currentAnimationVersion)
    {
        return currentAnimationVersion == _animationVersion && isActiveAndEnabled;
    }

    #endregion

    #region Animation Helpers

    private static float GetAnimatorDuration(Animator animator)
    {
        RuntimeAnimatorController controller =
            animator != null ? animator.runtimeAnimatorController : null;
        if (controller == null || controller.animationClips == null)
            return 0f;

        float duration = 0f;
        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null)
                duration = Mathf.Max(duration, clip.length);
        }

        if (animator.speed == 0f)
            return duration;

        return duration / Mathf.Abs(animator.speed);
    }

    private void StopAnimations(bool notifyAnimationComplete)
    {
        for (int i = 0; i < _runningTweens.Count; i++)
            _runningTweens[i]?.Kill();

        _runningTweens.Clear();
        _animationVersion++;

        Action complete = _onAnimationComplete;
        _onAnimationComplete = null;
        if (notifyAnimationComplete)
            complete?.Invoke();
    }

    #endregion
}
