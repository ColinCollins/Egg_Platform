using System.Collections;
using Bear.Logger;
using UnityEngine;

/// <summary>
/// 画线完成后先隐藏线段，actor 播放 throwing；到释放帧时线段从 startPoint 飞出并渐显，随后 actor 回到 idle 状态。
/// 提供一个 API 给气球，爆炸的时候调用，触发关卡胜利。
/// 线段被投掷出去，方向和力度暴露给我控制，并在场景中用 Gizmos 显示方向
/// DrawGame 第 7 关由本控制器消费第一笔有效线段，并把上述流程接入 DrawLevelCtrl 的局部生命周期。
///
/// 补充一下
/// 线段不缩小，另外，画的线段触碰到 enemy 的话， enmey 位置 play Knock_Explosion_2 动效，然后变为 Die 状态。游戏失败重置。气球爆炸后等待 2s， 2s 内如果 enemy 死亡，那么重置，不触发胜利
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 7 Ctrl")]
public sealed class DLevel7Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Constants

    private const float VictoryDelaySeconds = 2f;
    private const string EnemyObjectName = "Enemy";
    private const string EnemyTag = "enemy";
    private const string EnemyHitEffectName = "Knock_Explosion_2";
    private const float ForceDirectionGizmoLength = 2f;
    private const float ForceDirectionGizmoHeadLength = 0.35f;
    private const float ForceDirectionGizmoHeadWidth = 0.18f;

    #endregion

    #region Inspector References

    /// <summary>
    /// 当前关卡的画线功能。
    /// 调用边界：只绑定同一 DLevel_007 根节点上的 DrawLineFeature。
    /// </summary>
    [Header("关卡引用")]
    [SerializeField] private DrawLineFeature drawLineFeature;

    /// <summary>
    /// 当前关卡的 Draw 结算控制器。
    /// 调用边界：只用于气球爆炸后提交本关 Success，不直接操作 UI 或存档。
    /// </summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 第 7 关 Actor 专用动画控制器。
    /// 注意事项：throwing 与 idle 动画只能保留在 DLevel7ActorCtrl 内，不得上提共享 ActorCtrl。
    /// </summary>
    [SerializeField] private DLevel7ActorCtrl actorCtrl;

    /// <summary>
    /// 线段释放时的起始点。
    /// 调用边界：绑定 DLevel_007/level7/startPoint；只在 throwing 释放帧写入该世界坐标，并在同一帧启动物理投掷。
    /// </summary>
    [SerializeField] private Transform startPoint;

    /// <summary>
    /// 线段命中后死亡的 Enemy。
    /// 调用边界：只绑定 DLevel_007 内 Tag 为 enemy 的 ActorCtrl，不得指向玩家 Actor。
    /// </summary>
    [Header("敌人命中")]
    [SerializeField] private ActorCtrl enemyActor;

    /// <summary>
    /// Enemy 被线段命中时播放的 Knock_Explosion_2 粒子。
    /// 注意事项：播放前会移动到本次物理碰撞的接触点并保留粒子原 Z；缺失时仍执行 Enemy 死亡和失败重置。
    /// </summary>
    [SerializeField] private ParticleSystem enemyHitEffect;

    #endregion

    #region Throw Settings

    [Header("投掷配置")]
    /// <summary>
    /// 线段投掷冲量的世界二维方向。
    /// 注意事项：运行时与场景 Gizmos 都会使用归一化结果，向量长度不改变 throwForce；
    /// Vector2.zero 表示不施加方向冲量。
    /// </summary>
    [SerializeField] private Vector2 forceDirection = new(0.9063078f, 0.4226183f);

    /// <summary>
    /// 施加给线段 Rigidbody2D 的瞬时冲量。
    /// 注意事项：线段模板质量会影响最终速度，需要在 DLevel_007 PlayMode 中调校。
    /// </summary>
    [SerializeField, Min(0f)] private float throwForce = 260f;

    /// <summary>
    /// 线段从 startPoint 飞出后由透明渐显到原始画线透明度的时间。
    /// 注意事项：设置为 0 时在投掷同帧直接恢复原始透明度，不影响冲量和物理时序。
    /// </summary>
    [SerializeField, Min(0f)] private float throwFadeDuration = 0.2f;

    #endregion

    #region Runtime State

    private Coroutine _victoryDelayCoroutine;
    private Coroutine _strokeFadeCoroutine;
    private GameObject _activeStrokeObject;
    private Rigidbody2D _activeStrokeRigidbody;
    private LineRenderer _activeStrokeRenderer;
    private Gradient _activeStrokeOriginalGradient;
    private DrawLevelCtrl _subscribedDrawLevelCtrl;
    private int _flowVersion;
    private bool _hasStartedThrowFlow;
    private bool _hasReleasedStroke;
    private bool _hasHitBalloon;
    private bool _hasHandledBalloonExplosion;
    private bool _hasEnemyDied;
    private bool _isOutcomeResolved;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        SubscribeToLevelResolved();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        SubscribeToLevelResolved();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        CancelCurrentFlow(false, true);
        UnsubscribeFromLevelResolved();
    }

    private void OnDrawGizmos()
    {
        Vector2 normalizedDirection = GetNormalizedForceDirection();
        if (normalizedDirection == Vector2.zero)
            return;

        Vector3 origin = startPoint != null
            ? startPoint.position
            : transform.position;
        Vector3 direction = new(normalizedDirection.x, normalizedDirection.y, 0f);
        Vector3 perpendicular = new(-normalizedDirection.y, normalizedDirection.x, 0f);
        Vector3 end = origin + direction * ForceDirectionGizmoLength;
        Vector3 arrowBase = end - direction * ForceDirectionGizmoHeadLength;

        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawLine(
            end,
            arrowBase + perpendicular * ForceDirectionGizmoHeadWidth);
        Gizmos.DrawLine(
            end,
            arrowBase - perpendicular * ForceDirectionGizmoHeadWidth);
        Gizmos.color = previousColor;
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后接管并隐藏该线段，再启动 Actor 投掷流程。
    /// 后续笔画或重复回调不会再次启动。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _hasStartedThrowFlow)
            return;

        ResolveReferences();
        if (drawLineFeature == null || startPoint == null)
        {
            this.LogError("[DLevel7Ctrl] DrawLineFeature 或 startPoint 缺失，无法启动线段投掷。");
            return;
        }

        if (!drawLineFeature.TryGetCompletedStrokeObject(result.StrokeIndex, out GameObject strokeObject))
        {
            this.LogError($"[DLevel7Ctrl] 未找到第 {result.StrokeIndex} 笔运行时线段。");
            return;
        }

        Rigidbody2D strokeRigidbody = strokeObject.GetComponent<Rigidbody2D>();
        if (strokeRigidbody == null)
        {
            this.LogError("[DLevel7Ctrl] 已完成线段缺少 Rigidbody2D，无法执行投掷。");
            return;
        }

        _hasStartedThrowFlow = true;
        int currentFlow = ++_flowVersion;
        _activeStrokeObject = strokeObject;
        _activeStrokeRigidbody = strokeRigidbody;

        PrepareStrokeForThrow(strokeObject, strokeRigidbody);
        PrepareStrokeVisualForThrow(strokeObject);
        BeginThrowAnimation(currentFlow);
    }

    /// <summary>
    /// 当前尝试重置时取消尚未完成的投掷回调，并恢复本关一次性门禁。
    /// 运行时线段本体继续由 DrawLineFeature 统一销毁。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        CancelCurrentFlow(true, false);
        _hasHitBalloon = false;
        _hasHandledBalloonExplosion = false;
        _hasEnemyDied = false;
        _isOutcomeResolved = false;
    }

    #endregion

    #region Public API

    /// <summary>
    /// 气球触发器检测到当前投掷线段时立即记录命中，避免后续爆炸事件尚未执行时进入 FallArea 被误判失败。
    /// </summary>
    /// <param name="other">气球触发器检测到的 Draw 层碰撞体。</param>
    public void OnDrawLineHitBalloon(Collider2D other)
    {
        if (!Application.isPlaying ||
            other == null ||
            !_hasReleasedStroke ||
            _hasHitBalloon ||
            _isOutcomeResolved)
        {
            return;
        }

        Rigidbody2D hitRigidbody = other.attachedRigidbody;
        if (hitRigidbody == null ||
            hitRigidbody != _activeStrokeRigidbody ||
            hitRigidbody.gameObject != _activeStrokeObject)
        {
            return;
        }

        _hasHitBalloon = true;
    }

    /// <summary>
    /// 气球爆炸后开启 2 秒判定窗口；窗口内 Enemy 死亡则失败重置，否则提交 DLevel_007 胜利。
    /// 调用边界：只允许由当前 prefab 的气球爆炸执行器调用；同一尝试只处理一次。
    /// </summary>
    public void OnBalloonExploded()
    {
        if (!Application.isPlaying ||
            _hasHandledBalloonExplosion ||
            _isOutcomeResolved)
        {
            return;
        }

        ResolveReferences();
        SubscribeToLevelResolved();
        if (drawLevelCtrl == null)
        {
            this.LogError("[DLevel7Ctrl] DrawLevelCtrl 缺失，气球爆炸后无法提交胜利。");
            return;
        }

        _hasHitBalloon = true;
        _hasHandledBalloonExplosion = true;
        if (_hasEnemyDied || (enemyActor != null && enemyActor.IsDied))
        {
            TryFailForEnemyDeath();
            return;
        }

        int currentFlow = _flowVersion;
        _victoryDelayCoroutine = StartCoroutine(WaitForVictoryWindow(currentFlow));
    }

    /// <summary>
    /// 接收 Enemy 上 LayerCollisionDetector.OnEnter 传入的 Draw 线段物理碰撞并触发失败流程。
    /// 调用边界：只绑定 DLevel_007 Enemy 的 LayerCollisionDetector 动态 Collision2D 事件；
    /// LayerCollisionDetector 必须把目标层配置为 Draw，同一尝试只处理首次有效调用。
    /// </summary>
    /// <param name="collision">LayerCollisionDetector 检测到的 Draw 层碰撞数据。</param>
    public void OnDrawLineHitEnemy(Collision2D collision)
    {
        if (!Application.isPlaying ||
            collision == null ||
            _hasEnemyDied ||
            _isOutcomeResolved)
        {
            return;
        }

        ResolveReferences();
        HandleEnemyHit(collision);
    }

    /// <summary>
    /// 接收本关 FallArea 上 LayerTriggerDetector 传入的 Draw 线段触发器数据。
    /// 调用边界：只绑定 DLevel_007 新增的左右 FallArea，且只在本次投掷尚未命中气球时失败；
    /// 通过 attachedRigidbody 校验触发对象确实是本次投出的运行时线段，忽略其它 Draw 对象。
    /// </summary>
    /// <param name="other">FallArea 检测到的 Draw 层碰撞体。</param>
    public void OnThrownStrokeEnteredFallArea(Collider2D other)
    {
        if (!Application.isPlaying ||
            other == null ||
            !_hasReleasedStroke ||
            _hasHitBalloon ||
            _hasEnemyDied ||
            _isOutcomeResolved)
        {
            return;
        }

        Rigidbody2D enteredRigidbody = other.attachedRigidbody;
        if (enteredRigidbody == null ||
            enteredRigidbody != _activeStrokeRigidbody ||
            enteredRigidbody.gameObject != _activeStrokeObject)
        {
            return;
        }

        ResolveReferences();
        SubscribeToLevelResolved();
        if (drawLevelCtrl == null)
        {
            this.LogError("[DLevel7Ctrl] DrawLevelCtrl 缺失，线段飞出 FallArea 后无法触发失败重置。");
            return;
        }

        if (drawLevelCtrl.TryFailAndReset(
                $"{nameof(DLevel7Ctrl)}.{nameof(OnThrownStrokeEnteredFallArea)}"))
        {
            _isOutcomeResolved = true;
        }
    }

    #endregion

    #region Enemy Hit And Result

    private void HandleEnemyHit(Collision2D collision)
    {
        _hasEnemyDied = true;
        StopVictoryDelay();
        PlayEnemyHitEffect(collision);

        if (enemyActor != null && !enemyActor.IsDied)
            enemyActor.Die();

        TryFailForEnemyDeath();
    }

    private void PlayEnemyHitEffect(Collision2D collision)
    {
        if (enemyHitEffect == null)
        {
            this.LogWarning("[DLevel7Ctrl] Knock_Explosion_2 缺失，Enemy 命中粒子未播放。");
            return;
        }

        enemyHitEffect.transform.position = GetEnemyImpactPosition(collision);

        if (!enemyHitEffect.gameObject.activeSelf)
            enemyHitEffect.gameObject.SetActive(true);

        enemyHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        enemyHitEffect.Play(true);
    }

    private Vector3 GetEnemyImpactPosition(Collision2D collision)
    {
        Vector2 impactPoint;
        if (collision != null && collision.contactCount > 0)
        {
            impactPoint = collision.GetContact(0).point;
        }
        else
        {
            Collider2D enemyCollider = collision != null ? collision.otherCollider : null;
            Collider2D strokeCollider = collision != null ? collision.collider : null;
            if (enemyCollider != null)
            {
                Vector2 strokeCenter = strokeCollider != null
                    ? strokeCollider.bounds.center
                    : enemyHitEffect.transform.position;
                impactPoint = enemyCollider.ClosestPoint(strokeCenter);
            }
            else if (enemyActor != null)
            {
                impactPoint = enemyActor.transform.position;
            }
            else
            {
                impactPoint = enemyHitEffect.transform.position;
            }
        }

        Vector3 effectPosition = enemyHitEffect.transform.position;
        effectPosition.x = impactPoint.x;
        effectPosition.y = impactPoint.y;
        return effectPosition;
    }

    private void TryFailForEnemyDeath()
    {
        if (_isOutcomeResolved)
            return;

        if (drawLevelCtrl == null)
        {
            this.LogError("[DLevel7Ctrl] DrawLevelCtrl 缺失，Enemy 死亡后无法触发失败重置。");
            return;
        }

        drawLevelCtrl.TryFailAndReset(
            $"{nameof(DLevel7Ctrl)}.{nameof(HandleEnemyHit)}");
    }

    private IEnumerator WaitForVictoryWindow(int currentFlow)
    {
        float elapsedSeconds = 0f;
        while (elapsedSeconds < VictoryDelaySeconds)
        {
            if (!IsVictoryWindowCurrent(currentFlow))
            {
                _victoryDelayCoroutine = null;
                yield break;
            }

            if (_hasEnemyDied || (enemyActor != null && enemyActor.IsDied))
            {
                _victoryDelayCoroutine = null;
                TryFailForEnemyDeath();
                yield break;
            }

            elapsedSeconds += Time.deltaTime;
            yield return null;
        }

        _victoryDelayCoroutine = null;
        if (!IsVictoryWindowCurrent(currentFlow))
            yield break;

        _isOutcomeResolved = drawLevelCtrl.TryResolve(
            new DrawLevelResult(
                DrawLevelResultType.Success,
                $"{nameof(DLevel7Ctrl)}.{nameof(OnBalloonExploded)}"));
    }

    private bool IsVictoryWindowCurrent(int currentFlow)
    {
        return isActiveAndEnabled &&
               _hasHandledBalloonExplosion &&
               !_isOutcomeResolved &&
               currentFlow == _flowVersion &&
               drawLevelCtrl != null;
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        _isOutcomeResolved = true;
        StopVictoryDelay();
        StopStrokeFade(false);
    }

    private void StopVictoryDelay()
    {
        if (_victoryDelayCoroutine == null)
            return;

        StopCoroutine(_victoryDelayCoroutine);
        _victoryDelayCoroutine = null;
    }

    #endregion

    #region Stroke Placement And Throw

    private static void PrepareStrokeForThrow(
        GameObject strokeObject,
        Rigidbody2D strokeRigidbody)
    {
        strokeRigidbody.bodyType = RigidbodyType2D.Dynamic;
        strokeRigidbody.simulated = false;
        strokeRigidbody.linearVelocity = Vector2.zero;
        strokeRigidbody.angularVelocity = 0f;

        RecenterStrokePivot(strokeObject);
    }

    private static void RecenterStrokePivot(GameObject strokeObject)
    {
        LineRenderer lineRenderer = strokeObject.GetComponent<LineRenderer>();
        if (lineRenderer == null || lineRenderer.positionCount <= 0 || lineRenderer.useWorldSpace)
            return;

        int pointCount = lineRenderer.positionCount;
        var points = new Vector3[pointCount];
        lineRenderer.GetPositions(points);

        Vector3 minimum = points[0];
        Vector3 maximum = points[0];
        for (int i = 1; i < pointCount; i++)
        {
            minimum = Vector3.Min(minimum, points[i]);
            maximum = Vector3.Max(maximum, points[i]);
        }

        Vector3 localCenter = (minimum + maximum) * 0.5f;
        Vector3 worldCenter = strokeObject.transform.TransformPoint(localCenter);
        for (int i = 0; i < pointCount; i++)
            points[i] -= localCenter;

        lineRenderer.SetPositions(points);

        EdgeCollider2D edgeCollider = strokeObject.GetComponent<EdgeCollider2D>();
        if (edgeCollider != null)
        {
            Vector2[] colliderPoints = edgeCollider.points;
            Vector2 colliderOffset = new(localCenter.x, localCenter.y);
            for (int i = 0; i < colliderPoints.Length; i++)
                colliderPoints[i] -= colliderOffset;

            edgeCollider.points = colliderPoints;
        }

        for (int i = 0; i < strokeObject.transform.childCount; i++)
        {
            Transform child = strokeObject.transform.GetChild(i);
            if (child.GetComponent<Collider2D>() != null)
                child.localPosition -= localCenter;
        }

        strokeObject.transform.position = worldCenter;
    }

    private void BeginThrowAnimation(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        if (actorCtrl != null &&
            actorCtrl.PlayThrowSequence(
                () => ThrowStroke(currentFlow),
                null))
        {
            return;
        }

        this.LogWarning("[DLevel7Ctrl] Actor throwing 动画无法启动，线段将直接按配置投出。");
        ThrowStroke(currentFlow);
    }

    private void ThrowStroke(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow) || _activeStrokeRigidbody == null)
            return;

        Vector2 direction = GetNormalizedForceDirection();

        _activeStrokeObject.transform.position = startPoint.position;
        if (_activeStrokeRenderer != null)
            _activeStrokeRenderer.enabled = true;

        _activeStrokeRigidbody.linearVelocity = Vector2.zero;
        _activeStrokeRigidbody.angularVelocity = 0f;
        _activeStrokeRigidbody.simulated = true;
        _hasReleasedStroke = true;
        _activeStrokeRigidbody.AddForce(
            direction * Mathf.Max(0f, throwForce),
            ForceMode2D.Impulse);
        _activeStrokeRigidbody.WakeUp();
        BeginStrokeFade(currentFlow);
    }

    private Vector2 GetNormalizedForceDirection()
    {
        return forceDirection.sqrMagnitude > Mathf.Epsilon
            ? forceDirection.normalized
            : Vector2.zero;
    }

    private void PrepareStrokeVisualForThrow(GameObject strokeObject)
    {
        StopStrokeFade(true);
        _activeStrokeRenderer = strokeObject.GetComponent<LineRenderer>();
        if (_activeStrokeRenderer == null)
        {
            this.LogWarning("[DLevel7Ctrl] 已完成线段缺少 LineRenderer，无法播放投掷渐显。");
            return;
        }

        _activeStrokeOriginalGradient = CloneGradient(
            _activeStrokeRenderer.colorGradient);
        ApplyStrokeAlpha(
            _activeStrokeRenderer,
            _activeStrokeOriginalGradient,
            0f);
        _activeStrokeRenderer.enabled = false;
    }

    private void BeginStrokeFade(int currentFlow)
    {
        if (_activeStrokeRenderer == null ||
            _activeStrokeOriginalGradient == null)
        {
            return;
        }

        if (throwFadeDuration <= 0f)
        {
            _activeStrokeRenderer.colorGradient = _activeStrokeOriginalGradient;
            return;
        }

        _strokeFadeCoroutine = StartCoroutine(FadeInStroke(
            currentFlow,
            _activeStrokeRenderer,
            _activeStrokeOriginalGradient));
    }

    private IEnumerator FadeInStroke(
        int currentFlow,
        LineRenderer strokeRenderer,
        Gradient originalGradient)
    {
        // 先等待一次物理步进及随后的渲染更新，保证首个非零透明度帧已经离开 startPoint。
        yield return new WaitForFixedUpdate();
        yield return null;
        if (!IsCurrentFlow(currentFlow) || strokeRenderer == null)
        {
            _strokeFadeCoroutine = null;
            yield break;
        }

        float elapsedSeconds = 0f;
        while (elapsedSeconds < throwFadeDuration)
        {
            if (!IsCurrentFlow(currentFlow) || strokeRenderer == null)
            {
                _strokeFadeCoroutine = null;
                yield break;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                yield return null;
                continue;
            }

            elapsedSeconds += deltaTime;
            ApplyStrokeAlpha(
                strokeRenderer,
                originalGradient,
                Mathf.Clamp01(elapsedSeconds / throwFadeDuration));
            yield return null;
        }

        if (strokeRenderer != null && currentFlow == _flowVersion)
            strokeRenderer.colorGradient = originalGradient;

        _strokeFadeCoroutine = null;
    }

    private void StopStrokeFade(bool restoreOriginalGradient)
    {
        if (_strokeFadeCoroutine != null)
        {
            StopCoroutine(_strokeFadeCoroutine);
            _strokeFadeCoroutine = null;
        }

        if (restoreOriginalGradient &&
            _activeStrokeRenderer != null &&
            _activeStrokeOriginalGradient != null)
        {
            _activeStrokeRenderer.colorGradient = _activeStrokeOriginalGradient;
            _activeStrokeRenderer.enabled = true;
        }
    }

    private static Gradient CloneGradient(Gradient source)
    {
        var clone = new Gradient
        {
            mode = source.mode
        };
        clone.SetKeys(source.colorKeys, source.alphaKeys);
        return clone;
    }

    private static void ApplyStrokeAlpha(
        LineRenderer strokeRenderer,
        Gradient originalGradient,
        float alphaMultiplier)
    {
        GradientAlphaKey[] alphaKeys = originalGradient.alphaKeys;
        float clampedMultiplier = Mathf.Clamp01(alphaMultiplier);
        for (int i = 0; i < alphaKeys.Length; i++)
            alphaKeys[i].alpha *= clampedMultiplier;

        var fadedGradient = new Gradient
        {
            mode = originalGradient.mode
        };
        fadedGradient.SetKeys(originalGradient.colorKeys, alphaKeys);
        strokeRenderer.colorGradient = fadedGradient;
    }

    #endregion

    #region Flow And References

    private bool IsCurrentFlow(int currentFlow)
    {
        return isActiveAndEnabled &&
               _hasStartedThrowFlow &&
               currentFlow == _flowVersion &&
               _activeStrokeObject != null;
    }

    private void CancelCurrentFlow(
        bool returnActorToIdle,
        bool restoreStrokeVisual)
    {
        _flowVersion++;
        StopVictoryDelay();
        StopStrokeFade(restoreStrokeVisual);
        actorCtrl?.CancelThrowAction(returnActorToIdle);
        _activeStrokeObject = null;
        _activeStrokeRigidbody = null;
        _activeStrokeRenderer = null;
        _activeStrokeOriginalGradient = null;
        _hasStartedThrowFlow = false;
        _hasReleasedStroke = false;
    }

    private void ResolveReferences()
    {
        if (drawLineFeature == null)
            drawLineFeature = GetComponent<DrawLineFeature>();
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponent<DrawLevelCtrl>();
        if (actorCtrl == null)
            actorCtrl = GetComponentInChildren<DLevel7ActorCtrl>(true);
        if (startPoint == null)
            startPoint = transform.Find("level7/startPoint");
        if (enemyActor == null)
            enemyActor = FindEnemyActor();
        if (enemyHitEffect == null)
        {
            Transform effectTransform = FindDescendantByName(
                transform,
                EnemyHitEffectName);
            if (effectTransform != null)
            {
                enemyHitEffect =
                    effectTransform.GetComponent<ParticleSystem>() ??
                    effectTransform.GetComponentInChildren<ParticleSystem>(true);
            }
        }
    }

    private ActorCtrl FindEnemyActor()
    {
        ActorCtrl[] actors = GetComponentsInChildren<ActorCtrl>(true);
        for (int i = 0; i < actors.Length; i++)
        {
            ActorCtrl candidate = actors[i];
            if (candidate == null || candidate == actorCtrl)
                continue;

            if (string.Equals(candidate.name, EnemyObjectName) ||
                candidate.CompareTag(EnemyTag))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName))
                return child;

            Transform result = FindDescendantByName(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void SubscribeToLevelResolved()
    {
        if (_subscribedDrawLevelCtrl == drawLevelCtrl)
            return;

        UnsubscribeFromLevelResolved();
        _subscribedDrawLevelCtrl = drawLevelCtrl;
        if (_subscribedDrawLevelCtrl != null)
            _subscribedDrawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void UnsubscribeFromLevelResolved()
    {
        if (_subscribedDrawLevelCtrl == null)
            return;

        _subscribedDrawLevelCtrl.Resolved -= OnLevelResolved;
        _subscribedDrawLevelCtrl = null;
    }

    #endregion
}
