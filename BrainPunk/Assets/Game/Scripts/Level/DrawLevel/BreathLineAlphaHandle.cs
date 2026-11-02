using DG.Tweening;
using UnityEngine;

/// <summary>
/// 控制 DrawTipsLineObjectRenderer 的透明度进行循环渐隐渐显。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DrawTipsLineObjectRenderer))]
[AddComponentMenu("Game/Draw/Breath Line Alpha Handle")]
public sealed class BreathLineAlphaHandle : MonoBehaviour
{
    #region Inspector

    [Header("Target")]
    [Tooltip("需要控制透明度的提示线；为空时使用当前节点上的 DrawTipsLineObjectRenderer。")]
    [SerializeField] private DrawTipsLineObjectRenderer targetLine;

    [Header("Alpha")]
    [Tooltip("呼吸渐隐阶段到达的最低透明度。")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.2f;

    [Tooltip("呼吸渐显阶段到达的最高透明度。")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;

    [Tooltip("从最高透明度变化到最低透明度的单程时长，单位为秒。")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 1f;

    [Tooltip("透明度单程变化使用的缓动类型。")]
    [SerializeField] private Ease easeType = Ease.InOutSine;

    [Header("Playback")]
    [Tooltip("组件运行时启用后是否自动开始透明度呼吸。")]
    [SerializeField] private bool playOnEnable = true;

    [Tooltip("是否忽略 Time.timeScale；暂停界面仍需呼吸时开启。")]
    [SerializeField] private bool useUnscaledTime;

    [Tooltip("停止呼吸或组件关闭时，是否恢复到最高透明度。")]
    [SerializeField] private bool restoreMaxAlphaOnStop = true;

    #endregion

    #region Runtime State

    private Tweener alphaTweener;

    #endregion

    #region Unity Messages

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveTarget();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || !playOnEnable)
            return;

        StartBreath();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        StopBreath();
    }

    private void OnDestroy()
    {
        KillTween();
    }

    private void Reset()
    {
        targetLine = GetComponent<DrawTipsLineObjectRenderer>();
        NormalizeInspectorData();
    }

    private void OnValidate()
    {
        NormalizeInspectorData();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 从最高透明度开始循环渐隐渐显。
    /// 调用边界：仅在 PlayMode 生效；重复调用会先释放旧 Tween。
    /// </summary>
    public void StartBreath()
    {
        if (!Application.isPlaying)
            return;

        ResolveTarget();
        if (targetLine == null)
            return;

        KillTween();
        targetLine.SetAlpha(maxAlpha);
        alphaTweener = DOVirtual.Float(maxAlpha, minAlpha, fadeDuration, targetLine.SetAlpha)
            .SetEase(easeType)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(useUnscaledTime);
    }

    /// <summary>
    /// 暂停当前透明度呼吸并保持当前透明度。
    /// 调用边界：仅影响由当前组件创建且仍有效的 Tween。
    /// </summary>
    public void PauseBreath()
    {
        if (alphaTweener != null && alphaTweener.IsActive())
            alphaTweener.Pause();
    }

    /// <summary>
    /// 继续当前已暂停的透明度呼吸。
    /// 调用边界：Tween 已停止或销毁时不会重新创建，应调用 StartBreath。
    /// </summary>
    public void ResumeBreath()
    {
        if (alphaTweener != null && alphaTweener.IsActive())
            alphaTweener.Play();
    }

    /// <summary>
    /// 停止透明度呼吸；根据 Inspector 设置决定是否恢复最高透明度。
    /// 调用边界：可由关卡演出或组件关闭流程重复调用。
    /// </summary>
    public void StopBreath()
    {
        KillTween();

        if (restoreMaxAlphaOnStop && targetLine != null)
            targetLine.SetAlpha(maxAlpha);
    }

    #endregion

    #region Internal

    private void ResolveTarget()
    {
        if (targetLine == null)
            targetLine = GetComponent<DrawTipsLineObjectRenderer>();
    }

    private void KillTween()
    {
        if (alphaTweener != null && alphaTweener.IsActive())
            alphaTweener.Kill();

        alphaTweener = null;
    }

    private void NormalizeInspectorData()
    {
        minAlpha = Mathf.Clamp01(minAlpha);
        maxAlpha = Mathf.Clamp01(maxAlpha);
        if (minAlpha > maxAlpha)
            minAlpha = maxAlpha;

        fadeDuration = Mathf.Max(0.01f, fadeDuration);
    }

    #endregion
}
