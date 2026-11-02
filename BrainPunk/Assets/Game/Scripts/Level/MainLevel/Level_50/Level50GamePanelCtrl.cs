using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SwitchToFly 后，开始 3s 倒计时，然后文本切换为 GO 等待 2s，在 2s 内点击 FireToFly 触发飞行指令。OnTiggerItemEvent id = 2
/// 若不在时间内点击飞行，或超出 GO 的等待时间后仍未点击飞行，则触发飞行失败指令 OnTiggerItemEvent id = 3
/// </summary>
public class Level50GamePanelCtrl : MonoBehaviour, IEventSender
{
    private const int SuccessEventId = 2;
    private const int FailEventId = 3;
    private const int CountdownSeconds = 3;
    private const int GoWaitSeconds = 2;

    [SerializeField] private CustomButton flyBtn;
    [SerializeField] private GameObject jumpBtn;
    [SerializeField] private GameObject leftBtn;
    [SerializeField] private GameObject rightBtn;
    [SerializeField] private TextMeshProUGUI countdownTxt;
    [SerializeField] private Image countdownMask;
    [SerializeField] private RectTransform countdownObj;
    /// <summary>
    /// 
    /// </summary>
    [SerializeField] private TextMeshProUGUI goalTxt;
    [SerializeField] private GameObject goalBoard;

    private bool canFly;
    private bool isFinished;
    private bool isSequenceStarted;

    private void Start()
    {
        if (flyBtn != null)
        {
            flyBtn.OnClick += FireToFly;
            flyBtn.gameObject.SetActive(false);
            flyBtn.transform.localScale = Vector3.zero;
        }

        if (countdownObj != null)
        {
            countdownObj.gameObject.SetActive(false);
            countdownTxt.text = string.Empty;
        }

        if (goalBoard)
            goalBoard.gameObject.SetActive(false);
    }

    private void FireToFly(CustomButton btn)
    {
        if (isFinished)
            return;

        if (!canFly)
        {
            FinishFlyFlow(false);
            return;
        }

        FinishFlyFlow(true);

        AudioManager.PlaySound("fanSwitch");
    }

    public void SwitchToFly()
    {
        if (isFinished || isSequenceStarted)
            return;

        isSequenceStarted = true;

        if (jumpBtn != null)
            jumpBtn.SetActive(false);

        SwitchMoveBtns(false);

        if (countdownObj != null)
        {
            countdownObj.gameObject.SetActive(true);
            countdownObj.DOKill();
            countdownObj.localScale = Vector3.zero;
            countdownObj.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }

        if (flyBtn != null)
        {
            flyBtn.gameObject.SetActive(true);
            flyBtn.transform.DOKill();
            flyBtn.transform.localScale = Vector3.zero;
            flyBtn.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
        }

        RunFlySequence().Forget();
    }

    public void SwitchMoveBtns(bool isShow)
    {
        if (leftBtn != null)
            leftBtn.SetActive(isShow);

        if (rightBtn != null)
            rightBtn.SetActive(isShow);
    }

    private async UniTaskVoid RunFlySequence()
    {
        canFly = false;

        for (int i = CountdownSeconds; i > 0; i--)
        {
            if (countdownTxt != null)
                countdownTxt.text = i.ToString();

            if (countdownMask != null)
            {
                countdownMask.DOKill();
                countdownMask.fillAmount = 1f;
                countdownMask.DOFillAmount(0f, 1f).SetEase(Ease.Linear);
            }

            AudioManager.PlaySound("countdown");

            await UniTask.WaitForSeconds(1f, cancellationToken: this.GetCancellationTokenOnDestroy());

            countdownMask.fillAmount = 1f;
            if (isFinished)
                return;
        }

        if (countdownMask != null)
            countdownMask.DOKill();

        canFly = true;

        if (countdownTxt != null)
        {
            countdownTxt.text = "GO";
            countdownTxt.transform.localScale = Vector3.one * 1.5f;
            countdownTxt.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }

        AudioManager.PlaySound("fireLaunch");
        await UniTask.WaitForSeconds(GoWaitSeconds, cancellationToken: this.GetCancellationTokenOnDestroy());

        if (!isFinished)
            FinishFlyFlow(false);
    }

    private void FinishFlyFlow(bool isSuccess)
    {
        if (isFinished)
            return;

        isFinished = true;
        canFly = false;

        if (countdownObj != null)
            countdownObj.gameObject.SetActive(false);
        else if (countdownTxt != null)
            countdownTxt.gameObject.SetActive(false);

        if (flyBtn != null)
        {
            flyBtn.transform.DOKill();
            flyBtn.transform.localScale = Vector3.zero;
            flyBtn.gameObject.SetActive(false);
        }

        this.DispatchEvent(Witness<OnTiggerItemEvent>._, isSuccess ? SuccessEventId : FailEventId);
    }

    public void SwitchToFlyModelButtons()
    {
        if (rightBtn != null)
        {
            AlignRightButton();
        }

        SwitchMoveBtns(true);
    }

    private void AlignRightButton()
    {
        RectTransform rectTransform = rightBtn.transform as RectTransform;
        if (rectTransform == null)
            return;

        Vector2 anchoredPosition = rectTransform.anchoredPosition;
        rectTransform.anchorMin = new Vector2(1f, rectTransform.anchorMin.y);
        rectTransform.anchorMax = new Vector2(1f, rectTransform.anchorMax.y);
        rectTransform.pivot = new Vector2(.5f, rectTransform.pivot.y);
        anchoredPosition.x = -Mathf.Abs(254);
        rectTransform.anchoredPosition = anchoredPosition;
    }

    // 从 10000m 显示，DoTween Count int 类型降到 0 结束
    public void StartGoal(float duration)
    {
        if (goalBoard == null || goalTxt == null)
            return;

        goalTxt.DOKill();
        goalBoard.gameObject.SetActive(true);
        goalBoard.transform.DOKill();
        goalBoard.transform.localScale = Vector3.zero;
        goalBoard.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

        DOVirtual.Int(10000, 0, duration, value =>
        {
            if (goalTxt != null)
                goalTxt.text = $"{value}m";
        }).SetEase(Ease.Linear).SetTarget(goalTxt);
    }

    private void OnDestroy()
    {
        if (flyBtn != null)
        {
            flyBtn.OnClick -= FireToFly;
            flyBtn.transform.DOKill();
        }

        if (countdownTxt != null)
        {
            countdownTxt.transform.DOKill();
        }

        if (goalTxt != null)
        {
            goalTxt.DOKill();
        }

        if (countdownMask != null)
        {
            countdownMask.DOKill();
        }

        if (countdownObj != null)
        {
            countdownObj.DOKill();
        }

        if (goalBoard != null)
        {
            goalBoard.transform.DOKill();
        }
    }
}
