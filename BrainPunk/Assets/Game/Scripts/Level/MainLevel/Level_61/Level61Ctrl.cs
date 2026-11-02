using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ItemEvent;
using Game.Scripts.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 进入关卡的时候，检测 HasEnterLevel61 ，HasGetLevel61Key。
/// HasEnterLevel61 = false 的状态下，切换为 true 。不执行任何操作
/// HasGetLevel61Key = false 的状态下，不处理逻辑
/// HasEnterLevel61 = true 状态下，key active = true 触发 trigger Executor，解锁 door。并将 HasEnterLevel61 以及 HasGetLevel61Key 状态重置。
///
/// Actor sit  之后，延迟 2 s groupAlpha 逐渐变为 1.其他时候默认为 0，离开椅子后也变为 0。要求渐变。
/// 然后 foodRender 每隔 5s 进行一次渐隐渐显，根据 foodSprite 内容进行切换。
///
/// version2.0 补充
/// actor sit 之后，foodRender 切换变为 click 触发，我添加了 ClickItemEventHandle 在 actor 上，你只需要提供 API 就行，点击 actor 切换 foodSprite
/// actor 离开 sit 后 menu 飞回到原位置，并且显示。
/// turning_the_pages 点击一次播放一次
/// thinkBubble 每一次切换都需要缩放一次，从 1 -> 0 -> 1 .5f
///
/// </summary>
public class Level61Ctrl : MonoBehaviour
{
    [SerializeField] private Level61ActorCtrl actorCtrl;
    [SerializeField] private Transform sitableChair;
    [SerializeField] private Vector3 sitOffset = new Vector3(0.3f, 0.45f, 0f);

    [Header("Key Door Flow")]
    [SerializeField] private GameObject keyObject;
    [SerializeField] private BaseItemExecutor keyExecutor;

    [Header("Thinking Bubble")]
    [SerializeField] private SpriteRendererGroupAlpha groupAlpha;
    [SerializeField, Min(0f)] private float bubbleRevealDelay = 2f;
    [SerializeField, Min(0f)] private float bubbleFadeInDuration = 0.5f;
    [SerializeField, Min(0f)] private float bubbleFadeOutDuration = 0.2f;

    [Header("Food Cycle")]
    [SerializeField] private SpriteRenderer foodRender;
    [SerializeField, Min(0f)] private float foodSwitchInterval = 5f;
    [SerializeField, Min(0f)] private float foodFadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float clickSwitchDuration = 0.5f;

    [SerializeField] private Sprite[] foodSprites;

    [SerializeField] private Transform menuBook;
    [SerializeField, Min(0f)] private float menuBookReturnDuration = 0.3f;
    [SerializeField, Min(0f)] private float menuBookReturnJumpPower = 1f;

    [SerializeField] private Vector3 menuBookOffset = new Vector3(-0.1f, 0.2f, 0);

    [SerializeField] private DirectMove keyDirect;

    /// <summary>
    /// 食物切换达到指定次数后显示的明信片星星节点；调用边界：仅由 Level61 坐下后的有效食物点击触发显示。
    /// 注意事项：玩家触碰后的存档、发奖和事件派发仍由该节点上的 Postcard_Star 组件处理。
    /// </summary>
    [SerializeField] private GameObject star;
    /// <summary>
    /// 显示星星所需的有效食物切换次数；小于 1 时运行时按 1 次处理。
    /// </summary>
    [SerializeField, Min(1)] private int foodMaxSwitchTriggerCount = 5;

    private int switchFoodCount;
    private bool hasTriggeredFoodSwitchStar;

    private bool isSit = false;
    private bool isSitTransitioning = false;
    private bool isBubbleReadyForClick = false;
    private bool isFoodSwitching = false;
    private float bubbleAlpha = 0f;
    private int foodSpriteIndex = -1;
    private int bubbleFadeVersion = 0;
    private readonly List<Sprite> validFoodSprites = new();
    private CancellationTokenSource sitCts;
    private Tween bubbleTween;
    private Tween bubbleScaleTween;
    private Tween foodTween;
    private Tween menuBookTween;
    private bool hasStarted = false;
    private GamePanel_UseBtnCommonFeature commonFeature;
    private Vector3 bubbleOriginalScale = Vector3.one;
    private bool hasBubbleOriginalScale = false;
    private Vector3 menuBookOriginalPosition;
    private bool menuBookOriginalActive = true;
    private bool hasMenuBookOriginalState = false;

    void Start()
    {
        InitPresentation();
        EvaluateLevel61State();
        hasStarted = true;
        RegisterUseButton();
    }

    void OnEnable()
    {
        if (hasStarted)
        {
            RegisterUseButton();
        }
    }

    void Update()
    {
        CheckActorFoodClickFallback();
    }

    private void OnUseBtnClick(CustomButton btn)
    {
        ToggleSit().Forget();
    }

    private async UniTask ToggleSit()
    {
        if (isSitTransitioning)
        {
            return;
        }

        isSitTransitioning = true;

        if (!isSit)
        {
            EnterSit();
        }
        else
        {
            LeaveSit();
        }

        try
        {
            await UniTask.WaitForSeconds(0.8f, ignoreTimeScale: true, cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isSitTransitioning = false;
        }
    }

    private void InitPresentation()
    {
        CacheBubbleOriginalScale();
        CacheMenuBookOriginalState();
        BuildValidFoodSprites();
        InitFoodSprite();
        SetFoodAlpha(1f);
        SetBubbleAlpha(0f);
        InitFoodSwitchStar();
    }

    private async UniTaskVoid EvaluateLevel61State()
    {
        var gameData = DB.GameData;
        if (gameData == null)
        {
            Debug.LogWarning("[Level61Ctrl] GameData is null. Skip Level61 state evaluation.");
            SetKeyActive(false);
            return;
        }

        if (!gameData.HasEnterLevel61)
        {
            gameData.HasEnterLevel61 = true;
            gameData.Save();
            SetKeyActive(false);
            // return;
        }

        if (!gameData.HasGetLevel61Key)
        {
            SetKeyActive(false);
            return;
        }

        if (!await TryTriggerKeyDoorFlow())
        {
            return;
        }

        gameData.HasEnterLevel61 = false;
        gameData.HasGetLevel61Key = false;
        gameData.Save();
    }

    private async UniTask<bool> TryTriggerKeyDoorFlow()
    {
        SetKeyActive(false);
        if (keyObject == null)
        {
            Debug.LogWarning("[Level61Ctrl] Key door flow skipped: keyObject is null.");
            return false;
        }

        BaseItemExecutor executor = keyExecutor != null
            ? keyExecutor
            : keyObject.GetComponent<BaseItemExecutor>();

        if (executor == null)
        {
            Debug.LogWarning("[Level61Ctrl] Key door flow skipped: keyExecutor is null.");
            return false;
        }

        if (keyObject.GetComponentsInChildren<BaseItemEventHandle>(true).Length <= 0)
        {
            Debug.LogWarning("[Level61Ctrl] Key door flow skipped: no event handles found on keyObject.");
            return false;
        }

        await UniTask.WaitForSeconds(2f);

        SetKeyActive(true);

        /*         if (!executor.isActiveAndEnabled)
                {
                    Debug.LogWarning("[Level61Ctrl] Key door flow skipped: keyExecutor is not active and enabled.");
                    keyObject.SetActive(false);
                    return false;
                }

                executor.Execute(); */
        return true;
    }

    private void SetKeyActive(bool active)
    {
        if (keyObject != null)
        {
            keyObject.SetActive(active);
        }
    }

    private void RegisterUseButton()
    {
        UnregisterUseButton();

        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
        {
            Debug.LogWarning("[Level61Ctrl] Use button binding skipped: CurrentGamePlayPanel is null.");
            return;
        }

        commonFeature = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<GamePanel_UseBtnCommonFeature>();
        if (commonFeature == null || commonFeature.Use_btn == null)
        {
            Debug.LogWarning("[Level61Ctrl] Use button binding skipped: GamePanel_UseBtnCommonFeature is missing.");
            return;
        }

        commonFeature.Use_btn.OnClick += OnUseBtnClick;
    }

    private void UnregisterUseButton()
    {
        if (commonFeature != null && commonFeature.Use_btn != null)
        {
            commonFeature.Use_btn.OnClick -= OnUseBtnClick;
        }

        commonFeature = null;
    }

    private void TryPlayMenuBookJump()
    {
        if (menuBook == null)
        {
            Debug.LogWarning("[Level61Ctrl] menuBook jump skipped: menuBook is null.");
            return;
        }

        CacheMenuBookOriginalState();
        menuBookTween?.Kill();
        menuBook.gameObject.SetActive(true);
        menuBookTween = menuBook
            .DOJump(sitableChair.transform.position + menuBookOffset, 1f, 1, 0.3f)
            .SetDelay(1.3f)
            .SetLink(menuBook.gameObject)
            .OnComplete(() =>
            {
                if (isSit)
                {
                    menuBook.gameObject.SetActive(false);
                }
            });
    }

    private void EnterSit()
    {
        if (actorCtrl == null || sitableChair == null)
        {
            Debug.LogWarning("[Level61Ctrl] EnterSit skipped: actorCtrl or sitableChair is null.");
            return;
        }

        isSit = true;
        ResetFoodSwitchStarCounter();
        actorCtrl.PlaySit(sitableChair.transform, sitOffset);

        TryPlayMenuBookJump();

        StartBubbleRevealDelay();
    }

    private void LeaveSit()
    {
        isSit = false;

        CancelSitCts();
        StopFoodCycle();
        StopFoodSwitch(true);
        ReturnMenuBookToOriginal();

        if (actorCtrl != null)
        {
            actorCtrl.StopSit();
        }

        FadeBubbleAsync(0f, bubbleFadeOutDuration, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void StartBubbleRevealDelay()
    {
        CancelSitCts();
        StopFoodCycle();
        StopFoodSwitch(true);
        isBubbleReadyForClick = false;
        SetFoodAlpha(1f);
        SetBubbleAlpha(0f);
        ResetBubbleScale(false);

        sitCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        RevealBubbleAfterDelayAsync(sitCts.Token).Forget();
    }

    private async UniTask RevealBubbleAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.WaitForSeconds(bubbleRevealDelay, cancellationToken: cancellationToken);

            if (!isSit || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await RevealBubbleVisualAsync(cancellationToken);

            if (isSit && !cancellationToken.IsCancellationRequested)
            {
                isBubbleReadyForClick = true;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTask RevealBubbleVisualAsync(CancellationToken cancellationToken)
    {
        UniTask fadeTask = FadeBubbleAsync(1f, bubbleFadeInDuration, cancellationToken);
        UniTask popTask = PopBubbleScaleAsync(bubbleFadeInDuration, cancellationToken);

        await UniTask.WhenAll(fadeTask, popTask);

        if (!cancellationToken.IsCancellationRequested)
        {
            SetBubbleAlpha(1f);
            ResetBubbleScale();
        }
    }

    private async UniTask PopBubbleScaleAsync(float duration, CancellationToken cancellationToken)
    {
        Transform bubbleTransform = groupAlpha != null ? groupAlpha.transform : null;
        if (bubbleTransform == null)
        {
            return;
        }

        CacheBubbleOriginalScale();
        bubbleScaleTween?.Kill();
        duration = Mathf.Max(0f, duration);

        if (duration <= 0f)
        {
            ResetBubbleScale();
            return;
        }

        bubbleTransform.localScale = Vector3.zero;
        bubbleScaleTween = bubbleTransform
            .DOScale(bubbleOriginalScale, duration)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject);

        try
        {
            await UniTask.WaitForSeconds(duration, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            ResetBubbleScale();
        }
    }

    public void OnActorFoodClick(Transform clicker, Vector2 worldPoint)
    {
        if (!CanStartActorFoodSwitch())
        {
            return;
        }

        SwitchFoodByActorClickAsync(sitCts.Token).Forget();
        RecordFoodSwitchTrigger();

        AudioManager.PlaySound("button");
    }

    private void CheckActorFoodClickFallback()
    {
        if (!CanReceiveActorFoodClickInput())
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryStartActorFoodSwitchAtScreenPosition(cam, Input.mousePosition);
            return;
        }

        if (Input.touchCount <= 0)
        {
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
            {
                continue;
            }

            if (TryStartActorFoodSwitchAtScreenPosition(cam, touch.position))
            {
                return;
            }
        }
    }

    private bool CanReceiveActorFoodClickInput()
    {
        return isSit
            && isBubbleReadyForClick
            && !isFoodSwitching
            && sitCts != null
            && !sitCts.IsCancellationRequested
            && actorCtrl != null;
    }

    private bool TryStartActorFoodSwitchAtScreenPosition(Camera cam, Vector3 screenPos)
    {
        Vector2 worldPoint = cam.ScreenToWorldPoint(screenPos);
        RaycastHit2D[] hits = Physics2D.RaycastAll(worldPoint, Vector2.zero);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || !hitCollider.transform.IsChildOf(actorCtrl.transform))
            {
                continue;
            }

            OnActorFoodClick(hitCollider.transform, worldPoint);
            return true;
        }

        return false;
    }

    private bool CanStartActorFoodSwitch()
    {
        if (!isSit || !isBubbleReadyForClick || isFoodSwitching || sitCts == null || sitCts.IsCancellationRequested)
        {
            return false;
        }

        BuildValidFoodSprites();

        if (foodRender == null || validFoodSprites.Count < 2)
        {
            return false;
        }

        InitFoodSprite();
        return foodSpriteIndex >= 0;
    }

    private async UniTaskVoid SwitchFoodByActorClickAsync(CancellationToken cancellationToken)
    {
        isFoodSwitching = true;

        try
        {
            int nextIndex = (foodSpriteIndex + 1) % validFoodSprites.Count;

            if (actorCtrl != null)
            {
                actorCtrl.PlayTurningThePageOnce();
            }

            await ScaleBubbleAndSwitchFoodAsync(nextIndex, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isFoodSwitching = false;
        }
    }

    private async UniTask ScaleBubbleAndSwitchFoodAsync(int nextIndex, CancellationToken cancellationToken)
    {
        if (foodRender == null || nextIndex < 0 || nextIndex >= validFoodSprites.Count)
        {
            return;
        }

        Transform bubbleTransform = groupAlpha != null ? groupAlpha.transform : null;
        float duration = Mathf.Max(0f, clickSwitchDuration);

        foodTween?.Kill();
        bubbleScaleTween?.Kill();

        if (bubbleTransform == null || duration <= 0f)
        {
            SetFoodSprite(nextIndex);
            SetFoodAlpha(1f);
            return;
        }

        CacheBubbleOriginalScale();
        Vector3 originalScale = bubbleOriginalScale;
        float halfDuration = duration * 0.5f;

        if (halfDuration > 0f)
        {
            bubbleScaleTween = bubbleTransform
                .DOScale(Vector3.zero, halfDuration)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject);

            await UniTask.WaitForSeconds(halfDuration, cancellationToken: cancellationToken);
        }

        if (!isSit || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        SetFoodSprite(nextIndex);
        SetFoodAlpha(0f);

        if (halfDuration > 0f)
        {
            bubbleScaleTween = bubbleTransform
                .DOScale(originalScale, halfDuration)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }
        else
        {
            bubbleTransform.localScale = originalScale;
        }

        float fadeDuration = Mathf.Max(0f, foodFadeDuration);
        if (fadeDuration > 0f)
        {
            foodTween = foodRender
                .DOFade(1f, fadeDuration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject);
        }
        else
        {
            SetFoodAlpha(1f);
        }

        await UniTask.WaitForSeconds(Mathf.Max(halfDuration, fadeDuration), cancellationToken: cancellationToken);

        if (!cancellationToken.IsCancellationRequested)
        {
            ResetBubbleScale();
            SetFoodAlpha(1f);
        }
    }

    private void SetFoodSprite(int nextIndex)
    {
        foodSpriteIndex = nextIndex;
        foodRender.sprite = validFoodSprites[foodSpriteIndex];
    }

    private async UniTask FadeBubbleAsync(float targetAlpha, float duration, CancellationToken cancellationToken)
    {
        bubbleTween?.Kill();
        int version = ++bubbleFadeVersion;
        targetAlpha = Mathf.Clamp01(targetAlpha);
        duration = Mathf.Max(0f, duration);

        if (duration <= 0f || groupAlpha == null)
        {
            SetBubbleAlpha(targetAlpha);
            return;
        }

        bubbleTween = DOTween
            .To(() => bubbleAlpha, SetBubbleAlpha, targetAlpha, duration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject);

        try
        {
            await UniTask.WaitForSeconds(duration, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested && version == bubbleFadeVersion)
        {
            SetBubbleAlpha(targetAlpha);
        }
    }

    private void SetBubbleAlpha(float value)
    {
        bubbleAlpha = Mathf.Clamp01(value);
        if (groupAlpha != null)
        {
            groupAlpha.SetAlpha(bubbleAlpha);
        }
    }

    private void StartFoodCycle(CancellationToken cancellationToken)
    {
        BuildValidFoodSprites();

        if (foodRender == null || validFoodSprites.Count < 2 || foodSwitchInterval <= 0f)
        {
            return;
        }

        InitFoodSprite();
        FoodCycleAsync(cancellationToken).Forget();
    }

    private async UniTaskVoid FoodCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (isSit && validFoodSprites.Count >= 2 && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.WaitForSeconds(foodSwitchInterval, cancellationToken: cancellationToken);

                if (!isSit || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                int nextIndex = (foodSpriteIndex + 1) % validFoodSprites.Count;
                await SwitchFoodSpriteAsync(nextIndex, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTask SwitchFoodSpriteAsync(int nextIndex, CancellationToken cancellationToken)
    {
        if (foodRender == null || nextIndex < 0 || nextIndex >= validFoodSprites.Count)
        {
            return;
        }

        foodTween?.Kill();
        float duration = Mathf.Max(0f, foodFadeDuration);

        if (duration <= 0f)
        {
            foodSpriteIndex = nextIndex;
            foodRender.sprite = validFoodSprites[foodSpriteIndex];
            SetFoodAlpha(1f);
            return;
        }

        foodTween = foodRender
            .DOFade(0f, duration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject);

        await UniTask.WaitForSeconds(duration, cancellationToken: cancellationToken);

        if (!isSit || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        foodSpriteIndex = nextIndex;
        foodRender.sprite = validFoodSprites[foodSpriteIndex];

        foodTween = foodRender
            .DOFade(1f, duration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject);

        await UniTask.WaitForSeconds(duration, cancellationToken: cancellationToken);

        if (!cancellationToken.IsCancellationRequested)
        {
            SetFoodAlpha(1f);
        }
    }

    private void BuildValidFoodSprites()
    {
        validFoodSprites.Clear();

        if (foodSprites == null)
        {
            return;
        }

        for (int i = 0; i < foodSprites.Length; i++)
        {
            if (foodSprites[i] != null)
            {
                validFoodSprites.Add(foodSprites[i]);
            }
        }
    }

    private void InitFoodSprite()
    {
        if (foodRender == null || validFoodSprites.Count <= 0)
        {
            return;
        }

        foodSpriteIndex = validFoodSprites.IndexOf(foodRender.sprite);
        if (foodSpriteIndex < 0)
        {
            foodSpriteIndex = 0;
            foodRender.sprite = validFoodSprites[foodSpriteIndex];
        }
    }

    private void InitFoodSwitchStar()
    {
        if (star == null)
        {
            hasTriggeredFoodSwitchStar = false;
            return;
        }

        star.SetActive(false);
        hasTriggeredFoodSwitchStar = false;
    }

    private void ResetFoodSwitchStarCounter()
    {
        switchFoodCount = 0;
        hasTriggeredFoodSwitchStar = star != null && star.activeSelf;
    }

    private void RecordFoodSwitchTrigger()
    {
        switchFoodCount++;

        if (switchFoodCount < Mathf.Max(1, foodMaxSwitchTriggerCount))
        {
            return;
        }

        TryShowFoodSwitchStar();
    }

    private void TryShowFoodSwitchStar()
    {
        if (hasTriggeredFoodSwitchStar)
        {
            return;
        }

        if (star == null)
        {
            hasTriggeredFoodSwitchStar = true;
            Debug.LogWarning("[Level61Ctrl] Food switch star trigger skipped: star is null.");
            return;
        }

        hasTriggeredFoodSwitchStar = true;
        star.SetActive(true);

        star.GetComponentInChildren<SpriteRenderer>().DOFade(1, 0.5f);
        star.GetComponent<Collider2D>().enabled = true;

        if (star.TryGetComponent(out Postcard_Star postcardStar))
        {
            postcardStar.RefreshSavedCollectionState();
        }
    }

    private void StopFoodCycle()
    {
        foodTween?.Kill();
        foodTween = null;
        SetFoodAlpha(bubbleAlpha);
    }

    private void StopFoodSwitch(bool resetBubbleScale)
    {
        isFoodSwitching = false;
        isBubbleReadyForClick = false;

        bubbleScaleTween?.Kill();
        bubbleScaleTween = null;

        foodTween?.Kill();
        foodTween = null;

        if (resetBubbleScale)
        {
            ResetBubbleScale();
        }
    }

    private void SetFoodAlpha(float value)
    {
        if (foodRender == null)
        {
            return;
        }

        var color = foodRender.color;
        color.a = Mathf.Clamp01(value);
        foodRender.color = color;
    }

    private void CacheBubbleOriginalScale()
    {
        if (hasBubbleOriginalScale || groupAlpha == null)
        {
            return;
        }

        bubbleOriginalScale = groupAlpha.transform.localScale;
        hasBubbleOriginalScale = true;
    }

    private void ResetBubbleScale(bool isOrigin = true)
    {
        if (!hasBubbleOriginalScale || groupAlpha == null)
        {
            return;
        }

        groupAlpha.transform.localScale = isOrigin ? bubbleOriginalScale : Vector3.zero;
    }

    private void CacheMenuBookOriginalState()
    {
        if (hasMenuBookOriginalState || menuBook == null)
        {
            return;
        }

        menuBookOriginalPosition = menuBook.position;
        menuBookOriginalActive = menuBook.gameObject.activeSelf;
        hasMenuBookOriginalState = true;
    }

    private void ReturnMenuBookToOriginal()
    {
        if (menuBook == null)
        {
            return;
        }

        CacheMenuBookOriginalState();
        menuBookTween?.Kill();
        menuBook.gameObject.SetActive(true);

        float duration = Mathf.Max(0f, menuBookReturnDuration);
        if (duration <= 0f)
        {
            menuBook.position = menuBookOriginalPosition;
            return;
        }

        menuBookTween = menuBook
            .DOJump(menuBookOriginalPosition, menuBookReturnJumpPower, 1, duration)
            .SetLink(menuBook.gameObject)
            .OnComplete(() =>
            {
                menuBook.position = menuBookOriginalPosition;
                menuBook.gameObject.SetActive(true);
            });
    }

    private void CancelSitCts()
    {
        if (sitCts == null)
        {
            return;
        }

        sitCts.Cancel();
        sitCts.Dispose();
        sitCts = null;
    }

    void OnDisable()
    {
        CleanupRuntimeState();
    }

    void OnDestroy()
    {
        CleanupRuntimeState();
    }

    private void CleanupRuntimeState()
    {
        UnregisterUseButton();
        CancelSitCts();
        StopFoodCycle();
        StopFoodSwitch(true);

        bubbleTween?.Kill();
        bubbleTween = null;

        bubbleScaleTween?.Kill();
        bubbleScaleTween = null;

        foodTween?.Kill();
        foodTween = null;

        menuBookTween?.Kill();
        menuBookTween = null;

        if (isSit && actorCtrl != null)
        {
            actorCtrl.StopSit();
        }

        isSit = false;
        isSitTransitioning = false;
        SetBubbleAlpha(0f);
        ResetBubbleScale();

        if (menuBook != null && hasMenuBookOriginalState)
        {
            menuBook.position = menuBookOriginalPosition;
            menuBook.gameObject.SetActive(menuBookOriginalActive);
        }
    }

    /// <summary>
    /// 停止钥匙直线下落并切换到漂浮表现。
    /// 调用边界：由 Level61 key 相关事件链在运行时调用；依赖 keyDirect 所在节点挂载 MoveFloatHandle。
    /// </summary>
    public void StopKey()
    {
        Debug.Log("StopKey");
        if (keyDirect == null)
        {
            Debug.LogWarning("[Level61Ctrl] StopKey skipped: keyDirect is null.");
            return;
        }

        keyDirect.enabled = false;
        if (!keyDirect.TryGetComponent(out MoveFloatHandle moveFloatHandle))
        {
            Debug.LogWarning("[Level61Ctrl] StopKey skipped: MoveFloatHandle is missing.");
            return;
        }

        moveFloatHandle.enabled = true;
    }

}
