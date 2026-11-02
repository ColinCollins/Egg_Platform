# DrawGame_GamePlayPanel 商店入口功能与风险评估

## 需求与原行为

- Draw HUD prefab 与生成绑定中已有 `ShopBtn`，但 `DrawGame_GamePlayPanel.OnCreate()` 没有订阅点击事件，脚本也没有商店处理方法，因此按钮点击无业务行为。
- 主线 `GamePlayPanel` 会打开 `ShopPanel`，并依赖 `SwitchGameStateEvent` 恢复主线状态；`DrawPlayCtrl` 不消费该事件，不能直接照搬，否则 Draw 可能在商店关闭后保持暂停。

## 影响文件与系统

- `DrawGame_GamePlayPanel.cs`：绑定 `ShopBtn`，通过当前 `IPlayPauseHost` 暂停 Draw 并打开商店。
- `ShopPanel.cs`：`Create(...)` 新增可选暂停宿主；关闭时直接恢复宿主，或把宿主传给自动出现的奖励提示弹窗。
- `ShopRewardTipPopup.cs`：接收可选暂停宿主，在完整商店弹窗链结束时恢复当前玩法。
- 未修改 Draw HUD prefab、商品配置、奖励发放、广告、IAP、存档、analytics 或平台 SDK。

## 修改后流程

```text
Draw ShopBtn
  -> ActivePauseHost.Pause()
  -> ShopPanel.Create(resumeGame: true, pauseHost)
  -> 关闭 ShopPanel
     -> 无奖励提示：pauseHost.Resume()
     -> 有奖励提示：pauseHost 传给 ShopRewardTipPopup
        -> 奖励提示关闭/领奖完成：pauseHost.Resume()
```

- 商店创建失败时，Draw HUD 立即调用 `Resume()`，避免停留在暂停状态。
- 主线及其它旧调用不传 `pauseHost`，继续使用原 `SwitchGameStateEvent(PLAYING)` 恢复方式。
- 没有新增 `PlayEvents`、每帧逻辑、资源加载或实例化循环。

## 风险与验证

- 已执行目标差异 `git diff --check`，无空白错误。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly`，结果为 0 error / 1120 个工程既有 warning。
- `uncertain`：尚未在 Unity PlayMode 验证普通关闭、自动奖励提示、广告成功/失败、商店创建失败，以及控制器切换期间强制关闭 UI。
- Android/iOS 共用相同暂停宿主路径，没有新增平台分支；横屏触摸、广告返回和 IAP 系统弹窗仍需真机验证。

## 回滚路径

- 删除 Draw HUD 的 `ShopBtn` 绑定和 `OnClickShop()`。
- 删除 `ShopPanel.Create(...)`、`ShopRewardTipPopup.Create(...)` 的可选 `IPlayPauseHost` 参数及关闭时的宿主恢复分支。
- 恢复 `ShopPanel` 与奖励提示弹窗只派发 `SwitchGameStateEvent(PLAYING)` 的原行为；不涉及数据或资源迁移。

## 2026-07-30 PencilSkinPopup 入口

### 需求、基线与修改

- 需求：点击 Draw HUD 的 `pencilShopBtn` 打开 `PencilSkinPopup`。
- 修改前 `DrawGame_GamePlayPanel.Generated.cs` 已声明 `PencilShopBtn`，
  `DrawGame_GamePlayPanel.prefab` 也已把它绑定到 `pencilShop_btn` 的
  `CustomButton`；运行时 `OnCreate()` 没有订阅该按钮，所以点击没有业务行为。
- 修改后 `DrawGame_GamePlayPanel.OnCreate()` 订阅 `PencilShopBtn.OnClick`，
  `OnClickPencilShop(...)` 直接调用既有 `PencilSkinPopup.Create()`。
- 本次不修改自动生成脚本或 HUD prefab，不新增全局事件、资源地址、DB/Save、配置、
  analytics、广告、IAP 或平台分支。

### UI 与玩法边界

- 调用流为：
  `pencilShop_btn -> DrawGame_GamePlayPanel.OnClickPencilShop(...)
  -> PencilSkinPopup.Create() -> Bear UIManager.OpenUI(...)`。
- 本入口不主动暂停或恢复 `IPlayPauseHost`，也不修改 Draw 关卡、墨水、倒计时或输入状态；
  Popup 的层级、遮挡、关闭按钮、列表、购买和选择逻辑继续由 `PencilSkinPopup` 自己负责。
- 当前处理与 HUD 的 Tips 按钮一致，只负责打开目标 Popup；普通 `ShopBtn` 原有暂停宿主链
  不受影响。

### 验证、风险与回滚

- 静态确认 `PencilShopBtn` 的 generated 字段和 prefab 序列化引用均存在，引用目标
  `fileID 6050318230765636880` 为 `pencilShop_btn` 下的 `CustomButton`。
- 定向 `git diff --check` 通过；执行
  `dotnet build Assembly-CSharp.csproj --no-restore -nologo -v:minimal` 通过，
  结果为 `0 error / 0 warning`。
- `uncertain`：尚未在 Unity PlayMode 验证按钮点击、连续点击、Popup 层级、关闭返回、
  Draw 输入和倒计时是否需要产品层面的暂停策略，以及 Android/iOS 横屏触控。
- 回滚时删除 `PencilShopBtn.OnClick` 订阅和 `OnClickPencilShop(...)`；不涉及 prefab、
  配置、资源或存档迁移。
