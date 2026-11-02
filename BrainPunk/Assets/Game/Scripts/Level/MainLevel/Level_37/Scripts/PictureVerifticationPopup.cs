using System;
using System.Collections.Generic;
using Bear.UI;
using DG.Tweening;
using Game.Scripts.Common;
using I2.Loc;
using TMPro;
using UnityEngine;

public partial class PictureVerifticationPopup : BaseUIView
{
    public Sprite[] pics;

    private const int MaxPicCount = 16;

    [SerializeField] private VerificationItem item;
    [SerializeField] private RectTransform root;

    [SerializeField] private VerificationItem hideItem;
    // [Tooltip("隐藏格贴图，与 currentIndex 对应：下标 0→主题1、1→主题2、2→主题3；不填则按名 SP_yzm_img_{1~3}_hide 从 pics 查找")]
    // [SerializeField] private Sprite[] hideSprites;
    [Tooltip("I2 术语 key，下标 0~2 对应主题 1~3，随 ApplySpritesInOrder 切换")]
    [SerializeField] private string[] hideContentKeys;
    [SerializeField] private TextMeshProUGUI hideContent;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeDuration = 0.2f;

    [SerializeField] private string pic1SuccessIndex;
    [SerializeField] private string pic2SuccessIndex;
    [SerializeField] private string pic3SuccessIndex;


    // 关闭的时候调用
    public event Action<bool> OnVerificationEnd;
    /// <summary> 验证通过时触发（在关闭界面前调用），关卡逻辑可订阅 </summary>
    public event Action OnVerificationSuccess;

    private readonly List<VerificationItem> _items = new List<VerificationItem>(MaxPicCount);
    private Dictionary<string, Sprite> _spriteByName;
    private int currentIndex;

    public override void OnCreate()
    {
        base.OnCreate();
        BuildSpriteLookup();
        InitItems();
        if (group != null)
        {
            group.alpha = 1f;
            SetGroupInteractable(true);
        }
        currentIndex = UnityEngine.Random.Range(1, 4);
        ApplySpritesInOrder(useFade: false);

        if (CheckBtn != null) CheckBtn.OnClick += OnCheckClick;
        if (ResetBtn != null) ResetBtn.OnClick += OnResetClick;
        if (CloseBtn != null) CloseBtn.OnClick += OnCloseClick;
    }

    public override void OnClose()
    {
        if (group != null)
            group.DOKill();
        base.OnClose();
        OnVerificationEnd?.Invoke(false);
        // if (CheckBtn != null) CheckBtn.OnClick -= OnCheckClick;
        // if (ResetBtn != null) ResetBtn.OnClick -= OnResetClick;
        // if (CloseBtn != null) CloseBtn.OnClick -= OnCloseClick;
    }

    private void BuildSpriteLookup()
    {
        _spriteByName = new Dictionary<string, Sprite>();
        if (pics == null)
            return;
        for (int i = 0; i < pics.Length; i++)
        {
            var s = pics[i];
            if (s == null || string.IsNullOrEmpty(s.name))
                continue;
            _spriteByName[s.name] = s;
        }
    }

    private void InitItems()
    {
        _items.Clear();
        if (item == null || root == null)
        {
            Debug.LogError("[PictureVerifticationPopup] item 或 root 未配置");
            return;
        }

        for (int i = 0; i < MaxPicCount; i++)
        {
            var inst = Instantiate(item, root);
            inst.gameObject.SetActive(true);
            var vi = inst.GetComponent<VerificationItem>();
            if (vi == null)
            {
                Debug.LogError("[PictureVerifticationPopup] 预制体上缺少 VerificationItem");
                continue;
            }
            _items.Add(vi);
        }
        item.gameObject.SetActive(false);
    }

    private void OnCloseClick(CustomButton btn)
    {
        UIManager.Instance.DestroyUI(this);
    }

    private void OnResetClick(CustomButton btn)
    {
        for (int i = 0; i < _items.Count; i++)
            _items[i].SetSelectionState(false);
        if (hideItem != null)
            hideItem.SetSelectionState(false);
    }

    private void OnCheckClick(CustomButton btn)
    {
        if (_items.Count != MaxPicCount)
            return;

        string content = GetSuccessString(currentIndex);
        if (String.IsNullOrEmpty(content))
        {
            ApplySpritesInOrder();
            return;
        }

        var expected = ParseSuccessList(content);
        var actual = new List<int>();
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].isSelected)
                actual.Add(_items[i].AssignedCount);
        }
        if (hideItem != null && hideItem.isSelected)
            actual.Add(hideItem.AssignedCount);

        expected.Sort();
        actual.Sort();

        if (ListsEqual(expected, actual))
        {
            OnVerificationSuccess?.Invoke();
            UIManager.Instance.DestroyUI(this);

            AudioManager.PlaySound("verifticationCorrect");
            return;
        }

        ApplySpritesInOrder();
        AudioManager.PlaySound("verifticationError");
    }

    /// <summary> 按顺序填充：第 i 格对应图片序号 i+1（1～16），无随机；hideItem 刷新为 -1。useFade 时用 CanvasGroup 渐隐再换图再渐显。 </summary>
    private void ApplySpritesInOrder(bool useFade = true)
    {
        if (!useFade || group == null || fadeDuration <= 0f)
        {
            ApplySpritesInOrderCore();
            return;
        }

        group.DOKill();
        SetGroupInteractable(false);
        group.DOFade(0f, fadeDuration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                ApplySpritesInOrderCore();
                group.DOFade(1f, fadeDuration)
                    .SetUpdate(true)
                    .OnComplete(() => SetGroupInteractable(true));
            });
    }

    private void SetGroupInteractable(bool value)
    {
        if (group == null)
            return;
        group.interactable = value;
        group.blocksRaycasts = value;
    }

    private void ApplySpritesInOrderCore()
    {
        currentIndex = (++currentIndex % 3) + 1;
        for (int i = 0; i < _items.Count; i++)
        {
            int c = i + 1;
            var sp = GetSprite(currentIndex, c);
            _items[i].SetContent(c, sp);
        }
        RefreshHideItem();
        RefreshHideContent();
    }

    private void RefreshHideContent()
    {
        if (hideContent == null)
            return;
        if (hideContentKeys == null || hideContentKeys.Length == 0)
            return;

        int idx = currentIndex - 1;
        if (idx < 0 || idx >= hideContentKeys.Length)
            return;
        string termKey = hideContentKeys[idx];
        if (string.IsNullOrEmpty(termKey))
            return;
        string text = LocalizationManager.GetTranslation(termKey);
        hideContent.text = text;
    }

    private void RefreshHideItem()
    {
        if (hideItem == null)
            return;
        hideItem.SetContent(-1, null);
    }

    private Sprite GetSprite(int groupIndex, int count)
    {
        string key = $"SP_yzm_img_{groupIndex}_{count:00}";
        if (_spriteByName != null && _spriteByName.TryGetValue(key, out var sp))
            return sp;
        Debug.LogWarning($"[PictureVerifticationPopup] 缺少 Sprite: {key}");
        return null;
    }

    private string GetSuccessString(int index)
    {
        return index switch
        {
            1 => pic1SuccessIndex,
            2 => pic2SuccessIndex,
            3 => pic3SuccessIndex,
            _ => "",
        };
    }

    private static List<int> ParseSuccessList(string s)
    {
        var list = new List<int>();
        if (string.IsNullOrWhiteSpace(s))
            return list;
        var parts = s.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            var t = parts[i].Trim();
            if (int.TryParse(t, out int v))
                list.Add(v);
        }
        return list;
    }

    private static bool ListsEqual(List<int> a, List<int> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }

    /// <summary> 通过 UIManager 打开人机验证弹窗（Resources 路径与类型名一致）。 </summary>
    public static PictureVerifticationPopup Create()
    {
        var panel = UIManager.Instance.OpenUI<PictureVerifticationPopup>(
            $"{typeof(PictureVerifticationPopup).Name}", UILayer.Popup);
        return panel;
    }
}
