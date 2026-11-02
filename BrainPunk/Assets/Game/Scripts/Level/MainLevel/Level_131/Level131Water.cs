using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level 131 的浅水视觉，按 Level 125 的网格水面方式生成波浪。
/// 水体只负责水面与溅水，不负责死亡；水下 FallArea 承担失败判定。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(EdgeCollider2D))]
public sealed class Level131Water : MonoBehaviour
{
    // 弹簧与扩散参数取自 Level 125 关卡 Prefab 的实测序列化值，
    // 使本关浅水与 125 的水面行为一致。
    private const float SpringConstant = 3f;
    private const float Damping = 0.7f;
    private const float Spread = 6.5f;
    private const int WavePropagationIterations = 2;
    private const float SpeedMultiplier = 4f;

    [SerializeField] private int vertexCount = 48;
    [SerializeField] private float width = 30f;
    [SerializeField] private float height = 3.6f;
    [SerializeField] private Material waterMaterial;
    [SerializeField] private LayerMask splashMask;

    private const int BottomRow = 0;
    private const int TopRow = 1;
    private readonly List<WaterPoint> waterPoints = new();
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] topIndices;
    private EdgeCollider2D edgeCollider;

    public void Configure(float configuredWidth, float configuredHeight, Material material, LayerMask configuredSplashMask)
    {
        width = Mathf.Max(2f, configuredWidth);
        height = Mathf.Max(0.5f, configuredHeight);
        waterMaterial = material;
        splashMask = configuredSplashMask;
    }

    private void Start()
    {
        GenerateMesh();
        CreateWaterPoints();
        ResetEdgeCollider();

        SortingGroup sortingGroup = GetComponent<SortingGroup>() ?? gameObject.AddComponent<SortingGroup>();
        sortingGroup.sortingLayerID = SortingLayer.NameToID("SceneLayer");
        sortingGroup.sortingOrder = 301;
    }

    private void GenerateMesh()
    {
        int count = Mathf.Max(2, vertexCount);
        mesh = new Mesh { name = "Level131WaterMesh" };
        vertices = new Vector3[count * 2];
        topIndices = new int[count];

        for (int x = 0; x < count; x++)
        {
            float xPosition = x / (float)(count - 1) * width - width * 0.5f;
            vertices[BottomRow * count + x] = new Vector3(xPosition, -height * 0.5f, 0f);
            vertices[TopRow * count + x] = new Vector3(xPosition, height * 0.5f, 0f);
            topIndices[x] = TopRow * count + x;
        }

        int[] triangles = new int[(count - 1) * 6];
        int triangleIndex = 0;
        for (int x = 0; x < count - 1; x++)
        {
            int bottomLeft = x;
            int bottomRight = x + 1;
            int topLeft = count + x;
            int topRight = count + x + 1;
            triangles[triangleIndex++] = bottomLeft;
            triangles[triangleIndex++] = topLeft;
            triangles[triangleIndex++] = bottomRight;
            triangles[triangleIndex++] = bottomRight;
            triangles[triangleIndex++] = topLeft;
            triangles[triangleIndex++] = topRight;
        }

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int index = 0; index < vertices.Length; index++)
        {
            uvs[index] = new Vector2(
                (vertices[index].x + width * 0.5f) / width,
                (vertices[index].y + height * 0.5f) / height);
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter filter = GetComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        renderer.sharedMaterial = waterMaterial;
    }

    private void CreateWaterPoints()
    {
        waterPoints.Clear();
        int count = topIndices != null ? topIndices.Length : 0;
        for (int index = 0; index < count; index++)
        {
            float target = vertices[topIndices[index]].y;
            waterPoints.Add(new WaterPoint { position = target, target = target });
        }
    }

    private void ResetEdgeCollider()
    {
        edgeCollider = GetComponent<EdgeCollider2D>();
        edgeCollider.isTrigger = true;
        if (topIndices == null || topIndices.Length < 2)
            return;

        edgeCollider.points = new[]
        {
            new Vector2(vertices[topIndices[0]].x, vertices[topIndices[0]].y),
            new Vector2(
                vertices[topIndices[topIndices.Length - 1]].x,
                vertices[topIndices[topIndices.Length - 1]].y),
        };
    }

    private void FixedUpdate()
    {
        if (mesh == null || waterPoints.Count < 3)
            return;

        for (int index = 1; index < waterPoints.Count - 1; index++)
        {
            WaterPoint point = waterPoints[index];
            float displacement = point.position - point.target;
            float acceleration = -SpringConstant * displacement - Damping * point.velocity;
            point.position += point.velocity * SpeedMultiplier * Time.fixedDeltaTime;
            point.velocity += acceleration * SpeedMultiplier * Time.fixedDeltaTime;
            vertices[topIndices[index]].y = point.position;
        }

        // 125 的波浪扩散：相邻水质点互相传递速度，形成行波。
        for (int iteration = 0; iteration < WavePropagationIterations; iteration++)
        {
            for (int index = 1; index < waterPoints.Count - 1; index++)
            {
                float leftDelta =
                    Spread * (waterPoints[index].position - waterPoints[index - 1].position) *
                    SpeedMultiplier * Time.fixedDeltaTime;
                waterPoints[index - 1].velocity += leftDelta;

                float rightDelta =
                    Spread * (waterPoints[index].position - waterPoints[index + 1].position) *
                    SpeedMultiplier * Time.fixedDeltaTime;
                waterPoints[index + 1].velocity += rightDelta;
            }
        }

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || splashMask.value == 0 ||
            (splashMask.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        Rigidbody2D body = other.GetComponentInParent<Rigidbody2D>();
        if (body == null || waterPoints.Count == 0)
            return;

        float force = Mathf.Clamp(Mathf.Abs(body.linearVelocityY) * 0.2f, 0f, 5f);
        if (body.linearVelocityY < 0f)
            force = -force;

        Vector2 center = other.transform.position;
        float radius = Mathf.Max(0.4f, other.bounds.extents.x * 1.5f);
        for (int index = 0; index < waterPoints.Count; index++)
        {
            Vector2 point = transform.TransformPoint(vertices[topIndices[index]]);
            if ((point - center).sqrMagnitude <= radius * radius)
                waterPoints[index].velocity = force;
        }
    }

    private sealed class WaterPoint
    {
        public float position;
        public float target;
        public float velocity;
    }
}
