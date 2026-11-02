using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// Assigns a Spine BoneFollower target by bone name at runtime.
/// Supports both world-space BoneFollower and UI BoneFollowerGraphic.
/// </summary>
public class BoneFollowerAutoBinder : MonoBehaviour
{
    [SerializeField] private BoneFollower boneFollower;
    [SerializeField] private BoneFollowerGraphic boneFollowerGraphic;
    [SerializeField] private SkeletonRenderer skeletonRenderer;
    [SerializeField] private SkeletonGraphic skeletonGraphic;
    [SerializeField] private string defaultBoneName;
    [SerializeField] private bool applyDefaultOnEnable = true;
    [SerializeField] private string editorPreviewBoneName;

    public string CurrentBoneName { get; private set; }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (applyDefaultOnEnable && !string.IsNullOrEmpty(defaultBoneName))
        {
            SetBone(defaultBoneName);
        }
    }

    public bool SetBone(string boneName)
    {
        if (string.IsNullOrEmpty(boneName))
        {
            Debug.LogWarning("[BoneFollowerAutoBinder] Bone name is empty.", this);
            return false;
        }

        ResolveReferences();

        bool assigned = false;
        if (boneFollower != null)
        {
            assigned |= SetBoneFollowerBone(boneFollower, boneName);
        }

        if (boneFollowerGraphic != null)
        {
            assigned |= SetBoneFollowerGraphicBone(boneFollowerGraphic, boneName);
        }

        if (assigned)
        {
            CurrentBoneName = boneName;
        }
        else
        {
            Debug.LogWarning($"[BoneFollowerAutoBinder] Bone not assigned: {boneName}", this);
        }

        return assigned;
    }

    [Button("Apply Editor Preview Bone")]
    private void ApplyEditorPreviewBone()
    {
        string boneName = !string.IsNullOrEmpty(editorPreviewBoneName)
            ? editorPreviewBoneName
            : defaultBoneName;

        if (SetBone(boneName))
        {
            LateUpdateFollowers();
        }
    }

    private void ResolveReferences()
    {
        if (boneFollower == null)
        {
            boneFollower = GetComponent<BoneFollower>();
        }

        if (boneFollowerGraphic == null)
        {
            boneFollowerGraphic = GetComponent<BoneFollowerGraphic>();
        }

        if (skeletonRenderer == null)
        {
            skeletonRenderer = GetComponentInParent<SkeletonRenderer>();
        }

        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponentInParent<SkeletonGraphic>();
        }

        if (boneFollower != null && boneFollower.skeletonRenderer == null && skeletonRenderer != null)
        {
            boneFollower.skeletonRenderer = skeletonRenderer;
        }

        if (boneFollowerGraphic != null && boneFollowerGraphic.skeletonGraphic == null && skeletonGraphic != null)
        {
            boneFollowerGraphic.skeletonGraphic = skeletonGraphic;
        }
    }

    private static bool SetBoneFollowerBone(BoneFollower follower, string boneName)
    {
        if (follower.skeletonRenderer == null)
        {
            Debug.LogWarning("[BoneFollowerAutoBinder] BoneFollower skeletonRenderer is null.", follower);
            return false;
        }

        if (!follower.skeletonRenderer.valid)
        {
            follower.skeletonRenderer.Initialize(false);
        }

        follower.Initialize();
        return follower.SetBone(boneName);
    }

    private static bool SetBoneFollowerGraphicBone(BoneFollowerGraphic follower, string boneName)
    {
        if (follower.skeletonGraphic == null)
        {
            Debug.LogWarning("[BoneFollowerAutoBinder] BoneFollowerGraphic skeletonGraphic is null.", follower);
            return false;
        }

        if (!follower.skeletonGraphic.IsValid)
        {
            follower.skeletonGraphic.Initialize(false);
        }

        follower.Initialize();
        return follower.SetBone(boneName);
    }

    private void LateUpdateFollowers()
    {
        if (boneFollower != null && boneFollower.enabled)
        {
            boneFollower.LateUpdate();
        }

        if (boneFollowerGraphic != null && boneFollowerGraphic.enabled)
        {
            boneFollowerGraphic.LateUpdate();
        }
    }
}
