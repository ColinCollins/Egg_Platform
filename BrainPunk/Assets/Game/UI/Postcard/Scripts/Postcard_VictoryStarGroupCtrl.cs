using System;
using System.Collections.Generic;
using Config;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// version 1.0 
/// 判断当前关卡的 levelData levelVictoryStarCount 先执行获得 star 对 Postcard CollectedStar 数据进行更新。然后开始执行动画，星星 toggle 展示动画。
/// 根据当前已经 CollectedStar 数量将对应 toggle 变为 true。
/// 如果存在 WaitingAnim，对应 index 的 Postcard_UIStarCollection 进行动画展示，toggle 切换为 true 播放一个缩放。如果存在复数个 Star 要播放动画，需要有一个固定延迟间隔。
/// 若不存在 waitingAnim，则不播放动画。
/// 
/// 开启时记录 postcard 的 cache 数据。然后清理掉。利用当前生命周期的数据就可以了。
/// 一个 Level 的 CollectedStar 数量最大值为 3。从 1 开始。不可能出现超过 3 或者小于 1 的数据。
/// </summary>
public class Postcard_VictoryStarGroupCtrl : MonoBehaviour
{
    private const string StarBreathObjectName = "UI_common_xingfaguang_01";

    #region Inspector

    /// <summary>
    /// 用途：胜利界面中按顺序展示明信片星星状态的 Toggle 列表。
    /// 调用边界：优先由 prefab 显式绑定；未绑定时运行态会从当前节点子物体自动收集。
    /// 注意事项：顺序代表星星显示顺序，最多只消费前三个有效 Toggle。
    /// </summary>
    [SerializeField] private List<Toggle> starToggles;

    [SerializeField] private float waitingPlayDelay = 2f;

    /// <summary>
    /// 用途：多个待播放星星动画之间的固定延迟间隔。
    /// 调用边界：仅在本次胜利存在等待动画星星时使用。
    /// 注意事项：数值小于 0 时按 0 处理，避免反向延迟。
    /// </summary>
    [SerializeField] private float waitingAnimInterval = 0.18f;

    /// <summary>
    /// 用途：单个星星从未点亮切换到点亮时的缩放动画时长。
    /// 调用边界：仅作用于当前胜利面板生命周期内的 UI 表现。
    /// 注意事项：数值小于等于 0 时直接切换状态，不创建 tween。
    /// </summary>
    [SerializeField] private float scaleDuration = 0.22f;

    /// <summary>
    /// 用途：星星点亮缩放反馈的峰值倍率。
    /// 调用边界：仅在播放等待动画时临时修改目标 Toggle 的 Transform 缩放。
    /// 注意事项：动画结束会恢复进入动画前的本地缩放。
    /// </summary>
    [SerializeField] private float scalePunch = 1.22f;

    #endregion

    #region Runtime State

    private readonly List<Tween> runningTweens = new List<Tween>();
    private int cachedWaitingAnimCount;
    private int animationVersion;
    private Action onAnimationComplete;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：按当前关卡明信片星星存档刷新胜利页星星展示，并消费本轮等待动画数量。
    /// 调用边界：仅由胜利面板创建或打开流程在运行时传入当前 LevelData 调用。
    /// 注意事项：该方法只消费 `LevelWaitingAnimStar` 并刷新 UI，不负责授予胜利星；授予逻辑由成功流程在打开胜利面板前完成。
    /// </summary>
    /// <param name="levelData">当前胜利关卡配置。</param>
    /// <param name="animationComplete">本轮全部待播放星星动画结束后的回调，可为空。</param>
    /// <returns>存在待播放星星动画时返回 true；无动画或无法播放时返回 false。</returns>
    public bool Refresh(LevelData levelData, Action animationComplete = null)
    {
        if (!Application.isPlaying)
            return false;

        StopAnimations(false);
        EnsureToggles();
        PrepareStarVisuals();

        if (levelData == null || string.IsNullOrEmpty(levelData.Id))
        {
            ApplyToggleVisibility(GetToggleCount());
            ResetAllToggles();
            cachedWaitingAnimCount = 0;
            return false;
        }

        int visibleToggleCount = GetVisibleToggleCount(levelData);
        ApplyToggleVisibility(visibleToggleCount);
        cachedWaitingAnimCount = ConsumeWaitingAnimCount(levelData.Id, visibleToggleCount);
        List<int> collectedStarIds = GetCollectedStarIds(levelData.Id);
        return ApplyStarState(
            collectedStarIds,
            cachedWaitingAnimCount,
            visibleToggleCount,
            ++animationVersion,
            animationComplete);
    }

    /// <summary>
    /// 用途：按外部传入的最高星数和新增星数展示结算动画，不读取或消费任何存档。
    /// 调用边界：由 Draw DeepLink 胜利面板在消费本关等待记录后调用，复用当前星组的动画与清理流程。
    /// 注意事项：最多展示三个有效 Toggle；没有新增星时只刷新静态状态，返回 false 且不调用完成回调。
    /// </summary>
    /// <param name="collectedStarCount">已获得的最高星数。</param>
    /// <param name="waitingAnimStarCount">本轮需要逐颗播放的新增星数，限制在已获得数量内。</param>
    /// <param name="animationComplete">整组新增星动画结束后的回调，可为空。</param>
    /// <returns>存在待播放星星时返回 true，否则返回 false。</returns>
    public bool RefreshStars(int collectedStarCount, int waitingAnimStarCount, Action animationComplete = null)
    {
        if (!Application.isPlaying)
            return false;

        StopAnimations(false);
        EnsureToggles();
        PrepareStarVisuals();

        int toggleCount = GetToggleCount();
        int normalizedStarCount = Mathf.Clamp(collectedStarCount, 0, toggleCount);
        ApplyToggleVisibility(toggleCount);
        cachedWaitingAnimCount = Mathf.Clamp(waitingAnimStarCount, 0, normalizedStarCount);
        List<int> collectedStarIds = new List<int>(normalizedStarCount);
        for (int i = 1; i <= normalizedStarCount; i++)
            collectedStarIds.Add(i);

        return ApplyStarState(
            collectedStarIds,
            cachedWaitingAnimCount,
            toggleCount,
            ++animationVersion,
            animationComplete);
    }

    /// <summary>
    /// 用途：不读取或消费任何存档，直接以静态状态展示指定数量的星星。
    /// 调用边界：仅供不使用主线明信片星数据的结算界面调用。
    /// 注意事项：数量会限制在当前 prefab 最多三个有效 Toggle 内，且不会播放获得星星动画。
    /// </summary>
    /// <param name="starCount">需要静态点亮的星星数量。</param>
    public void RefreshStaticStars(int starCount)
    {
        if (!Application.isPlaying)
            return;

        StopAnimations(false);
        EnsureToggles();
        PrepareStarVisuals();

        int toggleCount = GetToggleCount();
        int normalizedStarCount = Mathf.Clamp(starCount, 0, toggleCount);
        ApplyToggleVisibility(toggleCount);
        cachedWaitingAnimCount = 0;
        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle != null)
                SetToggleOn(toggle, i < normalizedStarCount);
        }

        ShowEnabledStarBreathObjects();
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

    private bool ApplyStarState(
        List<int> collectedStarIds,
        int waitingAnimCount,
        int visibleToggleCount,
        int currentAnimationVersion,
        Action animationComplete)
    {
        int toggleCount = GetToggleCount();
        int clampedVisibleToggleCount = Mathf.Clamp(visibleToggleCount, 0, toggleCount);
        int collectedCount = GetCollectedStarCount(collectedStarIds, clampedVisibleToggleCount);
        int animCount = Mathf.Clamp(waitingAnimCount, 0, collectedCount);
        int staticCount = Mathf.Max(0, collectedCount - animCount);

        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle == null)
                continue;

            SetToggleOn(toggle, i < clampedVisibleToggleCount && i < staticCount);
        }

        if (animCount <= 0)
        {
            ShowEnabledStarBreathObjects();
            return false;
        }

        onAnimationComplete = animationComplete;
        PlayStarAnimationsAsync(animCount, staticCount, currentAnimationVersion).Forget();
        return true;
    }

    private async UniTask PlayStarAnimationsAsync(int animCount, int staticCount, int currentAnimationVersion)
    {

        try
        {
            await UniTask.WaitForSeconds(
                Mathf.Max(0f, waitingPlayDelay),
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (!IsAnimationVersionActive(currentAnimationVersion))
            return;

        float interval = Mathf.Max(0f, waitingAnimInterval);
        float completeDelay = 0f;
        for (int i = 0; i < animCount; i++)
        {
            int toggleIndex = staticCount + i;
            Toggle toggle = GetToggle(toggleIndex);
            if (toggle == null)
                continue;

            Toggle targetToggle = toggle;
            float delay = interval * i;
            completeDelay = Mathf.Max(completeDelay, ScheduleToggleOnAnimation(targetToggle, delay));
        }

        Tween completeTween = DOVirtual.DelayedCall(
                completeDelay,
                () => CompleteAnimation(currentAnimationVersion))
            .SetUpdate(true);
        runningTweens.Add(completeTween);
    }

    private float ScheduleToggleOnAnimation(Toggle toggle, float startDelay)
    {
        if (toggle == null)
            return 0f;

        Transform toggleTransform = toggle.transform;
        Vector3 originScale = toggleTransform.localScale;
        Animator animator = toggleTransform.GetComponent<Animator>();
        float triggerDelay = Mathf.Max(0f, startDelay) + Mathf.Max(0f, scaleDuration);
        float animatorDuration = GetAnimatorDuration(animator);

        void PlayAnimation()
        {
            if (toggle == null || toggleTransform == null)
                return;

            if (animator != null)
                animator.SetTrigger("play");

            AudioManager.PlaySound("collectedStar");
            toggleTransform.localScale = originScale;
            SetToggleOn(toggle, true);
        }

        if (triggerDelay <= 0f)
        {
            PlayAnimation();
        }
        else
        {
            Tween tween = DOVirtual.DelayedCall(triggerDelay, PlayAnimation)
                .SetUpdate(true);
            runningTweens.Add(tween);
        }

        return triggerDelay + animatorDuration;
    }

    private static float GetAnimatorDuration(Animator animator)
    {
        RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
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

    private void CompleteAnimation(int currentAnimationVersion)
    {
        if (!IsAnimationVersionActive(currentAnimationVersion))
            return;

        Action complete = onAnimationComplete;
        onAnimationComplete = null;
        ShowEnabledStarBreathObjects();
        complete?.Invoke();
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

            bool shouldShowBreath = toggle.isOn;
            breathTransform.gameObject.SetActive(shouldShowBreath);
            if (!shouldShowBreath)
                continue;

            Animator animator = toggle.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = false;
        }
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

    private void ResetAllToggles()
    {
        int toggleCount = GetToggleCount();
        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle != null)
                SetToggleOn(toggle, false);
        }
    }

    private static void SetToggleOn(Toggle toggle, bool isOn)
    {
        toggle.SetIsOnWithoutNotify(isOn);
    }

    #endregion

    #region Data Helpers

    private int ConsumeWaitingAnimCount(string levelId, int visibleToggleCount)
    {
        PostcardManager postcardManager = GameManager.Instance?.Postcard;
        if (postcardManager == null)
            return 0;

        return Mathf.Clamp(postcardManager.ConsumeCollectedStarCount(levelId), 0, Mathf.Max(0, visibleToggleCount));
    }

    private List<int> GetCollectedStarIds(string levelId)
    {
        List<int> collectedStarIds = new List<int>();
        PostcardManager postcardManager = GameManager.Instance?.Postcard;
        if (postcardManager == null)
            return collectedStarIds;

        List<int> collectedStars = postcardManager.GetLevelCollectedStarIds(levelId);

        HashSet<int> addedStarIds = new HashSet<int>();
        for (int i = 0; i < collectedStars.Count; i++)
        {
            int starId = collectedStars[i];
            if (IsValidStarId(starId) && addedStarIds.Add(starId))
                collectedStarIds.Add(starId);
        }

        return collectedStarIds;
    }

    #endregion

    #region Star Id Helpers

    private static int GetCollectedStarCount(List<int> collectedStarIds, int toggleCount)
    {
        if (collectedStarIds == null || toggleCount <= 0)
            return 0;

        HashSet<int> addedStarIds = new HashSet<int>();
        for (int i = 0; i < collectedStarIds.Count; i++)
        {
            int starId = collectedStarIds[i];
            if (IsValidStarId(starId))
                addedStarIds.Add(starId);
        }

        return Mathf.Min(addedStarIds.Count, toggleCount);
    }

    private static bool IsValidStarId(int starId)
    {
        return starId >= 1 && starId <= 3;
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
        for (int i = 0; i < childToggles.Length && i < 3; i++)
            starToggles.Add(childToggles[i]);
    }

    private int GetToggleCount()
    {
        return starToggles == null ? 0 : Mathf.Min(3, starToggles.Count);
    }

    private int GetVisibleToggleCount(LevelData levelData)
    {
        if (levelData == null)
            return GetToggleCount();

        return Mathf.Clamp(levelData.LevelSumStarCount, 0, GetToggleCount());
    }

    private void ApplyToggleVisibility(int visibleToggleCount)
    {
        int toggleCount = GetToggleCount();
        int clampedVisibleToggleCount = Mathf.Clamp(visibleToggleCount, 0, toggleCount);
        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle == null)
                continue;

            bool isVisible = i < clampedVisibleToggleCount;
            toggle.gameObject.SetActive(isVisible);
            if (!isVisible)
                SetToggleOn(toggle, false);
        }
    }

    private Toggle GetToggle(int index)
    {
        if (starToggles == null || index < 0 || index >= GetToggleCount())
            return null;

        return starToggles[index];
    }

    private bool IsAnimationVersionActive(int currentAnimationVersion)
    {
        return currentAnimationVersion == animationVersion && isActiveAndEnabled;
    }

    #endregion

    #region Animation Helpers

    private void StopAnimations(bool notifyAnimationComplete)
    {
        for (int i = 0; i < runningTweens.Count; i++)
            runningTweens[i]?.Kill();

        runningTweens.Clear();
        animationVersion++;

        Action complete = onAnimationComplete;
        onAnimationComplete = null;
        if (notifyAnimationComplete)
            complete?.Invoke();
    }

    #endregion


    #region Test

    [SerializeField] private int testCount = 1;
    [Button("Test")]
    private void Test()
    {
        ResetAllToggles();
        float interval = Mathf.Max(0f, waitingAnimInterval);

        for (int i = 0; i < testCount; i++)
        {
            int toggleIndex = i;
            Toggle toggle = GetToggle(toggleIndex);
            if (toggle == null)
                continue;

            ScheduleToggleOnAnimation(toggle, interval * i);
        }
    }

    #endregion
}
