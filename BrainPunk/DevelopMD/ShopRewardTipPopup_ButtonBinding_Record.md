# ShopRewardTipPopup 按钮绑定生命周期修复记录

## 需求背景

`ShopRewardTipPopup` 的关闭按钮和广告按钮原本在 `OnCreate()` 中绑定，在 `OnClose()` 中解绑。`UIManager.OpenUI(path)` 复用已存在 UI 实例时不会再次调用 `OnCreate()`，只会进入 `OnOpen()` / `OnShow()`。因此如果弹窗曾经通过 `CloseUI(this)` 关闭但未销毁，后续复用实例打开时按钮事件可能保持已解绑状态，表现为按钮功能没有绑定。

## 影响文件与系统

- `Assets/Game/UI/ShopPanel/Scripts/ShopRewardTipPopup.cs`：调整按钮事件绑定入口。
- `Assets/Game/UI/ShopPanel/Resources/ShopRewardTipPopup.prefab`：作为现有序列化引用依据，本次未修改 prefab。
- `Assets/BearGame/com.bear.ui@919f05cf0418/Runtime/Core/UIManager.cs`：作为生命周期依据，本次未修改 UIManager。

## 原行为基线

- `OnCreate()` 执行 `closeBtn.OnClick += OnCloseBtnClick` 与 `adBtn.OnClick += OnAdBtnClick`，随后刷新奖励文本。
- `OnClose()` 执行对应解绑，并在 `needResumeGame` 为 true 时派发 `SwitchGameStateEvent(PLAYING)`。
- `UIManager` 新建实例时调用一次 `OnCreate()`；复用实例时不再调用 `OnCreate()`。

## 修复后行为

- `OnCreate()` 只保留创建期 UI 刷新，不再绑定按钮事件。
- `OnOpen()` 每次弹窗打开时重新绑定关闭按钮和广告按钮；绑定前先执行一次对应解绑，避免重复订阅。
- `OnClose()` 仍负责解绑按钮事件和恢复游戏状态事件，保持关闭清理边界不变。

## 流程边界

- 本次未修改 `ShopRewardTipPopup.Create(...)`、`RewardAdHelper.TryToShowRewardAd(...)`、奖励发放、`PlayCtrl.Instance.Bag.AddTool(...)`、奖励飞行动画、`ShopAdRewardTipAutoShowModule`、每日展示次数、存档、配置、资源加载、analytics、IAP 或平台 SDK 流程。
- 该修复只针对 UI 实例复用时按钮事件丢失的生命周期问题。

## 验证记录

- 已静态检查 `ShopRewardTipPopup.cs`：按钮订阅只在 `OnOpen()` 中发生，`OnClose()` 中执行对应解绑。
- 已确认 `ShopRewardTipPopup.prefab` 当前仍序列化 `adBtn`、`closeBtn`、`rewardAnimCtrl` 与生成字段按钮引用。
- 已执行 `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/ShopRewardTipPopup.cs`，结果无空白错误；仅提示当前 Git 工作区 LF/CRLF 转换。
- 已对本记录文件执行尾随空白静态检查，未发现尾随空白。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。
- Unity PlayMode 手动回归仍待执行。

## 未验证风险

- 尚未在 Unity PlayMode 中验证 `CloseUI(this)` 后复用打开的按钮点击。
- 尚未在 Android/iOS 横屏真机上验证关闭商店自动弹出奖励提示、点击广告按钮、关闭弹窗后的触摸表现。

## 回滚路径

将 `ShopRewardTipPopup.OnOpen()` 中的按钮绑定移回 `OnCreate()`，删除 `OnOpen()` 覆盖方法，即可恢复到修复前生命周期。
