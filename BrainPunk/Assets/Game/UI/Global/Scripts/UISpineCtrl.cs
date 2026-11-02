using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 专用 Spine 控制器，基于 SkeletonGraphic。
/// 接口风格参考 ActorSpineCtrl。
/// </summary>
public class UISpineCtrl : MonoBehaviour
{
    [FoldoutGroup("Refs")]
    [SerializeField] private SkeletonGraphic skeletonGraphic;

    [FoldoutGroup("Config")]
    [SpineAnimation]
    [SerializeField] private string defaultAnimName = "";

    [FoldoutGroup("Config")]
    [SerializeField] private bool defaultLoop = true;

    [FoldoutGroup("Test")]
    [SpineAnimation]
    [SerializeField] private string testAnimName = "";

    [FoldoutGroup("Test")]
    [SerializeField] private bool testLoop = true;

    private void Awake()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponentInChildren<SkeletonGraphic>();
        }

        if (skeletonGraphic == null || skeletonGraphic.AnimationState == null)
        {
            Debug.LogWarning("[UISpineCtrl] SkeletonGraphic is null or not initialized.");
            return;
        }

        if (!string.IsNullOrEmpty(defaultAnimName))
        {
            PlayAnimation(defaultAnimName, defaultLoop);
        }
    }

    [FoldoutGroup("Test")]
    [Button("Play Test Animation")]
    private void PlayTestAnimation()
    {
        PlayAnimation(testAnimName, testLoop);
    }

    public string GetCurrentAnimationName()
    {
        if (skeletonGraphic == null || skeletonGraphic.AnimationState == null)
        {
            return string.Empty;
        }

        var trackEntry = skeletonGraphic.AnimationState.GetCurrent(0);
        if (trackEntry == null || trackEntry.Animation == null)
        {
            return string.Empty;
        }

        return trackEntry.Animation.Name;
    }

    public Spine.AnimationState GetAnimationState()
    {
        return skeletonGraphic != null ? skeletonGraphic.AnimationState : null;
    }

    public bool IsPlayingAnimation(string newAnimName)
    {
        if (string.IsNullOrEmpty(newAnimName))
        {
            return false;
        }

        string currentAnimName = GetCurrentAnimationName();
        return string.Equals(currentAnimName, newAnimName, System.StringComparison.OrdinalIgnoreCase);
    }

    public Spine.TrackEntry PlayAnimation(string animName, bool loop = true, bool checkCurrent = true)
    {
        if (skeletonGraphic == null || skeletonGraphic.AnimationState == null)
        {
            Debug.LogWarning("[UISpineCtrl] SkeletonGraphic is null or not initialized.");
            return null;
        }

        if (string.IsNullOrEmpty(animName))
        {
            Debug.LogWarning("[UISpineCtrl] Animation name is empty.");
            return null;
        }

        var currentEntry = skeletonGraphic.AnimationState.GetCurrent(0);

        if (checkCurrent && IsPlayingAnimation(animName))
        {
            return currentEntry;
        }

        return skeletonGraphic.AnimationState.SetAnimation(0, animName, loop);
    }

    public void RevertPlayAnimation(string animName, bool loop = false)
    {
        if (skeletonGraphic == null || skeletonGraphic.AnimationState == null)
        {
            Debug.LogWarning("[UISpineCtrl] SkeletonGraphic is null or not initialized.");
            return;
        }

        if (string.IsNullOrEmpty(animName))
        {
            Debug.LogWarning("[UISpineCtrl] Animation name is empty.");
            return;
        }

        Spine.TrackEntry entry = skeletonGraphic.AnimationState.SetAnimation(0, animName, loop);
        if (entry != null)
        {
            entry.TimeScale = -1f;
        }
    }

    public bool SetSkin(string skinName, bool resetToSetupPose = true)
    {
        if (skeletonGraphic == null || skeletonGraphic.Skeleton == null)
        {
            Debug.LogWarning("[UISpineCtrl] SkeletonGraphic is null or not initialized.");
            return false;
        }

        if (string.IsNullOrEmpty(skinName))
        {
            Debug.LogWarning("[UISpineCtrl] Skin name is empty.");
            return false;
        }

        var skeleton = skeletonGraphic.Skeleton;
        var skin = skeleton.Data.FindSkin(skinName);
        if (skin == null)
        {
            Debug.LogWarning($"[UISpineCtrl] Skin not found: {skinName}");
            return false;
        }

        skeleton.SetSkin(skin);

        if (resetToSetupPose)
        {
            skeleton.SetSlotsToSetupPose();
            skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Update);
        }

        return true;
    }

    public bool SetCombinedSkin(IEnumerable<string> skinNames, bool resetToSetupPose = true)
    {
        if (skeletonGraphic == null || skeletonGraphic.Skeleton == null)
        {
            Debug.LogWarning("[UISpineCtrl] SkeletonGraphic is null or not initialized.");
            return false;
        }

        if (skinNames == null)
        {
            Debug.LogWarning("[UISpineCtrl] Skin names are null.");
            return false;
        }

        var skeleton = skeletonGraphic.Skeleton;
        var combinedSkin = new Spine.Skin("combined-ui-skin");
        bool hasSkin = false;

        foreach (var skinName in skinNames)
        {
            if (string.IsNullOrEmpty(skinName))
            {
                continue;
            }

            var skin = skeleton.Data.FindSkin(skinName);
            if (skin == null)
            {
                Debug.LogWarning($"[UISpineCtrl] Skin not found: {skinName}");
                continue;
            }

            combinedSkin.AddSkin(skin);
            hasSkin = true;
        }

        if (!hasSkin)
        {
            Debug.LogWarning("[UISpineCtrl] No valid skins found for combined skin.");
            return false;
        }

        skeleton.SetSkin(combinedSkin);

        if (resetToSetupPose)
        {
            skeleton.SetSlotsToSetupPose();
            skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Update);
        }

        return true;
    }

    public void SetAlpha(float alpha)
    {
        if (skeletonGraphic != null && skeletonGraphic.Skeleton != null)
        {
            skeletonGraphic.Skeleton.A = alpha;
        }
    }
}

