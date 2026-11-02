using UnityEngine;

/// <summary>
/// Level 134 专属纵向相机跟随。普通攀爬只允许镜头向上；载运时冻结；
/// 载运台阶回到真实地板后允许镜头平滑回落。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level134CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform actor;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float actorScreenOffsetY = -1.5f;
    [SerializeField, Min(0f)] private float smoothTime = 0.1f;
    [SerializeField, Min(0f)] private float verticalDeadZone = 0.02f;

    private Vector3 initialCameraPosition;
    private Vector3 velocity;
    private bool initialized;
    private bool followEnabled;
    private bool allowDownwardFollow;

    public Camera TargetCamera => targetCamera;
    public bool IsFollowing => followEnabled;
    public float ActorScreenOffsetY => actorScreenOffsetY;

    public float ResolveCameraY(float actorWorldY)
    {
        return actorWorldY - actorScreenOffsetY;
    }

    private void LateUpdate()
    {
        if (!followEnabled || actor == null || targetCamera == null)
        {
            return;
        }

        float desiredY = Mathf.Max(initialCameraPosition.y, ResolveCameraY(actor.position.y));
        float currentY = targetCamera.transform.position.y;
        if (!allowDownwardFollow && desiredY <= currentY + verticalDeadZone)
        {
            return;
        }

        Vector3 targetPosition = new Vector3(
            initialCameraPosition.x,
            desiredY,
            initialCameraPosition.z);

        targetCamera.transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(
                targetCamera.transform.position,
                targetPosition,
                ref velocity,
                smoothTime)
            : targetPosition;

        if (allowDownwardFollow
            && Mathf.Abs(targetCamera.transform.position.y - desiredY) <= verticalDeadZone)
        {
            allowDownwardFollow = false;
        }
    }

    public void Configure(Transform actorTarget, Camera cameraTarget)
    {
        actor = actorTarget;
        targetCamera = cameraTarget;
        if (targetCamera == null)
        {
            return;
        }

        initialCameraPosition = targetCamera.transform.position;
        initialized = true;
        velocity = Vector3.zero;
    }

    public void BeginClimb()
    {
        EnsureInitialized();
        followEnabled = initialized;
        allowDownwardFollow = false;
        velocity = Vector3.zero;
    }

    public void Freeze()
    {
        followEnabled = false;
        allowDownwardFollow = false;
        velocity = Vector3.zero;
    }

    public void ResumeAfterGroundLanding()
    {
        EnsureInitialized();
        followEnabled = initialized;
        allowDownwardFollow = true;
        velocity = Vector3.zero;
    }

    public void FreezeForFinalStage()
    {
        Freeze();
    }

    public void ApplyWorldShift(float deltaY)
    {
        EnsureInitialized();
        initialCameraPosition.y += deltaY;
        if (targetCamera != null)
            targetCamera.transform.position += Vector3.up * deltaY;
        // SmoothDamp velocity is relative motion, so retain it across a common translation.
    }

    public void RestoreInitialPosition()
    {
        EnsureInitialized();
        Freeze();
        if (targetCamera != null)
        {
            targetCamera.transform.position = initialCameraPosition;
        }
    }

    private void EnsureInitialized()
    {
        if (initialized || targetCamera == null)
        {
            return;
        }

        initialCameraPosition = targetCamera.transform.position;
        initialized = true;
    }
}
