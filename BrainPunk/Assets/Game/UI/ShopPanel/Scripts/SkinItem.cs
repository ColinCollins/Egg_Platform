using System;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Config;
using Config.Game;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using I2.Loc;

/// <summary>
/// Binds one reusable skin slot to one skin config row.4
/// version2.0.0
/// 选中状态下，BlockBg 缩放到 scale 1.1 倍。
///
/// version 3.0
/// 新增 SkinItemState, ShopPanel_SkinPart
/// </summary>
public class SkinItem : MonoBehaviour, IDebuger
{

    /// <summary>
    /// SkinItem 复用槽位的显示和点击状态；只描述当前 UI 绑定状态，不直接读写存档。
    /// </summary>
    public enum SkinItemState
    {
        None = 0,
        // 普通皮肤槽位状态。
        Normal,
        // 当前存档已选中的皮肤状态。
        Selected,
        // 当前仅在 ShopPanel 中临时试穿的皮肤状态，不写入存档。
        Preview,
        // Restore 特殊按钮状态，调用 SkinPart 的当前部件清理逻辑。
        Restore
    }

    private const float SelectedScale = 1.11f;
    [Header("主视觉")]
    [Tooltip("用于展示皮肤品质等级的背景图。")]
    [SerializeField] private Image BlockBg;

    [Tooltip("品质图数组，使用 skin.Quality 作为索引；越界时会自动夹取到有效范围。")]
    [SerializeField] private Sprite[] skinLevelsSprites;

    [Tooltip("皮肤图标。当前组件只负责清理旧 sprite 状态，真实资源绑定由外部系统补充。")]
    [SerializeField] private Image icon;

    [Tooltip("皮肤显示名称文本。SkinName 为空时会回退显示皮肤 id。")]
    [SerializeField] private TextMeshProUGUI title;

    [Header("状态部件")]
    [Tooltip("当前槽位绑定到已选中皮肤时显示。")]
    [SerializeField] private Image selected;

    [Tooltip("金币解锁皮肤时显示；其子节点中的第一个 TMP 文本会写入 CoinPrice。")]
    [SerializeField] private GameObject coinPart;

    [Tooltip("广告解锁皮肤时显示；其子节点中的第一个 TMP 文本会写入 UnlockHint 或 AD。")]
    [SerializeField] private GameObject adPart;

    [Tooltip("默认、特殊、购买或不可用皮肤时显示；其子节点中的第一个 TMP 文本会写入解析后的状态文本。")]
    [SerializeField] private GameObject defaultPart;

    [Tooltip("当前复用槽位的点击目标。点击只会路由到当前绑定的 Skin 数据。")]
    [SerializeField] private CustomButton btn;

    [SerializeField] private GameObject redDot;

    private Skin data;
    private bool isSelected;
    private SkinItemState _state = SkinItemState.None;
    private Action<SkinItem> _clicked;
    private bool _isBound;
    private Vector3 _blockBgDefaultScale = Vector3.one;
    private Sprite _blockBgDefaultSprite;
    private bool _hasBlockBgDefaultScale;
    private bool _hasBlockBgDefaultSprite;
    private int _iconLoadVersion;
    private bool _hasBoundSkin;
    private Sprite _initialIconSprite;
    private bool _initialIconEnabled;
    private bool _hasInitialIconVisuals;
    private bool _preserveRestoreIconVisuals;

    public Skin Data => data;
    public bool IsSelected => isSelected;

    /// <summary>
    /// 当前槽位的显示/交互状态；外部只读，用于区分普通皮肤、试穿和 Restore 特殊行。
    /// </summary>
    public SkinItemState State => _state;

    private void Awake()
    {
        ResolveReferences();
        BindButton();
    }

    private void OnEnable()
    {
        BindButton();
        if (ShouldPreserveRestoreIconVisuals())
        {
            RestoreInitialIconVisuals();
        }
    }

    private void OnDisable()
    {
        _iconLoadVersion++;
        if (ShouldPreserveRestoreIconVisuals())
        {
            RestoreInitialIconVisuals();
        }

        SetInteractable(false);
    }

    private void OnDestroy()
    {
        UnbindButton();

        if (BlockBg != null)
        {
            BlockBg.transform.DOKill();
        }
    }

    public void Bind(Skin skin, bool selectedSkin, bool ownedSkin, Action<SkinItem> clicked)
    {
        Bind(skin, selectedSkin, ownedSkin, clicked, false);
    }

    public void Bind(Skin skin, bool selectedSkin, bool ownedSkin, Action<SkinItem> clicked, bool animateState)
    {
        Bind(
            skin,
            selectedSkin ? SkinItemState.Selected : SkinItemState.Normal,
            ownedSkin,
            clicked,
            animateState);
    }

    /// <summary>
    /// 按显式状态绑定槽位；Restore 不需要 Skin 数据，Normal/Selected/Preview 必须传入有效 Skin。
    /// </summary>
    public void Bind(Skin skin, SkinItemState state, bool ownedSkin, Action<SkinItem> clicked, bool animateState)
    {
        ResolveReferences();
        if (state == SkinItemState.Restore)
        {
            BindRestore(clicked, animateState);
            return;
        }

        if (state == SkinItemState.None || skin == null)
        {
            Clear();
            return;
        }

        _preserveRestoreIconVisuals = false;
        var previousSkin = data;
        var previousState = _state;
        bool animateSelectionChange = animateState &&
            _hasBoundSkin &&
            ReferenceEquals(previousSkin, skin) &&
            IsHighlightedState(previousState) != IsHighlightedState(state);
        ResetBindingVisuals(false, !animateSelectionChange);

        data = skin;
        _state = state;
        isSelected = state == SkinItemState.Selected;
        _clicked = clicked;

        if (title != null)
        {
            var skinName = string.IsNullOrEmpty(skin.SkinName) ? skin.Id : skin.SkinName;
            var name = LocalizationManager.GetTranslation(skinName);
            title.text = name;
        }

        if (BlockBg != null && skinLevelsSprites != null && skinLevelsSprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp(skin.Quality, 0, skinLevelsSprites.Length - 1);
            BlockBg.sprite = skinLevelsSprites[spriteIndex];
        }

        if (icon != null)
        {
            icon.enabled = icon.sprite != null || _state == SkinItemState.Restore;
        }

        LoadIconAsync(skin, ++_iconLoadVersion).Forget();

        bool highlighted = IsHighlightedState(state);
        SwitchSelected(highlighted, animateSelectionChange);
        ApplySelectedScale(highlighted, animateSelectionChange);
        ApplyUnlockState(skin, ownedSkin);
        SetInteractable(skin.UnlockType != SkinUnlockType.Unavailable);
        Refresh();
        _hasBoundSkin = true;
    }

    /// <summary>
    /// 根据当前绑定的皮肤 ID 刷新单个列表项红点；不注册全局节点，也不修改待预览数据。
    /// </summary>
    public void Refresh()
    {
        if (redDot)
            redDot.SetActive(NewSkinRedDotQuery.Contains(data?.Id));
    }

    /// <summary>
    /// 绑定 Restore 特殊行；该行不加载皮肤图标、不改动 icon 显示状态、不显示解锁价格，只回调给 SkinPart 清理当前部件。
    /// </summary>
    public void BindRestore(Action<SkinItem> clicked, bool animateState = false)
    {
        ResolveReferences();
        // Restore prefab 自带 icon 表现，绑定时只拦截旧异步回写，不改 sprite / enabled。
        _preserveRestoreIconVisuals = true;
        ResetBindingVisuals(animateState, true, false);
        RestoreInitialIconVisuals();
        _state = SkinItemState.Restore;
        _clicked = clicked;
        _hasBoundSkin = false;

        string restoreText = LocalizationManager.GetTranslation("U_Skins_Des_05");
        if (title != null)
        {
            title.text = restoreText;
        }

        SetActive(defaultPart, true);
        SetText(defaultPart, restoreText);
        SetInteractable(true);
    }

    public void Bind(Skin skin, bool selectedSkin, Action<SkinItem> clicked)
    {
        Bind(skin, selectedSkin, false, clicked, false);
    }

    public void Clear()
    {
        bool preserveIconVisuals = ShouldPreserveRestoreIconVisuals();
        ResetBindingVisuals(false, true, !preserveIconVisuals);
        if (preserveIconVisuals)
        {
            RestoreInitialIconVisuals();
        }

        _hasBoundSkin = false;
    }

    private void ResetBindingVisuals(bool animateState, bool resetSelectionVisuals, bool resetIconVisuals = true)
    {
        data = null;
        isSelected = false;
        _state = SkinItemState.None;
        _clicked = null;
        _iconLoadVersion++;

        if (title != null)
        {
            title.text = string.Empty;
        }

        if (resetIconVisuals && icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
            _preserveRestoreIconVisuals = false;
        }

        if (BlockBg != null && _hasBlockBgDefaultSprite)
        {
            BlockBg.sprite = _blockBgDefaultSprite;
        }

        if (resetSelectionVisuals)
        {
            SwitchSelected(false, animateState);
            ApplySelectedScale(false, animateState);
        }

        SetActive(coinPart, false);
        SetActive(adPart, false);
        SetActive(defaultPart, false);
        SetInteractable(false);
    }

    private async UniTaskVoid LoadIconAsync(Skin skin, int loadVersion)
    {
        if (icon == null || skin == null || string.IsNullOrEmpty(skin.TextureResources) || ShouldPreserveRestoreIconVisuals())
        {
            return;
        }

        string location = SkinUtils.ResolveIconLocation(skin);
        if (string.IsNullOrEmpty(location))
        {
            this.LogWarning($"[SkinItem] Skin icon location is invalid. skin:{skin.Id}, texture:{skin.TextureResources}");
            return;
        }

        try
        {
            var sprite = await SkinUtils.LoadIconAsync(location);
            if (this == null ||
                loadVersion != _iconLoadVersion ||
                data != skin ||
                icon == null ||
                ShouldPreserveRestoreIconVisuals())
            {
                return;
            }

            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.SetNativeSize();
        }
        catch (Exception ex)
        {
            if (this != null && loadVersion == _iconLoadVersion && data == skin)
            {
                this.LogWarning($"[SkinItem] Load skin icon failed. skin:{skin.Id}, location:{location}, error:{ex.Message}");
            }
        }
    }

    private void ApplyUnlockState(Skin skin, bool ownedSkin)
    {
        if (ownedSkin || _state == SkinItemState.Selected || _state == SkinItemState.Preview)
        {
            SetActive(coinPart, false);
            SetActive(adPart, false);
            SetActive(defaultPart, true);
            SetText(defaultPart, ResolveDefaultText());
            return;
        }

        SetActive(coinPart, skin.UnlockType == SkinUnlockType.Coin);
        SetActive(adPart, skin.UnlockType == SkinUnlockType.Ad);
        SetActive(defaultPart,
            skin.UnlockType == SkinUnlockType.Default ||
            skin.UnlockType == SkinUnlockType.Special ||
            skin.UnlockType == SkinUnlockType.Purchase ||
            skin.UnlockType == SkinUnlockType.Unavailable);

        SetText(coinPart, skin.CoinPrice > 0 ? skin.CoinPrice.ToString() : string.Empty);
        SetText(adPart, string.IsNullOrEmpty(skin.UnlockHint) ? LocalizationManager.GetTranslation("U_Skins_Btn_01") : skin.UnlockHint);
        SetText(defaultPart, ResolveDefaultText(skin));
    }

    private string ResolveDefaultText(Skin skin = null)
    {
        if (skin != null &&
            skin.UnlockType == SkinUnlockType.Special &&
            !string.IsNullOrEmpty(skin.UnlockHint))
        {
            return ResolveLocalizedText(skin.UnlockHint);
        }

        return _state switch
        {
            SkinItemState.Restore => LocalizationManager.GetTranslation("U_Skins_Des_05"),
            SkinItemState.Preview => LocalizationManager.GetTranslation("U_Skins_Des_06"),
            SkinItemState.Selected => LocalizationManager.GetTranslation("U_Skins_Des_03"),
            _ => LocalizationManager.GetTranslation("U_Skins_Des_04")
        };
    }

    private static string ResolveLocalizedText(string keyOrText)
    {
        if (string.IsNullOrEmpty(keyOrText))
        {
            return string.Empty;
        }

        string localizedText = LocalizationManager.GetTranslation(keyOrText);
        return string.IsNullOrEmpty(localizedText) ? keyOrText : localizedText;
    }

    private void BindButton()
    {
        if (_isBound)
        {
            return;
        }

        ResolveReferences();
        if (btn != null)
        {
            btn.OnClick += OnButtonClicked;
        }

        _isBound = true;
    }

    private void UnbindButton()
    {
        if (!_isBound)
        {
            return;
        }

        if (btn != null)
        {
            btn.OnClick -= OnButtonClicked;
        }

        _isBound = false;
    }

    private void OnButtonClicked(CustomButton _)
    {
        if (btn == null || !btn.Interactable)
        {
            return;
        }

        if (data == null && _state != SkinItemState.Restore)
        {
            return;
        }

        _clicked?.Invoke(this);
    }

    private void SetInteractable(bool interactable)
    {
        if (btn != null)
        {
            btn.Interactable = interactable;
        }
    }

    private void ResolveReferences()
    {
        BlockBg ??= GetComponent<Image>();
        icon ??= FindChildComponent<Image>("icon");
        title ??= FindChildComponent<TextMeshProUGUI>("ui_pifu_04_txt");
        selected ??= FindChildComponent<Image>("selected");
        coinPart ??= FindChildGameObject("coinPart");
        adPart ??= FindChildGameObject("adPart");
        defaultPart ??= FindChildGameObject("defaultPart");
        btn ??= GetComponentInChildren<CustomButton>(true);
        CaptureInitialIconVisuals();
        CaptureBlockBgDefaultScale();
    }

    private void CaptureInitialIconVisuals()
    {
        if (_hasInitialIconVisuals || icon == null)
        {
            return;
        }

        _initialIconSprite = icon.sprite;
        _initialIconEnabled = icon.enabled;
        _hasInitialIconVisuals = true;
    }

    private void RestoreInitialIconVisuals()
    {
        if (!_hasInitialIconVisuals || icon == null)
        {
            return;
        }

        icon.sprite = _initialIconSprite;
        icon.enabled = _initialIconEnabled;
    }

    private bool ShouldPreserveRestoreIconVisuals()
    {
        return _state == SkinItemState.Restore || _preserveRestoreIconVisuals;
    }

    private void CaptureBlockBgDefaultScale()
    {
        if (BlockBg == null)
        {
            return;
        }

        if (!_hasBlockBgDefaultScale)
        {
            _blockBgDefaultScale = BlockBg.transform.localScale;
            _hasBlockBgDefaultScale = true;
        }

        if (!_hasBlockBgDefaultSprite)
        {
            _blockBgDefaultSprite = BlockBg.sprite;
            _hasBlockBgDefaultSprite = true;
        }
    }

    private void ApplySelectedScale(bool selectedState, bool animate)
    {
        if (BlockBg == null)
        {
            return;
        }

        CaptureBlockBgDefaultScale();
        var scale = selectedState
            ? _blockBgDefaultScale * SelectedScale
            : _blockBgDefaultScale;
        BlockBg.transform.DOKill();
        if (animate)
        {
            BlockBg.transform.DOScale(scale, 0.15f);
        }
        else
        {
            BlockBg.transform.localScale = scale;
        }
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        var child = FindChildRecursive(transform, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private GameObject FindChildGameObject(string childName)
    {
        var child = FindChildRecursive(transform, childName);
        return child != null ? child.gameObject : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            var result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void SetText(GameObject root, string value)
    {
        if (root == null)
        {
            return;
        }

        var text = root.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void SwitchSelected(bool active, bool animate)
    {
        if (selected == null)
        {
            return;
        }

        selected.DOKill();

        SetText(defaultPart, ResolveDefaultText());

        if (!active)
        {
            if (animate)
            {
                selected.gameObject.SetActive(true);
                selected.DOFade(0f, 0.15f).OnComplete(() =>
                {
                    if (selected != null && !IsHighlightedState(_state))
                    {
                        selected.gameObject.SetActive(false);
                        SetSelectedAlpha(1f);
                    }
                });
            }
            else
            {
                selected.gameObject.SetActive(false);
                SetSelectedAlpha(1f);
            }

            return;
        }

        selected.gameObject.SetActive(true);
        if (animate)
        {
            SetSelectedAlpha(0f);
            selected.DOFade(1f, 0.3f);
        }
        else
        {
            SetSelectedAlpha(1f);
        }
    }

    private static bool IsHighlightedState(SkinItemState state)
    {
        return state == SkinItemState.Selected || state == SkinItemState.Preview;
    }

    private void SetSelectedAlpha(float alpha)
    {
        if (selected == null)
        {
            return;
        }

        var color = selected.color;
        color.a = alpha;
        selected.color = color;
    }
}
