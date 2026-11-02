# UI 模块使用示例

本目录包含 UI 模块的完整使用示例，展示如何使用 UI 管理系统的各项功能。

## 文件说明

### 1. UITestController.cs
**用途**: 测试控制器，展示如何初始化和使用 UIManager

**主要功能**:
- 初始化 UI 系统
- 打开/关闭 UI
- 演示不同层级的使用
- 演示栈管理功能

**使用方法**:
1. 创建一个 GameObject，添加 `UITestController` 组件
2. 在 Inspector 中配置测试按钮（可选）
3. 运行场景，点击按钮测试 UI 功能

### 2. SampleUIView.cs
**用途**: 示例 UI 视图，展示基础 UI 功能

**主要功能**:
- 展示 UI 生命周期方法的使用
- 展示数据绑定功能
- 展示动画组件的使用

**使用方法**:
1. 创建一个 UI GameObject（需要 RectTransform）
2. 添加 `SampleUIView` 组件
3. 在 Inspector 中配置 UI 元素（Text、Button 等）
4. 添加 `UIScaleAnimation` 组件（可选，代码会自动添加）
5. 通过 `UIManager.Instance.OpenUI<SampleUIView>()` 打开

### 3. SampleViewModel.cs
**用途**: 示例视图模型，展示数据绑定

**主要功能**:
- 定义 UI 需要的数据
- 提供数据变更通知
- 实现数据驱动 UI 更新

**使用方法**:
```csharp
// 创建视图模型
var viewModel = new SampleViewModel();
viewModel.Title = "标题";
viewModel.Score = 100;

// 绑定到 UI
var uiView = UIManager.Instance.OpenUI<SampleUIView>();
uiView.Bind(viewModel);
```

### 4. PopupUIView.cs
**用途**: 弹窗 UI 示例，展示弹窗层的使用

**主要功能**:
- 展示弹窗层的使用
- 展示遮罩的自动管理
- 展示多个按钮的处理

**使用方法**:
```csharp
// 打开弹窗（会自动显示遮罩）
UIManager.Instance.OpenUI<PopupUIView>(UILayer.Popup);

// 关闭弹窗（会自动隐藏遮罩）
UIManager.Instance.CloseUI<PopupUIView>();
```

## 完整使用流程

### 1. 初始化 UI 系统

```csharp
using Bear.UI;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // 初始化 UI 系统（只需要调用一次）
        UIManager.Instance.Initialize();
    }
}
```

### 2. 创建 UI 视图

```csharp
using Bear.UI;
using UnityEngine;
using UnityEngine.UI;

public class MyUIView : BaseUIView
{
    [SerializeField] private Text _titleText;
    [SerializeField] private Button _closeButton;

    private void Awake()
    {
        base.Awake();

        // 添加动画组件（可选）
        if (GetComponent<UIScaleAnimation>() == null)
        {
            gameObject.AddComponent<UIScaleAnimation>();
        }

        // 绑定按钮事件
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        // UI 打开时的逻辑
    }

    private void OnCloseClicked()
    {
        UIManager.Instance.CloseUI<MyUIView>();
    }
}
```

### 3. 打开 UI

```csharp
// 打开普通层 UI
var uiView = UIManager.Instance.OpenUI<MyUIView>(UILayer.Normal);

// 打开弹窗层 UI（会自动显示遮罩）
var popupView = UIManager.Instance.OpenUI<PopupUIView>(UILayer.Popup);
```

### 4. 使用数据绑定

```csharp
// 创建视图模型
public class MyViewModel : ViewModel
{
    public string Title
    {
        get => GetProperty<string>(nameof(Title));
        set => SetProperty(nameof(Title), value);
    }
}

// UI 视图实现数据绑定
public class MyUIView : BaseUIView, IBindable<MyViewModel>
{
    [SerializeField] private Text _titleText;
    private MyViewModel _viewModel;

    public void Bind(MyViewModel viewModel)
    {
        Unbind();
        _viewModel = viewModel;
        if (_viewModel != null)
        {
            _viewModel.OnPropertyChanged += OnDataChanged;
            OnDataChanged();
        }
    }

    public void Unbind()
    {
        if (_viewModel != null)
        {
            _viewModel.OnPropertyChanged -= OnDataChanged;
            _viewModel = null;
        }
    }

    public void OnDataChanged()
    {
        if (_titleText != null && _viewModel != null)
        {
            _titleText.text = _viewModel.Title;
        }
    }

    private void OnDataChanged(string propertyName, object value)
    {
        OnDataChanged();
    }
}

// 使用
var viewModel = new MyViewModel();
viewModel.Title = "新标题";

var uiView = UIManager.Instance.OpenUI<MyUIView>();
uiView.Bind(viewModel);
```

### 5. 配置动画

**方式一：在 Inspector 中配置**
1. 选择 UI GameObject
2. 添加 `UIScaleAnimation` 组件
3. 在 Inspector 中设置参数：
   - Duration: 动画持续时间
   - Scale From: 缩放起始值
   - Ease Type: 缓动类型
   - Target Rect Transform: 动画目标（可选）

**方式二：代码中添加**
```csharp
var animation = gameObject.AddComponent<UIScaleAnimation>();
// 参数可以在 Inspector 中设置
```

## 注意事项

1. **UI 视图必须继承 BaseUIView**
2. **使用数据绑定时，记得在 OnDestroyView 中调用 Unbind()**
3. **不同层级的 UI 栈互不影响**
4. **弹窗层和顶层会自动显示遮罩**
5. **动画组件会在 OnShow() 和 OnHide() 时自动播放**

## 常见问题

**Q: 如何自定义动画目标？**
A: 在 `UIScaleAnimation` 组件的 Inspector 中设置 `Target Rect Transform` 字段。

**Q: 如何关闭当前 UI？**
A: 调用 `UIManager.Instance.CloseUI<T>()` 或 `UIManager.Instance.CloseTopUI(layer)`。

**Q: 如何实现返回上一页？**
A: 调用 `UIManager.Instance.CloseTopUI(layer)` 关闭栈顶 UI。

**Q: 遮罩什么时候显示？**
A: 弹窗层（Popup）和顶层（Top）的 UI 打开时会自动显示遮罩。

