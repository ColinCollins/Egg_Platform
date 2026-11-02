using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>150 关正面按钮反馈；沿用原组件名，但按 069 关即时切图，不缩放。</summary>
public sealed class Level150PressScale : ButtonClickTrigger, IPointerExitHandler
{
    private Image idleFace;
    private Image pressedFace;
    private bool pressing;
    private bool inputEnabled = true;

    public event Action ReleasedOutside;

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled && pressing)
        {
            OnButtonUp(false);
            ReleasedOutside?.Invoke();
        }
    }

    public void SetButtonFaces(Image idle, Image pressed)
    {
        idleFace = idle;
        pressedFace = pressed;
        ShowPressed(false);
    }

    private void ShowPressed(bool pressed)
    {
        if (idleFace != null)
            idleFace.enabled = !pressed;
        if (pressedFace != null)
            pressedFace.enabled = pressed;
    }

    public override void OnButtonDown(bool hasAnim)
    {
        if (!inputEnabled || pressing)
            return;
        pressing = true;
        ShowPressed(true);
    }

    public override void OnButtonUp(bool hasAnim)
    {
        pressing = false;
        ShowPressed(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!pressing)
            return;
        OnButtonUp(false);
        ReleasedOutside?.Invoke();
    }

    private void OnDisable()
    {
        pressing = false;
        ShowPressed(false);
    }
}
