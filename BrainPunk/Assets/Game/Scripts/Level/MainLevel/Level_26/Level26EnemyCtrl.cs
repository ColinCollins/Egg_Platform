using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Level26EnemyCtrl : MonoBehaviour
{
    [SerializeField] private ParticleSystem knockDoor;

    [SerializeField] private Image LifeBar;

    [SerializeField] private ActorSpineCtrl spineCtrl;

    private bool isOver = false;

    /// <summary>
    /// 碰撞到 actor 的时候
    /// </summary>
    /// <param name="collision"></param>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        bool isTool = collision.gameObject.tag.Equals("tools");
        bool isPlayer = collision.gameObject.tag.Equals("Player");

        if (isPlayer || isTool)
        {
            // Debug.LogError($"Collision: {collision.gameObject.name}");
            PlayKnock(collision, isTool).Forget();
        }
    }

    async void TryToDestroyTool(ActorCtrl actorCtrl)
    {
        await UniTask.WaitForSeconds(1f, cancellationToken: this.GetCancellationTokenOnDestroy());

        if (actorCtrl != null)
        {
            Destroy(actorCtrl.gameObject);
        }
    }

    private async UniTask PlayKnock(Collider2D collision, bool isTool)
    {
        if (spineCtrl)
        {
            spineCtrl.PlayAnimation("knockback", false).Complete += (track) =>
            {
                spineCtrl.PlayAnimation("idle", true);
            };
            await UniTask.WaitForSeconds(0.2f);
        }

        var actor = collision.gameObject.GetComponent<ActorCtrl>();
        actor.Die();
        actor.Knockback(new Vector2(-.5f, .5f), 50f);
        actor.Body.DOLocalRotate(new Vector3(0, 0, 360f), 0.5f, RotateMode.FastBeyond360).SetLoops(-1);

        knockDoor.transform.position = actor.transform.position;
        knockDoor.Play();

        AudioManager.PlaySound("trap");

        if (isTool)
        {
            TryToDestroyTool(actor);
        }

        if (isOver)
            return;

        LifeBar.transform.parent.gameObject.SetActive(true);
        LifeBar.fillAmount = Mathf.Max(LifeBar.fillAmount * 0.9f, 0.1f);
    }

    public void HideLifeBar()
    {
        isOver = true;
        LifeBar.transform.parent.gameObject.SetActive(false);
    }
}
