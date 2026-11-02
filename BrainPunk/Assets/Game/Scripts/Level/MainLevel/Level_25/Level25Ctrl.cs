using System.Threading.Tasks;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.ItemEvent;
using Game.Scripts.Common;
using Sirenix.OdinInspector;
using UnityEngine;

public class Level25Ctrl : MonoBehaviour, IDebuger
{
    [Header("Door Path")]
    [SerializeField] private Transform pathCenterPoint;
    [SerializeField] private Transform leftEndPoint;
    [SerializeField] private Transform rightEndPoint;
    // [SerializeField] private ActorCtrl enemy;
    [SerializeField] private Level25EnemyCtrl enemyCtrl;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private BaseItemExecutor doorExecutor;

    [SerializeField] private ItemFlyPathListener doorFlyItem;
    [SerializeField] private ItemFlyPathListener keyFlyItem;
    [SerializeField] private BaseItemExecutor keyExecutor;
    [SerializeField] private ParticleSystem knock;
    [SerializeField] private ParticleSystem doorSoftLand;

    [Tooltip("中心位置判定")]
    [SerializeField] private float actorCheckRange = 0.5f;
    [Header("Unlock Part")]
    [SerializeField] private BaseItemExecutor lockExecutor;
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private bool showGizmos = true;

    private bool isKeyFlying = false;
    // 需要踢门
    private bool needKick = false;
    // 准备踢门
    private bool isReadyKick = false;
    // 默认左边
    private int lastStayDir = -1;
    // enemyCtrl 准备移动
    private bool enemyReadyMove = false;
    // 默认在左
    private int flyDir = -1;
    private int triggerCount = 0;
    private bool hasStart = false;

    void Start()
    {
        hasStart = false;
        enemyReadyMove = true;

        // enemyCtrl.GetComponent<Rigidbody2D>().simulated = false;
    }
    #region Key
    public void TriggerKey()
    {
        if (isKeyFlying)
            return;

        isKeyFlying = true;
        // hasStart = true;
        Debug.Log("Trigger Key");

        if (enemyCtrl.IsDie)
            AudioManager.PlaySound("getKey");

        flyDir *= -1;
        // key fly
        triggerCount++;

        if (triggerCount > 1)
        {
            pathCenterPoint.transform.position = Vector3.up * .6f;
        }

        keyFlyItem.SetEndTarget(flyDir > 0 ? rightEndPoint : leftEndPoint);
        keyExecutor.Execute();

        // enemyCtrl enter
        needKick = true;

        if (triggerCount == 1 && enemyCtrl != null)
        {
            enemyCtrl.SetMoveInput(-1);
            enemyCtrl.GetComponent<Rigidbody2D>().simulated = true;
        }
    }

    /// <summary>
    /// 钥匙落地
    /// </summary>
    public void KeyLand()
    {
        isKeyFlying = false;
    }

    #endregion


    [Button("MoveEnemy")]
    void TestMoveEnemy()
    {
        flyDir *= -1;
        enemyCtrl.SetMoveInput(flyDir > 0 ? -1 : 1);
    }


    // 记录 enemyCtrl 里 endPoint 距离
    Vector3 dPos;

    void Update()
    {
        if (enemyCtrl != null && !enemyCtrl.IsDie)
        {
            enemyCtrl.OnUpdate();
            if (enemyCtrl.IsMoving)
            {
                // Debug.Log("-------------");
                if (lastStayDir < 0)
                {
                    dPos = (rightEndPoint.position - enemyCtrl.transform.position);
                }
                else
                {
                    dPos = (leftEndPoint.position - enemyCtrl.transform.position);
                }

                dPos.y = 0;

                if (dPos.sqrMagnitude < 2f)
                {
                    isReadyKick = true;
                    enemyCtrl.SetMoveInput(0f);
                    enemyCtrl.UpdateAnimation();
                }
            }

            if (isReadyKick && needKick)
            {
                KickDoor();
            }

            CheckActorPosition();
        }
    }

    private void CheckActorPosition()
    {
        if (!hasStart || enemyCtrl == null || enemyCtrl.IsKicking)
            return;

        float dx = actor.transform.position.x;
        int dir = actor.transform.position.x > 0 ? 1 : -1;
        var posX = Mathf.Abs(dx);
        if (posX > actorCheckRange && lastStayDir != dir && enemyReadyMove)
        {
            lastStayDir = dir;
            enemyCtrl.SetMoveInput(lastStayDir < 0 ? -1 : 1);
            this.Log("---------- trigger move" + lastStayDir);
            enemyReadyMove = false;
        }
        else if (posX < actorCheckRange)
        {
            enemyReadyMove = true;
        }
    }

    public void OnEnemyTriggerDoorFly()
    {
        this.Log("-------------Trigger door" + dPos.sqrMagnitude);
        doorFlyItem.SetEndTarget(flyDir > 0 ? leftEndPoint : rightEndPoint);
        doorExecutor.Execute();
    }

    private async Task KickDoor()
    {
        hasStart = true;
        needKick = false;

        enemyCtrl.PlayKick();
        AudioManager.PlaySound("trap");

        await UniTask.WaitForSeconds(0.2f);

        knock.transform.position = doorCollider.transform.position;
        knock.Play();

        OnEnemyTriggerDoorFly();
    }

    public void PlayDoorSoftLand()
    {
        doorSoftLand.Play();
    }


    #region Unlock Part

    public void UnlockDoor(Transform target)
    {
        if (!enemyCtrl.IsDie)
            return;

        Destroy(target.gameObject);
        lockExecutor.Execute();
        //    lockSpineCtrl.PlayAnimation("unlock", false);

        EnableDoorCollider();
    }

    private async Task EnableDoorCollider()
    {
        await UniTask.WaitForSeconds(1);

        doorCollider.enabled = true;
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (!showGizmos || actorCheckRange <= 0f) return;

        Vector3 center = transform.position;
        float h = actorCheckRange;
        Vector3 p0 = center + new Vector3(-h, -h, 0f);
        Vector3 p1 = center + new Vector3(h, -h, 0f);
        Vector3 p2 = center + new Vector3(h, h, 0f);
        Vector3 p3 = center + new Vector3(-h, h, 0f);

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);
    }
}
