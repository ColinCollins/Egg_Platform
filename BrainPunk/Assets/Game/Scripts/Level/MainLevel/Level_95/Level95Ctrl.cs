using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using Sound = MelenitasDev.SoundsGood.Sound;

/// <summary>
/// Level095 专用控制器。
/// 保留 Level024 的潜水入口，并让开场门在角色离开后成为可推动、可点击和可吸水的机关。
/// </summary>
public sealed class Level95Ctrl : MonoBehaviour
{
    #region Inspector

    private const string DoorIdleAnimName = "door_idle";
    private const string DoorOpenAnimName = "door_open";
    private const string DoorOpenIdleAnimName = "door_open_idle";
    private const string DoorCloseAnimName = "dose_close";
    private const string SuctionAnimName = "Inhalation_effects";

    [Header("Level Refs")]
    [Tooltip("本关角色，用于响应潜水事件并确认角色已经离开入口门碰撞范围。")]
    [SerializeField] private ActorCtrl actor;

    [Tooltip("需要被吸干的完整 Water 根节点。")]
    [SerializeField] private Transform water;

    [Tooltip("Water 下的浮力区域，用于判断入口门是否已经进入水中。")]
    [SerializeField] private Collider2D waterVolume;

    [Tooltip("入口门点击坐标换算使用的关卡相机。")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("本关入口门；开场结束后会在运行时切换为可推动刚体。")]
    [SerializeField] private Transform startDoor;

    [Header("Drain")]
    [Tooltip("吸水完成时 Water 根节点的本地 Y。")]
    [SerializeField] private float drainedWaterLocalY = -8f;

    [Tooltip("入口门持续吸干 Water 的时长。")]
    [SerializeField] private float drainDuration = 4f;

    [Tooltip("是否水平镜像从成功门复制出的吸入 Spine 表现。")]
    [SerializeField] private bool mirrorSuctionEffect = true;

    #endregion

    #region Runtime state

    private BaseLevelCtrl _baseLevelCtrl;
    private Collider2D _actorCollider;
    private ActorSpineCtrl _doorSpine;
    private ActorSpineCtrl _suctionSpine;
    private BoxCollider2D _doorCollider;
    private Rigidbody2D _doorBody;
    private GameObject _suctionEffect;
    private Transform _downButton;
    private EventSubscriber _subscriber;
    private Tween _drainTween;
    private Sound _doorSound;
    private Sound _drainSound;

    private bool _isGameplayActive;
    private bool _doorPhysicsReady;
    private bool _doorClickInProgress;
    private bool _isDraining;

    #endregion

    #region Unity lifecycle

    private void Awake()
    {
        ResolveReferences();
        DisableLegacyDrainObjects();
        CreateSuctionEffect();
        AddListeners();
    }

    private void Start()
    {
        ApplyGameplayState(IsPlayingState(), true);

        ShowDiveButtonAsync().Forget();
        EnableDoorPhysicsWhenReadyAsync().Forget();
    }

    private void Update()
    {
        RefreshGameplayState();

        if (!CanClickDoor())
            return;

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began &&
                    !InputUtils.IsPointerOverUI(touch.fingerId) &&
                    TryClickDoor(touch.position))
                {
                    return;
                }
            }

            return;
        }

        if (Input.GetMouseButtonDown(0) &&
            !InputUtils.IsPointerOverUI((Vector2)Input.mousePosition))
        {
            TryClickDoor(Input.mousePosition);
        }
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);

        if (_drainTween != null && _drainTween.IsActive())
            _drainTween.Kill();

        _drainTween = null;
        StopSound(ref _doorSound);
        StopSound(ref _drainSound);
    }

    #endregion

    #region References and setup

    private void ResolveReferences()
    {
        _baseLevelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= _baseLevelCtrl != null ? _baseLevelCtrl.Actor : null;
        actor ??= GetComponentInChildren<ActorCtrl>(true);
        _actorCollider ??= actor != null ? actor.GetComponent<Collider2D>() : null;
        water ??= transform.Find("Water");
        startDoor ??= transform.Find("StartDoor");
        mainCamera ??= GetComponentInChildren<Camera>(true);

        if (waterVolume == null && water != null)
            waterVolume = water.Find("Buoyancy")?.GetComponent<Collider2D>();

        if (startDoor == null)
            return;

        _doorSpine ??= startDoor.GetComponent<ActorSpineCtrl>();
        _doorCollider ??= startDoor.GetComponent<BoxCollider2D>();
        _doorBody ??= startDoor.GetComponent<Rigidbody2D>();

        if (_doorBody != null && !_doorPhysicsReady)
        {
            _doorBody.bodyType = RigidbodyType2D.Kinematic;
            _doorBody.simulated = true;
        }
    }

    private void DisableLegacyDrainObjects()
    {
        Transform outlet = transform.Find("Outlet");
        Transform cork = outlet != null
            ? outlet.Find("Cork")
            : transform.Find("Cork");
        if (cork != null)
            cork.gameObject.SetActive(false);

        if (outlet != null)
            outlet.gameObject.SetActive(false);

        Transform waterOut = outlet != null
            ? outlet.Find("WaterOut")
            : transform.Find("WaterOut");
        if (waterOut != null)
            waterOut.gameObject.SetActive(false);
    }

    private void AddListeners()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerDiveEvent>(OnPlayerDive);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
    }

    #endregion

    #region Events and gameplay state

    private void OnPlayerDive(PlayerDiveEvent evt)
    {
        actor?.AddDiveVelocity();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        ApplyGameplayState(evt.NewState.Equals(GamePlayStateName.PLAYING));
    }

    private void RefreshGameplayState()
    {
        ApplyGameplayState(IsPlayingState());
    }

    private void ApplyGameplayState(bool isActive, bool force = false)
    {
        if (!force && _isGameplayActive == isActive)
            return;

        _isGameplayActive = isActive;

        if (_drainTween != null && _drainTween.IsActive())
        {
            if (isActive)
                _drainTween.Play();
            else
                _drainTween.Pause();
        }

        if (_doorBody != null && _doorPhysicsReady)
        {
            _doorBody.simulated = isActive;
            if (isActive)
                IgnoreDoorBuoyancy();
        }

        SetSpinePlayback(_doorSpine, isActive);
        SetSpinePlayback(_suctionSpine, isActive);
        SetSoundPlayback(_doorSound, isActive);
        SetSoundPlayback(_drainSound, isActive);
    }

    private async UniTask ShowDiveButtonAsync()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        if (playCtrl == null)
            return;

        await UniTask.WaitUntil(
            () => playCtrl.CurrentGamePlayPanel != null,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        _downButton = playCtrl.CurrentGamePlayPanel.transform.Find("Root/down_btn");
        if (_downButton == null)
        {
            Debug.LogWarning("[Level95Ctrl] Dive button was not found in GamePlayPanel_005.", this);
            return;
        }

        _downButton.gameObject.SetActive(false);
        await UniTask.WaitForSeconds(
            2f,
            ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        if (_downButton != null)
            _downButton.gameObject.SetActive(true);
    }

    #endregion

    #region Pushable door

    private async UniTask EnableDoorPhysicsWhenReadyAsync()
    {
        await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
        await UniTask.WaitUntil(
            CanEnableDoorPhysics,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        ConfigurePushableDoor();
    }

    private bool CanEnableDoorPhysics()
    {
        bool entranceFinished = _baseLevelCtrl == null || _baseLevelCtrl.ActorCtrlable;
        return entranceFinished && IsPlayingState() && IsActorClearOfDoor();
    }

    private bool IsActorClearOfDoor()
    {
        if (_doorCollider == null || _actorCollider == null ||
            !_doorCollider.enabled || !_actorCollider.enabled)
        {
            return true;
        }

        ColliderDistance2D distance = Physics2D.Distance(_doorCollider, _actorCollider);
        return !distance.isOverlapped && distance.distance > 0.02f;
    }

    private bool IsPlayingState()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        return playCtrl == null || playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    private void ConfigurePushableDoor()
    {
        ResolveReferences();
        if (startDoor == null || _doorCollider == null || _doorBody == null)
        {
            Debug.LogError("[Level95Ctrl] StartDoor, BoxCollider2D, or Rigidbody2D is missing.", this);
            return;
        }

        int stuffLayer = LayerMask.NameToLayer("Stuff");
        if (stuffLayer < 0)
        {
            Debug.LogError("[Level95Ctrl] Stuff layer is missing.", this);
            return;
        }

        _doorBody.simulated = false;
        startDoor.gameObject.layer = stuffLayer;
        _doorCollider.isTrigger = false;
        _doorBody.bodyType = RigidbodyType2D.Dynamic;

        _doorPhysicsReady = true;
        _doorBody.simulated = IsPlayingState();
        IgnoreDoorBuoyancy();
    }

    private void IgnoreDoorBuoyancy()
    {
        if (_doorCollider == null || waterVolume == null ||
            !_doorCollider.enabled || !waterVolume.enabled ||
            !_doorCollider.gameObject.activeInHierarchy ||
            !waterVolume.gameObject.activeInHierarchy)
        {
            return;
        }

        Physics2D.IgnoreCollision(_doorCollider, waterVolume, true);
    }

    #endregion

    #region Door input and animation

    private bool CanClickDoor()
    {
        return _doorPhysicsReady &&
               !_doorClickInProgress &&
               !_isDraining &&
               IsPlayingState() &&
               (_baseLevelCtrl == null || _baseLevelCtrl.ActorCtrlable);
    }

    private bool TryClickDoor(Vector3 screenPosition)
    {
        Camera targetCamera = mainCamera != null ? mainCamera : Camera.main;
        if (targetCamera == null || startDoor == null)
            return false;

        Vector2 worldPoint = targetCamera.ScreenToWorldPoint(screenPosition);
        RaycastHit2D[] hits = Physics2D.RaycastAll(worldPoint, Vector2.zero);

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
            if (hitTransform == null ||
                (hitTransform != startDoor && !hitTransform.IsChildOf(startDoor)))
            {
                continue;
            }

            OnDoorClicked();
            return true;
        }

        return false;
    }

    private void OnDoorClicked()
    {
        _doorClickInProgress = true;

        if (IsDoorInWater())
        {
            StartDrain();
            return;
        }

        PlayDoorOpenOnceAsync().Forget();
    }

    private bool IsDoorInWater()
    {
        if (_doorCollider == null || waterVolume == null ||
            !_doorCollider.enabled || !waterVolume.enabled ||
            !waterVolume.gameObject.activeInHierarchy)
        {
            return false;
        }

        ColliderDistance2D distance = Physics2D.Distance(_doorCollider, waterVolume);
        return distance.isValid &&
               (distance.isOverlapped || distance.distance <= 0.02f);
    }

    private async UniTask PlayDoorOpenOnceAsync()
    {
        try
        {
            PlayDoorSound("openDoor");
            _doorSpine?.PlayAnimation(DoorOpenAnimName, false, false);

            await WaitForGameplaySecondsAsync(0.8f);

            _doorSpine?.PlayAnimation(DoorCloseAnimName, false, false);
            await WaitForGameplaySecondsAsync(0.25f);

            PlayDoorSound("closeDoor");
            await WaitForGameplaySecondsAsync(0.2f);

            _doorSpine?.PlayAnimation(DoorIdleAnimName, true, false);
        }
        finally
        {
            _doorClickInProgress = false;
        }
    }

    private async UniTask WaitForGameplaySecondsAsync(float seconds)
    {
        float remainingSeconds = Mathf.Max(0f, seconds);
        while (remainingSeconds > 0f)
        {
            await UniTask.Yield(
                PlayerLoopTiming.Update,
                this.GetCancellationTokenOnDestroy());

            if (IsPlayingState())
                remainingSeconds -= Time.deltaTime;
        }
    }

    #endregion

    #region Drain flow

    private void StartDrain()
    {
        if (_isDraining || water == null)
        {
            _doorClickInProgress = false;
            return;
        }

        _isDraining = true;
        FreezeDoorForDrain();

        PlayDoorSound("openDoor");
        _doorSpine?.PlayAnimation(DoorOpenAnimName, false, false);
        _doorSpine?.AnimationState?.AddAnimation(0, DoorOpenIdleAnimName, true, 0f);

        SetSuctionEffectActive(true);
        StopSound(ref _drainSound);
        _drainSound = AudioManager.PlaySound("drainage");

        _drainTween = water
            .DOLocalMoveY(drainedWaterLocalY, Mathf.Max(0f, drainDuration))
            .OnComplete(OnWaterDrained);
    }

    private void FreezeDoorForDrain()
    {
        if (_doorBody == null)
            return;

        _doorBody.linearVelocity = Vector2.zero;
        _doorBody.angularVelocity = 0f;
        _doorBody.bodyType = RigidbodyType2D.Kinematic;
    }

    private void OnWaterDrained()
    {
        _drainTween = null;
        StopSound(ref _drainSound);
        DisableWaterInteraction();
        SetSuctionEffectActive(false);
        FinishDrainDoorAsync().Forget();
    }

    private void DisableWaterInteraction()
    {
        if (water == null)
            return;

        Collider2D[] colliders = water.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Effector2D[] effectors = water.GetComponentsInChildren<Effector2D>(true);
        for (int i = 0; i < effectors.Length; i++)
            effectors[i].enabled = false;

        actor?.ExitWater(null);
    }

    private async UniTask FinishDrainDoorAsync()
    {
        try
        {
            _doorSpine?.PlayAnimation(DoorCloseAnimName, false, false);
            await WaitForGameplaySecondsAsync(0.25f);

            PlayDoorSound("closeDoor");
            await WaitForGameplaySecondsAsync(0.2f);

            _doorSpine?.PlayAnimation(DoorIdleAnimName, true, false);
        }
        finally
        {
            if (_doorBody != null)
            {
                _doorBody.bodyType = RigidbodyType2D.Dynamic;
                _doorBody.simulated = IsPlayingState();
            }

            _doorClickInProgress = false;
        }
    }

    #endregion

    #region Suction and owned playback

    private void CreateSuctionEffect()
    {
        if (_suctionEffect != null || startDoor == null)
            return;

        SuccessAnimCtrl successDoor = GetComponentInChildren<SuccessAnimCtrl>(true);
        Transform ribbonTemplate = successDoor != null
            ? successDoor.transform.Find("ribbon")
            : null;

        if (ribbonTemplate == null)
        {
            Debug.LogWarning("[Level95Ctrl] The door_0 ribbon template was not found.", this);
            return;
        }

        _suctionEffect = Instantiate(ribbonTemplate.gameObject, startDoor, false);
        _suctionEffect.name = "Level95_SuctionRibbon";
        _suctionEffect.transform.localPosition = Vector3.zero;
        _suctionEffect.transform.localRotation = Quaternion.identity;
        _suctionEffect.transform.localScale = new Vector3(
            mirrorSuctionEffect ? -1f : 1f,
            1f,
            1f);
        _suctionSpine = _suctionEffect.GetComponent<ActorSpineCtrl>();
        _suctionEffect.SetActive(false);
    }

    private void SetSuctionEffectActive(bool active)
    {
        if (_suctionEffect == null)
            CreateSuctionEffect();

        if (_suctionEffect == null)
            return;

        _suctionEffect.SetActive(active);
        if (active)
        {
            _suctionSpine ??= _suctionEffect.GetComponent<ActorSpineCtrl>();
            _suctionSpine?.PlayAnimation(SuctionAnimName, true, false);
            SetSpinePlayback(_suctionSpine, IsPlayingState());
        }
    }

    private void PlayDoorSound(string tag)
    {
        StopSound(ref _doorSound);
        _doorSound = AudioManager.PlaySound(tag);
    }

    private static void SetSpinePlayback(ActorSpineCtrl spine, bool isPlaying)
    {
        if (spine?.AnimationState != null)
            spine.AnimationState.TimeScale = isPlaying ? 1f : 0f;
    }

    private static void SetSoundPlayback(Sound sound, bool isPlaying)
    {
        if (sound == null || !sound.Using)
            return;

        if (isPlaying)
        {
            if (sound.Paused)
                sound.Resume();
        }
        else if (sound.Playing)
        {
            sound.Pause();
        }
    }

    private static void StopSound(ref Sound sound)
    {
        if (sound != null && sound.Using)
            sound.Stop();

        sound = null;
    }

    #endregion
}
