using System;
using System.Collections;
using Bear.EventSystem;
using Game.Events;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using UnityEngine;

/// <summary>146「点击所有按钮」：灯最多亮两盏，八个指定入口全部点过才解锁。</summary>
public sealed class Level146Ctrl : MonoBehaviour
{
    #region 场景绑定
    /// <summary>本关八个独立点击记录；左右移动键不属于答案，亮灯状态也不代表点击记录。</summary>
    [Flags]
    public enum RequiredButton
    {
        Pause = 1, Reset = 2, Hint = 4, Shop = 8,
        LeftSwitch = 16, MiddleSwitch = 32, RightSwitch = 64, Jump = 128,
        All = 255
    }

    [Serializable]
    private sealed class SwitchBinding
    {
        /// <summary>Inspector 显式绑定底座命中范围、上方开态、下方关态和 05 灯光。</summary>
        public SpriteRenderer hitArea;
        public GameObject onVisual;
        public GameObject offVisual;
        public GameObject lightVisual;
        /// <summary>与 lightVisual 同一对象的 05 渲染器，显式绑定以独立控制灯光透明度。</summary>
        public SpriteRenderer lightRenderer;
    }

    /// <summary>只使用本关已有角色、相机与正常门锁执行链，不接管共享主流程。</summary>
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Camera levelCamera;
    [SerializeField] private EventHandleListener doorUnlockHandle;
    [SerializeField] private Collider2D exitDoorCollider;
    /// <summary>八个入口点齐后，先显示公共上浮提示，再执行原开锁链；使用I2多语言词条。</summary>
    [SerializeField] private string unlockMessageKey = "S_LevelTips_Des_level_146_01";
    /// <summary>从左到右的三个预放按钮；所有引用在 146 Prefab 显式绑定。</summary>
    [SerializeField] private SwitchBinding[] switches;
    /// <summary>每次有效点击电闸播放一次开关声，与140电闸一致；自动熄灯不重复播放。</summary>
    [SerializeField] private string switchSound = "fanSwitch";
    /// <summary>拉闸沿原端点运动并回弹；灯光在接通触点后短促闪烁。</summary>
    [SerializeField, Min(0.01f)] private float handleMoveDuration = 0.22f;
    [SerializeField, Min(0f)] private float lightContactDelay = 0.1f;
    [SerializeField, Min(0.01f)] private float lightFadeInDuration = 0.07f;
    [SerializeField, Min(0.01f)] private float lightFadeOutDuration = 0.08f;
    #endregion

    #region 本次场景与重开交接
    private readonly bool[] switchOn = new bool[3];
    private RequiredButton pressed;
    private bool unlocked;
    private LevelRuntimeState runtimeState;
    private int attemptReviveCount;
    private Level146GamePanelFeature panelFeature;
    private EventSubscriber subscriber;
    private SwitchMotion[] switchMotions;

    // 只交接给本会话紧邻的一次 Reset/失败重建；不写存档，不借用或改写打点字段。
    private static LevelRuntimeState resetRuntimeState;
    private static int expectedReviveCount;
    private static RequiredButton resetPressed;

    private bool CanInteract => PlayCtrl.Instance != null
        && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING)
        && !runtimeState.IsAttemptTransitioning && !actor.IsDied && !levelCtrl.IsActorEnterDoor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearResetTransfer()
    {
        resetRuntimeState = null;
        resetPressed = 0;
        expectedReviveCount = 0;
    }

    private void Awake()
    {
        if (!Application.isPlaying) return;
        runtimeState = PlayCtrl.Instance.Level.CurrentLevelState;
        attemptReviveCount = runtimeState.ResetCount + runtimeState.FailCount;
        if (ReferenceEquals(runtimeState, resetRuntimeState)
            && runtimeState.LevelPath == "Level_146" && attemptReviveCount == expectedReviveCount)
            pressed = resetPressed;
        ClearResetTransfer();
        InitializeSwitchVisuals();
        exitDoorCollider.enabled = false;
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private IEnumerator Start()
    {
        // 关卡先创建、HUD 后替换；只连接重建后的 146 私有面板。
        yield return null;
        while (PlayCtrl.Instance.CurrentGamePlayPanel == null) yield return null;
        panelFeature = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<Level146GamePanelFeature>();
        panelFeature.ButtonPressed += OnHudButton;
    }

    private void Update()
    {
        if (!CanInteract) return;
        UpdateSwitchVisuals(Time.deltaTime);
        HandleWorldInput();
        if (pressed != RequiredButton.All || unlocked) return;
        unlocked = true;
        SystemTips.Show(LocalizationManager.GetTranslation(unlockMessageKey));
        doorUnlockHandle.Execute();
        exitDoorCollider.enabled = true;
    }

    private void OnGameReset(GameResetEvent evt)
    {
        if (PlayCtrl.Instance.LevelCtrl != levelCtrl || levelCtrl.IsActorEnterDoor) return;
        resetRuntimeState = runtimeState;
        expectedReviveCount = attemptReviveCount + 1;
        resetPressed = pressed;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        StopAllCoroutines();
        EventsUtils.ResetEvents(ref subscriber);
        if (panelFeature != null) panelFeature.ButtonPressed -= OnHudButton;
    }
    #endregion

    #region 点击与灯光
    private void OnHudButton(RequiredButton button)
    {
        if (CanInteract) pressed |= button;
    }

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
        for (int i = 0; i < switches.Length; i++)
        {
            Bounds bounds = switches[i].hitArea.bounds;
            point.z = bounds.center.z;
            if (!bounds.Contains(point)) continue;
            pressed |= (RequiredButton)((int)RequiredButton.LeftSwitch << i);
            if (!switchOn[i])
            {
                int first = (i + 1) % 3;
                int second = (i + 2) % 3;
                if (switchOn[first] && switchOn[second])
                    SetSwitch(UnityEngine.Random.value < 0.5f ? first : second, false);
            }
            SetSwitch(i, !switchOn[i]);
            AudioManager.PlaySound(switchSound);
            return;
        }
    }

    private void SetSwitch(int index, bool on)
    {
        switchOn[index] = on;
        SwitchBinding binding = switches[index];
        SwitchMotion motion = switchMotions[index];

        // 连点时继续使用当前正在运动的那张图，从当前姿态反向，不瞬移、不叠两张手柄。
        if (motion.handle == null)
            motion.handle = binding.onVisual.activeSelf ? binding.onVisual.transform : binding.offVisual.transform;
        motion.startPose = new HandlePose(motion.handle);
        motion.handleElapsed = 0f;
        binding.onVisual.SetActive(motion.handle == binding.onVisual.transform);
        binding.offVisual.SetActive(motion.handle == binding.offVisual.transform);

        binding.lightVisual.SetActive(true);
        if (on)
        {
            motion.flickersLeft = UnityEngine.Random.Range(1, 3);
            // 旧灯先退完，再接通新灯；保留快速反向时的当前亮度。
            BeginLightPhase(motion, LightPhase.Contact, motion.lightLevel,
                Mathf.Max(lightContactDelay, lightFadeOutDuration));
        }
        else BeginLightPhase(motion, LightPhase.FadeOut, 0f, lightFadeOutDuration);
    }
    #endregion

    #region 本关拉闸与灯光表现
    private enum LightPhase { Off, Contact, FadeIn, FlickerDim, FlickerBright, Steady, FadeOut }

    private struct HandlePose
    {
        public Vector3 position;
        public Vector3 scale;
        public Quaternion rotation;

        public HandlePose(Transform target)
        {
            position = target.localPosition;
            scale = target.localScale;
            rotation = target.localRotation;
        }

        public void Apply(Transform target)
        {
            target.localPosition = position;
            target.localScale = scale;
            target.localRotation = rotation;
        }
    }

    /// <summary>固定三份运行态；用 Update 推进，暂停/重建门禁和快速反向共用同一状态。</summary>
    private sealed class SwitchMotion
    {
        public HandlePose onPose, offPose, startPose;
        public Transform handle;
        public float handleElapsed;
        public Color lightTint;
        public float lightLevel, lightFrom, lightTarget, lightElapsed, lightDuration;
        public LightPhase lightPhase;
        public int flickersLeft;
    }

    private void InitializeSwitchVisuals()
    {
        switchMotions = new SwitchMotion[switches.Length];
        for (int i = 0; i < switches.Length; i++)
        {
            SwitchBinding binding = switches[i];
            switchMotions[i] = new SwitchMotion
            {
                onPose = new HandlePose(binding.onVisual.transform),
                offPose = new HandlePose(binding.offVisual.transform),
                lightTint = binding.lightRenderer.color
            };
            switchOn[i] = false;
            binding.onVisual.SetActive(false);
            binding.offVisual.SetActive(true);
            ApplyLightLevel(binding, switchMotions[i], 0f);
            binding.lightVisual.SetActive(false);
        }
    }

    private void UpdateSwitchVisuals(float deltaTime)
    {
        for (int i = 0; i < switches.Length; i++)
        {
            SwitchBinding binding = switches[i];
            SwitchMotion motion = switchMotions[i];
            if (motion.handle != null) UpdateHandle(binding, motion, switchOn[i], deltaTime);
            if (motion.lightPhase != LightPhase.Off && motion.lightPhase != LightPhase.Steady)
                UpdateLight(binding, motion, deltaTime);
        }
    }

    private void UpdateHandle(SwitchBinding binding, SwitchMotion motion, bool on, float deltaTime)
    {
        motion.handleElapsed += deltaTime;
        float t = Mathf.Clamp01(motion.handleElapsed / Mathf.Max(0.01f, handleMoveDuration));
        HandlePose end = on ? motion.onPose : motion.offPose;
        if (t >= 1f)
        {
            // 两个端点用同一素材；重合后交接显示，恢复原对象供下次反向使用。
            motion.onPose.Apply(binding.onVisual.transform);
            motion.offPose.Apply(binding.offVisual.transform);
            binding.onVisual.SetActive(on);
            binding.offVisual.SetActive(!on);
            motion.handle = null;
            return;
        }

        const float contact = 0.72f;
        float travel = t < contact
            ? 1.065f * Mathf.SmoothStep(0f, 1f, t / contact)
            : Mathf.Lerp(1.065f, 1f, Mathf.SmoothStep(0f, 1f, (t - contact) / (1f - contact)));
        float bulge = Mathf.Sin(t * Mathf.PI);
        motion.handle.localPosition = Vector3.LerpUnclamped(motion.startPose.position, end.position, travel);
        motion.handle.localRotation = Quaternion.SlerpUnclamped(motion.startPose.rotation, end.rotation, travel)
            * Quaternion.Euler(0f, 0f, (on ? -3f : 3f) * bulge);
        Vector3 scale = Vector3.LerpUnclamped(motion.startPose.scale, end.scale, travel);
        scale.x *= 1f + 0.08f * bulge;
        // 原关态为负 Y、开态为正 Y；压到薄边再翻面，避免跨零时整张图消失。
        float edge = Mathf.Abs(end.scale.y) * 0.12f;
        if (Mathf.Abs(scale.y) < edge) scale.y = (scale.y < 0f ? -1f : 1f) * edge;
        motion.handle.localScale = scale;
    }

    private static void BeginLightPhase(SwitchMotion motion, LightPhase phase, float target, float duration)
    {
        motion.lightPhase = phase;
        motion.lightFrom = motion.lightLevel;
        motion.lightTarget = target;
        motion.lightElapsed = 0f;
        motion.lightDuration = Mathf.Max(0.01f, duration);
    }

    private void UpdateLight(SwitchBinding binding, SwitchMotion motion, float deltaTime)
    {
        motion.lightElapsed += deltaTime;
        float t = Mathf.Clamp01(motion.lightElapsed / motion.lightDuration);
        ApplyLightLevel(binding, motion, Mathf.Lerp(motion.lightFrom, motion.lightTarget,
            Mathf.SmoothStep(0f, 1f, t)));
        if (t < 1f) return;

        switch (motion.lightPhase)
        {
            case LightPhase.Contact:
                BeginLightPhase(motion, LightPhase.FadeIn, 1f, lightFadeInDuration);
                break;
            case LightPhase.FadeIn:
                BeginFlickerDim(motion);
                break;
            case LightPhase.FlickerDim:
                BeginLightPhase(motion, LightPhase.FlickerBright, 1f, UnityEngine.Random.Range(0.025f, 0.045f));
                break;
            case LightPhase.FlickerBright:
                if (--motion.flickersLeft > 0) BeginFlickerDim(motion);
                else motion.lightPhase = LightPhase.Steady;
                break;
            case LightPhase.FadeOut:
                motion.lightPhase = LightPhase.Off;
                binding.lightVisual.SetActive(false);
                break;
        }
    }

    private static void BeginFlickerDim(SwitchMotion motion)
    {
        BeginLightPhase(motion, LightPhase.FlickerDim, UnityEngine.Random.Range(0.06f, 0.25f),
            UnityEngine.Random.Range(0.025f, 0.045f));
    }

    private static void ApplyLightLevel(SwitchBinding binding, SwitchMotion motion, float level)
    {
        motion.lightLevel = level;
        Color color = motion.lightTint;
        color.a *= level;
        binding.lightRenderer.color = color;
    }
    #endregion
}
