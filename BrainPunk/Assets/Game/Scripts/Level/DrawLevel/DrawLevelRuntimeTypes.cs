using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 画线玩法当前单关所处阶段。
/// 阶段只描述 Draw 控制器生命周期，不替代关卡脚本自己的演出状态。
/// </summary>
public enum DrawPlayPhase
{
    Intro,
    Drawing,
    Resolved,
}

/// <summary>
/// 画线关卡的一次性结算类型。
/// </summary>
public enum DrawLevelResultType
{
    Success,
    Failed,
}

/// <summary>
/// 画线关卡本地机制上报给 DrawPlayCtrl 的一次性结算结果。
/// </summary>
public readonly struct DrawLevelResult
{
    #region Public Properties

    /// <summary>本次结算是胜利还是失败。</summary>
    public DrawLevelResultType Type { get; }
    /// <summary>触发结算的本地来源标识，用于日志和后续统计。</summary>
    public string Source { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// 创建一次关卡结算结果；Source 应使用稳定的本地组件或机制名称。
    /// </summary>
    public DrawLevelResult(DrawLevelResultType type, string source)
    {
        Type = type;
        Source = source ?? string.Empty;
    }

    #endregion
}

/// <summary>
/// 当前墨水状态快照。
/// 注意事项：长度使用 DrawSpace 局部坐标单位，不使用像素或采样点数量。
/// </summary>
public readonly struct DrawInkState
{
    #region Public Properties

    /// <summary>当前关卡不含奖励墨水的基础最大可画长度。</summary>
    public float MaxInk { get; }
    /// <summary>当前尝试包含奖励墨水后的总可画容量。</summary>
    public float CurrentCapacity { get; }
    /// <summary>当前尝试已使用的有效折线长度。</summary>
    public float UsedInk { get; }
    /// <summary>当前尝试剩余的可画长度。</summary>
    public float RemainingInk { get; }
    /// <summary>剩余墨水占当前总容量的 0~1 比例，用于进度填充。</summary>
    public float RemainingNormalized { get; }
    /// <summary>按照关卡阈值计算出的当前星级。</summary>
    public int StarCount { get; }
    /// <summary>
    /// 当前 DrawLineFeature 最终采用的 1/2/3 星剩余墨水阶段。
    /// 调用边界：x/y/z 依次对应一星、二星、三星，供 HUD 与运行时判定保持同源。
    /// 注意事项：单位是 DrawSpace 剩余墨水绝对长度，不是 0～1 比例。
    /// </summary>
    public Vector3 Stage { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// 创建一次只读墨水快照；输入会被限制到合法范围。
    /// </summary>
    public DrawInkState(float maxInk, float usedInk, int starCount)
        : this(maxInk, maxInk, usedInk, starCount)
    {
    }

    /// <summary>
    /// 创建包含奖励墨水容量的状态快照。
    /// 调用边界：currentCapacity 不得小于基础 maxInk；超出部分只属于当前尝试。
    /// </summary>
    public DrawInkState(
        float maxInk,
        float currentCapacity,
        float usedInk,
        int starCount,
        Vector3 stage = default)
    {
        MaxInk = Math.Max(0f, maxInk);
        CurrentCapacity = Math.Max(MaxInk, currentCapacity);
        UsedInk = Math.Max(0f, Math.Min(usedInk, CurrentCapacity));
        RemainingInk = Math.Max(0f, CurrentCapacity - UsedInk);
        RemainingNormalized =
            CurrentCapacity > 0f ? RemainingInk / CurrentCapacity : 0f;
        StarCount = Math.Max(0, starCount);
        Stage = stage;
    }

    #endregion
}

/// <summary>
/// 一笔有效画线完成后的只读结果。
/// 调用边界：仅用于 DrawLevelCtrl 与关卡本地机制同步消费，不能作为永久存档对象持有。
/// </summary>
public readonly struct DrawStrokeResult
{
    #region Public Properties

    /// <summary>当前完成的是本次尝试中的第几笔，从 1 开始。</summary>
    public int StrokeIndex { get; }
    /// <summary>这一笔在 DrawSpace 中的有效折线长度。</summary>
    public float StrokeLength { get; }
    /// <summary>是否为本次尝试第一笔有效画线。</summary>
    public bool IsFirstStroke => StrokeIndex == 1;
    /// <summary>这一笔完成后的墨水状态快照。</summary>
    public DrawInkState InkState { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// 创建一笔画线完成结果；该对象只用于当前尝试内的同步通知。
    /// </summary>
    public DrawStrokeResult(int strokeIndex, float strokeLength, DrawInkState inkState)
    {
        StrokeIndex = Math.Max(1, strokeIndex);
        StrokeLength = Math.Max(0f, strokeLength);
        InkState = inkState;
    }

    #endregion
}

/// <summary>
/// DrawPlayCtrl 操作当前画线关卡所需的最小运行时契约。
/// 注意事项：接口只承载单关运行时状态，不接触 DB、广告或跨关永久数据。
/// </summary>
public interface IDrawLevelRuntime
{
    /// <summary>关卡本地机制首次成功提交胜利或失败时触发。</summary>
    event Action<DrawLevelResult> Resolved;

    /// <summary>必要引用已经解析且允许进入运行时流程。</summary>
    bool IsRuntimeReady { get; }
    /// <summary>当前是否允许接收画线输入。</summary>
    bool IsInputEnabled { get; }
    /// <summary>当前是否处于暂停状态。</summary>
    bool IsPaused { get; }
    /// <summary>
    /// 是否需要在进关转场完全揭开后再播放开场门禁。
    /// 注意事项：仅用于必须让玩家看到开场演出的关卡；默认关卡应保持 false。
    /// </summary>
    bool PlaysIntroAfterTransition { get; }
    /// <summary>当前关卡是否允许完成第一笔后继续画线。</summary>
    bool AllowsMultipleStrokes { get; }
    /// <summary>当前尝试已完成的有效笔数。</summary>
    int CompletedStrokeCount { get; }
    /// <summary>当前尝试已使用的 DrawSpace 折线总长度。</summary>
    float DrawnLength { get; }
    /// <summary>当前墨水状态快照。</summary>
    DrawInkState InkState { get; }

    /// <summary>
    /// 完成运行时引用解析并建立一次新的画线尝试。
    /// 调用边界：关卡 prefab 实例化完成后由 DrawPlayCtrl 调用一次。
    /// </summary>
    void InitializeRuntime();

    /// <summary>
    /// 使用当前关卡配置设置基础墨水长度和 1/2/3 星剩余墨水阈值。
    /// 调用边界：必须在 InitializeRuntime 前调用；缺少有效配置时保留 prefab 默认值。
    /// 注意事项：全部参数均使用 DrawSpace 长度单位，不是百分比。
    /// </summary>
    void ConfigureInk(
        float maxInkLength,
        float oneStarRemainingInk,
        float twoStarRemainingInk,
        float threeStarRemainingInk);

    /// <summary>
    /// 等待当前关卡的开场动画门禁结束。
    /// 默认实现可立即完成；有关卡专属开场时由本地 IDrawLevelIntroHandler 承担。
    /// </summary>
    UniTask PlayIntroAsync();

    /// <summary>
    /// Reset/Replay 重建关卡后直接应用各开场 Handle 声明的重开完成态。
    /// 调用边界：由 DrawPlayCtrl 的重载流程调用；不会重播任何普通 Intro 或进关等待。
    /// </summary>
    UniTask PlayResetIntroAsync();

    /// <summary>
    /// 由当前活动的 DrawPlayCtrl 每帧调用，处理鼠标或触摸输入。
    /// </summary>
    void Tick();

    /// <summary>
    /// 设置是否允许开始或继续画线；关闭时会取消并回退当前未完成的一笔。
    /// </summary>
    void SetInputEnabled(bool enabled);

    /// <summary>
    /// 设置暂停状态；进入暂停时会取消并回退当前未完成的一笔。
    /// </summary>
    void SetPaused(bool paused);

    /// <summary>
    /// 清理当前尝试的全部运行时线条、碰撞体和墨水消耗。
    /// </summary>
    void ResetAttempt();

    /// <summary>
    /// 显示或隐藏策划预设的虚线提示对象。
    /// </summary>
    void ShowHint(bool visible);

    /// <summary>
    /// 向当前尝试增加指定 DrawSpace 长度的奖励墨水容量，返回实际增加量。
    /// 注意事项：奖励可以使剩余墨水超过基础 MaxInk；广告资格由外围系统判断。
    /// </summary>
    float AddInk(float amount);

    /// <summary>
    /// 更新玩家实际绘制线条的材质和颜色，并同步到当前尝试已经生成的线条。
    /// 调用边界：供 PencilShop 或 DrawPlayCtrl 使用；不能修改策划预设虚线提示材质。
    /// </summary>
    void SetDrawingStyle(Material material, Color color);

    /// <summary>
    /// 更新玩家实际绘制线条的材质和沿线长方向的颜色渐变，并同步当前尝试已有线条。
    /// 调用边界：供 PencilShop 或 DrawPlayCtrl 使用；不能修改策划预设虚线提示材质。
    /// </summary>
    void SetDrawingStyle(Material material, Gradient gradient);

    /// <summary>
    /// 尝试提交一次胜利或失败；同一尝试只接受第一次结果。
    /// 调用边界：供门、倒计时、死区、碰撞或 Enemy 等关卡本地组件调用。
    /// </summary>
    bool TryResolve(DrawLevelResult result);
}

/// <summary>
/// 关卡本地开场 Handle 的可选接口。
/// 调用边界：由同一 DLevel 内的 DrawLevelCtrl 在开放画线输入前顺序等待，不得访问跨关卡对象。
/// </summary>
public interface IDrawLevelIntroHandler
{
    /// <summary>
    /// 执行本关开放画线前的异步门禁。
    /// 注意事项：实现方应处理自身销毁取消；返回后 DrawPlayCtrl 才会进入 Drawing 阶段。
    /// </summary>
    UniTask PlayDrawIntroAsync();
}

/// <summary>
/// 开场 Handle 在失败或手动重开时可选的直接完成态契约。
/// 调用边界：由 DrawLevelCtrl 在重开遮罩期间同步调用；没有实现该接口的普通 Intro 会直接跳过。
/// </summary>
public interface IDrawLevelRetryIntroStateHandler
{
    /// <summary>
    /// 在失败或手动重开创建的新关卡实例中，直接应用开场动画结束后的可玩状态。
    /// 注意事项：调用发生在重开遮罩期间，禁止创建 Tween 或等待；开场含放球流程时还必须
    /// 把球直接放到该流程完成后的有效物理位置。
    /// </summary>
    void ApplyDrawRetryIntroState();
}

/// <summary>
/// 标记必须在进关转场完全揭开后才执行的可见开场门禁。
/// 调用边界：只用于关卡内需要展示给玩家的 Intro；实现方仍通过
/// <see cref="IDrawLevelIntroHandler.PlayDrawIntroAsync"/> 执行并负责自身取消。
/// Reset/Replay 不会重播可见 Intro，因此编译期强制同时提供动画结束后的直接终态。
/// </summary>
public interface IDrawLevelVisibleIntroHandler :
    IDrawLevelIntroHandler,
    IDrawLevelRetryIntroStateHandler
{
}

/// <summary>
/// 标记只在首次进关时执行、Reset/Replay 重建时应跳过的开场门禁。
/// 调用边界：仅用于不承担关卡状态恢复职责的进关等待或装饰性演出。
/// </summary>
public interface IDrawLevelEnterOnlyIntroHandler : IDrawLevelIntroHandler
{
}

/// <summary>
/// 关卡本地组件接收画线生命周期的可选接口。
/// 用于 Actor 自动行动或关卡脚本演出，避免为单关机制新增全局 PlayEvents。
/// </summary>
public interface IDrawStrokeCompletedHandler
{
    /// <summary>
    /// 一笔有效画线完成后调用；实现方必须自行保证关卡演出幂等。
    /// </summary>
    void OnDrawStrokeCompleted(DrawStrokeResult result);

    /// <summary>
    /// 当前画线尝试重置时调用，关卡本地组件应恢复自己的初始状态。
    /// </summary>
    void OnDrawAttemptReset();
}
