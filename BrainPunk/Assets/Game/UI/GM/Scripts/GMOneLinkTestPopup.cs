using Bear.UI;
using Game;
using TMPro;
using UnityEngine;

/// <summary>GM OneLink 测试弹窗；集中提供参数／输入 URL 测试、真实系统 URL 显示与主动请求，均复用正式业务门禁。</summary>
public sealed class GMOneLinkTestPopup : BaseUIView
{
    #region Inspector 绑定

    /// <summary>sub1：进关时填玩法 main/draw/iwanner，领奖时填奖励唯一标识；由 Prefab 显式绑定。</summary>
    [SerializeField] private TMP_InputField sub1Input;
    /// <summary>sub2：进关时填资源 Path（可留空回退），领奖时填正整数金币数量或皮肤 ID。</summary>
    [SerializeField] private TMP_InputField sub2Input;
    /// <summary>领奖可选的目标 Guru Uid；留空不限用户，进关测试不使用；由 Prefab 显式绑定。</summary>
    [SerializeField] private TMP_InputField sub3Input;
    /// <summary>完整 URL 或 OneLink 短链接；Android 短码交由 OneLinkManager 调用 SDK 解析。</summary>
    [SerializeField] private TMP_InputField urlInput;
    /// <summary>只读展示当前真实系统 URL；Prefab 显式绑定，不作为手动输入或覆盖地址的来源。</summary>
    [SerializeField] private TMP_InputField systemUrlInput;
    /// <summary>主动请求保存的系统 URL；受理后关闭本弹窗以放行首页门禁，不模拟 ERROR。</summary>
    [SerializeField] private CustomButton systemUrlButton;
    /// <summary>提交 sub1/sub2 测试的按钮。</summary>
    [SerializeField] private CustomButton parametersButton;
    /// <summary>用 sub1/sub2/sub3 测试 getCoin；真实发奖且遵守领取防重。</summary>
    [SerializeField] private CustomButton coinButton;
    /// <summary>用 sub1/sub2/sub3 测试 getSkin；皮肤存在性与用户限制由正式领奖流程核对。</summary>
    [SerializeField] private CustomButton skinButton;
    /// <summary>提交 URL 解析测试的按钮。</summary>
    [SerializeField] private CustomButton urlButton;
    /// <summary>将剪贴板粘贴到 URL 输入框，便于手机测试。</summary>
    [SerializeField] private CustomButton pasteButton;
    /// <summary>关闭本弹窗，不清理此前收到的真实 OneLink 请求。</summary>
    [SerializeField] private CustomButton closeButton;
    /// <summary>显示参数格式、排队或拒绝提示；不把排队当作加载成功。</summary>
    [SerializeField] private TMP_Text statusText;

    private bool _testingUrl;
    private int _openVersion;

    #endregion

    #region 生命周期

    /// <summary>打开时绑定按钮；只在运行态参与输入和 OneLink 调试。</summary>
    public override void OnOpen()
    {
        base.OnOpen();
        if (!Application.isPlaying)
            return;
        _openVersion++;
        SetTestingUrl(false);
        parametersButton.OnClick += TestParameters;
        coinButton.OnClick += TestCoin;
        skinButton.OnClick += TestSkin;
        urlButton.OnClick += TestUrl;
        pasteButton.OnClick += PasteUrl;
        closeButton.OnClick += Close;
        systemUrlButton.OnClick += RequestSystemUrl;
        RefreshSystemUrl();
        statusText.text = "进关：sub1 填玩法，sub2 填资源 Path（可留空）。\n领奖：sub1 填唯一标识，sub2 填金币数量 / 皮肤 ID。\nsub3 仅领奖使用，可填目标 Guru Uid；留空不限用户。\n会真实修改存档，同一奖励标识只能领取一次。";
    }

    /// <summary>关闭时解除本次绑定，避免缓存弹窗重开后重复提交。</summary>
    public override void OnClose()
    {
        _openVersion++;
        if (_testingUrl)
            OneLinkManager.Instance.CancelUrlTest();
        SetTestingUrl(false);
        parametersButton.OnClick -= TestParameters;
        coinButton.OnClick -= TestCoin;
        skinButton.OnClick -= TestSkin;
        urlButton.OnClick -= TestUrl;
        pasteButton.OnClick -= PasteUrl;
        closeButton.OnClick -= Close;
        systemUrlButton.OnClick -= RequestSystemUrl;
        base.OnClose();
    }

    private void Update()
    {
        if (!Application.isPlaying || !IsOpen)
            return;
        RefreshSystemUrl();
    }

    /// <summary>仅运行态的 GM 入口调用；访问边界由现有 GM 隐藏入口和密码负责，不依赖调试宏。</summary>
    public static GMOneLinkTestPopup Create()
    {
        if (!Application.isPlaying)
            return null;
        return UIManager.Instance.OpenUI<GMOneLinkTestPopup>(nameof(GMOneLinkTestPopup), UILayer.Popup);
    }

    #endregion

    #region 测试操作

    private void RefreshSystemUrl()
    {
        string url = OneLinkManager.Instance.SystemUrl;
        string display = string.IsNullOrWhiteSpace(url) ? "（空，尚未收到系统链接）" : url;
        if (systemUrlInput.text != display)
            systemUrlInput.SetTextWithoutNotify(display);
        bool canRequest = !_testingUrl && !string.IsNullOrWhiteSpace(url);
        if (systemUrlButton.Interactable != canRequest)
            systemUrlButton.Interactable = canRequest;
    }

    private void RequestSystemUrl(CustomButton _)
    {
        if (!Application.isPlaying || !IsOpen || _testingUrl)
            return;
        bool started = OneLinkManager.Instance.RequestSystemUrl(out string message);
        statusText.text = message;
        // 受理后由正式链继续等待 SDK；先关 Popup，避免本弹窗挡住 ERROR 补发和后续跳转。
        if (started)
            Close(null);
    }

    private void TestParameters(CustomButton _)
    {
        if (!Application.isPlaying || _testingUrl)
            return;
        FinishTest(OneLinkManager.TestDeepLink(sub1Input.text, sub2Input.text));
    }

    private void TestUrl(CustomButton _)
    {
        if (!Application.isPlaying || _testingUrl)
            return;
        int version = _openVersion;
        SetTestingUrl(true);
        statusText.text = "正在解析链接，最多等待 10 秒；关闭弹窗可取消本次测试。";
        OneLinkManager.TestDeepLinkUrl(urlInput.text.Trim(), (queued, message) =>
        {
            if (version != _openVersion || !IsOpen)
                return;
            SetTestingUrl(false);
            statusText.text = message;
            if (queued)
                Close(null);
        });
    }

    private void TestCoin(CustomButton _) => TestReward("getCoin");

    private void TestSkin(CustomButton _) => TestReward("getSkin");

    private void TestReward(string action)
    {
        if (!Application.isPlaying || !IsOpen || _testingUrl)
            return;
        bool queued = OneLinkManager.TestDeepLinkReward(action, sub1Input.text, sub2Input.text, sub3Input.text, out string message);
        statusText.text = message;
        if (queued)
            Close(null);
    }

    private void SetTestingUrl(bool testing)
    {
        _testingUrl = testing;
        parametersButton.Interactable = !testing;
        coinButton.Interactable = !testing;
        skinButton.Interactable = !testing;
        urlButton.Interactable = !testing;
        pasteButton.Interactable = !testing;
        sub1Input.interactable = !testing;
        sub2Input.interactable = !testing;
        sub3Input.interactable = !testing;
        urlInput.interactable = !testing;
        if (Application.isPlaying)
            RefreshSystemUrl();
    }

    private void FinishTest(bool queued)
    {
        if (!queued)
        {
            statusText.text = "未提交，请检查 sub1/sub2 并查看 [OneLink] 日志。";
            return;
        }
        // 先关闭当前 Popup，让正式首页门禁等待其余弹窗和遮罩收尾。
        Close(null);
    }

    private void PasteUrl(CustomButton _)
    {
        if (Application.isPlaying)
            urlInput.text = GUIUtility.systemCopyBuffer ?? string.Empty;
    }

    private void Close(CustomButton _)
    {
        if (!Application.isPlaying)
            return;
        sub1Input.DeactivateInputField();
        sub2Input.DeactivateInputField();
        sub3Input.DeactivateInputField();
        urlInput.DeactivateInputField();
        systemUrlInput.DeactivateInputField();
        UIManager.Instance.CloseUI(this);
    }

    #endregion
}
