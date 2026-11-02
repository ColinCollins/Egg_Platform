using Bear.EventSystem;
using Game.Events;
using UnityEngine;

/// <summary>入场沿用普通 Actor，仅在绳上/弹射阶段接管；不改共享 Actor 的输入或初始化。</summary>
public sealed class Level132ActorCtrl : ActorCtrl
{
    [SerializeField] private BoxCollider2D entranceGround;
    private PolygonCollider2D _collider;
    private Vector2[] _standingPoints;
    private Vector2[] _ropePosePoints;
    private Bounds _standingBounds;
    private Bounds _poseBounds;
    private bool _ropePoseApplied;
    private float _poseDegrees;
    private float _poseArcOffset;
    private Vector3 _bodyPosition;
    private Vector3 _bodyScale;
    private Quaternion _bodyRotation;
    private RigidbodyInterpolation2D _interpolation;
    private GameObject _shadow;
    private bool _shadowActive;
    private bool _ropeShadowHidden;
    private float _gravity;
    private bool _scripted;
    private bool _keyboardMoving;
    private bool _frozen;
    private bool _wasSimulated;
    private Vector2 _savedVelocity;
    private Spine.AnimationState _frozenAnimation;
    private float _animationSpeed;
    public bool AllowKeyboardInput { get; set; }
    public bool AllowNormalJump { get; set; }

    public bool Initialized { get; private set; }
    public bool HasEntranceGround => entranceGround != null && entranceGround.enabled &&
        entranceGround.gameObject.activeInHierarchy && !entranceGround.isTrigger;
    public PolygonCollider2D MainCollider => _collider;
    public float FootOffset => -_standingBounds.min.y;
    public float CollisionWidth => _standingBounds.size.x;
    public Vector2 CollisionHalfSize
    {
        get
        {
            Vector3 x = transform.TransformVector(Vector3.right * _poseBounds.extents.x);
            Vector3 y = transform.TransformVector(Vector3.up * _poseBounds.extents.y);
            return new Vector2(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y));
        }
    }
    public Vector2 CollisionCenter => transform.TransformPoint(_poseBounds.center + (Vector3)_collider.offset);

    // 包含所有转角、Collider offset、短弧线和扫掠余量；只用于障碍净空，不改碰撞或动作。
    public float RopeSwitchEnvelopeWidth(float arcOffsetX)
    {
        float radius = 0f;
        foreach (Vector2 point in _standingPoints)
            radius = Mathf.Max(radius, (point + _collider.offset).magnitude);
        Vector3 xAxis = transform.TransformVector(Vector3.right);
        Vector3 yAxis = transform.TransformVector(Vector3.up);
        float horizontalRadius = radius * Mathf.Sqrt(xAxis.x * xAxis.x + yAxis.x * yAxis.x);
        return 2f * (horizontalRadius + Mathf.Abs(xAxis.x * arcOffsetX) + 0.01f);
    }

    protected override void Start()
    {
        base.Start();
        _collider = GetComponent<PolygonCollider2D>();
        _standingPoints = _collider.points;
        _ropePosePoints = new Vector2[_standingPoints.Length];
        _standingBounds = new Bounds(_standingPoints[0], Vector3.zero);
        for (int i = 0; i < _standingPoints.Length; i++)
        {
            _standingBounds.Encapsulate(_standingPoints[i]);
        }
        _poseBounds = _standingBounds;
        _bodyPosition = Body.localPosition;
        _bodyScale = Body.localScale;
        _bodyRotation = Body.localRotation;
        _interpolation = RB.interpolation;
        _shadow = transform.Find("Shadow")?.gameObject;
        _gravity = RB.gravityScale;
        // 与 125 普通入场一致，保留源 Actor 的 Dynamic/重力/插值及共享出门输入。
        _collider.enabled = true;
        Initialized = true;
        LiftOutOfEntranceGround();
    }

    public override void OnUpdate()
    {
        if (!isActiveAndEnabled || !Initialized || _scripted || _frozen)
            return;
        base.OnUpdate();
    }

    protected override void HandleInput()
    {
#if UNITY_EDITOR
        if (!AllowKeyboardInput)
        {
            _keyboardMoving = false;
            return; // 出门演出只消费共享脚本输入，不让键盘抢走方向。
        }
        if (Input.GetKeyDown(KeyCode.Q)) IsEditor = !IsEditor;
        if (!IsEditor)
        {
            if (_keyboardMoving) this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
            _keyboardMoving = false;
            return;
        }
        float horizontal = Input.GetAxisRaw("Horizontal");
        if (horizontal > 0f) this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
        else if (horizontal < 0f) this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);
        else if (_keyboardMoving) this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
        // 空闲键盘不再逐帧取消 HUD 的按住输入；只在真实键盘松开时发一次取消。
        _keyboardMoving = horizontal != 0f;
        if (AllowNormalJump && Input.GetKeyDown(KeyCode.Space))
            this.DispatchEvent(Witness<PlayerJumpEvent>._);
#endif
    }

    private void LiftOutOfEntranceGround()
    {
        if (!HasEntranceGround) return;
        Bounds ground = entranceGround.bounds;
        Vector3 position = transform.position;
        if (position.x < ground.min.x || position.x > ground.max.x) return;
        // 共享入场会把根节点放到门底；只消除初始穿地，不移动门、不锁 X、不持续贴地。
        float footOffset = -transform.TransformVector(Vector3.up * (_standingBounds.min.y + _collider.offset.y)).y;
        position.y = Mathf.Max(position.y, ground.max.y + footOffset + 0.01f);
        SetPosition(position);
        Physics2D.SyncTransforms();
        HandleCollision();
    }

    public void CompleteEntrance()
    {
        SetFrozen(false);
        SetScripted(false);
        LiftOutOfEntranceGround();
        StandFacing(true);
        HandleCollision();
        UpdateAnimation(); // 站稳且输入为零时由共享规则进入 idle；不把空中状态强制画成 idle。
    }

    // 同一共享事件：初始行走是正常跳跃，脚本接管后只由本关解释为换位。
    protected override void JumpButton()
    {
        if (AllowNormalJump && !_scripted && !_frozen)
            base.JumpButton();
    }

    public void SetScripted(bool scripted)
    {
        if (!Initialized)
            return;
        SetMoveInput(0f);
        StopMoving();
        _keyboardMoving = false;
        _scripted = scripted;
        RB.bodyType = scripted ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        RB.gravityScale = scripted ? 0f : _gravity;
        RB.interpolation = scripted ? RigidbodyInterpolation2D.None : _interpolation;
        RB.angularVelocity = 0f;
        if (!scripted)
            ResumeMoving();
    }

    public void SetRopeShadowHidden(bool hidden)
    {
        if (_shadow == null) return;
        if (hidden)
        {
            // 只在真正上绳时保存现场，不采样入场淡入的临时状态。
            if (!_ropeShadowHidden) _shadowActive = _shadow.activeSelf;
            _ropeShadowHidden = true;
            // 一并隐藏 ShadowController 动态生成的子阴影。
            _shadow.SetActive(false);
        }
        else if (_ropeShadowHidden)
        {
            _ropeShadowHidden = false;
            _shadow.SetActive(_shadowActive);
        }
    }

    public void Face(bool right)
    {
        // 不调用共享 SetFaceDir：它在已经朝左时再次传 false 会翻回右边。
        SetActorFaceDirection(right);
    }

    public void SetBelowRope(bool below)
    {
        _ropePoseApplied = false;
        SetRopePose(below ? 180f : 0f, 0f);
    }

    public void SetRopePose(float degrees, float arcOffsetX)
    {
        if (_ropePoseApplied && degrees == _poseDegrees && arcOffsetX == _poseArcOffset) return;
        _ropePoseApplied = true;
        _poseDegrees = degrees;
        _poseArcOffset = arcOffsetX;
        Quaternion rotation = Quaternion.AngleAxis(degrees, Vector3.forward);
        Vector3 offset = Vector3.right * arcOffsetX;
        // 同一过渡角连续补偿朝向：Rz(180) * Ry(180) 的画面终态只倒置上下，仍朝右。
        // 不在半程/结束时跳变镜像；局部缩放保持原值，反向换位及离绳归正沿同一路径返回。
        Quaternion facingRotation = Quaternion.AngleAxis(degrees, Vector3.up);
        Body.localRotation = rotation * facingRotation * _bodyRotation;
        Body.localPosition = rotation * _bodyPosition + offset;
        Body.localScale = _bodyScale; // 连续旋转，不在过渡中把 Scale 穿过零或突然镜像。
        // 转向补偿只作用于视觉；2D 碰撞沿原 Z 轴翻身轮廓扫掠，不随侧身投影变窄/退化。
        Vector2 colliderOffset = _collider.offset;
        for (int i = 0; i < _standingPoints.Length; i++)
        {
            Vector3 point = rotation * (Vector3)(_standingPoints[i] + colliderOffset) + offset - (Vector3)colliderOffset;
            _ropePosePoints[i] = point;
            if (i == 0) _poseBounds = new Bounds(point, Vector3.zero);
            else _poseBounds.Encapsulate(point);
        }
        _collider.points = _ropePosePoints;
    }

    public void StandFacing(bool right)
    {
        SetBelowRope(false);
        Face(right);
    }

    public override void Die()
    {
        if (IsDied) return; // 重复失败通知不重播或打断已开始的死亡动画。
        if (Initialized)
        {
            SetFrozen(false);
            SetScripted(true);
            // 清除换位的 Z/Y 旋转、身体短弧偏移和碰撞轮廓，不移动根节点到绳面。
            StandFacing(true);
        }
        base.Die(); // 归正后仍走共享非循环 die，不替换动画或失败等待时长。
    }

    private void OnDisable()
    {
        SetRopeShadowHidden(false);
    }

    public void SetPosition(Vector3 position)
    {
        transform.position = position;
        RB.position = position;
    }

    public void SetFrozen(bool frozen)
    {
        if (!Initialized)
            return;
        if (_frozen != frozen)
        {
            _frozen = frozen;
            if (frozen)
            {
                _wasSimulated = RB.simulated;
                _savedVelocity = RB.linearVelocity;
                _savedVelocity.x = 0f;
                SetMoveInput(0f);
                RB.simulated = false;
            }
            else
            {
                RB.simulated = _wasSimulated;
                RB.linearVelocity = _savedVelocity;
            }
        }
        var animation = SpineCtrl != null ? SpineCtrl.AnimationState : null;
        if (_frozenAnimation != null && (!frozen || _frozenAnimation != animation))
        {
            _frozenAnimation.TimeScale = _animationSpeed;
            _frozenAnimation = null;
        }
        if (frozen && animation != null && _frozenAnimation == null)
        {
            _frozenAnimation = animation;
            _animationSpeed = animation.TimeScale;
            animation.TimeScale = 0f;
        }
    }
}
