using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// DLevel_026 的单套猫、夹子与球控制器。
/// 复用成熟的单装置流程：开场保持夹紧，第一笔有效画线完成后打开夹子并释放球，
/// 重开时恢复夹紧、球的挂载关系和初始物理状态。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 26 Ctrl")]
public sealed class DLevel26Ctrl : DLevel2Ctrl,
    IDrawLevelVisibleIntroHandler
{
    /// <summary>
    /// D026 开场动画的最长门禁时间。
    /// 使用非缩放时间兜底，避免菜单进关时 Tween 未完成导致画线永久关闭。
    /// </summary>
    private static readonly TimeSpan IntroTimeout = TimeSpan.FromSeconds(2d);

    /// <summary>
    /// 播放既有单装置入场；若两秒内没有完成，则终止动画并直接套用可玩完成态。
    /// </summary>
    async UniTask IDrawLevelIntroHandler.PlayDrawIntroAsync()
    {
        bool didTimeout = await base.PlayDrawIntroAsync()
            .TimeoutWithoutException(IntroTimeout, DelayType.UnscaledDeltaTime);
        if (!didTimeout)
            return;

        Debug.LogWarning(
            "[DLevel26Ctrl] Intro 超时，已直接恢复可玩完成态并解除画线门禁。",
            this);
        base.ApplyDrawRetryIntroState();
    }

    /// <summary>重开时沿用既有单装置完成态，不重播入场动画。</summary>
    void IDrawLevelRetryIntroStateHandler.ApplyDrawRetryIntroState()
    {
        base.ApplyDrawRetryIntroState();
    }
}
