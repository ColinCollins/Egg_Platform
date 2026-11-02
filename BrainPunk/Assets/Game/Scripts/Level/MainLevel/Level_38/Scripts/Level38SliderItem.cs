using UnityEngine;

public class Level38SliderItem : MonoBehaviour
{
    [SerializeField] private Transform sliderContent;

    public Transform Content => sliderContent;

    [Header("校验配置")]
    [Tooltip("允许的位置误差范围")]
    [SerializeField] private float allowDx = 1f;

    private Vector3 contentOriginalPos;

    void Awake()
    {
        if (sliderContent != null)
        {
            contentOriginalPos = sliderContent.position;
        }
    }

    /// <summary>
    /// 获取校验中心位置（世界坐标）
    /// </summary>
    public Vector3 GetCenterPosition()
    {
  /*       if (sliderContent != null)
            return sliderContent.position; */
        return transform.position;
    }

    /// <summary>
    /// 获取允许的位置误差范围
    /// </summary>
    public float GetAllowDx()
    {
        return allowDx;
    }

    /// <summary>
    /// 设置 content 的显示/隐藏
    /// </summary>
    public void SetContentActive(bool active)
    {
        if (sliderContent != null)
        {
            sliderContent.gameObject.SetActive(active);
            // 激活时恢复原始位置
            if (active)
            {
                sliderContent.position = contentOriginalPos;
            }
        }
    }

    /// <summary>
    /// 设置 content 的 x 坐标位置
    /// </summary>
    public void SetContentPositionX(float x)
    {
        if (sliderContent != null)
        {
            Vector3 pos = sliderContent.position;
            pos.x = x;
            sliderContent.position = pos;
        }
    }

    void OnDrawGizmosSelected()
    {
        // 可视化校验范围
        Vector3 center = GetCenterPosition();
        Gizmos.color = Color.red;
        Gizmos.DrawLine(center + Vector3.left * allowDx, center + Vector3.right * allowDx);
        // Gizmos.DrawWireSphere(center, 1 * allowDx);
    }
}
