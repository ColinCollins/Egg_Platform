# Level26 生成 Tool 皮肤清理风险评估

## 原始行为基线

- `ClickStartDoor()` 原先在 `canOpen == true` 时播放开门流程，并克隆 `actor` 生成一个向右移动的 tool。
- 生成 tool 的旧路径会保留 `SwitchDash(true)`、`SetMoveInput(1)`、从 `startDoor` 的 x 坐标出生、缩放 `0.7`、tag `tools`、layer `TriggerItem`。
- `Level26EnemyCtrl.OnTriggerEnter2D(...)` 通过 tag `tools` 或 `Player` 进入 knock 路径；tool 被撞时会死亡、击退、旋转，并在 1 秒后销毁。
- 修改前生成 tool 直接继承源 Actor 克隆体上的 `ActorSkinBinding`、当前 full-body skin 和已挂载的 `back` / `face` / `body` SkinPart 子对象。

## 受影响文件与资源

- `Assets/Game/Scripts/Level/LevelItem/Level_26/Level26Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_26/Level26EnemyCtrl.cs`
- `Assets/Game/Prefabs/Level/Level_026.prefab`
- `Assets/Game/Prefabs/LevelItems/Actor/Actor.prefab`
- 本地文档：`Level26ToolSkinCleanup功能说明.md`、`Level26ToolSkinCleanup风险评估.md`

## 已使用的现有系统

- `ActorCtrl`：继续作为生成 tool 的组件类型，并继续提供 `SwitchDash(...)`、`SetMoveInput(...)`、`OnUpdate()`、`SpineCtrl`。
- `ActorSpineCtrl.SetSkin(...)`：用于把生成 tool 的 full-body skin 重置为 `box_original`。
- `ActorSkinBinding`：生成 tool 后禁用克隆体上的该组件，避免后续玩家皮肤事件刷新到 tool。
- `BoneFollowerAutoBinder` / Spine `BoneFollower`：用于识别 `back`、`face`、`body` SkinPart 挂点，只删除挂点下子对象，不删除挂点自身。
- `Level26EnemyCtrl`：保持原有 tools tag 碰撞与销毁逻辑。

## 风险项

- P1：如果生成 tool 的 full-body skin 不重置，玩家当前 full-body 皮肤仍可能被克隆到 tool。处理：新增 `ResetGeneratedToolFullBodySkin(...)` 调用 `SetSkin("box_original")`。
- P1：如果只按名称递归删除 `body`，可能误删 Actor 的普通视觉根。处理：清理只接受同时带 `BoneFollowerAutoBinder` 或 `BoneFollower` 的挂点。
- P1：`ActorSkinBinding` 在 Instantiate 后可能已经经历 `OnEnable` 并启动异步部件加载。处理：立即禁用克隆体上的 `ActorSkinBinding`，依赖其 `OnDisable()` 增加版本并清理已创建部件；后续仍需 PlayMode 确认异步回写不会造成可见残留。
- P2：`box_original` 是否存在于当前 Actor Spine 数据，需要运行时验证；若缺失，`ActorSpineCtrl.SetSkin(...)` 会记录 warning，tool 可能保留当前 Skeleton 状态。
- OBS：`Level_054.prefab`、`Level_060.prefab` 中也存在 `Level26Ctrl.ClickStartDoor` UnityEvent 字符串，是否会实际运行到本脚本待关卡入口确认。

## 验证证据

- 静态代码：`Level26Ctrl.ClickStartDoor()` 在设置 tool tag/layer 后立即调用 `RemoveGeneratedToolSkins(tool)`。
- 静态代码：`RemoveGeneratedToolSkins(...)` 只禁用克隆体 `ActorSkinBinding`、清理 SkinPart 挂点子对象、重置 full-body skin，没有修改移动、碰撞、敌人或门动画逻辑。
- 静态扫描：`Level26Ctrl.cs` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`PlayerPrefs`、analytics、广告或 IAP 链路。
- 静态 prefab 证据：`Actor.prefab` 上存在 `ActorSkinBinding`，且绑定 `backMount`、`faceMount`、`bodyMount`。
- 静态 prefab 证据：`Actor.prefab` 的 SkinPart 挂点带 `BoneFollowerAutoBinder` / `BoneFollower`，支持按组件边界清理。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_26/Level26Ctrl.cs` 已通过，仅提示 LF/CRLF 转换。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 1114 warnings；warnings 属于当前工程既有范围。

## 未验证缺口

- 未执行 Unity Editor PlayMode，未确认生成 tool 实际不显示玩家已保存或试戴过的 SkinPart / full-body 皮肤。
- 未验证 Level26 敌人碰撞、扣血、tool 销毁、敌人销毁、开关门节奏和反复点击门。
- 未验证 Android/iOS 横屏触摸、快速点击门、重开、失败、胜利和退出清理。
- 未运行完整 Unity 构建；移动端资源裁剪和 Spine skin 名称仍需实际工程验证。

## 回滚路径

回滚 `Level26Ctrl.cs` 中 `RemoveGeneratedToolSkins(tool)` 调用、`DefaultToolSkinName` 常量、`Game.Level.Actor` / `Spine.Unity` using，以及 `生成 Tool 皮肤清理` region 内 helper 方法；同时删除本功能说明、风险评估和 `DevelopMD/已有功能设计与使用记录.md` 中对应追加记录。
