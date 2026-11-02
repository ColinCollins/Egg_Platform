using Config.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通过 SkinUtils，针对 Skin 类型奖励特殊处理，获取对应的 Sprite 
/// </summary>
public class CommonRewardItem : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI text;

    /// <summary>
    /// 根据传入内容，先切换 icon 和 text
    /// </summary>
    /// <param name="props"></param>
    /// <param name="count"></param>
    public void Refresh(Sprite sprite, int count)
    {
        if (text)
        {
            text.gameObject.SetActive(count > 0);
            if (count > 0)
                text.text = string.Format("x{0}", count);
        }

        if (icon)
        {
            icon.sprite = sprite;
        }
    }

    /// <summary>
    /// 用途：刷新真实 Skin 图标，先按 sprite 原始尺寸显示，再等比限制在当前 icon 框内。
    /// 调用边界：仅用于皮肤奖励图标加载完成后的显示刷新，Coin/Star 仍使用固定尺寸 Refresh。
    /// 注意事项：sprite 为空时只清空图标，不做 NativeSize 缩放。
    /// </summary>
    /// <param name="sprite">需要展示的皮肤图标。</param>
    /// <param name="count">奖励数量；小于等于 0 时隐藏数量文本。</param>
    public void RefreshSkin(Sprite sprite, int count)
    {
        RefreshText(count);

        if (!icon)
            return;

        var rectTransform = icon.rectTransform;
        Vector2 frameSize = GetValidSize(rectTransform.rect.size, rectTransform.sizeDelta);
        icon.sprite = sprite;

        if (sprite == null)
            return;

        icon.SetNativeSize();
        Vector2 nativeSize = GetValidSize(rectTransform.rect.size, rectTransform.sizeDelta);
        if (frameSize.x <= 0f || frameSize.y <= 0f || nativeSize.x <= 0f || nativeSize.y <= 0f)
            return;

        float scale = Mathf.Min(1f, Mathf.Min(frameSize.x / nativeSize.x, frameSize.y / nativeSize.y));
        SetIconSize(rectTransform, nativeSize * scale);
    }

    private void RefreshText(int count)
    {
        if (!text)
            return;

        text.gameObject.SetActive(count > 0);
        if (count > 0)
            text.text = string.Format("x{0}", count);
    }

    private static Vector2 GetValidSize(Vector2 primary, Vector2 fallback)
    {
        return primary.x > 0f && primary.y > 0f ? primary : fallback;
    }

    private static void SetIconSize(RectTransform rectTransform, Vector2 size)
    {
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
    }
}
