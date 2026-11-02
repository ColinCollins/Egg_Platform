using TMPro;
using UnityEngine;

/// <summary>
/// 商店型商品展示项的轻量基类。
/// 只统一按钮生命周期、交互开关和折扣展示，不持有商品选择、购买、广告或奖励交付业务。
/// </summary>
public abstract class ShopItemBase : BaseAutoUIBind
{
    #region Inspector 配置

    [Tooltip("商品项主操作按钮；基类只负责绑定点击并转发给子类。")]
    [SerializeField] private CustomButton Btn;

    [Tooltip("折扣展示根节点；Shop.discount 为 0 时隐藏，非 0 时显示。")]
    [SerializeField] private GameObject discountPart;

    [Tooltip("折扣值文本；首行按“{0}%”展示 Shop.discount，次行显示 off。")]
    [SerializeField] private TextMeshProUGUI discountText;

    #endregion

    #region 运行时状态

    private bool _isButtonBound;

    /// <summary>
    /// 当前商品项的主操作按钮。
    /// 调用边界：供子类和所属 UI 刷新交互状态，不负责判断具体商品是否可购买。
    /// </summary>
    protected CustomButton ItemButton => Btn;

    /// <summary>按钮当前是否允许把点击转发给子类。</summary>
    protected virtual bool CanInvokeItemAction => Btn != null && Btn.Interactable;

    #endregion

    #region 生命周期

    protected virtual void Awake()
    {
        BindButton();
    }

    protected virtual void OnDestroy()
    {
        UnbindButton();
    }

    /// <summary>
    /// 初始化通用商品项交互。
    /// 调用边界：可由所属 UI 重复调用，内部会防止按钮事件重复绑定。
    /// </summary>
    public override void Init()
    {
        BindButton();
    }

    #endregion

    #region 公共展示能力

    /// <summary>
    /// 使用 Shop.discount 刷新折扣展示。
    /// 调用边界：配置值为 0 时隐藏整个折扣区域；非 0 时首行显示“{0}%”，次行显示 off。
    /// </summary>
    public void SetDiscount(int discount)
    {
        bool visible = discount != 0;
        if (discountPart != null)
            discountPart.SetActive(visible);

        if (discountText != null)
            discountText.text = visible ? string.Format("{0}%\noff", discount) : string.Empty;
    }

    /// <summary>
    /// 设置商品项主按钮是否可交互。
    /// 调用边界：只修改按钮状态，售罄、购买中等业务条件由调用方合并后传入。
    /// </summary>
    public void SetItemInteractable(bool interactable)
    {
        if (Btn != null)
            Btn.Interactable = interactable;
    }

    #endregion

    #region 点击转发

    private void BindButton()
    {
        if (_isButtonBound || Btn == null)
            return;

        Btn.OnClick += HandleButtonClick;
        _isButtonBound = true;
    }

    private void UnbindButton()
    {
        if (!_isButtonBound || Btn == null)
            return;

        Btn.OnClick -= HandleButtonClick;
        _isButtonBound = false;
    }

    private void HandleButtonClick(CustomButton _)
    {
        if (CanInvokeItemAction)
            OnItemActionClicked();
    }

    /// <summary>
    /// 接收基类校验后的商品项点击。
    /// 调用边界：子类只做局部事件转发，购买、广告和发奖必须由所属控制器处理。
    /// </summary>
    protected abstract void OnItemActionClicked();

    #endregion
}
