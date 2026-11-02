using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;

public class PuppyEnemyCtrl : MonoBehaviour
{
    private bool isDead = false;
    [SerializeField] private ParticleSystem knockDoor;

    [SerializeField] private ActorSpineCtrl spineCtrl;

    /// <summary>
    /// 碰撞到 actor 的时候
    /// </summary>
    /// <param name="collision"></param>
    public void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag.Equals("Player") && !isDead)
        {
            PlayKnock(collision).Forget();
        }
    }

    private async UniTask PlayKnock(Collision2D collision)
    {
        // level 17 不需要，不要加
        if (spineCtrl)
        {
            spineCtrl.PlayAnimation("knockback", false).Complete += (track) =>
           {
               spineCtrl.PlayAnimation("idle", true);
           };
            await UniTask.WaitForSeconds(0.2f);

            // await UniTask.WaitForSeconds(0.2f);
        }

        isDead = true;
        Debug.LogError($"Collision: {collision.gameObject.name}");
        var actor = collision.gameObject.GetComponent<ActorCtrl>();
        actor.Die();
        actor.Knockback(new Vector2(-.5f, .5f), 50f);
        actor.Body.DOLocalRotate(new Vector3(0, 0, 360f), 0.5f, RotateMode.FastBeyond360).SetLoops(-1);

        knockDoor.transform.position = actor.transform.position;
        knockDoor.Play();

        AudioManager.PlaySound("trap");
    }
}
