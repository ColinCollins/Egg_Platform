using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using MelenitasDev.SoundsGood;
using Spine.Unity;
using UnityEngine;
using AudioManager = Game.Scripts.Common.AudioManager;

/// <summary>
/// Level144「给门降温」：自动风扇短暂降温，手动连续旋转才永久冷却。
/// 主角按离门距离受热；失败、进门和整关重建沿用既有主线流程。
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class Level144Ctrl : MonoBehaviour
{
    #region 显式引用与本关参数
    [Tooltip("本关基础控制器，负责失败和进门结算。")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [Tooltip("角色主碰撞体，用于辨认本关门触发事件。")]
    [SerializeField] private Collider2D actorCollider;
    [Tooltip("当前角色 Spine，只修改其 RGB，保留原有透明度。")]
    [SerializeField] private SkeletonAnimation actorSkeleton;
    [Tooltip("本关摄像机，同时用于扇叶输入与镜头水平拖动。")]
    [SerializeField] private Camera inputCamera;
    [Tooltip("原稿中的镜头拖动区域；不改变原地图布局。")]
    [SerializeField] private Collider2D cameraDragArea;
    [Tooltip("原稿风扇开关的点击区域，与手动扇叶区域分别识别。")]
    [SerializeField] private Collider2D fanSwitchArea;
    [Tooltip("现有扇叶 Spine，播放 off / on_fan。")]
    [SerializeField] private SkeletonAnimation fanSkeleton;
    [Tooltip("现有风效 Spine，播放 on_wind；风效只作视觉表现。")]
    [SerializeField] private SkeletonAnimation windSkeleton;
    [Tooltip("挂在出口门物体上的变色组件；颜色与编辑预览在该物体上调整。")]
    [SerializeField] private SpineHeatTint doorHeatTint;
    [Tooltip("出口门原有触发盒，使用它的边缘计算主角到门的距离。")]
    [SerializeField] private Collider2D doorTrigger;
    [Tooltip("门上方的上飘烟雾，随门的显示热度同时缩小尺寸、降低发射率。")]
    [SerializeField] private ParticleSystem doorSmoke;
    [Tooltip("本关由角色推动的冰块误导，控制融化表现和短暂褪红反馈。")]
    [SerializeField] private Level144IceDecoy iceDecoy;
    [Tooltip("可横向拖动的太阳伞误导，始终投影；遮门只稍微褪红，不参与真实冷热状态。")]
    [SerializeField] private Level144UmbrellaDecoy umbrellaDecoy;

    [Header("旋转手感")]
    [Tooltip("扇叶中心骨骼名，仅查询已显式绑定的本关 Spine。")]
    [SerializeField] private string rotorBoneName = "fan_003";
    [Tooltip("原稿同一台风扇的下方扇叶中心；两个扇叶都可以开始手动旋转。")]
    [SerializeField] private string lowerRotorBoneName = "fan_006";
    [Tooltip("开始拖扇叶时的局部半径；认领手指后允许滑出，不要求精确绕圆。")]
    [SerializeField, Min(0.1f)] private float rotorPickRadius = 2.2f;
    [Tooltip("连续有效旋转的完成时长（秒）。")]
    [SerializeField, Min(0.1f)] private float manualDuration = 4f;
    [Tooltip("仍按住时允许的短暂停顿（秒）；明确松手不使用此容错。")]
    [SerializeField, Min(0f)] private float pauseGrace = 0.3f;
    [Tooltip("有效旋转最低角速度（度/秒），排除静止时微小抖动。")]
    [SerializeField, Min(1f)] private float minimumTurnSpeed = 8f;
    [Tooltip("原 on_fan 动画每循环的实际旋转角度；用于将手势角速度换算为 Spine 速度。")]
    [SerializeField, Min(1f)] private float animationTurnDegrees = 355.46f;
    [Tooltip("旋转中心死区，按屏幕高度相对 1080 缩放；穿过中心不产生角度跳变。")]
    [SerializeField, Min(0f)] private float centerDeadZonePixels = 12f;

    [Header("门与受热")]
    [Tooltip("自动风扇每次运行时长（秒），运行期间重复点击不会续时。")]
    [SerializeField, Min(0.1f)] private float automaticDuration = 3f;
    [Tooltip("自动风扇最多把门暂时降到的热度，仍不能开启出口。")]
    [SerializeField, Range(0.05f, 0.9f)] private float automaticMinimumHeat = 0.2f;
    [Tooltip("自动风扇停止后渐变回红的时间（秒）；手动松手则立即回红。")]
    [SerializeField, Min(0.01f)] private float automaticReheatDuration = 0.35f;
    [Tooltip("全热门外开始逐渐变红的距离（世界单位）。")]
    [SerializeField, Min(0.1f)] private float heatStartDistance = 7f;
    [Tooltip("距离热出口边缘小于此值就晕倒失败；永久冷却后不再受热。")]
    [SerializeField, Min(0f)] private float failDistance = 0.4f;
    [Tooltip("角色最热颜色，Alpha 为最大混合强度，沿用 117 的染色方式。")]
    [SerializeField] private Color actorHotTint = new Color(1f, 0.25f, 0.2f, 0.65f);
    [Tooltip("角色颜色短渐变时长，缓和逐帧距离变化；受热进度不按时间累计。")]
    [SerializeField, Min(0.01f)] private float actorTintDuration = 0.12f;
    [Tooltip("沿用原稿镜头的水平移动范围。")]
    [SerializeField] private Vector2 cameraXRange = new Vector2(-3f, 8f);
    [Tooltip("冰块首次落地时的震屏时长（秒）。")]
    [SerializeField, Min(0.01f)] private float iceLandingShakeDuration = 0.18f;
    [Tooltip("冰块首次落地时的震屏幅度（世界单位）。")]
    [SerializeField, Min(0f)] private float iceLandingShakeStrength = 0.12f;
    [Tooltip("全热门时烟雾的尺寸倍率；保留原生命周期曲线，降温时已出现的烟与新烟都会变小。")]
    [SerializeField, Min(1f)] private float hotSmokeSizeMultiplier = 1.7f;
    #endregion

    #region 本轮状态与生命周期
    private enum PointerMode { None, Rotor, Camera, Switch, Umbrella }
    private PointerMode pointerMode;
    private int activeFinger = -1;
    private Vector2 previousPointer;
    private float previousAngle;
    private bool hasAngle;
    private float turnSpeed;
    private float stillTime;
    private float manualElapsed;
    private float manualStartHeat = 1f;
    private bool manualTurning;
    private float automaticRemaining;
    private float automaticStartHeat = 1f;
    private float doorHeat = 1f;
    private bool permanentlyCooled;
    private bool heatFailed;
    private bool initialized;
    private bool appPaused;
    private bool appFocused = true;
    private Spine.Bone rotorBone;
    private Spine.Bone upperRotorBone;
    private Spine.Bone lowerRotorBone;
    private Spine.TrackEntry fanTrack;
    private Spine.TrackEntry windTrack;
    private Color actorInitialColor;
    private float actorHeat;
    private float smokeRate;
    private float originalSmokeSizeMultiplier;
    private Sound fanSound;
    private bool fanRunning;
    private float fanCycleDuration;
    private float cameraShakeRemaining;
    private Vector3 cameraShakeOffset;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        actorSkeleton.Initialize(false);
        fanSkeleton.Initialize(false);
        windSkeleton.Initialize(false);
        actorInitialColor = actorSkeleton.Skeleton.GetColor();
        fanCycleDuration = fanSkeleton.Skeleton.Data.FindAnimation("on_fan").Duration;
        upperRotorBone = fanSkeleton.Skeleton.FindBone(rotorBoneName);
        lowerRotorBone = fanSkeleton.Skeleton.FindBone(lowerRotorBoneName);
        rotorBone = upperRotorBone;
        smokeRate = doorSmoke.emission.rateOverTime.constant;
        originalSmokeSizeMultiplier = doorSmoke.sizeOverLifetime.sizeMultiplier;
        fanTrack = fanSkeleton.AnimationState.SetAnimation(0, "off", true);
        windTrack = windSkeleton.AnimationState.SetAnimation(0, "on_wind", true);
        windSkeleton.gameObject.SetActive(false);
        initialized = true;
        ApplyDoorTint();
    }

    private bool CanRun()
    {
        return Application.isPlaying && initialized && !appPaused && appFocused &&
               !heatFailed && !levelCtrl.Actor.IsDied && !levelCtrl.IsActorEnterDoor && !levelCtrl.IsFail() &&
               PlayCtrl.Instance != null && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING) &&
               PlayCtrl.Instance.Level?.CurrentLevelState?.IsAttemptTransitioning != true;
    }

    private void Update()
    {
        if (!Application.isPlaying || !initialized) return;
        RemoveCameraShake();
        if (!CanRun())
        {
            CancelPointer();
            SetFanPlayback(0f);
            if (doorSmoke.isPlaying) doorSmoke.Pause();
            iceDecoy.SetSuspended(true);
            cameraShakeRemaining = 0f;
            return;
        }

        ReadPointer();
        iceDecoy.SetSuspended(false);
        iceDecoy.Tick(Time.deltaTime, !permanentlyCooled);
        if (iceDecoy.ConsumeLanding())
        {
            cameraShakeRemaining = iceLandingShakeDuration;
            AudioManager.PlaySound("crash");
        }
        TickCooling(Time.deltaTime);
        UpdateActorHeat();
        if (permanentlyCooled && doorTrigger.IsTouching(actorCollider)) OnDoorEnter(actorCollider);
        UpdateSmoke();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !initialized) return;
        ApplyDoorTint();
        UpdateCameraShake(Time.deltaTime);
        umbrellaDecoy.RefreshShadow();
        if (!CanRun() && !heatFailed) return;
        Color current = actorSkeleton.Skeleton.GetColor();
        Color target = Color.Lerp(actorInitialColor, actorHotTint, actorHeat * actorHotTint.a);
        float blend = heatFailed ? 1f : 1f - Mathf.Exp(-4.60517f * Time.deltaTime / actorTintDuration);
        Color next = Color.Lerp(current, target, blend);
        next.a = current.a;
        actorSkeleton.Skeleton.SetColor(next);
    }

    private void OnApplicationPause(bool paused)
    {
        appPaused = paused;
        if (paused && initialized) SuspendInteraction();
    }

    private void OnApplicationFocus(bool focused)
    {
        appFocused = focused;
        if (!focused && initialized) SuspendInteraction();
    }

    private void SuspendInteraction()
    {
        CancelPointer();
        SetFanPlayback(0f);
        if (doorSmoke.isPlaying) doorSmoke.Pause();
        iceDecoy.SetSuspended(true);
        cameraShakeRemaining = 0f;
        RemoveCameraShake();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying || !initialized) return;
        CancelPointer();
        SetFanPlayback(0f);
        doorSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Color restored = actorInitialColor;
        restored.a = actorSkeleton.Skeleton.A;
        actorSkeleton.Skeleton.SetColor(restored);
        doorHeatTint.RestoreAppearance();
        iceDecoy.SetSuspended(true);
        cameraShakeRemaining = 0f;
        RemoveCameraShake();
    }
    #endregion

    #region 冰块落地震屏
    private void RemoveCameraShake()
    {
        inputCamera.transform.localPosition -= cameraShakeOffset;
        cameraShakeOffset = Vector3.zero;
    }

    private void UpdateCameraShake(float deltaTime)
    {
        RemoveCameraShake();
        if (!CanRun() || cameraShakeRemaining <= 0f) return;
        cameraShakeRemaining = Mathf.Max(0f, cameraShakeRemaining - deltaTime);
        float envelope = cameraShakeRemaining / iceLandingShakeDuration;
        float phase = (iceLandingShakeDuration - cameraShakeRemaining) * 95f;
        cameraShakeOffset = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f), 0f) * (iceLandingShakeStrength * envelope);
        // 叠加到当前拖动位置，下一帧先移除偏移，不缓存旧镜头原点。
        inputCamera.transform.localPosition += cameraShakeOffset;
    }
    #endregion

    #region 单指旋转、开关与镜头拖动
    private Vector2 RotorScreenPoint()
    {
        Vector3 world = fanSkeleton.transform.TransformPoint(new Vector3(rotorBone.WorldX, rotorBone.WorldY, 0f));
        return inputCamera.WorldToScreenPoint(world);
    }

    private Vector2 WorldPoint(Vector2 screen)
    {
        return inputCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y,
            Mathf.Abs(inputCamera.transform.position.z - fanSkeleton.transform.position.z)));
    }

    private bool TryPointerAngle(Vector2 screen, out float angle)
    {
        Vector2 delta = screen - RotorScreenPoint();
        float deadZone = centerDeadZonePixels * Mathf.Max(0.5f, Screen.height / 1080f);
        angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        return delta.sqrMagnitude > deadZone * deadZone;
    }

    private void ReadPointer()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        ReadTouches();
#else
        if (Input.touchCount > 0) ReadTouches();
        else if (activeFinger >= 0) CancelPointer();
        else if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0)) CancelPointer();
        else if (Input.GetMouseButton(0)) MovePointer(Input.mousePosition, Time.deltaTime);
#endif
    }

    private void ReadTouches()
    {
        if (pointerMode == PointerMode.None)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;
                BeginPointer(touch.position);
                if (pointerMode == PointerMode.None) continue;
                activeFinger = touch.fingerId;
                return;
            }
            return;
        }
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != activeFinger) continue;
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) CancelPointer();
            else MovePointer(touch.position, Time.deltaTime);
            return;
        }
        CancelPointer();
    }

    private void BeginPointer(Vector2 screen)
    {
        if (pointerMode != PointerMode.None || InputUtils.IsPointerOverUI(screen)) return;
        Vector2 world = WorldPoint(screen);
        previousPointer = screen;
        if (fanSwitchArea.OverlapPoint(world))
        {
            pointerMode = PointerMode.Switch;
            if (automaticRemaining <= 0f)
            {
                automaticRemaining = automaticDuration;
                automaticStartHeat = doorHeat;
                AudioManager.PlaySound("fanSwitch");
            }
            return;
        }
        rotorBone = upperRotorBone;
        Vector2 upperCenter = RotorScreenPoint();
        rotorBone = lowerRotorBone;
        if ((screen - upperCenter).sqrMagnitude < (screen - RotorScreenPoint()).sqrMagnitude)
            rotorBone = upperRotorBone;
        Vector2 center = RotorScreenPoint();
        Vector3 worldCenter = fanSkeleton.transform.TransformPoint(new Vector3(rotorBone.WorldX, rotorBone.WorldY, 0f));
        float radius = Vector2.Distance(center, inputCamera.WorldToScreenPoint(worldCenter +
            fanSkeleton.transform.TransformVector(Vector3.right * rotorPickRadius)));
        if ((screen - center).sqrMagnitude <= radius * radius)
        {
            pointerMode = PointerMode.Rotor;
            hasAngle = TryPointerAngle(screen, out previousAngle);
            stillTime = pauseGrace + 1f;
            return;
        }
        if (umbrellaDecoy.TryBeginDrag(world)) pointerMode = PointerMode.Umbrella;
        else if (cameraDragArea.OverlapPoint(world)) pointerMode = PointerMode.Camera;
    }

    private void MovePointer(Vector2 screen, float deltaTime)
    {
        if (pointerMode == PointerMode.Umbrella)
        {
            umbrellaDecoy.MoveDrag(WorldPoint(screen));
            previousPointer = screen;
            return;
        }
        if (pointerMode == PointerMode.Camera)
        {
            float dx = WorldPoint(screen).x - WorldPoint(previousPointer).x;
            Vector3 cameraPosition = inputCamera.transform.position;
            cameraPosition.x = Mathf.Clamp(cameraPosition.x - dx, cameraXRange.x, cameraXRange.y);
            inputCamera.transform.position = cameraPosition;
        }
        previousPointer = screen;
        if (pointerMode != PointerMode.Rotor) return;
        if (!TryPointerAngle(screen, out float angle)) { hasAngle = false; return; }
        if (!hasAngle) { previousAngle = angle; hasAngle = true; return; }
        float angularSpeed = Mathf.DeltaAngle(previousAngle, angle) / Mathf.Max(deltaTime, 0.001f);
        previousAngle = angle;
        if (Mathf.Abs(angularSpeed) < minimumTurnSpeed)
        {
            // 停顿容错保留通关进度，画面速度仍迅速跟随手指减慢。
            turnSpeed = Mathf.Lerp(turnSpeed, 0f, 1f - Mathf.Exp(-30f * deltaTime));
            return;
        }
        if (!manualTurning)
        {
            manualStartHeat = doorHeat;
            automaticRemaining = 0f;
        }
        manualTurning = true;
        stillTime = 0f;
        float targetSpeed = Mathf.Clamp(angularSpeed * fanCycleDuration / animationTurnDegrees, -3f, 3f);
        // 反向时立即改向；同向时仅作很短的速度平滑，不设会抬高慢速的下限。
        turnSpeed = turnSpeed * targetSpeed <= 0f ? targetSpeed :
            Mathf.Lerp(turnSpeed, targetSpeed, 1f - Mathf.Exp(-30f * deltaTime));
    }

    private void CancelPointer()
    {
        if (pointerMode == PointerMode.Rotor) ResetManualTurn();
        if (pointerMode == PointerMode.Umbrella) umbrellaDecoy.EndDrag();
        pointerMode = PointerMode.None;
        activeFinger = -1;
        hasAngle = false;
    }

    private void ResetManualTurn()
    {
        if (manualTurning && !permanentlyCooled) doorHeat = 1f;
        manualTurning = false;
        manualElapsed = 0f;
        turnSpeed = 0f;
    }
    #endregion

    #region 冷却、距离受热与进门
    private void TickCooling(float deltaTime)
    {
        if (manualTurning)
        {
            stillTime += deltaTime;
            if (stillTime > pauseGrace)
            {
                ResetManualTurn();
                hasAngle = false;
            }
            else
            {
                manualElapsed = Mathf.Min(manualDuration, manualElapsed + deltaTime);
                if (!permanentlyCooled)
                {
                    doorHeat = Mathf.Lerp(manualStartHeat, 0f, manualElapsed / manualDuration);
                    if (manualElapsed >= manualDuration)
                    {
                        permanentlyCooled = true;
                        actorHeat = 0f;
                    }
                }
            }
        }
        else if (automaticRemaining > 0f)
        {
            automaticRemaining = Mathf.Max(0f, automaticRemaining - deltaTime);
            doorHeat = Mathf.Lerp(automaticStartHeat, automaticMinimumHeat, 1f - automaticRemaining / automaticDuration);
        }
        else doorHeat = Mathf.MoveTowards(doorHeat, 1f, deltaTime / automaticReheatDuration);
        if (permanentlyCooled) doorHeat = 0f;
        SetFanPlayback(manualTurning ? turnSpeed : automaticRemaining > 0f ? 1f : 0f);
    }

    private void SetFanPlayback(float speed)
    {
        bool running = Mathf.Abs(speed) > 0.001f;
        if (running != fanRunning)
        {
            fanRunning = running;
            fanTrack = fanSkeleton.AnimationState.SetAnimation(0, running ? "on_fan" : "off", true);
            windSkeleton.gameObject.SetActive(running);
            if (running) fanSound = AudioManager.PlaySound("fan", loop: true);
            else { fanSound?.Stop(); fanSound = null; }
        }
        bool reverse = speed < 0f;
        if (running && fanTrack.Reverse != reverse)
        {
            // Spine 不支持负 TimeScale；切换 Reverse 时镜像当前相位，防止扇叶跳一下。
            float duration = fanTrack.AnimationEnd - fanTrack.AnimationStart;
            float phase = fanTrack.AnimationTime - fanTrack.AnimationStart;
            fanTrack.TrackTime = Mathf.Repeat(duration - phase, duration);
            fanTrack.Reverse = reverse;
        }
        fanTrack.TimeScale = running ? Mathf.Abs(speed) : 1f;
        windTrack.TimeScale = Mathf.Abs(speed);
    }

    private float DisplayHeat => doorHeat * (1f - iceDecoy.CoolingAmount) * (1f - umbrellaDecoy.CoolingAmount);

    private void ApplyDoorTint()
    {
        doorHeatTint.SetHeat(DisplayHeat);
    }

    private void UpdateSmoke()
    {
        if (permanentlyCooled || heatFailed)
        {
            doorSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }
        float heat = Mathf.Clamp01(DisplayHeat);
        // 修改生命周期模块能同时缩小已发出的烟，避免只改新粒子导致旧的大烟残留。
        var size = doorSmoke.sizeOverLifetime;
        size.sizeMultiplier = originalSmokeSizeMultiplier * hotSmokeSizeMultiplier * Mathf.Lerp(0.15f, 1f, heat);
        var emission = doorSmoke.emission;
        emission.rateOverTime = smokeRate * heat;
        if (!doorSmoke.isPlaying) doorSmoke.Play();
    }

    private void UpdateActorHeat()
    {
        if (permanentlyCooled) { actorHeat = 0f; return; }
        Vector2 actorPosition = levelCtrl.Actor.transform.position;
        float distance = Vector2.Distance(actorPosition, doorTrigger.bounds.ClosestPoint(actorPosition));
        float range = Mathf.Lerp(failDistance + 0.1f, heatStartDistance, doorHeat);
        actorHeat = Mathf.InverseLerp(range, failDistance, distance);
        if (distance > failDistance) return;
        heatFailed = true;
        actorHeat = 1f;
        CancelPointer();
        SetFanPlayback(0f);
        levelCtrl.SetCtrlable(false);
        AudioManager.PlaySound("failed");
        levelCtrl.Actor.Die();
    }

    /// <summary>
    /// 仅绑定本关出口的进入事件；完成手动冷却后才交给原主线进门流程。
    /// 不直接派发胜利或存档事件，自动风扇降温不能打开出口。
    /// </summary>
    public void OnDoorEnter(Collider2D other)
    {
        if (!CanRun() || !permanentlyCooled || other.attachedRigidbody != actorCollider.attachedRigidbody) return;
        levelCtrl.OnActorTrigger2D(other);
    }
    #endregion
}
