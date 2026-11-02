using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>本关世界空间进度条：灰底、裁切的绿色填充、最上层透明边框，共用中心。</summary>
public sealed class Level141ProgressView
{
    /// <summary>世界空间进度条的根节点，供 Level141Ctrl 设置位置和统一销毁。</summary>
    public readonly RectTransform Root;
    private readonly CanvasGroup group;
    private readonly Image fill;
    private readonly TextMeshProUGUI clock;

    /// <summary>
    /// 创建一个不拦截输入的世界空间进度条。
    /// <paramref name="showClock"/> 为 false 时不创建倒计时文字；Level 141 的生长条和盒子条均使用此模式。
    /// </summary>
    public Level141ProgressView(Transform parent, Sprite background, Sprite foreground, Sprite frame,
        TMP_FontAsset font, bool showClock, float width)
    {
        var go = new GameObject(showClock ? "SeedBoxCooldown" : "GrowthProgress",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        Root = go.GetComponent<RectTransform>();
        Root.SetParent(parent, false);
        Root.localScale = Vector3.one * 0.01f;
        Root.sizeDelta = new Vector2(width * 100f, 50f);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "SceneLayer";
        canvas.sortingOrder = 260;
        group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        float height = width * 100f * background.rect.height / background.rect.width;
        CreateImage("Background", background, new Vector2(width * 100f, height));
        // The supplied fill is 151x31 with transparent padding around a 145x24 interior.
        // Preserve its native pixel ratio instead of stretching it to the background rectangle.
        fill = CreateImage("Fill", foreground, foreground.rect.size * (width * 100f / background.rect.width));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        // Draw the transparent frame above both layers, without cropping it with the progress.
        CreateImage("Frame", frame, frame.rect.size * (width * 100f / background.rect.width));
        if (showClock)
        {
            var text = new GameObject("MinutesSeconds", typeof(RectTransform));
            text.transform.SetParent(Root, false);
            clock = text.AddComponent<TextMeshProUGUI>();
            clock.font = font;
            clock.fontSize = 41.6f;
            clock.color = new Color(0.2f, 0.16f, 0.12f);
            clock.alignment = TextAlignmentOptions.Center;
            clock.raycastTarget = false;
            clock.rectTransform.sizeDelta = new Vector2(200f, 45f);
            clock.rectTransform.anchoredPosition = new Vector2(0f, height * 0.5f + 24f);
        }
        Set(0f, 0f);
    }

    private Image CreateImage(string name, Sprite sprite, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(Root, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = size;
        return image;
    }

    /// <summary>更新填充比例、整体透明度和短暂缩放反馈，不改变进度条资源绑定。</summary>
    public void Set(float progress, float alpha, float pulse = 1f)
    {
        fill.fillAmount = Mathf.Clamp01(progress);
        group.alpha = Mathf.Clamp01(alpha);
        Root.localScale = Vector3.one * (0.01f * pulse);
    }

    /// <summary>
    /// 更新可选的分秒文字；只有构造时 <paramref name="showClock"/> 为 true 才会产生可见效果。
    /// Level 141 当前不显示分秒文字，保留该方法仅为组件内部能力的明确边界。
    /// </summary>
    public void SetSeconds(float seconds)
    {
        if (clock == null) return;
        int remaining = Mathf.Max(0, Mathf.CeilToInt(seconds));
        clock.text = $"{remaining / 60}:{remaining % 60:00}";
    }
}
