using Game.Level;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level 82 入口门拖放、释放高度和单次落地判定。
/// </summary>
public sealed class Level82DoorDropCtrl : MonoBehaviour
{
    #region Inspector

    [Header("引用")]
    [Tooltip("Level 82 专用落石控制器。")]
    [SerializeField] private Level82Ctrl levelCtrl;
    [Tooltip("当前关卡基础控制器，用于确认开场动画已结束且角色可控制。")]
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;
    [Tooltip("入口门复用的公共拖拽输入组件，仅在本脚本允许时启用。")]
    [SerializeField] private DragableItem dragableItem;
    [Tooltip("入口门专用刚体，开场期间不参与物理模拟。")]
    [SerializeField] private Rigidbody2D doorRigidbody;
    [Tooltip("入口门的实体碰撞体；原有开门 Trigger 必须另外保留。")]
    [SerializeField] private BoxCollider2D physicalCollider;

    [Header("拖拽与落地")]
    [Tooltip("入口门允许拖拽的世界坐标区域中心。")]
    [SerializeField] private Vector2 dragAreaCenter = new Vector2(0f, 1.5f);
    [Tooltip("入口门允许拖拽的世界坐标区域大小。")]
    [SerializeField] private Vector2 dragAreaSize = new Vector2(19f, 8f);
    [Tooltip("只有碰到这些层才算一次有效落地；默认仅 Ground。")]
    [SerializeField] private LayerMask landingLayers = 1 << 6;
    [Tooltip("释放时门的世界 Y 达到该值，本次落地才会生成石头。")]
    [SerializeField] private float releaseHeightThreshold = 1.5f;
    [Tooltip("达到该释放高度后采用最大高度震屏权重。")]
    [SerializeField] private float fullStrengthHeight = 5f;
    [Tooltip("达到该相对碰撞速度后采用最大冲击震屏权重。")]
    [SerializeField] private float fullStrengthImpactSpeed = 9f;
    [Tooltip("入口门释放后的重力倍率。")]
    [SerializeField] private float gravityScale = 1.8f;
    [Tooltip("入口门低于该世界 Y 坐标时，回到最近一次安全落地点，避免掉入沟壑后无法再次拖拽。")]
    [SerializeField] private float fallRecoveryY = -9f;

    #endregion

    #region Runtime State

    private bool gameplayEnabled;
    private bool gameplayStarted;
    private bool isDragging;
    private bool isWaitingForLanding;
    private bool shouldSpawnOnLanding;
    private float releaseHeight;
    private Vector3 lastSafePosition;
    private Quaternion lastSafeRotation;
    private bool hasSafePose;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        RememberSafePose();
        PrepareForIntro();
    }

    private void Update()
    {
        if (isWaitingForLanding && transform.position.y < fallRecoveryY)
            RecoverDoorFromFall();

        bool shouldEnable = CanEnableGameplayDrag();
        if (gameplayEnabled != shouldEnable)
            SetGameplayEnabled(shouldEnable);
    }

    private void OnDisable()
    {
        gameplayEnabled = false;
        gameplayStarted = false;
        dragableItem?.SetEnableDrag(false);
        HoldDoorInPlace();

        if (doorRigidbody != null)
            doorRigidbody.simulated = false;

        if (physicalCollider != null)
            physicalCollider.enabled = false;
    }

    #endregion

    #region Drag And Landing

    /// <summary>
    /// 由 DragableItem 的开始拖拽事件调用。
    /// </summary>
    public void BeginDoorDrag()
    {
        if (!gameplayEnabled || !CanEnableGameplayDrag() ||
            doorRigidbody == null || physicalCollider == null)
            return;

        isDragging = true;
        isWaitingForLanding = false;
        shouldSpawnOnLanding = false;

        physicalCollider.enabled = false;
        doorRigidbody.simulated = true;
        doorRigidbody.bodyType = RigidbodyType2D.Kinematic;
        doorRigidbody.linearVelocity = Vector2.zero;
        doorRigidbody.angularVelocity = 0f;
    }

    /// <summary>
    /// 由 DragableItem 的结束拖拽事件调用。
    /// </summary>
    public void EndDoorDrag()
    {
        if (!isDragging)
            return;

        isDragging = false;
        if (!gameplayEnabled || !CanEnableGameplayDrag() ||
            doorRigidbody == null || physicalCollider == null)
        {
            HoldDoorInPlace();
            return;
        }

        releaseHeight = transform.position.y;
        shouldSpawnOnLanding = releaseHeight >= releaseHeightThreshold;
        isWaitingForLanding = true;

        physicalCollider.enabled = true;
        doorRigidbody.simulated = true;
        doorRigidbody.bodyType = RigidbodyType2D.Dynamic;
        doorRigidbody.gravityScale = Mathf.Max(0f, gravityScale);
        doorRigidbody.linearVelocity = Vector2.zero;
        doorRigidbody.angularVelocity = 0f;
    }

    /// <summary>
    /// 本次尝试结束时立即关闭入口门拖拽，并停止正在进行的坠落判定。
    /// </summary>
    public void BlockForAttemptEnd()
    {
        gameplayEnabled = false;
        dragableItem?.SetEnableDrag(false);
        HoldDoorInPlace();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleLanding(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryHandleLanding(collision);
    }

    private void TryHandleLanding(Collision2D collision)
    {
        if (!gameplayEnabled || !CanEnableGameplayDrag() ||
            !isWaitingForLanding || collision == null)
            return;

        if ((landingLayers.value & (1 << collision.gameObject.layer)) == 0)
            return;

        if (!HasSupportingContact(collision))
            return;

        isWaitingForLanding = false;
        bool spawnStones = shouldSpawnOnLanding;
        shouldSpawnOnLanding = false;
        RememberSafePose();

        float heightStrength = Mathf.InverseLerp(
            releaseHeightThreshold,
            Mathf.Max(releaseHeightThreshold + 0.01f, fullStrengthHeight),
            releaseHeight);
        float impactStrength = Mathf.Clamp01(
            collision.relativeVelocity.magnitude / Mathf.Max(0.01f, fullStrengthImpactSpeed));

        if (levelCtrl != null)
            levelCtrl.OnDoorLanded(Mathf.Max(heightStrength, impactStrength), spawnStones);
    }

    private static bool HasSupportingContact(Collision2D collision)
    {
        for (int index = 0; index < collision.contactCount; index++)
        {
            if (collision.GetContact(index).normal.y > 0.25f)
                return true;
        }

        return false;
    }

    #endregion

    #region State And References

    private bool CanEnableGameplayDrag()
    {
        if (!Application.isPlaying || baseLevelCtrl == null || PlayCtrl.Instance == null)
            return false;

        if (levelCtrl != null && levelCtrl.IsAttemptBlocked)
            return false;

        LevelRuntimeState runtimeState = PlayCtrl.Instance.Level?.CurrentLevelState;
        return runtimeState != null &&
               !runtimeState.IsAttemptTransitioning &&
               baseLevelCtrl.Actor != null &&
               !baseLevelCtrl.Actor.IsDied &&
               !baseLevelCtrl.IsActorEnterDoor &&
               baseLevelCtrl.ActorCtrlable &&
               PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }

    private void SetGameplayEnabled(bool enabled)
    {
        gameplayEnabled = enabled;

        if (enabled)
        {
            gameplayStarted = true;
            if (doorRigidbody != null)
            {
                doorRigidbody.simulated = true;
                if (!isWaitingForLanding)
                {
                    doorRigidbody.bodyType = RigidbodyType2D.Kinematic;
                    doorRigidbody.linearVelocity = Vector2.zero;
                    doorRigidbody.angularVelocity = 0f;
                }
            }

            if (physicalCollider != null)
                physicalCollider.enabled = true;

            dragableItem?.SetEnableDrag(true);
            return;
        }

        dragableItem?.SetEnableDrag(false);
        HoldDoorInPlace();
    }

    private void PrepareForIntro()
    {
        gameplayEnabled = false;
        gameplayStarted = false;
        isDragging = false;
        isWaitingForLanding = false;
        shouldSpawnOnLanding = false;

        if (doorRigidbody != null)
        {
            doorRigidbody.bodyType = RigidbodyType2D.Kinematic;
            doorRigidbody.linearVelocity = Vector2.zero;
            doorRigidbody.angularVelocity = 0f;
            doorRigidbody.simulated = false;
        }

        if (physicalCollider != null)
            physicalCollider.enabled = false;

        if (dragableItem != null)
        {
            dragableItem.SetDragArea(dragAreaCenter, dragAreaSize);
            dragableItem.SetEnableDrag(false);
        }
    }

    private void HoldDoorInPlace()
    {
        isDragging = false;
        isWaitingForLanding = false;
        shouldSpawnOnLanding = false;

        if (doorRigidbody != null)
        {
            doorRigidbody.bodyType = RigidbodyType2D.Kinematic;
            doorRigidbody.linearVelocity = Vector2.zero;
            doorRigidbody.angularVelocity = 0f;
            doorRigidbody.simulated = gameplayStarted;
        }

        if (physicalCollider != null)
            physicalCollider.enabled = gameplayStarted;
    }

    private void RememberSafePose()
    {
        lastSafePosition = transform.position;
        lastSafeRotation = transform.rotation;
        hasSafePose = true;
    }

    private void RecoverDoorFromFall()
    {
        isDragging = false;
        isWaitingForLanding = false;
        shouldSpawnOnLanding = false;

        if (doorRigidbody != null)
        {
            doorRigidbody.bodyType = RigidbodyType2D.Kinematic;
            doorRigidbody.linearVelocity = Vector2.zero;
            doorRigidbody.angularVelocity = 0f;
            doorRigidbody.simulated = gameplayStarted;
        }

        if (hasSafePose)
            transform.SetPositionAndRotation(lastSafePosition, lastSafeRotation);

        if (physicalCollider != null)
            physicalCollider.enabled = gameplayStarted;

        dragableItem?.SetEnableDrag(gameplayEnabled);
    }

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level82Ctrl>();

        if (baseLevelCtrl == null)
            baseLevelCtrl = GetComponentInParent<BaseLevelCtrl>();

        if (dragableItem == null)
            dragableItem = GetComponent<DragableItem>();

        if (doorRigidbody == null)
            doorRigidbody = GetComponent<Rigidbody2D>();

        if (physicalCollider == null)
        {
            BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (!colliders[index].isTrigger)
                {
                    physicalCollider = colliders[index];
                    break;
                }
            }
        }

        if (levelCtrl == null || baseLevelCtrl == null || dragableItem == null ||
            doorRigidbody == null || physicalCollider == null)
        {
            Debug.LogWarning("[Level82DoorDropCtrl] 入口门拖放引用不完整，本关门拖拽保持关闭。", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            new Vector3(dragAreaCenter.x - dragAreaSize.x * 0.5f, releaseHeightThreshold, transform.position.z),
            new Vector3(dragAreaCenter.x + dragAreaSize.x * 0.5f, releaseHeightThreshold, transform.position.z));
    }

    #endregion
}
