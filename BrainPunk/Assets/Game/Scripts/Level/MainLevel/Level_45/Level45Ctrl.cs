using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ItemEvent;
using Game.Scripts.Common;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 
/// </summary>
public class Level45Ctrl : MonoBehaviour
{
    [SerializeField] private DirectMove keyDirect;
    [SerializeField] private Level45ActorCtrl actorCtrl;

    [SerializeField] private ActorRandomSpineCtrl actorRandomSpineCtrl;

    [SerializeField] private SitableIStuffCtrl sitable;
    [SerializeField] private Animator tvAnimator;

    // bar 功能，在 sit 状态下， process 增长，增长速度可配置。对应两个 percent 节点，超过节点切换 emjObjs. index 默认为 0
    [SerializeField] private Transform barTrans;
    [SerializeField] private Image barProcess;
    [SerializeField, Min(0f)] private float barIncreaseSpeed = 0.2f;
    [SerializeField, Min(0f)] private float barShowScaleDuration = 0.15f;
    [SerializeField, Range(0.01f, 1f)] private float barShowFromScale = 0.5f;
    [SerializeField] private float[] emjPercents = { 0.33f, 0.66f };
    [SerializeField] private GameObject[] emjObjs;

    [SerializeField] private ParticleSystem wallCrash;
    [SerializeField] private MMF_Player shakePlayer;

    [Header("Basketball")]
    [SerializeField] private LayerCollisionDetector basketball;
    [SerializeField] private Vector2 throwForceMin = new Vector2(10f, 8f);
    [SerializeField] private Vector2 throwForceMax = new Vector2(14f, 12f);
    [SerializeField] private float throwOffsetX = 1.2f;
    [SerializeField] private float throwOffsetY = 0.5f;
    [SerializeField] private float preThrowDelay = 0.2f;
    [SerializeField] private float postThrowDelay = 0.5f;
    [SerializeField] private float basketballBarIncrement = 0.2f;
    [SerializeField] private float barTweenDuration = 0.3f;


    [SerializeField] private Transform sofa;
    [SerializeField] private float randomSpineInterval = 3f;

    // 用于切换wall crash 贴图表现
    [SerializeField] private SpriteRenderer crashRender;
    [SerializeField] private GameObject[] crashSprits;
    [SerializeField] private ClickItemEventHandle clickWallEvent;

    private bool isSit = false;
    private float barPercent = 0f;
    private int currentEmjIndex = -1;
    private int crashSpriteIndex = 0;
    private bool isClickWallStopped = false;
    private Vector3 barOriginScale = Vector3.one;
    private float _randomSpineTimer;
    private bool _isRandomPlaying;
    private bool _isBarTweening;
    private Tween _barTween;

    private GamePanel_UseBtnCommonFeature commonFeature;

    private void Awake()
    {
        if (actorRandomSpineCtrl == null)
        {
            actorRandomSpineCtrl = GetComponentInChildren<ActorRandomSpineCtrl>(true);
        }

        if (actorRandomSpineCtrl != null)
        {
            actorRandomSpineCtrl.RandomCycleCompleted -= OnRandomSpineCycleCompleted;
            actorRandomSpineCtrl.RandomCycleCompleted += OnRandomSpineCycleCompleted;
        }
    }

    void Start()
    {
        commonFeature = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<GamePanel_UseBtnCommonFeature>();
        commonFeature.Use_btn.OnClick += SitOnSofa;

        InitBar();
    }

    void Update()
    {
        UpdateBar();
        UpdateRandomSpine();
    }

    private void UpdateRandomSpine()
    {
        if (!isSit || _isRandomPlaying || actorRandomSpineCtrl == null || randomSpineInterval <= 0f)
            return;

        _randomSpineTimer += Time.deltaTime;
        if (_randomSpineTimer >= randomSpineInterval)
        {
            _randomSpineTimer = 0f;

            _isRandomPlaying = actorRandomSpineCtrl.PlayRandom();
        }
    }

    private void OnRandomSpineCycleCompleted()
    {
        if (!isSit)
            return;

        _randomSpineTimer = 0f;
        _isRandomPlaying = false;
    }

    private MelenitasDev.SoundsGood.Sound tv;
    private void SitOnSofa(CustomButton btn)
    {
        if (!isSit)
        {
            isSit = true;
            actorCtrl.PlaySit(sofa.transform, new Vector3(0.3f, -.15f, 0f));
            sitable.SitIn();

            tv = AudioManager.PlaySound("TV", loop: true);
        }
        else
        {
            isSit = false;
            actorCtrl.StopSit();
            sitable.SitOut();
            tv.Stop();
        }

        _randomSpineTimer = 0f;
        _isRandomPlaying = false;
        tvAnimator.SetBool("playing", isSit);
        SetBarVisible(isSit);
    }

    private void InitBar()
    {
        if (barTrans != null)
            barOriginScale = barTrans.localScale;

        barPercent = 0.1f;
        SetBarProcess(barPercent);
        SetBarVisible(false);
    }

    private void UpdateBar()
    {
        if (!isSit || barProcess == null || barPercent >= 1f || _isBarTweening)
            return;

        barPercent = Mathf.Clamp(barPercent + barIncreaseSpeed * Time.deltaTime, 0, 0.9f);
        SetBarProcess(barPercent);
    }

    private void AddBarProcess(float percent)
    {
        if (barProcess == null || barTweenDuration <= 0f)
        {
            barPercent = Mathf.Clamp01(barPercent + percent);
            SetBarProcess(barPercent);
            return;
        }

        float target = Mathf.Clamp01(barPercent + percent);
        if (Mathf.Approximately(target, barPercent))
            return;

        _barTween?.Kill();
        _isBarTweening = true;

        _barTween = DOTween.To(
            () => barPercent,
            v =>
            {
                barPercent = v;
                if (barProcess != null)
                    barProcess.fillAmount = v;
                UpdateEmjState(v);
            },
            target,
            barTweenDuration
        ).SetEase(Ease.OutQuad).OnComplete(() => _isBarTweening = false);
    }

    private void SetBarProcess(float percent)
    {
        if (barProcess != null)
            barProcess.fillAmount = percent;

        UpdateEmjState(percent);
    }

    private void SetBarVisible(bool isVisible)
    {
        if (barTrans != null)
        {
            barTrans.DOKill();

            if (isVisible && barTrans.gameObject.activeSelf)
                return;
                
            if (isVisible)
            {
                barTrans.gameObject.SetActive(true);
                barTrans.localScale = barOriginScale * barShowFromScale;
                barTrans.DOScale(barOriginScale, barShowScaleDuration).SetEase(Ease.OutBack);
            }
            else
            {
                barTrans.localScale = barOriginScale;
                barTrans.gameObject.SetActive(false);
            }
        }

        if (isVisible)
            UpdateEmjState(barPercent, true);
        else
            HideAllEmj();
    }

    private void UpdateEmjState(float percent, bool force = false)
    {
        if (emjObjs == null || emjObjs.Length == 0)
            return;

        int emjIndex = GetEmjIndex(percent);
        if (!force && currentEmjIndex == emjIndex)
            return;

        currentEmjIndex = emjIndex;
        for (int i = 0; i < emjObjs.Length; i++)
        {
            if (emjObjs[i] != null)
                emjObjs[i].SetActive(i == emjIndex);
        }
    }

    private int GetEmjIndex(float percent)
    {
        int emjIndex = 0;
        if (emjPercents != null)
        {
            for (int i = 0; i < emjPercents.Length; i++)
            {
                if (percent >= emjPercents[i])
                    emjIndex = i + 1;
            }
        }

        return Mathf.Clamp(emjIndex, 0, emjObjs.Length - 1);
    }

    private void HideAllEmj()
    {
        currentEmjIndex = -1;
        if (emjObjs == null)
            return;

        for (int i = 0; i < emjObjs.Length; i++)
        {
            if (emjObjs[i] != null)
                emjObjs[i].SetActive(false);
        }
    }

    public void ClickWall()
    {
        if (isClickWallStopped || crashSprits == null || crashSprits.Length == 0)
            return;

        SetCrashState(crashSpriteIndex);

        if (crashSpriteIndex >= crashSprits.Length - 1)
        {
            StopClickWall();
            AudioManager.PlaySound("crashWall", randomPitch: true);
            return;
        }

        if (wallCrash != null)
        {
            wallCrash.transform.position = GetCrashEffectPosition();
            wallCrash.Play();

            shakePlayer.PlayFeedbacks();
        }

        crashSpriteIndex++;

        AudioManager.PlaySound("crashWall");
    }

    private void SetCrashState(int activeIndex)
    {
        for (int i = 0; i < crashSprits.Length; i++)
        {
            if (crashSprits[i] != null)
                crashSprits[i].SetActive(i == activeIndex);
        }
    }

    private Vector3 GetCrashEffectPosition()
    {
        if (crashSpriteIndex >= 0 && crashSpriteIndex < crashSprits.Length && crashSprits[crashSpriteIndex] != null)
            return crashSprits[crashSpriteIndex].transform.position;

        if (crashRender != null)
            return crashRender.transform.position;

        return wallCrash.transform.position;
    }

    private void StopClickWall()
    {
        isClickWallStopped = true;

        if (clickWallEvent != null)
            clickWallEvent.SetEnableClick(false);

        if (keyDirect != null)
            keyDirect.gameObject.SetActive(true);
    }


    public void StopKey()
    {
        keyDirect.enabled = false;
        keyDirect.GetComponent<MoveFloatHandle>().enabled = true;
    }

    private Rigidbody2D _basketballRb;
    private Collider2D _basketballCollider;
    private bool _isPlayingBasketball;

    private void EnsureBasketballComponents()
    {
        if (_basketballRb != null && _basketballCollider != null)
            return;

        if (basketball != null)
        {
            _basketballRb = basketball.GetComponent<Rigidbody2D>();
            _basketballCollider = basketball.GetComponent<Collider2D>();
        }
    }

    public void WaitingPlayBasketball()
    {
        if (_isPlayingBasketball || actorCtrl.IsSit)
            return;

        _isPlayingBasketball = true;
        PlayBasketball().Forget();
    }

    private async UniTask PlayBasketball()
    {
        EnsureBasketballComponents();

        if (_basketballRb == null || _basketballCollider == null || basketball == null || actorCtrl == null)
        {
            _isPlayingBasketball = false;
            return;
        }

        actorCtrl.StopMoving();
        actorCtrl.PlayLift();
        SetBarVisible(true);

        var offsetDir = actorCtrl.FaingRight ? 1f : -1f;

        // 1. 准备阶段：重置球状态并摆到投掷起点
        PrepareBasketball(offsetDir);

        await UniTask.WaitForSeconds(preThrowDelay);

        // 2. 播放投掷动画，同时禁用碰撞检测组件避免干扰
        basketball.enabled = false;
        await actorCtrl.PlayThrow();

        // 3. 投掷阶段：恢复物理并施加冲量
        LaunchBasketball(offsetDir);

        await UniTask.WaitForSeconds(postThrowDelay);

        // 4. 恢复阶段：重新启用碰撞检测，增加 bar 进度
        basketball.enabled = true;
        AddBarProcess(basketballBarIncrement);
        _isPlayingBasketball = false;

        //  SetBarVisible(false);
         actorCtrl.ResumeMoving();
    }

    /// <summary>
    /// 重置球到投掷起点，设为 Kinematic 避免受外力影响。
    /// </summary>
    private void PrepareBasketball(float direction)
    {
        _basketballRb.linearVelocity = Vector2.zero;
        _basketballRb.angularVelocity = 0f;
        _basketballRb.bodyType = RigidbodyType2D.Kinematic;
        _basketballCollider.isTrigger = true;

        // 把球放到 actor 前方稍高处，避免卡在碰撞体内
        basketball.transform.position = actorCtrl.transform.position
            + new Vector3(direction * throwOffsetX, throwOffsetY, 0f);
    }

    /// <summary>
    /// 恢复 Dynamic 物理并施加抛射冲量。
    /// </summary>
    private void LaunchBasketball(float direction)
    {
        _basketballRb.bodyType = RigidbodyType2D.Dynamic;
        _basketballCollider.isTrigger = false;

        float forceX = UnityEngine.Random.Range(throwForceMin.x, throwForceMax.x);
        float forceY = UnityEngine.Random.Range(throwForceMin.y, throwForceMax.y);
        Vector2 force = new Vector2(forceX * direction, forceY);
        _basketballRb.AddForce(force, ForceMode2D.Impulse);
    }

    void OnDestroy()
    {
        if (commonFeature != null && commonFeature.Use_btn != null)
            commonFeature.Use_btn.OnClick -= SitOnSofa;

        if (actorRandomSpineCtrl != null)
            actorRandomSpineCtrl.RandomCycleCompleted -= OnRandomSpineCycleCompleted;

        if (barTrans != null)
            barTrans.DOKill();

        _barTween?.Kill();

        if (tv != null)
            tv.Stop();

        if (tvAnimator != null)
            tvAnimator.SetBool("playing", false);
    }
}
