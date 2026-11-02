using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// Level7 Key 控制脚本：Awake 时用曲线运动实现“斜向上蹦出来”的感觉（不使用 Rigidbody）
/// </summary>
public class ItemJumpOutExecutor : BaseItemExecutor
{
    [SerializeField] private Transform initTarget;
    [SerializeField] private bool isAutoPlay;

    [Header("曲线跳跃设置")]
    [Tooltip("跳跃时长（秒）")]
    [SerializeField] private Vector2 durationRange = new Vector2(0.35f, 0.6f);

    [Tooltip("水平偏移（世界坐标）")]
    [SerializeField] private Vector2 horizontalOffsetRange = new Vector2(-1.5f, 1.5f);

    [Tooltip("竖直高度（世界坐标）")]
    [SerializeField] private Vector2 heightRange = new Vector2(1.0f, 2.5f);

    [SerializeField] private float endOffseY = 0;

    [Tooltip("时间曲线（0->1），用于控制运动节奏")]
    [SerializeField] private AnimationCurve timeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    void Start()
    {
        // 如果提供了 initTarget，则以它的位置作为起点
        if (isAutoPlay)
            Play();
    }

    public void Play()
    {
        if (initTarget != null)
        {
            transform.position = initTarget.position;
        }

        StartCoroutine(JumpCurveRoutine(false, Vector3.zero, null));
    }

    /// <summary>
    /// 使用本组件原有的随机时长、随机高度和时间曲线，从指定起点跳到指定终点。
    /// 调用边界：用于必须保留原 ItemJumpOut 表现、但由关卡改写落点的场景；不改变 Play 的既有行为。
    /// </summary>
    public void PlayBetween(Vector3 startPosition, Vector3 endPosition, System.Action onComplete = null)
    {
        transform.position = startPosition;
        StartCoroutine(JumpCurveRoutine(true, endPosition, onComplete));
    }

    private System.Collections.IEnumerator JumpCurveRoutine(
        bool useFixedEndPosition,
        Vector3 fixedEndPosition,
        System.Action onComplete)
    {
        Vector3 startPos = transform.position;

        float duration = Random.Range(durationRange.x, durationRange.y);
        duration = Mathf.Max(0.01f, duration);

        float dx = Random.Range(horizontalOffsetRange.x, horizontalOffsetRange.y);
        float peak = Random.Range(heightRange.x, heightRange.y);

        Vector3 endPos = useFixedEndPosition
            ? fixedEndPosition
            : startPos + new Vector3(dx, endOffseY, 0f);

        float t = 0f;
        while (t < duration)
        {
            float normalized = t / duration;
            float eased = timeCurve != null ? timeCurve.Evaluate(normalized) : normalized;

            // 先做水平的线性插值
            Vector3 pos = Vector3.LerpUnclamped(startPos, endPos, eased);

            // 叠加一个抛物线高度：0->peak->0
            float parabola = 4f * eased * (1f - eased); // [0,1] peaked at 0.5
            pos.y += peak * parabola;

            transform.position = pos;

            t += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;

        Execute();
        onComplete?.Invoke();
    }

    private void OnDrawGizmos()
    {
        Vector3 center = initTarget != null ? initTarget.position : transform.position;
        float minX = horizontalOffsetRange.x;
        float maxX = horizontalOffsetRange.y;

        Vector3 left = center + Vector3.right * minX;
        Vector3 right = center + Vector3.right * maxX;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left, 0.1f);
        Gizmos.DrawWireSphere(right, 0.1f);
    }
}
