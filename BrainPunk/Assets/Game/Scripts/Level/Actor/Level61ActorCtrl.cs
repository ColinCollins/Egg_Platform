using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 控制 actor 进入 turning_the_pages 动画状态。
/// </summary>
public class Level61ActorCtrl : ActorCtrl, IDebuger
{
    private const string TurningThePageAnimName = "turning_the_pages";
    private const float SitJumpPower = 0.7f;
    private const float SitJumpDuration = 0.5f;

    private bool isTurningThePage = false;

    public bool IsTurningThePage => isTurningThePage;

    public override void OnUpdate()
    {
        if (isTurningThePage)
        {
            UpdateTurningThePageState();
            return;
        }

        base.OnUpdate();
    }

    protected override void HandleAnimators()
    {
        if (isTurningThePage)
            return;

        base.HandleAnimators();
    }

    public override void PlaySit(Transform target, Vector3 offset, bool isRight = true)
    {
        if (IsSit || IsDied)
            return;

        if (target == null)
        {
            Debug.LogWarning("[Level61ActorCtrl] PlaySit skipped: target is null.");
            return;
        }

        SetSitState(true);
        isTurningThePage = false;
        SetMoveInput(0f);
        StopKnock();

        if (isRight)
        {
            SetActorFaceDirection(true);
        }

        transform.DOKill();

        if (RB != null)
        {
            RB.linearVelocity = Vector2.zero;
            RB.bodyType = RigidbodyType2D.Kinematic;
        }

        transform.SetParent(target);
        transform
            .DOJump(target.position + offset, SitJumpPower, 1, SitJumpDuration)
            .OnComplete(PlaySofaIdleAfterSit);

        if (SpineCtrl != null)
        {
            SpineCtrl.PlayAnimation(ActorSpineState.sofa_jump.ToString(), false, false);
        }
    }

    public override void StopSit()
    {
        if (!IsSit)
        {
            StopTurningThePage();
            return;
        }

        isTurningThePage = false;
        transform.DOKill();
        SetSitState(false);
        transform.SetParent(OriginParent);

        if (RB != null)
        {
            RB.bodyType = RigidbodyType2D.Dynamic;
            RB.linearVelocity = Vector2.zero;
        }

        if (!IsDied && SpineCtrl != null)
        {
            SpineCtrl.PlayAnimation(ActorSpineState.idle.ToString(), true, false);
        }
    }

    public void PlayTurningThePage()
    {
        PlayTurningThePageOnce();
    }

    public void PlayTurningThePageOnce()
    {
        if (IsDied || !HasTurningThePageAnimation())
        {
            return;
        }

        isTurningThePage = true;
        StopHorizontalMotion();

        if (SpineCtrl.PlayAnimation(TurningThePageAnimName, false, false) == null)
        {
            isTurningThePage = false;
            UpdateAnimation();
        }
    }

    public void TriggerTurningThePage()
    {
        PlayTurningThePage();
    }

    public void StopTurningThePage()
    {
        if (!isTurningThePage)
            return;

        isTurningThePage = false;

        if (!IsDied)
        {
            UpdateAnimation();
        }
    }

    private void PlaySofaIdleAfterSit()
    {
        if (IsSit && !IsDied && SpineCtrl != null)
        {
            EnterTurningPage().Forget();
        }
    }

    private async UniTaskVoid EnterTurningPage()
    {
        SpineCtrl.PlayAnimation(ActorSpineState.sofa_idle.ToString(), true, false);
        await UniTask.WaitForSeconds(1f);

        if (IsSit && !IsDied && !isTurningThePage && SpineCtrl != null)
        {
            PlayTurningThePageOnce();
        }
    }

    private void UpdateTurningThePageState()
    {
        if (!IsReady)
            return;

        UpdateAirbornStatus();

        if (IsSit)
        {
            StopAllMotion();
        }
        else
        {
            StopHorizontalMotion();
        }

        HandleCollision();
    }

    private void StopAllMotion()
    {
        SetMoveInput(0f);

        if (RB != null)
        {
            RB.linearVelocity = Vector2.zero;
        }
    }

    private void StopHorizontalMotion()
    {
        SetMoveInput(0f);

        if (RB != null)
        {
            RB.linearVelocity = new Vector2(0f, RB.linearVelocityY);
        }
    }

    private bool HasTurningThePageAnimation()
    {
        if (SpineCtrl == null || SpineCtrl.AnimationState == null)
        {
            Debug.LogWarning("[Level61ActorCtrl] SpineCtrl or AnimationState is missing.");
            return false;
        }

        var skeletonData = SpineCtrl.AnimationState.Data?.SkeletonData;
        if (skeletonData == null)
        {
            Debug.LogWarning("[Level61ActorCtrl] SkeletonData is missing.");
            return false;
        }

        if (skeletonData.FindAnimation(TurningThePageAnimName) != null)
        {
            return true;
        }

        Debug.LogWarning($"[Level61ActorCtrl] Animation not found: {TurningThePageAnimName}");
        return false;
    }

    private void OnDisable()
    {
        isTurningThePage = false;
    }
}
