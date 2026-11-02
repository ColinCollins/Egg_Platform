using System.Collections.Generic;
using DG.Tweening;
using Game.ItemEvent;
using Game.Scripts.Common;
using MelenitasDev.SoundsGood;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DragStart 开始计时, group1 alpha 快速变为 0， group2 alpha 逐渐展示，时间为 5s。若 5s 内 DragEnd 则 group1 和 group2 alpha 重置。
/// group1 alpha = 1 group2 = 0. 重置状态也要有一个快速的过度变化，group1,group2 分别控制。
/// stars 初始化全部scale 设置为 0.再播放到 3s 的时候，开始按照顺序 DOScale 缩放为 1.Ease.OutBack.Delay 0.02f。提前 DragEnd 会被重置
/// Bg在这个过程中颜色逐渐从 dayColor 过度到 nightColor。过渡时间单独可配。提前 DragEnd 也会被重置。
/// door 在结束后按照 0.5f 进行 DOScale = 1 展示。初始化 scale = 0
/// </summary>
public class Level_55Ctrl : MonoBehaviour
{
    [SerializeField] private List<Transform> stars;
    [SerializeField] private SpriteRenderer Bg;
    [SerializeField] private TextMeshPro tips;
    [SerializeField] private Color dayColor;

    [SerializeField] private Color nightColor;

    [SerializeField] private GameObject door;

    [SerializeField] private SpriteRendererGroupAlpha group1;
    [SerializeField] private SpriteRendererGroupAlpha group2;

    [SerializeField] private List<SkeletonAnimation> enterDoor;

    [SerializeField] private MoveFloatHandle doorFlow;

    [SerializeField] private float group1FadeDuration = 0.15f;
    [SerializeField] private float group2FadeDuration = 5f;
    [SerializeField] private float starStartDelay = 3f;
    [SerializeField] private float starScaleDuration = 0.25f;
    [SerializeField] private float starDelayStep = 0.02f;
    [SerializeField] private float bgColorDuration = 5f;
    [SerializeField] private float doorScaleDuration = 0.5f;
    [SerializeField] private float doorStartRotationZ = 90f;
    [SerializeField] private float group1ResetFadeDuration = 0.15f;
    [SerializeField] private float group2ResetFadeDuration = 0.15f;
    [SerializeField] private float dragStartDelay = 0.1f;

    /// <summary>
    /// 误导刷除控制器；调用边界：仅在运行时绑定完成回调，用于驱动本关星星显示计数。
    /// </summary>
    [SerializeField] private Level55MisleadCtrl misleadCtrl;
    /// <summary>
    /// 达到误导刷除次数后显示的星星节点；注意事项：初始进入关卡时会主动隐藏。
    /// </summary>
    [SerializeField] private Transform star;
    [SerializeField] private Transform star2;
    /// <summary>
    /// 星星显示所需的误导刷除完成次数；completeCount 大于等于该值时显示 star。
    /// </summary>
    [SerializeField] private int maxCount = 5;

    private int completeCount;

    private Sequence _dragSequence;
    private Sequence _resetSequence;
    private Tween _dragStartDelayTween;
    private bool _isDragSequenceCompleted;
    private bool _isDragSequenceStarted;
    private bool _isDraging;
    private float _group1Alpha = 1f;
    private float _group2Alpha = 0f;

    private void Awake()
    {
        completeCount = 0;
        if (Application.isPlaying)
        {
            SetMisleadStarVisible(false);
        }

        ResetState(false);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        BindMisleadCallback();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UnbindMisleadCallback();
    }

    public void OnDragStart(Transform target)
    {
        if (_isDraging)
        {
            return;
        }

        _isDraging = true;
        KillDragStartDelay();
        _dragStartDelayTween = DOVirtual.DelayedCall(Mathf.Max(0f, dragStartDelay), () =>
        {
            _dragStartDelayTween = null;
            if (!_isDraging)
            {
                return;
            }

            StartDragSequence();
        }).SetLink(gameObject);
    }

    private void StartDragSequence()
    {
        PlaySound();
        KillSequence();
        KillResetSequence();
        ResetState(false);
        _isDragSequenceCompleted = false;
        _isDragSequenceStarted = true;

        _dragSequence = DOTween.Sequence().SetLink(gameObject);
        _dragSequence.Join(DOVirtual.Float(1f, 0f, Mathf.Max(0f, group1FadeDuration), SetGroup1Alpha));
        _dragSequence.Join(DOVirtual.Float(0f, 1f, Mathf.Max(0f, group2FadeDuration), SetGroup2Alpha));
        _dragSequence.OnComplete(() =>
        {
            _isDragSequenceCompleted = true;
            _isDragSequenceStarted = false;
            PlayDoorShow();
            _dragSequence = null;

            if (star2)
                star2.GetComponent<Collider2D>().excludeLayers = LayerMask.NameToLayer("");
        });

        if (Bg != null)
        {
            _dragSequence.Join(Bg.DOColor(nightColor, Mathf.Max(0f, bgColorDuration)).From(dayColor));
        }

        if (tips != null)
        {
            tips.DOKill();
            _dragSequence.Join(tips.DOFade(0f, Mathf.Max(0f, bgColorDuration)).From(1f));
        }

        if (stars == null)
        {
            return;
        }

        for (int i = 0; i < stars.Count; i++)
        {
            var star = stars[i];
            if (star == null)
            {
                continue;
            }

            star.transform.DOKill();
            _dragSequence.Insert(
                Mathf.Max(0f, starStartDelay) + i * Mathf.Max(0f, starDelayStep),
                star.transform.DOScale(Vector3.one, Mathf.Max(0f, starScaleDuration)).SetEase(Ease.OutBack));
        }
    }

    public void OnDragEnd(Transform target)
    {
        _isDraging = false;
        KillDragStartDelay();
        KillSequence();
        if (_isDragSequenceStarted && !_isDragSequenceCompleted)
        {
            _isDragSequenceStarted = false;
            ResetState(true);
        }

        if (!_isDragSequenceCompleted)
            ResetSound();
    }

    private void OnDestroy()
    {
        UnbindMisleadCallback();
        KillSequence();
        KillResetSequence();
        KillDragStartDelay();
    }

    private void BindMisleadCallback()
    {
        if (misleadCtrl == null)
        {
            misleadCtrl = GetComponent<Level55MisleadCtrl>();
        }

        if (misleadCtrl == null)
        {
            return;
        }

        misleadCtrl.completeCallback -= OnMisleadComplete;
        misleadCtrl.completeCallback += OnMisleadComplete;
    }

    private void UnbindMisleadCallback()
    {
        if (misleadCtrl == null)
        {
            return;
        }

        misleadCtrl.completeCallback -= OnMisleadComplete;
    }

    private void OnMisleadComplete()
    {
        completeCount++;

        if (completeCount >= maxCount)
        {
            SetMisleadStarVisible(true);
        }
    }

    private void SetMisleadStarVisible(bool visible)
    {
        if (star == null)
        {
            return;
        }

        GameObject targetStar = star.gameObject;
        targetStar.SetActive(visible);

        if (visible && targetStar.TryGetComponent(out Postcard_Star postcardStar))
        {
            postcardStar.RefreshSavedCollectionState();
        }
    }

    private void ResetState(bool animateGroupAlpha)
    {
        ResetGroupAlpha(animateGroupAlpha);

        if (door != null)
        {
            door.transform.DOKill();
            door.transform.localScale = Vector3.zero;
            door.transform.localEulerAngles = new Vector3(0f, 0f, doorStartRotationZ);
        }

        if (Bg != null)
        {
            Bg.DOKill();
            Bg.color = dayColor;
        }

        if (tips != null)
        {
            tips.DOKill();
            tips.alpha = 1f;
        }

        if (stars == null)
        {
            return;
        }

        for (int i = 0; i < stars.Count; i++)
        {
            var star = stars[i];
            if (star == null)
            {
                continue;
            }

            star.DOKill();
            star.localScale = Vector3.zero;
        }
    }

    private void ResetGroupAlpha(bool animate)
    {
        KillResetSequence();

        if (!animate)
        {
            SetGroup1Alpha(1f);
            SetGroup2Alpha(0f);
            return;
        }

        _resetSequence = DOTween.Sequence().SetLink(gameObject);
        _resetSequence.Join(DOVirtual.Float(_group1Alpha, 1f, Mathf.Max(0f, group1ResetFadeDuration), SetGroup1Alpha));
        _resetSequence.Join(DOVirtual.Float(_group2Alpha, 0f, Mathf.Max(0f, group2ResetFadeDuration), SetGroup2Alpha));
        _resetSequence.OnComplete(() => _resetSequence = null);
    }

    private void KillSequence()
    {
        if (_dragSequence != null)
        {
            _dragSequence.Kill();
            _dragSequence = null;
        }
    }

    private void KillResetSequence()
    {
        if (_resetSequence != null)
        {
            _resetSequence.Kill();
            _resetSequence = null;
        }
    }

    private void KillDragStartDelay()
    {
        if (_dragStartDelayTween != null)
        {
            _dragStartDelayTween.Kill();
            _dragStartDelayTween = null;
        }
    }

    private void SetGroup1Alpha(float value)
    {
        _group1Alpha = value;

        if (group1 != null)
        {
            group1.SetAlpha(value);
        }

        if (enterDoor == null)
        {
            return;
        }

        for (int i = 0; i < enterDoor.Count; i++)
        {
            var target = enterDoor[i];
            if (target != null && target.Skeleton != null)
            {
                target.Skeleton.A = value;
            }
        }
    }

    private void SetGroup2Alpha(float value)
    {
        _group2Alpha = value;

        if (group2 != null)
        {
            group2.SetAlpha(value);
        }
    }

    private void PlayDoorShow()
    {
        if (door == null)
        {
            return;
        }

        door.transform.DOKill();
        float duration = Mathf.Max(0f, doorScaleDuration);
        door.transform.DOScale(Vector3.one, duration);
        door.transform.DORotate(Vector3.zero, duration).OnComplete(() =>
        {
            doorFlow.StartMove();
        });
    }

    private Sound level55Sound;

    private void PlaySound()
    {
        level55Sound = Game.Scripts.Common.AudioManager.PlaySound("level55");
        Game.Scripts.Common.AudioManager.PauseMusic();
    }

    private void ResetSound()
    {
        if (level55Sound != null)
            level55Sound.Stop();
        Game.Scripts.Common.AudioManager.ResumeMusic();
    }
}
