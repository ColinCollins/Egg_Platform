using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Core.PathCore;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 物体飞行路径监听器（横版 2D）
    /// 继承自 BaseItemEventHandle，可以在 BaseTriggerEventOwner 中使用
    /// 支持可视化编辑路径点，使用 DOTween 实现平滑的曲线移动
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

        [Header("Animation Settings")]
        [SerializeField] private float duration = 2f;
        [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
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
        private Vector3[] worldWaypoints;
        private SpriteRenderer spriteRenderer;
        private bool originalFlipX;

        private void Awake()
        {
            // 如果没有指定 target，使用当前 Transform
            if (target == null)
            {
                target = transform;
            }

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

            // 停止之前的动画
            if (pathTweener != null && pathTweener.IsActive())
            {
                pathTweener.Kill();
            }

            IsRunning = true;
            IsDone = false;

            // 转换为数组
            worldWaypoints = pathPoints.ToArray();

            // 创建路径动画
            pathTweener = target.DOPath(worldWaypoints, duration, pathType);

            // 设置自定义缓动曲线
            pathTweener.SetEase(easeCurve);

            // 设置循环
            if (loops > 0)
            {
                pathTweener.SetLoops(loops, loopType);
            }

            // 设置自动销毁
            pathTweener.SetAutoKill(true);

            // 如果启用翻转 Sprite，监听路径方向
            if (flipSpriteByDirection && spriteRenderer != null)
            {
                Vector3 lastPosition = target.position;
                pathTweener.OnUpdate(() =>
                {
                    if (pathTweener != null && pathTweener.IsActive() && target != null)
                    {
                        Vector3 currentPosition = target.position;
                        
                        // 根据 X 方向决定是否翻转
                        if (currentPosition.x > lastPosition.x)
                        {
                            spriteRenderer.flipX = originalFlipX;
                        }
                        else if (currentPosition.x < lastPosition.x)
                        {
                            spriteRenderer.flipX = !originalFlipX;
                        }
                        
                        lastPosition = currentPosition;
                    }
                });
            }

            // 设置完成回调
            pathTweener.OnComplete(() =>
            {
                IsRunning = false;
                IsDone = true;
            });

            // 设置杀死回调
            pathTweener.OnKill(() =>
            {
                IsRunning = false;
            });
        }

        /// <summary>
        /// 停止路径动画
        /// </summary>
        public void Stop()
        {
            if (pathTweener != null && pathTweener.IsActive())
            {
                pathTweener.Kill();
            }
            IsRunning = false;
            IsDone = true;
        }

        /// <summary>
        /// 暂停路径动画
        /// </summary>
        public void Pause()
        {
            if (pathTweener != null && pathTweener.IsActive())
            {
                pathTweener.Pause();
            }
        }

        /// <summary>
        /// 恢复路径动画
        /// </summary>
        public void Resume()
        {
            if (pathTweener != null && pathTweener.IsActive())
            {
                pathTweener.Play();
            }
        }

        /// <summary>
        /// 重置到起始位置
        /// </summary>
        public void ResetToStart()
        {
            Stop();
            if (target != null)
            {
                // 重置到初始位置（target 的当前位置）
                target.position = target.position;
            }

            // 重置 Sprite 翻转状态
            if (flipSpriteByDirection && spriteRenderer != null)
            {
                spriteRenderer.flipX = originalFlipX;
            }
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
        /// 获取用于绘制的路径点
        /// </summary>
        private Vector3[] GetDrawPoints(List<Vector3> pathPoints)
        {
            if (pathPoints == null || pathPoints.Count < 2)
                return null;

            // 使用 DOTween 的路径插件计算曲线点
            int totalPoints = resolution * (pathPoints.Count - 1) + 1;
            Vector3[] drawPoints = new Vector3[totalPoints];

            // 简单的线性插值用于预览（实际运行时 DOTween 会使用更复杂的曲线）
            if (pathType == PathType.Linear)
            {
                int pointIndex = 0;
                for (int i = 0; i < pathPoints.Count - 1; i++)
                {
                    for (int j = 0; j <= resolution; j++)
                    {
                        if (pointIndex >= totalPoints) break;
                        float t = j / (float)resolution;
                        drawPoints[pointIndex] = Vector3.Lerp(pathPoints[i], pathPoints[i + 1], t);
                        pointIndex++;
                    }
                }
            }
            else
            {
                // 对于曲线路径，使用 Catmull-Rom 样条插值
                Vector3[] pointsArray = pathPoints.ToArray();
                for (int i = 0; i < totalPoints; i++)
                {
                    float t = i / (float)(totalPoints - 1);
                    drawPoints[i] = GetCatmullRomPoint(t, pointsArray);
                }
            }

            return drawPoints;
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
            {
                pathTweener.Kill();
            }
        }
    }
}
