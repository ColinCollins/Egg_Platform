using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using DG.Tweening;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// versio 2.0.0
/// 参考 Level52Ctrl 控制 Fire 的脚本，当前脚本需要增加对应的控制功能。触发火焰为止。不需要与冰块交互的逻辑。（已添加）
/// Level74GrassCtrl 新增与 fire 交互逻辑。Fire 小火苗拖拽触碰后，Instantiate 一个 fire 火焰Gameobject 到当前 grass 位置。
/// 持续 1s，然后 grass 进入烧焦状态，leave color 变为黑色，然后渐隐 0.5f，然后重新生长。
/// 
/// version 3.0
/// 补充逻辑 bubble 控制，3s 后 scale 变为 0. 点击 farmer 触发 dig 之后， click 点击能力不要取消，变为显示 bubble scale DOScale 变为 1， 然后 3s 后 scale 变为 0
/// </summary>
public class Level74Ctrl : MonoBehaviour, IDebuger
{
    private const int ToolEquippedSortingOrder = 300;
    private const int ToolUnequippedSortingOrder = 150;

    private enum ToolType
    {
        None,
        Eradicator,
        Mow,
        Hoe
    }

    [System.Serializable]
    private class ToolBinding
    {
        public ToolType toolType = ToolType.None;
        public Transform tool = null;
        public ClickItemEventHandle clickHandle = null;
    }

    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private ActorSpineCtrl doorSpineCtrl;
    [SerializeField] private Level74ActorCtrl actorCtrl;
    [SerializeField] private Transform handBone;
    [SerializeField] private List<Level74GrassCtrl> grasses = new List<Level74GrassCtrl>();
    [SerializeField] private List<ToolBinding> tools = new List<ToolBinding>();
    [SerializeField] private Transform farmer;
    [SerializeField] private ClickItemEventHandle hoeClick;
    [SerializeField] private ActorSpineCtrl farmerSpineCtrl;
    [Header("Farmer Bubble")]
    [SerializeField] private Transform farmerBubble; // Farmer 释放 Hoe 后再次点击显示的本地提示气泡根节点，仅做缩放显隐，不参与碰撞、存档或完成判定。
    [SerializeField, Min(0f)] private float farmerBubbleShowDuration = 3f; // 气泡保持显示的时长，结束后缩回到 0；仅影响 Level74 本地表现。
    [SerializeField, Min(0f)] private float farmerBubbleScaleDuration = 0.2f; // 气泡放大/缩回动画时长，重置、失败、成功和销毁时会被强制清理。
    [SerializeField] private float toolMoveDuration = 0.35f;
    [SerializeField] private float toolJumpPower = 0.7f;
    [SerializeField] private string farmerIdleAnimName = "idle";

    [Header("Fire")]
    [SerializeField] private Level74FireCtrl fireCtrl;
    [SerializeField] private Collider2D flashCollider;
    [SerializeField] private Collider2D fireCollider;
    [SerializeField] private GameObject grassFireVisualTemplate;

    [SerializeField] private GameObject star; // 第 3 棵草开始重生时按概率显示的关卡本地星星；仅控制显隐，不参与存档、配置或通关判定。
    [SerializeField] private float generateProbability = 0.5f; // 目标草每次开始重生时显示星星的概率，按 0~1 配置；当前值由 Inspector 序列化数据决定。

    private readonly Dictionary<ToolType, ToolRuntime> toolRuntimes = new Dictionary<ToolType, ToolRuntime>();
    private EventSubscriber subscriber;
    private Level74GamePlayPanelCtrl panelCtrl;
    private ToolType activeTool = ToolType.None;
    private Level74GrassCtrl selectedGrass;
    private Level74GrassCtrl panelStateGrass;
    private ToolType panelStateTool = ToolType.None;
    private bool panelStateCanOperate;
    private bool hasPanelState;
    private bool hoeReleased;
    private bool interactionLocked;
    private bool isDone;
    private bool isCleaningUp;
    private bool hasGeneratedStar;
    private Sequence farmerBubbleSequence;
    private Vector3 farmerBubbleInitialScale = Vector3.one;
    private bool hasFarmerBubbleInitialScale;

    private class ToolRuntime
    {
        public ToolBinding Binding;
        public Transform InitialParent;
        public Vector3 InitialLocalPosition;
        public Vector3 InitialLocalScale;
        public Quaternion InitialLocalRotation;
    }

    private void Awake()
    {
        CacheFarmerBubbleInitialScale();
        CacheTools();
        ResetRuntimeState();

        ShowFarmerBubble();
    }

    private void OnEnable()
    {
        BindRuntimeEvents();
    }

    private void Start()
    {
        ResolvePanel();
        BindPanel();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        RefreshSelectedGrassPanelState(false);
    }

    public void ClickTool(Transform clicker, Vector2 worldPoint)
    {
        ToolType toolType = ResolveToolType(clicker);
        SelectTool(toolType);
    }

    public void ClickEradicator(Transform clicker, Vector2 worldPoint)
    {
        SelectTool(ToolType.Eradicator);
    }

    public void ClickMow(Transform clicker, Vector2 worldPoint)
    {
        SelectTool(ToolType.Mow);
    }

    public void ClickHoe(Transform clicker, Vector2 worldPoint)
    {
        SelectTool(ToolType.Hoe);
    }

    public void ClickHoeTool(Transform clicker, Vector2 worldPoint)
    {
        if (interactionLocked || !IsActorEligible())
            return;

        hoeReleased = true;
        hoeClick?.SetEnableClick(false);
        hoeClick?.ResetClickCount();
        farmerSpineCtrl?.PlayAnimation(farmerIdleAnimName, true, false);
        SetToolAvailable(ToolType.Hoe, true);
        SelectTool(ToolType.Hoe);
    }

    public void ClickFarmer(Transform clicker, Vector2 worldPoint)
    {
        if (hoeReleased)
        {
            ShowFarmerBubble();
            return;
        }
    }

    public void OnGrassEnter(Collider2D collider)
    {
        Level74GrassCtrl grass = ResolveGrass(collider);
        OnGrassEnter(grass, collider);
    }

    public void OnGrassEnter(Level74GrassCtrl grass, Collider2D collider)
    {
        if (IsActiveFireCollider(collider))
        {
            BurnSelectedGrass(grass, collider);
            return;
        }

        if (grass == null || grass.IsDead) // IsActorEligible()
            return;

        switch (activeTool)
        {
            case ToolType.None:
                HideSelectedGrass(grass);
                break;
            case ToolType.Eradicator:
                TryAutoCut(grass);
                break;
            case ToolType.Mow:
                SelectGrassForPanel(grass);
                break;
            case ToolType.Hoe:
                SelectGrassForPanel(grass);
                break;
        }
    }

    public void OnGrassExit(Collider2D collider)
    {
        Level74GrassCtrl grass = ResolveGrass(collider);
        OnGrassExit(grass, collider);
    }

    public void OnGrassExit(Level74GrassCtrl grass, Collider2D collider)
    {
        HideSelectedGrass(grass);
    }

    /// <summary>
    /// 草开始重生时的关卡内回调；仅列表索引 2 的草按 generateProbability 参与随机判定，命中后显示已绑定的星星。
    /// </summary>
    public void OnGrassRegrown(Level74GrassCtrl grass)
    {
        bool isTargetGrass = grasses != null && grasses.Count > 2 && grasses[2] == grass;
        this.Log($"[Level74Ctrl] OnGrassRegrown executed. grass={(grass != null ? grass.name : "null")}, isTargetGrass={isTargetGrass}, starBound={star != null}, hasGeneratedStar={hasGeneratedStar}, generateProbability={generateProbability}");

        if (!isTargetGrass || star == null || hasGeneratedStar)
            return;

        if (Random.value < generateProbability)
        {
            hasGeneratedStar = true;
            star.SetActive(true);
        }
    }

    private void SelectTool(ToolType toolType)
    {
        if (toolType == ToolType.None || activeTool == toolType)
            return;

        if (interactionLocked || !IsActorEligible())
            return;

        if (toolType == ToolType.Hoe && !hoeReleased)
            return;

        if (!toolRuntimes.ContainsKey(toolType))
            return;

        HideSelectedGrass(null);
        interactionLocked = true;

        ToolType previousTool = activeTool;
        activeTool = toolType;
        actorCtrl.SetToolState(ToActorToolState(toolType));

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        if (previousTool != ToolType.None)
            AppendReturnTool(sequence, previousTool);

        AppendEquipTool(sequence, toolType);
        sequence.OnComplete(() => interactionLocked = false);

        AudioManager.PlaySound("button");
    }

    private void TryAutoCut(Level74GrassCtrl grass)
    {
        if (interactionLocked || grass == null || !grass.CanCut())
            return;

        if (!grass.TryCut(() => interactionLocked = false))
            interactionLocked = false;
    }

    private void SelectGrassForPanel(Level74GrassCtrl grass)
    {
        if (interactionLocked || panelCtrl == null || grass == null)
            return;

        selectedGrass = grass;
        RefreshSelectedGrassPanelState(true);
    }

    private void BurnSelectedGrass(Level74GrassCtrl grass, Collider2D collider)
    {
        if (!CanAcceptFireContact(grass))
            return;

        HideSelectedGrass(null);
        grass.TryBurn(ResolveGrassFireVisualTemplate());
    }

    private void OnCutClicked()
    {
        if (selectedGrass == null || activeTool != ToolType.Mow || interactionLocked || !IsActorEligible())
            return;

        Level74GrassCtrl grass = selectedGrass;
        if (!grass.CanCut())
            return;

        interactionLocked = true;
        panelCtrl?.SetInteractable(false);
        SetToolVisible(activeTool, false);

        if (!actorCtrl.ExecuteCurrentTool(() =>
        {
            if (!grass.TryCut(OnOperationComplete))
                OnOperationComplete();
        }))
        {
            OnOperationComplete();
        }
    }

    private void OnDigClicked()
    {
        if (selectedGrass == null || activeTool != ToolType.Hoe || interactionLocked || !IsActorEligible())
            return;

        Level74GrassCtrl grass = selectedGrass;
        if (!grass.CanDig())
            return;

        interactionLocked = true;
        panelCtrl?.SetInteractable(false);
        SetToolVisible(activeTool, false);

        if (!actorCtrl.ExecuteCurrentTool(() =>
        {
            if (!grass.TryDig(() =>
            {
                CheckDoorOpen();
                OnOperationComplete();
            }))
            {
                OnOperationComplete();
            }
        }))
        {
            OnOperationComplete();
        }
    }

    private void OnOperationComplete()
    {
        SetToolVisible(activeTool, true);
        interactionLocked = false;
        actorCtrl?.FinishLevel74Operation();

        panelCtrl?.SetInteractable(true);
        if (selectedGrass == null || selectedGrass.IsDead)
            HideSelectedGrassAfterOperation(selectedGrass);
        else
            RefreshSelectedGrassPanelState(true);
    }

    #region 门相关
    private void CheckDoorOpen()
    {
        if (isDone || grasses == null || grasses.Count == 0)
            return;

        for (int i = 0; i < grasses.Count; i++)
        {
            if (grasses[i] != null && !grasses[i].IsDead)
                return;
        }

        OpenDoor();
    }

    private void OpenDoor()
    {
        isDone = true;

        if (doorCollider != null)
            doorCollider.enabled = true;

        doorSpineCtrl?.PlayAnimation("door_open", false);
        AudioManager.PlaySound("openDoor");
    }

    #endregion

    private void CacheTools()
    {
        toolRuntimes.Clear();

        if (tools == null)
            return;

        for (int i = 0; i < tools.Count; i++)
        {
            ToolBinding binding = tools[i];
            if (binding == null || binding.tool == null || binding.toolType == ToolType.None)
                continue;

            if (binding.clickHandle == null)
                binding.clickHandle = binding.tool.GetComponent<ClickItemEventHandle>();

            toolRuntimes[binding.toolType] = new ToolRuntime
            {
                Binding = binding,
                InitialParent = binding.tool.parent,
                InitialLocalPosition = binding.tool.localPosition,
                InitialLocalScale = binding.tool.localScale,
                InitialLocalRotation = binding.tool.localRotation
            };
        }
    }

    private void ResetRuntimeState()
    {
        isCleaningUp = false;
        activeTool = ToolType.None;
        selectedGrass = null;
        ResetPanelRefreshState();
        interactionLocked = false;
        isDone = false;
        hoeReleased = false;

        if (doorCollider != null)
            doorCollider.enabled = false;

        RestoreAllTools();
        SetToolAvailable(ToolType.Hoe, false);
        hoeClick?.SetEnableClick(true);
        hoeClick?.ResetClickCount();
        ResetFarmerBubbleState();
        panelCtrl?.HideAll();
        ResetFireState();

        if (grasses != null)
        {
            for (int i = 0; i < grasses.Count; i++)
                grasses[i]?.ResetRuntimeState();
        }
    }

    private void RestoreAllTools()
    {
        foreach (KeyValuePair<ToolType, ToolRuntime> pair in toolRuntimes)
            RestoreTool(pair.Value, true);
    }

    private void RestoreTool(ToolRuntime runtime, bool active)
    {
        if (runtime?.Binding?.tool == null)
            return;

        Transform tool = runtime.Binding.tool;
        tool.DOKill();
        tool.SetParent(runtime.InitialParent);
        tool.localPosition = runtime.InitialLocalPosition;
        tool.localRotation = runtime.InitialLocalRotation;
        tool.localScale = runtime.InitialLocalScale;
        SetToolSortingOrder(tool, ToolUnequippedSortingOrder);
        tool.gameObject.SetActive(active);
    }

    private void AppendReturnTool(Sequence sequence, ToolType toolType)
    {
        if (!toolRuntimes.TryGetValue(toolType, out ToolRuntime runtime) || runtime.Binding.tool == null)
            return;

        Transform tool = runtime.Binding.tool;
        tool.SetParent(runtime.InitialParent);
        SetToolSortingOrder(tool, ToolUnequippedSortingOrder);
        tool.gameObject.SetActive(true);
        sequence.Join(tool.DOLocalJump(runtime.InitialLocalPosition, toolJumpPower, 1, toolMoveDuration));
        sequence.Join(tool.DOLocalRotateQuaternion(runtime.InitialLocalRotation, toolMoveDuration));
        sequence.Join(tool.DOScale(runtime.InitialLocalScale, toolMoveDuration));
    }

    private void AppendEquipTool(Sequence sequence, ToolType toolType)
    {
        if (!toolRuntimes.TryGetValue(toolType, out ToolRuntime runtime) || runtime.Binding.tool == null || handBone == null)
            return;

        Transform tool = runtime.Binding.tool;
        if (toolType == ToolType.Eradicator)
        {
            tool.SetParent(runtime.InitialParent);
            tool.localPosition = runtime.InitialLocalPosition;
            tool.localRotation = runtime.InitialLocalRotation;
            tool.localScale = runtime.InitialLocalScale;
            SetToolSortingOrder(tool, ToolUnequippedSortingOrder);
            tool.gameObject.SetActive(false);
            return;
        }

        tool.gameObject.SetActive(true);
        tool.SetParent(handBone);
        SetToolSortingOrder(tool, ToolEquippedSortingOrder);
        sequence.Join(tool.DOLocalJump(Vector3.zero, toolJumpPower, 1, toolMoveDuration));
        sequence.Join(tool.DOLocalRotate(Vector3.zero, toolMoveDuration));
        sequence.Join(tool.DOScale(runtime.InitialLocalScale, toolMoveDuration));
    }

    private void SetToolSortingOrder(Transform tool, int sortingOrder)
    {
        if (tool == null)
            return;

        SpriteRenderer[] spriteRenderers = tool.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].sortingOrder = sortingOrder;
        }

        SortingGroup[] sortingGroups = tool.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < sortingGroups.Length; i++)
        {
            if (sortingGroups[i] != null)
                sortingGroups[i].sortingOrder = sortingOrder;
        }
    }

    private void SetToolVisible(ToolType toolType, bool visible)
    {
        if (!toolRuntimes.TryGetValue(toolType, out ToolRuntime runtime) || runtime.Binding.tool == null)
            return;

        runtime.Binding.tool.gameObject.SetActive(visible);
    }

    private void SetToolAvailable(ToolType toolType, bool available)
    {
        if (!toolRuntimes.TryGetValue(toolType, out ToolRuntime runtime) || runtime.Binding.tool == null)
            return;

        runtime.Binding.tool.gameObject.SetActive(available);
        runtime.Binding.clickHandle?.SetEnableClick(available);
        runtime.Binding.clickHandle?.ResetClickCount();
    }

    private ToolType ResolveToolType(Transform clicker)
    {
        if (clicker == null)
            return ToolType.None;

        foreach (KeyValuePair<ToolType, ToolRuntime> pair in toolRuntimes)
        {
            Transform tool = pair.Value.Binding.tool;
            if (tool != null && (clicker == tool || clicker.IsChildOf(tool)))
                return pair.Key;
        }

        return ToolType.None;
    }

    private Level74GrassCtrl ResolveGrass(Collider2D collider)
    {
        if (collider == null)
            return null;

        Level74GrassCtrl grass = collider.GetComponent<Level74GrassCtrl>();
        if (grass != null)
            return grass;

        return collider.GetComponentInParent<Level74GrassCtrl>();
    }

    private void HideSelectedGrass(Level74GrassCtrl grass)
    {
        if (grass != null && selectedGrass != grass)
            return;

        selectedGrass = null;
        ResetPanelRefreshState();
        panelCtrl?.HideAll();
    }

    private void HideSelectedGrassAfterOperation(Level74GrassCtrl grass)
    {
        if (grass != null && selectedGrass != grass)
            return;

        selectedGrass = null;
        ResetPanelRefreshState();
        panelCtrl?.HideCurrentWithScaleTween();
    }

    private void RefreshSelectedGrassPanelState(bool force)
    {
        if (panelCtrl == null || selectedGrass == null || interactionLocked)
            return;

        if (selectedGrass.IsDead)
        {
            HideSelectedGrass(selectedGrass);
            return;
        }

        if (activeTool != ToolType.Mow && activeTool != ToolType.Hoe)
            return;

        bool canOperate = activeTool == ToolType.Mow ? selectedGrass.CanCut() : selectedGrass.CanDig();
        if (!force
            && hasPanelState
            && panelStateGrass == selectedGrass
            && panelStateTool == activeTool
            && panelStateCanOperate == canOperate)
        {
            return;
        }

        panelStateGrass = selectedGrass;
        panelStateTool = activeTool;
        panelStateCanOperate = canOperate;
        hasPanelState = true;

        if (activeTool == ToolType.Mow && !canOperate)
        {
            panelCtrl.HideCutWithScaleTween();
            return;
        }

        if (activeTool == ToolType.Mow)
            panelCtrl.ShowCut(canOperate);
        else
            panelCtrl.ShowDig(canOperate);
    }

    private void ResetPanelRefreshState()
    {
        panelStateGrass = null;
        panelStateTool = ToolType.None;
        panelStateCanOperate = false;
        hasPanelState = false;
    }

    #region Farmer Bubble

    private void CacheFarmerBubbleInitialScale()
    {
        if (hasFarmerBubbleInitialScale || farmerBubble == null)
            return;

        farmerBubbleInitialScale = farmerBubble.localScale.sqrMagnitude > 0.0001f
            ? farmerBubble.localScale
            : Vector3.one;
        hasFarmerBubbleInitialScale = true;
    }

    private Vector3 GetFarmerBubbleTargetScale()
    {
        CacheFarmerBubbleInitialScale();
        return hasFarmerBubbleInitialScale ? farmerBubbleInitialScale : Vector3.one;
    }

    private void ShowFarmerBubble()
    {
        if (farmerBubble == null)
            return;

        CacheFarmerBubbleInitialScale();
        farmerBubbleSequence?.Kill(false);
        farmerBubble.gameObject.SetActive(true);
        farmerBubble.localScale = Vector3.zero;

        farmerBubbleSequence = DOTween.Sequence().SetLink(gameObject);
        farmerBubbleSequence.Append(farmerBubble
            .DOScale(GetFarmerBubbleTargetScale(), Mathf.Max(0f, farmerBubbleScaleDuration))
            .SetEase(Ease.OutBack));
        farmerBubbleSequence.AppendInterval(Mathf.Max(0f, farmerBubbleShowDuration));
        farmerBubbleSequence.Append(farmerBubble.DOScale(Vector3.zero, Mathf.Max(0f, farmerBubbleScaleDuration)));
        farmerBubbleSequence.OnComplete(() =>
        {
            farmerBubble.gameObject.SetActive(false);
            farmerBubbleSequence = null;
        });
        farmerBubbleSequence.OnKill(() => farmerBubbleSequence = null);
    }

    private void ResetFarmerBubbleState()
    {
        farmerBubbleSequence?.Kill(false);
        farmerBubbleSequence = null;

        if (farmerBubble == null)
            return;

        farmerBubble.DOKill();
        farmerBubble.localScale = Vector3.zero;
        farmerBubble.gameObject.SetActive(false);
    }

    #endregion

    #region Fire

    private bool IsActiveFireCollider(Collider2D collider)
    {
        return collider != null && collider.name.Equals("fire");
    }

    private bool CanAcceptFireContact(Level74GrassCtrl grass)
    {
        Level74FireCtrl resolvedFireCtrl = ResolveFireCtrl();
        return grass != null && !grass.IsDead && !isCleaningUp && !isDone && isActiveAndEnabled && resolvedFireCtrl != null && resolvedFireCtrl.IsActiveFire;
    }

    private Level74FireCtrl ResolveFireCtrl()
    {
        if (fireCtrl == null)
            fireCtrl = GetComponentInChildren<Level74FireCtrl>(true);

        return fireCtrl;
    }

    private GameObject ResolveGrassFireVisualTemplate()
    {
        if (grassFireVisualTemplate != null)
            return grassFireVisualTemplate;

        if (fireCollider != null)
            return fireCollider.gameObject;

        return null;
    }

    private void ResetFireState()
    {
        ResolveFireCtrl()?.ResetRuntimeState();

        if (flashCollider != null)
            flashCollider.enabled = true;

        if (fireCollider != null)
            fireCollider.enabled = false;
    }

    #endregion

    private bool IsActorEligible()
    {
        return actorCtrl != null && actorCtrl.CanStartLevel74Action;
    }

    private Level74ActorCtrl.ToolState ToActorToolState(ToolType toolType)
    {
        switch (toolType)
        {
            case ToolType.Eradicator:
                return Level74ActorCtrl.ToolState.Using_Eradicator;
            case ToolType.Mow:
                return Level74ActorCtrl.ToolState.Using_Mow;
            case ToolType.Hoe:
                return Level74ActorCtrl.ToolState.Using_Hoe;
            default:
                return Level74ActorCtrl.ToolState.None;
        }
    }

    private void ResolvePanel()
    {
        if (PlayCtrl.Instance != null && PlayCtrl.Instance.CurrentGamePlayPanel != null)
            panelCtrl = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<Level74GamePlayPanelCtrl>();

        if (panelCtrl == null)
            this.LogWarning("[Level74Ctrl] Level74GamePlayPanelCtrl not found on CurrentGamePlayPanel.");
    }

    private void BindPanel()
    {
        if (panelCtrl == null)
            return;

        panelCtrl.Bind(OnCutClicked, OnDigClicked);
        panelCtrl.HideAll();
    }

    private void BindRuntimeEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
        subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
    }

    private void OnGameReset(GameResetEvent evt)
    {
        ResetRuntimeState();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        if (evt.NewState == GamePlayStateName.SUCCESS || evt.NewState == GamePlayStateName.FAILED)
            CleanupFireRuntime();
    }

    private void CleanupFireRuntime()
    {
        isCleaningUp = true;
        selectedGrass = null;
        ResetPanelRefreshState();
        panelCtrl?.HideAll();
        ResetFarmerBubbleState();
        ResetFireState();
        CleanupGrassFireRuntime();
    }

    private void Cleanup()
    {
        CleanupFireRuntime();
        interactionLocked = false;
        selectedGrass = null;
        ResetPanelRefreshState();
        transform.DOKill();
        ResetFarmerBubbleState();
        panelCtrl?.HideAll();
        panelCtrl?.Unbind();
        actorCtrl?.CancelLevel74Action();

        foreach (KeyValuePair<ToolType, ToolRuntime> pair in toolRuntimes)
        {
            if (pair.Value.Binding.tool != null)
                pair.Value.Binding.tool.DOKill();
        }
    }

    private void CleanupGrassFireRuntime()
    {
        if (grasses == null)
            return;

        for (int i = 0; i < grasses.Count; i++)
            grasses[i]?.CleanupFireRuntimeState();
    }

    private void OnDisable()
    {
        Cleanup();
        EventsUtils.ResetEvents(ref subscriber);
    }

    private void OnDestroy()
    {
        Cleanup();
        EventsUtils.ResetEvents(ref subscriber);
    }

    public void OnFireCallback()
    {
        if (isCleaningUp)
            return;

        if (flashCollider != null)
            flashCollider.enabled = false;

        if (fireCollider != null)
            fireCollider.enabled = true;
    }
}
