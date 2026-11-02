# UIManager 跨层共享遮罩功能与风险记录

## 2026-08-05：Popup 与 Top 遮罩去重

### 需求与背景

Bear UI 的 `Popup` 与 `Top` 层同时打开默认遮罩时，原实现会为两个层分别创建一张黑色 `UIMask`。在默认 `MaskAlpha = 0.5` 下，两张遮罩叠加后的等效遮黑度为 `1 - (1 - 0.5)² = 0.75`，玩家可见结果明显比单弹窗更黑。

本次目标是在不修改现有 `OpenUI(path, layer, isShowMask, isCloseByMask)` 调用签名和业务弹窗调用点的前提下，使 Popup 与 Top 同时存在时最多只渲染一张 Bear UI 遮罩，并保持原有遮罩淡入、点击阻断与层栈关闭入口。

### 影响文件与系统

- 运行时代码：`Assets/BearGame/com.bear.ui@919f05cf0418/Runtime/Core/UIManager.cs`
- 复用但未修改：`Assets/BearGame/com.bear.ui@919f05cf0418/Runtime/Layer/UIMask.cs`
- 复用但未修改：`Assets/BearGame/com.bear.ui@919f05cf0418/Runtime/Layer/UILayerManager.cs`
- 影响范围：Bear UI 的 Popup/Top 遮罩创建、层级转移、点击关闭回调和关闭后的遮罩恢复。
- 不涉及 prefab、场景、存档、配置、资源地址、事件、打点、广告、IAP、RemoteConfig 或平台 SDK。

### 修改前行为基线

- `UIManager` 通过 `_layerMasks` 为 Popup 与 Top 分别缓存一张 `UIMask`。
- `OpenUI` 默认 `isShowMask = true`，因此 Popup 上再打开默认 Top 弹窗时，两张 0.5 黑色遮罩会同时参与 alpha 混合。
- 同一层只复用本层遮罩；层栈非空时继续保留该层的遮罩状态，栈清空后调用 `HideMask(layer)`。
- 点击关闭仍由最近一次该层 `ShowMask(layer, true)` 设置的 `CloseTopUI(layer)` 回调负责。
- Draw 进关链依赖先清空 Popup 栈触发 `HideMask(Popup)`，等待一帧后再打开 `isShowMask:false` 的 `EnterLevelLoading`。

### 修改后行为

- `UIManager` 只维护一个 `_sharedMask`，并用 `_layerMaskStates` 保留 Popup/Top 各自是否请求遮罩及是否允许点击关闭。
- Top 与 Popup 都有有效请求时，Top 优先成为遮罩 owner；共享遮罩被移动到 Top 根节点并放到首个 sibling，使其位于 Top 内容下方、Popup 内容上方。
- Top 请求解除后，如果 Popup 请求仍有效，同一张遮罩直接移回 Popup 根节点；转移过程不调用 `Hide/Show`，因此不会重复淡入，也不会出现两张遮罩交叉叠加。
- 所有支持层都没有有效请求时才调用 `UIMask.Hide()`。
- 现有层级级别的遮罩继承语义保持不变：同层栈未清空时遮罩请求继续存在；`isShowMask:false` 不会创建或覆盖该层请求。
- `isCloseByMask=true` 继续调用当前遮罩 owner 层的 `CloseTopUI(ownerLayer)`；为 false 时遮罩只阻断点击。

### UI 与生命周期流

1. Popup 首次请求遮罩：创建共享 Mask，挂到 Popup 根节点首位并执行原 `UIMask.Show()`。
2. Top 请求遮罩：登记 Top 状态，把仍处于显示状态的同一 Mask 转移到 Top 根节点首位，不重置 alpha 动画。
3. Top 栈清空：撤销 Top 状态；Popup 仍有请求时把 Mask 移回 Popup，否则执行原淡出隐藏。
4. `CloseUI`、`CloseTopUI`、`CloseAllUI`、`DestroyUI` 仍沿原栈为空分支调用 `HideMask(layer)`，业务 UI 生命周期顺序未改。
5. `ReleaseInstance()` 清理层状态并置空共享 Mask 引用；实际 Mask GameObject 随 `UIRoot` 销毁。

### 性能与移动端边界

- Popup/Top 同时使用遮罩时，常驻 `Image + Button + UIMask.Update` 实例由最多两组降为一组。
- 没有新增每帧扫描、资源加载、Instantiate 循环或全局事件；状态刷新只发生在 UI 打开、关闭、销毁与遮罩 owner 切换时。
- RectTransform 继续使用全屏锚点，沿用各层相同的 CanvasScaler 参数；Android/iOS 横屏分辨率适配逻辑未改。

### 已执行验证

- 使用 CodeGraph 和磁盘源码核对 `UIManager.OpenUI/CloseUI/CloseTopUI/CloseAllUI/DestroyUI`、`UILayerManager` Canvas 排序和 `UIMask` 淡入淡出链。
- 静态搜索确认 `_layerMasks` 没有外部脚本引用；Draw 转场只依赖 Popup 栈清空后的 `HideMask` 行为。
- `git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo` 通过：0 个错误、1119 个警告。未建立修改前警告数量基线，因此这些警告不作为本次新增或已消除问题的结论。

### 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 目视验证 Popup 单独、Top 单独、Popup+Top 同时显示时的最终亮度和首帧 sibling 顺序。
- `uncertain`：尚未验证 Top 关闭后共享遮罩移回 Popup 时是否在具体弹窗动画、Canvas override 或第三方 UI 组件下出现单帧闪烁。
- `uncertain`：尚未覆盖同层多弹窗、`isShowMask=false` 叠加、`isCloseByMask=true/false`、`CloseAllUI`、`DestroyAllUI` 与 Draw `EnterLevelLoading` 的完整 PlayMode 矩阵。
- `uncertain`：尚未执行 Android/iOS 横屏触摸拦截、刘海屏/超宽屏适配和代表性设备性能 smoke。
- 当前仍沿用原来的“层级持有遮罩请求”语义，没有把遮罩配置升级为逐 UI 栈条目配置；同层不同弹窗使用不同 `isCloseByMask` 策略时，仍应执行回归验证。

### 回滚路径

- 将 `UIManager` 的 `_sharedMask + _layerMaskStates` 恢复为原 `_layerMasks` 字典，并恢复原 `ShowMask/HideMask` 的逐层创建逻辑即可回滚。
- 本次没有修改 `UIMask`、prefab、配置、存档或业务弹窗调用点，因此回滚不需要资源迁移或数据兼容处理。

## 2026-09-22：OneLink 只读阻塞查询

- `UIManager.HasBlockingPopup` 只读 Popup/Top 栈、关闭销毁集合、共享遮罩 GameObject 活跃状态；未初始化视为阻塞，常驻 System GMPanel 不作为弹窗。遮罩使用 GameObject 状态以涵盖 `Hide` 后尚未完成的淡出。
- 此查询供 StartPanel 消费 OneLink 前使用，不开关 UI、不变更遮罩 ownership。模拟测试覆盖 Popup/Top、关闭销毁、遮罩淡出、常驻 System 和未初始化状态；实际 Unity 动画时序为 `uncertain`。
- 完整流程与回滚见 `Assets/Game/Scripts/SDK/Services/Core/OneLink功能与测试记录.md`；撤销 OneLink 调用后可移除此只读属性。
