using System.Collections.Generic;
using Bear.Logger;
using UnityEngine;

/// <summary>
/// DrawGame 第 5 关射箭循环控制器。
/// 第一笔有效画线结束后驱动 Enemy 重复拉弓射箭，倒计时与胜负仍由共享组件负责。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 5 Ctrl")]
public sealed class DLevel5Ctrl : MonoBehaviour,
    IDrawStrokeCompletedHandler,
    IDebuger
{
    #region Constants

    private const float ReleaseNormalizedTime = 0.55f;
    private const float MinimumRigidbodyMass = 0.001f;
    private const float MaximumLaunchAngleDegrees = 80f;
    private const int MinimumLaunchDistributionSegmentCount = 2;
    private const int MaximumLaunchDistributionSegmentCount = 16;

    #endregion

    #region Inspector

    /// <summary>
    /// 当前 DLevel 根节点上的通用状态控制器。
    /// 调用边界：必须指向同一关卡；只读取暂停/结算状态，不访问跨关卡对象。
    /// </summary>
    [Header("关卡引用")]
    [Tooltip("当前 DLevel 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// DLevel5 内负责拉弓射箭动画的 Enemy 专用 Actor。
    /// 调用边界：必须绑定同一 prefab 内的 DLevel5ActorCtrl。
    /// </summary>
    [Tooltip("负责拉弓、持弓和射箭动画的 DLevel5 Enemy。")]
    [SerializeField] private DLevel5ActorCtrl enemyActor;

    /// <summary>
    /// 箭矢模板；运行时保持隐藏，每次松弦时克隆一份。
    /// 注意事项：模板需包含 Rigidbody2D、碰撞体和已绑定玩家的 DLevel5ArrowTrapHandle。
    /// </summary>
    [Tooltip("每次松弦时克隆的箭矢模板；模板本身运行时隐藏。")]
    [SerializeField] private GameObject arrowTemplate;

    /// <summary>
    /// 一套射箭动作完整结束到下一套动作开始之间的间隔，单位为秒。
    /// 调用边界：仅影响 DLevel5 Enemy 的连续攻击节奏，可在 Inspector 调整。
    /// </summary>
    [Header("射箭节奏")]
    [Tooltip("上一套射箭动作结束后，到下一次拉弓开始的间隔（秒）。")]
    [Min(0f)]
    [SerializeField] private float shootInterval = 0.05f;

    /// <summary>
    /// 拉弓、持弓、射箭动画及动作间隔的统一速度倍率。
    /// 注意事项：倍率会同时加速 Spine 动画并按倒数缩短 shootInterval。
    /// </summary>
    [Tooltip("完整射箭循环的动画速度倍率。")]
    [Min(0.01f)]
    [SerializeField] private float attackSpeedMultiplier = 2.5f;

    /// <summary>
    /// 箭模板父节点局部坐标系中的运行时发射点偏移。
    /// 注意事项：箭改为箭头小碰撞盒后可更靠近弓弦生成，不改变隐藏模板的摆位。
    /// </summary>
    [Header("箭矢发射")]
    [Tooltip("运行时发射点相对箭模板父节点的局部偏移。")]
    [SerializeField] private Vector2 launchOffset = new Vector2(-0.3f, 0.3f);

    /// <summary>
    /// 连续落箭分布范围的离弦速度中点，单位为世界单位/秒。
    /// 注意事项：通过等效 Impulse 发射，不再根据 Player 位置或目标点反算。
    /// </summary>
    [Tooltip("连续落箭分布范围的离弦速度中点。")]
    [Min(0f)]
    [SerializeField] private float launchSpeed = 23f;

    /// <summary>
    /// 连续分布两端相对速度中点的半宽。
    /// 注意事项：近端使用中点减该值，远端使用中点加该值，中间按分层位置连续插值。
    /// </summary>
    [Tooltip("连续落箭范围的速度半宽；0.8 表示从中点 -0.8 连续覆盖到中点 +0.8。")]
    [Min(0f)]
    [SerializeField] private float launchRangeSpeedBandOffset = 0.8f;

    /// <summary>
    /// 每支箭相对当前分层速度的额外随机浮动上限。
    /// 注意事项：非零值会让相邻分层重叠并削弱均匀性；只用于需要额外自然抖动时微调。
    /// </summary>
    [Tooltip("分层速度之外的额外随机浮动；保持 0 可获得最均匀的区域覆盖。")]
    [Min(0f)]
    [SerializeField] private float launchSpeedRandomOffset;

    /// <summary>
    /// 相对世界向左方向的连续分布仰角中点，单位为度。
    /// 注意事项：正值向上；近端与远端会在该中点两侧连续插值，不追踪 Player。
    /// </summary>
    [Tooltip("连续落箭分布范围的发射仰角中点；正值向上。")]
    [Range(-MaximumLaunchAngleDegrees, MaximumLaunchAngleDegrees)]
    [SerializeField] private float launchAngleDegrees = 61f;

    /// <summary>
    /// 连续分布两端相对仰角中点的半宽，单位为度。
    /// 注意事项：近端使用较高仰角，远端使用较低仰角，并与速度共用同一分层位置。
    /// </summary>
    [Tooltip("连续落箭范围的仰角半宽；4 表示从近端高角到远端低角共覆盖 8°。")]
    [Min(0f)]
    [SerializeField] private float launchRangeAngleBandOffsetDegrees = 4f;

    /// <summary>
    /// 每支箭相对当前分层仰角的额外随机浮动上限，单位为度。
    /// 注意事项：非零值会让相邻分层重叠；实际仰角仍限制在 -80° 至 80°。
    /// </summary>
    [Tooltip("分层仰角之外的额外随机浮动；保持 0 可获得最均匀的区域覆盖。")]
    [Min(0f)]
    [SerializeField] private float launchAngleRandomOffsetDegrees;

    /// <summary>
    /// 一轮连续落箭范围划分的分层数量。
    /// 注意事项：每轮会将所有分层随机洗牌，每层只消费一次；数量过大可能超过单局箭数。
    /// </summary>
    [Tooltip("区域均匀分布的分层数；每完整一轮会在每层各随机发射一支箭。")]
    [Range(
        MinimumLaunchDistributionSegmentCount,
        MaximumLaunchDistributionSegmentCount)]
    [SerializeField] private int launchDistributionSegmentCount = 8;

    /// <summary>
    /// 运行时箭刚体的重力倍率。
    /// 注意事项：与发射速度、仰角共同形成自然抛物线；不修改全局 Physics2D 重力。
    /// </summary>
    [Tooltip("箭的重力倍率；越大越快下坠。")]
    [Min(0f)]
    [SerializeField] private float arrowGravityScale = 4f;

    /// <summary>
    /// 运行时箭刚体质量。
    /// 注意事项：速度保持不变；质量越大，命中动态画线时传递的动量越明显。
    /// </summary>
    [Tooltip("箭的物理质量；用于控制命中动态物体时的推动效果。")]
    [Min(MinimumRigidbodyMass)]
    [SerializeField] private float arrowMass = 8f;

    /// <summary>
    /// 箭生成后从透明到原始透明度的渐显时间，单位为秒。
    /// 注意事项：只修改运行时克隆的 SpriteRenderer 颜色，不修改模板或共享材质。
    /// </summary>
    [Header("箭矢表现")]
    [Tooltip("箭生成后的渐显时间；0 表示立即显示。")]
    [Min(0f)]
    [SerializeField] private float arrowFadeInDuration = 0.06f;

    #endregion

    #region Runtime State

    private readonly List<DLevel5ArrowImpactHandle> _spawnedArrows = new();
    private readonly List<int> _launchDistributionSegments = new();
    private bool _isAttacking;
    private bool _isActionPlaying;
    private float _nextShotDelay;
    private int _launchDistributionCursor;
    private int _attemptVersion;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (arrowTemplate != null)
            arrowTemplate.SetActive(false);
        PrepareEnemy();

        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved -= OnLevelResolved;

        StopAttack(false);
        CleanupSpawnedArrows();
    }

    private void Update()
    {
        if (!_isAttacking ||
            _isActionPlaying ||
            drawLevelCtrl == null ||
            drawLevelCtrl.IsPaused)
        {
            return;
        }

        if (_nextShotDelay > 0f)
        {
            _nextShotDelay = Mathf.Max(0f, _nextShotDelay - Time.deltaTime);
            if (_nextShotDelay > 0f)
                return;
        }

        TryStartShot();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后启动 Enemy 连续射箭；后续笔画不会重复启动。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _isAttacking)
            return;

        ResolveReferences();
        if (drawLevelCtrl == null || enemyActor == null || arrowTemplate == null)
        {
            this.LogError("[DLevel5Ctrl] 关卡引用不完整，无法启动连续射箭。");
            return;
        }

        _attemptVersion++;
        _isAttacking = true;
        _isActionPlaying = false;
        _nextShotDelay = 0f;
        ResetLaunchDistribution();
    }

    /// <summary>
    /// 当前尝试重置时停止射箭并销毁本次尝试生成的箭矢。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        StopAttack(true);
        CleanupSpawnedArrows();
    }

    #endregion

    #region Shooting Flow

    private void TryStartShot()
    {
        if (enemyActor == null || !enemyActor.CanPlayArcherySequence)
        {
            _nextShotDelay = GetScaledShootInterval();
            return;
        }

        int currentAttempt = _attemptVersion;
        _isActionPlaying = true;
        bool started = enemyActor.PlayArcherySequence(
            ReleaseNormalizedTime,
            GetAttackSpeedMultiplier(),
            () => SpawnArrow(currentAttempt),
            () => CompleteShot(currentAttempt));
        if (started)
            return;

        _isActionPlaying = false;
        _nextShotDelay = GetScaledShootInterval();
    }

    private void SpawnArrow(int currentAttempt)
    {
        if (!IsCurrentAttempt(currentAttempt) || arrowTemplate == null)
            return;

        PrepareEnemy();
        Transform templateTransform = arrowTemplate.transform;
        Vector2 launchPosition = GetLaunchPosition(templateTransform);
        GameObject arrow = Instantiate(
            arrowTemplate,
            launchPosition,
            Quaternion.identity,
            templateTransform.parent);
        arrow.name = "RuntimeArrow";
        arrow.transform.localScale = templateTransform.localScale;

        Rigidbody2D arrowRigidbody = arrow.GetComponent<Rigidbody2D>();
        if (arrowRigidbody == null)
        {
            this.LogError("[DLevel5Ctrl] 箭矢模板缺少 Rigidbody2D，已销毁本次箭矢。");
            Destroy(arrow);
            return;
        }

        DLevel5ArrowImpactHandle impactHandle =
            arrow.GetComponent<DLevel5ArrowImpactHandle>();
        if (impactHandle == null)
            impactHandle = arrow.AddComponent<DLevel5ArrowImpactHandle>();

        arrow.SetActive(true);
        IgnoreExistingArrowCollisions(impactHandle);

        arrowRigidbody.simulated = true;
        arrowRigidbody.bodyType = RigidbodyType2D.Dynamic;
        arrowRigidbody.useAutoMass = false;
        arrowRigidbody.mass = Mathf.Max(MinimumRigidbodyMass, arrowMass);
        arrowRigidbody.gravityScale = Mathf.Max(0f, arrowGravityScale);
        arrowRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        arrowRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        arrowRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        arrowRigidbody.linearVelocity = Vector2.zero;
        arrowRigidbody.angularVelocity = 0f;
        Vector2 launchVelocity = CalculateLaunchVelocity();
        impactHandle.Initialize(
            arrowRigidbody,
            launchVelocity,
            arrowFadeInDuration);
        arrowRigidbody.AddForce(
            launchVelocity * arrowRigidbody.mass,
            ForceMode2D.Impulse);
        arrowRigidbody.WakeUp();
        _spawnedArrows.Add(impactHandle);
    }

    private void CompleteShot(int currentAttempt)
    {
        if (!IsCurrentAttempt(currentAttempt))
            return;

        _isActionPlaying = false;
        _nextShotDelay = GetScaledShootInterval();
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        StopAttack(true);

        // 失败时保留现场中的箭，确保死亡与淡出阶段仍能看到命中结果；
        // 真正重置或退出关卡时会由 OnDrawAttemptReset / OnDisable 统一清理。
        if (result.Type == DrawLevelResultType.Success)
            CleanupSpawnedArrows();
    }

    private void StopAttack(bool returnEnemyToIdle)
    {
        _attemptVersion++;
        _isAttacking = false;
        _isActionPlaying = false;
        _nextShotDelay = 0f;
        ResetLaunchDistribution();
        enemyActor?.CancelArcheryAction(returnEnemyToIdle);
    }

    private bool IsCurrentAttempt(int currentAttempt)
    {
        return isActiveAndEnabled &&
               _isAttacking &&
               drawLevelCtrl != null &&
               currentAttempt == _attemptVersion;
    }

    private void CleanupSpawnedArrows()
    {
        for (int i = _spawnedArrows.Count - 1; i >= 0; i--)
        {
            DLevel5ArrowImpactHandle arrow = _spawnedArrows[i];
            if (arrow != null)
                Destroy(arrow.gameObject);
        }

        _spawnedArrows.Clear();
    }

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);
    }

    private float GetAttackSpeedMultiplier()
    {
        return Mathf.Max(0.01f, attackSpeedMultiplier);
    }

    private float GetScaledShootInterval()
    {
        return Mathf.Max(0f, shootInterval) / GetAttackSpeedMultiplier();
    }

    private Vector2 GetLaunchPosition(Transform templateTransform)
    {
        if (templateTransform == null)
            return Vector2.zero;

        Transform templateParent = templateTransform.parent;
        Vector2 worldOffset =
            templateParent != null
                ? (Vector2)templateParent.TransformVector(launchOffset)
                : launchOffset;
        return (Vector2)templateTransform.position + worldOffset;
    }

    private Vector2 CalculateLaunchVelocity()
    {
        float distributionPosition = ConsumeLaunchDistributionPosition();
        float speedBandOffset = Mathf.Abs(launchRangeSpeedBandOffset);
        float minimumSpeed = Mathf.Max(0f, launchSpeed - speedBandOffset);
        float maximumSpeed = Mathf.Max(0f, launchSpeed + speedBandOffset);
        float speedBandCenter = Mathf.Lerp(
            minimumSpeed,
            maximumSpeed,
            distributionPosition);
        float speedRandomOffset = Mathf.Abs(launchSpeedRandomOffset);
        float randomizedSpeed = Random.Range(
            Mathf.Max(0f, speedBandCenter - speedRandomOffset),
            Mathf.Max(0f, speedBandCenter + speedRandomOffset));
        float angleBandOffset = Mathf.Abs(launchRangeAngleBandOffsetDegrees);
        float nearRangeAngle = Mathf.Clamp(
            launchAngleDegrees + angleBandOffset,
            -MaximumLaunchAngleDegrees,
            MaximumLaunchAngleDegrees);
        float farRangeAngle = Mathf.Clamp(
            launchAngleDegrees - angleBandOffset,
            -MaximumLaunchAngleDegrees,
            MaximumLaunchAngleDegrees);
        float angleBandCenter = Mathf.Lerp(
            nearRangeAngle,
            farRangeAngle,
            distributionPosition);
        float angleRandomOffset = Mathf.Abs(launchAngleRandomOffsetDegrees);
        float minimumAngle = Mathf.Clamp(
            angleBandCenter - angleRandomOffset,
            -MaximumLaunchAngleDegrees,
            MaximumLaunchAngleDegrees);
        float maximumAngle = Mathf.Clamp(
            angleBandCenter + angleRandomOffset,
            -MaximumLaunchAngleDegrees,
            MaximumLaunchAngleDegrees);
        float randomizedAngle = Random.Range(
            Mathf.Min(minimumAngle, maximumAngle),
            Mathf.Max(minimumAngle, maximumAngle));
        float angleRadians =
            randomizedAngle * Mathf.Deg2Rad;
        Vector2 launchDirection = new Vector2(
            -Mathf.Cos(angleRadians),
            Mathf.Sin(angleRadians));
        return launchDirection * randomizedSpeed;
    }

    private float ConsumeLaunchDistributionPosition()
    {
        int segmentCount = Mathf.Clamp(
            launchDistributionSegmentCount,
            MinimumLaunchDistributionSegmentCount,
            MaximumLaunchDistributionSegmentCount);
        if (_launchDistributionSegments.Count != segmentCount ||
            _launchDistributionCursor >= _launchDistributionSegments.Count)
        {
            RefillLaunchDistributionSegments(segmentCount);
        }

        int segmentIndex =
            _launchDistributionSegments[_launchDistributionCursor++];
        return Mathf.Clamp01(
            (segmentIndex + Random.value) / segmentCount);
    }

    private void RefillLaunchDistributionSegments(int segmentCount)
    {
        _launchDistributionSegments.Clear();
        for (int i = 0; i < segmentCount; i++)
            _launchDistributionSegments.Add(i);

        for (int i = _launchDistributionSegments.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            int currentSegment = _launchDistributionSegments[i];
            _launchDistributionSegments[i] =
                _launchDistributionSegments[randomIndex];
            _launchDistributionSegments[randomIndex] = currentSegment;
        }

        _launchDistributionCursor = 0;
    }

    private void ResetLaunchDistribution()
    {
        _launchDistributionSegments.Clear();
        _launchDistributionCursor = 0;
    }

    private void IgnoreExistingArrowCollisions(
        DLevel5ArrowImpactHandle newArrow)
    {
        if (newArrow == null)
            return;

        for (int i = _spawnedArrows.Count - 1; i >= 0; i--)
        {
            DLevel5ArrowImpactHandle existingArrow = _spawnedArrows[i];
            if (existingArrow == null)
            {
                _spawnedArrows.RemoveAt(i);
                continue;
            }

            newArrow.IgnoreCollisionsWith(existingArrow);
        }
    }

    private void PrepareEnemy()
    {
        if (enemyActor == null)
            return;

        Rigidbody2D enemyRigidbody = enemyActor.GetComponent<Rigidbody2D>();
        if (enemyRigidbody != null)
        {
            enemyRigidbody.linearVelocity = Vector2.zero;
            enemyRigidbody.angularVelocity = 0f;
            enemyRigidbody.simulated = false;
        }

        Collider2D[] enemyColliders = enemyActor.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < enemyColliders.Length; i++)
            enemyColliders[i].enabled = false;
    }

    #endregion
}
