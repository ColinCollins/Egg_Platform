using System;
using System.Collections;
using Bear.EventSystem;
using Game.Events;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using MelenitasDev.SoundsGood;
using UnityEngine;
using AudioManager = Game.Scripts.Common.AudioManager;

/// <summary>145：单选悬吊礼盒、木槌误导及点击角色找出真正钥匙。</summary>
public sealed class Level145Ctrl : MonoBehaviour
{
    #region 场景引用与参数
    [Serializable]
    private sealed class GiftBinding
    {
        /// <summary>本组容器，只在三盒吊落时整体移动。</summary>
        public Transform group;
        /// <summary>礼盒根节点，选中后独立落地。</summary>
        public Transform box;
        /// <summary>本组绳索，选中盒子的绳索也会收回。</summary>
        public Transform rope;
        /// <summary>原场景闭合/打开图；沿用美术相对位置。</summary>
        public SpriteRenderer closed;
        public GameObject opened;
    }
    /// <summary>Inspector 绑定现有基础关卡、角色、角色碰撞体和主相机。</summary>
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private Level145ActorCtrl actor;
    [SerializeField] private Collider2D actorCollider;
    [SerializeField] private Camera levelCamera;
    /// <summary>固定顺序：左木槌、中星星、右狗，不随机。</summary>
    [SerializeField] private GiftBinding[] gifts;
    /// <summary>场景红按钮的命中区域及两张状态图，全部显式绑定。</summary>
    [SerializeField] private Collider2D releaseButtonArea;
    [SerializeField] private GameObject releaseNormal;
    [SerializeField] private GameObject releasePressed;
    /// <summary>原场景两颗收集星，保留各自 index 与存档规则。</summary>
    [SerializeField] private Postcard_Star[] stars;
    /// <summary>两颗星的触发器，飞行完成前关闭，避免提前收集。</summary>
    [SerializeField] private Collider2D[] starColliders;
    /// <summary>原钥匙的显示、碰撞、拾取执行链和漂浮组件，不重写开锁表现。</summary>
    [SerializeField] private Transform key;
    [SerializeField] private SpriteRenderer keyRenderer;
    [SerializeField] private Collider2D keyCollider;
    [SerializeField] private TriggerExecutor keyPickup;
    [SerializeField] private MoveFloatHandle keyFloat;
    /// <summary>本关木槌与 point_hit 挂点，材质和大小在 Inspector 调整。</summary>
    [SerializeField] private SpriteRenderer hammer;
    [SerializeField] private Transform toolAnchor;
    /// <summary>木槌图片中心的落点，相对关卡根节点；对应用户截图中央偏右的红框。</summary>
    [SerializeField] private Vector2 hammerLandingPosition = new Vector2(1.1f, -1.45f);
    /// <summary>仅未拾取木槌上下浮动的幅度和周期；暂停冻结，拾取后停止。</summary>
    [SerializeField] private float hammerFloatAmplitude = 0.12f;
    [SerializeField] private float hammerFloatPeriod = 1.6f;
    /// <summary>066 狗的独立 Prefab 副本，保留狗外观、移动参数和击飞碰撞。</summary>
    [SerializeField] private DogEnemyCtrl dogPrefab;
    /// <summary>出生门、088 最终碎门图、右门锁及已有出口触发器。</summary>
    [SerializeField] private GameObject startDoor;
    [SerializeField] private SpriteRenderer brokenStartDoor;
    [SerializeField] private Transform doorLock;
    [SerializeField] private Collider2D exitDoorCollider;
    /// <summary>原收集星粒子只作开盒遮挡，不调用星星发奖。</summary>
    [SerializeField] private ParticleSystem[] revealEffects;
    /// <summary>沿用成功关门的彩纸参数；本关只播放粒子，不触发成功流程。</summary>
    [SerializeField] private ParticleSystem confettiEffectPrefab;
    /// <summary>113 纯星形敲击爆点与 088 最终 18 片碎门粒子，均为本关副本。</summary>
    [SerializeField] private ParticleSystem hitEffectPrefab;
    [SerializeField] private ParticleSystem doorBreakPrefab;
    /// <summary>吊落和落地参数，落点 Y 按用户截图；礼盒无实体碰撞。</summary>
    [SerializeField] private float hiddenHeight = 11f;
    [SerializeField] private float lowerDuration = 0.85f;
    [SerializeField] private float landedBoxY = -2.1f;
    [SerializeField] private float fallDuration = 0.5f;
    /// <summary>与 113 相同的随机出物时长、抛物线高度、时间曲线。</summary>
    [SerializeField] private Vector2 itemDuration = new Vector2(0.3f, 0.5f);
    [SerializeField] private Vector2 itemHeight = new Vector2(2f, 3f);
    [SerializeField] private AnimationCurve itemTimeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    /// <summary>道具落地中心高度、敲门可交互距离和命中特效延迟，沿用 113 时机。</summary>
    [SerializeField] private float itemGroundY = -1.38f;
    [SerializeField] private float hitDistance = 2.6f;
    [SerializeField] private float hitDelay = 0.18f;
    #endregion

    #region 尝试状态与生命周期
    private enum GiftState { Waiting, Lowering, Choosing, Falling, Grounded, Opening, Opened }
    private GiftState giftState;
    private int selectedGift = -1;
    private readonly Vector3[] groupPositions = new Vector3[3];
    private bool actorBusy, actorShaken, keyRevealed, keyReady, keyTaken;
    private bool hammerReady, hammerHeld, startDoorBroken, dogWaiting, dogChasing;
    private Vector3 hammerRestPosition;
    private float hammerFloatElapsed;
    private bool previousPlaying;
    private float lockAngle;
    private Quaternion lockInitialRotation;
    private Level145GamePanelFeature panel;
    private DogEnemyCtrl dog;
    private EventSubscriber subscriber;
    private Sound searchSound;
    private Sound dogBarkSound;
    // 原17关Prefab的实际等待间隔是0.4秒；不能用音频文件时长作为重复间隔。
    private const float DogBarkInterval = 0.4f;
    private float nextDogBarkTime;
    private bool audioPaused;
    private bool audioFocused = true;
    private Vector3 cameraInitialPosition;

    private bool IsPlaying => PlayCtrl.Instance != null
        && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING)
        && !actor.IsDied && !levelCtrl.IsActorEnterDoor;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        for (int i = 0; i < gifts.Length; i++)
        {
            groupPositions[i] = gifts[i].group.localPosition;
            gifts[i].group.localPosition += Vector3.up * hiddenHeight;
            gifts[i].closed.gameObject.SetActive(true);
            gifts[i].opened.SetActive(false);
        }
        foreach (Postcard_Star star in stars) star.gameObject.SetActive(false);
        key.gameObject.SetActive(false);
        keyFloat.enabled = false;
        hammer.gameObject.SetActive(false);
        brokenStartDoor.gameObject.SetActive(false);
        releaseNormal.SetActive(true);
        releasePressed.SetActive(false);
        exitDoorCollider.enabled = false;
        lockInitialRotation = doorLock.localRotation;
        cameraInitialPosition = levelCamera.transform.localPosition;
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
    }

    private IEnumerator Start()
    {
        // PlayCtrl 先创建关卡再替换 HUD，下一帧只绑定新面板。
        yield return null;
        while (PlayCtrl.Instance != null && PlayCtrl.Instance.CurrentGamePlayPanel == null)
            yield return null;
        if (PlayCtrl.Instance == null) yield break;
        panel = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<Level145GamePanelFeature>();
        if (panel != null) panel.HitRequested += OnHitRequested;
        else Debug.LogError("[Level145] 缺少本关专用交互键。", this);
    }

    private void Update()
    {
        bool playing = IsPlaying;
        UpdateDogBarkSound();
        if (playing != previousPlaying)
        {
            if (dogChasing && dog != null) dog.RB.simulated = playing;
            if (keyReady && !keyTaken) keyFloat.enabled = playing;
            previousPlaying = playing;
        }
        panel?.SetAvailable(playing && hammerHeld && !actorBusy && FindHitDoor() != null);
        if (!playing) return;

        HandleWorldInput();
        if (hammerReady && !hammerHeld)
        {
            hammerFloatElapsed += Time.deltaTime;
            hammer.transform.position = hammerRestPosition + Vector3.up
                * (Mathf.Sin(hammerFloatElapsed * Mathf.PI * 2f / hammerFloatPeriod) * hammerFloatAmplitude);
        }
        if (keyReady && !keyTaken && keyCollider.bounds.Intersects(actorCollider.bounds))
        {
            keyTaken = true;
            keyCollider.enabled = false;
            keyPickup.Execute();
        }
        if (hammerReady && !hammerHeld && !actorBusy && levelCtrl.ActorCtrlable
            && hammer.bounds.Intersects(actorCollider.bounds))
            StartCoroutine(ObtainHammer());
        if (dogWaiting && !actorBusy && levelCtrl.ActorCtrlable
            && (actor.IsMoving || (!actor.IsGround && Mathf.Abs(actor.RB.linearVelocityY) > 0.1f)))
            StartDogChase();
        if (dogChasing && dog != null)
        {
            dog.SetMoveInput(actor.transform.position.x >= dog.transform.position.x ? 1f : -1f);
            dog.OnUpdate();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        StopAllCoroutines();
        EventsUtils.ResetEvents(ref subscriber);
        if (panel != null) panel.HitRequested -= OnHitRequested;
        searchSound?.Stop();
        searchSound = null;
        StopDogBarkSound();
        if (dog != null) Destroy(dog.gameObject);
        if (levelCamera != null) levelCamera.transform.localPosition = cameraInitialPosition;
    }
    #endregion

    #region 场景点击与礼盒单选
    private void HandleWorldInput()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && !InputUtils.IsPointerOverUI(touch.position))
                ClickWorld(touch.position);
        }
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.touchCount == 0 && Input.GetMouseButtonDown(0)
            && !InputUtils.IsPointerOverUI(Input.mousePosition)) ClickWorld(Input.mousePosition);
#endif
    }

    private void ClickWorld(Vector2 screen)
    {
        Vector3 point = levelCamera.ScreenToWorldPoint(screen);
        if (giftState == GiftState.Waiting && releaseButtonArea.OverlapPoint(point))
        {
            giftState = GiftState.Lowering;
            releaseNormal.SetActive(false);
            releasePressed.SetActive(true);
            AudioManager.PlaySound("boardTrigger");
            StartCoroutine(LowerGifts());
            return;
        }
        // 主角与盒子重叠时仍优先允许点击主角解谜。
        if (!actorBusy && !keyRevealed && levelCtrl.ActorCtrlable && actorCollider.OverlapPoint(point))
        {
            StartCoroutine(ClickActor());
            return;
        }
        if (giftState != GiftState.Choosing && giftState != GiftState.Grounded) return;
        for (int i = 0; i < gifts.Length; i++)
        {
            if (giftState == GiftState.Grounded && i != selectedGift) continue;
            Bounds bounds = gifts[i].closed.bounds;
            point.z = bounds.center.z;
            if (!bounds.Contains(point)) continue;
            if (giftState == GiftState.Choosing)
            {
                selectedGift = i;
                giftState = GiftState.Falling;
                StartCoroutine(DropGift(i));
            }
            else
            {
                giftState = GiftState.Opening;
                StartCoroutine(OpenGift());
            }
            return;
        }
    }

    private IEnumerator LowerGifts()
    {
        // 复用58关绳吊门下降音效；三盒共同下放只触发一次，不循环。
        AudioManager.PlaySound("findItems");
        yield return Animate(lowerDuration, t =>
        {
            // 阻尼振荡只作用在悬吊高度，不改变礼盒的美术比例。
            float remaining = Mathf.Exp(-9f * t) * Mathf.Cos(8f * t);
            for (int i = 0; i < gifts.Length; i++)
                gifts[i].group.localPosition = groupPositions[i] + Vector3.up * (hiddenHeight * remaining);
        });
        for (int i = 0; i < gifts.Length; i++) gifts[i].group.localPosition = groupPositions[i];
        giftState = GiftState.Choosing;
    }

    private IEnumerator DropGift(int chosen)
    {
        GiftBinding gift = gifts[chosen];
        Vector3 start = gift.box.position;
        Vector3 end = new Vector3(start.x, landedBoxY, start.z);
        StartCoroutine(RetractGifts(chosen));
        yield return Animate(fallDuration, t => gift.box.position = Vector3.LerpUnclamped(start, end, t * t));
        AudioManager.PlaySound("crash");
        StartCoroutine(ShakeCamera());
        yield return Animate(0.2f, t => gift.box.position = end + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.13f));
        gift.box.position = end;
        giftState = GiftState.Grounded;
    }

    private IEnumerator RetractGifts(int chosen)
    {
        Vector3[] starts = new Vector3[gifts.Length];
        for (int i = 0; i < gifts.Length; i++) starts[i] = gifts[i].rope.position;
        Vector3[] boxes = new Vector3[gifts.Length];
        for (int i = 0; i < gifts.Length; i++) boxes[i] = gifts[i].box.position;
        yield return Animate(0.55f, t =>
        {
            float rise = hiddenHeight * t * t;
            for (int i = 0; i < gifts.Length; i++)
            {
                gifts[i].rope.position = starts[i] + Vector3.up * rise;
                if (i != chosen) gifts[i].box.position = boxes[i] + Vector3.up * rise;
            }
        });
        for (int i = 0; i < gifts.Length; i++)
        {
            gifts[i].rope.gameObject.SetActive(false);
            if (i != chosen) gifts[i].box.gameObject.SetActive(false);
        }
    }

    private IEnumerator OpenGift()
    {
        GiftBinding gift = gifts[selectedGift];
        Quaternion rotation = gift.box.localRotation;
        Vector3 scale = gift.box.localScale;
        yield return Animate(0.4f, t =>
        {
            gift.box.localScale = scale * Mathf.Lerp(1f, 0.78f, t);
            gift.box.localRotation = rotation * Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 12f) * 6f);
        });
        gift.box.localRotation = rotation;
        Vector3 effectPosition = gift.closed.bounds.center;
        foreach (ParticleSystem effect in revealEffects) SpawnEffect(effect, effectPosition, 1.8f);
        SpawnEffect(confettiEffectPrefab, effectPosition);
        yield return Animate(0.42f, t =>
        {
            gift.box.localScale = scale * (1f - 0.22f * Mathf.Exp(-6f * t) * Mathf.Cos(3f * Mathf.PI * t));
            // 爆点长到足以遮图后切换，打开图继续同一段弹性放大。
            if (t >= 0.19f && gift.closed.gameObject.activeSelf)
            {
                gift.closed.gameObject.SetActive(false);
                gift.opened.SetActive(true);
                AudioManager.PlaySound("getStar");
            }
        });
        gift.box.localScale = scale;
        Vector3 origin = gift.box.position + Vector3.up * 0.45f;
        if (selectedGift == 0)
        {
            hammer.gameObject.SetActive(true);
            // 槌柄 pivot 不在图中心；倾斜后补偿 X/Y，让图片中心落在指定红框位置。
            Vector3 centerOffset = hammer.transform.TransformVector(hammer.sprite.bounds.center);
            Vector3 landing = transform.TransformPoint(new Vector3(hammerLandingPosition.x, hammerLandingPosition.y, 0)) - centerOffset;
            yield return JumpOut(hammer.transform, origin, landing);
            hammerRestPosition = landing;
            hammerFloatElapsed = 0f;
            hammerReady = true;
        }
        else if (selectedGift == 1)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                Postcard_Star star = stars[i];
                star.gameObject.SetActive(true);
                if (!star.RefreshSavedCollectionState()) continue;
                starColliders[i].enabled = false;
                StartCoroutine(RevealStar(i, origin, new Vector3(origin.x + (i == 0 ? -2.2f : 2.2f), itemGroundY, 0)));
            }
        }
        else yield return RevealDog(origin);
        giftState = GiftState.Opened;
    }

    private IEnumerator RevealStar(int index, Vector3 start, Vector3 end)
    {
        yield return JumpOut(stars[index].transform, start, end);
        starColliders[index].enabled = true;
    }
    #endregion

    #region 狗、持槌与门
    private IEnumerator RevealDog(Vector3 start)
    {
        dog = Instantiate(dogPrefab, transform);
        dog.transform.position = start;
        dog.gameObject.SetActive(true);
        // ActorCtrl 在 Start 才缓存 RB/Body；动态出盒必须等它初始化，Prefab 已关闭物理。
        yield return null;
        dog.RB.simulated = false;
        float side = actor.transform.position.x >= start.x ? 1f : -1f;
        yield return JumpOut(dog.transform, start, new Vector3(start.x + side * 1.35f, -2.07f, 0));
        bool faceRight = actor.transform.position.x >= dog.transform.position.x;
        if (dog.FaingRight != faceRight) dog.SetFaceDir(faceRight);
        dog.SetMoveInput(0);
        dog.Bark();
        dog.SpineCtrl.PlayAnimation("bark", true);
        dogWaiting = true;
        nextDogBarkTime = Time.time + DogBarkInterval;
    }

    private void StartDogChase()
    {
        dogWaiting = false;
        StopDogBarkSound();
        dog.StopBark();
        dog.RB.simulated = true;
        dog.ResumeMoving();
        dogChasing = true;
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (!IsPlaying || dog == null) return;
        if (dogWaiting && !actorBusy && levelCtrl.ActorCtrlable)
        {
            StartDogChase();
            // 待机时没有推进狗的物理探测，先刷新接地再接入原跟跳。
            dog.OnUpdate();
        }
        if (dogChasing) dog.TriggerJump();
    }

    private IEnumerator ObtainHammer()
    {
        hammerHeld = true;
        BeginActorAction();
        AudioManager.PlaySound("getKey");
        hammer.transform.SetParent(toolAnchor, false);
        hammer.transform.localPosition = Vector3.zero;
        // 木槌图片的槌头朝 +Y；point_hit 的挥击资源朝向相反，绕握柄校正后再随骨骼挥动。
        hammer.transform.localRotation = Quaternion.Euler(0, 0, 180f);
        bool complete = false;
        actor.PlayLevelAction("obtain", false, () => complete = true);
        while (!complete || !IsPlaying) yield return null;
        EndActorAction();
    }

    private Transform FindHitDoor()
    {
        Transform nearest = null;
        float distance = hitDistance;
        if (!startDoorBroken)
        {
            float d = Vector2.Distance(actor.transform.position, startDoor.transform.position);
            if (d <= distance) { nearest = startDoor.transform; distance = d; }
        }
        if (!keyTaken && doorLock.gameObject.activeInHierarchy && Vector2.Distance(actor.transform.position, doorLock.position) <= distance)
            nearest = doorLock;
        return nearest;
    }

    private void OnHitRequested()
    {
        if (!IsPlaying || !hammerHeld || actorBusy) return;
        Transform target = FindHitDoor();
        if (target != null) StartCoroutine(HitDoor(target));
    }

    private IEnumerator HitDoor(Transform target)
    {
        BeginActorAction();
        bool faceRight = target.position.x >= actor.transform.position.x;
        if (actor.FaingRight != faceRight) actor.SetFaceDir(faceRight);
        bool complete = false;
        actor.PlayLevelAction("hit", false, () => complete = true);
        yield return WaitPlaying(hitDelay);
        SpawnEffect(hitEffectPrefab, target.position + new Vector3(-0.35f, 0.25f, 0));
        if (target == startDoor.transform)
        {
            startDoorBroken = true;
            startDoor.SetActive(false);
            brokenStartDoor.gameObject.SetActive(true);
            SpawnEffect(doorBreakPrefab, brokenStartDoor.bounds.center);
            AudioManager.PlaySound("blockByDoor");
        }
        else
        {
            AudioManager.PlaySound("knockDoor");
            float from = lockAngle;
            lockAngle = lockAngle > 0 ? -15f : 15f;
            yield return Animate(0.16f, t => doorLock.localRotation = lockInitialRotation
                * Quaternion.Euler(0, 0, Mathf.Lerp(from, lockAngle, Mathf.SmoothStep(0, 1, t))));
        }
        while (!complete || !IsPlaying) yield return null;
        EndActorAction();
    }
    #endregion

    #region 点击角色与真正钥匙
    private IEnumerator ClickActor()
    {
        BeginActorAction();
        if (!actorShaken)
        {
            actorShaken = true;
            Quaternion rotation = actor.Body.localRotation;
            yield return Animate(0.32f, t => actor.Body.localRotation = rotation
                * Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 6) * 7f * (1f - t)));
            actor.Body.localRotation = rotation;
            EndActorAction();
            yield break;
        }
        keyRevealed = true;
        // 044 原顺序：touching 1.5 秒，throwing 后 0.3 秒音效，再等待 0.2 秒出物。
        actor.PlayLevelAction("touching", true);
        searchSound = AudioManager.PlaySound("findItems", loop: true);
        yield return WaitPlaying(1.5f);
        searchSound?.Stop();
        searchSound = null;
        actor.PlayLevelAction("throwing", false);
        yield return WaitPlaying(0.3f);
        AudioManager.PlaySound("throwItem");
        yield return WaitPlaying(0.2f);
        Vector3 start = actor.transform.position + new Vector3(actor.FaingRight ? -0.35f : 0.35f, 0.6f, 0);
        Vector3 end = new Vector3(transform.position.x, itemGroundY, 0);
        Vector3 scale = key.localScale;
        int keySortingOrder = keyRenderer.sortingOrder;
        // Actor 为 Stuff/200：出物期间放到背后，落地后恢复原钥匙前景层级。
        keyRenderer.sortingOrder = 199;
        key.position = start;
        key.localScale = Vector3.zero;
        keyRenderer.color = new Color(1, 1, 1, 0);
        keyCollider.enabled = false;
        key.gameObject.SetActive(true);
        EndActorAction();
        yield return JumpOut(key, start, end, t =>
        {
            float show = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.6f));
            key.localScale = scale * show;
            keyRenderer.color = new Color(1, 1, 1, show);
        });
        keyCollider.enabled = true;
        keyRenderer.sortingOrder = keySortingOrder;
        keyFloat.enabled = true;
        keyReady = true;
    }

    private void BeginActorAction()
    {
        actorBusy = true;
        levelCtrl.SetCtrlable(false);
        actor.BeginLevelAction();
    }

    private void EndActorAction()
    {
        actorBusy = false;
        actor.FinishLevelAction();
        if (!actor.IsDied && !levelCtrl.IsActorEnterDoor)
        {
            levelCtrl.SetCtrlable(true);
            actor.ResumeMoving();
        }
    }
    #endregion

    #region 本关表现与暂停计时
    // 沿用17关等待狗叫：每0.4秒单次播放并随机音高；暂停等中断后重新等一个间隔，不补播。
    private void UpdateDogBarkSound()
    {
        if (!isActiveAndEnabled || !IsPlaying || audioPaused || !audioFocused || !dogWaiting || dog == null)
        {
            StopDogBarkSound();
            nextDogBarkTime = Time.time + DogBarkInterval;
            return;
        }
        if (Time.time < nextDogBarkTime) return;
        dogBarkSound = AudioManager.PlaySound("bark", randomPitch: true);
        nextDogBarkTime = Time.time + DogBarkInterval;
    }

    private void StopDogBarkSound()
    {
        dogBarkSound?.Stop();
        dogBarkSound = null;
    }

    private void OnApplicationPause(bool paused)
    {
        audioPaused = paused;
        if (Application.isPlaying) UpdateDogBarkSound();
    }

    private void OnApplicationFocus(bool focused)
    {
        audioFocused = focused;
        if (Application.isPlaying) UpdateDogBarkSound();
    }

    private IEnumerator JumpOut(Transform item, Vector3 start, Vector3 end, Action<float> progress = null)
    {
        float duration = UnityEngine.Random.Range(itemDuration.x, itemDuration.y);
        float height = UnityEngine.Random.Range(itemHeight.x, itemHeight.y);
        item.position = start;
        // 与 ItemJumpOutExecutor 相同的时间曲线和 4*t*(1-t) 抛物线；本关额外遵守游戏暂停。
        yield return Animate(duration, t =>
        {
            float eased = itemTimeCurve.Evaluate(t);
            item.position = Vector3.LerpUnclamped(start, end, eased) + Vector3.up * (4 * eased * (1 - eased) * height);
            progress?.Invoke(t);
        });
        item.position = end;
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
            apply(Mathf.Clamp01(elapsed / seconds));
        }
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

    private IEnumerator ShakeCamera()
    {
        yield return Animate(0.18f, t => levelCamera.transform.localPosition = cameraInitialPosition
            + Vector3.up * (Mathf.Sin(t * Mathf.PI * 8) * 0.035f * (1 - t)));
        levelCamera.transform.localPosition = cameraInitialPosition;
    }

    private void SpawnEffect(ParticleSystem prefab, Vector3 position, float scaleMultiplier = 1f)
    {
        ParticleSystem effect = Instantiate(prefab, position, prefab.transform.rotation, transform);
        effect.transform.localScale *= scaleMultiplier;
        effect.gameObject.SetActive(true);
        effect.Play(prefab != hitEffectPrefab && prefab != doorBreakPrefab);
        Destroy(effect.gameObject, 5f);
    }
    #endregion
}
