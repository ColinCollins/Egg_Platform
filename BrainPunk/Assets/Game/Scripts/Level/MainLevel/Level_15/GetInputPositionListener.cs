using Game.ItemEvent;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 提供触发时候鼠标位置
/// </summary>
public class GetInputPositionListener : BaseItemEventHandle
{
    [Header("Events")]
    [SerializeField] private UnityEvent<Vector3> OnClickEvent;
    [SerializeField] private Camera mainCamera;

    private Vector3 worldPosition;

    public override void Execute()
    {
        CheckInputClick();
        IsDone = true;
    }

    private void CheckInputClick()
    {
        bool inputDown = false;
        var inputPosition = Vector3.zero;

        if (Input.GetMouseButtonDown(0))
        {
            inputDown = true;
            inputPosition = Input.mousePosition;
        }
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            inputDown = true;
            inputPosition = Input.GetTouch(0).position;
        }

        if (!inputDown) return;

        worldPosition = mainCamera.ScreenToWorldPoint(inputPosition);
        OnClick();
    }

    public void OnClick()
    {
        worldPosition.z = 0;
        OnClickEvent?.Invoke(worldPosition);
    }

}
