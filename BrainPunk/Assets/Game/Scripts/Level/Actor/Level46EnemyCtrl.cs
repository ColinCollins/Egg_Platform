using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;


/// <summary>
/// 冲向 Actor，并将 Actor 撞飞
/// </summary>
public class Level46EnemyCtrl : ActorCtrl
{
    [SerializeField] private ParticleSystem knock;

    public bool IsDie { get; private set; }

    protected override void Start()
    {
        base.Start();
        IsDie = false;
    }

    /// <summary>
    /// 准备触发移动
    /// </summary>
    public void ReadyToMove()
    {
        GetComponent<Collider2D>().enabled = true;
        GetComponent<Rigidbody2D>().simulated = true;
        SetMoveInput(-1);
        SwitchDash(true);
    }

    private void OnTriggerEnter2D(Collider2D other)

    {
        if (other.gameObject.tag.Equals("Player") && !IsDie)
        {
            IsDie = true;
            PlayActorDeadAnim(other).Forget();
        }
    }

    private async UniTaskVoid PlayActorDeadAnim(Collider2D collision)
    {
        // PlayKick();
        var actorCtrl = collision.transform.GetComponent<ActorCtrl>();
        actorCtrl.StopMoving();

        await UniTask.WaitForSeconds(0.1f);


        var actorPos = collision.transform.position;
        if (knock != null)
        {
            knock.transform.position = transform.position;
            knock.Play();
        }

        AudioManager.PlaySound("trap");

        float dir = -1;
        actorCtrl.Knockback(new Vector2(dir * .5f, .5f), 50f);
        actorCtrl.transform.DOLocalRotate(new Vector3(0, 0, 360f), 0.5f, RotateMode.FastBeyond360).SetLoops(-1);

        // SetMoveInput(-1);
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
