# Level 58 GamePlayPanel 兼容修复与风险记录

## 2026-10-09 需求与原行为基线

- 用户要求修复此前共享 `GamePlayPanel` 按钮层级调整可能影响的关卡。Level 58 使用专用 `GamePlayPanel_020_58`，并有明确的关卡级差异：继承的 Jump `CustomButton` 和 `ClickScaleAnim` 被移除，旧 Jump 根 Image 保持可见但 `raycastTarget` 被覆盖为 false。
- 共享基础 Prefab 后续删除旧根 Image，新增 Jump 根 `UIEmptyRaycastTarget`，并把视觉 Image 移到 `bg`。Level 58 的旧 Image 覆盖因此失去目标；新根射线组件和 `bg` Image 默认接收射线，形成比本关原约定更大的无业务按钮射线区域。
- 本关目录此前没有匹配的功能/风险文档；本记录以当前源码、`Level_058.prefab` 的 `gamePlayPanelName: GamePlayPanel_020_58`、基础/variant Prefab 和 Git 差异为依据补齐文档缺口。

## 影响文件与系统

- 修改：`Assets/Game/UI/GamePanel/Resources/GamePlayPanel_020_58.prefab`。
- 只读核对：`Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab`、`Level_058.prefab`、`Level58Ctrl.cs`、`UIEmptyRaycastTarget.cs`。
- 未修改 Level 58 玩法脚本、关卡 Prefab、事件、存档、配置、资源加载、按钮1/按钮2、Actor 或其它关卡；`Assets/GF`、`Packages` 和 GuruSDK 保持只读。

## 修改后行为与资源流

1. Level 58 仍通过 `gamePlayPanelName` 加载 `GamePlayPanel_020_58`。
2. variant 继续移除继承的 Jump `CustomButton` 与 `ClickScaleAnim`，因此 Jump 根不承担共享跳跃业务。
3. variant 新增移除基础 Jump 根 `UIEmptyRaycastTarget`，并覆盖新 `bg` Image 的 `raycastTarget = false`，恢复旧版本“底板可见但不以整块区域截获射线”的语义。
4. 两条指向已删除旧根 Image 的失效覆盖已清理；该关原有新增 SkeletonGraphic 及其射线状态保持，不在本轮扩大处理。

## 风险项

- **P1，已静态修复但待运行确认**：共享新增的 290×153 根/背景射线区域可能遮挡 Level 58 原交互。当前 variant 已同时关闭根和背景新增射线来源，但未运行 Pointer/Physics 交互链，真实穿透仍为 `uncertain`。
- **P2，保留既有边界**：该关新增 SkeletonGraphic 自身仍可接收射线；它在共享层级调整前已经存在，本轮没有证据表明应改变其设计。若人工验收仍在局部区域被挡，应先用 EventSystem Raycast 结果确认命中对象，不直接关闭既有组件。
- **P2，Prefab 继承风险**：直接文本断言能证明 source fileID 与 override 关系存在；现有 Unity 编辑器已完成基础与本关 variant 的 PrefabImporter 且无目标导入错误，但不能替代运行时 EventSystem 行为验证。

## 验证证据与未覆盖

- 静态断言通过：基础 Jump 根含 `UIEmptyRaycastTarget fileID 2351160988346309770`，背景 Image 为 `2140115765941960643`；Level 58 variant 移除前者、把后者 `raycastTarget` 设为 0，并且不再引用已删除旧 Image `7873520932093643005`。
- `Assembly-CSharp.csproj` 完整定向编译退出码 0，0 error / 146 warning；本次只改 Prefab，但编译同时验证共享 `GamePlayPanel` 新 API 与关联三关代码。警告为工程既有范围。
- 现有 Unity 6000.0.59f2 编辑器重新导入基础 `GamePlayPanel.prefab`、全部受影响派生面板和 `GamePlayPanel_020_58.prefab`，日志无目标 PrefabImporter 错误。
- 目标 `git diff --check` 通过，仅有行尾转换提示。
- 未执行 PlayMode、Level 58 实际点击/拖动、Reset/Replay、Android/iOS 横屏或 IL2CPP。最短人工验收：进入 Level 58 后在原 Jump 大区域和新增 SkeletonGraphic 区域分别点击/拖动，确认底层玩法输入与两枚关卡按钮符合原表现，同时观察 Console 无 Prefab Missing/引用错误。

## 回滚路径

- 只撤销 `GamePlayPanel_020_58.prefab` 对 `2351160988346309770` 的移除和 `2140115765941960643.m_RaycastTarget = 0` 覆盖，并按需恢复两条旧 fileID 覆盖；不得整体恢复 Prefab，以免覆盖本关其它既有自定义内容。
- 若 Unity 导入证明 removed component 不可用，应在 Prefab Mode 使用同一语义保存 variant，再重新核对落盘引用；不得改回运行时名称扫描或修改 Level58Ctrl 绕过 UI。
