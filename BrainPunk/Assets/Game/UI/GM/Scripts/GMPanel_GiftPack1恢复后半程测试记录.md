# GMPanel GiftPack1 恢复后半程测试记录

## 2026-09-14 需求与原行为

- 需求：在 Editor PlayMode 中跳过平台恢复订单，模拟 `gift_pack_1` 已进入 `PurchaseManager._restoredProductNames`，检查后续 Skin、购买缓存与 ChestGift 档位联动。
- 原 GMPanel `RestoreBtn` 只调用 `PurchaseManager.Restore()`；Guru V4 在 Windows Editor 返回成功但不派发恢复订单，因此不能驱动 `SyncRestore()` 的补发链。

## 影响文件与行为

- `Assets/Game/UI/GM/Scripts/GMPanel.cs`：复用 Prefab 已绑定的 `RestoreBtn`。Editor 下点击时注入 `Products.ProductName.GiftPack1` 并调用 `SyncRestore()`；非 Editor 编译保持原 SDK 恢复调用。
- `Assets/Game/Scripts/SDK/Services/Purchase/PurchaseManager.cs`：新增仅在 `UNITY_EDITOR` 编译的 `DebugAddRestoredGiftPack1()`；只允许 PlayMode，在列表中幂等追加商品名，不制造 SDK 订单或资产。
- `Assets/Game/UI/GM/Resources/GMPanel.prefab`、`GMPanel.Generated.cs`、Luban/Guru 配置及正常购买链均未改动。

## 测试数据与边界

1. 在 Unity Editor PlayMode 中进入游戏并打开 GMPanel，点击现有恢复购买按钮。按钮先把 `gift_pack_1` 补进待恢复商品名列表，再调用原 `SyncRestore()`。
2. `SyncRestore()` 沿现有 `TbProducts(Possessive) -> TbShop.Rewards -> Bag / PurchaseCache -> SaveGameData -> OnRestoreSuccessEvent` 路径执行；Coin 按现有规则跳过，Skin 和购买记录会写入 Editor 当前游戏存档。
3. 若列表原已有其它商品名，本次同步也会逐项处理它们。重复点击不会重复加入 `gift_pack_1`；已拥有权益时 `SyncRestore()` 不派发新增成功事件。
4. 此工具跳过平台 SKU、收据、`OnRestore()` 和 `AssetStore` 回调；只能检验商品名进入列表之后的本地补发链，不能证明真实内购恢复成功。

## Editor 手工验收

1. 进入 PlayMode，加载到主页后打开 GMPanel，点击现有恢复购买按钮；Console 应出现 `GM restore test added productName:gift_pack_1` 和 `SyncRestore apply reward. productName:gift_pack_1`。
2. 核对 Shop `20008` 配置中的 Skin 已拥有、`PurchaseCache` 含 `gift_pack_1`，且 Coin 数量不因恢复而增加。若礼包入口在主页可见，核对它刷新后跳过已购的第一档。
3. 再点一次，核对 `gift_pack_1` 没有重复入列、Skin 与 Coin 不重复发放；在没有其它待恢复商品变化时，日志应为 `changedCount:0` 且不再弹成功提示。
4. 退出并重新进入 PlayMode 后核对 Skin 和购买记录持久化。若此前 Editor 存档已拥有权益，先使用隔离的测试存档；不要把已有权益导致的 `changedCount:0` 判为补发失败。

## 验证、未验证与回滚

- 静态核对：`gift_pack_1` 在当前 `tbproducts.json` 中为 Possessive 且映射 Shop `20008`；GM `RestoreBtn` 已在 Prefab 中绑定；代码路径仅在 `UNITY_EDITOR` 注入；目标文件 `git diff --check` 通过。
- 编译验证：`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 因 `Temp/bin/Debug` 缺少 Unity/Guru 等 200 个元数据文件失败。使用现有 Unity Bee 响应文件把当前 `Guru.SDK.Gen` 与 `Assembly-CSharp` 依次编译到系统临时目录，分别为 0 个错误；运行时程序集有 101 个警告，未建立变更前 warning 基线。单独使用旧 Guru DLL 编译会出现 4 个 `Products.ProductName.NewnoAds` 缺失错误，故必须按当前生成源码一起编译。此结论是编译验证，不代替 Unity PlayMode 或平台实测。
- `uncertain`：Editor PlayMode 中的 Skin 写入、`PurchaseCache` 落盘、首页礼包入口当场刷新、重复点击和重启持久性尚需手工验证；Android/iOS 平台恢复仍需真机验收。
- 回滚：移除 GMPanel 的 Editor 条件分支及 PurchaseManager 的 `DebugAddRestoredGiftPack1()`，GM 按钮恢复为原 SDK 调用。
