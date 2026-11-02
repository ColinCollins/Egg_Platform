using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 影子控制脚本 - 使用多射线检测，按平面分组绘制圆形阴影
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ShadowController : MonoBehaviour
{
    [Header("角色引用")]
    [Tooltip("要跟踪的角色Transform，如果为空则自动查找ActorCtrl")]
    [SerializeField] private Transform characterTransform;

    [Header("角色尺寸")]
    [Tooltip("角色宽度（用于计算射线分布），如果为0则尝试从Collider获取")]
    [SerializeField] private float characterWidth = 0f;

    [Header("射线检测设置")]
    [Tooltip("地面检测层")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("忽略检测层")]
    [SerializeField] private LayerMask ignoreLayer;

    [Tooltip("射线检测的最大距离")]
    [SerializeField] private float maxRaycastDistance = 10f;

    [Tooltip("最大射线数量（用于计算分布，实际使用会根据情况动态调整）")]
    [Range(5, 100)]
    [SerializeField] private int maxRayCount = 5;

    [Header("显示控制")]
    [Tooltip("影子消失的最大距离（超过此距离不显示影子）")]
    [SerializeField] private float maxShadowDistance = 5f;

    [Tooltip("高度容差（相同高度判断的容差，小于此值的视为同一平面）")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float heightTolerance = 0.1f;

    [Header("大小控制")]
    [Tooltip("最小缩放比例（距离最远时）")]
    [Range(0f, 1f)]
    [SerializeField] private float minScale = 0.3f;

    [Tooltip("最大缩放比例（距离最近时）")]
    [Range(0f, 2f)]
    [SerializeField] private float maxScale = 1f;

    [Tooltip("大小变化曲线（X轴：归一化距离 0-1，Y轴：缩放比例）")]
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);

    [Header("透明度控制")]
    [Tooltip("最小透明度（距离最远时，0=完全透明，1=完全不透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float minAlpha = 0.2f;

    [Tooltip("最大透明度（距离最近时）")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 1f;

    [Tooltip("透明度变化曲线（X轴：归一化距离 0-1，Y轴：透明度）")]
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);

    [Header("位置偏移")]
    [Tooltip("影子相对于角色位置的Y偏移（通常为负值，放在角色下方）")]
    [SerializeField] private float offsetY = -0.1f;

    [Header("地面检测")]
    [Tooltip("判断角色是否在地面的距离阈值（小于此值认为在地面）")]
    [SerializeField] private float groundCheckDistance = 0.1f;

    [Header("调试")]
    [Tooltip("是否显示调试信息")]
    [SerializeField] private bool showDebugInfo = false;

    [Tooltip("是否显示射线")]
    [SerializeField] private bool showRays = false;

    private SpriteRenderer shadowRenderer;
    private Sprite shadowSprite;
    private Color originalColor;
    private List<GameObject> shadowObjects = new List<GameObject>();

    /// <summary>
    /// 射线检测结果
    /// </summary>
    private class RayHitInfo
    {
        public int rayIndex;
        public float rayOffsetX;
        public Vector2 hitPoint;
        public float distance;
    }

    void Start()
    {
        shadowRenderer = GetComponent<SpriteRenderer>();
        shadowSprite = shadowRenderer.sprite;
        originalColor = shadowRenderer.color;

        ClearAllChild();

        // 如果没有指定角色，尝试自动查找
        if (characterTransform == null)
        {
            Debug.LogWarning($"[ShadowController] 未找到角色，请在Inspector中指定characterTransform");
            enabled = false;
            return;
        }

        // 隐藏主影子对象（我们使用动态创建的阴影）
        shadowRenderer.enabled = false;
    }

    void LateUpdate()
    {
        if (characterTransform == null)
            return;

        // 更新阴影
        UpdateShadows();
    }

    /// <summary>
    /// 发射单根射线
    /// </summary>
    private RayHitInfo CastRay(Vector2 characterPos, int rayIndex, int totalRays)
    {
        // 计算角色宽度（如果未设置则从Collider获取）
        float width = characterWidth;
        if (width <= 0f && characterTransform != null)
        {
            Collider2D collider = characterTransform.GetComponent<Collider2D>();
            if (collider != null)
            {
                width = collider.bounds.size.x;
            }
            else
            {
                width = 1f; // 默认值
            }
        }

        // 计算射线起点（从左到右均匀分布）
        float offsetX = (rayIndex / (float)(totalRays - 1) - 0.5f) * width;
        Vector2 rayOrigin = new Vector2(
            characterPos.x + offsetX,
            characterPos.y
        );

        // 向下发射射线
        RaycastHit2D ignoreHit = Physics2D.Raycast(rayOrigin, Vector2.down, maxRaycastDistance, ignoreLayer);

        if (ignoreHit.collider != null)
            return null;

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, maxRaycastDistance, groundLayer);

        if (showRays)
        {
            Debug.DrawLine(rayOrigin, rayOrigin + Vector2.down * maxRaycastDistance,
                hit.collider != null ? Color.green : Color.red);
        }

        if (hit.collider != null)
        {
            float distance = hit.distance;

            // 检查是否超过最大显示距离
            if (distance <= maxShadowDistance)
            {
                return new RayHitInfo
                {
                    rayIndex = rayIndex,
                    rayOffsetX = offsetX,
                    hitPoint = hit.point,
                    distance = distance
                };
            }
        }

        return null;
    }

    /// <summary>
    /// 更新阴影
    /// </summary>
    private void UpdateShadows()
    {
        Vector2 characterPos = characterTransform.position;
        List<RayHitInfo> rayHits = new List<RayHitInfo>();

        // 按照 maxRayCount 平均分配所有射线位置进行检测
        for (int rayIndex = 0; rayIndex < maxRayCount; rayIndex++)
        {
            RayHitInfo hit = CastRay(characterPos, rayIndex, maxRayCount);
            if (hit != null)
            {
                rayHits.Add(hit);
            }
        }

        // 如果没有有效的射线，隐藏所有阴影
        if (rayHits.Count == 0)
        {
            SetAllShadowsVisible(false);
            return;
        }

        // 对射线按索引排序，确保顺序正确
        rayHits.Sort((a, b) => a.rayIndex.CompareTo(b.rayIndex));

        // 第二遍：按高度分组（相同平面的射线）
        List<List<RayHitInfo>> planeGroups = new List<List<RayHitInfo>>();

        foreach (RayHitInfo hitInfo in rayHits)
        {
            float hitY = hitInfo.hitPoint.y;
            bool foundGroup = false;

            // 查找是否已有相同高度的组
            for (int j = 0; j < planeGroups.Count; j++)
            {
                // 检查第一个元素的高度（代表该组的高度）
                if (planeGroups[j].Count > 0)
                {
                    float groupY = planeGroups[j][0].hitPoint.y;
                    if (Mathf.Abs(hitY - groupY) < heightTolerance)
                    {
                        planeGroups[j].Add(hitInfo);
                        foundGroup = true;
                        break;
                    }
                }
            }

            // 如果没有找到相同高度的组，创建新组
            if (!foundGroup)
            {
                List<RayHitInfo> newGroup = new List<RayHitInfo> { hitInfo };
                planeGroups.Add(newGroup);
            }
        }

        // 确保有足够的阴影对象
        while (shadowObjects.Count < planeGroups.Count)
        {
            CreateShadowObject();
        }

        // 隐藏多余的阴影对象
        for (int i = planeGroups.Count; i < shadowObjects.Count; i++)
        {
            shadowObjects[i].SetActive(false);
        }

        // 第三遍：为每个平面组绘制阴影
        for (int groupIdx = 0; groupIdx < planeGroups.Count; groupIdx++)
        {
            List<RayHitInfo> group = planeGroups[groupIdx];

            // 按射线索引排序
            group.Sort((a, b) => a.rayIndex.CompareTo(b.rayIndex));

            // 计算该组的第一根和最后一根射线
            RayHitInfo firstRay = group[0];
            RayHitInfo lastRay = group[group.Count - 1];

            // 计算圆心：第一根和最后一根射线的中点
            Vector2 centerPoint = (firstRay.hitPoint + lastRay.hitPoint) * 0.5f;

            // 计算直径：第一根到最后一根射线的距离
            float diameter = Vector2.Distance(firstRay.hitPoint, lastRay.hitPoint);
            diameter = Mathf.Min(1f, diameter);

            // 计算平均距离（用于大小和透明度）
            float avgDistance = 0f;
            foreach (RayHitInfo hitInfo in group)
            {
                avgDistance += hitInfo.distance;
            }
            avgDistance /= group.Count;

            // 判断角色是否在地面（基础状态）
            bool isGrounded = IsCharacterGrounded();

            // 计算归一化距离（0=最近，1=最远）
            float normalizedDistance = Mathf.Clamp01(avgDistance / maxShadowDistance);

            Vector3 finalScale;

            if (isGrounded)
            {
                // 基础状态：子阴影 localScale 设置为 Vector3.one，视觉上与父物体保持一致
                // 因为子物体的世界 scale = 父物体 scale * 子物体 localScale
                finalScale = Vector3.one;
            }
            else
            {
                // 跳跃状态：根据距离进行缩放
                float scaleFactor = scaleCurve.Evaluate(normalizedDistance);
                scaleFactor = Mathf.Lerp(minScale, maxScale, 1f - normalizedDistance);

                // 计算最终scale（直径作为基础，然后应用距离scale）
                // 注意：这里计算的是 localScale，需要考虑父物体的 scale
                float baseDiameter = shadowSprite != null ? shadowSprite.bounds.size.x : 1f;
                float scaleX = (diameter / baseDiameter) * scaleFactor;
                float scaleY = scaleFactor;
                finalScale = new Vector3(scaleX, scaleY, 1f);
            }

            // 根据距离计算透明度
            float alphaFactor = alphaCurve.Evaluate(normalizedDistance);
            alphaFactor = Mathf.Lerp(minAlpha, maxAlpha, 1f - normalizedDistance);
            Color shadowColor = originalColor;
            shadowColor.a = alphaFactor;

            // 更新阴影对象
            GameObject shadowObj = shadowObjects[groupIdx];
            shadowObj.SetActive(true);

            // 设置位置
            Vector3 shadowPosition = centerPoint;
            shadowPosition.y += offsetY;
            shadowPosition.z = transform.position.z;
            shadowObj.transform.position = shadowPosition;

            // 设置大小
            shadowObj.transform.localScale = finalScale;

            // 设置透明度
            SpriteRenderer sr = shadowObj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = shadowColor;
            }
        }

        if (showDebugInfo && planeGroups.Count > 0)
        {
            Debug.Log($"[ShadowController] 显示 {planeGroups.Count} 个平面组的阴影");
        }
    }

    /// <summary>
    /// 创建阴影对象
    /// </summary>
    private void CreateShadowObject()
    {
        GameObject shadowObj = new GameObject($"Shadow_{shadowObjects.Count}");
        shadowObj.transform.SetParent(transform);
        shadowObj.transform.localPosition = Vector3.zero;
        shadowObj.transform.localRotation = Quaternion.identity;
        shadowObj.layer = transform.gameObject.layer;

        SpriteRenderer sr = shadowObj.AddComponent<SpriteRenderer>();
        sr.sprite = shadowSprite;
        sr.color = originalColor;
        sr.sortingOrder = shadowRenderer.sortingOrder;

        shadowObj.SetActive(false);
        shadowObjects.Add(shadowObj);
    }

    /// <summary>
    /// 设置所有影子可见性
    /// </summary>
    private void SetAllShadowsVisible(bool visible)
    {
        foreach (GameObject shadowObj in shadowObjects)
        {
            if (shadowObj != null)
            {
                shadowObj.SetActive(visible);
            }
        }
    }

    /// <summary>
    /// 设置角色Transform
    /// </summary>
    public void SetCharacter(Transform character)
    {
        characterTransform = character;
    }

    /// <summary>
    /// 设置地面层
    /// </summary>
    public void SetGroundLayer(LayerMask layer)
    {
        groundLayer = layer;
    }

    /// <summary>
    /// 检测角色是否在地面（基础状态）
    /// </summary>
    private bool IsCharacterGrounded()
    {
        if (characterTransform == null)
            return false;

        Vector2 characterPos = characterTransform.position;
        RaycastHit2D hit = Physics2D.Raycast(characterPos, Vector2.down, groundCheckDistance, groundLayer);

        return hit.collider != null && hit.distance <= groundCheckDistance;
    }
    void OnDrawGizmosSelected()
    {
        if (characterTransform == null)
            return;

        // 计算角色宽度（用于预览）
        float width = characterWidth > 0 ? characterWidth : 1f;
        if (characterWidth == 0)
        {
            Collider2D collider = characterTransform.GetComponent<Collider2D>();
            if (collider != null)
                width = collider.bounds.size.x;
        }

        Vector2 characterPos = characterTransform.position;

        // 按照 maxRayCount 平均分配所有射线位置
        for (int rayIndex = 0; rayIndex < maxRayCount; rayIndex++)
        {
            // 计算射线起点（从左到右均匀分布）
            float offsetX = (rayIndex / (float)(maxRayCount - 1) - 0.5f) * width;
            Vector2 rayOrigin = new Vector2(characterPos.x + offsetX, characterPos.y);

            // 绘制最大检测距离的射线（黄色）
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.down * maxRaycastDistance);

            // 绘制最大显示距离的射线（红色）
            Gizmos.color = Color.red;
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.down * maxShadowDistance);
        }
    }

    void ClearAllChild()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    void OnDestroy()
    {
        // 清理阴影对象
        foreach (GameObject shadowObj in shadowObjects)
        {
            if (shadowObj != null)
            {
                Destroy(shadowObj);
            }
        }
        shadowObjects.Clear();
    }
}
