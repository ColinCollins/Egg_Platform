# DLevel_011 多 TipsObject 展示功能与风险评估

## 1. 需求与原行为基线

- 需求：关卡中两份 TipsObject 都能通过 `DrawLineFeature` 现有入口展示，同时原关内
  TipsObject 对象引用绝不能丢失。
- 原行为：共享组件只序列化和控制一个 `tipsObject`，Reset 也只缓存一个布尔初态。
- 本次目标是把旧字段的数据迁入单一列表，不保留双字段结构，不修改 UI 入口或 Draw 生命周期。

## 2. 修改后行为与受影响文件

- `DrawLineFeature.cs` 删除旧单字段与过渡附加字段，只保留 `tipsObjects`；Show/Hide 统一处理，
  Reset 按不同对象分别恢复初态，空引用与重复引用可安全处理。
- `DLevel_011.prefab` 的原提示迁入 `tipsObjects[0]`，第二个提示位于 `[1]`，两个引用均非空且不重复。
- 本地文档同步更新；不修改关卡 UI、存档、配置、资源地址、全局事件或平台分支。

## 3. 风险项

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| 原提示绑定丢失 | 高 | 20 个 DLevel 已逐项迁移并静态确认列表非空 | Unity Inspector 后续人工误清空仍需制作检查 |
| 重复引用导致重复恢复 | 低 | 初态缓存和 ShowHint 都按引用去重 | 后续人工重复配置仍需 Inspector 目视检查 |
| 多提示初态被覆盖 | 中 | 每个不同对象独立缓存 `activeSelf` | PlayMode Reset 视觉未验证 |
| 移动端性能 | 低 | 仅在初始化、显隐、Reset 遍历少量引用，无逐帧逻辑 | Android/iOS 真机未验证 |

## 4. 验证证据

- 20 个现有 DLevel Prefab 全部存在非空 `tipsObjects`；旧 `tipsObject` 与
  `additionalTipsObjects` 字段计数为 0。DLevel_009、DLevel_011 各有两个不同提示对象。
- DLevel_011 有两个提示对象；164 个 YAML 锚点唯一，非零本地引用无缺失。
- Unity 当前 rsp 尚未收录随后新增的 `TagPressButton.cs`；在同一 rsp 基础上显式补入该源文件后，
  Assembly-CSharp Roslyn 全量编译通过，结果为 `0 error`。
- Unity Editor.log 显示 DLevel_011 由 PrefabImporter 成功导入，未发现该 Prefab 导入错误。
- 未执行 Unity PlayMode、连续 Show/Hide、清线/失败 Reset、Replay、Android/iOS 横屏与低帧率验证。

## 5. 手工验收清单

- [ ] 首次进入关卡时，两份提示分别保持 Prefab 中配置的初始显隐。
- [ ] 触发现有提示入口后，两份提示同时显示；关闭时同时隐藏。
- [ ] 连续显示/隐藏后清线或 Attempt Reset，两份提示分别恢复自己的初态。
- [ ] 未获取提示时，失败 Reset 与 Replay 后两份提示均保持隐藏；已经获取提示时，同关 Reset、失败重试和 Replay 后两份提示均恢复显示，且不重新收取首次提示费用。
- [ ] Android/iOS 横屏下验证触控入口、低帧率和切后台恢复。

## 6. 回滚路径

- 回滚需把 20 个 Prefab 的 `tipsObjects[0]` 写回旧单字段，再恢复旧单对象代码；DLevel_009、
  DLevel_011 的第二项会失去共享控制。资源、配置、存档和事件无需处理。
