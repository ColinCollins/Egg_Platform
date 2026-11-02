using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 被玩家踢
/// </summary>
public class Level25EnemyCtrl : ActorCtrl
{
    [SerializeField] private ParticleSystem knock;
    [SerializeField] private QuickTransformArea transformArea;

    public bool IsDie { get; private set; }

    protected override void Start()
    {
        base.Start();
        IsDie = false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag.Equals("Player") && !IsDie)
        {
            IsDie = true;
            PlayEnmeyDeadAnim(collision).Forget();
        }
    }

    private async UniTaskVoid PlayEnmeyDeadAnim(Collision2D collision)
    {
        Die();
        collision.transform.GetComponent<ActorCtrl>().PlayKick();

        await UniTask.WaitForSeconds(0.1f);


        var actorPos = collision.transform.position;
        if (knock != null)
        {
            knock.transform.position = transform.position;
            knock.Play();
        }


        AudioManager.PlaySound("trap");

        float dir = actorPos.x - transform.position.x < 0 ? 1 : -1;

        Knockback(new Vector2(dir * .5f, .5f), 50f);
        Body.DOLocalRotate(new Vector3(0, 0, 360f), 0.5f, RotateMode.FastBeyond360).SetLoops(-1);

        if (transformArea)
            transformArea.RemoveTarget(transform);

        TryToDestroy();
    }

    protected override void HandleInput()
    {

    }

    private async Task TryToDestroy()
    {
        await UniTask.WaitForSeconds(1.5f);

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        transform.DOKill();
        Body.DOKill();
    }
}
