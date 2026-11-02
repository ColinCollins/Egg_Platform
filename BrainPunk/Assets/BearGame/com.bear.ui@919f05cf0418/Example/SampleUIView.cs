using UnityEngine;
using UnityEngine.UI;
using Bear.UI;

namespace Bear.UI.Example
{
    /// <summary>
    /// 示例 UI 视图
    /// 展示如何使用 UI 管理系统的各项功能：
    /// 1. UI 生命周期管理
    /// 2. 数据绑定
    /// 3. 动画组件使用
    /// </summary>
    public class SampleUIView : BaseUIView, IBindable<SampleViewModel>
    {
        [Header("UI 元素")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _scoreText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _updateScoreButton;

        [Header("动画配置（可选）")]
        [Tooltip("指定动画目标 RectTransform，如果不指定则使用当前对象")]
        [SerializeField] private RectTransform _animationTarget;

        private SampleViewModel _viewModel;
        private UIScaleAnimation _scaleAnimation;

        private void Awake()
        {
            base.Awake();

            // 方式一：代码中添加动画组件（推荐在 Inspector 中手动添加）
            // 如果 Inspector 中没有添加 UIScaleAnimation 组件，则自动添加
            _scaleAnimation = GetComponent<UIScaleAnimation>();
            if (_scaleAnimation == null)
            {
                _scaleAnimation = gameObject.AddComponent<UIScaleAnimation>();
                Debug.Log("SampleUIView: 自动添加了 UIScaleAnimation 组件，建议在 Inspector 中手动配置参数");
            }

            // 如果指定了动画目标，设置到动画组件中
            // 这样可以对特定的子对象播放动画，而不是整个 UI
            if (_animationTarget != null && _scaleAnimation != null)
            {
                _scaleAnimation.TargetRectTransform = _animationTarget;
                Debug.Log($"SampleUIView: 动画目标已设置为 {_animationTarget.name}");
            }

            // 绑定按钮事件
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(OnCloseButtonClicked);
            }

            if (_updateScoreButton != null)
            {
                _updateScoreButton.onClick.AddListener(OnUpdateScoreClicked);
            }
        }

        public override void OnCreate()
        {
            base.OnCreate();
            Debug.Log("SampleUIView OnCreate - UI 创建时调用，可以在这里进行初始化");
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Debug.Log("SampleUIView OnOpen - UI 打开时调用，可以在这里准备数据");
        }

        public override void OnShow()
        {
            base.OnShow();
            Debug.Log("SampleUIView OnShow - UI 显示时调用，动画会自动播放");
        }

        public override void OnHide()
        {
            base.OnHide();
            Debug.Log("SampleUIView OnHide - UI 隐藏时调用，关闭动画会自动播放");
        }

        public override void OnClose()
        {
            base.OnClose();
            Debug.Log("SampleUIView OnClose - UI 关闭时调用，可以在这里清理资源");
        }

        public void Bind(SampleViewModel viewModel)
        {
            Unbind();
            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.OnPropertyChanged += OnViewModelPropertyChanged;
                OnDataChanged();
            }
        }

        public void Unbind()
        {
            if (_viewModel != null)
            {
                _viewModel.OnPropertyChanged -= OnViewModelPropertyChanged;
                _viewModel = null;
            }
        }

        public void OnDataChanged()
        {
            if (_viewModel != null)
            {
                if (_titleText != null)
                {
                    _titleText.text = _viewModel.Title;
                }

                if (_scoreText != null)
                {
                    _scoreText.text = $"Score: {_viewModel.Score}";
                }
            }
        }

        private void OnViewModelPropertyChanged(string propertyName, object value)
        {
            OnDataChanged();
        }

        private void OnCloseButtonClicked()
        {
            // 关闭当前 UI（会触发关闭动画）
            UIManager.Instance.CloseUI<SampleUIView>();
        }

        private void OnUpdateScoreClicked()
        {
            // 示例：更新数据（UI 会自动更新，因为数据绑定）
            if (_viewModel != null)
            {
                _viewModel.Score += 10;
                Debug.Log($"分数已更新: {_viewModel.Score}");
            }
        }

        /// <summary>
        /// 更新分数（示例：展示如何更新数据）
        /// </summary>
        /// <param name="newScore">新分数</param>
        public void UpdateScore(int newScore)
        {
            if (_viewModel != null)
            {
                _viewModel.Score = newScore;
            }
        }

        /// <summary>
        /// 立即完成动画（示例：展示如何控制动画）
        /// </summary>
        public void CompleteAnimationNow()
        {
            // CompleteAnimation();
            Debug.Log("动画已立即完成");
        }

        public override void OnDestroyView()
        {
            // 清理数据绑定
            Unbind();

            // 清理按钮事件
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            }

            if (_updateScoreButton != null)
            {
                _updateScoreButton.onClick.RemoveListener(OnUpdateScoreClicked);
            }

            base.OnDestroyView();
        }
    }
}

