using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>使用 106 同款 DOTweenTMPAnimator 做数字脉冲与提亮，不改整段文字或图标。</summary>
public sealed class Level141HudNumberFeedback : IDisposable
{
    /// <summary>收集数量在 TMP 文本中使用的 link 标识，必须与 Level 141 I2 模板一致。</summary>
    public const string CollectedLink = "Level141CollectedNumber";

    /// <summary>超越百分比在 TMP 文本中使用的 link 标识，必须与 Level 141 I2 模板一致。</summary>
    public const string PercentLink = "Level141PercentNumber";
    private const float Duration = 0.24f;
    private const float PeakScale = 1.15f;

    private sealed class Pulse
    {
        // 当前脉冲倍率只由 Pulse 自身和所属 HUD 反馈控制器读写。
        public float scale = 1f;
        private float initialScale = 1f;
        private float elapsed;
        private bool active;

        /// <summary>从当前倍率重新开始一次可连续衔接的数字脉冲。</summary>
        public void Play()
        {
            initialScale = scale;
            elapsed = 0f;
            active = true;
        }

        /// <summary>按本关暂停时钟推进一次数字脉冲，并返回本帧是否需要刷新 TMP 网格。</summary>
        public bool Advance(float dt)
        {
            if (!active) return false;
            elapsed += Mathf.Max(0f, dt);
            float t = Mathf.Clamp01(elapsed / Duration);
            scale = t < 0.3f
                ? Mathf.SmoothStep(initialScale, PeakScale, t / 0.3f)
                : Mathf.SmoothStep(PeakScale, 1f, (t - 0.3f) / 0.7f);
            if (t >= 1f) active = false;
            return true;
        }
    }

    private readonly TextMeshPro target;
    private readonly Pulse collected = new Pulse();
    private readonly Pulse percent = new Pulse();
    private DOTweenTMPAnimator animator;
    private bool disposed;

    /// <summary>
    /// 为指定的 Level 141 HUD 文本创建一次数字反馈控制器。
    /// 调用方必须在关卡销毁或重开时调用 <see cref="Dispose"/>，避免保留 DOTweenTMPAnimator 实例登记。
    /// </summary>
    public Level141HudNumberFeedback(TextMeshPro text)
    {
        target = text;
    }

    /// <summary>
    /// 启动本次收集造成的数量和/或百分比数字脉冲；不改变文本内容和内联南瓜图标。
    /// </summary>
    public void Play(bool collectedChanged, bool percentChanged)
    {
        if (disposed || target == null) return;
        if (collectedChanged) collected.Play();
        if (percentChanged) percent.Play();
        Apply();
    }

    /// <summary>由 Level141Ctrl 的 LateUpdate 按本关活动时钟推进反馈动画。</summary>
    public void Tick(float dt)
    {
        if (disposed || target == null || !target.isActiveAndEnabled) return;
        bool countChanged = collected.Advance(dt);
        bool percentChanged = percent.Advance(dt);
        if (!countChanged && !percentChanged) return;
        Apply();
    }

    private void Apply()
    {
        if (disposed || target == null || !target.isActiveAndEnabled) return;
        if (animator == null) animator = new DOTweenTMPAnimator(target);
        // Same Refresh / SetCharScale / SetCharColor path as Level106Ctrl's number effect.
        animator.Refresh();
        TMP_TextInfo info = target.textInfo;
        for (int i = 0; i < info.linkCount; i++)
        {
            TMP_LinkInfo link = info.linkInfo[i];
            string id = link.GetLinkID();
            if (id == CollectedLink) ApplyNumber(info, link, collected.scale);
            else if (id == PercentLink) ApplyNumber(info, link, percent.scale);
        }
    }

    private void ApplyNumber(TMP_TextInfo info, TMP_LinkInfo link, float scale)
    {
        int first = Mathf.Max(0, link.linkTextfirstCharacterIndex);
        int end = Mathf.Min(info.characterCount, first + link.linkTextLength);
        float pulse = Mathf.Clamp01((scale - 1f) / (PeakScale - 1f));
        Color32 baseColor = target.color;
        Color32 color = Color32.Lerp(baseColor, new Color32(255, 255, 255, 255), pulse);
        for (int i = first; i < end; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible || character.elementType != TMP_TextElementType.Character ||
                !char.IsDigit(character.character)) continue;
            animator.SetCharScale(i, Vector3.one * scale);
            animator.SetCharColor(i, color);
        }
    }

    /// <summary>
    /// 释放 TMP 动画实例登记；该方法可重复调用，重复释放不会执行额外清理。
    /// </summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (animator != null) DOTweenTMPAnimator.DisposeInstanceFor(target);
        animator = null;
    }
}
