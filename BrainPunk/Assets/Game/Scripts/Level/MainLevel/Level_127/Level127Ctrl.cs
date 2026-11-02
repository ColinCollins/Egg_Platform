using System.Collections.Generic;
using Bear.Logger;
using DG.Tweening;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level 127 私有控制器：14 块两击砖的点击输入与固定藏星/随机藏物分配、击碎特效与掉落、
/// "只击碎三块钥匙砖"的严格钥匙合成条件；普通道具不与主角或彼此发生实体碰撞。
/// 门锁解锁完全复用 083 同款 Key→DoorLock 纯 prefab 事件链，本控制器不参与门锁逻辑。
/// </summary>
public sealed class Level127Ctrl : MonoBehaviour, IDebuger
{
    private const string PropDropLayerName = "TriggerItem";
    private const string ActorLayerName = "Actor";
    private const string StarDropNameFormat = "Level127StarDrop_{0}";
    private const string StarBrickR02C02Name = "PlaceholderBrick_R02_C02";
    private const string StarBrickR03C04Name = "PlaceholderBrick_R03_C04";
    private const int BrickFlashSortingOffset = 1;
    private const int DropSortingOffset = 2;
    private const int KeyTransitionSortingOffset = 1;
    private const int SynthesisBurstSortingOffset = 3;
    private const float KeyTransitionFlashScaleMultiplier = 1.3f;
    /// <summary>掉落物飞行期间忽略砖块，只探测 Ground(6) 地面。</summary>
    private const int DropLandingMask = 1 << 6;

    [Header("Hidden Items")]
    [Tooltip("14 种普通道具候选贴图；已收集星位会回到此随机池，洗牌后优先不重复分配。")]
    [SerializeField] private Sprite[] propSprites;

    [Header("Hidden Stars")]
    [Tooltip("固定藏星模板，按索引顺序对应 R02_C02 的星星 1 与 R03_C04 的星星 2；模板仅用于运行时复制。")]
    [SerializeField] private GameObject[] starTemplates;

    [Tooltip("倍率为 1 时掉落物显示的最大边长（世界单位）。")]
    [SerializeField, Min(0.1f)] private float dropMaxSize = 0.8f;

    [Tooltip("普通道具视觉与碰撞体相对当前基准的统一倍率；标准星星不使用此倍率。")]
    [SerializeField, Min(0.1f)] private float hiddenItemScaleMultiplier = 1.5f;

    [Tooltip("掉落物抛物线弹出时长区间（秒）。")]
    [SerializeField] private Vector2 dropPopDurationRange = new(0.45f, 0.6f);

    [Tooltip("掉落物抛物线拱顶高（相对起点，世界单位）。")]
    [SerializeField, Min(0f)] private float dropPopArcHeight = 0.6f;

    [Tooltip("掉落物落点水平随机偏移（世界单位）。")]
    [SerializeField, Min(0f)] private float dropLandingSpreadX = 0.4f;

    [Header("Key Bricks")]
    [Tooltip("钥匙砖名字：第二行从左数第 2 块、第三行从右数第 1/2 块。")]
    [SerializeField] private string[] keyBrickNames =
        { "PlaceholderBrick_R02_C02", "PlaceholderBrick_R03_C05", "PlaceholderBrick_R03_C04" };

    [Header("Key Synthesis")]
    [Tooltip("第三块钥匙砖击碎后到播放合成特效的停顿时长（秒），给玩家反应时间。")]
    [SerializeField, Min(0f)] private float synthesisDelaySeconds = 1.2f;

    [Tooltip("合成音效 tag（006 关密码正确同款）。")]
    [SerializeField] private string synthesisSfxTag = "passwordCorrect";

    [Tooltip("砖阵与钥匙局部闪白使用的本关白化材质。")]
    [SerializeField] private Material synthesisFlashMaterial;

    [Tooltip("合成前砖阵整体闪白反复次数。")]
    [SerializeField, Min(1)] private int synthesisFlashCycles = 3;

    [Tooltip("单次闪白（亮+暗）时长（秒）。")]
    [SerializeField, Min(0.1f)] private float synthesisFlashCycleDuration = 0.4f;

    [Tooltip("砖阵第一次闪白的透明度峰值。")]
    [SerializeField, Range(0f, 1f)] private float synthesisFlashFirstPeakAlpha = 0.55f;

    [Tooltip("砖阵最后一次闪白的透明度峰值。")]
    [SerializeField, Range(0f, 1f)] private float synthesisFlashLastPeakAlpha = 1f;

    [Tooltip("钥匙状砖阵整体缩小到实体钥匙视觉尺寸的时长（秒）。")]
    [SerializeField, Min(0.1f)] private float synthesisScaleDuration = 1.3333333f;

    [Tooltip("收缩开头的砖阵蓄力放大倍率；该阶段包含在 synthesisScaleDuration 内。")]
    [SerializeField, Min(1f)] private float synthesisAnticipationScale = 1.04f;

    [Tooltip("收缩总时长中用于砖阵向外蓄力的时长（秒）。")]
    [SerializeField, Min(0f)] private float synthesisAnticipationDuration = 0.08f;

    [Tooltip("砖阵收缩期间保留的白色余光透明度。")]
    [SerializeField, Range(0f, 1f)] private float synthesisShrinkGlowAlpha = 0.12f;

    [Tooltip("砖阵与实体钥匙切换时，局部白色闪光的透明度峰值。")]
    [SerializeField, Range(0f, 1f)] private float transitionFlashPeakAlpha = 0.9f;

    [Tooltip("局部白色切换闪光的总时长（秒），不覆盖全屏。")]
    [SerializeField, Min(0.1f)] private float transitionFlashDuration = 0.3f;

    [Header("Key Synthesis Impact")]
    [Tooltip("钥匙切换峰值使用的 additive 星光材质。")]
    [SerializeField] private Material synthesisBurstMaterial;

    [Tooltip("钥匙切换峰值星光的暖白颜色。")]
    [SerializeField] private Color synthesisBurstColor = new(1f, 0.9f, 0.58f, 1f);

    [Tooltip("钥匙切换峰值一次生成的星光数量。")]
    [SerializeField, Min(1)] private int synthesisBurstCount = 16;

    [Tooltip("星光生命周期区间（秒）。")]
    [SerializeField] private Vector2 synthesisBurstLifetimeRange = new(0.35f, 0.6f);

    [Tooltip("星光向外速度区间（世界单位/秒）。")]
    [SerializeField] private Vector2 synthesisBurstSpeedRange = new(1.4f, 2.4f);

    [Tooltip("星光尺寸区间（世界单位）。")]
    [SerializeField] private Vector2 synthesisBurstSizeRange = new(0.08f, 0.16f);

    [Tooltip("星光生成圆的半径（世界单位）。")]
    [SerializeField, Min(0f)] private float synthesisBurstRadius = 0.12f;

    [Tooltip("钥匙切换峰值的镜头轻震时长（秒）。")]
    [SerializeField, Min(0.01f)] private float synthesisCameraShakeDuration = 0.22f;

    [Tooltip("钥匙切换峰值的镜头轻震强度。")]
    [SerializeField, Min(0f)] private float synthesisCameraShakeStrength = 0.12f;

    [Tooltip("钥匙切换峰值的镜头轻震次数。")]
    [SerializeField, Min(1)] private int synthesisCameraShakeVibrato = 18;

    [Header("Break Vfx")]
    [Tooltip("第二击碎裂图集粒子 Prefab（碎裂 10 帧 5x2 图集）。")]
    [SerializeField] private ParticleSystem shatterVfxPrefab;

    [Tooltip("碎裂粒子实例缩放；当前 127 配置为 1.2（此前 0.6 的 2 倍）。")]
    [SerializeField, Min(0.01f)] private float shatterVfxScale = 1.2f;

    [Tooltip("碎裂粒子一次爆发数量。")]
    [SerializeField, Min(1)] private int shatterBurstCount = 8;

    [Header("Key Object")]
    [Tooltip("预置钥匙物体名（083 同款 Key 实例，sprite 已覆写为 Sp_l127_st_03）。")]
    [SerializeField] private string keyObjectName = "Key";

    [Tooltip("钥匙落点锚点名（墙正下方地面）。")]
    [SerializeField] private string keyLandingObjectName = "KeyLandingPoint";

    [Tooltip("实体钥匙最终视觉缩放，相对当前 Key prefab 原尺寸。")]
    [SerializeField, Range(0.1f, 1f)] private float keyFinalScale = 0.75f;

    [Tooltip("实体钥匙出现后的短促回弹峰值倍率。")]
    [SerializeField, Min(1f)] private float keyPopScaleMultiplier = 1.1f;

    [Tooltip("实体钥匙回弹占用空中停留阶段的时长（秒）。")]
    [SerializeField, Min(0f)] private float keyPopDuration = 0.3f;

    [Tooltip("实体钥匙切换完成后，在空中停留的时长（秒）。")]
    [SerializeField, Min(0f)] private float keyHoldDuration = 0.3f;

    [Tooltip("拾取碰撞范围相对最终钥匙视觉包围盒的倍率。")]
    [SerializeField, Min(1f)] private float keyPickupColliderScale = 1.3f;

    [Tooltip("钥匙从墙位置缓慢落到地面的时长（秒）。")]
    [SerializeField, Min(0.1f)] private float keyFallDuration = 1.3f;

    private readonly List<Level127Brick> bricks = new();
    private readonly HashSet<string> keyBrickNameSet = new();
    private readonly Dictionary<Level127Brick, GameObject> hiddenStarByBrick = new();
    private readonly Dictionary<Level127Brick, int> starIndexByBrick = new();
    private readonly Dictionary<Level127Brick, Sprite> hiddenPropByBrick = new();
    private readonly List<BoxCollider2D> propDropColliders = new();
    private readonly HashSet<Level127Brick> smashedBricks = new();

    private ActorCtrl actor;
    private Camera worldCamera;
    private Transform dropRoot;
    private Transform effectRoot;
    private GameObject keyObject;
    private Transform keyLandingPoint;
    private Collider2D keyPickupCollider;
    private MoveFloatHandle keyFloatHandle;
    private Tween keyTween;
    private Transform synthesisCameraShakeTarget;
    private Vector3 synthesisCameraInitialLocalPosition;
    private Tween synthesisCameraShakeTween;
    private int smashedKeyBrickCount;
    private int smashedNonKeyBrickCount;
    private float synthesisTime;
    private bool synthesisScheduled;
    private bool keySpawned;
    private bool keyBrokenThisRun;

    /// <summary>合成排队后为 true：剩余砖块与点击输入全部锁定，等待合成特效接管。</summary>
    public bool InteractionLocked => synthesisScheduled || keySpawned;

    /// <summary>砖块碎片等临时特效的父节点（随关卡重建销毁）。</summary>
    public Transform EffectRoot => EnsureEffectRoot();

    private void Awake()
    {
        foreach (string keyBrickName in keyBrickNames)
            keyBrickNameSet.Add(keyBrickName);

        ResolveCamera();

        BaseLevelCtrl levelCtrl = GetComponentInParent<BaseLevelCtrl>(true);
        Debug.Log(
            $"[Level127Ctrl] Awake self:{name}, root:{transform.root.name}, baseLevelCtrl:{(levelCtrl != null ? levelCtrl.name : "NULL")}",
            this);
    }

    private void Start()
    {
        // 组件挂在 PlaceholderBrickWall（砖墙节点）上；Key/KeyLandingPoint/Actor 都在关卡根下，统一从关卡根查找。
        Transform levelRoot = ResolveLevelRoot();
        actor = levelRoot.GetComponentInChildren<ActorCtrl>(true);
        keyObject = FindChildByName(levelRoot, keyObjectName)?.gameObject;
        keyLandingPoint = FindChildByName(levelRoot, keyLandingObjectName);

        if (keyObject != null)
        {
            keyPickupCollider = keyObject.GetComponent<Collider2D>();
            keyFloatHandle = keyObject.GetComponent<MoveFloatHandle>();
            if (keyFloatHandle != null)
            {
                keyFloatHandle.StopMove();
                keyFloatHandle.enabled = false;
            }

            if (keyPickupCollider != null)
                keyPickupCollider.enabled = false;

            ConfigureKeyPickupCollider();
        }

        if (actor == null)
            this.LogWarning("[Level127Ctrl] 未找到 ActorCtrl，普通道具仅通过 Actor 层排除角色碰撞。");
        if (keyObject == null || keyLandingPoint == null)
            this.LogWarning("[Level127Ctrl] 预置钥匙或钥匙落点缺失，钥匙合成链路不完整。");
        if (shatterVfxPrefab == null)
            this.LogWarning("[Level127Ctrl] shatterVfxPrefab 未绑定（检查 Level_127.prefab 引用与 Unity 资产库刷新），击碎将没有碎裂粒子。");

        DisableStarTemplates();
        AssignHiddenItems();
        ValidateConfiguration();
    }

    private void OnDestroy()
    {
        keyTween?.Kill();
        synthesisCameraShakeTween?.Kill();
        RestoreSynthesisCameraPosition();
    }

    private void Update()
    {
        if (keySpawned || !IsGameplayPlaying() || InteractionLocked)
            return;

        ResolveCamera();
        if (worldCamera == null)
            return;

        if (!TryGetTapScreenPosition(out Vector2 screenPosition))
            return;

        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
        for (int index = 0; index < bricks.Count; index++)
        {
            Level127Brick brick = bricks[index];
            if (brick != null && brick.ContainsWorldPoint(worldPosition) && brick.TapHit())
                break;
        }
    }

    #region Brick Registration

    /// <summary>砖块在 OnEnable 时自注册；注册顺序无关，钥匙砖按名字识别。</summary>
    public void RegisterBrick(Level127Brick brick)
    {
        if (brick != null && !bricks.Contains(brick))
            bricks.Add(brick);
    }

    public void UnregisterBrick(Level127Brick brick)
    {
        bricks.Remove(brick);
    }

    #endregion

    #region Hidden Item Assignment

    /// <summary>按星星永久收集状态固定藏星，其余 12～14 块砖从普通道具池洗牌分配。</summary>
    private void AssignHiddenItems()
    {
        hiddenStarByBrick.Clear();
        starIndexByBrick.Clear();
        hiddenPropByBrick.Clear();

        List<Level127Brick> pool = new(bricks);
        HashSet<Level127Brick> fixedStarBricks = new();
        List<Level127Brick> replacementPropBricks = new();
        AssignFixedStar(StarBrickR02C02Name, 0, 1, fixedStarBricks, replacementPropBricks);
        AssignFixedStar(StarBrickR03C04Name, 1, 2, fixedStarBricks, replacementPropBricks);
        for (int index = pool.Count - 1; index >= 0; index--)
        {
            if (fixedStarBricks.Contains(pool[index]))
                pool.RemoveAt(index);
        }

        List<Sprite> props = CollectPropSprites();
        Shuffle(props);

        if (props.Count == 0)
        {
            this.LogWarning("[Level127Ctrl] 没有可用的普通道具贴图，无法保证每块砖都有可见掉落物。");
            return;
        }

        List<Level127Brick> propBricks = new(pool);
        propBricks.AddRange(replacementPropBricks);
        Shuffle(propBricks);

        for (int index = 0; index < propBricks.Count; index++)
            hiddenPropByBrick[propBricks[index]] = props[index % props.Count];
    }

    private void AssignFixedStar(
        string brickName,
        int templateIndex,
        int starIndex,
        HashSet<Level127Brick> fixedStarBricks,
        List<Level127Brick> replacementPropBricks)
    {
        Level127Brick brick = FindBrickNamed(brickName);
        if (brick == null)
        {
            this.LogWarning($"[Level127Ctrl] 固定藏星砖 {brickName} 未注册。");
            return;
        }

        // 固定星位未收集时出星，已收集时由普通道具替代。
        fixedStarBricks.Add(brick);
        starIndexByBrick[brick] = starIndex;
        GameObject template = starTemplates != null && templateIndex < starTemplates.Length
            ? starTemplates[templateIndex]
            : null;
        if (template == null)
        {
            this.LogWarning($"[Level127Ctrl] 固定星星模板 {templateIndex + 1} 未绑定：{brickName}。");
            replacementPropBricks.Add(brick);
            return;
        }

        // 已永久收集的星位进入普通道具槽，下一次击碎该砖时由普通道具替代。
        if (IsSavedStarCollected(starIndex))
        {
            replacementPropBricks.Add(brick);
            return;
        }

        hiddenStarByBrick[brick] = template;
    }

    private List<Sprite> CollectPropSprites()
    {
        List<Sprite> props = new();
        if (propSprites == null)
            return props;

        foreach (Sprite propSprite in propSprites)
        {
            if (propSprite != null && !props.Contains(propSprite))
                props.Add(propSprite);
        }

        return props;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int index = list.Count - 1; index > 0; index--)
        {
            int swap = UnityEngine.Random.Range(0, index + 1);
            (list[index], list[swap]) = (list[swap], list[index]);
        }
    }

    private void ValidateConfiguration()
    {
        if (bricks.Count != 14)
            this.LogWarning($"[Level127Ctrl] 注册砖块数量为 {bricks.Count}，预期 14。");

        foreach (string keyBrickName in keyBrickNames)
        {
            if (!ContainsBrickNamed(keyBrickName))
                this.LogWarning($"[Level127Ctrl] 钥匙砖 {keyBrickName} 未注册，钥匙合成条件无法满足。");
        }

        if (starTemplates == null || starTemplates.Length != 2)
            this.LogWarning("[Level127Ctrl] 固定星星模板应为 2 个，并按索引 1/2 排列。");
        ValidateFixedStar(StarBrickR02C02Name, 0, 1);
        ValidateFixedStar(StarBrickR03C04Name, 1, 2);
        if (CollectPropSprites().Count < 14)
            this.LogWarning("[Level127Ctrl] 不同的有效普通道具候选少于 14 个，槽位不足时会循环复用现有道具。");
        if (synthesisFlashMaterial == null)
            this.LogWarning("[Level127Ctrl] synthesisFlashMaterial 未绑定，砖阵与钥匙无法显示真实白化闪光。");
        if (synthesisBurstMaterial == null)
            this.LogWarning("[Level127Ctrl] synthesisBurstMaterial 未绑定，钥匙切换将没有星光爆发。");
    }

    private bool ContainsBrickNamed(string brickName)
    {
        return FindBrickNamed(brickName) != null;
    }

    private Level127Brick FindBrickNamed(string brickName)
    {
        foreach (Level127Brick brick in bricks)
        {
            if (brick != null && brick.name == brickName)
                return brick;
        }

        return null;
    }

    private void ValidateFixedStar(string brickName, int templateIndex, int starIndex)
    {
        Level127Brick brick = FindBrickNamed(brickName);
        if (brick != null && !keyBrickNameSet.Contains(brickName))
            this.LogWarning($"固定藏星砖 {brickName} 不在钥匙砖列表中，配置与玩法规则不一致。");

        GameObject template = starTemplates != null && templateIndex < starTemplates.Length
            ? starTemplates[templateIndex]
            : null;
        if (template != null && template.GetComponent<Postcard_Star>() == null)
            this.LogWarning($"固定星星模板 {templateIndex + 1} 缺少 Postcard_Star：{brickName}。");
        if (starIndex < 1 || starIndex > 3)
            this.LogWarning($"固定星星索引无效：{brickName} -> {starIndex}。");
    }

    private bool IsSavedStarCollected(int starIndex)
    {
        if (!TryGetCurrentLevelKey(out string levelKey))
            return false;

        PostcardManager postcardManager = GameManager.Instance?.Postcard;
        return postcardManager != null && postcardManager.IsLevelStarCollected(levelKey, starIndex);
    }

    private static bool TryGetCurrentLevelKey(out string levelKey)
    {
        levelKey = string.Empty;
        Config.LevelData currentLevelData = PlayCtrl.Instance?.Level?.CurrentLevelData;
        if (currentLevelData == null)
            return false;

        levelKey = currentLevelData.Id;
        return !string.IsNullOrEmpty(levelKey);
    }

    private void DisableStarTemplates()
    {
        if (starTemplates == null)
            return;

        foreach (GameObject starTemplate in starTemplates)
        {
            if (starTemplate != null)
                starTemplate.SetActive(false);
        }
    }

    #endregion

    #region Brick Smashed Flow

    /// <summary>砖块第二击击碎时由 Level127Brick 调用（调用方随后自行隐藏砖块）。</summary>
    public void NotifyBrickSmashed(Level127Brick brick)
    {
        if (brick == null || !smashedBricks.Add(brick))
            return;

        PlayShatterVfx(brick);
        SpawnHiddenItem(brick);

        if (keyBrickNameSet.Contains(brick.name))
            smashedKeyBrickCount++;
        else
            smashedNonKeyBrickCount++;

        EvaluateKeyCondition();
    }

    private void PlayShatterVfx(Level127Brick brick)
    {
        if (shatterVfxPrefab == null)
        {
            this.LogWarning("[Level127Ctrl] 碎裂粒子 Prefab 缺失，跳过击碎特效。");
            return;
        }

        ParticleSystem instance = Instantiate(
            shatterVfxPrefab,
            brick.Center,
            shatterVfxPrefab.transform.rotation,
            EnsureEffectRoot());
        instance.transform.localScale = Vector3.one * shatterVfxScale;
        instance.gameObject.SetActive(true);

        ResolveBrickSorting(brick, out int sortingLayerId, out int sortingOrder);
        foreach (ParticleSystemRenderer particleRenderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            particleRenderer.sortingLayerID = sortingLayerId;
            particleRenderer.sortingOrder = sortingOrder + DropSortingOffset;
        }

        ParticleSystem.EmissionModule emission = instance.emission;
        if (emission.burstCount > 0)
        {
            ParticleSystem.Burst burst = emission.GetBurst(0);
            burst.count = new ParticleSystem.MinMaxCurve(Mathf.Max(1, shatterBurstCount));
            emission.SetBurst(0, burst);
        }

        instance.Play(true);
        Destroy(instance.gameObject, 3f);
    }

    private void SpawnHiddenItem(Level127Brick brick)
    {
        if (hiddenStarByBrick.TryGetValue(brick, out GameObject starTemplate))
        {
            if (starIndexByBrick.TryGetValue(brick, out int starIndex) && IsSavedStarCollected(starIndex))
                SpawnReplacementPropDrop(brick);
            else
                SpawnStarDrop(brick, starTemplate);
            return;
        }

        if (hiddenPropByBrick.TryGetValue(brick, out Sprite propSprite) && propSprite != null)
            SpawnPropDrop(brick, propSprite);
    }

    private void EvaluateKeyCondition()
    {
        if (synthesisScheduled || keySpawned || keyBrokenThisRun)
            return;

        if (smashedNonKeyBrickCount > 0)
        {
            // 严格规则：多击碎任一非钥匙砖，本局无法合成钥匙，等待玩家自行 Reset 重开。
            keyBrokenThisRun = true;
            this.LogWarning("[Level127Ctrl] 已击碎非钥匙砖，本局钥匙合成条件失效。");
            return;
        }

        if (smashedKeyBrickCount >= keyBrickNames.Length)
        {
            synthesisScheduled = true;
            synthesisTime = Time.time + synthesisDelaySeconds;
            foreach (Level127Brick brick in bricks)
            {
                if (brick != null && !brick.IsSmashed)
                    brick.PrepareForSynthesis();
            }
        }
    }

    #endregion

    #region Key Synthesis

    private void TickSynthesis()
    {
        if (!synthesisScheduled || keySpawned || Time.time < synthesisTime)
            return;

        keySpawned = true;
        PerformSynthesis();
    }

    private void PerformSynthesis()
    {
        if (keyObject == null || keyLandingPoint == null)
            return;

        // 严格条件成立时剩余 11 块砖组成钥匙轮廓；收集其视觉包围盒用于等比收缩。
        List<Level127Brick> remaining = new();
        Bounds formationBounds = new(Vector3.zero, Vector3.zero);
        bool hasFormationBounds = false;
        foreach (Level127Brick brick in bricks)
        {
            if (brick == null || brick.IsSmashed)
                continue;

            remaining.Add(brick);
            Renderer brickRenderer = brick.GetComponent<Renderer>();
            if (brickRenderer == null)
                continue;

            if (hasFormationBounds)
                formationBounds.Encapsulate(brickRenderer.bounds);
            else
            {
                formationBounds = brickRenderer.bounds;
                hasFormationBounds = true;
            }
        }

        if (!hasFormationBounds || remaining.Count == 0 || !TryGetKeyLocalVisualBounds(out Bounds keyLocalBounds))
        {
            this.LogWarning("[Level127Ctrl] 砖阵或钥匙视觉包围盒缺失，直接把最终钥匙放到落点。");
            PlaceKeyAtLanding();
            return;
        }

        ConfigureKeyPickupCollider(keyLocalBounds);
        Vector2 finalKeyWorldSize = ComputeFinalKeyWorldSize(keyLocalBounds);
        float formationTargetScale = ComputeFormationTargetScale(formationBounds, finalKeyWorldSize);
        Vector3 formationCenter = formationBounds.center;
        Vector3 keyStartPosition = ComputeKeyRootPositionForVisualCenter(formationCenter, keyLocalBounds);

        keyObject.transform.position = keyStartPosition;
        keyObject.transform.localScale = Vector3.one * keyFinalScale;
        if (keyPickupCollider != null)
            keyPickupCollider.enabled = false;
        if (keyFloatHandle != null)
        {
            keyFloatHandle.StopMove();
            keyFloatHandle.enabled = false;
        }

        Vector3[] startPositions = new Vector3[remaining.Count];
        Vector3[] startScales = new Vector3[remaining.Count];
        for (int index = 0; index < remaining.Count; index++)
        {
            startPositions[index] = remaining[index].transform.position;
            startScales[index] = remaining[index].transform.localScale;
        }

        List<SpriteRenderer> overlays = CreateWhiteOverlays(remaining);
        SpriteRenderer transitionFlash = CreateKeyTransitionFlash(formationCenter, finalKeyWorldSize);
        float flashProgress = 0f;
        float formationScale = 1f;
        float overlayAlpha = 0f;
        int flashCycles = Mathf.Max(1, synthesisFlashCycles);
        float totalFlashDuration = flashCycles * Mathf.Max(0.1f, synthesisFlashCycleDuration);
        float totalScaleDuration = Mathf.Max(0.1f, synthesisScaleDuration);
        float anticipationDuration = Mathf.Clamp(
            synthesisAnticipationDuration,
            0f,
            Mathf.Max(0f, totalScaleDuration - 0.05f));
        float shrinkDuration = Mathf.Max(0.05f, totalScaleDuration - anticipationDuration);
        float transitionHalfDuration = Mathf.Max(0.05f, transitionFlashDuration * 0.5f);

        Sequence sequence = DOTween.Sequence();
        sequence.SetLink(gameObject);
        keyTween = sequence;

        // 1) 缩小前让剩余砖阵逐次增强地真实闪白，不创建全屏覆盖层。
        sequence.Append(DOTween.To(() => flashProgress, value =>
            {
                flashProgress = value;
                float alpha = EvaluateSynthesisFlashAlpha(flashProgress, flashCycles);
                SetOverlaysAlpha(overlays, alpha);
            }, 1f, totalFlashDuration)
            .SetEase(Ease.Linear));
        sequence.AppendCallback(() =>
        {
            overlayAlpha = 0f;
            SetOverlaysAlpha(overlays, 0f);
        });

        // 2) 缩短后的总时长内先向外蓄力，再让尺寸与相对中心间距同步收缩并保留淡白余光。
        if (anticipationDuration > 0f)
        {
            sequence.Append(DOTween.To(() => formationScale, value =>
                {
                    formationScale = value;
                    ApplyFormationScale(remaining, formationCenter, startPositions, startScales, formationScale);
                }, Mathf.Max(1f, synthesisAnticipationScale), anticipationDuration)
                .SetEase(Ease.OutQuad));
            sequence.Join(DOTween.To(() => overlayAlpha, value =>
                {
                    overlayAlpha = value;
                    SetOverlaysAlpha(overlays, overlayAlpha);
                }, Mathf.Clamp01(synthesisShrinkGlowAlpha), anticipationDuration)
                .SetEase(Ease.OutSine));
        }
        else
        {
            sequence.AppendCallback(() =>
            {
                overlayAlpha = Mathf.Clamp01(synthesisShrinkGlowAlpha);
                SetOverlaysAlpha(overlays, overlayAlpha);
            });
        }

        sequence.Append(DOTween.To(() => formationScale, value =>
            {
                formationScale = value;
                ApplyFormationScale(remaining, formationCenter, startPositions, startScales, formationScale);
            }, formationTargetScale, shrinkDuration)
            .SetEase(Ease.InOutSine));
        sequence.AppendCallback(() =>
        {
            overlayAlpha = 0f;
            SetOverlaysAlpha(overlays, 0f);
            ClearOverlays(overlays);
        });

        // 3) 局部白闪峰值处切换为最终 0.75 倍实体钥匙，同时播放星光与短促镜头震动。
        if (transitionFlash != null)
        {
            sequence.Append(transitionFlash
                .DOFade(transitionFlashPeakAlpha, transitionHalfDuration)
                .SetEase(Ease.InOutSine));
            sequence.AppendCallback(() =>
            {
                RevealKeyAtTransitionPeak(formationCenter, remaining, keyStartPosition);
            });
            sequence.Append(transitionFlash
                .DOFade(0f, transitionHalfDuration)
                .SetEase(Ease.InOutSine));
            sequence.AppendCallback(() =>
            {
                if (transitionFlash != null)
                    Destroy(transitionFlash.gameObject);
            });
        }
        else
        {
            sequence.AppendCallback(() =>
            {
                RevealKeyAtTransitionPeak(formationCenter, remaining, keyStartPosition);
            });
        }

        // 4) 实体钥匙在原空中停留阶段内回弹一次，再缓慢落地；落地完成才开放拾取。
        float holdDuration = Mathf.Max(0f, keyHoldDuration);
        float popDuration = keyPopScaleMultiplier > 1f
            ? Mathf.Min(holdDuration, Mathf.Max(0f, keyPopDuration))
            : 0f;
        if (popDuration > 0f)
        {
            float growDuration = popDuration * 0.45f;
            float settleDuration = popDuration - growDuration;
            sequence.Append(keyObject.transform
                .DOScale(Vector3.one * (keyFinalScale * keyPopScaleMultiplier), growDuration)
                .SetEase(Ease.OutQuad));
            sequence.Append(keyObject.transform
                .DOScale(Vector3.one * keyFinalScale, settleDuration)
                .SetEase(Ease.InOutSine));
        }

        if (holdDuration > popDuration)
            sequence.AppendInterval(holdDuration - popDuration);

        sequence.Append(keyObject.transform
            .DOMove(keyLandingPoint.position, keyFallDuration)
            .SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            if (keyPickupCollider != null)
                keyPickupCollider.enabled = true;
        });
    }

    private List<SpriteRenderer> CreateWhiteOverlays(List<Level127Brick> bricks)
    {
        List<SpriteRenderer> overlays = new(bricks.Count);
        foreach (Level127Brick brick in bricks)
        {
            Renderer brickRenderer = brick.GetComponent<Renderer>();
            SpriteRenderer brickSpriteRenderer = brickRenderer as SpriteRenderer;
            if (brickRenderer == null || brickSpriteRenderer == null)
                continue;

            GameObject overlay = new($"Level127BrickFlash_{brick.name}");
            overlay.transform.SetParent(brick.transform, false);
            overlay.transform.localPosition = Vector3.zero;
            overlay.layer = brick.gameObject.layer;

            SpriteRenderer overlayRenderer = overlay.AddComponent<SpriteRenderer>();
            overlayRenderer.sprite = brickSpriteRenderer.sprite;
            overlayRenderer.sharedMaterial = synthesisFlashMaterial;
            overlayRenderer.sortingLayerID = brickSpriteRenderer.sortingLayerID;
            overlayRenderer.sortingOrder = brickSpriteRenderer.sortingOrder + BrickFlashSortingOffset;
            overlayRenderer.color = new Color(1f, 1f, 1f, 0f);
            overlays.Add(overlayRenderer);
        }

        return overlays;
    }

    private static void SetOverlaysAlpha(List<SpriteRenderer> overlays, float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        foreach (SpriteRenderer overlay in overlays)
        {
            if (overlay != null)
                overlay.color = new Color(1f, 1f, 1f, clampedAlpha);
        }
    }

    private float EvaluateSynthesisFlashAlpha(float progress, int flashCycles)
    {
        float scaledProgress = Mathf.Clamp01(progress) * flashCycles;
        int cycleIndex = Mathf.Min(flashCycles - 1, Mathf.FloorToInt(scaledProgress));
        float cycleProgress = scaledProgress - Mathf.Floor(scaledProgress);
        if (progress >= 1f)
            cycleProgress = 1f;

        float peakProgress = flashCycles > 1 ? (float)cycleIndex / (flashCycles - 1) : 1f;
        float peakAlpha = Mathf.Lerp(
            synthesisFlashFirstPeakAlpha,
            synthesisFlashLastPeakAlpha,
            peakProgress);
        return Mathf.Max(0f, Mathf.Sin(cycleProgress * Mathf.PI)) * Mathf.Clamp01(peakAlpha);
    }

    private static void ApplyFormationScale(
        List<Level127Brick> bricks,
        Vector3 formationCenter,
        Vector3[] startPositions,
        Vector3[] startScales,
        float scale)
    {
        for (int index = 0; index < bricks.Count; index++)
        {
            Level127Brick brick = bricks[index];
            if (brick == null)
                continue;

            brick.transform.position = formationCenter + (startPositions[index] - formationCenter) * scale;
            brick.transform.localScale = startScales[index] * scale;
        }
    }

    private static void ClearOverlays(List<SpriteRenderer> overlays)
    {
        foreach (SpriteRenderer overlay in overlays)
        {
            if (overlay != null)
                Destroy(overlay.gameObject);
        }

        overlays.Clear();
    }

    private bool TryGetKeyLocalVisualBounds(out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (keyObject == null)
            return false;

        bool hasBounds = false;
        Matrix4x4 worldToKey = keyObject.transform.worldToLocalMatrix;
        foreach (SpriteRenderer renderer in keyObject.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || renderer.sprite == null)
                continue;

            Bounds spriteBounds = renderer.sprite.bounds;
            Matrix4x4 rendererToKey = worldToKey * renderer.transform.localToWorldMatrix;
            for (int xSign = -1; xSign <= 1; xSign += 2)
            {
                for (int ySign = -1; ySign <= 1; ySign += 2)
                {
                    Vector3 corner = spriteBounds.center + new Vector3(
                        spriteBounds.extents.x * xSign,
                        spriteBounds.extents.y * ySign,
                        0f);
                    Vector3 point = rendererToKey.MultiplyPoint3x4(corner);
                    if (hasBounds)
                        bounds.Encapsulate(point);
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }
        }

        return hasBounds;
    }

    private Vector2 ComputeFinalKeyWorldSize(Bounds keyLocalBounds)
    {
        Vector3 parentScale = keyObject.transform.parent != null
            ? keyObject.transform.parent.lossyScale
            : Vector3.one;
        return new Vector2(
            Mathf.Max(0.01f, keyLocalBounds.size.x * Mathf.Abs(parentScale.x) * keyFinalScale),
            Mathf.Max(0.01f, keyLocalBounds.size.y * Mathf.Abs(parentScale.y) * keyFinalScale));
    }

    /// <summary>以宽高缩放比的几何平均值等比收缩，使两轴尺寸误差尽量对称且保持钥匙轮廓。</summary>
    private static float ComputeFormationTargetScale(Bounds formationBounds, Vector2 finalKeyWorldSize)
    {
        float widthScale = finalKeyWorldSize.x / Mathf.Max(0.01f, formationBounds.size.x);
        float heightScale = finalKeyWorldSize.y / Mathf.Max(0.01f, formationBounds.size.y);
        return Mathf.Max(0.01f, Mathf.Sqrt(Mathf.Max(0.0001f, widthScale * heightScale)));
    }

    private Vector3 ComputeKeyRootPositionForVisualCenter(Vector3 visualCenter, Bounds keyLocalBounds)
    {
        Vector3 localOffset = keyLocalBounds.center * keyFinalScale;
        Vector3 worldOffset = keyObject.transform.parent != null
            ? keyObject.transform.parent.TransformVector(localOffset)
            : localOffset;
        return visualCenter - worldOffset;
    }

    private SpriteRenderer CreateKeyTransitionFlash(Vector3 position, Vector2 finalKeyWorldSize)
    {
        SpriteRenderer sourceRenderer = null;
        foreach (SpriteRenderer renderer in keyObject.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer != null && renderer.sprite != null)
            {
                sourceRenderer = renderer;
                break;
            }
        }

        if (sourceRenderer == null)
            return null;

        GameObject flash = new("Level127KeyTransitionFlash");
        flash.transform.SetParent(EnsureEffectRoot(), false);
        flash.transform.position = position;
        flash.layer = keyObject.layer;

        SpriteRenderer flashRenderer = flash.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = sourceRenderer.sprite;
        flashRenderer.sharedMaterial = synthesisFlashMaterial;
        flashRenderer.flipX = sourceRenderer.flipX;
        flashRenderer.flipY = sourceRenderer.flipY;
        flashRenderer.color = new Color(1f, 1f, 1f, 0f);

        SortingGroup keySortingGroup = keyObject.GetComponent<SortingGroup>();
        flashRenderer.sortingLayerID = keySortingGroup != null
            ? keySortingGroup.sortingLayerID
            : sourceRenderer.sortingLayerID;
        flashRenderer.sortingOrder = (keySortingGroup != null
            ? keySortingGroup.sortingOrder
            : sourceRenderer.sortingOrder) + KeyTransitionSortingOffset;

        Vector3 parentScale = flash.transform.parent != null
            ? flash.transform.parent.lossyScale
            : Vector3.one;
        flash.transform.localScale = new Vector3(
            finalKeyWorldSize.x * KeyTransitionFlashScaleMultiplier /
            Mathf.Max(0.01f, sourceRenderer.sprite.bounds.size.x * Mathf.Abs(parentScale.x)),
            finalKeyWorldSize.y * KeyTransitionFlashScaleMultiplier /
            Mathf.Max(0.01f, sourceRenderer.sprite.bounds.size.y * Mathf.Abs(parentScale.y)),
            1f);
        return flashRenderer;
    }

    private void RevealKeyAtTransitionPeak(
        Vector3 formationCenter,
        List<Level127Brick> remaining,
        Vector3 keyStartPosition)
    {
        PlaySynthesisImpact(formationCenter, remaining);
        SwapFormationForKey(remaining, keyStartPosition);

        // 合成确认音与砖阵切换为实体钥匙严格同帧触发，不在前置闪白阶段播放。
        if (keyObject != null)
            AudioManager.PlaySound(synthesisSfxTag);
    }

    private void PlaySynthesisImpact(Vector3 position, List<Level127Brick> remaining)
    {
        PlaySynthesisBurst(position, remaining);
        PlaySynthesisCameraShake();
    }

    private void PlaySynthesisBurst(Vector3 position, List<Level127Brick> remaining)
    {
        if (synthesisBurstMaterial == null)
            return;

        GameObject burstObject = new("Level127KeySynthesisBurst");
        burstObject.SetActive(false);
        burstObject.transform.SetParent(EnsureEffectRoot(), false);
        burstObject.transform.position = position;
        burstObject.layer = gameObject.layer;

        ParticleSystem burstSystem = burstObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = burstSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = OrderedCurve(synthesisBurstLifetimeRange, 0.01f);
        main.startSpeed = OrderedCurve(synthesisBurstSpeedRange, 0f);
        main.startSize = OrderedCurve(synthesisBurstSizeRange, 0.01f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(synthesisBurstColor);
        main.maxParticles = Mathf.Max(1, synthesisBurstCount);
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = burstSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        short burstCount = (short)Mathf.Clamp(synthesisBurstCount, 1, short.MaxValue);
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });

        ParticleSystem.ShapeModule shape = burstSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0f, synthesisBurstRadius);

        Gradient fadeGradient = new();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.55f),
                new GradientAlphaKey(0f, 1f),
            });
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = burstSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGradient);

        AnimationCurve sizeCurve = new(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.18f, 1f),
            new Keyframe(1f, 0f));
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = burstSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer burstRenderer = burstObject.GetComponent<ParticleSystemRenderer>();
        if (burstRenderer != null)
        {
            ResolveSynthesisSorting(remaining, out int sortingLayerId, out int sortingOrder);
            burstRenderer.sharedMaterial = synthesisBurstMaterial;
            burstRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            burstRenderer.sortingLayerID = sortingLayerId;
            burstRenderer.sortingOrder = sortingOrder;
        }

        burstObject.SetActive(true);
        burstSystem.Play(true);
    }

    private static ParticleSystem.MinMaxCurve OrderedCurve(Vector2 range, float minimum)
    {
        float min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return new ParticleSystem.MinMaxCurve(min, max);
    }

    private void PlaySynthesisCameraShake()
    {
        ResolveCamera();
        if (worldCamera == null || synthesisCameraShakeStrength <= 0f)
            return;

        if (synthesisCameraShakeTween != null && synthesisCameraShakeTween.IsActive())
            synthesisCameraShakeTween.Kill();

        synthesisCameraShakeTarget = worldCamera.transform;
        synthesisCameraInitialLocalPosition = synthesisCameraShakeTarget.localPosition;
        synthesisCameraShakeTween = synthesisCameraShakeTarget
            .DOShakePosition(
                Mathf.Max(0.01f, synthesisCameraShakeDuration),
                synthesisCameraShakeStrength,
                Mathf.Max(1, synthesisCameraShakeVibrato),
                90f,
                false,
                true)
            .SetLink(gameObject)
            .OnComplete(RestoreSynthesisCameraPosition)
            .OnKill(RestoreSynthesisCameraPosition);
    }

    private void RestoreSynthesisCameraPosition()
    {
        if (synthesisCameraShakeTarget != null)
            synthesisCameraShakeTarget.localPosition = synthesisCameraInitialLocalPosition;

        synthesisCameraShakeTween = null;
        synthesisCameraShakeTarget = null;
    }

    private void SwapFormationForKey(List<Level127Brick> remaining, Vector3 keyStartPosition)
    {
        foreach (Level127Brick brick in remaining)
        {
            if (brick != null)
                brick.gameObject.SetActive(false);
        }

        if (keyObject == null)
            return;

        keyObject.transform.position = keyStartPosition;
        keyObject.transform.localScale = Vector3.one * keyFinalScale;
        if (keyPickupCollider != null)
            keyPickupCollider.enabled = false;
        if (keyFloatHandle != null)
        {
            keyFloatHandle.StopMove();
            keyFloatHandle.enabled = false;
        }

        keyObject.SetActive(true);
    }

    private void PlaceKeyAtLanding()
    {
        if (keyObject == null || keyLandingPoint == null)
            return;

        if (keyFloatHandle != null)
        {
            keyFloatHandle.StopMove();
            keyFloatHandle.enabled = false;
        }

        keyObject.transform.position = keyLandingPoint.position;
        keyObject.transform.localScale = Vector3.one * keyFinalScale;
        keyObject.SetActive(true);
        if (keyPickupCollider != null)
            keyPickupCollider.enabled = true;
    }

    private void ConfigureKeyPickupCollider()
    {
        if (TryGetKeyLocalVisualBounds(out Bounds keyLocalBounds))
            ConfigureKeyPickupCollider(keyLocalBounds);
    }

    private void ConfigureKeyPickupCollider(Bounds keyLocalBounds)
    {
        if (keyPickupCollider is not BoxCollider2D boxCollider)
            return;

        boxCollider.offset = new Vector2(keyLocalBounds.center.x, keyLocalBounds.center.y);
        boxCollider.size = new Vector2(
            Mathf.Max(0.01f, keyLocalBounds.size.x * keyPickupColliderScale),
            Mathf.Max(0.01f, keyLocalBounds.size.y * keyPickupColliderScale));
    }

    #endregion

    #region Hidden Item Drops

    private void SpawnReplacementPropDrop(Level127Brick brick)
    {
        if (hiddenPropByBrick.TryGetValue(brick, out Sprite assignedProp) && assignedProp != null)
        {
            SpawnPropDrop(brick, assignedProp);
            return;
        }

        List<Sprite> props = CollectPropSprites();
        if (props.Count == 0)
        {
            this.LogWarning($"[Level127Ctrl] 星位 {brick.name} 已收集，但没有可用普通道具可替代。");
            return;
        }

        SpawnPropDrop(brick, props[UnityEngine.Random.Range(0, props.Count)]);
    }

    private void SpawnStarDrop(Level127Brick brick, GameObject starTemplate)
    {
        if (starTemplate == null)
        {
            this.LogWarning($"[Level127Ctrl] 砖块 {brick.name} 的固定星星模板为空。");
            return;
        }

        Vector3 position = brick.Center;
        GameObject star = Instantiate(starTemplate, EnsureDropRoot());
        star.name = string.Format(StarDropNameFormat, brick.name);
        star.transform.position = position;
        // 星星直接复制标准 Postcard_Star 模板，不经过普通隐藏物的尺寸归一化或 1.5 倍放大。
        ConfigureDropSorting(star, brick);

        MoveFloatHandle floatHandle = star.GetComponent<MoveFloatHandle>();
        if (floatHandle != null)
        {
            floatHandle.StopMove();
            floatHandle.enabled = false;
        }

        star.SetActive(true);
        Postcard_Star postcardStar = star.GetComponent<Postcard_Star>();
        if (postcardStar != null && !postcardStar.RefreshSavedCollectionState())
            return;

        float halfHeight = ResolveVisualHalfHeight(star, 0.4f);
        // 星星保持 Trigger 开启，抛出过程中即可收集；只有落地回调才恢复标准上下浮动。
        PopDropAlongParabola(star, position, halfHeight, null, floatHandle, false);
    }

    private void SpawnPropDrop(Level127Brick brick, Sprite propSprite)
    {
        Vector3 position = brick.Center;
        GameObject drop = new($"Level127PropDrop_{propSprite.name}");
        drop.transform.SetParent(EnsureDropRoot(), false);
        drop.transform.position = position;
        int dropLayer = LayerMask.NameToLayer(PropDropLayerName);
        if (dropLayer >= 0)
            drop.layer = dropLayer;
        ConfigureDropSorting(drop, brick);

        SpriteRenderer renderer = drop.AddComponent<SpriteRenderer>();
        renderer.sprite = propSprite;

        // 道具视觉与碰撞体统一按隐藏物倍率放大；落地前不参与物理，落地后转动态刚体。
        float maxSize = Mathf.Max(propSprite.bounds.size.x, propSprite.bounds.size.y);
        float scale = EffectiveDropMaxSize / Mathf.Max(0.01f, maxSize);
        drop.transform.localScale = Vector3.one * scale;

        Rigidbody2D body = drop.AddComponent<Rigidbody2D>();
        body.mass = 0.5f;
        body.gravityScale = 1f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.simulated = false;

        BoxCollider2D box = drop.AddComponent<BoxCollider2D>();
        box.size = propSprite.bounds.size;
        box.enabled = false;
        propDropColliders.Add(box);
        ExcludePropCollisionWithActor(box);

        float halfHeight = propSprite.bounds.extents.y * scale;
        PopDropAlongParabola(drop, position, halfHeight, box);
    }

    private static void ConfigureDropSorting(GameObject drop, Level127Brick brick)
    {
        ResolveBrickSorting(brick, out int sortingLayerId, out int sortingOrder);
        SortingGroup sortingGroup = drop.GetComponent<SortingGroup>();
        if (sortingGroup == null)
            sortingGroup = drop.AddComponent<SortingGroup>();
        sortingGroup.sortingLayerID = sortingLayerId;
        sortingGroup.sortingOrder = sortingOrder + DropSortingOffset;
    }

    private void ExcludePropCollisionWithActor(BoxCollider2D propCollider)
    {
        if (propCollider == null)
            return;

        int actorLayer = LayerMask.NameToLayer(ActorLayerName);
        if (actorLayer >= 0)
        {
            LayerMask excludedLayers = propCollider.excludeLayers;
            excludedLayers.value |= 1 << actorLayer;
            propCollider.excludeLayers = excludedLayers;
        }

        if (actor == null)
            return;

        if (!propCollider.enabled || !propCollider.gameObject.activeInHierarchy)
            return;

        foreach (Collider2D actorCollider in actor.GetComponentsInChildren<Collider2D>(true))
        {
            if (actorCollider != null && actorCollider.enabled && actorCollider.gameObject.activeInHierarchy)
                Physics2D.IgnoreCollision(propCollider, actorCollider, true);
        }
    }

    /// <summary>只忽略本控制器创建的普通道具之间的碰撞，不修改共享层矩阵或星星/钥匙 Trigger。</summary>
    private void ExcludePropCollisionWithOtherDrops(BoxCollider2D propCollider)
    {
        foreach (BoxCollider2D other in propDropColliders)
        {
            if (other == null || other == propCollider || !other.enabled ||
                !other.gameObject.activeInHierarchy || !other.attachedRigidbody.simulated)
                continue;

            Physics2D.IgnoreCollision(propCollider, other, true);
        }
    }

    private static void ResolveBrickSorting(Level127Brick brick, out int sortingLayerId, out int sortingOrder)
    {
        SpriteRenderer brickRenderer = brick != null ? brick.GetComponent<SpriteRenderer>() : null;
        sortingLayerId = brickRenderer != null
            ? brickRenderer.sortingLayerID
            : SortingLayer.NameToID("SceneLayer");
        sortingOrder = brickRenderer != null ? brickRenderer.sortingOrder : 90;
    }

    private void ResolveSynthesisSorting(
        List<Level127Brick> remaining,
        out int sortingLayerId,
        out int sortingOrder)
    {
        SortingGroup keySortingGroup = keyObject != null ? keyObject.GetComponent<SortingGroup>() : null;
        if (keySortingGroup != null)
        {
            sortingLayerId = keySortingGroup.sortingLayerID;
            sortingOrder = keySortingGroup.sortingOrder + SynthesisBurstSortingOffset;
            return;
        }

        ResolveBrickSorting(
            remaining != null && remaining.Count > 0 ? remaining[0] : null,
            out sortingLayerId,
            out sortingOrder);
        sortingOrder += SynthesisBurstSortingOffset;
    }

    /// <summary>小抛物线弹出：从砖中心在砖墙前景飞向地面；落地后恢复对应的物理或浮动行为。</summary>
    private void PopDropAlongParabola(
        GameObject drop,
        Vector3 startPosition,
        float halfHeight,
        Collider2D landingCollider = null,
        MoveFloatHandle floatHandle = null,
        bool randomizeLandingX = true)
    {
        // 普通道具飞行期间只做视觉演出；星星不传入 landingCollider，保留其 Trigger 拾取窗口。
        if (landingCollider != null)
            landingCollider.enabled = false;

        if (floatHandle != null)
        {
            floatHandle.StopMove();
            floatHandle.enabled = false;
        }

        Vector3 landingPosition = ComputeDropLandingPosition(startPosition, halfHeight, randomizeLandingX);
        float duration = UnityEngine.Random.Range(dropPopDurationRange.x, dropPopDurationRange.y);
        float progress = 0f;
        DOTween.To(() => progress, value =>
            {
                progress = value;
                if (drop == null)
                    return;

                float arc = 4f * dropPopArcHeight * progress * (1f - progress);
                drop.transform.position = new Vector3(
                    Mathf.Lerp(startPosition.x, landingPosition.x, progress),
                    Mathf.Lerp(startPosition.y, landingPosition.y, progress) + arc,
                    startPosition.z);
            }, 1f, duration)
            .SetEase(Ease.Linear)
            .SetLink(drop, LinkBehaviour.KillOnDisable)
            .OnComplete(() =>
            {
                if (drop == null)
                    return;

                drop.transform.position = landingPosition;
                Rigidbody2D body = drop.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.position = new Vector2(landingPosition.x, landingPosition.y);
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }

                if (landingCollider != null)
                    landingCollider.enabled = true;

                if (body != null)
                    body.simulated = true;

                // 两侧 Collider 与刚体启用后、下次物理步之前建立忽略；后落地者负责补齐碰撞对。
                if (landingCollider is BoxCollider2D propCollider)
                {
                    ExcludePropCollisionWithActor(propCollider);
                    ExcludePropCollisionWithOtherDrops(propCollider);
                }

                if (floatHandle != null && drop.activeInHierarchy)
                    floatHandle.enabled = true;
            });
    }

    /// <summary>从砖中心竖直向下只探测 Ground 地面；飞行期忽略下方砖块碰撞。</summary>
    private Vector3 ComputeDropLandingPosition(Vector3 startPosition, float halfHeight, bool randomizeLandingX = true)
    {
        RaycastHit2D hit = Physics2D.Raycast(startPosition, Vector2.down, 20f, DropLandingMask);
        float landingY = hit.collider != null ? hit.point.y + halfHeight : startPosition.y - 2.5f;
        float landingX = randomizeLandingX
            ? startPosition.x + UnityEngine.Random.Range(-dropLandingSpreadX, dropLandingSpreadX)
            : startPosition.x;
        return new Vector3(landingX, landingY, startPosition.z);
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            if (child.name == childName)
                return child;

            Transform result = FindChildByName(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    #endregion

    #region Common

    private Transform EnsureDropRoot()
    {
        if (dropRoot == null)
        {
            GameObject root = new("Level127Drops");
            root.transform.SetParent(ResolveLevelRoot(), false);
            dropRoot = root.transform;
        }

        return dropRoot;
    }

    private Transform EnsureEffectRoot()
    {
        if (effectRoot == null)
        {
            GameObject root = new("Level127Effects");
            root.transform.SetParent(ResolveLevelRoot(), false);
            effectRoot = root.transform;
        }

        return effectRoot;
    }

    /// <summary>关卡根节点：向上找 BaseLevelCtrl 所在节点，兜底 transform.root。</summary>
    private Transform ResolveLevelRoot()
    {
        BaseLevelCtrl levelCtrl = GetComponentInParent<BaseLevelCtrl>(true);
        if (levelCtrl != null)
            return levelCtrl.transform;

        return transform.root;
    }

    private float EffectiveHiddenItemScaleMultiplier => Mathf.Max(0.1f, hiddenItemScaleMultiplier);

    private float EffectiveDropMaxSize => dropMaxSize * EffectiveHiddenItemScaleMultiplier;

    private static float ResolveVisualHalfHeight(GameObject visual, float fallback)
    {
        if (visual == null)
            return fallback;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return fallback;

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        return bounds.size.y > 0.01f ? bounds.extents.y : fallback;
    }

    private void ResolveCamera()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private bool TryGetTapScreenPosition(out Vector2 screenPosition)
    {
        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.phase != TouchPhase.Began || InputUtils.IsPointerOverUI(touch.position))
                continue;

            screenPosition = touch.position;
            return true;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0) && !InputUtils.IsPointerOverUI(Input.mousePosition))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = Vector2.zero;
        return false;
    }

    private void LateUpdate()
    {
        TickSynthesis();
    }

    private static bool IsGameplayPlaying()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        return playCtrl != null &&
               playCtrl.IsInitialized &&
               playCtrl.IsActive &&
               playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    #endregion
}
