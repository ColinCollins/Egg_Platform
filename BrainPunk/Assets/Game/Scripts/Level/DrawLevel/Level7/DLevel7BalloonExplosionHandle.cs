using System.Collections;
using Bear.Logger;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// DLevel_007 气球爆炸视觉流程：在作者配置的世界爆点播放放大后的粒子，并在播放同帧隐藏气球视觉。
/// </summary>
public sealed class DLevel7BalloonExplosionHandle : BaseItemEventHandle, IDebuger
{
    /// <summary>
    /// 爆炸实例的稳定父节点。
    /// 调用边界：绑定 DLevel_007/level7，与 particleTemplate 当前父节点使用同一坐标系。
    /// </summary>
    [SerializeField] private Transform effectParent;

    /// <summary>
    /// 本关已摆到气球爆点的 Knock_Explosion 模板实例。
    /// 注意事项：只读取其世界位置、旋转、缩放和粒子配置，不移动或播放模板自身。
    /// </summary>
    [SerializeField] private GameObject particleTemplate;

    /// <summary>
    /// 爆炸实例相对模板的统一缩放倍率。
    /// 调用边界：只影响运行时克隆，不修改场景模板或共享粒子资源。
    /// </summary>
    [SerializeField, Min(0.01f)] private float effectScaleMultiplier = 1.3f;

    /// <summary>
    /// 爆炸播放同帧隐藏视觉、执行链安全结束后销毁的气球根节点。
    /// 缺失时回退到当前组件所在 GameObject；根节点承载 Executor，因此不能在命中帧直接销毁。
    /// </summary>
    [SerializeField] private GameObject balloonRoot;

    /// <summary>
    /// 为保持既有 BaseItemExecutor 节奏，视觉仍在播放时提前放行后续音效、淡出和胜利回调的时间。
    /// </summary>
    [SerializeField, Min(0f)] private float executorReleaseDelay = 0.1f;

    /// <summary>
    /// 防止误配循环粒子导致气球永不消失的最大等待时间。
    /// 当前 Knock_Explosion 理论完整时长约 0.6 秒，默认 2 秒只作为保护上限。
    /// </summary>
    [SerializeField, Min(0.1f)] private float maxEffectDuration = 2f;

    private Coroutine _explosionCoroutine;
    private GameObject _effectInstance;
    private bool _hasStartedOutcomeFlow;

    public override void Execute()
    {
        CancelExplosion();
        DisableBalloonColliders();

        ParticleSystem[] particleSystems = SpawnAndPlayEffect();
        HideBalloonVisuals();
        IsRunning = true;
        IsDone = false;
        _explosionCoroutine = StartCoroutine(WaitForEffectCompletion(particleSystems));
    }

    public override void ResetState()
    {
        CancelExplosion();
        _hasStartedOutcomeFlow = false;
        base.ResetState();
    }

    /// <summary>
    /// 标记同一执行链的 OnBalloonExploded 已实际调用，允许粒子结束后销毁承载 Executor 的气球。
    /// 调用边界：只绑定 ballon 上现有胜利 EventHandleListener 的同一个 Handle UnityEvent。
    /// </summary>
    public void OnBalloonOutcomeStarted()
    {
        _hasStartedOutcomeFlow = true;
    }

    private ParticleSystem[] SpawnAndPlayEffect()
    {
        if (particleTemplate == null)
        {
            this.LogWarning("[DLevel7BalloonExplosionHandle] Knock_Explosion 模板缺失，气球将直接结束视觉流程。");
            return System.Array.Empty<ParticleSystem>();
        }

        Transform stableParent = effectParent != null
            ? effectParent
            : particleTemplate.transform.parent;

        _effectInstance = Instantiate(
            particleTemplate,
            particleTemplate.transform.position,
            particleTemplate.transform.rotation,
            stableParent);
        _effectInstance.transform.localScale =
            particleTemplate.transform.localScale * Mathf.Max(0.01f, effectScaleMultiplier);
        _effectInstance.SetActive(true);

        ParticleSystem[] particleSystems =
            _effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystems[i].Play(false);
        }

        if (particleSystems.Length == 0)
        {
            this.LogWarning("[DLevel7BalloonExplosionHandle] Knock_Explosion 实例没有 ParticleSystem，气球将直接隐藏。");
        }

        return particleSystems;
    }

    private IEnumerator WaitForEffectCompletion(ParticleSystem[] particleSystems)
    {
        // Play 后先让粒子系统完成首帧初始化，再判断 IsAlive。
        yield return null;

        float elapsedSeconds = 0f;
        bool hasReleasedExecutor = false;
        while (AreAnyParticlesAlive(particleSystems) &&
               elapsedSeconds < maxEffectDuration)
        {
            elapsedSeconds += Time.deltaTime;
            if (!hasReleasedExecutor && elapsedSeconds >= executorReleaseDelay)
            {
                ReleaseExecutor();
                hasReleasedExecutor = true;
            }

            yield return null;
        }

        if (!hasReleasedExecutor)
            ReleaseExecutor();

        if (AreAnyParticlesAlive(particleSystems))
        {
            this.LogWarning("[DLevel7BalloonExplosionHandle] 爆炸粒子超过最大等待时间，已执行保护性清理。");
        }

        // 气球承载 BaseItemExecutor；必须等胜利回调真实执行后才能销毁，避免卡帧跳过后续事件项。
        while (!_hasStartedOutcomeFlow)
            yield return null;

        GameObject completedEffect = _effectInstance;
        _effectInstance = null;
        _explosionCoroutine = null;

        if (completedEffect != null)
            Destroy(completedEffect);

        GameObject root = balloonRoot != null ? balloonRoot : gameObject;
        if (root != null)
            Destroy(root);
    }

    private static bool AreAnyParticlesAlive(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null)
            return false;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] != null && particleSystems[i].IsAlive(true))
                return true;
        }

        return false;
    }

    private void DisableBalloonColliders()
    {
        GameObject root = balloonRoot != null ? balloonRoot : gameObject;
        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private void HideBalloonVisuals()
    {
        GameObject root = balloonRoot != null ? balloonRoot : gameObject;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = false;

        CanvasRenderer[] canvasRenderers = root.GetComponentsInChildren<CanvasRenderer>(true);
        for (int i = 0; i < canvasRenderers.Length; i++)
            canvasRenderers[i].SetAlpha(0f);
    }

    private void ReleaseExecutor()
    {
        IsRunning = false;
        IsDone = true;
    }

    private void CancelExplosion()
    {
        if (_explosionCoroutine != null)
        {
            StopCoroutine(_explosionCoroutine);
            _explosionCoroutine = null;
        }

        if (_effectInstance != null)
        {
            Destroy(_effectInstance);
            _effectInstance = null;
        }
    }

    private void OnDestroy()
    {
        CancelExplosion();
    }

    private void OnDisable()
    {
        CancelExplosion();
    }
}
