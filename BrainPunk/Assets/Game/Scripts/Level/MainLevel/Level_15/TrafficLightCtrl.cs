using System;
using System.Threading.Tasks;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

public class TrafficLightCtrl : MonoBehaviour, IDebuger
{
    [Header("References")]
    [SerializeField] private GameObject mainLight;
    [SerializeField] private DirectMove[] doors;

    [SerializeField] private ActorCtrl player;

    [SerializeField] private GameObject[] lights;
    [SerializeField] private Rigidbody2D boxRigidbody;
    [SerializeField] private GameObject[] cracks;
    [SerializeField] private GameObject[] boxes;
    [SerializeField] private GameObject[] grounds;

    [SerializeField] private bool hasDistanceRule;
    [SerializeField] private GameObject[] distanceTarget;

    [Header("Settings")]
    [SerializeField] private float flashTime = .6f;

    [SerializeField] private float maxGreenTime = 5f;
    [SerializeField] private float maxRedTime = 5f;

    private const int MAX_CLICK_STATE = 2;

    private bool isGreen;
    private bool isBroken;
    private bool isFall;
    private bool isShow = false;
    private bool stopSwitch = false;
    private float timer;
    private int clickState;

    private MelenitasDev.SoundsGood.Sound redLight;

    // 闪烁时间
    private int flashCount = 0;
    private float flashTimer = 0;

    private EventSubscriber _subscriber;

    void Start()
    {
        isGreen = true;
        isFall = false;
        isBroken = false;
        isShow = false;
        stopSwitch = false;
        clickState = 0;
        flashCount = 0;
        timer = maxGreenTime;

        UpdateAnimatorState();
        // AddListener();
    }

    void OnDestroy()
    {
        RemoveListener();
        for (int i = 0; i < grounds.Length; i++)
        {
            grounds[i].transform.DOKill();
        }

        if (redLight != null)
        {
            redLight.Stop();
            Debug.Log("Stop redLight audio");
        }
    }

    public void OnShowLight()
    {
        Debug.Log("AddListener");
        ShowLight();
        AudioManager.PlaySound("showTrafficLight");
    }

    private async Task ShowLight()
    {
        await UniTask.WaitForSeconds(0.5f);
        isShow = true;
        AddListener();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerMovingRight);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerMovingLeft);
    }

    private void RemoveListener()
    {
        _subscriber?.Dispose();
        _subscriber = null;
    }

    private void OnPlayerMovingRight(PlayerRightMoveEvent evt)
    {
        // TODO: Add player moving logic here
        // Debug.Log("Player is moving!");
        OnStopMoving();
    }

    private void OnPlayerMovingLeft(PlayerLeftMoveEvent evt)
    {
        // TODO: Add player moving logic here
        Debug.Log("Player is moving!");
        OnStopMoving();
    }

    void OnStopMoving()
    {
        if (isGreen || isFall || !mainLight.activeSelf || isBroken)
            return;

        TriggerFall();

        if (doors != null)
        {
            for (int i = 0; i < doors.Length; i++)
                doors[i].enabled = true;
        }
    }

    private void TriggerFall()
    {
        isFall = true;
        AudioManager.PlaySound("level16Drop");
        for (int i = 0; i < grounds.Length; i++)
        {
            grounds[i].transform.DOMoveY(-40f, 3f);
        }

        player.StopMoving();
    }


    void Update()
    {
        if (isBroken || !isShow || stopSwitch)
            return;

        UpdateTimer();
    }

    private void UpdateTimer()
    {
        timer -= Time.deltaTime;
        var greenTime = GetCurrentLeftGreenTime();
        timer = Mathf.Min(greenTime, timer);
        if (greenTime < flashTime * 3)
        {
            isGreen = false;
            stopSwitch = true;
            UpdateAnimatorState();
        }
        else
        {
            if (timer < flashTime * 3 && flashCount < 3)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0)
                {
                    flashCount++;
                    flashTimer = flashTime;
                    PlayFlash();
                    this.Log($"------flash: {flashTimer}");
                }
            }

            if (timer <= 0f)
            {
                SwitchStateByDistance();
                timer = isGreen ? maxGreenTime : maxRedTime;
                timer += flashTime * 3;
                flashCount = 0;
                flashTimer = 0;
                UpdateAnimatorState();

                this.Log($"------WaitingTIme: {timer}");
            }
        }


        // this.Log($"------WaitingTIme: {timer}");
    }

    private void UpdateAnimatorState()
    {
        lights[0].SetActive(isGreen);
        lights[1].SetActive(!isGreen);

        if (!isGreen)
        {
            if (redLight == null || !redLight.Playing)
                redLight = AudioManager.PlaySound("redLight", loop: true);
        }
        else
        {
            if (redLight != null)
                redLight.Stop();
        }
    }

    private async Task PlayFlash()
    {
        lights[0].SetActive(false);
        lights[1].SetActive(false);

        await UniTask.WaitForSeconds(0.2f);
        UpdateAnimatorState();
    }

    private void UpdateCrackState(int activeIndex)
    {
        for (int i = 0; i < cracks.Length; i++)
        {
            if (cracks[i] != null)
            {
                cracks[i].SetActive(i == activeIndex);
            }
        }
    }

    public void OnClick()
    {
        Debug.Log("TrafficLight clicked!");

        clickState++;

        if (clickState > MAX_CLICK_STATE)
        {
            TriggerDrop();
            return;
        }

        // Update crack display based on click state
        UpdateCrackState(clickState - 1);
    }

    private void TriggerDrop()
    {
        Debug.Log("TrafficLight drop!");

        if (boxRigidbody != null)
        {
            boxRigidbody.simulated = true;
            isBroken = true;
            if (redLight != null)
                redLight.Stop();
            mainLight.GetComponent<FollowTarget>().enabled = false;
            AudioManager.PlaySound("level16Drop");
        }
    }

    public void Broken()
    {
        if (boxes.Length < 2) return;

        boxes[1].transform.localPosition = boxes[0].transform.localPosition;
        boxes[0].SetActive(false);
        boxes[1].SetActive(true);

        AudioManager.PlaySound("trafficLightCrash");
        EventsUtils.ResetEvents(ref _subscriber);
    }

    private void SwitchStateByDistance()
    {
        if (hasDistanceRule)
        {
            var distance = GetHorizontalDistance();
            maxGreenTime = Mathf.Clamp(distance, 0, 12f) / 12f * maxGreenTime;

            // Debug.Log($"distance: {distance}---- {maxGreenTime}");
        }

        if (maxGreenTime <= 0.01)
            isGreen = false;
        else
        {
            isGreen = !isGreen;
        }
    }

    private float GetCurrentLeftGreenTime()
    {
        if (!hasDistanceRule)
            return maxGreenTime;

        var distance = GetHorizontalDistance();
        float time = Mathf.Clamp(distance, 0, 15f) / 15f * maxGreenTime;
        return time;
    }

    private float GetHorizontalDistance()
    {
        if (distanceTarget == null || distanceTarget.Length < 2) return 0f;
        if (distanceTarget[0] == null || distanceTarget[1] == null) return 0f;

        return Mathf.Abs(distanceTarget[1].transform.position.x - distanceTarget[0].transform.position.x);
    }
}
