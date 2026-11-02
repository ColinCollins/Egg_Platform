# ChoiceLevelPanel 返回与 NoAd 显示功能记录

## 需求与背景

- 日期：2026-07-30。
- `ChoiceLevelPanel` 新增返回按钮，点击后需要稳定回到应用启动后的 `StartPanel`。
- `NoAdBtn` 的 prefab 层级已调整，按钮组件当前绑定在 `noads/clickArea`；隐藏时需要隐藏父节点 `noads`，避免父节点下的整套美术继续显示。

## 影响范围

- 运行时代码：`Assets/Game/UI/LevelChoicePanel/Scripts/ChoiceLevelPanel.cs`。
- 现有绑定：`Assets/Game/UI/LevelChoicePanel/Scripts/ChoiceLevelPanel.Generated.cs` 中的 `BackBtn`。
- 现有资源：`Assets/Game/UI/LevelChoicePanel/Resources/ChoiceLevelPanel.prefab` 中的 `BackBtn`、`NoAdBtn` 及 `noads/clickArea` 层级。
- 复用系统：Bear UI 按钮回调、`GameManager.ReturnToStartPanel(...)`、现有 NoAds 道具数量查询。
- 未新增 DB 字段、存档键、配置、资源地址、全局事件、analytics 或平台分支。

## 修改前基线

- `BackBtn` 已由 prefab 绑定，但 `ChoiceLevelPanel.InitButtons()` 没有注册返回回调。
- `RefreshBtn()` 直接切换 `NoAdBtn.gameObject`。由于 `NoAdBtn` 已变为 `clickArea` 子节点，隐藏它不能同时隐藏父节点下的 NoAd 美术。
- `ChoiceLevelPanel` 除了从 `StartPanel` 打开，也会作为关卡加载失败或末关流程的回退界面打开；这些入口不保证 UI 栈下方存在可恢复的 `StartPanel`。

## 修改后行为

- `BackBtn` 点击后调用 `GameManager.ReturnToStartPanel(...)`，由现有控制器切换链清理 Normal/Popup UI、恢复主线运行时状态并重新打开 `StartPanel`。
- 返回失败时使用现有 Bear Logger 输出错误，不静默吞掉失败。
- NoAds 道具数量大于零时隐藏 `NoAdBtn.transform.parent.gameObject`；没有 NoAds 道具时重新显示同一父节点。

## UI、数据与平台流

- 返回流：`BackBtn` → `ChoiceLevelPanel.ReturnToStartPanel` → `GameManager.ReturnToStartPanel` → `PlayCtrl.TryRestoreStartPanel` → `StartPanel.Create`。
- NoAd 显示流：面板打开、NoAds 道具更新或恢复购买成功 → `RefreshBtn()` → 查询 Bag 中 NoAds 数量 → 切换 `noads` 父节点。
- 本次没有改动广告购买、恢复购买、Bag 写入或存档行为。
- 逻辑不包含分平台代码；Android/iOS 横屏共用同一 UI 流。

## 验证

- 已核对 prefab YAML：`NoAdBtn` 绑定 `clickArea`，其父节点为 `noads`；`BackBtn` 已绑定。
- 已通过 CodeGraph 与代码检查确认 `ChoiceLevelPanel` 存在多种非 `StartPanel` 入口，返回不能只依赖关闭当前 UI 后的栈恢复。
- 已执行 C# 项目编译与 `git diff --check`；具体结果以本次任务最终验证输出为准。
- Unity PlayMode、Android/iOS 横屏触控与真实恢复购买后的显示刷新尚未验证。

## 风险与回滚

- `NoAdBtn` 必须继续保持在需要整体显示/隐藏的 `noads` 根节点下一层；若后续再次调整层级，应改为显式序列化根节点，避免依赖父子关系。
- `GameManager.ReturnToStartPanel(...)` 会按既有设计重置主线运行时状态，这符合“返回 StartPanel”目标；若产品只希望恢复 UI 栈而不重置状态，需要另行确认并改为局部关闭。
- 回滚时移除 `BackBtn` 的回调注册与 `ReturnToStartPanel` 方法，并把 NoAd 可见性切换恢复为 `NoAdBtn.gameObject.SetActive(...)`。

## 2026-08-04：主线 Level999 选关路由修复

### 需求与原行为基线

- 用户确认目标流程为：点击主线 `Level999` 选关项先进入 `Level_999`，之后只有点击关内
  `enterDrawGame` 才进入 DrawGame。
- 修复前 `LevelChoiceItem.EnterLevel(...)` 将 `LevelSort.Path == "10999"` 硬编码为
  `GameManager.TryEnterDraw(...)`，导致玩家跳过 `Level_999` 并直接看到 Draw 选关 Popup。
- `Assets/Game/Configs/tbleveldata.json` 已存在 `10999 -> Level_999`；该主线资源映射无需修改。

### 修复后行为与边界

```text
主线 Level999 选关项
  -> ChoiceLevelPanel.EnterLevel
  -> 主线既有进关流程
  -> 加载 Level_999
  -> 玩家点击关内 enterDrawGame
  -> Level999Ctrl.EnterDrawGameAtHighestPlayableLevel
  -> 进入当前可玩的最大 Draw 关卡
```

- 删除 `LevelChoiceItem` 对 `10999` 的 Draw 路由特判，所有主线选关项重新统一交给
  `_owner.EnterLevel(_levelSort)`。
- `Level_999.prefab` 的 `enterDrawGame` UnityEvent 仍绑定
  `Level999Ctrl.EnterDrawGameAtHighestPlayableLevel()`，没有修改 prefab、配置、存档、资源地址、
  全局事件、analytics、广告或平台分支。
- 同步修正 `GameManager.TryEnterDraw(...)` 的调用边界注释；旧注释仍声称主线 TBN 选关项会直接
  进入 Draw，已与当前代码和本次确认需求冲突，因此必须更新。

### 验证、风险与回滚

- 静态扫描确认 `LevelChoiceItem.cs` 已不存在 `DrawEntryLevelPath`、`Path == "10999"` 或
  `TryEnterDraw(...)`；`Level_999.prefab` 的关内 UnityEvent 绑定仍存在。
- `git diff --check` 对目标文件通过，仅有工作区行尾转换提示。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1
  /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly` 通过：`0 warning / 0 error`。
- `uncertain`：尚未执行 Unity PlayMode 的“主线选关进入 Level_999 → 点击关内入口进入 Draw”完整点击链，
  Android/iOS 横屏触控也未验证。
- 回滚时恢复 `LevelChoiceItem` 的 `DrawEntryLevelPath` 特判、Bear Logger 依赖和 `IDebuger` 标记；
  不涉及存档或资源迁移。
