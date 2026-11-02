using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level142「删掉他」：踩按钮落下 APP，长按任意图标进入统一删除状态。
/// 进门胜利、重开重建、存档和打点继续交给既有主线流程。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level142Ctrl : MonoBehaviour
{
    #region Inspector 绑定
    [Tooltip("本关基础控制器，用于检查死亡和进门状态。")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [Tooltip("玩家摆放的中间按钮；压完并确认角色实际踩稳后才启动禁动和 APP 下落。")]
    [SerializeField] private Level142Button button;
    [Tooltip("当前关卡摄像机，负责屏幕坐标换算及局部震屏。")]
    [SerializeField] private Camera levelCamera;
    [Tooltip("显式绑定七个 APP；从后往前依次检测，顺序与 SortingGroup 一致。")]
    [SerializeField] private Level142AppItem[] apps;
    [Tooltip("按箭头显式绑定下落顺序：电话、天气、星球、齿轮、电源、灯泡、音乐；不改变点击层级。")]
    [SerializeField] private Level142AppItem[] dropOrder;
    [Tooltip("相邻 APP 下落开始的间隔，单位秒。")]
    [SerializeField, Min(0.01f)] private float dropInterval = 0.09f;
    [Tooltip("单个 APP 从屏幕上方落到原位置的时间，单位秒。")]
    [SerializeField, Min(0.05f)] private float dropDuration = 0.36f;
    [Tooltip("进入统一删除状态所需的持续按住时间，单位秒。")]
    [SerializeField, Min(0.1f)] private float longPressDuration = 0.45f;
    [Tooltip("每个 APP 落地后的震屏时长，单位秒。")]
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.14f;
    [Tooltip("局部摄像机震屏幅度，单位为世界单位。")]
    [SerializeField, Min(0f)] private float shakeStrength = 0.1f;
    #endregion

    #region 本轮状态与生命周期
    private bool triggered;
    private bool editing;
    private bool initialized;
    private bool appPaused;
    private bool appFocused = true;
    private float animationTime;
    private float shakeRemaining;
    private Vector3 cameraOrigin;
    private Level142AppItem pressedApp;
    private bool pressedMinus;
    private int activeFinger = -1;
    private Vector2 pressStart;
    private float pressTime;
    private int landedCount;
    private bool actorLocked;
    private bool actorWasCtrlable;
    private RigidbodyConstraints2D actorConstraints;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        cameraOrigin = levelCamera.transform.localPosition;
        for (int i = 0; i < apps.Length; i++)
            apps[i].Initialize(i * 1.7f);
        initialized = true;
    }

    private bool CanRun()
    {
        return Application.isPlaying && initialized && !appPaused && appFocused &&
               !levelCtrl.Actor.IsDied && !levelCtrl.IsActorEnterDoor && !levelCtrl.IsFail() &&
               PlayCtrl.Instance != null && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING) &&
               PlayCtrl.Instance.Level?.CurrentLevelState?.IsAttemptTransitioning != true;
    }

    private void Update()
    {
        // 暂停恢复会调用共享 ResumeMoving；本地机关仍在下落时继续持有禁动。
        if (actorLocked) levelCtrl.Actor.StopMoving();
        if (!CanRun())
        {
            if (initialized) StopInteraction();
            return;
        }
        if (!triggered && button.CanLockActor(levelCtrl.Actor.RB)) OnButtonPressed();
        animationTime += Time.deltaTime;
        foreach (Level142AppItem app in apps)
        {
            if (!app.Tick(Time.deltaTime, animationTime)) continue;
            shakeRemaining = shakeDuration;
            // 与82关门砸地使用同一音效；每个APP首次落定时和震屏同步，只播一次。
            AudioManager.PlaySound("crash");
            landedCount++;
        }
        if (actorLocked && landedCount == apps.Length) ReleaseActor();

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        ReadTouches();
#else
        if (Input.touchCount > 0) ReadTouches();
        else ReadMouse();
#endif
    }

    private void LateUpdate()
    {
        if (!initialized || shakeRemaining <= 0f) return;
        shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
        float envelope = shakeRemaining / shakeDuration;
        float phase = (shakeDuration - shakeRemaining) * 95f;
        levelCamera.transform.localPosition = cameraOrigin +
            new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f), 0f) * (shakeStrength * envelope);
    }

    private void OnButtonPressed()
    {
        if (triggered || !CanRun()) return;
        triggered = true;
        actorLocked = true;
        actorWasCtrlable = levelCtrl.ActorCtrlable;
        actorConstraints = levelCtrl.Actor.RB.constraints;
        levelCtrl.Actor.SetMoveInput(0f);
        levelCtrl.SetCtrlable(false);
        levelCtrl.Actor.StopMoving();
        levelCtrl.Actor.RB.constraints = RigidbodyConstraints2D.FreezeAll;
        // 每个图案从画面上方开始，不改动根节点保存的最终落点。
        float screenTop = levelCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f,
            Mathf.Abs(levelCamera.transform.position.z - transform.position.z))).y;
        for (int i = 0; i < dropOrder.Length; i++)
            dropOrder[i].ScheduleDrop(i * dropInterval,
                Mathf.Max(0f, screenTop - dropOrder[i].transform.position.y) + 3f, dropDuration);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying || !initialized) return;
        StopInteraction();
        ReleaseActor();
    }

    private void OnApplicationPause(bool paused)
    {
        appPaused = paused;
        if (paused && initialized) StopInteraction();
    }

    private void OnApplicationFocus(bool focused)
    {
        appFocused = focused;
        if (!focused && initialized) StopInteraction();
    }

    private void StopInteraction()
    {
        CancelPointer();
        SetEditing(false);
        shakeRemaining = 0f;
        if (levelCamera != null) levelCamera.transform.localPosition = cameraOrigin;
    }

    private void ReleaseActor()
    {
        if (!actorLocked) return;
        actorLocked = false;
        levelCtrl.Actor.StopMoving();
        levelCtrl.Actor.RB.constraints = actorConstraints;
        levelCtrl.SetCtrlable(actorWasCtrlable);
        if (CanRun()) levelCtrl.Actor.ResumeMoving();
    }
    #endregion

    #region 鼠标与触屏输入
    private void ReadMouse()
    {
        if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition);
        else if (Input.GetMouseButton(0)) HoldPointer(Input.mousePosition);
    }

    private void ReadTouches()
    {
        bool found = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (pressedApp == null && touch.phase == TouchPhase.Began)
            {
                BeginPointer(touch.position);
                if (pressedApp != null) activeFinger = touch.fingerId;
            }
            if (pressedApp == null || activeFinger != touch.fingerId) continue;
            found = true;
            if (touch.phase == TouchPhase.Ended) EndPointer(touch.position);
            else if (touch.phase == TouchPhase.Canceled) CancelPointer();
            else if (touch.phase != TouchPhase.Began) HoldPointer(touch.position);
        }
        if (pressedApp != null && !found) CancelPointer();
    }

    private Vector2 WorldPoint(Vector2 screenPoint)
    {
        return levelCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y,
            Mathf.Abs(levelCamera.transform.position.z - transform.position.z)));
    }

    private Level142AppItem HitApp(Vector2 screenPoint, out bool hitMinus)
    {
        Vector2 world = WorldPoint(screenPoint);
        for (int i = apps.Length - 1; i >= 0; i--)
        {
            hitMinus = apps[i].ContainsMinus(world);
            if (hitMinus || apps[i].ContainsIcon(world)) return apps[i];
        }
        hitMinus = false;
        return null;
    }

    private void BeginPointer(Vector2 screenPoint)
    {
        CancelPointer();
        if (InputUtils.IsPointerOverUI(screenPoint))
        {
            SetEditing(false);
            return;
        }
        pressedApp = HitApp(screenPoint, out pressedMinus);
        if (pressedApp == null)
        {
            SetEditing(false);
            return;
        }
        pressStart = screenPoint;
        pressTime = 0f;
    }

    private bool PointerStillValid(Vector2 screenPoint)
    {
        float tolerance = Mathf.Max(12f, 24f * Screen.height / 1080f);
        if (pressedApp == null || !pressedApp.CanInteract ||
            (screenPoint - pressStart).sqrMagnitude > tolerance * tolerance) return false;
        return HitApp(screenPoint, out bool overMinus) == pressedApp &&
               (!pressedMinus || overMinus);
    }

    private void HoldPointer(Vector2 screenPoint)
    {
        if (!PointerStillValid(screenPoint)) { CancelPointer(); return; }
        if (pressedMinus || editing) return;
        pressTime += Time.deltaTime;
        if (pressTime >= longPressDuration) SetEditing(true);
    }

    private void EndPointer(Vector2 screenPoint)
    {
        if (PointerStillValid(screenPoint) && pressedMinus && editing &&
            !InputUtils.IsPointerOverUI(screenPoint)) pressedApp.Delete();
        CancelPointer();
    }

    private void CancelPointer()
    {
        pressedApp = null;
        pressedMinus = false;
        activeFinger = -1;
        pressTime = 0f;
    }

    private void SetEditing(bool value)
    {
        if (editing == value) return;
        editing = value;
        foreach (Level142AppItem app in apps) app.SetEditing(value);
    }
    #endregion
}
