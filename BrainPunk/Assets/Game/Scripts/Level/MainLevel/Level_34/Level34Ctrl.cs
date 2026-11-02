using System.Threading.Tasks;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;

public class Level34Ctrl : MonoBehaviour, IEventSender
{
    [SerializeField] private Transform leftBtn;
    [SerializeField] private Transform rightBtn;
    [SerializeField] private Transform jumpBtn;

    [SerializeField] private Camera mainCamera;
    [SerializeField] private float colliderZPosition = 0f;

    [SerializeField] private MMF_Player cameraFeedback;

    [SerializeField] private MMF_Player[] btnsFeedback;

    [SerializeField] private DragableItem ground;

    [Header("Init Button Scale")]
    [SerializeField] private float initButtonScaleDuration = 0.3f;
    [SerializeField] private float initButtonDelayStep = 0.06f;

    [SerializeField] private float initButtonDelayOffset = 0.30f;

    private Canvas canvas;
    private Camera uiCamera;

    private bool hasPlayCameraShake;

    [Header("拖拽设置")]
    [SerializeField] private float dragSensitivity = 1f; // 拖拽灵敏度

    [MaxValue(0)]
    [SerializeField] private float dragLimitY = -1f; // 拖拽灵敏度

    private bool isDragging = false;
    private float dragStartY; // 拖拽开始时的屏幕 Y 坐标
    private Vector3 cameraStartPosition; // 拖拽开始时相机的世界位置
    private bool isRightDown;
    private bool isLeftDown;

    private Vector3 _leftBtnInitScale;
    private Vector3 _rightBtnInitScale;
    private Vector3 _jumpBtnInitScale;

    void Awake()
    {
        _leftBtnInitScale = leftBtn != null ? leftBtn.localScale : Vector3.one;
        _rightBtnInitScale = rightBtn != null ? rightBtn.localScale : Vector3.one;
        _jumpBtnInitScale = jumpBtn != null ? jumpBtn.localScale : Vector3.one;
    }

    void Start()
    {
        hasPlayCameraShake = false;
        InitButtons();
    }

    private async Task InitButtons()
    {
        await UniTask.WaitForSeconds(0.5f, ignoreTimeScale: true);
        InitializeButtonPositions();
    }

    #region button 
    /// <summary>
    /// 初始化按钮位置，将 UI 按钮位置转换为场景世界坐标
    /// </summary>
    private void InitializeButtonPositions()
    {
        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
        {
            Debug.LogWarning("[Level34Ctrl] PlayCtrl or CurrentGamePlayPanel is null");
            return;
        }

        // 获取相机
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = FindFirstObjectByType<Camera>();
        }

        if (mainCamera == null)
        {
            Debug.LogWarning("[Level34Ctrl] Main camera not found");
            return;
        }

        // 获取 Canvas
        canvas = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogWarning("[Level34Ctrl] Canvas not found");
            return;
        }

        // 获取 UI 相机
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
            uiCamera = canvas.worldCamera;
        else if (canvas.renderMode == RenderMode.WorldSpace)
            uiCamera = canvas.worldCamera;
        else
            uiCamera = null;

        // 获取 UI 按钮的 RectTransform
        Transform panelRoot = PlayCtrl.Instance.CurrentGamePlayPanel.transform.GetChild(0);

        RectTransform leftMoveBtnRect = panelRoot.Find("leftMove_btn") as RectTransform;
        RectTransform rightMoveBtnRect = panelRoot.Find("rightMove_btn") as RectTransform;
        RectTransform jumpBtnRect = panelRoot.Find("jump_btn") as RectTransform;

        // 转换 UI 位置到世界坐标并设置到场景对象
        if (leftMoveBtnRect != null && leftBtn != null)
        {
            Vector3 worldPos = CommonUtility.ConvertUIToWorldPosition(leftMoveBtnRect, mainCamera, colliderZPosition);
            leftBtn.position = worldPos;

            leftMoveBtnRect.gameObject.SetActive(false);
        }

        if (rightMoveBtnRect != null && rightBtn != null)
        {
            Vector3 worldPos = CommonUtility.ConvertUIToWorldPosition(rightMoveBtnRect, mainCamera, colliderZPosition);
            rightBtn.position = worldPos;

            rightMoveBtnRect.gameObject.SetActive(false);
        }

        if (jumpBtnRect != null && jumpBtn != null)
        {
            Vector3 worldPos = CommonUtility.ConvertUIToWorldPosition(jumpBtnRect, mainCamera, colliderZPosition);
            jumpBtn.position = worldPos;

            jumpBtnRect.gameObject.SetActive(false);
        }

        if (ground != null)
            ground.SetEnableDrag(true);

        PlayInitButtonScale();
    }

    private void PlayInitButtonScale()
    {
        PlayButtonScale(leftBtn, _leftBtnInitScale, 0);
        PlayButtonScale(rightBtn, _rightBtnInitScale, 1);
        PlayButtonScale(jumpBtn, _jumpBtnInitScale, 2);
    }

    private void PlayButtonScale(Transform target, Vector3 endScale, int order)
    {
        if (target == null)
            return;

        target.DOKill();
        target.localScale = Vector3.zero;
        target.DOScale(endScale, initButtonScaleDuration)
            .SetDelay(order * initButtonDelayStep + initButtonDelayOffset)
            .SetEase(Ease.OutBack)
            .SetLink(target.gameObject);
    }

    #endregion 

    public void PlayShakeCamera()
    {
        if (hasPlayCameraShake)
            return;

        hasPlayCameraShake = true;
        cameraFeedback.PlayFeedbacks();

        PlayButtonsDrop();
    }

    private void PlayButtonsDrop()
    {
        if (btnsFeedback == null || btnsFeedback.Length <= 0)
            return;
        AudioManager.PlaySound("crash");
        AudioManager.PlaySound("level16Drop");
        Debug.Log("-------------  play btns");
        for (int i = 0; i < btnsFeedback.Length; i++)
        {
            btnsFeedback[i].PlayFeedbacks();
        }
        // jumpBtn.DOMoveY(-11f, .8f).SetEase(Ease.InBack);
        StopMove();
    }

    /// <summary>
    /// 开始拖拽，记录触碰开始位置
    /// </summary>
    public void OnDragStart()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = FindFirstObjectByType<Camera>();
        }

        if (mainCamera == null)
            return;

        isDragging = true;

        // 获取当前输入位置（支持移动端和PC端）
        float currentY = GetInputPositionY();
        dragStartY = currentY;
        cameraStartPosition = mainCamera.transform.position;
    }

    /// <summary>
    /// 拖拽更新，相机沿 y 轴移动相反方向
    /// </summary>
    public void OnDragUpdate()
    {
        if (!isDragging || mainCamera == null)
            return;

        // 获取当前输入位置
        float currentY = GetInputPositionY();

        // 计算拖拽偏移量（屏幕坐标）
        float dragDeltaY = currentY - dragStartY;

        // 将屏幕坐标偏移转换为世界坐标偏移
        // 使用相机的 orthographicSize 或视野来计算合适的转换比例
        float worldDeltaY = 0f;
        if (mainCamera.orthographic)
        {
            // 正交相机：使用 orthographicSize 和屏幕高度计算
            float screenHeight = Screen.height;
            float worldHeight = mainCamera.orthographicSize * 2f;
            worldDeltaY = -(dragDeltaY / screenHeight) * worldHeight * dragSensitivity;
        }
        else
        {
            // 透视相机：使用相机到目标平面的距离计算
            float zDistance = Mathf.Abs(mainCamera.transform.position.z - colliderZPosition);
            float screenHeight = Screen.height;
            float worldHeight = 2f * zDistance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            worldDeltaY = -(dragDeltaY / screenHeight) * worldHeight * dragSensitivity;
        }

        // 更新相机位置（沿 y 轴移动相反方向）
        Vector3 newPosition = cameraStartPosition;
        newPosition.y += worldDeltaY;
        newPosition.y = Mathf.Clamp(newPosition.y, dragLimitY, 0);
        mainCamera.transform.position = newPosition;
    }

    /// <summary>
    /// 结束拖拽
    /// </summary>
    public void OnDragEnd()
    {
        isDragging = false;
    }

    /// <summary>
    /// 获取当前输入位置的 Y 坐标（支持移动端和PC端）
    /// </summary>
    private float GetInputPositionY()
    {
#if UNITY_ANDROID || UNITY_IOS
        // 移动端触摸检测
        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).position.y;
        }
        return dragStartY; // 如果没有触摸，返回上次记录的位置
#else
        // PC端鼠标检测
        return Input.mousePosition.y;
#endif
    }

    void Update()
    {
        if (isRightDown)
            this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
        else if (isLeftDown)
            this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);
        else
        {
            this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
        }
    }



    public void ClickLeftMove()
    {
        isLeftDown = true;
        Debug.Log("-----------test");
        //this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);
    }

    public void ClickRightMove()
    {
        isRightDown = true;
        Debug.Log("-----------test");
        //this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
    }

    public void StopMove()
    {
        isLeftDown = false;
        isRightDown = false;
        this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
    }

    public void ClickJump()
    {
        Debug.Log("-----------test");
        this.DispatchEvent(Witness<PlayerJumpEvent>._);
    }
}
