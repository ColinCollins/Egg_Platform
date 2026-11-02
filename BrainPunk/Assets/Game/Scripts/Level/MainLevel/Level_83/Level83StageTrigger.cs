using UnityEngine;

/// <summary>
/// Level 083 演奏台站立传感器，只在主角由台面顶端承托时报告站立态。
/// </summary>
public sealed class Level83StageTrigger : MonoBehaviour
{
    #region Inspector

    /// <summary>接收演奏台站立态变化的 Level 083 本地控制器。</summary>
    [SerializeField] private Level83GameplayCtrl gameplayCtrl;

    /// <summary>随 Stage 移动、实际承托主角的非 Trigger 台面碰撞体。</summary>
    [SerializeField] private Collider2D stageSurface;

    /// <summary>允许物理接触分离误差的脚底高度容差，不能扩大成侧面触发范围。</summary>
    [SerializeField] private float standingTopTolerance = 0.08f;

    #endregion

    #region Unity Messages

    private void OnTriggerEnter2D(Collider2D other)
    {
        RefreshStandingState(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        RefreshStandingState(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            gameplayCtrl?.SetActorStandingOnStage(false);
    }

    #endregion

    #region Standing Detection

    private void RefreshStandingState(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        gameplayCtrl?.SetActorStandingOnStage(IsStandingOnStage(other));
    }

    private bool IsStandingOnStage(Collider2D actorCollider)
    {
        if (stageSurface == null || actorCollider == null || !stageSurface.IsTouching(actorCollider))
            return false;

        return actorCollider.bounds.min.y >= stageSurface.bounds.max.y - standingTopTolerance;
    }

    #endregion
}
