using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level081 关卡专用 HUD，仅负责显示、数值刷新和评价动画。
/// 布局与本地化标签均直接制作在 GamePlayPanel_081 变体的 Level_081_HUD 节点中；策划可直接调整实体尺寸、字号和 I2 组件，运行时代码不会改写标签文本。
/// </summary>
public sealed class Level81GameplayHud : MonoBehaviour
{
    #region Types

    /// <summary>
    /// 本轮水果命中评价；只用于选择 HUD 评价图片，不参与分数计算。
    /// </summary>
    public enum Rating
    {
        Well,
        Good,
        Perfect
    }

    #endregion

    #region Authored References

    [Header("根节点")]
    [Tooltip("HUD 根 CanvasGroup，用于整体渐显渐隐；应绑定当前 Prefab 根节点上的组件。")]
    [SerializeField] private CanvasGroup rootGroup;

    [Header("本地化标签实体")]
    [Tooltip("分数标题实体。可在该对象上添加或调整 I2 Localize、RectTransform 与字号；运行时代码不会覆盖其文本。")]
    [SerializeField] private TextMeshProUGUI scoreLocalizedLabel;

    [Tooltip("目标标题实体。可在该对象上添加或调整 I2 Localize、RectTransform 与字号；运行时代码不会覆盖其文本。")]
    [SerializeField] private TextMeshProUGUI targetLocalizedLabel;

    [Tooltip("奖励标题实体。可在该对象上添加或调整 I2 Localize、RectTransform 与字号；运行时代码不会覆盖其文本。")]
    [SerializeField] private TextMeshProUGUI rewardLocalizedLabel;

    [Header("运行时数值实体")]
    [Tooltip("当前分数数字实体；只由本脚本写入非负整数，不应挂本地化组件。")]
    [SerializeField] private TextMeshProUGUI scoreValueText;
    [Tooltip("目标分数数字实体；只由本脚本写入正整数，不应挂本地化组件。")]
    [SerializeField] private TextMeshProUGUI targetValueText;
    [Tooltip("目标钥匙图标实体；创建 HUD 时可由关卡控制器替换 Sprite。")]
    [SerializeField] private Image targetKeyImage;
    [Tooltip("WELL、GOOD、PERFECT 共用的评价图片实体；运行时会切换 Sprite 并播放局部动画。")]
    [SerializeField] private Image ratingImage;

    [Header("评价图片")]
    [Tooltip("WELL 评价图片，仅在 ShowRating(Rating.Well) 时使用。")]
    [SerializeField] private Sprite wellSprite;
    [Tooltip("GOOD 评价图片，仅在 ShowRating(Rating.Good) 时使用。")]
    [SerializeField] private Sprite goodSprite;
    [Tooltip("PERFECT 评价图片，仅在 ShowRating(Rating.Perfect) 时使用。")]
    [SerializeField] private Sprite perfectSprite;

    #endregion

    #region Runtime State

    private Tween visibilityTween;
    private Tween scoreTween;
    private Sequence ratingSequence;

    #endregion

    #region Binding

    /// <summary>
    /// 绑定已经预摆在 GamePlayPanel_081 变体下的 HUD。
    /// HUD 不在运行时实例化；该方法只复位运行时状态并应用关卡动态资源。
    /// </summary>
    /// <param name="targetKey">可选的目标钥匙图标；为 null 时保留 Prefab 上的图片。</param>
    public void InitializeForLevel(Sprite targetKey = null)
    {
        visibilityTween?.Kill();
        scoreTween?.Kill();
        ratingSequence?.Kill();

        if (rootGroup == null)
            rootGroup = GetComponent<CanvasGroup>();

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        SetRaycastTarget(scoreLocalizedLabel, false);
        SetRaycastTarget(targetLocalizedLabel, false);
        SetRaycastTarget(rewardLocalizedLabel, false);
        SetRaycastTarget(scoreValueText, false);
        SetRaycastTarget(targetValueText, false);
        SetRaycastTarget(targetKeyImage, false);
        SetRaycastTarget(ratingImage, false);

        if (targetKeyImage != null && targetKey != null)
            targetKeyImage.sprite = targetKey;

        if (ratingImage != null)
        {
            ratingImage.gameObject.SetActive(false);
            ratingImage.color = Color.white;
            ratingImage.rectTransform.localScale = Vector3.one;
        }

        SetScore(0, false);
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        // 编辑器预览保持预置 HUD 可见；运行时面板创建后立即隐藏。玩法开始
        // 前由 Level81Ctrl 绑定并复位同一个实例。
        if (Application.isPlaying)
            InitializeForLevel();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 播放 HUD 整体渐显；重复调用会先终止上一段显隐 Tween，避免动画叠加。
    /// </summary>
    public void Show()
    {
        if (rootGroup == null)
            return;

        visibilityTween?.Kill();
        visibilityTween = rootGroup
            .DOFade(1f, 0.2f)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    /// <summary>
    /// 播放 HUD 整体渐隐；只改变透明度，不负责销毁实例。
    /// </summary>
    /// <param name="duration">渐隐时长，运行时最小限制为 0.01 秒。</param>
    public void Hide(float duration = 0.2f)
    {
        if (rootGroup == null)
            return;

        visibilityTween?.Kill();
        visibilityTween = rootGroup
            .DOFade(0f, Mathf.Max(0.01f, duration))
            .SetEase(Ease.InQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    /// <summary>
    /// 刷新当前分数数字，可选播放一次轻量回弹；不会改写任何本地化标签。
    /// </summary>
    /// <param name="score">待显示分数，负数会按 0 显示。</param>
    /// <param name="animate">是否播放数字回弹，初始化时应传 false。</param>
    public void SetScore(int score, bool animate = true)
    {
        if (scoreValueText == null)
            return;

        scoreValueText.text = Mathf.Max(0, score).ToString();
        if (!animate)
            return;

        scoreTween?.Kill();
        scoreValueText.rectTransform.localScale = Vector3.one * 0.88f;
        scoreTween = scoreValueText.rectTransform
            .DOScale(1f, 0.22f)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    /// <summary>
    /// 刷新目标分数数字；不会改写目标标题或 I2 组件。
    /// </summary>
    /// <param name="target">待显示目标分数，小于 1 时按 1 显示。</param>
    public void SetTarget(int target)
    {
        if (targetValueText != null)
            targetValueText.text = Mathf.Max(1, target).ToString();
    }

    /// <summary>
    /// 显示指定评价图片并播放一次弹性出现和渐隐；新的评价会替换尚未结束的上一段动画。
    /// </summary>
    /// <param name="rating">本轮要显示的评价档位。</param>
    public void ShowRating(Rating rating)
    {
        if (ratingImage == null)
            return;

        ratingSequence?.Kill();
        ratingImage.sprite = rating switch
        {
            Rating.Well => wellSprite,
            Rating.Good => goodSprite,
            _ => perfectSprite
        };
        ratingImage.gameObject.SetActive(true);
        ratingImage.color = new Color(1f, 1f, 1f, 0f);
        ratingImage.rectTransform.localScale = Vector3.one * 0.45f;

        ratingSequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        ratingSequence.Join(ratingImage.DOFade(1f, 0.12f));
        ratingSequence.Join(
            ratingImage.rectTransform
                .DOScale(1.08f, 0.24f)
                .SetEase(Ease.OutBack));
        ratingSequence.Append(
            ratingImage.rectTransform
                .DOScale(1f, 0.1f)
                .SetEase(Ease.OutQuad));
        ratingSequence.AppendInterval(0.55f);
        ratingSequence.Append(ratingImage.DOFade(0f, 0.18f));
        ratingSequence.OnComplete(() =>
        {
            if (ratingImage != null)
                ratingImage.gameObject.SetActive(false);
        });
    }

    /// <summary>
    /// 同步暂停或恢复 HUD 自有 Tween；只供 Level081 暂停生命周期调用，不改变 HUD 可见状态和游戏时间缩放。
    /// </summary>
    /// <param name="paused">true 暂停当前 Tween，false 从原进度继续。</param>
    public void SetPaused(bool paused)
    {
        if (paused)
        {
            visibilityTween?.Pause();
            scoreTween?.Pause();
            ratingSequence?.Pause();
        }
        else
        {
            visibilityTween?.Play();
            scoreTween?.Play();
            ratingSequence?.Play();
        }
    }

    #endregion

    #region Helpers

    private static void SetRaycastTarget(Graphic graphic, bool value)
    {
        if (graphic != null)
            graphic.raycastTarget = value;
    }

    #endregion

    private void OnDestroy()
    {
        visibilityTween?.Kill();
        scoreTween?.Kill();
        ratingSequence?.Kill();
    }
}
