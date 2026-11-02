using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Level 122 三消页面的关闭按钮。</summary>
[DisallowMultipleComponent]
public sealed class Level122CloseButton : MonoBehaviour, IPointerClickHandler
{
    private Level122MatchPanel _owner;

    public void Configure(Level122MatchPanel owner)
    {
        _owner = owner;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            _owner?.HandleCloseRequested();
    }

    private void OnDestroy()
    {
        _owner = null;
    }
}
