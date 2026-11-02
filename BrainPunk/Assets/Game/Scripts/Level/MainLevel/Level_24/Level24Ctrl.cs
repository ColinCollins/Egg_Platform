using System.Threading.Tasks;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

public class Level24Ctrl : MonoBehaviour
{
    [SerializeField] private ActorCtrl actor;

    [SerializeField] private Transform outlet;

    [SerializeField] private Transform water;

    [SerializeField] private Camera mainCamera;

    [SerializeField] private DragableItem dragable;

    [SerializeField] private float outletOffsetX = 1.5f; // 碰撞体的 Z 轴位置

    [SerializeField] private float colliderZPosition = 0f; // 碰撞体的 Z 轴位置

    [SerializeField] private Animator waterOut;
    private Transform downBtn;
    private EventSubscriber _subscriber;

    private bool isPullWater;

    private float startPosX = 0;
    private RectTransform rightBtn;

    void Awake()
    {
        isPullWater = false;
        AddListener();

        ResetOutletPosition();
    }



    void Start()
    {
        downBtn = PlayCtrl.Instance.CurrentGamePlayPanel.transform.Find("Root/down_btn");

        ShowDiveButton().Forget();
    }

    private async UniTask ShowDiveButton()
    {
        downBtn.gameObject.SetActive(false);

        await UniTask.WaitForSeconds(2f, ignoreTimeScale: true);

        downBtn.gameObject.SetActive(true);
    }

    private async Task ResetOutletPosition()
    {
        await UniTask.WaitForSeconds(0.5f, ignoreTimeScale: true);
        rightBtn = PlayCtrl.Instance.CurrentGamePlayPanel.transform.GetChild(0).Find("rightMove_btn") as RectTransform;
        startPosX = rightBtn.anchoredPosition.x;

        var worldPos = ConvertUIToWorldPosition(rightBtn);
        worldPos.x += outletOffsetX;
        worldPos.y -= 0.1f;
        outlet.position = worldPos;
        
        await UniTask.WaitForSeconds(1f);
        outlet.gameObject.SetActive(true);
    }

    void Update()
    {
        CheckRightBtnPosition();
    }

    private void CheckRightBtnPosition()
    {
        if (rightBtn == null)
            return;

        var dx = rightBtn.anchoredPosition.x - startPosX;
        if (dx > 200)
        {
            dragable.SetEnableDrag(true);
        }
    }

    /// <summary>
    /// 将 UI 按钮位置转换为场景世界位置
    /// </summary>
    private Vector3 ConvertUIToWorldPosition(RectTransform buttonRect)
    {

        var canvas = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponentInParent<Canvas>();
        var uiCamera = canvas.worldCamera;

        if (canvas == null || mainCamera == null)
            return Vector3.zero;

        Vector2 screenPoint;

        // 根据 Canvas 渲染模式获取屏幕坐标
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Screen Space - Overlay: 直接使用 RectTransform 的世界位置转换为屏幕坐标
            screenPoint = RectTransformUtility.WorldToScreenPoint(null, buttonRect.position);
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && uiCamera != null)
        {
            // Screen Space - Camera: 使用 UI 相机转换
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, buttonRect.position);
        }
        else if (canvas.renderMode == RenderMode.WorldSpace)
        {
            // World Space: 已经是世界坐标，直接使用
            return new Vector3(buttonRect.position.x, buttonRect.position.y, colliderZPosition);
        }
        else
        {
            // 默认情况
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera ?? mainCamera, buttonRect.position);
        }

        // 将屏幕坐标转换为场景世界坐标
        // 使用合适的 Z 距离（可以根据需要调整）
        float zDistance = Mathf.Abs(mainCamera.transform.position.z - colliderZPosition);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDistance));

        // 保持指定的 Z 轴位置
        worldPos.z = colliderZPosition;

        return worldPos;
    }

    public virtual void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerDiveEvent>(OnPlayerDive);
    }

    private void OnPlayerDive(PlayerDiveEvent @evt)
    {
        actor.AddDiveVelocity();
    }

    void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);

        if (water)
            water.DOKill();
    }

    public void OnCorkExit(Transform target)
    {
        Debug.Log("-------------" + target);
        if (isPullWater)
            return;

        isPullWater = true;

        target.GetComponent<SpriteRenderer>().DOFade(0, 1f);

        AudioManager.PlaySound("drainage");

        water.DOLocalMoveY(-8f, 4f).OnComplete(() =>
        {
            Destroy(water.GetChild(0).gameObject);
        });

        waterOut.gameObject.SetActive(true);

        WaterOutSmall();

    }

    private async Task WaterOutSmall()
    {
        await UniTask.WaitForSeconds(2.3f);

        waterOut.SetBool("isSmall", true);

        await UniTask.WaitForSeconds(2f);

        waterOut.GetComponent<SpriteRenderer>().DOFade(0, 0.5f);
    }
}
