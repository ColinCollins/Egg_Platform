# StartPanel BtnGroups 功能说明

## 需求与背景

- 日期：2026-08-25。
- 需求：参考 `ChoiceLevelPanel`，补齐 `StartPanel` 对 prefab 中 `BtnGroups` 各按钮的运行时控制。
- Prefab 静态层级显示 `BtnGroups` 的四个直接子项依次为 `shop_btn`、`gas_btn`、嵌套 `PostcardEnter`、
  `setting_btn`；其中 `gas_btn` 的生成字段名为 `GasBtn`，美术节点为成就图标。

## 影响范围

- 运行时代码：`Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`。
- 现有生成绑定：`StartPanel.Generated.cs` 的 `ShopBtn`、`GasBtn`、`SettingBtn`。
- 现有资源：`StartPanel.prefab` 的 `BtnGroups` 层级；本次未修改 prefab。
- 复用系统：`ShopPanel`、`TransformPopup`、`GameAccountUtils`、`GameAchievementUtils`、
  `ConfigManager.RemoteConfig.GetOpenGaps()`、iOS `AppleGameCenterAuthAgent` 与 Bear Logger。
- 未新增存档字段、配置键、资源地址、全局事件、广告、内购商品或分析打点。

## 修改前基线

- `SettingBtn` 已在 `StartPanel.OnCreate()` 绑定设置弹窗。
- 嵌套 `PostcardEnter` 已由自身脚本负责显隐、红点和点击，不应由 `StartPanel` 重复绑定。
- `ShopBtn` 与 `GasBtn` 虽有生成字段和 prefab 引用，但 `StartPanel` 没有注册点击回调；成就按钮也未按开关刷新显隐。
- `NoAd_btn` 不在 `BtnGroups` 直接子级，本次不纳入该组控制。

## 修改后行为与 UI 流

1. `StartPanel.OnCreate()` 通过本地绑定方法统一注册设置、商店和成就按钮，绑定前先移除同一回调；
   `OnDestroy()` 对称解绑。明信片入口继续完全交由 `PostcardEnter`。
2. 商店按钮复用 `ChoiceLevelPanel` 行为：打开 `ShopPanel`，并在存在可转换提示道具时继续打开 `TransformPopup`。
3. 成就按钮在 `StartPanel.OnOpen()` 按 `GetOpenGaps()` 刷新显隐；iOS 真机还要求 Game Center 已认证。
4. 点击成就按钮时使用一次性处理中门禁；未登录则先检查平台支持并请求游戏服务登录，成功后打开原生成就 UI。
   不支持、登录失败或异常时使用 Bear Logger 记录，并在 `finally` 恢复门禁。
5. 设置按钮仍打开原 `GameSettingPopup`，玩家可见行为不变。

## 验证

- 静态确认 `BtnGroups` 四个子项与三个生成按钮字段的 prefab fileID 绑定有效；成就按钮子级使用
  `UI_zhuye_img_chengjiu_01`。
- 静态确认 `StartPanel` 没有重复绑定 `PostcardEnter` 点击，也没有把组外 `NoAd_btn` 纳入本次逻辑。
- `dotnet build Assembly-CSharp.csproj --no-restore ...` 通过：`0 error / 118 warning`。
- `git diff --check` 对目标 C# 通过，仅有工作区 LF/CRLF 转换提示。
- `uncertain`：Unity PlayMode 的设置、商店、转换弹窗、成就开关与登录成功/失败流程，以及 Android/iOS 横屏触控尚未验证。

## 剩余风险与回滚

- `GasBtn` 是历史生成命名，职责实际为成就入口；若重命名 prefab 节点，需要重新生成字段并同步本脚本。
- 成就展示依赖外部游戏服务登录和平台原生覆盖层；本次没有改变 SDK 的输入恢复、暂停或生命周期处理。
- 若 `BtnGroups` 层级调整，需重新核对哪些入口由 `StartPanel` 直接拥有，避免与嵌套组件重复绑定。
- 回滚时删除 `BindButtonGroupButtons()`、`UnbindButtonGroupButtons()`、商店/成就处理和 OnOpen 成就显隐刷新，
  并恢复 `SettingBtn` 的原直接绑定；prefab、配置和存档无需回滚。

## 2026-08-26：补齐 NoAdBtn 免广告控制

### 需求与影响范围

- 需求：参考 `ChoiceLevelPanel`，为 `StartPanel` 补齐 NoAd 入口的点击、显隐和购买结果刷新逻辑。
- 运行时代码：`Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`。
- 现有绑定与资源：复用 `StartPanel.Generated.cs` 的 `UIZhuyeNoads01Btn`，其在
  `StartPanel.prefab` 中绑定 `NoAd_btn/UI_zhuye_btn_noads_01/clickArea` 的 `CustomButton`；未修改自动生成文件或 prefab。
- 复用系统：`NoAdsPopup`、`GameManager.Instance.Bag`、`GameProps.NoAds`、`UpdatePropEvent`、
  `OnRestoreSuccessEvent` 与 Bear `EventSubscriber`。
- 未新增存档字段、配置键、商品、资源地址、全局事件、analytics 或平台分支。

### 修改前基线

- Prefab 已包含 NoAd 完整美术和点击区域，但 `StartPanel` 未绑定点击回调，也未按 NoAds 道具数量控制显隐。
- 购买或恢复购买更新 NoAds 道具后，`StartPanel` 没有监听对应事件，因此入口不会在当前面板生命周期内主动刷新。

### 修改后行为与数据/UI 流

1. `StartPanel.OnCreate()` 绑定 NoAd 点击，`OnDestroy()` 对称解绑；点击后复用 `NoAdsPopup.Create()`。
2. `OnOpen()` 查询 Bag 中 `GameProps.NoAds` 数量：数量小于等于 0 时显示入口，大于 0 时隐藏
   `clickArea` 的父节点，从而整组隐藏按钮美术。
3. `OnOpen()` 订阅 `UpdatePropEvent` 和 `OnRestoreSuccessEvent`；仅 NoAds 道具更新或恢复购买成功时刷新显隐。
4. `OnClose()` 与 `OnDestroy()` 清理事件订阅，避免已关闭面板继续响应 Bag/恢复购买事件；再次打开时重新订阅。
5. 数据流只读现有 Bag 数量，不写存档；Android/iOS 横屏共用同一 UI 逻辑。

### 验证

- 已核对 prefab YAML：生成字段指向 `clickArea` 的 `CustomButton`，其直接父节点包含 NoAd 整套按钮美术。
- 已静态确认点击、打开刷新、NoAds 道具事件刷新、恢复购买刷新以及关闭/销毁解绑均存在，未新增逐帧逻辑。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1
  /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly` 通过：`0 warning / 0 error`。
- `uncertain`：尚未执行 Unity PlayMode 的点击弹窗、购买成功、恢复购买成功、关闭后事件隔离，以及 Android/iOS 横屏触控验证。

### 风险与回滚

- 当前显隐依赖 `clickArea` 的直接父节点承载整套 NoAd 美术；如果后续调整 Prefab 层级，应改为显式绑定显示根节点。
- 购买和恢复购买结果仍由既有 IAP/Bag 流程负责，本次只监听结果并刷新入口，不改变交易、回调、遮罩或输入恢复边界。
- 回滚时移除 `StartPanel` 的 NoAd 按钮别名、绑定/解绑、事件订阅、显隐刷新和 `ShowNoAd()`；
  `StartPanel.Generated.cs`、prefab、配置和存档均无需回滚。

## 2026-08-27：监听 iOS Game Center 认证状态刷新成就入口

### 需求、基线与影响范围

- 需求：玩家在 `StartPanel` 已打开期间完成 Game Center 登录后，`GasBtn` 应立即按最新认证状态显示；退出或认证失败时也应及时隐藏。
- 修改前只在 `StartPanel.OnOpen()` 和未认证点击保护分支刷新，面板保持打开时的认证变化不会主动更新入口。
- 运行时代码仅修改 `Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`，复用
  `AppleGameCenterAuthAgent.AuthStateUpdated`、R3 `IDisposable` 与现有 `RefreshAchievementButtonVisibility()`。
- 未修改 `StartPanel.prefab`、RemoteConfig `open_gaps`、Luban、存档、资源地址、全局事件、analytics 或平台 SDK 包。

### 新行为与生命周期

1. `OnOpen()` 先重建 Game Center 认证状态订阅，再执行一次原有显隐刷新，保证打开瞬间和后续变化都覆盖。
2. iOS 真机收到有效认证状态更新时复用原方法；按钮仍只在 `open_gaps=true` 且已认证时显示。
3. `OnClose()` 与 `OnDestroy()` 均释放订阅，再次打开时重新订阅，不增加 `Update()` 或轮询。
4. Android 与 Editor 不创建该订阅，继续只按 `open_gaps` 控制入口；点击登录及成就 UI 流程不变。

### 验证、风险与回滚

- 已静态确认订阅只存在于 `UNITY_IOS && !UNITY_EDITOR`，回调在面板存活期间复用现有显隐函数，并有关闭/销毁双重清理。
- `dotnet build Assembly-CSharp.csproj` 的 Android Editor 常量编译通过：`0 error / 1082 warning`；使用 iOS Player 常量并隔离无关 Editor 项目后，
  `Guru.UniKit.Apple.GameKit.csproj` 为 `0 warning / 0 error`，`Assembly-CSharp.csproj` 为 `124 warning / 0 error`。
- `git diff --check` 对本次两份 C# 与两份 Markdown 通过。
- `uncertain`：仍需在 iOS Sandbox 真机验证面板打开期间登录成功、退出账号、认证失败、重复开关面板和横屏触控；Android 与 Editor 行为也需 PlayMode 回归。
- 回滚时删除 `StartPanel` 的认证订阅字段、订阅/释放方法及生命周期调用，保留原有 `OnOpen()` 显隐刷新即可；无需迁移数据或回退 prefab。
