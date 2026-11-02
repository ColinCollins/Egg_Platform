using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level088 私有控制器：累计敲门次数，切换三段破损表现，并在完全破碎后开放出口。
/// </summary>
public sealed class Level88Ctrl : MonoBehaviour
{
    private const int DoorStageCount = 4;

    [Header("Door References")]
    [Tooltip("玩家敲门时必须命中的独立碰撞区域。")]
    [SerializeField] private Collider2D knockArea;

    [Tooltip("显示完整门和三段破损贴图的 SpriteRenderer。")]
    [SerializeField] private SpriteRenderer doorStageRenderer;

    [Tooltip("按完整、破损一、破损二、完全破碎的顺序配置四张门贴图。")]
    [SerializeField] private Sprite[] doorStages = new Sprite[DoorStageCount];

    [Tooltip("完全破碎后启用的 door_0 成功触发器。")]
    [SerializeField] private Collider2D successDoorCollider;

    [Tooltip("每次有效敲门时在点击位置播放的反馈粒子。")]
    [SerializeField] private ParticleSystem knockVfx;

    [Header("Break Thresholds")]
    [Tooltip("第一次出现裂纹所需的累计敲击次数。")]
    [SerializeField, Min(1)] private int firstBreakKnockCount = 15;

    [Tooltip("第二次加深裂纹所需的累计敲击次数。")]
    [SerializeField, Min(2)] private int secondBreakKnockCount = 20;

    [Tooltip("门完全破碎并开放出口所需的累计敲击次数。")]
    [SerializeField, Min(3)] private int finalBreakKnockCount = 30;

    [Header("Break VFX")]
    [Tooltip("复用 Level075 的四向门碎片粒子 Prefab。")]
    [SerializeField] private ParticleSystem doorBreakVfxPrefab;

    [Tooltip("第一次破损发射的主碎片数量。")]
    [SerializeField, Min(0)] private int firstBreakShardCount = 4;

    [Tooltip("第二次破损发射的主碎片数量。")]
    [SerializeField, Min(0)] private int secondBreakShardCount = 5;

    [Tooltip("完全破碎发射的主碎片数量。")]
    [SerializeField, Min(0)] private int finalBreakShardCount = 18;

    private Camera worldCamera;
    private int knockCount;
    private bool doorOpened;

    private void Awake()
    {
        ResolveCamera();
        InitializeDoor();
        ValidateConfiguration();
    }

    private void Update()
    {
        if (doorOpened || !IsGameplayPlaying())
            return;

        ResolveCamera();
        if (worldCamera == null || knockArea == null)
            return;

        if (!TryGetKnockScreenPosition(out Vector2 screenPosition))
            return;

        HandleKnock(screenPosition);
    }

    private void HandleKnock(Vector2 screenPosition)
    {
        PlayKnockVfx(screenPosition);
        knockCount++;

        if (knockCount == firstBreakKnockCount)
        {
            ApplyBreakStage(1, firstBreakShardCount, false);
            return;
        }

        if (knockCount == secondBreakKnockCount)
        {
            ApplyBreakStage(2, secondBreakShardCount, false);
            return;
        }

        if (knockCount == finalBreakKnockCount)
        {
            ApplyBreakStage(3, finalBreakShardCount, true);
            return;
        }

        AudioManager.PlaySound("knockDoor");
    }

    private void ApplyBreakStage(
        int stageIndex,
        int shardCount,
        bool openDoor)
    {
        SetDoorStage(stageIndex);
        PlayDoorBreakVfx(shardCount);
        AudioManager.PlaySound("blockByDoor");

        if (!openDoor)
            return;

        doorOpened = true;
        if (knockArea != null)
            knockArea.enabled = false;

        if (successDoorCollider != null)
        {
            successDoorCollider.isTrigger = true;
            successDoorCollider.enabled = true;
        }
    }

    private void SetDoorStage(int stageIndex)
    {
        if (doorStageRenderer == null ||
            doorStages == null ||
            stageIndex < 0 ||
            stageIndex >= doorStages.Length ||
            doorStages[stageIndex] == null)
        {
            return;
        }

        doorStageRenderer.sprite = doorStages[stageIndex];
    }

    private void PlayKnockVfx(Vector2 screenPosition)
    {
        if (knockVfx == null)
            return;

        Vector3 effectPosition = worldCamera.ScreenToWorldPoint(screenPosition);
        effectPosition.z = 0f;
        knockVfx.transform.position = effectPosition;
        if (!knockVfx.gameObject.activeSelf)
            knockVfx.gameObject.SetActive(true);

        knockVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        knockVfx.Play(true);
    }

    private void PlayDoorBreakVfx(int shardCount)
    {
        if (doorBreakVfxPrefab == null)
            return;

        Vector3 effectPosition = doorStageRenderer != null
            ? doorStageRenderer.bounds.center
            : transform.position;
        ParticleSystem instance = Instantiate(
            doorBreakVfxPrefab,
            effectPosition,
            doorBreakVfxPrefab.transform.rotation,
            transform);

        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < systems.Length; index++)
        {
            ParticleSystem system = systems[index];
            if (system == instance)
            {
                SetFirstBurstCount(system, shardCount);
            }
            else if (system.name == "Clouds")
            {
                system.gameObject.SetActive(false);
            }
        }

        instance.gameObject.SetActive(true);
        instance.Play(true);
    }

    private static void SetFirstBurstCount(ParticleSystem system, int count)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        if (emission.burstCount <= 0)
            return;

        ParticleSystem.Burst burst = emission.GetBurst(0);
        burst.count = new ParticleSystem.MinMaxCurve(Mathf.Max(0, count));
        emission.SetBurst(0, burst);
    }

    private bool TryGetKnockScreenPosition(out Vector2 screenPosition)
    {
        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.phase != TouchPhase.Began || InputUtils.IsPointerOverUI(touch.position))
                continue;

            if (IsInsideKnockArea(touch.position))
            {
                screenPosition = touch.position;
                return true;
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0) &&
            !InputUtils.IsPointerOverUI(Input.mousePosition) &&
            IsInsideKnockArea(Input.mousePosition))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = Vector2.zero;
        return false;
    }

    private bool IsInsideKnockArea(Vector2 screenPosition)
    {
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
        return knockArea.OverlapPoint(worldPosition);
    }

    private void ResolveCamera()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void InitializeDoor()
    {
        knockCount = 0;
        doorOpened = false;
        SetDoorStage(0);

        if (knockArea != null)
            knockArea.enabled = true;

        if (successDoorCollider != null)
            successDoorCollider.enabled = false;

        if (knockVfx != null)
            knockVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ValidateConfiguration()
    {
        if (knockArea == null ||
            doorStageRenderer == null ||
            successDoorCollider == null ||
            knockVfx == null ||
            doorBreakVfxPrefab == null)
        {
            Debug.LogError("[Level88Ctrl] One or more required Level088 references are missing.", this);
        }

        if (doorStages == null || doorStages.Length != DoorStageCount)
        {
            Debug.LogError($"[Level88Ctrl] Door stage array must contain exactly {DoorStageCount} sprites.", this);
        }
        else
        {
            for (int index = 0; index < doorStages.Length; index++)
            {
                if (doorStages[index] == null)
                    Debug.LogError($"[Level88Ctrl] Door stage {index} is missing.", this);
            }
        }

        if (firstBreakKnockCount < 1 ||
            secondBreakKnockCount <= firstBreakKnockCount ||
            finalBreakKnockCount <= secondBreakKnockCount)
        {
            Debug.LogError("[Level88Ctrl] Break thresholds must be strictly increasing.", this);
        }
    }

    private static bool IsGameplayPlaying()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        return playCtrl != null &&
               playCtrl.IsInitialized &&
               playCtrl.IsActive &&
               playCtrl.CheckState(GamePlayStateName.PLAYING);
    }
}
