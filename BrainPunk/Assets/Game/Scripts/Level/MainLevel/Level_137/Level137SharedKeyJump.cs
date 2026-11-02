using System.Collections.Generic;
using DG.Tweening;
using Game.ItemEvent;

/// <summary>
/// 不重写共享钥匙的飞行：直接执行 ItemJumpToListener，仅给其无 target 的虚拟 Tween 标记本关归属。
/// 这样暂停、洞吸入取消和关卡退出能精确处理它，而不暂停或终止其它关卡/界面的 Tween。
/// </summary>
public sealed class Level137SharedKeyJump : ItemJumpToListener
{
    /// <summary>
    /// 调用共享钥匙飞行实现，并把本次同步创建的 Tween 标记为本关实例所有。
    /// 不复制共享飞行算法，也不负责决定钥匙是否有效。
    /// </summary>
    public override void Execute()
    {
        List<Tween> before = DOTween.PlayingTweens(null);
        base.Execute();
        List<Tween> after = DOTween.PlayingTweens(null);
        if (after == null) return;
        // base.Execute 是同步调用；新增的 Tween 就是共享监听器本次创建的跳跃。
        foreach (Tween tween in after)
            if (before == null || !before.Contains(tween)) tween.SetTarget(transform);
    }
}
