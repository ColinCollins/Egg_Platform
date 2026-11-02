using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 让进度手柄跟随目标 Image.fillAmount。
/// </summary>
public class ProcessHandleFollowFillAmount : MonoBehaviour
{
    [Header("Progress Source")]
    [Tooltip("用于读取 fillAmount 的目标 Image。")]
    [SerializeField] private Image target;
    [SerializeField] private GameObject completeFacing;

    [Header("Follow Settings")]
    [Tooltip("需要移动的手柄 RectTransform，不填默认使用当前对象。")]
    [SerializeField] private RectTransform handle;
    [Min(0f)]
    [Tooltip("fillAmount 从 0 到 1 时，手柄在 X 轴上的移动总距离。")]
    [SerializeField] private float width = 100f;

    [Header("Callback")]
    [Tooltip("当 fillAmount 首次达到 1 时触发。")]
    [SerializeField] private UnityEvent onComplete;

    private float _startX;
    private bool _hasCompleted;
    [SerializeField] [Min(0f)] private float completeFacingToggleInterval = 0.1f;
    private float _completeFacingTimer;
    private bool _completeFacingVisible;

    private void Reset()
    {
        handle = transform as RectTransform;
    }

    private void Awake()
    {
        CacheHandleAndStartX();
    }

    private void LateUpdate()
    {
        if (target == null || handle == null)
        {
            return;
        }

        float progress = Mathf.Clamp01(target.fillAmount);
        Vector2 anchoredPos = handle.anchoredPosition;
        anchoredPos.x = _startX + width * progress;
        handle.anchoredPosition = anchoredPos;

        if (!_hasCompleted && progress >= 1f)
        {
            _hasCompleted = true;
            OnComplete();
            _completeFacingTimer = 0f;
            _completeFacingVisible = true;
        }
        else if (_hasCompleted && progress < 1f)
        {
            _hasCompleted = false;
            _completeFacingTimer = 0f;
            _completeFacingVisible = false;
        }

        if (completeFacing != null)
        {
            if (_hasCompleted && completeFacingToggleInterval > 0f)
            {
                _completeFacingTimer += Time.deltaTime;
                if (_completeFacingTimer >= completeFacingToggleInterval)
                {
                    _completeFacingTimer = completeFacingToggleInterval;
                    _completeFacingVisible = !_completeFacingVisible;
                }

                completeFacing.SetActive(_completeFacingVisible);
            }
            else
            {
                completeFacing.SetActive(false);
            }
        }
    }

    public void OnComplete()
    {
        onComplete?.Invoke();
    }

    private void CacheHandleAndStartX()
    {
        if (handle == null)
        {
            handle = transform as RectTransform;
        }

        if (handle != null)
        {
            _startX = handle.anchoredPosition.x;
        }
    }
}
