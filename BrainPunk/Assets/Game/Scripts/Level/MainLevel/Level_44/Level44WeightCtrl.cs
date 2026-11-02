using Bear.Logger;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Level 44 weight-scale controller.
/// Maps weight to anchor rotation, and shows/hides the process transform.
/// </summary>
public class Level44WeightCtrl : MonoBehaviour, IDebuger
{
    [Header("References")]
    [SerializeField] private Transform anchor;
    [SerializeField] private Transform process;

    [Header("Angle")]
    [SerializeField] private float minAngle = 0f;
    [SerializeField] private float maxAngle = 55f;

    [Header("Weight")]
    [SerializeField] private float limitWeight = 80f;
    [SerializeField] private float maxWeight = 100f;

    [Header("Process")]
    [SerializeField] private float processScaleSpeed = 8f;

    [Header("Animation")]
    [SerializeField] private float rotateDuration = 0.25f;
    [SerializeField] private Ease rotateEase = Ease.OutQuad;

    [Header("Debug")]
    [SerializeField] private bool enableApiLog;

    private Tweener _rotateTweener;
    private float _currentWeight;
    private float _currentAngle;
    private float _currentProcessScale;
    private float _targetProcessScale;
    private Vector3 _processBaseScale = Vector3.one;

    public float MaxWeight => maxWeight;
    public float CurrentWeight => _currentWeight;
    public float CurrentAngle => _currentAngle;
    public float CurrentProcessScale => _currentProcessScale;

    /// <summary>
    /// Normalized weight in [0, 1], using 0 as empty and MaxWeight as full.
    /// </summary>
    public float WeightPercent => maxWeight <= 0f ? 0f : Mathf.Clamp01(_currentWeight / maxWeight);

    /// <summary>
    /// True when CurrentWeight reaches or exceeds LimitWeight.
    /// </summary>
    public bool IsOverWeight => _currentWeight >= limitWeight;

    private void Awake()
    {
        if (anchor == null)
            anchor = transform;

        CacheProcessBaseScale();
        SetProcessScaleImmediate(0f);
    }

    private void Start()
    {
        SetWeight(0f, false);
    }

    private void Update()
    {
        UpdateProcessScale();
    }

    private void OnDestroy()
    {
        _rotateTweener?.Kill();
    }

    #region Public API

    public void PlayEnterWeight(float weight)
    {
        SetProcessScaleTarget(1f);
        SetWeight(weight, true);
    }

    public void PlayExitWeight(float weight)
    {
        SetProcessScaleTarget(0f);
        SetWeight(weight, true);
    }

    public void SetWeight(float weight)
    {
        SetWeight(weight, true);
    }

    /// <summary>
    /// Sets the current weight and rotates the anchor. Weight is clamped to [0, MaxWeight].
    /// </summary>
    public void SetWeight(float weight, bool animate)
    {
        _currentWeight = Mathf.Clamp(weight, 0f, Mathf.Max(0f, maxWeight));
        SetAnchorAngle(GetAngleByWeight(_currentWeight), animate);
        LogApi($"{nameof(SetWeight)} weight={_currentWeight}, angle={_currentAngle}, isOverWeight={IsOverWeight}");
    }

    /// <summary>
    /// Uses a normalized percent in [0, 1] as the current weight ratio.
    /// </summary>
    public void SetWeightByPercent(float percent)
    {
        SetWeightByPercent(percent, true);
    }

    public void SetWeightByPercent(float percent, bool animate)
    {
        SetWeight(Mathf.Clamp01(percent) * maxWeight, animate);
    }

    public bool IsOverWeightValue(float weight)
    {
        return weight >= limitWeight;
    }

    public bool GetIsOverWeight()
    {
        return IsOverWeight;
    }

    #endregion

    #region Weight

    private float GetAngleByWeight(float weight)
    {
        if (maxWeight <= 0f)
            return minAngle;

        float percent = Mathf.Clamp01(weight / maxWeight);
        return Mathf.Lerp(minAngle, maxAngle, percent);
    }

    #endregion

    #region Anchor

    private void SetAnchorAngle(float angle, bool animate)
    {
        _currentAngle = angle;

        if (anchor == null)
        {
            this.LogWarning("[Level44WeightCtrl] Anchor is not assigned.");
            return;
        }

        _rotateTweener?.Kill();

        var targetEuler = new Vector3(anchor.localEulerAngles.x, anchor.localEulerAngles.y, angle);
        if (animate && rotateDuration > 0f)
        {
            _rotateTweener = anchor
                .DOLocalRotate(targetEuler, rotateDuration, RotateMode.Fast)
                .SetEase(rotateEase);
            return;
        }

        anchor.localEulerAngles = targetEuler;
    }

    #endregion

    #region Process

    private void CacheProcessBaseScale()
    {
        if (process != null)
            _processBaseScale = process.localScale;
    }

    private void SetProcessScaleTarget(float scale)
    {
        _targetProcessScale = Mathf.Clamp01(scale);
    }

    private void SetProcessScaleImmediate(float scale)
    {
        _currentProcessScale = Mathf.Clamp01(scale);
        _targetProcessScale = _currentProcessScale;
        ApplyProcessScale(_currentProcessScale);
    }

    private void UpdateProcessScale()
    {
        if (process == null || Mathf.Approximately(_currentProcessScale, _targetProcessScale))
            return;

        if (processScaleSpeed <= 0f)
        {
            SetProcessScaleImmediate(_targetProcessScale);
            return;
        }

        _currentProcessScale = Mathf.MoveTowards(
            _currentProcessScale,
            _targetProcessScale,
            processScaleSpeed * Time.deltaTime);

        ApplyProcessScale(_currentProcessScale);
    }

    private void ApplyProcessScale(float scale)
    {
        if (process == null)
            return;

        process.localScale = _processBaseScale * scale;
    }

    #endregion

    #region Log

    private void LogApi(string message)
    {
        if (!enableApiLog)
            return;

        this.Log($"[Level44WeightCtrl] {message}");
    }

    #endregion

    private void OnValidate()
    {
        maxWeight = Mathf.Max(0f, maxWeight);
        limitWeight = Mathf.Clamp(limitWeight, 0f, Mathf.Max(0f, maxWeight));
        processScaleSpeed = Mathf.Max(0f, processScaleSpeed);
        rotateDuration = Mathf.Max(0f, rotateDuration);
    }
}
