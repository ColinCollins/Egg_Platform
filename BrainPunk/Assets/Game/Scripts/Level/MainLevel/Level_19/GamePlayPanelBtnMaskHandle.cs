using System.Collections.Generic;
using UnityEngine;

public class GamePlayPanelBtnMaskHandle : MonoBehaviour
{
    [Header("碰撞体设置")]
    [SerializeField] private Vector2 colliderSize = new Vector2(1f, 1f); // BoxCollider 大小
    [SerializeField] private float colliderZPosition = 0f; // 碰撞体的 Z 轴位置
    [SerializeField] private Camera sceneCamera; // 场景相机（用于坐标转换，为空则使用主相机）

    private GamePlayPanel_DragButtons dragButtons;
    private List<GameObject> worldColliders; // 对应的世界空间碰撞体
    private List<RectTransform> buttonRects; // 按钮的 RectTransform 列表
    private Canvas canvas;
    private Camera uiCamera; // UI Canvas 使用的相机
    private Camera worldCamera; // 场景世界相机
    private bool isInitialized = false;

    void Start()
    {
        worldColliders = new List<GameObject>();
        buttonRects = new List<RectTransform>();

        // 初始化场景相机
        if (sceneCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                worldCamera = FindFirstObjectByType<Camera>();
            }
        }
        else
        {
            worldCamera = sceneCamera;
        }
    }

    void OnEnable()
    {
        // 延迟初始化，确保 PlayCtrl 已经初始化
        if (!isInitialized)
        {
            Invoke(nameof(InitializeColliders), 0.1f);
        }
    }

    void OnDisable()
    {
        // 取消延迟调用
        CancelInvoke(nameof(InitializeColliders));
    }

    void Update()
    {
        UpdateColliderPositions();
    }

    /// <summary>
    /// 初始化碰撞体
    /// </summary>
    private void InitializeColliders()
    {
        if (isInitialized)
            return;

        // 获取 GamePlayPanel_DragButtons
        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
        {
            Debug.LogWarning("[GamePlayPanelBtnMaskHandle] PlayCtrl or CurrentGamePlayPanel is null, will retry later");
            // 如果还没准备好，稍后重试
            Invoke(nameof(InitializeColliders), 0.1f);
            return;
        }

        // 获取 Canvas
        canvas = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[GamePlayPanelBtnMaskHandle] Canvas not found");
            return;
        }

        // 获取 UI 相机
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
        {
            uiCamera = canvas.worldCamera;
        }
        else if (canvas.renderMode == RenderMode.WorldSpace)
        {
            uiCamera = canvas.worldCamera;
        }
        else
        {
            uiCamera = null; // Screen Space - Overlay 模式不需要相机
        }

        // 获取按钮列表
        List<Transform> buttons = new List<Transform>();
        var btn = PlayCtrl.Instance.CurrentGamePlayPanel.transform.Find("Root/leftMove_btn");
        buttons.Add(btn);
        btn = PlayCtrl.Instance.CurrentGamePlayPanel.transform.Find("Root/rightMove_btn");
        buttons.Add(btn);
        btn = PlayCtrl.Instance.CurrentGamePlayPanel.transform.Find("Root/jump_btn");
        buttons.Add(btn);

        if (buttons == null || buttons.Count == 0)
        {
            Debug.LogWarning("[GamePlayPanelBtnMaskHandle] No buttons found");
            return;
        }

        // 为每个按钮创建对应的碰撞体
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null)
            {
                RectTransform buttonRect = buttons[i].GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    buttonRects.Add(buttonRect);
                    CreateColliderForButton(buttons[i], i);
                }
            }
        }

        isInitialized = true;
    }

    /// <summary>
    /// 为按钮创建对应的碰撞体
    /// </summary>
    private void CreateColliderForButton(Transform button, int index)
    {
        // 创建空物体
        GameObject colliderObj = new GameObject($"ButtonCollider_{index}_{button.name}");
        colliderObj.transform.SetParent(transform); // 设置为当前对象的子物体
        colliderObj.layer = LayerMask.NameToLayer("Ground");

        // 添加 BoxCollider2D
        BoxCollider2D boxCollider = colliderObj.AddComponent<BoxCollider2D>();
        boxCollider.size = colliderSize;
        boxCollider.isTrigger = true;

        // 设置初始位置
        colliderObj.transform.position = new Vector3(0, 0, colliderZPosition);
        
        worldColliders.Add(colliderObj);
    }

    /// <summary>
    /// 更新碰撞体位置，使其与 UI 按钮位置同步
    /// </summary>
    private void UpdateColliderPositions()
    {
        if (canvas == null || worldCamera == null)
            return;

        if (buttonRects == null || buttonRects.Count != worldColliders.Count)
            return;

        for (int i = 0; i < buttonRects.Count && i < worldColliders.Count; i++)
        {
            if (buttonRects[i] != null && worldColliders[i] != null)
            {
                // 将 UI 按钮位置转换为场景世界位置
                Vector3 worldPosition = ConvertUIToWorldPosition(buttonRects[i]);

                // 更新碰撞体位置
                worldColliders[i].transform.position = worldPosition;
            }
        }
    }

    /// <summary>
    /// 将 UI 按钮位置转换为场景世界位置
    /// </summary>
    private Vector3 ConvertUIToWorldPosition(RectTransform buttonRect)
    {
        if (canvas == null || worldCamera == null)
            return Vector3.zero;

        Vector2 screenPoint;

        // 根据 Canvas 渲染模式获取屏幕坐标
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Screen Space - Overlay: 直接使用 RectTransform 的世界位置转换为屏幕坐标
            screenPoint = RectTransformUtility.WorldToScreenPoint(null, buttonRect.position);
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && uiCamera != null)
        {
            // Screen Space - Camera: 使用 UI 相机转换
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, buttonRect.position);
        }
        else if (canvas.renderMode == RenderMode.WorldSpace)
        {
            // World Space: 已经是世界坐标，直接使用
            return new Vector3(buttonRect.position.x, buttonRect.position.y, colliderZPosition);
        }
        else
        {
            // 默认情况
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera ?? worldCamera, buttonRect.position);
        }

        // 将屏幕坐标转换为场景世界坐标
        // 使用合适的 Z 距离（可以根据需要调整）
        float zDistance = Mathf.Abs(worldCamera.transform.position.z - colliderZPosition);
        Vector3 worldPos = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDistance));

        // 保持指定的 Z 轴位置
        worldPos.z = colliderZPosition;

        return worldPos;
    }

    /// <summary>
    /// 清理碰撞体
    /// </summary>
    void OnDestroy()
    {
        if (worldColliders != null)
        {
            foreach (GameObject obj in worldColliders)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            worldColliders.Clear();
        }
    }
}
