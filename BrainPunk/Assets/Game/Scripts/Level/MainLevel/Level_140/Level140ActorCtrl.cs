using Bear.EventSystem;
using Game.Events;
using UnityEngine;

/// <summary>仅 Level140：夹持、抬高锁定等待与进食期间不让共享移动动画覆盖演出。</summary>
public sealed class Level140ActorCtrl : ActorCtrl
{
    /// <summary>所属的 Level140 编排器；仅由关卡实例在 Awake 注入，未绑定时角色不得推进本关输入或动画。</summary>
    public Level140Ctrl Owner { get; set; }
    /// <summary>表示共享 Actor 初始化和本关夹持初态已经完成；只读供关卡门禁使用，重复初始化必须保持幂等。</summary>
    public bool Initialized { get; private set; }
    /// <summary>返回共享 Actor 当前是否处于席位锁定状态；仅暴露状态，不负责改变坐下或起身流程。</summary>
    public bool Seated => IsSit;
    /// <summary>表示角色正在进食、膨胀或最终退场演出；为 true 时必须阻止共享移动输入和普通动画覆盖。</summary>
    public bool Eating { get; private set; }
    /// <summary>表示角色已经脱离 Alien 夹持并进入本关地面流程；释放前不得恢复普通移动和物理模拟。</summary>
    public bool Released { get; private set; }
    private GameObject shadow;
    private bool frozen;
    private bool keyboardMoving;
    private Vector2 frozenVelocity;
    private Vector3 waitingPosition;

    private void Awake()
    {
        // 关卡加载转场可能跨多个物理帧；第一帧就停掉夹持中的物理。
        GetComponent<Rigidbody2D>().simulated = false;
    }

    /// <summary>由 Unity 生命周期调用并转入幂等初始化；不得在这里重复建立关卡运行对象或覆盖已恢复的重开初态。</summary>
    protected override void Start()
    {
        EnsureInitialized();
    }

    /// <summary>允许关卡控制器在自己的 Start 中提前完成角色初始化；可重复调用但只执行一次，避免 Unity 随后的 Start 覆盖重开初态。</summary>
    public void EnsureInitialized()
    {
        if (Initialized) return;
        base.Start();
        shadow = transform.Find("Shadow")?.gameObject;
        if (shadow != null)
        {
            shadow.GetComponent<ShadowController>()?.SetCharacter(transform);
            shadow.SetActive(false);
        }
        // 全身和部件外观由共享 ActorSkinBinding 应用，不能覆盖已保存的玩家选择。
        SpineCtrl.SetAlpha(1f);
        StopMoving();
        RB.simulated = false;
        Initialized = true;
    }

    /// <summary>由共享 Actor 更新入口调用；仅在本关运行、已释放且未冻结/进食时推进普通移动，席位中只保留跳跃输入。</summary>
    public override void OnUpdate()
    {
        if (!Initialized || Owner == null || !Owner.IsRunning || !Released || frozen || Eating)
            return;
        if (IsSit) { HandleInput(); return; }
        base.OnUpdate();
    }

    /// <summary>由共享 Actor 更新链调用；夹持、暂停、席位或进食阶段禁止共享动画状态机覆盖本关演出。</summary>
    protected override void HandleAnimators()
    {
        if (!Released || frozen || IsSit || Eating) return;
        base.HandleAnimators();
    }

    /// <summary>仅提供 Unity Editor 键盘调试输入；正式设备继续由共享 HUD 事件写入移动/跳跃状态，不得在此新增生产输入链。</summary>
    protected override void HandleInput()
    {
#if UNITY_EDITOR
        if (Owner == null || !Owner.CanOperate) return;
        if (Input.GetKeyDown(KeyCode.Q)) IsEditor = !IsEditor;
        if (!IsEditor)
        {
            if (keyboardMoving) this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
            keyboardMoving = false;
            return;
        }
        float horizontal = IsSit ? 0f : Input.GetAxisRaw("Horizontal");
        if (horizontal > 0f) this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
        else if (horizontal < 0f) this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);
        else if (keyboardMoving) this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
        // 空闲键盘不逐帧取消 HUD 的按住移动；只取消本次真实键盘输入。
        keyboardMoving = horizontal != 0f;
        if (Input.GetKeyDown(KeyCode.Space)) this.DispatchEvent(Witness<PlayerJumpEvent>._);
#endif
    }

    /// <summary>响应共享跳跃请求；只有本关允许操作时生效，席位中先解除锁定再沿共享 Jump 执行。</summary>
    protected override void JumpButton()
    {
        if (Owner == null || !Owner.CanOperate || Eating || frozen) return;
        if (IsSit)
        {
            StopSit();
            Jump();
        }
        else base.JumpButton();
    }

    /// <summary>由本关控制器在 Alien 松爪或重开定位时调用；把角色移回关卡根并恢复正向物理姿态，调用方须先完成站位判定。</summary>
    public void Release(Transform levelRoot, bool standing = false)
    {
        transform.SetParent(levelRoot, true);
        // 保留人工夹持姿态至松爪；落下后恢复普通 Actor 的正向物理坐标。
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        SetActorFaceDirection(true);
        Released = true;
        RB.bodyType = RigidbodyType2D.Dynamic;
        RB.linearVelocity = Vector2.zero;
        RB.angularVelocity = 0f;
        RB.simulated = !frozen;
        StopMoving();
        if (shadow != null) shadow.SetActive(true);
        SpineCtrl.PlayAnimation(standing ? "idle" : "jump_down", standing, false);
        if (standing) SpineCtrl.SetAlpha(1f);
    }

    /// <summary>入场或重开准备完成后开放普通行走；仅清零当前水平输入并恢复共享移动门禁，不负责切换玩法状态。</summary>
    public void EnableWalking()
    {
        SetMoveInput(0f);
        ResumeMoving();
    }

    /// <summary>由本关自动入座流程调用；锁定角色到已计算的等待点并关闭物理，目标必须属于当前关卡且不能在进食中调用。</summary>
    public override void PlaySit(Transform target, Vector3 offset, bool isRight = true)
    {
        if (!Initialized || !Released || IsSit || Eating || IsDied) return;
        SetMoveInput(0f);
        StopMoving();
        SetSitState(true);
        SetActorFaceDirection(isRight);
        waitingPosition = target.position + offset;
        RB.simulated = false;
        RB.position = waitingPosition;
        transform.position = waitingPosition;
        if (shadow != null) shadow.SetActive(false);
        SpineCtrl.PlayAnimation("idle", true, false);
    }

    /// <summary>每帧维持席位等待根位置；只在已坐下时生效，不重播动画，避免覆盖正在执行的进食表现。</summary>
    public void LockWaitingPose()
    {
        if (!IsSit) return;
        // 只锁根位置，不重播 idle，以免覆盖已经开始的 eat。
        transform.position = waitingPosition;
    }

    /// <summary>由跳跃或本关流程解除席位锁定；进食期间拒绝执行，恢复父级和物理时不得使用共享旧 OriginParent。</summary>
    public override void StopSit()
    {
        if (!IsSit || Eating) return;
        // Actor 的初始父级在 Alien 内，不能使用共享 StopSit 的 OriginParent。
        SetSitState(false);
        transform.SetParent(Owner.transform, true);
        RB.bodyType = RigidbodyType2D.Dynamic;
        RB.simulated = !frozen;
        RB.linearVelocity = Vector2.zero;
        ResumeMoving();
        if (shadow != null) shadow.SetActive(true);
        SpineCtrl.PlayAnimation("idle", true, false);
    }

    /// <summary>以1.5倍速度开始当前一份进食并返回实际时长；调用方须等本份入嘴及动画结束后再取下一份。</summary>
    public float BeginEating()
    {
        Eating = true;
        SetMoveInput(0f);
        RB.simulated = false;
        var entry = SpineCtrl.PlayAnimation("eat", false, false);
        if (entry == null) return 0.4f;
        entry.TimeScale = 1.5f;
        return entry.Animation.Duration / entry.TimeScale;
    }

    /// <summary>一至两份进食完成后恢复锁定 idle；只结束进食动画门禁，不清除席位状态或累计食量。</summary>
    public void EndEating()
    {
        Eating = false;
        // 一/两份后继续锁定等待，保留累计食量，不重新开启站立物理。
        SpineCtrl.PlayAnimation("idle", true, false);
    }

    /// <summary>三份实际入嘴后开始膨胀并返回动画时长；调用方须先确认无在途食物，期间保持输入和物理关闭。</summary>
    public float BeginMealExpansion()
    {
        Eating = true;
        SetMoveInput(0f);
        StopMoving();
        RB.simulated = false;
        return SpineCtrl.PlayAnimation("expand", false, false)?.Animation.Duration ?? 0.8f;
    }

    /// <summary>变大动画结束后原地保持体型；保留席位锁定、输入锁和关闭的物理，半秒后的成功由关卡时钟处理。</summary>
    public void HoldExpandedPose()
    {
        Eating = true;
        SetMoveInput(0f);
        StopMoving();
        RB.simulated = false;
        if (shadow != null) shadow.SetActive(false);
        SpineCtrl.PlayAnimation("expand_idle", true, false);
    }

    /// <summary>暂停或恢复本关角色物理；冻结时缓存速度，恢复仅在已释放、未坐下且未进食时重新启用模拟。</summary>
    public void SetFrozen(bool value)
    {
        if (!Initialized || value == frozen) return;
        frozen = value;
        if (value)
        {
            frozenVelocity = RB.linearVelocity;
            RB.simulated = false;
        }
        else
        {
            RB.simulated = Released && !IsSit && !Eating;
            if (RB.simulated) RB.linearVelocity = frozenVelocity;
        }
    }
}
