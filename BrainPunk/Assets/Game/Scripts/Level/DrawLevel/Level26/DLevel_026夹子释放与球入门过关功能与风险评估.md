# DLevel_026 夹子释放与球入门过关功能与风险评估

## 功能基线

- DLevel_026 使用 `DrawGame_AlienClipBallRig` 组合预制体承载猫、夹子、球与球内 Actor。
- 玩家完成第一笔有效画线后，夹子应从默认夹紧状态播放 `clip_open`，球脱离 Spine/BoneFollower 层级并切换为 Dynamic。
- 球进入右侧 `DrawGame_SuccessDoor` 后，由门的 `OnTrigger2DHandle` 调用当前关卡根节点 `DrawLevelCtrl.OnTriggerSuccess`；开门、角色与球壳渐隐完成后提交成功。
- 倒计时只承担超时失败，不得直接提交成功。

## 本次变更

1. 新增 `DLevel26Ctrl`，继承已验证的 `DLevel2Ctrl` 单套装置流程，复用开场、夹紧、首笔开夹放球和重开复位逻辑。
2. DLevel_026 根节点移除误带入的 `DLevel37Ctrl` 与无有效 Actor 引用的 `TrapEventHandlle`。
3. 将误配置为“5 秒直接成功”的倒计时改为“20 秒超时失败”。
4. 将 `DrawLevelCtrl.successDoorSpine` 和成功门的 UnityEvent 目标改绑到画面右侧启用中的门。
5. 将 `successExtraRendererGroup` 绑定到共享球预制体的渲染组，使球壳与球内 Actor 同步退场。
6. 保留原拼接素材中的第二扇门实例为禁用状态，并改名为 `DrawGame_SuccessDoor_DisabledLegacy`，避免编辑器 AutoBind 将其误识别为正式成功门。

## 受影响文件

- `Assets/Game/Prefabs/DrawLevel/DLevel_026.prefab`
- `Assets/Game/Scripts/Level/DrawLevel/Level26/DLevel26Ctrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level26/DLevel_026夹子释放与球入门过关功能与风险评估.md`

## 数据、UI、资源与事件影响

- 数据：未修改 Luban、JSON 或关卡排序配置。
- UI：倒计时显示沿用通用 `DrawLevelCountdownCtrl`；画线结束后开始 20 秒失败倒计时。
- 资源：继续复用共享 `DrawGame_AlienClipBallRig` 与 `DrawGame_SuccessDoor`，未复制猫、夹子、球资源。
- 事件：第一笔有效画线由 `IDrawStrokeCompletedHandler` 驱动放球；成功只由启用门的 `OnTrigger2DHandle.onEnter` 调用 `DrawLevelCtrl.OnTriggerSuccess`。
- 物理：释放前球为 Kinematic；脱离夹子层级、处理镜像缩放后改为 Dynamic。球现有质量 5、重力缩放 1.5、线性阻尼 0.03 与物理材质保持不变。

## 验证口径

1. 进入 DLevel_026，确认夹子默认闭合且球不会提前坠落。
2. 完成第一笔有效画线，确认夹子只打开一次，球脱离夹子并受重力运动。
3. 继续画线不应重复释放或重置球。
4. 球进入右侧启用门后，应播放开门与渐隐并提交成功；未入门不得成功。
5. 20 秒内未进门应触发通用超时失败。
6. 重开后夹子、球父级、位置、旋转、缩放、碰撞偏移与 Kinematic 状态均恢复。

## 风险与注意事项

- DLevel_026 来自多关卡素材拼接，关卡中仍保留一个禁用的旧成功门实例；当前已通过改名和明确序列化绑定隔离，不参与运行时成功逻辑。
- `DLevel26Ctrl` 依赖共享组合预制体中的 `DrawEntrancePathView`、`DrawAlienClipBallRigView`、`bone2`、`clip_close`、`clip_open`、`idle01` 与 `control`。共享预制体层级或 Spine 动画名变化时需同步回归。
- 球内 Actor 与球壳属于同一 Rigidbody2D 复合体；门触发身份识别依赖 `DrawLevelCtrl.ResolveSuccessTriggerActor` 的同刚体解析规则。

## 回滚方式

1. 从 DLevel_026 根节点移除 `DLevel26Ctrl`，恢复原控制器组件配置。
2. 恢复倒计时原参数、成功门回调目标以及 `DrawLevelCtrl` 的门和附属渲染组引用。
3. 删除 `Level26/DLevel26Ctrl.cs` 及其 `.meta`；共享组合预制体和通用控制器无需回滚。

## 2026-08-03 双平台旋转修复

### 背景与原行为

- D026 的两块平台都已经配置 `HingeJoint2D` 并连接到对应的 Static 齿轮，但平台 `Rigidbody2D` 保持 Kinematic。
- 平台原本隐式依赖 D037 控制器在第一笔完成时切换 Dynamic；清理跨关卡控制器后，两块平台不会受球或画线施加的力矩。

### 新行为与影响

- 新增 `DLevel26PlatformCtrl`，只管理 D026 的两组齿轮铰链平台。
- 开场时两块平台固定；第一笔有效画线完成后，两块平台与球在同一帧切换为 Dynamic。
- 齿轮继续保持 Static，铰链禁用马达与角度限制，平台可绕齿轮无限旋转。
- 清线或重开时恢复两块平台的初始位置、角度、零速度和 Kinematic 状态。
- 受影响文件：`DLevel26PlatformCtrl.cs`、`DLevel_026.prefab`。没有数据、配置、存档、UI、资源地址、事件、分析或平台 SDK 变化。

### 验证与风险

- 静态验证：两组齿轮、平台、铰链引用数量一致，且各平台铰链连接到对应齿轮。
- 编译验证：使用 Unity 当前 Assembly-CSharp 响应文件执行完整 C# 编译，退出码为 0；仅存在项目既有 warning，无新增编译错误。
- PlayMode 待验证：第一笔前平台不动；第一笔后球压到任一平台时可绕中心齿轮旋转；重开后两块平台复位。
- 剩余风险：平台当前沿用质量 10、线性阻尼 0.05、角阻尼 1.5；最终转动手感仍需在 PlayMode 中确认。

### 回滚

- 从 D026 根节点移除 `DLevel26PlatformCtrl`，删除对应脚本及 `.meta`，即可恢复平台始终保持 Prefab Kinematic 的旧状态。

## 2026-08-03 菜单首次进关画线门禁修复

### 根因修正

- 从选关菜单首次进入 D026 时，画线输入可能一直保持关闭；手动重开后才恢复。
- 早期排查曾把问题归因于重复的 `DelayAutoBeginDrawHandle`，该判断不完整。复测确认真实根因是
  夹子球在首次 Intro 首帧位于左侧场外起点，同时球刚体仍参与 2D Trigger，因而提前命中
  `FallArea_Left` 并进入失败/重开链；重开直接落到最终位置，所以不会复现。
- Kinematic 只能阻止重力驱动，不能阻止 Trigger；直接删除左侧死区只能隐藏问题，并会让
  正常游玩时从左侧出界不再失败。

### 修复行为

- `DLevel2Ctrl` 在夹子球进入场外 Intro 起点前把球根 Rigidbody2D 设置为
  `simulated=false`，使球壳与内部 Actor 的复合 Collider 暂时退出 2D 物理，不再触发死区、
  陷阱或平台，同时保留 Transform、DOTween、BoneFollower 与 Spine 动画。
- Intro 到达最终安全位置后先同步 Transform，再恢复 `simulated=true`；球仍保持 Kinematic，
  第一笔有效画线完成后才沿用原逻辑切为 Dynamic。
- 重开直接应用最终夹持态并恢复模拟，不重播 Intro；引用异常和释放入口也有恢复模拟兜底。
- `FallArea_Left` 已恢复，D026 重新保留左、右、上、下四块出界失败区域。
- 重复的 `DelayAutoBeginDrawHandle` 仍从 D026 根节点移除；`DLevel26Ctrl` 的 2 秒非缩放时间
  兜底继续保留，但它不再被描述为本次真实根因。

### 影响范围

- 影响文件：共享 `Level2/DLevel2Ctrl.cs`、`DLevel_026.prefab`、DLevel2 结构记录和本文档。
- 共享物理隔离也覆盖复用该控制器的 DLevel002、DLevel015；未修改 `DrawLevelCtrl` 全局死亡
  判定、球质量、释放后物理、平台释放、成功门、倒计时、数据、配置、存档、UI、资源地址、
  分析或平台 SDK。

### 验证、风险与回滚

- 完整 Assembly-CSharp 编译退出码为 0，无新增编译错误。
- 静态确认四个 FallArea 各一个、左侧 Transform 已重新挂回 D026 根节点，Prefab 纯本地 fileID
  无缺失或重复；`DelayAutoBeginDrawHandle` 的脚本 GUID 和组件 fileID 引用均为 0。
- PlayMode 待验证：从菜单首次进入时穿过左侧死区不得失败，动画完成后应立即可画；第一笔后
  球应恢复正常物理，任意方向出界仍应失败；重开路径保持可画。
- 剩余风险：D002、D015 共享控制器回归和 Android/iOS 首次进关烟测尚未执行。
- 回滚：移除 `DLevel2Ctrl` 的 Intro 模拟隔离代码；若同时回滚 D026 保护，必须调整场外起点或
  死区边界，不能再次通过删除 `FallArea_Left` 规避。
