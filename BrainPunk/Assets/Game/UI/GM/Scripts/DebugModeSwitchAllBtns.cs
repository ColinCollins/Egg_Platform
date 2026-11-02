using System;
using Unity.VisualScripting;
using UnityEngine;

/// <remarks>
/// <para><b>非 Debug 模式下，<see cref="SwitchAllBtns"/> 会直接返回，不会对 UI 做任何修改。</b></para>
/// <para>此处「Debug 模式」定义为：在 Unity Editor 中运行，或真机/PC 上为 Development Build（<see cref="Debug.isDebugBuild"/> 为 true）。</para>
/// <para>正式 Release（关闭 Development Build）包中 <see cref="IsDebugModeActive"/> 为 false，便于 GM 入口误调时也不影响玩家界面。</para>
/// </remarks>
public class DebugModeSwitchAllBtns : MonoBehaviour
{
    [SerializeField] private GameObject Gmbtn;

    public static bool isShow = true;

    /// <summary>
    /// 是否允许应用本类型中的调试 UI 开关（Editor 恒为 true；否则同 <see cref="Debug.isDebugBuild"/>）。
    /// </summary>
    public static bool IsDebugModeActive
    {
        get
        {
#if DEBUG_MODE
            return true;
#else
            return false;
#endif
        }
    }

    void Start()
    {
        if (!IsDebugModeActive)
            Destroy(gameObject);

        GetComponent<CustomButton>().OnClick += OnClick;
    }

    private void OnClick(CustomButton btn)
    {
        SwitchAllBtns();
    }

    /// <summary>
    /// 在 <see cref="IsDebugModeActive"/> 为 true 时，统一显隐 <paramref name="panel"/> 下所有 CustomButton；否则不执行。
    /// </summary>
    /// <param name="panel">目标面板；为 null 时不执行。</param>
    /// <param name="show">true：显示并可交互；false：缩放到 0 且不可交互。</param>
    public void SwitchAllBtns()
    {
        if (!IsDebugModeActive)
            return;

        isShow = !isShow;
        if (!Gmbtn)
            return;

        Gmbtn.SetActive(isShow);

        if (!PlayCtrl.Instance.CurrentGamePlayPanel)
            return;

        var root = PlayCtrl.Instance.CurrentGamePlayPanel.transform.GetChild(0);
        
        var group = root.GetComponent<CanvasGroup>();
        if (group == null)
            group = root.AddComponent<CanvasGroup>();
        
        group.alpha = isShow ? 1 : 0;
    }
}
