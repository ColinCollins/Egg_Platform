using UnityEngine;

/// <summary>
/// Level142 的单个 APP：根节点保存落点和静态碰撞，子节点负责下落、抖动及删除表现。
/// 仅由本关控制器初始化与推进，不操作共享物理层或关卡胜负。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level142AppItem : MonoBehaviour
{
    #region 场景绑定与状态
    [Tooltip("仅承载图案和减号的视觉节点；不得挂阻挡碰撞盒。")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("玩家原有 APP 图案，供点击命中判断。")]
    [SerializeField] private SpriteRenderer icon;
    [Tooltip("玩家原有减号图案，进入编辑状态后显示。")]
    [SerializeField] private SpriteRenderer minus;
    [Tooltip("根节点的静态碰撞盒；落地后阻挡，视觉动画不改变它。")]
    [SerializeField] private BoxCollider2D blocker;

    private enum Phase { Hidden, Waiting, Falling, Landed, Deleting, Removed }
    private Phase phase;
    private Vector3 visualPosition;
    private Quaternion visualRotation;
    private Vector3 visualScale;
    private float timer;
    private float dropHeight;
    private float dropDuration;
    private float wobblePhase;
    private bool editing;

    /// <summary>只有落定且尚未删除的 APP 接受长按和减号点击。</summary>
    public bool CanInteract => phase == Phase.Landed;
    #endregion

    #region 下落与碰撞
    /// <summary>运行态创建关卡时调用一次；缓存用户布局并隐藏本轮尚未触发的 APP。</summary>
    public void Initialize(float phaseOffset)
    {
        if (!Application.isPlaying) return;
        wobblePhase = phaseOffset;
        visualPosition = visualRoot.localPosition;
        visualRotation = visualRoot.localRotation;
        visualScale = visualRoot.localScale;
        blocker.enabled = false;
        minus.gameObject.SetActive(false);
        visualRoot.gameObject.SetActive(false);
        phase = Phase.Hidden;
    }

    /// <summary>按钮首次触发后安排下落；高度使用世界单位，保留用户摆放的最终姿态。</summary>
    public void ScheduleDrop(float delay, float worldHeight, float duration)
    {
        if (!Application.isPlaying || phase != Phase.Hidden) return;
        timer = -delay;
        dropHeight = worldHeight;
        dropDuration = duration;
        phase = Phase.Waiting;
    }

    /// <summary>仅在可游玩期间推进；返回 true 表示本帧首次落地，供控制器触发一次震屏。</summary>
    public bool Tick(float deltaTime, float animationTime)
    {
        bool landed = false;
        if (phase == Phase.Waiting || phase == Phase.Falling)
        {
            timer += deltaTime;
            if (timer < 0f) return false;
            phase = Phase.Falling;
            visualRoot.gameObject.SetActive(true);
            float progress = Mathf.Clamp01(timer / dropDuration);
            Vector3 worldOffset = Vector3.up * (dropHeight * (1f - progress * progress));
            visualRoot.localPosition = visualPosition + transform.InverseTransformVector(worldOffset);
            if (progress >= 1f)
            {
                phase = Phase.Landed;
                visualRoot.localPosition = visualPosition;
                blocker.enabled = true;
                minus.gameObject.SetActive(editing);
                landed = true;
            }
        }

        if (phase == Phase.Landed)
        {
            // APP 之间均为静态碰撞体，不会相互推挤；主角在全部落定后恢复操作。
            float angle = editing ? Mathf.Sin(animationTime * 32f + wobblePhase) * 3.5f : 0f;
            visualRoot.localRotation = visualRotation * Quaternion.Euler(0f, 0f, angle);
        }
        else if (phase == Phase.Deleting)
        {
            timer += deltaTime;
            float scale;
            if (timer < 0.1f)
                scale = Mathf.Lerp(1f, 1.12f, Mathf.Sin(timer / 0.1f * Mathf.PI * 0.5f));
            else
            {
                float t = Mathf.Clamp01((timer - 0.1f) / 0.24f);
                // 小幅回弹后缩至零，避免负缩放翻转图案。
                scale = 1.12f * (1f - (1.7f * t * t * t - 0.7f * t * t));
            }
            visualRoot.localScale = visualScale * scale;
            if (timer >= 0.34f)
            {
                phase = Phase.Removed;
                blocker.enabled = false;
                visualRoot.localScale = Vector3.zero;
                visualRoot.gameObject.SetActive(false);
            }
        }
        return landed;
    }
    #endregion

    #region 编辑与命中
    /// <summary>切换本关统一编辑状态；立即恢复视觉角度，碰撞盒始终不跟随抖动。</summary>
    public void SetEditing(bool value)
    {
        editing = value;
        minus.gameObject.SetActive(value && CanInteract);
        if (!value) visualRoot.localRotation = visualRotation;
    }

    /// <summary>减号点击后启动一次删除动画；重复点击无效，阻挡在缩为零时解除。</summary>
    public void Delete()
    {
        if (!Application.isPlaying || !editing || !CanInteract) return;
        phase = Phase.Deleting;
        timer = 0f;
        visualRoot.localRotation = visualRotation;
        minus.gameObject.SetActive(false);
    }

    /// <summary>按图案的局部矩形检测点击，支持用户原有旋转和编辑抖动。</summary>
    public bool ContainsIcon(Vector2 worldPoint) => CanInteract && Contains(icon, worldPoint);

    /// <summary>仅在减号可见时接受命中，不新增可能干扰角色的物理触发器。</summary>
    public bool ContainsMinus(Vector2 worldPoint) =>
        CanInteract && editing && Contains(minus, worldPoint);

    private static bool Contains(SpriteRenderer renderer, Vector2 worldPoint)
    {
        Vector3 local = renderer.transform.InverseTransformPoint(
            new Vector3(worldPoint.x, worldPoint.y, renderer.transform.position.z));
        Bounds bounds = renderer.sprite.bounds;
        return local.x >= bounds.min.x && local.x <= bounds.max.x &&
               local.y >= bounds.min.y && local.y <= bounds.max.y;
    }
    #endregion
}
