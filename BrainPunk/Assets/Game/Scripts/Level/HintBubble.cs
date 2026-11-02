using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using I2.Loc;
using TMPro;
using UnityEngine;

/// <summary>
/// 游戏内显示一个气泡和信息提示。
/// showTime 倒计时结束后自动关闭。
/// </summary>
public class HintBubble : MonoBehaviour, IDebuger
{
    private const string ShowBoolName = "show";
    private const string CloseStateName = "close";

    [SerializeField] private TextMeshPro tmpTxt;
    [SerializeField] private Animator anim;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private float closeDestroyDelayFallback = 1.25f;
    [SerializeField] private float textRevealDuration = 0.4f;
    [SerializeField] private float textScaleAnimDuration = 0.2f;
    [SerializeField] private float textScaleAnimStart = 0.9f;

    private Transform actor;
    private bool isClosed;
    private bool isClosing;
    // 关卡局部玩法可以把气泡接管为物理垫脚物。接管后停止跟随角色和自动关闭，
    // 但仍保留 HintBubble 自身的文字/语言刷新能力；显式 CloseStraightly 仍可关闭它。
    private bool physicsMode;
    // 同一实例重新显示或交给物理玩法时，使旧的延迟关闭任务失效。
    private int lifecycleVersion;
    private Vector3 defaultTextScale;
    private string currentTxtId;
    private EventSubscriber subscriber;

    private void Awake()
    {
        if (tmpTxt == null)
            tmpTxt = GetComponentInChildren<TextMeshPro>(true);

        if (anim == null)
            anim = GetComponent<Animator>();

        if (tmpTxt != null)
            defaultTextScale = tmpTxt.transform.localScale;

        AddListener();
    }

    private void LateUpdate()
    {
        if (isClosed || physicsMode)
            return;

        if (actor == null)
        {
            CloseStraightly();
            return;
        }

        transform.position = actor.position + followOffset;
    }

    /// <summary>
    /// 显示提示气泡。
    /// </summary>
    /// <param name="txtId">本地化文本 key</param>
    /// <param name="actor">跟随目标</param>
    /// <param name="showTime">显示时长</param>
    public void Show(string txtId, Transform actor, float disapprearTime = 20f)
    {
        if (isClosing)
        {
            this.LogWarning("[HintBubble] Show skipped: bubble is closing.");
            return;
        }

        if (string.IsNullOrEmpty(txtId))
        {
            this.LogWarning("[HintBubble] Show skipped: txtId is null or empty.");
            CloseStraightly();
            return;
        }

        if (actor == null)
        {
            this.LogWarning("[HintBubble] Show skipped: actor is null.");
            CloseStraightly();
            return;
        }

        if (tmpTxt == null)
        {
            this.LogWarning("[HintBubble] Show skipped: tmpTxt is null.");
            CloseStraightly();
            return;
        }

        isClosed = false;
        isClosing = false;
        physicsMode = false;
        int version = ++lifecycleVersion;
        this.actor = actor;
        currentTxtId = txtId;
        string content = LocalizationManager.GetTranslation(txtId);
        RefreshTips(string.IsNullOrEmpty(content) ? txtId : content);
        transform.position = actor.position + followOffset;

        if (anim)
            anim.SetBool(ShowBoolName, true);

        if (disapprearTime <= 0f)
            return;

        AutoCloseAsync(disapprearTime, version).Forget();
    }

    /// <summary>
    /// 将当前气泡交给局部物理玩法接管。接管后不再跟随角色，也不会被已经启动的
    /// 自动关闭等待销毁；调用 CloseStraightly 仍可显式结束当前气泡。
    /// </summary>
    public void SetPhysicsMode()
    {
        if (isClosed)
            return;

        bool wasClosing = isClosing;
        ++lifecycleVersion;
        physicsMode = true;
        actor = null;
        isClosing = false;

        // 指针可能与普通自动关闭在同一帧到达。接管物理时恢复显示状态，
        // 确保气泡继续作为玩家可移动的垫脚物保留。
        if (wasClosing && anim != null && anim.isActiveAndEnabled)
            anim.SetBool(ShowBoolName, true);
    }

    private void RefreshTips(string content)
    {
        tmpTxt.DOKill();
        tmpTxt.transform.DOKill();
        tmpTxt.maxVisibleCharacters = int.MaxValue;

        if (string.IsNullOrEmpty(content))
        {
            tmpTxt.text = string.Empty;
            tmpTxt.transform.localScale = defaultTextScale;
            return;
        }

        if (textRevealDuration <= 0f)
        {
            tmpTxt.enableAutoSizing = true;
            tmpTxt.text = content;
            tmpTxt.maxVisibleCharacters = int.MaxValue;
            tmpTxt.transform.localScale = defaultTextScale;
            return;
        }

        tmpTxt.enableAutoSizing = true;
        tmpTxt.text = content;
        tmpTxt.ForceMeshUpdate();

        int visibleCharacterCount = tmpTxt.textInfo.characterCount;
        tmpTxt.maxVisibleCharacters = 0;

        tmpTxt.transform.localScale = defaultTextScale * textScaleAnimStart;
        tmpTxt.transform.DOScale(defaultTextScale, textScaleAnimDuration)
            .SetEase(Ease.OutBack)
            .SetTarget(tmpTxt.transform);

        DOTween.To(
                () => tmpTxt.maxVisibleCharacters,
                value => tmpTxt.maxVisibleCharacters = value,
                visibleCharacterCount,
                textRevealDuration)
            .SetEase(Ease.Linear)
            .SetTarget(tmpTxt);
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<SwitchLanguageEvent>(OnSwitchLanguage);
    }

    private void OnSwitchLanguage(SwitchLanguageEvent evt)
    {
        if (isClosed || isClosing || tmpTxt == null || string.IsNullOrEmpty(currentTxtId))
            return;

        string content = LocalizationManager.GetTranslation(currentTxtId);
        RefreshTips(string.IsNullOrEmpty(content) ? currentTxtId : content);
    }

    private async UniTaskVoid AutoCloseAsync(float disappearTime, int version)
    {
        await UniTask.WaitForSeconds(disappearTime, cancellationToken: this.GetCancellationTokenOnDestroy());
        if (version == lifecycleVersion && !isClosing && !physicsMode)
            CloseStraightly();
    }

    /// <summary>
    /// 直接关闭。
    /// </summary>
    public void CloseStraightly()
    {
        if (isClosed || isClosing)
            return;

        int version = ++lifecycleVersion;
        physicsMode = false;
        isClosing = true;
        if (tmpTxt != null)
        {
            tmpTxt.DOKill();
            tmpTxt.transform.DOKill();
        }
        if (anim == null || !anim.isActiveAndEnabled)
        {
            DestroyBubble();
            return;
        }

        anim.SetBool(ShowBoolName, false);
        WaitCloseAnimAsync(version).Forget();
    }

    private async UniTaskVoid WaitCloseAnimAsync(int version)
    {
        float delay = GetCloseAnimDuration();
        if (delay > 0f)
            await UniTask.WaitForSeconds(delay, cancellationToken: this.GetCancellationTokenOnDestroy());
        if (version == lifecycleVersion && isClosing && !physicsMode)
            DestroyBubble();
    }

    private float GetCloseAnimDuration()
    {
        if (anim == null || anim.runtimeAnimatorController == null)
            return Mathf.Max(0f, closeDestroyDelayFallback);

        var clips = anim.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return Mathf.Max(0f, closeDestroyDelayFallback);

        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip != null && clip.name == CloseStateName)
                return Mathf.Max(clip.length, 0f);
        }

        return Mathf.Max(0f, closeDestroyDelayFallback);
    }

    private void DestroyBubble()
    {
        if (isClosed)
            return;

        isClosed = true;
        isClosing = false;
        physicsMode = false;
        ++lifecycleVersion;
        actor = null;
        currentTxtId = null;

        if (gameObject != null)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref subscriber);

        if (tmpTxt != null)
        {
            tmpTxt.DOKill();
            tmpTxt.transform.DOKill();
        }
    }
}
