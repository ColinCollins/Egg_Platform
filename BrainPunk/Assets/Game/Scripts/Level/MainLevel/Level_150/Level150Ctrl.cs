using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 双路线导弹关：真实出屏回收累计 10000 发，或拖动预警炸绳提前落门。
/// 所有弹道、回收理由和状态转换在本关完成，不修改共享角色、门或 UI。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level150Ctrl : MonoBehaviour, IEventSender
{
    private enum Phase { Waiting, Running, Falling, Open, Broken, Stopped }
    private enum WaveStage { None, Active, Flying }
    private enum HitTarget { None, Actor, Rope, Door }
    private enum RecycleReason { Exited, Cleanup }

    private const int Goal = 10000;
    private const int MissileCapacity = 12;
    private const int ExplosionCapacity = 4;
    private const int WaveCapacity = 3; // Total rows across both sides, not rows per side.
    private const int SalvoLength = 3;
    private const int WarningCapacity = 3;
    private const float WarningPickPadding = 0.05f;
    private const float WarningHorizontalInset = 0.15f;
    private const float WarningTopInset = 0.05f;
    private const int NoPointer = int.MinValue;
    // 使用用户提供的警报音；只在每条预警真正出现的那一帧播一次。
    private const string WarningAlertSound = "missileWarning";
    private const string WallTextFallbackPrefix = "\u5269\u4F59\u5BFC\u5F39\uFF1A";
    // 与 106 关 ApplyRemainingLayersText 同口径的数字脉冲：单次 0→1→0，峰值 1.15、白字。
    // 106 关终场为 5s 内 12 个同形脉冲，单峰约 0.4s；150 关每次计数变动复用这一个单峰。
    private const float WallTextPulseDuration = 0.4f;
    private const float WallTextPulseMaxScale = 1.15f;

    [Header("Level-owned references")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private SpriteRenderer missileTemplate;
    [SerializeField] private SpriteRenderer warning;
    [SerializeField] private SpriteRenderer brokenDoor;
    [SerializeField] private SpriteRenderer rope;
    [SerializeField] private Transform door;
    [SerializeField] private BoxCollider2D doorTrigger;
    [SerializeField] private Transform doorLanding;
    [SerializeField] private Collider2D ground;
    [SerializeField] private Sprite[] flameFrames;
    [SerializeField] private ParticleSystem explosionPrefab;
    [SerializeField] private Sprite startButtonIdleSprite;
    [SerializeField] private Sprite startButtonPressedSprite;
    // 墙上剩余数 TMP：按 106/131/134 关背景墙文字口径，在 Prefab 里挂在相机下。
    [SerializeField] private TMP_Text wallText;
    [SerializeField] private Localize wallTextLocalize;

    [Header("Cadence (seconds / world units)")]
    [Tooltip("出场动画结束后才开始计时，首发与后续批次相同。")]
    [SerializeField, Min(0.1f)] private float warningSeconds = 1f;
    [SerializeField, Min(0f)] private float warningAppearSeconds = 0.2f;
    [SerializeField, Min(0.01f)] private float salvoInterval = 0.2f;
    [SerializeField, Min(0f)] private float lowLaneDelay = 1f;
    [SerializeField, Min(0f)] private float jumpPreparationTime = 0.25f;
    [SerializeField, Min(0f)] private float standingClearance = 0.08f;
    [SerializeField, Min(0f)] private float doorClearance = 0.05f;
    [SerializeField, Min(0f)] private float laneSeparationPadding = 0.04f;
    [SerializeField, Min(0.1f)] private float missileSpeed = 8f;
    [SerializeField] private float lowestLaneHeight = -1.9f;
    [SerializeField, Min(0.1f)] private float fallGravity = 12f;
    [Tooltip("角色、绳子、门命中共用同一尺寸；不是按目标缩放。")]
    [SerializeField, Min(0.01f)] private float explosionScale = 1f;

    private sealed class ExplosionSlot
    {
        public ParticleSystem Root;
        public float Lifetime;
        public float Remaining;
    }

    private struct PlannedSalvo
    {
        public float Height;
        public int Direction;
        public int ShotCount;
        public float PreviewDelay;
        public int EmittedCount;
        public float NextShotAt;
        public bool PlayerAdjusted;
        public bool Complete => ShotCount > 0 && EmittedCount >= ShotCount;
    }

    private sealed class WarningView
    {
        public SpriteRenderer Renderer;
        public Vector3 NormalScale;
        public Color NormalColor;
        public Vector3 PickSize;
        public Vector3 PickCenterOffset;
        public int SalvoIndex;
        public float ShowAt;
        // 每个预警只在出现时播一次，避免预警倒计时里重复触发。
        public bool AlertPlayed;
        public int PointerId = NoPointer;
        public bool PointerSeen;
        public float DragOffsetY;
    }

    private readonly Level150Missile[] missiles = new Level150Missile[MissileCapacity];
    private readonly ExplosionSlot[] explosions = new ExplosionSlot[ExplosionCapacity];
    private readonly PlannedSalvo[] salvos = new PlannedSalvo[WaveCapacity];
    private readonly float[] standingLanes = new float[WaveCapacity];
    private readonly float[] pressureHighLanes = new float[WaveCapacity];
    private readonly WarningView[] warningViews = new WarningView[WarningCapacity];
    private EventSubscriber subscriber;
    private ActorCtrl actor;
    private Collider2D actorCollider;
    private Rigidbody2D actorBody;
    private Level150Hud hud;
    private Tween wallTextPulseTween;
    private DOTweenTMPAnimator wallTextAnimator;
    private bool wallTextFormatWarningLogged;
    private Transform poolRoot;
    private Phase phase;
    private WaveStage waveStage;
    private bool initialized;
    private bool gamePaused;
    private bool applicationPaused;
    private bool paused;
    private bool actorPauseCaptured;
    private bool actorBlasted;
    private bool actorWasSimulated;
    private bool actorWasControllable;
    private Vector2 actorVelocity;
    private float actorAngularVelocity;
    private int remaining = Goal;
    private int waveNumber;
    private int salvoCount;
    private int standingLaneCount;
    private int pressureHighLaneCount;
    private float missileVisualLength;
    private int activeWarningCount;
    private float waveElapsed;
    private float minimumLaneSeparation;
    private float fallVelocity;
    private bool brokenDoorFalling;
    private float brokenDoorRestY;
    private MelenitasDev.SoundsGood.Sound battleSound;
    private bool battleSoundStarted;
    private bool battleMusicPaused;

    private void OnEnable()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnPause);
        subscriber.Subscribe<GameResumeEvent>(OnResume);
        subscriber.Subscribe<GameResetEvent>(OnReset);
        subscriber.Subscribe<SwitchGameStateEvent>(OnGameStateChanged);
        subscriber.Subscribe<OnTriggerTrapEvent>(OnFailureStarted);
        subscriber.Subscribe<OnTriggerFailAreaEvent>(OnFailureStarted);
    }

    private void Awake()
    {
        if (wallText != null) wallText.gameObject.SetActive(false);
        // 悬门到位前不接受入门；门一开始下落就与气球关一样全程开放 Trigger。
        if (doorTrigger != null)
            doorTrigger.enabled = false;
        if (warning != null)
            warning.gameObject.SetActive(false);
        if (brokenDoor != null)
            brokenDoor.gameObject.SetActive(false);
        if (missileTemplate != null)
            missileTemplate.gameObject.SetActive(false);
    }

    private void Start()
    {
        actor = levelCtrl != null ? levelCtrl.Actor : null;
        actorCollider = actor != null ? actor.GetComponent<Collider2D>() : null;
        actorBody = actor != null ? actor.GetComponent<Rigidbody2D>() : null;
        if (actor == null || actorCollider == null || actorBody == null || gameplayCamera == null ||
            missileTemplate == null || missileTemplate.sprite == null || warning == null || warning.sprite == null ||
            brokenDoor == null || rope == null || door == null || doorTrigger == null ||
            doorLanding == null || ground == null || explosionPrefab == null ||
            startButtonIdleSprite == null || startButtonPressedSprite == null ||
            wallText == null || wallTextLocalize == null ||
            flameFrames == null || flameFrames.Length != 3 ||
            flameFrames[0] == null || flameFrames[1] == null || flameFrames[2] == null)
        {
            Debug.LogError("[Level150] Missing required prefab wiring; round not started.", this);
            enabled = false;
            return;
        }

        // Launch rotates the downward-facing sprite by +/-90 degrees: its X width becomes
        // the horizontal missile's vertical thickness. Do not shrink the user's missile.
        float missileHeight = missileTemplate.sprite.bounds.size.x * Mathf.Abs(missileTemplate.transform.lossyScale.x);
        missileVisualLength = missileTemplate.sprite.bounds.size.y * Mathf.Abs(missileTemplate.transform.lossyScale.y);
        minimumLaneSeparation = missileHeight + laneSeparationPadding;
        if (!InitializeDefaultLanes(missileHeight))
        {
            Debug.LogError("[Level150] Cannot fit a low pressure lane and safe upper lanes below the door; check ground/actor/door layout.", this);
            enabled = false;
            return;
        }

        poolRoot = new GameObject("Level150_Pools").transform;
        poolRoot.SetParent(transform, false);
        brokenDoor.sortingLayerID = missileTemplate.sortingLayerID;
        brokenDoor.sortingOrder = 100;
        for (int i = 0; i < missiles.Length; i++)
        {
            SpriteRenderer body = Instantiate(missileTemplate, poolRoot);
            body.name = "Level150_Missile_" + i;
            body.sortingOrder = 220;
            Level150Missile missile = body.gameObject.AddComponent<Level150Missile>();
            missile.Configure(body, flameFrames);
            missiles[i] = missile;
        }

        for (int i = 0; i < warningViews.Length; i++)
        {
            SpriteRenderer renderer = Instantiate(warning, poolRoot);
            renderer.name = "Level150_Warning_" + i;
            renderer.sortingOrder = 230;
            // Inherit the explicit prefab scale (0.9); never normalize by lane separation.
            warningViews[i] = new WarningView
            {
                Renderer = renderer,
                NormalScale = renderer.transform.localScale,
                NormalColor = renderer.color,
                PickSize = SpriteWorldSize(renderer),
                PickCenterOffset = renderer.transform.TransformVector(renderer.sprite.bounds.center)
            };
            renderer.gameObject.SetActive(false);
        }

        for (int i = 0; i < explosions.Length; i++)
        {
            ParticleSystem effect = Instantiate(explosionPrefab, poolRoot);
            effect.name = "Level150_Explosion_" + i;
            effect.transform.localScale = Vector3.one * explosionScale;
            float lifetime = 0f;
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.loop = false;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.useUnscaledTime = false;
                lifetime = Mathf.Max(lifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.gameObject.SetActive(false);
            explosions[i] = new ExplosionSlot { Root = effect, Lifetime = lifetime + 0.1f };
        }

        var hudObject = new GameObject("Level150_CameraHud", typeof(RectTransform));
        hudObject.layer = 5;
        hudObject.transform.SetParent(gameplayCamera.transform, false);
        hud = hudObject.AddComponent<Level150Hud>();
        hud.Configure(this, gameplayCamera, startButtonIdleSprite, startButtonPressedSprite);
        // 墙上剩余数由 Prefab 的 TMP + I2 Localize 回调按 {0} 格式化，不重建 HUD 文案。
        // 初次显示只是对齐逻辑值，不播数字脉冲；脉冲只跟随真实计数变动。
        RefreshWallText(false);
        initialized = true;
        // Start 之前可能已经收到加载暂停事件，此处补捕获刚完成初始化的角色。
        ApplyPause();
        hud.SetInteractable(!paused);
    }

    public bool HandleStartButtonClick()
    {
        if (!initialized || paused || phase != Phase.Waiting || actor.IsDied)
            return false;
        // Match Level 69: play the physical button sound on each accepted press,
        // not on release or on an input blocked by the current level state.
        AudioManager.PlaySound("l69button");
        wallText.gameObject.SetActive(true);
        RefreshWallText(false);
        StartBattleSoundOnce();
        BeginRound();
        return true;
    }

    private void BeginRound()
    {
        phase = Phase.Running;
        RefreshWallText(false);
        BeginNextWave();
    }

    private void Update()
    {
        if (!initialized || paused)
            return;
        TickExplosions(Time.deltaTime);
        if (brokenDoorFalling)
            TickBrokenDoor(Time.deltaTime);
        if (actor.IsDied)
        {
            // 命中玩家后存量导弹不断轨：停发新弹、不再碰撞计数，但继续按原轨迹飞到自然出屏。
            StopRound();
            TickMissiles(Time.deltaTime);
            return;
        }
        if (levelCtrl.IsActorEnterDoor)
        {
            StopRound();
            ClearMissiles();
            return;
        }

        if (phase == Phase.Running && waveStage == WaveStage.Active)
            TickArraySalvos(Time.deltaTime);

        if (phase == Phase.Running || phase == Phase.Falling || phase == Phase.Open || phase == Phase.Broken)
            TickMissiles(Time.deltaTime);
        // Both directions may coexist inside one array. Only the whole array, not an
        // individual side, has to finish before the next wave is prepared.
        if (phase == Phase.Running && waveStage == WaveStage.Flying && !HasFlyingMissiles())
            BeginNextWave();
        if (phase == Phase.Falling)
            TickDoor(Time.deltaTime);
    }

    private bool InitializeDefaultLanes(float missileHeight)
    {
        float actorHeight = actorCollider.bounds.size.y;
        if (actorHeight <= 0f || ground.bounds.size.y <= 0f)
            return false;

        // Upper lanes do not threaten a grounded actor before the mandatory low attack.
        // They are support lanes, not an all-high passive-survival wave template.
        float standingHead = ground.bounds.max.y + actorHeight;
        float minimum = Mathf.Max(transform.position.y + lowestLaneHeight,
            standingHead + missileHeight * 0.5f + standingClearance);
        float maximum = Mathf.Min(DoorBounds.min.y - missileHeight * 0.5f - doorClearance,
            gameplayCamera.transform.position.y + gameplayCamera.orthographicSize - missileHeight * 0.5f - WarningTopInset);
        if (maximum < minimum)
            return false;
        standingLaneCount = Mathf.Min(WaveCapacity,
            Mathf.FloorToInt((maximum - minimum + 0.0001f) / minimumLaneSeparation) + 1);
        if (standingLaneCount < 2)
            return false;

        float first = (minimum + maximum - minimumLaneSeparation * (standingLaneCount - 1)) * 0.5f;
        for (int i = 0; i < standingLaneCount; i++)
            standingLanes[i] = first + minimumLaneSeparation * i;

        pressureHighLaneCount = 0;
        float lowHeight = transform.position.y + lowestLaneHeight;
        for (int i = 0; i < standingLaneCount; i++)
            if (standingLanes[i] - lowHeight >= minimumLaneSeparation - 0.0001f)
                pressureHighLanes[pressureHighLaneCount++] = standingLanes[i];
        return pressureHighLaneCount > 0;
    }

    private static Vector3 SpriteWorldSize(SpriteRenderer renderer)
    {
        Vector3 size = renderer.sprite.bounds.size;
        Vector3 x = renderer.transform.TransformVector(Vector3.right * size.x);
        Vector3 y = renderer.transform.TransformVector(Vector3.up * size.y);
        return new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y), 1f);
    }

    private void BeginNextWave()
    {
        if (phase != Phase.Running || HasFlyingMissiles())
            return;
        if (remaining <= 0)
        {
            BeginDoorFall();
            return;
        }

        bool firstWave = waveNumber == 0;
        int availableRows = Mathf.Min(WaveCapacity, pressureHighLaneCount + 1);
        salvoCount = firstWave ? 1 : Mathf.Min(UnityEngine.Random.Range(2, availableRows + 1), remaining);
        waveNumber++;

        // Choose once, before warnings are shown. Low pressure comes from the farther
        // edge, so a stationary actor never has to jump into a later near-side high train.
        // Nothing tracks the actor or retaliates after the player edits a warning.
        int lowDirection = firstWave ? -1 :
            actorCollider.bounds.center.x >= gameplayCamera.transform.position.x ? 1 : -1;
        SetPlannedSalvo(0, lowDirection, transform.position.y + lowestLaneHeight,
            firstWave || salvoCount == 1 ? 1 : UnityEngine.Random.Range(1, 3), 0f);

        if (salvoCount == 2)
        {
            int lane = UnityEngine.Random.Range(0, pressureHighLaneCount);
            SetPlannedSalvo(1, -lowDirection, pressureHighLanes[lane], RandomShotCount(), 0f);
        }
        else if (salvoCount == 3)
        {
            int firstLane = UnityEngine.Random.Range(0, pressureHighLaneCount);
            int secondLane = UnityEngine.Random.Range(0, pressureHighLaneCount - 1);
            if (secondLane >= firstLane)
                secondLane++;
            SetPlannedSalvo(1, -lowDirection, pressureHighLanes[firstLane], RandomShotCount(), 0f);
            SetPlannedSalvo(2, lowDirection, pressureHighLanes[secondLane], RandomShotCount(), 0f);
        }

        int budget = remaining;
        for (int i = 0; i < salvoCount; i++)
        {
            // Keep every planned row populated, but never exceed the actual goal balance.
            int available = budget - (salvoCount - i - 1);
            salvos[i].ShotCount = Mathf.Min(salvos[i].ShotCount, available);
            budget -= salvos[i].ShotCount;
        }

        if (salvoCount > 1)
        {
            float latestUpperTail = 0f;
            for (int i = 1; i < salvoCount; i++)
                latestUpperTail = Mathf.Max(latestUpperTail, (salvos[i].ShotCount - 1) * salvoInterval);
            // For a stationary actor, the far-side high train is the last possible upper
            // threat. Let its tail clear the complete actor + missile width, then leave a
            // preparation margin before the low front arrives. This delays the WARNING
            // start; the low warning still gets its own full appearance + three seconds.
            float crossingTime = (actorCollider.bounds.size.x + missileVisualLength) / Mathf.Max(0.01f, missileSpeed);
            salvos[0].PreviewDelay = Mathf.Max(lowLaneDelay, latestUpperTail + crossingTime + jumpPreparationTime);
        }
        ShowWaveWarnings();
    }

    private static int RandomShotCount() => UnityEngine.Random.Range(1, SalvoLength + 1);

    private void SetPlannedSalvo(int index, int direction, float height, int shots, float delay)
    {
        salvos[index] = new PlannedSalvo
        {
            Height = height,
            Direction = direction,
            ShotCount = shots,
            PreviewDelay = delay,
            EmittedCount = 0,
            NextShotAt = 0f,
            PlayerAdjusted = false
        };
    }

    private void ShowWaveWarnings()
    {
        if (phase != Phase.Running || HasFlyingMissiles())
            return;
        HideWarnings();
        WarningView sizeReference = warningViews[0];
        float inset = Mathf.Min(CameraHalfWidth, sizeReference.PickSize.x * 0.5f +
            Mathf.Abs(sizeReference.PickCenterOffset.x) + WarningHorizontalInset);
        for (int i = 0; i < salvoCount; i++)
        {
            WarningView view = warningViews[activeWarningCount++];
            view.SalvoIndex = i;
            view.ShowAt = salvos[i].PreviewDelay;
            view.AlertPlayed = false;
            view.PointerId = NoPointer;
            float x = salvos[i].Direction < 0 ? ScreenRight - inset : ScreenLeft + inset;
            view.Renderer.transform.position = new Vector3(x, salvos[i].Height, transform.position.z);
            bool visibleNow = view.ShowAt <= 0f;
            view.Renderer.gameObject.SetActive(visibleNow);
            if (visibleNow)
            {
                // 立即出现的预警在本帧就播，不等下一帧 Tick。
                view.AlertPlayed = true;
                AudioManager.PlaySound(WarningAlertSound);
            }
            SetWarningAppearance(view, 0f);
        }
        waveElapsed = 0f;
        waveStage = WaveStage.Active;
    }

    private void TickArraySalvos(float deltaTime)
    {
        ReadWarningInput();
        waveElapsed += deltaTime;
        bool complete = true;
        for (int i = 0; i < activeWarningCount; i++)
        {
            WarningView view = warningViews[i];
            PlannedSalvo salvo = salvos[view.SalvoIndex];
            if (salvo.Complete)
                continue;

            if (salvo.EmittedCount == 0)
            {
                float age = waveElapsed - view.ShowAt;
                if (age >= 0f)
                {
                    view.Renderer.gameObject.SetActive(true);
                    if (!view.AlertPlayed)
                    {
                        view.AlertPlayed = true;
                        AudioManager.PlaySound(WarningAlertSound);
                    }
                    float progress = warningAppearSeconds <= 0f ? 1f : Mathf.Clamp01(age / warningAppearSeconds);
                    SetWarningAppearance(view, progress);
                    // Every lane gets its own full appearance + warning window.
                    if (age >= warningAppearSeconds + warningSeconds)
                        TryLaunchSalvoMissile(view);
                }
            }
            else if (waveElapsed >= salvo.NextShotAt)
                TryLaunchSalvoMissile(view);

            if (!salvos[view.SalvoIndex].Complete)
                complete = false;
        }

        // Wait for all scheduled emissions, not just for a temporary gap with no missiles.
        if (complete)
        {
            HideWarnings();
            waveStage = WaveStage.Flying;
        }
    }

    private static void SetWarningAppearance(WarningView view, float progress)
    {
        progress = Mathf.Clamp01(progress);
        // Use the shared DOTween easing functions, driven by the existing paused clock.
        // No independent tween can keep running after a warning is pooled or cancelled.
        float scale = DOVirtual.EasedValue(0.6f, 1f, progress, Ease.OutBack);
        view.Renderer.transform.localScale = view.NormalScale * scale;
        Color color = view.NormalColor;
        color.a = DOVirtual.EasedValue(0f, view.NormalColor.a, progress, Ease.OutQuad);
        view.Renderer.color = color;
    }

    private float CameraHalfWidth => gameplayCamera.orthographicSize * gameplayCamera.aspect;
    private float ScreenLeft => gameplayCamera.transform.position.x - CameraHalfWidth;
    private float ScreenRight => gameplayCamera.transform.position.x + CameraHalfWidth;

    private void TryLaunchSalvoMissile(WarningView view)
    {
        int index = view.SalvoIndex;
        PlannedSalvo salvo = salvos[index];
        if (salvo.Complete)
            return;

        Level150Missile free = null;
        foreach (Level150Missile missile in missiles)
        {
            if (!missile.IsFlying)
            {
                free = missile;
                break;
            }
        }
        // At most three rows with three missiles each: nine live missiles fit twelve slots.
        // If a slot is unavailable, keep the pending shot; never count it or overwrite one.
        if (free == null)
            return;

        float x = salvo.Direction < 0 ? ScreenRight + 1.5f : ScreenLeft - 1.5f;
        free.Launch(new Vector3(x, salvo.Height, transform.position.z), salvo.Direction,
            salvo.PlayerAdjusted);
        salvos[index].EmittedCount++;
        // Do not catch up several overdue shots in one frame after a hitch or pause.
        salvos[index].NextShotAt = waveElapsed + salvoInterval;
        view.PointerId = NoPointer;
        view.Renderer.gameObject.SetActive(false);
        // Hiding/locking at the first shot keeps the whole salvo on its preset lane.
    }

    private bool HasFlyingMissiles()
    {
        foreach (Level150Missile missile in missiles)
            if (missile != null && missile.IsFlying)
                return true;
        return false;
    }

    private void TickMissiles(float deltaTime)
    {
        foreach (Level150Missile missile in missiles)
        {
            if (!missile.IsFlying)
                continue;
            Bounds previous = missile.BodyBounds;
            missile.Advance(deltaTime, missileSpeed);
            Bounds current = missile.BodyBounds;
            // 终态（死亡/结束）后不再做任何碰撞判定，只推进位移与出屏回收。
            if (phase == Phase.Running || phase == Phase.Falling)
            {
                float nearest = float.PositiveInfinity;
                HitTarget hit = HitTarget.None;
                ConsiderHit(previous, current, actorCollider.bounds, HitTarget.Actor, ref hit, ref nearest);
                // Default system-authored rows can never solve or break the hanging door,
                // even if later layout changes make their visual bounds overlap it. Only
                // a row whose warning was actually moved by the player may hit rope/door.
                if (phase == Phase.Running && missile.CanHitDoorOrRope)
                {
                    ConsiderHit(previous, current, RopeBounds, HitTarget.Rope, ref hit, ref nearest);
                    ConsiderHit(previous, current, DoorBounds, HitTarget.Door, ref hit, ref nearest);
                }

                if (hit != HitTarget.None)
                {
                    Vector3 impact = Vector3.Lerp(previous.center, current.center, nearest);
                    impact.x += missile.Direction * current.extents.x;
                    impact.z = transform.position.z;
                    PlayExplosion(impact);
                    if (hit == HitTarget.Actor)
                    {
                        // 炸开的本发随爆炸消失（Cleanup，不计数）；其余在场导弹继续按原轨迹飞完。
                        Recycle(missile, RecycleReason.Cleanup);
                        StopRound();
                        // 复用 BaseLevelCtrl 的陷阱死亡/失败链，不新增失败面板或延时任务。
                        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
                        // 先让标准Die清空移动，再复用50关击飞，避免死亡入口覆盖击飞速度。
                        actorBlasted = true;
                        actor.Knockback(new Vector2(missile.Direction * 0.5f, 0.7f), 50f).Forget();
                        return;
                    }
                    if (hit == HitTarget.Rope)
                    {
                        Recycle(missile, RecycleReason.Cleanup);
                        BeginDoorFall();
                    }
                    else if (hit == HitTarget.Door)
                    {
                        // 炸到门的本发随爆炸消失（Cleanup，不计数）；其余存量继续飞完。
                        Recycle(missile, RecycleReason.Cleanup);
                        BreakDoor();
                        return;
                    }
                }
            }

            // 命中本发随爆炸立即回收（Cleanup，不计数）；其余只在真实飞出屏外回收。
            if ((missile.Direction > 0 && current.min.x > ScreenRight + 0.5f) ||
                (missile.Direction < 0 && current.max.x < ScreenLeft - 0.5f))
            {
                Recycle(missile, RecycleReason.Exited);
            }
        }
    }

    // 导弹只水平飞行；连续线段与扩张矩形求交，避免单帧越过角色/细绳。
    private static void ConsiderHit(Bounds previous, Bounds current, Bounds target, HitTarget candidate,
        ref HitTarget selected, ref float nearest)
    {
        target.Expand(new Vector3(current.size.x, current.size.y, 0f));
        float y = current.center.y;
        if (y < target.min.y || y > target.max.y)
            return;
        float from = previous.center.x;
        float to = current.center.x;
        float distance = to - from;
        float time;
        if (from >= target.min.x && from <= target.max.x)
            time = 0f;
        else if (Mathf.Abs(distance) < 0.00001f)
            return;
        else
            time = ((distance > 0f ? target.min.x : target.max.x) - from) / distance;
        if (time < 0f || time > 1f || time >= nearest)
            return;
        nearest = time;
        selected = candidate;
    }

    private Bounds DoorBounds
    {
        get
        {
            // Collider 禁用时 bounds 为空，因此从其序列化形状求轴对齐范围。
            Vector3 center = doorTrigger.transform.TransformPoint(doorTrigger.offset);
            Vector3 scale = doorTrigger.transform.lossyScale;
            return new Bounds(center, new Vector3(doorTrigger.size.x * Mathf.Abs(scale.x),
                doorTrigger.size.y * Mathf.Abs(scale.y), 1f));
        }
    }

    private Bounds RopeBounds
    {
        get
        {
            Bounds bounds = rope.bounds;
            bounds.size = new Vector3(Mathf.Max(0.12f, bounds.size.x), bounds.size.y, 1f);
            return bounds;
        }
    }

    private void Recycle(Level150Missile missile, RecycleReason reason)
    {
        if (!missile.IsFlying)
            return;
        missile.ReturnToPool();
        // 唯一计数入口：先关飞行标记，再记自然出屏；落门及重开清场永不计数。
        if (reason != RecycleReason.Exited || phase != Phase.Running)
            return;
        remaining = Mathf.Max(0, remaining - 1);
        RefreshWallText(true);
        if (remaining == 0)
            BeginDoorFall();
    }

    /// <summary>
    /// 让 Prefab 上的 I2 Localize 刷新文本，由本关回调按位置填入 {0}；
    /// I2 缺 term 时回退到简体中文前缀加数字，不静默显示 key。
    /// 数字真实变动时再播一次与 106 关同口径的单峰脉冲（数字逐字 1→1.15→1 并泛白）。
    /// </summary>
    private void RefreshWallText(bool animatePulse)
    {
        if (wallText == null)
            return;
        string number = remaining.ToString(CultureInfo.InvariantCulture);
        if (wallTextLocalize != null)
        {
            wallTextLocalize.OnLocalize(true);
            if (!string.IsNullOrEmpty(wallText.text) &&
                wallText.text.IndexOf(number, StringComparison.Ordinal) >= 0 &&
                !wallText.text.Contains("{0}"))
            {
                if (animatePulse)
                    PlayWallTextPulse();
                else
                    ApplyWallTextPulse(0f);
                return;
            }
        }
        wallText.text = WallTextFallbackPrefix + number;
        if (animatePulse)
            PlayWallTextPulse();
        else
            ApplyWallTextPulse(0f);
    }

    /// <summary>I2 LocalizeEvent 回调：{0} 为剩余导弹数，语言刷新时同样使用当前计数。</summary>
    public void FormatWallTextTranslation()
    {
        if (!Application.isPlaying) return;
        const string fallback = WallTextFallbackPrefix + "{0}";
        string template = string.IsNullOrEmpty(Localize.MainTranslation) ? fallback : Localize.MainTranslation;
        try
        {
            Localize.MainTranslation = string.Format(CultureInfo.InvariantCulture, template, remaining);
        }
        catch (FormatException)
        {
            if (!wallTextFormatWarningLogged)
            {
                Debug.LogWarning("[Level150] Wall translation requires a {0} placeholder.", this);
                wallTextFormatWarningLogged = true;
            }
            Localize.MainTranslation = string.Format(CultureInfo.InvariantCulture, fallback, remaining);
        }
    }

    private void PlayWallTextPulse()
    {
        if (wallText == null || !wallText.gameObject.activeInHierarchy)
            return;
        KillWallTextPulse(false);
        DisposeWallTextAnimator();
        try
        {
            wallTextAnimator = new DOTweenTMPAnimator(wallText);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Level150] Wall text pulse skipped: " + exception.Message, this);
            wallTextAnimator = null;
            return;
        }
        if (wallTextAnimator == null || wallTextAnimator.target == null)
        {
            DisposeWallTextAnimator();
            return;
        }
        float progress = 0f;
        wallTextPulseTween = DOTween.To(
                () => progress,
                value =>
                {
                    progress = value;
                    ApplyWallTextPulse(Mathf.Sin(value * Mathf.PI));
                },
                1f,
                Mathf.Max(0.01f, WallTextPulseDuration))
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                wallTextPulseTween = null;
                DisposeWallTextAnimator();
                ApplyWallTextPulse(0f);
            });
        if (paused)
            wallTextPulseTween.Pause();
    }

    /// <summary>
    /// 与 106 关 ApplyRemainingLayersText 的数字逐字效果同口径：
    /// 只放大/泛白数字子串，不改前后缀与整体 Transform。
    /// </summary>
    private void ApplyWallTextPulse(float pulse)
    {
        if (wallText == null)
            return;
        string number = remaining.ToString(CultureInfo.InvariantCulture);
        wallText.ForceMeshUpdate(true, true);
        int numberStartIndex = wallText.text.IndexOf(number, StringComparison.Ordinal);
        if (wallTextAnimator == null || numberStartIndex < 0)
            return;
        wallTextAnimator.Refresh();
        float clamped = Mathf.Clamp01(pulse);
        Vector3 scale = Vector3.one * Mathf.Lerp(1f, WallTextPulseMaxScale, clamped);
        Color32 baseColor = wallText.color;
        Color32 digitColor = Color32.Lerp(baseColor, new Color32(255, 255, 255, 255), clamped);
        for (int i = 0; i < number.Length; i++)
        {
            int charIndex = numberStartIndex + i;
            wallTextAnimator.SetCharScale(charIndex, scale);
            wallTextAnimator.SetCharColor(charIndex, digitColor);
        }
    }

    private void KillWallTextPulse(bool restoreBaseAppearance)
    {
        if (wallTextPulseTween != null)
        {
            wallTextPulseTween.Kill(false);
            wallTextPulseTween = null;
        }
        if (restoreBaseAppearance)
            ApplyWallTextPulse(0f);
    }

    private void DisposeWallTextAnimator()
    {
        if (wallTextAnimator == null)
            return;
        try
        {
            if (wallTextAnimator.target != null)
                wallTextAnimator.Reset();
        }
        finally
        {
            wallTextAnimator.Dispose();
            wallTextAnimator = null;
        }
    }

    private void StopWallTextPulse()
    {
        KillWallTextPulse(false);
        DisposeWallTextAnimator();
    }

    private void BeginDoorFall()
    {
        if (phase != Phase.Running)
            return;
        phase = Phase.Falling;
        StopBattleSound(true);
        HideWarnings();
        rope.gameObject.SetActive(false);
        fallVelocity = 0f;
        // 与气球关一致：门体下落全程开放进门 Trigger，下落中重叠即可真实入门。
        doorTrigger.enabled = true;
        // 保留已飞出的危险；不再检测门/绳，不再新增导弹。
    }

    private void TickDoor(float deltaTime)
    {
        fallVelocity += fallGravity * deltaTime;
        Vector3 position = door.position;
        position.y = Mathf.Max(doorLanding.position.y, position.y - fallVelocity * deltaTime);
        door.position = position;
        if (position.y > doorLanding.position.y)
            return;
        phase = Phase.Open;
        // 落稳不清场：存量导弹继续按原轨迹飞到自然出屏（Open 只位移回收，不再碰撞计数）。
        // 必须真实进入标准门 Trigger；绝不直接派发成功。
    }

    public void OnDoorEntered(Collider2D collider)
    {
        if (!initialized || paused || (phase != Phase.Falling && phase != Phase.Open) || actor.IsDied ||
            collider == null || collider.GetComponentInParent<ActorCtrl>() != actor)
            return;
        levelCtrl.OnActorTrigger2D(collider);
    }

    private void BreakDoor()
    {
        phase = Phase.Broken;
        StopBattleSound(true);
        HideWarnings();
        // 破门不清场：已发射的存量继续按原轨迹飞到自然出屏（只位移回收，不再碰撞计数）；
        // 未发射的随预警取消不再发射。
        brokenDoor.transform.position = door.position;
        brokenDoor.gameObject.SetActive(true);
        door.gameObject.SetActive(false);
        rope.gameObject.SetActive(false);
        // 炸碎的门按门下落同款重力落到地面站住，不悬空；落点按碎片实际包围盒算，不用 doorLanding。
        brokenDoorRestY = Mathf.Min(door.position.y,
            ground.bounds.max.y + brokenDoor.bounds.size.y * 0.5f);
        fallVelocity = 0f;
        brokenDoorFalling = true;
        // 故意不派发失败、不锁玩家操作，等待玩家自行 Reset。
    }

    private void TickBrokenDoor(float deltaTime)
    {
        fallVelocity += fallGravity * deltaTime;
        Vector3 position = brokenDoor.transform.position;
        position.y -= fallVelocity * deltaTime;
        if (position.y <= brokenDoorRestY)
        {
            position.y = brokenDoorRestY;
            brokenDoorFalling = false;
        }
        brokenDoor.transform.position = position;
    }

    private void HideWarnings()
    {
        waveStage = WaveStage.None;
        ReleaseWarningPointers();
        foreach (WarningView view in warningViews)
        {
            if (view == null || view.Renderer == null)
                continue;
            view.Renderer.transform.localScale = view.NormalScale;
            view.Renderer.color = view.NormalColor;
            view.Renderer.gameObject.SetActive(false);
        }
        activeWarningCount = 0;
        if (warning != null)
            warning.gameObject.SetActive(false);
    }

    private void ReleaseWarningPointers()
    {
        foreach (WarningView view in warningViews)
            if (view != null)
                view.PointerId = NoPointer;
    }

    private void ReadWarningInput()
    {
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < activeWarningCount; i++)
                warningViews[i].PointerSeen = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                    TryCapturePointer(touch.fingerId, touch.position);
                WarningView view = FindPointerWarning(touch.fingerId);
                if (view == null)
                    continue;
                view.PointerSeen = true;
                if (touch.phase == TouchPhase.Canceled)
                    view.PointerId = NoPointer;
                else
                {
                    MoveWarning(view, touch.position);
                    if (touch.phase == TouchPhase.Ended)
                        view.PointerId = NoPointer;
                }
            }
            for (int i = 0; i < activeWarningCount; i++)
                if (!warningViews[i].PointerSeen)
                    warningViews[i].PointerId = NoPointer;
            return;
        }

        for (int i = 0; i < activeWarningCount; i++)
            if (warningViews[i].PointerId >= 0)
                warningViews[i].PointerId = NoPointer;
        if (Input.GetMouseButtonDown(0))
            TryCapturePointer(-1, Input.mousePosition);
        WarningView mouseView = FindPointerWarning(-1);
        if (mouseView != null)
        {
            if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
                MoveWarning(mouseView, Input.mousePosition);
            if (Input.GetMouseButtonUp(0))
                mouseView.PointerId = NoPointer;
        }
    }

    private bool CanDragWarning(WarningView view)
    {
        return view.Renderer.gameObject.activeSelf && salvos[view.SalvoIndex].EmittedCount == 0;
    }

    private WarningView FindPointerWarning(int pointerId)
    {
        for (int i = 0; i < activeWarningCount; i++)
            if (warningViews[i].PointerId == pointerId && CanDragWarning(warningViews[i]))
                return warningViews[i];
        return null;
    }

    private void TryCapturePointer(int pointerId, Vector2 screenPosition)
    {
        if (FindPointerWarning(pointerId) != null ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId)))
            return;
        Vector3 world = ScreenToWorld(screenPosition);
        WarningView closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < activeWarningCount; i++)
        {
            WarningView view = warningViews[i];
            if (view.PointerId != NoPointer || !CanDragWarning(view))
                continue;
            // Use final-size hit bounds even while the sprite is scaling/fading in.
            var bounds = new Bounds(view.Renderer.transform.position + view.PickCenterOffset, view.PickSize);
            bounds.Expand(WarningPickPadding * 2f);
            world.z = bounds.center.z;
            float distance = (world - bounds.center).sqrMagnitude;
            if (bounds.Contains(world) && distance < closestDistance)
            {
                closest = view;
                closestDistance = distance;
            }
        }
        if (closest == null)
            return;
        closest.PointerId = pointerId;
        closest.DragOffsetY = closest.Renderer.transform.position.y - world.y;
    }

    private Vector3 ScreenToWorld(Vector2 position)
    {
        return gameplayCamera.ScreenToWorldPoint(new Vector3(position.x, position.y,
            transform.position.z - gameplayCamera.transform.position.z));
    }

    private void MoveWarning(WarningView view, Vector2 screenPosition)
    {
        // Keep this shot's spawn-side X and depth; dragging changes only its lane height.
        Vector3 position = view.Renderer.transform.position;
        float pointerY = ScreenToWorld(screenPosition).y + view.DragOffsetY;
        float newHeight = ClosestFreeLane(view.SalvoIndex, pointerY);
        if (!Mathf.Approximately(newHeight, salvos[view.SalvoIndex].Height))
            salvos[view.SalvoIndex].PlayerAdjusted = true;
        position.y = newHeight;
        salvos[view.SalvoIndex].Height = position.y;
        view.Renderer.transform.position = position;
    }

    private float ClosestFreeLane(int salvoIndex, float desiredHeight)
    {
        float minimum = transform.position.y + lowestLaneHeight;
        float maximum = gameplayCamera.transform.position.y + gameplayCamera.orthographicSize - WarningTopInset;
        float desired = Mathf.Clamp(desiredHeight, minimum, maximum);
        if (IsLaneFree(salvoIndex, desired))
            return desired;

        float best = salvos[salvoIndex].Height;
        float bestDistance = Mathf.Abs(best - desired);
        ConsiderFreeLane(salvoIndex, minimum, desired, minimum, maximum, ref best, ref bestDistance);
        ConsiderFreeLane(salvoIndex, maximum, desired, minimum, maximum, ref best, ref bestDistance);
        // The nearest free point is at a blocked interval's edge or a global bound.
        // Both directions now coexist. Reserve every other row's height, not only the
        // same side; moving a warning must not introduce a head-on shared lane.
        for (int i = 0; i < salvoCount; i++)
        {
            if (i == salvoIndex)
                continue;
            ConsiderFreeLane(salvoIndex, salvos[i].Height - minimumLaneSeparation, desired,
                minimum, maximum, ref best, ref bestDistance);
            ConsiderFreeLane(salvoIndex, salvos[i].Height + minimumLaneSeparation, desired,
                minimum, maximum, ref best, ref bestDistance);
        }
        return best;
    }

    private void ConsiderFreeLane(int salvoIndex, float candidate, float desired, float minimum, float maximum,
        ref float best, ref float bestDistance)
    {
        if (candidate < minimum || candidate > maximum || !IsLaneFree(salvoIndex, candidate))
            return;
        float distance = Mathf.Abs(candidate - desired);
        if (distance < bestDistance)
        {
            best = candidate;
            bestDistance = distance;
        }
    }

    private bool IsLaneFree(int salvoIndex, float height)
    {
        for (int i = 0; i < salvoCount; i++)
            if (i != salvoIndex && Mathf.Abs(height - salvos[i].Height) < minimumLaneSeparation - 0.0001f)
                return false;
        return true;
    }

    private void PlayExplosion(Vector3 position)
    {
        ExplosionSlot slot = explosions[0];
        foreach (ExplosionSlot candidate in explosions)
        {
            if (candidate.Remaining <= 0f)
            {
                slot = candidate;
                break;
            }
            if (candidate.Remaining < slot.Remaining)
                slot = candidate;
        }
        slot.Root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        slot.Root.transform.position = position;
        slot.Root.transform.localScale = Vector3.one * explosionScale;
        slot.Root.gameObject.SetActive(true);
        slot.Root.Play(true);
        slot.Remaining = slot.Lifetime;
        AudioManager.PlaySound("rocketExplosion");
    }

    private void TickExplosions(float deltaTime)
    {
        foreach (ExplosionSlot slot in explosions)
        {
            if (slot.Remaining <= 0f)
                continue;
            slot.Remaining -= deltaTime;
            if (slot.Remaining <= 0f)
            {
                slot.Root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                slot.Root.gameObject.SetActive(false);
            }
        }
    }

    private void ClearMissiles()
    {
        foreach (Level150Missile missile in missiles)
            if (missile != null)
                Recycle(missile, RecycleReason.Cleanup);
    }

    private void StopRound()
    {
        phase = Phase.Stopped;
        StopBattleSound(true);
        StopWallTextPulse();
        if (warning != null)
            HideWarnings();
        if (doorTrigger != null)
            doorTrigger.enabled = false;
        hud?.SetInteractable(false);
        // 死亡停发后不清场：存量导弹由 Update 死亡分支继续 TickMissiles，按原轨迹飞到自然出屏；
        // 成功进门、Reset、Disable 的清场由调用方显式执行。自然出屏在非 Running 下只回收不计数。
    }

    private void OnPause(GamePauseEvent evt) { gamePaused = true; ApplyPause(); }
    private void OnResume(GameResumeEvent evt) { gamePaused = false; ApplyPause(); }
    private void OnGameStateChanged(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;
        gamePaused = evt.NewState != GamePlayStateName.PLAYING;
        ApplyPause();
    }
    private void OnApplicationPause(bool value) { applicationPaused = value; ApplyPause(); }
    private void OnFailureStarted(OnTriggerTrapEvent evt) => StopRound();
    private void OnFailureStarted(OnTriggerFailAreaEvent evt) => StopRound();

    private void StartBattleSoundOnce()
    {
        if (battleSoundStarted)
            return;

        battleSoundStarted = true;
        // Reuse Level 129's post-switch battle track. It is an SFX overlay, so the
        // regular in-game music is paused rather than replaced.
        battleSound = AudioManager.PlaySound("l69battle", loop: true);
        if (battleSound == null)
            return;

        AudioManager.PauseMusic();
        battleMusicPaused = true;
    }

    private void StopBattleSound(bool resumeMusic)
    {
        battleSound?.Stop();
        battleSound = null;
        battleSoundStarted = false;
        if (resumeMusic && battleMusicPaused)
            AudioManager.ResumeMusic();
        battleMusicPaused = false;
    }

    private void OnReset(GameResetEvent evt)
    {
        // 项目 Reset 会重新实例化关卡；这里终止旧实例，不自行制造第二套重开流程。
        StopRound();
        ClearMissiles();
        ClearExplosions();
    }

    private void ApplyPause()
    {
        paused = gamePaused || applicationPaused;
        if (!initialized)
            return;
        hud.SetInteractable(!paused && phase == Phase.Waiting);
        if (paused)
        {
            ReleaseWarningPointers();
            // Waiting includes the shared StartDoor entrance: loading PAUSE does not pause
            // its fade-in. Leave physics/input ownership there, without capturing stale
            // velocity. After BeginRound, keep the existing physical pause contract.
            if (phase != Phase.Waiting && !actorPauseCaptured &&
                (!actor.IsDied || actorBlasted) && !levelCtrl.IsActorEnterDoor)
            {
                actorPauseCaptured = true;
                actorWasSimulated = actorBody.simulated;
                actorWasControllable = levelCtrl.ActorCtrlable;
                actorVelocity = actorBody.linearVelocity;
                actorAngularVelocity = actorBody.angularVelocity;
                actorBody.simulated = false;
                levelCtrl.SetCtrlable(false);
            }
        }
        else
            RestoreActorPause();

        foreach (ExplosionSlot slot in explosions)
        {
            if (slot.Remaining <= 0f)
                continue;
            if (paused)
                slot.Root.Pause(true);
            else
                slot.Root.Play(true);
        }
        // 与 106 关终场计数 Tween 同步暂停/恢复，避免暂停期间数字脉冲偷跑。
        if (wallTextPulseTween != null)
        {
            if (paused)
                wallTextPulseTween.Pause();
            else
                wallTextPulseTween.Play();
        }
    }

    private void RestoreActorPause()
    {
        if (!actorPauseCaptured)
            return;
        actorPauseCaptured = false;
        if (actorBody != null)
        {
            actorBody.simulated = actorWasSimulated;
            if (actor != null && (!actor.IsDied || actorBlasted))
            {
                actorBody.linearVelocity = actorVelocity;
                actorBody.angularVelocity = actorAngularVelocity;
            }
        }
        if (levelCtrl != null)
            levelCtrl.SetCtrlable(actorWasControllable);
    }

    private void ClearExplosions()
    {
        foreach (ExplosionSlot slot in explosions)
        {
            if (slot == null || slot.Root == null)
                continue;
            slot.Remaining = 0f;
            slot.Root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            slot.Root.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref subscriber);
        StopRound();
        ClearMissiles();
        ClearExplosions();
        RestoreActorPause();
        if (hud != null)
            hud.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref subscriber);
        StopWallTextPulse();
        RestoreActorPause();
        if (hud != null)
            Destroy(hud.gameObject);
        if (poolRoot != null)
            Destroy(poolRoot.gameObject);
    }
}
