using DG.Tweening;
using Game.Scripts.Common;
using Game.Turntable;
using UnityEngine;

/// <summary>
/// Level 47 控制器
/// </summary>
public class Level47Ctrl : MonoBehaviour
{
    [Header("假门 - Button1")]
    // press 状态下假门沿 y 轴缓慢移动到指定位置。离开后，快速返回原位置
    [SerializeField] PressButtonBase button1;
    [SerializeField] private Transform fakeDoor;
    [SerializeField] private Vector3 fakeDoorTargetPos;
    [SerializeField] private Ease fakeDoorDownEase = Ease.OutQuad;
    [SerializeField] private Ease fakeDoorUpEase = Ease.OutQuad;
    [SerializeField] private float fakeDoorDownDuration = 1f;
    [SerializeField] private float fakeDoorUpDuration = 0.2f;

    [Header("风 - Button2")]
    // 进入就播，离开就停
    [SerializeField] PressButtonBase button2;
    [SerializeField] ParticleSystem wind;

    [Header("天花板 - Button3")]
    // press 状态下天花板沿 y 轴缓慢移动到指定位置。离开后，暂停不动
    [SerializeField] PressButtonBase button3;
    [SerializeField] private Transform wall;
    [SerializeField] private Vector3 wallTargetPos;
    [SerializeField] private float wallDownSpeed = 2f;

    private Vector3 _fakeDoorOriginPos;
    private Tweener _fakeDoorTweener;
    private MelenitasDev.SoundsGood.Sound _findItemsSound;
    private bool _isWallPressed;

    void Start()
    {
        if (fakeDoor != null)
            _fakeDoorOriginPos = fakeDoor.position;

        if (button1 != null)
        {
            button1.SetEnter(() => MoveFakeDoor(true));
            button1.SetExit(() => MoveFakeDoor(false));
        }

        if (button2 != null)
        {
      /*       button2.SetEnter(() => wind?.Play());
            button2.SetExit(() => wind?.Stop()); */
        }

        if (button3 != null)
        {
            button3.SetEnter(() => _isWallPressed = true);
            button3.SetExit(() => _isWallPressed = false);
        }
    }

    private void OnDestroy()
    {
        _fakeDoorTweener?.Kill();
        _findItemsSound?.Stop();

        if (button1 != null)
        {
            button1.SetEnter(null);
            button1.SetExit(null);
        }

        if (button2 != null)
        {
            button2.SetEnter(null);
            button2.SetExit(null);
        }

        if (button3 != null)
        {
            button3.SetEnter(null);
            button3.SetExit(null);
        }
    }

    void Update()
    {
        UpdateWall();
    }

    private void MoveFakeDoor(bool down)
    {
        if (fakeDoor == null)
            return;

        _fakeDoorTweener?.Kill();

        float targetY = down ? fakeDoorTargetPos.y : _fakeDoorOriginPos.y;
        float duration = down ? fakeDoorDownDuration : fakeDoorUpDuration;
        Ease ease = down ? fakeDoorDownEase : fakeDoorUpEase;

        _fakeDoorTweener = fakeDoor.DOMoveY(targetY, duration).SetEase(ease);

        if (down)
        {
            _findItemsSound ??= AudioManager.PlaySound("findItems", loop: true);
        }
        else
        {
            _findItemsSound?.Stop();
            _findItemsSound = null;
        }
    }

    private void UpdateWall()
    {
        if (wall == null || !_isWallPressed)
            return;

        Vector3 pos = wall.position;
        pos.y = Mathf.MoveTowards(pos.y, wallTargetPos.y, wallDownSpeed * Time.deltaTime);
        wall.position = pos;
    }
}
