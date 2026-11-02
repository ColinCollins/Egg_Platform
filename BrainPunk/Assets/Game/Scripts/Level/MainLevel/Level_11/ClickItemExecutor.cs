using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using Bear.EventSystem;
using Game.Events;
using Game.Play;

namespace Game.ItemEvent
{
    /// <summary>
    /// 点击物体执行器基类
    /// 点击指定物体后触发子事件
    /// </summary>
    [Preserve]
    public class ClickItemExecutor : BaseItemExecutor
    {
        [SerializeField] protected GameObject Target;
        [SerializeField] protected Camera mainCamera;

        protected bool isClickTaskDone = false;
        private readonly List<Vector2> _inputPositions = new List<Vector2>(4);
        private EventSubscriber _subscriber;
        private bool _isPause;

        void Awake()
        {
            Execute();
        }

        private void Start()
        {
            AddListener();
        }

        private void OnDestroy()
        {
            EventsUtils.ResetEvents(ref _subscriber);
        }

        private void AddListener()
        {
            EventsUtils.ResetEvents(ref _subscriber);
            _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        }

        private void OnSwitchGameState(SwitchGameStateEvent evt)
        {
            _isPause = !evt.NewState.Equals(GamePlayStateName.PLAYING);
        }

        public override void Execute()
        {
            // 初始化点击任务状态
            isClickTaskDone = false;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        protected override void OnUpdate()
        {
            if (_isPause)
            {
                return;
            }

            // 如果点击任务未完成，处理点击检测
            if (!isClickTaskDone)
            {
                // 检测点击/触摸
                if (CheckClick())
                {
                    isClickTaskDone = true;
                    // 点击任务完成，执行子事件
                    base.Execute();
                }
            }
            else
            {
                // 点击任务已完成，执行父类的更新逻辑（管理子事件）
                base.OnUpdate();

                // 尝试重置点击
                TryToResetClick();
            }
        }

        private void TryToResetClick()
        {
            if (!isRunning)
            {
                isClickTaskDone = false;
            }
        }

        /// <summary>
        /// 检测点击/触摸输入
        /// </summary>
        protected virtual bool CheckClick()
        {
            if (mainCamera == null || Target == null)
                return false;

            _inputPositions.Clear();

#if UNITY_ANDROID || UNITY_IOS
            // 移动端触摸检测（支持多点，最多检查 3 根手指）
            if (Input.touchCount > 0)
            {
                int maxTouches = Mathf.Min(3, Input.touchCount);
                for (int i = 0; i < maxTouches; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began)
                    {
                        _inputPositions.Add(touch.position);
                    }
                }
            }
#endif
            // PC端鼠标点击检测（在移动端也支持，用于编辑器测试）
            if (Input.GetMouseButtonDown(0))
            {
                _inputPositions.Add(Input.mousePosition);
            }

            if (_inputPositions.Count == 0)
                return false;

            // 射线检测
            for (int i = 0; i < _inputPositions.Count; i++)
            {
                Vector2 inputPosition = _inputPositions[i];

                RaycastHit2D hit = Physics2D.Raycast(
                    mainCamera.ScreenToWorldPoint(inputPosition),
                    Vector2.zero
                );

                if (hit.collider != null && hit.collider.gameObject == Target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
