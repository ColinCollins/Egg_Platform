using UnityEngine;

/// <summary>
/// 管理 DLevel_038 的双锤铰链机关。
/// 第一笔有效画线完成前固定双锤；完成后同时切换为 Dynamic，
/// 让两个锤子在重力作用下绕同一个静态齿轮自由旋转。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 38 Double Hammer Ctrl")]
public sealed class DLevel038Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Inspector References

    [Header("双锤铰链")]
    [Tooltip("中央齿轮上的 Static Rigidbody2D。")]
    [SerializeField] private Rigidbody2D gearRigidbody;

    [Tooltip("左右两个锤子的 Rigidbody2D，顺序必须与锤子铰链一致。")]
    [SerializeField] private Rigidbody2D[] hammerRigidbodies;

    [Tooltip("左右两个锤子的 HingeJoint2D，顺序必须与锤子刚体一致。")]
    [SerializeField] private HingeJoint2D[] hammerHinges;

    #endregion

    #region Physics Parameters

    [Header("锤子物理")]
    [Tooltip("锤子的线性阻尼。铰链会限制平移，该值主要抑制接头数值抖动。")]
    [SerializeField, Min(0f)] private float hammerLinearDamping = 0.05f;

    [Tooltip("锤子的角阻尼；数值越高，摆动越快衰减。")]
    [SerializeField, Min(0f)] private float hammerAngularDamping = 0.2f;

    [Tooltip("锤子的重力倍率。")]
    [SerializeField, Min(0f)] private float hammerGravityScale = 1f;

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

    /// <summary>第一笔有效画线完成后，同时释放左右两个锤子。</summary>
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
        for (int i = 0; i < hammerRigidbodies.Length; i++)
            ReleaseBody(hammerRigidbodies[i]);
    }

    /// <summary>清线、失败重开或主动重置时恢复双锤的等待状态。</summary>
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
        int count = hammerRigidbodies?.Length ?? 0;
        bool isValid =
            gearRigidbody != null &&
            hammerHinges != null &&
            hammerHinges.Length == count;

        if (isValid)
        {
            for (int i = 0; i < count; i++)
            {
                if (hammerRigidbodies[i] == null || hammerHinges[i] == null)
                {
                    isValid = false;
                    break;
                }
            }
        }

        if (!isValid)
        {
            Debug.LogError(
                "[DLevel038Ctrl] 中央齿轮或左右双锤的刚体/铰链绑定不完整，机关已停用。",
                this);
        }

        return isValid;
    }

    private void CaptureInitialState()
    {
        int count = hammerRigidbodies.Length;
        _initialPositions = new Vector2[count];
        _initialRotations = new float[count];

        for (int i = 0; i < count; i++)
        {
            _initialPositions[i] = hammerRigidbodies[i].position;
            _initialRotations[i] = hammerRigidbodies[i].rotation;
        }
    }

    private void ConfigurePhysics()
    {
        gearRigidbody.bodyType = RigidbodyType2D.Static;
        gearRigidbody.simulated = true;

        for (int i = 0; i < hammerRigidbodies.Length; i++)
        {
            Rigidbody2D hammer = hammerRigidbodies[i];
            HingeJoint2D hinge = hammerHinges[i];

            hammer.simulated = true;
            hammer.linearDamping = Mathf.Max(0f, hammerLinearDamping);
            hammer.angularDamping = Mathf.Max(0f, hammerAngularDamping);
            hammer.gravityScale = Mathf.Max(0f, hammerGravityScale);
            hammer.interpolation = RigidbodyInterpolation2D.Interpolate;
            hammer.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            hammer.constraints = RigidbodyConstraints2D.None;

            hinge.enabled = true;
            hinge.connectedBody = gearRigidbody;
            hinge.enableCollision = false;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.useConnectedAnchor = true;
            hinge.anchor = hammer.transform.InverseTransformPoint(
                gearRigidbody.transform.position);
            hinge.connectedAnchor = gearRigidbody.transform.InverseTransformPoint(
                gearRigidbody.transform.position);
            hinge.useMotor = false;
            hinge.useLimits = false;
            hinge.breakForce = Mathf.Infinity;
            hinge.breakTorque = Mathf.Infinity;
        }
    }

    private void RestoreWaitingState()
    {
        _isReleased = false;
        for (int i = 0; i < hammerRigidbodies.Length; i++)
        {
            Rigidbody2D hammer = hammerRigidbodies[i];
            hammer.bodyType = RigidbodyType2D.Kinematic;
            hammer.position = _initialPositions[i];
            hammer.rotation = _initialRotations[i];
            hammer.linearVelocity = Vector2.zero;
            hammer.angularVelocity = 0f;
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
