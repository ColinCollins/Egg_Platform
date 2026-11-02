using UnityEngine;
using UnityEngine.Events;
using Bear.EventSystem;
using Game.Events;
using Game.Play;

public class ClickItemEventHandle : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool enableClick = true;
    [SerializeField] private int maxClickCount = -1; // -1 means unlimited

    [SerializeField] private bool multiplyInputCheck;

    [Header("Events")]
    [SerializeField] private UnityEvent<Transform, Vector2> OnClickEvent;

    private int clickCount = 0;
    private Transform currentClicker;
    private EventSubscriber _subscriber;
    private bool _isPause;

    private void Start()
    {
        AddListener();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        _isPause = !evt.NewState.Equals(GamePlayStateName.PLAYING);
    }

    void Update()
    {
        if (!enableClick) return;
        if (_isPause) return;
        if (maxClickCount >= 0 && clickCount >= maxClickCount) return;

        CheckInputClick();
    }

    private void CheckInputClick()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            TryClickAtScreenPosition(cam, Input.mousePosition);
            return;
        }
#endif

        if (Input.touchCount <= 0) return;

        int maxTouchChecks = multiplyInputCheck ? 3 : 1;
        int count = Mathf.Min(Input.touchCount, maxTouchChecks);
        for (int i = 0; i < count; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began) continue;

            if (TryClickAtScreenPosition(cam, touch.position))
            {
                return;
            }
        }
    }

    private bool TryClickAtScreenPosition(Camera cam, Vector3 screenPos)
    {
        Vector2 worldPoint = cam.ScreenToWorldPoint(screenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

        if (hit.collider == null || hit.collider.gameObject != gameObject)
            return false;

        currentClicker = hit.collider.transform;
        OnClick(worldPoint);
        return true;
    }

    public void OnClick(Vector2 worldPoint)
    {
        clickCount++;
        OnClickEvent?.Invoke(currentClicker, worldPoint);
    }

    public void SetEnableClick(bool enable)
    {
        enableClick = enable;
    }

    public void ResetClickCount()
    {
        clickCount = 0;
    }

    public int GetClickCount()
    {
        return clickCount;
    }
}
