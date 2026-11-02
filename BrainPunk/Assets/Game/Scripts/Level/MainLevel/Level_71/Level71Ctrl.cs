using System.Collections;
using Bear.Logger;
using Bear.UI;
using DG.Tweening;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// 71 关局部流程：控制长椅坐下/睡眠、昼夜循环，以及免费商品领取后的实体钥匙投放。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level71Ctrl : MonoBehaviour, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 用途：执行 71 关坐下与睡眠动画的角色控制器。
    /// 调用边界：必须绑定当前 Level_071 内的 Level71ActorCtrl，不得引用其它关卡角色。
    /// </summary>
    [Header("角色与长椅")]
    [SerializeField] private Level71ActorCtrl actorCtrl;

    /// <summary>
    /// 用途：作为角色坐下时的父节点和定位基准。
    /// 调用边界：仅绑定当前关卡的公园长椅 Sp_prop_1_15。
    /// </summary>
    [SerializeField] private Transform bench;

    /// <summary>
    /// 用途：同步 bench 入座和离座时的局部机关表现。
    /// 调用边界：仅绑定当前关卡 bench 上的 SitableIStuffCtrl，由坐下/起身流程调用。
    /// </summary>
    [SerializeField] private SitableIStuffCtrl benchSitable;

    /// <summary>
    /// 用途：微调角色相对长椅的坐姿位置。
    /// 注意事项：沿用 45 关已验收坐姿偏移，后续仅按 71 关美术对位微调。
    /// </summary>
    [SerializeField] private Vector3 sitOffset = new(0.3f, -0.15f, 0f);

    /// <summary>
    /// 用途：本关初始静止、达到首屏指定横向视口位置后才启用的主相机。
    /// 调用边界：仅绑定当前 Level_071 的 GameCamera；未绑定时会从本关层级或 MainCamera 兜底查找。
    /// </summary>
    [Header("镜头跟随")]
    [SerializeField] private Camera mainCamera;

    /// <summary>
    /// 用途：角色在屏幕横向视口达到该比例后锁存镜头跟随。
    /// 注意事项：使用视口比例而非世界坐标，场景物体移动或横屏宽高比变化不会改变触发语义。
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float cameraFollowStartViewportX = 0.6666667f;

    /// <summary>
    /// 用途：镜头横向跟随的平滑时间；设为 0 时立即跟随。
    /// </summary>
    [SerializeField, Min(0f)] private float cameraFollowSmoothTime = 0.2f;

    /// <summary>
    /// 用途：定义截图黄色区域的世界坐标中心，限制跟随镜头继续看向该区域右侧。
    /// 调用边界：只影响本关镜头跟随的横向终点，不改变开局相机位置和 2/3 启动条件。
    /// </summary>
    [SerializeField] private Vector2 cameraFollowVisibleAreaCenter = new(5.6f, -0.25f);

    /// <summary>
    /// 用途：定义截图黄色区域的世界尺寸；运行时会按当前正交相机宽度计算允许的最大相机 X。
    /// 注意事项：Y 仅用于 Scene 视图标记，本关镜头 Y/Z 始终保持开局值。
    /// </summary>
    [SerializeField] private Vector2 cameraFollowVisibleAreaSize = new(24f, 14f);

    /// <summary>
    /// 用途：作为太阳和月亮共同绕行的轨迹圆心，昼夜循环根据该节点计算两个天体的位置。
    /// 调用边界：必须绑定当前关卡的 center，Sun 和 Moon 应保持为其子节点。
    /// </summary>
    [Header("昼夜表现")]
    [SerializeField] private Transform orbitCenter;

    /// <summary>
    /// 用途：代表完整 Sun 层级，并确保昼夜循环期间太阳始终处于激活状态。
    /// 调用边界：必须绑定当前关卡的 Sun 根节点，不得绑定内部单张图片。
    /// </summary>
    [SerializeField] private Transform sunRoot;

    /// <summary>
    /// 用途：代表完整 Moon 层级，并确保昼夜循环期间月亮始终处于激活状态。
    /// 调用边界：必须绑定当前关卡的 Moon 根节点，不得绑定内部单张图片。
    /// </summary>
    [SerializeField] private Transform moonRoot;

    /// <summary>
    /// 用途：承载昼夜循环的背景颜色渐变。
    /// 调用边界：必须绑定当前关卡 CommonBg 下的 Bg SpriteRenderer。
    /// </summary>
    [SerializeField] private SpriteRenderer backgroundRenderer;

    /// <summary>
    /// 用途：让 Inspector 人工配置的一组场景 SpriteRenderer 同步参与昼夜 RGB 颜色渐变。
    /// 调用边界：组成员由 SpriteRendererGroupColor 自身配置，不应包含 Sun、Moon 或 CommonBg/Bg。
    /// </summary>
    [SerializeField] private SpriteRendererGroupColor environmentColorGroup;

    /// <summary>
    /// 用途：定义昼夜循环处于夜晚相位时 CommonBg/Bg 的颜色。
    /// 注意事项：该颜色会直接写入绑定的背景渲染器，可由关卡美术在 Inspector 调整。
    /// </summary>
    [Header("昼夜颜色")]
    [SerializeField] private Color nightBackgroundColor =
        new(0.22745098f, 0.22745098f, 0.30588236f, 1f);

    /// <summary>
    /// 用途：定义昼夜循环处于白天相位时 CommonBg/Bg 的颜色。
    /// 注意事项：默认使用白色，可由关卡美术在 Inspector 调整。
    /// </summary>
    [SerializeField] private Color dayBackgroundColor = Color.white;

    /// <summary>
    /// 用途：单独偏移背景与场景颜色组的昼夜渐变进度。
    /// 调用边界：按完整昼夜周期归一化，正值提前、负值延后；只影响颜色，不改变太阳和月亮轨道。
    /// 注意事项：`0.25` 表示偏移四分之一周期，`1` 与 `0` 等价。
    /// </summary>
    [SerializeField, Range(-1f, 1f)] private float colorTransitionOffset;

    /// <summary>
    /// 用途：免费商品领取后从 Gameplay 右上商店按钮飞向画面中部落点的标准实体钥匙。
    /// 调用边界：初始必须保持关闭；每个关卡实例至多激活一次。
    /// </summary>
    [Header("免费钥匙")]
    [SerializeField] private GameObject keyObject;

    /// <summary>
    /// 用途：保留实体钥匙原有 DirectMove 引用，供专用轨迹无法解析时的软回退使用。
    /// 调用边界：正常路径由 Level71Ctrl 的定点抛物线驱动，不修改共享 DirectMove 实现或 Prefab 参数。
    /// </summary>
    [SerializeField] private DirectMove keyDirectMove;

    /// <summary>
    /// 用途：钥匙落地后切换为轻微悬浮，避免继续穿过地面。
    /// 调用边界：定点飞行完成或原 DirectMove 触地后启用。
    /// </summary>
    [SerializeField] private MoveFloatHandle keyFloatHandle;

    /// <summary>
    /// 用途：指定钥匙在 Gameplay 商店按钮矩形内的飞出点，左下角为 (0, 0)，右上角为 (1, 1)。
    /// 注意事项：默认值对应需求截图中购物篮左侧偏下的入口，X/Y 均会在运行时钳制到 0~1。
    /// </summary>
    [SerializeField] private Vector2 keyShopButtonStartNormalizedPosition = new(0.2f, 0.4f);

    /// <summary>
    /// 用途：Gameplay 商店按钮位置无法取得时，指定钥匙飞行起点在当前场景相机视口中的回退位置。
    /// 注意事项：默认值对应需求截图中购物篮左侧偏下的入口，X/Y 均会在运行时钳制到 0~1。
    /// </summary>
    [SerializeField] private Vector2 keyFallbackStartViewportPosition = new(0.915f, 0.875f);

    /// <summary>
    /// 用途：定义实体钥匙在生成当帧的场景相机视口内最终落定的位置。
    /// 注意事项：使用屏幕相对位置避免依赖绝对场景坐标；默认值按本关截图落在中部商店前。
    /// </summary>
    [SerializeField] private Vector2 keyLandingViewportPosition = new(0.59f, 0.35f);

    #endregion

    #region Configuration

    /// <summary>
    /// 用途：角色持续坐下多久后进入睡眠并启动昼夜循环。
    /// </summary>
    [Header("时序参数")]
    [SerializeField, Min(0f)] private float sleepDelay = 3f;

    /// <summary>
    /// 用途：昼夜循环开始时每秒推进的循环数。
    /// </summary>
    [SerializeField, Min(0.001f)] private float initialCycleSpeed = 0.04f;

    /// <summary>
    /// 用途：限制逐渐加速后的最大昼夜循环速度。
    /// </summary>
    [SerializeField, Min(0.001f)] private float maxCycleSpeed = 0.3f;

    /// <summary>
    /// 用途：定义昼夜循环速度每秒增加的循环数。
    /// </summary>
    [SerializeField, Min(0f)] private float cycleAcceleration = 0.012f;

    /// <summary>
    /// 用途：定义 ShopPanel 完全关闭后到实体钥匙出现前的额外延迟。
    /// 注意事项：使用未缩放时间，默认 1 秒，可由关卡策划在 Inspector 微调。
    /// </summary>
    [SerializeField, Min(0f)] private float keySpawnDelay = 1f;

    /// <summary>
    /// 用途：定义钥匙从右上商店按钮飞到场景落点所需时间。
    /// 注意事项：设为 0 时直接落到目标位置并进入悬浮状态。
    /// </summary>
    [SerializeField, Min(0f)] private float keyFlightDuration = 1.1f;

    /// <summary>
    /// 用途：定义钥匙抛物线控制点高于起点的世界高度。
    /// 注意事项：只影响飞行弧度，不改变起点和最终落点。
    /// </summary>
    [SerializeField, Min(0f)] private float keyFlightArcHeight = 1f;

    /// <summary>
    /// 用途：控制钥匙沿抛物线前进的速度变化。
    /// 注意事项：默认 OutQuad，使钥匙起步利落并在落点自然收速，不改变轨迹端点。
    /// </summary>
    [SerializeField] private Ease keyFlightEase = Ease.OutQuad;

    /// <summary>
    /// 用途：定义实体钥匙激活后从零缩放恢复到一倍缩放的动画时长。
    /// 调用边界：每个关卡实例只在免费钥匙首次出现时读取；设为 0 时立即恢复一倍缩放。
    /// 注意事项：只影响钥匙根节点缩放；生成位置由本关钥匙定位流程单独处理。
    /// </summary>
    [SerializeField, Min(0f)] private float keyScaleDuration = 0.3f;

    #endregion

    #region Runtime State

    private GamePanel_UseBtnCommonFeature _commonFeature;
    private bool _isSitting;
    private bool _isSleeping;
    private bool _keySpawned;
    private Coroutine _keySpawnRoutine;
    private Tween _keyScaleTween;
    private Tween _keyFlightTween;
    private Collider2D _keyCollider;
    private float _sitTimer;
    private float _cyclePhase;
    private float _cycleSpeed;
    private Vector3 _sunInitialLocalPosition;
    private Vector3 _moonInitialLocalPosition;
    private Quaternion _sunInitialLocalRotation;
    private Quaternion _moonInitialLocalRotation;
    private Vector3 _cameraInitialPosition;
    private float _cameraFollowVelocityX;
    private float _cameraFollowAnchorOffsetX;
    private bool _cameraFollowReady;
    private bool _hasStartedCameraFollow;

    #endregion

    #region Unity Lifecycle

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ApplyColorState(0f);
    }
#endif

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        if (sunRoot != null)
        {
            _sunInitialLocalPosition = sunRoot.localPosition;
            _sunInitialLocalRotation = sunRoot.localRotation;
            sunRoot.gameObject.SetActive(true);
        }
        if (moonRoot != null)
        {
            _moonInitialLocalPosition = moonRoot.localPosition;
            _moonInitialLocalRotation = moonRoot.localRotation;
            moonRoot.gameObject.SetActive(true);
        }

        _cycleSpeed = initialCycleSpeed;
        ApplyDayNightState();
        InitializeCameraFollow();

        if (keyObject != null)
        {
            _keyCollider = keyObject.GetComponent<Collider2D>();
            keyObject.transform.localScale = Vector3.zero;
            if (keyDirectMove != null)
                keyDirectMove.enabled = false;
            if (keyFloatHandle != null)
                keyFloatHandle.enabled = false;
            keyObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (!_cameraFollowReady)
            InitializeCameraFollow();

        BindHud();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        UpdateSleepAndDayNight();
        TrySpawnClaimedKey();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        UpdateCameraFollow();
    }

    private void OnDestroy()
    {
        if (_keySpawnRoutine != null)
        {
            StopCoroutine(_keySpawnRoutine);
            _keySpawnRoutine = null;
        }

        _keyScaleTween?.Kill();
        _keyScaleTween = null;
        _keyFlightTween?.Kill();
        _keyFlightTween = null;

        if (_commonFeature?.Use_btn != null)
            _commonFeature.Use_btn.OnClick -= ToggleSitting;
    }

    #endregion

    #region Camera Follow

    private void InitializeCameraFollow()
    {
        if (_cameraFollowReady)
            return;

        if (mainCamera == null)
            mainCamera = GetComponentInChildren<Camera>(true);

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null || actorCtrl == null)
        {
            this.LogWarning("[Level71Ctrl] 角色或主相机引用不完整，镜头跟随未启用。");
            return;
        }

        _cameraInitialPosition = mainCamera.transform.position;
        _cameraFollowVelocityX = 0f;
        _cameraFollowReady = true;
    }

    private void UpdateCameraFollow()
    {
        if (!_cameraFollowReady ||
            mainCamera == null ||
            actorCtrl == null ||
            actorCtrl.IsDied)
        {
            return;
        }

        if (!_hasStartedCameraFollow)
        {
            Vector3 actorViewportPosition =
                mainCamera.WorldToViewportPoint(actorCtrl.transform.position);
            if (actorViewportPosition.z <= 0f ||
                actorViewportPosition.x < Mathf.Clamp01(cameraFollowStartViewportX))
            {
                return;
            }

            _hasStartedCameraFollow = true;
            _cameraFollowAnchorOffsetX =
                actorCtrl.transform.position.x - mainCamera.transform.position.x;
            _cameraFollowVelocityX = 0f;
        }

        Vector3 cameraPosition = mainCamera.transform.position;
        float maxCameraX = GetCameraFollowMaxX();
        float targetX = Mathf.Clamp(
            actorCtrl.transform.position.x - _cameraFollowAnchorOffsetX,
            _cameraInitialPosition.x,
            maxCameraX);
        cameraPosition.x = cameraFollowSmoothTime > 0f
            ? Mathf.SmoothDamp(
                cameraPosition.x,
                targetX,
                ref _cameraFollowVelocityX,
                cameraFollowSmoothTime)
            : targetX;
        cameraPosition.x = Mathf.Clamp(
            cameraPosition.x,
            _cameraInitialPosition.x,
            maxCameraX);
        cameraPosition.y = _cameraInitialPosition.y;
        cameraPosition.z = _cameraInitialPosition.z;
        mainCamera.transform.position = cameraPosition;
    }

    private float GetCameraFollowMaxX()
    {
        float areaRight = cameraFollowVisibleAreaCenter.x
            + Mathf.Abs(cameraFollowVisibleAreaSize.x) * 0.5f;
        float cameraHalfWidth = mainCamera != null && mainCamera.orthographic
            ? mainCamera.orthographicSize * mainCamera.aspect
            : 0f;
        return Mathf.Max(_cameraInitialPosition.x, areaRight - cameraHalfWidth);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(
                cameraFollowVisibleAreaCenter.x,
                cameraFollowVisibleAreaCenter.y,
                0f),
            new Vector3(
                Mathf.Abs(cameraFollowVisibleAreaSize.x),
                Mathf.Abs(cameraFollowVisibleAreaSize.y),
                0.1f));
    }

    #endregion

    #region Sitting And Sleeping

    private void BindHud()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        if (playCtrl?.CurrentGamePlayPanel == null)
        {
            this.LogError("[Level71Ctrl] 当前 GamePlayPanel 不存在，无法绑定坐下按钮。");
            return;
        }

        _commonFeature = playCtrl.CurrentGamePlayPanel.GetComponent<GamePanel_UseBtnCommonFeature>();
        if (_commonFeature?.Use_btn == null)
        {
            this.LogError("[Level71Ctrl] GamePlayPanel_018_45 缺少可用的 Use_btn。");
            return;
        }

        _commonFeature.Use_btn.OnClick -= ToggleSitting;
        _commonFeature.Use_btn.OnClick += ToggleSitting;
    }

    private void ToggleSitting(CustomButton _)
    {
        if (actorCtrl == null || bench == null || actorCtrl.IsDied)
            return;

        if (_isSitting)
        {
            StandUp();
            return;
        }

        _isSitting = true;
        _isSleeping = false;
        _sitTimer = 0f;
        actorCtrl.PlaySit(bench, sitOffset);
        benchSitable?.SitIn();
    }

    private void StandUp()
    {
        _isSitting = false;
        _isSleeping = false;
        _sitTimer = 0f;
        actorCtrl.StopSit();
        benchSitable?.SitOut();
    }

    private void UpdateSleepAndDayNight()
    {
        if (!_isSitting || actorCtrl == null || !actorCtrl.IsSit || actorCtrl.IsDied)
            return;

        if (!_isSleeping)
        {
            if (DOTween.IsTweening(actorCtrl.transform))
            {
                _sitTimer = 0f;
                return;
            }

            _sitTimer += Time.deltaTime;
            if (_sitTimer < sleepDelay)
                return;

            _isSleeping = actorCtrl.PlaySleepLoop();
            if (!_isSleeping)
                return;
        }

        _cycleSpeed = Mathf.MoveTowards(
            _cycleSpeed,
            Mathf.Max(initialCycleSpeed, maxCycleSpeed),
            cycleAcceleration * Time.deltaTime);
        _cyclePhase = Mathf.Repeat(_cyclePhase + _cycleSpeed * Time.deltaTime, 1f);

        ApplyDayNightState();
    }

    #endregion

    #region Day And Night Visuals

    private void ApplyDayNightState()
    {
        Quaternion orbitRotation = Quaternion.Euler(0f, 0f, _cyclePhase * 360f);
        if (orbitCenter != null && sunRoot != null && sunRoot.parent == orbitCenter)
        {
            sunRoot.localPosition = orbitRotation * _sunInitialLocalPosition;
            sunRoot.localRotation = _sunInitialLocalRotation;
        }

        if (orbitCenter != null && moonRoot != null && moonRoot.parent == orbitCenter)
        {
            moonRoot.localPosition = orbitRotation * _moonInitialLocalPosition;
            moonRoot.localRotation = _moonInitialLocalRotation;
        }

        ApplyColorState(_cyclePhase);
    }

    /// <summary>
    /// 用途：按指定昼夜相位刷新背景与场景颜色组，供运行时和 Inspector 编辑器预览共同复用。
    /// 调用边界：编辑器预览固定传入初始相位 0；运行时传入当前循环相位。
    /// 注意事项：只读取本地序列化字段并修改已绑定 Renderer，不访问运行时单例、配置或存档。
    /// </summary>
    /// <param name="cyclePhase">归一化昼夜相位。</param>
    private void ApplyColorState(float cyclePhase)
    {
        float colorPhase = Mathf.Repeat(cyclePhase + colorTransitionOffset, 1f);
        float dayBlend = 0.5f - 0.5f * Mathf.Cos(colorPhase * Mathf.PI * 2f);
        Color currentColor = Color.Lerp(
            nightBackgroundColor,
            dayBackgroundColor,
            dayBlend);

        if (backgroundRenderer != null)
            backgroundRenderer.color = currentColor;

        environmentColorGroup?.SetColor(currentColor);
    }

    #endregion

    #region Key Flow

    private void TrySpawnClaimedKey()
    {
        if (_keySpawned || _keySpawnRoutine != null || !Level71ShopBtnCtrl.HasClick)
            return;

        _keySpawnRoutine = StartCoroutine(SpawnClaimedKeyAfterShopClosed());
    }

    private IEnumerator SpawnClaimedKeyAfterShopClosed()
    {
        while (UIManager.Instance?.GetUI<ShopPanel>() != null)
            yield return null;

        if (keySpawnDelay > 0f)
            yield return new WaitForSecondsRealtime(keySpawnDelay);

        _keySpawnRoutine = null;
        SpawnClaimedKey();
    }

    private void SpawnClaimedKey()
    {
        if (_keySpawned)
            return;

        if (keyObject == null)
        {
            this.LogError("[Level71Ctrl] 免费钥匙未绑定，无法生成实体钥匙。");
            _keySpawned = true;
            return;
        }

        if (keyFloatHandle != null)
            keyFloatHandle.enabled = false;
        if (keyDirectMove != null)
            keyDirectMove.enabled = false;
        if (_keyCollider != null)
            _keyCollider.enabled = false;

        Transform keyTransform = keyObject.transform;
        bool hasFlightPositions = TryResolveKeyFlightPositions(
            keyTransform,
            out Camera flightCamera,
            out float flightCameraDepth,
            out float keyWorldZ,
            out Vector2 flightStartViewportPosition,
            out Vector2 landingViewportPosition);
        _keyScaleTween?.Kill();
        keyTransform.localScale = Vector3.zero;
        if (hasFlightPositions)
        {
            keyTransform.position = GetKeyWorldPosition(
                flightCamera,
                flightCameraDepth,
                keyWorldZ,
                flightStartViewportPosition);
        }
        keyObject.SetActive(true);

        if (keyScaleDuration <= 0f)
        {
            keyTransform.localScale = Vector3.one;
        }
        else
        {
            _keyScaleTween = keyTransform
                .DOScale(Vector3.one, keyScaleDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => _keyScaleTween = null);
        }

        if (hasFlightPositions)
        {
            PlayKeyFlight(
                keyTransform,
                flightCamera,
                flightCameraDepth,
                keyWorldZ,
                flightStartViewportPosition,
                landingViewportPosition);
        }
        else
        {
            if (_keyCollider != null)
                _keyCollider.enabled = true;
            if (keyDirectMove != null)
                keyDirectMove.enabled = true;
        }

        _keySpawned = true;
    }

    private bool TryResolveKeyFlightPositions(
        Transform keyTransform,
        out Camera flightCamera,
        out float cameraDepth,
        out float keyWorldZ,
        out Vector2 startViewportPosition,
        out Vector2 landingViewportPosition)
    {
        flightCamera = null;
        cameraDepth = 0f;
        keyWorldZ = keyTransform != null ? keyTransform.position.z : 0f;
        startViewportPosition = default;
        landingViewportPosition = default;
        if (keyTransform == null)
            return false;

        flightCamera = mainCamera;
        if (flightCamera == null)
            flightCamera = GetComponentInChildren<Camera>(true);
        if (flightCamera == null)
            flightCamera = Camera.main;

        if (flightCamera == null)
        {
            this.LogWarning("[Level71Ctrl] 未找到主相机，实体钥匙回退到原 DirectMove 轨迹。");
            return false;
        }

        Vector3 originalWorldPosition = keyTransform.position;
        Vector3 viewportPosition = flightCamera.WorldToViewportPoint(originalWorldPosition);
        if (viewportPosition.z <= 0f)
        {
            this.LogWarning("[Level71Ctrl] 主相机无法换算实体钥匙深度，回退到原 DirectMove 轨迹。");
            return false;
        }

        cameraDepth = viewportPosition.z;
        keyWorldZ = originalWorldPosition.z;
        startViewportPosition = new Vector2(
            Mathf.Clamp01(keyFallbackStartViewportPosition.x),
            Mathf.Clamp01(keyFallbackStartViewportPosition.y));

        RectTransform shopButton = PlayCtrl.Instance?.CurrentGamePlayPanel?.Shop2SkinGuideTarget;
        if (TryGetRectTransformScreenPoint(
                shopButton,
                keyShopButtonStartNormalizedPosition,
                out Vector2 shopButtonScreenPoint))
        {
            Vector3 shopButtonViewportPoint = flightCamera.ScreenToViewportPoint(new Vector3(
                shopButtonScreenPoint.x,
                shopButtonScreenPoint.y,
                0f));
            if (IsFinite(shopButtonViewportPoint))
            {
                startViewportPosition = new Vector2(
                    shopButtonViewportPoint.x,
                    shopButtonViewportPoint.y);
            }
            else
            {
                this.LogWarning("[Level71Ctrl] Gameplay 商店按钮坐标无效，钥匙使用右上视口回退起点。");
            }
        }
        else
        {
            this.LogWarning("[Level71Ctrl] Gameplay 商店按钮位置不可用，钥匙使用右上视口回退起点。");
        }

        landingViewportPosition = new Vector2(
            Mathf.Clamp01(keyLandingViewportPosition.x),
            Mathf.Clamp01(keyLandingViewportPosition.y));
        return true;
    }

    private static bool TryGetRectTransformScreenPoint(
        RectTransform rectTransform,
        Vector2 normalizedPosition,
        out Vector2 screenPoint)
    {
        screenPoint = default;
        if (rectTransform == null)
            return false;

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        if (canvas == null)
            return false;

        canvas = canvas.rootCanvas;
        Camera uiCamera = null;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
            if (uiCamera == null)
                return false;
        }

        Rect rect = rectTransform.rect;
        Vector3 localPoint = new(
            Mathf.Lerp(rect.xMin, rect.xMax, Mathf.Clamp01(normalizedPosition.x)),
            Mathf.Lerp(rect.yMin, rect.yMax, Mathf.Clamp01(normalizedPosition.y)),
            0f);
        Vector3 worldPoint = rectTransform.TransformPoint(localPoint);
        screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPoint);
        return !float.IsNaN(screenPoint.x)
            && !float.IsInfinity(screenPoint.x)
            && !float.IsNaN(screenPoint.y)
            && !float.IsInfinity(screenPoint.y);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.z);
    }

    private static Vector3 GetKeyWorldPosition(
        Camera flightCamera,
        float cameraDepth,
        float keyWorldZ,
        Vector2 viewportPosition)
    {
        Vector3 worldPosition = flightCamera.ViewportToWorldPoint(new Vector3(
            viewportPosition.x,
            viewportPosition.y,
            cameraDepth));
        worldPosition.z = keyWorldZ;
        return worldPosition;
    }

    private void PlayKeyFlight(
        Transform keyTransform,
        Camera flightCamera,
        float cameraDepth,
        float keyWorldZ,
        Vector2 startViewportPosition,
        Vector2 landingViewportPosition)
    {
        _keyFlightTween?.Kill();
        _keyFlightTween = null;

        if (keyFlightDuration <= 0f)
        {
            CompleteKeyFlight(
                keyTransform,
                flightCamera,
                cameraDepth,
                keyWorldZ,
                landingViewportPosition);
            return;
        }

        _keyFlightTween = DOVirtual.Float(0f, 1f, keyFlightDuration, progress =>
            {
                if (keyTransform == null || flightCamera == null)
                    return;

                Vector3 startPosition = GetKeyWorldPosition(
                    flightCamera,
                    cameraDepth,
                    keyWorldZ,
                    startViewportPosition);
                Vector3 landingPosition = GetKeyWorldPosition(
                    flightCamera,
                    cameraDepth,
                    keyWorldZ,
                    landingViewportPosition);
                Vector3 controlPosition = Vector3.Lerp(startPosition, landingPosition, 0.5f);
                controlPosition.y = Mathf.Max(startPosition.y, landingPosition.y)
                    + Mathf.Max(0f, keyFlightArcHeight);
                float inverseProgress = 1f - progress;
                keyTransform.position =
                    inverseProgress * inverseProgress * startPosition
                    + 2f * inverseProgress * progress * controlPosition
                    + progress * progress * landingPosition;
            })
            .SetEase(keyFlightEase)
            .SetLink(keyObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() => CompleteKeyFlight(
                keyTransform,
                flightCamera,
                cameraDepth,
                keyWorldZ,
                landingViewportPosition));
    }

    private void CompleteKeyFlight(
        Transform keyTransform,
        Camera flightCamera,
        float cameraDepth,
        float keyWorldZ,
        Vector2 landingViewportPosition)
    {
        _keyFlightTween = null;
        if (keyTransform != null && flightCamera != null)
        {
            keyTransform.position = GetKeyWorldPosition(
                flightCamera,
                cameraDepth,
                keyWorldZ,
                landingViewportPosition);
        }

        if (keyFloatHandle != null)
            keyFloatHandle.enabled = true;
        if (_keyCollider != null)
            _keyCollider.enabled = true;
    }

    /// <summary>
    /// 用途：实体钥匙接触地面后停止下坠并切换到悬浮待拾取状态。
    /// 调用边界：仅供钥匙上的 LayerTriggerDetector UnityEvent 调用。
    /// 注意事项：不会处理钥匙拾取或出口解锁，二者继续复用标准 Key.prefab 流程。
    /// </summary>
    /// <param name="groundCollider">触发停止的地面碰撞体，仅用于 UnityEvent 参数匹配。</param>
    public void StopKey(Collider2D groundCollider)
    {
        if (keyDirectMove != null)
            keyDirectMove.enabled = false;

        if (keyFloatHandle != null)
            keyFloatHandle.enabled = true;
    }

    #endregion
}
