using UnityEngine;

public class ClickScaleAnim : ButtonClickTrigger
{
    [Tooltip("可选：按钮按压缩放的视觉目标。为空时沿用旧行为，缩放当前按钮物体；配置为子视觉节点时不会改变按钮根节点的射线命中范围。")]
    [SerializeField]
    private Transform scaleTarget;

    [SerializeField]
    private float Scale = 0.8f;
    [Tooltip("插值开始前额外等待（缩放不变）；真正缩放时长见 Duration")]
    [SerializeField]
    private float Delay = 0f;
    [Tooltip("从当前缩放到目标缩放的插值时长（不含 Delay）")]
    [SerializeField] private float Duration = 0.1f;

    [SerializeField]
    private bool ignoreScaleTime = true;

    private Vector3 _targetScale = Vector3.one;
    private Vector3 _startScale = Vector3.one;
    private float _timer = 0f;
    private bool _isAnimating = false;
    private float _currentDelay = 0f;
    private Transform _activeScaleTarget;
    private Vector3 _normalScale = Vector3.one;
    /// <summary> true = 正在缩放到按下态，需播完；false = 回弹，可被新的按下打断 </summary>
    private bool _isPressPhase = false;
    /// <summary> 按下动画未结束时已收到抬起，在按下结束后再开始抬起动画 </summary>
    private bool _releasePending = false;

    /// <summary>
    /// 运行时指定按钮按压缩放的视觉目标；传入 null 会恢复旧行为（缩放当前物体）。
    /// 调用边界：应在按钮开始交互前调用，交互中切换会重置当前缩放动画状态。
    /// </summary>
    public void SetScaleTarget(Transform target)
    {
        if (_activeScaleTarget != null)
            _activeScaleTarget.localScale = _normalScale;

        scaleTarget = target;
        ResolveScaleTarget(true);
    }

    public override void OnButtonDown(bool hasAnim)
    {
        _releasePending = false;
        BeginPressAnim();
    }

    public override void OnButtonUp(bool hasAnim)
    {
        if (_isAnimating && _isPressPhase)
        {
            _releasePending = true;
            return;
        }

        BeginReleaseAnim();
    }

    private void BeginPressAnim()
    {
        Transform target = ResolveScaleTarget(false);
        if (scaleTarget != null && !_isAnimating)
            _normalScale = target.localScale;

        _startScale = target.localScale;
        _targetScale = GetNormalScale() * Scale;
        _timer = 0f;
        _currentDelay = Delay;
        _isPressPhase = true;
        _isAnimating = true;
    }

    private void BeginReleaseAnim()
    {
        Transform target = ResolveScaleTarget(false);
        _releasePending = false;
        _startScale = target.localScale;
        _targetScale = GetNormalScale();
        _timer = 0f;
        _currentDelay = Delay;
        _isPressPhase = false;
        _isAnimating = true;
    }

    private void LateUpdate()
    {
        if (!_isAnimating) return;

        Transform target = ResolveScaleTarget(false);
        float deltaTime = ignoreScaleTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // 延迟耗尽时，本帧剩余 delta 要并进插值，否则会多白等一整帧、总时长偏长
        if (_currentDelay > 0f)
        {
            _currentDelay -= deltaTime;
            if (_currentDelay > 0f)
                return;
            deltaTime = -_currentDelay;
            _currentDelay = 0f;
        }

        if (Duration <= 0f)
        {
            target.localScale = _targetScale;
            OnAnimSegmentComplete();
            return;
        }

        _timer += deltaTime;
        float t = Mathf.Clamp01(_timer / Duration);

        target.localScale = Vector3.LerpUnclamped(_startScale, _targetScale, t);

        if (t >= 1f)
        {
            target.localScale = _targetScale;
            OnAnimSegmentComplete();
        }
    }

    private Transform ResolveScaleTarget(bool resetAnimationState)
    {
        Transform target = scaleTarget != null ? scaleTarget : transform;
        if (_activeScaleTarget != target)
        {
            _activeScaleTarget = target;
            _normalScale = scaleTarget != null ? target.localScale : Vector3.one;

            if (resetAnimationState)
            {
                _timer = 0f;
                _currentDelay = 0f;
                _isAnimating = false;
                _isPressPhase = false;
                _releasePending = false;
            }
        }

        return _activeScaleTarget;
    }

    private Vector3 GetNormalScale()
    {
        return scaleTarget != null ? _normalScale : Vector3.one;
    }

    private void OnAnimSegmentComplete()
    {
        if (_isPressPhase && _releasePending)
        {
            _releasePending = false;
            BeginReleaseAnim();
            return;
        }

        _isAnimating = false;
        _isPressPhase = false;
    }
}
