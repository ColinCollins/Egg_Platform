# Bear UI 模块文档

## 待优化
- OnOPen 应该在隔一帧执行，避免 Create 的时候，有些附着之后，无法调用，因为 Create 的时候已经调用了。

## 概述

Bear UI 是一个完整的 Unity UI 管理系统，提供 UI 栈管理、生命周期管理、数据绑定、多层级管理和遮罩管理功能。模块保持独立性，不依赖其他业务模块。

**版本**: 0.0.1  
**Unity 版本**: 6000.59f2  
**命名空间**: `Bear.UI`

## 目录结构

```
UI/
├── Runtime/                    # 运行时核心代码
│   ├── Core/                   # 核心管理类
│   │   ├── UIManager.cs       # UI 管理器单例
│   │   └── UIStack.cs         # UI 栈管理器
│   ├── Interface/              # 接口定义
│   │   ├── IUIView.cs         # UI 视图接口
│   │   ├── IBindable.cs       # 数据绑定接口
│   │   ├── IUIAnimation.cs    # UI 动画接口
│   │   ├── IUILoader.cs       # UI 加载器接口
│   │   └── IUIPreloadable.cs  # UI 预加载接口（预留）
│   ├── Loader/                 # 加载器实现
│   │   ├── ResourcesUILoader.cs    # Resources 加载器
│   │   └── DefaultUILoader.cs      # 默认加载器
│   ├── Animation/              # 动画相关
│   │   ├── UIAnimationType.cs # 动画类型枚举（预留扩展）
│   │   └── UIScaleAnimation.cs # 缩放动画组件
│   ├── Base/                   # 基类
│   │   └── BaseUIView.cs      # UI 视图基类
│   ├── Layer/                  # 层级管理
│   │   ├── UILayer.cs         # UI 层级枚举
│   │   ├── UILayerManager.cs  # 层级管理器
│   │   └── UIMask.cs          # 遮罩管理
│   ├── Data/                   # 数据绑定
│   │   └── ViewModel.cs       # 视图模型基类
│   └── com.bear.ui.asmdef     # 程序集定义
├── Example/                    # 使用示例
│   ├── SampleUIView.cs        # 示例 UI 视图
│   └── SampleViewModel.cs    # 示例视图模型
└── Editor/                     # 编辑器工具
    └── UIGenerator/            # 从 JSON / PSD 导出生成 UI Prefab
```

## UIGenerator（从 JSON 生成 UI Prefab）

编辑器工具，用于根据设计稿导出的 JSON 在工程中生成带 Canvas 的 UI Prefab。命名空间：`Game.Editor.UIGenerator`。

### 入口

| 方式 | 说明 |
|------|------|
| **Tools → UI Generator → Generate from JSON** | 打开文件选择框，选择 `Assets` 下的 `.json` |
| **Tools → UI Generator → Settings** | 打开/创建 `UIGeneratorSettings` 资源 |
| **Project 中选中 `.json` → 右键 Assets → UI Generator → Generate UI** | 对当前 JSON 执行生成 |

菜单项 **Generate GameTipsPopup / Generate ShopPanel** 等为项目内固定路径示例，可按需修改或复制。

### 配置文件 `UIGeneratorSettings`

- **资源路径**：默认 **`Assets/Resources/UIGeneratorSettings.asset`**（首次打开 Settings 菜单时若不存在会自动创建）。
- **outputPath**：兜底目录（相对 `Assets`）；默认从菜单/右键生成时，Prefab 保存在 **与 JSON 同目录**。
- **defaultTMPFont / defaultLegacyFont**：由是否定义脚本宏 **`TMP_PRESENT`** 决定显示哪一项；安装 TextMesh Pro 时 Unity 一般会带上该宏。
- **defaultSpriteFolder**：切图未放在 JSON 同目录时，在此目录下按名称搜索 Sprite；该目录本身、其下所有名为 `Sprites` 的子目录，以及这些 `Sprites` 目录下的所有子文件夹都会进入兜底检索范围。

生成流程在 `UIGeneratorMenu` 中会读取 Settings，写入 `UIGeneratorConfig` 再调用 `UIGenerator.GenerateUIPrefab`。

### JSON 格式（两种）

1. **传统 Artboard 数组**（`[{ ... }]`）  
   - 含 `origin`、`bounds`、`layers` 等，`LayerData` 使用 `type`（如 `textLayer` / `shapeLayer` / `layer`）及 `x,y,width,height`、`index` 等字段。

2. **PSD / 设计工具导出（document + layers）**  
   - 根对象为 `document` + `layers`，图层含 `isGroup`、`children`、`bounds`（left/top/width/height）、`kind` 等。  
   - 导出常为类 JavaScript 字面量（未加引号的 key、外层 `(...)`），生成器会先经 **`UIGeneratorJsonNormalizer`** 再解析。  
   - **组**会生成空 Rect 容器，子节点挂在组下；坐标按父组中心做相对偏移。

解析顺序：先尝试 **document + layers** 走 PSD 流程；否则再按 **Artboard 数组** 解析。若原始内容带括号导致解析失败，会再尝试用规范化后的字符串解析 Artboard。

### Image 填充与 Sprite 检索

Image 图层优先使用 JSON 的 `src` 字段定位切图；没有 `src` 时走旧版图层名匹配，尝试 `.png` / `.jpg` / `.jpeg` 扩展名。

Sprite 检索顺序：

1. `src` 为 `Assets/...` 时，先直接按该资源路径加载。
2. 按 JSON 同目录尝试 `src` 相对路径和文件名。
3. 在 `defaultSpriteFolder` 兜底范围内尝试直接拼接路径。
4. 若直接拼接失败，在每个候选兜底目录下递归按文件名查找；因此 `Sprites` 文件夹下的所有子文件夹也会被检索。

兜底候选目录由 `defaultSpriteFolder` 本身，以及其下递归发现的所有名为 `Sprites` 的目录组成。多图 Sprite 资源会先按子 Sprite 名称匹配，匹配不到时回退到该资源内第一个 Sprite。

### 文本组件（TMP / 内置 Text）

- 定义 **`TMP_PRESENT`** 时：文本图层使用 **TextMeshProUGUI**，字体来自 TMP 资源或 Settings 中的默认 TMP 字体。  
- 未定义时：使用 **UnityEngine.UI.Text**，默认字体在 Settings 中选 **Legacy Font**，并在 `UIGeneratorConfig` 中对应 **`DefaultLegacyFont`**。

### 图层层级顺序（与 Photoshop 对齐）

Photoshop 图层面板**越靠上越在前**；Unity 同一父节点下**越靠后的子物体越后绘制（越在上层）**。生成器对图层列表**逆序创建**，使叠放顺序与 PS 一致。

### Image 与 Sprite 尺寸

当 **Rect 布局尺寸**（JSON 中的宽高）与 **Sprite.rect** 像素尺寸不一致（允许约 1px 误差）时，会自动将 **Image.type** 设为 **Sliced**；名称含 **`_btn_`** 且 Sprite 带 **border** 时同样会使用 Sliced。

### 生成后处理（扩展）

实现 **`IUIGeneratorPostProcessor`**（需有无参构造函数），在 Prefab **首次保存之后**、销毁场景临时物体**之前**调用；可对场景中的生成根物体继续改层级、挂脚本等，随后管线会**再次 SaveAsPrefab** 写回磁盘。

```csharp
public class MyUIGeneratorPostProcessor : IUIGeneratorPostProcessor
{
    public int Order => 0; // 越小越早执行

    public void OnPostProcessPrefab(GameObject prefabRoot, string prefabAssetPath, string sourceJsonPath)
    {
        // prefabRoot 一般为含 Canvas 的整棵 UI
    }
}
```

也可在其它编辑器代码中直接调用 **`UIGeneratorPostProcessPipeline.Invoke(prefabRoot, prefabAssetPath, jsonPath)`**。

项目侧后处理会在 Prefab 保存后检查同名 UI View 脚本；当脚本类型不存在时，会弹窗询问是否生成新的界面脚本和 `.Generated.cs` 绑定脚本。选择 **Skip** 时只跳过脚本生成，Prefab 仍会按当前生成流程保存。

### 依赖与说明

- **Newtonsoft.Json**（项目已引用）。  
- **TextMesh Pro**：可选；无 TMP 时用内置 **Text**。  
- 节点命名精简（`_txt` / `_btn` 等）与 `BaseAutoUIBind` 等逻辑见生成器源码注释。

---

## 核心组件

### 1. UIManager（UI 管理器）

**位置**: `Runtime/Core/UIManager.cs`

UI 管理器的核心控制器，负责管理所有 UI 的打开、关闭、栈管理等。

#### 主要功能

- **UI 初始化**: 初始化 UI 系统和层级
- **UI 打开**: 通过 `OpenUI<T>()` 方法打开 UI，支持路径加载
- **UI 关闭**: 通过 `CloseUI<T>()` 方法关闭 UI
- **动画销毁**: 通过 `CloseAndDestroyUI()` 播放关闭动画后卸载并销毁 UI
- **栈管理**: 自动管理 UI 栈，支持返回上一页
- **层级管理**: 支持多个 UI 层级
- **遮罩管理**: 自动管理弹窗遮罩
- **加载器管理**: 支持注册多种 UI 加载方式（Resources、Addressables 等）

#### 关键方法

```csharp
// 初始化 UI 系统
public void Initialize()

// 注册 UI 加载器（带优先级）
public void RegisterLoader(IUILoader loader, int priority = 0)

// 取消注册 UI 加载器
public void UnregisterLoader(IUILoader loader)

// 打开 UI（通过路径加载）
public T OpenUI<T>(string path, UILayer layer = UILayer.Normal, bool isShowMask = true) where T : BaseUIView

// 打开 UI（直接创建，向后兼容）
public T OpenUI<T>(UILayer layer = UILayer.Normal, bool isShowMask = true) where T : BaseUIView

// 关闭 UI
public void CloseUI<T>() where T : BaseUIView

// 播放关闭动画，并在动画完成后销毁实例
public void CloseAndDestroyUI(BaseUIView uiView)

// 关闭栈顶 UI
public void CloseTopUI(UILayer layer = UILayer.Normal)

// 关闭所有 UI
public void CloseAllUI()

// 获取 UI 实例
public T GetUI<T>() where T : BaseUIView
```

### 2. UIStack（UI 栈管理器）

**位置**: `Runtime/Core/UIStack.cs`

管理 UI 打开/关闭的栈结构，支持按层级分别管理栈。

#### 关键方法

```csharp
// 压入栈
public void Push(BaseUIView view)

// 弹出栈
public BaseUIView Pop()

// 查看栈顶
public BaseUIView Peek()

// 清空栈
public void Clear()

// 检查栈中是否存在指定 UI
public bool Contains<T>() where T : BaseUIView

// 获取栈中指定类型的 UI
public T Get<T>() where T : BaseUIView
```

### 3. BaseUIView（UI 视图基类）

**位置**: `Runtime/Base/BaseUIView.cs`

所有 UI 视图的抽象基类，定义了 UI 的生命周期方法。

#### 生命周期方法

- `OnCreate()`: UI 创建时调用
- `OnOpen()`: UI 打开时调用
- `OnShow()`: UI 显示时调用
- `OnHide()`: UI 隐藏时调用
- `OnClose()`: UI 关闭时调用
- `OnDestroyView()`: UI 销毁时调用

### 4. UILayer（UI 层级）

**位置**: `Runtime/Layer/UILayer.cs`

定义 UI 层级枚举，支持多个 UI 层级。

#### 层级枚举

```csharp
public enum UILayer
{
    Background = 0,  // 背景层
    Normal = 1,      // 普通层
    Popup = 2,       // 弹窗层
    Top = 3,         // 顶层
    System = 4       // 系统层
}
```

### 5. UIMask（遮罩管理）

**位置**: `Runtime/Layer/UIMask.cs`

管理遮罩显示/隐藏，支持点击遮罩关闭 UI。

#### 关键方法

```csharp
// 显示遮罩
public void Show(Color color, float alpha = 0.5f)

// 隐藏遮罩
public void Hide()

// 设置是否可点击关闭
public void SetClickable(bool clickable, System.Action onClick = null)
```

### 6. ViewModel（视图模型基类）

**位置**: `Runtime/Data/ViewModel.cs`

视图模型基类，提供数据变更通知机制，实现数据驱动 UI 更新。

#### 关键方法

```csharp
// 设置属性值
protected void SetProperty<T>(string propertyName, T value)

// 获取属性值
protected T GetProperty<T>(string propertyName)

// 通知所有属性变更
protected void NotifyAllPropertiesChanged()
```

### 7. IBindable（数据绑定接口）

**位置**: `Runtime/Interface/IBindable.cs`

数据绑定接口，UI 通过此接口绑定视图模型。

#### 接口方法

```csharp
// 绑定视图模型
void Bind(T viewModel)

// 解绑视图模型
void Unbind()

// 数据变更时调用
void OnDataChanged()
```

## 使用示例

### 1. 初始化 UI 系统

```csharp
using Bear.UI;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        UIManager.Instance.Initialize();
    }
}
```

### 2. 创建 UI 视图

```csharp
using Bear.UI;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuView : BaseUIView
{
    [SerializeField] private Button _startButton;

    public override void OnCreate()
    {
        base.OnCreate();
        Debug.Log("MainMenuView Created");
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (_startButton != null)
        {
            _startButton.onClick.AddListener(OnStartButtonClicked);
        }
    }

    public override void OnClose()
    {
        base.OnClose();
        if (_startButton != null)
        {
            _startButton.onClick.RemoveListener(OnStartButtonClicked);
        }
    }

    private void OnStartButtonClicked()
    {
        Debug.Log("Start Button Clicked");
    }
}
```

### 3. 注册 UI 加载器

```csharp
// 初始化 UI 系统
UIManager.Instance.Initialize();

// 注册 Resources 加载器（默认已注册，可自定义基础路径）
UIManager.Instance.RegisterLoader(new ResourcesUILoader("UI/"));

// 现在可以通过路径加载 UI
var uiView = UIManager.Instance.OpenUI<MainMenuView>("MainMenuView", UILayer.Normal);
```

### 4. 打开/关闭 UI

```csharp
// 方式1：通过路径加载 UI（推荐）
var uiView = UIManager.Instance.OpenUI<MainMenuView>("MainMenuView", UILayer.Normal);

// 方式2：直接创建 GameObject 并添加组件（向后兼容）
var uiView = UIManager.Instance.OpenUI<MainMenuView>(UILayer.Normal);

// 打开 UI 但不显示遮罩（isShowMask = false）
var uiView = UIManager.Instance.OpenUI<MainMenuView>("MainMenuView", UILayer.Popup, false);

// 关闭 UI
UIManager.Instance.CloseUI<MainMenuView>();

// 不保留缓存实例：播放 Prefab 配置的关闭动画后销毁 UI
UIManager.Instance.CloseAndDestroyUI(uiView);

// 关闭栈顶 UI（返回上一页）
UIManager.Instance.CloseTopUI(UILayer.Normal);
```

### 5. 使用数据绑定

```csharp
// 创建视图模型
public class PlayerViewModel : ViewModel
{
    private string _playerName;
    private int _level;

    public string PlayerName
    {
        get => GetProperty<string>(nameof(PlayerName));
        set => SetProperty(nameof(PlayerName), value);
    }

    public int Level
    {
        get => GetProperty<int>(nameof(Level));
        set => SetProperty(nameof(Level), value);
    }
}

// UI 视图实现数据绑定
public class PlayerInfoView : BaseUIView, IBindable<PlayerViewModel>
{
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _levelText;

    private PlayerViewModel _viewModel;

    public void Bind(PlayerViewModel viewModel)
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
            if (_nameText != null)
            {
                _nameText.text = _viewModel.PlayerName;
            }
            if (_levelText != null)
            {
                _levelText.text = $"Level: {_viewModel.Level}";
            }
        }
    }

    private void OnViewModelPropertyChanged(string propertyName, object value)
    {
        OnDataChanged();
    }
}
```

## 工作流程

1. **初始化阶段**
   - 调用 `UIManager.Instance.Initialize()` 初始化 UI 系统
   - 系统自动创建 UI 根节点和各个层级

2. **注册 Loader（可选）**
   - 调用 `RegisterLoader()` 注册 UI 加载器
   - 如果不注册，系统会使用默认的 ResourcesUILoader
   - 支持运行时切换不同的 Loader

3. **打开 UI**
   - 使用 `OpenUI<T>(path, layer)` 通过路径加载 UI（推荐）
   - 或使用 `OpenUI<T>(layer)` 直接创建 UI（向后兼容）
   - 系统自动创建 UI 实例（如果不存在）
   - 自动管理 UI 栈
   - 自动显示遮罩（弹窗层）

4. **关闭 UI**
   - 使用 `CloseUI<T>()` 关闭指定 UI
   - 或使用 `CloseTopUI()` 关闭栈顶 UI
   - 不需要缓存实例时，使用 `CloseAndDestroyUI(uiView)` 等待关闭动画结束后销毁
   - 系统自动恢复上一个 UI 的显示
   - 自动隐藏遮罩（如果没有其他 UI）

5. **数据绑定**
   - UI 实现 `IBindable<T>` 接口
   - 创建 ViewModel 实例
   - 调用 `Bind()` 方法绑定数据
   - ViewModel 数据变更时自动更新 UI

## 特性说明

- **栈管理**: 支持 UI 栈管理，实现返回上一页功能
- **生命周期**: 提供完整的生命周期方法，支持 UI 创建、打开、显示、隐藏、关闭、销毁
- **层级管理**: 支持多个 UI 层级，不同层级互不影响
- **遮罩管理**: 自动管理弹窗遮罩，支持点击遮罩关闭 UI
- **数据绑定**: 数据驱动设计，UI 不处理业务逻辑
- **加载器系统**: 支持多种 UI 加载方式，可扩展自定义加载器
- **独立性**: 模块保持独立性，不依赖其他业务模块

### 7. IUILoader（UI 加载器接口）

**位置**: `Runtime/Interface/IUILoader.cs`

UI 加载器接口，支持多种加载方式：Resources、Addressables、AssetBundle 等。

#### 接口方法

```csharp
// 同步加载 UI Prefab
GameObject Load(string path);

// 异步加载 UI Prefab
void LoadAsync(string path, Action<GameObject> onComplete);

// 卸载 UI 资源
void Unload(string path);
```

#### 内置实现

**ResourcesUILoader**（`Runtime/Loader/ResourcesUILoader.cs`）
- 基于 Unity Resources 系统的加载器
- 支持自定义基础路径
- 使用示例：
  ```csharp
  // 注册单个 Loader
  var loader = new ResourcesUILoader("UI/");
  UIManager.Instance.RegisterLoader(loader);
  // 加载 Resources/UI/SampleUIView.prefab
  var uiView = UIManager.Instance.OpenUI<SampleUIView>("SampleUIView", UILayer.Normal);

  // 注册多个 Loader，按优先级尝试加载
  // 优先级数字越小，优先级越高
  UIManager.Instance.RegisterLoader(new ResourcesUILoader("UI/Primary/"), 0);
  UIManager.Instance.RegisterLoader(new ResourcesUILoader("UI/Fallback/"), 10);
  // 加载时会先尝试 Primary，失败后尝试 Fallback
  var uiView2 = UIManager.Instance.OpenUI<SampleUIView>("SampleUIView", UILayer.Normal);
  ```

**DefaultUILoader**（`Runtime/Loader/DefaultUILoader.cs`）
- 默认加载器，用于向后兼容
- 不加载资源，直接创建空 GameObject

#### 自定义 Loader

实现 `IUILoader` 接口可以创建自定义加载器：

```csharp
public class AddressablesUILoader : IUILoader
{
    public GameObject Load(string path)
    {
        // 使用 Addressables 加载
        // var handle = Addressables.LoadAssetAsync<GameObject>(path);
        // return Object.Instantiate(handle.Result);
        return null;
    }

    public void LoadAsync(string path, Action<GameObject> onComplete)
    {
        // 异步加载实现
    }

    public void Unload(string path)
    {
        // 卸载资源
    }
}

// 注册自定义 Loader
UIManager.Instance.RegisterLoader(new AddressablesUILoader());
```

### 8. UIScaleAnimation（UI 缩放动画组件）

**位置**: `Runtime/Animation/UIScaleAnimation.cs`

UI 缩放动画组件，继承 MonoBehaviour，可在 Inspector 中配置。用户可以在 UI 界面中绑定此脚本，并指定 RectTransform 来确定动效播放主体。

#### 特性

- 继承 MonoBehaviour，可作为组件添加到 GameObject
- 支持在 Inspector 中配置动画参数
- 可指定目标 RectTransform（不指定则使用当前对象）
- 自动在 `OnShow()` 和 `OnHide()` 时播放动画

#### 配置参数

- **Target Rect Transform**: 动画目标（可选，默认使用当前对象）
- **Duration**: 动画持续时间（秒）
- **Scale From**: 缩放起始值
- **Ease Type**: 缓动类型

#### 使用示例

**方式一：在 Inspector 中手动添加组件**

1. 选择需要添加动画的 UI GameObject
2. 添加 `UIScaleAnimation` 组件
3. 在 Inspector 中配置参数
4. （可选）指定 Target Rect Transform

**方式二：代码中添加**

```csharp
public class MyUIView : BaseUIView
{
    private void Awake()
    {
        base.Awake();
        
        // 添加缩放动画组件
        var animation = gameObject.AddComponent<UIScaleAnimation>();
        // 动画参数可以在 Inspector 中设置
    }
}
```

动画会在 `OnShow()` 和 `OnHide()` 时自动播放。

## 预留功能

### 预加载接口（IUIPreloadable）

已预留预加载接口，后续可实现 UI 预加载和缓存机制。

## 注意事项

1. UI 视图必须继承自 `BaseUIView`
2. 使用数据绑定时，记得在 `OnDestroy()` 中调用 `Unbind()`
3. 不同层级的 UI 栈互不影响
4. 弹窗层和顶层会自动显示遮罩
5. UI 管理器会自动创建单例，无需手动创建

## 依赖项

- Unity UGUI (com.unity.ugui)
- DOTween (com.demigiant.dotween) - 用于动画（预留）

## 更新日志

详见 [Changelog.md](./Changelog.md)

---

**最后更新**: 2026年3月  
**维护者**: Bear UI 团队

