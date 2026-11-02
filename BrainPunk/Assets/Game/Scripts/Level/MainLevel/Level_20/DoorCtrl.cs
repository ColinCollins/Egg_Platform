using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using UnityEngine;

public class DoorCtrl : MonoBehaviour, IDebuger
{
    private EventSubscriber _subscriber;
    [SerializeField] private DoorEnemyCtrl enemy;
    public DoorEnemyCtrl Actor => enemy;
    [SerializeField] private ActorSpineCtrl actorSpine;

    [SerializeField] private Transform door;

    [Header("按下检测（可选）")]
    [Tooltip("按下检测组件，用于检测点击 enemy enemy")]
    [SerializeField] private PressItemEventHandle pressHandle;


    protected bool isPause = false;

    private bool isPress = false;

    private bool isStandUp = false;

    private bool canMove = false;



    private void OnDisable()
    {
        if (pressHandle != null)
        {
            pressHandle.OnPressEvent.RemoveListener(OnPress);
            pressHandle.OnCancelPressEvent.RemoveListener(OnPressCancel);
        }
    }

    private void SetupPressHandle()
    {
        if (pressHandle == null)
        {
            pressHandle = GetComponent<PressItemEventHandle>();
        }

        if (pressHandle != null && enemy != null)
        {
            pressHandle.SetTargetTag("enemy");
            pressHandle.SetTargetTransform(enemy.transform);
            pressHandle.OnPressEvent.AddListener(OnPress);
            pressHandle.OnCancelPressEvent.AddListener(OnPressCancel);
        }
    }

    /// <summary>
    /// 添加事件监听
    /// </summary>
    public virtual void AddListener()
    {
        this.Log("Door Add Listener");
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        _subscriber.Subscribe<PlayerMoveCancelEvent>(OnPlayerMoveCancel);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);

        _subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        _subscriber.Subscribe<GameResumeEvent>(OnGameResume);
    }

    protected void OnGamePause(GamePauseEvent evt)
    {
        isPause = true;
    }
    protected void OnGameResume(GameResumeEvent evt)
    {
        GameResume();
    }

    private void GameResume()
    {
        isPause = false;
    }

    /// <summary>
    /// 玩家跳跃事件处理
    /// </summary>
    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (enemy != null && !isPress && canMove)
        {
            enemy.TriggerJump();
        }
    }

    void Update()
    {
        if (enemy != null)
        {
            enemy.OnUpdate();
        }
    }

    /// <summary>
    /// 按下 enemy enemy 时触发（由 PressItemEventHandle 调用）
    /// </summary>
    protected virtual void OnPress()
    {
        this.Log("OnPress: Enemy enemy pressed");
    }

    /// <summary>
    /// 松开时取消（由 PressItemEventHandle 调用）
    /// </summary>
    protected virtual void OnPressCancel()
    {
        this.Log("OnPressCancel: Released");
    }

    /// <summary>
    /// 玩家向右移动事件处理
    /// </summary>
    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        if (isPress || !canMove)
            return;

        if (enemy != null)
        {
            enemy.SetMoveInput(1f);

            // this.Log("Right Moving ----------------- ");
        }
    }

    /// <summary>
    /// 玩家向左移动事件处理
    /// </summary>
    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        if (isPress || !canMove)
            return;

        if (enemy != null)
        {
            enemy.SetMoveInput(-1f);

            // this.Log("Left Moving ----------------- ");
        }
    }

    /// <summary>
    /// 取消移动事件处理
    /// </summary>
    private void OnPlayerMoveCancel(PlayerMoveCancelEvent evt)
    {
        StopMove();
    }

    private void StopMove()
    {
        if (enemy != null)
        {
            enemy.SetMoveInput(0f);
            enemy.OnUpdate();

            // enemy.StopMoving();
            // this.LogError("Stop Moving ----------------- ");
        }
    }

    public void OnDoorPress()
    {
        if (!isStandUp)
            return;

        isPress = true;
        StopMove();
        enemy.PressDoor();
        this.LogError("Door Press");
    }

    public void OnDoorCancelPress()
    {
        if (!isStandUp)
            return;

        isPress = false;
        enemy.CancelPress();
    }

    public void StandUp()
    {
        isStandUp = true;
        enemy.StandUp();
        AddListener();
        GameResume();
        SetupPressHandle();
    }

    public void StartMove()
    {
        canMove = true;
    }

    public void SyncRealDoorPosition()
    {
        var pos = door.transform.position;
        pos.x = enemy.transform.position.x;
        door.transform.position = pos + new Vector3(0, .6f, 0);
        var y = enemy.transform.localEulerAngles.y;
        door.transform.localEulerAngles = y > 0 ? Vector3.zero : new Vector3(0, 180, 0);
    }

    void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
    }
}
