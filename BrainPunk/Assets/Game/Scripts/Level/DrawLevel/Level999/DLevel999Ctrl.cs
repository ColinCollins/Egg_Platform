using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game;
using Game.Events;
using Game.Play;
using Lofelt.NiceVibrations;
using UnityEngine;

/// <summary>
/// DrawGame TBC 占位关控制器：处理传送门表现并进入最小序列的未通关主线关卡；全部通关时进入 Level_999。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 999 Ctrl")]
public sealed class DLevel999Ctrl : MonoBehaviour, IDebuger, IEventSender
{
    #region Inspector References

    /// <summary>
    /// 用途：配置 DLevel_999 进场后开放主线传送门点击的等待秒数。
    /// 调用边界：仅由本控制器启动时的入口门禁读取；设为 0 时立即开放。
    /// 注意事项：Inspector 限制为非负值，运行时仍会对异常序列化值执行非负保护。
    /// </summary>
    [SerializeField, Min(0f), Tooltip("进入关卡后，主线传送门点击开放前的等待秒数；0 表示立即开放。")]
    private float mainEntryEnableDelay = 2f;

    /// <summary>
    /// 用途：驱动 DLevel_999 玩家进入传送门时的缩放、旋转与销毁表现。
    /// 调用边界：仅由本控制器的 EnterDoorAnim 触发；为空时仍会在等待完成后尝试进入主线。
    /// 注意事项：target 与 endTarget 由 DLevel_999 Prefab 显式绑定，不在运行时跨关卡查找。
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

    /// <summary>
    /// 处理 DLevel_999 传送门的世界点击入口。
    /// 调用边界：仅供 Prefab 上的 ClickItemEventHandle UnityEvent 调用；碰撞入门表现不经过此入口。
    /// 注意事项：Bear.UI Popup、Top 或 System 层存在已打开视图时直接忽略，防止弹窗点击穿透到世界传送门。
    /// </summary>
    public void EnterMainLevelByClick()
    {
        if (!Application.isPlaying || HasOpenPopup())
            return;

        EnterMainLevel();
    }

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
    /// 退出当前 Draw TBC 关并进入最小序列的未通关主线关卡；全部通关时进入 Level_999。
    /// 调用边界：仅供 DLevel_999 运行时按钮或 UnityEvent 调用。
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
            this.LogError($"[DLevel999Ctrl] Enter target main level failed. error:{error}");
            return;
        }

        GameSDKService.Instance.EnterTBCPortal((int)PlayModeType.Draw);
    }

    /// <summary>
    /// 播放传送门入场表现，并在与主线 Level999Ctrl 一致的两秒节奏结束后进入目标主线关卡。
    /// 调用边界：仅供 DLevel_999 传送门 Trigger 调用。
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

    /// <summary>
    /// 检查 Bear.UI Popup、Top 或 System 层是否仍有已打开视图；已经关闭但由 UIManager 缓存的实例不会阻断点击。
    /// </summary>
    private static bool HasOpenPopup()
    {
        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
            return false;

        BaseUIView[] popupViews = uiManager.GetComponentsInChildren<BaseUIView>(true);
        for (int i = 0; i < popupViews.Length; i++)
        {
            BaseUIView popupView = popupViews[i];
            if (popupView == null || !popupView.IsOpen)
                continue;

            // DEBUG_MODE 下 GMPanel 是常驻的 System 层调试宿主，不代表存在需要阻断世界点击的覆盖层。
            if (popupView is GMPanel)
                continue;

            if (popupView.Layer == UILayer.Popup ||
                popupView.Layer == UILayer.Top ||
                popupView.Layer == UILayer.System)
                return true;
        }

        return false;
    }

    #endregion
}
