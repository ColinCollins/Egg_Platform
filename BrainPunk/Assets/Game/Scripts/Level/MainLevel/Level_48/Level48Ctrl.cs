using DG.Tweening;
using UnityEngine;

public class Level48Ctrl : MonoBehaviour
{
    [SerializeField] private GameObject traps;
    [SerializeField] private GameObject platform;
    [SerializeField] private float fadeOutDuration = 0.5f;

    void Start()
    {
        var state = PlayCtrl.Instance.Level.CurrentLevelState;
        if (state.ResetCount > 0)
        {
            Invoke(nameof(FadeOutTraps), 1f);
        }
    }

    private void FadeOutTraps()
    {
        if (traps == null)
            return;

        var renderers = traps.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            sr.DOFade(0f, fadeOutDuration);
        }

        DOVirtual.DelayedCall(fadeOutDuration, () => traps.SetActive(false));
    }
}
