# Level_55 误导刷除 Star 显示风险评估

## 原行为基线

- `SceneSpriteBrushClear.Completed` 原本只驱动 `Level55MisleadCtrl.OnBrushCompleted()`，完成误导图索引递增、sprite 切换和 brush 状态重置。
- `Level_55Ctrl` 原本负责拖拽触发的 group alpha、背景颜色、装饰星缩放、门显示和音效恢复，不消费误导刷除完成次数。
- 本次需求前工作区已有部分 Level_55 字段和 prefab 改动，字段尚未形成完整运行链路。

## 影响文件与资源

- 脚本：`Level55MisleadCtrl.cs`、`Level_55Ctrl.cs`、`Postcard_Star.cs`
- 资源：`Assets/Game/Prefabs/Level/Level_055.prefab`
- 关联节点：`Postcard_Star (1)`、`Level55MisleadCtrl`、`SceneSpriteBrushClear`

## 使用的既有系统

- 继续使用 `SceneSpriteBrushClear.Completed` 作为刷除完成信号。
- 继续使用 `Postcard_Star` 既有收集逻辑；本次只控制星星节点 Active，不复制发奖、保存或 UI 飞行动画逻辑。
- 通过 `Postcard_Star.RefreshSavedCollectionState()` 在延迟显示后复用原存档去重判断，避免已收集 Star 被重新显示。
- 继续使用本关 `Level_55Ctrl` 局部字段，不新增 `PlayEvents` 或其他全局事件。

## 风险项

- 回调重复绑定风险：`Level_55Ctrl.BindMisleadCallback()` 先 `-=` 再 `+=`，并在 `OnDisable` / `OnDestroy` 解除，降低重复计数风险。
- 初始显示风险：运行态 `Awake()` 会隐藏配置的 `star`，避免 prefab 中星星 Active 时一进关就可见。
- 配置缺失风险：若 `misleadCtrl` 未配置，运行时会从同节点 `GetComponent<Level55MisleadCtrl>()` 兜底；若 `star` 未配置，只会计数不显示，需要 prefab 绑定验证。
- 已收集重复显示风险：`Level_55Ctrl` 显示星星后调用 `Postcard_Star.RefreshSavedCollectionState()`；若存档中该星已收集，会沿用原 `ApplyCollectedState()` 隐藏并关闭触发器。
- 计数阈值风险：`maxCount` 为序列化字段，当前 prefab 绑定为 5；若配置为 0 或负数，第一次完成后会立即满足 `completeCount >= maxCount`。
- 行为干扰风险：新增逻辑不进入拖拽动画、门显示、音效、DB/save、资源加载或全局事件链路，理论影响范围局限于误导刷除与星星显示。

## 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：0 error / 99 warnings。
- 脚本级 `git diff --check` 通过，仅有 LF/CRLF 提示。
- 目标脚本敏感关键字扫描未发现新增编辑器生命周期、DB/save、RemoteConfig、Instance、Instantiate、PlayEvents、DispatchEvent 或 `Update()`。
- `Level_055.prefab` 静态证据显示 `Level_55Ctrl` 已绑定 `misleadCtrl`、`star`、`maxCount: 5`。

## 未验证缺口

- 未运行 Unity PlayMode，未实测第 1 到第 5 次刷除期间星星隐藏、第 5 次后显示。
- 未实测星星显示后触碰收集、已收集去重、失败/重开/退出后的状态。
- 未做 Android/iOS 横屏设备 smoke。
- `Level_055.prefab` 中任务开始前已有多处未提交改动，本次没有验证这些改动与本逻辑的组合效果。
- 知识库目录中未检索到名为“风险评估工作流”的条目，本风险评估按仓库 `AGENTS.md` 和本地文档更新技能要求编写。

## 回滚路径

1. 移除 `Level55MisleadCtrl.OnBrushCompleted()` 中对 `completeCallback` 的调用。
2. 移除 `Level_55Ctrl` 中 `misleadCtrl`、`star`、`maxCount`、`completeCount` 以及绑定、解绑、计数和显示方法。
3. 移除 `Postcard_Star.RefreshSavedCollectionState()`。
4. 从 `Level_055.prefab` 的 `Level_55Ctrl` 序列化数据中移除 `misleadCtrl`、`star`、`maxCount`。
5. 保留原拖拽、刷除 sprite 切换、门显示和音效流程不变。

## 待确认

- `maxCount` 是否永远固定为 5，或后续需要按关卡配置调整。
- `Postcard_Star (1)` 是否就是唯一目标星；当前判断来自 `Level_055.prefab` 的现有节点命名和本次绑定，仍需 PlayMode 画面确认。
