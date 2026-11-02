using System;

namespace Game.Dig
{
    /// <summary>Dig 单次关卡结果；大小失败与胜利只允许提交一个。</summary>
    public enum DigLevelResult { Succeeded, Failed }

    /// <summary>关卡与玩法控制器的边界；加载/HUD/存档不进入关卡脚本。</summary>
    public interface IDigLevelRuntime
    {
        /// <summary>初始化完成才允许揭幕与恢复物理。</summary>
        bool IsRuntimeReady { get; }
        /// <summary>死亡表现开始或胜利条件达成后锁定；胜利延迟期间 HUD 也应立即停止操作。</summary>
        bool IsResultLocked { get; }
        /// <summary>当前挖掘墨水快照；复用 Draw 的只读显示格式，长度单位为 Dig 世界单位。</summary>
        DrawInkState InkState { get; }
        /// <summary>本关是否绑定可展示的提示；TBC 不提供提示。</summary>
        bool HasHint { get; }
        /// <summary>耗墨或增加容量后通知 HUD；不通过跨玩法全局事件广播。</summary>
        event Action<DrawInkState> InkChanged;
        /// <summary>最后五秒的倒计时整数快照；0表示隐藏，TBC始终返回0。</summary>
        int CountdownSeconds { get; }
        /// <summary>倒计时整数变化或隐藏时通知当前Dig HUD；由玩法控制器转发，不广播跨玩法事件。</summary>
        event Action<int> CountdownChanged;
        /// <summary>死亡表现完成或统一胜利延迟结束后仅通知一次，订阅方负责重建或胜利面板。</summary>
        event Action<DigLevelResult> Resolved;
        /// <summary>由新实例的加载方调用一次，失败抛出异常交给加载层回首页。</summary>
        void InitializeRuntime();
        /// <summary>转场及结算输入门禁；关闭时结束当前笔画。</summary>
        void SetInputEnabled(bool enabled);
        /// <summary>冻结物理与局部动画；恢复不复活死亡或已吞噬对象。</summary>
        void SetPaused(bool paused);
        /// <summary>同关资格恢复或广告成功时增加一份基础容量；资格由玩法控制器管理。</summary>
        void AddRewardInk();
        /// <summary>显示/隐藏 Inspector 显式绑定的本关提示对象。</summary>
        void ShowHint(bool visible);
        /// <summary>仅供已获得提示后的金币跳关；不改变正常吞噬通关条件，TBC 必须拒绝。</summary>
        bool TryCompleteByHint();
    }
}
