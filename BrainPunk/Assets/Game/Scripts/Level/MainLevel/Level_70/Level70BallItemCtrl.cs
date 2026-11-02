using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 控制球的状态
/// 是否填充 item。inspector 可以调节。如果不填充，sprite 隐藏掉。
/// 1. init 初始化状态，刷新内部的 item 物品
/// 2. close 状态，等待被抓，collider 有碰撞。
/// 3. open 状态，隐藏 close 部分，展示 open 部分。collider 取消碰撞。
/// </summary>
public class Level70BallItemCtrl : MonoBehaviour
{
    // close 状态显示根节点；仅用于 Level70 抓钩物品开合表现，不参与存档或配置。
    [SerializeField] private GameObject closePart;

    // open 状态显示根节点；抓钩释放物品后显示，未配置时只切换 collider。
    [SerializeField] private GameObject openPart;

    // 物品主 SpriteRenderer；当没有内容时可隐藏，避免空物品阻塞抓钩流程。
    [SerializeField] private SpriteRenderer sprite;

    // 物品碰撞体；为空时运行时从本对象兜底获取，仅用于本关抓取/释放状态切换。
    [SerializeField] private Collider2D itemCollider;

    // 物品弹出执行器；为空时运行时从子节点兜底查找，仅在 Level70 ball 打开时播放，不写入存档或配置。
    [SerializeField] private ItemJumpOutExecutor jumpOut;

    // 球根节点的 SortingGroup；通过 dropArea 后只修改该组件的 Order in Layer，不逐个修改球壳 SpriteRenderer。
    [SerializeField] private SortingGroup sortingGroup;

    // 球通过 dropArea 后 SortingGroup 使用的 Order in Layer；不修改内部 item 的 SpriteRenderer 排序值。
    [SerializeField] private int openSortingOrder = 180;

    // 内部 item 打开后，SpriteRenderer 与 jumpOut 同对象可选 SortingGroup 使用的 Order in Layer。
    [SerializeField] private int openItemSortingOrder = 200;

    [SerializeField] private float rollForce = 5;

    // 球从触发打开到切换 open 状态的随机延迟区间（秒）；X/Y 顺序可互换，负值运行时按 0 处理。
    [SerializeField] private Vector2 ballOpenDelayRange = new Vector2(1.5f, 3f);

    // ball 打开时统一使用的世界 Z 轴角度；默认 0 表示按当前美术基准让开口朝向世界上方。
    [SerializeField] private float openUpwardWorldEulerZ;

    // ball 打开后仅让 openPart 球壳渐隐的时长（秒）；小于等于 0 时立即隐藏，不影响已脱离的 Item。
    [Min(0f)] [SerializeField] private float openPartFadeDuration = 0.5f;

    private bool hasOpened;
    private RigidbodyConstraints2D defaultRigidbodyConstraints;
    private bool hasDefaultRigidbodyConstraints;
    private int defaultSortingGroupOrder;
    private bool hasDefaultSortingGroupOrder;
    private int defaultItemSortingOrder;
    private bool hasDefaultItemSortingOrder;
    private SortingGroup jumpOutSortingGroup;
    private int defaultJumpOutSortingGroupOrder;
    private bool hasDefaultJumpOutSortingGroupOrder;
    private Quaternion defaultLocalRotation;
    private bool hasDefaultLocalRotation;
    private SpriteRenderer[] openPartRenderers;
    private Color[] defaultOpenPartColors;
    private Sequence openPartFadeSequence;

    private Rigidbody2D ballRigid;

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        SetClosedState();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 按 Level70 的 itemSprites 映射初始化球内容；仅在关卡进入、重开或清理重置时调用，运行中普通接触不应调用。
    /// 传入 null 表示可见空内容：隐藏内部 sprite，但保留 close 外观和 collider 参与物理/触发。
    /// </summary>
    /// <param name="contentSprite">本球要展示的物品 sprite；为空时显示为空内容。</param>
    public void InitializeContent(Sprite contentSprite)
    {
        ResolveReferences();
        hasOpened = false;

        if (sprite != null)
        {
            sprite.sprite = contentSprite;
            sprite.enabled = contentSprite != null;
        }

        if (closePart != null)
            closePart.SetActive(true);

        if (openPart != null)
            openPart.SetActive(false);

        if (itemCollider != null)
            itemCollider.enabled = true;

        RestoreRigidbodyConstraints();
        RestoreBallRotation();
        RestoreOpenPartAppearance();
        RestoreSortingGroupOrder();
        RestoreItemSortingOrder();
    }

    /// <summary>
    /// 触发 Level70 ball 打开并播放内部 sprite item 弹出；仅供本关 tools Trigger2D 调用，重复打开不会重复播放弹出表现。
    /// </summary>
    public async Task OpenAndPlayJumpOut()
    {
        if (!Application.isPlaying || hasOpened)
            return;

        ResolveReferences();
        SetSortingGroupOrder(openSortingOrder);

        if (ballRigid != null)
        {
            ballRigid.AddForceX(rollForce, ForceMode2D.Impulse);
        }

        if (itemCollider != null)
            itemCollider.excludeLayers = 1 << LayerMask.NameToLayer("Stuff") | 1 << LayerMask.NameToLayer("NonCollisionRigibodyLayer");

        await UniTask.WaitForSeconds(GetRandomOpenDelay());

        SetOpenState();

        if (jumpOut != null)
        {
            // 切换一下父节点
            jumpOut.transform.SetParent(jumpOut.transform.parent.parent);
            SetOpenItemSortingOrder();
            jumpOut.Play();

        }
        
    }

    /// <summary>
    /// 切换到等待被抓的 close 状态；调用边界是 Level70 初始化、回滚或抓取前刷新。
    /// </summary>
    public void SetClosedState()
    {
        ResolveReferences();
        hasOpened = false;

        if (closePart != null)
            closePart.SetActive(true);

        if (openPart != null)
            openPart.SetActive(false);

        if (sprite != null)
            sprite.enabled = sprite.sprite != null;

        if (itemCollider != null)
            itemCollider.enabled = true;

        RestoreRigidbodyConstraints();
        RestoreBallRotation();
        RestoreOpenPartAppearance();
        RestoreSortingGroupOrder();
        RestoreItemSortingOrder();
    }

    /// <summary>
    /// 切换到已打开/释放状态；只影响 Level70 本地视觉、collider 和球刚体约束，不修改背包、存档或配置数据。
    /// </summary>
    public void SetOpenState()
    {
        ResolveReferences();
        hasOpened = true;

        if (closePart != null)
            closePart.SetActive(false);

        RestoreOpenPartAppearance();

        if (openPart != null)
            openPart.SetActive(true);

        // if (itemCollider != null)
        //     itemCollider.enabled = false;

        FreezeOpenedRigidbody();
        SetOpenUpwardRotation();
        SetSortingGroupOrder(openSortingOrder);
        PlayOpenPartFade();
    }

    #endregion

    #region Resolve

    private void ResolveReferences()
    {
        if (!hasDefaultLocalRotation)
        {
            defaultLocalRotation = transform.localRotation;
            hasDefaultLocalRotation = true;
        }

        if (itemCollider == null)
            itemCollider = GetComponent<Collider2D>();

        if (ballRigid == null)
            ballRigid = GetComponent<Rigidbody2D>();

        if (ballRigid != null && !hasDefaultRigidbodyConstraints)
        {
            defaultRigidbodyConstraints = ballRigid.constraints;
            hasDefaultRigidbodyConstraints = true;
        }

        if (jumpOut == null)
            jumpOut = GetComponentInChildren<ItemJumpOutExecutor>(true);

        if (sortingGroup == null)
            sortingGroup = GetComponent<SortingGroup>();

        if (sortingGroup != null && !hasDefaultSortingGroupOrder)
        {
            defaultSortingGroupOrder = sortingGroup.sortingOrder;
            hasDefaultSortingGroupOrder = true;
        }

        if (sprite == null && jumpOut != null)
            sprite = jumpOut.GetComponent<SpriteRenderer>();

        if (sprite == null && jumpOut != null)
            sprite = jumpOut.GetComponentInChildren<SpriteRenderer>(true);

        if (sprite == null)
            sprite = GetComponentInChildren<SpriteRenderer>(true);

        if (sprite != null && !hasDefaultItemSortingOrder)
        {
            defaultItemSortingOrder = sprite.sortingOrder;
            hasDefaultItemSortingOrder = true;
        }

        if (jumpOutSortingGroup == null && jumpOut != null)
            jumpOutSortingGroup = jumpOut.GetComponent<SortingGroup>();

        if (jumpOutSortingGroup != null && !hasDefaultJumpOutSortingGroupOrder)
        {
            defaultJumpOutSortingGroupOrder = jumpOutSortingGroup.sortingOrder;
            hasDefaultJumpOutSortingGroupOrder = true;
        }

        if (openPartRenderers == null && openPart != null)
        {
            openPartRenderers = openPart.GetComponentsInChildren<SpriteRenderer>(true);
            defaultOpenPartColors = new Color[openPartRenderers.Length];
            for (int i = 0; i < openPartRenderers.Length; i++)
            {
                if (openPartRenderers[i] != null)
                    defaultOpenPartColors[i] = openPartRenderers[i].color;
            }
        }

    }

    private void SetSortingGroupOrder(int sortingOrder)
    {
        if (sortingGroup != null)
            sortingGroup.sortingOrder = sortingOrder;
    }

    private void RestoreSortingGroupOrder()
    {
        if (sortingGroup != null && hasDefaultSortingGroupOrder)
            sortingGroup.sortingOrder = defaultSortingGroupOrder;
    }

    private void RestoreItemSortingOrder()
    {
        if (sprite != null && hasDefaultItemSortingOrder)
            sprite.sortingOrder = defaultItemSortingOrder;

        if (jumpOutSortingGroup != null && hasDefaultJumpOutSortingGroupOrder)
            jumpOutSortingGroup.sortingOrder = defaultJumpOutSortingGroupOrder;
    }

    private void RestoreBallRotation()
    {
        if (hasDefaultLocalRotation)
            transform.localRotation = defaultLocalRotation;
    }

    private void SetOpenUpwardRotation()
    {
        transform.rotation = Quaternion.Euler(0f, 0f, openUpwardWorldEulerZ);
    }

    private void RestoreOpenPartAppearance()
    {
        KillOpenPartFade();
        if (openPartRenderers == null || defaultOpenPartColors == null)
            return;

        int rendererCount = Mathf.Min(openPartRenderers.Length, defaultOpenPartColors.Length);
        for (int i = 0; i < rendererCount; i++)
        {
            if (openPartRenderers[i] != null)
                openPartRenderers[i].color = defaultOpenPartColors[i];
        }
    }

    private void PlayOpenPartFade()
    {
        if (openPart == null || openPartRenderers == null || openPartRenderers.Length == 0)
            return;

        float fadeDuration = Mathf.Max(0f, openPartFadeDuration);
        if (fadeDuration <= 0f)
        {
            SetOpenPartAlpha(0f);
            openPart.SetActive(false);
            return;
        }

        openPartFadeSequence = DOTween.Sequence().SetLink(gameObject);
        for (int i = 0; i < openPartRenderers.Length; i++)
        {
            if (openPartRenderers[i] != null)
                openPartFadeSequence.Join(openPartRenderers[i].DOFade(0f, fadeDuration));
        }

        openPartFadeSequence.OnComplete(() =>
        {
            if (openPart != null)
                openPart.SetActive(false);
        });
        openPartFadeSequence.OnKill(() => openPartFadeSequence = null);
    }

    private void SetOpenPartAlpha(float alpha)
    {
        if (openPartRenderers == null)
            return;

        for (int i = 0; i < openPartRenderers.Length; i++)
        {
            SpriteRenderer targetRenderer = openPartRenderers[i];
            if (targetRenderer == null)
                continue;

            Color color = targetRenderer.color;
            color.a = alpha;
            targetRenderer.color = color;
        }
    }

    private void KillOpenPartFade()
    {
        if (openPartFadeSequence != null && openPartFadeSequence.IsActive())
            openPartFadeSequence.Kill();

        openPartFadeSequence = null;
    }

    private void SetOpenItemSortingOrder()
    {
        if (sprite != null)
            sprite.sortingOrder = openItemSortingOrder;

        if (jumpOutSortingGroup != null)
            jumpOutSortingGroup.sortingOrder = openItemSortingOrder;
    }

    private void FreezeOpenedRigidbody()
    {
        if (!Application.isPlaying || ballRigid == null)
            return;

        ballRigid.linearVelocity = Vector2.zero;
        ballRigid.angularVelocity = 0f;
        ballRigid.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void RestoreRigidbodyConstraints()
    {
        if (ballRigid != null && hasDefaultRigidbodyConstraints)
            ballRigid.constraints = defaultRigidbodyConstraints;
    }

    private float GetRandomOpenDelay()
    {
        float minDelay = Mathf.Max(0f, Mathf.Min(ballOpenDelayRange.x, ballOpenDelayRange.y));
        float maxDelay = Mathf.Max(minDelay, Mathf.Max(ballOpenDelayRange.x, ballOpenDelayRange.y));
        return Random.Range(minDelay, maxDelay);
    }

    #endregion
}
