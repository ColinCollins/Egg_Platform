using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// DrawGame 第 6 关的双猫猫夹子、双球释放与镜像入场控制器。
/// 遮罩揭开后同时播放左右装置入场；第一笔有效画线完成后同时开夹、释放两球，
/// 双球碰撞后的冻结、爱心、镜头聚焦与延迟结算由通用 DrawTwoBallReunionSuccessHandle 承担。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 6 Ctrl")]
public sealed class DLevel6Ctrl : MonoBehaviour,
    IDrawLevelVisibleIntroHandler,
    IDrawStrokeCompletedHandler,
    IDebuger
{
    #region Constants

    private const string ClipAttachmentBone = "bone2";
    private const string ClipCloseAnimation = "clip_close";
    private const string ClipOpenAnimation = "clip_open";
    private const string AlienIdleAnimation = "idle01";
    private const string AlienControllerAnimation = "control";
    private const float MinimumEntranceDuration = 0.01f;

    #endregion

    #region Inspector References

    /// <summary>
    /// 从屏幕左侧进入的第一套猫猫、夹子与蓝球组合。
    /// 调用边界：必须绑定 DLevel_006/level6/Alien 上的共享组合预制体入口。
    /// </summary>
    [Header("左侧装置")]
    [Tooltip("从屏幕左侧进入的第一套猫猫、夹子与蓝球组合。")]
    [SerializeField] private DrawAlienClipBallRigView firstRig;

    /// <summary>
    /// 从屏幕右侧进入的第二套镜像猫猫、夹子与蓝球组合。
    /// 调用边界：必须绑定 DLevel_006/level6/Alien (1) 上的共享组合预制体入口。
    /// </summary>
    [Header("右侧装置")]
    [Tooltip("从屏幕右侧进入的第二套镜像猫猫、夹子与蓝球组合。")]
    [SerializeField] private DrawAlienClipBallRigView secondRig;

    /// <summary>
    /// 两个球释放后共同挂载的稳定关卡父节点。
    /// 调用边界：通常绑定 level6；为空时分别回退到装置父节点，再回退到本控制器节点。
    /// </summary>
    [Header("球释放")]
    [Tooltip("两个球脱离 Spine/BoneFollower 层级后挂载的稳定父节点。")]
    [SerializeField] private Transform ballReleaseParent;

    #endregion

    #region Entrance Settings

    /// <summary>
    /// 遮罩揭开后开始双装置入场前的等待时间。
    /// </summary>
    [Header("整体入场")]
    [Tooltip("遮罩揭开后开始入场前的等待时间，单位为秒。")]
    [SerializeField, Min(0f)] private float entranceDelay;

    /// <summary>
    /// 左右装置从各自屏幕外起点移动到终点的共同持续时间。
    /// </summary>
    [Tooltip("左右装置移动到各自终点的共同持续时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float entranceDuration = 0.78f;

    /// <summary>
    /// 整体移动 OutBack 的回弹强度。
    /// 注意事项：只影响装置根节点移动，不替代夹子的独立惯性摆动。
    /// </summary>
    [Tooltip("整体移动 OutBack 的回弹强度。")]
    [SerializeField, Min(0f)] private float entranceBackOvershoot = 1.9f;

    /// <summary>
    /// 入场开始时夹子朝前进反方向后摆的角度。
    /// 镜像实例会通过自身 Transform 镜像为相反的屏幕方向。
    /// </summary>
    [Header("夹子惯性摆动")]
    [Tooltip("入场开始时夹子朝前进反方向后摆的角度。")]
    [SerializeField, Range(-60f, 60f)] private float clipEntranceStartAngle = 38f;

    /// <summary>
    /// 夹子保持初始后摆姿态的时间。
    /// </summary>
    [Tooltip("夹子保持初始后摆姿态的时间，单位为秒。")]
    [SerializeField, Min(0f)] private float clipEntranceBackHoldDuration = 0.18f;

    /// <summary>
    /// 装置制动后夹子第一次向前摆的峰值绝对角度。
    /// </summary>
    [Tooltip("装置制动后夹子第一次向前摆的峰值绝对角度。")]
    [SerializeField, Range(0f, 60f)] private float clipEntranceForwardPeakAngle = 32f;

    /// <summary>
    /// 从初始后摆到第一次前摆峰值的持续时间。
    /// </summary>
    [Tooltip("从初始后摆到第一次前摆峰值的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceForwardSwingDuration = 0.25f;

    /// <summary>
    /// 第一次衰减后摆的峰值绝对角度。
    /// </summary>
    [Tooltip("第一次衰减后摆的峰值绝对角度。")]
    [SerializeField, Range(0f, 60f)] private float clipEntranceBackwardReboundAngle = 15f;

    /// <summary>
    /// 第一次衰减后摆的持续时间。
    /// </summary>
    [Tooltip("第一次衰减后摆的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceBackwardReboundDuration = 0.14f;

    /// <summary>
    /// 第二次衰减前摆的峰值绝对角度。
    /// </summary>
    [Tooltip("第二次衰减前摆的峰值绝对角度。")]
    [SerializeField, Range(0f, 60f)] private float clipEntranceForwardReboundAngle = 7f;

    /// <summary>
    /// 第二次衰减前摆的持续时间。
    /// </summary>
    [Tooltip("第二次衰减前摆的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceForwardReboundDuration = 0.11f;

    /// <summary>
    /// 停止前最后一次微弱后摆的峰值绝对角度。
    /// </summary>
    [Tooltip("停止前最后一次微弱后摆的峰值绝对角度。")]
    [SerializeField, Range(0f, 60f)] private float clipEntranceFinalBackwardAngle = 3f;

    /// <summary>
    /// 最后一次微弱后摆的持续时间。
    /// </summary>
    [Tooltip("最后一次微弱后摆的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceFinalBackwardDuration = 0.09f;

    /// <summary>
    /// 夹子从最后后摆峰值回到静止姿态的持续时间。
    /// </summary>
    [Tooltip("夹子完成衰减摆动并回正的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceSettleDuration = 0.08f;

    #endregion

    #region Runtime State

    private sealed class RigRuntimeState
    {
        public Vector3 EntranceStartPosition;
        public Vector3 FinalAlienPosition;
        public Transform InitialBallParent;
        public Vector3 InitialBallLocalPosition;
        public Quaternion InitialBallLocalRotation;
        public Vector3 InitialBallLocalScale;
        public Vector2 InitialBallColliderOffset;
        public Vector3 InitialPayloadContentLocalPosition;
        public Quaternion InitialPayloadContentLocalRotation;
        public Vector3 InitialPayloadContentLocalScale;
        public Quaternion InitialClipSwingLocalRotation;
        public bool HasCachedInitialState;
        public int AlienAnimationVersion;
    }

    private readonly RigRuntimeState _firstRigState = new();
    private readonly RigRuntimeState _secondRigState = new();
    private bool _hasReleasedBalls;
    private Tween _entranceTween;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        InitializeClipAttachment(firstRig, "第一套");
        InitializeClipAttachment(secondRig, "第二套");
        CacheInitialState();
        RestoreInitialState(false);
    }

    private void OnDisable()
    {
        _entranceTween?.Kill();
        _entranceTween = null;
        _firstRigState.AlienAnimationVersion++;
        _secondRigState.AlienAnimationVersion++;
    }

    #endregion

    #region Draw Level Handlers

    /// <summary>
    /// 遮罩完全揭开后，同时播放左右两套装置的镜像入场与夹子衰减摆动。
    /// 调用边界：由 DrawLevelCtrl 的可见 Intro 链调用；两个装置都收稳前不开放画线。
    /// </summary>
    public async UniTask PlayDrawIntroAsync()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        InitializeClipAttachment(firstRig, "第一套");
        InitializeClipAttachment(secondRig, "第二套");
        CacheInitialState();
        RestoreInitialState();

        if (!ValidateIntroReferences())
            return;

        _entranceTween?.Kill();

        float delay = Mathf.Max(0f, entranceDelay);
        float moveDuration = Mathf.Max(MinimumEntranceDuration, entranceDuration);
        float overshoot = Mathf.Max(0f, entranceBackOvershoot);

        Sequence entranceSequence = DOTween.Sequence();
        if (delay > Mathf.Epsilon)
            entranceSequence.AppendInterval(delay);

        entranceSequence
            .Insert(
                delay,
                firstRig.AlienTransform
                    .DOMove(_firstRigState.FinalAlienPosition, moveDuration)
                    .SetEase(Ease.OutBack, overshoot))
            .Insert(
                delay,
                secondRig.AlienTransform
                    .DOMove(_secondRigState.FinalAlienPosition, moveDuration)
                    .SetEase(Ease.OutBack, overshoot));

        InsertClipSwing(
            entranceSequence,
            delay,
            firstRig,
            _firstRigState);
        InsertClipSwing(
            entranceSequence,
            delay,
            secondRig,
            _secondRigState);

        entranceSequence
            .SetLink(gameObject)
            .OnUpdate(KeepBallsHeld);

        _entranceTween = entranceSequence;
        await entranceSequence.AsyncWaitForCompletion();
        if (_entranceTween != entranceSequence)
            return;

        _entranceTween = null;
        RestoreClipSwing(firstRig, _firstRigState);
        RestoreClipSwing(secondRig, _secondRigState);
        KeepBallsHeld();
    }

    /// <inheritdoc />
    public void ApplyDrawRetryIntroState()
    {
        if (!Application.isPlaying)
            return;

        _entranceTween?.Kill();
        _entranceTween = null;
        ResolveReferences();
        InitializeClipAttachment(firstRig, "第一套");
        InitializeClipAttachment(secondRig, "第二套");
        CacheInitialState();
        RestoreInitialState();

        if (!ValidateIntroReferences())
            return;

        firstRig.AlienTransform.position = _firstRigState.FinalAlienPosition;
        secondRig.AlienTransform.position = _secondRigState.FinalAlienPosition;
        RestoreClipSwing(firstRig, _firstRigState);
        RestoreClipSwing(secondRig, _secondRigState);
        KeepBallsHeld();
    }

    /// <summary>
    /// 第一笔有效画线完成后，同时播放两只猫的 control、两只夹子的 clip_open，
    /// 并让两个球脱离动画父级后进入 Dynamic 物理。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _hasReleasedBalls)
            return;

        ResolveReferences();
        if (!ValidateReleaseReferences())
            return;

        _hasReleasedBalls = true;

        OpenAndReleaseRig(firstRig, _firstRigState, "第一套");
        OpenAndReleaseRig(secondRig, _secondRigState, "第二套");
    }

    /// <summary>
    /// 当前尝试重置时停止未完成 Intro，并恢复两套装置、夹子、猫动画和球的夹持初态。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        ApplyDrawRetryIntroState();
    }

    #endregion

    #region Entrance Motion

    private void InsertClipSwing(
        Sequence entranceSequence,
        float insertionTime,
        DrawAlienClipBallRigView rig,
        RigRuntimeState state)
    {
        Transform swingPivot = rig?.ClipSwingPivot;
        if (swingPivot == null || !state.HasCachedInitialState)
            return;

        float backwardDirection = Mathf.Approximately(clipEntranceStartAngle, 0f)
            ? 1f
            : Mathf.Sign(clipEntranceStartAngle);
        swingPivot.localRotation = CreateClipSwingRotation(
            state,
            clipEntranceStartAngle);

        Sequence swingSequence = DOTween.Sequence();
        float holdDuration = Mathf.Max(0f, clipEntranceBackHoldDuration);
        if (holdDuration > Mathf.Epsilon)
            swingSequence.AppendInterval(holdDuration);

        swingSequence
            .Append(CreateClipSwingTween(
                swingPivot,
                state,
                -backwardDirection * Mathf.Abs(clipEntranceForwardPeakAngle),
                clipEntranceForwardSwingDuration))
            .Append(CreateClipSwingTween(
                swingPivot,
                state,
                backwardDirection * Mathf.Abs(clipEntranceBackwardReboundAngle),
                clipEntranceBackwardReboundDuration))
            .Append(CreateClipSwingTween(
                swingPivot,
                state,
                -backwardDirection * Mathf.Abs(clipEntranceForwardReboundAngle),
                clipEntranceForwardReboundDuration))
            .Append(CreateClipSwingTween(
                swingPivot,
                state,
                backwardDirection * Mathf.Abs(clipEntranceFinalBackwardAngle),
                clipEntranceFinalBackwardDuration))
            .Append(CreateClipSwingTween(
                swingPivot,
                state,
                0f,
                clipEntranceSettleDuration));

        entranceSequence.Insert(insertionTime, swingSequence);
    }

    private static Quaternion CreateClipSwingRotation(
        RigRuntimeState state,
        float localZAngle)
    {
        return state.InitialClipSwingLocalRotation
            * Quaternion.Euler(0f, 0f, localZAngle);
    }

    private static Tween CreateClipSwingTween(
        Transform swingPivot,
        RigRuntimeState state,
        float localZAngle,
        float duration)
    {
        return swingPivot
            .DOLocalRotateQuaternion(
                CreateClipSwingRotation(state, localZAngle),
                Mathf.Max(MinimumEntranceDuration, duration))
            .SetEase(Ease.InOutSine);
    }

    private static void RestoreClipSwing(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state)
    {
        if (rig?.ClipSwingPivot != null && state.HasCachedInitialState)
            rig.ClipSwingPivot.localRotation = state.InitialClipSwingLocalRotation;
    }

    #endregion

    #region Initial State

    private void CacheInitialState()
    {
        CacheRigInitialState(firstRig, _firstRigState);
        CacheRigInitialState(secondRig, _secondRigState);
    }

    private static void CacheRigInitialState(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state)
    {
        if (state.HasCachedInitialState ||
            rig == null ||
            rig.YellowBallRigidbody == null)
        {
            return;
        }

        DrawEntrancePathView entrancePath =
            rig.AlienTransform.GetComponent<DrawEntrancePathView>();
        if (entrancePath == null ||
            !entrancePath.TryGetWorldPositions(
                out state.EntranceStartPosition,
                out state.FinalAlienPosition))
        {
            return;
        }

        Transform ballTransform = rig.YellowBallRigidbody.transform;
        state.InitialBallParent = ballTransform.parent;
        state.InitialBallLocalPosition = ballTransform.localPosition;
        state.InitialBallLocalRotation = ballTransform.localRotation;
        state.InitialBallLocalScale = ballTransform.localScale;
        CircleCollider2D ballCollider = rig.YellowBallRigidbody.GetComponent<CircleCollider2D>();
        if (ballCollider != null)
            state.InitialBallColliderOffset = ballCollider.offset;
        Transform payloadContentRoot = rig.PayloadContentRoot;
        if (payloadContentRoot != null)
        {
            state.InitialPayloadContentLocalPosition = payloadContentRoot.localPosition;
            state.InitialPayloadContentLocalRotation = payloadContentRoot.localRotation;
            state.InitialPayloadContentLocalScale = payloadContentRoot.localScale;
        }
        state.InitialClipSwingLocalRotation = rig.ClipSwingPivot != null
            ? rig.ClipSwingPivot.localRotation
            : Quaternion.identity;
        state.HasCachedInitialState = true;
    }

    private void RestoreInitialState(bool playIdleAnimation = true)
    {
        _hasReleasedBalls = false;
        RestoreRigInitialState(
            firstRig,
            _firstRigState,
            "第一套",
            playIdleAnimation);
        RestoreRigInitialState(
            secondRig,
            _secondRigState,
            "第二套",
            playIdleAnimation);
    }

    private void RestoreRigInitialState(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state,
        string rigName,
        bool playIdleAnimation)
    {
        PlayClipAnimation(rig, ClipCloseAnimation, false, rigName);

        if (!state.HasCachedInitialState)
        {
            if (playIdleAnimation)
                PlayAlienIdleAnimation(rig, state, rigName);
            return;
        }

        if (rig?.AlienTransform != null)
            rig.AlienTransform.position = state.EntranceStartPosition;

        RestoreClipSwing(rig, state);

        Rigidbody2D ballRigidbody = rig?.YellowBallRigidbody;
        if (ballRigidbody != null)
        {
            ballRigidbody.bodyType = RigidbodyType2D.Kinematic;
            ballRigidbody.linearVelocity = Vector2.zero;
            ballRigidbody.angularVelocity = 0f;
            ballRigidbody.transform.SetParent(state.InitialBallParent, false);
            ballRigidbody.transform.localPosition = state.InitialBallLocalPosition;
            ballRigidbody.transform.localRotation = state.InitialBallLocalRotation;
            ballRigidbody.transform.localScale = state.InitialBallLocalScale;
            RestoreBallColliderOffset(rig, state);
            RestorePayloadContent(rig, state);
        }

        if (playIdleAnimation)
            PlayAlienIdleAnimation(rig, state, rigName);
    }

    private void KeepBallsHeld()
    {
        if (_hasReleasedBalls)
            return;

        KeepBallHeld(firstRig, _firstRigState);
        KeepBallHeld(secondRig, _secondRigState);
    }

    private static void KeepBallHeld(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state)
    {
        Rigidbody2D ballRigidbody = rig?.YellowBallRigidbody;
        if (!state.HasCachedInitialState || ballRigidbody == null)
            return;

        Transform ballTransform = ballRigidbody.transform;
        if (ballTransform.parent != state.InitialBallParent)
            ballTransform.SetParent(state.InitialBallParent, false);

        ballTransform.localPosition = state.InitialBallLocalPosition;
        ballTransform.localRotation = state.InitialBallLocalRotation;
        ballTransform.localScale = state.InitialBallLocalScale;
        RestoreBallColliderOffset(rig, state);
        RestorePayloadContent(rig, state);
    }

    private static void RestoreBallColliderOffset(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state)
    {
        CircleCollider2D ballCollider =
            rig?.YellowBallRigidbody?.GetComponent<CircleCollider2D>();
        if (ballCollider != null && state.HasCachedInitialState)
            ballCollider.offset = state.InitialBallColliderOffset;
    }

    private static void RestorePayloadContent(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state)
    {
        Transform payloadContentRoot = rig?.PayloadContentRoot;
        if (payloadContentRoot == null || !state.HasCachedInitialState)
            return;

        payloadContentRoot.localPosition = state.InitialPayloadContentLocalPosition;
        payloadContentRoot.localRotation = state.InitialPayloadContentLocalRotation;
        payloadContentRoot.localScale = state.InitialPayloadContentLocalScale;
    }

    #endregion

    #region Release

    private void OpenAndReleaseRig(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state,
        string rigName)
    {
        PlayClipAnimation(rig, ClipOpenAnimation, false, rigName);
        ReleaseBall(rig, state, rigName);
        PlayAlienControllerAnimation(rig, state, rigName);
    }

    private void ReleaseBall(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state,
        string rigName)
    {
        Rigidbody2D ballRigidbody = rig?.YellowBallRigidbody;
        if (ballRigidbody == null)
        {
            this.LogError($"[DLevel6Ctrl] {rigName} yellow_ball Rigidbody2D 缺失，无法释放。");
            return;
        }

        Transform releaseParent = ballReleaseParent;
        if (releaseParent == null && rig.AlienTransform != null)
            releaseParent = rig.AlienTransform.parent;
        if (releaseParent == null)
            releaseParent = transform;

        ballRigidbody.bodyType = RigidbodyType2D.Kinematic;
        ballRigidbody.linearVelocity = Vector2.zero;
        ballRigidbody.angularVelocity = 0f;
        ballRigidbody.transform.SetParent(releaseParent, true);
        MoveMirrorFromRigidbodyToPayload(rig, state);
        ballRigidbody.bodyType = RigidbodyType2D.Dynamic;
        ballRigidbody.WakeUp();
    }

    /// <summary>
    /// 将镜像实例在挂载期继承到球根的负缩放转移到内容根。
    /// 这样球、Actor 和子碰撞体保持原画面朝向，同时 Dynamic Rigidbody2D 根使用正缩放。
    /// </summary>
    private static void MoveMirrorFromRigidbodyToPayload(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state)
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
            RestorePayloadContent(rig, state);
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
            Vector2 mirroredOffset = state.InitialBallColliderOffset;
            if (mirrorX)
                mirroredOffset.x = -mirroredOffset.x;
            if (mirrorY)
                mirroredOffset.y = -mirroredOffset.y;
            ballCollider.offset = mirroredOffset;
        }

        payloadContentRoot.localPosition = state.InitialPayloadContentLocalPosition;
        payloadContentRoot.localRotation =
            state.InitialPayloadContentLocalRotation * mirrorRotation;
        payloadContentRoot.localScale = state.InitialPayloadContentLocalScale;
    }

    #endregion

    #region Clip Spine

    private void InitializeClipAttachment(
        DrawAlienClipBallRigView rig,
        string rigName)
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
            this.LogError($"[DLevel6Ctrl] {rigName} BoneFollower 初始化失败。");
            return;
        }

        if (!boneFollower.SetBone(ClipAttachmentBone))
        {
            this.LogError(
                $"[DLevel6Ctrl] {rigName} Alien Spine 缺少骨骼 '{ClipAttachmentBone}'。");
        }
    }

    private void PlayClipAnimation(
        DrawAlienClipBallRigView rig,
        string animationName,
        bool loop,
        string rigName)
    {
        SkeletonAnimation clipSkeleton = rig?.ClipSkeletonAnimation;
        if (clipSkeleton == null)
        {
            this.LogError($"[DLevel6Ctrl] {rigName} Clip SkeletonAnimation 缺失。");
            return;
        }

        if (!clipSkeleton.valid)
            clipSkeleton.Initialize(false);

        if (clipSkeleton.AnimationState == null)
        {
            this.LogError($"[DLevel6Ctrl] {rigName} Clip Spine 未初始化。");
            return;
        }

        if (clipSkeleton.Skeleton.Data.FindAnimation(animationName) == null)
        {
            this.LogError(
                $"[DLevel6Ctrl] {rigName} 缺少夹子动画 '{animationName}'。");
            return;
        }

        clipSkeleton.AnimationState.SetAnimation(0, animationName, loop);
    }

    #endregion

    #region Alien Animation

    private void PlayAlienControllerAnimation(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state,
        string rigName)
    {
        ActorSpineCtrl spineCtrl = rig?.AlienSpineCtrl;
        if (spineCtrl == null)
        {
            this.LogError($"[DLevel6Ctrl] {rigName} Alien ActorSpineCtrl 缺失。");
            return;
        }

        int animationVersion = ++state.AlienAnimationVersion;
        Spine.TrackEntry entry = spineCtrl.PlayAnimation(
            AlienControllerAnimation,
            false,
            false);
        if (entry == null)
        {
            this.LogError(
                $"[DLevel6Ctrl] {rigName} control 播放失败，恢复 idle01。");
            PlayAlienIdleAnimation(rig, state, rigName);
            return;
        }

        entry.Complete += _ =>
        {
            if (animationVersion != state.AlienAnimationVersion)
                return;

            PlayAlienIdleAnimation(rig, state, rigName);
        };
    }

    private void PlayAlienIdleAnimation(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state,
        string rigName)
    {
        state.AlienAnimationVersion++;
        ActorSpineCtrl spineCtrl = rig?.AlienSpineCtrl;
        if (spineCtrl == null)
        {
            this.LogError($"[DLevel6Ctrl] {rigName} Alien ActorSpineCtrl 缺失。");
            return;
        }

        spineCtrl.PlayAnimation(AlienIdleAnimation, true, false);
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        Transform levelRoot = transform.Find("level6");
        if (levelRoot == null)
            return;

        if (firstRig == null)
        {
            firstRig = levelRoot
                .Find("Alien")
                ?.GetComponent<DrawAlienClipBallRigView>();
        }

        if (secondRig == null)
        {
            secondRig = levelRoot
                .Find("Alien (1)")
                ?.GetComponent<DrawAlienClipBallRigView>();
        }

        if (ballReleaseParent == null)
            ballReleaseParent = levelRoot;

        firstRig?.ResolveReferences();
        secondRig?.ResolveReferences();
    }

    private bool ValidateIntroReferences()
    {
        bool isValid = true;
        if (firstRig == null || !_firstRigState.HasCachedInitialState)
        {
            this.LogError(
                "[DLevel6Ctrl] 第一套装置、入场轨迹或夹持球引用缺失，无法播放双装置入场。");
            isValid = false;
        }

        if (secondRig == null || !_secondRigState.HasCachedInitialState)
        {
            this.LogError(
                "[DLevel6Ctrl] 第二套装置、入场轨迹或夹持球引用缺失，无法播放双装置入场。");
            isValid = false;
        }

        return isValid;
    }

    private bool ValidateReleaseReferences()
    {
        bool firstValid = ValidateRigReleaseReferences(
            firstRig,
            _firstRigState,
            "第一套");
        bool secondValid = ValidateRigReleaseReferences(
            secondRig,
            _secondRigState,
            "第二套");
        return firstValid && secondValid;
    }

    private bool ValidateRigReleaseReferences(
        DrawAlienClipBallRigView rig,
        RigRuntimeState state,
        string rigName)
    {
        if (rig == null ||
            !state.HasCachedInitialState ||
            rig.YellowBallRigidbody == null ||
            rig.PayloadContentRoot == null)
        {
            this.LogError(
                $"[DLevel6Ctrl] {rigName} 共享装置、缓存初态、球刚体或 payloadContentRoot 缺失，取消双球释放。");
            return false;
        }

        return true;
    }

    #endregion
}
