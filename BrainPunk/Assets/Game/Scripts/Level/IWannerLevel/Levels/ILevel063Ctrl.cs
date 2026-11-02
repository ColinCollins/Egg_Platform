using Bear.Logger;
using UnityEngine;

/// <summary>
/// 提供三个 API，控制 Actor 移动速度。每个 API 单独控制一个移动速度。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel063Ctrl : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定第 063 关中接收三档移动速度的 IWanner Actor。
    /// 调用边界：仅由本控制器的三个公开速度 API 使用；留空时会从当前关卡子层级自动查找。
    /// 注意事项：只修改 Actor 的基础移动速度，不直接改写当前刚体速度、移动方向或输入状态。
    /// </summary>
    [Header("References")]
    [Tooltip("第 063 关中接收三档移动速度的 Actor；留空时从当前关卡子层级自动查找")]
    [SerializeField] private Actor_IWanner actor;

    /// <summary>
    /// 用途：配置 SetMoveSpeed1 应用的第一档基础移动速度。
    /// 调用边界：由第 063 关本地 UnityEvent 调用；单位为 Unity 单位/秒。
    /// 注意事项：运行时负值按 0 处理，避免意外反转输入方向。
    /// </summary>
    [Header("Move Speeds")]
    [Tooltip("第一档基础移动速度，单位为 Unity 单位/秒")]
    [Min(0f)]
    [SerializeField] private float moveSpeed1 = 3.3f;

    /// <summary>
    /// 用途：配置 SetMoveSpeed2 应用的第二档基础移动速度。
    /// 调用边界：由第 063 关本地 UnityEvent 调用；单位为 Unity 单位/秒。
    /// 注意事项：运行时负值按 0 处理，避免意外反转输入方向。
    /// </summary>
    [Tooltip("第二档基础移动速度，单位为 Unity 单位/秒")]
    [Min(0f)]
    [SerializeField] private float moveSpeed2 = 5f;

    /// <summary>
    /// 用途：配置 SetMoveSpeed3 应用的第三档基础移动速度。
    /// 调用边界：由第 063 关本地 UnityEvent 调用；单位为 Unity 单位/秒。
    /// 注意事项：运行时负值按 0 处理，避免意外反转输入方向。
    /// </summary>
    [Tooltip("第三档基础移动速度，单位为 Unity 单位/秒")]
    [Min(0f)]
    [SerializeField] private float moveSpeed3 = 7f;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：把第 063 关 Actor 的基础移动速度切换为第一档。
    /// 调用边界：供本关压力按钮或其它本地 UnityEvent 在 PlayMode 调用，可安全重复调用。
    /// 注意事项：只更新后续移动计算使用的速度，不强制改变 Actor 当前输入和刚体速度。
    /// </summary>
    public void SetMoveSpeed1()
    {
        ApplyMoveSpeed(moveSpeed1, nameof(SetMoveSpeed1));
    }

    /// <summary>
    /// 用途：把第 063 关 Actor 的基础移动速度切换为第二档。
    /// 调用边界：供本关压力按钮或其它本地 UnityEvent 在 PlayMode 调用，可安全重复调用。
    /// 注意事项：只更新后续移动计算使用的速度，不强制改变 Actor 当前输入和刚体速度。
    /// </summary>
    public void SetMoveSpeed2()
    {
        ApplyMoveSpeed(moveSpeed2, nameof(SetMoveSpeed2));
    }

    /// <summary>
    /// 用途：把第 063 关 Actor 的基础移动速度切换为第三档。
    /// 调用边界：供本关压力按钮或其它本地 UnityEvent 在 PlayMode 调用，可安全重复调用。
    /// 注意事项：只更新后续移动计算使用的速度，不强制改变 Actor 当前输入和刚体速度。
    /// </summary>
    public void SetMoveSpeed3()
    {
        ApplyMoveSpeed(moveSpeed3, nameof(SetMoveSpeed3));
    }

    #endregion

    #region Internal

    private void ApplyMoveSpeed(float moveSpeed, string sourceApi)
    {
        if (!Application.isPlaying)
        {
            this.LogWarning($"[ILevel063Ctrl] {sourceApi} is only available in PlayMode.");
            return;
        }

        ResolveReferences();
        if (actor == null)
        {
            this.LogWarning($"[ILevel063Ctrl] Actor_IWanner is missing; {sourceApi} cannot apply move speed.");
            return;
        }

        actor.SetMoveSpeed(Mathf.Max(0f, moveSpeed));
    }

    private void ResolveReferences()
    {
        if (actor == null)
            actor = GetComponentInChildren<Actor_IWanner>(true);
    }

    #endregion
}
