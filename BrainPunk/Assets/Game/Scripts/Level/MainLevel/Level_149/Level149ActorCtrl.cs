using Bear.EventSystem;
using System.Collections.Generic;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>接管本关左台定点跑动与右盘自动跑动，普通移动、跳跃及死亡沿用共享 Actor。</summary>
public sealed class Level149ActorCtrl : ActorCtrl
{
    /// <summary>本关控制器对 Actor 的运行时回调入口。</summary>
    /// <remarks>只由同一 Level149Ctrl 实例在 Awake 和 OnDisable 期间绑定、解除，不应由外部替换。</remarks>
    public Level149Ctrl Gameplay { get; set; }

    /// <summary>Actor 是否已经完成本关专属几何缓存并可以接受本关操作。</summary>
    public bool Initialized { get; private set; }

    /// <summary>Actor 当前是否由左台或右盘锁定接管。</summary>
    public bool WheelLocked { get; private set; }

    /// <summary>Actor 主 PolygonCollider2D，供本关承托和脚底判定使用。</summary>
    public Collider2D MainCollider { get; private set; }
    /// <summary>本关Actor的原脚步组件；仅跑轮接管时暂时关闭以避免双重脚步，离台恢复。</summary>
    [SerializeField] private ActorStepPlayCtrl stepPlayer;
    private bool _stepWasEnabled;
    private float _stepClock;
    private int _stepClip;
    private RigidbodyType2D _bodyType;
    private float _gravity;
    private RigidbodyInterpolation2D _interpolation;
    private bool _keyboardMoving;
    private bool _paused;
    private bool _simulated;
    private Vector2 _pauseVelocity;
    private Spine.AnimationState _pausedAnimation;
    private float _animationScale;
    private Vector2[] _wheelFootPoints;

    protected override void Start()
    {
        base.Start();
        var polygon = GetComponent<PolygonCollider2D>();
        MainCollider = polygon;
        if (polygon != null)
        {
            // 缓存本地几何，不混用插值后的 Transform 与物理时刻的 Collider.bounds。
            var points = new List<Vector2>();
            for (int path = 0; path < polygon.pathCount; path++)
                foreach (Vector2 point in polygon.GetPath(path)) points.Add(point + polygon.offset);
            _wheelFootPoints = points.ToArray();
        }
        Initialized = true;
    }

    /// <summary>在未被转盘接管或暂停时沿用共享 Actor 的逐帧更新。</summary>
    public override void OnUpdate()
    {
        if (!Initialized || _paused || WheelLocked) return;
        base.OnUpdate();
    }

    protected override void HandleInput()
    {
#if UNITY_EDITOR
        if (Gameplay == null || !Gameplay.CanOperate) return;
        if (Input.GetKeyDown(KeyCode.Q)) IsEditor = !IsEditor;
        if (!IsEditor) return;
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        if (left != right)
        {
            if (right) this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
            else this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);
        }
        else if (_keyboardMoving || left)
            this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
        _keyboardMoving = left || right;
        if (Input.GetKeyDown(KeyCode.Space))
            this.DispatchEvent(Witness<PlayerJumpEvent>._);
#endif
    }

    protected override void HandleAnimators()
    {
        // 共享 HUD 松键会调用 UpdateAnimation，不允许覆盖充电专属动作。
        if (!WheelLocked && !_paused) base.HandleAnimators();
    }

    protected override void JumpButton()
    {
        if (_paused || (Gameplay != null && !Gameplay.CanOperate)) return;
        if (WheelLocked)
        {
            Gameplay?.RequestDismount();
        }
        else
        {
            bool groundedStart = IsGround && RB.linearVelocityY <= 0.1f;
            bool fromBattery = Gameplay != null && Gameplay.IsActorOnBattery();
            base.JumpButton();
            // 右盘自动跑动中拒绝手动 Jump；同帧落地再跳仍重新核对左台起跳来源。
            if (groundedStart) Gameplay?.NotifyGroundJump(fromBattery && RB.linearVelocityY > 0.1f);
        }
    }

    /// <summary>自动上下右盘和离台用idle小弧线表现，不触发共享Jump或起跳音效。</summary>
    public void ShowTransferPose()
    {
        if (IsDied) return;
        var entry = SpineCtrl.PlayAnimation("idle", true);
        if (entry != null) entry.TimeScale = 1f;
    }

    /// <summary>仅有效跑轮帧播放原step的两个片段；快跑0.16秒一次，慢走0.4秒一次，暂停不累计。</summary>
    public void TickWheelSteps(float dt, bool sprint)
    {
        if (!WheelLocked || _paused || IsDied) return;
        _stepClock -= dt;
        if (_stepClock > 0f) return;
        AudioManager.PlaySound("step", 0.4f, _stepClip);
        _stepClip = (_stepClip + 1) % 2;
        _stepClock = sprint ? 0.16f : 0.4f;
    }

    /// <summary>保存刚体状态并将 Actor 切换为转盘接管的运动状态。</summary>
    /// <remarks>重复调用无效；只有本关控制器完成吸附判定后才能调用。</remarks>
    public void LockToWheel()
    {
        if (WheelLocked || !Initialized) return;
        _bodyType = RB.bodyType;
        _gravity = RB.gravityScale;
        _interpolation = RB.interpolation;
        WheelLocked = true;
        _stepClock = 0f;
        _stepWasEnabled = stepPlayer != null && stepPlayer.enabled;
        if (stepPlayer != null) stepPlayer.enabled = false;
        // 入台过程不翻面：左台到位帧朝左，右台开始自动跑时再由控制器朝右。
        RB.linearVelocity = Vector2.zero;
        RB.angularVelocity = 0f;
        RB.bodyType = RigidbodyType2D.Kinematic;
        RB.gravityScale = 0f;
        RB.interpolation = RigidbodyInterpolation2D.None;
    }

    /// <summary>在已锁定转盘期间把 Actor 固定到指定世界位置。</summary>
    /// <param name="position">本帧吸附或转盘运动计算得到的 Actor 世界位置。</param>
    /// <remarks>未锁定时不修改 Transform 或刚体。</remarks>
    public void Pin(Vector3 position)
    {
        if (!WheelLocked) return;
        transform.position = position;
        RB.position = position;
        RB.linearVelocity = Vector2.zero;
    }

    /// <summary>根据缓存的碰撞体几何计算 Actor 脚底中心和根节点到脚底的距离。</summary>
    /// <param name="foot">当前朝向下的脚底世界位置。</param>
    /// <param name="rootToFoot">根节点到脚底最低点的世界距离。</param>
    /// <returns>存在可用碰撞体几何时返回 true。</returns>
    public bool TryGetWheelFoot(out Vector3 foot, out float rootToFoot)
    {
        foot = transform.position;
        rootToFoot = 0f;
        if (_wheelFootPoints == null || _wheelFootPoints.Length == 0) return false;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        foreach (Vector2 point in _wheelFootPoints)
        {
            Vector3 offset = transform.TransformVector(new Vector3(point.x, point.y, 0f));
            minX = Mathf.Min(minX, offset.x);
            maxX = Mathf.Max(maxX, offset.x);
            minY = Mathf.Min(minY, offset.y);
        }
        // 偏移不含当前世界 Y，同一缩放/朝向下每次跳回得到同一个锁定高度。
        rootToFoot = -minY;
        foot += new Vector3((minX + maxX) * 0.5f, minY, 0f);
        return true;
    }

    /// <summary>按吸附途中每帧的实际升降切换 jump_up／jump_down，不覆盖玩家朝向。</summary>
    public void ShowBoarding(float verticalSlope)
    {
        if (IsDied) return;
        var entry = SpineCtrl.PlayAnimation(verticalSlope > 0f ? "jump_up" : "jump_down", false);
        if (entry != null) entry.TimeScale = 1f;
    }

    /// <summary>明确切换 idle/walk/walk01；不经过渐加速阈值，不按每一步启停盘面。</summary>
    public void ShowWheelMotion(float facingDirection, string animationName, float gaitCyclesPerSecond)
    {
        if (IsDied) return;
        var previous = SpineCtrl.AnimationState?.GetCurrent(0);
        float previousClipCycles = GaitCyclesPerClip(previous);
        float previousPhase = Mathf.Repeat(ReadGaitCycles(previous), 1f);
        SetActorFaceDirection(facingDirection > 0f);
        var entry = SpineCtrl.PlayAnimation(animationName, true);
        if (entry != null)
        {
            if (previous != entry) entry.MixDuration = 0f;
            float cycles = GaitCyclesPerClip(entry);
            if (cycles > 0f)
            {
                // walk 有两次完整左右跨步，walk01 只有一次；切动作时保留跨步相位。
                if (previous != entry && previousClipCycles > 0f)
                {
                    entry.TrackTime = previousPhase * entry.Animation.Duration / cycles;
                    entry.MixDuration = 0f;
                }
                entry.TimeScale = gaitCyclesPerSecond * entry.Animation.Duration / cycles;
            }
            else entry.TimeScale = 1f;
        }
    }

    internal static float GaitCyclesPerClip(Spine.TrackEntry entry)
    {
        if (entry?.Animation == null) return 0f;
        return entry.Animation.Name == "walk" ? 2f : entry.Animation.Name == "walk01" ? 1f : 0f;
    }

    internal static float ReadGaitCycles(Spine.TrackEntry entry)
    {
        return entry?.Animation == null || entry.Animation.Duration <= 0f ? 0f :
            entry.TrackTime / entry.Animation.Duration * GaitCyclesPerClip(entry);
    }

    /// <summary>恢复锁定前保存的刚体状态并解除转盘对 Actor 的接管。</summary>
    /// <remarks>重复调用无效；释放后会清除水平输入并恢复当前动画速度。</remarks>
    public void ReleaseWheel()
    {
        if (!WheelLocked || RB == null) return;
        WheelLocked = false;
        if (stepPlayer != null) stepPlayer.enabled = _stepWasEnabled;
        RB.bodyType = _bodyType;
        RB.gravityScale = _gravity;
        RB.interpolation = _interpolation;
        RB.linearVelocity = Vector2.zero;
        SetMoveInput(0f);
        var entry = SpineCtrl.AnimationState?.GetCurrent(0);
        if (entry != null) entry.TimeScale = 1f;
    }

    /// <summary>在转盘接管期间暂停或恢复刚体模拟、动画和速度。</summary>
    /// <param name="paused">true 表示保存状态并冻结；false 表示恢复保存的状态。</param>
    /// <remarks>Actor 未初始化或尚未被转盘接管时，暂停请求不会关闭共享出生演出。</remarks>
    public void SetPaused(bool paused)
    {
        if (!Initialized || paused == _paused) return;
        // docs/Errors/level134-startdoor-sink-rebound 同因豁免：未接管（非 WheelLocked，含出生淡入）时，
        // 共享出门演出在加载 PAUSE 期间继续推进，本关不得额外关闭物理模拟；只在充电接管后冻结。
        // 必须在写入快照/保存速度/关闭 simulated 之前返回，避免恢复时回写过期速度。
        if (paused && !WheelLocked) return;
        _paused = paused;
        if (paused)
        {
            _simulated = RB.simulated;
            _pauseVelocity = RB.linearVelocity;
            RB.simulated = false;
            _pausedAnimation = SpineCtrl.AnimationState;
            if (_pausedAnimation != null)
            {
                _animationScale = _pausedAnimation.TimeScale;
                _pausedAnimation.TimeScale = 0f;
            }
        }
        else
        {
            RB.simulated = _simulated;
            RB.linearVelocity = _pauseVelocity;
            if (_pausedAnimation != null) _pausedAnimation.TimeScale = _animationScale;
            _pausedAnimation = null;
        }
    }

    /// <summary>沿用共享死亡流程，并先解除本关转盘接管。</summary>
    public override void Die()
    {
        if (IsDied) return;
        SetPaused(false);
        Gameplay?.LeaveWheel();
        base.Die();
    }

    private void OnDisable()
    {
        SetPaused(false);
        ReleaseWheel();
    }
}
