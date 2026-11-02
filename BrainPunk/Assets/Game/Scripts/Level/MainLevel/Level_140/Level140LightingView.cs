using Bear.UI;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>四席固定聚光幕罩与墙面开关，纯视觉；不持有角色、食物、计数或结算状态。</summary>
public sealed class Level140LightingView : MonoBehaviour
{
    private const int SceneLayer = 1752186269;
    private readonly Vector3[] vertices = new Vector3[4];
    private Camera viewCamera;
    private Mesh mesh;
    private Material material;
    private MeshRenderer overlay;
    private UIManager uiManager;
    private Shader overlayShader;
    private SpriteRenderer switchBase;
    private SpriteRenderer lever;
    private Quaternion leverOffRotation;
    private Vector3 leverOffLocalPos;
    private Vector3 leverOnLocalPos;
    private bool hasLeverOnPos;
    private Vector4 seatCenters;
    private float surfaceY;
    private float alpha, startAlpha, fadeTime, fadeDuration;
    private bool lightsOn, fading;
    private Vector3 lastBottomLeft, lastTopRight;
    private bool hasCameraBounds;
    private float darkness;
    private Vector3 leverOffScale, leverOnScale, leverStartPosition, leverStartScale;
    private Quaternion leverStartRotation;
    private float leverElapsed;
    private bool leverMoving;
    private enum BeamPhase { Off, Contact, FadeIn, FlickerDim, FlickerBright, Steady, FadeOut }
    private BeamPhase beamPhase;
    private float beamLevel, beamFrom, beamTarget, beamElapsed, beamDuration;
    private int flickersLeft;

    /// <summary>注入相机、四席位置、台面、Shader、手摆开关引用及本关遮罩浓度；由关卡初始化调用一次，所有引用须在首次 Toggle 前有效。</summary>
    public void Configure(Camera camera, Vector4 centers, float deckY, Shader shader,
        SpriteRenderer switchRenderer, SpriteRenderer switchLever, Vector3 switchOnLocalPos, float shadeAlpha)
    {
        viewCamera = camera;
        darkness = Mathf.Clamp01(shadeAlpha);
        seatCenters = centers;
        surfaceY = deckY;
        overlayShader = shader;
        switchBase = switchRenderer;
        lever = switchLever;
        // 借用已摆放的底座与拨杆，不创建、不重挂，也不改写它们的 Sprite。
        // OFF 取 Prefab 手摆姿态；ON 取用户摆好的翻转后位置，两态一起切换。
        leverOffRotation = lever.transform.localRotation;
        leverOffScale = lever.transform.localScale;
        leverOnScale = new Vector3(leverOffScale.x, -leverOffScale.y, leverOffScale.z);
        leverOffLocalPos = lever.transform.localPosition;
        leverOnLocalPos = switchOnLocalPos;
        hasLeverOnPos = true;
    }

    private void EnsureOverlay()
    {
        if (overlay != null) return;
        // 和共享 UI 遮罩一样首次显示才创建，关闭后停用复用；不重建手摆开关。
        var overlayObject = new GameObject("Level140_LightCurtain");
        overlayObject.transform.SetParent(transform, false);
        mesh = new Mesh { name = "Level140_LightCurtain" };
        mesh.MarkDynamic();
        var filter = overlayObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        overlay = overlayObject.AddComponent<MeshRenderer>();
        overlay.sortingLayerID = SceneLayer;
        // 场景最高常规食物205 < 幕罩250 < 共享对话框300，按钮仍在幕罩上方。
        overlay.sortingOrder = 250;
        overlay.shadowCastingMode = ShadowCastingMode.Off;
        overlay.receiveShadows = false;
        material = new Material(overlayShader) { name = "Level140_LightCurtain" };
        material.SetVector("_SeatCenters", seatCenters);
        material.SetFloat("_BeamBottomY", surfaceY);
        material.SetFloat("_Glow", 0.08f);
        material.SetFloat("_TopHalfWidth", 0.28f);
        material.SetFloat("_BottomHalfWidth", 1f);
        material.SetFloat("_Opacity", 0f);
        material.SetFloat("_BeamStrength", 0f);
        overlay.sharedMaterial = material;
        overlayObject.SetActive(false);

        UpdateCameraBounds();
    }

    /// <summary>切换四席幕罩和拨杆姿态；仅响应本关确认后的有效点击，不改变进食、转盘或结算状态。</summary>
    public void Toggle()
    {
        if (lightsOn) TurnOff();
        else Show();
    }

    private void ReadMaskSettings()
    {
        // 不访问会自动创建全局单例的 Instance；只借用游戏已有 UIManager。
        if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
        fadeDuration = uiManager != null ? uiManager.MaskFadeDuration : 0.2f;
    }

    private void Show()
    {
        ReadMaskSettings();
        EnsureOverlay();
        material.SetFloat("_Darkness", darkness);
        lightsOn = true;
        BeginLeverMotion();
        flickersLeft = Random.Range(1, 3);
        BeginBeamPhase(BeamPhase.Contact, beamLevel, 0.1f);
        // 连点反向时从当前浓度接续，避免幕罩突然跳回全透明。
        startAlpha = alpha;
        fadeTime = 0f;
        overlay.gameObject.SetActive(true);
        fading = fadeDuration > 0f;
        if (!fading) alpha = 1f;
        ApplyOpacity();
    }

    /// <summary>关卡收口时淡出幕罩并立即恢复人工OFF姿态；不再等待已停止的玩法时钟，不能用于普通点击动画。</summary>
    public void Hide()
    {
        TurnOff();
        leverMoving = false;
        RestoreLeverPose();
        beamPhase = BeamPhase.Off;
        beamLevel = 0f;
        if (material != null) material.SetFloat("_BeamStrength", 0f);
    }

    private void TurnOff()
    {
        if (!lightsOn) return;
        ReadMaskSettings();
        lightsOn = false;
        BeginLeverMotion();
        BeginBeamPhase(BeamPhase.FadeOut, 0f, 0.08f);
        startAlpha = alpha;
        fadeTime = 0f;
        fading = fadeDuration > 0f;
        if (!fading)
        {
            alpha = 0f;
            ApplyOpacity();
        }
    }

    private void Update()
    {
        if (overlay == null || !overlay.gameObject.activeSelf) return;
        UpdateCameraBounds();
        if (!fading) return;
        // 与 UIMask 一致，视觉显隐不受关卡暂停/停止玩法时钟影响。
        fadeTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(fadeTime / fadeDuration);
        alpha = Mathf.Lerp(startAlpha, lightsOn ? 1f : 0f, t);
        if (t >= 1f) fading = false;
        ApplyOpacity();
    }

    private void ApplyOpacity()
    {
        if (material == null || overlay == null) return;
        material.SetFloat("_Opacity", alpha);
        if (!lightsOn && !fading) overlay.gameObject.SetActive(false);
    }

    /// <summary>判断世界坐标是否命中开关两种姿态的合并范围；只做几何判断，调用方负责 UI 遮挡和点击互斥。</summary>
    public bool HitSwitch(Vector2 position)
    {
        // 命中区取底座与拨杆当前 pose，再并入另一 pose 的拨杆包围盒，上下两端都可点。
        Bounds bounds = switchBase != null ? switchBase.bounds : lever.bounds;
        if (lever != null && lever.gameObject.activeInHierarchy)
        {
            bounds.Encapsulate(lever.bounds);
            if (hasLeverOnPos && lever.transform.parent != null)
            {
                Vector3 otherLocal = lightsOn ? leverOffLocalPos : leverOnLocalPos;
                Vector3 deltaWorld = lever.transform.parent.TransformVector(otherLocal - lever.transform.localPosition);
                Bounds other = lever.bounds;
                other.center += deltaWorld;
                bounds.Encapsulate(other);
            }
        }
        return position.x >= bounds.min.x && position.x <= bounds.max.x &&
               position.y >= bounds.min.y && position.y <= bounds.max.y;
    }

    #region 本关拉闸与触点表现
    // 沿用146的拉闸时长、行程回弹与触点短闪；本关仅有一张拨杆，保留人工摆好的两个端点。
    private void BeginLeverMotion()
    {
        leverStartPosition = lever.transform.localPosition;
        leverStartRotation = lever.transform.localRotation;
        leverStartScale = lever.transform.localScale;
        leverElapsed = 0f;
        leverMoving = true;
    }

    /// <summary>仅由本关PLAYING时钟推进拨杆和灯光触点；暂停不推进，终态须用Hide取消并还原。</summary>
    public void Tick(float deltaTime)
    {
        if (leverMoving)
        {
            leverElapsed += deltaTime;
            float t = Mathf.Clamp01(leverElapsed / 0.22f);
            if (t >= 1f)
            {
                leverMoving = false;
                RestoreLeverPose();
            }
            else
            {
                const float contact = 0.72f;
                float travel = t < contact
                    ? 1.065f * Mathf.SmoothStep(0f, 1f, t / contact)
                    : Mathf.Lerp(1.065f, 1f, Mathf.SmoothStep(0f, 1f, (t - contact) / (1f - contact)));
                float bulge = Mathf.Sin(t * Mathf.PI);
                Vector3 targetPosition = lightsOn ? leverOnLocalPos : leverOffLocalPos;
                Vector3 targetScale = lightsOn ? leverOnScale : leverOffScale;
                lever.transform.localPosition = Vector3.LerpUnclamped(leverStartPosition, targetPosition, travel);
                lever.transform.localRotation = Quaternion.SlerpUnclamped(leverStartRotation, leverOffRotation, travel)
                    * Quaternion.Euler(0f, 0f, (lightsOn ? -3f : 3f) * bulge);
                Vector3 scale = Vector3.LerpUnclamped(leverStartScale, targetScale, travel);
                scale.x *= 1f + 0.08f * bulge;
                // 与146一致地跨过正负Y完成翻面，经过侧边时保留12%厚度。
                float edge = Mathf.Abs(targetScale.y) * 0.12f;
                if (Mathf.Abs(scale.y) < edge) scale.y = (scale.y < 0f ? -1f : 1f) * edge;
                lever.transform.localScale = scale;
            }
        }
        if (beamPhase == BeamPhase.Off || beamPhase == BeamPhase.Steady) return;
        beamElapsed += deltaTime;
        float progress = Mathf.Clamp01(beamElapsed / beamDuration);
        beamLevel = Mathf.Lerp(beamFrom, beamTarget, Mathf.SmoothStep(0f, 1f, progress));
        material.SetFloat("_BeamStrength", beamLevel);
        if (progress < 1f) return;
        switch (beamPhase)
        {
            case BeamPhase.Contact: BeginBeamPhase(BeamPhase.FadeIn, 1f, 0.07f); break;
            case BeamPhase.FadeIn: BeginFlickerDim(); break;
            case BeamPhase.FlickerDim:
                BeginBeamPhase(BeamPhase.FlickerBright, 1f, Random.Range(0.025f, 0.045f)); break;
            case BeamPhase.FlickerBright:
                if (--flickersLeft > 0) BeginFlickerDim(); else beamPhase = BeamPhase.Steady;
                break;
            case BeamPhase.FadeOut: beamPhase = BeamPhase.Off; break;
        }
    }

    private void BeginBeamPhase(BeamPhase phase, float target, float duration)
    {
        beamPhase = phase;
        beamFrom = beamLevel;
        beamTarget = target;
        beamElapsed = 0f;
        beamDuration = duration;
    }

    private void BeginFlickerDim()
    {
        BeginBeamPhase(BeamPhase.FlickerDim, Random.Range(0.06f, 0.25f), Random.Range(0.025f, 0.045f));
    }

    private void RestoreLeverPose()
    {
        if (lever == null) return;
        lever.transform.localPosition = lightsOn ? leverOnLocalPos : leverOffLocalPos;
        lever.transform.localRotation = leverOffRotation;
        lever.transform.localScale = lightsOn ? leverOnScale : leverOffScale;
    }
    #endregion

    private void UpdateCameraBounds()
    {
        float distance = Mathf.Abs(viewCamera.transform.position.z - transform.position.z);
        Vector3 bottomLeft = viewCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 topRight = viewCamera.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
        if (hasCameraBounds && bottomLeft == lastBottomLeft && topRight == lastTopRight) return;
        lastBottomLeft = bottomLeft;
        lastTopRight = topRight;
        hasCameraBounds = true;
        vertices[0] = transform.InverseTransformPoint(bottomLeft);
        vertices[1] = transform.InverseTransformPoint(new Vector3(topRight.x, bottomLeft.y, bottomLeft.z));
        vertices[2] = transform.InverseTransformPoint(topRight);
        vertices[3] = transform.InverseTransformPoint(new Vector3(bottomLeft.x, topRight.y, bottomLeft.z));
        mesh.vertices = vertices;
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        material.SetFloat("_BeamTopY", topRight.y);
    }

    private void OnDestroy()
    {
        fading = false;
        // 底座与拨杆属于关卡 Prefab；只把拨杆放回编辑器原始姿态与位置。
        lightsOn = false;
        leverMoving = false;
        RestoreLeverPose();
        if (overlay != null) overlay.sharedMaterial = null;
        if (material != null) Destroy(material);
        if (mesh != null) Destroy(mesh);
    }
}
