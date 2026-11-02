# SupportUsPopup 功能记录

## 需求与背景

- `UI_common_btn_yellow_01` 点击后播放一次激励广告。
- `u_supportus_des_02_txt` 使用本地化 key `u_supportus_des_02` 的文本作为 `string.Format()` 模板，并传入 `DB.GameData.RewardADCount` 展示累计成功获得激励广告奖励的次数。
- 弹窗每次执行 `OnOpen` 时默认刷新计数文本；当前弹窗内广告成功后立即再次刷新。

## 影响文件与系统

- `Assets/Game/UI/SupportUs/Scripts/SupportUsPopup.cs`
  - 管理按钮监听、广告请求状态和计数文本刷新。
- `Assets/Game/UI/SupportUs/Scripts/SupportUsPopup.Generated.cs`
  - 只读依赖；提供 `UiCommonX01Btn`、`UICommonYellow01Btn`、`USupportusDes02Txt` 序列化字段，本次未修改生成文件。
- `Assets/Game/UI/SupportUs/Resources/SupportUsPopup.prefab`
  - 只读依赖；静态 YAML 已确认上述三个字段均有非空绑定，本次未修改 prefab。
- `Assets/Game/Scripts/Ads/RewardAdHelper.cs`
  - 现有统一广告入口；负责等待弹窗、成功/失败提示和业务结果回调，不写入观看次数。
- `Assets/Game/Scripts/SDK/Services/Ads/GameAdsController.cs`
  - 现有计数写入入口；激励视频 `OnAdDisplayed` 时累加 `RewardADCount` 并调用 `DB.SaveGameData()`。
- `Assets/Game/DB/GameData.cs`、`Assets/Game/DB/GameData_Partial.cs`
  - 现有累计次数存档字段与公开属性依赖，本次弹窗只读取，不直接写入。
- I2 Localization
  - 运行时读取 `u_supportus_des_02` 作为格式化模板。

## 原有行为基线

- `SupportUsPopup` 仅调用 `BaseUIView` 的 `OnOpen`、`OnCreate`、`OnClose`，没有按钮监听、广告播放或描述文本刷新逻辑。
- 脚本内原有“注册按钮”占位 TODO 在功能完成后已不再符合代码事实，因此随实现移除。

## 修改后行为

1. `OnOpen` 重置广告请求状态、去重绑定关闭/黄色按钮、恢复按钮交互，并刷新描述文本。
2. 描述文本通过 `LocalizationManager.GetTranslation("u_supportus_des_02")` 获取模板，再执行 `string.Format(template, DB.GameData?.RewardADCount ?? 0)`；若运行时缺少该 key，则降级显示次数数字，避免 `string.Format(null, ...)` 导致弹窗打开异常。
3. 点击黄色按钮时，使用广告位字符串 `SupportUs` 调用 `RewardAdHelper.TryToShowRewardAd()`。
4. 广告请求期间锁定关闭按钮和黄色按钮，防止重复请求或在原生广告覆盖层返回前由当前弹窗主动关闭。
5. 广告实际开始展示时，`GameAdsController` 完成次数累加和 GameData 保存；广告结果返回弹窗后恢复按钮并重新读取计数。
6. 展示失败不会增加计数；广告已展示但未领奖时仍按既定展示次数口径计数，结果回调后也会刷新。关闭弹窗时解除监听，避免 UI 实例复用造成重复回调。

## 数据、UI、存档与平台流程

```text
OnOpen
  -> 读取 DB.GameData.RewardADCount
  -> 获取 u_supportus_des_02
  -> string.Format(template, count)
  -> 写入 USupportusDes02Txt.text

黄色按钮
  -> 锁定弹窗按钮
  -> RewardAdHelper.TryToShowRewardAd("SupportUs")
  -> GameSDKService / WaitingPopup 处理广告加载与原生覆盖层
  -> OnAdDisplayed：GameAdsController 累加计数并保存
  -> SupportUsPopup 结果回调恢复按钮并刷新文本
```

- 本次不新增全局事件、不新增存档字段、不修改广告 SDK 或等待遮罩所有权。
- Android 与 iOS 共用现有 `RewardAdHelper` / `GameSDKService` 流程；按钮锁定只依赖广告结果回调，不假设原生覆盖期间 Unity 帧或 Tween 会继续推进。

## 验证证据

- 2026-08-19：静态检查 `SupportUsPopup.prefab`，`UiCommonX01Btn`、`UICommonYellow01Btn`、`USupportusDes02Txt` 均绑定到非零 fileID。
- 2026-08-19：最终执行 `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1 /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly`，结果为 0 个错误、106 个项目既有警告。
- 2026-08-19：静态检查确认 `RewardADCount` 仅由 `GameAdsController` 的 `OnAdDisplayed` 写入；`RewardAdHelper` 不在奖励成功时重复累计。

## 剩余风险与未验证项

- `uncertain`：当前仓库文本搜索以及 `Luban/Configs/Datas/#Language.xlsx` 压缩内容中未找到 `u_supportus_des_02`；代码已对缺失 key 做数字文本降级，但仍需在实际运行时语言包确认 key 存在，并且翻译文本包含与单个整数参数匹配的 `{0}` 占位符。
- 未在 Unity PlayMode 中实际打开弹窗并播放广告；需手工验证打开时的初始次数、广告展示后 `+1`、展示失败不增加、已展示但未领奖仍保留次数、按钮恢复以及关闭/重开后的持久化显示。
- 未完成 Android/iOS 真机广告覆盖层验证；需分别确认广告成功、失败、取消、切后台返回和网络不可用路径。
- `SupportUs` 为本弹窗局部广告位字符串，当前生成的 `Config.Game.RewardPlacement` 尚无同名枚举；如广告后台要求预注册广告位，需要同步后台或配置源后再生成枚举。

## 回滚路径

- 回滚 `SupportUsPopup.cs` 中的按钮绑定、广告请求和 `RefreshDescriptionText()` 调用，即可恢复为空生命周期弹窗。
- 不需要回滚或迁移已有 `RewardADCount` 存档；本弹窗不拥有计数写入逻辑，也不改变其它广告入口的行为。

## 2026-08-19：ClickItem、OnTrigger2D API 与随机感谢提示

### 需求、基线与新行为

- 新增 `SupportUsCtrl.OpenSupportUsPopup(Transform, Vector2)`，签名与
  `ClickItemEventHandle.OnClickEvent` 的 `UnityEvent<Transform, Vector2>` 一致，仅负责调用 `SupportUsPopup.Create()`。
- 补充 `SupportUsCtrl.OpenSupportUsPopup(Collider2D)`，签名与 `OnTrigger2DHandle.OnEnter`、`OnExit` 使用的
  `Collider2DEvent` 一致；两个公开 API 共用同一私有打开入口。
- `SupportUsCtrl` 不查找、不订阅按钮，也没有 `RequireComponent`；ClickItem 的检测、次数和 prefab 绑定由调用方维护。
- 原广告成功统一展示 `U_HurryTips_RewardVideo_succeed`。现在每次请求从
  `U_SupportUs_Thanks_Des_01` 至 `U_SupportUs_Thanks_Des_08` 中随机选一个 key，作为 `RewardAdHelper` 的
  `success` 参数；只有广告成功时才沿用既有 `LocalizationManager -> SystemTips.Show` 展示。

### 影响范围与流程

- 代码仅新增 `Assets/Game/UI/SupportUs/Scripts/SupportUsCtrl.cs`，并修改
  `Assets/Game/UI/SupportUs/Scripts/SupportUsPopup.cs` 的成功提示选择。
- 未修改任何 ClickItem、设置页脚本或 prefab，也未新增存档、全局事件、广告位、analytics 或 SDK 分支。

```text
ClickItemEventHandle.OnClickEvent
  -> SupportUsCtrl.OpenSupportUsPopup(Transform, Vector2)
  -> SupportUsPopup.Create()

OnTrigger2DHandle.OnEnter / OnExit
  -> SupportUsCtrl.OpenSupportUsPopup(Collider2D)
  -> SupportUsPopup.Create()

SupportUsPopup 黄色按钮
  -> 随机感谢 key
  -> RewardAdHelper
  -> 广告成功：SystemTips.Show(本地化感谢文本)
```

### 验证、风险与回滚

- 静态确认两个公开 API 分别匹配 `ClickItemEventHandle` 与 `OnTrigger2DHandle` 的 UnityEvent 参数，8 个 key 各出现一次。
- 2026-08-19：执行完整 `Assembly-CSharp.csproj` 构建，结果为 `0 error / 865 个工程现有 warning`。
- 2026-08-19：补充 Trigger API 后执行 `BuildProjectReferences=false` 定向构建，结果为
  `0 error / 106 个工程现有 warning`。
- `uncertain`：调用方 prefab 的 UnityEvent 绑定、8 个 key 的运行时语言包内容、广告成功/失败/取消及 Android/iOS
  原生覆盖层仍需 PlayMode/真机验证。
- 回滚时删除 `SupportUsCtrl`，并恢复 `SupportUsPopup` 的固定成功 key；不涉及数据迁移。

## 2026-09-11：阻止 SupportUsPopup 重复打开

### 运行时证据与原行为基线

- Unity PlayMode 堆栈确认：`SupportUsPopup` 已位于 Popup 层栈顶时，弹窗上的后续点击仍可能由世界层
  `ClickItemEventHandle` 命中 `SupportEnter`，再次沿
  `SupportUsCtrl.OpenSupportUsPopup() -> SupportUsPopup.Create()` 调用 `UIManager.OpenUI()`。
- `UIManager` 在发现同一实例已经位于 Popup 层栈顶后会返回现有实例，但同时输出
  `[UIManager] OpenUI aborted: UI SupportUsPopup is already on top of layer Popup.` 错误；因此没有重复入栈，
  但 SupportUs 打开请求确实被重复触发。
- `SupportUsCtrl` 的点击和 Trigger 重载共用 `SupportUsPopup.Create()`，将幂等门禁放在弹窗工厂可覆盖现有全部打开入口。

### 修改后行为与影响边界

- `SupportUsPopup.Create()` 先幂等初始化 UIManager，再通过 `UIManager.GetUI<SupportUsPopup>(nameof(SupportUsPopup))` 获取已注册实例；
  实例存在且 `IsOpen=true` 时直接返回，不再调用 `OpenUI`，也不重复执行 `OnOpen`、按钮绑定和文本刷新。
- 弹窗执行 `OnClose()` 后 `IsOpen=false`，后续合法点击仍可正常重新打开，不采用永久一次性的 ClickItem 点击次数限制。
- 点击入口与 Trigger 入口自动共享该幂等行为；未修改 `SupportEnter.prefab`、共享 `ClickItemEventHandle`、UIManager、
  Popup 遮罩、广告、存档、配置、打点或平台分支。

### 验证、剩余风险与回滚

- 已依据用户提供的 PlayMode 堆栈确认重复请求来源和 UIManager 栈顶防护位置。
- 静态幂等顺序断言通过：UIManager 初始化、`GetUI` 查询、`IsOpen` 判断、`OpenUI` 调用按预期顺序存在；当前仓库仅有
  `SupportUsCtrl` 一处 `SupportUsPopup.Create()` 调用点，其点击和 Trigger 重载共用该入口。
- `git diff --check` 通过；输出仅包含工作区既有的 LF/CRLF 转换提示，没有空白错误。
- 定向执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，
  结果为 `0 error / 123 warning`；未建立修改前 warning 数量基线，因此不把 warning 归因于本次修改。
- `uncertain`：修改后尚未在 Unity PlayMode 实测连续点击、Trigger 与点击同帧触发、关闭后重开，以及 Android/iOS
  横屏多点触控。
- 回滚时删除 `Create()` 中的 `GetUI`/`IsOpen` 判断并恢复直接调用 `OpenUI`；无数据或资源迁移。

## 2026-09-11：关闭动画完成后销毁实例

### 需求与原行为基线

- `SupportUsPopup.prefab` 已启用 `UIScaleAnimation._playCloseAnim`，动画时长为 `0.3s`。
- 关闭按钮原本直接调用 `UIManager.DestroyUI(this)`；该入口会立即移栈、执行 `OnDestroyView() -> OnClose()`、
  卸载资源并销毁对象，不调用 `OnHide()`，因此 Prefab 的关闭动画没有播放。
- `UIManager.CloseUI(this)` 会调用 `OnHide()` 播放动画，但关闭后保留已加载实例；本次需求明确不保留该缓存实例。

### 修改后行为与 UI/资源流程

- Bear.UI 新增 `UIManager.CloseAndDestroyUI(BaseUIView)`；现有 `DestroyUI()` 仍保持立即销毁语义，避免改变既有调用方时序。
- 新 API 仅对当前层栈顶 UI 播放关闭动画。动画期间 UI 继续占据栈顶并保留遮罩；全部动画完成后再复用
  `DestroyUI()` 完成移栈、`OnClose()`、Loader 卸载和 GameObject 销毁。非栈顶实例无需展示关闭过程，直接销毁。
- `BaseUIView.HideWithCompletion(...)` 复用现有多个 `IUIAnimation` 的完成计数，不读取或硬编码 Prefab 的 `0.3s`。
  当动画配置为同步完成时，回调会等待虚方法 `OnHide()` 返回后再执行，避免子类后续代码访问已销毁对象。
- `SupportUsPopup` 关闭按钮改用新 API，并以 `_isClosing` 配合按钮不可交互状态阻止动画期间再次关闭或发起广告。
  此时 `IsOpen` 仍为 true，既有 `Create()` 幂等门禁也会拦截世界点击穿透导致的重复打开；动画结束销毁后，下一次合法点击重新加载新实例。
- 本次未修改 Prefab、广告、存档、配置、打点、输入组件或 Android/iOS 平台分支。

```text
SupportUsPopup 关闭按钮
  -> 标记 closing 并锁定按钮
  -> UIManager.CloseAndDestroyUI(this)
  -> BaseUIView.OnHide()
  -> 等待 Prefab 全部关闭动画完成
  -> UIManager.DestroyUI(this)
  -> OnDestroyView() -> OnClose()
  -> Loader 卸载资源并销毁 GameObject
  -> 恢复下一层 UI 或隐藏遮罩
```

### 验证、剩余风险与回滚

- `com.bear.ui.csproj` 独立编译通过：`0 error / 0 warning`。
- Bear.UI 重建后，`Assembly-CSharp.csproj` 定向编译通过：`0 error / 123 warning`；未建立本次修改前 warning
  数量基线，不将这些 warning 归因于本次修改。
- 静态确认 `DestroyUI()` 原有立即销毁代码未改序，`SupportUsPopup` 不再直接调用该入口，关闭时长只来自 Prefab 动画组件。
- `git diff --check` 对本次代码无新增空白问题；命令仍报告任务开始前已有 `en SDF.asset` 尾空格。
- `uncertain`：尚未在 Unity PlayMode 验证实际 `0.3s` 缩放动画、关闭期间连续点击、关闭后重新加载、同层其它 Popup
  插入，以及 Android/iOS 横屏多点触控。
- 回滚时将 `SupportUsPopup.OnCloseBtnClick()` 恢复为 `DestroyUI(this)`，移除 `_isClosing` 门禁；若不再需要通用能力，
  再移除 `UIManager.CloseAndDestroyUI()`、`BaseUIView.HideWithCompletion()` 和对应 Readme 说明。无需迁移数据或资源。
