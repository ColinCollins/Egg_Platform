using System.Net;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Level56 main flow controller.
/// </summary>
public class Level56Ctrl : MonoBehaviour
{
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Transform door;
    [SerializeField] private Transform finishPoint;
    [SerializeField] private Level56FlipBirdGameCtrl flipBirdGame;
    [SerializeField] private float finishMoveDuration = 2f;
    [SerializeField] private Ease finishMoveEase = Ease.InOutSine;

    private Level56GamePanelCtrl panelCtrl;
    private bool isPlayingFlipBird;
    private bool isFlipBirdCompleted;
    private float lockedActorX;
    private Tween finishMoveTween;

    private void Start()
    {
        ResolveFinishPoint();
        ResolveFlipBirdGame();
        CacheGamePlayPanel();
        BindFlipBirdGameCallbacks();
    }

    public void OnActorEnterFlipBirdArea(Transform target)
    {
        if (target == null)
        {
            return;
        }

        ActorCtrl targetActor = target.GetComponentInParent<ActorCtrl>();
        if (targetActor == null)
        {
            return;
        }

        if (actor == null)
        {
            actor = targetActor;
        }

        if (targetActor != actor)
        {
            return;
        }

        StartFlipBird();
    }

    public void OnActorEnterFlipBirdArea(Collider2D target)
    {
        OnActorEnterFlipBirdArea(target == null ? null : target.transform);
    }

    private void Update()
    {
        if (!isPlayingFlipBird)
        {
            return;
        }

        LockActorX();
    }

    private void OnDestroy()
    {
        UnbindFlipBirdGameCallbacks();
        finishMoveTween?.Kill();
        finishMoveTween = null;
    }

    private void StartFlipBird()
    {
        ResolveFinishPoint();
        ResolveFlipBirdGame();

        if (actor == null || flipBirdGame == null || isPlayingFlipBird || isFlipBirdCompleted)
        {
            return;
        }

        CacheGamePlayPanel();
        BindFlipBirdGameCallbacks();

        isPlayingFlipBird = true;
        lockedActorX = actor.transform.position.x;

        actor.SetMoveInput(0f);
        if (actor.RB != null)
        {
            Vector2 velocity = actor.RB.linearVelocity;
            velocity.x = 0f;
            actor.RB.linearVelocity = velocity;
        }

        panelCtrl?.SwitchMoveBtns(false);
        panelCtrl?.StartGoal(flipBirdGame.Duration);

        flipBirdGame.SetActor(actor);
        flipBirdGame.StartPlay();
    }

    private void OnFlipBirdSuccess()
    {
        if (!isPlayingFlipBird || isFlipBirdCompleted)
        {
            return;
        }

        isPlayingFlipBird = false;
        isFlipBirdCompleted = true;

        flipBirdGame?.StopPlay();
        panelCtrl?.StopGoal();

        door.transform.localEulerAngles = Vector3.zero;
        door.transform.position = finishPoint.transform.position;

        // MoveActorToFinishPoint();
    }

    private void OnFlipBirdFail()
    {
        if (!isPlayingFlipBird || isFlipBirdCompleted)
        {
            return;
        }

        isPlayingFlipBird = false;
        flipBirdGame?.StopPlay();
        panelCtrl?.StopGoal();
    }

    private void LockActorX()
    {
        if (actor == null)
        {
            return;
        }

        Vector3 actorPosition = actor.transform.position;
        actorPosition.x = lockedActorX;
        actor.transform.position = actorPosition;

        actor.SetMoveInput(0f);
        if (actor.RB != null)
        {
            Vector2 velocity = actor.RB.linearVelocity;
            velocity.x = 0f;
            actor.RB.linearVelocity = velocity;
        }
    }

    private void MoveActorToFinishPoint()
    {
        if (actor == null || finishPoint == null)
        {
            return;
        }

        actor.StopMoving();

        finishMoveTween?.Kill();
        finishMoveTween = actor.transform
            .DOMove(finishPoint.position, Mathf.Max(0f, finishMoveDuration))
            .SetEase(finishMoveEase);
    }

    private void CacheGamePlayPanel()
    {
        if (panelCtrl != null)
        {
            return;
        }

        if (PlayCtrl.Instance?.CurrentGamePlayPanel == null)
        {
            return;
        }

        panelCtrl = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<Level56GamePanelCtrl>();
    }

    private void ResolveFinishPoint()
    {
        if (finishPoint != null)
        {
            return;
        }

        finishPoint = FindChildRecursive(transform, "PathPoint");
        if (finishPoint == null)
        {
            finishPoint = FindChildRecursive(transform, "FinishPoint");
        }
    }

    private void ResolveFlipBirdGame()
    {
        if (flipBirdGame != null)
        {
            return;
        }

        flipBirdGame = GetComponentInChildren<Level56FlipBirdGameCtrl>(true);
    }

    private void BindFlipBirdGameCallbacks()
    {
        if (flipBirdGame == null)
        {
            return;
        }

        flipBirdGame.OnSuccess -= OnFlipBirdSuccess;
        flipBirdGame.OnFail -= OnFlipBirdFail;
        flipBirdGame.OnSuccess += OnFlipBirdSuccess;
        flipBirdGame.OnFail += OnFlipBirdFail;
    }

    private void UnbindFlipBirdGameCallbacks()
    {
        if (flipBirdGame == null)
        {
            return;
        }

        flipBirdGame.OnSuccess -= OnFlipBirdSuccess;
        flipBirdGame.OnFail -= OnFlipBirdFail;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform nestedChild = FindChildRecursive(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }
}
