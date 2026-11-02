using System;
using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using UnityEngine;

public class Level21Ctrl : MonoBehaviour, IEventSender, IDebuger
{
    public GameObject NoiseArea;
    [SerializeField] private Level21ActorCtrl actorCtrl;

    [SerializeField] private Collider2D door;

    [SerializeField] private Transform noiseDistancePoint;
    [SerializeField] private AudioSource noise;
    private bool isInNoise = false;
    private bool isDestroy = false;

    private bool isPassed = false;

    private float noiseMaxDistance;
    private EventSubscriber _subscriber;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OpenSfx();

        noiseMaxDistance = door.transform.position.x - noiseDistancePoint.position.x;

        AddListener();
    }

    public virtual void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<MusicToggleEvent>(OnMusicToggleChanged);
        _subscriber.Subscribe<SfxToggleEvent>(OnSfxToggleChanged);
    }

    private void OnSfxToggleChanged(SfxToggleEvent @event)
    {
        RefreshNoise();
    }


    private void OnMusicToggleChanged(MusicToggleEvent @event)
    {
        RefreshNoise();
    }

    void RefreshNoise()
    {
        if (isPassed)
            return;
        Debug.Log($"---------- Music: {DB.GameSetting.MusicOn} : Sfx: {DB.GameSetting.SfxOn}");
        if (!DB.GameSetting.MusicOn && !DB.GameSetting.SfxOn)
        {
            door.enabled = true;
            NoiseArea.SetActive(false);
            noise.Stop();
        }
        else
        {
            door.enabled = false;
            NoiseArea.SetActive(true);
            noise.Play();
        }
    }

    void Update()
    {
        UpdateNoiseVolume();
    }

    void UpdateNoiseVolume()
    {
        if (isPassed)
            return;

        if (noiseDistancePoint && noise && actorCtrl)
        {
            var distance = door.transform.position.x - actorCtrl.transform.position.x;
            var basic = distance > noiseMaxDistance ? 0.1f : 0.3f;
            distance = Math.Clamp(distance, 0, noiseMaxDistance);
            noise.volume = (noiseMaxDistance - distance) / noiseMaxDistance + basic;
        }
    }

    public void PlayCoverEarTrigger(Collider2D collider)
    {
        isInNoise = true;
        actorCtrl.TriggerCoverEar();
        this.Log("trigger enter noise");
    }

    public void ExitNoiseArea(Collider2D collider)
    {
        isInNoise = false;
        actorCtrl.StopCoverEar();
    }

    public void OpenSfx()
    {
        DB.GameSetting.MusicOn = true;
        DB.GameSetting.SfxOn = true;

        this.DispatchEvent(Witness<SfxToggleEvent>._, true);
        this.DispatchEvent(Witness<MusicToggleEvent>._, true);

        DB.GameSetting.Save();
    }

    public void PassedOpenSfx(Collider2D collider)
    {
        isPassed = true;
        OpenSfx();
    }

    void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
    }
}
