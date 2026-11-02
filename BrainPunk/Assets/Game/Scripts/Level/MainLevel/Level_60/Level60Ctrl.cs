using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Level.Actor;
using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// 初始化时 FakeActor 可以移动， DreamActor 不可以移动。
/// 监听PlayerJumpEvent， PlayerRightMoveEvent，PlayerLeftMoveEvent 和 Input.touch。触发的时候 waitingTime = 0;
/// waitingTime 等待 5 s 后开始显示 dreamBubble 对象。对 dreamBubble 包含其子对象进行渐隐渐显操作。
/// dreamBubble 渐显操作花费 2s。中间监听player 操作，一旦操作 waitingTime 重新开始计时，需要重新等待 5s，并且 dreamBubble 快速渐隐。
/// dreamBubble 下有 spriteRenderer 和 spine，都需要控制。
/// 并且 dreamBubble 完成显示后。FakeActor 停止移动。DreamActor 可以移动。并且 BaseLevelCtrl 切换 Actor 控制，设置 DreamActor
///
/// version 2.0 补充
/// 参考 Level55Ctrl 背景变色效果。在显示 dreamBubble 的时候逐渐改变背景颜色。
/// 新增一个 Level60ActorCtrl， 可以参考 Level61ACtorCtrl. fakeActor 角色在准备显示 dreamBubble 之前进入 idle_random animation 状态，并且 loop
/// Level60Ctrl 场景新增了两个 patrol actor。增加两个 point leftPoint 和 rightPoint，actor 超过 leftPoint 后转向想右，反之向左移动。
/// 提供一个 click API 点击后 actor 停止移动并切换为 idle 状态。x 秒后恢复移动。
///
/// </summary>
public class Level60Ctrl : MonoBehaviour
{
    private const string NormalIdleAnimationName = "idle";
    private const string BarkSoundTag = "bark";
    private const float MinPatrolPointDistance = 0.01f;
    private const float DefaultPatrolDirection = 1f;
    private const float BarkInterval = 0.4f;

    [Header("Level Refs")]
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;
    [SerializeField] private ActorCtrl dreamActor;
    [SerializeField] private ActorCtrl fakeActor;
    [SerializeField] private Level60ActorCtrl level60FakeActor;
    [SerializeField] private GameObject dreamBubble;
    [SerializeField] private ActorSkinBinding skinBinding;

    [SerializeField] private SpriteRenderer Bg;

    [SerializeField] private Color dayColor;

    [SerializeField] private Color nightColor;

    [SerializeField] private SpriteRendererGroupAlpha spriteGroupAlpha;
    [SerializeField] private SpriteRendererGroupAlpha actorGroupAlpha;
    [Header("Timing")]
    [SerializeField] private float idleWaitDuration = 5f;
    [SerializeField] private float bubbleFadeInDuration = 2f;
    [SerializeField] private float bubbleFadeOutDuration = 0.15f;

    [Header("Patrol")]
    [SerializeField] private ActorCtrl[] patrolActors;
    [SerializeField] private Transform leftPoint;
    [SerializeField] private Transform rightPoint;
    [SerializeField] private float patrolMoveInput = 1f;
    [SerializeField] private float clickPauseDuration = 3f;

    private EventSubscriber _subscriber;
    private SpriteRenderer[] _bubbleSpriteRenderers;
    private SkeletonRenderer[] _bubbleSkeletonRenderers;
    private SkeletonGraphic[] _bubbleSkeletonGraphics;
    private float[] _spriteOriginalAlphas;
    private float[] _skeletonRendererOriginalAlphas;
    private float[] _skeletonGraphicOriginalAlphas;
    private Tweener _bubbleFadeTween;
    private Tweener _bgColorTween;
    private Tween[] _patrolResumeTweens;
    private float[] _patrolDirections;
    private bool[] _patrolPaused;
    private bool _fallbackFakeActorDreamIdle;
    private float _nextBarkTime;
    private float _waitingTime;
    private float _bubbleAlpha;
    private bool _isSwitchCompleted;
    private bool _isBubbleFadingIn;
    private bool _isTouching;

    private void Awake()
    {
        ResolveReferences();
        CacheBubbleVisuals();
        HideBubbleImmediately();
        ResetBackground(false);
        InitializePatrolState();
    }

    private void RecollectSkinAsync()
    {
        skinBinding.RefreshFromCurrentSelection();
        RefreshActorGroupAlpha();
    }

    private void OnEnable()
    {
        AddListener();
        BindSkinCallbacks();
    }

    private IEnumerator Start()
    {
        ApplyInitialActorState();
        yield return null;
        ApplyInitialActorState();

        RecollectSkinAsync();
    }

    private void Update()
    {
        if (!_isSwitchCompleted)
        {
            CheckTouchInput();

            _waitingTime += Time.deltaTime;
            if (!_isBubbleFadingIn && _waitingTime >= Mathf.Max(0f, idleWaitDuration))
            {
                StartBubbleFadeIn();
            }

            UpdatePreRevealBark();
        }

        // UpdateFallbackFakeActorDreamIdle();
        UpdatePatrolActors();
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        UnbindSkinCallbacks();
        KillBubbleTween();
        KillBackgroundTween();
        KillPatrolResumeTweens();
        StopPreRevealBark();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        UnbindSkinCallbacks();
        KillBubbleTween();
        KillBackgroundTween();
        KillPatrolResumeTweens();
        StopPreRevealBark();
    }

    private void ResolveReferences()
    {
        if (baseLevelCtrl == null)
            baseLevelCtrl = GetComponentInParent<BaseLevelCtrl>() ?? GetComponent<BaseLevelCtrl>();

        Transform root = baseLevelCtrl != null ? baseLevelCtrl.transform : transform;

        if (fakeActor == null)
            fakeActor = FindActorByName(root, "FakeActor");

        if (level60FakeActor == null && fakeActor != null)
            level60FakeActor = fakeActor as Level60ActorCtrl ?? fakeActor.GetComponent<Level60ActorCtrl>();

        if (dreamActor == null)
            dreamActor = FindActorByName(root, "DreamActor");

        if (fakeActor == null && baseLevelCtrl != null && baseLevelCtrl.Actor != dreamActor)
            fakeActor = baseLevelCtrl.Actor;

        if (dreamActor == null && baseLevelCtrl != null && baseLevelCtrl.Actor != fakeActor)
            dreamActor = baseLevelCtrl.Actor;

        if (dreamBubble == null)
        {
            Transform bubble = FindChildByName(root, "dreamBubble") ?? FindChildByName(transform, "dreamBubble");
            if (bubble != null)
                dreamBubble = bubble.gameObject;
        }

        if (leftPoint == null)
            leftPoint = FindChildByName(root, "leftPoint") ?? FindChildByName(transform, "leftPoint");

        if (rightPoint == null)
            rightPoint = FindChildByName(root, "rightPoint") ?? FindChildByName(transform, "rightPoint");
    }

    private void CacheBubbleVisuals()
    {
        if (dreamBubble == null)
        {
            _bubbleSpriteRenderers = System.Array.Empty<SpriteRenderer>();
            _bubbleSkeletonRenderers = System.Array.Empty<SkeletonRenderer>();
            _bubbleSkeletonGraphics = System.Array.Empty<SkeletonGraphic>();
            _spriteOriginalAlphas = System.Array.Empty<float>();
            _skeletonRendererOriginalAlphas = System.Array.Empty<float>();
            _skeletonGraphicOriginalAlphas = System.Array.Empty<float>();
            return;
        }

        var spriteRenderers = new List<SpriteRenderer>();
        SpriteRenderer[] allSpriteRenderers = dreamBubble.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < allSpriteRenderers.Length; i++)
        {
            if (!IsActorVisual(allSpriteRenderers[i]))
                spriteRenderers.Add(allSpriteRenderers[i]);
        }

        var skeletonRenderers = new List<SkeletonRenderer>();
        SkeletonRenderer[] allSkeletonRenderers = dreamBubble.GetComponentsInChildren<SkeletonRenderer>(true);
        for (int i = 0; i < allSkeletonRenderers.Length; i++)
        {
            if (!IsActorVisual(allSkeletonRenderers[i]))
                skeletonRenderers.Add(allSkeletonRenderers[i]);
        }

        var skeletonGraphics = new List<SkeletonGraphic>();
        SkeletonGraphic[] allSkeletonGraphics = dreamBubble.GetComponentsInChildren<SkeletonGraphic>(true);
        for (int i = 0; i < allSkeletonGraphics.Length; i++)
        {
            if (!IsActorVisual(allSkeletonGraphics[i]))
                skeletonGraphics.Add(allSkeletonGraphics[i]);
        }

        _bubbleSpriteRenderers = spriteRenderers.ToArray();
        _bubbleSkeletonRenderers = skeletonRenderers.ToArray();
        _bubbleSkeletonGraphics = skeletonGraphics.ToArray();

        _spriteOriginalAlphas = new float[_bubbleSpriteRenderers.Length];
        for (int i = 0; i < _bubbleSpriteRenderers.Length; i++)
        {
            _spriteOriginalAlphas[i] = _bubbleSpriteRenderers[i] != null ? _bubbleSpriteRenderers[i].color.a : 1f;
        }

        _skeletonRendererOriginalAlphas = new float[_bubbleSkeletonRenderers.Length];
        for (int i = 0; i < _bubbleSkeletonRenderers.Length; i++)
        {
            _skeletonRendererOriginalAlphas[i] = GetSkeletonAlpha(_bubbleSkeletonRenderers[i]);
        }

        _skeletonGraphicOriginalAlphas = new float[_bubbleSkeletonGraphics.Length];
        for (int i = 0; i < _bubbleSkeletonGraphics.Length; i++)
        {
            _skeletonGraphicOriginalAlphas[i] = GetSkeletonGraphicAlpha(_bubbleSkeletonGraphics[i]);
        }
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerAction);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerAction);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerAction);
    }

    private void BindSkinCallbacks()
    {
        if (skinBinding == null)
            return;

        skinBinding.CurrentSelectionApplied -= OnSkinBindingApplied;
        skinBinding.CurrentSelectionApplied += OnSkinBindingApplied;
    }

    private void UnbindSkinCallbacks()
    {
        if (skinBinding != null)
            skinBinding.CurrentSelectionApplied -= OnSkinBindingApplied;
    }

    private void OnSkinBindingApplied(ActorSkinBinding binding)
    {
        if (binding != skinBinding)
            return;

        RefreshActorGroupAlpha();
    }

    private void RefreshActorGroupAlpha()
    {
        if (actorGroupAlpha == null)
            return;

        actorGroupAlpha.CollectRenderers();
        actorGroupAlpha.SetAlpha(_bubbleAlpha);
        dreamActor.gameObject.SetActive(true);
    }

    private void ApplyInitialActorState()
    {
        ResolveReferences();

        _isSwitchCompleted = false;
        _isBubbleFadingIn = false;
        _waitingTime = 0f;
        _isTouching = false;
        ResetBarkTimer();

        if (baseLevelCtrl != null && fakeActor != null)
            baseLevelCtrl.SetActor(fakeActor);

        KillBubbleTween();
        HideBubbleImmediately();
        ResetBackground(false);
        ResetDreamIdleState();
        InitializePatrolState();

        fakeActor?.ResumeMoving();
        fakeActor?.SetMoveInput(0f);

        dreamActor?.StopMoving();
        dreamActor?.SetMoveInput(0f);
    }

    private void OnPlayerAction(PlayerRightMoveEvent evt)
    {
        ResetWaitingByPlayerAction();
    }

    private void OnPlayerAction(PlayerLeftMoveEvent evt)
    {
        ResetWaitingByPlayerAction();
    }

    private void OnPlayerAction(PlayerJumpEvent evt)
    {
        ResetWaitingByPlayerAction();
    }

    private void ResetWaitingByPlayerAction()
    {
        if (_isSwitchCompleted)
            return;

        _waitingTime = 0f;
        if (_bubbleAlpha > 0f || _isBubbleFadingIn)
            StartBubbleFadeOut();
    }

    private void CheckTouchInput()
    {
        bool hasTouch = Input.touchCount > 0;
        if (!hasTouch)
        {
            _isTouching = false;
            return;
        }

        _waitingTime = 0f;
        if (_isTouching)
            return;

        _isTouching = true;
        if (_bubbleAlpha > 0f || _isBubbleFadingIn)
            StartBubbleFadeOut();
    }

    private async Task StartBubbleFadeIn()
    {
        _isBubbleFadingIn = true;
        StopPreRevealBark();
        EnterFakeActorDreamIdle();

        await UniTask.WaitForSeconds(2.5f);

        StartBackgroundFadeIn();
        SetBubbleAlpha(_bubbleAlpha);
        KillBubbleTween();

        AudioManager.PlaySound("level60dream");
        _bubbleFadeTween = DOVirtual.Float(_bubbleAlpha, 1f, Mathf.Max(0f, bubbleFadeInDuration), SetBubbleAlpha)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                _bubbleFadeTween = null;
                _isBubbleFadingIn = false;
                SwitchToDreamActor();
                RemoveStepSound();
            });
    }

    private void StartBubbleFadeOut()
    {
        _isBubbleFadingIn = false;
        if (!_isSwitchCompleted)
            ExitFakeActorDreamIdle();

        StartBackgroundFadeOut();
        KillBubbleTween();

        if (_bubbleAlpha <= 0f)
        {
            HideBubbleImmediately();
            return;
        }

        SetBubbleAlpha(_bubbleAlpha);
        _bubbleFadeTween = DOVirtual.Float(_bubbleAlpha, 0f, Mathf.Max(0f, bubbleFadeOutDuration), SetBubbleAlpha)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                _bubbleFadeTween = null;
                HideBubbleImmediately();
                ResetBarkTimer();
            });
    }

    private void RemoveStepSound()
    {
        for (int i = 0; i < patrolActors.Length; i++)
        {
            patrolActors[i].GetComponent<ActorStepPlayCtrl>().enabled = false;
        }
    }

    private void SwitchToDreamActor()
    {
        if (_isSwitchCompleted)
            return;

        _isSwitchCompleted = true;
        StopPreRevealBark();
        SetBubbleAlpha(1f);

        fakeActor?.SetMoveInput(0f);
        fakeActor?.StopMoving();

        dreamActor?.ResumeMoving();
        dreamActor?.SetMoveInput(0f);

        if (baseLevelCtrl != null && dreamActor != null)
            baseLevelCtrl.SetActor(dreamActor);
    }

    private void HideBubbleImmediately()
    {
        KillBubbleTween();
        _isBubbleFadingIn = false;
        SetBubbleAlpha(0f);
    }

    public void ClickPatrolActor1()
    {
        ClickPatrolActor(0);
    }

    public void ClickPatrolActor2()
    {
        ClickPatrolActor(1);
    }

    public void ClickPatrolActor(int index)
    {
        if (!IsValidPatrolIndex(index))
            return;

        ActorCtrl actor = patrolActors[index];
        if (!IsPatrolActorAvailable(actor))
            return;

        EnsurePatrolRuntimeArrays();
        KillPatrolResumeTween(index);
        _patrolPaused[index] = true;
        StopPatrolActor(actor);

        _patrolResumeTweens[index] = DOVirtual
            .DelayedCall(Mathf.Max(0f, clickPauseDuration), () => ResumePatrolActor(index))
            .SetLink(gameObject);
    }

    public void ClickPatrolActor(ActorCtrl actor)
    {
        if (actor == null || patrolActors == null)
            return;

        for (int i = 0; i < patrolActors.Length; i++)
        {
            if (patrolActors[i] == actor)
            {
                ClickPatrolActor(i);
                return;
            }
        }
    }

    public void OnPatrolActor1Click(Transform target, Vector2 worldPoint)
    {
        ClickPatrolActor1();
    }

    public void OnPatrolActor2Click(Transform target, Vector2 worldPoint)
    {
        ClickPatrolActor2();
    }

    private void EnterFakeActorDreamIdle()
    {
        if (level60FakeActor != null)
        {
            level60FakeActor.EnterDreamIdle();
            _fallbackFakeActorDreamIdle = false;
            return;
        }

        _fallbackFakeActorDreamIdle = true;
        fakeActor?.SetMoveInput(0f);

    }

    private void ExitFakeActorDreamIdle()
    {
        if (level60FakeActor != null)
        {
            level60FakeActor.ExitDreamIdle();
        }
        else if (_fallbackFakeActorDreamIdle)
        {
            fakeActor?.SpineCtrl?.PlayAnimation(NormalIdleAnimationName, true, false);
        }

        _fallbackFakeActorDreamIdle = false;
    }

    private void ResetDreamIdleState()
    {
        level60FakeActor?.ExitDreamIdle();
        if (_fallbackFakeActorDreamIdle)
            fakeActor?.SpineCtrl?.PlayAnimation(NormalIdleAnimationName, true, false);

        _fallbackFakeActorDreamIdle = false;
    }

    private void UpdateFallbackFakeActorDreamIdle()
    {
        if (!_fallbackFakeActorDreamIdle || level60FakeActor != null || fakeActor == null)
            return;

        fakeActor.SetMoveInput(0f);
        // fakeActor.SpineCtrl?.PlayAnimation(DreamIdleAnimationName, true, true);
    }

    private void UpdatePreRevealBark()
    {
        if (!CanPlayPreRevealBark())
        {
            StopPreRevealBark();
            return;
        }

        if (Time.time < _nextBarkTime)
            return;

        AudioManager.PlaySound(BarkSoundTag);
        _nextBarkTime = Time.time + BarkInterval;
    }

    private bool CanPlayPreRevealBark()
    {
        return !_isSwitchCompleted && !_isBubbleFadingIn && _bubbleAlpha <= 0f;
    }

    private void ResetBarkTimer()
    {
        _nextBarkTime = Time.time + BarkInterval;
    }

    private void StopPreRevealBark()
    {
        _nextBarkTime = Time.time + BarkInterval;
    }

    private void StartBackgroundFadeIn()
    {
        if (Bg == null)
            return;

        KillBackgroundTween();
        _bgColorTween = Bg
            .DOColor(nightColor, Mathf.Max(0f, bubbleFadeInDuration))
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                _bgColorTween = null;
            });
    }

    private void StartBackgroundFadeOut()
    {
        if (Bg == null)
            return;

        KillBackgroundTween();
        _bgColorTween = Bg
            .DOColor(dayColor, Mathf.Max(0f, bubbleFadeOutDuration))
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                _bgColorTween = null;
            });
    }

    private void ResetBackground(bool animate)
    {
        if (Bg == null)
            return;

        KillBackgroundTween();

        if (!animate)
        {
            Bg.color = dayColor;
            return;
        }

        StartBackgroundFadeOut();
    }

    private void InitializePatrolState()
    {
        EnsurePatrolRuntimeArrays();
        KillPatrolResumeTweens();

        if (patrolActors == null)
            return;

        for (int i = 0; i < patrolActors.Length; i++)
        {
            _patrolPaused[i] = false;
            _patrolDirections[i] = GetInitialPatrolDirection(patrolActors[i]);
            if (patrolActors[i] != null)
            {
                patrolActors[i].IsEditor = false;
                patrolActors[i].ResumeMoving();
                patrolActors[i].SwitchDash(true);
            }
        }
    }

    private void UpdatePatrolActors()
    {
        if (!HasValidPatrolPoints() || patrolActors == null)
            return;

        EnsurePatrolRuntimeArrays();

        for (int i = 0; i < patrolActors.Length; i++)
        {
            if (_patrolPaused[i])
                continue;

            ActorCtrl actor = patrolActors[i];
            if (!IsPatrolActorAvailable(actor))
                continue;

            UpdatePatrolDirection(i, actor);
            float direction = Mathf.Sign(_patrolDirections[i]);
            actor.SetMoveInput(direction * Mathf.Abs(patrolMoveInput));
            actor.OnUpdate();
        }
    }

    private void UpdatePatrolDirection(int index, ActorCtrl actor)
    {
        float x = actor.transform.position.x;
        if (_patrolDirections[index] >= 0f && x >= rightPoint.position.x)
        {
            _patrolDirections[index] = -1f;
        }
        else if (_patrolDirections[index] <= 0f && x <= leftPoint.position.x)
        {
            _patrolDirections[index] = 1f;
        }

        if (Mathf.Approximately(_patrolDirections[index], 0f))
            _patrolDirections[index] = DefaultPatrolDirection;
    }

    private float GetInitialPatrolDirection(ActorCtrl actor)
    {
        if (actor == null || !HasValidPatrolPoints())
            return DefaultPatrolDirection;

        float leftDistance = Mathf.Abs(actor.transform.position.x - leftPoint.position.x);
        float rightDistance = Mathf.Abs(actor.transform.position.x - rightPoint.position.x);
        return leftDistance <= rightDistance ? 1f : -1f;
    }

    private void ResumePatrolActor(int index)
    {
        if (!IsValidPatrolIndex(index))
            return;

        _patrolResumeTweens[index] = null;
        _patrolPaused[index] = false;

        ActorCtrl actor = patrolActors[index];
        if (!IsPatrolActorAvailable(actor))
            return;

        UpdatePatrolDirection(index, actor);
        actor.ResumeMoving();
        actor.SetMoveInput(Mathf.Sign(_patrolDirections[index]) * Mathf.Abs(patrolMoveInput));
        actor.UpdateAnimation();
    }

    private void StopPatrolActor(ActorCtrl actor)
    {
        if (actor == null)
            return;

        actor.SetMoveInput(0f);
        if (actor.RB != null)
            actor.RB.linearVelocity = new Vector2(0f, actor.RB.linearVelocityY);

        actor.SpineCtrl?.PlayAnimation(NormalIdleAnimationName, true, false);
    }

    private bool HasValidPatrolPoints()
    {
        if (leftPoint == null || rightPoint == null)
            return false;

        return Mathf.Abs(rightPoint.position.x - leftPoint.position.x) > MinPatrolPointDistance;
    }

    private bool IsValidPatrolIndex(int index)
    {
        return patrolActors != null && index >= 0 && index < patrolActors.Length;
    }

    private static bool IsPatrolActorAvailable(ActorCtrl actor)
    {
        return actor != null && actor.isActiveAndEnabled && !actor.IsDied;
    }

    private void EnsurePatrolRuntimeArrays()
    {
        int count = patrolActors != null ? patrolActors.Length : 0;
        if (_patrolDirections != null && _patrolDirections.Length == count)
            return;

        _patrolDirections = new float[count];
        _patrolPaused = new bool[count];
        _patrolResumeTweens = new Tween[count];
    }

    private static ActorCtrl FindActorByName(Transform root, string actorName)
    {
        if (root == null || string.IsNullOrEmpty(actorName))
            return null;

        ActorCtrl[] actors = root.GetComponentsInChildren<ActorCtrl>(true);
        for (int i = 0; i < actors.Length; i++)
        {
            if (IsNameMatch(actors[i], actorName, true))
                return actors[i];
        }

        for (int i = 0; i < actors.Length; i++)
        {
            if (IsNameMatch(actors[i], actorName, false))
                return actors[i];
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (IsNameMatch(children[i], childName, true))
                return children[i];
        }

        for (int i = 0; i < children.Length; i++)
        {
            if (IsNameMatch(children[i], childName, false))
                return children[i];
        }

        return null;
    }

    private static bool IsNameMatch(Component target, string expectedName, bool exactMatch)
    {
        if (target == null || string.IsNullOrEmpty(expectedName))
            return false;

        string targetName = target.gameObject.name;
        if (exactMatch)
            return string.Equals(targetName, expectedName, StringComparison.OrdinalIgnoreCase);

        return targetName.IndexOf(expectedName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsActorVisual(Component target)
    {
        return target != null && target.GetComponentInParent<ActorCtrl>() != null;
    }

    private void SetBubbleAlpha(float alpha)
    {
        _bubbleAlpha = Mathf.Clamp01(alpha);
        dreamActor?.SpineCtrl?.SetAlpha(_bubbleAlpha);
        spriteGroupAlpha.SetAlpha(1 - _bubbleAlpha);
        actorGroupAlpha.SetAlpha(_bubbleAlpha);

        if (_bubbleSpriteRenderers != null)
        {
            for (int i = 0; i < _bubbleSpriteRenderers.Length; i++)
            {
                SpriteRenderer target = _bubbleSpriteRenderers[i];
                if (target == null)
                    continue;

                Color color = target.color;
                float originalAlpha = GetOriginalAlpha(_spriteOriginalAlphas, i);
                color.a = originalAlpha * _bubbleAlpha;
                target.color = color;
            }
        }

        if (_bubbleSkeletonRenderers != null)
        {
            for (int i = 0; i < _bubbleSkeletonRenderers.Length; i++)
            {
                SetSkeletonAlpha(_bubbleSkeletonRenderers[i], GetOriginalAlpha(_skeletonRendererOriginalAlphas, i) * _bubbleAlpha);
            }
        }

        if (_bubbleSkeletonGraphics != null)
        {
            for (int i = 0; i < _bubbleSkeletonGraphics.Length; i++)
            {
                SetSkeletonGraphicAlpha(_bubbleSkeletonGraphics[i], GetOriginalAlpha(_skeletonGraphicOriginalAlphas, i) * _bubbleAlpha);
            }
        }
    }

    private static float GetOriginalAlpha(float[] values, int index)
    {
        if (values == null || index < 0 || index >= values.Length)
            return 1f;

        return values[index];
    }

    private static float GetSkeletonAlpha(SkeletonRenderer target)
    {
        if (target != null && target.Skeleton != null)
            return target.Skeleton.A;

        return 1f;
    }

    private static void SetSkeletonAlpha(SkeletonRenderer target, float alpha)
    {
        if (target != null && target.Skeleton != null)
            target.Skeleton.A = Mathf.Clamp01(alpha);
    }

    private static float GetSkeletonGraphicAlpha(SkeletonGraphic target)
    {
        if (target != null && target.Skeleton != null)
            return target.Skeleton.A;

        return 1f;
    }

    private static void SetSkeletonGraphicAlpha(SkeletonGraphic target, float alpha)
    {
        if (target != null && target.Skeleton != null)
            target.Skeleton.A = Mathf.Clamp01(alpha);
    }

    private void KillBubbleTween()
    {
        if (_bubbleFadeTween == null)
            return;

        _bubbleFadeTween.Kill();
        _bubbleFadeTween = null;
    }

    private void KillBackgroundTween()
    {
        if (_bgColorTween == null)
            return;

        _bgColorTween.Kill();
        _bgColorTween = null;
    }

    private void KillPatrolResumeTweens()
    {
        if (_patrolResumeTweens == null)
            return;

        for (int i = 0; i < _patrolResumeTweens.Length; i++)
        {
            KillPatrolResumeTween(i);
        }
    }

    private void KillPatrolResumeTween(int index)
    {
        if (_patrolResumeTweens == null || index < 0 || index >= _patrolResumeTweens.Length)
            return;

        if (_patrolResumeTweens[index] == null)
            return;

        _patrolResumeTweens[index].Kill();
        _patrolResumeTweens[index] = null;
    }
}
