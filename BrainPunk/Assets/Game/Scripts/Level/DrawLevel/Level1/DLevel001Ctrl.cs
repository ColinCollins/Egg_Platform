using Bear.Logger;
using UnityEngine;

/// <summary>
/// DLevel_001 的关卡专用控制脚本。
/// 持有通用 DrawLevelCtrl，在玩家开始绘制时隐藏整组引导，并在第一笔有效 Stroke 完成后
/// 直接驱动 Actor 向右移动。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/DLevel 001 Ctrl")]
public sealed class DLevel001Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector

    /// <summary>
    /// DLevel_001 的通用状态控制器。
    /// 调用边界：必须指向同一 prefab 根节点上的 DrawLevelCtrl，不允许跨关卡引用。
    /// </summary>
    [Header("Runtime")]
    [Tooltip("DLevel_001 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// DLevel_001 中由完成画线触发自动移动的玩家 Actor。
    /// 调用边界：必须绑定本关 prefab 内的 ActorCtrl；为空时仅在运行时从子层级解析。
    /// </summary>
    [Tooltip("第一笔有效 Stroke 完成后持续向右移动的 Actor。")]
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// DLevel_001 开始绘制后需要隐藏的场景引导根节点。
    /// 调用边界：必须绑定本关 Guide 根节点；为空时仅在运行时按该固定层级回退查找。
    /// 注意事项：隐藏后 GuideLine 和 Finger_Scene 会一起停止显示。
    /// </summary>
    [Tooltip("玩家在有效绘制区域内开始落笔后隐藏的 Guide 根节点。")]
    [SerializeField] private GameObject guideRoot;

    #endregion

    #region Runtime State

    private DrawLineFeature _drawFeature;
    private bool _moveRight;
    private bool _moveInputApplied;
    private bool _initialGuideVisible;
    private bool _hasCapturedGuideState;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved += OnLevelResolved;

        if (_drawFeature != null)
        {
            _drawFeature.StrokeStarted -= OnStrokeStarted;
            _drawFeature.StrokeStarted += OnStrokeStarted;
        }

        CaptureGuideState();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved -= OnLevelResolved;

        if (_drawFeature != null)
            _drawFeature.StrokeStarted -= OnStrokeStarted;

        StopMovingRight();
    }

    private void Update()
    {
        if (!_moveRight || actor == null)
            return;

        if (drawLevelCtrl == null || drawLevelCtrl.IsPaused || actor.IsDied)
        {
            ClearMoveInput();
            return;
        }

        actor.SetMoveInput(1f);
        _moveInputApplied = true;
        actor.OnUpdate();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke)
            return;

        ResolveReferences();
        if (actor == null)
        {
            this.LogError("[DLevel001Ctrl] ActorCtrl 缺失，第一笔完成后无法向右移动。");
            return;
        }

        _moveRight = true;
    }

    /// <inheritdoc />
    public void OnDrawAttemptReset()
    {
        StopMovingRight();
        RestoreGuide();
    }

    #endregion

    #region Internal

    private void OnStrokeStarted()
    {
        if (guideRoot != null)
            guideRoot.SetActive(false);
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        StopMovingRight();
    }

    private void StopMovingRight()
    {
        _moveRight = false;
        ClearMoveInput();
    }

    private void ClearMoveInput()
    {
        if (!_moveInputApplied || actor == null)
            return;

        actor.SetMoveInput(0f);
        actor.UpdateAnimation();
        _moveInputApplied = false;
    }

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);

        if (_drawFeature == null)
            _drawFeature = GetComponentInParent<DrawLineFeature>(true);

        if (actor == null)
            actor = GetComponentInChildren<ActorCtrl>(true);

        if (guideRoot == null)
        {
            Transform guideTransform = transform.Find("Guide");
            if (guideTransform != null)
                guideRoot = guideTransform.gameObject;
        }
    }

    private void CaptureGuideState()
    {
        if (_hasCapturedGuideState || guideRoot == null)
            return;

        _initialGuideVisible = guideRoot.activeSelf;
        _hasCapturedGuideState = true;
    }

    private void RestoreGuide()
    {
        ResolveReferences();
        CaptureGuideState();
        if (guideRoot != null && _hasCapturedGuideState)
            guideRoot.SetActive(_initialGuideVisible);
    }

    #endregion
}
