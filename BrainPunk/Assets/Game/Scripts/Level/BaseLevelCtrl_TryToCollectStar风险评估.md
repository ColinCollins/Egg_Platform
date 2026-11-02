# BaseLevelCtrl TryToCollectStar 风险评估

## 原有行为基线

- `BaseLevelCtrl.TryToCollectStar(Vector3 pos)` 原本只有占位注释，不会写入 `DB.Postcard.LevelCollectedStar`，不会发放背包星，也不会派发 `PostcardGetStarEvent`。
- `Postcard_Star` 是已有场景星收集权威链路：去重后调用 `PostcardManager.TryCollectLevelStar(...)` 保存，成功后发放 `GameProps.Star` 并派发 `PostcardGetStarEvent`。
- `PostcardManager` 监听 `PostcardGetStarEvent`，把当前关卡本轮新增星数量累加到 `DB.Postcard.LevelWaitingAnimStar`，供胜利面板消费表现。

## 影响文件与资源

- 已修改：`Assets/Game/Scripts/Level/BaseLevelCtrl.cs`
- 新增记录：
  - `Assets/Game/Scripts/Level/BaseLevelCtrl_TryToCollectStar功能说明.md`
  - `Assets/Game/Scripts/Level/BaseLevelCtrl_TryToCollectStar风险评估.md`
  - `DevelopMD/已有功能设计与使用记录.md`
- 未修改：关卡 prefab、`Postcard_Star.cs`、`PostcardManager.cs`、`PlayEvents.cs`、`GamePlayPanel.PostcardStar.cs`、DB 字段、Luban 配置、资源加载、广告、IAP、analytics、RemoteConfig、平台 SDK。

## 现有系统复用

- 永久星记录和保存失败回滚复用 `PostcardManager.TryCollectLevelStar(...)`。
- 第 3 颗星去重复用 `PostcardManager.IsLevelStarCollected(...)`。
- 延迟等待复用 `UniTask.WaitForSeconds(...)` 和当前 `BaseLevelCtrl` 销毁取消 token；延迟完成后才进入写入、发奖和事件派发。
- 背包发奖复用 `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)`。
- HUD 飞星和胜利面板等待动画复用已有 `PostcardGetStarEvent` 监听链路。

## 风险项

### P1-1 共享成功流程影响面较大

- 风险：`BaseLevelCtrl` 是共享关卡控制脚本，默认成功流程的所有关卡都可能在成功时尝试收集第 3 颗明信片星。
- 当前控制：按当前关卡 `LevelData.Id` 和星 id `3` 去重，已收集时直接退出；保存失败不发奖、不派发事件。
- 待验证：代表关卡首次通关、重复通关、历史已收集第 3 颗星、`LevelVictoryStarCount` 为 0/1/2/3 的数据组合。

### P1-2 与首次通关默认星的等待动画计数叠加

- 风险：`BaseLevelCtrl` 延迟后派发第 3 颗星事件，`PlayCtrl_Success.TryGrantVictoryStars(...)` 后续还会按配置发放默认星。
- 当前控制：`TryGrantVictoryStars(...)` 会跳过已经存在的第 3 颗星，只把新增的剩余星 id 累加到等待动画计数，因此不会重复写第 3 颗。
- 待验证：胜利面板本轮等待动画数量是否等于实际新增永久星数量。

### P1-3 延迟执行可能改变胜利面板消费时机

- 风险：如果收集延迟晚于 SUCCESS 状态和胜利面板创建，`PostcardGetStarEvent` 监听写入的 `LevelWaitingAnimStar` 可能晚于胜利面板消费。
- 当前控制：当前 `SuccessAnimCtrl.Play(...)` 静态流程显示 callback 在门动画、关门音效和彩带后才触发；`TryToCollectStar(..., 1.5f)` 的延迟早于该 callback。该结论绑定当前脚本证据，仍需 PlayMode 验证 prefab 实际表现。
- 待验证：有 `successAnim` 的代表关卡中，胜利面板打开时能消费到本次第 3 颗星等待动画。

### P2-1 无成功动画时 HUD 飞星起点可能不准确

- 风险：无 `successAnim` 分支沿用 `Vector3.zero` 作为事件坐标，HUD 飞星起点可能不在门口或角色位置。
- 当前控制：未改变现有调用参数，只补齐收集逻辑。
- 待验证：无成功动画关卡是否存在，以及其 HUD 飞星起点是否可接受。

### P2-2 背包保存失败没有二次回滚永久星

- 风险：`PostcardManager.TryCollectLevelStar(...)` 保存成功后才调用 `SimpleBag.AddTool(...)`；若背包保存失败，当前方法不会回滚永久星。
- 当前控制：与 `Postcard_Star.TryCollectStarAndReward(...)` 现有顺序保持一致，避免为共享流程单独引入新的回滚策略。
- 待验证：存档失败模拟场景仍需专项验证。

### P2-3 延迟期间关卡销毁导致本次自动星不发放

- 风险：延迟期间如果当前关卡被销毁，自动第 3 颗星不会写入、不会发奖、不会派发事件。
- 当前控制：这是显式取消保护，避免销毁后继续访问 `PlayCtrl`、`Bag`、`PostcardManager` 或派发迟到事件。
- 待验证：退出、重开、失败重置等中断路径下不会出现迟到发奖或迟到 HUD 飞星。

## 验证证据

- 延迟修复后重新执行 `git diff --check -- Assets/Game/Scripts/Level/BaseLevelCtrl.cs Assets/Game/Scripts/Level/BaseLevelCtrl_TryToCollectStar功能说明.md Assets/Game/Scripts/Level/BaseLevelCtrl_TryToCollectStar风险评估.md DevelopMD/已有功能设计与使用记录.md` 已通过，仅有工作区 LF/CRLF 提示。
- 延迟修复后重新执行敏感链路静态扫描，目标脚本 diff 只命中预期的 `WaitForSeconds`、`.Forget()` 和 `PostcardGetStarEvent` 派发，未新增编辑器生命周期、DB 直连、资源加载、RemoteConfig、PlayerPrefs、StreamingAssets 或 Instantiate 链路。
- 延迟修复后重新执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 98 warnings / 0 errors。
- 待补验 Unity PlayMode、代表关卡成功流程、重复通关去重、胜利面板星动画数量和 Android/iOS 横屏 smoke。

## 回滚路径

将 `BaseLevelCtrl.TryToCollectStar(Vector3 pos)` 恢复为空实现即可停止成功时自动收集第 3 颗星；如需连入口也回退，删除 `PlaySuccessAnim()` 中对 `TryToCollectStar(...)` 的调用。

## 2026-07-10：LevelSumStarCount 门禁增量评估

### 原有基线与影响范围

- 原有共享成功流程不检查 `LevelSumStarCount`，因此配置为 0 的关卡也可能尝试自动收集第 3 颗星。
- 本次仅修改 `PostcardManager.cs` 与 `BaseLevelCtrl.cs`，新增只读配置门禁；不修改关卡 prefab、Luban 数据、DB/save 结构、背包实现、事件定义或平台流程。

### 风险与当前控制

- P1 共享关卡范围：现在只有当前 `LevelData.LevelSumStarCount` 归一化到 0 到 3 后大于 0，且有效永久收集数量小于该上限时才进入自动收集链路；仍需验证零值、正值与已满上限的代表关卡。
- P1 延迟切关：有成功动画时实际收集延迟 2.3 秒，期间 `CurrentLevelData` 可能变化；当前在延迟前后双重门禁，第二次检查紧邻实际关卡 id 读取与写入。
- P1 数量越界：原门禁只判断配置大于 0，可能在永久星数量已满时继续进入后续逻辑；当前使用 `GetLevelCollectedStarIds(...)` 的有效唯一 id 数量与 0 到 3 的有效配置上限比较，`collectedStarCount >= maxStarCount` 时退出，避免配置大于有效星 id 数量后永远无法满额。
- P1 验收误判：`LevelSumStarCount == 0` 不会关闭 `LevelVictoryStarCount` 的首次通关默认星链路，必须分别核对永久第 3 星、Bag +1 和 `PostcardGetStarEvent`。
- P2 缺失数据：static API 对非运行态、空 `PlayCtrl/Level/CurrentLevelData`、空关卡 id、缺失 `PostcardManager` 或 `DB.Postcard` 安全返回 `false`；正常判定只读永久收集记录，不调用保存。

### 验证证据与缺口

- 目标脚本 scoped `git diff --check` 通过，仅有 LF/CRLF 提示；静态扫描确认门禁位于延迟前后，且未新增编辑器生命周期、资源加载或全局事件。
- `Assembly-CSharp.csproj` 独立编译通过，0 error / 98 warnings，warnings 未逐项审计。
- 补充已收集数量上限比较后复跑同一编译命令通过，0 warning / 0 error。
- 缺口：Unity PlayMode 的 0/3/大于 3 配置、DB 缺失、已收集数量 0/2/3、重复通关、延迟切关/销毁、实际 HUD 与 Bag 结果，以及 Android/iOS 横屏 smoke 尚未验证。

### 回滚路径

- 移除 `BaseLevelCtrl.TryToCollectStar(...)` 中两处门禁调用，并移除无调用方的 `PostcardManager.CanCurrentLevelCollectStar()`；配置和存档无需回滚。
