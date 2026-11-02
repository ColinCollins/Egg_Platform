using System.Collections.Generic;
using Bear.EventSystem;
using Game.Events;
using Game.Play;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>内孔旋涡常驻；独立彩带只在物件吸入期间显示，原图和共享Spine资源只读。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class Level137HoleVortex : MonoBehaviour
{
    private const int RibbonDirectionCount = 2;
    // authored 洞口 Sprite；只用于确定父节点、排序层和局部坐标参考。
    [SerializeField] private SpriteRenderer hole;
    // 本关派生的内孔 SpriteMask；不得替换或修改原洞口素材。
    [SerializeField] private Sprite interiorMask;
    // 只读复用的门内 effects SkeletonDataAsset，用于常驻内孔旋涡。
    [SerializeField] private SkeletonDataAsset vortexData;
    // 与 VisibleInsideMask 配套的本关 Stencil 材质。
    [SerializeField] private Material insideMaskMaterial;
    // 只读复用的门吸入彩带 SkeletonDataAsset；实例仅显示 line_EF_ 槽位。
    [SerializeField] private SkeletonDataAsset ribbonData;
    [Tooltip("彩带适配内孔后再放大的线性倍率；2表示宽高各为原来的两倍，汇聚中心不变。")]
    // 彩带适配后的宽高倍率，不改变吸入计时和目标状态。
    [SerializeField, Min(0.1f)] private float ribbonRangeScale = 2f;
    [Tooltip("原洞图深色内孔像素范围的局部中心；遮罩与原图同尺寸、同pivot、100 PPU。")]
    // 内孔遮罩中心，使用 hole 局部坐标而不是吸入目标中心。
    [SerializeField] private Vector2 innerCenter = new Vector2(0.175f, -0.01f);
    // 内孔遮罩的局部尺寸；必须覆盖内孔而不扩大到洞口外壳。
    [SerializeField] private Vector2 innerSize = new Vector2(1.47f, 1.34f);
    // 常驻旋涡适配内孔时的填充超采样倍率。
    [SerializeField, Min(1f)] private float fillOverscan = 1f;

    private EventSubscriber subscriber;
    private GameObject visualRoot;
    private SkeletonAnimation vortexAnimation;
    private GameObject ribbonRoot;
    private readonly List<SkeletonAnimation> ribbonAnimations = new List<SkeletonAnimation>(RibbonDirectionCount);
    private bool started;
    private bool paused;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnPause);
        subscriber.Subscribe<GameResumeEvent>(OnResume);
        if (started) CreateVisual();
    }

    private void Start()
    {
        started = true;
        CreateVisual();
    }

    private void CreateVisual()
    {
        if (visualRoot != null || !Application.isPlaying) return;
        if (hole == null || hole.sprite == null || hole.drawMode != SpriteDrawMode.Simple ||
            interiorMask == null || vortexData == null || insideMaskMaterial == null ||
            innerSize.x <= 0f || innerSize.y <= 0f)
        {
            Debug.LogError("[Level137] Hole vortex bindings are missing; keeping the original hole artwork.", this);
            return;
        }
        try
        {
            visualRoot = new GameObject("Level137HoleVortexVisual");
            visualRoot.transform.SetParent(hole.transform, false);
            visualRoot.transform.localScale = new Vector3(hole.flipX ? -1f : 1f, hole.flipY ? -1f : 1f, 1f);
            // 单独SortingGroup隔离SpriteMask作用域，避免遮罩影响门、玩家或其它关卡物件。
            var group = visualRoot.AddComponent<SortingGroup>();
            group.sortingLayerID = hole.sortingLayerID;
            group.sortingOrder = hole.sortingOrder + 1;
            var maskObject = new GameObject("InteriorMask");
            maskObject.transform.SetParent(visualRoot.transform, false);
            var mask = maskObject.AddComponent<SpriteMask>();
            mask.sprite = interiorMask;
            mask.alphaCutoff = 0.5f;

            var effectObject = new GameObject("DoorInteriorEffect");
            effectObject.transform.SetParent(visualRoot.transform, false);
            vortexAnimation = SkeletonRenderer.AddSpineComponent<SkeletonAnimation>(effectObject, vortexData);
            if (vortexAnimation.Skeleton == null || vortexAnimation.AnimationState == null ||
                vortexAnimation.Skeleton.Data.FindAnimation("effects") == null)
                throw new System.InvalidOperationException("Door interior effects animation is unavailable.");
            // 复用door_0/effect而非door或ribbon。取消实例的门形裁切，改由洞口独立遮罩裁切。
            vortexAnimation.useClipping = false;
            vortexAnimation.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            vortexAnimation.maskMaterials.materialsInsideMask = new[] { insideMaskMaterial };
            vortexAnimation.updateWhenInvisible = UpdateMode.FullUpdate;
            vortexAnimation.UnscaledTime = false;
            vortexAnimation.AnimationState.SetAnimation(0, "effects", true).MixDuration = 0f;
            vortexAnimation.Update(0f);

            Bounds background = Level137Geometry.SkeletonLocalBounds(vortexAnimation, "EF_bg");
            if (background.size.x <= 0f || background.size.y <= 0f)
                throw new System.InvalidOperationException("Door interior EF_bg bounds are unavailable.");
            float scale = Mathf.Max(innerSize.x / background.size.x, innerSize.y / background.size.y) * fillOverscan;
            vortexAnimation.transform.localScale = Vector3.one * scale;
            vortexAnimation.transform.localPosition = (Vector3)innerCenter - background.center * scale;
            var renderer = effectObject.GetComponent<MeshRenderer>();
            renderer.sortingLayerID = hole.sortingLayerID;
            renderer.sortingOrder = 0;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            vortexAnimation.timeScale = paused ? 0f : 1f;
        }
        catch (System.Exception exception)
        {
            ReleaseVisual();
            Debug.LogError("[Level137] Hole vortex creation failed; original artwork retained. " + exception, this);
        }
    }

    /// <summary>
    /// 开始播放吸入彩带。彩带只表示当前存在实际吸入物件，不负责推进吸入状态。
    /// </summary>
    public void PlayAbsorptionRibbon()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (ribbonRoot != null && ribbonRoot.activeSelf) return;
        if (ribbonRoot == null && !CreateRibbon()) return;
        ribbonRoot.SetActive(true);
        foreach (SkeletonAnimation animated in ribbonAnimations)
        {
            if (animated == null) continue;
            animated.AnimationState.SetAnimation(0, "Inhalation_effects", true).MixDuration = 0f;
            animated.timeScale = paused ? 0f : 1f;
            animated.Update(0f);
        }
    }

    private bool CreateRibbon()
    {
        if (hole == null || ribbonData == null || innerSize.x <= 0f || innerSize.y <= 0f || ribbonRangeScale <= 0f)
        {
            Debug.LogError("[Level137] Hole ribbon bindings are missing; absorption gameplay continues without ribbons.", this);
            return false;
        }
        try
        {
            // 与遮罩子树同级，绝不放进visualRoot；彩带须伸出洞外，不受内孔SpriteMask裁切。
            ribbonRoot = new GameObject("Level137HoleAbsorptionRibbon");
            ribbonRoot.transform.SetParent(hole.transform, false);
            ribbonRoot.transform.localPosition = innerCenter;
            var group = ribbonRoot.AddComponent<SortingGroup>();
            group.sortingLayerID = hole.sortingLayerID;
            group.sortingOrder = hole.sortingOrder + 2;
            for (int i = 0; i < RibbonDirectionCount; i++)
            {
                var directionObject = new GameObject("RibbonDirection_" + i);
                directionObject.transform.SetParent(ribbonRoot.transform, false);
                var animated = SkeletonRenderer.AddSpineComponent<SkeletonAnimation>(directionObject, ribbonData);
                ribbonAnimations.Add(animated);
                if (animated.Skeleton == null || animated.AnimationState == null ||
                    animated.Skeleton.Data.FindAnimation("Inhalation_effects") == null)
                    throw new System.InvalidOperationException("Door Inhalation_effects animation is unavailable.");
                animated.useClipping = false;
                animated.maskInteraction = SpriteMaskInteraction.None;
                animated.updateWhenInvisible = UpdateMode.FullUpdate;
                animated.UnscaledTime = false;
                animated.UpdateComplete += HideNonRibbonSlots;
                animated.AnimationState.SetAnimation(0, "Inhalation_effects", true).MixDuration = 0f;
                animated.Update(0f);

                Bounds opening = RibbonOpeningBounds(animated);
                float scale = Mathf.Min(innerSize.x / opening.size.x, innerSize.y / opening.size.y) * ribbonRangeScale;
                Quaternion rotation = Quaternion.Euler(0f, 0f, 360f * i / RibbonDirectionCount);
                directionObject.transform.localScale = Vector3.one * scale;
                directionObject.transform.localRotation = rotation;
                // 先把原门汇聚点移到每组的原点，再绕共同洞心旋转，避免各个方向各自偏心。
                directionObject.transform.localPosition = -(rotation * opening.center) * scale;
                var renderer = directionObject.GetComponent<MeshRenderer>();
                renderer.sortingLayerID = hole.sortingLayerID;
                renderer.sortingOrder = i;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            ribbonRoot.SetActive(false);
            return true;
        }
        catch (System.Exception exception)
        {
            ReleaseRibbon();
            Debug.LogError("[Level137] Hole ribbon creation failed; gameplay and interior vortex retained. " + exception, this);
            return false;
        }
    }

    private static Bounds RibbonOpeningBounds(SkeletonAnimation animated)
    {
        // 原门的EF裁切轮廓只读用于取得汇聚区，不用彩带整段外接框居中，也不拿它裁切彩带。
        Spine.Slot slot = animated.Skeleton.FindSlot("EF");
        if (slot == null || !(slot.Attachment is Spine.ClippingAttachment opening) || opening.WorldVerticesLength < 6)
            throw new System.InvalidOperationException("Door opening reference shape is unavailable.");
        var vertices = new float[opening.WorldVerticesLength];
        opening.ComputeWorldVertices(slot, 0, vertices.Length, vertices, 0, 2);
        var bounds = new Bounds(new Vector3(vertices[0], vertices[1], 0f), Vector3.zero);
        for (int i = 2; i < vertices.Length; i += 2)
            bounds.Encapsulate(new Vector3(vertices[i], vertices[i + 1], 0f));
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            throw new System.InvalidOperationException("Door opening reference bounds are invalid.");
        return bounds;
    }

    private static void HideNonRibbonSlots(ISkeletonAnimation animated)
    {
        // 仅隐藏实例的非彩带槽位，保留PathAttachment供共享路径约束计算；不修改共享Attachment数据。
        foreach (Spine.Slot slot in animated.Skeleton.Slots)
            if (!slot.Data.Name.StartsWith("line_EF_", System.StringComparison.Ordinal)) slot.A = 0f;
    }

    /// <summary>
    /// 隐藏吸入彩带但保留常驻内孔旋涡；用于吸入完成、重启退场和关卡卸载。
    /// </summary>
    public void StopAbsorptionRibbon()
    {
        if (ribbonRoot != null && ribbonRoot.activeSelf) ribbonRoot.SetActive(false);
    }

    private void ReleaseRibbon()
    {
        foreach (SkeletonAnimation animated in ribbonAnimations)
            if (animated != null) animated.UpdateComplete -= HideNonRibbonSlots;
        StopAbsorptionRibbon();
        if (ribbonRoot != null) Destroy(ribbonRoot);
        ribbonRoot = null;
        ribbonAnimations.Clear();
    }

    private void OnPause(GamePauseEvent _)
    {
        paused = true;
        if (vortexAnimation != null) vortexAnimation.timeScale = 0f;
        foreach (SkeletonAnimation animated in ribbonAnimations)
            if (animated != null) animated.timeScale = 0f;
    }

    private void OnResume(GameResumeEvent _)
    {
        paused = false;
        if (vortexAnimation != null) vortexAnimation.timeScale = 1f;
        foreach (SkeletonAnimation animated in ribbonAnimations)
            if (animated != null) animated.timeScale = 1f;
    }

    private void ReleaseVisual()
    {
        vortexAnimation = null;
        if (visualRoot == null) return;
        visualRoot.SetActive(false);
        Destroy(visualRoot);
        visualRoot = null;
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref subscriber);
        ReleaseRibbon();
        ReleaseVisual();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref subscriber);
        ReleaseRibbon();
        ReleaseVisual();
    }
}
