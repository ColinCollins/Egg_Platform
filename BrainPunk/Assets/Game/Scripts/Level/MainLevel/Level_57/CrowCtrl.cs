using System.Collections.Generic;
using Game.Scripts.Common;
using UnityEngine;


/// <summary>
/// 涔岄甫鎺у埗锛岃礋璐ｇ偣鍑汇€佹湞鍚戝拰鎺夎惤鐗╃敓鎴愩€?
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ShitPoolCtrl))]
public class CrowCtrl : MonoBehaviour
{
    private const float DefaultGoldShitChance = 0.1f;

    [Header("Crow")]
    [SerializeField] private ActorSpineCtrl crowSpineCtrl;
    [SerializeField] private Transform shitNormal;
    [SerializeField] private Transform shitGold;
    [SerializeField, Range(0f, 1f)] private float goldShitChance = DefaultGoldShitChance;
    [SerializeField] private Vector3 shitSpawnOffset;
    [SerializeField] private Transform shitSpawnParent;
    [SerializeField] private string crowHitAnimName = "corw_hit";
    [SerializeField] private string crowFlyAnimName = "corw_fly";

    private Transform _crowTrackedTransform;
    private Vector3 _crowLastPosition;
    private ShitPoolCtrl _shitPoolCtrl;
    private bool _isCrowHitPlaying;
    private Vector3 _initialShitSpawnOffset;

    private void Start()
    {
        _initialShitSpawnOffset = shitSpawnOffset;
        InitializeShitPool();
        InitializeCrowTracking();
    }

    private void Update()
    {
        UpdateCrowFacing();
    }

    private void OnDestroy()
    {
        _shitPoolCtrl?.DestroySpawnedAndUnregisterPool();
    }

    public void ClickCrow(Transform crow, Vector2 worldPoint)
    {
        if (_isCrowHitPlaying)
            return;

        SpawnShit(crow);
        PlayCrowHitThenFly();
    }

    public void ClickCrow(Transform crow)
    {
        ClickCrow(crow, crow != null ? (Vector2)crow.position : Vector2.zero);
    }

    private void InitializeCrowTracking()
    {
        if (crowSpineCtrl == null)
            return;

        _crowTrackedTransform = crowSpineCtrl.transform.parent != null
            ? crowSpineCtrl.transform.parent
            : crowSpineCtrl.transform;
        _crowLastPosition = _crowTrackedTransform.position;
        UpdateShitSpawnOffsetFacing(crowSpineCtrl.transform.localScale.x);
    }

    private void UpdateCrowFacing()
    {
        if (crowSpineCtrl == null || _crowTrackedTransform == null)
            return;

        Vector3 currentPos = _crowTrackedTransform.position;
        float deltaX = currentPos.x - _crowLastPosition.x;

        if (Mathf.Abs(deltaX) > 0.001f)
        {
            Vector3 scale = crowSpineCtrl.transform.localScale;
            scale.x = Mathf.Sign(deltaX) * Mathf.Abs(scale.x);
            crowSpineCtrl.transform.localScale = scale;
            UpdateShitSpawnOffsetFacing(scale.x);
        }

        _crowLastPosition = currentPos;
    }

    private void UpdateShitSpawnOffsetFacing(float facingX)
    {
        float offsetX = Mathf.Abs(_initialShitSpawnOffset.x);
        if (Mathf.Approximately(offsetX, 0f))
            return;

        shitSpawnOffset.x = facingX >= 0f ? -offsetX : offsetX;
        shitSpawnOffset.y = _initialShitSpawnOffset.y;
        shitSpawnOffset.z = _initialShitSpawnOffset.z;
    }

    private void InitializeShitPool()
    {
        if (_shitPoolCtrl == null)
            _shitPoolCtrl = GetComponent<ShitPoolCtrl>();

        var prefabs = new List<GameObject>();
        if (shitNormal != null)
            prefabs.Add(shitNormal.gameObject);
        if (shitGold != null)
            prefabs.Add(shitGold.gameObject);

        if (prefabs.Count > 0)
            _shitPoolCtrl.SetupPrefabs(prefabs, goldShitChance);
    }

    private void SpawnShit(Transform crow)
    {
        if (_shitPoolCtrl == null)
        {
            Debug.LogWarning("[CrowCtrl] ShitPoolCtrl is not initialized.", this);
            return;
        }

        Transform spawnPoint = crow != null ? crow : transform;
        Vector3 spawnPos = spawnPoint.position + shitSpawnOffset;
        Transform parent = shitSpawnParent != null ? shitSpawnParent : transform;

        _shitPoolCtrl.Spawn(spawnPos, parent);
    }

    private void PlayCrowHitThenFly()
    {
        if (crowSpineCtrl == null)
        {
            Debug.LogWarning("[CrowCtrl] ClickCrow skipped crow animation. crowSpineCtrl is missing.", this);
            return;
        }

        if (_isCrowHitPlaying)
            return;

        _isCrowHitPlaying = true;
        Spine.TrackEntry hitEntry = crowSpineCtrl.PlayAnimation(crowHitAnimName, false, false);
        if (hitEntry == null)
        {
            _isCrowHitPlaying = false;
            return;
        }

        AudioManager.PlaySound("crow");

        hitEntry.Complete += OnCrowHitComplete;
    }

    private void OnCrowHitComplete(Spine.TrackEntry trackEntry)
    {
        if (trackEntry != null)
            trackEntry.Complete -= OnCrowHitComplete;

        _isCrowHitPlaying = false;

        if (crowSpineCtrl == null || string.IsNullOrEmpty(crowFlyAnimName))
            return;

        crowSpineCtrl.PlayAnimation(crowFlyAnimName, true, false);
    }
}
