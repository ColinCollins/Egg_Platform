using System.Collections.Generic;
using Game.Turntable;
using UnityEngine;

/// <summary>
/// 只响应指定 Tag 的下压按钮。
/// 同一目标存在多个 Collider2D 时，必须等全部 Collider 离开后才释放按钮。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Tag Press Button")]
public sealed class TagPressButton : PressButtonBase
{
    #region Inspector Settings

    /// <summary>
    /// 可以触发按钮的对象 Tag。
    /// 注意事项：该值必须已在 ProjectSettings/TagManager 中定义；空值表示不接受任何对象。
    /// </summary>
    [Header("Tag Filter")]
    [Tooltip("只有该 Tag 的 Collider2D 可以压下和释放按钮。")]
    [SerializeField] private string targetTag = "Player";

    #endregion

    #region Runtime State

    private readonly HashSet<Collider2D> _pressingColliders = new HashSet<Collider2D>();

    #endregion

    #region Trigger Lifecycle

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsTarget(other) || !_pressingColliders.Add(other))
            return;

        if (_pressingColliders.Count != 1)
            return;

        SetPressed(true);
        _onEnter?.Invoke();
    }

    protected override void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || !_pressingColliders.Remove(other) || _pressingColliders.Count > 0)
            return;

        SetPressed(false);
        _onExit?.Invoke();
    }

    private void OnDisable()
    {
        bool wasPressed = _isPressed;
        _pressingColliders.Clear();
        if (!wasPressed)
            return;

        SetPressedInstant(false);
        _onExit?.Invoke();
    }

    #endregion

    #region Internal Helpers

    private bool IsTarget(Collider2D other)
    {
        return other != null &&
               !string.IsNullOrEmpty(targetTag) &&
               other.CompareTag(targetTag);
    }

    #endregion
}
