using System;
using System.Collections.Generic;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game.ItemEvent;
using Game.Scripts.Common;
using I2.Loc;
using TMPro;
using UnityEngine;

public class Level35Ctrl : MonoBehaviour, IDebuger
{
    [SerializeField] private TextMeshProUGUI level_txt;
    [SerializeField] private UIDrag[] numberTxt;
    [SerializeField] private ParticleSystem feather;
    [SerializeField] private float mergeRadius = 100f;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float clickTipMoveThreshold = 20f;

    [SerializeField] private DirectMove key;

    /// <summary>
    /// 主相机属性，如果未绑定则自动查找
    /// </summary>
    private Camera MainCamera
    {
        get
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                    mainCamera = FindFirstObjectByType<Camera>();
            }
            return mainCamera;
        }
    }

    private RectTransform targetRectTransform;
    private TextMeshProUGUI targetTextMeshProUGUI;
    private UIDrag targetUIDrag;
    private bool isInitialized = false;

    private int lastTxtCount = 0;
    private CustomButton _currentDragBtn;
    private Vector2 _dragStartAnchoredPosition;

    void Start()
    {
        InitLevelTxt();
        InitNumberDragEvents();
    }

    void Update()
    {
        SyncLevelTxtPosition();
    }

    /// <summary>
    /// 初始化数字拖拽事件：DragBegin 时记录 currentBtn 与起始位置，Click 时仅当该 btn 相对起始位置超出阈值才弹出提示。
    /// </summary>
    private void InitNumberDragEvents()
    {
        if (numberTxt == null || numberTxt.Length == 0)
            return;

        foreach (UIDrag drag in numberTxt)
        {
            if (drag == null)
                continue;

            drag.OnDragEnd += CheckAndMergeNumbers;

            var btn = drag.GetComponent<CustomButton>();
            if (btn == null)
                continue;

            btn.OnClickDown += OnClickBegin;
            btn.OnClick += CheckMsg;
            btn.OnClickUp += OnClickEnd;
        }
    }

    private void OnClickBegin(CustomButton btn)
    {
        var rect = btn.GetComponent<RectTransform>();
        if (rect != null)
        {
            _currentDragBtn = btn;
            _dragStartAnchoredPosition = rect.anchoredPosition;
            this.Log("[Level35 CheckMsg] click begin");
        }
    }

    private void OnClickEnd(CustomButton btn)
    {
        _currentDragBtn = null;
        this.Log("[Level35 CheckMsg] click end");
    }

    private void CheckMsg(CustomButton btn)
    {
        // Debug.Log($"[Level35 CheckMsg] 进入 btn={btn != null}, name={btn?.name}");

        if (numberTxt == null || numberTxt.Length < 2 || btn == null)
        {
            Debug.Log("[Level35 CheckMsg] return: numberTxt 无效或 btn 为空");
            return;
        }

        var rect = btn.GetComponent<RectTransform>();
        if (rect == null)
        {
            Debug.Log("[Level35 CheckMsg] return: rect 为空");
            return;
        }

        float distance = Vector2.Distance(rect.anchoredPosition, _dragStartAnchoredPosition);
        // Debug.Log($"[Level35 CheckMsg] distance={distance:F1} threshold={clickTipMoveThreshold} pos={rect.anchoredPosition} start={_dragStartAnchoredPosition}");

        if (distance > clickTipMoveThreshold)
        {
            this.Log("[Level35 CheckMsg] return: 未超出阈值，不弹提示");
            return;
        }

        // Debug.Log("[Level35 CheckMsg] 触发 SystemTips");
        var msg = LocalizationManager.GetTranslation("S_HurryTips_Des_level_35");
        SystemTips.Show(string.Format(msg, btn.GetComponent<TextMeshProUGUI>().text));
        AudioManager.PlaySound("passwordError");
    }

    /// <summary>
    /// 初始化关卡文本引用和内容
    /// </summary>
    private void InitLevelTxt()
    {
        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
        {
            return;
        }

        // 查找 GamePlayPanel 中的关卡文本（先按路径，找不到再按名称递归查找）
        Transform root = PlayCtrl.Instance.CurrentGamePlayPanel.transform;
        Transform levelBlock = root.Find("Root/levelBlock");
        if (levelBlock == null)
            levelBlock = root.Find("levelBlock");
        Transform targetTransform = levelBlock != null ? levelBlock.Find("level_txt") : null;
        if (targetTransform == null)
            targetTransform = FindChildByName(root, "level_txt");

        if (targetTransform != null)
        {
            targetRectTransform = targetTransform as RectTransform;
            targetTextMeshProUGUI = targetTransform.GetComponent<TextMeshProUGUI>();
            targetUIDrag = targetTransform.GetComponent<UIDrag>();

            if (level_txt != null && targetTextMeshProUGUI != null)
            {
                // 初始化文本内容（只需要设置一次）
                level_txt.text = targetTextMeshProUGUI.text;
                isInitialized = true;
            }

            // 绑定 targetTransform 的拖拽结束事件
            if (targetUIDrag != null)
            {
                targetUIDrag.OnDragEnd += CheckAndMergeNumbers;
            }
        }
        else
        {
            Debug.LogWarning("Target level_txt not found in GamePlayPanel at path: Root/levelBlock/level_txt");
        }
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name))
            return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;
            Transform found = FindChildByName(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// 同步关卡文本位置（在 Update 中持续执行）
    /// </summary>
    private void SyncLevelTxtPosition()
    {
        if (!isInitialized || level_txt == null)
            return;

        if (targetRectTransform != null)
        {
            if (MainCamera != null)
            {
                // 使用 CommonUtility 将 anchoredPosition 转换为世界坐标
                float targetZ = level_txt.transform.position.z;
                Vector3 worldPos = CommonUtility.ConvertUIToWorldPosition(targetRectTransform, MainCamera, targetZ);
                level_txt.transform.position = worldPos;
            }
        }
        else if (targetRectTransform == null && PlayCtrl.Instance != null && PlayCtrl.Instance.CurrentGamePlayPanel != null)
        {
            Transform root = PlayCtrl.Instance.CurrentGamePlayPanel.transform;
            Transform targetTransform = root.Find("Root/levelBlock/level_txt") ?? FindChildByName(root, "level_txt");
            if (targetTransform != null)
                targetRectTransform = targetTransform as RectTransform;
        }
    }

    /// <summary>
    /// 检查并合并数字文本
    /// </summary>
    private void CheckAndMergeNumbers()
    {
        if (numberTxt == null || numberTxt.Length < 2)
            return;

        lastTxtCount = 0;
        // 获取所有有效的文本组件
        List<(UIDrag drag, TextMeshProUGUI text, RectTransform rect)> validTexts =
            new List<(UIDrag, TextMeshProUGUI, RectTransform)>();

        foreach (UIDrag drag in numberTxt)
        {
            if (drag == null || !drag.gameObject.activeSelf)
                continue;

            TextMeshProUGUI text = drag.GetComponent<TextMeshProUGUI>();
            RectTransform rect = drag.GetComponent<RectTransform>();

            if (text != null && rect != null)
            {
                lastTxtCount++;
                validTexts.Add((drag, text, rect));
            }
        }

        // 检查所有文本对之间的距离
        for (int i = 0; i < validTexts.Count; i++)
        {
            for (int j = i + 1; j < validTexts.Count; j++)
            {
                var text1 = validTexts[i];
                var text2 = validTexts[j];

                // 计算两个文本之间的距离（使用世界坐标）
                float distance = Vector3.Distance(text1.rect.position, text2.rect.position);

                if (distance < mergeRadius)
                {
                    // 尝试合并
                    if (TryMergeNumbers(text1, text2))
                    {
                        // 合并成功后，重新检查（因为列表已改变）
                        AfterMergeCheck().Forget();
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 尝试合并两个数字文本
    /// </summary>
    private bool TryMergeNumbers(
        (UIDrag drag, TextMeshProUGUI text, RectTransform rect) text1,
        (UIDrag drag, TextMeshProUGUI text, RectTransform rect) text2)
    {
        string content1 = text1.text.text.Trim();
        string content2 = text2.text.text.Trim();

        // 尝试解析为整数
        if (!int.TryParse(content1, out int num1))
        {
            Debug.LogError($"[Level35Ctrl] Text content '{content1}' is not a valid integer!");
            return false;
        }

        if (!int.TryParse(content2, out int num2))
        {
            Debug.LogError($"[Level35Ctrl] Text content '{content2}' is not a valid integer!");
            return false;
        }

        // 计算合并后的位置（两者中间）
        Vector3 mergePosition = (text1.rect.position + text2.rect.position) / 2f;

        // 计算合并后的值
        int mergedValue = num1 + num2;

        var t = text1;
        if (text1.drag.gameObject.name.Equals("level"))
        {
            t = text2;
            text2 = text1;
            text1 = t;

            targetRectTransform.gameObject.SetActive(false);
        }

        if (text2.drag.gameObject.name.Equals("level"))
            targetRectTransform.gameObject.SetActive(false);

        // 隐藏第二个文本
        text2.drag.gameObject.SetActive(false);

        // 移动第一个文本到中间位置
        text1.rect.position = mergePosition;

        // 更新第一个文本的内容
        text1.text.text = mergedValue.ToString();

        Debug.Log($"[Level35Ctrl] Merged {num1} + {num2} = {mergedValue}");

        if (feather != null)
        {
            feather.transform.position = text1.text.transform.position;
            feather.Play();
            AudioManager.PlaySound("mergeNumber");
        }

        return true;
    }

    private async UniTaskVoid AfterMergeCheck()
    {
        Debug.Log("----------- " + lastTxtCount);
        if (lastTxtCount <= 2)
        {
            await UniTask.WaitForSeconds(0.5f);

            foreach (UIDrag drag in numberTxt)
            {
                if (drag == null || !drag.gameObject.activeSelf)
                    continue;

                drag.gameObject.SetActive(false);
                feather.transform.position = drag.transform.position;
                feather.Play();
                key.transform.position = drag.transform.position;
                key.gameObject.SetActive(true);

                break;
            }
        }
    }

    public void StopKey()
    {
        key.enabled = false;
        key.GetComponent<MoveFloatHandle>().enabled = true;
    }

    private void OnDestroy()
    {
        if (numberTxt != null)
        {
            foreach (UIDrag drag in numberTxt)
            {
                if (drag == null)
                    continue;
                drag.OnDragEnd -= CheckAndMergeNumbers;
                var btn = drag.GetComponent<CustomButton>();
                if (btn != null)
                    btn.OnClick -= CheckMsg;
            }
        }

        if (targetUIDrag != null)
            targetUIDrag.OnDragEnd -= CheckAndMergeNumbers;
    }
}
