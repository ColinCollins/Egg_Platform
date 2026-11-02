using UnityEngine;
using Bear.UI;

namespace Bear.UI.Example
{
    /// <summary>
    /// UI 测试控制器
    /// 展示如何使用 UIManager 打开和关闭 UI
    /// </summary>
    public class UITestController : MonoBehaviour
    {
        [Header("测试按钮")]
        [SerializeField] private UnityEngine.UI.Button _openSampleUIButton;
        [SerializeField] private UnityEngine.UI.Button _openPopupUIButton;
        [SerializeField] private UnityEngine.UI.Button _closeTopUIButton;
        [SerializeField] private UnityEngine.UI.Button _closeAllUIButton;

        private SampleViewModel _sampleViewModel;

        private void Start()
        {
            // 初始化 UI 系统
            UIManager.Instance.Initialize();

            // 注册 UI 加载器（可选，如果不注册则使用默认的 ResourcesUILoader）
            // 示例：使用 ResourcesUILoader，基础路径为 "UI/"
            UIManager.Instance.RegisterLoader(new ResourcesUILoader(""));

            // 创建视图模型
            _sampleViewModel = new SampleViewModel();
            _sampleViewModel.Title = "测试 UI";
            _sampleViewModel.Score = 100;

            // 绑定按钮事件
            if (_openSampleUIButton != null)
            {
                _openSampleUIButton.onClick.AddListener(OnOpenSampleUIClicked);
            }

            if (_openPopupUIButton != null)
            {
                _openPopupUIButton.onClick.AddListener(OnOpenPopupUIClicked);
            }

            if (_closeTopUIButton != null)
            {
                _closeTopUIButton.onClick.AddListener(OnCloseTopUIClicked);
            }

            if (_closeAllUIButton != null)
            {
                _closeAllUIButton.onClick.AddListener(OnCloseAllUIClicked);
            }
        }

        /// <summary>
        /// 打开示例 UI（普通层）
        /// </summary>
        private void OnOpenSampleUIClicked()
        {
            // 方式1：通过路径加载 UI（推荐）
            // var uiView = UIManager.Instance.OpenUI<SampleUIView>("SampleUIView", UILayer.Normal);
            
            // 方式2：直接创建 GameObject 并添加组件（向后兼容）
            var uiView = UIManager.Instance.OpenUI<SampleUIView>(UILayer.Normal);
            
            // 绑定视图模型
            if (uiView != null)
            {
                uiView.Bind(_sampleViewModel);
            }

            Debug.Log("打开 SampleUIView");
        }

        /// <summary>
        /// 打开弹窗 UI（弹窗层）
        /// </summary>
        private void OnOpenPopupUIClicked()
        {
            var uiView = UIManager.Instance.OpenUI<PopupUIView>("PopupUIView", UILayer.Popup);
            Debug.Log("打开 PopupUIView");
        }

        /// <summary>
        /// 关闭栈顶 UI（返回上一页）
        /// </summary>
        private void OnCloseTopUIClicked()
        {
            UIManager.Instance.CloseTopUI(UILayer.Normal);
            Debug.Log("关闭栈顶 UI");
        }

        /// <summary>
        /// 关闭所有 UI
        /// </summary>
        private void OnCloseAllUIClicked()
        {
            UIManager.Instance.CloseAllUI();
            Debug.Log("关闭所有 UI");
        }

        private void OnDestroy()
        {
            // 清理按钮事件
            if (_openSampleUIButton != null)
            {
                _openSampleUIButton.onClick.RemoveListener(OnOpenSampleUIClicked);
            }

            if (_openPopupUIButton != null)
            {
                _openPopupUIButton.onClick.RemoveListener(OnOpenPopupUIClicked);
            }

            if (_closeTopUIButton != null)
            {
                _closeTopUIButton.onClick.RemoveListener(OnCloseTopUIClicked);
            }

            if (_closeAllUIButton != null)
            {
                _closeAllUIButton.onClick.RemoveListener(OnCloseAllUIClicked);
            }
        }
    }
}

