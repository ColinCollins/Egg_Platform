using System.Collections.Generic;
using Bear.Logger;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 按下检测组件：通过 raycast 检测鼠标/触摸是否点击到指定 tag 的物体
/// 按下时触发 OnPress，松开时触发 CancelPress
/// </summary>
public class PressItemEventHandle : MonoBehaviour, IDebuger
{
    [Header("检测设置")]
    [Tooltip("目标 tag（如 'enemy'）")]
    [SerializeField] private string targetTag = "enemy";

    [Tooltip("可选：指定目标 Transform（检测该物体或其子物体），为空则检测所有匹配 tag 的物体")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("触摸检测时最多检查的手指数，范围 1~3")]
    [SerializeField] private int maxTouchCount = 1;

    [Tooltip("用于 raycast 的相机（为空则使用 Main Camera）")]
    [SerializeField] private Camera raycastCamera;

    [Header("事件")]
    [SerializeField] public UnityEvent OnPressEvent;

    [SerializeField] public UnityEvent OnCancelPressEvent;

    private bool isPressing = false;
    private readonly List<Vector2> _inputPositions = new List<Vector2>(4);

    void Update()
    {
        CheckRaycastInput();
    }

    private void CheckRaycastInput()
    {
        bool inputDown = false;
        bool inputUp = false;
        _inputPositions.Clear();
#if UNITY_EDITOR
        // 鼠标输入
        if (Input.GetMouseButtonDown(0))
        {
            _inputPositions.Add(Input.mousePosition);
            inputDown = true;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            inputUp = true;
        }
        else if (Input.GetMouseButton(0) && !isPressing)
        {
            // 持续按下但还未触发 OnPress
            _inputPositions.Add(Input.mousePosition);
            inputDown = true;
        }
#endif
        // 触摸输入（支持多指，最多检查 maxTouchCount 根手指，且不超过 10）
        if (Input.touchCount > 0)
        {
            // 每次循环重新获取触摸数量，避免索引越界
            int touchLimit = Mathf.Clamp(maxTouchCount, 1, 10);
            
            // 先看是否有任意一根手指结束/取消，用于取消按压
            // 检查所有手指，不限制数量，确保能检测到抬起
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    inputUp = true;
                    this.Log($"手指 {touch.fingerId} 抬起/取消");
                    break;
                }
            }

            // 如果当前未处于按压状态，再收集新的按下位置
            if (!isPressing)
            {
                int checkCount = Mathf.Min(touchLimit, Input.touchCount);
                for (int i = 0; i < checkCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Canceled && touch.phase != TouchPhase.Ended)
                    {
                        _inputPositions.Add(touch.position);
                        inputDown = true;
                        this.Log($"手指 {touch.fingerId} 按下, _inputPositions.Count = {_inputPositions.Count}, pos = {touch.position}");
                    }
                }
            }
        }

        // 松开时取消
        if (inputUp && isPressing)
        {
            OnCancelPress();
            this.Log("is cancel press");
            return;
        }

        // 按下时检测 raycast
        if (inputDown && !isPressing && _inputPositions.Count > 0)
        {
            bool hit = false;
            for (int i = 0; i < _inputPositions.Count; i++)
            {
                this.Log($"check position: {i}:  " + _inputPositions[i]);
                if (RaycastHitTarget(_inputPositions[i]))
                {
                    hit = true;
                    break;
                }
            }

            if (hit)
            {
                OnPress();
            }
        }
    }

    private bool RaycastHitTarget(Vector2 screenPosition)
    {
        Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
        if (cam == null) return false;

        // 方法1：使用 ScreenPointToRay 进行 2D 射线检测（更可靠）
        Ray ray = cam.ScreenPointToRay(screenPosition);
        
        // 计算射线与 z=0 平面的交点（假设 2D 游戏在 z=0 平面）
        float distance = Mathf.Abs(ray.origin.z / ray.direction.z);
        if (float.IsInfinity(distance) || float.IsNaN(distance))
        {
            // 射线与 z=0 平面平行，使用相机距离
            distance = cam.nearClipPlane;
        }
        
        Vector2 worldPoint2D = ray.GetPoint(distance);
        this.Log($"ScreenPos: {screenPosition}, Ray distance: {distance}, WorldPos: {worldPoint2D}");
        
        // 2D Raycast：从计算出的世界点向 z 轴负方向发射短射线
        RaycastHit2D hit2D = Physics2D.Raycast(worldPoint2D, Vector2.zero, 0.1f);

        this.Log("hit worldPosition - 1: " + worldPoint2D);
        
        // 如果没命中，尝试从相机位置发射长射线（适用于 2D 物体在不同 z 层的情况）
        if (hit2D.collider == null)
        {
            Vector2 rayOrigin = new Vector2(ray.origin.x, ray.origin.y);
            Vector2 rayDirection = new Vector2(ray.direction.x, ray.direction.y).normalized;
            hit2D = Physics2D.Raycast(rayOrigin, rayDirection, Mathf.Infinity);
            
            this.Log($"尝试从相机发射 2D 射线: origin={rayOrigin}, hit={hit2D.collider != null}");
        }
        
        if (hit2D.collider != null)
        {
            this.Log("hit worldPosition - 2: " + hit2D.point);
            GameObject hitObj = hit2D.collider.gameObject;
            if (hitObj.CompareTag(targetTag))
            {
                this.Log("hit worldPosition - 3: " + hit2D.point);
                // 如果指定了 targetTransform，检查是否是目标或其子物体
                if (targetTransform == null || hitObj.transform == targetTransform || hitObj.transform.IsChildOf(targetTransform))
                {
                    this.Log("hit worldPosition - 4: " + hit2D.point);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 按下时触发
    /// </summary>
    public void OnPress()
    {
        isPressing = true;
        OnPressEvent?.Invoke();
    }

    /// <summary>
    /// 取消按下（松开时触发）
    /// </summary>
    public void OnCancelPress()
    {
        isPressing = false;
        OnCancelPressEvent?.Invoke();
    }

    /// <summary>
    /// 设置目标 tag
    /// </summary>
    public void SetTargetTag(string tag)
    {
        targetTag = tag;
    }

    /// <summary>
    /// 设置目标 Transform
    /// </summary>
    public void SetTargetTransform(Transform target)
    {
        targetTransform = target;
    }

    /// <summary>
    /// 手动触发 OnPress
    /// </summary>
    public void TriggerPress()
    {
        OnPress();
    }

    /// <summary>
    /// 手动触发 CancelPress
    /// </summary>
    public void TriggerCancelPress()
    {
        OnCancelPress();
    }

#if DEBUG_MODE
    // 缓存圆形纹理
    private Texture2D _circleTexture;
    private Texture2D _circleTextureRed;
    private Texture2D _circleTextureGreen;
    private Texture2D _circleTextureBlue;
    
    private const int CIRCLE_SIZE = 80;
    
    /// <summary>
    /// 创建圆形纹理
    /// </summary>
    private Texture2D CreateCircleTexture(Color color)
    {
        int size = CIRCLE_SIZE;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        
        float center = size / 2f;
        float radius = size / 2f - 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                if (dist <= radius)
                {
                    float alpha = dist > radius - 4f ? (radius - dist) / 4f : 1f;
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha * 0.6f);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
    
    private void OnGUI()
    {
        if (_circleTexture == null) _circleTexture = CreateCircleTexture(Color.yellow);
        if (_circleTextureRed == null) _circleTextureRed = CreateCircleTexture(Color.red);
        if (_circleTextureGreen == null) _circleTextureGreen = CreateCircleTexture(Color.green);
        if (_circleTextureBlue == null) _circleTextureBlue = CreateCircleTexture(Color.cyan);
        
        Texture2D GetTextureByPhase(TouchPhase phase)
        {
            switch (phase)
            {
                case TouchPhase.Began: return _circleTextureGreen;
                case TouchPhase.Moved: return _circleTextureRed;
                case TouchPhase.Stationary: return _circleTextureBlue;
                default: return _circleTexture;
            }
        }
        
        // 绘制触摸点圆形标记
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            Vector2 pos = touch.position;
            
            Rect rect = new Rect(
                pos.x - CIRCLE_SIZE / 2f, 
                Screen.height - pos.y - CIRCLE_SIZE / 2f,
                CIRCLE_SIZE, 
                CIRCLE_SIZE
            );
            
            Texture2D tex = GetTextureByPhase(touch.phase);
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            
            GUI.Label(rect, touch.fingerId.ToString(), style);
        }
        
#if UNITY_EDITOR
        if (Input.GetMouseButton(0))
        {
            Vector2 mousePos = Input.mousePosition;
            Rect mouseRect = new Rect(
                mousePos.x - CIRCLE_SIZE / 2f,
                Screen.height - mousePos.y - CIRCLE_SIZE / 2f,
                CIRCLE_SIZE,
                CIRCLE_SIZE
            );
            GUI.DrawTexture(mouseRect, _circleTextureGreen, ScaleMode.ScaleToFit, true);
            
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(mouseRect, "M", style);
        }
#endif
        
        // 图例说明
        float legendX = Screen.width - 250;
        float legendY = 10;
        float lineHeight = 35;
        
        GUIStyle legendStyle = new GUIStyle(GUI.skin.label);
        legendStyle.fontSize = 18;
        legendStyle.normal.textColor = Color.white;
        
        GUI.Label(new Rect(legendX, legendY, 200, lineHeight), "=== Touch Debug ===", legendStyle);
        legendY += lineHeight;
        
        GUI.DrawTexture(new Rect(legendX, legendY + 5, 25, 25), _circleTextureGreen, ScaleMode.ScaleToFit, true);
        GUI.Label(new Rect(legendX + 30, legendY, 150, lineHeight), "Began", legendStyle);
        legendY += lineHeight;
        
        GUI.DrawTexture(new Rect(legendX, legendY + 5, 25, 25), _circleTextureRed, ScaleMode.ScaleToFit, true);
        GUI.Label(new Rect(legendX + 30, legendY, 150, lineHeight), "Moved", legendStyle);
        legendY += lineHeight;
        
        GUI.DrawTexture(new Rect(legendX, legendY + 5, 25, 25), _circleTextureBlue, ScaleMode.ScaleToFit, true);
        GUI.Label(new Rect(legendX + 30, legendY, 150, lineHeight), "Stationary", legendStyle);
        legendY += lineHeight;
        
        GUI.Label(new Rect(legendX, legendY, 200, lineHeight), $"isPressing: {isPressing}", legendStyle);
    }
#endif
}
