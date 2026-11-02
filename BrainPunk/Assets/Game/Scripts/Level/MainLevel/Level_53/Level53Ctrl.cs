using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Level;
using Game.Scripts.Common;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class Level53Ctrl : MonoBehaviour
{
    [Header("Announce")]
    [SerializeField] private Transform announce;
    [SerializeField] private float announceMoveInDuration = 1f;
    [SerializeField] private float announceStayDuration = 1f;
    [SerializeField] private float announceMoveOutDuration = 1f;
    [SerializeField] private float announceStopX = 0f;
    [SerializeField] private float announceFlyUpSpeed = 0.5f;
    [SerializeField] private TextMeshPro tmpTxt;

    [SerializeField] private SpringJoint2D springJointLeft;

    [SerializeField] private SpringJoint2D springJointRight;

    [SerializeField] private ActorCtrl actor;

    [SerializeField] private BaseLevelCtrl levelCtrl;

    private Sequence _announceSequence;
    private Vector3 _announceStartLocalPosition;
    private bool _hasAnnounceStartPosition;

    void Start()
    {
        if (announce != null)
        {
            _announceStartLocalPosition = announce.localPosition;
            _hasAnnounceStartPosition = true;
            PlayAnnounce();

            tmpTxt.text = string.Format(tmpTxt.text, PlayCtrl.Instance.Level.CurrentLevel.ToString());
        }

        Time.fixedDeltaTime = 1f / 120f;
    }

    public void StopMoving()
    {
        if (_announceSequence != null)
        {
            _announceSequence.Kill();
            _announceSequence = null;
        }
    }

    private bool isFlying = false;
    public void CloudTryToFly()
    {
        SetAnnounceFlying(true);
    }

    public void SetAnnounceFlying(bool flying)
    {
        isFlying = flying;
        if (isFlying)
        {
            StopMoving();
        }
    }

    private void Update()
    {
        if (!isFlying || announce == null)
        {
            return;
        }

        announce.localPosition += Vector3.up * (announceFlyUpSpeed * Time.deltaTime);
    }

    private void PlayAnnounce()
    {
        if (announce == null || !_hasAnnounceStartPosition)
        {
            return;
        }

        StopMoving();
        announce.localPosition = _announceStartLocalPosition;

        Vector3 stopPosition = new Vector3(announceStopX, _announceStartLocalPosition.y, _announceStartLocalPosition.z);
        Vector3 exitPosition = new Vector3(Mathf.Abs(_announceStartLocalPosition.x), _announceStartLocalPosition.y, _announceStartLocalPosition.z);

        _announceSequence = DOTween.Sequence().SetLink(gameObject);
        _announceSequence.Append(announce.DOLocalMove(stopPosition, announceMoveInDuration).SetEase(Ease.Linear));

        if (announceStayDuration > 0f)
        {
            _announceSequence.AppendInterval(announceStayDuration);
        }

        _announceSequence.Append(announce.DOLocalMove(exitPosition, announceMoveOutDuration).SetEase(Ease.Linear));
        _announceSequence.OnKill(() => _announceSequence = null);
    }

    private void OnDestroy()
    {
        StopMoving();

        Time.fixedDeltaTime = 1f / 60f;
    }

    public void TriggerSpringRight(Collider2D collider)
    {
        PlaySpringRight();
    }

    private async void PlaySpringRight()
    {
        AudioManager.PlaySound("boardTrigger");
        var startY = springJointRight.transform.localPosition.y;
        springJointRight.transform.DOLocalMoveY(startY - 0.2f, 0.1f);

        await UniTask.WaitForSeconds(.2f);

        levelCtrl.SetCtrlable(false);
        springJointRight.enabled = true;
        AudioManager.PlaySound("spring");
        actor.RB.linearVelocity = Vector2.zero;
        actor.Knockback(new Vector2(0.08f, 0.6f), 70f).Forget();
        actor.GetComponent<Rigidbody2D>().gravityScale = 2.5f;

        await UniTask.WaitForSeconds(.6f);

        if (!levelCtrl.ActorCtrlable)
        {
            actor.transform.localEulerAngles = Vector3.zero;
            actor.transform.DORotate(new Vector3(0, 0, 360f), 3f, RotateMode.FastBeyond360);
        }
    }


    private bool hasLeft = false;
    public void TriggerSpringLeft(Collider2D collider)
    {
        if (hasLeft)
            return;
            
        hasLeft = true;
        PlaySpringLeft();
    }

    private async void PlaySpringLeft()
    {
        AudioManager.PlaySound("boardTrigger");
        var startY = springJointLeft.transform.localPosition.y;
        springJointLeft.transform.DOLocalMoveY(startY - 0.2f, 0.1f);

        await UniTask.WaitForSeconds(.2f);

        levelCtrl.SetCtrlable(false);
        springJointLeft.enabled = true;
        AudioManager.PlaySound("spring");
        actor.RB.linearVelocity = Vector2.zero;
        actor.Knockback(new Vector2(0.3f, 0.6f), 20f).Forget();
        actor.GetComponent<Rigidbody2D>().gravityScale = 1.5f;

        await UniTask.WaitForSeconds(.6f);

        if (!levelCtrl.ActorCtrlable)
        {
            actor.transform.localEulerAngles = Vector3.zero;
            actor.transform.DORotate(new Vector3(0, 0, 360f), 2.5f, RotateMode.FastBeyond360).OnComplete(() =>
            {
                levelCtrl.SetCtrlable(true);
                actor.GetComponent<Rigidbody2D>().gravityScale = 4.5f;
            });
        }
    }
}
