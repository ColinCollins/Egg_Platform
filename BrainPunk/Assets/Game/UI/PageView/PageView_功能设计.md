# PageView 功能设计

## 1. 组件定位

`PageView` 是一个基于 UGUI `ScrollRect` 的通用分页组件。

V1 的核心目标不是做业务分页容器，而是围绕以下两件事提供稳定能力：

- `ScrollView` 的整页滑动与吸附
- 导航栏按钮驱动的页面切换

`PageView` 负责：

- 管理当前页索引与总页数
- 处理拖拽松手后的分页判断
- 执行整页吸附
- 提供按钮翻页、导航栏跳页接口
- 对外通知页码变化

`PageView` 不负责：

- 业务数据生成
- 存档写入与 `DB.Save()`
- 书页特效、Spine 翻页、Shader 翻页
- 虚拟列表或大数据复用

---

## 2. V1 设计目标

V1 只做最小闭环：

- 支持横向 `ScrollView`
- 支持有限页，不循环
- 支持手势拖拽翻页
- 支持按钮翻页
- 支持导航栏按钮直达指定页
- 支持整页吸附
- 支持外部监听页码变化

设计原则：

- 以 `ScrollRect` 为核心，不依赖业务面板实现
- 以连续滚动进度驱动 `pageHandle`，而不是只在翻页完成后瞬移
- 业务逻辑、视觉逻辑、分页逻辑分离

---

## 3. 适用场景

适合：

- 新手引导多页说明
- 横向 Banner 或卡片翻页
- 活动页多屏切换
- 设置页分页内容
- 图文介绍页滑动浏览

不适合：

- 长列表滚动
- 瀑布流
- 无限循环轮播
- 超大数据量虚拟化场景

---

## 4. 层级建议

建议基础层级：

```text
PageRoot
|- ScrollView
|  |- Viewport
|  |  `- Content
|  |     |- Page_0
|  |     |- Page_1
|  |     `- Page_2
|- NavBar
|  |- NavBtn_0
|  |- NavBtn_1
|  `- NavBtn_2
|- BtnPrev
`- BtnNext
```

说明：

- `Content` 下每个直接子节点视为一个 Page
- `NavBar` 下每个按钮对应一个 Page
- `BtnPrev/BtnNext` 为可选

---

## 5. 核心状态

组件内部建议维护以下状态：

- `CurrentPageIndex`
- `PageCount`
- `IsDragging`
- `IsSnapping`
- `ScrollNormalized`
- `DragStartNormalized`
- `ReleaseVelocity`

状态约束：

- `CurrentPageIndex` 始终限制在 `0 ~ PageCount - 1`
- `ScrollNormalized` 表示滚动内容当前归一化进度
- 吸附期间可选择锁定重复输入

---

## 6. 核心能力

### 6.1 页面识别

- 默认将 `Content` 下的直接子物体视为完整页面
- `PageCount` 由直接子物体数量决定
- 页面增删后可通过 `RefreshPages()` 重新同步

### 6.2 ScrollView 分页

- 拖拽开始时记录起始进度
- 拖拽结束时根据位移和速度判断目标页
- 超过阈值则进入相邻页
- 未超过阈值则回弹到当前页
- 松手后自动吸附到目标页

### 6.3 导航栏跳页

- 导航栏每个按钮直接映射一个页索引
- 点击导航按钮后调用 `SetPage(index, true)`
- 当前页对应的导航按钮进入选中态或禁用态
- 当页面数量少于导航按钮数量时，多余按钮应禁用

### 6.4 按钮翻页

- `PrevPage()`：切到上一页
- `NextPage()`：切到下一页
- 超出边界时不翻页
- 第一页禁用上一页按钮
- 最后一页禁用下一页按钮

### 6.5 跳页

- `SetPage(int index, bool animated = true)`
- 允许外部直接跳到指定页
- 非法页码自动 Clamp

---

## 7. 导航栏规则

导航栏是当前版本的主要视觉反馈入口。

建议规则：

- `navigationButtons[i]` 对应第 `i` 页
- 当前页按钮不可再次点击，或以外层样式标记选中态
- 点击导航按钮以整页吸附方式切换，不直接改业务内容
- 拖拽翻页完成后需要反向刷新导航栏状态

---

## 8. 对外接口建议

建议提供以下公开属性和方法：

```csharp
public int CurrentPageIndex { get; }
public int PageCount { get; }
public bool IsSnapping { get; }
public float ScrollProgress { get; }

public void RefreshPages();
public void SetPage(int index, bool animated = true);
public void PrevPage();
public void NextPage();
```

建议提供以下事件：

```csharp
public event Action<int, int> OnPageChanged;
```

事件说明：

- `OnPageChanged(from, to)`：页码正式变化时触发

---

## 9. Inspector 配置建议

建议序列化以下字段：

```csharp
[SerializeField] private ScrollRect scrollRect;
[SerializeField] private RectTransform viewport;
[SerializeField] private RectTransform content;
[SerializeField] private Button[] navigationButtons;
[SerializeField] private Button prevButton;
[SerializeField] private Button nextButton;
[SerializeField] private float snapDuration = 0.25f;
[SerializeField] private float dragThresholdNormalized = 0.15f;
[SerializeField] private float velocityThreshold = 800f;
[SerializeField] private bool lockInputWhileSnapping = true;
```

字段含义：

- `navigationButtons`：导航栏按钮数组，下标与页索引一一对应
- `snapDuration`：自动吸附动画时长
- `dragThresholdNormalized`：判定是否翻页的最小拖拽比例
- `velocityThreshold`：快速甩动时的翻页速度阈值

---

## 10. 行为约定

默认行为建议如下：

- 每个页面宽度等于 `viewport` 宽度
- `Content` 横向排布
- `ScrollRect.horizontal = true`
- `ScrollRect.vertical = false`
- `ScrollRect.movementType` 建议为 `Clamped`
- 分页组件统一接管最终吸附位置

按钮与拖拽行为的优先级：

- 如果正在吸附且 `lockInputWhileSnapping = true`，忽略按钮重复触发
- 如果正在拖拽，按钮翻页请求可选择忽略
- 拖拽结束后统一进入吸附阶段

---

## 11. 与业务的边界

`PageView` 只维护分页进度和导航栏状态，不处理业务副作用。

业务侧通过事件扩展：

- 刷新标题
- 切换页签文案
- 更新红点
- 播放音效
- 记录最近页码

例如：

- 外层界面在 `OnPageChanged` 中更新当前标题
- 外层界面在 `OnPageChanged` 中同步导航栏之外的其他 UI 装饰

---

## 12. V1 实现边界

V1 建议只做：

- 横向分页
- 有限页
- 按钮翻页
- 导航栏跳页
- 拖拽翻页
- 整页吸附
- 页码变化事件

V1 暂不做：

- 纵向分页
- 无限循环
- 自动轮播
- 懒加载
- 虚拟列表复用
- 内建翻页特效
- 点状分页指示器系统

---

## 13. 后续扩展方向

如果 V1 稳定，后续可扩展：

- 纵向分页
- 循环分页
- 自动轮播
- 点状页码指示器
- 一屏多卡片分页
- 自定义每页宽度
- 翻页音效钩子

---

## 14. 建议落地方式

建议脚本目录：

- `Assets/Game/UI/PageView/Scripts/PageView.cs`

建议推进顺序：

1. 先实现最小版 `PageView.cs`
2. 先打通 `ScrollRect` 分页吸附
3. 再接入 `pageHandle` 实时联动
4. 最后补按钮翻页和事件
