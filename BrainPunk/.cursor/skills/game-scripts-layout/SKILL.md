---
name: game-scripts-layout
description: >-
  Maps Assets/Game C# layout: Scripts (Level, Play, SDK, Common), UI folders with
  Scripts/Global, Editor, DB, Temp. Use when placing new code, refactoring paths,
  or when the user asks to align with project structure; update this file when
  they ask to refresh the layout skill.
---

# BrainPunk `Assets/Game` 脚本结构（结构图）

> **维护**：目录有大调整、或用户说「更新 scripts 结构 / 更新 game-scripts-layout」时，重新浏览 `Assets/Game/Scripts`、`Assets/Game/UI` 一级子目录，并同步修改本文件（必要时补充 `reference.md` 存更细的树）。

---

## 顶层一览

| 路径 | 用途 |
|------|------|
| `Assets/Game/Scripts/` | 运行时：关卡、玩法状态机、配置、SDK 服务封装、通用工具（非 UI） |
| `Assets/Game/UI/` | 按功能分文件夹；业务脚本多在 `<Feature>/Scripts/` |
| `Assets/Game/UI/Global/Scripts/` | **跨界面复用**（全局弹层、动效、加载表现等） |
| `Assets/Game/Editor/` | **仅编辑器**：UIGenerator、Luban、图集、字体工具等 |
| `Assets/Game/DB/` | 存档 / `GameData` 等（常与 SaveModule 配合） |
| `Assets/Game/Temp/` | **临时/试验**（如按关卡号的临时逻辑）；稳定后迁入 `LevelItem` 等正式路径 |
| `Assets/Game/Scripts/HotReload/` | 热更相关 |

Bear 嵌入式包在 `Assets/BearGame/`，不在此表；选型见 **`beargame-packages`**。

---

## `Assets/Game/Scripts/` 子目录职责

| 子目录 | 内容 |
|--------|------|
| `Common/` | 通用：`Buttons/`（含 `CustomButton`）、`ObjectPool/`、`ResolutionFit/`、`Singleton/`、`DragEventsListener.cs`、`EmailUtils`、`SequentialScaleAnim`、`AudioManager`、`OnTrigger2DHandle` 等 |
| `Config/` | `ConfigManager` 分部、远程表等 |
| `Level/` | `BaseLevelCtrl.cs`、`Actor/`、`ActorSpineCtrl.cs`、`LevelItem/Level_XX/`（**按关卡号**分子目录，命名多为 `Level{N}Ctrl` / 关卡专属脚本） |
| `Play/` | `PlayCtrl`、`LevelCtrl`、`LevelRuntimeState`、`PlayEvents`、`PlayCtrl_Nodes/`（FSM 状态节点） |
| `Runtime/` | 如 `Ads/`（激励视频等运行时策略） |
| `SDK/` | `Services/`（`Purchase`、`Events`、`Remote` 等）、`SDKImps/`（项目对接实现） |

---

## `Assets/Game/UI/` 组织方式

- 典型形态： **`UI/<面板名>/Scripts/`** 下放 `XxxPanel.cs` / `XxxPopup.cs`，与 Prefab、资源同层级并列。
- **生成代码**：许多视图为 `partial class`，配套 **`*Generated.cs`**（由 UIGenerator 生成 **SerializeField**，**不要手改 Generated**）。
- **示例路径**（非穷举，便于定位风格）：
  - `UI/GamePanel/Scripts/` — 内玩 HUD（如 `GamePlayPanel`）
  - `UI/SettingPopup/Scripts/` — 设置（如 `GameSettingPopup`、`LocalizationPopup`、`CustomButtonOpenUrl`）
  - `UI/Global/Scripts/` — **`WaitingPopup`、`UIFadeAnimation`、`MoveAnimation`、`EnterLevelLoading`、`SystemTips`** 等
  - `UI/GM/Scripts/` — GM、`GMPasswordPopup` 等
  - `UI/Loading/Scripts/` — `LoadManager` 等
  - `UI/ShopPanel`、`UI/LevelChoicePanel`、`UI/GameVictory`、`UI/GameTipsPopup`、`UI/RatingPopup`、`UI/NoAds`…

---

## 惯例（和结构强相关）

1. **UI 栈与生命周期**：`Bear.UI` 的 `UIManager`、`BaseUIView`、`UILayer`（见 **`beargame-packages`**）。
2. **事件订阅**：`Bear.EventSystem` + 项目内 `EventsUtils.ResetEvents` / `EventSubscriber`（见 **`ui-event-listener`**）。
3. **调试日志**：业务类可实现 `Bear.Logger.IDebuger`，用 `this.Log`（见 **`debug-idebuger`**）；受 `DEBUG_MODE` 等约束。
4. **2D 关卡**：关卡玩法以 `Physics2D`、`Collider2D` 为主；关卡脚本放在对应 `LevelItem/Level_XX/`。
5. **DOTween**：多在各脚本内直接 `using DG.Tweening`；可复用的轻量封装如 `UI/Global/Scripts/UIFadeAnimation.cs`、`MoveAnimation.cs`、`Common/Others/BreathingScaleHandle.cs`。**`SequentialScaleAnim`** 为自写 Update 缩放（有意避免部分 DOScale 冲突）。

---

## 更新本 skill 的检查清单

1. `Get-ChildItem Assets/Game/Scripts -Directory`（或资源管理器）看 **一级子目录** 是否增删。
2. `Get-ChildItem Assets/Game/UI -Directory` 看 **是否新增大面板目录**。
3. `UI/Global/Scripts` 是否新增「全项目共用」组件。
4. 修改上表与示例路径；若文件数暴增，可新建 **`reference.md`** 只列目录树，本 `SKILL.md` 保持精简。

---

## 相关 Skills

| Skill | 用途 |
|--------|------|
| `beargame-packages` | Bear UI / EventSystem / Logger / Save / FSM 选型 |
| `ui-event-listener` | UI/玩法事件订阅与清理 |
| `debug-idebuger` | IDebuger / Bear 日志习惯 |
| `guru-products-spec` | `guru_spec.yaml` IAP 目录 |
