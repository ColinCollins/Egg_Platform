using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draw 双球相遇后的通用胜利演出入口。
/// 首次确认两个指定球体碰撞时立即冻结它们，在接触点上方播放爱心气泡并轻微聚焦本关相机，
/// 演出结束后再通过 DrawLevelCtrl 正式提交成功。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[AddComponentMenu("Game/Draw/Two Ball Reunion Success Handle")]
public sealed class DrawTwoBallReunionSuccessHandle : MonoBehaviour,
    IDrawStrokeCompletedHandler
{
    #region Constants

    private const int HeartTextureSize = 128;
    private const int HeartSupersample = 4;
    private const float MinimumDuration = 0.01f;
    private const float MinimumHeartSizeMultiplier = 0.01f;
    private const string HeartTextureResourcePath = "Sp_hx_ax_01";

    #endregion

    #region Inspector References

    /// <summary>
    /// 当前 DLevel 的状态控制器。
    /// 调用边界：必须与两个球属于同一个 DLevel；为空时仅在当前父层级解析。
    /// </summary>
    [Header("关卡引用")]
    [Tooltip("当前 DLevel 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 与本组件所在球配对的另一个球刚体。
    /// 注意事项：双球以上场景必须显式绑定；为空时只会从同关的 AlienClipBallRig 中选取首个其它球。
    /// </summary>
    [Tooltip("与当前球配对的另一个 yellow_ball Rigidbody2D。")]
    [SerializeField] private Rigidbody2D otherBall;

    /// <summary>
    /// 本关用于世界画面的相机。
    /// 调用边界：优先显式绑定关卡内 GameCamera；为空时先查当前 DLevel，再回退 Camera.main。
    /// </summary>
    [Tooltip("当前 DLevel 内的正交 GameCamera。")]
    [SerializeField] private Camera presentationCamera;

    /// <summary>
    /// 爱心上冒动画使用的粒子模板。
    /// 运行时只克隆模板参数，并用共享 Resources 爱心贴图替换渲染纹理，不修改源 Prefab。
    /// </summary>
    [Tooltip("爱心上冒所复用的 ParticleSystem 模板；建议绑定 ArtSrc/Particles/Prefabs/Bubble。")]
    [SerializeField] private ParticleSystem heartBubblePrefab;

    #endregion

    #region Presentation Settings

    [Header("相遇演出")]
    [Tooltip("从双球接触到正式提交成功的总时长，单位为秒。")]
    [SerializeField, Min(MinimumDuration)] private float presentationDuration = 1.8f;

    [Tooltip("镜头移动并缩放到双球的时间，单位为秒。")]
    [SerializeField, Min(MinimumDuration)] private float cameraFocusDuration = 0.28f;

    [Tooltip("正式成功前镜头恢复原位与原视野的时间，单位为秒。")]
    [SerializeField, Min(MinimumDuration)] private float cameraRestoreDuration = 0.22f;

    [Tooltip("聚焦后的正交视野比例；小于 1 表示放大。")]
    [SerializeField, Range(0.75f, 1f)] private float cameraZoomRatio = 0.91f;

    [Tooltip("镜头从原位置向双球中点靠近的比例，避免边缘碰撞时移动过猛。")]
    [SerializeField, Range(0f, 1f)] private float cameraPositionBlend = 0.75f;

    [Tooltip("相机聚焦点相对双球中点的世界坐标偏移。")]
    [SerializeField] private Vector2 cameraFocusOffset = new(0f, 0.25f);

    [Header("爱心气泡")]
    [Tooltip("爱心粒子发射点相对首次接触点的世界坐标偏移。")]
    [SerializeField] private Vector2 heartSpawnOffset = new(0f, 0.55f);

    [Tooltip("每秒发射的爱心数量。")]
    [SerializeField, Min(0f)] private float heartEmissionRate = 5f;

    /// <summary>
    /// 爱心粒子相对模板原始起始尺寸的倍率。
    /// 调用边界：只作用于运行时克隆，不修改共享 Bubble Prefab；1 表示保持模板尺寸。
    /// </summary>
    [Tooltip("爱心粒子相对 Bubble 模板原始起始尺寸的倍率；1 表示保持原尺寸。")]
    [SerializeField, Min(MinimumHeartSizeMultiplier)] private float heartSizeMultiplier = 1f;

    [Tooltip("爱心特效所在的 Sorting Order；应高于猫、夹子与球。")]
    [SerializeField] private int heartSortingOrder = 340;

    [Tooltip("爱心随机颜色范围的下限。")]
    [SerializeField] private Color heartColorMin = new(1f, 0.24f, 0.46f, 1f);

    [Tooltip("爱心随机颜色范围的上限。")]
    [SerializeField] private Color heartColorMax = new(1f, 0.68f, 0.78f, 1f);

    #endregion

    #region Runtime State

    private struct BodySnapshot
    {
        public RigidbodyType2D BodyType;
        public RigidbodyConstraints2D Constraints;
        public bool Simulated;
        public bool IsValid;
    }

    private static Texture2D _sharedHeartTexture;
    private static bool _usesAuthoredHeartTexture;

    private Rigidbody2D _selfBall;
    private BodySnapshot _selfInitialState;
    private BodySnapshot _otherInitialState;
    private Sequence _presentationSequence;
    private ParticleSystem _heartEffect;
    private Material _heartMaterial;
    private Vector3 _cameraInitialLocalPosition;
    private float _cameraInitialOrthographicSize;
    private int _presentationVersion;
    private bool _isPresenting;
    private bool _isPresentationPaused;
    private bool _hasCameraSnapshot;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CacheInitialBodyStates();
    }

    private void Update()
    {
        if (!_isPresenting || drawLevelCtrl == null)
            return;

        bool shouldPause = drawLevelCtrl.IsPaused;
        if (shouldPause == _isPresentationPaused)
            return;

        _isPresentationPaused = shouldPause;
        if (shouldPause)
        {
            _presentationSequence?.Pause();
            _heartEffect?.Pause(true);
            return;
        }

        _presentationSequence?.Play();
        _heartEffect?.Play(true);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        CancelPresentation(true);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Application.isPlaying || _isPresenting || collision == null)
            return;

        ResolveReferences();
        if (_selfBall == null || otherBall == null || drawLevelCtrl == null)
            return;

        Rigidbody2D incomingBody = collision.collider != null
            ? collision.collider.attachedRigidbody
            : collision.rigidbody;
        if (incomingBody != otherBall)
            return;

        if (!drawLevelCtrl.TryBeginSuccessPerformance())
            return;

        _isPresenting = true;
        _isPresentationPaused = false;
        int version = ++_presentationVersion;

        Vector2 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : GetBallMidpoint();

        FreezeBall(_selfBall);
        FreezeBall(otherBall);
        PlayHeartEffect(contactPoint + heartSpawnOffset);
        PlayPresentation(version, GetBallMidpoint() + cameraFocusOffset);
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        // 本组件只消费碰撞；实现接口是为了接入 DrawLevelCtrl 的局部 Reset 生命周期。
    }

    /// <inheritdoc />
    public void OnDrawAttemptReset()
    {
        CancelPresentation(true);
    }

    #endregion

    #region Presentation

    private void PlayPresentation(int version, Vector2 focusPoint)
    {
        float totalDuration = Mathf.Max(MinimumDuration, presentationDuration);
        float focusDuration = Mathf.Min(
            Mathf.Max(MinimumDuration, cameraFocusDuration),
            totalDuration);
        float restoreDuration = Mathf.Min(
            Mathf.Max(MinimumDuration, cameraRestoreDuration),
            totalDuration);

        Sequence sequence = DOTween.Sequence();
        _presentationSequence = sequence;
        sequence.AppendInterval(totalDuration);

        if (presentationCamera != null)
        {
            Transform cameraTransform = presentationCamera.transform;
            _cameraInitialLocalPosition = cameraTransform.localPosition;
            _cameraInitialOrthographicSize = presentationCamera.orthographicSize;
            _hasCameraSnapshot = true;

            Vector3 targetWorldPosition = new(
                focusPoint.x,
                focusPoint.y,
                cameraTransform.position.z);
            Vector3 targetLocalPosition = cameraTransform.parent != null
                ? cameraTransform.parent.InverseTransformPoint(targetWorldPosition)
                : targetWorldPosition;
            targetLocalPosition.z = _cameraInitialLocalPosition.z;
            targetLocalPosition = Vector3.Lerp(
                _cameraInitialLocalPosition,
                targetLocalPosition,
                Mathf.Clamp01(cameraPositionBlend));

            sequence.Insert(
                0f,
                cameraTransform
                    .DOLocalMove(targetLocalPosition, focusDuration)
                    .SetEase(Ease.OutCubic));

            if (presentationCamera.orthographic)
            {
                sequence.Insert(
                    0f,
                    presentationCamera
                        .DOOrthoSize(
                            _cameraInitialOrthographicSize * Mathf.Clamp(cameraZoomRatio, 0.75f, 1f),
                            focusDuration)
                        .SetEase(Ease.OutCubic));
            }

            float restoreStart = Mathf.Max(focusDuration, totalDuration - restoreDuration);
            sequence.Insert(
                restoreStart,
                cameraTransform
                    .DOLocalMove(_cameraInitialLocalPosition, restoreDuration)
                    .SetEase(Ease.InOutSine));

            if (presentationCamera.orthographic)
            {
                sequence.Insert(
                    restoreStart,
                    presentationCamera
                        .DOOrthoSize(_cameraInitialOrthographicSize, restoreDuration)
                        .SetEase(Ease.InOutSine));
            }
        }

        sequence
            .SetLink(gameObject)
            .OnComplete(() => CompletePresentation(version));

        if (drawLevelCtrl != null && drawLevelCtrl.IsPaused)
        {
            _isPresentationPaused = true;
            sequence.Pause();
            _heartEffect?.Pause(true);
        }
    }

    private void CompletePresentation(int version)
    {
        if (!_isPresenting || version != _presentationVersion)
            return;

        _isPresenting = false;
        _isPresentationPaused = false;
        _presentationSequence = null;
        StopHeartEffect();
        RestoreCameraImmediate();
        drawLevelCtrl?.CompleteSuccessPerformance(nameof(DrawTwoBallReunionSuccessHandle));
    }

    private void PlayHeartEffect(Vector2 worldPosition)
    {
        StopHeartEffect();
        if (heartBubblePrefab == null)
            return;

        Transform effectParent = drawLevelCtrl != null ? drawLevelCtrl.transform : null;
        _heartEffect = Instantiate(
            heartBubblePrefab,
            worldPosition,
            heartBubblePrefab.transform.rotation,
            effectParent);
        _heartEffect.name = "DrawGame_HeartBubble";

        Texture2D heartTexture = GetOrCreateHeartTexture();
        ParticleSystem.MainModule main = _heartEffect.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = _usesAuthoredHeartTexture
            ? Color.white
            : new ParticleSystem.MinMaxGradient(heartColorMin, heartColorMax);
        main.startSize = ScaleParticleCurve(main.startSize, heartSizeMultiplier);

        ParticleSystem.EmissionModule emission = _heartEffect.emission;
        emission.rateOverTime = Mathf.Max(0f, heartEmissionRate);

        ParticleSystemRenderer particleRenderer =
            _heartEffect.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            Material sourceMaterial = particleRenderer.sharedMaterial;
            Shader fallbackShader = Shader.Find("Sprites/Default");
            if (sourceMaterial != null || fallbackShader != null)
            {
                _heartMaterial = sourceMaterial != null
                    ? new Material(sourceMaterial)
                    : new Material(fallbackShader);
                _heartMaterial.name = "DrawGame_HeartBubble_Runtime";
                _heartMaterial.mainTexture = heartTexture;
                particleRenderer.sharedMaterial = _heartMaterial;
            }

            ApplyHeartSorting(particleRenderer);
        }

        SortingGroup sortingGroup = _heartEffect.GetComponent<SortingGroup>();
        if (sortingGroup != null)
        {
            sortingGroup.sortingLayerID = ResolveBallSortingLayerId();
            sortingGroup.sortingOrder = heartSortingOrder;
        }

        _heartEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _heartEffect.Play(true);
        _heartEffect.Emit(1);
    }

    private static ParticleSystem.MinMaxCurve ScaleParticleCurve(
        ParticleSystem.MinMaxCurve source,
        float multiplier)
    {
        float scale = multiplier > 0f
            ? Mathf.Max(MinimumHeartSizeMultiplier, multiplier)
            : 1f;
        switch (source.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return new ParticleSystem.MinMaxCurve(source.constant * scale);
            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(
                    source.constantMin * scale,
                    source.constantMax * scale);
            case ParticleSystemCurveMode.Curve:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * scale,
                    source.curve);
            case ParticleSystemCurveMode.TwoCurves:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * scale,
                    source.curveMin,
                    source.curveMax);
            default:
                return source;
        }
    }

    private void ApplyHeartSorting(ParticleSystemRenderer particleRenderer)
    {
        particleRenderer.sortingLayerID = ResolveBallSortingLayerId();
        particleRenderer.sortingOrder = heartSortingOrder;
    }

    private int ResolveBallSortingLayerId()
    {
        SortingGroup sortingGroup = _selfBall != null
            ? _selfBall.GetComponent<SortingGroup>()
            : null;
        return sortingGroup != null
            ? sortingGroup.sortingLayerID
            : SortingLayer.NameToID("Game");
    }

    #endregion

    #region Physics And Reset

    private static void FreezeBall(Rigidbody2D ball)
    {
        if (ball == null)
            return;

        ball.linearVelocity = Vector2.zero;
        ball.angularVelocity = 0f;
        ball.bodyType = RigidbodyType2D.Kinematic;
        ball.constraints = RigidbodyConstraints2D.FreezeAll;
        ball.Sleep();
    }

    private void CacheInitialBodyStates()
    {
        if (_selfBall != null && !_selfInitialState.IsValid)
            _selfInitialState = CaptureBody(_selfBall);

        if (otherBall != null && !_otherInitialState.IsValid)
            _otherInitialState = CaptureBody(otherBall);
    }

    private static BodySnapshot CaptureBody(Rigidbody2D ball)
    {
        return new BodySnapshot
        {
            BodyType = ball.bodyType,
            Constraints = ball.constraints,
            Simulated = ball.simulated,
            IsValid = true,
        };
    }

    private static void RestoreBody(Rigidbody2D ball, BodySnapshot snapshot)
    {
        if (ball == null || !snapshot.IsValid)
            return;

        ball.linearVelocity = Vector2.zero;
        ball.angularVelocity = 0f;
        ball.constraints = snapshot.Constraints;
        ball.bodyType = snapshot.BodyType;
        ball.simulated = snapshot.Simulated;
        ball.Sleep();
    }

    private void CancelPresentation(bool restoreBodies)
    {
        bool wasPresenting = _isPresenting;
        _presentationVersion++;
        _isPresenting = false;
        _isPresentationPaused = false;

        _presentationSequence?.Kill();
        _presentationSequence = null;
        StopHeartEffect();
        RestoreCameraImmediate();

        if (wasPresenting)
            drawLevelCtrl?.CancelSuccessPerformance();

        if (!restoreBodies)
            return;

        RestoreBody(_selfBall, _selfInitialState);
        RestoreBody(otherBall, _otherInitialState);
    }

    private void StopHeartEffect()
    {
        if (_heartEffect != null)
        {
            _heartEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(_heartEffect.gameObject);
            _heartEffect = null;
        }

        if (_heartMaterial != null)
        {
            Destroy(_heartMaterial);
            _heartMaterial = null;
        }
    }

    private void RestoreCameraImmediate()
    {
        if (!_hasCameraSnapshot || presentationCamera == null)
        {
            _hasCameraSnapshot = false;
            return;
        }

        presentationCamera.transform.localPosition = _cameraInitialLocalPosition;
        if (presentationCamera.orthographic)
            presentationCamera.orthographicSize = _cameraInitialOrthographicSize;

        _hasCameraSnapshot = false;
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        if (_selfBall == null)
            _selfBall = GetComponent<Rigidbody2D>();

        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);

        if (presentationCamera == null && drawLevelCtrl != null)
            presentationCamera = drawLevelCtrl.GetComponentInChildren<Camera>(true);

        if (presentationCamera == null)
            presentationCamera = Camera.main;

        if (otherBall == null && drawLevelCtrl != null)
        {
            DrawAlienClipBallRigView[] rigViews =
                drawLevelCtrl.GetComponentsInChildren<DrawAlienClipBallRigView>(true);
            for (int i = 0; i < rigViews.Length; i++)
            {
                DrawAlienClipBallRigView rigView = rigViews[i];
                rigView.ResolveReferences();
                Rigidbody2D candidate = rigView.YellowBallRigidbody;
                if (candidate == null || candidate == _selfBall)
                    continue;

                otherBall = candidate;
                break;
            }
        }

        CacheInitialBodyStates();
    }

    private Vector2 GetBallMidpoint()
    {
        if (_selfBall == null || otherBall == null)
            return transform.position;

        return (_selfBall.worldCenterOfMass + otherBall.worldCenterOfMass) * 0.5f;
    }

    #endregion

    #region Runtime Heart Texture

    private static Texture2D GetOrCreateHeartTexture()
    {
        if (_sharedHeartTexture != null)
            return _sharedHeartTexture;

        Texture2D authoredTexture = Resources.Load<Texture2D>(HeartTextureResourcePath);
        if (authoredTexture != null)
        {
            _sharedHeartTexture = authoredTexture;
            _usesAuthoredHeartTexture = true;
            return _sharedHeartTexture;
        }

        Texture2D texture = new(
            HeartTextureSize,
            HeartTextureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "DrawGame_Heart_Runtime",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        Color32[] pixels = new Color32[HeartTextureSize * HeartTextureSize];
        int sampleCount = HeartSupersample * HeartSupersample;
        for (int y = 0; y < HeartTextureSize; y++)
        {
            for (int x = 0; x < HeartTextureSize; x++)
            {
                int insideCount = 0;
                for (int sampleY = 0; sampleY < HeartSupersample; sampleY++)
                {
                    for (int sampleX = 0; sampleX < HeartSupersample; sampleX++)
                    {
                        float normalizedX =
                            (x + (sampleX + 0.5f) / HeartSupersample) / HeartTextureSize;
                        float normalizedY =
                            (y + (sampleY + 0.5f) / HeartSupersample) / HeartTextureSize;
                        float heartX = Mathf.Lerp(-1.35f, 1.35f, normalizedX);
                        float heartY = Mathf.Lerp(-1.25f, 1.25f, normalizedY);
                        float squareX = heartX * heartX;
                        float squareY = heartY * heartY;
                        float baseValue = squareX + squareY - 1f;
                        float implicitValue =
                            baseValue * baseValue * baseValue -
                            squareX * heartY * squareY;
                        if (implicitValue <= 0f)
                            insideCount++;
                    }
                }

                byte alpha = (byte)Mathf.RoundToInt(255f * insideCount / sampleCount);
                pixels[y * HeartTextureSize + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        _sharedHeartTexture = texture;
        return _sharedHeartTexture;
    }

    #endregion
}
