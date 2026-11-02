using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// 控制共享猫猫夹子球装置按配置方向入场、默认夹紧、放球开夹、原路退场与落地双弹演出。
/// 球体双弹和猫猫夹子退场全部结束前保持 Draw Intro 门禁，防止玩家提前画线。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 10 Ctrl")]
public sealed class DLevel10Ctrl : MonoBehaviour,
    IDrawLevelVisibleIntroHandler,
    IDebuger
{
    #region Types

    /// <summary>
    /// 单套共享装置支持的入场方向状态。
    /// DLevel_010 使用 FromTop；复用该流程的关卡可显式选择左右入场。
    /// </summary>
    private enum RigEntranceState
    {
        FromLeft = 0,
        FromRight = 1,
        FromTop = 2,
    }

    #endregion

    #region Constants

    private const string ClipAttachmentBone = "bone2";
    private const string ClipCloseAnimation = "clip_close";
    private const string ClipOpenAnimation = "clip_open";
    private const float MinimumDuration = 0.01f;
    private const int RetryLandingHitCapacity = 32;

    #endregion

    #region Inspector References

    /// <summary>
    /// DLevel_010 内复用的猫猫、Spine 夹子与蓝球组合预制体入口。
    /// 调用边界：必须绑定 level10/Alien，不允许跨关卡查找或运行时创建。
    /// </summary>
    [Header("共享装置")]
    [Tooltip("level10/Alien 上的共享猫猫夹子球组合入口。")]
    [SerializeField] private DrawAlienClipBallRigView rig;

    /// <summary>
    /// 球从夹子层级脱离后使用的稳定父节点。
    /// 调用边界：通常绑定 level10；为空时回退到装置父节点，再回退到本控制器。
    /// </summary>
    [Tooltip("蓝球释放后挂载的稳定关卡父节点。")]
    [SerializeField] private Transform ballReleaseParent;

    #endregion

    #region Entrance Settings

    /// <summary>
    /// 装置入场方向状态。
    /// 入场方向由关卡 Prefab 显式配置，DLevel_010 使用 FromTop。
    /// </summary>
    [Header("装置入场")]
    [Tooltip("装置入场方向；DLevel_010 使用 FromTop。")]
    [SerializeField] private RigEntranceState entranceState = RigEntranceState.FromTop;

    /// <summary>
    /// 装置开始移动前的等待时间。
    /// 调用边界：等待、移动、落球和双弹全部完成前不会开放画线。
    /// </summary>
    [Tooltip("装置开始移动前的延迟秒数。")]
    [SerializeField, Min(0f)] private float entranceDelay;

    /// <summary>
    /// 装置从上方起点移动到最终位置的持续时间。
    /// 注意事项：运行时最小按 0.01 秒处理。
    /// </summary>
    [Tooltip("装置从上方移动到最终位置的持续时间，单位为秒。")]
    [SerializeField, Min(MinimumDuration)] private float entranceDuration = 1.2f;

    /// <summary>
    /// 整体移动 OutBack 的过冲强度。
    /// 调用边界：只改变越过终点再回正的幅度，不改变最终位置。
    /// </summary>
    [Tooltip("装置越过目标后回正的 OutBack 过冲强度。")]
    [SerializeField, Min(0f)] private float entranceBackOvershoot = 1.9f;

    /// <summary>
    /// 球释放后猫猫与夹子沿入场路径退回起点的持续时间。
    /// 调用边界：退场与球的落地、双弹并行；两者都结束前不会开放画线。
    /// </summary>
    [Tooltip("球释放后猫猫与夹子原路退场的持续时间，单位为秒。")]
    [SerializeField, Min(MinimumDuration)] private float exitDuration = 0.8f;

    #endregion

    #region Landing Bounce Settings

    /// <summary>
    /// 释放后等待球接触场景碰撞面的最长时间。
    /// 注意事项：超时会记录错误并结束 Intro 门禁，避免输入永久锁死。
    /// </summary>
    [Header("落地双弹")]
    [Tooltip("等待球落地的最长秒数；超时后仍会安全开放画线。")]
    [SerializeField, Min(0.1f)] private float landingTimeout = 8f;

    /// <summary>
    /// 重开跳过完整 Intro 时，从正常放球点向下寻找首个有效落点的最大距离。
    /// 调用边界：只用于失败/手动重开，不改变首次进关的真实下落和双弹演出。
    /// </summary>
    [Tooltip("重开跳过 Intro 时，从放球点向下寻找球落点的最大世界距离。")]
    [SerializeField, Min(0.1f)] private float retryLandingSearchDistance = 20f;

    /// <summary>
    /// 判定球已落稳时允许的最大竖直速度。
    /// 调用边界：必须同时满足碰撞接触和连续稳定帧数才会开始双弹动画。
    /// </summary>
    [Tooltip("落地判定允许的最大竖直速度。")]
    [SerializeField, Min(0f)] private float landingVelocityThreshold = 0.35f;

    /// <summary>
    /// 第一次弹起的世界空间高度。
    /// 第二次固定使用该高度的 55%，形成衰减弹性。
    /// </summary>
    [Tooltip("球第一次弹起的世界空间高度。")]
    [SerializeField, Min(0f)] private float firstBounceHeight = 0.55f;

    /// <summary>
    /// 第一次完整起落的持续时间。
    /// 注意事项：第二次固定使用该时长的 80%，两次完成后动画停止。
    /// </summary>
    [Tooltip("球第一次完整起落的持续时间，单位为秒。")]
    [SerializeField, Min(MinimumDuration)] private float firstBounceDuration = 0.34f;

    /// <summary>
    /// 弹起时内容根的纵向拉伸比例。
    /// 调用边界：只影响 payloadContentRoot 的表现，动画结束后恢复缓存缩放。
    /// </summary>
    [Tooltip("球弹起时内容根的纵向拉伸比例。")]
    [SerializeField, Range(1f, 1.5f)] private float bounceStretchY = 1.12f;

    /// <summary>
    /// 球落回地面时内容根的纵向压缩比例。
    /// 调用边界：只影响 payloadContentRoot 的表现，不修改球刚体和碰撞参数。
    /// </summary>
    [Tooltip("球落地时内容根的纵向压缩比例。")]
    [SerializeField, Range(0.5f, 1f)] private float bounceSquashY = 0.86f;

    #endregion

    #region Runtime State

    private Vector3 _entranceStartPosition;
    private Vector3 _finalRigPosition;
    private Transform _initialBallParent;
    private Vector3 _initialBallLocalPosition;
    private Quaternion _initialBallLocalRotation;
    private Vector3 _initialBallLocalScale;
    private Vector2 _initialBallColliderOffset;
    private Vector3 _initialPayloadLocalPosition;
    private Quaternion _initialPayloadLocalRotation;
    private Vector3 _initialPayloadLocalScale;
    private bool _hasCachedInitialState;
    private bool _hasReleasedBall;
    private int _introVersion;
    private Tween _rigTween;
    private Tween _ballTween;
    private readonly RaycastHit2D[] _retryLandingHits =
        new RaycastHit2D[RetryLandingHitCapacity];

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        InitializeClipAttachment();
        CacheInitialState();
        RestoreInitialState();
    }

    private void OnDisable()
    {
        _introVersion++;
        _rigTween?.Kill();
        _rigTween = null;
        _ballTween?.Kill();
        _ballTween = null;
    }

    #endregion

    #region Draw Intro

    /// <summary>
    /// 播放配置方向入场、默认夹紧、放球开夹、猫猫夹子原路退场与落地双弹的完整序列。
    /// 调用边界：仅由当前 DLevel 的 DrawLevelCtrl 可见 Intro 链调用；返回后才允许画线。
    /// 注意事项：落地超时会安全结束门禁，避免碰撞配置异常造成永久不可操作。
    /// </summary>
    public async UniTask PlayDrawIntroAsync()
    {
        if (!Application.isPlaying)
            return;

        int introVersion = ++_introVersion;
        ResolveReferences();
        InitializeClipAttachment();
        CacheInitialState();
        RestoreInitialState();

        if (!ValidateReferences())
            return;

        _rigTween?.Kill();
        _ballTween?.Kill();
        Tween entranceTween = rig.AlienTransform
            .DOMove(
                _finalRigPosition,
                Mathf.Max(MinimumDuration, entranceDuration))
            .SetDelay(Mathf.Max(0f, entranceDelay))
            .SetEase(Ease.OutBack, Mathf.Max(0f, entranceBackOvershoot))
            .SetLink(gameObject)
            .OnUpdate(KeepBallHeld);
        _rigTween = entranceTween;

        await entranceTween.AsyncWaitForCompletion();
        if (!IsCurrentRigTween(introVersion, entranceTween))
            return;

        _rigTween = null;
        KeepBallHeld();
        PlayClipOpenAnimation();
        ReleaseBall();
        await UniTask.WhenAll(
            PlayRigExitAsync(introVersion),
            PlayBallReleasePerformanceAsync(introVersion));
    }

    /// <inheritdoc />
    public void ApplyDrawRetryIntroState()
    {
        if (!Application.isPlaying)
            return;

        _introVersion++;
        _rigTween?.Kill();
        _rigTween = null;
        _ballTween?.Kill();
        _ballTween = null;

        ResolveReferences();
        InitializeClipAttachment();
        CacheInitialState();
        RestoreInitialState();
        if (!ValidateReferences())
            return;

        rig.AlienTransform.position = _finalRigPosition;
        KeepBallHeld();
        PlayClipOpenAnimation();
        ReleaseBall();
        PlaceReleasedBallAtRetryLanding();

        // 完整 Intro 的最终状态中猫猫和夹子已经沿原路径退回场外。
        rig.AlienTransform.position = _entranceStartPosition;
    }

    #endregion

    #region Initial State

    private void CacheInitialState()
    {
        if (_hasCachedInitialState || rig == null)
            return;

        rig.ResolveReferences();
        Transform rigTransform = rig.AlienTransform;
        Rigidbody2D ballRigidbody = rig.YellowBallRigidbody;
        Transform payloadContentRoot = rig.PayloadContentRoot;
        DrawEntrancePathView entrancePath = rig.GetComponent<DrawEntrancePathView>();
        if (rigTransform == null ||
            ballRigidbody == null ||
            payloadContentRoot == null ||
            entrancePath == null ||
            !entrancePath.TryGetWorldPositions(
                out Vector3 configuredStartPosition,
                out _finalRigPosition))
        {
            return;
        }

        float entranceDistance = Vector3.Distance(
            configuredStartPosition,
            _finalRigPosition);
        _entranceStartPosition = ResolveDirectionalStartPosition(
            _finalRigPosition,
            entranceDistance);
        _initialBallParent = ballRigidbody.transform.parent;
        _initialBallLocalPosition = ballRigidbody.transform.localPosition;
        _initialBallLocalRotation = ballRigidbody.transform.localRotation;
        _initialBallLocalScale = ballRigidbody.transform.localScale;
        CircleCollider2D ballCollider =
            ballRigidbody.GetComponent<CircleCollider2D>();
        if (ballCollider != null)
            _initialBallColliderOffset = ballCollider.offset;
        _initialPayloadLocalPosition = payloadContentRoot.localPosition;
        _initialPayloadLocalRotation = payloadContentRoot.localRotation;
        _initialPayloadLocalScale = payloadContentRoot.localScale;
        _hasCachedInitialState = true;
    }

    private Vector3 ResolveDirectionalStartPosition(
        Vector3 finalPosition,
        float entranceDistance)
    {
        float distance = Mathf.Max(0f, entranceDistance);
        return entranceState switch
        {
            RigEntranceState.FromLeft => finalPosition + Vector3.left * distance,
            RigEntranceState.FromRight => finalPosition + Vector3.right * distance,
            RigEntranceState.FromTop => finalPosition + Vector3.up * distance,
            _ => finalPosition + Vector3.up * distance,
        };
    }

    private void RestoreInitialState()
    {
        _hasReleasedBall = false;
        PlayClipCloseAnimation();

        if (!_hasCachedInitialState)
            return;

        if (rig?.AlienTransform != null)
            rig.AlienTransform.position = _entranceStartPosition;

        Rigidbody2D ballRigidbody = rig?.YellowBallRigidbody;
        if (ballRigidbody != null)
        {
            ballRigidbody.bodyType = RigidbodyType2D.Kinematic;
            ballRigidbody.linearVelocity = Vector2.zero;
            ballRigidbody.angularVelocity = 0f;
            ballRigidbody.transform.SetParent(_initialBallParent, false);
            ballRigidbody.transform.localPosition = _initialBallLocalPosition;
            ballRigidbody.transform.localRotation = _initialBallLocalRotation;
            ballRigidbody.transform.localScale = _initialBallLocalScale;
        }

        RestoreBallColliderAndPayload();
    }

    private void KeepBallHeld()
    {
        if (!_hasCachedInitialState || _hasReleasedBall)
            return;

        Rigidbody2D ballRigidbody = rig?.YellowBallRigidbody;
        if (ballRigidbody == null)
            return;

        Transform ballTransform = ballRigidbody.transform;
        if (ballTransform.parent != _initialBallParent)
            ballTransform.SetParent(_initialBallParent, false);

        ballTransform.localPosition = _initialBallLocalPosition;
        ballTransform.localRotation = _initialBallLocalRotation;
        ballTransform.localScale = _initialBallLocalScale;
        RestoreBallColliderAndPayload();
    }

    private void RestoreBallColliderAndPayload()
    {
        CircleCollider2D ballCollider =
            rig?.YellowBallRigidbody?.GetComponent<CircleCollider2D>();
        if (ballCollider != null)
            ballCollider.offset = _initialBallColliderOffset;

        Transform payloadContentRoot = rig?.PayloadContentRoot;
        if (payloadContentRoot == null)
            return;

        payloadContentRoot.localPosition = _initialPayloadLocalPosition;
        payloadContentRoot.localRotation = _initialPayloadLocalRotation;
        payloadContentRoot.localScale = _initialPayloadLocalScale;
    }

    #endregion

    #region Clip And Ball

    private void InitializeClipAttachment()
    {
        if (rig == null)
            return;

        rig.ResolveReferences();
        BoneFollower boneFollower = rig.ClipBoneFollower;
        SkeletonAnimation alienSkeleton = rig.AlienSkeletonAnimation;
        if (boneFollower == null || alienSkeleton == null)
            return;

        if (!alienSkeleton.valid)
            alienSkeleton.Initialize(false);

        boneFollower.SkeletonRenderer = alienSkeleton;
        if (!boneFollower.valid)
        {
            this.LogError("[DLevel10Ctrl] 共享装置 BoneFollower 初始化失败。");
            return;
        }

        if (!boneFollower.SetBone(ClipAttachmentBone))
        {
            this.LogError(
                $"[DLevel10Ctrl] 共享装置 Alien Spine 缺少骨骼 '{ClipAttachmentBone}'。");
        }
    }

    private void PlayClipCloseAnimation()
    {
        PlayClipAnimation(ClipCloseAnimation);
    }

    private void PlayClipOpenAnimation()
    {
        PlayClipAnimation(ClipOpenAnimation);
    }

    private void PlayClipAnimation(string animationName)
    {
        SkeletonAnimation clipSkeleton = rig?.ClipSkeletonAnimation;
        if (clipSkeleton == null)
            return;

        if (!clipSkeleton.valid)
            clipSkeleton.Initialize(false);

        if (clipSkeleton.AnimationState == null ||
            clipSkeleton.Skeleton.Data.FindAnimation(animationName) == null)
        {
            this.LogError($"[DLevel10Ctrl] 共享夹子缺少 {animationName} 动画。");
            return;
        }

        clipSkeleton.AnimationState.SetAnimation(0, animationName, false);
    }

    private void ReleaseBall()
    {
        if (_hasReleasedBall)
            return;

        _hasReleasedBall = true;
        Rigidbody2D ballRigidbody = rig?.YellowBallRigidbody;
        if (ballRigidbody == null)
            return;

        Transform releaseParent = ballReleaseParent;
        if (releaseParent == null && rig.AlienTransform != null)
            releaseParent = rig.AlienTransform.parent;
        if (releaseParent == null)
            releaseParent = transform;

        ballRigidbody.bodyType = RigidbodyType2D.Kinematic;
        ballRigidbody.linearVelocity = Vector2.zero;
        ballRigidbody.angularVelocity = 0f;
        ballRigidbody.transform.SetParent(releaseParent, true);
        MoveMirrorFromRigidbodyToPayload();
        ballRigidbody.bodyType = RigidbodyType2D.Dynamic;
        ballRigidbody.WakeUp();
    }

    /// <summary>
    /// 重开时把刚释放的复合球刚体直接放到其下方首个实体碰撞面，跳过自由落体和双弹。
    /// 找不到落点时保持 Kinematic 并记录配置错误，避免黑幕期间继续下落或触发失败竞态。
    /// </summary>
    private void PlaceReleasedBallAtRetryLanding()
    {
        Rigidbody2D ballRigidbody = rig?.YellowBallRigidbody;
        if (ballRigidbody == null)
            return;

        ballRigidbody.bodyType = RigidbodyType2D.Kinematic;
        ballRigidbody.linearVelocity = Vector2.zero;
        ballRigidbody.angularVelocity = 0f;
        Physics2D.SyncTransforms();

        ContactFilter2D landingFilter = new()
        {
            useTriggers = false,
        };
        landingFilter.SetLayerMask(
            Physics2D.GetLayerCollisionMask(ballRigidbody.gameObject.layer));

        int hitCount = ballRigidbody.Cast(
            Vector2.down,
            landingFilter,
            _retryLandingHits,
            Mathf.Max(0.1f, retryLandingSearchDistance));
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _retryLandingHits[i];
            if (hit.collider == null || hit.distance < 0f)
                continue;

            Rigidbody2D hitRigidbody = hit.rigidbody;
            if (hitRigidbody == ballRigidbody ||
                (hitRigidbody != null && hitRigidbody.bodyType != RigidbodyType2D.Static))
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;
            if (hitTransform != transform && !hitTransform.IsChildOf(transform))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, hit.distance);
        }

        if (float.IsPositiveInfinity(nearestDistance))
        {
            this.LogError(
                "[DLevel10Ctrl] 重开时未找到蓝球下方的当前关卡静态落点，球保持 Kinematic。" +
                $" searchDistance:{retryLandingSearchDistance}");
            return;
        }

        ballRigidbody.position += Vector2.down * nearestDistance;
        Physics2D.SyncTransforms();

        ballRigidbody.bodyType = RigidbodyType2D.Dynamic;
        ballRigidbody.linearVelocity = Vector2.zero;
        ballRigidbody.angularVelocity = 0f;
        // 重开黑幕还会淡入 0.5 秒；先休眠可避免球在玩家看到前从圆面或斜面滚走。
        // Dynamic 刚体仍保留物理碰撞，后续线条或其它刚体接触会按 Unity 规则将其唤醒。
        ballRigidbody.Sleep();
    }

    /// <summary>
    /// 球体脱离且夹子播放开夹后，让猫猫与夹子沿原入场路径退回画面外。
    /// 注意事项：球已换到稳定关卡父节点，因此退场只移动装置，不会再次带走球。
    /// </summary>
    private async UniTask PlayRigExitAsync(int introVersion)
    {
        Transform rigTransform = rig?.AlienTransform;
        if (rigTransform == null || introVersion != _introVersion)
            return;

        Tween exitTween = rigTransform
            .DOMove(
                _entranceStartPosition,
                Mathf.Max(MinimumDuration, exitDuration))
            .SetEase(Ease.InBack, Mathf.Max(0f, entranceBackOvershoot))
            .SetLink(gameObject);
        _rigTween = exitTween;

        await exitTween.AsyncWaitForCompletion();
        if (!IsCurrentRigTween(introVersion, exitTween))
            return;

        _rigTween = null;
        rigTransform.position = _entranceStartPosition;
    }

    private async UniTask PlayBallReleasePerformanceAsync(int introVersion)
    {
        bool hasLanded = await WaitForBallLandingAsync(introVersion);
        if (!hasLanded || introVersion != _introVersion)
            return;

        await PlayDoubleBounceAsync(introVersion);
    }

    /// <summary>
    /// 把共享装置夹持阶段的负缩放从 Dynamic Rigidbody2D 根转移到内容根。
    /// 这样球体视觉保持原画面朝向，同时让进入物理模拟的刚体根使用正缩放。
    /// </summary>
    private void MoveMirrorFromRigidbodyToPayload()
    {
        Transform ballTransform = rig?.YellowBallRigidbody?.transform;
        Transform payloadContentRoot = rig?.PayloadContentRoot;
        if (ballTransform == null || payloadContentRoot == null)
            return;

        Vector3 releasedScale = ballTransform.localScale;
        bool mirrorX = releasedScale.x < 0f;
        bool mirrorY = releasedScale.y < 0f;
        if (!mirrorX && !mirrorY)
        {
            RestoreBallColliderAndPayload();
            return;
        }

        ballTransform.localScale = new Vector3(
            Mathf.Abs(releasedScale.x),
            Mathf.Abs(releasedScale.y),
            Mathf.Abs(releasedScale.z));

        Quaternion mirrorRotation = Quaternion.identity;
        if (mirrorX)
            mirrorRotation *= Quaternion.Euler(0f, 180f, 0f);
        if (mirrorY)
            mirrorRotation *= Quaternion.Euler(180f, 0f, 0f);

        CircleCollider2D ballCollider =
            rig.YellowBallRigidbody.GetComponent<CircleCollider2D>();
        if (ballCollider != null)
        {
            Vector2 mirroredOffset = _initialBallColliderOffset;
            if (mirrorX)
                mirroredOffset.x = -mirroredOffset.x;
            if (mirrorY)
                mirroredOffset.y = -mirroredOffset.y;
            ballCollider.offset = mirroredOffset;
        }

        payloadContentRoot.localPosition = _initialPayloadLocalPosition;
        payloadContentRoot.localRotation =
            _initialPayloadLocalRotation * mirrorRotation;
        payloadContentRoot.localScale = _initialPayloadLocalScale;
    }

    private async UniTask<bool> WaitForBallLandingAsync(int introVersion)
    {
        Rigidbody2D ballRigidbody = rig?.YellowBallRigidbody;
        Collider2D ballCollider = ballRigidbody != null
            ? ballRigidbody.GetComponent<Collider2D>()
            : null;
        if (ballRigidbody == null || ballCollider == null)
            return false;

        float elapsed = 0f;
        int stableFrames = 0;
        float timeout = Mathf.Max(0.1f, landingTimeout);
        float velocityThreshold = Mathf.Max(0f, landingVelocityThreshold);
        while (elapsed < timeout && introVersion == _introVersion)
        {
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
            if (ballRigidbody == null || ballCollider == null)
                return false;

            bool isTouching = ballCollider.IsTouchingLayers();
            bool isVerticallyStable =
                Mathf.Abs(ballRigidbody.linearVelocity.y) <= velocityThreshold;
            stableFrames = isTouching && isVerticallyStable
                ? stableFrames + 1
                : 0;
            if (stableFrames >= 2)
                return true;

            elapsed += Time.fixedDeltaTime;
        }

        if (introVersion == _introVersion)
        {
            this.LogError(
                "[DLevel10Ctrl] 蓝球在等待时间内未检测到稳定落地，已结束 Intro 门禁避免永久锁住画线。");
        }

        return false;
    }

    private async UniTask PlayDoubleBounceAsync(int introVersion)
    {
        Rigidbody2D ballRigidbody = rig?.YellowBallRigidbody;
        Transform payloadContentRoot = rig?.PayloadContentRoot;
        if (ballRigidbody == null || payloadContentRoot == null)
            return;

        ballRigidbody.bodyType = RigidbodyType2D.Kinematic;
        ballRigidbody.linearVelocity = Vector2.zero;
        ballRigidbody.angularVelocity = 0f;

        Transform ballTransform = ballRigidbody.transform;
        Vector3 landingPosition = ballTransform.position;
        Vector3 basePayloadScale = _initialPayloadLocalScale;
        float firstDuration = Mathf.Max(MinimumDuration, firstBounceDuration);
        float secondDuration = Mathf.Max(MinimumDuration, firstDuration * 0.8f);

        Sequence bounceSequence = DOTween.Sequence();
        AppendBounce(
            bounceSequence,
            ballTransform,
            payloadContentRoot,
            landingPosition.y,
            Mathf.Max(0f, firstBounceHeight),
            firstDuration,
            basePayloadScale);
        AppendBounce(
            bounceSequence,
            ballTransform,
            payloadContentRoot,
            landingPosition.y,
            Mathf.Max(0f, firstBounceHeight) * 0.55f,
            secondDuration,
            basePayloadScale);
        bounceSequence
            .Append(payloadContentRoot
                .DOScale(basePayloadScale, Mathf.Max(MinimumDuration, firstDuration * 0.2f))
                .SetEase(Ease.OutBack))
            .SetLink(gameObject);

        _ballTween = bounceSequence;
        await bounceSequence.AsyncWaitForCompletion();
        if (!IsCurrentBallTween(introVersion, bounceSequence))
            return;

        _ballTween = null;
        ballTransform.position = landingPosition;
        payloadContentRoot.localScale = basePayloadScale;
        ballRigidbody.bodyType = RigidbodyType2D.Dynamic;
        ballRigidbody.linearVelocity = Vector2.zero;
        ballRigidbody.angularVelocity = 0f;
        ballRigidbody.WakeUp();
    }

    private void AppendBounce(
        Sequence sequence,
        Transform ballTransform,
        Transform payloadContentRoot,
        float landingY,
        float bounceHeight,
        float duration,
        Vector3 basePayloadScale)
    {
        float halfDuration = Mathf.Max(MinimumDuration, duration * 0.5f);
        float stretchY = Mathf.Max(1f, bounceStretchY);
        float squashY = Mathf.Clamp(bounceSquashY, 0.01f, 1f);
        Vector3 stretchScale = new(
            basePayloadScale.x / Mathf.Sqrt(stretchY),
            basePayloadScale.y * stretchY,
            basePayloadScale.z);
        Vector3 squashScale = new(
            basePayloadScale.x / Mathf.Sqrt(squashY),
            basePayloadScale.y * squashY,
            basePayloadScale.z);

        sequence
            .Append(ballTransform
                .DOMoveY(landingY + bounceHeight, halfDuration)
                .SetEase(Ease.OutQuad))
            .Join(payloadContentRoot
                .DOScale(stretchScale, halfDuration)
                .SetEase(Ease.OutQuad))
            .Append(ballTransform
                .DOMoveY(landingY, halfDuration)
                .SetEase(Ease.InQuad))
            .Join(payloadContentRoot
                .DOScale(squashScale, halfDuration)
                .SetEase(Ease.InQuad));
    }

    #endregion

    #region Validation And Resolution

    private bool IsCurrentRigTween(int introVersion, Tween completedTween)
    {
        return introVersion == _introVersion && _rigTween == completedTween;
    }

    private bool IsCurrentBallTween(int introVersion, Tween completedTween)
    {
        return introVersion == _introVersion && _ballTween == completedTween;
    }

    private bool ValidateReferences()
    {
        if (rig == null ||
            !_hasCachedInitialState ||
            rig.AlienTransform == null ||
            rig.ClipBoneFollower == null ||
            rig.ClipSkeletonAnimation == null ||
            rig.YellowBallRigidbody == null ||
            rig.PayloadContentRoot == null)
        {
            this.LogError(
                "[DLevel10Ctrl] 共享猫猫夹子球装置、入场轨迹或内部引用缺失，无法播放完整 Intro。");
            return false;
        }

        return true;
    }

    private void ResolveReferences()
    {
        Transform levelRoot = transform.Find("level10");
        if (rig == null)
        {
            rig = levelRoot
                ?.Find("Alien")
                ?.GetComponent<DrawAlienClipBallRigView>();
        }

        if (ballReleaseParent == null)
            ballReleaseParent = levelRoot;

        rig?.ResolveReferences();
    }

    #endregion
}
