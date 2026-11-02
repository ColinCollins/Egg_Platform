using UnityEngine;
using UnityEngine.UI;

/// <summary>本关相机固定单次开始按钮；沿用 069 关按下/松开手感，不修改共享按钮。</summary>
[DisallowMultipleComponent]
public sealed class Level150Hud : MonoBehaviour
{
    private CustomButton startButton;
    private Level150PressScale pressFeedback;
    private bool startRequested;
    private bool pressHeld;
    private bool canPress = true;
    private Level150Ctrl owner;

    public void Configure(Level150Ctrl controller, Camera camera, Sprite buttonIdleSprite,
        Sprite buttonPressedSprite)
    {
        owner = controller;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 2f;
        // ScreenSpaceCamera participates in sorting layers: Default is behind CommonBg.
        // 场景按钮在背景之上、角色 SceneLayer/200 之后，避免盖住跳到此处的角色。
        canvas.sortingLayerID = SortingLayer.NameToID("SceneLayer");
        canvas.sortingOrder = 199;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();

        // The face-on Level 69 button sits high against the backdrop, not on the ground.
        // Match its 2x2-world-unit BoxCollider2D as well as its artwork scale.
        float pixelsPerWorldUnit = 1080f / (2f * camera.orthographicSize);
        var buttonObject = new GameObject("Start", typeof(RectTransform), typeof(UIEmptyRaycastTarget));
        buttonObject.SetActive(false);
        buttonObject.layer = 5;
        buttonObject.transform.SetParent(transform, false);
        var buttonRect = (RectTransform)buttonObject.transform;
        SetRect(buttonRect, new Vector2(0.5f, 0.65f), Vector2.one * (2f * pixelsPerWorldUnit));

        var visualObject = new GameObject("Visual", typeof(RectTransform));
        visualObject.layer = 5;
        visualObject.transform.SetParent(buttonRect, false);
        var visualRect = (RectTransform)visualObject.transform;
        SetRect(visualRect, new Vector2(0.5f, 0.5f), buttonRect.sizeDelta);
        // Level 69 uses two complete button faces, not two stacked parts. Its prefab
        // renders at scale 1 with 100 PPU; convert those world dimensions into this
        // 1080-height camera canvas so the resting face retains the same screen size.
        Image idleImage = CreateButtonSprite("Idle", visualRect, buttonIdleSprite,
            pixelsPerWorldUnit);
        Image pressedImage = CreateButtonSprite("Pressed", visualRect, buttonPressedSprite,
            pixelsPerWorldUnit);
        pressedImage.enabled = false;

        // Match Level 69: switch the full face immediately on press, restore on
        // release/exit, with no scale tween, generic release sound or vibration.
        // Build while inactive so CustomButton caches the local feedback on Awake.
        pressFeedback = buttonObject.AddComponent<Level150PressScale>();
        pressFeedback.SetButtonFaces(idleImage, pressedImage);
        pressFeedback.ReleasedOutside += OnStartReleasedOutside;
        startButton = buttonObject.AddComponent<CustomButton>();
        startButton.OnClickDown += OnStartPressed;
        startButton.OnClickUp += OnStartReleased;
        buttonObject.SetActive(true);
    }

    private static Image CreateButtonSprite(string name, Transform parent, Sprite sprite,
        float pixelsPerWorldUnit)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        SetRect(rect, new Vector2(0.5f, 0.5f),
            new Vector2(sprite.bounds.size.x, sprite.bounds.size.y) * pixelsPerWorldUnit);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private void OnDestroy()
    {
        if (pressFeedback != null)
            pressFeedback.ReleasedOutside -= OnStartReleasedOutside;
        if (startButton != null)
        {
            startButton.OnClickDown -= OnStartPressed;
            startButton.OnClickUp -= OnStartReleased;
        }
    }

    private void OnStartPressed(CustomButton button)
    {
        if (!canPress || startRequested || pressHeld)
            return;
        pressHeld = true;
        // 首次有效按下即开始预警，长按或重复按下不重复启动。
        if (owner.HandleStartButtonClick())
            SetStarted();
    }

    private void OnStartReleased(CustomButton button) => FinishPress();

    private void OnStartReleasedOutside() => FinishPress();

    private void FinishPress()
    {
        if (!pressHeld)
            return;
        pressHeld = false;
        if (startRequested)
            HideStartButton();
    }

    private void HideStartButton()
    {
        startRequested = false;
        startButton.gameObject.SetActive(false);
    }

    public void SetStarted()
    {
        // 有效按下即开始攻击，保持按下图直到松开或移出后隐藏。
        if (startRequested)
            return;
        startRequested = true;
        startButton.IsBlockClick = true;
    }

    public void SetInteractable(bool value)
    {
        // CustomButton's IsBlockClick does not block PointerDown; guard both the
        // visual and business path when loading/paused, without tinting the image.
        canPress = value;
        if (pressFeedback != null)
            pressFeedback.SetInputEnabled(value);
        if (startButton != null)
            startButton.IsBlockClick = !value;
    }

}
