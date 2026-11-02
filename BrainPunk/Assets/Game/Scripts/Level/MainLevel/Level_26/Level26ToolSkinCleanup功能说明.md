# Level26 生成 Tool 皮肤清理功能说明

## 需求背景

2026-06-23 需求：`Level26Ctrl` 参考 `Level64AICtrl`，移除点击门后生成的 tool 从玩家 Actor 克隆过来的皮肤表现。

## 受影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_26/Level26Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_26/Level26EnemyCtrl.cs`
- `Assets/Game/Prefabs/Level/Level_026.prefab`
- `Assets/Game/Prefabs/LevelItems/Actor/Actor.prefab`
- 现有系统：`ActorCtrl`、`ActorSpineCtrl`、`ActorSkinBinding`、`BoneFollowerAutoBinder`、Spine `BoneFollower`

本次未修改 prefab、Luban 配置、存档、资源打包、热更、广告、内购、analytics 或平台 SDK。

## 修改前行为基线

- `Level26Ctrl.ClickStartDoor()` 调用 `Instantiate(actor, actor.transform.parent)` 直接克隆当前关卡 Actor。
- 生成 tool 会保持 dash、向右移动、缩放到 `0.7`，并设置 tag 为 `tools`、layer 为 `TriggerItem`。
- 因为克隆源是当前 Actor，若玩家 Actor 已经通过 `ActorSkinBinding` 应用了 full-body 或 `back` / `face` / `body` SkinPart，生成出来的 tool 可能继承这些皮肤表现。
- `Level26EnemyCtrl` 仍通过 tag `tools` 识别生成 tool，被撞后调用 `ActorCtrl.Die()`、`Knockback(...)`，并在 1 秒后销毁 tool。

## 修改后行为

- `ClickStartDoor()` 生成 tool 后立即调用 `RemoveGeneratedToolSkins(tool)`。
- 清理范围只作用于本次生成的 tool 克隆体：
  - 禁用克隆体子层级中的 `ActorSkinBinding`，避免后续玩家 Skin 变更事件继续刷新到 tool。
  - 清理带 `BoneFollowerAutoBinder` 或 `BoneFollower` 的 `back` / `face` / `body` 挂点子物体，不删除挂点自身。
  - 将克隆体 full-body Spine skin 重置为 `box_original`，避免继承玩家当前 full-body Skin。
- 生成、移动、碰撞、受击、敌人扣血、tool 销毁和开关门节奏保持原有路径。

## 数据、资源与事件流

- 数据：不读写 `DB.GameData`，不修改 `OwnedSkin` 或当前皮肤保存字段。
- 资源：不新增资源加载；只清理克隆体已有子对象并调用现有 `ActorSpineCtrl.SetSkin("box_original")`。
- 事件：禁用克隆体 `ActorSkinBinding` 后，tool 不再订阅或响应 `ActorSkinSelectionChangedEvent`。
- UI/平台：不涉及 UI、广告、内购、analytics、RemoteConfig、热更或 Android/iOS 平台 SDK。

## 验证记录

- 静态检查确认 `Level26Ctrl.cs` 新增路径只命中预期的 `Instantiate`、`ActorSkinBinding` 禁用、`SetSkin` 和挂点清理。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_26/Level26Ctrl.cs` 已通过，仅提示 Git 工作区下次接触该文件会做 LF/CRLF 转换。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 1114 warnings；warnings 属于当前工程既有范围。
- `Actor.prefab` 文本证据显示存在 `ActorSkinBinding`，且绑定了 `backMount`、`faceMount`、`bodyMount`。
- `Actor.prefab` 文本证据显示 `back` / `body` 等挂点带 `BoneFollowerAutoBinder` 和 Spine `BoneFollower`；本次代码复用该边界清理挂点子对象。

## 未验证风险

- Unity PlayMode 未执行；仍需验证 Level26 点击门生成 tool 后不继承玩家当前 full-body、back、face、body 皮肤。
- 仍需验证 tool 进入敌人触发区后 enemy 扣血、tool knockback、1 秒后销毁和敌人最终销毁流程不回归。
- Android/iOS 横屏触摸、快速连续点击门、快速重开/退出后的 tool 残留仍待设备或 PlayMode 补验。
- 静态搜索发现 `Level_054.prefab`、`Level_060.prefab` 也存在指向 `Level26Ctrl.ClickStartDoor` 的 UnityEvent 字符串；是否为实际运行引用待 Unity prefab/关卡入口确认。

## 回滚路径

回滚 `Level26Ctrl.cs` 中 `RemoveGeneratedToolSkins(tool)` 调用、`DefaultToolSkinName` 常量、`Game.Level.Actor` / `Spine.Unity` using，以及 `生成 Tool 皮肤清理` region 内所有 helper 方法，即可恢复到生成 tool 完整继承源 Actor 外观的旧行为。
