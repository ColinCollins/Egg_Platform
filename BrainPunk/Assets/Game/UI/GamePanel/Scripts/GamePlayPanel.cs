using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Play;
using UnityEngine;
using UnityEngine.UI;

public partial class GamePlayPanel : BaseUIView, IDebuger, IEventSender
{
    #region System
    private bool isPause = false;
    private bool arePauseTipsFunctionsSwapped;

    private EventSubscriber _subscriber;

    #endregion

    #region Player ctrl

    private bool isRightDown = false;

    private bool isLeftDown = false;

    [SerializeField] private Image blackMask;
    [SerializeField] private GameObject clickBlock;

    [SerializeField] private CustomButton useBtn;

    /// <summary>
    /// 用途：向需要临时替换跳跃按钮视觉的关卡提供当前共享 HUD 跳跃图标。
    /// 调用边界：必须由 GamePlayPanel Prefab 显式绑定，关卡只可修改本局临时表现并在退出时恢复。
    /// 注意事项：该引用不负责输入命中；Jump 按钮根节点的 Graphic 继续维护稳定射线范围。
    /// </summary>
    [SerializeField] private Image jumpButtonIcon;

    [SerializeField] private float fadeInDuration = 0.4f;
    
    [SerializeField] private float fadeOutDuration = 0.4f;

    #endregion

    [Header("Show Panel Anim")]
    [SerializeField] private SequentialScaleAnim sequentialScaleAnim;

    public override void OnCreate()
    {
        base.OnCreate();

        InitBtns();

        sequentialScaleAnim.Completed += () =>
        {
            Debug.Log("All Complete");
            clickBlock.SetActive(false);
        };
    }

    public override void OnOpen()
    {
        base.OnOpen();
        isPause = false;
        isRightDown = false;
        isLeftDown = false;
        arePauseTipsFunctionsSwapped = false;
        clickBlock.SetActive(false);

        AddListener();
        HidePostcardStarBlockAtLevelStart();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
        _subscriber.Subscribe<GameReplayEvent>(OnGameReplay);
        _subscriber.Subscribe<GameResumeEvent>(OnGamePanelResume);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnGameStateChanged);
        _subscriber.Subscribe<GamePlayPanelFadeInEvent>(OnGamePlayFadeIn);
        _subscriber.Subscribe<GamePlayPanelSwitchBlockEvent>(OnSwichEventBlock);
        _subscriber.Subscribe<PostcardGetStarEvent>(OnGetStar);

        // _subscriber.Subscribe<GameShowBannerEvent>(OnSDKShowBannerEvent);
    }

    private void OnGameReplay(GameReplayEvent @event)
    {
        PlayResetAnim();
    }

    private void OnSwichEventBlock(GamePlayPanelSwitchBlockEvent @event)
    {
        clickBlock.SetActive(@event.IsShow);
    }

    #region Buttons

    /// <summary>
    /// 用途：提供当前 Gameplay 商店按钮的首次皮肤引导定位目标。
    /// 调用边界：仅由 Shop2SkinGuideFlow 通过 PlayCtrl.CurrentGamePlayPanel 在运行态读取。
    /// 注意事项：只暴露 RectTransform，不允许引导流程改写生成字段或按钮事件。
    /// </summary>
    internal RectTransform Shop2SkinGuideTarget =>
        ShopBtn != null ? ShopBtn.transform as RectTransform : null;

    /// <summary>
    /// 用途：读取基础 GameplayPanel Prefab 已显式绑定的跳跃图标。
    /// 调用边界：仅供需要替换或旋转本局跳跃图标的关卡控制器使用。
    /// 注意事项：调用方必须缓存并恢复原图标状态，不得据此改写共享按钮事件。
    /// </summary>
    internal Image JumpButtonIcon => jumpButtonIcon;

    private void InitBtns()
    {
        ResetBtn.OnClick += OnClickReset;
        PauseBtn.OnClick += OnClickSetting;
        TipsBtn.OnClick += OnClickTips;
        ShopBtn.OnClick += OnClickShop;

        if (JumpBtn)
            JumpBtn.OnClickDown += OnClickJump;

        RightMoveBtn.OnClickEnter += OnClickDownRight;
        LeftMoveBtn.OnClickEnter += OnClickDownLeft;

        RightMoveBtn.OnClickUp += OnClickUpRight;
        LeftMoveBtn.OnClickUp += OnClickUpLeft;
        RightMoveBtn.OnClickExit += OnClickUpRight;
        LeftMoveBtn.OnClickExit += OnClickUpLeft;
    }


    private void OnClickDownRight(CustomButton btn)
    {
        this.Log("Right Down");
        isRightDown = true;
    }

    private void OnClickDownLeft(CustomButton btn)
    {
        this.Log("Left Down");
        isLeftDown = true;
    }

    private void OnClickUpRight(CustomButton btn)
    {
        this.Log("Right Up");
        isRightDown = false;
        this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
    }

    private void OnClickUpLeft(CustomButton btn)
    {
        this.Log("Left Up");
        isLeftDown = false;
        this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
    }

    private void OnClickJump(CustomButton btn)
    {
        if (isPause)
            return;

        this.Log("Jump");
        this.DispatchEvent(Witness<PlayerJumpEvent>._);
    }

    #endregion
    void Update()
    {
        if (isPause)
            return;

        if (isRightDown)
            this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
        else if (isLeftDown)
            this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);
    }

    private void OnClickReset(CustomButton btn)
    {
        this.Log("Play Game");
        isPause = true;
        this.DispatchEvent(Witness<GameResetEvent>._, GameResetType.Manually);
        // PlayResetAnim();
    }

    private void OnClickSetting(CustomButton btn)
    {
        if (arePauseTipsFunctionsSwapped)
        {
            OpenTipsFunction();
            return;
        }

        OpenPauseFunction();
    }

    private void OpenPauseFunction()
    {
        this.Log("Pause Game");
        isPause = true;
        IPlayPauseHost pauseHost = GameManager.Instance?.ControllerManager?.ActivePauseHost;
        if (pauseHost != null)
            pauseHost.Pause();
        else
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);

        GameSettingPopup.Create(true, pauseHost);
    }

    private void OnClickShop(CustomButton btn)
    {
        isPause = true;
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
        ShopPanel.Create(true, GetType().Name);
    }


    private void OnClickTips(CustomButton btn)
    {
        if (arePauseTipsFunctionsSwapped)
        {
            OpenPauseFunction();
            return;
        }

        OpenTipsFunction();
    }

    private void OpenTipsFunction()
    {
        this.Log("Tips Panel");
        isPause = true;
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
        this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, false);
        GameTipsPopup.Create();
    }

    /// <summary>
    /// 仅供关卡局部异常玩法交换暂停/提示图标时同步按钮功能；关闭后恢复公共面板默认映射。
    /// </summary>
    public void SetPauseTipsFunctionsSwapped(bool swapped)
    {
        arePauseTipsFunctionsSwapped = swapped;
    }

    private void OnGameReset(GameResetEvent evt)
    {
        PlayResetAnim();
    }

    private void PlayResetAnim()
    {
        blackMask.gameObject.SetActive(true);
        blackMask.color = new Color(0, 0, 0, 0);
        blackMask.DOFade(1f, fadeOutDuration);
    }

    private void OnGamePlayFadeIn(GamePlayPanelFadeInEvent @event)
    {
        blackMask.gameObject.SetActive(true);
        blackMask.color = new Color(0, 0, 0, 1f);
        blackMask.DOFade(0f, fadeInDuration).SetUpdate(true).OnComplete(() =>
        {
            blackMask.gameObject.SetActive(false);
        });
    }

    private void OnGamePanelResume(GameResumeEvent evt)
    {
        isPause = false;
    }

    private void OnGameStateChanged(SwitchGameStateEvent evt)
    {
        isPause = !evt.NewState.Equals(GamePlayStateName.PLAYING);
    }

    public override void OnClose()
    {
        arePauseTipsFunctionsSwapped = false;
        PlayCtrl.Instance?.CancelGameTipsGuideFor(this);
        Shop2SkinGuideFlow.CancelFor(this);
        base.OnClose();
        StopPostcardStarEffects();

        EventsUtils.ResetEvents(ref _subscriber);
    }

    public void SetData(int level)
    {
        LevelTxt.text = level.ToString();
    }

    public void HideAllBtns()
    {
        var btns = GetComponentsInChildren<CustomButton>();
        for (int i = 0; i < btns.Length; i++)
        {
            btns[i].transform.localScale = Vector3.zero;
        }
    }

    public async UniTask PlayShowPanelAnim()
    {
        HideAllBtns();

        clickBlock.SetActive(true);
        await UniTask.WaitForSeconds(1f, true);

        if (sequentialScaleAnim == null)
            sequentialScaleAnim = GetComponent<SequentialScaleAnim>();

        if (sequentialScaleAnim != null)
        {
            var btns = GetComponentsInChildren<CustomButton>();
            var transforms = new Transform[btns.Length];
            for (int i = 0; i < btns.Length; i++)
            {
                transforms[i] = btns[i] != null ? btns[i].transform : null;

                /*         btns[i].transform.localScale = Vector3.zero;
                        btns[i].transform.DOScale(Vector3.one, 0.2f).SetDelay(0.06f * i).SetUpdate(UpdateType.Normal, true).SetEase(Ease.Linear); */
            }

            sequentialScaleAnim.SetTargets(transforms);
            sequentialScaleAnim.Play();
        }
    }

    public void ResetState()
    {
        isRightDown = false;
        isLeftDown = false;
    }

    public void SwitchMoveBtns(bool isShow)
    {
        if (!isShow)
            ResetState();

        if (LeftMoveBtn != null)
            LeftMoveBtn.gameObject.SetActive(isShow);

        if (RightMoveBtn != null)
            RightMoveBtn.gameObject.SetActive(isShow);
    }

    #region Game Tips Guide

    /// <summary>
    /// 用途：向 PlayCtrl 提供首次提示剧情引导所需的提示按钮和界面根节点。
    /// 调用边界：仅由 PlayCtrl 创建引导流程时在 Unity 运行态主线程调用。
    /// 注意事项：本方法只返回当前界面的局部 UI 引用，不创建、持有或运行引导流程。
    /// </summary>
    /// <param name="tipsButtonTarget">可用时返回提示按钮的 RectTransform。</param>
    /// <param name="guideRoot">可用时返回承载第一枚 GuideFinger 的界面根节点。</param>
    /// <returns>界面已打开且两个引导节点均有效时返回 true。</returns>
    internal bool TryGetGameTipsGuideTargets(
        out RectTransform tipsButtonTarget,
        out RectTransform guideRoot)
    {
        tipsButtonTarget = null;
        guideRoot = null;

        if (!Application.isPlaying || !IsOpen || TipsBtn == null || !TipsBtn.gameObject.activeInHierarchy)
        {
            return false;
        }

        tipsButtonTarget = TipsBtn.transform as RectTransform;
        guideRoot = transform as RectTransform;
        return tipsButtonTarget != null && guideRoot != null;
    }

    /// <summary>
    /// 用途：按提示按钮既有行为暂停关卡并打开首次引导专用的免费提示弹窗。
    /// 调用边界：仅供 PlayCtrl 持有的 GameTipsGuideFlow 通过局部回调调用。
    /// 注意事项：界面已关闭时返回 null，不负责引导流程的生命周期。
    /// </summary>
    internal GameTipsPopup OpenFirstGuideFreeTipsPopup()
    {
        if (!IsOpen)
            return null;

        isPause = true;
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
        this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, false);
        return GameTipsPopup.CreateFirstGuideFree();
    }

    #endregion

    public static GamePlayPanel Create(string panelName = "")
    {
        if (string.IsNullOrEmpty(panelName))
            panelName = $"{typeof(GamePlayPanel).Name}";

        var panel = UIManager.Instance.OpenUI<GamePlayPanel>(panelName, UILayer.Normal);
        return panel;
    }

    void OnDestroy()
    {
        StopPostcardStarEffects();

        if (blackMask != null)
            blackMask.DOKill();
    }
}
