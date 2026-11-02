using Spine.Unity;
using UnityEngine;

/// <summary>
/// DrawGame 猫猫、Spine 夹子与夹持球组合预制体的内部引用入口。
/// 只描述组合结构，不决定关卡终点、入场时机、释放时机或胜负规则；这些职责仍由各 DLevel 控制器持有。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Alien Clip Ball Rig View")]
public sealed class DrawAlienClipBallRigView : MonoBehaviour
{
    #region Inspector References

    /// <summary>
    /// 猫猫根节点上的动画控制器。
    /// 调用边界：供关卡控制器播放 idle01/control，不用于球内 Actor 动画。
    /// </summary>
    [Tooltip("猫猫根节点上的 ActorSpineCtrl。")]
    [SerializeField] private ActorSpineCtrl alienSpineCtrl;

    /// <summary>
    /// 提供 bone2 挂点的猫猫 Spine 实例。
    /// 注意事项：必须与本预制体内的 BoneFollower 属于同一个实例，禁止跨装置引用。
    /// </summary>
    [Tooltip("提供 bone2 挂点的猫猫 SkeletonAnimation。")]
    [SerializeField] private SkeletonAnimation alienSkeletonAnimation;

    /// <summary>
    /// 跟随猫猫 bone2 的夹子根节点组件。
    /// 调用边界：只负责骨骼挂接，额外惯性摆动由 clipSwingPivot 承担。
    /// </summary>
    [Tooltip("夹子根节点上跟随 bone2 的 BoneFollower。")]
    [SerializeField] private BoneFollower clipBoneFollower;

    /// <summary>
    /// 夹子视觉与夹持球共享的局部惯性摆动节点。
    /// 注意事项：关卡 Intro 只旋转该节点，不直接覆盖 BoneFollower 计算出的姿态。
    /// </summary>
    [Tooltip("承载夹子视觉与夹持球的惯性摆动节点。")]
    [SerializeField] private Transform clipSwingPivot;

    /// <summary>
    /// 播放 clip_close/clip_open 的夹子 Spine 实例。
    /// 调用边界：仅控制夹子视觉，不直接切换球的 Rigidbody2D 状态。
    /// </summary>
    [Tooltip("播放 clip_close / clip_open 的夹子 SkeletonAnimation。")]
    [SerializeField] private SkeletonAnimation clipSkeletonAnimation;

    /// <summary>
    /// 夹持阶段保持 Kinematic、释放后切换 Dynamic 的蓝球刚体。
    /// 注意事项：释放前必须先脱离 Spine/BoneFollower 层级并保持世界姿态。
    /// </summary>
    [Tooltip("yellow_ball 节点上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D yellowBallRigidbody;

    /// <summary>
    /// 蓝球内部视觉、Actor 与复合碰撞体的统一内容根。
    /// 调用边界：镜像装置释放球时，可把镜像从 Dynamic 刚体根转移到该节点，
    /// 从而让 Rigidbody2D 根保持正缩放且不改变玩家看到的朝向。
    /// </summary>
    [Tooltip("yellow_ball 下承载球视觉、Actor 与子碰撞体的内容根。")]
    [SerializeField] private Transform payloadContentRoot;

    /// <summary>
    /// 蓝球成功渐隐时使用的 SpriteRenderer 聚合组件。
    /// 调用边界：由需要球随人物退场的关卡按需绑定；为空不影响夹持与物理逻辑。
    /// </summary>
    [Tooltip("yellow_ball 节点上的可选 SpriteRendererGroupAlpha。")]
    [SerializeField] private SpriteRendererGroupAlpha ballRendererGroup;

    #endregion

    #region Public References

    /// <summary>
    /// 返回组合预制体根 Transform，供关卡控制器执行整体入场移动。
    /// </summary>
    public Transform AlienTransform => transform;

    /// <summary>
    /// 返回猫猫动画控制器；仅供当前关卡局部控制器播放预制动画。
    /// </summary>
    public ActorSpineCtrl AlienSpineCtrl => alienSpineCtrl;

    /// <summary>
    /// 返回猫猫 SkeletonAnimation；供 BoneFollower 初始化和实例朝向检查使用。
    /// </summary>
    public SkeletonAnimation AlienSkeletonAnimation => alienSkeletonAnimation;

    /// <summary>
    /// 返回夹子 BoneFollower；禁止将其绑定到其它组合实例的 SkeletonAnimation。
    /// </summary>
    public BoneFollower ClipBoneFollower => clipBoneFollower;

    /// <summary>
    /// 返回夹子惯性摆动节点；关卡动画结束或 Reset 后应恢复其缓存旋转。
    /// </summary>
    public Transform ClipSwingPivot => clipSwingPivot;

    /// <summary>
    /// 返回夹子 SkeletonAnimation；只用于 clip_close/clip_open 状态切换。
    /// </summary>
    public SkeletonAnimation ClipSkeletonAnimation => clipSkeletonAnimation;

    /// <summary>
    /// 返回蓝球 Rigidbody2D；关卡控制器负责 Kinematic/Dynamic 与脱父级顺序。
    /// </summary>
    public Rigidbody2D YellowBallRigidbody => yellowBallRigidbody;

    /// <summary>
    /// 返回蓝球内部内容根；镜像只在释放边界转移到此节点，不改变共享刚体参数。
    /// </summary>
    public Transform PayloadContentRoot => payloadContentRoot;

    /// <summary>
    /// 返回蓝球渲染组；仅供需要成功同步渐隐的关卡使用。
    /// </summary>
    public SpriteRendererGroupAlpha BallRendererGroup => ballRendererGroup;

    #endregion

    #region Reference Resolution

    /// <summary>
    /// 在当前组合实例内部补齐缺失引用。
    /// 调用边界：可由 prefab 迁移工具或运行时关卡初始化调用；只做本地组件查找，
    /// 不创建对象、不访问 DB/配置，也不会改变动画或物理状态。
    /// </summary>
    public void ResolveReferences()
    {
        if (alienSpineCtrl == null)
            alienSpineCtrl = GetComponent<ActorSpineCtrl>();

        if (alienSkeletonAnimation == null)
            alienSkeletonAnimation = GetComponent<SkeletonAnimation>();

        Transform clipRoot = transform.Find("catch");
        if (clipBoneFollower == null && clipRoot != null)
            clipBoneFollower = clipRoot.GetComponent<BoneFollower>();

        if (clipSwingPivot == null && clipRoot != null)
            clipSwingPivot = clipRoot.Find("clipSwingPivot");

        Transform clipVisual = clipSwingPivot?.Find("clipVisual");
        if (clipSkeletonAnimation == null && clipVisual != null)
            clipSkeletonAnimation = clipVisual.GetComponent<SkeletonAnimation>();

        Transform yellowBall = clipSwingPivot?.Find("yellow_ball");
        if (yellowBallRigidbody == null && yellowBall != null)
            yellowBallRigidbody = yellowBall.GetComponent<Rigidbody2D>();

        if (payloadContentRoot == null && yellowBallRigidbody != null)
            payloadContentRoot = yellowBallRigidbody.transform.Find("payloadContentRoot");

        if (ballRendererGroup == null && yellowBallRigidbody != null)
            ballRendererGroup = yellowBallRigidbody.GetComponent<SpriteRendererGroupAlpha>();
    }

    #endregion
}
