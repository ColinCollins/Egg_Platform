using DG.Tweening;
using Game.Common;
using TMPro;
using UnityEngine;

/// <summary>
/// 分数飞字效果，显示减少的分数并向上飞逐渐透明
/// </summary>
public class ScoreFlyText : MonoBehaviour, IRecycle
{
    [SerializeField] private TextMeshProUGUI textMesh;
    [SerializeField] private RectTransform rectTransform;

    [SerializeField] private Transform root;

    private Sequence _sequence;
    private bool _fromPool;

    private static bool _isRegistering;

    private void Awake()
    {
        if (textMesh == null)
            textMesh = GetComponentInChildren<TextMeshProUGUI>();

        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        RegisterScoreFlyTextPool();

        gameObject.SetActive(false);
    }

    private void RegisterScoreFlyTextPool()
    {
        if (_isRegistering)
        {
            return;
        }

        if (ObjectPoolManager.Instance != null)
        {
            if (ObjectPoolManager.Instance.IsPoolRegistered<ScoreFlyText>())
                return;

            _isRegistering = true;
            try
            {
                ObjectPoolManager.Instance.RegisterPool(
                    () => Instantiate(this),
                    initialSize: 3,
                    maxSize: 10
                );
            }
            finally
            {
                _isRegistering = false;
            }
        }

        // 如果没有指定父节点，尝试从 score 的父节点获取 Canvas
        if (root == null)
        {
            Canvas canvas = this.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                root = canvas.transform;
            }
            else
            {
                root = transform; // 回退到当前 transform
            }
        }
    }

    /// <summary>
    /// 设置分数并播放飞字动画
    /// </summary>
    /// <param name="scoreValue">分数值（会显示为 -scoreValue）</param>
    /// <param name="startPosition">起始世界坐标位置</param>
    /// <param name="parent">父节点（Canvas 或 UI 根节点）</param>
    /// <param name="flyDistance">向上飞的距离（像素）</param>
    /// <param name="duration">动画持续时间</param>
    public void Setup(int scoreValue, Vector3 startPosition, Transform parent, float flyDistance = 100f, float duration = 1f)
    {
        if (textMesh == null || rectTransform == null)
        {
            Debug.LogWarning("[ScoreFlyText] TextMesh or RectTransform is null!");
            return;
        }

        // 设置父节点和位置
        transform.SetParent(parent, false);
        transform.localScale = Vector3.one;

        // 如果是 UI，需要转换世界坐标到屏幕坐标
        if (parent != null && parent.GetComponent<Canvas>() != null)
        {
            Canvas canvas = parent.GetComponent<Canvas>();
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, startPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent as RectTransform, screenPos, cam, out Vector2 localPos);
            rectTransform.anchoredPosition = localPos;
        }
        else
        {
            // 世界空间位置
            transform.position = startPosition;
        }

        // 设置文本（显示负数）
        textMesh.text = $"-{scoreValue}";

        // 重置状态
        Color color = textMesh.color;
        color.a = 1f;
        textMesh.color = color;

        // 停止之前的动画
        _sequence?.Kill();

        // 创建动画序列：向上移动 + 淡出
        _sequence = DOTween.Sequence();
        _sequence.Append(rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + flyDistance, duration).SetEase(Ease.OutQuad));
        _sequence.Join(textMesh.DOFade(0f, duration).SetEase(Ease.InQuad));

        _sequence.OnComplete(OnAnimationComplete);
    }

    private void OnAnimationComplete()
    {
        _sequence = null;
        if (_fromPool && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Recycle(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnSpawn()
    {
        _fromPool = true;
        gameObject.SetActive(true);
    }

    public void OnRecycle()
    {
        _fromPool = false;
        _sequence?.Kill();
        _sequence = null;

        // 重置状态
        if (textMesh != null)
        {
            Color color = textMesh.color;
            color.a = 255f;
            textMesh.color = color;
        }

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
        _sequence = null;
    }
}
