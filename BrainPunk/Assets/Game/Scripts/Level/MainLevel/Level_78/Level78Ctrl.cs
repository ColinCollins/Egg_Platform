using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Bear.SaveModule;
using DG.Tweening;
using Game.Common;
using Game.Events;
using Game.Level;
using Game.Level.Actor;
using Game.Level078;
using Game.Play;
using Game.Scripts.Common;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Level 078 本地射箭对决控制器；拥有阶段、挡板、角色、箭矢、计分板、HUD 与结算适配。
/// </summary>
public sealed class Level78Ctrl : MonoBehaviour, IEventSender, IDebuger
{
    #region Constants

    private const int ArrowCountPerParticipant = 3;
    private const int ScoreCellCountPerParticipant = 4;
    private const int TotalScoreCellIndex = ScoreCellCountPerParticipant - 1;
    private const float ScoreFontSize = 76f;
    private const float ScoreTextLocalScale = 0.1f;
    private const float SingleScorePopStartScaleMultiplier = 0.65f;
    private const float ScoreFeedbackDuration = 0.2f;
    private const string SingleScoreSfxTag = "normalScore";
    private const string TotalScoreRevealSfxTag = "finalScore";
    private const string TargetHitSfxTag = "targetHit";
    // arrowWhoosh.ogg 自带约 0.54s 前导静音（实测发声段 0.54–0.74s），从发声点起播，
    // 否则箭命中时破空音尚未出声，会被命中音与计分音掩盖。
    private const float ArrowWhooshSfxStartSeconds = 0.54f;
    // 射箭动作音效以 1.0 音量播放，避免被同帧响起的计分音（normalScore，默认 0.4）掩盖。
    private const float ArrowActionSfxVolume = 1f;
    private const float TotalScoreOutlineWidth = 0.2631579f;
    private const string DemonSkinName = "box_bad";
    private const string HintBubbleResourceName = "HintBubble";
    private const string JumpButtonPath = "Root/jump_btn";
    /// <summary>玩家箭尖触发收集星的 trigger 圆半径；配合星星自身 0.58 触发半径，保证不脱靶的箭必中、脱靶箭不触发。</summary>
    private const float ArrowStarCollectionTipRadius = 0.2f;
    private const string AimButtonPath = "Root/aim_btn";
    // 小恶魔气泡走 HintBubble 的 key 驱动链路（LocalizationManager.GetTranslation + 切语言热刷新）；
    // key 在本组件 Inspector 上可配；简体中文已登记到飞书多语言表，其他语言与本地 CSV/I2 导入由后续同步流程处理。
    // 本地词条未导入前，气泡 fallback 显示 key 原文。
    [SerializeField] private string turnAnnouncementKey = "S_LevelTips_Des_level_78_2";
    [SerializeField] private string victoryAnnouncementKey = "S_LevelTips_Des_level_78_3";
    [SerializeField] private string failureAnnouncementKey = "S_LevelTips_Des_level_78_4";
    private const float DemonSpeechFontSize = 4f;
    private static readonly Color32 BoxTotalScoreColor = new Color32(0x2f, 0x7e, 0xdf, 0xff);
    private static readonly Color32 DemonTotalScoreColor = new Color32(0xe1, 0x1b, 0x00, 0xff);
    private static readonly Color32 TotalScoreOutlineColor = new Color32(0xff, 0xff, 0xff, 0xff);
    private static readonly Vector2[,] ScoreCellLocalPositions =
    {
        {
            new Vector2(-2.15f, 0.16f),
            new Vector2(-0.58f, 0.16f),
            new Vector2(1.02f, 0.16f),
            new Vector2(2.55f, 0.16f)
        },
        {
            new Vector2(-2.12f, -1.48f),
            new Vector2(-0.50f, -1.48f),
            new Vector2(1.10f, -1.48f),
            new Vector2(2.67f, -1.48f)
        }
    };

    #endregion

    #region Inspector

    [Header("Scene References")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private Transform board;
    [SerializeField] private Transform target;
    [SerializeField] private Transform scoreBoard;
    [SerializeField] private TMP_FontAsset scoreFont;
    [SerializeField] private GameObject aimGaugeRoot;
    [SerializeField] private Transform aimPointer;
    /// <summary>弧形蓄力表中承载完整红黄绿区段的主图片。</summary>
    [SerializeField] private Image aimGaugeColorRange;
    /// <summary>近靶满分点直接显示的绿色素材图片（Sp_l73_jdt_02），不使用运行时材质绘制。</summary>
    [SerializeField] private Image nearAimGaugeColorRange;
    [SerializeField] private SpriteRenderer arrowTemplate;
    /// <summary>箭矢破空音；资源自带约 0.54s 前导静音，由本关自带 AudioSource 跳过静音起播。</summary>
    [SerializeField] private AudioClip arrowWhooshClip;
    /// <summary>拖板门控的行走收集星（槽位 Two）：挡板吸附到近靶满分点前禁用其收集触发器（星星仍可见，箭与玩家都无法收集），到位后按存档恢复；近射释放点在其右侧，箭程不经过，保证只能走路收集。</summary>
    [SerializeField] private Postcard_Star boardGatedStar;

    [Header("Board")]
    [SerializeField] private float nearBoardLocalX = 2.65f;
    [SerializeField] private float boardSnapDuration = 0.2f;
    [SerializeField] private float shootingPointLeftOffset = 0.85f;
    [SerializeField] private float shootingPointTolerance = 0.12f;

    [Header("Aim")]
    [SerializeField] private Vector2 pointerAngleRange = new Vector2(-70f, 70f);
    [SerializeField] private float pointerOneWayDuration = 0.8f;
    [SerializeField] private float aimGaugeForwardOffset = 1.6f;
    [SerializeField] private float aimGaugeVerticalOffset = 0.76f;
    [SerializeField] private float greenHalfWidth = 0.1f;
    [SerializeField] private float yellowOuterHalfWidth = 0.3f;

    [Header("Archery")]
    [Range(0f, 1f)]
    [SerializeField] private float releaseNormalizedTime = 0.55f;
    /// <summary>双方箭矢离弦后的恒定水平速度；飞行时长按释放点到落点的水平距离派生。</summary>
    [SerializeField, Min(0.01f)] private float arrowHorizontalSpeed = 27.65625f;
    /// <summary>Level 078 飞行轨迹拖尾使用的本地材质；未绑定时只关闭拖尾，不阻断射箭。</summary>
    [SerializeField] private Material arrowTrailMaterial;
    /// <summary>箭抵达后仍保留的短促拖尾时间。</summary>
    [SerializeField, Min(0f)] private float arrowTrailTime = 0.1f;
    /// <summary>箭尾处的拖尾起始宽度；尾端沿宽度曲线收窄到零。</summary>
    [SerializeField, Min(0f)] private float arrowTrailWidth = 0.1f;
    [SerializeField] private float demonArrowInterval = 0.2f;
    [SerializeField] private float demonResultHoldDuration = 1f;
    [SerializeField] private float demonArrowFadeDuration = 0.5f;
    [SerializeField] private Vector3 demonHomePosition = new Vector3(-4.2f, -2.06f, 0f);
    [SerializeField] private Vector3 demonShootingPosition = new Vector3(-2.84f, -2.06f, 0f);

    /// <summary>小恶魔每句角色讲话气泡的显示时长；行动交接播报不阻塞操作，比分结果播报结束后才触发胜负链。</summary>
    [Header("Dialogue")]
    [SerializeField, Min(0f)] private float demonSpeechDuration = 1.5f;

    /// <summary>命中时计分靶沿箭水平行进方向后退的距离。</summary>
    [Header("Target Impact")]
    [SerializeField, Min(0f)] private float targetRecoilDistance = 0.06f;
    /// <summary>计分靶回弹时越过基准位置的距离。</summary>
    [SerializeField, Min(0f)] private float targetReboundDistance = 0.015f;
    /// <summary>计分靶从基准位置后退到最大位移的时长。</summary>
    [SerializeField, Min(0f)] private float targetRecoilDuration = 0.06f;
    /// <summary>计分靶从最大位移回弹并越过基准位置的时长。</summary>
    [SerializeField, Min(0f)] private float targetReboundDuration = 0.06f;
    /// <summary>计分靶从反向回弹位置精确恢复基准位置的时长。</summary>
    [SerializeField, Min(0f)] private float targetRecoveryDuration = 0.1f;
    /// <summary>命中后仅遮住箭头前端的圆形遮罩 Sprite；不得改变箭的逻辑插入点。</summary>
    [SerializeField] private Sprite hitArrowOcclusionMaskSprite;
    /// <summary>箭头沿自身方向进入靶面的视觉深度，当前 0.15 对应原图约 15px。</summary>
    [SerializeField, Min(0f)] private float hitArrowOcclusionDepth = 0.15f;
    /// <summary>新命中箭围绕原插入点产生的首段轻量余振角度。</summary>
    [SerializeField, Min(0f)] private float hitArrowSwingAngle = 3f;
    /// <summary>新命中箭完成全部衰减余振并恢复原插入角的总时长。</summary>
    [SerializeField, Min(0f)] private float hitArrowVibrationDuration = 0.22f;

    #endregion

    #region Runtime State

    private readonly List<GameObject> spawnedArrows = new List<GameObject>();
    private readonly List<SpriteRenderer> demonArrowRenderers = new List<SpriteRenderer>();
    private AudioSource arrowWhooshSource;
    private readonly TMP_Text[,] scoreTexts =
        new TMP_Text[2, ScoreCellCountPerParticipant];
    private readonly Tween[,] scoreFeedbackTweens =
        new Tween[2, ScoreCellCountPerParticipant];
    private readonly string[,] committedScoreTexts =
        new string[2, ScoreCellCountPerParticipant];

    private EventSubscriber subscriber;
    private Level78Duel duel;
    private Level78ActorCtrl player;
    private Level78ActorCtrl demon;
    private GamePlayPanel gamePlayPanel;
    private CustomButton jumpButton;
    private CustomButton aimButton;
    private BoxCollider2D boardCollider;
    private Tween pointerTween;
    private Tween boardSnapTween;
    private Tween settlementDelayTween;
    private Tween speechCloseTween;
    private Sequence targetImpactTween;
    private HintBubble demonSpeechBubble;
    private Transform activeHitArrowRoot;
    private Quaternion activeHitArrowRestLocalRotation;
    private Action demonWalkCompleted;
    private int sessionVersion;
    private int demonWalkSession;
    private int demonArrowIndex;
    private int boxArrowIndex;
    private float farBoardLocalX;
    private float demonWalkTargetWorldX;
    private float demonWalkDirection;
    private bool dragActive;
    private bool boardSnapBusy;
    private bool demonWalkActive;
    private bool demonWalkPrepared;
    private bool demonFaceRightOnArrival;
    private float dragOffsetX;
    private Level78BoardPosition dragOrigin;
    private Vector3 targetRestLocalPosition;
    private bool targetRestPositionCaptured;
    private bool settlementPendingAfterTargetImpact;
    private bool turnAnnouncementShown;
    private bool settlementAnnouncementStarted;
    private bool isGamePaused;

    #endregion

    #region Lifecycle

    private void Start()
    {
        ResolveReferences();
        BindEvents();
        InitializeAttempt();
    }

    private void Update()
    {
        if (duel == null)
            return;

        ResolveAndBindPanel();
        UpdateBoardDragInput();
        UpdatePlayerBoundaryAndQualification();
    }

    private void FixedUpdate()
    {
        if (duel != null)
            UpdateDemonWalk();
    }

    private void OnDisable()
    {
        CleanupAttempt(false);
    }

    private void OnDestroy()
    {
        CleanupAttempt(false);
        EventsUtils.ResetEvents(ref subscriber);
    }

    #endregion

    #region Initialization

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<BaseLevelCtrl>();

        player = levelCtrl != null ? levelCtrl.Actor as Level78ActorCtrl : null;
        board ??= FindDeepChild(transform, "Sp_l78_img_03");
        target ??= FindDeepChild(transform, "Sp_l78_img_01");
        scoreBoard ??= FindDeepChild(transform, "Sp_l78_img_02");
        aimGaugeRoot ??= FindDeepChild(transform, "Level078_AimGauge")?.gameObject;
        aimPointer ??= FindDeepChild(transform, "Level078_AimPointer");
        if (aimGaugeColorRange == null)
            aimGaugeColorRange = FindDeepChild(transform, "ArrowRange_normal")?.GetComponent<Image>();
        if (nearAimGaugeColorRange == null)
            nearAimGaugeColorRange = FindDeepChild(transform, "ArrowRange_green")?.GetComponent<Image>();

        if (arrowTemplate == null)
        {
            Transform arrowTransform = FindDeepChild(transform, "Sp_l78_jian_01");
            arrowTemplate = arrowTransform != null ? arrowTransform.GetComponent<SpriteRenderer>() : null;
        }

        if (target != null && !targetRestPositionCaptured)
        {
            targetRestLocalPosition = target.localPosition;
            targetRestPositionCaptured = true;
        }
    }

    private void InitializeAttempt()
    {
        CleanupAttempt(false);
        ++sessionVersion;
        ResolveReferences();

        if (levelCtrl == null || player == null || board == null || target == null || scoreBoard == null || scoreFont == null ||
            aimGaugeRoot == null || aimPointer == null || aimGaugeColorRange == null || nearAimGaugeColorRange == null ||
            arrowTemplate == null)
        {
            this.LogError("[Level78Ctrl] Required Level 078 prefab references are incomplete.");
            return;
        }

        Level78FarScoringProfile scoringProfile;
        try
        {
            scoringProfile = new Level78FarScoringProfile(greenHalfWidth, yellowOuterHalfWidth);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            this.LogWarning($"[Level78Ctrl] Invalid aim calibration; using default profile. {exception.Message}");
            scoringProfile = Level78FarScoringProfile.Default;
        }

        duel = new Level78Duel(new UnityLevel78Random(), scoringProfile);
        demonArrowIndex = 0;
        boxArrowIndex = 0;
        farBoardLocalX = board.localPosition.x;
        SetBoardLocalX(farBoardLocalX);
        ApplyAimGaugeVisual(Level78BoardPosition.Far);
        arrowTemplate.gameObject.SetActive(false);
        EnsureBoardCollider();
        CreateScoreBoardTexts();
        ClearScoreBoard();

        levelCtrl.SetCtrlable(true);
        player.SetLevel78ControlLocked(false);
        ResolveAndBindPanel();
        SetAimMode(false);
        CreateDemon();
    }

    private void CreateDemon()
    {
        GameObject demonObject = Instantiate(player.gameObject, player.transform.parent);
        demonObject.name = "Level078_Demon";
        demonObject.transform.localPosition = demonHomePosition;
        demon = demonObject.GetComponent<Level78ActorCtrl>();

        Rigidbody2D demonRigidbody = demonObject.GetComponent<Rigidbody2D>();
        if (demonRigidbody != null)
        {
            demonRigidbody.linearVelocity = Vector2.zero;
            demonRigidbody.angularVelocity = 0f;
            demonRigidbody.gravityScale = 0f;
            demonRigidbody.simulated = true;
        }

        foreach (Collider2D demonCollider in demonObject.GetComponentsInChildren<Collider2D>(true))
            demonCollider.enabled = false;

        if (demon == null)
        {
            this.LogError("[Level78Ctrl] Runtime demon clone is missing Level78ActorCtrl.");
            Destroy(demonObject);
            return;
        }

        demon.SetLevel78ControlLocked(true);
        demon.IsEditor = false;
        RemoveDemonInheritedSkins();
        ShadowController demonShadow = demonObject.GetComponentInChildren<ShadowController>(true);
        if (demonShadow != null)
        {
            demonShadow.gameObject.SetActive(true);
            demonShadow.SetCharacter(demon.transform);
        }

        demon.transform.DOKill();
    }

    /// <summary>
    /// 清理从玩家 Actor 克隆到小恶魔上的 Skin 绑定与部件，只保留本关固定的 box_bad 外观。
    /// </summary>
    private void RemoveDemonInheritedSkins()
    {
        if (demon == null)
            return;

        ActorSkinBinding[] skinBindings = demon.GetComponentsInChildren<ActorSkinBinding>(true);
        for (int i = 0; i < skinBindings.Length; i++)
        {
            if (skinBindings[i] != null)
                skinBindings[i].enabled = false;
        }

        ClearDemonSkinPartMount("back");
        ClearDemonSkinPartMount("face");
        ClearDemonSkinPartMount("body");
        demon.SpineCtrl?.SetSkin(DemonSkinName);
    }

    private void ClearDemonSkinPartMount(string mountName)
    {
        Transform mount = FindDemonSkinPartMount(demon.transform, mountName);
        if (mount == null)
            return;

        for (int i = mount.childCount - 1; i >= 0; i--)
            Destroy(mount.GetChild(i).gameObject);
    }

    private static Transform FindDemonSkinPartMount(Transform root, string mountName)
    {
        if (root == null || string.IsNullOrEmpty(mountName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == mountName &&
                (child.GetComponent<BoneFollowerAutoBinder>() != null || child.GetComponent<BoneFollower>() != null))
            {
                return child;
            }

            Transform result = FindDemonSkinPartMount(child, mountName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void StartDemonOpening()
    {
        if (duel == null || demon == null || !duel.TryStartDemonOpening())
            return;

        int currentSession = sessionVersion;
        StartDemonWalk(
            demonShootingPosition,
            true,
            currentSession,
            () =>
            {
                if (IsCurrentSession(currentSession) && duel.NotifyDemonApproachCompleted())
                    StartDemonShot(currentSession);
            });
    }

    private void StartDemonWalk(
        Vector3 targetLocalPosition,
        bool faceRightOnArrival,
        int currentSession,
        Action onCompleted)
    {
        if (!IsCurrentSession(currentSession) || demon == null)
        {
            this.LogError("[Level78Ctrl] Demon cannot start normal walking without an active actor.");
            return;
        }

        Transform parent = demon.transform.parent;
        demonWalkTargetWorldX = parent != null
            ? parent.TransformPoint(targetLocalPosition).x
            : targetLocalPosition.x;
        demonWalkDirection = Mathf.Sign(demonWalkTargetWorldX - demon.transform.position.x);
        demonWalkSession = currentSession;
        demonFaceRightOnArrival = faceRightOnArrival;
        demonWalkCompleted = onCompleted;
        demonWalkActive = true;
        demonWalkPrepared = false;

        if (Mathf.Approximately(demonWalkDirection, 0f))
            CompleteDemonWalk();
    }

    private void UpdateDemonWalk()
    {
        if (!demonWalkActive || demon == null || !IsCurrentSession(demonWalkSession))
            return;

        if (demon.RB == null)
            return;

        if (!demonWalkPrepared)
        {
            demon.RB.simulated = true;
            demon.RB.gravityScale = 0f;
            demon.RB.linearVelocity = Vector2.zero;
            demon.SetLevel78ControlLocked(false);
            demon.SetMoveInput(demonWalkDirection);
            demonWalkPrepared = true;
        }

        float remaining = (demonWalkTargetWorldX - demon.transform.position.x) * demonWalkDirection;
        if (remaining <= 0f)
        {
            CompleteDemonWalk();
            return;
        }

        float fixedStepDistance = demon.MoveSpeed * Time.fixedDeltaTime;
        float inputMagnitude = fixedStepDistance > 0f
            ? Mathf.Clamp01(remaining / fixedStepDistance)
            : 1f;
        demon.SetMoveInput(demonWalkDirection * inputMagnitude);
        demon.OnUpdate();
    }

    private void CompleteDemonWalk()
    {
        if (!demonWalkActive)
            return;

        Action onCompleted = demonWalkCompleted;
        demonWalkActive = false;
        demonWalkPrepared = false;
        demonWalkCompleted = null;

        if (demon != null)
        {
            demon.SetMoveInput(0f);
            if (demon.RB != null)
                demon.RB.linearVelocity = Vector2.zero;

            demon.SetFaceDir(demonFaceRightOnArrival);
            demon.UpdateAnimation();
            demon.SetLevel78ControlLocked(true);
        }

        onCompleted?.Invoke();
    }

    private void CancelDemonWalk()
    {
        demonWalkActive = false;
        demonWalkPrepared = false;
        demonWalkCompleted = null;
        if (demon == null)
            return;

        demon.SetMoveInput(0f);
        if (demon.RB != null)
            demon.RB.linearVelocity = Vector2.zero;
    }

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
        subscriber.Subscribe<GameReplayEvent>(OnGameReplay);
        subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerMoveStarted);
        subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerMoveStarted);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        subscriber.Subscribe<SwitchGameStateEvent>(OnGameStateChanged);
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        isGamePaused = false;
    }

    private void OnGameStateChanged(SwitchGameStateEvent evt)
    {
        if (evt != null)
            isGamePaused = !evt.NewState.Equals(GamePlayStateName.PLAYING);
    }

    private void OnPlayerMoveStarted(PlayerRightMoveEvent evt)
    {
        StartDemonOpening();
    }

    private void OnPlayerMoveStarted(PlayerLeftMoveEvent evt)
    {
        StartDemonOpening();
    }

    #endregion

    #region Demon Flow

    private void StartDemonShot(int currentSession)
    {
        if (!IsCurrentSession(currentSession) || duel.Phase != Level78Phase.DemonShooting ||
            demonArrowIndex >= ArrowCountPerParticipant || demon == null)
        {
            return;
        }

        int arrowIndex = demonArrowIndex;
        int score = duel.DemonScores[arrowIndex];
        if (!demon.CanPlayArcherySequence)
        {
            this.LogWarning($"[Level78Ctrl] Demon arrow {arrowIndex + 1} animation is still busy.");
            return;
        }

        bool arrowCommitted = false;
        bool actionCompleted = false;
        bool flowAdvanced = false;
        Action tryAdvance = () =>
        {
            if (flowAdvanced || !arrowCommitted || !actionCompleted || !IsCurrentSession(currentSession))
                return;

            flowAdvanced = true;
            ContinueDemonFlowAfterArrow(currentSession);
        };
        bool started = demon.PlayArcherySequence(
            releaseNormalizedTime,
            () => LaunchArrow(
                Level78Participant.Demon,
                arrowIndex,
                score,
                currentSession,
                () =>
                {
                    arrowCommitted = CommitDemonArrow(currentSession);
                    tryAdvance();
                }),
            () =>
            {
                actionCompleted = true;
                tryAdvance();
            });

        if (!started)
            this.LogWarning($"[Level78Ctrl] Demon arrow {arrowIndex + 1} animation could not start.");
    }

    private bool CommitDemonArrow(int currentSession)
    {
        if (!IsCurrentSession(currentSession) || !duel.TryCompleteDemonShot(out Level78ShotResult result))
            return false;

        WriteScore(result);
        demonArrowIndex++;
        return true;
    }

    private void ContinueDemonFlowAfterArrow(int currentSession)
    {
        if (!IsCurrentSession(currentSession))
            return;

        if (duel.Phase == Level78Phase.DemonCleanupAndReturn)
        {
            DOVirtual.DelayedCall(demonResultHoldDuration, () => StartDemonCleanup(currentSession))
                .SetLink(gameObject);
            return;
        }

        DOVirtual.DelayedCall(demonArrowInterval, () => StartDemonShot(currentSession))
            .SetLink(gameObject);
    }

    private void StartDemonCleanup(int currentSession)
    {
        if (!IsCurrentSession(currentSession) || duel.Phase != Level78Phase.DemonCleanupAndReturn)
            return;

        Sequence fadeSequence = DOTween.Sequence().SetLink(gameObject);
        foreach (SpriteRenderer arrowRenderer in demonArrowRenderers)
        {
            if (arrowRenderer != null)
                fadeSequence.Join(arrowRenderer.DOFade(0f, demonArrowFadeDuration));
        }

        fadeSequence.OnComplete(() =>
        {
            if (!IsCurrentSession(currentSession))
                return;

            duel.NotifyDemonArrowFadeCompleted();
            TryEnterBoardAdjustment();
        });

        StartDemonWalk(
            demonHomePosition,
            true,
            currentSession,
            () =>
            {
                if (!IsCurrentSession(currentSession))
                    return;

                duel.NotifyDemonReturnCompleted(IsPlayerAtShootingPoint());
                TryEnterBoardAdjustment();
            });
    }

    private void TryEnterBoardAdjustment()
    {
        if (duel.Phase != Level78Phase.BoardAdjustment)
            return;

        SetAimMode(false);
        levelCtrl.SetCtrlable(true);
        player.SetLevel78ControlLocked(false);
        if (!turnAnnouncementShown)
        {
            turnAnnouncementShown = true;
            ShowDemonSpeech(turnAnnouncementKey);
        }
    }

    #endregion

    #region Board and Positioning

    private void EnsureBoardCollider()
    {
        boardCollider = board.GetComponent<BoxCollider2D>();
        if (boardCollider == null)
            boardCollider = board.gameObject.AddComponent<BoxCollider2D>();

        SpriteRenderer boardRenderer = board.GetComponent<SpriteRenderer>();
        if (boardRenderer != null && boardRenderer.sprite != null)
            boardCollider.size = boardRenderer.sprite.bounds.size;

        boardCollider.isTrigger = true;
    }

    private void UpdateBoardDragInput()
    {
        if (duel.Phase != Level78Phase.BoardAdjustment || boardSnapBusy || Camera.main == null)
            return;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(touch.position);
            HandleDragPhase(touch.phase, worldPoint.x, worldPoint);
            return;
        }

        Vector3 mouseWorldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (Input.GetMouseButtonDown(0))
            TryBeginBoardDrag(mouseWorldPoint.x, mouseWorldPoint);
        else if (Input.GetMouseButton(0) && dragActive)
            UpdateBoardDrag(mouseWorldPoint.x);
        else if (Input.GetMouseButtonUp(0) && dragActive)
            EndBoardDrag();
    }

    private void HandleDragPhase(TouchPhase phase, float worldX, Vector2 worldPoint)
    {
        switch (phase)
        {
            case TouchPhase.Began:
                TryBeginBoardDrag(worldX, worldPoint);
                break;
            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                if (dragActive)
                    UpdateBoardDrag(worldX);
                break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                if (dragActive)
                    EndBoardDrag();
                break;
        }
    }

    private void TryBeginBoardDrag(float worldX, Vector2 worldPoint)
    {
        if (duel.BoardPosition == Level78BoardPosition.NearFullScore ||
            boardCollider == null ||
            !boardCollider.OverlapPoint(worldPoint))
            return;

        dragActive = true;
        dragOrigin = duel.BoardPosition;
        dragOffsetX = board.position.x - worldX;
    }

    private void UpdateBoardDrag(float worldX)
    {
        float farWorldX = GetBoardWorldX(farBoardLocalX);
        float nearWorldX = GetBoardWorldX(nearBoardLocalX);
        Vector3 position = board.position;
        position.x = Mathf.Clamp(worldX + dragOffsetX, Mathf.Min(farWorldX, nearWorldX), Mathf.Max(farWorldX, nearWorldX));
        board.position = position;
    }

    private void EndBoardDrag()
    {
        dragActive = false;
        float farWorldX = GetBoardWorldX(farBoardLocalX);
        float nearWorldX = GetBoardWorldX(nearBoardLocalX);
        Level78BoardPosition destination = Level78BoardSnap.Resolve(
            board.position.x,
            farWorldX,
            nearWorldX,
            dragOrigin);
        float destinationX = destination == Level78BoardPosition.Far ? farWorldX : nearWorldX;
        int currentSession = sessionVersion;

        boardSnapBusy = true;
        boardSnapTween?.Kill();
        boardSnapTween = board.DOMoveX(destinationX, boardSnapDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (!IsCurrentSession(currentSession) || duel.Phase != Level78Phase.BoardAdjustment)
                    return;

                Vector3 position = board.position;
                position.x = destinationX;
                board.position = position;
                if (duel.TrySetBoardPosition(destination))
                    ApplyAimGaugeVisual(destination);

                boardSnapBusy = false;
            });
    }

    private void UpdatePlayerBoundaryAndQualification()
    {
        if (player == null || duel.Phase == Level78Phase.BoxShooting || duel.Phase == Level78Phase.Settled)
            return;

        float shootingPointX = GetShootingPointX();
        Vector3 playerPosition = player.transform.position;
        if (playerPosition.x > shootingPointX)
        {
            playerPosition.x = shootingPointX;
            player.transform.position = playerPosition;
            player.SetMoveInput(0f);
        }

        if (duel.Phase != Level78Phase.BoardAdjustment || dragActive || boardSnapBusy)
            return;

        if (player.transform.position.x < shootingPointX - shootingPointTolerance)
        {
            duel.NotifyBoxExitedShootingPoint();
            return;
        }

        if (player.XInput < 0f)
            return;

        playerPosition = player.transform.position;
        playerPosition.x = shootingPointX;
        player.transform.position = playerPosition;
        player.SetMoveInput(0f);
        if (duel.TryEnterBoxShootingPoint())
            EnterBoxShooting();
    }

    private void EnterBoxShooting()
    {
        dragActive = false;
        boardSnapBusy = false;
        boardSnapTween?.Kill();
        boardSnapTween = null;
        levelCtrl.SetCtrlable(false);
        player.SetLevel78ControlLocked(true);
        if (!player.ShowArcheryReadyPose())
            this.LogWarning("[Level78Ctrl] Box ready pose could not start at the shooting point.");

        PositionAimGaugeAtBoxFront();
        ApplyAimGaugeVisual(duel.BoardPosition);
        SetAimMode(true);
        StartPointerLoop();
    }

    private void PositionAimGaugeAtBoxFront()
    {
        if (aimGaugeRoot == null || player == null || target == null)
            return;

        float targetDirection = target.position.x >= player.transform.position.x ? 1f : -1f;
        Vector3 gaugePosition = aimGaugeRoot.transform.position;
        gaugePosition.x = player.transform.position.x + targetDirection * Mathf.Abs(aimGaugeForwardOffset);
        gaugePosition.y = player.transform.position.y + aimGaugeVerticalOffset;
        aimGaugeRoot.transform.position = gaugePosition;
    }

    private bool IsPlayerAtShootingPoint()
    {
        return player != null && Mathf.Abs(player.transform.position.x - GetShootingPointX()) <= shootingPointTolerance;
    }

    private float GetShootingPointX()
    {
        return board.position.x - Mathf.Abs(shootingPointLeftOffset);
    }

    private float GetBoardWorldX(float localX)
    {
        if (board.parent == null)
            return localX;

        Vector3 localPosition = board.localPosition;
        localPosition.x = localX;
        return board.parent.TransformPoint(localPosition).x;
    }

    private void SetBoardLocalX(float localX)
    {
        Vector3 position = board.localPosition;
        position.x = localX;
        board.localPosition = position;
    }

    #endregion

    #region Box Flow and Aim

    private void ResolveAndBindPanel()
    {
        if (gamePlayPanel != null)
            return;

        gamePlayPanel = PlayCtrl.Instance?.CurrentGamePlayPanel;
        if (gamePlayPanel == null)
            return;

        jumpButton = ResolvePanelButton(gamePlayPanel, JumpButtonPath);
        aimButton = ResolvePanelButton(gamePlayPanel, AimButtonPath);
        if (aimButton != null)
        {
            aimButton.OnClickDown -= OnAimPressed;
            aimButton.OnClickDown += OnAimPressed;
        }

        SetPanelAimMode(duel != null && duel.Phase == Level78Phase.BoxShooting);
    }

    private static CustomButton ResolvePanelButton(GamePlayPanel panel, string path)
    {
        Transform buttonTransform = panel.transform.Find(path);
        return buttonTransform != null ? buttonTransform.GetComponent<CustomButton>() : null;
    }

    private void SetPanelAimMode(bool aimMode)
    {
        if (jumpButton != null)
            jumpButton.gameObject.SetActive(!aimMode);

        if (aimButton != null)
        {
            aimButton.gameObject.SetActive(aimMode);
            aimButton.Interactable = aimMode;
            if (aimMode)
            {
                // GamePlayPanel.PlayShowPanelAnim 的 HideAllBtns 会把激活按钮缩放归零，
                // 而 aim_btn 在收集恢复目标前已被本类停用，不会被 SequentialScaleAnim 恢复；
                // 激活时恢复 prefab 设计缩放，避免按钮以 0 缩放隐形。
                aimButton.transform.localScale = Vector3.one;
            }
        }
    }

    private void UnbindPanel()
    {
        if (aimButton != null)
            aimButton.OnClickDown -= OnAimPressed;

        jumpButton = null;
        aimButton = null;
        gamePlayPanel = null;
    }

    private void SetAimMode(bool aimMode)
    {
        ResolveAndBindPanel();
        SetPanelAimMode(aimMode);
        aimGaugeRoot?.SetActive(aimMode);
        if (!aimMode)
            StopPointerLoop();
    }

    private void ApplyAimGaugeVisual(Level78BoardPosition boardPosition)
    {
        if (aimGaugeColorRange == null || nearAimGaugeColorRange == null)
            return;

        bool showNearGreenRange = boardPosition == Level78BoardPosition.NearFullScore;
        aimGaugeColorRange.enabled = !showNearGreenRange;
        nearAimGaugeColorRange.enabled = showNearGreenRange;
        nearAimGaugeColorRange.fillAmount = showNearGreenRange ? 1f : 0f;
    }

    private void StartPointerLoop()
    {
        StopPointerLoop();
        Vector3 rotation = aimPointer.localEulerAngles;
        rotation.z = pointerAngleRange.x;
        aimPointer.localEulerAngles = rotation;
        pointerTween = aimPointer.DOLocalRotate(
                new Vector3(rotation.x, rotation.y, pointerAngleRange.y),
                pointerOneWayDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    private void StopPointerLoop()
    {
        pointerTween?.Kill();
        pointerTween = null;
    }

    private void OnAimPressed(CustomButton button)
    {
        if (isGamePaused || duel == null || player == null || !player.CanPlayArcherySequence)
            return;

        float pointerPosition = GetNormalizedPointerPosition();
        if (!duel.TryStartBoxShot(pointerPosition, out int score))
            return;

        int currentSession = sessionVersion;
        int arrowIndex = boxArrowIndex;
        StopPointerLoop();
        VibrationManager.Instance.Vibrate(VibrationIntensity.Light);

        bool arrowCommitted = false;
        bool actionCompleted = false;
        bool nextShotOpened = false;
        Action tryOpenNextShot = () =>
        {
            if (nextShotOpened || !arrowCommitted || !actionCompleted || !IsCurrentSession(currentSession) ||
                duel.Phase != Level78Phase.BoxShooting)
            {
                return;
            }

            nextShotOpened = true;
            StartPointerLoop();
        };
        bool started = player.PlayArcheryShotFromReadyPose(
            releaseNormalizedTime,
            () => LaunchArrow(
                Level78Participant.Box,
                arrowIndex,
                score,
                currentSession,
                () =>
                {
                    arrowCommitted = CommitBoxArrow(currentSession);
                    tryOpenNextShot();
                }),
            () =>
            {
                actionCompleted = true;
                tryOpenNextShot();
            });

        if (!started)
            this.LogError($"[Level78Ctrl] Box arrow {arrowIndex + 1} animation could not start.");
    }

    private bool CommitBoxArrow(int currentSession)
    {
        if (!IsCurrentSession(currentSession) || !duel.TryCompleteBoxShot(out Level78ShotResult result))
            return false;

        WriteScore(result);
        boxArrowIndex++;
        if (duel.Phase == Level78Phase.Settled)
        {
            if (targetImpactTween != null && targetImpactTween.IsActive())
                settlementPendingAfterTargetImpact = true;
            else
                CompleteSettlement();
        }

        return true;
    }

    private float GetNormalizedPointerPosition()
    {
        float angle = NormalizeAngle180(aimPointer.localEulerAngles.z);
        return Mathf.InverseLerp(pointerAngleRange.x, pointerAngleRange.y, angle);
    }

    #endregion

    #region Arrow Presentation

    /// <summary>
    /// 仅 Box 玩家的箭启用收集星触发：参考 Level 087 football 的做法，给箭根节点挂上 Player 标签、
    /// Kinematic Rigidbody2D 与箭尖 trigger 圆，使箭穿过 Postcard_Star 触发器时走既有收集链路（存档、发奖、
    /// 飞星事件、特效、音效全部由 Postcard_Star 自身处理）；小恶魔的箭不调用本方法，只能视觉穿过星星。
    /// </summary>
    private static void EnableArrowStarCollection(GameObject arrowRootObject)
    {
        arrowRootObject.tag = "Player";

        var body = arrowRootObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var tipCollider = arrowRootObject.AddComponent<CircleCollider2D>();
        tipCollider.isTrigger = true;
        tipCollider.radius = ArrowStarCollectionTipRadius;
        tipCollider.excludeLayers = LayerMask.GetMask("Actor");
    }

    private void LaunchArrow(
        Level78Participant participant,
        int arrowIndex,
        int score,
        int currentSession,
        Action onArrive)
    {
        if (!IsCurrentSession(currentSession))
            return;

        Level78ActorCtrl shooter = participant == Level78Participant.Demon ? demon : player;
        if (shooter == null)
            return;

        // 箭离弦瞬间播放箭矢飞行音效，双方规则一致，火后即忘、与飞行时长脱钩。
        PlayArrowWhooshSound();

        var arrowRootObject = new GameObject($"Level078_{participant}_Arrow_{arrowIndex + 1}");
        Transform arrowRoot = arrowRootObject.transform;
        arrowRoot.SetParent(transform, false);
        GameObject arrowObject = Instantiate(arrowTemplate.gameObject, arrowRoot);
        arrowObject.name = "Visual";
        arrowObject.SetActive(true);
        Level78ArrowPlacement placement = Level78ArrowLayout.Resolve(score, arrowIndex);
        spawnedArrows.Add(arrowRootObject);

        if (participant == Level78Participant.Box)
            EnableArrowStarCollection(arrowRootObject);

        SpriteRenderer arrowRenderer = arrowObject.GetComponent<SpriteRenderer>();
        if (participant == Level78Participant.Demon && arrowRenderer != null)
            demonArrowRenderers.Add(arrowRenderer);

        // 飞行中的箭（含脱靶箭）临时抬高到全部留靶箭之上；命中靶面时一次性落位到最终插入点偏移，
        // 脱靶落地时落回模板基线。拖尾在抬高之后创建，自动保持比本箭低一级。
        int arrowBaseSortingOrder = 0;
        if (arrowRenderer != null)
        {
            arrowBaseSortingOrder = arrowRenderer.sortingOrder;
            arrowRenderer.sortingOrder += Level78ArrowDepthOrder.MaxSortingOffset + 1;
        }

        Vector3 releasePoint = shooter.transform.position + new Vector3(0.75f, 0.8f, -0.1f);
        Vector3 hitPoint = target.position + new Vector3(placement.OffsetX, placement.OffsetY, -0.1f);
        Level78ArrowTrajectory trajectory = Level78ArrowTrajectory.Create(
            new Level78ArrowTrajectoryPoint(releasePoint.x, releasePoint.y),
            new Level78ArrowTrajectoryPoint(hitPoint.x, hitPoint.y));
        Vector3 localTip = arrowRenderer?.sprite != null
            ? Vector3.right * arrowRenderer.sprite.bounds.extents.x
            : Vector3.zero;
        arrowObject.transform.localPosition = -localTip;
        arrowObject.transform.localRotation = Quaternion.identity;
        arrowObject.transform.localScale = arrowTemplate.transform.localScale;

        SpriteMask hitOcclusionMask = CreateHitArrowOcclusionMask(arrowRoot, arrowRenderer);
        ApplyArrowTrajectory(arrowRoot, trajectory, releasePoint.z, 0f);
        TrailRenderer arrowTrail = CreateArrowTrail(arrowObject.transform, arrowRenderer);
        float flightDuration = Level78ArrowFlightTiming.CalculateDuration(
            releasePoint.x,
            hitPoint.x,
            arrowHorizontalSpeed);

        DOVirtual.Float(
                0f,
                1f,
                flightDuration,
                progress => ApplyArrowTrajectory(
                    arrowRoot,
                    trajectory,
                    releasePoint.z,
                    progress))
            .SetEase(Ease.Linear)
            .SetTarget(arrowRoot)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (!IsCurrentSession(currentSession))
                    return;

                if (arrowTrail != null)
                    arrowTrail.emitting = false;

                ApplyHitArrowDepthOrder(arrowRenderer, placement, arrowBaseSortingOrder);
                if (!placement.IsMiss)
                {
                    AudioManager.PlaySound(TargetHitSfxTag, ArrowActionSfxVolume);
                    EnableHitArrowOcclusion(arrowRenderer, hitOcclusionMask);
                    float horizontalDirection = Mathf.Sign(hitPoint.x - releasePoint.x);
                    PlayTargetImpactFeedback(arrowRoot, horizontalDirection, currentSession);
                }

                onArrive?.Invoke();
            });
    }

    /// <summary>
    /// 播放箭矢破空音。arrowWhoosh.ogg 自带约 0.54s 前导静音（实测发声段 0.54–0.74s），
    /// 共享 Sounds Good 与 AudioManager 无播放起点跳转能力且不属于本关修改范围，
    /// 因此由本关自带 AudioSource 从发声点起播；音量 1.0，避免被同帧命中音与计分音掩盖。
    /// 规则与共享音效一致：双方每箭各一次、火后即忘、与飞行时长脱钩、受音效开关控制。
    /// </summary>
    private void PlayArrowWhooshSound()
    {
        if (!DB.GameSetting.SfxOn || arrowWhooshClip == null)
            return;

        if (arrowWhooshSource == null)
        {
            var sourceObject = new GameObject("Level078_ArrowWhooshSource");
            sourceObject.transform.SetParent(transform, false);
            arrowWhooshSource = sourceObject.AddComponent<AudioSource>();
            arrowWhooshSource.playOnAwake = false;
            arrowWhooshSource.spatialBlend = 0f;
        }

        arrowWhooshSource.clip = arrowWhooshClip;
        arrowWhooshSource.volume = ArrowActionSfxVolume;
        arrowWhooshSource.time = Mathf.Clamp(ArrowWhooshSfxStartSeconds, 0f, arrowWhooshClip.length);
        arrowWhooshSource.Play();
    }

    private TrailRenderer CreateArrowTrail(Transform arrowTransform, SpriteRenderer arrowRenderer)
    {
        if (arrowTrailMaterial == null || arrowTrailTime <= 0f || arrowTrailWidth <= 0f)
            return null;

        var trailObject = new GameObject("Level078_ArrowFlightTrail");
        trailObject.transform.SetParent(arrowTransform, false);
        float arrowHalfLength = arrowRenderer?.sprite != null
            ? arrowRenderer.sprite.bounds.extents.x
            : 0f;
        trailObject.transform.localPosition = Vector3.left * arrowHalfLength;

        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = arrowTrailMaterial;
        trail.time = arrowTrailTime;
        trail.widthMultiplier = arrowTrailWidth;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.colorGradient = CreateArrowTrailGradient();
        trail.minVertexDistance = 0.04f;
        trail.numCapVertices = 2;
        trail.numCornerVertices = 2;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.autodestruct = false;
        trail.sortingLayerID = arrowRenderer != null ? arrowRenderer.sortingLayerID : 0;
        trail.sortingOrder = arrowRenderer != null ? arrowRenderer.sortingOrder - 1 : 0;
        trail.Clear();
        trail.emitting = true;
        return trail;
    }

    private static Gradient CreateArrowTrailGradient()
    {
        var gradient = new Gradient();
        var warmWhite = new Color(1f, 0.94f, 0.82f);
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(warmWhite, 0f),
                new GradientColorKey(warmWhite, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.65f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    /// <summary>
    /// 箭落地时一次性落位：命中箭按最终插入点偏移确定遮挡顺序，脱靶箭落回模板基线。
    /// </summary>
    private static void ApplyHitArrowDepthOrder(
        SpriteRenderer arrowRenderer,
        Level78ArrowPlacement placement,
        int baseSortingOrder)
    {
        if (arrowRenderer == null)
            return;

        arrowRenderer.sortingOrder = placement.IsMiss
            ? baseSortingOrder
            : baseSortingOrder + Level78ArrowDepthOrder.ResolveSortingOffset(placement);
    }

    private SpriteMask CreateHitArrowOcclusionMask(Transform arrowRoot, SpriteRenderer arrowRenderer)
    {
        if (arrowRoot == null || arrowRenderer == null || hitArrowOcclusionMaskSprite == null)
            return null;

        var maskObject = new GameObject("HitOcclusionMask");
        Transform maskTransform = maskObject.transform;
        maskTransform.SetParent(arrowRoot, false);
        float maskSpriteDiameter = Mathf.Max(
            hitArrowOcclusionMaskSprite.bounds.size.x,
            hitArrowOcclusionMaskSprite.bounds.size.y);
        float maskScale = maskSpriteDiameter > 0f
            ? Mathf.Max(0f, hitArrowOcclusionDepth) * 2f / maskSpriteDiameter
            : 0f;
        maskTransform.localScale = Vector3.one * maskScale;

        SpriteMask spriteMask = maskObject.AddComponent<SpriteMask>();
        spriteMask.sprite = hitArrowOcclusionMaskSprite;
        spriteMask.alphaCutoff = 0.1f;
        spriteMask.enabled = false;
        return spriteMask;
    }

    private static void EnableHitArrowOcclusion(SpriteRenderer arrowRenderer, SpriteMask spriteMask)
    {
        if (arrowRenderer == null || spriteMask == null)
            return;

        spriteMask.isCustomRangeActive = true;
        spriteMask.frontSortingLayerID = arrowRenderer.sortingLayerID;
        spriteMask.backSortingLayerID = arrowRenderer.sortingLayerID;
        spriteMask.frontSortingOrder = arrowRenderer.sortingOrder;
        spriteMask.backSortingOrder = arrowRenderer.sortingOrder;
        arrowRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        spriteMask.enabled = true;
    }

    /// <summary>
    /// 让计分靶与全部留靶箭沿箭的水平行进方向后退、反向回弹并精确归位。
    /// 脱靶箭不得进入此流程；结算、重开和退出会强制恢复基准位置。
    /// </summary>
    private void PlayTargetImpactFeedback(Transform hitArrowRoot, float horizontalDirection, int currentSession)
    {
        if (target == null || hitArrowRoot == null || !targetRestPositionCaptured)
            return;

        ResetTargetImpactFeedback();
        hitArrowRoot.SetParent(target, true);
        activeHitArrowRoot = hitArrowRoot;
        activeHitArrowRestLocalRotation = hitArrowRoot.localRotation;

        float direction = Mathf.Approximately(horizontalDirection, 0f) ? 1f : Mathf.Sign(horizontalDirection);
        Vector3 recoilPosition = targetRestLocalPosition;
        recoilPosition.x += direction * Mathf.Abs(targetRecoilDistance);
        Vector3 reboundPosition = targetRestLocalPosition;
        reboundPosition.x -= direction * Mathf.Abs(targetReboundDistance);

        float vibrationSegmentDuration = Mathf.Max(0f, hitArrowVibrationDuration) * 0.25f;
        Vector3 restEulerAngles = activeHitArrowRestLocalRotation.eulerAngles;
        Sequence arrowVibration = DOTween.Sequence()
            .Append(hitArrowRoot.DOLocalRotate(
                new Vector3(0f, 0f, restEulerAngles.z + direction * Mathf.Abs(hitArrowSwingAngle)),
                vibrationSegmentDuration).SetEase(Ease.OutSine))
            .Append(hitArrowRoot.DOLocalRotate(
                new Vector3(0f, 0f, restEulerAngles.z - direction * Mathf.Abs(hitArrowSwingAngle) * 0.5f),
                vibrationSegmentDuration).SetEase(Ease.InOutSine))
            .Append(hitArrowRoot.DOLocalRotate(
                new Vector3(0f, 0f, restEulerAngles.z + direction * Mathf.Abs(hitArrowSwingAngle) * 0.2f),
                vibrationSegmentDuration).SetEase(Ease.InOutSine))
            .Append(hitArrowRoot.DOLocalRotate(
                restEulerAngles,
                vibrationSegmentDuration).SetEase(Ease.OutSine));

        Sequence targetMovement = DOTween.Sequence()
            .Append(target.DOLocalMoveX(recoilPosition.x, Mathf.Max(0f, targetRecoilDuration)).SetEase(Ease.OutQuad))
            .Append(target.DOLocalMoveX(reboundPosition.x, Mathf.Max(0f, targetReboundDuration)).SetEase(Ease.InOutSine))
            .Append(target.DOLocalMoveX(targetRestLocalPosition.x, Mathf.Max(0f, targetRecoveryDuration)).SetEase(Ease.OutSine));

        targetImpactTween = DOTween.Sequence()
            .Append(targetMovement)
            .Join(arrowVibration)
            .SetTarget(target)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                target.localPosition = targetRestLocalPosition;
                if (activeHitArrowRoot == hitArrowRoot)
                {
                    hitArrowRoot.localRotation = activeHitArrowRestLocalRotation;
                    activeHitArrowRoot = null;
                }

                targetImpactTween = null;
                if (!settlementPendingAfterTargetImpact || !IsCurrentSession(currentSession) ||
                    duel.Phase != Level78Phase.Settled)
                {
                    return;
                }

                settlementPendingAfterTargetImpact = false;
                CompleteSettlement();
            });
    }

    private void ResetTargetImpactFeedback()
    {
        targetImpactTween?.Kill();
        targetImpactTween = null;
        if (activeHitArrowRoot != null)
            activeHitArrowRoot.localRotation = activeHitArrowRestLocalRotation;
        activeHitArrowRoot = null;
        if (target != null && targetRestPositionCaptured)
            target.localPosition = targetRestLocalPosition;
    }

    private static void ApplyArrowTrajectory(
        Transform arrowRoot,
        Level78ArrowTrajectory trajectory,
        float worldZ,
        float progress)
    {
        Level78ArrowTrajectoryPoint tipPoint = trajectory.EvaluatePosition(progress);
        float tangentAngle = trajectory.EvaluateTangentAngleDegrees(progress);
        arrowRoot.rotation = Quaternion.Euler(0f, 0f, tangentAngle);
        arrowRoot.position = new Vector3(tipPoint.X, tipPoint.Y, worldZ);
    }

    #endregion

    #region Score Board and Settlement

    private void CreateScoreBoardTexts()
    {
        if (scoreTexts[0, 0] != null)
            return;

        for (int participantIndex = 0; participantIndex < 2; participantIndex++)
        {
            for (int cellIndex = 0; cellIndex < ScoreCellCountPerParticipant; cellIndex++)
                scoreTexts[participantIndex, cellIndex] = CreateScoreText(participantIndex, cellIndex);
        }
    }

    private TMP_Text CreateScoreText(int participantIndex, int cellIndex)
    {
        string objectName = participantIndex == 0
            ? $"BoxScore_{cellIndex + 1}"
            : $"DemonScore_{cellIndex + 1}";
        var scoreObject = new GameObject(objectName, typeof(RectTransform));
        var scoreRect = (RectTransform)scoreObject.transform;
        scoreRect.SetParent(scoreBoard, false);
        Vector2 cellPosition = ScoreCellLocalPositions[participantIndex, cellIndex];
        scoreRect.localPosition = new Vector3(cellPosition.x, cellPosition.y, -0.05f);
        scoreRect.sizeDelta = new Vector2(7f, 6.8f);
        scoreRect.localScale = Vector3.one * ScoreTextLocalScale;

        TextMeshPro text = scoreObject.AddComponent<TextMeshPro>();
        text.font = scoreFont;
        text.fontSharedMaterial = scoreFont.material;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = ScoreFontSize;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.text = string.Empty;
        ConfigureScoreTextStyle(text, participantIndex, cellIndex);

        MeshRenderer textRenderer = scoreObject.GetComponent<MeshRenderer>();
        SpriteRenderer scoreRenderer = scoreBoard.GetComponent<SpriteRenderer>();
        if (scoreRenderer != null)
        {
            textRenderer.sortingLayerID = scoreRenderer.sortingLayerID;
            textRenderer.sortingOrder = scoreRenderer.sortingOrder + 1;
        }

        return text;
    }

    private static void ConfigureScoreTextStyle(TMP_Text text, int participantIndex, int cellIndex)
    {
        if (cellIndex != TotalScoreCellIndex)
            return;

        text.color = participantIndex == 0 ? BoxTotalScoreColor : DemonTotalScoreColor;
        text.outlineColor = TotalScoreOutlineColor;
        text.outlineWidth = TotalScoreOutlineWidth;

        // TMP 默认把描边居中覆盖字芯；同值扩张 Face，使 4px 白边完整落在 76px 字形外侧。
        text.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, TotalScoreOutlineWidth);
        text.UpdateMeshPadding();
    }

    private void ClearScoreBoard()
    {
        StopScoreFeedbacks(false);
        for (int participantIndex = 0; participantIndex < 2; participantIndex++)
        {
            for (int cellIndex = 0; cellIndex < ScoreCellCountPerParticipant; cellIndex++)
            {
                TMP_Text text = scoreTexts[participantIndex, cellIndex];
                // 总分格初始保持空白，待三格单次得分记完后单次弹出直接揭晓。
                string value = string.Empty;
                committedScoreTexts[participantIndex, cellIndex] = value;
                text.text = value;
                text.transform.localScale = Vector3.one * ScoreTextLocalScale;
            }
        }
    }

    private void WriteScore(Level78ShotResult result)
    {
        int participantIndex = result.Participant == Level78Participant.Box ? 0 : 1;
        PlaySingleScoreFeedback(participantIndex, result.ArrowIndex, result.Score);
        if (result.ArrowIndex < ArrowCountPerParticipant - 1)
            return;

        // 第三箭提交瞬间写入最终总分，任何打断都能立即收口到正确数字；
        // 揭晓序列在第三格单次得分弹出完成后才开始（见 PlaySingleScoreFeedback）。
        committedScoreTexts[participantIndex, TotalScoreCellIndex] = result.Total.ToString();
    }

    private void PlaySingleScoreFeedback(int participantIndex, int cellIndex, int score)
    {
        StopScoreFeedback(participantIndex, cellIndex, true);

        AudioManager.PlaySound(SingleScoreSfxTag);

        TMP_Text text = scoreTexts[participantIndex, cellIndex];
        string finalValue = score.ToString();
        committedScoreTexts[participantIndex, cellIndex] = finalValue;
        text.text = finalValue;

        Transform textTransform = text.transform;
        textTransform.localScale = Vector3.one * (ScoreTextLocalScale * SingleScorePopStartScaleMultiplier);
        Tween tween = textTransform
            .DOScale(Vector3.one * ScoreTextLocalScale, ScoreFeedbackDuration)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject);
        scoreFeedbackTweens[participantIndex, cellIndex] = tween;
        tween.OnComplete(() =>
        {
            CompleteScoreFeedback(participantIndex, cellIndex, tween);
            // 第三格弹出完成后开始总分揭晓；tween 被打断时 OnComplete 不触发，揭晓不会误启动。
            if (cellIndex == ArrowCountPerParticipant - 1)
                PlayTotalScoreReveal(participantIndex);
        });
    }

    /// <summary>
    /// 第三格单次得分弹出完成后触发：总分格以与单次得分相同的缩放弹出直接亮出最终总分
    /// （三箭累计值，含 0 分），不再逐箭累加翻牌；最终总分在第三箭提交时已写入
    /// committedScoreTexts，任何打断都能立即收口到正确数字。
    /// </summary>
    private void PlayTotalScoreReveal(int participantIndex)
    {
        StopScoreFeedback(participantIndex, TotalScoreCellIndex, true);

        AudioManager.PlaySound(TotalScoreRevealSfxTag);

        TMP_Text text = scoreTexts[participantIndex, TotalScoreCellIndex];
        if (text == null)
            return;

        text.text = committedScoreTexts[participantIndex, TotalScoreCellIndex];

        Transform textTransform = text.transform;
        textTransform.localScale = Vector3.one * (ScoreTextLocalScale * SingleScorePopStartScaleMultiplier);
        Tween tween = textTransform
            .DOScale(Vector3.one * ScoreTextLocalScale, ScoreFeedbackDuration)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject);
        scoreFeedbackTweens[participantIndex, TotalScoreCellIndex] = tween;
        tween.OnComplete(() => CompleteScoreFeedback(participantIndex, TotalScoreCellIndex, tween));
    }

    private void CompleteScoreFeedback(int participantIndex, int cellIndex, Tween completedTween)
    {
        if (scoreFeedbackTweens[participantIndex, cellIndex] != completedTween)
            return;

        scoreFeedbackTweens[participantIndex, cellIndex] = null;
        TMP_Text text = scoreTexts[participantIndex, cellIndex];
        if (text == null)
            return;

        text.text = committedScoreTexts[participantIndex, cellIndex];
        text.transform.localScale = Vector3.one * ScoreTextLocalScale;
    }

    private void StopScoreFeedbacks(bool restoreCommittedValues)
    {
        for (int participantIndex = 0; participantIndex < 2; participantIndex++)
        {
            for (int cellIndex = 0; cellIndex < ScoreCellCountPerParticipant; cellIndex++)
                StopScoreFeedback(participantIndex, cellIndex, restoreCommittedValues);
        }
    }

    private void StopScoreFeedback(int participantIndex, int cellIndex, bool restoreCommittedValue)
    {
        scoreFeedbackTweens[participantIndex, cellIndex]?.Kill();
        scoreFeedbackTweens[participantIndex, cellIndex] = null;

        TMP_Text text = scoreTexts[participantIndex, cellIndex];
        if (text == null)
            return;

        if (restoreCommittedValue && committedScoreTexts[participantIndex, cellIndex] != null)
            text.text = committedScoreTexts[participantIndex, cellIndex];

        text.transform.localScale = Vector3.one * ScoreTextLocalScale;
    }

    private void CompleteSettlement()
    {
        if (settlementAnnouncementStarted)
            return;

        settlementAnnouncementStarted = true;
        StopScoreFeedbacks(true);
        settlementPendingAfterTargetImpact = false;
        StopPointerLoop();
        SetAimMode(false);
        levelCtrl.SetCtrlable(false);
        if (duel.Result == Level78DuelResult.Failure)
        {
            if (!player.ShowFailureSettlementIdlePose())
                this.LogWarning("[Level78Ctrl] Box failure settlement idle pose could not start.");
        }
        else
        {
            if (!player.ShowVictorySettlementCelebratePose())
                this.LogWarning("[Level78Ctrl] Box victory settlement celebrate pose could not start.");
        }

        player.SetLevel78ControlLocked(true);

        string announcement = duel.Result == Level78DuelResult.Victory
            ? victoryAnnouncementKey
            : failureAnnouncementKey;
        ShowDemonSpeech(announcement);

        int currentSession = sessionVersion;
        settlementDelayTween?.Kill();
        settlementDelayTween = DOVirtual.DelayedCall(
                Mathf.Max(0f, demonSpeechDuration),
                () => FinalizeSettlement(currentSession))
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void FinalizeSettlement(int currentSession)
    {
        settlementDelayTween = null;
        if (!IsCurrentSession(currentSession) || duel.Phase != Level78Phase.Settled)
            return;

        CloseDemonSpeech();

        if (duel.Result == Level78DuelResult.Victory)
            levelCtrl.SuccessStrightly();
        else
            this.DispatchEvent(Witness<GameResetEvent>._, GameResetType.Failed);
    }

    private void ShowDemonSpeech(string content)
    {
        CloseDemonSpeech();
        if (demon == null)
        {
            this.LogWarning("[Level78Ctrl] Demon speech skipped because the runtime demon is missing.");
            return;
        }

        HintBubble bubblePrefab = Resources.Load<HintBubble>(HintBubbleResourceName);
        if (bubblePrefab == null)
        {
            this.LogWarning("[Level78Ctrl] Demon speech skipped because HintBubble could not be loaded.");
            return;
        }

        demonSpeechBubble = Instantiate(bubblePrefab, transform);

        TextMeshPro speechText = demonSpeechBubble.GetComponentInChildren<TextMeshPro>(true);
        if (speechText != null)
        {
            speechText.fontSize = DemonSpeechFontSize;
            speechText.fontSizeMax = DemonSpeechFontSize;
        }

        demonSpeechBubble.Show(content, demon.transform, 0f);

        HintBubble shownBubble = demonSpeechBubble;
        speechCloseTween = DOVirtual.DelayedCall(
                Mathf.Max(0f, demonSpeechDuration),
                () =>
                {
                    speechCloseTween = null;
                    if (demonSpeechBubble != shownBubble)
                        return;

                    shownBubble.CloseStraightly();
                    demonSpeechBubble = null;
                })
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void CloseDemonSpeech()
    {
        speechCloseTween?.Kill();
        speechCloseTween = null;
        if (demonSpeechBubble != null)
            demonSpeechBubble.CloseStraightly();

        demonSpeechBubble = null;
    }

    #endregion

    #region Cleanup and Events

    private void OnGameReset(GameResetEvent evt)
    {
        CleanupOnLevelTeardown();
    }

    private void OnGameReplay(GameReplayEvent evt)
    {
        CleanupOnLevelTeardown();
    }

    // GameReset/GameReplay 事件由 PlayCtrl 先尝试插屏广告并等待约 0.5 秒后才销毁并重建整个关卡；
    // 小恶魔与落箭都挂在关卡层级内，会随关卡一并销毁。此处若立即销毁可视对象，box-bad 会先于关卡
    // 其余元素凭空消失，因此事件路径只作逻辑清理，把可视对象留给关卡层级统一销毁。
    private void CleanupOnLevelTeardown()
    {
        CleanupAttempt(false, false);
    }

    private void CleanupAttempt(bool resumePlayer, bool destroyRuntimeObjects = true)
    {
        sessionVersion++;
        dragActive = false;
        boardSnapBusy = false;
        pointerTween?.Kill();
        boardSnapTween?.Kill();
        settlementDelayTween?.Kill();
        pointerTween = null;
        boardSnapTween = null;
        settlementDelayTween = null;
        DOTween.Kill(gameObject);
        StopScoreFeedbacks(true);
        ResetTargetImpactFeedback();
        settlementPendingAfterTargetImpact = false;
        turnAnnouncementShown = false;
        settlementAnnouncementStarted = false;
        CloseDemonSpeech();

        CancelDemonWalk();
        player?.CancelLevel78Action(resumePlayer);
        demon?.CancelLevel78Action(false);
        if (destroyRuntimeObjects && demon != null)
            Destroy(demon.gameObject);

        demon = null;

        if (destroyRuntimeObjects)
        {
            foreach (GameObject arrowObject in spawnedArrows)
            {
                if (arrowObject != null)
                    Destroy(arrowObject);
            }
        }

        spawnedArrows.Clear();
        demonArrowRenderers.Clear();
        ApplyAimGaugeVisual(Level78BoardPosition.Far);
        SetAimMode(false);
        UnbindPanel();
        duel = null;
    }

    private bool IsCurrentSession(int currentSession)
    {
        return isActiveAndEnabled && duel != null && currentSession == sessionVersion;
    }

    private static float NormalizeAngle180(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        if (angle < -180f)
            angle += 360f;
        return angle;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private sealed class UnityLevel78Random : ILevel78Random
    {
        public int Range(int minInclusive, int maxExclusive)
        {
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
    }

    #endregion
}
