using System;
using System.Collections.Generic;
using System.Threading;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 在界面中随机限定范围 GenerateRandomLimit 内生成 GenerateRandomCount 个数个 WarningPart.
/// </summary>
public partial class Level43ErrorPopup : BaseUIView
{
    private Action callback;

    public Vector2Int GenerateRandomCount = new Vector2Int(5, 10);

    public Vector2Int GenerateRandomLimit = new Vector2Int(1000, 1000);
    public GameObject WarningPart;

    [Header("生成间隔")]
    [Tooltip("每个 WarningPart 生成之间的时间间隔（秒）")]
    [SerializeField] private float spawnInterval = 0.3f;

    private int randomCount;

    private readonly List<GameObject> _spawnedParts = new List<GameObject>();
    private readonly List<CustomButton> _spawnedButtons = new List<CustomButton>();
    private CancellationTokenSource _spawnCts;

    private bool isGenerate = true;

    public override void OnOpen()
    {
        base.OnOpen();
        _spawnCts = new CancellationTokenSource();
        GenerateWarningPartsAsync().Forget();
    }

    public override void OnCreate()
    {
        base.OnCreate();
        // TODO: 注册按钮等（示例见 PictureVerifticationPopup）
        if (UiCommonBlack01Btn != null)
        {
            UiCommonBlack01Btn.OnClick += TryToClosePopup;
        }
        else
        {
            Debug.LogWarning("[Level43ErrorPopup] UiCommonBlack01Btn is null.");
        }
    }

    private void TryToClosePopup(CustomButton btn)
    {
        if (btn == null || isGenerate)
        {
            return;
        }

        randomCount--;
        if (btn.transform != null && btn.transform.parent != null)
        {
            btn.transform.parent.gameObject.SetActive(false);
        }
        if (randomCount <= 0)
        {
            callback?.Invoke();
            gameObject.SetActive(false);
            CancelSpawnTask();
            // UIManager.Instance.DestroyUI(this);
        }
    }

    public override void OnClose()
    {
        CancelSpawnTask();
        base.OnClose();
    }

    public override void OnDestroyView()
    {
        CancelSpawnTask();

        // 清理动态生成按钮的事件绑定
        foreach (var btn in _spawnedButtons)
        {
            if (btn != null)
            {
                btn.OnClick -= TryToClosePopup;
            }
        }
        _spawnedButtons.Clear();
        _spawnedParts.Clear();
        base.OnDestroyView();
    }

    /// <summary>
    /// 取消生成任务
    /// </summary>
    private void CancelSpawnTask()
    {
        if (_spawnCts != null)
        {
            _spawnCts.Cancel();
            _spawnCts.Dispose();
            _spawnCts = null;
        }
    }

    /// <summary>
    /// 在限定范围内随机生成 WarningPart（带时间间隔）
    /// </summary>
    private async UniTaskVoid GenerateWarningPartsAsync()
    {
        if (WarningPart == null)
        {
            Debug.LogWarning("[Level43ErrorPopup] WarningPart prefab is null.");
            return;
        }

        if (GenerateRandomCount.x > GenerateRandomCount.y)
        {
            Debug.LogWarning($"[Level43ErrorPopup] GenerateRandomCount.x ({GenerateRandomCount.x}) > y ({GenerateRandomCount.y}), swapping values.");
            int temp = GenerateRandomCount.x;
            GenerateRandomCount.x = GenerateRandomCount.y;
            GenerateRandomCount.y = temp;
        }

        if (spawnInterval < 0f)
        {
            Debug.LogWarning($"[Level43ErrorPopup] spawnInterval ({spawnInterval}) is negative, clamping to 0.");
            spawnInterval = 0f;
        }

        // 清理上一次的生成记录
        foreach (var part in _spawnedParts)
        {
            if (part != null)
            {
                Destroy(part);
            }
        }
        _spawnedParts.Clear();
        _spawnedButtons.Clear();

        randomCount = UnityEngine.Random.Range(GenerateRandomCount.x, GenerateRandomCount.y + 1);

        for (int i = 0; i < randomCount; i++)
        {
            if (_spawnCts == null || _spawnCts.IsCancellationRequested)
            {
                return;
            }

            var instance = Instantiate(WarningPart, transform);
            instance.name = $"{WarningPart.name}_{i}";
            instance.gameObject.SetActive(true);
            instance.transform.localScale = Vector3.zero;
            instance.transform.DOScale(Vector3.one,0.2f);
            _spawnedParts.Add(instance);

            // 设置随机位置
            var rectTransform = instance.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                float x = UnityEngine.Random.Range(-GenerateRandomLimit.x / 2f, GenerateRandomLimit.x / 2f);
                float y = UnityEngine.Random.Range(-GenerateRandomLimit.y / 2f, GenerateRandomLimit.y / 2f);
                rectTransform.anchoredPosition = new Vector2(x, y);
            }
            else
            {
                float x = UnityEngine.Random.Range(-GenerateRandomLimit.x / 2f, GenerateRandomLimit.x / 2f);
                float y = UnityEngine.Random.Range(-GenerateRandomLimit.y / 2f, GenerateRandomLimit.y / 2f);
                instance.transform.localPosition = new Vector3(x, y, 0f);
            }

            // 绑定按钮事件
            var btn = instance.GetComponentInChildren<CustomButton>();
            if (btn != null)
            {
                btn.OnClick += TryToClosePopup;
                _spawnedButtons.Add(btn);
            }

            AudioManager.PlaySound("systemError");

            if (i < randomCount - 1 && spawnInterval > 0f)
            {
                try
                {
                    await UniTask.Delay((int)(spawnInterval * 1000), cancellationToken: _spawnCts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        isGenerate = false;
    }

    public void SetCallback(Action callback)
    {
        this.callback = callback;
    }

    public static Level43ErrorPopup Create(Action callback = null)
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("[Level43ErrorPopup] UIManager.Instance is null. Cannot create popup.");
            return null;
        }

        var panel = UIManager.Instance.OpenUI<Level43ErrorPopup>($"{typeof(Level43ErrorPopup).Name}", UILayer.Popup);
        if (panel == null)
        {
            Debug.LogError("[Level43ErrorPopup] Failed to open Level43ErrorPopup from UIManager.");
            return null;
        }

        panel.callback = callback;
        return panel;
    }
}
