using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// DLevel_039 的猫夹球与石头同步释放控制器。
/// 复用 DLevel_002 的可见入场和开夹放球流程；第一笔有效画线完成后，
/// 同时把右上方石头从等待态切换为 Dynamic。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 39 Ctrl")]
public sealed class DLevel39Ctrl : DLevel2Ctrl,
    IDrawLevelVisibleIntroHandler,
    IDrawStrokeCompletedHandler
{
    #region Constants

    private static readonly TimeSpan IntroTimeout = TimeSpan.FromSeconds(2d);

    #endregion

    #region Inspector

    /// <summary>
    /// 第一笔有效画线完成后释放的右上方石头刚体。
    /// 绑定边界：必须指向本关 Stone；等待和重置阶段由本控制器保持 Kinematic。
    /// </summary>
    [Header("石头机关")]
    [Tooltip("第一笔有效画线完成后切换为 Dynamic 的 Stone Rigidbody2D。")]
    [SerializeField] private Rigidbody2D stoneRigidbody;

    #endregion

    #region Runtime State

    private Vector2 _initialStonePosition;
    private float _initialStoneRotation;
    private bool _hasStoneInitialState;
    private bool _hasReleasedStone;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        if (!Application.isPlaying)
            return;

        if (stoneRigidbody == null)
        {
            Debug.LogError(
                "[DLevel39Ctrl] Stone Rigidbody2D 未绑定；开夹放球保留，石头释放停用。",
                this);
            return;
        }

        _initialStonePosition = stoneRigidbody.position;
        _initialStoneRotation = stoneRigidbody.rotation;
        _hasStoneInitialState = true;
        RestoreStoneWaitingState();
    }

    #endregion

    #region Draw Level Handlers

    /// <summary>
    /// 播放既有单装置入场；两秒内未完成时直接应用可玩完成态，避免画线门禁卡死。
    /// </summary>
    async UniTask IDrawLevelIntroHandler.PlayDrawIntroAsync()
    {
        bool didTimeout = await base.PlayDrawIntroAsync()
            .TimeoutWithoutException(IntroTimeout, DelayType.UnscaledDeltaTime);
        if (!didTimeout)
            return;

        Debug.LogWarning(
            "[DLevel39Ctrl] Intro 超时，已直接恢复可玩完成态并解除画线门禁。",
            this);
        base.ApplyDrawRetryIntroState();
    }

    /// <summary>重开时沿用单装置完成态，不重播入场动画。</summary>
    void IDrawLevelRetryIntroStateHandler.ApplyDrawRetryIntroState()
    {
        base.ApplyDrawRetryIntroState();
    }

    /// <summary>第一笔有效画线完成后，同步打开夹子、释放球并激活石头物理。</summary>
    void IDrawStrokeCompletedHandler.OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke)
            return;

        base.OnDrawStrokeCompleted(result);
        ReleaseStone();
    }

    /// <summary>清线、失败重开或主动重置时恢复夹持球和石头等待态。</summary>
    void IDrawStrokeCompletedHandler.OnDrawAttemptReset()
    {
        base.OnDrawAttemptReset();
        RestoreStoneWaitingState();
    }

    #endregion

    #region Stone Physics

    private void ReleaseStone()
    {
        if (!_hasStoneInitialState || _hasReleasedStone)
            return;

        _hasReleasedStone = true;
        stoneRigidbody.bodyType = RigidbodyType2D.Dynamic;
        stoneRigidbody.linearVelocity = Vector2.zero;
        stoneRigidbody.angularVelocity = 0f;
        stoneRigidbody.WakeUp();
    }

    private void RestoreStoneWaitingState()
    {
        if (!_hasStoneInitialState)
            return;

        _hasReleasedStone = false;
        stoneRigidbody.simulated = true;
        stoneRigidbody.bodyType = RigidbodyType2D.Kinematic;
        stoneRigidbody.position = _initialStonePosition;
        stoneRigidbody.rotation = _initialStoneRotation;
        stoneRigidbody.linearVelocity = Vector2.zero;
        stoneRigidbody.angularVelocity = 0f;
        Physics2D.SyncTransforms();
    }

    #endregion
}
