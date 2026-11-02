using System.Collections;
using MelenitasDev.SoundsGood;
using UnityEngine;

public class SimpleAudioPlay : MonoBehaviour
{
    [SerializeField] private string clipName;
    [SerializeField] private bool loop;
    [SerializeField] private float loopInterval;
    [SerializeField] private bool playOnStart;
    [SerializeField] private bool stopSoundOnDisable = true;

    private Coroutine _loopCoroutine;
    private Sound _currentSound;

    private void Start()
    {
        if (playOnStart)
            PlayAudio();
    }

    private void OnDisable()
    {
        if (stopSoundOnDisable)
        {
            StopAudio();
            return;
        }

        StopLoopCoroutine();
    }

    private void OnDestroy()
    {
        StopAudio();
    }

    public void PlayAudio()
    {
        if (string.IsNullOrEmpty(clipName))
            return;

        StopAudio();

        if (!loop)
        {
            _currentSound = Game.Scripts.Common.AudioManager.PlaySound(clipName);
            return;
        }

        if (loopInterval <= 0f)
        {
            _currentSound = Game.Scripts.Common.AudioManager.PlaySound(clipName, loop: true);
            return;
        }

        _loopCoroutine = StartCoroutine(PlayLoopWithInterval());
    }

    public void StopAudio()
    {
        StopLoopCoroutine();

        if (_currentSound != null && _currentSound.Using)
            _currentSound.Stop();

        _currentSound = null;
    }

    public void RestartAudio()
    {
        PlayAudio();
    }

    private IEnumerator PlayLoopWithInterval()
    {
        WaitForSeconds wait = new WaitForSeconds(loopInterval);

        while (isActiveAndEnabled)
        {
            _currentSound = Game.Scripts.Common.AudioManager.PlaySound(clipName);
            yield return wait;
        }
    }

    private void StopLoopCoroutine()
    {
        if (_loopCoroutine == null)
            return;

        StopCoroutine(_loopCoroutine);
        _loopCoroutine = null;
    }
}
