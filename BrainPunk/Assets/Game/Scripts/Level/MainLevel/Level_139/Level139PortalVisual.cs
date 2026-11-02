using System;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>本关往返使用同轮廓的抗锯齿黑门洞；首次共享出生不接管。</summary>
[DisallowMultipleComponent]
public sealed class Level139PortalVisual : MonoBehaviour
{
    [SerializeField] private Material blackInteriorMaterial;
    private const int MaxEdges = 16;
    private static readonly int EdgeCountId = Shader.PropertyToID("_EdgeCount");
    private static readonly int EdgesId = Shader.PropertyToID("_Edges");
    private readonly Vector4[] edgePlanes = new Vector4[MaxEdges];
    private SkeletonAnimation effectAnimation;
    private MeshRenderer effectRenderer;
    private bool effectWasRenderingOff;
    private GameObject blackInterior;
    private MeshRenderer blackRenderer;
    private Mesh blackMesh;
    private Material blackMaterial;
    private readonly ExposedList<float> polygon = new ExposedList<float>();
    private readonly Triangulator triangulator = new Triangulator();
    private Vector3[] vertices;
    private int[] triangles;

    public bool TryUseBlackInterior()
    {
        if (!isActiveAndEnabled)
            return false;
        if (blackInterior != null)
            return true;
        try
        {
            // 可见漩涡在独立 effect 骨架；主门骨架的 EF_bg 位于门洞外，不能用来填黑。
            Transform effect = transform.Find("effect");
            effectAnimation = effect != null ? effect.GetComponent<SkeletonAnimation>() : null;
            effectRenderer = effect != null ? effect.GetComponent<MeshRenderer>() : null;
            if (effectRenderer != null)
                effectWasRenderingOff = effectRenderer.forceRenderingOff;
            if (effectAnimation == null || effectRenderer == null || !effect.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Level139 楼层门缺少可用的原 effect 漩涡。");
            effectAnimation.Initialize(false);
            var clipSlot = effectAnimation.Skeleton?.FindSlot("EF");
            if (!(clipSlot?.Attachment is ClippingAttachment) || blackInteriorMaterial == null
                || !blackInteriorMaterial.HasProperty(EdgeCountId))
                throw new InvalidOperationException("Level139 原漩涡缺少 EF 裁剪轮廓或材质。");

            blackInterior = new GameObject("Level139_ManualBlackInterior");
            blackInterior.SetActive(false);
            blackInterior.layer = effect.gameObject.layer;
            blackInterior.transform.SetParent(effect, false);
            blackMesh = new Mesh { name = "Level139_OriginalPortalContour", hideFlags = HideFlags.DontSave };
            blackMesh.MarkDynamic();
            blackInterior.AddComponent<MeshFilter>().sharedMesh = blackMesh;
            blackRenderer = blackInterior.AddComponent<MeshRenderer>();
            blackRenderer.sortingLayerID = effectRenderer.sortingLayerID;
            blackRenderer.sortingOrder = effectRenderer.sortingOrder;
            blackRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blackRenderer.receiveShadows = false;

            // 独立材质只在轮廓内侧约一屏幕像素抗锯齿，内部固定不透明黑。
            // 不采样原纹理 alpha，不改变轮廓或共享漩涡材质。
            blackMaterial = new Material(blackInteriorMaterial)
            {
                name = "Level139_OpaquePortalBlack",
                hideFlags = HideFlags.DontSave
            };
            blackRenderer.sharedMaterial = blackMaterial;
            UpdateBlackMesh(effectAnimation);
            if (blackMesh.vertexCount < 3)
                throw new InvalidOperationException("Level139 原漩涡裁剪轮廓无有效顶点。");
            effectAnimation.UpdateComplete += UpdateBlackMesh;
            effectRenderer.forceRenderingOff = true;
            blackInterior.SetActive(true);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            UseSharedVisuals();
            return false;
        }
    }

    private void UpdateBlackMesh(ISkeletonAnimation animated)
    {
        if (blackMesh == null || blackRenderer == null)
            return;
        Slot slot = animated.Skeleton?.FindSlot("EF");
        var clip = slot?.Attachment as ClippingAttachment;
        if (clip == null || clip.WorldVerticesLength < 6)
        {
            blackRenderer.enabled = false;
            return;
        }
        polygon.Resize(clip.WorldVerticesLength);
        clip.ComputeWorldVertices(slot, 0, clip.WorldVerticesLength, polygon.Items, 0, 2);
        // 与 Spine 自身裁剪使用同一顶点及三角化算法；自动包含 initialFlipX，不另猜矩形。
        SkeletonClipping.MakeClockwise(polygon);
        int count = polygon.Count / 2;
        if (count > MaxEdges)
        {
            blackRenderer.enabled = false;
            return;
        }
        if (vertices == null || vertices.Length != count)
            vertices = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = new Vector3(polygon.Items[i * 2], polygon.Items[i * 2 + 1], 0f);
            int next = (i + 1) % count;
            Vector2 start = new Vector2(polygon.Items[i * 2], polygon.Items[i * 2 + 1]);
            Vector2 end = new Vector2(polygon.Items[next * 2], polygon.Items[next * 2 + 1]);
            Vector2 edge = end - start;
            // 当前源 EF 轮廓是凸八边形；顺时针边的右侧为内部。
            Vector2 inward = new Vector2(edge.y, -edge.x).normalized;
            edgePlanes[i] = new Vector4(inward.x, inward.y, -Vector2.Dot(inward, start), 0f);
        }
        blackMaterial.SetFloat(EdgeCountId, count);
        blackMaterial.SetVectorArray(EdgesId, edgePlanes);
        var indices = triangulator.Triangulate(polygon);
        if (triangles == null || triangles.Length != indices.Count)
            triangles = new int[indices.Count];
        Array.Copy(indices.Items, triangles, indices.Count);
        blackMesh.Clear();
        blackMesh.vertices = vertices;
        blackMesh.triangles = triangles;
        blackMesh.RecalculateBounds();
        blackRenderer.enabled = true;
    }

    public void UseSharedVisuals()
    {
        if (effectAnimation != null)
            effectAnimation.UpdateComplete -= UpdateBlackMesh;
        if (effectRenderer != null)
            effectRenderer.forceRenderingOff = effectWasRenderingOff;
        if (blackInterior != null)
        {
            blackInterior.SetActive(false);
            // Destroy 延迟至帧尾：先脱离源门，避免同帧复制出门副本时带入旧黑网格。
            blackInterior.transform.SetParent(null, false);
            Destroy(blackInterior);
        }
        if (blackMesh != null)
            Destroy(blackMesh);
        if (blackMaterial != null)
            Destroy(blackMaterial);
        blackInterior = null;
        blackRenderer = null;
        blackMesh = null;
        blackMaterial = null;
        effectAnimation = null;
        effectRenderer = null;
    }

    private void OnDisable() => UseSharedVisuals();
    private void OnDestroy() => UseSharedVisuals();
}
