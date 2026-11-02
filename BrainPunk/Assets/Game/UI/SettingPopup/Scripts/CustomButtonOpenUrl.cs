using UnityEngine;

/// <summary>
/// 与本物体上的 <see cref="CustomButton"/> 绑定：点击后用 <see cref="Application.OpenURL"/> 打开配置的链接。
/// 适用于设置页隐私政策、条款等外链按钮。
/// </summary>
[RequireComponent(typeof(CustomButton))]
public class CustomButtonOpenUrl : MonoBehaviour
{
    [Tooltip("完整 URL，例如 https://...")]
    [SerializeField]
    private string url;

    private CustomButton _button;

    private void Awake()
    {
        _button = GetComponent<CustomButton>();
    }

    private void OnEnable()
    {
        // MediaShareItem 需要先登记待补播奖励，再打开外链，因此由它统一接管点击顺序。
        if (_button != null && GetComponent<MediaShareItem>() == null)
            _button.OnClick += OnClickOpenUrl;
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.OnClick -= OnClickOpenUrl;
    }

    private void OnClickOpenUrl(CustomButton _)
    {
        OpenUrl();
    }

    /// <summary>
    /// 打开当前组件配置的外链；供需要在打开前登记恢复状态的同物体流程调用。
    /// </summary>
    public void OpenUrl()
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        Application.OpenURL(url.Trim());
    }
}
