using UnityEngine;
using UnityEngine.UI;

public class ClickSwitchSprite : ButtonClickTrigger
{
    [SerializeField]
    private Sprite normalSprite;

    [SerializeField]
    private Sprite pressedSprite;

    private SpriteRenderer spriteRenderer;
    private Image image;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        image = GetComponent<Image>();
    }

    public override void OnButtonDown(bool hasAnim)
    {
        if (pressedSprite == null)
            return;

        if (spriteRenderer != null)
            spriteRenderer.sprite = pressedSprite;

        if (image != null)
            image.sprite = pressedSprite;
    }

    public override void OnButtonUp(bool hasAnim)
    {
        if (normalSprite == null)
            return;

        if (spriteRenderer != null)
            spriteRenderer.sprite = normalSprite;

        if (image != null)
            image.sprite = normalSprite;
    }
}
