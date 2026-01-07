using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ClickScaleAnim : ButtonClickTrigger
{
    public float Scale = 0.95f;
    public float Delay = 0.1f;
    public float Duration = 0.2f;
    
    private Tweener _downTweener;
    private Tweener _upTweener;

    public override void OnButtonDown(bool hasAnim)
    {
        ResetTween();
        _downTweener = transform.DOScale(Vector3.one * Scale, Duration).SetDelay(Delay);
    }

    public override void OnButtonUp(bool hasAnim)
    {
        ResetTween();
        _upTweener = transform.DOScale(Vector3.one, Duration).SetDelay(Delay);
    }

    private void ResetTween()
    {
        _downTweener?.Kill();
        _upTweener?.Kill();
        transform.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        ResetTween();
    }
}
