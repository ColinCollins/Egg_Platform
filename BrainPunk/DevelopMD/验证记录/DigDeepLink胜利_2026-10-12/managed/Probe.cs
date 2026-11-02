using System;using System.Collections.Generic;using Config;using Config.Level;using Game;using Bear.UI;using UnityEngine;
public class DigPlayCtrl {
 private BaseUIView _victory;private DigLevelData _data;private Runtime _runtime=new();private int _levelId,_version;private bool _resolved=true,_transitioning,_rewardInkGranted=true,_hintGranted=true;public bool IsActive=true;
 public BaseUIView Victory=>_victory;public int Current=>_levelId;public int Starts,Replays,Transitions,Exits;public bool KeepInk=>_rewardInkGranted;public bool KeepHint=>_hintGranted;
 public void Load(int id,int stars){_levelId=id;_data=GameManager.Instance.DigGameLevel.GetLevelDataById(GameManager.Instance.DigGameLevel.GetLevelSortById(id)?.Path);_runtime.InkState.StarCount=stars;}
 public void Win()=>ShowVictoryPanel();public void Next()=>OnNext(new());public void Replay()=>OnReplay(new());
 private void StartNewLevelAnalytics(){Starts++;}private void StartReplayAnalytics(){Replays++;}private void BeginTransition(){_transitioning=true;Transitions++;}private void BeginRebuild(bool _){_transitioning=true;Transitions++;}
 private void ExitToLevelSelection(){Exits++;OneLinkManager.Instance.EndEntry(Config.Level.GameType.Dig);}
 private void HandleFailure(int version,Exception ex){ExitToLevelSelection();}
    private void ShowVictoryPanel()
    {
        try
        {
            var levels = GameManager.Instance.DigGameLevel;
            bool first = levels.IsFirstVictory(_levelId);
            DB.DigGame.LevelCollectedStar.TryGetValue(_data.Id, out int previousStars);
            int stars = Mathf.Clamp(_runtime.InkState.StarCount, 0, 3);
            int collectedStars = Mathf.Max(previousStars, stars);
            int addedStars = Mathf.Max(0, stars - previousStars);
            if (OneLinkManager.Instance.IsCurrentEntry(Config.Level.GameType.Dig))
            {
                _victory = GameVictoryPanel_DeepLink.CreateForDig(collectedStars, addedStars);
            }
            else
            {
                var panel = first ? DigGame_GameVictoryPanel_Reward.Create(_data) : DigGame_GameVictoryPanel.Create(_data);
                panel?.ConfigureStars(collectedStars, addedStars);
                _victory = panel;
            }
            if (!_victory) throw new InvalidOperationException("Dig 胜利面板加载失败。");
            // 与 Draw 的职责顺序一致：控制器创建胜利面板，再记录独立模式进度。
            levels.RecordVictory(_levelId, _data, stars);
            this.Log($"[Dig] 胜利面板已创建。level:{_levelId}, panel:{_victory.GetType().Name}");
        }
        catch (Exception ex) { HandleFailure(_version, ex); }
    }
    private void OnNext(EnterNextLevelEvent evt)
    {
        if (!IsActive || !_resolved || _transitioning) return;
        var levels = GameManager.Instance.DigGameLevel;
        bool isDeepLinkEntry = OneLinkManager.Instance.IsCurrentEntry(Config.Level.GameType.Dig);
        DigLevelSort sort = null;
        if (isDeepLinkEntry)
        {
            // 专属面板的 Next 回到当前有效序列最靠前未通过关；普通全通时沿原查询进入 TBC。
            sort = levels.GetLevelSortById(levels.ResolveLowestUnpassed());
        }
        else
        {
            // 远端允许正在玩时更新列表；下一关须按进入时的内容在新列表中定位。
            int index = -1;
            for (int i = 0; i < levels.LevelSorts.Count; i++)
                if (levels.LevelSorts[i].Path == _data.Id) { index = i; break; }
            if (index >= 0 && index + 1 < levels.LevelSorts.Count) sort = levels.LevelSorts[index + 1];
        }
        var data = levels.GetLevelDataById(sort?.Path);
        if (data == null)
        {
            if (isDeepLinkEntry) OneLinkManager.Instance.EndEntry(Config.Level.GameType.Dig);
            ExitToLevelSelection();
            return;
        }
        _levelId = sort.Id; _data = data;
        if (!DB.DigGame.UnlockLevels.Contains(_levelId)) DB.DigGame.UnlockLevels.Add(_levelId);
        StartNewLevelAnalytics();
        _rewardInkGranted = _hintGranted = false;
        // 保留已结算旧关直到翻页完全遮挡。
        BeginTransition();
        if (isDeepLinkEntry) OneLinkManager.Instance.EndEntry(Config.Level.GameType.Dig);
    }
    private void OnReplay(GameReplayEvent evt)
    {
        if (IsActive && _resolved && !_transitioning)
        {
            StartReplayAnalytics();
            BeginRebuild(false);
        }
    }
}
public class GameVictoryPanel_DeepLink:BaseUIView {
 public Stars victoryStarGroup=new();public bool _isStarPresentationComplete,_isEntranceComplete=true,_isNavigationTriggered;public Block clickBlock=new();public CustomButton homeButton=new(),replayButton=new(),nextButton=new();public CanvasGroup nextButtonGroup=new();
 public bool TryNavigate()=>TryBeginNavigation();
    public static GameVictoryPanel_DeepLink CreateForDig(int collectedStarCount, int waitingAnimStarCount)
    {
        if (UIManager.Instance == null)
            return null;

        GameVictoryPanel_DeepLink panel = OpenPanel();
        if (panel == null)
            return null;

        bool isAnimationPlaying = panel.victoryStarGroup != null && panel.victoryStarGroup.RefreshStars(
            collectedStarCount,
            waitingAnimStarCount,
            panel.CompleteStarPresentation);
        panel.SetStarAnimationBlocked(isAnimationPlaying);
        if (!isAnimationPlaying)
            panel.CompleteStarPresentation();
        return panel;
    }
    private static GameVictoryPanel_DeepLink OpenPanel()
    {
        return UIManager.Instance.OpenUI<GameVictoryPanel_DeepLink>(
            nameof(GameVictoryPanel_DeepLink),
            UILayer.Popup);
    }
    private void CompleteStarPresentation()
    {
        _isStarPresentationComplete = true;
        SetStarAnimationBlocked(false);
        RefreshButtonStates();
    }
    private void SetStarAnimationBlocked(bool isBlocked)
    {
        if (clickBlock != null)
            clickBlock.SetActive(isBlocked);
    }
    private bool TryBeginNavigation()
    {
        if (_isNavigationTriggered || !_isEntranceComplete || !_isStarPresentationComplete)
            return false;

        _isNavigationTriggered = true;
        RefreshButtonStates();
        return true;
    }
    private void RefreshButtonStates()
    {
        bool interactable = _isEntranceComplete && _isStarPresentationComplete && !_isNavigationTriggered;
        if (homeButton != null)
            homeButton.Interactable = interactable;
        if (replayButton != null)
            replayButton.Interactable = interactable;
        if (nextButton != null)
            nextButton.Interactable = interactable;
        if (nextButtonGroup != null)
        {
            nextButtonGroup.interactable = interactable;
            nextButtonGroup.blocksRaycasts = interactable;
        }
    }
}
public class DigGameLevelCtrl {
 public const string TbcDataId="40999";public List<DigLevelSort> _levelSorts=new();public List<DigLevelSort> LevelSorts=>_levelSorts;public Dictionary<string,DigLevelData> Data=new();
 public DigLevelData GetLevelDataById(string id)=>id!=null&&Data.TryGetValue(id,out var data)?data:null;
 public bool IsFirstVictory(int id)=>!DB.DigGame.PassedLevels.Contains(id);
    public int ResolveLowestUnpassed()
    {
        foreach (var sort in _levelSorts)
            if (sort.Path == TbcDataId || !DB.DigGame.PassedLevels.Contains(sort.Id)) return sort.Id;
        return 1;
    }
    public DigLevelSort GetLevelSortById(int id)
    {
        return _levelSorts.Find(data => data != null && data.Id == id);
    }
    public bool RecordVictory(int id, DigLevelData data, int starCount)
    {
        if (data == null || data.Id == TbcDataId) return false;
        var save = DB.DigGame;
        save.EnsureValidCollections();
        save.LevelCollectedStar.TryGetValue(data.Id, out int previousStars);
        save.LevelCollectedStar[data.Id] = Mathf.Max(previousStars, Mathf.Clamp(starCount, 0, 3));
        bool first = !save.PassedLevels.Contains(id);
        if (first) save.PassedLevels.Add(id);
        save.MaxLevel = Math.Max(save.MaxLevel, id);
        save.CurrentLevel = id;
        // 当前排序仍映射原内容时，才顺序解锁下一关，避免云控重排后解锁错误内容。
        int index = _levelSorts.FindIndex(item => item.Id == id && item.Path == data.Id);
        if (index >= 0 && index + 1 < _levelSorts.Count)
        {
            int next = _levelSorts[index + 1].Id;
            if (!save.UnlockLevels.Contains(next)) save.UnlockLevels.Add(next);
            save.CurrentLevel = next;
        }
        if (!DB.SaveDigGame()) this.LogError("[Dig] 通关进度保存失败。");
        return first;
    }
}
namespace Game {
 public class OneLinkManager {
 public static OneLinkManager Instance=new();public Config.Level.GameType? _entryGameType;public bool _entryBypassesHistory;
 public void Set(Config.Level.GameType type,bool confirmed=true){_entryGameType=type;DB.GameData.IsEnterByOneLink=confirmed;}
 public bool IsCurrentEntry(Config.Level.GameType gameType)=>_entryGameType==gameType&&DB.GameData?.IsEnterByOneLink==true;
        internal void EndEntry(GameType gameType)
        {
            if (_entryGameType != gameType)
                return;

            _entryGameType = null;
            _entryBypassesHistory = false;
            if (DB.GameData != null)
                DB.GameData.IsEnterByOneLink = false;
            GuruLog.I($"[OneLink] stage=entry_context_cleared, gameType={gameType}.");
        }
}
}
