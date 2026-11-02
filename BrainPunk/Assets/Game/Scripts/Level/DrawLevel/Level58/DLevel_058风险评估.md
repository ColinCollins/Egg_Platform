# DLevel_058 风险评估

## 1. 需求与基线

058 是从已有 DrawGame 关卡复制并把原夹持球替换为 Actor 的 Prefab。修复前的本关脚本只在首笔后切换 Actor 刚体，没有接入可见 Intro，也明确不播放开夹动画，因此出现“猫没有飞进来、夹子没有张开”。

当前方案让 `DLevel58ActorReleaseCtrl` 继承 `DLevel2Ctrl`，复用已有单套猫夹球装置的可见入场、首笔开夹释放和 Retry 完成态。D058 仍保留独立脚本类型，不修改 `DLevel2Ctrl` 或其它关卡 Prefab。

## 2. 本次变更范围

- 新增/更新：`Assets/Game/Scripts/Level/DrawLevel/Level58/DLevel58ActorReleaseCtrl.cs` 及其 `.meta`；当前类为不重写方法的 `DLevel2Ctrl` 薄子类。
- 修改：`Assets/Game/Prefabs/DrawLevel/DLevel_058.prefab`，根组件序列化为 `DLevel2Ctrl` 继承字段，显式绑定 `DrawLevelCtrl`、Alien 根、Actor 根 Rigidbody2D、D058 释放父级和 `clipSwingPivot`，并保留入场/夹子摆动参数。
- 更新：Level58 局部记录、DrawGame 共享演进记录和 `DevelopMD` 已有功能记录。
- 未修改：`DLevel2Ctrl`、`DrawAlienClipBallRigView`、`DLevel53Ctrl`、`DLevel53Hive`、DrawLine 参数、Actor/猫/钩爪源 Prefab、场景碰撞、配置表、UI、存档和 SDK。

## 3. 风险项

1. **Intro Handler 发现风险（高）**：猫飞入依赖继承得到的 `IDrawLevelVisibleIntroHandler`。若未来把 D058 控制器改回只实现 `IDrawStrokeCompletedHandler` 的独立类，`DrawLevelCtrl` 会立即开放画线，Alien 也会停在 Prefab FINAL 位置。当前继承关系已静态确认，运行时扫描仍需 PlayMode 验证。
2. **共享动画契约风险（中）**：基类依赖 Alien `idle01/control`、夹子 `clip_close/clip_open` 和猫 Spine `bone2`。D058 使用的共享 Rig 当前符合这些命名；替换 Spine 资源或改骨骼名时会使入场持有/开夹演出降级。
3. **夹持对象引用风险（高）**：共享字段仍名为 `yellowBallRigidbody`，但 D058 必须绑定 Actor 根 Rigidbody2D（fileID `1138963634036635986`）。共享 `DrawAlienClipBallRigView` 的原球引用在 D058 实例中为空，不能把它当作本关的有效夹持刚体。
4. **释放父级风险（高）**：`ballReleaseParent` 必须是 D058 根或其它不受猫 Spine/BoneFollower 驱动的稳定节点。误绑到 Alien、`catch` 或 `clipSwingPivot` 会让 Dynamic Actor 继续被动画父级带动。当前绑定是 D058 根 fileID `8549164348132234925`。
5. **物理手感风险（中）**：基类释放时只清零速度、脱父级并切换 Dynamic，不额外施加力、调整质量/阻尼或停用 Actor 自身控制器。释放后的下落和移动受 Actor Prefab 与场景碰撞共同影响。
6. **镜像与碰撞形状风险（中）**：D058 删除了原 `yellow_ball/payloadContentRoot`，因此基类的球体镜像迁移分支不会处理 Actor 视觉层级。当前 Actor 与 `catch` 的负 Y 缩放在持有态互相抵消，但脱父级后的实际根缩放、角色朝向和 Collider2D 形状仍需 PlayMode 观察，不标记为已验证。
7. **Retry 完成态风险（中）**：可见 Intro 只在首次进关播放；Retry 直接将 Alien 放到 FINAL，恢复闭夹、Actor 持有位姿和 Kinematic。需要在失败/手动重试下确认不会短暂闪现 START 位置或留下上一 Attempt 速度。
8. **共享基类演进风险（中）**：D058 现在会自动继承未来对 `DLevel2Ctrl` 的行为变更。修改基类时必须同时回归 D002、D020、D047 和 D058。
9. **复制残留风险（待确认）**：058 继续保留 D053 蜂群/蜜蜂组件。用户要求其它内容保持不变，本次不做猜测性清理。

## 4. 验证与未验证项

- 已静态确认：脚本 GUID `98e78cc6aaf249b183132cd4d46d4d2c` 存在；`DLevel58ActorReleaseCtrl` 当前是 `DLevel2Ctrl` 薄子类，因此继承 `IDrawLevelVisibleIntroHandler` 和 `IDrawStrokeCompletedHandler`。
- 已静态确认：Prefab 根组件的 `drawLevelCtrl`、`alien`、Actor Rigidbody2D、`ballReleaseParent` 和 `clipSwingPivot` 本地引用均存在；Actor 初始 override 仍为 `m_BodyType: 1`；`20058 / DLevel_058` 配置映射已存在。
- 已通过代码路径核对确认：首次进关走 START -> FINAL 可见 Intro；入场时 Actor `simulated=false` + Kinematic；首笔回调播放 `clip_open`、脱父级并切 Dynamic，猫 `control` 完成后回 `idle01`；Retry 走 FINAL 完成态而不重播飞入。这些结论仅是静态路径证据。
- 当前 `DLevel58ActorReleaseCtrl : DLevel2Ctrl` 薄子类已使用 Unity 自带 Roslyn 与项目程序集做隔离编译，结果为 `CSC_EXIT=0`、无错误。该结果不作为 Prefab 导入或 PlayMode 证据。
- `uncertain`：Unity 编辑器域重载、PlayMode 中猫的实际入场轨迹、夹子开合、Actor 释放姿态/朝向/碰撞、画线交互、蜂群失败与成功竞争、Reset/Replay 以及 Android/iOS 表现均未验证。

## 5. 回滚路径

回滚 `DLevel58ActorReleaseCtrl.cs/.meta` 和 `DLevel_058.prefab` 中 fileID `900000000000000007` 的继承字段绑定，可恢复修复前的独立首笔 Dynamic 释放实现。若要完全撤销 D058 释放功能，可从根节点移除该组件，Actor 将保持 Prefab Kinematic 状态。`DLevel2Ctrl`、共享 Rig、Actor Prefab、蜂巢、配置、UI 和其它关卡不需回滚。

## 6. 角色防滑补充风险

- D058 现仅对本关 Actor 根刚体覆盖 `DrawStrokeStable.physicsMaterial2D`。高摩擦不锁定 X 轴，也不持续清零速度，因此不会从代码层吞掉画线或蜜蜂的水平冲量。
- Maximum 合并会让角色与低摩擦地面接触时仍采用较高摩擦；这正是修复平地滑走的依据，但对陡坡或被重物夹住时的脱离手感可能有影响，需 PlayMode 调参。
- 本轮未预加 `linearDamping`。阻尼会同时作用于空中轨迹，只有 PlayMode 仍证明强碰撞后惯性过长时，才应在本关局部小幅增加并重新回归蜂群推动。
- 静态证据仅覆盖 Override 唯一性、材质参数和 YAML 引用完整性；实际停止距离、斜坡、Reset/Replay 与移动端仍为 `uncertain`。
