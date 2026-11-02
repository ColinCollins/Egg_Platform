using System.Collections.Generic;
using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using Spine.Unity;
using UnityEngine;
using AudioManager = Game.Scripts.Common.AudioManager;

/// <summary>
/// Level117「太阳太大」专用控制器：管理日晒减速、阴影恢复、屏幕太阳、汗液和日晒死亡。
/// </summary>
[DefaultExecutionOrder(900)]
public sealed class Level117Ctrl : MonoBehaviour
{
    #region Inspector

    [Header("关卡引用")]
    [Tooltip("本关基础关卡控制器；为空时自动从当前物体获取。")]
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;

    [Tooltip("本关玩家角色；为空时优先使用 BaseLevelCtrl.Actor。")]
    [SerializeField] private ActorCtrl actor;

    [Tooltip("固定在屏幕初始位置的太阳节点；调整场景中的 Sun 位置即可调整它的屏幕锚点。")]
    [SerializeField] private Transform sunTransform;

    [Tooltip("固定太阳屏幕位置使用的关卡相机；为空时自动查找名为 GameCamera 的子节点。")]
    [SerializeField] private Camera sunFollowCamera;

    [Tooltip("已绑定的遮阳伞阴影区域；绑定后优先使用这些区域。")]
    [SerializeField] private Level117ShadeZone[] shadeZones;

    [Tooltip("未绑定阴影组件时自动查找名称包含 zheyangsan 的伞根节点。")]
    [SerializeField] private Transform[] shadeRoots;

    [Header("日晒参数")]
    [Tooltip("角色离开阴影后，按移动速度曲线减速到零并死亡的总时长（秒）。")]
    [Min(0.01f)]
    [SerializeField] private float exposureDuration = 5f;

    [Tooltip("日晒热度从 0 到 1 时对应的移动速度系数。默认使用缓入缓出，曲线起点应为 1、终点应为 0。")]
    [SerializeField] private AnimationCurve moveSpeedFactorByHeat = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Tooltip("商店按钮遮挡太阳达到该比例时立即免疫日晒，范围为 0 到 1。")]
    [Range(0f, 1f)]
    [SerializeField] private float eclipseImmunityThreshold = 0.8f;

    [Header("日食视觉颜色")]
    /// <summary>未遮挡太阳时的热色蒙版，可在本关根节点 Inspector 中调整。</summary>
    [SerializeField] private Color sunHotTint = new Color(1f, 0.55f, 0.30f, 0.30f);

    /// <summary>太阳被完全遮挡时的暗色蒙版，可在本关根节点 Inspector 中调整。</summary>
    [SerializeField] private Color sunEclipseDarkTint = new Color(0.18f, 0.12f, 0.08f, 0.46f);

    /// <summary>日全食边缘日冕的颜色，可在本关根节点 Inspector 中调整。</summary>
    [SerializeField] private Color sunCoronaTint = new Color(1f, 0.86f, 0.22f, 0.95f);

    [Tooltip("日冕外框直径相对于太阳直径的倍率；数值越大，光晕向外扩散越远。")]
    [Min(1.05f)]
    [SerializeField] private float sunCoronaScale = 1.8f;

    [Tooltip("日冕贴边亮光向外渐弱的纹理宽度比例。")]
    [Range(0.05f, 0.24f)]
    [SerializeField] private float sunCoronaOuterFeather = 0.2f;

    [Header("角色日晒颜色")]
    [Tooltip("角色最热时混合到的 RGB；Alpha 表示最热时的最大混合强度，角色自身透明度不受影响。")]
    [SerializeField] private Color actorHotTint = new Color(1f, 0.25f, 0.2f, 0.65f);

    [Tooltip("角色在正常、阴影和晒红颜色之间切换的短渐变时长（秒）。")]
    [Min(0f)]
    [SerializeField] private float actorTintTransitionDuration = 0.12f;

    [Header("阴影颜色")]
    [Tooltip("进入遮阳伞阴影时覆盖 Actor Spine 的 RGB 颜色；Alpha 始终保留原值。")]
    [SerializeField] private Color shadeTint = new Color(0.12f, 0.15f, 0.2f, 0.6f);

    [Tooltip("伞面向下覆盖的最大阴影深度，用于避免角色远离伞面后仍被判定在阴影中。")]
    [Min(0f)]
    [SerializeField] private float shadeVerticalDepth = 4f;

    [Header("汗液效果")]
    [Tooltip("跟随角色的汗滴粒子根节点；使用场景中的摆放位置作为相对角色的初始偏移。")]
    [SerializeField] private Transform sweatParticleRoot;

    [Tooltip("Level_117/hanshui1 下左右两套常驻汗滴粒子；轨迹、大小、重力、颜色、随机图片和排序均在各 ParticleSystem Inspector 中调整。")]
    [SerializeField] private ParticleSystem[] sweatParticleSystems;

    [Tooltip("刚开始受到日晒时，每套汗滴粒子的发射率。")]
    [Min(0f)]
    [SerializeField] private float sweatMinEmissionRate = 10f;

    [Tooltip("角色达到最热、移动速度降至最低时，每套汗滴粒子的最大发射率。")]
    [Min(0f)]
    [SerializeField] private float sweatMaxEmissionRate = 20f;

    #endregion

    #region Runtime

    private readonly HashSet<Level117ShadeZone> activeShadeZones = new HashSet<Level117ShadeZone>();
    private readonly HashSet<Transform> activeFallbackShadeRoots = new HashSet<Transform>();
    private EventSubscriber subscriber;
    private SkeletonAnimation actorSkeleton;
    private Color actorInitialSpineColor = Color.white;
    private Color actorSpineTintTarget = Color.white;
    private float actorInitialMoveSpeed;
    private Vector3 sunViewportAnchor;
    private float exposureElapsed;
    private float eclipseCoverage;
    private bool actorSpeedCaptured;
    private bool spineColorCaptured;
    private bool actorSpineTintTargetInitialized;
    private bool gameplayPaused;
    private bool deathTriggered;
    private bool attemptTerminating;
    private bool hasExplicitShadeZones;
    private bool hasAutoShadeRoots;
    private bool warnedMissingActor;
    private bool warnedMissingSweatParticleSystems;
    private bool sunViewportAnchorCaptured;
    private Level117ShopButtonDrag hudController;
    private bool sweatVisualConfigured;
    private Vector3 sweatParticleRootWorldOffset;
    private bool sweatParticleRootOffsetCaptured;

    #endregion

    #region Public API

    /// <summary>
    /// 太阳节点引用，供 Level117 专用 HUD 计算屏幕空间遮挡使用。
    /// </summary>
    public Transform SunTransform => sunTransform;

    /// <summary>
    /// 当前太阳被商店按钮遮挡的比例，范围为 0 到 1。
    /// </summary>
    public float EclipseCoverage => eclipseCoverage;

    /// <summary>
    /// 当前日晒累计时间（秒），仅供 Level117 专用 HUD 或调试读取。
    /// </summary>
    public float ExposureElapsed => exposureElapsed;

    /// <summary>
    /// 伞面没有显式碰撞体时，供 Level117ShadeZone 使用的阴影垂直判定深度。
    /// </summary>
    internal float ShadeVerticalDepth => shadeVerticalDepth;

    /// <summary>本关 HUD 使用的未遮挡热色，供运行时绑定时传递给日食显示组件。</summary>
    internal Color SunHotTint => sunHotTint;

    /// <summary>本关 HUD 使用的完全遮挡暗色，供运行时绑定时传递给日食显示组件。</summary>
    internal Color SunEclipseDarkTint => sunEclipseDarkTint;

    /// <summary>本关 HUD 使用的日冕颜色，供运行时绑定时传递给日食显示组件。</summary>
    internal Color SunCoronaTint => sunCoronaTint;

    /// <summary>本关 HUD 使用的日冕尺寸倍率，仅供 Level117 日食显示组件读取。</summary>
    internal float SunCoronaScale => sunCoronaScale;

    /// <summary>本关 HUD 使用的日冕外侧羽化比例，仅供 Level117 日食显示组件读取。</summary>
    internal float SunCoronaOuterFeather => sunCoronaOuterFeather;

    /// <summary>
    /// 向本关上报商店按钮遮挡太阳的比例；达到阈值会立即清零日晒并恢复速度。
    /// </summary>
    /// <param name="coverage">太阳被遮挡的屏幕比例。</param>
    public void SetEclipseCoverage(float coverage)
    {
        if (attemptTerminating)
            return;

        eclipseCoverage = Mathf.Clamp01(coverage);
        if (eclipseCoverage >= Mathf.Clamp01(eclipseImmunityThreshold))
        {
            exposureElapsed = 0f;
            if (!deathTriggered)
                RestoreActorSpeed();
        }
    }

    /// <summary>
    /// 注册玩家进入伞影区域，仅供 Level117ShadeZone 调用。
    /// </summary>
    internal void RegisterShadeZone(Level117ShadeZone zone, ActorCtrl enteringActor)
    {
        if (attemptTerminating || zone == null || enteringActor == null || enteringActor != actor)
            return;

        activeShadeZones.Add(zone);
        UpdateActorSpineTint(true, IsEclipseImmune());
    }

    /// <summary>
    /// 注销玩家离开伞影区域，仅供 Level117ShadeZone 调用。
    /// </summary>
    internal void UnregisterShadeZone(Level117ShadeZone zone, ActorCtrl leavingActor)
    {
        if (attemptTerminating || zone == null || leavingActor == null || leavingActor != actor)
            return;

        activeShadeZones.Remove(zone);
        UpdateActorSpineTint(IsActorInShade(), IsEclipseImmune());
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        ResolveReferences();
        CaptureInitialState();
        EnsureSweatVisual();
        ResolveShadeSources();
        Level117RuntimeBridge.Bind(this);
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureInitialState();
        EnsureSweatVisual();

        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
        subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        gameplayPaused = false;
        if (PlayCtrl.Instance != null)
        {
            try
            {
                gameplayPaused = !PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
            }
            catch
            {
                // PlayCtrl 尚未完成初始化时，等待后续 SwitchGameStateEvent 修正状态。
            }
        }
        Level117RuntimeBridge.Bind(this);
    }

    private void Start()
    {
        ResolveReferences();
        CaptureInitialState();
        EnsureSweatVisual();
        ResolveShadeSources();
        CaptureSunViewportAnchor();
        EnsureHudControllerBound();
    }

    private void Update()
    {
        if (!attemptTerminating)
            EnsureHudControllerBound();

        if (attemptTerminating)
        {
            StopSweat();
            return;
        }

        if (actor == null)
        {
            if (!warnedMissingActor)
            {
                Debug.LogWarning("[Level117Ctrl] Actor 引用缺失，日晒逻辑暂时跳过。", this);
                warnedMissingActor = true;
            }

            return;
        }

        if (baseLevelCtrl != null && baseLevelCtrl.IsSuccess())
        {
            ResetExposureAndRestore();
            UpdateActorSpineTint(false, true);
            StopSweat();
            return;
        }

        if (baseLevelCtrl != null && baseLevelCtrl.IsFail())
        {
            StopSweat();
            return;
        }

        if (actor.IsDied && !deathTriggered)
        {
            deathTriggered = true;
            StopSweat();
            return;
        }

        ReconcileGameplayPausedState();
        if (gameplayPaused)
        {
            StopSweat();
            if (deathTriggered)
                KeepDeathMovementLocked();
            return;
        }

        if (deathTriggered)
        {
            StopSweat();
            KeepDeathMovementLocked();
            return;
        }

        ReconcileShadeState();

        bool inShade = IsActorInShade();
        bool eclipseImmune = IsEclipseImmune();

        if (inShade || eclipseImmune)
        {
            ResetExposureAndRestore();
        }
        else
        {
            exposureElapsed += Mathf.Max(0f, Time.deltaTime);
            ApplyExposureSpeed();

            if (exposureElapsed >= GetExposureDuration())
                TriggerSunDeath();
        }

        UpdateActorSpineTint(inShade, eclipseImmune);
        UpdateSweat(inShade, eclipseImmune);

    }

    private void LateUpdate()
    {
        TickActorSpineTint();
        KeepSunAtViewportAnchor();
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
        EventsUtils.ResetEvents(ref subscriber);
        Level117RuntimeBridge.Unbind(this);
    }

    private void OnDestroy()
    {
        CleanupRuntimeState();
        EventsUtils.ResetEvents(ref subscriber);
        Level117RuntimeBridge.Unbind(this);
    }

    #endregion

    #region Exposure

    private void ApplyExposureSpeed()
    {
        if (!actorSpeedCaptured || actor == null || deathTriggered)
            return;

        float heatT = Mathf.Clamp01(exposureElapsed / GetExposureDuration());
        float speedFactor = moveSpeedFactorByHeat == null || moveSpeedFactorByHeat.length == 0
            ? 1f - heatT
            : Mathf.Clamp01(moveSpeedFactorByHeat.Evaluate(heatT));
        actor.SetMoveSpeed(Mathf.Max(0f, actorInitialMoveSpeed * speedFactor));
    }

    private float GetExposureDuration()
    {
        return Mathf.Max(0.01f, exposureDuration);
    }

    private void ResetExposureAndRestore()
    {
        exposureElapsed = 0f;
        if (!deathTriggered)
            RestoreActorSpeed();
    }

    private void RestoreActorSpeed()
    {
        if (actor != null && actorSpeedCaptured)
            actor.SetMoveSpeed(actorInitialMoveSpeed);
    }

    private void TriggerSunDeath()
    {
        if (deathTriggered || actor == null)
            return;

        deathTriggered = true;
        exposureElapsed = GetExposureDuration();
        StopSweat();

        if (baseLevelCtrl != null)
            baseLevelCtrl.SetCtrlable(false);

        actor.SetMoveInput(0f);
        actor.SetMoveSpeed(0f);
        AudioManager.PlaySound("failed");
        actor.Die();
    }

    private void KeepDeathMovementLocked()
    {
        if (baseLevelCtrl != null)
            baseLevelCtrl.SetCtrlable(false);

        if (actor == null)
            return;

        actor.SetMoveInput(0f);
        actor.SetMoveSpeed(0f);
    }

    #endregion

    #region Shade And Spine

    private void ReconcileShadeState()
    {
        if (hasExplicitShadeZones)
        {
            for (int i = 0; i < shadeZones.Length; i++)
            {
                Level117ShadeZone zone = shadeZones[i];
                if (zone == null)
                    continue;

                if (zone.ContainsActor(actor))
                    activeShadeZones.Add(zone);
                else
                    activeShadeZones.Remove(zone);
            }
        }
        else if (hasAutoShadeRoots)
        {
            activeFallbackShadeRoots.Clear();
            for (int i = 0; i < shadeRoots.Length; i++)
            {
                Transform root = shadeRoots[i];
                if (root != null && IsActorUnderShadeCanopy(root, actor))
                    activeFallbackShadeRoots.Add(root);
            }
        }

        UpdateActorSpineTint(IsActorInShade(), IsEclipseImmune());
    }

    private bool IsActorInShade()
    {
        return activeShadeZones.Count > 0 || activeFallbackShadeRoots.Count > 0;
    }

    private bool IsEclipseImmune()
    {
        return eclipseCoverage >= Mathf.Clamp01(eclipseImmunityThreshold);
    }

    private void UpdateActorSpineTint(bool isInShade, bool eclipseImmune)
    {
        if (actorSkeleton?.Skeleton == null || !spineColorCaptured)
            return;

        Color tint = actorInitialSpineColor;
        float tintStrength = 0f;
        if (isInShade)
        {
            tint = shadeTint;
            tintStrength = Mathf.Clamp01(shadeTint.a);
        }
        else if (!eclipseImmune)
        {
            float heatT = Mathf.Clamp01(exposureElapsed / GetExposureDuration());
            tint = actorHotTint;
            tintStrength = heatT * Mathf.Clamp01(actorHotTint.a);
        }

        Color targetTint = new Color(tint.r, tint.g, tint.b, actorInitialSpineColor.a);
        actorSpineTintTarget = Color.Lerp(actorInitialSpineColor, targetTint, tintStrength);
        actorSpineTintTargetInitialized = true;
    }

    private void TickActorSpineTint()
    {
        if (actorSkeleton?.Skeleton == null || !spineColorCaptured || !actorSpineTintTargetInitialized)
            return;

        Color current = actorSkeleton.Skeleton.GetColor();
        Color target = actorSpineTintTarget;
        float duration = Mathf.Max(0f, actorTintTransitionDuration);
        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
        float blend = duration <= 0f
            ? 1f
            : 1f - Mathf.Exp(-4.60517f * deltaTime / duration);
        Color next = Color.Lerp(current, target, Mathf.Clamp01(blend));

        if (Mathf.Abs(next.r - target.r) <= 0.001f &&
            Mathf.Abs(next.g - target.g) <= 0.001f &&
            Mathf.Abs(next.b - target.b) <= 0.001f)
        {
            next.r = target.r;
            next.g = target.g;
            next.b = target.b;
        }

        // 保留 Spine 当前的整体透明度，颜色渐变不干扰死亡、进门或其它显隐表现。
        next.a = current.a;
        if (Mathf.Approximately(current.r, next.r) &&
            Mathf.Approximately(current.g, next.g) &&
            Mathf.Approximately(current.b, next.b))
        {
            return;
        }

        actorSkeleton.Skeleton.SetColor(next);
    }

    #endregion

    #region Sweat And Sun

    private void UpdateSweat(bool inShade, bool eclipseImmune)
    {
        if (gameplayPaused || deathTriggered || actor == null)
        {
            StopSweat(true);
            return;
        }

        EnsureSweatVisual();
        if (!HasSweatParticleSystems())
            return;

        UpdateSweatParticleRootPosition();

        // 进入阴影或日食免疫后只停止发射，让已经生成的汗滴自然播放完。
        if (inShade || eclipseImmune)
        {
            StopSweat(false);
            return;
        }

        float heatT = Mathf.Clamp01(exposureElapsed / GetExposureDuration());
        UpdateSweatEmissionRates(heatT);
        PlaySweatParticleSystems();
    }

    private void StopSweat(bool clearParticles = true)
    {
        if (sweatParticleSystems == null)
            return;

        ParticleSystemStopBehavior behavior = clearParticles
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;
        for (int i = 0; i < sweatParticleSystems.Length; i++)
        {
            ParticleSystem system = sweatParticleSystems[i];
            if (system != null)
                system.Stop(true, behavior);
        }
    }

    private bool HasSweatParticleSystems()
    {
        if (sweatParticleSystems == null || sweatParticleSystems.Length == 0)
            return false;

        for (int i = 0; i < sweatParticleSystems.Length; i++)
        {
            if (sweatParticleSystems[i] != null)
                return true;
        }

        return false;
    }

    private void PlaySweatParticleSystems()
    {
        for (int i = 0; i < sweatParticleSystems.Length; i++)
        {
            ParticleSystem system = sweatParticleSystems[i];
            if (system != null && !system.isPlaying)
                system.Play(false);
        }
    }

    private void UpdateSweatEmissionRates(float heatT)
    {
        if (sweatParticleSystems == null)
            return;

        float minimumRate = Mathf.Max(0f, sweatMinEmissionRate);
        float maximumRate = Mathf.Max(minimumRate, sweatMaxEmissionRate);
        float emissionRate = Mathf.Lerp(minimumRate, maximumRate, Mathf.Clamp01(heatT));
        for (int i = 0; i < sweatParticleSystems.Length; i++)
        {
            ParticleSystem system = sweatParticleSystems[i];
            if (system == null)
                continue;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = emissionRate;
        }
    }

    private void RestoreSweatEmissionRates()
    {
        if (sweatParticleSystems == null)
            return;

        float minimumRate = Mathf.Max(0f, sweatMinEmissionRate);
        for (int i = 0; i < sweatParticleSystems.Length; i++)
        {
            ParticleSystem system = sweatParticleSystems[i];
            if (system == null)
                continue;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = minimumRate;
        }
    }

    private void CaptureSweatParticleRootOffset()
    {
        if (sweatParticleRootOffsetCaptured || sweatParticleRoot == null || actor == null)
            return;

        sweatParticleRootWorldOffset = sweatParticleRoot.position - actor.transform.position;
        sweatParticleRootOffsetCaptured = true;
    }

    private void UpdateSweatParticleRootPosition()
    {
        CaptureSweatParticleRootOffset();
        if (!sweatParticleRootOffsetCaptured || sweatParticleRoot == null || actor == null)
            return;

        sweatParticleRoot.position = actor.transform.position + sweatParticleRootWorldOffset;
    }

    private void KeepSunAtViewportAnchor()
    {
        if (sunTransform == null || sunFollowCamera == null || !sunViewportAnchorCaptured)
            return;

        sunTransform.position = sunFollowCamera.ViewportToWorldPoint(sunViewportAnchor);
    }

    #endregion

    #region Events And Setup

    private void OnGamePause(GamePauseEvent _)
    {
        gameplayPaused = true;
        StopSweat();
    }

    private void OnGameResume(GameResumeEvent _)
    {
        if (attemptTerminating)
        {
            gameplayPaused = true;
            return;
        }

        // GameResumeEvent 在 PAUSE 节点退出时派发，此时 FSM 仍可能报告 PAUSE。
        // Update 会在状态机真正进入 PLAYING 后自动校正，避免 Reset 后永久停在暂停分支。
        gameplayPaused = true;
        if (deathTriggered)
            KeepDeathMovementLocked();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (attemptTerminating)
        {
            gameplayPaused = true;
            StopSweat();
            return;
        }

        if (evt == null)
            return;

        gameplayPaused = !string.Equals(evt.NewState, GamePlayStateName.PLAYING, System.StringComparison.Ordinal);
        if (gameplayPaused)
            StopSweat();
    }

    private void OnGameReset(GameResetEvent _)
    {
        attemptTerminating = true;
        exposureElapsed = 0f;
        eclipseCoverage = 0f;
        gameplayPaused = true;
        activeShadeZones.Clear();
        activeFallbackShadeRoots.Clear();
        StopSweat();

        if (baseLevelCtrl != null)
            baseLevelCtrl.SetCtrlable(false);

        if (actor != null)
            actor.SetMoveInput(0f);

        UpdateActorSpineTint(false, true);
    }

    private void ReconcileGameplayPausedState()
    {
        if (attemptTerminating)
            return;

        bool isPlaying = IsPlayingState();
        if (gameplayPaused == !isPlaying)
            return;

        gameplayPaused = !isPlaying;
        if (gameplayPaused)
            StopSweat();
    }

    private bool IsPlayingState()
    {
        if (PlayCtrl.Instance == null)
            return !gameplayPaused;

        try
        {
            return PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
        }
        catch
        {
            return !gameplayPaused;
        }
    }

    private void ResolveReferences()
    {
        if (baseLevelCtrl == null)
            baseLevelCtrl = GetComponent<BaseLevelCtrl>();

        if (actor == null && baseLevelCtrl != null)
            actor = baseLevelCtrl.Actor;

        if (actor == null)
            actor = GetComponentInChildren<ActorCtrl>(true);

        if (sunTransform == null)
        {
            Transform sun = transform.Find("Sun");
            sunTransform = sun != null ? sun : FindNamedChild("sun");
        }

        if (sunFollowCamera == null)
        {
            Transform cameraTransform = transform.Find("GameCamera");
            if (cameraTransform != null)
                sunFollowCamera = cameraTransform.GetComponent<Camera>();
        }

        if (sweatParticleRoot == null)
            sweatParticleRoot = FindNamedChild("hanshui1");
    }

    private void EnsureHudControllerBound()
    {
        if (hudController != null || PlayCtrl.Instance == null)
            return;

        GamePlayPanel currentPanel = PlayCtrl.Instance.CurrentGamePlayPanel;
        if (currentPanel == null)
            return;

        hudController = currentPanel.GetComponent<Level117ShopButtonDrag>();
        if (hudController == null)
            hudController = currentPanel.gameObject.AddComponent<Level117ShopButtonDrag>();

        hudController.BindController(this);
    }

    private void CaptureInitialState()
    {
        if (actor != null && !actorSpeedCaptured)
        {
            actorInitialMoveSpeed = Mathf.Max(0f, actor.MoveSpeed);
            actorSpeedCaptured = true;
        }

        if (actor != null && actor.SpineCtrl != null && actorSkeleton == null)
            actorSkeleton = actor.SpineCtrl.GetComponentInChildren<SkeletonAnimation>(true);

        if (actorSkeleton?.Skeleton != null && !spineColorCaptured)
        {
            actorInitialSpineColor = actorSkeleton.Skeleton.GetColor();
            spineColorCaptured = true;
        }

        if (spineColorCaptured && !actorSpineTintTargetInitialized)
        {
            actorSpineTintTarget = actorInitialSpineColor;
            actorSpineTintTargetInitialized = true;
        }

        CaptureSweatParticleRootOffset();
    }

    private void CaptureSunViewportAnchor()
    {
        if (sunFollowCamera == null || sunTransform == null || sunViewportAnchorCaptured)
            return;

        sunViewportAnchor = sunFollowCamera.WorldToViewportPoint(sunTransform.position);
        if (sunViewportAnchor.z > 0f)
            sunViewportAnchorCaptured = true;
    }

    private void ResolveShadeSources()
    {
        if (shadeZones == null)
            shadeZones = new Level117ShadeZone[0];

        hasExplicitShadeZones = shadeZones.Length > 0;
        if (hasExplicitShadeZones)
            return;

        Level117ShadeZone[] foundZones = GetComponentsInChildren<Level117ShadeZone>(true);
        if (foundZones.Length > 0)
        {
            shadeZones = foundZones;
            hasExplicitShadeZones = true;
            return;
        }

        if (shadeRoots == null || shadeRoots.Length == 0)
        {
            List<Transform> roots = new List<Transform>();
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == transform)
                    continue;

                if (child.name.IndexOf("zheyangsan", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    roots.Add(child);
            }

            shadeRoots = roots.ToArray();
        }

        hasAutoShadeRoots = shadeRoots != null && shadeRoots.Length > 0;
    }

    private Transform FindNamedChild(string name)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (string.Equals(children[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                return children[i];
        }

        return null;
    }

    private bool IsActorUnderShadeCanopy(Transform root, ActorCtrl targetActor)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled ||
                !string.Equals(renderer.name, "zheyang", System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return false;

        Bounds actorBounds = GetActorBounds(targetActor);
        return actorBounds.center.x >= bounds.min.x &&
               actorBounds.center.x <= bounds.max.x &&
               actorBounds.max.y <= bounds.max.y + 0.25f &&
               actorBounds.min.y >= bounds.min.y - Mathf.Max(0f, shadeVerticalDepth);
    }

    private static Bounds GetActorBounds(ActorCtrl targetActor)
    {
        Collider2D[] colliders = targetActor.GetComponentsInChildren<Collider2D>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(targetActor.transform.position, Vector3.zero);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }

    private void EnsureSweatVisual()
    {
        if (sweatVisualConfigured)
            return;

        if (!HasSweatParticleSystems())
        {
            if (!warnedMissingSweatParticleSystems)
            {
                Debug.LogWarning("[Level117Ctrl] 未绑定 Level_117/hanshui1 下的汗滴 ParticleSystem，汗液表现已跳过。", this);
                warnedMissingSweatParticleSystems = true;
            }
            return;
        }

        RestoreSweatEmissionRates();
        CaptureSweatParticleRootOffset();
        for (int i = 0; i < sweatParticleSystems.Length; i++)
        {
            ParticleSystem system = sweatParticleSystems[i];
            if (system != null)
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        sweatVisualConfigured = true;
    }

    private void CleanupRuntimeState()
    {
        StopSweat();
        RestoreSweatEmissionRates();
        if (!attemptTerminating)
            RestoreActorSpeed();

        RestoreInitialSpineColor();

        activeShadeZones.Clear();
        activeFallbackShadeRoots.Clear();
        if (!attemptTerminating)
        {
            gameplayPaused = false;
            deathTriggered = false;
        }

        sweatVisualConfigured = false;
    }

    private void RestoreInitialSpineColor()
    {
        if (actorSkeleton?.Skeleton == null || !spineColorCaptured)
            return;

        Color restoredColor = actorInitialSpineColor;
        restoredColor.a = actorSkeleton.Skeleton.A;
        actorSkeleton.Skeleton.SetColor(restoredColor);
        actorSpineTintTarget = actorInitialSpineColor;
        actorSpineTintTargetInitialized = false;
    }

    #endregion
}
