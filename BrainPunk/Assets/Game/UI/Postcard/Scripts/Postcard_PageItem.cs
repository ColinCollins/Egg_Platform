using System;
using System.Collections.Generic;
using Bear.Logger;
using Config;
using Config.Game;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Common;
using GameCommon;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Scripts.Common;
using Spine.Unity;



#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEditor;
#endif

/// <summary>
/// version 1.0
/// items 初始化，根据 Index 获取 config 对应的 unlockProgress 配置。若 index 超出 unlockProgress 长度，则默认给 3 作为解锁需求 star 数量。
/// items ClickBtn 绑定功能写道 Page 这里。点击后尝试 unlock。判断 star 数量是否足够。若是不够，弹出 Systemtips。若足够，消耗 star， 并 playUnlockAnim
/// items 初始化时，读取 Postcard.currentCollectedPuzzledId 若存在对应 Index，则 unlock 状态。
/// PlayUnlockAnim 播放完成后， Postcard.currentCollectedPuzzledId 记录 Index，并检测是否所有的 items 都已经解锁完成。播放 PageCompleteAnim 这里暂时留存一个 API，不做具体实现。
/// 然后 Postcard 记录完成 id。
/// _owner 在 Page 播放动画期间不可以关闭。同时 CompleteAnim 播放完成还有 Callback 需要调用。
/// 
/// grayImage 补充逻辑。uncomplete 状态下，grayImage 时 fillAmount = 0。complete 的时候，播放 anim fillAmount 从 0 - 1。
/// Postcard_unlock 不处理 
///
/// version 1.1 
/// 补充 anim 逻辑。非 complete 状态下 isComplete = false. 如果从 isComplete = false 转换到 true trigger 一次 transform。contentTxt 播放一次 DOText 逐字填充。
/// 若已经完成状态下 isComplete 就设置为 true。
/// </summary>
public class Postcard_PageItem : MonoBehaviour, IDebuger
{
    private const int DefaultUnlockStarCount = 3;
    private const float CompleteGrayFillDuration = .7f;
    private const float CompleteGrayScaleMultiplier = 1.1f;
    private const float CompleteGrayScaleDuration = 0.2f;
    private const float CompleteTransformWaitDuration = 1.5f;
    private const string UnlockPagePrefabName = "Postcard_unlock";
    private const string CompleteTransformSourceStateName = "Base Layer.postcard big_idle";
    private static readonly int IsCompleteAnimHash = Animator.StringToHash("isComplete");
    private static readonly int TransformAnimHash = Animator.StringToHash("transform");
    private static readonly int EnterAnimHash = Animator.StringToHash("enter");

    /// <summary>
    /// 用途：预留当前明信片页整页动画控制器。
    /// 调用边界：后续接入 PageCompleteAnim 时由本页面内部调用。
    /// 注意事项：当前实现未主动播放该 Animator，避免引入未验证动画状态。
    /// </summary>
    [SerializeField] private Animator anim;

    /// <summary>
    /// 用途：显式绑定当前明信片页的进度 item 列表。
    /// 调用边界：由 prefab 序列化配置；为空时运行时从子节点自动查找。
    /// 注意事项：列表顺序会按 item.Index 重新排序，调用方不要依赖 Inspector 顺序作为最终顺序。
    /// </summary>
    [SerializeField] private List<Postcard_ProgressItem> items;

    /// <summary>
    /// 用途：预留当前明信片页背景图引用。
    /// 调用边界：后续需要按配置切换背景时由本页面内部使用。
    /// 注意事项：当前实现不修改该 Image，避免影响已制作 prefab 的美术表现。
    /// </summary>
    [SerializeField] private Image bg;

    /// <summary>
    /// 默认未解锁状态 grayImage 全灰。play Complete Anim 的时候从 0 - 1
    /// </summary>
    [SerializeField] PostcardGrayImage grayImage;

    /// <summary>
    /// 完成状态时显示的 object
    /// </summary>
    [SerializeField] GameObject completePart;

    /// <summary>
    /// 用途：完成态明信片正文文本，用于显示当前配置 Diary 多语言正文并在整页首次完成时播放逐字填充表现。
    /// 调用边界：优先由 prefab 或 AutoBind 绑定；未绑定时从 completePart 子级 TMP_Text 兜底查找。
    /// 注意事项：只读取当前 config.Diary 多语言 key 控制本页完成态 UI 文本显示，不写入存档、不参与奖励流程。
    /// </summary>
    [SerializeField] private TMP_Text contentTxt;

    private PostcardConfig config;

    private PostcardPopup _owner;
    private readonly List<Postcard_ProgressItem> _runtimeItems = new List<Postcard_ProgressItem>();
    private Action _onCompleted;
    private Tween _grayImageTween;
    private Tween _completeTextTween;
    private UniTaskCompletionSource _completeEffectsCompletionSource;
    private string _completeContentText;
    private bool? _lastCompletedState;
    private bool _isGuideInteractionLocked;

#if UNITY_EDITOR
    #region Editor

    /// <summary>
    /// 用途：编辑器中按当前 prefab 层级补齐明信片页的本地序列化引用。
    /// 调用边界：仅供 Unity Editor Inspector 手动点击，不参与运行时构建和页面刷新流程。
    /// 注意事项：只读取当前 Transform 下已有组件和对象，不访问 DB/save/config，也不创建实例。
    /// </summary>
    [Button("AutoBind")]
    public void AutoBind()
    {
        Animator bindAnim = GetComponent<Animator>();
        List<Postcard_ProgressItem> bindItems = CollectProgressItems();
        Transform mainPage = FindChildTransformByName(transform, "mainPage");
        Image bindBg = mainPage == null ? null : mainPage.GetComponent<Image>();
        PostcardGrayImage bindGrayImage = mainPage == null ? null : mainPage.GetComponent<PostcardGrayImage>();
        if (bindGrayImage == null)
            bindGrayImage = GetComponentInChildren<PostcardGrayImage>(true);
        GameObject bindCompletePart = FindCompletePart();
        TMP_Text bindContentTxt = FindCompleteContentText(bindCompletePart);

        bool shouldBindAnim = bindAnim != null && anim != bindAnim;
        bool shouldBindItems = !IsSameProgressItems(items, bindItems);
        bool shouldBindBg = bindBg != null && bg != bindBg;
        bool shouldBindCompletePart = bindCompletePart != null && completePart != bindCompletePart;
        bool shouldBindGrayImage = bindGrayImage != null && grayImage != bindGrayImage;
        bool shouldBindContentTxt = bindContentTxt != null && contentTxt != bindContentTxt;

        if (!shouldBindAnim
            && !shouldBindItems
            && !shouldBindBg
            && !shouldBindCompletePart
            && !shouldBindGrayImage
            && !shouldBindContentTxt)
        {
            return;
        }

        Undo.RecordObject(this, "Postcard_PageItem AutoBind");

        if (shouldBindAnim)
            anim = bindAnim;
        if (shouldBindItems)
            items = bindItems;
        if (shouldBindBg)
            bg = bindBg;
        if (shouldBindCompletePart)
            completePart = bindCompletePart;
        if (shouldBindGrayImage)
            grayImage = bindGrayImage;
        if (shouldBindContentTxt)
            contentTxt = bindContentTxt;

        EditorUtility.SetDirty(this);
        if (PrefabUtility.IsPartOfPrefabInstance(this))
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    /// <summary>
    /// 用途：在 Unity Inspector 中手动触发当前页 Complete 表现，便于 PlayMode 下调试动画、灰度和正文逐字填充。
    /// 调用边界：仅供编辑器 Inspector 手动点击；必须处于 PlayMode，不写存档、不发奖、不切页。
    /// 注意事项：会复用当前页绑定的 Animator、grayImage 和 contentTxt；缺少绑定时对应表现会按运行时逻辑跳过。
    /// </summary>
    [Button("Test Complete Effect")]
    private void TestCompleteEffect()
    {
        if (!Application.isPlaying)
        {
            this.LogWarning("[Postcard_PageItem] Test Complete Effect only works in PlayMode.");
            return;
        }

        TestCompleteEffectAsync().Forget();
    }

    private async UniTaskVoid TestCompleteEffectAsync()
    {
        ApplyCompleteVisualState(true, true, null);
        await PlayCompleteTransformAsync();
    }

    private List<Postcard_ProgressItem> CollectProgressItems()
    {
        List<Postcard_ProgressItem> result = new List<Postcard_ProgressItem>();
        Postcard_ProgressItem[] foundItems = GetComponentsInChildren<Postcard_ProgressItem>(true);
        for (int i = 0; i < foundItems.Length; i++)
        {
            if (foundItems[i] != null && !result.Contains(foundItems[i]))
                result.Add(foundItems[i]);
        }

        result.Sort(CompareProgressItem);
        return result;
    }

    private static bool IsSameProgressItems(List<Postcard_ProgressItem> current, List<Postcard_ProgressItem> found)
    {
        int currentCount = current == null ? 0 : current.Count;
        int foundCount = found == null ? 0 : found.Count;
        if (currentCount != foundCount)
            return false;

        for (int i = 0; i < currentCount; i++)
        {
            if (current[i] != found[i])
                return false;
        }

        return true;
    }

    private static Transform FindChildTransformByName(Transform root, string targetName)
    {
        GameObject found = FindChildGameObjectByName(root, targetName);
        return found == null ? null : found.transform;
    }

    #endregion
#endif

    private GameObject FindCompletePart()
    {
        GameObject found = FindChildGameObjectByName(transform, "Postcard_CompleteMode");
        if (found != null)
            return found;

        found = FindChildGameObjectByName(transform, "completePart");
        if (found != null)
            return found;

        found = FindChildGameObjectByName(transform, "CompletePart");
        if (found != null)
            return found;

        found = FindChildGameObjectByName(transform, "complete");
        if (found != null)
            return found;

        found = FindChildGameObjectByName(transform, "Complete");
        if (found != null)
            return found;

        return FindDirectCompletePartFallback();
    }

    private GameObject FindDirectCompletePartFallback()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (IsPageStructuralChild(child))
                continue;
            if (child.GetComponent<Postcard_ProgressItem>() != null
                || child.GetComponentInChildren<Postcard_ProgressItem>(true) != null)
            {
                continue;
            }

            return child.gameObject;
        }

        return null;
    }

    private static bool IsPageStructuralChild(Transform child)
    {
        if (child == null)
            return true;

        string childName = child.name;
        return string.Equals(childName, "Root", StringComparison.OrdinalIgnoreCase)
            || string.Equals(childName, "main", StringComparison.OrdinalIgnoreCase)
            || string.Equals(childName, "mainPage", StringComparison.OrdinalIgnoreCase);
    }

    private static GameObject FindChildGameObjectByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                return child.gameObject;

            GameObject result = FindChildGameObjectByName(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static TMP_Text FindCompleteContentText(GameObject completeRoot)
    {
        return completeRoot == null ? null : completeRoot.GetComponentInChildren<TMP_Text>(true);
    }

    #region Public API

    /// <summary>
    /// 用途：绑定当前明信片页配置、所属弹窗和完成回调，供 PostcardPopup 创建页面后初始化运行时数据。
    /// 调用边界：仅在页面 prefab 被 PostcardPopup 实例化后调用，调用方负责传入当前页配置与弹窗实例。
    /// 注意事项：该方法只缓存配置和回调，不读取或写入存档，实际刷新由 OnShow / Refresh 执行。
    /// </summary>
    /// <param name="postcardConfig">当前明信片页配置。</param>
    /// <param name="owner">创建并持有当前页面的 PostcardPopup。</param>
    /// <param name="onCompleted">当前页全部进度解锁后的回调。</param>
    public void SetData(PostcardConfig postcardConfig, PostcardPopup owner, Action onCompleted)
    {
        config = postcardConfig;
        _owner = owner;
        _onCompleted = onCompleted;
        _lastCompletedState = null;
        _completeContentText = null;
        _isGuideInteractionLocked = false;
    }

    /// <summary>
    /// 用途：由明信片弹窗内引导层锁定或恢复本页原生 item 点击，确保引导期间只能通过引导入口推进。
    /// 调用边界：仅由 `Postcard_GuidePart` 在运行时引导开始、关闭或页面销毁前调用。
    /// 注意事项：该方法不修改存档、不扣星、不刷新 UI，只影响本页普通点击入口是否响应。
    /// </summary>
    /// <param name="isLocked">true 表示忽略本页原生 item 点击；false 表示恢复原生点击。</param>
    public void SetGuideInteractionLocked(bool isLocked)
    {
        _isGuideInteractionLocked = isLocked;
    }

    /// <summary>
    /// 用途：按当前运行时 item 列表下标读取页面真实解锁目标，供引导层校验一一对应关系。
    /// 调用边界：仅用于当前 `Postcard_PageItem` 已完成 `OnShow()` 后的运行时页面实例。
    /// 注意事项：返回的 item 仍由本页控制解锁逻辑，调用方不得直接写入其解锁状态。
    /// </summary>
    /// <param name="listIndex">当前页面运行时 item 列表下标。</param>
    /// <param name="item">找到的真实页面 item。</param>
    /// <returns>下标有效且 item 非空时返回 true。</returns>
    public bool TryGetRuntimeItemByListIndex(int listIndex, out Postcard_ProgressItem item)
    {
        ResolveRuntimeItems();
        item = null;

        if (listIndex < 0 || listIndex >= _runtimeItems.Count)
            return false;

        item = _runtimeItems[listIndex];
        return item != null;
    }

    /// <summary>
    /// 用途：供引导层判断当前下标对应的页面目标是否已经解锁，以便跳过已完成目标。
    /// 调用边界：仅在运行时引导刷新目标时调用。
    /// 注意事项：该方法只读当前 `DB.Postcard` 与页面配置，不写入存档，也不改变 UI。
    /// </summary>
    /// <param name="listIndex">当前页面运行时 item 列表下标。</param>
    /// <returns>页面已完成或对应 item 已收集时返回 true。</returns>
    public bool IsProgressItemUnlockedByListIndex(int listIndex)
    {
        if (!Application.isPlaying)
            return false;

        if (!TryGetRuntimeItemByListIndex(listIndex, out Postcard_ProgressItem item))
            return false;

        var postcardData = DB.Postcard;
        return IsConfigCompleted(postcardData) || IsPuzzleCollected(postcardData, item.Index);
    }

    /// <summary>
    /// 用途：按当前页面真实 item 下标读取 guide 层显示所需的解锁状态、消耗星星数和当前星星数。
    /// 调用边界：仅供 `Postcard_GuidePart` 在引导开始、目标切换或降级刷新时同步遮罩层 item 表现。
    /// 注意事项：该方法只读取当前配置、背包和明信片存档，不扣星、不写存档、不播放动画。
    /// </summary>
    /// <param name="listIndex">当前页面运行时 item 列表下标。</param>
    /// <param name="isUnlock">真实页面目标是否已经解锁。</param>
    /// <param name="unlockCount">该目标解锁所需星星数。</param>
    /// <param name="currentStar">当前玩家可用星星数。</param>
    /// <returns>下标有效且目标 item 存在时返回 true。</returns>
    public bool TryGetGuideProgressViewStateByListIndex(
        int listIndex,
        out bool isUnlock,
        out int unlockCount,
        out int currentStar)
    {
        isUnlock = false;
        unlockCount = DefaultUnlockStarCount;
        currentStar = 0;

        if (!Application.isPlaying)
            return false;

        if (!TryGetRuntimeItemByListIndex(listIndex, out Postcard_ProgressItem item))
            return false;

        var postcardData = DB.Postcard;
        isUnlock = IsConfigCompleted(postcardData) || IsPuzzleCollected(postcardData, item.Index);
        unlockCount = GetUnlockCount(item.Index);
        currentStar = GetCurrentStarCount();
        return true;
    }

    /// <summary>
    /// 用途：由明信片引导层按列表下标触发真实页面 item 的原有解锁流程。
    /// 调用边界：仅允许 `Postcard_GuidePart` 在当前引导目标被点击时调用。
    /// 注意事项：该入口复用本页原 `TryUnlockItem`，不复制扣星、存档、回滚、动画或整页完成逻辑；`onStable` 只表示本次尝试被接受后的本地表现已结束。
    /// </summary>
    /// <param name="listIndex">当前页面运行时 item 列表下标。</param>
    /// <param name="onStable">被接受的解锁表现结束后的回调；参数为 true 表示本次解锁被接受。</param>
    /// <returns>本次点击进入真实解锁流程时返回 true；校验失败、星星不足或保存失败时返回 false。</returns>
    public bool TryUnlockGuidedItemByListIndex(int listIndex, Action<bool> onStable)
    {
        if (!Application.isPlaying)
            return false;

        if (!TryGetRuntimeItemByListIndex(listIndex, out Postcard_ProgressItem item))
            return false;

        return TryUnlockItem(item, onStable);
    }

    /// <summary>
    /// 用途：在明信片整页完成粒子发射后、Chest 开启前播放当前页完成转换动画。
    /// 调用边界：仅由 `PostcardPopup` 在刷新完成视觉后调用；方法内部等待 grayImage 完成及粒子实际触发。
    /// 注意事项：粒子触发后才设置 Animator 的 `transform`，等待转换表现窗口结束后再播放完成文案逐字动画；不切页、不发奖、不写存档。
    /// </summary>
    /// <returns>动画等待完成或无需播放时返回 true；对象销毁导致等待取消时返回 false。</returns>
    public async UniTask<bool> PlayCompleteTransformAsync()
    {
        if (!Application.isPlaying || IsUnlockPage())
            return true;

        if (!await WaitCompleteEffectsAsync())
            return false;

        if (this == null || !gameObject.activeInHierarchy)
            return false;

        ApplyCompletePartState(true);
        if (anim == null)
            return true;

        bool hasCompleteParameter = HasAnimatorParameter(anim, IsCompleteAnimHash, AnimatorControllerParameterType.Bool);
        if (hasCompleteParameter)
            anim.SetBool(IsCompleteAnimHash, false);

        PrepareCompleteTransformSourceState();

        if (!HasAnimatorParameter(anim, TransformAnimHash, AnimatorControllerParameterType.Trigger))
        {
            if (hasCompleteParameter)
                anim.SetBool(IsCompleteAnimHash, true);
            return true;
        }

        anim.SetTrigger(TransformAnimHash);
        bool isCanceled = await UniTask.WaitForSeconds(
                CompleteTransformWaitDuration,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (isCanceled)
            return false;

        PlayCompleteContentTextAnim();
        isCanceled = await UniTask.WaitForSeconds(
                CompleteGrayFillDuration,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (isCanceled)
            return false;

        if (hasCompleteParameter)
            anim.SetBool(IsCompleteAnimHash, true);
        return true;
    }

    #endregion

    /// <summary>
    /// 初始化几个 item 的状态
    /// </summary>
    public void OnShow()
    {
        ResolveRuntimeItems();
        BindItemButtons();
        Refresh();
        TriggerEnterAnimation();
    }

    /// <summary>
    /// 用途：按当前存档、配置和玩家星星数量刷新本页所有进度 item 的解锁状态。
    /// 调用边界：页面显示、解锁失败降级、非完成解锁动画结束后调用。
    /// 注意事项：该方法只刷新 UI，不消耗星星、不写入明信片存档。
    /// </summary>
    public void Refresh()
    {
        Refresh(null);
    }

    private void Refresh(Action onCompleteAnim)
    {
        ResolveRuntimeItems();

        if (config == null)
            return;

        var postcardData = DB.Postcard;
        int currentStar = GetCurrentStarCount();
        bool isCompleted = IsConfigCompleted(postcardData);
        bool shouldPlayCompleteGrayAnim = _lastCompletedState.HasValue && !_lastCompletedState.Value && isCompleted;
        _lastCompletedState = isCompleted;

        for (int i = 0; i < _runtimeItems.Count; i++)
        {
            Postcard_ProgressItem item = _runtimeItems[i];
            if (item == null)
                continue;

            bool isUnlock = isCompleted || IsPuzzleCollected(postcardData, item.Index);
            item.Refresh(isUnlock, GetUnlockCount(item.Index), currentStar);
        }

        ApplyCompleteVisualState(isCompleted, shouldPlayCompleteGrayAnim, onCompleteAnim);
    }

    /// <summary>
    /// 用途：当前明信片页退出时解除 item 点击订阅并释放完成回调引用。
    /// 调用边界：由 PostcardPopup 切页、显示锁定页或关闭弹窗时调用。
    /// 注意事项：该方法不清空存档和背包数据，也不主动停止子 item 的本地动画。
    /// </summary>
    public void OnExit()
    {
        _isGuideInteractionLocked = false;
        UnbindItemButtons();
        StopCompleteVisualTweens();
        _onCompleted = null;
    }

    private void OnDisable()
    {
        StopCompleteVisualTweens();
    }

    private void OnDestroy()
    {
        StopCompleteVisualTweens();
    }

    #region Button Handling

    private void BindItemButtons()
    {
        for (int i = 0; i < _runtimeItems.Count; i++)
        {
            CustomButton clickBtn = _runtimeItems[i] == null ? null : _runtimeItems[i].ClickBtn;
            if (clickBtn == null)
                continue;

            clickBtn.OnClick -= OnProgressItemClick;
            clickBtn.OnClick += OnProgressItemClick;
        }
    }

    private void UnbindItemButtons()
    {
        for (int i = 0; i < _runtimeItems.Count; i++)
        {
            CustomButton clickBtn = _runtimeItems[i] == null ? null : _runtimeItems[i].ClickBtn;
            if (clickBtn == null)
                continue;

            clickBtn.OnClick -= OnProgressItemClick;
        }
    }

    private void OnProgressItemClick(CustomButton btn)
    {
        if (_isGuideInteractionLocked)
            return;

        Postcard_ProgressItem item = FindItemByButton(btn);
        if (item == null)
            return;

        TryUnlockItem(item);
    }

    private Postcard_ProgressItem FindItemByButton(CustomButton btn)
    {
        if (btn == null)
            return null;

        for (int i = 0; i < _runtimeItems.Count; i++)
        {
            Postcard_ProgressItem item = _runtimeItems[i];
            if (item != null && item.ClickBtn == btn)
                return item;
        }

        return null;
    }

    #endregion

    #region Unlock Flow

    private void TryUnlockItem(Postcard_ProgressItem item)
    {
        TryUnlockItem(item, null);
    }

    private bool TryUnlockItem(Postcard_ProgressItem item, Action<bool> onStable)
    {
        if (config == null || item == null)
            return false;

        if (_owner != null && _owner.IsPagePlayingAnim)
            return false;

        var postcardData = DB.Postcard;
        if (postcardData == null)
        {
            this.LogWarning("[Postcard_PageItem] Unlock failed because DB.Postcard is null.");
            // SystemTips.Show("明信片数据异常");
            return false;
        }

        if (IsConfigCompleted(postcardData) || IsPuzzleCollected(postcardData, item.Index))
            return false;

        int unlockCount = GetUnlockCount(item.Index);
        int currentStar = GetCurrentStarCount();
        if (currentStar < unlockCount)
        {
            SystemTips.Show(LocalizationManager.GetTranslation("U_Stars_Tips_03"));
            Refresh();
            return false;
        }

        List<int> originalCollectedPuzzleIds = postcardData.CurrentCollectedPuzzledId;
        bool hadOriginalCollectedPuzzleIds = originalCollectedPuzzleIds != null;
        List<int> collectedPuzzleIds = EnsureCurrentCollectedPuzzles(postcardData);
        collectedPuzzleIds.Add(item.Index);

        bool completedNow = IsAllProgressCollected(collectedPuzzleIds);
        bool addedCompletedId = false;
        if (completedNow)
        {
            postcardData.CompletedId ??= new List<string>();
            if (!postcardData.CompletedId.Contains(config.Id))
            {
                postcardData.CompletedId.Add(config.Id);
                addedCompletedId = true;
            }

            postcardData.CurrentCollectedPuzzledId = new List<int>();
        }

        if (!DB.SavePostcard())
        {
            RollbackUnlock(
                postcardData,
                collectedPuzzleIds,
                item.Index,
                addedCompletedId,
                originalCollectedPuzzleIds,
                hadOriginalCollectedPuzzleIds);
            this.LogWarning($"[Postcard_PageItem] SavePostcard failed. postcardId:{config.Id}, puzzleId:{item.Index}");
            // SystemTips.Show("保存失败");
            Refresh();
            return false;
        }

        GameManager.Instance?.Bag?.RemoveTool(GameProps.Star, unlockCount, CostType.Other);
        if (completedNow)
            _owner?.TryGrantPageCompleteNormalRewards(config);

        Vector3 unlockEffectPosition = item.transform is RectTransform itemRect
            ? itemRect.TransformPoint(itemRect.rect.center)
            : item.transform.position;
        if (_owner != null)
            _owner.PlayUnlockEffectsAsync(unlockEffectPosition).Forget();
        _owner?.OnPagePlayAnim(true);

        item.PlayUnlockAnim(() =>
        {
            if (completedNow)
            {
                if (_onCompleted != null)
                    _owner?.PreparePageCompleteChest();

                onStable?.Invoke(true);
                if (_onCompleted != null)
                {
                    _onCompleted.Invoke();
                }
                else
                {
                    Refresh();
                    _owner?.OnPagePlayAnim(false);
                }
            }
            else
            {
                _owner?.OnPagePlayAnim(false);
                Refresh();
                onStable?.Invoke(true);
            }
        }, !completedNow);
        return true;
    }

    private void RollbackUnlock(
        Bear.SaveModule.Postcard postcardData,
        List<int> collectedPuzzleIds,
        int puzzleId,
        bool addedCompletedId,
        List<int> originalCollectedPuzzleIds,
        bool hadOriginalCollectedPuzzleIds)
    {
        collectedPuzzleIds?.Remove(puzzleId);

        postcardData.CurrentCollectedPuzzledId = hadOriginalCollectedPuzzleIds
            ? originalCollectedPuzzleIds
            : null;

        if (addedCompletedId && postcardData.CompletedId != null)
            postcardData.CompletedId.Remove(config.Id);
    }

    #endregion

    #region Page Visuals

    private void ApplyCompleteVisualState(bool isCompleted, bool playCompleteAnim, Action onCompleteAnim)
    {
        bool shouldShowCompletePart = isCompleted
            && !playCompleteAnim
            && _completeEffectsCompletionSource == null;
        ApplyCompletePartState(shouldShowCompletePart);
        ApplyAnimatorCompleteState(isCompleted, playCompleteAnim);
        ApplyCompleteContentTextState(isCompleted, playCompleteAnim);
        ApplyGrayImageState(isCompleted, playCompleteAnim);
        onCompleteAnim?.Invoke();
    }

    private void ApplyCompletePartState(bool isVisible)
    {
        if (IsUnlockPage())
            return;

        if (completePart == null)
            completePart = FindCompletePart();

        if (completePart != null && completePart.activeSelf != isVisible)
            completePart.SetActive(isVisible);
    }

    private void ApplyAnimatorCompleteState(bool isCompleted, bool playCompleteAnim)
    {
        if (IsUnlockPage() || anim == null)
            return;

        if (HasAnimatorParameter(anim, IsCompleteAnimHash, AnimatorControllerParameterType.Bool))
            anim.SetBool(IsCompleteAnimHash, playCompleteAnim ? false : isCompleted);
    }

    private void TriggerEnterAnimation()
    {
        if (IsUnlockPage() || anim == null)
            return;

        if (HasAnimatorParameter(anim, EnterAnimHash, AnimatorControllerParameterType.Trigger))
            anim.SetTrigger(EnterAnimHash);
    }

    private void PrepareCompleteTransformSourceState()
    {
        if (anim == null || anim.layerCount <= 0)
            return;

        if (anim.GetCurrentAnimatorStateInfo(0).IsName(CompleteTransformSourceStateName))
            return;

        anim.Play(CompleteTransformSourceStateName, 0, 0f);
        anim.Update(0f);
    }

    private void ApplyCompleteContentTextState(bool isCompleted, bool playCompleteAnim)
    {
        TMP_Text target = ResolveCompleteContentText();
        if (IsUnlockPage() || target == null)
            return;

        string completeText = GetCompleteContentText(target);
        StopCompleteTextTween();

        target.text = isCompleted && !playCompleteAnim ? completeText : string.Empty;
    }

    private void PlayCompleteContentTextAnim()
    {
        TMP_Text target = ResolveCompleteContentText();
        if (IsUnlockPage() || target == null)
            return;

        string completeText = GetCompleteContentText(target);
        StopCompleteTextTween();
        if (!gameObject.activeInHierarchy || string.IsNullOrEmpty(completeText))
        {
            target.text = completeText;
            return;
        }

        target.text = string.Empty;
        _completeTextTween = target.DOText(completeText, CompleteGrayFillDuration)
            .SetTarget(target)
            .OnComplete(() =>
            {
                _completeTextTween = null;
            });
    }

    private void ApplyGrayImageState(bool isCompleted, bool playCompleteAnim)
    {
        if (IsUnlockPage() || grayImage == null)
            return;

        StopGrayImageTween();

        if (!isCompleted)
        {
            grayImage.SetFillAmount(0f);
            return;
        }

        if (!playCompleteAnim || !gameObject.activeInHierarchy || !grayImage.isActiveAndEnabled)
        {
            grayImage.RestoreOriginalMaterial();
            grayImage.SetFillAmount(1f);
            grayImage.SetGray(false);

            return;
        }

        grayImage.SetFillAmount(0f);
        Transform grayImageTransform = grayImage.transform;
        Vector3 originalScale = grayImageTransform.localScale;

        AudioManager.PlaySound("postcardGetReward");

        Sequence grayImageSequence = DOTween.Sequence();
        UniTaskCompletionSource completeEffectsSource = new UniTaskCompletionSource();
        _completeEffectsCompletionSource = completeEffectsSource;
        bool didGrayImageSequenceComplete = false;
        grayImageSequence.SetTarget(grayImage);
        grayImageSequence.Append(DOTween.To(
                () => grayImage.FillAmount,
                value => grayImage.SetFillAmount(value),
                1f,
                CompleteGrayFillDuration)
            .SetEase(Ease.OutCirc));
        grayImageSequence.AppendCallback(() =>
        {
            grayImage.RestoreOriginalMaterial();
            grayImage.SetFillAmount(1f);
            grayImage.SetGray(false);

        });
        grayImageSequence.Append(grayImageTransform
            .DOScale(originalScale * CompleteGrayScaleMultiplier, CompleteGrayScaleDuration)
            .SetLoops(2, LoopType.Yoyo)
            .SetEase(Ease.OutCirc));
        grayImageSequence.OnComplete(() =>
        {
            didGrayImageSequenceComplete = true;
            PlayCompleteEffectsAndSignalAsync(completeEffectsSource).Forget();

            grayImage.RestoreOriginalMaterial();
            grayImage.SetFillAmount(1f);
            grayImage.SetGray(false);
        });
        grayImageSequence.OnKill(() =>
        {
            if (!didGrayImageSequenceComplete)
                ResolveCompleteEffectsWait(completeEffectsSource);

            if (grayImageTransform != null)
                grayImageTransform.localScale = originalScale;

            if (_grayImageTween == grayImageSequence)
            {
                _grayImageTween = null;
            }
        });
        _grayImageTween = grayImageSequence;
    }

    private async UniTask<bool> WaitCompleteEffectsAsync()
    {
        UniTaskCompletionSource completeEffectsSource = _completeEffectsCompletionSource;
        if (completeEffectsSource == null)
            return true;

        bool isCanceled = await completeEffectsSource.Task
            .AttachExternalCancellation(this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        return !isCanceled;
    }

    private async UniTask PlayCompleteEffectsAndSignalAsync(UniTaskCompletionSource completeEffectsSource)
    {
        try
        {
            if (_owner != null)
                await _owner.PlayCompleteEffects();
        }
        finally
        {
            ResolveCompleteEffectsWait(completeEffectsSource);
        }
    }

    private void ResolveCompleteEffectsWait(UniTaskCompletionSource completeEffectsSource)
    {
        completeEffectsSource?.TrySetResult();
        if (_completeEffectsCompletionSource == completeEffectsSource)
            _completeEffectsCompletionSource = null;
    }

    private TMP_Text ResolveCompleteContentText()
    {
        if (contentTxt != null)
            return contentTxt;

        if (completePart == null)
            completePart = FindCompletePart();

        contentTxt = FindCompleteContentText(completePart);
        return contentTxt;
    }

    private string GetCompleteContentText(TMP_Text target)
    {
        if (_completeContentText == null)
            _completeContentText = ResolveLocalizedCompleteContentText(target);

        return _completeContentText;
    }

    private string ResolveLocalizedCompleteContentText(TMP_Text target)
    {
        string diaryKey = config?.Diary;
        if (!string.IsNullOrEmpty(diaryKey))
        {
            string translated = LocalizationManager.GetTranslation(diaryKey);
            return string.IsNullOrEmpty(translated) ? diaryKey : translated;
        }

        return target == null ? string.Empty : target.text ?? string.Empty;
    }

    private static bool HasAnimatorParameter(Animator targetAnimator, int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (targetAnimator == null)
            return false;

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
                return true;
        }

        return false;
    }

    private void StopCompleteVisualTweens()
    {
        StopGrayImageTween();
        StopCompleteTextTween();
    }

    private void StopGrayImageTween()
    {
        _grayImageTween?.Kill();
        _grayImageTween = null;
    }

    private void StopCompleteTextTween()
    {
        _completeTextTween?.Kill();
        _completeTextTween = null;
    }

    private bool IsUnlockPage()
    {
        return string.Equals(config?.PrefabPath, UnlockPagePrefabName, StringComparison.OrdinalIgnoreCase)
            || gameObject.name.StartsWith(UnlockPagePrefabName, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Data Helpers

    private void ResolveRuntimeItems()
    {
        _runtimeItems.Clear();

        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && !_runtimeItems.Contains(items[i]))
                    _runtimeItems.Add(items[i]);
            }
        }

        if (_runtimeItems.Count <= 0)
        {
            Postcard_ProgressItem[] foundItems = GetComponentsInChildren<Postcard_ProgressItem>(true);
            for (int i = 0; i < foundItems.Length; i++)
            {
                if (foundItems[i] != null && !_runtimeItems.Contains(foundItems[i]))
                    _runtimeItems.Add(foundItems[i]);
            }
        }

        _runtimeItems.Sort(CompareProgressItem);
    }

    private int GetUnlockCount(int puzzleId)
    {
        if (config != null
            && config.UnlockProgress != null
            && config.UnlockProgress.TryGetValue(puzzleId, out int unlockCount))
        {
            return Mathf.Max(0, unlockCount);
        }

        return DefaultUnlockStarCount;
    }

    private int GetCurrentStarCount()
    {
        if (!Singleton<PlayCtrl>.IsInitialized)
            return 0;

        return Mathf.Max(0, GameManager.Instance?.Bag?.GetToolCount(GameProps.Star) ?? 0);
    }

    private bool IsConfigCompleted(Bear.SaveModule.Postcard postcardData)
    {
        return postcardData != null
            && postcardData.CompletedId != null
            && !string.IsNullOrEmpty(config?.Id)
            && postcardData.CompletedId.Contains(config.Id);
    }

    private bool IsPuzzleCollected(Bear.SaveModule.Postcard postcardData, int puzzleId)
    {
        return postcardData != null
            && postcardData.CurrentCollectedPuzzledId != null
            && postcardData.CurrentCollectedPuzzledId.Contains(puzzleId);
    }

    private List<int> EnsureCurrentCollectedPuzzles(Bear.SaveModule.Postcard postcardData)
    {
        postcardData.CurrentCollectedPuzzledId ??= new List<int>();
        return postcardData.CurrentCollectedPuzzledId;
    }

    private bool IsAllProgressCollected(List<int> collectedPuzzleIds)
    {
        if (_runtimeItems.Count <= 0)
            return false;

        for (int i = 0; i < _runtimeItems.Count; i++)
        {
            Postcard_ProgressItem item = _runtimeItems[i];
            if (item == null)
                continue;

            if (collectedPuzzleIds == null || !collectedPuzzleIds.Contains(item.Index))
                return false;
        }

        return true;
    }

    private static int CompareProgressItem(Postcard_ProgressItem left, Postcard_ProgressItem right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        return left.Index.CompareTo(right.Index);
    }

    #endregion
}
