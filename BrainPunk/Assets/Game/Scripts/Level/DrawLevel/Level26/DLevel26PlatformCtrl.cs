using UnityEngine;

/// <summary>
/// DLevel_026 两组齿轮铰链平台的物理状态控制器。
/// 开场和重开时固定平台；第一笔有效画线完成后切换为 Dynamic，
/// 让平台在球、画线和重力作用下绕各自中心齿轮自由旋转。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 26 Platform Ctrl")]
public sealed class DLevel26PlatformCtrl : MonoBehaviour,
    IDrawStrokeCompletedHandler
{
    #region Inspector References

    /// <summary>两组平台中心齿轮上的 Static Rigidbody2D。</summary>
    [Header("齿轮铰链平台")]
    [Tooltip("两组中心齿轮的 Static Rigidbody2D，顺序必须与平台和铰链一致。")]
    [SerializeField] private Rigidbody2D[] gearRigidbodies;

    /// <summary>两块受力旋转平台上的 Rigidbody2D。</summary>
    [Tooltip("两块平台的 Rigidbody2D，第一笔完成前固定，之后切换为 Dynamic。")]
    [SerializeField] private Rigidbody2D[] platformRigidbodies;

    /// <summary>连接平台和对应中心齿轮的 HingeJoint2D。</summary>
    [Tooltip("两块平台上的 HingeJoint2D，顺序必须与齿轮和平台一致。")]
    [SerializeField] private HingeJoint2D[] platformHinges;

    #endregion

    #region Runtime State

    private Vector2[] _initialPositions;
    private float[] _initialRotations;
    private bool _isInitialized;
    private bool _isReleased;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        CaptureInitialState();
        ConfigurePhysics();
        _isInitialized = true;
        RestoreWaitingState();
    }

    #endregion

    #region Draw Handlers

    /// <summary>
    /// 第一笔有效画线完成后释放两块平台，使其与夹子中的球在同一帧进入物理模拟。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying ||
            !_isInitialized ||
            _isReleased ||
            !result.IsFirstStroke)
        {
            return;
        }

        _isReleased = true;
        for (int i = 0; i < platformRigidbodies.Length; i++)
            ReleaseBody(platformRigidbodies[i]);
    }

    /// <summary>清线或重开时恢复两块平台的初始位置、角度和固定状态。</summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying || !_isInitialized)
            return;

        RestoreWaitingState();
    }

    #endregion

    #region Physics Setup

    private bool ValidateReferences()
    {
        int count = platformRigidbodies?.Length ?? 0;
        bool isValid =
            count > 0 &&
            gearRigidbodies != null &&
            platformHinges != null &&
            gearRigidbodies.Length == count &&
            platformHinges.Length == count;

        if (isValid)
        {
            for (int i = 0; i < count; i++)
            {
                if (gearRigidbodies[i] == null ||
                    platformRigidbodies[i] == null ||
                    platformHinges[i] == null)
                {
                    isValid = false;
                    break;
                }
            }
        }

        if (!isValid)
        {
            Debug.LogError(
                "[DLevel26PlatformCtrl] 齿轮、平台或铰链引用缺失/数量不一致，平台物理已停用。",
                this);
        }

        return isValid;
    }

    private void CaptureInitialState()
    {
        int count = platformRigidbodies.Length;
        _initialPositions = new Vector2[count];
        _initialRotations = new float[count];

        for (int i = 0; i < count; i++)
        {
            _initialPositions[i] = platformRigidbodies[i].position;
            _initialRotations[i] = platformRigidbodies[i].rotation;
        }
    }

    private void ConfigurePhysics()
    {
        for (int i = 0; i < platformRigidbodies.Length; i++)
        {
            Rigidbody2D gear = gearRigidbodies[i];
            Rigidbody2D platform = platformRigidbodies[i];
            HingeJoint2D hinge = platformHinges[i];

            gear.bodyType = RigidbodyType2D.Static;
            gear.simulated = true;

            platform.simulated = true;
            platform.interpolation = RigidbodyInterpolation2D.Interpolate;
            platform.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            platform.constraints = RigidbodyConstraints2D.None;

            hinge.enabled = true;
            hinge.connectedBody = gear;
            hinge.enableCollision = false;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.useConnectedAnchor = true;
            hinge.anchor = platform.transform.InverseTransformPoint(gear.transform.position);
            hinge.connectedAnchor = gear.transform.InverseTransformPoint(gear.transform.position);
            hinge.useMotor = false;
            hinge.useLimits = false;
            hinge.breakForce = Mathf.Infinity;
            hinge.breakTorque = Mathf.Infinity;
        }
    }

    private void RestoreWaitingState()
    {
        _isReleased = false;
        for (int i = 0; i < platformRigidbodies.Length; i++)
        {
            Rigidbody2D platform = platformRigidbodies[i];
            platform.bodyType = RigidbodyType2D.Kinematic;
            platform.position = _initialPositions[i];
            platform.rotation = _initialRotations[i];
            platform.linearVelocity = Vector2.zero;
            platform.angularVelocity = 0f;
        }

        Physics2D.SyncTransforms();
    }

    private static void ReleaseBody(Rigidbody2D body)
    {
        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.WakeUp();
    }

    #endregion
}
