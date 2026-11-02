using System;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;

public class Level50Ctrl : MonoBehaviour
{
    private const int FlySuccessEventId = 2;
    private const int FlyFailEventId = 3;
    private static readonly int UvParamsPropertyId = Shader.PropertyToID("_UVParams");

    [SerializeField] private GameObject doorEffect;
    [SerializeField] private GameObject doorSpine;
    [SerializeField] private Level50ActorCtrl actor;
    [SerializeField] private ActorSpineCtrl actorSpineCtrl;
    [SerializeField] private ParticleSystem floorExplosion;
    [SerializeField] private CameraFollowArea camerFollow;

    // flyingGame anima target
    [SerializeField] private Transform moon;
    [SerializeField] private Transform star;

    [SerializeField] private Material lineMaskMat;
    [SerializeField] private float lineMaskScrollSpeed = 0.5f;

    [SerializeField] private Level50FlyingGameCtrl flyingGame;
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;

    [Header("Movement Limit")]
    [Tooltip("Actor 左右移动范围限制，X=最小值，Y=最大值")]
    [SerializeField] private Vector2 limitX = new Vector2(-15f, 15f);

    private Level50GamePanelCtrl panelCtrl;
    private EventSubscriber subscriber;
    private Vector4 lineMaskBaseUvParams;

    private void Start()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<OnTiggerItemEvent>(OnTriggerItem);

        CacheLineMaskUvParams();

        if (PlayCtrl.Instance?.CurrentGamePlayPanel == null)
        {
            return;
        }

        panelCtrl = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<Level50GamePanelCtrl>();

        flyingGame.OnSuccess += OnFlyGameSuccess;
    }

    #region Step2 飞行躲避游戏结束
    private void OnFlyGameSuccess()
    {
        flyingGame.StopPlay();

        actor.RB.linearVelocityX = 0;
        actor.GetComponent<Collider2D>().enabled = false;

        panelCtrl.SwitchMoveBtns(false);
        actor.transform.DOMove(new Vector2(5f, 203f), 2f).SetEase(Ease.InQuart).OnComplete(WaitingFlyGamePassLevel);
    }

    // 播放动画，通过关卡.actor doJump to moon, 同时缩放scale = 0， 然后播放完成后，发送游戏胜利事件
    private void WaitingFlyGamePassLevel()
    {
        if (baseLevelCtrl == null)
        {
            baseLevelCtrl = GetComponentInParent<BaseLevelCtrl>() ?? GetComponent<BaseLevelCtrl>();
        }

        if (moon == null)
        {
            baseLevelCtrl?.SuccessStrightly();
            return;
        }

        PlayFinishedAnim().Forget();
    }

    private async UniTask PlayFinishedAnim()
    {

        // 获取 moon 的世界坐标，保持与 actor 相同的 z
        Vector3 targetPos = moon.position;
        targetPos.z = actor.transform.position.z;

        const float jumpDuration = 1.2f;

        actor.SetFaceDir(true);
        actor.StopRocket();

        await UniTask.WaitForSeconds(2f);

        camerFollow.enabled = false;
        actor.StartRocketFly();

        // 跳跃到 moon
        actor.transform.DOJump(targetPos, jumpPower: 4f, numJumps: 1, duration: jumpDuration)
            .SetEase(Ease.OutQuad);

        // 同时缩放到 0
        actor.transform.DOScale(Vector3.zero, jumpDuration)
            .SetEase(Ease.InQuad);

        // 同时绕 z 轴旋转 -120°
        actor.transform.DOLocalRotate(new Vector3(0, 0, -135f), jumpDuration)
            .SetEase(Ease.OutQuad);

        await UniTask.WaitForSeconds(jumpDuration);

        star.DOScale(Vector3.one * 0.5f, 0.3f).SetLoops(2, loopType: LoopType.Yoyo);
        
        AudioManager.PlaySound("farStar");
        await UniTask.WaitForSeconds(1);

        baseLevelCtrl?.SuccessStrightly();
    }

    #endregion

    #region 直接通关
    public void ShowDoor()
    {
        baseLevelCtrl.SetSuccessAnim(doorEffect.transform.parent.GetComponent<SuccessAnimCtrl>());
        doorSpine.SetActive(true);
        doorEffect.SetActive(true);
        // panelCtrl?.SwitchToFly();
    }

    #endregion


    #region Step1 准备飞行
    public void StartFly()
    {
        panelCtrl.SwitchToFly();
        actor.SwitchRocket(true);
        // actor.PlayRocket();
    }

    private void OnTriggerItem(OnTiggerItemEvent @event)
    {
        switch (@event.EventId)
        {
            case FlySuccessEventId:
                OnFlySuccessTriggered();
                break;
            case FlyFailEventId:
                OnFlyFailTriggered();
                break;
        }
    }

    private void OnFlySuccessTriggered()
    {
        actor.StartRocketFly();
        actor.SetMoveSpeed(7f);
        actor.RB.bodyType = RigidbodyType2D.Kinematic;
        actor.RB.linearVelocity = Vector2.zero;
        actor.transform.DOMoveY(150f, 5f).SetEase(Ease.InQuart).OnComplete(FlyingStable);
        camerFollow.enabled = true;

        AudioManager.PlaySound("rocket", loop: true);
    }

    private void FlyingStable()
    {
        panelCtrl.SwitchToFlyModelButtons();

        if (flyingGame)
        {
            flyingGame.StartPlay();
        }

        panelCtrl.StartGoal(flyingGame == null ? 600 : flyingGame.Duration);
    }

    private void OnFlyFailTriggered()
    {
        PlayKnock().Forget();
    }

    private async UniTask PlayKnock()
    {
        actor.Die();
        actor.Knockback(new Vector2(-.5f, .7f), 50f).Forget();
        actor.Body.DOLocalRotate(new Vector3(0, 0, 360f), 0.5f, RotateMode.FastBeyond360).SetLoops(-1);
        actor.SwitchRocket(false);

        floorExplosion.transform.position = actor.transform.position;
        floorExplosion.Play();

        AudioManager.PlaySound("rocketExplosion");
    }

    private void Update()
    {
        UpdateLineMaskUvParams();
        ClampActorPosition();
    }

    private void ClampActorPosition()
    {
        if (actor == null)
            return;

        Vector3 pos = actor.transform.position;
        pos.x = Mathf.Clamp(pos.x, limitX.x, limitX.y);
        actor.transform.position = pos;
    }


    private void CacheLineMaskUvParams()
    {
        if (lineMaskMat == null || !lineMaskMat.HasProperty(UvParamsPropertyId))
        {
            return;
        }

        lineMaskBaseUvParams = lineMaskMat.GetVector(UvParamsPropertyId);
    }

    private void UpdateLineMaskUvParams()
    {
        if (lineMaskMat == null || !lineMaskMat.HasProperty(UvParamsPropertyId))
        {
            return;
        }

        float time = Time.time;
        Vector4 uvParams = lineMaskBaseUvParams;
        uvParams.w = Mathf.Repeat(lineMaskBaseUvParams.w + time * lineMaskScrollSpeed, 5f);
        lineMaskMat.SetVector(UvParamsPropertyId, uvParams);
    }

    #endregion

    private void OnDestroy()
    {
        if (lineMaskMat != null && lineMaskMat.HasProperty(UvParamsPropertyId))
        {
            lineMaskMat.SetVector(UvParamsPropertyId, lineMaskBaseUvParams);
        }

        EventsUtils.ResetEvents(ref subscriber);
    }
}
