using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using TMPro;
using UnityEngine;

public class Level56GamePanelCtrl : MonoBehaviour
{
    private const float GoalDistance = 1000f;

    [SerializeField] private GamePlayPanel playPanel;
    [SerializeField] private TextMeshProUGUI goalTxt;
    [SerializeField] private GameObject goalBoard;
    [SerializeField] private bool hideGoalBoardOnStop = true;
    [SerializeField] private float moveDistanceStep = 10;

    private float currentDistance = GoalDistance;
    private float goalTweenDistance = GoalDistance;
    private float moveDistanceOffset;
    private Transform leftMoveBtn;
    private Transform rightMoveBtn;
    private Tween goalTween;
    private EventSubscriber _subscriber;
    private bool isGoalRunning;

    private void Start()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);

        ResolvePlayPanel();
        CacheMoveButtons();

/*         if (goalBoard != null)
        {
            goalBoard.SetActive(false);
        } */

        RefreshGoalText();
    }

    public void SwitchMoveBtns(bool isShow)
    {
        CacheMoveButtons();

        if (leftMoveBtn != null)
        {
            leftMoveBtn.gameObject.SetActive(isShow);
        }

        if (rightMoveBtn != null)
        {
            rightMoveBtn.gameObject.SetActive(isShow);
        }
    }

    public void StartGoal(float duration)
    {
        if (goalBoard == null || goalTxt == null)
        {
            return;
        }

        float startDistance = Mathf.Max(0f, currentDistance);
        isGoalRunning = true;
        goalTweenDistance = startDistance;
        moveDistanceOffset = 0;
        currentDistance = startDistance;
        goalTween?.Kill();
        goalTxt.DOKill();
        goalBoard.transform.DOKill();

        RefreshGoalText();
        goalBoard.SetActive(true);
/*         goalBoard.transform.localScale = Vector3.zero;
        goalBoard.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack); */

        goalTween = DOVirtual.Float(startDistance, 0f, Mathf.Max(0.01f, duration), value =>
        {
            goalTweenDistance = value;
            RefreshCurrentDistance();
        }).SetEase(Ease.Linear).SetTarget(goalTxt);
    }

    public void StopGoal()
    {
        isGoalRunning = false;
        goalTween?.Kill();
        goalTween = null;

        if (goalTxt != null)
        {
            goalTxt.DOKill();
            goalTxt.text = "0.0m";
        }

        if (goalBoard != null)
        {
            goalBoard.transform.DOKill();
            if (hideGoalBoardOnStop)
            {
                goalBoard.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        goalTween?.Kill();
        goalTween = null;

        if (goalTxt != null)
        {
            goalTxt.DOKill();
        }

        if (goalBoard != null)
        {
            goalBoard.transform.DOKill();
        }
    }

    private void ResolvePlayPanel()
    {
        if (playPanel != null)
        {
            return;
        }

        playPanel = GetComponent<GamePlayPanel>();
        if (playPanel == null)
        {
            playPanel = PlayCtrl.Instance?.CurrentGamePlayPanel;
        }
    }

    private void CacheMoveButtons()
    {
        ResolvePlayPanel();
        if (playPanel == null)
        {
            return;
        }

        if (leftMoveBtn == null)
        {
            leftMoveBtn = playPanel.transform.Find("Root/leftMove_btn") ?? playPanel.transform.Find("leftMove_btn");
        }

        if (rightMoveBtn == null)
        {
            rightMoveBtn = playPanel.transform.Find("Root/rightMove_btn") ?? playPanel.transform.Find("rightMove_btn");
        }
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        ChangeDistance(Mathf.Max(0f, moveDistanceStep));
    }

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        ChangeDistance(-Mathf.Max(0f, moveDistanceStep));
    }

    private void ChangeDistance(float delta)
    {
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        if (isGoalRunning)
        {
            moveDistanceOffset += delta;
            RefreshCurrentDistance();
            return;
        }

        UpdateCurrentDistance(currentDistance + delta);
    }

    private void UpdateCurrentDistance(float value)
    {
        currentDistance = Mathf.Max(0f, value);
        RefreshGoalText();
    }

    private void RefreshCurrentDistance()
    {
        UpdateCurrentDistance(goalTweenDistance + moveDistanceOffset);
    }

    private void RefreshGoalText()
    {
        if (goalTxt != null)
        {
            goalTxt.text = $"{currentDistance:F1}m";
        }
    }
}
