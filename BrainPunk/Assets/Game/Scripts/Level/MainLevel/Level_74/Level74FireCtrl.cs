using Bear.Logger;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class Level74FireCtrl : MonoBehaviour, IDebuger
{
    [SerializeField] private GameObject fire;
    [SerializeField] private ParticleSystem flash;
    [SerializeField] private DragEventsListener dragListener;


    [Tooltip("相对段落起点，指针在屏幕上的位移 ≥ 该值（像素）时触发 PlayFlash（仅在可触发状态下）")]
    [SerializeField] private float distance = 1f;

    [Tooltip("触发 PlayFlash 后锁定；相对当前段落起点的位移需先 < 该值（像素）才再次允许触发，或松手重新 BeginDrag")]
    [SerializeField] private float minRearmDistance = 1f;

    private int count;

    [FormerlySerializedAs("MaxFlashCount")]
    [SerializeField] private int maxFlashCount = 3;

    /// <summary>火源视觉成功激活后才为 true；外部仅用于判断 active fire 是否允许点燃草叶。</summary>
    public bool IsActiveFire => isActiveFire;

    private int RequiredFlashCount => Mathf.Max(1, maxFlashCount);

    private Vector2 segmentStartScreen;

    private MelenitasDev.SoundsGood.Sound flashSound;
    private bool isActiveFire;
    private bool isSubscribed;

    /// <summary>false 表示刚触发过 Flash，需位移回到 min 以下或新 Begin 后才可再触发。</summary>
    private bool canFlash = true;

    [SerializeField] private UnityEvent OnFireCallback;

    private void Awake()
    {
        ResolveDragListener();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        BindDragEvents();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        UnbindDragEvents();
        StopFlashFeedback(true);
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        UnbindDragEvents();
        StopFlashFeedback(true);

        if (fire != null)
            fire.transform.DOKill();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (flashSound != null && (flash == null || !flash.isPlaying))
            StopFlashSound();
    }


    /// <summary>对应 Drag Begin：记录当前指针位置为段落起点。</summary>
    private void OnBeginDrag(Transform target)
    {
        if (!CanProcessDrag())
            return;

        segmentStartScreen = dragListener.CurrentScreenPosition;
        canFlash = true;
    }

    /// <summary>
    /// 可触发时：<see cref="distance"/> 达标则 PlayFlash 并锁定，直至相对段落起点位移 &lt; <see cref="minRearmDistance"/> 再重新取点解锁；
    /// <see cref="OnBeginDrag"/> 会强制解锁并取新起点。
    /// </summary>
    private void OnDrag(Transform target)
    {
        if (!CanProcessDrag() || distance <= 0f)
            return;

        Vector2 current = dragListener.CurrentScreenPosition;
        float delta = Vector2.Distance(segmentStartScreen, current);

        Vector3 targetWorldPosition = target != null ? target.position : transform.position;
        bool flashStarted = UpdateFlashPosition(current, targetWorldPosition);
        UpdateFlashSound(flashStarted);

        if (!canFlash)
        {
            if (ShouldRearm(delta))
            {
                canFlash = true;
                segmentStartScreen = current;
            }
            return;
        }

        if (delta < distance)
            return;

        PlayFlash();
        canFlash = false;
    }

    public void PlayFlash()
    {
        if (IsActiveFire)
            return;

        count++;

        if (count >= RequiredFlashCount)
            PlayFire();
    }

    public void PlayFire()
    {
        if (IsActiveFire)
            return;

        if (fire == null)
        {
            count = Mathf.Min(count, RequiredFlashCount - 1);
            canFlash = true;
            StopFlashSound();
            this.LogWarning("[Level74FireCtrl] PlayFire skipped: fire visual is missing.");
            return;
        }

        isActiveFire = true;
        count = RequiredFlashCount;

        fire.SetActive(true);
        fire.transform.DOKill();
        fire.transform.localScale = Vector3.zero;
        fire.transform.DOScale(Vector3.one * 1.5f, 0.2f).SetEase(Ease.OutBack, 3).SetLink(gameObject);

        AudioManager.PlaySound("buildInFire");

        OnFireCallback?.Invoke();

        StopFlashSound();
    }

    public void ResetRuntimeState()
    {
        count = 0;
        isActiveFire = false;
        canFlash = true;
        segmentStartScreen = Vector2.zero;

        StopFlashFeedback(true);

        if (fire != null)
        {
            fire.transform.DOKill();
            fire.transform.localScale = Vector3.one;
            fire.SetActive(false);
        }
    }

    private bool CanProcessDrag()
    {
        return dragListener != null && isActiveAndEnabled && !IsActiveFire;
    }

    private void ResolveDragListener()
    {
        if (dragListener != null)
            return;

        dragListener = GetComponent<DragEventsListener>();
        if (dragListener == null)
            dragListener = GetComponentInChildren<DragEventsListener>(true);
    }

    private void BindDragEvents()
    {
        ResolveDragListener();

        if (dragListener == null || isSubscribed)
            return;

        dragListener.BeginDragHandlers += OnBeginDrag;
        dragListener.DragHandlers += OnDrag;
        isSubscribed = true;
    }

    private void UnbindDragEvents()
    {
        if (!isSubscribed)
            return;

        if (dragListener != null)
        {
            dragListener.BeginDragHandlers -= OnBeginDrag;
            dragListener.DragHandlers -= OnDrag;
        }

        isSubscribed = false;
    }

    private bool ShouldRearm(float delta)
    {
        return minRearmDistance <= 0f || delta < minRearmDistance;
    }

    private Vector2 ResolveFlashScreenPosition(Vector2 fallback, Vector3 targetWorldPosition)
    {
        if (Input.touchCount <= 0)
            return fallback;

        Vector2 best = Input.GetTouch(0).position;
        float bestDistance = Vector3.Distance(ScreenToWorld(best), targetWorldPosition);
        for (int i = 1; i < Input.touchCount; i++)
        {
            Vector2 candidate = Input.GetTouch(i).position;
            float candidateDistance = Vector3.Distance(ScreenToWorld(candidate), targetWorldPosition);
            if (candidateDistance < bestDistance)
            {
                best = candidate;
                bestDistance = candidateDistance;
            }
        }

        return best;
    }

    private bool UpdateFlashPosition(Vector2 fallbackScreenPosition, Vector3 targetWorldPosition)
    {
        if (flash == null)
            return false;

        Vector2 inputPosition = ResolveFlashScreenPosition(fallbackScreenPosition, targetWorldPosition);
        Vector3 worldPosition = ScreenToWorld(inputPosition);
        worldPosition.z = 0f;
        flash.transform.position = worldPosition;
        flash.Play();
        return true;
    }

    private void UpdateFlashSound(bool flashStarted)
    {
        if (!flashStarted || flash == null || !flash.isPlaying || IsActiveFire)
        {
            StopFlashSound();
            return;
        }

        if (flashSound == null)
            flashSound = AudioManager.PlaySound("frictional", loop: true);
    }

    private void StopFlashFeedback(bool clearParticle)
    {
        if (clearParticle && flash != null)
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        StopFlashSound();
    }

    private void StopFlashSound()
    {
        if (flashSound == null)
            return;

        flashSound.Stop();
        flashSound = null;
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position;

        float zDistance = Mathf.Abs(cam.transform.position.z - transform.position.z);
        return cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, zDistance));
    }

}
