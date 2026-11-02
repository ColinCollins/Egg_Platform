using System;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;

public class Level74GrassCtrl : MonoBehaviour
{
    private enum LeafState
    {
        Visible,
        Cutting,
        Hidden,
        Burning,
        ScorchedFading,
        RegrowWaiting,
        Regrowing
    }

    private enum RootState
    {
        Present,
        Digging,
        Removed
    }

    [SerializeField] private Transform leave;
    [SerializeField] private Transform root;
    [SerializeField] private Transform hole;
    [SerializeField] private Transform whole;
    [SerializeField] private Level74Ctrl levelCtrl;
    [SerializeField] private float cutDuration = 0.25f;
    [SerializeField] private float cutFadeDuration = 0.25f;
    [SerializeField] private float regrowDelay = 1.2f;
    [SerializeField] private float regrowDuration = 0.25f;
    /// <summary>
    /// 草叶重生开始时相对初始本地坐标向下偏移的距离；仅影响 leaf 运行时视觉，不改变 root、开门、存档或配置流程。
    /// </summary>
    [SerializeField] private float regrowStartOffsetY = 0.45f;
    [SerializeField] private float digFadeDuration = 0.3f;
    [SerializeField] private float popOffsetY = 0.35f;
    [SerializeField] private float fireDuration = 1f;
    /// <summary>
    /// 叶子临时火苗正常结束时的淡出时长；重置、失败、退出等清理路径仍立即销毁火苗，避免残留。
    /// </summary>
    [SerializeField] private float fireEffectFadeDuration = 0.25f;
    /// <summary>
    /// Fire grass 从原始颜色过渡到焦黑色的时长；仅影响 leaf 烧焦变黑过程，不改变 root、开门或清理流程。
    /// </summary>
    [SerializeField] private float fireScorchedDuration = 0.25f;
    /// <summary>
    /// Fire grass 变黑后进入渐隐前的停留时间；仅影响烧草 leaf 视觉节奏，不改变 root、开门或清理流程。
    /// </summary>
    [SerializeField] private float fireScorchedHoldDuration = 0.5f;
    [SerializeField] private float fireFadeDuration = 0.5f;
    [SerializeField] private Vector3 fireEffectWorldOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private float fireEffectScale = 0.7f;

    private LeafState leafState = LeafState.Visible;
    private RootState rootState = RootState.Present;
    private Sequence activeSequence;
    private GameObject fireEffectInstance;
    private bool fireSequenceActive;
    private Vector3 leaveInitialLocalPosition;
    private Vector3 rootInitialLocalPosition;
    private Vector3 leaveInitialLocalScale;
    private Vector3 rootInitialLocalScale;
    private SpriteRenderer[] leaveRenderers;
    private SpriteRenderer[] rootRenderers;
    private Color[] leaveInitialColors;
    private Color[] rootInitialColors;
    private bool initialStateCached;

    public bool IsDead => rootState == RootState.Removed;
    public bool IsAnimating => IsLeafBusy() || rootState == RootState.Digging;

    private void Awake()
    {
        CacheInitialState();
    }

    public bool CanCut()
    {
        EnsureInitialStateCached();
        return !IsDead && !IsAnimating && leafState == LeafState.Visible && leave != null;
    }

    public bool CanDig()
    {
        EnsureInitialStateCached();
        return !IsDead && !IsAnimating && root != null;
    }

    public bool CanBurn()
    {
        EnsureInitialStateCached();
        return !IsDead && !IsAnimating && leafState == LeafState.Visible && leave != null;
    }

    public void OnEnterTrigger(Collider2D collider)
    {
        levelCtrl?.OnGrassEnter(this, collider);
    }

    public void OnExitTrigger(Collider2D collider)
    {
        levelCtrl?.OnGrassExit(this, collider);
    }

    public bool TryCut(Action onComplete = null)
    {
        if (!CanCut())
            return false;

        KillActiveSequence();
        leafState = LeafState.Cutting;
        fireSequenceActive = false;

        AudioManager.PlaySound("grassCut");
        activeSequence = DOTween.Sequence().SetLink(gameObject);
        activeSequence.Append(leave.DORotate(new Vector3(0, 0, -90f), cutDuration));
        activeSequence.Join(leave.DOJump(leave.transform.position + new Vector3(.5f, -0.5f, 0), 2, 1, cutDuration));
        activeSequence.AppendCallback(() =>
        {
            // SetLeafVisible(false);
            leafState = LeafState.Hidden;
            onComplete?.Invoke();
        });

        activeSequence.AppendInterval(Mathf.Max(0f, regrowDelay));
        TweenLeafAlpha(activeSequence, 0f, cutFadeDuration);
        activeSequence.AppendCallback(() =>
        {
            if (IsDead || leave == null)
                return;

            BeginRegrow();
        });

        // 草重生
        AppendLeafRegrowTween(activeSequence);
        activeSequence.AppendInterval(2);
        activeSequence.OnComplete(() =>
        {
            if (!IsDead)
            {
                leafState = LeafState.Visible;
                ResetLeafVisual(true, 1f);
            }

            activeSequence = null;
        });

        return true;
    }

    public bool TryDig(Action onComplete = null)
    {
        if (!CanDig())
            return false;

        KillActiveSequence();
        rootState = RootState.Digging;
        fireSequenceActive = false;

        AudioManager.PlaySound("grassCut");
        SetHoleVisible(true);
        root.GetComponentInChildren<SpriteRenderer>().sortingOrder = 200;
        activeSequence = DOTween.Sequence().SetLink(gameObject);

        if (leave != null)
        {
            SetLeafVisible(true);

            activeSequence.Append(whole.DORotate(new Vector3(0, 0, -90f), cutDuration));
            activeSequence.Join(whole.DOJump(leave.transform.position + new Vector3(.5f, -0.5f, 0), 2, 1, cutDuration));
            // activeSequence.Join(leave.DOLocalMoveY(leaveInitialLocalPosition.y + popOffsetY, digFadeDuration).SetEase(Ease.OutQuad));
            // TweenLeafAlpha(activeSequence, 0f, digFadeDuration);
        }

        activeSequence.Join(whole.DOLocalMoveY(rootInitialLocalPosition.y + popOffsetY, digFadeDuration).SetEase(Ease.OutQuad));
        // TweenRootAlpha(activeSequence, 0f, digFadeDuration);
        activeSequence.OnComplete(() =>
        {
            rootState = RootState.Removed;
            leafState = LeafState.Hidden;
            // SetLeafVisible(false);
            // SetRootVisible(false);
            activeSequence = null;
            onComplete?.Invoke();
        });

        return true;
    }

    public bool TryBurn(GameObject fireVisualTemplate, Action onComplete = null)
    {
        if (!CanBurn() || fireVisualTemplate == null)
            return false;

        BeginBurn(fireVisualTemplate);
        BuildBurnSequence(onComplete);

        return true;
    }

    public void ResetRuntimeState()
    {
        EnsureInitialStateCached();
        KillActiveSequence();
        ClearFireEffect();
        fireSequenceActive = false;
        leafState = LeafState.Visible;
        rootState = RootState.Present;
        ResetLeafVisual(true, 1f);
        ResetRootVisual(true, 1f);
    }

    public void CleanupFireRuntimeState()
    {
        EnsureInitialStateCached();
        if (!fireSequenceActive && fireEffectInstance == null)
            return;

        bool wasDead = IsDead;
        KillActiveSequence();
        ClearFireEffect();
        fireSequenceActive = false;

        if (wasDead)
        {
            rootState = RootState.Removed;
            leafState = LeafState.Hidden;
            SetLeafVisible(false);
            SetRootVisible(false);
            return;
        }

        if (IsFireLeafState())
        {
            leafState = LeafState.Visible;
            ResetLeafVisual(true, 1f);
        }
    }

    private bool IsLeafBusy()
    {
        return leafState == LeafState.Cutting || IsFireLeafState();
    }

    private bool IsFireLeafState()
    {
        return leafState == LeafState.Burning
            || leafState == LeafState.ScorchedFading
            || leafState == LeafState.RegrowWaiting
            || leafState == LeafState.Regrowing;
    }

    private void CacheInitialState()
    {
        if (leave != null)
        {
            leaveInitialLocalPosition = leave.localPosition;
            leaveInitialLocalScale = leave.localScale;
            leaveRenderers = leave.GetComponentsInChildren<SpriteRenderer>(true);
            leaveInitialColors = CacheColors(leaveRenderers);
        }

        if (root != null)
        {
            rootInitialLocalPosition = root.localPosition;
            rootInitialLocalScale = root.localScale;
            rootRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            rootInitialColors = CacheColors(rootRenderers);
        }

        initialStateCached = true;
    }

    private void EnsureInitialStateCached()
    {
        if (!initialStateCached)
            CacheInitialState();
    }

    private Color[] CacheColors(SpriteRenderer[] renderers)
    {
        if (renderers == null)
            return Array.Empty<Color>();

        Color[] colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            colors[i] = renderers[i] != null ? renderers[i].color : Color.white;
        }

        return colors;
    }

    private void ResetLeafVisual(bool isVisible, float alpha)
    {
        if (leave == null)
            return;

        leave.DOKill();
        leave.gameObject.SetActive(isVisible);
        leave.localEulerAngles = Vector3.zero;
        leave.localPosition = leaveInitialLocalPosition;
        leave.localScale = isVisible && alpha <= 0f ? Vector3.zero : leaveInitialLocalScale;
        RestoreColors(leaveRenderers, leaveInitialColors, alpha);
    }

    private void ResetRootVisual(bool isVisible, float alpha)
    {
        if (root == null)
            return;

        root.DOKill();
        root.gameObject.SetActive(isVisible);
        root.localPosition = rootInitialLocalPosition;
        root.localScale = rootInitialLocalScale;
        RestoreColors(rootRenderers, rootInitialColors, alpha);
    }

    private void RestoreColors(SpriteRenderer[] renderers, Color[] colors, float alpha)
    {
        if (renderers == null || colors == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || i >= colors.Length)
                continue;

            Color color = colors[i];
            color.a *= alpha;
            renderers[i].color = color;
        }
    }

    private void SetLeafScorched(float alpha)
    {
        if (leaveRenderers == null)
            return;

        for (int i = 0; i < leaveRenderers.Length; i++)
        {
            if (leaveRenderers[i] == null)
                continue;

            leaveRenderers[i].color = new Color(0f, 0f, 0f, alpha);
        }
    }

    private void BeginBurn(GameObject fireVisualTemplate)
    {
        KillActiveSequence();
        ClearFireEffect();
        fireSequenceActive = true;
        leafState = LeafState.Burning;
        fireEffectInstance = CreateFireEffect(fireVisualTemplate);
    }

    private void BuildBurnSequence(Action onComplete)
    {
        activeSequence = DOTween.Sequence().SetLink(gameObject);
        activeSequence.AppendInterval(Mathf.Max(0f, fireDuration));
        AppendFireEffectFade(activeSequence);
        activeSequence.AppendCallback(EnterScorchedFade);
        AppendLeafScorchedAlpha(activeSequence, 1f, fireScorchedDuration);
        activeSequence.AppendInterval(Mathf.Max(0f, fireScorchedHoldDuration));
        AppendLeafScorchedAlpha(activeSequence, 0f, fireFadeDuration);
        activeSequence.AppendCallback(() => EnterFireRegrowWait(onComplete));
        activeSequence.AppendInterval(Mathf.Max(0f, regrowDelay));
        activeSequence.AppendCallback(BeginRegrow);
        AppendLeafRegrowTween(activeSequence);
        activeSequence.OnComplete(CompleteRegrow);
    }

    private void EnterScorchedFade()
    {
        ClearFireEffect();
        if (IsDead || leave == null)
            return;

        leafState = LeafState.ScorchedFading;
        SetLeafVisible(true);
    }

    private void EnterFireRegrowWait(Action onComplete)
    {
        if (IsDead || leave == null)
            return;

        SetLeafVisible(false);
        leafState = LeafState.RegrowWaiting;
        onComplete?.Invoke();
    }

    private void BeginRegrow()
    {
        if (IsDead || leave == null)
            return;

        leafState = LeafState.Regrowing;
        PrepareLeafRegrowVisual();
        levelCtrl?.OnGrassRegrown(this);
    }

    private void PrepareLeafRegrowVisual()
    {
        ResetLeafVisual(true, 0f);
        leave.localPosition = GetLeafRegrowStartLocalPosition();
    }

    private Vector3 GetLeafRegrowStartLocalPosition()
    {
        Vector3 position = leaveInitialLocalPosition;
        position.y -= Mathf.Max(0f, regrowStartOffsetY);
        return position;
    }

    private void AppendLeafRegrowTween(Sequence sequence)
    {
        if (sequence == null || leave == null)
            return;

        float duration = Mathf.Max(0f, regrowDuration);
        sequence.Append(leave.DOLocalMove(leaveInitialLocalPosition, duration));
        sequence.Join(leave.DOScale(leaveInitialLocalScale, duration));
        TweenLeafAlpha(sequence, 1f, duration);
    }

    private void CompleteRegrow()
    {
        if (!IsDead)
        {
            leafState = LeafState.Visible;
            ResetLeafVisual(true, 1f);
        }

        activeSequence = null;
        fireSequenceActive = false;
    }

    private void TweenLeafAlpha(Sequence sequence, float alpha, float duration)
    {
        TweenAlpha(sequence, leaveRenderers, leaveInitialColors, alpha, duration);
    }

    private void TweenRootAlpha(Sequence sequence, float alpha, float duration)
    {
        TweenAlpha(sequence, rootRenderers, rootInitialColors, alpha, duration);
    }

    private void AppendLeafScorchedAlpha(Sequence sequence, float alpha, float duration)
    {
        if (sequence == null || leaveRenderers == null)
            return;

        bool hasAppended = false;
        Color targetColor = new Color(0f, 0f, 0f, alpha);
        for (int i = 0; i < leaveRenderers.Length; i++)
        {
            SpriteRenderer renderer = leaveRenderers[i];
            if (renderer == null)
                continue;

            Tween tween = renderer.DOColor(targetColor, Mathf.Max(0f, duration));
            if (hasAppended)
            {
                sequence.Join(tween);
            }
            else
            {
                sequence.Append(tween);
                hasAppended = true;
            }
        }
    }

    private void TweenAlpha(Sequence sequence, SpriteRenderer[] renderers, Color[] colors, float alpha, float duration)
    {
        if (sequence == null || renderers == null || colors == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || i >= colors.Length)
                continue;

            Color targetColor = colors[i];
            targetColor.a *= alpha;
            sequence.Join(renderer.DOColor(targetColor, duration));
        }
    }

    private void SetLeafVisible(bool isVisible)
    {
        if (leave != null)
            leave.gameObject.SetActive(isVisible);
    }

    private void SetRootVisible(bool isVisible)
    {
        if (root != null)
            root.gameObject.SetActive(isVisible);
    }

    private void SetHoleVisible(bool isVisible)
    {
        if (hole != null)
            hole.gameObject.SetActive(isVisible);
    }


    private void KillActiveSequence()
    {
        if (activeSequence != null)
        {
            activeSequence.Kill(false);
            activeSequence = null;
        }

        leave?.DOKill();
        root?.DOKill();
    }

    private GameObject CreateFireEffect(GameObject fireVisualTemplate)
    {
        if (fireVisualTemplate == null)
            return null;

        GameObject instance = Instantiate(fireVisualTemplate, transform);
        instance.name = $"{fireVisualTemplate.name}_visual";
        instance.transform.position = transform.position + fireEffectWorldOffset;
        instance.transform.localScale = Vector3.one * fireEffectScale;
        DisableInteractiveComponents(instance);
        instance.SetActive(true);
        return instance;
    }

    private void AppendFireEffectFade(Sequence sequence)
    {
        if (sequence == null || fireEffectInstance == null)
            return;

        GameObject effect = fireEffectInstance;
        float duration = Mathf.Max(0f, fireEffectFadeDuration);
        sequence.AppendCallback(() => StopFireEffectParticles(effect));

        bool hasAppended = false;
        SpriteRenderer[] renderers = effect.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Tween tween = renderer.DOFade(0f, duration);
            if (hasAppended)
            {
                sequence.Join(tween);
            }
            else
            {
                sequence.Append(tween);
                hasAppended = true;
            }
        }

        if (!hasAppended && duration > 0f)
            sequence.AppendInterval(duration);

        sequence.AppendCallback(() => CompleteFireEffectFade(effect));
    }

    private void StopFireEffectParticles(GameObject effect)
    {
        if (effect == null)
            return;

        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;

            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void DisableInteractiveComponents(GameObject instance)
    {
        if (instance == null)
            return;

        Collider2D[] colliders = instance.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody2D[] rigidbodies = instance.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            rigidbodies[i].simulated = false;

        DragEventsListener[] dragListeners = instance.GetComponentsInChildren<DragEventsListener>(true);
        for (int i = 0; i < dragListeners.Length; i++)
            dragListeners[i].SetEnableDrag(false);
    }

    private void ClearFireEffect()
    {
        if (fireEffectInstance == null)
            return;

        DestroyFireEffectInstance(fireEffectInstance);
        fireEffectInstance = null;
    }

    private void CompleteFireEffectFade(GameObject effect)
    {
        if (effect == null)
            return;

        DestroyFireEffectInstance(effect);
        if (fireEffectInstance == effect)
            fireEffectInstance = null;
    }

    private void DestroyFireEffectInstance(GameObject effect)
    {
        if (effect == null)
            return;

        effect.transform.DOKill();
        SpriteRenderer[] renderers = effect.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i]?.DOKill();

        Destroy(effect);
    }

    private void OnDisable()
    {
        KillActiveSequence();
        ClearFireEffect();
        fireSequenceActive = false;
    }

    private void OnDestroy()
    {
        KillActiveSequence();
        ClearFireEffect();
        fireSequenceActive = false;
    }
}
