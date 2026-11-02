using System.Threading.Tasks;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.ItemEvent;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 44 controller: keeps the actor frozen while dragged, spawns throw items on a timer,
/// and fires completion callbacks when the drag-spawn count reaches the configured limit.
/// Exposes percent APIs for outside logic to read the remaining drag-spawn progress.
/// 
/// version 2.0 补充 star 逻辑。每次 throw 的时候进行一次 random 测试，大于 0.7 的时候，触发 Star gameObject active = true 并执行。
/// 参考 ThrowItemHandle.JumpTo 逻辑，补充 star 开启后的移动轨迹。
/// </summary>
public class Level44Ctrl : MonoBehaviour, IDebuger
{
    [Header("References")]
    [SerializeField] private Level44ActorCtrl actor;
    [SerializeField] private ThrowItemHandle throwItems;
    [SerializeField] private Level44WeightCtrl weightCtrl;
    [SerializeField] private Transform Bag;

    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private ActorSpineCtrl doorSpineCtrl;

    [Header("Actor")]
    [SerializeField] private float actorMoveSpeed = 5f;
    [SerializeField] private float actorJumpForce = 18f;

    [Header("Weight")]
    [SerializeField] private float defaultWeight = 70f;
    [SerializeField] private float weightAnimationDuration = 0.25f;
    [SerializeField] private Ease weightAnimationEase = Ease.OutQuad;

    [Header("Drag")]
    [SerializeField] private float dragDeadZone = 0.01f;
    [SerializeField] private float dragSpawnInterval = 1f;
    [SerializeField] private int maxSpawnCount = 10;
    [SerializeField] private bool spawnImmediatelyOnDragStart;

    [Header("Completion")]
    [SerializeField] private bool stopSpawningOnComplete = true;

    [Header("light")]
    [SerializeField] private Level38LightItem[] lights;

    [Header("Debug")]
    [SerializeField] private bool enableApiLog = true;


    [SerializeField] private GameObject star;

    private const float StarSpawnRandomThreshold = 0.7f;
    private const float StarJumpDuration = 1f;
    private const float StarJumpPower = 2f;

    private DragableItem _actorDragable;
    private Vector3 _lastDragPosition;
    private bool _isDraggingActor;
    private string _currentDragAnimation;
    private int _spawnCount;
    private float _nextDragSpawnTime;
    private bool _spawnCompleted;
    private bool _isEnterWeight;
    private Tween _weightAnimationTweener;
    private Tween _starJumpTweener;
    private EventSubscriber _subscriber;
    private bool _hasTriggeredStar;

    // DragableItem UnityEvents and this LateUpdate poll can both call OnDragMoving in the same frame.
    // Keeping the frame index avoids duplicate spawn checks and duplicate animation/log updates.
    private int _lastDragMovingFrame = -1;

    public int SpawnCount => _spawnCount;
    public bool IsSpawnCompleted => _spawnCompleted;

    /// <summary>
    /// Remaining drag-spawn progress in [0, 1]. Formula: 1 - (SpawnCount / MaxSpawnCount).
    /// </summary>
    public float Percent => RemainingSpawnPercent;

    /// <summary>
    /// Remaining drag-spawn progress in [0, 1]. 1 means not started; 0 means completed.
    /// </summary>
    public float RemainingSpawnPercent => 1f - SpawnProgressPercent;

    /// <summary>
    /// Completed drag-spawn progress in [0, 1].
    /// </summary>
    public float SpawnProgressPercent => maxSpawnCount <= 0 ? 1f : Mathf.Clamp01((float)_spawnCount / maxSpawnCount);

    private void Awake()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<OnTiggerItemEvent>(OnTiggerItem);

        if (actor != null)
            _actorDragable = actor.GetComponent<DragableItem>();

        _hasTriggeredStar = star != null && star.activeSelf;
        ResetLights();
    }

    private void Start()
    {
        if (actor == null)
        {
            this.LogWarning("[Level44Ctrl] Actor is not assigned.");
            return;
        }

        ResetSpawnProgress();
        ApplyActorWeightPenalty();
    }

    private void LateUpdate()
    {
        if (_spawnCompleted || _actorDragable == null || actor == null)
            return;

        /*         bool isDragging = _actorDragable.IsDragging();
                if (isDragging)
                {
                    if (!_isDraggingActor)
                        OnDragStart(actor.transform);

                    // OnDragMoving(actor.transform);
                }
                else if (_isDraggingActor)
                {
                    OnDragEnd(actor.transform);
                } */
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        StopWeightAnimation();
        StopStarAnimation();

        if (Bag)
            Bag.DOKill();
    }

    void ResetLights()
    {
        if (lights == null) return;
        foreach (var light in lights)
        {
            if (light != null)
                light.TurnOffAll();
        }
    }

    void ShowGreenLights()
    {

        if (lights != null)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].ShowGreenLight();
            }
        }
    }

    async UniTask ShowRedLights()
    {

        // 播放当前阶段对应的红灯闪烁
        if (lights != null)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].PlayRedBlink();
                }
            }
        }

        await UniTask.WaitForSeconds(0.5f);

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
            {
                lights[i].ShowRedLight();
            }
        }
    }

    #region Public API

    public async Task ClickThrowItem()
    {
        LogApi($"{nameof(ClickThrowItem)} spawn item");

        if (Percent <= 0)
            return;

        actor.StopMoving();
        await actor.PlayThrow();
        actor.ResumeMoving();

        if (SpawnThrowItem(false))
            DOWeightAnimation();
    }

    private void OnTiggerItem(OnTiggerItemEvent evt)
    {
        if (evt.EventId != 1)
            return;

        ClickThrowItem();
    }

    /// <summary>
    /// Animates weight from DefaultWeight to the current target weight.
    /// Repeated calls restart the animation and keep the final value at the latest target.
    /// </summary>
    public void DOWeightAnimation()
    {
        if (!_isEnterWeight)
            return;

        if (weightCtrl == null)
        {
            this.LogWarning("[Level44Ctrl] WeightCtrl is not assigned.");
            return;
        }

        float targetWeight = GetCurrentWeight();
        float currentWeight = weightCtrl.CurrentWeight;
        StopWeightAnimation();

        if (weightAnimationDuration <= 0f)
        {
            weightCtrl.SetWeight(targetWeight, false);
            return;
        }

        // 当前重量已在 defaultWeight 附近，直接播放到 targetWeight 的动画
        if (Mathf.Abs(currentWeight - defaultWeight) < 0.01f)
        {
            _weightAnimationTweener = DOVirtual
                .Float(defaultWeight, targetWeight, weightAnimationDuration, value => weightCtrl.SetWeight(value, false))
                .SetEase(weightAnimationEase)
                .OnComplete(() => weightCtrl.SetWeight(targetWeight, false));
            return;
        }

        // 先动画回到 defaultWeight，再动画到 targetWeight
        float halfDuration = weightAnimationDuration * 0.5f;
        _weightAnimationTweener = DOTween.Sequence()
            .Append(DOVirtual.Float(currentWeight, defaultWeight, halfDuration, value => weightCtrl.SetWeight(value, false)).SetEase(weightAnimationEase))
            .Append(DOVirtual.Float(defaultWeight, targetWeight, halfDuration, value => weightCtrl.SetWeight(value, false)).SetEase(weightAnimationEase))
            .OnComplete(() => weightCtrl.SetWeight(targetWeight, false));
    }

    public void OnDragStart()
    {
        if (actor != null)
            OnDragStart(actor.transform);
    }

    public void OnDragStart(Transform target)
    {
        if (_spawnCompleted || target == null)
            return;

        _isDraggingActor = true;
        _lastDragPosition = target.position;
        _currentDragAnimation = string.Empty;
        _lastDragMovingFrame = -1;
        _nextDragSpawnTime = Time.time + (spawnImmediatelyOnDragStart ? 0f : dragSpawnInterval);

        StopActorVelocity();
        LogApi($"{nameof(OnDragStart)} target={target.name}, position={target.position}");
    }

    public void OnDragMoving()
    {
        if (actor != null)
            OnDragMoving(actor.transform);
    }

    /// <summary>
    /// Updates actor drag animation and runs the timed drag-spawn rule.
    /// </summary>
    public void OnDragMoving(Transform target)
    {
        if (!_isDraggingActor || _spawnCompleted || target == null || actor == null)
            return;

        if (_lastDragMovingFrame == Time.frameCount)
            return;

        _lastDragMovingFrame = Time.frameCount;

        var delta = target.position - _lastDragPosition;
        if (delta.sqrMagnitude < dragDeadZone * dragDeadZone)
            return;

        StopActorVelocity();
        LogApi($"{nameof(OnDragMoving)} target={target.name}, position={target.position}, delta={delta}");

        TrySpawnByDrag();

        if (_spawnCompleted)
        {
            _lastDragPosition = target.position;
            return;
        }

        if (Mathf.Abs(delta.x) > dragDeadZone)
        {
            SetActorFacing(delta.x > 0f);
            PlayActorAnimation("walk", true);
        }
        else if (Mathf.Abs(delta.y) > dragDeadZone)
        {
            PlayActorAnimation(delta.y > 0f ? "jump_up" : "jump_down", false);
        }
        else
        {
            PlayActorAnimation("idle", true);
        }

        _lastDragPosition = target.position;
    }

    public void OnDragEnd()
    {
        if (actor != null)
            OnDragEnd(actor.transform);
    }

    public void OnDragEnd(Transform target)
    {
        if (!_isDraggingActor)
            return;

        _isDraggingActor = false;
        StopActorVelocity();
        PlayActorAnimation("idle", true);
        LogApi($"{nameof(OnDragEnd)} target={(target == null ? "null" : target.name)}");
    }

    public void ResetSpawnProgress()
    {
        _spawnCount = 0;
        _spawnCompleted = false;
        _nextDragSpawnTime = Time.time + Mathf.Max(0.01f, dragSpawnInterval);
        LogApi(nameof(ResetSpawnProgress));
    }

    /// <summary>
    /// Method-form API for callers that cannot bind directly to properties.
    /// </summary>
    public float GetPercent()
    {
        return Percent;
    }

    /// <summary>
    /// Player exits the scale with collider context. Intended for trigger/UnityEvent binding.
    /// </summary>
    public void OnPlayerExit(Collider2D collider)
    {
        if (weightCtrl == null)
        {
            this.LogWarning("[Level44Ctrl] WeightCtrl is not assigned.");
            return;
        }

        StopWeightAnimation();
        _isEnterWeight = false;
        weightCtrl.PlayExitWeight(0);

        if (!_spawnCompleted)
            ResetLights();
    }

    /// <summary>
    /// Player enters the scale with collider context. Intended for trigger/UnityEvent binding.
    /// </summary>
    public void OnPlayerEnter(Collider2D collider)
    {
        if (weightCtrl == null)
        {
            this.LogWarning("[Level44Ctrl] WeightCtrl is not assigned.");
            return;
        }

        StopWeightAnimation();
        _isEnterWeight = true;
        weightCtrl.PlayEnterWeight(GetCurrentWeight());
        if (weightCtrl.IsOverWeight)
        {
            ShowRedLights().Forget();
        }
        else
        {
            ShowGreenLights();
        }

        if (_spawnCount >= maxSpawnCount)
            CompleteSpawn();
    }

    #endregion

    #region Spawn

    // Only drag-spawned items count toward completion. Manual button spawns stay independent.
    private void TrySpawnByDrag()
    {
        if (_spawnCompleted || Time.time < _nextDragSpawnTime)
            return;

        if (SpawnThrowItem(true))
        {
            _nextDragSpawnTime = Time.time + Mathf.Max(0.01f, dragSpawnInterval);
            ApplyActorWeightPenalty();
        }
    }

    private bool SpawnThrowItem(bool countForComplete)
    {
        TryTriggerStarFromThrow(actor.transform.position);

        if (_spawnCount >= maxSpawnCount)
            return false;

        if (throwItems == null)
        {
            this.LogWarning("[Level44Ctrl] ThrowItemHandle is not assigned.");
            return false;
        }

        int spawnAmount = countForComplete ? Random.Range(1, 4) : 1;
        int spawnedCount = 0;
        Vector3 starStartPosition = Vector3.zero;
        bool hasStarStartPosition = false;
        for (int i = 0; i < spawnAmount; i++)
        {
            var item = throwItems.SpawnItem();
            if (item == null)
                break;

            if (!hasStarStartPosition)
            {
                starStartPosition = item.transform.position;
                hasStarStartPosition = true;
            }

            spawnedCount++;
        }

        if (spawnedCount <= 0)
            return false;


        if (!countForComplete)
            return true;

        _spawnCount += spawnedCount;
        LogApi($"Spawn count: {_spawnCount}/{maxSpawnCount}, spawned={spawnedCount}");

        return true;
    }

    private void CompleteSpawn()
    {
        if (_spawnCompleted)
            return;

        _spawnCompleted = true;
        _isDraggingActor = false;

        if (stopSpawningOnComplete && throwItems != null)
            throwItems.StopSpawning();

        // StopActorVelocity();
        PlayActorAnimation("idle", true);
        LogApi($"{nameof(CompleteSpawn)} count={_spawnCount}");

        actor.SetMoveSpeed(actorMoveSpeed);
        actor.SetJumpForce(actorJumpForce);

        doorCollider.enabled = true;
        doorSpineCtrl.PlayAnimation("door_open", false);
        AudioManager.PlaySound("openDoor");
    }

    #endregion

    #region Star

    private void TryTriggerStarFromThrow(Vector3 startPos)
    {
        if (_hasTriggeredStar || Random.value <= StarSpawnRandomThreshold)
            return;

        _hasTriggeredStar = true;
        if (star == null)
        {
            this.LogWarning("[Level44Ctrl] Star is not assigned.");
            return;
        }

        Transform starTransform = star.transform;
        Vector3 endPos = starTransform.position;
        StopStarAnimation();
        starTransform.position = startPos;
        star.SetActive(true);

        if (!star.activeInHierarchy)
            return;

        _starJumpTweener = JumpStarTo(starTransform, startPos, endPos, StarJumpDuration, StarJumpPower).OnComplete(() =>
        {
            star.GetComponent<Collider2D>().excludeLayers = LayerMask.NameToLayer("");
            star.GetComponent<MoveFloatHandle>().enabled = true;
        });
    }

    private Tween JumpStarTo(Transform target, Vector3 startPos, Vector3 endPos, float duration, float jumpPower)
    {
        float progress = 0f;
        return DOTween.To(() => progress, x => progress = x, 1f, duration)
            .SetEase(Ease.Linear)
            .SetAutoKill(true)
            .SetTarget(target)
            .OnUpdate(() =>
            {
                if (target == null)
                    return;

                float t = progress;
                float arc = 4f * jumpPower * t * (1f - t);
                target.position = new Vector3(
                    Mathf.Lerp(startPos.x, endPos.x, t),
                    Mathf.Lerp(startPos.y, endPos.y, t) + arc,
                    Mathf.Lerp(startPos.z, endPos.z, t));
            })
            .OnComplete(() =>
            {
                if (target != null)
                    target.position = endPos;

                _starJumpTweener = null;
            });
    }

    private void StopStarAnimation()
    {
        _starJumpTweener?.Kill();
        _starJumpTweener = null;

        if (star != null)
            star.transform.DOKill();
    }

    #endregion

    #region Actor

    private void StopActorVelocity()
    {
        if (actor?.RB != null)
            actor.RB.linearVelocity = Vector2.zero;
    }

    private void SetActorFacing(bool isRight)
    {
        if (actor == null)
            return;

        bool currentlyRight = Mathf.Abs(Mathf.DeltaAngle(actor.transform.eulerAngles.y, 0f)) < 90f;
        if (currentlyRight != isRight)
            actor.SetFaceDir(isRight);
    }

    private void PlayActorAnimation(string animationName, bool loop)
    {
        if (actor?.SpineCtrl == null || _currentDragAnimation == animationName)
            return;

        _currentDragAnimation = animationName;
        actor.SpineCtrl.PlayAnimation(animationName, loop);
    }

    private void ApplyActorWeightPenalty()
    {
        if (actor == null)
            return;

        actor.SetMoveSpeed(actorMoveSpeed - Percent * 4);
        actor.SetJumpForce(actorJumpForce - Percent * 10);

        if (Bag)
        {
            float scale = Percent;
            if (scale < 0.4f && scale >= 0)
                scale = 0.4f;

            Bag.DOScale(scale * 2.5f, 0.5f);

            this.Log(scale.ToString());
        }
    }

    #endregion

    #region Weight

    private float GetCurrentWeight()
    {
        if (weightCtrl == null)
            return defaultWeight;

        // 给空体重留点空间
        var maxWeight = Percent > 0 ? defaultWeight + weightCtrl.MaxWeight * Percent + 15f : defaultWeight;
        return Mathf.Max(defaultWeight, maxWeight);
    }

    private void StopWeightAnimation()
    {
        _weightAnimationTweener?.Kill();
        _weightAnimationTweener = null;
    }

    #endregion

    #region Log

    private void LogApi(string message)
    {
        if (!enableApiLog)
            return;

        this.Log($"[Level44Ctrl] {message}");
    }

    #endregion

    private void OnValidate()
    {
        actorMoveSpeed = Mathf.Max(0f, actorMoveSpeed);
        actorJumpForce = Mathf.Max(0f, actorJumpForce);
        defaultWeight = Mathf.Max(0f, defaultWeight);
        weightAnimationDuration = Mathf.Max(0f, weightAnimationDuration);
        dragDeadZone = Mathf.Max(0f, dragDeadZone);
        dragSpawnInterval = Mathf.Max(0.01f, dragSpawnInterval);
        maxSpawnCount = Mathf.Max(1, maxSpawnCount);
    }
}
