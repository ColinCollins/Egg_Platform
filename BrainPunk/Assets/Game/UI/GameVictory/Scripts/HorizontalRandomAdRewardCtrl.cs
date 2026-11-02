using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Reward selector for the victory panel.
/// The arrow swings left and right around a configurable center point.
/// Play keeps a constant loop speed, and Stop settles to the current reward band.
/// </summary>
[DisallowMultipleComponent]
public class HorizontalRandomAdRewardCtrl : MonoBehaviour, IDebuger
{
    /// <summary>Default reward multiplier values used in editor preview or as fallback.</summary>
    private static readonly int[] DefaultPreviewRewardValues = { 1, 2, 3, 4 };

    [Header("Refs")]
    [SerializeField] private RectTransform arrow;

    [Header("Swing")]
    [Tooltip("Swing center offset relative to the arrow rest position.")]
    [SerializeField] private Vector2 swingCenterOffset = new Vector2(0f, -220f);
    /// <summary>Loop speed in cycles per second while Play is running.</summary>
    private const float LoopFrequency = 0.28f;
    [SerializeField] private bool ignoreTimeScale = true;

    [Header("Visual")]
    [Tooltip("Max swing angle around the swing center, in degrees.")]
    [SerializeField] private float rotateAngle = 18f;
    [SerializeField] private bool resetToCenterOnEnable = true;
    [SerializeField] private float motionSmoothness = 14f;
    [SerializeField] private float stopDuration = 0.6f;
    [SerializeField] private AnimationCurve settleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Reward")]
    [SerializeField] private TextMeshProUGUI[] rewardTxts;
    /// <summary>
    /// 用途：配置指针从中心向外移动时，各奖励档位之间的分界角度。
    /// 调用边界：数组长度应为奖励档位数减一，并按角度从小到大填写。
    /// 注意事项：角度会限制在摆动范围内；配置无效时运行时和 Gizmos 都回退为等分档位。
    /// </summary>
    [Tooltip("从中心向外的奖励分界角度（度）。数量应为奖励档位数减一，并按升序填写。")]
    [SerializeField] private float[] rewardBandBoundaryAngles = { 1.5f, 4.5f, 8f };
    [SerializeField] private float rewardTextFadeDelay = 2f;
    [SerializeField] private float rewardTextFadeDuration = 0.35f;

    // Rest state cache for the arrow transform.
    private Vector2 _restAnchoredPosition;
    private Vector3 _restEulerAngles;
    private bool _hasRestState;

    // Play loop versioning and state.
    private int _playVersion;
    private bool _isPlaying;

    // Normalized position in range [-1, 1] representing the swing phase.
    private float _currentNormalized;
    private float _displayNormalized;

    // Current reward tier derived from the arrow position.
    private int _currentRewardTier;

    // Fade animation versioning to allow cancellation.
    private int _rewardTextFadeVersion;

    // Stop request state for smooth settling.
    private bool _stopRequested;
    private float _stopTargetNormalized;
    // Lets callers preserve the current pointer phase across a temporary disable/enable cycle.
    private bool _skipResetOnNextEnable;

    // Completion source used to let callers await the actual end of a stop animation.
    private UniTaskCompletionSource _stopTcs;

    /// <summary>Whether the swing loop is currently active.</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>Current reward tier index (0-based).</summary>
    public int CurrentRewardTier => _currentRewardTier;

    /// <summary>Current reward level (1-based).</summary>
    public int CurrentRewardLevel => _currentRewardTier + 1;

    private void Awake()
    {
        if (!TryResolveArrow())
            return;

        TryResolveRewardTexts();
        CacheRestState();
        RefreshRewardTexts();
        UpdateRewardState();
    }

    private void OnEnable()
    {
        this.Log("OnEnable Reward Text Fade");
        RefreshRewardTexts();
        RestartRewardTextFade();

        bool shouldResetToCenter = resetToCenterOnEnable && !_skipResetOnNextEnable;
        _skipResetOnNextEnable = false;
        if (shouldResetToCenter)
            ResetArrow();
    }

    /// <summary>
    /// Lifecycle cleanup: performed synchronously because async await is unsafe inside
    /// <see cref="OnDisable"/>. All pending stop waiters are released immediately.
    /// </summary>
    private void OnDisable()
    {
        StopRewardTextFade();
        AbortPlayLoop(bumpVersion: true);
    }

    /// <summary>
    /// Lifecycle cleanup: performed synchronously because the object is being destroyed.
    /// All pending stop waiters are released immediately.
    /// </summary>
    private void OnDestroy()
    {
        StopRewardTextFade();
        AbortPlayLoop(bumpVersion: true);
    }

    private void Reset()
    {
        TryResolveArrow();
        TryResolveRewardTexts();
        CacheRestState();
        RefreshRewardTexts();
    }

    private void OnValidate()
    {
        // Clamp serialized fields to valid ranges.
        rotateAngle = Mathf.Max(0f, rotateAngle);
        motionSmoothness = Mathf.Max(0f, motionSmoothness);
        stopDuration = Mathf.Max(0f, stopDuration);
        rewardTextFadeDelay = Mathf.Max(0f, rewardTextFadeDelay);
        rewardTextFadeDuration = Mathf.Max(0f, rewardTextFadeDuration);
        NormalizeRewardBandBoundaryAngles();

        TryResolveRewardTexts();
        RefreshRewardTexts();
        SetRewardTextAlpha(1f);
    }

    /// <summary>
    /// Starts the continuous left-right swing loop.
    /// The loop runs until <see cref="Stop"/> is called.
    /// </summary>
    public async UniTask Play()
    {
        if (!TryResolveArrow())
        {
            _stopTcs?.TrySetResult();
            return;
        }

        CacheRestState();

        int version = ++_playVersion;
        _isPlaying = true;
        _stopRequested = false;
        _stopTargetNormalized = 0f;

        // Derive initial phase from current normalized position so motion stays continuous.
        float phase = Mathf.Asin(Mathf.Clamp(_currentNormalized, -1f, 1f));

        while (!_stopRequested && version == _playVersion && this != null && isActiveAndEnabled)
        {
            float deltaTime = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime <= 0f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                continue;
            }

            phase += deltaTime * LoopFrequency * Mathf.PI * 2f;

            ApplyNormalized(Mathf.Sin(phase));
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        // If the loop exited for any reason other than an intentional stop request,
        // release any pending waiters and bail out.
        if (version != _playVersion || this == null || !_stopRequested)
        {
            ResetPlayState();
            ReleaseStopWaiters();
            return;
        }

        // Animate the arrow settling onto the target reward band.
        if (!await TryAnimateStopToRewardBand(version, _stopTargetNormalized))
        {
            ResetPlayState();
            ReleaseStopWaiters();
            return;
        }

        ResetPlayState();
        ReleaseStopWaiters();
    }

    /// <summary>
    /// Requests the swing to stop.
    /// When <paramref name="snapToRewardBand"/> is <c>true</c> (default), the method
    /// awaits until the arrow has fully settled at the center of the current reward band.
    /// When <paramref name="snapToRewardBand"/> is <c>false</c>, the loop is aborted
    /// immediately and the task returns at once.
    /// </summary>
    /// <param name="snapToRewardBand">Whether to smoothly settle to the reward band center.</param>
    public async UniTask Stop(bool snapToRewardBand = true)
    {
        if (!_isPlaying)
        {
            if (snapToRewardBand)
                SnapToCurrentRewardBand();
            return;
        }

        // Hard abort: kill the loop immediately without settling.
        if (!snapToRewardBand)
        {
            AbortPlayLoop(bumpVersion: true);
            return;
        }

        // If a stop is already in flight, just wait for the existing one to finish.
        if (_stopRequested)
        {
            if (_stopTcs != null)
                await _stopTcs.Task;
            return;
        }

        // Initiate a graceful stop and create a new completion source for callers to await.
        _stopRequested = true;
        _stopTargetNormalized = GetBandCenterNormalized(_displayNormalized);
        _stopTcs = new UniTaskCompletionSource();
        await _stopTcs.Task;
    }

    /// <summary>Resets the arrow to the center (normalized = 0) immediately.</summary>
    public void ResetArrow()
    {
        if (!TryResolveArrow())
            return;

        CacheRestState();
        ApplyNormalized(0f, true);
    }

    /// <summary>
    /// Preserves the current arrow position through the next disable/enable cycle so
    /// callers can restart <see cref="Play"/> from the current swing state instead of resetting.
    /// </summary>
    public void PreserveCurrentStateOnNextEnable()
    {
        _skipResetOnNextEnable = true;
    }

    /// <summary>
    /// Clears all runtime play-state flags without altering the current visual position.
    /// </summary>
    private void ResetPlayState()
    {
        _isPlaying = false;
        _stopRequested = false;
    }

    /// <summary>
    /// Completes and releases any outstanding stop waiter so callers do not hang on a dead loop.
    /// </summary>
    private void ReleaseStopWaiters()
    {
        _stopTcs?.TrySetResult();
        _stopTcs = null;
    }

    /// <summary>
    /// Synchronously terminates the current play loop.
    /// Used by lifecycle teardown and hard-stop paths that cannot await animation completion.
    /// </summary>
    private void AbortPlayLoop(bool bumpVersion)
    {
        if (bumpVersion)
            _playVersion++;

        ResetPlayState();
        ReleaseStopWaiters();
    }

    /// <summary>Returns the final reward amount using a base reward of 1.</summary>
    public int GetReward()
    {
        return GetReward(1);
    }

    /// <summary>
    /// Calculates the final reward amount based on the current tier multiplier
    /// and the provided <paramref name="baseReward"/>.
    /// </summary>
    public int GetReward(int baseReward)
    {
        return Mathf.Max(0, baseReward) * GetRewardMultiplier();
    }

    /// <summary>Returns the multiplier value of the current reward tier.</summary>
    public int GetRewardMultiplier()
    {
        int[] rewardValues = GetRewardValues();
        if (rewardValues.Length == 0)
            return 1;

        int tier = Mathf.Clamp(_currentRewardTier, 0, rewardValues.Length - 1);
        return Mathf.Max(0, rewardValues[tier]);
    }

    /// <summary>Alias for <see cref="GetRewardMultiplier"/>.</summary>
    public int GetCurrentReward()
    {
        return GetRewardMultiplier();
    }

    /// <summary>Alias for <see cref="CurrentRewardLevel"/>.</summary>
    public int GetRewardLevel()
    {
        return CurrentRewardLevel;
    }

    /// <summary>Returns the current arrow position in normalized range [-1, 1].</summary>
    public float GetCurrentNormalizedPosition()
    {
        return _displayNormalized;
    }

    /// <summary>Returns the live pointer transform used by the selector.</summary>
    public Transform GetPointerTransform()
    {
        return TryResolveArrow() ? arrow : null;
    }

    [FoldoutGroup("Test")]
    [Button("Play Presentation Test")]
    private void PlayPresentationTest()
    {
        if (!Application.isPlaying)
        {
            this.LogWarning("[HorizontalRandomAdRewardCtrl] Presentation test is only available in play mode.");
            return;
        }

        Stop(false).Forget();
        ResetArrow();
        Play().Forget();
    }

    /// <summary>
    /// Attempts to locate the arrow RectTransform by name ("arrow")
    /// among children if the serialized reference is missing.
    /// </summary>
    private bool TryResolveArrow()
    {
        if (arrow != null)
            return true;

        Transform arrowTransform = transform.Find("arrow");
        if (arrowTransform == null)
        {
            foreach (RectTransform rect in GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == null || rect == transform)
                    continue;

                if (string.Equals(rect.name, "arrow", System.StringComparison.OrdinalIgnoreCase))
                {
                    arrowTransform = rect;
                    break;
                }
            }
        }

        arrow = arrowTransform as RectTransform;
        if (arrow == null)
        {
            this.LogWarning("Arrow not found.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Attempts to auto-bind reward text components under the arrow or self,
    /// sorted by horizontal anchored position (left to right).
    /// </summary>
    private void TryResolveRewardTexts()
    {
        if (HasValidRewardTextBindings())
            return;

        Transform root = arrow != null ? arrow : transform;
        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null || texts.Length == 0)
            return;

        List<TextMeshProUGUI> resolvedTexts = new List<TextMeshProUGUI>(texts.Length);
        foreach (TextMeshProUGUI text in texts)
        {
            if (text == null)
                continue;

            resolvedTexts.Add(text);
        }

        resolvedTexts.Sort((left, right) =>
        {
            float leftX = left.rectTransform.anchoredPosition.x;
            float rightX = right.rectTransform.anchoredPosition.x;
            return leftX.CompareTo(rightX);
        });

        rewardTxts = resolvedTexts.ToArray();
    }

    /// <summary>Checks whether all bound reward text references are non-null.</summary>
    private bool HasValidRewardTextBindings()
    {
        if (rewardTxts == null || rewardTxts.Length == 0)
            return false;

        for (int i = 0; i < rewardTxts.Length; i++)
        {
            if (rewardTxts[i] == null)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Caches the arrow's initial anchored position and rotation as the rest state.
    /// This state is used to compute the swing orbit center.
    /// </summary>
    private void CacheRestState()
    {
        if (arrow == null || _hasRestState)
            return;

        _restAnchoredPosition = arrow.anchoredPosition;
        _restEulerAngles = arrow.localEulerAngles;
        _hasRestState = true;
    }

    /// <summary>
    /// Applies a new normalized swing value [-1, 1].
    /// When <paramref name="instant"/> is false, smoothing is applied for visual interpolation.
    /// </summary>
    private void ApplyNormalized(float normalized, bool instant = false)
    {
        if (arrow == null)
            return;

        _currentNormalized = Mathf.Clamp(normalized, -1f, 1f);

        float visualNormalized = _currentNormalized;
        if (!instant)
        {
            float deltaTime = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            if (Application.isPlaying && deltaTime > 0f && motionSmoothness > 0f)
            {
                // Exponential decay smoothing for organic motion feel.
                float lerpFactor = 1f - Mathf.Exp(-motionSmoothness * deltaTime);
                visualNormalized = Mathf.Lerp(_displayNormalized, _currentNormalized, lerpFactor);
            }
        }

        SetArrowVisual(visualNormalized);
        UpdateRewardState();
    }

    /// <summary>
    /// Updates the arrow's anchored position and Z rotation based on the swing orbit.
    /// The arrow moves along an arc centered at <see cref="GetSwingCenterAnchoredPosition"/>.
    /// </summary>
    private void SetArrowVisual(float normalized)
    {
        _displayNormalized = Mathf.Clamp(normalized, -1f, 1f);

        float orbitAngle = _displayNormalized * rotateAngle;
        Vector2 orbitCenter = GetSwingCenterAnchoredPosition(_restAnchoredPosition);
        float orbitRadius = GetSwingRadius();
        arrow.anchoredPosition = orbitCenter + GetOrbitOffset(orbitAngle, orbitRadius);

        Vector3 eulerAngles = _restEulerAngles;
        eulerAngles.z += -orbitAngle;
        arrow.localEulerAngles = eulerAngles;
    }

    /// <summary>Computes the swing orbit center by offsetting the rest position.</summary>
    private Vector2 GetSwingCenterAnchoredPosition(Vector2 restAnchoredPosition)
    {
        return restAnchoredPosition + ResolveSwingCenterOffset();
    }

    /// <summary>Returns the configured swing center offset.</summary>
    private Vector2 ResolveSwingCenterOffset()
    {
        return swingCenterOffset;
    }

    /// <summary>Returns the radius of the swing arc, clamped to a minimum of 1.</summary>
    private float GetSwingRadius()
    {
        return Mathf.Max(ResolveSwingCenterOffset().magnitude, 1f);
    }

    /// <summary>
    /// Calculates the offset on the swing arc for a given angle (in degrees) and radius.
    /// Angle 0 points upward (positive Y).
    /// </summary>
    private static Vector2 GetOrbitOffset(float angleDeg, float radius)
    {
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad)) * radius;
    }

    /// <summary>Updates <see cref="_currentRewardTier"/> based on the current normalized position.</summary>
    private void UpdateRewardState()
    {
        _currentRewardTier = CalculateRewardTier(_displayNormalized);
    }

    /// <summary>Refreshes the text of each reward label according to current configuration values.</summary>
    private void RefreshRewardTexts()
    {
        TryResolveRewardTexts();
        if (rewardTxts == null || rewardTxts.Length == 0)
            return;

        int[] displayValues = GetRewardValues();
        if (displayValues.Length == 0)
            return;

        for (int i = 0; i < rewardTxts.Length; i++)
        {
            TextMeshProUGUI rewardText = rewardTxts[i];
            if (rewardText == null)
                continue;

            rewardText.text = GetDisplayValueAt(i, rewardTxts.Length, displayValues).ToString();
        }
    }

    /// <summary>
    /// Restarts the fade-in animation for reward texts.
    /// In editor (non-play mode), texts are forced to fully opaque.
    /// </summary>
    private void RestartRewardTextFade()
    {
        if (!Application.isPlaying)
        {
            SetRewardTextAlpha(1f);
            return;
        }

        StopRewardTextFade();
        SetRewardTextAlpha(0f);
        PlayRewardTextFadeInAsync().Forget();
    }

    /// <summary>Cancels any running reward text fade by incrementing the version token.</summary>
    private void StopRewardTextFade()
    {
        _rewardTextFadeVersion++;
    }

    /// <summary>
    /// Async fade-in sequence: wait for <see cref="rewardTextFadeDelay"/>,
    /// then interpolate alpha from 0 to 1 over <see cref="rewardTextFadeDuration"/>.
    /// </summary>
    private async UniTask PlayRewardTextFadeInAsync()
    {
        TryResolveRewardTexts();
        if (rewardTxts == null || rewardTxts.Length == 0)
            return;

        int version = ++_rewardTextFadeVersion;

        // Delay phase.
        float delayElapsed = 0f;
        while (delayElapsed < rewardTextFadeDelay && version == _rewardTextFadeVersion && this != null && isActiveAndEnabled)
        {
            delayElapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (version != _rewardTextFadeVersion || this == null || !isActiveAndEnabled)
            return;

        if (rewardTextFadeDuration <= 0f)
        {
            SetRewardTextAlpha(1f);
            return;
        }

        // Fade phase.
        float fadeElapsed = 0f;
        while (fadeElapsed < rewardTextFadeDuration && version == _rewardTextFadeVersion && this != null && isActiveAndEnabled)
        {
            fadeElapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            SetRewardTextAlpha(Mathf.Clamp01(fadeElapsed / rewardTextFadeDuration));
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (version == _rewardTextFadeVersion && this != null)
            SetRewardTextAlpha(1f);
    }

    /// <summary>Sets the alpha of all bound reward texts uniformly.</summary>
    private void SetRewardTextAlpha(float alpha)
    {
        TryResolveRewardTexts();
        if (rewardTxts == null || rewardTxts.Length == 0)
            return;

        alpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < rewardTxts.Length; i++)
        {
            TextMeshProUGUI rewardText = rewardTxts[i];
            if (rewardText == null)
                continue;

            Color color = rewardText.color;
            color.a = alpha;
            rewardText.color = color;
        }
    }

    /// <summary>
    /// Retrieves the reward multiplier values.
    /// In play mode, uses RemoteConfig; falls back to defaults if unavailable.
    /// </summary>
    private int[] GetRewardValues()
    {
        if (!Application.isPlaying)
            return GetEditorPreviewRewardValues();

        int[] runtimeValues = ConfigManager.RemoteConfig.GetVictoryHorizantalRewards();
        return runtimeValues != null && runtimeValues.Length > 0
            ? runtimeValues
            : GetEditorPreviewRewardValues();
    }

    /// <summary>Returns the default preview reward values used in editor or as runtime fallback.</summary>
    private int[] GetEditorPreviewRewardValues()
    {
        return DefaultPreviewRewardValues;
    }

    /// <summary>
    /// Maps a UI text index to a display value from the reward array.
    /// Supports 1:1 mapping, mirrored symmetric layout (2N-1), and proportional mapping.
    /// </summary>
    private int GetDisplayValueAt(int textIndex, int textCount, int[] displayValues)
    {
        if (displayValues == null || displayValues.Length == 0)
            return 0;

        // Direct one-to-one mapping.
        if (textCount == displayValues.Length)
        {
            int directIndex = Mathf.Clamp(textIndex, 0, displayValues.Length - 1);
            return displayValues[directIndex];
        }

        // Mirrored symmetric layout: texts are arranged as [small ... center ... small].
        if (textCount == displayValues.Length * 2 - 1)
        {
            int mirrorIndex = textIndex <= textCount / 2 ? textIndex : textCount - 1 - textIndex;
            mirrorIndex = Mathf.Clamp(mirrorIndex, 0, displayValues.Length - 1);
            return displayValues[mirrorIndex];
        }

        if (textCount <= 1)
            return displayValues[displayValues.Length - 1];

        // Proportional mapping based on normalized horizontal position.
        float normalizedIndex = (float)textIndex / (textCount - 1);
        int mappedIndex = Mathf.RoundToInt(normalizedIndex * (displayValues.Length - 1));
        mappedIndex = Mathf.Clamp(mappedIndex, 0, displayValues.Length - 1);
        return displayValues[mappedIndex];
    }

    /// <summary>
    /// Calculates which reward tier the arrow currently points to.
    /// Custom boundary angles are measured from the center; center is the highest tier.
    /// </summary>
    private int CalculateRewardTier(float normalized)
    {
        int tierCount = GetTierCount();
        if (tierCount <= 1)
            return 0;

        if (rotateAngle <= Mathf.Epsilon)
            return tierCount - 1;

        float absoluteAngle = Mathf.Abs(Mathf.Clamp(normalized, -1f, 1f)) * rotateAngle;
        int bandFromCenter = 0;
        while (bandFromCenter < tierCount - 1
               && absoluteAngle >= GetRewardBandBoundaryAngle(bandFromCenter, tierCount))
        {
            bandFromCenter++;
        }

        // Reverse so that center (lowest band index) maps to the highest tier.
        return tierCount - 1 - bandFromCenter;
    }

    /// <summary>Snap the arrow instantly to the center of the current reward band.</summary>
    private void SnapToCurrentRewardBand()
    {
        if (arrow == null)
            return;

        ApplyNormalized(GetBandCenterNormalized(_displayNormalized), true);
    }

    /// <summary>
    /// Smoothly animates the arrow from its current position to the target band center
    /// over <see cref="stopDuration"/> using <see cref="settleCurve"/>.
    /// </summary>
    private async UniTask<bool> TryAnimateStopToRewardBand(int version, float targetNormalized)
    {
        if (arrow == null)
            return false;

        if (stopDuration <= 0f || !Application.isPlaying)
        {
            ApplyNormalized(targetNormalized, true);
            return version == _playVersion && this != null;
        }

        float startNormalized = _displayNormalized;
        float elapsed = 0f;
        while (elapsed < stopDuration && version == _playVersion && this != null && isActiveAndEnabled)
        {
            float deltaTime = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime <= 0f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                continue;
            }

            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / stopDuration);
            float easedT = settleCurve == null ? t : settleCurve.Evaluate(t);
            ApplyNormalized(Mathf.Lerp(startNormalized, targetNormalized, easedT), true);
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (version != _playVersion || this == null)
            return false;

        ApplyNormalized(targetNormalized, true);
        return true;
    }

    /// <summary>
    /// Computes the normalized position that corresponds to the center of the reward band
    /// containing the given normalized value.
    /// </summary>
    private float GetBandCenterNormalized(float normalized)
    {
        int tierCount = GetTierCount();
        if (tierCount <= 1)
            return 0f;

        int tier = CalculateRewardTier(normalized);
        int bandFromCenter = tierCount - 1 - tier;
        if (bandFromCenter == 0)
            return 0f;

        if (rotateAngle <= Mathf.Epsilon)
            return 0f;

        float minAngle = GetRewardBandBoundaryAngle(bandFromCenter - 1, tierCount);
        float maxAngle = bandFromCenter < tierCount - 1
            ? GetRewardBandBoundaryAngle(bandFromCenter, tierCount)
            : rotateAngle;
        float centerNormalized = ((minAngle + maxAngle) * 0.5f) / rotateAngle;
        return Mathf.Sign(normalized) * Mathf.Clamp01(centerNormalized);
    }

    /// <summary>Returns one center-out reward boundary angle, using equal bands as invalid-config fallback.</summary>
    private float GetRewardBandBoundaryAngle(int boundaryIndex, int tierCount)
    {
        boundaryIndex = Mathf.Clamp(boundaryIndex, 0, Mathf.Max(0, tierCount - 2));
        if (HasValidRewardBandBoundaryAngles(tierCount))
            return rewardBandBoundaryAngles[boundaryIndex];

        return rotateAngle * (boundaryIndex + 1f) / tierCount;
    }

    /// <summary>Checks whether the exposed boundary angles can represent every current reward tier.</summary>
    private bool HasValidRewardBandBoundaryAngles(int tierCount)
    {
        if (tierCount <= 1)
            return true;

        if (rewardBandBoundaryAngles == null || rewardBandBoundaryAngles.Length != tierCount - 1)
            return false;

        float previousAngle = 0f;
        for (int i = 0; i < rewardBandBoundaryAngles.Length; i++)
        {
            float angle = rewardBandBoundaryAngles[i];
            if (angle <= previousAngle || angle >= rotateAngle)
                return false;

            previousAngle = angle;
        }

        return true;
    }

    /// <summary>Clamps and sorts local Inspector boundary data without accessing runtime config or DB.</summary>
    private void NormalizeRewardBandBoundaryAngles()
    {
        if (rewardBandBoundaryAngles == null || rewardBandBoundaryAngles.Length == 0)
            return;

        for (int i = 0; i < rewardBandBoundaryAngles.Length; i++)
            rewardBandBoundaryAngles[i] = Mathf.Clamp(rewardBandBoundaryAngles[i], 0f, rotateAngle);

        System.Array.Sort(rewardBandBoundaryAngles);
    }

    /// <summary>Returns the number of reward tiers based on configured reward values.</summary>
    private int GetTierCount()
    {
        return GetRewardValues().Length;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor gizmo visualization of the swing arc, orbit center, and reward band markers.
    /// Only drawn when the GameObject is selected.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        RectTransform targetArrow = arrow;
        if (targetArrow == null)
        {
            Transform arrowTransform = transform.Find("arrow");
            targetArrow = arrowTransform as RectTransform;
        }

        if (targetArrow == null)
            return;

        Vector2 previewRestAnchored = Application.isPlaying && _hasRestState
            ? _restAnchoredPosition
            : targetArrow.anchoredPosition;

        Vector2 previewCenterAnchored = GetSwingCenterAnchoredPosition(previewRestAnchored);
        float previewRadius = GetSwingRadius();

        Transform parent = targetArrow.parent;
        Vector3 center = parent != null
            ? parent.TransformPoint(new Vector3(previewCenterAnchored.x, previewCenterAnchored.y, 0f))
            : targetArrow.position;
        int tierCount = Mathf.Max(1, GetTierCount());
        if (tierCount > 1 && !HasValidRewardBandBoundaryAngles(tierCount))
            UnityEditor.Handles.Label(center, "Invalid reward boundaries: using equal-band fallback");

        // Draw the swing arc with the same reward-tier calculation used at runtime.
        const int segmentCount = 64;
        Vector3 previousPoint = GetGizmoPoint(parent, previewCenterAnchored, -rotateAngle, previewRadius);
        for (int i = 1; i <= segmentCount; i++)
        {
            float previousT = (i - 1) / (float)segmentCount;
            float t = i / (float)segmentCount;
            float angle = Mathf.Lerp(-rotateAngle, rotateAngle, t);
            float segmentMiddleAngle = Mathf.Lerp(-rotateAngle, rotateAngle, (previousT + t) * 0.5f);
            int segmentTier = CalculateRewardTier(
                rotateAngle <= Mathf.Epsilon ? 0f : segmentMiddleAngle / rotateAngle);
            Gizmos.color = GetRewardTierGizmoColor(segmentTier, tierCount);
            Vector3 currentPoint = GetGizmoPoint(parent, previewCenterAnchored, angle, previewRadius);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        // Draw orbit center.
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(center, 8f);

        // Draw exact reward boundaries from the orbit center to both sides of the arc.
        Gizmos.color = new Color(1f, 0.45f, 0.05f, 1f);
        for (int boundaryIndex = 0; boundaryIndex < tierCount - 1; boundaryIndex++)
        {
            float boundaryAngle = GetRewardBandBoundaryAngle(boundaryIndex, tierCount);
            Vector3 rightBoundary = GetGizmoPoint(
                parent,
                previewCenterAnchored,
                boundaryAngle,
                previewRadius);
            Vector3 leftBoundary = GetGizmoPoint(
                parent,
                previewCenterAnchored,
                -boundaryAngle,
                previewRadius);
            Gizmos.DrawLine(center, rightBoundary);
            Gizmos.DrawLine(center, leftBoundary);
            Gizmos.DrawWireSphere(rightBoundary, 4f);
            Gizmos.DrawWireSphere(leftBoundary, 4f);
            UnityEditor.Handles.Label(rightBoundary, $"{boundaryAngle:0.##}°");
        }

        // Draw reward band center markers along the arc.
        for (int tier = 0; tier < tierCount; tier++)
        {
            float bandCenterNormalized;
            if (tier == tierCount - 1)
            {
                bandCenterNormalized = 0f;
            }
            else
            {
                int bandFromCenter = tierCount - 1 - tier;
                float minAngle = GetRewardBandBoundaryAngle(bandFromCenter - 1, tierCount);
                float maxAngle = bandFromCenter < tierCount - 1
                    ? GetRewardBandBoundaryAngle(bandFromCenter, tierCount)
                    : rotateAngle;
                bandCenterNormalized = rotateAngle <= Mathf.Epsilon
                    ? 0f
                    : ((minAngle + maxAngle) * 0.5f) / rotateAngle;
            }

            Gizmos.color = GetRewardTierGizmoColor(tier, tierCount);
            Vector3 rightMarker = GetGizmoPoint(parent, previewCenterAnchored, bandCenterNormalized * rotateAngle, previewRadius);
            Gizmos.DrawWireSphere(rightMarker, 6f);
            UnityEditor.Handles.Label(rightMarker, $"T{tier + 1}");

            if (bandCenterNormalized > 0f)
            {
                Vector3 leftMarker = GetGizmoPoint(parent, previewCenterAnchored, -bandCenterNormalized * rotateAngle, previewRadius);
                Gizmos.DrawWireSphere(leftMarker, 6f);
            }
        }
    }

    /// <summary>Returns a low-to-high reward color for Scene-view band visualization.</summary>
    private Color GetRewardTierGizmoColor(int tier, int tierCount)
    {
        float t = tierCount <= 1 ? 1f : Mathf.Clamp01(tier / (float)(tierCount - 1));
        return Color.Lerp(new Color(1f, 0.2f, 0.15f, 1f), new Color(0.15f, 1f, 0.35f, 1f), t);
    }

    /// <summary>Helper to convert anchored orbit coordinates into world-space gizmo points.</summary>
    private Vector3 GetGizmoPoint(Transform parent, Vector2 orbitCenterAnchored, float angle, float radius)
    {
        Vector2 anchoredPosition = orbitCenterAnchored + GetOrbitOffset(angle, radius);
        return parent != null
            ? parent.TransformPoint(new Vector3(anchoredPosition.x, anchoredPosition.y, 0f))
            : transform.position + new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
    }
#endif
}
