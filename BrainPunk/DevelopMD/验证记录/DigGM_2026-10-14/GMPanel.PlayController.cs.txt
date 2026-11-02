using Game.Play;
using UnityEngine;

/// <summary>
/// GMPanel 的玩法控制器调试入口。
/// 只通过当前活动 Controller 的 IGMFeature 执行命令；无活动 Controller 时拒绝，不感知具体玩法类型。
/// </summary>
public partial class GMPanel
{
    private void NextLevel(CustomButton btn)
    {
        IGMFeature feature = GetActiveGMFeature();
        if (feature == null || !feature.TryEnterNextLevel())
            LogCommandRejected("next level");
    }

    private void LastLevel(CustomButton btn)
    {
        IGMFeature feature = GetActiveGMFeature();
        if (feature == null || !feature.TryEnterPreviousLevel())
            LogCommandRejected("previous level");
    }

    private void SendVictoryEvent(CustomButton btn)
    {
        IGMFeature feature = GetActiveGMFeature();
        if (feature == null || !feature.TryForceVictory(nameof(GMPanel)))
            LogCommandRejected("victory");
    }

    private static IGMFeature GetActiveGMFeature()
    {
        return GameManager.Instance?.ControllerManager?.ActiveController as IGMFeature;
    }

    private static void LogCommandRejected(string command)
    {
        IPlayController activeController =
            GameManager.Instance?.ControllerManager?.ActiveController;
        string mode = activeController?.Mode.ToString() ?? "None";
        Debug.LogWarning(
            $"[GMPanel] Command rejected. mode:{mode}, command:{command}.");
    }
}
