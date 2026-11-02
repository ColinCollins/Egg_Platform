using DG.Tweening;
using Game.Level104;
using UnityEngine;

/// <summary>
/// Level 104「不要回头」飞门编排器：沿用 103 飞门（FlyingDoorCtrl）的解耦编排模式，
/// 采用 104 专属可见折线路径——① 长翅膀 → ② 右侧垂直上飞到固定高度 → ③ 保持高度向左飞到左门位置
/// → ④ 左侧垂直下落停稳 → ⑤ 收翅恢复可进入（103 在右侧悬停，104 继续飞向左侧落定）。
/// 飞行状态机归属 Domain <see cref="Level104DontLookBackRule"/>；本组件只做表现与标定项适配。
/// </summary>
public sealed class Level104FlyingDoorCtrl : MonoBehaviour
{
    #region Inspector

    /// <summary>门体后层的两侧白色羽翼节点；Prefab 中保存最终镜像与 0.85 倍适配缩放，Awake 缓存后收起为零。</summary>
    [SerializeField] private Transform[] wings;

    /// <summary>一段「长翅膀」时长（秒）/ OutBack 弹出感。</summary>
    [SerializeField] private float wingGrowDuration = 0.3f;

    /// <summary>右侧可见上升段锁定的世界 X。</summary>
    [SerializeField] private float rightFlightX = 8.1f;

    /// <summary>右侧垂直上飞后的固定世界高度；该高度与 Level 103 飞门悬停高度一致，门体仍在相机内可见。</summary>
    [SerializeField] private float flightHeight = 3.545f;

    /// <summary>右侧垂直上飞时长（秒）/ InOutSine。</summary>
    [SerializeField] private float flyUpDuration = 0.8f;

    /// <summary>保持飞行高度向左移动后的左门世界 X；随后沿该 X 垂直下降。</summary>
    [SerializeField] private float leftDoorX = -7.61f;

    /// <summary>高位向左水平飞行时长（秒）/ InOutSine；当前合同 0.6 秒。</summary>
    [SerializeField] private float flyLeftDuration = 0.6f;

    /// <summary>左侧垂直下落的目标高度（地面原位世界 Y）。</summary>
    [SerializeField] private float landY = -1.25f;

    /// <summary>左侧垂直下落时长（秒）/ InOutSine。</summary>
    [SerializeField] private float landDuration = 1f;

    /// <summary>五段「收翅」时长（秒）。</summary>
    [SerializeField] private float wingRetractDuration = 0.2f;

    /// <summary>门体进入触发器（door_0 实例内唯一 Collider2D，为空时运行时自动解析）。
    /// 从关卡初始化（OnEnable）即封印、落定收翅后恢复——封印若等到 Play() 才设置，正解玩家可在门飞走判定
    /// 之前直接走进仍开启的右门判胜（已登记缺陷根因）；「门落定前的一切进门尝试不被 Domain 承认」由物理封印保证。</summary>
    [SerializeField] private Collider2D doorTrigger;

    #endregion

    #region Runtime State

    private Level104DontLookBackRule rule;
    private Sequence flySequence;
    private Vector3[] wingExpandedScales;

    #endregion

    #region Unity Messages

    private void Awake()
    {
        // Prefab 缩放同时承载左右镜像与门体比例适配；先缓存最终值，再收起等待「长翅膀」。
        if (wings != null)
        {
            wingExpandedScales = new Vector3[wings.Length];
            for (int i = 0; i < wings.Length; i++)
            {
                Transform wing = wings[i];
                if (wing == null)
                    continue;

                wingExpandedScales[i] = wing.localScale;
                wing.localScale = Vector3.zero;
            }
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        // 门触发器从关卡初始化即封印；落定收翅后由 Play() 序列末尾恢复。
        if (doorTrigger != null)
            doorTrigger.enabled = false;
    }

    private void OnDisable() => KillSequence();

    private void OnDestroy() => KillSequence();

    #endregion

    #region Public API

    /// <summary>由 Level104Ctrl 注入 Domain 规则会话；可见折线飞行的每一步状态迁移都经 Domain 门禁。</summary>
    public void Initialize(Level104DontLookBackRule dontLookBackRule)
    {
        rule = dontLookBackRule;
    }

    /// <summary>
    /// 可见折线编排：① 翅膀 DOScale 0→Prefab 适配比例 OutBack → ② 右侧锁 X 垂直上飞至固定高度 InOutSine
    /// → ③ 锁定该高度向左飞到左门 X InOutSine → ④ 左侧锁 X 垂直下落 InOutSine → ⑤ 翅膀 DOScale 适配比例→0 InBack；
    /// 扇翅动画由门根节点引用的 Level_104/FlyingDoorWing 本关专属 Animator 统一驱动双侧羽翼纯旋转，
    /// 贯穿上升、左飞与下降，且不改写门体或挂点位置。每步 callback 检查 Domain 返回值，
    /// false（如中途判负）即中止后续编排。
    /// </summary>
    public void Play()
    {
        KillSequence();
        // 门触发器在 OnEnable 已封印；此处幂等重断，防止飞行期被外部恢复。
        if (doorTrigger != null)
            doorTrigger.enabled = false;

        Vector3 startPosition = transform.position;
        transform.position = new Vector3(rightFlightX, startPosition.y, startPosition.z);

        flySequence = DOTween.Sequence().SetLink(gameObject);

        // ① 长翅膀
        if (wings != null)
        {
            for (int i = 0; i < wings.Length; i++)
            {
                Transform wing = wings[i];
                if (wing == null)
                    continue;

                Vector3 expandedScale = wingExpandedScales != null && i < wingExpandedScales.Length
                    ? wingExpandedScales[i]
                    : Vector3.one;
                flySequence.Join(wing.DOScale(expandedScale, wingGrowDuration).SetEase(Ease.OutBack));
            }
        }

        flySequence.AppendCallback(() =>
        {
            if (rule != null && !rule.NotifyWingsGrown())
                KillSequence();
        });

        // ② 右侧垂直上飞到固定可见高度。
        flySequence.Append(transform.DOMoveY(flightHeight, flyUpDuration).SetEase(Ease.InOutSine));
        flySequence.AppendCallback(() =>
        {
            if (rule != null && !rule.NotifyDoorReachedFlightHeight())
                KillSequence();
        });

        // ③ 保持高度向左飞到正常左门 X，让玩家直接看到门的水平去向。
        flySequence.Append(transform.DOMoveX(leftDoorX, flyLeftDuration).SetEase(Ease.InOutSine));
        flySequence.AppendCallback(() =>
        {
            if (rule != null && !rule.NotifyDoorReachedLeftX())
                KillSequence();
        });

        // ④ 左侧垂直下落停稳；DOMoveY 保持水平飞行后的左门 X。
        flySequence.Append(transform.DOMoveY(landY, landDuration).SetEase(Ease.InOutSine));
        flySequence.AppendCallback(() =>
        {
            if (rule != null && !rule.NotifyDoorLanded())
                KillSequence();
        });

        // ⑤ 收翅
        bool firstWingAppended = false;
        if (wings != null)
        {
            foreach (Transform wing in wings)
            {
                if (wing == null)
                    continue;

                Tween retract = wing.DOScale(0f, wingRetractDuration).SetEase(Ease.InBack);
                if (!firstWingAppended)
                {
                    flySequence.Append(retract);
                    firstWingAppended = true;
                }
                else
                {
                    flySequence.Join(retract);
                }
            }
        }

        flySequence.AppendCallback(() =>
        {
            if (rule != null && !rule.NotifyWingsRetracted())
            {
                KillSequence();
                return;
            }

            // 门落定收翅：恢复既有门触发通关链（Domain DoorLanded 与物理通路同步开放）。
            if (doorTrigger != null)
                doorTrigger.enabled = true;
        });
    }

    #endregion

    #region Helpers

    private void ResolveReferences()
    {
        // door_0 实例内唯一 Collider2D 即门进入触发器；翅膀无碰撞体，解析无歧义。
        if (doorTrigger == null)
            doorTrigger = GetComponentInChildren<Collider2D>(true);
    }

    private void KillSequence()
    {
        if (flySequence != null && flySequence.IsActive())
            flySequence.Kill();
        flySequence = null;
    }

    #endregion
}
