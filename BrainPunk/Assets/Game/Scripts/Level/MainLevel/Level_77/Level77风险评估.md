# Level77 风险评估

更新时间：2026-06-23

## 原始行为基线

- `Level77Ctrl.cs` 原有需求注释要求在拖拽 Begin 时把 `actorRigibody` 的 `bodyType` 改为 `Kinematic`，End 时改回 `Dynamic`。
- 原脚本没有实现该逻辑，`Start()` 和 `Update()` 为空。
- `Level_077.prefab` 根对象已绑定 `Level77Ctrl.actorRigidbody` 和 `Level77Ctrl.dragable`。
- `Level_077.prefab` 目标 `DragableItem` 原 `onDragStartEvent` / `onDragEndEvent` 均为空。

## 影响文件 / 资源

- `Assets/Game/Scripts/Level/LevelItem/Level_77/Level77Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_077.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_77/Level77功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_77/Level77风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

## 使用的既有系统

- `DragableItem.onDragStartEvent`：拖拽开始时通过 UnityEvent 调用 Level77 本地入口。
- `DragableItem.onDragEndEvent`：拖拽结束时通过 UnityEvent 调用 Level77 本地入口。
- Unity `Rigidbody2D.bodyType`：只在 Level77 本地 actor 上切换 `Kinematic` / `Dynamic`。

## 风险项

- P1：拖拽中断时 actor 可能残留 `Kinematic`。缓解：`OnDisable()` 运行态恢复 `Dynamic`，但仍需 PlayMode 验证重开、失败、退出和切关路径。
- P1：若设计期望 actor 初始并非 `Dynamic`，拖拽结束固定恢复 `Dynamic` 可能改变行为。当前依据来自原需求注释；PlayMode 或策划确认前标记为待验证。
- P2：若 prefab 事件误绑到其他拖拽物，可能误切 actor 物理。缓解：`OnDragBegin(Transform)` / `OnDragEnd(Transform)` 校验事件来源必须是绑定 `dragable` 或其子节点。
- P2：移动端触摸取消、暂停、切后台等路径是否必定触发 DragableItem End 需要真机验证。当前仅通过 `OnDisable()` 覆盖关卡对象禁用场景。

## 静态验证证据

- `Level_077.prefab` 中 `Level77Ctrl.actorRigidbody` 指向 `{fileID: 1138963634036635986}`。
- `Level_077.prefab` 中 `Level77Ctrl.dragable` 指向 `{fileID: 1268057685807008906}`。
- `Level_077.prefab` 中该 `DragableItem.onDragStartEvent` 已绑定 `Level77Ctrl.OnDragBegin`。
- `Level_077.prefab` 中该 `DragableItem.onDragEndEvent` 已绑定 `Level77Ctrl.OnDragEnd`。
- `Level77Ctrl.cs` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`PlayerPrefs`、`Resources`、`StreamingAssets`、`YooAsset`、`Instantiate`、Unity `Debug.Log`、analytics、广告、内购或平台 SDK 调用。

## 编译验证

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已执行通过。
- 最近一次复跑结果：0 error / 96 warnings。
- warnings 属于当前工程既有范围，未见 `Level77Ctrl.cs` 新增编译错误。
- `git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_77 Assets\Game\Prefabs\Level\Level_077.prefab DevelopMD\已有功能设计与使用记录.md` 仍报告 `Level_077.prefab` 中 Unity YAML 空值字段 `m_Name:` / `m_EditorClassIdentifier:` trailing whitespace；新增事件参数行已清理，脚本与文档未报告 whitespace error。

## 未验证缺口

- 未运行 Unity PlayMode，尚未验证 Level77 首次进入、拖拽开始、拖拽结束、失败、重开、退出后再进入。
- 未验证 actor 在 Level77 prefab 中的设计初始 `bodyType` 是否一定应为 `Dynamic`。
- 未验证 Android / iOS 横屏触控下拖拽开始、拖拽结束和触摸取消路径。
- 未做长时间运行或性能采样；本次逻辑无 per-frame 处理，预期性能风险低，但仍需实际关卡冒烟确认。

## 手工验证建议

- 进入 Level77 后先不拖拽，确认 actor 原始物理表现与当前设计一致。
- 按住目标拖拽物，确认 actor 切为非 Dynamic 物理表现。
- 松开目标拖拽物，确认 actor 恢复 Dynamic。
- 拖拽中点击重开、失败、退出或切换关卡，再次进入确认 actor 未残留 Kinematic。
- 快速重复拖拽和短距离点击，确认事件不会重复导致状态卡死。
- Android / iOS 横屏设备或模拟器各做一次触摸拖拽冒烟。

## 回滚路径

- 回滚 `Level77Ctrl.cs` 至只有原始需求注释和空生命周期方法的版本。
- 回滚 `Level_077.prefab` 中目标 `DragableItem` 的 `onDragStartEvent` 和 `onDragEndEvent` 绑定。
- 删除或回滚 `Level77功能说明.md`、本风险评估和 `DevelopMD/已有功能设计与使用记录.md` 的 Level77 追加记录。
