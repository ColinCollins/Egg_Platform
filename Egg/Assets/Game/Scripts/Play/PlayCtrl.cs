using System.Diagnostics;
using Bear.EventSystem;
using Bear.Fsm;
using Bear.Logger;
using Bear.UI;
using Game.Common;
using Game.Events;
using Game.Level;
using Game.Play;
using UnityEngine;

/* public enum GamePlayState
{
    // 默认。没什么功能设计
    Start = 0,
    // 开始游戏
    Playing, 
    // 暂停游戏
    Pause,
    // 游戏成功
    Success,
    // 游戏失败
    Failed
} */

/// <summary>
/// 
/// </summary>
public class PlayCtrl : Singleton<PlayCtrl>, IBearMachineOwner, IDebuger, IEventSender
{
    private StateMachine _machine;

    private EventSubscriber _subscriber;

    public EventSubscriber Subscriber => _subscriber;

    #region Level
    private LevelCtrl _level;

    public LevelCtrl Level => _level;

    public GamePlayPanel @CurrentGamePlayPanel
    {
        private set;
        get;
    }

    public Transform SceneRoot;
    public BaseLevelCtrl LevelCtrl;
    public BaseLevelCtrl LevelPrefab;
    private const string LevelPath = "Level/{0}";
    #endregion


    public void Init()
    {
        _level = new LevelCtrl();
        _level.Init();

        _machine = new StateMachine(this);
        _machine.Inject(typeof(PlayCtrl_Start),
        typeof(PlayCtrl_Playing),
        typeof(PlayCtrl_Pause),
        typeof(PlayCtrl_Success),
        typeof(PlayCtrl_Failed));

        _machine.Apply(GetType());
        _machine.Enter(GamePlayStateName.START);

        AddListener();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchState);
        _subscriber.Subscribe<GameSettingEvent>(OnGameSettingEvent);
        _subscriber.Subscribe<GameTipsEvent>(OnGameTipsEvent);
        _subscriber.Subscribe<GameResetEvent>(OnGameResetEvent);

        _subscriber.Subscribe<EnterLevelEvent>(OnGameEnter);
        _subscriber.Subscribe<EnterNextLevelEvent>(OnEnterNextLevel);
    }

    private void OnGameEnter(EnterLevelEvent evt)
    {
        CreateLevel(evt.Data.Path);
        Level.SetCurrentLevel(evt.Data.Id);
        _machine.Enter(GamePlayStateName.PLAYING);
    }

    private void OnEnterNextLevel(EnterNextLevelEvent evt)
    {
        DestroyLevel();
        var data = Level.CurrentLevelData;
        if (data == null)
        {
            // switch panel
            ChoiceLevelPanel.Create();
        }
        else
        {
            CreateLevel(data.Path);
            _machine.Enter(GamePlayStateName.PLAYING);
        }
    }

    private void OnSwitchState(SwitchGameStateEvent evt)
    {
        this.Log(evt.NewState);
        _machine.Enter(evt.NewState);
    }

    private void OnGameSettingEvent(GameSettingEvent evt)
    {
        this.Log("show Settings");
        _machine.Enter(GamePlayStateName.PAUSE);
        GameSettingPopup.Create();
    }

    private void OnGameTipsEvent(GameTipsEvent evt)
    {
        this.Log("show Tips");
        _machine.Enter(GamePlayStateName.PAUSE);
        GameTipsPopup.Create();
    }

    private void OnGameResetEvent(GameResetEvent evt)
    {
        // Show Ask 
        DestroyLevel();
        CreateLevel(Level.CurrentLevelData.Path);
        _machine.Enter(GamePlayStateName.PLAYING);
        this.DispatchEvent(Witness<GameResumeEvent>._);
    }

    /// <summary>
    /// 清理當前關卡
    /// </summary>
    private void DestroyLevel()
    {
        if (LevelCtrl == null)
            return;

        LevelCtrl.DestroyLevel();
        LevelCtrl = null;
        LevelPrefab = null;
    }

    public void CreateLevel(string levelName)
    {
        if (LevelCtrl != null)
            return;

        // id, 测试关卡
        if (!LevelPrefab)
            LevelPrefab = Resources.Load<BaseLevelCtrl>(string.Format(LevelPath, levelName));

        if (!LevelPrefab)
        {
            this.LogError($"Level lost: {levelName}");
            return;
        }

        LevelCtrl = GameObject.Instantiate(LevelPrefab, SceneRoot);
        RefreshGamePanel();
    }

    /// <summary>
    /// 因为需求会有多种不同的 gamePanel，所以我们需要针对变化，设置变体
    /// </summary>
    private void RefreshGamePanel()
    {
        if (CurrentGamePlayPanel != null)
        {
            UIManager.Instance.CloseUI(CurrentGamePlayPanel);
        }

        CurrentGamePlayPanel = GamePlayPanel.Create(LevelCtrl.GamePlayPanelName);
    }

    private void Update()
    {
        _machine?.Update();
    }

    private void OnDestroy()
    {
        _machine?.Dispose();
    }
}
