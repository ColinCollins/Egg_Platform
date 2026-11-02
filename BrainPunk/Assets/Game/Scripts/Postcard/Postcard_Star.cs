using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Config.Game;
using Game.Events;
using GameCommon;
using Sirenix.OdinInspector;
using UnityEngine;
using Game.Scripts.Common;


#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public enum Postcard_StarNum
{
    One = 1,
    Two,
    Three
}

/// <summary>
/// verision 1.0 
/// Trigger 2d 触碰 Player 后，全局 Bag 直接获得一个 star，并把当前 star 的 worldPosition 通过 PostcardGetStarEvent 交给 GamePlayPanel 播放 UI 飞行动画。
/// Awake 的时候检测，Postcard.LevelCollectedStar 数据，判断当前的 index 对象是否已经获取过；编辑器排序功能继续只处理本地 index 配置。
/// </summary>
public class Postcard_Star : MonoBehaviour, IDebuger, IEventSender
{
    [SerializeField] private Postcard_StarNum index;

    #region Runtime Config

    /// <summary>
    /// 星星触发用 Collider2D；为空时运行时从当前节点获取，只在 PlayMode 收集流程中启停。
    /// </summary>
    [SerializeField] private Collider2D triggerCollider;

    /// <summary>
    /// 作为 UI 飞行动画起点的视觉根节点；为空时使用当前 Transform，只读取 worldPosition，不再由场景星星自身执行飞行动画。
    /// </summary>
    [SerializeField] private Transform flyVisualRoot;

    /// <summary>
    /// 收集成功后播放的特效预制体列表；允许同一颗明信片星星同时配置多个粒子特效，只在保存和发奖成功后实例化。
    /// </summary>
    [Tooltip("收集成功后播放的多个粒子特效，按列表顺序同时实例化。")]
    [SerializeField] private List<ParticleSystem> collectionEffects = new List<ParticleSystem>();

    /// <summary>
    /// 旧版单特效绑定兼容字段；仅用于未迁移 prefab 的隐藏兜底，不作为新配置入口。
    /// </summary>
    [SerializeField, HideInInspector] private ParticleSystem expolsion;

    #endregion

    #region Runtime State

    private bool isCollected;
    private bool isCollecting;

    private enum CollectionWriteResult
    {
        SavedAndRewarded,
        AlreadyCollected,
        Failed
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        CacheRuntimeRefs();
        ApplySavedCollectionState();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Application.isPlaying || isCollected || isCollecting)
            return;

        if (!IsPlayer(other))
            return;

        Collect();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 根据当前关卡存档刷新 Star 的可收集状态。
    /// 调用边界：仅供关卡专用逻辑在延迟显示 Star 前复用既有去重判断。
    /// 注意事项：该方法只读取既有收集状态，不发奖、不写存档、不派发收集事件。
    /// </summary>
    public bool RefreshSavedCollectionState()
    {
        if (!Application.isPlaying)
            return false;

        CacheRuntimeRefs();
        ApplySavedCollectionState();
        return !isCollected;
    }

    #endregion

    #region Collection Flow

    private void Collect()
    {
        isCollecting = true;
        SetTriggerEnabled(false);

        if (!TryGetCurrentLevelKey(out string levelKey))
        {
            this.LogWarning("[Postcard_Star] Collect skipped because current level id is missing.");
            isCollecting = false;
            SetTriggerEnabled(true);
            return;
        }

        int starIndex = ResolveStarIndex();
        if (IsSavedCollected(levelKey, starIndex))
        {
            ApplyCollectedState();
            return;
        }

        CollectionWriteResult writeResult = TryCollectStarAndReward(levelKey, starIndex);
        if (writeResult == CollectionWriteResult.AlreadyCollected)
        {
            ApplyCollectedState();
            return;
        }

        if (writeResult == CollectionWriteResult.Failed)
        {
            isCollecting = false;
            SetTriggerEnabled(true);
            return;
        }

        isCollected = true;
        this.DispatchEvent(Witness<PostcardGetStarEvent>._, ResolveCollectionWorldPosition(), false);
        ApplyCollectedState();

        PlayCollectionEffects();

        AudioManager.PlaySound("getStar");
    }

    private CollectionWriteResult TryCollectStarAndReward(string levelKey, int starIndex)
    {
        if (GameManager.Instance == null || GameManager.Instance.Bag == null)
        {
            this.LogWarning("[Postcard_Star] Collect skipped because GameManager or Bag is missing.");
            return CollectionWriteResult.Failed;
        }

        PostcardManager postcardManager = GameManager.Instance?.Postcard;
        if (postcardManager == null)
        {
            this.LogWarning("[Postcard_Star] Collect skipped because PostcardManager is missing.");
            return CollectionWriteResult.Failed;
        }

        if (!postcardManager.TryCollectLevelStar(levelKey, starIndex, out bool isNewCollection))
        {
            this.LogWarning("[Postcard_Star] TryCollectLevelStar returned false, collection aborted.");
            return CollectionWriteResult.Failed;
        }

        if (!isNewCollection)
            return CollectionWriteResult.AlreadyCollected;

        GameManager.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other);
        return CollectionWriteResult.SavedAndRewarded;
    }

    private void ApplySavedCollectionState()
    {
        if (!TryGetCurrentLevelKey(out string levelKey))
        {
            RestoreAvailableState();
            return;
        }

        if (IsSavedCollected(levelKey, ResolveStarIndex()))
            ApplyCollectedState();
        else
            RestoreAvailableState();
    }

    private void ApplyCollectedState()
    {
        isCollected = true;
        isCollecting = false;
        SetTriggerEnabled(false);
        gameObject.SetActive(false);
    }

    private void RestoreAvailableState()
    {
        isCollected = false;
        isCollecting = false;
        SetTriggerEnabled(true);
    }

    #endregion

    #region Helpers

    private void CacheRuntimeRefs()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider2D>();

        if (flyVisualRoot == null)
            flyVisualRoot = transform;
    }

    private Transform GetVisualRoot()
    {
        return flyVisualRoot != null ? flyVisualRoot : transform;
    }

    private Vector3 ResolveCollectionWorldPosition()
    {
        Transform visualRoot = GetVisualRoot();
        return visualRoot != null ? visualRoot.position : transform.position;
    }

    private void PlayCollectionEffects()
    {
        bool hasConfiguredEffect = false;
        if (collectionEffects != null)
        {
            for (int i = 0; i < collectionEffects.Count; i++)
            {
                ParticleSystem effectPrefab = collectionEffects[i];
                if (effectPrefab == null)
                    continue;

                InstantiateCollectionEffect(effectPrefab);
                hasConfiguredEffect = true;
            }
        }

        if (!hasConfiguredEffect && expolsion != null)
            InstantiateCollectionEffect(expolsion);
    }

    private IReadOnlyList<ParticleSystem> ResolveArrivalEffectPrefabs()
    {
        if (collectionEffects != null && collectionEffects.Count > 0)
            return collectionEffects;

        if (expolsion != null)
            return new[] { expolsion };

        return null;
    }

    private void InstantiateCollectionEffect(ParticleSystem effectPrefab)
    {
        if (effectPrefab == null)
            return;

        ParticleSystem effect = Instantiate(effectPrefab, transform.parent);
        effect.transform.position = transform.position;
    }

    private void SetTriggerEnabled(bool isEnabled)
    {
        if (triggerCollider != null)
            triggerCollider.enabled = isEnabled;
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        if (other.GetComponentInParent<ActorCtrl>() != null && other.CompareTag("Player"))
            return true;

        return other.CompareTag("Player");
    }

    private bool TryGetCurrentLevelKey(out string levelKey)
    {
        levelKey = string.Empty;
        PlayCtrl playCtrl = PlayCtrl.Instance;
        if (playCtrl == null || playCtrl.Level == null)
            return false;

        Config.LevelData currentLevelData = playCtrl.Level.CurrentLevelData;
        if (currentLevelData == null)
            return false;

        levelKey = currentLevelData.Id;
        return !string.IsNullOrEmpty(levelKey);
    }

    private int ResolveStarIndex()
    {
        int starIndex = (int)index;
        if (starIndex > 0)
            return starIndex;

        this.LogWarning("[Postcard_Star] index is not configured, fallback to One.");
        return (int)Postcard_StarNum.One;
    }

    private bool IsSavedCollected(string levelKey, int starIndex)
    {
        PostcardManager postcardManager = GameManager.Instance?.Postcard;
        if (postcardManager == null)
            return false;

        return postcardManager.IsLevelStarCollected(levelKey, starIndex);
    }

    #endregion

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器按钮入口：在当前关卡根节点内查找全部 Postcard_Star，并按层级顺序分配 StarNum。
    /// 仅供 Inspector/Odin 手动整理配置使用，不应在运行时或批量导入流程中调用。
    /// </summary>
    [Button("Search star")]
    public void SearchAllStarInScene()
    {
        // 遍历当前 level prefab，查看是否有其他的 Star，StarNum 自动排序。如果超出，弹出警告。
        Transform searchRoot = FindLevelSearchRoot();
        Postcard_Star[] stars = searchRoot.GetComponentsInChildren<Postcard_Star>(true);
        Array.Sort(stars, CompareByHierarchyOrder);

        Postcard_StarNum[] starNums = (Postcard_StarNum[])Enum.GetValues(typeof(Postcard_StarNum));
        if (stars.Length > starNums.Length)
        {
            EditorUtility.DisplayDialog(
                "Postcard Star",
                $"当前节点 {searchRoot.name} 下找到 {stars.Length} 个 Postcard_Star，超过可用 StarNum 数量 {starNums.Length}。\n请减少星星数量或扩展 Postcard_StarNum 后再自动排序。",
                "OK");
            return;
        }

        UnityEngine.Object[] undoTargets = new UnityEngine.Object[stars.Length];
        for (int i = 0; i < stars.Length; i++)
        {
            undoTargets[i] = stars[i];
        }

        Undo.RecordObjects(undoTargets, "Sort Postcard Stars");

        int changedCount = 0;
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i].index == starNums[i])
            {
                continue;
            }

            stars[i].index = starNums[i];
            changedCount++;
            EditorUtility.SetDirty(stars[i]);
            if (PrefabUtility.IsPartOfPrefabInstance(stars[i]))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(stars[i]);
            }
        }

        if (changedCount > 0 && searchRoot.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(searchRoot.gameObject.scene);
        }

        Debug.Log($"[Postcard_Star] Sorted {stars.Length} stars under {searchRoot.name}, changed {changedCount}.");
    }

    private Transform FindLevelSearchRoot()
    {
        Transform current = transform;
        Transform levelRoot = null;
        while (current != null)
        {
            if (current.name.StartsWith("Level_", StringComparison.OrdinalIgnoreCase))
            {
                levelRoot = current;
            }

            current = current.parent;
        }

        return levelRoot != null ? levelRoot : transform.root;
    }

    private static int CompareByHierarchyOrder(Postcard_Star left, Postcard_Star right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        List<int> leftPath = BuildSiblingIndexPath(left.transform);
        List<int> rightPath = BuildSiblingIndexPath(right.transform);
        int minCount = Mathf.Min(leftPath.Count, rightPath.Count);
        for (int i = 0; i < minCount; i++)
        {
            int result = leftPath[i].CompareTo(rightPath[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return leftPath.Count.CompareTo(rightPath.Count);
    }

    private static List<int> BuildSiblingIndexPath(Transform target)
    {
        var path = new List<int>();
        while (target != null)
        {
            path.Add(target.GetSiblingIndex());
            target = target.parent;
        }

        path.Reverse();
        return path;
    }
#endif  
}
