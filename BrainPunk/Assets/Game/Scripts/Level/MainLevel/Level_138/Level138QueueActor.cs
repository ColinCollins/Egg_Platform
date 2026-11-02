using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>138 本关排队者；复用 Actor 美术模板，不运行玩家输入、皮肤存档或失败事件。</summary>
public sealed class Level138QueueActor : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SkeletonAnimation skeleton;
    [SerializeField] private SortingGroup sorting;

    private CupBinding cup;
    private string animationName;
    private string leavingAnimationName = "walk";
    private float animationSpeed = 1f;
    /// <summary>指示该排队者是否已经进入向右离场流程。</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>指示该排队者是否仍是从入口向队尾移动的补位者。</summary>
    public bool IsRefilling { get; private set; }

    /// <summary>指示该排队者是否因关店而以补位动画离场。</summary>
    public bool IsRefillDeparture { get; private set; }

    /// <summary>指示该排队者是否已经被玩家插队打斗流程标记为命中对象。</summary>
    public bool HasHit { get; private set; }

    /// <summary>指示该排队者是否已经完成本次点单并进入领餐流程。</summary>
    public bool HasOrdered { get; private set; }

    /// <summary>当前实例使用的身体服饰索引；未成功初始化时保持为 -1。</summary>
    public int ClothingId { get; private set; } = -1;

    /// <summary>当前实例使用的额外装饰索引；没有额外装饰或未初始化时保持为 -1。</summary>
    public int ExtraAccessoryId { get; private set; } = -1;

    /// <summary>当前排队者主碰撞体在世界空间中的包围盒。</summary>
    public Bounds BodyBounds => bodyCollider.bounds;

    /// <summary>当前排队者用于移动和碰撞控制的主碰撞体。</summary>
    public Collider2D BodyCollider => bodyCollider;

    /// <summary>
    /// 初始化一个排队者实例的外观、运动学刚体、动画和挂杯状态。
    /// 该方法只应由 Level 138 总控在模板克隆后调用；返回 false 表示实例不能加入队伍。
    /// </summary>
    /// <param name="clothingId">已分配的身体服饰索引。</param>
    /// <param name="extraAccessoryId">已分配的额外装饰索引；无装饰时传入 -1。</param>
    /// <param name="face">要挂到 point01 的脸部装饰实例或预制体。</param>
    /// <param name="clothes">要挂到 point02 的身体服饰实例或预制体。</param>
    /// <param name="back">要挂到 point 的背部装饰实例或预制体。</param>
    /// <param name="refill">是否将该实例作为补位者初始化。</param>
    /// <param name="cupTemplate">用于创建本实例右手奶茶的 SpriteRenderer 模板。</param>
    /// <returns>所有必要绑定、骨骼和挂杯初始化成功时返回 true。</returns>
    public bool Initialize(int clothingId, int extraAccessoryId, GameObject face, GameObject clothes, GameObject back,
        bool refill, SpriteRenderer cupTemplate)
    {
        if (body == null || bodyCollider == null || skeleton == null || sorting == null)
            return false;

        // 模板在 Prefab 中已隔离共享 ActorCtrl / SkinBinding；激活前再次保留 NPC 身份。
        gameObject.tag = "Untagged";
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        bodyCollider.enabled = true;
        IsLeaving = false;
        IsRefillDeparture = false;
        leavingAnimationName = "walk";
        IsRefilling = refill;
        HasHit = false;
        HasOrdered = false;
        skeleton.Initialize(false);
        const string skin = "box_original";
        if (skeleton.Skeleton == null || skeleton.Skeleton.Data.FindSkin(skin) == null)
            return false;
        skeleton.Skeleton.SetSkin(skin);
        skeleton.Skeleton.SetSlotsToSetupPose();
        skeleton.Skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Update);
        // 每人必须穿衣服；脸饰/背饰仍可额外随机，全部是本关实例的子物体。
        if (clothes == null || !AttachAccessory(face, "point01") ||
            !AttachAccessory(clothes, "point02") || !AttachAccessory(back, "point"))
            return false;
        ClothingId = clothingId;
        ExtraAccessoryId = extraAccessoryId;
        animationSpeed = skeleton.timeScale;
        cup = CupBinding.Create(transform, skeleton, cupTemplate);
        if (cup == null)
            return false;
        Play(refill ? "walk01" : "idle");
        return true;
    }

    private bool AttachAccessory(GameObject prefab, string boneName)
    {
        if (prefab == null) return true;
        if (skeleton.Skeleton.FindBone(boneName) == null) return false;
        foreach (BoneFollower follower in skeleton.GetComponentsInChildren<BoneFollower>(true))
        {
            if (follower.skeletonRenderer != skeleton || follower.boneName != boneName) continue;
            follower.Initialize();
            follower.LateUpdate();
            if (!follower.valid) return false;
            GameObject part = Instantiate(prefab, follower.transform, false);
            part.name = prefab.name;
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = Vector3.one;
            if (boneName == "point")
                foreach (SpriteRenderer sprite in part.GetComponentsInChildren<SpriteRenderer>(true))
                    sprite.sortingOrder = -1; // 背饰在本Actor身体之后，不与身体order0争同层。
            // 使用Actor已有的背/脸/身体挂点与内部层级，杯order300始终高于装饰。
            part.SetActive(true);
            return true;
        }
        return false;
    }

    /// <summary>设置该排队者的渲染层级；由 Level 138 总控统一重排。</summary>
    /// <param name="order">要写入 SortingGroup 的排序层级。</param>
    public void SetSorting(int order) => sorting.sortingOrder = order;

    /// <summary>标记该排队者已经被玩家插队打斗命中。</summary>
    public void MarkHit() => HasHit = true;

    /// <summary>标记该排队者已经完成点单并获得领餐资格。</summary>
    public void MarkOrdered() => HasOrdered = true;

    /// <summary>将补位者转为正式队列成员，并切换到待机动画。</summary>
    public void JoinQueue()
    {
        IsRefilling = false;
        Play("idle");
    }

    /// <summary>仅移动本关运动学刚体；朝玩家行进时限制距离，不能用 MovePosition 推走玩家。</summary>
    /// <param name="targetCenterX">本帧期望到达的身体中心 X 坐标。</param>
    /// <param name="speed">本帧使用的水平移动速度。</param>
    /// <param name="dt">本帧的时间步长。</param>
    /// <param name="player">需要避免主动推开的玩家碰撞体；离场时可传入 null。</param>
    public void TickMove(float targetCenterX, float speed, float dt, Collider2D player)
    {
        float currentX = IsLeaving ? transform.position.x : BodyBounds.center.x;
        float step = Mathf.MoveTowards(currentX, targetCenterX, speed * dt) - currentX;
        bool wantsToWalk = Mathf.Abs(step) > 0.0001f;
        if (!IsLeaving && player != null && player.enabled && wantsToWalk)
        {
            Bounds mine = BodyBounds;
            Bounds other = player.bounds;
            if (mine.min.y < other.max.y && mine.max.y > other.min.y)
            {
                if (step > 0f && other.center.x > mine.center.x)
                    step = Mathf.Min(step, Mathf.Max(0f, other.min.x - mine.max.x - 0.01f));
                else if (step < 0f && other.center.x < mine.center.x)
                    step = Mathf.Max(step, Mathf.Min(0f, other.max.x - mine.min.x + 0.01f));
            }
        }
        body.MovePosition(body.position + Vector2.right * step);
        string walkAnimation = IsLeaving ? leavingAnimationName : (IsRefilling ? "walk01" : "walk");
        Play(wantsToWalk ? walkAnimation : "idle");
    }

    internal CupBinding Cup => cup;

    /// <summary>隐藏并重新挂回当前排队者的奶茶，不改变点单或离场标记。</summary>
    public void HideCup() => cup?.Hide();

    /// <summary>
    /// 关店后让队伍原地等待在途杯和队首问号演出，并暂时关闭其主碰撞体。
    /// 该方法不负责决定何时离场。
    /// </summary>
    public void WaitForClosedShopDelivery()
    {
        // 原地idle等待关店离场时机；关店队伍不再互挡玩家或参与插队惩罚。
        body.linearVelocity = Vector2.zero;
        bodyCollider.enabled = false;
        Play("idle");
    }

    /// <summary>
    /// 开始向右离场并关闭该实例的队列碰撞。
    /// 关店待交付者应由总控确认奶茶已到手后再调用；在途飞行只在此处作为异常兜底取消。
    /// </summary>
    /// <param name="playerOrder">玩家的排序层级，用于计算离场者的渲染层级。</param>
    public void Depart(int playerOrder)
    {
        // 关店待交付由总控等到IsHeld后才调用；此处只兜底清理非正常残留飞行，保留已到手杯。
        cup?.CancelFlight();
        // 尚在途的补位者关店后保持walk01直接离场；已归队者仍按普通walk离场。
        IsRefillDeparture = IsRefilling;
        leavingAnimationName = IsRefillDeparture ? "walk01" : "walk";
        IsLeaving = true;
        IsRefilling = false;
        // 关闭本体碰撞，离场不再占位、挡玩家、触发攻击或误发 FallArea 事件。
        bodyCollider.enabled = false;
        sorting.sortingOrder = playerOrder - 10;
        Play(leavingAnimationName);
    }

    /// <summary>暂停或恢复该排队者的 Spine 动画，并在暂停时清零刚体速度。</summary>
    /// <param name="paused">true 表示暂停，false 表示恢复初始化时记录的动画速度。</param>
    public void SetPaused(bool paused)
    {
        if (skeleton != null)
            skeleton.timeScale = paused ? 0f : animationSpeed;
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 清理奶茶和动态挂点后销毁该排队者实例。
    /// 只应由 Level 138 总控在实例离开右侧区域或清理旧关卡时调用。
    /// </summary>
    public void Recycle()
    {
        // 飞行中杯子暂挂关卡根，必须显式销毁，不能只依赖角色子树回收。
        cup?.Dispose();
        cup = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDisable() => cup?.Hide();
    private void OnDestroy() => cup?.Dispose();

    private void Play(string name)
    {
        if (animationName == name)
            return;
        animationName = name;
        skeleton.AnimationState.SetAnimation(0, name, true);
    }

    /// <summary>玩家与 NPC 共用右手挂杯；Box_Center9s 属于 Soft_arm_R，不复用左手或店铺摆件。</summary>
    internal sealed class CupBinding
    {
        private const string RightHandBoneName = "Box_Center9s";
        private const float CupScale = 0.75f; // 按用户要求恢复0.75；玩家、NPC、飞行和重新挂手共用。
        private readonly SpriteRenderer renderer;
        private readonly Transform hand;
        private readonly BoneFollower handFollower;
        private readonly Transform ownedHand;
        private Vector3 flightStartPosition;
        private Quaternion flightStartRotation;
        private bool flying;
        private bool disposed;
        /// <summary>指示奶茶当前是否已经挂回角色右手。</summary>
        public bool IsHeld { get; private set; }

        /// <summary>指示奶茶是否正在从店铺出杯点飞向角色右手。</summary>
        public bool IsFlying => flying && !disposed;

        private CupBinding(SpriteRenderer renderer, Transform hand, BoneFollower handFollower, Transform ownedHand)
        {
            this.renderer = renderer;
            this.hand = hand;
            this.handFollower = handFollower;
            this.ownedHand = ownedHand;
        }

        /// <summary>
        /// 为指定角色创建右手挂杯绑定；优先复用已绑定右手骨骼的 handBone，缺少时才创建本关拥有的动态挂点。
        /// </summary>
        /// <param name="root">角色实例根节点，动态挂点和奶茶实例会归属于该节点。</param>
        /// <param name="skeleton">角色使用的 Spine 骨骼。</param>
        /// <param name="template">要实例化的奶茶 SpriteRenderer 模板。</param>
        /// <returns>创建成功的绑定；缺少右手骨骼或模板时返回 null。</returns>
        public static CupBinding Create(Transform root, SkeletonAnimation skeleton, SpriteRenderer template)
        {
            if (skeleton == null || template == null)
                return null;
            skeleton.Initialize(false);
            if (skeleton.Skeleton == null || skeleton.Skeleton.FindBone(RightHandBoneName) == null)
                return null;
            Transform hand = null;
            BoneFollower handFollower = null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "handBone") continue;
                BoneFollower existingFollower = child.GetComponent<BoneFollower>();
                // 同名挂点可能仍属于左手；只复用本角色骨架上已经绑定右手的节点。
                if (existingFollower != null && existingFollower.skeletonRenderer == skeleton &&
                    existingFollower.boneName == RightHandBoneName)
                {
                    hand = child;
                    handFollower = existingFollower;
                    break;
                }
            }

            Transform owned = null;
            if (hand == null)
            {
                owned = hand = new GameObject("handBone").transform;
                hand.SetParent(root, false);
                handFollower = hand.gameObject.AddComponent<BoneFollower>();
                handFollower.skeletonRenderer = skeleton;
                handFollower.boneName = RightHandBoneName;
                handFollower.followBoneRotation = true;
                handFollower.Initialize();
            }
            SpriteRenderer instance = Object.Instantiate(template, hand, false);
            instance.name = "Level138_HandCup";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * CupScale;
            instance.sortingOrder = 300;
            instance.gameObject.SetActive(false);
            return new CupBinding(instance, hand, handFollower, owned);
        }

        /// <summary>进度由服务时钟提供；起点只在起飞时采样，终点每次取当前右手位置。</summary>
        /// <param name="source">本次飞杯的店铺出杯点。</param>
        /// <param name="flightRoot">飞行期间暂挂奶茶的关卡根节点。</param>
        /// <param name="progress">范围为 0 到 1 的飞行进度，超出范围会被夹限。</param>
        /// <param name="arcHeight">抛物线弧高；负值按 0 处理。</param>
        public void UpdateFlight(Transform source, Transform flightRoot, float progress, float arcHeight)
        {
            if (disposed || IsHeld || renderer == null || hand == null || handFollower == null || source == null)
                return;
            // 不依赖本控制器与 BoneFollower 的 LateUpdate 顺序，接杯前刷新当前骨骼挂点。
            handFollower.LateUpdate();
            if (!handFollower.valid || handFollower.bone == null) return;
            if (!flying)
            {
                flightStartPosition = source.position;
                flightStartRotation = source.rotation;
                renderer.transform.SetParent(flightRoot, true);
                renderer.gameObject.SetActive(true);
                flying = true;
                AudioManager.PlaySound("level64Block");
            }
            float t = Mathf.Clamp01(progress);
            renderer.transform.position = Vector3.Lerp(flightStartPosition, hand.position, t)
                + Vector3.up * (4f * Mathf.Max(0f, arcHeight) * t * (1f - t));
            // 飞行保持出杯朝向；不向含Y轴翻面的手部旋转做3D插值，避免杯子途中转成侧面。
            renderer.transform.rotation = flightStartRotation;
            if (t < 1f) return;

            AttachToHand();
            flying = false;
            IsHeld = true;
        }

        /// <summary>取消当前飞行并将奶茶隐藏、重新挂回右手。</summary>
        public void CancelFlight()
        {
            if (flying) Hide();
        }

        /// <summary>隐藏奶茶并清除已持有、飞行状态；对象仍可继续复用。</summary>
        public void Hide()
        {
            if (disposed) return;
            flying = false;
            IsHeld = false;
            if (renderer == null) return;
            renderer.gameObject.SetActive(false);
            AttachToHand();
        }

        private void AttachToHand()
        {
            if (renderer == null || hand == null) return;
            renderer.transform.SetParent(hand, false);
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one * CupScale;
        }

        /// <summary>
        /// 幂等释放奶茶渲染实例和本绑定创建的动态 handBone。
        /// 释放后不能再用于飞行或挂杯。
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            Hide();
            disposed = true;
            if (renderer != null) Object.Destroy(renderer.gameObject);
            if (ownedHand != null) Object.Destroy(ownedHand.gameObject);
        }
    }
}
