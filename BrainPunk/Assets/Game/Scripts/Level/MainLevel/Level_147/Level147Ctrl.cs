using System;
using System.Collections;
using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>147「自行车大赛」：自动比赛，重来箭头使命中的整车倒退；误用黄车会坠崖。</summary>
[DefaultExecutionOrder(200)]
public sealed class Level147Ctrl : MonoBehaviour
{
    #region Inspector 显式绑定
    [Serializable]
    private sealed class Bike
    {
        /// <summary>原场景车身、两个独立车轮及骑手坐标偏移，均显式配置。</summary>
        public Transform root;
        public SpriteRenderer rearWheel;
        public SpriteRenderer frontWheel;
        public Vector2 seatOffset = new Vector2(-0.12f, 0.25f);
        [NonSerialized] public float radius;
        public Vector3 Seat => root.position + root.TransformVector(seatOffset);
    }

    /// <summary>147 场景已有对象；不按名称查找或修改共享关卡资源。</summary>
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private Level147ActorCtrl actor;
    [SerializeField] private Level147ActorCtrl rival;
    /// <summary>主角已有的父级排序组；到终点停车后置于黄车后、黑盒子和红车前。</summary>
    [SerializeField] private SortingGroup actorSortingGroup;
    [SerializeField] private Camera levelCamera;
    [SerializeField] private Bike yellowBike;
    [SerializeField] private Bike redBike;
    [SerializeField] private Transform finishLine;
    [SerializeField] private Transform exitDoor;
    [SerializeField] private Collider2D exitTrigger;
    /// <summary>左端首段地面决定悬崖边缘；误用黄车时停用侧边失败墙，保留下方原 FallArea。</summary>
    [SerializeField] private Collider2D leftGround;
    [SerializeField] private Collider2D leftFallArea;
    /// <summary>复用已有气泡、吃星星和敲击特效，保持项目表现风格。</summary>
    [SerializeField] private HintBubble dialoguePrefab;
    [SerializeField] private ParticleSystem pickupEffectPrefab;
    [SerializeField] private ParticleSystem impactEffectPrefab;

    /// <summary>比赛节奏：等待玩家时缓慢骑行，双方上车后快追、交替领先、末段冲刺。</summary>
    [SerializeField] private float soloSpeed = 2.4f;
    [SerializeField] private float raceSpeed = 8.4f;
    [SerializeField] private float catchUpSpeed = 12f;
    [SerializeField] private float acceleration = 8f;
    /// <summary>黑盒子追赶的速度上限与末段冲刺速度，和本关比赛提速保持一致。</summary>
    [SerializeField] private float rivalSprintSpeed = 15f;
    /// <summary>上车范围覆盖黄车两侧和正常跳跃高度，允许空中接上骑行。</summary>
    [SerializeField] private float mountDistance = 2.2f;
    [SerializeField] private float mountHeight = 4.2f;
    [SerializeField] private float jumpDuration = 0.55f;
    [SerializeField] private float jumpHeight = 1.05f;
    /// <summary>一次蹬踏对应的轮子转数；蹬踏速率结合实际轮胎半径与车速换算。</summary>
    [SerializeField] private float wheelTurnsPerPedalCycle = 1.7f;
    [SerializeField] private float doorDropHeight = 13f;
    [SerializeField] private float doorDropDuration = 0.72f;
    #endregion

    #region 本局状态
    private EventSubscriber subscriber;
    private bool initialized, introStarted, actorMounting, actorRiding, actorArrived;
    private bool rivalRiding, rivalArrived, rivalWaiting, reversed, finishStarted;
    private bool actorReversed, actorFalling, fallAreaReached;
    private bool raceStarted, caughtUp;
    private float actorSpeed, rivalSpeed, raceStartX, playerGroundY, rivalGroundY;
    private float actorFallSpeed, actorFallGravity;
    private Vector3 doorGroundPosition;
    private Quaternion doorGroundRotation;
    private HintBubble dialogue;
    private float shakeElapsed = 1f;
    private Vector3 appliedShake;

    /// <summary>仅当前有效实例、正常游戏且未进入结算时推进本关演出。</summary>
    public bool IsPlaying => initialized && PlayCtrl.Instance != null
        && PlayCtrl.Instance.LevelCtrl == levelCtrl
        && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING)
        && !PlayCtrl.Instance.Level.CurrentLevelState.IsAttemptTransitioning
        && !actor.IsDied && !levelCtrl.IsActorEnterDoor && !fallAreaReached;

    /// <summary>必须双方正在骑行、对手仍向前运动且尚未到终点，才能消耗重来箭头。</summary>
    public bool CanUseReverse => IsPlaying && actorRiding && rivalRiding
        && !reversed && !actorReversed && !rivalArrived && !actorArrived && rivalSpeed > 0.01f;

    /// <summary>供本关 HUD 将移动中的车轮投影到屏幕，不使用 UI 相机推断场景坐标。</summary>
    public Camera LevelCamera => levelCamera;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        doorGroundPosition = exitDoor.position;
        doorGroundRotation = exitDoor.rotation;
        exitTrigger.enabled = false;
        exitDoor.position = doorGroundPosition + Vector3.up * doorDropHeight;
        exitDoor.gameObject.SetActive(false);
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<PlayerLeftMoveEvent>(OnMove);
        subscriber.Subscribe<PlayerRightMoveEvent>(OnMove);
        subscriber.Subscribe<PlayerJumpEvent>(OnJump);
        subscriber.Subscribe<OnTriggerFailAreaEvent>(OnFallAreaReached);
    }

    private IEnumerator Start()
    {
        // ActorCtrl 在 Start 缓存 Rigidbody，不能在本控制器 Awake 中启动动作。
        while (actor.RB == null || rival.RB == null) yield return null;
        yellowBike.radius = WheelRadius(yellowBike.rearWheel);
        redBike.radius = WheelRadius(redBike.rearWheel);
        actorFallGravity = Mathf.Abs(Physics2D.gravity.y * actor.RB.gravityScale);
        playerGroundY = actor.transform.position.y;
        rivalGroundY = rival.transform.position.y;
        rival.PlayRaceAnimation("idle");
        rival.FaceRight(false);
        initialized = true;
    }

    private void OnMove(PlayerLeftMoveEvent evt) => BeginIntro();
    private void OnMove(PlayerRightMoveEvent evt) => BeginIntro();
    private void OnJump(PlayerJumpEvent evt) => BeginIntro();

    private void OnFallAreaReached(OnTriggerFailAreaEvent evt)
    {
        if (!IsPlaying) return;
        // 实际碰到原 FallArea 后封锁玩法，保留下坠表现；失败与重开仍由 BaseLevelCtrl 处理。
        fallAreaReached = true;
    }

    private void BeginIntro()
    {
        if (!IsPlaying || introStarted || !levelCtrl.ActorCtrlable) return;
        introStarted = true;
        StartCoroutine(Intro());
    }

    private void Update()
    {
        // 在原相机 LateUpdate 之前移除上一帧震屏偏移，防止跟随把偏移累计进位置。
        if (appliedShake != Vector3.zero)
        {
            levelCamera.transform.position -= appliedShake;
            appliedShake = Vector3.zero;
        }
        if (!initialized || PlayCtrl.Instance == null || PlayCtrl.Instance.LevelCtrl != levelCtrl) return;
        bool playing = IsPlaying;
        // 进场和重建过渡只关闭交互，不把待机、掉落或死亡动画当作真正暂停。
        bool paused = PlayCtrl.Instance.CheckState(GamePlayStateName.PAUSE)
            && !PlayCtrl.Instance.Level.CurrentLevelState.IsAttemptTransitioning;
        actor.PauseRaceAnimation(paused);
        rival.PauseRaceAnimation(paused);
        if (!playing)
        {
            // 原失败流程等待期间继续画面运动，不再触发追赶阶段、下车或通关。
            if (fallAreaReached && !paused)
            {
                if (actorFalling) AdvancePlayerBackwards(Time.deltaTime);
                if (rivalRiding) AdvanceBike(redBike, rival, rivalSpeed * Time.deltaTime, Time.deltaTime);
            }
            return;
        }
        if (shakeElapsed < 0.25f) shakeElapsed += Time.deltaTime;

        if (!actorMounting && !actorRiding && !actorArrived)
        {
            // 只记站稳时的地面高度，不能把跳跃接车的位置作为赛后落点高度。
            if (actor.IsGround && Mathf.Abs(actor.RB.linearVelocity.y) < 0.1f)
                playerGroundY = actor.transform.position.y;
            float height = actor.transform.position.y - playerGroundY;
            if (introStarted && Mathf.Abs(actor.transform.position.x - yellowBike.root.position.x) <= mountDistance
                && height >= -0.5f && height <= mountHeight)
                StartCoroutine(MountPlayer());
        }

        UpdateRace(Time.deltaTime);
        if (actorArrived && (reversed || rivalWaiting) && !finishStarted)
        {
            finishStarted = true;
            StartCoroutine(FinishRace());
        }
    }

    private void LateUpdate()
    {
        if (!initialized || shakeElapsed >= 0.25f) return;
        float t = Mathf.Clamp01(shakeElapsed / 0.25f);
        appliedShake = Vector3.up * (Mathf.Sin(t * Mathf.PI * 10f) * 0.075f * (1f - t));
        levelCamera.transform.position += appliedShake;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        StopAllCoroutines();
        EventsUtils.ResetEvents(ref subscriber);
        if (levelCamera != null) levelCamera.transform.position -= appliedShake;
        appliedShake = Vector3.zero;
        if (dialogue != null) Destroy(dialogue.gameObject);
    }
    #endregion

    #region 开场与自动上下车
    private IEnumerator Intro()
    {
        rival.FaceRight(actor.transform.position.x >= rival.transform.position.x);
        yield return WaitPlaying(0.3f);
        ShowDialogue("S_LevelTips_Des_level_147");
        yield return WaitPlaying(1.65f);
        CloseDialogue();
        rival.FaceRight(true);
        rival.PlayRaceAnimation("walk01");
        Vector3 destination = new Vector3(redBike.root.position.x - 0.7f, rivalGroundY, rival.transform.position.z);
        yield return WalkTo(rival, destination, 4.8f);
        yield return JumpTo(rival, redBike.Seat);
        rival.PlayRaceAnimation("bike_ride");
        AudioManager.PlaySound("getKey");
        rivalRiding = true;
        rivalSpeed = soloSpeed;
    }

    private IEnumerator MountPlayer()
    {
        actorMounting = true;
        levelCtrl.SetCtrlable(false);
        actor.BeginScriptedMotion();
        actor.FaceRight(true);
        yield return JumpTo(actor, yellowBike.Seat);
        actor.PlayRaceAnimation("bike_ride");
        AudioManager.PlaySound("getKey");
        actorRiding = true;
        actorMounting = false;
    }

    private IEnumerator JumpTo(Level147ActorCtrl rider, Vector3 destination)
    {
        Vector3 start = rider.transform.position;
        rider.PlayRaceAnimation("jump_up", false);
        bool descending = false;
        yield return Animate(jumpDuration, t =>
        {
            rider.transform.position = Vector3.Lerp(start, destination, t)
                + Vector3.up * (4f * t * (1f - t) * jumpHeight);
            if (t >= 0.5f && !descending)
            {
                descending = true;
                rider.PlayRaceAnimation("jump_down", false);
            }
        });
        rider.transform.position = destination;
    }

    private IEnumerator RivalDismount()
    {
        yield return JumpTo(rival, new Vector3(redBike.root.position.x + 1.85f, rivalGroundY, rival.transform.position.z));
        rival.FaceRight(false);
        rival.PlayRaceAnimation("idle");
        rivalWaiting = true;
    }
    #endregion

    #region 速度、追赶与车轮
    private void UpdateRace(float dt)
    {
        if (actorRiding && rivalRiding && !raceStarted)
        {
            raceStarted = true;
            raceStartX = yellowBike.root.position.x;
        }

        float actorFinishX = finishLine.position.x + 1.1f;
        float rivalFinishX = finishLine.position.x + 2.7f;
        float gap = redBike.root.position.x - yellowBike.root.position.x;
        float progress = Mathf.InverseLerp(raceStartX, actorFinishX, yellowBike.root.position.x);
        if (raceStarted && gap < -3.5f) caughtUp = true;

        if (actorRiding)
        {
            if (actorReversed)
            {
                AdvancePlayerBackwards(dt);
            }
            else
            {
                float target = !raceStarted && !rivalArrived ? soloSpeed : raceSpeed;
                if (raceStarted && !caughtUp && !reversed && !rivalArrived) target = catchUpSpeed;
                actorSpeed = Mathf.MoveTowards(actorSpeed, target, acceleration * dt);
                AdvanceBike(yellowBike, actor, Mathf.Min(actorSpeed * dt, actorFinishX - yellowBike.root.position.x), dt);
                if (yellowBike.root.position.x >= actorFinishX - 0.001f)
                {
                    actorRiding = false;
                    actorArrived = true;
                    actorSpeed = 0;
                    actor.SetPedalSpeed(0);
                    actorSortingGroup.sortingOrder = 212;
                }
            }
        }

        if (!rivalRiding) return;
        if (reversed)
            rivalSpeed = -Mathf.Max(raceSpeed, Mathf.Abs(rivalSpeed));
        else
        {
            float target = actorReversed ? raceSpeed : soloSpeed;
            if (raceStarted && !actorReversed)
            {
                // 第一次追上后两轮交替领先，最后四分之一赛程黑箱明显冲刺。
                float desiredGap = !caughtUp ? -4f
                    : progress < 0.28f ? 4.5f : progress < 0.44f ? -4.5f
                    : progress < 0.60f ? 4.5f : progress < 0.76f ? -4.5f : 5f;
                target = Mathf.Clamp(actorSpeed + (desiredGap - gap) * 1.7f, soloSpeed, rivalSprintSpeed);
                if (progress >= 0.76f) target = rivalSprintSpeed;
            }
            rivalSpeed = Mathf.MoveTowards(rivalSpeed, target, acceleration * dt);
        }
        float distance = rivalSpeed * dt;
        if (!reversed) distance = Mathf.Min(distance, rivalFinishX - redBike.root.position.x);
        AdvanceBike(redBike, rival, distance, dt);
        if (!reversed && redBike.root.position.x >= rivalFinishX - 0.001f)
        {
            rivalRiding = false;
            rivalArrived = true;
            rivalSpeed = 0;
            StartCoroutine(RivalDismount());
        }
    }

    private void AdvancePlayerBackwards(float dt)
    {
        actorSpeed = -Mathf.Max(raceSpeed, Mathf.Abs(actorSpeed));
        AdvanceBike(yellowBike, actor, actorSpeed * dt, dt);
        // 向左倒退时前轮最后离开地面；两轮都越过真实碰撞边缘后，整车才开始下坠。
        if (!actorFalling && yellowBike.frontWheel.bounds.center.x < leftGround.bounds.min.x)
            actorFalling = true;
        if (!actorFalling) return;
        actorFallSpeed += actorFallGravity * dt;
        yellowBike.root.position += Vector3.down * (actorFallSpeed * dt);
        actor.transform.position = yellowBike.Seat;
    }

    private static float WheelRadius(SpriteRenderer wheel)
        => Mathf.Max(0.01f, wheel.sprite.bounds.extents.y * Mathf.Abs(wheel.transform.lossyScale.y));

    private void AdvanceBike(Bike bike, Level147ActorCtrl rider, float distance, float dt)
    {
        bike.root.position += Vector3.right * distance;
        float degrees = -distance / bike.radius * Mathf.Rad2Deg;
        bike.rearWheel.transform.Rotate(0, 0, degrees, Space.Self);
        bike.frontWheel.transform.Rotate(0, 0, degrees, Space.Self);
        rider.transform.position = bike.Seat;
        float speed = dt > 0f ? Mathf.Abs(distance) / dt : 0f;
        rider.SetPedalSpeed(speed / (2f * Mathf.PI * bike.radius * wheelTurnsPerPedalCycle));
    }

    /// <summary>HUD 松手命中最近的车轮后只消费一次；红车倒退解题，黄车倒退坠崖，动画均保持正向。</summary>
    public bool TryUseReverse(Vector2 screenPosition, out Transform wheelTarget)
    {
        wheelTarget = null;
        if (!CanUseReverse) return false;
        Bike selectedBike = null;
        SpriteRenderer selectedWheel = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < 4; i++)
        {
            Bike bike = i < 2 ? yellowBike : redBike;
            SpriteRenderer wheel = i % 2 == 0 ? bike.rearWheel : bike.frontWheel;
            Vector3 center = levelCamera.WorldToScreenPoint(wheel.bounds.center);
            Vector3 edge = levelCamera.WorldToScreenPoint(wheel.bounds.center + Vector3.up * bike.radius);
            float screenRadius = Mathf.Max(28f * Screen.height / 1080f, Vector2.Distance(center, edge) * 1.35f);
            float distance = Vector2.Distance(screenPosition, center);
            if (center.z <= 0 || distance > screenRadius || distance >= closestDistance) continue;
            closestDistance = distance;
            selectedBike = bike;
            selectedWheel = wheel;
        }
        if (selectedBike == null) return false;
        if (selectedBike == yellowBike)
        {
            actorReversed = true;
            actorSpeed = -Mathf.Max(raceSpeed, actorSpeed);
            // 侧边区域覆盖崖顶高度，会让玩家还没落下就失败；本分支改由下方原 FallArea 接收。
            leftFallArea.enabled = false;
        }
        else
        {
            reversed = true;
            rivalSpeed = -Mathf.Max(raceSpeed, rivalSpeed);
        }
        wheelTarget = selectedWheel.transform;
        SpawnEffect(pickupEffectPrefab, selectedWheel.bounds.center, 1.25f);
        AudioManager.PlaySound("getStar");
        StartCoroutine(WheelReceived(selectedWheel.transform));
        return true;
    }

    private IEnumerator WheelReceived(Transform wheel)
    {
        Vector3 scale = wheel.localScale;
        yield return Animate(0.32f, t => wheel.localScale = scale * (1f + Mathf.Sin(t * Mathf.PI) * 0.25f));
        wheel.localScale = scale;
    }
    #endregion

    #region 门落下与两种结局
    private IEnumerator FinishRace()
    {
        exitDoor.gameObject.SetActive(true);
        Vector3 above = doorGroundPosition + Vector3.up * doorDropHeight;
        yield return Animate(doorDropDuration, t => exitDoor.position = Vector3.Lerp(above, doorGroundPosition, t * t));
        exitDoor.position = doorGroundPosition;
        shakeElapsed = 0;
        AudioManager.PlaySound("crash");
        yield return Animate(0.2f, t => exitDoor.position = doorGroundPosition + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.15f));
        exitDoor.position = doorGroundPosition;
        yield return JumpTo(actor, new Vector3(yellowBike.root.position.x + 2.2f, playerGroundY, actor.transform.position.z));
        actor.PlayRaceAnimation("idle");

        if (!reversed)
        {
            rival.FaceRight(actor.transform.position.x >= rival.transform.position.x);
            ShowDialogue("S_LevelTips_Des_level_147_01");
            yield return WaitPlaying(1.5f);
            CloseDialogue();
            rival.FaceRight(true);
            var walk = rival.PlayRaceAnimation("walk01");
            if (walk != null) walk.TimeScale = 2f;
            yield return WalkTo(rival, new Vector3(doorGroundPosition.x - 1.15f, rivalGroundY, rival.transform.position.z), 6.8f);
            var kick = rival.PlayRaceAnimation("kick", false);
            if (kick != null) kick.TimeScale = 1.5f;
            yield return WaitPlaying(0.2f / 1.5f);
            SpawnEffect(impactEffectPrefab, doorGroundPosition + new Vector3(-0.6f, -0.6f, 0), 1f);
            AudioManager.PlaySound("trap");
            yield return Animate(0.42f, t =>
            {
                exitDoor.position = doorGroundPosition + new Vector3(16f * t, 13f * t - 3f * t * t, 0);
                exitDoor.rotation = doorGroundRotation * Quaternion.Euler(0, 0, -780f * t);
            });
            exitDoor.gameObject.SetActive(false);
            rival.PlayRaceAnimation("idle");
        }
        else exitTrigger.enabled = true;

        actor.ResumeWalking();
        levelCtrl.SetCtrlable(true);
        // 输掉比赛不派发失败/重开事件，保留原重来按钮供玩家主动重开。
    }

    private void ShowDialogue(string term)
    {
        dialogue = Instantiate(dialoguePrefab, transform);
        dialogue.Show(term, rival.transform, 0);
    }

    private void CloseDialogue()
    {
        if (dialogue == null) return;
        dialogue.CloseStraightly();
        dialogue = null;
    }

    private IEnumerator WalkTo(Level147ActorCtrl rider, Vector3 destination, float speed)
    {
        Vector3 start = rider.transform.position;
        yield return Animate(Vector3.Distance(start, destination) / speed,
            t => rider.transform.position = Vector3.Lerp(start, destination, t));
    }

    private IEnumerator WaitPlaying(float seconds)
    {
        float elapsed = 0;
        while (elapsed < seconds)
        {
            yield return null;
            if (IsPlaying) elapsed += Time.deltaTime;
        }
    }

    private IEnumerator Animate(float seconds, Action<float> apply)
    {
        float elapsed = 0;
        apply(0);
        while (elapsed < seconds)
        {
            yield return null;
            if (!IsPlaying) continue;
            elapsed += Time.deltaTime;
            apply(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, seconds)));
        }
    }

    private void SpawnEffect(ParticleSystem prefab, Vector3 position, float scale)
    {
        ParticleSystem effect = Instantiate(prefab, position, prefab.transform.rotation, transform);
        effect.transform.localScale *= scale;
        effect.gameObject.SetActive(true);
        effect.Play();
        Destroy(effect.gameObject, 4f);
    }
    #endregion
}
