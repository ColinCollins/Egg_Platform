using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>狗在门内贴钩尖显现；上背挂点始终贴住钩尖，实际回收距离驱动0.35→0.8缩放。</summary>
public sealed class Level137DogExit : MonoBehaviour
{
    // 狗的局部 Spine 控制器；模板必须提供 idle 动画和可查找的挂点骨骼。
    [SerializeField] private ActorSpineCtrl dogSpine;
    [Header("Upper-back hook contact")]
    // 狗上背用于接触钩尖的骨骼名称，不负责改变狗的整体朝向。
    [SerializeField] private string hookBoneName = "idle5";
    [Tooltip("导入后Spine骨骼局部单位；先以此上背挂点人工预览，调整不影响素材大小。")]
    // 挂点骨骼局部偏移；只用于把上背挂点对齐到钩尖。
    [SerializeField] private Vector2 hookBoneOffset = new Vector2(0.15f, 0.35f);
    private static readonly Vector3 FullScale = Vector3.one * 0.8f;
    private Vector3 initialScale;
    private SkeletonAnimation skeletonAnimation;
    private Bone hookBone;
    private Transform hookTransform;
    private Vector2 hookTipLocal;
    private Vector3 startTip;
    private Vector3 pullDirection;
    private float exitDistance;
    /// <summary>返回狗的 Spine 控制器，供本关打斗表现和透明度交接使用。</summary>
    public ActorSpineCtrl Spine => dogSpine;

    /// <summary>表示狗是否已经完成离开门区域的回收距离计算。</summary>
    public bool Completed { get; private set; }

    /// <summary>表示狗当前是否仍由钩尖挂点驱动。</summary>
    public bool IsAttached { get; private set; }

    private void Awake() { initialScale = dogSpine.transform.localScale; }

    /// <summary>
    /// 在门内创建并挂载狗，按完整狗身包围盒计算离门所需的真实回收距离。
    /// 失败时会停用自身并返回 false，不会伪造狗已完成出门。
    /// </summary>
    /// <param name="doorFrame">正式门入口区域的坐标参考。</param>
    /// <param name="exitRegion">用于计算狗完全离开门区域的局部范围。</param>
    /// <param name="hook">当前钩爪 Transform。</param>
    /// <param name="tipLocal">钩尖在钩爪局部坐标中的位置。</param>
    /// <param name="retractDirection">钩爪回收方向。</param>
    /// <param name="maximumPullDistance">当前绳长允许的最大回收距离。</param>
    /// <returns>资源、挂点和剩余绳长均可用时返回 true，否则返回 false。</returns>
    public bool Begin(Transform doorFrame, Bounds exitRegion, Transform hook, Vector2 tipLocal,
        Vector3 retractDirection, float maximumPullDistance)
    {
        Release();
        gameObject.SetActive(true);
        transform.rotation = Quaternion.identity;
        dogSpine.transform.localScale = initialScale;
        TrackEntry track = dogSpine.PlayAnimation("idle", true, false);
        dogSpine.SetAlpha(1f);
        skeletonAnimation = dogSpine.GetComponent<SkeletonAnimation>();
        hookBone = skeletonAnimation != null && skeletonAnimation.Skeleton != null
            ? skeletonAnimation.Skeleton.FindBone(hookBoneName) : null;
        if (hook == null || track == null || hookBone == null || retractDirection.sqrMagnitude < 0.000001f)
        {
            Debug.LogError("[Level137] Cannot attach dog: missing upper-back bone, hook or idle pose.", this);
            gameObject.SetActive(false);
            return false;
        }
        track.MixDuration = 0f;
        skeletonAnimation.AnimationState.Apply(skeletonAnimation.Skeleton);
        Bounds bodyLocal = Level137Geometry.SkeletonLocalBounds(skeletonAnimation);
        hookTransform = hook;
        hookTipLocal = tipLocal;
        startTip = hookTransform.TransformPoint(hookTipLocal);
        pullDirection = retractDirection.normalized;
        Completed = false;

        // 按0.8体型、真实挂点和原回收方向求完全离门距离，不再从门中心另走一条轨迹。
        dogSpine.transform.localScale = FullScale;
        PinToHook();
        Bounds fullBody = Level137Geometry.TransformBounds(dogSpine.transform, bodyLocal);
        bool overlaps = Level137Geometry.SweepShape(fullBody.center, fullBody.center + pullDirection,
            fullBody.extents, doorFrame, exitRegion, out _, out float exit);
        float requiredDistance = overlaps ? Mathf.Max(0f, exit) + 0.01f : 0f;
        if (float.IsNaN(requiredDistance) || float.IsInfinity(requiredDistance) ||
            requiredDistance > maximumPullDistance + 0.0001f)
        {
            // 不用让狗独自飞走或提前宣称完全离门来掩盖不可达的布局。
            Debug.LogError("[Level137] Dog cannot clear the door within the remaining hook travel; check the door and hook layout.", this);
            gameObject.SetActive(false);
            return false;
        }
        // 只消除容差内的浮点尾差，避免绳子已收完但Completed永远差一点。
        exitDistance = Mathf.Min(requiredDistance, Mathf.Max(0f, maximumPullDistance));
        IsAttached = true;
        skeletonAnimation.UpdateComplete += OnPoseUpdated;
        FollowHook();
        return true;
    }

    /// <summary>
    /// 根据钩尖实际位移更新狗的缩放和挂点位置；不创建独立运动计时器。
    /// </summary>
    public void FollowHook()
    {
        if (!IsAttached) return;
        if (hookTransform == null) { Release(); return; }
        Vector3 tip = hookTransform.TransformPoint(hookTipLocal);
        float travel = Mathf.Max(0f, Vector3.Dot(tip - startTip, pullDirection));
        Completed = travel >= exitDistance - 0.0001f;
        float progress = Completed ? 1f : Mathf.Clamp01(travel / exitDistance);
        dogSpine.transform.localScale = Vector3.Lerp(initialScale, FullScale, progress);
        PinToHook();
    }

    private void PinToHook()
    {
        hookBone.LocalToWorld(hookBoneOffset.x, hookBoneOffset.y, out float x, out float y);
        Vector3 contact = dogSpine.transform.TransformPoint(new Vector3(x, y, 0f));
        transform.position += hookTransform.TransformPoint(hookTipLocal) - contact;
    }

    // idle的骨骼更新后再次贴合，避免身体呼吸/扭动在本帧把挂点从钩尖拉开。
    private void OnPoseUpdated(ISkeletonAnimation _) { FollowHook(); }

    /// <summary>
    /// 解除 Spine 更新回调和钩爪引用；不负责把狗重新放回初始位置。
    /// </summary>
    public void Release()
    {
        IsAttached = false;
        if (skeletonAnimation != null) skeletonAnimation.UpdateComplete -= OnPoseUpdated;
        hookTransform = null;
        hookBone = null;
    }

    private void OnDisable() { Release(); }
    private void OnDestroy() { Release(); }
}
