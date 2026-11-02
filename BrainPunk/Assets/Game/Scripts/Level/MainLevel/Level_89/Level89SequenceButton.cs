using Game.Turntable;
using UnityEngine;

/// <summary>
/// Level089 的顺序机关按钮；只有离开触发器后才能再次触发。
/// </summary>
public class Level89SequenceButton : PressButtonBase
{
    /// <summary>
    /// 按钮在顺序谜题中的编号；从画面左侧到右侧依次为 1 至 4。
    /// </summary>
    [SerializeField] private int buttonNumber;

    private bool isLocked;
    private bool requiresExitBeforeRearm;
    private Level89Ctrl levelCtrl;

    internal int ButtonNumber => buttonNumber;

    protected override void Start()
    {
        base.Start();
        levelCtrl = GetComponentInParent<Level89Ctrl>();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || isLocked || requiresExitBeforeRearm)
            return;

        requiresExitBeforeRearm = true;
        SetPressed(true);
        levelCtrl?.OnSequenceButtonPressed(this);
    }

    protected override void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        requiresExitBeforeRearm = false;
        if (!isLocked)
            SetPressed(false);
    }

    internal void SetLocked(bool value)
    {
        isLocked = value;
        if (isLocked)
            SetPressedInstant(true);
    }

    internal void SetReleasedFast()
    {
        isLocked = false;
        SetPressed(false);
    }
}
