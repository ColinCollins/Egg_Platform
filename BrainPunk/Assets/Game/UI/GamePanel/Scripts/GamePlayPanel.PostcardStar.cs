using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 实现功能，PlayStar, 通过 postcardStar 创建一个实例，然后播放飞行效果。从 startPosition 按照弧线，飞到 starBlock 位置，并触发 starBlock 刷新/
/// startPosition 通过场景中获得的 star worldPosition 转换到 screenPosition 获得。
/// </summary>
public partial class GamePlayPanel
{
    private const float PostcardStarJumpOutDuration = 0.5f;

    #region Serialized Fields

    /// <summary>
    /// 明信片星星对应的 HUD 道具块；面板打开时保持隐藏，仅在收集事件到达后展示，并在 UI 星星飞完后刷新数量。
    /// </summary>
    [SerializeField] private PropBlock starBlock;

    /// <summary>
    /// 明信片星星 UI 飞行动画预制体；由 GamePlayPanel 在当前 Canvas 下创建实例，未绑定时只执行星星块刷新降级。
    /// </summary>
    [SerializeField] private GameObject postcardStar;

    /// <summary>
    /// UI 星星抵达 HUD 图标后额外播放的特效列表，可同时配置多个 UI 或粒子特效。
    /// </summary>
    [SerializeField] private List<GameObject> postcardStarArriveEffects = new List<GameObject>();

    /// <summary>
    /// UI 星星从场景星星位置飞到 HUD 星星块的耗时；只影响表现，不改变奖励、存档或事件计数。
    /// </summary>
    [SerializeField] private float postcardStarFlyDuration = 0.9f;

    /// <summary>
    /// UI 星星弧线高度，单位为当前 Canvas 本地坐标；用于让飞行轨迹有轻微抛物线表现。
    /// </summary>
    [SerializeField] private float postcardStarArcHeight = 180f;

    /// <summary>
    /// UI 星星飞行和缩放缓动；调用边界仅限本面板的明信片星星表现。
    /// </summary>
    [SerializeField] private Ease postcardStarFlyEase = Ease.InOutSine;

    /// <summary>
    /// UI 星星在飞行尾段开始淡出前的额外提前量；越大越早消失。
    /// </summary>
    [SerializeField] private float postcardStarArriveFadeDelay = 0f;

    /// <summary>
    /// UI 星星飞到目标前尾段淡出的时间，抵达时本体应已不可见。
    /// </summary>
    [SerializeField] private float postcardStarArriveFadeDuration = 0.08f;

    /// <summary>
    /// 抵达目标时额外播放的脉冲特效持续时间。
    /// </summary>
    [SerializeField] private float postcardStarArriveEffectDuration = 0.45f;

    /// <summary>
    /// 抵达目标时额外脉冲特效的最大放大倍率。
    /// </summary>
    [SerializeField] private float postcardStarArriveEffectScaleMultiplier = 1.45f;

    /// <summary>
    /// 抵达粒子特效相对 HUD Canvas 的排序偏移，确保场景粒子能盖在星星栏上层。
    /// </summary>
    [SerializeField] private int postcardStarArriveParticleSortingOrderOffset = 80;

    /// <summary>
    /// 收集星星后刷新完成继续停留的时间；调用边界是 UI 星星飞行动画完成并刷新 PropBlock 之后。
    /// </summary>
    [SerializeField] private float starBlockCollectHoldDuration = 2f;

    /// <summary>
    /// 星星道具块淡入淡出的耗时；运行时缺少 CanvasGroup 时会在玩法面板生命周期内补齐。
    /// </summary>
    [SerializeField] private float starBlockFadeDuration = 0.2f;

    /// <summary>
    /// 星星飞行动画完成后道具块的缩放反馈耗时；只作用于当前 starBlock 个体，不影响其他 PropBlock。
    /// </summary>
    [SerializeField] private float starBlockScaleDuration = 0.45f;

    /// <summary>
    /// 星星飞行动画完成后道具块的放大倍率；小于 1 时会按 1 处理，避免把 HUD 压缩到异常尺寸。
    /// </summary>
    [SerializeField] private float starBlockScaleMultiplier = 1.25f;

    #endregion

    #region Runtime State

    /// <summary>
    /// 默认 alpha = 0
    /// </summary>
    private CanvasGroup starBlockCanvasGroup;
    private PropBlockNotice starBlockNotice;
    private Sequence starBlockSequence;
    private Sequence starBlockScaleSequence;
    private Vector3 starBlockOriginalScale = Vector3.one;
    private bool hasStarBlockOriginalScale;
    private readonly List<Sequence> postcardStarSequences = new List<Sequence>();
    private readonly List<RectTransform> currentPostcardStars = new List<RectTransform>();
    private readonly List<Sequence> postcardStarArriveEffectSequences = new List<Sequence>();
    private readonly List<GameObject> currentPostcardStarArriveEffects = new List<GameObject>();
    private int postcardStarCleanupVersion;

    #endregion

    #region Event Handling

    private void OnGetStar(PostcardGetStarEvent evt)
    {
        if (!Application.isPlaying)
            return;

        PlayPostcardStarAsync(evt.StarWorldPosition, evt.IsJumpOut).Forget();
    }

    #endregion

    #region Animation Flow

    private void HidePostcardStarBlockAtLevelStart()
    {
        if (!Application.isPlaying || !TryResolveStarBlockRefs())
            return;

        StopStarBlockSequence(true);
        StopStarBlockScaleFeedback(true);
        SetStarBlockVisibleState(false);
    }

    /// <summary>
    /// 入口：接收场景星星的世界坐标，创建 UI 飞星并播放到 HUD 星星图标的表现。
    /// </summary>
    /// <param name="starWorldPosition">场景中被收集明信片星星的世界坐标，只作为 UI 飞星起点，不代表 HUD 坐标。</param>
    /// <param name="isJumpOut">为 true 时先播放 0.5 秒原地跳出表现，再进入原飞行动画。</param>
    private async UniTaskVoid PlayPostcardStarAsync(Vector3 starWorldPosition, bool isJumpOut)
    {
        int cleanupVersion = postcardStarCleanupVersion;
        bool playedArrivalFeedback = false;

        ShowStarBlockForCollecting();

        if (TryCreatePostcardStar(starWorldPosition, out RectTransform starRect))
        {
            playedArrivalFeedback = await PlayPostcardStarFlightAsync(starRect, cleanupVersion, isJumpOut);
        }

        if (!CanContinuePostcardStar(cleanupVersion))
            return;

        if (!playedArrivalFeedback)
            RefreshPulseAndHideStarBlock();
    }

    private bool TryCreatePostcardStar(Vector3 starWorldPosition, out RectTransform starRect)
    {
        starRect = null;
        if (postcardStar == null || starBlock == null)
            return false;

        RectTransform parentRect = ResolvePostcardStarParent();
        if (parentRect == null)
            return false;

        if (!TryWorldToCanvasLocalPoint(starWorldPosition, parentRect, out Vector2 startPoint))
            return false;

        GameObject starInstance = Instantiate(postcardStar, parentRect);
        starInstance.SetActive(true);
        if (!starInstance.TryGetComponent(out starRect))
        {
            Destroy(starInstance);
            return false;
        }

        starRect.anchoredPosition = startPoint;
        starRect.localScale = Vector3.one;
        starRect.localRotation = Quaternion.identity;
        starRect.SetAsLastSibling();
        ResolveCanvasGroup(starRect).alpha = 1f;
        currentPostcardStars.Add(starRect);
        return true;
    }

    /// <summary>
    /// 播放 UI 飞星移动。该方法会把 HUD 目标解析为同一 Canvas 父节点下的本地坐标和目标缩放，
    /// 后续飞行终点、淡出点和到达特效都复用这组坐标，避免重复做世界/Canvas 坐标转换。
    /// </summary>
    /// <param name="starRect">运行时实例化出来的 UI 飞星 RectTransform。</param>
    /// <param name="cleanupVersion">面板飞星清理版本；版本变化时停止后续到达反馈。</param>
    /// <param name="isJumpOut">是否在 fly 前播放 0.5 秒原地跳出表现。</param>
    private async UniTask<bool> PlayPostcardStarFlightAsync(RectTransform starRect, int cleanupVersion, bool isJumpOut)
    {
        if (starRect == null)
            return false;

        RectTransform parentRect = starRect.parent as RectTransform;
        if (parentRect == null || !TryGetPostcardStarFlyTarget(parentRect, starRect, out Vector2 targetPoint, out Vector3 targetScale))
        {
            DestroyPostcardStarInstance(starRect);
            return false;
        }

        Vector2 startPoint = starRect.anchoredPosition;
        Vector2 controlPoint = (startPoint + targetPoint) * 0.5f + Vector2.up * postcardStarArcHeight;
        float duration = Mathf.Max(0.01f, postcardStarFlyDuration);
        float fadeDuration = Mathf.Max(0.01f, postcardStarArriveFadeDuration);
        float fadeDelay = Mathf.Max(0f, postcardStarArriveFadeDelay);
        float fadeStartTime = Mathf.Max(0f, duration - fadeDuration - fadeDelay);
        float jumpOutDuration = isJumpOut ? PostcardStarJumpOutDuration * 2f : 0f;
        CanvasGroup starCanvasGroup = ResolveCanvasGroup(starRect);
        bool playedArrivalFeedback = false;

        Sequence sequence = DOTween.Sequence()
            .SetTarget(starRect)
            .SetUpdate(true);

        if (isJumpOut)
        {
            sequence.Append(starRect.DOJumpAnchorPos(
                startPoint,
                postcardStarArcHeight,
                1,
                PostcardStarJumpOutDuration));

            sequence.AppendInterval(PostcardStarJumpOutDuration);
        }

        sequence
            .Append(DOTween.To(
                () => 0f,
                t => starRect.anchoredPosition = EvaluateQuadraticBezier(startPoint, controlPoint, targetPoint, t),
                1f,
                duration).SetEase(postcardStarFlyEase))
            .Join(starRect.DOScale(targetScale, duration).SetEase(postcardStarFlyEase))
            .Insert(jumpOutDuration + fadeStartTime, starCanvasGroup.DOFade(0f, fadeDuration).SetEase(Ease.OutSine))
            .AppendCallback(() =>
            {
                if (!CanContinuePostcardStar(cleanupVersion))
                    return;

                if (starRect != null)
                {
                    starRect.SetAsLastSibling();
                    starCanvasGroup.alpha = 0f;
                }

                PlayPostcardStarArriveEffect(parentRect, targetPoint, targetScale);
                RefreshPulseAndHideStarBlock();
                playedArrivalFeedback = true;
            })
            .OnKill(() =>
            {
                postcardStarSequences.Remove(sequence);
            });

        postcardStarSequences.Add(sequence);

        await sequence.AsyncWaitForCompletion();

        DestroyPostcardStarInstance(starRect);
        return playedArrivalFeedback;
    }

    private void ShowStarBlockForCollecting()
    {
        if (!TryResolveStarBlockRefs())
            return;

        StopStarBlockSequence(false);
        SetStarBlockVisibleState(true);

        if (starBlockCanvasGroup == null)
            return;

        Sequence sequence = DOTween.Sequence()
            .SetTarget(starBlockCanvasGroup)
            .SetUpdate(true);

        sequence
            .Append(starBlockCanvasGroup.DOFade(1f, GetStarBlockFadeDuration()))
            .OnKill(() =>
            {
                if (starBlockSequence == sequence)
                    starBlockSequence = null;
            });

        starBlockSequence = sequence;
    }

    private void RefreshPulseAndHideStarBlock()
    {
        if (!TryResolveStarBlockRefs())
            return;

        StopStarBlockSequence(false);
        SetStarBlockVisibleState(true);
        starBlock.PlayRewardFeedback(1);
        PlayStarBlockScaleFeedback();
        StartStarBlockHideAfterHold(starBlockCollectHoldDuration);
    }

    private void DestroyPostcardStarInstance(RectTransform starRect)
    {
        if (starRect == null)
            return;

        currentPostcardStars.Remove(starRect);
        Destroy(starRect.gameObject);
    }

    private void StartStarBlockHideAfterHold(float holdDuration)
    {
        if (starBlockCanvasGroup == null)
            return;

        float waitTime = Mathf.Max(0f, holdDuration);
        float fadeDuration = GetStarBlockFadeDuration();

        Sequence sequence = DOTween.Sequence()
            .SetTarget(starBlockCanvasGroup)
            .SetUpdate(true);

        sequence
            .AppendInterval(waitTime)
            .Append(starBlockCanvasGroup.DOFade(0f, fadeDuration))
            .OnComplete(() => SetStarBlockVisibleState(false))
            .OnKill(() =>
            {
                if (starBlockSequence == sequence)
                    starBlockSequence = null;
            });

        starBlockSequence = sequence;
    }

    private void PlayStarBlockScaleFeedback()
    {
        if (starBlock == null)
            return;

        CacheStarBlockOriginalScale();
        StopStarBlockScaleFeedback(false);

        Transform target = starBlock.transform;
        target.localScale = starBlockOriginalScale;

        float duration = Mathf.Max(0.01f, starBlockScaleDuration);
        float scaleMultiplier = Mathf.Max(1f, starBlockScaleMultiplier);
        Vector3 overScale = starBlockOriginalScale * scaleMultiplier;
        Vector3 reboundScale = starBlockOriginalScale * Mathf.Clamp(1f - (scaleMultiplier - 1f) * 0.35f, 0.86f, 1f);

        Sequence sequence = DOTween.Sequence()
            .SetTarget(target)
            .SetUpdate(true);

        sequence
            .Append(target.DOScale(overScale, duration * 0.42f).SetEase(Ease.OutBack))
            .Append(target.DOScale(reboundScale, duration * 0.23f).SetEase(Ease.InOutSine))
            .Append(target.DOScale(starBlockOriginalScale, duration * 0.35f).SetEase(Ease.OutBack))
            .OnKill(() =>
            {
                if (starBlockScaleSequence == sequence)
                    starBlockScaleSequence = null;
            });

        starBlockScaleSequence = sequence;
    }

    #endregion

    #region StarBlock Helpers

    private bool TryResolveStarBlockRefs()
    {
        if (starBlock == null)
            return false;

        if (starBlockCanvasGroup == null)
            starBlockCanvasGroup = starBlock.GetComponent<CanvasGroup>();

        if (starBlockCanvasGroup == null)
            starBlockCanvasGroup = starBlock.gameObject.AddComponent<CanvasGroup>();

        if (starBlockNotice == null)
            starBlockNotice = starBlock.GetComponent<PropBlockNotice>();

        CacheStarBlockOriginalScale();
        return true;
    }

    private void CacheStarBlockOriginalScale()
    {
        if (hasStarBlockOriginalScale || starBlock == null)
            return;

        starBlockOriginalScale = starBlock.transform.localScale;
        hasStarBlockOriginalScale = true;
    }

    private void SetStarBlockVisibleState(bool isVisible)
    {
        if (starBlockCanvasGroup == null)
            return;

        starBlockCanvasGroup.alpha = isVisible ? 1f : 0f;
        starBlockCanvasGroup.interactable = false;
        starBlockCanvasGroup.blocksRaycasts = false;
    }

    private float GetStarBlockFadeDuration()
    {
        return Mathf.Max(0f, starBlockFadeDuration);
    }

    private void StopStarBlockSequence(bool hideImmediately)
    {
        if (starBlockNotice != null)
            starBlockNotice.StopAndHide();

        if (starBlockSequence != null && starBlockSequence.IsActive())
            starBlockSequence.Kill();

        starBlockSequence = null;

        if (starBlockCanvasGroup != null)
        {
            starBlockCanvasGroup.DOKill();
            if (hideImmediately)
                SetStarBlockVisibleState(false);
        }
    }

    private void StopStarBlockScaleFeedback(bool restoreScale)
    {
        if (starBlockScaleSequence != null && starBlockScaleSequence.IsActive())
            starBlockScaleSequence.Kill();

        starBlockScaleSequence = null;

        if (restoreScale && starBlock != null && hasStarBlockOriginalScale)
            starBlock.transform.localScale = starBlockOriginalScale;
    }

    #endregion

    #region Coordinate Helpers

    /// <summary>
    /// 获取玩法 HUD 星星道具块的世界坐标；供 UI 星星飞行动画作为目标，未绑定时返回 false 并允许调用方走降级刷新。
    /// </summary>
    public bool TryGetStarBlockWorldPosition(out Vector3 position)
    {
        if (starBlock == null)
        {
            position = default;
            return false;
        }

        RectTransform targetRect = ResolvePostcardStarTargetRect();
        position = targetRect != null
            ? targetRect.TransformPoint(targetRect.rect.center)
            : starBlock.transform.position;
        return true;
    }

    private RectTransform ResolvePostcardStarParent()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return transform as RectTransform;

        return canvas.transform as RectTransform;
    }

    /// <summary>
    /// 解析 HUD 星星图标在飞星父节点坐标系下的位置和大小。
    /// </summary>
    /// <param name="parentRect">飞星所在 Canvas/父节点坐标系，返回的 localPoint 只对这个父节点有效。</param>
    /// <param name="starRect">当前飞星实例，用于按自身尺寸计算贴合 HUD 图标的缩放。</param>
    /// <param name="localPoint">HUD 图标中心点在 parentRect 下的本地坐标，可直接用于 anchoredPosition。</param>
    /// <param name="targetScale">飞星缩放到 HUD 图标尺寸时应使用的 localScale。</param>
    private bool TryGetPostcardStarFlyTarget(RectTransform parentRect, RectTransform starRect, out Vector2 localPoint, out Vector3 targetScale)
    {
        localPoint = default;
        targetScale = Vector3.one;

        RectTransform targetRect = ResolvePostcardStarTargetRect();
        if (targetRect == null || parentRect == null || starRect == null)
            return false;

        localPoint = parentRect.InverseTransformPoint(targetRect.TransformPoint(targetRect.rect.center));
        targetScale = CalculatePostcardStarTargetScale(parentRect, starRect, targetRect);
        return true;
    }

    private RectTransform ResolvePostcardStarTargetRect()
    {
        if (starBlock == null)
            return null;

        if (starBlock.TryGetIconTarget(out RectTransform iconTarget))
            return iconTarget;

        return starBlock.transform as RectTransform;
    }

    private static Vector3 CalculatePostcardStarTargetScale(RectTransform parentRect, RectTransform starRect, RectTransform targetRect)
    {
        Vector2 targetSize = CalculateRectSizeInParent(parentRect, targetRect);
        Vector2 starSize = starRect.rect.size;
        if (targetSize.x <= 0f || targetSize.y <= 0f || starSize.x <= 0f || starSize.y <= 0f)
            return Vector3.one;

        float scale = Mathf.Min(targetSize.x / starSize.x, targetSize.y / starSize.y);
        return Vector3.one * Mathf.Max(0.01f, scale);
    }

    private static Vector2 CalculateRectSizeInParent(RectTransform parentRect, RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Vector2 min = parentRect.InverseTransformPoint(corners[0]);
        Vector2 max = min;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 point = parentRect.InverseTransformPoint(corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return max - min;
    }

    /// <summary>
    /// 在飞星抵达 HUD 图标时播放到达特效。
    /// </summary>
    /// <param name="parentRect">飞星和 UI 到达特效共同使用的 Canvas/父节点坐标系。</param>
    /// <param name="targetPoint">HUD 图标中心点在 parentRect 下的本地坐标，不是世界坐标。</param>
    /// <param name="targetScale">按 HUD 图标尺寸算出的缩放，UI 到达特效用它对齐图标大小。</param>
    private void PlayPostcardStarArriveEffect(RectTransform parentRect, Vector2 targetPoint, Vector3 targetScale)
    {
        if (parentRect == null)
            return;

        bool playedConfiguredEffect = PlayConfiguredPostcardStarArriveEffects(parentRect, targetPoint, targetScale);
        if (!playedConfiguredEffect && postcardStar != null)
            PlayPostcardStarArriveEffectPrefab(postcardStar, parentRect, targetPoint, targetScale);
    }

    /// <summary>
    /// 播放面板 prefab 上配置的到达特效列表。返回 false 表示没有有效配置，调用方会回退播放 postcardStar。
    /// </summary>
    private bool PlayConfiguredPostcardStarArriveEffects(RectTransform parentRect, Vector2 targetPoint, Vector3 targetScale)
    {
        bool playedEffect = false;
        if (postcardStarArriveEffects != null)
        {
            for (int i = 0; i < postcardStarArriveEffects.Count; i++)
            {
                GameObject effectPrefab = postcardStarArriveEffects[i];
                if (effectPrefab == null)
                    continue;

                PlayPostcardStarArriveEffectPrefab(effectPrefab, parentRect, targetPoint, targetScale);
                playedEffect = true;
            }
        }

        return playedEffect;
    }

    /// <summary>
    /// 按 prefab 类型实例化单个到达特效：UI RectTransform 特效挂到 parentRect 下用本地坐标播放；
    /// 非 UI/粒子特效使用 parentRect 转出的世界坐标播放，并提升粒子排序到 HUD 上层。
    /// </summary>
    /// <param name="effectPrefab">到达特效 prefab，来自 postcardStarArriveEffects 或 postcardStar 兜底。</param>
    /// <param name="parentRect">Canvas/父节点坐标系，UI 特效挂载于此，非 UI 特效用它换算世界坐标。</param>
    /// <param name="targetPoint">HUD 图标中心点在 parentRect 下的本地坐标。</param>
    /// <param name="targetScale">UI 特效对齐 HUD 图标尺寸时使用的缩放。</param>
    private void PlayPostcardStarArriveEffectPrefab(GameObject effectPrefab, RectTransform parentRect, Vector2 targetPoint, Vector3 targetScale)
    {
        if (effectPrefab == null || parentRect == null)
            return;

        bool isUiEffect = effectPrefab.TryGetComponent(out RectTransform _);
        GameObject effectInstance = isUiEffect ? Instantiate(effectPrefab, parentRect) : Instantiate(effectPrefab);
        effectInstance.SetActive(true);
        
        AudioManager.PlaySound("collectedStar");
        if (!isUiEffect || !effectInstance.TryGetComponent(out RectTransform effectRect))
        {
            effectInstance.transform.position = GetPostcardStarCanvasWorldPoint(parentRect, targetPoint);
            effectInstance.transform.rotation = effectPrefab.transform.rotation;
            effectInstance.transform.localScale = effectPrefab.transform.localScale;
            BoostPostcardStarParticleSorting(effectInstance, parentRect);
            PlayEffectComponents(effectInstance);
            currentPostcardStarArriveEffects.Add(effectInstance);

            Sequence nonRectSequence = DOTween.Sequence()
                .SetTarget(effectInstance)
                .SetUpdate(true);
            nonRectSequence
                .AppendInterval(Mathf.Max(0.01f, postcardStarArriveEffectDuration))
                .OnComplete(() =>
                {
                    currentPostcardStarArriveEffects.Remove(effectInstance);
                    if (effectInstance != null)
                        Destroy(effectInstance);
                })
                .OnKill(() => postcardStarArriveEffectSequences.Remove(nonRectSequence));
            postcardStarArriveEffectSequences.Add(nonRectSequence);
            return;
        }

        effectInstance.transform.SetAsLastSibling();
        effectRect.anchoredPosition = targetPoint;
        effectRect.localRotation = Quaternion.identity;
        effectRect.localScale = targetScale;
        effectRect.SetAsLastSibling();
        PlayEffectComponents(effectInstance);

        // UI 到达特效的显示与销毁由下方 Sequence 统一管理，避免根粒子的 Destroy Stop Action 提前销毁 Tween 目标。
        if (effectInstance.TryGetComponent(out ParticleSystem rootParticleSystem))
        {
            ParticleSystem.MainModule rootMain = rootParticleSystem.main;
            if (rootMain.stopAction == ParticleSystemStopAction.Destroy)
                rootMain.stopAction = ParticleSystemStopAction.None;
        }

        CanvasGroup effectCanvasGroup = ResolveCanvasGroup(effectRect);
        effectCanvasGroup.alpha = 1f;

        float duration = Mathf.Max(0.01f, postcardStarArriveEffectDuration);
        Vector3 effectTargetScale = targetScale * Mathf.Max(1f, postcardStarArriveEffectScaleMultiplier);

        currentPostcardStarArriveEffects.Add(effectRect.gameObject);
        Sequence sequence = DOTween.Sequence()
            .SetTarget(effectRect)
            .SetLink(effectInstance, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);

        sequence
            .Append(effectRect.DOScale(effectTargetScale, duration).SetEase(Ease.OutQuad))
            .Join(effectCanvasGroup.DOFade(0f, duration).SetEase(Ease.OutSine))
            .OnComplete(() =>
            {
                currentPostcardStarArriveEffects.Remove(effectInstance);
                if (effectInstance != null)
                    Destroy(effectInstance);
            })
            .OnKill(() =>
            {
                postcardStarArriveEffectSequences.Remove(sequence);
                currentPostcardStarArriveEffects.Remove(effectInstance);
            });

        postcardStarArriveEffectSequences.Add(sequence);
    }

    private static void PlayEffectComponents(GameObject effectInstance)
    {
        ParticleSystem[] particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
            particleSystems[i].Play(true);

        Animator[] animators = effectInstance.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i].runtimeAnimatorController == null)
                continue;

            animators[i].enabled = true;
            animators[i].Rebind();
            animators[i].Update(0f);
        }
    }

    private void BoostPostcardStarParticleSorting(GameObject effectInstance, RectTransform parentRect)
    {
        if (effectInstance == null || parentRect == null)
            return;

        Canvas canvas = parentRect.GetComponent<Canvas>();
        if (canvas == null)
            canvas = parentRect.GetComponentInParent<Canvas>();

        int sortingLayerID = canvas != null ? canvas.sortingLayerID : 0;
        int sortingOrder = (canvas != null ? canvas.sortingOrder : 0) + postcardStarArriveParticleSortingOrderOffset;
        ParticleSystemRenderer[] renderers = effectInstance.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingLayerID = sortingLayerID;
            renderers[i].sortingOrder = sortingOrder + i;
        }
    }

    /// <summary>
    /// 将 parentRect 下的本地坐标转换成世界坐标，供非 UI/粒子到达特效定位。
    /// </summary>
    private static Vector3 GetPostcardStarCanvasWorldPoint(RectTransform parentRect, Vector2 localPoint)
    {
        return parentRect.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0f));
    }

    private static CanvasGroup ResolveCanvasGroup(RectTransform rectTransform)
    {
        CanvasGroup canvasGroup = rectTransform.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        return canvasGroup;
    }

    private bool TryWorldToCanvasLocalPoint(Vector3 worldPosition, RectTransform parentRect, out Vector2 localPoint)
    {
        localPoint = default;
        if (parentRect == null)
            return false;

        Camera worldCamera = ResolveWorldCamera();
        if (worldCamera == null)
            return false;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, ResolveCanvasCamera(parentRect), out localPoint);
    }

    private Camera ResolveCanvasCamera(RectTransform canvasRect)
    {
        Canvas canvas = canvasRect != null ? canvasRect.GetComponent<Canvas>() : GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private static Camera ResolveWorldCamera()
    {
        Transform levelCamera = PlayCtrl.Instance != null && PlayCtrl.Instance.LevelCtrl != null
            ? PlayCtrl.Instance.LevelCtrl.transform.Find("GameCamera")
            : null;
        if (levelCamera != null && levelCamera.TryGetComponent(out Camera camera))
            return camera;

        return Camera.main;
    }

    private static Vector2 EvaluateQuadraticBezier(Vector2 startPoint, Vector2 controlPoint, Vector2 targetPoint, float time)
    {
        float inverseTime = 1f - time;
        return inverseTime * inverseTime * startPoint
            + 2f * inverseTime * time * controlPoint
            + time * time * targetPoint;
    }

    #endregion

    #region Cleanup

    private void StopPostcardStarEffects()
    {
        StopPostcardStarFlight();
        StopStarBlockSequence(true);
        StopStarBlockScaleFeedback(true);

        if (starBlockCanvasGroup != null)
            SetStarBlockVisibleState(false);
    }

    private void StopPostcardStarFlight()
    {
        postcardStarCleanupVersion++;
        StopPostcardStarArriveEffect();

        for (int i = postcardStarSequences.Count - 1; i >= 0; i--)
        {
            Sequence sequence = postcardStarSequences[i];
            if (sequence != null && sequence.IsActive())
                sequence.Kill();
        }

        postcardStarSequences.Clear();

        for (int i = currentPostcardStars.Count - 1; i >= 0; i--)
        {
            RectTransform starRect = currentPostcardStars[i];
            if (starRect != null)
                Destroy(starRect.gameObject);
        }

        currentPostcardStars.Clear();
    }

    private void StopPostcardStarArriveEffect()
    {
        for (int i = postcardStarArriveEffectSequences.Count - 1; i >= 0; i--)
        {
            Sequence sequence = postcardStarArriveEffectSequences[i];
            if (sequence != null && sequence.IsActive())
                sequence.Kill();
        }

        postcardStarArriveEffectSequences.Clear();

        for (int i = currentPostcardStarArriveEffects.Count - 1; i >= 0; i--)
        {
            if (currentPostcardStarArriveEffects[i] != null)
                Destroy(currentPostcardStarArriveEffects[i]);
        }

        currentPostcardStarArriveEffects.Clear();
    }

    private bool CanContinuePostcardStar(int cleanupVersion)
    {
        return this != null && isActiveAndEnabled && cleanupVersion == postcardStarCleanupVersion;
    }

    #endregion
}
