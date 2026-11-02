using Bear.Logger;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.ItemEvent
{
    /// <summary>
    /// 在规定时间内点击指定物体指定次数后触发
    /// </summary>
    public class KnockKnock : BaseItemEventHandle, IDebuger
    {
        [SerializeField] private GameObject Target;
        [SerializeField] private int RequiredCount = 3;
        [SerializeField] private float TimeLimit = 5f;

        [SerializeField] private ParticleSystem knockVfx;

        [SerializeField] private Collider2D collider;

        [SerializeField] private ActorSpineCtrl doorSpineCtrl;

        private Camera mainCamera;

        private Vector3 inputPosition;

        private int currentCount = 0;
        private float elapsedTime = 0f;


        void Awake()
        {
            Execute();
        }

        public override void Execute()
        {
            currentCount = 0;
            elapsedTime = 0f;
            IsRunning = true;
            IsDone = false;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        void Update()
        {
            if (!IsRunning || IsDone)
                return;

            elapsedTime += Time.deltaTime;

            // 超时重置
            if (elapsedTime >= TimeLimit)
            {
                ResetState();
                return;
            }

            // 检测点击/触摸
            if (CheckClick())
            {
                currentCount++;
                PerTrigger();

                if (currentCount >= RequiredCount)
                {
                    Trigger();
                    IsDone = true;
                    IsRunning = false;
                }
            }
        }

        private bool CheckClick()
        {
            inputPosition = Vector2.zero;
            bool isClick = false;

#if UNITY_ANDROID || UNITY_IOS
            // 移动端触摸检测
            if (Input.touchCount > 0)
            {
                int count = Mathf.Min(Input.touchCount, 3);
                for (int i = 0; i < count; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began)
                    {
                        // 检查坐标是否在 UI 上
                        if (InputUtils.IsPointerOverUI(touch.position))
                        {
                            continue;
                        }
                        
                        inputPosition = touch.position;
                        isClick = true;
                    }
                }
            }
#elif UNITY_EDITOR
            // PC端鼠标点击检测
            if (Input.GetMouseButtonDown(0))
            {
                // 检查坐标是否在 UI 上
                if (InputUtils.IsPointerOverUI(Input.mousePosition))
                {
                    return false;
                }

                inputPosition = Input.mousePosition;
                isClick = true;
            }
#endif

            if (!isClick)
                return false;

            // 射线检测
            if (mainCamera == null || Target == null)
                return false;

            RaycastHit2D hit = Physics2D.Raycast(
                mainCamera.ScreenToWorldPoint(inputPosition),
                Vector2.zero
            );

            if (hit.collider != null && hit.collider.gameObject == Target)
            {
                return true;
            }

            return false;
        }

        private void ResetState()
        {
            currentCount = 0;
            elapsedTime = 0f;
            // IsRunning 保持 true，任务继续运行，只是重置计数和时间
        }

        private void PerTrigger()
        {
            this.Log($"Knock count: {currentCount}");

            if (knockVfx != null)
            {
                var worldPos = mainCamera.ScreenToWorldPoint(inputPosition);
                worldPos.z = 0;
                knockVfx.transform.position = worldPos;
                knockVfx.Play();
            }

            AudioManager.PlaySound("knockDoor");
        }

        /// <summary>
        /// 触发函数，当达到点击次数时调用
        /// </summary>
        private void Trigger()
        {
            // 可以在这里添加触发逻辑
            // 例如：播放音效、触发动画、发送事件等
            IsDone = true;

            // 执行 Execute
            collider.enabled = true;
            doorSpineCtrl.PlayAnimation("door_open", false);
            AudioManager.PlaySound("openDoor");
        }
    }
}