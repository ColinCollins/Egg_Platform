using Game.Level;

/// <summary>
/// Level 129 local result gate. A released arrow may still defeat the boss after
/// the actor dies, but a dead actor can never convert the attempt to success.
/// </summary>
public sealed class Level129BaseLevelCtrl : BaseLevelCtrl
{
    private Level129Ctrl gameplay;
    private bool resultFinalized;

    private void Awake()
    {
        if (Actor == null)
            SetActor(FindFirstObjectByType<Level129ActorCtrl>());
        SetCtrlable(true);
    }

    private void Start()
    {
        AddListener();
        isPause = false;
        Actor?.ResumeMoving();
    }

    public override bool IsSuccess()
    {
        ResolveGameplay();
        return Actor != null &&
               !Actor.IsDied &&
               (gameplay == null || !gameplay.HasPlayerDeathCommitted) &&
               base.IsSuccess();
    }

    public override void CheckFinished()
    {
        ResolveGameplay();
        if (Actor == null || resultFinalized)
            return;

        bool successRequested = IsSuccess();
        bool failureRequested = IsFail() || Actor.IsDied ||
                                (gameplay != null && gameplay.HasPlayerDeathCommitted);
        if (!successRequested && !failureRequested)
            return;

        if (!successRequested &&
            failureRequested &&
            gameplay != null &&
            !gameplay.CanFinalizeFailure)
            return;

        resultFinalized = true;
        base.CheckFinished();
    }

    internal void NotifyDeferredFailureReady()
    {
        CheckFinished();
    }

    private void ResolveGameplay()
    {
        if (gameplay == null)
            gameplay = GetComponent<Level129Ctrl>();
    }
}
