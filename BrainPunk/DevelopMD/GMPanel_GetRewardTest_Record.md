# GMPanel GetRewardTest 调试入口记录

## 2026-07-03 GetRewardTest1 / GetRewardTest2

### 需求背景

GMPanel 需要补充 `GetRewardTest1` 和 `GetRewardTest2` 两个调试 API，用于验证 `CommonGetRewardPopup` 对 Coin、Star、Skin 的组合展示与发放行为。

### 影响范围

- 运行时代码：`Assets/Game/UI/GM/Scripts/GMPanel.cs`
- 已存在序列化字段：`Assets/Game/UI/GM/Scripts/GMPanel.Generated.cs` 中的 `RewardTest1Btn`、`RewardTest2Btn`
- 已存在 prefab 绑定：`Assets/Game/UI/GM/Resources/GMPanel.prefab` 中的 `rewardTest1_btn`、`rewardTest2_btn`
- 复用系统：
  - `CommonGetRewardPopup.Create(...)`
  - `FindRandomUnownedSkin()`
  - `ConfigManager.Instance.Tables.TbSkin.DataList`
  - `DB.GameData.OwnedSkin`
  - `PlayCtrl.Instance.Bag`

本次未修改 GMPanel prefab、Skin 配表、存档字段、广告 placement、analytics、IAP 或平台 SDK。

### 原行为基线

- `GMPanel.OnCreate()` 已绑定 `RewardTest1Btn -> GetRewardTest1(...)` 和 `RewardTest2Btn -> GetRewardTest2(...)`。
- `GetRewardTest1(...)` 与 `GetRewardTest2(...)` 原本只有测试注释，没有实际打开通用奖励弹窗。
- `GMPanel.OnClose()` 原本没有解绑 `RewardTest1Btn` 和 `RewardTest2Btn`。

### 新行为

- `GetRewardTest2(...)`：
  - 随机选择一个未拥有且可授予的 Skin。
  - 构造只包含 `GameProps.Skin -> skin.Id` 的奖励字典，并调用 `CommonGetRewardPopup.Create(rewards)` 打开只包含 Skin 的通用奖励弹窗。
  - 不显式传入倍率，使用 `CommonGetRewardPopup` 默认 `multiple = 1`。
- `GetRewardTest1(...)`：
  - 随机选择一个未拥有且可授予的 Skin。
  - 构造 Coin 100、Star 5、`GameProps.Skin -> skin.Id` 的奖励字典。
  - 调用 `CommonGetRewardPopup.Create(rewards, 4)`，用于验证 Skin + Coin + Star 组合奖励和广告 4 倍入口。
- `TryGetRewardTestSkin(...)` 统一处理 Bag 缺失、没有可随机获取 Skin 时的日志和 `SystemTips` 提示。
- `GMPanel.OnClose()` 现在会解绑 `RewardTest1Btn` 和 `RewardTest2Btn`，避免重复打开 GMPanel 后回调叠加。

### 数据、资源、UI 与保存流

1. GMPanel 点击测试按钮后，读取 Skin 配表和 `DB.GameData.OwnedSkin` 生成未拥有 Skin 候选。
2. 找到 Skin 后把 `GameProps.Skin -> skin.Id` 写入 rewards 字典，再调用 `CommonGetRewardPopup`。
3. Skin 图标和发放由 `CommonGetRewardPopup` 内部处理：从 rewards 字典读取 `skin.Id`，查 `Config.Skin`，加载 `TextureResources`，并调用 `Bag.GetNewSkin(skinId)`。
4. Coin / Star 由 `CommonGetRewardPopup` 内部调用 `Bag.AddTool(...)` 发放。
5. `GetRewardTest1` 的广告成功路径只追加 Coin / Star 的 `multiple - 1` 份额外奖励，Skin 不受倍数影响。

### 验证记录

- 已运行：
  - `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly`
- 结果：
  - 0 个错误。
  - 0 个警告。
- 静态检查：
  - `RewardTest1Btn` 和 `RewardTest2Btn` 已在 `GMPanel.Generated.cs` 中存在。
  - `GMPanel.prefab` 中已有 `rewardTest1_btn`、`rewardTest2_btn` 绑定。

### 未验证风险

- 尚未执行 Unity PlayMode，仍需验证点击两个测试按钮后通用奖励弹窗打开、Skin 图标加载、奖励保存和关闭链路。
- 尚未执行广告成功回调，`GetRewardTest1` 的额外 Coin / Star 发放需要在可播放激励视频环境中补验。
- 尚未执行 Android/iOS 横屏 smoke；该入口属于 GM 调试 UI，仍需在调试包中确认触摸链路。

### 回滚路径

- 回滚 `GMPanel.cs` 中 `GetRewardTest1(...)`、`GetRewardTest2(...)`、`TryGetRewardTestSkin(...)` 的实现。
- 同时回滚 `OnClose()` 中 `RewardTest1Btn` / `RewardTest2Btn` 的解绑，即可恢复为按钮无实际通用奖励弹窗测试行为的旧状态。
