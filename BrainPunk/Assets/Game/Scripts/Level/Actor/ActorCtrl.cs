using System;
using System.Diagnostics;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;

using UnityEngine;

public partial class ActorCtrl : MonoBehaviour, IDebuger, IEventSender
{
    [Header("Basic Move")]
    [SerializeField] private float moveSpeed = 5;

    public float MoveSpeed
    {
        get
        {
            return moveSpeed;
        }

        protected set
        {
            moveSpeed = value;
        }
    }

    [SerializeField] private float jumpForce = 18;
    [SerializeField] private float buoyantForce = 1f;

    [SerializeField] private float doubleJumpForce = 11f;

    [Header("Buffer & Coyote Jump")]
    [SerializeField] private float bufferJumpWindow = .25f;
    private float bufferJumpActivated = -1;

    [SerializeField] private float coyoteJumpWindow = .2f;
    private float coyoteJumpActivated = -1;

    [Header("Collision info")]

    /// <summary>
    /// 用途：配置角色 Ground BoxCast 的检测框尺寸，并允许关卡专用 Actor 子类在运行时局部调整。
    /// 调用边界：共享 ActorCtrl 继续直接读取；派生类只能修改自身组件实例，不影响其它 Actor 或 prefab 资源。
    /// 注意事项：默认值与既有序列化字段名保持不变，派生类写入时应保证 X/Y 非负。
    /// </summary>
    [SerializeField] protected Vector2 groundCheckSize = new Vector2(1, 0.2f);

    /// <summary>
    /// 用途：配置角色沿 GroundCheckDirection 执行 BoxCast 的检测距离，并允许关卡专用 Actor 子类在运行时局部调整。
    /// 调用边界：共享 ActorCtrl 继续直接读取；派生类只能修改自身组件实例，不影响其它 Actor 或 prefab 资源。
    /// 注意事项：默认值与既有序列化字段名保持不变，派生类写入时应保证数值非负。
    /// </summary>
    [SerializeField] protected float GroundCheckDistance = 0.42f;

    /// <summary>
    /// 用途：提供当前 Actor 的地面 BoxCast 世界方向；默认向下，关卡专用 Actor 可按自身重力方向覆写。
    /// 调用边界：仅由共享碰撞检测与 Gizmo 绘制读取；覆写不得修改全局 Physics2D、其它 Actor 或序列化配置。
    /// 注意事项：返回零向量时共享逻辑回退为 Vector2.down，非零向量会在使用前归一化。
    /// </summary>
    protected virtual Vector2 GroundCheckDirection => Vector2.down;

    [SerializeField] private float WallCheckDistance = 0.86f;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private LayerMask whatIsWall;
    [SerializeField] private LayerMask whatIsStuff;
    [SerializeField] private LayerMask whatIsWater;
    [SerializeField] private GameObject pushCollider;

    [Header("Jump Feature")]
    [SerializeField] private bool OpenDoubleJump;
    [SerializeField] private bool isBuffJump;


    [Header("Knock back")]
    [SerializeField] private float KnockDuration = 2f;

    private bool isKnocked = false;
    public bool IsKnocked => isKnocked;

    #region Temp

    public bool IsEditor = false;
    private float xInput;
    public float XInput => xInput;
    private bool _preserveExternalHorizontalMomentum;
    private float yInput;
    public float YInput => yInput;

    private float defaultGravity = 4.5f;

    // 正在踢
    private bool isKicking = false;
    public bool IsKicking => isKicking;


    public Transform Body { get; private set; }

    // 是否在移动
    public bool IsMoving { get => xInput != 0; }

    // 是否在地面
    public bool IsGround { get => isGrounded; }

    // 是否死了
    public bool IsDied { get; private set; }

    // 是否坐着
    public bool IsSit => isSit;

    /// <summary>
    /// 用途：向派生角色暴露共享移动门禁的只读状态，确保局部动作与 StopMoving()/ResumeMoving() 保持一致。
    /// 调用边界：仅供 ActorCtrl 派生类在提交局部移动或跳跃动作前检查；外部系统仍通过现有控制方法修改状态。
    /// 注意事项：该属性不改变移动状态，也不替代关卡输入、暂停或结算门禁。
    /// </summary>
    protected bool CanMove => canMove;

    // 是否朝右
    public bool FaingRight => facingRight;

    // 墙壁碰撞检测
    public bool IsWallDetected => isWallDetected;

    // 推物体检测
    public bool IsPushStuff => isPushStuff;

    #endregion

    #region Public Control Methods

    /// <summary>
    /// 设置水平移动输入 (-1 到 1)
    /// </summary>
    public void SetMoveInput(float input)
    {
        if (!canMove)
        {
            xInput = 0;
            return;
        }

        xInput = Mathf.Clamp(input, -1f, 1f);
    }

    /// <summary>
    /// 控制移动逻辑是否临时保留与当前输入同向、且快于基础移动速度的外部水平动量。
    /// 调用边界：仅供真实物理冲量需要跨帧保留 X 速度的局部机关使用；关闭后下一次移动更新恢复基础速度。
    /// </summary>
    public void SetPreserveExternalHorizontalMomentum(bool preserve)
    {
        _preserveExternalHorizontalMomentum = preserve;
    }

    // 基本上是用于测试的
    public void UpdateAnimation()
    {
        HandleAnimators();
    }

    /// <summary>
    /// 触发跳跃
    /// </summary>
    public void TriggerJump()
    {
        JumpButton();
        RequestBufferJump();
    }

    #endregion

    #region State 

    // 是否在地面
    private bool isGrounded;

    // 是否可以二段跳
    private bool canDoubleJump;

    // 是否是浮空状态
    private bool isAirbornes;
    // 是否贴墙
    private bool isWallDetected;

    // 是否在水里
    private bool isInWater;

    // 面朝 右
    private bool facingRight;
    // 朝向，用于拓展
    private int facingDir = 1;

    private bool isPushStuff;

    private bool isDive;

    // 是否是冲刺模式
    private bool isDash;

    #endregion

    [SerializeField] private ActorSpineCtrl spineCtrl;
    public ActorSpineCtrl SpineCtrl => spineCtrl;
    private Rigidbody2D rb;
    public Rigidbody2D RB => rb;

    /// <summary>
    /// 初始化时的父节点，用于 Site 动作使用
    /// </summary>
    private Transform originParent;
    protected Transform OriginParent => originParent;

    private bool canMove = true;
    private bool isSit = false;

    protected void SetSitState(bool sit)
    {
        isSit = sit;
        canMove = !sit;
    }

    protected void SetActorFaceDirection(bool isRight)
    {
        if (facingRight != isRight)
        {
            Flip();
        }
    }

    private bool isReady = false;
    public bool IsReady = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        facingDir = 1;
        facingRight = true;
        rb = GetComponent<Rigidbody2D>();
        Body = transform.Find("spine");
        originParent = transform.parent;

        canMove = true;
        IsDied = false;

        isReady = true;
    }

    // Update is called once per frame
    public virtual void OnUpdate()
    {
        if (!isReady)
            return;

        UpdateAirbornStatus();

        if (isSit)
            return;

        if (isKnocked || !canMove)
        {
            HandleCollision();
            HandleAnimators();
            return;
        }

        HandleInput();
        HandleFlip();
        // HandleWallSlide();
        HandleMovement();
        // HandleUnderWater();
        HandleCollision();
        HandleAnimators();
    }

    protected void UpdateAirbornStatus()
    {
        if (!isGrounded && !isAirbornes)
            BecomeAirborne();

        if (isGrounded && isAirbornes)
            HandleLanding();
    }

    // 空中状态
    private void BecomeAirborne()
    {
        isAirbornes = true;

        if (rb != null && rb.linearVelocityY < 0)
            ActivateCoyoteJump();
    }

    // 落地状态
    protected virtual void HandleLanding()
    {
        isAirbornes = false;
        canDoubleJump = true;

        if (isBuffJump)
            AttempBufferJump();
    }

    protected void HandleMovement()
    {
        // this.Log("----------------xVelocity: " + rb.linearVelocity.x);
        if (isWallDetected || rb == null)
            return;
        // this.Log("----------------xInput: " + xInput);

        float multiply = isDive ? 0.5f : 1;
        var xVelocity = xInput * moveSpeed * multiply;
        if (_preserveExternalHorizontalMomentum &&
            xVelocity * rb.linearVelocityX > 0f &&
            Mathf.Abs(rb.linearVelocityX) > Mathf.Abs(xVelocity))
        {
            xVelocity = rb.linearVelocityX;
        }

        rb.linearVelocity = new Vector2(xVelocity, rb.linearVelocityY);
        // this.Log("----------------: " +  rb.linearVelocityX);
    }

    [Conditional("UNITY_EDITOR")]
    protected virtual void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            IsEditor = !IsEditor;
        }
        // this.Log(" Editor Moving ----------------- " + xInput);

        if (!IsEditor || !canMove)
        {
            return;
        }

        var x = Input.GetAxisRaw("Horizontal");
        if (x == 1)
        {
            this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
        }
        else if (x == -1)
        {
            this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);
        }
        else
        {
            this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
        }

        yInput = Input.GetAxisRaw("Vertical");

        // this.Log(" Editor Moving ----------------- " + xInput);
        // Temp - 键盘输入仍然可用
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // TriggerJump();
            this.DispatchEvent(Witness<PlayerJumpEvent>._);
        }
    }

    public void ResumeMoving()
    {
        canMove = true;
        this.Log("--------- ResumeMoving");
    }

    public void StopMoving()
    {
        canMove = false;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
        this.Log("--------- StopMoving");
        // rb.simulated = false;
    }

    protected void HandleCollision()
    {
        // 使用 BoxCast 进行地面检测
        Vector2 groundCheckDirection = ResolveGroundCheckDirection();
        RaycastHit2D groundHit = Physics2D.BoxCast(
            transform.position,
            groundCheckSize,
            0f,
            groundCheckDirection,
            GroundCheckDistance,
            whatIsGround
        );
        isGrounded = groundHit.collider != null;

        isWallDetected = Physics2D.Raycast(transform.position, Vector2.right * facingDir, WallCheckDistance, whatIsWall);
        isPushStuff = Physics2D.Raycast(transform.position, Vector2.right * facingDir, WallCheckDistance, whatIsStuff);

        pushCollider.SetActive(isPushStuff);
    }

    private Vector2 ResolveGroundCheckDirection()
    {
        Vector2 direction = GroundCheckDirection;
        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.down;
    }

    #region Spine

    protected enum ActorSpineState
    {
        idle,
        jump_up,
        jump_down,
        walk,
        walk01,
        push,
        swim,
        die,
        sofa_jump,
        sofa_idle,
    }

    protected virtual void HandleAnimators()
    {
        // this.Log("???: "+xInput.ToString());
        // 死亡后不切动画
        if (IsDied || IsKicking || isSit || rb == null)
            return;

        if (isInWater)
        {
            spineCtrl.PlayAnimation(ActorSpineState.swim.ToString(), true);
        }
        else if (isGrounded)
        {
            // this.Log("???: "+ xInput.ToString());
            if (xInput != 0)
            {
                if (isWallDetected || isPushStuff)
                {
                    spineCtrl.PlayAnimation(ActorSpineState.push.ToString(), true);
                }
                else
                {
                    if (!isDash)
                        spineCtrl.PlayAnimation(ActorSpineState.walk.ToString(), true);
                    else
                        spineCtrl.PlayAnimation(ActorSpineState.walk01.ToString(), true);

                }
            }
            else
            {
                spineCtrl.PlayAnimation(ActorSpineState.idle.ToString(), true);
            }
        }
        else
        {
            if (rb.linearVelocityY > 0)
            {
                spineCtrl.PlayAnimation(ActorSpineState.jump_up.ToString(), false);
            }
            else
            {
                spineCtrl.PlayAnimation(ActorSpineState.jump_down.ToString(), false);
            }
        }
    }

    public void PlayKick()
    {
        isKicking = true;
        SpineCtrl.PlayAnimation("kick", false).Complete += (track) =>
        {
            isKicking = false;
        };
    }


    #endregion 

    protected void HandleFlip()
    {
        if (xInput < 0 && facingRight || xInput > 0 && !facingRight)
            Flip();
    }

    public void SetFaceDir(bool isRight = false)
    {
        if (facingRight && isRight)
            return;

        Flip();
    }


    #region Jump

    private void RequestBufferJump()
    {
        if (isAirbornes)
            bufferJumpActivated = Time.time;
    }

    private void AttempBufferJump()
    {
        if (Time.timeSinceLevelLoad < bufferJumpActivated + bufferJumpWindow)
        {
            this.Log("Buffer Jump");
            bufferJumpActivated = Time.time - 1;
            Jump();
        }
    }

    private void ActivateCoyoteJump() => coyoteJumpActivated = Time.time;

    private void CancelCoyoteJump() => coyoteJumpActivated = Time.time - 1;

    private bool IsCoyoteJumpAvaliable => Time.time < coyoteJumpActivated + coyoteJumpWindow;

    protected virtual void JumpButton()
    {
        if (isSit || !canMove)
            return;
        // bool coyoteJumpActivated = IsCoyoteJumpAvaliable;
        if (isGrounded || IsCoyoteJumpAvaliable || isInWater)
        {
            // this.Log("--------------jump");
            Jump();
        }
        else if (canDoubleJump && OpenDoubleJump)
        {
            DoubleJump();
        }

        CancelCoyoteJump();
    }

    protected void Jump()
    {
        AudioManager.PlaySound("jump");
        rb.linearVelocity = new Vector2(rb.linearVelocityX, jumpForce);
    }

    // 只有落地之后，才能再次触发
    protected void DoubleJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocityX, doubleJumpForce);
        canDoubleJump = false;
        AudioManager.PlaySound("jump");
        this.Log("Double Jump");
    }

    #endregion

    private void Flip()
    {
        facingDir = facingDir * -1;
        transform.Rotate(0, 180f, 0);
        facingRight = !facingRight;
    }

    private void HandleWallSlide()
    {
        bool canWallSlide = isWallDetected && rb.linearVelocityY < 0;
        float yModify = yInput < 0 ? 1 : .5f;

        if (!canWallSlide)
            return;

        rb.linearVelocity = new Vector2(rb.linearVelocityX, rb.linearVelocityY * yModify);
    }

    private void OnDrawGizmos()
    {
        var pos = transform.position;

        // 绘制地面检测 Box（BoxCast）
        Vector2 boxSize = groundCheckSize;
        Vector2 boxCenter = (Vector2)pos + ResolveGroundCheckDirection() * GroundCheckDistance;

        // 绘制 Box 的四个角
        Vector2 halfSize = boxSize * 0.5f;
        // this.Log($"------{halfSize.y}");
        Vector2 topLeft = boxCenter + new Vector2(-halfSize.x, halfSize.y);
        Vector2 topRight = boxCenter + new Vector2(halfSize.x, halfSize.y);
        Vector2 bottomLeft = boxCenter + new Vector2(-halfSize.x, -halfSize.y);
        Vector2 bottomRight = boxCenter + new Vector2(halfSize.x, -halfSize.y);

        Gizmos.color = Color.green;
        // 绘制 Box 的轮廓
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
        // 绘制墙壁检测线
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pos, new Vector2(pos.x + WallCheckDistance * facingDir, pos.y));
    }


    #region Collision Check

    // 正在碰撞指定对象
    /*     private bool IsCollision;
        private LayerMask _checkMask;
        // 检测是否进入
        public bool CheckCollisionLayer(LayerMask layer)
        {
            if (_checkMask != layer)
                _checkMask = layer;

            return IsCollision;
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            IsCollision = 1 << other.gameObject.layer == _checkMask;
        }
     */

    /// <summary>
    /// 切换 Actor 推动物体检测层；仅供关卡专用逻辑在明确需要临时改变推物判定时调用，传入空 LayerMask 会关闭该检测。
    /// </summary>
    /// <param name="mask">新的推物检测层；调用方需要负责后续恢复或确认本关不再需要原始推物层。</param>
    public void SwitchWhatIsStuff(LayerMask mask)
    {
        whatIsStuff = mask;
    }

    #endregion

    #region Water

    private void FixedUpdate()
    {
        HandleUnderWater();
    }

    private void HandleUnderWater()
    {
        if (isInWater)
        {
            if (isDive)
            {
                rb.linearVelocityY = -2f;
            }
            else
            {
                rb.linearVelocityY *= Time.fixedDeltaTime * 30f;
                if (Mathf.Abs(rb.linearVelocityY) < .1f)
                    rb.linearVelocityY = 0;
                // rb.linearVelocityY = Mathf.Clamp(rb.linearVelocityY, -2f, 2f);
            }
            this.Log($"gravity velocity y: {rb.linearVelocityY}");
        }
    }

    public void EnterWater(Collider2D other)
    {
        isInWater = true;
        isGrounded = false;
        rb.gravityScale = 0;
        rb.linearVelocityY = -20f;
        this.Log($"Enter water: {isInWater}");
    }

    public void ExitWater(Collider2D other)
    {
        isInWater = false;
        rb.gravityScale = defaultGravity;
    }

    public void SwitchDiveState(bool isDive)
    {
        this.Log($"add dive force: {isDive}");
        this.isDive = isDive;
    }

    public void AddDiveVelocity()
    {
        this.Log($"add dive force: {isDive}");
        if (rb.linearVelocityY != 0)
            return;
        rb.linearVelocityY = -30f;
        rb.gravityScale = defaultGravity;
    }


    #endregion

    public virtual void Die()
    {
        IsDied = true;

        spineCtrl.PlayAnimation(ActorSpineState.die.ToString(), false);
        SetMoveInput(0);
        StopMoving();
    }

    public void SetSpineAlpha(float alpha)
    {
        spineCtrl.SetAlpha(alpha);
    }

    /// <summary>
    /// 开启是否冲刺
    /// </summary>
    /// <param name="isOpen"></param>
    public void SwitchDash(bool isOpen)
    {
        isDash = isOpen;
    }

    /// <summary>
    /// 设置移动速度
    /// </summary>
    /// <param name="moveSpeed"></param>
    public void SetMoveSpeed(float moveSpeed)
    {
        MoveSpeed = moveSpeed;
    }

    public void SetJumpForce(float force)
    {
        jumpForce = force;
    }

    #region Knock
    /// <summary>
    /// 添加一个力
    /// </summary>
    /// <param name="dir"></param>
    /// <param name="force"></param>
    public async UniTask Knockback(Vector2 dir, float force, float knockDuration = -1)
    {
        isKnocked = true;

        rb.linearVelocity = dir * force;
        this.Log("---------" + rb.linearVelocity);

        await UniTask.WaitForSeconds(knockDuration < 0 ? KnockDuration : knockDuration);

        isKnocked = false;
    }

    public void StopKnock()
    {
        isKnocked = false;
    }

    #endregion


    #region Sofa

    private Vector3 siteOffset;
    /// <summary>
    /// 播放坐下动画
    /// </summary>
    /// <param name="target"></param>
    /// <param name="offset"></param>
    public virtual void PlaySit(Transform target, Vector3 offset, bool isRight = true)
    {
        if (isSit)
            return;
        SetSitState(true);
        siteOffset = offset;

        if (isRight)
        {
            facingRight = true;
            transform.rotation = Quaternion.identity;
        }

        rb.linearVelocity = Vector2.zero;
        RB.bodyType = RigidbodyType2D.Kinematic;

        transform.SetParent(target);
        transform.DOJump(target.position + siteOffset, .7f, 1, 0.5f);
        spineCtrl.PlayAnimation(ActorSpineState.sofa_jump.ToString(), false).Complete += (track) =>
        {
            spineCtrl.PlayAnimation(ActorSpineState.sofa_idle.ToString(), true);
        };
    }

    /// <summary>
    /// 停止坐下
    /// </summary>
    public virtual void StopSit()
    {
        SetSitState(false);
        transform.SetParent(originParent);
        RB.bodyType = RigidbodyType2D.Dynamic;
        spineCtrl.PlayAnimation(ActorSpineState.idle.ToString(), true);
    }

    #endregion

    void OnDestroy()
    {
        if (Body != null)
            Body.DOKill();

        if (transform)
            transform.DOKill();
    }
}
