using UnityEngine;
using UnityEngine.UI;
using Bear.UI;

namespace Bear.UI.Example
{
    /// <summary>
    /// 弹窗 UI 示例
    /// 展示如何在弹窗层使用 UI，以及遮罩的使用
    /// </summary>
    public class PopupUIView : BaseUIView
    {
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _messageText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _closeButton;

        private void Awake()
        {
            base.Awake();

            // 添加缩放动画组件（如果 Inspector 中没有配置）
            if (GetComponent<UIScaleAnimation>() == null)
            {
                var animation = gameObject.AddComponent<UIScaleAnimation>();
                // 动画参数可以在 Inspector 中设置
            }

            // 绑定按钮事件
            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        public override void OnCreate()
        {
            base.OnCreate();
            Debug.Log("PopupUIView OnCreate");
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Debug.Log("PopupUIView OnOpen");

            // 设置弹窗内容
            if (_titleText != null)
            {
                _titleText.text = "确认对话框";
            }

            if (_messageText != null)
            {
                _messageText.text = "这是一个弹窗示例，展示弹窗层的使用。点击遮罩或关闭按钮可以关闭。";
            }
        }

        public override void OnShow()
        {
            base.OnShow();
            Debug.Log("PopupUIView OnShow - 弹窗显示，遮罩会自动显示");
        }

        public override void OnHide()
        {
            base.OnHide();
            Debug.Log("PopupUIView OnHide");
        }

        public override void OnClose()
        {
            base.OnClose();
            Debug.Log("PopupUIView OnClose");
        }

        private void OnConfirmClicked()
        {
            Debug.Log("确认按钮被点击");
            UIManager.Instance.CloseUI<PopupUIView>();
        }

        private void OnCancelClicked()
        {
            Debug.Log("取消按钮被点击");
            UIManager.Instance.CloseUI<PopupUIView>();
        }

        private void OnCloseClicked()
        {
            Debug.Log("关闭按钮被点击");
            UIManager.Instance.CloseUI<PopupUIView>();
        }

        public override void OnDestroyView()
        {
            // 清理按钮事件
            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(OnCancelClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(OnCloseClicked);
            }

            base.OnDestroyView();
        }
    }
}

