using System;
using Sirenix.OdinInspector;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 用于触发 postcard unlock progress
///
/// 补充 unlock 状态。coverImage 默认隐藏，解锁时播放翻页并保持显示。redDot 和 blockCost 隐藏掉。
///
/// </summary>
public class Postcard_ProgressItem : MonoBehaviour
{
    // 进度 id
    [SerializeField] private int index = 0;

    public int Index => index;

    [SerializeField] private CustomButton clickBtn;

    public CustomButton ClickBtn => clickBtn;

    [SerializeField] private TMP_Text text;

    /// <summary>
    /// 用途：显示解锁所需星星数量的消耗块；解锁后隐藏。
    /// 调用边界：由 Refresh 和 PlayUnlockAnim 按当前 item 状态控制显隐。
    /// 注意事项：该节点只负责 UI 表现，不处理扣星、存档或奖励发放。
    /// </summary>
    [SerializeField] private GameObject blockCost;

    /// <summary>
    /// 用途：解锁后显示的明信片碎片图片，默认未解锁状态保持 inactive。
    /// 调用边界：由 Refresh 和 PlayUnlockAnim 控制显隐，AutoBind 按末位为 1 的明信片图片自动绑定。
    /// 注意事项：该节点只负责本地 UI 表现，不处理扣星、存档或奖励发放。
    /// </summary>
    [SerializeField] private Image coverImg;

    /// <summary>
    /// 用途：驱动 coverImg 的明信片翻页显示表现。
    /// 调用边界：由 PlayUnlockAnim 在解锁流程被接受后调用；为空时会从 coverImg 同物体兜底查找。
    /// 注意事项：播放失败或未绑定时会直接完成回调，避免上层解锁流程卡住。
    /// </summary>
    [SerializeField] private Postcard_TurnEffect turnEffect;

    /// <summary>
    /// 用途：兼容旧遮罩动画配置，并作为解锁时本体轻微缩放反馈的时长参考。
    /// 调用边界：仅 PlayUnlockAnim 的本地缩放反馈使用；翻页表现时长由 Postcard_TurnEffect 控制。
    /// 注意事项：不再驱动 coverImg.fillAmount，不创建对象、不写存档。
    /// </summary>
    [SerializeField] private float unlockAnimDuration = 1f;

    [SerializeField] private GameObject redDot;


    /// <summary>
    /// 是否已经解锁
    /// </summary>
    private bool _isUnlock = false;

    private int _unlockCount = 0;
    private Tween _unlockTween;
    private Vector3 _unlockBaseScale = Vector3.one;
    private bool _hasUnlockBaseScale;
    private Tween _redDotTween;
    private Tween _redDotShakeTween;
    private Vector3 _redDotBaseScale;
    private Vector3 _redDotBaseEulerAngles;
    private bool _hasRedDotBaseScale;
    private bool _hasRedDotBaseRotation;

    /// <summary>
    /// 传参是否已经 isUnlock
    /// </summary>
    /// <param name="isUnlock"></param>
    /// <param name="unlockCount"></param>
    /// <param name="currentStar"></param>
    public void Refresh(bool isUnlock, int unlockCount, int currentStar)
    {
        StopUnlockTween();
        _isUnlock = isUnlock;
        _unlockCount = unlockCount;

        if (text != null)
            text.text = unlockCount.ToString();

        ApplyUnlockState(currentStar);
    }

    /// <summary>
    /// 解锁动画，coverImg 激活后播放 Postcard_TurnEffect.ClosePage。redDot 和 blockCost 隐藏掉。
    /// </summary>

    public void PlayUnlockAnim()
    {
        PlayUnlockAnim(null);
    }

    /// <summary>
    /// 用途：播放当前进度 item 的解锁翻页表现，并在动画完成或降级直接完成时回调调用方。
    /// 调用边界：由 Postcard_PageItem 在确认扣除星星并写入明信片进度后调用。
    /// 注意事项：回调只表示本地 UI 表现结束，不代表存档或奖励发放结果。
    /// </summary>
    /// <param name="onComplete">解锁表现完成后的回调，可为空。</param>
    /// <param name="forceCompleteOnInterrupt">true 时翻页表现因组件禁用或销毁中断会强制执行完成回调；仅应传给已保存且不需要进入页面完成流程的普通解锁。</param>
    public void PlayUnlockAnim(Action onComplete, bool forceCompleteOnInterrupt = false)
    {
        StopUnlockTween();
        _isUnlock = true;

        if (clickBtn != null)
            clickBtn.Interactable = false;
        StopRedDotTween();
        if (redDot != null)
            redDot.SetActive(false);
        HideBlockCost();

        ApplyCoverActiveState(true);
        PlayUnlockPulse();

        bool hasCompleted = false;
        void CompleteUnlockAnim()
        {
            if (hasCompleted)
                return;

            hasCompleted = true;
            ApplyCoverActiveState(true);
            onComplete?.Invoke();
        }

        Postcard_TurnEffect resolvedTurnEffect = ResolveTurnEffect();
        if (resolvedTurnEffect == null || !resolvedTurnEffect.isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            CompleteUnlockAnim();
            return;
        }

        if (!resolvedTurnEffect.TryClosePage(CompleteUnlockAnim, forceCompleteOnInterrupt))
            CompleteUnlockAnim();
    }

    private void Awake()
    {
        CaptureUnlockBaseScale();
    }

    private void OnEnable()
    {
        if (redDot != null && redDot.activeSelf)
            PlayRedDotTween();
    }

    private void OnDisable()
    {
        StopUnlockTween();
        StopRedDotTween();
        if (_isUnlock)
            ApplyCoverActiveState(true);
        if (_isUnlock)
            HideBlockCost();
    }

    private void OnDestroy()
    {
        StopUnlockTween();
        StopRedDotTween();
    }

    private void ApplyUnlockState(int currentStar)
    {
        ApplyCoverActiveState(_isUnlock);

        if (clickBtn != null)
            clickBtn.Interactable = !_isUnlock;

        if (blockCost != null)
            blockCost.SetActive(!_isUnlock);

        if (redDot != null)
        {
            bool shouldShowRedDot = !_isUnlock && currentStar >= _unlockCount;
            StopRedDotTween();
            redDot.SetActive(shouldShowRedDot);
            if (shouldShowRedDot)
                PlayRedDotTween();
        }
    }

    private void HideBlockCost()
    {
        if (blockCost == null)
            return;

        // clickArea 会随消耗块立即隐藏，先收口按压回弹，避免禁用后停在 0.8～1 之间。
        if (clickBtn != null)
            clickBtn.transform.localScale = Vector3.one;

        blockCost.SetActive(false);
    }

    private void ApplyCoverActiveState(bool isActive)
    {
        if (coverImg == null)
            return;

        coverImg.fillAmount = 1f;
        if (coverImg.gameObject.activeSelf != isActive)
            coverImg.gameObject.SetActive(isActive);
    }

    private Postcard_TurnEffect ResolveTurnEffect()
    {
        if (turnEffect == null && coverImg != null)
            turnEffect = coverImg.GetComponent<Postcard_TurnEffect>();

        return turnEffect;
    }

    private void PlayUnlockPulse()
    {
        if (!gameObject.activeInHierarchy)
            return;

        float pulseDuration = Mathf.Min(0.2f, Mathf.Max(0f, unlockAnimDuration));
        if (pulseDuration <= 0f)
            return;

        CaptureUnlockBaseScale();
        transform.localScale = _unlockBaseScale;
        _unlockTween = transform.DOScale(_unlockBaseScale * 1.1f, pulseDuration)
            .SetLoops(2, LoopType.Yoyo)
            .SetDelay(0.6f)
            .SetEase(Ease.OutCirc)
            .OnComplete(() =>
            {
                transform.localScale = _unlockBaseScale;
                _unlockTween = null;
            });
    }

    private void StopUnlockTween()
    {
        Tween unlockTween = _unlockTween;
        _unlockTween = null;
        unlockTween?.Kill();
        RestoreUnlockBaseScale();
    }

    private void CaptureUnlockBaseScale()
    {
        if (_hasUnlockBaseScale)
            return;

        _unlockBaseScale = transform.localScale;
        _hasUnlockBaseScale = true;
    }

    private void RestoreUnlockBaseScale()
    {
        if (_hasUnlockBaseScale)
            transform.localScale = _unlockBaseScale;
    }

    private void PlayRedDotTween()
    {
        if (!Application.isPlaying || redDot == null || !redDot.activeInHierarchy)
            return;

        if (!_hasRedDotBaseScale)
        {
            _redDotBaseScale = redDot.transform.localScale;
            _hasRedDotBaseScale = true;
        }

        if (!_hasRedDotBaseRotation)
        {
            _redDotBaseEulerAngles = redDot.transform.localEulerAngles;
            _hasRedDotBaseRotation = true;
        }

        _redDotTween = redDot.transform.DOScale(_redDotBaseScale * 1.1f, 0.8f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        redDot.transform.localEulerAngles = _redDotBaseEulerAngles;
        Tweener shakeTweener = redDot.transform
            .DOShakeRotation(0.3f, new Vector3(0f, 0f, 18f), 5, 10f, true)
            .SetEase(Ease.Linear)
            .SetLoops(2, LoopType.Restart);

        _redDotShakeTween = DOTween.Sequence()
            .Append(shakeTweener)
            .AppendInterval(0.7f)
            .SetUpdate(true)
            .SetLoops(-1, LoopType.Restart)
            .SetAutoKill(false);
    }

    private void StopRedDotTween()
    {
        _redDotTween?.Kill();
        _redDotTween = null;
        _redDotShakeTween?.Kill();
        _redDotShakeTween = null;

        if (_hasRedDotBaseScale && redDot != null)
            redDot.transform.localScale = _redDotBaseScale;
        if (_hasRedDotBaseRotation && redDot != null)
            redDot.transform.localEulerAngles = _redDotBaseEulerAngles;
    }

    [Button("AutoBind")]
    public void AutoBind()
    {
#if UNITY_EDITOR
        CustomButton bindClickBtn = FindChildComponentByName<CustomButton>(transform, "clickArea");
        TMP_Text bindText = GetComponentInChildren<TMP_Text>(true);
        GameObject bindBlockCost = FindBlockCost(bindText);
        GameObject bindRedDot = FindChildGameObjectByNamePrefix(transform, "UI_common_img_gantan_01");
        Image bindCoverImg = FindDirectCoverImage(bindRedDot, bindClickBtn);
        Postcard_TurnEffect bindTurnEffect = FindTurnEffect(bindCoverImg);

        bool shouldBindClickBtn = bindClickBtn != null && clickBtn != bindClickBtn;
        bool shouldBindText = bindText != null && text != bindText;
        bool shouldBindBlockCost = bindBlockCost != null && blockCost != bindBlockCost;
        bool shouldBindCoverImg = bindCoverImg != null && coverImg != bindCoverImg;
        bool shouldBindTurnEffect = bindTurnEffect != null && turnEffect != bindTurnEffect;
        bool shouldBindRedDot = bindRedDot != null && redDot != bindRedDot;
        bool shouldHideCoverImg = bindCoverImg != null && bindCoverImg.gameObject.activeSelf;

        if (!shouldBindClickBtn
            && !shouldBindText
            && !shouldBindBlockCost
            && !shouldBindCoverImg
            && !shouldBindTurnEffect
            && !shouldBindRedDot
            && !shouldHideCoverImg)
            return;

        Undo.RecordObject(this, "Postcard_ProgressItem AutoBind");
        if (shouldHideCoverImg)
            Undo.RecordObject(bindCoverImg.gameObject, "Postcard_ProgressItem AutoBind Cover Active");

        if (shouldBindClickBtn)
            clickBtn = bindClickBtn;
        if (shouldBindText)
            text = bindText;
        if (shouldBindBlockCost)
            blockCost = bindBlockCost;
        if (shouldBindCoverImg)
            coverImg = bindCoverImg;
        if (shouldBindTurnEffect)
            turnEffect = bindTurnEffect;
        if (shouldBindRedDot)
            redDot = bindRedDot;
        if (shouldHideCoverImg)
            bindCoverImg.gameObject.SetActive(false);

        EditorUtility.SetDirty(this);
        if (shouldHideCoverImg)
            EditorUtility.SetDirty(bindCoverImg.gameObject);
        if (PrefabUtility.IsPartOfPrefabInstance(this))
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        if (shouldHideCoverImg && PrefabUtility.IsPartOfPrefabInstance(bindCoverImg.gameObject))
            PrefabUtility.RecordPrefabInstancePropertyModifications(bindCoverImg.gameObject);
#endif
    }

#if UNITY_EDITOR
    private const string CoverImageNamePrefix = "UI_mingxinpian_img_";
    private const string CoverImageNameSuffix = "_01";

    private GameObject FindBlockCost(TMP_Text bindText)
    {
        GameObject found = FindChildGameObjectByName(transform, "PostcardCostBlock");
        if (found != null)
            return found;

        found = FindChildGameObjectByName(transform, "Postcard_CostBlock");
        if (found != null)
            return found;

        return bindText == null || bindText.transform.parent == null
            ? null
            : bindText.transform.parent.gameObject;
    }

    private Image FindDirectCoverImage(GameObject bindRedDot, CustomButton bindClickBtn)
    {
        Transform redDotTransform = bindRedDot == null ? null : bindRedDot.transform;
        Transform clickTransform = bindClickBtn == null ? null : bindClickBtn.transform;
        Image fallbackImage = null;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == redDotTransform || child == clickTransform)
                continue;

            Image image = child.GetComponent<Image>();
            if (image == null)
                continue;

            if (IsCoverImageName(child.name) || (image.sprite != null && IsCoverImageName(image.sprite.name)))
                return image;

            if (fallbackImage == null)
                fallbackImage = image;
        }

        return fallbackImage;
    }

    private static Postcard_TurnEffect FindTurnEffect(Image bindCoverImg)
    {
        return bindCoverImg == null ? null : bindCoverImg.GetComponent<Postcard_TurnEffect>();
    }

    private static bool IsCoverImageName(string imageName)
    {
        if (string.IsNullOrEmpty(imageName))
            return false;

        string normalizedName = imageName.Trim();
        if (!normalizedName.StartsWith(CoverImageNamePrefix, System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (!normalizedName.EndsWith(CoverImageNameSuffix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        int numberStart = CoverImageNamePrefix.Length;
        int numberEnd = normalizedName.Length - CoverImageNameSuffix.Length;
        if (numberEnd <= numberStart)
            return false;
        if (normalizedName[numberEnd - 1] != '1')
            return false;

        for (int i = numberStart; i < numberEnd; i++)
        {
            char c = normalizedName[i];
            if (c < '0' || c > '9')
                return false;
        }

        return true;
    }

    private static T FindChildComponentByName<T>(Transform root, string targetName) where T : Component
    {
        GameObject found = FindChildGameObjectByName(root, targetName);
        return found == null ? null : found.GetComponent<T>();
    }

    private static GameObject FindChildGameObjectByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return child.gameObject;

            GameObject result = FindChildGameObjectByName(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static GameObject FindChildGameObjectByNamePrefix(Transform root, string targetNamePrefix)
    {
        if (root == null || string.IsNullOrEmpty(targetNamePrefix))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith(targetNamePrefix, System.StringComparison.OrdinalIgnoreCase))
                return child.gameObject;

            GameObject result = FindChildGameObjectByNamePrefix(child, targetNamePrefix);
            if (result != null)
                return result;
        }

        return null;
    }
#endif
}
