using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 水花控制器
/// - Idle 状态下等待 IdleInterval 后自动触发 short splash（不受触碰影响）
/// - 玩家触碰一个 Collider 时，另一个 Animator 额外触发 long splash
/// - 玩家同时触碰两个 Collider 时，不触发 long splash
/// </summary>
public class WaterSplashCtrl : MonoBehaviour
{
    #region 配置参数

    [Header("时间设置")]
    [Tooltip("Idle 状态持续时间")]
    [SerializeField] private float IdleInterval = 3f;

    [Tooltip("Splash 动画持续时间")]
    [SerializeField] private float SplashDuring = 3f;

    [Header("组件引用")]
    [Tooltip("Animator 列表")]
    [SerializeField] private List<Animator> anims;

    [Tooltip("对应的 Collider2D 列表（索引需与 anims 对应）")]
    [SerializeField] private List<Collider2D> colliders;

    [Tooltip("用于触碰检测的相机（为空则使用主相机）")]
    [SerializeField] private Camera targetCamera;

    #endregion

    #region 状态变量

    // 当前触碰的 Collider 数量
    private int pressCount = 0;

    // 当前被触碰的 Collider 索引列表
    private List<int> touchedIndices = new List<int>();

    private bool isStop = false;

    // 计时器
    private float idleTimer = 0f;
    private float splashTimer = 0f;

    // 状态标记
    private bool isShortSplashing = false;
    private bool isLongSplashing = false;

    private MelenitasDev.SoundsGood.Sound splash;

    // 相机引用
    private Camera mainCamera;

    #endregion

    #region 生命周期

    void Awake()
    {
        InitCamera();
    }

    void Update()
    {
        if (isStop)
            return;
        
        UpdateTouchInput();
        UpdateSplashState();
    }

    #endregion

    #region 初始化

    private void InitCamera()
    {
        if (targetCamera != null)
        {
            mainCamera = targetCamera;
            return;
        }

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
    }

    #endregion

    #region 触碰检测

    private void UpdateTouchInput()
    {
        Vector2 inputPosition;
        bool hasInput = TryGetInputPosition(out inputPosition);

        if (!hasInput)
        {
            pressCount = 0;
            touchedIndices.Clear();
            return;
        }

        // 检测触碰了哪些 Collider
        touchedIndices = GetTouchedIndices(inputPosition);
        pressCount = touchedIndices.Count;
    }

    private bool TryGetInputPosition(out Vector2 position)
    {
        position = Vector2.zero;

#if UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began ||
                touch.phase == TouchPhase.Stationary ||
                touch.phase == TouchPhase.Moved)
            {
                position = touch.position;
                return true;
            }
        }
        return false;
#else
        if (Input.GetMouseButton(0))
        {
            position = Input.mousePosition;
            return true;
        }
        return false;
#endif
    }

    private List<int> GetTouchedIndices(Vector2 screenPosition)
    {
        List<int> indices = new List<int>();

        if (mainCamera == null || colliders == null)
            return indices;

        Vector2 worldPos = ScreenToWorldPosition(screenPosition);

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null && colliders[i].OverlapPoint(worldPos))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    private Vector2 ScreenToWorldPosition(Vector2 screenPosition)
    {
        if (mainCamera == null)
            return Vector2.zero;

        float zDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 screenPosWithZ = new Vector3(screenPosition.x, screenPosition.y, zDistance);
        return mainCamera.ScreenToWorldPoint(screenPosWithZ);
    }

    #endregion

    #region Splash 状态管理

    private void UpdateSplashState()
    {
        // Long Splash 优先，与 Short Splash 互斥
        if (isLongSplashing || isShortSplashing)
        {
            UpdateSplash();
            return;
        }

        // 更新待机状态
        UpdateIdle();
    }

    private void UpdateIdle()
    {
        idleTimer += Time.deltaTime;

        if (idleTimer >= IdleInterval)
        {
            if (pressCount == 1)
            {
                TriggerLongSplash(touchedIndices[0]);
            }
            else if (pressCount == 0)
            {
                StartShortSplash();
            }
        }
    }

    private void UpdateSplash()
    {
        splashTimer += Time.deltaTime;

        if (splashTimer >= SplashDuring)
        {
            if (isShortSplashing)
                EndShortSplash();
            else
                EndLongSplash();
        }
    }

    private void UpdateLongSplash()
    {
        splashTimer += Time.deltaTime;

        if (splashTimer >= SplashDuring)
        {
            EndLongSplash();
        }
    }

    #endregion

    #region Splash 触发

    private void StartShortSplash()
    {
        isShortSplashing = true;
        splashTimer = 0f;

        foreach (var anim in anims)
        {
            if (anim != null)
            {
                anim.SetTrigger("short");
                anim.SetBool("stay_short", true);
                PlaySplash().Forget();
            }
        }
    }

    private void EndShortSplash()
    {
        isShortSplashing = false;
        idleTimer = 0f;

        foreach (var anim in anims)
        {
            if (anim != null)
            {
                anim.SetBool("stay_short", false);
            }
        }
    }

    private void TriggerLongSplash(int touchedIndex)
    {
        if (isLongSplashing)
            return;

        // 停止 short splash
        if (isShortSplashing)
        {
            isShortSplashing = false;
            foreach (var anim in anims)
            {
                if (anim != null)
                {
                    anim.SetBool("stay_short", false);
                }
            }
        }

        // 开始 long splash（仅未被触碰的 Animator）
        isLongSplashing = true;
        splashTimer = 0f;

        for (int i = 0; i < anims.Count; i++)
        {
            if (i != touchedIndex && anims[i] != null)
            {
                anims[i].SetTrigger("long");
                anims[i].SetBool("stay_long", true);
                PlaySplash().Forget();
            }
        }
    }

    private void EndLongSplash()
    {
        isLongSplashing = false;
        idleTimer = 0f;

        foreach (var anim in anims)
        {
            if (anim != null)
            {
                anim.SetBool("stay_long", false);
            }
        }
    }

    private async UniTaskVoid PlaySplash()
    {
        await UniTask.WaitForSeconds(0.35f);
        splash = AudioManager.PlaySound("splash");
    }

    private void OnDestroy()
    {
        if (splash != null)
            splash.Stop();
    }

    #endregion

    #region 公共接口

    /// <summary>
    /// 获取当前触碰数量
    /// </summary>
    public int GetPressCount()
    {
        return pressCount;
    }

    public void GameFinished()
    {
        isStop = true;
    }

    #endregion
}
