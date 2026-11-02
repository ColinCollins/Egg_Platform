# DLevel_999 主线传送门功能与风险评估

## 1. 需求与影响范围

- 需求：点击或进入 `DLevel_999` 传送门后，不返回 StartPanel，进入当前未通关关卡中
  `LevelSort.Id` 最小的有效主线关卡；全部通关时进入 `Level_999`，门禁与表现参考主线 `Level999Ctrl`。
- 运行时代码：`DLevel999Ctrl.cs`、`LevelCtrl.cs`、`GameManager.cs`、`PlayCtrl.cs`。
- 资源：`Assets/Game/Prefabs/DrawLevel/DLevel_999.prefab`。
- 复用：`ActorDestroyAnimHandle`、`GamePlayPanelSwitchBlockEvent`、`EnterLevelEvent`、
  `PlayControllerManager`、`GameSDKService.EnterTBCPortal(int)`。

## 2. 原行为基线

- Prefab 已有传送门美术、CustomButton、Player Trigger 和 ActorDestroyAnimHandle，但按钮与 Trigger 的 UnityEvent
  目标为空且仍声明主线 `Level999Ctrl`，ActorDestroy 的玩家 target 也为空，运行时不能跳转。
- `DLevel999Ctrl` 是未绑定的墨水循环候选；墨水耗尽仍按现行行为保留旧线到 Reset/Replay。
- Draw 退出到其它玩法时，`DrawPlayCtrl.Deactivate()` 负责 `level_end(exit)` 与现场清理。

## 3. 修改后行为与流程

1. 关卡实例创建后，`DLevel999Ctrl` 等待两秒开放入口。
2. Trigger 调用 `EnterDoorAnim()`：显示 HUD 输入遮罩、播放 ActorDestroy，1.2 秒后按设置震动，再等待 0.8 秒。
3. 按钮直接调用或表现结束调用 `EnterMainLevel()`；一次性门禁阻止当前实例重复发起请求。
4. `GameManager.TryEnterMainLevel()` 切换到主线控制器；Draw 停用阶段先发送退出结束事件并清理当前运行时。
5. `PlayCtrl.TryEnterLowestUnpassedOrLastLevel()` 通过 `LevelCtrl` 扫描最终主线排序，跳过已通关和配置缺失项，
   选择 `LevelSort.Id` 最小的候选；全部通关时选择 `10999 / Level_999`，随后派发现有 `EnterLevelEvent`。
6. `PlayCtrl.OnGameEnter()` 沿既有流程把所选 Id 写入 `DB.GameData.CurrentLevel` 并创建该关卡。
7. 进关请求被接受后上报 `enter_tbc_portal(type=1)`；请求失败时不上报，目标配置无效时回退 StartPanel。

## 4. 系统边界

- DB/存档：不新增字段；进关沿既有 `OnGameEnter()` 更新 `CurrentLevel`，不修改解锁/通关集合、Draw 进度或墨水数据。
- 配置/资源：不新增 Luban、RemoteConfig 或资源地址；主线目标由 `LevelCtrl` 的最终排序及现有解锁/通关存档解析。
- UI/事件：复用现有 HUD 遮罩与 `EnterLevelEvent`，不新增 PlayEvents；不会先创建 StartPanel。
- Analytics：保留 Draw `level_end(exit)` 与主线 `level_start` 既有顺序，另在请求接受后发送 TBC 门事件。
- 平台：Android/iOS 共用代码；震动继续受 `DB.GameSetting.VibrationOn` 控制。

## 5. 风险与验证

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| 快速点击/多 Collider 重复跳转 | 中 | 两秒门禁与实例级一次性锁 | PlayMode 多点触控和复合 Collider 待验 |
| 退出/开始打点顺序改变 | 高 | 只经 `PlayControllerManager` 切换；Draw Deactivate 先执行退出上报 | GuruSDK 后台事件序列待验 |
| 主线配置缺失后无界面 | 中 | 进关校验失败时恢复 StartPanel | 故障注入待验 |
| 当前存档关卡不是最小可玩关 | 中 | 不读取 `CurrentLevelSort` 作为目标，按最终排序与解锁/通关集合重新筛选 | 多候选存档矩阵待验 |
| ActorDestroy target 或 UnityEvent 丢失 | 中 | Prefab 显式绑定并做静态 fileID/GUID 校验 | Unity 导入和 PlayMode 待验 |
| 移动端震动/触控差异 | 低 | 复用主线同一节奏和设置开关 | Android/iOS 真机待验 |

- 定向 C# 构建：`0 error / 109 warning`，warning 为工程既有范围。
- 当前本地 `tblevelsort.json` 静态检查为 91 行、Id 严格递增且无重复；运行时查询仍按最终排序逐项比较 Id，
  不依赖列表预先有序。
- 静态确认两个公开入口、Prefab 组件、按钮/Trigger 回调、ActorDestroy target/endTarget 与脚本 GUID 均存在。
- `uncertain`：尚未执行 Unity PlayMode、GuruSDK 真实后台和 Android/iOS 横屏验证。

## 6. 回滚路径

1. 从 DLevel Prefab 移除 `DLevel999Ctrl` 组件，清空传送门按钮/Trigger 目标并恢复 ActorDestroy target 为空。
2. 恢复 `DLevel999Ctrl` 的未绑定状态；采样参数与墨水现行行为不需要回滚。
3. 若 `ILevel999Ctrl` 也回滚且共享 API 无其它调用者，再移除 `GameManager.TryEnterMainLevel()`、
   `PlayCtrl.TryEnterLowestUnpassedOrLastLevel()` 与 `LevelCtrl.TryGetLowestUnpassedOrLastLevel()`。
4. 无需迁移存档、配置或资源包数据。

## 7. 2026-08-19：Prefab 绑定恢复风险复核

### 7.1 本轮处理

- 在用户已保存的传送门对象基础上，恢复 Prefab 根节点的 `DLevel999Ctrl` 组件，并重新绑定按钮点击、玩家 Trigger、
  `ActorDestroyAnimHandle.target/endTarget` 和控制器的 `actorDestroy` 引用。
- 本关 `enableDrawAreaRestriction=0`，因此局部禁用 `DrawLineArea` BoxCollider2D，避免
  `ClickItemEventHandle` 的无 LayerMask 射线先命中区域碰撞体；传送门自身 Collider 保持启用。
- 没有修改共享点击组件、画线组件、主线选择算法、事件、存档、配置、SDK 或其它 DLevel Prefab。

### 7.2 风险更新

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| `DrawLineArea` 截获点击射线 | 中 | 在区域限制关闭的 DLevel999 内局部禁用其 Collider | PlayMode 多点触控与不同屏幕比例待验 |
| 点击与 Trigger 同帧重复请求 | 中 | `DLevel999Ctrl` 的实例级一次性门禁 | 实际复合 Collider 进入顺序待验 |
| Actor 或 UnityEvent 再次丢失 | 中 | 目标改为组件 fileID 并完成本地引用/GUID 静态校验 | Unity Editor 导入、Prefab Apply/重开待验 |
| 将来重新启用画线区域限制 | 中 | 当前明确以 `enableDrawAreaRestriction=0` 为前提 | 启用时需改为局部射线过滤或输入仲裁 |

### 7.3 验证与回滚

- Prefab 校验结果：245 个定义 anchor、0 重复；485 个本地引用、0 缺失；9 个脚本 GUID、0 无法解析。
- `Assembly-CSharp.csproj` 定向构建：`0 error / 1124 warning`，warning 为当前工程既有范围。
- `uncertain`：Unity 当前已打开，本轮没有另起 BatchMode；PlayMode、GuruSDK 事件序列、异常加载回退及 Android/iOS
  横屏真机尚未验证。
- 回滚时可只撤销 `DLevel_999.prefab` 本轮恢复的控制器/UnityEvent/Actor 引用，并重新启用
  `DrawLineArea` Collider；若恢复 Collider，应同步接受或解决它再次截获点击的风险。

## 8. 2026-08-19：弹窗点击穿透门禁

### 8.1 行为边界

- 世界点击不再直接绑定 `EnterMainLevel()`，而是先进入 `EnterMainLevelByClick()`。
- Bear.UI 的 Popup、Top、System 层存在任意 `IsOpen=true` 的 `BaseUIView` 时，点击入口返回且不占用控制器的一次性跳转锁；
  弹窗关闭后可以重新点击。
- Trigger 入门表现不经过该门禁，避免把“禁止弹窗点击穿透”扩大成角色碰撞规则变更。
- 未新增全局事件、存档、配置、资源或打点；成功跳转后的 Draw `level_end(exit)`、主线 `level_start` 与
  `enter_tbc_portal(type=1)` 顺序保持不变。

### 8.2 风险与验证

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| Popup 遮罩下仍触发世界点击 | 中 | 点击入口主动扫描 Bear.UI 已打开的 Popup 及更高层视图 | PlayMode 各类弹窗待验 |
| 已关闭缓存视图误拦截 | 低 | 只判断 `BaseUIView.IsOpen`，标准 `OnClose()` 会清零 | 未调用 base 的非标准弹窗待排查 |
| 点击门禁误伤角色碰撞入门 | 低 | Prefab 仅把 Click UnityEvent 改绑新入口，Trigger 保持原绑定 | 弹窗打开时角色走入门的产品预期待验 |
| 点击时扫描 UI 子级产生分配 | 低 | 只在实际点击发生时扫描，不在 Update 中执行 | 低端机高频连点待验 |

- 最终 `Assembly-CSharp --no-dependencies` 定向构建为 `0 error / 106 warning`；本轮此前完整依赖构建为
  `0 error / 1124 warning`。静态确认新旧点击方法绑定数量分别为 `1/0`，Trigger `EnterDoorAnim` 绑定数量为 `1`。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 横屏真机验证弹窗打开、关闭同帧、连续点击及多点触控。
- 回滚只需恢复 Prefab 点击方法名并删除 DLevel999 的两个局部点击门禁方法和 `Bear.UI` 引用；不影响共享系统。

## 9. 2026-08-21：最小未通关关卡与全通后 Level_999 兜底

### 9.1 需求、基线与修改后行为

- 本节覆盖第 1、3、5、6 节中“只进入已解锁未通关关卡、无候选时回到 StartPanel”的旧目标选择语义。
- 修改前，`LevelCtrl.TryGetLowestUnlockedUnpassedLevel()` 同时要求候选存在于 `UnlockLevels` 且不在
  `PassedLevels`；所有有效关卡都已通关或解锁集合不完整时，目标解析失败并由 `GameManager` 恢复 StartPanel。
- 修改后，`LevelCtrl.TryGetLowestUnpassedOrLastLevel()` 只按当前最终主线排序、有效 `LevelData` 和
  `PassedLevels` 选择 `LevelSort.Id` 最小的未通关关卡，不再用 `UnlockLevels` 过滤。若所有有效排序项都已通关，
  固定查找 `10999 / Level_999` 并进入该关；仅当主线排序、通关存档或 `Level_999` 配置无效时保留 StartPanel 故障回退。
- `PlayCtrl.TryEnterLowestUnpassedOrLastLevel()` 仍只派发现有 `EnterLevelEvent`；DLevel999 的点击、Trigger、两秒门禁、
  Actor 吸入、HUD 遮罩和 `enter_tbc_portal(type=1)` 均未修改。本次没有编辑 `DLevel_999.prefab`。

### 9.2 数据、事件、风险与验证

- 数据流只读取最终 `LevelSorts`、`LevelData` 和 `DB.GameData.PassedLevels`；不写入或补齐
  `UnlockLevels`、`PassedLevels`，目标进入后仍由主线既有链更新 `CurrentLevel`。
- 跨玩法切换仍先产生 Draw `level_end(exit)`，再由主线标准进关链产生 `level_start`；与
  `DevelopMD/关卡打点生命周期约定.md` 第 2.1、2.5 节无冲突。全通后进入已通关的 `Level_999` 时，
  `start_type` 继续按该文档第 3.1 节由通关状态判定为 `replay`。
- 新的产品语义会允许进入“未通关但不在 UnlockLevels”的主线关卡；这是本次需求明确取消解锁过滤的直接影响。
  若存档出现异常通关空洞，玩家会进入其中 Id 最小的空洞关，而不是当前最高解锁关。
- 静态配置矩阵：无通关记录解析到 `1 / 10001 / Level_001`；前五关通关解析到
  `6 / 10006 / Level_006`；107 个最终排序项全部通关时解析到 `107 / 10999 / Level_999`，且
  `10999` 兜底排序项唯一。
- CodeGraph 确认新查询只有 `PlayCtrl` 一个调用者，DLevel 与 ILevel 继续经同一 `GameManager.TryEnterMainLevel()`
  边界进入；没有覆盖测试。`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo`
  通过，结果为 `0 error / 106 warning`，warning 为工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 验证通关空洞、全通档、远端排序替换、资源加载失败、
  Draw `level_end(exit)` 与主线 `level_start` 实际顺序，以及 Android/iOS 横屏触控。
- 回滚时恢复两个旧查询方法名与 `UnlockLevels` 过滤，删除 `10999` 全通兜底分支，并同步恢复本节覆盖的旧文档语义；
  Prefab、存档、配置和 SDK 无需迁移。

## 10. 2026-08-21：常驻 GMPanel 不再误拦截传送门点击

### 10.1 问题基线与修复后行为

- Unity Editor 日志确认 `StartPanel.OnCreate()` 在 `DEBUG_MODE` 下创建 `GMPanel`，该视图以 `UILayer.System` 打开，
  `GMPanel.OnOpen()` 调用基类后会长期保持 `IsOpen=true`；进入 DrawGame 后没有对应的关闭记录。
- 修改前，`DLevel999Ctrl.HasOpenPopup()` 把任意已打开的 Popup、Top、System 视图都视为阻断条件，因此常驻
  `GMPanel` 会让 `EnterMainLevelByClick()` 每次都提前返回。该问题与 `DrawLineArea` Collider 是否启用无关。
- 修改后，点击门禁先跳过已打开的 `GMPanel`，再按原规则阻断其它 Popup、Top、System 视图。传送门两秒开放门禁、
  Trigger 入门表现、主线目标解析及一次性跳转锁保持不变。

### 10.2 影响边界、验证与回滚

- 本次只修改 `DLevel999Ctrl.cs` 的局部 Bear.UI 查询，不修改 `GMPanel`、`UIManager`、共享
  `ClickItemEventHandle`、Prefab、DB/存档、配置、资源、事件类型、Analytics 或平台 SDK。
- 静态确认 `GMPanel` 排除发生在层级判断之前，同时其它 Popup、Top、System 分支仍然保留；
  `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 106 warning`，warning 为工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 验证无弹窗点击、设置/支持我们弹窗遮挡、GM 调试界面实际展开时的点击预期、
  进场两秒门禁以及 Android/iOS 横屏触控。
- 风险：当前排除对象是整个 `GMPanel` 视图；若以后 GMPanel 自身改为全屏交互覆盖层，需要重新定义它展开时是否应阻断
  世界点击。回滚时删除 `popupView is GMPanel` 分支即可，不需要迁移 Prefab、存档或配置。

## 11. 2026-08-21：主线入口开放延迟改为 Inspector 参数

### 11.1 需求、基线与修改后行为

- 修改前，`DLevel999Ctrl` 使用私有常量固定等待 `2f`，关卡制作者不能在 Inspector 调整；门禁从控制器 `Start()` 开始
  计时，等待期间到达 `EnterMainLevel()` 的调用直接忽略且不会排队。
- 修改后，等待秒数由 `[SerializeField, Min(0f)] mainEntryEnableDelay` 提供，默认仍为 `2f`；设为 `0` 时进场后立即
  开放，正值按各自 Prefab/实例配置等待。运行时使用 `Mathf.Max(0f, value)` 防止异常 YAML 负值。
- Trigger 固定两秒演出结束时若门禁尚未开放，会通过对象销毁令牌等待剩余时间再调用 `EnterMainLevel()`；因此配置值
  大于两秒时不会再因单次调用过早而丢失跳转。
- 本次只暴露 DLevel999 自身的门禁参数，不修改主线 `Level999Ctrl.DrawEntryEnableDelay`、传送门两秒 Actor 演出、Popup
  防穿透、目标主线解析、一次性跳转锁或打点流程。

### 11.2 影响、验证与回滚

- 影响文件仅为 `DLevel999Ctrl.cs` 与本地记录；没有手工写入 `DLevel_999.prefab`，现有组件在字段无显式覆盖时使用脚本
  默认值 `2f`，后续可由 Unity Inspector 保存独立覆盖值。
- 静态确认固定常量已移除，等待调用只读取新的序列化字段；
  `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 106 warning`，warning 为工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 验证 `0`、`2` 和大于传送门演出时长的配置、首次点击临界帧、Trigger 等待期间的
  HUD/Actor 表现、Reset/Replay 及 Android/iOS 横屏触控。
- 风险：较大延迟会让 Trigger 的 Actor 演出先结束、HUD 继续锁定直到门禁开放，这是参数配置的直接可见影响。
  回滚时恢复 `MainEntryEnableDelay = 2f` 常量、删除序列化字段及 Trigger 剩余等待。

## 12. 2026-09-11：SupportUsPopup 重复打开门禁覆盖

### 12.1 需求、基线与现行行为

- 需求：DrawGame `DLevel_999` 中的 SupportUs 入口也不得在弹窗已打开期间被重复触发。
- 静态确认 `DLevel_999.prefab` 嵌套一份 `SupportEnter.prefab`，源 GUID 为
  `899f0c9a5be38494283e75b145947f38`；该公共 Prefab 的点击和 Trigger 都经
  `SupportUsCtrl.OpenSupportUsPopup()` 调用 `SupportUsPopup.Create()`。
- 修改前，SupportUsPopup 已在 Popup 栈顶时，世界点击仍可能再次命中 SupportEnter；UIManager 会拒绝重复入栈，
  但输出 `OpenUI aborted` 错误。该重复请求与 DLevel999 传送门已有的 `HasOpenPopup()` 门禁是两条独立调用链。
- 现行 `SupportUsPopup.Create()` 在调用 `OpenUI` 前检查已注册实例；`IsOpen=true` 时直接返回现有实例，因此
  DLevel_999 的点击与 Trigger 都不会重复打开、重复执行 `OnOpen` 或产生上述 UIManager 错误。关闭后仍允许重新打开。
- DLevel999 主线传送门继续绑定 `EnterMainLevelByClick()`，Popup 打开时由原有 `HasOpenPopup()` 阻断世界点击；
  两层门禁共同覆盖“穿透到传送门”和“穿透到 SupportEnter 自身”。

### 12.2 影响、验证、风险与回滚

- 本轮无需修改 `DLevel999Ctrl.cs` 或 `DLevel_999.prefab`；行为变化来自共享
  `Assets/Game/UI/SupportUs/Scripts/SupportUsPopup.cs` 的幂等打开入口。
- 静态断言通过：DLevel_999 中 SupportEnter 源 Prefab 实例为 1 处，`EnterMainLevelByClick` 绑定为 1 处；
  SupportUsPopup 的初始化、`GetUI`、`IsOpen` 与 `OpenUI` 顺序检查通过。
- 包含该共享修改的 `Assembly-CSharp` 定向构建通过：`0 error / 123 warning`；未建立修改前 warning 数量基线。
- 未修改 Draw 关卡状态、画线、Reset/Replay、DB/存档、配置、资源地址、事件、广告逻辑、Analytics 或平台分支。
- `uncertain`：尚未在 DLevel_999 PlayMode 实测 SupportUs 连续点击、点击与 Trigger 同帧触发、关窗重开、弹窗下传送门点击，
  以及 Android/iOS 横屏多点触控。
- 回滚时恢复 `SupportUsPopup.Create()` 为直接调用 `OpenUI` 即可撤销 SupportUs 幂等门禁；DLevel999 的传送门 Popup 门禁
  应独立保留，无需迁移 Prefab、存档或配置。
