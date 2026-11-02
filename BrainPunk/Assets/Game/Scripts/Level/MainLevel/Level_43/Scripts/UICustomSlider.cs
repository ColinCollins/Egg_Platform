using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 依托 Image fillAmount 的自定义 slider 组件。不可以直接交互。
/// 提供一个 SetProcess API 对内传入 process 修改 fillAmount
/// handle（如果存在）需要跟随着 amount 的进度位置进行偏移
/// </summary>
public class UICustomSlider : MonoBehaviour
{
    [Tooltip("进度条 Image，使用 Filled 类型")]
    [SerializeField] private Image process;

    [Tooltip("滑块 handle，可选")]
    [SerializeField] private RectTransform handle;

    [Tooltip("handle 的起始位置（进度为 0 时的位置）")]
    [SerializeField] private Vector2 handleStartPos;

    [Tooltip("handle 的结束位置（进度为 1 时的位置）")]
    [SerializeField] private Vector2 handleEndPos;

    [Tooltip("是否反向（从右到左）")]
    [SerializeField] private bool reverse;

    [Tooltip("填充方向（水平或垂直）")]
    [SerializeField] private Slider.Direction direction = Slider.Direction.LeftToRight;

    [Range(0f, 1f)]
    [Tooltip("当前进度（0-1）")]
    [SerializeField] private float currentProgress;

    /// <summary>
    /// 当前进度值（0-1）
    /// </summary>
    public float CurrentProgress => currentProgress;

    void Start()
    {
        // 初始化时应用当前设置的进度
        SetProcess(currentProgress);
    }

    /// <summary>
    /// 设置进度
    /// </summary>
    /// <param name="progress">进度值，范围 0-1</param>
    public void SetProcess(float progress)
    {
        // 限制范围在 0-1
        currentProgress = Mathf.Clamp01(progress);

        // 更新 process 的 fillAmount
        if (process != null)
        {
            process.fillAmount = currentProgress;
        }

        // 更新 handle 位置
        UpdateHandlePosition();
    }

    /// <summary>
    /// 更新 handle 位置，跟随 fillAmount 进度
    /// </summary>
    private void UpdateHandlePosition()
    {
        if (handle == null) return;

        // 根据进度插值计算 handle 位置
        float t = reverse ? 1f - currentProgress : currentProgress;
        Vector2 targetPos = Vector2.Lerp(handleStartPos, handleEndPos, t);
        handle.anchoredPosition = targetPos;
    }

    /// <summary>
    /// 设置 handle 的起始和结束位置
    /// </summary>
    /// <param name="start">起始位置</param>
    /// <param name="end">结束位置</param>
    public void SetHandleRange(Vector2 start, Vector2 end)
    {
        handleStartPos = start;
        handleEndPos = end;
        UpdateHandlePosition();
    }

    /// <summary>
    /// 设置是否反向
    /// </summary>
    /// <param name="isReverse">true 为从右到左</param>
    public void SetReverse(bool isReverse)
    {
        reverse = isReverse;
        UpdateHandlePosition();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 自动根据 process Image 计算 handle 的起始和结束位置
    /// </summary>
    [Button("自动计算 Handle 范围")]
    private void AutoCalculateHandleRange()
    {
        if (process == null)
        {
            Debug.LogError("[UICustomSlider] process 未赋值，无法自动计算", this);
            return;
        }

        RectTransform processRect = process.GetComponent<RectTransform>();
        if (processRect == null)
        {
            Debug.LogError("[UICustomSlider] process 没有 RectTransform", this);
            return;
        }

        // 获取 process 的尺寸
        Vector2 size = processRect.rect.size;

        switch (direction)
        {
            case Slider.Direction.LeftToRight:
                handleStartPos = new Vector2(-size.x / 2, 0);
                handleEndPos = new Vector2(size.x / 2, 0);
                reverse = false;
                break;
            case Slider.Direction.RightToLeft:
                handleStartPos = new Vector2(size.x / 2, 0);
                handleEndPos = new Vector2(-size.x / 2, 0);
                reverse = false;
                break;
            case Slider.Direction.BottomToTop:
                handleStartPos = new Vector2(0, -size.y / 2);
                handleEndPos = new Vector2(0, size.y / 2);
                reverse = false;
                break;
            case Slider.Direction.TopToBottom:
                handleStartPos = new Vector2(0, size.y / 2);
                handleEndPos = new Vector2(0, -size.y / 2);
                reverse = false;
                break;
        }

        // 更新 handle 位置
        UpdateHandlePosition();

        Debug.Log($"[UICustomSlider] 已自动计算 Handle 范围: Start={handleStartPos}, End={handleEndPos}", this);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// 记录当前 handle 位置为 StartPos
    /// </summary>
    [ContextMenu("记录当前位置为 StartPos")]
    private void RecordCurrentAsStart()
    {
        if (handle == null)
        {
            Debug.LogError("[UICustomSlider] handle 未赋值", this);
            return;
        }
        handleStartPos = handle.anchoredPosition;
        UpdateHandlePosition();
        Debug.Log($"[UICustomSlider] 已记录 StartPos: {handleStartPos}", this);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// 记录当前 handle 位置为 EndPos
    /// </summary>
    [ContextMenu("记录当前位置为 EndPos")]
    private void RecordCurrentAsEnd()
    {
        if (handle == null)
        {
            Debug.LogError("[UICustomSlider] handle 未赋值", this);
            return;
        }
        handleEndPos = handle.anchoredPosition;
        UpdateHandlePosition();
        Debug.Log($"[UICustomSlider] 已记录 EndPos: {handleEndPos}", this);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void OnValidate()
    {
        // 编辑器中修改参数时实时更新
        if (process != null)
        {
            process.fillAmount = Mathf.Clamp01(currentProgress);
        }
        UpdateHandlePosition();
    }

    void OnDrawGizmosSelected()
    {
        if (process == null || handle == null) return;

        // 在 Scene 视图中绘制 handle 的移动范围
        Vector3 startWorld = transform.TransformPoint(handleStartPos);
        Vector3 endWorld = transform.TransformPoint(handleEndPos);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(startWorld, endWorld);
        Gizmos.DrawWireSphere(startWorld, 5f);
        Gizmos.DrawWireSphere(endWorld, 5f);
    }
#endif
}
