using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rebases this level's real climb below the fixed summit between delivery cycles.
/// The exit and summit scenery stay authored in place; every settled layer is retained.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class Level119ClimbLoop : MonoBehaviour
{
    private static readonly int StartHeightId = Shader.PropertyToID("_StartHeight");
    private static readonly int EndHeightId = Shader.PropertyToID("_EndHeight");

    [SerializeField] private Transform summitScenery;
    [SerializeField] private Transform[] movingRoots;
    [SerializeField] private Transform[] backgroundTiles;
    [SerializeField] private InfiniteScrollMap backgroundScroller;
    [SerializeField] private Transform[] sideFallAreas;
    [SerializeField] private float loopEntryCameraBottom = 70f;
    [SerializeField] private float rebaseSupportHeight = 110f;
    [SerializeField, Min(1f)] private float rebaseStep = 20f;
    [SerializeField, Min(1f)] private float backgroundTileSize = 50f;

#if UNITY_EDITOR
    [SerializeField] private bool showLoopSceneGuide = true;

    private void OnDrawGizmos()
    {
        if (!showLoopSceneGuide || Camera.current == null || Camera.current.cameraType != CameraType.SceneView)
            return;

        // World-space thresholds, just like RebaseBetweenLayers. This is an authoring
        // guide, not a camera viewport or a hard upper boundary of the dark background.
        const float left = -10.5f;
        const float right = 24f;
        float returnReferenceY = rebaseSupportHeight - rebaseStep;
        using (new UnityEditor.Handles.DrawingScope(Color.cyan, Matrix4x4.identity))
        {
            UnityEditor.Handles.DrawWireCube(
                new Vector3((left + right) * 0.5f, (loopEntryCameraBottom + rebaseSupportHeight) * 0.5f, 0f),
                new Vector3(right - left, rebaseSupportHeight - loopEntryCameraBottom, 0f));
            UnityEditor.Handles.Label(new Vector3(left, loopEntryCameraBottom, 0f),
                $"119 深空 LOOP 入口 Y={loopEntryCameraBottom:g}\n相机底边超过此线后进入；线框不是背景裁切范围");
            UnityEditor.Handles.Label(new Vector3(left, (loopEntryCameraBottom + rebaseSupportHeight) * 0.5f, 0f),
                "深色循环观察区（仅 Scene 标识，不改变背景）");
        }

        using (new UnityEditor.Handles.DrawingScope(Color.yellow, Matrix4x4.identity))
        {
            UnityEditor.Handles.DrawLine(new Vector3(left, rebaseSupportHeight, 0f), new Vector3(right, rebaseSupportHeight, 0f));
            UnityEditor.Handles.Label(new Vector3(left, rebaseSupportHeight, 0f),
                $"承托面 Y≥{rebaseSupportHeight:g}：层间尝试回移（不是深色区上界）");
            UnityEditor.Handles.DrawDottedLine(new Vector3(left, returnReferenceY, 0f), new Vector3(right, returnReferenceY, 0f), 5f);
            UnityEditor.Handles.DrawLine(new Vector3(right - 1f, rebaseSupportHeight, 0f), new Vector3(right - 1f, returnReferenceY, 0f));
            UnityEditor.Handles.Label(new Vector3(left, returnReferenceY, 0f),
                $"标准回移参考 {rebaseSupportHeight:g} → {returnReferenceY:g}（最多 {rebaseStep:g}）\n实际落点受承托高度、相机安全余量和剩余回移预算影响");
        }
    }
#endif

    private readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>();
    private readonly List<Vector2> bodyPositions = new List<Vector2>();
    private readonly List<BackgroundState> backgrounds = new List<BackgroundState>();
    private readonly List<FallAreaState> fallAreas = new List<FallAreaState>();
    private Camera gameplayCamera;
    private Level119CameraFollowArea cameraFollow;
    private float summitSupportY;
    private float worldOffsetY;
    private bool loopEntered;
    private bool finalApproach;
    private bool ownsBackground;
    private bool suspended = true;
    private bool summitInitiallyActive;

    private sealed class BackgroundState
    {
        public SpriteRenderer renderer;
        public MaterialPropertyBlock properties;
        public float startHeight;
        public float endHeight;
    }

    private sealed class FallAreaState
    {
        public BoxCollider2D collider;
        public Vector2 offset;
        public Vector2 size;
        public float scaleY;
    }

    public bool IsFinalApproach => finalApproach;

    public void Initialize(Camera camera, Level119CameraFollowArea follow, float supportY)
    {
        gameplayCamera = camera;
        cameraFollow = follow;
        summitSupportY = supportY;
        worldOffsetY = 0f;
        loopEntered = false;
        finalApproach = false;
        suspended = true;
        ownsBackground = false;
        summitInitiallyActive = summitScenery != null && summitScenery.gameObject.activeSelf;

        fallAreas.Clear();
        if (sideFallAreas == null)
            return;

        foreach (Transform area in sideFallAreas)
        {
            if (area == null)
                continue;

            foreach (BoxCollider2D collider in area.GetComponentsInChildren<BoxCollider2D>(true))
            {
                fallAreas.Add(new FallAreaState
                {
                    collider = collider,
                    offset = collider.offset,
                    size = collider.size,
                    scaleY = Mathf.Max(0.001f, Mathf.Abs(collider.transform.lossyScale.y))
                });
            }
        }
    }

    public void SetSuspended(bool value)
    {
        suspended = value;
    }

    private void LateUpdate()
    {
        if (suspended || gameplayCamera == null)
            return;

        ObserveLoopEntry();
        if (ownsBackground)
            UpdateBackgroundTiles();
    }

    private void ObserveLoopEntry()
    {
        if (loopEntered || finalApproach)
            return;

        if (gameplayCamera.transform.position.y - gameplayCamera.orthographicSize <= loopEntryCameraBottom)
            return;

        loopEntered = true;
        if (summitScenery != null)
            summitScenery.gameObject.SetActive(false);
    }

    // The prediction budgets coordinate shifts only; it never completes or removes a layer.
    public float RebaseBetweenLayers(float supportTopY, float predictedFinalTopY)
    {
        if (suspended || gameplayCamera == null || cameraFollow == null || finalApproach)
            return 0f;

        ObserveLoopEntry();
        if (!loopEntered)
            return 0f;

        float remainingShift = predictedFinalTopY - summitSupportY;
        if (remainingShift <= 0.001f)
        {
            BeginFinalApproach();
            return 0f;
        }

        if (supportTopY < rebaseSupportHeight)
            return 0f;

        float cameraBottom = gameplayCamera.transform.position.y - gameplayCamera.orthographicSize;
        float distance = Mathf.Min(rebaseStep, remainingShift, cameraBottom - loopEntryCameraBottom - 1f);
        if (distance <= 0.001f)
            return 0f;

        float deltaY = -distance;
        ApplyWorldShift(deltaY);
        if (remainingShift - distance <= 0.001f)
            BeginFinalApproach();

        return deltaY;
    }

    private void BeginFinalApproach()
    {
        finalApproach = true;
        if (summitScenery != null)
            summitScenery.gameObject.SetActive(summitInitiallyActive);
    }

    private void ApplyWorldShift(float deltaY)
    {
        TakeBackgroundOwnership();
        bodies.Clear();
        bodyPositions.Clear();
        if (movingRoots != null)
        {
            foreach (Transform root in movingRoots)
            {
                if (root == null)
                    continue;

                foreach (Rigidbody2D body in root.GetComponentsInChildren<Rigidbody2D>(true))
                {
                    bodies.Add(body);
                    bodyPositions.Add(body.position);
                }
            }

            foreach (Transform root in movingRoots)
            {
                if (root != null)
                    root.position += Vector3.up * deltaY;
            }
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            Rigidbody2D body = bodies[i];
            RigidbodyInterpolation2D interpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.position = bodyPositions[i] + Vector2.up * deltaY;
            body.interpolation = interpolation;
        }

        worldOffsetY += deltaY;
        cameraFollow.ApplyWorldShift(deltaY);
        foreach (BackgroundState background in backgrounds)
        {
            background.renderer.GetPropertyBlock(background.properties);
            background.properties.SetFloat(StartHeightId, background.startHeight + worldOffsetY);
            background.properties.SetFloat(EndHeightId, background.endHeight + worldOffsetY);
            background.renderer.SetPropertyBlock(background.properties);
        }

        foreach (FallAreaState area in fallAreas)
        {
            area.collider.offset = area.offset + Vector2.up * (worldOffsetY / (2f * area.scaleY));
            area.collider.size = area.size - Vector2.up * (worldOffsetY / area.scaleY);
        }

        UpdateBackgroundTiles();
        Physics2D.SyncTransforms();
    }

    private void TakeBackgroundOwnership()
    {
        if (ownsBackground)
            return;

        ownsBackground = true;
        if (backgroundScroller != null)
            backgroundScroller.enabled = false;

        backgrounds.Clear();
        if (backgroundTiles == null)
            return;

        foreach (Transform tile in backgroundTiles)
        {
            if (tile == null)
                continue;

            foreach (SpriteRenderer renderer in tile.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(StartHeightId) || !material.HasProperty(EndHeightId))
                    continue;

                backgrounds.Add(new BackgroundState
                {
                    renderer = renderer,
                    properties = new MaterialPropertyBlock(),
                    startHeight = material.GetFloat(StartHeightId),
                    endHeight = material.GetFloat(EndHeightId)
                });
            }
        }
    }

    private void UpdateBackgroundTiles()
    {
        if (backgroundTiles == null || backgroundTiles.Length < 2 || gameplayCamera == null)
            return;

        // Keep the original texture lattice phase across every world-coordinate shift.
        float cameraY = gameplayCamera.transform.position.y;
        float centerY = Mathf.Round((cameraY - worldOffsetY) / backgroundTileSize) * backgroundTileSize + worldOffsetY;
        for (int i = 0; i < backgroundTiles.Length; i++)
        {
            Transform tile = backgroundTiles[i];
            if (tile == null)
                continue;

            float tileY = i == 1
                ? centerY + (cameraY >= centerY ? backgroundTileSize : -backgroundTileSize)
                : centerY;
            Vector3 position = tile.position;
            position.y = tileY;
            tile.position = position;
            tile.gameObject.SetActive(true);
        }
    }
}
