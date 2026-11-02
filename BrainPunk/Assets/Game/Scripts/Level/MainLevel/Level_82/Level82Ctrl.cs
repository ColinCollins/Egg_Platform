using System.Collections;
using System.Collections.Generic;
using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 82 专用落石与镜头反馈控制器。
/// </summary>
public sealed class Level82Ctrl : MonoBehaviour
{
    private const string RockFallSfxTag = "l82RockFall";
    private const float SettledLinearSpeedSqr = 0.04f;
    private const float SettledAngularSpeed = 8f;

    #region Inspector

    [Header("场景引用")]
    [Tooltip("运行时石头的父节点；未绑定时仅在 PlayMode 下自动创建。")]
    [SerializeField] private Transform stoneRoot;
    [Tooltip("关卡世界相机，用于计算屏幕外出生点和播放震屏。")]
    [SerializeField] private Camera sceneCamera;

    [Header("石头预制体")]
    [Tooltip("大石头候选，单次生成数量默认高于其他尺寸。")]
    [SerializeField] private GameObject[] largeStonePrefabs;
    [Tooltip("中石头候选，用于填补大石之间的空间。")]
    [SerializeField] private GameObject[] mediumStonePrefabs;
    [Tooltip("小石头候选，用于填补石堆缝隙。")]
    [SerializeField] private GameObject[] smallStonePrefabs;

    [Header("每次落石数量")]
    [Tooltip("每次有效高处落地生成的大石头数量。")]
    [Min(0)] [SerializeField] private int largeStoneCount = 4;
    [Tooltip("每次有效高处落地生成的中石头数量。")]
    [Min(0)] [SerializeField] private int mediumStoneCount = 2;
    [Tooltip("每次有效高处落地生成的小石头数量。")]
    [Min(0)] [SerializeField] private int smallStoneCount = 2;
    [Tooltip("前多少次有效高处落地生成完整的大、中、小石头批次。")]
    [Min(0)] [SerializeField] private int fullStoneWaveLimit = 5;
    [Tooltip("超过完整批次次数后，每次有效高处落地只生成的小石头数量。")]
    [Min(1)] [SerializeField] private int smallOnlyStoneCount = 2;
    [Tooltip("同一批石头的生成间隔，使用游戏时间并随暂停停止。")]
    [Min(0.01f)] [SerializeField] private float spawnInterval = 0.1f;
    [Tooltip("同时参与动态物理的石头软上限；达到后优先把已稳定的旧石头冻结为桥面。")]
    [Min(1)] [SerializeField] private int maxActiveStones = 48;
    [Tooltip("低于该世界 Y 坐标的石头会在下一批生成前清理。")]
    [SerializeField] private float cleanupY = -12f;

    [Header("屏幕外落点")]
    [Tooltip("落石区域在视口水平方向的中心，0.5 表示屏幕中央。")]
    [Range(0f, 1f)] [SerializeField] private float spawnViewportCenterX = 0.5f;
    [Tooltip("落石区域占视口宽度的比例。")]
    [Range(0.05f, 1f)] [SerializeField] private float spawnViewportWidth = 0.34f;
    [Tooltip("落石出生点的视口 Y，必须大于等于 1 才位于屏幕上方。")]
    [Min(1f)] [SerializeField] private float spawnViewportY = 1.1f;
    [Tooltip("石头生成时的随机水平速度范围。")]
    [SerializeField] private Vector2 initialHorizontalSpeed = new Vector2(-0.7f, 0.7f);
    [Tooltip("石头生成时的随机垂直速度范围，负值表示向下。")]
    [SerializeField] private Vector2 initialVerticalSpeed = new Vector2(-1.2f, -0.4f);
    [Tooltip("石头生成时的随机角速度范围。")]
    [SerializeField] private Vector2 initialAngularSpeed = new Vector2(-75f, 75f);

    [Header("落地反馈")]
    [Tooltip("低高度落地的最小震屏强度。")]
    [Min(0f)] [SerializeField] private float minCameraShakeStrength = 0.1f;
    [Tooltip("高处重落地的最大震屏强度。")]
    [Min(0f)] [SerializeField] private float maxCameraShakeStrength = 0.26f;
    [Tooltip("单次震屏持续时间。")]
    [Min(0.01f)] [SerializeField] private float cameraShakeDuration = 0.28f;
    [Tooltip("单次震屏的振动次数。")]
    [Min(1)] [SerializeField] private int cameraShakeVibrato = 18;

    #endregion

    #region Runtime State

    private readonly List<GameObject> activeStones = new();
    private Tween cameraShakeTween;
    private Vector3 cameraInitialLocalPosition;
    private bool hasCameraInitialPosition;
    private bool attemptBlocked;
    private int triggeredStoneWaveCount;
    private EventSubscriber eventSubscriber;
    private Level82DoorDropCtrl doorDropCtrl;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        attemptBlocked = false;
        triggeredStoneWaveCount = 0;
        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<OnTriggerTrapEvent>(OnTriggerTrap);
        eventSubscriber.Subscribe<OnTriggerFailAreaEvent>(OnTriggerFailArea);
        eventSubscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref eventSubscriber);
        MarkAttemptEnding();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref eventSubscriber);
    }

    #endregion

    #region Door Impact

    /// <summary>
    /// 入口门完成一次落地时调用。达到高度门槛的落地会额外生成一批石头。
    /// </summary>
    public void OnDoorLanded(float strength01, bool shouldSpawnStones)
    {
        if (!IsGameplayRunning())
            return;

        PlayImpactFeedback(strength01);

        if (shouldSpawnStones)
        {
            bool spawnFullWave = triggeredStoneWaveCount < Mathf.Max(0, fullStoneWaveLimit);
            triggeredStoneWaveCount++;
            StartCoroutine(SpawnStoneWave(spawnFullWave));
        }
    }

    /// <summary>
    /// 当前尝试是否已经因死亡、掉坑或重置而结束；入口门据此立即停止交互。
    /// </summary>
    public bool IsAttemptBlocked => attemptBlocked;

    #endregion

    #region Stone Spawning

    private IEnumerator SpawnStoneWave(bool spawnFullWave)
    {
        CleanupFallenStones();
        AudioManager.PlaySound(RockFallSfxTag);

        if (!spawnFullWave)
        {
            yield return SpawnGroup(smallStonePrefabs, smallOnlyStoneCount, 1f);
            yield break;
        }

        yield return SpawnGroup(largeStonePrefabs, largeStoneCount, 0.82f);
        yield return SpawnGroup(mediumStonePrefabs, mediumStoneCount, 0.95f);
        yield return SpawnGroup(smallStonePrefabs, smallStoneCount, 1f);
    }

    private IEnumerator SpawnGroup(GameObject[] prefabs, int count, float spreadMultiplier)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0)
            yield break;

        for (int index = 0; index < count; index++)
        {
            yield return WaitForGameplay();

            RemoveMissingStoneEntries();
            MaintainDynamicStoneBudget();

            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab != null)
                SpawnStone(prefab, spreadMultiplier);

            yield return WaitForGameplaySeconds(Mathf.Max(0.01f, spawnInterval));
        }
    }

    private IEnumerator WaitForGameplay()
    {
        while (!IsGameplayRunning())
            yield return null;
    }

    private IEnumerator WaitForGameplaySeconds(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsGameplayRunning())
                elapsed += Time.deltaTime;

            yield return null;
        }
    }

    private bool IsGameplayRunning()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        LevelRuntimeState runtimeState = playCtrl?.Level?.CurrentLevelState;
        return !attemptBlocked &&
               playCtrl != null &&
               runtimeState != null &&
               !runtimeState.IsAttemptTransitioning &&
               playCtrl.LevelCtrl != null &&
               playCtrl.LevelCtrl.Actor != null &&
               !playCtrl.LevelCtrl.Actor.IsDied &&
               !playCtrl.LevelCtrl.IsActorEnterDoor &&
               playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    private void SpawnStone(GameObject prefab, float spreadMultiplier)
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;

        if (sceneCamera == null || prefab == null)
            return;

        float halfWidth = Mathf.Clamp01(spawnViewportWidth) * 0.5f * Mathf.Max(0f, spreadMultiplier);
        float viewportX = Random.Range(spawnViewportCenterX - halfWidth, spawnViewportCenterX + halfWidth);
        float zPlane = stoneRoot != null ? stoneRoot.position.z : transform.position.z;
        float zDistance = Mathf.Abs(sceneCamera.transform.position.z - zPlane);
        Vector3 spawnPosition = sceneCamera.ViewportToWorldPoint(
            new Vector3(viewportX, Mathf.Max(1f, spawnViewportY), zDistance));
        spawnPosition.z = zPlane;

        Quaternion rotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
        GameObject stone = Instantiate(prefab, spawnPosition, rotation, stoneRoot);
        activeStones.Add(stone);

        Rigidbody2D body = stone.GetComponent<Rigidbody2D>();
        if (body == null)
            return;

        body.linearVelocity = new Vector2(
            Random.Range(initialHorizontalSpeed.x, initialHorizontalSpeed.y),
            Random.Range(initialVerticalSpeed.x, initialVerticalSpeed.y));
        body.angularVelocity = Random.Range(initialAngularSpeed.x, initialAngularSpeed.y);
    }

    private void CleanupFallenStones()
    {
        for (int index = activeStones.Count - 1; index >= 0; index--)
        {
            GameObject stone = activeStones[index];
            if (stone == null)
            {
                activeStones.RemoveAt(index);
                continue;
            }

            if (stone.transform.position.y >= cleanupY)
                continue;

            activeStones.RemoveAt(index);
            Destroy(stone);
        }
    }

    private void RemoveMissingStoneEntries()
    {
        for (int index = activeStones.Count - 1; index >= 0; index--)
        {
            if (activeStones[index] == null)
                activeStones.RemoveAt(index);
        }
    }

    private void MaintainDynamicStoneBudget()
    {
        int dynamicStoneLimit = Mathf.Max(1, maxActiveStones);
        int dynamicStoneCount = 0;
        for (int index = 0; index < activeStones.Count; index++)
        {
            Rigidbody2D body = activeStones[index] != null
                ? activeStones[index].GetComponent<Rigidbody2D>()
                : null;
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
                dynamicStoneCount++;
        }

        while (dynamicStoneCount >= dynamicStoneLimit)
        {
            bool frozeSettledStone = false;
            for (int index = 0; index < activeStones.Count; index++)
            {
                Rigidbody2D body = activeStones[index] != null
                    ? activeStones[index].GetComponent<Rigidbody2D>()
                    : null;
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic)
                    continue;

                bool isSettled = body.IsSleeping() ||
                                 (body.linearVelocity.sqrMagnitude <= SettledLinearSpeedSqr &&
                                  Mathf.Abs(body.angularVelocity) <= SettledAngularSpeed);
                if (!isSettled)
                    continue;

                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.bodyType = RigidbodyType2D.Static;
                body.interpolation = RigidbodyInterpolation2D.None;
                dynamicStoneCount--;
                frozeSettledStone = true;
                break;
            }

            if (!frozeSettledStone)
                break;
        }
    }

    #endregion

    #region Feedback And References

    private void PlayImpactFeedback(float strength01)
    {
        AudioManager.PlaySound("crash");

        if (sceneCamera == null)
            sceneCamera = Camera.main;

        if (sceneCamera == null)
            return;

        if (!hasCameraInitialPosition)
        {
            cameraInitialLocalPosition = sceneCamera.transform.localPosition;
            hasCameraInitialPosition = true;
        }

        if (cameraShakeTween != null && cameraShakeTween.IsActive())
            cameraShakeTween.Kill();

        sceneCamera.transform.localPosition = cameraInitialLocalPosition;
        float strength = Mathf.Lerp(
            Mathf.Max(0f, minCameraShakeStrength),
            Mathf.Max(minCameraShakeStrength, maxCameraShakeStrength),
            Mathf.Clamp01(strength01));

        cameraShakeTween = sceneCamera.transform
            .DOShakePosition(
                Mathf.Max(0.01f, cameraShakeDuration),
                strength,
                Mathf.Max(1, cameraShakeVibrato),
                90f,
                false,
                true)
            .SetLink(gameObject)
            .OnComplete(RestoreCameraPosition)
            .OnKill(() => cameraShakeTween = null);
    }

    private void ResolveReferences()
    {
        if (stoneRoot == null)
            stoneRoot = transform.Find("StoneRoot");

        if (stoneRoot == null)
        {
            GameObject root = new GameObject("StoneRoot");
            stoneRoot = root.transform;
            stoneRoot.SetParent(transform, false);
        }

        if (sceneCamera == null)
            sceneCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        if (doorDropCtrl == null)
            doorDropCtrl = GetComponentInChildren<Level82DoorDropCtrl>(true);

        if (sceneCamera != null)
        {
            cameraInitialLocalPosition = sceneCamera.transform.localPosition;
            hasCameraInitialPosition = true;
        }
    }

    private void RestoreCameraPosition()
    {
        if (sceneCamera != null && hasCameraInitialPosition)
            sceneCamera.transform.localPosition = cameraInitialLocalPosition;

        cameraShakeTween = null;
    }

    private void OnTriggerTrap(OnTriggerTrapEvent _)
    {
        MarkAttemptEnding();
    }

    private void OnTriggerFailArea(OnTriggerFailAreaEvent _)
    {
        MarkAttemptEnding();
    }

    private void OnGameReset(GameResetEvent _)
    {
        MarkAttemptEnding();
    }

    private void MarkAttemptEnding()
    {
        if (attemptBlocked)
            return;

        attemptBlocked = true;
        triggeredStoneWaveCount = 0;
        StopAllCoroutines();

        if (cameraShakeTween != null && cameraShakeTween.IsActive())
            cameraShakeTween.Kill();

        RestoreCameraPosition();
        doorDropCtrl?.BlockForAttemptEnd();
    }

    #endregion
}
