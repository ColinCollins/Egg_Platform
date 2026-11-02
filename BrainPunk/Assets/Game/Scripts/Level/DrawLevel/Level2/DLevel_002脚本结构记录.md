# DLevel_002 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame `DLevel_002`
- 更新日期：2026-08-02
- 当前状态：实现、Unity 导入和编译已完成；完整 PlayMode 与 Android/iOS 横屏验收待补
- 参考基线：`uncertain`，当前没有一关同时覆盖本关全部机制；局部参考
  DLevel_010 的可见 Alien Intro、DLevel_006 的首笔释放，以及共享 DrawLevel Handler 契约
- 选择理由：本关同时具有“遮罩后飞入、首笔才释放、独立倒计时、Spine 骨骼挂架”四项组合，
  不适合为了形式统一把单关动画名或机关状态上提到共享控制器
- 主要脚本：
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCountdownCtrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawAlienClipBallRigView.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawEntrancePathView.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/Level2/DLevel2Ctrl.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_002.prefab`
- 共享装置 Prefab：
  `Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab`

## 2. 脚本与组件树

```text
DLevel_002
├─ DrawLevelCtrl                         # Draw 关卡运行时、结果锁和 Handler 分发
├─ DrawLineFeature                       # 输入、采样、线段渲染/碰撞/刚体
├─ DrawLevelCountdownCtrl                # 首笔后 15 秒倒计时
├─ DLevel2Ctrl                           # 本关 Intro、Spine 架子与球释放
└─ level2
   ├─ Alien                              # 共享实例；Transform 直接摆在最终位置 (-4.58, 2.41)
   │  ├─ DrawAlienClipBallRigView        # 只暴露装置内部结构引用
   │  ├─ DrawEntrancePathView            # 起点、终点与 Scene 轨迹预览
   │  ├─ entranceStartPoint              # 入场起点 (-15.40, 2.41)
   │  ├─ ActorSpineCtrl
   │  ├─ SkeletonAnimation               # sp_AlienSpaceship_003
   │  └─ catch
   │     ├─ BoneFollower                 # 跟随 Alien/bone2
   │     └─ clipSwingPivot               # 关卡代码只在此层叠加甩动
   │        ├─ clipVisual
   │        │  └─ SkeletonAnimation      # sp_clip_002，内容已更新为用户 003 版
   │        └─ yellow_ball
   │           ├─ Rigidbody2D
   │           ├─ SpriteRendererGroupAlpha # 成功时与 Actor 同步渐隐
   │           └─ payloadContentRoot
   │              └─ Actor.prefab 子实例  # 保留嵌套 Prefab 链接
   ├─ DrawGame_DrawLine.prefab
   ├─ DrawGame_SuccessDoor.prefab
   ├─ FallArea.prefab
   └─ GameCamera.prefab
```

旧 catch Sprite、Collider 与 Animator 子节点仍保留为 inactive 回滚材料，不参与当前显示和逻辑。

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| `DrawLevelCtrl` | 共享运行时 | 初始化 Draw、扫描 Handler、成功时同步 Actor 与可选附属物渐隐、结果一次性提交、Reset | `IDrawLevelRuntime` 等共享契约 | `DrawPlayCtrl` 随 DLevel 实例创建/销毁 | 不持有 DLevel_002 动画名和架子状态 |
| `DrawLineFeature` | 共享组件 | 画线输入、有效笔画、Collider 与 Dynamic 刚体 | MonoBehaviour、局部 Stroke 事件 | DLevel prefab | 不判断本关球、倒计时或胜负解法 |
| `DrawLevelCountdownCtrl` | 共享可选 Handler | 首笔后启动 15 秒倒计时，暂停时停表，归零按配置提交结果 | `IDrawStrokeCompletedHandler` | DLevel prefab | 不控制架子与球 |
| `DrawAlienClipBallRigView` | 共享 Prefab 视图 | 暴露猫 Spine、BoneFollower、甩动节点、夹子 Spine、球刚体、`payloadContentRoot` 和球渐隐组 | MonoBehaviour | 共享装置 Prefab | 不运行 Intro、不放球、不判断胜负 |
| `DrawEntrancePathView` | 共享摆放视图 | 规定根节点为最终位置、子节点为入场起点，并在 Scene 绘制轨迹/起点占位 | MonoBehaviour | 共享装置 Prefab | 不播放 Tween、不决定释放时机 |
| `DLevel2Ctrl` | 单关控制器 | 可见 Intro、Alien/架子动画、夹持、释放、Reset | `IDrawLevelVisibleIntroHandler`、`IDrawStrokeCompletedHandler`、`IDebuger` | DLevel prefab | 不改画线生成、共享 HUD、存档、打点或平台流程 |
| `ActorSpineCtrl` | 既有动画控制 | 播放 Alien `idle01`、`control` | 既有组件 | Alien | 不承担本关物理 |
| `BoneFollower` | Spine Runtime | 把 catch 跟随到 `bone2` | Spine 组件 | Alien 子层级 | 不执行额外甩动；甩动归 `clipSwingPivot` |
| `SkeletonAnimation` | Spine Runtime | 猫与架子的骨骼渲染和姿势 | Spine 组件 | Alien/clipVisual | 架子动画不决定实际放球时机 |
| 成功门 / FallArea | 既有 prefab | 进入成功、坠落失败 | 既有触发组件 | DLevel prefab | 不访问 DLevel2Ctrl 的局部动画状态 |

## 4. 生命周期与关键调用

1. DLevel 实例创建后，`DLevel2Ctrl.Awake()` 解析局部引用、绑定 `BoneFollower` 到
   `bone2`，先缓存 Alien 根的策划最终位置与 `entranceStartPoint` 世界起点，再把 Alien
   移到起点，并缓存球/甩动节点初态、把球恢复为 Kinematic。
2. 转场遮罩完全揭开后，`DrawLevelCtrl` 调用
   `IDrawLevelVisibleIntroHandler.PlayDrawIntroAsync()`；输入仍保持关闭。
3. Intro 首帧播放猫 `idle01` 与架子 `clip_close`，把 `clipSwingPivot` 预倾 `38°`。
   Alien 从 `X=-15.4` 向 `X=-4.58` 移动 `0.78s`，使用
   `Ease.OutBack(1.9)`；架子先保持 `+38°` 后摆 `0.18s`，再按
   `-32° → +15° → -7° → +3° → 0°` 连续左右衰减，每段使用
   `Ease.InOutSine`。架子在 `0.43s` 到达向前峰值，整段约 `0.85s` 完成。
4. Sequence 的每次更新都调用 `KeepBallHeld()`，球保持初始夹持局部姿态；Sequence
   完成后强制恢复甩动节点静止旋转，随后 Draw 流程才开放输入。
5. 第一笔有效线完成时，`DrawLineFeature` 已先完成线条刚体/碰撞体；`DrawLevelCtrl`
   再同步分发 Stroke Handler：
   - `DrawLevelCountdownCtrl` 只启动一次 15 秒倒计时；
   - `DLevel2Ctrl` 只消费一次，播放 `clip_open`，球保持世界姿态脱离动画父级后切换
     Dynamic，并播放猫 `control`；完成后恢复循环 `idle01`。
6. Actor 子碰撞体进入成功门时走既有 Draw 成功链；Actor Spine、Actor Sprite 组与
   `yellow_ball` 额外 Sprite 组共用同一 `0.4s` alpha 同步渐隐，门动画等待结束后提交成功。
   球/Actor 进入 FallArea 或倒计时归零时仍走既有失败重载链。
7. 失败、手动重来或胜利 Replay 会重建关卡，但不再重播可见 Intro；新实例在黑幕下直接把
   Alien 放到缓存 FINAL，并恢复 `clip_close`、Kinematic、零速度、原父级和夹持局部姿态。
   `OnDisable()` 仍会 Kill Tween 并令旧动画完成回调失效。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 当前绑定或数值 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| `DLevel_002` | `drawLevelCtrl` | 同根 `DrawLevelCtrl` | 必填 | 自动向父级查找；仍缺失会失去共享状态入口 |
| `DLevel_002` | `alien` | `level2/Alien`，根位置即最终位置 `(-4.58,2.41)` | 必填 | 记录错误并结束 Intro，避免永久卡门禁 |
| `Alien` | `DrawEntrancePathView / entranceStartPoint` | 共享 Prefab 子节点；世界起点 `(-15.40,2.41)` | 必填 | 记录错误并结束 Intro；不会把最终位置误当起点 |
| `DLevel_002` | Intro 参数 | `0s / 0.78s / OutBack 1.9` | 必填 | 运行时对时长做最小值保护 |
| `DLevel_002` | 架子惯性摆参数 | 后摆 `+38° / 0.18s`；峰值 `-32° / +15° / -7° / +3° / 0°`；分段 `0.25 / 0.14 / 0.11 / 0.09 / 0.08s` | 可调 | 运行时保护负保持时间和过短 Tween 时长 |
| `Alien` | 共享 Prefab 来源 | `DrawGame_AlienClipBallRig.prefab` | 必填 | Prefab/GUID 缺失会导致猫、架子、球和 View 整组丢失 |
| `Alien` | `DrawAlienClipBallRigView` | 七个内部引用非空 | 必填 | 控制器按精确相对路径兜底；仍缺失则记录错误并停止对应动作 |
| `Alien` | SkeletonData | `sp_AlienSpaceship_003` | 必填 | 无法播放猫动画或绑定 bone2 |
| `catch` | `BoneFollower.boneName` | `bone2`，跟随 skeleton flip | 必填 | 初始化失败会记录错误 |
| `clipVisual` | SkeletonData | `sp_clip_002/clip_SkeletonData.asset` | 必填 | 缺动画时记录错误，不切换视觉姿势 |
| `yellow_ball` | Rigidbody2D | 初始 Kinematic，释放后 Dynamic | 必填 | 记录错误且不能放球 |
| `yellow_ball` | `SpriteRendererGroupAlpha` | `includeInactive=true`，绑定到 `DrawLevelCtrl.successExtraRendererGroup` | 成功演出必填 | 未绑定时只有人物渐隐，球保持可见 |
| `DLevel_002` | `ballReleaseParent` | `level2` 稳定父节点 | 可选 | 回退 Alien 父节点，再回退控制器节点 |
| `DrawLevelCountdownCtrl` | `cdTime / isSuccess` | `15 / false` | 本关必填 | 未绑定时本关不会启动倒计时 |
| `DrawLineFeature` | 当前工作区局部参数 | 线宽 `0.2`、墨水 `50`、允许多笔、满长度质量 `200` | 已有用户配置 | 非本次 Spine/Intro 调整范围 |

资源：

- 共享装置：`Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab`
- 猫：`Assets/Game/ArtSrc/Spines/sp_AlienSpaceship_003/`
- 架子：`Assets/Game/ArtSrc/Spines/sp_clip_002/`
- 用户 003 版只改变 `clip.json` 的 `clip_close` 四个骨骼角度；PNG、Atlas、
  Mesh、Bounds、`clip_open` 和 GUID 均保持不变。

## 6. 数据、UI、资源与事件流

- DB/存档：本关控制器不直接访问 DB；成功后的进度/星级继续由既有 Draw 结算链处理。
- Luban/配置：本次不新增或修改表；当前 DrawLineFeature 使用 prefab 局部参数。
- UI：倒计时复用既有 Draw HUD，仅显示最后 5 秒；Intro 与架子不新增 UI。
- 局部引用：DLevel2Ctrl 只访问同一 prefab 可达对象和 Spine 资源。
- 事件：复用 DrawLevelCtrl 已有 Handler 扫描与结果回调；未新增 `PlayEvents` 或全局事件。
- 资源加载：猫和架子作为 DLevel prefab 的 Unity 资源引用随关卡实例加载，无额外运行时地址。
- Analytics/SDK：本次不新增打点、广告、IAP、RemoteConfig 或平台分支。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| Intro 后不立即放球，等首笔才释放 | 本关解法要求玩家先画线 | 线条先进入物理，随后同帧开夹放球 | 静态调用链、Unity 编译；完整 PlayMode 待补 | 本关必要差异 |
| `BoneFollower → clipSwingPivot → clipVisual + ball` | Spine 骨骼跟随会覆盖同节点 Tween；Dynamic 球不能继续受动画父级驱动 | 架子跟随猫 idle，甩动独立；放球前先脱父级 | 独立关键帧渲染、层级/GUID 静态检查 | 观察中的局部模式 |
| Intro 使用短距离高速 OutBack 和显式阻尼摆序列 | 用户要求起飞时向后、制动后向前并左右衰减，不接受到点额外 Punch | 总门禁约由 1.2s 缩至 0.85s，Alien 停稳后只再等待约 0.07s | 参数、时序和编译已验；主观手感待 PlayMode | 不升级共享 |
| 成功时球与人物同步渐隐 | 球是 Actor 父节点，Actor 自带渐隐组无法向上收集球 Sprite | `yellow_ball` 的 idle/open/item 与人物共用现有 `0.4s` alpha，不增加成功等待 | 绑定、调用顺序与编译已验；完整成功 PlayMode 待补 | 共享控制器保留可空附属物组，不改变未绑定关卡 |
| 猫 + Spine 架子 + 蓝球抽为结构 Prefab | DLevel006 出现第二个明确复用关卡，且用户要求后续其它关继续使用调整后的组合 | DLevel002 玩家行为不变；DLevel006 可对同一结构做 Tag、皮肤、镜像、碰撞层和成功组件的实例覆盖 | 三实例结构、GUID、嵌套 Actor、镜像释放与 Unity 编译已验；完整 PlayMode 待补 | 已采用纯结构共享，关卡行为不共享 |
| 本关附加首笔倒计时 | 关卡失败条件 | 首笔后 15 秒归零重载 | 既有倒计时文档与静态调用链 | 共享可选组件已存在 |

## 8. 可复用结论与优化候选

- 本关继续沿用的稳定结构：共享 `DrawLevelCtrl` 管运行时/结算，单关 Handler 只控制局部机关。
- DLevel002 和 DLevel006 已形成第二个具体复用证据，并有用户明确要求，因此将猫、
  `BoneFollower`、Spine 架子、蓝球、`payloadContentRoot` 与嵌套 Actor 抽为共享结构 Prefab；
  `DrawAlienClipBallRigView` 只提供强类型引用，不持有任何时序或玩法状态。
- 关卡局部边界保持不变：最终位置/入场起点、Intro 参数、首笔释放、倒计时、碰撞成功、Tag、皮肤、
  Layer override 和 Reset 仍分别属于 `DLevel2Ctrl`、`DLevel6Ctrl` 与各自 Prefab 实例。
- 动画骨骼跟随与额外物理甩动需要分层；刚体释放前先保持世界姿态脱离动画父级，再切换
  Dynamic。镜像装置释放时还需把负缩放等价转移到 `payloadContentRoot`，避免 Dynamic
  Rigidbody2D 根保留反射矩阵。
- 成功退场若视觉对象位于 Actor 父级或兄弟层级，应通过 `successExtraRendererGroup`
  显式绑定，而不是扩大 Actor 自带组的收集范围；未绑定关卡继续只淡出 Actor。
- 暂不共享的局部实现：猫/架子动画名、入场距离、惯性摆各峰值、释放触发和胜负条件继续
  留在关卡控制器；不修改共享 Draw 或 Actor API。
- 后续关卡若继续复用，只实例化共享结构 Prefab 并在关卡层覆盖必要差异；不得把单关最终位置、
  倒计时或成功条件回填到共享 View。
- Spine 来料若 PNG/Atlas/GUID 不变、只有 JSON 姿势更新，应优先原位替换 JSON 并保留
  已验证的纹理 meta 与材质设置，避免资源元数据 churn。

## 9. 验证证据与缺口

- 静态检查：
  - 新 `clip.json` SHA256 与用户 `003/003` 来料一致；
  - `clip_close` 新角度和 `clip_open` 存在，SkeletonData GUID 保持不变；
  - Prefab 的 `bone2`、Spine 引用、球刚体、稳定释放父级和全部 Intro 参数已绑定；
  - DLevel002 的 Alien 与 DLevel006 的两套装置均引用同一共享 Prefab，View 七个引用完整，
    Actor 保持嵌套 Prefab 关系；共享球保留 DLevel002 的 Actor 排除层，DLevel006 两实例
    明确覆盖为 `Exclude Layers=0`；
  - `yellow_ball` 根的额外渐隐组已绑定，`includeInactive=true`，成功循环对 Actor 组与
    额外组写入同一个 alpha，最终均写 0；Reset 对额外组恢复 1；
  - 旋转时间轴为 `0～0.18s +38°`、`0.43s -32°`、`0.57s +15°`、
    `0.68s -7°`、`0.77s +3°`、`0.85s 0°`，不存在重叠旋转 Tween；
  - `git diff --check` 在收尾时执行。
- 编译：Unity 6.0.59f2 已重新生成 `Assembly-CSharp.dll`，最近日志无新增 DLevel2 C# 错误。
- Unity 预览：以真实 prefab 和新 Spine 渲染了 `38°` 起始帧及静止帧，确认向左后方倾斜、
  回正后夹爪仍包住球；临时预览脚本已删除。
- Unity PlayMode：`uncertain`，尚未完整实测连续飞入、阻尼摆、首笔开夹、球与人物同步
  渐隐、下落与胜负。
- Android/iOS：`uncertain`，尚未在代表性横屏设备验证 60 fps、出屏边缘和触控流程。
- 性能：本次只改变一个局部 DOTween Sequence 和已有 Spine JSON，不新增 Update、
  运行时实例化、资源加载或持续分配；移动设备实际帧率仍待验证。

## 10. 风险与回滚

- 主要风险：
  - `38° / 32°` 的动态力度属于主观手感，静态关键帧不能代替完整 PlayMode 观看；
  - 新 `clip_close` 比旧姿势略宽，虽然静态预览仍包住球，但移动中遮挡需继续观察；
  - DLevel002 现在依赖共享 Prefab 与 View 脚本，提交/搬运时必须同时包含 Prefab、脚本及
    两者 `.meta`；否则会整组 Missing Prefab 或 Missing Script；
  - 额外渐隐组以绝对 alpha 覆盖子 SpriteRenderer；当前球素材初始 alpha 均为 1，若未来
    增加半透明子图，需要改为缓存并按初始 alpha 比例缩放；
  - 成功门演出尚未提交结果时倒计时仍可能归零的既有竞态不在本次范围内。
- 回滚路径：
  1. 若只回滚本次惯性摆修正，恢复旧的回正加 `DOPunchRotation` 字段和 Sequence；
  2. 若只回滚成功同步渐隐，清空 `successExtraRendererGroup` 并移除 yellow_ball 上的
     `SpriteRendererGroupAlpha`；
  3. 恢复旧 `clip.json` 的四个 `clip_close` 角度；
  4. Alien 起点恢复 `X=-16.72`，移动恢复 `1.2s` 和默认 OutBack；
  5. 移除预倾/惯性摆段，把架子恢复为正位开始的
     `18° / 0.85s / vibrato 3 / elasticity 0.45` Punch；
  6. 若回滚整套 Spine 替换，再按风险评估文档第 11.5 节恢复旧 catch Animator 与资源。
  7. 若仅回滚共享抽取，应先把共享 Prefab 内容完整嵌回 DLevel002，再删除共享 Prefab/View；
     不得在 DLevel006 仍引用时删除共享资源。

## 11. 2026-08-02 共享初态与碰撞边界修正

- 当前共享树保持 `yellow_ball/payloadContentRoot/Actor`；中间层用于 DLevel006 镜像释放，
  不改变 Actor 属于球内容的 Transform 语义。人物视觉层恢复为 SortingGroup order `68`，
  位于球壳 idle order `100` 下方。
- 共享 Actor 恢复 `shrink` 初始动画与测试动画；保留已调局部位姿
  `(-0.09,-0.09,+10.374°,0.9)`，没有回滚 DLevel002 的 Intro 参数或负 Y 抵消姿态。
- 共享旧 hook/block/catchArea 的 4 个 Collider2D 与 DLevel002 Alien 根 Trigger 已删除。
  持球仍由控制器锁位姿，必须保留球 CircleCollider2D 和 Actor 主碰撞体。
- Unity 迁移日志与序列化计数已核对；完整 PlayMode、成功门和 Android/iOS 仍未验证。
  回滚只需恢复目标 Collider 组件和三项共享 Actor override，不涉及数据迁移。

## 12. 2026-08-02 最终位置摆放与入场轨迹契约

- 关卡编辑时 `level2/Alien` 必须直接放在球最终释放位置 `(-4.58,2.41,0)`；不再把整套
  装置摆在屏幕外，也不再维护同级 `endPoint`。
- 共享装置新增 `DrawEntrancePathView` 与直接子节点 `entranceStartPoint`。当前共享根为
  `(0.9,0.9,0.9)`，默认局部起点反补偿为 `(-12.022222,0,0)`，本关世界起点仍为
  `(-15.40,2.41,0)`。
- Scene 视图会显示起终点圆标、虚线路径、方向箭头和把装置 Renderer 包围盒平移到起点的
  虚拟占位；这些 Gizmo 只用于编辑器预览，不参与运行时物理或动画。
- `DLevel2Ctrl` 在移动根节点前一次性缓存世界起点与最终位置；Awake/Reset 回到缓存起点，
  Intro 用完整 `DOMove` 回到缓存最终位置。当前两点 Y/Z 相同，因此既有 `0.78s / OutBack(1.9)`
  水平表现、夹子惯性摆、首笔释放、倒计时和成功渐隐均不变。
- Unity 迁移器已核对起终点误差小于 `0.001` 并删除旧 `endPoint`；C# 编译和 PrefabImporter
  已通过。完整 PlayMode、Replay 与 Android/iOS 仍为 `uncertain`。
- 回滚本节需同时恢复“场外根节点 + 同级 endPoint”数据模型和旧控制器缓存方向；只删除
  Gizmo 或只移动 Transform 会导致运行时从错误位置起飞。

## 13. 2026-08-02 共享正位、球内无影子与单装置镜像边界

- 共享 Rig 以 Alien `bone2` 的 `+4.22°` 静止旋转为依据，把
  `clipSwingPivot.localZ` 校正为 `-4.22°`，并把 `yellow_ball.localZ` 成对校正为
  `180°`。DLevel002 的入场摆动仍相对缓存基准叠加并回到该基准，最终夹子与球回正；
  `followBoneRotation` 保留，idle 原有不足约 1° 的轻微骨骼微摆不被抹除。
- 共享 nested Actor 的 `Shadow` GameObject 改为 inactive。该表现同步用于 DLevel002、
  DLevel006 与 DLevel015；没有修改通用 `Actor.prefab`，普通角色关卡不受影响。
- DLevel002 与 DLevel015 曾保存 BoneFollower 编辑态采样产生的 `catch` Position/Rotation
  instance override；现已移除，两关 Scene 静止预览继承同一共享基线。运行时仍由
  BoneFollower 每帧根据 `bone2` 重算，不改变挂点动画。
- `DLevel2Ctrl` 现在也是 `DLevel15Ctrl` 的单装置行为基类，引用解析优先走
  `DrawAlienClipBallRigView`。释放边界可把镜像实例的负缩放转移到 payload；DLevel002 本身
  是非镜像实例，因此球释放、碰撞体、成功渐隐、15 秒倒计时和 Reset 玩家行为不变。
- 静态矩阵与序列化绑定已核对；完整 DLevel002 PlayMode、成功/失败、Replay 与 Android/iOS
  仍为 `uncertain`。回滚 Pivot 与球角度必须成对进行，Shadow override 可单独移除。

## 14. 2026-08-02 共享猫夹球整体缩放 0.9

- 共享 Rig 根缩放为 `(0.9,0.9,0.9)`，DLevel002 无实例缩放 override，因此猫、夹子、
  球、Actor 和复合碰撞体统一缩小 `10%`。
- 起点 local X 改为 `-12.022222`，经 `0.9` 根缩放后世界偏移仍为 `-10.82`，
  所以 START/FINAL、入场距离与 Tween 时长不变。
- 质量、重力、首笔释放、倒计时和结算链未改。需 PlayMode 验证缩小后落球接触、
  成功门与 Reset；未验证项为 `uncertain`。回滚需同时恢复根 `(1,1,1)` 和起点 `-10.82`。

## 15. 2026-08-02 Actor 碰撞体当前结构

- `payloadContentRoot/Actor` 仍是嵌套 Actor prefab，保留 ActorCtrl、Spine、SortingGroup 和
  `shrink` 配置；共享实例的 RemovedComponents 包含 Actor 自带 Rigidbody2D 与
  PolygonCollider2D。
- `yellow_ball` 根保留唯一 Rigidbody2D 与根 CircleCollider2D；Actor 既有子 BoxCollider2D 保留，
  但物理球不再包含角色轮廓 PolygonCollider2D。删除后实际进入成功门的是父级球壳 Collider，
  旧的“只向父级找 ActorCtrl”无法识别其子层级 Actor，具体修复见第 16 节。

## 16. 2026-08-02 球壳成功门身份解析

- 成功门仍只接受 Player Tag；共享 `yellow_ball` 根保持 Player Tag，不修改门 Prefab 或 Layer。
- `DrawLevelCtrl.ResolveSuccessTriggerActor()` 先兼容普通 Actor Collider 的父级查找，再比较
  `collider.attachedRigidbody` 与本关 Actor 最近父级 Rigidbody2D。DLevel002 中两者都指向
  `yellow_ball`，因此球壳进入门可安全回认球内 Actor。
- 回认后仍严格要求命中 DrawLevelCtrl 缓存的同一个 `_actor` 且角色未死亡；其它球、线条、
  场景物体和别关 Actor 不会通过。原门动画、球/人物同步渐隐及结果提交时序保持不变。
- Assembly-CSharp 全量编译为 `0 error`；完整 PlayMode 与设备验证仍为 `uncertain`。

## 17. 2026-08-03 场外 Intro 物理隔离

- 问题背景：DLevel026 复用本控制器后，夹子球会在 `Awake/RestoreInitialState` 中先移动到
  画面外的 `entranceStartPoint`。该位置与左侧 `FallArea` 重叠时，球根 CircleCollider2D
  可通过复合 Rigidbody2D 回认内部 Actor，并在 Intro 尚未完成时提前派发失败事件。
- 原行为：入场阶段球虽然是 Kinematic，但 `Rigidbody2D.simulated` 仍为 `true`；Kinematic
  只禁止重力驱动，不会禁止 Trigger，因此删除 FallArea 能掩盖问题，但会永久丢失出界失败。
- 新行为：`RestoreInitialState()` 在设置起点和夹持态前把夹持球刚体设为
  `simulated=false`。Transform、DOTween、BoneFollower 与 Spine Intro 仍正常播放，但球及其
  复合 Collider 不参与 FallArea、陷阱、平台或其它 2D 物理检测。
- Intro 正常完成、重开直接应用完成态或引用异常回退时，控制器在最终位置执行
  `Physics2D.SyncTransforms()` 后恢复 `simulated=true`；此时球仍为 Kinematic。只有玩家完成
  第一笔有效画线后，既有 `ReleaseBall()` 才会把球切换为 Dynamic。
- 影响范围：共享 `DLevel2Ctrl` 及其直接/派生使用者 DLevel002、DLevel015、DLevel026。
  未修改 DrawLevelCtrl 的全局失败规则、FallArea prefab、数据、配置、存档、UI、资源地址、
  分析、广告或平台 SDK。
- 验证：使用 Unity 当前 Assembly-CSharp 响应文件执行完整 C# 编译，退出码 `0`；D026
  Prefab 静态检查确认四个 FallArea 均存在，且本地 fileID 无缺失或重复。PlayMode 首次进关、
  重开、D002/D015 回归与 Android/iOS 横屏设备验证仍为 `uncertain`。
- 风险：若未来新增的装置在 Intro 期间必须参与 2D 物理，本共享控制器的隔离策略不适用，
  应使用独立控制器而不是提前恢复刚体模拟。回滚时移除 `simulated=false`、
  `EnableHeldBallSimulation()` 及释放前的模拟兜底，并同步恢复相关关卡的安全入场路径。

## 18. 2026-09-17 Odin手动自动绑定Alien引用

- 需求：用户明确指定修改DLevel2Ctrl，增加Odin Inspector按钮，自动绑定指定Alien下的装置脚本与组件。此前需要手填或等运行时ResolveReferences补空，没有编辑器按钮。
- 改动仅为DLevel2Ctrl.cs中的UNITY_EDITOR导入和Editor Tools分区，新增公开方法AutoBindAlienReferences。没有修改原注释、既有运行时方法、Rig View代码、Prefab、配置、全局事件、资源加载、UI/存档/打点或Packages/GF。此次是组件编辑工具，不迁移已完成关卡。
- 用法：在DLevel2Ctrl的Alien字段拖入本关装置根，点击“自动绑定 Alien 引用”。按钮仅编辑模式可用，方法另有Application.isPlaying入口保护；Alien未指定时只警告，不写引用。
- 写入本关DrawLevelCtrl，以及Alien ActorSpineCtrl、SkeletonAnimation、夹子BoneFollower、clipSwingPivot、clipVisual的SkeletonAnimation与球Rigidbody2D。装置引用优先使用同一Alien上的DrawAlienClipBallRigView公开属性；属性缺失时按标准catch/clipSwingPivot/clipVisual及yellow_ball路径查找，不使用第一个任意子刚体。更换Alien后再次点击会覆盖旧装置引用，释放父节点仅在空时填Alien.parent或控制器Transform。
- 只设置引用，不调用运行时ResolveReferences、InitializeClipAttachment、CacheInitialState或RestoreInitialState；不初始化Spine、播放动画、改Transform/物理参数、访问DB或创建组件。名称/层级查找仅新增于用户明确要求的编辑器自动绑定入口，未增加运行时扫描。
- Undo.RecordObject记录本组件；SetDirty保留修改，Prefab实例记录Override。按钮不会自动保存Prefab、Apply到共享源Prefab或修改Rig View的内部字段；编辑后仍由用户保存当前场景/Prefab。
- 验证：使用当前Unity/游戏/Spine/UniTask/DOTween/Odin实际DLL独立编译Editor及Player两种条件，均退出0、0错误，各1项entranceDelay序列化字段未由代码赋值的CS0649警告。产物在C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel2-autobind-20260917。首次独立编译遗漏DOTween.Modules.dll导致原AsyncWaitForCompletion无法解析，补真实引用后两种编译通过；没有修改GF或原异步代码。
- 静态差异确认原运行时方法未改，新增按钮及Editor API在UNITY_EDITOR内，Player不包含按钮方法；Inspector未自动触发绑定。CodeGraph CLI不可用、引用feature plan缺失，知识库没有匹配自动绑定说明，依据当前本地结构记录、Editor态约束和相邻Odin/Undo范式实现。
- **uncertain**：未在用户主编辑器实际点击按钮或执行Undo、保存并重开Prefab。未实测继承控制器中的按钮显示、跨装置手动误绑的Rig View引用；按钮不验证动画名、bone2、入场路径或整关可玩性。缺组件只留下空引用并警告，不自动创建组件。非标准装置需完整Rig View引用或手动补齐。
- 手工验收：指定Alien并点击，核对六项装置引用及主控；改成另一套Alien再点击，确认旧引用被替换；Undo后恢复原引用，保存并重开Prefab确认Override；PlayMode按钮不可操作；实际入场/首笔放球/Retry仍按原流程试玩。Android/iOS运行时编译条件未新增逻辑，但整关/设备体验仍未实测。
- 回滚：只移除DLevel2Ctrl新增的两个编辑器using、Editor Tools按钮分区以及本节/日期主题。按钮写入的序列化引用按Undo或组件Override逐项回退，不用旧整份Prefab覆盖其它编辑。
