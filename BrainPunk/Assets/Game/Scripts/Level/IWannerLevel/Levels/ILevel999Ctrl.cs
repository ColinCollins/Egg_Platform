using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game;
using Game.Events;
using Game.Play;
using Lofelt.NiceVibrations;
using UnityEngine;

/// <summary>
/// IWannerGame TBN 占位关控制器：处理传送门表现并进入最小序列的未通关主线关卡；全部通关时进入 Level_999。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/IWanner/Level 999 Ctrl")]
public sealed class ILevel999Ctrl : MonoBehaviour, IDebuger, IEventSender
{
    #region Inspector References

    /// <summary>
    /// 用途：配置 ILevel_999 进场后开放主线传送门点击的等待秒数。
    /// 调用边界：仅由本控制器启动时的入口门禁读取；设为 0 时立即开放。
    /// 注意事项：Inspector 限制为非负值，运行时仍会对异常序列化值执行非负保护。
    /// </summary>
    [SerializeField, Min(0f), Tooltip("进入关卡后，主线传送门点击开放前的等待秒数；0 表示立即开放。")]
    private float mainEntryEnableDelay = 2f;

    /// <summary>
    /// 用途：驱动 ILevel_999 玩家进入传送门时的缩放、旋转与销毁表现。
    /// 调用边界：仅由本控制器的 EnterDoorAnim 触发；为空时仍会在等待完成后尝试进入主线。
    /// 注意事项：target 与 endTarget 由 ILevel_999 Prefab 显式绑定，不在运行时跨关卡查找。
    /// </summary>
    [SerializeField] private ActorDestroyAnimHandle actorDestroy;

    #endregion

    #region Runtime State

    private bool _canEnterMainLevel;
    private bool _hasTriggeredMainEntry;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        EnableMainEntryAfterDelay().Forget();
    }

    #endregion

    #region Main Level Entry

    private async UniTask EnableMainEntryAfterDelay()
    {
        bool isCanceled = await UniTask.WaitForSeconds(
                Mathf.Max(0f, mainEntryEnableDelay),
                cancellationToken: this.destroyCancellationToken)
            .SuppressCancellationThrow();
        if (isCanceled || this == null)
            return;

        _canEnterMainLevel = true;
    }

    private async UniTask<bool> WaitForMainEntryEnabled()
    {
        if (_canEnterMainLevel)
            return true;

        bool isCanceled = await UniTask.WaitUntil(
                () => _canEnterMainLevel,
                cancellationToken: this.destroyCancellationToken)
            .SuppressCancellationThrow();
        return !isCanceled && this != null;
    }

    /// <summary>
    /// 退出当前 IWanner TBN 关并进入最小序列的未通关主线关卡；全部通关时进入 Level_999。
    /// 调用边界：仅供 ILevel_999 运行时按钮或 UnityEvent 调用。
    /// 触发门禁：进场达到 Inspector 配置的等待秒数后才响应；成功发起一次请求后，当前实例内的后续调用全部忽略。
    /// 注意事项：切换失败时不重复创建关卡或 UI，并通过 Bear Logger 记录失败原因。
    /// </summary>
    public void EnterMainLevel()
    {
        if (!Application.isPlaying || !_canEnterMainLevel || _hasTriggeredMainEntry)
            return;

        _hasTriggeredMainEntry = true;

        GameManager gameManager = GameManager.Instance;
        if (!gameManager.TryEnterMainLevel(out string error))
        {
            this.LogError($"[ILevel999Ctrl] Enter target main level failed. error:{error}");
            return;
        }

        GameSDKService.Instance.EnterTBCPortal((int)PlayModeType.IWanner);
    }

    /// <summary>
    /// 播放传送门入场表现，并在与主线 Level999Ctrl 一致的两秒节奏结束后进入目标主线关卡。
    /// 调用边界：仅供 ILevel_999 传送门 Trigger 调用。
    /// 注意事项：等待使用对象销毁令牌；关卡退出或对象销毁后不会继续发起跳转。
    /// </summary>
    public async void EnterDoorAnim()
    {
        if (!Application.isPlaying)
            return;

        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        if (actorDestroy)
            actorDestroy.PlayDestroy();

        bool isCanceled = await UniTask.WaitForSeconds(
                1.2f,
                cancellationToken: this.destroyCancellationToken)
            .SuppressCancellationThrow();
        if (isCanceled || this == null)
            return;

        if (DB.GameSetting.VibrationOn)
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.Success);

        isCanceled = await UniTask.WaitForSeconds(
                0.8f,
                cancellationToken: this.destroyCancellationToken)
            .SuppressCancellationThrow();
        if (isCanceled || this == null)
            return;

        if (await WaitForMainEntryEnabled())
            EnterMainLevel();
    }

    #endregion
}
