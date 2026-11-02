using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level089 的 3-2-4-1 按钮顺序、石柱位移与失败复位控制器。
/// </summary>
public class Level89Ctrl : MonoBehaviour
{
    private const string StonePillarDropSfxTag = "l89StonePillarDrop";
    private const string IncorrectSequenceSfxTag = "passwordError";
    private static readonly int[] RequiredOrder = { 3, 2, 4, 1 };
    private const float FailureButtonPressHoldDuration = 0.18f;
    private const float FailureButtonReboundDuration = 0.18f;

    /// <summary>
    /// 石柱从初始位置到完全降下的总距离；四次正确按键均分此距离。
    /// </summary>
    [SerializeField] private float pillarDropDistance = 8f;

    /// <summary>
    /// 每次正确按键推动石柱下降的持续时间。
    /// </summary>
    [SerializeField] private float correctMoveDuration = 1.2f;

    /// <summary>
    /// 输入错误后石柱回到初始位置的快速复位时间。
    /// </summary>
    [SerializeField] private float resetMoveDuration = 0.35f;

    /// <summary>
    /// 第一次按错时只抖动石柱的持续时间。
    /// </summary>
    [SerializeField] private float firstFailureShakeDuration = 0.32f;

    /// <summary>
    /// 石柱子物体在演出期间的本地位置抖动幅度。
    /// </summary>
    [SerializeField] private float stoneShakeStrength = 0.08f;

    /// <summary>
    /// 本关石柱演出使用的镜头位置抖动幅度。
    /// </summary>
    [SerializeField] private float cameraShakeStrength = 0.12f;

    private ActorCtrl actor;
    private Transform stone;
    private Transform[] stonePieces;
    private Level89SequenceButton[] buttons;
    private Camera sceneCamera;
    private Vector3 initialStonePosition;
    private Sequence activeSequence;
    private Tween cameraShakeTween;
    private int correctPressCount;
    private bool isBusy;
    private bool actorLocked;

    private void Awake()
    {
        actor = GetComponentInChildren<ActorCtrl>(true);
        stone = transform.Find("stone");
        buttons = GetComponentsInChildren<Level89SequenceButton>(true);
        sceneCamera = Camera.main;

        if (stone == null)
        {
            Debug.LogWarning("[Level89Ctrl] stone transform is missing.");
            return;
        }

        initialStonePosition = stone.position;
        stonePieces = new Transform[stone.childCount];
        for (int index = 0; index < stone.childCount; index++)
            stonePieces[index] = stone.GetChild(index);
    }

    internal void OnSequenceButtonPressed(Level89SequenceButton button)
    {
        if (isBusy || button == null)
            return;

        if (correctPressCount >= RequiredOrder.Length || button.ButtonNumber != RequiredOrder[correctPressCount])
        {
            AudioManager.PlaySound(IncorrectSequenceSfxTag);
            if (correctPressCount == 0)
                PlayFirstFailure();
            else
                PlayResetFailure();
            return;
        }

        button.SetLocked(true);
        correctPressCount++;
        PlayCorrectStep();
    }

    private void PlayCorrectStep()
    {
        if (stone == null)
            return;

        LockActor();
        isBusy = true;
        KillActiveSequence();
        AudioManager.PlaySound(StonePillarDropSfxTag);

        float targetY = initialStonePosition.y - pillarDropDistance * correctPressCount / RequiredOrder.Length;
        activeSequence = DOTween.Sequence().SetLink(gameObject);
        activeSequence.Join(stone.DOMoveY(targetY, correctMoveDuration).SetEase(Ease.InOutSine));
        JoinStoneShake(activeSequence, correctMoveDuration, stoneShakeStrength);
        PlayCameraShake(correctMoveDuration);
        activeSequence.OnComplete(ReleaseAfterSequence);
    }

    private void PlayFirstFailure()
    {
        isBusy = true;
        KillActiveSequence();

        activeSequence = DOTween.Sequence().SetLink(gameObject);
        activeSequence.AppendInterval(FailureButtonPressHoldDuration);
        activeSequence.AppendCallback(ResetButtons);
        activeSequence.AppendInterval(FailureButtonReboundDuration);
        activeSequence.AppendCallback(LockActor);
        JoinStoneShake(activeSequence, firstFailureShakeDuration, stoneShakeStrength * 1.6f);
        activeSequence.OnComplete(ReleaseAfterSequence);
    }

    private void PlayResetFailure()
    {
        isBusy = true;
        LockActor();
        KillActiveSequence();

        activeSequence = DOTween.Sequence().SetLink(gameObject);
        activeSequence.AppendInterval(FailureButtonPressHoldDuration);
        activeSequence.AppendCallback(ResetButtons);
        activeSequence.Join(stone.DOMove(initialStonePosition, resetMoveDuration).SetEase(Ease.OutQuad));
        JoinStoneShake(activeSequence, resetMoveDuration, stoneShakeStrength * 1.4f);
        activeSequence.OnComplete(ReleaseAfterSequence);
    }

    private void ResetButtons()
    {
        correctPressCount = 0;
        if (buttons == null)
            return;

        for (int index = 0; index < buttons.Length; index++)
            buttons[index]?.SetReleasedFast();
    }

    private void JoinStoneShake(Sequence sequence, float duration, float strength)
    {
        if (sequence == null || stonePieces == null)
            return;

        for (int index = 0; index < stonePieces.Length; index++)
        {
            Transform piece = stonePieces[index];
            if (piece != null)
                sequence.Join(piece.DOShakePosition(duration, strength, 20, 90f, false, true));
        }
    }

    private void PlayCameraShake(float duration)
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;

        if (sceneCamera != null)
        {
            cameraShakeTween?.Kill();
            cameraShakeTween = sceneCamera.transform
                .DOShakePosition(duration, cameraShakeStrength, 20, 90f, false, true)
                .SetLink(gameObject);
        }
    }

    private void LockActor()
    {
        if (actor == null)
            return;

        actor.SetMoveInput(0f);
        actor.StopMoving();
        actorLocked = true;
    }

    private void ReleaseAfterSequence()
    {
        activeSequence = null;
        isBusy = false;
        if (actorLocked && actor != null)
            actor.ResumeMoving();

        actorLocked = false;
    }

    private void KillActiveSequence()
    {
        if (activeSequence != null && activeSequence.IsActive())
            activeSequence.Kill();

        activeSequence = null;
    }

    private void OnDisable()
    {
        KillActiveSequence();
        if (stone != null)
            stone.DOKill();

        if (stonePieces != null)
        {
            for (int index = 0; index < stonePieces.Length; index++)
                stonePieces[index]?.DOKill();
        }

        if (cameraShakeTween != null && cameraShakeTween.IsActive())
            cameraShakeTween.Kill();

        cameraShakeTween = null;

        if (actorLocked && actor != null)
            actor.ResumeMoving();

        actorLocked = false;
        isBusy = false;
    }
}
