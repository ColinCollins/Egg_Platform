using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;

public class ActorSpineCtrl : MonoBehaviour
{
    [FoldoutGroup("Refs")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    public Spine.AnimationState AnimationState => skeletonAnimation != null ? skeletonAnimation.AnimationState : null;

    [FoldoutGroup("Config")]
    [SpineAnimation]
    [SerializeField] private string defaultAnimName = "";

    [FoldoutGroup("Config")]
    [SerializeField] private bool defaultLoop = true;

    [FoldoutGroup("Test")]
    // [ValueDropdown("GetAvailableAnimations")]
    [SpineAnimation]
    [SerializeField] private string testAnimName = "";

    [FoldoutGroup("Test")]
    [SerializeField] private bool testLoop = true;


    void Awake()
    {
        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
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

    [FoldoutGroup("Test")]
    [Button("Reverse Current Animation")]
    private void TestReverseCurrentAnimation()
    {
        if (skeletonAnimation == null || skeletonAnimation.AnimationState == null)
        {
            Debug.LogWarning("[ActorSpineCtrl] SkeletonAnimation is null or not initialized.");
            return;
        }

        var entry = skeletonAnimation.AnimationState.GetCurrent(0);
        if (entry == null || entry.Animation == null)
        {
            Debug.LogWarning("[ActorSpineCtrl] No animation on track 0.");
            return;
        }

        // 片头且正向时直接倒放会从 0 往负时间走，先对齐到片尾更直观
        if (entry.TimeScale >= 0f && entry.TrackTime <= 0.0001f)
            entry.TrackTime = entry.AnimationEnd;

        entry.TimeScale = -1f;
    }

    /// <summary>
    /// 获取当前正在播放的动画名称
    /// </summary>
    /// <returns>当前动画名称，如果没有播放动画则返回空字符串</returns>
    public string GetCurrentAnimationName()
    {
        if (skeletonAnimation == null || skeletonAnimation.AnimationState == null)
        {
            return string.Empty;
        }

        var trackEntry = skeletonAnimation.AnimationState.GetCurrent(0);
        if (trackEntry == null || trackEntry.Animation == null)
        {
            return string.Empty;
        }

        return trackEntry.Animation.Name;
    }

    /// <summary>
    /// 检查当前正在播放的动画名称是否与新动画名称一致
    /// </summary>
    /// <param name="newAnimName">新的动画名称</param>
    /// <returns>如果当前动画与新动画名称一致返回 true，否则返回 false</returns>
    public bool IsPlayingAnimation(string newAnimName)
    {
        if (string.IsNullOrEmpty(newAnimName))
        {
            return false;
        }

        string currentAnimName = GetCurrentAnimationName();
        return string.Equals(currentAnimName, newAnimName, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 播放指定动画
    /// </summary>
    /// <param name="animName">动画名称</param>
    /// <param name="loop">是否循环</param>
    /// <param name="checkCurrent">是否检查当前动画，如果正在播放相同动画则跳过（默认 false）</param>
    /// <returns>当前播放轨道的 TrackEntry；失败返回 null；如果因 checkCurrent 跳过，则返回当前 TrackEntry</returns>
    public Spine.TrackEntry PlayAnimation(string animName, bool loop = true, bool checkCurrent = true)
    {

        if (skeletonAnimation == null || skeletonAnimation.AnimationState == null)
        {
            Debug.LogWarning("[ActorSpineCtrl] SkeletonAnimation is null or not initialized.");
            return null;
        }

        if (string.IsNullOrEmpty(animName))
        {
            Debug.LogWarning("[ActorSpineCtrl] Animation name is empty.");
            return null;
        }

        var currentEntry = skeletonAnimation.AnimationState.GetCurrent(0);

        // 如果启用检查且当前正在播放相同动画，则跳过
        if (checkCurrent && IsPlayingAnimation(animName))
        {
            return currentEntry;
        }

        // Debug.Log("[ActorSpineCtrl] Animation name" + animName);

        return skeletonAnimation.AnimationState.SetAnimation(0, animName, loop);
    }

    /// <summary>
    /// 倒放指定动画（从尾到头播放）
    /// </summary>
    /// <param name="animName">动画名称</param>
    /// <param name="loop">是否循环倒放</param>
    public void RevertPlayAnimation(string animName, bool loop = false)
    {
        if (skeletonAnimation == null || skeletonAnimation.AnimationState == null)
        {
            Debug.LogWarning("[ActorSpineCtrl] SkeletonAnimation is null or not initialized.");
            return;
        }

        if (string.IsNullOrEmpty(animName))
        {
            Debug.LogWarning("[ActorSpineCtrl] Animation name is empty.");
            return;
        }

        Spine.TrackEntry entry = skeletonAnimation.AnimationState.SetAnimation(0, animName, loop);
        if (entry != null)
            entry.TimeScale = -1f;
    }

    /// <summary>
    /// 切换 Spine Skin。
    /// </summary>
    /// <param name="skinName">目标 Skin 名称</param>
    /// <param name="resetToSetupPose">切换后是否重置到 Setup Pose（默认 true）</param>
    /// <returns>切换成功返回 true，否则返回 false</returns>
    public bool SetSkin(string skinName, bool resetToSetupPose = true)
    {
        if (skeletonAnimation == null || skeletonAnimation.Skeleton == null)
        {
            Debug.LogWarning("[ActorSpineCtrl] SkeletonAnimation is null or not initialized.");
            return false;
        }

        if (string.IsNullOrEmpty(skinName))
        {
            Debug.LogWarning("[ActorSpineCtrl] Skin name is empty.");
            return false;
        }

        var skeleton = skeletonAnimation.Skeleton;
        var skin = skeleton.Data.FindSkin(skinName);
        if (skin == null)
        {
            Debug.LogWarning($"[ActorSpineCtrl] Skin not found: {skinName}");
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

    public void SetAlpha(float alpha)
    {
        if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
        {
            skeletonAnimation.Skeleton.A = alpha;
        }
    }
}
