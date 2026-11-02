using DG.Tweening;
using Game.Common;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// 单条残影实例，用于对象池。复制源骨骼的动画状态后做透明度渐变并回收。
/// </summary>
public class QuickActorTrailItem : MonoBehaviour, IRecycle
{
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    private Tweener _alphaTweener;
    private bool _fromPool;

    private void Awake()
    {
        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
    }

    /// <summary>
    /// 设置残影位置、旋转并同步到指定动画时间点，然后做透明度渐变
    /// </summary>
    public void Setup(Vector3 position, Quaternion rotation, string animName, float trackTime, bool isLoop, float fadeDuration)
    {
        transform.position = position;
        transform.rotation = rotation;

        if (skeletonAnimation != null && skeletonAnimation.AnimationState != null && !string.IsNullOrEmpty(animName))
        {
            skeletonAnimation.enabled = true;

            var state = skeletonAnimation.AnimationState;
            var skeleton = skeletonAnimation.Skeleton;

            // 清空并重建当前轨道
            state.ClearTracks();
            Spine.TrackEntry entry = state.SetAnimation(0, animName, isLoop);

            // 用 trackTime 采样到对应帧，再应用到 skeleton
            float sampleTime = Mathf.Max(0f, trackTime);
            if (entry != null && entry.Animation != null)
            {
                float animDuration = Mathf.Max(0f, entry.Animation.Duration);
                if (animDuration > 0f && sampleTime > animDuration)
                    sampleTime = 0f;
            }
            state.Update(sampleTime);

            // 使用 TimeScale=0 冻结该轨道
            if (entry != null)
                entry.TimeScale = 0f;

            state.Apply(skeleton);
            skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Update);

            // 这里不禁用组件，依赖 entry.TimeScale=0 保持定格
            Debug.Log($"Setup: animName = {animName}, trackTime = {trackTime}, isLoop = {isLoop}");      
        }

        SetAlpha(1f);

        _alphaTweener?.Kill();
        _alphaTweener = DOTween.To(GetAlpha, SetAlpha, 0f, fadeDuration)
            .SetEase(Ease.Linear)
            .OnComplete(OnFadeComplete);
    }

    private float GetAlpha()
    {
        if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
            return skeletonAnimation.Skeleton.A;
        return 1f;
    }

    private void SetAlpha(float alpha)
    {
        if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
            skeletonAnimation.Skeleton.A = alpha;
    }

    private void OnFadeComplete()
    {
        _alphaTweener = null;
        if (_fromPool && ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Recycle(this);
        else
            Destroy(gameObject);
    }

    public void OnSpawn()
    {
        _fromPool = true;
    }

    public void OnRecycle()
    {
        _fromPool = false;
        _alphaTweener?.Kill();
        _alphaTweener = null;
        if (skeletonAnimation != null && skeletonAnimation.AnimationState != null)
            skeletonAnimation.AnimationState.ClearTracks();
        skeletonAnimation.enabled = true;
        SetAlpha(0f);
    }

    private void OnDestroy()
    {
        _alphaTweener?.Kill();
        _alphaTweener = null;
    }
}
