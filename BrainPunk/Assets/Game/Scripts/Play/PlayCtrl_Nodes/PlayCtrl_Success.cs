using System;
using System.Threading;
using Bear.EventSystem;
using Bear.Fsm;
using Bear.Game;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game.Events;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏成功状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.SUCCESS, false)]
    public class PlayCtrl_Success : StateNode, IEventSender, IDebuger
    {
        private PlayCtrl owner;
        private BaseUIView _gameVictoryPanel;
        private CancellationTokenSource _victoryProcessCancellationSource;
        private bool _hasContinuedToVictory;

        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Success)} Enter");

            owner = _owner as PlayCtrl;
            GameManager.Instance?.DailyChallenge?.TryUnlock(owner?.Level?.CurrentLevel ?? 0);
            _hasContinuedToVictory = false;

            CancelVictoryProcess();
            _victoryProcessCancellationSource = new CancellationTokenSource();
            RunVictoryProcessAsync(_victoryProcessCancellationSource.Token).Forget();
        }

        private async UniTask RunVictoryProcessAsync(CancellationToken cancellationToken)
        {
            bool isFirstVictory = IsFirstVictory();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 插屏逻辑调整 1.5.0
                // if (!Level.IsLastLevel)
                owner.Level.CurrentLevelState.TryToAddSuccessCount();
                InterstitialAdHelper.TryToShowInterstitial(
                    Config.Game.InterstitialPlacement.InGameVictory.ToString());

                DailyChallengeSettlement dailyChallengeSettlement = null;
                GameManager.Instance?.DailyChallenge?.TryRecordVictory(
                    owner.Level.CurrentLevel,
                    out dailyChallengeSettlement);

                GameSDKService.Instance.LevelEndEvent(GameSDKService.LevelEndTypeSuccess);
                owner.Level.CurrentLevelState.ResetData();
                if (RatingPopupPolicy.TryToShowRating(owner.Level.CurrentLevel))
                    await RatingPopup.ShowAndWaitAsync(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                if (dailyChallengeSettlement != null)
                {
                    await DailyChallengePopup.ShowAndWaitAsync(
                        dailyChallengeSettlement,
                        cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                ShowVictory(isFirstVictory);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                ReleaseVictoryProcess(cancellationToken);
            }
        }

        private bool HasPassedCurrentLevelBefore()
        {
            if (owner?.Level?.CurrentLevelState == null)
                return false;

            return DB.GameData?.PassedLevels.Contains(owner.Level.CurrentLevel) ?? false;
        }

        private void ShowVictory(bool isFirstVictory)
        {
            if (_hasContinuedToVictory)
                return;

            _hasContinuedToVictory = true;
            int completedLevel = owner.Level.CurrentLevel;
            if (isFirstVictory)
                TryToGetPostcardVictoryStar();

            _gameVictoryPanel = OneLinkManager.Instance.IsCurrentEntry(Config.Level.GameType.Default)
                ? GameVictoryPanel_DeepLink.Create(owner.Level.CurrentLevelData)
                : GameVictoryPanel.Create(owner.Level.CurrentLevelData, isFirstVictory);
            owner.Level.Victory();

            if (_gameVictoryPanel != null)
                GiftManager.Instance.TryOpenFirstPlayVictoryChestGift(completedLevel, isFirstVictory);

            WaitingUnlockAchievement().Forget();
        }

        /// <summary>
        /// 尝试检测解锁成就
        /// </summary>
        private async UniTask WaitingUnlockAchievement()
        {
            await GameAchievementUtils.TryToAchievement(AchievementType.Level);
            await GameAchievementUtils.TryToAchievement(AchievementType.LevelCount);
            await GameAchievementUtils.LoadAchievementCacheAsync();
        }

        /// <summary>
        /// 首次通关时根据 levelData.levelVictoryStarCount 获得 star，写入 Bag、Postcard.levelWaitingAnimStar 以及 levelCollectedStar。
        /// victory 获得的 star，id 从 3 开始计数，递减到 1.
        /// </summary>
        private void TryToGetPostcardVictoryStar()
        {
            if (!Application.isPlaying || !IsFirstVictory())
                return;

            Config.LevelData levelData = owner?.Level?.CurrentLevelData;
            if (levelData == null || string.IsNullOrEmpty(levelData.Id))
            {
                this.LogWarning("[PlayCtrl_Success] TryToGetPostcardVictoryStar skipped because level data is missing.");
                return;
            }

            PostcardManager postcardManager = GameManager.Instance?.Postcard;
            if (postcardManager == null)
            {
                this.LogWarning("[PlayCtrl_Success] TryToGetPostcardVictoryStar skipped because PostcardManager is missing.");
                return;
            }

            if (!postcardManager.TryGrantVictoryStars(levelData))
            {
                this.LogWarning($"[PlayCtrl_Success] Grant victory stars failed. levelId:{levelData.Id}, count:{levelData.LevelVictoryStarCount}, maxCount:{levelData.LevelSumStarCount}");
            }
        }

        /// <summary>
        /// 进入当前状态的时候，判断当前关卡是否首次通关。LevelCtrl IsPassed 不存在数据时返回 true，否则返回 false。
        /// </summary>
        private bool IsFirstVictory()
        {
            if (DB.GameData?.PassedLevels == null)
                return false;

            return !HasPassedCurrentLevelBefore();
        }

        private void CancelVictoryProcess()
        {
            CancellationTokenSource cancellationSource = _victoryProcessCancellationSource;
            _victoryProcessCancellationSource = null;
            if (cancellationSource == null)
                return;

            if (!cancellationSource.IsCancellationRequested)
                cancellationSource.Cancel();

            cancellationSource.Dispose();
        }

        private void ReleaseVictoryProcess(CancellationToken cancellationToken)
        {
            CancellationTokenSource cancellationSource = _victoryProcessCancellationSource;
            if (cancellationSource == null || cancellationSource.Token != cancellationToken)
                return;

            _victoryProcessCancellationSource = null;
            cancellationSource.Dispose();
        }

        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Success)} Execute");
        }

        public override void OnUpdate()
        {
        }

        public override void OnExit()
        {
            CancelVictoryProcess();
            Debug.Log($"{nameof(PlayCtrl_Success)} Exit");
        }
    }
}
