using Game.Scripts.Common;
using Game.Turntable;
using UnityEngine;

public class Leve39Ctrl : MonoBehaviour
{
    [SerializeField] private Transform actor;

    [SerializeField] private Transform stopPoint;

    [SerializeField] private ParticleSystem leaves;

    [SerializeField] private ActorSpineCtrl fanSpineCtrl1;
    [SerializeField] private ActorSpineCtrl fanSpineCtrl2;

    [Tooltip("运行时按与 StopPoint 水平距离改写 Force Magnitude；Force Angle 等由 Inspector 配置。")]
    [SerializeField] private AreaEffector2D areaEffect;
    [SerializeField] private Transform currentCamera;

    [Header("Area effect — 距停点越近风力越弱（力越小）")]
    [Tooltip("与 StopPoint 水平距离 ≥ 此值时用 forceWhenFar；距离 → 0 时过渡到 forceWhenNearStop")]
    [SerializeField] private float resistanceDistanceRef = 5f;

    [Tooltip("离停点较远时的 Force Magnitude（风较强，方向由 Effector 的 Force Angle 决定）")]
    [SerializeField] private float forceWhenFar = 10f;

    [Tooltip("贴近停点时的 Force Magnitude（风较弱）")]
    [SerializeField] private float forceWhenNearStop = 3f;

    [Header("Camera horizontal drag")]
    [SerializeField] private Transform dragObj;
    [SerializeField] private float minCameraX = -10f;
    [SerializeField] private float maxCameraX = 10f;

    [Header("干扰按钮")]
    [SerializeField] private SceneCommonButton button;
    // [SerializeField] private float fanDelay = 2.8f;

    private bool isFanOpen = true;

    private bool isEnterWindZone = false;

    private bool _isDelaying;
    private float _areaEffectForceMagnitudeDefault;

    private ActorCtrl actorCtrl;

    private void Awake()
    {
        if (areaEffect != null)
            _areaEffectForceMagnitudeDefault = areaEffect.forceMagnitude;

        actorCtrl = actor.GetComponent<ActorCtrl>();
        AudioManager.PlaySound("fan", loop: true);

        button.SetEnter(Play);
        button.SetExit(OnExitButton);
    }

    private async void Play()
    {
        if (_isDelaying)
            return;

        _isDelaying = true;

        if (isFanOpen)
            SwitchFanState(null, Vector2.zero);
    }

    private void OnExitButton()
    {
        if (!_isDelaying)
            return;

        if (!isFanOpen)
            SwitchFanState(null, Vector2.zero);

        // await UniTask.WaitForSeconds(fanDelay, cancellationToken: this.GetCancellationTokenOnDestroy());

        _isDelaying = false;
    }

    public void SwitchFanState(Transform target, Vector2 pos)
    {
        isFanOpen = !isFanOpen;

        if (isFanOpen)
        {
            fanSpineCtrl1.PlayAnimation("on_fan", true);
            fanSpineCtrl2.gameObject.SetActive(true);
            areaEffect.gameObject.SetActive(true);
            leaves.Play();

            AudioManager.PlaySound("fan", loop: true);
        }
        else
        {
            fanSpineCtrl1.PlayAnimation("off", true);
            fanSpineCtrl2.gameObject.SetActive(false);
            areaEffect.gameObject.SetActive(false);
            leaves.Stop();

            AudioManager.StopAllSound();
        }

        AudioManager.PlaySound("fanSwitch");
    }

    void Update()
    {
        if (actor == null || stopPoint == null)
            return;

        UpdateAreaEffectForceByDistanceToStop();

        if (isFanOpen && isEnterWindZone)
        {
            if (actor.position.x > stopPoint.position.x)
            {
                actorCtrl.RB.linearVelocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 角色越接近 <see cref="stopPoint"/>（水平距离越小），<see cref="AreaEffector2D.forceMagnitude"/> 越接近 <see cref="forceWhenNearStop"/>（风力越弱）。
    /// 只使用 X 轴距离；不修改 Force Angle。
    /// 风扇关闭时恢复 Awake 时缓存的 Force Magnitude。
    /// </summary>
    private void UpdateAreaEffectForceByDistanceToStop()
    {
        if (areaEffect == null)
            return;

        if (!isFanOpen)
        {
            areaEffect.forceMagnitude = _areaEffectForceMagnitudeDefault;
            return;
        }

        float dx = Mathf.Abs(actor.position.x - stopPoint.position.x);
        float t = 1f - Mathf.Clamp01(dx / Mathf.Max(resistanceDistanceRef, 0.0001f));
        areaEffect.forceMagnitude = Mathf.Lerp(forceWhenFar, forceWhenNearStop, t);
    }

    /// <summary>
    /// 拖拽回调（绑定 UIDragHandle.OnDragEvent / IDragHandler）：按指针 delta 平移 <see cref="currentCamera"/> 的 X，并夹在 [minCameraX, maxCameraX]。
    /// </summary>
    public void OnDrag(Transform targets)
    {
        if (currentCamera == null || dragObj == null)
            return;

        float posX = dragObj.position.x;
        if (posX < minCameraX || posX > maxCameraX)
        {
            var currentPos = dragObj.position;
            currentPos.x = Mathf.Clamp(posX, minCameraX, maxCameraX);
            dragObj.position = currentPos;
            return;
        }

        float dxWorld = posX / 1f;
        Vector3 pos = currentCamera.position;
        pos.x = Mathf.Clamp(pos.x - dxWorld, minCameraX, maxCameraX);
        currentCamera.position = pos;
    }

    public void EnterWindZone()
    {
        isEnterWindZone = true;
    }

    public void ExitWindZone()
    {
        isEnterWindZone = false;
    }
}
