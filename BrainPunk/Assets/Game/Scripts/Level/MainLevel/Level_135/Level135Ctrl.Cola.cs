using UnityEngine;

/// <summary>135 喷发分支：顾客中招清场，主角中招失败；与原排队失败状态分别管理。</summary>
public sealed partial class Level135Ctrl
{
    #region 喷发绑定与阶段
    /// <summary>本关预置的横喷水柱组件，引用用户提供的两帧素材。</summary>
    [SerializeField] private Level135ColaJet jet;
    /// <summary>本关固定问号和三个感叹号的气泡Prefab，不走共享多语言提示接口。</summary>
    [SerializeField] private Level135ColaAlert colaAlertTemplate;
    private enum ColaPhase { Idle, Question, Alert, Blast, Fade }
    private ColaPhase colaPhase;
    private Level135QueueActor reactionNpc;
    private Level135QueueActor.CupBinding reactionBottle;
    private Level135ColaAlert alertBubble;
    private bool crowdCleared, blastingPlayer, ownsBlastBody;
    private float colaElapsed, playerWaveOffset;
    private RigidbodyType2D previousBodyType;
    private bool previousBodyCollider;
    private const float QuestionDuration = 1.5f;
    private const float AlertDuration = 0.8f;
    private const float BlastDuration = 0.5f;
    private const float FadeDuration = 0.35f;
    #endregion

    #region 到手疑问和惊讶与横向清场
    private void BeginColaReaction(Level135QueueActor npc)
    {
        colaPhase = ColaPhase.Question;
        colaElapsed = 0f;
        reactionNpc = npc;
        blastingPlayer = npc == null;
        reactionBottle = blastingPlayer ? playerCup : npc.Cup;
        colaInput.Clear();
        CloseOrderBubble();
        refilling = false;
        Transform speaker = blastingPlayer ? player.transform : npc.transform;
        alertBubble = Instantiate(colaAlertTemplate, transform);
        alertBubble.name = "Level135_ColaAlert";
        // 先疑问1.5秒，再惊讶；切换和关闭由本关时钟负责，暂停不提前推进。
        alertBubble.ShowQuestion(speaker);
        if (blastingPlayer)
        {
            player.SetMoveInput(0f);
            player.StopMoving();
            levelCtrl.SetCtrlable(false);
        }
    }

    private void TickColaEffect(float dt)
    {
        colaElapsed += dt;
        if (colaPhase == ColaPhase.Question)
        {
            if (colaElapsed < QuestionDuration) return;
            colaPhase = ColaPhase.Alert;
            colaElapsed = 0f;
            alertBubble.ShowExclamation();
            return;
        }
        if (colaPhase == ColaPhase.Alert)
        {
            if (colaElapsed < AlertDuration) return;
            colaPhase = ColaPhase.Blast;
            colaElapsed = 0f;
            if (alertBubble != null) Destroy(alertBubble.gameObject);
            alertBubble = null;
            reactionBottle.ReleaseForSpray(transform);
            Vector3 origin = reactionBottle.Position + Vector3.left * 0.5f;
            jet.Begin(origin, Mathf.Max(8f, origin.x - startDoor.position.x + 6f));
            if (blastingPlayer)
            {
                // 本关短暂接管为纯水平位移，结束后调用原Die进入共享失败链。
                previousBodyType = playerRb.bodyType;
                previousBodyCollider = playerBody.enabled;
                playerWaveOffset = playerBody.bounds.max.x - playerRb.position.x;
                ownsBlastBody = true;
                playerRb.bodyType = RigidbodyType2D.Kinematic;
                playerRb.linearVelocity = Vector2.zero;
                playerBody.enabled = false;
                phase = Phase.Flying;
            }
            else
            {
                crowdCleared = true;
                foreach (Level135QueueActor npc in queue) npc.BeginColaPush();
                foreach (Level135QueueActor npc in leaving) npc.BeginColaPush();
                if (inflight != null) inflight.BeginColaPush();
            }
            return;
        }
        if (colaPhase == ColaPhase.Blast)
        {
            jet.Tick(dt, colaElapsed / BlastDuration);
            PushCrowdWithWave(jet.FrontX);
            if (colaElapsed < BlastDuration) return;
            // 浪头先抵达离屏终点，瓶子和水柱仍完全可见，再进入统一渐隐。
            colaPhase = ColaPhase.Fade;
            colaElapsed = 0f;
            return;
        }
        jet.Tick(dt, 1f);
        float opacity = 1f - Mathf.Clamp01(colaElapsed / FadeDuration);
        jet.SetOpacity(opacity);
        reactionBottle.SetOpacity(opacity);
        if (colaElapsed < FadeDuration) return;
        jet.Hide();
        if (blastingPlayer)
        {
            phase = Phase.Failed;
            player.Die();
            CancelPlayerService();
        }
        else
        {
            // 队首、已买完离场者和在途补位者都已退出屏幕，清空后永久停止补位。
            CancelService();
            RecycleQueue();
        }
        reactionNpc = null;
        reactionBottle = null;
        colaPhase = ColaPhase.Idle;
    }

    private void PushCrowdWithWave(float frontX)
    {
        if (blastingPlayer)
        {
            Vector2 position = playerRb.position;
            position.x = Mathf.Min(position.x, frontX - playerWaveOffset);
            playerRb.position = position;
            return;
        }
        int index = 0;
        foreach (Level135QueueActor npc in queue) npc.PushWithWave(frontX, index++ * 0.14f);
        foreach (Level135QueueActor npc in leaving) npc.PushWithWave(frontX, index++ * 0.14f);
        if (inflight != null) inflight.PushWithWave(frontX, index * 0.14f);
    }

    private void StopColaEffect()
    {
        if (jet != null) jet.Hide();
        if (alertBubble != null) Destroy(alertBubble.gameObject);
        if (ownsBlastBody && playerRb != null)
        {
            playerRb.bodyType = previousBodyType;
            if (playerBody != null) playerBody.enabled = previousBodyCollider;
        }
        ownsBlastBody = false;
        colaPhase = ColaPhase.Idle;
        reactionNpc = null;
        reactionBottle = null;
    }
    #endregion
}
