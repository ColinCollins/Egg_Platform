using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Level083;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 083“动人的音乐”本地玩法编排器；拥有演奏台、乐器、谱面、音符和胜负收口。
/// </summary>
public sealed class Level83GameplayCtrl : MonoBehaviour, IDebuger
{
    private const float InstrumentBubbleShowStagger = 0.08f;
    private const float InstrumentBubbleDismissStagger = 0.08f;
    private const float RivalBubbleShowDuration = 1.5f;
    private const float BeatSlotShowStagger = 0.08f;
    private const float BeatSlotHideStagger = 0.08f;
    // 谱面槽判定音效：正确输入播放当前乐器对应的多 clip tag（SoundsGood 默认随机选 clip，
    // 与 actorWalk/step 等多 clip 先例一致）；错误复用 l5 passwordError（L89 序列输入错误同款先例）。
    private const string GuitarSfxTag = "l83Guitar";
    private const string HarpSfxTag = "l83Harp";
    private const string SaxophoneSfxTag = "l83Sax";
    private const string BeatSlotIncorrectSfxTag = "passwordError";
    // 对手台词气泡走 HintBubble 的 key 驱动链路（LocalizationManager.GetTranslation + 切语言热刷新）；
    // key 在本组件 Inspector 上可配；简体中文已登记到飞书多语言表，其他语言与本地 CSV/I2 导入由后续同步流程处理。
    // 本地词条未导入前，气泡 fallback 显示 key 原文。
    [SerializeField] private string evaluationBubbleKey = "S_LevelTips_Des_level_83_1";
    [SerializeField] private string victoryBubbleKey = "S_LevelTips_Des_level_83_2";
    [SerializeField] private string incorrectBubbleKey = "S_LevelTips_Des_level_83_3";
    private const string HintBubbleResourcePath = "HintBubble";
    private static readonly Vector2[] NoteArrivalLaneOffsets =
    {
        new Vector2(0f, 0f),
        new Vector2(0.12f, 0.28f),
        new Vector2(0.34f, 0.08f)
    };

    #region Inspector

    /// <summary>承载标准输入开关、成功与失败流程的关卡基础控制器。</summary>
    [SerializeField] private BaseLevelCtrl levelCtrl;

    /// <summary>从左门进入并执行演奏输入的主角 Box。</summary>
    [SerializeField] private ActorCtrl actor;

    /// <summary>Level 083 独立 Box Bad 状态与冲撞控制器。</summary>
    [SerializeField] private Level83RivalCtrl rival;

    /// <summary>站稳演奏台且未选乐器时显示的三个候选气泡根节点。</summary>
    [SerializeField] private GameObject instrumentBubbles;

    /// <summary>乐器被选中后移动到主角手中的世界坐标锚点。</summary>
    [SerializeField] private Transform instrumentHandAnchor;

    /// <summary>八个预置谱面格的显示根节点。</summary>
    [SerializeField] private GameObject directionStrip;

    /// <summary>固定预置的八个谱面格，顺序必须与视觉从左到右一致。</summary>
    [SerializeField] private Level83BeatSlot[] beatSlots;

    /// <summary>运行时生成实体音符的父节点。</summary>
    [SerializeField] private Transform notesRoot;

    /// <summary>实体音符的发声锚点（与手部锚点同节点）；生成时再叠加乐器专属发声口偏移。</summary>
    [SerializeField] private Transform noteEmissionAnchor;

    /// <summary>实体音符从乐器发声点飘到可拖拽位置的时长。</summary>
    [SerializeField] private float noteEmissionDuration = 0.4f;

    /// <summary>实体音符沿主角朝向向前飘出的基础距离。</summary>
    [SerializeField] private float noteForwardDistance = 1.2f;

    /// <summary>实体音符从乐器发声点向上飘出的基础距离。</summary>
    [SerializeField] private float noteUpwardDistance = 0.8f;

    /// <summary>吉他发声口（音孔）相对手部锚点的偏移；x 按主角朝向镜像。</summary>
    [SerializeField] private Vector2 guitarEmissionOffset = new Vector2(0f, -1.1f);

    /// <summary>竖琴发声口（琴弦中心）相对手部锚点的偏移；x 按主角朝向镜像。</summary>
    [SerializeField] private Vector2 harpEmissionOffset = new Vector2(0.24f, -0.77f);

    /// <summary>萨克斯发声口（喇叭口）相对手部锚点的偏移；x 按主角朝向镜像。</summary>
    [SerializeField] private Vector2 saxophoneEmissionOffset = new Vector2(0.45f, -1.15f);

    /// <summary>吉他正确输入生成的蓝色音符模板。</summary>
    [SerializeField] private Level83MusicNote guitarNoteTemplate;

    /// <summary>竖琴正确输入生成的红色音符模板。</summary>
    [SerializeField] private Level83MusicNote harpNoteTemplate;

    /// <summary>萨克斯正确输入生成的黄色音符模板。</summary>
    [SerializeField] private Level83MusicNote saxophoneNoteTemplate;

    /// <summary>世界空间拖拽换算使用的关卡相机。</summary>
    [SerializeField] private Camera gameplayCamera;

    /// <summary>命中同帧从 Box Bad 身上爆出的预置钥匙；初始隐藏，由本控制器激活。</summary>
    [SerializeField] private GameObject dropKey;

    /// <summary>预置钥匙自带的浮动组件；爆出飞行期间禁用，落地后启用并接管浮动。</summary>
    [SerializeField] private Behaviour dropKeyFloat;

    /// <summary>钥匙爆出后的落点锚点（位于 Box Bad 与右侧门之间），供策划微调。</summary>
    [SerializeField] private Transform keyLandingPoint;

    /// <summary>钥匙爆出抛物时长范围（秒），对齐 ItemJumpOutExecutor 的 0.35–0.6。</summary>
    [SerializeField] private Vector2 keyJumpDurationRange = new Vector2(0.35f, 0.6f);

    /// <summary>钥匙爆出抛物高度范围（世界坐标），对齐 ItemJumpOutExecutor 的 1.0–2.5。</summary>
    [SerializeField] private Vector2 keyJumpHeightRange = new Vector2(1.0f, 2.5f);

    #endregion

    #region Runtime State

    private readonly List<Level83MusicNote> activeNotes = new List<Level83MusicNote>();
    private readonly Level83Performance performance = new Level83Performance();
    private Level83ActorCtrl level83Actor;
    private EventSubscriber subscriber;
    private bool isActorStandingOnStage;
    private bool moveInputLatched;
    private bool isInstrumentReady;
    private bool isResolving;
    private bool isCelebrating;
    private bool isAwaitingReselection;
    private Level83InstrumentOption selectedInstrumentOption;
    private int noteEmissionIndex;
    private int flowVersion;
    private HintBubble activeRivalBubble;
    private Tween keyJumpTween;

    #endregion

    #region Unity Messages

    private void Start()
    {
        ResolveReferences();
        BindEvents();
        InitializeGameplay();
    }

    private void OnDisable()
    {
        // 先解除全局监听，避免子对象清理异常留下已销毁关卡的事件回调。
        EventsUtils.ResetEvents(ref subscriber);
        ++flowVersion;
        keyJumpTween?.Kill();
        keyJumpTween = null;
        if (level83Actor != null)
            level83Actor.RestoreDefaultAnimation();
        StopDirectionStripFeedback(true);
        RemoveActiveRivalBubble();
    }

    private void OnDestroy()
    {
        // 销毁顺序不保证子对象仍有效，事件解绑必须早于任何表现清理。
        EventsUtils.ResetEvents(ref subscriber);
        ++flowVersion;
        keyJumpTween?.Kill();
        keyJumpTween = null;
        if (level83Actor != null)
            level83Actor.RestoreDefaultAnimation();
        StopDirectionStripFeedback(true);
        RemoveActiveRivalBubble();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 接收演奏台顶面站立状态；已选择乐器后离开不再收回候选或解除锁定，
    /// 但等待重选状态下与未选乐器走同一显隐分支（离开收回、站稳重现）。
    /// </summary>
    /// <param name="standing">true 表示主角脚底正由演奏台顶面承托；false 表示尚未站稳或已经离台。</param>
    public void SetActorStandingOnStage(bool standing)
    {
        if (isActorStandingOnStage == standing)
            return;

        isActorStandingOnStage = standing;
        if ((performance.IsStarted && !isAwaitingReselection) || isResolving)
            return;

        SetInstrumentBubblesVisible(standing);
    }

    /// <summary>
    /// 选定本轮乐器并进入演奏锁定；首次选择走 TryStart，等待重选状态下走 TryReselect。
    /// </summary>
    public void SelectInstrument(Level83Instrument instrument, Level83InstrumentOption selectedOption)
    {
        if (!isActorStandingOnStage || isResolving)
            return;

        bool accepted = isAwaitingReselection
            ? performance.TryReselect(instrument)
            : performance.TryStart(instrument);
        if (!accepted)
            return;

        isAwaitingReselection = false;
        selectedInstrumentOption = selectedOption;
        levelCtrl?.SetCtrlable(false);
        actor?.StopMoving();
        level83Actor?.BeginLevel83AnimationControl();
        isInstrumentReady = false;
        SetInstrumentOptionsSelectable(false);
        if (directionStrip != null)
            directionStrip.SetActive(false);

        int selectionVersion = flowVersion;
        selectedOption?.MoveInstrumentToHand(
            instrumentHandAnchor,
            () => BeginPerformanceAfterInstrumentArrival(instrument, selectionVersion));
        SetInstrumentBubblesVisible(false, selectedOption);
        if (selectedOption == null)
            BeginPerformanceAfterInstrumentArrival(instrument, selectionVersion);
    }

    /// <summary>
    /// 接收实体音符首次有效命中，关闭谱面与音符交互；命中同帧爆出钥匙并恢复移动输入，
    /// 庆祝循环期间任意移动或跳跃输入立即打断庆祝恢复通用移动；玩家始终无输入时
    /// 由 die 完成回调兜底恢复默认动画。通关改由捡拾钥匙解锁右侧门后进入门触发。
    /// </summary>
    public void HandleMusicNoteHit(Level83MusicNote note)
    {
        if (isResolving || !performance.IsStarted || note == null)
            return;

        isResolving = true;
        isCelebrating = true;
        ++flowVersion;
        // 等待重选期间命中同样成立（IsStarted 保持 true）；胜利收口前必须退出重选态
        // 并收起正在显示的乐器候选气泡，旧流程胜利时气泡从不可见，未覆盖该路径。
        // 非重选期间气泡本就隐藏，不得调用 SetInstrumentBubblesVisible(false)，
        // 避免 HideImmediately 把已飞手的候选乐器图形还原显示。
        bool wasAwaitingReselection = isAwaitingReselection;
        isAwaitingReselection = false;
        if (wasAwaitingReselection)
            SetInstrumentBubblesVisible(false);
        StopDirectionStripFeedback(false);
        if (directionStrip != null)
            directionStrip.SetActive(false);
        RemoveActiveRivalBubble();
        DisableNoteAndOptionInteraction();
        rival?.PlayNoteHitEffect(note.transform.position);
        note.Consume();
        activeNotes.Remove(note);
        level83Actor?.PlayCelebrateAnimation();
        ShowRivalBubble(victoryBubbleKey);
        DropKeyFromRival();
        ReleaseMovementForCelebrationInterrupt();
        rival?.PlayDie(HandleRivalDieCompleted);
    }

    #endregion

    #region Initialization

    private void ResolveReferences()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor : null;
        level83Actor = actor as Level83ActorCtrl;
        if (actor != null && level83Actor == null)
            this.LogWarning("[Level83GameplayCtrl] 主角未绑定 Level83ActorCtrl，演奏动画将无法由本关隔离控制。");
        gameplayCamera ??= GetComponentInChildren<Camera>(true);
    }

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        subscriber.Subscribe<PlayerMoveCancelEvent>(OnPlayerMoveCancel);
        subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
        subscriber.Subscribe<GameReplayEvent>(OnGameReplay);
    }

    private void InitializeGameplay()
    {
        ++flowVersion;
        StopDirectionStripFeedback(true);
        isActorStandingOnStage = false;
        moveInputLatched = false;
        isInstrumentReady = false;
        isResolving = false;
        isCelebrating = false;
        isAwaitingReselection = false;
        selectedInstrumentOption = null;
        noteEmissionIndex = 0;
        level83Actor?.RestoreDefaultAnimation();
        SetInstrumentBubblesVisible(false);
        SetInstrumentOptionsSelectable(false);
        if (directionStrip != null)
            directionStrip.SetActive(false);

        SetNoteTemplateActive(false);
    }

    #endregion

    #region Input

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        BreakCelebrationIfActive();
        HandleMoveInput(Level83PerformanceInput.Left);
    }

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        BreakCelebrationIfActive();
        HandleMoveInput(Level83PerformanceInput.Right);
    }

    private void OnPlayerMoveCancel(PlayerMoveCancelEvent evt)
    {
        moveInputLatched = false;
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        BreakCelebrationIfActive();
        HandlePerformanceInput(Level83PerformanceInput.Jump);
    }

    /// <summary>
    /// 庆祝循环期间首个移动或跳跃输入立即打断庆祝、恢复通用角色动画；
    /// 移动与跳跃本身由 BaseLevelCtrl 对同一事件的既有处理驱动。必须保留当前移动输入：
    /// 与本控制器接收同一事件的 BaseLevelCtrl 写入输入的先后序不定，打断不得吞掉本次输入。
    /// </summary>
    private void BreakCelebrationIfActive()
    {
        if (!isCelebrating)
            return;

        isCelebrating = false;
        level83Actor?.RestoreDefaultAnimation(true);
    }

    private void HandleMoveInput(Level83PerformanceInput input)
    {
        if (moveInputLatched)
            return;

        moveInputLatched = true;
        HandlePerformanceInput(input);
    }

    private void HandlePerformanceInput(Level83PerformanceInput input)
    {
        if (isResolving || !performance.IsStarted || !isInstrumentReady)
            return;

        Level83InputResult result = performance.TryAcceptInput(input, out int beatIndex);
        if (result == Level83InputResult.Ignored || beatIndex < 0 || beatIndex >= beatSlots.Length)
            return;

        switch (result)
        {
            case Level83InputResult.Correct:
                beatSlots[beatIndex]?.SetCorrect();
                PlayInstrumentSfx();
                SetAwaitingBeat(beatIndex + 1);
                SpawnMusicNote();
                break;
            case Level83InputResult.RoundCompleted:
                beatSlots[beatIndex]?.SetCorrect();
                PlayInstrumentSfx();
                SpawnMusicNote();
                rival?.PlayIdle();
                HintBubble evaluationBubble = ShowRivalBubble(evaluationBubbleKey);
                EnterReselectionAfterFeedback(flowVersion, evaluationBubble).Forget();
                break;
            case Level83InputResult.Incorrect:
                StopDirectionStripFeedback(false);
                beatSlots[beatIndex]?.SetIncorrect();
                AudioManager.PlaySound(BeatSlotIncorrectSfxTag);
                BeginFailure();
                break;
        }
    }

    #endregion

    #region Performance Flow

    /// <summary>
    /// 评价气泡完整收起后进入乐器重选：解除演奏锁定放行玩家、上一轮乐器图形静默归位、
    /// 谱面槽保持全绿原样从右到左反向错峰退回；退完后关闭谱面条并按站立态重现气泡。
    /// </summary>
    private async UniTaskVoid EnterReselectionAfterFeedback(int version, HintBubble evaluationBubble)
    {
        await UniTask.WaitUntil(
            () => evaluationBubble == null,
            cancellationToken: this.GetCancellationTokenOnDestroy());
        if (version != flowVersion || isResolving || !performance.TryEnterReselection())
            return;

        isAwaitingReselection = true;
        isInstrumentReady = false;
        ReleasePlayerControl();
        selectedInstrumentOption?.RestoreInstrumentVisualSilently();
        selectedInstrumentOption = null;
        PlayBeatSlotsExit(version);
    }

    /// <summary>
    /// 谱面槽退回：PlayBeatSlotsEntrance 的倒放镜像，从最右格开始逐格 InBack 缩回，
    /// 槽内箭头保持完成时状态（全绿）原样退回；全部退完后关闭谱面条并重现乐器候选气泡。
    /// </summary>
    private void PlayBeatSlotsExit(int version)
    {
        int firstHideIndex = -1;
        for (int index = 0; index < beatSlots.Length; index++)
        {
            if (beatSlots[index] != null)
            {
                firstHideIndex = index;
                break;
            }
        }

        if (firstHideIndex < 0)
        {
            CompleteBeatSlotsExit(version);
            return;
        }

        for (int index = 0; index < beatSlots.Length; index++)
        {
            int capturedIndex = index;
            beatSlots[index]?.PlayHide(
                (beatSlots.Length - 1 - index) * BeatSlotHideStagger,
                capturedIndex == firstHideIndex ? () => CompleteBeatSlotsExit(version) : null);
        }
    }

    private void CompleteBeatSlotsExit(int version)
    {
        if (version != flowVersion || isResolving || !isAwaitingReselection)
            return;

        if (directionStrip != null)
            directionStrip.SetActive(false);
        SetInstrumentBubblesVisible(isActorStandingOnStage);
    }

    private void BeginFailure()
    {
        if (isResolving)
            return;

        isResolving = true;
        ++flowVersion;
        level83Actor?.RestoreDefaultAnimation();
        FreezeGameplayInteraction();
        ShowRivalBubble(incorrectBubbleKey);
        rival?.PlayAngryThenCharge();
    }

    private void BeginPerformanceAfterInstrumentArrival(Level83Instrument instrument, int version)
    {
        if (version != flowVersion || isResolving || !performance.IsStarted)
            return;

        ConfigureBeatSlots();
        if (directionStrip != null)
            directionStrip.SetActive(true);
        PlayBeatSlotsEntrance(version);

        level83Actor?.PlayPerformanceAnimation(instrument);
        rival?.PlayWant();
    }

    /// 谱面槽错峰弹出：从最左格开始逐格 OutBack 弹出；全部弹出完成前谱面输入不生效，
    /// 完成后才开放输入并启动首格待输入引导。
    /// </summary>
    private void PlayBeatSlotsEntrance(int version)
    {
        int lastPopIndex = -1;
        for (int index = beatSlots.Length - 1; index >= 0; index--)
        {
            if (beatSlots[index] != null)
            {
                lastPopIndex = index;
                break;
            }
        }

        for (int index = 0; index < beatSlots.Length; index++)
        {
            int capturedIndex = index;
            beatSlots[index]?.PlayShow(
                index * BeatSlotShowStagger,
                capturedIndex == lastPopIndex ? () => CompleteBeatSlotsEntrance(version) : null);
        }
    }

    private void CompleteBeatSlotsEntrance(int version)
    {
        if (version != flowVersion || isResolving || !performance.IsStarted)
            return;

        isInstrumentReady = true;
        SetAwaitingBeat(0);
    }

    /// <summary>
    /// die 表现完成兜底：玩家始终未输入时结束庆祝循环并恢复通用动画；
    /// 钥匙已在命中同帧爆出，移动输入开关也已在命中时恢复，此处调用幂等。
    /// 玩家已捡钥匙进门通关时不再触碰输入开关，避免打断门触发通关链。
    /// </summary>
    private void HandleRivalDieCompleted()
    {
        if (!isResolving || levelCtrl == null || levelCtrl.IsSuccess())
            return;

        isCelebrating = false;
        ReleasePlayerControl();
    }

    /// <summary>
    /// 命中同帧恢复移动与跳跃输入开关，让玩家可在庆祝循环期间随时打断庆祝；
    /// 必须配对 ResumeMoving 解除演奏期间 StopMoving 关闭的 canMove。
    /// </summary>
    private void ReleaseMovementForCelebrationInterrupt()
    {
        actor?.ResumeMoving();
        levelCtrl?.SetCtrlable(true);
    }

    /// <summary>解除演奏锁定、恢复主角通用移动动画并重新开放移动输入；
    /// 必须配对 ResumeMoving 解除演奏期间 StopMoving 关闭的 canMove，否则输入事件恢复后主角仍无法移动。</summary>
    private void ReleasePlayerControl()
    {
        level83Actor?.RestoreDefaultAnimation();
        actor?.ResumeMoving();
        levelCtrl?.SetCtrlable(true);
    }

    /// <summary>
    /// 命中同帧把预置钥匙激活并从 Box Bad 位置抛物爆出到落点锚点；
    /// 参数对齐 ItemJumpOutExecutor，落地后由钥匙自带浮动循环接管。
    /// </summary>
    private void DropKeyFromRival()
    {
        if (dropKey == null || keyLandingPoint == null || rival == null)
        {
            this.LogWarning("[Level83GameplayCtrl] 预置钥匙、落点锚点或 Box Bad 未绑定，无法爆出钥匙。");
            return;
        }

        keyJumpTween?.Kill();
        Transform keyTransform = dropKey.transform;
        Vector3 startPosition = rival.transform.position;
        Vector3 landPosition = keyLandingPoint.position;
        keyTransform.position = startPosition;
        if (dropKeyFloat != null)
            dropKeyFloat.enabled = false;
        dropKey.SetActive(true);

        float duration = Mathf.Max(0.01f, UnityEngine.Random.Range(keyJumpDurationRange.x, keyJumpDurationRange.y));
        float peakHeight = Mathf.Max(0f, UnityEngine.Random.Range(keyJumpHeightRange.x, keyJumpHeightRange.y));
        float progress = 0f;
        keyJumpTween = DOTween.To(() => progress, value => progress = value, 1f, duration)
            .SetEase(Ease.Linear)
            .OnUpdate(() =>
            {
                if (keyTransform == null)
                    return;

                float arc = 4f * peakHeight * progress * (1f - progress);
                keyTransform.position = new Vector3(
                    Mathf.Lerp(startPosition.x, landPosition.x, progress),
                    Mathf.Lerp(startPosition.y, landPosition.y, progress) + arc,
                    Mathf.Lerp(startPosition.z, landPosition.z, progress));
            })
            .OnComplete(() =>
            {
                if (keyTransform != null)
                    keyTransform.position = landPosition;
                if (dropKeyFloat != null)
                    dropKeyFloat.enabled = true;
            });
    }

    private void FreezeGameplayInteraction()
    {
        levelCtrl?.SetCtrlable(false);
        actor?.StopMoving();
        DisableNoteAndOptionInteraction();
    }

    /// <summary>
    /// 关闭乐器候选与场上音符交互；不涉及移动输入开关，
    /// 命中胜利链需要保持移动可用以供打断庆祝。
    /// </summary>
    private void DisableNoteAndOptionInteraction()
    {
        SetInstrumentOptionsSelectable(false);
        foreach (Level83MusicNote activeNote in activeNotes)
            activeNote?.SetInteractionEnabled(false);
    }

    #endregion

    #region Direction Strip Feedback

    private void StopDirectionStripFeedback(bool restoreSlotDirections)
    {
        if (beatSlots == null)
            return;

        foreach (Level83BeatSlot slot in beatSlots)
        {
            // Unity 已销毁对象必须通过重载的判空跳过，不能使用 ?.。
            if (slot == null)
                continue;

            if (restoreSlotDirections)
                slot.RestoreOriginalTransform();
            else
                slot.StopFeedback();
        }
    }

    private void SetAwaitingBeat(int beatIndex)
    {
        if (beatIndex < 0 || beatIndex >= beatSlots.Length)
            return;

        beatSlots[beatIndex]?.SetAwaiting();
    }

    #endregion

    #region Notes

    private void SpawnMusicNote()
    {
        Level83MusicNote template = GetNoteTemplate(performance.Instrument);
        if (template == null || noteEmissionAnchor == null || notesRoot == null || actor == null)
        {
            this.LogWarning("[Level83GameplayCtrl] 实体音符模板、发声锚点或主角未绑定。");
            return;
        }

        float facingDirection = actor.FaingRight ? 1f : -1f;
        Vector2 laneOffset = NoteArrivalLaneOffsets[noteEmissionIndex % NoteArrivalLaneOffsets.Length];
        Vector2 mouthOffset = GetEmissionOffset(performance.Instrument);
        Vector3 emissionPosition = noteEmissionAnchor.position + new Vector3(
            facingDirection * mouthOffset.x,
            mouthOffset.y,
            0f);
        Vector3 arrivalPosition = emissionPosition + new Vector3(
            facingDirection * (noteForwardDistance + laneOffset.x),
            noteUpwardDistance + laneOffset.y,
            0f);
        noteEmissionIndex++;

        Level83MusicNote note = Instantiate(template, emissionPosition, Quaternion.identity, notesRoot);
        note.gameObject.SetActive(true);
        note.Initialize(
            this,
            gameplayCamera,
            rival?.GetHitCollider(),
            arrivalPosition,
            noteEmissionDuration);
        activeNotes.Add(note);
        activeNotes.RemoveAll(item => item == null);
    }

    /// <summary>返回当前乐器发声口相对手部锚点的偏移，与乐器音符映射同为一一对应。</summary>
    private Vector2 GetEmissionOffset(Level83Instrument instrument)
    {
        return instrument switch
        {
            Level83Instrument.Guitar => guitarEmissionOffset,
            Level83Instrument.Harp => harpEmissionOffset,
            Level83Instrument.Saxophone => saxophoneEmissionOffset,
            _ => Vector2.zero
        };
    }

    private Level83MusicNote GetNoteTemplate(Level83Instrument instrument)
    {
        return instrument switch
        {
            Level83Instrument.Guitar => guitarNoteTemplate,
            Level83Instrument.Harp => harpNoteTemplate,
            Level83Instrument.Saxophone => saxophoneNoteTemplate,
            _ => null
        };
    }

    private void SetNoteTemplateActive(bool active)
    {
        if (guitarNoteTemplate != null)
            guitarNoteTemplate.gameObject.SetActive(active);
        if (harpNoteTemplate != null)
            harpNoteTemplate.gameObject.SetActive(active);
        if (saxophoneNoteTemplate != null)
            saxophoneNoteTemplate.gameObject.SetActive(active);

        if (notesRoot != null)
            notesRoot.gameObject.SetActive(true);
    }

    #endregion

    #region Visual Helpers

    private HintBubble ShowRivalBubble(string text)
    {
        HintBubble template = Resources.Load<HintBubble>(HintBubbleResourcePath);
        if (template == null || rival == null)
        {
            this.LogWarning("[Level83GameplayCtrl] Box Bad 文字气泡模板或跟随目标未绑定。");
            return null;
        }

        activeRivalBubble = Instantiate(template, transform);
        activeRivalBubble.Show(text, rival.transform, RivalBubbleShowDuration);
        return activeRivalBubble;
    }

    private void RemoveActiveRivalBubble()
    {
        if (activeRivalBubble == null)
            return;

        activeRivalBubble.gameObject.SetActive(false);
        Destroy(activeRivalBubble.gameObject);
        activeRivalBubble = null;
    }

    /// <summary>
    /// 播放当前乐器对应的音效 tag；每个 tag 挂该乐器的全部 clip，由 SoundsGood 默认随机选 clip。
    /// </summary>
    private void PlayInstrumentSfx()
    {
        AudioManager.PlaySound(GetInstrumentSfxTag(performance.Instrument));
    }

    private static string GetInstrumentSfxTag(Level83Instrument instrument)
    {
        return instrument switch
        {
            Level83Instrument.Guitar => GuitarSfxTag,
            Level83Instrument.Harp => HarpSfxTag,
            Level83Instrument.Saxophone => SaxophoneSfxTag,
            _ => null
        };
    }

    private void ConfigureBeatSlots()
    {
        ReadOnlySpan<Level83PerformanceInput> pattern = performance.GetCurrentPattern();
        int count = Mathf.Min(pattern.Length, beatSlots.Length);
        for (int index = 0; index < count; index++)
            beatSlots[index]?.Configure(pattern[index]);
    }

    private void SetInstrumentBubblesVisible(bool visible, Level83InstrumentOption selectedOption = null)
    {
        if (instrumentBubbles == null)
            return;

        Level83InstrumentOption[] options =
            instrumentBubbles.GetComponentsInChildren<Level83InstrumentOption>(true);

        SetInstrumentOptionsSelectable(false);
        if (!visible)
        {
            if (!instrumentBubbles.activeSelf)
            {
                foreach (Level83InstrumentOption option in options)
                    option?.HideImmediately(option == selectedOption);

                instrumentBubbles.SetActive(false);
                return;
            }

            // 落选气泡消失：收回是弹出的倒放镜像，后出现的先消失（反向错峰）；
            // 容器延迟到最后一个缩小完成后才关闭，让收回动效完整可见。
            int dismissableCount = 0;
            foreach (Level83InstrumentOption option in options)
            {
                if (option != null && option != selectedOption)
                    dismissableCount++;
            }

            if (dismissableCount == 0)
            {
                instrumentBubbles.SetActive(false);
                return;
            }

            int remaining = dismissableCount;
            int dismissIndex = 0;
            foreach (Level83InstrumentOption option in options)
            {
                if (option == null || option == selectedOption)
                    continue;

                float delay = (dismissableCount - 1 - dismissIndex) * InstrumentBubbleDismissStagger;
                dismissIndex++;
                option.PlayDismiss(delay, () =>
                {
                    remaining--;
                    if (remaining <= 0 && instrumentBubbles != null)
                        instrumentBubbles.SetActive(false);
                });
            }
            return;
        }

        instrumentBubbles.SetActive(true);
        for (int index = 0; index < options.Length; index++)
            options[index]?.PlayShow(index * InstrumentBubbleShowStagger);

        SetInstrumentOptionsSelectable((!performance.IsStarted || isAwaitingReselection) && !isResolving);
    }

    private void SetInstrumentOptionsSelectable(bool selectable)
    {
        if (instrumentBubbles == null)
            return;

        foreach (Level83InstrumentOption option in instrumentBubbles.GetComponentsInChildren<Level83InstrumentOption>(true))
            option.SetSelectable(selectable);
    }

    #endregion

    #region Reset

    private void OnGameReset(GameResetEvent evt)
    {
        if (this == null || !isActiveAndEnabled)
            return;

        ++flowVersion;
        isResolving = true;
        isCelebrating = false;
        // 失败收口时谱面格必须保持结束瞬间的方向与 Sprite 直到关卡销毁：
        // GameResetEvent 比 PlayCtrl 销毁关卡早约 0.5s 到达，若在此恢复 Prefab
        // 原始方向，玩家会在该窗口内看到八格音符全部变回默认朝向；因此只停止
        // Tween 并恢复缩放/位置，不还原各格方向。关卡随即销毁重建，无需复用清理。
        StopDirectionStripFeedback(false);
        RemoveActiveRivalBubble();
        if (level83Actor != null)
            level83Actor.RestoreDefaultAnimation();
        isAwaitingReselection = false;
        selectedInstrumentOption = null;
    }

    private void OnGameReplay(GameReplayEvent evt)
    {
        if (this == null || !isActiveAndEnabled)
            return;

        ++flowVersion;
        isResolving = true;
        isCelebrating = false;
        StopDirectionStripFeedback(true);
        RemoveActiveRivalBubble();
        if (level83Actor != null)
            level83Actor.RestoreDefaultAnimation();
        isAwaitingReselection = false;
        selectedInstrumentOption = null;
    }

    #endregion
}
