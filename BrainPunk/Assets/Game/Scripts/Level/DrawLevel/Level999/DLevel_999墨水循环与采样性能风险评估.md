# DLevel_999 墨水循环与采样性能风险评估

## 1. 需求与影响范围

- 需求：降低 TBN/DLevel_999 的画线采样数量；墨水耗尽后的自动清线与补充功能暂不触发。
- 影响文件：`DLevel_999.prefab`、`DLevel999Ctrl.cs` 及本关中文记录。
- 不修改共享画线脚本、其它 DLevel、配置表、存档、资源地址、UI、事件定义、Analytics 或平台 SDK。

## 2. 原行为基线

- 本关 `maxInkLength=300`、`sampleSpacing=0.1`，理论最多约 3000 个采样段。
- 默认碰撞路径按相邻采样点创建子 `BoxCollider2D`；完成线条保留到 Reset/Replay。
- 墨水耗尽后停止新增采样，但不会自动销毁旧线或恢复基础墨水。

## 3. 修改后行为

- 最终 `sampleSpacing=0.5`、`maxInkLength=150`，理论最大采样段约 300，降为原来的约 10%。
- `DLevel999Ctrl` 已于 2026-08-18 改为主线传送门控制器，并绑定到 Prefab；该类不再实现
  `IDrawStrokeCompletedHandler`，因此不会接收画线完成回调，也不会在墨水耗尽时调用 `ResetAttempt()`。
- 墨水耗尽后继续沿用原行为：停止新增采样，已画内容保留到正常 Reset/Replay；不会自动清线或恢复基础墨水。
- 不新增运行时 `Update`、`FixedUpdate`、协程或全局事件。

## 4. 数据、资源、UI 与事件流

- 数据/存档：不写入新增数据；没有新增墨水循环状态。
- UI：不主动调用 `ResetAttempt()`，不触发额外的墨水重置事件或 UI 刷新。
- 资源：传送门功能复用同一局部脚本与 Prefab 现有对象；墨水功能本身不增加运行时资源加载。
- Analytics/广告/IAP/平台 SDK：无新调用链。

## 5. 风险与验证证据

- 静态计算：`150 / 0.5 = 300`，理论碰撞段上限由约 3000 降到约 300。
- 静态绑定：Prefab 包含 `DLevel999Ctrl`，但脚本接口列表不含 `IDrawStrokeCompletedHandler`，墨水重置调用链仍不成立。
- `dotnet build Assembly-CSharp.csproj --no-restore` 通过，结果为 `0 error / 101 个项目既有 warning`。
- 风险：更大采样间距会使急转弯视觉和碰撞轮廓更粗；基础墨水减半会让同长度线条按当前最大墨水归一化后取得更高质量比例。墨水循环未启用，不存在自动销毁旧线带来的帧末表现风险。
- `uncertain`：尚未完成 Unity Editor 导入与 PlayMode 墨水耗尽、Reset/Replay、低端 Android/iOS 横屏帧率与触控手感验证。

## 6. 回滚路径

1. 若回滚降采样，将 `DLevel_999.prefab` 的 `sampleSpacing` 恢复为 `0.1`、`maxInkLength` 恢复为 `300`。
2. `DLevel999Ctrl.cs/.meta` 当前承担主线传送门职责，不可作为回滚墨水参数时的删除项。
3. 若以后启用墨水循环，应新增显式关闭默认值的局部组件或重新评估职责，不能直接让当前传送门控制器实现 Handler 后自动生效。
4. 无需迁移或清理存档、配置、资源包或玩家数据。
