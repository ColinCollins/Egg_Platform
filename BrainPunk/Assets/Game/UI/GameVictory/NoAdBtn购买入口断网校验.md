# 胜利面板 NoAdBtn 购买入口断网校验

## 2026-09-14 需求与行为

- 主线 `GameVictoryPanel`、Draw `DrawGame_GameVictoryPanel` 和 IWanner `IWannerGame_GameVictoryPanel` 原版在无网时仍先设置 `_isNoAdsPurchasing`，禁用 NoAdBtn 与下一关/重玩导航；SDK 无结果回调时，WaitingPopup 被关闭也不能恢复导航。
- 当前三个入口在锁按钮、发送 `IAP_CLK`、调用 `Purchase()` 之前检查设备无网。已报告无网络时复用 `PurchaseManager.OnPurchaseFailed()` 显示 `U_NoNetWork_Tips_01`，不改变胜利面板输入状态。
- 购买权益、奖励按钮、导航时序、打点、Prefab、存档及平台购买结果回调未修改。IWanner 普通版与 Reward 版复用同一入口。

## 验证、风险与回滚

- 静态核对三个入口的判断均位于商品 ID 查询与按钮锁之前；整个运行时程序集编译 0 error，`git diff --check` 通过。
- PlayMode、iOS 26/Android 横屏、点击后切网及连网但商店不可达均为 `uncertain`；`Application.internetReachability` 不保证商店实际可访问。
- 回滚时删除三个 `PurchaseNoAd()` 中的前置断网分支即可。

## 2026-09-14 新 NoAds SKU 与旧权益兼容

- 主线、Draw、IWanner 三处 NoAdBtn 原先硬编码旧 `no_ads`，当前改为购买 `newno_ads`；新商品 SKU 用于 IAP 点击打点，成功后按 Shop `20001` 发放 NoAds。
- 三处 `HasNoAds()` 先读取 Bag 的 NoAds 数量，再检查新旧独立免广告商品、新旧免广告礼包的平台资产；同一判断同时控制按钮显隐与点击前拦截，避免恢复时重复购买。
- 无改动原生购买结果、胜利导航、关卡资源或存档结构。Guru 生成表和运行时程序集编译通过；Android/iOS 真机旧账号恢复与新 SKU 购买仍待验证。回滚时将三处购买商品名和已拥有判定恢复原实现，但旧 SKU 的 Guru 与 Luban 映射必须保留。
