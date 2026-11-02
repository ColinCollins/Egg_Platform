using System;
using System.Collections.Generic;
using System.Threading;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Level;
using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Level 119 独属弹窗通关入口。弹窗布局保持原样；脚本把真实 Actor 投影到屏幕，
/// 与现有门 Rect 重叠后播放开门和进门表现，再交给 BaseLevelCtrl 完成标准成功结算。
/// </summary>
public sealed class Level119FakeTipsPopup : BaseUIView
{
    private const string DoorRootName = "L119_TipsDoor";
    private const string DoorBodyName = "L119_TipsDoorBodyPreview";
    private const string DoorEffectName = "L119_TipsDoorEffectPreview";
    private const string PaperLeftName = "UI_l106_tips_img_bg_01";
    private const string PaperRightName = "UI_l106_tips_img_bg_02";
    private const string ActorPreviewName = "L119_TipsBox_jump_up";
    private const string RightUfoRootName = "L119_TipsUFORight";
    private const string ClawUpperName = "UFO_ClawUpper";
    private const string ClawLowerName = "UFO_ClawLower";
    private const string VisualMaskTopName = "L119_VisualMask_Top";
    private const string VisualMaskBottomName = "L119_VisualMask_Bottom";
    private const string VisualMaskLeftName = "L119_VisualMask_Left";
    private const string VisualMaskRightName = "L119_VisualMask_Right";
    private const int PaperSortingOffset = -4;
    private const int ActorPreviewSortingOffset = -1;
    private const int ClawUpperSortingOffset = -3;
    private const int DoorSortingOffset = -2;
    private const int ClawLowerSortingOffset = -1;
    private const string DoorIdleAnimation = "door_idle";
    private const string DoorOpenAnimation = "door_open";
    private const string DoorEffectAnimation = "effects";
    private const float DoorOpenDelay = 0.4f;
    private const float ActorEnterDuration = 1.2f;
    private static readonly Vector3 ActorEnterDoorOffset = new Vector3(0f, 1f, 0f);

    private CustomButton closeButton;
    private RectTransform doorRect;
    private SkeletonGraphic doorBody;
    private SkeletonGraphic doorEffect;
    private Canvas popupCanvas;
    private UIScaleAnimation scaleAnimation;
    private RectTransform visualMaskTop;
    private RectTransform visualMaskBottom;
    private RectTransform visualMaskLeft;
    private RectTransform visualMaskRight;
    private Vector4 visualMaskBaseBounds;
    private Vector2 visualMaskBaseContentScale = Vector2.one;
    private bool visualMaskLayoutConfigured;
    private Level119Ctrl owner;
    private BaseLevelCtrl levelCtrl;
    private ActorCtrl actor;
    private Collider2D actorCollider;
    private Rigidbody2D actorBody;
    private Camera gameplayCamera;
    private Level119CameraFollowArea cameraFollowArea;
    private SortingGroup actorSortingGroup;
    private SpriteRenderer shadow;
    private SpriteRendererGroupAlpha actorGroupAlpha;
    private Image paperLeftImage;
    private Image paperRightImage;
    private Image actorPreviewImage;
    private Image rightClawUpperImage;
    private Image rightClawLowerImage;
    private GameObject sourceVisualRoot;
    private RectTransform doorLayoutAnchor;
    private float sourceVisualWorldZ;
    private readonly List<SourceVisualBinding> sourceVisualBindings = new List<SourceVisualBinding>();
    private bool sourceVisualsConfigured;
    private Sequence enterSequence;
    private CancellationTokenSource flowCancellation;

    private Transform actorInitialParent;
    private Vector3 actorInitialPosition;
    private Vector3 actorInitialLocalScale;
    private Quaternion actorInitialLocalRotation;
    private RigidbodyType2D actorInitialBodyType;
    private float actorInitialGravityScale;
    private bool actorInitialSimulated;
    private Vector2 actorInitialVelocity;
    private float actorInitialAngularVelocity;
    private bool actorInitialShadowActive;
    private bool actorBaselineCaptured;
    private bool configured;
    private bool suspended;
    private bool doorCommitted;
    private bool successSubmitted;
    private bool ownerClosing;
    private bool userClosing;
    private bool ownerVisualHiddenNotified;
    private float doorBodyTimeScale = 1f;
    private float doorEffectTimeScale = 1f;

    private sealed class SourceVisualBinding
    {
        internal RectTransform VisualRect;
        internal RectTransform LayoutAnchor;
        internal Transform OriginalParent;
        internal int OriginalSiblingIndex;
        internal Vector2 OriginalAnchorMin;
        internal Vector2 OriginalAnchorMax;
        internal Vector3 OriginalAnchoredPosition;
        internal Vector2 OriginalSizeDelta;
        internal Vector2 OriginalPivot;
        internal Quaternion OriginalLocalRotation;
        internal Vector3 OriginalLocalScale;
        internal List<SourceLayerState> OriginalLayers;
        internal Canvas Canvas;
        internal int SortingOffset;
        internal bool AddedAtRuntime;
        internal bool InitialEnabled;
        internal RenderMode InitialRenderMode;
        internal Camera InitialWorldCamera;
        internal bool InitialOverrideSorting;
        internal int InitialSortingLayerId;
        internal int InitialSortingOrder;
    }

    private sealed class SourceLayerState
    {
        internal GameObject GameObject;
        internal int Layer;
    }

    public override void OnCreate()
    {
        base.OnCreate();
        ResolvePopupReferences();
        BindCloseButton();
        ConfigureRaycastPassthrough();
        ResetDoorPresentation();
    }

    public override void OnShow()
    {
        base.OnShow();
        ownerVisualHiddenNotified = false;
        RefreshVisualMaskLayout();
        if (sourceVisualRoot != null)
        {
            sourceVisualRoot.SetActive(true);
        }

        RefreshSourceVisuals();
    }

    public override void OnHide()
    {
        base.OnHide();
    }

    private void OnDisable()
    {
        if (sourceVisualRoot != null)
        {
            sourceVisualRoot.SetActive(false);
        }

        NotifyOwnerVisualHidden();
    }

    private void Update()
    {
        if (!configured
            || suspended
            || doorCommitted
            || userClosing
            || owner == null
            || actor == null
            || actor.IsDied
            || !IsActorOverlappingDoor())
        {
            return;
        }

        if (!owner.TryCommitPopupDoor(this))
        {
            return;
        }

        doorCommitted = true;
        if (closeButton != null)
        {
            closeButton.IsBlockClick = true;
            closeButton.Interactable = false;
        }

        PlayDoorEntryAsync().Forget();
    }

    private void LateUpdate()
    {
        RefreshVisualMaskLayout();
        RefreshSourceVisuals();
    }

    internal bool Configure(
        Level119Ctrl newOwner,
        BaseLevelCtrl newLevelCtrl,
        ActorCtrl realActor,
        Camera realGameplayCamera)
    {
        owner = newOwner;
        ownerVisualHiddenNotified = false;
        levelCtrl = newLevelCtrl;
        actor = realActor;
        gameplayCamera = realGameplayCamera != null ? realGameplayCamera : Camera.main;
        actorCollider = ResolveActorBodyCollider(actor);
        actorBody = actor != null ? actor.GetComponent<Rigidbody2D>() : null;
        actorSortingGroup = actor != null ? actor.GetComponent<SortingGroup>() : null;
        shadow = actor != null ? actor.transform.Find("Shadow")?.GetComponent<SpriteRenderer>() : null;
        actorGroupAlpha = actor != null
            ? actor.GetComponentInChildren<SpriteRendererGroupAlpha>(true)
            : null;
        cameraFollowArea = newOwner != null
            ? newOwner.GetComponent<Level119CameraFollowArea>()
            : null;
        configured = TryConfigureSourceVisuals(out string error);
        if (!configured)
        {
            Debug.LogError($"[Level119FakeTipsPopup] Configuration failed: {error}", this);
            RestoreSourceVisualsToPopup();
            return false;
        }

        ResetDoorPresentation();
        return true;
    }

    internal void SetSuspended(bool isSuspended)
    {
        if (suspended == isSuspended)
        {
            return;
        }

        suspended = isSuspended;
        if (suspended)
        {
            if (enterSequence != null && enterSequence.IsActive() && enterSequence.IsPlaying())
            {
                enterSequence.Pause();
            }

            if (doorEffect != null
                && doorEffect.gameObject.activeSelf
                && doorEffect.AnimationState != null)
            {
                doorEffectTimeScale = doorEffect.AnimationState.TimeScale;
                doorEffect.AnimationState.TimeScale = 0f;
            }

            if (doorBody != null && doorBody.AnimationState != null)
            {
                doorBodyTimeScale = doorBody.AnimationState.TimeScale;
                doorBody.AnimationState.TimeScale = 0f;
            }
        }
        else
        {
            if (enterSequence != null && enterSequence.IsActive() && !enterSequence.IsPlaying())
            {
                enterSequence.Play();
            }

            if (doorBody != null && doorBody.AnimationState != null)
            {
                doorBody.AnimationState.TimeScale = doorBodyTimeScale;
            }

            if (doorEffect != null
                && doorEffect.gameObject.activeSelf
                && doorEffect.AnimationState != null)
            {
                doorEffect.AnimationState.TimeScale = doorEffectTimeScale;
            }
        }
    }

    internal void CancelForFailure()
    {
        CancelDoorFlow(restoreActor: true);
    }

    internal void CloseFromOwner()
    {
        ownerClosing = true;
        CancelDoorFlow(restoreActor: !successSubmitted);
        if (sourceVisualRoot != null)
        {
            sourceVisualRoot.SetActive(false);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.DestroyUI(this);
        }
    }

    private void ResolvePopupReferences()
    {
        closeButton = GetComponentInChildren<CustomButton>(true);
        popupCanvas = GetComponentInParent<Canvas>();
        scaleAnimation = GetComponent<UIScaleAnimation>();

        Transform rightUfoRoot = FindDescendant(transform, RightUfoRootName);
        Transform doorRoot = rightUfoRoot != null
            ? FindDescendant(rightUfoRoot, DoorRootName)
            : FindDescendant(transform, DoorRootName);
        doorRect = doorRoot as RectTransform;
        doorBody = doorRoot != null
            ? FindDescendant(doorRoot, DoorBodyName)?.GetComponent<SkeletonGraphic>()
            : null;
        doorEffect = doorRoot != null
            ? FindDescendant(doorRoot, DoorEffectName)?.GetComponent<SkeletonGraphic>()
            : null;
        paperLeftImage = FindDescendant(transform, PaperLeftName)?.GetComponent<Image>();
        paperRightImage = FindDescendant(transform, PaperRightName)?.GetComponent<Image>();
        actorPreviewImage = FindDescendant(transform, ActorPreviewName)?.GetComponent<Image>();
        rightClawUpperImage = rightUfoRoot != null
            ? FindDescendant(rightUfoRoot, ClawUpperName)?.GetComponent<Image>()
            : null;
        rightClawLowerImage = rightUfoRoot != null
            ? FindDescendant(rightUfoRoot, ClawLowerName)?.GetComponent<Image>()
            : null;
        visualMaskTop = FindDescendant(transform, VisualMaskTopName) as RectTransform;
        visualMaskBottom = FindDescendant(transform, VisualMaskBottomName) as RectTransform;
        visualMaskLeft = FindDescendant(transform, VisualMaskLeftName) as RectTransform;
        visualMaskRight = FindDescendant(transform, VisualMaskRightName) as RectTransform;
        CaptureVisualMaskLayout();
    }

    private void CaptureVisualMaskLayout()
    {
        RectTransform contentRoot = scaleAnimation != null
            ? scaleAnimation.TargetRectTransform
            : null;
        visualMaskLayoutConfigured = contentRoot != null
            && visualMaskTop != null
            && visualMaskBottom != null
            && visualMaskLeft != null
            && visualMaskRight != null;
        if (!visualMaskLayoutConfigured)
        {
            return;
        }

        visualMaskBaseBounds = new Vector4(
            visualMaskLeft.sizeDelta.x,
            visualMaskBottom.sizeDelta.y,
            -visualMaskRight.sizeDelta.x,
            -visualMaskTop.sizeDelta.y);
        if (visualMaskBaseBounds.x >= visualMaskBaseBounds.z
            || visualMaskBaseBounds.y >= visualMaskBaseBounds.w)
        {
            visualMaskLayoutConfigured = false;
            return;
        }

        Vector3 contentScale = contentRoot.localScale;
        visualMaskBaseContentScale = new Vector2(
            Mathf.Max(0.0001f, Mathf.Abs(contentScale.x)),
            Mathf.Max(0.0001f, Mathf.Abs(contentScale.y)));
        RefreshVisualMaskLayout();
    }

    private void RefreshVisualMaskLayout()
    {
        if (!visualMaskLayoutConfigured || scaleAnimation == null)
        {
            return;
        }

        RectTransform contentRoot = scaleAnimation.TargetRectTransform;
        if (contentRoot == null)
        {
            return;
        }

        Vector3 contentScale = contentRoot.localScale;
        float scaleX = Mathf.Abs(contentScale.x) / visualMaskBaseContentScale.x;
        float scaleY = Mathf.Abs(contentScale.y) / visualMaskBaseContentScale.y;
        float left = visualMaskBaseBounds.x * scaleX;
        float bottom = visualMaskBaseBounds.y * scaleY;
        float right = visualMaskBaseBounds.z * scaleX;
        float top = visualMaskBaseBounds.w * scaleY;
        float centerY = (bottom + top) * 0.5f;
        float height = top - bottom;

        visualMaskTop.anchoredPosition = new Vector2(0f, top * 0.5f);
        visualMaskTop.sizeDelta = new Vector2(0f, -top);
        visualMaskBottom.anchoredPosition = new Vector2(0f, bottom * 0.5f);
        visualMaskBottom.sizeDelta = new Vector2(0f, bottom);
        visualMaskLeft.anchoredPosition = new Vector2(left * 0.5f, centerY);
        visualMaskLeft.sizeDelta = new Vector2(left, height);
        visualMaskRight.anchoredPosition = new Vector2(right * 0.5f, centerY);
        visualMaskRight.sizeDelta = new Vector2(-right, height);
    }

    private void BindCloseButton()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.OnClick -= OnCloseButtonClicked;
        closeButton.OnClick += OnCloseButtonClicked;
        closeButton.IsBlockClick = false;
        closeButton.Interactable = true;
    }

    private void OnCloseButtonClicked(CustomButton _)
    {
        if (doorCommitted || userClosing)
        {
            return;
        }

        userClosing = true;
        owner?.NotifyPopupClosed(this);
        UnbindCloseButton();
        CancelDoorFlow(restoreActor: true);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseUI(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void ConfigureRaycastPassthrough()
    {
        Graphic closeGraphic = closeButton != null
            ? closeButton.BGraphic ?? closeButton.GetComponent<Graphic>()
            : null;
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = graphics[i] == closeGraphic;
        }
    }

    private static Collider2D ResolveActorBodyCollider(ActorCtrl targetActor)
    {
        if (targetActor == null)
        {
            return null;
        }

        Collider2D rootCollider = targetActor.GetComponent<Collider2D>();
        if (IsUsableActorBodyCollider(rootCollider))
        {
            return rootCollider;
        }

        Collider2D[] candidates = targetActor.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (IsUsableActorBodyCollider(candidates[i]))
            {
                return candidates[i];
            }
        }

        return rootCollider;
    }

    private static bool IsUsableActorBodyCollider(Collider2D candidate)
    {
        return candidate != null
            && candidate.enabled
            && candidate.gameObject.activeInHierarchy
            && !candidate.isTrigger
            && LayerMask.LayerToName(candidate.gameObject.layer) == "Actor";
    }

    private bool TryConfigureSourceVisuals(out string error)
    {
        error = null;
        if (owner == null || levelCtrl == null)
        {
            error = "owner or BaseLevelCtrl is missing.";
            return false;
        }

        if (actor == null || actorCollider == null || actorBody == null || actorSortingGroup == null)
        {
            error = "Actor, its body collider, Rigidbody2D, or SortingGroup is missing.";
            return false;
        }

        if (gameplayCamera == null || popupCanvas == null)
        {
            error = "Gameplay camera or Popup Canvas is missing.";
            return false;
        }

        if (paperLeftImage == null || paperLeftImage.sprite == null
            || paperRightImage == null || paperRightImage.sprite == null
            || actorPreviewImage == null || actorPreviewImage.sprite == null
            || rightClawUpperImage == null || rightClawUpperImage.sprite == null
            || rightClawLowerImage == null || rightClawLowerImage.sprite == null
            || doorRect == null || doorBody == null || doorEffect == null)
        {
            error = "A required source paper, Actor preview, right claw, or door reference is missing.";
            return false;
        }

        if (sourceVisualBindings.Count == 0)
        {
            try
            {
                sourceVisualRoot = new GameObject("Level119FakeTipsPopupSourceVisuals");
                AddSourceVisualBinding(paperLeftImage.rectTransform, PaperSortingOffset);
                AddSourceVisualBinding(paperRightImage.rectTransform, PaperSortingOffset);
                AddSourceVisualBinding(actorPreviewImage.rectTransform, ActorPreviewSortingOffset);
                AddSourceVisualBinding(rightClawUpperImage.rectTransform, ClawUpperSortingOffset);
                AddSourceVisualBinding(doorRect, DoorSortingOffset, isDoor: true);
                AddSourceVisualBinding(rightClawLowerImage.rectTransform, ClawLowerSortingOffset);
            }
            catch (Exception exception)
            {
                error = $"Unable to move source UI into world rendering: {exception.Message}";
                return false;
            }
        }

        if (sourceVisualRoot == null || doorLayoutAnchor == null)
        {
            error = "The source visual root or door layout anchor is missing.";
            return false;
        }

        for (int i = 0; i < sourceVisualBindings.Count; i++)
        {
            SourceVisualBinding binding = sourceVisualBindings[i];
            if (binding?.Canvas == null
                || binding.VisualRect == null
                || binding.LayoutAnchor == null)
            {
                error = "A source UI world binding was destroyed unexpectedly.";
                return false;
            }
        }

        sourceVisualsConfigured = true;
        sourceVisualRoot.SetActive(true);
        RefreshSourceVisuals();
        return true;
    }

    private void AddSourceVisualBinding(
        RectTransform visualRect,
        int sortingOffset,
        bool isDoor = false)
    {
        Transform originalParent = visualRect.parent;
        if (originalParent == null || sourceVisualRoot == null)
        {
            throw new InvalidOperationException($"{visualRect.name} has no layout parent or source visual root.");
        }

        var binding = new SourceVisualBinding
        {
            VisualRect = visualRect,
            OriginalParent = originalParent,
            OriginalSiblingIndex = visualRect.GetSiblingIndex(),
            OriginalAnchorMin = visualRect.anchorMin,
            OriginalAnchorMax = visualRect.anchorMax,
            OriginalAnchoredPosition = visualRect.anchoredPosition3D,
            OriginalSizeDelta = visualRect.sizeDelta,
            OriginalPivot = visualRect.pivot,
            OriginalLocalRotation = visualRect.localRotation,
            OriginalLocalScale = visualRect.localScale,
            OriginalLayers = CaptureAndSetHierarchyLayer(visualRect, actor.gameObject.layer),
            SortingOffset = sortingOffset,
        };
        sourceVisualBindings.Add(binding);

        GameObject anchorObject = new GameObject($"{visualRect.name}_LayoutAnchor", typeof(RectTransform));
        RectTransform layoutAnchor = (RectTransform)anchorObject.transform;
        layoutAnchor.SetParent(originalParent, false);
        ApplyRectTransformState(
            layoutAnchor,
            binding.OriginalAnchorMin,
            binding.OriginalAnchorMax,
            binding.OriginalAnchoredPosition,
            binding.OriginalSizeDelta,
            binding.OriginalPivot,
            binding.OriginalLocalRotation,
            binding.OriginalLocalScale);
        layoutAnchor.SetSiblingIndex(binding.OriginalSiblingIndex);
        binding.LayoutAnchor = layoutAnchor;

        visualRect.SetParent(sourceVisualRoot.transform, false);
        visualRect.anchorMin = new Vector2(0.5f, 0.5f);
        visualRect.anchorMax = new Vector2(0.5f, 0.5f);
        visualRect.anchoredPosition3D = Vector3.zero;
        visualRect.localRotation = Quaternion.identity;
        visualRect.localScale = Vector3.one;

        Canvas canvas = visualRect.GetComponent<Canvas>();
        bool addedAtRuntime = canvas == null;
        if (addedAtRuntime)
        {
            canvas = visualRect.gameObject.AddComponent<Canvas>();
        }

        binding.Canvas = canvas;
        binding.AddedAtRuntime = addedAtRuntime;
        binding.InitialEnabled = canvas.enabled;
        binding.InitialRenderMode = canvas.renderMode;
        binding.InitialWorldCamera = canvas.worldCamera;
        binding.InitialOverrideSorting = canvas.overrideSorting;
        binding.InitialSortingLayerId = canvas.sortingLayerID;
        binding.InitialSortingOrder = canvas.sortingOrder;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = gameplayCamera;
        canvas.overrideSorting = true;

        if (isDoor)
        {
            doorLayoutAnchor = layoutAnchor;
        }
    }

    private static List<SourceLayerState> CaptureAndSetHierarchyLayer(
        Transform root,
        int targetLayer)
    {
        Transform[] hierarchy = root.GetComponentsInChildren<Transform>(true);
        var result = new List<SourceLayerState>(hierarchy.Length);
        for (int i = 0; i < hierarchy.Length; i++)
        {
            GameObject target = hierarchy[i].gameObject;
            result.Add(new SourceLayerState
            {
                GameObject = target,
                Layer = target.layer,
            });
            target.layer = targetLayer;
        }

        return result;
    }

    private static void RestoreHierarchyLayers(List<SourceLayerState> states)
    {
        if (states == null)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            SourceLayerState state = states[i];
            if (state?.GameObject != null)
            {
                state.GameObject.layer = state.Layer;
            }
        }
    }

    private static void ApplyRectTransformState(
        RectTransform target,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector3 anchoredPosition,
        Vector2 sizeDelta,
        Vector2 pivot,
        Quaternion localRotation,
        Vector3 localScale)
    {
        target.anchorMin = anchorMin;
        target.anchorMax = anchorMax;
        target.anchoredPosition3D = anchoredPosition;
        target.sizeDelta = sizeDelta;
        target.pivot = pivot;
        target.localRotation = localRotation;
        target.localScale = localScale;
    }

    private void RefreshSourceVisuals()
    {
        if (!sourceVisualsConfigured
            || sourceVisualRoot == null
            || !sourceVisualRoot.activeInHierarchy
            || actorSortingGroup == null
            || actor == null)
        {
            return;
        }

        sourceVisualWorldZ = actor.transform.position.z;
        int sortingLayerId = actorSortingGroup.sortingLayerID;
        int actorSortingOrder = actorSortingGroup.sortingOrder;
        for (int i = 0; i < sourceVisualBindings.Count; i++)
        {
            SourceVisualBinding binding = sourceVisualBindings[i];
            if (binding?.Canvas == null
                || binding.VisualRect == null
                || binding.LayoutAnchor == null)
            {
                continue;
            }

            binding.Canvas.enabled = binding.LayoutAnchor.gameObject.activeInHierarchy;
            binding.Canvas.renderMode = RenderMode.WorldSpace;
            binding.Canvas.worldCamera = gameplayCamera;
            binding.Canvas.overrideSorting = true;
            binding.Canvas.sortingLayerID = sortingLayerId;
            binding.Canvas.sortingOrder = actorSortingOrder + binding.SortingOffset;

            if (!binding.Canvas.enabled
                || !TryProjectLayoutRectToWorld(
                    binding.LayoutAnchor,
                    out Vector3 center,
                    out Vector2 size,
                    out float rotationZ))
            {
                binding.Canvas.enabled = false;
                continue;
            }

            Rect rect = binding.VisualRect.rect;
            if (Mathf.Abs(rect.width) <= 0.0001f || Mathf.Abs(rect.height) <= 0.0001f)
            {
                binding.Canvas.enabled = false;
                continue;
            }

            binding.VisualRect.position = center;
            binding.VisualRect.rotation = Quaternion.Euler(0f, 0f, rotationZ);
            binding.VisualRect.localScale = new Vector3(
                size.x / Mathf.Abs(rect.width),
                size.y / Mathf.Abs(rect.height),
                1f);
        }
    }

    private bool TryProjectLayoutRectToWorld(
        RectTransform layoutRect,
        out Vector3 center,
        out Vector2 size,
        out float rotationZ)
    {
        center = default;
        size = default;
        rotationZ = 0f;
        if (layoutRect == null || gameplayCamera == null)
        {
            return false;
        }

        Vector3[] uiCorners = new Vector3[4];
        layoutRect.GetWorldCorners(uiCorners);
        Vector3[] worldCorners = new Vector3[4];
        Camera eventCamera = ResolvePopupEventCamera();
        for (int i = 0; i < uiCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, uiCorners[i]);
            if (!TryScreenPointToSourceWorld(screenPoint, out worldCorners[i]))
            {
                return false;
            }
        }

        Vector3 horizontal = worldCorners[3] - worldCorners[0];
        Vector3 vertical = worldCorners[1] - worldCorners[0];
        size = new Vector2(horizontal.magnitude, vertical.magnitude);
        if (size.x <= 0.0001f || size.y <= 0.0001f)
        {
            return false;
        }

        center = (worldCorners[0] + worldCorners[1] + worldCorners[2] + worldCorners[3]) * 0.25f;
        rotationZ = Mathf.Atan2(horizontal.y, horizontal.x) * Mathf.Rad2Deg;
        return IsFinite(center);
    }

    private Camera ResolvePopupEventCamera()
    {
        if (popupCanvas == null)
        {
            return null;
        }

        Canvas rootCanvas = popupCanvas.rootCanvas != null ? popupCanvas.rootCanvas : popupCanvas;
        if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return rootCanvas.worldCamera != null ? rootCanvas.worldCamera : gameplayCamera;
    }

    private bool TryScreenPointToSourceWorld(Vector2 screenPoint, out Vector3 worldPoint)
    {
        worldPoint = default;
        if (gameplayCamera == null)
        {
            return false;
        }

        Ray screenRay = gameplayCamera.ScreenPointToRay(screenPoint);
        Plane targetPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, sourceVisualWorldZ));
        if (!targetPlane.Raycast(screenRay, out float distance) || distance <= 0f)
        {
            return false;
        }

        worldPoint = screenRay.GetPoint(distance);
        worldPoint.z = sourceVisualWorldZ;
        return IsFinite(worldPoint);
    }

    private void RestoreSourceVisualsToPopup()
    {
        sourceVisualsConfigured = false;
        if (sourceVisualRoot != null)
        {
            sourceVisualRoot.SetActive(false);
        }

        for (int i = sourceVisualBindings.Count - 1; i >= 0; i--)
        {
            SourceVisualBinding binding = sourceVisualBindings[i];
            if (binding == null)
            {
                continue;
            }

            if (binding.Canvas != null)
            {
                if (binding.AddedAtRuntime)
                {
                    binding.Canvas.enabled = false;
                    Destroy(binding.Canvas);
                }
                else
                {
                    binding.Canvas.enabled = binding.InitialEnabled;
                    binding.Canvas.renderMode = binding.InitialRenderMode;
                    binding.Canvas.worldCamera = binding.InitialWorldCamera;
                    binding.Canvas.overrideSorting = binding.InitialOverrideSorting;
                    binding.Canvas.sortingLayerID = binding.InitialSortingLayerId;
                    binding.Canvas.sortingOrder = binding.InitialSortingOrder;
                }
            }

            if (binding.VisualRect != null && binding.OriginalParent != null)
            {
                binding.VisualRect.SetParent(binding.OriginalParent, false);
                ApplyRectTransformState(
                    binding.VisualRect,
                    binding.OriginalAnchorMin,
                    binding.OriginalAnchorMax,
                    binding.OriginalAnchoredPosition,
                    binding.OriginalSizeDelta,
                    binding.OriginalPivot,
                    binding.OriginalLocalRotation,
                    binding.OriginalLocalScale);
                binding.VisualRect.SetSiblingIndex(binding.OriginalSiblingIndex);
            }

            RestoreHierarchyLayers(binding.OriginalLayers);

            if (binding.LayoutAnchor != null)
            {
                Destroy(binding.LayoutAnchor.gameObject);
            }
        }

        sourceVisualBindings.Clear();
        doorLayoutAnchor = null;
        if (sourceVisualRoot != null)
        {
            Destroy(sourceVisualRoot);
            sourceVisualRoot = null;
        }
    }

    private bool IsActorOverlappingDoor()
    {
        return TryGetActorScreenRect(out Rect actorScreenRect)
            && TryGetDoorScreenRect(out Rect doorScreenRect)
            && actorScreenRect.Overlaps(doorScreenRect, true);
    }

    private bool TryGetActorScreenRect(out Rect result)
    {
        result = default;
        if (actorCollider == null || gameplayCamera == null)
        {
            return false;
        }

        Bounds bounds = actorCollider.bounds;
        Vector3[] corners =
        {
            new Vector3(bounds.min.x, bounds.min.y, bounds.center.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.center.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.center.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.center.z),
        };
        return TryBuildScreenRect(corners, gameplayCamera, out result);
    }

    private bool TryGetDoorScreenRect(out Rect result)
    {
        result = default;
        RectTransform layoutRect = doorLayoutAnchor != null ? doorLayoutAnchor : doorRect;
        if (layoutRect == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        layoutRect.GetWorldCorners(corners);
        Camera eventCamera = ResolvePopupEventCamera();

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
            min = Vector2.Min(min, screenPoint);
            max = Vector2.Max(max, screenPoint);
        }

        if (!IsFinite(min) || !IsFinite(max))
        {
            return false;
        }

        result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return result.width > 0f && result.height > 0f;
    }

    private static bool TryBuildScreenRect(Vector3[] worldCorners, Camera camera, out Rect result)
    {
        result = default;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 screenPoint = camera.WorldToScreenPoint(worldCorners[i]);
            if (screenPoint.z < 0f)
            {
                return false;
            }

            min = Vector2.Min(min, screenPoint);
            max = Vector2.Max(max, screenPoint);
        }

        if (!IsFinite(min) || !IsFinite(max))
        {
            return false;
        }

        result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return result.width > 0f && result.height > 0f;
    }

    private async UniTaskVoid PlayDoorEntryAsync()
    {
        CaptureActorBaseline();
        PrepareActorForDoorEntry();
        PlayDoorOpen();

        flowCancellation?.Dispose();
        flowCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        CancellationToken cancellationToken = flowCancellation.Token;

        try
        {
            await WaitForActiveSeconds(DoorOpenDelay, cancellationToken);
            if (!CanContinueDoorFlow())
            {
                return;
            }

            AudioManager.PlaySound("enterDoor");
            await PlayActorEnterTweenAsync();
            if (!CanContinueDoorFlow())
            {
                return;
            }

            successSubmitted = true;
            owner?.CompletePopupDoorSuccess(this);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CaptureActorBaseline()
    {
        if (actorBaselineCaptured || actor == null)
        {
            return;
        }

        actorBaselineCaptured = true;
        actorInitialParent = actor.transform.parent;
        actorInitialPosition = actor.transform.position;
        actorInitialLocalScale = actor.transform.localScale;
        actorInitialLocalRotation = actor.transform.localRotation;
        actorInitialShadowActive = shadow != null && shadow.gameObject.activeSelf;
        actorGroupAlpha?.CollectRenderers();

        if (actorBody != null)
        {
            actorInitialBodyType = actorBody.bodyType;
            actorInitialGravityScale = actorBody.gravityScale;
            actorInitialSimulated = actorBody.simulated;
            actorInitialVelocity = actorBody.linearVelocity;
            actorInitialAngularVelocity = actorBody.angularVelocity;
        }
    }

    private void PrepareActorForDoorEntry()
    {
        actor.StopMoving();
        actor.SetMoveInput(0f);
        actor.UpdateAnimation();
        cameraFollowArea?.SetCanFollow(false);

        if (actorBody != null)
        {
            actorBody.linearVelocity = Vector2.zero;
            actorBody.angularVelocity = 0f;
            actorBody.simulated = false;
        }
    }

    private void PlayDoorOpen()
    {
        AudioManager.PlaySound("openDoor");
        PlayDoorAnimation(DoorOpenAnimation, false);

        if (doorEffect != null)
        {
            doorEffect.gameObject.SetActive(true);
            if (doorEffect.AnimationState != null)
            {
                doorEffect.AnimationState.SetAnimation(0, DoorEffectAnimation, true);
            }
        }
    }

    // Match ActorDestroyAnimHandle: move to the adapted door target, shrink, rotate 720 degrees and fade out together.
    private async UniTask PlayActorEnterTweenAsync()
    {
        Vector3 targetPosition = ResolveDoorTargetWorldPosition() + ActorEnterDoorOffset;
        Transform actorTransform = actor.transform;
        float alpha = 1f;
        Vector3 targetAngle = actorTransform.localEulerAngles;
        targetAngle.z = targetAngle.y != 0f ? -720f : 720f;

        enterSequence?.Kill(false);
        enterSequence = DOTween.Sequence();
        enterSequence.Append(actorTransform.DOMove(targetPosition, ActorEnterDuration));
        enterSequence.Join(actorTransform.DOScale(Vector3.zero, ActorEnterDuration));
        enterSequence.Join(actorTransform.DORotate(targetAngle, ActorEnterDuration, RotateMode.FastBeyond360));
        enterSequence.Join(DOTween.To(
            () => alpha,
            value =>
            {
                alpha = value;
                SetActorAlpha(value);
            },
            0f,
            ActorEnterDuration));

        if (suspended)
        {
            enterSequence.Pause();
        }

        await enterSequence.AsyncWaitForCompletion();
    }

    private Vector3 ResolveDoorTargetWorldPosition()
    {
        if (!TryGetDoorScreenRect(out Rect doorScreenRect) || gameplayCamera == null)
        {
            return actor.transform.position;
        }

        Vector3 screenPoint = doorScreenRect.center;
        screenPoint.z = Mathf.Abs(gameplayCamera.transform.position.z - actor.transform.position.z);
        Vector3 worldPoint = gameplayCamera.ScreenToWorldPoint(screenPoint);
        worldPoint.z = actor.transform.position.z;
        return worldPoint;
    }

    private async UniTask WaitForActiveSeconds(float seconds, CancellationToken cancellationToken)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, seconds);
        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            if (!suspended)
            {
                elapsed += Mathf.Max(0f, Time.deltaTime);
            }
        }
    }

    private bool CanContinueDoorFlow()
    {
        return doorCommitted
            && !userClosing
            && owner != null
            && actor != null
            && !actor.IsDied
            && isActiveAndEnabled;
    }

    private void ResetDoorPresentation()
    {
        PlayDoorAnimation(DoorIdleAnimation, true);
        if (doorEffect != null)
        {
            if (doorEffect.AnimationState != null)
            {
                doorEffect.AnimationState.ClearTracks();
            }

            doorEffect.gameObject.SetActive(false);
        }
    }

    private void PlayDoorAnimation(string animationName, bool loop)
    {
        if (doorBody == null || doorBody.AnimationState == null)
        {
            return;
        }

        doorBody.AnimationState.SetAnimation(0, animationName, loop);
    }

    private void CancelDoorFlow(bool restoreActor)
    {
        flowCancellation?.Cancel();
        flowCancellation?.Dispose();
        flowCancellation = null;
        enterSequence?.Kill(false);
        enterSequence = null;

        if (restoreActor && !successSubmitted)
        {
            RestoreActorBaseline();
        }

        ResetDoorPresentation();
    }

    private void RestoreActorBaseline()
    {
        if (!actorBaselineCaptured || actor == null)
        {
            return;
        }

        if (actorInitialParent != null)
        {
            actor.transform.SetParent(actorInitialParent, true);
        }

        actor.transform.position = actorInitialPosition;
        actor.transform.localScale = actorInitialLocalScale;
        actor.transform.localRotation = actorInitialLocalRotation;
        SetActorAlpha(1f);
        if (shadow != null)
        {
            shadow.gameObject.SetActive(actorInitialShadowActive);
        }

        if (actorBody != null)
        {
            actorBody.bodyType = actorInitialBodyType;
            actorBody.gravityScale = actorInitialGravityScale;
            actorBody.simulated = actorInitialSimulated;
            actorBody.linearVelocity = actorInitialVelocity;
            actorBody.angularVelocity = actorInitialAngularVelocity;
        }

        actor.ResumeMoving();
        cameraFollowArea?.SetCanFollow(true);
        actorBaselineCaptured = false;
    }

    private void SetActorAlpha(float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        actor?.SpineCtrl?.SetAlpha(clampedAlpha);
        if (actorGroupAlpha != null)
        {
            actorGroupAlpha.SetAlpha(clampedAlpha);
        }

        if (shadow != null)
        {
            shadow.gameObject.SetActive(clampedAlpha >= 0.5f);
        }
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindDescendant(root.GetChild(i), targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.z);
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.y);
    }

    private void UnbindCloseButton()
    {
        if (closeButton != null)
        {
            closeButton.OnClick -= OnCloseButtonClicked;
        }
    }

    private void NotifyOwnerVisualHidden()
    {
        if (ownerVisualHiddenNotified || owner == null)
        {
            return;
        }

        ownerVisualHiddenNotified = true;
        owner.NotifyPopupVisualHidden(this);
    }

    private void OnDestroy()
    {
        UnbindCloseButton();
        RestoreSourceVisualsToPopup();
        NotifyOwnerVisualHidden();
        if (!ownerClosing && !userClosing && !successSubmitted)
        {
            owner?.NotifyPopupClosed(this);
        }

        CancelDoorFlow(restoreActor: !successSubmitted);
    }

    public static Level119FakeTipsPopup Create()
    {
        if (UIManager.Instance == null)
        {
            return null;
        }

        return UIManager.Instance.OpenUI<Level119FakeTipsPopup>(
            nameof(Level119FakeTipsPopup),
            UILayer.Popup,
            isShowMask: false,
            isCloseByMask: false);
    }
}
