# BaseLevelCtrl TryToCollectStar 功能说明

## 需求背景

2026-07-09 需求：补齐 `BaseLevelCtrl.TryToCollectStar(Vector3 pos)` 逻辑。当前成功动画流程已经在 `PlaySuccessAnim()` 成功分支调用该方法，目标是在关卡成功时尝试补发当前关卡的第 3 颗明信片星，并复用现有 HUD 飞星与胜利面板等待星表现。

## 影响文件与系统

- `Assets/Game/Scripts/Level/BaseLevelCtrl.cs`
  - `TryToCollectStar(Vector3 pos, float delay = 0)` 补齐运行态保护、可选延迟、当前关卡数据校验、背包和 `PostcardManager` 校验。
  - 有 `successAnim` 的成功流程会传入 `1.5f` 延迟，并在延迟完成后再进入明信片星写入、发奖和事件派发链路；无 `successAnim` 时仍立即执行。
  - 使用 `PostcardManager.IsLevelStarCollected(...)` 判断当前关卡 `Postcard_StarNum.Three` 是否已经永久收集。
  - 新增收集时通过 `PostcardManager.TryCollectLevelStar(...)` 写入 `DB.Postcard.LevelCollectedStar`，再调用 `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)` 发放背包星，并派发 `PostcardGetStarEvent(pos)`。
- 相关系统：
  - `PostcardManager` 负责永久星记录保存、保存失败回滚和 `PostcardGetStarEvent` 后的 `LevelWaitingAnimStar` 计数。
  - `GamePlayPanel.PostcardStar` 监听 `PostcardGetStarEvent` 播放 HUD 飞星。
  - `PlayCtrl_Success.TryGrantVictoryStars(...)` 后续发放首次通关默认星时会跳过已经收集的第 3 颗星，只补配置允许的剩余星。

本次未修改 prefab、关卡资源、Luban 配置、DB 字段、资源加载、广告、IAP、analytics、RemoteConfig 或平台 SDK。

## 原有行为基线

- `TryToCollectStar(Vector3 pos)` 只有注释，没有实际校验、保存、发奖或事件派发逻辑。
- 当前工作区中 `PlaySuccessAnim()` 已在有 `successAnim` 时传入 `successAnim.transform.position`，无 `successAnim` 时传入 `Vector3.zero`。
- 用户随后为有 `successAnim` 的分支补充了延迟参数；修复前 `UniTask.WaitForSeconds(delay)` 没有 `await`，不会真正等待。
- 普通场景 `Postcard_Star` 的收集链路是先写永久收集记录，再发放 `GameProps.Star`，最后派发 `PostcardGetStarEvent`。

## 当前行为

1. `TryToCollectStar(...)` 仅在 `Application.isPlaying` 时继续执行。
2. `delay > 0` 时等待对应秒数；等待使用当前 `BaseLevelCtrl` 的销毁取消 token，延迟期间关卡对象销毁会直接退出。
3. 延迟完成后再次确认仍在 PlayMode。
4. 读取 `PlayCtrl.Instance.Level.CurrentLevelData.Id`，缺失时记录 warning 并退出。
5. 校验 `PlayCtrl.Instance.Bag` 和 `GameManager.Instance.Postcard`，任一缺失时记录 warning 并退出。
6. 如果当前关卡第 3 颗明信片星已经收集，直接退出，不重复发奖或播 HUD 星。
7. 未收集时调用 `PostcardManager.TryCollectLevelStar(levelId, 3, out isNewCollection)` 写入永久收集记录；保存失败时不发奖、不派发事件。
8. 保存成功且确认为新增时，背包增加 1 个 `GameProps.Star`，并派发 `PostcardGetStarEvent(pos)`。

## 数据、事件与 UI 流程

1. 关卡成功进入 `BaseLevelCtrl.PlaySuccessAnim()`。
2. 有成功动画分支调用 `TryToCollectStar(successAnim.transform.position, 1.5f).Forget()`；无成功动画分支调用 `TryToCollectStar(Vector3.zero).Forget()`。
3. 有延迟时先等待，等待期间若关卡对象销毁则取消，不写明信片星、不发背包、不派事件。
4. `PostcardManager.TryCollectLevelStar(...)` 写入 `DB.Postcard.LevelCollectedStar[levelId]`，保存失败时回滚本次新增星 id。
5. `SimpleBag.AddTool(GameProps.Star, 1, RewardType.Other)` 写入背包星并触发既有道具更新链路。
6. `PostcardGetStarEvent` 通知 `GamePlayPanel.PostcardStar` 播放 HUD 飞星，也被 `PostcardManager` 监听并累加 `LevelWaitingAnimStar[levelId]`。
7. 后续 `PlayCtrl_Success.TryGrantVictoryStars(...)` 仍按 `LevelData.LevelVictoryStarCount` 补齐首次通关默认星，已存在的第 3 颗星不会重复写入。

## 验证记录

- 延迟修复后重新执行 `git diff --check -- Assets/Game/Scripts/Level/BaseLevelCtrl.cs Assets/Game/Scripts/Level/BaseLevelCtrl_TryToCollectStar功能说明.md Assets/Game/Scripts/Level/BaseLevelCtrl_TryToCollectStar风险评估.md DevelopMD/已有功能设计与使用记录.md` 已通过，仅有工作区 LF/CRLF 提示。
- 延迟修复后重新执行敏感链路静态扫描，目标脚本 diff 只命中预期的 `WaitForSeconds`、`.Forget()` 和 `PostcardGetStarEvent` 派发，未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB 直连、资源加载、RemoteConfig、PlayerPrefs、StreamingAssets 或 Instantiate 链路。
- 延迟修复后重新执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 98 warnings / 0 errors。
- Unity PlayMode、实际成功门飞星位置、重复通关去重和 Android/iOS 横屏 smoke 尚未验证。

## 剩余风险与未验证项

- 无 `successAnim` 时事件位置仍沿用当前调用方传入的 `Vector3.zero`，HUD 飞星起点表现需 PlayMode 验证。
- 有 `successAnim` 时收集链路延迟 1.5 秒执行；静态证据显示 `SuccessAnimCtrl.Play(...)` 的成功回调晚于该延迟，但实际不同 prefab 动画表现仍需 PlayMode 验证。
- `SimpleBag.AddTool(...)` 保存失败或平台打点失败没有在 `TryToCollectStar(...)` 中做二次回滚；该行为与普通 `Postcard_Star` 现有链路一致，但仍需在存档异常场景补验。
- 当前为共享 `BaseLevelCtrl` 行为，可能影响所有使用默认成功流程的关卡；需要至少验证首次通关、重复通关、已收集第 3 颗星和配置 `LevelVictoryStarCount` 不同取值的代表关卡。

## 回滚路径

恢复 `TryToCollectStar(Vector3 pos)` 为空实现，只保留原注释；如需完全回到旧行为，可同时移除 `PlaySuccessAnim()` 中对 `TryToCollectStar(...)` 的调用。

## 2026-07-10：按当前关卡可收集星数量增加门禁

### 需求与行为变化

- 需求：仅当当前 `LevelData.LevelSumStarCount` 在有效星 id 范围内归一化后大于 0，且当前有效永久收集星数量小于该上限时，`TryToCollectStar(...)` 才进入第 3 颗明信片星收集链路。
- 原有行为：方法只校验运行态、关卡 id、背包、`PostcardManager` 和永久收集状态，没有按关卡可收集星总数过滤。
- 当前行为：调用 `PostcardManager.CanCurrentLevelCollectStar()`；非运行态、当前关卡或 Postcard 存档无效、`PostcardManager` 缺失、有效星上限为 0 或已收集数量达到上限时直接退出，不写永久星、不增加背包、不派发事件。
- 门禁在延迟前执行一次，并在延迟完成后再执行一次。前者避免零星关卡启动 2.3 秒延迟，后者避免延迟期间关卡切换后向不可收星关卡写入。
- 正数关卡继续沿用现有 `IsLevelStarCollected(...)`、`TryCollectLevelStar(...)`、`Bag.AddTool(...)` 与 `PostcardGetStarEvent` 流程；本次没有调整成功动画延迟参数和无动画分支参数。

### 数据、事件与平台边界

1. `CanCurrentLevelCollectStar()` 读取当前 Luban `LevelData.LevelSumStarCount` 并限制到 0 到 3，通过 `GetLevelCollectedStarIds(...)` 读取去重且只含 1 到 3 的永久星 id 数量；不写 DB/save。
2. 数据无效、有效配置上限为 0 或 `collectedStarCount >= maxStarCount` 时返回 `false`，流程在任何永久星写入、背包或事件操作前结束。
3. 返回 `true` 时原有收集链路不变；首次通关默认星仍由 `PlayCtrl_Success.TryGrantVictoryStars(...)` 独立处理。
4. 本次未修改配置表、prefab、资源加载、全局事件、analytics、广告、IAP、RemoteConfig 或 Android/iOS 平台代码。

### 验证、风险与回滚

- 目标脚本 `git diff --check` 通过，static API 与两处调用的静态扫描符合预期。
- `Assembly-CSharp.csproj` 独立编译通过，0 error / 98 warnings，warnings 未逐项审计。
- 补充已收集数量上限比较后复跑同一编译命令通过，0 warning / 0 error。
- Unity PlayMode 尚需覆盖 `Level_001/002`（配置为 0）、`Level_003`（配置为 3 且已收集数量分别为 0/2/3）、DB 缺失、异常配置大于 3、重复通关和延迟切关；Android/iOS 横屏 smoke 未验证。
- 回滚时移除 `TryToCollectStar(...)` 中两处 `CanCurrentLevelCollectStar()` 调用；若不再有调用方，再移除该 static API。
