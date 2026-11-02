# Level44 version 2.0 Star 风险评估

## 原始行为基线

- Level44 的 throw 流程只负责生成投掷物、推进拖拽生成进度、更新重量、开门和灯光状态。
- 明信片 Star 的保存、奖励和 UI 飞星由 `Postcard_Star` 自身在玩家触发 2D collider 后处理。
- 当前工作区的 `Level_044.prefab` 已有 inactive 的 `Postcard_Star (1)`，但 `Level44Ctrl` 的 `star` 字段在静态 YAML 中未显示序列化绑定。

## 影响文件与资源

- 修改脚本：`Assets/Game/Scripts/Level/LevelItem/Level_44/Level44Ctrl.cs`
- 运行时依赖资源：`Assets/Game/Prefabs/Level/Level_044.prefab` 中的 `Postcard_Star (1)`
- 复用既有脚本：`Assets/Game/Scripts/Level/LevelItem/Level_31/ThrowItemHandle.cs`、`Assets/Game/Scripts/Postcard/Postcard_Star.cs`

## 既有系统使用

- 使用 `ThrowItemHandle.SpawnItem()` 的返回投掷物位置作为 Star 抛物线起点。
- 使用 DOTween 做本地位置插值，不新增 Update 轮询。
- 使用 `Postcard_Star` 的原收集链路，`Level44Ctrl` 不直接访问 `DB.Postcard`、不直接发放 `GameProps.Star`、不派发 `PostcardGetStarEvent`。
- 使用 Bear.Logger 的 `this.LogWarning(...)` 输出缺失绑定 warning。

## 风险项

- P1：Star 兜底绑定风险。若 `Postcard_Star (1)` 被重命名且 Inspector 没有绑定 `star` 字段，随机命中后只会输出 warning，不会出现 Star。缓解：优先使用 Inspector 绑定；兜底仅作为当前 prefab 的保护。
- P1：已收集状态风险。已收集的 `Postcard_Star` 被激活后会立即由自身状态逻辑隐藏；本次不绕过该行为。风险是视觉上可能看不到随机命中的 Star，符合去重但需要 PlayMode 验证。
- P2：碰撞时机风险。Star 在抛物线移动期间已经 active，玩家可能在落地前触发收集。当前没有额外禁用 collider，因为收集权限仍由 `Postcard_Star` 控制；是否需要落地后才可收集需策划体验验证。
- P2：随机体验风险。每局只允许首次命中触发一次 Star，后续 throw 不会重复激活，避免已收集对象被反复重置；如果设计期望多次出现，需要另开需求明确。
- P2：移动终点风险。终点来自 Star 在 prefab 中的配置位置；如果策划调整 inactive Star 的初始位置，运行时落点会随之变化。
- P3：行尾空白检查风险。当前 `git diff --check` 被任务开始前已有注释/空白行 trailing whitespace 阻断，本次未按工具建议清理，原因是用户明确要求不要改注释。

## 验证证据

- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 error / 99 warnings。
- 静态扫描：`Level44Ctrl.cs` 未新增 DB/save、全局事件、资源加载、广告、IAP、analytics、RemoteConfig 或平台 SDK 链路；只命中原有 `OnValidate()`。
- 源码证据：`Level44Ctrl.SpawnThrowItem(...)` 仅在投掷物生成成功后调用 Star 随机逻辑；`TryTriggerStarFromThrow(...)` 设置一次性触发标记；`JumpStarTo(...)` 使用局部 DOTween。
- 既有链路证据：`Postcard_Star.OnTriggerEnter2D()` 仍是收集入口，保存和发奖继续走 `PostcardManager.TryCollectLevelStar(...)` 与 `Bag.AddTool(...)`。
- Prefab 静态证据：`Level_044.prefab` 当前存在 inactive 的 `Postcard_Star (1)`，`index` 覆盖值为 `2`。

## 未验证缺口

- 未运行 Unity PlayMode 验证 Level44 中 manual throw、drag throw、随机命中、Star 抛物线、玩家碰撞收集和已收集去重。
- 未验证 Android/iOS 横屏设备输入、碰撞和帧率。
- 未通过 Unity Inspector 检查 `Level44Ctrl.star` 是否已绑定；当前代码使用运行时兜底降低未绑定风险。
- 未验证保存失败路径；该路径仍归属 `Postcard_Star` / `PostcardManager`，本次没有新增保存逻辑。

## 回滚路径

回退 `Level44Ctrl.cs` 的 Star 分支和 `SpawnThrowItem(...)` 调用点即可。若后续 Unity 保存了 `Level_044.prefab` 的 `star` 字段绑定，也可在 prefab diff 中移除该绑定，恢复为原 inactive Star 不受 throw 流程控制。

## 待确认

- 是否允许 Star 在抛物线飞行过程中被玩家提前收集。
- 当前 `Postcard_Star (1)` 的 `index = 2` 是否就是 Level44 version 2.0 期望的明信片星序号。
