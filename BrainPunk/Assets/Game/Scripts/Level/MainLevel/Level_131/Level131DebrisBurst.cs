using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本局专用的碎片池。每根下柱播放一次原序列化碎片集，不在方块尾部持续发射。
/// 实例在推进前预备，播完才复用；关卡销毁时统一释放，不回收或复活障碍柱。
/// </summary>
public sealed class Level131DebrisBurst
{
    // 保留既有容量窗口；池容量继续覆盖最长播放窗口和密集连续命中。
    public const float CapacityWindowSeconds = 2f;
    private const float VisualScale = 1f;
    private static readonly Vector3 EmissionArea = new(0.15f, 1.5f, 0.01f);

    private sealed class Slot
    {
        public ParticleSystem Particles;
        public ParticleSystemRenderer[] Renderers;
        public int StartedFrame;
        public bool InUse;
    }

    private readonly ParticleSystem prefab;
    private readonly Transform poolRoot;
    private readonly Transform stagingRoot;
    private readonly List<Slot> slots;
    private bool paused;
    private bool overflowReported;

    /// <summary>
    /// 创建本关碎片复用池。池根保持为独立场景根节点，只由关卡控制器在销毁时显式释放。
    /// </summary>
    /// <param name="burstPrefab">Inspector 绑定的碎片粒子 Prefab。</param>
    /// <param name="ownerContext">仅用于缺少 Prefab 时定位日志来源，不作为粒子父节点。</param>
    /// <param name="capacity">推进开始前需要预创建的槽位数量。</param>
    public Level131DebrisBurst(
        ParticleSystem burstPrefab,
        Object ownerContext,
        int capacity)
    {
        prefab = burstPrefab;
        slots = new List<Slot>(Mathf.Max(1, capacity));
        if (prefab == null)
        {
            Debug.LogError("Level 131 缺少序列化碎片集，无法预备破障特效。", ownerContext);
            return;
        }

        GameObject root = new("Level131DebrisPool");
        poolRoot = root.transform;
        GameObject staging = new("InactiveCreationRoot");
        stagingRoot = staging.transform;
        stagingRoot.SetParent(poolRoot, false);
        staging.SetActive(false);
        for (int index = 0; index < Mathf.Max(1, capacity); index++)
            CreateSlot();
    }

    private Slot CreateSlot()
    {
        // 单独的 inactive 创建根阻止 playOnAwake；扩容时不能停用承载正在播放效果的池根。
        ParticleSystem instance = Object.Instantiate(prefab, stagingRoot, false);
        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = instance.main;
        main.loop = false;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.useUnscaledTime = false;
        main.startDelay = 0f;
        // 独立池根作为模拟空间，避免平台、障碍或关卡布局节点移动/销毁时截断在播粒子。
        main.simulationSpace = ParticleSystemSimulationSpace.Custom;
        main.customSimulationSpace = poolRoot;
        // 保留原 Prefab 的粒子尺寸，不再额外缩小。
        main.maxParticles = 12;

        ParticleSystem.EmissionModule emission = instance.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.burstCount = 1;
        ConfigureSpeed(instance, 0f);

        // 竖向窄带只负责分散出生位置；移动方向由世界空间速度统一指向左侧。
        ParticleSystem.ShapeModule shape = instance.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.scale = EmissionArea;
        // Play 使用源 Prefab 的世界朝向，抵消其 -90° X，保持窄带沿画面 Y 方向。
        shape.rotation = Quaternion.Inverse(prefab.transform.rotation).eulerAngles;
        shape.randomDirectionAmount = 0f;
        shape.sphericalDirectionAmount = 0f;
        ParticleSystem.ForceOverLifetimeModule force = instance.forceOverLifetime;
        force.space = ParticleSystemSimulationSpace.World;

        ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);

        // 只在创建槽位时预模拟后清空，提前准备粒子数据；不改变原图集、材质和随机帧。
        bool[] rendererEnabled = new bool[renderers.Length];
        for (int index = 0; index < renderers.Length; index++)
        {
            rendererEnabled[index] = renderers[index].enabled;
            renderers[index].enabled = false;
        }
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(poolRoot, false);
        instance.transform.localScale = Vector3.one * VisualScale;
        instance.gameObject.SetActive(true);
        instance.Simulate(0.02f, true, true, false);
        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        instance.gameObject.SetActive(false);
        for (int index = 0; index < renderers.Length; index++)
            renderers[index].enabled = rendererEnabled[index];
        Slot slot = new() { Particles = instance, Renderers = renderers };
        slots.Add(slot);
        return slot;
    }

    private static void ConfigureSpeed(ParticleSystem instance, float speedFactor)
    {
        ParticleSystem.MainModule main = instance.main;
        // 播放时长和单颗寿命沿用 Prefab，不在池代码中提前截断完整序列。
        // 不使用形状自带的发射方向，避免径向或向景深喷出。
        main.startSpeed = 0f;
        main.gravityModifier = Mathf.Lerp(2.5f, 5f, speedFactor);

        ParticleSystem.VelocityOverLifetimeModule velocity = instance.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // 保留原速度档的水平速率范围，仅把方向固定为世界左侧。
        velocity.x = new ParticleSystem.MinMaxCurve(
            -Mathf.Lerp(2.4f, 6f, speedFactor),
            -Mathf.Lerp(1.2f, 3.5f, speedFactor));
        velocity.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        // 三轴必须使用相同曲线模式；0～0 保持无 Z 速度，同时与 X/Y 的 TwoConstants 一致。
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.EmissionModule emission = instance.emission;
        // SetBurst 写既有槽位，不再为每次撞击分配 Burst[]。
        emission.SetBurst(0, new ParticleSystem.Burst(
            0f, (short)Mathf.RoundToInt(Mathf.Lerp(6f, 12f, speedFactor))));
    }

    public void Play(Vector3 position, float speedFactor, int sortingLayerId, int sortingOrder)
    {
        if (prefab == null || poolRoot == null || paused)
            return;

        Slot available = null;
        foreach (Slot slot in slots)
        {
            if (HasFinished(slot))
                Release(slot);
            if (!slot.InUse)
            {
                available = slot;
                break;
            }
        }

        if (available == null)
        {
            // 异常密度/运行中改配置时宁可扩容，也不能漏掉一根柱或截断仍在播放的特效。
            // 正常容量在推进前按最高速度和组间净空预估；扩容不属于无创建的正常热路径。
            if (!overflowReported)
            {
                overflowReported = true;
                Debug.LogWarning("Level 131 碎片池容量不足，扩容保留本次破障效果；需复核速度/间距配置。", poolRoot);
            }
            available = CreateSlot();
        }

        ParticleSystem instance = available.Particles;
        // 每次复用都从停止且清空的状态开始，再配置、激活与播放；不延迟柱体反馈。
        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        instance.transform.position = position;
        instance.transform.rotation = prefab.transform.rotation;
        instance.transform.localScale = Vector3.one * VisualScale;
        ConfigureSpeed(instance, Mathf.Clamp01(speedFactor));
        foreach (ParticleSystemRenderer renderer in available.Renderers)
        {
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
        }
        available.InUse = true;
        available.StartedFrame = Time.frameCount;
        instance.gameObject.SetActive(true);
        instance.Play(true);
    }

    public void Tick()
    {
        if (paused)
            return;

        foreach (Slot slot in slots)
        {
            if (HasFinished(slot))
                Release(slot);
        }
    }

    private static bool HasFinished(Slot slot)
    {
        // 不在 Play 所在帧判断完成，避免同帧的物理回调/Update 抢先清空刚启动的实例。
        return slot.InUse && Time.frameCount > slot.StartedFrame && !slot.Particles.IsAlive(true);
    }

    private static void Release(Slot slot)
    {
        slot.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        slot.Particles.gameObject.SetActive(false);
        slot.InUse = false;
    }

    public void SetPaused(bool value)
    {
        if (paused == value)
            return;
        paused = value;
        foreach (Slot slot in slots)
        {
            if (!slot.InUse)
                continue;
            if (paused)
                slot.Particles.Pause(true);
            else
                slot.Particles.Play(true);
        }
    }

    public void Dispose()
    {
        slots.Clear();
        if (poolRoot != null)
            Object.Destroy(poolRoot.gameObject);
    }
}
