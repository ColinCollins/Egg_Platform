# ILevel999Ctrl 功能与风险记录

## 1. 需求与影响范围

- 需求：`ILevel_999` 传送门逻辑与主线 `Level999Ctrl` 保持一致，但目标是进入未通关关卡中
  `LevelSort.Id` 最小的有效主线关卡；全部通关时进入 `Level_999`，不返回 StartPanel。
- 运行时代码：`ILevel999Ctrl.cs`、`LevelCtrl.cs`、`GameManager.cs`、`PlayCtrl.cs`。
- 资源：`Assets/Game/Prefabs/WannerLevel/ILevel_999.prefab`。
- 复用系统：IWanner TBN 关卡生命周期、`ActorDestroyAnimHandle`、HUD 输入遮罩、玩法控制器切换、主线既有进关链与
  TBC 传送门打点。

## 2. 原行为基线

- Prefab 已有从主线末关复制的传送门美术、按钮、Player Trigger 与 ActorDestroyAnimHandle，但两个 UnityEvent
  的目标为空且类型仍为 `Level999Ctrl`，ActorDestroy 的 target 也为空，运行时不能执行跳转。
- `IWannerPlayCtrl.Deactivate()` 已负责结束当前 TBN 尝试、清理关卡/HUD 和停止玩法音乐；
  `GameManager.ReturnToStartPanel()` 只适用于返回主界面，不满足本需求。

## 3. 修改后行为与调用链

1. `ILevel999Ctrl.Start()` 启动两秒入口门禁。
2. 进入 Trigger 后锁定 HUD 输入，Actor 在两秒内移动、缩放并旋转进入传送门；1.2 秒处按设置触发成功震动。
3. 表现结束或按钮直接调用 `EnterMainLevel()`，实例级一次性锁阻止重复请求。
4. `GameManager.TryEnterMainLevel()` 通过 `PlayControllerManager` 停用 IWanner 并激活主线。
5. `PlayCtrl.TryEnterLowestUnpassedOrLastLevel()` 通过 `LevelCtrl` 扫描最终主线排序，排除已通关和配置缺失项，
   选出最小 `LevelSort.Id`；全部通关时选择 `10999 / Level_999`，再派发现有 `EnterLevelEvent`，继续走主线加载与 `level_start`。
6. 主线进关请求被接受后上报 `enter_tbc_portal(type=2)`；失败分支不上报。

## 4. 数据、资源、UI、事件与平台边界

- DB/存档：不新增字段，不改变 IWanner Group/TBN 进度或主线解锁/通关集合；进关仍由既有 `OnGameEnter()` 将所选 Id
  写入 `DB.GameData.CurrentLevel`。
- 资源/配置：不新增资源地址、Luban 或 RemoteConfig；保留用户对 ILevel_999 场景、相机、地面与传送门布局的现有改动。
- UI/事件：复用 `GamePlayPanelSwitchBlockEvent` 与 `EnterLevelEvent`，不新增全局事件或修改共享 GameplayPanel。
- Analytics：IWanner 停用仍按权威生命周期形成 `level_end(exit)`，随后主线形成新 `level_start`；另发送
  `enter_tbc_portal(type=2)`。
- Android/iOS：共用运行时代码；横屏触控、震动和低帧率时序仍需真机验证。

## 5. 验证、风险与未验证缺口

- 定向 C# 构建通过：`0 error / 109 warning`，warning 为工程既有范围。
- 当前本地主线排序静态检查为 91 行、Id 严格递增且无重复；运行时查询仍按最终排序逐项比较 Id，不依赖列表预先有序。
- 静态确认新脚本与 meta GUID、Prefab 根组件、按钮/Trigger 回调、ActorDestroy target/endTarget 均可解析；本地
  YAML anchor 无重复、非零本地引用无缺失。
- 高风险：必须在 PlayMode 或 SDK 后台确认 IWanner `level_end(exit)` 先于主线 `level_start`，且 TBN 退出不会写入普通
  Group 进度。
- `uncertain`：两秒门禁、Actor_IWanner 缩放/销毁、碰撞与点击竞争、主线资源加载失败回退、连续重进、
  GuruSDK 收数及 Android/iOS 横屏尚未验证。

## 6. 回滚路径

1. 从 ILevel Prefab 移除 `ILevel999Ctrl`，恢复两个 UnityEvent 和 ActorDestroy target 为空。
2. 删除 `ILevel999Ctrl.cs/.meta` 与本记录。
3. 若 DLevel 入口也回滚且共享 API 无调用者，再移除 `GameManager.TryEnterMainLevel()`、
   `PlayCtrl.TryEnterLowestUnpassedOrLastLevel()` 与 `LevelCtrl.TryGetLowestUnpassedOrLastLevel()`。
4. 无需迁移存档、配置、资源包或 SDK 数据。

## 7. 2026-08-21：最小未通关关卡与全通后 Level_999 兜底

### 7.1 需求与修改后流程

- 本节覆盖第 1、3、5、6 节中“要求候选已解锁、无候选时失败”的旧目标选择语义。
- `ILevel999Ctrl` 仍在传送门表现结束后调用 `GameManager.TryEnterMainLevel()`；主线激活后改由
  `PlayCtrl.TryEnterLowestUnpassedOrLastLevel()` 请求目标。
- `LevelCtrl.TryGetLowestUnpassedOrLastLevel()` 在当前最终主线排序中选择配置有效且不在
  `DB.GameData.PassedLevels` 的最小 `LevelSort.Id`，不再检查 `UnlockLevels`。所有有效排序项都已通关时，
  固定进入 `10999 / Level_999`；只有排序、通关存档或该兜底配置无效时才恢复 StartPanel。
- ILevel999 的两秒门禁、Actor 吸入、震动、HUD 输入遮罩、TBN 清理和
  `enter_tbc_portal(type=2)` 未改；本次没有修改 `ILevel_999.prefab`、IWanner Group 进度、资源或事件。

### 7.2 风险、验证与回滚

- 本入口现在允许进入未通关但未记录于 `UnlockLevels` 的主线空洞关；该行为是本次需求的直接结果，
  不会反向补写主线解锁或通关集合。
- IWanner `level_end(exit)` 与主线 `level_start` 的既有顺序不变；全通后进入已通关 `Level_999` 时，
  `start_type` 仍按权威生命周期约定判定为 `replay`。
- 静态配置矩阵确认无通关记录进入 `Level_001`、前五关通关进入 `Level_006`、107 个最终排序项全部通关进入
  `107 / 10999 / Level_999`；CodeGraph 未发现新调用者或覆盖测试。
- `Assembly-CSharp` 构建通过，结果为 `0 error / 106 warning`。Unity PlayMode、全通/空洞存档、远端排序、
  IWanner 退出打点顺序、GuruSDK 后台和 Android/iOS 横屏仍为 `uncertain`。
- 回滚时恢复 `TryEnterLowestUnlockedUnpassedLevel()`、`TryGetLowestUnlockedUnpassedLevel()`、解锁过滤及无候选失败语义；
  ILevel999 脚本绑定、Prefab、存档和配置无需迁移。

## 8. 2026-08-21：主线入口开放延迟改为 Inspector 参数

### 8.1 需求与修改后行为

- 修改前，`ILevel999Ctrl` 通过私有常量固定等待 `2f`，Inspector 无法调整；门禁开放前的入口调用直接忽略。
- 修改后，`[SerializeField, Min(0f)] mainEntryEnableDelay` 提供独立参数，默认 `2f`，设为 `0` 时立即开放；运行时使用
  `Mathf.Max(0f, value)` 保护异常负值。
- Trigger 固定两秒演出结束时若门禁尚未开放，会等待门禁剩余时间后再进入主线，关卡销毁会取消该等待。
- 本次不修改 ILevel999 的固定两秒 Actor 演出、主线目标解析、IWanner 退出清理、HUD 遮罩、存档、资源、事件或
  `enter_tbc_portal(type=2)`。

### 8.2 影响、验证、风险与回滚

- 影响文件仅为 `ILevel999Ctrl.cs` 与本记录；没有手工写入 `ILevel_999.prefab`，无显式覆盖时使用脚本默认值 `2f`。
- 静态确认固定常量已移除且等待只读取序列化字段；
  `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 106 warning`，warning 为工程既有范围。
- `uncertain`：Unity PlayMode 的 `0/2` 秒、首次点击临界帧、较大配置下 Trigger 等待期间的 HUD/Actor 表现、
  Reset/Replay 与 Android/iOS 横屏尚未验证。
- 较大延迟会让 Actor 演出先结束、HUD 继续锁定直到门禁开放。回滚时恢复 `MainEntryEnableDelay = 2f` 常量、删除序列化
  字段及 Trigger 剩余等待。

## 9. 2026-08-21：ILevel_999 隐藏 SkipButton

### 9.1 需求、基线与修改后行为

- 需求：`ILevel_999` 运行期间不显示 IWanner HUD 的 `SkipButton`。
- 修改前，TBN 关与普通小关共用失败次数阈值判断；ILevel_999 失败或 Reset 达到远端阈值后，重建 HUD 时会显示 Skip，
  但 `TrySkipCurrentLevel()` 又因 TBN 没有 `_currentGroup` 与 `_currentLevelData` 而拒绝请求。
- 修改后，`IWannerPlayCtrl.ShouldShowSkip()` 先排除 `_isTbnLevel`；ILevel_999 首次进入及失败/Reset 重载时均向 HUD 传入
  `SetSkipVisible(false)`，普通小关仍按原远端失败阈值显示 Skip。

### 9.2 影响范围与系统边界

- 代码影响：`IWannerPlayCtrl.cs` 的 Skip 可见性判断，以及 `IWannerGame_GamePlayPanel.cs` 中与新事实冲突的接口注释。
- Prefab/资源：不修改 `ILevel_999.prefab` 或共享 HUD prefab，不新增资源、配置、存档、事件、打点或平台分支。
- UI/输入：仅改变 TBN 的 Skip 可见性；移动、跳跃、Reset、Pause、全屏死亡重置区和传送门流程保持原逻辑。

### 9.3 验证、风险与回滚

- 静态矩阵确认 TBN 分支始终返回 false，普通小关达到/未达到阈值的结果保持原逻辑；定向 `git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 106 warning`，warning 为工程既有范围。
- `uncertain`：Unity PlayMode 中首次进入、连续失败/Reset 后的 HUD 实际显隐，以及 Android/iOS 横屏触控尚未验证。
- 回滚时移除 `ShouldShowSkip()` 的 `_isTbnLevel` 排除，并恢复 HUD 接口注释；无需迁移 Prefab、配置或存档。
