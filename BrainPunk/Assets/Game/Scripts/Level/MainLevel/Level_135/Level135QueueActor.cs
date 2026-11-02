using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>135 本关排队者；复用 Actor 美术模板，不运行玩家输入、皮肤存档或失败事件。</summary>
public sealed class Level135QueueActor : MonoBehaviour
{
    #region NPC绑定与状态
    /// <summary>本关模板的刚体、碰撞、骨架和父排序组，均在Prefab显式绑定。</summary>
    [SerializeField] private Rigidbody2D body;
    /// <summary>队列身体碰撞，普通离场和喷发时关闭。</summary>
    [SerializeField] private Collider2D bodyCollider;
    /// <summary>模板原骨架，播放顾客行走和待机。</summary>
    [SerializeField] private SkeletonAnimation skeleton;
    /// <summary>顾客整体排序组，不修改共享角色资源。</summary>
    [SerializeField] private SortingGroup sorting;
    /// <summary>使用原Actor骨架内已有的饰物挂点，保持138的父级缩放与前后关系。</summary>
    [SerializeField] private BoneFollower facePoint, bodyPoint, backPoint;

    #endregion

    #region 排队与演出
    private CupBinding cup;
    private string animationName;
    private float animationSpeed = 1f;
    private float waveBodyOffset;
    /// <summary>指示该排队者是否已经进入向右离场流程。</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>指示该排队者是否仍是从入口向队尾移动的补位者。</summary>
    public bool IsRefilling { get; private set; }

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
    /// 该方法只应由 Level 135 总控在模板克隆后调用；返回 false 表示实例不能加入队伍。
    /// </summary>
    /// <param name="clothingId">已分配的身体服饰索引。</param>
    /// <param name="extraAccessoryId">已分配的额外装饰索引；无装饰时传入 -1。</param>
    /// <param name="face">要挂到 point01 的脸部装饰实例或预制体。</param>
    /// <param name="clothes">要挂到 point02 的身体服饰实例或预制体。</param>
    /// <param name="back">要挂到 point 的背部装饰实例或预制体。</param>
    /// <param name="refill">是否将该实例作为补位者初始化。</param>
    /// <returns>所有必要绑定、骨骼和挂杯初始化成功时返回 true。</returns>
    public bool Initialize(int clothingId, int extraAccessoryId, GameObject face, GameObject clothes, GameObject back,
        bool refill)
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
        cup = CupBinding.Create(transform, skeleton);
        if (cup == null)
            return false;
        Play(refill ? "walk01" : "idle");
        return true;
    }

    private bool AttachAccessory(GameObject prefab, string boneName)
    {
        if (prefab == null) return true;
        if (skeleton.Skeleton.FindBone(boneName) == null) return false;
        BoneFollower follower = boneName == "point01" ? facePoint : boneName == "point02" ? bodyPoint : backPoint;
        follower.Initialize();
        follower.LateUpdate();
        if (!follower.valid) return false;
        GameObject part = Instantiate(prefab, follower.transform, false);
        // 对齐ActorSkinBinding：使用原挂点的内部排序，不为背饰另加SceneLayer排序组。
        // 外层场景SortingGroup会盖过身体/衣服的Default层，即使order为负也仍在前面。
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;
        part.SetActive(true);
        return true;
    }

    /// <summary>设置该排队者的渲染层级；由 Level 135 总控统一重排。</summary>
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
        string walkAnimation = IsRefilling ? "walk01" : "walk";
        Play(wantsToWalk ? walkAnimation : "idle");
    }

    internal CupBinding Cup => cup;

    /// <summary>隐藏并重新挂回当前排队者的可乐，不改变点单或离场标记。</summary>
    public void HideCup() => cup?.Hide();

    /// <summary>
    /// 开始向右离场并关闭该实例的队列碰撞。
    /// 只由主控在普通可乐交付完成后调用；喷发使用独立的横向清场分支。
    /// </summary>
    /// <param name="playerOrder">玩家的排序层级，用于计算离场者的渲染层级。</param>
    public void Depart(int playerOrder)
    {
        IsLeaving = true;
        IsRefilling = false;
        waveBodyOffset = bodyCollider.bounds.max.x - body.position.x;
        // 关闭本体碰撞，离场不再占位、挡玩家、触发攻击或误发 FallArea 事件。
        bodyCollider.enabled = false;
        sorting.sortingOrder = playerOrder - 10;
        Play("walk");
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
    /// 清理可乐和动态挂点后销毁该排队者实例。
    /// 只应由 Level 135 总控在实例离开右侧区域或清理旧关卡时调用。
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

    /// <summary>喷发开始时退出队列碰撞；只改变本NPC，不碰店员或玩家。</summary>
    public void BeginColaPush()
    {
        // 普通离场者已关闭碰撞，沿用离场前记录的身体边缘，不能读取空 bounds。
        if (bodyCollider.enabled) waveBodyOffset = bodyCollider.bounds.max.x - body.position.x;
        body.linearVelocity = Vector2.zero;
        bodyCollider.enabled = false;
        Play("idle");
    }

    /// <summary>浪花碰到后才往左顶；少量前后错位让被聚拢的顾客仍能辨认。</summary>
    public void PushWithWave(float frontX, float crowdOffset)
    {
        Vector2 position = body.position;
        position.x = Mathf.Min(position.x, frontX - waveBodyOffset - crowdOffset);
        body.position = position;
    }

    private void Play(string name)
    {
        if (animationName == name)
            return;
        animationName = name;
        skeleton.AnimationState.SetAnimation(0, name, true);
    }

    #endregion

    #region 右手瓶子所有权与飞行
    /// <summary>交付柜台的原瓶；每个接收者拥有自己的有气标记，回收时同时回收手持瓶。</summary>
    internal sealed class CupBinding
    {
        private readonly BoneFollower hand;
        private SpriteRenderer renderer;
        private Vector3 flightStart;
        private Quaternion flightRotation;
        private Vector3 startScale;
        private float clock;
        /// <summary>已飞抵右手，才可判断喝下后的效果。</summary>
        public bool IsHeld { get; private set; }
        /// <summary>仅在真正交出柜台瓶后开始飞行。</summary>
        public bool IsFlying { get; private set; }
        /// <summary>在出售那一刻捕获，不受补货瓶或其它瓶摇晃影响。</summary>
        public bool Charged { get; private set; }
        /// <summary>供本关喷发对准已交付的瓶口。</summary>
        public Vector3 Position => renderer != null ? renderer.transform.position : hand.transform.position;

        private CupBinding(BoneFollower hand) { this.hand = hand; }

        /// <summary>在已知右手骨骼创建本关动态挂点；不扫描/修改共享Actor节点。</summary>
        public static CupBinding Create(Transform root, SkeletonAnimation skeleton)
        {
            skeleton.Initialize(false);
            if (skeleton.Skeleton == null || skeleton.Skeleton.FindBone("Box_Center9s") == null) return null;
            BoneFollower follower = new GameObject("Level135_RightHand").AddComponent<BoneFollower>();
            follower.transform.SetParent(root, false);
            follower.skeletonRenderer = skeleton;
            follower.boneName = "Box_Center9s";
            follower.followBoneRotation = true;
            follower.Initialize();
            follower.LateUpdate();
            return new CupBinding(follower);
        }

        /// <summary>转移原柜台瓶的所有权，而不是在手上另造一瓶。</summary>
        public void BeginFlight(SpriteRenderer bottle, bool charged, Transform flightRoot)
        {
            renderer = bottle; Charged = charged; IsFlying = true; IsHeld = false;
            flightStart = bottle.transform.position;
            flightRotation = bottle.transform.rotation;
            bottle.transform.SetParent(flightRoot, true);
            startScale = bottle.transform.localScale;
            bottle.sortingOrder = 300;
            AudioManager.PlaySound("level64Block");
        }

        /// <summary>服务进度控制抛物线，目标每帧使用当前右手挂点。</summary>
        public void UpdateFlight(float progress, float arcHeight)
        {
            if (!IsFlying) return;
            hand.LateUpdate();
            float t = Mathf.Clamp01(progress);
            renderer.transform.position = Vector3.Lerp(flightStart, hand.transform.position, t)
                + Vector3.up * (4f * arcHeight * t * (1f - t));
            renderer.transform.rotation = flightRotation;
            renderer.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.75f, t);
            if (t < 1f) return;
            renderer.transform.SetParent(hand.transform, false);
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one * 0.75f;
            IsFlying = false; IsHeld = true;
        }

        /// <summary>手持有气瓶同样切两帧；普通瓶不切换。</summary>
        public void Tick(float dt, Level135ColaSupply supply)
        {
            clock += dt;
            if (Charged && renderer != null) renderer.sprite = supply.FizzFrame(clock);
        }

        /// <summary>喷发时将原瓶横向朝左，避免跟随手部翻转而朝反方向。</summary>
        public void PourLeft()
        {
            if (renderer != null) renderer.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        }

        /// <summary>开盖时原瓶留在起喷处；接收者由向前伸长的浪花顶走，瓶仍由本绑定回收。</summary>
        public void ReleaseForSpray(Transform levelRoot)
        {
            renderer.transform.SetParent(levelRoot, true);
            PourLeft();
        }

        /// <summary>喷发把角色推出后，让原瓶与水柱使用相同透明度；归属和回收责任不变。</summary>
        public void SetOpacity(float opacity)
        {
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = Mathf.Clamp01(opacity);
            renderer.color = color;
        }

        /// <summary>取消领取时回收本次瓶子；已经补出的柜台库存仍由柜台管理。</summary>
        public void Hide()
        {
            if (renderer != null) Object.Destroy(renderer.gameObject);
            renderer = null; IsFlying = false; IsHeld = false; Charged = false;
        }
        /// <summary>关卡/顾客回收时释放自有瓶和动态挂点。</summary>
        public void Dispose() { Hide(); if (hand != null) Object.Destroy(hand.gameObject); }
    }
    #endregion
}
