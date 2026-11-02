using Game.Level;
using UnityEngine;

/// <summary>
/// Level 130 camera policy: continuously follows the Actor on X while preserving
/// the authored camera Y/Z and clamping the orthographic view to the level range.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level130ContinuousHorizontalCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform actor;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float horizontalOffset = 8f;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.12f;
    [SerializeField] private CameraFollowRange followRange;

    private float fixedCameraY;
    private float fixedCameraZ;
    private float horizontalVelocity;
    private bool cameraAxesCached;

    private void Awake()
    {
        ResolveReferences();
        CacheCameraAxes();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheCameraAxes();
        horizontalVelocity = 0f;
    }

    private void LateUpdate()
    {
        ResolveReferences();
        CacheCameraAxes();
        if (actor == null || targetCamera == null || !cameraAxesCached)
            return;

        float targetX = actor.position.x + horizontalOffset;
        Vector3 targetPosition = new Vector3(targetX, fixedCameraY, fixedCameraZ);
        if (followRange != null)
            targetPosition = followRange.ClampOrthographic(targetCamera, targetPosition);

        float nextX = followSmoothTime > 0f
            ? Mathf.SmoothDamp(
                targetCamera.transform.position.x,
                targetPosition.x,
                ref horizontalVelocity,
                followSmoothTime)
            : targetPosition.x;
        Vector3 nextPosition = new Vector3(nextX, fixedCameraY, fixedCameraZ);
        if (followRange != null)
            nextPosition = followRange.ClampOrthographic(targetCamera, nextPosition);

        nextPosition.y = fixedCameraY;
        nextPosition.z = fixedCameraZ;
        targetCamera.transform.position = nextPosition;
    }

    private void OnDisable()
    {
        horizontalVelocity = 0f;
    }

    private void ResolveReferences()
    {
        if (actor == null)
        {
            BaseLevelCtrl owner = GetComponent<BaseLevelCtrl>();
            if (owner != null && owner.Actor != null)
                actor = owner.Actor.transform;
        }

        if (targetCamera == null)
        {
            Transform cameraTransform = transform.Find("GameCamera");
            if (cameraTransform != null)
                targetCamera = cameraTransform.GetComponent<Camera>();
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        if (followRange == null)
            followRange = GetComponent<CameraFollowRange>();
    }

    private void CacheCameraAxes()
    {
        if (cameraAxesCached || targetCamera == null)
            return;

        Vector3 cameraPosition = targetCamera.transform.position;
        fixedCameraY = cameraPosition.y;
        fixedCameraZ = cameraPosition.z;
        cameraAxesCached = true;
    }
}
