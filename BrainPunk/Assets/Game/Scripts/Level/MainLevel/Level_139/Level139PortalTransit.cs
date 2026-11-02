using System;
using System.Threading;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 本关楼层门衔接：玩家入区后原位渐隐，不旋转、缩小或播放吸入漩涡，
/// 后续出门反向渐显并自动走出黑门洞；只有首次出生由共享 StartDoor 负责。
/// </summary>
public sealed class Level139PortalTransit : MonoBehaviour, IEventSender
{
    private const float PortalFadeSeconds = 0.4f;
    private BaseLevelCtrl level;
    private Level139ActorCtrl player;
    private Func<bool> ownerActive;
    private CancellationTokenSource cancellation;
    private Tween entryFade;
    private Tween cameraTravel;
    private EnterDoorAnimCtrl exitClone;
    private Transform destination;
    private bool destinationWasActive;
    private bool paused;
    private Spine.TrackEntry portalMotion;

    public bool IsBusy { get; private set; }

    /// <summary>响应本关暂停状态，冻结私有镜头、渐隐、门动画及等待，不修改全局时间缩放。</summary>
    public void SetPaused(bool value)
    {
        if (paused == value)
            return;
        paused = value;
        if (value)
        {
            entryFade?.Pause();
            cameraTravel?.Pause();
        }
        else
        {
            entryFade?.Play();
            cameraTravel?.Play();
        }
        if (portalMotion != null)
            portalMotion.TimeScale = value ? 0f : 1f;
    }

    private async UniTask WaitForActiveSeconds(float seconds, CancellationToken token)
    {
        // 游戏暂停仍保留 Time.timeScale=1，只累计本关处于运行状态的时间。
        float elapsed = 0f;
        while (elapsed < seconds || paused)
        {
            await UniTask.Yield(cancellationToken: token);
            if (!paused)
                elapsed += Time.deltaTime;
        }
    }

    public void Initialize(BaseLevelCtrl owner, Level139ActorCtrl target, Func<bool> isOwnerActive)
    {
        level = owner;
        player = target;
        ownerActive = isOwnerActive;
    }

    public bool TryPlay(Transform sourceDoor, Transform targetDoor,
        Camera camera, float targetCameraY, float cameraTravelSeconds, Action arrive, Action rollback)
    {
        if (IsBusy || !isActiveAndEnabled || player == null || player.IsDied)
            return false;
        ActorSpineCtrl doorSpine = sourceDoor != null ? sourceDoor.GetComponent<ActorSpineCtrl>() : null;
        EnterDoorAnimCtrl targetExit = targetDoor != null ? targetDoor.GetComponent<EnterDoorAnimCtrl>() : null;
        if (doorSpine == null || targetExit == null || camera == null)
        {
            Debug.LogError("[Level139] 楼层门缺少相机、原门 Spine 或共享 StartDoor 出场组件。", this);
            return false;
        }
        IsBusy = true;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);
        Run(doorSpine, targetExit, camera, targetCameraY,
            cameraTravelSeconds, arrive, rollback, cancellation.Token).Forget();
        return true;
    }

    private async UniTaskVoid Run(ActorSpineCtrl sourceSpine,
        EnterDoorAnimCtrl targetExit, Camera camera, float targetCameraY,
        float cameraTravelSeconds, Action arrive, Action rollback,
        CancellationToken token)
    {
        Vector3 originalPosition = player.transform.position;
        Vector3 originalScale = player.transform.localScale;
        Quaternion originalRotation = player.transform.localRotation;
        bool originalFacingRight = player.FaingRight;
        Rigidbody2D body = player.RB;
        bool wasSimulated = body != null && body.simulated;
        bool wasCtrlable = level.ActorCtrlable;
        GameObject shadow = player.transform.Find("Shadow")?.gameObject;
        bool shadowWasActive = shadow != null && shadow.activeSelf;
        SpriteRendererGroupAlpha alphaGroup = player.GetComponentInChildren<SpriteRendererGroupAlpha>(true);
        float groupAlpha = alphaGroup != null ? alphaGroup.GetAlpha() : 1f;
        Vector3 originalCameraPosition = camera.transform.position;
        bool travelStarted = false;
        bool finished = false;
        destination = targetExit.transform;
        destinationWasActive = destination.gameObject.activeSelf;

        try
        {
            level.SetCtrlable(false);
            player.SetPortalTransit(true, false);
            player.SpineCtrl?.PlayAnimation("idle", true, false);
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }
            alphaGroup?.CollectRenderers();
            this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

            // 源门已由玩家点击并完全打开，不重播开门或再次等待开门动画；
            // 渐隐进入黑洞不再播放 enterDoor，入场只保留开门与关门音效。
            if (shadow != null)
                shadow.SetActive(false);
            // 玩家已自行走入门口；仅改变身体与饰品透明度，不再吸向门心。
            float entryAlpha = groupAlpha;
            bool fadeFinished = false;
            entryFade = DOTween.To(() => entryAlpha, value =>
            {
                entryAlpha = value;
                SetAlpha(value, alphaGroup);
            }, 0f, PortalFadeSeconds).SetEase(Ease.Linear).SetLink(gameObject)
                .OnComplete(() => fadeFinished = true);
            while (entryFade != null && entryFade.IsActive() && !entryFade.IsComplete())
                await UniTask.Yield(cancellationToken: token);
            token.ThrowIfCancellationRequested();
            if (!fadeFinished)
                throw new OperationCanceledException("Level139 entry fade interrupted.");
            entryFade = null;
            SetAlpha(0f, alphaGroup);

            await WaitForActiveSeconds(0.2f, token);
            var closing = portalMotion = sourceSpine.PlayAnimation("dose_close", false, false);
            float closeDuration = closing != null && closing.Animation != null ? closing.Animation.Duration : 0.25f;
            await WaitForActiveSeconds(0.25f, token);
            AudioManager.PlaySound("closeDoor");
            if (closeDuration > 0.25f)
                await WaitForActiveSeconds(closeDuration - 0.25f, token);
            token.ThrowIfCancellationRequested();
            sourceSpine.GetComponent<Level139PortalVisual>()?.UseSharedVisuals();

            // 玩家保持隐藏，只移动相机 Y，不改变 X/Z/缩放；中途取消也要恢复源楼层。
            travelStarted = true;
            bool cameraArrived = false;
            cameraTravel = camera.transform.DOMoveY(targetCameraY, Mathf.Max(0.01f, cameraTravelSeconds))
                .SetEase(Ease.Linear).SetLink(gameObject).OnComplete(() => cameraArrived = true);
            while (cameraTravel != null && cameraTravel.IsActive() && !cameraTravel.IsComplete())
                await UniTask.Yield(cancellationToken: token);
            token.ThrowIfCancellationRequested();
            if (!cameraArrived)
                throw new OperationCanceledException("Level139 camera travel interrupted.");
            cameraTravel = null;
            arrive?.Invoke();

            // 首次出生仍由 BaseLevelCtrl 走共享 Play。本关往返副本改为黑洞渐显+自动走出。
            // 清除源实例的临时网格后再复制，副本独立持有自己的黑门洞并可统一回收。
            targetExit.GetComponent<Level139PortalVisual>()?.UseSharedVisuals();
            exitClone = Instantiate(targetExit, targetExit.transform.parent);
            exitClone.gameObject.name = "Level139_PortalExit";
            destination.gameObject.SetActive(false);
            player.transform.localScale = originalScale;
            player.transform.localRotation = originalRotation;
            if (body != null)
                body.simulated = wasSimulated;
            SetAlpha(0f, alphaGroup);
            player.SetPortalTransit(true, true);
            await PlayBlackExit(alphaGroup, groupAlpha, shadow, shadowWasActive, token);
            token.ThrowIfCancellationRequested();
            finished = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            entryFade?.Kill();
            entryFade = null;
            cameraTravel?.Kill();
            cameraTravel = null;
            if (portalMotion != null)
                portalMotion.TimeScale = 1f;
            portalMotion = null;
            RemoveExitClone();
            bool ownerStillActive = ownerActive != null && ownerActive();
            if (!finished && travelStarted && ownerStillActive)
            {
                try
                {
                    // 只有取消恢复使用瞬时回到源姿态，正常路线始终使用上面的 Y Tween。
                    if (camera != null)
                        camera.transform.position = originalCameraPosition;
                    rollback?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
            if (player != null)
            {
                if (!player.IsDied)
                {
                    player.transform.localScale = originalScale;
                    if (!finished)
                    {
                        if (player.FaingRight != originalFacingRight)
                            player.SetFaceDir(originalFacingRight);
                        player.transform.localRotation = originalRotation;
                        if (ownerStillActive)
                            player.transform.position = originalPosition;
                    }
                    SetAlpha(groupAlpha, alphaGroup);
                    if (body != null)
                    {
                        body.linearVelocity = Vector2.zero;
                        body.angularVelocity = 0f;
                        body.position = player.transform.position;
                        body.simulated = wasSimulated;
                    }
                    if (shadow != null)
                        shadow.SetActive(shadowWasActive);
                }
                player.SetPortalTransit(false, false);
                player.SetMoveInput(0f);
            }
            if (!finished && sourceSpine != null)
            {
                sourceSpine.PlayAnimation("door_idle", true, false);
                sourceSpine.GetComponent<Level139PortalVisual>()?.UseSharedVisuals();
            }
            // 不让旧关卡清理回调重新解锁新关卡的面板。
            if (level != null && PlayCtrl.Instance != null && PlayCtrl.Instance.LevelCtrl == level)
            {
                bool canControl = ownerStillActive && player != null && !player.IsDied;
                bool resume = canControl && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
                // 暂停时恢复原控制资格但保持 StopMoving；Resume 事件即可恢复，不能永久锁住。
                level.SetCtrlable(wasCtrlable && canControl);
                if (resume)
                    player.ResumeMoving();
                else if (player != null)
                    player.StopMoving();
                this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
            }
            IsBusy = false;
            cancellation?.Dispose();
            cancellation = null;
        }
    }

    private async UniTask PlayBlackExit(SpriteRendererGroupAlpha alphaGroup, float targetAlpha,
        GameObject shadow, bool shadowWasActive, CancellationToken token)
    {
        var doorSpine = exitClone.GetComponent<ActorSpineCtrl>();
        var visual = exitClone.GetComponent<Level139PortalVisual>();
        if (doorSpine == null || visual == null || !visual.TryUseBlackInterior())
            throw new InvalidOperationException("Level139 换层出门缺少黑洞视觉或门动画。");

        player.StopMoving();
        player.SetMoveInput(0f);
        alphaGroup?.CollectRenderers();
        SetAlpha(0f, alphaGroup);
        if (shadow != null)
            shadow.SetActive(false);
        var opening = portalMotion = doorSpine.PlayAnimation("door_open", false, false);
        if (opening?.Animation == null)
            throw new InvalidOperationException("Level139 换层出门缺少 door_open 动画。");
        AudioManager.PlaySound("openDoor");
        await WaitForActiveSeconds(Mathf.Max(0.4f, opening.Animation.Duration), token);

        // 两层门的现有出场方向均向右；沿 Actor 正常移动接口走出，不用 Transform 推移。
        player.ResumeMoving();
        player.SetMoveInput(1f);
        player.UpdateAnimation();
        float elapsed = 0f;
        while (elapsed < PortalFadeSeconds || paused)
        {
            token.ThrowIfCancellationRequested();
            if (paused)
            {
                await UniTask.Yield(cancellationToken: token);
                continue;
            }
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / PortalFadeSeconds);
            SetAlpha(targetAlpha * progress, alphaGroup);
            if (shadow != null)
                shadow.SetActive(shadowWasActive && progress >= 0.5f);
            await UniTask.Yield(cancellationToken: token);
        }
        token.ThrowIfCancellationRequested();
        SetAlpha(targetAlpha, alphaGroup);
        player.SetMoveInput(0f);
        player.UpdateAnimation();
        player.StopMoving();

        await WaitForActiveSeconds(0.2f, token);
        var closing = portalMotion = doorSpine.PlayAnimation("dose_close", false, false);
        if (closing?.Animation == null)
            throw new InvalidOperationException("Level139 换层出门缺少 dose_close 动画。");
        await WaitForActiveSeconds(0.25f, token);
        AudioManager.PlaySound("closeDoor");
        if (closing.Animation.Duration > 0.25f)
            await WaitForActiveSeconds(closing.Animation.Duration - 0.25f, token);
        token.ThrowIfCancellationRequested();
        // Run 的统一 finally 回收副本、恢复透明度/物理/输入；关门完成前不释放操作。
    }

    private void SetAlpha(float value, SpriteRendererGroupAlpha group)
    {
        if (player != null)
            player.SpineCtrl?.SetAlpha(value);
        if (group != null)
            group.SetAlpha(value);
    }

    private void RemoveExitClone()
    {
        if (exitClone != null)
        {
            exitClone.gameObject.SetActive(false);
            Destroy(exitClone.gameObject);
            exitClone = null;
        }
        if (destination != null)
            destination.gameObject.SetActive(destinationWasActive);
        destination = null;
    }

    public void Cancel()
    {
        if (!IsBusy)
            return;
        cancellation?.Cancel();
        entryFade?.Kill();
        entryFade = null;
        cameraTravel?.Kill();
        cameraTravel = null;
        if (player != null)
            player.SetPortalTransit(true, false);
        RemoveExitClone();
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() => Cancel();
}
