using System;
using System.Collections.Generic;
using Config.Game;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ConfigModule;
using Game.Scripts.Common;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// version 1.0 
/// 点击切换 bubble 显示。_isShow = true 情况下，点击可以立马关闭。 = false 情况下展示。isShow 的时候 需要 DOScale 0 -> 1。false 不需要，直接 scale = 0；
/// RefreshITems 的时候自动隐藏。然后刷新 rewardItems
/// 
/// rewardItem 根据 rewards 内容进行生成填充。
/// 新的内容如果数量匹配不上，进行隐藏或者生成。跟随 PostcardPopup 界面销毁而销毁
/// </summary>
public class Postcard_Chest : MonoBehaviour
{
    private const float BubbleShowDuration = 0.2f;
    private const float ProgressFillDuration = 0.2f;
    private const string ChestIdleAnimName = "box_idle";
    private const string ChestOpenAnimName = "box-open";
    private const string ChestOpenIdleAnimName = "box-open_idle";
    private const string ChestOpenEffectTrigger = "play";
    private const float ChestOpenEffectDelay = 0.5f;

    /// <summary>
    /// 用途：配置 complete 流程中 Chest 缩放隐藏动画的持续时间。
    /// 调用边界：仅在 Hide(true) 的缩放隐藏分支中使用，普通 Hide() 仍立即隐藏。
    /// 注意事项：Inspector 中限制为非负数，运行时仍会按最小 0 秒保护。
    /// </summary>
    [Header("隐藏动画")]
    [SerializeField, Min(0f)] private float chestHideScaleDuration = 1f;

    /// <summary>
    /// 用途：配置 complete 流程中 Chest 缩放隐藏动画的 DOTween 缓动类型。
    /// 调用边界：仅在 Hide(true) 创建隐藏 Tween 时使用。
    /// 注意事项：默认保留当前 Linear 表现，可在 PostcardPopup prefab 的 Inspector 中调整。
    /// </summary>
    [SerializeField] private Ease chestHideEase = Ease.Linear;

    [SerializeField] private RectTransform bubble;
    [SerializeField] private List<CommonRewardItem> rewardItems;
    [SerializeField] private CustomButton clickBtn;

    /// <summary>
    /// 用途：Coin 奖励预览图标，解析规则与 CommonGetRewardPopup 的 Coin 图标一致。
    /// 调用边界：仅用于 Chest bubble 奖励预览，不参与发奖、存档或资源加载。
    /// 注意事项：应在 prefab 上绑定与 CommonGetRewardPopup.coinSprite 相同的资源。
    /// </summary>
    [SerializeField] private Sprite coinSprite;

    /// <summary>
    /// 用途：Star 奖励预览图标，解析规则与 CommonGetRewardPopup 的 Star 图标一致。
    /// 调用边界：仅用于 Chest bubble 奖励预览，不参与发奖、存档或资源加载。
    /// 注意事项：应在 prefab 上绑定与 CommonGetRewardPopup.starSprite 相同的资源。
    /// </summary>
    [SerializeField] private Sprite starSprite;

    [SerializeField] private Image progressBar;
    [SerializeField] TMP_Text progressTxt;

    [SerializeField] private Animator openEffect;

    /// <summary>
    /// 用途：播放明信片宝箱的关闭、开箱和开箱待机 Spine 动画。
    /// 调用边界：优先由 prefab 序列化绑定；未绑定时运行时从子级查找。
    /// 注意事项：只负责本地 UI 表现，不发奖、不写存档、不派发事件。
    /// </summary>
    [SerializeField] private UISpineCtrl spineCtrl;

    [SerializeField] private float bubbleWaitingTime = 3f;
    private bool _isShow;
    private Dictionary<GameProps, object> _rewards;
    private Config.Skin _skinReward;
    private CommonRewardItem _skinRewardItem;
    private int _skinIconLoadVersion;
    private Tween _bubbleTween;
    private Tween _autoHideTween;
    private Tween _progressTween;
    private Tween _hideTween;
    private Spine.TrackEntry _chestOpenEntry;
    private int _chestOpenVersion;

    void Awake()
    {
        if (clickBtn == null)
            clickBtn = GetComponentInChildren<CustomButton>(true);

        if (clickBtn != null)
        {
            clickBtn.OnClick -= ClickChest;
            clickBtn.OnClick += ClickChest;
        }

    }

    public void Show()
    {
        StopChestOpenPresentation();
        StopHideTween();
        transform.localScale = Vector3.one;
        gameObject.SetActive(true);
        PlayChestIdleAnim();
    }

    /// <summary>
    /// 用途：隐藏 Chest；完成流程可选择先缩放到 0，再关闭对象。
    /// 调用边界：普通页面刷新使用默认立即隐藏；仅整页 complete 动画结束后传入 true 播放缩放隐藏。
    /// 注意事项：缩放 Tween 使用 unscaled time；中途重新 Show、禁用或销毁时会停止 Tween，并恢复根节点缩放为 1。
    /// </summary>
    /// <param name="playScaleAnimation">true 表示先 DOScale 到 0 后隐藏；false 表示立即隐藏。</param>
    public void Hide(bool playScaleAnimation = false)
    {
        StopHideTween();
        if (!playScaleAnimation || !gameObject.activeInHierarchy)
        {
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
            return;
        }

        _hideTween = transform.DOScale(Vector3.zero, Mathf.Max(0f, chestHideScaleDuration))
            .SetEase(chestHideEase)
            .SetUpdate(true)
            .SetTarget(transform)
            .OnComplete(() =>
            {
                _hideTween = null;
                if (this == null)
                    return;

                gameObject.SetActive(false);
                transform.localScale = Vector3.one;
            });
    }

    private void OnDisable()
    {
        _skinIconLoadVersion++;
        _skinReward = null;
        _skinRewardItem = null;
        StopBubbleTweens();
        StopProgressTween();
        StopHideTween();
        StopChestOpenPresentation();
        transform.localScale = Vector3.one;
        if (bubble != null)
            bubble.localScale = Vector3.zero;
        _isShow = false;
    }

    private void OnDestroy()
    {
        if (clickBtn != null)
            clickBtn.OnClick -= ClickChest;
        _skinIconLoadVersion++;
        _skinReward = null;
        _skinRewardItem = null;
        StopBubbleTweens();
        StopProgressTween();
        StopHideTween();
        StopChestOpenPresentation();
    }

    public void ClickChest(CustomButton btn)
    {
        SwitchBubble(!_isShow);
    }

    public void SwitchBubble(bool isShow)
    {
        _isShow = isShow;
        if (bubble == null)
            return;

        StopBubbleTweens();
        if (isShow)
        {
            bubble.gameObject.SetActive(true);
            bubble.localScale = Vector3.zero;
            _bubbleTween = bubble.DOScale(Vector3.one, BubbleShowDuration);

            if (bubbleWaitingTime > 0f)
                _autoHideTween = DOVirtual.DelayedCall(bubbleWaitingTime, () => SwitchBubble(false));
        }
        else
        {
            bubble.localScale = Vector3.zero;
        }
    }

    // 隐藏 bubble
    public void RefreshItems(Dictionary<GameProps, object> rewards)
    {
        SwitchBubble(false);
        _skinIconLoadVersion++;
        _skinReward = null;
        _skinRewardItem = null;
        _rewards = rewards;
        RefreshRewardItems();
    }

    private void StopBubbleTweens()
    {
        _bubbleTween?.Kill();
        _bubbleTween = null;
        _autoHideTween?.Kill();
        _autoHideTween = null;
    }

    private void RefreshRewardItems()
    {
        _skinIconLoadVersion++;
        _skinReward = null;
        _skinRewardItem = null;

        if (rewardItems == null || rewardItems.Count <= 0)
            return;

        int itemIndex = 0;
        if (_rewards != null)
        {
            foreach (var reward in _rewards)
            {
                if (itemIndex >= rewardItems.Count)
                    break;

                if (reward.Key == GameProps.Skin)
                {
                    Config.Skin skin = ResolveSkinReward(reward.Value);
                    if (skin == null)
                        continue;

                    CommonRewardItem item = TakeNextRewardItem(ref itemIndex);
                    if (item == null)
                        continue;

                    item.gameObject.SetActive(true);
                    item.RefreshSkin(null, 1);
                    _skinReward = skin;
                    _skinRewardItem = item;
                    LoadSkinRewardIconAsync(skin, item, ++_skinIconLoadVersion).Forget();
                    continue;
                }

                int count = GetRewardCount(reward.Value);
                if (count <= 0)
                    continue;

                Sprite sprite = ResolveRewardSprite(reward.Key);
                if (sprite == null)
                    continue;

                CommonRewardItem rewardItem = TakeNextRewardItem(ref itemIndex);
                if (rewardItem == null)
                    continue;

                rewardItem.gameObject.SetActive(true);
                rewardItem.Refresh(sprite, count);
            }
        }

        for (int i = itemIndex; i < rewardItems.Count; i++)
        {
            if (rewardItems[i] != null)
                rewardItems[i].gameObject.SetActive(false);
        }
    }

    private CommonRewardItem TakeNextRewardItem(ref int itemIndex)
    {
        while (itemIndex < rewardItems.Count)
        {
            CommonRewardItem item = rewardItems[itemIndex];
            itemIndex++;
            if (item != null)
                return item;
        }

        return null;
    }

    /// <summary>
    /// 传入当前完成进度和最大值
    /// </summary>
    /// <param name="current"></param>
    /// <param name="max"></param>
    /// <param name="isImmediate">是否立即刷新进度，切换配置页时用于避免旧进度动画残留。</param>
    public void RefreshProgress(int current, int max, bool isImmediate = false)
    {
        int safeMax = Mathf.Max(0, max);
        int safeCurrent = safeMax > 0 ? Mathf.Clamp(current, 0, safeMax) : 0;
        float targetProgress = safeMax > 0 ? (float)safeCurrent / safeMax : 0f;

        if (progressTxt != null)
            progressTxt.text = $"{safeCurrent}/{safeMax}";

        StopProgressTween();
        if (progressBar == null)
            return;

        if (isImmediate || !gameObject.activeInHierarchy)
        {
            progressBar.fillAmount = targetProgress;
            return;
        }

        _progressTween = progressBar.DOFillAmount(targetProgress, ProgressFillDuration)
            .SetEase(Ease.OutQuad)
            .SetTarget(progressBar);
    }

    private void StopProgressTween()
    {
        _progressTween?.Kill();
        _progressTween = null;
    }

    private void StopHideTween()
    {
        _hideTween?.Kill();
        _hideTween = null;
    }

    /// <summary>
    /// 用途：播放宝箱打开动画，并在动画完成后切换到打开待机动画。
    /// 调用边界：由 `PostcardPopup` 在当前明信片页完成流程开始时调用。
    /// 注意事项：只控制本地 Spine 表现并隐藏奖励 bubble，不发奖、不写存档、不推进页面流程。
    /// </summary>
    /// <returns>成功播放时返回本次 Spine 动画 TrackEntry；对象未激活、缺少绑定或动画失败时返回 null。</returns>
    public Spine.TrackEntry PlayChestOpenAnim()
    {
        SwitchBubble(false);
        StopChestOpenPresentation();

        if (!gameObject.activeInHierarchy)
            return null;

        UISpineCtrl chestSpine = ResolveSpineCtrl();
        if (chestSpine == null)
            return null;

        Spine.TrackEntry entry = chestSpine.PlayAnimation(ChestOpenAnimName, false, false);
        if (entry == null)
        {
            chestSpine.PlayAnimation(ChestOpenIdleAnimName, true, false);
            return null;
        }

        _chestOpenEntry = entry;
        entry.Complete += OnChestOpenAnimComplete;
        PlayChestOpenEffectAsync(_chestOpenVersion).Forget();
        return entry;
    }

    private void OnChestOpenAnimComplete(Spine.TrackEntry entry)
    {
        if (entry != null)
            entry.Complete -= OnChestOpenAnimComplete;

        if (entry != _chestOpenEntry)
            return;

        _chestOpenEntry = null;

        if (this == null || !isActiveAndEnabled)
            return;

        UISpineCtrl chestSpine = ResolveSpineCtrl();
        if (chestSpine != null)
            chestSpine.PlayAnimation(ChestOpenIdleAnimName, true, false);
    }

    private async UniTask PlayChestOpenEffectAsync(int openVersion)
    {
        if (openEffect == null)
            return;

        AudioManager.PlaySound("openChest");
        await UniTask.WaitForSeconds(ChestOpenEffectDelay);
        if (this == null || openVersion != _chestOpenVersion || !isActiveAndEnabled || openEffect == null)
            return;

        openEffect.SetTrigger(ChestOpenEffectTrigger);
    }

    private void StopChestOpenPresentation()
    {
        _chestOpenVersion++;
        if (_chestOpenEntry != null)
            _chestOpenEntry.Complete -= OnChestOpenAnimComplete;

        _chestOpenEntry = null;
    }

    private void PlayChestIdleAnim()
    {
        UISpineCtrl chestSpine = ResolveSpineCtrl();
        if (chestSpine != null)
            chestSpine.PlayAnimation(ChestIdleAnimName, false);
    }

    private UISpineCtrl ResolveSpineCtrl()
    {
        if (spineCtrl == null)
            spineCtrl = GetComponentInChildren<UISpineCtrl>(true);

        return spineCtrl;
    }

    private Sprite ResolveRewardSprite(GameProps prop)
    {
        return prop switch
        {
            GameProps.Coin => coinSprite,
            GameProps.Star => starSprite,
            _ => null,
        };
    }

    private static int GetRewardCount(object value)
    {
        return BaseParamUtils.TryGetInt(value, out int count) ? count : 0;
    }

    private static Config.Skin ResolveSkin(string skinId)
    {
        if (string.IsNullOrEmpty(skinId))
            return null;

        return ConfigManager.Instance?.Tables?.TbSkin?.GetOrDefault(skinId);
    }

    private static Config.Skin ResolveSkinReward(object rewardValue)
    {
        switch (rewardValue)
        {
            case Config.Skin skin:
                return skin;
            default:
                return BaseParamUtils.TryGetString(rewardValue, out string skinId)
                    ? ResolveSkin(skinId)
                    : null;
        }
    }

    private async UniTaskVoid LoadSkinRewardIconAsync(Config.Skin skin, CommonRewardItem item, int loadVersion)
    {
        if (skin == null || item == null || string.IsNullOrEmpty(skin.TextureResources))
            return;

        string location = SkinUtils.ResolveIconLocation(skin);
        if (string.IsNullOrEmpty(location))
            return;

        try
        {
            Sprite sprite = await SkinUtils.LoadIconAsync(location);
            if (this == null ||
                loadVersion != _skinIconLoadVersion ||
                _skinReward != skin ||
                _skinRewardItem != item ||
                item == null)
            {
                return;
            }

            item.RefreshSkin(sprite, 1);
        }
        catch (Exception)
        {
        }
    }

    [Button("Test")]
    public void PlayShiny()
    {
        PlayChestOpenAnim();
    }
}
