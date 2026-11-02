using Bear.Logger;
using UnityEngine;

/// <summary>
/// 管理 DLevel_035 的 Wood 旋转演出。
/// 第一笔有效画线完成后开始绕 Wood 自身中心匀速旋转，清线或重试时恢复初始角度。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 35 Wood Rotation Ctrl")]
public sealed class DLevel35Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 需要绕自身中心旋转的 Wood 根节点。
    /// 调用边界：只绑定 DLevel_035/Scene/Wood，不应指向共享资源或其它关卡对象。
    /// </summary>
    [Header("Bindings")]
    [Tooltip("DLevel_035/Scene/Wood 根节点。")]
    [SerializeField] private Transform wood;

    /// <summary>
    /// 当前关卡根节点上的 DrawLevelCtrl，用于读取暂停状态并监听结算。
    /// </summary>
    [Tooltip("DLevel_035 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    #endregion

    #region Rotation Parameters

    /// <summary>
    /// Wood 每秒旋转角度；正值逆时针，负值顺时针，零表示保持静止。
    /// </summary>
    [Header("Rotation")]
    [Tooltip("Wood 每秒旋转角度；正值逆时针，负值顺时针。")]
    [SerializeField] private float rotationSpeed = 10f;

    #endregion

    #region Runtime State

    private Quaternion _initialLocalRotation;
    private bool _hasInitialState;
    private bool _isRotating;
    private bool _isResolved;
    private DrawLevelCtrl _subscribedDrawLevelCtrl;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (wood == null)
        {
            this.LogError("[DLevel35Ctrl] Wood 未绑定，首笔旋转演出已停用。");
            enabled = false;
            return;
        }

        CacheInitialState();
        SubscribeToLevelResolved();
        _isResolved = false;
        _isRotating = false;
    }

    private void Update()
    {
        if (!_isRotating || _isResolved || wood == null ||
            (drawLevelCtrl != null && drawLevelCtrl.IsPaused))
        {
            return;
        }

        wood.Rotate(0f, 0f, rotationSpeed * Time.deltaTime, Space.Self);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        UnsubscribeFromLevelResolved();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后启动 Wood 旋转；后续笔画不会重复改变当前角度或旋转状态。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !_hasInitialState || _isResolved ||
            _isRotating || !result.IsFirstStroke)
        {
            return;
        }

        _isRotating = true;
    }

    /// <summary>
    /// 清线或重置尝试时停止旋转，并恢复 Wood 的初始局部角度。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying || !_hasInitialState || wood == null)
            return;

        _isResolved = false;
        _isRotating = false;
        wood.localRotation = _initialLocalRotation;
    }

    #endregion

    #region References And Level Lifecycle

    private void ResolveReferences()
    {
        if (wood == null)
            wood = transform.Find("Scene/Wood");

        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponent<DrawLevelCtrl>();
    }

    private void CacheInitialState()
    {
        if (_hasInitialState)
            return;

        _initialLocalRotation = wood.localRotation;
        _hasInitialState = true;
    }

    private void SubscribeToLevelResolved()
    {
        if (_subscribedDrawLevelCtrl == drawLevelCtrl)
            return;

        UnsubscribeFromLevelResolved();
        _subscribedDrawLevelCtrl = drawLevelCtrl;
        if (_subscribedDrawLevelCtrl != null)
            _subscribedDrawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void UnsubscribeFromLevelResolved()
    {
        if (_subscribedDrawLevelCtrl == null)
            return;

        _subscribedDrawLevelCtrl.Resolved -= OnLevelResolved;
        _subscribedDrawLevelCtrl = null;
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        _isResolved = true;
        _isRotating = false;
    }

    #endregion
}
