# Level30Ctrl 拖拽结构整理风险评估

## 2026-07-08 OnWingStartDrag 列表化整理

### 原始行为基线

- `Level30KeyTrigger.OnTriggerEnter2D` 获取自身 `FollowTarget.Target` 后，依次调用 `Level30Ctrl.OnWingStartDrag(...)` 与 `OnWingEndDrag(...)`。
- `OnWingStartDrag` 原本按 `key1`、`key2`、`star` 三段重复代码检查 `FollowTarget.Target`，命中后停止移动并执行 `DOMoveY(-1.8f, .8f).SetEase(Ease.InQuart)`。
- `OnWingEndDrag`、门锁绑定、销毁拖拽目标等后续流程本次未改动；`BindLockToWing` 仅把硬编码关闭 `key1/key2` 碰撞改为遍历列表关闭可用的 `BoxCollider2D`。

### 受影响文件与资源

- `Assets/Game/Scripts/Level/LevelItem/Level_30/Level30Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_030.prefab`
- 新增记录文档：`Level30Ctrl拖拽结构整理记录.md`
- 未修改其它 Level30 脚本、配置、资源或 UI。

### 使用的既有系统

- Unity `Transform` / `Collider2D`
- `FollowTarget`
- `PingPongMoveHandle`
- DOTween `DOMoveY`

### 风险项

- **列表映射风险**：若 Inspector 列表映射顺序或移动控制器填错，会改变拖拽开始时停止的移动对象。当前 prefab 迁移为原 `key1 -> move1`、原 `key2 -> move2`、`star -> move3`。
- **多命中语义风险**：如果改成首次命中后退出，会改变多个 `FollowTarget.Target` 指向同一对象时的行为。当前循环不 `break`，保持原有三段 `if` 可多次命中的语义。
- **空引用风险**：本次没有新增空引用保护，避免把结构整理变成行为修复；原有缺失绑定时的报错风险仍存在。
- **碰撞关闭范围风险**：`BindLockToWing` 从硬编码 `key1/key2` 改为遍历列表关闭 `BoxCollider2D`。为避免第三组对象无碰撞体时崩溃，当前只在碰撞体存在时关闭。
- **注释清理风险**：原重复块内注释掉的 collider 启用代码被随结构整理移除；它们原本不执行，本次未改变 collider 启用逻辑。

### 知识库风险工作流对齐

- **Git 变更入口**：当前工作区未提交 diff 中的 `Level30Ctrl.cs`、`Level_030.prefab` Inspector 绑定迁移，以及本次新增的 Level30 本地文档。
- **扩散链路**：`Level30KeyTrigger.OnTriggerEnter2D` 触发 `OnWingStartDrag(...)`，再影响 `FollowTarget` 停用、`PingPongMoveHandle.StopMove()` 和 DOTween 下移动画；门锁绑定后会遍历同一个列表关闭可用的 `BoxCollider2D`。
- **优先级判断**：本次意图为代码结构整理且静态 diff 显示映射保持原样，新增风险暂定为 OBS。若后续发现列表映射或多命中语义被改错，会升级为 P1，因为会造成 Level30 局部交互异常或关卡卡住。

### 验证证据

- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_30/Level30Ctrl.cs` 通过，仅有 LF/CRLF 规范化提示。
- 本次脚本与文档文件 diff check 通过；包含 `Level_030.prefab` 的整份 diff check 被 Unity YAML 新增空字段行尾空格阻断，命中位置为 prefab 新增块中的 `m_Name:` / `m_EditorClassIdentifier:` 等空值行。
- 静态 YAML 检查确认 `Level30Ctrl` 组件已迁移为 `wingStartDragItems`，三项分别绑定原 `key1 -> move1`、原 `key2 -> move2`、`star -> move3`。
- 敏感链路静态扫描无命中：编辑器生命周期、DB/save、RemoteConfig、Instantiate、全局事件、PlayerPrefs、Debug.Log。
- `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 未完成业务代码编译，阻断原因是当前 `Temp\bin\Debug` 缺失大量 Unity/插件元数据 DLL，首个错误为 `Apple.Core.dll` 缺失。

### 未验证缺口

- 未运行 Unity Editor 编译、PlayMode 或 Level30 手工拖拽流程。
- 未验证 Android/iOS 横屏触控。
- 未在 Unity Inspector 中打开 `Level_030.prefab` 进行可视化确认；当前仅基于 YAML 静态证据确认列表绑定。

### 回滚路径

- 恢复 `OnWingStartDrag` 为三段独立 `FollowTarget` 判断，并把 `Level_030.prefab` 的 `Level30Ctrl` 组件恢复为旧散字段绑定。
- 若回滚时看到第三组 `star` 或 `move3` 相关 prefab 改动，应先确认它来自当前关卡设计还是本次结构整理，不能直接删除未确认归属的改动。

### 待确认事项

- `star -> move3` 的运行时表现仍未在 Unity PlayMode 中验证；当前只完成脚本与 prefab Inspector 结构整合。
