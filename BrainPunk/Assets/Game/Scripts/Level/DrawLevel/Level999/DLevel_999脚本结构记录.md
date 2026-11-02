# DLevel_999 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame TBC 占位关 `20999 / DLevel_999`
- 记录日期：2026-08-18（更新）
- 交付状态：TBC 音效、降采样与进入“最小未通关主线关卡、全通后 Level_999”的传送门已实现；PlayMode 与移动端验证待补
- 参考基线：主线 `Level999Ctrl` 的两秒门禁、传送门表现、一次性跳转与 TBC 打点边界
- 选择理由：本关与主线 `Level_999` 都是末尾占位关，传送门需要以对称流程切回可继续游玩的最前主线关卡
- 主要脚本：共享 `LevelCtrl.cs`、`GameManager.cs`、`PlayCtrl.cs`、`DrawPlayCtrl.cs`，局部 `DLevel999Ctrl.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_999.prefab`

## 2. 脚本与组件树

```text
DrawPlayCtrl
├─ TryEnterLevel：记录 Draw 排序 ID 与资源路径
├─ OpenDrawLevelAsync：创建 HUD、加载并初始化 DLevel
├─ TryCreateGamePlayPanel：创建 HUD；DLevel_999 播放 TBC 音效
└─ BeginDrawing：保持既有 musicInGame 背景音乐逻辑

DLevel_999.prefab
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DLevel999Ctrl
└─ enterMainLevel
   ├─ CustomButton -> DLevel999Ctrl.EnterMainLevel
   ├─ OnTrigger2DHandle -> DLevel999Ctrl.EnterDoorAnim
   └─ ActorDestroyAnimHandle -> Actor / portal endTarget
```

## 3. 类职责与边界

| 类/组件 | 职责 | 本次边界 |
| --- | --- | --- |
| `DrawPlayCtrl` | Draw 选关、加载、HUD、输入与结算生命周期 | 仅在当前 `DrawLevelData.Path` 为 `DLevel_999` 时播放 `TBC` |
| `AudioManager` | 根据用户音效开关播放 Sounds Good SFX | 复用现有 `PlaySound("TBC")`，不新增 API |
| `DrawLevelCtrl` / `DrawLineFeature` | 本关运行时与画线输入 | 本次不修改 |
| `DLevel999Ctrl` | 本关主线传送门门禁、表现与跳转 | 不处理画线完成、墨水、胜负或主线关卡选择 |
| `LevelCtrl` / `GameManager` / `PlayCtrl` | 从最终主线排序选出未通关的最小 Id，全通后选择 Level_999，切换主线并进关 | 复用现有 `EnterLevelEvent`、加载、打点与失败回退链 |

## 4. 生命周期与关键调用

1. `TryEnterLevel()` 保存当前排序 ID 和 DLevel 资源路径。
2. `OpenDrawLevelAsync()` 调用 `TryCreateGamePlayPanel()`。
3. HUD 设置当前关卡显示 ID 后，`IsTbcLevel()` 检查本次请求保存的标准化资源路径。
4. 仅当资源路径为 `Assets/Game/Prefabs/DrawLevel/DLevel_999.prefab` 时调用 `AudioManager.PlaySound("TBC")`。
5. 后续 prefab 加载、Intro 和 `BeginDrawing()` 保持既有流程；`musicInGame` 背景音乐不变。
6. Reset/Replay 会重建 HUD，因此与主线 `RefreshGamePanel()` 一样再次播放 TBC 音效。

## 5. Prefab、配置与资源绑定

| 项目 | 证据 | 结果 |
| --- | --- | --- |
| Draw 配置 | `tbdrawleveldata.json`：`20999 -> DLevel_999` | 由进关链转换为标准化资源路径 |
| 音效标签 | `SoundCollection.asset`：`tag: TBC` 且绑定一个 AudioClip | 复用现有资源 |
| Prefab | `DLevel_999.prefab` | 保留采样参数；已绑定局部控制器、传送门按钮/Trigger、Actor 与终点 |

## 6. 数据、UI、资源与事件流

- DB/存档：不读写新增数据；音效门禁只读取当前控制器已保存的资源路径。
- UI：音效触发点与主线一致，位于 GameplayPanel 创建成功后。
- 音频：`DrawPlayCtrl -> AudioManager.PlaySound("TBC") -> Sounds Good SoundCollection`。
- 事件：传送表现复用 `GamePlayPanelSwitchBlockEvent`，主线进关复用 `EnterLevelEvent`，未新增全局事件类型。
- Analytics：主线进关请求被接受后调用 `EnterTBCPortal(type=1)`；Draw 退出与主线开始仍走既有 `level_end(exit)` / `level_start`。
- 广告、IAP、RemoteConfig、存档结构和平台分支：均未修改。

## 7. 相对参考基线的差异

- 主线通过 `Level.IsLastLevel` 判断；Draw 需求明确限定 `DLevel_999`，因此使用实际加载资源路径精确判断，避免运行时追加项不在原始排序表时漏播，也避免把其它普通关误当 TBC。
- Draw 背景音乐仍由 `BeginDrawing()` 管理；TBC 是一次性 SFX，不替代 `musicInGame`。

## 8. 结构复盘

- 本次只在共享控制器现有 HUD 创建边界增加关卡路径门禁，没有修改已验收 DLevel prefab 或新增全局事件。
- `DLevel_999` 是唯一消费者，证据不足以抽象新的共享“关卡专用音效”系统。

## 9. 验证证据与缺口

- Editor 日志确认上一版门禁复用原始 `TbDrawLevelSort` 时无法解析运行时追加的 `sortId:45`；当前实现不再依赖该表。
- 静态确认 `ResolveLevelResourcePath("DLevel_999")` 与门禁资源路径一致。
- 静态确认 `TBC` 存在于 `SoundCollection.asset` 且绑定 AudioClip。
- 静态确认 `AudioManager.PlaySound()` 会在 SFX 关闭时返回。
- 编译：`dotnet build Assembly-CSharp.csproj --no-restore -m:1 --nologo` 成功，`0 error / 101 个项目既有 warning`。
- `uncertain`：Unity PlayMode 首次进入、Reset/Replay、关闭音效、Android/iOS 横屏真机听感尚未验证。

## 10. 风险与回滚

- 风险：HUD 重建会再次播放 TBC，与主线当前行为一致，但连续 Reset 的听感仍需 PlayMode 确认。
- 已知旁路：Editor 日志中的 `Draw level_start skipped because DrawLevelData is missing. sortId:45` 来自既有 Analytics 当前关配置查询，不再阻断本次音效门禁；该查询修复不在本需求范围。
- 回滚：删除 `DrawPlayCtrl.TryCreateGamePlayPanel()` 中的 TBC 分支、`IsTbcLevel()` 和 `TbcLevelResourcePath` 常量；无需修改 prefab、配置、资源或存档。

## 11. 2026-08-06 历史基线：降低采样量；墨水循环暂不启用

> 本节保留 2026-08-06 当时的脚本与 Prefab 状态；`DLevel999Ctrl` 的现行职责和绑定以第 12 节为准。

### 11.1 需求与原行为基线

- 用户反馈本关画线累积后卡顿，要求先通过关卡参数降低采样量；随后明确墨水重置功能暂不触发。
- 原 `sampleSpacing=0.1`、`maxInkLength=300`，理论最多约 3000 个采样段；默认碰撞路径会按采样段创建子 `BoxCollider2D`。
- 原墨水耗尽后只停止追加采样，已完成线条保留到整关 Reset/Replay；没有本关局部自动循环。

### 11.2 最终结构与生命周期

- `DLevel_999.prefab` 最终将 `sampleSpacing` 从 `0.1` 调整为 `0.5`、`maxInkLength` 从 `300` 调整为 `150`；多笔、碰撞、质量配置与画线宽度保持不变，理论最大采样段降为约 300。
- `DLevel999Ctrl : IDrawStrokeCompletedHandler` 脚本保留备用，但当前未绑定到 Prefab，`DrawLevelCtrl` 不会扫描到该实例。
- 墨水耗尽后沿用原行为：停止追加采样，已完成线条保留到正常 Reset/Replay；不会自动清线或补满墨水。

### 11.3 绑定、边界与验证缺口

- Prefab 根节点没有绑定 `DLevel999Ctrl`，备用脚本不进入运行时 Handler 扫描。
- 不修改共享 `DrawLineFeature`、`DrawLevelCtrl`、其它 DLevel、DB/存档、Luban、资源地址、UI、Analytics、广告或平台 SDK。
- 静态确认 Prefab 不包含 `DLevel999Ctrl` 组件及脚本 GUID，因此不会进入墨水重置回调链。
- `Assembly-CSharp.csproj` 定向构建通过，结果为 `0 error / 101 个项目既有 warning`。
- `uncertain`：尚未在 Unity PlayMode 验证 0.5 间距和 150 墨水容量的曲线手感、墨水耗尽表现、Reset/Replay 和 Android/iOS 横屏帧率。

### 11.4 风险与回滚

- 采样间距增大会降低急转弯轨迹与碰撞轮廓精度；基础墨水减半会改变同长度线条按 `maxInkLength` 归一化后的质量比例。墨水循环当前未启用，不构成运行时行为风险。
- 回滚降采样时把 `sampleSpacing` 恢复为 `0.1`、`maxInkLength` 恢复为 `300`；共享代码、存档和资源不需要迁移。
  若以后重新启用墨水循环，应创建或复用独立的局部 Handler，不得依赖已转为传送门控制器的 `DLevel999Ctrl`，并补做
  PlayMode 验证。

## 12. 2026-08-18：传送门进入最小序列的已解锁未通关主线关卡

### 12.1 原行为与新行为

- 修改前，Prefab 已存在从主线关复制的传送门美术、按钮、Trigger 与 `ActorDestroyAnimHandle`，但两个 UnityEvent 的
  `m_Target` 均为空、类型仍写作 `Level999Ctrl`，ActorDestroy 的 `target` 也为空，因此无法形成有效运行时跳转。
- 修改后，`DLevel999Ctrl` 在进场两秒后开放入口；按钮可直接调用 `EnterMainLevel()`，玩家进入 Trigger 时先锁定 HUD、
  播放两秒 Actor 入门表现并在 1.2 秒处按设置触发成功震动，然后进入目标主线关卡。
- `DLevel999Ctrl` 已从未启用的墨水循环候选改为传送门控制器，不再实现
  `IDrawStrokeCompletedHandler`；因此即使组件现在绑定到 Prefab，墨水耗尽仍不会自动清线或补充。

### 12.2 生命周期与共享边界

1. `GameManager.TryEnterMainLevel()` 通过 `PlayControllerManager` 从 Draw 切到 Main。
2. `DrawPlayCtrl.Deactivate()` 先按既有契约发送 `level_end(exit)`，再清理 DLevel、HUD 和旧异步请求。
3. 已激活的 `PlayCtrl.TryEnterLowestUnlockedUnpassedLevel()` 调用
   `LevelCtrl.TryGetLowestUnlockedUnpassedLevel()`，在最终主线排序中筛选“配置有效、已解锁、未通关”的项并取最小
   `LevelSort.Id`，再派发现有 `EnterLevelEvent`。
4. `PlayCtrl.OnGameEnter()` 将所选 Id 写入既有 `DB.GameData.CurrentLevel`，并继续负责主线运行态、`level_start`、
   加载界面和关卡实例创建。
5. 请求被接受后上报 `enter_tbc_portal(type=1)`；失败分支不打点，主线配置失败时回退 StartPanel。

本次没有新增全局事件、主线关卡选择算法、DB 字段、配置、资源加载入口或平台分支；`GameManager` 的共享 API 同时服务
`ILevel_999`，两个关卡的门禁和表现分别保留在局部控制器中，不提取新的 TBC 基类。

### 12.3 验证、风险与回滚

- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 109 warning`，warning 为工程既有范围。
- 静态确认筛选不依赖旧 `CurrentLevelSort`：已通关、未解锁或缺少 `LevelData` 的排序项都会跳过，候选按
  `LevelSort.Id` 比较而非存档当前值。
- 静态确认 DLevel Prefab 的控制器脚本 GUID、按钮/Trigger 目标、ActorDestroy target/endTarget 均可解析，且本地 YAML
  anchor 无重复、非零本地引用无缺失。
- `uncertain`：Unity PlayMode 的两秒门禁、碰撞与点击竞争、Actor 完整消失、Draw `level_end(exit)` 与主线
  `level_start` 顺序、失败回退、GuruSDK 后台收数及 Android/iOS 横屏触控/震动尚未验证。
- 回滚时移除 Prefab 的 `DLevel999Ctrl` 组件和三个传送门绑定，恢复局部脚本为未绑定状态；若共享直达主线 API 无其它
  消费者，再同步移除 `GameManager.TryEnterMainLevel()`、`PlayCtrl.TryEnterLowestUnlockedUnpassedLevel()` 与
  `LevelCtrl.TryGetLowestUnlockedUnpassedLevel()`。无需迁移存档或配置。

## 13. 2026-08-19：主线传送门绑定恢复与点击射线隔离

### 13.1 修复基线

- 本轮开始时，Prefab 中已有重新复制的传送门、结束点和 `SupportEnter`，但根节点未挂载 `DLevel999Ctrl`；按钮与
  Trigger 的 UnityEvent 目标仍指向根 GameObject、方法名为空且类型声明为主线 `Level999Ctrl`，
  `ActorDestroyAnimHandle.target` 也为空，因此点击与进入 Trigger 都不能完成主线跳转。
- `DrawLineArea` 的 BoxCollider2D 仍启用。`ClickItemEventHandle` 使用未限定 LayerMask 的点击射线时，该区域碰撞体可能
  先于传送门按钮被命中；本关 `enableDrawAreaRestriction=0`，它不承担现行画线边界职责。

### 13.2 最终绑定与局部边界

- Prefab 根节点重新挂载 `DLevel999Ctrl`，并将其 `actorDestroy` 指向当前传送门的
  `ActorDestroyAnimHandle`。
- `ClickItemEventHandle.OnClickEvent` 绑定 `DLevel999Ctrl.EnterMainLevel()`；
  `OnTrigger2DHandle.OnEnter` 绑定 `DLevel999Ctrl.EnterDoorAnim()`。
- `ActorDestroyAnimHandle.target` 绑定当前 Actor Transform，`endTarget` 保留传送门终点；点击仍直接进入主线，碰撞仍先
  播放吸入表现再进入主线。
- 仅在 `DLevel_999.prefab` 内禁用 `DrawLineArea` 的 BoxCollider2D，传送门自身 Collider 保持启用。本次没有修改共享
  `ClickItemEventHandle`、`DrawLineFeature` 或 Physics2D Layer Collision Matrix；若以后启用本关画线区域限制，必须重新
  设计点击射线过滤或局部输入仲裁，不能直接复用当前禁用方案。
- 用户已保存的传送门美术、结束点、`SupportEnter`、摄像机参数及其它关卡布局均保持原样。

### 13.3 数据流、验证与回滚

- 未新增脚本、全局事件、配置、资源地址或存档字段；跨玩法切换、Draw 退出清理、主线选择与 Analytics 仍沿用第 12 节
  记录的既有调用链。
- Prefab 静态校验：245 个定义 anchor 全部唯一，485 个本地引用无缺失，9 个脚本 GUID 全部可解析；控制器、按钮、
  Trigger、Actor target/endTarget 的目标引用均存在。
- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 1124 warning`，warning 为当前工程既有范围。
- `uncertain`：Unity 当前处于打开状态，本轮未另起 BatchMode 导入；PlayMode 的点击/Trigger、Actor 吸入、Draw
  `level_end(exit)` 与主线 `level_start` 顺序、GuruSDK 后台收数和 Android/iOS 横屏触控仍未验证。
- 局部回滚：移除 Prefab 根节点的 `DLevel999Ctrl`，清空按钮/Trigger/ActorDestroy 绑定，并恢复 `DrawLineArea`
  BoxCollider2D；共享跨玩法 API、存档和配置无需回滚。若只回滚点击射线隔离，则仅恢复该 Collider，但会重新引入点击被
  区域碰撞体截获的风险。

## 14. 2026-08-19：弹窗打开时禁止点击穿透传送门

### 14.1 原行为与修改后行为

- 原 `ClickItemEventHandle` 在 `Update()` 中直接读取鼠标/触摸并执行 Physics2D 射线，不经过 Unity UI EventSystem；即使
  Bear.UI 弹窗带遮罩，射线仍可能命中弹窗下方的传送门，并直接调用 `EnterMainLevel()`。
- Prefab 的点击 UnityEvent 现改绑 `DLevel999Ctrl.EnterMainLevelByClick()`。该入口只在运行时检查
  `UIManager` 子级的 `BaseUIView`：Popup、Top 或 System 层只要存在 `IsOpen=true` 的视图，本次点击即被忽略；已关闭但
  仍缓存的视图不会阻断。
- 弹窗门禁命中时不会设置 `_hasTriggeredMainEntry`，弹窗关闭后仍可重新点击。Trigger 继续调用
  `EnterDoorAnim()`，不经过点击专用门禁，保持玩家实际走入传送门的既有表现。

### 14.2 边界、性能与验证

- 本次仅修改 `DLevel999Ctrl.cs` 与 `DLevel_999.prefab` 的点击方法名；没有修改共享
  `ClickItemEventHandle`、Bear.UI、其它关卡、主线选择、DB/存档、配置、事件或 Analytics。
- 弹窗查询只在传送门实际收到点击 UnityEvent 时执行，不进入每帧轮询；数组分配频率受玩家点击限制，对 Android/iOS
  运行时开销较低。若后续多个关卡稳定复用该需求，再评估由 UIManager 提供无分配公开查询，本轮不扩展共享接口。
- 最终 `Assembly-CSharp.csproj --no-dependencies` 定向构建通过：`0 error / 106 warning`；本轮此前完整依赖构建为
  `0 error / 1124 warning`，warning 均为当前工程既有范围。Prefab 静态确认点击方法仅绑定一次
  `EnterMainLevelByClick`，旧点击绑定已移除，Trigger 仍绑定 `EnterDoorAnim`。
- `uncertain`：Unity PlayMode 中设置、提示、支持我们等 Popup，以及 Top/System 覆盖层下的实际点穿、关窗后再次点击、
  Android/iOS 多点触控仍未验证。
- 回滚时把 Prefab 点击 UnityEvent 恢复为 `EnterMainLevel`，删除 `EnterMainLevelByClick()`、`HasOpenPopup()` 及
  `Bear.UI` using；Trigger、主线跳转、存档和打点无需迁移。

## 15. 2026-08-21：主线目标解析改为最小未通关与全通兜底

### 15.1 最终结构与职责

- `DLevel999Ctrl` 的局部职责未变，仍只拥有传送门门禁、点击穿透检查、演出和玩法来源打点；它继续调用
  `GameManager.TryEnterMainLevel()`，不直接读取主线存档或配置。
- 共享目标解析改由 `PlayCtrl.TryEnterLowestUnpassedOrLastLevel()` 调用
  `LevelCtrl.TryGetLowestUnpassedOrLastLevel()`：先从最终排序中选择有效且未通关的最小 Id；全部通关时固定选择
  `10999 / Level_999`。解锁集合不再参与本入口筛选。
- 本次只修改 `DLevel999Ctrl.cs` 的事实注释和三个共享脚本；没有修改本关 Prefab、序列化绑定、资源、UI、事件类型、
  存档结构或平台分支。

### 15.2 结构复盘、验证与回滚

- 结构复盘结论：DLevel999 与 ILevel999 已有两个稳定消费者，目标选择继续保留在主线 `LevelCtrl / PlayCtrl / GameManager`
  边界是合适的共享能力；两关传送门表现仍保持局部，不新增基类或全局事件。
- 静态配置矩阵确认 `1 -> Level_001`、前五关通关后 `6 -> Level_006`、全通后
  `107 -> 10999 -> Level_999`；CodeGraph 确认新查询只有既定调用链。
- `Assembly-CSharp` 定向构建为 `0 error / 106 warning`。Unity PlayMode、通关空洞档、远端排序、加载失败、
  Analytics 实际顺序和 Android/iOS 横屏仍为 `uncertain`。
- 回滚只需恢复旧查询方法与解锁过滤并删除全通兜底；DLevel999 的 Prefab 和局部控制器绑定无需回滚。

## 16. 2026-08-21：点击门禁排除常驻 GMPanel

### 16.1 最终结构与调用边界

- `EnterMainLevelByClick()` 仍先调用局部 `HasOpenPopup()`，通过后才进入既有 `EnterMainLevel()`；没有改变 Prefab
  UnityEvent、Trigger、Actor 表现或跨玩法切换入口。
- `HasOpenPopup()` 继续扫描 `UIManager` 子级的已打开 `BaseUIView`，但在 Popup、Top、System 层级判断前排除
  `GMPanel`。原因是 DEBUG_MODE 下它由 `StartPanel` 创建为常驻 System 调试宿主，不能等同于玩家可见的阻断覆盖层。
- 其它已打开的 Popup、Top、System 视图仍会阻止世界点击；Normal 层 Draw GameplayPanel 和 Trigger 入口保持原边界。

### 16.2 证据、风险与回滚

- 本地代码证据：`GMPanel.Create()` 使用 `UILayer.System`，`GMPanel.OnOpen()` 调用 `base.OnOpen()`；Editor 日志显示
  GMPanel 打开后进入多次 DrawGame 流程但没有对应关闭记录。
- 本次不修改共享 Bear.UI、`GMPanel`、`ClickItemEventHandle`、Prefab、存档、配置、事件或 Analytics。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 106 warning`，warning 为工程既有范围；Unity PlayMode 的真实点击、各类弹窗遮挡、GM 展开状态和
  Android/iOS 横屏触控仍为 `uncertain`。
- 回滚仅删除 DLevel999 的 `GMPanel` 排除分支；若未来多个世界点击入口出现相同常驻视图问题，再基于回归证据评估
  UIManager 的统一只读查询，本轮不提升共享抽象。

## 17. 2026-08-21：入口门禁延迟序列化

- `DLevel999Ctrl` 移除固定 `MainEntryEnableDelay` 常量，新增 Inspector 字段 `mainEntryEnableDelay`，默认 `2f`、最小显示值
  为 `0`；`EnableMainEntryAfterDelay()` 读取该字段并执行运行时非负保护。
- 字段只影响 `_canEnterMainLevel` 的开放时机；Trigger 演出结束后会通过局部 `WaitForMainEntryEnabled()` 等待门禁剩余时间，
  避免大于两秒的配置丢失单次跳转。点击 Popup 检查、Trigger UnityEvent、主线目标选择、存档、资源、事件和 Analytics
  不变；本次不编辑 Prefab，未覆盖时沿用脚本默认值。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 106 warning`，warning 为工程既有范围；Unity PlayMode 的 `0/2` 秒、较大配置下 Trigger 等待期间的
  HUD/Actor 表现、Reset/Replay 和移动端仍为 `uncertain`。
- 回滚时恢复固定常量、删除序列化字段及 Trigger 剩余等待，无需迁移 Prefab、存档或配置表。

## 18. 2026-09-11：SupportUs 重复触发防护覆盖

- `DLevel_999.prefab` 现有 `SupportEnter` 是公共 `SupportEnter.prefab` 的嵌套实例，点击与 Trigger 均进入共享
  `SupportUsPopup.Create()`；该工厂现会在弹窗 `IsOpen=true` 时返回现有实例，不再重复调用 UIManager.OpenUI。
- 本关传送门仍通过 `EnterMainLevelByClick() -> HasOpenPopup()` 阻断 Popup 下的世界点击；SupportUs 工厂幂等负责阻断
  穿透到 SupportEnter 自身后的重复打开请求，两者职责不同且无需新增 DLevel999 局部组件或事件。
- 本轮未修改 DLevel999 脚本或 Prefab。静态确认 SupportEnter 实例与传送门点击绑定各 1 处；共享代码定向构建为
  `0 error / 123 warning`。PlayMode 连点、关窗重开及 Android/iOS 多点触控仍为 `uncertain`。
- 回滚 SupportUs 幂等行为只需恢复 `SupportUsPopup.Create()`；不应回滚本关既有传送门 Popup 门禁。
