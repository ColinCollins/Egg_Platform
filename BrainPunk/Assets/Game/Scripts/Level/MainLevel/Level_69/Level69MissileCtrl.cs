using Game.Common;
using Game.Scripts.Common;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 控制missile poolObject 回收和移动。
/// 触碰 Player 播放 explosion1
/// 触碰 Ground 播放 explosion2
/// </summary>
[DefaultExecutionOrder(-50)]
public class Level69MissileCtrl : MonoBehaviour, IRecycle
{
    [Header("移动设置")]
    [Tooltip("默认向下移动速度")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("碰撞特效")]
    [Tooltip("触碰 Player 时实例化的爆炸特效模板")]
    [SerializeField] private ParticleSystem explosion1;

    [Tooltip("触碰 Ground 时实例化的爆炸特效模板")]
    [SerializeField] private ParticleSystem explosion2;

    [Header("回收设置")]
    [Tooltip("超出屏幕底部多少距离后自动回收")]
    [SerializeField] private float autoRecycleBottomOffset = 2f;

    [Header("玩家受击反馈")]
    [Tooltip("玩家被导弹命中后的归一化炸飞方向；实际方向会结合导弹与玩家位置。")]
    [SerializeField] private Vector2 playerKnockbackDirection = new Vector2(0.6f, 1f);

    [Tooltip("玩家被导弹命中后的炸飞力度。")]
    [SerializeField] private float playerKnockbackForce = 12f;

    [Tooltip("炸飞状态持续时间；失败重开仍由 BaseLevelCtrl 负责。")]
    [SerializeField] private float playerKnockbackDuration = 0.5f;

    /// <summary>
    /// 爆炸偏移
    /// </summary>
    [SerializeField] private Vector3 explosionOffset;

    private bool _isActive;
    private Collider2D _cachedCollider;
    private Transform _cachedTransform;
    private Camera _mainCamera;
    private Level69Ctrl _owner;
    private ActorCtrl _actor;
    private bool _collisionResolved;
    private bool _pendingActorHit;

    // 当前处于活跃状态的导弹实例，只在 Level69 销毁时统一回收
    private static readonly HashSet<Level69MissileCtrl> activeInstances = new HashSet<Level69MissileCtrl>();

    /// <summary>
    /// 当前 Level69 活跃导弹数量，用于在调用共享对象池前执行本关 30 个上限保护。
    /// </summary>
    public static int ActiveCount => activeInstances.Count;

    private void Awake()
    {
        _cachedTransform = transform;
        _cachedCollider = GetComponent<Collider2D>();
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!_isActive)
            return;

        _cachedTransform.position += Vector3.down * (moveSpeed * Time.deltaTime);

        if (IsOutOfBottom())
        {
            RecycleSelf();
        }
    }

    private void LateUpdate()
    {
        if (!_isActive || _collisionResolved || !_pendingActorHit)
            return;

        _pendingActorHit = false;
        if (_owner != null && _owner.IsMissileBlockedByShield(_cachedCollider))
        {
            ResolveNonPlayerHit();
            return;
        }

        ResolveActorHit();
    }

    /// <summary>
    /// 初始化导弹，每次从对象池取出时调用
    /// </summary>
    public void Init()
    {
        _isActive = true;
        _collisionResolved = false;
        _pendingActorHit = false;
        if (_cachedCollider != null)
            _cachedCollider.enabled = true;
    }

    /// <summary>
    /// 初始化本次池化导弹所属的 Level69 会话和玩家引用；每次从池中取出后必须调用。
    /// </summary>
    /// <param name="owner">当前 Level69Ctrl，会话结束后旧导弹回调会被拒绝。</param>
    /// <param name="actor">当前关卡 Actor，只用于本次命中的死亡与炸飞反馈。</param>
    public void Init(Level69Ctrl owner, ActorCtrl actor)
    {
        _owner = owner;
        _actor = actor;
        Init();
    }

    /// <summary>
    /// 设置导弹移动速度
    /// </summary>
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!_isActive || _collisionResolved)
            return;

        string tag = collider.gameObject.tag;
        string layerName = LayerMask.LayerToName(collider.gameObject.layer);

        if (tag.Equals("Player") || layerName.Equals("Actor"))
        {
            _pendingActorHit = true;
            AudioManager.PlaySound("rocketExplosion");
        }
        else if (tag.Equals("Ground") || layerName.Equals("Ground") || layerName.Equals("TriggerItem"))
        {
            ResolveNonPlayerHit();
        }
    }

    private void ResolveNonPlayerHit()
    {
        if (!_isActive || _collisionResolved)
            return;

        _collisionResolved = true;
        _pendingActorHit = false;
        PlayExplosion(explosion2);
        AudioManager.PlaySound("rocketExplosion");
        RecycleSelf();
    }

    private void ResolveActorHit()
    {
        if (!_isActive || _collisionResolved)
            return;

        _collisionResolved = true;
        Vector2 knockbackDirection = playerKnockbackDirection;
        if (_actor != null)
        {
            float horizontalSign = Mathf.Sign(_actor.transform.position.x - _cachedTransform.position.x);
            if (!Mathf.Approximately(horizontalSign, 0f))
                knockbackDirection.x = Mathf.Abs(knockbackDirection.x) * horizontalSign;
        }

        knockbackDirection = knockbackDirection.sqrMagnitude > 0f ? knockbackDirection.normalized : Vector2.up;
        PlayExplosion(explosion1);

        _owner?.TryCommitPlayerHit(
            _actor,
            knockbackDirection,
            Mathf.Max(0f, playerKnockbackForce),
            Mathf.Max(0f, playerKnockbackDuration));

        if (_isActive)
            RecycleSelf();
    }

    private void PlayExplosion(ParticleSystem template)
    {
        if (template == null)
            return;

        Vector3 position = _cachedTransform.position + explosionOffset;
        ParticleSystem instance = Instantiate(template, position, template.transform.rotation);
        instance.transform.localScale = template.transform.lossyScale;
        instance.gameObject.SetActive(true);
        instance.Play(true);
        Destroy(instance.gameObject, GetExplosionLifetime(instance));
    }

    private static float GetExplosionLifetime(ParticleSystem root)
    {
        float lifetime = 0.1f;
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            float systemLifetime = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
            lifetime = Mathf.Max(lifetime, systemLifetime);
        }

        return lifetime;
    }

    private bool IsOutOfBottom()
    {
        if (_mainCamera == null)
            return false;

        float bottomY = _mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0f, 0f)).y;
        return _cachedTransform.position.y < bottomY - autoRecycleBottomOffset;
    }

    public void OnSpawn()
    {
        gameObject.SetActive(true);
        _isActive = true;
        _collisionResolved = false;
        _pendingActorHit = false;
        if (_cachedCollider != null)
            _cachedCollider.enabled = true;

        activeInstances.Add(this);
    }

    public void OnRecycle()
    {
        _isActive = false;
        _collisionResolved = false;
        _pendingActorHit = false;
        _owner = null;
        _actor = null;
        if (_cachedCollider != null)
            _cachedCollider.enabled = false;
        gameObject.SetActive(false);

        activeInstances.Remove(this);
    }

    public void RecycleSelf()
    {
        if (!_isActive)
            return;

        _isActive = false;
        _collisionResolved = true;
        _pendingActorHit = false;
        if (_cachedCollider != null)
            _cachedCollider.enabled = false;

        activeInstances.Remove(this);

        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Recycle(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 回收所有当前活跃的导弹实例，由 Level69 销毁时调用
    /// </summary>
    public static void RecycleAllActive()
    {
        if (activeInstances.Count == 0)
            return;

        // 遍历时可能修改集合，先复制一份
        Level69MissileCtrl[] snapshot = new Level69MissileCtrl[activeInstances.Count];
        activeInstances.CopyTo(snapshot);

        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] != null)
                snapshot[i].RecycleSelf();
        }

        // 清理已被销毁但未正常移除的引用
        activeInstances.RemoveWhere(item => item == null);
    }
}
