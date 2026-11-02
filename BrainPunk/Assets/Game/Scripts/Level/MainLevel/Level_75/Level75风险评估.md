# Level75 风险评估

## 2026-07-27 第九次 PlayMode 反馈（当前实现）

- 原行为基线：门受压第一段观感可接受；第二段在 `0.18s` 内把 Y 倍率从 `0.82` 降到 `0.55`，且使用后段加速的 `Ease.InQuad`。门顶平均下降约 `4.55u/s`，而刺墙始终线性下降约 `2.24u/s`，因此后半段门顶会跑在齿尖前面。
- 影响范围：只修改 075 私有控制器的第二段 Ease、075 关卡 Prefab 的第二段时长和本地/共享文档；不修改共享门、共享尖刺、第一段变形、最终倍率、粒子、相机、UI 或玩法输入。

| 风险 | 当前控制 | 状态 |
|---|---|---|
| 后半段门自行压扁，齿尖与门顶脱离 | 第二段改为 `0.36s / Ease.Linear`，估算门顶速度约 `2.28u/s`，贴近墙速 `2.24u/s` | 参数与代码完成，待 PlayMode |
| 为修正视觉而改变触发时机，导致挡门判定回归 | 门和尖刺 Collider、Tag、Layer 与回调绑定均未修改 | 静态确认 |
| 总受压节奏过慢 | 总时长由约 `0.44s` 增至约 `0.62s`，仅增加 `0.18s` | `uncertain`，待策划目测 |
| 第二段线性后缺少临界爆裂的加速感 | 最终倍率、四向粒子、震屏和音效仍在第二段完成后同时触发 | 待画面确认 |
| 受压过程中玩家反向抬升刺墙，门仍按固定 Sequence 变形 | 本轮保持既有并发语义，没有扩大到机关状态重构 | 既有边界，待 PlayMode |
| 修改共享 Caltrop Collider 影响其它关卡 | 不修改公共 Caltrop 资源；其约 `0.10u` 透明边缘不是本轮后半段速度错配的主因 | 已隔离 |

- 验证证据：Unity Roslyn 全量编译 `0 error / 105 warnings`，Level075 无诊断；关卡 Prefab 引用闭包为 `266/543/0/0`，第二段时长绑定唯一。只读复核确认第一段、倍率、Trigger/Collider、粒子引用、unscaled Sequence、`SetLink`、`OnComplete` 与 Cleanup Kill 均未变化；目标差异空白检查通过。
- 未验证缺口：Unity PlayMode 中的持续贴合、总节奏、爆裂临界感、受压期间抬墙、重开/退出，以及 Android/iOS 横屏低帧率表现。
- 回滚路径：只把 Prefab 的第二段时长恢复为 `0.18s`，并把 `PlayDoorSqueeze()` 第二段 Ease 恢复为 `Ease.InQuad`；不得回滚粒子、按钮或其它 075 机制。

以下第八次及更早章节为历史风险记录；第八次粒子控制仍有效，但其门受压总时长描述已失效。

## 2026-07-27 第八次 PlayMode 反馈（历史，粒子参数仍保留）

- 原行为基线：第七次版本虽然补回了 X 轴随机速度并扩大初始发射半径，但主 Shape 仍是朝单一方向的窄锥，无法形成真正的二维四周爆开；单片尺寸仍为 `0.5~0.8`。
- 影响范围：只修改 075 私有 `DoorBroken_075.prefab` 的主碎片参数和本地/共享文档；门受压 Sequence、关卡 Prefab、控制器、Clouds 子系统、其它关卡和公共资源均不变。

| 风险 | 当前控制 | 状态 |
|---|---|---|
| Circle 发射平面被根节点 `X=-90°` 转到世界 XZ，画面仍像横线 | Shape 自身设为 `X=90°` 抵消根旋转，使 Circle 位于世界 XY 平面 | YAML 静态确认，待 PlayMode |
| 原 X 轴速度叠加径向速度，导致左右明显强于上下 | 关闭主 `VelocityModule`，仅使用 Circle 的 `360°` 径向初速度 | YAML 静态确认 |
| 上半圈受重力过快下坠，四向形状读不清 | 重力由 `4` 降到 `2.5`，生命周期仍限制在 `0.8~1.2s` | 参数完成，待画面确认 |
| `9~15` 初速度让边缘碎片飞出镜头 | 不延长生命周期、不增加 Burst，实际最大画面边界标记为 `uncertain` | 待 PlayMode |
| 单片增大后首帧仍显拥挤 | 尺寸只小幅增至 `0.6~0.95`，发射半径仅增至 `0.12`，Burst 保持 `25` | 待 PlayMode |
| 调参误伤烟尘或公共粒子 | Clouds 子系统字段未改；资源仍为 075 私有 Prefab，没有修改共享 `BoxBroken` | 静态确认 |
| 移动端透明填充或瞬时峰值增加 | 数量、生命周期和 Clouds 数量均未增加，但更大粒子的 overdraw 仍需真机检查 | Android/iOS 未验证 |

- 验证证据：逐项回读主系统 Shape、速度、尺寸、重力和 Velocity 状态；Clouds 仍为原 Shape 且 Velocity 关闭。Prefab 本地引用扫描为 `8/14/0/0`（定义/引用/重复/缺失），Unity Editor 日志记录 `PrefabImporter` 成功重新导入；本轮无 C# 变更，未重复执行脚本编译。
- 未验证缺口：Unity PlayMode 中的四向扩散距离、屏幕边界、首帧密度、碎片尺寸、震屏组合，以及 Android/iOS 横屏低帧率表现。
- 回滚路径：只恢复主系统的 `Cone(type=4)`、`arc=180°`、Shape 旋转零值、`radius=0.1`、`radiusThickness=1`、`Velocity X=-3~3`、初速度 `5~10`、尺寸 `0.5~0.8` 和重力 `4`；不得回滚门受压或其它 075 机制。

以下第七次及更早章节为历史风险记录，其窄锥碎片方案已被本节替代。

## 2026-07-27 第七次 PlayMode 反馈（历史，已由第八次覆盖）

- 原行为基线：门触发刺墙后在同一帧关闭、生成压扁残骸、播放粒子/震屏/音效，没有玩家可见的受压阶段；主碎片缺少 Level007 场景实例的 X 轴随机速度，25 片碎片从半径 `0.01` 的区域集中喷出。
- 影响范围：仅修改 075 私有控制器、关卡 Prefab、075 私有门碎片 Prefab 和 Level075/共享设计记录；不修改参考关 Level007、基础关 Level029、共享 `BoxBroken`、公共物理或输入系统。

| 风险 | 当前控制 | 状态 |
|---|---|---|
| 缩放门根导致碰撞体、碎门锚点或策划落点一起漂移 | 只缩放绑定的 `StartDoor (1)/StartDoor` 可视子节点，门根和 `CrushedDoorSpawnPoint` 保持不动 | Prefab 绑定完成，待画面验证 |
| 延迟爆炸期间重复进入 Trigger，生成多份残骸 | 回调入口先置 `doorCrushed=true`，并立即关闭门层级全部 Collider 与拖拽组件 | 代码完成 |
| Reset/退出在受压中途仍执行爆炸回调 | Sequence 由字段持有并在 `Cleanup()` Kill；完成逻辑只挂 `OnComplete`，没有 `OnKill` 回调 | 代码完成 |
| 刺墙继续下降但门受压动画因暂停冻结 | 两段 Sequence 使用 unscaled update，与既有刺墙非缩放 Tween 一致 | 代码完成，暂停语义仍待策划确认 |
| 碎片仍聚成一团 | 主碎片复用 Level007 的本地 X 轴随机速度 `-3~3`，发射半径增至 `0.1` | YAML 静态确认，待 PlayMode |
| 碎片过散或移动端粒子峰值增加 | 不增加 Burst、生命周期、初速度、根缩放或 Clouds 数量，仍为单次 25 片 | Android/iOS 未验证 |
| 受压后门底离地或变形过强 | 变形对象支点位于门底附近；参数均局限在 075 Prefab，可单独调节 `0.26/0.18s` 与两段倍率 | `uncertain`，待 PlayMode 手感确认 |

- 编译与静态证据：Roslyn 全量编译 `0 error / 105 warnings`，Level075 无诊断；关卡 Prefab `266/543/0/0`、粒子 Prefab `8/14/0/0`（定义/本地引用/重复/缺失）；空白差异检查通过。
- 未验证缺口：Unity PlayMode 完整挤压/爆炸画面、刺墙接触构图、门底稳定、碎片扩散边界、暂停/重开/退出、完整过关和 Android/iOS 横屏性能。
- 回滚路径：恢复 `CrushDoor()` 的同步完成流程，删除门受压字段、Sequence 和清理分支；将 `DoorBroken_075` 的主碎片 X 速度改回常量 `0`、Shape 半径改回 `0.01`。其它 075 功能不得随本轮回滚。

以下第六次及更早章节为历史风险记录，其门触发即爆炸的基线已被本节替代。

## 2026-07-27 第六次 PlayMode 反馈（历史，已由第七次覆盖）

- 原行为基线：075 在 `0.5s` 后读取 HUD 的 `jump_btn` 槽位，隐藏原 UI，随后等待 `PLAYING` 并把 inactive 的 `WorldUpButton` 直接以 Prefab 缩放 `1.09` 激活。场景按钮不受 `GamePlayPanel.SequentialScaleAnim` 或 `clickBlock` 管理，因此会早于顺序 HUD 满尺寸出现并可交互。
- 本轮影响仅为 `Level75Ctrl.cs`、075 私有 `Level75WorldButtonClick.cs`、`Level_075.prefab`、Level075 功能/风险文档和共享设计记录。没有修改专用 GamePanel、Level034、Level029 或公共 UI/输入系统。
- 复用边界：只复用 Level034 已验收的数值与局部 DOTween 写法，不抽取共享逻辑。目标缩放在 `Awake` 归零前缓存；Prefab 继续保持 `WorldUpButton.m_IsActive=0` 和目标缩放 `1.09`，规避首帧闪现及缓存零值。
- 时序风险控制：075 只替换 jump，不能直接沿用 034 同时替换左/右/跳时的 `0.3s` 基础偏移，否则 jump 会约在 `0.92s` 开始并早于面板左右键。当前基础偏移为 `0.8s`，加 jump 顺位 `2 × 0.06s` 后实际延迟 `0.92s`，约在页面启动后 `1.42s` 接在左右键之后，以 unscaled update 执行 `0.3s Ease.OutBack` 入场。输入组件在入场期间保持 disabled；Tween 正常完成后先等待 FSM 为 `PLAYING`，再启用组件、等待一帧并复核 `PLAYING`，最后才开放点击/拖拽。Tween 被终止时不会误开放输入。
- 生命周期控制：重置/重玩会先创建关卡并进入 `PAUSE`，随后可能直接进入 `PLAYING` 而不派发旧输入组件监听的状态事件。075 私有输入组件现改为在 `Update` 入口和松手结算时实时检查 `PlayCtrl.CheckState(PLAYING)`，非游玩态立即取消在途手势，避免陈旧暂停缓存导致永久无输入。`PrepareWorldUpButton` 和 `Cleanup` 都禁用组件、关闭点击/拖拽并把按钮归零；清理还会终止入场 Tween并停用对象。
- 原输入与机关回归边界保持不变：未吸附时正常松手派发一次 `PlayerJumpEvent`，超过 `60px` 才拖拽；吸附后永久锁拖且不再跳跃，点击只启动一次 `3s` 抬墙。
- 数据、资源和平台边界：没有新增存档、配置、资源加载、实例化、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK；移动端仍复用既有 fingerId 跟踪。
- 已验证 Unity Roslyn 全量编译 `0 error`、Level075 诊断 `0`；`Level_075.prefab` 引用闭包 `266/542/0/0`，专用面板 `35/68/0/0`；差异空白检查通过。
- 未验证风险：Unity PlayMode 中与实际 HUD 的视觉顺序、暂停在延迟/Tween 中途的表现、退出重开、不同宽高比和 Android/iOS 真机低帧率触摸仍需人工补验。
- 回滚路径：删除 `initButtonScaleDuration/initButtonDelayStep/initButtonDelayOffset`、`Awake` 缩放缓存和入场 Tween/清理代码，恢复定位后直接激活；若仅回滚顺序校准，把 `initButtonDelayOffset` 从 `0.8` 恢复为 `0.3`。不得回滚此前已确认的松手跳跃、`60px` 阈值、吸附职责切换和抬墙速度。

以下第五次及更早章节为历史风险记录，其直接激活时序已被本节替代。

## 2026-07-27 第五次 PlayMode 反馈（历史，已由第六次覆盖）

| 风险 | 当前控制 | 状态 |
|---|---|---|
| 按下按钮就立即跳跃，玩家无法决定是否拖动 | 按下只捕获鼠标/触点；`PlayerJumpEvent` 只在正常 `MouseUp/Touch Ended` 的短按分支发送 | 代码完成，待 PlayMode |
| 轻点时手指小幅偏移导致按钮被拖走 | 拖拽阈值由 `20px` 提高到 `60px`，超过阈值前不移动 Transform | 参数完成，手感待验证 |
| 拖出阈值后移回起点又被判定为跳跃 | `pointerMoved` 记录整段手势的最大位移，一旦超过阈值不会恢复为短按 | 代码完成 |
| 吸附后点击仍让角色跳跃 | 松手时读取 `IsWorldUpButtonInstalled`；已吸附时跳过 `PlayerJumpEvent`，只调用尖刺墙机关 | 代码完成 |
| 吸附后的第一次点击既不跳也不抬墙 | 跳跃阻断后仍继续调用 `ClickWorldUpButton()`；控制器只在已吸附且 `PLAYING` 时启动一次抬墙 Tween | 代码完成，待 PlayMode |
| `60px` 在不同分辨率或 DPI 下过严/过松 | 参数保留序列化，可由策划在 `Level_075.prefab` 单独调整，不影响公共输入 | `uncertain`，待多分辨率与真机 |
| 取消、暂停或失焦在松手分支误跳 | `canceled`、非 `PLAYING`、触点消失、应用失焦/切后台和 Cleanup 均先取消手势，不派发跳跃 | 代码完成 |

- 本轮没有修改公共按钮的 PointerDown 行为；Level075 运行时仍会隐藏公共 `jump_btn`，实际可见的唯一世界按钮使用上述私有松手规则。
- 已完成 Unity Roslyn 全量编译（`0 error / 105` 个项目既有 warning，Level075 诊断 `0`）、Prefab 本地引用扫描（`266/542/0/0`）和目标差异检查。
- 仍需 PlayMode/真机验证按下与松手时序、`60px` 手感、拖动不跳、吸附后只抬墙，以及多指和低帧率场景。
- 仅回滚本次时，删除吸附状态跳跃阻断并把阈值恢复为 `20px`；其它 Level075 机制保持不变。

以下第四次及更早的旧事实、风险和验证章节仅保留历史证据；“吸附后继续跳跃”和“20px 阈值”均已失效。

## 2026-07-27 第四次 PlayMode 反馈（历史，已由第五次覆盖）

| 风险 | 当前控制 | 状态 |
|---|---|---|
| 画面仍出现两个跳跃按钮 | 旧 `Root/up_btn` 在 075 面板 Prefab 中默认失活；完成世界坐标换算后同时停用原 `Root/jump_btn`，最终只显示 `WorldUpButton` | 静态完成，待 PlayMode |
| 原 UI 与世界按钮重叠导致一次触摸跳两次 | 先验证世界按钮、相机和 jump 槽引用完整，再停用原 `jump_btn` GameObject，连同其 `CustomButton` 输入一起关闭 | 代码完成 |
| 玩家准备拖动时先误触一次跳跃 | 私有输入组件等待手势结束；最大位移不超过 `20px` 才按短按派发一次 `PlayerJumpEvent`，超过阈值只进入拖拽 | 代码完成，阈值手感待验证 |
| 拖出去又拖回来被误判为短按 | `pointerMoved` 记录整段手势是否曾超过阈值，不按最终位移重新归零 | 代码完成 |
| 按住左右移动时第二根手指不能拖按钮 | 075 私有组件扫描命中按钮的触点并保存 `fingerId`，拖拽与短按使用同一触点所有权；不再依赖只读取 `GetTouch(0)` 的共享 `DragableItem` | 代码完成，待 Android/iOS |
| 暂停或触摸取消触发跳跃/吸附 | `PAUSE`、`TouchPhase.Canceled`、Disable 和 Cleanup 都只清空当前手势；只有正常释放才进入短按或投放分支，两个入口仍复核 `PLAYING` | 代码完成 |
| 按住期间按钮随墙移动，松手不再命中 Collider | 指针只在按下时通过自身 Collider 认领；松手按最大手指位移判定短按，不因目标自身移动重复做命中检查 | 代码完成，待移动中点击验证 |
| 系统漏发触摸结束导致旧 fingerId 永久锁死 | 每帧找不到活动 `fingerId` 时静默取消；应用失焦或切后台也取消当前手势 | 代码完成，待真机切后台验证 |
| 吸附后按钮又能被拖走 | 安装成功后关闭私有组件的拖拽能力，按钮改挂红框 Transform；短按能力单独保留 | 代码完成 |
| 机关触发后唯一按钮失去跳跃功能 | `mechanismActivated` 只阻止墙体重复 Tween，不再关闭按钮点击；后续短按仍派发 `PlayerJumpEvent` | 代码完成 |
| 墙体 Collider 抢占按钮输入 | 私有组件直接检查按钮自身 `Collider2D.OverlapPoint`，不依赖 Physics2D 首个命中对象 | 代码完成，待 PlayMode |
| 投放要求过于精确 | 红框 bounds 四周外扩 `0.5` 世界单位，以按钮中心判定；参数仍可在 075 Prefab 调整 | 参数已序列化，待手感验证 |
| 墙体向上速度仍过快 | 目标 `Y=7.8` 和 `Ease.OutCubic` 不变，上升时长由 `1s` 调整为 `3s` | 参数完成，待策划确认节奏 |
| 初始化失败后屏幕没有任何跳跃按钮 | 只有 jump 槽、相机和世界按钮引用全部有效才停用原 `jump_btn`；失败时记录 warning 并保留共享跳跃键作为兜底 | 代码完成 |
| 暂停时按钮不可操作但尖刺墙继续移动 | 下降和抬升 Tween 仍沿用 `SetUpdate(true)` 非缩放时间 | 既有基线，`uncertain`，待策划确认是否改变 |

- 本轮删除的只是 `WorldUpButton` 上对共享 `DragableItem` 的组件依赖，没有修改共享脚本本身；Level029、Level034、公共 HUD、配置、存档、资源、analytics、广告、IAP 和平台 SDK 均未改变。
- 已完成 Unity Roslyn 全量编译（`0 error`，仅项目既有 warning）、两个 Prefab 的本地 fileID 重复/缺失扫描；`Level_075.prefab` 为 `266/542/0/0`，专用面板为 `35/68/0/0`（定义/引用/重复/缺失）。
- 未完成项为 Unity PlayMode 完整过关、暂停/退出重开、多分辨率安全区、低帧率手势，以及 Android/iOS 横屏多指触摸验证。
- 仅回滚本轮时，恢复旧 `up_btn + DragableItem` 独立机关输入、保留共享 `jump_btn`，并把抬墙时间恢复为 `1s`；门压碎反馈及其它 075 机制不需要回滚。

以下全部旧事实、风险表、验证边界、待验证项和回滚说明只保留历史证据；凡涉及“双按钮分工”“WorldUpButton 不发跳跃”“吸附前点击关闭”或“1 秒抬墙”的条目均已被本节覆盖。

## 2026-07-27 第三次 PlayMode 反馈

| 风险 | 当前控制 | 状态 |
|---|---|---|
| 玩家实际拖到共享跳跃键，机关按钮仍不可拖 | 075 改用 Level034 同类的世界按钮；专用 `up_btn` 仅作为响应式起始槽位，实际输入不再经过 UI Raycast | 代码与 Prefab 完成，待 PlayMode |
| 运行时专用 `up_btn` 仍不可见 | 世界按钮读取其 RectTransform 位置后主动显示，并隐藏槽位；可见性不再依赖 `CustomButton/UIDrag` 状态 | 静态完成，待实际面板实例验证 |
| 场景按钮误触发普通跳跃 | `WorldUpButton` 使用本关 `Level75WorldButtonClick`，未绑定 `PlayerJumpEvent`；共享 `jump_btn` 不改 | 引用静态确认 |
| 拖动过程被当成机关点击 | 世界按钮点击初始关闭，只在 DragEnd 命中红框后开启 | 代码完成 |
| 墙体 Collider 抢占吸附后按钮点击 | 不复用只检查 Physics2D 首命中的共享点击组件；`Level75WorldButtonClick` 直接检查按钮自身 Collider 的 `OverlapPoint` | 代码完成，待 PlayMode |
| 投放要求过于精确 | 红框 X/Y bounds 四周外扩 `0.5` 世界单位，以按钮中心判定 | 参数已序列化，手感待验证 |
| 吸附后还能拖离 | 成功吸附后立即 `SetEnableDrag(false)`，且本关生命周期内没有重新启用路径 | 代码完成 |
| 红框移动后按钮脱离 | 吸附时把世界按钮改挂到红框 Transform 下，不依赖逐帧 UI 反投影 | 层级完成，待动画验证 |
| GamePanel 初始化较晚导致按钮缺失 | 先等待 `0.5s`，随后最多重试 `15` 次、每次 `0.1s`；仍失败时记录警告并保持机关不可误触 | 代码完成 |
| 延迟激活的按钮漏掉此前 PAUSE 广播 | 槽位准备后等待当前 FSM 为 `PLAYING`；组件激活并完成一帧订阅后再次确认状态才启用拖拽，点击组件启动时也主动读取当前状态 | 代码完成，待暂停/重开验证 |
| 暂停强制 EndDrag 时误吸附 | 吸附入口再次校验当前 FSM 必须为 `PLAYING`；暂停结束拖拽只会松手，不会提交安装 | 代码完成 |
| 暂停时按钮不可操作但尖刺墙继续移动 | 下降和抬升 Tween 继续沿用 `SetUpdate(true)` 非缩放时间 | 既有基线，`uncertain`，待策划确认是否需要改变 |
| 碎门落点继续依赖猜测数值 | 新增门子节点 `CrushedDoorSpawnPoint`，策划可直接在 Prefab 中移动；旧地面 Y 只作缺失兜底 | 节点已绑定，最终位置待策划调整 |
| 门被横向拖动后碎门锚点不跟随 | 锚点挂在 `StartDoor (1)` 下，随门保留最终世界 X；生成残骸仍挂关卡根，门隐藏后不会一起消失 | 层级静态确认 |
| 世界按钮 Collider 抢占其它场景输入 | 按钮只在专用 up 槽位激活，Collider 尺寸与 Level034 按钮一致；仍需验证与不同宽高比下场景 Collider 的重叠 | `uncertain`，待 PlayMode/移动端 |

- 点击修正使用 075 本关私有 `Level75WorldButtonClick`；修改没有扩展到共享输入脚本、其它关卡、存档、配置、资源加载、全局事件、analytics、广告、IAP 或平台分支。
- 已完成 Roslyn 全量编译（`0 errors / 105` 个既有 warning）、Prefab 本地 fileID 重复/缺失扫描和目标 `git diff --check`。
- 尚未完成 Unity PlayMode、暂停/退出重开、不同安全区、Android/iOS 多点触摸和低帧率拖放验证。
- 仅回滚本轮时，移除世界按钮与碎门锚点，并恢复 075 原有 `Level75GamePlayPanelCtrl + UIDrag` 路径和 `crushedDoorGroundY` 定位。

## 2026-07-27 第二次 PlayMode 反馈

| 风险 | 当前控制 | 状态 |
|---|---|---|
| World Space 子 Canvas 与场景世界坐标错配，按钮永远不能命中 | 075 局部改用 `rootCanvas` 屏幕投影，不再调用公共 `CommonUtility` 做命中 | 代码完成，待多分辨率 PlayMode |
| 投放必须像素级准确 | 红框 bounds 外扩 `0.5` 世界单位；按按钮中心判定 | 参数已序列化，待触摸手感 |
| 吸附后仍能拖走 | 只有成功移动到红框中心后才写入已安装状态，并单独禁用 `UIDrag` | 代码完成 |
| 锁拖拽时误关点击造成软锁 | 不调用 `SetInteractable(false)`；吸附后保留 `CustomButton`，点击机关后才整体关交互 | 代码完成 |
| 红框继续移动而按钮留在原地 | 已安装后 `LateUpdate` 持续将红框中心投影回按钮父 RectTransform | 代码完成，待动画验证 |
| Canvas 转换失败却提交已安装 | 先检查 `TrySetUpButtonScreenPosition` 成功，再锁拖拽；失败回起点 | 代码完成 |
| 无效投放把按钮遗留到屏幕外 | DragEnd 未命中时调用 `UIDrag.ResetPosition()` | 代码完成，待触摸验证 |
| 碎门按物理面定位而埋入可见地面 | 改用门所在区域美术顶线 `Y=-2.365`，仍按 Sprite 最低点对齐 | 静态数值确认，待画面验证 |
| 碎门主体轮廓仍可能显得未贴地 | `door_sl_01` 轮廓不规则，部分主体距图片最低点约 `0.34` 世界单位 | `uncertain`，需策划 PlayMode 目视决定是否追加视觉偏移 |

- 当前修改不新增存档、配置、资源路径、全局事件、analytics、广告、IAP 或平台分支。
- Unity 6000.0.59f2 自带 Roslyn 完整编译结果为 `0 error / 105` 个项目既有 warning。
- 仍需验证暂停/退出/重开时按钮交互清理、不同安全区与宽高比、触摸 DragEnd/Click 隔离，以及碎门排序与地面遮挡观感。

本节覆盖本文后续旧表中关于复用公共坐标转换、`0.05` 命中容差和 `Y=-2.5764` 物理地面落点的描述。

## 已确认事实

- 075 的场景布局来自当前工作区内已由策划调整的 `Level_075.prefab`，本次只替换关卡脚本绑定、面板名、门回调和新增字段引用。
- 红框 `Sp_l75_wxqk_01` 只有 `SpriteRenderer`，尺寸约为 `3.03×1.57` 世界单位。
- 原 Level029 的共享 `jump_btn` 在 PointerDown 时会发出跳跃事件，因此只保留标准跳跃职责；075 的拖拽机关使用独立 `up_btn`。
- 共享 `jump_btn` 位于右侧 `(-277, -403.5)`，独立 `up_btn` 位于右侧第二槽 `(-593, -403.5)`，两者中心距 `316` UI 单位。
- 红框初始世界坐标保持 `(0.17, 4.91, 0)`，父级已从关卡根改为刺墙整体 `CaltropGround`。
- 外层门触发器底边为 `Y=-2.295`，比 `Ground_long` 物理顶面 `Y=-2.57640745` 高约 `0.2814`；压扁门不能继续用该触发器底边作为落地线。
- 门碎片图是 `1024×256` 的 8×2 图集，共 15 个有效碎片格和 1 个透明格。
- 门与刺墙的旧碰撞回调会 `DOKill` 刺墙；075 已改为只压碎门，不停止刺墙。
- `LevelData.xlsx` 已提供 `10075 -> Level_075`，隔离生成结果只要求在运行时 `tbleveldata.json` 新增对应行。
- 当前 `tblevelsort.json` 已提供 `id 86 -> path 10075`；HUD 显示 Level 86 时会继续解析到 `Level_075`。
- `S_congraktTips_Des_level_75` 与 `S_answer_Des_level_75` 在当前 `localization.csv` / `I2Languages.asset` 中均不存在。
- 场景 Tips 的本地化 term 仍是 `S_LevelTips_Des_level_29`，会优先于 Prefab 中直接填写的“危险！”文本。

## 风险与控制

| 风险 | 当前控制 | 状态 |
|---|---|---|
| 普通跳跃键缺失 | 专用面板撤销隐藏覆盖，恢复共享 `jump_btn` 原事件与外观 | Prefab 已恢复，待 PlayMode |
| 拖动机关按钮时误触玩家跳跃 | `up_btn` 使用独立 `CustomButton`/`UIDrag`，与共享 `jump_btn` 分槽且不复用 `PlayerJumpEvent` | 代码与 Prefab 已隔离 |
| jump 与 up 重叠导致射线抢占 | 两键中心距 `316` UI 单位，按钮宽度 `290`，边缘保留 `26` UI 单位间隔 | 静态确认，待多分辨率 |
| 拖拽释放被当作机关点击 | DragBegin 阻断 Click，DragEnd 保持阻断到下一帧 | 代码层已隔离，待触摸验证 |
| 不同宽高比下 UI 到世界坐标偏移 | 复用项目已有 Canvas/Camera 转换工具，并显式绑定关卡相机 | 待多分辨率 PlayMode |
| 红框边缘难以命中 | 按按钮中心判定，bounds 外扩 `0.05` 世界单位 | 参数可调，待手感验证 |
| 刺墙移动后红框留在原处 | 红框改为 `CaltropGround` 子物体，初始世界位置保持不变 | 层级静态确认，待动画验证 |
| 门碰撞多次重复生成残骸 | `doorCrushed` 门闩在表现前置位 | 代码层已限制 |
| 门仍能挡停刺墙 | 压碎回调不调用 `DOKill`，只关闭门自身 | 静态确认 |
| 压扁门悬在地面上方 | 使用 `Ground_long` 顶面 `Y=-2.5764`，并按 Sprite 本地最小 Y 对齐 | 数值静态确认，待画面验证 |
| 动态压扁门层级不正确 | 继承红框 SortingLayer，排序值加一到 `201` | 待画面验证 |
| 震屏结束后相机残留偏移 | 开始前缓存局部坐标；完成、Kill、关卡禁用和销毁均恢复 | 代码层有兜底，待 PlayMode |
| 震屏与暂停/退出交错 | 使用非缩放时间，Cleanup 会 Kill Tween 并归位 | 待暂停/退出验证 |
| 旧刺墙 PositionShake 与下降 Tween 争写 | 075 不再调用复制自 029 的 `MMF_Player`，只保留独立 GameCamera 震屏 | 代码层已隔离 |
| 粒子抽到透明图集格 | 8×2 Grid 的随机归一化上限设为 `0.9374` | 静态确认 |
| 粒子实例残留 | 根 ParticleSystem 使用 StopAction Destroy | 静态确认，待运行验证 |
| 共享资源被 075 调参污染 | 复制为 075 专用 prefab/material，未改 BoxBroken/brokenBox | 已隔离 |
| 门碎片数量造成移动端峰值 | 单次 25 个碎片并附带 Clouds 子系统 | Android/iOS 性能未验证 |
| 刺墙抬升高度或时长不符合最终节奏 | `Y=7.8`、`1s` 均为 Level75 序列化字段 | 待策划 PlayMode 调参 |
| HUD 关卡号与资源编号被误判为加载错关 | 静态链路为 `86 -> 10075 -> Level_075`，显示号、数据号和资源号本就不同 | 配置已确认，待正式入口复验 |
| 通关/答案文案显示 key 或空内容 | 保留策划 Excel 中已指定的 075 key，不伪造翻译 | 待本地化补词条 |
| 场景提示仍显示 029 文案 | 保留当前 Prefab 的 term，不在玩法需求外改文案归属 | 待策划确认是否新建 075 term |

## 验证边界

已执行的静态验证包括：

- 脚本 GUID、Prefab fileID、材质/贴图引用闭环；
- Level075 中旧 `Level29Ctrl` 与 `KillMove` 引用清零；
- 新面板只影响 075，公共面板和公共脚本无差异；
- 专用面板不再包含 `jump_btn.m_IsActive=0` 覆盖，`up_btn` 的槽位、箭头方向和独立引用闭合；
- 红框只在 `CaltropGround.m_Children` 出现一次，父级和局部坐标与原世界坐标一致；
- 地面顶面、压扁门落点参数和相机震动归位路径已静态核对；
- Unity 6000.0.59f2 Roslyn 参数下完整 `Assembly-CSharp` 编译通过（`0 error`）；
- 三个 Prefab 的 YAML 重复/缺失 fileID 扫描和目标文件 `git diff --check` 通过；
- Unity `Editor.log` 已确认 075 关卡、专用面板和私有粒子资源完成导入，未出现 075 相关编译错误。
- Luban 隔离生成通过，运行时 `tbleveldata.json` 同步后与当前 Excel 生成物一致。

本轮反馈修正后，再次使用 Unity 6000.0.59f2 自带 `DotNetSdkRoslyn/csc.dll` 和 Unity 生成的响应文件完整编译目标程序集，结果仍为 `0 error`；输出仅包含项目既有警告，未发现 Level075 诊断。

以下内容在实际执行前均保持未验证：

- Unity PlayMode 完整过关、失败、重开、暂停和退出；
- 普通跳跃键与独立向上键同时显示、各自事件正确且不同分辨率下不重叠；
- 红框在刺墙下降、抬升和局部抖动期间保持相对位置；
- 门在刺墙边缘、多次进入/退出触发器时的物理顺序；
- 压扁门的最终大小、贴地落点、遮挡关系、全屏震动强度与粒子视觉；
- Android/iOS 横屏安全区、多指触摸、低帧率和粒子性能；
- Unity 正式导入后两张新图片的 Inspector 预览与平台压缩结果。

## 回滚路径

1. 恢复 `Level_075.prefab` 的 `GamePlayPanel_000`、`Level29Ctrl` 和 `KillMove` 绑定。
2. 删除 `Level75Ctrl.cs`、`Level75GamePlayPanelCtrl.cs` 与 `GamePlayPanel_027_75.prefab`。
3. 删除 `DoorBroken_075.prefab` 和 `DoorBroken_075.mat`。
4. 如不再显示压扁门，将 `door_sl_01.png.meta` 恢复为原导入设置；图片原文件和策划的 LevelData 修改不需要回滚。

仅回滚本轮反馈时：重新添加专用面板的 `jump_btn.m_IsActive=0` 覆盖并把 `up_btn` 放回原槽；把红框父级改回关卡根且世界坐标恢复为 `(0.17, 4.91, 0)`；删除 `crushedDoorGroundY` 与相机震动字段和调用即可。该局部回滚不会影响 075 的主机关、门碎粒子或运行时配置。
