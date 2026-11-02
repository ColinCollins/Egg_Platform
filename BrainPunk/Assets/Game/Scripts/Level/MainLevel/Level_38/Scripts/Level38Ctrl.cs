using System.Collections;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using I2.Loc;
using UnityEngine;

/// <summary>
/// 滑动校验关卡，items 是校验对象，lights 展示当前进度。最大校验次数 3
/// 每次校验完成，slider 会到初始位置，OnDragEnd 时，检测当前 SliderItem 的位置与items 的center 位置差距是否在要求 dx 内。
/// SliderItem 展示出来，成功后隐藏对应进度的 SliderItem
/// 在，亮一个绿灯，进入下个阶段
/// 不在，播放红灯闪烁动画，并重置进度
/// </summary>
public class Level38Ctrl : MonoBehaviour, IDebuger
{
    [SerializeField] private Level38SliderItem[] items;
    [SerializeField] private Level38LightItem[] lights;

    [SerializeField] private Transform slider;
    private Vector3 sliderStartPos; // slider 初始位置

    [SerializeField] private GameObject key;
    [SerializeField] private Camera mainCamera;

    [Header("校验配置")]
    [Tooltip("关卡阶段数（items 长度）")]
    [SerializeField] private int maxStageCount = 3;

    [Header("特效")]
    [Tooltip("Feather 粒子特效")]
    [SerializeField] private ParticleSystem featherEffect;

    [SerializeField] private Transform MaskBlock;

    [SerializeField] private float WaitingTime = 3f;

    // 当前校验阶段
    private int index = 0;

    // 拖拽相关
    private Vector3 dragStartPos;
    private bool isDragging = false;

    // 防护状态
    private bool _isProcessing = false;      // 是否正在处理验证中（防止重复触发）
    private bool _isFailedAnimPlaying = false; // 是否正在播放失败动画
    private bool _isLevelCompleted = false;  // 关卡是否已完成

    void Start()
    {
        // 基础数据校验
        if (!ValidateSetup())
        {
            this.LogError("关卡初始化失败，配置有误！");
            enabled = false;
            return;
        }

        // 记录初始位置
        if (sliderStartPos == Vector3.zero && slider != null)
        {
            sliderStartPos = slider.position;
        }

        InitAllItems();
        ResetProgress();
    }

    /// <summary>
    /// 初始化所有 items：全部展示出来
    /// </summary>
    void InitAllItems()
    {
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
            {
                // 全部激活
                items[i].gameObject.SetActive(true);
                items[i].SetContentActive(false);
            }
        }
    }

    /// <summary>
    /// 验证基础配置是否正确
    /// </summary>
    bool ValidateSetup()
    {
        if (items == null || items.Length == 0)
        {
            this.LogError("items 数组为空！");
            return false;
        }
        if (lights == null || lights.Length == 0)
        {
            this.LogError("lights 数组为空！");
            return false;
        }
        if (lights.Length < items.Length)
        {
            this.LogError($"lights 数量 ({lights.Length}) 小于 items 数量 ({items.Length})！");
            return false;
        }
        if (slider == null)
        {
            this.LogError("slider 未赋值！");
            return false;
        }
        if (key == null)
        {
            this.LogError("key 未赋值！");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 重置进度
    /// </summary>
    void ResetProgress()
    {
        index = 0;
        _isLevelCompleted = false;

        ResetLights();
        ResetSlider();
        // 显示所有未完成的 items
        ShowAllUncompletedItems();
        ResetKeyState();
        ResetWaitingMask();
    }

    /// <summary>
    /// 显示所有未完成的 items, 隐藏已完成的 item
    /// </summary>
    void ShowAllUncompletedItems()
    {
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
            {
                // 当前阶段及之后的 items 显示
                bool shouldShow = i >= index;
                items[i].gameObject.SetActive(shouldShow);
                items[i].SetContentActive(i == index);
            }
        }

        ResetCurrentItemPosition();
    }

    /// <summary>
    /// 重置当前 item 位置
    /// </summary>
    void ResetCurrentItemPosition()
    {
        // 防护：检查 index 边界
        if (index < 0 || index >= items.Length)
        {
            this.LogWarning($"ResetCurrentItemPosition: index {index} 超出范围 [0, {items.Length})");
            return;
        }

        var currentItem = items[index];
        if (currentItem == null || currentItem.Content == null)
        {
            this.LogWarning($"items[{index}] 或其 Content 为 null");
            return;
        }

        // currentItem.SetContentPositionX(sliderStartPos.x);
    }

    /// <summary>
    /// 更新当前 item content 的 x 位置跟随 slider
    /// </summary>
    void UpdateCurrentItemContentPosition()
    {
        if (index < 0 || index >= items.Length || items[index] == null || slider == null)
            return;

        items[index].SetContentPositionX(slider.position.x);
    }

    /// <summary>
    /// 重置所有灯
    /// </summary>
    void ResetLights()
    {
        if (lights == null) return;
        foreach (var light in lights)
        {
            if (light != null)
                light.TurnOffAll();
        }
    }

    /// <summary>
    /// 重置 slider 到初始位置
    /// </summary>
    void ResetSlider()
    {
        if (slider != null)
        {
            slider.DOMove(sliderStartPos, 0.3f);
        }
    }

    void ResetKeyState()
    {
        // 防护：key 和 Collider2D 检查
        if (key != null)
        {
            var collider = key.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = false;
            }
            else
            {
                this.LogWarning("key 没有 Collider2D 组件！");
            }
        }
    }

    void ResetWaitingMask()
    {
        MaskBlock.gameObject.SetActive(false);
    }

    /// <summary>
    /// 点亮当前阶段的灯，进入下一阶段
    /// </summary>
    void OnVerifySuccess()
    {
        // 防护：防止重复调用
        if (_isProcessing) return;
        _isProcessing = true;

        // 防护：关卡已完成则不处理
        if (_isLevelCompleted)
        {
            _isProcessing = false;
            return;
        }

        if (lights != null && index >= 0 && index < lights.Length)
        {
            lights[index].ShowGreenLight();
        }

        // 播放 feather 特效并隐藏对应进度的 item
        StartCoroutine(PlaySuccessEffectAndHideItem(index));

        index++;

        // 检查是否全部完成
        if (index >= items.Length)
        {
            OnLevelComplete();
        }
        else
        {
            // 进入下一阶段，重置 slider 和当前 item 位置
            ResetSlider();
            ResetCurrentItemPosition();
            ShowAllUncompletedItems();
        }

        _isProcessing = false;
    }

    /// <summary>
    /// 播放成功特效并隐藏对应进度的 item
    /// </summary>
    System.Collections.IEnumerator PlaySuccessEffectAndHideItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= items.Length) yield break;

        Level38SliderItem targetItem = items[itemIndex];
        if (targetItem == null) yield break;

        // 移动 feather 到 item 位置并播放
        if (featherEffect != null)
        {
            featherEffect.transform.position = targetItem.GetCenterPosition();
            featherEffect.Play();
        }

        // 隐藏对应进度的 item
        targetItem.gameObject.SetActive(false);
    }

    /// <summary>
    /// 关卡完成
    /// </summary>
    void OnLevelComplete()
    {
        if (_isLevelCompleted) return;
        _isLevelCompleted = true;

        this.Log("Level 38 Complete!");

        // 防护：key 和 Collider2D 检查
        if (key != null)
        {
            var collider = key.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = true;
            }
            else
            {
                this.LogWarning("key 没有 Collider2D 组件！");
            }
        }

        // 防护：MaskBlock 检查
        if (MaskBlock != null)
        {
            MaskBlock.gameObject.SetActive(true);
            MaskBlock.localScale = Vector3.zero;
            MaskBlock.DOScale(Vector3.one, 0.3f);
        }
        else
        {
            this.LogWarning("MaskBlock 未赋值！");
        }


        slider.GetComponent<DragableItem>().enabled = false;

        WaitingForReset().Forget();
    }

    private async UniTask WaitingForReset()
    {
        await UniTask.WaitForSeconds(WaitingTime);
        OnVerifyFail();

        if (!stopFailed)
            SystemTips.Show(LocalizationManager.GetTranslation("S_LevelTips_Des_level_38_02"));
    }

    /// <summary>
    /// 校验失败，播放红灯闪烁然后重置全部进度
    /// </summary>
    void OnVerifyFail()
    {
        // 防护：防止重复调用
        if (_isProcessing || _isFailedAnimPlaying || stopFailed) return;
        _isProcessing = true;

        // 防护：关卡已完成则不处理
        /*  if (_isLevelCompleted)
         {
             _isProcessing = false;
             return;
         } */

        slider.GetComponent<DragableItem>().enabled = true;
        this.Log("校验失败，重置进度");

        StartCoroutine(OnVerifyFailCoroutine());
    }

    /// <summary>
    /// 校验失败协程：播放红灯闪烁，然后重置当前阶段进度
    /// </summary>
    IEnumerator OnVerifyFailCoroutine()
    {
        _isFailedAnimPlaying = true;

        // 播放当前阶段对应的红灯闪烁
        if (lights != null)
        {
            for (int i = 0; i < lights.Length && i <= index; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].PlayRedBlink();
                }
            }
        }

        // 等待红灯闪烁完成 (0.5s)
        yield return new WaitForSeconds(0.5f);

        this.Log("红灯闪烁完成，重置当前阶段");

        // 失败后：重置所有灯光，重置 slider 位置，重置当前 item 位置
        /* ResetLights();
        ResetSlider();

        // 显示所有未完成的 items
        ShowAllUncompletedItems();

        ResetCurrentItemPosition(); */
        ResetProgress();

        _isFailedAnimPlaying = false;
        _isProcessing = false;
    }



    /// <summary>
    /// 检测 slider 位置是否在允许范围内
    /// </summary>
    bool CheckSliderPosition()
    {
        if (index < 0 || index >= items.Length || items[index] == null || slider == null)
            return false;

        Level38SliderItem currentItem = items[index];
        Vector3 itemCenter = currentItem.GetCenterPosition();
        float dx = currentItem.GetAllowDx();

        // 计算 slider 与 item center 的水平距离
        float distance = Mathf.Abs(slider.position.x - itemCenter.x);
        // this.Log("slider.position.x: " + slider.position.x);
        // this.Log("distance: " + distance);

        return distance <= dx;
    }

    #region 拖拽接口实现

    public void OnBeginDrag(Transform target)
    {
        // 防护：各种状态检查
        if (_isLevelCompleted || _isFailedAnimPlaying || _isProcessing) return;
        if (index < 0 || index >= items.Length) return;
        if (items[index] == null) return;

        isDragging = true;
        dragStartPos = slider.position;
    }

    public void Update()
    {
        if (slider == null || index < 0 || index >= items.Length) return;

        // 只允许水平拖动
        // 更新当前 item content 的位置跟随 slider
        UpdateCurrentItemContentPosition();
    }

    public void OnEndDrag(Transform target)
    {
        if (!isDragging) return;
        isDragging = false;

        // 防护：各种状态检查
        if (_isLevelCompleted || _isFailedAnimPlaying || _isProcessing) return;

        // 检测位置是否在要求范围内
        if (CheckSliderPosition())
        {
            OnVerifySuccess();
        }
        else
        {
            OnVerifyFail();
        }
    }

    #endregion

    private bool stopFailed = false;
    public void StopReset()
    {
        stopFailed = true;
    }

    public void PlayKeyJump()
    {

    }

    #region 调试信息

    void OnGUI()
    {
        // 仅在编辑器或非发布版本显示调试信息
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUILayout.BeginArea(new Rect(10, 10, 200, 100));
        GUILayout.Label($"Stage: {index + 1}/{items.Length}");
        GUILayout.Label($"Processing: {_isProcessing}");
        GUILayout.Label($"Completed: {_isLevelCompleted}");
        GUILayout.EndArea();
#endif
    }

    #endregion
}
