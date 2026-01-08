using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using Game.Play;
using UnityEngine;

namespace Game.Level
{

    /// <summary>
    /// 关卡控制类， 用于控制场景中的基本内容，可以用于拓展
    /// 需要绑定在 Level prefab 上
    /// </summary>
    public class BaseLevelCtrl : MonoBehaviour, IDebuger, IEventSender
    {
        [SerializeField] private LayerMask SuccessLayer;

        [SerializeField] private ActorCtrl actor;
        public ActorCtrl Actor => actor;

        // 事件订阅器
        private EventSubscriber _subscriber;

        // 移动状态
        private bool isMovingRight = false;
        private bool isMovingLeft = false;

        protected bool isPause = false;
        protected bool isFinished = false;

        private void Awake()
        {
            // 确保 Actor 已赋值
            if (actor == null)
            {
                actor = FindObjectOfType<ActorCtrl>();
            }
        }

        private void Start()
        {
            AddListener();
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

        }


        private void OnGamePause(GamePauseEvent evt)
        {
            isPause = true;
        }
        private void OnGameResume(GameResumeEvent evt)
        {
            isPause = false;
        }

        #region Movement


        /// <summary>
        /// 玩家向右移动事件处理
        /// </summary>
        private void OnPlayerRightMove(PlayerRightMoveEvent evt)
        {
            if (actor != null)
            {
                isMovingRight = true;
                actor.SetMoveInput(1f);
            }
        }

        /// <summary>
        /// 玩家向左移动事件处理
        /// </summary>
        private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
        {
            if (actor != null)
            {
                isMovingLeft = true;
                actor.SetMoveInput(-1f);
            }
        }

        /// <summary>
        /// 取消移动事件处理
        /// </summary>
        private void OnPlayerMoveCancel(PlayerMoveCancelEvent evt)
        {
            if (actor != null)
            {
                isMovingRight = false;
                isMovingLeft = false;
                actor.SetMoveInput(0f);
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
        }

        public virtual bool IsSuccess()
        {
            return Actor.CheckCollisionLayer(SuccessLayer);
        }

        public virtual void CheckFinished()
        {
            if (IsSuccess())
            {
                isFinished = true;
                this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.SUCCESS);
            }
        }


        /// <summary>
        /// 销毁关卡时清理
        /// </summary>
        public virtual void DestroyLevel()
        {
            EventsUtils.ResetEvents(ref _subscriber);

            // 重置移动状态
            isMovingRight = false;
            isMovingLeft = false;

            if (actor != null)
            {
                actor.SetMoveInput(0f);
            }
        }

        private void OnDestroy()
        {
            DestroyLevel();
        }
    }

}
