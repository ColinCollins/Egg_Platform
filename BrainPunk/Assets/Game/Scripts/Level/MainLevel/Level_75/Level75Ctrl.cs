using System.Collections;
using DG.Tweening;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

public class Level75Ctrl : MonoBehaviour
{
    [Header("Level 029 baseline")]
    [SerializeField] private Transform ceiling;
    [SerializeField] private Transform door;
    [SerializeField] private DragableItem dragItem;

    [Header("Level 075 mechanism")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private SpriteRenderer targetFrame;
    [Tooltip("场景中的唯一跳跃按钮。起始位置会从专用 GamePanel 的 jump_btn 槽位换算。")]
    [SerializeField] private Transform worldUpButton;
    [Tooltip("场景跳跃按钮的本关输入组件；负责短按跳跃、拖拽以及安装后的机关点击。")]
    [SerializeField] private Level75WorldButtonClick worldUpButtonClick;
    [SerializeField] private Sprite crushedDoorSprite;
    [SerializeField] private ParticleSystem doorBrokenVfxPrefab;
    [SerializeField, Min(0f)] private float targetTolerance = 0.5f;

    [Header("Init Button Scale")]
    [Tooltip("场景跳跃按钮缩放入场的持续时间，与 Level034 的按钮入场节奏保持一致。")]
    [SerializeField, Min(0f)] private float initButtonScaleDuration = 0.3f;
    [Tooltip("场景跳跃按钮在顺序入场中的相邻按钮延迟步长；本按钮固定使用第三顺位。")]
    [SerializeField, Min(0f)] private float initButtonDelayStep = 0.06f;
    [Tooltip("场景跳跃按钮的基础延迟；包含对齐 GamePanel 入场起点的 0.5 秒和 Level034 的 0.3 秒偏移。")]
    [SerializeField, Min(0f)] private float initButtonDelayOffset = 0.8f;

    [Header("Door crush feedback")]
    [Tooltip("门受压时缩放的可视子节点；只变形画面，不缩放门根碰撞体和碎门落点。")]
    [SerializeField] private Transform doorVisual;
    [Tooltip("门受压第一阶段的局部缩放倍率。")]
    [SerializeField] private Vector3 doorSqueezeBuildScaleMultiplier = new Vector3(1.12f, 0.82f, 1f);
    [Tooltip("门达到破碎临界点时的局部缩放倍率。")]
    [SerializeField] private Vector3 doorSqueezeBreakScaleMultiplier = new Vector3(1.25f, 0.55f, 1f);
    [Tooltip("门受压第一阶段持续时间。")]
    [SerializeField, Min(0f)] private float doorSqueezeBuildDuration = 0.26f;
    [Tooltip("门从明显变形到破碎临界点的持续时间。")]
    [SerializeField, Min(0f)] private float doorSqueezeBreakDuration = 0.36f;
    [Tooltip("碎门生成位置。节点跟随可拖门移动，策划可直接在 Prefab 中调整。")]
    [SerializeField] private Transform crushedDoorSpawnPoint;
    [SerializeField] private float crushedDoorGroundY = -2.365f;
    [SerializeField, Min(0.01f)] private float cameraShakeDuration = 0.25f;
    [SerializeField, Min(0f)] private float cameraShakeStrength = 0.18f;
    [SerializeField, Min(1)] private int cameraShakeVibrato = 20;

    [Header("Timing")]
    [SerializeField] private float ceilingDownY = -3.4f;
    [SerializeField, Min(0f)] private float ceilingDownDuration = 2.5f;
    [SerializeField, Min(0f)] private float ceilingStartDelay = 2f;
    [SerializeField, Min(0f)] private float doorDragDelay = 1.5f;
    [SerializeField] private float ceilingRaisedY = 7.8f;
    [SerializeField, Min(0f)] private float ceilingRaiseDuration = 3f;

    private bool upButtonInstalled;
    private bool mechanismActivated;
    private bool doorCrushed;
    private bool cleanedUp;
    private Transform cameraShakeTarget;
    private Vector3 cameraInitialLocalPosition;
    private Tween cameraShakeTween;
    private Tween worldUpButtonIntroTween;
    private Sequence doorCrushSequence;
    private Vector3 worldUpButtonInitialScale = Vector3.one;

    /// <summary>
    /// 场景跳跃按钮是否已经吸附到红框；吸附后按钮只负责机关，不再触发角色跳跃。
    /// </summary>
    public bool IsWorldUpButtonInstalled => upButtonInstalled;

    private void Awake()
    {
        if (worldUpButton == null)
            return;

        worldUpButtonInitialScale = worldUpButton.localScale;
        worldUpButton.localScale = Vector3.zero;
    }

    private void Start()
    {
        ResolveCameraShakeTarget();
        PrepareWorldUpButton();

        if (ceiling != null)
        {
            ceiling.DOMoveY(ceilingDownY, ceilingDownDuration)
                .SetDelay(ceilingStartDelay)
                .SetUpdate(true)
                .SetEase(Ease.Linear);
        }

        Invoke(nameof(OpenDragDoor), doorDragDelay);
        StartCoroutine(InitializeWorldUpButton());
    }

    private void PrepareWorldUpButton()
    {
        upButtonInstalled = false;
        worldUpButtonClick?.SetEnableDrag(false);
        worldUpButtonClick?.SetEnableClick(false);
        if (worldUpButtonClick != null)
            worldUpButtonClick.enabled = false;

        if (worldUpButton != null)
        {
            worldUpButton.DOKill();
            worldUpButton.localScale = Vector3.zero;
            worldUpButton.gameObject.SetActive(false);
        }

        worldUpButtonIntroTween = null;
    }

    private IEnumerator InitializeWorldUpButton()
    {
        yield return new WaitForSecondsRealtime(0.5f);

        const int maxAttempts = 15;
        RectTransform jumpButtonSlot = null;
        for (int attempt = 0; attempt < maxAttempts && jumpButtonSlot == null; attempt++)
        {
            jumpButtonSlot = FindButtonSlot("jump_btn");
            if (jumpButtonSlot == null)
                yield return new WaitForSecondsRealtime(0.1f);
        }

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (jumpButtonSlot == null ||
            cameraToUse == null ||
            worldUpButton == null ||
            worldUpButtonClick == null)
        {
            Debug.LogWarning(
                "[Level75Ctrl] World jump button or GamePanel jump_btn slot was not found.",
                this);
            yield break;
        }

        RectTransform legacyUpButtonSlot = FindButtonSlot("up_btn");
        float buttonZ = targetFrame != null ? targetFrame.transform.position.z : 0f;
        worldUpButton.position = CommonUtility.ConvertUIToWorldPosition(
            jumpButtonSlot,
            cameraToUse,
            buttonZ);
        jumpButtonSlot.gameObject.SetActive(false);
        if (legacyUpButtonSlot != null)
            legacyUpButtonSlot.gameObject.SetActive(false);

        worldUpButton.DOKill();
        worldUpButton.localScale = Vector3.zero;
        worldUpButton.gameObject.SetActive(true);

        bool introCompleted = false;
        const int jumpButtonOrder = 2;
        float introDelay =
            Mathf.Max(0f, initButtonDelayOffset) +
            jumpButtonOrder * Mathf.Max(0f, initButtonDelayStep);
        worldUpButtonIntroTween = worldUpButton
            .DOScale(
                worldUpButtonInitialScale,
                Mathf.Max(0f, initButtonScaleDuration))
            .SetDelay(introDelay)
            .SetUpdate(true)
            .SetEase(Ease.OutBack)
            .SetLink(worldUpButton.gameObject)
            .OnComplete(() => introCompleted = true);

        while (!introCompleted &&
               worldUpButtonIntroTween != null &&
               worldUpButtonIntroTween.IsActive())
        {
            yield return null;
        }

        if (!introCompleted)
            yield break;

        worldUpButtonIntroTween = null;

        while (PlayCtrl.Instance == null ||
               !PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING))
        {
            yield return null;
        }

        worldUpButtonClick.enabled = true;

        // Give the newly enabled input component one frame to finish activation.
        yield return null;

        while (PlayCtrl.Instance == null ||
               !PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING))
        {
            yield return null;
        }

        worldUpButtonClick.SetEnableClick(true);
        worldUpButtonClick.SetEnableDrag(true);
    }

    private static RectTransform FindButtonSlot(string buttonName)
    {
        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
            return null;

        Transform panelTransform = PlayCtrl.Instance.CurrentGamePlayPanel.transform;
        Transform slot = panelTransform.Find($"Root/{buttonName}");
        if (slot == null && panelTransform.childCount > 0)
        {
            slot = panelTransform.GetChild(0).Find(buttonName);
        }

        return slot as RectTransform;
    }

    /// <summary>
    /// 供本关按钮输入组件在拖拽正常松手时调用；命中红框后吸附并永久锁定拖拽。
    /// </summary>
    public void TryInstallWorldUpButton()
    {
        if (PlayCtrl.Instance == null ||
            !PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING) ||
            upButtonInstalled ||
            targetFrame == null ||
            worldUpButton == null ||
            worldUpButtonClick == null)
        {
            return;
        }

        Bounds snapBounds = targetFrame.bounds;
        snapBounds.Expand(targetTolerance * 2f);
        Vector3 buttonPosition = worldUpButton.position;
        if (buttonPosition.x < snapBounds.min.x ||
            buttonPosition.x > snapBounds.max.x ||
            buttonPosition.y < snapBounds.min.y ||
            buttonPosition.y > snapBounds.max.y)
        {
            return;
        }

        upButtonInstalled = true;

        Vector3 snappedPosition = targetFrame.bounds.center;
        snappedPosition.z = buttonPosition.z;
        worldUpButton.SetParent(targetFrame.transform, true);
        worldUpButton.position = snappedPosition;
        worldUpButtonClick.SetEnableDrag(false);
        worldUpButtonClick.SetEnableClick(true);
    }

    /// <summary>
    /// 供已吸附的场景向上按钮点击事件调用。
    /// </summary>
    public void ClickWorldUpButton()
    {
        TryRaiseCeiling();
    }

    private void TryRaiseCeiling()
    {
        if (PlayCtrl.Instance == null ||
            !PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING) ||
            mechanismActivated ||
            !upButtonInstalled)
        {
            return;
        }

        mechanismActivated = true;

        if (ceiling != null)
        {
            ceiling.DOKill();
            ceiling.DOMoveY(ceilingRaisedY, ceilingRaiseDuration)
                .SetUpdate(true)
                .SetEase(Ease.OutCubic);
        }

        AudioManager.PlaySound("button");
    }

    private void OpenDragDoor()
    {
        if (!doorCrushed && dragItem != null)
            dragItem.enabled = true;
    }

    public void CrushDoor(Collider2D _)
    {
        if (doorCrushed || door == null)
            return;

        doorCrushed = true;

        BoxCollider2D doorCollider = door.GetComponent<BoxCollider2D>();
        Vector3 effectPosition = doorCollider != null
            ? doorCollider.bounds.center
            : door.position;

        DisableDoorInteraction();
        PlayDoorSqueeze(effectPosition);
    }

    private void DisableDoorInteraction()
    {
        if (dragItem != null)
            dragItem.enabled = false;

        Collider2D[] doorColliders = door.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < doorColliders.Length; i++)
        {
            if (doorColliders[i] != null)
                doorColliders[i].enabled = false;
        }
    }

    private void PlayDoorSqueeze(Vector3 effectPosition)
    {
        Transform squeezeTarget = doorVisual != null ? doorVisual : door;
        float buildDuration = Mathf.Max(0f, doorSqueezeBuildDuration);
        float breakDuration = Mathf.Max(0f, doorSqueezeBreakDuration);
        if (squeezeTarget == null || buildDuration + breakDuration <= 0f)
        {
            CompleteDoorCrush(effectPosition);
            return;
        }

        squeezeTarget.DOKill();
        Vector3 initialScale = squeezeTarget.localScale;
        Vector3 buildScale = Vector3.Scale(
            initialScale,
            ClampDoorScaleMultiplier(doorSqueezeBuildScaleMultiplier));
        Vector3 breakScale = Vector3.Scale(
            initialScale,
            ClampDoorScaleMultiplier(doorSqueezeBreakScaleMultiplier));

        doorCrushSequence = DOTween.Sequence()
            .Append(squeezeTarget.DOScale(buildScale, buildDuration).SetEase(Ease.InQuad))
            .Append(squeezeTarget.DOScale(breakScale, breakDuration).SetEase(Ease.Linear))
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() => CompleteDoorCrush(effectPosition));
    }

    private static Vector3 ClampDoorScaleMultiplier(Vector3 multiplier)
    {
        return new Vector3(
            Mathf.Max(0.01f, multiplier.x),
            Mathf.Max(0.01f, multiplier.y),
            Mathf.Max(0.01f, multiplier.z));
    }

    private void CompleteDoorCrush(Vector3 effectPosition)
    {
        doorCrushSequence = null;
        if (cleanedUp || door == null)
            return;

        CreateCrushedDoor();
        PlayDoorBrokenVfx(effectPosition);
        door.gameObject.SetActive(false);
        PlayCameraShake();
        AudioManager.PlaySound("blockByDoor");
    }

    private void CreateCrushedDoor()
    {
        if (crushedDoorSprite == null)
            return;

        Vector3 position;
        Quaternion rotation;
        if (crushedDoorSpawnPoint != null)
        {
            position = crushedDoorSpawnPoint.position;
            rotation = crushedDoorSpawnPoint.rotation;
        }
        else
        {
            position = door.position;
            position.y = crushedDoorGroundY - crushedDoorSprite.bounds.min.y;
            rotation = Quaternion.identity;
        }

        GameObject crushedDoor = new GameObject("CrushedDoor_075");
        crushedDoor.transform.SetParent(transform, true);
        crushedDoor.transform.SetPositionAndRotation(position, rotation);

        SpriteRenderer renderer = crushedDoor.AddComponent<SpriteRenderer>();
        renderer.sprite = crushedDoorSprite;
        if (targetFrame != null)
        {
            renderer.sortingLayerID = targetFrame.sortingLayerID;
            renderer.sortingOrder = targetFrame.sortingOrder + 1;
        }
        else
        {
            renderer.sortingLayerName = "SceneLayer";
            renderer.sortingOrder = 201;
        }
    }

    private void PlayDoorBrokenVfx(Vector3 position)
    {
        if (doorBrokenVfxPrefab == null)
            return;

        ParticleSystem instance = Instantiate(
            doorBrokenVfxPrefab,
            position,
            doorBrokenVfxPrefab.transform.rotation,
            transform);

        instance.gameObject.SetActive(true);
        instance.Play(true);
    }

    private void PlayCameraShake()
    {
        ResolveCameraShakeTarget();
        if (cameraShakeTarget == null || cameraShakeStrength <= 0f)
            return;

        if (cameraShakeTween != null && cameraShakeTween.IsActive())
            cameraShakeTween.Kill();

        cameraShakeTarget.localPosition = cameraInitialLocalPosition;
        cameraShakeTween = cameraShakeTarget
            .DOShakePosition(
                Mathf.Max(0.01f, cameraShakeDuration),
                cameraShakeStrength,
                Mathf.Max(1, cameraShakeVibrato),
                90f,
                false,
                true)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(RestoreCameraPosition)
            .OnKill(RestoreCameraPosition);
    }

    private void ResolveCameraShakeTarget()
    {
        if (cameraShakeTarget != null)
            return;

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
            return;

        cameraShakeTarget = cameraToUse.transform;
        cameraInitialLocalPosition = cameraShakeTarget.localPosition;
    }

    private void RestoreCameraPosition()
    {
        if (cameraShakeTarget != null)
            cameraShakeTarget.localPosition = cameraInitialLocalPosition;

        cameraShakeTween = null;
    }

    private void Cleanup()
    {
        if (cleanedUp)
            return;

        cleanedUp = true;
        StopAllCoroutines();
        CancelInvoke(nameof(OpenDragDoor));
        worldUpButtonClick?.SetEnableDrag(false);
        worldUpButtonClick?.SetEnableClick(false);
        if (worldUpButtonClick != null)
            worldUpButtonClick.enabled = false;

        upButtonInstalled = false;

        if (worldUpButtonIntroTween != null &&
            worldUpButtonIntroTween.IsActive())
        {
            worldUpButtonIntroTween.Kill();
        }

        worldUpButtonIntroTween = null;
        if (worldUpButton != null)
        {
            worldUpButton.DOKill();
            worldUpButton.localScale = Vector3.zero;
            worldUpButton.gameObject.SetActive(false);
        }

        if (ceiling != null)
            ceiling.DOKill();

        if (doorCrushSequence != null && doorCrushSequence.IsActive())
            doorCrushSequence.Kill();

        doorCrushSequence = null;

        if (cameraShakeTween != null && cameraShakeTween.IsActive())
            cameraShakeTween.Kill();

        RestoreCameraPosition();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }
}
