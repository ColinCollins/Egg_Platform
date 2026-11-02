using Bear.Logger;
using UnityEngine;

/// <summary>
/// 第一笔有效画线完成后，把 Actor Rigidbody2D 改为 Dynamic，并开启场景内的 AreaEffector2D 风区。
/// 本控制器不直接施加力，只负责首笔门禁、结算和清线/重试时的局部状态边界。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 40 Area Effector Ctrl")]
public sealed class DLevel040Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 接收风力的 Actor 刚体。
    /// 调用边界：只绑定 DLevel_040 prefab 内 Actor 根节点的 Rigidbody2D。
    /// </summary>
    [Header("Bindings")]
    [Tooltip("DLevel_040/Scene/Actor 根节点上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D actorRigidbody;

    /// <summary>
    /// 当前关卡根节点上的 DrawLevelCtrl，用于读取暂停状态并监听结算。
    /// </summary>
    [Tooltip("DLevel_040 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 第一笔完成后启用的风区 AreaEffector2D。
    /// 调用边界：只绑定 DLevel_040/Scene/Fan/WindArea；风力方向、大小和作用层由 prefab 配置。
    /// </summary>
    [Tooltip("DLevel_040/Scene/Fan/WindArea 上的 AreaEffector2D。")]
    [SerializeField] private AreaEffector2D windAreaEffector;

    #endregion

    #region Runtime State

    private Vector2 _initialPosition;
    private float _initialRotation;
    private RigidbodyType2D _initialBodyType;
    private bool _hasInitialState;
    private bool _hasActivated;
    private bool _isResolved;
    private DrawLevelCtrl _subscribedDrawLevelCtrl;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (actorRigidbody == null || windAreaEffector == null)
        {
            this.LogError("[DLevel040Ctrl] Actor Rigidbody2D 或 WindArea AreaEffector2D 未绑定，风区流程已停用。");
            enabled = false;
            return;
        }

        CacheInitialState();
        SubscribeToLevelResolved();
        _isResolved = false;
        _hasActivated = false;
        SetWindAreaActive(false);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        SetWindAreaActive(false);
        UnsubscribeFromLevelResolved();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后将 Actor 切换为 Dynamic，并启用 AreaEffector2D 风区。
    /// 后续笔画或重复回调不会重复切换物理状态。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || _isResolved || _hasActivated || !result.IsFirstStroke)
            return;

        ResolveReferences();
        if (actorRigidbody == null)
        {
            this.LogError("[DLevel040Ctrl] Actor Rigidbody2D 未绑定，无法启动风区流程。");
            return;
        }

        if (windAreaEffector == null)
        {
            this.LogError("[DLevel040Ctrl] WindArea AreaEffector2D 未绑定，无法启动风区流程。");
            return;
        }

        _hasActivated = true;
        actorRigidbody.bodyType = RigidbodyType2D.Dynamic;
        actorRigidbody.linearVelocity = Vector2.zero;
        actorRigidbody.angularVelocity = 0f;
        actorRigidbody.WakeUp();
        SetWindAreaActive(true);
    }

    /// <summary>
    /// 清线或重置尝试时关闭 AreaEffector2D 风区，并恢复 Actor 的初始位姿、刚体类型和零速度。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying || !_hasInitialState || actorRigidbody == null)
            return;

        _isResolved = false;
        _hasActivated = false;
        SetWindAreaActive(false);

        actorRigidbody.bodyType = _initialBodyType;
        actorRigidbody.linearVelocity = Vector2.zero;
        actorRigidbody.angularVelocity = 0f;
        actorRigidbody.position = _initialPosition;
        actorRigidbody.rotation = _initialRotation;
        actorRigidbody.Sleep();
    }

    #endregion

    #region References And Level Lifecycle

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponent<DrawLevelCtrl>();

        if (actorRigidbody == null)
        {
            Rigidbody2D[] rigidbodies = GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody2D candidate = rigidbodies[i];
                if (candidate != null && candidate.CompareTag("Player"))
                {
                    actorRigidbody = candidate;
                    break;
                }
            }
        }

        if (windAreaEffector == null)
        {
            Transform windArea = transform.Find("Scene/Fan/WindArea");
            windAreaEffector = windArea?.GetComponent<AreaEffector2D>();
        }
    }

    private void SetWindAreaActive(bool active)
    {
        if (windAreaEffector == null)
            return;

        GameObject windArea = windAreaEffector.gameObject;
        if (active)
        {
            if (!windArea.activeSelf)
                windArea.SetActive(true);

            windAreaEffector.enabled = true;
            return;
        }

        windAreaEffector.enabled = false;
        if (windArea.activeSelf)
            windArea.SetActive(false);
    }

    private void CacheInitialState()
    {
        if (_hasInitialState || actorRigidbody == null)
            return;

        _initialPosition = actorRigidbody.position;
        _initialRotation = actorRigidbody.rotation;
        _initialBodyType = actorRigidbody.bodyType;
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
        SetWindAreaActive(false);
    }

    #endregion
}
