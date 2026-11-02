using System;
using System.Threading;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ItemEvent;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEditor;
#endif

/// <summary>
/// IWanner 入口门触发动画控制器：播放关门动画，并让入口特效按配置渐隐。
/// 调用边界：作为 BaseItemEventHandle 交给 TriggerExecutor 执行，不负责玩家判定或关卡结算。
/// 成功流程边界：PlayAsync 额外控制 Actor 进门后的关门等待，以及 fade 完成后的切关等待。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannerGame_EnterDoorAnimCtrl : BaseItemEventHandle, IDebuger
{
    #region Constants

    private const string DoorCloseAnimationName = "close";

    #endregion

    #region References

    /// <summary>
    /// 门关闭动画节点；触发时会启用节点并从头播放 close 动画。
    /// </summary>
    [Tooltip("门关闭动画节点；触发时启用并从头播放 close 动画")]
    [SerializeField] private ActorSpineCtrl doorCloseSpine;

    /// <summary>
    /// 需要渐隐的门口特效节点；Reset 时恢复为完全不透明。
    /// </summary>
    [Tooltip("触发后需要渐隐的门口特效节点")]
    [SerializeField] private ActorSpineCtrl effectSpine;

    #endregion

    #region Sequence Timing Settings

    /// <summary>
    /// Actor 已进入门后，到开始播放关门动画之间的等待时间。
    /// </summary>
    [Tooltip("Actor 进入门后，等待多少秒开始播放关门动画")]
    [Min(0f)]
    [SerializeField] private float doorCloseDelayAfterActorEntered = 0.3f;

    /// <summary>
    /// 播放关门动画后，等待多少秒开始渐隐特效。
    /// </summary>
    [Tooltip("播放关门动画后，等待多少秒开始渐隐特效")]
    [Min(0f)]
    [SerializeField] private float effectFadeDelay;

    /// <summary>
    /// 特效从完全不透明渐隐到透明所需的时间。
    /// </summary>
    [Tooltip("特效从完全不透明渐隐到透明所需的时间")]
    [Min(0f)]
    [SerializeField] private float effectFadeDuration = 0.4f;

    /// <summary>
    /// 特效渐隐完成后，到允许成功流程进入下一小关之间的等待时间。
    /// </summary>
    [Tooltip("特效渐隐完成后，等待多少秒再进入下一小关")]
    [Min(0f)]
    [SerializeField] private float nextLevelDelayAfterFade = 0.3f;

    #endregion

    #region Runtime State

    private Tween _effectFadeTween;
    private float _effectAlpha = 1f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResetVisualState();
    }

    private void OnDisable()
    {
        KillEffectFadeTween();
        IsRunning = false;
        IsDone = false;
    }

    private void OnDestroy()
    {
        KillEffectFadeTween();
    }

    #endregion

    #region BaseItemEventHandle

    /// <summary>
    /// 用途：从头播放 door_close 的 close 动画，然后按配置让 effect 渐隐。
    /// 调用边界：由 TriggerExecutor/BaseItemExecutor 触发；渐隐完成后才标记事件执行完成。
    /// 注意事项：重复执行会先停止旧渐隐，并从完全不透明状态重新开始本次表现。
    /// </summary>
    public override void Execute()
    {
        KillEffectFadeTween();
        ResolveReferences();

        IsRunning = true;
        IsDone = false;

        PlayDoorCloseAnimation();
        BeginEffectFade();
    }

    /// <summary>
    /// 用途：停止未完成的渐隐，并恢复门关闭节点隐藏、effect 完全不透明的初始状态。
    /// 调用边界：供 BaseItemExecutor 再次执行前或当前小关 Reset 时调用，可重复调用。
    /// </summary>
    public override void ResetState()
    {
        KillEffectFadeTween();
        base.ResetState();
        ResolveReferences();
        ResetVisualState();
    }

    /// <summary>
    /// 用途：重置并执行一次入口门表现，异步等待特效渐隐句柄完成。
    /// 调用边界：由 IWannerLevelCtrl 的成功流程调用；取消令牌应绑定当前小关实例生命周期。
    /// 注意事项：组件中途禁用时返回 false，调用方可记录降级并继续清理，避免关卡永久阻塞。
    /// 节奏边界：Actor 进门后先等待关门延迟；fade 完成后再等待切关延迟，方法才会返回。
    /// </summary>
    /// <param name="cancellationToken">当前小关销毁、退出或玩法切换时取消等待。</param>
    /// <returns>动画句柄正常完成时返回 true；组件不可用或中途禁用时返回 false。</returns>
    public async UniTask<bool> PlayAsync(CancellationToken cancellationToken)
    {
        ResetState();
        if (!isActiveAndEnabled)
        {
            this.LogWarning("[IWannerGame_EnterDoorAnimCtrl] Component is disabled; enter door animation skipped.");
            return false;
        }

        float safeDoorCloseDelay = Mathf.Max(0f, doorCloseDelayAfterActorEntered);
        if (safeDoorCloseDelay > 0f)
        {
            await UniTask.WaitForSeconds(
                safeDoorCloseDelay,
                cancellationToken: cancellationToken);
            if (!isActiveAndEnabled)
                return false;
        }

        Execute();
        if (!IsDone)
        {
            await UniTask.WaitUntil(
                () => IsDone || !isActiveAndEnabled,
                cancellationToken: cancellationToken);
        }

        if (!IsDone || !isActiveAndEnabled)
            return false;

        float safeNextLevelDelay = Mathf.Max(0f, nextLevelDelayAfterFade);
        if (safeNextLevelDelay > 0f)
        {
            await UniTask.WaitForSeconds(
                safeNextLevelDelay,
                cancellationToken: cancellationToken);
        }

        return IsDone && isActiveAndEnabled;
    }

    #endregion

    #region Internal Helpers

    private void ResolveReferences()
    {
        if (doorCloseSpine == null)
            doorCloseSpine = transform.Find("door_close")?.GetComponent<ActorSpineCtrl>();

        if (effectSpine == null)
            effectSpine = transform.Find("effect")?.GetComponent<ActorSpineCtrl>();
    }

    private void PlayDoorCloseAnimation()
    {
        if (doorCloseSpine == null)
        {
            this.LogWarning("[IWannerGame_EnterDoorAnimCtrl] door_close ActorSpineCtrl is missing.");
            return;
        }

        doorCloseSpine.gameObject.SetActive(true);

        try
        {
            if (doorCloseSpine.PlayAnimation(DoorCloseAnimationName, false, false) == null)
                this.LogWarning("[IWannerGame_EnterDoorAnimCtrl] Failed to play door_close animation: close.");
        }
        catch (ArgumentException exception)
        {
            this.LogError($"[IWannerGame_EnterDoorAnimCtrl] door_close animation is invalid: {exception.Message}");
        }
    }

    private void BeginEffectFade()
    {
        if (effectSpine == null)
        {
            this.LogWarning("[IWannerGame_EnterDoorAnimCtrl] effect ActorSpineCtrl is missing; fade skipped.");
            CompleteExecution();
            return;
        }

        effectSpine.gameObject.SetActive(true);
        SetEffectAlpha(1f);

        float fadeDelay = Mathf.Max(0f, effectFadeDelay);
        float fadeDuration = Mathf.Max(0f, effectFadeDuration);
        if (fadeDelay <= 0f && fadeDuration <= 0f)
        {
            SetEffectAlpha(0f);
            CompleteExecution();
            return;
        }

        if (fadeDuration <= 0f)
        {
            _effectFadeTween = DOVirtual.DelayedCall(fadeDelay, () =>
                {
                    SetEffectAlpha(0f);
                    CompleteExecution();
                }, false)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            return;
        }

        _effectFadeTween = DOTween.To(() => _effectAlpha, SetEffectAlpha, 0f, fadeDuration)
            .SetDelay(fadeDelay)
            .SetEase(Ease.Linear)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(CompleteExecution);
    }

    private void ResetVisualState()
    {
        if (doorCloseSpine != null)
            doorCloseSpine.gameObject.SetActive(false);

        if (effectSpine == null)
            return;

        effectSpine.gameObject.SetActive(true);
        SetEffectAlpha(1f);
    }

    private void SetEffectAlpha(float alpha)
    {
        _effectAlpha = Mathf.Clamp01(alpha);
        effectSpine?.SetAlpha(_effectAlpha);
    }

    private void CompleteExecution()
    {
        _effectFadeTween = null;
        IsRunning = false;
        IsDone = true;
    }

    private void KillEffectFadeTween()
    {
        if (_effectFadeTween != null && _effectFadeTween.IsActive())
            _effectFadeTween.Kill(false);

        _effectFadeTween = null;
    }

    #endregion

    #region Editor Tools

#if UNITY_EDITOR
    /// <summary>
    /// 用途：在 Unity Inspector 中按子节点名称自动绑定 door_close 与 effect 的 ActorSpineCtrl。
    /// 调用边界：仅编辑器手动执行，只读取当前对象层级并写入本组件序列化引用，不触发运行时逻辑。
    /// 注意事项：找不到目标节点时保留原引用；Prefab Instance 的修改会记录为 Override。
    /// </summary>
    [Button("AutoBind")]
    public void AutoBind()
    {
        ActorSpineCtrl bindDoorCloseSpine = FindSpineCtrlByName("door_close");
        ActorSpineCtrl bindEffectSpine = FindSpineCtrlByName("effect");
        bool shouldBindDoorClose = bindDoorCloseSpine != null && doorCloseSpine != bindDoorCloseSpine;
        bool shouldBindEffect = bindEffectSpine != null && effectSpine != bindEffectSpine;
        if (!shouldBindDoorClose && !shouldBindEffect)
            return;

        Undo.RecordObject(this, "IWanner Enter Door AutoBind");
        if (shouldBindDoorClose)
            doorCloseSpine = bindDoorCloseSpine;
        if (shouldBindEffect)
            effectSpine = bindEffectSpine;

        EditorUtility.SetDirty(this);
        if (PrefabUtility.IsPartOfPrefabInstance(this))
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    private ActorSpineCtrl FindSpineCtrlByName(string targetName)
    {
        Transform directChild = transform.Find(targetName);
        ActorSpineCtrl directResult = directChild != null
            ? directChild.GetComponent<ActorSpineCtrl>()
            : null;
        if (directResult != null)
            return directResult;

        ActorSpineCtrl[] candidates = GetComponentsInChildren<ActorSpineCtrl>(true);
        foreach (ActorSpineCtrl candidate in candidates)
        {
            if (candidate != null &&
                string.Equals(candidate.gameObject.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
#endif

    #endregion
}
