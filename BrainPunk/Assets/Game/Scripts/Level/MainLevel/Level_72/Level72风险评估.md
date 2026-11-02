# Level72 plane 拖动旋转风险评估

## 原始基线与变更边界

- 原 prefab 已有策划创建的 `RotateGroup` / `plane` 层级和 Ground 碰撞盒调整，但没有旋转输入控制器。
- 本次新增 `Level72Ctrl`，局部调整 Level 72 Actor 实例层级，并复用根级 `TrapEventHandlle`、`Knock_Explosion` 与 5 个地刺事件绑定；共享玩法脚本、Actor prefab、资源 prefab、配置、存档与平台链路均未修改。
- 输入区明确为 `plane` 当前 5 个子 Ground 碰撞盒的并集，而不是额外创建的整屏或矩形触发区。

## 风险与控制

- **输入区随地形变化**：脚本在 `Awake()` 缓存 `plane` 下全部 `Collider2D`；Ground 碰撞盒尺寸变化或以后新增子碰撞体会同步改变手势区。当前 prefab 静态确认只有 5 个目标碰撞盒，PlayMode 需逐段点测边界和空白。
- **重叠物体抢命中**：不使用全局单命中 Raycast；只遍历已缓存的 plane 碰撞盒并调用 `OverlapPoint`，Actor、陷阱或按钮 mask 不会替换目标结果。
- **多指接管或残留**：开始后锁定 owner `fingerId`；其他手指被忽略。End、Cancel、触摸丢失、失焦、切后台、暂停和禁用都会清理 owner。
- **跨角度跳变**：使用逐帧 `Mathf.DeltaAngle`，避免正负 180 度边界跳转；中心死区内暂停并在离开后重新建基线。
- **非 Playing 期间误操作**：同时监听游戏状态与暂停/恢复事件；暂停、成功、失败和重置入口会立即停止拖动。
- **重置角度残留**：`GameResetEvent` 先恢复缓存的初始 `localRotation`，原 `PlayCtrl` 随后仍销毁并重建关卡实例。
- **缺引用**：`RotateGroup`、`plane`、相机或子碰撞盒缺失时记录 warning 并保持输入关闭，不继续空引用调用。
- **旋转地形与固定 Actor 交互**：Actor 已保持在关卡根节点，不随 `RotateGroup` 转；旋转中的地形、门或陷阱仍可能扫入、推挤或穿过 Dynamic Rigidbody2D，必须由 PlayMode 验证。本次不修改刚体、重力或插值。
- **死亡依赖引用**：`TrapEventHandlle` 缺 Actor 或粒子引用会导致死亡链异常；当前静态审计确认两条引用闭合，仍需在 Unity Inspector 和 PlayMode 验证实际实例。
- **击退方向**：复用 Level 013 的处理器，力度 10 始终沿世界向上，不随地刺旋转角度改变；这是既有行为，旋转后的体验需实测。
- **重复触发与延迟失败**：多个地刺可能在同一物理帧触发；需验证 Actor 死亡锁与既有延迟失败事件不会重复结算。
- **性能**：每帧只跟踪一个 owner 并计算一次角度；5 个碰撞盒只在按下时遍历。移动端实际 CPU/物理开销仍待真机确认。

## 验证证据

- Prefab 组件 fileID 无重复，根组件引用闭合；脚本 GUID 唯一，`RotateGroup`、`plane`、Camera 序列化类型正确。
- 任务开始前该 prefab 已同时存在大量暂存和未暂存的策划改动；本次只做 Level 72 局部增量，未覆盖或还原整文件。
- 结构化静态审计确认 186 个 YAML header fileID 唯一，23 个 PrefabInstance 的 parent/children 双向一致；Actor 只属于关卡根，其他对象未因 Actor 重挂发生层级漂移。
- 5/5 个地刺事件目标、处理器类型与方法正确；Trigger Collider 保留，默认 handler 保持移除，根级 handler 的 Actor/ParticleSystem 引用闭合。
- 静态输入审查未发现第二根手指覆盖 owner、中心穿越跳角、暂停仍拖动或触摸丢失后永久占用的问题。
- Unity 6000.0.59f2 自带 Roslyn 按当前目标程序集响应文件追加 `Level72Ctrl.cs` 编译通过，退出码为 0、0 errors；临时程序集包含 `Level72Ctrl`。
- 已跟踪差异执行 `git diff --check` 通过，仅有 LF/CRLF 转换提示；尚未跟踪的 `Level_72` 目录 6 个新文件已另行扫描，未发现尾随空格。Unity Editor 正式导入/程序集重建与 PlayMode 尚未完成。

## 未验证项

- Unity Inspector 的 Missing Script、三条引用和运行时缓存数量可视确认。
- 盒内/盒外、UI、多指、跨 180 度、连续多圈、中心死区、暂停/恢复、切后台与重置的实际输入矩阵。
- Actor 不旋转时与旋转地形、门、陷阱及失败区交互的物理稳定性和通关流程。
- 5 个地刺逐一触发、旋转后触发、同时碰多刺、死亡重开后再触发，以及粒子、音效、击退和失败时序。
- Android/iOS 横屏触摸、帧率、发热和后台恢复。

## 回滚路径

- 从 `Level_072.prefab` 根节点移除 `Level72Ctrl` 组件引用和序列化块。
- 同时移除本关根级 `TrapEventHandlle`、`Knock_Explosion` 实例，成套还原 5 个地刺 UnityEvent 和 Actor 层级；不要修改或删除 Level 013 的共享脚本与共享粒子资源。
- 删除 `Assets/Game/Scripts/Level/LevelItem/Level_72` 及对应文件 meta。
- 保留策划已有的 `RotateGroup`、`plane` 层级和碰撞盒调整；共享系统无需回滚。
