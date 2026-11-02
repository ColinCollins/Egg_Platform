---
name: beargame-packages
description: >-
  Maps embedded packages under `Assets/BearGame` (Bear UI, EventSystem, FSM,
  SaveModule, Logger) to when to use which API. Use when editing or adding code
  that touches Bear.* namespaces, UI stack/layers, saves, game state machines,
  in-game events, or logging macros; or when choosing between Bear modules vs
  project Game code.
---

# BearGame 嵌入包 — 选型与职责

`Assets/BearGame/` 下是多个 **嵌入式 UPM 包**（文件夹名带 hash）。改 Bear 层逻辑、接入新系统、或排查命名空间时，先对照本表选对模块，避免用错层。

## 五包一览

| 包目录前缀 | 主要命名空间 | 一句话职责 |
|------------|--------------|------------|
| `com.bear.ui` | `Bear.UI`，Editor：`Game.Editor` / `Game.Editor.UIGenerator` | UI 栈、层级、遮罩、加载、视图基类、可选 ViewModel；编辑器生成 Prefab |
| `com.bear.package.eventsystem` | `Bear.EventSystem` | 类型化事件派发 / 订阅（`EventDispatcher`、`EventSubscriber`） |
| `com.bear.package.statemachine` | `Bear.Fsm` | 状态机：`StateMachine`、`StateNode` + `[StateMachineNode]` |
| `com.bear.savemodule` | `Bear.SaveModule`（Editor：`Bear.SaveModule.Editor`） | 存档：`SaveManager`、PlayerPrefs/JSON 等 Provider、`DBManager` |
| `com.bear.logger` | `Bear.Logger` | 条件编译日志：`BearLogger`（如 `DEBUG_MODE`）、`DebugSetting` |

详细 API 以包内源码与 `com.bear.ui/Readme.md` 为准。

---

## 按任务选模块（判断流程）

1. **要做全屏 / 弹窗 / 打开关闭界面、UILayer、遮幕、Resources 路径加载 UI**  
   → **`Bear.UI`**：`UIManager`、`UIStack`、`BaseUIView`、`UILayer` / `UILayerManager`、`UIMask`、`IUILoader` 实现（如 `ResourcesUILoader`）、`IUIAnimation` / `UIScaleAnimation`。

2. **要在 Prefab 上自动绑子节点引用、或写继承自基类的 UI 脚本**  
   → **`BaseAutoUIBind`**（全局命名空间，无 `Bear.UI`）：仅 Mono + `Init()` + 编辑器扩展绑定；若还要走栈与生命周期，视图侧用 **`BaseUIView`**。

3. **从设计稿 JSON/PSD 导出生成带 Canvas 的 UI Prefab**  
   → **Editor**：`Game.Editor.UIGenerator`（菜单 **Tools → UI Generator** 等），见 `com.bear.ui/Readme.md` 中 UIGenerator 章节。

4. **跨模块广播 / 监听自定义事件结构体（解耦 UI 与玩法）**  
   → **`Bear.EventSystem`**：`EventDispatcher` + `EventSubscriber`。  
   本项目若需在 MonoBehaviour 里统一注册/清理，可再对照项目 skill **`ui-event-listener`**（与 `EventsUtils.ResetEvents` 等惯例配合）。

5. **主流程 / 关卡 / 玩法在有限状态之间切换（Enter/Exit、默认状态）**  
   → **`Bear.Fsm`**：`StateMachine`、`IBearMachineOwner`，节点继承 `StateNode` 并用 `StateMachineNode` 特性注册。

6. **持久化玩家进度、多存储后端、或 SO/DB 配置驱动存档**  
   → **`Bear.SaveModule`**：`SaveManager.Instance`、各 `StorageType`、`ISaveProvider`；复杂表结构可能涉及 `DBManager`、`DBSetting` 及 **Editor** 下生成器。

7. **统一 Debug 日志开关、颜色、`DEBUG_MODE` 下才输出的日志**  
   → **`Bear.Logger`**：`BearLogger`，勿与 `UnityEngine.Debug` 用途混淆；先看包内 `Conditional` 与 `DebugSetting`。

---

## Bear.UI 常用类速查（实现弹窗/面板时）

- **`UIManager`**：打开、关闭、与加载器协作；具体扩展可能在分部类（如 `UIManager_Loader`）。
- **`BaseUIView`**：单界面生命周期与栈行为；数据侧可配 **`ViewModel`**（`IBindable`）。
- **层级**：`UILayer` 枚举 + **`UILayerManager`**；遮罩：**`UIMask`**。
- **加载**：实现 **`IUILoader`** 或沿用 **`DefaultUILoader` / `ResourcesUILoader`**。
- **动画**：实现 **`IUIAnimation`** 或使用 **`UIScaleAnimation`**。

项目业务 UI 往往继承 `BaseUIView` 或项目封装类，修改时先搜现有 Popup/Panel 的打开方式再对齐。

---

## 与其它项目约定的关系

- **事件**：Bear 提供底层 `EventDispatcher`；Game 层可能还有自己的事件类型与生命周期工具，改动订阅逻辑时两处都可能涉及。
- **存档**：若已有 `SaveManager` 初始化与 Provider 注册流程，新键应走同一模块，避免直接散落 `PlayerPrefs`。
- **Editor**：`Bear.SaveModule.Editor`、`Game.Editor.UIGenerator` 等仅在 Editor /asmdef 中引用，运行时程序集勿依赖。

---

## 路径提示

包物理路径形如：`Assets/BearGame/com.bear.<name>@<hash>/`。版本升级若 hash 变化，以 **`Assets/BearGame` 下实际文件夹** 为准；命名空间与 public API 通常稳定。
