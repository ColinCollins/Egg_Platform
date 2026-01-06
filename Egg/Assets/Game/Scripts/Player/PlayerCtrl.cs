using System;
using Bear.Logger;
using NUnit.Framework;
using SingularityGroup.HotReload;
using UnityEngine;
using UnityEngine.Rendering.UI;

public class PlayerCtrl : MonoBehaviour, IDebuger
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;

    [SerializeField] private float doubleJumpForce;

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
        if (isGrounded && isAirbornes)
            BecomeAirborne();

        if (!isGrounded && !isAirbornes)
            HandleLanding();
    }

    private void BecomeAirborne()
    {
        isAirbornes = true;
    }

    private void HandleLanding()
    {
        isAirbornes = false;
        canDoubleJump = true;
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

    private void JumpButton()
    {
        if (isGrounded)
        {
            Jump();
        }
        else if (canDoubleJump)
        {
            DoubleJump();
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocityX, jumpForce);
    }

    private void DoubleJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocityX, doubleJumpForce);
        canDoubleJump = false;
    }

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
