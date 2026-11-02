# DLevel_027 夹子放球与静态斜坡功能与风险评估

## 功能基线

- DLevel_027 使用共享 `DrawGame_AlienClipBallRig` 承载猫、夹子、球和球内 Actor。
- 首次进关需要先播放装置飞入；第一笔有效画线后夹子打开、球脱离动画层级并进入 Dynamic 物理。
- 中间 `platform_Stone` 是固定斜坡，只提供实体碰撞，不属于 D026 的双铰链平台玩法。
- 球进入右侧启用成功门后，沿用共享开门、人物和球壳渐隐、胜利提交；20 秒倒计时只承担失败。

## 原问题

1. Prefab 根节点误挂 `DLevel26Ctrl`，关卡身份、日志和后续维护归属错误。
2. 根节点同时挂有 `DelayAutoBeginDrawHandle(delay=0)` 和可见 Intro 控制器，形成重复画线门禁链。
3. 根节点残留 `DLevel26PlatformCtrl`，但两套齿轮、平台、铰链数组全部为空；运行时必然校验失败、输出错误并自禁用。
4. D027 实际只有无 Rigidbody2D/HingeJoint2D 的静态斜坡，不能也不应套用 D026 双平台释放逻辑。

## 本次变更

1. 新增 `DLevel27Ctrl : DLevel2Ctrl`，保留 DLevel002 已验证的单套猫夹球入场、首笔释放和重开完成态。
2. 不复制 D026 曾使用的 2 秒 Intro 补丁；D027 没有对应复现，直接沿用 DLevel2Ctrl 的正常完成边界，避免未来合法延长动画时被固定阈值截断。
3. 将 D027 根控制器脚本绑定改为 `DLevel27Ctrl`，并完整移除重复的 `DelayAutoBeginDrawHandle`。
4. 完整移除空绑定的 `DLevel26PlatformCtrl`；`platform_Stone` 的位置、旋转、缩放、加长 Sprite 和 BoxCollider2D 不变。
5. 未改猫夹球布局、球参数、画线参数、成功门、倒计时、FallArea、相机、背景或 DLevel_026。

## 受影响文件与系统

- `Assets/Game/Prefabs/DrawLevel/DLevel_027.prefab`
- `Assets/Game/Scripts/Level/DrawLevel/Level27/DLevel27Ctrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level27/DLevel_027脚本结构记录.md`
- 本风险评估、DrawGame 中央结构记录与 `DevelopMD/已有功能设计与使用记录.md`
- 未修改 Luban/DB、存档、UI Prefab、共享猫夹球/成功门/FallArea、Physics2D Matrix、analytics、SDK 或平台配置。

## 预期运行行为

1. 首次进入：夹子闭合、球为 Kinematic；整套装置从路径起点飞到 Inspector 配置的最终位置，同时夹子按惯性回摆。
2. 画线开放：共享入场动画结束后立即开放，不再叠加 delay=0 的第二个 Intro Handler。
3. 第一笔有效线：夹子只打开一次，球脱离夹子后切 Dynamic；猫播放按键动画，20 秒失败倒计时开始。
4. 后续物理：球、线、固定斜坡和地面只通过 Physics2D 碰撞互动；斜坡不会旋转或被脚本释放。
5. 成功：球内 Actor 进入右侧启用门，人物和蓝色球壳同步渐隐后提交胜利。
6. 失败：球复合刚体进入任一四向 FallArea，或 20 秒归零，沿用共享失败重建。
7. 重开：失败、手动重开与 Replay 均不重播飞入；装置直接位于最终位置，闭夹持球，等待下一笔。

## 验证证据

- Prefab 根组件静态扫描未发现 DLevel26Ctrl、DLevel26PlatformCtrl 或 DelayAutoBeginDrawHandle 的 GUID/fileID 残留。
- `DLevel27Ctrl` 序列化引用覆盖 DrawLevelCtrl、Alien、球刚体、释放父级、猫 Spine、BoneFollower、摆动 Pivot 和夹子 Spine。
- 倒计时为 `20 / isSuccess=false`；唯一启用成功门回调 `DrawLevelCtrl.OnTriggerSuccess`；旧门为 DisabledLegacy。
- `successExtraRendererGroup` 指向共享球 Rig 的渲染组；Left/Right/Bottom/Top 四个 FallArea 均启用。
- 静态斜坡源 Prefab 的根节点组件为 Transform、SortingGroup 和 BoxCollider2D，三个子节点只承载 SpriteRenderer 视觉；当前实例没有 Rigidbody2D/HingeJoint2D，符合固定地形契约。
- Prefab YAML 为 139 个唯一锚点、139 个唯一非零本地 fileID，重复/缺失均为 0；脚本/meta GUID 与 D026/D027 Prefab GUID 已完成静态一致性检查。
- Unity 6.0.59f2 当前 Assembly-CSharp 响应文件全量 Roslyn 编译退出码为 0，结果 `0 error / 104 warning`；最近 Editor.log 未见 C#、Prefab 导入、断言或序列化错误。

## 风险与待验证项

- `uncertain`：尚未执行完整 Unity PlayMode，实际入场回弹、第一笔放球、球沿斜坡轨迹、进门触发和 FallArea 边缘触发需试玩确认。
- `uncertain`：成功动画期间倒计时竞争、暂停恢复、连续 Reset/Replay 及移动端固定帧率表现需运行时回归。
- 本关没有局部 Intro timeout；若运行时实际复现动画永不返回，需要先区分共享 Tween 问题与单关引用问题，再决定修复层级。
- 共享 Rig 的 Spine 动画名、`bone2` 或球父级契约变化会影响本关；本次没有修改共享资源，因此其它球关卡不受本次变更影响。

## 回滚方式

1. 从 D027 根节点移除 `DLevel27Ctrl` 并恢复旧控制器绑定，可回到原状态，但会恢复跨关卡身份误绑。
2. 若回滚独立脚本身份，可恢复旧控制器绑定，但不要恢复重复 Delay；否则首次进关会重新出现两条 Intro Handler 链。
3. 若未来确需动态平台，应为 D027 新建并绑定本关平台控制器和完整物理拓扑；不要恢复两套空数组的 DLevel26PlatformCtrl。
4. 删除本关局部脚本及文档不涉及数据迁移；共享 Prefab、配置和其它关卡无需回滚。
