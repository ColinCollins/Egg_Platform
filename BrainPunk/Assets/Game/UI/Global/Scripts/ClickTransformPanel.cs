using System;
using UnityEngine;
using UnityEngine.UI;
using Bear.UI;
using Bear.Logger;
using DG.Tweening;

public partial class ClickTransformPanel : BaseUIView, IDebuger
{
    [SerializeField] private Graphic _circleMaskGraphic;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Animator _door;

    [SerializeField] private float _waitingDuration = 0.3f;
    [SerializeField] private float _radiusDuration = 0.3f;
    [SerializeField] private float _fadeDuration = 0.2f;

    private static readonly int ScreenPosId = Shader.PropertyToID("_ScreenPos");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");

    private float _radius;
    private Action _onComplete;
    private Tweener _radiusTween;
    private Tween _fadeTween;

    public override void OnCreate()
    {
        base.OnCreate();
    }

    public override void OnOpen()
    {
        Vector2 screenPos = Input.touchCount > 0
            ? (Vector2)Input.GetTouch(0).position
            : (Vector2)Input.mousePosition;
        SetCirclePositionFromScreenPixel(screenPos.x, screenPos.y);
        _radiusTween?.Kill();

        _door.SetTrigger("Show");
        _radius = -0.1f;
        SetCircleRadius(_radius);
        _radiusTween = DOTween.To(() => _radius, x =>
        {
            _radius = x;
            SetCircleRadius(x);
        }, 3f, _radiusDuration).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(CompleteAnimation);
    }

    /// <summary>
    /// 半径动画结束后：先执行回调，再反向播放 _radiusTween（0→3），播完关闭界面。
    /// </summary>
    private new void CompleteAnimation()
    {
        _radiusTween = null;
        var cb = _onComplete;
        _onComplete = null;

        // _door.SetActive(true);
        cb?.Invoke();

        // fade 效果注释掉，改用反向播放 _radiusTween
        // _fadeTween?.Kill();
        // _canvasGroup.alpha = 1f;
        // _canvasGroup.DOKill();
        // _fadeTween = _canvasGroup.DOFade(0f, _fadeDuration).SetDelay(_waitingDuration).OnComplete(() =>
        // {
        //     _fadeTween = null;
        //     _door.SetActive(false);
        //     UIManager.Instance.CloseUI(this);
        // });

        _radius = 3f;
        SetCircleRadius(_radius);
        SetCirclePositionFromScreenPixel(Screen.width / 2f, Screen.height / 2f);
        _radiusTween = DOTween.To(() => _radius, x =>
        {
            _radius = x;
            SetCircleRadius(x);
        }, -0.1f, _fadeDuration).SetDelay(_waitingDuration).SetUpdate(true).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            _radiusTween = null;
            // _door.SetActive(false);
            UIManager.Instance.CloseUI(this);
        });
    }

    void OnDisable()
    {
        OnDestroy();
    }

    private void OnDestroy()
    {
        _radiusTween?.Kill();
        _radiusTween = null;
        _fadeTween?.Kill();
        _fadeTween = null;
        _onComplete = null;
    }

    /// <summary>
    /// 设置镂空圆心位置（屏幕 UV 0-1）。用于驱动 ClickTransform shader 的 _ScreenPos。
    /// </summary>
    public void SetCirclePosition(Vector2 screenUV01)
    {
        if (_circleMaskGraphic == null || _circleMaskGraphic.material == null)
        {
            this.LogWarning("[ClickTransformPanel] SetCirclePosition: _circleMaskGraphic or material is null.");
            return;
        }

        if (!_circleMaskGraphic.material.HasProperty(ScreenPosId))
        {
            this.LogWarning("[ClickTransformPanel] SetCirclePosition: material has no _ScreenPos.");
            return;
        }

        _circleMaskGraphic.material.SetVector(ScreenPosId, new Vector4(screenUV01.x, screenUV01.y, 0f, 0f));
    }

    /// <summary>
    /// 将屏幕像素坐标转为 0-1 UV 并设置圆心。与 shader 约定一致：左下 (0,0)，右上 (1,1)。
    /// </summary>
    public void SetCirclePositionFromScreenPixel(float pixelX, float pixelY)
    {
        float w = Screen.width;
        float h = Screen.height;
        if (w <= 0f || h <= 0f) return;
        SetCirclePosition(new Vector2(pixelX / w, pixelY / h));
    }

    /// <summary>
    /// 设置镂空圆半径（与 shader 中 _Radius 一致，0-1 屏幕 UV 比例）。
    /// </summary>
    public void SetCircleRadius(float radius)
    {
        if (_circleMaskGraphic == null || _circleMaskGraphic.material == null) return;
        if (!_circleMaskGraphic.material.HasProperty(RadiusId)) return;
        _circleMaskGraphic.material.SetFloat(RadiusId, radius);
    }

    /// <summary>
    /// 半径动画结束后的回调。
    /// </summary>
    public void SetCallback(Action onComplete)
    {
        _onComplete = onComplete;
    }

    public static ClickTransformPanel Create(Action onComplete = null)
    {
        var panel = UIManager.Instance.OpenUI<ClickTransformPanel>(nameof(ClickTransformPanel), UILayer.Top, false);
        if (onComplete != null)
            panel.SetCallback(onComplete);
        panel._canvasGroup.alpha = 1f;

        return panel;
    }
}
