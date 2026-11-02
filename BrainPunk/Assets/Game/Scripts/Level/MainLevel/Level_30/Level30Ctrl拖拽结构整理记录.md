# Level30Ctrl 拖拽结构整理记录

## 2026-07-08 OnWingStartDrag 列表化整理

### 需求与背景

- 用户要求整理 `Level30Ctrl.OnWingStartDrag` 的代码结构，并改用 `List` 管理入口。
- 本次目标是同时整合脚本结构与 Inspector 表现，把 `key1`、`key2`、`star` 及对应移动控制器收敛到一个序列化列表中。

### 影响范围

- 代码文件：`Assets/Game/Scripts/Level/LevelItem/Level_30/Level30Ctrl.cs`
- prefab：`Assets/Game/Prefabs/Level/Level_030.prefab`
- 关联系统：Level30 翅膀拖拽触发、`FollowTarget`、`PingPongMoveHandle`、DOTween 位移动画。
- 未修改配置、存档、全局事件、广告、IAP、analytics、平台 SDK 或编辑器生命周期入口。

### 原有行为基线

- `OnWingStartDrag(Transform target)` 原本分别检查 `key1`、`key2`、`star` 的 `FollowTarget.Target` 是否等于传入 `target`。
- 命中后会设置 `currentWing`，关闭对应 `FollowTarget`，停止对应移动控制，并把对象移动到 `Y=-1.8`。
- 本次修正前脚本字段已经出现 `key1`、`key2`、`star`、`move1`、`move2`、`move3` 的散字段形态，但 prefab 仍显示为散字段 Inspector。
- 原逻辑没有在拖拽开始入口增加空引用兜底；本次整理保持该边界不变。

### 整理后行为

- `Level30Ctrl` 现在只暴露 `wingStartDragItems` 序列化列表，每项包含 `itemTransform` 与 `moveHandle`。
- `Level_030.prefab` 的 `Level30Ctrl` Inspector 已迁移为三条列表项：原 `key1 -> move1`、原 `key2 -> move2`、`star -> move3`。
- 循环不在首次命中后 `break`，因此保留原有多个对象指向同一个 `target` 时多个分支都可执行的语义。
- `BindLockToWing` 现在复用同一个列表关闭可用的 `BoxCollider2D`，没有 `BoxCollider2D` 的列表项会跳过。
- 原重复块内注释掉的 `BoxCollider2D` 启用代码随重复块删除；这些注释代码原本不参与运行，本次也没有启用碰撞器。

### 数据、资源与事件流

- 本次不读写 DB/save，不新增资源加载，不派发或订阅全局事件。
- 拖拽触发仍由 `Level30KeyTrigger.OnTriggerEnter2D` 调用 `OnWingStartDrag(...)` 与 `OnWingEndDrag(...)`。
- DOTween 位移动画目标与时长保持不变。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_30/Level30Ctrl.cs` 以及本次文档文件通过，仅有工作区 LF/CRLF 规范化提示。
- 包含 `Assets/Game/Prefabs/Level/Level_030.prefab` 的整份 diff check 被 Unity YAML 新增空字段行尾空格阻断，命中位置包括 `m_Name:` / `m_EditorClassIdentifier:` 等空值行；未直接修改这些空值语义。
- 静态 YAML 检查确认 `Level30Ctrl` 组件块只保留 `wingStartDragItems` 列表，旧 `key1/key2/star/move1/move2/move3` 散字段无残留。
- 静态扫描 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`Instantiate`、`PlayEvents`、`DispatchEvent`、`Subscribe<`、`PlayerPrefs`、`Debug.Log` 无命中。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 被现有 `Temp\bin\Debug` 元数据 DLL 缺失阻断，首个错误为缺失 `Apple.Core.dll`；未得到本次业务脚本的完整编译证明。

### 剩余风险与未验证项

- 未在 Unity Inspector 中打开 `Level_030.prefab` 做可视化确认，未执行 Unity PlayMode、Level30 实际拖拽流程、Android/iOS 横屏触控 smoke。
- 由于本次不新增拖拽入口空引用兜底，若 prefab 列表项中的 `itemTransform`、`moveHandle` 或 `FollowTarget` 缺失，行为仍可能报错，未在本次修复范围内。

### 回滚方式

- 回滚 `Level30Ctrl.cs` 中的 `wingStartDragItems` 序列化列表与循环处理，并把 `Level_030.prefab` 的 `Level30Ctrl` 组件恢复为旧散字段绑定即可。
- 若需要同时回滚第三组 `star -> move3` 配置，需要确认 prefab 中新增的第三组对象是否属于当前关卡设计，避免误删他人变更。
