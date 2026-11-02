# DrawGameEnter 功能说明

## 2026-10-13：选关分页对齐主线

- 用户确认替换旧分页规则。`OnClickDrawLevel` 保留 Draw 开关与失败日志，进入时复用 `GameManager.TryEnterDraw`；删除仅为首页最高可玩分页设计的旧专用入口。
- 首页打开 Draw 选关采用面板的当前有效关卡优先、保存进关页兜底规则，不再额外跳到最高可玩页。关内返回采用相同打开规则；选关进入时保存页码，浏览与关闭不保存。
- 影响代码仅四个游戏侧文件，无首页 Prefab、生成绑定、开关、星数、资源或存档结构改动。依据、修改前核对及局部回滚见 `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelPanel功能与风险评估.md` 的 2026-10-13 章节。
- 48 项实际源码隔离验证及 Editor/Android 条件编译通过，日志见 `DevelopMD/验证记录/Draw选关分页_2026-10-13/`。`uncertain`：正式 UI 动画、真实存档重启和 Android/iOS 真机尚未验证。

## 2026-09-18 云控刷新导出日志补充

- 为定位 iOS 拉取成功后入口未更新的报告缺口，在 StartPanel.RefreshDrawRemoteConfig 的原有门禁与完成位置通过 SDK 公开持久化接口记录 `[DrawRemoteConfigUI]`：首页关闭则 home_skipped，刷新调用返回则 home_refreshed，并输出入口引用/激活状态。
- 只补诊断，不改变入口显隐、星数、刷新顺序或 UI 生命周期；编辑器非运行态继续直接返回。真实引用编译 0 错误，手机可见效果及新 ZIP 归档尚未实测，标记 `uncertain`；细节见 DevelopMD/RemoteConfigService_Design.md 第 34 节。回滚只删除日志并恢复等价早退，不回滚既有入口开关功能。

## 需求与背景

主界面的 `DrawGameEnter` 入口需要受 DrawGame RemoteConfig 功能开关控制，关闭时不能显示或进入 DrawGame。

## 影响范围

- 代码：`Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`
- UI：`Assets/Game/UI/StartPanel/Resources/StartPanel.prefab` 中嵌套的 `DrawGameEnter` 根节点
- 配置：复用 `ConfigManager.RemoteConfig.GetDrawGameIsOpen()`；未新增字段或默认值
- 未修改 DrawGame 关卡、存档、资源地址、事件、广告、分析或平台流程

## 修改前基线

- `StartPanel` 已通过 `drawGameBtn` 处理 DrawGame 点击进入。
- `GetDrawGameIsOpen()` 已能优先读取 `drawgame_config.drawGameIsOpen`，缺失时回退 `GlobalConst.DrawGameIsOpen`。
- `StartPanel` 没有调用该读取接口，因此配置关闭时入口仍显示，点击仍会调用 `GameManager.Instance.TryEnterDraw(...)`。

## 修改后行为

- `StartPanel.OnOpen()` 在运行态刷新入口显隐。
- `drawGameBtn` 位于 `DrawGameEnter` 根节点之下；显隐控制作用于其父节点，因此按钮与提示气泡一并隐藏或恢复。
- 点击处理再次读取开关。若界面打开后开关变为关闭，则立即刷新入口并中止进入流程。
- `drawGameBtn` 缺失时沿用现有警告；父节点异常缺失时记录 Bear Logger 警告，并降级为只控制按钮对象。

## 配置与调用流

`StartPanel.OnOpen()` → `RefreshDrawGameEntryVisibility()` → `ConfigManager.RemoteConfig.GetDrawGameIsOpen()` → `drawgame_config` 或 `GlobalConst` → `DrawGameEnter.SetActive(...)`。

点击时：`OnClickDrawLevel(...)` → 再次检查开关 → 开启时才调用 `GameManager.Instance.TryEnterDraw(...)`。

## 验证记录

- 静态确认 `StartPanel.prefab` 的 `drawGameBtn` 绑定到嵌套 `DrawGameEnter.prefab` 的 `clickBtn`，该按钮父节点为 `DrawGameEnter` 根节点。
- 静态确认入口显隐与点击路径均调用 `GetDrawGameIsOpen()`。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo --verbosity:minimal`：`0` 个错误、`1119` 个既有警告。
- `git diff --check`：通过。
- Unity PlayMode、真实 Firebase 配置切换、Android/iOS 横屏触控验证：`uncertain`，尚未执行。

## 剩余风险与回滚

- 面板保持打开时不会主动轮询配置；点击守卫可阻止关闭后的进入，但入口显隐只在打开面板或关闭状态下点击时刷新。
- 若未来调整 `DrawGameEnter` 层级，需要同步检查 `drawGameBtn.transform.parent` 是否仍是入口根节点。
- 回滚方式：移除 `OnOpen()` 的显隐刷新、`OnClickDrawLevel(...)` 的开关守卫和 `RefreshDrawGameEntryVisibility()`，即可恢复原始入口行为。

## 2026-08-25：入口展示 Draw 星星进度

### 需求、基线与影响范围

- 需求：`DrawGameEnter` 的 `ui_zhuye_03_txt` 展示当前有效 Draw 关卡已经永久获得的星星总数，
  `ui_zhuye_04_txt` 展示 `/{当前有效 Draw 关卡星星总数}`。
- 修改前两个 TMP 文本只有 prefab 占位值，入口没有独立进度脚本；`StartPanel.OnOpen()` 只刷新 Draw 功能开关显隐，
  不会在面板复用打开时刷新星星进度。
- 新增 `Assets/Game/UI/StartPanel/Scripts/DrawGameEnter.cs` 及 `.meta`，并在
  `Assets/Game/UI/DrawGame/StartPanel/Sprites/DrawGameEnter.prefab` 根节点挂载、绑定两个现有文本；
  `StartPanel.cs` 增加复用打开刷新。未修改 Draw 关卡、存档结构、配置、资源地址、事件、打点或平台流程。

### 修改后行为与数据流

1. `DrawGameEnter.OnEnable()` 与公开 `Refresh()` 读取 `GameManager.DrawGameLevel.LevelSorts`，按当前本地或远端最终有效排序计算。
2. 每个唯一且具备星级阈值的 `DrawLevelData.Id` 计 3 颗可获得星；无星级配置的等待关 `20999` 不计入总数。
3. `ui_zhuye_03_txt` 只汇总当前有效关卡在 `DB.DrawGame.LevelCollectedStar` 中保存的永久最高星数，
   单关结果夹取到 `0～3`；旧排序残留、重复 Id 和无效存档值不会抬高显示。
4. `ui_zhuye_04_txt` 以 `/{总数}` 格式刷新。控制器未就绪时降级显示 `0` 与 `/0` 并记录 Bear Logger 警告，
   不主动初始化配置，也不写存档。
5. `StartPanel.OnOpen()` 包含 inactive 子节点查找并缓存 `DrawGameEnter`，每次复用打开都调用 `Refresh()`；
   首次打开可能与组件 `OnEnable()` 各刷新一次，均为有界只读遍历。

### 验证、剩余风险与回滚

- 当前运行时 JSON 静态解析得到 58 个唯一有效星级关卡，预期总数为 174；prefab 两个 TMP fileID 绑定检查通过。
- `dotnet build Assembly-CSharp.csproj --no-restore ...` 通过：`0 error / 118 warning`；warning 属于当前工程既有范围。
- CodeGraph 确认 `StartPanel.OnOpen() -> DrawGameEnter.Refresh()` 调用关系；未新增 `Update()`、全局事件或存档写入。
- `uncertain`：尚未在 Unity PlayMode 验证清档、部分星级、满星、远端排序替换、返回首页复用打开，以及 Android/iOS 横屏显示。
- 回滚时移除 prefab 根节点的 `DrawGameEnter` 组件，删除新脚本及 `.meta`，并删除 `StartPanel` 中对应缓存与刷新调用；
  两个 TMP 节点、Draw 存档和配置无需迁移。
