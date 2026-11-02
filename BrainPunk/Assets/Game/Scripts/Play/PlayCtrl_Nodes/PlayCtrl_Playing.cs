using Bear.EventSystem;
using Bear.Fsm;
using Bear.Logger;
using Config;
using Game.ConfigModule;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏进行中状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.PLAYING, false)]
    public class PlayCtrl_Playing : StateNode, IDebuger, IEventSender
    {
        private PlayCtrl owner;

        private LevelRuntimeState state;
        private float showTipsTimeLimit;
        private int showTipsFailCount;
        private int guideBubbleAttemptIndex = -1;
        private bool hasCheckedGameTipsGuide;
        private GameTipsGuideTriggerSettings gameTipsGuideTriggerSettings;

        private LevelData data;

        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Enter");

            owner = _owner as PlayCtrl;
            state = owner.Level.CurrentLevelState;
            data = owner.Level.CurrentLevelData;
            showTipsTimeLimit = ConfigManager.RemoteConfig.GetShowTipsTimeLimit();
            ApplyLevelTipsWarningIntervalOverride();
            showTipsFailCount = ConfigManager.RemoteConfig.GetShowTipsFailCount();
            gameTipsGuideTriggerSettings = GameTipsGuideFlow.LoadTriggerSettings();
            guideBubbleAttemptIndex = -1;
            hasCheckedGameTipsGuide = false;

            this.Log("showTipsTimeLimit: " + showTipsTimeLimit);
            this.Log("showTipsFailCount: " + showTipsFailCount);

            if (owner.SceneRoot == null)
                owner.SceneRoot = GameObject.Find("Scene").transform;

            // this.Log("AudioManager.IsCurrentMusicTag: " + AudioManager.IsCurrentMusicTag("musicInGame"));

            if (!AudioManager.IsCurrentMusicTag("musicInGame"))
            {
                AudioManager.PlayMusic("musicInGame");
            }

            state.CompleteAttemptTransition();
        }

        /// <summary>
        /// 读取当前关卡实例的提示气泡等待时间覆盖；没有有效覆盖时保留远端配置。
        /// </summary>
        private void ApplyLevelTipsWarningIntervalOverride()
        {
            if (owner?.LevelCtrl == null)
                return;

            MonoBehaviour[] behaviours = owner.LevelCtrl.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not ILevelTipsWarningIntervalProvider provider ||
                    !provider.TryGetTipsWarningInterval(out float intervalSeconds) ||
                    intervalSeconds <= 0f)
                {
                    continue;
                }

                showTipsTimeLimit = intervalSeconds;
                return;
            }
        }

        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Execute");
        }

        public override void OnUpdate()
        {
            UpdateGuideBubble();

            if (state == null)
                return;

            state.Tick(Time.deltaTime);

            // Reset 重建期间保留现有尝试计时，只延后所有 tips 门槛检测。
            if (state.IsAttemptTransitioning)
                return;

            UpdateGameTipsGuide();

            if (state.HasTips || state.HasClickTips)
                return;

            bool hasShownTipsWarning = state.HasShownTipsWarning;
            float tipsWaitingTime = hasShownTipsWarning
                ? state.CurrentAttemptTimeSeconds - state.TipsWarningHiddenAtSeconds
                : state.CurrentAttemptTimeSeconds;
            bool playTimeFetch = tipsWaitingTime > showTipsTimeLimit;
            bool failCountFetch = !hasShownTipsWarning &&
                                  state.FailCount + state.ResetCount >= showTipsFailCount;
            if (failCountFetch || playTimeFetch)
            {
                state.SwitchClickTips(true);
                this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, true);

                this.Log($"Show Tips warning. waitingTime:{tipsWaitingTime:F1}");
            }
        }

        /// <summary>
        /// 在现有 Playing 更新节点中复用关卡累计时间与重试计数，只在门槛首次满足时请求 UI 启动引导。
        /// </summary>
        private void UpdateGameTipsGuide()
        {
            if (hasCheckedGameTipsGuide || state == null || data == null || owner == null)
                return;

            if (!GameTipsGuideFlow.HasReachedTrigger(state, gameTipsGuideTriggerSettings))
                return;

            GamePlayPanel gamePlayPanel = owner.CurrentGamePlayPanel;
            if (gamePlayPanel == null)
                return;

            hasCheckedGameTipsGuide = true;
            owner.TryStartGameTipsGuide(state, gameTipsGuideTriggerSettings);
        }

        /// <summary>
        /// 引导气泡计时
        /// 对 hintsTime 进行倒计时，Reset 重新计时。达到时间后，发送 ShowHintBubbleEvent。
        /// </summary>
        private void UpdateGuideBubble()
        {
            if (state == null || data == null)
                return;

            if (!data.IsGiveHints)
                return;

            if (state.HasShownGuideBubble || state.IsAttemptTransitioning)
                return;

            // 次数限制
            if (data.HintsResetCount <= (state.ResetCount + state.FailCount))
            {
                state.SwitchGuideBubble(true);
                this.DispatchEvent(Witness<ShowHintBubbleEvent>._, data);
                return;
            }

            if (data.HintsTime <= 0 || string.IsNullOrEmpty(data.HintsDesc))
                return;

            if (state.CurrentAttemptTimeSeconds < data.HintsTime)
                return;

            state.SwitchGuideBubble(true);
            this.DispatchEvent(Witness<ShowHintBubbleEvent>._, data);
            this.Log($"Show guide bubble: {data.HintsDesc}, hintsTime: {data.HintsTime}, attempt: {guideBubbleAttemptIndex}");
        }

        public override void OnExit()
        {
            this.Log($"{nameof(PlayCtrl_Playing)} Exit");
        }

    }
}

