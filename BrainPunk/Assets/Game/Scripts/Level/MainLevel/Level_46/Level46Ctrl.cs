using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using Game.Scripts.Effects;
using Sirenix.OdinInspector;
using UnityEngine;

public class Level46Ctrl : MonoBehaviour, IEventSender, IDebuger
{

    [SerializeField] Collider2D levelDoor;
    [SerializeField] Animator scrolls;
    [SerializeField] Level46EnemyCtrl enemy;

    [Header("擦除效果")]
    [SerializeField] private SceneSpriteBrushClear brushClear;
    [SerializeField] private Camera levelCamera;
    [Tooltip("擦除进度达到此值时触发完成"), Range(0f, 1f)]
    [SerializeField] private float clearProgressThreshold = 0.5f;
    [Tooltip("进度检测间隔（秒），避免每帧读取 RenderTexture"), MinValue(0.01f)]
    [SerializeField] private float progressCheckInterval = 0.3f;

    private float _lastProgressCheckTime;
    private bool _clearCompleted;
    private Vector2 _lastEraseUV = new Vector2(-1f, -1f);
    [SerializeField] private float eraseMoveThreshold = 0.005f;
    [Tooltip("从按下起点移动超过该距离后才开始擦除（UV 距离）")]
    [SerializeField] private float startMoveThreshold = 0.01f;

    [SerializeField] private GameObject star;

    private bool _hasEraseStartUV;
    private Vector2 _eraseStartUV;

    public void PlayPickUpScroll()
    {
        PlayPickAnim();
    }

    private async void PlayPickAnim()
    {
        if (scrolls == null || enemy == null)
            return;

        scrolls.SetTrigger("pickup");

        await UniTask.WaitForSeconds(1f);

        enemy.ReadyToMove();

        star.SetActive(true);
    }

    void Update()
    {
        if (enemy && !enemy.IsDie)
            enemy.OnUpdate();

        HandleEraseInput(null);
        CheckEraseProgress();
    }

    private void OnEnable()
    {
        if (brushClear != null)
            brushClear.Completed += OnBrushClearCompleted;
    }

    private void OnDisable()
    {
        if (brushClear != null)
            brushClear.Completed -= OnBrushClearCompleted;
    }

    private void OnBrushClearCompleted()
    {
        if (_clearCompleted)
            return;
        _clearCompleted = true;
        OnEraseCompleted();
    }

    #region 擦除交互

    public void HandleEraseInput(Transform target)
    {
        if (_clearCompleted)
            return;

        if (brushClear == null || levelCamera == null)
            return;

        Vector2? inputPos = null;
        bool isNewPress = false;
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Began)
                inputPos = t.position;
            if (t.phase == TouchPhase.Began)
                isNewPress = true;
        }
        else if (Input.GetMouseButton(0))
        {
            inputPos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
                isNewPress = true;
        }

        if (!inputPos.HasValue)
        {
            _lastEraseUV = new Vector2(-1f, -1f);
            _hasEraseStartUV = false;
            return;
        }

        if (brushClear.TryGetSpriteUV(inputPos.Value, levelCamera, out var uv))
        {
            // 新的一笔：只记录起点，避免点击直接触发擦除
            if (isNewPress || !_hasEraseStartUV)
            {
                _eraseStartUV = uv;
                _hasEraseStartUV = true;
                _lastEraseUV = new Vector2(-1f, -1f);
                return;
            }

            // 移动不足：仍然不开始擦除
            if (Vector2.Distance(uv, _eraseStartUV) < startMoveThreshold)
                return;

            if (_lastEraseUV.x < 0f)
            {
                brushClear.StampEraseAt(uv);
            }
            else
            {
                float dist = Vector2.Distance(uv, _lastEraseUV);
                if (dist >= eraseMoveThreshold)
                {
                    // 两点间插值，防止快速滑动断触
                    int segments = Mathf.Max(1, Mathf.CeilToInt(dist / (eraseMoveThreshold * 0.5f)));
                    for (int i = 1; i <= segments; i++)
                    {
                        Vector2 interUV = Vector2.Lerp(_lastEraseUV, uv, (float)i / segments);
                        brushClear.StampEraseAt(interUV);
                    }
                }
            }
            _lastEraseUV = uv;
        }
    }

    private float currentProgress = 0f;

    private void CheckEraseProgress()
    {
        if (brushClear == null || _clearCompleted) return;

        float progress = brushClear.GetClearProgress();
        
        // 按间隔输出日志，避免刷屏
        float now = Time.time;
        if (now - _lastProgressCheckTime >= progressCheckInterval)
        {
            _lastProgressCheckTime = now;
            if (Mathf.Abs(currentProgress - progress) > 0.0001f)
            {
                currentProgress = progress;
                this.Log($"擦除进度: {progress:P1}");
            }
        }

        if (progress >= clearProgressThreshold)
        {
            _clearCompleted = true;
            OnEraseCompleted();
        }
    }

    /// <summary>
    /// 擦除进度达到阈值后的回调，可在此触发后续关卡逻辑。
    /// </summary>
    private void OnEraseCompleted()
    {
        this.Log("擦除完成！");
        // TODO: 触发卷轴显现、敌人激活或开门等逻辑
        // 例如：PlayPickUpScroll();
        if (levelDoor != null)
            levelDoor.enabled = true;

        // 隐藏/销毁刮刮乐目标（根据需求二选一）
        if (brushClear != null)
        {
            // 渐隐（表现更自然）
            var sr = brushClear.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.DOFade(0f, 0.25f).SetEase(Ease.OutQuad).OnComplete(() =>
                {
                    sr.enabled = false;
                });
            }

            // 方式2：直接销毁整个刮刮乐对象（如果后续不再需要）
            // Destroy(brushClear.gameObject);
        }
    }

    #endregion

    // 进门出门表现
    #region ActorEnterFackDoor

    [SerializeField] SuccessAnimCtrl enterDoor;

    [SerializeField] ActorSpineCtrl outDoor;

    [SerializeField] private ActorCtrl target;
    [SerializeField] private Transform endTarget;
    [SerializeField] private float duration;

    private Sequence seq;

    // enter door

    public void OnEnterDoor()
    {
        if (target == null)
            return;

        target.StopMoving();
        TriggerEnterDoor().Forget();
    }


    public async UniTask TriggerEnterDoor()
    {
        if (enterDoor == null)
            return;

        enterDoor.Play(OutDoor);

        await UniTask.WaitForSeconds(0.4f);

        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
        PlayDestroy();
    }

    public void PlayDestroy()
    {
        if (target == null || endTarget == null)
            return;

        if (duration <= 0f)
            duration = 0.01f;

        seq?.Kill();
        seq = null;

        seq = DOTween.Sequence();
        target.StopMoving();
        var rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.simulated = false;

        var trs = target.transform;
        seq.Append(trs.DOMove(endTarget.position + new Vector3(0, 1f, 0), duration));
        seq.Join(trs.DOScale(Vector3.zero, duration));

        var angle = trs.localEulerAngles;
        angle.z = angle.y != 0 ? -720 : 720;
        seq.Join(trs.DORotate(angle, duration, RotateMode.FastBeyond360));
        seq.OnComplete(() =>
        {
            target.DOKill();
        });
    }

    private void OutDoor()
    {
        if (target == null)
            return;

        target.transform.localEulerAngles = Vector3.zero;
        target.SetFaceDir(true);

        PlayOutDoor().Forget();
    }

    private async UniTask PlayOutDoor()
    {
        if (target == null || outDoor == null)
            return;

        if (!outDoor.IsPlayingAnimation("door_open"))
        {
            AudioManager.PlaySound("openDoor");
            outDoor.PlayAnimation("door_open", false);
        }

        await UniTask.WaitForSeconds(0.5f);

        target.transform.position = outDoor.transform.position;
        var rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.simulated = true;

        target.Knockback(new Vector2(.6f, 2f), 10f, .8f);
        target.ResumeMoving();
        target.transform.DOScale(Vector3.one, 0.2f);

        await UniTask.WaitForSeconds(0.5f);

        if (!outDoor.IsPlayingAnimation("dose_close"))
        {
            AudioManager.PlaySound("closeDoor");
            outDoor.PlayAnimation("dose_close", false);
        }
    }

    #endregion

    private void OnDestroy()
    {
        seq?.Kill();
        seq = null;

        if (target != null)
        {
            target.transform.DOKill();
            target.DOKill();
        }

        if (scrolls != null)
        {
            scrolls.DOKill();
        }

        if (enemy != null)
        {
            enemy.transform.DOKill();
            enemy.DOKill();
        }

        if (enterDoor != null)
        {
            enterDoor.transform.DOKill();
            enterDoor.DOKill();
        }

        if (outDoor != null)
        {
            outDoor.transform.DOKill();
            outDoor.DOKill();
        }
    }
}
