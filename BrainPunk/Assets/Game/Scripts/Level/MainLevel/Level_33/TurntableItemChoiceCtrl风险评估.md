# TurntableItemChoiceCtrl 风险评估

## 评估范围

- 变更入口：当前工作区未跟踪并修改的 `Assets/Game/Scripts/Level/LevelItem/Level_33/TurntableItemChoiceCtrl.cs`。
- 扩散范围：Level_33 转盘按钮触发、DOTween 旋转、区域回调状态、奖励/掉落分支、音效生命周期、prefab 挂载。
- 不在本次范围：关卡胜负条件、存档、配置表、资源热更、全局事件、analytics、广告、IAP、RemoteConfig、Android/iOS 平台 SDK。

## 事实

- CodeGraph 证据显示 `TurntableItemChoiceCtrl` 与 `TurntableItemCtrl` 结构相近，依赖 `TurntableButton` 和 `ThrowItemHandle`。
- `TurntableButton` 继承 `PressButtonBase`，按钮进入时通过 `SetEnter(Play)` 调用脚本播放逻辑。
- `TurntableItemChoiceCtrl` 原注释已经声明需求：从两组角度范围中随机一组作为停止范围，并保持旋转动画。
- `rg` 静态证据显示 `Assets/Game/Prefabs/Level/Level_033.prefab` 仍引用 `TurntableItemCtrl`，未发现 `TurntableItemChoiceCtrl` 或其 guid 引用。
- 本次未修改 prefab，所以新脚本是否生效取决于后续 prefab/场景绑定。

## 直接结论

- 本次实现不会影响当前已绑定 `TurntableItemCtrl` 的 prefab 行为，除非后续手动或另一次变更把新脚本挂到关卡对象上。
- 新脚本默认两组范围为 `0,0`，未配置角度时停止点与旧逻辑保持一致。
- 新增逻辑只在 `Play()` 单次旋转开始时计算随机角度，没有新增 `Update()`、轮询、DB/save 或全局事件链路。
- `IsStopRangeA` 直接记录本次 `Play()` 随机选择的停止区间；`true` 表示 A，`false` 表示 B。

## 风险项

| 等级 | 风险 | 扩散链路 | 缓解与状态 |
| --- | --- | --- | --- |
| P1 | 新脚本未挂载时功能不会在关卡内生效 | 工作区脚本变更 -> prefab 仍引用 `TurntableItemCtrl` -> 玩家触发旧逻辑 | 本次文档与最终说明明确未改 prefab；如要启用需后续绑定并验证 |
| P1 | 停止角度区间配置错误导致奖励区域与转盘停点不匹配 | Inspector 角度范围 -> `GetRandomStopAngle()` -> 转盘最终角度 -> TriggerArea/奖励分支 | 默认保持旧停点；需要 PlayMode 按目标角度实测 key、other、drop |
| P1 | 销毁或切关时循环音效残留 | `Play()` 播放 loop 音效 -> 对象销毁 -> 原逻辑可能不停止 | 已在 `OnDestroy()` 调用 `StopTurntableSound()` 并 Kill tween；仍需 PlayMode 验证 |
| OBS | 外部调用方读取时机不一致 | `Play()` 开始 -> 已选定停止区间 -> 外部读取 `IsStopRangeA` | API 只表达本次选中的 A/B；如调用方要求动画真正停稳，应放在旋转完成后的逻辑读取 |
| OBS | 角度随机边界的设计口径待验 | `Random.Range(min,max)` 对 float 上界近似不含端点 -> 最终停点分布 | 对转盘奖励区域通常可接受；如必须包含上界需另行明确 |
| OBS | 移动端横屏触控和帧率未验证 | 玩家触发按钮 -> DOTween 3 秒旋转 -> 奖励分支 | 编译与静态检查通过，但 Android/iOS smoke 未执行 |

## 验证证据

- 编译：`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过，0 个错误，0 个警告。
- 敏感关键字扫描：目标脚本未命中 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB.`、`Save`、`RemoteConfig`、`Instance`、`Instantiate`、`Update(`。
- 空白检查：PowerShell 行尾空格检查通过。
- prefab 引用扫描：未发现 `TurntableItemChoiceCtrl` 或其 guid 被 prefab/scene 引用。
- 2026-07-08 公开 API 简化为 `IsStopRangeA` 后复验：`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过，0 个错误，0 个警告；目标脚本敏感关键字和行尾空格检查通过。

## 未验证缺口

- Unity PlayMode 未验证。
- Android/iOS 横屏未验证。
- 未验证 Level_33 具体转盘角度范围、TriggerArea 和奖励物体的空间对应关系。
- 未验证对象销毁发生在旋转完成后 0.1 秒等待期间的运行时日志表现。
- 未验证外部关卡逻辑实际读取 `IsStopRangeA` 的调用时机。

## 回滚路径

- 脚本级回滚：恢复固定 `360 * 20f` 目标角度，移除新增角度范围和随机角度方法。
- 绑定级回滚：如果后续 prefab 切换到 `TurntableItemChoiceCtrl` 后出现异常，重新挂回 `TurntableItemCtrl`。
- 验证回滚条件：PlayMode 出现转盘停点错误、奖励无法触发、循环音效残留、按钮重复触发卡死时，先回滚 prefab 绑定，再评估脚本逻辑。
