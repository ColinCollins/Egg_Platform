using DG.Tweening;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;

/// <summary>
/// Level110 单个问号块：只接受角色从下方顶撞，并循环显示 0 到 9。
/// </summary>
public sealed class Level110QuestionDigit : MonoBehaviour
{
    #region Inspector

    [Tooltip("Level110 总控。")]
    [SerializeField] private Level110Ctrl owner;

    [Tooltip("块内显示问号或数字的 TMP 文本。")]
    [SerializeField] private TMP_Text digitText;

    [Tooltip("切换为数字后使用的直立字体；问号初态仍保留题面原字体。")]
    [SerializeField] private TMP_FontAsset numberFont;

    [Tooltip("数字状态使用的对称边距，避免沿用问号字形边距后发生挤压和偏斜。")]
    [SerializeField] private Vector4 numberMargin = new Vector4(9f, 1f, 9f, 1f);

    [Tooltip("数字状态使用的固定字号；关闭 TMP 自动字号，避免切换字体后放大到上限。")]
    [SerializeField, Min(1f)] private float numberFontSize = 7.5f;

    [Tooltip("答案中的位置，0/1/2 分别表示百位、十位、个位。")]
    [SerializeField, Range(0, 2)] private int digitIndex;

    [Tooltip("判定角色确实接触方块底面的纵向容差。")]
    [SerializeField, Min(0f)] private float undersideContactTolerance = 0.16f;

    [Tooltip("数字滚出、滚入时相对初始位置的距离。")]
    [SerializeField, Min(0.01f)] private float rollDistance = 0.32f;

    [Tooltip("一次完整数字滚动的持续时间。")]
    [SerializeField, Min(0.05f)] private float rollDuration = 0.18f;

    [Tooltip("同一方块连续触发的最短间隔，避免一次碰撞被重复计数。")]
    [SerializeField, Min(0f)] private float hitCooldown = 0.12f;

    #endregion

    #region Runtime State

    private BoxCollider2D blockCollider;
    private Tween rollTween;
    private Vector3 textOriginLocalPosition;
    private Color textOriginColor = Color.white;
    private int currentValue = -1;
    private float nextAllowedHitTime;
    private bool acceptHits = true;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        blockCollider = GetComponent<BoxCollider2D>();
        if (digitText != null)
        {
            textOriginLocalPosition = digitText.transform.localPosition;
            textOriginColor = digitText.color;
        }
    }

    private void OnDisable()
    {
        rollTween?.Kill();
        rollTween = null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!acceptHits || Time.time < nextAllowedHitTime || owner == null)
            return;

        ActorCtrl hitActor = collision.collider.GetComponentInParent<ActorCtrl>();
        if (!owner.IsControlledActor(hitActor) || !IsUndersideHit(collision, hitActor))
            return;

        nextAllowedHitTime = Time.time + Mathf.Max(0f, hitCooldown);
        AdvanceDigit();
    }

    #endregion

    #region Public Control

    /// <summary>
    /// 开关本数字块的顶撞响应；答案成立后关闭，避免出口已经解锁又继续改数。
    /// </summary>
    public void SetAcceptHits(bool value)
    {
        acceptHits = value;
    }

    #endregion

    #region Hit And Roll

    private bool IsUndersideHit(Collision2D collision, ActorCtrl hitActor)
    {
        if (blockCollider == null || hitActor == null)
            return false;

        Bounds bounds = blockCollider.bounds;
        if (hitActor.transform.position.y >= bounds.center.y)
            return false;

        int contactCount = collision.contactCount;
        for (int i = 0; i < contactCount; i++)
        {
            if (collision.GetContact(i).point.y <= bounds.min.y + undersideContactTolerance)
                return true;
        }

        return false;
    }

    private void AdvanceDigit()
    {
        currentValue = currentValue < 0 ? 0 : (currentValue + 1) % 10;
        AudioManager.PlaySound("getCoin", randomPitch: true);

        if (digitText == null)
        {
            owner.OnDigitChanged(digitIndex, currentValue);
            return;
        }

        rollTween?.Kill();
        digitText.transform.localPosition = textOriginLocalPosition;
        digitText.color = textOriginColor;

        float halfDuration = Mathf.Max(0.025f, rollDuration * 0.5f);
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(digitText.transform
            .DOLocalMoveY(textOriginLocalPosition.y + rollDistance, halfDuration)
            .SetEase(Ease.InQuad));
        sequence.Join(digitText.DOFade(0f, halfDuration).SetEase(Ease.InQuad));
        sequence.AppendCallback(ApplyCurrentDigit);
        sequence.Append(digitText.transform
            .DOLocalMoveY(textOriginLocalPosition.y, halfDuration)
            .SetEase(Ease.OutQuad));
        sequence.Join(digitText.DOFade(textOriginColor.a, halfDuration).SetEase(Ease.OutQuad));
        sequence.OnComplete(() => rollTween = null);
        rollTween = sequence;
    }

    private void ApplyCurrentDigit()
    {
        if (numberFont != null)
            digitText.font = numberFont;
        digitText.margin = numberMargin;
        digitText.enableAutoSizing = false;
        digitText.fontSize = Mathf.Max(1f, numberFontSize);
        digitText.fontStyle &= ~FontStyles.Italic;
        digitText.alignment = TextAlignmentOptions.Center;
        digitText.text = currentValue.ToString();
        digitText.ForceMeshUpdate(true, true);
        digitText.transform.localPosition = textOriginLocalPosition - Vector3.up * rollDistance;
        digitText.color = new Color(
            textOriginColor.r,
            textOriginColor.g,
            textOriginColor.b,
            0f);
        owner.OnDigitChanged(digitIndex, currentValue);
    }

    #endregion
}
