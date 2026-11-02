using Bear.Logger;
using UnityEngine;

/// <summary>
/// 目前是用在 small_dog 上的
/// </summary>
public class DogEnemyCtrl : ActorCtrl, IDebuger
{
    [SerializeField] private GameObject barkPartical;

    [SerializeField] private GameObject idle_bowknot;
    [SerializeField] private GameObject run_bowknot;
    
    [SerializeField] private bool isBarking = false;

    protected override void HandleAnimators()
    {
        if (IsGround)
        {
            // this.Log("-----------" + XInput);
            if (XInput != 0)
            {
                SpineCtrl.PlayAnimation("run", true);
            }
            else
            {
                if (isBarking)
                {
                    SpineCtrl.PlayAnimation("bark".ToString(), true);
                }
                else
                {
                    SpineCtrl.PlayAnimation("idle".ToString(), true);
                }
            }
        }
        else
        {
            if (RB.linearVelocityY > 0)
            {
                SpineCtrl.PlayAnimation("jump_up", false);
            }
            else
            {
                SpineCtrl.PlayAnimation("jump_down", false);
            }
        }

        CheckStateForBowknot();
    }

    private void CheckStateForBowknot()
    {
        if (SpineCtrl == null)
        {
            return;
        }

        var currentAnimation = SpineCtrl.GetCurrentAnimationName();
        var isIdle = string.Equals(currentAnimation, "idle", System.StringComparison.OrdinalIgnoreCase);
        var isRun = string.Equals(currentAnimation, "run", System.StringComparison.OrdinalIgnoreCase);

        UpdateBowknotState(idle_bowknot, isIdle);
        UpdateBowknotState(run_bowknot, isRun);
    }

    private static void UpdateBowknotState(GameObject bowknot, bool shouldBeActive)
    {
        if (bowknot == null || bowknot.activeSelf == shouldBeActive)
        {
            return;
        }

        bowknot.SetActive(shouldBeActive);
    }

    public void Bark()
    {
        isBarking = true;
        barkPartical.SetActive(isBarking);
    }

    public void StopBark()
    {
        isBarking = false;
        barkPartical.SetActive(isBarking);
    }
}
