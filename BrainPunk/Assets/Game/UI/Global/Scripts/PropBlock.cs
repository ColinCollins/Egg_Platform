using System;
using Bear.EventSystem;
using Bear.Logger;
using Config.Game;
using DG.Tweening;
using Game.Events;
using UnityEngine;


public partial class PropBlock : BaseAutoUIBind, IDebuger
{
    [SerializeField] private GameProps Prop;
    [SerializeField] private bool IsAutoUpdate = true;

    /// <summary>
    /// 道具图标目标节点；用于奖励飞行动画对齐到图标中心和图标尺寸，未绑定时调用方应回退到 PropBlock 整体节点。
    /// </summary>
    [SerializeField] private RectTransform iconTarget;

    [SerializeField] private float countRollDuration = 0.5f;
    [SerializeField] private float floatingTipDuration = 0.65f;
    [SerializeField] private Vector2 floatingTipOffset = new Vector2(18f, 4f);
    [SerializeField] private Vector2 floatingTipMoveOffset = new Vector2(24f, 42f);
    [SerializeField] private float floatingTipScaleMultiplier = 1.25f;
    private EventSubscriber _subscriber;
    private Sequence floatingTipSequence;
    private RectTransform currentFloatingTip;
    private int lastUpdateOldCount;
    private int lastUpdateNewCount;
    private bool hasLastUpdateCount;

    void Awake()
    {
        if (IsAutoUpdate)
        {
            EventsUtils.ResetEvents(ref _subscriber);
            _subscriber.Subscribe<UpdatePropEvent>(OnPropUpdate);
        }

        Init();
    }

    void OnEnable()
    {
        BindData(GameManager.Instance.Bag.GetToolCount(Prop));
    }

    private void OnPropUpdate(UpdatePropEvent evt)
    {   
        // 1.5.0 tips 移除改用 coin
        if (Prop == GameProps.Tips || Prop == GameProps.Coin)
        {
            if (GameManager.Instance.Bag.HasTool(GameProps.UnlimitCoin))
            {
                CountTxt.text = "∞";
                return;
            }
        }

        if (Prop != evt.Prop)
            return;

        lastUpdateOldCount = evt.OldCount;
        lastUpdateNewCount = evt.NewCount;
        hasLastUpdateCount = true;

        PlayCountRoll(evt.OldCount, evt.NewCount, 0.4f);
    }

    public override void Init()
    {
        if (ShopBtn != null)
            ShopBtn.OnClick += ShowShop;
    }

    public void BindData(int oldCount)
    {
        if (Prop == GameProps.Tips || Prop == GameProps.Coin)
        {
            if (GameManager.Instance.Bag.HasTool(GameProps.UnlimitCoin))
            {
                CountTxt.text = "∞";
                return;
            }
        }

        CountTxt.text = oldCount.ToString();
    }

    /// <summary>
    /// 根据 prop 更新数据
    /// </summary>
    public void UpdateData()
    {
        CountTxt.DOKill();

        if (GameManager.Instance == null || GameManager.Instance.Bag == null)
        {
            this.LogWarning("UpdateData failed because GameManager or Bag is null.");
            return;
        }

        int newCount = GameManager.Instance.Bag.GetToolCount(Prop);
        UpdateDisplayCount(newCount);
    }

    /// <summary>
    /// 用途：把当前数量文本滚动到调用方指定的显示值，不读取或修改背包中的真实道具数量。
    /// 调用边界：仅供需要分阶段控制表现数值的 UI 使用；普通真实余额刷新仍调用无参 UpdateData()。
    /// 注意事项：调用方负责保证目标值符合业务语义；无限 Coin 状态仍由 BindData 统一显示为无限符号。
    /// </summary>
    /// <param name="newCount">本次 UI 需要显示的目标数量。</param>
    public void UpdateData(int newCount)
    {
        CountTxt.DOKill();
        UpdateDisplayCount(newCount);
    }

    private void UpdateDisplayCount(int newCount)
    {
        if (!int.TryParse(CountTxt.text, out int oldCount))
        {
            BindData(newCount);
            return;
        }

        if (oldCount == newCount)
        {
            BindData(newCount);
            return;
        }

        PlayCountRoll(oldCount, newCount, 0.4f);
    }

    /// <summary>
    /// 获取道具图标目标 RectTransform，供 UI 飞行动画使用；返回 false 表示未绑定或节点已失效。
    /// </summary>
    public bool TryGetIconTarget(out RectTransform target)
    {
        target = iconTarget;
        return target != null;
    }

    public void PlayRewardFeedback(int fallbackDelta)
    {
        CountTxt.DOKill();

        if (GameManager.Instance == null || GameManager.Instance.Bag == null)
        {
            this.LogWarning("PlayRewardFeedback failed because GameManager or Bag is null.");
            return;
        }

        int newCount = GameManager.Instance.Bag.GetToolCount(Prop);
        int delta = Mathf.Max(1, fallbackDelta);
        int oldCount = Mathf.Max(0, newCount - delta);

        if (hasLastUpdateCount && lastUpdateNewCount == newCount && lastUpdateNewCount > lastUpdateOldCount)
        {
            oldCount = lastUpdateOldCount;
            delta = lastUpdateNewCount - lastUpdateOldCount;
        }
        else if (int.TryParse(CountTxt.text, out int displayCount))
        {
            oldCount = displayCount < newCount ? displayCount : Mathf.Max(0, newCount - delta);
            delta = Mathf.Max(1, newCount - oldCount);
        }

        PlayCountRoll(oldCount, newCount, countRollDuration);
        PlayFloatingTip(delta);
        hasLastUpdateCount = false;
    }

    private void PlayCountRoll(int oldCount, int newCount, float duration)
    {
        CountTxt.DOKill();

        if (oldCount == newCount)
        {
            CountTxt.text = newCount.ToString();
            return;
        }

        CountTxt.DOCounter(oldCount, newCount, Mathf.Max(0.01f, duration)).SetUpdate(true);
    }

    private void PlayFloatingTip(int delta)
    {
        StopFloatingTip();

        if (delta <= 0 || CountTxt == null)
            return;

        CountTxt.ForceMeshUpdate();

        var tip = Instantiate(CountTxt, CountTxt.transform.parent);
        tip.name = $"{CountTxt.name}_FloatTip";
        tip.text = $"+{delta}";
        tip.color = CountTxt.color;
        tip.raycastTarget = false;
        tip.alignment = TMPro.TextAlignmentOptions.Left;
        tip.transform.SetAsLastSibling();

        RectTransform countRect = CountTxt.rectTransform;
        RectTransform tipRect = tip.rectTransform;
        Vector2 startPoint = countRect.anchoredPosition
            + Vector2.right * (CountTxt.preferredWidth * 0.5f + floatingTipOffset.x)
            + Vector2.up * floatingTipOffset.y;

        tipRect.anchorMin = countRect.anchorMin;
        tipRect.anchorMax = countRect.anchorMax;
        tipRect.pivot = new Vector2(0f, 0.5f);
        tipRect.sizeDelta = new Vector2(90f, countRect.rect.height);
        tipRect.anchoredPosition = startPoint;
        tipRect.localRotation = Quaternion.identity;
        tipRect.localScale = Vector3.one * floatingTipScaleMultiplier;

        CanvasGroup canvasGroup = tip.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = tip.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        currentFloatingTip = tipRect;
        Sequence sequence = DOTween.Sequence()
            .SetTarget(tipRect)
            .SetUpdate(true);

        float duration = Mathf.Max(0.01f, floatingTipDuration);
        sequence
            .Append(canvasGroup.DOFade(1f, duration * 0.18f))
            .Join(tipRect.DOScale(Vector3.one, duration * 0.28f).SetEase(Ease.OutBack))
            .AppendInterval(duration * 0.25f)
            .Append(tipRect.DOAnchorPos(startPoint + floatingTipMoveOffset, duration * 0.57f).SetEase(Ease.OutSine))
            .Join(canvasGroup.DOFade(0f, duration * 0.57f).SetEase(Ease.InSine))
            .OnComplete(() =>
            {
                if (currentFloatingTip == tipRect)
                    currentFloatingTip = null;

                if (tipRect != null)
                    Destroy(tipRect.gameObject);
            })
            .OnKill(() =>
            {
                if (floatingTipSequence == sequence)
                    floatingTipSequence = null;
            });

        floatingTipSequence = sequence;
    }

    private void StopFloatingTip()
    {
        if (floatingTipSequence != null && floatingTipSequence.IsActive())
            floatingTipSequence.Kill();

        floatingTipSequence = null;

        if (currentFloatingTip != null)
        {
            Destroy(currentFloatingTip.gameObject);
            currentFloatingTip = null;
        }
    }

    private void ShowShop(CustomButton btn)
    {
        this.Log("Show Shop");
        ShopPanel.Create(false, "GameTipsPopup");
    }

    void OnDestroy()
    {
        CountTxt.DOKill();
        StopFloatingTip();
    }
}
