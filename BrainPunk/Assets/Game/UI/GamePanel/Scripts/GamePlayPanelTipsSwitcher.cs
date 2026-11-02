using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.ItemEvent;
using UnityEngine;

public class GamePlayPanelTipsSwitcher : MonoBehaviour, IEventSender
{
    #region Serialized Fields

    [SerializeField] private GameObject target;

    /// <summary>
    /// Target 从 0 缩放到 1 的展开动画时长，单位秒；为 0 时立即展开。
    /// </summary>
    [Tooltip("Target 从 0 缩放到 1 的展开动画时长，单位秒；为 0 时立即展开。")]
    [SerializeField, Min(0f)] private float expandDuration = 0.3f;

    /// <summary>
    /// 展开完成后的持续展示时长，单位秒；计时结束后回缩并隐藏 Target。
    /// </summary>
    [Tooltip("展开完成后的持续展示时长，单位秒；计时结束后回缩并隐藏 Target。")]
    [SerializeField, Min(0f)] private float displayDuration = 3f;

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;
    private Sequence _displaySequence;
    private bool _externalControl;
    private bool _targetPoseCaptured;
    private Vector3 _targetInitialLocalPosition;
    private Vector2 _targetInitialAnchoredPosition;
    private Quaternion _targetInitialLocalRotation;
    private Vector3 _targetInitialLocalScale = Vector3.one;

    /// <summary>
    /// 当前提示视觉对象。局部关卡可以在不修改 warningBubble 预制体的前提下
    /// 为同一个实例增加自己的物理/拖拽表现。
    /// </summary>
    public GameObject TargetObject => target;

    /// <summary>Inspector 中配置的默认展示时长。</summary>
    public float DisplayDuration => displayDuration;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<GamePlayPanelSwitchTipsEvent>(OnSwitchTips);

        CaptureTargetPose();

        if (target != null)
        {
            target.SetActive(false);
        }
    }

    private void OnDisable()
    {
        StopDisplaySequence();

        if (target != null)
        {
            target.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        StopDisplaySequence();
        EventsUtils.ResetEvents(ref _subscriber);
    }

    #endregion

    #region Event Handling

    private void OnSwitchTips(GamePlayPanelSwitchTipsEvent evt)
    {
        if (_externalControl || target == null)
        {
            return;
        }

        Debug.Log("Tips Warnging : " + evt.IsShow);

        if (!evt.IsShow)
        {
            HideTargetImmediately();
            return;
        }

        ShowTarget();
    }

    /// <summary>
    /// 交给关卡局部玩法接管 Target 的显示生命周期。
    /// 开启后仍订阅全局事件，但不再执行默认的展开/回缩动画；关闭后恢复
    /// 原有事件驱动行为。调用边界：仅由需要物理化 warningBubble 的关卡控制器使用。
    /// </summary>
    public void SetExternalControl(bool enabled)
    {
        if (enabled)
        {
            // Capture once while the panel still owns the authored pose. A Level
            // controller may be recreated while this panel survives a retry, so
            // the snapshot must outlive the controller that first used it.
            CaptureTargetPose();
        }
        else
        {
            // Leave the shared panel ready for its normal event-driven flow if a
            // level releases ownership before the panel itself is destroyed.
            ResetTargetPose();
        }

        _externalControl = enabled;
        StopDisplaySequence();

        if (target != null)
        {
            target.transform.localScale = _targetPoseCaptured
                ? _targetInitialLocalScale
                : Vector3.one;
            target.SetActive(false);
        }
    }

    /// <summary>
    /// Restore the target's authored local pose before a level takes ownership of
    /// the shared warning bubble. This is intentionally kept on the persistent
    /// panel so a dragged pose cannot become the next attempt's baseline.
    /// </summary>
    public void ResetTargetPose()
    {
        if (target == null)
            return;

        CaptureTargetPose();
        if (!_targetPoseCaptured)
            return;

        Transform targetTransform = target.transform;
        if (targetTransform is RectTransform targetRectTransform)
        {
            targetRectTransform.anchoredPosition = _targetInitialAnchoredPosition;
            // Anchored position is the authoritative pose for a RectTransform.
            // DragableItem moves through world position, so only restore its depth
            // here; writing local X/Y as well would apply a second coordinate
            // system and offset World Space UI on the next attempt.
            Vector3 localPosition = targetTransform.localPosition;
            localPosition.z = _targetInitialLocalPosition.z;
            targetTransform.localPosition = localPosition;
        }
        else
            targetTransform.localPosition = _targetInitialLocalPosition;
        targetTransform.localRotation = _targetInitialLocalRotation;
        targetTransform.localScale = _targetInitialLocalScale;
    }

    /// <summary>
    /// 外部接管者在提示完成回缩并隐藏后调用，通知主线从此刻开始下一轮配置化倒计时。
    /// 仅在 SetExternalControl(true) 有效期间派发，避免默认显示流程重复通知。
    /// </summary>
    public void NotifyExternalDisplayEnded()
    {
        if (!_externalControl)
            return;

        NotifyDisplayEnded();
    }

    #endregion

    #region Display Flow

    private void ShowTarget()
    {
        StopDisplaySequence();

        target.SetActive(true);
        target.transform.localScale = Vector3.zero;

        RotateFloatHandle rotateFloatHandle = target.GetComponent<RotateFloatHandle>();
        if (rotateFloatHandle != null)
        {
            rotateFloatHandle.ResetToStart();
        }

        _displaySequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);

        float safeExpandDuration = Mathf.Max(0f, expandDuration);
        if (safeExpandDuration > 0f)
        {
            _displaySequence.Append(target.transform
                .DOScale(Vector3.one, safeExpandDuration)
                .SetEase(Ease.OutQuad));
        }
        else
        {
            _displaySequence.AppendCallback(() =>
            {
                if (target != null)
                {
                    target.transform.localScale = Vector3.one;
                }
            });
        }

        float safeDisplayDuration = Mathf.Max(0f, displayDuration);
        if (safeDisplayDuration > 0f)
        {
            _displaySequence.AppendInterval(safeDisplayDuration);
        }

        _displaySequence.Append(target.transform.DOScale(Vector3.zero, 0.2f));

        _displaySequence.OnComplete(() =>
        {
            _displaySequence = null;

            if (target != null)
            {
                target.SetActive(false);
            }

            NotifyDisplayEnded();
        });
    }

    private void HideTargetImmediately()
    {
        StopDisplaySequence();
        target.SetActive(false);
    }

    private void StopDisplaySequence()
    {
        if (_displaySequence != null && _displaySequence.IsActive())
        {
            _displaySequence.Kill();
        }

        _displaySequence = null;
    }

    private void NotifyDisplayEnded()
    {
        this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, false);
    }

    private void CaptureTargetPose()
    {
        if (_targetPoseCaptured || target == null)
            return;

        Transform targetTransform = target.transform;
        _targetInitialLocalPosition = targetTransform.localPosition;
        if (targetTransform is RectTransform targetRectTransform)
            _targetInitialAnchoredPosition = targetRectTransform.anchoredPosition;
        _targetInitialLocalRotation = targetTransform.localRotation;
        Vector3 scale = targetTransform.localScale;
        _targetInitialLocalScale = scale.sqrMagnitude > 0.0001f ? scale : Vector3.one;
        _targetPoseCaptured = true;
    }

    #endregion
}
