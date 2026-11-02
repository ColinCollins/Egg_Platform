using UnityEngine;

/// <summary>
/// DrawGame 第 11 关 Actor 专用控制器。
/// 在保留通用 Actor 行为和行走动画的基础上，阻止角色沿超过阈值的 Draw 层斜面向上攀爬。
/// </summary>
[DisallowMultipleComponent]
public sealed class DLevel11ActorCtrl : ActorCtrl
{
    #region Constants

    private const int MaxSlopeContactCount = 8;

    #endregion

    #region Inspector

    /// <summary>
    /// 可向上行走的最大坡度角；仅影响本关 Actor，0 表示只允许水平面，89 接近不限制。
    /// </summary>
    [Header("Slope Limit")]
    [SerializeField, Range(0f, 89f)]
    private float maxWalkableSlopeAngle = 50f;

    /// <summary>
    /// 参与坡度限制的物理层；DLevel_011 prefab 仅配置 Draw 层，避免影响普通地面和机关。
    /// </summary>
    [SerializeField]
    private LayerMask slopeCheckLayers;

    #endregion

    #region Runtime State

    private readonly ContactPoint2D[] _slopeContacts = new ContactPoint2D[MaxSlopeContactCount];
    private ContactFilter2D _slopeContactFilter;
    private bool _isSlopeContactFilterReady;

    #endregion

    #region Actor Lifecycle

    /// <summary>
    /// 由当前 DrawGame 关卡控制器逐帧调用；陡坡上行时只冻结实际速度，保留输入供通用行走动画使用。
    /// </summary>
    public override void OnUpdate()
    {
        if (!ShouldBlockUphillMovement())
        {
            base.OnUpdate();
            return;
        }

        float originalMoveSpeed = MoveSpeed;
        MoveSpeed = 0f;
        try
        {
            base.OnUpdate();
        }
        finally
        {
            MoveSpeed = originalMoveSpeed;
        }
    }

    #endregion

    #region Slope Detection

    private bool ShouldBlockUphillMovement()
    {
        if (!Application.isPlaying || RB == null || Mathf.Approximately(XInput, 0f))
            return false;

        EnsureSlopeContactFilter();
        int contactCount = RB.GetContacts(_slopeContactFilter, _slopeContacts);

        for (int i = 0; i < contactCount; i++)
        {
            Vector2 supportNormal = GetActorFacingNormal(_slopeContacts[i]);
            if (supportNormal.y <= 0f)
                continue;

            float slopeAngle = Vector2.Angle(supportNormal, Vector2.up);
            bool isMovingUphill = XInput * -supportNormal.x > 0f;
            if (isMovingUphill && slopeAngle > maxWalkableSlopeAngle)
                return true;
        }

        return false;
    }

    private void EnsureSlopeContactFilter()
    {
        if (_isSlopeContactFilterReady)
            return;

        _slopeContactFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = slopeCheckLayers
        };
        _isSlopeContactFilterReady = true;
    }

    private Vector2 GetActorFacingNormal(ContactPoint2D contact)
    {
        Vector2 normal = contact.normal;
        bool colliderBelongsToActor = contact.collider != null && contact.collider.attachedRigidbody == RB;
        bool otherColliderBelongsToActor =
            contact.otherCollider != null && contact.otherCollider.attachedRigidbody == RB;

        if (colliderBelongsToActor && !otherColliderBelongsToActor)
            normal = -normal;

        return normal;
    }

    #endregion
}
