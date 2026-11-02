using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEditor;
#endif

/// <summary>
/// 单套 Alien 夹子球装置的可见入场门禁与首笔完成演出控制器。
/// 架子在夹持阶段跟随 Alien Spine 的 bone2；首笔完成后，球先脱离动画层级再进入物理模拟。
/// 镜像装置释放时会把负缩放从 Dynamic Rigidbody2D 根转移到 payloadContentRoot。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 2 Ctrl")]
public class DLevel2Ctrl : MonoBehaviour,
    IDrawLevelVisibleIntroHandler,
    IDrawStrokeCompletedHandler,
    IDebuger
{
    #region Constants

    private const string AlienIdleAnimation = "idle01";
    private const string AlienControllerAnimation = "control";
    private const string ClipCloseAnimation = "clip_close";
    private const string ClipOpenAnimation = "clip_open";
    private const string ClipAttachmentBone = "bone2";
    private const float MinimumEntranceDuration = 0.01f;

    #endregion

    #region Inspector

    /// <summary>
    /// 当前 DLevel 根节点上的通用状态控制器。
    /// 调用边界：必须指向同一关卡；本脚本不通过该引用访问跨关卡状态。
    /// </summary>
    [Header("关卡引用")]
    [Tooltip("当前 DLevel 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 猫、架子和球共同入场的 Alien 根节点。
    /// 摆放契约：Prefab 中该根节点的位置就是最终位置，起点由同节点上的 DrawEntrancePathView 提供。
    /// </summary>
    [Tooltip("猫、架子和球共同飞入的 Alien 根节点。")]
    [SerializeField] private Transform alien;

    /// <summary>
    /// Alien 开始入场前的等待时间。
    /// 注意事项：等待和后续回弹全部结束前，Draw intro 门禁保持关闭。
    /// </summary>
    [Header("入场参数")]
    [Tooltip("Alien 开始移动前的延迟秒数。")]
    [SerializeField, Min(0f)] private float entranceDelay;

    /// <summary>
    /// Alien 从 entranceStartPoint 移动到根节点所配置最终位置的持续时间。
    /// 注意事项：运行时最小按 0.01 秒处理，不包含架子惯性摆超过移动时长的剩余部分。
    /// </summary>
    [Tooltip("Alien 从入场起点飞到最终位置的持续时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float entranceDuration = 0.78f;

    /// <summary>
    /// Alien 使用 OutBack 缓动时的过冲幅度。
    /// 调用边界：只改变本关入场曲线，不改变最终坐标或其它关卡。
    /// </summary>
    [Tooltip("Alien OutBack 的过冲幅度；数值越大，整体回弹越明显。")]
    [SerializeField, Range(0f, 4f)] private float entranceBackOvershoot = 1.9f;

    /// <summary>
    /// Intro 首帧相对静止姿态施加到 clipSwingPivot 的局部 Z 轴预倾角。
    /// 注意事项：球在夹持阶段随该节点一起倾斜，Reset 会恢复静止姿态。
    /// </summary>
    [Tooltip("架子和球进入画面前的初始局部倾角。")]
    [SerializeField, Range(-60f, 60f)] private float clipEntranceStartAngle = 38f;

    /// <summary>
    /// 架子随 Alien 起飞后保持后摆姿态的时间。
    /// 调用边界：只形成起飞时的惯性拖后感，不影响 Alien 的移动时长。
    /// </summary>
    [Tooltip("架子起飞后保持向后倾斜的时间，单位为秒。")]
    [SerializeField, Min(0f)] private float clipEntranceBackHoldDuration = 0.18f;

    /// <summary>
    /// Alien 制动时架子向前摆到的第一峰值角度绝对值。
    /// 注意事项：运行时自动使用初始后摆角的反方向，Inspector 只填写正数幅度。
    /// </summary>
    [Tooltip("架子因制动惯性向前摆动的峰值角度。")]
    [SerializeField, Range(0f, 60f)] private float clipEntranceForwardPeakAngle = 32f;

    /// <summary>
    /// 架子从初始后摆姿态运动到向前峰值的持续时间。
    /// 调用边界：与后摆保持时间共同决定向前峰值落在整体制动阶段。
    /// </summary>
    [Tooltip("架子从后摆运动到向前峰值的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceForwardSwingDuration = 0.25f;

    /// <summary>
    /// 第一次反向回摆的角度绝对值。
    /// 注意事项：方向与初始后摆一致，幅度应小于向前峰值以形成衰减。
    /// </summary>
    [Tooltip("架子第一次向后回摆的峰值角度。")]
    [SerializeField, Range(0f, 60f)] private float clipEntranceBackwardReboundAngle = 15f;

    /// <summary>
    /// 第一次反向回摆的持续时间。
    /// 调用边界：只驱动 clipSwingPivot，不改变球的物理状态。
    /// </summary>
    [Tooltip("架子第一次向后回摆的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceBackwardReboundDuration = 0.14f;

    /// <summary>
    /// 第二次向前回摆的角度绝对值。
    /// 注意事项：方向与初始后摆相反，幅度应继续小于前一峰值。
    /// </summary>
    [Tooltip("架子第二次向前回摆的峰值角度。")]
    [SerializeField, Range(0f, 60f)] private float clipEntranceForwardReboundAngle = 7f;

    /// <summary>
    /// 第二次向前回摆的持续时间。
    /// 调用边界：Intro 会等待完整衰减序列结束后才开放画线。
    /// </summary>
    [Tooltip("架子第二次向前回摆的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceForwardReboundDuration = 0.11f;

    /// <summary>
    /// 最后一次微弱后摆的角度绝对值。
    /// 注意事项：方向与初始后摆一致，随后回到缓存的静止姿态。
    /// </summary>
    [Tooltip("架子停止前最后一次向后回摆的峰值角度。")]
    [SerializeField, Range(0f, 60f)] private float clipEntranceFinalBackwardAngle = 3f;

    /// <summary>
    /// 最后一次微弱后摆的持续时间。
    /// 调用边界：只属于可见 Intro，不参与首笔完成后的开夹动画。
    /// </summary>
    [Tooltip("架子最后一次向后回摆的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceFinalBackwardDuration = 0.09f;

    /// <summary>
    /// 架子从最后后摆峰值回到正位的持续时间。
    /// 注意事项：该段结束后 Intro 才完成，最终会再次强制恢复缓存姿态。
    /// </summary>
    [Tooltip("架子完成衰减摆动并回正的时间，单位为秒。")]
    [SerializeField, Min(MinimumEntranceDuration)] private float clipEntranceSettleDuration = 0.08f;

    /// <summary>
    /// 被架子夹持并在首笔完成后释放的球刚体。
    /// 注意事项：Intro 和夹持阶段保持 Kinematic，脱离动画父级后才切换为 Dynamic。
    /// </summary>
    [Header("夹持球")]
    [Tooltip("架子夹持的 yellow_ball Rigidbody2D。")]
    [SerializeField] private Rigidbody2D yellowBallRigidbody;

    /// <summary>
    /// 球释放后使用的稳定场景父节点。
    /// 调用边界：为空时回退到 Alien 的父节点，再回退到当前控制器节点。
    /// </summary>
    [Tooltip("球脱离 Spine 动画层级后挂载的稳定父节点。")]
    [SerializeField] private Transform ballReleaseParent;

    /// <summary>
    /// Alien 根节点上的动画控制器。
    /// 调用边界：Intro 播放 idle01；首笔完成播放一次 control，完成后恢复 idle01。
    /// </summary>
    [Header("Alien Spine")]
    [Tooltip("Alien 根节点上的 ActorSpineCtrl。")]
    [SerializeField] private ActorSpineCtrl alienSpineCtrl;

    /// <summary>
    /// Alien 根节点上的 Spine 实例。
    /// 调用边界：只供 BoneFollower 初始化和 bone2 绑定使用。
    /// </summary>
    [Tooltip("提供 bone2 挂点的 Alien SkeletonAnimation。")]
    [SerializeField] private SkeletonAnimation alienSkeletonAnimation;

    /// <summary>
    /// 把架子根节点绑定到 Alien bone2 的 Spine BoneFollower。
    /// 注意事项：只跟随骨骼姿态；额外甩动必须施加在其子节点 clipSwingPivot。
    /// </summary>
    [Header("架子 Spine")]
    [Tooltip("跟随 Alien bone2 的 BoneFollower。")]
    [SerializeField] private BoneFollower clipBoneFollower;

    /// <summary>
    /// 架子视觉与夹持球共用的局部甩动父节点。
    /// 调用边界：Intro 只修改该节点的局部旋转，完成或 Reset 后恢复缓存姿态。
    /// </summary>
    [Tooltip("承载架子视觉和夹持球的入场甩动节点。")]
    [SerializeField] private Transform clipSwingPivot;

    /// <summary>
    /// 架子 Spine 实例。
    /// 调用边界：只播放 clip_close 与 clip_open，不承担球的物理切换。
    /// </summary>
    [Tooltip("播放 clip_close / clip_open 的 SkeletonAnimation。")]
    [SerializeField] private SkeletonAnimation clipSkeletonAnimation;

    #endregion

    #region Runtime State

    private Vector3 _entranceStartPosition;
    private Vector3 _finalAlienPosition;
    private Transform _initialBallParent;
    private Vector3 _initialBallLocalPosition;
    private Quaternion _initialBallLocalRotation;
    private Vector3 _initialBallLocalScale;
    private Vector2 _initialBallColliderOffset;
    private Vector3 _initialPayloadContentLocalPosition;
    private Quaternion _initialPayloadContentLocalRotation;
    private Vector3 _initialPayloadContentLocalScale;
    private Quaternion _initialClipSwingLocalRotation;
    private DrawAlienClipBallRigView _rigView;
    private bool _hasCachedInitialState;
    private bool _hasCachedPayloadState;
    private bool _hasReleasedBall;
    private int _alienAnimationVersion;
    private Tween _entranceTween;

    #endregion

    #region Editor Tools

#if UNITY_EDITOR
    /// <summary>
    /// 用途：按Inspector中指定的Alien，自动绑定本关主控、猫动画、夹子与夹持球引用。
    /// 调用边界：仅编辑模式手动点击；优先使用Rig View引用，缺失时按同一Alien的标准层级查找。
    /// 注意事项：重新指定Alien后会覆盖装置引用，释放父节点只补空值；支持Undo和Prefab Override，
    /// 不创建组件、不初始化Spine、不播放动画，也不改变球的物理状态。
    /// </summary>
    [Button("自动绑定 Alien 引用")]
    [EnableIf("@!UnityEngine.Application.isPlaying")]
    public void AutoBindAlienReferences()
    {
        if (Application.isPlaying)
            return;

        if (alien == null)
        {
            Debug.LogWarning("[DLevel2Ctrl] 请先在Inspector中指定Alien，再自动绑定引用。", this);
            return;
        }

        Undo.RecordObject(this, "自动绑定 Alien 引用");
        drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);
        _rigView = alien.GetComponent<DrawAlienClipBallRigView>();
        Transform clipRoot = alien.Find("catch");

        alienSpineCtrl = _rigView != null ? _rigView.AlienSpineCtrl : null;
        if (alienSpineCtrl == null)
            alienSpineCtrl = alien.GetComponent<ActorSpineCtrl>();

        alienSkeletonAnimation = _rigView != null ? _rigView.AlienSkeletonAnimation : null;
        if (alienSkeletonAnimation == null)
            alienSkeletonAnimation = alien.GetComponent<SkeletonAnimation>();

        clipBoneFollower = _rigView != null ? _rigView.ClipBoneFollower : null;
        if (clipBoneFollower == null && clipRoot != null)
            clipBoneFollower = clipRoot.GetComponent<BoneFollower>();

        clipSwingPivot = _rigView != null ? _rigView.ClipSwingPivot : null;
        if (clipSwingPivot == null && clipRoot != null)
            clipSwingPivot = clipRoot.Find("clipSwingPivot");

        Transform clipVisual = clipSwingPivot != null ? clipSwingPivot.Find("clipVisual") : null;
        clipSkeletonAnimation = _rigView != null ? _rigView.ClipSkeletonAnimation : null;
        if (clipSkeletonAnimation == null && clipVisual != null)
            clipSkeletonAnimation = clipVisual.GetComponent<SkeletonAnimation>();

        Transform ball = clipSwingPivot != null ? clipSwingPivot.Find("yellow_ball") : null;
        yellowBallRigidbody = _rigView != null ? _rigView.YellowBallRigidbody : null;
        if (yellowBallRigidbody == null && ball != null)
            yellowBallRigidbody = ball.GetComponent<Rigidbody2D>();

        if (ballReleaseParent == null)
            ballReleaseParent = alien.parent != null ? alien.parent : transform;

        EditorUtility.SetDirty(this);
        if (PrefabUtility.IsPartOfPrefabInstance(this))
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);

        if (drawLevelCtrl == null || alienSpineCtrl == null || alienSkeletonAnimation == null ||
            clipBoneFollower == null || clipSwingPivot == null || clipSkeletonAnimation == null ||
            yellowBallRigidbody == null)
        {
            Debug.LogWarning("[DLevel2Ctrl] 自动绑定后仍有缺失引用，请核对本关主控和Alien装置结构。", this);
        }
    }
#endif

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        InitializeClipAttachment();
        CacheInitialState();
        RestoreInitialState(false);
    }

    protected virtual void OnDisable()
    {
        _entranceTween?.Kill();
        _entranceTween = null;
        _alienAnimationVersion++;
    }

    #endregion

    #region Draw Level Handlers

    /// <summary>
    /// 在转场遮罩完全揭开后播放本关可见 Intro。
    /// 调用边界：由 DrawLevelCtrl 的 Intro 链调用；整个 Sequence 完成前不开放画线。
    /// </summary>
    public async UniTask PlayDrawIntroAsync()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        InitializeClipAttachment();
        CacheInitialState();
        RestoreInitialState();

        if (!_hasCachedInitialState)
        {
            this.LogError(
                "[DLevel2Ctrl] Alien、DrawEntrancePathView、entranceStartPoint 或夹持球引用缺失，无法播放入场。");
            EnableHeldBallSimulation();
            return;
        }

        _entranceTween?.Kill();

        float delay = Mathf.Max(0f, entranceDelay);
        float moveDuration = Mathf.Max(MinimumEntranceDuration, entranceDuration);
        Sequence entranceSequence = DOTween.Sequence()
            .AppendInterval(delay)
            .Append(alien
                .DOMove(_finalAlienPosition, moveDuration)
                .SetEase(
                    Ease.OutBack,
                    Mathf.Max(0f, entranceBackOvershoot)));

        if (clipSwingPivot != null)
        {
            float backwardDirection = Mathf.Approximately(
                clipEntranceStartAngle,
                0f)
                ? 1f
                : Mathf.Sign(clipEntranceStartAngle);
            clipSwingPivot.localRotation = CreateClipSwingRotation(
                clipEntranceStartAngle);

            Sequence clipSwingSequence = DOTween.Sequence();
            float backHoldDuration = Mathf.Max(0f, clipEntranceBackHoldDuration);
            if (backHoldDuration > Mathf.Epsilon)
                clipSwingSequence.AppendInterval(backHoldDuration);

            clipSwingSequence
                .Append(CreateClipSwingTween(
                    -backwardDirection * Mathf.Abs(clipEntranceForwardPeakAngle),
                    clipEntranceForwardSwingDuration))
                .Append(CreateClipSwingTween(
                    backwardDirection * Mathf.Abs(clipEntranceBackwardReboundAngle),
                    clipEntranceBackwardReboundDuration))
                .Append(CreateClipSwingTween(
                    -backwardDirection * Mathf.Abs(clipEntranceForwardReboundAngle),
                    clipEntranceForwardReboundDuration))
                .Append(CreateClipSwingTween(
                    backwardDirection * Mathf.Abs(clipEntranceFinalBackwardAngle),
                    clipEntranceFinalBackwardDuration))
                .Append(CreateClipSwingTween(0f, clipEntranceSettleDuration));

            entranceSequence.Insert(delay, clipSwingSequence);
        }

        entranceSequence
            .SetLink(gameObject)
            .OnUpdate(KeepBallHeld);

        _entranceTween = entranceSequence;
        await entranceSequence.AsyncWaitForCompletion();
        if (_entranceTween != entranceSequence)
            return;

        _entranceTween = null;
        if (clipSwingPivot != null)
            clipSwingPivot.localRotation = _initialClipSwingLocalRotation;

        KeepBallHeld();
        EnableHeldBallSimulation();
    }

    /// <inheritdoc />
    public void ApplyDrawRetryIntroState()
    {
        if (!Application.isPlaying)
            return;

        _entranceTween?.Kill();
        _entranceTween = null;
        ResolveReferences();
        InitializeClipAttachment();
        CacheInitialState();
        RestoreInitialState();

        if (!_hasCachedInitialState)
        {
            this.LogError(
                "[DLevel2Ctrl] 重开时无法解析装置最终位置或夹持球，不能应用 Intro 完成态。");
            EnableHeldBallSimulation();
            return;
        }

        if (alien != null)
            alien.position = _finalAlienPosition;
        if (clipSwingPivot != null)
            clipSwingPivot.localRotation = _initialClipSwingLocalRotation;

        KeepBallHeld();
        EnableHeldBallSimulation();
    }

    /// <summary>
    /// 第一笔有效线完成后打开架子、释放球并播放 Alien control。
    /// 注意事项：每次尝试只消费一次；不改变倒计时、成功门或失败重载流程。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || _hasReleasedBall)
            return;

        ResolveReferences();
        _hasReleasedBall = true;

        PlayClipAnimation(ClipOpenAnimation, false);
        ReleaseBall();
        PlayAlienControllerAnimation();
    }

    /// <summary>
    /// 新尝试开始前停止未完成的 Intro，并恢复 Alien、架子和球的初始状态。
    /// 调用边界：只重置本关局部对象，不创建实例或访问存档/配置。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        ApplyDrawRetryIntroState();
    }

    #endregion

    #region Entrance Motion

    private Quaternion CreateClipSwingRotation(float localZAngle)
    {
        return _initialClipSwingLocalRotation
            * Quaternion.Euler(0f, 0f, localZAngle);
    }

    private Tween CreateClipSwingTween(float localZAngle, float duration)
    {
        return clipSwingPivot
            .DOLocalRotateQuaternion(
                CreateClipSwingRotation(localZAngle),
                Mathf.Max(MinimumEntranceDuration, duration))
            .SetEase(Ease.InOutSine);
    }

    #endregion

    #region Initial State

    private void CacheInitialState()
    {
        if (_hasCachedInitialState || alien == null || yellowBallRigidbody == null)
            return;

        DrawEntrancePathView entrancePath = alien.GetComponent<DrawEntrancePathView>();
        if (entrancePath == null ||
            !entrancePath.TryGetWorldPositions(
                out _entranceStartPosition,
                out _finalAlienPosition))
        {
            return;
        }

        _initialBallParent = yellowBallRigidbody.transform.parent;
        _initialBallLocalPosition = yellowBallRigidbody.transform.localPosition;
        _initialBallLocalRotation = yellowBallRigidbody.transform.localRotation;
        _initialBallLocalScale = yellowBallRigidbody.transform.localScale;
        CircleCollider2D ballCollider =
            yellowBallRigidbody.GetComponent<CircleCollider2D>();
        if (ballCollider != null)
            _initialBallColliderOffset = ballCollider.offset;

        Transform payloadContentRoot = _rigView?.PayloadContentRoot;
        if (payloadContentRoot != null)
        {
            _initialPayloadContentLocalPosition = payloadContentRoot.localPosition;
            _initialPayloadContentLocalRotation = payloadContentRoot.localRotation;
            _initialPayloadContentLocalScale = payloadContentRoot.localScale;
            _hasCachedPayloadState = true;
        }

        _initialClipSwingLocalRotation = clipSwingPivot != null
            ? clipSwingPivot.localRotation
            : Quaternion.identity;
        _hasCachedInitialState = true;
    }

    private void RestoreInitialState(bool playIdleAnimation = true)
    {
        _hasReleasedBall = false;
        PlayClipAnimation(ClipCloseAnimation, false);

        if (!_hasCachedInitialState)
        {
            if (playIdleAnimation)
                PlayAlienIdleAnimation();
            return;
        }

        if (alien != null)
            alien.position = _entranceStartPosition;

        if (clipSwingPivot != null)
            clipSwingPivot.localRotation = _initialClipSwingLocalRotation;

        if (yellowBallRigidbody != null)
        {
            yellowBallRigidbody.simulated = false;
            yellowBallRigidbody.bodyType = RigidbodyType2D.Kinematic;
            yellowBallRigidbody.linearVelocity = Vector2.zero;
            yellowBallRigidbody.angularVelocity = 0f;
            yellowBallRigidbody.transform.SetParent(_initialBallParent, false);
            yellowBallRigidbody.transform.localPosition = _initialBallLocalPosition;
            yellowBallRigidbody.transform.localRotation = _initialBallLocalRotation;
            yellowBallRigidbody.transform.localScale = _initialBallLocalScale;
            RestoreBallColliderOffset();
            RestorePayloadContent();
        }

        if (playIdleAnimation)
            PlayAlienIdleAnimation();
    }

    private void KeepBallHeld()
    {
        if (!_hasCachedInitialState || yellowBallRigidbody == null || _hasReleasedBall)
            return;

        Transform ballTransform = yellowBallRigidbody.transform;
        if (ballTransform.parent != _initialBallParent)
            ballTransform.SetParent(_initialBallParent, false);

        ballTransform.localPosition = _initialBallLocalPosition;
        ballTransform.localRotation = _initialBallLocalRotation;
        ballTransform.localScale = _initialBallLocalScale;
    }

    /// <summary>
    /// 在夹子球已经到达可玩位置后恢复其 2D 物理模拟。
    /// Intro 期间刚体保持不模拟，避免画面外的入场起点提前触发 FallArea、陷阱或平台；
    /// 恢复时仍维持 Kinematic，只有玩家完成第一笔后才由既有释放流程切换为 Dynamic。
    /// </summary>
    private void EnableHeldBallSimulation()
    {
        if (yellowBallRigidbody == null)
            return;

        Physics2D.SyncTransforms();
        yellowBallRigidbody.simulated = true;
    }

    #endregion

    #region Ball Release

    private void ReleaseBall()
    {
        if (yellowBallRigidbody == null)
        {
            this.LogError("[DLevel2Ctrl] yellow_ball Rigidbody2D is missing.");
            return;
        }

        Transform releaseParent = ballReleaseParent;
        if (releaseParent == null && alien != null)
            releaseParent = alien.parent;
        if (releaseParent == null)
            releaseParent = transform;

        yellowBallRigidbody.bodyType = RigidbodyType2D.Kinematic;
        yellowBallRigidbody.simulated = true;
        yellowBallRigidbody.linearVelocity = Vector2.zero;
        yellowBallRigidbody.angularVelocity = 0f;
        yellowBallRigidbody.transform.SetParent(releaseParent, true);
        MoveMirrorFromRigidbodyToPayload();
        yellowBallRigidbody.bodyType = RigidbodyType2D.Dynamic;
        yellowBallRigidbody.WakeUp();
    }

    /// <summary>
    /// 将镜像装置释放后落到球刚体根上的负缩放转移到 payloadContentRoot。
    /// 这样球视觉与角色保持镜像朝向，同时 Dynamic Rigidbody2D 和圆形碰撞体使用正缩放。
    /// </summary>
    private void MoveMirrorFromRigidbodyToPayload()
    {
        Transform ballTransform = yellowBallRigidbody?.transform;
        Transform payloadContentRoot = _rigView?.PayloadContentRoot;
        if (ballTransform == null || payloadContentRoot == null || !_hasCachedPayloadState)
            return;

        Vector3 releasedScale = ballTransform.localScale;
        bool mirrorX = releasedScale.x < 0f;
        bool mirrorY = releasedScale.y < 0f;
        if (!mirrorX && !mirrorY)
        {
            RestoreBallColliderOffset();
            RestorePayloadContent();
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
            yellowBallRigidbody.GetComponent<CircleCollider2D>();
        if (ballCollider != null)
        {
            Vector2 mirroredOffset = _initialBallColliderOffset;
            if (mirrorX)
                mirroredOffset.x = -mirroredOffset.x;
            if (mirrorY)
                mirroredOffset.y = -mirroredOffset.y;
            ballCollider.offset = mirroredOffset;
        }

        payloadContentRoot.localPosition = _initialPayloadContentLocalPosition;
        payloadContentRoot.localRotation =
            _initialPayloadContentLocalRotation * mirrorRotation;
        payloadContentRoot.localScale = _initialPayloadContentLocalScale;
    }

    private void RestoreBallColliderOffset()
    {
        CircleCollider2D ballCollider =
            yellowBallRigidbody?.GetComponent<CircleCollider2D>();
        if (ballCollider != null && _hasCachedInitialState)
            ballCollider.offset = _initialBallColliderOffset;
    }

    private void RestorePayloadContent()
    {
        Transform payloadContentRoot = _rigView?.PayloadContentRoot;
        if (payloadContentRoot == null || !_hasCachedPayloadState)
            return;

        payloadContentRoot.localPosition = _initialPayloadContentLocalPosition;
        payloadContentRoot.localRotation = _initialPayloadContentLocalRotation;
        payloadContentRoot.localScale = _initialPayloadContentLocalScale;
    }

    #endregion

    #region Clip Spine

    private void InitializeClipAttachment()
    {
        if (clipBoneFollower == null || alienSkeletonAnimation == null)
            return;

        if (!alienSkeletonAnimation.valid)
            alienSkeletonAnimation.Initialize(false);

        clipBoneFollower.SkeletonRenderer = alienSkeletonAnimation;
        if (!clipBoneFollower.valid)
        {
            this.LogError("[DLevel2Ctrl] Clip BoneFollower could not initialize.");
            return;
        }

        if (!clipBoneFollower.SetBone(ClipAttachmentBone))
            this.LogError($"[DLevel2Ctrl] Alien Spine bone '{ClipAttachmentBone}' is missing.");
    }

    private void PlayClipAnimation(string animationName, bool loop)
    {
        if (clipSkeletonAnimation == null)
        {
            this.LogError("[DLevel2Ctrl] Clip SkeletonAnimation is missing.");
            return;
        }

        if (!clipSkeletonAnimation.valid)
            clipSkeletonAnimation.Initialize(false);

        if (clipSkeletonAnimation.AnimationState == null)
        {
            this.LogError("[DLevel2Ctrl] Clip Spine is not initialized.");
            return;
        }

        if (clipSkeletonAnimation.Skeleton.Data.FindAnimation(animationName) == null)
        {
            this.LogError($"[DLevel2Ctrl] Clip animation '{animationName}' is missing.");
            return;
        }

        clipSkeletonAnimation.AnimationState.SetAnimation(0, animationName, loop);
    }

    #endregion

    #region Alien Animation

    private void PlayAlienControllerAnimation()
    {
        if (alienSpineCtrl == null)
        {
            this.LogError("[DLevel2Ctrl] Alien ActorSpineCtrl is missing.");
            return;
        }

        int animationVersion = ++_alienAnimationVersion;
        Spine.TrackEntry entry = alienSpineCtrl.PlayAnimation(
            AlienControllerAnimation,
            false,
            false);
        if (entry == null)
        {
            this.LogError("[DLevel2Ctrl] Alien control animation failed; restoring idle01.");
            PlayAlienIdleAnimation();
            return;
        }

        entry.Complete += _ =>
        {
            if (animationVersion != _alienAnimationVersion)
                return;

            PlayAlienIdleAnimation();
        };
    }

    private void PlayAlienIdleAnimation()
    {
        _alienAnimationVersion++;
        if (alienSpineCtrl == null)
        {
            this.LogError("[DLevel2Ctrl] Alien ActorSpineCtrl is missing.");
            return;
        }

        alienSpineCtrl.PlayAnimation(AlienIdleAnimation, true, false);
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);

        if (_rigView == null)
        {
            _rigView = alien != null
                ? alien.GetComponent<DrawAlienClipBallRigView>()
                : GetComponentInChildren<DrawAlienClipBallRigView>(true);
            _rigView?.ResolveReferences();
        }

        if (alien == null)
            alien = _rigView != null
                ? _rigView.AlienTransform
                : transform.Find("level2/Alien");

        if (yellowBallRigidbody == null && alien != null)
        {
            yellowBallRigidbody = alien
                .GetComponentInChildren<Rigidbody2D>(true);
        }

        if (alienSpineCtrl == null && alien != null)
            alienSpineCtrl = alien.GetComponent<ActorSpineCtrl>();

        if (alienSkeletonAnimation == null && alien != null)
            alienSkeletonAnimation = alien.GetComponent<SkeletonAnimation>();

        Transform clipRoot = alien != null ? alien.Find("catch") : null;
        if (clipBoneFollower == null && clipRoot != null)
            clipBoneFollower = clipRoot.GetComponent<BoneFollower>();

        if (clipSwingPivot == null && clipRoot != null)
            clipSwingPivot = clipRoot.Find("clipSwingPivot");

        if (clipSkeletonAnimation == null && clipSwingPivot != null)
        {
            clipSkeletonAnimation = clipSwingPivot
                .GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (ballReleaseParent == null && alien != null)
            ballReleaseParent = alien.parent;
    }

    #endregion
}
