using System.Collections;
using Bear.EventSystem;
using Bear.UI;
using DG.Tweening;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;

/// <summary>
/// Level100-private password setup and mirror decoy behavior.
/// </summary>
public sealed class Level100Ctrl : MonoBehaviour
{
    private const string PasswordPopupName = "PasswordPopup_100_2679";

    [Header("Password decoy")]
    [SerializeField] private RectTransform passwordClue;

    [Header("Mirror decoy")]
    [SerializeField] private SpriteRenderer[] mirrorImageResults;
    [SerializeField] private RectTransform mirrorDigitRoot;
    [SerializeField] private Transform[] mirrorStarRoots;
    [SerializeField] private Transform mirrorClickArea;
    [SerializeField] private Vector2 mirrorClickSize = new Vector2(2.5f, 3.99f);
    [SerializeField, Range(0f, 1f)] private float mirrorStarProbability = 0.2f;
    [SerializeField, Range(0f, 1f)] private float mirrorDigitProbability = 0.4f;
    [SerializeField, Min(0.01f)] private float mirrorFadeOutDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float mirrorFadeInDuration = 0.32f;

    private EventSubscriber _subscriber;
    private Level6GamePanelFeature _panelFeature;
    private Coroutine _panelSetupCoroutine;
    private Coroutine _pauseCheckCoroutine;
    private bool _passwordSolved;

    private TMP_Text _mirrorDigitText;
    private float[] _mirrorImageTargetAlphas;
    private float _mirrorDigitTargetAlpha = 1f;
    private MirrorStarRuntime[] _mirrorStars;
    private bool _mirrorCached;
    private bool _mirrorTransitioning;
    private bool _mirrorClickPending;
    private bool _hasCurrentMirrorChoice;
    private MirrorChoice _currentMirrorChoice;
    private bool _hasLastMirrorChoice;
    private MirrorChoice _lastMirrorChoice;
    private Sequence _mirrorSequence;

    private enum MirrorChoiceType
    {
        Image,
        Digit,
        Star,
    }

    private struct MirrorChoice
    {
        public MirrorChoiceType Type;
        public int ImageIndex;
        public int Digit;
        public int StarIndex;
    }

    private sealed class MirrorStarRuntime
    {
        public Transform Root;
        public Postcard_Star Star;
        public Collider2D Collider;
        public SpriteRenderer[] Renderers;
        public float[] TargetAlphas;
    }

    #region Unity lifecycle

    private void OnEnable()
    {
        ResetMirrorState();
        SetPasswordClueVisible(false);
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        _subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        _subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
        _subscriber.Subscribe<OnTiggerItemEvent>(OnTiggerItem);

        _panelSetupCoroutine = StartCoroutine(ConfigurePasswordPopupWhenReady());
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
        SetPasswordClueVisible(false);
    }

    private void OnDestroy() => CleanupRuntimeState();

    private void Update()
    {
        if (!Application.isPlaying || !CanProcessMirrorInput())
            return;

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began
                    && !InputUtils.IsPointerOverUI(touch.fingerId)
                    && TryClickMirror(touch.position))
                {
                    return;
                }
            }

            return;
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)
            && !InputUtils.IsPointerOverUI((Vector2)Input.mousePosition))
        {
            TryClickMirror(Input.mousePosition);
        }
#endif
    }

    private void OnValidate()
    {
        mirrorClickSize.x = Mathf.Max(0.01f, mirrorClickSize.x);
        mirrorClickSize.y = Mathf.Max(0.01f, mirrorClickSize.y);
        mirrorStarProbability = Mathf.Clamp01(mirrorStarProbability);
        mirrorDigitProbability = Mathf.Clamp01(mirrorDigitProbability);
        mirrorFadeOutDuration = Mathf.Max(0.01f, mirrorFadeOutDuration);
        mirrorFadeInDuration = Mathf.Max(0.01f, mirrorFadeInDuration);
    }

    private void CleanupRuntimeState()
    {
        CancelPendingCoroutines();
        SetAllMirrorStarsCollectible(false);
        KillMirrorSequence();
        EventsUtils.ResetEvents(ref _subscriber);
        _panelFeature = null;
    }

    #endregion

    #region Password popup

    private IEnumerator ConfigurePasswordPopupWhenReady()
    {
        // PlayCtrl replaces the previous HUD after creating the level instance.
        yield return null;

        while (isActiveAndEnabled
               && (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null))
        {
            yield return null;
        }

        _panelSetupCoroutine = null;
        if (!isActiveAndEnabled || PlayCtrl.Instance == null)
            yield break;

        _panelFeature = PlayCtrl.Instance.CurrentGamePlayPanel
            .GetComponent<Level6GamePanelFeature>();
        if (_panelFeature == null)
        {
            Debug.LogWarning("[Level100Ctrl] Level6GamePanelFeature is missing from GamePlayPanel_001.");
            yield break;
        }

        _panelFeature.ConfigurePasswordPopup(PasswordPopupName);
        ApplyPasswordEntryState();
    }

    private void OnTiggerItem(OnTiggerItemEvent evt)
    {
        if (evt == null || evt.EventId != 1 || _passwordSolved)
            return;

        _passwordSolved = true;
        ApplyPasswordEntryState();
    }

    private void ApplyPasswordEntryState()
    {
        CustomButton useButton = _panelFeature != null ? _panelFeature.Use_btn : null;
        if (useButton == null)
            return;

        useButton.IsBlockClick = _passwordSolved;
        useButton.Interactable = !_passwordSolved;
        if (_passwordSolved)
            useButton.gameObject.SetActive(false);
    }

    #endregion

    #region Mirror decoy

    private void OnMirrorClicked()
    {
        if (!Application.isPlaying || !CanProcessMirrorInput())
            return;

        if (_mirrorTransitioning)
        {
            _mirrorClickPending = true;
            return;
        }

        EnsureMirrorCached();
        NormalizeCollectedMirrorStarState();
        if (!TryRollMirrorChoice(out MirrorChoice nextChoice))
            return;

        if (!_hasCurrentMirrorChoice)
        {
            RevealFirstMirrorChoice(nextChoice);
            return;
        }

        ReplaceMirrorChoice(nextChoice);
    }

    private bool CanProcessMirrorInput()
    {
        return mirrorClickArea != null
               && mirrorClickArea.gameObject.activeInHierarchy
               && PlayCtrl.Instance != null
               && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }

    private bool TryClickMirror(Vector2 screenPosition)
    {
        Camera inputCamera = Camera.main;
        if (inputCamera == null)
            return false;

        float zDistance = Mathf.Abs(inputCamera.transform.position.z - mirrorClickArea.position.z);
        Vector3 worldPosition = inputCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, zDistance));
        Vector3 localPosition = mirrorClickArea.InverseTransformPoint(worldPosition);
        Vector2 halfSize = mirrorClickSize * 0.5f;
        if (Mathf.Abs(localPosition.x) > halfSize.x
            || Mathf.Abs(localPosition.y) > halfSize.y)
        {
            return false;
        }

        // The original draggable password blocks overlap the top of the mirror.
        // Keep their interaction priority without adding a collider that can steal their pick ray.
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].GetComponentInParent<DragableItem>() != null)
                return false;
        }

        OnMirrorClicked();
        return true;
    }

    private void EnsureMirrorCached()
    {
        if (_mirrorCached)
            return;

        int imageCount = mirrorImageResults != null ? mirrorImageResults.Length : 0;
        _mirrorImageTargetAlphas = new float[imageCount];
        for (int i = 0; i < imageCount; i++)
        {
            SpriteRenderer renderer = mirrorImageResults[i];
            _mirrorImageTargetAlphas[i] = renderer != null ? renderer.color.a : 1f;
        }

        if (mirrorDigitRoot != null)
            _mirrorDigitText = mirrorDigitRoot.GetComponentInChildren<TMP_Text>(true);
        if (_mirrorDigitText != null)
            _mirrorDigitTargetAlpha = _mirrorDigitText.color.a;

        int starCount = mirrorStarRoots != null ? mirrorStarRoots.Length : 0;
        _mirrorStars = new MirrorStarRuntime[starCount];
        for (int i = 0; i < starCount; i++)
        {
            Transform root = mirrorStarRoots[i];
            if (root == null)
                continue;

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            MirrorStarRuntime runtime = new MirrorStarRuntime
            {
                Root = root,
                Star = root.GetComponent<Postcard_Star>(),
                Collider = root.GetComponent<Collider2D>(),
                Renderers = renderers,
                TargetAlphas = new float[renderers.Length],
            };

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SpriteRenderer renderer = renderers[rendererIndex];
                runtime.TargetAlphas[rendererIndex] = renderer != null ? renderer.color.a : 1f;
            }

            _mirrorStars[i] = runtime;
        }

        _mirrorCached = true;
    }

    private void ResetMirrorState()
    {
        EnsureMirrorCached();
        KillMirrorSequence();

        if (mirrorImageResults != null)
        {
            for (int i = 0; i < mirrorImageResults.Length; i++)
            {
                SpriteRenderer renderer = mirrorImageResults[i];
                if (renderer == null)
                    continue;

                SetSpriteAlpha(renderer, GetImageTargetAlpha(i));
                renderer.gameObject.SetActive(false);
            }
        }

        if (_mirrorDigitText != null)
            SetTextAlpha(_mirrorDigitText, _mirrorDigitTargetAlpha);
        if (mirrorDigitRoot != null)
            mirrorDigitRoot.gameObject.SetActive(false);

        if (_mirrorStars != null)
        {
            for (int i = 0; i < _mirrorStars.Length; i++)
            {
                RestoreMirrorStarAlpha(i);
                SetMirrorStarCollectible(i, false);
                if (_mirrorStars[i]?.Root != null)
                    _mirrorStars[i].Root.gameObject.SetActive(false);
            }
        }

        _mirrorTransitioning = false;
        _mirrorClickPending = false;
        _hasCurrentMirrorChoice = false;
        _currentMirrorChoice = new MirrorChoice();
        _hasLastMirrorChoice = false;
        _lastMirrorChoice = new MirrorChoice();
    }

    private bool TryRollMirrorChoice(out MirrorChoice choice)
    {
        int eligibleImageCount = CountEligibleMirrorImages();
        int availableStarCount = CountAvailableMirrorStars();
        bool canShowDigit = _mirrorDigitText != null && mirrorDigitRoot != null;
        bool canShowStar = availableStarCount > 0;
        bool starIsEligible = canShowStar
                              && (!_hasLastMirrorChoice
                                  || _lastMirrorChoice.Type != MirrorChoiceType.Star);

        float starWeight = starIsEligible ? Mathf.Clamp01(mirrorStarProbability) : 0f;
        float digitWeight = canShowDigit
            ? Mathf.Min(Mathf.Clamp01(mirrorDigitProbability), 1f - starWeight)
            : 0f;
        float imageWeight = eligibleImageCount > 0
            ? Mathf.Max(0f, 1f - starWeight - digitWeight)
            : 0f;
        float totalWeight = starWeight + digitWeight + imageWeight;

        if (totalWeight <= Mathf.Epsilon)
        {
            choice = new MirrorChoice();
            return false;
        }

        float roll = Random.value * totalWeight;
        if (starWeight > Mathf.Epsilon && roll <= starWeight)
            return TryCreateStarChoice(out choice);

        roll -= starWeight;
        if (digitWeight > Mathf.Epsilon && roll <= digitWeight)
        {
            choice = CreateDigitChoice();
            return true;
        }

        if (imageWeight > Mathf.Epsilon)
            return TryCreateRandomImageChoice(eligibleImageCount, out choice);

        if (digitWeight > Mathf.Epsilon)
        {
            choice = CreateDigitChoice();
            return true;
        }

        return TryCreateStarChoice(out choice);
    }

    private bool TryCreateStarChoice(out MirrorChoice choice)
    {
        int selectedStarIndex = -1;
        int availableCount = 0;
        if (_mirrorStars != null)
        {
            for (int i = 0; i < _mirrorStars.Length; i++)
            {
                if (!RefreshMirrorStarAvailability(i))
                    continue;

                availableCount++;
                if (Random.Range(0, availableCount) == 0)
                    selectedStarIndex = i;
            }
        }

        if (selectedStarIndex < 0)
        {
            choice = new MirrorChoice();
            return false;
        }

        choice = new MirrorChoice
        {
            Type = MirrorChoiceType.Star,
            ImageIndex = -1,
            Digit = -1,
            StarIndex = selectedStarIndex,
        };
        return true;
    }

    private MirrorChoice CreateDigitChoice()
    {
        int digit = Random.Range(0, _hasLastMirrorChoice
            && _lastMirrorChoice.Type == MirrorChoiceType.Digit ? 9 : 10);
        if (_hasLastMirrorChoice
            && _lastMirrorChoice.Type == MirrorChoiceType.Digit
            && digit >= _lastMirrorChoice.Digit)
        {
            digit++;
        }

        return new MirrorChoice
        {
            Type = MirrorChoiceType.Digit,
            ImageIndex = -1,
            Digit = digit,
            StarIndex = -1,
        };
    }

    private bool TryCreateRandomImageChoice(int eligibleImageCount, out MirrorChoice choice)
    {
        int validSelection = Random.Range(0, eligibleImageCount);
        for (int i = 0; i < mirrorImageResults.Length; i++)
        {
            if (!IsEligibleMirrorImage(i))
                continue;

            if (validSelection-- != 0)
                continue;

            choice = new MirrorChoice
            {
                Type = MirrorChoiceType.Image,
                ImageIndex = i,
                Digit = -1,
                StarIndex = -1,
            };
            return true;
        }

        choice = new MirrorChoice();
        return false;
    }

    private int CountEligibleMirrorImages()
    {
        if (mirrorImageResults == null)
            return 0;

        int count = 0;
        for (int i = 0; i < mirrorImageResults.Length; i++)
        {
            if (!IsEligibleMirrorImage(i))
                continue;

            count++;
        }

        return count;
    }

    private bool IsEligibleMirrorImage(int index)
    {
        return IsValidImageIndex(index)
               && (!_hasLastMirrorChoice
                   || _lastMirrorChoice.Type != MirrorChoiceType.Image
                   || _lastMirrorChoice.ImageIndex != index);
    }

    private int CountAvailableMirrorStars()
    {
        if (_mirrorStars == null)
            return 0;

        int count = 0;
        for (int i = 0; i < _mirrorStars.Length; i++)
        {
            if (RefreshMirrorStarAvailability(i))
                count++;
        }

        return count;
    }

    private void RevealFirstMirrorChoice(MirrorChoice choice)
    {
        _mirrorTransitioning = true;
        ApplyMirrorChoice(choice);

        Sequence sequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _mirrorSequence = sequence;
        sequence.Append(CreateChoiceFade(choice, GetChoiceTargetAlpha(choice), mirrorFadeInDuration));
        BindMirrorSequenceCompletion(sequence);
    }

    private void ReplaceMirrorChoice(MirrorChoice choice)
    {
        _mirrorTransitioning = true;
        if (_currentMirrorChoice.Type == MirrorChoiceType.Star)
            SetMirrorStarCollectible(_currentMirrorChoice.StarIndex, false);

        Tween fadeOut = CreateCurrentChoiceFade(0f, mirrorFadeOutDuration);
        Tween fadeIn = CreateChoiceFade(choice, GetChoiceTargetAlpha(choice), mirrorFadeInDuration);

        Sequence sequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _mirrorSequence = sequence;
        sequence.Append(fadeOut);
        sequence.AppendCallback(HideCurrentMirrorChoice);
        sequence.AppendCallback(() => ApplyMirrorChoice(choice));
        sequence.Append(fadeIn);
        BindMirrorSequenceCompletion(sequence);
    }

    private void BindMirrorSequenceCompletion(Sequence sequence)
    {
        sequence.OnComplete(() =>
        {
            if (_mirrorSequence != sequence)
                return;

            _mirrorSequence = null;
            _mirrorTransitioning = false;
            CommitCurrentMirrorChoiceAsLast();

            if (!_mirrorClickPending)
            {
                RestoreCurrentMirrorStarCollectionIfReady();
                return;
            }

            _mirrorClickPending = false;
            OnMirrorClicked();
            RestoreCurrentMirrorStarCollectionIfReady();
        });
    }

    private void ApplyMirrorChoice(MirrorChoice choice)
    {
        if (choice.Type == MirrorChoiceType.Digit)
        {
            if (_mirrorDigitText == null || mirrorDigitRoot == null)
                return;

            _mirrorDigitText.text = choice.Digit.ToString();
            SetTextAlpha(_mirrorDigitText, 0f);
            mirrorDigitRoot.gameObject.SetActive(true);
        }
        else if (choice.Type == MirrorChoiceType.Star)
        {
            if (!RefreshMirrorStarAvailability(choice.StarIndex))
                return;

            SetMirrorStarAlpha(choice.StarIndex, 0f);
            _mirrorStars[choice.StarIndex].Root.gameObject.SetActive(true);
            SetMirrorStarCollectible(choice.StarIndex, false);
        }
        else
        {
            if (!IsValidImageIndex(choice.ImageIndex))
                return;

            SpriteRenderer renderer = mirrorImageResults[choice.ImageIndex];
            SetSpriteAlpha(renderer, 0f);
            renderer.gameObject.SetActive(true);
        }

        _currentMirrorChoice = choice;
        _hasCurrentMirrorChoice = true;
    }

    private void HideCurrentMirrorChoice()
    {
        if (!_hasCurrentMirrorChoice)
            return;

        if (_currentMirrorChoice.Type == MirrorChoiceType.Digit)
        {
            if (mirrorDigitRoot != null)
                mirrorDigitRoot.gameObject.SetActive(false);
        }
        else if (_currentMirrorChoice.Type == MirrorChoiceType.Star)
        {
            int starIndex = _currentMirrorChoice.StarIndex;
            SetMirrorStarCollectible(starIndex, false);
            if (IsValidStarIndex(starIndex))
                _mirrorStars[starIndex].Root.gameObject.SetActive(false);
        }
        else if (IsValidImageIndex(_currentMirrorChoice.ImageIndex))
        {
            mirrorImageResults[_currentMirrorChoice.ImageIndex].gameObject.SetActive(false);
        }

        _hasCurrentMirrorChoice = false;
    }

    private Tween CreateCurrentChoiceFade(float targetAlpha, float duration)
    {
        if (_currentMirrorChoice.Type == MirrorChoiceType.Digit)
            return _mirrorDigitText.DOFade(targetAlpha, Mathf.Max(0.01f, duration));

        if (_currentMirrorChoice.Type == MirrorChoiceType.Star)
        {
            int starIndex = _currentMirrorChoice.StarIndex;
            return DOVirtual.Float(
                GetMirrorStarCurrentAlpha(starIndex),
                targetAlpha,
                Mathf.Max(0.01f, duration),
                alpha => SetMirrorStarAlpha(starIndex, alpha));
        }

        return mirrorImageResults[_currentMirrorChoice.ImageIndex]
            .DOFade(targetAlpha, Mathf.Max(0.01f, duration));
    }

    private Tween CreateChoiceFade(MirrorChoice choice, float targetAlpha, float duration)
    {
        return DOVirtual.Float(
            0f,
            targetAlpha,
            Mathf.Max(0.01f, duration),
            alpha => SetChoiceAlpha(choice, alpha));
    }

    private void SetChoiceAlpha(MirrorChoice choice, float alpha)
    {
        if (choice.Type == MirrorChoiceType.Digit)
        {
            SetTextAlpha(_mirrorDigitText, alpha);
            return;
        }

        if (choice.Type == MirrorChoiceType.Star)
        {
            SetMirrorStarAlpha(choice.StarIndex, alpha);
            return;
        }

        SetSpriteAlpha(mirrorImageResults[choice.ImageIndex], alpha);
    }

    private float GetChoiceTargetAlpha(MirrorChoice choice)
    {
        if (choice.Type == MirrorChoiceType.Digit)
            return _mirrorDigitTargetAlpha;
        if (choice.Type == MirrorChoiceType.Star)
            return GetMirrorStarTargetAlpha(choice.StarIndex);
        return GetImageTargetAlpha(choice.ImageIndex);
    }

    private void CommitCurrentMirrorChoiceAsLast()
    {
        if (!_hasCurrentMirrorChoice)
            return;

        _lastMirrorChoice = _currentMirrorChoice;
        _hasLastMirrorChoice = true;
    }

    private void NormalizeCollectedMirrorStarState()
    {
        if (!_hasCurrentMirrorChoice
            || _currentMirrorChoice.Type != MirrorChoiceType.Star)
        {
            return;
        }

        int starIndex = _currentMirrorChoice.StarIndex;
        if (IsValidStarIndex(starIndex)
            && _mirrorStars[starIndex].Root.gameObject.activeInHierarchy)
        {
            return;
        }

        _hasCurrentMirrorChoice = false;
        _currentMirrorChoice = new MirrorChoice();
    }

    private void EnableCurrentMirrorStarCollection()
    {
        if (!_hasCurrentMirrorChoice
            || _currentMirrorChoice.Type != MirrorChoiceType.Star)
        {
            return;
        }

        int starIndex = _currentMirrorChoice.StarIndex;
        if (!IsValidStarIndex(starIndex)
            || !_mirrorStars[starIndex].Root.gameObject.activeInHierarchy
            || !RefreshMirrorStarAvailability(starIndex))
        {
            return;
        }

        SetMirrorStarCollectible(starIndex, true);
    }

    private void RestoreCurrentMirrorStarCollectionIfReady()
    {
        if (_mirrorTransitioning
            || PlayCtrl.Instance == null
            || !PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING))
        {
            return;
        }

        EnableCurrentMirrorStarCollection();
    }

    private bool RefreshMirrorStarAvailability(int starIndex)
    {
        if (!Application.isPlaying || !IsValidStarIndex(starIndex))
            return false;

        MirrorStarRuntime runtime = _mirrorStars[starIndex];
        bool isAvailable = runtime.Star.RefreshSavedCollectionState();
        SetMirrorStarCollectible(starIndex, false);
        return isAvailable;
    }

    private void SetMirrorStarCollectible(int starIndex, bool collectible)
    {
        if (!IsValidStarIndex(starIndex))
            return;

        MirrorStarRuntime runtime = _mirrorStars[starIndex];
        if (runtime.Collider != null)
        {
            runtime.Collider.enabled = collectible
                                       && runtime.Root.gameObject.activeInHierarchy;
        }
    }

    private void SetAllMirrorStarsCollectible(bool collectible)
    {
        if (_mirrorStars == null)
            return;

        for (int i = 0; i < _mirrorStars.Length; i++)
            SetMirrorStarCollectible(i, collectible);
    }

    private void RestoreMirrorStarAlpha(int starIndex)
    {
        if (!IsValidStarIndex(starIndex))
            return;

        MirrorStarRuntime runtime = _mirrorStars[starIndex];
        for (int i = 0; i < runtime.Renderers.Length; i++)
        {
            SpriteRenderer renderer = runtime.Renderers[i];
            if (renderer == null)
                continue;

            float targetAlpha = runtime.TargetAlphas != null && i < runtime.TargetAlphas.Length
                ? runtime.TargetAlphas[i]
                : 1f;
            SetSpriteAlpha(renderer, targetAlpha);
        }
    }

    private void SetMirrorStarAlpha(int starIndex, float alpha)
    {
        if (!IsValidStarIndex(starIndex))
            return;

        SpriteRenderer[] renderers = _mirrorStars[starIndex].Renderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                SetSpriteAlpha(renderers[i], alpha);
        }
    }

    private float GetMirrorStarCurrentAlpha(int starIndex)
    {
        if (!IsValidStarIndex(starIndex))
            return 0f;

        SpriteRenderer[] renderers = _mirrorStars[starIndex].Renderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                return renderers[i].color.a;
        }

        return 0f;
    }

    private float GetMirrorStarTargetAlpha(int starIndex)
    {
        if (!IsValidStarIndex(starIndex)
            || _mirrorStars[starIndex].TargetAlphas == null
            || _mirrorStars[starIndex].TargetAlphas.Length == 0)
        {
            return 1f;
        }

        return _mirrorStars[starIndex].TargetAlphas[0];
    }

    private float GetImageTargetAlpha(int index)
    {
        if (_mirrorImageTargetAlphas == null
            || index < 0
            || index >= _mirrorImageTargetAlphas.Length)
        {
            return 1f;
        }

        return _mirrorImageTargetAlphas[index];
    }

    private bool IsValidImageIndex(int index)
    {
        return mirrorImageResults != null
               && index >= 0
               && index < mirrorImageResults.Length
               && mirrorImageResults[index] != null;
    }

    private bool IsValidStarIndex(int index)
    {
        return _mirrorStars != null
               && index >= 0
               && index < _mirrorStars.Length
               && _mirrorStars[index] != null
               && _mirrorStars[index].Root != null
               && _mirrorStars[index].Star != null;
    }

    private void KillMirrorSequence()
    {
        if (_mirrorSequence != null)
        {
            _mirrorSequence.Kill(false);
            _mirrorSequence = null;
        }

        _mirrorTransitioning = false;
        _mirrorClickPending = false;
    }

    private static void SetSpriteAlpha(SpriteRenderer renderer, float alpha)
    {
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }

    private static void SetTextAlpha(TMP_Text text, float alpha)
    {
        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }

    #endregion

    #region Pause clue

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt.NewState == GamePlayStateName.PAUSE)
        {
            PauseMirrorTransition();
            return;
        }

        CancelPauseCheck();
        SetPasswordClueVisible(false);
        if (evt.NewState == GamePlayStateName.PLAYING)
            ResumeMirrorTransition();
        else
            PauseMirrorTransition();
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        CancelPauseCheck();
        PauseMirrorTransition();
        _pauseCheckCoroutine = StartCoroutine(RevealClueAfterSettingsPopupCreated());
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        CancelPauseCheck();
        SetPasswordClueVisible(false);
        ResumeMirrorTransition();
    }

    private void PauseMirrorTransition()
    {
        if (_mirrorSequence != null && _mirrorSequence.IsActive())
            _mirrorSequence.Pause();
        SetAllMirrorStarsCollectible(false);
    }

    private void ResumeMirrorTransition()
    {
        if (_mirrorSequence != null && _mirrorSequence.IsActive())
            _mirrorSequence.Play();
        else
            EnableCurrentMirrorStarCollection();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        CancelPauseCheck();
        SetPasswordClueVisible(false);
        ResetMirrorState();
    }

    private IEnumerator RevealClueAfterSettingsPopupCreated()
    {
        yield return null;
        _pauseCheckCoroutine = null;

        if (!isActiveAndEnabled || PlayCtrl.Instance == null || UIManager.Instance == null)
            yield break;

        GameSettingPopup settingPopup = UIManager.Instance
            .GetUI<GameSettingPopup>(nameof(GameSettingPopup));
        bool isSettingsPause = PlayCtrl.Instance.CheckState(GamePlayStateName.PAUSE)
                               && settingPopup != null
                               && settingPopup.IsVisible;
        SetPasswordClueVisible(isSettingsPause);
    }

    private void SetPasswordClueVisible(bool isVisible)
    {
        if (passwordClue != null && passwordClue.gameObject.activeSelf != isVisible)
            passwordClue.gameObject.SetActive(isVisible);
    }

    #endregion

    #region Cleanup

    private void CancelPendingCoroutines()
    {
        if (_panelSetupCoroutine != null)
        {
            StopCoroutine(_panelSetupCoroutine);
            _panelSetupCoroutine = null;
        }

        CancelPauseCheck();
    }

    private void CancelPauseCheck()
    {
        if (_pauseCheckCoroutine == null)
            return;

        StopCoroutine(_pauseCheckCoroutine);
        _pauseCheckCoroutine = null;
    }

    #endregion
}
