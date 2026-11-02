using DG.Tweening;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;

/// <summary>
/// Level110 的算式、食物交互和出口解锁总控。
/// </summary>
public sealed class Level110Ctrl : MonoBehaviour, ILevelTipsWarningIntervalProvider
{
    private static readonly int[] AnswerDigits = { 4, 5, 0 };

    #region Inspector

    [Header("Level References")]
    [Tooltip("本关基础控制器；只复用标准出口和通关流程。")]
    [SerializeField] private BaseLevelCtrl levelCtrl;

    [Tooltip("本关角色；用于校验顶块碰撞和播放吃东西动画。")]
    [SerializeField] private Level110ActorCtrl actor;

    [Tooltip("角色的实体碰撞体；用于判定草莓是否被拖到角色身上。")]
    [SerializeField] private Collider2D actorCollider;

    [Tooltip("三个问号数字块，按百位、十位、个位顺序填写。")]
    [SerializeField] private Level110QuestionDigit[] questionDigits;

    [Header("Door Unlock")]
    [Tooltip("原 DoorLock 的标准事件入口；450 时复用原解锁动画、音效和回收链。")]
    [SerializeField] private EventHandleListener doorUnlockHandle;

    [Tooltip("正常出口门的触发碰撞体；解锁前关闭，450 时开启。")]
    [SerializeField] private BoxCollider2D exitDoorCollider;

    [Header("Door Focus")]
    [Tooltip("本关 GameCamera；为空时运行时从关卡根节点下查找。")]
    [SerializeField] private Camera levelCamera;

    [Tooltip("需要短暂聚焦的 DoorLock 根节点；优先使用其 Collider2D 中心作为构图目标。")]
    [SerializeField] private Transform doorLockFocusTarget;

    [Tooltip("密码正确时播放的既有音效 Tag。")]
    [SerializeField] private string correctPasswordSfxTag = "passwordCorrect";

    [Tooltip("原镜头中心向门锁中心靠近的比例；小于 1 可保留部分原构图。")]
    [SerializeField, Range(0f, 1f)] private float lockFocusPositionBlend = 0.75f;

    [Tooltip("聚焦时正交尺寸相对当前尺寸的比例；越小越近。")]
    [SerializeField, Range(0.5f, 1f)] private float lockFocusZoomRatio = 0.88f;

    [Tooltip("镜头移向门锁的持续时间。")]
    [SerializeField, Min(0.05f)] private float lockFocusMoveDuration = 0.28f;

    [Tooltip("DoorLock 解锁链启动后保持特写的时间，覆盖当前 unlock 动画。")]
    [SerializeField, Min(0f)] private float unlockFocusHoldDuration = 1.05f;

    [Tooltip("门锁解开后镜头恢复的持续时间。")]
    [SerializeField, Min(0.05f)] private float lockFocusRestoreDuration = 0.3f;

    [Header("Strawberry Equation")]
    [Tooltip("右侧草莓前的加号文本；吃掉后与草莓一起隐藏。")]
    [SerializeField] private TMP_Text strawberryTermText;

    [Tooltip("右侧算式结果文本；吃掉草莓后从 1250 改为 1000。")]
    [SerializeField] private TMP_Text rightResultText;

    [Tooltip("-250 从原结果位置向上漂浮的世界距离。")]
    [SerializeField, Min(0.05f)] private float reductionFloatDistance = 0.75f;

    [Tooltip("-250 上浮并渐隐的持续时间。")]
    [SerializeField, Min(0.05f)] private float reductionFloatDuration = 0.75f;

    [Tooltip("草莓靠近角色碰撞体后可被吃掉的额外容差。")]
    [SerializeField, Min(0f)] private float eatDropPadding = 0.35f;

    [Tooltip("草莓飞向嘴部并缩小、淡出的持续时间。")]
    [SerializeField, Min(0.05f)] private float strawberryConsumeDuration = 0.42f;

    [Tooltip("eat 动画无法播放时，恢复交互并刷新算式的兜底等待。")]
    [SerializeField, Min(0.05f)] private float eatFallbackDuration = 0.45f;

    [Header("Tips")]
    [Tooltip("本关提示按钮气泡再次出现的等待秒数。")]
    [SerializeField, Min(0.1f)] private float tipsWarningIntervalSeconds = 30f;

    #endregion

    #region Runtime State

    private readonly int[] currentDigits = { -1, -1, -1 };
    private Sequence doorFocusSequence;
    private Tween eatFallbackTween;
    private Sequence reductionFloatSequence;
    private TMP_Text floatingReductionText;
    private Vector3 cameraRestorePosition;
    private float cameraRestoreSize;
    private bool hasCameraRestoreState;
    private bool isAnswerAccepted;
    private bool isDoorUnlocked;
    private bool isEating;
    private bool hasEatenStrawberry;

    /// <summary>
    /// 当前右侧出口是否已经由答案 450 解锁。
    /// </summary>
    public bool IsDoorUnlocked => isDoorUnlocked;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (actorCollider == null && actor != null)
            actorCollider = actor.GetComponent<Collider2D>();

        if (levelCamera == null)
        {
            Transform cameraTransform = transform.Find("GameCamera");
            levelCamera = cameraTransform != null
                ? cameraTransform.GetComponent<Camera>()
                : Camera.main;
        }

        if (exitDoorCollider != null)
            exitDoorCollider.enabled = false;
    }

    private void OnDisable()
    {
        eatFallbackTween?.Kill();
        eatFallbackTween = null;

        reductionFloatSequence?.Kill();
        reductionFloatSequence = null;

        doorFocusSequence?.Kill();
        doorFocusSequence = null;

        if (!Application.isPlaying)
            return;

        RestoreCameraImmediately();
        if (floatingReductionText != null)
            Destroy(floatingReductionText.gameObject);
        floatingReductionText = null;
    }

    #endregion

    #region Question Digits And Door

    /// <summary>
    /// 接收问号块滚动完成后的数字，并在三个位置组成 450 时解除出口锁。
    /// 调用边界：只允许由本关绑定的三个 Level110QuestionDigit 调用。
    /// 注意事项：草莓是否被吃掉不参与答案判定，也不会阻止解锁。
    /// </summary>
    /// <param name="digitIndex">数字位置，0/1/2 分别表示百位、十位、个位。</param>
    /// <param name="value">当前位置滚动后的 0 到 9。</param>
    public void OnDigitChanged(int digitIndex, int value)
    {
        if (isAnswerAccepted || digitIndex < 0 || digitIndex >= currentDigits.Length)
            return;

        currentDigits[digitIndex] = Mathf.Clamp(value, 0, 9);
        if (!MatchesAnswer())
            return;

        BeginCorrectAnswerSequence();
    }

    /// <summary>
    /// 校验碰撞来源是否为本关实际受控角色，避免其它刚体触发问号块。
    /// </summary>
    public bool IsControlledActor(ActorCtrl candidate)
    {
        return candidate != null && candidate == actor;
    }

    private bool MatchesAnswer()
    {
        for (int i = 0; i < AnswerDigits.Length; i++)
        {
            if (currentDigits[i] != AnswerDigits[i])
                return false;
        }

        return true;
    }

    private void BeginCorrectAnswerSequence()
    {
        if (isAnswerAccepted)
            return;

        isAnswerAccepted = true;

        if (questionDigits != null)
        {
            for (int i = 0; i < questionDigits.Length; i++)
                questionDigits[i]?.SetAcceptHits(false);
        }

        if (!string.IsNullOrWhiteSpace(correctPasswordSfxTag))
            AudioManager.PlaySound(correctPasswordSfxTag);

        if (levelCamera == null || doorLockFocusTarget == null || !levelCamera.orthographic)
        {
            ExecuteDoorUnlock();
            return;
        }

        PlayDoorFocusSequence();
    }

    private void PlayDoorFocusSequence()
    {
        doorFocusSequence?.Kill();

        cameraRestorePosition = levelCamera.transform.position;
        cameraRestoreSize = levelCamera.orthographicSize;
        hasCameraRestoreState = true;

        Collider2D lockCollider = doorLockFocusTarget.GetComponent<Collider2D>();
        Vector3 lockCenter = lockCollider != null
            ? lockCollider.bounds.center
            : doorLockFocusTarget.position;
        lockCenter.z = cameraRestorePosition.z;

        Vector3 focusPosition = Vector3.Lerp(
            cameraRestorePosition,
            lockCenter,
            Mathf.Clamp01(lockFocusPositionBlend));
        float focusSize = cameraRestoreSize * Mathf.Clamp(lockFocusZoomRatio, 0.5f, 1f);
        float moveDuration = Mathf.Max(0.05f, lockFocusMoveDuration);
        float restoreDuration = Mathf.Max(0.05f, lockFocusRestoreDuration);

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(levelCamera.transform
            .DOMove(focusPosition, moveDuration)
            .SetEase(Ease.InOutSine));
        sequence.Join(levelCamera
            .DOOrthoSize(focusSize, moveDuration)
            .SetEase(Ease.InOutSine));
        sequence.AppendCallback(ExecuteDoorUnlock);
        sequence.AppendInterval(Mathf.Max(0f, unlockFocusHoldDuration));
        sequence.Append(levelCamera.transform
            .DOMove(cameraRestorePosition, restoreDuration)
            .SetEase(Ease.InOutSine));
        sequence.Join(levelCamera
            .DOOrthoSize(cameraRestoreSize, restoreDuration)
            .SetEase(Ease.InOutSine));
        sequence.OnComplete(() =>
        {
            doorFocusSequence = null;
            hasCameraRestoreState = false;
        });
        doorFocusSequence = sequence;
    }

    private void ExecuteDoorUnlock()
    {
        if (isDoorUnlocked)
            return;

        isDoorUnlocked = true;

        if (exitDoorCollider != null)
            exitDoorCollider.enabled = true;

        doorUnlockHandle?.Execute();
    }

    private void RestoreCameraImmediately()
    {
        if (!hasCameraRestoreState || levelCamera == null)
            return;

        levelCamera.transform.position = cameraRestorePosition;
        levelCamera.orthographicSize = cameraRestoreSize;
        hasCameraRestoreState = false;
    }

    #endregion

    #region Strawberry Flow

    /// <summary>
    /// 尝试接受拖到角色身上的草莓并启动吃东西表现。
    /// 调用边界：只有标记为草莓的 Level110DraggableItem 会调用；其它图标始终复位。
    /// 注意事项：吃草莓只改写右侧算式，不参与 450 的解锁条件。
    /// </summary>
    /// <param name="item">本次松手的草莓拖拽组件。</param>
    /// <returns>草莓命中角色且已开始消费流程时返回 true。</returns>
    public bool TryConsumeStrawberry(Level110DraggableItem item)
    {
        if (item == null || actor == null || actor.IsDied || hasEatenStrawberry || isEating ||
            (levelCtrl != null && levelCtrl.IsActorEnterDoor))
            return false;

        if (!IsDroppedOnActor(item.ItemRenderer))
            return false;

        hasEatenStrawberry = true;
        isEating = true;
        levelCtrl?.SetCtrlable(false);
        actor.StopMoving();

        Vector3 mouthPosition = actor.transform.position + Vector3.up * 0.85f;
        mouthPosition.z = item.transform.position.z;
        item.BeginConsume(mouthPosition, strawberryConsumeDuration);

        if (actor.TryPlayEat(CompleteStrawberryFlow))
            return true;

        eatFallbackTween?.Kill();
        eatFallbackTween = DOVirtual.DelayedCall(eatFallbackDuration, CompleteStrawberryFlow)
            .SetLink(gameObject);
        return true;
    }

    private bool IsDroppedOnActor(SpriteRenderer itemRenderer)
    {
        if (itemRenderer == null || actorCollider == null)
            return false;

        Bounds acceptanceBounds = actorCollider.bounds;
        float padding = Mathf.Max(0f, eatDropPadding) * 2f;
        acceptanceBounds.Expand(new Vector3(padding, padding, 1f));
        return acceptanceBounds.Intersects(itemRenderer.bounds);
    }

    private void CompleteStrawberryFlow()
    {
        if (!isEating)
            return;

        eatFallbackTween?.Kill();
        eatFallbackTween = null;
        isEating = false;

        PlayReductionFloat();

        if (strawberryTermText != null)
            strawberryTermText.gameObject.SetActive(false);
        if (rightResultText != null)
            rightResultText.text = "1000";

        if (actor != null && !actor.IsDied && (levelCtrl == null || !levelCtrl.IsActorEnterDoor))
        {
            actor.ResumeMoving();
            levelCtrl?.SetCtrlable(true);
        }
    }

    private void PlayReductionFloat()
    {
        if (rightResultText == null)
            return;

        reductionFloatSequence?.Kill();
        reductionFloatSequence = null;
        if (floatingReductionText != null)
            Destroy(floatingReductionText.gameObject);

        floatingReductionText = Instantiate(rightResultText, rightResultText.transform.parent);
        floatingReductionText.name = "Level110_ReductionFloat";
        floatingReductionText.text = "-250";
        floatingReductionText.raycastTarget = false;

        Transform floatingTransform = floatingReductionText.transform;
        floatingTransform.SetSiblingIndex(rightResultText.transform.GetSiblingIndex() + 1);
        Vector3 startLocalPosition = floatingTransform.localPosition;
        Vector3 startLocalScale = floatingTransform.localScale;
        float duration = Mathf.Max(0.05f, reductionFloatDuration);

        Sequence sequence = DOTween.Sequence().SetLink(floatingReductionText.gameObject);
        sequence.Join(floatingTransform
            .DOLocalMoveY(startLocalPosition.y + Mathf.Max(0.05f, reductionFloatDistance), duration)
            .SetEase(Ease.OutQuad));
        sequence.Join(floatingReductionText
            .DOFade(0f, duration)
            .SetEase(Ease.InQuad));
        sequence.Insert(0f, floatingTransform
            .DOScale(startLocalScale * 1.08f, Mathf.Min(0.16f, duration * 0.35f))
            .SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            if (floatingReductionText != null)
                Destroy(floatingReductionText.gameObject);
            floatingReductionText = null;
            reductionFloatSequence = null;
        });
        reductionFloatSequence = sequence;
    }

    #endregion

    #region Tips Interval

    /// <summary>
    /// 提供 Level110 的提示按钮气泡等待时间覆盖值。
    /// 调用边界：由主线 Playing 状态进入时读取一次；不写远端配置，不影响其它关卡。
    /// </summary>
    public bool TryGetTipsWarningInterval(out float seconds)
    {
        seconds = Mathf.Max(0.1f, tipsWarningIntervalSeconds);
        return true;
    }

    #endregion
}
