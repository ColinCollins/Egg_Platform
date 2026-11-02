using Game.ItemEvent;
using Game.Scripts.Common;
using UnityEngine;

public class Level31EnemyCtrl : MonoBehaviour
{
    private ActorCtrl enemyCtrl;
    [SerializeField] private Transform diamond;
    [SerializeField] private PingPongMoveHandle moveHandle;
    [SerializeField] private GameObject throwHandle;
    [SerializeField] private ParticleSystem knock;

    [SerializeField] private ItemJumpToListener jumpToPath;

    private bool isFinished = false;
    private bool hasDie = false;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isFinished)
            return;

        if (collision.gameObject.tag.Equals("Player") && !hasDie)
        {
            enemyCtrl = GetComponent<ActorCtrl>();

            enemyCtrl.Die();

            var pos = transform.position;
            pos.x += Random.Range(-8f, 8f);
            pos.y = -2.2f;
            pos.z = 0;
            jumpToPath.SetEndPosition(pos);
            jumpToPath.Execute();
            hasDie = true;
            // enemyCtrl.Knockback(new Vector2(-.5f, .5f), 15f);

            throwHandle.SetActive(false);
            moveHandle.StopMove();
            
            enemyCtrl.GetComponent<Collider2D>().excludeLayers = LayerMask.GetMask("Actor");
            // collision.transform.GetComponent<ActorCtrl>().PlayKick();
            knock.transform.position = enemyCtrl.transform.position;
            knock.Play();

            AudioManager.PlaySound("trap");
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            diamond.transform.position = transform.position;
            diamond.gameObject.SetActive(true);
            diamond.GetComponent<FollowTarget>().enabled = false;
            diamond.GetComponent<ItemJumpToListener>().Execute();
            diamond.GetComponent<Collider2D>().enabled = true;
            enemyCtrl.SpineCtrl.SetSkin("box_rich01");
            enemyCtrl.StopMoving();
            isFinished = true;
        }
    }
}
