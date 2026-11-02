# Level143「钢绳图钉」风险评估

更新时间：2026-10-10

## 2026-10-10：重置后拖拽恢复风险复核

- 原行为基线与已报告问题：首次进入可拖图钉，用户实测重置后 nail 无法拖动。旧 `OnGameResume` 在暂停退出时读取尚为 PAUSE 的状态，缓存 `isPaused=true`；Reset/Replay 后没有显式状态通知再纠正。
- 影响范围：只修改 `Level143Ctrl.OnGameResume` 和本关/当日文档；不改 Prefab、五图钉绑定、共享 FSM/PlayCtrl/Actor、Level005、资源、SDK、存档或打点。继续复用原事件订阅、输入门禁和整关销毁重建。

| 风险与场景 | 本轮控制和验证 | 证据边界 |
| --- | --- | --- |
| Reset、失败重试、Replay 后仍卡在本地暂停 | Resume 只解除本地标记；真实 FSM/Pause 节点与提取的本关方法测试，三种重建入口各连续 5 次恢复通过，旧版同场景失败 | 时序隔离通过；不模拟 Unity 实例加载、碰撞或实际拖拽 |
| 解除标记后提前接受暂停/失败状态输入 | CanReceiveInput 的实际 PLAYING 校验未改；Resume 发送期间、Pause 退出到 FAILED、成功/失败/死亡/不可控/未激活/未初始化都拒绝输入 | 隔离门禁断言通过；UI 遮罩与手机原生覆盖层未实测 |
| 已开始销毁的旧实例被晚到 Resume 复活 | teardownStarted 拦截保持；测试旧实例保持暂停且不可输入 | 本地保护隔离通过；真实连续重建和事件清理仍需 PlayMode |
| 修复波及钢绳物理、火花、Shadow 或其它关卡 | 控制器与 HEAD 逐字比较，只有恢复赋值及两行新增注释变化；现有初始化、输入、碰撞与清理代码未修改 | 静态边界通过；实际触控、绳坡道和阴影表现为 `uncertain` |

- 验证结果：182 项断言通过；修改前复现 16 次恢复失败、修改后对应场景无恢复失败。Unity Bee 响应文件的独立 Roslyn 编译返回 0，0 error / 113 warning，本关无诊断；产物仅写系统 TEMP。脚本、测试日志和编译日志位于 `C:/Users/admin/AppData/Local/Temp/brainpunk-level143-resume-20261010/`。
- 剩余缺口：Unity 导入/PlayMode、五图钉实际拖拽、钢绳站立、火花/Shadow、Android/iOS 横屏触控和原生广告覆盖期间恢复均未执行。按下文用例 1、2、3、5、6 补验；不要将隔离测试当作真机关卡验收。
- 未决项：指定 feature plan 缺失、CodeGraph CLI/MCP 不可用；已用本关文档、源码、Prefab、既有重置日志补足当前修改依据，不扩大修改范围。
- 本轮回滚：仅恢复 `OnGameResume` 原赋值、移除新增注释并同步文档；无需改资源或存档，但会恢复已复现的时序错误。

## 范围与证据边界

本关是 Level005 副本上的单关扩展，核心行为由 `Level143Ctrl` 和 `Level_143.prefab` 本地承载；共享 Level005、Level052、Actor、Door、BaseLevelCtrl、输入/事件、存档与结算系统不在修改范围。

当前结论来自用户需求、参考截图、源码、Prefab、配置表、Luban 生成结果、Unity 导入日志和 Roslyn 编译。未运行 Unity PlayMode 或真机，因此所有实际画面、触控手感、物理稳定性和通关闭环结论均标记为 `uncertain`。

## 主要风险

| 风险 | 当前控制与证据 | 验证状态 |
| --- | --- | --- |
| Level143 仍执行 Level005 切绳逻辑 | Prefab 根组件已从 Level005 `CutManager` 替换为唯一 `Level143Ctrl`；本关脚本不写 `IsCut`、不禁用 Line、不断开 Joint 来完成切割 | YAML 静态通过；PlayMode 待验 |
| 拖图钉时门体一起移动或掉落 | 初始化固定门 Rigidbody2D 为 Kinematic + FreezeAll，并关闭五个遗留 SpringJoint2D；绳视觉端点仍指向门上原 EndPoint | 源码/绑定静态通过；拖拽中实际门稳定性待验 |
| 绳可见但 Actor 站不上去 | 每根 Line 运行时挂非 Trigger `EdgeCollider2D` 并使用 `WithoutShadowGround`；Level143 Actor 的 `whatIsGround` 从 `320` 局部扩为 `8512`，只追加 bit 13。Collider 与 LineRenderer 共用端点，物理层碰撞矩阵允许 Actor 与该层碰撞 | 层、掩码和源码静态通过；角色脚底、斜率和穿透待 PlayMode 验证 |
| 角色在钢绳上方仍出现多个投影点 | 第一版只在碰撞时隐藏，无法处理角色跳起时 ShadowController 提前向 `Stuff` 投影。现将钢绳移到其 `groundLayer=320` 排除的 `WithoutShadowGround`，射线不再命中钢绳；实际踩住时再隐藏整个 Shadow 根，阻止穿透后投到下层普通地面 | 层掩码静态闭合；截图场景需 PlayMode 复验 |
| 侧面碰绳时误隐藏 | 站立兜底只在 Actor Collider 与钢绳 EdgeCollider 真实接触、最近接触点落在角色碰撞盒底部 `0.18` 范围、且角色中心位于绳接触点上方时隐藏 `Shadow` | 源码/Prefab 参数静态通过；斜率、绳端和角色跳落待 PlayMode 验证 |
| 离开钢绳后 Shadow 没有恢复或覆盖其它动画原有状态 | 首次隐藏时缓存 Shadow 原激活状态；离开、暂停、非 PLAYING、死亡、Reset/Replay、Disable/Destroy 均走同一恢复入口 | 生命周期源码静态通过；进门动画并发待 PlayMode 验证 |
| 绳太陡导致解法不可达或门口存在碰撞台阶 | 图钉可在视口内自由拖动并保持离门最短 `0.75`；未自动限制最大斜率，保留玩家构造坡道的自由度 | `uncertain`，需按关卡截图和实际角色能力验收至少一条通路 |
| 点击绳端附近时误拖图钉，或拖图钉时误出火花 | 按下只做一次模式分流：图钉拾取半径内为 DragAnchor，否则整段手势为 TestRope；两种模式互斥 | 源码静态通过；移动端触控半径待验 |
| 快速划过钢绳漏火花 | 使用相邻屏幕采样对应的世界线段与钢绳求交，不依赖单帧 OverlapPoint；末端靠近时另有 `0.12` 世界单位容差 | 算法静态通过；低帧率/超快手势待验 |
| 长时间摩擦产生过多实例、GC 或音效堆叠 | Awake 预建 6 个火花实例并循环复用；火花限频 `0.08s`，音效限频 `0.18s`；摩擦过程中不新建对象 | 源码静态通过；低端机 Profiler 待验 |
| 火花与 Level052 参数或渲染层不一致 | Prefab 和 Glow 材质直接引用 Level052 使用资源；运行时覆盖根尺寸/速度/寿命/Burst/颜色、Glow 尺寸及 `SceneLayer/250` | 资源 GUID/参数静态通过；视觉一致性待验 |
| 刀光或火花被门/锁/背景遮挡 | 刀光设为 `SceneLayer/1000`；火花保持 Level052 的 `SceneLayer/250` | 静态参数通过；不同交点和门附近实际遮挡待验 |
| 暂停或 UI 上起手仍控制场景 | 起手调用共享 `InputUtils.IsPointerOverUI`；非 PLAYING、暂停、成功、失败、Actor 不可控/死亡均拒绝输入并清理手势 | 源码静态通过；多指与 Canvas 层级待验 |
| 重试时旧碰撞/粒子/事件残留，触发 MissingReferenceException | Reset/Replay 进入本地 Teardown，先停止输入和粒子、关闭动态碰撞；OnDisable/OnDestroy 统一退订；整关重建恢复 authored 图钉位置 | 生命周期源码静态通过；连续重试待 PlayMode/Console 验证 |
| 现有 Level143 场景摆放或蓝色表现被副本生成覆盖 | 实现以工作区已有未跟踪 `Level_143.prefab` 为目标，只替换控制器和本关字段，保留五个图钉、绳及已有 Transform/颜色差异 | 目标 diff 静态确认；Scene 视觉待验 |
| Level143 配置表存在但运行时找不到关卡 | 源表已有 `10143 / Level_143` 与 `117 -> 10143`；Luban validation 通过且正式 JSON 已出现对应记录 | 配置闭合通过；资源收集/真包加载待验 |
| 修改后继续测试到旧 Prefab 实例 | 当前 Unity 会话日志显示 SourceAssetDB 仍缓存 14:27 版本，而磁盘 Prefab 已是 14:31；独立 Roslyn 编译通过，但编辑器 DLL 尚未刷新 | 必须退出当前 PlayMode 并等待导入完成后重进；新版本 PlayMode 为 `uncertain` |
| `specs/037-remote-resource-loading/plan.md` 无法核对 | 工作区 AGENTS.md 指向该计划，但文件当前不存在 | `uncertain`；本次未据此扩大资源加载或远端配置行为 |

## 建议验收用例

1. 首次进入后分别拖动五个图钉，确认只有目标图钉和相连钢绳移动，门位置不动。
2. 将一根钢绳拉成可行斜坡，角色从左侧地面完整走上绳、走到门口并正常通关；重点观察端点是否绊脚或穿透。
3. 沿同一切点缓慢来回摩擦 2 秒，确认钢绳不断、火花持续在接触点出现、音效不过度叠加；再用快速跨线手势验证不漏判。
4. 在门前、背景、绳交叉附近试切，确认火花和刀光层级可见且不会触发门/图钉拖拽。
5. 拖动中暂停/恢复，试切中暂停/恢复，并连续执行 Reset、Replay 至少 5 次；检查位置复原、碰撞存在、无残留粒子和 Console MissingReferenceException。
6. 在 Android/iOS 横屏、不同宽高比和多指输入下检查图钉命中半径、视口边界、Actor 站立稳定性与帧率。

## 回滚

优先只撤回 Level143 私有控制器和 Prefab，不修改共享 Level005 或 Level052。若整关取消，再从两个 Level Excel 源表移除 Level143 对应行并重新生成 JSON；不要手工只删运行时 JSON，以免下次 Luban 生成重新带回配置。
