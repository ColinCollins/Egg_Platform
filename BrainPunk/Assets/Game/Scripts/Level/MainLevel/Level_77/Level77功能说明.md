# Level77 功能说明

更新时间：2026-06-23

## 需求背景

Level77 需要在指定拖拽物开始拖拽时，把关卡内 actor 的 `Rigidbody2D.bodyType` 临时切换为 `Kinematic`；拖拽结束、脚本禁用或切关清理时恢复为 `Dynamic`。目标是避免拖拽过程中 actor 继续受 Dynamic 物理影响，拖拽结束后再回到正常物理结算。

## 影响范围

- 运行时代码：`Assets/Game/Scripts/Level/LevelItem/Level_77/Level77Ctrl.cs`
- 关卡 prefab：`Assets/Game/Prefabs/Level/Level_077.prefab`
- 本地文档：`Assets/Game/Scripts/Level/LevelItem/Level_77/Level77功能说明.md`
- 风险记录：`Assets/Game/Scripts/Level/LevelItem/Level_77/Level77风险评估.md`
- 复用事实记录：`DevelopMD/已有功能设计与使用记录.md`

本次不修改共享 `DragableItem`、Actor prefab、关卡基类、存档、配置、资源加载、热更、广告、内购、analytics、RemoteConfig 或平台 SDK。

## 原有行为基线

- `Level77Ctrl` 已挂在 `Level_077.prefab` 根对象上，并已序列化 `actorRigidbody` 与 `dragable` 引用。
- `Level77Ctrl` 原脚本只有需求注释和空 `Start()` / `Update()`，没有运行时拖拽处理逻辑。
- `Level_077.prefab` 中目标 `DragableItem` 的 `onDragStartEvent` 与 `onDragEndEvent` 原本为空，不会调用 Level77 逻辑。

## 当前行为

- `DragableItem.onDragStartEvent` 调用 `Level77Ctrl.OnDragBegin(Transform)`。
- `OnDragBegin` 只接受当前绑定 `dragable` 或其子节点发出的事件，随后把 `actorRigidbody.bodyType` 设置为 `RigidbodyType2D.Kinematic`。
- `DragableItem.onDragEndEvent` 调用 `Level77Ctrl.OnDragEnd(Transform)`。
- `OnDragEnd` 只接受当前绑定 `dragable` 或其子节点发出的事件，随后把 `actorRigidbody.bodyType` 设置为 `RigidbodyType2D.Dynamic`。
- `OnDisable()` 在运行态会把 actor 恢复为 `Dynamic`，降低拖拽中退出、切关或禁用脚本后物理状态残留的风险。
- 脚本保留 `OnDragStart` / `OnDragEnd` 的 Transform 和无参兼容入口，便于 Inspector 或其他局部脚本按现有 DragableItem 命名调用。

## 事件与数据流

- 输入：玩家在 Level77 中拖拽绑定的 `DragableItem`。
- 事件：`DragableItem` 运行时触发 `onDragStartEvent` / `onDragEndEvent`，传入自身 `Transform`。
- 处理：`Level77Ctrl` 校验事件来源后，只修改已绑定 actor 的 `Rigidbody2D.bodyType`。
- 数据：不读写 `DB`、`Save`、`PlayerPrefs`、配置表或远端配置。
- 资源：不加载、不实例化、不销毁资源。
- UI / 打点 / 平台：不触碰 UI、analytics、广告、内购或平台 SDK。

## 验证记录

- 静态 prefab 检查：`Level_077.prefab` 存在 `Level77Ctrl.actorRigidbody` 与 `Level77Ctrl.dragable` 引用；目标 `DragableItem` 已绑定 `OnDragBegin` 和 `OnDragEnd`。
- 静态代码检查：`Level77Ctrl.cs` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`Resources`、`Instantiate`、analytics、广告、内购或平台 SDK 调用。
- 编译验证：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过；最近一次复跑结果为 0 error / 96 warnings，warnings 属于当前工程既有范围，未见 Level77 新增编译错误。
- 格式检查：`git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_77 Assets\Game\Prefabs\Level\Level_077.prefab DevelopMD\已有功能设计与使用记录.md` 仍报告 `Level_077.prefab` 中 Unity YAML 空值字段 `m_Name:` / `m_EditorClassIdentifier:` trailing whitespace；新增事件参数行已清理，脚本与文档未报告 whitespace error。
- PlayMode 验证：未执行，需在 Unity 中进入 Level77 验证拖拽开始/结束时 actor 物理类型切换。
- Android/iOS 横屏触控验证：未执行。

## 手工验收清单

- [ ] 首次进入 Level77，确认 actor 初始可正常受物理影响。
- [ ] 开始拖拽目标物体时，确认 actor 不再按 Dynamic 物理下落或被外力继续推动。
- [ ] 松开拖拽物后，确认 actor 恢复 Dynamic 物理。
- [ ] 拖拽中重开、失败、退出或切关，确认再次进入时 actor 不是残留 Kinematic。
- [ ] 快速重复拖拽多次，确认没有物理类型卡死。
- [ ] Android / iOS 横屏触控下验证拖拽开始和结束均能触发。

## 剩余风险

- 尚未运行 Unity PlayMode，无法确认当前 prefab 中 actor 的实际初始物理状态和设计期望是否一定为 `Dynamic`。
- 尚未真机验证移动端触摸拖拽的开始/取消事件是否覆盖所有边界手势。
- 如果后续策划要求拖拽结束恢复为 actor 原始 bodyType，而不是固定 `Dynamic`，需要改为缓存初始值并补充回归验证。

## 回滚路径

- 回滚 `Level77Ctrl.cs` 中拖拽回调和物理状态切换逻辑。
- 回滚 `Level_077.prefab` 中目标 `DragableItem.onDragStartEvent` / `onDragEndEvent` 到空列表。
- 删除或回退本功能说明、`Level77风险评估.md` 和 `DevelopMD/已有功能设计与使用记录.md` 中的 Level77 记录。
