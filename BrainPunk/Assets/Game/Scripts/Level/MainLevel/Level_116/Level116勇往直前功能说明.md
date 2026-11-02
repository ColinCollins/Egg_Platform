# Level116「勇往直前」功能说明

## 需求与范围

本记录基于 2026-08-18 黄色关卡脚本精简任务建立。只清理可由代码和编译器证明不可达的分支，不改变 Level116 的输入判定、障碍运动、碰撞反馈、失败/成功、Reset/Replay、资源、UI、配置或打点流程。

## 相关脚本与职责

- `Level116Ctrl.cs`：解析角色和关卡引用，绑定输入与事件，统一推进障碍，处理碰撞、失败收口及生命周期清理。
- `Level116ForwardRule.cs`：记录越过挡板后的首次动作、连续右移链和三角障碍无敌资格。
- `Level116InputTracker.cs`：在本关局部观察 GameplayPanel 左右按钮与键盘输入，不修改共享面板或全局输入合同。
- `Level116Obstacle.cs`：按方向、速度档位和初始相位在世界 Y=-7 到 7 的固定 14 单位行程内循环，并把触发碰撞交回控制器。

## 2026-08-18 行为等价精简

- `TravelDistance` 由常量 `TopWorldY(7) - BottomWorldY(-7)` 编译期确定为 14，原 `NormalizeDistance()` 中 `TravelDistance <= 0` 分支永远不可达；本轮删除该分支，仍直接使用 `Mathf.Repeat(distance, 14)`。
- `Level116Ctrl.OnDisable()` / `OnDestroy()` 的原清理边界均保留；未合并或删除障碍命中特效、输入解绑、事件解绑和角色物理恢复逻辑。
- `Assembly-CSharp.csproj` 编译通过（0 错误），删除前的 `Level116Obstacle.cs:160 CS0162` 不可达代码告警已消失。

## 数据、资源、事件与回滚

本轮未修改 Prefab、序列化值、障碍编队、速度、输入资源、声音/特效、配置、存档、事件类型和打点。若需回滚，只需在 `NormalizeDistance()` 中恢复常量不可能成立的防御分支；其它 Level116 文件无需回滚。

## 未验证项

尚未执行 Unity PlayMode、Reset/Replay、暂停/恢复、Android/iOS 触摸和真机帧率回归，运行时表现标记为 `uncertain`。
