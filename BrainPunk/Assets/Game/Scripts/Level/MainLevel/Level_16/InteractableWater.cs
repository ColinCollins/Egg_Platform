using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;


[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(EdgeCollider2D))]
[RequireComponent(typeof(WaterTriggerHandler))]
public class InteractableWater : MonoBehaviour
{
    [Header("Mesh Generation")]
    [Range(2, 500)] public int NumOfVertices = 50;

    public float Width = 10f;
    public float Height = 4f;

    public Material WaterMaterial;
    private const int NUM_OF_Y_VERTICES = 2;

    [Header("Springs")]
    [SerializeField] private float _spriteConstant = 1.4f;
    [SerializeField] private float _damping = 1.1f;
    [SerializeField] private float _spread = 6.5f;
    [SerializeField, Range(1, 10)] private int _wavePropogationIterations = 0;
    [SerializeField, Range(0, 20)] private float _speedMult = 5.5f;

    [Header("Force")]
    public float ForceMultiplier = 0.2f;
    [SerializeField, Range(1, 50f)]
    public float MaxForce = 5f;

    [Header("Collision")]
    [SerializeField, Range(1, 10f)]
    private float _playerCollisionRadiusMult = 4.15f;

    [Header("Gizmos")]
    public Color GizmosColor = Color.white;

    private Mesh _mesh;
    private MeshRenderer _meshRenderer;
    private MeshFilter _meshFilter;
    private Vector3[] _vertices;
    private int[] _topVerticesIndex;

    private EdgeCollider2D _collider;

    private void Start()
    {
        GenerateMesh();
        CreateWaterPoint();
    }

    // 编译阶段生效，但是这个其实不是很重要
    private void Reset()
    {
        _collider = GetComponent<EdgeCollider2D>();
        _collider.isTrigger = true;
    }

    [Button("Refresh button")]
    private void RefrehsMesh()
    {
        GenerateMesh();
        CreateWaterPoint();
        ResetEdgeCollider();
    }

    private void GenerateMesh()
    {
        _mesh = new Mesh();

        // add vertices;

        _vertices = new Vector3[NumOfVertices * NUM_OF_Y_VERTICES];
        _topVerticesIndex = new int[NumOfVertices];

        for (int y = 0; y < NUM_OF_Y_VERTICES; y++)
        {
            for (int x = 0; x < NumOfVertices; x++)
            {
                float xPos = (x / (float)(NumOfVertices - 1)) * Width - Width / 2f;
                float yPos = (y / (float)(NUM_OF_Y_VERTICES - 1)) * Height - Height / 2f;

                _vertices[y * NumOfVertices + x] = new Vector3(xPos, yPos, 0f);

                if (y == NUM_OF_Y_VERTICES - 1)
                {
                    _topVerticesIndex[x] = y * NumOfVertices + x;
                }
            }
        }

        // 构建三角形
        int[] triangle = new int[(NumOfVertices - 1) * (NUM_OF_Y_VERTICES - 1) * 6];
        int index = 0;
        for (int y = 0; y < NUM_OF_Y_VERTICES - 1; y++)
        {
            for (int x = 0; x < NumOfVertices - 1; x++)
            {
                int bottomLeft = y * NumOfVertices + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + NumOfVertices;
                int topRight = topLeft + 1;

                triangle[index++] = bottomLeft;
                triangle[index++] = topLeft;
                triangle[index++] = bottomRight;

                triangle[index++] = bottomRight;
                triangle[index++] = topLeft;
                triangle[index++] = topRight;
            }
        }

        // 构建 uv
        Vector2[] uvs = new Vector2[_vertices.Length];
        for (int i = 0; i < _vertices.Length; i++)
        {
            uvs[i] = new Vector2((_vertices[i].x + Width / 2) / Width, (_vertices[i].y + Height / 2) / Height);
        }

        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();

        if (_meshFilter == null)
            _meshFilter = GetComponent<MeshFilter>();

        _meshRenderer.material = WaterMaterial;
        _mesh.vertices = _vertices;
        _mesh.triangles = triangle;
        _mesh.uv = uvs;

        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        _meshFilter.mesh = _mesh;
    }


    private void  ResetEdgeCollider()
    {
        _collider = GetComponent<EdgeCollider2D>();
        Vector2[] newPoints = new Vector2[2];

        Vector2 firstPoint = new Vector2(_vertices[_topVerticesIndex[0]].x, _vertices[_topVerticesIndex[0]].y);
        newPoints[0] = firstPoint;

        Vector2 secondPoint = new Vector2(_vertices[_topVerticesIndex[_topVerticesIndex.Length - 1]].x, _vertices[_topVerticesIndex[_topVerticesIndex.Length - 1]].y);
        newPoints[1] = secondPoint;

        _collider.offset = Vector2.zero;
        _collider.points= newPoints;
    }

    private class WaterPoint
    {
        public float velocity, acceleration, pos, targetHeight;
    }

    private List<WaterPoint> _waterPoint = new List<WaterPoint>();

    // visiual water point
    private void CreateWaterPoint()
    {
        _waterPoint.Clear();
        for (int i = 0; i < _topVerticesIndex.Length; i++)
        {
            _waterPoint.Add(new WaterPoint
            {
                pos = _vertices[_topVerticesIndex[i]].y,
                targetHeight = _vertices[_topVerticesIndex[i]].y
            });
        }
    }

    void FixedUpdate()
    {
        for (int i = 1; i < _waterPoint.Count - 1; i++)
        {
            WaterPoint point = _waterPoint[i];

            float x = point.pos - point.targetHeight;
            float acceleration = -_spriteConstant * x - _damping * point.velocity;
            point.pos += point.velocity * _speedMult * Time.fixedDeltaTime;

            _vertices[_topVerticesIndex[i]].y = point.pos;
            point.velocity += acceleration * _speedMult * Time.fixedDeltaTime;
        }

        // wave
        for (int j = 0; j < _wavePropogationIterations; j++)
        {
            for (int i = 1; i < _waterPoint.Count - 1; i++)
            {
                float leftDelta = _spread * (_waterPoint[i].pos - _waterPoint[i - 1].pos) * _speedMult * Time.fixedDeltaTime;
                _waterPoint[i - 1].velocity += leftDelta;

                float rightDelta = _spread * (_waterPoint[i].pos - _waterPoint[i + 1].pos) * _speedMult * Time.fixedDeltaTime;
                _waterPoint[i + 1].velocity += rightDelta;
            }
        }

        _mesh.vertices = _vertices;
    }

    public void Splash(Collider2D collision, float force)
    {
        float radius = collision.bounds.extents.x * _playerCollisionRadiusMult;
        Vector2 center = collision.transform.position;

        for (int i = 0; i < _waterPoint.Count; i++)
        {
            Vector2 vertexWorldPos = transform.TransformPoint(_vertices[_topVerticesIndex[i]]);
            if (IsPointInsiderCircle(vertexWorldPos, center, radius))
            {
                _waterPoint[i].velocity = force;
            }
        }
    }

    private bool IsPointInsiderCircle(Vector2 point, Vector2 center, float radius)
    {
        float distancesquared = (point - center).sqrMagnitude;
        return distancesquared <= (Mathf.Pow(radius, 2));
    }

}
