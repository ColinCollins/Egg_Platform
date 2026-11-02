using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Core.PathCore;
using DG.Tweening.Plugins.Options;
using UnityEngine;

/// <summary>
/// 乒乓移动处理器：Target 按照 Points 顺序移动，支持 Yoyo 来回或无限循环
/// </summary>
public class PingPongMoveHandle : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;

    [Header("Path Settings")]
    [Tooltip("移动路径点列表（按顺序）")]
    [SerializeField] private List<Transform> points = new List<Transform>();

    [Tooltip("路径类型")]
    [SerializeField] private PathType pathType = PathType.Linear;

    [Header("Animation Settings")]
    [Tooltip("单次移动时长（秒）")]
    [SerializeField] private float duration = 2f;

    [Tooltip("缓动曲线")]
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Loop Settings")]
    [Tooltip("循环类型：Restart=从头开始，Yoyo=来回移动")]
    [SerializeField] private LoopType loopType = LoopType.Yoyo;

    [Tooltip("循环次数：-1=无限循环，0=不循环，>0=指定次数")]
    [SerializeField] private int loops = -1;

    [Header("Auto Start")]
    [Tooltip("是否在 Start 时自动开始移动")]
    [SerializeField] private bool autoStart = true;

    private TweenerCore<Vector3, Path, PathOptions> pathTweener;
    private Vector3[] worldPoints;

    private void Awake()
    {
        if (target == null)
            target = transform;
    }

    private void Start()
    {
        if (autoStart)
            StartMove();
    }

    /// <summary>
    /// 开始移动
    /// </summary>
    public void StartMove()
    {
        if (target == null)
        {
            Debug.LogWarning("[PingPongMoveHandle] Target is null!");
            return;
        }

        if (points == null || points.Count < 2)
        {
            Debug.LogWarning("[PingPongMoveHandle] Need at least 2 points!");
            return;
        }

        StopMove();
        
        target.position = points[0].position;
        worldPoints = new Vector3[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null)
            {
                Debug.LogWarning($"[PingPongMoveHandle] Point {i} is null!");
                return;
            }
            worldPoints[i] = points[i].position;
        }

        pathTweener = target.DOPath(worldPoints, duration, pathType);
        pathTweener.SetEase(easeCurve);

        if (loops != 0)
            pathTweener.SetLoops(loops, loopType);

        pathTweener.SetAutoKill(false);
    }

    /// <summary>
    /// 停止移动
    /// </summary>
    public void StopMove()
    {
        if (pathTweener != null && pathTweener.IsActive())
            pathTweener.Kill();
    }

    /// <summary>
    /// 暂停移动
    /// </summary>
    public void PauseMove()
    {
        if (pathTweener != null && pathTweener.IsActive())
            pathTweener.Pause();
    }

    /// <summary>
    /// 恢复移动
    /// </summary>
    public void ResumeMove()
    {
        if (pathTweener != null && pathTweener.IsActive())
            pathTweener.Play();
    }

    /// <summary>
    /// 重置到第一个点
    /// </summary>
    public void ResetToStart()
    {
        StopMove();
        if (target != null && points != null && points.Count > 0 && points[0] != null)
            target.position = points[0].position;
    }

    private void OnDestroy()
    {
        if (pathTweener != null && pathTweener.IsActive())
            pathTweener.Kill();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (points == null || points.Count < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null) continue;

            Gizmos.DrawWireSphere(points[i].position, 0.2f);

            if (i < points.Count - 1 && points[i + 1] != null)
            {
                Gizmos.DrawLine(points[i].position, points[i + 1].position);
            }
        }

        if (loopType == LoopType.Yoyo && points.Count > 0 && points[points.Count - 1] != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(points[points.Count - 1].position, points[0].position);
        }
    }
#endif
}
