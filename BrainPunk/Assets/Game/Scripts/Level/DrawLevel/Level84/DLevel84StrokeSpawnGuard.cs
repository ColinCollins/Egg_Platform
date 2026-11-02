using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弹性线系列的新笔画接触保护：只暂缓出生时已与动态物体重叠的碰撞对，分离后恢复。
/// 由当前关卡的首笔/后续笔画完成接口调用，不修改画线采样、几何、静态地面或重建流程。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class DLevel84StrokeSpawnGuard : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region 序列化绑定与本关状态

    /// <summary>当前关卡的画线组件，必须显式绑定；只从其公开接口取得刚完成的运行时笔画。</summary>
    [SerializeField] private DrawLineFeature drawFeature;

    private struct IgnoredPair
    {
        public Collider2D Stroke;
        public Collider2D Existing;
    }

    private readonly List<Collider2D> _strokeColliders = new();
    private readonly List<Collider2D> _overlaps = new();
    private readonly List<IgnoredPair> _ignoredPairs = new();

    #endregion

    #region 完成笔画后的同步接触准备

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || drawFeature == null ||
            !drawFeature.TryGetCompletedStrokeObject(result.StrokeIndex, out GameObject stroke) ||
            !stroke.TryGetComponent(out Rigidbody2D strokeBody))
            return;

        // 笔画的子 Collider 刚生成，须在第一步求解前同步它们的实际世界位置。
        Physics2D.SyncTransforms();
        strokeBody.GetAttachedColliders(_strokeColliders);
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(stroke.layer));
        foreach (Collider2D collider in _strokeColliders)
        {
            if (!collider.enabled || collider.isTrigger)
                continue;

            collider.Overlap(filter, _overlaps);
            foreach (Collider2D other in _overlaps)
            {
                Rigidbody2D otherBody = other.attachedRigidbody;
                if (otherBody == null || otherBody == strokeBody ||
                    otherBody.bodyType == RigidbodyType2D.Static ||
                    Physics2D.GetIgnoreCollision(collider, other))
                    continue;

                ColliderDistance2D distance = collider.Distance(other);
                if (!distance.isValid || !distance.isOverlapped)
                    continue;

                Physics2D.IgnoreCollision(collider, other, true);
                _ignoredPairs.Add(new IgnoredPair { Stroke = collider, Existing = other });
            }
        }
        _strokeColliders.Clear();
        _overlaps.Clear();
    }

    #endregion

    #region 分离恢复与自身状态清理

    private void FixedUpdate()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        for (int i = _ignoredPairs.Count - 1; i >= 0; i--)
        {
            IgnoredPair pair = _ignoredPairs[i];
            if (pair.Stroke == null || pair.Existing == null)
            {
                RemovePair(i);
                continue;
            }

            ColliderDistance2D distance = pair.Stroke.Distance(pair.Existing);
            if (!distance.isValid || distance.distance <= 0.02f)
                continue;

            Physics2D.IgnoreCollision(pair.Stroke, pair.Existing, false);
            RemovePair(i);
        }
    }

    private void RemovePair(int index)
    {
        int last = _ignoredPairs.Count - 1;
        _ignoredPairs[index] = _ignoredPairs[last];
        _ignoredPairs.RemoveAt(last);
    }

    /// <summary>只释放本组件持有的临时碰撞忽略对；笔画和关卡对象的销毁仍由既有 Draw 流程负责。</summary>
    public void OnDrawAttemptReset()
    {
        if (Application.isPlaying)
            RestoreOwnedPairs();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            RestoreOwnedPairs();
    }

    private void RestoreOwnedPairs()
    {
        foreach (IgnoredPair pair in _ignoredPairs)
        {
            if (pair.Stroke != null && pair.Existing != null)
                Physics2D.IgnoreCollision(pair.Stroke, pair.Existing, false);
        }
        _ignoredPairs.Clear();
        _strokeColliders.Clear();
        _overlaps.Clear();
    }

    #endregion
}
