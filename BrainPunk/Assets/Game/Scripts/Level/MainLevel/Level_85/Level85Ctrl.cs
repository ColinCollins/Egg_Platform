using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level85 local audience bubble and suspect-found payoff controller.
/// 
/// version 2.0
/// 不要删改注释
/// 触发 OnFindOutSuspect 后，所有 audience 转向 suspect 等待 2s ，然后再播放跳跃动画
/// 结束动画，触发 knock 时，踢的是 suspect
/// 
/// version 2.1
/// audience 和 suspect 默认进入 celebrate 状态
/// 触发 OnFindOutSuspect 后 audience 和 suspect停止 celebrate。audience 进入 idle 状态 随机左右转动 2 - 3次，间隔为 1s。
/// 然后全部转向 suspect，等待 2s。之后的保持现在内容不变。
/// </summary>
public class Level85Ctrl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bubble;
    [SerializeField] private List<Level85ActorCtrl> audiences;
    [SerializeField] private Level85ActorCtrl suspect;
    [SerializeField] private GameObject fightAnim;
    [SerializeField] private BaseLevelCtrl levelCtrl;

    [Header("Bubble")]
    [SerializeField] private float bubbleScaleDuration = 0.2f;

    [Header("Payoff")]
    [SerializeField] private SpriteRenderer shirt;
    [SerializeField] private ParticleSystem knock;
    [SerializeField] private float audienceJumpDuration = 0.45f;
    [SerializeField] private float audienceJumpPower = 1.5f;
    [SerializeField] private float audienceJumpInterval = 0.08f;
    [SerializeField] private float suspectDelayAfterAudience = 2f;
    [SerializeField] private float suspectKnockForce = 12f;
    [SerializeField] private float suspectKnockDuration = 2f;
    [SerializeField] private float successDelayAfterSuspectKnock = 1f;

    [Header("Version 2.1")]
    [SerializeField] private int randomTurnMinCount = 2;
    [SerializeField] private int randomTurnMaxCount = 3;
    [SerializeField] private float randomTurnInterval = 1f;

    private readonly List<Tween> activeTweens = new List<Tween>();
    private readonly List<ActorInitialState> actorInitialStates = new List<ActorInitialState>();

    private Tween bubbleTween;
    private Sequence payoffSequence;
    private int sessionId;
    private bool hasInitialState;
    private bool payoffStarted;
    private bool payoffCompleted;

    private bool bubbleInitialActive;
    private Vector3 bubbleInitialPosition;
    private Vector3 bubbleInitialScale;
    private bool fightInitialActive;
    private bool shirtInitialActive;

    private void Awake()
    {
        CacheInitialState();
        ResolveLevelCtrl();
    }

    private void OnEnable()
    {
        CacheInitialState();
        CleanupRuntimeState(true, true);
    }

    private void OnDisable()
    {
        CleanupRuntimeState(true, false);
    }

    private void OnDestroy()
    {
        CleanupRuntimeState(true, false);
    }

    public void ClickAudience(Transform trans, Vector2 pos)
    {
        if (!Application.isPlaying || payoffStarted || bubble == null)
        {
            return;
        }

        Vector3 targetPosition = trans != null ? trans.position : new Vector3(pos.x, pos.y, bubble.position.z);
        targetPosition.z = bubble.position.z;

        bubbleTween?.Kill();
        bubble.gameObject.SetActive(true);
        bubble.position = targetPosition;
        bubble.localScale = Vector3.zero;

        bubbleTween = bubble
            .DOScale(GetSafeBubbleScale(), Mathf.Max(0f, bubbleScaleDuration))
            .SetEase(Ease.OutBack)
            .SetLink(gameObject)
            .OnKill(() => bubbleTween = null);
    }

    public void OnFindOutSuspect()
    {
        if (!Application.isPlaying || payoffStarted || payoffCompleted)
        {
            return;
        }

        if (suspect == null)
        {
            return;
        }

        ResolveLevelCtrl();
        CacheInitialState();

        payoffStarted = true;
        int currentSession = ++sessionId;

        bubbleTween?.Kill();
        if (bubble != null)
        {
            bubble.gameObject.SetActive(false);
        }

        StartSuspectFoundSequence(currentSession).Forget();
    }

    private async UniTaskVoid StartSuspectFoundSequence(int currentSession)
    {
        List<Level85ActorCtrl> validAudiences = GetValidAudiences();

        suspect.StopCelebrateToIdle();
        for (int i = 0; i < validAudiences.Count; i++)
        {
            validAudiences[i].StopCelebrateToIdle();
        }

        if (shirt != null)
        {
            shirt.DOFade(0, 0.2f);
        }

        List<UniTask> turnTasks = new List<UniTask>(validAudiences.Count);
        for (int i = 0; i < validAudiences.Count; i++)
        {
            Level85ActorCtrl actor = validAudiences[i];

            actor.FaceTarget(suspect.transform);
            /* turnTasks.Add(actor.PlayRandomFacingSequence(
                suspect.transform,
                turnCount,
                randomTurnInterval,
                currentSession,
                IsCurrentSession)); */
        }

        int turnCount = UnityEngine.Random.Range(
               Mathf.Min(randomTurnMinCount, randomTurnMaxCount),
               Mathf.Max(randomTurnMinCount, randomTurnMaxCount) + 1);
               
        turnTasks.Add(suspect.PlayRandomFacingSequence(
                suspect.transform,
                turnCount,
                randomTurnInterval,
                currentSession,
                IsCurrentSession));

        if (turnTasks.Count > 0)
        {
            await UniTask.WhenAll(turnTasks);
        }

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        validAudiences = GetValidAudiences();
        for (int i = 0; i < validAudiences.Count; i++)
        {
            validAudiences[i].FaceTarget(suspect.transform);
        }

        await StartAudienceJumpAfterTurnDelay(currentSession, validAudiences);
    }

    private async UniTask StartAudienceJumpAfterTurnDelay(int currentSession, List<Level85ActorCtrl> validAudiences)
    {
        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        try
        {
            await UniTask.WaitForSeconds(
                Mathf.Max(0f, suspectDelayAfterAudience),
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        if (fightAnim != null)
        {
            fightAnim.SetActive(true);
            AudioManager.PlaySound("dogFight");
        }

        StartAudienceJumpSequence(currentSession, validAudiences);
    }

    private void StartAudienceJumpSequence(int currentSession, List<Level85ActorCtrl> validAudiences)
    {
        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        if (validAudiences == null || validAudiences.Count == 0)
        {
            KnockSuspectAway(currentSession);
            return;
        }

        int completedCount = 0;
        payoffSequence?.Kill();
        payoffSequence = DOTween.Sequence().SetLink(gameObject).OnKill(() => payoffSequence = null);

        for (int i = 0; i < validAudiences.Count; i++)
        {
            Level85ActorCtrl actor = validAudiences[i];
            float startTime = i * Mathf.Max(0f, audienceJumpInterval);

            payoffSequence.InsertCallback(startTime, () =>
            {
                if (!IsCurrentSession(currentSession) || actor == null)
                {
                    return;
                }

                actor.CancelLevel85Presentation(false);
                actor.StopMoving();
                Tween jumpTween = JumpAudienceToFight(actor, currentSession, () =>
                {
                    completedCount++;
                    if (completedCount >= validAudiences.Count)
                    {
                        KnockSuspectAway(currentSession);
                    }
                });
                TrackTween(jumpTween);
            });
        }
    }

    private Tween JumpAudienceToFight(Level85ActorCtrl actor, int currentSession, Action onComplete)
    {
        Transform actorTransform = actor.transform;
        actorTransform.DOKill();

        if (actor.RB != null)
        {
            actor.RB.linearVelocity = Vector2.zero;
            actor.RB.bodyType = RigidbodyType2D.Kinematic;
        }

        Vector3 targetPosition = GetPayoffTargetPosition();
        targetPosition.z = actorTransform.position.z;

        return actorTransform
            .DOJump(targetPosition, Mathf.Max(0.01f, audienceJumpPower), 1, Mathf.Max(0.01f, audienceJumpDuration))
            .SetEase(Ease.OutQuad)
            .SetLink(actor.gameObject)
            .OnComplete(() =>
            {
                if (!IsCurrentSession(currentSession) || actor == null)
                {
                    return;
                }

                actor.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    private void KnockSuspectAway(int currentSession)
    {
        if (!IsCurrentSession(currentSession) || suspect == null)
        {
            return;
        }

        Vector2 dir = new Vector2(0.3f, 0.5f);
        if (dir.sqrMagnitude <= 0.0001f)
        {
            dir = Vector2.right;
        }
        dir.Normalize();

        suspect.CancelLevel85Presentation(false);
        suspect.StopMoving();

        if (suspect.Body != null)
        {
            Tween rotateTween = suspect.Body
                .DOLocalRotate(new Vector3(0f, 0f, 720f), 0.5f, RotateMode.FastBeyond360)
                .SetLink(suspect.gameObject);
            TrackTween(rotateTween);
        }

        if (suspect.RB != null)
        {
            suspect.Knockback(
                dir,
                Mathf.Max(0.01f, suspectKnockForce),
                Mathf.Max(0.01f, suspectKnockDuration)).Forget();

            AudioManager.PlaySound("footballOut");
        }

        if (knock != null)
        {
            knock.transform.position = suspect.transform.position;
            knock.Play();
        }

        Tween successDelayTween = DOVirtual
            .DelayedCall(Mathf.Max(0f, successDelayAfterSuspectKnock), () => CompleteLevel(currentSession))
            .SetLink(gameObject);
        TrackTween(successDelayTween);
    }

    private Vector3 GetPayoffTargetPosition()
    {
        if (fightAnim != null)
        {
            return fightAnim.transform.position;
        }

        return suspect != null ? suspect.transform.position : transform.position;
    }

    private void CompleteLevel(int currentSession)
    {
        if (!IsCurrentSession(currentSession) || payoffCompleted)
        {
            return;
        }

        payoffCompleted = true;
        ResolveLevelCtrl();
        levelCtrl?.SuccessStrightly();
    }

    private List<Level85ActorCtrl> GetValidAudiences()
    {
        List<Level85ActorCtrl> valid = new List<Level85ActorCtrl>();
        if (audiences == null)
        {
            return valid;
        }

        for (int i = 0; i < audiences.Count; i++)
        {
            Level85ActorCtrl actor = audiences[i];
            if (actor != null && actor.gameObject.activeInHierarchy)
            {
                valid.Add(actor);
            }
        }

        return valid;
    }

    private bool IsCurrentSession(int currentSession)
    {
        return payoffStarted && sessionId == currentSession && isActiveAndEnabled;
    }

    private Vector3 GetSafeBubbleScale()
    {
        if (bubbleInitialScale.sqrMagnitude <= 0.0001f)
        {
            return Vector3.one;
        }

        return bubbleInitialScale;
    }

    private void ResolveLevelCtrl()
    {
        if (levelCtrl != null)
        {
            return;
        }

        levelCtrl = GetComponent<BaseLevelCtrl>();
        if (levelCtrl == null)
        {
            levelCtrl = GetComponentInParent<BaseLevelCtrl>();
        }
    }

    private void CacheInitialState()
    {
        if (hasInitialState)
        {
            return;
        }

        if (bubble != null)
        {
            bubbleInitialActive = bubble.gameObject.activeSelf;
            bubbleInitialPosition = bubble.position;
            bubbleInitialScale = bubble.localScale;
        }

        if (fightAnim != null)
        {
            fightInitialActive = fightAnim.activeSelf;
        }

        if (shirt != null)
        {
            shirtInitialActive = true;
        }

        actorInitialStates.Clear();
        CacheActorState(suspect);
        if (audiences != null)
        {
            for (int i = 0; i < audiences.Count; i++)
            {
                CacheActorState(audiences[i]);
            }
        }

        hasInitialState = true;
    }

    private void CacheActorState(Level85ActorCtrl actor)
    {
        if (actor == null || actorInitialStates.Exists(state => state.Actor == actor))
        {
            return;
        }

        actorInitialStates.Add(new ActorInitialState(actor));
    }

    private void ResetPresentationState(bool startCelebrate)
    {
        payoffStarted = false;
        payoffCompleted = false;

        if (bubble != null)
        {
            bubble.DOKill();
            bubble.gameObject.SetActive(bubbleInitialActive);
            bubble.position = bubbleInitialPosition;
            bubble.localScale = bubbleInitialScale;
        }

        if (fightAnim != null)
        {
            fightAnim.SetActive(fightInitialActive);
        }

        if (shirt != null)
        {
            shirt.DOFade(1, 0.2f);
        }

        if (knock != null)
        {
            knock.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        for (int i = 0; i < actorInitialStates.Count; i++)
        {
            actorInitialStates[i].Restore();
        }

        if (Application.isPlaying && startCelebrate)
        {
            PlayDefaultCelebrateForActors();
        }
    }

    private void PlayDefaultCelebrateForActors()
    {
        List<Level85ActorCtrl> validAudiences = GetValidAudiences();
        for (int i = 0; i < validAudiences.Count; i++)
        {
            validAudiences[i].PlayDefaultCelebrate();
        }

        if (suspect != null && suspect.gameObject.activeInHierarchy)
        {
            suspect.PlayDefaultCelebrate();
        }
    }

    private void CleanupRuntimeState(bool restorePresentation, bool startCelebrate)
    {
        sessionId++;
        payoffStarted = false;

        bubbleTween?.Kill();
        bubbleTween = null;

        payoffSequence?.Kill();
        payoffSequence = null;

        for (int i = activeTweens.Count - 1; i >= 0; i--)
        {
            activeTweens[i]?.Kill();
        }
        activeTweens.Clear();

        for (int i = 0; i < actorInitialStates.Count; i++)
        {
            actorInitialStates[i].CancelPresentation();
        }

        if (restorePresentation)
        {
            ResetPresentationState(startCelebrate);
        }
    }

    private void TrackTween(Tween tween)
    {
        if (tween == null)
        {
            return;
        }

        activeTweens.Add(tween);
        tween.OnKill(() => activeTweens.Remove(tween));
    }

    private readonly struct ActorInitialState
    {
        public readonly Level85ActorCtrl Actor;
        private readonly Transform parent;
        private readonly Transform bodyTransform;
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 localScale;
        private readonly Quaternion bodyLocalRotation;
        private readonly bool active;
        private readonly RigidbodyType2D bodyType;
        private readonly Vector2 velocity;
        private readonly bool hasRigidbody;

        public ActorInitialState(Level85ActorCtrl actor)
        {
            Actor = actor;
            Transform actorTransform = actor.transform;
            parent = actorTransform.parent;
            bodyTransform = actor.Body != null ? actor.Body : actorTransform.Find("spine");
            position = actorTransform.position;
            rotation = actorTransform.rotation;
            localScale = actorTransform.localScale;
            bodyLocalRotation = bodyTransform != null ? bodyTransform.localRotation : Quaternion.identity;
            active = actor.gameObject.activeSelf;
            Rigidbody2D body = actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
            hasRigidbody = body != null;
            bodyType = hasRigidbody ? body.bodyType : RigidbodyType2D.Dynamic;
            velocity = hasRigidbody ? body.linearVelocity : Vector2.zero;
        }

        public void Restore()
        {
            if (Actor == null)
            {
                return;
            }

            Actor.CancelLevel85Presentation(false);

            Transform actorTransform = Actor.transform;
            actorTransform.DOKill();
            actorTransform.SetParent(parent);
            actorTransform.position = position;
            actorTransform.rotation = rotation;
            actorTransform.localScale = localScale;
            Actor.gameObject.SetActive(active);
            Actor.StopKnock();

            Transform actorBody = Actor.Body != null ? Actor.Body : bodyTransform;
            if (actorBody != null)
            {
                actorBody.DOKill();
                actorBody.localRotation = bodyLocalRotation;
            }

            Rigidbody2D body = Actor.RB != null ? Actor.RB : Actor.GetComponent<Rigidbody2D>();
            if (hasRigidbody && body != null)
            {
                body.bodyType = bodyType;
                body.linearVelocity = velocity;
            }
        }

        public void CancelPresentation()
        {
            if (Actor == null)
            {
                return;
            }

            Actor.CancelLevel85Presentation(false);
        }
    }
}
