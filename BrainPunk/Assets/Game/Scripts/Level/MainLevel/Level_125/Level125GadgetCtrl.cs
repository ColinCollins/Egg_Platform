using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 125 坑A机关控制器：升降地板 + 吊环状态机，并负责右侧坑石块排的箱子落台触发下落。
/// 状态流（2026-08 会话敲定的最终合同）：
/// S0 初始（电梯完整位于初始镜头下方，吊环在 G+3 初始位）
/// → 首次触碰吊环：3 秒内环+Actor 降至校准后的近地低位、电梯同步升至最高（G+2），同帧到位后自动松手
/// → 箱子被推上电梯：18 秒内电梯载箱匀速降至镜头下方、吊环同步回升至高位
/// → 回升途中触碰吊环 = 搭乘：Actor 挂环随环升到高于长平台 1.8 个世界单位的高位
/// → 高位收到左右输入立即以方向初速度释放；收到跳跃输入则按当前水平输入叠加向上跳；吊环永久失效。
/// 右坑石块排只接受动态箱子真实落到石块顶面的碰撞，不接受越过坑沿或 Actor 站立的几何门槛。
/// 死亡判定不在本组件内：水域 FallArea（上缘=水面-0.5）独立承担。
/// </summary>
public sealed class Level125GadgetCtrl : MonoBehaviour, IEventSender
{
    private enum Phase
    {
        Neutral,
        Pulling,
        WaitingForBox,
        SinkingAndRising,
        WaitingForHighRelease,
        Done
    }

    [Header("引用")]
    [SerializeField] private Rigidbody2D elevatorBody;
    [SerializeField] private Rigidbody2D rightPitBox;
    [SerializeField] private Transform ring;

    [Header("电梯几何")]
    [SerializeField] private float elevatorBaseX = 1.565f;
    [SerializeField] private float elevatorColliderTopOffset = 0.4236f;
    [SerializeField] private float elevatorInitTopY = -6.5f;
    [SerializeField] private float elevatorMaxTopY = -0.365f;
    [SerializeField] private float elevatorMinTopY = -7.5f;

    [Header("吊环行程")]
    [SerializeField] private float ringNeutralCenterY = 1.265f;
    [SerializeField] private float ringLowCenterY = -0.24f;
    [SerializeField] private float ringHighCenterY = 12.725f;
    [SerializeField] private Vector2 actorHangLocalOffset = new Vector2(0f, -1.25f);

    [Header("高位释放")]
    [SerializeField, Min(0f)] private float highReleaseJumpVelocity = 18f; // 高位跳跃释放时使用的向上速度，沿用 Actor 默认跳跃力度。

    [Header("同步行程")]
    [SerializeField, Min(0.01f)] private float pullDuration = 3f;
    [SerializeField, Min(0.01f)] private float sinkDuration = 18f;

    [Header("压秤探测")]
    [SerializeField] private LayerMask boxLayerMask = 256; // Stuff
    [SerializeField] private Vector2 boxProbeSize = new Vector2(2.4f, 0.4f);

    [Header("右侧第二坑石块排下落")]
    [SerializeField] private Transform[] rightPitStones;
    [SerializeField, Min(0f)] private float rightPitStoneLandingTolerance = 0.15f;
    [SerializeField, Min(0f)] private float rightPitBoxFallingSpeedThreshold = 0.05f;
    [SerializeField, Min(0f)] private float rightPitStoneDropDistance = 6f;
    [SerializeField, Min(0.01f)] private float rightPitStoneDropDuration = 0.75f;
    [SerializeField, Min(0f)] private float rightPitStoneDropRotation = 5f;
    [SerializeField, Min(0f)] private float rightPitStoneDropStagger = 0.12f;
    [SerializeField] private string rightPitStoneDropSfxTag = "l89StonePillarDrop";

    private Phase phase = Phase.Neutral;
    private EventSubscriber inputSubscriber;
    private ActorCtrl carried;
    private Rigidbody2D carriedBody;
    private float pullTimer;
    private float sinkTimer;
    private bool highReleaseRequested;
    private float highReleaseDirection;
    private float heldHorizontalDirection;
    private float currentElevatorTopY;
    private float currentRingCenterY;
    private bool elevatorConfigurationValid;
    private ActorCtrl pendingJumpResumeActor;
    private float pendingJumpResumeDirection;
    private bool hasCompletedHighRingRide;
    private Collider2D rightPitBoxCollider;
    private Collider2D[] rightPitStoneColliders;
    private bool[] pendingRightPitStoneSeeds;
    private int pendingRightPitStoneSeedCount;
    private bool rightPitStonesDropped;
    private Sequence rightPitStoneDropTween;

#if UNITY_EDITOR
    private bool editorKeyboardInputActive;
    private int editorKeyboardDirection;
#endif

    public bool IsActorCarried => carried != null;

    public bool IsHighRingRideActive =>
        carried != null &&
        (phase == Phase.SinkingAndRising || phase == Phase.WaitingForHighRelease);

    public bool HasCompletedHighRingRide => hasCompletedHighRingRide;

    private void Awake()
    {
        elevatorConfigurationValid = ValidateElevatorConfiguration();
        InitializeRightPitStoneState();
    }

    private void OnEnable()
    {
        EventsUtils.ResetEvents(ref inputSubscriber);
        inputSubscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        inputSubscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        inputSubscriber.Subscribe<PlayerMoveCancelEvent>(OnPlayerMoveCancel);
        inputSubscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
    }

    private void OnDisable()
    {
        RestoreCarriedActorAnimation(carried);
        ResumePendingJumpActor();
        rightPitStoneDropTween?.Kill();
        rightPitStoneDropTween = null;
        ClearPendingRightPitStoneLandings();
        heldHorizontalDirection = 0f;
#if UNITY_EDITOR
        editorKeyboardInputActive = false;
        editorKeyboardDirection = 0;
#endif
        EventsUtils.ResetEvents(ref inputSubscriber);
    }

    private void Start()
    {
        if (!elevatorConfigurationValid)
            return;

        currentElevatorTopY = elevatorInitTopY;
        currentRingCenterY = ringNeutralCenterY;
        ApplyElevator();
        ApplyRing();
    }

    private void LateUpdate()
    {
        ProcessPendingRightPitLanding();
        ResumePendingJumpActor();
    }

#if UNITY_EDITOR
    private void Update()
    {
        UpdateEditorKeyboardInput();
    }

    /// <summary>
    /// 只在 Actor 被本关吊环接管、或刚由键盘释放并等待松键时桥接 Editor 键盘输入。
    /// 普通状态不改变 ActorCtrl 原有的键盘行为。
    /// </summary>
    private void UpdateEditorKeyboardInput()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        if (playCtrl != null && playCtrl.IsInitialized && !playCtrl.CheckState(GamePlayStateName.PLAYING))
            return;

        bool rightHeld = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        bool leftHeld = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
        int direction = rightHeld ? 1 : leftHeld ? -1 : 0;

        if (carried == null && !editorKeyboardInputActive)
            return;

        // 先记录当前水平意图，再派发跳跃，保证同帧的跳跃+方向输入不会丢失水平初速度。
        if (direction != 0)
        {
            editorKeyboardInputActive = true;
            heldHorizontalDirection = direction;
        }

        // 方向键在本帧松开时先清除水平意图，避免“方向键松开+跳跃”沿用上一帧的水平速度。
        if (direction == 0 && editorKeyboardDirection != 0)
            this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);

        if (jumpPressed && carried != null)
            this.DispatchEvent(Witness<PlayerJumpEvent>._);

        if (direction > 0)
            this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
        else if (direction < 0)
            this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);

        editorKeyboardDirection = direction;
        if (direction == 0)
            editorKeyboardInputActive = false;
    }
#endif

    /// <summary>由 Level125RingContact 转发的吊环触碰；返回是否接管了该次触碰。</summary>
    public bool HandleRingTouch(Collider2D other)
    {
        if (!elevatorConfigurationValid)
            return false;
        if (phase != Phase.Neutral && phase != Phase.SinkingAndRising)
            return false;
        if (carried != null)
            return false;

        ActorCtrl actor = ResolveActor(other);
        if (actor == null)
            return false;

        Attach(actor);
        if (phase == Phase.Neutral)
        {
            pullTimer = 0f;
            phase = Phase.Pulling;
        }
        else
        {
            phase = Phase.SinkingAndRising;
        }
        return true;
    }

    private void FixedUpdate()
    {
        if (!elevatorConfigurationValid)
            return;

        float dt = Time.fixedDeltaTime;
        switch (phase)
        {
            case Phase.Neutral:
                break;
            case Phase.Pulling:
                StepPulling(dt);
                break;
            case Phase.WaitingForBox:
                StepWaitingForBox();
                break;
            case Phase.SinkingAndRising:
                StepSinkingAndRising(dt);
                break;
            case Phase.WaitingForHighRelease:
                StepWaitingForHighRelease();
                break;
            case Phase.Done:
                break;
        }
    }

    // 首触：环+Actor 缓降，同时电梯升到最高
    private void StepPulling(float dt)
    {
        pullTimer += dt;
        float t = Mathf.Clamp01(pullTimer / Mathf.Max(0.01f, pullDuration));

        currentElevatorTopY = Mathf.Lerp(elevatorInitTopY, elevatorMaxTopY, t);
        ApplyElevator();

        currentRingCenterY = Mathf.Lerp(ringNeutralCenterY, ringLowCenterY, t);
        ApplyRing();

        if (carriedBody != null)
            carriedBody.MovePosition(ring.position + (Vector3)actorHangLocalOffset);

        if (t < 1f)
            return;

        Detach();
        phase = Phase.WaitingForBox;
    }

    // 电梯与吊环已同步到位；探测箱子上板
    private void StepWaitingForBox()
    {
        Vector2 probeCenter = new Vector2(elevatorBaseX + 0.685f, currentElevatorTopY + boxProbeSize.y * 0.5f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(probeCenter, boxProbeSize, 0f, boxLayerMask.value);
        foreach (Collider2D hit in hits)
        {
            Rigidbody2D body = hit.attachedRigidbody;
            if (body != null && body.gameObject.name.StartsWith("Box"))
            {
                sinkTimer = 0f;
                phase = Phase.SinkingAndRising;
                return;
            }
        }
    }

    /// <summary>
    /// 由 Level125BoxCollisionRelay 转发箱子自身的真实物理碰撞。
    /// 只接收箱子向下运动时的石块顶面碰撞；同一石块重复命中去重，不同相邻石块共同进入首波。
    /// </summary>
    public void HandleRightPitBoxCollision(Collision2D collision)
    {
        if (!isActiveAndEnabled || rightPitStonesDropped || collision == null || !IsGameplayActive())
            return;

        if (!TryResolveRightPitBox() || !IsRightPitBoxCollision(collision))
            return;

        if (!TryResolveRightPitStoneColliders() ||
            !TryGetRightPitStoneCollision(collision, out int stoneIndex, out Collider2D stoneCollider))
            return;

        if (!TryGetRightPitTopLanding(collision, stoneCollider))
            return;

        float minimumFallSpeed = Mathf.Max(0f, rightPitBoxFallingSpeedThreshold);
        if (Mathf.Abs(collision.relativeVelocity.y) < minimumFallSpeed)
            return;

        QueueRightPitStoneLanding(stoneIndex);
    }

    private bool IsGameplayActive()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        return playCtrl == null ||
               !playCtrl.IsInitialized ||
               playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    private bool IsRightPitBoxCollision(Collision2D collision)
    {
        return collision.collider == rightPitBoxCollider ||
               collision.otherCollider == rightPitBoxCollider ||
               collision.rigidbody == rightPitBox ||
               collision.otherRigidbody == rightPitBox;
    }

    private bool TryResolveRightPitStoneColliders()
    {
        if (rightPitStones == null || rightPitStones.Length == 0)
            return false;

        if (rightPitStoneColliders == null || rightPitStoneColliders.Length != rightPitStones.Length)
            rightPitStoneColliders = new Collider2D[rightPitStones.Length];

        bool allStonesValid = true;
        Transform previousStone = null;
        for (int index = 0; index < rightPitStones.Length; index++)
        {
            Transform stone = rightPitStones[index];
            if (stone == null)
            {
                allStonesValid = false;
                continue;
            }

            if (previousStone != null && stone.position.x <= previousStone.position.x)
                allStonesValid = false;
            previousStone = stone;

            Collider2D collider = rightPitStoneColliders[index];
            if (collider == null)
            {
                collider = stone.GetComponent<Collider2D>();
                if (collider == null)
                    collider = stone.GetComponentInChildren<Collider2D>(true);
                rightPitStoneColliders[index] = collider;
            }

            if (collider == null || !collider.enabled || collider.isTrigger)
                allStonesValid = false;
        }

        return allStonesValid;
    }

    private bool TryGetRightPitStoneCollision(
        Collision2D collision,
        out int stoneIndex,
        out Collider2D stoneCollider)
    {
        stoneIndex = -1;
        stoneCollider = null;
        if (rightPitStoneColliders == null)
            return false;

        for (int index = 0; index < rightPitStoneColliders.Length; index++)
        {
            Collider2D candidate = rightPitStoneColliders[index];
            if (candidate == null ||
                (collision.collider != candidate && collision.otherCollider != candidate))
                continue;

            stoneIndex = index;
            stoneCollider = candidate;
            return true;
        }

        return false;
    }

    private bool TryGetRightPitTopLanding(
        Collision2D collision,
        Collider2D stoneCollider)
    {
        if (collision == null || stoneCollider == null || rightPitBoxCollider == null)
            return false;

        Bounds boxBounds = rightPitBoxCollider.bounds;
        Bounds stoneBounds = stoneCollider.bounds;
        float tolerance = Mathf.Max(0f, rightPitStoneLandingTolerance);

        if (boxBounds.center.y <= stoneBounds.center.y ||
            boxBounds.min.y < stoneBounds.max.y - tolerance ||
            boxBounds.min.y > stoneBounds.max.y + tolerance)
            return false;

        for (int index = 0; index < collision.contactCount; index++)
        {
            ContactPoint2D contact = collision.GetContact(index);
            if (contact.point.y < stoneBounds.max.y - tolerance ||
                Mathf.Abs(contact.normal.y) < 0.5f)
                continue;

            return true;
        }

        return false;
    }

    private void ProcessPendingRightPitLanding()
    {
        if (rightPitStonesDropped ||
            pendingRightPitStoneSeedCount == 0 ||
            !IsGameplayActive())
            return;

        QueueTouchingRightPitStoneSeeds();
        if (TriggerRightPitStoneDrop())
            ClearPendingRightPitStoneLandings();
    }

    private void QueueTouchingRightPitStoneSeeds()
    {
        if (!TryResolveRightPitBox() || !TryResolveRightPitStoneColliders())
            return;

        for (int index = 0; index < rightPitStoneColliders.Length; index++)
        {
            if (IsRightPitStoneTopTouching(rightPitStoneColliders[index]))
                QueueRightPitStoneLanding(index);
        }
    }

    private bool IsRightPitStoneTopTouching(Collider2D stoneCollider)
    {
        if (stoneCollider == null ||
            rightPitBoxCollider == null ||
            !stoneCollider.enabled ||
            stoneCollider.isTrigger ||
            !stoneCollider.IsTouching(rightPitBoxCollider))
            return false;

        Bounds boxBounds = rightPitBoxCollider.bounds;
        Bounds stoneBounds = stoneCollider.bounds;
        float tolerance = Mathf.Max(0f, rightPitStoneLandingTolerance);
        return boxBounds.center.y > stoneBounds.center.y &&
               boxBounds.min.y >= stoneBounds.max.y - tolerance &&
               boxBounds.min.y <= stoneBounds.max.y + tolerance;
    }

    private void QueueRightPitStoneLanding(int stoneIndex)
    {
        if (rightPitStones == null ||
            pendingRightPitStoneSeeds == null ||
            stoneIndex < 0 ||
            stoneIndex >= rightPitStones.Length ||
            stoneIndex >= pendingRightPitStoneSeeds.Length ||
            rightPitStones[stoneIndex] == null ||
            pendingRightPitStoneSeeds[stoneIndex])
            return;

        pendingRightPitStoneSeeds[stoneIndex] = true;
        pendingRightPitStoneSeedCount++;
    }

    private bool TryResolveRightPitBox()
    {
        if (rightPitBox == null || !IsRightPitBoxCandidate(rightPitBox))
            return false;

        if (rightPitBoxCollider == null)
        {
            rightPitBoxCollider = rightPitBox.GetComponent<Collider2D>();
            if (rightPitBoxCollider == null)
                rightPitBoxCollider = rightPitBox.GetComponentInChildren<Collider2D>(true);
        }

        return rightPitBoxCollider != null &&
               rightPitBoxCollider.enabled &&
               !rightPitBoxCollider.isTrigger;
    }

    private bool IsRightPitBoxCandidate(Rigidbody2D body)
    {
        return body != null &&
               body.bodyType == RigidbodyType2D.Dynamic &&
               body.simulated &&
               (boxLayerMask.value & (1 << body.gameObject.layer)) != 0 &&
               body.gameObject.name.StartsWith("Box");
    }

    private bool TriggerRightPitStoneDrop()
    {
        if (rightPitStonesDropped ||
            rightPitStones == null ||
            pendingRightPitStoneSeeds == null ||
            pendingRightPitStoneSeedCount == 0 ||
            !TryResolveRightPitStoneColliders())
            return false;

        int maxWaveDistance = 0;
        for (int index = 0; index < rightPitStones.Length; index++)
        {
            if (rightPitStones[index] != null)
                maxWaveDistance = Mathf.Max(maxWaveDistance, GetRightPitStoneWaveDistance(index));
        }

        rightPitStonesDropped = true;
        if (!string.IsNullOrEmpty(rightPitStoneDropSfxTag))
            AudioManager.PlaySound(rightPitStoneDropSfxTag);

        float duration = Mathf.Max(0.01f, rightPitStoneDropDuration);
        float dropDistance = Mathf.Max(0f, rightPitStoneDropDistance);
        float rotation = Mathf.Abs(rightPitStoneDropRotation);
        float stagger = Mathf.Max(0f, rightPitStoneDropStagger);

        rightPitStoneDropTween?.Kill();
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        float waveStartTime = 0f;
        int scheduledStoneCount = 0;
        for (int waveDistance = 0; waveDistance <= maxWaveDistance; waveDistance++)
        {
            int waveStoneCount = 0;
            for (int index = 0; index < rightPitStones.Length; index++)
            {
                Transform stone = rightPitStones[index];
                if (stone == null ||
                    GetRightPitStoneWaveDistance(index) != waveDistance)
                    continue;

                sequence.Insert(
                    waveStartTime,
                    CreateRightPitStoneDrop(stone, index, duration, dropDistance, rotation));
                waveStoneCount++;
                scheduledStoneCount++;
            }

            if (waveStoneCount == 0)
                continue;

            waveStartTime += stagger;
        }

        if (scheduledStoneCount == 0)
        {
            rightPitStonesDropped = false;
            sequence.Kill();
            return false;
        }

        rightPitStoneDropTween = sequence;
        sequence.OnComplete(() => rightPitStoneDropTween = null);
        return true;
    }

    private void InitializeRightPitStoneState()
    {
        int stoneCount = rightPitStones?.Length ?? 0;
        rightPitStoneColliders = stoneCount > 0 ? new Collider2D[stoneCount] : null;
        pendingRightPitStoneSeeds = stoneCount > 0 ? new bool[stoneCount] : null;
        pendingRightPitStoneSeedCount = 0;
    }

    private void ClearPendingRightPitStoneLandings()
    {
        if (pendingRightPitStoneSeeds != null)
            System.Array.Clear(pendingRightPitStoneSeeds, 0, pendingRightPitStoneSeeds.Length);
        pendingRightPitStoneSeedCount = 0;
    }

    private int GetRightPitStoneWaveDistance(int stoneIndex)
    {
        int nearestDistance = int.MaxValue;
        if (pendingRightPitStoneSeeds == null)
            return nearestDistance;

        for (int index = 0; index < pendingRightPitStoneSeeds.Length; index++)
        {
            if (pendingRightPitStoneSeeds[index])
                nearestDistance = Mathf.Min(nearestDistance, Mathf.Abs(stoneIndex - index));
        }

        return nearestDistance;
    }

    private Sequence CreateRightPitStoneDrop(
        Transform stone,
        int stoneIndex,
        float duration,
        float dropDistance,
        float rotation)
    {
        float targetY = stone.localPosition.y - dropDistance;
        float tilt = ResolveRightPitStoneDropTilt(stoneIndex, rotation);
        float targetAngle = stone.localEulerAngles.z + tilt;
        Transform shakeTarget = stone.GetComponentInChildren<SpriteRenderer>(true)?.transform;

        Sequence fall = DOTween.Sequence()
            .Insert(0f, stone.DOLocalMoveY(targetY, duration).SetEase(Ease.InQuad))
            .Insert(0f, stone.DOLocalRotate(new Vector3(0f, 0f, targetAngle), duration)
                .SetEase(Ease.InQuad));
        if (shakeTarget != null && shakeTarget != stone)
        {
            fall.Insert(
                0f,
                shakeTarget.DOShakePosition(
                    0.12f,
                    new Vector3(0.06f, 0.04f, 0f),
                    8,
                    90f,
                    false,
                    true));
        }

        return fall;
    }

    private float ResolveRightPitStoneDropTilt(int stoneIndex, float rotation)
    {
        if (pendingRightPitStoneSeeds == null || pendingRightPitStoneSeeds[stoneIndex])
            return 0f;

        int firstSeedIndex = -1;
        int lastSeedIndex = -1;
        for (int index = 0; index < pendingRightPitStoneSeeds.Length; index++)
        {
            if (!pendingRightPitStoneSeeds[index])
                continue;

            if (firstSeedIndex < 0)
                firstSeedIndex = index;
            lastSeedIndex = index;
        }

        // 正 Z 让石块左侧下沉，负 Z 让石块右侧下沉；石块向首波接触区外侧倾斜。
        if (stoneIndex < firstSeedIndex)
            return rotation;
        if (stoneIndex > lastSeedIndex)
            return -rotation;
        return 0f;
    }

    // 载箱匀速下沉；同时吊环回升。回升途中可被抓乘。
    private void StepSinkingAndRising(float dt)
    {
        sinkTimer += dt;
        float t = Mathf.Clamp01(sinkTimer / Mathf.Max(0.01f, sinkDuration));

        currentElevatorTopY = Mathf.Lerp(elevatorMaxTopY, elevatorMinTopY, t);
        ApplyElevator();

        currentRingCenterY = Mathf.Lerp(ringLowCenterY, ringHighCenterY, t);
        ApplyRing();

        if (carriedBody != null)
            carriedBody.MovePosition(ring.position + (Vector3)actorHangLocalOffset);

        if (t >= 1f)
        {
            if (carried == null)
            {
                phase = Phase.Done;
                return;
            }

            highReleaseRequested = false;
            highReleaseDirection = 0f;
            phase = Phase.WaitingForHighRelease;
        }
    }

    // 到达高位后保持挂载；玩家首次按左右移动或跳跃时释放。
    private void StepWaitingForHighRelease()
    {
        if (carriedBody != null)
            carriedBody.MovePosition(ring.position + (Vector3)actorHangLocalOffset);

        if (!highReleaseRequested)
            return;

        ActorCtrl releasedActor = carried;
        float direction = highReleaseDirection;
        hasCompletedHighRingRide = true;
        ReleaseHighActorWithDirection(releasedActor, direction);

        phase = Phase.Done;
    }

    private void OnPlayerRightMove(PlayerRightMoveEvent _)
    {
        heldHorizontalDirection = 1f;
        UpdatePendingJumpResumeDirection(1f);
        RequestHighRelease(1f);
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent _)
    {
        heldHorizontalDirection = -1f;
        UpdatePendingJumpResumeDirection(-1f);
        RequestHighRelease(-1f);
    }

    private void OnPlayerMoveCancel(PlayerMoveCancelEvent _)
    {
        heldHorizontalDirection = 0f;
        UpdatePendingJumpResumeDirection(0f);
    }

    private void OnPlayerJump(PlayerJumpEvent _)
    {
        if (phase != Phase.WaitingForHighRelease || carried == null)
            return;

        ReleaseHighActorWithJump(heldHorizontalDirection);
    }

    private void RequestHighRelease(float direction)
    {
        if (phase != Phase.WaitingForHighRelease || carried == null)
            return;

        highReleaseRequested = true;
        highReleaseDirection = direction;
    }

    private void ReleaseHighActorWithDirection(ActorCtrl releasedActor, float direction)
    {
        Rigidbody2D releasedBody = carriedBody;
        carried = null;
        carriedBody = null;

        if (releasedActor == null)
            return;

        if (releasedBody != null)
        {
            releasedBody.isKinematic = false;
            releasedBody.linearVelocity = new Vector2(
                Mathf.Clamp(direction, -1f, 1f) * releasedActor.MoveSpeed,
                0f);
            releasedBody.WakeUp();
        }

        releasedActor.ResumeMoving();
        releasedActor.SetMoveInput(direction);
        RestoreCarriedActorAnimation(releasedActor);
    }

    private void ReleaseHighActorWithJump(float horizontalDirection)
    {
        ActorCtrl releasedActor = carried;
        Rigidbody2D releasedBody = carriedBody;
        carried = null;
        carriedBody = null;
        hasCompletedHighRingRide = true;
        phase = Phase.Done;

        if (releasedActor == null)
            return;

        float clampedDirection = Mathf.Clamp(horizontalDirection, -1f, 1f);
        releasedActor.SetMoveInput(0f);
        if (releasedBody != null)
        {
            releasedBody.isKinematic = false;
            releasedBody.linearVelocity = new Vector2(
                clampedDirection * releasedActor.MoveSpeed,
                highReleaseJumpVelocity);
            releasedBody.WakeUp();
        }

        // 先保持 canMove=false，避免同一 PlayerJumpEvent 被 BaseLevelCtrl 当成普通跳跃再次处理；
        // 下一帧恢复输入，刚体已写入的水平/向上初速度仍会继续生效。
        pendingJumpResumeActor = releasedActor;
        pendingJumpResumeDirection = clampedDirection;
        RestoreCarriedActorAnimation(releasedActor);
        AudioManager.PlaySound("jump");
    }

    private void UpdatePendingJumpResumeDirection(float direction)
    {
        if (pendingJumpResumeActor == null)
            return;

        float clampedDirection = Mathf.Clamp(direction, -1f, 1f);
        pendingJumpResumeDirection = clampedDirection;

        Rigidbody2D body = pendingJumpResumeActor.RB;
        if (body == null || pendingJumpResumeActor.IsDied)
            return;

        body.linearVelocity = new Vector2(
            clampedDirection * pendingJumpResumeActor.MoveSpeed,
            body.linearVelocityY);
    }

    private void ResumePendingJumpActor()
    {
        if (pendingJumpResumeActor == null)
            return;

        ActorCtrl actorToResume = pendingJumpResumeActor;
        pendingJumpResumeActor = null;
        float direction = pendingJumpResumeDirection;
        pendingJumpResumeDirection = 0f;
        if (!actorToResume.IsDied)
        {
            actorToResume.ResumeMoving();
            actorToResume.SetMoveInput(direction);
        }
    }

    private void Attach(ActorCtrl actor)
    {
        carried = actor;
        carriedBody = actor.RB;
        heldHorizontalDirection = 0f;
        actor.StopMoving();
        if (carriedBody != null)
        {
            carriedBody.isKinematic = true;
            carriedBody.linearVelocity = Vector2.zero;
        }

        BeginCarriedActorAnimation(actor);
    }

    private void Detach()
    {
        if (carried == null)
            return;

        ActorCtrl detachedActor = carried;
        if (carriedBody != null)
        {
            carriedBody.isKinematic = false;
            carriedBody.linearVelocity = Vector2.zero;
        }
        detachedActor.ResumeMoving();
        RestoreCarriedActorAnimation(detachedActor);
        carried = null;
        carriedBody = null;
    }

    private void BeginCarriedActorAnimation(ActorCtrl actor)
    {
        if (actor is Level125ActorCtrl level125Actor)
        {
            level125Actor.BeginHangAnimation();
            return;
        }

        Debug.LogWarning(
            "[Level125GadgetCtrl] Level 125 Actor is not using Level125ActorCtrl; continuing without hang animation.",
            actor);
    }

    private static void RestoreCarriedActorAnimation(ActorCtrl actor)
    {
        if (actor is Level125ActorCtrl level125Actor)
            level125Actor.RestoreDefaultAnimation();
    }

    private bool ValidateElevatorConfiguration()
    {
        if (elevatorBody == null)
        {
            Debug.LogError(
                "[Level125GadgetCtrl] elevatorBody must reference the Kinematic Rigidbody2D on Level125_ElevatorFloor.",
                this);
            return false;
        }

        bool isValid =
            elevatorBody.bodyType == RigidbodyType2D.Kinematic &&
            elevatorBody.simulated &&
            Mathf.Approximately(elevatorBody.gravityScale, 0f) &&
            elevatorBody.interpolation == RigidbodyInterpolation2D.Interpolate &&
            elevatorBody.collisionDetectionMode == CollisionDetectionMode2D.Continuous &&
            elevatorBody.constraints == RigidbodyConstraints2D.FreezeRotation;
        if (isValid)
            return true;

        Debug.LogError(
            $"[Level125GadgetCtrl] Level125_ElevatorFloor Rigidbody2D configuration is invalid. " +
            $"Expected Kinematic, Simulated=true, GravityScale=0, Interpolate, Continuous, FreezeRotation; " +
            $"actual BodyType={elevatorBody.bodyType}, Simulated={elevatorBody.simulated}, " +
            $"GravityScale={elevatorBody.gravityScale}, Interpolation={elevatorBody.interpolation}, " +
            $"CollisionDetection={elevatorBody.collisionDetectionMode}, Constraints={elevatorBody.constraints}.",
            this);
        return false;
    }

    private void ApplyElevator()
    {
        elevatorBody.MovePosition(new Vector2(elevatorBaseX, currentElevatorTopY - elevatorColliderTopOffset));
    }

    private void ApplyRing()
    {
        if (ring != null)
            ring.position = new Vector3(ring.position.x, currentRingCenterY, ring.position.z);
    }

    private static ActorCtrl ResolveActor(Collider2D collision)
    {
        if (collision == null)
            return null;
        ActorCtrl actor = collision.GetComponentInParent<ActorCtrl>();
        if (actor != null)
            return actor.CompareTag("Player") ? actor : null;
        return null;
    }
}
