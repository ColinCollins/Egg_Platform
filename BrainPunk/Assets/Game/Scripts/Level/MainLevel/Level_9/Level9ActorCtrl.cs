using System.Collections.Generic;
using Bear.EventSystem;
using Game.Events;
using Spine.Unity;
using UnityEngine;

/// <summary>009／011／012／019 显式接入的岩浆角色：仅岩浆死亡时变黑、冒烟并持续下沉，其余移动沿用 ActorCtrl。</summary>
public sealed class Level9ActorCtrl : ActorCtrl
{
    #region 显式绑定和局部参数
    [SerializeField, Tooltip("本角色根刚体；只改变当前关卡实例。")]
    private Rigidbody2D burnBody;
    [SerializeField, Tooltip("死亡后关闭以避免池底碰撞或浮力重新托起。")]
    private Collider2D bodyCollider;
    [SerializeField, Tooltip("死亡后关闭的推物体碰撞节点。")]
    private GameObject burnPushCollider;
    [SerializeField, Tooltip("主角 Spine，直接改实例 RGB，保留现有透明度。")]
    private SkeletonAnimation burnSkeleton;
    [SerializeField, Tooltip("已绑定的三个装扮挂点；只收集其运行时生成的服饰。")]
    private Transform[] clothingMounts;
    [SerializeField, Tooltip("烟雾挂在关卡根下，避免继承 Actor 或 Water 的缩放/排序组。")]
    private Transform effectRoot;
    [SerializeField, Tooltip("借用只读烟雾材质，不修改材质资源。")]
    private Material smokeMaterial;
    [SerializeField, Tooltip("用户提供的 shaojiao 音频，死亡只播放一次。")]
    private AudioClip burnClip;
    [SerializeField, Min(0.05f), Tooltip("从原色到炭黑的时间，单位秒。")]
    private float blackenSeconds = 0.4f;
    [SerializeField, Min(0.1f), Tooltip("下沉最低速度；不使用上浮或弹起动画。")]
    private float sinkSpeed = 3f;
    [SerializeField, Range(0f, 1f), Tooltip("烧焦音效音量，遵循玩家音效开关。")]
    private float burnVolume = 0.65f;
    #endregion

    #region 运行时状态
    private bool burning;
    private float burnAge;
    private Color originalColor;
    private ParticleSystem smoke;
    private AudioSource burnAudio;
    private EventSubscriber events;
    private readonly Dictionary<SpriteRenderer, Color> clothingColors = new Dictionary<SpriteRenderer, Color>();
    private readonly List<SpriteRenderer> clothingBuffer = new List<SpriteRenderer>();
    #endregion

    #region 岩浆动作
    /// <summary>仅由本关 FallArea 在派发标准失败事件后调用；幂等，不直接重开或写存档。</summary>
    public void BeginLavaBurn()
    {
        if (!Application.isPlaying || burning || IsDied || burnBody == null || burnSkeleton == null) return;
        burning = true;
        burnAge = 0f;
        originalColor = new Color(burnSkeleton.Skeleton.R, burnSkeleton.Skeleton.G, burnSkeleton.Skeleton.B, 1f);
        base.Die();
        bodyCollider.enabled = false;
        burnPushCollider.SetActive(false);
        burnBody.gravityScale = 1f;
        burnBody.linearVelocity = Vector2.down * sinkSpeed;
        CreateSmoke();
        // 私有音源随当前角色销毁，不改变共享音效集合或生成表。
        if (burnClip != null)
        {
            burnAudio = gameObject.AddComponent<AudioSource>();
            burnAudio.playOnAwake = false;
            burnAudio.loop = false;
            burnAudio.spatialBlend = 0f;
            burnAudio.volume = burnVolume;
            burnAudio.clip = burnClip;
            if (DB.GameSetting.SfxOn) burnAudio.Play();
            EventsUtils.ResetEvents(ref events);
            events.Subscribe<SfxToggleEvent>(OnSfxToggle);
        }
    }

    /// <summary>非烧焦状态完整沿用原移动；烧焦后不再提交输入和普通状态动画。</summary>
    public override void OnUpdate()
    {
        if (!burning) base.OnUpdate();
    }

    /// <summary>烧焦期间即使收到迟到的跳跃事件也不弹起。</summary>
    protected override void JumpButton()
    {
        if (!burning) base.JumpButton();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying || !burning) return;
        burnBody.linearVelocity = new Vector2(0f, Mathf.Clamp(burnBody.linearVelocityY, -sinkSpeed * 1.5f, -sinkSpeed));
    }

    private void Update()
    {
        if (!Application.isPlaying || !burning) return;
        burnAge += Time.deltaTime;
        float t = Mathf.Clamp01(burnAge / Mathf.Max(0.05f, blackenSeconds));
        Color tint = Color.Lerp(originalColor, new Color(0.08f, 0.065f, 0.055f), t);
        // Skeleton 的整体颜色不受动画 slot 颜色曲线覆盖，也能覆盖分离渲染出的前后半身。
        burnSkeleton.Skeleton.R = tint.r;
        burnSkeleton.Skeleton.G = tint.g;
        burnSkeleton.Skeleton.B = tint.b;
        foreach (Transform mount in clothingMounts)
        {
            if (mount == null) continue;
            // 挂点均显式绑定；下面只枚举 Skin 系统运行时加载的服饰，不查找预摆对象。
            clothingBuffer.Clear();
            mount.GetComponentsInChildren(true, clothingBuffer);
            foreach (SpriteRenderer sprite in clothingBuffer)
            {
                if (!clothingColors.TryGetValue(sprite, out Color start))
                {
                    start = sprite.color;
                    clothingColors.Add(sprite, start);
                }
                Color next = Color.Lerp(start, new Color(0.08f, 0.065f, 0.055f), t);
                next.a = sprite.color.a;
                sprite.color = next;
            }
        }
        if (smoke != null) smoke.transform.position = transform.position + Vector3.up * 0.45f;
    }
    #endregion

    #region 有限烟粒子和清理
    private void CreateSmoke()
    {
        if (smokeMaterial == null || effectRoot == null) return;
        var effect = new GameObject("Level009_BurnSmoke");
        effect.transform.SetParent(effectRoot, false);
        effect.transform.position = transform.position + Vector3.up * 0.45f;
        smoke = effect.AddComponent<ParticleSystem>();
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = smoke.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.85f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.6f);
        main.startSpeed = 0f;
        main.startColor = new Color(0.27f, 0.25f, 0.24f, 0.8f);
        main.maxParticles = 28;
        var emission = smoke.emission;
        emission.rateOverTime = 24f;
        var shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;
        var velocity = smoke.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        velocity.y = new ParticleSystem.MinMaxCurve(1.5f, 2.4f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var size = smoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f));
        var color = smoke.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var renderer = effect.GetComponent<ParticleSystemRenderer>(); // 本方法新建并拥有的对象。
        renderer.sharedMaterial = smokeMaterial;
        renderer.sortingLayerID = 1752186269;
        renderer.sortingOrder = 203;
        smoke.Play();
    }

    private void OnSfxToggle(SfxToggleEvent evt)
    {
        if (!evt.isOn && burnAudio != null) burnAudio.Stop();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        EventsUtils.ResetEvents(ref events);
        if (burnAudio != null) burnAudio.Stop();
        if (smoke != null) Destroy(smoke.gameObject);
        if (!burning) return;
        if (burnSkeleton != null && burnSkeleton.Skeleton != null)
        {
            burnSkeleton.Skeleton.R = originalColor.r;
            burnSkeleton.Skeleton.G = originalColor.g;
            burnSkeleton.Skeleton.B = originalColor.b;
        }
        foreach (var pair in clothingColors)
        {
            if (pair.Key == null) continue;
            Color restored = pair.Value;
            restored.a = pair.Key.color.a;
            pair.Key.color = restored;
        }
    }
    #endregion
}
