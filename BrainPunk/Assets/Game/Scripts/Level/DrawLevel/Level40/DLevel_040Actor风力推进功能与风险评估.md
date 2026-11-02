# DLevel_040 AreaEffector2D 风区功能与风险评估

## 1. 需求、原行为与影响范围

- 新需求：移除 `DLevel040Ctrl` 手动 `AddForce`；改由场景 AreaEffector2D 提供风力，并且只在第一笔有效
  画线完成后生效。
- 原行为：第一笔后脚本把 Actor 切为 Dynamic，并在 `FixedUpdate` 中持续施加左向力；是否停力由
  Actor 世界 X 阈值决定。
- 新行为只影响 DLevel_040 本地脚本、prefab 和文档；不修改共享 `DrawLevelCtrl`、`DrawLineFeature`、
  `ActorCtrl`、GameplayPanel、配置、存档、资源加载、全局事件或平台 SDK。

## 2. Facts

- `Scene/Fan/WindArea` 已预置 `AreaEffector2D + BoxCollider2D`，对象初始 inactive。
- AreaEffector2D 当前为方向 `180°`、力度 `40`、Variation `0`、Collider Mask `2048`；BoxCollider2D 为
  Trigger、Used By Effector、Size `15×5`。
- `DLevel040Ctrl` 只在第一笔有效线完成后把 Actor 从 Kinematic 切为 Dynamic，并激活 WindArea。
- 脚本不再包含 `FixedUpdate`、`AddForce`、手动力参数或世界 X 停止阈值。
- 结算、组件禁用和 Attempt Reset 都会关闭 WindArea；Reset 同时恢复 Actor 初始世界位姿、刚体类型和零速度。

## 3. Direct conclusions

- Actor 的持续动力来源是 Unity Physics2D AreaEffector2D，不是脚本逐固定步施力或 Transform 轨迹。
- 风力生效范围由 WindArea BoxCollider2D 决定；Actor 离开 Trigger 后不再获得该 Effector 的新增力，已有惯性保留。
- 第一笔之前 WindArea 被 `OnEnable` 强制关闭，即使 prefab 初态被误设 active 也不会提前施力。
- 后续笔画不会重复清零 Actor 速度或重复启动流程。
- 本实现不判断胜负，成功门、FallArea、10 秒倒计时和 Draw 结算链保持原职责。

## 4. Hypotheses / risks

- `P1`：WindArea 当前 `15×5` 可能覆盖轨道过多区域，使 Actor 长时间被向左推压；范围需要 PlayMode 观察。
- `P1`：力度 `40` 与 Actor 的质量、Gravity Scale、画线轨道摩擦和曲率共同决定能否绕轨并到门，静态检查不能
  证明当前参数可解。
- `P1`：暂停行为依赖 Draw 主流程停止 Physics2D 推进；原生覆盖层、低帧率和设备差异仍需运行时验证。
- `OBS`：AreaEffector2D 只作用 Layer 11；Actor Layer 被误改时不会受风，但不会影响其它层对象。

## 5. Verification evidence

- 静态确认 DLevel040Ctrl 不再直接施力，WindArea 绑定唯一且首笔前关闭。
- Prefab YAML 共 149 个 anchor，无重复；149 个唯一非零本地引用无缺失；
  控制器脚本 GUID 和 `windAreaEffector` 绑定均唯一。
- Assembly-CSharp 独立全量编译为 `0 error / 101 warning`，DLevel040Ctrl 无新增警告。
- 目标文件的 `git diff --check` 通过，仅有工作区换行符提示。

## 6. Missing evidence

- `uncertain`：Unity PlayMode 的首笔前后风区状态、进出边界、轨道绕行、成功门、FallArea、倒计时、暂停、
  清线、失败重试和 Replay 尚未人工验收。
- `uncertain`：Android/iOS 横屏设备的 AreaEffector2D 手感、触控、帧率和功耗未验证。

## 7. 手工验收清单

- [ ] 首次进入与第一笔完成前 WindArea inactive，Actor 保持 Kinematic 且不受风。
- [ ] 第一笔有效线完成后 Actor 只切一次 Dynamic，WindArea 同帧激活并开始对 Actor 施力。
- [ ] 第二笔及后续笔画不会重复清零速度或重启风区。
- [ ] Actor 离开 WindArea 后不再新增风力，但保留合理惯性并可沿轨道运动。
- [ ] 结算、清线、失败重试和 Replay 关闭 WindArea，并恢复 Actor 初始 Kinematic、位姿和零速度。
- [ ] 风区不会作用 Draw Line、门或场景机关；至少存在一种 10 秒内可解方案。
- [ ] Android/iOS 横屏触控与目标 60 fps 下无明显物理跳变。

## 8. Rollback path

- 快速停用：禁用 WindArea 或 AreaEffector2D；Actor 首笔后仍会 Dynamic，但不会获得主动风力。
- 完整回滚：恢复 DLevel040Ctrl 的直接力实现与旧 prefab 序列化字段；无需数据迁移或共享系统回滚。
