/// <summary>
/// Level 116「勇往直前」的单次尝试输入规则。
/// 仅记录挡板越线后的首次有效动作、连续右移链和三角障碍无敌资格。
/// </summary>
public sealed class Level116ForwardRule
{
    public bool HasEnteredGameplay { get; private set; }

    public bool IsFirstActionResolved { get; private set; }

    public bool IsRightMoveChainActive { get; private set; }

    public bool HasPermanentlyLostChain { get; private set; }

    public bool IsTriangleObstacleInvulnerable =>
        HasEnteredGameplay &&
        IsFirstActionResolved &&
        IsRightMoveChainActive &&
        !HasPermanentlyLostChain;

    public void ResetAttempt()
    {
        HasEnteredGameplay = false;
        IsFirstActionResolved = false;
        IsRightMoveChainActive = false;
        HasPermanentlyLostChain = false;
    }

    /// <summary>
    /// Actor 主 PolygonCollider2D 完整越过玩法起始挡板时调用一次。
    /// 挡板左侧已经持续持有的单独右移可直接成为首次有效动作。
    /// </summary>
    public bool TryEnterGameplay(bool isRightHeld, bool isLeftHeld)
    {
        if (HasEnteredGameplay)
            return false;

        HasEnteredGameplay = true;
        NotifyHeldInput(isRightHeld, isLeftHeld);
        return true;
    }

    /// <summary>
    /// 同步当前左右输入事实。任何左键优先永久断链；链建立后右键不再持有同样永久断链。
    /// 尚未产生首次有效动作且左右均未持有时保持等待。
    /// </summary>
    public void NotifyHeldInput(bool isRightHeld, bool isLeftHeld)
    {
        if (!HasEnteredGameplay || HasPermanentlyLostChain)
            return;

        if (isLeftHeld)
        {
            BreakChainPermanently();
            return;
        }

        if (!IsFirstActionResolved)
        {
            if (isRightHeld)
            {
                IsFirstActionResolved = true;
                IsRightMoveChainActive = true;
            }

            return;
        }

        if (IsRightMoveChainActive && !isRightHeld)
            BreakChainPermanently();
    }

    /// <summary>
    /// 跳跃是挡板越线后的候选首次动作；右键持续有效时的附加跳跃不破坏既有链。
    /// </summary>
    public void NotifyJump(bool isRightHeld, bool isLeftHeld)
    {
        if (!HasEnteredGameplay || HasPermanentlyLostChain)
            return;

        if (isLeftHeld)
        {
            BreakChainPermanently();
            return;
        }

        if (IsRightMoveChainActive)
        {
            if (!isRightHeld)
                BreakChainPermanently();

            return;
        }

        if (!IsFirstActionResolved)
            BreakChainPermanently();
    }

    /// <summary>
    /// 游戏内弹窗、系统中断或输入设备断开只在已经进入玩法判定后永久断链。
    /// </summary>
    public void NotifyInterruption()
    {
        if (HasEnteredGameplay)
            BreakChainPermanently();
    }

    private void BreakChainPermanently()
    {
        IsFirstActionResolved = true;
        IsRightMoveChainActive = false;
        HasPermanentlyLostChain = true;
    }
}
