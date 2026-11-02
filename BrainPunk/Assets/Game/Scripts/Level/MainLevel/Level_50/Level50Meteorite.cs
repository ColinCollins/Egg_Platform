
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Common;
using Game.Events;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;


/// <summary>
/// 陨石对象，prefab 上需自带此脚本，参考 OnTriggerTrapHandle 实现碰撞与对象池回收
/// </summary>
public class Level50Meteorite : MonoBehaviour, IRecycle, IEventSender
{
    [SerializeField] private ActorCtrl actor;

    [SerializeField] private ParticleSystem knock;
    [SerializeField] private float lifetime = 5f;

    [SerializeField] private Vector2 MoveSpeed = new Vector2(1, 3);
    private float fallSpeed;
    private float autoRecycleY = -50f;
    private bool isActive;
    private bool isTriggered;
    private Transform cachedTransform;
    private float lifeTimer;
    private Collider2D cachedCollider;

    private AutoRotate rotate;
    private DirectMove move;

    private void Awake()
    {
        cachedTransform = transform;
        cachedCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (!isActive)
            return;

        cachedTransform.position += Vector3.down * (fallSpeed * Time.deltaTime);

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f || cachedTransform.position.y < autoRecycleY)
        {
            RecycleSelf();
        }
    }

    /// <summary>
    /// 初始化陨石位置、速度与目标 Actor（直接赋值）
    /// </summary>
    public void Init(Vector3 position, float speed, ActorCtrl actorCtrl, ParticleSystem knock)
    {
        cachedTransform.position = position;
        fallSpeed = speed;
        actor = actorCtrl;
        this.knock = knock;
        isActive = true;
        isTriggered = false;
        lifeTimer = lifetime;
        if (cachedCollider != null)
            cachedCollider.enabled = true;
    }

    /// <summary>
    /// 设置自动回收的 Y 坐标
    /// </summary>
    public void SetAutoRecycleY(float y)
    {
        autoRecycleY = y;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!isActive || isTriggered)
            return;

        var tag = collider.gameObject.tag;
        var layerName = LayerMask.LayerToName(collider.gameObject.layer);
        if (layerName.Equals("Actor") && tag.Equals("Player"))
        {
            TriggerTrap(collider);
        }
    }

    private void TriggerTrap(Collider2D collider)
    {
        isTriggered = true;

        if (actor != null && !actor.IsDied)
        {
            actor.Die();
            actor.Knockback(new Vector2(.2f, 1f), 10f).Forget();
            actor.transform.DOLocalRotate(new Vector3(0, 0, 360f), 0.5f, RotateMode.FastBeyond360).SetLoops(-1);
            
            knock.transform.position = actor.transform.position;
            knock.Play();

            AudioManager.PlaySound("flyRocketExplosion");

            TriggerTrapFailed().Forget();
        }

        RecycleSelf();
    }

    private async UniTaskVoid TriggerTrapFailed()
    {
        await UniTask.WaitForSeconds(2f, cancellationToken: this.GetCancellationTokenOnDestroy());
        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }

    public void OnSpawn()
    {
        gameObject.SetActive(true);
        isActive = true;
        isTriggered = false;
        if (cachedCollider != null)
            cachedCollider.enabled = true;

        if (rotate == null)
            rotate = GetComponent<AutoRotate>();

        if (rotate)
            rotate.SetSpeed(Random.Range(20, 90));

        if (move == null)
            move = GetComponent<DirectMove>();

        move.SetInitSpeed(Vector2.down * Random.Range(MoveSpeed.x, MoveSpeed.y));
    }

    public void OnRecycle()
    {
        isActive = false;
        isTriggered = false;
        actor = null;
        lifeTimer = 0f;
        if (cachedCollider != null)
            cachedCollider.enabled = false;
        gameObject.SetActive(false);
    }

    public void RecycleSelf()
    {
        if (!isActive)
            return;

        isActive = false;
        if (cachedCollider != null)
            cachedCollider.enabled = false;

        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Recycle(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
