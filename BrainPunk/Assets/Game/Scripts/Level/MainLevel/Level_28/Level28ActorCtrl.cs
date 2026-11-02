using UnityEngine;

/// <summary>
/// 28 关特殊的角色控制脚本
/// </summary>
public class Level28ActorCtrl : ActorCtrl
{
    [Header("Jump Feature")]
    [SerializeField] private bool infiniteJump;

    [SerializeField] private bool doubleJump;

    [SerializeField] private bool tripleJump;

    
    [SerializeField] private float increaseSpeed = 2;
    
    private int jumpCount = 0;
    
    protected override void JumpButton()
    {
        bool canDoubleJump = doubleJump && jumpCount < 2;
        bool cantripleJump = tripleJump && jumpCount < 3;
        if (infiniteJump || canDoubleJump || cantripleJump)
        {
            Jump();
            jumpCount++;
        }
        else
        {
            base.JumpButton();
        }
    }

    protected override void HandleLanding()
    {
        base.HandleLanding();
        jumpCount = 0;
    }

    public void SwitchDouleJump()
    {
        infiniteJump = false;
        doubleJump = true;
    }

    public void SwitchTripleJump()
    {
        infiniteJump = false;
        tripleJump = true;
    }

    public void AddMoveSpeed()
    {
        MoveSpeed += increaseSpeed;
    }

}
