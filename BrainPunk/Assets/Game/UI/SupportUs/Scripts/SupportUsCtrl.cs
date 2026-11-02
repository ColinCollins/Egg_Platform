using UnityEngine;

/// <summary>
/// SupportUs 入口控制器：提供 ClickItemEventHandle 可绑定的弹窗 API。
/// </summary>
public sealed class SupportUsCtrl : MonoBehaviour
{
    #region ClickItem API

    /// <summary>
    /// 用途：打开 SupportUsPopup。
    /// 调用边界：由 ClickItemEventHandle.OnClickEvent 在运行态调用。
    /// 注意事项：参数与 ClickItemEventHandle 的 UnityEvent 签名保持一致，本方法不处理点击检测。
    /// </summary>
    public void OpenSupportUsPopup(Transform _, Vector2 __)
    {
        OpenPopup();
    }

    /// <summary>
    /// 用途：打开 SupportUsPopup。
    /// 调用边界：由 OnTrigger2DHandle.OnEnter 或 OnExit 在运行态调用。
    /// 注意事项：参数与 Collider2DEvent 的 UnityEvent 签名保持一致，本方法不处理触发器检测。
    /// </summary>
    public void OpenSupportUsPopup(Collider2D _)
    {
        OpenPopup();
    }

    private static void OpenPopup()
    {
        if (!Application.isPlaying)
            return;

        SupportUsPopup.Create();
    }

    #endregion
}
