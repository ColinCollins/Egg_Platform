using UnityEngine;

/// <summary>
/// Level59 控制器：根据双指缩放监听器的 Scale 值同步调整 Actor 缩放、相机视野，并控制 Door 碰撞体开关
/// </summary>
public class Level59Ctrl : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Camera mainCamera;

    [Header("Scale Ranges")]
    [Tooltip("Actor localScale 的乘数范围 (min, max)，基于初始 localScale 进行缩放")]
    [SerializeField] private Vector2 actorScaleRange = new Vector2(0.1f, 1f);

    [Tooltip("Camera orthographicSize 的绝对值映射范围 (min, max)")]
    [SerializeField] private Vector2 cameraSizeScaleRange = new Vector2(0.41f, 5.4f);

    [Tooltip("Actor 移动速度的映射范围 (min, max)")]
    [SerializeField] private Vector2 actorSpeedRange = new Vector2(0.5f, 5f);

    [Tooltip("Actor 跳跃力的映射范围 (min, max)")]
    [SerializeField] private Vector2 actorJumpForceRange = new Vector2(3f, 18f);

    [Header("Listener & Collider")]
    [SerializeField] private DoubleTouchScaleListener touchScaleListener;
    [SerializeField] private BoxCollider2D doorCollider;

    private Vector3 _actorBaseScale;

    private void Awake()
    {
        if (actor != null)
           _actorBaseScale = Vector3.one;

        if (touchScaleListener != null)
            touchScaleListener.SetScale(1f);

        touchScaleListener.Execute();
    }

    private void Update()
    {
        if (touchScaleListener == null)
            return;

        float scale = touchScaleListener.Scale;

        
        // Debug.Log("scale: " + scale);
        float t = Mathf.Clamp01(1 - scale);

        // Actor localScale 按照 Range 进行缩放（基于初始 localScale 的乘数）
        if (actor != null)
        {
            float scaleFactor = Mathf.Lerp(actorScaleRange.x, actorScaleRange.y, t);
            actor.transform.localScale = _actorBaseScale * scaleFactor;
        }

        // MainCamera orthographicSize 按照 Range 进行绝对值映射
        if (mainCamera != null)
        {
            mainCamera.orthographicSize = Mathf.Lerp(cameraSizeScaleRange.x, cameraSizeScaleRange.y, t);
        }

        // DoorCollider 仅在 Scale >= 1 时启用
        if (doorCollider != null)
        {
            doorCollider.enabled = scale >= 1f;
        }

        // 同步设置 Actor 的移动速度和跳跃力
        if (actor != null)
        {
            float targetSpeed = Mathf.Lerp(actorSpeedRange.x, actorSpeedRange.y, t);
            float targetJumpForce = Mathf.Lerp(actorJumpForceRange.x, actorJumpForceRange.y, t);
            actor.SetMoveSpeed(targetSpeed);
            actor.SetJumpForce(targetJumpForce);
        }
    }
}
