using Bear.Logger;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 检测 star，如果 Start 的时候 star activeSelf = false 则直接隐藏
/// // 如果 start 之后 update 过程中 star activSelf = false。则渐隐
/// 
/// 渐隐要求 content 和 bubble 一起隐藏
/// </summary>
public class GameObjectActiveSelfListener_Bubble : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    [SerializeField] private GameObject star;
    [SerializeField] private SpriteRenderer bubble;
    [SerializeField] private TMP_Text content;

    /// <summary>
    /// Star 在运行中隐藏后，气泡和文字共同渐隐的时长；为 0 时立即隐藏。
    /// </summary>
    [SerializeField, Min(0f)] private float fadeDuration = 0.3f;

    #endregion

    #region Runtime State

    private Sequence fadeSequence;
    private bool isFading;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (bubble == null)
            bubble = GetComponentInChildren<SpriteRenderer>(true);

        if (content == null)
            content = GetComponentInChildren<TMP_Text>(true);

        if (star == null)
        {
            this.LogWarning("[GameObjectActiveSelfListener_Bubble] Star is missing, bubble hidden.");
            gameObject.SetActive(false);
            return;
        }

        if (!star.activeSelf)
            gameObject.SetActive(false);
    }

    private void Update()
    {
        if (isFading || star == null || star.activeSelf)
            return;

        isFading = true;
        FadeOut();
    }

    private void OnDisable()
    {
        fadeSequence?.Kill();
        fadeSequence = null;
        isFading = false;
    }

    #endregion

    #region Fade

    private void FadeOut()
    {
        float duration = Mathf.Max(0f, fadeDuration);
        if (duration <= 0f || (bubble == null && content == null))
        {
            gameObject.SetActive(false);
            return;
        }

        fadeSequence = DOTween.Sequence().SetLink(gameObject);
        if (bubble != null)
            fadeSequence.Join(bubble.DOFade(0f, duration));

        if (content != null)
            fadeSequence.Join(content.DOFade(0f, duration));

        fadeSequence.OnComplete(() =>
        {
            fadeSequence = null;
            gameObject.SetActive(false);
        });
    }

    #endregion
}
