using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;
using Bear.UI;
using Spine.Unity;
using Cysharp.Threading.Tasks;
using Bear.EventSystem;
using Game.Events;
using Game.Play;

public class EnterLevelLoading : BaseUIView, IEventSender
{
    [Title("References")]
    [SerializeField] private PageTurnEffect _pageEffect;
    [SerializeField] private BrushClearEffect _brushEffect;

    [Title("Animation Settings")]
    [SerializeField] private float _duration = 1.5f;
    [SerializeField] private float _startDelay = 0.2f;
    [Tooltip("沿路径每段盖章步长（路径索引），越小擦除越密，避免漏擦")]
    [SerializeField] private float _stampPathStep = 0.25f;

    [Title("Path Generation")]
    [SerializeField] private bool _useProceduralPath = true;

    [ShowIf("_useProceduralPath")]
    [SerializeField] private int _spiralTurns = 4;

    [ShowIf("_useProceduralPath")]
    [SerializeField] private int _pathResolution = 100;

    [ShowIf("_useProceduralPath")]
    [SerializeField] private float _maxRadius = 0.8f;

    [HideIf("_useProceduralPath")]
    [SerializeField] private List<Transform> _customPathPoints = new List<Transform>();


    [SerializeField] private float _fadeDuration = 0.2f;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Title("Graphic Scale")]
    [SerializeField] private SkeletonGraphic _graphsic;
    [SerializeField] private float _graphicScaleDelay = 0.35f;
    [SerializeField] private float _graphicScaleFrom = 0.8f;
    [SerializeField] private float _graphicScaleDuration = 0.35f;
    [SerializeField] private Ease _graphicScaleEase = Ease.OutBack;


    [Title("Debug")]
    [SerializeField] private Color _gizmoColor = Color.green;
    [SerializeField] private float _gizmoRadius = 10f;

    private Func<UniTask> _onPageTurnComplete;
    private Action _onEraseComplete;

    private Tween _fadeTween;
    private Tween _scaleTween;
    private Camera _uiCamera;

    private List<Vector2> _currentErasePath;
    private float _eraseProgress;
    private float _eraseDelayTimer;
    private float _lastStampPathIndex;
    private bool _isEraseSequenceActive;

    public override void OnOpen()
    {
        base.OnOpen();
        _uiCamera = GetUICamera();

        if (_brushEffect == null) _brushEffect = GetComponentInChildren<BrushClearEffect>();
        if (_pageEffect == null) _pageEffect = GetComponentInChildren<PageTurnEffect>();

        bool hasPageEffect = _pageEffect != null;

        if (hasPageEffect)
        {
            // 如果有翻页效果，先隐藏刮刮乐层，播放翻页
            // if (_brushEffect != null) _brushEffect.gameObject.SetActive(false);

            _pageEffect.gameObject.SetActive(true);
            _pageEffect.TurnPage(-1.5f, 1.5f, OnPageTurnComplete);

            PlayGraphicScale();
        }
        else
        {
            // 没有翻页效果，直接显示刮刮乐层并开始
            if (_brushEffect != null)
            {
                _brushEffect.gameObject.SetActive(true);
                _brushEffect.ClearMask();
            }
            StartEraseSequence();
        }
    }

    private async UniTask PlayGraphicScale()
    {
        if (_graphsic == null) return;

        await UniTask.WaitForSeconds(_graphicScaleDelay);

        _scaleTween?.Kill();
        var rt = _graphsic.rectTransform;
        rt.localScale = Vector3.one * _graphicScaleFrom;

        _scaleTween = rt.DOScale(1f, _graphicScaleDuration)
            .SetEase(_graphicScaleEase)
            .SetUpdate(true)
            .OnKill(() => _scaleTween = null);
    }

    private void OnPageTurnComplete()
    {
        OnPageTurnCompleteAsync().Forget();
    }

    private async UniTask OnPageTurnCompleteAsync()
    {
        if (_onPageTurnComplete != null)
        {
            await _onPageTurnComplete();
        }

        if (this == null)
        {
            return;
        }

        if (_graphsic != null && _brushEffect != null)
        {
            _graphsic.transform.SetParent(_brushEffect.transform);
        }
        else
        {
            Debug.LogError("[EnterLevelLoading] _graphsic or _brushEffect is null. Aborting transition.");
            CloseSelf();
            return;
        }

        if (_pageEffect != null)
        {
            _pageEffect.gameObject.SetActive(false);
        }

        if (_brushEffect != null)
        {
            _brushEffect.gameObject.SetActive(true);
            // 激活后 OnEnable 会自动 ResetMaskRT (带圆圈)，所以这里必须手动 ClearMask
            _brushEffect.ClearMask();
        }

        StartEraseSequence();
    }

    private Camera GetUICamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return canvas.worldCamera;
        }
        return null;
    }

    public void StartEraseSequence()
    {
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);

        List<Vector2> path = _useProceduralPath ? GenerateSpiralPath() : ConvertTransformsToUV(_customPathPoints);

        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("[EnterLevelLoading] Path is empty or too short. Closing immediately.");
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
            CloseSelf();
            return;
        }

        _currentErasePath = path;
        _eraseProgress = 0f;
        _eraseDelayTimer = 0f;
        _lastStampPathIndex = -1f;
        _isEraseSequenceActive = true;
    }

    private void Update()
    {
        if (!_isEraseSequenceActive || _currentErasePath == null || _currentErasePath.Count < 2)
        {
            return;
        }

        float delta = Time.unscaledDeltaTime;

        if (_eraseDelayTimer < _startDelay)
        {
            _eraseDelayTimer += delta;
            return;
        }

        if (_duration <= 0f)
        {
            Vector2 lastPos = _currentErasePath[_currentErasePath.Count - 1];
            _brushEffect.StampEraseAt(lastPos);
            CompleteEraseSequence();
            return;
        }

        _eraseProgress += delta / _duration;
        if (_eraseProgress > 1f) _eraseProgress = 1f;

        float linearIndex = Mathf.Lerp(0f, _currentErasePath.Count - 1, _eraseProgress);
        float startIndex = _lastStampPathIndex < 0f ? 0f : _lastStampPathIndex;
        float step = Mathf.Max(0.01f, _stampPathStep);

        for (float fi = startIndex; fi < linearIndex; fi += step)
        {
            float clampFi = Mathf.Min(fi, _currentErasePath.Count - 1);
            int index = Mathf.FloorToInt(clampFi);
            float t = clampFi - index;
            Vector2 pos;
            if (index >= _currentErasePath.Count - 1)
                pos = _currentErasePath[_currentErasePath.Count - 1];
            else
                pos = Vector2.Lerp(_currentErasePath[index], _currentErasePath[index + 1], t);
            _brushEffect.StampEraseAt(pos);
        }

        float endIdx = Mathf.Min(linearIndex, _currentErasePath.Count - 1);
        int i = Mathf.FloorToInt(endIdx);
        float tr = endIdx - i;
        Vector2 endPos = i >= _currentErasePath.Count - 1
            ? _currentErasePath[_currentErasePath.Count - 1]
            : Vector2.Lerp(_currentErasePath[i], _currentErasePath[i + 1], tr);
        _brushEffect.StampEraseAt(endPos);

        _lastStampPathIndex = linearIndex;

        if (_eraseProgress >= 1f)
        {
            CompleteEraseSequence();
        }
    }

    private void CompleteEraseSequence()
    {
        _isEraseSequenceActive = false;
        _currentErasePath = null;
        OnEraseComplete();
    }

    private List<Vector2> ConvertTransformsToUV(List<Transform> transforms)
    {
        if (transforms == null) return null;
        var list = new List<Vector2>();
        foreach (var t in transforms)
        {
            if (t == null) continue;
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(_uiCamera, t.position);
            list.Add(new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height));
        }
        return list;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = _gizmoColor;

        if (_useProceduralPath)
        {
            if (_brushEffect == null) _brushEffect = GetComponentInChildren<BrushClearEffect>();
            if (_brushEffect == null) return;

            var rt = _brushEffect.GetComponent<RectTransform>();
            if (rt == null) return;

            var path = GenerateSpiralPath();
            if (path == null || path.Count < 2) return;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            // 0: bottom-left, 1: top-left, 2: top-right, 3: bottom-right
            // UV (0,0) -> corners[0]
            // UV (1,0) -> corners[3]
            // UV (0,1) -> corners[1]

            Vector3 origin = corners[0];
            Vector3 right = corners[3] - corners[0];
            Vector3 up = corners[1] - corners[0];

            Vector3 prevPos = origin + right * path[0].x + up * path[0].y;
            Gizmos.DrawSphere(prevPos, _gizmoRadius * 0.5f);

            for (int i = 1; i < path.Count; i++)
            {
                Vector3 currPos = origin + right * path[i].x + up * path[i].y;
                Gizmos.DrawLine(prevPos, currPos);
                prevPos = currPos;
            }
            Gizmos.DrawSphere(prevPos, _gizmoRadius * 0.5f);
        }
        else
        {
            if (_customPathPoints == null || _customPathPoints.Count < 2) return;

            for (int i = 0; i < _customPathPoints.Count - 1; i++)
            {
                var p1 = _customPathPoints[i];
                var p2 = _customPathPoints[i + 1];
                if (p1 != null && p2 != null)
                {
                    Gizmos.DrawLine(p1.position, p2.position);
                    Gizmos.DrawSphere(p1.position, _gizmoRadius);
                }
            }
            if (_customPathPoints[_customPathPoints.Count - 1] != null)
                Gizmos.DrawSphere(_customPathPoints[_customPathPoints.Count - 1].position, _gizmoRadius);
        }
    }

#endif 

    private void OnEraseComplete()
    {
        _fadeTween?.Kill();
        _canvasGroup.alpha = 1f;
        _canvasGroup.DOKill();
        _fadeTween = _canvasGroup.DOFade(0f, _fadeDuration).SetUpdate(true).OnComplete(() =>
        {
            _fadeTween = null;
            CloseSelf();

            if (_onEraseComplete != null)
                _onEraseComplete();
            else
                this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        });

    }

    private void CloseSelf()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.DestroyUI(this);
        }
        else
        {
            // Fallback if UIManager is not ready or in test scene
            gameObject.SetActive(false);
        }
    }

    private List<Vector2> GenerateSpiralPath()
    {
        var points = new List<Vector2>();
        Vector2 center = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < _pathResolution; i++)
        {
            float t = (float)i / (_pathResolution - 1); // 0 to 1

            // 角度：从 0 旋转到 2*PI * turns
            float angle = t * Mathf.PI * 2 * _spiralTurns;

            // 半径：从 0 扩散到 maxRadius
            float radius = t * _maxRadius;

            // 简单的极坐标转换
            float x = center.x + Mathf.Cos(angle) * radius;
            float y = center.y + Mathf.Sin(angle) * radius;

            points.Add(new Vector2(x, y));
        }

        return points;
    }

    public override void OnClose()
    {
        base.OnClose();
        _scaleTween?.Kill();
        _isEraseSequenceActive = false;
        _currentErasePath = null;
    }

    private void OnDestroy()
    {
        _scaleTween?.Kill();
        _isEraseSequenceActive = false;
        _currentErasePath = null;
    }

    public static EnterLevelLoading Create(Func<UniTask> onPageTurnComplete = null, Action onEraseComplete = null)
    {
        var panel = UIManager.Instance.OpenUI<EnterLevelLoading>(nameof(EnterLevelLoading), UILayer.Popup, false);
        panel._onPageTurnComplete = onPageTurnComplete;
        panel._onEraseComplete = onEraseComplete;
        return panel;
    }
}
