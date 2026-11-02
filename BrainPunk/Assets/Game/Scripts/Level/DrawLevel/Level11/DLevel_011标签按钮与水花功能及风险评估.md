# DLevel_011 标签按钮与水花功能及风险评估

## 1. 需求与背景

- 为 DLevel_011 实现 `TagPressButton`：只有指定 Tag 的 Collider2D 能压下按钮。
- 按钮下压时触发既有 WaterSplash Animator 的 `water_2` 长水柱动画；释放后收起水柱。
- 保持关卡局部实现，不修改共享 `PressButtonBase`、WaterSplash Controller、Draw 生命周期或全局事件。

## 2. 原行为基线

- Button/Trigger 原挂载空实现的 `TurntableButton : PressButtonBase`，基类只硬编码响应 `Player`。
- DLevel011Ctrl 是未绑定 Prefab 的空壳，只有无行为的 `Start/Update`。
- WaterSplash 根已有 Animator，并绑定包含 `long`、`stay_long` 与
  `water_2 -> water_2_splash_idle -> water_2_hide` 的 Controller，但按钮没有调用链。
- 按钮原有视觉 Transform、实体 Collider、下压距离 `0.2`、缓动 `0.1` 秒和音效配置已经存在。

## 3. 修改后行为

1. `TagPressButton` 继承 `PressButtonBase`，用序列化 `targetTag` 过滤；Prefab 当前配置 `tools`，
   因而玩家触发基线不变。
2. 按钮用 HashSet 记录匹配的 Collider；第一个进入时按下，最后一个离开时释放，避免多 Collider
   对象局部离开造成误释放。
3. `DLevel011Ctrl` 在 PlayMode 启用时绑定按钮回调。按下时设置 `stay_long=true` 并触发 `long`；
   释放时设置 `stay_long=false`，沿既有 Animator 状态机收起。
4. 按钮单独禁用时会复位并通知释放；控制器禁用时清理回调和 Animator 保持态，避免 Replay/退出残留。
5. Prefab 在根节点显式绑定按钮与 Animator，不依赖运行时层级搜索。

## 4. 影响文件与系统

- `Assets/Game/Scripts/Level/DrawLevel/Level11/DLevel011Ctrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level11/TagPressButton.cs` 及 meta
- `Assets/Game/Prefabs/DrawLevel/DLevel_011.prefab`
- 本地结构记录、风险文档、中央演进记录和已有功能设计记录
- 复用：`PressButtonBase`、Bear Logger、Unity Animator、WaterSplash Controller
- 未修改：共享按钮基类、Animator Controller/动画 clip、DrawLevelCtrl、DrawLineFeature、
  GamePlayPanel、DB/存档、Luban、资源地址、全局事件、Analytics、SDK 与平台配置

## 5. 数据、资源、UI、事件与平台流

- DB/存档/Luban：不涉及。
- 资源：只使用 Prefab 已有 Animator 和 Controller，无加载、实例化或资源地址变化。
- UI：不涉及 HUD 或弹窗。
- 事件：局部 Action 回调；禁用时解除，不新增全局事件。
- 平台：Android/iOS 共用 Physics2D Trigger 与 Animator 路径，无平台分支。
- 性能：无新增 Update；HashSet 规模等于按钮当前接触的少量 Collider，只有 Enter/Exit 时操作。

## 6. 风险项

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| targetTag 配错导致按钮无响应 | P1 | Prefab 显式配置 `tools`，与画线模板 Tag 一致；空值不接受对象 | Inspector 后续改配和其它 Tag 场景未验证 |
| 多 Collider 提前释放导致水花闪断 | P1 | HashSet 等最后一个 Collider 离开才释放 | Player 实际 Collider 组合需 PlayMode 验证 |
| Animator 参数或状态名变化 | P1 | 复用当前 Controller 的 `long`/`stay_long` 既有契约 | 资源后续重命名没有编译期保护 |
| 快速按下/释放过渡卡住 | P1 | 按下先置保持态再触发，释放关闭保持态 | 低帧率和同物理帧 Enter/Exit 尚未验证 |
| Disable/Replay 状态残留 | P1 | 双方 OnDisable 都复位各自状态 | 完整失败、Reset、Replay 尚未验证 |
| 移动端性能与触发差异 | OBS | 无逐帧脚本成本和平台分支 | Android/iOS 横屏真机未验证 |

## 7. 验证证据

- Unity 自动导入后，`Assembly-CSharp.rsp` 已包含 `DLevel011Ctrl.cs` 与 `TagPressButton.cs`；
  Unity 6000.0.59f2 Roslyn 全量编译为 `0 error / 104 warning`，warning 均为项目既有项。
- `git diff --check` 对目标脚本与 Prefab 通过。
- Prefab 静态确认：DLevel011Ctrl 的两个引用非空；TagPressButton GUID 对应 meta；WaterSplash Animator
  Controller 指向既有 `WaterSplash.controller`；按钮原 Transform/Collider/offset/tween 字段保留。
- Editor.log 记录 DLevel_011 由 PrefabImporter 成功导入，未发现本关脚本编译错误。
- 静态生命周期扫描未发现 `OnValidate`、`Reset`、编辑器执行属性、DB/Save、全局事件或直接
  `Debug.Log`。
- 未执行 Unity PlayMode、Android/iOS 真机或完整关卡成功/失败/Replay 验收。

## 8. 手工验收清单

- [ ] 非 `Player` Tag 对象进入 Trigger 时，按钮和水花均不响应。
- [ ] Player 第一个 Collider 进入后，按钮只下压一次并播放一次按钮音效，WaterSplash 进入 water_2。
- [ ] Player 多个 Collider 交叠时，任一 Collider 先离开不会提前释放按钮。
- [ ] 最后一个 Collider 离开后按钮复位，水花从保持态进入 water_2_hide。
- [ ] 快速进入/离开、低帧率和暂停恢复时 Animator 不停在错误状态。
- [ ] 失败、Reset、Replay、退出再进入后按钮与水花恢复 Prefab 初态。
- [ ] Android/iOS 横屏设备验证碰撞、动画、音效与 60 fps 表现。

## 9. 回滚路径

- 删除根节点 DLevel011Ctrl 组件，将 Button/Trigger 的脚本恢复为原 `TurntableButton`。
- 将 `DLevel011Ctrl.cs` 恢复为空壳基线，删除 `TagPressButton.cs` 及其 meta，并同步回滚本地文档追加内容。
- 共享按钮、Animator Controller、动画资源、Draw 系统、配置和存档无需迁移或回滚。
