using Bear.Logger;
using Bear.UI;
using Config;
using DG.Tweening;
using Game.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Global 信息提示
/// </summary>
public class SystemTips : MonoBehaviour, IRecycle, IDebuger
{
    // move dotween
    Sequence sq;
    private RectTransform Rt { get; set; }
    [SerializeField] private RectTransform Target;
    [SerializeField] private CanvasGroup TargetCanvasGroup;

    [SerializeField] private TextMeshProUGUI Content;

    bool isSpawn = false;
    private Canvas _canvas;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (TargetCanvasGroup == null && Target != null)
        {
            TargetCanvasGroup = Target.GetComponent<CanvasGroup>();
        }
    }

    public static SystemTips Create()
    {
        var prefab = Resources.Load<SystemTips>("SystemTips");
        var tips = Instantiate(prefab);
        return tips;
    }

    public static SystemTips Show(string content)
    {
        var topLayer = UIManager.Instance.transform.Find("UIRoot/Top");
        return Show(topLayer, content);
    }

    /// <summary>
    /// 展示
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    public static SystemTips Show(Transform parent, string content)
    {
        if (parent == null)
        {
            return null;
        }

        var tips = ObjectPoolManager.Instance.Get<SystemTips>();
        if (tips == null)
        {
            return null;
        }

        tips.gameObject.name = content;
        tips.transform.SetParent(parent, false);
        tips.transform.SetAsLastSibling();

        // prefab 自带 Canvas 且 overrideSorting，必须通过动态提升 sortingOrder 确保新 tips 在最上层
        if (tips._canvas != null)
        {
            int maxOrder = 450;
            foreach (Transform child in parent)
            {
                if (child == tips.transform) continue;
                var childCanvas = child.GetComponent<Canvas>();
                if (childCanvas != null)
                {
                    maxOrder = Mathf.Max(maxOrder, childCanvas.sortingOrder);
                }
            }
            tips._canvas.sortingOrder = maxOrder + 1;
        }

        tips.Rt = (tips.transform as RectTransform);
        tips.Rt.anchoredPosition = Vector2.zero;
        tips.Rt.localScale = Vector3.one;

        var y = -tips.Target.sizeDelta.y / 2f + 100f;
        tips.Target.anchoredPosition = new Vector2(0, y);

        tips.Content.text = content;
        tips.Content.UpdateFontAsset();
        tips.Content.ForceMeshUpdate(true, true);
        tips.Play();

        return tips;
    }

    // 播放动画
    private void Play()
    {
        var y = Rt.sizeDelta.y / 2f;
        if (TargetCanvasGroup == null && Target != null)
        {
            TargetCanvasGroup = Target.GetComponent<CanvasGroup>();
        }
        if (TargetCanvasGroup != null)
        {
            TargetCanvasGroup.alpha = 0f;
        }

        sq = DOTween.Sequence();
        sq.Append(Target.DOAnchorPosY(200f, 0.3f).SetEase(Ease.OutQuad));
        if (TargetCanvasGroup != null)
        {
            sq.Join(TargetCanvasGroup.DOFade(1f, 0.5f));
        }
        sq.AppendInterval(1f);
        if (TargetCanvasGroup != null)
        {
            sq.Append(TargetCanvasGroup.DOFade(0f, 0.3f));
        }
        sq.Join(Target.DOAnchorPosY(y + 100f, 1f).SetEase(Ease.InQuad));


        sq.OnComplete(OnComplete);

        sq.SetUpdate(true);
    }

    public void OnRecycle()
    {
        isSpawn = false;
        this.Log("Recycle back");
    }

    public void OnSpawn()
    {
        isSpawn = true;
    }

    private void OnDestroy()
    {
        OnComplete();
    }
    private void OnComplete()
    {
        if (this == null || !isSpawn)
            return;
        ObjectPoolManager.Instance.Recycle<SystemTips>(this);

        if (sq != null)
        {
            sq.Kill();
            sq = null;
        }
    }
}
