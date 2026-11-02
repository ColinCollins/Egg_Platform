using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Core.PathCore;
using DG.Tweening.Plugins.Options;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 物体飞行路径监听器（横版 2D）
    /// 继承自 BaseItemEventHandle，可以在 BaseTriggerEventOwner 中使用
    /// 支持可视化编辑路径点，使用 DOTween 实现平滑的曲线移动。
    /// 可单独设置 X/Y 轴运动曲线（Ease Curve X / Ease Curve Y），实现不同轴向的缓动效果。
    /// </summary>
    public class ItemFlyPathListener : BaseItemEventHandle
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform endTarget;

        [Header("Path Settings")]
        [SerializeField] private List<Transform> waypoints = new List<Transform>();
        [SerializeField] private PathType pathType = PathType.CatmullRom;
        [SerializeField] private int resolution = 10;

        [Header("Bezier Settings")]
        [Tooltip("使用二次贝塞尔生成曲线（参考高度控制的 Bezier 曲线）")]
        [SerializeField] private bool useBezierCurve = false;
        [ShowIf("useBezierCurve")]
        [Tooltip("二次贝塞尔控制点列表（每个路径段对应一个控制点，在 Scene Editor 中可直接拖动调整）")]
        [SerializeField] private List<Vector3> bezierControlPoints = new List<Vector3>();

        [Header("Animation Settings")]
        [SerializeField] private float duration = 2f;
        [Tooltip("勾选为单曲线（Ease Curve），不勾选为 X/Y 双曲线")]
        [SerializeField] private bool isSingleCurve = true;
        [Tooltip("统一缓动曲线（单曲线时使用）")]
        [ShowIf("isSingleCurve")]
        [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("X 轴运动曲线（双曲线时使用）")]
        [HideIf("isSingleCurve")]
        [SerializeField] private AnimationCurve easeCurveX;
        [Tooltip("Y 轴运动曲线（双曲线时使用）")]
        [HideIf("isSingleCurve")]
        [SerializeField] private AnimationCurve easeCurveY;
        [SerializeField] private bool flipSpriteByDirection = false;

        [Header("Loop Settings")]
        [SerializeField] private LoopType loopType = LoopType.Restart;
        [SerializeField] private int loops = 0;

        [Header("Gizmos Settings")]
        [SerializeField] private Color pathColor = Color.green;
        [SerializeField] private Color waypointColor = Color.yellow;
        [SerializeField] private float waypointSize = 0.2f;
        [SerializeField] private bool showGizmos = true;

        private TweenerCore<Vector3, Path, PathOptions> pathTweener;
        private Tweener progressTweener;
        private Vector3[] worldWaypoints;
        private SpriteRenderer spriteRenderer;
        private bool originalFlipX;
        private float pathProgress;
        private Vector3 initialPosition;

        private void Awake()
        {
            // 如果没有指定 target，使用当前 Transform
            if (target == null)
            {
                target = transform;
            }
            initialPosition = target.position;

            // 获取 SpriteRenderer（用于翻转）
            if (flipSpriteByDirection)
            {
                spriteRenderer = target.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    originalFlipX = spriteRenderer.flipX;
                }
            }
        }

        public override void Execute()
        {
            if (target == null)
            {
                Debug.LogWarning("[ItemFlyPathListener] Target is null!");
                IsDone = true;
                return;
            }

            // 构建路径点数组（包括起始点、中间点和结束点）
            List<Vector3> pathPoints = new List<Vector3>();
            
            // 起始点：target 当前位置
            pathPoints.Add(target.position);
            
            // 中间路径点
            foreach (var waypoint in waypoints)
            {
                if (waypoint != null)
                {
                    pathPoints.Add(waypoint.position);
                }
            }
            
            // 结束点：endTarget 位置（如果指定）
            if (endTarget != null)
            {
                pathPoints.Add(endTarget.position);
            }
            else if (pathPoints.Count > 1)
            {
                // 如果没有指定 endTarget，使用最后一个 waypoint 的位置
                pathPoints.Add(pathPoints[pathPoints.Count - 1]);
            }

            if (pathPoints.Count < 2)
            {
                Debug.LogWarning("[ItemFlyPathListener] Need at least 2 path points!");
                IsDone = true;
                return;
            }

            // 初始化 Bezier 控制点
            if (useBezierCurve)
            {
                InitializeBezierControlPoints(pathPoints);
            }

            // 停止之前的动画
            if (pathTweener != null && pathTweener.IsActive())
            {
                pathTweener.Kill();
            }
            if (progressTweener != null && progressTweener.IsActive())
            {
                progressTweener.Kill();
            }

            IsRunning = true;
            IsDone = false;

            // 转换为数组
            worldWaypoints = pathPoints.ToArray();

            bool useSeparateCurves = !isSingleCurve;
            bool useCustomPath = useSeparateCurves || useBezierCurve;
            AnimationCurve curveX = easeCurveX != null ? easeCurveX : easeCurve;
            AnimationCurve curveY = easeCurveY != null ? easeCurveY : easeCurve;

            if (useCustomPath)
            {
                // 使用自定义路径采样（分轴曲线或二次贝塞尔）
                pathTweener = null;
                pathProgress = 0f;

                progressTweener = DOTween.To(() => pathProgress, x => pathProgress = x, 1f, duration)
                    .SetEase(Ease.Linear)
                    .SetAutoKill(true);

                if (loops > 0)
                    progressTweener.SetLoops(loops, loopType);

                Vector3 lastPosition = target.position;
                progressTweener.OnUpdate(() =>
                {
                    if (target == null || worldWaypoints == null || worldWaypoints.Length < 2) return;

                    float t = Mathf.Clamp01(pathProgress);
                    if (useSeparateCurves)
                    {
                        float sx = Mathf.Clamp01(curveX.Evaluate(t));
                        float sy = Mathf.Clamp01(curveY.Evaluate(t));

                        Vector3 posX = GetPathPositionAt(sx);
                        Vector3 posY = GetPathPositionAt(sy);
                        target.position = new Vector3(posX.x, posY.y, posX.z);
                    }
                    else
                    {
                        float s = Mathf.Clamp01(easeCurve.Evaluate(t));
                        target.position = GetPathPositionAt(s);
                    }

                    if (flipSpriteByDirection && spriteRenderer != null)
                    {
                        Vector3 currentPosition = target.position;
                        if (currentPosition.x > lastPosition.x)
                            spriteRenderer.flipX = originalFlipX;
                        else if (currentPosition.x < lastPosition.x)
                            spriteRenderer.flipX = !originalFlipX;
                    }
                    lastPosition = target.position;
                });

                progressTweener.OnComplete(() =>
                {
                    IsRunning = false;
                    IsDone = true;
                });
                progressTweener.OnKill(() => { IsRunning = false; });
            }
            else
            {
                // 原有：单曲线 DOPath
                progressTweener = null;
                pathTweener = target.DOPath(worldWaypoints, duration, pathType);
                pathTweener.SetEase(easeCurve);

                if (loops > 0)
                    pathTweener.SetLoops(loops, loopType);

                pathTweener.SetAutoKill(true);

                if (flipSpriteByDirection && spriteRenderer != null)
                {
                    Vector3 lastPosition = target.position;
                    pathTweener.OnUpdate(() =>
                    {
                        if (pathTweener != null && pathTweener.IsActive() && target != null)
                        {
                            Vector3 currentPosition = target.position;
                            if (currentPosition.x > lastPosition.x)
                                spriteRenderer.flipX = originalFlipX;
                            else if (currentPosition.x < lastPosition.x)
                                spriteRenderer.flipX = !originalFlipX;
                            lastPosition = currentPosition;
                        }
                    });
                }

                pathTweener.OnComplete(() =>
                {
                    IsRunning = false;
                    IsDone = true;
                });
                pathTweener.OnKill(() => { IsRunning = false; });
            }
        }

        /// <summary>
        /// 停止路径动画
        /// </summary>
        public void Stop()
        {
            if (pathTweener != null && pathTweener.IsActive())
                pathTweener.Kill();
            if (progressTweener != null && progressTweener.IsActive())
                progressTweener.Kill();
            IsRunning = false;
            IsDone = true;
        }

        /// <summary>
        /// 暂停路径动画
        /// </summary>
        public void Pause()
        {
            if (pathTweener != null && pathTweener.IsActive())
                pathTweener.Pause();
            if (progressTweener != null && progressTweener.IsActive())
                progressTweener.Pause();
        }

        /// <summary>
        /// 恢复路径动画
        /// </summary>
        public void Resume()
        {
            if (pathTweener != null && pathTweener.IsActive())
                pathTweener.Play();
            if (progressTweener != null && progressTweener.IsActive())
                progressTweener.Play();
        }

        /// <summary>
        /// 重置到起始位置
        /// </summary>
        public void ResetToStart()
        {
            Stop();
            if (target != null)
            {
                // 重置到初始位置
                target.position = initialPosition;
            }

            // 重置 Sprite 翻转状态
            if (flipSpriteByDirection && spriteRenderer != null)
            {
                spriteRenderer.flipX = originalFlipX;
            }
        }

        public void SetEndTarget(Transform newTarget)
        {
            this.endTarget = newTarget;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos)
                return;

            // 构建路径点列表用于绘制
            List<Vector3> pathPoints = new List<Vector3>();
            
            // 起始点：target 当前位置
            if (target != null)
            {
                pathPoints.Add(target.position);
            }
            
            // 中间路径点
            foreach (var waypoint in waypoints)
            {
                if (waypoint != null)
                {
                    pathPoints.Add(waypoint.position);
                }
            }
            
            // 结束点：endTarget 位置（如果指定）
            if (endTarget != null)
            {
                pathPoints.Add(endTarget.position);
            }

            if (pathPoints.Count < 2)
                return;

            // 绘制路径点
            Gizmos.color = waypointColor;
            for (int i = 0; i < pathPoints.Count; i++)
            {
                Vector3 worldPos = pathPoints[i];
                
                Gizmos.DrawSphere(worldPos, waypointSize);
                
                // 绘制序号
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.3f, i.ToString());
                #endif
            }

            // 绘制 Bezier 控制点
            if (useBezierCurve && bezierControlPoints != null && bezierControlPoints.Count > 0 && pathPoints.Count >= 2)
            {
                Gizmos.color = Color.magenta;
                for (int i = 0; i < pathPoints.Count - 1 && i < bezierControlPoints.Count; i++)
                {
                    Vector3 start = pathPoints[i];
                    Vector3 end = pathPoints[i + 1];
                    Vector3 controlPointPos = bezierControlPoints[i];
                    
                    // 绘制控制点
                    Gizmos.DrawWireSphere(controlPointPos, waypointSize * 0.8f);
                    
                    // 绘制从起点到控制点，控制点到终点的辅助线
                    Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
                    Gizmos.DrawLine(start, controlPointPos);
                    Gizmos.DrawLine(controlPointPos, end);
                    Gizmos.color = Color.magenta;
                }
            }

            // 绘制路径曲线
            if (pathPoints.Count >= 2)
            {
                Gizmos.color = pathColor;
                
                // 使用 DOTween 的路径计算来绘制曲线
                Vector3[] drawPoints = GetDrawPoints(pathPoints);
                if (drawPoints != null && drawPoints.Length > 1)
                {
                    for (int i = 0; i < drawPoints.Length - 1; i++)
                    {
                        Gizmos.DrawLine(drawPoints[i], drawPoints[i + 1]);
                    }
                }
            }
        }

        /// <summary>
        /// 获取用于绘制的路径点（Gizmos 用）
        /// </summary>
        private Vector3[] GetDrawPoints(List<Vector3> pathPoints)
        {
            if (pathPoints == null || pathPoints.Count < 2)
                return null;

            int segmentCount = pathPoints.Count - 1;
            int totalPoints = resolution * segmentCount + 1;
            Vector3[] drawPoints = new Vector3[totalPoints];

            if (useBezierCurve)
            {
                int idx = 0;
                for (int i = 0; i < segmentCount && idx < drawPoints.Length; i++)
                {
                    Vector3 a = pathPoints[i];
                    Vector3 b = pathPoints[i + 1];
                    for (int j = 0; j <= resolution && idx < drawPoints.Length; j++, idx++)
                    {
                        float t = (resolution > 0) ? j / (float)resolution : 0f;
                        float actualHeight = GetActualBezierHeight(a, b, i);
                        drawPoints[idx] = CalculateBezierPoint(a, b, actualHeight, t);
                    }
                }
            }
            else if (pathType == PathType.Linear)
            {
                SampleSegments(pathPoints, segmentCount, drawPoints, totalPoints, Vector3.Lerp);
            }
            else
            {
                Vector3[] pointsArray = pathPoints.ToArray();
                for (int i = 0; i < totalPoints; i++)
                {
                    float t = (totalPoints > 1) ? i / (float)(totalPoints - 1) : 0f;
                    drawPoints[i] = GetCatmullRomPoint(t, pointsArray);
                }
            }

            return drawPoints;
        }

        /// <summary>
        /// 按段采样：每段 [0,1] 上取 resolution+1 个点，总点数 = resolution*segmentCount+1
        /// </summary>
        private delegate Vector3 SegmentSample(Vector3 a, Vector3 b, float t);

        private void SampleSegments(List<Vector3> pathPoints, int segmentCount, Vector3[] drawPoints, int totalPoints, SegmentSample sample)
        {
            int idx = 0;
            for (int i = 0; i < segmentCount && idx < totalPoints; i++)
            {
                Vector3 a = pathPoints[i];
                Vector3 b = pathPoints[i + 1];
                for (int j = 0; j <= resolution && idx < totalPoints; j++, idx++)
                {
                    float t = (resolution > 0) ? j / (float)resolution : 0f;
                    drawPoints[idx] = sample(a, b, t);
                }
            }
        }

        /// <summary>
        /// 根据归一化进度 s∈[0,1] 取路径上的位置（用于 X/Y 分离曲线）
        /// </summary>
        private Vector3 GetPathPositionAt(float s)
        {
            if (worldWaypoints == null || worldWaypoints.Length == 0)
                return target != null ? target.position : Vector3.zero;
            if (worldWaypoints.Length == 1)
                return worldWaypoints[0];

            s = Mathf.Clamp01(s);
            if (useBezierCurve)
            {
                int n = worldWaypoints.Length;
                float f = s * (n - 1);
                int i = Mathf.FloorToInt(f);
                if (i >= n - 1)
                    return worldWaypoints[n - 1];
                float t = f - i;
                float actualHeight = GetActualBezierHeight(worldWaypoints[i], worldWaypoints[i + 1], i);
                return CalculateBezierPoint(worldWaypoints[i], worldWaypoints[i + 1], actualHeight, t);
            }
            if (pathType == PathType.Linear)
            {
                int n = worldWaypoints.Length;
                float f = s * (n - 1);
                int i = Mathf.FloorToInt(f);
                if (i >= n - 1)
                    return worldWaypoints[n - 1];
                float t = f - i;
                return Vector3.Lerp(worldWaypoints[i], worldWaypoints[i + 1], t);
            }

            return GetCatmullRomPoint(s, worldWaypoints);
        }

        /// <summary>
        /// 初始化 Bezier 控制点列表（根据路径段数量）
        /// </summary>
        private void InitializeBezierControlPoints(List<Vector3> pathPoints)
        {
            if (!useBezierCurve || pathPoints == null || pathPoints.Count < 2)
                return;

            int segmentCount = pathPoints.Count - 1;
            
            // 如果控制点数量不匹配，重新初始化
            while (bezierControlPoints.Count < segmentCount)
            {
                int index = bezierControlPoints.Count;
                Vector3 start = pathPoints[index];
                Vector3 end = pathPoints[index + 1];
                Vector3 midPoint = (start + end) * 0.5f;
                // 默认高度为 1
                bezierControlPoints.Add(midPoint + Vector3.up * 1f);
            }
            
            // 如果控制点数量过多，移除多余的
            while (bezierControlPoints.Count > segmentCount)
            {
                bezierControlPoints.RemoveAt(bezierControlPoints.Count - 1);
            }
        }

        /// <summary>
        /// 获取实际的 Bezier 高度（从控制点位置计算）
        /// </summary>
        private float GetActualBezierHeight(Vector3 start, Vector3 end, int segmentIndex)
        {
            if (bezierControlPoints != null && segmentIndex >= 0 && segmentIndex < bezierControlPoints.Count)
            {
                Vector3 midPoint = (start + end) * 0.5f;
                return bezierControlPoints[segmentIndex].y - midPoint.y;
            }
            // 如果没有设置控制点，返回 0（直线）
            return 0f;
        }

        private Vector3 CalculateBezierPoint(Vector3 start, Vector3 end, float height, float t)
        {
            Vector3 midPoint = (start + end) * 0.5f;
            Vector3 controlPoint = midPoint + Vector3.up * height;

            float u = 1f - t;
            Vector3 point = u * u * start + 2f * u * t * controlPoint + t * t * end;
            return point;
        }

        /// <summary>
        /// Catmull-Rom 样条插值
        /// </summary>
        private Vector3 GetCatmullRomPoint(float t, Vector3[] points)
        {
            int numSections = points.Length - 1;
            int currPt = Mathf.Min(Mathf.FloorToInt(t * numSections), numSections - 1);
            float u = t * numSections - currPt;

            Vector3 a = currPt > 0 ? points[currPt - 1] : points[0];
            Vector3 b = points[currPt];
            Vector3 c = currPt < points.Length - 1 ? points[currPt + 1] : points[points.Length - 1];
            Vector3 d = currPt < points.Length - 2 ? points[currPt + 2] : points[points.Length - 1];

            return 0.5f * (
                (-a + 3f * b - 3f * c + d) * (u * u * u) +
                (2f * a - 5f * b + 4f * c - d) * (u * u) +
                (-a + c) * u +
                2f * b
            );
        }

        private void OnDestroy()
        {
            if (pathTweener != null && pathTweener.IsActive())
                pathTweener.Kill();
            if (progressTweener != null && progressTweener.IsActive())
                progressTweener.Kill();
        }
    }
}
