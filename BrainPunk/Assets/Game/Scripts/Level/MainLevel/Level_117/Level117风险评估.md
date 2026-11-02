# Level117「太阳太大」风险评估

更新时间：2026-08-13

## 原有行为基线

本关沿用常规 `BaseLevelCtrl`、共享 `ActorCtrl`、标准暂停/失败/成功结算和 `GamePlayPanel` 的商店按钮。新增行为仅限 Level117 私有控制器、遮阳区域、汗液表现、屏幕固定太阳和商店按钮拖拽日食，不修改共享关卡脚本或全局数据系统。

## 受影响文件与系统

| 范围 | 文件/资源 | 说明 |
| --- | --- | --- |
| 关卡 | `Assets/Game/Prefabs/Level/Level_117.prefab` | 绑定本关控制器、太阳和三个遮阳区域 |
| HUD | `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_117.prefab` | 从 `GamePlayPanel_000` 变体化，仅保留资源名；本关组件在真实运行时面板上自愈挂载 |
| 逻辑 | `Level117Ctrl.cs`、`Level117ShadeZone.cs`、`Level117RuntimeBridge.cs` | 暴露计时、着色、汗液、跟随与状态桥接 |
| 输入/视觉 | `Level117ShopButtonDrag.cs`、`Level117ShopButtonDragHandle.cs` | 拖拽阈值、点击抑制、覆盖率和日食视觉 |
| 素材 | `Assets/Game/ArtSrc/Sprites/l117/`、日冕贴图/材质 | 本关背景、伞面、地面阴影、三张汗滴与日冕资源 |
| 配置 | `LevelData.xlsx`、`LevelSort.xlsx`、`tbleveldata.json`、`tblevelsort.json` | 唯一 `10117 / Level_117` 数据与排序入口 |

## 风险与控制

| 风险 | 当前控制 | 状态 |
| --- | --- | --- |
| 角色停下后计时被错误暂停 | 暴露累加只依赖 PLAYING、遮阴和日食状态，不读取 `IsMoving` | 源码静态通过；PlayMode 待验 |
| 角色没有从暴晒首帧开始减速或终点未归零 | `exposureDuration=5`，速度严格按 `1 - elapsed / duration` 线性下降并在终点夹到 0 | 源码静态通过；手感待验 |
| 死亡后仍可移动或继续出汗 | `TriggerSunDeath` 与后续帧锁输入/速度并停止、清空三套汗滴 ParticleSystem | 源码静态通过；失败链待验 |
| 其它机关死亡后本关仍推进 | 检测外部 `ActorCtrl.IsDied` 后登记死亡并立即停止日晒和汗液；保持分支不再每帧调用 `StopMoving` | 源码静态通过；组合机关待验 |
| 结算窗口仍日晒或可拖按钮 | `Level117Ctrl` 在成功后清零日晒，在失败后停止日晒推进和汗液；HUD 的 `CanBeginDrag` 同时拒绝成功、失败或角色死亡状态，进行中的拖拽会被取消 | 源码静态通过；成功/失败演出待验 |
| 遮阳伞边界误判 | 显式 Collider 优先；本关无 Collider，横向按已绑定的可见地面阴影 `Sp_l117_zys_02`，垂直按伞面 `zheyang` | 三处引用静态闭合；实际边界待验 |
| 阴影颜色改变角色透明度，或颜色过浅被误认为未生效 | 着色时保留 Skeleton 当前 Alpha，仅混合 RGB；Prefab 使用 `(0.12, 0.15, 0.2, 0.6)` 深灰冷色 | 源码/Prefab 静态通过；视觉待验 |
| 重开后阴影、计时、汗液或拖拽永久失效 | 新实例每帧以 `PlayCtrl.CheckState(PLAYING)` 校正状态；旧关卡用 `attemptTerminating` 阻断回写；复用 HUD 会缓存旧控制器，等待 `ActiveController` 切换后重绑太阳/相机并解除 `isResetting` | 源码与状态机时序静态通过；PlayMode 重开待验 |
| 日食覆盖率与策划 80% 不一致 | 以屏幕投影圆形交叠面积计算，两个 0.8 Inspector 参数分别控制视觉/免疫；按未按压基准尺寸补偿共享按钮缩放 | 阈值/缩放静态通过；视觉近似需 PlayMode 校准，并避免两参数分叉 |
| 自动太阳半径偏小 | `sunRadiusWorld=0` 时取启用 Renderer 中最小 bounds 半径 | 已识别风险；建议后续填显式半径或在 PlayMode 校准 |
| 短点击被误判为拖拽 | 按下时保留 `eligibleForClick`，只有 `IBeginDragHandler` 到达 EventSystem 阈值后才取消当前指针点击并置 `IsBlockClick` | 源码静态通过；触摸设备待验 |
| 拖拽后仍触发商店点击 | 拖拽开始后以 `eligibleForClick=false` 和 `IsBlockClick` 双重阻断；组件级阻断下一帧清除，指针级阻断保留到该周期结束；取消/暂停同样取消当前周期 | 源码静态通过；边界待验 |
| 多指抢占拖拽或误开商店 | 首个有效指针独占拖拽周期；后续指针不覆盖缓存的 pointerId，并取消自身点击资格 | 源码静态通过；真机多指待验 |
| 安全区使太阳无法被 80% 遮挡 | 通常按 Canvas 与 `Screen.safeArea` 夹取，仅向太阳屏幕中心扩展至可见画面边界；其余方向仍受安全区限制 | 源码与几何静态通过；异形屏设备待验 |
| 相机或角色移动造成太阳漂移 | 初始化捕获 `Sun` 当前摆放位置的 viewport 锚点；执行顺序 `900` 在相机跟随后重投影，HUD 顺序 `1000` 再计算遮挡 | 坐标链与执行顺序静态通过；多画幅待验 |
| 按压缩放导致覆盖率跳变 | 覆盖半径和夹取 extents 用缓存的未按压 `localScale` 补偿；保留共享按钮根节点原有按压反馈 | 源码静态通过；运行时动画帧待验 |
| 真实拖拽继承按下缩小尺寸 | `OnDragStarted` 立即恢复 `initialButtonLocalScale`，拖拽期间每帧、结束、取消和 Reset 均恢复同一基准尺寸 | 源码静态通过；视觉手感待验 |
| HUD 入场动画的零缩放被误存为按钮基准 | `PlayShowPanelAnim` 会先把按钮缩放清零；当前位置与缩放分开捕获，只有 `SequentialScaleAnim` 停止且按钮三轴非零后才记录缩放，完成前 `CanBeginDrag=false` | 源码与编译静态通过；首次入场拖拽待验 |
| 商店按钮完全无法拖拽 | 根因是 World Space 内层 Canvas 错用外层 Overlay `rootCanvas`，空事件相机使坐标换算每帧回退原位置；现按事件 `pressEventCamera` 换算，兜底使用内层 `targetCanvas.worldCamera ?? Camera.main` | 根因与修复源码静态确认；真实拖拽待验 |
| 商店拖拽与全屏炎热效果同时完全不生效 | 用户现场结果推翻此前仅凭脚本/GUID/Variant YAML 判断已生效的结论。共同根因是二级 Variant 的追加组件目标是无效间接源对象，真实 HUD 未稳定获得 `Level117ShopButtonDrag`，因此 Handle、热色层和日冕都没有创建。当前由 `Level117Ctrl.Start/Update` 获取真实 `CurrentGamePlayPanel`，缺失时补挂组件并显式 `BindController(this)`；动态视觉对象继承实际 Canvas 的 UI Layer | 运行时链路、YAML 与编译静态通过；PlayMode 视觉/拖拽待验 |
| 修改后仍观察到旧表现 | Unity 当前 `Assembly-CSharp.dll` 的时间早于两份关键 Level117 源码，说明现场 PlayMode 尚未装载最新逻辑；只有程序集时间晚于源码且 Unity Console 无编译错误后，才允许用新一轮画面判断修复是否有效 | 时间戳已确认；Unity 前台刷新按用户要求未代操作 |
| 商店按钮因暂停缓存失真而无法拖拽 | `CanBeginDrag` 每次直接读取 FSM 权威状态；缓存仅控制暂停视觉和立即取消当前指针；Reset 等待新控制器后才恢复拖拽 | 状态机时序和输入链静态通过；真实拖拽待验 |
| 旧 HUD 销毁清理覆盖率时污染已创建的新局 | HUD 缓存创建时所属 `Level117Ctrl`，只有它仍是 `Level117RuntimeBridge.ActiveController` 时才上报覆盖率 | 源码静态通过；快速重开待验 |
| 汗液过高、朝向错误、朝左时穿头或密度不变 | 发射中心为 `(0,0.52,-0.1)`、左右偏移 `0.4`，以相反水平速度 `0.95~1.45`、相同向上速度 `1.1~1.7` 和重力 `3` 形成左右镜像抛物线；图片尖端每帧按当前 `totalVelocity` 切线旋转，再叠加由 `randomSeed` 固定的 `8°` 以内偏移；发射间隔由 `0.24s` 连续缩短至 `0.045s`，寿命为 `1.0~1.4s` | 源码、Prefab 参数与静态引用通过；镜像轨迹视觉待验 |
| 进入阴影时汗滴突然消失 | 阴影/日食免疫分支每帧仍更新已有粒子朝向，并以 `StopEmitting` 只停新发射；已有粒子继续运动、渐隐至生命结束。暂停、死亡、成功、失败和重开才清空 | 源码静态通过；PlayMode 生命周期待验 |
| 同时使用 ParticleSystem 速度对齐与手动旋转导致角度叠加 | Renderer 使用 `View` 对齐，不启用 Rotation over Lifetime；唯一朝向来源是 `UpdateSweatParticleRotations` 根据当前速度写入的粒子角度 | 双重旋转风险已静态排除；素材轴向视觉待验 |
| Billboard 将非方形汗滴拉宽 | 三张素材均为 `42x67`；启用 `startSize3D`，Y 使用随机缩放值，X 乘对应 `Sprite.rect` 宽高比，保留约 `0.627` 的原图比例 | 尺寸公式与三张资源尺寸静态通过；实际画面待验 |
| 运行时 `hanshui` 重复、重开残留或误删策划节点 | 先复用同名空根；三个粒子子节点和克隆材质都单独记录所有权。运行时创建的根可整体删除，复用的策划根只删本轮子节点；`OnDisable/OnDestroy` 清理幂等 | 源码所有权与双重清理静态通过；快速重开待验 |
| 热浪蒙版太淡或遮挡后不是逐渐变黑 | 热色、暗色和日冕由 `Level_117.prefab` 根节点序列化字段控制；默认热色 `(1,0.55,0.30,0.30)`，按 `coverage/0.8` 平滑插值到暖暗 `(0.18,0.12,0.08,0.46)`；独立日冕使用亮黄色 `(1,0.86,0.22,0.95)`；覆盖层不接收射线且位于 HUD 按钮下方 | 源码与 Prefab 参数静态通过；视觉待验 |
| 遮阳区域触发器误注册 | `Level117ShadeZone` 记录 Actor 父级并以 bounds 相交兜底 | 已识别风险：未显式排除 Actor 子节点 trigger collider |
| 跨关卡残留控制器 | `Level117RuntimeBridge.Bind/Unbind` 仅持有当前实例 | 源码静态通过；切关/销毁顺序待验 |
| 运行时创建汗滴/日冕造成移动端峰值 | 汗滴初始化一次性创建 `3` 个 ParticleSystem 与 `3` 个运行时材质，逐滴只调用 `Emit`；旋转更新使用预分配粒子缓冲。日冕纹理单例缓存，不在发射时分配对象 | 代码静态通过；低端设备 Profiler 待验 |
| 每帧查询 Renderer/Collider 产生 GC | 太阳半径、伞面和角色碰撞体首次解析后缓存，UI 四角使用固定缓冲 | 源码静态通过；Profiler 待验 |

## 验证证据

- 已静态读取五个 Level117 脚本、Level117 HUD 变体 YAML 和 Level117 关卡 YAML；脚本 GUID 与组件引用可对应，`BaseLevelCtrl` 指向 `GamePlayPanel_117`。
- 已确认新增逻辑只订阅既有事件，未修改共享 `ActorCtrl`、共享 `GamePlayPanel` 或全局事件定义。
- 已检查用户本轮要求：太阳相对屏幕固定；离开阴影立即流汗并开始 5 秒线性减速；轻点仍打开商店且只有真实拖拽抑制本次点击；日食参数为 `80%`；旧实例 Reset 后不能再激活玩法；本关视觉/逻辑字段均可在 Inspector 调整。
- 已补齐基础关卡先进入失败态、随后等待重开的边界：失败成立后本关立即停止继续累计日晒和播放汗液，避免结算窗口重复触发死亡表现。
- 已确认此前“拖拽链静态通过”的结论不足：首先修正了内外 Canvas 相机不一致和 HUD Reset 门禁；用户再次现场验证仍无拖拽且无热场后，进一步定位到真实 HUD 根本没有稳定获得二级 Variant 追加组件。本轮已删除该失效追加组件，改由当前 Level117 实例向真实 `CurrentGamePlayPanel` 补挂并显式绑定。
- 五张项目 PNG 与用户提供的共享源逐一 SHA256 一致；三张汗滴 `.meta` 已改为 Sprite Single、mipmap off、alpha transparency on、Clamp，Prefab 的三个 Sprite GUID 均存在。
- 已使用 Unity 6000.0.59f2 自带 Roslyn 与项目 Bee 定向响应文件编译包含全部 `Assembly-CSharp` 源码的 Level117 检查程序集，最终结果为 `0 error / 104` 条工程既有 warning。
- 已执行 Level117 脚本/文档空白检查；本关脚本和文档无空白错误。`Level_117.prefab` 为 `237/237` 个定义/唯一 fileID、`482` 次纯本地非零引用、`0` 重复、`0` 悬空；`GamePlayPanel_117.prefab` 仅有 `1` 个 PrefabInstance 定义，对象目标均指向源 Prefab，不存在本地悬空引用。
- Prefab 脚本引用统计为 `Level117Ctrl=1`、`Level117ShadeZone=3`；HUD 变体 `m_AddedComponents=[]`，`Level117ShopButtonDrag` 与拖拽句柄均由运行时挂载。`10117` ID、`Level_117` path、排序记录各唯一 `1` 条，两处日食阈值均为 `0.8`。
- 按用户要求未操作 Unity 前台，没有 PlayMode 日志；上述静态检查与编译不能替代实际画面、触控和失败/重开链验收。

## 2026-08-13：颜色与汗水行为变更评估

- 颜色配置从运行时 HUD 默认值迁移到本关 `Level117Ctrl` 序列化字段，绑定链仅在 `Level117Ctrl.EnsureHudControllerBound` 获取真实面板后执行，不触及共享面板或全局材质。若 Prefab 字段被清空，HUD 会显示透明/低强度颜色，需在 Inspector 保留有效 Alpha。
- 该轮的 SpriteRenderer 角度锁定和对象池结论已被后续 ParticleSystem 方案取代。当前以粒子当前 `totalVelocity` 持续对齐轨迹切线，三个粒子系统与材质只在初始化时创建，逐滴发射不 Instantiate。阴影停发收尾、低端设备峰值和真实视觉密度仍标记为 `uncertain`。

## 2026-08-13：ParticleSystem 轨迹与资源生命周期评估

- 本轮按用户要求将汗滴从 `SpriteRenderer` 池改为本关运行时三套 Unity `ParticleSystem`。每张 `Sp_l117_hz_01~03` 使用独立材质/粒子系统，不依赖 Sprite Atlas UV；未修改共享粒子、Actor、HUD 或其它关卡。
- 两侧抛物线由符号相反的水平初速度形成镜像，旋转每帧由当前 `totalVelocity` 计算；Renderer 保持 `View` 对齐且 Rotation over Lifetime 关闭，避免 ParticleSystem 自带方向与手动角度重复叠加。发射尺寸按各 Sprite 的 `rect.width / rect.height` 拆分 X/Y，避免 `42x67` 水滴被方形 Billboard 拉伸。
- `hanshui` 根节点不要求写入 Prefab；策划如预先建立同名空节点，运行时会复用并只删除自己创建的子粒子。Prefab 原有 Sprite、速度、重力、寿命、尺寸、密度、角度扰动和排序字段保持不变，无新序列化引用风险。
- 静态检查已覆盖双重旋转、同名根复用、运行时子节点/材质所有权、`OnDisable/OnDestroy` 幂等清理和阴影停发不清粒子。PlayMode 左右轨迹观感、低帧率旋转连续性、快速重开和移动端性能仍为 `uncertain`。

## 待确认与建议

1. 在 Unity PlayMode 中先验证短点仍打开商店、超过阈值能真实移动、拖拽松手不打开商店；再分别死亡重开和手动重开，确认按钮回初始位并重新可拖。
2. 校准太阳显式世界半径，或改用最大相关 Renderer bounds，确保按钮遮挡实际达到 80% 时视觉与免疫同步。
3. 沿三张 `Sp_l117_zys_02` 的左右可见边缘走入/走出，确认日晒重置点与画面阴影一致；若后续改用 Collider，需验证 Trigger 注册与角色子碰撞体边界。
4. 在 16:9、超宽、Android/iOS 横屏和低帧率下检查固定太阳锚点、日冕中心、橙红到黑色渐变、按钮夹取、汗滴原图宽高比及三套 ParticleSystem 峰值。

## 回滚路径

优先回滚 Level117 私有控制器及其运行时 HUD 挂载，并恢复原 `GamePlayPanel` 名称与商店按钮行为；不回滚共享 Actor、全局事件、素材库或其它关卡。若仅覆盖率不合适，先把 `sunRadiusWorld`、`fullEclipseCoverage` 和 `eclipseImmunityThreshold` 恢复为上一个 Inspector 值，不删除逻辑。

## 2026-08-13：汗滴粒子常驻化风险补充

### 原有问题与影响范围

- 原汗滴 ParticleSystem、子节点和材质只在运行时创建，退出 PlayMode 后消失；脚本每次初始化又强制写入视觉模块，导致策划无法在 Prefab Inspector 找到并可靠调整粒子。
- 本轮只影响 `Level117Ctrl.cs`、`Level_117.prefab` 与三张新增的 Level117 私有汗滴材质；不修改 Bubble 源 Prefab、共享 Actor、GamePlayPanel、存档、配置、事件或其它关卡。

### 风险与控制

| 风险 | 当前控制 | 状态 |
| --- | --- | --- |
| 常驻粒子在进入关卡时自行喷发 | 三套实例均序列化为 `looping=0`、`playOnAwake=0`、`EmissionModule.enabled=0`、`ShapeModule.enabled=0` | YAML 静态通过；PlayMode 待验 |
| Inspector 调参仍被脚本覆盖 | 删除运行时粒子/材质创建和模块配置；`EmitParams` 仅写位置、速度、旋转与随机种子 | 源码静态通过；Inspector 现场待验 |
| 退出或重开后粒子节点被销毁、引用失效 | Cleanup 只 Stop/Clear 并重置运行时计时，不销毁节点、不清空序列化数组 | 源码静态通过；重开待验 |
| 汗滴被角色或场景遮挡 | 三套 SortingGroup 初始使用 Foreground、Order 220，保留为每节点 Inspector 可调参数 | YAML 静态通过；视觉待验 |
| 粒子数量超过旧旋转缓冲而被截断 | 缓冲按每套 `main.maxParticles` 与当前粒子数扩容并复用 | 源码静态通过；压力场景待验 |
| Main 中部分参数看似可调但无效 | 文档明确 Start Speed、Shape、Start Rotation 由脚本发射轨迹接管；Lifetime、Size、Gravity、Color、材质和排序由 Inspector 接管 | 职责已记录 |

### 未验证项与回滚

- `uncertain`：未启动前台 Unity，尚未确认 Prefab Inspector 展开体验、默认大小/重力/渐隐曲线最终观感、阴影停发后旧粒子收尾、重开及 Android/iOS 横屏性能。
- 若常驻粒子序列化出现导入问题，只需移除 `Level_117.prefab` 的 `hanshui` 节点与 `sweatParticleSystems` 三条引用，并回滚本轮 `Level117Ctrl` 汗液字段/初始化改造和三张 `Level117_Sweat_*.mat`；Bubble 源 Prefab 和其它关卡不需要回滚。

## 2026-08-14：用户双侧粒子接入风险补充

本节取代上文关于 `hanshui/SweatParticle_01~03`、手动 `Emit`、旋转缓冲和三份独立材质的当前行为描述；这些内容仅保留为历史记录。

| 风险 | 当前控制 | 验证状态 |
| --- | --- | --- |
| 新旧粒子同时发射造成数量翻倍 | 控制器仅绑定 `hanshui1` 下两套新粒子，并删除旧手动 `Emit` 路径 | 源码与 Prefab 引用静态通过；PlayMode 待验 |
| 脚本覆盖用户做好的轨迹/旋转 | 运行时只改根位置、Play/Stop 和 `rateOverTimeMultiplier`；Shape、Velocity、Rotation、Size、Color、Renderer 与镜像均由 Prefab 持有 | 左右 ParticleSystem 逐行对比通过；视觉待验 |
| 三张图在一滴汗中依次轮播 | 两套均为 Sprite 模式，`Start Frame=0~0.9999`、`Frame over Time=0`，列表均为 01/02/03 | YAML 静态通过；随机分布待验 |
| 角色走开后汗水留在出生点 | 缓存用户摆放的根节点相对偏移，运行时随 Actor 根位置更新 | 源码/绑定静态通过；跳跃和移动待验 |
| 热度越高但粒子数不变或超过上限 | 使用与减速相同的 `exposureElapsed / exposureDuration`，从各系统 Inspector 初值线性插值到每套最大 `20` | 后台编译通过；PlayMode 数值待验 |
| Disable 后复用把 `20` 当成新起点 | 首次缓存 Inspector 初值，清理时恢复，不在每次 Enable 后重新采集运行时值 | 生命周期源码静态通过；快速重开待验 |
| 两套各 `20` 导致总量过高 | 用户指向的是单套粒子的 Rate 字段，因此当前按“每套最高 20”实现，总计最高约 40/s | 需求解释已记录；最终观感待策划验收 |

- 影响范围只包含 `Level117Ctrl.cs`、`Level_117.prefab` 和本关记录，不涉及存档、配置、事件、共享粒子或其它关卡。
- Unity 6000.0.59f2 后台 Roslyn 编译结果为 `0 errors / 104` 条工程既有 warning；两套粒子静态配置差异为 `0`，Sprite/本地引用闭合。
- `uncertain`：按用户要求未操作 Unity 前台，实际三图随机、密度递增、角色跟随、阴影自然收尾、暂停/重开和移动端性能尚未验收。
- 回滚时恢复旧控制器汗水逻辑与旧三套绑定，并撤销两套新粒子的三 Sprite 随机字段；无需回滚用户的其它粒子模块或共享资产。

## 2026-08-14：图集随机、晒红与减速曲线风险补充

| 风险 | 当前控制 | 验证状态 |
| --- | --- | --- |
| 三张独立纹理导致随机索引变化但画面仍固定 | 三张原图无缩放拼为同一 `126x67` 纹理，三个 Full Rect Sprite 与 Renderer 材质均引用该 atlas；材质保持原 `Sprite-Unlit-Default` Shader | 逐像素及隔离 Unity 导入通过；PlayMode 分布待验 |
| atlas 切片 fileID 被 Unity 导入重写后 Prefab 丢引用 | meta 固定 `-1170101/-1170102/-1170103`，隔离 Unity 6000.0.59f2 导入后实际 local ID 与 Prefab 完全一致 | 批处理导入通过；主编辑器刷新待验 |
| 策划只能改最大汗水量，最小量仍散落在粒子 Inspector | `sweatMinEmissionRate` 与 `sweatMaxEmissionRate` 均序列化在 Level117 根节点；两套系统统一读取 | 源码、Prefab 与编译通过；运行数值待验 |
| 晒红与阴影逻辑互相覆盖或改变角色透明度 | 合并为 `UpdateActorSpineTint`；阴影优先、日食恢复、热度使用同一进度，写 RGB 时保留当前 Alpha | 生命周期只读审查通过；视觉待验 |
| 成功或重开后角色保持红色 | 成功显式恢复初始 Spine 色；Reset、Disable、Destroy 继续幂等恢复 | 源码审查通过；完整流程待验 |
| 默认减速仍被感觉突然 | 旧线性改为可编辑 `moveSpeedFactorByHeat` EaseInOut；空曲线回退线性，死亡终点仍强制速度为 0 | 数学与编译通过；策划手感待调 |
| EaseInOut 中段比旧线性下降更快 | 默认 SmoothStep 中段斜率约为线性的 1.5 倍，但首尾斜率为 0；可直接编辑曲线或增加 `exposureDuration` | 已暴露调参；PlayMode 待验 |

- 本轮未修改日晒计时语义：角色站着不动也会继续累计热度；进入阴影或达到日食免疫阈值仍在单帧清零热度并恢复满速。
- 后台 Roslyn 编译为 `0 errors / 104` 条工程既有 warning；未操作当前 Unity 前台，实际颜色、粒子随机和减速手感仍标记为 `uncertain`。
- 回滚只需恢复旧独立 Sprite/材质、旧线性速度与原 Spine 阴影方法，并移除新增 atlas、最小发射率和热色字段；共享 Actor、HUD、数据和其它关卡无需回滚。

## 2026-08-14：颜色过渡、扩散日冕与日晒死亡音效风险补充

| 风险 | 当前控制 | 验证状态 |
| --- | --- | --- |
| 同一帧多次颜色调用导致渐变变快 | 状态方法只更新目标色；实际 Spine RGB 只在 `LateUpdate` 推进一次 | 调用链静态通过；PlayMode 待验 |
| 渐变覆盖角色死亡或进门 Alpha | 每帧读取并保留 Skeleton 当前 Alpha，只插值 RGB | 源码与编译通过；完整演出待验 |
| Reset 后立即早退导致无法变回正常色 | `OnGameReset` 先请求初始色，`LateUpdate` 使用 unscaled 时间继续推进；Disable/Destroy 最终立即清理 | 生命周期静态通过；失败重开待验 |
| 单纯放大日冕使亮圈脱离太阳 | 纹理亮峰按 `0.5 / sunCoronaScale` 反算，`1.8` 倍外框下仍贴近太阳边缘；外侧单独使用 `0.2` 羽化 | 数学复核通过；画面待验 |
| 日晒死亡音效重复或手动 Reset 误播 | `failed` 播放位于 `deathTriggered` 单次门禁内，不放在共享 `GameResetEvent` | 调用点与标签静态通过；听感待验 |
| 修改失败/重来打点时序 | 本轮只在死亡入口播放现有音效，不改事件派发、Reset 类型、广告和统计链 | 已对照《关卡打点生命周期约定》，无冲突 |

- 当前 Prefab 保留用户已调整的 `moveSpeedFactorByHeat`、`actorHotTint.a=0.8588`、`shadeTint.a=0.3255` 和汗水 `1~30`；新增参数仅为 `actorTintTransitionDuration=0.12`、`sunCoronaScale=1.8`、`sunCoronaOuterFeather=0.2`。
- 后台 Roslyn 完整编译为 `0 error / 104` 条工程既有 warning；未操作 Unity 前台，颜色切换、光晕扩散、失败音效、暂停/重开和移动端表现仍为 `uncertain`。
- 回滚本轮只恢复 Level117 私有颜色写入和日冕生成方法、删除日晒死亡音效调用及三个新增字段；不回滚共享 Actor、HUD、SoundsGood、打点文档或其它关卡。
