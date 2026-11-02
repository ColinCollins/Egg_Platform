using Bear.Logger;
using DG.Tweening;
using Game.Common;
using Game.Scripts.Common;
using MoreMountains.Feedbacks;
using UnityEngine;

public class Level52Ctrl : MonoBehaviour, IDebuger
{
    [SerializeField] private GameObject fire;
    [SerializeField] private ParticleSystem flash;

    [Header("拖拽")]
    [Tooltip("为空则在自身上 GetComponent；请在同物体或子物体上挂 DragEventsListener")]
    [SerializeField]
    private DragEventsListener dragListener;

    [SerializeField] private TimelineCtrl timelineCtrl;
    [SerializeField] private Collider2D iceCollider;

    [SerializeField] private Collider2D doorCollider;

    [SerializeField] private TriggerAreaHandle triggerHandle;

    [SerializeField] private MMF_Player shake;

    [Header("Ice crash")]


    [SerializeField] private Camera mainCamera;

    [SerializeField] private ParticleSystem knock;
    [SerializeField] private SpriteRenderer iceCrash;
    [SerializeField] private Sprite[] iceCrashSprites;

    [SerializeField] private int[] crashLimitCount;

    private int currentCrashCount = 0;
    private int currentCrashIndex = -1;

    private bool crashFinished = false;


    [Tooltip("相对段落起点，指针在屏幕上的位移 ≥ 该值（像素）时触发 PlayFlash（仅在可触发状态下）")]
    [SerializeField]
    private float distance = 30f;

    [Tooltip("触发 PlayFlash 后锁定；相对当前段落起点的位移需先 < 该值（像素）才再次允许触发，或松手重新 BeginDrag")]
    [SerializeField]
    private float minRearmDistance = 8f;

    private int count;

    [SerializeField] private int MaxFlashCount = 3;

    private bool IsFire => count >= MaxFlashCount;

    private Vector2 _segmentStartScreen;

    /// <summary>false 表示刚触发过 Flash，需位移回到 min 以下或新 Begin 后才可再触发。</summary>
    private bool _canFlash = true;

    private void Awake()
    {
        if (dragListener == null)
            dragListener = GetComponent<DragEventsListener>();

        timelineCtrl.OnStopped += OnMelt;

        var pos = iceCollider.transform.localPosition;
        pos.y += 20;
        iceCollider.transform.localPosition = pos;
        iceCollider.transform.DOMoveY(-1.46f, .8f).SetEase(Ease.InQuart).OnComplete(() =>
        {
            shake.PlayFeedbacks();
            AudioManager.PlaySound("crash");
            VibrationManager.Instance.Vibrate();
        });
    }

    private void OnEnable()
    {
        if (dragListener == null)
            return;

        dragListener.BeginDragHandlers += OnBeginDrag;
        dragListener.DragHandlers += OnDrag;
    }

    private void OnDisable()
    {
        if (dragListener == null)
            return;

        dragListener.BeginDragHandlers -= OnBeginDrag;
        dragListener.DragHandlers -= OnDrag;
    }

    /// <summary>对应 Drag Begin：记录当前指针位置为段落起点。</summary>
    private void OnBeginDrag(Transform _)
    {
        if (dragListener == null)
            return;

        _segmentStartScreen = dragListener.CurrentScreenPosition;
        _canFlash = true;

        this.Log(
            $"BeginDrag segmentStartScreen={_segmentStartScreen}, distanceThreshold={distance}, minRearm={minRearmDistance}, fingerId={dragListener.TrackingFingerId}");
    }

    /// <summary>
    /// 可触发时：<see cref="distance"/> 达标则 PlayFlash 并锁定，直至相对段落起点位移 &lt; <see cref="minRearmDistance"/> 再重新取点解锁；
    /// <see cref="OnBeginDrag"/> 会强制解锁并取新起点。
    /// </summary>
    private void OnDrag(Transform _)
    {
        if (dragListener == null || distance <= 0f)
            return;

        if (IsFire)
            return;

        var current = dragListener.CurrentScreenPosition;
        var delta = Vector2.Distance(_segmentStartScreen, current);

        if (flash != null)
        {
            var inputPosition = Input.GetTouch(0).position;
            var worldPosition = Camera.main.ScreenToWorldPoint(inputPosition);
            if (Input.touchCount > 1)
                for (int i = 1; i < Input.touchCount; i++)
                {
                    var pos = Input.GetTouch(i).position;
                    var ws = Camera.main.ScreenToWorldPoint(pos);
                    var dis1 = Vector3.Distance(ws, _.position);
                    var dis2 = Vector3.Distance(worldPosition, _.position);
                    if (dis1 < dis2)
                    {
                        worldPosition = ws;
                    }
                }
            worldPosition.z = 0;
            flash.transform.position = worldPosition;
            flash.Play();
        }

        if (!_canFlash)
        {
            this.Log(
                   $"Drag re-armed: deltaPx={delta:F1} (< minRearm={minRearmDistance}), newSegmentStart={_segmentStartScreen}");
            if (minRearmDistance > 0f && delta < minRearmDistance)
            {
                _canFlash = true;
                _segmentStartScreen = current;
            }
            else
            {
                _segmentStartScreen = current;
            }

            return;
        }

        if (delta < distance)
        {
            this.Log($"Drag threshold not reached: deltaPx={delta:F1} (< distance={distance})");
            return;
        }

        PlayFlash();
        _canFlash = false;
    }

    public void PlayFlash()
    {
        if (IsFire)
            return;

        /* if (flash != null)
            flash.Play(); */

        count++;

        if (IsFire)
        {
            PlayFire();
        }

        AudioManager.PlaySound("frictional");
    }

    public void PlayFire()
    {
        triggerHandle.enabled = true;
        fire.SetActive(true);
        fire.transform.localScale = Vector3.zero;
        fire.transform.DOScale(Vector3.one * 1.5f, 0.2f).SetEase(Ease.OutBack, 3);

        AudioManager.PlaySound("buildInFire");
    }

    public void OnEnterFire()
    {
        if (timelineCtrl == null)
        {
            this.LogError("[Level52Ctrl] OnEnterFire failed: timelineCtrl is null. Fallback to OnMelt.");
            OnMelt();
        }
        else if (!timelineCtrl.TryPlay(out var timelineError))
        {
            this.LogError($"[Level52Ctrl] Timeline play failed: {timelineError}. Fallback to OnMelt.");
            OnMelt();
        }

        iceCollider.enabled = false;

        if (iceCollider != null)
        {
            Destroy(iceCollider.GetComponent<Rigidbody2D>());
        }
        this.Log($"OnEnter Fire");
    }

    public void OnMelt()
    {
        doorCollider.enabled = true;
    }

    void OnDestroy()
    {
        if (fire)
            fire.transform.DOKill();

        if (iceCollider)
            iceCollider.DOKill();
    }


    public void PlayBreakIce()
    {
        var inputPosition = Vector3.zero;
        inputPosition = Input.mousePosition;
        if (Input.touchCount > 0)
        {
            inputPosition = Input.GetTouch(0).position;
        }

        var worldPos = mainCamera.ScreenToWorldPoint(inputPosition);
        worldPos.z = 0;
        var obj = Instantiate(knock, transform);
        obj.gameObject.SetActive(true);
        obj.transform.position = worldPos;

        AudioManager.PlaySound("trap", volume: 0.1f);
        iceCollider.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 1, 0.5f).OnComplete(() =>
        {
            iceCollider.transform.localScale = Vector3.one;
        });

        currentCrashCount++;

        if (crashFinished)
            return;

        for (int i = 0; i < iceCrashSprites.Length; i++)
        {
            if (currentCrashCount >= crashLimitCount[i] && currentCrashIndex < i)
            {
                iceCrash.gameObject.SetActive(true);
                iceCrash.sprite = iceCrashSprites[i];

                currentCrashIndex = i;

                if (i == iceCrashSprites.Length - 1)
                    crashFinished = true;

                AudioManager.PlaySound(string.Format("iceCrash{0}", i + 1));
            }
        }

        this.Log("currentCrashCount: " + currentCrashCount + ": " + currentCrashIndex);
    }
}
