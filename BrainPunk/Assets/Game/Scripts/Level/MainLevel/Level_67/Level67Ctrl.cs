using Bear.Logger;
using DG.Tweening;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// version 1.0
/// 关注 actor，当高度低于 rangeTransformPoint 的位置的时候 parachute DOScale = 1，OutBack， actor 的 rigidbody2D gravity 变为 0.2
/// 监听左右移动，actor 进行绕 z 轴进行反向的 range 范围内的转动
/// </summary>
public class Level67Ctrl : MonoBehaviour, IDebuger
{
    #region Inspector 配置

    /// <summary>
    /// Level67 受控主角；用于读取当前左右移动输入和缓存主角根节点旋转，不修改共享移动逻辑。
    /// </summary>
    [SerializeField] private Level64ActorCtrl actor;

    /// <summary>
    /// 降落伞触发高度点；actor 世界坐标 y 低于该点时只触发一次开伞。
    /// </summary>
    // [SerializeField] private Transform limitPoint;

    /// <summary>
    /// 降落伞表现根节点；仅在本关开伞时缩放到 Vector3.one。
    /// </summary>
    [SerializeField] private Transform parachute;

    /// <summary>
    /// 主角刚体；用于开伞后把 gravityScale 降到 reducedGravityScale，不在清理时主动恢复。
    /// </summary>
    [SerializeField] private Rigidbody2D actorRigid;

    /// <summary>
    /// 开伞缩放时长；只在触发开伞的一次 DOTween 表现中使用。
    /// </summary>
    [SerializeField] private float openDuration = 0.2f;

    /// <summary>
    /// 开伞缩放曲线；默认匹配需求注释里的 OutBack。
    /// </summary>
    [SerializeField] private Ease openEase = Ease.OutBack;

    /// <summary>
    /// OpenParachute 触发后 actor 左右衰减晃动的最大 Z 轴角度；仅用于开伞瞬间表现，负数按绝对值处理。
    /// </summary>
    [SerializeField] private float shakeRotationAngle = 12f;

    /// <summary>
    /// OpenParachute 触发后 actor 左右衰减晃动的总时长；默认 2 秒，小于等于 0 时跳过该表现。
    /// </summary>
    [SerializeField] private float shakeRotationDuration = 2f;

    /// <summary>
    /// 开伞后主角刚体重力；按需求固定默认为 0.2。
    /// </summary>
    [SerializeField] private float reducedGravityScale = 0.2f;

    [SerializeField] private float reducedFallSpeed = 0.2f;

    [SerializeField] private float parachuteScale = 1.5f;

    /// <summary>
    /// 左右输入对应的最大反向绕 Z 轴倾斜角度；只影响 Level67 actor 根节点。
    /// </summary>
    [SerializeField] private float maxTiltAngle = 18f;

    /// <summary>
    /// 有左右输入时靠近目标倾斜角的速度，单位为度/秒。
    /// </summary>
    [SerializeField] private float tiltSpeed = 120f;

    /// <summary>
    /// 取消移动或输入为 0 时回到中立旋转的速度，单位为度/秒。
    /// </summary>
    [SerializeField] private float neutralReturnSpeed = 160f;

    /// <summary>
    /// 缺少关键引用时是否输出一次 warning；用于 Inspector 绑定排查，不阻断其他系统。
    /// </summary>
    [SerializeField] private bool logMissingReferences = true;

    #endregion

    #region 运行时状态

    private bool isParachuteDeployed;
    private bool hasLoggedMissingReferences;
    private bool hasCachedActorRotation;
    private bool isActorRotationReleasedAfterSuccess;
    private float currentTiltAngle;
    private Quaternion actorNeutralRotation;
    private BaseLevelCtrl levelCtrl;
    private Tween parachuteTween;
    private Tween actorShakeRotationTween;
    private bool isActorShakeRotating;

    #endregion

    /// <summary>
    /// 
    /// </summary>
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ResolveRuntimeReferences();
        CacheActorNeutralRotation();
        HideParachute();
    }

    // Update is called once per frame
    void Update()
    {
        ResolveRuntimeReferences();

        if (!CanRun())
        {
            return;
        }

        if (IsLevelSuccess())
        {
            ReleaseActorRotationControlAfterSuccess();
            return;
        }

        /* if (!isParachuteDeployed && IsActorBelowLimit())
        {
            DeployParachute();
        } */

        if (isParachuteDeployed && IsLevelSuccess())
        {
            ReleaseActorRotationControlAfterSuccess();
            return;
        }

        if (isParachuteDeployed)
        {
            UpdateActorTilt();
        }
    }

    private void OnDisable()
    {
        CleanupLevel67State();
    }

    private void OnDestroy()
    {
        CleanupLevel67State();
    }

    #region 引用解析与检查

    private void ResolveRuntimeReferences()
    {
        if (actor == null)
        {
            actor = GetComponentInChildren<Level64ActorCtrl>();
        }

        if (actorRigid == null && actor != null)
        {
            actorRigid = actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
        }

        ResolveLevelCtrl();
    }

    private void ResolveLevelCtrl()
    {
        if (levelCtrl == null)
        {
            levelCtrl = GetComponentInParent<BaseLevelCtrl>();
        }
    }

    private bool CanRun()
    {
        bool hasRequiredReferences = actor != null
            && actorRigid != null
            // && limitPoint != null
            && parachute != null;

        if (!hasRequiredReferences && logMissingReferences && !hasLoggedMissingReferences)
        {
            hasLoggedMissingReferences = true;
            this.LogWarning("[Level67Ctrl] Missing actor, actorRigid, or parachute reference; parachute control is skipped.");
        }

        return hasRequiredReferences;
    }

    #endregion

    #region 开伞与重力

    private void HideParachute()
    {
        parachute.transform.DOKill();
        parachute.localScale = Vector3.zero;
    }

    /*     private bool IsActorBelowLimit()
        {
            return actor.transform.position.y < limitPoint.position.y;
        } */

    /// <summary>
    /// 外部触发 Level67 开伞的运行时入口；target 和 pos 保留给统一调用签名，当前仅触发本关开伞与 actor 晃动表现。
    /// </summary>
    /// <remarks>仅应在 PlayMode 运行态调用，不读写存档、配置或全局事件。</remarks>
    public void OpenParachute(Transform target, Vector2 pos)
    {
        ResolveRuntimeReferences();

        if (!CanRun())
        {
            return;
        }

        if (IsLevelSuccess())
        {
            ReleaseActorRotationControlAfterSuccess();
            return;
        }

        bool wasParachuteDeployed = isParachuteDeployed;
        DeployParachute();

        if (!wasParachuteDeployed && isParachuteDeployed)
        {
            TweenShakeRotation();
        }

        AudioManager.PlaySound("openParachute");
        actor.GetComponent<BoxCollider2D>().enabled = false;
    }

    private void DeployParachute()
    {
        if (isParachuteDeployed)
        {
            return;
        }

        isParachuteDeployed = true;
        actorRigid.linearVelocityY = reducedFallSpeed;
        actorRigid.gravityScale = reducedGravityScale;
        actor.SetMoveSpeed(2f);
        actor.SwitchLiftState(true);

        parachuteTween?.Kill();
        parachuteTween = parachute
            .DOScale(Vector3.one * parachuteScale, Mathf.Max(0f, openDuration))
            .SetEase(openEase)
            .SetLink(gameObject).OnComplete(() =>
            {
                parachute.DOScaleY(parachuteScale - 0.1f, 1f).SetLoops(-1, LoopType.Yoyo);
            });
    }

    #endregion

    #region 反向旋转

    private void TweenShakeRotation()
    {
        if (IsLevelSuccess())
        {
            ReleaseActorRotationControlAfterSuccess();
            return;
        }

        CacheActorNeutralRotation();

        if (!hasCachedActorRotation || actor == null)
        {
            return;
        }

        actorShakeRotationTween?.Kill();
        actorShakeRotationTween = null;
        isActorShakeRotating = false;
        currentTiltAngle = 0f;
        actor.transform.rotation = actorNeutralRotation;

        float angle = Mathf.Abs(shakeRotationAngle);
        float duration = Mathf.Max(0f, shakeRotationDuration);

        if (Mathf.Approximately(angle, 0f) || Mathf.Approximately(duration, 0f))
        {
            return;
        }

        const int swingCount = 6;
        float segmentDuration = duration / (swingCount + 1);
        float currentShakeAngle = 0f;
        Sequence sequence = DOTween.Sequence();

        isActorShakeRotating = true;

        for (int i = 0; i < swingCount; i++)
        {
            float fade = 1f - i / (float)swingCount;
            float targetAngle = (i % 2 == 0 ? angle : -angle) * fade;
            sequence.Append(DOTween.To(
                    () => currentShakeAngle,
                    value =>
                    {
                        currentShakeAngle = value;
                        ApplyActorShakeRotation(value);
                    },
                    targetAngle,
                    segmentDuration)
                .SetEase(Ease.InOutSine));
        }

        sequence.Append(DOTween.To(
                () => currentShakeAngle,
                value =>
                {
                    currentShakeAngle = value;
                    ApplyActorShakeRotation(value);
                },
                0f,
                segmentDuration)
            .SetEase(Ease.OutSine));

        actorShakeRotationTween = sequence
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                isActorShakeRotating = false;
                currentTiltAngle = 0f;
                if (actor != null && !IsLevelSuccess())
                {
                    actor.transform.rotation = actorNeutralRotation;
                }

                actorShakeRotationTween = null;
            })
            .OnKill(() =>
            {
                isActorShakeRotating = false;
                if (actorShakeRotationTween == sequence)
                {
                    actorShakeRotationTween = null;
                }
            });
    }

    private void ApplyActorShakeRotation(float angle)
    {
        if (IsLevelSuccess())
        {
            return;
        }

        if (!hasCachedActorRotation || actor == null)
        {
            return;
        }

        actor.transform.rotation = actorNeutralRotation * Quaternion.Euler(0f, 0f, angle);
    }

    private void CacheActorNeutralRotation()
    {
        if (hasCachedActorRotation || actor == null)
        {
            return;
        }

        actorNeutralRotation = actor.transform.rotation;
        hasCachedActorRotation = true;
        currentTiltAngle = 0f;
    }

    private void UpdateActorTilt()
    {
        if (IsLevelSuccess())
        {
            ReleaseActorRotationControlAfterSuccess();
            return;
        }

        actorRigid.linearVelocityY = reducedFallSpeed;
        
        if (isActorShakeRotating)
        {
            return;
        }

        CacheActorNeutralRotation();

        if (!hasCachedActorRotation)
        {
            return;
        }

        float input = Mathf.Clamp(actor.XInput, -1f, 1f);
        float targetAngle = -input * Mathf.Max(0f, maxTiltAngle);
        float speed = Mathf.Approximately(input, 0f) ? neutralReturnSpeed : tiltSpeed;

        currentTiltAngle = Mathf.MoveTowardsAngle(
            currentTiltAngle,
            targetAngle,
            Mathf.Max(0f, speed) * Time.deltaTime);

        actor.transform.rotation = actorNeutralRotation * Quaternion.Euler(0f, 0f, currentTiltAngle);
    }

    private bool IsLevelSuccess()
    {
        ResolveLevelCtrl();
        return levelCtrl != null && levelCtrl.IsSuccess();
    }

    private void ReleaseActorRotationControlAfterSuccess()
    {
        if (isActorRotationReleasedAfterSuccess)
        {
            return;
        }

        actorShakeRotationTween?.Kill();
        actorShakeRotationTween = null;
        isActorShakeRotating = false;
        currentTiltAngle = 0f;
        isActorRotationReleasedAfterSuccess = true;
    }

    #endregion

    #region 清理

    private void CleanupLevel67State()
    {
        bool shouldRestoreActorRotation = !isActorRotationReleasedAfterSuccess && !IsLevelSuccess();

        if (parachute)
            parachute.DOKill();

        parachuteTween?.Kill();
        parachuteTween = null;
        actorShakeRotationTween?.Kill();
        actorShakeRotationTween = null;
        isActorShakeRotating = false;
        isActorRotationReleasedAfterSuccess = false;
        isParachuteDeployed = false;

        if (shouldRestoreActorRotation && hasCachedActorRotation && actor != null)
        {
            actor.transform.rotation = actorNeutralRotation;
        }

        currentTiltAngle = 0f;
    }

    #endregion
}
