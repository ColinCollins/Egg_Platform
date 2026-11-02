using System;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>一层电梯的私有运行视图。编辑态保存开门终点，运行时才合拢。</summary>
[Serializable]
public sealed class Level139Elevator
{
    public Transform root;
    public SpriteRenderer leftDoor;
    public SpriteRenderer rightDoor;
    public SpriteRenderer background;
    public SpriteRenderer frame;
    public SpriteRenderer lamp;
    public Transform button;
    // 玩法几何独立于可人工移动的光束内景；保持原门洞标定。
    [SerializeField] private Vector3 cabinLocalCenter = new Vector3(7.29f, -0.94f, 0f);

    public enum DoorState { Closed, Waiting, Opening, Open, Closing }
    public DoorState State { get; private set; }
    public Level139PlayerZone ButtonZone { get; private set; }
    public Level139PlayerZone CabinZone { get; private set; }
    public bool SuccessClosing { get; private set; }
    public bool IsOpen => State == DoorState.Open;
    public bool CanRequestOpen => !SuccessClosing && (State == DoorState.Closed
        || State == DoorState.Open || State == DoorState.Closing);
    public bool HasPlayer => CabinZone != null && CabinZone.HasPlayer;
    public Bounds CabinBounds => new Bounds(root.TransformPoint(cabinLocalCenter), background.bounds.size);
    public float CabinLeft => CabinBounds.min.x;
    public float CabinRight => CabinBounds.max.x;
    public float CabinFloor => CabinBounds.min.y;

    private Vector3 leftOpen, rightOpen, leftClosed, rightClosed;
    private Vector3 leftFrom, rightFrom, leftTo, rightTo;
    private float openInnerLeft, openInnerRight;
    private float elapsed, idleSeconds;
    private bool wasOverloaded;
    private Sprite originalLamp;
    private bool originalLampEnabled;
    private Sprite whiteLamp, yellowLamp, greenLamp, redLamp;
    private SpriteMaskInteraction originalLeftMask, originalRightMask;
    private Material leftMaterial, rightMaterial;
    private Material runtimeDoorMaterial;
    private static readonly int DoorClipRectId = Shader.PropertyToID("_DoorClipRect");
    private static readonly int DoorSeamRectId = Shader.PropertyToID("_DoorSeamRect");
    private static readonly int DoorClosedId = Shader.PropertyToID("_DoorClosed");
    // 100 PPU 素材边缘最多 6 像素未全不透明，另留 2 像素采样余量；不移动/拉伸门扇。
    private const float ClosedSeamHalfWidth = 0.08f;
    private int leftOrder, rightOrder, backgroundOrder, frameOrder, lampOrder;
    private GameObject runtimeRoot;
    private bool initialized;
    private Action beforeOpening;

    public bool HasReferences => root && leftDoor && rightDoor && background
        && background.sprite && frame && lamp && button;

    /// <summary>同步缓存人工开门姿态并闭合；必须在任何初始化 yield 或乘客生成前调用。</summary>
    public void PrepareClosedVisuals(Material doorMaterial, Sprite white, Sprite yellow, Sprite green, Sprite red)
    {
        if (initialized)
            return;
        if (!HasReferences || !white || !yellow || !green || !red
            || doorMaterial == null || !doorMaterial.HasProperty(DoorClipRectId)
            || !doorMaterial.HasProperty(DoorSeamRectId) || !doorMaterial.HasProperty(DoorClosedId))
            throw new InvalidOperationException("Level139 电梯缺少门体引用或专属门洞裁剪材质。");
        leftOpen = leftDoor.transform.localPosition;
        rightOpen = rightDoor.transform.localPosition;
        // 登梯区间固定取人工完全开门姿态的两条内沿，不能随着关门过程一起收窄。
        openInnerLeft = leftOpen.x + leftDoor.size.x * Mathf.Abs(leftDoor.transform.localScale.x) * 0.5f;
        openInnerRight = rightOpen.x - rightDoor.size.x * Mathf.Abs(rightDoor.transform.localScale.x) * 0.5f;
        if (openInnerRight <= openInnerLeft)
            throw new InvalidOperationException("Level139 完全开门姿态的两门内沿没有有效登梯区间。");
        // 标定来自本次几何计算，不是历史关门坐标快照。保留 Y/Z、尺寸、缩放。
        float centre = cabinLocalCenter.x;
        leftClosed = leftOpen;
        rightClosed = rightOpen;
        leftClosed.x = centre - leftDoor.size.x * leftDoor.transform.localScale.x * 0.5f;
        rightClosed.x = centre + rightDoor.size.x * rightDoor.transform.localScale.x * 0.5f;
        originalLamp = lamp.sprite;
        originalLampEnabled = lamp.enabled;
        whiteLamp = white;
        yellowLamp = yellow;
        greenLamp = green;
        redLamp = red;
        originalLeftMask = leftDoor.maskInteraction;
        originalRightMask = rightDoor.maskInteraction;
        leftMaterial = leftDoor.sharedMaterial;
        rightMaterial = rightDoor.sharedMaterial;
        leftOrder = leftDoor.sortingOrder;
        rightOrder = rightDoor.sortingOrder;
        backgroundOrder = background.sortingOrder;
        frameOrder = frame.sortingOrder;
        lampOrder = lamp.sortingOrder;
        initialized = true;

        background.sortingOrder = -20;
        leftDoor.sortingOrder = rightDoor.sortingOrder = 0;
        frame.sortingOrder = 10;
        // V2 顶部内灯：仍高于门扇以显示呼叫状态，但由门框压住顶部边缘。
        lamp.sortingOrder = 5;
        SetLamp(whiteLamp);
        // 两层使用各自的材质实例，不能互相覆盖裁剪矩形，也不修改共享素材材质。
        runtimeDoorMaterial = new Material(doorMaterial)
        {
            name = "Level139_DoorClip_" + root.name,
            hideFlags = HideFlags.DontSave
        };
        leftDoor.sharedMaterial = rightDoor.sharedMaterial = runtimeDoorMaterial;
        leftDoor.maskInteraction = rightDoor.maskInteraction = SpriteMaskInteraction.None;
        State = DoorState.Closed;
        SuccessClosing = false;
        leftDoor.transform.localPosition = leftClosed;
        rightDoor.transform.localPosition = rightClosed;
        UpdateClipRect();
    }

    /// <summary>建立本层判定区；可选回调仅在真正开门前调用，用于上层乘客换装。</summary>
    public void Initialize(ActorCtrl player, Action onBeforeOpening = null)
    {
        if (!initialized)
            throw new InvalidOperationException("Level139 必须先同步建立关门初态。");

        beforeOpening = onBeforeOpening;
        runtimeRoot = new GameObject("Level139_ElevatorRuntime");
        runtimeRoot.transform.SetParent(root, false);

        ButtonZone = CreateZone("ButtonZone", button.position, new Vector2(1f, 2f), player);
        Vector3 cabinCentre = root.TransformPoint(new Vector3(
            (openInnerLeft + openInnerRight) * 0.5f,
            cabinLocalCenter.y, cabinLocalCenter.z));
        Vector2 cabinSize = new Vector2((openInnerRight - openInnerLeft) * Mathf.Abs(root.lossyScale.x),
            background.bounds.size.y);
        CabinZone = CreateZone("CabinZone", cabinCentre, cabinSize, player, true);
        // 电梯只参与区域判定，不生成实体阻挡，不让 Actor 将电梯当作墙进入 push。
    }

    private void UpdateClipRect()
    {
        // 取人工门洞的背景宽度和门高；裁剪固定在门框内，不随移动门扇一起移动。
        float width = background.drawMode == SpriteDrawMode.Simple
            ? background.sprite.bounds.size.x : background.size.x;
        width *= Mathf.Abs(background.transform.lossyScale.x);
        float height = leftDoor.size.y * Mathf.Abs(leftDoor.transform.lossyScale.y);
        Vector3 centre = root.TransformPoint(new Vector3(cabinLocalCenter.x,
            leftOpen.y, leftOpen.z));
        runtimeDoorMaterial.SetVector(DoorClipRectId, new Vector4(
            centre.x - width * 0.5f, centre.y - height * 0.5f,
            centre.x + width * 0.5f, centre.y + height * 0.5f));
        float seamHalfWidth = ClosedSeamHalfWidth * Mathf.Abs(root.lossyScale.x);
        runtimeDoorMaterial.SetVector(DoorSeamRectId, new Vector4(
            centre.x - seamHalfWidth, CabinBounds.min.y,
            centre.x + seamHalfWidth, CabinBounds.max.y));
        ApplyClosedSeamState();
    }

    private void ApplyClosedSeamState()
    {
        runtimeDoorMaterial.SetFloat(DoorClosedId,
            State == DoorState.Closed || State == DoorState.Waiting ? 1f : 0f);
    }

    public Vector3 GetPassengerStandPosition(int index, int count)
    {
        if (count <= 0 || index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));
        // 三列两排错位；保留角色原尺寸，后排略高，形成拥挤但不完全重叠的轮廓。
        int columns = Mathf.CeilToInt(count / 2f);
        float fraction = (index % columns + (index < columns ? 0.4f : 0.6f)) / columns;
        float localX = Mathf.Lerp(openInnerLeft, openInnerRight, fraction);
        Vector3 position = root.TransformPoint(new Vector3(localX, 0f, 0f));
        position.y = CabinFloor + (index < columns ? 0.45f : 0f) * Mathf.Abs(root.lossyScale.y);
        position.z = root.position.z;
        return position;
    }

    private Level139PlayerZone CreateZone(string name, Vector3 position, Vector2 worldSize,
        ActorCtrl player, bool requireFullBodyWidth = false)
    {
        var go = new GameObject(name, typeof(BoxCollider2D), typeof(Level139PlayerZone));
        go.layer = 0; // Default，不进入 Actor 的 Ground(6)/Stuff(8) 射线层。
        go.transform.SetParent(runtimeRoot.transform, false);
        go.transform.position = position;
        var zone = go.GetComponent<Level139PlayerZone>();
        zone.Initialize(player, new Vector2(worldSize.x / root.lossyScale.x,
            worldSize.y / root.lossyScale.y), requireFullBodyWidth);
        return zone;
    }

    public bool RequestOpen()
    {
        if (!CanRequestOpen)
            return false;
        idleSeconds = 0f;
        if (State == DoorState.Closed)
        {
            State = DoorState.Waiting;
            elapsed = 0f;
            SetLamp(yellowLamp);
        }
        else if (State == DoorState.Closing)
            StartMotion(true);
        // 已完全打开只续期，不手动关门，也不重复播放开门音效。
        return true;
    }

    public void BeginSuccessClose()
    {
        if (!IsOpen || SuccessClosing)
            return;
        SuccessClosing = true;
        StartMotion(false);
    }

    private void StartMotion(bool open)
    {
        if (open)
            beforeOpening?.Invoke();
        State = open ? DoorState.Opening : DoorState.Closing;
        if (open)
        {
            SetLamp(wasOverloaded ? redLamp : greenLamp);
        }
        elapsed = idleSeconds = 0f;
        leftFrom = leftDoor.transform.localPosition;
        rightFrom = rightDoor.transform.localPosition;
        leftTo = open ? leftOpen : leftClosed;
        rightTo = open ? rightOpen : rightClosed;
        // 开始开/关门立即撤掉封缝，避免开门后中间残留不透明竖条。
        ApplyClosedSeamState();
    }

    public void Tick(float delta)
    {
        UpdateClipRect();
        if (State == DoorState.Waiting)
        {
            elapsed += delta;
            if (elapsed >= 3f)
            {
                // 仅本次呼叫到达时响铃；门已打开续期或防夹重开不重复。
                AudioManager.PlaySound("dianti");
                StartMotion(true);
            }
            return;
        }
        if (State == DoorState.Opening || State == DoorState.Closing)
        {
            // 普通关门时有人重新进入就重新开门，防夹；成功关门不受退出回调影响。
            if (State == DoorState.Closing && !SuccessClosing && HasPlayer)
                StartMotion(true);
            elapsed += delta;
            float progress = Mathf.Clamp01(elapsed / 0.5f);
            leftDoor.transform.localPosition = Vector3.LerpUnclamped(leftFrom, leftTo, progress);
            rightDoor.transform.localPosition = Vector3.LerpUnclamped(rightFrom, rightTo, progress);
            if (progress >= 1f)
            {
                // 到位时精确回写缓存端点，禁止每次开门重新推算距离或累加偏移。
                leftDoor.transform.localPosition = leftTo;
                rightDoor.transform.localPosition = rightTo;
                State = State == DoorState.Opening ? DoorState.Open : DoorState.Closed;
                // 普通与成功关门均保持绿灯至到位；中途反向开门不会切白灯。
                if (State == DoorState.Closed)
                    SetLamp(whiteLamp);
                idleSeconds = 0f;
                // 完全关闭的同一帧启用，不能等下一次 Tick 才遮住乘客。
                ApplyClosedSeamState();
            }
            return;
        }
        if (!IsOpen || SuccessClosing)
            return;
        if (HasPlayer || wasOverloaded)
            idleSeconds = 0f;
        else
        {
            idleSeconds += delta;
            if (idleSeconds >= 3f)
                StartMotion(false);
        }
    }

    public bool SetOverload(bool overloaded)
    {
        if (overloaded == wasOverloaded)
            return false;
        bool entered = overloaded && !wasOverloaded;
        idleSeconds = 0f;
        if (overloaded)
            SetLamp(redLamp);
        else if (State == DoorState.Open || State == DoorState.Opening || State == DoorState.Closing)
            SetLamp(greenLamp);
        if (entered)
            AudioManager.PlaySound("passwordError");
        wasOverloaded = overloaded;
        return entered;
    }

    private void SetLamp(Sprite sprite)
    {
        lamp.enabled = true;
        lamp.sprite = sprite;
    }

    public void ClearContacts()
    {
        ButtonZone?.ClearContacts();
        CabinZone?.ClearContacts();
    }

    public void Restore()
    {
        if (!initialized)
            return;
        initialized = false;
        beforeOpening = null;
        if (leftDoor != null)
        {
            leftDoor.transform.localPosition = leftOpen;
            leftDoor.sortingOrder = leftOrder;
            leftDoor.sharedMaterial = leftMaterial;
            leftDoor.maskInteraction = originalLeftMask;
        }
        if (rightDoor != null)
        {
            rightDoor.transform.localPosition = rightOpen;
            rightDoor.sortingOrder = rightOrder;
            rightDoor.sharedMaterial = rightMaterial;
            rightDoor.maskInteraction = originalRightMask;
        }
        if (background != null)
            background.sortingOrder = backgroundOrder;
        if (frame != null)
            frame.sortingOrder = frameOrder;
        if (lamp != null)
        {
            lamp.sortingOrder = lampOrder;
            lamp.sprite = originalLamp;
            lamp.enabled = originalLampEnabled;
        }
        if (runtimeRoot != null)
        {
            runtimeRoot.SetActive(false);
            UnityEngine.Object.Destroy(runtimeRoot);
        }
        if (runtimeDoorMaterial != null)
            UnityEngine.Object.Destroy(runtimeDoorMaterial);
        runtimeDoorMaterial = null;
        runtimeRoot = null;
        SuccessClosing = wasOverloaded = false;
        elapsed = idleSeconds = 0f;
    }
}
