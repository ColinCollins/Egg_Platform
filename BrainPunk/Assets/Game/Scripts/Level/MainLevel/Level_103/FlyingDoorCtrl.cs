using DG.Tweening;
using Game.ItemEvent;
using Game.Level103;
using UnityEngine;

/// <summary>
/// Level 103「不要跳」飞门编排器：门先长出双翼，飞到固定悬停点；陷坑流程完成后，
/// 玩家按下门体或任意翅膀会触发同一套收束流程。双翼同步沿世界 Y 轴垂直上飞，
/// 门体在点击同帧沿世界 Y 轴垂直落地，落稳并完成既有门禁收口后才恢复进门触发器。
/// </summary>
public sealed class FlyingDoorCtrl : MonoBehaviour
{
    #region Inspector

    /// <summary>门体后层的两侧白色羽翼节点；Prefab 保存最终镜像与适配缩放，Awake 缓存后收起为零。</summary>
    [SerializeField] private Transform[] wings;

    /// <summary>悬停浮动节点（门体视觉根，浮空时上下 Yoyo）；门下落时同步归零。</summary>
    [SerializeField] private Transform floatPivot;

    /// <summary>悬停高度（世界 Y）。</summary>
    [SerializeField] private float hoverHeight = 3.545f;

    /// <summary>悬停点世界 X；门落地时只改 Y，因此落点固定在该 X 正下方。</summary>
    [SerializeField] private float hoverX = 26.9f;

    /// <summary>竖直飞至 hoverHeight 后，水平飞抵 hoverX 的时长（秒）。</summary>
    [SerializeField] private float hoverFlyDuration = 1f;

    /// <summary>双翼从零缩放长到 Prefab 适配比例的时长（秒）。</summary>
    [SerializeField] private float wingGrowDuration = 0.3f;

    /// <summary>门向上飞至 hoverHeight 的时长（秒）。</summary>
    [SerializeField] private float flyDuration = 1.2f;

    /// <summary>悬停漂浮组件（autoStart=false、Vertical、Yoyo、loops=-1）。</summary>
    [SerializeField] private MoveFloatHandle hoverFloat;

    /// <summary>
    /// 复用现有 DragEventsListener 的按下回调做世界物体点击检测；不再处理 Drag/End。
    /// fallbackPickRadius 覆盖门体和双翼，鼠标按下或触摸 Began 即触发。
    /// </summary>
    [SerializeField] private DragEventsListener dragListener;

    /// <summary>
    /// door_0 实例内的进入触发器。初始化至门体落稳前始终禁用，落地 Tween 完成且 Domain 接受后才启用。
    /// </summary>
    [SerializeField] private Collider2D doorTrigger;

    [Header("点击落门标定")]
    /// <summary>双翼点击后垂直上飞的世界目标 Y。</summary>
    [SerializeField] private float wingFlyTargetY = 10f;

    /// <summary>双翼点击后垂直上飞并隐藏的时长（秒）。</summary>
    [SerializeField] private float wingFlyDuration = 1f;

    /// <summary>点击后与双翼上飞同时开始的门体垂直下落时长（秒）；沿用 Level 30 钥匙下落节奏。</summary>
    [SerializeField] private float doorFallDuration = 0.8f;

    #endregion

    #region Runtime State

    private Level103DontJumpRule rule;
    private float groundY;
    private Sequence flySequence;
    private Sequence landingSequence;
    private Vector3[] wingExpandedScales;
    private bool clickSubscribed;

    #endregion

    #region Unity Messages

    private void Awake()
    {
        groundY = transform.position.y;
        if (wings != null)
        {
            wingExpandedScales = new Vector3[wings.Length];
            for (int i = 0; i < wings.Length; i++)
            {
                Transform wing = wings[i];
                if (wing == null)
                    continue;

                wingExpandedScales[i] = wing.localScale;
                wing.localScale = Vector3.zero;
            }
        }

        if (dragListener != null)
            dragListener.SetEnableDrag(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeClick();
    }

    private void OnDisable() => CleanupRuntimeState();

    private void OnDestroy() => CleanupRuntimeState();

    private void CleanupRuntimeState()
    {
        KillSequences();
        UnsubscribeClick();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 门飞至悬停高度时门体底部的世界 Y。门处于 Prefab 地面原位时读取触发器底边偏移，
    /// 供悬空平台等本关编排定位。
    /// </summary>
    public float HoverBottomY
    {
        get
        {
            float groundBottomY = doorTrigger != null ? doorTrigger.bounds.min.y : transform.position.y;
            return hoverHeight + (groundBottomY - transform.position.y);
        }
    }

    /// <summary>门是否已物理飞抵悬停点。</summary>
    public bool HasReachedHover { get; private set; }

    /// <summary>由 Level103Ctrl 注入 Domain 规则会话，并关闭落地前的进门触发器。</summary>
    public void Initialize(Level103DontJumpRule dontJumpRule)
    {
        rule = dontJumpRule;
        if (doorTrigger != null)
            doorTrigger.enabled = false;
    }

    /// <summary>
    /// ① 双翼长出；② 门竖直飞到 hoverHeight；③ 门水平飞到 hoverX；④ 开始轻微悬停浮动。
    /// 飞门演出由 Level103Ctrl 在主角 X≥0.8 时一次性启动。
    /// </summary>
    public void Play()
    {
        KillSequences();
        HasReachedHover = false;
        if (doorTrigger != null)
            doorTrigger.enabled = false;

        flySequence = DOTween.Sequence().SetLink(gameObject);
        if (wings != null)
        {
            for (int i = 0; i < wings.Length; i++)
            {
                Transform wing = wings[i];
                if (wing == null)
                    continue;

                wing.gameObject.SetActive(true);
                Vector3 expandedScale = wingExpandedScales != null && i < wingExpandedScales.Length
                    ? wingExpandedScales[i]
                    : Vector3.one;
                flySequence.Join(wing.DOScale(expandedScale, wingGrowDuration).SetEase(Ease.OutBack));
            }
        }

        flySequence.Append(transform.DOMoveY(hoverHeight, flyDuration).SetEase(Ease.OutCubic));
        flySequence.Append(transform.DOMoveX(hoverX, hoverFlyDuration).SetEase(Ease.OutCubic));
        flySequence.AppendCallback(EnterHover);
    }

    /// <summary>
    /// 陷坑完成且门已到位后开放点击。DragEventsListener 只用于按下命中，不再移动门体。
    /// </summary>
    public void OpenClickAtHover()
    {
        if (!HasReachedHover)
            return;

        if (dragListener != null)
            dragListener.SetEnableDrag(true);
    }

    #endregion

    #region Hover And Landing

    private void EnterHover()
    {
        HasReachedHover = true;
        hoverFloat?.StartMove();
    }

    private void OnDoorPressed(Transform target)
    {
        _ = target;
        if (rule == null || !rule.TryStartDoorLanding())
            return;

        HasReachedHover = false;
        if (dragListener != null)
            dragListener.SetEnableDrag(false);
        hoverFloat?.StopMove();

        KillLandingSequence();
        landingSequence = DOTween.Sequence().SetLink(gameObject);

        float departureDuration = Mathf.Max(0.01f, wingFlyDuration);
        if (wings != null)
        {
            foreach (Transform wing in wings)
            {
                if (wing == null || !wing.gameObject.activeSelf)
                    continue;

                landingSequence.Join(wing.DOMoveY(wingFlyTargetY, departureDuration));
            }
        }

        float fallDuration = Mathf.Max(0.01f, doorFallDuration);
        landingSequence.Join(transform.DOMoveY(groundY, fallDuration).SetEase(Ease.InQuart));
        if (floatPivot != null)
            landingSequence.Join(floatPivot.DOLocalMoveY(0f, fallDuration).SetEase(Ease.InQuart));
        landingSequence.InsertCallback(departureDuration, CompleteWingDeparture);
        landingSequence.InsertCallback(fallDuration, CompleteDoorLanding);
        landingSequence.OnComplete(() => landingSequence = null);
    }

    private void CompleteWingDeparture()
    {
        if (wings != null)
        {
            foreach (Transform wing in wings)
            {
                if (wing != null)
                    wing.gameObject.SetActive(false);
            }
        }
    }

    private void CompleteDoorLanding()
    {
        if (rule == null || !rule.NotifyDoorLanded())
            return;

        if (doorTrigger != null)
            doorTrigger.enabled = true;
    }

    #endregion

    #region Helpers

    private void ResolveReferences()
    {
        if (dragListener == null)
            dragListener = GetComponent<DragEventsListener>();
        if (dragListener == null)
            dragListener = GetComponentInChildren<DragEventsListener>(true);
        if (hoverFloat == null)
            hoverFloat = GetComponent<MoveFloatHandle>();
        if (doorTrigger == null)
            doorTrigger = GetComponentInChildren<Collider2D>(true);
    }

    private void SubscribeClick()
    {
        if (clickSubscribed || dragListener == null)
            return;

        dragListener.BeginDragHandlers += OnDoorPressed;
        clickSubscribed = true;
    }

    private void UnsubscribeClick()
    {
        if (!clickSubscribed)
            return;

        if (dragListener != null)
            dragListener.BeginDragHandlers -= OnDoorPressed;
        clickSubscribed = false;
    }

    private void KillSequences()
    {
        if (flySequence != null && flySequence.IsActive())
            flySequence.Kill();
        flySequence = null;
        KillLandingSequence();
    }

    private void KillLandingSequence()
    {
        if (landingSequence != null && landingSequence.IsActive())
            landingSequence.Kill();
        landingSequence = null;
    }

    #endregion
}
