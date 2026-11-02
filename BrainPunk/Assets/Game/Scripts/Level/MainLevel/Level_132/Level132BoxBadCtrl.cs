using System;
using Spine.Unity;
using UnityEngine;

/// <summary>132 非玩家角色：保留通用接地/物理，只在准备阶段托管锯绳视觉。</summary>
public sealed class Level132BoxBadCtrl : ActorCtrl
{
    [SerializeField] private SkeletonAnimation sawSkeleton;
    [SerializeField] private BoneFollower sawFollower;
    [SerializeField] private SpriteRenderer sawVisual;

    private bool _sawControlled;
    private bool _sawUnavailable;
    private bool _frozen;
    private float _animationSpeed;

    private void Awake()
    {
        HideSaw();
    }

    protected override void HandleAnimators()
    {
        // 接地检测仍由 ActorCtrl.OnUpdate 执行，不让通用 idle 覆盖 saw。
        if (_sawControlled || _frozen)
            return;
        base.HandleAnimators();
    }

    /// <summary>只消费关卡阶段与游戏暂停，不读取拉力或剩余秒数来推进动画。</summary>
    public void SetSawPresentation(bool sawing, bool suspended)
    {
        if (!isActiveAndEnabled)
        {
            ClearSawPresentation();
            return;
        }

        SetAnimationFrozen(suspended);
        if (!sawing || IsDied)
        {
            StopSaw();
            _sawUnavailable = false;
            return;
        }
        // 等待共享 Start/Spine 初始化；准备期间逐帧重复调用不重播已开始的动画。
        if (RB == null || SpineCtrl == null || SpineCtrl.AnimationState == null ||
            _sawControlled || _sawUnavailable)
            return;

        try
        {
            if (sawSkeleton == null || sawSkeleton.AnimationState != SpineCtrl.AnimationState ||
                sawSkeleton.Skeleton == null || sawFollower == null || sawVisual == null || sawVisual.sprite == null ||
                sawFollower.skeletonRenderer != sawSkeleton || sawFollower.boneName != "point_saw" ||
                sawSkeleton.Skeleton.FindBone("point_saw") == null ||
                sawSkeleton.Skeleton.Data.FindAnimation("saw") == null ||
                sawSkeleton.Skeleton.Data.FindAnimation("idle") == null)
            {
                ReportSawUnavailable("Check the local Spine, saw/idle animations and point_saw references.");
                return;
            }

            if (SpineCtrl.PlayAnimation("saw", true, false) == null)
            {
                ReportSawUnavailable("Could not start saw animation.");
                return;
            }
            _sawControlled = true;
            // 首次显示前应用首帧和挂点，避免锯子短暂出现在 idle 的挂点位置。
            sawSkeleton.Update(0f);
            sawFollower.Initialize();
            if (!sawFollower.valid || sawFollower.bone == null)
            {
                ReportSawUnavailable("Could not bind point_saw.");
                return;
            }
            sawFollower.enabled = true;
            sawFollower.LateUpdate();
            sawVisual.enabled = true;
        }
        catch (Exception exception)
        {
            ReportSawUnavailable(exception.Message);
        }
    }

    private void SetAnimationFrozen(bool frozen)
    {
        if (sawSkeleton == null || _frozen == frozen)
            return;
        _frozen = frozen;
        if (frozen)
        {
            _animationSpeed = sawSkeleton.timeScale;
            sawSkeleton.timeScale = 0f;
        }
        else
        {
            sawSkeleton.timeScale = _animationSpeed;
        }
    }

    private void HideSaw()
    {
        if (sawVisual != null) sawVisual.enabled = false;
        if (sawFollower != null) sawFollower.enabled = false;
    }

    private void StopSaw()
    {
        bool wasSawing = _sawControlled;
        _sawControlled = false;
        HideSaw();
        if (!wasSawing || IsDied || SpineCtrl == null ||
            SpineCtrl.AnimationState?.Data?.SkeletonData?.FindAnimation("idle") == null)
            return;

        try
        {
            SpineCtrl.PlayAnimation("idle", true, false);
            if (sawSkeleton != null && sawSkeleton.valid)
                sawSkeleton.Update(0f);
        }
        catch (Exception exception)
        {
            // 清理失败也不能让锯子可见或阻断随后对动画速度的恢复。
            Debug.LogWarning("[Level132] Could not restore Box-Bad idle: " + exception.Message, this);
        }
    }

    private void ReportSawUnavailable(string reason)
    {
        _sawUnavailable = true;
        StopSaw();
        Debug.LogWarning("[Level132] Saw presentation unavailable; gameplay is unchanged. " + reason, this);
    }

    /// <summary>终态、重开、禁用共用清理；不恢复移动输入，也不修改角色物理。</summary>
    public void ClearSawPresentation()
    {
        StopSaw();
        SetAnimationFrozen(false);
        _sawUnavailable = false;
    }

    private void OnDisable()
    {
        ClearSawPresentation();
    }
}
