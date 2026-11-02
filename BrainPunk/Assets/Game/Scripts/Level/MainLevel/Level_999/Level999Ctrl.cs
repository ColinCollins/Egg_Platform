using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game;
using Game.ConfigModule;
using Game.Events;
using Lofelt.NiceVibrations;
using UnityEngine;

/// <summary>
/// 当前关卡逻辑特殊，不存在 victory
/// </summary>
public class Level999Ctrl : MonoBehaviour, IDebuger, IEventSender
{
    private const float DrawEntryEnableDelay = 2f;

    [SerializeField] private ActorDestroyAnimHandle actorDestroy;

    /// <summary>
    /// Level_999 内通往 DrawGame 的完整入口节点；仅在 DrawGame 功能开关开启时显示和响应。
    /// </summary>
    [SerializeField] private GameObject drawGameEntry;

    private bool _canEnterDrawGame;
    private bool _hasTriggeredDrawEntry;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        bool isDrawGameOpen = ConfigManager.RemoteConfig.GetDrawGameIsOpen();
        if (drawGameEntry != null)
            drawGameEntry.SetActive(isDrawGameOpen);
        else
            this.LogWarning("[Level999Ctrl] DrawGame entry is missing.");

        var data = PlayCtrl.Instance.Level.CurrentLevelData;
        if (DB.GameData.LevelSuccessCount.TryAdd(data.Path, 0))
        {
            DB.GameData.LevelSuccessCount[data.Path]++;
            DB.GameData.Save();
        }

        // 解锁成就
        WaitingUnlockAchievement().Forget();
        if (isDrawGameOpen)
            EnableDrawEntryAfterDelay().Forget();
    }

    private async UniTask EnableDrawEntryAfterDelay()
    {
        bool isCanceled = await UniTask.WaitForSeconds(
                DrawEntryEnableDelay,
                cancellationToken: this.destroyCancellationToken)
            .SuppressCancellationThrow();
        if (isCanceled || this == null)
            return;

        _canEnterDrawGame = true;
    }

    private async UniTask WaitingUnlockAchievement()
    {
        await GameAchievementUtils.TryToAchievement(AchievementType.Level);
        await GameAchievementUtils.TryToAchievement(AchievementType.LevelCount);

        // 刷新 achievementcache
        await GameAchievementUtils.LoadAchievementCacheAsync();
    }

    /// <summary>
    /// 处理 Level_999 DrawGame 传送门的世界点击入口。
    /// 调用边界：仅供 Prefab 上的 ClickItemEventHandle UnityEvent 调用；碰撞入门表现不经过此入口。
    /// 注意事项：Bear.UI Popup、Top 或 System 层存在已打开视图时直接忽略，防止弹窗点击穿透到世界传送门。
    /// </summary>
    public void EnterDrawGameByClick()
    {
        if (!Application.isPlaying || HasOpenPopup())
            return;

        EnterDrawGameAtHighestPlayableLevel();
    }

    /// <summary>
    /// 退出当前主线关卡并直接进入 DrawGame 有效排序中最前的已解锁且未通关关卡。
    /// 调用边界：仅供 Level_999 运行时按钮或 UnityEvent 调用；方法名为兼容现有 prefab 绑定保留。
    /// 触发门禁：Level_999 进场满 2 秒后才响应调用，门禁开放前的调用直接忽略且不会排队。
    /// 一次性门禁：成功解析目标 Draw 关卡后立即锁定，当前关卡实例内的后续调用全部忽略。
    /// 注意事项：切换失败时保留现有控制器的失败回退行为，不重复创建关卡或 UI。
    /// </summary>
    public void EnterDrawGameAtHighestPlayableLevel()
    {
        if (!Application.isPlaying ||
            !ConfigManager.RemoteConfig.GetDrawGameIsOpen() ||
            !_canEnterDrawGame ||
            _hasTriggeredDrawEntry)
            return;

        GameManager gameManager = GameManager.Instance;
        if (!TryGetLowestUnlockedUnpassedDrawLevel(
                gameManager,
                out Config.DrawLevelSort sortData,
                out Config.DrawLevelData levelData,
                out string error))
        {
            this.LogError($"[Level999Ctrl] Resolve Draw level failed. error:{error}");
            return;
        }

        _hasTriggeredDrawEntry = true;

        if (!gameManager.TryEnterDraw(out error))
        {
            this.LogError($"[Level999Ctrl] Exit main level and enter DrawPlayCtrl failed. error:{error}");
            return;
        }

        if (!gameManager.TryEnterDrawLevel(sortData, levelData, out error))
        {
            this.LogError($"[Level999Ctrl] Enter Draw level failed. sortId:{sortData.Id}, error:{error}");
            return;
        }

        GameSDKService.Instance.EnterTBCPortal((int)Game.Play.PlayModeType.Main);
    }

    private static bool TryGetLowestUnlockedUnpassedDrawLevel(
        GameManager gameManager,
        out Config.DrawLevelSort sortData,
        out Config.DrawLevelData levelData,
        out string error)
    {
        sortData = null;
        levelData = null;
        error = null;

        DrawGameLevelCtrl levelCtrl = gameManager?.DrawGameLevel;
        if (levelCtrl == null || !levelCtrl.IsReady)
        {
            error = "DrawGameLevelCtrl is not ready.";
            return false;
        }

        var levelSorts = levelCtrl.LevelSorts;
        if (levelSorts == null || levelSorts.Count == 0)
        {
            error = "Draw level sequence is empty.";
            return false;
        }

        var saveData = DB.DrawGame;
        var unlockLevels = saveData?.UnlockLevels;
        var passedLevels = saveData?.PassedLevels;
        for (int i = 0; i < levelSorts.Count; i++)
        {
            Config.DrawLevelSort candidate = levelSorts[i];
            if (candidate == null ||
                unlockLevels == null ||
                !unlockLevels.Contains(candidate.Id) ||
                passedLevels?.Contains(candidate.Id) == true)
                continue;

            Config.DrawLevelData candidateData = levelCtrl.GetLevelDataById(candidate.Path);
            if (candidateData == null)
                continue;

            sortData = candidate;
            levelData = candidateData;
            return true;
        }

        // 不存在有效的已解锁未通关候选时，优先保留最小已解锁关的可重玩入口。
        for (int i = 0; i < levelSorts.Count; i++)
        {
            Config.DrawLevelSort candidate = levelSorts[i];
            if (candidate == null || unlockLevels == null || !unlockLevels.Contains(candidate.Id))
                continue;

            Config.DrawLevelData candidateData = levelCtrl.GetLevelDataById(candidate.Path);
            if (candidateData == null)
                continue;

            sortData = candidate;
            levelData = candidateData;
            return true;
        }

        // 清档或旧档没有解锁集合时，继续回退到排序中第一条有效关卡。
        for (int i = 0; i < levelSorts.Count; i++)
        {
            Config.DrawLevelSort candidate = levelSorts[i];
            Config.DrawLevelData candidateData = candidate == null
                ? null
                : levelCtrl.GetLevelDataById(candidate.Path);
            if (candidate == null || candidateData == null)
                continue;

            sortData = candidate;
            levelData = candidateData;
            return true;
        }

        error = "No valid Draw level is available.";
        sortData = null;
        levelData = null;
        return false;
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

    public async void EnterDoorAnim()
    {
        if (!Application.isPlaying || !ConfigManager.RemoteConfig.GetDrawGameIsOpen())
            return;

        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        if (actorDestroy)
            actorDestroy.PlayDestroy();

        await UniTask.WaitForSeconds(1.2f, cancellationToken: this.destroyCancellationToken);
        if (DB.GameSetting.VibrationOn)
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.Success);
        await UniTask.WaitForSeconds(.8f, cancellationToken: this.destroyCancellationToken);

        EnterDrawGameAtHighestPlayableLevel();
    }
}
