using UnityEngine;

/// <summary>
/// D091 的固定支点补正：消除重画线接触后平台与齿轮之间残留的平移误差。
/// 首笔释放与重置仍由 DLevel37Ctrl 负责，平台角度与角速度继续由物理系统决定。
/// 仅用于本关显式绑定的静态齿轮铰链，不修改共享控制器或全局物理参数。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class DLevel91PlatformAnchorStabilizer : MonoBehaviour
{
    #region 本关绑定与状态

    /// <summary>平台上的铰链，connectedBody 必须显式连接本关静态齿轮。</summary>
    [SerializeField, Tooltip("绑定本关平台的 HingeJoint2D，其连接体为中央静态齿轮。")]
    private HingeJoint2D platformHinge;

    private Rigidbody2D _platformBody;
    private RigidbodyInterpolation2D _originalInterpolation;
    private bool _ownsInterpolation;

    #endregion

    #region 生命周期

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (platformHinge == null || platformHinge.attachedRigidbody == null ||
            platformHinge.connectedBody == null ||
            platformHinge.connectedBody.bodyType != RigidbodyType2D.Static)
        {
            Debug.LogError("[D091] 平台支点补正缺少平台铰链或静态齿轮绑定。", this);
            enabled = false;
            return;
        }

        // 在D37初始化之后接管本关显示时刻，停用时归还其原插值设置。
        _platformBody = platformHinge.attachedRigidbody;
        _originalInterpolation = _platformBody.interpolation;
        _ownsInterpolation = true;
        _platformBody.interpolation = RigidbodyInterpolation2D.None;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !_ownsInterpolation || platformHinge == null ||
            !platformHinge.enabled || _platformBody == null || !_platformBody.simulated ||
            _platformBody.bodyType != RigidbodyType2D.Dynamic)
            return;

        Rigidbody2D gearBody = platformHinge.connectedBody;
        if (gearBody == null || !gearBody.simulated || gearBody.bodyType != RigidbodyType2D.Static)
            return;

        // GetRelativePoint按刚体位置/角度转换，不包含Transform缩放；D091平台缩放为1.1768。
        Vector2 platformAnchor = _platformBody.GetRelativePoint(
            Vector2.Scale(platformHinge.anchor, _platformBody.transform.lossyScale));
        Vector2 gearAnchor = gearBody.GetRelativePoint(
            Vector2.Scale(platformHinge.connectedAnchor, gearBody.transform.lossyScale));
        Vector2 correction = gearAnchor - platformAnchor;
        if (correction.sqrMagnitude <= 0.00000001f)
            return;

        Vector2 position = _platformBody.position + correction;
        _platformBody.position = position;

        // 同帧同步可见位置，否则本帧仍可能显示修正前的平台。保留Z和物理旋转/速度。
        Vector3 visualPosition = _platformBody.transform.position;
        visualPosition.x = position.x;
        visualPosition.y = position.y;
        _platformBody.transform.position = visualPosition;
    }

    private void OnDisable()
    {
        // 离开Play时isPlaying可能已经为false，仍需恢复本组件持有的插值覆盖。
        if (_ownsInterpolation && _platformBody != null &&
            _platformBody.interpolation == RigidbodyInterpolation2D.None)
            _platformBody.interpolation = _originalInterpolation;
        _ownsInterpolation = false;
    }

    #endregion
}
