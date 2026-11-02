using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 敌人进入，玩家移动，触发 trigger，敌人朝向左边移动，跟随跳跃。触碰玩家，玩家死亡。等待播放 reset
/// </summary>
public class Level17Ctrl : MonoBehaviour, IDebuger
{
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private DogEnemyCtrl enemy;
    [SerializeField] private ActorSpineCtrl startDoor;

    [SerializeField] private Transform BarkDistance;

    [SerializeField] private Transform TrackDistance;

    [Header("Waiting Bark SFX")]
    [SerializeField] private string barkSfxTag = "bark";
    [SerializeField] private float barkInterval = 2f;

    private bool canEnterDoor = false;
    private float nextBarkTime;
    private enum EnemyState
    {
        idle,
        enter,
        waiting,
        tracking
    };

    private EnemyState state = EnemyState.idle;

    private ActorDestroyAnimHandle animHandle;

    private EventSubscriber _subscriber;

    /// <summary>
    /// 添加事件监听
    /// </summary>
    public virtual void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);

        _subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        _subscriber.Subscribe<GameResumeEvent>(OnGameResume);
    }

    private void OnGameResume(GameResumeEvent @event)
    {
        enemy.SetMoveInput(-1);
    }


    private void OnGamePause(GamePauseEvent @event)
    {
        enemy.SetMoveInput(0);
        this.Log("---------------");
    }


    private void OnPlayerJump(PlayerJumpEvent @event)
    {
        enemy.TriggerJump();
    }

    void Awake()
    {
        // StartAnim1();
        animHandle = GetComponent<ActorDestroyAnimHandle>();
    }

    public void ShowEnemy(Collider2D collider)
    {
        StartAnim1().Forget();
    }

    /// <summary>
    /// 开场动画 1，角色从右边移动到左边的某个位置
    /// </summary>
    public async UniTaskVoid StartAnim1()
    {
        enemy.SetMoveInput(-1);
        await UniTask.WaitForSeconds(.8f);
        enemy.SetMoveInput(0);
        state = EnemyState.enter;
    }

    void Update()
    {
        if (enemy != null)
        {
            enemy.OnUpdate();

            if (state == EnemyState.enter)
                TriggerBark();

            if (state == EnemyState.waiting)
            {
                TriggerEnemy();
                TryPlayWaitingBark();
            }
        }
    }

    /// <summary>
    /// waiting 状态下按间隔播放 bark 音效。
    /// </summary>
    private void TryPlayWaitingBark()
    {
        if (string.IsNullOrEmpty(barkSfxTag) || barkInterval <= 0f)
            return;
        if (Time.time < nextBarkTime)
            return;
        AudioManager.PlaySound(barkSfxTag, randomPitch: true);
        nextBarkTime = Time.time + barkInterval;
    }

    /// <summary>
    /// 点击开始门之后，切换状态
    /// </summary>
    public void OnStartDoorClick()
    {
        this.Log("Open the door!");
        if (canEnterDoor)
            return;

        canEnterDoor = true;
        startDoor.PlayAnimation("door_open", false);
    }

    public void TriggerBark()
    {
        if (actor.transform.localPosition.x > BarkDistance.transform.localPosition.x)
        {
            enemy.Bark();
            state = EnemyState.waiting;
            nextBarkTime = Time.time + barkInterval;
        }
        else
        {
            enemy.StopBark();
            state = EnemyState.enter;
        }
    }

    // 进入 collision = enemy
    public void TriggerEnemy()
    {
        if (actor.transform.localPosition.x > TrackDistance.transform.localPosition.x)
        {
            enemy.StopBark();
            AddListener();
            enemy.SetMoveInput(-1);
            state = EnemyState.tracking;

            actor.SwitchDash(true);
        }
    }


    private Tweener _tween;
    // 进入 collision = enemy
    public void OnTriggerEnemyEnterDoor(Collider2D collision)
    {
        this.Log("OnTriggerEnemyEnterDoor ---------" + collision.name);
        if (enemy == null || !canEnterDoor || !collision.gameObject.tag.Equals("enemy"))
            return;

        enemy.SetMoveInput(0);
        enemy.GetComponent<Collider2D>().enabled = false;
        animHandle.PlayDestroy();
        actor.SwitchDash(false);

        AudioManager.PlaySound("enterDoor");
        enemy = null;
    }

    void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);

        if (_tween != null)
            _tween.Kill();
    }
}
