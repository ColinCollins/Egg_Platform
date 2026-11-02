using Bear.Logger;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// IWanner 一次性场景按钮：指定 Tag 的 Collider2D 首次进入 BoxCollider2D Trigger 时派发按下事件。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class IWannerGame_Button : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：限制允许按下按钮的 Collider2D Tag。
    /// 调用边界：只比较进入当前 BoxCollider2D Trigger 的 Collider2D 所在 GameObject。
    /// 注意事项：默认 Player；配置为空时按钮不会响应任何触发。
    /// </summary>
    [Header("Trigger Settings")]
    [Tooltip("允许按下按钮的 Collider2D Tag，默认 Player")]
    [SerializeField] private string triggerTag = "Player";

    /// <summary>
    /// 用途：指定按钮未按下时需要显示、按下后需要隐藏的中间节点。
    /// 调用边界：仅在当前按钮首次有效触发时调用 SetActive(false)。
    /// 注意事项：不会在组件禁用、启用或销毁时恢复显示状态。
    /// </summary>
    [Header("Visual Settings")]
    [Tooltip("按钮首次触发后需要隐藏的 middle 节点")]
    [SerializeField] private GameObject middle;

    /// <summary>
    /// 用途：指定按钮首次按下后需要调整局部 Y 坐标的顶部节点。
    /// 调用边界：仅在当前按钮首次有效触发时把 localPosition.y 设为 0。
    /// 注意事项：保留 top 原有的 localPosition.x 和 localPosition.z。
    /// </summary>
    [Tooltip("按钮首次触发后 localPosition.y 需要变为 0 的 top 节点")]
    [SerializeField] private Transform top;

    /// <summary>
    /// 用途：配置按钮首次被有效 Collider2D 按下时需要执行的本地事件。
    /// 调用边界：仅由当前组件的 BoxCollider2D Trigger 入口派发，并把进入者作为参数传出。
    /// 注意事项：单个组件实例的整个生命周期内最多派发一次，禁用后重新启用也不会重置。
    /// </summary>
    [Header("Events")]
    [Tooltip("按钮首次被有效 Collider2D 按下时触发一次")]
    [SerializeField] private Collider2DEvent onPressed = new();

    #endregion

    #region Public API

    /// <summary>
    /// 用途：供运行时代码订阅或移除当前按钮的一次性按下回调。
    /// 调用边界：组件只在 BoxCollider2D Trigger 首次接收到匹配 Tag 的 Collider2D 时派发。
    /// 注意事项：外部只应管理监听器，不应直接调用 Invoke 绕过一次性门禁。
    /// </summary>
    public Collider2DEvent OnPressed => onPressed;

    #endregion

    #region Runtime State

    private bool _hasTriggered;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        BoxCollider2D triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider == null)
        {
            this.LogWarning(
                "[IWannerGame_Button] BoxCollider2D is missing; button cannot receive trigger events.");
        }
        else if (!triggerCollider.isTrigger)
        {
            this.LogWarning(
                "[IWannerGame_Button] BoxCollider2D is not marked as Trigger; button will not run as expected.");
        }

        if (string.IsNullOrWhiteSpace(triggerTag))
        {
            this.LogWarning(
                "[IWannerGame_Button] Trigger Tag is empty; button is disabled.");
        }

        if (middle == null)
        {
            this.LogWarning(
                "[IWannerGame_Button] Middle is missing; pressed visibility cannot be applied.");
        }

        if (top == null)
        {
            this.LogWarning(
                "[IWannerGame_Button] Top is missing; pressed local position cannot be applied.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Application.isPlaying
            || _hasTriggered
            || other == null
            || string.IsNullOrWhiteSpace(triggerTag)
            || !other.CompareTag(triggerTag))
            return;

        _hasTriggered = true;
        ApplyPressedVisual();
        onPressed?.Invoke(other);
    }

    #endregion

    #region Pressed Visual

    private void ApplyPressedVisual()
    {
        if (middle != null)
            middle.SetActive(false);

        if (top == null)
            return;

        Vector3 topLocalPosition = top.localPosition;
        topLocalPosition.y = 0f;
        top.localPosition = topLocalPosition;

        AudioManager.PlaySound("boardTrigger");
    }

    #endregion
}
