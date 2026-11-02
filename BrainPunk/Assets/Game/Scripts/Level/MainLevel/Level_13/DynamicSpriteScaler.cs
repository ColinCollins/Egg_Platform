using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Level.Level13
{
    /// <summary>
    /// 动态 SpriteRenderer 缩放控制器
    /// 根据目标位置超出检测范围的程度，动态调整 SpriteRenderer 的大小
    /// </summary>
    public class DynamicSpriteScaler : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("要检测的目标位置")]
        [SerializeField] private Transform target;

        [Header("Detection Bounds")]
        [Tooltip("检测范围比例（基于 SpriteRenderer 原始大小的比例）")]
        [SerializeField] private Vector2 boundsScale = new Vector2(1.5f, 1.5f);

        [Header("Sprite Settings")]
        [Tooltip("要缩放的 SpriteRenderer（如果为空则自动从当前物体获取）")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Vector2 originalSize = Vector2.one;
        private Vector2 currentSize = Vector2.one;

        [SerializeField] private float ScaleFactory = 1.5f;

        [Header("Debug")]
        [Tooltip("是否显示检测范围")]
        [SerializeField] private bool showBounds = true;

        [Tooltip("检测范围颜色")]
        [SerializeField] private Color boundsColor = Color.yellow;

        private Bounds detectionBounds;

        void Awake()
        {
            // 自动获取当前对象的 SpriteRenderer
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                Debug.LogError("[DynamicSpriteScaler] SpriteRenderer is not found on " + gameObject.name + "!");
                enabled = false;
                return;
            }

            originalSize = spriteRenderer.size;
            currentSize = originalSize;
        }

        void Start()
        {
            UpdateDetectionBounds();
        }

        void Update()
        {
            if (target == null || spriteRenderer == null)
                return;

            UpdateDetectionBounds();
            ApplyScale();
        }

        /// <summary>
        /// 更新检测范围
        /// </summary>
        private void UpdateDetectionBounds()
        {
            // 使用当前 transform 的中心位置
            Vector3 worldCenter = transform.position;

            // 基于 SpriteRenderer 的原始大小和比例计算检测范围
            Vector3 calculatedBoundsSize = CalculateBoundsSize();
            detectionBounds = new Bounds(worldCenter, calculatedBoundsSize);
        }

        /// <summary>
        /// 计算检测范围大小（基于原始大小和比例）
        /// </summary>
        private Vector3 CalculateBoundsSize()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                // 如果没有 Sprite，使用 originalSize 和 boundsScale
                return new Vector3(currentSize.x * boundsScale.x, currentSize.y * boundsScale.y, 0f);
            }

            // 获取 Sprite 的实际大小（世界空间）
            // Vector2 spriteSize = spriteRenderer.sprite.bounds.size;

            // 考虑原始大小
            float worldWidth = currentSize.x * boundsScale.x;
            float worldHeight = currentSize.y * boundsScale.y;

            return new Vector3(worldWidth, worldHeight, 0f);
        }

        /// <summary>
        /// 应用缩放（target 超出范围时，根据距离进行缩放，无上限）
        /// </summary>
        private void ApplyScale()
        {
            Vector3 targetPos = target.position;
            float appliedScale = ScaleFactory;

            // 检查目标是否在检测范围内
            if (detectionBounds.Contains(targetPos))
                return;

            // 确保不小于原始大小
            // appliedScale = Mathf.Max(appliedScale, 1f);

            // 直接应用缩放：Size * Scale
            var newSize = currentSize * appliedScale;

            currentSize = new Vector2(Mathf.Max(newSize.x, originalSize.x), Mathf.Max(newSize.y, originalSize.y));
            spriteRenderer.size = currentSize;

        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // 在 Editor 模式下，如果 spriteRenderer 为空，自动获取
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                originalSize = spriteRenderer.size;
            }
        }
#endif

        void OnDrawGizmos()
        {
            if (!showBounds)
                return;

            DrawBoundsGizmos(false);
        }

        void OnDrawGizmosSelected()
        {
            if (!showBounds)
                return;

            DrawBoundsGizmos(true);
        }

        /// <summary>
        /// 绘制边界调试信息
        /// </summary>
        private void DrawBoundsGizmos(bool isSelected)
        {
            // 确保 spriteRenderer 已获取
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
                return;

            // 使用当前 transform 的中心位置
            Vector3 worldCenter = transform.position;
            Vector3 calculatedBoundsSize = CalculateBoundsSize();
            Bounds bounds = new Bounds(worldCenter, calculatedBoundsSize);

            // 绘制检测范围
            Gizmos.color = boundsColor;
            DrawBounds(bounds);

            // 选中时显示更详细的调试信息
            if (isSelected)
            {
                // 绘制中心点
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(worldCenter, 0.5f);

                // 绘制到目标的连线（如果已设置）
                if (target != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(worldCenter, target.position);

                    // 如果目标超出范围，绘制距离线
                    if (!bounds.Contains(target.position))
                    {
                        Vector3 closestPoint = bounds.ClosestPoint(target.position);
                        Gizmos.color = Color.red;
                        Gizmos.DrawLine(target.position, closestPoint);
                    }
                }
            }
        }

        /// <summary>
        /// 绘制边界框（2D）
        /// </summary>
        private void DrawBounds(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float z = bounds.center.z;

            // 绘制2D边界框的4条边
            Gizmos.DrawLine(new Vector3(min.x, min.y, z), new Vector3(max.x, min.y, z));
            Gizmos.DrawLine(new Vector3(max.x, min.y, z), new Vector3(max.x, max.y, z));
            Gizmos.DrawLine(new Vector3(max.x, max.y, z), new Vector3(min.x, max.y, z));
            Gizmos.DrawLine(new Vector3(min.x, max.y, z), new Vector3(min.x, min.y, z));
        }

        /// <summary>
        /// 获取当前缩放比例
        /// </summary>
        public float GetCurrentScale()
        {
            if (spriteRenderer == null || originalSize.x == 0f)
                return 1f;

            // 计算当前缩放比例（相对于原始大小）
            Vector2 currentSize = spriteRenderer.size;
            return currentSize.x / originalSize.x;
        }

        /// <summary>
        /// 获取检测范围
        /// </summary>
        public Bounds GetDetectionBounds()
        {
            UpdateDetectionBounds();
            return detectionBounds;
        }

        /// <summary>
        /// 重置到原始大小
        /// </summary>
        public void ResetScale()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.size = originalSize;
            }
        }
    }
}
