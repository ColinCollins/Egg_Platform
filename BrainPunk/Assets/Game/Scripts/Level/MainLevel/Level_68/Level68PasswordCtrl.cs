using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using TMPro;
using UnityEngine;

public class Level68PasswordCtrl : MonoBehaviour, IDebuger
{
    private const string AutoPassword = "4148";

    [SerializeField] private GameObject[] numTxts;
    [SerializeField] private TextMeshProUGUI passwordTxt;

    [SerializeField] private PasswordPopup popup;

    private CancellationTokenSource _waitingCts;
    private Tween _revealTween;
    private string _initialPasswordText;
    private bool _hasCachedInitialText;

    public bool IsWaitingPassRunning { get; private set; }

    public void WaitingPass(Action onComplete = null)
    {
        CancelWaitingPass(false);

        EnsureReferences();

        if (popup == null)
        {
            this.LogWarning("[Level68PasswordCtrl] WaitingPass skipped: PasswordPopup reference is missing.");
            onComplete?.Invoke();
            return;
        }

        _waitingCts = new CancellationTokenSource();
        RunWaitingPassAsync(_waitingCts.Token, onComplete).Forget();
    }

    public void CancelWaitingPass(bool restorePresentation = true)
    {
        if (_waitingCts != null)
        {
            _waitingCts.Cancel();
            _waitingCts.Dispose();
            _waitingCts = null;
        }

        if (_revealTween != null)
        {
            _revealTween.Kill(false);
            _revealTween = null;
        }

        IsWaitingPassRunning = false;

        if (restorePresentation)
            RestorePresentation();
    }

    private async UniTaskVoid RunWaitingPassAsync(CancellationToken token, Action onComplete)
    {
        IsWaitingPassRunning = true;

        try
        {
            popup.ResetInput();
            HideNumberTexts();
            await RevealPasswordTextAsync(token);

            for (int i = 0; i < AutoPassword.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                popup.AddInput(AutoPassword[i] - '0');
            }

            onComplete?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsWaitingPassRunning = false;
            _waitingCts?.Dispose();
            _waitingCts = null;
            _revealTween = null;
        }
    }

    private async UniTask RevealPasswordTextAsync(CancellationToken token)
    {
        if (passwordTxt == null)
        {
            this.LogWarning("[Level68PasswordCtrl] Password clue text is missing; auto input will continue without reveal.");
            return;
        }

        if (!_hasCachedInitialText)
        {
            _initialPasswordText = passwordTxt.text;
            _hasCachedInitialText = true;
        }

        passwordTxt.gameObject.SetActive(true);
        passwordTxt.maxVisibleCharacters = 0;
        passwordTxt.ForceMeshUpdate();

        int characterCount = Mathf.Max(0, passwordTxt.textInfo.characterCount);
        if (characterCount <= 0)
        {
            passwordTxt.maxVisibleCharacters = int.MaxValue;
            return;
        }

        bool revealCompleted = false;
        _revealTween = DOTween.To(
                () => passwordTxt != null ? passwordTxt.maxVisibleCharacters : 0,
                value =>
                {
                    if (passwordTxt != null)
                        passwordTxt.maxVisibleCharacters = value;
                },
            characterCount,
            0.5f)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() => revealCompleted = true);

        await UniTask.WaitUntil(() => revealCompleted, cancellationToken: token);

        if (passwordTxt != null)
            passwordTxt.maxVisibleCharacters = int.MaxValue;
    }

    private void HideNumberTexts()
    {
        if (numTxts == null || numTxts.Length == 0)
        {
            this.LogWarning("[Level68PasswordCtrl] Number text references are empty; trying fallback discovery.");
            numTxts = FindNumberTextObjects();
        }

        if (numTxts == null)
            return;

        for (int i = 0; i < numTxts.Length; i++)
        {
            if (numTxts[i] != null)
                numTxts[i].SetActive(false);
        }
    }

    private void RestorePresentation()
    {
        if (numTxts != null)
        {
            for (int i = 0; i < numTxts.Length; i++)
            {
                if (numTxts[i] != null)
                    numTxts[i].SetActive(true);
            }
        }

        if (passwordTxt != null)
        {
            if (_hasCachedInitialText)
                passwordTxt.text = _initialPasswordText;

            passwordTxt.maxVisibleCharacters = int.MaxValue;
        }
    }

    private void EnsureReferences()
    {
        if (popup == null)
            popup = GetComponent<PasswordPopup>();

        if (passwordTxt == null)
            passwordTxt = FindPasswordText();

        if ((numTxts == null || numTxts.Length == 0))
            numTxts = FindNumberTextObjects();
    }

    private TextMeshProUGUI FindPasswordText()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == "password_txt")
                return texts[i];
        }

        this.LogWarning("[Level68PasswordCtrl] Could not find password_txt fallback reference.");
        return null;
    }

    private GameObject[] FindNumberTextObjects()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null || texts.Length == 0)
            return Array.Empty<GameObject>();

        var results = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            string objectName = texts[i].name;
            if (objectName == "password_txt")
                continue;

            if (objectName.Contains("Num") || objectName.Contains("num"))
                results.Add(texts[i].gameObject);
        }

        if (results.Count == 0)
            this.LogWarning("[Level68PasswordCtrl] Could not find number text fallback references.");

        return results.ToArray();
    }

    private void OnDisable()
    {
        CancelWaitingPass(false);
    }

    private void OnDestroy()
    {
        CancelWaitingPass(false);
    }
}
