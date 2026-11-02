using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level31 金币触发处理器
/// 碰到 Actor 时飞到指定目标然后回收，碰到其他对象直接回收
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Level31CoinTriggerHandle : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("目标位置 Transform")]
    [SerializeField] private Transform targetTransform;

    [Header("Fly Animation Settings")]
    [Tooltip("飞到目标的持续时间")]
    [SerializeField] private float flyDuration = 0.5f;

    [Tooltip("飞行动画曲线")]
    [SerializeField] private Ease flyEase = Ease.OutQuad;

    [Tooltip("是否在飞行时缩放")]
    [SerializeField] private bool scaleOnFly = true;

    [Tooltip("飞行结束时的缩放值")]
    [SerializeField] private Vector3 endScale = Vector3.zero;

    [Header("Actor Detection")]
    [Tooltip("Actor 的 Tag（默认 Player）")]
    [SerializeField] private string actorTag = "Player";

    [SerializeField] private Level31Ctrl levelCtrl;

    [SerializeField] private int goldScore;

    [SerializeField] private int silverScore;

    private Sequence _flySequence;
    private bool _isTriggered = false;
    private ThrowableItem _throwableItem;

    private Animator anim;
    private bool isGold = false;

    /// <summary>
    /// 设置目标 Transform
    /// </summary>
    public void SetTarget(Transform target)
    {
        targetTransform = target;
    }

    private void Awake()
    {
        // 获取 ThrowableItem 组件
        _throwableItem = GetComponent<ThrowableItem>();

        anim = GetComponent<Animator>();
    }

    void OnEnable()
    {
        _isTriggered = false;

        isGold = Random.Range(0, 2) == 1;
        GetComponent<SpriteRenderer>().color = Color.white;
        anim.SetBool("isGold", isGold);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isTriggered)
        {
            // Debug.LogError("----------- asdas" + other.name);
            return;
        }
        // Debug.LogError("----------- asdas" + other.name);
        // 检查是否是 Actor
        bool isActor = CheckIsActor(other);

        if (isActor)
        {
            // 碰到 Actor，飞到目标位置然后回收
            FlyToTargetAndRecycle();
            AudioManager.PlaySound("getCoin", randomPitch: true);
        }
        else if (CheckIsGround(other))
        {
            // 渐隐消失回收
            FadeOut();
            // RecycleImmediately();
        }
    }

    /// <summary>
    /// 检查碰撞对象是否是 Actor
    /// </summary>
    private bool CheckIsActor(Collider2D collider)
    {
        return !string.IsNullOrEmpty(actorTag) && collider.CompareTag(actorTag);
    }

    /// <summary>
    /// 检查碰撞对象是否是 Actor
    /// </summary>
    private bool CheckIsGround(Collider2D collider)
    {
        // Debug.LogError("---------------" + collider.name);
        return collider.gameObject.layer == LayerMask.NameToLayer("Ground");
    }


    /// <summary>
    /// 飞到目标位置然后回收
    /// </summary>
    private void FlyToTargetAndRecycle()
    {
        _isTriggered = true;

        // 禁用碰撞器，避免重复触发
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        // 获取目标位置
        Vector3 targetPos = GetTargetPosition();

        // 停止之前的动画
        _flySequence?.Kill();

        // 创建飞行动画序列
        _flySequence = DOTween.Sequence();

        // 移动到目标位置
        _flySequence.Append(transform.DOMove(targetPos, flyDuration).SetEase(flyEase));

        // 如果需要缩放
        if (scaleOnFly)
        {
            _flySequence.Join(transform.DOScale(endScale, flyDuration).SetEase(flyEase));
        }

        // 动画完成后回收
        _flySequence.OnComplete(() =>
        {
            RecycleItem();
            levelCtrl.CutdownScore(isGold ? goldScore : silverScore);
        });
    }

    private void FadeOut()
    {
        _isTriggered = true;
        GetComponent<SpriteRenderer>().DOFade(0, 0.5f).OnComplete(() =>
        {
            RecycleImmediately();
        });
    }

    /// <summary>
    /// 立即回收
    /// </summary>
    private void RecycleImmediately()
    {
        RecycleItem();
    }

    /// <summary>
    /// 回收物体到对象池
    /// </summary>
    private void RecycleItem()
    {
        if (_throwableItem != null)
        {
            // _throwableItem.GetComponent<DirectMove>().enabled = false;
            _throwableItem.RecycleSelf();
        }
        else
        {
            // 如果没有 ThrowableItem 组件，降级为销毁
            Debug.LogWarning("[Level31CoinTriggerHandle] No ThrowableItem component found, destroying instead.");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 获取目标位置
    /// </summary>
    private Vector3 GetTargetPosition()
    {
        if (targetTransform != null)
            return targetTransform.position;

        Debug.LogWarning("[Level31CoinTriggerHandle] Target Transform is not assigned!");
        return transform.position;
    }

    private void OnDestroy()
    {
        _flySequence?.Kill();
        _flySequence = null;
    }
}
