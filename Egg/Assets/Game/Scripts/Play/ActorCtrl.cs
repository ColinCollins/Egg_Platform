using System;
using Bear.Logger;
using UnityEngine;

public class ActorCtrl : MonoBehaviour, IDebuger
{
    [Header("Basic Move")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;

    [SerializeField] private float doubleJumpForce;

    [Header("Buffer & Coyote Jump")]
    [SerializeField] private float bufferJumpWindow = .25f;
    private float bufferJumpActivated = -1;

    [SerializeField] private float coyoteJumpWindow = .2f;
    private float coyoteJumpActivated = -1;



    [Header("Collision info")]
    [SerializeField] private float GroundCheckDistance;
    [SerializeField] private float WallCheckDistance;
    [SerializeField] private LayerMask whatIsGround;


    #region Temp

    private float xInput;
    private float yInput;

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
    // 面朝 右
    private bool facingRight;
    // 朝向，用于拓展
    private int facingDir = 1;

    #endregion

    private Animator anim;
    private Rigidbody2D rb;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        facingDir = 1;
        facingRight = true;
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateAirbornStatus();
        HandleInput();
        HandleFlip();
        HandleWallSlide();
        HandleMovement();
        HandleCollision();
        HandleAnimators();
    }

    private void UpdateAirbornStatus()
    {
        if (!isGrounded && !isAirbornes)
            BecomeAirborne();

        if (isGrounded && isAirbornes)
            HandleLanding();
    }

    private void BecomeAirborne()
    {
        isAirbornes = true;

        if (rb.linearVelocityY < 0) 
            ActivateCoyoteJump();
    }

    private void HandleLanding()
    {
        isAirbornes = false;
        canDoubleJump = true;

        AttempBufferJump();
    }

    private void HandleMovement()
    {
        if (isWallDetected)
            return;

        rb.linearVelocity = new Vector2(xInput * moveSpeed, rb.linearVelocityY);
    }

    private void HandleInput()
    {
        xInput = Input.GetAxisRaw("Horizontal");
        yInput = Input.GetAxisRaw("Vertical");

        // Temp
        if (Input.GetKeyDown(KeyCode.Space))
        {
            JumpButton();
            RequestBufferJump();
        }
    }

    private void HandleCollision()
    {
        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, GroundCheckDistance, whatIsGround);
        isWallDetected = Physics2D.Raycast(transform.position, Vector2.right * facingDir, WallCheckDistance, whatIsGround);
    }

    private void HandleAnimators()
    {
        // this.Log(xInput.ToString());
        anim.SetFloat("xVelocity", xInput);
        anim.SetFloat("yVelocity", rb.linearVelocityY);
        anim.SetBool("isGrounded", isGrounded);
        anim.SetBool("isWallDetected", isWallDetected);
    }

    private void HandleFlip()
    {
        if (xInput < 0 && facingRight || xInput > 0 && !facingRight)
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

    private void JumpButton()
    {
        // bool coyoteJumpActivated = IsCoyoteJumpAvaliable;
        if (isGrounded || IsCoyoteJumpAvaliable)
        {
            Jump();
        }
        else if (canDoubleJump)
        {
            DoubleJump();
        }

        CancelCoyoteJump();
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocityX, jumpForce);
    }

    // 只有落地之后，才能再次触发
    private void DoubleJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocityX, doubleJumpForce);
        canDoubleJump = false;
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
        Gizmos.DrawLine(pos, new Vector2(pos.x, pos.y - GroundCheckDistance));
        Gizmos.DrawLine(pos, new Vector2(pos.x + WallCheckDistance, pos.y));
    }
}
