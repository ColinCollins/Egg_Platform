using System.Collections.Generic;
using Bear.Logger;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DLevel53 蜂巢与蜂群运行控制器，可由多个 Draw 关卡复用。
/// 第一笔有效画线后由蜂巢建立随机批次、均匀攻击通道并生成蜜蜂；同时负责暂停和清理实例。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 53 Hive")]
public sealed class DLevel53Hive : MonoBehaviour,
    IDrawStrokeCompletedHandler,
    IDebuger
{
    #region Inspector References

    /// <summary>
    /// 蜜蜂生成中心点；必须位于蜂巢层级内，为空时回退到蜂巢根节点。
    /// </summary>
    [Header("蜂窝运行时引用")]
    [Tooltip("蜜蜂生成中心点；为空时使用蜂巢根节点。")]
    [SerializeField] private Transform spawnPoint;

    /// <summary>
    /// 运行时蜜蜂实例的父节点；只用于整理层级，不参与目标计算。
    /// </summary>
    [Tooltip("运行时生成蜜蜂的父节点；为空时使用蜂巢根节点。")]
    [SerializeField] private Transform runtimeBeeRoot;

    /// <summary>
    /// 场景中的禁用蜜蜂模板。请直接调整其 DLevel53Bee 参数，运行时克隆会继承这些值。
    /// 注意事项：模板根节点必须保持禁用，避免模板自身刚体参与物理。
    /// </summary>
    [Tooltip("展开 BeeTemplate_AdjustHere 调整蜜蜂速度、攻击路线、推力和反冲参数。")]
    [SerializeField] private DLevel53Bee beeTemplate;

    #endregion

    #region Spawn Settings

    /// <summary>
    /// 每次尝试生成的蜜蜂总数；随机分批不会改变该总数。
    /// </summary>
    [Header("蜂群生成")]
    [Tooltip("第一笔有效画线后，本次尝试生成的蜜蜂总数。")]
    [SerializeField, Min(0)] private int totalBeeCount = 8;

    /// <summary>
    /// 从首批到末批的生成时间窗口，单位为秒；暂停期间不会推进。
    /// </summary>
    [Tooltip("全部蜜蜂分批出现所覆盖的时间窗口，单位为秒。")]
    [SerializeField, Min(0f)] private float spawnWindowSeconds = 3f;

    /// <summary>
    /// 单批随机数量下限；实际值会与上限和剩余数量一起钳制。
    /// </summary>
    [Tooltip("每批蜜蜂数量的随机下限。")]
    [SerializeField, Min(1)] private int minBatchSize = 1;

    /// <summary>
    /// 单批随机数量上限；最后一批只生成剩余数量。
    /// </summary>
    [Tooltip("每批蜜蜂数量的随机上限。")]
    [SerializeField, Min(1)] private int maxBatchSize = 3;

    #endregion

    #region Spawn Distribution

    /// <summary>
    /// 出生点围绕 SpawnPoint 的椭圆半径，X/Y 分别控制横向和纵向随机范围。
    /// </summary>
    [Header("出生分布")]
    [Tooltip("蜜蜂出生椭圆的横向/纵向半径；增大可让出巢位置更分散。")]
    [SerializeField] private Vector2 spawnAreaRadius = new(0.6f, 0.3f);

    /// <summary>
    /// 同批蜜蜂出生位置的期望最小间距；空间不足时选取尝试中最远的位置。
    /// </summary>
    [Tooltip("同批出生点之间的期望最小间距。")]
    [SerializeField, Min(0f)] private float minSpawnSeparation = 0.4f;

    /// <summary>
    /// 每只蜜蜂寻找分散出生点的最大随机尝试次数。
    /// </summary>
    [Tooltip("寻找分散出生点时最多随机多少次。")]
    [SerializeField, Min(1)] private int spawnPositionAttempts = 12;

    #endregion

    #region Attack Distribution

    /// <summary>
    /// 蜂群分层攻击通道相对直扑方向的最大偏角；值越大，撞击位置越分散。
    /// </summary>
    [Header("攻击分布")]
    [Tooltip("最外侧蜜蜂相对直扑方向的最大偏角；调大可扩大蜂群攻击范围。")]
    [SerializeField, Range(0f, 80f)] private float maxAttackAngle = 42f;

    /// <summary>
    /// 每个均匀角度槽位内部的随机抖动比例；不会破坏整体均匀覆盖。
    /// </summary>
    [Tooltip("每个均匀攻击槽内部的随机比例；0 为固定槽中心，1 为整个槽宽。")]
    [SerializeField, Range(0f, 1f)] private float attackAngleJitter = 0.25f;

    #endregion

    #region Runtime State

    private readonly List<SpawnBatch> _spawnPlan = new();
    private readonly List<float> _attackAngles = new();
    private readonly List<DLevel53Bee> _spawnedBees = new();

    private DLevel53Ctrl _owner;
    private ActorCtrl _actor;
    private int _nextBatchIndex;
    private int _nextAttackAngleIndex;
    private int _spawnSequence;
    private float _spawnElapsed;
    private bool _attemptStarted;
    private bool _isSpawning;
    private bool _beesSuspended;

    /// <summary>
    /// 供运行时蜜蜂读取关卡暂停和结算状态。
    /// 首次命中后的延迟失败演出期不属于蜂群暂停。
    /// </summary>
    internal bool IsBeeSimulationSuspended =>
        _owner == null || _owner.IsBeeSimulationSuspended;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        EnsureTemplateInactive();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        ResetSwarmState();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        bool shouldSuspend = IsBeeSimulationSuspended;
        if (shouldSuspend != _beesSuspended)
            SetSimulationSuspended(shouldSuspend);

        if (!_isSpawning || shouldSuspend)
            return;

        _spawnElapsed += Time.deltaTime;
        while (_nextBatchIndex < _spawnPlan.Count &&
               _spawnElapsed >= _spawnPlan[_nextBatchIndex].Time)
        {
            SpawnBeeBatch(_spawnPlan[_nextBatchIndex].Count);
            _nextBatchIndex++;
        }

        if (_nextBatchIndex >= _spawnPlan.Count)
            _isSpawning = false;
    }

    #endregion

    #region Draw Attempt Lifecycle

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying ||
            !result.IsFirstStroke ||
            _attemptStarted)
        {
            return;
        }

        ResolveReferences();
        EnsureTemplateInactive();
        if (_owner == null ||
            _actor == null ||
            beeTemplate == null ||
            IsBeeSimulationSuspended)
        {
            this.LogError("[DLevel53Hive] 蜂巢引用不完整，无法启动蜂群。");
            return;
        }

        _attemptStarted = true;
        BuildSpawnPlan();
        BuildAttackAngles();
        _spawnElapsed = 0f;
        _nextBatchIndex = 0;
        _nextAttackAngleIndex = 0;
        _spawnSequence = 0;
        _isSpawning = _spawnPlan.Count > 0;

        if (!_isSpawning || spawnWindowSeconds > 0f)
            return;

        while (_nextBatchIndex < _spawnPlan.Count)
        {
            SpawnBeeBatch(_spawnPlan[_nextBatchIndex].Count);
            _nextBatchIndex++;
        }

        _isSpawning = false;
    }

    /// <inheritdoc />
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        ResetSwarmState();
    }

    #endregion

    #region Bee Runtime API

    /// <summary>
    /// 同步暂停或恢复全部运行时蜜蜂；仅用于真正暂停、恢复和关卡结算。
    /// </summary>
    internal void SetSimulationSuspended(bool suspended)
    {
        _beesSuspended = suspended;
        for (int i = _spawnedBees.Count - 1; i >= 0; i--)
        {
            DLevel53Bee bee = _spawnedBees[i];
            if (bee == null)
            {
                _spawnedBees.RemoveAt(i);
                continue;
            }

            bee.SetSuspended(suspended);
        }
    }

    /// <summary>
    /// 判断碰撞体是否属于本关受保护玩家；实际判定仍由关卡结算控制器负责。
    /// </summary>
    internal bool IsCurrentPlayerCollider(Collider2D collider)
    {
        return _owner != null && _owner.IsCurrentPlayerCollider(collider);
    }

    /// <summary>
    /// 将真实玩家碰撞转交给关卡结算控制器，蜂巢本身不提交成功或失败结果。
    /// </summary>
    internal bool TryHandlePlayerHit(
        DLevel53Bee sourceBee,
        Collision2D collision,
        Collider2D playerCollider)
    {
        return _owner != null &&
               _owner.TryHandlePlayerHit(sourceBee, collision, playerCollider);
    }

    /// <summary>
    /// 蜜蜂销毁时解除蜂巢运行时登记；禁用模板从不进入该列表。
    /// </summary>
    internal void UnregisterBee(DLevel53Bee bee)
    {
        if (bee != null)
            _spawnedBees.Remove(bee);
    }

    #endregion

    #region Spawn Scheduling

    private void BuildSpawnPlan()
    {
        _spawnPlan.Clear();

        int remaining = Mathf.Max(0, totalBeeCount);
        int actualMin = Mathf.Max(1, minBatchSize);
        int actualMax = Mathf.Max(actualMin, maxBatchSize);
        while (remaining > 0)
        {
            int batchMax = Mathf.Min(actualMax, remaining);
            int batchMin = Mathf.Min(actualMin, batchMax);
            int batchSize = Random.Range(batchMin, batchMax + 1);
            _spawnPlan.Add(new SpawnBatch(0f, batchSize));
            remaining -= batchSize;
        }

        int batchCount = _spawnPlan.Count;
        float window = Mathf.Max(0f, spawnWindowSeconds);
        if (batchCount == 0 || window <= 0f)
            return;

        float slice = window / batchCount;
        for (int i = 0; i < batchCount; i++)
        {
            float sliceStart = slice * i;
            float scheduledTime = Random.Range(
                sliceStart + slice * 0.2f,
                sliceStart + slice * 0.8f);
            _spawnPlan[i] = new SpawnBatch(scheduledTime, _spawnPlan[i].Count);
        }
    }

    private void BuildAttackAngles()
    {
        _attackAngles.Clear();
        int count = Mathf.Max(0, totalBeeCount);
        if (count == 0)
            return;

        float angleLimit = Mathf.Max(0f, maxAttackAngle);
        float normalizedSlotWidth = 2f / count;
        float jitterScale = Mathf.Clamp01(attackAngleJitter);
        for (int i = 0; i < count; i++)
        {
            float slotCenter = -1f + normalizedSlotWidth * (i + 0.5f);
            float slotJitter = Random.Range(-0.5f, 0.5f) *
                               normalizedSlotWidth * jitterScale;
            float normalizedAngle = Mathf.Clamp(
                slotCenter + slotJitter,
                -1f,
                1f);
            _attackAngles.Add(normalizedAngle * angleLimit);
        }

        for (int i = _attackAngles.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (_attackAngles[i], _attackAngles[swapIndex]) =
                (_attackAngles[swapIndex], _attackAngles[i]);
        }
    }

    private void SpawnBeeBatch(int count)
    {
        if (beeTemplate == null || _owner == null)
            return;

        Transform parent = runtimeBeeRoot != null ? runtimeBeeRoot : transform;
        List<Vector2> batchPositions = new(Mathf.Max(0, count));
        for (int i = 0; i < count; i++)
        {
            Vector2 spawnPosition = GetSpawnPosition(batchPositions);
            batchPositions.Add(spawnPosition);

            DLevel53Bee bee = Instantiate(
                beeTemplate,
                spawnPosition,
                Quaternion.identity,
                parent);
            if (bee == null)
                continue;

            ActorCtrl targetActor = _owner.GetRandomTargetActor();
            if (targetActor == null)
            {
                Destroy(bee.gameObject);
                continue;
            }

            bee.gameObject.name = $"Bee_Runtime_{++_spawnSequence:00}";
            float attackAngle = _nextAttackAngleIndex < _attackAngles.Count
                ? _attackAngles[_nextAttackAngleIndex++]
                : Random.Range(-maxAttackAngle, maxAttackAngle);
            bee.Initialize(this, targetActor, attackAngle);
            bee.gameObject.SetActive(true);
            IgnoreExistingBeeCollisions(bee);
            _spawnedBees.Add(bee);
        }
    }

    private void IgnoreExistingBeeCollisions(DLevel53Bee newBee)
    {
        Collider2D newCollider = newBee != null ? newBee.HitCollider : null;
        if (newCollider == null)
            return;

        for (int i = 0; i < _spawnedBees.Count; i++)
        {
            DLevel53Bee existingBee = _spawnedBees[i];
            if (existingBee == null || existingBee.HitCollider == null)
                continue;

            Physics2D.IgnoreCollision(
                newCollider,
                existingBee.HitCollider,
                true);
        }
    }

    private Vector2 GetSpawnPosition(IReadOnlyList<Vector2> reservedPositions)
    {
        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector2 center = origin.position;
        Vector2 radius = new(
            Mathf.Abs(spawnAreaRadius.x),
            Mathf.Abs(spawnAreaRadius.y));
        int attempts = Mathf.Max(1, spawnPositionAttempts);
        float requiredDistanceSqr =
            Mathf.Max(0f, minSpawnSeparation) *
            Mathf.Max(0f, minSpawnSeparation);
        Vector2 bestCandidate = center;
        float bestNearestDistanceSqr = -1f;

        for (int i = 0; i < attempts; i++)
        {
            Vector2 sample = Random.insideUnitCircle;
            Vector2 candidate = center + new Vector2(
                sample.x * radius.x,
                sample.y * radius.y);
            float nearestDistanceSqr = GetNearestSpawnDistanceSqr(
                candidate,
                reservedPositions);
            if (nearestDistanceSqr >= requiredDistanceSqr)
                return candidate;

            if (nearestDistanceSqr > bestNearestDistanceSqr)
            {
                bestNearestDistanceSqr = nearestDistanceSqr;
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    private float GetNearestSpawnDistanceSqr(
        Vector2 candidate,
        IReadOnlyList<Vector2> reservedPositions)
    {
        float nearestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < reservedPositions.Count; i++)
        {
            nearestDistanceSqr = Mathf.Min(
                nearestDistanceSqr,
                (candidate - reservedPositions[i]).sqrMagnitude);
        }

        for (int i = 0; i < _spawnedBees.Count; i++)
        {
            DLevel53Bee bee = _spawnedBees[i];
            if (bee == null)
                continue;

            nearestDistanceSqr = Mathf.Min(
                nearestDistanceSqr,
                (candidate - (Vector2)bee.transform.position).sqrMagnitude);
        }

        return nearestDistanceSqr;
    }

    #endregion

    #region State And Cleanup

    private void ResetSwarmState()
    {
        _attemptStarted = false;
        _isSpawning = false;
        _beesSuspended = false;
        _spawnElapsed = 0f;
        _nextBatchIndex = 0;
        _nextAttackAngleIndex = 0;
        _spawnSequence = 0;
        _spawnPlan.Clear();
        _attackAngles.Clear();

        for (int i = _spawnedBees.Count - 1; i >= 0; i--)
        {
            DLevel53Bee bee = _spawnedBees[i];
            if (bee == null)
                continue;

            bee.gameObject.SetActive(false);
            Destroy(bee.gameObject);
        }

        _spawnedBees.Clear();
        EnsureTemplateInactive();
    }

    private void ResolveReferences()
    {
        if (_owner == null)
            _owner = GetComponentInParent<DLevel53Ctrl>(true);

        if (_actor == null && _owner != null)
            _actor = _owner.TargetActor;

        if (runtimeBeeRoot == null)
            runtimeBeeRoot = transform;

        if (beeTemplate != null)
            return;

        DLevel53Bee[] candidates = GetComponentsInChildren<DLevel53Bee>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (!candidates[i].gameObject.activeSelf)
            {
                beeTemplate = candidates[i];
                break;
            }
        }
    }

    private void EnsureTemplateInactive()
    {
        if (beeTemplate == null || !beeTemplate.gameObject.activeSelf)
            return;

        this.LogWarning(
            "[DLevel53Hive] BeeTemplate_AdjustHere 必须保持禁用，运行时已自动关闭模板。");
        beeTemplate.gameObject.SetActive(false);
    }

    #endregion

    #region Runtime Types

    private readonly struct SpawnBatch
    {
        public SpawnBatch(float time, int count)
        {
            Time = time;
            Count = count;
        }

        public float Time { get; }
        public int Count { get; }
    }

    #endregion
}
