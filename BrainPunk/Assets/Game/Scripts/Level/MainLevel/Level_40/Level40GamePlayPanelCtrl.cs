using Bear.EventSystem;
using Bear.Logger;
using DG.Tweening;
using Game.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Level40GamePlayPanelCtrl : MonoBehaviour, IEventSender, IDebuger
{
    [SerializeField] private Material pageMaterial;

    [SerializeField] private RawImage rawImage;
    [SerializeField] private UIDragHandle dragHandle;

    [Header("Trigger")]
    [SerializeField, Range(0f, 1f)] private float triggerProgressX = 0.35f;
    [SerializeField] private int triggerEventId = 1;
    [SerializeField] private bool dragLeftToOpen = true;

    [Header("End Animation")]
    [SerializeField] private float endAnimDuration = 0.35f;
    [SerializeField] private Ease endAnimEase = Ease.OutCubic;
    [SerializeField] private float startFoldDistance;
    private float _beginFoldDistance;
    private float _progress;
    private float currentProgress;
    private bool _canDrag;
    private bool _isFinished;
    private Tween _endTween;
    private float _cachedRadius;
    private Vector2 _cachedDir;

    private static readonly int DistancePropId = Shader.PropertyToID("_Distance");
    private static readonly int RadiusPropId = Shader.PropertyToID("_Radius");
    private static readonly int AnglePropId = Shader.PropertyToID("_Angle");

    private void Start()
    {
        if (pageMaterial == null)
            return;

        if (dragHandle == null)
            dragHandle = GetComponentInChildren<UIDragHandle>();

        if (dragHandle == null)
            return;

        dragHandle.OnBeginDragEvent += OnBeginDrag;
        dragHandle.OnDragEvent += OnDrag;
        dragHandle.OnEndDragEvent += OnEndDrag;

        pageMaterial.SetFloat(DistancePropId, startFoldDistance);
        _beginFoldDistance = startFoldDistance;
        currentProgress = 0;

        _cachedRadius = Mathf.Max(pageMaterial.GetFloat(RadiusPropId), 1e-5f);
        float angle = pageMaterial.GetFloat(AnglePropId);
        float rad = angle * Mathf.Deg2Rad;
        _cachedDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void TriggerFinish()
    {
        if (_isFinished)
            return;

        Debug.Log("TriggerFinish" + endAnimDuration);
        _isFinished = true;
        _canDrag = false;
        // 结束阶段锁住拖拽 handle，避免继续拖拽扰动参数
        if (dragHandle != null)
            dragHandle.enabled = false;

        this.DispatchEvent(Witness<OnTiggerItemEvent>._, triggerEventId);
        _endTween?.Kill();
        _endTween = DOTween
            .To(
                () => pageMaterial.GetFloat(DistancePropId),
                value => pageMaterial.SetFloat(DistancePropId, value),
                -.5f,
                endAnimDuration)
            .SetEase(endAnimEase)
            .OnComplete(() =>
            {
                if (rawImage)
                    rawImage.gameObject.SetActive(false);
            });
    }

    private void OnBeginDrag(PointerEventData eventData)
    {
        if (_isFinished || pageMaterial == null)
            return;

        _beginFoldDistance = pageMaterial.GetFloat(DistancePropId);

        _canDrag = true;

        if (rawImage != null && TryGetUv(eventData.position, out var uv))
        {
            float d = ComputeSignedDistance(uv);
            _canDrag = d > -_cachedRadius && d < 0;
        }
    }

    private void OnDrag(PointerEventData eventData)
    {
        if (_isFinished || !_canDrag || pageMaterial == null || dragHandle == null)
            return;
        float dx = dragHandle.RelativeDragDelta.x;
        float signedDeltaX = dragLeftToOpen ? -dx : dx;
        float scaledCompleteDragDistanceX = GetScaledCompleteDragDistanceX();
        _progress = Mathf.Clamp(signedDeltaX / Mathf.Max(scaledCompleteDragDistanceX, 1f), -1, 1);
        float realProgress = _progress + currentProgress;
        float foldDistance = Mathf.Lerp(startFoldDistance, 0, realProgress);
        pageMaterial.SetFloat(DistancePropId, foldDistance);

        this.Log("Drag page foldDistance: " + currentProgress);

        if (realProgress >= triggerProgressX)
            TriggerFinish();
    }

    private void OnEndDrag(PointerEventData eventData)
    {
        _canDrag = false;
        currentProgress += _progress;
    }

    private float GetScaledCompleteDragDistanceX()
    {
        return Screen.width;
    }

    private float ComputeSignedDistance(Vector2 uv)
    {
        float foldDistance = pageMaterial.GetFloat(DistancePropId);
        return Vector2.Dot(uv, _cachedDir) - foldDistance;
    }

    private bool TryGetUv(Vector2 screenPos, out Vector2 uv)
    {
        uv = default;
        if (rawImage == null)
            return false;

        var rt = rawImage.rectTransform;
        Camera eventCamera = null;
        var canvas = rawImage.canvas;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, eventCamera, out var local))
            return false;

        var rect = rt.rect;
        if (Mathf.Approximately(rect.width, 0f) || Mathf.Approximately(rect.height, 0f))
            return false;

        uv.x = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
        uv.y = Mathf.Clamp01((local.y - rect.yMin) / rect.height);
        return true;
    }

    private void OnDestroy()
    {
        if (dragHandle != null)
        {
            dragHandle.OnBeginDragEvent -= OnBeginDrag;
            dragHandle.OnDragEvent -= OnDrag;
            dragHandle.OnEndDragEvent -= OnEndDrag;
        }

        _endTween?.Kill();
        _endTween = null;
    }
}
