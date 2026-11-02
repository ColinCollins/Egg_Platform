# NoAdsPopup 购买入口断网校验

## 2026-09-14 需求与行为

- 原版 BuyNoAd 在无网时仍会先设置 `_isPurchasing` 并关闭购买、退出按钮交互，等待 SDK 结果；若平台购买未拉起且无回调，关闭 WaitingPopup 后仍不能退出弹窗。
- 当前点击前检查 `Application.internetReachability`。已报告无网络时复用 `PurchaseManager.OnPurchaseFailed()` 显示 `U_NoNetWork_Tips_01`，不改变按钮状态，不发送 `IAP_CLK`，不调用 `Purchase()`。
- NoAds 权益、购买结果回调、关闭打点、Prefab、存档和 SDK 均沿用原流程；主线、Draw、IWanner 胜利面板的 NoAdBtn 同步做相同前置判断，见 `DevelopMD/购买入口断网前置校验.md`。

## 验证、风险与回滚

- 静态检查确认断网分支位于商品 ID 查询与 `_isPurchasing = true` 之前；整个运行时程序集编译 0 error，`git diff --check` 通过。
- Unity PlayMode、iOS 26 真机、Android 横屏、点击后断网和联网但商店不可达尚未验证，状态为 `uncertain`。
- 回滚时删除 `BuyNoAd()` 中的前置断网分支；无需迁移资源或存档。

## 2026-09-14 独立免广告商品切换

- 当前 `newno_ads` 关联 Shop `20001`，旧 `no_ads` 保留原 SKU 并关联相同权益的 Shop `20015`。本弹窗原先仍查询旧商品价格并购买旧 SKU；现在价格、点击打点和购买请求改用新商品。
- 点击前同时检查新旧独立免广告商品、新旧免广告礼包的已购资产，以及 Bag 中的 NoAds 道具；老用户尚未完成本轮恢复但平台资产已可见时，也不会再次购买。成功回调继续交给 PurchaseManager 按当前商品名发奖。
- Guru 生成表与运行时程序集编译通过；真机旧账号、未返回平台资产和商店后台新 SKU 可售性未验证。回滚时恢复商品名及旧判断，恢复链中的旧 SKU 不应删除。
