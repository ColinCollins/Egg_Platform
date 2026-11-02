# PlayCtrl_Success 明信片胜利默认星记录

## 需求背景

2026-07-02 需求：在 `PlayCtrl_Success` 中最小化补齐 `TryToGetPostcardVictoryStar()` 和 `IsFirstVictory()` API。目标是在当前关卡首次通关时，按 `LevelData.LevelVictoryStarCount` 发放胜利默认明信片星，并让胜利面板本次打开时可以消费到对应新增星星数量。

## 影响文件与系统

- `Assets/Game/Scripts/Play/PlayCtrl_Nodes/PlayCtrl_Success.cs`
  - `OnEnter()` 缓存首次通关判断。
  - `ShowVictory(bool isFirstVictory)` 在创建胜利面板前写入胜利默认星。
  - `TryToGetPostcardVictoryStar()` 只负责校验运行时、关卡数据和 `PostcardManager`，实际明信片存档写入交给 `PostcardManager.TryGrantVictoryStars(...)`。
  - `IsFirstVictory()` 返回当前关卡是否尚未存在通关记录。
- `Assets/Game/UI/Postcard/Scripts/PostcardManager.cs`
  - `TryGrantVictoryStars(...)` 集中写入 `DB.Postcard.LevelCollectedStar` 和 `DB.Postcard.LevelWaitingAnimStar`。
  - 保存失败时回滚本次新增的永久星 id 和等待动画计数。
- 相关系统：
  - `LevelCtrl.Victory()` 的 `PassedLevels` 写入和下一关推进。
  - `GameVictoryPanel.Create(...)` 打开胜利星组后对 `PostcardManager.ConsumeCollectedStarCount(...)` 的一次性消费。
  - `Postcard` 存档字段 `LevelCollectedStar`、`LevelWaitingAnimStar`。

本次未修改 prefab、Luban 配置、RemoteConfig、资源加载、广告、IAP、analytics、平台 SDK 或全局事件。

## 原有行为基线

- `PlayCtrl_Success.ShowVictory(...)` 原本先创建胜利面板，再调用 `owner.Level.Victory()` 写入通关记录。
- `GameVictoryPanel.Create(LevelData data, ...)` 会按 `data.Id` 立即消费 `LevelWaitingAnimStar`，并缓存到胜利面板生命周期内。
- `TryToGetPostcardVictoryStar()` 和 `IsFirstVictory()` 只有注释和空实现，没有写入明信片星星数据。
- `LevelCtrl.Victory()` 会在当前关卡未通关时把 `owner.Level.CurrentLevel` 写入 `DB.GameData.PassedLevels`。

## 当前行为

- `OnEnter()` 在 `LevelCtrl.Victory()` 之前计算并缓存 `isFirstVictory`，避免胜利后 `PassedLevels` 已写入导致判断变化。
- `ShowVictory(isFirstVictory)` 在首次通关时先调用 `TryToGetPostcardVictoryStar()`，再创建 `GameVictoryPanel`，保证胜利面板本次打开可以消费到新增默认星。
- `TryToGetPostcardVictoryStar()`：
  - 仅在 PlayMode 且 `IsFirstVictory()` 为 true 时执行。
  - 把完整 `LevelData` 传给 `PostcardManager.TryGrantVictoryStars(...)`，由统一入口同时检测胜利星数量与关卡总星数。
  - 不直接访问 `DB.Postcard` 或调用 `DB.SavePostcard()`。
- `PostcardManager.TryGrantVictoryStars(...)`：
  - 发放数量限制为 `0~min(LevelData.LevelVictoryStarCount, LevelData.LevelSumStarCount, 3)`；总星数为 0 时不写存档、不增加 Bag Star。
  - 胜利默认星 id 从 3 递减写入到 1，已存在的 id 不重复写入。
  - 新增 id 数量会累加到 `LevelWaitingAnimStar[levelId]`，供胜利面板本次表现消费。
  - `DB.SavePostcard()` 失败时回滚本次新增的永久星 id 和等待动画计数，并输出 warning。
- `IsFirstVictory()` 现在返回当前关卡是否未存在于 `DB.GameData.PassedLevels`。

## 数据与流程

1. 进入 SUCCESS 状态时读取 `DB.GameData.PassedLevels`，得到首次通关缓存值。
2. rating 弹窗结束或无 rating 弹窗时进入 `ShowVictory(...)`。
3. 首次通关时 `PlayCtrl_Success` 调用 `PostcardManager.TryGrantVictoryStars(...)`，由管理器写入 `DB.Postcard.LevelCollectedStar[levelId]` 和 `DB.Postcard.LevelWaitingAnimStar[levelId]`。
4. `GameVictoryPanel.Create(...)` 打开胜利面板并消费 `LevelWaitingAnimStar[levelId]`。
5. `owner.Level.Victory()` 写入通关、解锁下一关并保存 `GameData`。

## 验证记录

- `git diff --check -- Assets/Game/Scripts/Play/PlayCtrl_Nodes/PlayCtrl_Success.cs Assets/Game/UI/Postcard/Scripts/PostcardManager.cs Assets/Game/Scripts/Postcard/Postcard_Star.cs Assets/Game/UI/Postcard/Scripts/Postcard_VictoryStarGroupCtrl.cs` 待本次收尾复跑。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已在首次实现后通过，结果为 0 error / 1115 warnings；本次文档收尾后需复跑。
- 静态检查确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、RemoteConfig、广告、IAP、analytics 或平台 SDK 路径。

## 剩余风险与未验证项

- 尚未运行 Unity PlayMode，需验证首次通关时胜利面板能消费到默认星，二次通关不会重复写入。
- 尚未验证 Android/iOS 横屏设备上的胜利流程和保存耗时表现。
- 胜利星动画 UI 仍依赖 `GameVictoryPanel` / `Postcard_VictoryStarGroupCtrl` 后续表现接入，本次只保证数据写入和消费时机。

## 回滚路径

删除 `ShowVictory(...)` 中 `TryToGetPostcardVictoryStar()` 的调用，并将 `TryToGetPostcardVictoryStar()` / `IsFirstVictory()` 恢复为空实现；同时恢复 `ShowVictory(...)` 的旧参数语义，即可回到只打开胜利面板并由 `LevelCtrl.Victory()` 写通关记录的旧行为。

## 2026-07-20 总星数保护补充

- 原始风险：首次通关只使用 `LevelVictoryStarCount`，当 `LevelSumStarCount` 为 0、但胜利星配置为正数时仍可能写入星星并发放 Bag Star。
- 当前实现：首次通关与历史补录都把完整 `LevelData` 交给 `PostcardManager.TryGrantVictoryStars(LevelData)`；统一入口按关卡总星数限制胜利星数量。
- 数据流、保存失败回滚、已有星去重和胜利面板消费顺序不变；本次不清理旧存档。
- C# 工程构建通过，0 error / 99 warnings；Unity PlayMode 与 Android/iOS 横屏仍待验证。
- 回滚方式：恢复旧 API 签名和两个旧调用点，并恢复历史补录的外部总星数门禁。

## 2026-07-23 DailyChallenge Bubble 完成回调接入

- `GameVictoryPanel.RefreshPostcardVictoryStars(...)` 原完成回调只解除胜利页点击阻塞；当前在同一回调边界增加 `DailyChallengeBtn.TryExpandIfIncomplete()`，满足《每日挑战功能需求分析.xlsx》“功能需求”工作表 `REQ-005` 的星星动画结束后自动展开要求。
- 没有待播放星星或缺少星星控制器时，完成边界直接成立；每日挑战已全部完成、入口隐藏、面板已关闭或禁用时不会展开。普通与奖励胜利页共用基类逻辑。
- 星星发放、等待动画计数消费、点击阻塞和原动画序列不变；本次不修改明信片存档、配置、prefab、资源或事件。
- C# 工程构建通过（0 error / 99 个项目既有 warning）；Unity PlayMode 的有/无星动画及中途关闭面板仍待验证。回滚时移除完成边界上的 DailyChallenge 调用即可。
