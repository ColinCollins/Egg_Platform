using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ReactionButtonEffect : MonoBehaviour
{
    [SerializeField] private CustomButton btn;
    [SerializeField] private GameObject lightThumb;
    [SerializeField] private Image light;

    private Sequence seq;
    private bool _isBound;

    void OnEnable()
    {
        BindButton();
        HideEffect();
    }

    void OnDisable()
    {
        UnbindButton();
        StopEffect();
        HideEffect();
    }

    void OnDestroy()
    {
        UnbindButton();
        StopEffect();
    }

    public void OnButtonClick(CustomButton _)
    {
        PlayEffect();
    }

    public void OnGameObjectSwitchToggle(bool isOn)
    {
        if (isOn)
        {
            PlayEffect();
            return;
        }

        StopEffect();
        HideEffect();
    }

    private void BindButton()
    {
        if (_isBound || btn == null)
        {
            return;
        }

        btn.OnClick += OnButtonClick;
        _isBound = true;
    }

    private void UnbindButton()
    {
        if (!_isBound || btn == null)
        {
            return;
        }

        btn.OnClick -= OnButtonClick;
        _isBound = false;
    }

    private void PlayEffect()
    {
        if (light == null)
        {
            return;
        }

        StopEffect();

        SetActiveIfNeeded(lightThumb, true);
        SetActiveIfNeeded(light.gameObject, true);

        light.transform.localScale = Vector3.zero;
        SetLightAlpha(1f);

        seq = DOTween.Sequence()
            .SetUpdate(true)
            .Append(light.transform.DOScale(Vector3.one * 2f, 0.3f))
            .Join(light.DOFade(0f, 0.2f).SetDelay(0.1f));
            // .OnComplete(HideEffect);
    }

    private void StopEffect()
    {
        if (seq == null)
        {
            return;
        }

        seq.Kill();
        seq = null;
    }

    private void HideEffect()
    {
        SetActiveIfNeeded(lightThumb, false);

        if (light == null)
        {
            return;
        }

        light.transform.localScale = Vector3.zero;
        SetLightAlpha(0f);
        SetActiveIfNeeded(light.gameObject, false);
    }

    private void SetLightAlpha(float alpha)
    {
        if (light == null)
        {
            return;
        }

        Color color = light.color;
        color.a = alpha;
        light.color = color;
    }

    private static void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
