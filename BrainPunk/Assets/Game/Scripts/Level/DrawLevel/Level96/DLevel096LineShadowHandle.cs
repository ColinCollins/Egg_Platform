using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D096脚底实际踩住画线时隐藏整组阴影，离开后恢复。
/// 画线使用本关WithoutShadowGround层，不改变共享Actor或阴影投影算法。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/DLevel 096 Line Shadow Handle")]
public sealed class DLevel096LineShadowHandle : MonoBehaviour
{
    #region Inspector

    [SerializeField] private DrawLevelCtrl drawLevelCtrl;
    [SerializeField] private Collider2D actorCollider;
    [SerializeField] private GameObject actorShadowRoot;
    [SerializeField] private LayerMask lineLayers = 1 << 13;

    #endregion

    #region Runtime State

    private const float FootContactTolerance = 0.2f;
    private readonly List<ContactPoint2D> _contacts = new(16);
    private bool _suppressed;
    private bool _shadowWasActive;

    #endregion

    #region Lifecycle

    private void LateUpdate()
    {
        if (!Application.isPlaying || actorShadowRoot == null || drawLevelCtrl == null)
            return;

        // 结算演出拥有Shadow状态；不能在成功渐隐后因角色离线把影子重新打开。
        if (drawLevelCtrl.IsOutcomeLocked)
            return;

        if (IsStandingOnLine())
        {
            if (!_suppressed)
                _shadowWasActive = actorShadowRoot.activeSelf;
            _suppressed = true;
            actorShadowRoot.SetActive(false);
        }
        else
        {
            RestoreShadow();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (drawLevelCtrl != null && !drawLevelCtrl.IsOutcomeLocked)
            RestoreShadow();
    }

    #endregion

    #region Contact And Visibility

    private bool IsStandingOnLine()
    {
        if (actorCollider == null || !actorCollider.enabled ||
            !actorCollider.gameObject.activeInHierarchy)
            return false;

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(lineLayers);
        actorCollider.GetContacts(filter, _contacts);
        float footLimit = actorCollider.bounds.min.y + FootContactTolerance;
        foreach (ContactPoint2D contact in _contacts)
        {
            // 侧擦或从线下顶到线不算踩线；使用真实接触，线在地面下也不会误隐藏。
            if (contact.normal.y > 0.2f && contact.point.y <= footLimit)
                return true;
        }
        return false;
    }

    private void RestoreShadow()
    {
        if (!_suppressed)
            return;

        if (actorShadowRoot != null)
            actorShadowRoot.SetActive(_shadowWasActive);
        _suppressed = false;
    }

    #endregion
}
