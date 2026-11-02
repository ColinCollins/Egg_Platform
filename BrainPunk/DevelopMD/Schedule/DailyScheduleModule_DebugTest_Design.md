# DailyScheduleModule Debug Test 设计

## 1. 当前目标

当前阶段只验证 `DailyScheduleModule` 本身的跨天判定、`GameSchedule` 落库结果，以及几个关键 daily count 的当前值。

本次实际落地目标：

- 观察 `Daily` 当前最后刷新时间
- 观察当前 `GameSchedule` 内两个 daily count
- 一键把 `Daily` 伪装成“昨天已刷新过”
- 一键执行 `ScheduleSystem.CheckAllModules()`
- 直接确认 `DB.GameSchedule["Daily"]` 是否被更新为今天 UTC 0 点

---

## 2. 测试原则

- 不改设备系统时间
- 不新增 fake `ScheduleModule`
- 不新增 fake `IScheduleRefreshable`
- 优先验证 `DailyScheduleModule` 自己的判定与持久化
- GM 入口只服务当前开发调试，不承担完整回滚能力

---

## 3. 为什么只改 `lastRefreshTime`

`DailyScheduleModule` 的真实判定条件只有：

1. 当前真实 `UtcNow` 对应的今天 UTC 0 点
2. `DB.GameSchedule.GetScheduleLastRefreshTime("Daily")`

因此最稳定的调试方式不是改“当前时间”，而是只改：

- `DB.GameSchedule["Daily"]`

把它改成“昨天 UTC 0 点”后，就能得到与玩家隔天重启近似等价的输入条件。

---

## 4. 当前实现方案

当前 GM 测试控制器为：

- `Assets/Game/UI/GM/Scripts/GMDailyScheduleTestController.cs`

当前实现职责：

- 读取 `DB.GameSchedule["Daily"]`
- 读取 `DB.GameSchedule.Daily_interstitialNoAdPopupShowCount`
- 读取 `DB.GameSchedule.Daily_shopFreeCoinShowCount`
- 计算当前 `TodayUtcStart`
- 推导当前结果是“Should Trigger”还是“Should Skip”
- 提供 `Prepare CrossDay + Check`
- 提供 `Refresh Daily Data`
- 生成调试文本并实时刷新 `Now UTC`

当前实现已提供：

- `DailyScheduleModule` 构造函数中统一创建并注册 Daily 子模块，启动链路已完整

当前实现没有提供：

- 恢复测试前数据
- 跨 session 的测试状态持久化

---

## 5. GM 入口设计

控制器使用方式：

- 提供一个 `CustomButton` 模板
- 提供一个按钮父节点
- 提供一个 `TextMeshProUGUI` 文本区域

按钮由脚本自动生成，不需要在 prefab 里手拖多个独立按钮引用。

当前自动生成的按钮只有两个：

- `Prepare CrossDay + Check`
- `Refresh Daily Data`

---

## 6. 当前文本输出

当前调试文本固定展示：

- `Now UTC`
- `Today UTC Start`
- `Daily Last Refresh`
- `Daily Last Refresh (Local)`
- `DailyCount.InterstitialNoAdPopup`
- `DailyCount.ShopFreeCoin`
- `Prediction`
- `Prepared CrossDay`
- `Module Registered`
- `GameSchedule Ready`
- `Last Action`

表现规则：

- `Now UTC` 按固定间隔实时刷新
- 关键字段使用 rich text 着色
- 绿色表示通过/正常
- 红色表示缺失/失败/应关注

---

## 7. 按钮说明

### 7.1 `Prepare CrossDay + Check`

作用：

- 将 `DB.GameSchedule["Daily"]` 改为昨天 UTC 0 点
- 调用 `DB.SaveGameSchedule()`
- 将控制器内存态 `Prepared` 标为 `true`
- 保存成功后，立即执行一次 `ScheduleSystem.Instance.CheckAllModules()`
- 刷新调试文本

用途：

- 一步制造“下一次检查应触发 Daily”的输入条件
- 一步观察执行前后 `Daily` 时间戳是否推进
- 一步观察当前两个 daily count 的值

### 7.2 `Refresh Daily Data`

作用：

- 确保 `DailyScheduleModule` 已注册到 `ScheduleSystem`
- 直接调用 `ScheduleSystem.Instance.CheckAllModules()`
- 读取执行前后的 `Daily` 时间并写入调试结果

用途：

- 用最短路径确认 `Daily` 是否在当前条件下触发

---

## 8. 推荐测试流程

当前推荐流程收敛为一步优先、一步复查：

1. 点击 `Prepare CrossDay + Check`
2. 如需复查，再点击 `Refresh Daily Data`

预期：

- 第一步后，`Daily Last Refresh` 应更新为今天 UTC 0 点
- 第一步后，`Prediction` 应变成 `Should Skip`
- 第二步再次执行时，`Daily Last Refresh` 不应继续变化

---

## 9. 接口

当前 `GMDailyScheduleTestController` 暴露的核心接口为：

```csharp
public sealed class GMDailyScheduleTestController
{
    public void RefreshView();
    public void PrepareCrossDay();
    public void QuickCheckNow();
    public string BuildDebugText();
}
```

---

## 10. 当前限制

### 10.1 没有恢复按钮

当前不再保存 backup，也不提供 `Restore()`。

这意味着：

- 调试写入会真实落到 `DB.GameSchedule`
- 如果要恢复现场，需要重新手动准备目标数据，或通过后续正常 Daily 流程覆盖

### 10.2 业务模块注册已收敛到启动阶段

`DailyScheduleModule` 现在在构造函数中统一创建并注册 `InterstitialNOAdPopupAutoShowModule` 和 `ShopAdRewardTipAutoShowModule`：

```csharp
public DailyScheduleModule()
{
    InterstitialNOAdPopupModule = new InterstitialNOAdPopupAutoShowModule();
    this.Register(InterstitialNOAdPopupModule);

    ShopAdRewardTipModule = new ShopAdRewardTipAutoShowModule();
    this.Register(ShopAdRewardTipModule);
}
```

因此：
- 启动时 `CheckAllModules()` 即可确保所有 Daily 子模块的 `OnScheduleRefresh()` 被调用
- 不再存在"业务模块晚注册导致错过 Daily reset"的问题
- GM 工具的 `PrepareCrossDay + Check` 现在能完整验证 Daily 时间戳推进 **和** daily count 重置

### 10.3 当前更偏向开发调试

当前版本更适合：

- 本地调试
- 判定逻辑回归
- 存档时间验证
- daily count 观察

不适合把它当成“完整玩家重启流程验收”的替代品。

---

## 11. 结论

如果当前目标是“快速确认 Daily 跨天判定是否正确，并顺手看到关键 daily count 当前值”，最小且稳定的方案就是：

1. 只改 `DB.GameSchedule["Daily"]`
2. 直接调 `ScheduleSystem.CheckAllModules()`
3. 观察 `Daily` 时间戳是否更新
4. 观察 `DailyCount.InterstitialNoAdPopup` 和 `DailyCount.ShopFreeCoin`

当前实现不是完整测试平台，而是一个收敛后的 Daily 单点验证工具。对现在这轮开发来说，这个范围更清晰，也更不容易误导使用者。

---

## 12. 2026-09-07 ChestGift 跨天一键测试补充

### 12.1 当前范围

用户要求“点击按钮后即可测试 ChestGiftPopup”。因此 `PrepareCrossDay()` 当前除验证 Daily 外，还承担 ChestGift 的本地跨天
测试准备与直接展示；本节取代第 1、3、4、7.1、8、10.3、11 节中“只改 GameSchedule、只做 Daily 单点验证”的旧结论。

按钮当前按以下顺序同步执行：

1. 使用 `DailyScheduleModule` 的当地自然日算法计算昨天 00:00。
2. 将 `DB.GameSchedule["Daily"]` 与 `DB.GiftData.LastChestGiftPurchaseLocalDay` 都写为该时间并保存。
3. 调用 `ScheduleSystem.CheckAllModules()`，由原 Daily 注册链推进 PlayDays、广告/商店/DailyChallenge/Gift 等模块。
4. 从刷新后的 ChestGift 档位开始跳过已购或无效商品，取得第一个可售档。
5. 通过 `GiftManager` 打开对应 ChestGiftPopup；成功后关闭位于 System 层的 GMPanel，避免 Popup 被遮挡。

`Refresh Daily Data` 保持原行为，只执行 Schedule 检查，不修改 ChestGift 购买日，也不打开弹窗。

### 12.2 数据与边界

- 该调试按钮会真实保存 GameSchedule 和 GiftData，且会触发所有已注册 Daily 子模块；连续点击等价于连续模拟多个自然日，
  `PlayDays` 与 ChestGift 日刷次数会继续累计。
- GM 只回退 ChestGift 的最近购买日，不清除 `PurchaseCache`，不会让永久限购商品恢复未购买状态。
- 功能开关、SKU、Shop/Product/Reward/Resources、全档已购和活动 GiftPopup 互斥仍由原业务层拒绝；失败时不关闭 GMPanel。
- 生产启动弹窗序列没有修改；这里使用显式 GM 打开路径，使同一 Play 会话内已经执行过启动队列后仍可测试。

### 12.3 验证与回滚

- 运行时程序集构建通过：`0 error / 121 warning`；静态确认只修改 GM 控制器与中文记录。
- `uncertain`：Unity PlayMode 一键弹出、连续五档轮换、各失败边界和移动端横屏触控尚未实测。
- 回滚时删除 GiftData 日期回退、ChestGift 解析/打开、GMPanel 关闭和三项调试文本，恢复仅写 GameSchedule 的旧按钮流程；
  已写入的本地调试数据不自动恢复。

---

## 13. 2026-09-07 ShopPanel 每日免费金币跨天补充

- ShopPanel 免费金币使用独立的 `GameSchedule.DailyFreeCoinLastClaimTime` 判断当地自然日资格，不会因 `Daily` 模块时间戳回退自动恢复。
- “跨天测试”现在把该领取时间与 Daily、ChestGift 日期一并回退到昨天当地 00:00，并在 Schedule 检查完成后刷新 `DailyGiftCoin` 红点。
- 已打开的 ShopPanel 会由 `ShopDailyFreeCoinController` 检测领取时间变化并重算卡片，无需关闭重开；未打开时仍在下次 `OnEnable()` 按存档计算。
- 防回拨高水位 `DailyFreeCoinLastObservedTime` 保持不变，调试入口不降低生产保护数据，也不直接授予金币。
