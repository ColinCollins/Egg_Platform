using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 L82 石头图片生成带简化多边形碰撞的独立 prefab。
/// </summary>
internal static class Level82StonePrefabGenerator
{
    private const string SpriteFolder = "Assets/Game/ArtSrc/Sprites/L82";
    private const string PrefabRoot = "Assets/Game/Prefabs/LevelItems/Level_82";
    private const string PrefabFolder = PrefabRoot + "/Stones";
    private const string MaterialPath = PrefabRoot + "/Level82Stone.physicsMaterial2D";
    private const int MinimumVertices = 6;
    private const int MaximumVertices = 12;

    private static readonly string[] SpriteNames =
    {
        "Sp_l82_st_",
        "Sp_l82_st_02",
        "Sp_l82_st_03",
        "Sp_l82_st_04",
        "Sp_l82_st_05",
        "Sp_l82_st_06",
        "Sp_l82_st_07",
        "Sp_l82_st_08",
        "Sp_l82_st_09",
        "Sp_l82_st_10",
        "Sp_l82_st_11",
        "Sp_l82_st_12",
        "Sp_l82_st_13"
    };

    [MenuItem("Tools/Level 082/Regenerate Stone Prefabs")]
    private static void RegeneratePrefabs()
    {
        GeneratePrefabs(true);
    }

    private static void GeneratePrefabs(bool overwriteExisting)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureFolder("Assets/Game/Prefabs/LevelItems");
        EnsureFolder(PrefabRoot);
        EnsureFolder(PrefabFolder);

        PhysicsMaterial2D material = LoadOrCreateMaterial();
        int generatedCount = 0;

        for (int index = 0; index < SpriteNames.Length; index++)
        {
            string prefabPath = $"{PrefabFolder}/Stone_L82_{index + 1:00}.prefab";
            if (!overwriteExisting && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                continue;

            string spritePath = $"{SpriteFolder}/{SpriteNames[index]}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Level82StonePrefabGenerator] 找不到石头图片：{spritePath}");
                continue;
            }

            CreateStonePrefab(index + 1, sprite, material, prefabPath);
            generatedCount++;
        }

        if (generatedCount <= 0)
            return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Level82StonePrefabGenerator] 已生成 {generatedCount} 个 Level 82 石头 prefab。");
    }

    private static PhysicsMaterial2D LoadOrCreateMaterial()
    {
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
        if (material != null)
            return material;

        material = new PhysicsMaterial2D("Level82Stone")
        {
            friction = 1.1f,
            bounciness = 0f
        };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static void CreateStonePrefab(
        int number,
        Sprite sprite,
        PhysicsMaterial2D material,
        string prefabPath)
    {
        GameObject stone = new GameObject($"Stone_L82_{number:00}");
        try
        {
            stone.layer = LayerMask.NameToLayer("WithoutShadowGround");

            SpriteRenderer spriteRenderer = stone.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingLayerName = "SceneLayer";
            spriteRenderer.sortingOrder = 10;

            Rigidbody2D body = stone.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.useAutoMass = true;
            body.gravityScale = 1.8f;
            body.linearDamping = 0.15f;
            body.angularDamping = 2f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.StartAwake;

            PolygonCollider2D collider = stone.AddComponent<PolygonCollider2D>();
            collider.sharedMaterial = material;
            collider.density = 1f;
            SimplifyCollider(collider, sprite);

            PrefabUtility.SaveAsPrefabAsset(stone, prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(stone);
        }
    }

    private static void SimplifyCollider(PolygonCollider2D collider, Sprite sprite)
    {
        List<Vector2> points = GetLargestPath(collider);
        if (points.Count < MinimumVertices)
            points = CreateFallbackOctagon(sprite.bounds.extents * 0.94f);

        float tolerance = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) * 0.025f;
        while (points.Count > MaximumVertices)
            RemoveLeastSignificantVertex(points);

        while (points.Count > MinimumVertices && FindLeastSignificantDistance(points) < tolerance)
            RemoveLeastSignificantVertex(points);

        collider.pathCount = 1;
        collider.SetPath(0, points);
    }

    private static List<Vector2> GetLargestPath(PolygonCollider2D collider)
    {
        List<Vector2> largest = new();
        float largestArea = 0f;

        for (int pathIndex = 0; pathIndex < collider.pathCount; pathIndex++)
        {
            Vector2[] path = collider.GetPath(pathIndex);
            float area = Mathf.Abs(CalculateArea(path));
            if (area <= largestArea)
                continue;

            largestArea = area;
            largest = new List<Vector2>(path);
        }

        return largest;
    }

    private static float CalculateArea(IReadOnlyList<Vector2> points)
    {
        float area = 0f;
        for (int index = 0; index < points.Count; index++)
        {
            Vector2 current = points[index];
            Vector2 next = points[(index + 1) % points.Count];
            area += current.x * next.y - next.x * current.y;
        }

        return area * 0.5f;
    }

    private static void RemoveLeastSignificantVertex(List<Vector2> points)
    {
        if (points.Count <= 3)
            return;

        int removeIndex = 0;
        float smallestDistance = float.MaxValue;
        for (int index = 0; index < points.Count; index++)
        {
            float distance = DistanceToNeighbourSegment(points, index);
            if (distance >= smallestDistance)
                continue;

            smallestDistance = distance;
            removeIndex = index;
        }

        points.RemoveAt(removeIndex);
    }

    private static float FindLeastSignificantDistance(List<Vector2> points)
    {
        float smallestDistance = float.MaxValue;
        for (int index = 0; index < points.Count; index++)
            smallestDistance = Mathf.Min(smallestDistance, DistanceToNeighbourSegment(points, index));

        return smallestDistance;
    }

    private static float DistanceToNeighbourSegment(List<Vector2> points, int index)
    {
        Vector2 previous = points[(index - 1 + points.Count) % points.Count];
        Vector2 current = points[index];
        Vector2 next = points[(index + 1) % points.Count];
        Vector2 segment = next - previous;

        if (segment.sqrMagnitude <= Mathf.Epsilon)
            return Vector2.Distance(current, previous);

        float t = Mathf.Clamp01(Vector2.Dot(current - previous, segment) / segment.sqrMagnitude);
        return Vector2.Distance(current, previous + segment * t);
    }

    private static List<Vector2> CreateFallbackOctagon(Vector3 extents)
    {
        float x = Mathf.Max(0.05f, extents.x);
        float y = Mathf.Max(0.05f, extents.y);
        return new List<Vector2>
        {
            new(-x * 0.55f, -y),
            new(x * 0.55f, -y),
            new(x, -y * 0.45f),
            new(x, y * 0.45f),
            new(x * 0.55f, y),
            new(-x * 0.55f, y),
            new(-x, y * 0.45f),
            new(-x, -y * 0.45f)
        };
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int split = folderPath.LastIndexOf('/');
        string parent = folderPath.Substring(0, split);
        string folderName = folderPath.Substring(split + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
