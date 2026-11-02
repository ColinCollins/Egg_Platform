using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 角色用身体推动的冰块误导：靠近热门后固定在原地融化，碰撞盒同步缩小并留下水迹。
/// 门只短暂褪红，不修改通关状态、角色受热距离或手动风扇进度。
/// </summary>
public sealed class Level144IceDecoy : MonoBehaviour
{
    #region 显式引用与参数
    [Tooltip("52 关冰块原图的本关 SpriteRenderer，视觉缩放不直接缩放刚体根节点。")]
    [SerializeField] private SpriteRenderer iceSprite;
    [Tooltip("52 关水迹原图；固定在开始融化的地面位置，化完后保留到重开。")]
    [SerializeField] private SpriteRenderer puddleSprite;
    [Tooltip("本关热门触发盒，按两个碰撞盒边缘之间的距离判断是否开始融化。")]
    [SerializeField] private Collider2D doorArea;
    [Tooltip("预先放置的蒸汽粒子，沿用本关柔和烟雾材质。")]
    [SerializeField] private ParticleSystem steam;
    [Tooltip("冰块动态刚体，由角色碰撞推动；开始融化后锁定位置，不接收拖拽输入。")]
    [SerializeField] private Rigidbody2D iceBody;
    [Tooltip("冰块实体盒；融化时保持底边不变、同步缩小，化完后禁用。")]
    [SerializeField] private BoxCollider2D iceCollider;
    [Tooltip("开局相对编辑时落点的下落高度；编辑模式保持落地摆放，便于调整。")]
    [SerializeField, Min(0f)] private float dropHeight = 9f;
    [Tooltip("下落时使用的重力倍率，落地后恢复推动时的原重力。")]
    [SerializeField, Min(0.1f)] private float dropGravityScale = 7f;
    [Tooltip("只有与这些地面层接触才算落地，不把碰到角色或门算作落地。")]
    [SerializeField] private LayerMask landingLayers = (1 << 6) | (1 << 13);
    [Tooltip("冰块边缘距热门小于此距离时开始融化（世界单位）。")]
    [SerializeField, Min(0.1f)] private float meltStartDistance = 1f;
    [Tooltip("开始受热后完全化成水的时长（秒）；不要求持续推动。")]
    [SerializeField, Min(0.1f)] private float meltDuration = 2f;
    [Tooltip("最大暂时褪红比例，仅影响热门显示，不允许冰块完成关卡。")]
    [SerializeField, Range(0f, 0.8f)] private float coolingStrength = 0.45f;
    #endregion

    #region 本次尝试状态
    private enum IceState { Solid, Melting, Spent, Falling }
    private IceState state;
    private bool initialized;
    private bool suspended;
    private bool wasSimulated;
    private Vector2 suspendedVelocity;
    private float suspendedAngularVelocity;
    private Vector3 originalIceScale;
    private Vector3 originalIcePosition;
    private Vector3 originalPuddleScale;
    private Vector2 originalColliderSize;
    private Vector2 originalColliderOffset;
    private Color originalIceColor;
    private Color originalPuddleColor;
    private float originalVisualBottom;
    private float meltElapsed;
    private float originalGravityScale;
    private bool landingPending;

    /// <summary>融化期间的短暂褪红比例；开始和结束都为 0，关卡只用于门的显示。</summary>
    public float CoolingAmount => state == IceState.Melting
        ? coolingStrength * Mathf.Sin(Mathf.Clamp01(meltElapsed / Mathf.Max(0.1f, meltDuration)) * Mathf.PI) : 0f;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        originalIceScale = iceSprite.transform.localScale;
        originalIcePosition = iceSprite.transform.localPosition;
        originalPuddleScale = puddleSprite.transform.localScale;
        originalColliderSize = iceCollider.size;
        originalColliderOffset = iceCollider.offset;
        originalIceColor = iceSprite.color;
        originalPuddleColor = puddleSprite.color;
        originalGravityScale = iceBody.gravityScale;
        originalVisualBottom = iceSprite.sprite.bounds.min.y * originalIceScale.y;
        puddleSprite.enabled = false;
        steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = steam.main;
        main.startColor = new Color(0.66f, 0.7f, 0.72f, 0.72f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = steam.emission;
        emission.rateOverTime = 20f;
        if (dropHeight > 0f)
        {
            state = IceState.Falling;
            // 开局传送同时对齐 Transform 与物理姿态，避免暂停模拟后同步回编辑落点。
            iceBody.transform.position += Vector3.up * dropHeight;
            iceBody.position = iceBody.transform.position;
            iceBody.gravityScale = dropGravityScale;
        }
        initialized = true;
        // 等关卡进入可游玩状态再下落，加载/暂停期间不提前落地。
        SetSuspended(true);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying || !initialized) return;
        SetSuspended(true);
        if (steam != null) steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    #endregion

    #region 下落与首次着地
    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckLanding(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckLanding(collision);
    }

    private void CheckLanding(Collision2D collision)
    {
        if (!Application.isPlaying || !initialized || suspended || state != IceState.Falling) return;
        if ((landingLayers.value & (1 << collision.gameObject.layer)) == 0) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < 0.5f) continue;
            state = IceState.Solid;
            landingPending = true;
            iceBody.gravityScale = originalGravityScale;
            iceBody.linearVelocity = new Vector2(iceBody.linearVelocity.x, 0f);
            return;
        }
    }

    /// <summary>关卡消费一次首次着地通知以震屏；推动、重复接触和暂停恢复不重复触发。</summary>
    public bool ConsumeLanding()
    {
        if (!Application.isPlaying || !initialized || suspended || !landingPending) return false;
        landingPending = false;
        return true;
    }
    #endregion

    #region 受热与融化
    /// <summary>仅由可游玩状态的关卡推进；永久冷却后不再让完整冰块开始融化。</summary>
    public void Tick(float deltaTime, bool doorIsHot)
    {
        if (!Application.isPlaying || !initialized || suspended || !isActiveAndEnabled) return;
        if (steam.isPaused)
        {
            steam.Play();
            if (state != IceState.Melting) steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (state == IceState.Solid && doorIsHot && DoorDistance() <= meltStartDistance) BeginMelt();
        if (state != IceState.Melting) return;
        meltElapsed = Mathf.Min(meltDuration, meltElapsed + Mathf.Max(0f, deltaTime));
        float progress = Mathf.Clamp01(meltElapsed / Mathf.Max(0.1f, meltDuration));
        float blend = Mathf.SmoothStep(0f, 1f, progress);
        float remaining = 1f - blend;
        // 视觉和实体都向底边收缩，避免因缩小使冰块悬空或留下看不见的障碍。
        iceSprite.transform.localScale = Vector3.Scale(originalIceScale, new Vector3(remaining, remaining, 1f));
        iceSprite.transform.localPosition = originalIcePosition + Vector3.up * originalVisualBottom * blend;
        Vector2 size = originalColliderSize * Mathf.Max(0.03f, remaining);
        iceCollider.size = size;
        iceCollider.offset = new Vector2(originalColliderOffset.x * remaining,
            originalColliderOffset.y - originalColliderSize.y * 0.5f + size.y * 0.5f);
        Color ice = originalIceColor;
        ice.a *= remaining;
        iceSprite.color = ice;
        puddleSprite.transform.localScale = Vector3.Scale(originalPuddleScale, new Vector3(Mathf.Lerp(0.15f, 1f, blend), Mathf.Lerp(0.4f, 1f, blend), 1f));
        Color water = originalPuddleColor;
        water.a *= blend;
        puddleSprite.color = water;
        steam.transform.position = iceSprite.bounds.center;
        if (progress < 1f) return;
        state = IceState.Spent;
        iceSprite.enabled = false;
        iceCollider.enabled = false;
        iceBody.linearVelocity = Vector2.zero;
        iceBody.simulated = false;
        steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private float DoorDistance()
    {
        Bounds ice = iceCollider.bounds;
        Bounds door = doorArea.bounds;
        float dx = Mathf.Max(0f, Mathf.Max(door.min.x - ice.max.x, ice.min.x - door.max.x));
        float dy = Mathf.Max(0f, Mathf.Max(door.min.y - ice.max.y, ice.min.y - door.max.y));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private void BeginMelt()
    {
        state = IceState.Melting;
        meltElapsed = 0f;
        // 开始化水就固定在当前落点，保留实体直到化完；暂停恢复也不解除约束。
        iceBody.linearVelocity = Vector2.zero;
        iceBody.angularVelocity = 0f;
        iceBody.constraints = RigidbodyConstraints2D.FreezeAll;
        // 水迹是地面上的独立图像；只在开始融化时定位，之后只增长，不跟随刚体。
        puddleSprite.transform.position = new Vector3(iceBody.position.x, iceCollider.bounds.min.y + 0.02f, iceSprite.transform.position.z);
        puddleSprite.enabled = true;
        Color water = originalPuddleColor;
        water.a = 0f;
        puddleSprite.color = water;
        steam.transform.position = iceSprite.bounds.center;
        steam.Play();
        AudioManager.PlaySound("splash");
    }
    #endregion

    #region 暂停和退出
    /// <summary>暂停/失焦时冻结本物体物理与蒸汽；恢复时保留剩余融化进度，化完的碰撞不恢复。</summary>
    public void SetSuspended(bool value)
    {
        if (!Application.isPlaying || !initialized || suspended == value) return;
        suspended = value;
        if (value)
        {
            wasSimulated = iceBody.simulated;
            suspendedVelocity = iceBody.linearVelocity;
            suspendedAngularVelocity = iceBody.angularVelocity;
            iceBody.simulated = false;
            if (steam.IsAlive(true)) steam.Pause(true);
        }
        else if (state != IceState.Spent)
        {
            iceBody.simulated = wasSimulated;
            iceBody.linearVelocity = suspendedVelocity;
            iceBody.angularVelocity = suspendedAngularVelocity;
        }
    }
    #endregion
}
