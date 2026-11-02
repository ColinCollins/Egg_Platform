using System;
using Bear.EventSystem;
using Bear.Logger;
using Bear.SaveModule;
using Bear.UI;
using Game.ConfigModule;
using UnityEngine;
using UnityEngine.UI;

public partial class LocalizationPopup : BaseUIView, IEventSender, IDebuger
{
    public static event Action<LocalizationPopup> OnPopupCreated;

    [SerializeField] private LanguageItem item;
    [SerializeField] private RectTransform content;

    private Action<string> onSelectLanguage;
    private LanguageItem customItemView;

    public override void OnCreate()
    {
        base.OnCreate();

        CloseBtn.OnClick += OnClickClose;
        InitLanguageItems();
    }

    private void InitLanguageItems()
    {
        if (item == null || content == null)
        {
            Debug.LogWarning("[LocalizationPopup] Language item prefab or content is not assigned.");
            return;
        }

        // 清空旧项
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        var languages = ConfigManager.Instance.Tables.TbLanguage.DataList;
        if (languages == null || languages.Count == 0)
        {
            Debug.LogWarning("[LocalizationPopup] No languages found from LocalizationManager.");
            return;
        }

        string currentLanguage = DB.GameSetting.CurrentLanguageKeyCode;

        this.Log($"------language: {currentLanguage}");

        foreach (var lang in languages)
        {
            if (!lang.IsOpen)
                continue;
            var go = Instantiate(item, content);
            bool isSelected = string.Equals(lang.CorrespondingKey, currentLanguage, StringComparison.OrdinalIgnoreCase);
            go.Init(lang, isSelected, OnClickLanguageItem);
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();

        // 打开时根据当前语言刷新一次
        InitLanguageItems();
        customItemView = null;
    }

    public static LocalizationPopup Create()
    {
        return Create(null);
    }

    public static LocalizationPopup Create(Action<string> onSelectLanguage)
    {
        var panel = UIManager.Instance.OpenUI<LocalizationPopup>($"{typeof(LocalizationPopup).Name}", UILayer.Popup);
        panel.onSelectLanguage = onSelectLanguage;
        try
        {
            OnPopupCreated?.Invoke(panel);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LocalizationPopup] Popup-created extension failed: {exception.Message}");
        }

        return panel;
    }

    // 示例：关闭事件（生成字段后再解注释并绑定）
    private void OnClickClose(CustomButton btn)
    {
        ClearSessionCustomItem();
        // UIManager.Instance.CloseUI(this);
        UIManager.Instance.DestroyUI(this);
    }

    /// <summary>
    /// 添加会话级自定义语言项；isFirst 为 true 时放到列表首位，并将语言列表滚动到顶部。
    /// 该接口只扩展当前弹窗实例，不会修改系统语言配置或存档。
    /// </summary>
    public LanguageItem AddItem(LanguageItem itemPrefab, Action<LanguageItem> onClick, bool isFirst = true)
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning("[LocalizationPopup] Cannot add custom language item because prefab is missing.");
            return null;
        }

        if (content == null)
        {
            Debug.LogWarning("[LocalizationPopup] Cannot add custom language item because content is missing.");
            return null;
        }

        ClearSessionCustomItem();

        customItemView = Instantiate(itemPrefab, content);
        if (isFirst)
        {
            customItemView.transform.SetAsFirstSibling();
        }

        customItemView.InitCustom(false, onClick);

        if (isFirst)
        {
            RefreshScrollViewToTop();
        }

        return customItemView;
    }

    private void RefreshScrollViewToTop()
    {
        var scrollRect = content.GetComponentInParent<ScrollRect>();
        if (scrollRect == null)
        {
            Debug.LogWarning("[LocalizationPopup] Cannot refresh to top because ScrollRect is missing.");
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    public void CancelAllItem()
    {
        if (content == null)
            return;

        for (int i = 0; i < content.childCount; i++)
        {
            var childItem = content.GetChild(i).GetComponent<LanguageItem>();
            if (childItem != null)
            {
                childItem.SetSelected(false);
            }
        }
    }

    public void RemoveCustomItem()
    {
        ClearSessionCustomItem();
    }

    private void OnClickLanguageItem(LanguageItem itemView)
    {
        if (itemView == null)
            return;

        string key = itemView.GetLanguageKeyCode();
        if (string.IsNullOrEmpty(key))
            return;

        if (!LocalizatioinUtils.TrySetLanguage(key))
        {
            Debug.LogWarning($"[LocalizationPopup] Cannot apply language '{key}'.");
            return;
        }

        onSelectLanguage?.Invoke(key);

        Debug.Log("----------- Current LanguageCode: " + key);

        // 更新选中状态
        for (int i = 0; i < content.childCount; i++)
        {
            var childItem = content.GetChild(i).GetComponent<LanguageItem>();
            if (childItem != null)
            {
                childItem.SetSelected(childItem == itemView);
            }
        }
    }

    private void ClearSessionCustomItem()
    {
        if (customItemView == null)
            return;

        Destroy(customItemView.gameObject);
        customItemView = null;
    }

    void OnDestroy()
    {
        customItemView = null;
    }
}
