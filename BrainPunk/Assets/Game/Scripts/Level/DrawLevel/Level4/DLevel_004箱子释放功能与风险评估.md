# DLevel_004 箱子释放功能与风险评估

> 2026-07-31 现状说明：本文保留 2026-07-28 的箱子释放历史记录。当前 DLevel_004 prefab
> 已调整为 10 个石头；石头释放与命中特效定位的现行行为、验证和回滚见同目录
> `DLevel_004石头碰撞特效定位功能与风险评估.md` 及 `DLevel_004脚本结构记录.md`。

## 1. 需求与背景

Level4 需要在玩家第一笔有效绘制结束后，将指定 Box 列表中的全部 Rigidbody2D 切换为
`Dynamic`。实现必须保持关卡局部，不修改共享 DrawLevelCtrl，也不新增全局事件。

## 2. 原行为基线

- DLevel4Ctrl 原为 Unity 默认空脚本，仅包含空 `Start()` 和 `Update()`。
- DLevel_004 已挂载该组件，但没有箱子引用或画线完成处理。
- 四个 Box 实例来自共享 `Box.prefab`，DLevel_004 原先将它们的 BodyType 覆盖为
  `Dynamic`。
- 没有代码在第一笔有效画线结束时统一切换这些 Box。

## 3. 修改范围

- `Assets/Game/Scripts/Level/DrawLevel/Level4/DLevel4Ctrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_004.prefab`
- Level4 本地结构记录与本文档
- DrawGame 中央结构演进记录和已有功能设计记录

未修改共享 DrawLevelCtrl、DrawLineFeature、Box.prefab、UI、DB、Luban、资源地址、打点、
广告、SDK 或平台配置。

## 4. 修改后行为

1. DLevel_004 prefab 直接把四个 Box 的初始 BodyType 序列化为 `Kinematic`。
2. DLevel4Ctrl 作为局部 `IDrawStrokeCompletedHandler` 被唯一主 DrawLevelCtrl 自动发现。
3. 第一笔有效画线结束时，将全部有效绑定切为 `Dynamic` 并调用 `WakeUp()`。
4. 后续笔画通过 `IsFirstStroke` 门禁，不再修改刚体状态。
5. 当前 Reset 与 Replay 均销毁旧 DLevel 并从 prefab 重建，不缓存或恢复 Box 运行时姿态。
6. `OnDrawAttemptReset()` 仅为满足现有接口保留空实现。

## 5. 数据、资源、UI 与事件流

- 数据/存档：无读写。
- 资源：只使用 DLevel_004 已有的四个 Box 实例。
- UI：无。
- 事件：不新增 PlayEvents；调用链为
  `DrawLineFeature.StrokeCompleted → DrawLevelCtrl → IDrawStrokeCompletedHandler`。
- 平台：逻辑使用 Unity Rigidbody2D，Android/iOS 共用；设备表现未验证。

## 6. 风险项

| 风险 | 等级 | 控制 |
| --- | --- | --- |
| DLevel4Ctrl 或 Box 引用未绑定 | 中 | Prefab 显式绑定四个 Rigidbody2D；空引用只跳过对应 Box |
| 非有效短线误释放 | 低 | 只消费 DrawStrokeResult；无效画线不会产生完成结果 |
| 多笔或重复回调再次释放 | 低 | `IsFirstStroke` 与 `_hasReleasedBoxes` 双门禁 |
| 为不存在的原地 Reset 增加状态缓存 | 低 | 不缓存初始姿态；严格依赖当前整关重建流程 |
| 四个 Dynamic Box 同时碰撞导致抖动或穿透 | 中 | 保留各实例原碰撞配置；需 PlayMode/设备验证 |
| 多个局部 Handler 相互依赖顺序 | 低 | 本控制器不依赖其他 Handler；不新增排序机制 |

## 7. 验证证据

- 静态检查：代码/文档无尾随空白；Prefab fileID 无重复。
- C# 编译：`dotnet build Assembly-CSharp.csproj --no-restore` 通过，
  `0 error / 102 warning`；warning 为工程现有输出。
- Prefab 绑定：DLevel4Ctrl 和主 DrawLevelCtrl 各 1 个；四个 Box 列表引用均有唯一
  stripped Rigidbody2D，并分别关联四个现有 Box PrefabInstance。
- Unity PlayMode：未执行。
- Android/iOS 横屏：未执行。

## 8. 剩余缺口

- `uncertain`：尚未观察第一笔完成时四箱同时落下的实际关卡表现。
- 未验证失败 Reset、手动 Reset、Replay 重建后四箱是否按 prefab 的 Kinematic 初态出现。
- 未验证移动端多刚体同时碰撞时的帧率和穿透情况。

## 9. 回滚路径

1. 从 DLevel_004 根节点移除 DLevel4Ctrl 及四个 Box 引用。
2. 只有同时移除 DLevel_016 对该脚本的引用后，才删除 `Level4/DLevel4Ctrl.cs` 与对应 meta；
   若只回滚 DLevel_004，则保留脚本供 DLevel_016 使用。
3. 删除本文与 Level4 结构记录中的本次机制内容。
4. 共享 DrawLevelCtrl、DrawLineFeature 和 Box.prefab 保持不变，无需回滚。

## 10. 2026-08-02 DLevel_016 复用更新

- DLevel_016 根节点直接复用 DLevel4Ctrl，`boxRigidbodies` 只绑定本关唯一 Stone；Stone
  初始 Kinematic，第一笔有效画线后切 Dynamic 并 WakeUp。
- 该复用只泛化 DLevel4Ctrl 的 XML 职责注释，运行时代码与 DLevel_004 现有绑定不变。
- DLevel_016 prefab 导入、Assembly-CSharp 编译及 YAML 引用检查通过；两关完整 PlayMode、
  Reset/Replay 和移动端物理仍待验收。
