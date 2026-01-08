using Bear.EventSystem;
using Bear.Fsm;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Level;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏进行中状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.PLAYING, false)]
    public class PlayCtrl_Playing : StateNode, IDebuger
    {
        private PlayCtrl owner;

        private GamePlayPanel gamePlayPanel;
        private BaseLevelCtrl levelCtrl;
        private BaseLevelCtrl levelPrefab;

        private Transform SceneRoot;

        private const string LevelPath = "Level/Level_{0}";

        private EventSubscriber _subscriber;

        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Enter");

            owner = _owner as PlayCtrl;

            SceneRoot = GameObject.Find("Scene").transform;

            gamePlayPanel = GamePlayPanel.Create();

            AddListener();

            // 测试用关卡
            CreateLevel("001");
        }

        private void AddListener()
        {
            EventsUtils.ResetEvents(ref _subscriber);
            _subscriber.Subscribe<GameResetEvent>(OnGameResetEvent);
        }


        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Execute");
        }

        public override void OnUpdate()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Update");
        }

        public override void OnExit()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Exit");
            EventsUtils.ResetEvents(ref _subscriber);
        }

        // 可以传参
        private void CreateLevel(string levelId)
        {
            // id, 测试关卡
            if (!levelPrefab)
                levelPrefab = Resources.Load<BaseLevelCtrl>(string.Format(LevelPath, levelId));

            if (!levelPrefab)
                return;

            CreateCurrentLevel();
        }

        private void CreateCurrentLevel()
        {
            levelCtrl = GameObject.Instantiate(levelPrefab, SceneRoot);
        }

        private void DestroyLevel()
        {
            if (levelCtrl == null)
                return;

            levelCtrl.DestroyLevel();
        }

        private void OnGameResetEvent(GameResetEvent evt)
        {
            // Show Ask 
            DestroyLevel();
            CreateCurrentLevel();
        }
    }
}

