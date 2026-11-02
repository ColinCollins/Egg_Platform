using System.Collections.Generic;
using Game.Scripts.Common;
using UnityEngine;

public class ActorStepPlayCtrl : MonoBehaviour
{
    private const int StepClipCount = 2;

    [Header("Refs")]
    [SerializeField] private ActorSpineCtrl actorSpineCtrl;

    [Header("Step Config")]
    [SerializeField] private List<string> walkAnimName = new List<string>()
    {
        "walk",
        "walk01"
    };
    [SerializeField] private string stepSfxTag = "step";
    [SerializeField] private float stepInterval = 0.25f;
    [SerializeField] private float stepVolume = 0.4f;
    [SerializeField, Min(0)] private int stepClipIndex = 0;

    private float nextPlayTime;
    private bool wasWalking;

    private int MaxStepClipIndex => Mathf.Max(0, StepClipCount - 1);

    private int CurrentStepClipIndex => Mathf.Clamp(stepClipIndex, 0, MaxStepClipIndex);

    private bool IsPlayingAnyWalkAnimation()
    {
        for (int i = 0; i < walkAnimName.Count; i++)
        {
            if (actorSpineCtrl.IsPlayingAnimation(walkAnimName[i]))
                return true;
        }
        return false;
    }

    private void OnValidate()
    {
        stepClipIndex = Mathf.Clamp(stepClipIndex, 0, MaxStepClipIndex);
        stepInterval = Mathf.Max(0.01f, stepInterval);
    }

    private void Awake()
    {
        if (actorSpineCtrl == null)
        {
            actorSpineCtrl = GetComponent<ActorSpineCtrl>();
        }

        if (actorSpineCtrl == null)
        {
            actorSpineCtrl = GetComponentInChildren<ActorSpineCtrl>();
        }
    }

    private void Update()
    {
        if (actorSpineCtrl == null || walkAnimName == null || walkAnimName.Count == 0 || string.IsNullOrEmpty(stepSfxTag))
        {
            return;
        }

        bool isWalking = IsPlayingAnyWalkAnimation();
        if (!isWalking)
        {
            wasWalking = false;
            return;
        }

        if (!wasWalking)
        {
            // Enter walk state: play first step immediately.
            wasWalking = true;
            nextPlayTime = Time.time;
        }

        if (Time.time < nextPlayTime)
        {
            return;
        }

        AudioManager.PlaySound(stepSfxTag, stepVolume, CurrentStepClipIndex);
        stepClipIndex = (CurrentStepClipIndex + 1) % StepClipCount;
        nextPlayTime = Time.time + Mathf.Max(0.01f, stepInterval);
    }
}
