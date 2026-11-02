using System;
using Bear.EventSystem;
using Bear.Logger;
using Config.Game;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using GameCommon;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Level
{

    /// <summary>
    /// 关卡控制类， 用于控制场景中的基本内容，可以用于拓展
    /// 需要绑定在 Level prefab 上
    /// </summary>
    public class BaseLevelCtrl : MonoBehaviour, IDebuger, IEventSender, Actor.IActorSkinBindingOwner
    {
        // [SerializeField] private LayerMask SuccessLayer;
        [SerializeField] private LayerMask FailLayer;
        // Success
        // [SerializeField] private OnTrigger2DHandle onTrigger2DHandle;

        [SerializeField] private ActorCtrl actor;

        [SerializeField] private SuccessAnimCtrl successAnim;
        [SerializeField] private EnterDoorAnimCtrl enterDoorAnim;
        private float straightSuccessDelay = .5f;
        private bool enterDoorAnimationEnabled = true;

        public ActorCtrl Actor => actor;

        /// <summary>
        /// 每个 Level 专门配置不同的 gamepanel
        /// </summary>
        [SerializeField] private string gamePlayPanelName = "GamePlayPanel_000";

        public string GamePlayPanelName => gamePlayPanelName;

        // 事件订阅器
        private EventSubscriber _subscriber;

        public bool IsActorEnterDoor { get; set; }

        private bool isFailed = false;
        protected bool isPause = false;
        protected bool isFinished = false;

        /// <summary>
        /// 提示气泡
        /// </summary>
        protected HintBubble bubble;

        // 关卡局部玩法可以暂时关闭角色头顶的通用 HintBubble。默认保持开启，
        // 这样其它关卡仍完全沿用原有提示事件链。
        private bool hintBubbleEnabled = true;

        /// <summary>
        /// 用于控制 ActorCtrl 移动操作开关
        /// </summary>
        public bool ActorCtrlable { get; private set; }

        public void SetSuccessAnim(SuccessAnimCtrl newSuccessAnim)
        {
            successAnim = newSuccessAnim;
            BindSuccessAnimActorTarget();
        }

        public void SetEnterDoorAnim(EnterDoorAnimCtrl doorAnim)
        {
            enterDoorAnim = doorAnim;
        }

        /// <summary>
        /// 允许关卡实例在创建阶段关闭通用入口门演出。默认开启，只影响当前关卡实例。
        /// </summary>
        public void SetEnterDoorAnimationEnabled(bool enabled)
        {
            enterDoorAnimationEnabled = enabled;
        }

        private void Awake()
        {
            // 确保 Actor 已赋值
            if (actor == null)
            {
                actor = FindFirstObjectByType<ActorCtrl>();
            }

            ActorCtrlable = true;
        }

        private void Start()
        {
            AddListener();
            GameResume();
        }

        /// <summary>
        /// 用于代码设置 actor
        /// </summary>
        /// <param name="newActor"></param>
        public void SetActor(ActorCtrl newActor)
        {
            actor = newActor;
        }

        /// <summary>
        /// 设置通用角色头顶提示气泡是否允许创建。
        /// 调用边界：仅由需要接管提示表现的关卡控制器在运行态调用；默认值为 true。
        /// </summary>
        public void SetHintBubbleEnabled(bool enabled)
        {
            hintBubbleEnabled = enabled;
            if (enabled || bubble == null)
                return;

            bubble.CloseStraightly();
            bubble = null;
        }

        /// <summary>
        /// 添加事件监听
        /// </summary>
        public virtual void AddListener()
        {
            EventsUtils.ResetEvents(ref _subscriber);
            _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
            _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
            _subscriber.Subscribe<PlayerMoveCancelEvent>(OnPlayerMoveCancel);
            _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);

            _subscriber.Subscribe<GamePauseEvent>(OnGamePause);
            _subscriber.Subscribe<GameResumeEvent>(OnGameResume);

            _subscriber.Subscribe<OnTriggerTrapEvent>(OnTriggerTrap);
            _subscriber.Subscribe<OnTriggerFailAreaEvent>(OnTriggerFailArea);
            _subscriber.Subscribe<ShowHintBubbleEvent>(OnShowHintBubbleEvent);
        }

        private void OnShowHintBubbleEvent(ShowHintBubbleEvent @event)
        {
            if (!hintBubbleEnabled || isFinished || @event == null || string.IsNullOrEmpty(@event.Data.HintsDesc))
                return;

            if (actor == null)
                actor = FindFirstObjectByType<ActorCtrl>();

            if (actor == null)
            {
                this.LogWarning("[BaseLevelCtrl] OnShowHintBubbleEvent skipped: actor is null.");
                return;
            }

            if (bubble != null)
            {
                bubble.CloseStraightly();
                bubble = null;
            }

            var bubblePrefab = Resources.Load<HintBubble>("HintBubble");
            if (bubblePrefab == null)
            {
                this.LogWarning("[BaseLevelCtrl] OnShowHintBubbleEvent skipped: HintBubble prefab not found in Resources.");
                return;
            }

            bubble = Instantiate(bubblePrefab, transform);
            if (bubble == null)
            {
                this.LogWarning("[BaseLevelCtrl] OnShowHintBubbleEvent skipped: instantiate HintBubble failed.");
                return;
            }

            bubble.Show(@event.Data.HintsDesc, actor.transform, @event.Data.HintsDisappearTime);
        }

        protected void OnGamePause(GamePauseEvent evt)
        {
            // 暂停游戏也不要停止 actor 的功能，因为这个版本 sdk bug 导致 timescale 不能随便设置
            // isPause = true;
            actor.StopMoving();
        }
        protected void OnGameResume(GameResumeEvent evt)
        {
            GameResume();
        }

        private void GameResume()
        {
            isPause = false;
            actor.ResumeMoving();
        }

        #region Movement

        /// <summary>
        /// 开关，用于管理用户输入输出是否生效
        /// </summary>
        /// <param name="isOpen"></param>
        public void SetCtrlable(bool isOpen)
        {
            ActorCtrlable = isOpen;
        }

        /// <summary>
        /// 玩家向右移动事件处理
        /// </summary>
        private void OnPlayerRightMove(PlayerRightMoveEvent evt)
        {
            if (!ActorCtrlable)
                return;
            if (actor != null)
            {
                actor.SetMoveInput(1f);
                // this.Log("Right Moving ----------------- ");
            }
        }

        /// <summary>
        /// 玩家向左移动事件处理
        /// </summary>
        private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
        {
            if (!ActorCtrlable)
                return;
            if (actor != null)
            {
                actor.SetMoveInput(-1f);
                // this.Log("Left Moving ----------------- ");
            }
        }

        /// <summary>
        /// 取消移动事件处理
        /// </summary>
        private void OnPlayerMoveCancel(PlayerMoveCancelEvent evt)
        {
            if (!ActorCtrlable)
                return;

            StopMove();
        }

        private void StopMove()
        {
            if (actor != null)
            {
                actor.SetMoveInput(0f);
                actor.UpdateAnimation();

                // this.Log("------------ Level StopMove");
            }
        }

        /// <summary>
        /// 玩家跳跃事件处理
        /// </summary>
        private void OnPlayerJump(PlayerJumpEvent evt)
        {
            if (actor != null)
            {
                actor.TriggerJump();
            }
        }

        #endregion


        void Update()
        {
            if (isFinished || isPause)
                return;

            CheckFinished();
            Actor.OnUpdate();
        }

        #region Success  or Failed


        // 当前关卡操作结束
        private void FinishedLevel()
        {
            StopMove();
            isFinished = true;

            if (bubble)
            {
                bubble.CloseStraightly();
            }
        }
        // 23 关需求
        public void SuccessStrightly()
        {
            IsActorEnterDoor = true;
            CheckFinished();
        }

        // enter door trigger
        public void OnActorTrigger2D(Collider2D collider)
        {
            var layerName = LayerMask.LayerToName(collider.gameObject.layer);
            this.Log("trigger layer: " + layerName);
            switch (layerName)
            {
                // success
                case "Actor":
                    IsActorEnterDoor = true;
                    break;
                default:
                    this.Log("trigger layer: " + layerName);
                    break;
            }
        }

        // 玩家触发陷阱
        private void OnTriggerTrap(OnTriggerTrapEvent evt)
        {
            if (isFinished)
                return;

            TryToStopCameraFollow();
            WaitPlayDie(1.5f).Forget();
        }


        private void OnTriggerFailArea(OnTriggerFailAreaEvent evt)
        {
            TryToStopCameraFollow();
            WaitingFail().Forget();
        }

        /// <summary>
        /// 尝试停止相机跟随，当触发陷阱或失败区域时调用
        /// </summary>
        private void TryToStopCameraFollow()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            var followArea = mainCamera.GetComponent<CameraFollowArea>();
            if (followArea != null)
            {
                followArea.SetCanFollow(false);
            }
        }

        private async UniTask WaitingFail()
        {
            if (isFinished)
                return;

            isFinished = true;
            AudioManager.PlaySound("failed");

            // Actor.Die();
            await UniTask.WaitForSeconds(0.2f, cancellationToken: this.GetCancellationTokenOnDestroy());

            isFailed = true;
            CheckFinished();
        }

        private async UniTask WaitPlayDie(float waitingTime)
        {
            if (isFinished)
                return;

            isFinished = true;

            Actor.Die();

            if (waitingTime > 0)
                await UniTask.WaitForSeconds(waitingTime, cancellationToken: this.GetCancellationTokenOnDestroy());

            CheckFinished();
        }

        // collision trap

        public virtual bool IsSuccess()
        {
            return IsActorEnterDoor;
        }

        public virtual bool IsFail()
        {
            return isFailed;
            // return Actor.CheckCollisionLayer(FailLayer);
        }

        public virtual void CheckFinished()
        {
            if (IsSuccess())
            {
                FinishedLevel();
                PlaySuccessAnim();

                return;
            }

            if (IsFail() || Actor.IsDied)
            {
                FinishedLevel();
                TryToRestart();

                return;
            }
        }

        private void PlaySuccessAnim()
        {
            if (successAnim)
            {
                actor.StopMoving();
                UniTask<bool> collectTask = TryToCollectStar(successAnim.transform.position, 2.5f);
                successAnim.Play(() =>
                {
                    ContinueSuccessAfterCollect(collectTask).Forget();
                });
            }
            else
            {
                ContinueSuccessAfterCollect(TryToCollectStar(Vector3.zero)).Forget();
            }
        }

        private async UniTask ContinueSuccessAfterCollect(UniTask<bool> collectTask)
        {
            bool isCollected = await collectTask;
            await SuccessStraightly(isCollected ? 1f : 0f);
        }

        private async UniTask SuccessStraightly(float delay = 0)
        {
            await UniTask.WaitForSeconds(straightSuccessDelay + delay, cancellationToken: this.GetCancellationTokenOnDestroy());
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.SUCCESS);
        }

        private async UniTask<bool> TryToCollectStar(Vector3 pos, float delay = 0)
        {
            // check 当前 level Postcard_StarNum.Three 获取了没有。如果有，则不管。如果没有，发送一个 PostcardGetStarEvent 事件
            if (!Application.isPlaying)
                return false;

            if (!PostcardManager.CanCurrentLevelCollectStar())
                return false;

            if (delay > 0f)
            {
                try
                {
                    await UniTask.WaitForSeconds(delay, cancellationToken: this.GetCancellationTokenOnDestroy());
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }

            PlayCtrl playCtrl = PlayCtrl.Instance;
            var levelData = playCtrl?.Level?.CurrentLevelData;
            if (levelData == null || string.IsNullOrWhiteSpace(levelData.Id))
            {
                this.LogWarning("[BaseLevelCtrl] TryToCollectStar skipped because current level id is missing.");
                return false;
            }

            PostcardManager postcardManager = GameManager.Instance?.Postcard;
            if (postcardManager == null)
            {
                this.LogWarning("[BaseLevelCtrl] TryToCollectStar skipped because PostcardManager is missing.");
                return false;
            }

            SimpleBag bag = GameManager.Instance?.Bag;
            if (bag == null)
            {
                this.LogWarning("[BaseLevelCtrl] TryToCollectStar skipped because Bag is missing.");
                return false;
            }

            int starId = (int)Postcard_StarNum.Three;
            if (postcardManager.IsLevelStarCollected(levelData.Id, starId))
                return false;

            if (!postcardManager.TryCollectLevelStar(levelData.Id, starId, out bool isNewCollection))
            {
                this.LogWarning($"[BaseLevelCtrl] TryCollectLevelStar failed. levelId:{levelData.Id}, starId:{starId}");
                return false;
            }

            if (!isNewCollection)
                return false;

            bag.AddTool(GameProps.Star, 1, RewardType.Other);
            this.DispatchEvent(Witness<PostcardGetStarEvent>._, pos, true);

            return true;
        }


        public async UniTask TryToPlayEnterDoorAnim()
        {
            if (!enterDoorAnimationEnabled)
                return;

            if (enterDoorAnim == null)
            {
                enterDoorAnim = transform.Find("StartDoor")?.GetComponent<EnterDoorAnimCtrl>();
            }

            if (enterDoorAnim == null)
                return;

            SetCtrlable(false);
            await enterDoorAnim.Play();
            actor?.ResumeMoving();
            SetCtrlable(true);
        }

        private void BindSuccessAnimActorTarget()
        {
            if (successAnim == null || actor == null)
                return;

            var animHandle = successAnim.transform.GetComponent<ActorDestroyAnimHandle>();
            if (animHandle != null)
            {
                animHandle.SetTarget(actor.transform);
            }
        }

        private async void TryToRestart()
        {
            await UniTask.WaitForSeconds(1f, cancellationToken: this.GetCancellationTokenOnDestroy());
            this.DispatchEvent(Witness<GameResetEvent>._, GameResetType.Failed);
        }

        #endregion

        /// <summary>
        /// 销毁关卡时清理
        /// </summary>
        public virtual void DestroyLevel()
        {
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            EventsUtils.ResetEvents(ref _subscriber);
            if (actor != null)
            {
                actor.SetMoveInput(0f);
            }
        }

#if UNITY_EDITOR
        // 用于自动化绑定一些内容，懒狗必备
        [Button("Auto assigned level")]
        private void AutoAssigned()
        {
            if (actor == null)
            {
                actor = transform.Find("Actor")?.GetComponent<ActorCtrl>();
                actor.GetComponent<SortingGroup>().sortingOrder = 200;
            }

            SetSuccessAnim(transform.Find("door_0")?.GetComponent<SuccessAnimCtrl>());

            // 资源遗留问题
            var mask = transform.Find("Mask");
            if (mask != null)
            {
                DestroyImmediate(mask.gameObject);
            }

            var bg = transform.Find("CommonBg");
            if (bg == null)
            {
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Level/Background/CommonBg.prefab");
                Debug.Log("Auto assigned --------" + obj);
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Level/Background/CommonBg.prefab"), transform);
            }

            var btn = transform.Find("StartDoor");
            if (btn)
                SetEnterDoorAnim(btn.GetComponent<EnterDoorAnimCtrl>());
            else
            {
                var ctrl = GetComponentInChildren<EnterDoorAnimCtrl>(true);
                ctrl.gameObject.name = "StartDoor";
                SetEnterDoorAnim(ctrl);
            }

        }

#endif
    }
}
