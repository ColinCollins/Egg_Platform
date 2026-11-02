using System;
using System.Collections.Generic;
using Bear.Logger;
using DG.Tweening;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;

/// <summary>
/// verison 2.0.0 新增需求
/// GameFinished 触发的时候，currentBlockCount 1s 快速逐渐增长到 maxBlockCount 数量并更新文本。
/// 数量更新完毕后，blockCOuntTxt 和 taskTips 隐藏，successTips 显示
/// </summary>
public class Level64Ctrl : MonoBehaviour, IDebuger
{
    private const int V200MaxBlockCount = 10000;
    private const float CompletionFeedbackDuration = 1f;
    private static readonly Vector3 CarryOffset = Vector3.up;
    [SerializeField] private float DragFinishMoveDuration = 0.35f;
    [SerializeField] private float aiInteractDistance = 0.5f;
    [SerializeField] private int visibleBlockExtraPoolCapacity = 4;

    [SerializeField] private Level64ActorCtrl actor;
    [SerializeField] private Level64AICtrl aiCtrl;

    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private ActorSpineCtrl doorSpineCtrl;
    [SerializeField] private Level64BlockCtrl blockCtrl;
    [SerializeField] private SpriteDragRotate dragRotate;
    [SerializeField] private TextMeshPro blockCountTxt;
    [SerializeField] private int maxBlockCount = V200MaxBlockCount;

    [SerializeField] private GameObject taskTips;
    [SerializeField] private GameObject successTips;

    private readonly Dictionary<Level64ActorCtrl, Level64BlockItem> _carriedItems = new();
    private readonly Dictionary<Level64ActorCtrl, Tween> _itemTweens = new();

    private int currentBlockCount;
    private bool isOver;
    private bool _isFinishingByDrag;
    private bool _isRotateMaxTriggered;
    private bool _isCompletionFeedbackRunning;
    private Tween _completionFeedbackTween;

    public bool IsFinished { get; private set; }

    /// <summary>
    /// 当 dragRotate 旋转角度达到或超过 MaxAngle 时触发，仅触发一次。
    /// </summary>
    public Action OnDragRotateReachedMax;

    private void Start()
    {
        ResolveTipsReferences();
        InitializeTipsState();

        if (dragRotate != null)
            dragRotate.OnAngleChanged += OnDragAngleChanged;

        InitializeBlockCtrl();
        RefreshLeftCountTxt();
    }

    private void OnDestroy()
    {
        blockCtrl?.GetOutputBlocksRoot()?.DOKill();
        KillCompletionFeedbackTween();
        KillAllItemTweens();

        if (dragRotate != null)
            dragRotate.OnAngleChanged -= OnDragAngleChanged;
    }

    private void OnDragAngleChanged(float angle)
    {
        if (_isRotateMaxTriggered || dragRotate == null)
            return;

        float maxAngle = dragRotate.GetMaxAngle();
        if (angle < maxAngle - 0.01f)
            return;

        _isRotateMaxTriggered = true;
        OnDragRotateReachedMax?.Invoke();
        OnDragToMaxRotateCallback();
    }

    public void ClickStartDoor()
    {
        aiCtrl?.ClickStartDoor();
    }

    public void OnPlayerEnterOutputArea(Collider2D collider)
    {
        TryPickupOutputForActor(ResolveActor(collider));
    }

    public void OnPlayerEnterInputArea(Collider2D collider)
    {
        TryDropInputForActor(ResolveActor(collider));
    }

    public bool TryPickupOutputForActor(Level64ActorCtrl targetActor)
    {
        if (targetActor == null || blockCtrl == null || IsActorBusy(targetActor) || IsActorCarrying(targetActor) || IsFinished || _isFinishingByDrag)
            return false;

        Level64BlockItem carriedItem = blockCtrl.OutputTriggerEnter();
        if (carriedItem == null)
            return false;

        _carriedItems[targetActor] = carriedItem;
        carriedItem.OffsetSortingGroupOrder(1);
        targetActor.SwitchLiftState(true);
        PlayPickupJump(targetActor, carriedItem);
        return true;
    }

    public bool TryDropInputForActor(Level64ActorCtrl targetActor)
    {
        if (targetActor == null || blockCtrl == null || IsActorBusy(targetActor) || IsFinished || GetLeftBlockCount() <= 0 || _isFinishingByDrag)
            return false;

        if (!TryGetCarriedItem(targetActor, out Level64BlockItem carriedItem))
            return false;

        Vector3 targetPosition = blockCtrl.GetInputAvailablePosition();
        PlayDropJump(targetActor, carriedItem, targetPosition);

        currentBlockCount++;
        RefreshLeftCountTxt();

        if (!isOver)
        {
            RefreshOver();
            blockCtrl.SetOver(isOver);
        }

        if (GetLeftBlockCount() <= 0)
            GameFinished();

        return true;
    }

    /// <summary>
    /// 拖拽达到最大角度时，驱动 output 整体移动到 input 区，结束后完成关卡。
    /// </summary>
    private void OnDragToMaxRotateCallback()
    {
        if (IsFinished || _isFinishingByDrag)
            return;

        _isFinishingByDrag = true;
        isOver = true;
        blockCtrl?.SetOver(true);

        Transform outputRoot = blockCtrl != null ? blockCtrl.GetOutputBlocksRoot() : null;
        Transform inputRoot = blockCtrl != null ? blockCtrl.GetInputBlocksRoot() : null;
        if (outputRoot == null || inputRoot == null)
        {
            GameFinished();
            return;
        }

        outputRoot.DOKill();
        outputRoot.DOMove(inputRoot.position, DragFinishMoveDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(GameFinished);
    }

    private void GameFinished()
    {
        if (IsFinished)
            return;

        _isFinishingByDrag = false;
        OpenDoor();
        IsFinished = true;
        StartCompletionFeedback();

        this.Log("Level64Ctrl game finished");
    }

    public bool IsActorCarrying(Level64ActorCtrl targetActor)
    {
        return TryGetCarriedItem(targetActor, out _);
    }

    public bool IsActorBusy(Level64ActorCtrl targetActor)
    {
        if (targetActor == null)
            return false;

        if (!_itemTweens.TryGetValue(targetActor, out Tween tween))
            return false;

        return tween != null && tween.IsActive();
    }

    public int GetLeftBlockCount()
    {
        return Mathf.Max(0, maxBlockCount - currentBlockCount);
    }

    public int GetMaxBlockCount()
    {
        return maxBlockCount;
    }

    public void RefreshOver()
    {
        if (blockCtrl == null)
            return;

        isOver = maxBlockCount - (currentBlockCount + blockCtrl.GetOutputCacheCount()) <= 0;
    }

    public bool IsActorInOutputArea(Level64ActorCtrl targetActor)
    {
        if (targetActor == null || blockCtrl == null || blockCtrl.GetOutputBlocksRoot() == null)
            return false;

        return targetActor.transform.position.x >= blockCtrl.GetOutputBlocksRoot().position.x - aiInteractDistance;
    }

    public bool IsActorInInputArea(Level64ActorCtrl targetActor)
    {
        if (targetActor == null || blockCtrl == null || blockCtrl.GetInputBlocksRoot() == null)
            return false;

        return targetActor.transform.position.x <= blockCtrl.GetInputBlocksRoot().position.x + aiInteractDistance;
    }

    public void CleanupActorCarry(Level64ActorCtrl targetActor)
    {
        if (targetActor == null)
            return;

        KillItemTween(targetActor);

        if (TryGetCarriedItem(targetActor, out Level64BlockItem carriedItem))
        {
            if (carriedItem != null)
            {
                carriedItem.transform.DOKill();
                carriedItem.RestoreSortingGroupOrder();
                blockCtrl?.RecycleBlockItem(carriedItem);
            }

            _carriedItems.Remove(targetActor);
        }

        targetActor.SwitchLiftState(false);
    }

    private void InitializeBlockCtrl()
    {
        if (blockCtrl == null)
            return;

        int configuredMaxPoolSize = blockCtrl.GetRecommendedPoolCapacity(visibleBlockExtraPoolCapacity);
        int configuredInitialPoolSize = configuredMaxPoolSize > 0
            ? Mathf.Min(blockCtrl.GetInitialOutputCapacity(), configuredMaxPoolSize)
            : 0;

        blockCtrl.ConfigurePool(configuredInitialPoolSize, configuredMaxPoolSize);
        blockCtrl.Initialize();
    }

    private void RefreshLeftCountTxt()
    {
        SetLeftCountText(GetLeftBlockCount());
    }

    private void SetLeftCountText(int leftCount)
    {
        if (blockCountTxt != null)
            blockCountTxt.text = "x" + Mathf.Max(0, leftCount).ToString();
    }

    private void StartCompletionFeedback()
    {
        if (_isCompletionFeedbackRunning)
            return;

        int startLeftCount = GetLeftBlockCount();
        if (startLeftCount <= 0 || blockCountTxt == null)
        {
            _isCompletionFeedbackRunning = true;
            CompleteCompletionFeedback();
            return;
        }

        KillCompletionFeedbackTween();
        _isCompletionFeedbackRunning = true;

        int displayedLeftCount = startLeftCount;
        _completionFeedbackTween = DOTween.To(
                () => displayedLeftCount,
                value =>
                {
                    displayedLeftCount = value;
                    SetLeftCountText(displayedLeftCount);
                },
                0,
                CompletionFeedbackDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(CompleteCompletionFeedback)
            .OnKill(() => _completionFeedbackTween = null);
    }

    private void CompleteCompletionFeedback()
    {
        SetLeftCountText(0);

        if (blockCountTxt != null)
            blockCountTxt.gameObject.SetActive(false);

        if (taskTips != null)
            taskTips.SetActive(false);

        if (successTips != null)
            successTips.SetActive(true);

        _isCompletionFeedbackRunning = false;
    }

    private void KillCompletionFeedbackTween()
    {
        if (_completionFeedbackTween != null && _completionFeedbackTween.IsActive())
            _completionFeedbackTween.Kill();

        _completionFeedbackTween = null;
        _isCompletionFeedbackRunning = false;
    }

    private void ResolveTipsReferences()
    {
        if (taskTips == null)
            taskTips = FindChildGameObjectByName("TaskTips");

        if (successTips == null)
            successTips = FindChildGameObjectByName("SuccessTips");
    }

    private void InitializeTipsState()
    {
        if (blockCountTxt != null)
            blockCountTxt.gameObject.SetActive(true);

        if (taskTips != null)
            taskTips.SetActive(true);

        if (successTips != null)
            successTips.SetActive(false);
    }

    private GameObject FindChildGameObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == objectName)
                return child.gameObject;
        }

        this.LogWarning($"[Level64Ctrl] Missing child reference: {objectName}");
        return null;
    }

    private void PlayPickupJump(Level64ActorCtrl targetActor, Level64BlockItem blockItem)
    {
        const float jumpPower = 1f;
        const float jumpDuration = 0.3f;

        Transform itemTransform = blockItem.transform;
        Vector3 startPosition = itemTransform.position;

        KillItemTween(targetActor);
        itemTransform.DOKill();
        itemTransform.SetParent(null, true);

        AudioManager.PlaySound("level64Block");

        Tween tween = null;
        tween = DOTween.To(() => 0f, progress =>
            {
                Vector3 targetPosition = GetCarryWorldPosition(targetActor);
                Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, progress);
                currentPosition.y += Mathf.Sin(progress * Mathf.PI) * jumpPower;
                itemTransform.position = currentPosition;
            }, 1f, jumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                ClearItemTween(targetActor, tween);
                AttachItemToActor(targetActor, blockItem);
            })
            .OnKill(() => ClearItemTween(targetActor, tween));

        _itemTweens[targetActor] = tween;
    }

    private void PlayDropJump(Level64ActorCtrl targetActor, Level64BlockItem blockItem, Vector3 targetPosition)
    {
        const float jumpPower = 1f;
        const float jumpDuration = 0.3f;

        Transform itemTransform = blockItem.transform;

        KillItemTween(targetActor);
        itemTransform.DOKill();
        itemTransform.SetParent(null, true);

        AudioManager.PlaySound("level64Block");

        Tween tween = null;
        tween = itemTransform.DOJump(targetPosition, jumpPower, 1, jumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                ClearItemTween(targetActor, tween);
                blockItem.RestoreSortingGroupOrder();
                blockCtrl.InputTriggerEnter(blockItem);
                targetActor.SwitchLiftState(false);
                _carriedItems.Remove(targetActor);
            })
            .OnKill(() => ClearItemTween(targetActor, tween));

        _itemTweens[targetActor] = tween;
    }

    private void AttachItemToActor(Level64ActorCtrl targetActor, Level64BlockItem blockItem)
    {
        if (targetActor == null || blockItem == null)
            return;

        blockItem.transform.SetParent(targetActor.transform, true);
        blockItem.transform.position = GetCarryWorldPosition(targetActor);
    }

    private Vector3 GetCarryWorldPosition(Level64ActorCtrl targetActor)
    {
        return targetActor.transform.position + CarryOffset;
    }

    private Level64ActorCtrl ResolveActor(Collider2D collider)
    {
        Level64ActorCtrl resolvedActor = collider != null ? collider.GetComponentInParent<Level64ActorCtrl>() : null;
        return resolvedActor != null ? resolvedActor : actor;
    }

    private bool TryGetCarriedItem(Level64ActorCtrl targetActor, out Level64BlockItem carriedItem)
    {
        carriedItem = null;
        if (targetActor == null)
            return false;

        if (!_carriedItems.TryGetValue(targetActor, out carriedItem))
            return false;

        if (carriedItem != null)
            return true;

        _carriedItems.Remove(targetActor);
        return false;
    }

    private void KillItemTween(Level64ActorCtrl targetActor)
    {
        if (targetActor == null)
            return;

        if (_itemTweens.TryGetValue(targetActor, out Tween tween) && tween != null && tween.IsActive())
            tween.Kill();

        _itemTweens.Remove(targetActor);
    }

    private void KillAllItemTweens()
    {
        var tweens = new List<Tween>(_itemTweens.Values);
        foreach (Tween tween in tweens)
        {
            if (tween != null && tween.IsActive())
                tween.Kill();
        }

        _itemTweens.Clear();
        _carriedItems.Clear();
    }

    private void ClearItemTween(Level64ActorCtrl targetActor, Tween expectedTween)
    {
        if (targetActor == null)
            return;

        if (!_itemTweens.TryGetValue(targetActor, out Tween tween))
            return;

        if (tween == expectedTween)
            _itemTweens.Remove(targetActor);
    }

    private void OpenDoor()
    {
        if (doorCollider != null)
            doorCollider.enabled = true;
        else
            this.LogWarning("[Level64Ctrl] doorCollider is missing.");

        if (doorSpineCtrl != null)
            doorSpineCtrl.PlayAnimation("door_open", false);
        else
            this.LogWarning("[Level64Ctrl] doorSpineCtrl is missing.");

        AudioManager.PlaySound("openDoor");
    }
}
